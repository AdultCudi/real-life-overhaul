using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.NewGround
{
    /// <summary>Which of a job's three passes is queued: the empty tiles, the old tiles new ground joins, or everything given back.</summary>
    public enum GroundPass
    {
        None,
        Fresh,
        Join,
        Back
    }

    /// <summary>
    /// One city walking mesh grown over new ground: which tiles the boxes cover, which of them hold old ground the seeds
    /// stand on (they are rebuilt from its own triangles and the new ground together, so the two join on one surface), the
    /// rectangles of tiles each pass updates at once, the sources of each pass, and prints of the samples round about taken
    /// before, to say nothing else moved.
    /// </summary>
    public sealed class GroundJob
    {
        public CityMesh Mesh;
        public readonly List<GroundBox> Boxes = new List<GroundBox>(8);
        public readonly List<GroundBox> Obstacles = new List<GroundBox>(8);   // what stands on the new ground: no walking surface under it
        public readonly List<GroundPoint> Seeds = new List<GroundPoint>(4);    // points of the old ground the new joins
        public readonly List<long> Covered = new List<long>(8);    // every tile under the boxes
        public readonly List<long> Fresh = new List<long>(8);      // ...of them, those with no old ground joined to the seeds
        public readonly List<long> Joining = new List<long>(8);    // ...those with it, rebuilt with the new ground
        public readonly List<long> Round = new List<long>(16);     // the tiles round the covered ones, which must not change
        public readonly List<TileRect> FreshRects = new List<TileRect>(4);
        public readonly List<TileRect> JoinRects = new List<TileRect>(4);
        public readonly List<TileRect> BackRects = new List<TileRect>(4);
        public readonly List<Mesh> Old = new List<Mesh>(2);        // the kept old ground's own triangles, one mesh per area
        public readonly List<int> OldAreas = new List<int>(2);
        public readonly List<Mesh> Others = new List<Mesh>(2);     // the old ground let go (islands no seed stands on), for giving back
        public readonly List<int> OtherAreas = new List<int>(2);
        public int OldTriangles;           // kept
        public int OtherTriangles;         // let go: walking mesh the game baked where nothing walks
        public int Islands;                // the old mesh's islands round the new ground
        public int SeededIslands;          // ...and those a seed stands on
        public readonly List<NavMeshBuildSource> FreshSources = new List<NavMeshBuildSource>(8);
        public readonly List<NavMeshBuildSource> JoinSources = new List<NavMeshBuildSource>(8);
        public readonly List<NavMeshBuildSource> BackSources = new List<NavMeshBuildSource>(4);
        public readonly List<float> RoundX = new List<float>(144);
        public readonly List<float> RoundZ = new List<float>(144);
        public readonly List<float> CoveredX = new List<float>(64);   // samples on the joining tiles
        public readonly List<float> CoveredZ = new List<float>(64);
        public float[] RoundBefore;
        public float[] CoveredBefore;
        public float Height;               // m: the height prints are sampled round
        public string Problem;             // why the job stood down, or null
        public GroundPass Pass;
        public double Ms;                  // the builder's time so far, for the log
        public int Updates;                // updates asked of Unity's builder so far
        internal float[] CandX, CandY, CandZ;   // the old ground's triangles under the job, both meshes', three corners each
        internal int[] CandAreas;
        internal bool[] CandKept;                // ...kept: on an island a seed stands on, and this mesh's
        internal int CandCount;
        internal Dictionary<long, List<int>> CandByTile;   // each triangle under every tile it lies over, for the check
        internal bool BackReady;                 // the sources for giving back are made
        internal readonly Queue<TileRect> Pending = new Queue<TileRect>(8);
        internal AsyncOperation Running;
        internal long Started;
        internal NavMeshBuildSettings Own;
        internal NavMeshBuildSettings Flat;
    }

    /// <summary>
    /// The walking core of new ground. The game's walking meshes were baked in the editor and the game never builds one, so
    /// this proves Unity's run-time builder first on data of its own far below the city (<see cref="DryRun"/>: a tile
    /// rebuilt alone keeps the tile beside and its join to it, an empty rebuild clears its tile), then grows the city's
    /// own data (NavMeshBuilder.UpdateNavMeshData, the tiles beyond kept as they are): the tiles holding no old ground the
    /// seeds stand on from the new ground with the agent's own margin, then the tiles holding it from that old ground's own
    /// triangles and the new ground, built without a second margin (the triangles keep the agent's distance already; the
    /// new ground's boxes keep it themselves where they meet nothing), so old and new are one surface the keys walk
    /// across. Old ground no seed stands on (what the game baked behind its walls, where nothing walks) is let go in the
    /// tiles rebuilt, so the new ground joins the city only where the plan joins it. Whatever stands on the new ground is
    /// built as no walking surface. The tiles go in rectangles, each one update with its bounds a metre past it: Unity's
    /// builder rebuilds the tiles the bounds hold whole and builds many on every core at once (proven in a built player of
    /// the game's Unity). Giving back rebuilds the covered tiles from all the old triangles. The separate mesh joined by
    /// links (<see cref="WalkGame.Link"/>) is the fallback the tests weigh it against. Heavy steps behind a pause or the
    /// loading screen; nothing per frame otherwise.
    /// </summary>
    public static class GroundBuilder
    {
        public const float DryDepth = 400f;     // m below the player the dry run's data stands, where nothing walks
        public const float DryLength = 20f;     // m: its strip across the border of its two tiles
        public const float DryWidth = 4f;       // m
        public const float FallbackMargin = 0.01f;   // m: the margin asked of the builder for old tiles if it refuses none (one voxel once rounded up)
        public const float BackInset = 2f;      // m a clearing box keeps off its tile's edges: clear of the border Unity builds round the tiles beside
        public const float HeightBelow = 200f;  // m below the ground's height an update's bounds reach
        public const float HeightAbove = 300f;  // m above it

        private static readonly List<NavMeshBuildSource> Scratch = new List<NavMeshBuildSource>(4);

        /// <summary>
        /// Why the dry run's last step failed, or null: an emptied tile not cleared. Only giving back needs it, which a
        /// district laid whole never does at its load, so it is said in the log and the ground is grown all the same.
        /// </summary>
        public static string ClearMiss { get; private set; }

        // A box well inside a tile, of nothing walkable, that a tile is cleared by rebuilding it from.
        private static GroundBox Inside(long tile, TileGrid grid, float top)
        {
            float minX = grid.MinX(TileGrid.KeyX(tile)), minZ = grid.MinZ(TileGrid.KeyZ(tile)), half = grid.Side * 0.5f;
            return new GroundBox(minX + half, minZ + half, top, Math.Max(0.5f, half - BackInset), Math.Max(0.5f, half - BackInset), 0f, GroundRules.PatchDepth);
        }

        /// <summary>
        /// Unity's run-time builder tried on data of the mod's own, with the city mesh's settings: two tiles built together
        /// join; one rebuilt alone without the agent's margin keeps the tile beside and the join; an empty rebuild clears
        /// its tile and keeps the one beside. Null when all hold; else which did not. Small and done now.
        /// </summary>
        public static string DryRun(CityMesh mesh, Vector3 near, out double ms, out float flatRadius)
        {
            ms = 0.0;
            flatRadius = 0f;
            ClearMiss = null;
            if (mesh == null)
            {
                return "no walking mesh";
            }
            float y = near.y - DryDepth;
            TileGrid grid = mesh.Grid;
            int tx = grid.TileX(near.x), tz = grid.TileZ(near.z);
            float border = grid.MinX(tx + 1), middleZ = grid.MinZ(tz) + grid.Side * 0.5f;
            var strip = new GroundBox(border, middleZ, y, DryLength * 0.5f, DryWidth * 0.5f, 0f, GroundRules.PatchDepth);
            NavMeshBuildSettings own = mesh.Settings, flat = mesh.Settings;
            flat.agentRadius = 0f;
            Scratch.Clear();
            Scratch.Add(WalkGame.Box(strip, mesh.Area));
            long left = TileGrid.Key(tx, tz), right = TileGrid.Key(tx + 1, tz);
            GroundRules.TileBounds(left, grid, out float minX, out float minZ, out _, out float maxZ);
            GroundRules.TileBounds(right, grid, out _, out _, out float maxX, out _);
            NavMeshData data = WalkGame.Build(mesh, own, Scratch, WalkGame.Local(mesh, minX, minZ, maxX, maxZ, y - 20f, y + 20f), out double built);
            ms += built;
            NavMeshDataInstance instance = data != null ? WalkGame.Add(data) : default;
            try
            {
                if (data == null || !instance.valid)
                {
                    return "Unity's builder made no walking mesh of its own here";
                }
                var a = new Vector3(border - DryLength * 0.25f, y, middleZ);
                var b = new Vector3(border + DryLength * 0.25f, y, middleZ);
                if (!WalkGame.Sample(mesh, a, 1f, out Vector3 onA) || !WalkGame.Sample(mesh, b, 1f, out Vector3 onB))
                {
                    return "a strip over two tiles built together has no walking surface";
                }
                if (WalkGame.Path(mesh, onA, onB) != "COMPLETE")
                {
                    return "two tiles built together do not join (" + WalkGame.Path(mesh, onA, onB) + ")";
                }
                GroundRules.TileBounds(left, grid, out float lx0, out float lz0, out float lx1, out float lz1);
                Bounds leftOnly = WalkGame.Local(mesh, lx0, lz0, lx1, lz1, y - 20f, y + 20f);
                long began = Stopwatch.GetTimestamp();
                bool rebuilt = false;
                for (int attempt = 0; attempt < 2 && !rebuilt; attempt++)
                {
                    // No margin at all first; if Unity refuses that, a margin of one voxel.
                    flat.agentRadius = attempt == 0 ? 0f : FallbackMargin;
                    Scratch.Clear();
                    Scratch.Add(WalkGame.Box(GroundRules.Shrunk(strip, own.agentRadius - flat.agentRadius), mesh.Area));
                    rebuilt = WalkGame.UpdateNow(data, flat, Scratch, leftOnly);
                }
                ms += (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
                if (!rebuilt)
                {
                    return "Unity's builder would not rebuild a tile alone with a margin smaller than the agent's";
                }
                flatRadius = flat.agentRadius;
                if (!WalkGame.Sample(mesh, b, 1f, out onB) || !WalkGame.Sample(mesh, a, 1f, out onA))
                {
                    return "a tile rebuilt alone lost the tile beside it";
                }
                string kept = WalkGame.Path(mesh, onA, onB);
                if (kept != "COMPLETE")
                {
                    return "a tile rebuilt alone lost its join to the tile beside (" + kept + ")";
                }
                Scratch.Clear();
                Scratch.Add(WalkGame.Box(Inside(left, grid, y), WalkGame.NotWalkable));
                began = Stopwatch.GetTimestamp();
                WalkGame.UpdateNow(data, flat, Scratch, leftOnly);
                ms += (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
                bool cleared = !WalkGame.Sample(mesh, a, 1f, out _);
                bool beside = WalkGame.Sample(mesh, b, 1f, out _);
                if (!beside)
                {
                    return "clearing a tile cleared the tile beside too";
                }
                if (!cleared)
                {
                    ClearMiss = "Unity's builder did not clear a tile rebuilt from nothing walkable, so ground grown at this load stays until the next load if it is given back";
                }
                return null;
            }
            finally
            {
                WalkGame.Remove(ref instance, ref data);
            }
        }

        /// <summary>
        /// Plans growing one city mesh over boxes of new ground: the tiles they cover; the old ground there sorted into
        /// islands, those a seed stands on kept (the city the new ground joins) and the rest let go; which covered tiles hold
        /// kept old ground; the rectangles of each pass, their sources and the prints before. A job with a
        /// <see cref="GroundJob.Problem"/> stands down: the old ground could not be read whole where it is to be rebuilt.
        /// Heavy: behind a pause or the loading screen.
        /// </summary>
        public static GroundJob Plan(CityMesh mesh, List<GroundBox> boxes, List<FlatRect> area, List<GroundBox> obstacles, List<GroundPoint> seeds, float height, float flatRadius)
        {
            var job = new GroundJob { Mesh = mesh, Height = height };
            job.Boxes.AddRange(boxes);
            if (obstacles != null)
            {
                job.Obstacles.AddRange(obstacles);
            }
            if (seeds != null)
            {
                job.Seeds.AddRange(seeds);
            }
            TileGrid grid = mesh.Grid;
            var under = new List<long>(8);
            var covered = new HashSet<long>();
            for (int b = 0; b < boxes.Count; b++)
            {
                GroundRules.TilesUnder(boxes[b], grid, 0f, under);
                for (int i = 0; i < under.Count; i++)
                {
                    if (covered.Add(under[i]))
                    {
                        job.Covered.Add(under[i]);
                    }
                }
            }
            for (int a = 0; area != null && a < area.Count; a++)
            {
                // The plan's whole area is rebuilt, so no patch of old walking mesh baked there is left between its boxes.
                FlatRect r = area[a];
                for (int tx = grid.TileX(r.MinX); tx <= grid.TileX(r.MaxX); tx++)
                {
                    for (int tz = grid.TileZ(r.MinZ); tz <= grid.TileZ(r.MaxZ); tz++)
                    {
                        long tile = TileGrid.Key(tx, tz);
                        if (covered.Add(tile))
                        {
                            job.Covered.Add(tile);
                        }
                    }
                }
            }
            GroundRules.Round(job.Covered, job.Round);
            for (int i = 0; i < job.Round.Count; i++)
            {
                GroundRules.Spread(job.Round[i], grid, GroundRules.PrintSamples, job.RoundX, job.RoundZ);
            }
            job.RoundBefore = WalkGame.Print(mesh, job.RoundX, job.RoundZ, height, GroundRules.SpreadReach(grid, GroundRules.PrintSamples));
            GroundRules.Extent(job.Covered, grid, out float minX, out float minZ, out float maxX, out float maxZ);
            var joining = new HashSet<long>();
            WalkGame.OldGround(mesh, minX - GroundRules.OldMargin, minZ - GroundRules.OldMargin, maxX + GroundRules.OldMargin, maxZ + GroundRules.OldMargin, job, covered, joining);
            for (int i = 0; i < job.Covered.Count; i++)
            {
                long tile = job.Covered[i];
                if (joining.Contains(tile))
                {
                    job.Joining.Add(tile);
                    GroundRules.Spread(tile, grid, GroundRules.TileSamples, job.CoveredX, job.CoveredZ);
                }
                else
                {
                    job.Fresh.Add(tile);
                }
            }
            job.CoveredBefore = WalkGame.Print(mesh, job.CoveredX, job.CoveredZ, height, GroundRules.SpreadReach(grid, GroundRules.TileSamples));
            job.Problem = WalkGame.Whole(job);
            GroundShapes.Rects(job.Fresh, job.FreshRects);
            GroundShapes.Rects(job.Joining, job.JoinRects);
            GroundShapes.Rects(job.Covered, job.BackRects);
            job.Own = mesh.Settings;
            job.Flat = mesh.Settings;
            job.Flat.agentRadius = flatRadius;
            float margin = mesh.Settings.agentRadius - flatRadius;
            var shrunk = new List<GroundBox>(boxes.Count);
            GroundShapes.ShrinkOutward(boxes, margin, shrunk);
            for (int b = 0; b < boxes.Count; b++)
            {
                job.FreshSources.Add(WalkGame.Box(boxes[b], mesh.Area));
            }
            for (int b = 0; b < shrunk.Count; b++)
            {
                job.JoinSources.Add(WalkGame.Box(shrunk[b], mesh.Area));
            }
            for (int o = 0; o < job.Obstacles.Count; o++)
            {
                // In a tile built without the agent's margin the obstacle keeps it itself.
                GroundBox standing = job.Obstacles[o];
                job.FreshSources.Add(WalkGame.Box(standing, WalkGame.NotWalkable));
                job.JoinSources.Add(WalkGame.Box(new GroundBox(standing.X, standing.Z, standing.Top, standing.HalfX + margin, standing.HalfZ + margin, standing.Yaw, standing.Depth),
                    WalkGame.NotWalkable));
            }
            for (int m = 0; m < job.Old.Count; m++)
            {
                NavMeshBuildSource source = WalkGame.MeshSource(job.Old[m], job.OldAreas[m]);
                job.FreshSources.Add(source);
                job.JoinSources.Add(source);
            }
            return job;
        }

        /// <summary>Queues a pass: each of its rectangles of tiles is one update, started by <see cref="Step"/>.</summary>
        public static void Begin(GroundJob job, GroundPass pass)
        {
            job.Pending.Clear();
            job.Pass = pass;
            if (pass == GroundPass.Back)
            {
                WalkGame.PrepareBack(job, Inside);   // given back, a tile is rebuilt from all its old ground, an empty one emptied again
            }
            List<TileRect> rects = Rects(job, pass);
            for (int i = 0; i < rects.Count; i++)
            {
                job.Pending.Enqueue(rects[i]);
            }
        }

        private static List<TileRect> Rects(GroundJob job, GroundPass pass)
        {
            return pass == GroundPass.Fresh ? job.FreshRects : pass == GroundPass.Join ? job.JoinRects : job.BackRects;
        }

        /// <summary>
        /// One look at a job: the running update finished, the next rectangle's begun. True once the pass is done (or had
        /// nothing), with any fault in <see cref="GroundJob.Problem"/>.
        /// </summary>
        public static bool Step(GroundJob job)
        {
            if (job.Running != null)
            {
                if (!job.Running.isDone)
                {
                    return false;
                }
                job.Ms += (Stopwatch.GetTimestamp() - job.Started) * 1000.0 / Stopwatch.Frequency;
                job.Running = null;
            }
            if (job.Pending.Count == 0)
            {
                return true;
            }
            TileRect rect = job.Pending.Dequeue();
            job.Started = Stopwatch.GetTimestamp();
            job.Updates++;
            job.Running = WalkGame.Update(job.Mesh.Data, Settings(job), Sources(job), Bounds(job, rect));
            if (job.Running == null)
            {
                job.Problem = Refused(job, rect);
                job.Pending.Clear();
                return true;
            }
            return false;
        }

        /// <summary>A whole pass done now, rectangle after rectangle: for new ground laid behind the loading screen.</summary>
        public static void Now(GroundJob job, GroundPass pass)
        {
            Begin(job, pass);
            while (job.Pending.Count > 0)
            {
                TileRect rect = job.Pending.Dequeue();
                long began = Stopwatch.GetTimestamp();
                job.Updates++;
                if (!WalkGame.UpdateNow(job.Mesh.Data, Settings(job), Sources(job), Bounds(job, rect)))
                {
                    job.Problem = Refused(job, rect);
                    if (pass != GroundPass.Back)
                    {
                        job.Pending.Clear();   // nothing more is built on a builder that refused; giving back carries on, to put back all it can
                    }
                }
                job.Ms += (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            }
        }

        private static string Refused(GroundJob job, TileRect rect)
        {
            return "Unity's builder would not update the " + job.Mesh.Label + " mesh's tiles (" + rect.X0 + ", " + rect.Z0 + ") to (" + rect.X1 + ", " + rect.Z1 + ")";
        }

        // The tiles with no old ground keep the agent's own margin; the old tiles, rebuilt from triangles that keep it already, none.
        private static NavMeshBuildSettings Settings(GroundJob job)
        {
            return job.Pass == GroundPass.Fresh ? job.Own : job.Flat;
        }

        private static List<NavMeshBuildSource> Sources(GroundJob job)
        {
            return job.Pass == GroundPass.Fresh ? job.FreshSources : job.Pass == GroundPass.Join ? job.JoinSources : job.BackSources;
        }

        private static Bounds Bounds(GroundJob job, TileRect rect)
        {
            GroundShapes.RectBounds(rect, job.Mesh.Grid, out float minX, out float minZ, out float maxX, out float maxZ);
            return WalkGame.Local(job.Mesh, minX, minZ, maxX, maxZ, job.Height - HeightBelow, job.Height + HeightAbove);
        }

        /// <summary>How many of the samples round the covered tiles are as they were before, on their own tiles (GroundRules.RoundAlike).</summary>
        public static int RoundAlike(GroundJob job)
        {
            float[] after = WalkGame.Print(job.Mesh, job.RoundX, job.RoundZ, job.Height, GroundRules.SpreadReach(job.Mesh.Grid, GroundRules.PrintSamples));
            return GroundRules.RoundAlike(job.RoundBefore, after, job.Round, GroundRules.PrintSamples, job.Mesh.Grid);
        }

        /// <summary>How many of the samples on the joining tiles are as they were before (after giving back).</summary>
        public static int CoveredAlike(GroundJob job)
        {
            return GroundRules.Alike(job.CoveredBefore, WalkGame.Print(job.Mesh, job.CoveredX, job.CoveredZ, job.Height, GroundRules.SpreadReach(job.Mesh.Grid, GroundRules.TileSamples)));
        }

        /// <summary>
        /// One city mesh grown over a plan's boxes now, whatever stands on them built as no walking surface, the old ground
        /// kept where a seed stands on it: the empty tiles, then the old tiles joined, the tiles round about compared with
        /// how they were, the new ground checked walkable on its boxes. Null when grown; else why not, the tiles given back.
        /// <paramref name="walkable"/>: the boxes whose middle can be walked on.
        /// </summary>
        public static string Grow(CityMesh mesh, List<GroundBox> boxes, List<FlatRect> area, List<GroundBox> obstacles, List<GroundPoint> seeds, float flat, out int walkable,
            out GroundJob done)
        {
            walkable = 0;
            done = null;
            GroundJob job = Plan(mesh, boxes, area, obstacles, seeds, boxes[0].Top, flat);
            try
            {
                if (job.Problem != null)
                {
                    return job.Problem;
                }
                Now(job, GroundPass.Fresh);
                if (job.Problem == null)
                {
                    Now(job, GroundPass.Join);
                }
                int samples = job.RoundBefore.Length / 3;
                int alike = job.Problem == null ? RoundAlike(job) : samples;
                if (alike != samples)
                {
                    job.Problem = "the " + mesh.Label + " mesh changed beyond the new ground's tiles (" + (samples - alike) + " of " + samples + " samples moved)";
                }
                // Unity's builder says an update was done whether or not it rebuilt anything (the owner's game of 23 Oct), so
                // the ground is said grown only when it can be walked on.
                walkable = job.Problem == null ? WalkGame.Walkable(mesh, boxes) : 0;
                if (job.Problem == null && walkable == 0)
                {
                    job.Problem = "the ground grown into the " + mesh.Label + " mesh cannot be walked on";
                }
                done = job;
                if (job.Problem == null)
                {
                    return null;
                }
                // Standing down leaves no island of new ground unjoined, nor an old tile half rebuilt.
                string why = job.Problem;
                job.Problem = null;
                Now(job, GroundPass.Back);
                return why + (job.Problem == null ? "; its tiles given back" : "; giving its tiles back failed too (" + job.Problem + ")");
            }
            finally
            {
                Free(job);
            }
        }

        /// <summary>A job is over: nothing more is asked of the builder, and the meshes made for it go.</summary>
        public static void Free(GroundJob job)
        {
            if (job == null)
            {
                return;
            }
            job.Pending.Clear();
            job.Running = null;
            WalkGame.Drop(job.Old);
            WalkGame.Drop(job.Others);
            job.FreshSources.Clear();
            job.JoinSources.Clear();
            job.BackSources.Clear();
        }
    }
}
