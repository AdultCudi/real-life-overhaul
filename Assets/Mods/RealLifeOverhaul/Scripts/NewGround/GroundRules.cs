using System;
using System.Collections.Generic;
using System.Globalization;

namespace RealLifeOverhaul.NewGround
{
    /// <summary>
    /// The tile grid of one of the game's walking meshes, read from the game at every load: the mesh's own position
    /// (NavMeshData.position), from which its tiles are laid, and a tile's side (its tile size in voxels times the voxel
    /// size, from the surface's build settings; 512 x 0.07 = 35.84 m in this build). Two tiles join only where walkable
    /// polygons meet on their shared border, so new ground is laid tile by tile on this grid.
    /// </summary>
    public struct TileGrid
    {
        public float OriginX;   // m
        public float OriginZ;
        public float Side;      // m

        public TileGrid(float originX, float originZ, float side)
        {
            OriginX = originX;
            OriginZ = originZ;
            Side = side;
        }

        public bool Valid => Side > 0.5f;

        public int TileX(float x)
        {
            return (int)Math.Floor((x - OriginX) / Side);
        }

        public int TileZ(float z)
        {
            return (int)Math.Floor((z - OriginZ) / Side);
        }

        public float MinX(int tx)
        {
            return OriginX + tx * Side;
        }

        public float MinZ(int tz)
        {
            return OriginZ + tz * Side;
        }

        /// <summary>One number for a tile, for sets and lists.</summary>
        public static long Key(int tx, int tz)
        {
            return ((long)tx << 32) | (uint)tz;
        }

        public static int KeyX(long key)
        {
            return (int)(key >> 32);
        }

        public static int KeyZ(long key)
        {
            return (int)(key & 0xffffffffL);
        }
    }

    /// <summary>
    /// A box of new ground, as the walking meshes are built from it: the height of its walking surface, its size, the way
    /// it is turned (degrees about y, as Unity turns: its own z axis points along (sin yaw, cos yaw)). The builder fills
    /// it as a solid and walks on its top.
    /// </summary>
    public struct GroundBox
    {
        public float X;        // m: its middle
        public float Z;
        public float Top;      // m: the height of its walking surface
        public float HalfX;    // m: half its size along its own x
        public float HalfZ;    // m: half its size along its own z
        public float Yaw;      // degrees
        public float Depth;    // m below its top

        public GroundBox(float x, float z, float top, float halfX, float halfZ, float yaw, float depth)
        {
            X = x;
            Z = z;
            Top = top;
            HalfX = halfX;
            HalfZ = halfZ;
            Yaw = yaw;
            Depth = depth;
        }

        /// <summary>A point of the box's own frame (along its x and z) in the world.</summary>
        public void World(float alongX, float alongZ, out float x, out float z)
        {
            double a = Yaw * Math.PI / 180.0;
            float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
            x = X + alongX * cos + alongZ * sin;
            z = Z - alongX * sin + alongZ * cos;
        }

        /// <summary>True for a point over the box's top, within <paramref name="margin"/> m of it.</summary>
        public bool Covers(float x, float z, float margin)
        {
            double a = Yaw * Math.PI / 180.0;
            float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
            float dx = x - X, dz = z - Z;
            float alongX = dx * cos - dz * sin;
            float alongZ = dx * sin + dz * cos;
            return Math.Abs(alongX) <= HalfX + margin && Math.Abs(alongZ) <= HalfZ + margin;
        }

        /// <summary>The box's extent on the ground, along the world's axes.</summary>
        public void Extent(out float minX, out float minZ, out float maxX, out float maxZ)
        {
            minX = float.MaxValue;
            minZ = float.MaxValue;
            maxX = float.MinValue;
            maxZ = float.MinValue;
            for (int c = 0; c < 4; c++)
            {
                World((c & 1) == 0 ? -HalfX : HalfX, (c & 2) == 0 ? -HalfZ : HalfZ, out float x, out float z);
                minX = Math.Min(minX, x);
                minZ = Math.Min(minZ, z);
                maxX = Math.Max(maxX, x);
                maxZ = Math.Max(maxZ, z);
            }
        }
    }

