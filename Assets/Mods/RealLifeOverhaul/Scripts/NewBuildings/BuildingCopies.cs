using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>A decorative copy asked for by a feature: where it is to stand, and the copy once it stands.</summary>
    public sealed class CopyHandle
    {
        public readonly string Design;   // the design's name (DesignEntry.Name)
        public readonly string Size;     // the size it is listed under; null for the first of its name
        public readonly Vector3 At;      // where its root stands
        public readonly float Yaw;       // degrees: its root's turn
        public readonly string Owner;    // the feature that asked, for the log

        internal CopyLook Look;
        internal bool Done;
        internal bool OverLot;     // stood on Lot (a building's own footprint, its front on the lot's front), not at At
        internal MadeLot Lot;
        internal float DoorGround; // m: the height of the ground at that building's door, where the copy's door goes

        internal CopyHandle(string design, string size, Vector3 at, float yaw, string owner)
        {
            Design = design;
            Size = size;
            At = at;
            Yaw = yaw;
            Owner = owner;
        }

        /// <summary>True while the copy stands.</summary>
        public bool Ready => Look != null;

        /// <summary>True once it was taken away, or could not be made: <see cref="Why"/> says which.</summary>
        public bool Gone => Done && Look == null;

        /// <summary>Why it is gone or was never made (English, for the log), or null.</summary>
        public string Why { get; internal set; }

        /// <summary>The copy's root while it stands, or null.</summary>
        public Transform Root => Look != null ? Look.transform : null;
    }

    /// <summary>
    /// Decorative copies of the game's own buildings for other features, a construction site's finished look first of all:
    ///
    ///     CopyHandle look = BuildingCopies.PlaceOver(building, "SM_FlatironAreaBuilding_02", null, "civic");   // made within a few frames
    ///     BuildingCopies.HideLook(building);    // the building's own look hidden, its door, colliders and card still working
    ///     BuildingCopies.ShowLook(building);    // and shown again
    ///     BuildingCopies.Remove(look);          // the copy taken away
    ///
    /// A copy is made one a frame (a building is a few milliseconds to copy), behind the loading screen if asked for during
    /// a load; the first copy in play waits while the city's designs are measured, a millisecond a frame. A copy never
    /// keeps the game's controller, so it can be taken away at any time. Switching the option off takes every copy away and
    /// shows every hidden look again; a handle then says <see cref="CopyHandle.Gone"/>. The copies are what this machine
    /// shows: a feature shared in Going Public asks for the same copy on every machine. Costs nothing a frame.
    /// </summary>
    public static class BuildingCopies
    {
        internal static readonly List<CopyHandle> Waiting = new List<CopyHandle>(8);
        internal static readonly List<CopyHandle> Standing = new List<CopyHandle>(16);

        /// <summary>True while copies can be asked for: the option is on and a city is loaded.</summary>
        public static bool Available => Settings.NewBuildings && NewBuildingsDirector.Instance != null;

        /// <summary>A copy of a design (by name, and size where one name stands for several) to stand at a place; null while not available.</summary>
        public static CopyHandle Place(string design, string size, Vector3 at, float yaw, string owner)
        {
            if (!Available || string.IsNullOrEmpty(design))
            {
                return null;
            }
            var handle = new CopyHandle(design, size, at, yaw, owner);
            Waiting.Add(handle);
            NewBuildingsDirector.Instance.Wake();
            return handle;
        }

        /// <summary>
        /// A copy of a design to stand where one of the game's buildings stands, its door on the same side, its front on the
        /// same line: a construction site's finished look. Null while not available or if the building cannot be measured.
        /// </summary>
        public static CopyHandle PlaceOver(CityBuildingController building, string design, string size, string owner)
        {
            if (!Available || building == null || string.IsNullOrEmpty(design))
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
            var handle = new CopyHandle(design, size, new Vector3(lot.X, root.y, lot.Z), facing, owner)
            {
                OverLot = true, Lot = lot, DoorGround = building.entranceDoors[0].doorTransform.position.y
            };
            Waiting.Add(handle);
            NewBuildingsDirector.Instance.Wake();
            return handle;
        }

        /// <summary>Hides one of the game's buildings' own look; its door, colliders and card keep working. False if hidden already or not available.</summary>
        public static bool HideLook(CityBuildingController building)
        {
            return Available && CopyGame.HideLook(building);
        }

        /// <summary>Shows a look hidden by <see cref="HideLook"/> again. False if it was not hidden.</summary>
        public static bool ShowLook(CityBuildingController building)
        {
            return CopyGame.ShowLook(building);
        }

        /// <summary>Takes a copy away, or forgets one not made yet.</summary>
        public static void Remove(CopyHandle handle)
        {
            if (handle == null)
            {
                return;
            }
            Waiting.Remove(handle);
            if (Standing.Remove(handle))
            {
                MadeBuildings.Changed();   // the minimap draws the copies too
            }
            CopyGame.Remove(handle.Look);
            handle.Look = null;
            handle.Done = true;
            handle.Why = handle.Why ?? "taken away by " + (handle.Owner ?? "its feature");
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

        /// <summary>Every copy taken away and every hidden look shown (the option off, the city going); how many copies.</summary>
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
            CopyGame.ShowAllLooks();
            return removed;
        }
    }
}
