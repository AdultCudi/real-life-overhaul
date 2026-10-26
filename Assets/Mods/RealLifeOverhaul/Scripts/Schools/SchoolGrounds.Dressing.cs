using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Schools
{
    /// <summary>A box on the ground, game-free: its middle and half its size each way (m).</summary>
    public struct CampusBox
    {
        public float X, Z, HalfX, HalfZ;

        public CampusBox(float x0, float z0, float x1, float z1, float top = 0f)
        {
            X = (x0 + x1) * 0.5f;
            Z = (z0 + z1) * 0.5f;
            HalfX = Math.Abs(x1 - x0) * 0.5f;
            HalfZ = Math.Abs(z1 - z0) * 0.5f;
            if (top < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(top));
            }
        }
    }

    /// <summary>When a square of the campus has people in it: the school day's moments (<see cref="SchoolRules.CrowdMoments"/>).</summary>
    [Flags]
    public enum CrowdWhen
    {
        None = 0,
        Run = 1,       // the school run's hours, morning and afternoon: families and pupils arriving and leaving
        Break = 2,     // the morning break
        Lunch = 4,     // the lunch hour
        After = 8,     // the afternoon's clubs and games, after classes
        OpenDay = 16   // a term's open day
    }

    /// <summary>
    /// The campus dressed (docs/features/schools.md, "The dressing"): the game's own pieces laid on the lawns and the paths between the gates and the
    /// buildings, the court with its two hoops and its fence, the squares of ground where people gather and when. Game-free, fixed figures from the
    /// block's own edges, so the campus stands the same on every machine (EconomyCheck's SchoolChecks). COST: lists filled once a load.
    /// </summary>
    public static partial class SchoolGrounds
    {
        public const float PathWide = 3f;         // m: a spine's width
        public const float WalkWide = 1.6f;       // m: the walk along the main school's front, and a yard's path (2.6)
        public const float FieldMargin = 5f;      // m the playing field keeps from the fence at least
        public const float PieceMargin = 0.6f;    // m a lawn piece keeps from the fence's line at least

        // Where a bench faces: its front towards +X is a yaw of 90 (the way the court's benches faced before).
        private const float East = 90f, West = 270f, North = 0f, South = 180f;

        /// <summary>The playing field's middle, from the block's south edge: its north end <see cref="PitchNorth"/> in from the north edge.</summary>
        public static float PitchMiddleV => HeightOfBlock - PitchNorth - PitchDepth * 0.5f;

        private static float HeightOfBlock
        {
            get
            {
                Ground(out _, out float z0, out _, out float z1);
                return z1 - z0;
            }
        }

        /// <summary>The courtyard's middle x from the block's west edge, and the x of the campus's middle.</summary>
        private static void Frame(out float x0, out float z0, out float courtX, out float mid)
        {
            Ground(out x0, out z0, out float x1, out _);
            Courtyard(out courtX, out _);
            mid = (x0 + x1) * 0.5f;
        }

        /// <summary>
        /// Every piece stood on the campus (into the list, cleared; the fence's carving runs into <paramref name="runs"/>): the fence round the block in modules,
        /// broken by the main gate, the side gate and the field's gate, a post at each end of a run; the court as two half courts back to back with their hoops
        /// and the fence round them; the playing field; the lawns dressed with trees, benches, table groups, bins, climbing frames and planters.
        /// The main school's door is at <paramref name="doorZ"/> (NaN: where the copy engine's door stands on its lot).
        /// </summary>
        public static void Pieces(List<CampusPiece> into, List<CampusFence> runs, float doorZ = float.NaN)
        {
            into.Clear();
            runs.Clear();
            Frame(out float x0, out float z0, out float courtX, out float mid);
            Ground(out _, out _, out float x1, out float z1);
            float s = z0 + FenceIn, n = z1 - FenceIn, w = x0 + FenceIn, e = x1 - FenceIn;
            float sideGate = z0 + SideGateV;
            // South, on Uptown Avenue: the main gate before the main school's door.
            Run(into, runs, w, s, mid - GateWidth * 0.5f, s);
            Run(into, runs, mid + GateWidth * 0.5f, s, e, s);
            // East, on Elm Street: the side gate to the green.
            Run(into, runs, e, s, e, sideGate - SideGate * 0.5f);
            Run(into, runs, e, sideGate + SideGate * 0.5f, e, n);
            // North, on Summit Avenue: the field's gate.
            Run(into, runs, e, n, mid + SideGate * 0.5f, n);
            Run(into, runs, mid - SideGate * 0.5f, n, w, n);
            // West, on Birch Street: whole.
            Run(into, runs, w, n, w, s);
            Court(into, runs, z0, courtX);
            // The playing field at the north end, behind the side gate, before the field's gate, a margin from the fence on every side.
            into.Add(new CampusPiece
            {
                Kind = CampusPieceKind.Pitch, X = mid, Z = z0 + PitchMiddleV, Yaw = 0f, StretchX = PitchScale, StretchZ = PitchScale
            });
            Dress(into, x0, z0, courtX, mid, DoorOf(z0, doorZ));
        }

        /// <summary>
        /// Where the main school's door is on the ground (m, north): as given, or where the plan puts it: the front wall at the lot's front line (<see cref="MainInset"/> in
        /// from the block's edge), the door a bay (<see cref="DoorBay"/>) in front of it.
        /// </summary>
        public static float DoorOf(float z0, float doorZ) => float.IsNaN(doorZ) ? z0 + MainInset - DoorBay : doorZ;

        /// <summary>Where the side gate on Elm Street is, north of the block's south edge (m): between the junior block's north end and the field.</summary>
        public const float SideGateV = MainDepth + 2f + 30f + 3f;

        // The court: two of the game's half courts back to back, each with its hoop 0.6 m (as stretched: 0.94 m) in from its baseline facing in, the
        // court's middle on the line the hoop faces; and the game's playground fence round it, whole on the south baseline and the two long sides and open for
        // CourtGap in the middle of the north baseline, to the plaza, so the court reads as a court, a ball stays on it, and people come in from the plaza.
        private static void Court(List<CampusPiece> into, List<CampusFence> runs, float z0, float courtX)
        {
            float deep = HalfCourtDeep * CourtScale;                                          // a half court from its baseline to the middle line: 7.95 m
            float hoopIn = (HalfCourtDeep * 0.5f - HoopToCentre) * CourtScale;    // 0.6 m at the game's scale, 0.94 m as stretched: the court's half depth less the hoop's distance to its middle
            float southBaseline = z0 + CourtFrom, northBaseline = southBaseline + 2f * deep;
            AddHalf(into, courtX, southBaseline + hoopIn, 0f);
            AddHalf(into, courtX, northBaseline - hoopIn, 180f);
            float half = CourtWidth * 0.5f + 0.2f;
            float fenceS = southBaseline - 0.3f, fenceN = northBaseline + 0.3f;
            Run(into, runs, courtX - half, fenceS, courtX + half, fenceS);
            Run(into, runs, courtX - half, fenceN, courtX - CourtGap * 0.5f, fenceN);    // the north baseline has its opening, to the plaza
            Run(into, runs, courtX + CourtGap * 0.5f, fenceN, courtX + half, fenceN);
            Run(into, runs, courtX - half, fenceS, courtX - half, fenceN);               // the long sides are whole: the strips beside them stay open to walk
            Run(into, runs, courtX + half, fenceS, courtX + half, fenceN);
        }

        // One half court: its hoop on the line the hoop faces (yaw 0 faces north), the court's plane on that line, a half court's depth in front of the
        // baseline and turned the half turn from the hoop (as the game places them: its court at the hoop's yaw less 180 degrees, 1.96 m out).
        private static void AddHalf(List<CampusPiece> into, float x, float hoopZ, float hoopYaw)
        {
            double a = hoopYaw * Math.PI / 180.0;
            float fx = (float)Math.Sin(a), fz = (float)Math.Cos(a);     // the way the hoop faces
            float rx = (float)Math.Cos(a), rz = -(float)Math.Sin(a);    // its right
            float cx = x + (fx * HoopToCentre + rx * HoopSide) * CourtScale, cz = hoopZ + (fz * HoopToCentre + rz * HoopSide) * CourtScale;
            into.Add(new CampusPiece { Kind = CampusPieceKind.Court, X = cx, Z = cz, Yaw = LookTurn(hoopYaw + 180f), StretchX = CourtScale, StretchZ = CourtScale });
            into.Add(new CampusPiece { Kind = CampusPieceKind.Hoop, X = x, Z = hoopZ, Yaw = LookTurn(hoopYaw), StretchX = 1f, StretchZ = 1f });
        }

        private static float LookTurn(float degrees) => CampusLookRules.Turn(degrees);

        // The lawns: pieces put at block-relative places (u across from the west edge, v along from the south edge).
        private static void Dress(List<CampusPiece> into, float x0, float z0, float courtX, float mid, float doorZ)
        {
            float cu = courtX - x0;
            // Planters on the main school's forecourt, 1.2 m in front of its front wall, which is a door's bay behind the door.
            float wallV = doorZ + DoorBay - z0;
            float planterZ = wallV - PlanterBack;
            foreach (float across in new[] { -17.5f, -13.5f, 13.5f, 17.5f })
            {
                Put(into, CampusPieceKind.Bush, x0, z0, (mid - x0) + across, planterZ, 0f);
            }
            // The junior block's door faces Elm Street: two planters 1.0 m in front of its front wall, 5.5 m either side of its door.
            LookModel junior = CampusLookRules.Models[2];
            Ground(out _, out _, out float east, out _);
            float frontU = east - x0 - CampusLookRules.WallFromEdge(junior) + 1.0f;
            float jv = MainDepth + 2f + 15f;   // the middle of the junior block's plot (Blocks: 2 m past the main school's lot and 15 m in)
            Put(into, CampusPieceKind.Bush, x0, z0, frontU, jv - 5.5f, 0f);
            Put(into, CampusPieceKind.Bush, x0, z0, frontU, jv + 5.5f, 0f);
            // The main gate's flanks: a bin each side, inside the fence, off the walk and the gate's way in. No bicycle stands: the game's only one is its bike-share dock,
            // which stood as a bare black slab on the lawn in the owner's screenshots of 26 Oct 2026.
            Put(into, CampusPieceKind.Bin, x0, z0, 27.4f, wallV - 1.15f, 0f);
            Put(into, CampusPieceKind.Bin, x0, z0, 42.6f, wallV - 1.15f, 0f);
            // The two yards beside the main school: a path each (the front walk's), benches facing it, a table group, a climbing frame and trees.
            Yard(into, x0, z0, 1f);
            Yard(into, x0, z0, -1f);
            // The courtyard's strips beside the court's fence stay open to walk (1.5 m between the fence and the buildings).
            // The plaza north of the court: benches facing it, a table group, trees at its corners.
            Put(into, CampusPieceKind.Bench, x0, z0, cu - 4.6f, 57.0f, South);
            Put(into, CampusPieceKind.Bench, x0, z0, cu + 4.6f, 57.0f, South);
            Put(into, CampusPieceKind.Bin, x0, z0, cu - 2.6f, 56.4f, 0f);
            Put(into, CampusPieceKind.Bin, x0, z0, cu + 2.6f, 56.4f, 0f);
            Put(into, CampusPieceKind.Table, x0, z0, cu, 61.4f, 0f);
            Put(into, CampusPieceKind.Oak, x0, z0, cu - 5.4f, 64.6f, 0f);
            Put(into, CampusPieceKind.Maple, x0, z0, cu + 5.4f, 64.6f, 0f);
            // The green north of the junior block and the hall, either side of the path to the field: benches along the cross path facing it, table groups,
            // bins, trees along the fence and the field's sides.
            foreach (float u in new[] { 9f, 18f, 52f, 61f })
            {
                Put(into, CampusPieceKind.Bench, x0, z0, u, 72.4f, South);
            }
            Put(into, CampusPieceKind.Table, x0, z0, 24f, 74.6f, 0f);
            Put(into, CampusPieceKind.Table, x0, z0, 46f, 74.6f, 0f);
            Put(into, CampusPieceKind.Bin, x0, z0, 30.2f, 72.4f, 0f);
            Put(into, CampusPieceKind.Bin, x0, z0, 40.2f, 72.4f, 0f);
            CampusPieceKind[] kinds = { CampusPieceKind.Oak, CampusPieceKind.Birch, CampusPieceKind.Beech, CampusPieceKind.Maple };
            int k = 0;
            foreach (float v in new[] { 76f, 86f, 96f, 106f, 113f })
            {
                Put(into, kinds[k++ % 4], x0, z0, 3.5f, v, 0f);
                Put(into, kinds[k++ % 4], x0, z0, 66.5f, v, 0f);
            }
            foreach (float v in new[] { 88f, 108f })
            {
                Put(into, kinds[k++ % 4], x0, z0, 8.5f, v, 0f);
                Put(into, kinds[k++ % 4], x0, z0, 61.5f, v, 0f);
            }
        }

        // A yard beside the main school: side 1 the west one, -1 the east one (mirrored about the campus's middle line).
        private static void Yard(List<CampusPiece> into, float x0, float z0, float side)
        {
            float u(float west) => side > 0f ? west : 70f - west;
            float toward = side > 0f ? West : East;     // a bench faces the yard's path: the west yard's path is on its west, the east yard's on its east
            Put(into, CampusPieceKind.Bench, x0, z0, u(9.6f), 11f, toward);
            Put(into, CampusPieceKind.Bench, x0, z0, u(9.6f), 23f, toward);
            Put(into, CampusPieceKind.Table, x0, z0, u(12.6f), 17f, 0f);
            Put(into, CampusPieceKind.Climb, x0, z0, u(3.0f), 18f, 0f);
            Put(into, CampusPieceKind.Oak, x0, z0, u(2.6f), 6f, 0f);
            Put(into, CampusPieceKind.Birch, x0, z0, u(2.6f), 30f, 0f);
            Put(into, CampusPieceKind.Beech, x0, z0, u(12.6f), 30.4f, 0f);
            Put(into, CampusPieceKind.Maple, x0, z0, u(12.6f), 6.4f, 0f);
            Put(into, CampusPieceKind.Bin, x0, z0, u(9.6f), 7.2f, 0f);
            Put(into, CampusPieceKind.Bin, x0, z0, u(9.6f), 27.4f, 0f);
        }

        private static void Put(List<CampusPiece> into, CampusPieceKind kind, float x0, float z0, float u, float v, float yaw)
        {
            into.Add(new CampusPiece { Kind = kind, X = x0 + u, Z = z0 + v, Yaw = yaw, StretchX = 1f, StretchZ = 1f });
        }

        // ---------------------------------------------------------------- the paths

        /// <summary>
        /// The paved paths, as rectangles on the ground (the game's own pavement material over them, the parks' way): the main gate to the main school's
        /// door and along its front to the two yards; the side gate and the field's gate to the courtyard's plaza, the court and the field; the green's cross
        /// path to the hall's and the junior block's north ends (their doors face the streets, 0.2 m inside the fence, so no path goes there). The main
        /// school's door is at <paramref name="doorZ"/> (NaN: where the copy engine's door stands on its lot).
        /// </summary>
        public static void Paths(List<CampusBox> into, float doorZ = float.NaN)
        {
            into.Clear();
            Frame(out float x0, out float z0, out float courtX, out float mid);
            Ground(out _, out _, out float x1, out _);
            float door = DoorOf(z0, doorZ) - z0;
            float cu = courtX - x0, m = mid - x0;
            Rect(into, x0, z0, m - PathWide * 0.5f, m + PathWide * 0.5f, 0.1f, Math.Max(1.2f, door - 0.2f));   // gate to the door
            float slab = CampusLookRules.Models[0].HalfWidth;                                                   // the main school's forecourt slab spans its width: the walks end on it
            Rect(into, x0, z0, 3f, m - slab + 0.5f, 0.8f, 0.8f + WalkWide);                                     // along the front, west
            Rect(into, x0, z0, m + slab - 0.5f, 67f, 0.8f, 0.8f + WalkWide);                                    // ... and east
            Rect(into, x0, z0, 6f, 8.6f, 0.8f + WalkWide - 0.1f, 33.6f);                                       // the west yard's path
            Rect(into, x0, z0, 61.4f, 64f, 0.8f + WalkWide - 0.1f, 33.6f);                                     // the east yard's
            Rect(into, x0, z0, cu - 7f, cu + 7.3f, 55.2f, 66.4f);                                              // the plaza north of the court
            Rect(into, x0, z0, m - PathWide * 0.5f, m + PathWide * 0.5f, 66.2f, PitchMiddleV - PitchDepth * 0.5f);   // the plaza to the field
            Rect(into, x0, z0, 3f, x1 - x0, SideGateV - 1.5f, SideGateV + 1.5f);                         // the green's cross path, from the side gate to the hall's end
            Rect(into, x0, z0, m - SideGate * 0.5f, m + SideGate * 0.5f, PitchMiddleV + PitchDepth * 0.5f, HeightOfBlock);   // the field's gate to the field
        }

        private static void Rect(List<CampusBox> into, float x0, float z0, float u0, float u1, float v0, float v1)
        {
            if (u1 > u0 && v1 > v0)
            {
                into.Add(new CampusBox(x0 + u0, z0 + v0, x0 + u1, z0 + v1, 0f));
            }
        }

        // ---------------------------------------------------------------- the buildings and the court, as ground

        /// <summary>The squares of ground people gather in (<see cref="Crowds"/>): how many.</summary>
        public const int CrowdCount = 9;

        /// <summary>A route someone on foot must be able to walk: a name, where it starts and where it ends (m on the ground).</summary>
        public struct CampusRoute
        {
            public string Name;
            public float FromX, FromZ, ToX, ToZ;
        }

        /// <summary>
        /// The routes the campus must offer on foot, from the pavement outside a gate to a place inside (the walking meshes join the campus to the pavements at
        /// the gates): the main gate to the main school's door and to each yard; the side gate to the court, the plaza and the north ends of the hall and the
        /// junior block; the field's gate to the field. Checked offline against the paths and at a load against the pedestrians' walking mesh.
        /// </summary>
        public static void Routes(List<CampusRoute> into, float doorX, float doorZ)
        {
            into.Clear();
            Frame(out float x0, out float z0, out float courtX, out float mid);
            Ground(out _, out _, out float x1, out float z1);
            float main = DoorOf(z0, doorZ);
            float doorAt = float.IsNaN(doorX) ? mid : doorX;
            float mainGateX = mid, mainGateZ = z0 - PavementIn;
            float sideX = x1 + PavementIn, sideZ = z0 + SideGateV;
            float fieldX = mid, fieldZ = z1 + PavementIn;
            into.Add(new CampusRoute { Name = "the main gate to the main school's door", FromX = mainGateX, FromZ = mainGateZ, ToX = doorAt, ToZ = main - 0.5f });
            into.Add(new CampusRoute { Name = "the main gate to the west yard", FromX = mainGateX, FromZ = mainGateZ, ToX = x0 + 7.3f, ToZ = z0 + 20f });
            into.Add(new CampusRoute { Name = "the main gate to the east yard", FromX = mainGateX, FromZ = mainGateZ, ToX = x1 - 7.3f, ToZ = z0 + 20f });
            into.Add(new CampusRoute { Name = "the side gate to the court", FromX = sideX, FromZ = sideZ, ToX = courtX, ToZ = z0 + CourtFrom + CourtLength * 0.5f });
            into.Add(new CampusRoute { Name = "the side gate to the plaza", FromX = sideX, FromZ = sideZ, ToX = courtX, ToZ = z0 + 61f });
            into.Add(new CampusRoute { Name = "the side gate to the hall's north end", FromX = sideX, FromZ = sideZ, ToX = x0 + 14f, ToZ = z0 + SideGateV });
            into.Add(new CampusRoute { Name = "the side gate to the junior block's north end", FromX = sideX, FromZ = sideZ, ToX = x1 - 14f, ToZ = z0 + SideGateV });
            into.Add(new CampusRoute { Name = "the field's gate to the field", FromX = fieldX, FromZ = fieldZ, ToX = mid, ToZ = z0 + PitchMiddleV });
            into.Add(new CampusRoute { Name = "the field's gate to the plaza", FromX = fieldX, FromZ = fieldZ, ToX = courtX, ToZ = z0 + 61f });
        }

        /// <summary>The pieces laid, counted by kind in words for Player.log: "22 trees, 14 benches, ...".</summary>
        public static string Words(List<CampusPiece> pieces)
        {
            int trees = 0, bushes = 0, benches = 0, tables = 0, bins = 0, climbs = 0, hoops = 0, courts = 0, modules = 0;
            for (int i = 0; i < pieces.Count; i++)
            {
                switch (pieces[i].Kind)
                {
                    case CampusPieceKind.Oak: case CampusPieceKind.Birch: case CampusPieceKind.Beech: case CampusPieceKind.Maple: case CampusPieceKind.Tree: trees++; break;
                    case CampusPieceKind.Bush: bushes++; break;
                    case CampusPieceKind.Bench: benches++; break;
                    case CampusPieceKind.Table: tables++; break;
                    case CampusPieceKind.Bin: bins++; break;
                    case CampusPieceKind.Climb: climbs++; break;
                    case CampusPieceKind.Hoop: hoops++; break;
                    case CampusPieceKind.Court: courts++; break;
                    case CampusPieceKind.Fence: modules++; break;
                }
            }
            return trees + " trees, " + bushes + " bushes, " + benches + " benches, " + tables + " table groups, " + bins + " bins, " + climbs + " climbing frames, "
                   + courts + " half courts with " + hoops + " hoops, " + modules + " fence modules";
        }

        /// <summary>
        /// The ground the three buildings' bodies take, behind their front walls (their porches and steps stand in front: what is solid is
        /// CampusLookRules'): the main school from its door, the hall and the junior block from their plots.
        /// </summary>
        public static void Buildings(List<CampusBox> into, float doorZ = float.NaN)
        {
            into.Clear();
            Frame(out float x0, out float z0, out float _, out float mid);
            Ground(out _, out _, out float x1, out _);
            float door = DoorOf(z0, doorZ);
            LookModel main = CampusLookRules.Models[0];
            float wall = door + DoorBay;
            into.Add(new CampusBox(mid - main.HalfWidth, wall, mid + main.HalfWidth, wall + main.Depth, 0f));
            LookModel hall = CampusLookRules.Models[1], junior = CampusLookRules.Models[2];
            float middle = z0 + MainDepth + 2f + 15f;
            float wallH = x0 + CampusLookRules.WallFromEdge(hall), wallJ = x1 - CampusLookRules.WallFromEdge(junior);
            into.Add(new CampusBox(wallH, middle - hall.HalfWidth, wallH + hall.Depth, middle + hall.HalfWidth, 0f));
            into.Add(new CampusBox(wallJ - junior.Depth, middle - junior.HalfWidth, wallJ, middle + junior.HalfWidth, 0f));
        }

        /// <summary>The main school's forecourt: its slab in front of its front wall, as wide as its body (CampusLookRules), where the walks and the spine meet.</summary>
        public static CampusBox Forecourt(float doorZ = float.NaN)
        {
            Frame(out float x0, out float z0, out _, out float mid);
            LookModel main = CampusLookRules.Models[0];
            float wall = DoorOf(z0, doorZ) + DoorBay;
            return new CampusBox(mid - main.HalfWidth, z0 + 0.3f, mid + main.HalfWidth, wall, 0f);
        }

        /// <summary>The ground the court and its fence take: the two half courts, the fence's posts and a yard round them.</summary>
        public static CampusBox CourtGround()
        {
            Frame(out float x0, out float z0, out float courtX, out _);
            return new CampusBox(courtX - CourtWidth * 0.5f - 0.4f, z0 + CourtFrom - 0.5f, courtX + CourtWidth * 0.5f + 0.4f, z0 + CourtFrom + CourtLength + 0.5f, 0f);
        }

        /// <summary>
        /// The room a kind of piece needs, its whole size across, deep and high (the walking meshes are cut round it, turned with it): zero
        /// for a piece with none (a plane, a fence).
        /// </summary>
        public static void RoomOf(CampusPieceKind kind, out float across, out float deep, out float high)
        {
            high = 1.5f;
            switch (kind)
            {
                case CampusPieceKind.Bench: across = 2.4f; deep = 0.8f; high = 1.2f; break;
                case CampusPieceKind.Table: across = 3.0f; deep = 3.0f; high = 1.2f; break;
                case CampusPieceKind.Climb: across = 4.2f; deep = 4.2f; high = 2.2f; break;
                case CampusPieceKind.Bin: across = 0.6f; deep = 0.6f; high = 1.0f; break;
                case CampusPieceKind.Oak:
                case CampusPieceKind.Birch:
                case CampusPieceKind.Beech:
                case CampusPieceKind.Maple:
                case CampusPieceKind.Tree: across = 1.0f; deep = 1.0f; high = 3.0f; break;
                case CampusPieceKind.Bush: across = 1.2f; deep = 1.2f; high = 1.2f; break;
                case CampusPieceKind.Hoop: across = 1.2f; deep = 0.9f; high = 3.0f; break;
                default: across = 0f; deep = 0f; break;
            }
        }
    }
}
