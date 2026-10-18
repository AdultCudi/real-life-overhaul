using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>The floor of a room as a grid of square cells: outside, open, or under the player's furniture. No engine type: NavCheck tests it.</summary>
    public sealed class RoomGrid
    {
        public const byte Outside = 0, Open = 1, Furniture = 2;

        private readonly byte[] _cells;
        private int[] _queue;

        public RoomGrid(float minX, float minZ, float maxX, float maxZ, float cell)
        {
            if (cell <= 0.01f || maxX <= minX || maxZ <= minZ)
            {
                throw new ArgumentException("a grid needs a size and a cell");
            }
            MinX = minX;
            MinZ = minZ;
            Cell = cell;
            Cols = Math.Max(1, (int)Math.Ceiling((maxX - minX) / cell));
            Rows = Math.Max(1, (int)Math.Ceiling((maxZ - minZ) / cell));
            _cells = new byte[Cols * Rows];
        }

        public float MinX { get; }
        public float MinZ { get; }
        public float Cell { get; }
        public int Cols { get; }
        public int Rows { get; }
        public int Count => _cells.Length;

        public byte this[int col, int row]
        {
            get => col >= 0 && row >= 0 && col < Cols && row < Rows ? _cells[row * Cols + col] : Outside;
            set
            {
                if (col >= 0 && row >= 0 && col < Cols && row < Rows)
                {
                    _cells[row * Cols + col] = value;
                }
            }
        }

        public byte At(int index) => _cells[index];

        public bool IsOpen(int col, int row) => this[col, row] == Open;

        public void Centre(int col, int row, out float x, out float z)
        {
            x = MinX + (col + 0.5f) * Cell;
            z = MinZ + (row + 0.5f) * Cell;
        }

        public void Centre(int index, out float x, out float z) => Centre(index % Cols, index / Cols, out x, out z);

        public bool CellOf(float x, float z, out int col, out int row)
        {
            col = (int)Math.Floor((x - MinX) / Cell);
            row = (int)Math.Floor((z - MinZ) / Cell);
            return col >= 0 && row >= 0 && col < Cols && row < Rows;
        }

        /// <summary>The open cells walkable from a point round anything closed, into[cell]. Returns the count; 0 with no open cell within a metre.</summary>
        public int Reach(float x, float z, bool[] closed, bool[] into)
        {
            Array.Clear(into, 0, into.Length);
            int start = NearestOpen(x, z, closed);
            if (start < 0)
            {
                return 0;
            }
            if (_queue == null || _queue.Length < _cells.Length)
            {
                _queue = new int[_cells.Length];
            }
            int head = 0, tail = 0;
            _queue[tail++] = start;
            into[start] = true;
            while (head < tail)
            {
                int i = _queue[head++];
                int c = i % Cols, r = i / Cols;
                for (int k = 0; k < 4; k++)
                {
                    int nc = c + (k == 0 ? 1 : k == 1 ? -1 : 0), nr = r + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nc < 0 || nr < 0 || nc >= Cols || nr >= Rows)
                    {
                        continue;
                    }
                    int n = nr * Cols + nc;
                    if (!into[n] && _cells[n] == Open && (closed == null || !closed[n]))
                    {
                        into[n] = true;
                        _queue[tail++] = n;
                    }
                }
            }
            return tail;
        }

        /// <summary>The open cell nearest a point, within a metre, that is not closed; -1 for none.</summary>
        public int NearestOpen(float x, float z, bool[] closed)
        {
            CellOf(x, z, out int col, out int row);
            int reach = Math.Max(1, (int)Math.Ceiling(1f / Cell));
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int r = row - reach; r <= row + reach; r++)
            {
                for (int c = col - reach; c <= col + reach; c++)
                {
                    if (c < 0 || r < 0 || c >= Cols || r >= Rows)
                    {
                        continue;
                    }
                    int i = r * Cols + c;
                    if (_cells[i] != Open || (closed != null && closed[i]))
                    {
                        continue;
                    }
                    Centre(c, r, out float cx, out float cz);
                    float sqr = (cx - x) * (cx - x) + (cz - z) * (cz - z);
                    if (sqr < bestSqr && sqr <= 1.0001f + Cell * Cell)
                    {
                        bestSqr = sqr;
                        best = i;
                    }
                }
            }
            return best;
        }

        /// <summary>The cells within a distance of something of the player's: into[cell].</summary>
        public void NearFurniture(float within, bool[] into)
        {
            Array.Clear(into, 0, into.Length);
            int reach = (int)Math.Ceiling(within / Cell);
            float sqr = within * within + 1e-4f;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] != Furniture)
                {
                    continue;
                }
                int c = i % Cols, r = i / Cols;
                for (int dr = -reach; dr <= reach; dr++)
                {
                    for (int dc = -reach; dc <= reach; dc++)
                    {
                        int nc = c + dc, nr = r + dr;
                        if (nc >= 0 && nr >= 0 && nc < Cols && nr < Rows && (dc * dc + dr * dr) * Cell * Cell <= sqr)
                        {
                            into[nr * Cols + nc] = true;
                        }
                    }
                }
            }
        }

        /// <summary>Closes the cells under the pieces that stand on the floor, each grown by a margin, into[cell]; what was closed stays.</summary>
        public void Close(IList<SitePiece> pieces, float margin, bool[] into)
        {
            for (int p = 0; pieces != null && p < pieces.Count; p++)
            {
                SitePiece piece = pieces[p];
                if (!piece.Stands)
                {
                    continue;
                }
                CellOf(piece.X - piece.HalfX - margin, piece.Z - piece.HalfZ - margin, out int c0, out int r0);
                CellOf(piece.X + piece.HalfX + margin, piece.Z + piece.HalfZ + margin, out int c1, out int r1);
                for (int r = Math.Max(0, r0); r <= Math.Min(Rows - 1, r1); r++)
                {
                    for (int c = Math.Max(0, c0); c <= Math.Min(Cols - 1, c1); c++)
                    {
                        Centre(c, r, out float x, out float z);
                        if (piece.Contains(x, z, margin))
                        {
                            into[r * Cols + c] = true;
                        }
                    }
                }
            }
        }

        /// <summary>True if every cell whose middle is in the rectangle is open, not barred and (with <paramref name="reached"/>) reached; at least one.</summary>
        public bool AllOpen(float x0, float z0, float x1, float z1, bool[] barred, bool[] reached = null)
        {
            CellOf(x0, z0, out int c0, out int r0);
            CellOf(x1, z1, out int c1, out int r1);
            int seen = 0;
            for (int r = r0; r <= r1; r++)
            {
                for (int c = c0; c <= c1; c++)
                {
                    Centre(c, r, out float x, out float z);
                    if (x < x0 || x > x1 || z < z0 || z > z1)
                    {
                        continue;
                    }
                    if (c < 0 || r < 0 || c >= Cols || r >= Rows)
                    {
                        return false;
                    }
                    int i = r * Cols + c;
                    if (_cells[i] != Open || (barred != null && barred[i]) || (reached != null && !reached[i]))
                    {
                        return false;
                    }
                    seen++;
                }
            }
            return seen > 0;
        }

        /// <summary>The largest rectangle of usable cells by area, one pass a row; bounds are its cells' outer edges. False if there is none.</summary>
        public bool Largest(bool[] usable, out float x0, out float z0, out float x1, out float z1)
        {
            x0 = z0 = x1 = z1 = 0f;
            var height = new int[Cols];
            var stack = new int[Cols + 1];
            int bestArea = 0, bestC0 = 0, bestC1 = 0, bestR0 = 0, bestR1 = 0;
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    height[c] = usable[r * Cols + c] ? height[c] + 1 : 0;
                }
                int top = 0;
                for (int c = 0; c <= Cols; c++)
                {
                    int h = c < Cols ? height[c] : 0;
                    while (top > 0 && height[stack[top - 1]] >= h)
                    {
                        int tall = height[stack[--top]];
                        int left = top > 0 ? stack[top - 1] + 1 : 0;
                        int area = tall * (c - left);
                        if (area > bestArea)
                        {
                            bestArea = area;
                            bestC0 = left;
                            bestC1 = c - 1;
                            bestR1 = r;
                            bestR0 = r - tall + 1;
                        }
                    }
                    stack[top++] = c;
                }
            }
            if (bestArea == 0)
            {
                return false;
            }
            x0 = MinX + bestC0 * Cell;
            x1 = MinX + (bestC1 + 1) * Cell;
            z0 = MinZ + bestR0 * Cell;
            z1 = MinZ + (bestR1 + 1) * Cell;
            return true;
        }

        /// <summary>Every reached open cell next to the player's furniture, the places it is used from, into[cell]. Returns the count.</summary>
        public int FurnitureSides(bool[] reached, bool[] into)
        {
            Array.Clear(into, 0, into.Length);
            int count = 0;
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] != Furniture)
                {
                    continue;
                }
                int c = i % Cols, r = i / Cols;
                for (int k = 0; k < 4; k++)
                {
                    int nc = c + (k == 0 ? 1 : k == 1 ? -1 : 0), nr = r + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (nc < 0 || nr < 0 || nc >= Cols || nr >= Rows)
                    {
                        continue;
                    }
                    int n = nr * Cols + nc;
                    if (_cells[n] == Open && reached[n] && !into[n])
                    {
                        into[n] = true;
                        count++;
                    }
                }
            }
            return count;
        }

        public bool Reached(float x, float z, bool[] reached)
        {
            return CellOf(x, z, out int c, out int r) && reached[r * Cols + c];
        }
    }
}
