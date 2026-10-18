using System.Collections.Generic;
using GleyTrafficSystem;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The city bus the mod runs on its lines, a vehicle of its own: a low-floor midi bus 10.9 m by 2.40 m (a full 2.55 m
    /// would not stand clear of the lane in the city's 2.6 m parking strips), with two doors on its kerb side, a lit sign and
    /// working lights; the modelled body where the prop bundle has it (city_bus, props_service.py), else plain shapes of the
    /// same size. It is solid to everyone: one kinematic box on the 'Vehicles' layer, which the traffic's sensors stop for and
    /// the game's free-spot searches count (not the traffic cars' own layer, which VehicleStoppedReversing would take for one
    /// of its own, nor 'Player'); traffic is told before it goes (Manager.TriggerColliderRemovedEvent). <see cref="BusDriver"/>
    /// moves it; here the wheels roll and steer, the body leans and dips, the doors slide and the lamps light. The modelled
    /// body has an inside (<see cref="BusInside"/>) and its roof apart, which <see cref="ShowRoof"/> stops drawing while the
    /// camera is over a rider. Cost a frame while out: the doors, wheels, body and lamps posed; switched off in between.
    /// </summary>
    public sealed class BusVehicle : MonoBehaviour, IDrivenVehicle
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("transport", "BusVehicle", typeof(BusVehicle));

        // Its measurements in metres: the bays are laid out for them (BusBay), the modelled body is built to them.
        public const float Length = BusBay.BusLength;
        public const float HalfWidth = BusBay.BusSideHalf;
        public const float Height = 3.05f;
        public const float AxleFront = BusBay.BusAxleFront;           // from its middle, along
        public const float AxleRear = BusBay.BusAxleFront - BusBay.BusWheelbase;
        public const float Wheelbase = BusBay.BusWheelbase;
        public const float NoseAhead = BusBay.BusNoseAhead;           // the nose, ahead of the front axle
        public const float TailBehind = BusBay.BusTailBehind;         // the tail, behind the front axle
        public const float WheelRadius = 0.48f;
        public const float FrontDoorAhead = 4.45f;    // the front door's middle, ahead of the middle
        public const float MiddleDoorAhead = -0.55f;  // and the middle door's
        public const float DoorOut = 0.30f;           // where somebody stands to step in, out from the side (board_step)
        public const float Floor = 0.33f;             // the floor at the doors, over the road

        private const float SolidFrom = 0.30f;        // the box starts this far over the road: kerbs and humps pass under it
        private const float TrackFront = 1.02f;       // half the track, to the middle of each wheel
        private const float TrackRear = 0.92f;        // to the middle of the twin tyres
        private const float TyreFront = 0.30f;
        private const float TyreRear = 0.56f;
        private const float DoorLeaf = 0.60f;         // each door is two leaves of this
        private const float DoorTall = 2.30f;
        private const float DoorSlide = 0.56f;        // a leaf opens out by DoorOutward and along by this
        private const float DoorOutward = 0.07f;
        private const float DoorSeconds = 1.3f;       // seconds to open or to shut
        private const float BlinkEvery = 0.68f;       // an indicator's cycle, seconds

        private enum Light { Head, Tail, Brake, Left, Right, Sign, Count }

        private static readonly Color Livery = new Color(0.86f, 0.10f, 0.12f);    // the modelled body's red (paint_service.py, BUS_RED)
        private static readonly Color Skirt = new Color(0.18f, 0.18f, 0.20f);
        private static readonly Color Glass = new Color(0.07f, 0.09f, 0.11f);
        private static readonly Color Tyre = new Color(0.05f, 0.05f, 0.05f);
        private static readonly Color Hub = new Color(0.55f, 0.56f, 0.58f);
        private static readonly Color Amber = new Color(1f, 0.62f, 0.08f);

        private Rigidbody _body;
        private BoxCollider _solid;
        private Transform _shell;                 // what leans and dips: the body and what hangs on it
        private readonly Transform[] _steer = new Transform[2];
        private readonly Transform[] _spin = new Transform[4];
        private readonly Transform[] _leaves = new Transform[4];   // front door left, right; middle door left, right (as seen from outside)
        private readonly Vector3[] _leafShut = new Vector3[4];
        private readonly float[] _leafSlide = new float[4];
        private readonly List<Renderer>[] _lights = new List<Renderer>[(int)Light.Count];
        private readonly bool[] _lit = new bool[(int)Light.Count];
        private TextMeshPro _signFront;
        private TextMeshPro _signSide;
        private TextMeshPro _signBack;
        private float _doors;                     // 0 shut, 1 open
        private bool _doorsWanted;
        private int _indicate;                    // -1 left, 0 none, 1 right
        private bool _braking;
        private float _spun;
        private float _steered;
        private float _lean;
        private float _leanSpeed;
        private float _dip;
        private float _dipSpeed;
        private MeshRenderer _roof;
        private bool _roofShown = true;

        /// <summary>True when the body is the modelled bus (a step at its doors: <c>board_step</c>), false for plain shapes.</summary>
        public bool Modelled { get; private set; }

        /// <summary>True when the modelled body has its inside and its roof apart, so somebody can be seen riding in it.</summary>
        public bool HasInside { get; private set; }

        /// <summary>What leans and dips on the springs; the inside is on it, so whoever rides is placed by it.</summary>
        public Transform Shell => _shell;

        /// <summary>True while it is on the street.</summary>
        public bool Out => gameObject.activeSelf;

        public Rigidbody Body => _body;

        public Transform Frame => transform;
        float IDrivenVehicle.FrontAxleAhead => AxleFront;
        float IDrivenVehicle.RearAxleAhead => AxleRear;
        float IDrivenVehicle.AxleSpan => Wheelbase;
        float IDrivenVehicle.NoseBeyondAxle => NoseAhead;
        float IDrivenVehicle.HalfBreadth => HalfWidth;
        public Collider Solid => _solid;

        /// <summary>A bus, switched off, under <paramref name="parent"/>. Null if the game has no shader to draw it with.</summary>
        public static BusVehicle Make(Transform parent, string name)
        {
            if (!Shapes.Available)
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
            var bus = root.AddComponent<BusVehicle>();
            bus.Build();
            return bus;
        }

        private void Build()
        {
            for (int i = 0; i < _lights.Length; i++)
            {
                _lights[i] = new List<Renderer>(4);
            }
            _body = gameObject.AddComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.useGravity = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _solid = gameObject.AddComponent<BoxCollider>();
            _solid.center = new Vector3(0f, SolidFrom + (Height - SolidFrom) * 0.5f, 0f);
            _solid.size = new Vector3(HalfWidth * 2f, Height - SolidFrom, Length);

            _shell = new GameObject("Shell").transform;
            _shell.SetParent(transform, false);
            Modelled = Shapes.Prop("city_bus", _shell);
            // The body with its inside comes with its roof apart; one is never used without the other.
            HasInside = Modelled && PropLibrary.Has("city_bus_roof") && Shapes.Prop("city_bus_roof", _shell);
            if (!Modelled)
            {
                PlainBody();
            }
            Wheels();
            Doors();
            Lights();
            Signs();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is MeshRenderer && (renderer.name == "city_bus" || renderer.name == "city_bus_roof"))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    if (renderer.name == "city_bus_roof")
                    {
                        _roof = (MeshRenderer)renderer;
                    }
                }
            }
            for (int i = 0; i < _lit.Length; i++)
            {
                _lit[i] = true;
                Show((Light)i, false);
            }
        }

        // The body in plain shapes, to the model's measurements; the wheel arches are left to the wheels.
        private void PlainBody()
        {
            Material red = Shapes.Paint(Livery);
            Material glass = Shapes.Paint(Glass);
            Material skirt = Shapes.Paint(Skirt);
            Shapes.Piece("Skirt", _shell, Shapes.Cube, skirt, new Vector3(0f, 0.55f, 0f), new Vector3(HalfWidth * 2f, 0.50f, Length));
            Shapes.Piece("Panels", _shell, Shapes.Cube, red, new Vector3(0f, 1.05f, 0f), new Vector3(HalfWidth * 2f + 0.01f, 0.55f, Length - 0.02f));
            Shapes.Piece("Windows", _shell, Shapes.Cube, glass, new Vector3(0f, 1.95f, -0.15f), new Vector3(HalfWidth * 2f - 0.04f, 1.25f, Length - 0.5f));
            Shapes.Piece("Windscreen", _shell, Shapes.Cube, glass, new Vector3(0f, 1.80f, Length * 0.5f - 0.04f), new Vector3(HalfWidth * 2f - 0.16f, 1.55f, 0.08f));
            Shapes.Piece("Roof", _shell, Shapes.Cube, red, new Vector3(0f, 2.80f, 0f), new Vector3(HalfWidth * 2f, 0.50f, Length));
            Shapes.Piece("Sign Box", _shell, Shapes.Cube, Shapes.Paint(new Color(0.02f, 0.02f, 0.02f)), new Vector3(0f, 2.78f, Length * 0.5f - 0.02f), new Vector3(1.70f, 0.30f, 0.06f));
            Shapes.Piece("Bumper", _shell, Shapes.Cube, skirt, new Vector3(0f, 0.45f, Length * 0.5f + 0.02f), new Vector3(HalfWidth * 2f - 0.05f, 0.30f, 0.10f));
            Shapes.Piece("Rear Bumper", _shell, Shapes.Cube, skirt, new Vector3(0f, 0.45f, -Length * 0.5f - 0.02f), new Vector3(HalfWidth * 2f - 0.05f, 0.30f, 0.10f));
        }

        // Four wheels, each on a pivot that spins it, the front two on one more that steers them. The modelled wheels have the
        // hub to +X; the rear pair is its own mesh where the bundle has it, else the front wheel drawn out to the pair's width.
        // The left ones are turned round so their hubs face out.
        private void Wheels()
        {
            bool modelled = PropLibrary.TryGet("city_bus_wheel", out Mesh wheel, out Material paint) && wheel != null;
            Mesh twin = null;
            Material twinPaint = null;
            bool pair = modelled && PropLibrary.TryGet("city_bus_wheel_twin", out twin, out twinPaint) && twin != null;
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
                if (modelled)
                {
                    bool own = !front && pair;
                    Transform piece = Shapes.Piece(front ? "city_bus_wheel" : "city_bus_wheel_twin", face, own ? twin : wheel, own ? twinPaint : paint, Vector3.zero,
                        new Vector3(front || own ? 1f : TyreRear / TyreFront, 1f, 1f));
                    piece.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
                    continue;
                }
                // A drum stands on its origin along its own Y: laid on its side across the bus.
                float wide = front ? TyreFront : TyreRear;
                Transform tyre = Shapes.Piece("Tyre", face, Shapes.Drum, Shapes.Paint(Tyre), new Vector3(wide * 0.5f, 0f, 0f), new Vector3(WheelRadius * 2f, wide, WheelRadius * 2f));
                tyre.localRotation = Quaternion.Euler(0f, 0f, 90f);
                Transform hub = Shapes.Piece("Hub", face, Shapes.Drum, Shapes.Paint(Hub), new Vector3(wide * 0.5f + 0.01f, 0f, 0f), new Vector3(WheelRadius * 1.1f, 0.02f, WheelRadius * 1.1f));
                hub.localRotation = Quaternion.Euler(0f, 0f, 90f);
                Shapes.Piece("Spoke", face, Shapes.Cube, Shapes.Paint(Tyre), new Vector3(wide * 0.5f + 0.02f, 0f, 0f), new Vector3(0.01f, WheelRadius * 1.0f, 0.08f));   // to see it turn
            }
        }

        // Two doors on the kerb side (+X), each two leaves that slide out and along, away from the door's middle.
        private void Doors()
        {
            bool modelled = PropLibrary.TryGet("city_bus_door", out Mesh leaf, out Material paint) && leaf != null;
            Material glass = Shapes.Paint(Glass);
            for (int i = 0; i < 4; i++)
            {
                float middle = i < 2 ? FrontDoorAhead : MiddleDoorAhead;
                float side = (i & 1) == 0 ? 1f : -1f;   // the leaf ahead of the door's middle, then the one behind it
                var shut = new Vector3(HalfWidth + 0.03f, Floor, middle + side * DoorLeaf * 0.5f);   // proud of the side: it slides over it
                _leaves[i] = new GameObject("Door Leaf").transform;
                _leaves[i].SetParent(_shell, false);
                _leaves[i].localPosition = shut;
                _leafShut[i] = shut;
                // The front door's forward leaf has only the metre to the nose: it stops just short of it.
                float room = i == 0 ? Length * 0.5f - 0.02f - (middle + DoorLeaf) : DoorSlide;
                _leafSlide[i] = side * Mathf.Clamp01(room / DoorSlide);
                if (modelled)
                {
                    Shapes.Piece("city_bus_door", _leaves[i], leaf, paint, Vector3.zero, Vector3.one);
                }
                else
                {
                    Shapes.Piece("Leaf", _leaves[i], Shapes.Cube, glass, new Vector3(0f, DoorTall * 0.5f, 0f), new Vector3(0.04f, DoorTall, DoorLeaf - 0.02f));
                }
            }
        }

        // The lamps: the modelled ones where the bundle has them, else small glowing shapes in the same places and sizes
        // (props_service.py bus_lamps).
        private void Lights()
        {
            float nose = Length * 0.5f + 0.03f;           // the shapes stand proud of the plain body's ends
            float tail = -Length * 0.5f - 0.03f;
            float repeater = AxleFront + 0.8f;
            var cornerLamp = new Vector3(0.11f, 0.15f, 0.03f);
            var frontTurn = new Vector3(0.08f, 0.15f, 0.03f);
            var backTurn = new Vector3(0.11f, 0.12f, 0.03f);
            var sideTurn = new Vector3(0.02f, 0.034f, 0.10f);
            Lamp(Light.Head, "city_bus_light_head", new Color(1f, 0.95f, 0.85f), 3f, new[] { new Vector3(-0.61f, 0.74f, nose), new Vector3(0.61f, 0.74f, nose) },
                new[] { new Vector3(0.21f, 0.12f, 0.03f) });
            Lamp(Light.Tail, "city_bus_light_tail", new Color(0.7f, 0.04f, 0.03f), 1.2f, new[] { new Vector3(-0.90f, 1.00f, tail), new Vector3(0.90f, 1.00f, tail) }, new[] { cornerLamp });
            Lamp(Light.Brake, "city_bus_light_brake", new Color(1f, 0.05f, 0.04f), 4f, new[] { new Vector3(-0.90f, 1.16f, tail), new Vector3(0.90f, 1.16f, tail), new Vector3(0f, 2.81f, tail) },
                new[] { cornerLamp, cornerLamp, new Vector3(0.36f, 0.035f, 0.03f) });
            Lamp(Light.Left, "city_bus_light_left", Amber, 4f, new[] { new Vector3(-0.77f, 0.74f, nose), new Vector3(-0.90f, 1.31f, tail), new Vector3(-HalfWidth - 0.01f, 0.80f, repeater) },
                new[] { frontTurn, backTurn, sideTurn });
            Lamp(Light.Right, "city_bus_light_right", Amber, 4f, new[] { new Vector3(0.77f, 0.74f, nose), new Vector3(0.90f, 1.31f, tail), new Vector3(HalfWidth + 0.01f, 0.80f, repeater) },
                new[] { frontTurn, backTurn, sideTurn });
        }

        // One size for all of a group's lamps, or one for each.
        private void Lamp(Light which, string model, Color colour, float glow, Vector3[] places, Vector3[] sizes)
        {
            List<Renderer> lamps = _lights[(int)which];
            if (PropLibrary.TryGet(model, out Mesh mesh, out Material paint) && mesh != null)
            {
                lamps.Add(Shapes.Piece(model, _shell, mesh, paint, Vector3.zero, Vector3.one).GetComponent<Renderer>());
                return;
            }
            Material lit = Shapes.Paint(colour, glow);
            for (int i = 0; i < places.Length; i++)
            {
                lamps.Add(Shapes.Piece(which + " Lamp", _shell, Shapes.Cube, lit, places[i], sizes[Mathf.Min(i, sizes.Length - 1)]).GetComponent<Renderer>());
            }
        }

        // The line and where it goes over the windscreen, the number over the front door and at the back: amber lettering,
        // whose shader is not lit by the scene, so it shows by day and night alike.
        private void Signs()
        {
            _signFront = Letter("Sign", new Vector3(0f, 2.78f, Length * 0.5f + 0.015f), Quaternion.Euler(0f, 180f, 0f), 1.62f, 0.24f);
            _signSide = Letter("Side Sign", new Vector3(HalfWidth + 0.012f, 2.55f, FrontDoorAhead - 1.2f), Quaternion.Euler(0f, -90f, 0f), 1.1f, 0.2f);
            _signBack = Letter("Back Sign", new Vector3(0f, 2.62f, -Length * 0.5f - 0.015f), Quaternion.identity, 0.45f, 0.2f);
        }

        private TextMeshPro Letter(string name, Vector3 at, Quaternion facing, float width, float height)
        {
            TMP_FontAsset font = GameUi.GameFont.Asset;
            if (font == null)
            {
                return null;
            }
            var label = new GameObject(name);
            label.transform.SetParent(_shell, false);
            label.transform.localPosition = at;
            label.transform.localRotation = facing;
            var text = label.AddComponent<TextMeshPro>();
            text.font = font;
            text.color = Amber;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.overflowMode = TextOverflowModes.Truncate;
            text.rectTransform.sizeDelta = new Vector2(width, height);
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.4f;
            text.fontSizeMax = 2f;
            var renderer = label.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return text;
        }

        /// <summary>Puts it on the street with its front axle at <paramref name="frontAxle"/> facing along <paramref name="heading"/>, signed for a line.</summary>
        public void Place(Vector3 frontAxle, Vector3 heading, TransitLine line)
        {
            heading.y = 0f;
            heading = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector3.forward;
            if (_signFront != null && line != null)
            {
                _signFront.text = line.Number + "  " + line.To;
                _signSide.text = line.Number + "  " + line.To;
                _signBack.text = line.Number;
            }
            _doors = 0f;
            _doorsWanted = false;
            _indicate = 0;
            _braking = false;
            _lean = _leanSpeed = _dip = _dipSpeed = 0f;
            _steered = 0f;
            ApplyDoors();
            Vector3 rear = frontAxle - heading * Wheelbase;
            Quaternion facing = Quaternion.LookRotation(heading, Vector3.up);
            transform.SetPositionAndRotation((frontAxle + rear) * 0.5f, facing);
            gameObject.SetActive(true);
            _body.position = transform.position;
            _body.rotation = facing;
            Lamps();
        }

        /// <summary>Draws its roof, or only its shadow, for the view into the bus while the player rides. Only a change touches the renderer.</summary>
        public void ShowRoof(bool shown)
        {
            if (_roof == null || shown == _roofShown)
            {
                return;
            }
            _roofShown = shown;
            _roof.shadowCastingMode = shown ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
        }

        /// <summary>Where a place on its floor is in the world, at a height over the floor, as the body leans and dips now.</summary>
        public Vector3 OnFloor(BusInside.Spot spot, float up = 0f)
        {
            return _shell.TransformPoint(new Vector3(spot.X, BusInside.Floor + up, spot.Z));
        }

        /// <summary>The world's rotation for a facing on its floor (degrees about the vertical, 0 to its nose, 90 to its doors).</summary>
        public Quaternion Facing(float facing)
        {
            return _shell.rotation * Quaternion.Euler(0f, facing, 0f);
        }

        /// <summary>Takes it off the street, telling the traffic first that it is gone from in front of whoever was waiting for it.</summary>
        public void Hide()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }
            ShowRoof(true);
            if (TrafficManager.HasInstance && TrafficManager.IsInitialized && _solid != null)
            {
                Manager.TriggerColliderRemovedEvent(_solid);
            }
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Places it as the driver has moved it (<see cref="IDrivenVehicle.Pose"/>); acceleration is negative while slowing.
        /// Called from the physics step.
        /// </summary>
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
            // The body on its springs: out of a bend by the pull sideways (turning right it rolls to its left, a positive turn
            // about its own forward), nose down as it brakes.
            float sideways = speed * turning * Mathf.Deg2Rad;
            Spring(ref _lean, ref _leanSpeed, Mathf.Clamp(sideways * 0.9f, -3f, 3f), dt);
            Spring(ref _dip, ref _dipSpeed, Mathf.Clamp(-acceleration * 0.8f, -2f, 2f), dt);
            _braking = acceleration < -0.3f || speed < 0.1f;
        }

        // A spring that settles without swinging past (critically damped) in about a third of a second.
        private static void Spring(ref float value, ref float rate, float target, float dt)
        {
            const float stiffness = 140f;
            float damping = 2f * Mathf.Sqrt(stiffness);
            rate += ((target - value) * stiffness - rate * damping) * dt;
            value += rate * dt;
        }

        /// <summary>Opens or shuts its doors (they take a little over a second).</summary>
        public void Doors(bool open)
        {
            _doorsWanted = open;
        }

        /// <summary>True while its doors are wholly open.</summary>
        public bool DoorsOpen => _doors >= 0.999f;

        /// <summary>True once its doors are wholly shut and stay so: it may pull away.</summary>
        public bool DoorsShut => !_doorsWanted && _doors <= 0.001f;

        /// <summary>The indicator: -1 left, 1 right, 0 none.</summary>
        public void Indicate(int side)
        {
            _indicate = side;
        }

        private static readonly int HotUpdate = HotSpots.Slot("Transport: BusVehicle.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults
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
            float dt = Time.deltaTime;
            float doors = Mathf.MoveTowards(_doors, _doorsWanted ? 1f : 0f, dt / DoorSeconds);
            if (doors != _doors)
            {
                _doors = doors;
                ApplyDoors();
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
            Lamps();
        }

        // A leaf goes out first, then along: the plug door of a city bus.
        private void ApplyDoors()
        {
            float outward = Mathf.Clamp01(_doors * 3f);
            float slide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_doors - 0.25f) / 0.75f));
            for (int i = 0; i < 4; i++)
            {
                if (_leaves[i] != null)
                {
                    _leaves[i].localPosition = _leafShut[i] + new Vector3(outward * DoorOutward, 0f, _leafSlide[i] * slide * DoorSlide);
                }
            }
        }

        private void Lamps()
        {
            bool night = Night;
            bool blink = Mathf.Repeat(Time.time, BlinkEvery) < BlinkEvery * 0.5f;
            Show(Light.Head, night);
            Show(Light.Tail, night);
            Show(Light.Brake, _braking);
            Show(Light.Left, _indicate < 0 && blink);
            Show(Light.Right, _indicate > 0 && blink);
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

        // Dusk to dawn, as the street lights.
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
            centre = transform.position;
            forward = transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            halfLength = Length * 0.5f;
            halfWidth = HalfWidth;
        }
    }
}
