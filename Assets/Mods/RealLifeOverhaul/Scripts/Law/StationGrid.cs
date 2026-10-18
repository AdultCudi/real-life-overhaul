using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Law
{
    /// <summary>A piece of floor in a room's frame: aside A (to the right of somebody walking in) and depth D, with half its sizes.</summary>
    public struct FloorRect
    {
        public float A, D, HalfA, HalfD;

        public FloorRect(float a, float d, float halfA, float halfD)
        {
            A = a;
            D = d;
            HalfA = halfA;
            HalfD = halfD;
        }

        /// <summary>True if the two overlap, each grown by <paramref name="margin"/> on every side.</summary>
        public bool Overlaps(FloorRect other, float margin = 0f)
        {
            return Math.Abs(A - other.A) < HalfA + other.HalfA + 2f * margin && Math.Abs(D - other.D) < HalfD + other.HalfD + 2f * margin;
        }

        /// <summary>True if the point is inside, grown by <paramref name="margin"/>.</summary>
        public bool Contains(float a, float d, float margin = 0f)
        {
            return Math.Abs(a - A) <= HalfA + margin && Math.Abs(d - D) <= HalfD + margin;
        }
    }

    /// <summary>
    /// A station room's floor as a grid of square cells, walkable or not, and the walkway rule every furnished room keeps:
    /// from the door, every place that must be reached can still be walked to with the furniture standing, a body's width
    /// to spare round each piece. Game-free, so NavCheck checks it (StationCheck.cs).
    /// </summary>
    public sealed class StationGrid
    {
        private readonly bool[] _walkable;
        private readonly bool[] _blocked;
        private readonly int[] _from;
        private readonly int[] _queue;
        private int _spread = NoSpread;   // the cell the last spread began from, over the cells as last closed

        private const int NoSpread = -3;  // the cells were closed again since the last spread

        public StationGrid(float minA, float minD, float maxA, float maxD, float cell)
        {
            if (cell <= 0.01f || maxA <= minA || maxD <= minD)
            {
                throw new ArgumentException("a grid needs a size and a cell");
            }
            MinA = minA;
            MinD = minD;
            Cell = cell;
            Cols = Math.Max(1, (int)Math.Ceiling((maxA - minA) / cell));
            Rows = Math.Max(1, (int)Math.Ceiling((maxD - minD) / cell));
            int n = Cols * Rows;
            _walkable = new bool[n];
            _blocked = new bool[n];
            _from = new int[n];
            _queue = new int[n];
        }

        public float MinA { get; }
        public float MinD { get; }
        public float Cell { get; }
        public int Cols { get; }
        public int Rows { get; }

        public void Centre(int col, int row, out float a, out float d)
        {
            a = MinA + (col + 0.5f) * Cell;
            d = MinD + (row + 0.5f) * Cell;
        }

        public void SetWalkable(int col, int row, bool walkable)
        {
            _walkable[row * Cols + col] = walkable;
        }

        public bool Walkable(int col, int row)
        {
            return col >= 0 && row >= 0 && col < Cols && row < Rows && _walkable[row * Cols + col];
        }

        private bool CellOf(float a, float d, out int col, out int row)
        {
            col = (int)Math.Floor((a - MinA) / Cell);
            row = (int)Math.Floor((d - MinD) / Cell);
            return col >= 0 && row >= 0 && col < Cols && row < Rows;
        }

        // The cells the pieces stand on, each piece grown by the margin (a body's half width), are closed.
        private void Close(IList<FloorRect> pieces, IList<bool> keep, float margin)
        {
            _spread = NoSpread;
            Array.Clear(_blocked, 0, _blocked.Length);
            if (pieces == null)
            {
                return;
            }
            for (int i = 0; i < pieces.Count; i++)
            {
                if (keep != null && !keep[i])
                {
                    continue;
                }
                FloorRect p = pieces[i];
                CellOf(p.A - p.HalfA - margin, p.D - p.HalfD - margin, out int c0, out int r0);
                CellOf(p.A + p.HalfA + margin, p.D + p.HalfD + margin, out int c1, out int r1);
                c0 = Math.Max(0, c0);
                r0 = Math.Max(0, r0);
                c1 = Math.Min(Cols - 1, c1);
                r1 = Math.Min(Rows - 1, r1);
                for (int r = r0; r <= r1; r++)
                {
                    for (int c = c0; c <= c1; c++)
                    {
                        Centre(c, r, out float a, out float d);
                        if (p.Contains(a, d, margin))
                        {
                            _blocked[r * Cols + c] = true;
                        }
                    }
                }
            }
        }

        private int Nearest(float a, float d, float reach)
        {
            int best = -1;
            float bestSqr = reach * reach;
            int span = (int)Math.Ceiling(reach / Cell) + 1;
            CellOf(a, d, out int c0, out int r0);
            for (int r = r0 - span; r <= r0 + span; r++)
            {
                for (int c = c0 - span; c <= c0 + span; c++)
                {
                    if (!Walkable(c, r) || _blocked[r * Cols + c])
                    {
                        continue;
                    }
                    Centre(c, r, out float ca, out float cd);
                    float sqr = (ca - a) * (ca - a) + (cd - d) * (cd - d);
                    if (sqr <= bestSqr)
                    {
                        bestSqr = sqr;
                        best = r * Cols + c;
                    }
                }
            }
            return best;
        }

        // Breadth first over the open cells from one cell; each cell reached keeps the one it was reached from. The count reached.
        private int Spread(int start)
        {
            _spread = start;
            for (int i = 0; i < _from.Length; i++)
            {
                _from[i] = -2;
            }
            if (start < 0)
            {
                return 0;
            }
            int head = 0, tail = 0;
            _queue[tail++] = start;
            _from[start] = -1;
            while (head < tail)
            {
                int at = _queue[head++];
                int col = at % Cols, row = at / Cols;
                for (int k = 0; k < 4; k++)
                {
                    int c = col + (k == 0 ? 1 : k == 1 ? -1 : 0), r = row + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (!Walkable(c, r))
                    {
                        continue;
                    }
                    int next = r * Cols + c;
                    if (_blocked[next] || _from[next] != -2)
                    {
                        continue;
                    }
                    _from[next] = at;
                    _queue[tail++] = next;
                }
            }
            return tail;
        }

        /// <summary>
        /// True if somebody can walk from one point to another with the kept pieces standing, each grown by
        /// <paramref name="margin"/>; each end may be up to <paramref name="reach"/> from an open cell.
        /// </summary>
        public bool Reachable(float fromA, float fromD, float toA, float toD, IList<FloorRect> pieces, IList<bool> keep, float margin, float reach)
        {
            Close(pieces, keep, margin);
            int start = Nearest(fromA, fromD, reach);
            int goal = Nearest(toA, toD, reach);
            if (start < 0 || goal < 0)
            {
                return false;
            }
            Spread(start);
            return _from[goal] != -2;
        }

        /// <summary>
        /// Where somebody stands to sit down: the open cell nearest a point, within a reach, reachable from the door.
        /// <paramref name="sameBlocks"/>: the pieces and what is kept are as at the last call, so the walk from the door made
        /// then stands, and only the cells round the point are looked at.
        /// </summary>
        public bool NearestOpen(float doorA, float doorD, float a, float d, float reach, IList<FloorRect> pieces, IList<bool> keep, float margin,
            out float openA, out float openD, bool sameBlocks = false)
        {
            openA = a;
            openD = d;
            if (!sameBlocks)
            {
                Close(pieces, keep, margin);
            }
            int door = Nearest(doorA, doorD, Math.Max(reach, 1f));
            if (door != _spread)
            {
                Spread(door);
            }
            int best = -1;
            float bestSqr = reach * reach;
            int span = (int)Math.Ceiling(reach / Cell) + 1;
            CellOf(a, d, out int c0, out int r0);
            for (int r = r0 - span; r <= r0 + span; r++)
            {
                for (int c = c0 - span; c <= c0 + span; c++)
                {
                    if (!Walkable(c, r) || _blocked[r * Cols + c] || _from[r * Cols + c] == -2)
                    {
                        continue;
                    }
                    Centre(c, r, out float ca, out float cd);
                    float sqr = (ca - a) * (ca - a) + (cd - d) * (cd - d);
                    if (sqr <= bestSqr)
                    {
                        bestSqr = sqr;
                        best = r * Cols + c;
                    }
                }
            }
            if (best < 0)
            {
                return false;
            }
            Centre(best % Cols, best / Cols, out openA, out openD);
            return true;
        }

        /// <summary>
        /// Takes pieces out (their keep set to false) until every target can be walked to from the door; returns how many.
        /// For each target cut off, every kept piece on its way in the empty room goes; fixed pieces never do, and a target
        /// the room itself cuts off is left.
        /// </summary>
        public int KeepWaysOpen(float doorA, float doorD, IList<float> targetsA, IList<float> targetsD, IList<FloorRect> pieces, IList<bool> keep,
            IList<bool> isFixed, float margin, float reach)
        {
            int removed = 0;
            int door = -1;
            bool walked = false;   // the walk from the door over the kept pieces as they stand is made: every target reads it
            for (int t = 0; t < targetsA.Count; t++)
            {
                for (int round = 0; round < 6; round++)
                {
                    if (!walked)
                    {
                        Close(pieces, keep, margin);
                        door = Nearest(doorA, doorD, reach);
                        Spread(door);
                        walked = true;
                    }
                    int end = Nearest(targetsA[t], targetsD[t], reach);
                    if (door >= 0 && end >= 0 && _from[end] != -2)
                    {
                        break;   // reachable, as Reachable would say
                    }
                    walked = false;
                    // The way in a room with only the fixed pieces in it.
                    Close(pieces, Only(isFixed, keep, pieces.Count), margin);
                    int start = Nearest(doorA, doorD, reach);
                    int goal = Nearest(targetsA[t], targetsD[t], reach);
                    if (start < 0 || goal < 0)
                    {
                        break;
                    }
                    Spread(start);
                    if (_from[goal] == -2)
                    {
                        break;   // cut off by the room or the fixed pieces: taking anything out does not help
                    }
                    int took = 0;
                    for (int at = goal; at >= 0; at = _from[at])
                    {
                        Centre(at % Cols, at / Cols, out float a, out float d);
                        for (int i = 0; i < pieces.Count; i++)
                        {
                            if (keep[i] && (isFixed == null || !isFixed[i]) && pieces[i].Contains(a, d, margin))
                            {
                                keep[i] = false;
                                took++;
                            }
                        }
                    }
                    removed += took;
                    if (took == 0)
                    {
                        break;
                    }
                }
            }
            return removed;
        }

        private static List<bool> _only;

        private static List<bool> Only(IList<bool> isFixed, IList<bool> keep, int count)
        {
            _only = _only ?? new List<bool>(64);
            _only.Clear();
            for (int i = 0; i < count; i++)
            {
                _only.Add(isFixed != null && isFixed[i] && keep[i]);
            }
            return _only;
        }
    }

        /// <summary>How many people a furnished station has, and where: modest, and within what the mod's pool hands out.</summary>
    public struct StationCrew
    {
        public const int MostAtDesks = 2;
        public const int MostPresence = 4;   // People/ActorPool.MaxPresence: those only there to be seen
        public const int Most = 6;

        public int AtDesks;      // officers seated at a desk, typing (presence)
        public int OnBreak;      // an officer seated in the break area (a person of the pool's own)
        public int Walking;      // an officer walking between the rooms, who comes on and goes off shift (presence)
        public int Visitors;     // a member of the public waiting on a seat, who comes and goes (presence)
        public int Sergeant;     // the desk sergeant (a person of the pool's own)

        public int Total => AtDesks + OnBreak + Walking + Visitors + Sergeant;
        public int Presence => AtDesks + Walking + Visitors;

        /// <summary>The cast for a room with these seats at desks, in a break area and to wait on, this deep in metres.</summary>
        public static StationCrew For(int deskSeats, int breakSeats, int waitingSeats, float depth, bool furnished)
        {
            var crew = new StationCrew { Sergeant = 1 };
            if (!furnished)
            {
                return crew;
            }
            crew.AtDesks = Math.Max(0, Math.Min(MostAtDesks, deskSeats));
            crew.Visitors = waitingSeats > 0 ? 1 : 0;
            crew.Walking = depth >= 6f ? 1 : 0;
            crew.OnBreak = breakSeats > 0 && depth >= 7f ? 1 : 0;
            while (crew.Presence > MostPresence)
            {
                crew.AtDesks--;
            }
            if (crew.Total > Most)
            {
                crew.OnBreak = 0;
            }
            return crew;
        }
    }
}
