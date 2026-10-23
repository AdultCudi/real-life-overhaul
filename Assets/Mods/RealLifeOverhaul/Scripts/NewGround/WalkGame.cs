using System;
using System.Collections;
using System.Collections.Generic;
using Extensions;
using Helpers;
using Streets.Pedestrians;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.NewGround
{
    /// <summary>
    /// Where new ground touches the game's walking: with its pure part (WalkGame.Build.cs), the only file that calls Unity's
    /// navigation API or reads the game's walking meshes. The two meshes the city walks on (the game's NavMeshSurfaces
    /// NavMeshPlayer and NavMeshNPC, both baked in the editor; the game never builds any at run time) are found at every
    /// load from the surfaces the game has switched on, never assumed: the pedestrians' agent type is the game's own filter's
    /// (NavMeshHelper.NpcNavMeshFilter), the player's is the other large one, and each mesh's tile grid and settings are its
    /// own. This part holds what reads the game: the surfaces, the ground and what stands on new ground, the player and
    /// the pedestrians. Called on a load, a test or a build's end, never per frame.
    /// </summary>
    public static partial class WalkGame
    {
        public const float GroundAbove = 3f;     // m above a height a ray for the ground starts
        public const float GroundBelow = 12f;    // m below it the ground is looked for
        public const float HeadHeight = 2.2f;    // m above new ground something counts as standing on it
        public const float WidestObstacle = 60f; // m: anything wider standing on new ground is a whole building or more, never a prop to walk round

        private static CityMesh _player;
        private static CityMesh _npc;
        private static string _missing;
        private static readonly Collider[] Hits = new Collider[8];
        private static readonly Collider[] Many = new Collider[1024];
        private static readonly List<Pedestrian> Walkers = new List<Pedestrian>(32);
        private static int _groundMask;
        private static int _blockMask;
        private static int _obstacleMask;

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
            _builtMask = 0;
            _obstacleMask = 0;
            ForgetTriangles();
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

        /// <summary>
        /// How many of the game's colliders stand on a box's top (from a little above it to 4 m): buildings, walls, parking
        /// and the street's own pieces, never the ground it lies on nor the mod's own, nor a small prop at the edge (a
        /// lamp or a tree, GroundRules.IsProp). A plan's count is kept with it; more at a later load means the game has
        /// built there since. Each one counted is named into <paramref name="seen"/>, a few at most, for the line that
        /// stands the plan down.
        /// </summary>
        public static int Built(GroundBox box, List<string> seen)
        {
            if (_builtMask == 0)
            {
                _builtMask = LayerMask.GetMask("Buildings", "BuildingsOutlined", "BuildingWalls", "StreetProps", "InteractiveItems", "InteractiveItemsOutlined",
                    "ParkingArea", "Sidewalk", "Crosswalk", "Roads");
                _propMask = LayerMask.GetMask("StreetProps", "InteractiveItems", "InteractiveItemsOutlined");
            }
            var centre = new Vector3(box.X, box.Top + 2.2f, box.Z);
            var half = new Vector3(box.HalfX, 1.8f, box.HalfZ);
            int count = Physics.OverlapBoxNonAlloc(centre, half, Standing, Quaternion.Euler(0f, box.Yaw, 0f), _builtMask, QueryTriggerInteraction.Ignore);
            int built = 0;
            for (int i = 0; i < count; i++)
            {
                Collider c = Standing[i];
                if (c == null || c.transform.root.name.StartsWith("RealLifeOverhaul", StringComparison.Ordinal))
                {
                    continue;
                }
                Bounds b = c.bounds;
                if (GroundRules.IsProp((_propMask & (1 << c.gameObject.layer)) != 0, b.size.x, b.size.z))
                {
                    continue;
                }
                built++;
                if (seen != null && seen.Count < SeenMost)
                {
                    seen.Add("'" + c.name + "' under '" + c.transform.root.name + "' (" + LayerMask.LayerToName(c.gameObject.layer) + ", at "
                             + b.center.x.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) + ", " + b.center.z.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)
                             + ", " + b.size.x.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " x "
                             + b.size.z.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " m)");
                }
            }
            return built;
        }

        /// <summary>
        /// Everything solid standing on a box's top, the game's and the mod's own (a lamp of a copied street, a tree, a
        /// bin, a pole), as boxes along the world's axes from just under the box's top to the thing's own top: the walking
        /// mesh is built with no surface under them, as the game's own bake leaves none under its street furniture. Each
        /// collider once (<paramref name="seen"/> holds the ones found already). Never the ground, a road, a pavement or a
        /// parking lane, nor a vehicle or a person; nothing wider than <see cref="WidestObstacle"/>.
        /// </summary>
        public static int Obstacles(GroundBox box, HashSet<int> seen, List<GroundBox> into)
        {
            if (_obstacleMask == 0)
            {
                _obstacleMask = LayerMask.GetMask("Default", "Buildings", "BuildingsOutlined", "BuildingWalls", "StreetProps", "InteractiveItems", "InteractiveItemsOutlined", "Water");
            }
            var centre = new Vector3(box.X, box.Top + HeadHeight * 0.5f, box.Z);
            var half = new Vector3(box.HalfX, HeadHeight * 0.5f, box.HalfZ);
            int count = Physics.OverlapBoxNonAlloc(centre, half, Many, Quaternion.Euler(0f, box.Yaw, 0f), _obstacleMask, QueryTriggerInteraction.Ignore);
            int added = 0;
            for (int i = 0; i < count; i++)
            {
                Collider c = Many[i];
                Many[i] = null;
                if (c == null || !c.enabled || !seen.Add(c.GetInstanceID()))
                {
                    continue;
                }
                Bounds b = c.bounds;
                if (b.size.x > WidestObstacle || b.size.z > WidestObstacle || b.max.y < box.Top + 0.05f)
                {
                    continue;
                }
                float top = Mathf.Min(b.max.y, box.Top + HeadHeight), bottom = box.Top - box.Depth;
                into.Add(GroundRules.Obstacle(b.center.x, b.center.z, b.extents.x, b.extents.z, top, bottom));
                added++;
            }
            return added;
        }

        private const int SeenMost = 3;   // colliders named in a stand-down's line
        private static readonly Collider[] Standing = new Collider[64];
        private static int _builtMask, _propMask;

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