    /// <summary>
    /// New ground to walk on: boxes of the mod's own (pavements, plots' fronts, squares), and how many of the game's
    /// colliders stood on them when the plan was made (0 for open ground). At every load the game's colliders there are
    /// counted again and the plan stands down if there are more: a game update has built there since.
    /// </summary>
    public sealed class GroundPlan
    {
        public string Name;
        public readonly List<GroundBox> Boxes = new List<GroundBox>(8);
        public int ExpectColliders;     // the game's colliders on its boxes when the plan was made
        public bool Player = true;      // grow the player's walking mesh
        public bool Npc = true;         // grow the pedestrians'
    }

    /// <summary>
    /// The walking side of new ground, game-free: tiles under a box, the boxes the tests lay (a patch beside the player,
    /// a strip from the pavement into an empty tile), the points sampled to say what walking mesh is where and that none
    /// changed elsewhere, and the self-check lines. Checked offline (NavCheck, NewGroundCheck.cs). Nothing per frame.
    /// </summary>
    public static class GroundRules
    {
        public const float RingRadius = 10f;        // m: the 8 points round the player that "what is under me" samples
        public const int RingPoints = 9;            // the player's own and the 8 round
        public const float SampleReach = 1f;        // m: a walking mesh within this of a point counts as under it
        public const float PatchSide = 20f;         // m: the test patch's side
        public const float PatchGap = 2f;           // m between the player and the patch's near side
        public const float PatchDepth = 0.5f;       // m: the patch's and the strip's boxes below their top
        public const float KeepSeconds = 60f;       // s of play a test's ground stays before it is given back
        public const float LinkWidth = 2f;          // m: the fallback's link
        public const float StripWidth = 3f;         // m: the strip grown from the pavement into an empty tile
        public const float StripInto = 6f;          // m the strip reaches into the empty tile
        public const float PropMost = 2f;           // m across at most for a prop at a plan's edge that its count leaves out (see IsProp)

        /// <summary>
        /// A collider a plan's count leaves out: a prop no wider than <see cref="PropMost"/> either way (a lamp, a tree,
        /// a bin or a bench at the edge of a pavement). The offline scans hold no round colliders, so a plan cannot have
        /// counted them, and a game update builds walls, roads and buildings, never a lone lamp.
        /// </summary>
        public static bool IsProp(bool propLayer, float sizeX, float sizeZ)
        {
            return propLayer && sizeX <= PropMost && sizeZ <= PropMost;
        }
        public const float JoinOverlap = 0.5f;      // m new ground reaches back onto old ground, past where the old mesh stops short of the kerb
        public const float OldMargin = 0.5f;        // m of the old mesh kept round rebuilt tiles, so their borders build as before
        public const float LongestStrip = 40f;      // m: a strip from further than this to the tile's border is not laid
        public const int TileSamples = 5;           // points a side sampled to say whether a tile has a walking mesh
        public const int PrintSamples = 3;          // points a side of each tile round the grown ones, sampled before and after
        public const float PrintAlike = 0.05f;      // m: a sample's hit moved less than this is unchanged
        public const float StepUp = 0.3f;           // m: ground a box's top may sit above or below the pavement it is flush with
        public const float FlatEnough = 1.5f;       // m: ground under a patch's corners may differ by this much
        public const float TileEdge = 0.01f;        // m a one-tile build's bounds keep inside its tile (a seventh of a voxel), so the builder never takes the tile beside for its own

        /// <summary>A box made smaller on every side: new ground in a tile built without the agent's own margin keeps that margin itself.</summary>
        public static GroundBox Shrunk(GroundBox box, float by)
        {
            return new GroundBox(box.X, box.Z, box.Top, Math.Max(0.05f, box.HalfX - by), Math.Max(0.05f, box.HalfZ - by), box.Yaw, box.Depth);
        }

        /// <summary>One tile's ground, kept <see cref="TileEdge"/> inside its border: the bounds that rebuild that tile alone.</summary>
        public static void TileBounds(long tile, TileGrid grid, out float minX, out float minZ, out float maxX, out float maxZ)
        {
            minX = grid.MinX(TileGrid.KeyX(tile)) + TileEdge;
            minZ = grid.MinZ(TileGrid.KeyZ(tile)) + TileEdge;
            maxX = minX + grid.Side - 2f * TileEdge;
            maxZ = minZ + grid.Side - 2f * TileEdge;
        }

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The 9 points "what is under me" samples: the player's own, then 8 at <see cref="RingRadius"/> round, from north clockwise.</summary>
        public static void Ring(float x, float z, float[] xs, float[] zs)
        {
            xs[0] = x;
            zs[0] = z;
            for (int i = 0; i < RingPoints - 1; i++)
            {
                double a = i * Math.PI / 4.0;
                xs[i + 1] = x + (float)(Math.Sin(a) * RingRadius);
                zs[i + 1] = z + (float)(Math.Cos(a) * RingRadius);
            }
        }

