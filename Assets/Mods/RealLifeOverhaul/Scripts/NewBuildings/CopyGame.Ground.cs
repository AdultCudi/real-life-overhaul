using System.Collections.Generic;
using System.Globalization;
using GleyTrafficSystem;
using GleyUrbanAssets;
using Helpers;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>
    /// The copy engine's check of the ground a copy is stood on, at every load before a made building is placed and in
    /// play before a test's copy: the game's own limits read now (the city map's bounds, the traffic grid), no traffic lane
    /// on it and one near enough to front, none of the game's buildings (inactive ones of a DLC too, so the answer is the
    /// same for players who own different DLCs), no DLC content and no fixed obstacle of the game's, none of the mod's own
    /// made buildings, the player's walking mesh under it and at its door, level enough, and nothing solid standing there.
    /// So a free update or a DLC that puts something there makes the building stand down and take its next lot, never cover
    /// the game's content. Costs nothing a frame: a check is a pass over the lane points and the kept boxes, a few samples
    /// of the walking mesh and one physics query that keeps no array.
    /// </summary>
    internal static partial class CopyGame
    {
        private const float SampleReach = 6f;     // m: how far from the street's height the walking mesh is looked for under a plot
        private const float SampleAside = 1.5f;   // m: a sample of the walking mesh found further aside than this is not under the point
        private const float CornerIn = 1f;        // m: the plot's corners are sampled this far inside them

        private static readonly List<float> GameBoxes = new List<float>(4096);    // the game's buildings: x0, z0, x1, z1 each
        private static readonly List<string> GameNames = new List<string>(1024);  // each one's object name, for the log
        private static readonly List<float> OtherBoxes = new List<float>(2048);   // DLC content and fixed obstacles of the game's
        private static readonly List<string> OtherNames = new List<string>(512);
        private static readonly List<float> MadeRects = new List<float>(64);      // the mod's own: x, z, facing, half width, half depth each
        private static readonly List<string> MadeNames = new List<string>(16);
        private static readonly Collider[] Hits = new Collider[16];
        private static readonly List<Renderer> Boxed = new List<Renderer>(64);
        private static int _solid;
        private static bool _othersMeasured;

        private static void BeginGround()
        {
            GameBoxes.Clear();
            GameNames.Clear();
        }

        private static void ForgetGround()
        {
            GameBoxes.Clear();
            GameNames.Clear();
            OtherBoxes.Clear();
            OtherNames.Clear();
            MadeRects.Clear();
            MadeNames.Clear();
            _othersMeasured = false;
            _lanesOf = null;
            _lanesCount = 0;
        }

        /// <summary>True once the DLC content and the fixed obstacles have been measured in this city (a load with made buildings).</summary>
        public static bool OthersMeasured => _othersMeasured;

        // A game building's ground: its box (its own mesh, which an inactive one has too), else its parts' renderers.
        private static void KeepGround(CityBuildingController c)
        {
            float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
            MeshRenderer box = c.lodVersion;
            MeshFilter mesh = box != null && !box.isPartOfStaticBatch ? box.GetComponent<MeshFilter>() : null;
            if (mesh != null && mesh.sharedMesh != null)
            {
                MeshBox(mesh, ref x0, ref z0, ref x1, ref z1);
            }
            else
            {
                Boxed.Clear();
                c.GetComponentsInChildren(true, Boxed);   // switched-off parts too: the same answer whatever a player's DLCs switch on
                for (int i = 0; i < Boxed.Count; i++)
                {
                    Renderer r = Boxed[i];
                    float rx0 = float.MaxValue, rz0 = float.MaxValue, rx1 = float.MinValue, rz1 = float.MinValue;
                    if (!r.isPartOfStaticBatch && r.TryGetComponent(out MeshFilter own) && own.sharedMesh != null)
                    {
                        MeshBox(own, ref rx0, ref rz0, ref rx1, ref rz1);   // its own mesh, which it keeps while switched off
                    }
                    else if (r.gameObject.activeInHierarchy)
                    {
                        Bounds b = r.bounds;   // a part of a batch, whose mesh is the whole batch's: its bounds, kept while it is shown
                        rx0 = b.min.x;
                        rz0 = b.min.z;
                        rx1 = b.max.x;
                        rz1 = b.max.z;
                    }
                    if (rx1 >= rx0 && rz1 >= rz0 && rx1 - rx0 + (rz1 - rz0) > 0.1f && rx1 - rx0 < 600f && rz1 - rz0 < 600f)
                    {
                        x0 = Mathf.Min(x0, rx0);
                        z0 = Mathf.Min(z0, rz0);
                        x1 = Mathf.Max(x1, rx1);
                        z1 = Mathf.Max(z1, rz1);
                    }
                }
                Boxed.Clear();
            }
            if (x1 > x0 && z1 > z0)
            {
                GameBoxes.Add(x0);
                GameBoxes.Add(z0);
                GameBoxes.Add(x1);
                GameBoxes.Add(z1);
                GameNames.Add(c.name);
            }
        }

        private static void MeshBox(MeshFilter mesh, ref float x0, ref float z0, ref float x1, ref float z1)
        {
            Bounds b = mesh.sharedMesh.bounds;
            Matrix4x4 m = mesh.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = m.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                x0 = Mathf.Min(x0, p.x);
                z0 = Mathf.Min(z0, p.z);
                x1 = Mathf.Max(x1, p.x);
                z1 = Mathf.Max(z1, p.z);
            }
        }

        /// <summary>
        /// Behind the loading screen: the DLC content (shown or hidden by ownership, both counted, so every player gets the
        /// same answer) and the game's fixed obstacles, from the load's two searches.
        /// </summary>
        public static void MeasureOthers(NavMeshObstacle[] obstacles, ToggleObjectDLC[] toggles)
        {
            OtherBoxes.Clear();
            OtherNames.Clear();
            int moving = Moving();
            for (int i = 0; obstacles != null && i < obstacles.Length; i++)
            {
                NavMeshObstacle o = obstacles[i];
                if (o == null || ((moving >> o.gameObject.layer) & 1) != 0 || o.GetComponentInParent<Rigidbody>() != null || o.GetComponentInParent<CopyLook>() != null)
                {
                    continue;   // something that moves (a car, a person), or the mod's own
                }
                Vector3 size = o.shape == NavMeshObstacleShape.Box ? o.size : new Vector3(o.radius * 2f, o.height, o.radius * 2f);
                Bounds local = new Bounds(o.center, size);
                Matrix4x4 m = o.transform.localToWorldMatrix;
                float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 p = m.MultiplyPoint3x4(new Vector3((k & 1) == 0 ? local.min.x : local.max.x, (k & 2) == 0 ? local.min.y : local.max.y, (k & 4) == 0 ? local.min.z : local.max.z));
                    x0 = Mathf.Min(x0, p.x);
                    z0 = Mathf.Min(z0, p.z);
                    x1 = Mathf.Max(x1, p.x);
                    z1 = Mathf.Max(z1, p.z);
                }
                AddOther(x0, z0, x1, z1, "an obstacle of the game's (" + o.name + ")");
            }
            for (int i = 0; toggles != null && i < toggles.Length; i++)
            {
                ToggleObjectDLC t = toggles[i];
                if (t == null)
                {
                    continue;
                }
                float x0 = float.MaxValue, z0 = float.MaxValue, x1 = float.MinValue, z1 = float.MinValue;
                Meshes.Clear();
                t.GetComponentsInChildren(true, Meshes);
                for (int k = 0; k < Meshes.Count; k++)
                {
                    if (Meshes[k] != null && Meshes[k].sharedMesh != null)
                    {
                        MeshBox(Meshes[k], ref x0, ref z0, ref x1, ref z1);
                    }
                }
                Meshes.Clear();
                AddOther(x0, z0, x1, z1, "content of a DLC (" + t.name + ")");
            }
            _othersMeasured = true;
        }

        private static void AddOther(float x0, float z0, float x1, float z1, string name)
        {
            if (x1 <= x0 || z1 <= z0 || x1 - x0 > 600f || z1 - z0 > 600f)
            {
                return;   // nothing measurable, or a whole batch
            }
            OtherBoxes.Add(x0);
            OtherBoxes.Add(z0);
            OtherBoxes.Add(x1);
            OtherBoxes.Add(z1);
            OtherNames.Add(name);
        }

        /// <summary>A made building has taken this ground: no later one is stood on it.</summary>
        public static void KeepMade(float cx, float cz, float facing, float halfWidth, float halfDepth, string name)
        {
            MadeRects.Add(cx);
            MadeRects.Add(cz);
            MadeRects.Add(facing);
            MadeRects.Add(halfWidth);
            MadeRects.Add(halfDepth);
            MadeNames.Add(name);
        }

        /// <summary>
        /// A made building taken down (a district's building knocked down in play) has let its ground go: a building made
        /// there later is not held off by it. Nothing when no building of that name kept ground.
        /// </summary>
        public static void ReleaseMade(string name)
        {
            for (int i = MadeNames.Count - 1; i >= 0; i--)
            {
                if (string.Equals(MadeNames[i], name, System.StringComparison.Ordinal))
                {
                    MadeRects.RemoveRange(i * 5, 5);
                    MadeNames.RemoveAt(i);
                }
            }
        }

        // The layers of things that move or are only there to be walked or clicked on: what stands on a plot is anything else.
        private static int Moving()
        {
            int mask = 0;
            foreach (int layer in new[]
                     {
                         LayerHelper.PlayerLayerIndex, LayerHelper.ParkedVehiclesLayerIndex, LayerHelper.VehiclesLayerIndex, LayerHelper.AiVehiclesLayerIndex,
                         LayerHelper.PlayerVehiclesLayerIndex, LayerHelper.HumanLayerIndex
                     })
            {
                mask |= layer >= 0 ? 1 << layer : 0;
            }
            return mask;
        }

        private static int Solid()
        {
            if (_solid == 0)
            {
                int not = Moving() | LayerHelper.guidersLayerMask.value | LayerHelper.triggerLayerIndex.value;
                foreach (int layer in new[]
                         {
                             LayerHelper.UiLayerIndex, LayerHelper.AutoParkSpotsLayerIndex, LayerHelper.ParkingAreaLayerIndex, LayerHelper.GroundLayerIndex,
                             LayerHelper.GroundUnplacableLayerIndex, LayerHelper.IgnoreRaycastLayerIndex, LayerMask.NameToLayer("Water"), LayerMask.NameToLayer("TransparentFX")
                         })
                {
                    not |= layer >= 0 ? 1 << layer : 0;
                }
                _solid = ~not;
            }
            return _solid;
        }

        /// <summary>
        /// Why a design cannot stand on a lot now (English, for the log), or null when it can; then where its root stands,
        /// its turn and its door, at the height of the walkable ground at that door.
        /// </summary>
        public static string GroundWhy(DesignEntry d, MadeLot lot, out Vector3 root, out float yaw, out Vector3 door, bool inPlay = false)
        {
            NewBuildingsRules.Place(d, lot, out float x, out float z, out yaw);
            NewBuildingsRules.Footprint(d, lot, out float cx, out float cz, out float hw, out float hd);
            NewBuildingsRules.DoorAt(d, x, z, yaw, out float doorX, out float doorZ);
            root = new Vector3(x, 0f, z);
            door = new Vector3(doorX, 0f, doorZ);
            float facing = lot.Facing;
            string limits = Limits(cx, cz, facing, hw, hd);
            if (limits != null)
            {
                return limits;
            }
            string street = Street(cx, cz, facing, hw, hd, out float streetY);
            if (street != null)
            {
                return street;
            }
            // Up to some 2,900 boxes a lot: the turned test, with its trigonometry, only for those within the rectangle's reach.
            NewBuildingsRules.Reach(facing, hw, hd, NewBuildingsRules.Gap, out float reachX, out float reachZ);
            for (int i = 0; i < GameNames.Count; i++)
            {
                int k = i * 4;
                float x0 = GameBoxes[k], z0 = GameBoxes[k + 1], x1 = GameBoxes[k + 2], z1 = GameBoxes[k + 3];
                if (!NewBuildingsRules.Apart(x0, z0, x1, z1, cx, cz, reachX, reachZ) && NewBuildingsRules.Overlaps(x0, z0, x1, z1, cx, cz, facing, hw, hd, NewBuildingsRules.Gap))
                {
                    return "the game's building " + GameNames[i] + " stands there";
                }
            }
            for (int i = 0; i < OtherNames.Count; i++)
            {
                int k = i * 4;
                float x0 = OtherBoxes[k], z0 = OtherBoxes[k + 1], x1 = OtherBoxes[k + 2], z1 = OtherBoxes[k + 3];
                if (!NewBuildingsRules.Apart(x0, z0, x1, z1, cx, cz, reachX, reachZ) && NewBuildingsRules.Overlaps(x0, z0, x1, z1, cx, cz, facing, hw, hd, NewBuildingsRules.Gap))
                {
                    return OtherNames[i] + " stands there";
                }
            }
            for (int i = 0; i < MadeNames.Count; i++)
            {
                int k = i * 5;
                if (RectsMeet(MadeRects[k], MadeRects[k + 1], MadeRects[k + 2], MadeRects[k + 3], MadeRects[k + 4], cx, cz, facing, hw, hd))
                {
                    return "the mod's own building '" + MadeNames[i] + "' stands there";
                }
            }
            NavMeshQueryFilter walk = Walk();
            float low = float.MaxValue, high = float.MinValue;
            for (int s = 0; s < 5; s++)
            {
                float a = s == 0 ? 0f : ((s & 1) == 0 ? -1f : 1f) * Mathf.Max(0f, hw - CornerIn);
                float b = s == 0 ? 0f : ((s & 2) == 0 ? -1f : 1f) * Mathf.Max(0f, hd - CornerIn);
                Corner(cx, cz, facing, a, b, out float px, out float pz);
                if (!Ground(px, streetY, pz, walk, out float y))
                {
                    return "there is no ground to walk on at (" + px.ToString("F0", CultureInfo.InvariantCulture) + ", " + pz.ToString("F0", CultureInfo.InvariantCulture) + ")";
                }
                low = Mathf.Min(low, y);
                high = Mathf.Max(high, y);
            }
            if (high - low > NewBuildingsRules.GroundStep)
            {
                return "the ground is not level enough (" + (high - low).ToString("F1", CultureInfo.InvariantCulture) + " m between its highest and its lowest)";
            }
            if (!NavMesh.SamplePosition(new Vector3(doorX, high, doorZ), out NavMeshHit atDoor, NewBuildingsRules.DoorReach, walk))
            {
                return "the player could not walk to its door at (" + doorX.ToString("F0", CultureInfo.InvariantCulture) + ", " + doorZ.ToString("F0", CultureInfo.InvariantCulture) + ")";
            }
            float rootY = atDoor.position.y - d.DoorY;
            root = new Vector3(x, rootY, z);
            door = new Vector3(doorX, atDoor.position.y, doorZ);
            float bottom = high + NewBuildingsRules.GroundLift;
            float height = Mathf.Max(1f, rootY + d.Height - bottom);
            var centre = new Vector3(cx, bottom + height * 0.5f, cz);
            var half = new Vector3(hw + NewBuildingsRules.Gap, height * 0.5f, hd + NewBuildingsRules.Gap);
            int found = Physics.OverlapBoxNonAlloc(centre, half, Hits, Quaternion.Euler(0f, facing, 0f), Solid(), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < found; i++)
            {
                Collider hit = Hits[i];
                if (hit != null && !(hit is TerrainCollider) && !OfMade(hit) && !(inPlay && OfLook(hit)))
                {
                    Hits[i] = null;
                    return "something solid stands there (" + hit.name + ")";
                }
            }
            return null;
        }

        // The city map's bounds and the traffic grid, read now; a limit that cannot be read is not held against the plot.
        private static string Limits(float cx, float cz, float facing, float hw, float hd)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityMapCam cam = city != null && city.cityMap != null ? city.cityMap.cityMapCam : null;
            if (cam != null && cam.bounds.size.sqrMagnitude > 1f)
            {
                Bounds b = cam.bounds;
                if (!NewBuildingsRules.Within(cx, cz, facing, hw, hd, b.min.x, b.min.z, b.max.x, b.max.z, NewBuildingsRules.EdgeMargin))
                {
                    return "it is outside the city map's bounds";
                }
            }
            CurrentSceneData roads = RoadData.Scene;
            if (roads != null && roads.grid != null && roads.grid.Length > 0 && roads.grid[0] != null && roads.grid[0].row != null && roads.gridCellSize > 0)
            {
                float x0 = roads.gridCorner.x, z0 = roads.gridCorner.z;
                float x1 = x0 + roads.grid[0].row.Length * (float)roads.gridCellSize, z1 = z0 + roads.grid.Length * (float)roads.gridCellSize;
                if (!NewBuildingsRules.Within(cx, cz, facing, hw, hd, x0, z0, x1, z1, NewBuildingsRules.EdgeMargin))
                {
                    return "it is outside the traffic system's grid";
                }
            }
            return null;
        }

        // No traffic lane on the plot, and one near enough for it to front a street; the street's height for the samples. The
        // lanes' points are sorted into squares once a load (LaneGrid), whose answers are a whole pass's: the lowest point
        // inside, and the nearest within reach.
        private static string Street(float cx, float cz, float facing, float hw, float hd, out float streetY)
        {
            streetY = 0f;
            Waypoint[] points = RoadData.Points;
            if (points == null)
            {
                return "the city's traffic lanes could not be read";
            }
            IndexLanes(points);
            int inside = Lanes.Inside(cx, cz, facing, hw, hd, NewBuildingsRules.RoadClear);
            if (inside >= 0)
            {
                return "a traffic lane runs there (" + points[inside].name + ")";
            }
            float reach = NewBuildingsRules.StreetReach + Mathf.Max(hw, hd);
            int near = Lanes.Nearest(cx, cz, reach, out _);
            if (near < 0)
            {
                return "it fronts no street (the nearest traffic lane is " + Mathf.Sqrt(NearestAnywhere(points, cx, cz)).ToString("F0", CultureInfo.InvariantCulture) + " m from its middle)";
            }
            streetY = points[near].position.y;
            return null;
        }

        private static readonly LaneGrid Lanes = new LaneGrid();
        private static Waypoint[] _lanesOf;   // the points the squares were filled from: lanes appended at a load make a new array
        private static int _lanesCount;

        private static void IndexLanes(Waypoint[] points)
        {
            if (ReferenceEquals(points, _lanesOf) && points.Length == _lanesCount)
            {
                return;
            }
            int n = points.Length;
            var xs = new float[n];
            var zs = new float[n];
            var present = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (points[i] != null)
                {
                    Vector3 p = points[i].position;
                    xs[i] = p.x;
                    zs[i] = p.z;
                    present[i] = true;
                }
            }
            Lanes.Fill(xs, zs, present, n);
            _lanesOf = points;
            _lanesCount = n;
        }

        // The squared distance to the nearest lane point anywhere: only for the words of a lot that fronts no street.
        private static float NearestAnywhere(Waypoint[] points, float cx, float cz)
        {
            float nearest = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] != null)
                {
                    Vector3 p = points[i].position;
                    nearest = Mathf.Min(nearest, (p.x - cx) * (p.x - cx) + (p.z - cz) * (p.z - cz));
                }
            }
            return nearest;
        }

        private static NavMeshQueryFilter Walk()
        {
            return new NavMeshQueryFilter { agentTypeID = BuildingManager.PlayerAgentTypeId, areaMask = NavMesh.AllAreas };
        }

        private static bool Ground(float x, float nearY, float z, NavMeshQueryFilter walk, out float y)
        {
            y = 0f;
            if (!NavMesh.SamplePosition(new Vector3(x, nearY, z), out NavMeshHit hit, SampleReach, walk))
            {
                return false;
            }
            float ax = hit.position.x - x, az = hit.position.z - z;
            y = hit.position.y;
            return ax * ax + az * az <= SampleAside * SampleAside;
        }

        private static void Corner(float cx, float cz, float facing, float across, float outward, out float x, out float z)
        {
            float f = facing * Mathf.Deg2Rad;
            float fx = Mathf.Sin(f), fz = Mathf.Cos(f);
            x = cx + across * fz + outward * fx;
            z = cz - across * fx + outward * fz;
        }

        // A collider of a copy the mod stands for its look alone (a filler beside a building, a finished building's look over a
        // site): in play, when a building is made after the load, the load's own order of things is gone (it makes the buildings
        // first, the fillers after), and a copy of a look is no building to keep clear of; the building it stands for replaces it.
        private static bool OfLook(Collider hit)
        {
            return hit.GetComponentInParent<CopyLook>() != null;
        }

        // A collider of one of the mod's own buildings: the made buildings keep apart by their footprints (RectsMeet), so a
        // district's side by side never stand each other down by a wall shared to the centimetre.
        private static bool OfMade(Collider hit)
        {
            CityBuildingController c = hit.GetComponentInParent<CityBuildingController>();
            return c != null && MadeBuildings.Of(c) != null;
        }

        // Two turned rectangles meet if any corner of one lies more than Touch inside the other, or their middles do (the one
        // inside the other): two buildings side by side, as a district's stand, share an edge and do not meet.
        private static bool RectsMeet(float ax, float az, float af, float ahw, float ahd, float bx, float bz, float bf, float bhw, float bhd)
        {
            const float In = -NewBuildingsRules.Touch;
            if (NewBuildingsRules.Inside(ax, az, bx, bz, bf, bhw, bhd, In) || NewBuildingsRules.Inside(bx, bz, ax, az, af, ahw, ahd, In))
            {
                return true;
            }
            for (int i = 0; i < 4; i++)
            {
                Corner(ax, az, af, (i & 1) == 0 ? -ahw : ahw, (i & 2) == 0 ? -ahd : ahd, out float px, out float pz);
                if (NewBuildingsRules.Inside(px, pz, bx, bz, bf, bhw, bhd, In))
                {
                    return true;
                }
                Corner(bx, bz, bf, (i & 1) == 0 ? -bhw : bhw, (i & 2) == 0 ? -bhd : bhd, out px, out pz);
                if (NewBuildingsRules.Inside(px, pz, ax, az, af, ahw, ahd, In))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
