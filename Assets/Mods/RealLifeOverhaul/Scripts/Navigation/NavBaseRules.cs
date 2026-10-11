using System;
using System.IO;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The arithmetic of the minimap's base picture (the city under the roads: buildings and water). No engine and no
    /// game types: checked offline (RealLifeOverhaul.Build/NavCheck/BaseCheck.cs).
    ///
    /// The picture covers the same piece of the world as the road picture, from the same origin, so one conversion
    /// serves both: a pixel is (world - origin) x pixels per metre, counted from the picture's lower left corner,
    /// x to the east and z to the north. Nothing here runs per frame except what <see cref="NavView.Window"/> already
    /// costs (four divisions when the player has moved).
    /// </summary>
    public static class NavBaseMath
    {
        public const int LongestSide = 2048;      // pixels: 3.6 km of city at 1.77 m a pixel; see NavBase's summary for why that is enough
        public const float MostPerMetre = 1.25f;  // pixels a metre in a small city: what the road picture has

        /// <summary>Where a place is on the picture, in pixels and parts of one, along one axis.</summary>
        public static float ToPixel(float world, float origin, float perMetre)
        {
            return (world - origin) * perMetre;
        }

        /// <summary>Where a pixel (or a part of one) is in the world, along one axis.</summary>
        public static float ToWorld(float pixel, float origin, float perMetre)
        {
            return origin + pixel / perMetre;
        }

        /// <summary>The pixel the graphics card reads for a place: the one it lies in, or the edge's when it lies outside (the picture is not repeated).</summary>
        public static int Texel(float pixel, int size)
        {
            int index = (int)Math.Floor(pixel);
            return index < 0 ? 0 : index > size - 1 ? size - 1 : index;
        }

        /// <summary>
        /// The opposite of <see cref="NavView.ToView"/>: the place in the world that is drawn at a point of the view
        /// (in the view's units from its middle), for a view centred on (px, pz) and turned by <paramref name="turn"/> degrees.
        /// </summary>
        public static void FromView(float vx, float vy, float px, float pz, float turn, float unitsPerMetre, out float wx, out float wz)
        {
            double radians = turn * Math.PI / 180.0;
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            wx = px + (vx * cos + vy * sin) / unitsPerMetre;
            wz = pz + (-vx * sin + vy * cos) / unitsPerMetre;
        }

        /// <summary>The share of a window on the picture (texture coordinates: corner and size) that lies on the picture itself; the rest shows the edge's pixels drawn out.</summary>
        public static float Inside(float u, float v, float width, float height)
        {
            if (width <= 0f || height <= 0f)
            {
                return 0f;
            }
            float x = Math.Min(1f, u + width) - Math.Max(0f, u);
            float y = Math.Min(1f, v + height) - Math.Max(0f, v);
            return x <= 0f || y <= 0f ? 0f : x * y / (width * height);
        }

        /// <summary>
        /// How far (metres) the base picture draws a place from where the markers' layer draws it, for a view of
        /// <paramref name="metres"/> a side centred on the player. The markers are placed in metres from the player;
        /// the picture is placed by its window. The two must agree, whatever the view is turned by (both are turned
        /// together).
        /// </summary>
        public static float Misfit(float wx, float wz, float px, float pz, float originX, float originZ, float worldWidth, float worldHeight, float metres)
        {
            NavView.Window(px, pz, originX, originZ, worldWidth, worldHeight, metres, out float u, out float v, out float width, out float height);
            float byPictureX = (((wx - originX) / worldWidth - u) / width - 0.5f) * metres;
            float byPictureZ = (((wz - originZ) / worldHeight - v) / height - 0.5f) * metres;
            float dx = byPictureX - (wx - px);
            float dz = byPictureZ - (wz - pz);
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// The outline round some points seen from above (their convex hull), anticlockwise, written from
        /// <paramref name="at"/> on. The points are put in order where they lie (they are the caller's scratch).
        /// Returns how many corners the outline has: fewer than 3 if the points are one place or one line.
        /// </summary>
        public static int Hull(float[] x, float[] z, int count, float[] hullX, float[] hullZ, int at)
        {
            for (int i = 1; i < count; i++)
            {
                float kx = x[i], kz = z[i];
                int j = i - 1;
                while (j >= 0 && (x[j] > kx || (x[j] == kx && z[j] > kz)))
                {
                    x[j + 1] = x[j];
                    z[j + 1] = z[j];
                    j--;
                }
                x[j + 1] = kx;
                z[j + 1] = kz;
            }
            int n = 0;
            for (int i = 0; i < count; i++)   // the lower side, west to east
            {
                while (n >= 2 && Cross(hullX[at + n - 2], hullZ[at + n - 2], hullX[at + n - 1], hullZ[at + n - 1], x[i], z[i]) <= 0f)
                {
                    n--;
                }
                hullX[at + n] = x[i];
                hullZ[at + n] = z[i];
                n++;
            }
            int lower = n + 1;
            for (int i = count - 2; i >= 0; i--)   // the upper side, back again
            {
                while (n >= lower && Cross(hullX[at + n - 2], hullZ[at + n - 2], hullX[at + n - 1], hullZ[at + n - 1], x[i], z[i]) <= 0f)
                {
                    n--;
                }
                hullX[at + n] = x[i];
                hullZ[at + n] = z[i];
                n++;
            }
            return n - 1;   // the last is the first again
        }

        private static float Cross(float ax, float az, float bx, float bz, float cx, float cz)
        {
            return (bx - ax) * (cz - az) - (bz - az) * (cx - ax);
        }

        /// <summary>How far outside an anticlockwise outline a place is (metres); 0 on it or inside it.</summary>
        public static float Outside(float x, float z, float[] hullX, float[] hullZ, int start, int count)
        {
            bool inside = true;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                int a = start + i;
                int b = start + (i + 1) % count;
                float ex = hullX[b] - hullX[a];
                float ez = hullZ[b] - hullZ[a];
                float qx = x - hullX[a];
                float qz = z - hullZ[a];
                if (ex * qz - ez * qx < 0f)
                {
                    inside = false;
                }
                float lengthSqr = ex * ex + ez * ez;
                float t = lengthSqr > 0.000001f ? (qx * ex + qz * ez) / lengthSqr : 0f;
                t = t < 0f ? 0f : t > 1f ? 1f : t;
                float ox = qx - ex * t;
                float oz = qz - ez * t;
                best = Math.Min(best, ox * ox + oz * oz);
            }
            return inside && count >= 3 ? 0f : (float)Math.Sqrt(best);
        }

        /// <summary>
        /// Three places far from one another, for a check that must not depend on one corner of the city: the one
        /// furthest from the middle of them all, the one furthest from that, and the one furthest from both. Returns
        /// how many were picked (all of them if there are three or fewer).
        /// </summary>
        public static int FarApart(float[] x, float[] z, int count, int[] picked)
        {
            if (count <= 0)
            {
                return 0;
            }
            if (count <= 3)
            {
                for (int i = 0; i < count; i++)
                {
                    picked[i] = i;
                }
                return count;
            }
            double mx = 0.0, mz = 0.0;
            for (int i = 0; i < count; i++)
            {
                mx += x[i];
                mz += z[i];
            }
            mx /= count;
            mz /= count;
            int first = 0, second = 0, third = 0;
            double most = -1.0;
            for (int i = 0; i < count; i++)
            {
                double d = (x[i] - mx) * (x[i] - mx) + (z[i] - mz) * (z[i] - mz);
                if (d > most)
                {
                    most = d;
                    first = i;
                }
            }
            most = -1.0;
            for (int i = 0; i < count; i++)
            {
                double d = Sqr(x[i] - x[first], z[i] - z[first]);
                if (d > most)
                {
                    most = d;
                    second = i;
                }
            }
            most = -1.0;
            for (int i = 0; i < count; i++)
            {
                double d = Math.Min(Sqr(x[i] - x[first], z[i] - z[first]), Sqr(x[i] - x[second], z[i] - z[second]));
                if (d > most)
                {
                    most = d;
                    third = i;
                }
            }
            picked[0] = first;
            picked[1] = second;
            picked[2] = third;
            return 3;
        }

        private static double Sqr(double dx, double dz)
        {
            return dx * dx + dz * dz;
        }
    }

    /// <summary>
    /// What the base picture is drawn from, and what is kept on disk so that a city is measured once per version of
    /// the game: the outline of every building (an anticlockwise polygon in metres) and a coarse grid that says where
    /// water is. Some hundreds of outlines and a byte per 8 m square: about 150 KB. Written and read once per city load, never per frame.
    /// </summary>
    public sealed class NavBaseData
    {
        public const int Format = 1;
        private const int Magic = 0x424D4C52;   // "RLMB"

        public int Build;                              // the game's build number
        public float OriginX, OriginZ;                 // the world's corner the picture and the grid start at
        public float WorldWidth, WorldHeight;          // metres, as the road graph gave them
        public int Hulls;
        public int[] HullStart = new int[1];           // Hulls + 1 entries into HullX and HullZ
        public int[] HullOf = new int[0];              // which of the game's buildings (its place in the game's list) each outline is
        public float[] HullX = new float[0];
        public float[] HullZ = new float[0];
        public int Columns, Rows;
        public float Cell;                             // metres a side
        public byte[] Cells = new byte[0];             // see NavBaseWater

        /// <summary>The whole of it as bytes, with a sum at the end that tells a file cut short.</summary>
        public byte[] Encode()
        {
            using (var stream = new MemoryStream(64 + HullX.Length * 8 + Cells.Length))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(Format);
                writer.Write(Build);
                writer.Write(OriginX);
                writer.Write(OriginZ);
                writer.Write(WorldWidth);
                writer.Write(WorldHeight);
                writer.Write(Hulls);
                int corners = HullStart[Hulls];
                writer.Write(corners);
                for (int i = 0; i <= Hulls; i++)
                {
                    writer.Write(HullStart[i]);
                }
                for (int i = 0; i < Hulls; i++)
                {
                    writer.Write(HullOf[i]);
                }
                for (int i = 0; i < corners; i++)
                {
                    writer.Write(HullX[i]);
                    writer.Write(HullZ[i]);
                }
                writer.Write(Columns);
                writer.Write(Rows);
                writer.Write(Cell);
                writer.Write(Cells, 0, Columns * Rows);
                writer.Flush();
                byte[] body = stream.ToArray();
                var all = new byte[body.Length + 4];
                Buffer.BlockCopy(body, 0, all, 0, body.Length);
                uint sum = Sum(body, body.Length);
                all[body.Length] = (byte)sum;
                all[body.Length + 1] = (byte)(sum >> 8);
                all[body.Length + 2] = (byte)(sum >> 16);
                all[body.Length + 3] = (byte)(sum >> 24);
                return all;
            }
        }

        /// <summary>
        /// Reads it back. Null if the bytes are not such a file, are cut short or damaged, were made for another
        /// build of the game, or for a city whose roads lie elsewhere (more than half a metre): it is then made again.
        /// </summary>
        public static NavBaseData Decode(byte[] bytes, int build, float originX, float originZ, float worldWidth, float worldHeight)
        {
            if (bytes == null || bytes.Length < 56)
            {
                return null;
            }
            try
            {
                int length = bytes.Length - 4;
                uint sum = (uint)(bytes[length] | bytes[length + 1] << 8 | bytes[length + 2] << 16 | bytes[length + 3] << 24);
                if (sum != Sum(bytes, length))
                {
                    return null;
                }
                using (var stream = new MemoryStream(bytes, 0, length))
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadInt32() != Magic || reader.ReadInt32() != Format)
                    {
                        return null;
                    }
                    var data = new NavBaseData
                    {
                        Build = reader.ReadInt32(),
                        OriginX = reader.ReadSingle(),
                        OriginZ = reader.ReadSingle(),
                        WorldWidth = reader.ReadSingle(),
                        WorldHeight = reader.ReadSingle(),
                        Hulls = reader.ReadInt32()
                    };
                    int corners = reader.ReadInt32();
                    if (data.Build != build || Math.Abs(data.OriginX - originX) > 0.5f || Math.Abs(data.OriginZ - originZ) > 0.5f
                        || Math.Abs(data.WorldWidth - worldWidth) > 0.5f || Math.Abs(data.WorldHeight - worldHeight) > 0.5f
                        || data.Hulls < 0 || corners < 0 || (long)data.Hulls * 8 + (long)corners * 8 > length)
                    {
                        return null;
                    }
                    data.HullStart = new int[data.Hulls + 1];
                    for (int i = 0; i <= data.Hulls; i++)
                    {
                        data.HullStart[i] = reader.ReadInt32();
                        if (data.HullStart[i] < 0 || data.HullStart[i] > corners || (i > 0 && data.HullStart[i] < data.HullStart[i - 1]))
                        {
                            return null;
                        }
                    }
                    data.HullOf = new int[data.Hulls];
                    for (int i = 0; i < data.Hulls; i++)
                    {
                        data.HullOf[i] = reader.ReadInt32();
                    }
                    data.HullX = new float[corners];
                    data.HullZ = new float[corners];
                    for (int i = 0; i < corners; i++)
                    {
                        data.HullX[i] = reader.ReadSingle();
                        data.HullZ[i] = reader.ReadSingle();
                    }
                    data.Columns = reader.ReadInt32();
                    data.Rows = reader.ReadInt32();
                    data.Cell = reader.ReadSingle();
                    if (data.Columns <= 0 || data.Rows <= 0 || data.Cell <= 0f || (long)data.Columns * data.Rows != stream.Length - stream.Position)
                    {
                        return null;
                    }
                    data.Cells = reader.ReadBytes(data.Columns * data.Rows);
                    return data.Cells.Length == data.Columns * data.Rows ? data : null;
                }
            }
            catch (Exception)
            {
                return null;   // not a file of ours after all
            }
        }

        private static uint Sum(byte[] bytes, int length)
        {
            uint sum = 2166136261u;
            for (int i = 0; i < length; i++)
            {
                sum = (sum ^ bytes[i]) * 16777619u;
            }
            return sum;
        }
    }

    /// <summary>
    /// Where the water is. The game has no water object to ask (Leisure's fishing found the same), so the city is
    /// measured on a grid of 8 m squares: each square is ground the city stands on, or open (nothing to stand on
    /// there), and the water is the open squares that can be reached from the picture's edge without crossing ground.
    /// A courtyard or a lawn nobody walks on is open too, but roads and buildings shut it in, so it stays ground.
    /// Nothing here runs per frame: Classify once per square while a city is measured, Settle once, At while the picture is drawn.
    /// </summary>
    public static class NavBaseWater
    {
        public const float CellMetres = 8f;
        public const float StreetDrop = 0.8f;   // metres below the nearest road that are still the ground beside it (the fishing rule's figure)

        public const byte Land = 0;      // after Settle: not water
        public const byte Water = 1;     // after Settle: water
        public const byte Open = 2;      // before Settle: nothing to stand on here
        public const byte Named = 3;     // before Settle: the game calls what lies here water
        public const byte Unasked = 255; // before the city has been asked

        /// <summary>
        /// What one square is, from one look straight down at its middle. <paramref name="hit"/>: something solid was
        /// there. <paramref name="named"/>: the game's name for it says water. <paramref name="built"/>: it is a road
        /// or a building. <paramref name="walkable"/>: people can walk there (asked only when the others have not
        /// decided). <paramref name="streetY"/>: the height of the nearest road, or not a number if none is near.
        /// </summary>
        public static byte Classify(bool hit, bool named, bool built, bool walkable, float hitY, float streetY)
        {
            if (!hit)
            {
                return Open;
            }
            if (named)
            {
                return Named;
            }
            if (built || walkable || AtStreetLevel(hitY, streetY))
            {
                return Land;
            }
            return Open;
        }

        /// <summary>True for a surface no more than a step below the nearest road: the ground beside it, not a river bed.</summary>
        public static bool AtStreetLevel(float hitY, float streetY)
        {
            return !float.IsNaN(streetY) && hitY > streetY - StreetDrop;
        }

        /// <summary>The squares whose middle lies in a building are ground without asking; the rest are still to be asked. Returns how many were settled.</summary>
        public static int Prepare(NavBaseData data)
        {
            byte[] cells = data.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = Unasked;
            }
            int settled = 0;
            for (int h = 0; h < data.Hulls; h++)
            {
                int start = data.HullStart[h];
                int count = data.HullStart[h + 1] - start;
                if (count < 3)
                {
                    continue;
                }
                float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
                for (int i = start; i < start + count; i++)
                {
                    minX = Math.Min(minX, data.HullX[i]);
                    maxX = Math.Max(maxX, data.HullX[i]);
                    minZ = Math.Min(minZ, data.HullZ[i]);
                    maxZ = Math.Max(maxZ, data.HullZ[i]);
                }
                int c0 = Math.Max(0, (int)Math.Floor((minX - data.OriginX) / data.Cell));
                int c1 = Math.Min(data.Columns - 1, (int)Math.Floor((maxX - data.OriginX) / data.Cell));
                int r0 = Math.Max(0, (int)Math.Floor((minZ - data.OriginZ) / data.Cell));
                int r1 = Math.Min(data.Rows - 1, (int)Math.Floor((maxZ - data.OriginZ) / data.Cell));
                for (int row = r0; row <= r1; row++)
                {
                    for (int column = c0; column <= c1; column++)
                    {
                        int cell = row * data.Columns + column;
                        if (cells[cell] == Unasked
                            && NavBaseMath.Outside(data.OriginX + (column + 0.5f) * data.Cell, data.OriginZ + (row + 0.5f) * data.Cell, data.HullX, data.HullZ, start, count) <= 0f)
                        {
                            cells[cell] = Land;
                            settled++;
                        }
                    }
                }
            }
            return settled;
        }

        /// <summary>
        /// Decides: an open square is water if the picture's edge can be reached from it over open squares (step by
        /// step, not across corners); a square the game names water is water where it lies and floods nothing; every
        /// other square is ground. Afterwards the grid holds <see cref="Land"/> and <see cref="Water"/> only. Returns
        /// how many squares are water.
        /// </summary>
        public static int Settle(byte[] cells, int columns, int rows)
        {
            const byte Reached = 4;
            var queue = new int[cells.Length];
            int head = 0, tail = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    bool edge = row == 0 || column == 0 || row == rows - 1 || column == columns - 1;
                    int cell = row * columns + column;
                    if (edge && cells[cell] == Open)
                    {
                        cells[cell] = Reached;
                        queue[tail++] = cell;
                    }
                }
            }
            while (head < tail)
            {
                int cell = queue[head++];
                int column = cell % columns;
                int row = cell / columns;
                if (column > 0 && cells[cell - 1] == Open)
                {
                    cells[cell - 1] = Reached;
                    queue[tail++] = cell - 1;
                }
                if (column < columns - 1 && cells[cell + 1] == Open)
                {
                    cells[cell + 1] = Reached;
                    queue[tail++] = cell + 1;
                }
                if (row > 0 && cells[cell - columns] == Open)
                {
                    cells[cell - columns] = Reached;
                    queue[tail++] = cell - columns;
                }
                if (row < rows - 1 && cells[cell + columns] == Open)
                {
                    cells[cell + columns] = Reached;
                    queue[tail++] = cell + columns;
                }
            }
            int water = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                bool wet = cells[i] == Reached || cells[i] == Named;
                cells[i] = wet ? Water : Land;
                water += wet ? 1 : 0;
            }
            return water;
        }

        /// <summary>How much water there is at a place, 0 to 1, read smoothly between the middles of the settled squares (so a coast is a line, not steps).</summary>
        public static float At(NavBaseData data, float x, float z)
        {
            float gx = (x - data.OriginX) / data.Cell - 0.5f;
            float gz = (z - data.OriginZ) / data.Cell - 0.5f;
            int c0 = (int)Math.Floor(gx);
            int r0 = (int)Math.Floor(gz);
            float fx = gx - c0;
            float fz = gz - r0;
            int c1 = Clamp(c0 + 1, data.Columns - 1);
            int r1 = Clamp(r0 + 1, data.Rows - 1);
            c0 = Clamp(c0, data.Columns - 1);
            r0 = Clamp(r0, data.Rows - 1);
            byte[] cells = data.Cells;
            float low = cells[r0 * data.Columns + c0] * (1f - fx) + cells[r0 * data.Columns + c1] * fx;
            float high = cells[r1 * data.Columns + c0] * (1f - fx) + cells[r1 * data.Columns + c1] * fx;
            return low * (1f - fz) + high * fz;
        }

        private static int Clamp(int value, int most)
        {
            return value < 0 ? 0 : value > most ? most : value;
        }
    }

    /// <summary>
    /// The base picture as numbers: four bytes a pixel (red, green, blue, how solid), buildings in one flat colour
    /// over water in another, clear where there is neither (the minimap's ground tint shows through). It touches only
    /// the arrays it is given, so it runs away from the main thread, as the road picture's drawing does.
    /// </summary>
    public static class NavBaseRaster
    {
        /// <summary>
        /// Every building's outline into one byte a pixel (how much of the pixel it covers), with an edge one pixel
        /// soft. The picture's outermost pixels are left empty, so that looking past the edge shows no building drawn
        /// out. Returns how many outlines were drawn.
        /// </summary>
        public static int Buildings(byte[] cover, int width, int height, float originX, float originZ, float perMetre, NavBaseData data)
        {
            int drawn = 0;
            for (int h = 0; h < data.Hulls; h++)
            {
                int start = data.HullStart[h];
                int count = data.HullStart[h + 1] - start;
                if (count >= 3)
                {
                    Polygon(cover, width, height, originX, originZ, perMetre, data.HullX, data.HullZ, start, count);
                    drawn++;
                }
            }
            return drawn;
        }

        /// <summary>One anticlockwise convex outline (metres) into the cover.</summary>
        public static void Polygon(byte[] cover, int width, int height, float originX, float originZ, float perMetre, float[] x, float[] z, int start, int count)
        {
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            for (int i = start; i < start + count; i++)
            {
                float px = (x[i] - originX) * perMetre;
                float pz = (z[i] - originZ) * perMetre;
                minX = Math.Min(minX, px);
                maxX = Math.Max(maxX, px);
                minZ = Math.Min(minZ, pz);
                maxZ = Math.Max(maxZ, pz);
            }
            int x0 = Math.Max(1, (int)Math.Floor(minX - 1f));
            int x1 = Math.Min(width - 2, (int)Math.Ceiling(maxX + 1f));
            int y0 = Math.Max(1, (int)Math.Floor(minZ - 1f));
            int y1 = Math.Min(height - 2, (int)Math.Ceiling(maxZ + 1f));
            for (int py = y0; py <= y1; py++)
            {
                int row = py * width;
                float cz = py + 0.5f;
                for (int px = x0; px <= x1; px++)
                {
                    float cx = px + 0.5f;
                    float inside = float.MaxValue;   // pixels from the nearest edge, inwards
                    for (int i = 0; i < count; i++)
                    {
                        int a = start + i;
                        int b = start + (i + 1) % count;
                        float ax = (x[a] - originX) * perMetre;
                        float az = (z[a] - originZ) * perMetre;
                        float ex = (x[b] - originX) * perMetre - ax;
                        float ez = (z[b] - originZ) * perMetre - az;
                        float length = (float)Math.Sqrt(ex * ex + ez * ez);
                        if (length < 0.0001f)
                        {
                            continue;
                        }
                        float d = (ex * (cz - az) - ez * (cx - ax)) / length;
                        if (d < inside)
                        {
                            inside = d;
                            if (inside <= -0.5f)
                            {
                                break;
                            }
                        }
                    }
                    float share = inside + 0.5f;
                    if (share <= 0f || inside == float.MaxValue)
                    {
                        continue;
                    }
                    byte value = share >= 1f ? (byte)255 : (byte)(share * 255f);
                    if (value > cover[row + px])
                    {
                        cover[row + px] = value;
                    }
                }
            }
        }

        /// <summary>
        /// The picture itself from the buildings' cover and the settled water grid. Colours are red, green, blue and
        /// how solid, each 0 to 255. A clear pixel beside a coloured one takes that one's colour (still clear), so
        /// that the graphics card's smoothing between the two does not darken the edge.
        /// </summary>
        public static void Compose(byte[] rgba, byte[] cover, int width, int height, float originX, float originZ, float perMetre, NavBaseData data,
                                   byte buildingR, byte buildingG, byte buildingB, byte buildingA, byte waterR, byte waterG, byte waterB, byte waterA)
        {
            float cellPixels = data.Cell * perMetre;
            bool anyWater = false;
            for (int i = 0; i < data.Cells.Length && !anyWater; i++)
            {
                anyWater = data.Cells[i] == NavBaseWater.Water;
            }
            for (int py = 0; py < height; py++)
            {
                float z = originZ + (py + 0.5f) / perMetre;
                for (int px = 0; px < width; px++)
                {
                    int at = py * width + px;
                    float building = cover[at] * (buildingA / 65025f);   // 255 x 255
                    float water = 0f;
                    if (anyWater)
                    {
                        float share = (NavBaseWater.At(data, originX + (px + 0.5f) / perMetre, z) - 0.5f) * cellPixels + 0.5f;
                        water = share <= 0f ? 0f : (share >= 1f ? 1f : share) * (waterA / 255f) * (1f - building);
                    }
                    float solid = building + water;
                    int o = at * 4;
                    if (solid <= 0.002f)
                    {
                        rgba[o] = buildingR;
                        rgba[o + 1] = buildingG;
                        rgba[o + 2] = buildingB;
                        rgba[o + 3] = 0;
                        continue;
                    }
                    rgba[o] = (byte)((buildingR * building + waterR * water) / solid + 0.5f);
                    rgba[o + 1] = (byte)((buildingG * building + waterG * water) / solid + 0.5f);
                    rgba[o + 2] = (byte)((buildingB * building + waterB * water) / solid + 0.5f);
                    rgba[o + 3] = (byte)(solid * 255f + 0.5f);
                }
            }
            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    int o = (py * width + px) * 4;
                    if (rgba[o + 3] != 0)
                    {
                        continue;
                    }
                    int from = -1;
                    byte most = 0;
                    if (px > 0 && rgba[o - 4 + 3] > most)
                    {
                        most = rgba[o - 4 + 3];
                        from = o - 4;
                    }
                    if (px < width - 1 && rgba[o + 4 + 3] > most)
                    {
                        most = rgba[o + 4 + 3];
                        from = o + 4;
                    }
                    if (py > 0 && rgba[o - width * 4 + 3] > most)
                    {
                        most = rgba[o - width * 4 + 3];
                        from = o - width * 4;
                    }
                    if (py < height - 1 && rgba[o + width * 4 + 3] > most)
                    {
                        from = o + width * 4;
                    }
                    if (from >= 0)
                    {
                        rgba[o] = rgba[from];
                        rgba[o + 1] = rgba[from + 1];
                        rgba[o + 2] = rgba[from + 2];
                    }
                }
            }
        }
    }
}
