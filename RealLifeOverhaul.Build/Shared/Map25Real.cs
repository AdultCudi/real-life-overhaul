using System;
using System.Collections.Generic;
using System.IO;

namespace RealLifeOverhaul.Map25
{
    /// <summary>
    /// The owner's own city for the previews, read from the file the minimap keeps (minimap_base_*.bin, format 2 or 3: every building's
    /// outline and the water grid), so what the previews draw has the real grain: 883 footprints, slices 8 to 14 m wide round courtyards,
    /// a dense district to the north east and sparse boxes elsewhere. What the file does not hold is made up, and says so: the heights
    /// (a model, <see cref="Map25Fixture.ModelHeight"/>, since the game measures them only in the game), the roads (the gaps that run clear
    /// through the buildings of a window are avenues and streets, with lanes filling them) and the trees (street trees along those roads, and
    /// one park in the largest hole of the buildings). Preview and checks only: never part of the mod.
    /// </summary>
    public sealed class Map25Real
    {
        public Map25City City;
        public byte[] Cells;
        public int Columns, Rows;
        public float Cell, OriginX, OriginZ;
        public readonly List<float> Avenues = new List<float>(), AvenueWidth = new List<float>(), Streets = new List<float>(), StreetWidth = new List<float>();
        public float WindowX0, WindowZ0, WindowX1, WindowZ1;

        /// <summary>Reads the file; null when it is not a base file of format 2 or 3 or is cut short.</summary>
        public static Map25Real Load(string path, bool tall)
        {
            byte[] bytes = File.ReadAllBytes(path);
            using (var reader = new BinaryReader(new MemoryStream(bytes)))
            {
                if (reader.ReadInt32() != 0x424D4C52)
                {
                    return null;
                }
                int format = reader.ReadInt32();
                if (format != 2 && format != 3)
                {
                    return null;
                }
                reader.ReadInt32();                       // the game's build
                float originX = reader.ReadSingle(), originZ = reader.ReadSingle();   // where the water grid starts
                reader.ReadSingle(); reader.ReadSingle(); // the picture's size
                int hulls = reader.ReadInt32(), corners = reader.ReadInt32();
                var start = new int[hulls + 1];
                for (int i = 0; i <= hulls; i++)
                {
                    start[i] = reader.ReadInt32();
                }
                var key = new long[hulls];
                for (int i = 0; i < hulls; i++)
                {
                    key[i] = reader.ReadInt64();
                }
                var height = new float[hulls];
                if (format >= 3)
                {
                    for (int i = 0; i < hulls; i++)
                    {
                        reader.ReadSingle();
                        height[i] = reader.ReadSingle();
                    }
                }
                var x = new float[corners];
                var z = new float[corners];
                for (int i = 0; i < corners; i++)
                {
                    x[i] = reader.ReadSingle();
                    z[i] = reader.ReadSingle();
                }
                var real = new Map25Real { OriginX = originX, OriginZ = originZ, Columns = reader.ReadInt32(), Rows = reader.ReadInt32(), Cell = reader.ReadSingle() };
                real.Cells = reader.ReadBytes(real.Columns * real.Rows);
                bytes = null;
                var city = new Map25City();
                city.Reserve(hulls, corners);
                Array.Copy(x, city.HullX, corners);
                Array.Copy(z, city.HullZ, corners);
                Array.Copy(start, city.HullStart, hulls + 1);
                for (int i = 0; i < hulls; i++)
                {
                    float cx = 0f, cz = 0f;
                    for (int k = start[i]; k < start[i + 1]; k++)
                    {
                        cx += x[k];
                        cz += z[k];
                    }
                    cx /= start[i + 1] - start[i];
                    cz /= start[i + 1] - start[i];
                    float core = (float)Math.Exp(-(((cx + 80f) / 520f) * ((cx + 80f) / 520f) + ((cz - 20f) / 430f) * ((cz - 20f) / 430f)));
                    city.Key[i] = key[i] != 0L ? key[i] : 0x1000L + i;
                    city.Height[i] = Map25Fixture.ModelHeight((uint)(key[i] ^ (key[i] >> 32)) + (uint)i * 7919u, core, tall);
                }
                city.Compress();
                city.FindBounds();
                real.City = city;
                return real;
            }
        }

