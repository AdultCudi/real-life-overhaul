using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// One of the game's building designs as the copy engine knows it: a building of the city as it was placed in the
    /// scenes, measured round its own root in the root's own turn (metres), and the building of the game's it was copied
    /// from (by the caller's index). One entry per design and size, so that a made building of a size gets a design a
    /// building of that size wears. Its footprint is all the ground it takes: its box (the game's shadow and map look) and
    /// every solid part and every drawn part reaching below head height round it (steps, porches, bays, scaffolding), as
    /// <see cref="NewBuildingsRules.Begin"/> and <see cref="NewBuildingsRules.Part"/> measure it.
    /// </summary>
    public sealed class DesignEntry
    {
        public string Name;         // the scene object's name with its copy number taken off ("SM_Hell's KitchenBuilding11")
        public string Size;         // the size id of the game's building it stands for ("ba:buildingsize_c")
        public int Version;         // that building's version
        public string Type;         // that building's type id
        public int Uses;            // buildings of the city of this size wearing it
        public bool Batched;        // drawn in a static batch with its neighbours: a copy would draw at the original's place
        public float MinX, MaxX;    // m: its footprint across, round its root
        public float MinZ, MaxZ;    // m: its footprint along its root's forward
        public float Height;        // m: from its root up to its highest point
        public float DoorX, DoorZ;  // m: its first door round its root
        public float DoorY;         // m: that door's height above its root: the root stands this far below the ground at the door
        public float DoorFacing;    // degrees: the way its first door faces out, from its root's forward, squared to the root's nearest right angle
        public int Source;          // the caller's index of the game's building it is copied from
        public bool Whole;          // one of the game's special buildings copied whole, its workings with it (NewBuildingsRules.Whole): only ever named, never picked by size
        public float Front = -1f;   // degrees from its root's forward of the side stood to the street; below 0, its door's (NewBuildingsRules.FrontOf)
        public float BoxMinX, BoxMaxX, BoxMinZ, BoxMaxZ;   // m: its box alone, round its root: what a part straying from it is measured against
        public bool Strays;         // a part of it stands more than StrayMost outside its box (a collider of another place under its root): never copied
    }

    /// <summary>
    /// A place a made building may stand: a rectangle of open ground with one side on a street. A design is stood in it
    /// with its door on that side, in the middle of it and its front on the lot's front line; a lot may instead stand it
    /// <see cref="Inset"/> back from that line and against one of its sides (<see cref="Side"/>), as a special place copied
    /// whole keeps the forecourt or car park the game's own has in front of it or beside it.
    /// </summary>
    public struct MadeLot
    {
        public float X;        // m: the lot's middle, in the world
        public float Z;
        public float Facing;   // degrees round the vertical, as the game turns things (0 = +Z): the way the street lies from the lot
        public float Width;    // m: along the street
        public float Depth;    // m: back from the street
        public float Inset;    // m the design's front stands back from the lot's front line; 0 on it
        public int Side;       // where across the lot it stands, looking out to the street: 0 in the middle, -1 against its left side, +1 its right
        public float SideGap;  // m between that side of the lot and the design's (with Side not 0)

        public MadeLot(float x, float z, float facing, float width, float depth)
        {
            X = x;
            Z = z;
            Facing = facing;
            Width = width;
            Depth = depth;
            Inset = 0f;
            Side = 0;
            SideGap = 0f;
        }
    }

    /// <summary>
    /// One of the game's special buildings whose workings are part of its look (a drive-in store's garage doors and their
    /// triggers, a filling station's pumps, triggers and repair bays, all under its own controller), so that a special
    /// place copies it whole: its design's name and size, and the side of it a place stands to its street.
    /// </summary>
    public struct WholeDesign
    {
        public readonly string Name;
        public readonly string Size;
        public readonly float Front;   // degrees from its root's forward; below 0, its door's side

        public WholeDesign(string name, string size, float front)
        {
            Name = name;
            Size = size;
            Front = front;
        }
    }

    /// <summary>
    /// A building of the mod's own as a feature declares it (<c>MadeBuildings.Declare</c>): its address on a street
    /// of the mod's own, the game's size, version and type of its interior, its district, its look (a design by name, or one
    /// a building of its size wears) and where it may stand. Nothing of it is saved; it is made again at every load.
    /// </summary>
    public sealed class MadeSpec
    {
        public string Id;          // the feature's own name for it, also its hold in the special buildings' book
        public string Street;      // a street of the mod's own (MadeStreets), never one of the game's
        public int Number;         // its number in that street
        public string Size;        // the game's size id: the interior is the game's own structure of this size and version
        public int Version;
        public string Type;        // the game's building type id; the special one only with a Service (its towing reads one on every special-type building)
        public string District;    // the game's district id it is filed under
        public string Design;      // a design by name (DesignEntry.Name), or null for one a building of its size wears
        public MadeLot[] Lots;     // where it may stand, in order: the next free one after the preferred one is taken
        public int Preferred;      // the lot asked for first
        public int Traffic;        // the building's traffic index, 0 to 100, as the game's buildings have one
        public int Floors = 1;     // storeys of floor area its record has: the floors core gives a host that many floors
        public Func<bool> Wanted;  // the declaring feature's own option; null for always
        public bool Life;          // a district's building in the game's own simulation: its registration the save's (the district save keeps it), never claimed
        public bool DesignArea;    // its record's floor area that of the game's building its design copies (it is as large as it looks), not its size's times Floors
        public MadeStructure[] Others;   // structures to take in order when no design of its own size fits a lot (a district's lot takes any of its class's); null for none
        public bool Whole;         // its Design is one of the game's special buildings copied whole, its workings with it (NewBuildingsRules.Whole)
        // A place of the game's own kind (a bank, an agency, a store, an importer): a copy of one of the game's special services
        // on its record, as the game's special buildings have theirs. Only a district's building (Life) takes one.
        public string Service;         // the asset name of the game's SpecialService copied (its own buildings carry the original); null for none
        public string ServiceName;     // the copy's business name, its contact's key with its type; null keeps the game's own: a branch of that business
        public string ServiceEmployer; // the boss's name of a job the service offers (its contact's key with the type): the game's own boss is another place's
        public string[] ServiceGoods;  // an importer's own goods, its own settings with this list; null shares the game's settings
        public string PlainType;       // the type it stands as where it may not take the service (a player holds its lot); null: it then stands down
    }

    /// <summary>What a district building's registration is to the game's service its declaration copies (<see cref="NewBuildingsRules.ServiceOf"/>).</summary>
    public enum ServiceVerdict
    {
        Held,     // the player's (rented or owned), so another player's in a session too: it stays an ordinary building
        Theirs,   // the service's already: its type and name the copy's
        Fill      // new at this load, empty, or an AI business of the game's: filled as the game fills a special building's
    }

    /// <summary>One of the game's structures a made building may take instead of its own: a size, a version and a type.</summary>
    public struct MadeStructure
    {
        public string Size;
        public int Version;
        public string Type;
    }

    /// <summary>A street of the mod's own: its id, the locale key of its name, its short name for the game's help, avenue or not.</summary>
    public sealed class MadeStreet
    {
        public readonly string Id;
        public readonly string NameKey;
        public readonly string Abbreviation;
        public readonly bool Avenue;

        public MadeStreet(string id, string nameKey, string abbreviation, bool avenue)
        {
            Id = id;
            NameKey = nameKey;
            Abbreviation = abbreviation;
            Avenue = avenue;
        }
    }

    /// <summary>
    /// The streets of the mod's own. Their ids carry the mod's prefix, which no street of the game's can have, so a free
    /// update that puts buildings in a street the game left empty can never take a made building's address, and every
    /// address a made building ever had is told by its street alone (the save guard needs nothing else). The game shows
    /// them by name once they are among its own streets (<c>MadeGame.AddStreets</c>, at every load).
    /// </summary>
    public static class MadeStreets
    {
        public const string Prefix = "reallife:street_";   // the game's own begin "ba:street_"

        public static readonly MadeStreet[] All = WithDistricts(new[]
        {
            new MadeStreet(Prefix + "provinglane", NewBuildingsText.StreetProvingLane, "PRL", false),
        });

        // The mod's own streets, then those of its districts (Uptown's, Scripts/Uptown/LayoutStreets.cs), so a building on a
        // district's lot has an address the game writes and finds.
        private static MadeStreet[] WithDistricts(MadeStreet[] own)
        {
            Uptown.LayoutStreet[] uptown = Uptown.LayoutStreets.All;
            var all = new MadeStreet[own.Length + uptown.Length];
            own.CopyTo(all, 0);
            for (int i = 0; i < uptown.Length; i++)
            {
                all[own.Length + i] = new MadeStreet(uptown[i].Id, uptown[i].NameKey, uptown[i].Abbreviation, uptown[i].Avenue);
            }
            return all;
        }

        /// <summary>True for a street of the mod's own, whether or not a building of it stands in this city. A string test.</summary>
        public static bool IsMine(string street)
        {
            return street != null && street.StartsWith(Prefix, StringComparison.Ordinal);
        }

        /// <summary>The street of that id, or null.</summary>
        public static MadeStreet Find(string id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i].Id, id, StringComparison.Ordinal))
                {
                    return All[i];
                }
            }
            return null;
        }
    }

    /// <summary>
    /// The rules of the mod's own buildings, game-free: the name of a design, which design a made building wears, where it
    /// stands on its lot and whether that lot is clear, which lot it takes when the one it asked for is not, whether a
    /// declaration can be made at all, and what ten copies may cost. Every answer comes from the arguments alone, so every
    /// machine of a Going Public session with the same game and the same mod gets the same buildings in the same places.
    /// </summary>
    public static class NewBuildingsRules
    {
        public const float Gap = 1.5f;              // m kept clear round a footprint: the game's buildings, obstacles and anything standing
        public const float Touch = 0.05f;           // m one made building's footprint may reach into another's and still stand beside it: a district's stand side by side, as Manhattan's
        public const float RoadClear = 3.5f;        // m from a footprint to the middle of the nearest traffic lane: a lane and its kerb
        public const float StreetReach = 60f;       // m: a lot with no traffic lane this near fronts no street
        public const float GroundLift = 0.4f;       // m above the highest walkable ground under a footprint where the check for anything standing begins
        public const float GroundStep = 1.5f;       // m the walkable ground may rise or fall under a footprint: more is no plot to build on
        public const float DoorReach = 2.5f;        // m: the player's walking mesh must come this near a made building's door
        public const float EdgeMargin = 40f;        // m kept inside the city map's bounds and the traffic grid
        public const float UprightMost = 1f;        // degrees a design's root may lean and still be stood upright
        public const float CopyAhead = 40f;         // m in front of the player: where the copy test stands its building
        public const int CostCopies = 10;           // copies the frame cost is measured with
        public const float CostWindow = 4f;         // s of real time each side of the frame cost test is measured over
        public const float MostAddedMs = 0.5f;      // ms: what ten copies in view may add to the frame
        public const float MostMakeMs = 5f;         // ms one made building may take at the load on average: a district of 360 in under two seconds behind the loading screen
        public const float LaneCell = 50f;          // m: the side of the squares the traffic lanes' points are sorted into for the ground checks of one load
        public const float DoorNear = 3f;           // m: the player stands at a made building's door after leaving it when this near
        public const int MostFloors = 12;           // storeys a made building's record may have: the most floors the floors core gives a building
        public const float FillShare = 0.7f;        // of the ground the best fitting design covers: the designs a plot or a building's place is given among
        public const float WidthShare = 0.7f;       // of the widest fitting design's front: the designs a made building of a size is given among, so its lot's frontage is built
        public const float HeadClear = 2.5f;        // m above its door: a drawn part reaching lower takes ground (a step, a porch); one wholly above may hang over the pavement, as the game's signs do
        public const float StrayMost = 5f;          // m outside its box a part of a design may stand (the game's scaffolding reaches 3.7): further, it is of another place under the same root
        public const float DoorBackMost = 2f;       // m a door may stand behind its front: the game sends a pedestrian 0.5 m out of it to their mesh within 2 m, which ends at the lot's front
        public const float SquareTie = 10f;         // degrees from halfway between two right angles within which a door's facing goes to the box face nearer the door

        public const string SpecialType = "ba:buildingtype_special";   // the game's type that needs a special service
        public const string HomeType = "ba:buildingtype_residential";  // the game's type its food deliveries are brought to

        private const float Deg = (float)(Math.PI / 180.0);

        /// <summary>
        /// The game's special buildings the copy engine copies whole for a special place, Build 3682's, measured from the
        /// city's scenes (not-in-repo filllab/facts/specials_scan.py): every part of each, its controller's garage doors,
        /// pumps, triggers and parking spots, stands under its own root and names only it, so one Instantiate carries the
        /// workings over to the copy's own controller.
        /// </summary>
        public static readonly WholeDesign[] Whole =
        {
            new WholeDesign("DriveInMurrayHill", "ba:buildingsize_e", -1f),   // Total Produce Trading's drive-in store: its door to the street, its two garage doors on its right
            new WholeDesign("GasStationBuilding", "ba:buildingsize_a", 0f),   // the game's filling stations: the pumps' open end (+Z) to the street, as United Gasoline stands to 1st Avenue
        };

        /// <summary>The design's place in <see cref="Whole"/>, or -1 for a design never copied whole.</summary>
        public static int WholeOf(string name, string size)
        {
            for (int i = 0; i < Whole.Length; i++)
            {
                if (string.Equals(Whole[i].Name, name, StringComparison.Ordinal) && string.Equals(Whole[i].Size, size, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The side of a design stood to its street, degrees from its root's forward: its door's, or a whole design's front.</summary>
        public static float FrontOf(DesignEntry d)
        {
            return d.Front >= 0f ? d.Front : d.DoorFacing;
        }

        /// <summary>A design's name: the scene object's name with Unity's copy numbers (" (1)") taken off the end.</summary>
        public static string DesignName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return string.Empty;
            }
            string name = objectName.Trim();
            while (name.EndsWith(")", StringComparison.Ordinal))
            {
                int open = name.LastIndexOf(" (", StringComparison.Ordinal);
                if (open <= 0 || open + 2 >= name.Length - 1)
                {
                    break;
                }
                bool digits = true;
                for (int i = open + 2; i < name.Length - 1; i++)
                {
                    digits &= name[i] >= '0' && name[i] <= '9';
                }
                if (!digits)
                {
                    break;
                }
                name = name.Substring(0, open).TrimEnd();
            }
            return name;
        }

        /// <summary>The order the catalogue keeps: by name, then size, ordinal, so that the same city lists the same designs everywhere.</summary>
        public static int CompareDesigns(DesignEntry a, DesignEntry b)
        {
            int byName = string.CompareOrdinal(a.Name, b.Name);
            return byName != 0 ? byName : string.CompareOrdinal(a.Size, b.Size);
        }

        /// <summary>
        /// True when a building of a design already measured on another should be the one it is measured and copied from:
        /// one not drawn in a static batch before one that is, then the lower address (street, ordinal, then number), so
        /// that every machine measures each design on the same building, whatever order the scene lists them in.
        /// </summary>
        public static bool MeasuredFrom(bool batched, string street, int number, bool wasBatched, string wasStreet, int wasNumber)
        {
            if (batched != wasBatched)
            {
                return !batched;
            }
            int byStreet = string.CompareOrdinal(street, wasStreet);
            return byStreet != 0 ? byStreet < 0 : number < wasNumber;
        }

        /// <summary>The order the declarations are made in: by id, ordinal, whatever order the features declared them in.</summary>
        public static int CompareSpecs(MadeSpec a, MadeSpec b)
        {
            return string.CompareOrdinal(a.Id, b.Id);
        }

        /// <summary>FNV-1a over the text's characters: the same number on every machine and every run, unlike string.GetHashCode.</summary>
        public static uint Hash(string text)
        {
            uint hash = 2166136261u;
            for (int i = 0; text != null && i < text.Length; i++)
            {
                hash ^= text[i];
                hash = unchecked(hash * 16777619u);
            }
            return hash;
        }

        /// <summary>True when a root that leans no more than <see cref="UprightMost"/> (the cosine of its up from the vertical) may be stood upright.</summary>
        public static bool Upright(float upY)
        {
            return upY >= Math.Cos(UprightMost * Deg);
        }

        /// <summary>
        /// A design's measures begun from its box, round its root (m): its footprint and its height the box's, nothing
        /// straying yet. Its door (<see cref="DesignEntry.DoorY"/>) is set first: what reaches below head height is told from it.
        /// </summary>
        public static void Begin(DesignEntry into, float minX, float maxX, float minZ, float maxZ, float top)
        {
            into.MinX = into.BoxMinX = minX;
            into.MaxX = into.BoxMaxX = maxX;
            into.MinZ = into.BoxMinZ = minZ;
            into.MaxZ = into.BoxMaxZ = maxZ;
            into.Height = top;
            into.Strays = false;
        }

        /// <summary>
        /// One part of a design, round its root (m; <paramref name="bottom"/> and <paramref name="top"/> its lowest and highest
        /// points above the root): the box measures only the building's bulk, so the steps, porches, bays and scaffolding the
        /// game stands before it would stand on a lot's pavement. A solid part, or a drawn one reaching below
        /// <see cref="HeadClear"/> above the door, takes ground; one more than <see cref="StrayMost"/> outside the box is of
        /// another place, left out, and the design is never copied.
        /// </summary>
        public static void Part(DesignEntry into, bool solid, float minX, float maxX, float minZ, float maxZ, float bottom, float top)
        {
            float outside = Math.Max(Math.Max(into.BoxMinX - minX, maxX - into.BoxMaxX), Math.Max(into.BoxMinZ - minZ, maxZ - into.BoxMaxZ));
            if (outside > StrayMost)
            {
                into.Strays = true;
                return;
            }
            into.Height = Math.Max(into.Height, top);
            if (!solid && bottom - into.DoorY >= HeadClear)
            {
                return;
            }
            into.MinX = Math.Min(into.MinX, minX);
            into.MaxX = Math.Max(into.MaxX, maxX);
            into.MinZ = Math.Min(into.MinZ, minZ);
            into.MaxZ = Math.Max(into.MaxZ, maxZ);
        }

        /// <summary>
        /// A door's facing (degrees from its root's forward) turned to the root's nearest right angle, so a copy stands square
        /// to its street as the box is square to its root: some of the game's doors are turned across a corner (45 degrees).
        /// Within <see cref="SquareTie"/> of halfway, the right angle whose box face is nearer the door.
        /// </summary>
        public static float SquareFacing(float facing, float doorX, float doorZ, float minX, float maxX, float minZ, float maxZ)
        {
            float t = Turn(facing);
            float low = 90f * (float)Math.Floor(t / 90f), high = low + 90f;
            float toLow = t - low, toHigh = high - t;
            if (Math.Abs(toLow - toHigh) >= SquareTie)
            {
                return Turn(toLow < toHigh ? low : high);
            }
            return Turn(FaceGap(low, doorX, doorZ, minX, maxX, minZ, maxZ) <= FaceGap(high, doorX, doorZ, minX, maxX, minZ, maxZ) ? low : high);
        }

        // m from the door to the box face looking that way (0 forward, 90 right, 180 back, 270 left).
        private static float FaceGap(float facing, float doorX, float doorZ, float minX, float maxX, float minZ, float maxZ)
        {
            switch ((int)Math.Round(Turn(facing)) % 360)
            {
                case 0:
                    return Math.Abs(doorZ - maxZ);
                case 90:
                    return Math.Abs(doorX - maxX);
                case 180:
                    return Math.Abs(doorZ - minZ);
                default:
                    return Math.Abs(doorX - minX);
            }
        }

        /// <summary>How far (m) a design's door stands behind its front (its footprint's side the door faces); below 0 before it.</summary>
        public static float DoorBehind(DesignEntry d)
        {
            DoorFrame(d, out _, out _, out _, out float vMax);
            float s = (float)Math.Sin(d.DoorFacing * Deg), c = (float)Math.Cos(d.DoorFacing * Deg);
            return vMax - (d.DoorX * s + d.DoorZ * c);
        }

        /// <summary>
        /// True for a design a copy may be made of: drawn in no static batch, no part of another place under its root, and
        /// its door at most <see cref="DoorBackMost"/> behind its front, so the pavement reaches it.
        /// </summary>
        public static bool Copyable(DesignEntry d)
        {
            // A whole design stands by its own front, its forecourt or car park outside its box and its door behind them, as the game's own.
            return d != null && !d.Batched && (d.Whole || (!d.Strays && DoorBehind(d) <= DoorBackMost));
        }

        /// <summary>
        /// A design's footprint in the frame of the side it stands to the street (<see cref="FrontOf"/>: its door's but for a
        /// whole design's own front): u across it (to that side's right, looking out), v the way it faces out.
        /// </summary>
        public static void DoorFrame(DesignEntry d, out float uMin, out float uMax, out float vMin, out float vMax)
        {
            float front = FrontOf(d);
            float s = (float)Math.Sin(front * Deg), c = (float)Math.Cos(front * Deg);
            uMin = vMin = float.MaxValue;
            uMax = vMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? d.MinX : d.MaxX;
                float z = (i & 2) == 0 ? d.MinZ : d.MaxZ;
                float u = x * c - z * s;
                float v = x * s + z * c;
                uMin = Math.Min(uMin, u);
                uMax = Math.Max(uMax, u);
                vMin = Math.Min(vMin, v);
                vMax = Math.Max(vMax, v);
            }
        }

        /// <summary>True when the design stood with its door to the street takes no more than the lot's width and depth.</summary>
        public static bool Fits(DesignEntry d, float width, float depth)
        {
            DoorFrame(d, out float uMin, out float uMax, out float vMin, out float vMax);
            return uMax - uMin <= width && vMax - vMin <= depth && uMax > uMin && vMax > vMin;
        }

        /// <summary>
        /// The design a made building wears: the one named if it can be copied and fits; else, of the designs buildings of
        /// its size wear that can be copied and fit, those of its type if any, and of those the ones at least
        /// <see cref="WidthShare"/> as wide as the widest (a narrow design on a wide lot leaves its frontage half empty), one
        /// picked by its id. -1 when none. <paramref name="designs"/> is in <see cref="CompareDesigns"/> order.
        /// </summary>
        public static int Pick(IList<DesignEntry> designs, string size, string type, string named, float width, float depth, string id)
        {
            return Pick(designs, size, type, named, width, depth, id, false);
        }

        /// <summary>
        /// As above; with <paramref name="whole"/> the named design must be one of the game's special buildings copied whole
        /// (<see cref="Whole"/>), and without it never one: such a design is only ever named by a special place, never picked
        /// by size, as its workings belong to the service it copies.
        /// </summary>
        public static int Pick(IList<DesignEntry> designs, string size, string type, string named, float width, float depth, string id, bool whole)
        {
            if (designs == null)
            {
                return -1;
            }
            if (whole && string.IsNullOrEmpty(named))
            {
                return -1;
            }
            if (!string.IsNullOrEmpty(named))
            {
                // Its own size's entry first: one name stands for several sizes, each measured on a building of its own, so
                // another size's may stand otherwise (a feature that chose by size, as Uptown's streets, gets what it chose).
                for (int pass = 0; pass < 2; pass++)
                {
                    for (int i = 0; i < designs.Count; i++)
                    {
                        if (string.Equals(designs[i].Name, named, StringComparison.Ordinal) && (pass == 1 || string.Equals(designs[i].Size, size, StringComparison.Ordinal))
                            && designs[i].Whole == whole && Copyable(designs[i]) && Fits(designs[i], width, depth))
                        {
                            return i;
                        }
                    }
                }
                return -1;
            }
            int typed = 0;
            float widest = 0f, widestTyped = 0f;
            for (int i = 0; i < designs.Count; i++)
            {
                if (Candidate(designs[i], size, width, depth))
                {
                    float across = Across(designs[i]);
                    widest = Math.Max(widest, across);
                    if (string.Equals(designs[i].Type, type, StringComparison.Ordinal))
                    {
                        typed++;
                        widestTyped = Math.Max(widestTyped, across);
                    }
                }
            }
            if (widest <= 0f)
            {
                return -1;
            }
            bool byType = typed > 0;
            float least = (byType ? widestTyped : widest) * WidthShare;
            int pool = 0;
            for (int i = 0; i < designs.Count; i++)
            {
                pool += Pooled(designs[i], size, type, byType, least, width, depth) ? 1 : 0;
            }
            int wanted = (int)(Hash(id) % (uint)pool);
            for (int i = 0; i < designs.Count; i++)
            {
                if (Pooled(designs[i], size, type, byType, least, width, depth) && wanted-- == 0)
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool Candidate(DesignEntry d, string size, float width, float depth)
        {
            return string.Equals(d.Size, size, StringComparison.Ordinal) && !d.Whole && Copyable(d) && Fits(d, width, depth);
        }

        private static bool Pooled(DesignEntry d, string size, string type, bool byType, float least, float width, float depth)
        {
            return Candidate(d, size, width, depth) && (!byType || string.Equals(d.Type, type, StringComparison.Ordinal)) && Across(d) >= least;
        }

        // m: a design's front, across its door's side.
        private static float Across(DesignEntry d)
        {
            DoorFrame(d, out float uMin, out float uMax, out _, out _);
            return uMax - uMin;
        }

        /// <summary>
        /// A design for ground of that width and depth when none is named (a plot's, or a building's own place for another
        /// look): of the designs that can be copied, fit and are not named <paramref name="other"/> (the look replaced), those
        /// covering at least <see cref="FillShare"/> of what the best fitting one covers, one picked by <paramref name="key"/>.
        /// -1 when none fits. <paramref name="designs"/> is in <see cref="CompareDesigns"/> order.
        /// </summary>
        public static int PickFor(IList<DesignEntry> designs, float width, float depth, string other, string key)
        {
            if (designs == null)
            {
                return -1;
            }
            float best = 0f;
            for (int i = 0; i < designs.Count; i++)
            {
                best = Fitting(designs[i], width, depth, other) ? Math.Max(best, Covers(designs[i])) : best;
            }
            if (best <= 0f)
            {
                return -1;
            }
            int pool = 0;
            for (int i = 0; i < designs.Count; i++)
            {
                pool += Fitting(designs[i], width, depth, other) && Covers(designs[i]) >= best * FillShare ? 1 : 0;
            }
            int wanted = (int)(Hash(key) % (uint)pool);
            for (int i = 0; i < designs.Count; i++)
            {
                if (Fitting(designs[i], width, depth, other) && Covers(designs[i]) >= best * FillShare && wanted-- == 0)
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool Fitting(DesignEntry d, float width, float depth, string other)
        {
            return Copyable(d) && !d.Whole && Fits(d, width, depth) && !string.Equals(d.Name, other, StringComparison.Ordinal);
        }

        // m2: the ground a design takes, measured from its door.
        private static float Covers(DesignEntry d)
        {
            DoorFrame(d, out float uMin, out float uMax, out float vMin, out float vMax);
            return (uMax - uMin) * (vMax - vMin);
        }

        /// <summary>
        /// Where a design's root stands on a lot and its turn (degrees): its door (a whole design's front) to the street, its
        /// footprint's front (its box, steps, porches and anything solid: <see cref="Part"/>) on the lot's front line, the back
        /// of the street's pavement, so nothing of it stands on the pavement, or <see cref="MadeLot.Inset"/> back from it; in the
        /// middle of the lot's width or against the side <see cref="MadeLot.Side"/> names.
        /// </summary>
        public static void Place(DesignEntry d, MadeLot lot, out float x, out float z, out float yaw)
        {
            DoorFrame(d, out float uMin, out float uMax, out _, out float vMax);
            yaw = Turn(lot.Facing - FrontOf(d));
            float f = lot.Facing * Deg;
            float fx = (float)Math.Sin(f), fz = (float)Math.Cos(f);    // the way the street lies
            float rx = (float)Math.Cos(f), rz = -(float)Math.Sin(f);   // along the street, to the right of it
            float across = (uMin + uMax) * 0.5f, toFront = lot.Depth * 0.5f - lot.Inset - vMax;
            float aside = Aside(lot, uMax - uMin);
            x = lot.X + toFront * fx + (aside - across) * rx;
            z = lot.Z + toFront * fz + (aside - across) * rz;
        }

        // m from the lot's middle to the footprint's, to the right along the street: none in the middle, else against the side named.
        private static float Aside(MadeLot lot, float wide)
        {
            float room = Math.Max(0f, lot.Width * 0.5f - lot.SideGap - wide * 0.5f);
            return lot.Side > 0 ? room : lot.Side < 0 ? -room : 0f;
        }

        /// <summary>
        /// As <see cref="Place"/>, then moved along the lot's front until the design's door is <paramref name="doorAcross"/> m
        /// right of the lot's middle (looking out from it): a copy over one of the game's buildings has its door where the
        /// building's own is, as far as its footprint may move and stay within <paramref name="slack"/> of the lot. Returns the
        /// metres the door is still short by.
        /// </summary>
        public static float AlignDoor(DesignEntry d, MadeLot lot, float doorAcross, float slack, out float x, out float z, out float yaw)
        {
            Place(d, lot, out x, out z, out yaw);
            DoorAt(d, x, z, yaw, out float doorX, out float doorZ);
            float f = lot.Facing * Deg;
            float rx = (float)Math.Cos(f), rz = -(float)Math.Sin(f);
            float across = (doorX - lot.X) * rx + (doorZ - lot.Z) * rz;
            DoorFrame(d, out float uMin, out float uMax, out _, out _);
            float room = Math.Max(0f, lot.Width * 0.5f + slack - (uMax - uMin) * 0.5f);
            float shift = Math.Max(-room, Math.Min(room, doorAcross - across));
            x += shift * rx;
            z += shift * rz;
            return Math.Abs(doorAcross - across - shift);
        }

        /// <summary>The ground a design takes on a lot, as a turned rectangle: its middle and its half width and half depth (m), turned as the lot.</summary>
        public static void Footprint(DesignEntry d, MadeLot lot, out float cx, out float cz, out float halfWidth, out float halfDepth)
        {
            DoorFrame(d, out float uMin, out float uMax, out float vMin, out float vMax);
            halfWidth = (uMax - uMin) * 0.5f;
            halfDepth = (vMax - vMin) * 0.5f;
            float f = lot.Facing * Deg;
            float fx = (float)Math.Sin(f), fz = (float)Math.Cos(f);
            float rx = (float)Math.Cos(f), rz = -(float)Math.Sin(f);
            float toMiddle = lot.Depth * 0.5f - lot.Inset - halfDepth;
            float aside = Aside(lot, uMax - uMin);
            cx = lot.X + toMiddle * fx + aside * rx;
            cz = lot.Z + toMiddle * fz + aside * rz;
        }

        /// <summary>Where a design's door stands in the world when its root is at (x, z) turned by <paramref name="yaw"/> degrees.</summary>
        public static void DoorAt(DesignEntry d, float x, float z, float yaw, out float doorX, out float doorZ)
        {
            float a = yaw * Deg;
            float s = (float)Math.Sin(a), c = (float)Math.Cos(a);
            doorX = x + d.DoorX * c + d.DoorZ * s;
            doorZ = z - d.DoorX * s + d.DoorZ * c;
        }

        /// <summary>The game's structure for a size and version as the game names its sizes: "ba:buildingsize_c", 1 is "C1".</summary>
        public static string StructureName(string size, int version)
        {
            if (string.IsNullOrEmpty(size))
            {
                return "?" + version;
            }
            int cut = size.LastIndexOf('_');
            return size.Substring(cut + 1).ToUpperInvariant() + version;
        }

        /// <summary>An angle in degrees brought into [0, 360).</summary>
        public static float Turn(float degrees)
        {
            float t = degrees % 360f;
            return t < 0f ? t + 360f : t;
        }

        /// <summary>True when the point lies within <paramref name="margin"/> of the turned rectangle (or inside it).</summary>
        public static bool Inside(float px, float pz, float cx, float cz, float facing, float halfWidth, float halfDepth, float margin)
        {
            float f = facing * Deg;
            float fx = (float)Math.Sin(f), fz = (float)Math.Cos(f);
            float dx = px - cx, dz = pz - cz;
            float along = dx * fz - dz * fx;    // on the right axis (cos f, -sin f)
            float outward = dx * fx + dz * fz;  // on the facing axis (sin f, cos f)
            return Math.Abs(along) <= halfWidth + margin && Math.Abs(outward) <= halfDepth + margin;
        }

        /// <summary>True when an upright box (min and max corners) and the turned rectangle grown by <paramref name="margin"/> overlap: four axes are tried.</summary>
        public static bool Overlaps(float minX, float minZ, float maxX, float maxZ, float cx, float cz, float facing, float halfWidth, float halfDepth, float margin)
        {
            if (maxX < minX || maxZ < minZ)
            {
                return false;
            }
            // The rectangle's reach along the world's axes, then the box's along the rectangle's.
            Reach(facing, halfWidth, halfDepth, margin, out float reachX, out float reachZ);
            if (Apart(minX, minZ, maxX, maxZ, cx, cz, reachX, reachZ))
            {
                return false;
            }
            float f = facing * Deg;
            float fx = (float)Math.Sin(f), fz = (float)Math.Cos(f);
            float rx = fz, rz = -fx;
            float hw = halfWidth + margin, hd = halfDepth + margin;
            float bx = (minX + maxX) * 0.5f - cx, bz = (minZ + maxZ) * 0.5f - cz;
            float ex = (maxX - minX) * 0.5f, ez = (maxZ - minZ) * 0.5f;
            float alongR = Math.Abs(bx * rx + bz * rz), boxR = ex * Math.Abs(rx) + ez * Math.Abs(rz);
            if (alongR > hw + boxR)
            {
                return false;
            }
            float alongF = Math.Abs(bx * fx + bz * fz), boxF = ex * Math.Abs(fx) + ez * Math.Abs(fz);
            return alongF <= hd + boxF;
        }

        /// <summary>
        /// How far the turned rectangle grown by <paramref name="margin"/> reaches from its middle along the world's X and Z:
        /// <see cref="Overlaps"/>'s first test, worked out once for a pass over many boxes (one <see cref="Apart"/> from it never overlaps).
        /// </summary>
        public static void Reach(float facing, float halfWidth, float halfDepth, float margin, out float reachX, out float reachZ)
        {
            float f = facing * Deg;
            float fx = (float)Math.Sin(f), fz = (float)Math.Cos(f);
            float rx = fz, rz = -fx;
            float hw = halfWidth + margin, hd = halfDepth + margin;
            reachX = Math.Abs(rx) * hw + Math.Abs(fx) * hd;
            reachZ = Math.Abs(rz) * hw + Math.Abs(fz) * hd;
        }

        /// <summary>True when an upright box lies beyond a rectangle's <see cref="Reach"/> round (cx, cz): the two cannot overlap.</summary>
        public static bool Apart(float minX, float minZ, float maxX, float maxZ, float cx, float cz, float reachX, float reachZ)
        {
            return cx + reachX < minX || cx - reachX > maxX || cz + reachZ < minZ || cz - reachZ > maxZ;
        }

        /// <summary>True when every corner of the turned rectangle lies inside the upright box shrunk by <paramref name="margin"/>.</summary>
        public static bool Within(float cx, float cz, float facing, float halfWidth, float halfDepth, float minX, float minZ, float maxX, float maxZ, float margin)
        {
            float f = facing * Deg;
            float fx = (float)Math.Sin(f), fz = (float)Math.Cos(f);
            float rx = fz, rz = -fx;
            for (int i = 0; i < 4; i++)
            {
                float a = (i & 1) == 0 ? -halfWidth : halfWidth;
                float b = (i & 2) == 0 ? -halfDepth : halfDepth;
                float x = cx + a * rx + b * fx;
                float z = cz + a * rz + b * fz;
                if (x < minX + margin || x > maxX - margin || z < minZ + margin || z > maxZ - margin)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// The lot a made building takes: the one it asked for if <paramref name="free"/> says so, else the next free one in
        /// the lots' order, round to the start. -1 when none is free (or there are none).
        /// </summary>
        public static int FirstFree(int lots, int preferred, Func<int, bool> free)
        {
            if (lots <= 0 || free == null)
            {
                return -1;
            }
            int start = preferred >= 0 && preferred < lots ? preferred : 0;
            for (int k = 0; k < lots; k++)
            {
                int lot = (start + k) % lots;
                if (free(lot))
                {
                    return lot;
                }
            }
            return -1;
        }

        /// <summary>
        /// The lot ahead of someone standing at (<paramref name="x"/>, <paramref name="z"/>) and looking along
        /// <paramref name="yaw"/> (degrees round the vertical, as the game turns things): its front line <paramref name="gap"/>
        /// metres ahead of them, its middle on their line of sight, its front turned back towards them. The test building's lot
        /// where the developer stood when they asked for it, so a plot found taken in the game can be swapped for ground they see.
        /// </summary>
        public static MadeLot LotAhead(float x, float z, float yaw, float width, float depth, float gap)
        {
            double turn = yaw * Math.PI / 180.0;
            float ahead = gap + depth * 0.5f;
            float facing = yaw + 180f;
            facing -= 360f * (float)Math.Floor(facing / 360f);
            return new MadeLot(x + (float)Math.Sin(turn) * ahead, z + (float)Math.Cos(turn) * ahead, facing, width, depth);
        }

        /// <summary>Where someone stood and the way they looked, as text kept until the next load: "x;z;yaw", the invariant culture's.</summary>
        public static string SpotText(float x, float z, float yaw)
        {
            return x.ToString("R", CultureInfo.InvariantCulture) + ";" + z.ToString("R", CultureInfo.InvariantCulture) + ";"
                   + yaw.ToString("R", CultureInfo.InvariantCulture);
        }

        /// <summary>The spot back from <see cref="SpotText"/>; false for anything else (nothing kept, or text of another form).</summary>
        public static bool ReadSpot(string text, out float x, out float z, out float yaw)
        {
            x = 0f;
            z = 0f;
            yaw = 0f;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            string[] parts = text.Split(';');
            return parts.Length == 3 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                   && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out z)
                   && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out yaw);
        }

        /// <summary>
        /// Why a declaration cannot be made at all (English, for the log), or null when it can. The game's own limits are
        /// asked, never assumed: <paramref name="structureTakes"/> whether the game has a structure of that size and version
        /// for that type, <paramref name="districtKnown"/> whether it has that district, <paramref name="homesLeftOut"/>
        /// whether the game's food deliveries can be told to leave a made home alone.
        /// </summary>
        public static string Refusal(MadeSpec spec, Func<string, int, string, bool> structureTakes, Func<string, bool> districtKnown, bool homesLeftOut)
        {
            if (spec == null || string.IsNullOrEmpty(spec.Id))
            {
                return "it has no id";
            }
            if (!MadeStreets.IsMine(spec.Street) || MadeStreets.Find(spec.Street) == null)
            {
                return "its street " + (spec.Street ?? "(none)") + " is not one of the mod's own streets (a free update may fill the game's own)";
            }
            if (spec.Number < 1)
            {
                return "its number " + spec.Number + " is not a house number";
            }
            if (spec.Whole && WholeNamed(spec.Design) < 0)
            {
                return "a building copied whole names one of the game's special buildings the copy engine copies whole, not " + (spec.Design ?? "(none)");
            }
            string served = ServiceRefusal(spec, structureTakes);
            if (served != null)
            {
                return served;
            }
            if (structureTakes == null || !structureTakes(spec.Size, spec.Version, spec.Type))
            {
                return "the game has no structure of size " + (spec.Size ?? "(none)") + " version " + spec.Version + " for the type " + spec.Type;
            }
            if (districtKnown == null || !districtKnown(spec.District))
            {
                return "the game has no district " + (spec.District ?? "(none)");
            }
            if (spec.Type == HomeType && !homesLeftOut && !spec.Life)
            {
                // A district's home takes the game's food deliveries as any home does: what names it leaves every save with the district's records.
                return "a home would be sent the game's food deliveries, and the game's list of addresses those leave out could not be reached";
            }
            if (spec.Lots == null || spec.Lots.Length == 0)
            {
                return "it has nowhere to stand";
            }
            if (spec.Traffic < 0 || spec.Traffic > 100)
            {
                return "its traffic index " + spec.Traffic + " is not between 0 and 100";
            }
            if (spec.Floors < 1 || spec.Floors > MostFloors)
            {
                return "its floors " + spec.Floors + " are not between 1 and " + MostFloors;
            }
            return null;
        }

        // A whole design by its name alone, any size: -1 for none.
        private static int WholeNamed(string name)
        {
            for (int i = 0; name != null && i < Whole.Length; i++)
            {
                if (string.Equals(Whole[i].Name, name, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// True when a declaration may be made with <paramref name="other"/> instead of its own structure (one of its
        /// <see cref="MadeSpec.Others"/>): by <see cref="Refusal"/>'s own limits, the game has that structure for its type, it is
        /// not the special type, and a home only where the food deliveries can leave it alone (a district's home always).
        /// </summary>
        public static bool OtherTakes(MadeSpec spec, MadeStructure other, Func<string, int, string, bool> structureTakes, bool homesLeftOut)
        {
            // A service's layout is the game's for its own size and version alone (BusinessLayoutSetHelper), so it takes no other.
            return spec != null && string.IsNullOrEmpty(spec.Service) && !string.IsNullOrEmpty(other.Type) && other.Type != SpecialType && structureTakes != null
                   && structureTakes(other.Size, other.Version, other.Type) && (other.Type != HomeType || homesLeftOut || spec.Life);
        }

        /// <summary>
        /// Why a declaration's type and service cannot go together (English, for the log), or null. The game's special type
        /// is made only with a service of the game's attached, as its towing reads the service's settings on every
        /// special-type building (VehicleHelper); a service only on a district's building (its registration the save's, as
        /// the game's special buildings' are), and the type it falls back to where a player holds its lot is an ordinary one
        /// the game has that structure for.
        /// </summary>
        public static string ServiceRefusal(MadeSpec spec, Func<string, int, string, bool> structureTakes)
        {
            bool service = !string.IsNullOrEmpty(spec.Service);
            if (string.IsNullOrEmpty(spec.Type) || (spec.Type == SpecialType && !service))
            {
                return "the game's special building type needs a special service of the game's (its towing reads one), so it is made only with one";
            }
            if (!service)
            {
                return null;
            }
            if (!spec.Life)
            {
                return "a special service of the game's stands only in a district's building, whose registration is the save's";
            }
            if (spec.PlainType == SpecialType || (spec.PlainType != null && (structureTakes == null || !structureTakes(spec.Size, spec.Version, spec.PlainType))))
            {
                return "the type it stands as without its service, " + spec.PlainType + ", is not one the game has size " + spec.Size + " version " + spec.Version + " for";
            }
            if (spec.ServiceGoods != null && spec.ServiceGoods.Length == 0)
            {
                return "an importer of its own goods has none";
            }
            return null;
        }

        /// <summary>
        /// What a district building's registration is to the service its declaration copies: the player's (rented or owned,
        /// which in a Going Public session is also another player's) stays an ordinary building; of the service's type with
        /// the copy's name it is the service's already; anything else (new at this load, empty, an AI business the game's
        /// generator put there before the place was the service's) is filled as the game fills a special building's.
        /// </summary>
        public static ServiceVerdict ServiceOf(bool players, string type, string name, string serviceType, string serviceName)
        {
            if (players)
            {
                return ServiceVerdict.Held;
            }
            return !string.IsNullOrEmpty(serviceType) && type == serviceType && name == serviceName ? ServiceVerdict.Theirs : ServiceVerdict.Fill;
        }

        /// <summary>True when what ten copies in view add to the frame is under <see cref="MostAddedMs"/>.</summary>
        public static bool CostPasses(float withMs, float withoutMs)
        {
            return withMs - withoutMs < MostAddedMs;
        }

        /// <summary>
        /// Where the copies of the frame cost test stand, ahead of a camera at (x, z) looking along <paramref name="facing"/>
        /// degrees: two rows of five across the view, the nearer at <see cref="CopyAhead"/> m, 30 m apart each way.
        /// </summary>
        public static void CostSpot(int index, float x, float z, float facing, out float spotX, out float spotZ)
        {
            int row = index / 5, column = index % 5;
            float ahead = CopyAhead + row * 30f;
            float across = (column - 2) * 30f;
            float f = facing * Deg;
            float fx = (float)Math.Sin(f), fz = (float)Math.Cos(f);
            spotX = x + ahead * fx + across * fz;
            spotZ = z + ahead * fz - across * fx;
        }
    }

    /// <summary>
    /// The traffic lanes' points sorted into squares of <see cref="NewBuildingsRules.LaneCell"/> m, once a load, so a made
    /// building's ground check looks at the points near its footprint instead of all of them (some 20,000 with Uptown's
    /// lanes, for each of hundreds of buildings). Game-free: the points are given by x and z. Its answers are a whole pass's:
    /// whether a point lies inside a footprint with the road's clearance, and the nearest point within a reach.
    /// COST: a pass over the points to fill it; then a few squares a question.
    /// </summary>
    public sealed class LaneGrid
    {
        private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>(1024);
        private float[] _x = new float[0];
        private float[] _z = new float[0];

        /// <summary>How many points it holds.</summary>
        public int Count { get; private set; }

        /// <summary>The points afresh: x and z of each, those not <paramref name="present"/> left out; indices stay the caller's.</summary>
        public void Fill(float[] x, float[] z, bool[] present, int count)
        {
            _cells.Clear();
            _x = x ?? new float[0];
            _z = z ?? new float[0];
            Count = 0;
            for (int i = 0; i < count && i < _x.Length && i < _z.Length; i++)
            {
                if (present != null && i < present.Length && !present[i])
                {
                    continue;
                }
                long key = Key(Cell(_x[i]), Cell(_z[i]));
                if (!_cells.TryGetValue(key, out List<int> list))
                {
                    list = new List<int>(16);
                    _cells.Add(key, list);
                }
                list.Add(i);
                Count++;
            }
        }

        /// <summary>A point inside the turned rectangle grown by <paramref name="margin"/> (NewBuildingsRules.Inside), the lowest index of those; -1 for none.</summary>
        public int Inside(float cx, float cz, float facing, float halfWidth, float halfDepth, float margin)
        {
            float w = halfWidth + margin, d = halfDepth + margin;
            float reach = (float)Math.Sqrt(w * w + d * d);
            int found = -1;
            for (int gx = Cell(cx - reach); gx <= Cell(cx + reach); gx++)
            {
                for (int gz = Cell(cz - reach); gz <= Cell(cz + reach); gz++)
                {
                    if (!_cells.TryGetValue(Key(gx, gz), out List<int> list))
                    {
                        continue;
                    }
                    for (int k = 0; k < list.Count; k++)
                    {
                        int i = list[k];
                        if ((found < 0 || i < found) && NewBuildingsRules.Inside(_x[i], _z[i], cx, cz, facing, halfWidth, halfDepth, margin))
                        {
                            found = i;
                        }
                    }
                }
            }
            return found;
        }

        /// <summary>The nearest point to (x, z) within <paramref name="reach"/> m, the lowest index of equals, and its squared distance; -1 for none.</summary>
        public int Nearest(float x, float z, float reach, out float squared)
        {
            squared = float.MaxValue;
            int best = -1;
            for (int gx = Cell(x - reach); gx <= Cell(x + reach); gx++)
            {
                for (int gz = Cell(z - reach); gz <= Cell(z + reach); gz++)
                {
                    if (!_cells.TryGetValue(Key(gx, gz), out List<int> list))
                    {
                        continue;
                    }
                    for (int k = 0; k < list.Count; k++)
                    {
                        int i = list[k];
                        float dx = _x[i] - x, dz = _z[i] - z;
                        float sqr = dx * dx + dz * dz;
                        if (sqr <= reach * reach && (sqr < squared || (sqr == squared && i < best)))
                        {
                            squared = sqr;
                            best = i;
                        }
                    }
                }
            }
            return best;
        }

        private static int Cell(float v)
        {
            return (int)Math.Floor(v / NewBuildingsRules.LaneCell);
        }

        private static long Key(int gx, int gz)
        {
            return ((long)gx << 32) ^ (uint)gz;
        }
    }
}
