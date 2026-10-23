using System.Collections.Generic;
using RealLifeOverhaul.NewGround;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>The game's road modules a street's art is copied from: a street of two lanes, an avenue of four, a crossing of the two.</summary>
    public enum ModuleKind
    {
        Street = 0,      // the game's SM_RoadModule- 120-70: 100 m of a street, its pavements, lamps and parking lane
        Avenue = 1,      // SM_RoadModule 120-160-70: 50 m of an avenue
        Crossing = 2     // SM_RoadModule Intersection_4-2 Lanes: an avenue crossing a street, its lights and crosswalks
    }

    /// <summary>One copy of a road module of the game's: what, where (its middle) and which of the plan's junctions it is, if any.</summary>
    public struct ModulePlace
    {
        public ModuleKind Kind;
        public float X;          // m: the module's middle, as the game's own stand by theirs
        public float Z;
        public int Street;       // the street it lies on or crosses (9th Street is 0)
        public int Junction;     // a crossing's junction among the plan's, or -1 where no lanes cross (5th Street)

        public ModulePlace(ModuleKind kind, float x, float z, int street, int junction)
        {
            Kind = kind;
            X = x;
            Z = z;
            Street = street;
            Junction = junction;
        }
    }

    /// <summary>A box on the ground: its middle, half its size each way, and the height of its top (m).</summary>
    public struct PlanBox
    {
        public float X;
        public float Z;
        public float HalfX;
        public float HalfZ;
        public float Top;

        public PlanBox(float x0, float z0, float x1, float z1, float top)
        {
            X = (x0 + x1) * 0.5f;
            Z = (z0 + z1) * 0.5f;
            HalfX = System.Math.Abs(x1 - x0) * 0.5f;
            HalfZ = System.Math.Abs(z1 - z0) * 0.5f;
            Top = top;
        }

        public bool Holds(float x, float z, float margin)
        {
            return System.Math.Abs(x - X) <= HalfX + margin && System.Math.Abs(z - Z) <= HalfZ + margin;
        }
    }

    /// <summary>
    /// How the first block is laid at every load (game-free; the game's side does what it says): the streets as the
    /// traffic's lanes, eight of Manhattan's streets continued through 1st Avenue's own junctions and Uptown Avenue across
    /// them with lights; the walking ground for the player (streets, pavements and lots) and for the pedestrians
    /// (pavements and crosswalks), joined to Manhattan's pavements across the old wall; the game's own road modules copied
    /// along it; the old walls opened and new ones round its edge; the ground and road colliders; the backdrop hidden
    /// where it stands on it; the pedestrians' zones and the bus terminus. Every figure is the layout's or measured from
    /// the game's own scene at Manhattan's north edge (Build 3682); the load checks the game's ground against them and
    /// stands down where it has changed.
    /// </summary>
    public static class LayoutPlans
    {
        public const float ThroughZ = 482.58f;          // m: where a continued street's lanes meet 1st Avenue's junction, beyond its crosswalk as the game's
        public const float ThroughFind = 460f;          // m: where that junction is looked for, among its stop points
        public const float StreetStop = 19.6f;          // m short of a crossing a street's lanes stop, as at the game's own crossings
        public const float AvenueStop = 14.85f;         // m short of it an avenue's lanes stop
        public const float AvenueLaneLine = 0.02f;      // m north of an avenue's middle the line between its two ways runs, as 1st Avenue's
        public const int StreetSpeed = 50;              // km/h, the game's city streets
        public const float GroundTop = 0.05f;           // m: the top of the walking ground's boxes, between the road and the pavement
        public const float GroundDepth = 0.5f;          // m below it
        public const float ArmFrom = 476f;              // m: the walking ground begins this far north, over the old dead end's carriageway
        public const float StripFrom = 479.5f;          // m: the pedestrians' strip up each street's east pavement begins here, on the old corner
        public const float StripWest = 7.1f;            // m east of a street's middle that strip runs from (clear of the trees in its pavement) ...
        public const float StripEast = 8.4f;            // ... to here (clear of the buildings beside it)
        public const float ModuleClear = 488.18f;       // m: where 1st Avenue's crossing modules end and the old arms' pavements were backdrop
        public const float BackClear = 0.3f;            // m kept from the backs of 1st Avenue's buildings and their scaffolds
        public const float EdgeClear = 0.5f;            // m the outermost pavements keep off the district's edge: the backdrop's buildings east of 1st Street stand 0.3 m onto its pavement
        public const float WallHigh = 4.76f;            // m: the game's own invisible walls' height
        public const float WallThick = 5f;              // m: and their depth
        public const float WallFrom = 478.3f;           // m: the new side walls and the ground begin just north of 1st Avenue's north pavement
        public const float OldWallReach = 12f;          // m from a street end's middle the game's old wall across it is looked for
        public const float HideHalf = 12.5f;            // m each side of a street end's middle the dead end's dressing is hidden
        public const float HideFrom = 476f;             // m: and from here north
        public const float HideTo = 530f;               // m: to here, behind 1st Avenue's buildings
        public const float AreaFrom = 498f;             // m: the first block's main area, where the backdrop goes, begins here
        public const float GroundCollider = 1f;         // m: the ground collider's thickness, its top the city's ground
        public const float RoadCollider = 0.2f;         // m: a road collider's thickness
        public const float CrosswalkNear = 6.0f;        // m from a crossing's middle a crosswalk over the avenue begins, the game's
        public const float CrosswalkFar = 9.6f;         // m: and ends
        public const float ZoneLength = 40f;            // m: a pedestrians' zone along a pavement
        public const float ZoneWidth = 3f;              // m: across it
        public const float MapNorth = 700f;             // m: the city map's bounds reach at least this far north, 40 m past the lots and more
        public const float TerminusInset = 11.8f;       // m south of Uptown Avenue's middle: its south pavement, where the terminus stands

        // The streets whose lanes go through: all but 5th Street, which 1st Avenue's traffic passes without a junction.
        private static readonly int[] Lanes = { 0, 1, 2, 3, 5, 6, 7, 8 };

        // The first metre north of which each street's pavements are clear of 1st Avenue's buildings, scaffolds, trees and
        // bins (m, measured from the game's scene, Build 3682): west and east, 9th to 1st Street. The pavements alongside
        // those buildings are left to the carriageway and the strip; a free update that puts something there stands down.
        private static readonly float[] PavementWest = { 488.2f, 488.2f, 488.2f, 504.0f, 488.2f, 509.2f, 507.1f, 488.2f, 509.2f };
        private static readonly float[] PavementEast = { 488.2f, 508.8f, 488.2f, 488.2f, 488.2f, 488.2f, 488.2f, 488.2f, 488.2f };

        /// <summary>True for a street whose lanes are laid (all but 5th Street).</summary>
        public static bool HasLanes(int street)
        {
            return System.Array.IndexOf(Lanes, street) >= 0;
        }

        /// <summary>The plan's junction at a street's crossing with Uptown Avenue, or -1 where no lanes cross.</summary>
        public static int JunctionAt(int street)
        {
            return System.Array.IndexOf(Lanes, street);
        }

        /// <summary>The plan's road of a street's lanes, or -1 (the streets come first, in their order).</summary>
        public static int StreetRoad(int street)
        {
            return System.Array.IndexOf(Lanes, street);
        }

        /// <summary>The first metre north of which a street's pavement on one side is laid whole.</summary>
        public static float PavementFrom(int street, bool east)
        {
            return east ? PavementEast[street] : PavementWest[street];
        }

        // ---- the streets

        /// <summary>
        /// The first block's streets as a plan of the traffic's lanes: each continued street of two lanes from 1st Avenue's
        /// junction (through it) to Uptown Avenue, then Uptown Avenue of four lanes in segments between its crossings, each
        /// crossing a junction with lights where the avenue's two ways share one green, as at the game's own. A new plan
        /// each call: the game's side fills in its junctions at the load.
        /// </summary>
        public static RoadPlan Streets()
        {
            var plan = new RoadPlan { Name = "uptown" };
            float avenue = LayoutRules.AvenueZ(1);
            for (int j = 0; j < Lanes.Length; j++)
            {
                plan.Junctions.Add(new PlanJunction { X = LayoutRules.StreetX(Lanes[j]), Y = 0f, Z = avenue, Lights = true, Inset = AvenueStop });
            }
            for (int s = 0; s < Lanes.Length; s++)
            {
                float x = LayoutRules.StreetX(Lanes[s]) + LayoutRules.StreetLanes;
                plan.Roads.Add(new PlanRoad
                {
                    Ahead = 1,
                    Back = 1,
                    LaneWidth = LayoutRules.StreetLaneWidth,
                    Speed = StreetSpeed,
                    End = s,
                    EndInset = StreetStop
                }.Point(x, 0f, ThroughZ).Point(x, 0f, avenue));
                plan.Throughs.Add(new PlanThrough { Road = s, AtStart = true, X = LayoutRules.StreetX(Lanes[s]), Z = ThroughFind });
            }
            for (int a = 0; a + 1 < Lanes.Length; a++)
            {
                int road = plan.Roads.Count;
                plan.Roads.Add(new PlanRoad
                {
                    Ahead = 2,
                    Back = 2,
                    LaneWidth = LayoutRules.AvenueLaneWidth,
                    Speed = StreetSpeed,
                    Start = a,
                    End = a + 1
                }.Point(LayoutRules.StreetX(Lanes[a]), 0f, avenue + AvenueLaneLine).Point(LayoutRules.StreetX(Lanes[a + 1]), 0f, avenue + AvenueLaneLine));
                plan.Junctions[a].Together.Add(road);
                plan.Junctions[a + 1].Together.Add(road);
            }
            return plan;
        }

        // ---- the walking ground

        /// <summary>
        /// The player's walking ground: every street's carriageway from the old dead end, its pavements where clear, every
        /// lot of the first block from the backs of 1st Avenue's buildings, and Uptown Avenue with its pavements. No game
        /// collider stands on any of it in the build it was measured on (ExpectColliders 0), the backdrop hidden first.
        /// </summary>
        public static GroundPlan PlayerGround()
        {
            var plan = new GroundPlan { Name = "uptown player", Player = true, Npc = false, ExpectColliders = 0 };
            float avenue = LayoutRules.AvenueZ(1);
            float south = avenue - LayoutRules.AvenueHalf;
            for (int k = 0; k <= LayoutRules.FirstColumns; k++)
            {
                float c = LayoutRules.StreetX(k);
                plan.Boxes.Add(Box(c - LayoutRules.StreetKerbWest, ArmFrom, c + LayoutRules.StreetKerbEast, avenue - LayoutRules.AvenueKerb));
                plan.Boxes.Add(Box(Outer(k, false), PavementFrom(k, false), c - LayoutRules.StreetKerbWest, south));
                plan.Boxes.Add(Box(c + LayoutRules.StreetKerbEast, PavementFrom(k, true), Outer(k, true), south));
            }
            for (int b = 0; b < LayoutRules.FirstColumns; b++)
            {
                int column = LayoutRules.FirstColumn + b;
                LayoutRules.Block(1, column, out float x0, out _, out float x1, out float z1);
                plan.Boxes.Add(Box(x0, LayoutRules.FirstBack(column) + BackClear, x1, z1));
            }
            plan.Boxes.Add(Box(LayoutRules.StreetX(0) - LayoutRules.StreetHalf, south, LayoutRules.StreetX(LayoutRules.FirstColumns) + LayoutRules.StreetHalf,
                avenue + LayoutRules.AvenueHalf));
            return plan;
        }

        /// <summary>
        /// The pedestrians' walking ground: every street's pavements where clear, a strip up its east pavement from the old
        /// corner (clear of its trees and the buildings beside it) to join Manhattan's, Uptown Avenue's two pavements, and
        /// the crosswalks over the avenue at its junctions. Never the carriageways or the lots.
        /// </summary>
        public static GroundPlan NpcGround()
        {
            var plan = new GroundPlan { Name = "uptown pedestrians", Player = false, Npc = true, ExpectColliders = 0 };
            float avenue = LayoutRules.AvenueZ(1);
            float southKerb = avenue - LayoutRules.AvenueKerb;
            for (int k = 0; k <= LayoutRules.FirstColumns; k++)
            {
                float c = LayoutRules.StreetX(k);
                plan.Boxes.Add(Box(Outer(k, false), PavementFrom(k, false), c - LayoutRules.StreetKerbWest, southKerb));
                plan.Boxes.Add(Box(c + LayoutRules.StreetKerbEast, PavementFrom(k, true), Outer(k, true), southKerb));
                plan.Boxes.Add(Box(c + StripWest, StripFrom, c + StripEast, PavementFrom(k, true) + 0.5f));
            }
            float west = LayoutRules.StreetX(0) - LayoutRules.StreetHalf, east = LayoutRules.StreetX(LayoutRules.FirstColumns) + LayoutRules.StreetHalf;
            plan.Boxes.Add(Box(west, avenue - LayoutRules.AvenueHalf, east, southKerb));
            plan.Boxes.Add(Box(west, avenue + LayoutRules.AvenueKerb, east, avenue + LayoutRules.AvenueHalf));
            for (int j = 0; j < Lanes.Length; j++)
            {
                float c = LayoutRules.StreetX(Lanes[j]);
                if (j > 0)
                {
                    plan.Boxes.Add(Box(c - CrosswalkFar, southKerb, c - CrosswalkNear, avenue + LayoutRules.AvenueKerb));
                }
                if (j < Lanes.Length - 1)
                {
                    plan.Boxes.Add(Box(c + CrosswalkNear, southKerb, c + CrosswalkFar, avenue + LayoutRules.AvenueKerb));
                }
            }
            return plan;
        }

        // A street's pavement's outer edge: its own, but for the district's outermost two, which keep off its edge.
        private static float Outer(int street, bool east)
        {
            float c = LayoutRules.StreetX(street);
            if (east)
            {
                return c + LayoutRules.StreetHalf - (street == LayoutRules.FirstColumns ? EdgeClear : 0f);
            }
            return c - LayoutRules.StreetHalf + (street == 0 ? EdgeClear : 0f);
        }

        private static GroundBox Box(float x0, float z0, float x1, float z1)
        {
            return new GroundBox((x0 + x1) * 0.5f, (z0 + z1) * 0.5f, GroundTop, System.Math.Abs(x1 - x0) * 0.5f, System.Math.Abs(z1 - z0) * 0.5f, 0f, GroundDepth);
        }

        // ---- the art, the walls, the colliders

        /// <summary>The game's road modules copied along the first block: each street's 100 m, each crossing on Uptown Avenue, the avenue between them.</summary>
        public static void Modules(List<ModulePlace> into)
        {
            into.Clear();
            float avenue = LayoutRules.AvenueZ(1);
            float streetMiddle = LayoutRules.FirstAvenueZ + LayoutRules.CrossingHalfZ + LayoutRules.StreetModule * 0.5f;
            for (int k = 0; k <= LayoutRules.FirstColumns; k++)
            {
                into.Add(new ModulePlace(ModuleKind.Street, LayoutRules.StreetX(k), streetMiddle, k, -1));
            }
            for (int k = 0; k <= LayoutRules.FirstColumns; k++)
            {
                into.Add(new ModulePlace(ModuleKind.Crossing, LayoutRules.StreetX(k), avenue, k, JunctionAt(k)));
            }
            for (int k = 0; k < LayoutRules.FirstColumns; k++)
            {
                into.Add(new ModulePlace(ModuleKind.Avenue, LayoutRules.StreetX(k) + LayoutRules.StreetPitch * 0.5f, avenue, k, -1));
            }
        }

        /// <summary>Where the game's old walls across the first block's street ends stand: one at each street's middle on the old wall line.</summary>
        public static void OldWalls(List<float> x, out float z)
        {
            x.Clear();
            for (int k = 0; k <= LayoutRules.FirstColumns; k++)
            {
                x.Add(LayoutRules.StreetX(k));
            }
            z = LayoutRules.OldWallZ + WallThick * 0.5f;
        }

        /// <summary>The new invisible walls round the first block's edge, as the game's own (StreetProps, 4.76 m): north, west and east.</summary>
        public static void NewWalls(List<PlanBox> into)
        {
            into.Clear();
            float avenue = LayoutRules.AvenueZ(1);
            float west = LayoutRules.StreetX(0) - LayoutRules.StreetHalf, east = LayoutRules.StreetX(LayoutRules.FirstColumns) + LayoutRules.StreetHalf;
            float north = avenue + LayoutRules.AvenueHalf;
            into.Add(new PlanBox(west - WallThick, north, east + WallThick, north + WallThick, WallHigh));
            into.Add(new PlanBox(west - WallThick, WallFrom, west, north + WallThick, WallHigh));
            into.Add(new PlanBox(east, WallFrom, east + WallThick, north + WallThick, WallHigh));
        }

        /// <summary>The ground collider under the whole first block (the Ground layer, its top the city's ground) and one under each carriageway (Roads).</summary>
        public static void Colliders(out PlanBox ground, List<PlanBox> roads)
        {
            float avenue = LayoutRules.AvenueZ(1);
            float west = LayoutRules.StreetX(0) - LayoutRules.CrossingHalfX, east = LayoutRules.StreetX(LayoutRules.FirstColumns) + LayoutRules.CrossingHalfX;
            ground = new PlanBox(west, WallFrom, east, avenue + LayoutRules.CrossingHalfZ + WallThick, UptownLots.GroundY);
            roads.Clear();
            for (int k = 0; k <= LayoutRules.FirstColumns; k++)
            {
                float c = LayoutRules.StreetX(k);
                roads.Add(new PlanBox(c - LayoutRules.StreetKerbWest, LayoutRules.OldWallZ, c + LayoutRules.StreetKerbEast, avenue - LayoutRules.AvenueKerb, UptownLots.GroundY));
            }
            roads.Add(new PlanBox(LayoutRules.StreetX(0) - LayoutRules.StreetHalf, avenue - LayoutRules.AvenueKerb, LayoutRules.StreetX(LayoutRules.FirstColumns) + LayoutRules.StreetHalf,
                avenue + LayoutRules.AvenueKerb, UptownLots.GroundY));
        }

        /// <summary>
        /// Where the backdrop's pieces are hidden (a piece whose middle lies in one): each old dead end with its dressing,
        /// and the first block's main area behind 1st Avenue's buildings to Uptown Avenue's north pavement. Its block
        /// grounds stay: they are the lots' ground.
        /// </summary>
        public static void Hides(List<GroundRect> into)
        {
            into.Clear();
            for (int k = 0; k <= LayoutRules.FirstColumns; k++)
            {
                float c = LayoutRules.StreetX(k);
                into.Add(new GroundRect(c - HideHalf, HideFrom, c + HideHalf, HideTo));
            }
            float avenue = LayoutRules.AvenueZ(1);
            into.Add(new GroundRect(LayoutRules.StreetX(0) - LayoutRules.StreetHalf, AreaFrom, LayoutRules.StreetX(LayoutRules.FirstColumns) + LayoutRules.StreetHalf,
                avenue + LayoutRules.AvenueHalf + 0.5f));
        }

        // ---- the people and the buses

        /// <summary>The pedestrians' zones: one along each street's east pavement and one along each stretch of Uptown Avenue's south pavement.</summary>
        public static void Zones(List<PlanBox> into)
        {
            into.Clear();
            float avenue = LayoutRules.AvenueZ(1);
            float middle = (ModuleClear + avenue - LayoutRules.AvenueHalf) * 0.5f;
            for (int k = 0; k <= LayoutRules.FirstColumns; k++)
            {
                float x = (LayoutRules.StreetX(k) + LayoutRules.StreetKerbEast + Outer(k, true)) * 0.5f;   // the middle of the pavement as laid
                into.Add(new PlanBox(x - ZoneWidth * 0.5f, middle - ZoneLength * 0.5f, x + ZoneWidth * 0.5f, middle + ZoneLength * 0.5f, GroundTop));
            }
            float z = avenue - (LayoutRules.AvenueHalf + LayoutRules.AvenueKerb) * 0.5f;
            for (int k = 0; k < LayoutRules.FirstColumns; k++)
            {
                float x = LayoutRules.StreetX(k) + LayoutRules.StreetPitch * 0.5f;
                into.Add(new PlanBox(x - ZoneLength * 0.5f, z - ZoneWidth * 0.5f, x + ZoneLength * 0.5f, z + ZoneWidth * 0.5f, GroundTop));
            }
        }

        /// <summary>The bus terminus: Uptown Avenue's south pavement between 5th and 4th Street, the middle of the first block.</summary>
        public static void Terminus(out float x, out float z)
        {
            x = LayoutRules.StreetX(4) + LayoutRules.StreetPitch * 0.5f;
            z = LayoutRules.AvenueZ(1) - TerminusInset;
        }

        // ---- what the load lays, counted, for the census

        public const int StreetParking = 1;   // parking lanes in the game's street module (along its west kerb), Build 3682
        public const int AvenueParking = 2;   // ... and in its avenue module (along both kerbs)

        /// <summary>The first block's streets, 9th to 1st Street.</summary>
        public static int StreetCount => LayoutRules.FirstColumns + 1;

        /// <summary>The road modules its art copies: each street's, each crossing with the avenue, the avenue between them.</summary>
        public static int ModuleCount => StreetCount * 2 + LayoutRules.FirstColumns;

        /// <summary>The game's walls across the old street ends that open, one a street.</summary>
        public static int OldWallCount => StreetCount;

        /// <summary>The pedestrians' zones: one along each street, one along each stretch of the avenue.</summary>
        public static int ZoneCount => StreetCount + LayoutRules.FirstColumns;

        /// <summary>The parking lanes along the copied streets and the avenue.</summary>
        public static int ParkingCount => StreetCount * StreetParking + LayoutRules.FirstColumns * AvenueParking;

        /// <summary>The street with lanes nearest a place west to east (5th Street has none), 9th Street first where two are as near.</summary>
        public static int NearestLanes(float x)
        {
            int best = Lanes[0];
            float nearest = float.MaxValue;
            for (int i = 0; i < Lanes.Length; i++)
            {
                float d = System.Math.Abs(LayoutRules.StreetX(Lanes[i]) - x);
                if (d < nearest)
                {
                    nearest = d;
                    best = Lanes[i];
                }
            }
            return best;
        }
    }
}
