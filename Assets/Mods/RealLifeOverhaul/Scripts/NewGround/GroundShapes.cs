using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.NewGround
{
    /// <summary>A rectangle of whole tiles of one walking mesh: its first and last column and row, both inclusive.</summary>
    public struct TileRect
    {
        public int X0;
        public int Z0;
        public int X1;
        public int Z1;

        public TileRect(int x0, int z0, int x1, int z1)
        {
            X0 = x0;
            Z0 = z0;
            X1 = x1;
            Z1 = z1;
        }

        public int Count => (X1 - X0 + 1) * (Z1 - Z0 + 1);

        public bool Holds(long tile)
        {
            int x = TileGrid.KeyX(tile), z = TileGrid.KeyZ(tile);
            return x >= X0 && x <= X1 && z >= Z0 && z <= Z1;
        }
    }

    /// <summary>A rectangle of ground along the world's axes (m), as a plan keeps off something standing there.</summary>
    public struct FlatRect
    {
        public float MinX;
        public float MinZ;
        public float MaxX;
        public float MaxZ;

        public FlatRect(float x0, float z0, float x1, float z1)
        {
            MinX = Math.Min(x0, x1);
            MinZ = Math.Min(z0, z1);
            MaxX = Math.Max(x0, x1);
            MaxZ = Math.Max(z0, z1);
        }

        public bool Holds(float x, float z)
        {
            return x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
        }

        /// <summary>The same rectangle grown by <paramref name="by"/> on every side.</summary>
        public FlatRect Grown(float by)
        {
            return new FlatRect(MinX - by, MinZ - by, MaxX + by, MaxZ + by);
        }
    }

    /// <summary>A point of a walking mesh: a seed the old mesh joined to it is known by.</summary>
    public struct GroundPoint
    {
        public float X;
        public float Y;
        public float Z;

        public GroundPoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>
    /// The shapes a district's ground is grown with, game-free: the covered tiles as rectangles, each one update of Unity's
    /// builder (a rectangle's bounds hold whole exactly its own tiles, and one update over many tiles builds them on every
    /// core at once); the new ground's boxes made smaller only where their sides meet nothing (a tile rebuilt without the
    /// agent's margin must keep it at the ground's edge, but two boxes side by side must stay one surface); a box with
    /// the places something stands cut out of it; and the old mesh's triangles sorted into islands, so a rebuilt tile
    /// keeps only the old ground the seeds stand on (the city the player walks) and lets go of the unreachable patches
    /// the game baked behind its walls. Checked offline (NavCheck, NewGroundCheck.cs). At a load only; nothing per frame.
    /// </summary>
    public static class GroundShapes
    {
        public const float Touch = 0.01f;         // m: a side this near another box's ground counts as meeting it
        public const float OnLine = 0.01f;        // m: a corner this near a tile's border lies on it
        public const float SameHeight = 0.6f;     // m: two edges on one border join only at about the same height (a deck above a road does not)
        public const float SeedHeight = 1f;       // m: a seed belongs to a triangle at most this far above or below it

        // ---- tiles as rectangles

        /// <summary>
        /// The tiles as rectangles, each tile in exactly one, greedily: from the lowest row and column a run along the row,
        /// then down the rows while the whole run is there and not yet taken. Deterministic for a set in any order.
        /// </summary>
        public static void Rects(List<long> tiles, List<TileRect> into)
        {
            into.Clear();
            if (tiles == null || tiles.Count == 0)
            {
                return;
            }
            var left = new HashSet<long>(tiles);
            var order = new List<long>(left);
            order.Sort((a, b) =>
            {
                int za = TileGrid.KeyZ(a), zb = TileGrid.KeyZ(b);
                return za != zb ? za.CompareTo(zb) : TileGrid.KeyX(a).CompareTo(TileGrid.KeyX(b));
            });
            for (int i = 0; i < order.Count; i++)
            {
                long start = order[i];
                if (!left.Contains(start))
                {
                    continue;
                }
                int x0 = TileGrid.KeyX(start), z0 = TileGrid.KeyZ(start), x1 = x0, z1 = z0;
                while (left.Contains(TileGrid.Key(x1 + 1, z0)))
                {
                    x1++;
                }
                while (true)
                {
                    bool whole = true;
                    for (int x = x0; x <= x1 && whole; x++)
                    {
                        whole = left.Contains(TileGrid.Key(x, z1 + 1));
                    }
                    if (!whole)
                    {
                        break;
                    }
                    z1++;
                }
                for (int x = x0; x <= x1; x++)
                {
                    for (int z = z0; z <= z1; z++)
                    {
                        left.Remove(TileGrid.Key(x, z));
                    }
                }
                into.Add(new TileRect(x0, z0, x1, z1));
            }
        }

        /// <summary>
        /// A rectangle of tiles' ground and <see cref="GroundRules.TileOver"/> past its border all round: the bounds that
        /// rebuild exactly its tiles (Unity's builder rebuilds the tiles the bounds hold whole with their border, and a metre
        /// more holds no tile beside).
        /// </summary>
        public static void RectBounds(TileRect rect, TileGrid grid, out float minX, out float minZ, out float maxX, out float maxZ)
        {
            minX = grid.MinX(rect.X0) - GroundRules.TileOver;
            minZ = grid.MinZ(rect.Z0) - GroundRules.TileOver;
            maxX = grid.MinX(rect.X1 + 1) + GroundRules.TileOver;
            maxZ = grid.MinZ(rect.Z1 + 1) + GroundRules.TileOver;
        }

        /// <summary>The tiles a set of rectangles holds, counted.</summary>
        public static int Count(List<TileRect> rects)
        {
            int count = 0;
            for (int i = 0; i < rects.Count; i++)
            {
                count += rects[i].Count;
            }
            return count;
        }

        // ---- the agent's margin kept at the ground's edge only

        /// <summary>
        /// The boxes made smaller by <paramref name="by"/> only where a side meets no other box: a side lying on another
        /// box's ground (touching or overlapping it) stays where it is, so two boxes side by side are one surface, as Unity
        /// builds them in a tile with the agent's own margin. A side that meets other boxes along part of its length is cut
        /// there, each piece kept or made smaller on its own. A turned box is made smaller on every side, as before.
        /// </summary>
        public static void ShrinkOutward(List<GroundBox> boxes, float by, List<GroundBox> into)
        {
            into.Clear();
            var cover = new List<float>(16);
            var cutsX = new List<float>(8);
            var cutsZ = new List<float>(8);
            for (int b = 0; b < boxes.Count; b++)
            {
                GroundBox box = boxes[b];
                if (!Square(box))
                {
                    into.Add(GroundRules.Shrunk(box, by));
                    continue;
                }
                float x0 = box.X - box.HalfX, x1 = box.X + box.HalfX, z0 = box.Z - box.HalfZ, z1 = box.Z + box.HalfZ;
                cutsZ.Clear();
                cutsX.Clear();
                cutsZ.Add(z0);
                cutsZ.Add(z1);
                cutsX.Add(x0);
                cutsX.Add(x1);
                // Where along each side other boxes lie just beyond it: their ends cut the side into pieces.
                SideCover(boxes, b, true, x0 - Touch, z0, z1, cover);
                AddCuts(cover, z0, z1, cutsZ);
                SideCover(boxes, b, true, x1 + Touch, z0, z1, cover);
                AddCuts(cover, z0, z1, cutsZ);
                SideCover(boxes, b, false, z0 - Touch, x0, x1, cover);
                AddCuts(cover, x0, x1, cutsX);
                SideCover(boxes, b, false, z1 + Touch, x0, x1, cover);
                AddCuts(cover, x0, x1, cutsX);
                cutsX.Sort();
                cutsZ.Sort();
                for (int i = 0; i + 1 < cutsX.Count; i++)
                {
                    float px0 = cutsX[i], px1 = cutsX[i + 1];
                    if (px1 - px0 < 0.001f)
                    {
                        continue;
                    }
                    for (int j = 0; j + 1 < cutsZ.Count; j++)
                    {
                        float pz0 = cutsZ[j], pz1 = cutsZ[j + 1];
                        if (pz1 - pz0 < 0.001f)
                        {
                            continue;
                        }
                        float mz = (pz0 + pz1) * 0.5f, mx = (px0 + px1) * 0.5f;
                        // Only a piece's sides on the box's own sides can meet nothing; its other sides meet its neighbours.
                        float w = px0 == x0 && !Covered(boxes, b, x0 - Touch, mz) ? by : 0f;
                        float e = px1 == x1 && !Covered(boxes, b, x1 + Touch, mz) ? by : 0f;
                        float s = pz0 == z0 && !Covered(boxes, b, mx, z0 - Touch) ? by : 0f;
                        float n = pz1 == z1 && !Covered(boxes, b, mx, z1 + Touch) ? by : 0f;
                        float nx0 = px0 + w, nx1 = px1 - e, nz0 = pz0 + s, nz1 = pz1 - n;
                        if (nx1 - nx0 < 0.1f || nz1 - nz0 < 0.1f)
                        {
                            continue;   // a sliver the margin swallows whole
                        }
                        into.Add(new GroundBox((nx0 + nx1) * 0.5f, (nz0 + nz1) * 0.5f, box.Top, (nx1 - nx0) * 0.5f, (nz1 - nz0) * 0.5f, 0f, box.Depth));
                    }
                }
            }
        }

        private static bool Square(GroundBox box)
        {
            return Math.Abs(box.Yaw % 180f) < 0.01f;
        }

        // The stretches of a box's side that other boxes cover just beyond it: along z for a side at x = line, along x for one at z = line.
        private static void SideCover(List<GroundBox> boxes, int self, bool alongZ, float line, float from, float to, List<float> cover)
        {
            cover.Clear();
            for (int o = 0; o < boxes.Count; o++)
            {
                GroundBox other = boxes[o];
                if (o == self || !Square(other) || Math.Abs(other.Top - boxes[self].Top) > GroundRules.StepUp)
                {
                    continue;
                }
                float ox0 = other.X - other.HalfX, ox1 = other.X + other.HalfX, oz0 = other.Z - other.HalfZ, oz1 = other.Z + other.HalfZ;
                bool across = alongZ ? line >= ox0 && line <= ox1 : line >= oz0 && line <= oz1;
                float a = alongZ ? Math.Max(from, oz0) : Math.Max(from, ox0), b = alongZ ? Math.Min(to, oz1) : Math.Min(to, ox1);
                if (across && b > a)
                {
                    cover.Add(a);
                    cover.Add(b);
                }
            }
        }

        private static void AddCuts(List<float> cover, float from, float to, List<float> cuts)
        {
            for (int i = 0; i < cover.Count; i++)
            {
                float v = cover[i];
                if (v > from + 0.001f && v < to - 0.001f && !cuts.Contains(v))
                {
                    cuts.Add(v);
                }
            }
        }

        private static bool Covered(List<GroundBox> boxes, int self, float x, float z)
        {
            for (int o = 0; o < boxes.Count; o++)
            {
                if (o != self && Square(boxes[o]) && Math.Abs(boxes[o].Top - boxes[self].Top) <= GroundRules.StepUp && boxes[o].Covers(x, z, 0f))
                {
                    return true;
                }
            }
            return false;
        }

        // ---- a box with what stands on it cut out

        /// <summary>
        /// An unturned box with the given rectangles cut out of it, as boxes: the whole width south and north of each hole,
        /// the strips beside it only as long as it is, and none narrower than <paramref name="least"/> either way (a sliver
        /// nobody walks), so a narrow strip beside a hole is all that is let go.
        /// </summary>
        public static void Cut(GroundBox box, List<FlatRect> holes, float least, List<GroundBox> into)
        {
            var pieces = new List<FlatRect>(4) { new FlatRect(box.X - box.HalfX, box.Z - box.HalfZ, box.X + box.HalfX, box.Z + box.HalfZ) };
            var next = new List<FlatRect>(8);
            for (int h = 0; holes != null && h < holes.Count; h++)
            {
                FlatRect hole = holes[h];
                next.Clear();
                for (int p = 0; p < pieces.Count; p++)
                {
                    FlatRect piece = pieces[p];
                    if (hole.MaxX <= piece.MinX || hole.MinX >= piece.MaxX || hole.MaxZ <= piece.MinZ || hole.MinZ >= piece.MaxZ)
                    {
                        next.Add(piece);
                        continue;
                    }
                    // South and north of the hole the piece's whole width; west and east of it only as long as the hole.
                    if (hole.MinZ > piece.MinZ)
                    {
                        next.Add(new FlatRect(piece.MinX, piece.MinZ, piece.MaxX, hole.MinZ));
                    }
                    if (hole.MaxZ < piece.MaxZ)
                    {
                        next.Add(new FlatRect(piece.MinX, hole.MaxZ, piece.MaxX, piece.MaxZ));
                    }
                    float az = Math.Max(piece.MinZ, hole.MinZ), bz = Math.Min(piece.MaxZ, hole.MaxZ);
                    if (hole.MinX > piece.MinX)
                    {
                        next.Add(new FlatRect(piece.MinX, az, hole.MinX, bz));
                    }
                    if (hole.MaxX < piece.MaxX)
                    {
                        next.Add(new FlatRect(hole.MaxX, az, piece.MaxX, bz));
                    }
                }
                pieces.Clear();
                pieces.AddRange(next);
            }
            for (int p = 0; p < pieces.Count; p++)
            {
                FlatRect r = pieces[p];
                if (r.MaxX - r.MinX >= least && r.MaxZ - r.MinZ >= least)
                {
                    into.Add(new GroundBox((r.MinX + r.MaxX) * 0.5f, (r.MinZ + r.MaxZ) * 0.5f, box.Top, (r.MaxX - r.MinX) * 0.5f, (r.MaxZ - r.MinZ) * 0.5f, 0f, box.Depth));
                }
            }
        }

        // ---- the old mesh's islands

        /// <summary>
        /// The old mesh's triangles (three corners each in <paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>)
        /// sorted into islands: two triangles are one island when they share a corner, or when their edges lie on the same
        /// border of two tiles, overlap along it and stand at about the same height (Unity joins tiles there, and their
        /// corners need not meet). Each triangle's island, a number from 0, into <paramref name="island"/>; returns how many.
        /// </summary>
        public static int Islands(float[] x, float[] y, float[] z, int triangles, TileGrid grid, int[] island)
        {
            var parent = new int[triangles];
            for (int t = 0; t < triangles; t++)
            {
                parent[t] = t;
            }
            var corners = new Dictionary<long, int>(triangles * 2);
            for (int t = 0; t < triangles; t++)
            {
                for (int c = 0; c < 3; c++)
                {
                    long key = CornerKey(x[t * 3 + c], y[t * 3 + c], z[t * 3 + c]);
                    if (corners.TryGetValue(key, out int other))
                    {
                        Union(parent, t, other);
                    }
                    else
                    {
                        corners[key] = t;
                    }
                }
            }
            // Edges lying on a tile's border, by the border's line: sorted along it, those that overlap are joined.
            var edges = new List<BorderEdge>(triangles / 4 + 8);
            for (int t = 0; grid.Valid && t < triangles; t++)
            {
                for (int c = 0; c < 3; c++)
                {
                    int a = t * 3 + c, b = t * 3 + (c + 1) % 3;
                    if (OnLineX(x[a], grid, out int lx) && OnLineX(x[b], grid, out int lb) && lx == lb && Math.Abs(z[a] - z[b]) > OnLine)
                    {
                        edges.Add(new BorderEdge { Line = (long)lx * 2, From = Math.Min(z[a], z[b]), To = Math.Max(z[a], z[b]), Y = (y[a] + y[b]) * 0.5f, Triangle = t });
                    }
                    if (OnLineZ(z[a], grid, out int lz) && OnLineZ(z[b], grid, out int lzb) && lz == lzb && Math.Abs(x[a] - x[b]) > OnLine)
                    {
                        edges.Add(new BorderEdge { Line = (long)lz * 2 + 1, From = Math.Min(x[a], x[b]), To = Math.Max(x[a], x[b]), Y = (y[a] + y[b]) * 0.5f, Triangle = t });
                    }
                }
            }
            edges.Sort((p, q) => p.Line != q.Line ? p.Line.CompareTo(q.Line) : p.From.CompareTo(q.From));
            for (int i = 0; i < edges.Count; i++)
            {
                for (int j = i + 1; j < edges.Count && edges[j].Line == edges[i].Line && edges[j].From < edges[i].To - OnLine; j++)
                {
                    if (Math.Abs(edges[j].Y - edges[i].Y) <= SameHeight)
                    {
                        Union(parent, edges[i].Triangle, edges[j].Triangle);
                    }
                }
            }
            var number = new Dictionary<int, int>(64);
            for (int t = 0; t < triangles; t++)
            {
                int root = Find(parent, t);
                if (!number.TryGetValue(root, out int n))
                {
                    n = number.Count;
                    number[root] = n;
                }
                island[t] = n;
            }
            return number.Count;
        }

        /// <summary>
        /// Which islands a seed stands on: a seed lies on a triangle seen from above, at most <see cref="SeedHeight"/> from
        /// its height. <paramref name="seeded"/> is sized by the caller to the islands' count.
        /// </summary>
        public static int Seeded(float[] x, float[] y, float[] z, int triangles, int[] island, List<GroundPoint> seeds, bool[] seeded)
        {
            int found = 0;
            for (int s = 0; seeds != null && s < seeds.Count; s++)
            {
                GroundPoint seed = seeds[s];
                for (int t = 0; t < triangles; t++)
                {
                    int a = t * 3;
                    float mid = (y[a] + y[a + 1] + y[a + 2]) / 3f;
                    if (Math.Abs(mid - seed.Y) > SeedHeight || seeded[island[t]])
                    {
                        continue;
                    }
                    if (GroundRules.InTriangle(seed.X, seed.Z, x[a], z[a], x[a + 1], z[a + 1], x[a + 2], z[a + 2]))
                    {
                        seeded[island[t]] = true;
                        found++;
                    }
                }
            }
            return found;
        }

        private struct BorderEdge
        {
            public long Line;      // a tile border line: twice its column for one along z, twice its row and one for one along x
            public float From;
            public float To;
            public float Y;
            public int Triangle;
        }

        private static bool OnLineX(float x, TileGrid grid, out int line)
        {
            float at = (x - grid.OriginX) / grid.Side;
            line = (int)Math.Round(at);
            return Math.Abs(at - line) * grid.Side <= OnLine;
        }

        private static bool OnLineZ(float z, TileGrid grid, out int line)
        {
            float at = (z - grid.OriginZ) / grid.Side;
            line = (int)Math.Round(at);
            return Math.Abs(at - line) * grid.Side <= OnLine;
        }

        // A corner's place to a millimetre (a centimetre in height): the same corner of two triangles has the same key.
        private static long CornerKey(float x, float y, float z)
        {
            long qx = (long)Math.Round(x * 1000.0) & 0x7FFFFF;   // 23 bits each way: 4 km either side of the origin
            long qz = (long)Math.Round(z * 1000.0) & 0x7FFFFF;
            long qy = (long)Math.Round(y * 100.0) & 0x3FFFF;     // 18 bits: a kilometre up or down
            return (qx << 41) | (qz << 18) | qy;
        }

        private static int Find(int[] parent, int t)
        {
            while (parent[t] != t)
            {
                parent[t] = parent[parent[t]];
                t = parent[t];
            }
            return t;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a), rb = Find(parent, b);
            if (ra != rb)
            {
                parent[Math.Max(ra, rb)] = Math.Min(ra, rb);
            }
        }
    }
}
