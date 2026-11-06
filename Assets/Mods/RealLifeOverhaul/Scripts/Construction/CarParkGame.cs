using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Helpers;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Paint = UnityEngine.Material;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// A car park standing in the world: a root under the site's middle turned by its yaw, the ground (surface, paint, low kerb and the
    /// dropped kerb at the way in), the rows of bays as lanes of the game's parking, the pay booth with its pointer, the barrier posts and
    /// the lamps. Made by <see cref="CarParkGame.Stand"/>, taken away by <see cref="CarParkGame.Remove"/>.
    /// </summary>
    internal sealed class CarParkWorld
    {
        public int Id;
        public GameObject Root;
        public Vector3 At;
        public int Yaw, Width, Depth;
        public Bounds Box;
        public readonly List<ParkingLaneGenerator> Lanes = new List<ParkingLaneGenerator>(8);
        public readonly List<Light> Lights = new List<Light>(12);
        public readonly List<GameObject> Hidden = new List<GameObject>(2);
        public readonly List<Mesh> Meshes = new List<Mesh>(2);
        public CarParkPointer Pointer;
        public int LaneBays;        // bays that have a lane (the bays for the disabled have none)
        public int Lamps;
        public int Barriers;
        public bool Surface, Ramp, Booth;
        public bool Modelled;       // the booth is the props bundle's model, not the plain stand-in
        public bool Night;
        public int Free = -1;       // the chance of a free bay last given to the lanes, per cent
        public int Asked = -1;      // ... and the one their cars were last drawn at
        public bool Ok;
        public string Why = string.Empty;
    }

    /// <summary>
    /// The construction game's Game file for car parks: the only place that makes, reads or takes away what a car park is in the
    /// scene. The game's parking lane (ParkingLaneGenerator) is a box on the ParkingArea layer that parks the game's cars along itself
    /// and makes a car of the player's "Legal" where all four wheels stand in one; a row of bays is a copy of one of the game's, set to
    /// park its cars side by side (the same copy as new ground's, made asleep under a switched-off holder from a lane found once behind
    /// the loading screen), so the game's own cars park in the bays and the player's car parks there as in the street. The lanes' share
    /// of free bays follows the hour's occupancy (Fill), redrawn by the game's own queue and never while the player is within 80 m.
    /// Called when a car park stands or goes, an hour's pass and a press; nothing per frame. COST: a stand is a few meshes and lanes, once.
    /// </summary>
    internal static class CarParkGame
    {
        public const string RootName = "RealLifeOverhaul car park";
        public const string LaneName = "RealLifeOverhaul car park lane";
        private const string HolderName = "RealLifeOverhaul car park lanes (asleep)";
        // The holder of Uptown's street furniture (Scripts/Uptown/StreetsGame): its pieces may give way to a way in.
        private const string FurnitureHolder = "RealLifeOverhaul Uptown street furniture";
        private const string ReservedName = "Reserved Parking Spot";                         // a spot the game holds for a car on its way (ParkingLaneGenerator.TryReserveSpot)
        private const float ClearFrom = 0.2f;        // m above the ground the way in is looked at from: over the kerb, under nothing
        private const float ClearHalf = 1.2f;        // m: half the height of the way in's box
        private const float InverseChance = 15f;     // per cent of cars the lanes park the other way round (backed in)
        private const int ChangedBy = 12;            // points of free share from which the lanes draw their cars again
        private const int LampLights = 12;           // lamps of one car park that carry a light of their own
        private const float LampHeight = 5.4f;       // m
        private const float LampLumens = 9000f;
        private const float LampRange = 14f;
        private const int NightFrom = 18, NightTo = 7;   // the hours the lamps are lit between

        private static readonly FieldInfo SideField = GameContract.LaneSideBySide.Field;
        private static readonly FieldInfo SpotField = GameContract.LaneSpotDistance.Field;
        private static readonly FieldInfo CountField = GameContract.LaneSpotCount.Field;
        private static readonly FieldInfo CurvedField = GameContract.LaneCurved.Field;
        private static readonly FieldInfo VehiclesField = GameContract.LaneVehicles.Field;
        private static readonly FieldInfo AngleField = GameContract.LaneSideAngle.Field;
        private static readonly FieldInfo InverseField = GameContract.LaneInverseChance.Field;
        private static readonly FieldInfo MinFreeField = GameContract.LaneMinFree.Field;

        private static readonly Collider[] Near = new Collider[48];
        private static readonly List<Transform> Children = new List<Transform>(16);
        private static readonly Plane[] Planes = new Plane[6];
        private static readonly Vector3[] Corners = new Vector3[4];
        private static readonly StringBuilder Words = new StringBuilder(160);

        private static GameObject _holder;
        private static ParkingLaneGenerator _template;
        private static float _centreY, _sizeY, _base;
        private static int _tries, _planesFrame = -1, _blockMask, _groundMask;
        private static bool _fieldsSaid;
        private static Paint[] _paints;

        /// <summary>True when a car park can be stood: the game's lit shader is there (the lanes are optional, said in the check).</summary>
        internal static bool Ready => Shapes.Available;

        /// <summary>True when a lane of the game's was found to copy.</summary>
        internal static bool HasLane => _template != null;

        // ------------------------------------------------------------------ the lane to copy

        /// <summary>
        /// Behind the loading screen: a lane of the game's found once (a straight one of a street, not a van's spot, not one of the
        /// mod's) and a clean copy of it kept asleep for every row of bays. True when done; false to be asked again at the next black
        /// screen outdoors (a few times).
        /// </summary>
        internal static bool Prewarm()
        {
            if (_template != null)
            {
                return true;
            }
            if (SideField == null || SpotField == null)
            {
                if (!_fieldsSaid)
                {
                    _fieldsSaid = true;
                    ConstructionDirector.Logger?.Warn("Construction: the game's parking lane has no side-by-side setting in this build, so car parks stand without parked cars"
                                                      + " and the player's car is not made legal in their bays.");
                }
                return true;
            }
            ParkingLaneGenerator[] all = SceneFinds.All<ParkingLaneGenerator>("car parks: the game's parking lanes being listed for the lane to copy");
            ParkingLaneGenerator pick = null;
            for (int i = 0; i < all.Length; i++)
            {
                ParkingLaneGenerator lane = all[i];
                if (!Fit(lane) || (pick != null && (lane.building != null || pick.building == null)))
                {
                    continue;
                }
                pick = lane;
                if (pick.building == null)
                {
                    break;
                }
            }
            if (pick == null)
            {
                _tries++;
                ConstructionDirector.Logger?.Warn("Construction: none of the " + all.Length + " parking lane(s) in the scene could be copied for car parks (try " + _tries + ").");
                return _tries >= 3;
            }
            try
            {
                MakeTemplate(pick);
            }
            catch (Exception e)
            {
                ConstructionDirector.Logger?.Error(e);
                Forget();
                return true;
            }
            return true;
        }

        // A lane of a street, straight, on the parking layer, at the scene's own size, not made by the mod.
        private static bool Fit(ParkingLaneGenerator lane)
        {
            if (lane == null || lane.designatedDeliveryVanSpot || lane.isHandicapParking || lane.name.StartsWith("RealLifeOverhaul", StringComparison.Ordinal)
                || lane.gameObject.layer != LayerHelper.ParkingAreaLayerIndex)
            {
                return false;
            }
            var box = lane.GetComponent<BoxCollider>();
            if (box == null || box.size.x < 0.1f || box.size.z < 0.1f || lane.transform.Find("Lane Segment 0") != null)
            {
                return false;
            }
            Vector3 scale = lane.transform.lossyScale;
            return Mathf.Abs(scale.x - 1f) < 0.05f && Mathf.Abs(scale.y - 1f) < 0.05f && Mathf.Abs(scale.z - 1f) < 0.05f;
        }

        private static void MakeTemplate(ParkingLaneGenerator pick)
        {
            var source = pick.GetComponent<BoxCollider>();
            Vector3 centre = pick.transform.TransformPoint(source.center);
            _base = Ground(centre, out float ground) ? Mathf.Clamp(centre.y - ground, -0.5f, 1.5f) : 0f;   // how far above the ground the game puts a parked car's pivot
            _centreY = source.center.y;
            _sizeY = Mathf.Max(0.6f, source.size.y);
            _holder = new GameObject(HolderName);
            _holder.SetActive(false);
            GameObject copy = UnityEngine.Object.Instantiate(pick.gameObject, _holder.transform);
            copy.name = LaneName + " (template)";
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Children.Clear();
            foreach (Transform child in copy.transform)
            {
                Children.Add(child);
            }
            for (int i = 0; i < Children.Count; i++)
            {
                // The scene lane's cars and spots came along as children: copies the game's pool never handed out, so they go.
                int layer = Children[i].gameObject.layer;
                if (layer == LayerHelper.ParkedVehiclesLayerIndex || layer == LayerHelper.AutoParkSpotsLayerIndex || Children[i].name == ReservedName)
                {
                    SceneryTrees.DestroyNow(Children[i].gameObject);
                }
            }
            Children.Clear();
            var lane = copy.GetComponent<ParkingLaneGenerator>();
            lane.building = null;
            lane.designatedDeliveryVanSpot = false;
            lane.isHandicapParking = false;
            lane.autoParkSpots.Clear();
            Set(SideField, lane, true);
            Set(SpotField, lane, CarParkRules.BayWidth);
            Set(CountField, lane, 0);
            Set(CurvedField, lane, false);
            Set(AngleField, lane, 0f);
            Set(InverseField, lane, (int)InverseChance);
            Set(MinFreeField, lane, 0);
            Set(VehiclesField, lane, null);
            _template = lane;
            ConstructionDirector.Logger?.Info("Construction: the parking lane " + pick.name + " found; car parks copy it for their rows of bays (a parked car's pivot stands "
                                              + _base.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " m over the ground).");
        }

        private static void Set(FieldInfo field, object target, object value)
        {
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        private static bool Ground(Vector3 over, out float y)
        {
            if (_groundMask == 0)
            {
                _groundMask = LayerMask.GetMask("Ground", "GroundUnplacable", "Roads");
            }
            if (Physics.Raycast(over + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, _groundMask, QueryTriggerInteraction.Ignore))
            {
                y = hit.point.y;
                return true;
            }
            y = 0f;
            return false;
        }

        /// <summary>The city is going: the lane kept, its holder and the paints are forgotten.</summary>
        internal static void Forget()
        {
            if (_holder != null)
            {
                SceneryTrees.Destroy(_holder);
            }
            _holder = null;
            _template = null;
            _tries = 0;
            _blockMask = 0;
            _groundMask = 0;
            _planesFrame = -1;
            _paints = null;
        }

        // ------------------------------------------------------------------ the way in

        /// <summary>
        /// The first of the plans (most bays first) whose way in is clear of what the game puts on the pavement and the road's edge: a
        /// street lamp, a pole, a tree, a stop. Uptown's own street furniture (a bench, a bin, a tree in its hole) gives way when the
        /// car park stands. False with the first thing in the way when every plan is blocked.
        /// </summary>
        internal static bool PickMouth(LotView site, List<CarParkLayout> candidates, out CarParkLayout plan, out string blocker)
        {
            plan = null;
            blocker = null;
            var at = new Vector3(site.X, site.Y, site.Z);
            for (int i = 0; candidates != null && i < candidates.Count; i++)
            {
                if (MouthFree(at, site.Yaw, candidates[i], null, out string why))
                {
                    plan = candidates[i];
                    return true;
                }
                blocker = blocker ?? why;
            }
            return false;
        }

        // The box of the way in, from the road's kerb across the pavement to just inside the site, as the plan puts it.
        private static void MouthBox(Vector3 at, int yaw, CarParkLayout l, out Vector3 centre, out Vector3 half, out Quaternion turn)
        {
            float z0 = -l.Depth * 0.5f - CarParkMesh.RampTo, z1 = -l.Depth * 0.5f + 1f;
            float x0 = l.Mouth.X0 + 0.15f, x1 = l.Mouth.X1 - 0.15f;
            turn = Quaternion.Euler(0f, yaw, 0f);
            centre = at + turn * new Vector3((x0 + x1) * 0.5f, ClearFrom + ClearHalf, (z0 + z1) * 0.5f);
            half = new Vector3((x1 - x0) * 0.5f, ClearHalf, (z1 - z0) * 0.5f);
        }

        // True when nothing of the game's stands in the way in; Uptown's own street furniture there is put in <paramref name="give"/> (when asked for), the rest named.
        private static bool MouthFree(Vector3 at, int yaw, CarParkLayout l, List<GameObject> give, out string blocker)
        {
            blocker = null;
            if (_blockMask == 0)
            {
                _blockMask = LayerMask.GetMask("Default", "Buildings", "BuildingsOutlined", "BuildingWalls", "StreetProps", "InteractiveItems", "InteractiveItemsOutlined");
            }
            MouthBox(at, yaw, l, out Vector3 centre, out Vector3 half, out Quaternion turn);
            int count = Physics.OverlapBoxNonAlloc(centre, half, Near, turn, _blockMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = Near[i];
                if (hit == null)
                {
                    continue;
                }
                Bounds b = hit.bounds;
                if (b.max.y < at.y + 0.3f || b.size.x > 30f || b.size.z > 30f)
                {
                    continue;   // a kerb, a marking, a plane of the ground: not in a car's way
                }
                GameObject piece = FurniturePiece(hit.transform);
                if (piece != null)
                {
                    if (give != null && !give.Contains(piece))
                    {
                        give.Add(piece);
                    }
                    continue;
                }
                blocker = blocker ?? hit.transform.root.name + "/" + hit.name;
                return false;
            }
            return true;
        }

        // The piece of Uptown's street furniture a collider belongs to (the child of the furniture's holder), or null.
        private static GameObject FurniturePiece(Transform t)
        {
            while (t != null && t.parent != null)
            {
                if (t.parent.name == FurnitureHolder)
                {
                    return t.gameObject;
                }
                t = t.parent;
            }
            return null;
        }

        // ------------------------------------------------------------------ seen

        /// <summary>Whether any of a car park's ground (and the way in) is in the camera's picture or on the city map's: it stands only when none is.</summary>
        internal static bool InView(LotView site, CarParkLayout layout)
        {
            if (CityMap.IsOpen)
            {
                return true;
            }
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return false;
            }
            Bounds box = BoxOf(new Vector3(site.X, site.Y, site.Z), site.Yaw, layout.Width, layout.Depth);
            if (box.SqrDistance(camera.transform.position) > 400f * 400f)
            {
                return false;
            }
            if (_planesFrame != Time.frameCount)
            {
                GeometryUtility.CalculateFrustumPlanes(camera, Planes);
                _planesFrame = Time.frameCount;
            }
            return GeometryUtility.TestPlanesAABB(Planes, box);
        }

        /// <summary>Whether the player (or the car they drive) is on the ground of a site, with two metres round it.</summary>
        internal static bool PlayerOn(LotView site)
        {
            if (PlayerHelper.PlayerController == null)
            {
                return false;
            }
            Vector3 p = PlayerHelper.GetPosition();
            double a = site.Yaw * Math.PI / 180.0;
            float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a), dx = p.x - site.X, dz = p.z - site.Z;
            float lx = cos * dx - sin * dz, lz = sin * dx + cos * dz;   // the site's own axes
            return Mathf.Abs(lx) <= site.Width * 0.5f + 2f && lz >= -site.Depth * 0.5f - CarParkMesh.RampTo - 2f && lz <= site.Depth * 0.5f + 2f;
        }

        // The box round a site and its way in: its four corners turned into the world, the way in's reach, a few metres of height.
        private static Bounds BoxOf(Vector3 at, int yaw, int width, int depth)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            float hx = width * 0.5f, hz = depth * 0.5f;
            Corners[0] = at + turn * new Vector3(-hx, 0f, -hz - CarParkMesh.RampTo);
            Corners[1] = at + turn * new Vector3(hx, 0f, -hz - CarParkMesh.RampTo);
            Corners[2] = at + turn * new Vector3(hx, 0f, hz);
            Corners[3] = at + turn * new Vector3(-hx, 0f, hz);
            var box = new Bounds(Corners[0], Vector3.zero);
            for (int i = 1; i < Corners.Length; i++)
            {
                box.Encapsulate(Corners[i]);
            }
            box.Encapsulate(Corners[0] + Vector3.up * 6f);
            box.Encapsulate(Corners[2] - Vector3.up * 0.5f);
            return box;
        }

        /// <summary>The ground a car park stands on as a rectangle of the world (x1, z1, x2, z2), for the works' dressing that comes down with it.</summary>
        internal static void GroundBox(CarParkWorld world, out float x1, out float z1, out float x2, out float z2)
        {
            x1 = world.Box.min.x;
            z1 = world.Box.min.z;
            x2 = world.Box.max.x;
            z2 = world.Box.max.z;
        }

        // ------------------------------------------------------------------ standing

        /// <summary>
        /// A car park made on its site: asleep, then woken whole. <paramref name="lanes"/> false stands it without the game's parked cars (a
        /// Going Public session, where the host's would be sent to the others); <paramref name="freePercent"/> is the share of bays the
        /// game's cars leave free as they park. Null with why only when nothing could be made (no shader).
        /// </summary>
        internal static CarParkWorld Stand(int id, LotView site, CarParkLayout layout, string neighbourhood, bool lanes, int freePercent, out string why)
        {
            why = null;
            if (!Ready)
            {
                why = "the game's lit shader was not found";
                return null;
            }
            var at = new Vector3(site.X, site.Y, site.Z);
            var world = new CarParkWorld { Id = id, At = at, Yaw = site.Yaw, Width = layout.Width, Depth = layout.Depth, Box = BoxOf(at, site.Yaw, layout.Width, layout.Depth) };
            var root = new GameObject(RootName + " " + id);
            root.SetActive(false);   // nothing of it wakes until it is whole
            root.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, site.Yaw, 0f));
            world.Root = root;
            try
            {
                Lay(world, layout, at, site.Yaw);
                Furnish(world, layout);
                if (lanes)
                {
                    Rows(world, layout, neighbourhood, freePercent);
                }
                root.SetActive(true);
                SetLights(world, Night());
                Check(world, layout, lanes);
            }
            catch (Exception e)
            {
                ConstructionDirector.Logger?.Error(e);
                why = e.GetType().Name + ": " + e.Message;
                Remove(world);
                return null;
            }
            return world;
        }

        // The surface with its paint and low kerb, and the dropped kerb at the way in with the collider the cars cross.
        private static void Lay(CarParkWorld world, CarParkLayout layout, Vector3 at, int yaw)
        {
            Paint[] paints = Paints();
            Transform root = world.Root.transform;
            Mesh surface = CarParkMesh.Surface(layout, "RealLifeOverhaul car park surface");
            world.Meshes.Add(surface);
            Draw("Surface", root, surface, paints, false, 0);
            world.Surface = true;
            Mesh ramp = CarParkMesh.Ramp(layout, "RealLifeOverhaul car park way in");
            world.Meshes.Add(ramp);
            Draw("Way in", root, ramp, paints, true, GroundLayer(at, yaw, layout));
            world.Ramp = true;
            var moved = new List<GameObject>(2);
            MouthFree(at, yaw, layout, moved, out _);
            for (int i = 0; i < moved.Count; i++)
            {
                moved[i].SetActive(false);   // Uptown's own bench, bin or tree where the way in goes
                world.Hidden.Add(moved[i]);
            }
        }

        // The layer the game's ground at the way in is on, so a car's wheels and a walker's feet meet the ramp as they meet the pavement.
        private static int GroundLayer(Vector3 at, int yaw, CarParkLayout layout)
        {
            if (_groundMask == 0)
            {
                _groundMask = LayerMask.GetMask("Ground", "GroundUnplacable", "Roads");
            }
            Vector3 over = at + Quaternion.Euler(0f, yaw, 0f) * new Vector3(layout.Mouth.MidX, 0f, -layout.Depth * 0.5f - 2f);
            if (Physics.Raycast(over + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 8f, _groundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.collider.gameObject.layer;
            }
            return 0;
        }

        private static GameObject Draw(string name, Transform parent, Mesh mesh, Paint[] paints, bool solid, int layer)
        {
            var piece = new GameObject(name);
            piece.layer = layer;
            piece.transform.SetParent(parent, false);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = paints;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (solid)
            {
                piece.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            return piece;
        }

        // The five flat paints of CarParkMesh's submeshes, in its order.
        private static Paint[] Paints()
        {
            if (_paints == null || _paints.Length != CarParkMesh.Submeshes || _paints[0] == null)
            {
                _paints = new Paint[CarParkMesh.Submeshes];
                _paints[CarParkMesh.Asphalt] = Shapes.Paint(new Color(0.075f, 0.075f, 0.08f));
                _paints[CarParkMesh.White] = Shapes.Paint(new Color(0.88f, 0.88f, 0.86f));
                _paints[CarParkMesh.Blue] = Shapes.Paint(new Color(0.10f, 0.32f, 0.78f));
                _paints[CarParkMesh.Concrete] = Shapes.Paint(new Color(0.55f, 0.55f, 0.53f));
                _paints[CarParkMesh.Yellow] = Shapes.Paint(new Color(0.88f, 0.70f, 0.10f));
            }
            return _paints;
        }

        // ------------------------------------------------------------------ the pieces

        private static void Furnish(CarParkWorld world, CarParkLayout layout)
        {
            Transform root = world.Root.transform;
            // The pay booth in the bay beside the throat, facing it; it is solid, and the pointer is round it.
            ParkPoint b = layout.Booth;
            Transform booth = Model(root, "dc_booth", b.X, 0f, b.Z, b.Yaw, new Vector3(1.84f, 2.42f, 1.84f), new Color(0.86f, 0.87f, 0.88f), out bool modelled);
            world.Modelled = modelled;
            Solid(booth, new Vector3(0f, 1.21f, 0f), new Vector3(1.84f, 2.42f, 1.84f), true);
            world.Pointer = CarParkPointer.Make(booth, new Vector3(0f, 1.2f, 0f), new Vector3(2.2f, 2.5f, 2.2f), world.Id);
            world.Booth = booth != null;
            // The barrier's posts flank the throat.
            for (int i = 0; i < layout.Barriers.Count; i++)
            {
                ParkPoint p = layout.Barriers[i];
                Transform post = Model(root, "dc_barrier", p.X, 0f, p.Z, p.Yaw + (i == 0 ? 90f : 270f), new Vector3(0.4f, 1.05f, 0.4f), new Color(0.88f, 0.70f, 0.10f), out _);
                Solid(post, new Vector3(0f, 0.52f, 0f), new Vector3(0.4f, 1.05f, 0.4f), true);
                world.Barriers++;
            }
            // The lamps along the aisles: a pole and a glowing head, a light at night on some.
            Paint steel = Shapes.Paint(new Color(0.30f, 0.31f, 0.33f)), glow = Shapes.Paint(new Color(1f, 0.93f, 0.75f), 3f);
            for (int i = 0; i < layout.Lamps.Count; i++)
            {
                ParkPoint p = layout.Lamps[i];
                var lamp = new GameObject("Lamp " + i).transform;
                lamp.SetParent(root, false);
                lamp.localPosition = new Vector3(p.X, CarParkMesh.SurfaceY, p.Z);
                Shapes.Piece("pole", lamp, Shapes.Post, steel, Vector3.zero, new Vector3(0.18f, LampHeight, 0.18f));
                Shapes.Piece("head", lamp, Shapes.Cube, glow, new Vector3(0f, LampHeight + 0.05f, 0f), new Vector3(0.95f, 0.1f, 0.34f));
                Solid(lamp, new Vector3(0f, LampHeight * 0.5f, 0f), new Vector3(0.3f, LampHeight, 0.3f), true);
                if (world.Lights.Count < LampLights)
                {
                    var spot = new GameObject("Light");
                    spot.transform.SetParent(lamp, false);
                    spot.transform.localPosition = new Vector3(0f, LampHeight - 0.2f, 0f);
                    var light = spot.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.9f, 0.7f);
                    light.range = LampRange;
                    light.shadows = LightShadows.None;
                    var data = spot.AddComponent<HDAdditionalLightData>();
                    data.SetIntensity(LampLumens, LightUnit.Lumen);
                    data.range = LampRange;
                    light.enabled = false;
                    world.Lights.Add(light);
                }
                world.Lamps++;
            }
        }

        // A modelled prop of the bundle at a place of the car park, or a box of its size in a colour; the origin is its middle on the ground.
        private static Transform Model(Transform parent, string id, float x, float y, float z, float yaw, Vector3 size, Color colour, out bool modelled)
        {
            var holder = new GameObject("RealLifeOverhaul " + id).transform;
            holder.SetParent(parent, false);
            holder.localPosition = new Vector3(x, CarParkMesh.SurfaceY + y, z);
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
            modelled = Shapes.Prop(id, holder);
            if (!modelled)
            {
                Shapes.Piece("Stand-in", holder, Shapes.Cube, Shapes.Paint(colour), new Vector3(0f, size.y * 0.5f, 0f), size);
            }
            return holder;
        }

        // What stands in the way: a box the player's body meets (and cars), and the same box cut out of the walking mesh.
        private static void Solid(Transform piece, Vector3 centre, Vector3 size, bool body)
        {
            if (body)
            {
                var box = piece.gameObject.AddComponent<BoxCollider>();
                box.center = centre;
                box.size = size;
            }
            var cut = piece.gameObject.AddComponent<NavMeshObstacle>();
            cut.shape = NavMeshObstacleShape.Box;
            cut.center = centre;
            cut.size = new Vector3(Mathf.Max(0.3f, size.x - 0.2f), size.y, Mathf.Max(0.3f, size.z - 0.2f));
            cut.carving = true;
            cut.carveOnlyStationary = true;
        }

        // ------------------------------------------------------------------ the bays

        // A lane of the game's for each row of bays; the first row's bays for the disabled, painted blue, have none (no car parks there, and a car of the player's is not legal there).
        private static void Rows(CarParkWorld world, CarParkLayout layout, string neighbourhood, int freePercent)
        {
            if (_template == null || _holder == null)
            {
                return;
            }
            Transform root = world.Root.transform;
            for (int i = 0; i < layout.Rows.Count; i++)
            {
                ParkRow row = layout.Rows[i];
                int skip = i == 0 ? layout.Accessible : 0;
                int bays = row.Bays - skip;
                if (bays <= 0)
                {
                    continue;
                }
                float length = bays * CarParkRules.BayWidth, shift = skip * CarParkRules.BayWidth * 0.5f;
                float x = row.X, z = row.Z;
                if (row.Along)
                {
                    z += shift;
                }
                else
                {
                    x += row.NearHigh ? -shift : shift;
                }
                GameObject go = UnityEngine.Object.Instantiate(_template.gameObject, _holder.transform);   // asleep
                go.name = LaneName + " " + world.Id + "." + i;
                var lane = go.GetComponent<ParkingLaneGenerator>();
                lane.neighbourhood = neighbourhood;
                lane.chanceOfFreeSpot = Mathf.Clamp(freePercent, 0, 100);
                var box = go.GetComponent<BoxCollider>();
                box.size = new Vector3(length, _sizeY, CarParkRules.BayDepth);
                box.center = new Vector3(0f, _centreY, 0f);
                Transform t = go.transform;
                t.SetParent(root, false);
                t.localPosition = new Vector3(x, CarParkMesh.SurfaceY + _base - _centreY, z);
                t.localRotation = Quaternion.Euler(0f, row.Yaw, 0f);
                t.localScale = Vector3.one;
                world.Lanes.Add(lane);
                world.LaneBays += bays;
            }
            world.Free = Mathf.Clamp(freePercent, 0, 100);
            world.Asked = world.Free;
        }

        // ------------------------------------------------------------------ the hour

        /// <summary>
        /// The share of free bays the lanes park their cars to follows the hour's occupancy (the cars are drawn again by the game's own queue,
        /// at once for a change of a dozen points or more, never while the player is within 80 m), and the lamps are lit at night.
        /// </summary>
        internal static void Fill(CarParkWorld world, int freePercent)
        {
            if (world == null || world.Root == null)
            {
                return;
            }
            SetLights(world, Night());
            if (world.Lanes.Count == 0)
            {
                return;
            }
            freePercent = Mathf.Clamp(freePercent, 0, 100);
            bool again = world.Asked < 0 || Math.Abs(freePercent - world.Asked) >= ChangedBy;
            world.Free = freePercent;
            for (int i = 0; i < world.Lanes.Count; i++)
            {
                ParkingLaneGenerator lane = world.Lanes[i];
                if (lane == null)
                {
                    continue;
                }
                lane.chanceOfFreeSpot = freePercent;
                if (again)
                {
                    ParkingSimulator.parkingQueueWorker.AddWork(lane);
                }
            }
            if (again)
            {
                world.Asked = freePercent;
            }
        }

        private static bool Night()
        {
            int hour = ConstructionGame.Hour;
            return hour >= NightFrom || hour < NightTo;
        }

        private static void SetLights(CarParkWorld world, bool night)
        {
            world.Night = night;
            for (int i = 0; i < world.Lights.Count; i++)
            {
                if (world.Lights[i] != null)
                {
                    world.Lights[i].enabled = night;
                }
            }
        }

        /// <summary>The game's cars parked in a car park's lanes now, and the bays its lanes hold.</summary>
        internal static int Parked(CarParkWorld world, out int bays)
        {
            bays = world != null ? world.LaneBays : 0;
            int parked = 0;
            for (int i = 0; world != null && i < world.Lanes.Count; i++)
            {
                ParkingLaneGenerator lane = world.Lanes[i];
                if (lane == null)
                {
                    continue;
                }
                foreach (Transform child in lane.transform)
                {
                    parked += child.gameObject.layer == LayerHelper.ParkedVehiclesLayerIndex ? 1 : 0;
                }
            }
            return parked;
        }

        // ------------------------------------------------------------------ the check

        // What stood, said once: the pieces a car park is made of against what its plan holds.
        private static void Check(CarParkWorld world, CarParkLayout layout, bool lanes)
        {
            var missing = new List<string>(4);
            if (!world.Surface)
            {
                missing.Add("the surface");
            }
            if (!world.Ramp)
            {
                missing.Add("the dropped kerb");
            }
            if (!world.Booth)
            {
                missing.Add("the pay booth");
            }
            if (world.Pointer == null)
            {
                missing.Add("the pay machine's pointer");
            }
            if (world.Lamps != layout.Lamps.Count)
            {
                missing.Add("lamps");
            }
            if (lanes && world.Lanes.Count == 0)
            {
                missing.Add(_template == null ? "any lane of the game's to copy" : "the lanes");
            }
            world.Ok = missing.Count == 0;
            world.Why = missing.Count == 0 ? string.Empty : "missing: " + string.Join(", ", missing.ToArray());
        }

        /// <summary>The middle of a bay of a lane (counted from the lane's end the game's cars begin to park at) on the surface, and the way a car in it points.</summary>
        internal static void BayAt(ParkingLaneGenerator lane, int bay, out Vector3 middle, out Quaternion turn)
        {
            var box = lane.GetComponent<BoxCollider>();
            float along = box.size.x * 0.5f - (bay + 0.5f) * CarParkRules.BayWidth;
            middle = lane.transform.TransformPoint(new Vector3(box.center.x + along, 0f, box.center.z));
            middle.y = lane.transform.parent != null ? lane.transform.parent.position.y + CarParkMesh.SurfaceY : middle.y;
            turn = lane.transform.rotation;
        }

        /// <summary>
        /// How many of a car's four wheels, the car standing at a place turned a way, are over a lane of this car park that is not for the
        /// disabled, as the game's own test of a car left in the street (CarController.UpdateParkingZone) finds them; the lane's district in
        /// <paramref name="neighbourhood"/>. A car with all four is legal and is charged the district's rate.
        /// </summary>
        internal static int WheelsOver(CarParkWorld world, Vector3 middle, Quaternion turn, out string neighbourhood)
        {
            neighbourhood = null;
            Physics.SyncTransforms();
            int over = 0;
            for (int i = 0; world != null && i < 4; i++)
            {
                var offset = new Vector3(i % 2 == 0 ? -0.8f : 0.8f, 0.33f - 0.1f, i < 2 ? -1.35f : 1.35f);
                int count = Physics.OverlapBoxNonAlloc(middle + turn * offset, new Vector3(0.1f, 0.66f, 0.1f), Near, turn, LayerHelper.parkingAreaLayerMask);
                for (int k = 0; k < count; k++)
                {
                    ParkingLaneGenerator lane = Near[k] != null ? Near[k].GetComponentInParent<ParkingLaneGenerator>() : null;
                    if (lane != null && !lane.isHandicapParking && world.Lanes.Contains(lane))
                    {
                        over++;
                        neighbourhood = lane.neighbourhood;
                        break;
                    }
                }
            }
            return over;
        }

        /// <summary>A car park standing in words, for the check line in Player.log.</summary>
        internal static string Describe(CarParkWorld world)
        {
            if (world == null)
            {
                return "not standing";
            }
            int parked = Parked(world, out int bays);
            Words.Length = 0;
            Words.Append(world.Width).Append(" by ").Append(world.Depth).Append(" m, ").Append(world.Lanes.Count).Append(" lane(s) of ").Append(bays).Append(" bay(s) with ")
                 .Append(parked).Append(" parked at stand, ").Append(world.Lamps).Append(" lamp(s) (").Append(world.Lights.Count).Append(" lit at night), ")
                 .Append(world.Barriers).Append(" barrier post(s), the booth ").Append(world.Modelled ? "modelled" : "a stand-in box").Append(", ").Append(world.Hidden.Count)
                 .Append(" piece(s) of street furniture given way");
            return Words.ToString();
        }

        // ------------------------------------------------------------------ taking away

        /// <summary>
        /// A car park taken away: its lanes give their cars back to the game's pool first, the furniture it displaced comes back, its meshes go.
        /// Safe on null and on a half-made one.
        /// </summary>
        internal static void Remove(CarParkWorld world)
        {
            if (world == null)
            {
                return;
            }
            for (int i = 0; i < world.Lanes.Count; i++)
            {
                try
                {
                    if (world.Lanes[i] != null)
                    {
                        world.Lanes[i].CleanupParkedVehicles(force: true);
                    }
                }
                catch (Exception e)
                {
                    ConstructionDirector.Logger?.Error(e);   // the pool may already be gone with the city: the lane goes with its cars
                }
            }
            for (int i = 0; i < world.Hidden.Count; i++)
            {
                if (world.Hidden[i] != null)
                {
                    world.Hidden[i].SetActive(true);
                }
            }
            world.Pointer = null;
            if (world.Root != null)
            {
                SceneryTrees.Destroy(world.Root);
            }
            for (int i = 0; i < world.Meshes.Count; i++)
            {
                CarParkMesh.Drop(world.Meshes[i]);
            }
            world.Lanes.Clear();
            world.Lights.Clear();
            world.Hidden.Clear();
            world.Meshes.Clear();
            world.Root = null;
            world.Ok = false;
        }
    }
}
