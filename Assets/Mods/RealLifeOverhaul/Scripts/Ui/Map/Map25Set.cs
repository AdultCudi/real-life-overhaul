using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Map25
{
    /// <summary>
    /// The whole city's blocks as chunks, one or more for each 192 m cell and each kind of drawing (<see cref="Map25Kind"/>), each in one
    /// mesh: built once (on a pool thread, from arrays), kept for the session, culled a chunk at a time by its box. Game-free.
    /// </summary>
    public sealed class Map25Set
    {
        public const float Cell = 192f;
        private const int Flush = 58000;   // vertices at which a chunk is closed before the next building (a building never reaches 7,000)

        public readonly Map25Layer[] Layers = new Map25Layer[Map25Builders.Kinds];
        public int Vertices, Triangles;
        public double Milliseconds;

        public Map25Set()
        {
            for (int k = 0; k < Layers.Length; k++)
            {
                Layers[k] = new Map25Layer();
            }
        }

        public Map25Chunk[] Blocks => Layers[(int)Map25Kind.Blocks].Chunks;
        public Map25Chunk[] Contacts => Layers[(int)Map25Kind.Contacts].Chunks;

        /// <summary>The blocks of every building of the city in every kind of drawing the detail has.</summary>
        public static Map25Set Build(Map25City city, float groundY)
        {
            long began = System.Diagnostics.Stopwatch.GetTimestamp();
            var set = new Map25Set();
            for (int k = 0; k < set.Layers.Length; k++)
            {
            }
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
            var builders = new Map25Builders(16384);
            var contact = builders.Contacts;
            var chunks = new List<Map25Chunk>[Map25Builders.Kinds];
            for (int k = 0; k < chunks.Length; k++)
            {
                chunks[k] = new List<Map25Chunk>(128);
            }
            for (int c = 0; c < cellsX * cellsZ; c++)
            {
                for (int at = counts[c]; at < counts[c + 1]; at++)
                {
                    int i = order[at];
                    for (int k = 0; k < chunks.Length; k++)
                    {
                        if (builders.All[k].Vertices > Flush)
                        {
                            chunks[k].Add(builders.All[k].ToChunk());
                        }
                    }
                    generator.Generate(builders, city, i);
                    generator.Contact(contact, city, i, groundY);
                }
                for (int k = 0; k < chunks.Length; k++)
                {
                    if (builders.All[k].Vertices > 0)
                    {
                        chunks[k].Add(builders.All[k].ToChunk());
                    }
                }
            }
            for (int k = 0; k < chunks.Length; k++)
            {
                set.Layers[k].Chunks = chunks[k].ToArray();
                set.Layers[k].Total();
                set.Vertices += set.Layers[k].Vertices;
                set.Triangles += set.Layers[k].Triangles;
            }
            set.Milliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - began) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            return set;
        }
    }
}
