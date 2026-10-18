using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Events
{
    /// <summary>
    /// What fires and street events need from the road: the lane beside a place, the stretch of
    /// carriageway to close, and vehicles that drive up, stand in lane and leave. This is the
    /// adapter over Scripts/Traffic: it uses only that folder's public surface (TrafficTools,
    /// ResponseUnit.Take/Spawn/Park/Unpark/Release, EmergencyDriver, SceneCordon, SceneParking,
    /// SceneObstacles, TrafficEffects' lights and siren) and edits nothing in it. Roads are
    /// closed only through a SceneCordon of the director's own, which reopens exactly the road
    /// points it closed. All of it is asked on events, never per frame.
    /// </summary>
    internal static class EventRoad
    {
        private static readonly List<Waypoint> Scratch = new List<Waypoint>(160);
        private static Vector3 _against;
        private static readonly Predicate<Waypoint> PlainLane = IsPlainLane;
        private static readonly Predicate<Waypoint> PlainLaneAgainst = IsPlainLaneAgainst;

        /// <summary>The lane nearest a place that is not in a junction, and the way it runs. False if no road is that near.</summary>
        public static bool Lane(Vector3 near, float within, out Waypoint point, out Vector3 direction)
        {
            direction = default;
            point = TrafficTools.Ready ? TrafficTools.ClosestWaypoint(near, within, PlainLane) : null;
            return point != null && Direction(point, out direction);
        }

        /// <summary>The middle of the lane running the other way beside a lane, if there is one within a carriageway's width.</summary>
        public static bool Oncoming(Waypoint lane, Vector3 direction, out Vector3 middle)
        {
            middle = default;
            _against = direction;
            Waypoint other = TrafficTools.ClosestWaypoint(lane.position, 9f, PlainLaneAgainst);
            if (other == null)
            {
                return false;
            }
            // The point of that lane abreast of ours, not merely its nearest road point.
            Vector3 offset = other.position - lane.position;
            offset.y = 0f;
            Vector3 across = offset - direction * Vector3.Dot(offset, direction);
            if (across.sqrMagnitude < 1.5f * 1.5f)
            {
                return false;
            }
            middle = lane.position + across;
            return true;
        }

        public static bool Direction(Waypoint point, out Vector3 direction)
        {
            direction = default;
            if (point == null || point.neighbors == null || point.neighbors.Count == 0)
            {
                return false;
            }
            Waypoint next = TrafficManager.Instance.GetWaypoint(point.neighbors[0]);
            if (next == null)
            {
                return false;
            }
            direction = next.position - point.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                return false;
            }
            direction.Normalize();
            return true;
        }

        private static bool IsPlainLane(Waypoint point)
        {
            return point.neighbors != null && point.neighbors.Count == 1 && point.prev != null && point.prev.Count == 1 && !point.temporaryDisabled
                   && TrafficManager.IsNotIntersection(point);
        }

        private static bool IsPlainLaneAgainst(Waypoint point)
        {
            return IsPlainLane(point) && Direction(point, out Vector3 direction) && Vector3.Dot(direction, _against) < -0.6f;
        }

        /// <summary>A stretch of carriageway as the cordon wants it described: something lying in the road that it goes round.</summary>
        public static Footprint Stretch(Vector3 centre, Vector3 direction, float halfLength, float halfWidth)
        {
            return new Footprint { Centre = centre, Forward = direction, HalfLength = halfLength, HalfWidth = halfWidth, Vehicle = false };
        }

        public static Footprint Of(VehicleComponent car)
        {
            TrafficTools.Footprint(car, out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth);
            return new Footprint { Centre = centre, Forward = forward, HalfLength = halfLength, HalfWidth = halfWidth, Vehicle = true };
        }

        /// <summary>
        /// Sets a parked unit driving away along the road ahead of it. False if there is no road
        /// ahead to take (the caller then releases it into traffic where it stands).
        /// </summary>
        public static bool DriveOff(ResponseUnit unit, bool quiet)
        {
            VehicleComponent car = unit.Car;
            if (car != null)
            {
                SceneObstacles.Remove(car);   // whatever becomes of it, it stands here no longer
            }
            if (car == null || unit.IsLost)
            {
                return false;
            }
            Waypoint ahead = TrafficTools.ClosestWaypointAlong(car.transform.position, car.transform.forward, 30f);
            if (ahead == null || TrafficTools.WalkForward(ahead, 130f, 60, Scratch) <= 20f)
            {
                Scratch.Clear();
                return false;
            }
            unit.Path.Clear();
            for (int i = 0; i < Scratch.Count; i++)
            {
                unit.Path.Add(Scratch[i].position);
            }
            Scratch.Clear();
            unit.Unpark(quiet);
            unit.Driver.Begin(car, unit.Path, unit.Path[unit.Path.Count - 1], 6f, 90f);
            return true;
        }

        /// <summary>
        /// Walks the road graph back from an anchor and puts a pooled vehicle of a prefab at the far
        /// end, held by the unit as a plain car of the mod's, its route filled in. Null with the
        /// reason if it could not be done.
        /// </summary>
        public static VehicleComponent Bring(ResponseUnit unit, GameObject prefab, Waypoint anchor, float routeLength, float minimum, out string refusal)
        {
            refusal = null;
            if (unit.InUse || anchor == null || prefab == null || !TrafficTools.Ready)
            {
                refusal = unit.InUse ? "the unit is still out" : anchor == null ? "there is no road point to head for" : prefab == null ? "the traffic pool has no truck or van free" : "traffic is not running";
                return null;
            }
            float walked = 0f;
            for (int attempt = 0; attempt < 4 && walked < minimum; attempt++)
            {
                walked = TrafficTools.WalkBack(anchor, routeLength, 160, Scratch);
            }
            if (walked < minimum || Scratch.Count < 3)
            {
                refusal = "the road leading there is only " + walked.ToString("F0") + " m long";
                Scratch.Clear();
                return null;
            }
            VehicleComponent car = null;
            int start = 0;
            for (int i = 0; i < 3 && i < Scratch.Count - 2 && car == null; i++)
            {
                start = i;
                car = TrafficTools.Load(prefab, Scratch[i]);   // an idle car of that prefab, never one taken by its model
            }
            if (car == null || !unit.Take(car))
            {
                if (car != null)
                {
                    TrafficTools.Remove(car, "a fire engine that could not be taken");
                }
                refusal = "the first road points of its route are all taken";
                Scratch.Clear();
                return null;
            }
            for (int i = start + 1; i < Scratch.Count; i++)
            {
                unit.Path.Add(Scratch[i].position);
            }
            Scratch.Clear();
            return car;
        }

        /// <summary>
        /// The pooled vehicle a fire engine is made of. With the modelled body in the prop bundle:
        /// an idle truck whose axles are the body's (the delivery truck, which the body is made
        /// to), so that the engine is exactly as long as what carries it and its wheels are where
        /// that turns. Else, and without the body: an idle truck (the freight truck before any
        /// other), else an idle van; those are repainted, never given the body. Only idle
        /// instances, so nothing in view is taken.
        /// </summary>
        public static GameObject PickTruck(out bool van)
        {
            van = false;
            List<VehicleComponent> pool = Manager.GetVehicleList();
            bool body = PropLibrary.TryGet(EngineLivery.Body, out _, out _);
            GameObject freight = null;
            GameObject truck = null;
            GameObject fallback = null;
            for (int i = 0; i < pool.Count; i++)
            {
                VehicleComponent car = pool[i];
                if (car == null || car.prefab == null || car.gameObject.activeInHierarchy || TrafficTools.IsAmbulance(car))
                {
                    continue;
                }
                string name = car.prefab.name;
                if (body && EngineLivery.Carries(car))
                {
                    return car.prefab;
                }
                if (name.IndexOf("Freight", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (!body)
                    {
                        return car.prefab;
                    }
                    freight = freight != null ? freight : car.prefab;
                }
                else if (truck == null && (car.vehicleType == VehicleTypes.Truck || name.IndexOf("Truck", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    truck = car.prefab;
                }
                else if (fallback == null && (name.IndexOf("Van", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Dash", StringComparison.OrdinalIgnoreCase) >= 0
                                              || name.IndexOf("Desert", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    fallback = car.prefab;
                }
            }
            truck = freight != null ? freight : truck;
            van = truck == null && fallback != null;
            return truck != null ? truck : fallback;
        }
    }

    /// <summary>
    /// What makes a pooled truck a fire engine, on an object of its own under the truck: the
    /// modelled body "fire_engine" over it if the prop bundle has one and the truck is the one
    /// the body is made to (the delivery truck: the same length, the same axles; the truck's own
    /// meshes hidden, as the patrol car's are, so the body's wheels are all that is seen and
    /// they stand on the truck's axles); otherwise a red repaint of the truck's bodywork through
    /// property blocks (glass, lamps, wheels and chrome left alone) and a light bar on the roof.
    /// The modelled body has its own light bar: none is added to it, only a lit lens over each
    /// of its ends for <see cref="BarFlash"/> to show in turn.
    /// It undoes itself: when it is destroyed, or when the truck goes back into the traffic pool,
    /// every renderer gets back exactly the block (or none) and the visibility it had.
    /// </summary>
    internal sealed class EngineLivery : MonoBehaviour
    {
        // The modelled body (PropAssets/README.md, "Service vehicles"; checks_service.py holds the model to these), in its
        // own space: X right, Y up, Z to the nose, from the ground under the middle of its length.
        public const string Body = "fire_engine";
        public const string BarLens = "fire_bar_lens";
        public const float BodyLength = 7.43f;
        public const float BodyHalfWidth = 1.25f;
        public const float BodyAxleFront = 2.756f;    // the delivery truck's axles: 4.118 m apart, the rear one 2.353 m from the back
        public const float BodyAxleRear = -1.362f;
        public const float PanelOut = 1.22f;          // the pump panels, on both sides
        public const float PanelAhead = 0.756f;
        public const float OutletAhead = -0.333f;     // the discharge outlets, low on both sides
        public const float CrewDoorAhead = 1.78f;
        public const float FrontDoorAhead = 2.756f;
        public const float BarTop = 3.20f;
        private static readonly Vector3 BarAt = new Vector3(0f, 3.006f, 2.535f);   // the middle of the underside of its light bar
        private const float AxlesFit = 0.25f;         // metres of wheelbase the truck under it may differ by
        private const float LengthFit = 0.4f;         // and metres of length

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color EngineRed = new Color(0.78f, 0.05f, 0.04f, 1f);
        private static readonly string[] Spared = { "glass", "window", "light", "lamp", "wheel", "tire", "tyre", "rim", "chrome", "interior", "mirror", "plate", "driver" };
        private static readonly List<Renderer> RendererScratch = new List<Renderer>(32);
        private static readonly List<Material> MaterialScratch = new List<Material>(8);
        private static MaterialPropertyBlock _paint;

        private struct Painted
        {
            public Renderer Renderer;
            public int Slot;
            public MaterialPropertyBlock Before;   // null if it had none
        }

        private readonly List<Painted> _painted = new List<Painted>(24);
        private readonly List<Renderer> _hidden = new List<Renderer>(24);
        private Renderer _lensLeft, _lensRight;

        /// <summary>The modelled body, if this truck wears it; null on a repainted truck.</summary>
        public Transform Modelled { get; private set; }

        /// <summary>The lit lens over the left (0) and the right (1) end of the modelled body's light bar, switched off; null without the body.</summary>
        public Renderer Lens(int end) => end == 0 ? _lensLeft : _lensRight;

        /// <summary>Where the flashing lights of the modelled body belong: just over its light bar.</summary>
        public Vector3 Beacon => Modelled.TransformPoint(new Vector3(0f, BarTop + 0.3f, BarAt.z));

        /// <summary>True for a vehicle the modelled body is made to: its wheelbase and its length are the body's (a van with the same wheelbase is not).</summary>
        public static bool Carries(VehicleComponent car)
        {
            return Mathf.Abs(car.wheelDistance - (BodyAxleFront - BodyAxleRear)) <= AxlesFit && Mathf.Abs(car.length - BodyLength) <= LengthFit;
        }

        /// <summary>Dresses a truck. Returns the livery (on an object of its own, a child of the truck), or null if nothing could be done to it.</summary>
        public static EngineLivery Dress(VehicleComponent car)
        {
            car.GetComponentsInChildren(false, RendererScratch);   // before anything of ours is under it
            var holder = new GameObject("RealLifeOverhaul Engine Livery");
            holder.transform.SetParent(car.transform, false);
            var livery = holder.AddComponent<EngineLivery>();
            Vector3 scale = car.transform.lossyScale;
            holder.transform.localScale = new Vector3(Inverse(scale.x), Inverse(scale.y), Inverse(scale.z));
            // The body only over the truck it is made to, and its axles on that truck's own (the trucks have their origin under
            // the rear axle, not under their middle): on anything else it would stand out over one end, short of the other,
            // and its wheels would slide through every bend.
            float rear = 0f, front = 0f;
            bool modelled = Carries(car) && TrafficTools.Axles(car, RendererScratch, out rear, out front)
                            && Mathf.Abs(front - rear - (BodyAxleFront - BodyAxleRear)) <= AxlesFit && Shapes.Prop(Body, holder.transform);
            if (modelled)
            {
                Transform body = holder.transform.GetChild(holder.transform.childCount - 1);
                body.localPosition = new Vector3(0f, 0f, ((rear + front) * 0.5f - (BodyAxleRear + BodyAxleFront) * 0.5f) * scale.z);
                body.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;   // the truck's own shadow went with its meshes
                livery.Modelled = body;
                livery._lensLeft = LitLens(body, 180f);
                livery._lensRight = LitLens(body, 0f);
            }
            for (int i = 0; i < RendererScratch.Count; i++)
            {
                Renderer renderer = RendererScratch[i];
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }
                if (modelled)
                {
                    if (!renderer.forceRenderingOff)
                    {
                        renderer.forceRenderingOff = true;
                        livery._hidden.Add(renderer);
                    }
                    continue;
                }
                livery.Repaint(renderer);
            }
            RendererScratch.Clear();
            if (!modelled)
            {
                // The light bar, on the highest part of the truck, over the middle of its length (which is not its origin).
                float roof = TrafficEffects.RoofHeight(car);
                TrafficTools.Footprint(car, out Vector3 centre, out _, out _, out _);
                var bar = new GameObject("Light Bar");
                bar.transform.SetParent(holder.transform, false);
                bar.transform.localPosition = new Vector3(0f, roof, car.transform.InverseTransformPoint(centre).z * scale.z);
                EventPropsBar.Build(bar.transform);
            }
            return livery;
        }

        // One end of the modelled light bar again, in the glow material, a hair proud of it; shown only while that end is lit.
        private static Renderer LitLens(Transform body, float turn)
        {
            if (!PropLibrary.TryGet(BarLens, out Mesh mesh, out Material material) || mesh == null || material == null)
            {
                return null;
            }
            Transform lens = Shapes.Piece(BarLens, body, mesh, material, BarAt, Vector3.one);
            lens.localRotation = Quaternion.Euler(0f, turn, 0f);
            var renderer = lens.GetComponent<MeshRenderer>();
            renderer.enabled = false;
            return renderer;
        }

        private static float Inverse(float scale)
        {
            return Mathf.Abs(scale) > 0.001f ? 1f / scale : 1f;
        }

        private void Repaint(Renderer renderer)
        {
            if (Skip(renderer.name))
            {
                return;
            }
            if (_paint == null)
            {
                _paint = new MaterialPropertyBlock();
            }
            renderer.GetSharedMaterials(MaterialScratch);
            for (int slot = 0; slot < MaterialScratch.Count; slot++)
            {
                Material material = MaterialScratch[slot];
                if (material == null || Skip(material.name))
                {
                    continue;
                }
                int property = material.HasProperty(BaseColorId) ? BaseColorId : material.HasProperty(ColorId) ? ColorId : 0;
                if (property == 0)
                {
                    continue;
                }
                MaterialPropertyBlock before = null;
                if (renderer.HasPropertyBlock())
                {
                    before = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(before, slot);
                    if (before.isEmpty)
                    {
                        before = null;
                    }
                }
                // Whatever the block already carried for this slot is kept; only the colour is ours.
                renderer.GetPropertyBlock(_paint, slot);
                _paint.SetColor(property, EngineRed);
                renderer.SetPropertyBlock(_paint, slot);
                _painted.Add(new Painted { Renderer = renderer, Slot = slot, Before = before });
            }
            MaterialScratch.Clear();
        }

        private static bool Skip(string name)
        {
            for (int i = 0; i < Spared.Length; i++)
            {
                if (name.IndexOf(Spared[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        // Runs when the livery is destroyed or the truck under it is switched off (returned to the traffic pool).
        private void OnDisable()
        {
            for (int i = 0; i < _painted.Count; i++)
            {
                Painted painted = _painted[i];
                if (painted.Renderer != null)
                {
                    painted.Renderer.SetPropertyBlock(painted.Before, painted.Slot);
                }
            }
            _painted.Clear();
            for (int i = 0; i < _hidden.Count; i++)
            {
                if (_hidden[i] != null)
                {
                    _hidden[i].forceRenderingOff = false;
                }
            }
            _hidden.Clear();
            Destroy(gameObject);
        }
    }

    /// <summary>The light bar's shapes, shared by the livery (it is part of the truck's dressing, not a pooled prop).</summary>
    internal static class EventPropsBar
    {
        public static void Build(Transform root)
        {
            if (Shapes.Prop(EventProps.LightBar, root) || !Shapes.Available)
            {
                return;
            }
            Shapes.Piece("Bar", root, Shapes.Cube, Shapes.Paint(new Color(0.04f, 0.04f, 0.045f)), new Vector3(0f, 0.03f, 0f), new Vector3(1.30f, 0.06f, 0.28f));
            Shapes.Piece("Red", root, Shapes.Cube, Shapes.Paint(new Color(0.95f, 0.06f, 0.05f), 5f), new Vector3(-0.36f, 0.11f, 0f), new Vector3(0.52f, 0.11f, 0.24f));
            Shapes.Piece("Clear", root, Shapes.Cube, Shapes.Paint(new Color(1f, 0.96f, 0.84f), 3f), new Vector3(0f, 0.11f, 0f), new Vector3(0.18f, 0.11f, 0.24f));
            Shapes.Piece("Red 2", root, Shapes.Cube, Shapes.Paint(new Color(0.95f, 0.06f, 0.05f), 5f), new Vector3(0.36f, 0.11f, 0f), new Vector3(0.52f, 0.11f, 0.24f));
        }
    }

    /// <summary>
    /// The modelled engine's light bar flashing: the lit lens over one end is shown, then the one
    /// over the other, in step with the two lights above it (TrafficEffects' flasher: the same
    /// clock, the same interval). On the lights' own object, so it exists only while an engine
    /// is out with its lights on, touches the two renderers only on the frames the state flips,
    /// and leaves both lenses dark when the lights go.
    /// </summary>
    internal sealed class BarFlash : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("events", "BarFlash", typeof(BarFlash));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        public Renderer Left;
        public Renderer Right;
        public float Interval;

        private int _shown = -1;

        private static readonly int HotUpdate = HotSpots.Slot("Events: BarFlash.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            int shown = Interval > 0f && Mathf.FloorToInt(Time.unscaledTime / Interval) % 2 == 0 ? 0 : 1;
            if (shown != _shown)
            {
                _shown = shown;
                Show(shown == 0, shown == 1);
            }
        }

        private void OnDisable()
        {
            _shown = -1;
            Show(false, false);
        }

        private void Show(bool left, bool right)
        {
            if (Left != null)
            {
                Left.enabled = left;
            }
            if (Right != null)
            {
                Right.enabled = right;
            }
        }
    }

    /// <summary>
    /// The fire engine: a pooled truck dressed as one (EngineLivery), with red and white lights
    /// and a siren, driven by the same emergency driver as the ambulance and the patrol cars. It
    /// stops in its lane short of the fire, is cut out of the navigation mesh while it stands,
    /// and always leaves: driving on when the road ahead is clear, or handed back to traffic.
    /// </summary>
    internal sealed class FireEngine
    {
        private readonly ResponseUnit _unit;
        private readonly Transform _host;
        private GameObject _lights;
        private EngineLivery _livery;

        public FireEngine(GameObject host)
        {
            _unit = new ResponseUnit(host);
            _host = host.transform;
        }

        public ResponseUnit Unit => _unit;
        public VehicleComponent Car => _unit.Car;
        public bool Out => _unit.InUse;
        public bool Lost => _unit.IsLost;
        public bool IsVan { get; private set; }
        public string Refusal { get; private set; }

        /// <summary>True while it wears the modelled body: places on it are then known (<see cref="BodyPoint"/>).</summary>
        public bool Modelled => _livery != null && _livery.Modelled != null;

        /// <summary>A place given in the modelled body's own space (metres to its right, up, and ahead of the middle of its length), in the world.</summary>
        public Vector3 BodyPoint(float right, float up, float ahead)
        {
            return _livery.Modelled.TransformPoint(new Vector3(right, up, ahead));
        }

        /// <summary>Puts the engine on the road a run-in away and sets it driving to its place in lane short of the scene.</summary>
        public bool Dispatch(SceneCordon cordon, Vector3 scene, float back)
        {
            Waypoint anchor = null;
            Vector3 stand = default;
            bool hasStand = cordon != null && cordon.Stand(false, back, out anchor, out stand);
            if (!hasStand)
            {
                anchor = TrafficTools.ClosestWaypoint(scene, 40f);
            }
            GameObject prefab = EventRoad.PickTruck(out bool van);
            VehicleComponent car = EventRoad.Bring(_unit, prefab, anchor, Random.Range(150f, 210f), 45f, out string refusal);
            if (car == null)
            {
                Refusal = refusal;
                return false;
            }
            IsVan = van;
            _unit.Driver.Lit = true;   // lit wherever it drives (its unit puts that out when it lets the truck go)
            try
            {
                const float flash = 0.16f;
                _livery = EngineLivery.Dress(car);
                _lights = TrafficEffects.CreateFlasher(car, "RealLifeOverhaul Engine Lights", TrafficEffects.RoofHeight(car) + 0.3f,
                    new Color(1f, 0.08f, 0.06f), new Color(1f, 0.95f, 0.85f), flash);
                if (Modelled)
                {
                    // The lights over the body's own light bar (the truck's origin is under its rear axle), and the bar's two
                    // ends lit in turn with them.
                    _lights.transform.position = _livery.Beacon;
                    if (_livery.Lens(0) != null && _livery.Lens(1) != null)
                    {
                        var bar = _lights.AddComponent<BarFlash>();
                        bar.Left = _livery.Lens(0);     // the red light is the left one of the pair
                        bar.Right = _livery.Lens(1);
                        bar.Interval = flash;
                    }
                }
                if (Settings.Siren)
                {
                    TrafficEffects.AddSiren(_lights, car, SirenTone.Wail);
                }
            }
            catch (Exception e)
            {
                EventDirector.Logger?.Error(e);
            }
            if (hasStand)
            {
                Vector3 beyond = stand - anchor.position;
                beyond.y = 0f;
                if (beyond.sqrMagnitude > 1f)
                {
                    _unit.Path.Add(stand);
                }
                _unit.Driver.BeginRoute(car, _unit.Path, 28f);
            }
            else
            {
                _unit.Driver.Begin(car, _unit.Path, scene, back, 28f);
            }
            string wears = Modelled ? "wearing the modelled body with its axles on the truck's"
                : PropLibrary.TryGet(EngineLivery.Body, out _, out _) ? FormattableString.Invariant($"repainted (wheelbase {car.wheelDistance:F2}: not the truck the modelled body is made to)")
                : "repainted (no modelled body in the prop bundle)";
            EventDirector.Logger?.Info($"Fire: {TrafficTools.Name(car)} dispatched as the fire engine{(van ? " (a van: no truck was free)" : "")}, {wears}, {_unit.Path.Count} road points, "
                                       + (hasStand ? $"to stand {back:F0} m short of the fire in its lane." : "no lane to stand in: it pulls up where it can."));
            return true;
        }

        /// <summary>It has stopped: pinned where it stands, siren off, lights on, and people on foot go round it.</summary>
        public void Park()
        {
            _unit.Park();
            if (_unit.Car != null && !_unit.IsLost)
            {
                SceneObstacles.Add(_unit.Car, _host);
                Quiet();
            }
        }

        private void Quiet()
        {
            if (_lights != null && _lights.TryGetComponent(out AudioSource siren))
            {
                siren.Stop();
            }
        }

        /// <summary>Drives away along the road ahead. False if it could only be handed back to traffic where it stood (it is released either way once gone).</summary>
        public bool Leave()
        {
            if (!_unit.InUse)
            {
                return false;
            }
            if (EventRoad.DriveOff(_unit, quiet: true))
            {
                return true;
            }
            Release(resumeTraffic: true, remove: false);
            return false;
        }

        /// <summary>Lets the truck go. The livery stays on one that drives off until the traffic pool takes it in; otherwise all of it comes off now.</summary>
        public void Release(bool resumeTraffic, bool remove)
        {
            VehicleComponent car = _unit.Car;
            if (car != null)
            {
                SceneObstacles.Remove(car);
            }
            bool drivesOff = _unit.InUse && !_unit.IsLost && resumeTraffic && !remove && TrafficTools.Ready;
            if (_lights != null)
            {
                Quiet();
                Object.Destroy(_lights, drivesOff ? 9f : 0f);
            }
            if (_livery != null && !drivesOff)
            {
                Object.Destroy(_livery.gameObject);
            }
            _lights = null;
            _livery = null;
            _unit.Release(resumeTraffic, remove, quiet: true);
        }
    }
}
