using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Map25
{
    /// <summary>
    /// What lies under the blocks, from the data the minimap already keeps: the land as a smooth outline (marching squares over
    /// the 8 m water grid, so a coast is a line and not steps), the roads as a band along every lane (the same 4.6 m the flat
    /// minimap draws), and the lines of the GPS (the route as one strip, the player's view cone). In 192 m chunks like the blocks,
    /// built once on a pool thread. Game-free.
    /// </summary>
    public static class Map25Ground
    {
        public const float RoadHalf = 2.3f;        // metres either side of a lane's middle
        public const float FastLane = 12f;         // metres a second from which a road is an avenue, drawn a little lighter

        private const int Flush = 58000;

        /// <summary>The roads: every lane that costs nothing extra (not a change of lane) between two usable points, one chunk a cell.</summary>
        public static Map25Chunk[] Roads(float[] x, float[] y, float[] z, int[] edgeStart, int[] edgeTo, float[] edgeExtra, bool[] usable, float[] speed, int count,
                                         float originX, float originZ, float width, float height, float y0)
        {
            int cellsX = Math.Max(1, (int)(width / Map25Set.Cell) + 1), cellsZ = Math.Max(1, (int)(height / Map25Set.Cell) + 1);
            int edges = 0;
            for (int i = 0; i < count; i++)
            {
                if (!usable[i])
                {
                    continue;
                }
                for (int e = edgeStart[i]; e < edgeStart[i + 1]; e++)
                {
                    edges += edgeExtra[e] > 0f || !usable[edgeTo[e]] ? 0 : 1;
                }
            }
            var from = new int[edges];
            var to = new int[edges];
            var cellOf = new int[edges];
            var counts = new int[cellsX * cellsZ + 1];
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                if (!usable[i])
                {
                    continue;
                }
                for (int e = edgeStart[i]; e < edgeStart[i + 1]; e++)
                {
                    int j = edgeTo[e];
                    if (edgeExtra[e] > 0f || !usable[j])
                    {
                        continue;
                    }
                    from[n] = i;
                    to[n] = j;
                    int gx = Math.Min(cellsX - 1, Math.Max(0, (int)(((x[i] + x[j]) * 0.5f - originX) / Map25Set.Cell)));
                    int gz = Math.Min(cellsZ - 1, Math.Max(0, (int)(((z[i] + z[j]) * 0.5f - originZ) / Map25Set.Cell)));
                    cellOf[n] = gz * cellsX + gx;
                    counts[cellOf[n] + 1]++;
                    n++;
                }
            }
            for (int c = 0; c < cellsX * cellsZ; c++)
            {
                counts[c + 1] += counts[c];
            }
            var order = new int[n];
            var fill = new int[cellsX * cellsZ];
            for (int k = 0; k < n; k++)
            {
                order[counts[cellOf[k]] + fill[cellOf[k]]++] = k;
            }
            var chunks = new List<Map25Chunk>(64);
            var b = new Map25Builder(8192);
            for (int c = 0; c < cellsX * cellsZ; c++)
            {
                for (int at = counts[c]; at < counts[c + 1]; at++)
                {
                    if (b.Vertices > Flush)
                    {
                        chunks.Add(b.ToChunk());
                    }
                    int i = from[order[at]], j = to[order[at]];
                    float dx = x[j] - x[i], dz = z[j] - z[i];
                    float l = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (l < 0.05f)
                    {
                        continue;
                    }
                    float ux = dx / l, uz = dz / l;
                    float px = -uz * RoadHalf, pz = ux * RoadHalf;
                    float ex = ux * RoadHalf * 0.7f, ez = uz * RoadHalf * 0.7f;   // the ends overlap, so a bend leaves no gap
                    Map25Colour colour = speed != null && speed[i] >= FastLane ? Map25Palette.Avenue : Map25Palette.Street;
                    float ya = y[i] - y0 + 0.02f, yb = y[j] - y0 + 0.02f;
                    b.Flat(x[i] - ex + px, ya, z[i] - ez + pz, x[j] + ex + px, yb, z[j] + ez + pz, x[j] + ex - px, yb, z[j] + ez - pz, x[i] - ex - px, ya, z[i] - ez - pz, colour, 1f);
                }
                if (b.Vertices > 0)
                {
                    chunks.Add(b.ToChunk());
                }
            }
            return chunks.ToArray();
        }

        /// <summary>
        /// The land, from the water grid (byte 1 is water, as NavBaseWater leaves it): the outline at half way between a land square
        /// and a water one, so a coast runs at an angle, in chunks of one cell block each. Squares wholly land are joined along a row.
        /// </summary>
        public static Map25Chunk[] Land(byte[] cells, int columns, int rows, float cell, float originX, float originZ, float y)
        {
            int block = Math.Max(1, (int)Math.Round(Map25Set.Cell / cell));
            var chunks = new List<Map25Chunk>(256);
            var b = new Map25Builder(2048);
            var f = new float[4];
            var px = new float[8];
            var pz = new float[8];
            Map25Colour land = Map25Palette.Block;
            for (int gz = 0; gz <= rows; gz += block)
            {
                for (int gx = 0; gx <= columns; gx += block)
                {
                    for (int vz = gz; vz < Math.Min(gz + block, rows + 1); vz++)
                    {
                        int runStart = -1;
                        for (int vx = gx; vx <= Math.Min(gx + block, columns + 1); vx++)
                        {
                            int inside = 0;
                            bool whole = false;
                            if (vx < Math.Min(gx + block, columns + 1))
                            {
                                f[0] = Field(cells, columns, rows, vx, vz);
                                f[1] = Field(cells, columns, rows, vx + 1, vz);
                                f[2] = Field(cells, columns, rows, vx + 1, vz + 1);
                                f[3] = Field(cells, columns, rows, vx, vz + 1);
                                for (int k = 0; k < 4; k++)
                                {
                                    inside += f[k] >= 0.5f ? 1 : 0;
                                }
                                whole = inside == 4;
                            }
                            if (whole)
                            {
                                if (runStart < 0)
                                {
                                    runStart = vx;
                                }
                                continue;
                            }
                            if (runStart >= 0)
                            {
                                float x0 = originX + runStart * cell, x1 = originX + vx * cell, z0 = originZ + vz * cell, z1 = z0 + cell;
                                b.Flat(x0, y, z0, x1, y, z0, x1, y, z1, x0, y, z1, land, 1f);
                                runStart = -1;
                            }
                            if (inside == 0 || vx >= Math.Min(gx + block, columns + 1))
                            {
                                continue;
                            }
                            int m = Clip(f, originX + vx * cell, originZ + vz * cell, cell, px, pz);
                            if (m >= 3)
                            {
                                int first = b.Vertex(px[0], y, pz[0], land);
                                int prev = b.Vertex(px[1], y, pz[1], land);
                                for (int k = 2; k < m; k++)
                                {
                                    int next = b.Vertex(px[k], y, pz[k], land);
                                    b.Triangle(first, prev, next);
                                    prev = next;
                                }
                            }
                        }
                    }
                    if (b.Vertices > 0)
                    {
                        chunks.Add(b.ToChunk());
                    }
                }
            }
            return chunks.ToArray();
        }

        // The land share at a corner of the grid: the mean of the squares round it, a square outside the grid taken as the nearest one.
        private static float Field(byte[] cells, int columns, int rows, int vx, int vz)
        {
            float sum = 0f;
            for (int k = 0; k < 4; k++)
            {
                int cx = Math.Min(columns - 1, Math.Max(0, vx - 1 + (k & 1)));
                int cz = Math.Min(rows - 1, Math.Max(0, vz - 1 + (k >> 1)));
                sum += cells[cz * columns + cx] == 1 ? 0f : 1f;
            }
            return sum * 0.25f;
        }

        // The part of one grid square whose field is land (a half or more), as a polygon of up to eight corners.
        private static int Clip(float[] f, float x, float z, float size, float[] px, float[] pz)
        {
            int m = 0;
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) & 3;
                float ix = x + ((i == 1 || i == 2) ? size : 0f), iz = z + (i >= 2 ? size : 0f);
                float jx = x + ((j == 1 || j == 2) ? size : 0f), jz = z + (j >= 2 ? size : 0f);
                bool a = f[i] >= 0.5f, c = f[j] >= 0.5f;
                if (a)
                {
                    px[m] = ix;
                    pz[m] = iz;
                    m++;
                }
                if (a != c)
                {
                    float t = (0.5f - f[i]) / (f[j] - f[i]);
                    px[m] = ix + (jx - ix) * t;
                    pz[m] = iz + (jz - iz) * t;
                    m++;
                }
            }
            return m;
        }

        /// <summary>
        /// A line along a path as one strip, its corners mitred so it has no overlaps (a translucent line must not show its joints),
        /// <paramref name="lift"/> above the ground. Points closer than a metre to the last are left out. Returns the points used.
        /// </summary>
        public static int Ribbon(Map25Builder b, float[] x, float[] y, float[] z, int count, float half, Map25Colour colour, float alpha, float lift, float y0)
        {
            int used = 0;
            float lx = 0f, lz = 0f;
            int pa = -1, pb = -1;
            float dxPrev = 0f, dzPrev = 0f;
            for (int i = 0; i < count; i++)
            {
                if (used > 0 && (x[i] - lx) * (x[i] - lx) + (z[i] - lz) * (z[i] - lz) < 1f && i < count - 1)
                {
                    continue;
                }
                float dx, dz;
                int j = i + 1;
                while (j < count - 1 && (x[j] - x[i]) * (x[j] - x[i]) + (z[j] - z[i]) * (z[j] - z[i]) < 1f)
                {
                    j++;
                }
                if (i < count - 1)
                {
                    dx = x[j] - x[i];
                    dz = z[j] - z[i];
                }
                else
                {
                    dx = dxPrev;
                    dz = dzPrev;
                }
                float l = (float)Math.Sqrt(dx * dx + dz * dz);
                if (l < 1e-4f)
                {
                    continue;
                }
                dx /= l;
                dz /= l;
                float sx = dx, sz = dz;
                float scale = 1f;
                if (used > 0 && i < count - 1)
                {
                    sx = dx + dxPrev;
                    sz = dz + dzPrev;
                    float sl = (float)Math.Sqrt(sx * sx + sz * sz);
                    if (sl > 0.2f)
                    {
                        sx /= sl;
                        sz /= sl;
                        float cos = sx * dx + sz * dz;
                        scale = 1f / Math.Max(0.5f, cos);
                    }
                    else
                    {
                        sx = dx;
                        sz = dz;
                    }
                }
                float nx = -sz * half * scale, nz = sx * half * scale;
                float yy = y[i] - y0 + lift;
                int left = b.Vertex(x[i] + nx, yy, z[i] + nz, colour, alpha);
                int right = b.Vertex(x[i] - nx, yy, z[i] - nz, colour, alpha);
                if (pa >= 0)
                {
                    b.Quad(pa, left, right, pb);
                }
                pa = left;
                pb = right;
                lx = x[i];
                lz = z[i];
                dxPrev = dx;
                dzPrev = dz;
                used++;
            }
            return used;
        }

        /// <summary>A pie of a disc on the ground: the player's view cone (a few degrees either side of <paramref name="heading"/>, clockwise from north), or a halo with the full circle.</summary>
        public static void Fan(Map25Builder b, float cx, float y, float cz, float radius, float headingDegrees, float halfAngleDegrees, Map25Colour colour, float alpha)
        {
            int steps = Math.Max(3, (int)(halfAngleDegrees * 2f / 6f));
            int centre = b.Vertex(cx, y, cz, colour, alpha);
            int prev = -1;
            for (int s = 0; s <= steps; s++)
            {
                float a = (headingDegrees - halfAngleDegrees + s * 2f * halfAngleDegrees / steps) * Map25Math.Deg;
                int v = b.Vertex(cx + (float)Math.Sin(a) * radius, y, cz + (float)Math.Cos(a) * radius, colour, alpha);
                if (prev >= 0)
                {
                    b.Triangle(centre, prev, v);
                }
                prev = v;
            }
        }
    }
}