        /// <summary>
        /// Finds the avenues and streets of a window of the city: the places where a clear strip at least 9 m wide runs through every building of the
        /// window (a building is of the window when its middle is). Their middles and widths go into <see cref="Avenues"/> and <see cref="Streets"/>.
        /// </summary>
        public void FindGrid(float x0, float z0, float x1, float z1)
        {
            WindowX0 = x0;
            WindowZ0 = z0;
            WindowX1 = x1;
            WindowZ1 = z1;
            Avenues.Clear();
            AvenueWidth.Clear();
            Streets.Clear();
            StreetWidth.Clear();
            var coverX = new int[(int)(x1 - x0) + 2];
            var coverZ = new int[(int)(z1 - z0) + 2];
            for (int i = 0; i < City.Count; i++)
            {
                int s = City.HullStart[i], e = City.HullStart[i + 1];
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                for (int k = s; k < e; k++)
                {
                    minX = Math.Min(minX, City.HullX[k]);
                    maxX = Math.Max(maxX, City.HullX[k]);
                    minZ = Math.Min(minZ, City.HullZ[k]);
                    maxZ = Math.Max(maxZ, City.HullZ[k]);
                }
                float cx = (minX + maxX) * 0.5f, cz = (minZ + maxZ) * 0.5f;
                if (cx < x0 || cx > x1 || cz < z0 || cz > z1)
                {
                    continue;
                }
                for (int a = Math.Max(0, (int)(minX - x0)); a <= Math.Min(coverX.Length - 1, (int)(maxX - x0)); a++)
                {
                    coverX[a]++;
                }
                for (int a = Math.Max(0, (int)(minZ - z0)); a <= Math.Min(coverZ.Length - 1, (int)(maxZ - z0)); a++)
                {
                    coverZ[a]++;
                }
            }
            Gaps(coverX, x0, Avenues, AvenueWidth);
            Gaps(coverZ, z0, Streets, StreetWidth);
        }

        private static void Gaps(int[] cover, float origin, List<float> middles, List<float> widths)
        {
            int i = 0;
            while (i < cover.Length)
            {
                if (cover[i] != 0)
                {
                    i++;
                    continue;
                }
                int j = i;
                while (j < cover.Length && cover[j] == 0)
                {
                    j++;
                }
                if (i > 0 && j < cover.Length && j - i >= 9)
                {
                    middles.Add(origin + (i + j) * 0.5f);
                    widths.Add(j - i);
                }
                i = j;
            }
        }

        /// <summary>The lanes of the grid found: every avenue and street the window has, lanes 3.3 m apart filling it, a point every 10 m, both ways.</summary>
        public void Roads(out float[] x, out float[] y, out float[] z, out int[] edgeStart, out int[] edgeTo, out float[] edgeExtra, out bool[] usable, out float[] speed, out int count)
        {
            var px = new List<float>(16384);
            var pz = new List<float>(16384);
            var sp = new List<float>(16384);
            var chains = new List<int[]>();
            for (int a = 0; a < Avenues.Count; a++)
            {
                int lanes = Math.Max(2, (int)Math.Round(AvenueWidth[a] / 3.3f));
                for (int lane = 0; lane < lanes; lane++)
                {
                    float lx = Avenues[a] + (lane - (lanes - 1) * 0.5f) * 3.3f;
                    Chain(px, pz, sp, chains, lx, WindowZ0 - 40f, lx, WindowZ1 + 40f, lane < lanes / 2 ? -1 : 1, 14f);
                }
            }
            for (int s = 0; s < Streets.Count; s++)
            {
                int lanes = Math.Max(2, (int)Math.Round(StreetWidth[s] / 3.3f));
                for (int lane = 0; lane < lanes; lane++)
                {
                    float lz = Streets[s] + (lane - (lanes - 1) * 0.5f) * 3.3f;
                    Chain(px, pz, sp, chains, WindowX0 - 40f, lz, WindowX1 + 40f, lz, lane < lanes / 2 ? -1 : 1, 9f);
                }
            }
            count = px.Count;
            x = px.ToArray();
            z = pz.ToArray();
            y = new float[count];
            speed = sp.ToArray();
            usable = new bool[count];
            var from = new List<int>(count * 2);
            var to = new List<int>(count * 2);
            foreach (int[] chain in chains)
            {
                for (int k = 0; k + 1 < chain.Length; k++)
                {
                    from.Add(chain[k]);
                    to.Add(chain[k + 1]);
                    usable[chain[k]] = usable[chain[k + 1]] = true;
                }
            }
            edgeStart = new int[count + 1];
            for (int e = 0; e < from.Count; e++)
            {
                edgeStart[from[e] + 1]++;
            }
            for (int i = 0; i < count; i++)
            {
                edgeStart[i + 1] += edgeStart[i];
            }
            edgeTo = new int[from.Count];
            edgeExtra = new float[from.Count];
            var fill = new int[count];
            for (int e = 0; e < from.Count; e++)
            {
                edgeTo[edgeStart[from[e]] + fill[from[e]]++] = to[e];
            }
        }

