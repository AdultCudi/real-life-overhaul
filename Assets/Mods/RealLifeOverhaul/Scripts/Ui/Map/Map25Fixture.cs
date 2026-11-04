using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Map25
{
    /// <summary>
    /// A made-up city of the size and the grain of the game's (883 buildings, 4.0 by 2.5 km of world with a sea along two sides,
    /// avenues 250 m apart and streets 92 m, lots 18 to 40 m wide), for the checks and for the previews that are drawn outside the
    /// game. It is not the game's city and never ships a pixel of any screen: the game's own footprints come from Navigation/NavBase.
    /// Deterministic from its seed. Game-free.
    /// </summary>
    public static class Map25Fixture
    {
        public const float AvenueGap = 250f, StreetGap = 92f, AvenueWidth = 30f, StreetWidth = 20f;
        public const int Avenues = 9, Streets = 8;

        public sealed class Poi
        {
            public float X, Z;
            public int Building;
            public int Kind;           // 0 yours, 1 rival, 2 for rent, 3 job, 4 destination
            public string Name, Sub;
        }

        public static readonly List<Poi> Pois = new List<Poi>();

        /// <summary>The route the previews follow: north along an avenue, east along a street, north again, west to the door.</summary>
        public static readonly float[] RouteX, RouteZ;

        static Map25Fixture()
        {
            var x = new List<float>();
            var z = new List<float>();
            AddLine(x, z, 0f, -240f, 0f, 0f);
            AddLine(x, z, 0f, 0f, AvenueGap, 0f);
            AddLine(x, z, AvenueGap, 0f, AvenueGap, StreetGap);
            AddLine(x, z, AvenueGap, StreetGap, 100f, StreetGap);
            RouteX = x.ToArray();
            RouteZ = z.ToArray();
        }

        private static void AddLine(List<float> x, List<float> z, float x0, float z0, float x1, float z1)
        {
            float l = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0));
            int steps = Math.Max(1, (int)(l / 12f));
            for (int s = (x.Count == 0 ? 0 : 1); s <= steps; s++)
            {
                x.Add(x0 + (x1 - x0) * s / steps);
                z.Add(z0 + (z1 - z0) * s / steps);
            }
        }

        public static void AvenueX(int i, out float x)
        {
            x = (i - Avenues / 2) * AvenueGap;
        }

        /// <summary>The buildings: every lot of every block, in two rows a block, with heights tallest near the middle.</summary>
        public static Map25City City(int seed)
        {
            var hx = new List<float>(4096);
            var hz = new List<float>(4096);
            var start = new List<int> { 0 };
            var height = new List<float>(1024);
            var key = new List<long>(1024);
            var rng = new Map25Rng((uint)seed);
            for (int i = 0; i < Avenues - 1; i++)
            {
                for (int j = 0; j < Streets - 1; j++)
                {
                    float x0 = (i - Avenues / 2) * AvenueGap + AvenueWidth * 0.5f, x1 = (i + 1 - Avenues / 2) * AvenueGap - AvenueWidth * 0.5f;
                    float z0 = (j - Streets / 2) * StreetGap + StreetWidth * 0.5f, z1 = (j + 1 - Streets / 2) * StreetGap - StreetWidth * 0.5f;
                    float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
                    if (i == 3 && j == 3)
                    {
                        continue;   // a park
                    }
                    float core = (float)Math.Exp(-((cx / 700f) * (cx / 700f) + (cz / 220f) * (cz / 220f)));
                    for (int row = 0; row < 2; row++)
                    {
                        float za = row == 0 ? z0 + 1f : (z0 + z1) * 0.5f + 1f, zb = row == 0 ? (z0 + z1) * 0.5f - 1f : z1 - 1f;
                        float x = x0 + 1f;
                        while (x < x1 - 14f)
                        {
                            float w = Math.Min(x1 - 1f - x, rng.Range(18f, 40f));
                            if (x1 - 1f - (x + w) < 14f)
                            {
                                w = x1 - 1f - x;
                            }
                            float r = rng.Float();
                            float h = r < 0.10f + 0.28f * core ? rng.Range(55f, 140f) : r < 0.52f ? rng.Range(24f, 55f) : rng.Range(9f, 24f);
                            // anticlockwise from above: x east, z north
                            hx.Add(x); hz.Add(za);
                            hx.Add(x + w); hz.Add(za);
                            hx.Add(x + w); hz.Add(zb);
                            hx.Add(x); hz.Add(zb);
                            start.Add(hx.Count);
                            height.Add(h);
                            key.Add(0L);
                            x += w + 2f;
                        }
                    }
                }
            }
            var city = new Map25City();
            city.Reserve(height.Count, hx.Count);
            for (int i = 0; i < hx.Count; i++)
            {
                city.HullX[i] = hx[i];
                city.HullZ[i] = hz[i];
            }
            for (int i = 0; i < start.Count; i++)
            {
                city.HullStart[i] = start[i];
            }
            for (int i = 0; i < height.Count; i++)
            {
                city.Height[i] = height[i];
                city.Key[i] = 0x1000L + i;
            }
            city.FindBounds();
            return city;
        }

        /// <summary>The lanes: two each way along every avenue and street, a point every 10 m, edges both ways, none that is a change of lane.</summary>
        public static void Roads(out float[] x, out float[] y, out float[] z, out int[] edgeStart, out int[] edgeTo, out float[] edgeExtra, out bool[] usable, out float[] speed, out int count)
        {
            var px = new List<float>(8192);
            var pz = new List<float>(8192);
            var sp = new List<float>(8192);
            var chains = new List<int[]>();
            float minX = (-Avenues / 2) * AvenueGap - 200f, maxX = (Avenues - Avenues / 2) * AvenueGap + 200f;
            float minZ = (-Streets / 2) * StreetGap - 200f, maxZ = (Streets - Streets / 2) * StreetGap + 200f;
            for (int i = 0; i <= Avenues; i++)
            {
                float cx = (i - Avenues / 2) * AvenueGap;
                for (int lane = -2; lane <= 1; lane++)
                {
                    float lx = cx + (lane + 0.5f) * 3.4f;
                    Chain(px, pz, sp, chains, lx, minZ, lx, maxZ, lane < 0 ? -1 : 1, 14f);
                }
            }
            for (int j = 0; j <= Streets; j++)
            {
                float cz = (j - Streets / 2) * StreetGap;
                for (int lane = -1; lane <= 0; lane++)
                {
                    float lz = cz + (lane + 0.5f) * 3.4f;
                    Chain(px, pz, sp, chains, minX, lz, maxX, lz, lane < 0 ? -1 : 1, 9f);
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

        // A lane from one end to the other, its points 10 m apart, driven forwards or back.
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

        /// <summary>The water grid the way NavBase leaves it (1 is water): a sea to the south and the east with a coast that bends.</summary>
        public static void Water(out byte[] cells, out int columns, out int rows, out float cell, out float originX, out float originZ)
        {
            cell = 8f;
            originX = (-Avenues / 2) * AvenueGap - 450f;
            originZ = (-Streets / 2) * StreetGap - 380f;
            columns = (int)(((Avenues - Avenues / 2) * AvenueGap + 900f - originX) / cell);
            rows = (int)(((Streets - Streets / 2) * StreetGap + 500f - originZ) / cell);
            cells = new byte[columns * rows];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    float x = originX + (c + 0.5f) * cell, z = originZ + (r + 0.5f) * cell;
                    float south = -StreetGap * Streets / 2 - 120f + 30f * (float)Math.Sin(x / 160f);
                    float east = (Avenues - Avenues / 2) * AvenueGap + 150f + 40f * (float)Math.Sin(z / 90f);
                    cells[r * columns + c] = z < south || x > east ? (byte)1 : (byte)0;
                }
            }
        }

        /// <summary>The first building whose outline holds or comes nearest the place; the preview's places stand on these.</summary>
        public static int Nearest(Map25City city, float px, float pz)
        {
            int best = 0;
            float most = float.MaxValue;
            for (int i = 0; i < city.Count; i++)
            {
                float cx = 0f, cz = 0f;
                int s = city.HullStart[i], e = city.HullStart[i + 1];
                for (int k = s; k < e; k++)
                {
                    cx += city.HullX[k];
                    cz += city.HullZ[k];
                }
                cx /= e - s;
                cz /= e - s;
                float d = (cx - px) * (cx - px) + (cz - pz) * (cz - pz);
                if (d < most)
                {
                    most = d;
                    best = i;
                }
            }
            return best;
        }
    }
}
