using System.Collections.Generic;
using RealLifeOverhaul.NewGround;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// The game's pieces a district's art is copied from: a street of two lanes, an avenue of four, a crossing of the two,
    /// and a block's ground.
    /// </summary>
    public enum ModuleKind
    {
        Street = 0,      // the game's SM_RoadModule- 120-70: 100 m of a street, its pavements and parking lane
        Avenue = 1,      // SM_RoadModule 120-160-70: 50 m of an avenue
        Crossing = 2,    // SM_RoadModule Intersection_4-2 Lanes: an avenue crossing a street, its lights and crosswalks
        Ground = 3       // SM_120-70 GroundPlane: a block's ground, one to every block of the grid (the backdrop's own are put away under it)
    }

    /// <summary>
    /// One copy of a piece of the game's: what, where (its middle), the street or avenue it lies on (a street's place west
    /// to east, 9th Street 0; an avenue's row, 1st Avenue 0), which of the plan's junctions a crossing is (or -1), and
    /// whether it is scenery past the district's walls (no parking, no lights, no name).
    /// </summary>
    public struct ModulePlace
    {
        public ModuleKind Kind;
        public float X;          // m: the piece's middle, as the game's own stand by theirs
        public float Z;
        public int Street;       // the street it lies on or crosses (9th Street is 0), or for an avenue's length the street west of it
        public int Row;          // the avenue it lies on or meets (1st Avenue 0, Uptown Avenue 1), or the row of blocks for a street or a ground
        public int Junction;     // a crossing's junction among the plan's, or -1 where no lanes cross
        public bool Scenery;     // past the walls round the district: seen, never walked or driven

        public ModulePlace(ModuleKind kind, float x, float z, int street, int row, int junction, bool scenery)
        {
            Kind = kind;
            X = x;
            Z = z;
            Street = street;
            Row = row;
            Junction = junction;
            Scenery = scenery;
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

    /// <summary>A terminus for the buses: where it stands, the text a line to it says it goes to, and its name for the log.</summary>
    public struct PlanTerminus
    {
        public float X;
        public float Z;
        public string NameKey;
        public string Name;

        public PlanTerminus(float x, float z, string nameKey, string name)
        {
            X = x;
            Z = z;
            NameKey = nameKey;
            Name = name;
        }
    }

    /// <summary>
    /// How the whole band is laid at every load (game-free; the game's side does what it says): three rows of eighteen
    /// blocks north of 1st Avenue between Willow and Lake Street. Its streets as the traffic's lanes: eight of Manhattan's
    /// streets continued through 1st Avenue's own junctions, 1st Avenue itself continued west through its own corner at
    /// 9th Street, the nineteen streets and three avenues meeting at junctions with lights, the avenue's two ways on one
    /// green and the street's two arms on the other, as at the game's crossings. The walking ground for the player (every
    /// street, avenue and block) and for the pedestrians (pavements and crosswalks), joined to Manhattan's at every street
    /// end, both pavements and the carriageway, and at 1st Avenue's two corners, keeping off the scaffolds and the
    /// building that stand on the old corners' pavements. The game's own road modules and block grounds copied along it,
    /// and one row of them past its walls as scenery, so the camera never looks out on nothing; the old walls opened and
    /// new ones round its edge; its ground and road colliders; the backdrop put away over it; the pedestrians' zones and
    /// three bus termini. Every figure is the layout's or measured from the game's own scene (Build 3682); the load
    /// checks the game's ground against them and stands down where it has changed.
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
        // Where the new ground starts over the game's own walking meshes so the two join: the game's player mesh reaches z
        // 479.4 to 480.9 over the nine streets' carriageways, both meshes z 479.3 to 480.9 over their pavements (read from
        // the game's data, Build 3682), so each start keeps at least 0.8 m of overlap, the agent's margin taken off.
        public const float ArmFrom = 476f;              // m: the carriageway's ground begins this far north, over the old dead end's
        public const float JoinFrom = 478.5f;           // m: a pavement's ground begins here, on the old corner
        public const float CornerJoin = 3f;             // m past the game's corner walls 1st Avenue's continuation reaches over the old ground
        public const float KeepClear = 0.3f;            // m the ground keeps off what stands on the old corners' pavements
        public const float Least = 1f;                  // m: a piece of pavement narrower than this either way is not laid
        public const float ModuleClear = 488.18f;       // m: where 1st Avenue's crossing modules end and the old arms' pavements were backdrop
        public const float BackClear = 0.3f;            // m kept from the backs of 1st Avenue's buildings and their scaffolds
        public const float EdgeClear = 0.5f;            // m the outermost ground keeps off the district's edge, where its walls stand
        public const float WallHigh = 4.76f;            // m: the game's own invisible walls' height
        public const float WallThick = 5f;              // m: and their depth
        public const float SouthWall = 452.9f;          // m: 1st Avenue's continuations are walled along here, south of their lanes: the
                                                        // backdrop's buildings south of them stand to z 452.6
        public const float OldWallReach = 12f;          // m from an old wall's place the game's wall there is looked for
        public const float HideHalf = 12.5f;            // m each side of a street end's middle the dead end's dressing is hidden
        public const float HideFrom = 476f;             // m: and from here north
        public const float HideTo = 530f;               // m: to here, behind 1st Avenue's buildings
        public const float AreaFrom = 498f;             // m: the first block's main area, where the backdrop goes, begins here
        public const float HideBeyond = 130f;           // m past the district's walls the backdrop is put away too, under its scenery
        public const float GroundCollider = 1f;         // m: the ground collider's thickness, its top the city's ground
        public const float RoadCollider = 0.2f;         // m: a road collider's thickness
        public const float CrosswalkNear = 6.0f;        // m from a crossing's middle a crosswalk over the avenue begins, the game's
        public const float CrosswalkFar = 9.6f;         // m: and ends
        public const float ZoneLength = 40f;            // m: a pedestrians' zone along a pavement
        public const float ZoneWidth = 3f;              // m: across it
        public const float MapMargin = 60f;             // m the city map's bounds reach past the district's ground: past every lot by more than a made building needs (40)
        public const float TerminusInset = 11.8f;       // m south of an avenue's middle: its south pavement, where a terminus stands
        public const float SeedY = 0.05f;               // m: the height the seeds on 1st Avenue are looked for at

        private const int Streets0 = LayoutStreets.WestMost;   // the westmost street's place
        private const int StreetsN = LayoutStreets.EastMost - LayoutStreets.WestMost + 1;   // nineteen
        private const int Rows = LayoutRules.Rows;

        // 1st Avenue's corners: its own walls across its two ends (the game's, Build 3682), opened for its continuations.
        public const float WestCornerX = -315.7f;       // m: the middle of the wall across 1st Avenue at 9th Street
        public const float EastCornerX = 409.1f;        // m: ... and at 1st Street
        public const float CornerWallZ = 466.5f;        // m: their middles' z
        public const float WestCornerJoin = -310f;      // m: 1st Avenue West's ground reaches east to here, over the old corner's
        public const float EastCornerJoin = 404f;       // m: 1st Avenue East's reaches west to here
        public const float WestKeptWall = -321.1f;      // m: the game's wall across the south-west corner of 9th Street, which stays
        // That wall's own ground (MapBorder's City Bounds Colliders, Build 3682): it stands 2 m onto 1st Avenue West's
        // carriageway at the corner, so the player's ground there keeps off it as off a scaffold.
        private static readonly FlatRect KeptWall = new FlatRect(-321.15f, 446.86f, -313.17f, 456.72f);
        public const float EastBuilding = 429.7f;       // m: the game's building at 2 1st Street stands on 1st Avenue's south side to here

        // The streets whose lanes go through 1st Avenue's junctions: all of Manhattan's but 5th Street, which 1st Avenue's
        // traffic passes without a junction.
        private static readonly int[] Through = { 0, 1, 2, 3, 5, 6, 7, 8 };

        // What stands on the first block's pavements that the walking ground keeps off (m, the game's scene, Build 3682): the
        // street, then x from its middle and z, both ways. Small things (trees, bins, bags) are walked round as they stand.
        private static readonly float[] KeepOff =
        {
            1, 8.53f, 8.65f, 478.49f, 508.53f,     // 8th Street's east pavement: the ground floor of the corner building east of it
            3, -7.49f, -6.91f, 480.95f, 503.68f,   // 6th Street's west pavement: the corner building's scaffolding
            5, -7.38f, -7.02f, 485.18f, 508.92f,   // 4th Street's west pavement: the same
            6, -7.94f, -7.59f, 480.00f, 506.77f,   // 3rd Street's
            8, -7.48f, -7.12f, 485.18f, 508.92f,   // 1st Street's
        };

        // ---- the grid

        public static float West => LayoutRules.StreetX(LayoutStreets.WestMost) - LayoutRules.StreetHalf;   // m: the band's west edge
        public static float East => LayoutRules.StreetX(LayoutStreets.EastMost) + LayoutRules.StreetHalf;   // m: its east edge
        public static float North => LayoutRules.AvenueZ(Rows) + LayoutRules.AvenueHalf;                    // m: its north edge, Crest Avenue's north pavement
        public static float RowOneFrom => LayoutRules.AvenueZ(0) + LayoutRules.AvenueHalf;                  // m: 1st Avenue's north pavement's back, where row 1 begins
        public static float South => LayoutRules.AvenueZ(0) - LayoutRules.AvenueHalf;                      // m: 1st Avenue's south pavement's back, its continuations' edge
        // The ring's strips past the band's outer streets and Crest Avenue are built (LayoutRules.IsRing): the district's walls
        // stand behind them, and its outer pavements go to the band's edge, the ring's buildings' fronts.
        public static float RingWest => West - LayoutRules.RingDepth(false);   // m: the ring's west strips' backs, where the west wall stands
        public static float RingEast => East + LayoutRules.RingDepth(false);   // m: its east strips' backs
        public static float RingNorth => North + LayoutRules.RingDepth(true);  // m: its north strips' backs

        /// <summary>True for a street of the first block, Manhattan's own continued (9th to 1st Street).</summary>
        public static bool Manhattan(int street)
        {
            return street >= 0 && street <= LayoutRules.FirstColumns;
        }

        /// <summary>True for a street whose lanes go through 1st Avenue's own junction (all of Manhattan's but 5th Street).</summary>
        public static bool HasThrough(int street)
        {
            return System.Array.IndexOf(Through, street) >= 0;
        }

        /// <summary>True where a street's lanes are laid in a row of blocks: everywhere but 5th Street's first row, which has no junction at 1st Avenue.</summary>
        public static bool HasLanes(int street, int row)
        {
            if (street < LayoutStreets.WestMost || street > LayoutStreets.EastMost || row < 1 || row > Rows)
            {
                return false;
            }
            return row > 1 || !Manhattan(street) || HasThrough(street);
        }

        /// <summary>True for 1st Avenue's continuation at a street: west of 9th Street and east of 1st Street.</summary>
        public static bool OnFirstAvenue(int street)
        {
            return (street >= LayoutStreets.WestMost && street < 0) || (street > LayoutRules.FirstColumns && street <= LayoutStreets.EastMost);
        }

        /// <summary>
        /// The plan's junction where a street meets an avenue (row 1 to 3: Uptown, Summit and Crest Avenue; row 0: 1st
        /// Avenue's continuations west of 9th and east of 1st Street), or -1 where the plan has none (1st Avenue's own).
        /// </summary>
        public static int JunctionAt(int street, int row)
        {
            if (street < LayoutStreets.WestMost || street > LayoutStreets.EastMost)
            {
                return -1;
            }
            if (row >= 1 && row <= Rows)
            {
                return (row - 1) * StreetsN + street - Streets0;
            }
            if (row != 0 || !OnFirstAvenue(street))
            {
                return -1;
            }
            return Rows * StreetsN + (street < 0 ? street - Streets0 : -Streets0 + street - LayoutRules.FirstColumns - 1);
        }

        /// <summary>
        /// True where an avenue's lanes leave a junction on one side: everywhere along Uptown, Summit and Crest Avenue but past
        /// the band's ends; on 1st Avenue's continuations, toward its own corner at 9th Street, never from Hill Street west to 1st
        /// Street (its own corner there takes no new way in).
        /// </summary>
        public static bool AvenueArm(int street, int row, bool east)
        {
            if (row >= 1 && row <= Rows && street >= LayoutStreets.WestMost && street <= LayoutStreets.EastMost)
            {
                return east ? street < LayoutStreets.EastMost : street > LayoutStreets.WestMost;
            }
            if (row != 0 || !OnFirstAvenue(street))
            {
                return false;
            }
            if (street < 0)
            {
                return east || street > LayoutStreets.WestMost;
            }
            return east ? street < LayoutStreets.EastMost : street > LayoutRules.FirstColumns + 1;
        }

        /// <summary>The plan's junctions: three avenues of nineteen and 1st Avenue's ten.</summary>
        public static int JunctionCount => Rows * StreetsN + 2 * -Streets0;

        // ---- the streets

        /// <summary>
        /// The band's streets as a plan of the traffic's lanes: each street of two lanes between its avenues (Manhattan's
        /// continued through 1st Avenue's own junctions), each avenue of four lanes between its streets, 1st Avenue's
        /// continuations (the west one through its own corner at 9th Street), every crossing a junction with lights where
        /// the avenue's two ways share one green and the street's two arms the other, as at the game's own. A new plan each
        /// call: the game's side fills in its junctions at the load.
        /// </summary>
        public static RoadPlan Streets()
        {
            var plan = new RoadPlan { Name = "uptown" };
            for (int j = 0; j < JunctionCount; j++)
            {
                plan.Junctions.Add(new PlanJunction { Lights = true, Inset = AvenueStop });
            }
            for (int row = 0; row <= Rows; row++)
            {
                for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
                {
                    int j = JunctionAt(k, row);
                    if (j >= 0)
                    {
                        plan.Junctions[j].X = LayoutRules.StreetX(k);
                        plan.Junctions[j].Z = LayoutRules.AvenueZ(row);
                    }
                }
            }
            for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
            {
                float x = LayoutRules.StreetX(k) + LayoutRules.StreetLanes;
                for (int row = 1; row <= Rows; row++)
                {
                    if (!HasLanes(k, row))
                    {
                        continue;
                    }
                    int road = plan.Roads.Count;
                    var street = new PlanRoad { Ahead = 1, Back = 1, LaneWidth = LayoutRules.StreetLaneWidth, Speed = StreetSpeed, End = JunctionAt(k, row), EndInset = StreetStop };
                    plan.Junctions[street.End].Across.Add(road);
                    if (row == 1 && Manhattan(k))
                    {
                        street.Point(x, 0f, ThroughZ);
                        plan.Throughs.Add(new PlanThrough { Road = road, AtStart = true, X = LayoutRules.StreetX(k), Z = ThroughFind });
                    }
                    else
                    {
                        street.Start = JunctionAt(k, row - 1);
                        street.StartInset = StreetStop;
                        street.Point(x, 0f, LayoutRules.AvenueZ(row - 1));
                        plan.Junctions[street.Start].Across.Add(road);
                    }
                    plan.Roads.Add(street.Point(x, 0f, LayoutRules.AvenueZ(row)));
                }
            }
            for (int row = 0; row <= Rows; row++)
            {
                float z = LayoutRules.AvenueZ(row) + AvenueLaneLine;
                for (int k = LayoutStreets.WestMost; k < LayoutStreets.EastMost; k++)
                {
                    int a = JunctionAt(k, row), b = JunctionAt(k + 1, row);
                    if (row == 0 && k == -1)
                    {
                        // 1st Avenue West's last length: from Cedar Street into 1st Avenue's own corner at 9th Street, through it.
                        int last = plan.Roads.Count;
                        plan.Roads.Add(Avenue(a, -1).Point(LayoutRules.StreetX(-1), 0f, z).Point(LayoutRules.StreetX(0) - AvenueStop, 0f, z));
                        plan.Junctions[a].Together.Add(last);
                        plan.Throughs.Add(new PlanThrough { Road = last, AtStart = false, X = LayoutRules.StreetX(0), Z = ThroughFind });
                        continue;
                    }
                    if (a < 0 || b < 0)
                    {
                        continue;   // 1st Avenue's own between 9th and 1st Street, and the gap from 1st Street to Hill Street
                    }
                    int road = plan.Roads.Count;
                    plan.Roads.Add(Avenue(a, b).Point(LayoutRules.StreetX(k), 0f, z).Point(LayoutRules.StreetX(k + 1), 0f, z));
                    plan.Junctions[a].Together.Add(road);
                    plan.Junctions[b].Together.Add(road);
                }
            }
            return plan;
        }

        private static PlanRoad Avenue(int start, int end)
        {
            return new PlanRoad { Ahead = 2, Back = 2, LaneWidth = LayoutRules.AvenueLaneWidth, Speed = StreetSpeed, Start = start, End = end };
        }

        /// <summary>The street with lanes through 1st Avenue nearest a place west to east (5th Street has none), 9th Street first where two are as near.</summary>
        public static int NearestThrough(float x)
        {
            int best = Through[0];
            float nearest = float.MaxValue;
            for (int i = 0; i < Through.Length; i++)
            {
                float d = System.Math.Abs(LayoutRules.StreetX(Through[i]) - x);
                if (d < nearest)
                {
                    nearest = d;
                    best = Through[i];
                }
            }
            return best;
        }

        // ---- the walking ground

        /// <summary>
        /// The player's walking ground: every street, avenue and block of the band whole, its outer edge kept off the walls;
        /// in the first block, each carriageway from the old dead end and its pavements from the old corner (over Manhattan's
        /// own ground), off the scaffolds and the building fronts there, and each block from the backs of 1st Avenue's
        /// buildings; 1st Avenue's continuations from over its own corners, the west one off the game's wall at 9th Street's
        /// corner. Seeds on 1st Avenue's carriageway: the city's ground it joins. No game collider stands on any of it once
        /// the backdrop is put away (ExpectColliders 0, the build it was measured on); small things are walked round.
        /// </summary>
        public static GroundPlan PlayerGround()
        {
            var plan = new GroundPlan { Name = "uptown player", Player = true, Npc = false, ExpectColliders = 0 };
            float uptown = LayoutRules.AvenueZ(1), south = uptown - LayoutRules.AvenueHalf;
            for (int k = 0; k <= LayoutRules.FirstColumns; k++)
            {
                float c = LayoutRules.StreetX(k);
                plan.Boxes.Add(Box(c - LayoutRules.StreetKerbWest, ArmFrom, c + LayoutRules.StreetKerbEast, uptown - LayoutRules.AvenueKerb));
                Pavements(k, south, plan.Boxes);
            }
            for (int b = 0; b < LayoutRules.FirstColumns; b++)
            {
                int column = LayoutRules.FirstColumn + b;
                LayoutRules.Block(1, column, out float x0, out _, out float x1, out float z1);
                plan.Boxes.Add(Box(x0, LayoutRules.FirstBack(column) + BackClear, x1, z1));
            }
            for (int row = 1; row <= Rows; row++)
            {
                for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
                {
                    if (row == 1 && Manhattan(k))
                    {
                        continue;
                    }
                    plan.Boxes.Add(Box(Outer(k, false), BlockSouth(row), Outer(k, true), BlockNorth(row)));
                }
                for (int column = 1; column <= LayoutRules.Columns; column++)
                {
                    if (LayoutRules.IsFirst(row, column))
                    {
                        continue;
                    }
                    LayoutRules.Block(row, column, out float x0, out float z0, out float x1, out float z1);
                    plan.Boxes.Add(Box(x0, z0, x1, z1));
                }
                float a = LayoutRules.AvenueZ(row);
                plan.Boxes.Add(Box(West + EdgeClear, a - LayoutRules.AvenueHalf, East - EdgeClear, a + LayoutRules.AvenueHalf));
            }
            Ring(plan.Boxes);
            float first = LayoutRules.AvenueZ(0);
            var wall = new List<FlatRect>(1) { KeptWall.Grown(KeepClear) };
            GroundShapes.Cut(Box(West + EdgeClear, first - LayoutRules.AvenueKerb, WestCornerJoin, RowOneFrom), wall, Least, plan.Boxes);
            plan.Boxes.Add(Box(EastCornerJoin, first - LayoutRules.AvenueKerb, East - EdgeClear, RowOneFrom));
            for (int k = 0; k <= LayoutRules.FirstColumns; k++)
            {
                plan.Seeds.Add(new GroundPoint(LayoutRules.StreetX(k), SeedY, first));
                if (k < LayoutRules.FirstColumns)
                {
                    plan.Seeds.Add(new GroundPoint(LayoutRules.StreetX(k) + LayoutRules.StreetPitch * 0.5f, SeedY, first));   // mid-block, between its own corners
                }
            }
            Area(plan.Area);
            return plan;
        }

        /// <summary>
        /// The pedestrians' walking ground: every pavement of the band's streets and avenues, the first block's from the old
        /// corners as the player's, 1st Avenue's continuations' north pavements, and the crosswalks over the avenues at
        /// every junction, the parks' paths and the school campus's block (Campus). Never a carriageway nor any other block. Seeds on 1st Avenue's north pavement.
        /// </summary>
        public static GroundPlan NpcGround()
        {
            var plan = new GroundPlan { Name = "uptown pedestrians", Player = false, Npc = true, ExpectColliders = 0 };
            float south = LayoutRules.AvenueZ(1) - LayoutRules.AvenueHalf;
            for (int k = 0; k <= LayoutRules.FirstColumns; k++)
            {
                Pavements(k, south, plan.Boxes);
            }
            for (int row = 1; row <= Rows; row++)
            {
                for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
                {
                    if (row == 1 && Manhattan(k))
                    {
                        continue;
                    }
                    float c = LayoutRules.StreetX(k);
                    plan.Boxes.Add(Box(Outer(k, false), BlockSouth(row), c - LayoutRules.StreetKerbWest, BlockNorth(row)));
                    plan.Boxes.Add(Box(c + LayoutRules.StreetKerbEast, BlockSouth(row), Outer(k, true), BlockNorth(row)));
                }
                float a = LayoutRules.AvenueZ(row);
                plan.Boxes.Add(Box(West + EdgeClear, a - LayoutRules.AvenueHalf, East - EdgeClear, a - LayoutRules.AvenueKerb));
                plan.Boxes.Add(Box(West + EdgeClear, a + LayoutRules.AvenueKerb, East - EdgeClear, a + LayoutRules.AvenueHalf));
                for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
                {
                    float c = LayoutRules.StreetX(k);
                    if (k > LayoutStreets.WestMost)
                    {
                        plan.Boxes.Add(Box(c - CrosswalkFar, a - LayoutRules.AvenueKerb, c - CrosswalkNear, a + LayoutRules.AvenueKerb));
                    }
                    if (k < LayoutStreets.EastMost)
                    {
                        plan.Boxes.Add(Box(c + CrosswalkNear, a - LayoutRules.AvenueKerb, c + CrosswalkFar, a + LayoutRules.AvenueKerb));
                    }
                }
            }
            float first = LayoutRules.AvenueZ(0);
            plan.Boxes.Add(Box(West + EdgeClear, first + LayoutRules.AvenueKerb, WestCornerJoin, RowOneFrom));
            plan.Boxes.Add(Box(EastCornerJoin, first + LayoutRules.AvenueKerb, East - EdgeClear, RowOneFrom));
            float pavement = first + (LayoutRules.AvenueKerb + LayoutRules.AvenueHalf) * 0.5f;
            for (int k = 0; k < LayoutRules.FirstColumns; k++)
            {
                plan.Seeds.Add(new GroundPoint(LayoutRules.StreetX(k) + LayoutRules.StreetPitch * 0.5f, SeedY, pavement));
            }
            Parks(plan.Boxes);
            Campus(plan.Boxes);
            Area(plan.Area);
            return plan;
        }

        // The school campus's ground, walked by the pedestrians as the player walks it: its whole block, reaching PavementReach over the pavements round it. The campus's fence
        // (carved out of both meshes along its runs) and its buildings and pieces shut it again, so people come in and go out by its three gates alone, where the fence is
        // broken. Without it the pedestrians' mesh stopped at the block's edge and no one could walk the campus (Player.log, 26 Oct 2026: 0 of 9 routes for people, 9 for the player).
        private static void Campus(List<GroundBox> into)
        {
            LayoutRules.Block(LayoutRules.CampusBlock / 100, LayoutRules.CampusBlock % 100, out float x0, out float z0, out float x1, out float z1);
            float reach = ParksRules.PavementReach;
            into.Add(Box(x0 - reach, z0 - reach, x1 + reach, z1 + reach));
        }

        // The parks' paths, walked by the pedestrians as the pavements are: each joined at the block's edge to the pavement it opens on.
        private static void Parks(List<GroundBox> into)
        {
            var blocks = new List<int>(4);
            var walks = new List<PlanBox>(10);
            ParksRules.Blocks(blocks);
            for (int b = 0; b < blocks.Count; b++)
            {
                ParksRules.Walks(blocks[b], walks);
                for (int i = 0; i < walks.Count; i++)
                {
                    PlanBox w = walks[i];
                    into.Add(Box(w.X - w.HalfX, w.Z - w.HalfZ, w.X + w.HalfX, w.Z + w.HalfZ));
                }
            }
        }

        // The ring's built strips, walked as the band's blocks are, their backs kept off the walls.
        private static void Ring(List<GroundBox> into)
        {
            for (int row = 1; row <= Rows + 1; row++)
            {
                for (int column = 0; column <= LayoutRules.Columns + 1; column++)
                {
                    if (!LayoutRules.IsRing(row, column))
                    {
                        continue;
                    }
                    LayoutRules.RingStrip(row, column, out float x0, out float z0, out float x1, out float z1, out int facing);
                    into.Add(Box(facing == 90 ? x0 + EdgeClear : x0, z0, facing == 270 ? x1 - EdgeClear : x1, facing == 180 ? z1 - EdgeClear : z1));
                }
            }
        }

        // The band's whole ground, rebuilt for both meshes: what the game baked on the backdrop between the new ground's boxes goes.
        private static void Area(List<FlatRect> into)
        {
            List<GroundRect> band = LayoutRules.BandGround;
            for (int i = 0; i < band.Count; i++)
            {
                into.Add(new FlatRect(band[i].X1, band[i].Z1, band[i].X2, band[i].Z2));
            }
        }

        // A first-block street's two pavements from the old corner to the avenue's pavement, what stands there cut out of them.
        private static void Pavements(int k, float to, List<GroundBox> into)
        {
            float c = LayoutRules.StreetX(k);
            var holes = new List<FlatRect>(2);
            for (int side = 0; side < 2; side++)
            {
                bool east = side == 1;
                holes.Clear();
                for (int i = 0; i + 4 < KeepOff.Length; i += 5)
                {
                    if ((int)KeepOff[i] == k && (KeepOff[i + 1] > 0f) == east)
                    {
                        holes.Add(new FlatRect(c + KeepOff[i + 1], KeepOff[i + 3], c + KeepOff[i + 2], KeepOff[i + 4]).Grown(KeepClear));
                    }
                }
                GroundBox strip = east ? Box(c + LayoutRules.StreetKerbEast, JoinFrom, c + LayoutRules.StreetHalf, to) : Box(c - LayoutRules.StreetHalf, JoinFrom, c - LayoutRules.StreetKerbWest, to);
                GroundShapes.Cut(strip, holes, Least, into);
            }
        }

        /// <summary>
        /// What of the game's the walking ground keeps off, as it stands (the clearance not added): the scaffolds and
        /// building fronts on the first block's pavements, then the wall at 9th Street's corner. For the offline checks.
        /// </summary>
        public static void KeptOff(List<FlatRect> into)
        {
            into.Clear();
            for (int i = 0; i + 4 < KeepOff.Length; i += 5)
            {
                float c = LayoutRules.StreetX((int)KeepOff[i]);
                into.Add(new FlatRect(c + KeepOff[i + 1], KeepOff[i + 3], c + KeepOff[i + 2], KeepOff[i + 4]));
            }
            into.Add(KeptWall);
        }

        // A street's outer edge on one side, its pavement's: the band's outermost two keep off its edge.
        private static float Outer(int street, bool east)
        {
            float c = LayoutRules.StreetX(street);
            return east ? c + LayoutRules.StreetHalf : c - LayoutRules.StreetHalf;   // the outer streets' outer pavements meet the ring's buildings, no wall
        }

        // A row of blocks' ground between its avenues: from the avenue south of it (1st Avenue's north pavement for row 1).
        private static float BlockSouth(int row)
        {
            return LayoutRules.AvenueZ(row - 1) + LayoutRules.AvenueHalf;
        }

        private static float BlockNorth(int row)
        {
            return LayoutRules.AvenueZ(row) - LayoutRules.AvenueHalf;
        }

        private static GroundBox Box(float x0, float z0, float x1, float z1)
        {
            return new GroundBox((x0 + x1) * 0.5f, (z0 + z1) * 0.5f, GroundTop, System.Math.Abs(x1 - x0) * 0.5f, System.Math.Abs(z1 - z0) * 0.5f, 0f, GroundDepth);
        }

        // ---- the art, the walls, the colliders

        /// <summary>
        /// The game's pieces copied along the band: each street's 100 m between its avenues, each avenue's 50 m between its
        /// streets, each crossing, 1st Avenue's continuations, and a block's ground on every block of the band (the first
        /// block's too) and of the row of scenery round it, each on the grid's own place, the backdrop's block grounds
        /// under them put away (they lay off the grid, at the pavements' own height); past the walls, the streets go on
        /// north one length and the avenues west and east one, as scenery.
        /// </summary>
        public static void Modules(List<ModulePlace> into)
        {
            into.Clear();
            for (int row = 1; row <= Rows + 1; row++)
            {
                float middle = LayoutRules.AvenueZ(row - 1) + LayoutRules.CrossingHalfZ + LayoutRules.StreetModule * 0.5f;
                for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
                {
                    into.Add(new ModulePlace(ModuleKind.Street, LayoutRules.StreetX(k), middle, k, row, -1, row > Rows));
                }
            }
            for (int row = 0; row <= Rows; row++)
            {
                float z = LayoutRules.AvenueZ(row);
                for (int k = LayoutStreets.WestMost - 1; k <= LayoutStreets.EastMost; k++)
                {
                    bool scenery = k < LayoutStreets.WestMost || k == LayoutStreets.EastMost;
                    if (row == 0 && k >= 0 && k < LayoutRules.FirstColumns)
                    {
                        continue;   // 1st Avenue's own
                    }
                    into.Add(new ModulePlace(ModuleKind.Avenue, LayoutRules.StreetX(k) + LayoutRules.StreetPitch * 0.5f, z, k, row, -1, scenery));
                }
                for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
                {
                    if (row == 0 && !OnFirstAvenue(k))
                    {
                        continue;
                    }
                    into.Add(new ModulePlace(ModuleKind.Crossing, LayoutRules.StreetX(k), z, k, row, JunctionAt(k, row), false));
                }
            }
            for (int row = 0; row <= Rows + 1; row++)
            {
                for (int column = 0; column <= LayoutRules.Columns + 1; column++)
                {
                    bool band = row >= 1 && row <= Rows && column >= 1 && column <= LayoutRules.Columns;
                    bool ring = !band && (row == Rows + 1 || column == 0 || column == LayoutRules.Columns + 1);
                    if (!band && !ring)
                    {
                        continue;   // south of 1st Avenue, between the corners: the backdrop's and Manhattan's own
                    }
                    int west = LayoutRules.WestStreet(column);
                    float x = (LayoutRules.StreetX(west) + LayoutRules.StreetX(west + 1)) * 0.5f;
                    float z = (LayoutRules.AvenueZ(row - 1) + LayoutRules.AvenueZ(row)) * 0.5f;
                    into.Add(new ModulePlace(ModuleKind.Ground, x, z, west, row, -1, ring));
                }
            }
        }

        /// <summary>
        /// Where the game's old walls stand that open: one across each first-block street's end on the old wall line, and the
        /// two across 1st Avenue's ends at 9th and 1st Street. The wall across 9th Street's south-west corner stays.
        /// </summary>
        public static void OldWalls(List<float> x, List<float> z)
        {
            x.Clear();
            z.Clear();
            for (int k = 0; k <= LayoutRules.FirstColumns; k++)
            {
                x.Add(LayoutRules.StreetX(k));
                z.Add(LayoutRules.OldWallZ + WallThick * 0.5f);
            }
            x.Add(WestCornerX);
            z.Add(CornerWallZ);
            x.Add(EastCornerX);
            z.Add(CornerWallZ);
        }

        /// <summary>
        /// The new invisible walls round the band's edge, as the game's own (StreetProps, 4.76 m): north, west and east of
        /// it behind the ring's buildings, across 1st Avenue's continuations' ends and their corners with the ring, and along
        /// the south of 1st Avenue's continuations, from the backdrop's buildings south of them.
        /// </summary>
        public static void NewWalls(List<PlanBox> into)
        {
            into.Clear();
            // Behind the ring's strips; 1st Avenue's continuations walled at the band's edge as before, up to row 1's ring.
            float west = RingWest, east = RingEast, north = RingNorth, south = South;
            into.Add(new PlanBox(west - WallThick, north, east + WallThick, north + WallThick, WallHigh));
            into.Add(new PlanBox(west - WallThick, RowOneFrom, west, north + WallThick, WallHigh));
            into.Add(new PlanBox(east, RowOneFrom, east + WallThick, north + WallThick, WallHigh));
            into.Add(new PlanBox(West - WallThick, south - WallThick, West, RowOneFrom, WallHigh));
            into.Add(new PlanBox(East, south - WallThick, East + WallThick, RowOneFrom, WallHigh));
            into.Add(new PlanBox(west - WallThick, RowOneFrom - WallThick, West - WallThick, RowOneFrom, WallHigh));
            into.Add(new PlanBox(East + WallThick, RowOneFrom - WallThick, east + WallThick, RowOneFrom, WallHigh));
            west = West;
            east = East;
            into.Add(new PlanBox(west, SouthWall - WallThick, WestKeptWall, SouthWall, WallHigh));
            into.Add(new PlanBox(EastBuilding, SouthWall - WallThick, east, SouthWall, WallHigh));
        }

        /// <summary>
        /// The ground colliders under the band (the Ground layer, their top the city's ground: the city's own ground ends at
        /// z 606), one under row 1 to Crest Avenue and one under each of 1st Avenue's continuations, and one road collider
        /// under each carriageway (Roads).
        /// </summary>
        public static void Colliders(List<PlanBox> grounds, List<PlanBox> roads)
        {
            grounds.Clear();
            roads.Clear();
            float west = West - WallThick, east = East + WallThick, first = LayoutRules.AvenueZ(0);
            grounds.Add(new PlanBox(RingWest - WallThick, RowOneFrom, RingEast + WallThick, RingNorth + WallThick, UptownLots.GroundY));
            grounds.Add(new PlanBox(west, South - WallThick, LayoutRules.StreetX(0) - LayoutRules.CrossingHalfX, RowOneFrom, UptownLots.GroundY));
            grounds.Add(new PlanBox(LayoutRules.StreetX(LayoutRules.FirstColumns) + LayoutRules.CrossingHalfX, South - WallThick, east, RowOneFrom, UptownLots.GroundY));
            for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
            {
                float c = LayoutRules.StreetX(k);
                for (int row = 1; row <= Rows; row++)
                {
                    float from = row == 1 && Manhattan(k) ? LayoutRules.OldWallZ : LayoutRules.AvenueZ(row - 1) + LayoutRules.AvenueKerb;
                    roads.Add(new PlanBox(c - LayoutRules.StreetKerbWest, from, c + LayoutRules.StreetKerbEast, LayoutRules.AvenueZ(row) - LayoutRules.AvenueKerb, UptownLots.GroundY));
                }
            }
            for (int row = 1; row <= Rows; row++)
            {
                float a = LayoutRules.AvenueZ(row);
                roads.Add(new PlanBox(West, a - LayoutRules.AvenueKerb, East, a + LayoutRules.AvenueKerb, UptownLots.GroundY));
            }
            roads.Add(new PlanBox(West, first - LayoutRules.AvenueKerb, WestCornerJoin, first + LayoutRules.AvenueKerb, UptownLots.GroundY));
            roads.Add(new PlanBox(EastCornerJoin, first - LayoutRules.AvenueKerb, East, first + LayoutRules.AvenueKerb, UptownLots.GroundY));
        }

        /// <summary>
        /// Where the backdrop's pieces are put away (a piece whose middle lies in one): each old dead end with its dressing,
        /// 1st Avenue's two ends with theirs, the first block's main area behind 1st Avenue's buildings, the band west of 9th
        /// and east of 1st Street, the rows north of Uptown Avenue, and the scenery's ground past the walls. Its block grounds
        /// are not among them: each is put away by the ground laid over its block (GroundGame.CopyArt), which stands on the
        /// grid's own place. 1st Avenue's own buildings and corners are no piece of the backdrop's.
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
            float x0 = LayoutRules.StreetX(0) - LayoutRules.StreetHalf, x1 = LayoutRules.StreetX(LayoutRules.FirstColumns) + LayoutRules.StreetHalf;
            into.Add(new GroundRect(x0, AreaFrom, x1, avenue + LayoutRules.AvenueHalf + 0.5f));
            float far = North + HideBeyond, west = West - HideBeyond, east = East + HideBeyond;
            into.Add(new GroundRect(west, RowOneFrom, x0, far));
            into.Add(new GroundRect(x1, RowOneFrom, east, far));
            into.Add(new GroundRect(west, South, WestCornerJoin, RowOneFrom));
            into.Add(new GroundRect(EastCornerJoin, South, east, RowOneFrom));
            into.Add(new GroundRect(x0, avenue + LayoutRules.AvenueHalf + 0.5f, x1, far));
        }

        /// <summary>The city map's bounds the district needs: its ground and <see cref="MapMargin"/> round it, west, east and north.</summary>
        public static void MapBounds(out float west, out float east, out float north)
        {
            west = West - MapMargin;
            east = East + MapMargin;
            north = North + MapMargin;
        }

        // ---- the people and the buses

        /// <summary>The pedestrians' zones: one along the east pavement of each street between its avenues, one along the south pavement of each avenue between its streets.</summary>
        public static void Zones(List<PlanBox> into)
        {
            into.Clear();
            for (int row = 1; row <= Rows; row++)
            {
                float from = row == 1 ? ModuleClear : BlockSouth(row);
                float middle = (from + BlockNorth(row)) * 0.5f;
                for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
                {
                    float x = (LayoutRules.StreetX(k) + LayoutRules.StreetKerbEast + Outer(k, true)) * 0.5f;   // the middle of the pavement as laid
                    into.Add(new PlanBox(x - ZoneWidth * 0.5f, middle - ZoneLength * 0.5f, x + ZoneWidth * 0.5f, middle + ZoneLength * 0.5f, GroundTop));
                }
                float a = LayoutRules.AvenueZ(row);
                float z = a - (LayoutRules.AvenueHalf + LayoutRules.AvenueKerb) * 0.5f;
                for (int k = LayoutStreets.WestMost; k < LayoutStreets.EastMost; k++)
                {
                    float x = LayoutRules.StreetX(k) + LayoutRules.StreetPitch * 0.5f;
                    into.Add(new PlanBox(x - ZoneLength * 0.5f, z - ZoneWidth * 0.5f, x + ZoneLength * 0.5f, z + ZoneWidth * 0.5f, GroundTop));
                }
            }
        }

        /// <summary>
        /// The bus termini, each on its avenue's south pavement between two streets: Uptown Avenue's in the middle of the
        /// first block (between 5th and 4th Street), Summit Avenue's in the west of the band (between Maple and Cedar
        /// Street) and Crest Avenue's in the east (between Ridge and Grove Street), so lines reach all of it.
        /// </summary>
        public static void Termini(List<PlanTerminus> into)
        {
            into.Clear();
            into.Add(Terminus(4, 1, GroundText.Terminus, "Uptown"));
            into.Add(Terminus(-2, 2, LayoutStreets.Avenue(2).NameKey, "Summit Avenue"));
            into.Add(Terminus(10, 3, LayoutStreets.Avenue(3).NameKey, "Crest Avenue"));
        }

        private static PlanTerminus Terminus(int westStreet, int row, string nameKey, string name)
        {
            return new PlanTerminus(LayoutRules.StreetX(westStreet) + LayoutRules.StreetPitch * 0.5f, LayoutRules.AvenueZ(row) - TerminusInset, nameKey, name);
        }

        // ---- what the load lays, counted, for the census

        public const int StreetParking = 1;   // parking lanes in the game's street module (along its west kerb), Build 3682
        public const int AvenueParking = 2;   // ... and in its avenue module (along both kerbs)

        /// <summary>The band's streets west to east.</summary>
        public static int StreetCount => StreetsN;

        /// <summary>The pieces copied, of a kind: among the scenery past the walls or not.</summary>
        public static int ModuleCount(ModuleKind kind, bool scenery)
        {
            var places = new List<ModulePlace>(512);
            Modules(places);
            int count = 0;
            for (int i = 0; i < places.Count; i++)
            {
                count += places[i].Kind == kind && places[i].Scenery == scenery ? 1 : 0;
            }
            return count;
        }

        /// <summary>The game's walls that open: across the first block's street ends and 1st Avenue's two ends.</summary>
        public static int OldWallCount => LayoutRules.FirstColumns + 1 + 2;

        /// <summary>The pedestrians' zones along the pavements: one along each street between its avenues, one along each length of avenue.</summary>
        public static int ZoneCount => Rows * StreetsN + Rows * (StreetsN - 1);

        /// <summary>The parks' zones, appended to the list after the pavements' (into the list as it is): two along each park's spine.</summary>
        public static void ParkZones(List<PlanBox> into)
        {
            var blocks = new List<int>(4);
            ParksRules.Blocks(blocks);
            for (int b = 0; b < blocks.Count; b++)
            {
                ParksRules.Zones(blocks[b], into);
            }
        }

        /// <summary>How many zones the parks have.</summary>
        public static int ParkZoneCount
        {
            get
            {
                var blocks = new List<int>(4);
                ParksRules.Blocks(blocks);
                return blocks.Count * ParksRules.ZonesPerPark;
            }
        }

        /// <summary>The parking lanes along the copied streets and avenues of the band, never the scenery's.</summary>
        public static int ParkingCount => ModuleCount(ModuleKind.Street, false) * StreetParking + ModuleCount(ModuleKind.Avenue, false) * AvenueParking;
    }
}
