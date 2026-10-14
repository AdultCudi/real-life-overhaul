using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A grid over a fixed set of points (the city's lane points), for "which points lie within r of here" in a few cells
    /// instead of a pass over every point. <see cref="Candidates"/> gives every point whose square of 2r round the place holds
    /// it, in the points' own order (ascending index): a caller that then runs the very loop a pass over every point would run,
    /// over these alone, gets exactly the same answer, with any filter it hands asked of the same points in the same order,
    /// because every point it leaves out lies at r or more on the ground (x, z), so at r or more in space, where that loop
    /// skips it before its filter (Traffic/TrafficTools.ClosestWaypoint, against the traffic system's GetClosestWaypoint).
    ///
    /// Made once per city (some 19,000 points: two passes and three int arrays, about 0.5 ms), behind the loading screen.
    /// A question costs the cells it covers and a sort of what they hold (a few hundred numbers for 40 m); the list it fills
    /// is the caller's, kept, so nothing is allocated once it has grown. No Unity or game types: checked offline (NavCheck,
    /// PointGridCheck.cs).
    /// </summary>
    public sealed class PointGrid
    {
        private readonly float _cell;       // metres a cell is wide
        private readonly float _minX, _minZ;
        private readonly int _columns, _rows;
        private readonly int[] _starts;     // cell c holds _items[_starts[c] .. _starts[c + 1]), each cell's points in ascending order
        private readonly int[] _items;

        /// <summary>The number of points the grid was made over.</summary>
        public int Count { get; }

        /// <summary>
        /// Makes the grid over points given by their ground coordinates (x, z), in their own order. <paramref name="cell"/> is
        /// the width of a cell in metres (more than zero).
        /// </summary>
        public PointGrid(float[] xs, float[] zs, float cell)
        {
            if (xs == null || zs == null || xs.Length != zs.Length)
            {
                throw new ArgumentException("one x and one z for each point");
            }
            if (!(cell > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(cell));
            }
            _cell = cell;
            Count = xs.Length;
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            for (int i = 0; i < Count; i++)
            {
                minX = Math.Min(minX, xs[i]);
                minZ = Math.Min(minZ, zs[i]);
                maxX = Math.Max(maxX, xs[i]);
                maxZ = Math.Max(maxZ, zs[i]);
            }
            if (Count == 0)
            {
                minX = minZ = maxX = maxZ = 0f;
            }
            _minX = minX;
            _minZ = minZ;
            _columns = Math.Max(1, (int)((maxX - minX) / cell) + 1);
            _rows = Math.Max(1, (int)((maxZ - minZ) / cell) + 1);
            _starts = new int[_columns * _rows + 1];
            _items = new int[Count];
            int[] cells = new int[Count];
            for (int i = 0; i < Count; i++)
            {
                int c = Column(xs[i]) + Row(zs[i]) * _columns;
                cells[i] = c;
                _starts[c + 1]++;
            }
            for (int c = 0; c < _columns * _rows; c++)
            {
                _starts[c + 1] += _starts[c];
            }
            int[] next = new int[_columns * _rows];
            Array.Copy(_starts, next, next.Length);
            for (int i = 0; i < Count; i++)
            {
                _items[next[cells[i]]++] = i;   // (in ascending order within each cell)
            }
        }

        private int Column(float x)
        {
            int c = (int)((x - _minX) / _cell);
            return c < 0 ? 0 : c >= _columns ? _columns - 1 : c;
        }

        private int Row(float z)
        {
            int r = (int)((z - _minZ) / _cell);
            return r < 0 ? 0 : r >= _rows ? _rows - 1 : r;
        }

        /// <summary>
        /// Fills <paramref name="into"/> (cleared first) with every point in the cells that the square of half-width
        /// <paramref name="reach"/> round (x, z) touches, in ascending order: a superset of the points within
        /// <paramref name="reach"/> on the ground. Nothing is allocated once the list has grown to hold them.
        /// </summary>
        public void Candidates(float x, float z, float reach, List<int> into)
        {
            into.Clear();
            if (Count == 0 || !(reach > 0f))
            {
                return;
            }
            float left = x - reach, right = x + reach, near = z - reach, far = z + reach;
            if (right < _minX || far < _minZ || left > _minX + _columns * _cell || near > _minZ + _rows * _cell)
            {
                return;   // the square lies wholly outside the points
            }
            int c0 = Column(left), c1 = Column(right), r0 = Row(near), r1 = Row(far);
            int cells = 0;
            for (int r = r0; r <= r1; r++)
            {
                for (int c = c0; c <= c1; c++)
                {
                    int cell = c + r * _columns;
                    int end = _starts[cell + 1];
                    for (int k = _starts[cell]; k < end; k++)
                    {
                        into.Add(_items[k]);
                    }
                    cells++;
                }
            }
            if (cells > 1)
            {
                into.Sort();   // the points' own order, as a pass over all of them meets them
            }
        }
    }
}
