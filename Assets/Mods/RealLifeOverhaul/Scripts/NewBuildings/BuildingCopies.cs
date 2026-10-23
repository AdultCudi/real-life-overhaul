using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>A decorative copy asked for by a feature: where it is to stand, and the copy once it stands.</summary>
    public sealed class CopyHandle
    {
        public readonly string Design;   // the design's name (DesignEntry.Name); null for one picked to fit
        public readonly string Size;     // the size it is listed under; null for the first of its name
        public readonly Vector3 At;      // where its root stands (a building's or a plot's middle for those)
        public readonly float Yaw;       // degrees: its root's turn (the way a building's or a plot's front faces, for those)
        public readonly string Owner;    // the feature that asked, for the log

        internal CopyLook Look;
        internal bool Done;
        internal bool OverLot;           // stood on Lot (a building's own place, or a plot), not at At
        internal MadeLot Lot;
        internal float DoorGround;       // m: the height of the ground at that building's door, or the plot's middle
        internal CityBuildingController Over;   // the building whose place it takes; null for a plot or a place
        internal bool HideOwn;           // Over's own look hidden in the frame the copy stands, shown again as it goes
        internal bool HidOwn;            // that look is hidden now, by this copy
        internal bool AlignDoor;         // its door moved along the front to where the building's door is
        internal float DoorAcross;       // m right of the lot's middle (looking out) where that door is
        internal string Other;           // the building's own design, never picked in its place
        internal string Key;             // what a design is picked by when none is named: the same on every machine
        internal bool Leaving;           // to be taken away once out of view
        internal float OwnHeight;        // m: the building's own height, for the box a change must be out of view by
        internal int Planned = -1;       // the design it wears, once the city's designs are known
        internal Vector3 PlannedAt;
        internal float PlannedYaw;
        internal Bounds Box;             // what must be out of view for it to stand or go
        internal float DoorMissed;       // m its door is short of the building's own door

        internal CopyHandle(string design, string size, Vector3 at, float yaw, string owner)
        {
            Design = design;
            Size = size;
            At = at;
            Yaw = yaw;
            Owner = owner;
            Wears = design;
        }

        /// <summary>True while the copy stands.</summary>
        public bool Ready => Look != null;

        /// <summary>True once it was taken away, or could not be made: <see cref="Why"/> says which.</summary>
        public bool Gone => Done && Look == null;

        /// <summary>Why it is gone or was never made (English, for the log), or null.</summary>
        public string Why { get; internal set; }

        /// <summary>The copy's root while it stands (its colliders, for a wrap round it), or null.</summary>
        public Transform Root => Look != null ? Look.transform : null;

        /// <summary>The design it wears once it stands (the one picked, when none was named); before that, the one asked for.</summary>
        public string Wears { get; internal set; }

        /// <summary>The building whose place it takes, or null.</summary>
        public CityBuildingController Building => Over;
    }

    /// <summary>
    /// Decorative copies of the game's own buildings for other features, a construction site's finished look first of all
    /// (docs/features/new-buildings.md, "Decorative copies"):
    ///
    ///     CopyHandle look = BuildingCopies.PlaceOver(building, null, null, "civic", true);   // a rebuilt building's new look
    ///     BuildingCopies.RemoveOutOfView(look);                                            // its own look back again
    ///     CopyHandle built = BuildingCopies.PlaceOnPlot(null, null, middle, yaw, width, depth, "civic");   // a plot's building
    ///
    /// Every change a player could see is made out of the camera's view with the city map shut, one copy a frame, or behind
    /// the loading screen when asked for during a load. A copy never keeps the game's controller, so it can be taken away at
    /// any time. Switching the option off takes every copy away and shows every hidden look again at once; a handle then
    /// says <see cref="CopyHandle.Gone"/>. The copies are what this machine shows: a feature shared in Going Public asks for
    /// the same copy on every machine, and a design picked to fit is picked the same on every one. Costs nothing a frame
    /// while nothing waits.
    /// </summary>
    public static class BuildingCopies
    {
        internal static readonly List<CopyHandle> Waiting = new List<CopyHandle>(8);
        internal static readonly List<CopyHandle> Standing = new List<CopyHandle>(16);
        internal static readonly List<CopyHandle> Leaving = new List<CopyHandle>(4);
        internal static readonly List<CityBuildingController> ToHide = new List<CityBuildingController>(4);   // looks hidden or shown out of view
        internal static readonly List<CityBuildingController> ToShow = new List<CityBuildingController>(4);

        /// <summary>True while copies can be asked for: the option is on and a city is loaded.</summary>
        public static bool Available => Settings.NewBuildings && NewBuildingsDirector.Instance != null;

        /// <summary>True while a change waits: a copy to stand or to go, a look to hide or show.</summary>
        internal static bool Pending => Waiting.Count + Leaving.Count + ToHide.Count + ToShow.Count > 0;

        /// <summary>A copy of a design (by name, and size where one name stands for several) to stand at a place; null while not available.</summary>
        public static CopyHandle Place(string design, string size, Vector3 at, float yaw, string owner)
        {
            if (!Available || string.IsNullOrEmpty(design))
            {
                return null;
            }
            var handle = new CopyHandle(design, size, at, yaw, owner);
            Ask(handle);
            return handle;
        }

        /// <summary>
        /// A copy to stand where one of the game's buildings stands, its front on the same line and its door where the
        /// building's own is (as far as its footprint may move), at the height of the ground at that door: a rebuilt
        /// building's new look. A design by name, or null for one that fits the building's place, other than its own, picked
        /// by its address. With <paramref name="hideOwn"/> the building's own look is hidden in the frame the copy stands
        /// (its renderers, its box, its city map look, its signs' lights), and a hover or a click on the copy is one on the
        /// building. Null while not available, or if the building cannot be measured.
        /// </summary>
        public static CopyHandle PlaceOver(CityBuildingController building, string design, string size, string owner, bool hideOwn)
        {
            if (!Available || building == null)
            {
                return null;
            }
            DesignEntry own = CopyGame.DescribeOne(building, out _);
            if (own == null)
            {
                return null;
            }
            NewBuildingsRules.DoorFrame(own, out float uMin, out float uMax, out float vMin, out float vMax);
            float yaw = building.transform.eulerAngles.y;
            float facing = NewBuildingsRules.Turn(yaw + own.DoorFacing);
            // The building's own footprint as a lot: its middle, its width and depth from its door's side.
            float ux = (uMin + uMax) * 0.5f, vz = (vMin + vMax) * 0.5f;
            float f = facing * Mathf.Deg2Rad;
            Vector3 root = building.transform.position;
            var lot = new MadeLot(root.x + ux * Mathf.Cos(f) + vz * Mathf.Sin(f), root.z - ux * Mathf.Sin(f) + vz * Mathf.Cos(f), facing, uMax - uMin, vMax - vMin);
            Vector3 door = building.entranceDoors[0].doorTransform.position;
            string key = building.building != null ? building.building.StreetName + "#" + building.building.StreetNumber.ToString(CultureInfo.InvariantCulture) : own.Name;
            var handle = new CopyHandle(design, size, new Vector3(lot.X, root.y, lot.Z), facing, owner)
            {
                OverLot = true, Lot = lot, DoorGround = door.y, Over = building, HideOwn = hideOwn, AlignDoor = true,
                DoorAcross = (door.x - lot.X) * Mathf.Cos(f) - (door.z - lot.Z) * Mathf.Sin(f), Other = own.Name, Key = key, OwnHeight = own.Height
            };
            Ask(handle);
            return handle;
        }

        /// <summary>
        /// A finished building on a plot's ground, given as CivicWorks.StartPlot has it: its middle on the ground, the land's
        /// turn (its front at -Z, as PlotRules has it), its width and its depth in metres. The design stands with its front on
        /// the land's front, in the middle of its width; a design by name, or null for one that fits, picked by the plot's
        /// place. Pass the frame's footprint (PlotRules.Frame) for a building where the frame stood. Null while not available.
        /// </summary>
        public static CopyHandle PlaceOnPlot(string design, string size, Vector3 middle, int yaw, int width, int depth, string owner)
        {
            if (!Available || width <= 0 || depth <= 0)
            {
                return null;
            }
            float facing = NewBuildingsRules.Turn(yaw + 180f);
            string key = "plot:" + Mathf.RoundToInt(middle.x).ToString(CultureInfo.InvariantCulture) + ":" + Mathf.RoundToInt(middle.z).ToString(CultureInfo.InvariantCulture)
                         + ":" + ((yaw % 360 + 360) % 360).ToString(CultureInfo.InvariantCulture);
            var handle = new CopyHandle(design, size, middle, facing, owner)
            {
                OverLot = true, Lot = new MadeLot(middle.x, middle.z, facing, width, depth), DoorGround = middle.y, Key = key
            };
            Ask(handle);
            return handle;
        }

        private static void Ask(CopyHandle handle)
        {
            Waiting.Add(handle);
            NewBuildingsDirector.Instance.Wake();
        }

        /// <summary>
        /// Hides one of the game's buildings' own look (as <see cref="PlaceOver"/> does); its door, colliders and card keep
        /// working. Now, or with <paramref name="outOfView"/> once it is out of the camera's view. False if hidden already or
        /// not available.
        /// </summary>
        public static bool HideLook(CityBuildingController building, bool outOfView = false)
        {
            if (!Available || building == null || CopyGame.LookHidden(building))
            {
                return false;
            }
            if (!outOfView)
            {
                return CopyGame.HideLook(building);
            }
            ToShow.Remove(building);
            if (!ToHide.Contains(building))
            {
                ToHide.Add(building);
                NewBuildingsDirector.Instance.Wake();
            }
            return true;
        }

        /// <summary>Shows a look hidden by <see cref="HideLook"/> again, now or once out of view. False if it was not hidden.</summary>
        public static bool ShowLook(CityBuildingController building, bool outOfView = false)
        {
            if (building == null)
            {
                return false;
            }
            if (ToHide.Remove(building))
            {
                return true;   // asked to be hidden out of view, and not hidden yet
            }
            if (!CopyGame.LookHidden(building))
            {
                return false;
            }
            if (!outOfView || NewBuildingsDirector.Instance == null)
            {
                return CopyGame.ShowLook(building);
            }
            if (!ToShow.Contains(building))
            {
                ToShow.Add(building);
                NewBuildingsDirector.Instance.Wake();
            }
            return true;
        }

        /// <summary>True while that building's own look is hidden.</summary>
        public static bool LookHidden(CityBuildingController building) => CopyGame.LookHidden(building);

        /// <summary>The copy standing (or waiting to) where that building stands, or null: a wrap round the building should wrap it instead.</summary>
        public static CopyHandle Over(CityBuildingController building)
        {
            for (int i = 0; building != null && i < Standing.Count; i++)
            {
                if (Standing[i].Over == building && !Standing[i].Leaving)
                {
                    return Standing[i];
                }
            }
            for (int i = 0; building != null && i < Waiting.Count; i++)
            {
                if (Waiting[i].Over == building)
                {
                    return Waiting[i];
                }
            }
            return null;
        }

        /// <summary>Takes a copy away now, whatever is in view, or forgets one not made yet; a look it hid is shown again.</summary>
        public static void Remove(CopyHandle handle)
        {
            if (handle == null)
            {
                return;
            }
            Waiting.Remove(handle);
            Leaving.Remove(handle);
            handle.Leaving = false;
            if (Standing.Remove(handle))
            {
                MadeBuildings.Changed();   // the minimap draws the copies too
            }
            if (handle.HidOwn)
            {
                CopyGame.ShowLook(handle.Over);
                handle.HidOwn = false;
            }
            CopyGame.Remove(handle.Look);
            handle.Look = null;
            handle.Done = true;
            handle.Why = handle.Why ?? "taken away by " + (handle.Owner ?? "its feature");
        }

        /// <summary>Takes a copy away once it is out of view, a look it hid shown again in the same frame; one not made yet is forgotten now.</summary>
        public static void RemoveOutOfView(CopyHandle handle)
        {
            if (handle == null)
            {
                return;
            }
            if (Waiting.Contains(handle) || handle.Look == null || NewBuildingsDirector.Instance == null)
            {
                Remove(handle);
                return;
            }
            if (!handle.Leaving)
            {
                handle.Leaving = true;
                Leaving.Add(handle);
                NewBuildingsDirector.Instance.Wake();
            }
        }

        /// <summary>The box of every copy standing, into the caller's list (not cleared): the minimap draws them.</summary>
        public static void Boxes(List<MeshRenderer> into)
        {
            for (int i = 0; i < Standing.Count; i++)
            {
                CopyLook look = Standing[i].Look;
                if (look != null && look.Box != null)
                {
                    into.Add(look.Box);
                }
            }
        }

        /// <summary>A copy the tests made themselves, counted among the copies standing (the minimap draws it) until taken away.</summary>
        internal static CopyHandle Stood(CopyLook look, string design, string size, Vector3 at, float yaw, string owner)
        {
            var handle = new CopyHandle(design, size, at, yaw, owner) { Look = look, Done = true };
            Standing.Add(handle);
            MadeBuildings.Changed();
            return handle;
        }

        /// <summary>Every copy taken away and every hidden look shown, at once (the option off, the city going); how many copies.</summary>
        internal static int RemoveAll(string why)
        {
            int removed = Standing.Count + Waiting.Count;
            for (int i = Standing.Count - 1; i >= 0; i--)
            {
                CopyHandle h = Standing[i];
                h.Why = why;
                Remove(h);
            }
            for (int i = Waiting.Count - 1; i >= 0; i--)
            {
                CopyHandle h = Waiting[i];
                h.Why = why;
                Remove(h);
            }
            Leaving.Clear();
            ToHide.Clear();
            ToShow.Clear();
            CopyGame.ShowAllLooks();
            return removed;
        }
    }
}
