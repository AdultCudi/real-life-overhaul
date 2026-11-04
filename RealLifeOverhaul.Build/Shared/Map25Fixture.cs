using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Map25
{
    /// <summary>
    /// A made-up city of the grain of the game's, for the checks and for previews drawn outside the game (it is compiled into the checks and
    /// the preview only, never into the mod): avenues 87 m apart and 13 m wide, streets 187 m apart and 22 m wide, blocks of 74 by 165 m,
    /// each a ring of buildings 24 m deep cut into slices 9 to 14 m wide round a courtyard (the owner's real city measures the same: median
    /// shorter side 15 m, a quarter of the buildings 10 m or less), a park, a sparse scatter of boxes beyond, and a sea along two sides.
    /// Deterministic from its seed. Heights come from <see cref="ModelHeight"/>, whose numbers say what they are.
    /// </summary>
    public static class Map25Fixture
    {
        public const float AvenuePitch = 87.4f, AvenueGap = 13f, StreetPitch = 187f, StreetGap = 22f, Depth = 24f;
        public const int Columns = 5, Rows = 3;

        /// <summary>The route the previews follow: north along an avenue, east along a street, north again, west to the door.</summary>
        public static readonly float[] RouteX, RouteZ;

        /// <summary>The place the player stands at the start of the route.</summary>
        public static float StartX => RouteX[0];
        public static float StartZ => RouteZ[0];

        static Map25Fixture()
        {
            var x = new List<float>();
            var z = new List<float>();
            float a1 = AvenueX(2), a2 = AvenueX(3), s1 = StreetZ(1), s2 = StreetZ(2);
            AddLine(x, z, a1, StreetZ(0) - 40f, a1, s1);
            AddLine(x, z, a1, s1, a2, s1);
            AddLine(x, z, a2, s1, a2, s2);
            AddLine(x, z, a2, s2, a2 - 36f, s2);
            RouteX = x.ToArray();
            RouteZ = z.ToArray();
        }

        /// <summary>The middle of avenue <paramref name="i"/> (0 to <see cref="Columns"/>), metres east of the city's middle.</summary>
        public static float AvenueX(int i)
        {
            return (i - Columns * 0.5f) * AvenuePitch;
        }

        /// <summary>The middle of street <paramref name="j"/> (0 to <see cref="Rows"/>), metres north of the city's middle.</summary>
        public static float StreetZ(int j)
        {
            return (j - Rows * 0.5f) * StreetPitch;
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

        /// <summary>
        /// A building's height by a model, in metres: a half are sheds and low rise (6 to 16 m), most of the rest mid rise (16 to 42 m), and towers of
        /// 45 to 130 m, more of them near the middle of the city (<paramref name="tall"/>: 120 to 260 m, to show that a block's height has a limit). The
        /// game's own heights are only measured in the game; this is what the previews and the checks draw with.
        /// </summary>
        public static float ModelHeight(uint seed, float core, bool tall)
        {
            var rng = new Map25Rng(seed);
            float r = rng.Float();
            if (r < 0.05f + 0.20f * core)
            {
                return tall ? rng.Range(120f, 260f) : rng.Range(45f, 130f);
            }
            return r < 0.50f ? rng.Range(16f, 42f) : rng.Range(6f, 16f);
        }

        /// <summary>The buildings: the dense blocks, a ring of slices round each courtyard, and a sparse scatter beyond them.</summary>
        public static Map25City City(int seed, bool tall = false)
        {
            var hx = new List<float>(8192);
            var hz = new List<float>(8192);
            var start = new List<int> { 0 };
            var height = new List<float>(1024);
            var key = new List<long>(1024);
            var rng = new Map25Rng((uint)seed);
            for (int i = 0; i < Columns; i++)
            {
                for (int j = 0; j < Rows; j++)
                {
                    float x0 = AvenueX(i) + AvenueGap * 0.5f, x1 = AvenueX(i + 1) - AvenueGap * 0.5f;
                    float z0 = StreetZ(j) + StreetGap * 0.5f, z1 = StreetZ(j + 1) - StreetGap * 0.5f;
                    if (i == 2 && j == 1)
                    {
                        continue;   // the park
                    }
                    float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
                    float core = (float)Math.Exp(-((cx / 260f) * (cx / 260f) + (cz / 300f) * (cz / 300f)));
                    // the ring: the south and north rows run the block's width, the west and east columns what lies between them
                    Row(hx, hz, start, height, key, rng, x0, z0, x1, z0 + Depth, true, core, tall);
                    Row(hx, hz, start, height, key, rng, x0, z1 - Depth, x1, z1, true, core, tall);
                    Row(hx, hz, start, height, key, rng, x0, z0 + Depth, x0 + Depth, z1 - Depth, false, core, tall);
                    Row(hx, hz, start, height, key, rng, x1 - Depth, z0 + Depth, x1, z1 - Depth, false, core, tall);
                    if (rng.Float() < 0.35f)
                    {
                        float w = rng.Range(18f, 24f), l = rng.Range(26f, 44f);
                        float ix = cx - w * 0.5f + rng.Range(-2f, 2f), iz = cz - l * 0.5f + rng.Range(-30f, 30f);
                        AddBox(hx, hz, start, height, key, ix, iz, ix + w, iz + l, ModelHeight((uint)(hx.Count * 31 + 7), core, tall), 0x7000L + key.Count);
                    }
                }
            }
            // boxes scattered beyond the grid, as the game's sparse districts are
            for (int k = 0; k < 90; k++)
            {
                float a = rng.Range(0f, 6.2831f), d = rng.Range(0.62f, 1.0f);
                float cx = (float)Math.Cos(a) * (Columns * AvenuePitch * 0.5f + 120f + 260f * d), cz = (float)Math.Sin(a) * (Rows * StreetPitch * 0.5f + 140f + 200f * d);
                float w = rng.Range(14f, 34f), l = rng.Range(14f, 34f);
                AddBox(hx, hz, start, height, key, cx - w * 0.5f, cz - l * 0.5f, cx + w * 0.5f, cz + l * 0.5f, rng.Range(5f, 16f), 0x8000L + k);
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
            city.Compress();
            city.FindBounds();
            return city;
        }

        // A run of slices along a rectangle's longer way (along x when alongX): 9 to 14 m wide, each a box with a height of the model's.
        private static void Row(List<float> hx, List<float> hz, List<int> start, List<float> height, List<long> key, Map25Rng rng, float x0, float z0, float x1, float z1,
                                bool alongX, float core, bool tall)
        {
            float at = alongX ? x0 : z0, end = alongX ? x1 : z1;
            while (end - at > 5f)
            {
                float w = Math.Min(end - at, rng.Range(9f, 14.5f));
                if (end - (at + w) < 6f)
                {
                    w = end - at;
                }
                float h = ModelHeight((uint)(hx.Count * 17 + 3), core, tall);
                if (alongX)
                {
                    AddBox(hx, hz, start, height, key, at, z0, at + w, z1, h, 0x1000L + key.Count);
                }
                else
                {
                    AddBox(hx, hz, start, height, key, x0, at, x1, at + w, h, 0x1000L + key.Count);
                }
                at += w;
            }
        }

        // Anticlockwise from above (x east, z north).
        private static void AddBox(List<float> hx, List<float> hz, List<int> start, List<float> height, List<long> key, float x0, float z0, float x1, float z1, float h, long id)
        {
            hx.Add(x0); hz.Add(z0);
            hx.Add(x1); hz.Add(z0);
            hx.Add(x1); hz.Add(z1);
            hx.Add(x0); hz.Add(z1);
            start.Add(hx.Count);
            height.Add(h);
            key.Add(id);
        }

        /// <summary>The lanes: as many as fill each avenue and street (a lane every 3.3 m), a point every 10 m, edges both ways, none that is a change of lane.</summary>
        public static void Roads(out float[] x, out float[] y, out float[] z, out int[] edgeStart, out int[] edgeTo, out float[] edgeExtra, out bool[] usable, out float[] speed, out int count)
        {
            var px = new List<float>(16384);
            var pz = new List<float>(16384);
            var sp = new List<float>(16384);
            var chains = new List<int[]>();
            float minX = AvenueX(0) - 500f, maxX = AvenueX(Columns) + 500f;
            float minZ = StreetZ(0) - 500f, maxZ = StreetZ(Rows) + 500f;
            for (int i = 0; i <= Columns; i++)
            {
                int lanes = (int)Math.Round(AvenueGap / 3.3f);
                for (int lane = 0; lane < lanes; lane++)
                {
                    float lx = AvenueX(i) + (lane - (lanes - 1) * 0.5f) * 3.3f;
                    Chain(px, pz, sp, chains, lx, minZ, lx, maxZ, lane < lanes / 2 ? -1 : 1, 14f);
                }
            }
            for (int j = 0; j <= Rows; j++)
            {
                int lanes = (int)Math.Round(StreetGap / 3.3f);
                for (int lane = 0; lane < lanes; lane++)
                {
                    float lz = StreetZ(j) + (lane - (lanes - 1) * 0.5f) * 3.3f;
                    Chain(px, pz, sp, chains, minX, lz, maxX, lz, lane < lanes / 2 ? -1 : 1, 9f);
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
            originX = AvenueX(0) - 900f;
            originZ = StreetZ(0) - 800f;
            columns = (int)((AvenueX(Columns) + 900f - originX) / cell);
            rows = (int)((StreetZ(Rows) + 800f - originZ) / cell);
            cells = new byte[columns * rows];
            float south = StreetZ(0) - 120f, east = AvenueX(Columns) + 130f;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    float x = originX + (c + 0.5f) * cell, z = originZ + (r + 0.5f) * cell;
                    float s = south + 26f * (float)Math.Sin(x / 150f);
                    float e = east + 34f * (float)Math.Sin(z / 90f);
                    cells[r * columns + c] = z < s || x > e ? (byte)1 : (byte)0;
                }
            }
        }

        /// <summary>The trees: a park in the empty block, and a row along every avenue every 14 m on both sides. Places only; the game's are in TreeController.AllTrees.</summary>
        public static void Trees(out float[] x, out float[] z, out int count)
        {
            var tx = new List<float>(1024);
            var tz = new List<float>(1024);
            var rng = new Map25Rng(77);
            float x0 = AvenueX(2) + AvenueGap * 0.5f, x1 = AvenueX(3) - AvenueGap * 0.5f, z0 = StreetZ(1) + StreetGap * 0.5f, z1 = StreetZ(2) - StreetGap * 0.5f;
            for (int k = 0; k < 130; k++)
            {
                tx.Add(rng.Range(x0 + 4f, x1 - 4f));
                tz.Add(rng.Range(z0 + 4f, z1 - 4f));
            }
            for (int i = 0; i <= Columns; i++)
            {
                for (float zz = StreetZ(0) + 20f; zz < StreetZ(Rows) - 20f; zz += 14f)
                {
                    tx.Add(AvenueX(i) - AvenueGap * 0.5f - 1.5f);
                    tz.Add(zz + rng.Range(-1.5f, 1.5f));
                    tx.Add(AvenueX(i) + AvenueGap * 0.5f + 1.5f);
                    tz.Add(zz + rng.Range(-1.5f, 1.5f));
                }
            }
            x = tx.ToArray();
            z = tz.ToArray();
            count = x.Length;
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
