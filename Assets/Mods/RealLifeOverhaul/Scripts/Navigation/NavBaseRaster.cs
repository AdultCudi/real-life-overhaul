using System;

namespace RealLifeOverhaul.Nav
{
    /// <summary>
    /// The base picture as numbers, four bytes a pixel: buildings in one flat colour over water in another, clear where
    /// there is neither. It touches only the arrays it is given, so it runs off the main thread.
    /// </summary>
    public static class NavBaseRaster
    {
        /// <summary>
        /// Every building's outline into a byte of cover a pixel, its edge a pixel soft, the outermost pixels left empty so
        /// no building is drawn out past the edge. Returns how many outlines were drawn.
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
        /// The picture from the buildings' cover and the settled water. A clear pixel beside a coloured one takes its colour,
        /// still clear, so the graphics card's smoothing does not darken the edge.
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
