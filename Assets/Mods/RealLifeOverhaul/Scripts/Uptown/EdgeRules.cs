using System;
using System.Collections.Generic;
using RealLifeOverhaul.NewGround;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>How the outline of the districts turns at one end of an edge: no edge beyond it, a corner that points out, or one that points in.</summary>
    public enum EdgeCorner
    {
        Free = 0,     // the outline goes on as the city's own side (no fog there)
        Convex = 1,   // the districts' corner points out: the layers of fog run on past it to meet the next edge's
        Reflex = 2    // the corner points in: the layers stop short of it, where they meet the next edge's
    }

    /// <summary>
    /// One stretch of the districts' edge that has fog behind it: the line of an invisible wall's inner face, seen from
    /// inside, and the way out of the districts from it. The edges follow the outline clockwise from the south-west.
    /// </summary>
    public struct EdgeSeg
    {
        public string Name;      // for the log
        public float X0;         // m: the edge's start
        public float Z0;
        public float X1;         // m: and its end
        public float Z1;
        public float OutX;       // the way out of the districts, a unit vector
        public float OutZ;
        public EdgeCorner Start;
        public EdgeCorner End;

        /// <summary>The edge's length (m).</summary>
        public float Length => (float)Math.Sqrt((X1 - X0) * (X1 - X0) + (Z1 - Z0) * (Z1 - Z0));
    }

    /// <summary>The kinds of piece a barricade is dressed with, each one of the mod's existing props.</summary>
    public enum PieceKind
    {
        Hoarding = 0,   // site_hoarding: a 2.4 m panel of construction fencing, DANGER KEEP OUT
        Sign = 1,       // road_sign_works: the orange roadworks board on its stand
        Cone = 2        // cone: a traffic cone
    }

    /// <summary>One piece of a barricade: what, where its foot stands, which way its face looks and how much it is stretched along its width.</summary>
    public struct PiecePlace
    {
        public PieceKind Kind;
        public float X;
        public float Z;
        public float Yaw;       // degrees: the rotation round y that turns the prop's +Z face to look into the districts
        public float Stretch;   // the scale along the prop's width, so a row of panels fits the street whole

        public PiecePlace(PieceKind kind, float x, float z, float yaw, float stretch)
        {
            Kind = kind;
            X = x;
            Z = z;
            Yaw = yaw;
            Stretch = stretch;
        }
    }

    /// <summary>
    /// A closed street end: where a street or an avenue of the districts runs into the edge, the box that stops a car and a
    /// person there (the walls' own layer, the same as the invisible walls round it), and the way out.
    /// </summary>
    public struct Barricade
    {
        public string Name;       // for the log, "street 3 north" or "avenue 2 west"
        public bool Avenue;       // an avenue's west or east end; false: a street's north end
        public int Index;         // the street's place west to east, or the avenue's row (1st Avenue 0)
        public float Cx;          // m: the middle of the line across the end, on the band's edge
        public float Cz;
        public float OutX;        // the way out of the districts, a unit vector
        public float OutZ;
        public float Half;        // m: half the way across, pavement to pavement
        public PlanBox Wall;      // the box that closes it, its top the wall's height
    }

    /// <summary>One way out of a junction no lane takes, that runs into the edge within reach: a street end the mod must close.</summary>
    public struct OpenArm
    {
        public float X;           // m: the junction's middle
        public float Z;
        public float DirX;        // the way the arm leads, a unit vector
        public float DirZ;
        public float Reach;       // m from the junction to the edge it runs into
    }

    /// <summary>
    /// The fog the edges have and the barricades they are closed with (docs/features/uptown.md, "The edges"), game-free. The
    /// districts' outline is the inner faces of the invisible walls round them (<see cref="LayoutPlans.NewWalls"/>); behind
    /// every stretch of it stands a bank of fog: four layers of the game's own border fog stood up as curtains 0, 4, 8 and 12
    /// m past the wall's inner face, each a little thicker than the one before and the last whole, so it fades in over 12
    /// m, and a lid of the same fog at the height of the game's own sheet over the ground beyond, whole from 12 m to 100 m
    /// and gone by 170 m, where the game's own fog is whole, so the map and the camera looking down see the same.
    /// Every street and avenue the plan's lanes do not take across the edge ends in a barricade just short of the wall: a row
    /// of construction fencing across its whole width, roadworks signs and cones before it, and a box (the walls' layer)
    /// that stops a car and a person. Nothing here is drawn or built; the game's side does what it says. No Unity or game
    /// types. COST: made once a load; nothing per frame.
    /// </summary>
    public static class EdgeRules
    {
        // ---- the fog

        public const float CurtainFloor = -2f;          // m: the curtains start under the ground, so no gap shows at its foot
        public const float Height = 46f;                // m: the curtains' top and the lid, the height of the game's own fog sheet
        public const float WholeU = 0.12f;              // uv0.x from which the game's fog shader (alpha = smoothstep(uv0.x * 500 * (1 + noise) / 60)) is whole, whatever its noise
        public const float NoiseMost = 0.863f;          // the game's fog noise texture's greatest red, linear
        public const float NoiseMean = 0.346f;          // ... and its mean
        public const float Threshold = 500f;            // the shader's _Threshold
        public const float Smooth = 60f;                // ... and its _EdgeSmooth
        public const float LidFull = 12f;               // m past the edge the lid is whole, as the last curtain
        public const float LidHold = 100f;              // m past the edge the lid starts to give way to the game's own fog
        public const float LidReach = 170f;             // m past the edge the lid ends: the game's own fog is whole from its hole's edge 136 m out, the hole 21 m past the edge or more
        public const float Seen = 300f;                 // m: the street camera's far plane, the farthest the fog is drawn from

        /// <summary>Metres past the wall's inner face of each layer of curtain.</summary>
        public static readonly float[] CurtainOffset = { 0f, 4f, 8f, 12f };

        /// <summary>uv0.x of each layer: about a tenth, a fifth, two fifths of the fog's strength, and the last whole.</summary>
        public static readonly float[] CurtainU = { 0.018f, 0.027f, 0.037f, WholeU };

        private static readonly float[] LidRow = { 0f, LidFull, LidHold, LidReach };
        private static readonly float[] LidU = { 0f, WholeU, WholeU, 0f };

        // ---- the barricades

        public const float WallTop = 3f;                // m: the barricade's box, over the 2.4 m of fencing
        public const float WallGap = 0.1f;              // m: the box starts this far past the band's edge, clear of the walking ground that ends at it
        public const float Depth = 3.3f;                // m: the box's depth, the fencing at its back
        public const float HoardingAt = 3.0f;           // m past the band's edge the fencing stands
        public const float SignAt = 1.3f;               // m: the roadworks signs
        public const float ConeAt = 0.8f;               // m: the cones
        public const float PanelWidth = 2.4f;           // m: one panel of fencing, the prop's own
        public const float ConeStreet = 4f;             // m each side of a street's middle a cone stands
        public const float Reach = 60f;                 // m a way out of a junction is followed for the edge it runs into

        /// <summary>The props the barricades use, each in the mod's bundles already (reallife_props): fencing, signs, cones.</summary>
        public static readonly string[] PropIds = { "site_hoarding", "road_sign_works", "cone" };

        // ---- the edges

        /// <summary>
        /// The stretches of edge with fog behind them, clockwise round the outline from the south-west: 1st Avenue West's end, the
        /// ring's south end west of it, the west edge, the north, the east, and the same on the east. Along 1st Avenue's
        /// continuations' south side stand the city's own backdrop and the invisible walls: no edge of the districts, no fog.
        /// </summary>
        public static void Edges(List<EdgeSeg> into)
        {
            into.Clear();
            float west = LayoutPlans.West, east = LayoutPlans.East, ringWest = LayoutPlans.RingWest, ringEast = LayoutPlans.RingEast;
            float south = LayoutPlans.SouthWall, first = LayoutPlans.RowOneFrom, north = LayoutPlans.RingNorth;
            into.Add(Edge("1st Avenue West's end", west, south, west, first, -1f, 0f, EdgeCorner.Free, EdgeCorner.Reflex));
            into.Add(Edge("row 1's south end, west", west, first, ringWest, first, 0f, -1f, EdgeCorner.Reflex, EdgeCorner.Convex));
            into.Add(Edge("the west edge", ringWest, first, ringWest, north, -1f, 0f, EdgeCorner.Convex, EdgeCorner.Convex));
            into.Add(Edge("the north edge", ringWest, north, ringEast, north, 0f, 1f, EdgeCorner.Convex, EdgeCorner.Convex));
            into.Add(Edge("the east edge", ringEast, north, ringEast, first, 1f, 0f, EdgeCorner.Convex, EdgeCorner.Convex));
            into.Add(Edge("row 1's south end, east", ringEast, first, east, first, 0f, -1f, EdgeCorner.Convex, EdgeCorner.Reflex));
            into.Add(Edge("1st Avenue East's end", east, first, east, south, 1f, 0f, EdgeCorner.Reflex, EdgeCorner.Free));
        }

        private static EdgeSeg Edge(string name, float x0, float z0, float x1, float z1, float outX, float outZ, EdgeCorner start, EdgeCorner end)
        {
            return new EdgeSeg { Name = name, X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, OutX = outX, OutZ = outZ, Start = start, End = end };
        }

        /// <summary>The fog's strength (0 to 1) of a layer of the given uv0.x at a noise value, as the game's shader makes it.</summary>
        public static double Alpha(double u, double noise)
        {
            double t = Math.Max(0.0, Math.Min(1.0, u * Threshold * (1.0 + noise) / Smooth));
            return Math.Min(1.0, t * t * (3.0 - 2.0 * t));
        }

        /// <summary>The lid's uv0.x at a distance (m) from the edge: 0 on the edge, whole from <see cref="LidFull"/> to <see cref="LidHold"/>, 0 at <see cref="LidReach"/>.</summary>
        public static float LidAt(float distance)
        {
            if (distance <= 0f)
            {
                return LidU[0];
            }
            for (int i = 1; i < LidRow.Length; i++)
            {
                if (distance <= LidRow[i])
                {
                    float f = (distance - LidRow[i - 1]) / (LidRow[i] - LidRow[i - 1]);
                    return LidU[i - 1] + (LidU[i] - LidU[i - 1]) * f;
                }
            }
            return LidU[LidU.Length - 1];
        }

        /// <summary>The fog's vertices and triangles for a list of edges: a curtain for each layer of each edge, a lid strip along it, a lid corner at each convex corner of the outline.</summary>
        public static EdgeFogData Fog(List<EdgeSeg> edges)
        {
            var fog = new EdgeFogData();
            for (int e = 0; e < edges.Count; e++)
            {
                EdgeSeg seg = edges[e];
                float length = seg.Length, dx = (seg.X1 - seg.X0) / length, dz = (seg.Z1 - seg.Z0) / length;
                for (int layer = 0; layer < CurtainOffset.Length; layer++)
                {
                    float o = CurtainOffset[layer];
                    float back = Past(seg.Start, o), ahead = Past(seg.End, o);
                    float ax = seg.X0 + seg.OutX * o - dx * back, az = seg.Z0 + seg.OutZ * o - dz * back;
                    float bx = seg.X1 + seg.OutX * o + dx * ahead, bz = seg.Z1 + seg.OutZ * o + dz * ahead;
                    fog.Curtain(ax, az, bx, bz, CurtainU[layer], -seg.OutX, -seg.OutZ);
                }
                fog.Strip(seg);
                if (seg.End == EdgeCorner.Convex)
                {
                    EdgeSeg next = edges[(e + 1) % edges.Count];
                    if (next.Start == EdgeCorner.Convex)
                    {
                        fog.Corner(seg.X1, seg.Z1, seg.OutX, seg.OutZ, next.OutX, next.OutZ);
                    }
                }
                fog.Segments++;
            }
            return fog;
        }

        // How far a layer of curtain, o metres out, runs past an edge's end: on past a corner that points out, short of one that points in.
        private static float Past(EdgeCorner corner, float o)
        {
            return corner == EdgeCorner.Convex ? o : corner == EdgeCorner.Reflex ? -o : 0f;
        }

        // ---- the barricades

        /// <summary>
        /// Every street end the districts close: each of the nineteen streets' north end past Crest Avenue, and each avenue's
        /// west and east end (1st Avenue's and Uptown's, Summit's and Crest's).
        /// </summary>
        public static void Ends(List<Barricade> into)
        {
            into.Clear();
            float north = LayoutPlans.North;
            for (int k = LayoutStreets.WestMost; k <= LayoutStreets.EastMost; k++)
            {
                float c = LayoutRules.StreetX(k), half = LayoutRules.StreetHalf, z0 = north + WallGap;
                into.Add(new Barricade
                {
                    Name = "street " + k + " north", Avenue = false, Index = k, Cx = c, Cz = north, OutX = 0f, OutZ = 1f, Half = half,
                    Wall = new PlanBox(c - half, z0, c + half, z0 + Depth, WallTop)
                });
            }
            for (int row = 0; row <= LayoutRules.Rows; row++)
            {
                float a = LayoutRules.AvenueZ(row), half = LayoutRules.AvenueHalf;
                float west = LayoutPlans.West, east = LayoutPlans.East;
                into.Add(new Barricade
                {
                    Name = "avenue " + row + " west", Avenue = true, Index = row, Cx = west, Cz = a, OutX = -1f, OutZ = 0f, Half = half,
                    Wall = new PlanBox(west - WallGap - Depth, a - half, west - WallGap, a + half, WallTop)
                });
                into.Add(new Barricade
                {
                    Name = "avenue " + row + " east", Avenue = true, Index = row, Cx = east, Cz = a, OutX = 1f, OutZ = 0f, Half = half,
                    Wall = new PlanBox(east + WallGap, a - half, east + WallGap + Depth, a + half, WallTop)
                });
            }
        }

        /// <summary>The boxes of the barricades, for the walls' layer.</summary>
        public static void Walls(List<Barricade> ends, List<PlanBox> into)
        {
            into.Clear();
            for (int i = 0; i < ends.Count; i++)
            {
                into.Add(ends[i].Wall);
            }
        }

        /// <summary>The pieces a barricade is dressed with: a row of fencing across its width, signs and cones before it, all facing into the districts.</summary>
        public static void Pieces(Barricade end, List<PiecePlace> into)
        {
            into.Clear();
            float span = end.Half * 2f, ax = end.OutZ, az = -end.OutX;   // along the end, a unit vector
            float yaw = (float)(Math.Atan2(-end.OutX, -end.OutZ) * 180.0 / Math.PI);
            int panels = Math.Max(1, (int)Math.Round(span / PanelWidth));
            float pitch = span / panels;
            for (int i = 0; i < panels; i++)
            {
                Put(into, PieceKind.Hoarding, end, ax, az, -span * 0.5f + pitch * (i + 0.5f), HoardingAt, yaw, pitch / PanelWidth);
            }
            if (end.Avenue)
            {
                Put(into, PieceKind.Sign, end, ax, az, -span * 0.25f, SignAt, yaw, 1f);
                Put(into, PieceKind.Sign, end, ax, az, span * 0.25f, SignAt, yaw, 1f);
                Put(into, PieceKind.Cone, end, ax, az, -9f, ConeAt, yaw, 1f);
                Put(into, PieceKind.Cone, end, ax, az, -3f, ConeAt, yaw, 1f);
                Put(into, PieceKind.Cone, end, ax, az, 3f, ConeAt, yaw, 1f);
                Put(into, PieceKind.Cone, end, ax, az, 9f, ConeAt, yaw, 1f);
            }
            else
            {
                Put(into, PieceKind.Sign, end, ax, az, 0f, SignAt, yaw, 1f);
                Put(into, PieceKind.Cone, end, ax, az, -ConeStreet, ConeAt, yaw, 1f);
                Put(into, PieceKind.Cone, end, ax, az, ConeStreet, ConeAt, yaw, 1f);
            }
        }

        private static void Put(List<PiecePlace> into, PieceKind kind, Barricade end, float ax, float az, float across, float past, float yaw, float stretch)
        {
            into.Add(new PiecePlace(kind, end.Cx + ax * across + end.OutX * past, end.Cz + az * across + end.OutZ * past, yaw, stretch));
        }

        /// <summary>The prop a piece is.</summary>
        public static string PropOf(PieceKind kind)
        {
            return PropIds[(int)kind];
        }

        /// <summary>How many pieces all the barricades have.</summary>
        public static int PieceCount(List<Barricade> ends)
        {
            var pieces = new List<PiecePlace>(24);
            int count = 0;
            for (int i = 0; i < ends.Count; i++)
            {
                Pieces(ends[i], pieces);
                count += pieces.Count;
            }
            return count;
        }

        // ---- what the plan's lanes leave open

        /// <summary>
        /// The ways out of the plan's junctions that no lane takes and that run into an edge within <see cref="Reach"/>: each is a
        /// street end that needs a barricade. Found from the lanes alone, not from the ends' list, so a list that misses one is caught.
        /// </summary>
        public static void OpenArms(RoadPlan plan, List<EdgeSeg> edges, List<OpenArm> into)
        {
            into.Clear();
            for (int j = 0; j < plan.Junctions.Count; j++)
            {
                PlanJunction junction = plan.Junctions[j];
                bool north = false, south = false, east = false, west = false;
                for (int r = 0; r < plan.Roads.Count; r++)
                {
                    PlanRoad road = plan.Roads[r];
                    int n = road.X.Count;
                    if (n < 2)
                    {
                        continue;
                    }
                    if (road.Start == j)
                    {
                        Mark(road.X[1] - road.X[0], road.Z[1] - road.Z[0], ref north, ref south, ref east, ref west);
                    }
                    if (road.End == j)
                    {
                        Mark(road.X[n - 2] - road.X[n - 1], road.Z[n - 2] - road.Z[n - 1], ref north, ref south, ref east, ref west);
                    }
                }
                Try(junction, 0f, 1f, north, edges, into);
                Try(junction, 0f, -1f, south, edges, into);
                Try(junction, 1f, 0f, east, edges, into);
                Try(junction, -1f, 0f, west, edges, into);
            }
        }

        private static void Mark(float dx, float dz, ref bool north, ref bool south, ref bool east, ref bool west)
        {
            if (Math.Abs(dx) > Math.Abs(dz))
            {
                east |= dx > 0f;
                west |= dx < 0f;
            }
            else
            {
                north |= dz > 0f;
                south |= dz < 0f;
            }
        }

        private static void Try(PlanJunction junction, float dx, float dz, bool taken, List<EdgeSeg> edges, List<OpenArm> into)
        {
            if (taken)
            {
                return;
            }
            float reach = Reached(junction.X, junction.Z, dx, dz, edges);
            if (reach >= 0f && reach <= Reach)
            {
                into.Add(new OpenArm { X = junction.X, Z = junction.Z, DirX = dx, DirZ = dz, Reach = reach });
            }
        }

        // The metres from a point along an axis-aligned way to the first edge it crosses (an edge faces the way's opposite), or -1.
        private static float Reached(float x, float z, float dx, float dz, List<EdgeSeg> edges)
        {
            float best = -1f;
            for (int e = 0; e < edges.Count; e++)
            {
                EdgeSeg s = edges[e];
                if (s.OutX * dx + s.OutZ * dz <= 0f)
                {
                    continue;   // a way out of the districts reaches an edge whose outside it leads to
                }
                float t, along;
                if (Math.Abs(dx) > 0.5f && Math.Abs(s.OutX) > 0.5f)
                {
                    t = (s.X0 - x) * dx;
                    along = z;
                    if (t < 0f || along < Math.Min(s.Z0, s.Z1) || along > Math.Max(s.Z0, s.Z1))
                    {
                        continue;
                    }
                }
                else if (Math.Abs(dz) > 0.5f && Math.Abs(s.OutZ) > 0.5f)
                {
                    t = (s.Z0 - z) * dz;
                    along = x;
                    if (t < 0f || along < Math.Min(s.X0, s.X1) || along > Math.Max(s.X0, s.X1))
                    {
                        continue;
                    }
                }
                else
                {
                    continue;
                }
                best = best < 0f ? t : Math.Min(best, t);
            }
            return best;
        }

        // ---- what crosses a barricade

        /// <summary>True when a lane's middle line (a road of the plan) comes within its own width and half a metre of the barricade's box.</summary>
        public static bool LaneTouches(PlanRoad road, Barricade end)
        {
            float margin = (road.Ahead + road.Back) * road.LaneWidth * 0.5f + 0.5f;
            float x0 = end.Wall.X - end.Wall.HalfX - margin, x1 = end.Wall.X + end.Wall.HalfX + margin;
            float z0 = end.Wall.Z - end.Wall.HalfZ - margin, z1 = end.Wall.Z + end.Wall.HalfZ + margin;
            for (int i = 0; i + 1 < road.X.Count; i++)
            {
                if (SegmentMeets(road.X[i], road.Z[i], road.X[i + 1], road.Z[i + 1], x0, z0, x1, z1))
                {
                    return true;
                }
            }
            return road.X.Count == 1 && road.X[0] >= x0 && road.X[0] <= x1 && road.Z[0] >= z0 && road.Z[0] <= z1;
        }

        /// <summary>True when a segment meets a rectangle (Liang and Barsky).</summary>
        public static bool SegmentMeets(float ax, float az, float bx, float bz, float x0, float z0, float x1, float z1)
        {
            float t0 = 0f, t1 = 1f, dx = bx - ax, dz = bz - az;
            return Clip(-dx, ax - x0, ref t0, ref t1) && Clip(dx, x1 - ax, ref t0, ref t1) && Clip(-dz, az - z0, ref t0, ref t1) && Clip(dz, z1 - az, ref t0, ref t1);
        }

        private static bool Clip(float p, float q, ref float t0, ref float t1)
        {
            if (Math.Abs(p) < 1e-9f)
            {
                return q >= 0f;
            }
            float r = q / p;
            if (p < 0f)
            {
                if (r > t1)
                {
                    return false;
                }
                t0 = Math.Max(t0, r);
            }
            else
            {
                if (r < t0)
                {
                    return false;
                }
                t1 = Math.Min(t1, r);
            }
            return true;
        }

        /// <summary>True when two boxes on the ground share any area (touching edges do not count).</summary>
        public static bool Overlap(PlanBox a, float bx, float bz, float bHalfX, float bHalfZ)
        {
            return Math.Abs(a.X - bx) < a.HalfX + bHalfX - 1e-4f && Math.Abs(a.Z - bz) < a.HalfZ + bHalfZ - 1e-4f;
        }
    }

    /// <summary>
    /// The fog's mesh as arrays: positions, normals and the one number its shader reads (uv0.x), and the triangles, wound so the
    /// front faces look into the districts (a curtain) or up (the lid). Game-free.
    /// </summary>
    public sealed class EdgeFogData
    {
        public readonly List<float> X = new List<float>(256);
        public readonly List<float> Y = new List<float>(256);
        public readonly List<float> Z = new List<float>(256);
        public readonly List<float> NormalX = new List<float>(256);
        public readonly List<float> NormalY = new List<float>(256);
        public readonly List<float> NormalZ = new List<float>(256);
        public readonly List<float> U = new List<float>(256);
        public readonly List<int> Triangles = new List<int>(768);
        public int Segments;     // edges with fog
        public int Curtains;     // vertical layers
        public int Lids;         // strips and corners of the lid

        /// <summary>The vertices.</summary>
        public int Vertices => X.Count;

        // A vertical layer from a to b, floor to top, its front looking along (nx, nz).
        internal void Curtain(float ax, float az, float bx, float bz, float u, float nx, float nz)
        {
            int first = Vertices;
            Vertex(ax, EdgeRules.CurtainFloor, az, nx, 0f, nz, u);
            Vertex(bx, EdgeRules.CurtainFloor, bz, nx, 0f, nz, u);
            Vertex(bx, EdgeRules.Height, bz, nx, 0f, nz, u);
            Vertex(ax, EdgeRules.Height, az, nx, 0f, nz, u);
            Quad(first, first + 1, first + 2, first + 3, nx, 0f, nz);
            Curtains++;
        }

        // The lid along an edge: rows out from it, each with the uv0.x for its distance.
        internal void Strip(EdgeSeg seg)
        {
            int rows = 4;
            float[] distance = { 0f, EdgeRules.LidFull, EdgeRules.LidHold, EdgeRules.LidReach };
            int first = Vertices;
            for (int r = 0; r < rows; r++)
            {
                float u = EdgeRules.LidAt(distance[r]);
                Vertex(seg.X0 + seg.OutX * distance[r], EdgeRules.Height, seg.Z0 + seg.OutZ * distance[r], 0f, 1f, 0f, u);
                Vertex(seg.X1 + seg.OutX * distance[r], EdgeRules.Height, seg.Z1 + seg.OutZ * distance[r], 0f, 1f, 0f, u);
            }
            for (int r = 0; r + 1 < rows; r++)
            {
                Quad(first + r * 2, first + r * 2 + 1, first + r * 2 + 3, first + r * 2 + 2, 0f, 1f, 0f);
                Lids++;
            }
        }

        // The lid at a corner that points out: a grid out from the corner both ways, the uv0.x by the distance from the corner itself.
        internal void Corner(float cx, float cz, float ax, float az, float bx, float bz)
        {
            float[] distance = { 0f, EdgeRules.LidFull, EdgeRules.LidHold, EdgeRules.LidReach };
            int n = distance.Length, first = Vertices;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float x = cx + ax * distance[i] + bx * distance[j], z = cz + az * distance[i] + bz * distance[j];
                    float reach = (float)Math.Sqrt(distance[i] * distance[i] + distance[j] * distance[j]);
                    Vertex(x, EdgeRules.Height, z, 0f, 1f, 0f, EdgeRules.LidAt(reach));
                }
            }
            for (int i = 0; i + 1 < n; i++)
            {
                for (int j = 0; j + 1 < n; j++)
                {
                    Quad(first + i * n + j, first + i * n + j + 1, first + (i + 1) * n + j + 1, first + (i + 1) * n + j, 0f, 1f, 0f);
                    Lids++;
                }
            }
        }

        private void Vertex(float x, float y, float z, float nx, float ny, float nz, float u)
        {
            X.Add(x);
            Y.Add(y);
            Z.Add(z);
            NormalX.Add(nx);
            NormalY.Add(ny);
            NormalZ.Add(nz);
            U.Add(u);
        }

        // Two triangles over a quad's four vertices, wound so the front faces the given way (Unity's front is the side the cross product of the first two edges points to).
        private void Quad(int a, int b, int c, int d, float nx, float ny, float nz)
        {
            float ux = X[b] - X[a], uy = Y[b] - Y[a], uz = Z[b] - Z[a];
            float vx = X[c] - X[a], vy = Y[c] - Y[a], vz = Z[c] - Z[a];
            float cross = (uy * vz - uz * vy) * nx + (uz * vx - ux * vz) * ny + (ux * vy - uy * vx) * nz;
            if (cross >= 0f)
            {
                Triangles.Add(a);
                Triangles.Add(b);
                Triangles.Add(c);
                Triangles.Add(a);
                Triangles.Add(c);
                Triangles.Add(d);
            }
            else
            {
                Triangles.Add(a);
                Triangles.Add(c);
                Triangles.Add(b);
                Triangles.Add(a);
                Triangles.Add(d);
                Triangles.Add(c);
            }
        }
    }
}
