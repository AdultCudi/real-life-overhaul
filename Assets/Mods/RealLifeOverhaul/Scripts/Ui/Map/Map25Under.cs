using System;

namespace RealLifeOverhaul.Map25
{
    /// <summary>
    /// What lies under the blocks, in chunks: the land, the green ground of the parks, the roads and the lane marking over them, and the
    /// trees. Made once on a pool thread (<see cref="Make"/>) from the arrays the minimap and the road graph keep and the game's trees.
    /// </summary>
    public sealed class Map25Under
    {
        public Map25Chunk[] Land = new Map25Chunk[0], Greens = new Map25Chunk[0], Roads = new Map25Chunk[0], Lanes = new Map25Chunk[0], Trees = new Map25Chunk[0];
        public int Vertices, Triangles;
        public double Milliseconds;

        /// <summary>Makes all five. <paramref name="treeX"/> and <paramref name="treeZ"/> may be null (no trees); the roads' arrays are the graph's.</summary>
        public static Map25Under Make(byte[] cells, int columns, int rows, float cell, float originX, float originZ, float landY,
                                      float[] roadX, float[] roadY, float[] roadZ, int[] edgeStart, int[] edgeTo, float[] edgeExtra, bool[] usable, float[] speed, int roadCount,
                                      float worldOriginX, float worldOriginZ, float worldWidth, float worldHeight, float y0,
                                      float[] treeX, float[] treeZ, int trees, float minX, float minZ, float maxX, float maxZ)
        {
            long began = System.Diagnostics.Stopwatch.GetTimestamp();
            var under = new Map25Under();
            under.Land = Map25Ground.Land(cells, columns, rows, cell, originX, originZ, landY);
            under.Roads = Map25Ground.Roads(roadX, roadY, roadZ, edgeStart, edgeTo, edgeExtra, usable, speed, roadCount, worldOriginX, worldOriginZ, worldWidth, worldHeight, y0, out under.Lanes);
            if (treeX != null && treeZ != null && trees > 0)
            {
                under.Greens = Map25Ground.Greens(treeX, treeZ, trees, minX, minZ, maxX, maxZ, landY * 0.25f);
                under.Trees = Map25Ground.Trees(treeX, treeZ, trees, minX, minZ, maxX, maxZ, 0f);
            }
            foreach (Map25Chunk[] group in new[] { under.Land, under.Greens, under.Roads, under.Lanes, under.Trees })
            {
                foreach (Map25Chunk chunk in group)
                {
                    under.Vertices += chunk.Vertices;
                    under.Triangles += chunk.Indices / 3;
                }
            }
            under.Milliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - began) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            return under;
        }
    }
}
