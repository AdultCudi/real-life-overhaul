using System;

namespace RealLifeOverhaul.Map25
{
    /// <summary>
    /// One piece of the map's geometry as plain arrays, ready to be handed to a mesh a slice at a time: three floats a vertex
    /// for the place, four half floats for the colour (in linear light, alpha last), 16 bit indices, and the box it lies in
    /// (the renderer culls whole chunks by it). Game-free.
    /// </summary>
    public sealed class Map25Chunk
    {
        public float[] Pos;
        public ushort[] Col;
        public ushort[] Idx;
        public int Vertices, Indices;
        public float MinX, MinY, MinZ, MaxX, MaxY, MaxZ;

        /// <summary>Counts up whenever colours were written again; the renderer sends the colours again when it has seen a lower one.</summary>
        public int ColourVersion;

        /// <summary>The first and last vertex whose colour changed since the renderer last sent them; <c>From &gt; To</c> when none.</summary>
        public int DirtyFrom = int.MaxValue, DirtyTo = -1;

        public void Dirty(int first, int count)
        {
            if (first < DirtyFrom)
            {
                DirtyFrom = first;
            }
            if (first + count - 1 > DirtyTo)
            {
                DirtyTo = first + count - 1;
            }
            ColourVersion++;
        }
    }

    /// <summary>Writes vertices and triangles into growing arrays, then hands them out as one <see cref="Map25Chunk"/>. Not safe across threads: one a thread.</summary>
    public sealed class Map25Builder
    {
        public const int MostVertices = 65000;   // a 16 bit index reaches 65535

        private const int Table = 1024;
        private static readonly float[] LinearOf = MakeTable();

        private float[] _pos;
        private ushort[] _col;
        private ushort[] _idx;
        private int _v, _i;
        private float _minX, _minY, _minZ, _maxX, _maxY, _maxZ;

        public Map25Builder(int capacity)
        {
            _pos = new float[capacity * 3];
            _col = new ushort[capacity * 4];
            _idx = new ushort[capacity * 3];
            Clear();
        }

        public int Vertices => _v;
        public int Indices => _i;

        public void Clear()
        {
            _v = 0;
            _i = 0;
            _minX = _minY = _minZ = float.MaxValue;
            _maxX = _maxY = _maxZ = float.MinValue;
        }

        private static float[] MakeTable()
        {
            var table = new float[Table + 1];
            for (int i = 0; i <= Table; i++)
            {
                table[i] = Map25Math.Linear(i / (float)Table);
            }
            return table;
        }

        /// <summary>A colour channel from how it is written to light, by a table (the same to 1 part in 20,000, with no power in the loop).</summary>
        public static float ToLight(float c)
        {
            if (c <= 0f)
            {
                return 0f;
            }
            if (c >= 1f)
            {
                return 1f;
            }
            float at = c * Table;
            int k = (int)at;
            float f = at - k;
            return LinearOf[k] + (LinearOf[k + 1] - LinearOf[k]) * f;
        }

        /// <summary>One vertex; <paramref name="alpha"/> is the share of the colour laid over what is there (1 for the solid parts).</summary>
        public int Vertex(float x, float y, float z, Map25Colour c, float alpha)
        {
            if (_v >= _pos.Length / 3)
            {
                Array.Resize(ref _pos, _pos.Length * 2);
                Array.Resize(ref _col, _col.Length * 2);
            }
            int p = _v * 3, q = _v * 4;
            _pos[p] = x;
            _pos[p + 1] = y;
            _pos[p + 2] = z;
            _col[q] = Map25Math.Half(ToLight(c.R));
            _col[q + 1] = Map25Math.Half(ToLight(c.G));
            _col[q + 2] = Map25Math.Half(ToLight(c.B));
            _col[q + 3] = Map25Math.Half(alpha);
            if (x < _minX) { _minX = x; }
            if (x > _maxX) { _maxX = x; }
            if (y < _minY) { _minY = y; }
            if (y > _maxY) { _maxY = y; }
            if (z < _minZ) { _minZ = z; }
            if (z > _maxZ) { _maxZ = z; }
            return _v++;
        }

        public int Vertex(float x, float y, float z, Map25Colour c)
        {
            return Vertex(x, y, z, c, 1f);
        }

        public void Triangle(int a, int b, int c)
        {
            if (_i + 3 > _idx.Length)
            {
                Array.Resize(ref _idx, _idx.Length * 2);
            }
            _idx[_i++] = (ushort)a;
            _idx[_i++] = (ushort)b;
            _idx[_i++] = (ushort)c;
        }

        /// <summary>Two triangles over four corners in a ring.</summary>
        public void Quad(int a, int b, int c, int d)
        {
            Triangle(a, b, c);
            Triangle(a, c, d);
        }

        /// <summary>A flat quad in one colour; the corners go round.</summary>
        public void Flat(float x0, float y0, float z0, float x1, float y1, float z1, float x2, float y2, float z2, float x3, float y3, float z3, Map25Colour c, float alpha)
        {
            int a = Vertex(x0, y0, z0, c, alpha);
            int b = Vertex(x1, y1, z1, c, alpha);
            int cc = Vertex(x2, y2, z2, c, alpha);
            int d = Vertex(x3, y3, z3, c, alpha);
            Quad(a, b, cc, d);
        }

        /// <summary>A vertical strip of wall from <paramref name="ya"/> to <paramref name="yb"/> over the line a to b, shaded from one colour to the other.</summary>
        public void Strip(float ax, float az, float bx, float bz, float ya, float yb, Map25Colour low, Map25Colour high)
        {
            int p = Vertex(ax, ya, az, low);
            int q = Vertex(bx, ya, bz, low);
            int r = Vertex(bx, yb, bz, high);
            int s = Vertex(ax, yb, az, high);
            Quad(p, s, r, q);
        }

        /// <summary>Everything written since the last clear, copied out; the builder is cleared.</summary>
        public Map25Chunk ToChunk()
        {
            var chunk = new Map25Chunk
            {
                Pos = new float[_v * 3],
                Col = new ushort[_v * 4],
                Idx = new ushort[_i],
                Vertices = _v,
                Indices = _i,
                MinX = _minX, MinY = _minY, MinZ = _minZ, MaxX = _maxX, MaxY = _maxY, MaxZ = _maxZ
            };
            Array.Copy(_pos, chunk.Pos, _v * 3);
            Array.Copy(_col, chunk.Col, _v * 4);
            Array.Copy(_idx, chunk.Idx, _i);
            Clear();
            return chunk;
        }

        /// <summary>The colours of the vertices written since the last clear, from <paramref name="first"/>, copied into a chunk's array at <paramref name="at"/>.</summary>
        public void CopyColours(int first, int count, ushort[] into, int at)
        {
            Array.Copy(_col, first * 4, into, at * 4, count * 4);
        }
    }
}
