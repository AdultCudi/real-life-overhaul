using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.NewGround
{
    /// <summary>One of the city's two walking meshes as the game has it at this load: its surface, data, agent type and build settings.</summary>
    public sealed class CityMesh
    {
        public string Label;                   // "Player" or "NPC", for the log
        public string Name;                    // the surface's object in the game's scene
        public Component Surface;              // the game's NavMeshSurface, read through the contract
        public NavMeshData Data;
        public int Agent;
        public NavMeshBuildSettings Settings;  // the surface's own, as it was baked
        public int Layers;                     // the layers it is baked from
        public int Area;                       // the area new ground is given, the surface's own default
        public TileGrid Grid;
        public NavMeshQueryFilter Filter;
    }

    /// <summary>
    /// The part of WalkGame that is Unity's navigation alone, no type of the game's: samples, paths, builds and updates of
    /// a walking mesh's data, links, the old mesh's own triangles (NavMesh.CalculateTriangulation, read once a load for
    /// both meshes) sorted into islands so a rebuilt tile keeps the old ground its seeds stand on, and the prints that say
    /// what moved. So the lab project (not-in-repo/navlab) builds this very file into a player of the game's Unity and
    /// proves it there. Called on a load, a test or a build's end, never per frame.
    /// </summary>
    public static partial class WalkGame
    {
        public const float OnMesh = 0.05f;       // m: a triangle's corner sampled on a mesh this near is that mesh's
        public const float SourcesReach = 30f;   // m round the player the game's meshes are counted as build sources

        private static NavMeshPath _path;
        private static readonly List<NavMeshBuildSource> Collected = new List<NavMeshBuildSource>(256);
        private static readonly List<NavMeshBuildMarkup> NoMarkups = new List<NavMeshBuildMarkup>(0);
        private static Vector3[] _allVertices;   // the world's walking meshes as triangles, read once a load (both meshes' old ground)
        private static int[] _allIndices;
        private static int[] _allAreas;

        /// <summary>The old ground is read again at the next grow: the city's walking meshes have changed or gone.</summary>
        public static void ForgetTriangles()
        {
            _allVertices = null;
            _allIndices = null;
            _allAreas = null;
        }

        /// <summary>A walking mesh of this agent type within <paramref name="reach"/> of a point: where, or false.</summary>
        public static bool Sample(CityMesh mesh, Vector3 at, float reach, out Vector3 hit)
        {
            if (mesh != null && NavMesh.SamplePosition(at, out NavMeshHit found, reach, mesh.Filter))
            {
                hit = found.position;
                return true;
            }
            hit = at;
            return false;
        }

        /// <summary>The way along the walking mesh from one point to another: COMPLETE, PARTIAL or INVALID, as Unity says.</summary>
        public static string Path(CityMesh mesh, Vector3 from, Vector3 to)
        {
            if (mesh == null)
            {
                return "INVALID";
            }
            if (_path == null)
            {
                _path = new NavMeshPath();
            }
            if (!NavMesh.CalculatePath(from, to, mesh.Filter, _path))
            {
                return "INVALID";
            }
            return _path.status == NavMeshPathStatus.PathComplete ? "COMPLETE" : _path.status == NavMeshPathStatus.PathPartial ? "PARTIAL" : "INVALID";
        }

        /// <summary>Unity's own area that nothing walks on: a source of it is built as no walking surface at all.</summary>
        public static int NotWalkable
        {
            get
            {
                if (_notWalkable < 0)
                {
                    int area = NavMesh.GetAreaFromName("Not Walkable");
                    _notWalkable = area >= 0 ? area : 1;   // the built-in index, should the name ever be another
                }
                return _notWalkable;
            }
        }

        private static int _notWalkable = -1;

        /// <summary>The box as a build source: a solid of the builder's own, so no mesh of the game's need be readable.</summary>
        public static NavMeshBuildSource Box(GroundBox box, int area)
        {
            return new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                size = new Vector3(box.HalfX * 2f, box.Depth, box.HalfZ * 2f),
                transform = Matrix4x4.TRS(new Vector3(box.X, box.Top - box.Depth * 0.5f, box.Z), Quaternion.Euler(0f, box.Yaw, 0f), Vector3.one),
                area = area
            };
        }

        /// <summary>A square of ground in a mesh's own frame, full height: the bounds a build or an update is limited to.</summary>
        public static Bounds Local(CityMesh mesh, float minX, float minZ, float maxX, float maxZ, float minY, float maxY)
        {
            Quaternion back = Quaternion.Inverse(mesh.Data.rotation);
            Vector3 origin = mesh.Data.position;
            var bounds = new Bounds(back * (new Vector3(minX, minY, minZ) - origin), Vector3.zero);
            for (int c = 1; c < 8; c++)
            {
                var corner = new Vector3((c & 1) == 0 ? minX : maxX, (c & 2) == 0 ? minY : maxY, (c & 4) == 0 ? minZ : maxZ);
                bounds.Encapsulate(back * (corner - origin));
            }
            return bounds;
        }

        /// <summary>
        /// A walking mesh of its own over the given sources, on the city mesh's own tile grid and settings (with the
        /// changes asked), timed. Not added to the world: <see cref="Add"/> does that.
        /// </summary>
        public static NavMeshData Build(CityMesh mesh, NavMeshBuildSettings settings, List<NavMeshBuildSource> sources, Bounds local, out double ms)
        {
            long began = Stopwatch.GetTimestamp();
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, local, mesh.Data.position, mesh.Data.rotation);
            ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            return data;
        }

        public static NavMeshDataInstance Add(NavMeshData data)
        {
            return NavMesh.AddNavMeshData(data);
        }

        /// <summary>How many of the boxes have this mesh's walking surface at their middle now.</summary>
        public static int Walkable(CityMesh mesh, List<GroundBox> boxes)
        {
            int found = 0;
            for (int b = 0; b < boxes.Count; b++)
            {
                GroundBox box = boxes[b];
                found += Sample(mesh, new Vector3(box.X, box.Top, box.Z), 1f, out _) ? 1 : 0;
            }
            return found;
        }

        /// <summary>Takes a walking mesh of the mod's own out of the world and lets its data go. Safe on one already gone.</summary>
        public static void Remove(ref NavMeshDataInstance instance, ref NavMeshData data)
        {
            if (instance.valid)
            {
                NavMesh.RemoveNavMeshData(instance);
            }
            instance = default;
            if (data != null)
            {
                UnityEngine.Object.Destroy(data);
                data = null;
            }
        }

        /// <summary>Rebuilds the tiles of a mesh's data that the bounds hold whole, from the sources, in the background; the rest stays as it is.</summary>
        public static AsyncOperation Update(NavMeshData data, NavMeshBuildSettings settings, List<NavMeshBuildSource> sources, Bounds local)
        {
            settings.preserveTilesOutsideBounds = true;   // the tiles beyond the bounds are kept as they are, never rebuilt from nothing
            return NavMeshBuilder.UpdateNavMeshDataAsync(data, settings, sources, local);
        }

        /// <summary>The same, done now: a district's ground behind the loading screen, and the small checks made before the city's own data is touched.</summary>
        public static bool UpdateNow(NavMeshData data, NavMeshBuildSettings settings, List<NavMeshBuildSource> sources, Bounds local)
        {
            settings.preserveTilesOutsideBounds = true;
            return NavMeshBuilder.UpdateNavMeshData(data, settings, sources, local);
        }

        /// <summary>The fallback join: a two-way link of this agent type between two points, or an invalid one.</summary>
        public static NavMeshLinkInstance Link(CityMesh mesh, Vector3 from, Vector3 to, float width)
        {
            var link = new NavMeshLinkData
            {
                startPosition = from,
                endPosition = to,
                width = width,
                costModifier = -1f,
                bidirectional = true,
                area = mesh.Area,
                agentTypeID = mesh.Agent
            };
            return NavMesh.AddLink(link);
        }

        public static void Unlink(ref NavMeshLinkInstance link)
        {
            if (link.valid)
            {
                NavMesh.RemoveLink(link);
            }
            link = default;
        }

        /// <summary>
        /// The game's own meshes within <see cref="SourcesReach"/> on a walking mesh's layers, and how many could be read
        /// by a build in this player: the measure of whether the game's geometry can rebuild a tile.
        /// </summary>
        public static void CountSources(CityMesh mesh, Vector3 at, out int meshes, out int readable, out int batched, out int other)
        {
            meshes = 0;
            readable = 0;
            batched = 0;
            other = 0;
            var bounds = new Bounds(at, new Vector3(SourcesReach * 2f, 80f, SourcesReach * 2f));
            NavMeshBuilder.CollectSources(bounds, mesh.Layers, NavMeshCollectGeometry.RenderMeshes, mesh.Area, NoMarkups, Collected);
            for (int i = 0; i < Collected.Count; i++)
            {
                NavMeshBuildSource source = Collected[i];
                if (source.shape != NavMeshBuildSourceShape.Mesh)
                {
                    other++;
                    continue;
                }
                meshes++;
                var shape = source.sourceObject as Mesh;
                if (shape == null)
                {
                    continue;
                }
                readable += shape.isReadable ? 1 : 0;
                batched += shape.name.StartsWith("Combined Mesh", StringComparison.Ordinal) ? 1 : 0;
            }
            Collected.Clear();
        }

        // ---- the old ground

        // The world's walking meshes as triangles, read once a load: a heavy call (every walking mesh at once), behind a
        // pause or the loading screen. Growing one mesh leaves the other's triangles as they were, so both read the same.
        private static void Triangles()
        {
            if (_allVertices != null)
            {
                return;
            }
            NavMeshTriangulation all = NavMesh.CalculateTriangulation();
            _allVertices = all.vertices ?? new Vector3[0];
            _allIndices = all.indices ?? new int[0];
            _allAreas = all.areas ?? new int[0];
        }

        /// <summary>
        /// The old ground under a job's covered tiles: every triangle of the world's walking meshes whose middle lies in the
        /// square, sorted into islands (GroundShapes.Islands); those of the islands a seed stands on, that this agent type's
        /// mesh holds at that very place, are kept (the city's ground the new joins) and made into one readable mesh per
        /// area, ready to be a build source. The covered tiles any kept triangle lies over are put in
        /// <paramref name="joining"/>. The rest is kept in the job for its check and for giving back.
        /// </summary>
        public static void OldGround(CityMesh mesh, float minX, float minZ, float maxX, float maxZ, GroundJob job, HashSet<long> covered, HashSet<long> joining)
        {
            Triangles();
            Vector3[] v = _allVertices;
            int[] index = _allIndices;
            var picked = new List<int>(1024);
            for (int t = 0; t + 2 < index.Length; t += 3)
            {
                Vector3 a = v[index[t]], b = v[index[t + 1]], c = v[index[t + 2]];
                float mx = (a.x + b.x + c.x) / 3f, mz = (a.z + b.z + c.z) / 3f;
                if (mx >= minX && mx <= maxX && mz >= minZ && mz <= maxZ)
                {
                    picked.Add(t);
                }
            }
            int count = picked.Count;
            var x = new float[count * 3];
            var y = new float[count * 3];
            var z = new float[count * 3];
            var areas = new int[count];
            for (int i = 0; i < count; i++)
            {
                int t = picked[i];
                for (int k = 0; k < 3; k++)
                {
                    Vector3 p = v[index[t + k]];
                    x[i * 3 + k] = p.x;
                    y[i * 3 + k] = p.y;
                    z[i * 3 + k] = p.z;
                }
                areas[i] = _allAreas != null && t / 3 < _allAreas.Length ? _allAreas[t / 3] : mesh.Area;
            }
            var island = new int[count];
            int islands = GroundShapes.Islands(x, y, z, count, mesh.Grid, island);
            var seeded = new bool[islands];
            int seededCount = GroundShapes.Seeded(x, y, z, count, island, job.Seeds, seeded);
            var kept = new bool[count];
            var points = new List<List<Vector3>>(2);
            for (int i = 0; i < count; i++)
            {
                if (!seeded[island[i]] || !Holds(mesh, x, y, z, i))
                {
                    continue;
                }
                kept[i] = true;
                Add(points, job.OldAreas, areas[i], x, y, z, i);
                job.OldTriangles++;
                // Every covered tile this triangle lies over holds the city's own ground: it is rebuilt with it.
                float tx0 = Math.Min(x[i * 3], Math.Min(x[i * 3 + 1], x[i * 3 + 2])), tx1 = Math.Max(x[i * 3], Math.Max(x[i * 3 + 1], x[i * 3 + 2]));
                float tz0 = Math.Min(z[i * 3], Math.Min(z[i * 3 + 1], z[i * 3 + 2])), tz1 = Math.Max(z[i * 3], Math.Max(z[i * 3 + 1], z[i * 3 + 2]));
                for (int gx = mesh.Grid.TileX(tx0); gx <= mesh.Grid.TileX(tx1); gx++)
                {
                    for (int gz = mesh.Grid.TileZ(tz0); gz <= mesh.Grid.TileZ(tz1); gz++)
                    {
                        long tile = TileGrid.Key(gx, gz);
                        if (covered.Contains(tile))
                        {
                            joining.Add(tile);
                        }
                    }
                }
            }
            Meshes(points, job.Old);
            var byTile = new Dictionary<long, List<int>>(256);
            for (int i = 0; i < count; i++)
            {
                float tx0 = Math.Min(x[i * 3], Math.Min(x[i * 3 + 1], x[i * 3 + 2])), tx1 = Math.Max(x[i * 3], Math.Max(x[i * 3 + 1], x[i * 3 + 2]));
                float tz0 = Math.Min(z[i * 3], Math.Min(z[i * 3 + 1], z[i * 3 + 2])), tz1 = Math.Max(z[i * 3], Math.Max(z[i * 3 + 1], z[i * 3 + 2]));
                for (int gx = mesh.Grid.TileX(tx0); gx <= mesh.Grid.TileX(tx1); gx++)
                {
                    for (int gz = mesh.Grid.TileZ(tz0); gz <= mesh.Grid.TileZ(tz1); gz++)
                    {
                        long tile = TileGrid.Key(gx, gz);
                        if (!byTile.TryGetValue(tile, out List<int> list))
                        {
                            list = new List<int>(16);
                            byTile[tile] = list;
                        }
                        list.Add(i);
                    }
                }
            }
            job.CandByTile = byTile;
            job.Islands = islands;
            job.SeededIslands = seededCount;
            job.CandX = x;
            job.CandY = y;
            job.CandZ = z;
            job.CandAreas = areas;
            job.CandKept = kept;
            job.CandCount = count;
        }

        /// <summary>
        /// Null when every sample of the old mesh on a tile to be rebuilt lies on a triangle read from the world's walking
        /// meshes; else where one does not (a mesh the triangulation did not give whole would lose ground in the rebuild).
        /// A note for the log, never a stand-down: what it measures is a few samples' worth of ground at most, and a false
        /// miss on the mesh's edge cost the owner the whole district (24 Oct).
        /// </summary>
        public static string Whole(GroundJob job)
        {
            if (job.Joining.Count > 0 && job.OldTriangles == 0)
            {
                return "the old " + job.Mesh.Label + " mesh's triangles could not be read where it is to be rebuilt";
            }
            float[] print = job.CoveredBefore;
            for (int i = 0; print != null && i + 2 < print.Length; i += 3)
            {
                if (float.IsNaN(print[i]))
                {
                    continue;
                }
                bool on = false;
                long tile = TileGrid.Key(job.Mesh.Grid.TileX(print[i]), job.Mesh.Grid.TileZ(print[i + 2]));
                List<int> near = job.CandByTile != null && job.CandByTile.TryGetValue(tile, out List<int> found) ? found : null;
                for (int n = 0; near != null && n < near.Count && !on; n++)
                {
                    int a = near[n] * 3;
                    on = GroundRules.InTriangle(print[i], print[i + 2], job.CandX[a], job.CandZ[a], job.CandX[a + 1], job.CandZ[a + 1], job.CandX[a + 2], job.CandZ[a + 2])
                         && Math.Abs(GroundRules.HeightIn(print[i], print[i + 2], job.CandX[a], job.CandY[a], job.CandZ[a], job.CandX[a + 1], job.CandY[a + 1], job.CandZ[a + 1],
                                                          job.CandX[a + 2], job.CandY[a + 2], job.CandZ[a + 2]) - print[i + 1]) <= 0.6f;
                }
                if (!on)
                {
                    return "the old " + job.Mesh.Label + " mesh at (" + print[i].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ", "
                           + print[i + 2].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + ") is not among the triangles read from it";
                }
            }
            return null;
        }

        /// <summary>
        /// Readies a job for giving back: the old ground it let go that this agent type's mesh holds, made into meshes, so the
        /// covered tiles are rebuilt from all of their old triangles; a covered tile with none is emptied from a box of
        /// nothing walkable well inside it. Once a job.
        /// </summary>
        public static void PrepareBack(GroundJob job, Func<long, TileGrid, float, GroundBox> inside)
        {
            if (job.BackReady)
            {
                return;
            }
            job.BackReady = true;
            var points = new List<List<Vector3>>(2);
            var had = new HashSet<long>();
            for (int i = 0; i < job.CandCount; i++)
            {
                bool mine = job.CandKept[i] || Holds(job.Mesh, job.CandX, job.CandY, job.CandZ, i);
                if (!mine)
                {
                    continue;
                }
                float mx = (job.CandX[i * 3] + job.CandX[i * 3 + 1] + job.CandX[i * 3 + 2]) / 3f, mz = (job.CandZ[i * 3] + job.CandZ[i * 3 + 1] + job.CandZ[i * 3 + 2]) / 3f;
                had.Add(TileGrid.Key(job.Mesh.Grid.TileX(mx), job.Mesh.Grid.TileZ(mz)));
                if (!job.CandKept[i])
                {
                    Add(points, job.OtherAreas, job.CandAreas[i], job.CandX, job.CandY, job.CandZ, i);
                    job.OtherTriangles++;
                }
            }
            Meshes(points, job.Others);
            job.BackSources.Clear();
            for (int m = 0; m < job.Old.Count; m++)
            {
                job.BackSources.Add(MeshSource(job.Old[m], job.OldAreas[m]));
            }
            for (int m = 0; m < job.Others.Count; m++)
            {
                job.BackSources.Add(MeshSource(job.Others[m], job.OtherAreas[m]));
            }
            for (int i = 0; i < job.Covered.Count; i++)
            {
                if (!had.Contains(job.Covered[i]))
                {
                    job.BackSources.Add(Box(inside(job.Covered[i], job.Mesh.Grid, job.Height), NotWalkable));
                }
            }
        }

        private static void Add(List<List<Vector3>> points, List<int> areas, int area, float[] x, float[] y, float[] z, int i)
        {
            int slot = areas.IndexOf(area);
            if (slot < 0)
            {
                areas.Add(area);
                points.Add(new List<Vector3>(512));
                slot = areas.Count - 1;
            }
            for (int k = 0; k < 3; k++)
            {
                points[slot].Add(new Vector3(x[i * 3 + k], y[i * 3 + k], z[i * 3 + k]));
            }
        }

        private static void Meshes(List<List<Vector3>> points, List<Mesh> into)
        {
            for (int s = 0; s < points.Count; s++)
            {
                List<Vector3> corners = points[s];
                var made = new Mesh { name = "RealLifeOverhaul old walking mesh" };
                if (corners.Count > 65000)
                {
                    made.indexFormat = IndexFormat.UInt32;
                }
                var order = new int[corners.Count];
                for (int i = 0; i < order.Length; i++)
                {
                    order[i] = i;
                }
                made.SetVertices(corners);
                made.SetTriangles(order, 0);
                made.RecalculateBounds();
                into.Add(made);
            }
        }

        // A triangle this agent type's mesh holds: its middle and its corners (each a little toward the middle) stand on it.
        private static bool Holds(CityMesh mesh, float[] x, float[] y, float[] z, int i)
        {
            var a = new Vector3(x[i * 3], y[i * 3], z[i * 3]);
            var b = new Vector3(x[i * 3 + 1], y[i * 3 + 1], z[i * 3 + 1]);
            var c = new Vector3(x[i * 3 + 2], y[i * 3 + 2], z[i * 3 + 2]);
            Vector3 middle = (a + b + c) / 3f;
            return Holds(mesh, middle) && Holds(mesh, Vector3.MoveTowards(a, middle, OnMesh)) && Holds(mesh, Vector3.MoveTowards(b, middle, OnMesh))
                   && Holds(mesh, Vector3.MoveTowards(c, middle, OnMesh));
        }

        /// <summary>The meshes made for a job go when it is over: made by code, they are freed by code.</summary>
        public static void Drop(List<Mesh> made)
        {
            for (int i = 0; i < made.Count; i++)
            {
                if (made[i] != null)
                {
                    UnityEngine.Object.Destroy(made[i]);
                }
            }
            made.Clear();
        }

        // A point stands on this agent type's walking mesh, at that very place.
        private static bool Holds(CityMesh mesh, Vector3 at)
        {
            return NavMesh.SamplePosition(at, out NavMeshHit hit, OnMesh * 2f, mesh.Filter) && (hit.position - at).sqrMagnitude <= OnMesh * OnMesh * 4f;
        }

        /// <summary>A mesh of the mod's own as a build source of one area, standing where its corners are.</summary>
        public static NavMeshBuildSource MeshSource(Mesh shape, int area)
        {
            return new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Mesh,
                sourceObject = shape,
                transform = Matrix4x4.identity,
                area = area
            };
        }

        /// <summary>
        /// Where each point finds this agent type's walking mesh within <paramref name="reach"/>, sampled from several
        /// heights round <paramref name="y"/> (a deck above the ground counts too): x, y, z of the nearest hit, NaN where none.
        /// </summary>
        public static float[] Print(CityMesh mesh, List<float> xs, List<float> zs, float y, float reach)
        {
            var print = new float[xs.Count * 3];
            for (int i = 0; i < xs.Count; i++)
            {
                bool found = false;
                Vector3 best = Vector3.zero;
                float nearest = float.MaxValue;
                for (int level = -1; level <= 3; level++)
                {
                    var at = new Vector3(xs[i], y + level * reach, zs[i]);
                    if (Sample(mesh, at, reach, out Vector3 hit) && (hit - at).sqrMagnitude < nearest)
                    {
                        nearest = (hit - at).sqrMagnitude;
                        best = hit;
                        found = true;
                    }
                }
                print[i * 3] = found ? best.x : float.NaN;
                print[i * 3 + 1] = found ? best.y : float.NaN;
                print[i * 3 + 2] = found ? best.z : float.NaN;
            }
            return print;
        }

        /// <summary>True when this agent type's walking mesh has any polygon on a tile, from the points of a print over it.</summary>
        public static bool Has(float[] print, long tile, TileGrid grid)
        {
            for (int i = 0; i + 2 < print.Length; i += 3)
            {
                if (!float.IsNaN(print[i]) && GroundRules.OnTile(print[i], print[i + 2], tile, grid))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