        /// <summary>The 8 headings (degrees, north clockwise) a test tries in turn.</summary>
        public static float Heading(int i)
        {
            return (i & 7) * 45f;
        }

        /// <summary>The test patch: <see cref="PatchSide"/> square, its near side <see cref="PatchGap"/> from the player, along a heading.</summary>
        public static GroundBox Patch(float x, float z, float top, float heading)
        {
            double a = heading * Math.PI / 180.0;
            float reach = PatchGap + PatchSide * 0.5f;
            return new GroundBox(x + (float)Math.Sin(a) * reach, z + (float)Math.Cos(a) * reach, top, PatchSide * 0.5f, PatchSide * 0.5f, heading, PatchDepth);
        }

        /// <summary>The middle of a patch's side nearest the player, a little inside it: where the fallback's link ends.</summary>
        public static void NearSide(GroundBox patch, out float x, out float z)
        {
            patch.World(0f, -patch.HalfZ + 0.5f, out x, out z);
        }

        /// <summary>
        /// The strip that grows a walking mesh from the pavement into the empty tile beside: from <see cref="JoinOverlap"/>
        /// behind the pavement point, along one of the tile grid's four headings (0, 90, 180, 270 degrees), to
        /// <see cref="StripInto"/> past the border between the two tiles. False when the border is further than
        /// <see cref="LongestStrip"/> or the heading is not one of the four.
        /// </summary>
        public static bool Strip(float x, float z, float top, int heading, TileGrid grid, out GroundBox strip, out float toBorder)
        {
            strip = default;
            toBorder = 0f;
            if (!grid.Valid || heading % 90 != 0)
            {
                return false;
            }
            int tx = grid.TileX(x), tz = grid.TileZ(z);
            switch ((heading % 360 + 360) % 360)
            {
                case 0:
                    toBorder = grid.MinZ(tz + 1) - z;
                    break;
                case 90:
                    toBorder = grid.MinX(tx + 1) - x;
                    break;
                case 180:
                    toBorder = z - grid.MinZ(tz);
                    break;
                default:
                    toBorder = x - grid.MinX(tx);
                    break;
            }
            if (toBorder > LongestStrip)
            {
                return false;
            }
            float from = -JoinOverlap, to = toBorder + StripInto;
            double a = heading * Math.PI / 180.0;
            float mid = (from + to) * 0.5f;
            strip = new GroundBox(x + (float)Math.Sin(a) * mid, z + (float)Math.Cos(a) * mid, top, StripWidth * 0.5f, (to - from) * 0.5f, heading, PatchDepth);
            return true;
        }

        /// <summary>The tile next to a point's own along one of the four headings.</summary>
        public static long Beside(float x, float z, int heading, TileGrid grid)
        {
            int tx = grid.TileX(x), tz = grid.TileZ(z);
            switch ((heading % 360 + 360) % 360)
            {
                case 0:
                    return TileGrid.Key(tx, tz + 1);
                case 90:
                    return TileGrid.Key(tx + 1, tz);
                case 180:
                    return TileGrid.Key(tx, tz - 1);
                default:
                    return TileGrid.Key(tx - 1, tz);
            }
        }

        /// <summary>Every tile a box covers, its extent grown by <paramref name="margin"/> m, in order (x then z).</summary>
        public static void TilesUnder(GroundBox box, TileGrid grid, float margin, List<long> into)
        {
            into.Clear();
            box.Extent(out float minX, out float minZ, out float maxX, out float maxZ);
            int x0 = grid.TileX(minX - margin), x1 = grid.TileX(maxX + margin);
            int z0 = grid.TileZ(minZ - margin), z1 = grid.TileZ(maxZ + margin);
            for (int tx = x0; tx <= x1; tx++)
            {
                for (int tz = z0; tz <= z1; tz++)
                {
                    into.Add(TileGrid.Key(tx, tz));
                }
            }
        }

