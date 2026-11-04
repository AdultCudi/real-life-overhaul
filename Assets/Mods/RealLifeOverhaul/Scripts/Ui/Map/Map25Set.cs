using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Map25
{
    /// <summary>
    /// The whole city's blocks as chunks, one or more for each 192 m cell, each in one mesh: built once (on a pool thread, from
    /// arrays), kept for the session, culled a chunk at a time by its box. A building knows which chunk and which vertices are
    /// its, so a tint is a write of colours into one range of one chunk and nothing is built again. Game-free.
    /// </summary>
    public sealed class Map25Set
    {
        public const float Cell = 192f;
        private const int Flush = 58000;   // vertices at which a chunk is closed before the next building (a building never reaches 7,000)

        public Map25Chunk[] Blocks = new Map25Chunk[0];
        public Map25Chunk[] Contacts = new Map25Chunk[0];
        public int[] ChunkOf = new int[0], First = new int[0], Count = new int[0];
        public int Vertices, Triangles;
        public double Milliseconds;
        public Map25Detail Detail;

        /// <summary>The blocks of every building of the city, and the shadow chunks beside them.</summary>
        public static Map25Set Build(Map25City city, Map25Detail detail, float groundY)
        {
            long began = System.Diagnostics.Stopwatch.GetTimestamp();
            var set = new Map25Set { ChunkOf = new int[city.Count], First = new int[city.Count], Count = new int[city.Count], Detail = detail };
            if (city.Count == 0)
            {
                return set;
            }
            city.FindBounds();
            int cellsX = Math.Max(1, (int)((city.MaxX - city.MinX) / Cell) + 1);
            int cellsZ = Math.Max(1, (int)((city.MaxZ - city.MinZ) / Cell) + 1);
            var cellOf = new int[city.Count];
            var counts = new int[cellsX * cellsZ + 1];
            for (int i = 0; i < city.Count; i++)
            {
                int s = city.HullStart[i], e = city.HullStart[i + 1];
                float cx = 0f, cz = 0f;
                for (int k = s; k < e; k++)
                {
                    cx += city.HullX[k];
                    cz += city.HullZ[k];
                }
                int n = Math.Max(1, e - s);
                int gx = Math.Min(cellsX - 1, Math.Max(0, (int)((cx / n - city.MinX) / Cell)));
                int gz = Math.Min(cellsZ - 1, Math.Max(0, (int)((cz / n - city.MinZ) / Cell)));
                cellOf[i] = gz * cellsX + gx;
                counts[cellOf[i] + 1]++;
            }
            for (int c = 0; c < cellsX * cellsZ; c++)
            {
                counts[c + 1] += counts[c];
            }
            var order = new int[city.Count];
            var fill = new int[cellsX * cellsZ];
            for (int i = 0; i < city.Count; i++)
            {
                int c = cellOf[i];
                order[counts[c] + fill[c]++] = i;
            }
            var generator = new Map25Blocks();
            var builder = new Map25Builder(16384);
            var shade = new Map25Builder(4096);
            var blocks = new List<Map25Chunk>(128);
            var contacts = new List<Map25Chunk>(128);
            for (int c = 0; c < cellsX * cellsZ; c++)
            {
                for (int at = counts[c]; at < counts[c + 1]; at++)
                {
                    int i = order[at];
                    if (builder.Vertices > Flush)
                    {
                        blocks.Add(builder.ToChunk());
                    }
                    if (shade.Vertices > Flush)
                    {
                        contacts.Add(shade.ToChunk());
                    }
                    set.ChunkOf[i] = blocks.Count;
                    set.First[i] = builder.Vertices;
                    generator.Generate(builder, city, i, detail);
                    set.Count[i] = builder.Vertices - set.First[i];
                    generator.Contact(shade, city, i, groundY);
                }
                if (builder.Vertices > 0)
                {
                    blocks.Add(builder.ToChunk());
                }
                if (shade.Vertices > 0)
                {
                    contacts.Add(shade.ToChunk());
                }
            }
            set.Blocks = blocks.ToArray();
            set.Contacts = contacts.ToArray();
            for (int k = 0; k < set.Blocks.Length; k++)
            {
                set.Vertices += set.Blocks[k].Vertices;
                set.Triangles += set.Blocks[k].Indices / 3;
            }
            for (int k = 0; k < set.Contacts.Length; k++)
            {
                set.Vertices += set.Contacts[k].Vertices;
                set.Triangles += set.Contacts[k].Indices / 3;
            }
            set.Milliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - began) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            return set;
        }

        /// <summary>
        /// Writes building <paramref name="i"/>'s colours again, as the city now has its tint, into the same vertices; false when the
        /// building would no longer have the vertices it has (it never does: a tint changes colours only). Cost: one building drawn
        /// into a scratch builder, about 20 microseconds.
        /// </summary>
        public bool Retint(Map25City city, int i, Map25Blocks generator, Map25Builder scratch)
        {
            if (i < 0 || i >= ChunkOf.Length || Count[i] == 0)
            {
                return false;
            }
            scratch.Clear();
            generator.Generate(scratch, city, i, Detail);
            bool same = scratch.Vertices == Count[i];
            if (same)
            {
                Map25Chunk chunk = Blocks[ChunkOf[i]];
                scratch.CopyColours(0, Count[i], chunk.Col, First[i]);
                chunk.Dirty(First[i], Count[i]);
            }
            scratch.Clear();
            return same;
        }
    }
}
