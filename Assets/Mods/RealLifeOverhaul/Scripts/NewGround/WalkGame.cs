using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Extensions;
using Helpers;
using Streets.Pedestrians;
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
    /// Where new ground touches the game's walking: the only file that calls Unity's navigation API or reads the game's
    /// walking meshes. The two meshes the city walks on (the game's NavMeshSurfaces NavMeshPlayer and NavMeshNPC, both
    /// baked in the editor; the game never builds any at run time) are found at every load from the surfaces the game has
    /// switched on, never assumed: the pedestrians' agent type is the game's own filter's (NavMeshHelper.NpcNavMeshFilter),
    /// the player's is the other large one, and each mesh's tile grid and settings are its own. New ground is built from
    /// boxes of the mod's own and, where it meets the old mesh, from the old mesh's own triangles
    /// (NavMesh.CalculateTriangulation), since the game's meshes need not be readable in a player's build. Called on a
    /// load, a test or a build's end, never per frame.
    /// </summary>
    public static class WalkGame
    {
        public const float GroundAbove = 3f;     // m above a height a ray for the ground starts
        public const float GroundBelow = 12f;    // m below it the ground is looked for
        public const float OnMesh = 0.05f;       // m: a triangle's corner sampled on a mesh this near is that mesh's
        public const float SourcesReach = 30f;   // m round the player the game's meshes are counted as build sources

        private static CityMesh _player;
        private static CityMesh _npc;
        private static string _missing;
        private static NavMeshPath _path;
        private static readonly Collider[] Hits = new Collider[8];
        private static readonly List<NavMeshBuildSource> Collected = new List<NavMeshBuildSource>(256);
        private static readonly List<NavMeshBuildMarkup> NoMarkups = new List<NavMeshBuildMarkup>(0);
        private static readonly List<Pedestrian> Walkers = new List<Pedestrian>(32);
        private static int _groundMask;
        private static int _blockMask;

        /// <summary>The player's walking mesh at this load, or null (see <see cref="Missing"/>).</summary>
        public static CityMesh Player => Found() ? _player : null;

        /// <summary>The pedestrians' walking mesh at this load, or null.</summary>
        public static CityMesh Npc => Found() ? _npc : null;

        /// <summary>Why the city's walking meshes were not found, or null.</summary>
        public static string Missing => Found() ? null : _missing;

        /// <summary>The city is going: its walking meshes are let go, and found again in the next.</summary>
        public static void Forget()
        {
            _player = null;
            _npc = null;
            _missing = null;
            _groundMask = 0;
            _blockMask = 0;
        }

        // Once a city: the game's switched-on surfaces, the largest of each agent type.
        private static bool Found()
        {
            if (_player != null && _npc != null && _player.Data != null && _npc.Data != null)
            {
                return true;
            }
            _player = null;
            _npc = null;
            if (GameContract.NewGroundWalk.Off)
            {
                _missing = "this game lacks what its walking meshes are read by (" + GameContract.NewGroundWalk.Id + ")";
                return false;
            }
            try
            {
                IList surfaces = GameContract.SurfacesActive.Property.GetValue(null) as IList;
                if (surfaces == null || surfaces.Count == 0)
                {
                    _missing = "the game has no walking mesh switched on";
                    return false;
                }
                int npcAgent = NavMeshHelper.NpcNavMeshFilter.agentTypeID;
                CityMesh npc = null, player = null;
                float npcArea = 0f, playerArea = 0f;
                for (int i = 0; i < surfaces.Count; i++)
                {
                    var surface = surfaces[i] as Component;
                    CityMesh mesh = surface != null ? Read(surface) : null;
                    if (mesh == null)
                    {
                        continue;
                    }
                    Vector3 size = mesh.Data.sourceBounds.size;
                    float area = size.x * size.z;
                    if (mesh.Agent == npcAgent && area > npcArea)
                    {
                        npc = mesh;
                        npcArea = area;
                    }
                    else if (mesh.Agent != npcAgent && area > playerArea)
                    {
                        player = mesh;
                        playerArea = area;
                    }
                }
                if (npc == null || player == null)
                {
                    _missing = "the game's walking meshes for the player and the pedestrians were not both found (" + surfaces.Count + " switched on)";
                    return false;
                }
                npc.Label = "NPC";
                player.Label = "Player";
                _npc = npc;
                _player = player;
                _missing = null;
                return true;
            }
            catch (Exception e)
            {
                _missing = "the game's walking meshes could not be read (" + e.GetType().Name + ": " + e.Message + ")";
                return false;
            }
        }

        // One surface: its data, agent type, settings and grid, all as the game has them.
        private static CityMesh Read(Component surface)
        {
            var data = GameContract.SurfaceData.Property.GetValue(surface) as NavMeshData;
            if (data == null)
            {
                return null;
            }
            var settings = (NavMeshBuildSettings)GameContract.SurfaceSettings.Method.Invoke(surface, null);
            int agent = (int)GameContract.SurfaceAgent.Property.GetValue(surface);
            var layers = (LayerMask)GameContract.SurfaceLayers.Property.GetValue(surface);
            int tiles = settings.overrideTileSize ? settings.tileSize : 256;                       // voxels: Unity's own default without the override
            float voxel = settings.overrideVoxelSize ? settings.voxelSize : settings.agentRadius / 3f;   // m: likewise
            Vector3 origin = data.position;
            return new CityMesh
            {
                Name = surface.gameObject.name,
                Surface = surface,
                Data = data,
                Agent = agent,
                Settings = settings,
                Layers = layers.value,
                Area = (int)GameContract.SurfaceArea.Property.GetValue(surface),
                Grid = new TileGrid(origin.x, origin.z, tiles * voxel),
                Filter = new NavMeshQueryFilter { agentTypeID = agent, areaMask = NavMesh.AllAreas }
            };
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

        /// <summary>The ground's height under a point, looked for from a little above <paramref name="near"/>; false where there is none.</summary>
        public static bool Ground(float x, float z, float near, out float y)
        {
            if (Physics.Raycast(new Vector3(x, near + GroundAbove, z), Vector3.down, out RaycastHit hit, GroundAbove + GroundBelow, GroundMask, QueryTriggerInteraction.Ignore))
            {
                y = hit.point.y;
                return true;
            }
            y = near;
            return false;
        }

        // What ground is: the game's ground and road colliders and the walking meshes' own layers, never a roof or a prop.
        private static int GroundMask
        {
            get
            {
                if (_groundMask == 0)
                {
                    int baked = Player != null ? _player.Layers | _npc.Layers : 0;
                    int notGround = LayerMask.GetMask("Buildings", "BuildingsOutlined", "StreetProps", "InteractiveItems", "InteractiveItemsOutlined");
                    _groundMask = (baked & ~notGround) | LayerMask.GetMask("Ground", "GroundUnplacable", "Roads");
                }
                return _groundMask;
            }
        }

        // What new ground may not be laid through: buildings, walls, props, vehicles and people.
        private static int BlockMask
        {
            get
            {
                if (_blockMask == 0)
                {
                    _blockMask = LayerMask.GetMask("Buildings", "BuildingsOutlined", "BuildingWalls", "StreetProps", "InteractiveItems", "InteractiveItemsOutlined",
                        "Vehicles", "AiVehicles", "ParkedVehicles", "PlayerVehicles", "Human");
                }
                return _blockMask;
            }
        }

        /// <summary>True when nothing of the game's stands on a box's top, from just above it to head height; else the first thing that does.</summary>
        public static bool Clear(GroundBox box, out string blocker)
        {
            var centre = new Vector3(box.X, box.Top + 1.2f, box.Z);
            var half = new Vector3(box.HalfX, 1f, box.HalfZ);
            int count = Physics.OverlapBoxNonAlloc(centre, half, Hits, Quaternion.Euler(0f, box.Yaw, 0f), BlockMask, QueryTriggerInteraction.Ignore);
            blocker = count > 0 && Hits[0] != null ? Hits[0].name + " (" + LayerMask.LayerToName(Hits[0].gameObject.layer) + ")" : null;
            return count == 0;
        }

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

        /// <summary>Rebuilds the tiles of a mesh's data that the bounds cover, from the sources, in the background; the rest stays as it is.</summary>
        public static AsyncOperation Update(NavMeshData data, NavMeshBuildSettings settings, List<NavMeshBuildSource> sources, Bounds local)
        {
            settings.preserveTilesOutsideBounds = true;   // the tiles beyond the bounds are kept as they are, never rebuilt from nothing
            return NavMeshBuilder.UpdateNavMeshDataAsync(data, settings, sources, local);
        }

        /// <summary>The same, done now: for the small checks made before the city's own data is touched.</summary>
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

        /// <summary>
        /// The old walking mesh of one agent type over a square of ground, as triangles of its own: every triangle of the
        /// world's walking meshes (NavMesh.CalculateTriangulation, all agent types together) that lies there and whose
        /// corners all stand on this agent type's mesh. One readable mesh per area, ready to be a build source; the
        /// triangles found are counted. A heavy call (every walking mesh at once): behind a pause or the loading screen.
        /// </summary>
        public static List<Mesh> OldMesh(CityMesh mesh, float minX, float minZ, float maxX, float maxZ, List<int> areas, out int triangles)
        {
            triangles = 0;
            areas.Clear();
            var meshes = new List<Mesh>(2);
            NavMeshTriangulation all = NavMesh.CalculateTriangulation();
            if (all.vertices == null || all.indices == null)
            {
                return meshes;
            }
            var points = new List<List<Vector3>>(2);
            Vector3[] v = all.vertices;
            int[] index = all.indices;
            for (int t = 0; t + 2 < index.Length; t += 3)
            {
                Vector3 a = v[index[t]], b = v[index[t + 1]], c = v[index[t + 2]];
                Vector3 middle = (a + b + c) / 3f;
                if (middle.x < minX || middle.x > maxX || middle.z < minZ || middle.z > maxZ)
                {
                    continue;
                }
                if (!Holds(mesh, middle) || !Holds(mesh, Vector3.MoveTowards(a, middle, OnMesh)) || !Holds(mesh, Vector3.MoveTowards(b, middle, OnMesh))
                    || !Holds(mesh, Vector3.MoveTowards(c, middle, OnMesh)))
                {
                    continue;
                }
                int area = all.areas != null && t / 3 < all.areas.Length ? all.areas[t / 3] : mesh.Area;
                int slot = areas.IndexOf(area);
                if (slot < 0)
                {
                    areas.Add(area);
                    points.Add(new List<Vector3>(512));
                    slot = areas.Count - 1;
                }
                points[slot].Add(a);
                points[slot].Add(b);
                points[slot].Add(c);
                triangles++;
            }
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
                meshes.Add(made);
            }
            return meshes;
        }

        /// <summary>The meshes <see cref="OldMesh"/> made go when their job is over: made by code, they are freed by code.</summary>
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

        /// <summary>The local player, or null outside a city.</summary>
        public static PlayerController Body => PlayerHelper.PlayerController;

        /// <summary>True while the player is out of doors on foot, the save loaded.</summary>
        public static bool OnFoot()
        {
            PlayerController body = Body;
            return body != null && ModState.Loaded && !BuildingManager.IsInsideBuilding && !VehicleHelper.IsInsideVehicle() && !UI.Load.LoadScene.isLoading;
        }

        public static Vector3 At => Body != null ? Body.transform.position : Vector3.zero;

        /// <summary>Puts the player back on a point of the walking mesh, the game's own way (it samples within 3 m).</summary>
        public static void Warp(Vector3 to)
        {
            PlayerController body = Body;
            if (body != null && body.Character != null)
            {
                body.Character.WarpSafely(to);
            }
        }

        /// <summary>Sends every walking pedestrian standing on a box back to the game's pool, as the game does with those too far away.</summary>
        public static int ReleaseWalkersOn(GroundBox box)
        {
            CityManager city = InstanceBehavior<CityManager>.IsInitialized ? InstanceBehavior<CityManager>.Instance : null;
            if (city == null || city.pedestrianSpawner == null || city.pedestrianSpawner.activePedestrians == null)
            {
                return 0;
            }
            Walkers.Clear();
            Walkers.AddRange(city.pedestrianSpawner.activePedestrians);   // Release takes them out of the game's list
            int released = 0;
            for (int i = 0; i < Walkers.Count; i++)
            {
                Pedestrian walker = Walkers[i];
                if (walker != null && box.Covers(walker.transform.position.x, walker.transform.position.z, 1f))
                {
                    walker.Release();
                    released++;
                }
            }
            Walkers.Clear();
            return released;
        }
    }
}
