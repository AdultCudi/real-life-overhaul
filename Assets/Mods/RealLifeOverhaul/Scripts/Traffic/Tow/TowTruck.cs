using System.Collections.Generic;
using GleyTrafficSystem;
using Helpers;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city tow truck the mod sends for a wreck: a vehicle of its own, as the city bus is (<see cref="BusVehicle"/>), and
    /// driven by the same driver (<see cref="BusDriver"/>). A cab-over rollback 8.3 m long and 2.40 m wide over its bed, on
    /// two axles 4.6 m apart (the city bus's wheels, drawn at 0.45 m), in the livery of a firm of the city's own, with an amber
    /// light bar on its cab. Its body, subframe, bed and lamps are the modelled ones (PropAssets, props_tow.py); without them
    /// there is no truck (<see cref="Make"/> says so) and the wreck goes as it always has.
    ///
    /// IT IS SOLID, as the bus is: one box from 0.3 m over the road to its cab roof, a kinematic body on the layer
    /// 'Vehicles', and traffic is told when it goes (Manager.TriggerColliderRemovedEvent).
    ///
    /// IT SHOWS WHAT IT DOES. Its wheels roll and steer, its body leans and dips on its springs, the brake lamps come on as it
    /// slows and while it stands, from dusk its head and tail lamps are lit. At work its light bar flashes (the bar's two lit
    /// halves in turn, with two amber lights over it so that it lights the street at night) and its hazard lamps blink. The
    /// bed (<see cref="Deck"/>) turns and slides as TowRules.DeckPose says, the ram under it following; the winch's cable
    /// (<see cref="Cable"/>) runs from the drum to whatever it is hooked to.
    ///
    /// One per <see cref="TowService"/>, made the first time a truck is sent and switched off in between. Its Update runs only
    /// while it is out (a few transform writes; the lamps are touched only when one of them changes).
    /// </summary>
    public sealed class TowTruck : MonoBehaviour, IDrivenVehicle
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("traffic", "TowTruck", typeof(TowTruck));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        public const float AxleFront = TowRules.AxleFront;
        public const float AxleRear = TowRules.AxleRear;
        public const float Wheelbase = AxleFront - AxleRear;
        public const float NoseAhead = TowRules.Nose - AxleFront;   // the nose, ahead of the front axle
        public const float Tail = TowRules.BedTail;                 // the tail of the bed at rest, ahead of the middle
        public const float Length = TowRules.Nose - TowRules.BedTail;
        public const float HalfWidth = TowRules.HalfWidth;
        public const float Roof = 2.62f;                            // the cab's roof: the top of the solid box
        public const float WheelRadius = 0.45f;                     // the city bus's wheel (0.48) drawn at this size

        // Places on it, in its own space at rest (props_tow.py).
        public static readonly Vector3 TiltPivot = new Vector3(0f, TowRules.PivotUp, TowRules.PivotAhead);
        public static readonly Vector3 WinchAt = new Vector3(0f, 1.18f, 0.84f);       // where the cable leaves the winch (on the bed)
        public static readonly Vector3 BarAt = new Vector3(0f, 2.615f, 2.40f);        // the middle of the light bar's underside
        public const float PanelAhead = 0.75f;                      // the control levers, on both sides
        public const float PanelOut = 1.24f;                        // ...their knobs this far out
        public const float DoorAhead = 2.30f;                       // the cab's doors, on both sides
        private static readonly Vector3 RamFoot = new Vector3(0f, 0.60f, -1.00f);     // the ram's mount on the chassis...
        private static readonly Vector3 RamHead = new Vector3(0f, 0.84f, -2.50f);     // ...and under the subframe, at rest

        private const float SolidFrom = 0.30f;
        private const float TrackFront = 0.98f;
        private const float TrackRear = 0.90f;
        private const float BusWheel = 0.48f;
        private const float BlinkEvery = 0.68f;     // an indicator's cycle, seconds
        private const float BarEvery = 0.35f;       // the light bar's halves, seconds each
        private const float CableThick = 0.018f;

        private enum Light { Head, Tail, Brake, Left, Right, BarA, BarB, Count }

        private static readonly Color Amber = new Color(1f, 0.58f, 0.06f);

        private Rigidbody _body;
        private BoxCollider _solid;
        private Transform _shell;
        private Transform _tilt;
        private Transform _slide;
        private Transform _ramBarrel;
        private Transform _ramRod;
        private Transform _cable;
        private GameObject _bar;
        private readonly Transform[] _steer = new Transform[2];
        private readonly Transform[] _spin = new Transform[4];
        private readonly List<Renderer>[] _lights = new List<Renderer>[(int)Light.Count];
        private readonly bool[] _lit = new bool[(int)Light.Count];
        private bool _beacon;
        private bool _hazards;
        private bool _braking;
        private float _deck;
        private float _spun;
        private float _steered;
        private float _lean, _leanSpeed, _dip, _dipSpeed;
        private float _posedAt;     // when its driver last posed it

        public bool Out => gameObject.activeSelf;
        public Rigidbody Body => _body;
        public Collider Solid => _solid;
        public Transform Frame => transform;
        float IDrivenVehicle.FrontAxleAhead => AxleFront;
        float IDrivenVehicle.RearAxleAhead => AxleRear;
        float IDrivenVehicle.AxleSpan => Wheelbase;
        float IDrivenVehicle.NoseBeyondAxle => NoseAhead;
        float IDrivenVehicle.HalfBreadth => HalfWidth;

        /// <summary>What leans and dips on the springs: the bed, and whatever is on it, are carried by it.</summary>
        public Transform Shell => _shell;

        /// <summary>The bed: a point of it in the truck's own space at rest, through this, is where that point of the bed is now.</summary>
        public Transform Bed => _slide;

        /// <summary>The bed's pose, 0 at rest and 1 down (TowRules.DeckPose).</summary>
        public float DeckAt => _deck;

        /// <summary>True when the bundle has every part of the truck: there is no truck of plain shapes.</summary>
        public static bool Available =>
            Shapes.Available && PropLibrary.Has("tow_truck") && PropLibrary.Has("tow_truck_bed") && PropLibrary.Has("tow_truck_tilt")
            && PropLibrary.Has("city_bus_wheel") && PropLibrary.Has("tow_bar_lens");

        // ---------------------------------------------------------------- making it

        /// <summary>A tow truck, switched off, under <paramref name="parent"/>. Null if the modelled truck cannot be had.</summary>
        public static TowTruck Make(Transform parent, string name)
        {
            if (!Available)
            {
                return null;
            }
            var root = new GameObject(name);
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            int layer = LayerMask.NameToLayer("Vehicles");
            if (layer >= 0)
            {
                root.layer = layer;
            }
            var truck = root.AddComponent<TowTruck>();
            try
            {
                truck.Build();
            }
            catch
            {
                UnityEngine.Object.Destroy(root);   // a truck half built is not left under the service for the next tow to build another beside
                throw;
            }
            return truck;
        }

        private void Build()
        {
            for (int i = 0; i < _lights.Length; i++)
            {
                _lights[i] = new List<Renderer>(2);
            }
            _body = gameObject.AddComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.useGravity = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _solid = gameObject.AddComponent<BoxCollider>();
            _solid.center = new Vector3(0f, SolidFrom + (Roof - SolidFrom) * 0.5f, (TowRules.Nose + Tail) * 0.5f);
            _solid.size = new Vector3(HalfWidth * 2f, Roof - SolidFrom, Length);

            _shell = new GameObject("Shell").transform;
            _shell.SetParent(transform, false);
            Shapes.Prop("tow_truck", _shell);
            // The subframe turns about its pivot; the bed slides along it. Both are modelled where they lie at rest.
            _tilt = new GameObject("Tilt").transform;
            _tilt.SetParent(_shell, false);
            _tilt.localPosition = TiltPivot;
            PropAt("tow_truck_tilt", _tilt, -TiltPivot);
            _slide = new GameObject("Bed").transform;
            _slide.SetParent(_tilt, false);
            _slide.localPosition = -TiltPivot;
            Shapes.Prop("tow_truck_bed", _slide);
            Material steel = Shapes.Paint(new Color(0.16f, 0.16f, 0.17f));
            _ramBarrel = Shapes.Piece("Ram", _shell, Shapes.Drum, steel, Vector3.zero, Vector3.one);
            _ramRod = Shapes.Piece("Ram Rod", _shell, Shapes.Drum, Shapes.Paint(new Color(0.70f, 0.71f, 0.73f)), Vector3.zero, Vector3.one);
            _cable = Shapes.Piece("Cable", _shell, Shapes.Cube, Shapes.Paint(new Color(0.08f, 0.08f, 0.09f)), Vector3.zero, Vector3.one);
            _cable.gameObject.SetActive(false);
            Wheels();
            Lamps();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is MeshRenderer && (renderer.name == "tow_truck" || renderer.name == "tow_truck_bed"))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                }
            }
            for (int i = 0; i < _lit.Length; i++)
            {
                _lit[i] = true;
                Show((Light)i, false);
            }
            Deck(0f);
        }

        private static void PropAt(string id, Transform parent, Vector3 at)
        {
            if (PropLibrary.TryGet(id, out Mesh mesh, out Material material) && mesh != null)
            {
                Shapes.Piece(id, parent, mesh, material, at, Vector3.one);
            }
        }

        // The city bus's wheels, drawn at the truck's size: each on a pivot that spins it, the front two on one more that
        // steers them; the left ones turned round so that their hubs face out.
        private void Wheels()
        {
            PropLibrary.TryGet("city_bus_wheel", out Mesh wheel, out Material paint);
            bool pair = PropLibrary.TryGet("city_bus_wheel_twin", out Mesh twin, out Material twinPaint) && twin != null;
            float scale = WheelRadius / BusWheel;
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                bool left = (i & 1) == 0;
                float x = (front ? TrackFront : TrackRear) * (left ? -1f : 1f);
                var at = new Vector3(x, WheelRadius, front ? AxleFront : AxleRear);
                Transform hang = transform;
                if (front)
                {
                    _steer[i] = new GameObject(left ? "Steer Left" : "Steer Right").transform;
                    _steer[i].SetParent(transform, false);
                    _steer[i].localPosition = at;
                    hang = _steer[i];
                    at = Vector3.zero;
                }
                _spin[i] = new GameObject("Wheel").transform;
                _spin[i].SetParent(hang, false);
                _spin[i].localPosition = at;
                var face = new GameObject("Face").transform;
                face.SetParent(_spin[i], false);
                face.localRotation = Quaternion.Euler(0f, left ? 180f : 0f, 0f);
                bool own = !front && pair;
                Transform piece = Shapes.Piece("Wheel", face, own ? twin : wheel, own ? twinPaint : paint, Vector3.zero,
                    new Vector3(front || own ? scale : scale * 0.56f / 0.30f, scale, scale));
                piece.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
            }
        }

        // The lamps lit, each group a mesh of the glow material over its lamps in the body; the light bar's two lit halves,
        // and the two amber lights over it, switched on only while it is at work.
        private void Lamps()
        {
            Lamp(Light.Head, "tow_truck_light_head");
            Lamp(Light.Tail, "tow_truck_light_tail");
            Lamp(Light.Brake, "tow_truck_light_brake");
            Lamp(Light.Left, "tow_truck_light_left");
            Lamp(Light.Right, "tow_truck_light_right");
            if (PropLibrary.TryGet("tow_bar_lens", out Mesh lens, out Material glow) && lens != null)
            {
                Transform a = Shapes.Piece("tow_bar_lens", _shell, lens, glow, BarAt, Vector3.one);
                Transform b = Shapes.Piece("tow_bar_lens", _shell, lens, glow, BarAt, Vector3.one);
                b.localRotation = Quaternion.Euler(0f, 180f, 0f);
                _lights[(int)Light.BarA].Add(a.GetComponent<Renderer>());
                _lights[(int)Light.BarB].Add(b.GetComponent<Renderer>());
            }
            _bar = TrafficEffects.CreateFlasher(_shell, "RealLifeOverhaul Tow Lights", BarAt + Vector3.up * 0.45f, Amber, Amber, BarEvery, 7000f, 22f);
            _bar.SetActive(false);
        }

        private void Lamp(Light which, string model)
        {
            if (PropLibrary.TryGet(model, out Mesh mesh, out Material paint) && mesh != null)
            {
                _lights[(int)which].Add(Shapes.Piece(model, _shell, mesh, paint, Vector3.zero, Vector3.one).GetComponent<Renderer>());
            }
        }

        // ---------------------------------------------------------------- on the street

        /// <summary>Puts it on the street with its front axle at <paramref name="frontAxle"/> facing along <paramref name="heading"/>, its bed at rest.</summary>
        public void Place(Vector3 frontAxle, Vector3 heading)
        {
            heading.y = 0f;
            heading = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector3.forward;
            _braking = false;
            _lean = _leanSpeed = _dip = _dipSpeed = 0f;
            _steered = 0f;
            Beacon(false);
            Hazards(false);
            Cable(false, Vector3.zero);
            Deck(0f);
            Vector3 rear = frontAxle - heading * Wheelbase;
            Quaternion facing = Quaternion.LookRotation(heading, Vector3.up);
            transform.SetPositionAndRotation((frontAxle + rear) * 0.5f, facing);
            gameObject.SetActive(true);
            _body.position = transform.position;
            _body.rotation = facing;
            ShowLamps();
        }

        /// <summary>Takes it off the street, telling the traffic first that it is gone from in front of whoever was waiting for it.</summary>
        public void Hide()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }
            Beacon(false);
            if (TrafficManager.HasInstance && TrafficManager.IsInitialized && _solid != null)
            {
                Manager.TriggerColliderRemovedEvent(_solid);
            }
            gameObject.SetActive(false);
        }

        /// <summary>BusVehicle.Pose: the body to its axles, the wheels rolled and steered, the springs, the brake lamps.</summary>
        public void Pose(Vector3 frontAxle, Vector3 rearAxle, float rolled, float steer, float acceleration, float turning, float speed, float dt)
        {
            Vector3 along = frontAxle - rearAxle;
            if (along.sqrMagnitude < 0.01f)
            {
                return;
            }
            Quaternion facing = Quaternion.LookRotation(along.normalized, Vector3.up);
            _body.MovePosition((frontAxle + rearAxle) * 0.5f);
            _body.MoveRotation(facing);
            _spun = Mathf.Repeat(_spun + rolled / WheelRadius * Mathf.Rad2Deg, 360f);
            _steered = Mathf.MoveTowards(_steered, Mathf.Clamp(steer, -38f, 38f), 90f * dt);
            float sideways = speed * turning * Mathf.Deg2Rad;
            Spring(ref _lean, ref _leanSpeed, Mathf.Clamp(sideways * 0.7f, -2.5f, 2.5f), dt);
            Spring(ref _dip, ref _dipSpeed, Mathf.Clamp(-acceleration * 0.6f, -1.5f, 1.5f), dt);
            _braking = acceleration < -0.3f || speed < 0.1f;
            _posedAt = Time.time;
        }

        private static void Spring(ref float value, ref float rate, float target, float dt)
        {
            const float stiffness = 140f;
            float damping = 2f * Mathf.Sqrt(stiffness);
            rate += ((target - value) * stiffness - rate * damping) * dt;
            value += rate * dt;
        }

        /// <summary>Its light bar flashing (and the amber lights over it), or not.</summary>
        public void Beacon(bool on)
        {
            _beacon = on;
            if (_bar != null && _bar.activeSelf != on)
            {
                _bar.SetActive(on);
            }
        }

        /// <summary>Both indicators blinking together, or not.</summary>
        public void Hazards(bool on)
        {
            _hazards = on;
        }

        /// <summary>
        /// The bed to its pose t, 0 at rest and 1 down (TowRules.DeckPose): the subframe turned about its pivot, the bed slid
        /// back along it, the ram between the chassis and the subframe stretched to reach.
        /// </summary>
        public void Deck(float t)
        {
            _deck = Mathf.Clamp01(t);
            TowRules.DeckPose(_deck, out float tilt, out float slide);
            _tilt.localRotation = Quaternion.Euler(-tilt, 0f, 0f);   // nose up: Unity turns +Z towards +Y the negative way about X
            _slide.localPosition = new Vector3(0f, 0f, -slide) - TiltPivot;
            // The ram: a barrel from its foot towards the subframe, the rod out of it to the head.
            Vector3 head = TiltPivot + _tilt.localRotation * (RamHead - TiltPivot);
            Vector3 way = head - RamFoot;
            float length = way.magnitude;
            Quaternion along = Quaternion.FromToRotation(Vector3.up, way / Mathf.Max(0.01f, length));
            _ramBarrel.localPosition = RamFoot;
            _ramBarrel.localRotation = along;
            _ramBarrel.localScale = new Vector3(0.11f, Mathf.Min(1.0f, length * 0.7f), 0.11f);
            _ramRod.localPosition = RamFoot + way * 0.5f;
            _ramRod.localRotation = along;
            _ramRod.localScale = new Vector3(0.05f, length * 0.5f, 0.05f);
        }

        /// <summary>Where the winch's cable leaves the drum, in the world, with the bed as it is now.</summary>
        public Vector3 Winch => _slide.TransformPoint(WinchAt);

        /// <summary>The cable out from the winch to a point in the world, or wound in.</summary>
        public void Cable(bool shown, Vector3 to)
        {
            if (_cable == null)
            {
                return;
            }
            if (!shown)
            {
                if (_cable.gameObject.activeSelf)
                {
                    _cable.gameObject.SetActive(false);
                }
                return;
            }
            Vector3 from = Winch;
            Vector3 way = to - from;
            float length = way.magnitude;
            if (length < 0.05f)
            {
                _cable.gameObject.SetActive(false);
                return;
            }
            if (!_cable.gameObject.activeSelf)
            {
                _cable.gameObject.SetActive(true);
            }
            _cable.SetPositionAndRotation((from + to) * 0.5f, Quaternion.LookRotation(way / length, Vector3.up));
            Vector3 lossy = _shell.lossyScale;
            _cable.localScale = new Vector3(CableThick / Mathf.Max(0.01f, lossy.x), CableThick / Mathf.Max(0.01f, lossy.y), length / Mathf.Max(0.01f, lossy.z));
        }

        private static readonly int HotUpdate = HotSpots.Slot("Traffic: TowTruck.Update");
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
            if (Time.time - _posedAt > 0.1f)
            {
                // Standing (nobody poses it): the springs settle it level, so that its bed is the truck's own at work.
                float dt = Mathf.Min(Time.deltaTime, 0.05f);   // the springs are stepped as in the physics step
                Spring(ref _lean, ref _leanSpeed, 0f, dt);
                Spring(ref _dip, ref _dipSpeed, 0f, dt);
                _braking = true;
            }
            for (int i = 0; i < 4; i++)
            {
                _spin[i].localRotation = Quaternion.Euler(_spun, 0f, 0f);
            }
            for (int i = 0; i < 2; i++)
            {
                _steer[i].localRotation = Quaternion.Euler(0f, _steered, 0f);
            }
            _shell.localRotation = Quaternion.Euler(_dip, 0f, _lean);
            ShowLamps();
        }

        private void ShowLamps()
        {
            bool night = Night;
            bool blink = Mathf.Repeat(Time.time, BlinkEvery) < BlinkEvery * 0.5f;
            bool first = Mathf.FloorToInt(Time.unscaledTime / BarEvery) % 2 == 0;   // the clock the amber lights flip on (TrafficEffects' Flasher)
            Show(Light.Head, night);
            Show(Light.Tail, night);
            Show(Light.Brake, _braking);
            Show(Light.Left, _hazards && blink);
            Show(Light.Right, _hazards && blink);
            Show(Light.BarA, _beacon && first);
            Show(Light.BarB, _beacon && !first);
        }

        private void Show(Light which, bool on)
        {
            if (_lit[(int)which] == on)
            {
                return;
            }
            _lit[(int)which] = on;
            List<Renderer> lamps = _lights[(int)which];
            for (int i = 0; i < lamps.Count; i++)
            {
                if (lamps[i] != null)
                {
                    lamps[i].enabled = on;
                }
            }
        }

        // Dusk to dawn, as the street lights and the bus.
        private static bool Night
        {
            get
            {
                int hour = TimeHelper.CurrentHour;
                return hour >= 19 || hour < 7;
            }
        }

        /// <summary>What it fills on the road: its middle, the way it faces, half its length and half its width.</summary>
        public void Footprint(out Vector3 centre, out Vector3 forward, out float halfLength, out float halfWidth)
        {
            forward = transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            centre = transform.position + forward * ((TowRules.Nose + Tail) * 0.5f);
            halfLength = Length * 0.5f;
            halfWidth = HalfWidth;
        }
    }
}
