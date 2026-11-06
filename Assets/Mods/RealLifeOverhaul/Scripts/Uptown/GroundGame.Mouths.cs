using System.Collections.Generic;
using Parking.UndergroundParking;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.NewGround;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The mouths of the district's garage doors (docs/features/uptown.md, "Garage doors"): once its buildings stand, each warehouse's
    /// door that looks out on the street gets the ground a car or a truck crosses from the road to it made clear (MouthRules.Gate,
    /// over the pavement to a little past the kerb): the lamp posts, the bags of litter and the street furniture that stand in
    /// it put away, and the parking lanes that cross it cut short, laid so (<see cref="Populate"/>) or, where the parking is
    /// already there, laid again so. A building made in play (the construction game's) opens its own the same way, the moment it
    /// stands. Only a door a car can be taken in by has one (CopyGame.Wire keeps no other), so a warehouse costs the street the
    /// few metres its doors need and a shop with a garage door in its look, shut for good, costs it nothing. A big special place's
    /// mouths (BigPlacesRules) are found earlier, from fixed data, and keep their whole lane off.
    /// COST: nothing per frame; a pass over the copied modules' lamps and litter and the furniture at a load, and a lane laid again
    /// for a building made in play.
    /// </summary>
    public static partial class GroundGame
    {
        private const string LampsName = "StreetLights";     // a module's street lamps: each child, a lamp post with its light
        private const string LampPrefix = "SM_StreetLight";
        private const string CurvedSegment = "Lane Segment 0";   // the first piece a curved lane of the game's makes under itself
        private const float ParkMouthWide = 6f;              // m across the mouth of a car park's ramp
        private const float PostReach = MouthRules.PoleReach;   // m a street lamp's post reaches round its middle

        private sealed class LaneSlot
        {
            public ParkingLaneGenerator Template;   // the game's lane the module copy has at this place
            public Vector3 At;
            public Quaternion Turn;
            public bool Off;                        // kept off whole by a big special place's mouth
            public bool Gone;                       // a warehouse's mouths left no piece of it long enough to park a car in
            public readonly List<ParkingLaneGenerator> Made = new List<ParkingLaneGenerator>(2);   // the lane laid here: whole, or the pieces the mouths left
        }

        private static readonly List<GroundRect> GateMouths = new List<GroundRect>(64);   // where a car crosses into a warehouse or a car park (MouthRules.Gate)
        private static readonly List<LaneSlot> Slots = new List<LaneSlot>(256);
        private static readonly List<float> From = new List<float>(4);
        private static readonly List<float> To = new List<float>(4);
        private static readonly List<BoxCollider> Boxes = new List<BoxCollider>(4);
        private static readonly List<GroundRect> Fresh = new List<GroundRect>(8);

        /// <summary>The mouths of garage doors and car parks found at this load and since, in the world.</summary>
        public static int GateMouthCount => GateMouths.Count;

        /// <summary>
        /// The plan's parking lanes laid, whole or cut short by a mouth (a lane cut in two counts once), and those a mouth left no piece of;
        /// with the lanes a big place's mouth kept off, they are the plan's all (the district's census).
        /// </summary>
        public static int LanesLaid
        {
            get
            {
                int laid = 0;
                for (int i = 0; i < Slots.Count; i++)
                {
                    laid += Slots[i].Made.Count > 0 ? 1 : 0;
                }
                return laid;
            }
        }

        public static int LanesGone
        {
            get
            {
                int gone = 0;
                for (int i = 0; i < Slots.Count; i++)
                {
                    gone += Slots[i].Gone ? 1 : 0;
                }
                return gone;
            }
        }

        /// <summary>The lamp posts and bags of litter put away from the mouths, the parking lanes laid shorter, and the metres of kerb parking those lanes gave up.</summary>
        public static int LampsAway { get; private set; }
        public static int GateLitterAway { get; private set; }
        public static int LanesTrimmed { get; private set; }
        public static int ParkingMetresAway { get; private set; }

        /// <summary>The mouths found since the district was laid, into the caller's list (not cleared): for the checks and the census.</summary>
        public static void GateMouthsInto(List<GroundRect> into)
        {
            into.AddRange(GateMouths);
        }

        /// <summary>
        /// Opens the mouths of the garage doors and car parks these made buildings keep (a door the game's own warehouse would take a
        /// car in by: the copy's list of them, CopyGame.Wire's): what stands in each mouth is put away, and a parking lane that crosses
        /// it is cut short, at once if the parking is laid already. Returns how many mouths were found. Buildings of other features,
        /// and a special place copied whole, have theirs found elsewhere or none.
        /// </summary>
        public static int OpenMouths(IList<MadeBuilding> buildings)
        {
            int first = GateMouths.Count;
            for (int i = 0; buildings != null && i < buildings.Count; i++)
            {
                MouthsOf(buildings[i], GateMouths);
            }
            if (GateMouths.Count == first)
            {
                return 0;
            }
            Fresh.Clear();
            for (int i = first; i < GateMouths.Count; i++)
            {
                Fresh.Add(GateMouths[i]);
            }
            LampsAway += PutAwayLamps();
            GateLitterAway += PutAwayLitter();
            StreetsGame.Open(GateMouths);
            if (Slots.Count > 0)
            {
                Relay();
            }
            int found = GateMouths.Count - first;
            Fresh.Clear();
            return found;
        }

        // The mouth of each door a made building keeps, and of its car park's ramp, appended to the list.
        private static void MouthsOf(MadeBuilding made, List<GroundRect> into)
        {
            CityBuildingController c = made != null && made.Shown && made.Spec.Life ? made.Controller : null;
            if (c == null)
            {
                return;
            }
            if (made.DesignIndex >= 0 && made.DesignIndex < CopyGame.Catalogue.Count && CopyGame.Catalogue[made.DesignIndex].Whole)
            {
                return;   // a special place copied whole keeps its own (BigPlacesRules)
            }
            DriveInEntrance[] doors = c.driveInEntrances;
            for (int i = 0; doors != null && i < doors.Length; i++)
            {
                if (doors[i] == null)
                {
                    continue;
                }
                Transform t = doors[i].transform;
                MouthRules.Gate(t.position.x, t.position.z, WayOf(t.forward), WidthOf(doors[i]), into);
            }
            UndergroundParkingEntrance park = c.undergroundParkingEntrance;
            if (park != null && park.vehicleExitPoint != null)
            {
                Transform exit = park.vehicleExitPoint;
                MouthRules.Gate(exit.position.x, exit.position.z, WayOf(exit.forward), ParkMouthWide, into);
            }
        }

        // 0 north (+Z), 1 east, 2 south, 3 west: the way a forward vector points on the district's grid.
        private static int WayOf(Vector3 forward)
        {
            return Mathf.Abs(forward.z) >= Mathf.Abs(forward.x) ? (forward.z >= 0f ? 0 : 2) : (forward.x >= 0f ? 1 : 3);
        }

        // A garage door's width in the world: its closed door's collider across (the first that is no trigger), else the game's 3.25 m.
        private static float WidthOf(DriveInEntrance door)
        {
            Boxes.Clear();
            door.GetComponents(Boxes);
            float width = 3.25f;
            for (int i = 0; i < Boxes.Count; i++)
            {
                if (!Boxes[i].isTrigger)
                {
                    width = Boxes[i].size.x * Mathf.Abs(door.transform.lossyScale.x);
                    break;
                }
            }
            Boxes.Clear();
            return width;
        }

        // The lamp posts of the copied streets that stand in a mouth, put away (a crossing's light poles hang from the crossing, not here).
        private static int PutAwayLamps()
        {
            int away = 0;
            for (int i = 0; i < Copies.Count; i++)
            {
                Transform lamps = Copies[i] != null ? Copies[i].transform.Find(LampsName) : null;
                if (lamps == null)
                {
                    continue;
                }
                for (int k = 0; k < lamps.childCount; k++)
                {
                    Transform lamp = lamps.GetChild(k);
                    if (lamp.gameObject.activeSelf && lamp.name.StartsWith(LampPrefix, System.StringComparison.Ordinal)
                        && MouthRules.Holds(GateMouths, lamp.position.x, lamp.position.z, PostReach))
                    {
                        lamp.gameObject.SetActive(false);
                        away++;
                    }
                }
            }
            return away;
        }

        // The modules' bags of litter that lie in a mouth, put away as a big place's are.
        private static int PutAwayLitter()
        {
            int away = 0;
            for (int i = 0; i < Copies.Count; i++)
            {
                Transform litter = Copies[i] != null ? Copies[i].transform.Find(LitterName) : null;
                if (litter == null)
                {
                    continue;
                }
                Collider[] solid = litter.GetComponentsInChildren<Collider>(false);
                for (int k = 0; k < solid.Length; k++)
                {
                    Vector3 at = solid[k].transform.position;
                    if (MouthRules.Holds(GateMouths, at.x, at.z, 0f))
                    {
                        solid[k].gameObject.SetActive(false);
                        away++;
                    }
                }
            }
            return away;
        }

        // ---------------------------------------------------------------- the parking lanes

        // One of the template's lanes at its place on a copy of the module: laid whole where no mouth meets it, else as the pieces the
        // mouths leave (each a lane of its own). Returns how many lanes were laid, <refused> how many could not be (the first refusal
        // is said in <why>); a piece the mouths left too short to park a car in is neither.
        private static int LayLane(LaneSlot slot, ref string why, out int refused)
        {
            refused = 0;
            ParkingLaneGenerator template = slot.Template;
            var box = template.GetComponent<BoxCollider>();
            if (box != null && GateMouths.Count > 0 && Geometry(slot, box, out float start, out float end, out float crossMin, out float crossMax, out bool alongX, out float middle, out float sign)
                && MouthRules.Meets(start, end, crossMin, crossMax, alongX, GateMouths))
            {
                int pieces = box.size.z >= box.size.x && template.transform.Find(CurvedSegment) == null
                    ? MouthRules.Pieces(start, end, crossMin, crossMax, alongX, GateMouths, From, To) : 0;
                LanesTrimmed++;
                slot.Gone = pieces == 0;
                float kept = 0f;
                int laid = 0;
                for (int p = 0; p < pieces; p++)
                {
                    float from = sign > 0f ? From[p] - middle : middle - To[p], to = sign > 0f ? To[p] - middle : middle - From[p];
                    ParkingLaneGenerator made = null;
                    string refusal = null;
                    if (ParkingGame.Room(template, slot.At, slot.Turn, from, to, out refusal))
                    {
                        made = ParkingGame.Copy(template, slot.At, slot.Turn, from, to, out refusal);
                    }
                    if (made != null)
                    {
                        Lanes.Add(made);
                        slot.Made.Add(made);
                        kept += To[p] - From[p];
                        laid++;
                        continue;
                    }
                    refused++;
                    why = why ?? template.name + ": " + refusal;
                }
                ParkingMetresAway += Mathf.Max(0, Mathf.RoundToInt(end - start - kept));
                return laid;
            }
            string whole = null;
            ParkingLaneGenerator lane = ParkingGame.Room(template, slot.At, slot.Turn, out whole) ? ParkingGame.Copy(template, slot.At, slot.Turn, out whole) : null;
            if (lane == null)
            {
                refused = 1;
                why = why ?? template.name + ": " + whole;
                return 0;
            }
            Lanes.Add(lane);
            slot.Made.Add(lane);
            return 1;
        }

        // A lane's place in the world on the axes: its two ends along the axis it runs on, the band across it, the middle and the
        // way its own forward points (+1 along the axis, -1 against it). False for a lane not along an axis.
        private static bool Geometry(LaneSlot slot, BoxCollider box, out float start, out float end, out float crossMin, out float crossMax, out bool alongX, out float middle, out float sign)
        {
            Vector3 scale = slot.Template.transform.lossyScale;
            Vector3 centre = slot.At + slot.Turn * Vector3.Scale(box.center, scale);
            Vector3 axis = slot.Turn * Vector3.forward;
            alongX = Mathf.Abs(axis.x) > Mathf.Abs(axis.z);
            float half = box.size.z * Mathf.Abs(scale.z) * 0.5f, wide = box.size.x * Mathf.Abs(scale.x) * 0.5f;
            middle = alongX ? centre.x : centre.z;
            float across = alongX ? centre.z : centre.x;
            sign = (alongX ? axis.x : axis.z) >= 0f ? 1f : -1f;
            start = middle - half;
            end = middle + half;
            crossMin = across - wide;
            crossMax = across + wide;
            return Mathf.Abs(Mathf.Abs(alongX ? axis.x : axis.z) - 1f) < 0.01f;
        }

        // A lane already laid that a new mouth crosses: taken away, its cars back to the game's pool, and laid again as the mouths leave it.
        private static void Relay()
        {
            string why = null;
            for (int s = 0; s < Slots.Count; s++)
            {
                LaneSlot slot = Slots[s];
                var box = slot.Off ? null : slot.Template.GetComponent<BoxCollider>();
                if (box == null || slot.Made.Count == 0 || !Geometry(slot, box, out float start, out float end, out float crossMin, out float crossMax, out bool alongX, out _, out _)
                    || !MouthRules.Meets(start, end, crossMin, crossMax, alongX, Fresh))
                {
                    continue;
                }
                for (int m = 0; m < slot.Made.Count; m++)
                {
                    ParkingLaneGenerator lane = slot.Made[m];
                    Lanes.Remove(lane);
                    ParkingGame.Remove(ref lane);
                }
                slot.Made.Clear();
                slot.Gone = false;
                LayLane(slot, ref why, out _);
            }
        }
    }
}