        /// <summary>The ground a set of tiles covers, along the world's axes.</summary>
        public static void Extent(List<long> tiles, TileGrid grid, out float minX, out float minZ, out float maxX, out float maxZ)
        {
            minX = float.MaxValue;
            minZ = float.MaxValue;
            maxX = float.MinValue;
            maxZ = float.MinValue;
            for (int i = 0; i < tiles.Count; i++)
            {
                float x = grid.MinX(TileGrid.KeyX(tiles[i])), z = grid.MinZ(TileGrid.KeyZ(tiles[i]));
                minX = Math.Min(minX, x);
                minZ = Math.Min(minZ, z);
                maxX = Math.Max(maxX, x + grid.Side);
                maxZ = Math.Max(maxZ, z + grid.Side);
            }
        }

        /// <summary>The tiles that touch a set of tiles (corners too) and are not in it: where "unchanged elsewhere" is sampled.</summary>
        public static void Round(List<long> tiles, List<long> into)
        {
            into.Clear();
            var inside = new HashSet<long>(tiles);
            for (int i = 0; i < tiles.Count; i++)
            {
                int tx = TileGrid.KeyX(tiles[i]), tz = TileGrid.KeyZ(tiles[i]);
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        long key = TileGrid.Key(tx + dx, tz + dz);
                        if (!inside.Contains(key) && !into.Contains(key))
                        {
                            into.Add(key);
                        }
                    }
                }
            }
        }

        /// <summary>Points spread over a tile, <paramref name="perSide"/> a side, away from its edges: appended to the lists.</summary>
        public static void Spread(long tile, TileGrid grid, int perSide, List<float> xs, List<float> zs)
        {
            float x0 = grid.MinX(TileGrid.KeyX(tile)), z0 = grid.MinZ(TileGrid.KeyZ(tile));
            for (int i = 0; i < perSide; i++)
            {
                for (int j = 0; j < perSide; j++)
                {
                    xs.Add(x0 + grid.Side * (i + 0.5f) / perSide);
                    zs.Add(z0 + grid.Side * (j + 0.5f) / perSide);
                }
            }
        }

        /// <summary>True for a point on a tile's ground (its border counts to the tile it starts).</summary>
        public static bool OnTile(float x, float z, long tile, TileGrid grid)
        {
            return grid.TileX(x) == TileGrid.KeyX(tile) && grid.TileZ(z) == TileGrid.KeyZ(tile);
        }

        /// <summary>The reach a sample needs to find any walking mesh on its share of a tile: half the diagonal of its square and a little.</summary>
        public static float SpreadReach(TileGrid grid, int perSide)
        {
            return grid.Side / perSide * 0.75f;
        }

        /// <summary>
        /// How many samples are the same before and after: the same found or not found, and where found, within
        /// <see cref="PrintAlike"/> of each other. Both prints hold x, y, z for each sample, NaN where none was found.
        /// </summary>
        public static int Alike(float[] before, float[] after)
        {
            if (before == null || after == null || before.Length != after.Length)
            {
                return 0;
            }
            int same = 0;
            for (int i = 0; i + 2 < before.Length; i += 3)
            {
                bool was = !float.IsNaN(before[i]), now = !float.IsNaN(after[i]);
                if (was != now)
                {
                    continue;
                }
                if (!was)
                {
                    same++;
                    continue;
                }
                float dx = before[i] - after[i], dy = before[i + 1] - after[i + 1], dz = before[i + 2] - after[i + 2];
                same += dx * dx + dy * dy + dz * dz <= PrintAlike * PrintAlike ? 1 : 0;
            }
            return same;
        }

        /// <summary>True when a point (x, z) is inside a triangle seen from above, its edges counted in.</summary>
        public static bool InTriangle(float x, float z, float ax, float az, float bx, float bz, float cx, float cz)
        {
            float d1 = (x - bx) * (az - bz) - (ax - bx) * (z - bz);
            float d2 = (x - cx) * (bz - cz) - (bx - cx) * (z - cz);
            float d3 = (x - ax) * (cz - az) - (cx - ax) * (z - az);
            bool negative = d1 < -1e-4f || d2 < -1e-4f || d3 < -1e-4f;
            bool positive = d1 > 1e-4f || d2 > 1e-4f || d3 > 1e-4f;
            return !(negative && positive);
        }

        /// <summary>Tiles a side of a square of ground covers on a grid, both ways: the build's size in tiles.</summary>
        public static int TilesOf(float minX, float minZ, float maxX, float maxZ, TileGrid grid)
        {
            if (!grid.Valid || maxX < minX || maxZ < minZ)
            {
                return 0;
            }
            return (grid.TileX(maxX) - grid.TileX(minX) + 1) * (grid.TileZ(maxZ) - grid.TileZ(minZ) + 1);
        }

        // ---- the self-check lines, one per event

        /// <summary>"Walk check (navmesh here): Player 9 of 9, NPC 3 of 9 at (x, z)".</summary>
        public static string UnderLine(string first, int firstFound, string second, int secondFound, float x, float z)
        {
            return "Walk check (navmesh here): " + first + " " + firstFound + " of " + RingPoints + ", " + second + " " + secondFound + " of " + RingPoints
                   + " at (" + x.ToString("0.0", Inv) + ", " + z.ToString("0.0", Inv) + ")";
        }

        /// <summary>"Walk check (runtime build): Player N tiles in N ms, NPC N tiles in N ms, sample PASS, path from the player PARTIAL".</summary>
        public static string BuildLine(string first, int firstTiles, double firstMs, string second, int secondTiles, double secondMs, bool sampled, string path)
        {
            return "Walk check (runtime build): " + first + " " + firstTiles + " tiles in " + firstMs.ToString("0", Inv) + " ms, " + second + " " + secondTiles
                   + " tiles in " + secondMs.ToString("0", Inv) + " ms, sample " + (sampled ? "PASS" : "FAIL (no polygon of the patch found at its middle)")
                   + ", path from the player " + path;
        }

        /// <summary>What the game's meshes round a point are worth as build sources.</summary>
        public static string SourcesLine(int meshes, int readable, int batched, int other, float metres)
        {
            return "Walk check (sources): " + meshes + " of the game's meshes within " + metres.ToString("0", Inv) + " m on the walking mesh's layers, "
                   + readable + " readable as build sources, " + batched + " static-batched, " + other + " other sources (terrain, boxes); "
                   + (meshes > 0 && readable == meshes ? "the game's own meshes can rebuild a tile here"
                       : "new ground is built from the mod's own shapes and the old mesh's own triangles");
        }

        /// <summary>The grow test's line for one walking mesh.</summary>
        public static string GrowLine(string mesh, string alone, string joined, string from, int alike, int samples)
        {
            bool complete = joined == "COMPLETE";
            return "Walk check (grow one tile, " + mesh + "): the new tile alone: path " + alone + "; with the tile beside rebuilt from " + from
                   + ": path pavement to new ground " + joined + ": " + (complete ? "PASS" : "FAIL") + ", polygons elsewhere "
                   + (alike == samples ? "unchanged" : "CHANGED (" + (samples - alike) + " of " + samples + " samples moved)");
        }

        /// <summary>The line when what was grown is given back.</summary>
        public static string BackLine(string mesh, int tilesAlike, int tileSamples, int roundAlike, int roundSamples)
        {
            bool back = tilesAlike == tileSamples && roundAlike == roundSamples;
            return "Walk check (given back, " + mesh + "): the grown tiles " + tilesAlike + " of " + tileSamples + " samples as before, elsewhere "
                   + roundAlike + " of " + roundSamples + ": " + (back ? "PASS" : "FAIL (load the save again to put the walking mesh right)");
        }

        /// <summary>The fallback's line: a path over the link.</summary>
        public static string LinkLine(string first, string firstPath, string second, string secondPath)
        {
            bool complete = firstPath == "COMPLETE" && secondPath == "COMPLETE";
            return "Walk check (link): path to the patch " + first + " " + firstPath + ", " + second + " " + secondPath + ": " + (complete ? "PASS" : "FAIL");
        }

        /// <summary>Pedestrians on new ground: a zone copied from the game's own on the patch, and the most people seen on it.</summary>
        public static string PeopleLine(bool zoned, int most, float seconds)
        {
            return "Walk check (pedestrians on new ground): " + (zoned ? "a zone of the game's own copied onto the patch, " : "no zone could be copied, ")
                   + "at most " + most + " people on the patch in " + seconds.ToString("0", Inv) + " s: " + (zoned && most > 0 ? "PASS" : "FAIL");
        }

        /// <summary>The dry run's line: Unity's builder tried on data of the mod's own before the city's is touched.</summary>
        public static string DryLine(string mesh, string problem, float margin, double ms)
        {
            return "Walk check (dry run, " + mesh + "): two tiles built together join, a tile rebuilt alone keeps the tile beside and its join, an empty rebuild clears it: "
                   + (problem == null ? "PASS (old tiles rebuilt with a margin of " + margin.ToString("0.00", Inv) + " m)" : "FAIL, " + problem)
                   + ", in " + ms.ToString("0", Inv) + " ms";
        }
    }
}
