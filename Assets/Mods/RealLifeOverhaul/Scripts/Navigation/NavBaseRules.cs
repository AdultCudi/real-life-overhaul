using System;
using System.Collections.Generic;
using System.IO;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The arithmetic of the minimap's base picture, the buildings and water under the roads; game-free and checked by
    /// NavCheck. It covers the road picture's piece of the world from the same origin, so one conversion serves both: a
    /// pixel is (world - origin) x pixels per metre from the lower left corner, x east and z north.
    /// </summary>
    public static class NavBaseMath
    {
        public const int LongestSide = 2048;      // pixels: 3.6 km of city at 1.77 m a pixel
        public const float MostPerMetre = 1.25f;  // pixels a metre in a small city, as the road picture has

        public static float ToPixel(float world, float origin, float perMetre)
        {
            return (world - origin) * perMetre;
        }

        public static float ToWorld(float pixel, float origin, float perMetre)
        {
            return origin + pixel / perMetre;
        }

        /// <summary>The pixel the graphics card reads for a place: the one it lies in, or the edge's outside the picture.</summary>
        public static int Texel(float pixel, int size)
        {
            int index = (int)Math.Floor(pixel);
            return index < 0 ? 0 : index > size - 1 ? size - 1 : index;
        }

        /// <summary>The opposite of <see cref="NavView.ToView"/>: the place in the world drawn at a point of the view.</summary>
        public static void FromView(float vx, float vy, float px, float pz, float turn, float unitsPerMetre, out float wx, out float wz)
        {
            double radians = turn * Math.PI / 180.0;
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            wx = px + (vx * cos + vy * sin) / unitsPerMetre;
            wz = pz + (-vx * sin + vy * cos) / unitsPerMetre;
        }

        /// <summary>The share of a window (texture coordinates: corner and size) that lies on the picture itself.</summary>
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

        /// <summary>Metres between where the base picture and the markers' layer draw one place; both turn together.</summary>
        public static float Misfit(float wx, float wz, float px, float pz, float originX, float originZ, float worldWidth, float worldHeight, float metres)
        {
            NavView.Window(px, pz, originX, originZ, worldWidth, worldHeight, metres, out float u, out float v, out float width, out float height);
            float byPictureX = (((wx - originX) / worldWidth - u) / width - 0.5f) * metres;
            float byPictureZ = (((wz - originZ) / worldHeight - v) / height - 0.5f) * metres;
            float dx = byPictureX - (wx - px);
            float dz = byPictureZ - (wz - pz);
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>The convex hull, anticlockwise, from <paramref name="at"/> on, sorting the caller's points; fewer than 3 corners for a line.</summary>
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

        /// <summary>Three places far apart, so a check does not hang on one corner of the city; returns how many were picked.</summary>
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

        /// <summary>
        /// A building's name in the kept file: 64-bit FNV-1a over its street's name and number, which the game keeps for a building
        /// for good (its dictionary of the city's buildings is by address). Never its place in CityManager's list: the game fills that list
        /// with FindObjectsByType(None), whose order it does not keep from one load to the next. 0 for no address; no real building gets 0.
        /// </summary>
        public static long BuildingKey(string street, int number)
        {
            if (string.IsNullOrEmpty(street))
            {
                return 0L;
            }
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                for (int i = 0; i < street.Length; i++)
                {
                    hash = (hash ^ street[i] & 0xFFu) * 1099511628211UL;
                    hash = (hash ^ (uint)street[i] >> 8) * 1099511628211UL;
                }
                hash = (hash ^ 0x7Cu) * 1099511628211UL;
                for (int shift = 0; shift < 32; shift += 8)
                {
                    hash = (hash ^ ((uint)number >> shift & 0xFFu)) * 1099511628211UL;
                }
                return hash == 0UL ? 1L : (long)hash;
            }
        }

        /// <summary>
        /// Finds each kept outline's building in the game's list as it stands now: <paramref name="found"/>[h] is the place in
        /// <paramref name="liveKey"/> (the keys of the list, 0 for a building to leave out) of outline h's building, or -1 when there is
        /// none or the address is not one building's alone (two outlines or two buildings with it). Returns how many outlines found theirs.
        /// Run for a check or a test, never a frame.
        /// </summary>
        public static int Resolve(long[] hullKey, int hulls, long[] liveKey, int live, int[] found)
        {
            var at = new Dictionary<long, int>(live);
            for (int i = 0; i < live; i++)
            {
                if (liveKey[i] != 0L)
                {
                    at[liveKey[i]] = at.ContainsKey(liveKey[i]) ? -1 : i;
                }
            }
            var seen = new Dictionary<long, int>(hulls);
            for (int h = 0; h < hulls; h++)
            {
                seen[hullKey[h]] = seen.TryGetValue(hullKey[h], out int times) ? times + 1 : 1;
            }
            int matched = 0;
            for (int h = 0; h < hulls; h++)
            {
                found[h] = hullKey[h] != 0L && seen[hullKey[h]] == 1 && at.TryGetValue(hullKey[h], out int place) ? place : -1;
                matched += found[h] >= 0 ? 1 : 0;
            }
            return matched;
        }

        /// <summary>
        /// Three places far apart among those that <paramref name="known"/>, as <see cref="FarApart"/> picks among all; the picks are
        /// places in the arrays given. Returns how many were picked (none when none is known).
        /// </summary>
        public static int FarApartKnown(float[] x, float[] z, bool[] known, int count, int[] picked)
        {
            var place = new int[count];
            var kx = new float[count];
            var kz = new float[count];
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                if (known[i])
                {
                    place[n] = i;
                    kx[n] = x[i];
                    kz[n] = z[i];
                    n++;
                }
            }
            int got = FarApart(kx, kz, n, picked);
            for (int i = 0; i < got; i++)
            {
                picked[i] = place[picked[i]];
            }
            return got;
        }

        /// <summary>
        /// Of the picked outlines, how far outside its own outline the door furthest from it lies (metres), and which pick that is
        /// (<paramref name="worst"/>, a place in <paramref name="picked"/>; -1 with none), so a check can name the building and its door.
        /// </summary>
        public static float WorstOutside(float[] doorX, float[] doorZ, NavBaseData data, int[] picked, int count, out int worst)
        {
            worst = -1;
            float most = 0f;
            for (int i = 0; i < count; i++)
            {
                int h = picked[i];
                float outside = Outside(doorX[h], doorZ[h], data.HullX, data.HullZ, data.HullStart[h], data.HullStart[h + 1] - data.HullStart[h]);
                if (worst < 0 || outside > most)
                {
                    most = outside;
                    worst = i;
                }
            }
            return most;
        }
    }

    /// <summary>
    /// What the base picture is drawn from, kept on disk so a city is measured once per version of the game: every
    /// building's outline (anticlockwise, in metres) and a grid of where water is, about 150 KB. Written and read once per
    /// city load.
    /// </summary>
    public sealed class NavBaseData
    {
        public const int Format = 2;   // 2: each outline's building is named by its address, not by its place in the game's list (which the game does not keep from one load to the next)
        private const int Magic = 0x424D4C52;   // "RLMB"

        public int Build;                              // the game's build number
        public float OriginX, OriginZ;                 // the world's corner where the picture and the grid start
        public float WorldWidth, WorldHeight;          // metres, as the road graph gave them
        public int Hulls;
        public int[] HullStart = new int[1];           // Hulls + 1 entries into HullX and HullZ
        public long[] HullKey = new long[0];           // each outline's building, by its address (NavBaseMath.BuildingKey); 0 for none
        public float[] HullX = new float[0];
        public float[] HullZ = new float[0];
        public int Columns, Rows;
        public float Cell;                             // metres a side
        public byte[] Cells = new byte[0];             // see NavBaseWater

        /// <summary>
        /// This city with more outlines on top (the buildings the mod stands in it), for drawing only: the same squares of
        /// water, the outlines appended with no building of the game's list (key 0). Itself when there are none.
        /// </summary>
        public NavBaseData With(List<float> moreX, List<float> moreZ, List<int> moreStart)
        {
            int more = moreStart != null ? moreStart.Count : 0;
            if (more == 0)
            {
                return this;
            }
            int corners = HullStart[Hulls];
            var drawn = new NavBaseData
            {
                Build = Build, OriginX = OriginX, OriginZ = OriginZ, WorldWidth = WorldWidth, WorldHeight = WorldHeight, Columns = Columns, Rows = Rows, Cell = Cell,
                Cells = Cells, Hulls = Hulls + more, HullStart = new int[Hulls + more + 1], HullKey = new long[Hulls + more],
                HullX = new float[corners + moreX.Count], HullZ = new float[corners + moreZ.Count]
            };
            Array.Copy(HullStart, drawn.HullStart, Hulls + 1);
            Array.Copy(HullKey, drawn.HullKey, Hulls);
            Array.Copy(HullX, drawn.HullX, corners);
            Array.Copy(HullZ, drawn.HullZ, corners);
            for (int i = 0; i < more; i++)
            {
                drawn.HullKey[Hulls + i] = 0;
                drawn.HullStart[Hulls + i] = corners + moreStart[i];
            }
            drawn.HullStart[Hulls + more] = corners + moreX.Count;
            for (int i = 0; i < moreX.Count; i++)
            {
                drawn.HullX[corners + i] = moreX[i];
                drawn.HullZ[corners + i] = moreZ[i];
            }
            return drawn;
        }

        /// <summary>Everything a picture is drawn from, into the print the base picture is kept by across loads.</summary>
        public void AddTo(ref InputPrint print)
        {
            print.Add(Build);
            print.Add(OriginX);
            print.Add(OriginZ);
            print.Add(WorldWidth);
            print.Add(WorldHeight);
            print.Add(Hulls);
            print.Add(HullStart);
            print.Add(HullKey.Length);
            for (int i = 0; i < HullKey.Length; i++)
            {
                print.Add((int)HullKey[i]);
                print.Add((int)(HullKey[i] >> 32));
            }
            print.Add(HullX);
            print.Add(HullZ);
            print.Add(Columns);
            print.Add(Rows);
            print.Add(Cell);
            print.Add(Cells);
        }

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
                    writer.Write(HullKey[i]);
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

        /// <summary>Reads it back; null, so it is made again, if damaged or for another build or a city whose roads lie elsewhere.</summary>
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
                        || data.Hulls < 0 || corners < 0 || (long)data.Hulls * 12 + (long)corners * 8 > length)
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
                    data.HullKey = new long[data.Hulls];
                    for (int i = 0; i < data.Hulls; i++)
                    {
                        data.HullKey[i] = reader.ReadInt64();
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
    /// Where the water is. The game has no water object to ask, so the city is measured on a grid of squares, each ground
    /// or open, and the water is the open squares reached from the picture's edge without crossing ground: a courtyard
    /// shut in by roads and buildings stays ground. Run while a city is measured and its picture drawn, never per frame.
    /// </summary>
    public static class NavBaseWater
    {
        public const float CellMetres = 8f;     // metres a side
        public const float StreetDrop = 0.8f;   // metres below the nearest road still the ground beside it, as fishing has it

        public const byte Land = 0;      // after Settle: not water
        public const byte Water = 1;     // after Settle: water
        public const byte Open = 2;      // before Settle: nothing to stand on here
        public const byte Named = 3;     // before Settle: the game calls what lies here water
        public const byte Unasked = 255; // before the city has been asked

        /// <summary>What one square is, from one look straight down at its middle; <paramref name="streetY"/> is NaN with no road near.</summary>
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

        /// <summary>Makes ground the squares whose middle lies in a building, the rest still to be asked. Returns how many.</summary>
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

        /// <summary>Open squares that reach the edge, not across corners, and those the game names are water, the rest ground; returns the water.</summary>
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

        /// <summary>Water at a place, 0 to 1, read smoothly between the squares' middles so a coast is a line, not steps.</summary>
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
}
