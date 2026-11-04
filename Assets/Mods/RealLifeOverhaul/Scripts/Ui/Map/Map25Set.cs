using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Map25
{
    /// <summary>
    /// The whole city's blocks as chunks, one or more for each 192 m cell and each kind of drawing (<see cref="Map25Kind"/>), each in one
    /// mesh: built once (on a pool thread, from arrays), kept for the session, culled a chunk at a time by its box. A building knows
    /// which chunk and which vertices are its in each kind, so a tint is a write of colours into one range of one chunk and nothing is
    /// built again. Game-free.
    /// </summary>
    public sealed class Map25Set
    {
        public const float Cell = 192f;
        private const int Flush = 58000;   // vertices at which a chunk is closed before the next building (a building never reaches 7,000)

        public readonly Map25Layer[] Layers = new Map25Layer[Map25Builders.Kinds];
        public int Vertices, Triangles;
        public double Milliseconds;
        public Map25Detail Detail;

        public Map25Set()
        {
            for (int k = 0; k < Layers.Length; k++)
            {
                Layers[k] = new Map25Layer { Tints = k <= (int)Map25Kind.Fronts };
            }
        }

        public Map25Chunk[] Blocks => Layers[(int)Map25Kind.Blocks].Chunks;
        public Map25Chunk[] Contacts => Layers[(int)Map25Kind.Contacts].Chunks;
        public int[] ChunkOf => Layers[(int)Map25Kind.Blocks].ChunkOf;
        public int[] First => Layers[(int)Map25Kind.Blocks].First;
        public int[] Count => Layers[(int)Map25Kind.Blocks].Count;

        /// <summary>The blocks of every building of the city in every kind of drawing the detail has.</summary>
        public static Map25Set Build(Map25City city, Map25Detail detail, float groundY)
        {
            long began = System.Diagnostics.Stopwatch.GetTimestamp();
            var set = new Map25Set { Detail = detail };
            for (int k = 0; k < set.Layers.Length; k++)
            {
                set.Layers[k].ChunkOf = new int[city.Count];
                set.Layers[k].First = new int[city.Count];
                set.Layers[k].Count = new int[city.Count];
                set.Layers[k].Tints = k <= (int)Map25Kind.Fronts && detail.Fine;   // only the full map shows the game's highlights: the minimap's set is never written again
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
                        set.Layers[k].ChunkOf[i] = chunks[k].Count;
                        set.Layers[k].First[i] = builders.All[k].Vertices;
                    }
                    generator.Generate(builders, city, i, detail, true);
                    generator.Contact(contact, city, i, groundY);
                    for (int k = 0; k < chunks.Length; k++)
                    {
                        set.Layers[k].Count[i] = builders.All[k].Vertices - set.Layers[k].First[i];
                    }
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

        /// <summary>
        /// Writes building <paramref name="i"/>'s colours again, as the city now has its tint, into the same vertices of the kinds a tint
        /// changes; false when the building would no longer have the vertices it has (it never does: a tint changes colours only). Cost: one
        /// building drawn into the scratch builders, about 20 microseconds.
        /// </summary>
        public bool Retint(Map25City city, int i, Map25Blocks generator, Map25Builders scratch)
        {
            if (i < 0 || i >= Layers[0].ChunkOf.Length || Layers[0].Count[i] == 0)
            {
                return false;
            }
            scratch.Clear();
            generator.Generate(scratch, city, i, Detail, false);
            bool same = true;
            for (int k = 0; k < Layers.Length; k++)
            {
                if (Layers[k].Tints && scratch.All[k].Vertices != Layers[k].Count[i])
                {
                    same = false;
                }
            }
            if (same)
            {
                for (int k = 0; k < Layers.Length; k++)
                {
                    Map25Layer layer = Layers[k];
                    if (!layer.Tints || layer.Count[i] == 0)
                    {
                        continue;
                    }
                    Map25Chunk chunk = layer.Chunks[layer.ChunkOf[i]];
                    scratch.All[k].CopyColours(0, layer.Count[i], chunk.Col, layer.First[i]);
                    chunk.Dirty(layer.First[i], layer.Count[i]);
                }
            }
            scratch.Clear();
            return same;
        }
    }
}
