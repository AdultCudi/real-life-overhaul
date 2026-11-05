using System;

namespace RealLifeOverhaul.Map25
{
    /// <summary>
    /// Where a street's name is written on the map: every 260 m or so along each named road, on its middle line, with the way the
    /// road runs there. The roads are the lane points the GPS keeps (each tagged with its road, several lanes and both ways to
    /// a road), so a road's middle is the mean of its lanes' points near the spot. Made once per city, after every road has its
    /// name. Game-free.
    /// </summary>
    public sealed class Map25Names
    {
        public const float Spacing = 260f;        // metres between two labels of one road
        public const float LeastLength = 36f;     // a road shorter than this is not named on the map
        private const float Reach = 22f;          // metres either side of a spot whose points are averaged

        public int Count;
        public float[] X = new float[0], Z = new float[0];
        public float[] DirX = new float[0], DirZ = new float[0];
        public string[] Name = new string[0];

        /// <summary>
        /// The places of the names: <paramref name="road"/> gives each lane point its road (or -1), <paramref name="nameOfRoad"/> each
        /// road's name (null for none). Points that are junctions' own, or not in the road graph, are left out.
        /// </summary>
        public static Map25Names Anchors(int[] road, float[] x, float[] z, bool[] usable, bool[] junction, int points, string[] nameOfRoad)
        {
            int roads = nameOfRoad.Length;
            var start = new int[roads + 1];
            for (int i = 0; i < points; i++)
            {
                if (usable[i] && !junction[i] && road[i] >= 0 && road[i] < roads && nameOfRoad[road[i]] != null)
                {
                    start[road[i] + 1]++;
                }
            }
            for (int r = 0; r < roads; r++)
            {
                start[r + 1] += start[r];
            }
            var order = new int[start[roads]];
            var fill = new int[roads];
            for (int i = 0; i < points; i++)
            {
                if (usable[i] && !junction[i] && road[i] >= 0 && road[i] < roads && nameOfRoad[road[i]] != null)
                {
                    order[start[road[i]] + fill[road[i]]++] = i;
                }
            }
            var names = new Map25Names();
            var xs = new System.Collections.Generic.List<float>(256);
            var zs = new System.Collections.Generic.List<float>(256);
            var dxs = new System.Collections.Generic.List<float>(256);
            var dzs = new System.Collections.Generic.List<float>(256);
            var words = new System.Collections.Generic.List<string>(256);
            for (int r = 0; r < roads; r++)
            {
                int from = start[r], to = start[r + 1];
                if (to - from < 4)
                {
                    continue;
                }
                double mx = 0, mz = 0;
                for (int k = from; k < to; k++)
                {
                    mx += x[order[k]];
                    mz += z[order[k]];
                }
                mx /= to - from;
                mz /= to - from;
                double sxx = 0, szz = 0, sxz = 0;
                for (int k = from; k < to; k++)
                {
                    double dx = x[order[k]] - mx, dz = z[order[k]] - mz;
                    sxx += dx * dx;
                    szz += dz * dz;
                    sxz += dx * dz;
                }
                double angle = 0.5 * Math.Atan2(2 * sxz, sxx - szz);
                float ux = (float)Math.Cos(angle), uz = (float)Math.Sin(angle);
                float low = float.MaxValue, high = float.MinValue;
                for (int k = from; k < to; k++)
                {
                    float t = (x[order[k]] - (float)mx) * ux + (z[order[k]] - (float)mz) * uz;
                    low = Math.Min(low, t);
                    high = Math.Max(high, t);
                }
                float extent = high - low;
                if (extent < LeastLength)
                {
                    continue;
                }
                int n = Math.Max(1, (int)(extent / Spacing));
                for (int a = 0; a < n; a++)
                {
                    float at = low + (a + 0.5f) * extent / n;
                    float sumX = 0f, sumZ = 0f;
                    int near = 0;
                    float nearest = float.MaxValue;
                    int nearestPoint = -1;
                    for (int k = from; k < to; k++)
                    {
                        int p = order[k];
                        float t = (x[p] - (float)mx) * ux + (z[p] - (float)mz) * uz;
                        float away = Math.Abs(t - at);
                        if (away <= Reach)
                        {
                            sumX += x[p];
                            sumZ += z[p];
                            near++;
                        }
                        if (away < nearest)
                        {
                            nearest = away;
                            nearestPoint = p;
                        }
                    }
                    xs.Add(near > 0 ? sumX / near : x[nearestPoint]);
                    zs.Add(near > 0 ? sumZ / near : z[nearestPoint]);
                    dxs.Add(ux);
                    dzs.Add(uz);
                    words.Add(nameOfRoad[r]);
                }
            }
            names.Count = xs.Count;
            names.X = xs.ToArray();
            names.Z = zs.ToArray();
            names.DirX = dxs.ToArray();
            names.DirZ = dzs.ToArray();
            names.Name = words.ToArray();
            return names;
        }
    }
}
