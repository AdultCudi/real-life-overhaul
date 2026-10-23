using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>A solid box of a building in the building's own axes: metres, Y up from the street at its door.</summary>
    public struct ShellBox
    {
        public float MinX, MaxX, MinY, MaxY, MinZ, MaxZ;

        public ShellBox(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
            MinZ = minZ;
            MaxZ = maxZ;
        }
    }

    /// <summary>One side of a building's shell as a scaffold sees it: where its faces stand along its outward axis and how far it runs.</summary>
    public struct ShellSide
    {
        public float Outer;                  // the outermost face at any height, metres along the outward axis
        public float Ground;                 // the face at street level (what a door is in)
        public float ProudFrom;              // metres up from which the side stands out from its street-level face; the top if never
        public float RunMin, RunMax;         // the side's length along its run, at any height, metres
        public float GroundMin, GroundMax;   // and at street level
    }

    /// <summary>A building's shell: its four sides (+Z, +X, -Z, -X, in that order) and its height.</summary>
    public sealed class WrapShell
    {
        public readonly ShellSide[] Sides = new ShellSide[4];
        public float Top;      // metres above the street at the door
        public bool Valid;     // at least one box a storey high was read
    }

    /// <summary>The game's own construction pieces a wrap is made of (the Buildings scene's SM_Scaffolding_* objects).</summary>
    public enum WrapPieceKind { Long, Short, Corner, LargeFabric, SmallFabric }

    /// <summary>One of the game's scaffold pieces placed: in the building's axes, at the piece's foot, turned about Y.</summary>
    public struct WrapPiece
    {
        public WrapPieceKind Kind;
        public int Side, Lift;
        public float X, Y, Z;     // metres in the building's axes: the middle of the piece's footprint, its foot
        public float Yaw;         // degrees about Y: the piece's +Z looks out from the building

        public bool Fabric => Kind == WrapPieceKind.LargeFabric || Kind == WrapPieceKind.SmallFabric;
    }

    /// <summary>A stretch of a side the scaffold keeps clear of: of its posts only (a door is walked through between them), or of all of it (a tree).</summary>
    public struct WrapKeepOut
    {
        public int Side;
        public float From, To;    // along the side's run, metres
        public bool PostsOnly;
    }

    /// <summary>The wrap of one building: every piece from the bottom up, and how it came out.</summary>
    public sealed class WrapPlan
    {
        public readonly List<WrapPiece> Pieces = new List<WrapPiece>(256);
        public int Lifts;         // lifts of modules from the street up
        public int Modules;       // modules a lift (fabric not counted)
        public int Runs;          // separate runs of scaffold, split by corners, doors and what stands in the way
        public string Why;        // why less than the whole building was wrapped; null when all of it was

        public void Clear()
        {
            Pieces.Clear();
            Lifts = 0;
            Modules = 0;
            Runs = 0;
            Why = null;
        }
    }

    /// <summary>
    /// How the game's own scaffolding is stood round a building, game-free: the game stacks its modules (6.0 and 3.0 m long,
    /// 3.04 m deep) at a 5.17 m pitch with fabric hung from the second lift up and a corner module closing each end of a run
    /// (read from the Buildings scene, 26 buildings). A run is laid along every street side so that no post stands in a
    /// doorway; what the scaffold cannot clear splits the run. Also when a wrap may grow or come down, and by how much a frame.
    /// </summary>
    public static class WrapRules
    {
        public const float Unit = 3.0f;              // metres: the length of the game's 4 m and corner modules; its 8 m module is two
        public const float Depth = 3.04f;            // metres from the back posts to the front, the game's module
        public const float Pitch = 5.17f;            // metres between lifts, as the game stacks them (0.05, 5.21, 10.39 m on 1st Street)
        public const float Gap = 0.12f;              // metres off the outermost face, the mod's own scaffold's gap
        public const float FabricOut = 1.37f;        // metres out from a module's middle to its fabric, as the game hangs it
        public const float FabricLift = 0.04f;       // metres the game's fabric hangs above its lift's foot
        public const float PostClear = 0.95f;        // metres either side of a door's middle kept free of posts: a door and a shoulder each way
        public const float SplitGap = 0.6f;          // metres beyond a door's clear stretch where a run split round it ends
        public const float GroundBand = 2.5f;        // metres: a box starting below this is part of the street-level front
        public const float Proud = 0.3f;             // metres a face may stand out from the street-level one before it counts as an overhang
        public const int MostLifts = 10;             // 52 m: taller buildings are not wrapped to the roof
        public const int MostPieces = 360;           // pieces in one wrap, fabric included: 360 objects is the budget of a whole site
        public const int PlacePerFrame = 6;          // pieces set out a frame out of view: a 120-piece wrap in 20 frames, a third of a second
        public const int TakePerFrame = 12;          // pieces taken down a frame out of view
        public const int BlackPerFrame = 60;         // pieces a frame behind the game's black screen, where nobody sees a frame's length
        private const int MostSplits = 6;            // runs one side may be split into round doors
        private const float ShiftStep = 0.1f;        // metres a run is slid along its side to clear a door

        private static readonly List<float> Boundaries = new List<float>(24);
        private static readonly List<float> Free = new List<float>(16);

        /// <summary>Which way a side faces: its outward axis and its run (the module's +X), both in the building's X and Z.</summary>
        public static void Axes(int side, out float outX, out float outZ, out float runX, out float runZ)
        {
            switch (side & 3)
            {
                case 0: outX = 0f; outZ = 1f; runX = 1f; runZ = 0f; break;
                case 1: outX = 1f; outZ = 0f; runX = 0f; runZ = -1f; break;
                case 2: outX = 0f; outZ = -1f; runX = -1f; runZ = 0f; break;
                default: outX = -1f; outZ = 0f; runX = 0f; runZ = 1f; break;
            }
        }

        /// <summary>Degrees about Y that turn a piece's +Z out of a side.</summary>
        public static float YawOf(int side) => 90f * (side & 3);

        /// <summary>How far a point stands out along a side's outward axis.</summary>
        public static float Outward(int side, float x, float z)
        {
            Axes(side, out float ox, out float oz, out _, out _);
            return ox * x + oz * z;
        }

        /// <summary>Where a point is along a side's run.</summary>
        public static float Along(int side, float x, float z)
        {
            Axes(side, out _, out _, out float rx, out float rz);
            return rx * x + rz * z;
        }

        /// <summary>The building's X and Z of a place given along a side's outward axis and its run.</summary>
        public static void Point(int side, float outward, float along, out float x, out float z)
        {
            Axes(side, out float ox, out float oz, out float rx, out float rz);
            x = ox * outward + rx * along;
            z = oz * outward + rz * along;
        }

        /// <summary>Reads a building's boxes into its four sides and its height. A box below a metre high (a plinth, a plane) is passed over.</summary>
        public static void Measure(ShellBox[] boxes, int count, WrapShell shell)
        {
            shell.Valid = false;
            shell.Top = 0f;
            for (int side = 0; side < 4; side++)
            {
                shell.Sides[side] = new ShellSide
                {
                    Outer = float.MinValue, Ground = float.MinValue, ProudFrom = float.MaxValue,
                    RunMin = float.MaxValue, RunMax = float.MinValue, GroundMin = float.MaxValue, GroundMax = float.MinValue
                };
            }
            for (int i = 0; i < count; i++)
            {
                ShellBox box = boxes[i];
                if (box.MaxY - box.MinY < 1f)
                {
                    continue;
                }
                shell.Valid = true;
                shell.Top = Math.Max(shell.Top, box.MaxY);
                bool street = box.MinY < GroundBand;
                for (int side = 0; side < 4; side++)
                {
                    Extent(box, side, out float face, out float from, out float to);
                    ShellSide s = shell.Sides[side];
                    s.Outer = Math.Max(s.Outer, face);
                    s.RunMin = Math.Min(s.RunMin, from);
                    s.RunMax = Math.Max(s.RunMax, to);
                    if (street)
                    {
                        s.Ground = Math.Max(s.Ground, face);
                        s.GroundMin = Math.Min(s.GroundMin, from);
                        s.GroundMax = Math.Max(s.GroundMax, to);
                    }
                    shell.Sides[side] = s;
                }
            }
            for (int side = 0; side < 4 && shell.Valid; side++)
            {
                ShellSide s = shell.Sides[side];
                if (s.Ground == float.MinValue)
                {
                    // Nothing reaches the street (a building on stilts): its lowest face stands for the front.
                    s.Ground = s.Outer;
                    s.GroundMin = s.RunMin;
                    s.GroundMax = s.RunMax;
                }
                s.ProudFrom = shell.Top;
                for (int i = 0; i < count; i++)
                {
                    ShellBox box = boxes[i];
                    if (box.MaxY - box.MinY < 1f)
                    {
                        continue;
                    }
                    Extent(box, side, out float face, out _, out _);
                    if (face > s.Ground + Proud)
                    {
                        s.ProudFrom = Math.Min(s.ProudFrom, box.MinY);
                    }
                }
                shell.Sides[side] = s;
            }
        }

        // A box seen from one side: its face along the outward axis, and its extent along the run.
        private static void Extent(ShellBox box, int side, out float face, out float from, out float to)
        {
            switch (side & 3)
            {
                case 0: face = box.MaxZ; from = box.MinX; to = box.MaxX; break;
                case 1: face = box.MaxX; from = -box.MaxZ; to = -box.MinZ; break;
                case 2: face = -box.MinZ; from = -box.MaxX; to = -box.MinX; break;
                default: face = -box.MinX; from = box.MinZ; to = box.MaxZ; break;
            }
        }

        /// <summary>The side a point (a door) is nearest, by its street-level face.</summary>
        public static int SideOf(WrapShell shell, float x, float z)
        {
            int best = 0;
            float bestGap = float.MaxValue;
            for (int side = 0; side < 4; side++)
            {
                ShellSide s = shell.Sides[side];
                float along = Along(side, x, z);
                float outside = Math.Abs(Outward(side, x, z) - s.Ground);
                float beyond = along < s.GroundMin ? s.GroundMin - along : along > s.GroundMax ? along - s.GroundMax : 0f;
                float gap = outside + beyond;
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = side;
                }
            }
            return best;
        }

        /// <summary>Lifts to the roof: the top deck within half a lift of 0.6 m below the roof line, one at least, <see cref="MostLifts"/> at most.</summary>
        public static int LiftsFor(float top)
        {
            int lifts = (int)Math.Round((top - 0.6f) / Pitch, MidpointRounding.AwayFromZero);
            return lifts < 1 ? 1 : lifts > MostLifts ? MostLifts : lifts;
        }

        /// <summary>
        /// The wrap of a building: a run along each street side, from the bottom up, clear of every keep-out. The pieces of a
        /// lift come before those of the next, and a lift's fabric after its modules, so a wrap set out in order grows upwards.
        /// </summary>
        public static void Plan(WrapShell shell, bool[] street, List<WrapKeepOut> keepOuts, int mostLifts, int mostPieces, WrapPlan plan)
        {
            plan.Clear();
            if (!shell.Valid)
            {
                plan.Why = "the building has no solid box a storey high";
                return;
            }
            List<Run> runs = Runs;
            runs.Clear();
            for (int side = 0; side < 4; side++)
            {
                if (street[side])
                {
                    LaySide(shell, side, keepOuts, runs, plan);
                }
            }
            if (runs.Count == 0)
            {
                plan.Why = plan.Why ?? "no street side has room for the game's scaffold";
                return;
            }
            int perLift = 0;
            for (int i = 0; i < runs.Count; i++)
            {
                perLift += runs[i].Count;
            }
            int lifts = Math.Min(LiftsFor(shell.Top), Math.Max(1, mostLifts));
            int most = Math.Max(perLift, mostPieces);
            if (perLift * (2 * lifts - 1) > most)
            {
                int fits = Math.Max(1, (most / perLift + 1) / 2);
                plan.Why = "the building would take " + perLift * (2 * lifts - 1) + " pieces to the roof; the wrap stops at lift " + fits;
                lifts = fits;
            }
            else if (LiftsFor(shell.Top) > lifts)
            {
                plan.Why = "the building is taller than " + lifts + " lifts; the wrap stops there";
            }
            plan.Lifts = lifts;
            plan.Modules = perLift;
            plan.Runs = runs.Count;
            for (int lift = 0; lift < lifts; lift++)
            {
                for (int r = 0; r < runs.Count; r++)
                {
                    runs[r].Emit(shell, lift, false, plan.Pieces);
                }
                if (lift == 0)
                {
                    continue;   // the street lift stays open, as the game's own: the pavement is walked under it
                }
                for (int r = 0; r < runs.Count; r++)
                {
                    runs[r].Emit(shell, lift, true, plan.Pieces);
                }
            }
        }

        // One run of modules along a side: its start and the widths in order (3 or 6 m), corner first and last.
        private sealed class Run
        {
            public int Side;
            public float Start;
            public readonly List<float> Widths = new List<float>(12);
            public int Count => Widths.Count;

            public void Emit(WrapShell shell, int lift, bool fabric, List<WrapPiece> into)
            {
                float outward = shell.Sides[Side].Outer + Gap + Depth * 0.5f + (fabric ? FabricOut : 0f);
                float at = Start;
                float yaw = YawOf(Side);
                for (int i = 0; i < Widths.Count; i++)
                {
                    float width = Widths[i];
                    float middle = at + width * 0.5f;
                    at += width;
                    Point(Side, outward, middle, out float x, out float z);
                    bool corner = i == 0 || i == Widths.Count - 1;
                    WrapPieceKind kind = fabric ? (width > Unit ? WrapPieceKind.LargeFabric : WrapPieceKind.SmallFabric)
                        : corner ? WrapPieceKind.Corner : width > Unit ? WrapPieceKind.Long : WrapPieceKind.Short;
                    // The game's corner module is closed at its own -X end and its front: at a run's far end it is turned a
                    // quarter, which closes that end instead (the 8 m runs of Murray Hill's Section E).
                    float turn = !fabric && corner && i == Widths.Count - 1 && Widths.Count > 1 ? 90f : 0f;
                    into.Add(new WrapPiece
                    {
                        Kind = kind, Side = Side, Lift = lift, X = x, Z = z, Y = lift * Pitch + (fabric ? FabricLift : 0f), Yaw = (yaw + turn) % 360f
                    });
                }
            }
        }

        private static readonly List<Run> Runs = new List<Run>(8);

        // Every run of a street side: its length less what must be kept wholly clear, each part laid clear of doors.
        private static void LaySide(WrapShell shell, int side, List<WrapKeepOut> keepOuts, List<Run> runs, WrapPlan plan)
        {
            ShellSide s = shell.Sides[side];
            Free.Clear();
            Free.Add(s.RunMin);
            Free.Add(s.RunMax);
            if (keepOuts != null)
            {
                for (int k = 0; k < keepOuts.Count; k++)
                {
                    WrapKeepOut keep = keepOuts[k];
                    if (keep.Side == side && !keep.PostsOnly)
                    {
                        Cut(keep.From - 0.15f, keep.To + 0.15f);
                    }
                }
            }
            for (int i = 0; i + 1 < Free.Count; i += 2)
            {
                Lay(side, Free[i], Free[i + 1], keepOuts, runs, 0);
            }
        }

        // Takes a stretch out of the free stretches (kept as pairs of from and to, in order).
        private static void Cut(float from, float to)
        {
            for (int i = Free.Count - 2; i >= 0; i -= 2)
            {
                float a = Free[i], b = Free[i + 1];
                if (to <= a || from >= b)
                {
                    continue;
                }
                Free.RemoveRange(i, 2);
                if (to < b)
                {
                    Free.Insert(i, b);
                    Free.Insert(i, to);
                }
                if (from > a)
                {
                    Free.Insert(i, from);
                    Free.Insert(i, a);
                }
            }
        }

        // One stretch: as many 3 m units as fit, corners at both ends, 6 m modules between with one 3 m where the count is
        // odd; slid along the slack and the 3 m moved until no post stands in a door's way. If none does, the stretch is
        // split round the first door in the way.
        private static void Lay(int side, float from, float to, List<WrapKeepOut> keepOuts, List<Run> runs, int depth)
        {
            float length = to - from;
            int units = (int)Math.Floor((length + 0.05f) / Unit);
            if (units < 2)
            {
                return;
            }
            float slack = Math.Max(0f, length - units * Unit);
            int middle = units - 2;
            int longs = middle / 2;
            bool odd = middle % 2 == 1;
            int steps = (int)Math.Floor(slack * 0.5f / ShiftStep + 0.001f);
            for (int step = 0; step <= 2 * steps; step++)
            {
                float shift = (step + 1) / 2 * ShiftStep * (step % 2 == 1 ? 1f : -1f);
                float start = from + slack * 0.5f + shift;
                for (int k = 0; k <= (odd ? longs : 0); k++)
                {
                    Lengths(start, longs, odd, k);
                    if (Clear(side, keepOuts))
                    {
                        var run = new Run { Side = side, Start = start };
                        run.Widths.Add(Unit);
                        for (int m = 0; m < longs + (odd ? 1 : 0); m++)
                        {
                            run.Widths.Add(odd && m == k ? Unit : 2f * Unit);
                        }
                        run.Widths.Add(Unit);
                        runs.Add(run);
                        return;
                    }
                }
            }
            if (depth >= MostSplits)
            {
                return;
            }
            // No placing clears every door: the first door in the way gets a gap of its own.
            for (int i = 0; keepOuts != null && i < keepOuts.Count; i++)
            {
                WrapKeepOut keep = keepOuts[i];
                if (keep.Side == side && keep.PostsOnly && keep.To > from && keep.From < to)
                {
                    Lay(side, from, keep.From - SplitGap, keepOuts, runs, depth + 1);
                    Lay(side, keep.To + SplitGap, to, keepOuts, runs, depth + 1);
                    return;
                }
            }
        }

        // The posts of a run (every joint between modules, and both ends), in Boundaries.
        private static void Lengths(float start, int longs, bool odd, int shortAt)
        {
            Boundaries.Clear();
            float at = start;
            Boundaries.Add(at);
            at += Unit;
            Boundaries.Add(at);
            for (int m = 0; m < longs + (odd ? 1 : 0); m++)
            {
                at += odd && m == shortAt ? Unit : 2f * Unit;
                Boundaries.Add(at);
            }
            at += Unit;
            Boundaries.Add(at);
        }

        private static bool Clear(int side, List<WrapKeepOut> keepOuts)
        {
            for (int i = 0; keepOuts != null && i < keepOuts.Count; i++)
            {
                WrapKeepOut keep = keepOuts[i];
                if (keep.Side != side || !keep.PostsOnly)
                {
                    continue;
                }
                for (int b = 0; b < Boundaries.Count; b++)
                {
                    if (Boundaries[b] > keep.From && Boundaries[b] < keep.To)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>The stretch of a side a door's posts must stay out of.</summary>
        public static WrapKeepOut Door(int side, float along) => new WrapKeepOut { Side = side, From = along - PostClear, To = along + PostClear, PostsOnly = true };

        /// <summary>What a rebuild site shows of its wrap in a stage (0 to 4): how many lifts, and whether the fabric is hung.</summary>
        public static void RebuildStage(int stage, int lifts, out int shown, out bool fabric)
        {
            switch (stage)
            {
                case 0: shown = 0; fabric = false; break;                           // hoarding only
                case 1: shown = Math.Max(1, (lifts + 1) / 2); fabric = false; break;  // going up
                case 2:
                case 3: shown = lifts; fabric = true; break;                         // wrapped to the roof; the inspection
                default: shown = Math.Max(1, lifts / 2); fabric = false; break;     // coming down
            }
        }

        /// <summary>What a new plot shows of its wrap in a stage: only the last, round the finished frame.</summary>
        public static void PlotStage(int stage, int lifts, out int shown, out bool fabric)
        {
            shown = stage >= 4 ? lifts : 0;
            fabric = stage >= 4;
        }

        /// <summary>Whether a piece stands in a stage that shows so many lifts, with or without the fabric.</summary>
        public static bool Shown(WrapPiece piece, int lifts, bool fabric) => piece.Lift < lifts && (!piece.Fabric || fabric);

        /// <summary>
        /// Pieces a wrap may set out or take down this frame: none while it is in view (nobody sees one appear), unless a test
        /// asked for it; and always only a few a frame, never the whole wrap at once.
        /// </summary>
        public static int Budget(bool adding, bool inView, bool black, bool now)
        {
            if (black)
            {
                return BlackPerFrame;
            }
            if (inView && !now)
            {
                return 0;
            }
            return adding ? PlacePerFrame : TakePerFrame;
        }
    }
}
