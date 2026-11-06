using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A grid over a fixed set of points (the city's lane points) for "which points lie within r of here". The
    /// candidates come in ascending index, and every point left out lies at r or more on the ground, so a caller's loop
    /// over them gives the same answer as over every point. Made once per city (about 0.5 ms for 19,000 points); a
    /// question costs the cells it covers and a sort, into the caller's kept list. Game-free.
    /// </summary>
    public sealed class PointGrid
    {
        private readonly float _cell;       // metres a cell is wide
        private readonly float _minX, _minZ;
        private readonly int _columns, _rows;
        private readonly int[] _starts;     // cell c holds _items[_starts[c] .. _starts[c + 1]), each cell's points in ascending order
        private readonly int[] _items;

        public int Count { get; }

        /// <summary>Makes the grid over points by their ground coordinates; <paramref name="cell"/> in metres, above 0.</summary>
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
                _items[next[cells[i]]++] = i;   // ascending within each cell
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
        /// Fills <paramref name="into"/> with every point in the cells the square of half-width <paramref name="reach"/>
        /// round (x, z) touches, ascending: a superset of the points within reach on the ground.
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
