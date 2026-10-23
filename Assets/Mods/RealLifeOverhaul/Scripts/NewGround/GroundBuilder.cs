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
    /// One city walking mesh grown over new ground, tile by tile: which tiles the boxes cover, which of them already hold
    /// the old mesh (they are rebuilt from its own triangles and the new ground together, so the two join on one surface),
    /// the sources of each pass, and prints of the samples round about taken before, to say nothing else moved.
    /// </summary>
    public sealed class GroundJob
    {
        public CityMesh Mesh;
        public readonly List<GroundBox> Boxes = new List<GroundBox>(4);
        public readonly List<long> Covered = new List<long>(8);    // every tile under the boxes
        public readonly List<long> Fresh = new List<long>(8);      // ...of them, those with no walking mesh of this kind yet
        public readonly List<long> Joining = new List<long>(8);    // ...those with the old mesh, rebuilt with the new ground
        public readonly List<long> Round = new List<long>(16);     // the tiles round the covered ones, which must not change
        public readonly List<Mesh> Old = new List<Mesh>(2);        // the old mesh's own triangles, one mesh per area
        public readonly List<int> OldAreas = new List<int>(2);
        public int OldTriangles;
        public readonly List<NavMeshBuildSource> FreshSources = new List<NavMeshBuildSource>(8);
        public readonly List<NavMeshBuildSource> JoinSources = new List<NavMeshBuildSource>(8);
        public readonly List<NavMeshBuildSource> BackSources = new List<NavMeshBuildSource>(4);
        public readonly List<float> RoundX = new List<float>(144);
        public readonly List<float> RoundZ = new List<float>(144);
        public readonly List<float> CoveredX = new List<float>(64);
        public readonly List<float> CoveredZ = new List<float>(64);
        public float[] RoundBefore;
        public float[] CoveredBefore;
        public float Height;               // m: the height prints are sampled round
        public string Problem;             // why the job stood down, or null
        public GroundPass Pass;
        public double Ms;                  // the builder's time so far, for the log
        internal readonly Queue<long> Pending = new Queue<long>(8);
        internal AsyncOperation Running;
        internal long Started;
        internal NavMeshBuildSettings Own;
        internal NavMeshBuildSettings Flat;
    }

    /// <summary>
    /// The walking core of new ground. The game's walking meshes were baked in the editor and the game never builds one, so
    /// this proves Unity's run-time builder first on data of its own far below the city (<see cref="DryRun"/>: a tile
    /// rebuilt alone keeps the tile beside and its join to it, an empty rebuild clears its tile), then grows the city's
    /// own data (NavMeshBuilder.UpdateNavMeshDataAsync, the tiles beyond kept as they are): the empty tiles first from the
    /// new ground with the agent's own margin, then each old tile it touches from the old mesh's own triangles and the new
    /// ground, built without a second margin (the triangles already keep the agent's distance; the new ground's boxes are
    /// made that much smaller instead), so the old and new are one surface the keys walk across. Every tile is its own
    /// update, its bounds just inside it, so no other tile is ever rebuilt. Giving back rebuilds the covered tiles from the
    /// old triangles alone. The separate mesh joined by links (<see cref="WalkGame.Link"/>) is the fallback the tests
    /// weigh it against. Heavy steps (reading the old mesh) behind a pause or the loading screen; the updates in the
    /// background, one asked a frame at most while a job runs; nothing per frame otherwise.
    /// </summary>
    public static class GroundBuilder
    {
        public const float DryDepth = 400f;     // m below the player the dry run's data stands, where nothing walks
        public const float DryLength = 20f;     // m: its strip across the border of its two tiles
        public const float DryWidth = 4f;       // m
        public const float FallbackMargin = 0.01f;   // m: the margin asked of the builder for old tiles if it refuses none (one voxel once rounded up)

        private static readonly List<NavMeshBuildSource> Scratch = new List<NavMeshBuildSource>(4);

        /// <summary>
        /// Unity's run-time builder tried on data of the mod's own, with the city mesh's settings: two tiles built together
        /// join; one rebuilt alone without the agent's margin keeps the tile beside and the join; an empty rebuild clears
        /// its tile and keeps the one beside. Null when all hold; else which did not. Small and done now.
        /// </summary>
        public static string DryRun(CityMesh mesh, Vector3 near, out double ms, out float flatRadius)
        {
            ms = 0.0;
            flatRadius = 0f;
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
                began = Stopwatch.GetTimestamp();
                WalkGame.UpdateNow(data, flat, Scratch, leftOnly);
                ms += (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
                bool cleared = !WalkGame.Sample(mesh, a, 1f, out _);
                bool beside = WalkGame.Sample(mesh, b, 1f, out _);
                if (!cleared || !beside)
                {
                    return cleared ? "an empty rebuild of a tile cleared the tile beside too" : "an empty rebuild did not clear its tile";
                }
                return null;
            }
            finally
            {
                WalkGame.Remove(ref instance, ref data);
            }
        }

        /// <summary>
        /// Plans growing one city mesh over boxes of new ground: the tiles they cover, which hold the old mesh, its own
        /// triangles there, the sources of each pass and the prints before. A job with a <see cref="GroundJob.Problem"/>
        /// stands down: the old mesh could not be read whole where it is to be rebuilt. Heavy: behind a pause or the
        /// loading screen.
        /// </summary>
        public static GroundJob Plan(CityMesh mesh, List<GroundBox> boxes, float height, float flatRadius)
        {
            var job = new GroundJob { Mesh = mesh, Height = height };
            job.Boxes.AddRange(boxes);
            TileGrid grid = mesh.Grid;
            var under = new List<long>(8);
            for (int b = 0; b < boxes.Count; b++)
            {
                GroundRules.TilesUnder(boxes[b], grid, 0f, under);
                for (int i = 0; i < under.Count; i++)
                {
                    if (!job.Covered.Contains(under[i]))
                    {
                        job.Covered.Add(under[i]);
                    }
                }
            }
            GroundRules.Round(job.Covered, job.Round);
            for (int i = 0; i < job.Round.Count; i++)
            {
                GroundRules.Spread(job.Round[i], grid, GroundRules.PrintSamples, job.RoundX, job.RoundZ);
            }
            for (int i = 0; i < job.Covered.Count; i++)
            {
                GroundRules.Spread(job.Covered[i], grid, GroundRules.TileSamples, job.CoveredX, job.CoveredZ);
            }
            job.RoundBefore = WalkGame.Print(mesh, job.RoundX, job.RoundZ, height, GroundRules.SpreadReach(grid, GroundRules.PrintSamples));
            job.CoveredBefore = WalkGame.Print(mesh, job.CoveredX, job.CoveredZ, height, GroundRules.SpreadReach(grid, GroundRules.TileSamples));
            GroundRules.Extent(job.Covered, grid, out float minX, out float minZ, out float maxX, out float maxZ);
            List<Mesh> old = WalkGame.OldMesh(mesh, minX - GroundRules.OldMargin, minZ - GroundRules.OldMargin, maxX + GroundRules.OldMargin,
                maxZ + GroundRules.OldMargin, job.OldAreas, out job.OldTriangles);
            job.Old.AddRange(old);
            for (int i = 0; i < job.Covered.Count; i++)
            {
                long tile = job.Covered[i];
                if (WalkGame.Has(job.CoveredBefore, tile, grid))
                {
                    job.Joining.Add(tile);
                }
                else
                {
                    job.Fresh.Add(tile);
                }
            }
            job.Problem = Whole(job);
            job.Own = mesh.Settings;
            job.Flat = mesh.Settings;
            job.Flat.agentRadius = flatRadius;
            for (int b = 0; b < boxes.Count; b++)
            {
                job.FreshSources.Add(WalkGame.Box(boxes[b], mesh.Area));
                job.JoinSources.Add(WalkGame.Box(GroundRules.Shrunk(boxes[b], mesh.Settings.agentRadius - flatRadius), mesh.Area));
            }
            for (int m = 0; m < job.Old.Count; m++)
            {
                NavMeshBuildSource source = WalkGame.MeshSource(job.Old[m], job.OldAreas[m]);
                job.FreshSources.Add(source);
                job.JoinSources.Add(source);
                job.BackSources.Add(source);
            }
            return job;
        }

        // Every point of the old mesh sampled on a tile to be rebuilt must lie on one of the triangles read from it, or the
        // rebuild would lose walkable ground there (a mesh the world's triangulation did not give whole).
        private static string Whole(GroundJob job)
        {
            if (job.Joining.Count > 0 && job.OldTriangles == 0)
            {
                return "the old " + job.Mesh.Label + " mesh's triangles could not be read where it is to be rebuilt";
            }
            float[] print = job.CoveredBefore;
            var corners = new List<Vector3[]>(job.Old.Count);
            for (int m = 0; m < job.Old.Count; m++)
            {
                corners.Add(job.Old[m].vertices);   // a copy each time it is asked: asked once
            }
            for (int i = 0; i + 2 < print.Length; i += 3)
            {
                if (float.IsNaN(print[i]))
                {
                    continue;
                }
                bool onJoining = false;
                for (int t = 0; t < job.Joining.Count && !onJoining; t++)
                {
                    onJoining = GroundRules.OnTile(print[i], print[i + 2], job.Joining[t], job.Mesh.Grid);
                }
                if (onJoining && !OnOld(corners, print[i], print[i + 1], print[i + 2]))
                {
                    return "the old " + job.Mesh.Label + " mesh at (" + print[i].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ", "
                           + print[i + 2].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ") is not among the triangles read from it";
                }
            }
            return null;
        }

        private static bool OnOld(List<Vector3[]> corners, float x, float y, float z)
        {
            for (int m = 0; m < corners.Count; m++)
            {
                Vector3[] v = corners[m];
                for (int k = 0; k + 2 < v.Length; k += 3)
                {
                    if (Math.Abs((v[k].y + v[k + 1].y + v[k + 2].y) / 3f - y) <= 0.6f
                        && GroundRules.InTriangle(x, z, v[k].x, v[k].z, v[k + 1].x, v[k + 1].z, v[k + 2].x, v[k + 2].z))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Queues a pass: each of its tiles is its own update, started by <see cref="Step"/>.</summary>
        public static void Begin(GroundJob job, GroundPass pass)
        {
            job.Pending.Clear();
            job.Pass = pass;
            List<long> tiles = pass == GroundPass.Fresh ? job.Fresh : pass == GroundPass.Join ? job.Joining : job.Covered;
            for (int i = 0; i < tiles.Count; i++)
            {
                job.Pending.Enqueue(tiles[i]);
            }
        }

        /// <summary>
        /// One look at a job: the running update finished, the next tile's begun. True once the pass is done (or had
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
            long tile = job.Pending.Dequeue();
            job.Started = Stopwatch.GetTimestamp();
            job.Running = WalkGame.Update(job.Mesh.Data, Settings(job), Sources(job), Bounds(job, tile));
            if (job.Running == null)
            {
                job.Problem = "Unity's builder would not update the " + job.Mesh.Label + " mesh's tile (" + TileGrid.KeyX(tile) + ", " + TileGrid.KeyZ(tile) + ")";
                job.Pending.Clear();
                return true;
            }
            return false;
        }

        /// <summary>A whole pass done now, tile after tile: for new ground laid behind the loading screen.</summary>
        public static void Now(GroundJob job, GroundPass pass)
        {
            Begin(job, pass);
            while (job.Pending.Count > 0)
            {
                long tile = job.Pending.Dequeue();
                long began = Stopwatch.GetTimestamp();
                if (!WalkGame.UpdateNow(job.Mesh.Data, Settings(job), Sources(job), Bounds(job, tile)))
                {
                    job.Problem = "Unity's builder would not update the " + job.Mesh.Label + " mesh's tile (" + TileGrid.KeyX(tile) + ", " + TileGrid.KeyZ(tile) + ")";
                }
                job.Ms += (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            }
        }

        // The empty tiles keep the agent's own margin; the old tiles, rebuilt from triangles that keep it already, none.
        private static NavMeshBuildSettings Settings(GroundJob job)
        {
            return job.Pass == GroundPass.Fresh ? job.Own : job.Flat;
        }

        private static List<NavMeshBuildSource> Sources(GroundJob job)
        {
            return job.Pass == GroundPass.Fresh ? job.FreshSources : job.Pass == GroundPass.Join ? job.JoinSources : job.BackSources;
        }

        private static Bounds Bounds(GroundJob job, long tile)
        {
            GroundRules.TileBounds(tile, job.Mesh.Grid, out float minX, out float minZ, out float maxX, out float maxZ);
            return WalkGame.Local(job.Mesh, minX, minZ, maxX, maxZ, job.Height - 200f, job.Height + 300f);
        }

        /// <summary>How many of the samples round the covered tiles are as they were before.</summary>
        public static int RoundAlike(GroundJob job)
        {
            return GroundRules.Alike(job.RoundBefore, WalkGame.Print(job.Mesh, job.RoundX, job.RoundZ, job.Height, GroundRules.SpreadReach(job.Mesh.Grid, GroundRules.PrintSamples)));
        }

        /// <summary>How many of the samples on the covered tiles are as they were before (after giving back).</summary>
        public static int CoveredAlike(GroundJob job)
        {
            return GroundRules.Alike(job.CoveredBefore, WalkGame.Print(job.Mesh, job.CoveredX, job.CoveredZ, job.Height, GroundRules.SpreadReach(job.Mesh.Grid, GroundRules.TileSamples)));
        }

        /// <summary>A job is over: nothing more is asked of the builder, and the old mesh's triangles, made for it, go.</summary>
        public static void Free(GroundJob job)
        {
            if (job == null)
            {
                return;
            }
            job.Pending.Clear();
            job.Running = null;
            WalkGame.Drop(job.Old);
            job.FreshSources.Clear();
            job.JoinSources.Clear();
            job.BackSources.Clear();
        }
    }
}