        private static void Chain(List<float> px, List<float> pz, List<float> sp, List<int[]> chains, float x0, float z0, float x1, float z1, int direction, float speed)
        {
            float l = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0));
            int steps = Math.Max(1, (int)(l / 10f));
            var ids = new int[steps + 1];
            for (int s = 0; s <= steps; s++)
            {
                int at = direction > 0 ? s : steps - s;
                ids[s] = px.Count;
                px.Add(x0 + (x1 - x0) * at / steps);
                pz.Add(z0 + (z1 - z0) * at / steps);
                sp.Add(speed);
            }
            chains.Add(ids);
        }

        /// <summary>
        /// Trees: a row along each avenue of the window every 14 m on both sides, and a park of a tree to every 70 square metres (150 to 900) in the largest hole between the buildings that
        /// is not a road (found on an 8 m grid, at least 48 by 48 m). <paramref name="park"/> is that hole's middle, or the window's if none was found.
        /// </summary>
        public void Trees(out float[] x, out float[] z, out int count, out float parkX, out float parkZ)
        {
            var tx = new List<float>(2048);
            var tz = new List<float>(2048);
            var rng = new Map25Rng(91);
            for (int a = 0; a < Avenues.Count; a++)
            {
                for (float zz = WindowZ0; zz < WindowZ1; zz += 14f)
                {
                    tx.Add(Avenues[a] - AvenueWidth[a] * 0.5f - 1.5f);
                    tz.Add(zz + rng.Range(-1.5f, 1.5f));
                    tx.Add(Avenues[a] + AvenueWidth[a] * 0.5f + 1.5f);
                    tz.Add(zz + rng.Range(-1.5f, 1.5f));
                }
            }
            const float g = 8f;
            int columns = (int)((WindowX1 - WindowX0) / g) + 1, rows = (int)((WindowZ1 - WindowZ0) / g) + 1;
            var taken = new bool[columns * rows];
            for (int i = 0; i < City.Count; i++)
            {
                int s = City.HullStart[i], e = City.HullStart[i + 1];
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                for (int k = s; k < e; k++)
                {
                    minX = Math.Min(minX, City.HullX[k]);
                    maxX = Math.Max(maxX, City.HullX[k]);
                    minZ = Math.Min(minZ, City.HullZ[k]);
                    maxZ = Math.Max(maxZ, City.HullZ[k]);
                }
                for (int r = Math.Max(0, (int)((minZ - WindowZ0) / g)); r <= Math.Min(rows - 1, (int)((maxZ - WindowZ0) / g)); r++)
                {
                    for (int c = Math.Max(0, (int)((minX - WindowX0) / g)); c <= Math.Min(columns - 1, (int)((maxX - WindowX0) / g)); c++)
                    {
                        taken[r * columns + c] = true;
                    }
                }
            }
            for (int a = 0; a < Avenues.Count; a++)
            {
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < columns; c++)
                    {
                        if (Math.Abs(WindowX0 + (c + 0.5f) * g - Avenues[a]) < AvenueWidth[a] * 0.5f + g)
                        {
                            taken[r * columns + c] = true;
                        }
                    }
                }
            }
            for (int s = 0; s < Streets.Count; s++)
            {
                for (int r = 0; r < rows; r++)
                {
                    if (Math.Abs(WindowZ0 + (r + 0.5f) * g - Streets[s]) < StreetWidth[s] * 0.5f + g)
                    {
                        for (int c = 0; c < columns; c++)
                        {
                            taken[r * columns + c] = true;
                        }
                    }
                }
            }
            // the largest empty rectangle by the histogram of free squares, rows upward
            var heights = new int[columns];
            int bestArea = 0, bx = 0, bz = 0, bw = 0, bh = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    heights[c] = taken[r * columns + c] ? 0 : heights[c] + 1;
                }
                for (int c = 0; c < columns; c++)
                {
                    int minHeight = int.MaxValue;
                    for (int d = c; d < columns && heights[d] > 0; d++)
                    {
                        minHeight = Math.Min(minHeight, heights[d]);
                        int area = (d - c + 1) * minHeight;
                        if (area > bestArea)
                        {
                            bestArea = area;
                            bx = c;
                            bz = r - minHeight + 1;
                            bw = d - c + 1;
                            bh = minHeight;
                        }
                    }
                }
            }
            parkX = (WindowX0 + WindowX1) * 0.5f;
            parkZ = (WindowZ0 + WindowZ1) * 0.5f;
            if (bw >= 6 && bh >= 6)
            {
                float x0 = WindowX0 + bx * g + 3f, x1 = WindowX0 + (bx + bw) * g - 3f, z0 = WindowZ0 + bz * g + 3f, z1 = WindowZ0 + (bz + bh) * g - 3f;
                parkX = (x0 + x1) * 0.5f;
                parkZ = (z0 + z1) * 0.5f;
                int park = Math.Max(150, Math.Min(900, (int)((x1 - x0) * (z1 - z0) / 70f)));   // a tree to every 70 square metres of it, at the most 900
                for (int k = 0; k < park; k++)
                {
                    tx.Add(rng.Range(x0, x1));
                    tz.Add(rng.Range(z0, z1));
                }
            }
            x = tx.ToArray();
            z = tz.ToArray();
            count = x.Length;
        }
    }
}
