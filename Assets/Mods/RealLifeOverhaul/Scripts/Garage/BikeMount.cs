using System;
using System.Collections.Generic;
using System.Globalization;
using BAModAPI;
using GleyTrafficSystem;
using Helpers;
using NWH.Common.Vehicles;
using NWH.VehiclePhysics2.Modules.SpeedLimiter;
using NWH.VehiclePhysics2.Powertrain;
using NWH.VehiclePhysics2.Sound.SoundComponents;
using UnityEngine;
using UnityEngine.AI;
using LeanModule = NWH.VehiclePhysics2.Modules.MotorcycleModule.MotorcycleModule;
using LeanWrapper = NWH.VehiclePhysics2.Modules.MotorcycleModule.MotorcycleModuleWrapper;
using NwhVehicle = NWH.VehiclePhysics2.VehicleController;

namespace RealLifeOverhaul
{
    /// <summary>
    /// One of the game's electric scooters made a bicycle or a motorbike. What makes it rideable stays the game's (its
    /// controller, two-wheel physics, getting on and off, the hands, the camera, the save); this gives it another body
    /// (the scooter's renderers are not drawn, never removed), its own name and numbers, the boxes the world meets
    /// (<see cref="BikeRules.Frame"/>, <see cref="BikeRules.Bars"/>) and a rider who sits it. A motorbike runs on the
    /// cars' model; a bicycle's drivetrain is let go and the rider pushes it (<see cref="BikeRules.Pedal"/>). Every value
    /// changed is recorded and <see cref="Detach"/> puts the scooter back exactly. No cost while parked; while ridden, a
    /// force a physics step and a few comparisons and two leg turns a frame, nothing allocated.
    /// </summary>
    [DefaultExecutionOrder(5000)]   // after the game has posed the rider, before the mod redraws the player (Character/PlayerDouble)
    public sealed partial class BikeMount : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("garage", "BikeMount", typeof(BikeMount));   // its LateUpdate and FixedUpdate

        private const float CadencePerMetre = 1.41f;   // radians of crank per metre: 75 turns a minute at 20 km/h
        private const float AnkleAbovePedal = 0.10f;   // metres
        private const float BicycleRolling = 6f;       // N m per wheel: a bicycle coasts; the scooter's own tyres stop it in a few metres
        private const float LegsFade = 0.3f;           // seconds for the feet to leave the pedals for the clip's own legs, and back
        private const float CrankHome = 5f;            // radians a second: the cranks brought round to "right pedal forward" when stopped
        private const float SteerEase = 0.06f;         // seconds: the bars follow the physics' steering between its steps, not in steps
        private const string BarsName = "RealLifeOverhaul Bars";

        public static IModLogger Logger;

        private static readonly CarSetup Setup = new CarSetup();
        private static readonly HashSet<int> Logged = new HashSet<int>();
        private static AnimationCurve _leanMostCurve, _leanRateCurve;   // shared by every two-wheeler

        private ScooterController _scooter;
        private NwhVehicle _nwh;
        private Rigidbody _rigidbody;
        private BikeBody _body;
        private readonly List<Renderer> _hidden = new List<Renderer>(8);
        private PointOfInterest _poi;   // the game's own vehicle marker, which it does not give a scooter

        // what the scooter was like
        private bool _changed;
        private float _power, _final, _brake, _limit;
        private float[] _gears;
        private float[] _rolling;
        private SpeedLimiterModule _limiter;
        private SoundComponent[] _sounds;
        private float[] _volumes;
        private float _noteRange;
        private AutoDestroyVehicle _autoDestroy;
        private bool _autoDestroyWas;
        private bool _canRunWas;
        private ClutchComponent.ClutchControlType _clutchTypeWas;
        private float _clutchInputWas;
        private bool _drivetrainLetGo;
        private LeanModule _lean;
        private AnimationCurve _leanMostWas, _leanRateWas;
        private string _headerWas;
        private bool _headerSet;
        private BoxCollider _frame;
        private Vector3 _frameCentreWas, _frameSizeWas;
        private BoxCollider _bars;
        private NavMeshObstacle _obstacle;
        private Vector3 _obstacleCentreWas, _obstacleSizeWas;
        private bool _massKept;
        private Vector3 _massCentre, _inertia;          // as the game set them (VariableCenterOfMass)
        private Quaternion _inertiaTurn;
        private Action<VehicleController> _onExit;      // on GlobalEvents.onExitVehicle, made once
        private bool _exitHooked;

        // while ridden
        private bool _ridden;
        private float _drag;
        private float _effort = 1f;
        private float _spin, _crank, _steerShown;
        private Vector3 _lastVelocity;
        private Vector3 _lastPosition;
        private Animator _animator;
        private Transform _player;
        private Transform _head;
        private Transform _thighL, _shinL, _footL, _thighR, _shinR, _footR;
        private GameObject _helmet;
        private Quaternion _headSquare = Quaternion.identity;   // from the Head bone's axes to the character's (helmet: visor +Z, crown +Y)
        private bool _headKnown;
        private WheelUAPI _front;
        private ActivityAnimator _seated;   // the mod's riding clip on the rider, when the clip bundle has one
        private AnimationClip _rideClip, _coastClip, _standClip;   // coast and stand may be missing
        private BikeRules.Pose _riding;
        private float _otherFor;            // seconds the rider has been doing other than the clip shows
        private float _poseAge;             // seconds since the clip last changed
        private float _legs = 1f;           // a cyclist's legs: 1 on the pedals, 0 the clip's (a foot on the ground)

        public BikeRecord Record { get; private set; }

        public BikeDef Def { get; private set; }

        public ScooterController Scooter => _scooter;

        public bool Ridden => _ridden;

        /// <summary>The speed (m/s) the rider came off at; TwoWheelers deals with it and clears it.</summary>
        public float Fell { get; set; }

        /// <summary>Signed speed along the bike, m/s.</summary>
        public float Speed => _nwh != null ? _nwh.Speed : 0f;

        public bool Pedalling => _ridden && _nwh != null && _nwh.input != null && _nwh.input.Throttle > 0.1f && Mathf.Abs(_nwh.Speed) > 0.5f;

        /// <summary>How hard the rider can push: 1 fresh, 0.5 worn out. Bicycles only.</summary>
        public float Effort
        {
            get => _effort;
            set => _effort = value;
        }

        public static BikeMount Attach(ScooterController scooter, BikeRecord record)
        {
            if (scooter == null || record == null || scooter.GetComponent<BikeMount>() != null)
            {
                return null;
            }
            var mount = scooter.gameObject.AddComponent<BikeMount>();
            mount.enabled = false;
            mount._scooter = scooter;
            mount.Record = record;
            mount.Def = BikeCatalogue.Of(record.Kind);
            try
            {
                mount.Dress();
                mount.Tune();
                mount.KeepMass();
                mount.Shape();
                mount.PutMassBack();
                mount.Name();
            }
            catch (Exception e)
            {
                Logger?.Warn("Two-wheelers: a " + record.Kind + " could not be made of this scooter; it stays a scooter. " + e);
                mount.Detach();
                return null;
            }
            mount.Mark();
            return mount;
        }

        private void Dress()
        {
            // The scooter's renderers are only not drawn: the game still asks them things. Taken before our body exists:
            // its own list, and every mesh under it the list leaves out (a wheel, the bag it carries).
            Hide(_scooter.Renderers);
            GameInstance save = SaveGameManager.Current;
            bool ridden = _scooter.controlledByPlayer || (save != null && _scooter.vehicleInstance != null && save.ActiveVehicleId == _scooter.vehicleInstance.id);
            if (!ridden)   // a save made in the saddle: the rider is among its children
            {
                Hide(_scooter.GetComponentsInChildren<MeshRenderer>(true));
                Hide(_scooter.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            }

            _body = BikeBody.Build(Def, transform, Record.Colour);
            if (_body == null)
            {
                throw new InvalidOperationException("the game's lit shader was not found");
            }
            // Whatever the scooter does to its children, ours are drawn.
            Renderer[] ours = _body.Root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < ours.Length; i++)
            {
                ours[i].forceRenderingOff = false;
                ours[i].enabled = true;
            }
        }

        private void Hide(Renderer[] own)
        {
            for (int i = 0; own != null && i < own.Length; i++)
            {
                if (own[i] != null && !own[i].forceRenderingOff)
                {
                    own[i].forceRenderingOff = true;
                    _hidden.Add(own[i]);
                }
            }
        }

        // The scooter's own box (its vehicleCollider, which stations, garages, barriers and parking look for) becomes the
        // frame's, wheel to wheel; the bars and the rider's knees are a box of ours, on while ridden. The walkers' obstacle
        // is made the frame's length, so that nobody walks into the wheels of one standing.
        private void Shape()
        {
            BikeRules.Box frame = BikeRules.Frame(Def.RearZ, Def.FrontZ, Def.WheelRadius, Def.Seat.x, Def.Motor);
            _frame = _scooter.vehicleCollider as BoxCollider;
            if (_frame != null)
            {
                Transform space = _frame.transform;   // the game's box sits at the scooter's origin, turned as it is
                _frameCentreWas = _frame.center;
                _frameSizeWas = _frame.size;
                _frame.center = space.InverseTransformPoint(transform.TransformPoint(new Vector3(frame.X, frame.Y, frame.Z)));
                _frame.size = SizeIn(space, new Vector3(frame.Width, frame.Height, frame.Length));
            }
            else
            {
                _frame = null;
                Logger?.Warn("Two-wheelers: the scooter's own box is not what was expected; a " + Def.Kind + " meets the world with the scooter's.");
            }

            BikeRules.Box bars = BikeRules.Bars(Def.Grip.x, Def.Grip.y, Def.Grip.z, Def.Seat.y);
            var holder = new GameObject(BarsName);
            holder.transform.SetParent(_body.Root.transform, false);
            holder.layer = LayerHelper.VehiclesLayerIndex;
            _bars = holder.AddComponent<BoxCollider>();
            _bars.isTrigger = false;
            _bars.center = new Vector3(bars.X, bars.Y, bars.Z);
            _bars.size = new Vector3(bars.Width, bars.Height, bars.Length);
            _bars.enabled = false;   // on while ridden

            _obstacle = _scooter.GetComponentInChildren<NavMeshObstacle>(true);
            if (_obstacle != null && _obstacle.shape == NavMeshObstacleShape.Box)
            {
                Transform space = _obstacle.transform;
                _obstacleCentreWas = _obstacle.center;
                _obstacleSizeWas = _obstacle.size;
                _obstacle.center = space.InverseTransformPoint(transform.TransformPoint(new Vector3(0f, frame.Y, frame.Z)));
                Vector3 was = SizeIn(space, _obstacleSizeWas, true);   // the game's, in the bike's axes
                _obstacle.size = SizeIn(space, new Vector3(Mathf.Max(was.x, frame.Width), Mathf.Max(was.y, frame.Height), frame.Length));
            }
            else
            {
                _obstacle = null;
            }
        }

        // A size along the bike's axes as the size along those of the transform it is set on, however that is turned and
        // scaled; back: the other way.
        private Vector3 SizeIn(Transform space, Vector3 size, bool back = false)
        {
            Quaternion turn = Quaternion.Inverse(space.rotation) * transform.rotation;   // the bike's axes in the space's
            if (back)
            {
                turn = Quaternion.Inverse(turn);
            }
            Matrix4x4 m = Matrix4x4.Rotate(turn);
            BikeRules.SizeAlong(m.m00, m.m01, m.m02, m.m10, m.m11, m.m12, m.m20, m.m21, m.m22, size.x, size.y, size.z, out float x, out float y, out float z);
            Vector3 scale = space.lossyScale, own = transform.lossyScale;
            if (back)
            {
                return new Vector3(x * Mathf.Abs(scale.x) / Mathf.Max(1e-4f, Mathf.Abs(own.x)), y * Mathf.Abs(scale.y) / Mathf.Max(1e-4f, Mathf.Abs(own.y)), z * Mathf.Abs(scale.z) / Mathf.Max(1e-4f, Mathf.Abs(own.z)));
            }
            return new Vector3(x * Mathf.Abs(own.x) / Mathf.Max(1e-4f, Mathf.Abs(scale.x)), y * Mathf.Abs(own.y) / Mathf.Max(1e-4f, Mathf.Abs(scale.y)), z * Mathf.Abs(own.z) / Mathf.Max(1e-4f, Mathf.Abs(scale.z)));
        }

        // The centre of mass and inertia are the game's (VariableCenterOfMass writes them as the game lets the scooter go),
        // and a collider changed on the body must not move them: kept before the boxes change, written back after. Only
        // from a body free to turn: a standing bike is frozen with an inertia of (0, 0, 0), which written back as it is got
        // on the engine takes as infinite (the front wheel steers, the bike goes straight on). So it is kept when made, if
        // free then, and each time it is got on.
        private void KeepMass()
        {
            if (_rigidbody == null)
            {
                return;
            }
            Vector3 inertia = _rigidbody.inertiaTensor;
            if (!BikeRules.KeepMass(TurnFree, inertia.x, inertia.y, inertia.z))
            {
                return;   // what was kept before stays
            }
            _massCentre = _rigidbody.centerOfMass;
            _inertia = inertia;
            _inertiaTurn = _rigidbody.inertiaTensorRotation;
            _massKept = true;
        }

        // Never into a frozen body: the game gives it its own values as it lets it go, and a write would undo its hold.
        private void PutMassBack()
        {
            if (_rigidbody == null || !BikeRules.PutMassBack(_massKept, TurnFree))
            {
                return;
            }
            _rigidbody.centerOfMass = _massCentre;
            _rigidbody.inertiaTensor = _inertia;
            _rigidbody.inertiaTensorRotation = _inertiaTurn;
        }

        // The game's hold on a scooter nobody is on freezes every constraint.
        private bool TurnFree => _rigidbody != null && (_rigidbody.constraints & RigidbodyConstraints.FreezeRotation) == 0;

        // Getting on's self-check: a fault always in the log, a pass only with Developer diagnostics.
        private void TurnCheck()
        {
            if (_rigidbody == null)
            {
                return;
            }
            Vector3 inertia = _rigidbody.inertiaTensor;
            bool free = TurnFree;
            bool turns = BikeRules.Turns(inertia.x, inertia.y, inertia.z);
            if (free && turns && !Meta.Dev.Verbose)
            {
                return;
            }
            string line = "Two-wheelers: turning check (got on): a " + Def.Kind + ", "
                          + (free ? "free to turn" : "its turning frozen (" + _rigidbody.constraints + ")")
                          + ", inertia " + inertia.ToString("F1", CultureInfo.InvariantCulture) + " kg m2 against at least "
                          + BikeRules.LeastInertia.ToString("F3", CultureInfo.InvariantCulture) + " on each axis ("
                          + (_massKept ? "the game's kept" : "none of the game's kept") + ")";
            if (free && turns)
            {
                Logger?.Info(line + ": PASS");
            }
            else
            {
                Logger?.Warn(line + ": FAIL (its front wheel steers and it goes straight on)");
            }
        }

        // Solid, on the layer the game gives a ridden scooter's box.
        private void BarsOn()
        {
            if (_bars == null)
            {
                return;
            }
            _bars.gameObject.layer = LayerHelper.VehiclesLayerIndex;
            _bars.enabled = true;
            PutMassBack();
        }

        /// <summary>
        /// The bars box off, traffic told first as the game tells it of a collider it takes away: the engine does not, and
        /// a car that had sensed the box would wait for it for ever. Safe to ask twice.
        /// </summary>
        public void BarsOff()
        {
            if (_bars == null || !_bars.enabled)
            {
                return;
            }
            Manager.TriggerColliderRemovedEvent(_bars);
            _bars.enabled = false;
            PutMassBack();
        }

        // From inside the game's ExitVehicle: the box is off before the rest of the exit runs.
        private void Exited(VehicleController vehicle)
        {
            if (ReferenceEquals(vehicle, _scooter))
            {
                BarsOff();
            }
        }

        private void HookExit(bool on)
        {
            _onExit = _onExit ?? Guard.Wrap<VehicleController>(Exited);
            GlobalEvents.onExitVehicle = (Action<VehicleController>)Delegate.Remove(GlobalEvents.onExitVehicle, _onExit);
            if (on)
            {
                GlobalEvents.onExitVehicle = (Action<VehicleController>)Delegate.Combine(_onExit, GlobalEvents.onExitVehicle);
            }
            _exitHooked = on;
        }

        /// <summary>The last thing <see cref="Pinned"/> found a bike's box in, for the log; null if none yet.</summary>
        public static Collider PinnedBy { get; private set; }

        /// <summary>
        /// True if this bike, stood here so, would have its frame or bars box (less <see cref="BikeClearRules.Skin"/>) in
        /// something on these layers, not counting itself or <paramref name="also"/> (the player just got off). Two box
        /// queries: asked on getting off and on, never per frame.
        /// </summary>
        public bool Pinned(Vector3 position, Quaternion rotation, int mask, Transform also)
        {
            BoxCollider frame = _frame != null ? _frame : _scooter != null ? _scooter.vehicleCollider as BoxCollider : null;
            return Boxed(frame, position, rotation, mask, also) || Boxed(_bars, position, rotation, mask, also);
        }

        private static readonly Collider[] Touching = new Collider[24];   // the queries run one at a time, on the main thread

        // One box of the bike, moved with it to the place asked about.
        private bool Boxed(BoxCollider box, Vector3 position, Quaternion rotation, int mask, Transform also)
        {
            if (box == null || mask == 0)
            {
                return false;
            }
            Transform space = box.transform;
            Vector3 centre = Vector3.Scale(transform.InverseTransformPoint(space.TransformPoint(box.center)), transform.lossyScale);   // from the bike's origin, in its axes
            Quaternion turn = rotation * (Quaternion.Inverse(transform.rotation) * space.rotation);
            Vector3 scale = space.lossyScale;
            var half = new Vector3(BikeClearRules.Inner(Mathf.Abs(box.size.x * scale.x) * 0.5f), BikeClearRules.Inner(Mathf.Abs(box.size.y * scale.y) * 0.5f),
                BikeClearRules.Inner(Mathf.Abs(box.size.z * scale.z) * 0.5f));
            int count = Physics.OverlapBoxNonAlloc(position + rotation * centre, half, Touching, turn, mask, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Collider other = Touching[i];
                Touching[i] = null;
                if (found || other == null || other.transform.IsChildOf(transform) || (also != null && other.transform.IsChildOf(also)))
                {
                    continue;
                }
                PinnedBy = other;
                found = true;
            }
            return found;
        }

        // The scooter destroyed, or the scene going, with the box on: traffic is told.
        private void OnDestroy()
        {
            try
            {
                BarsOff();
                if (_exitHooked)
                {
                    HookExit(false);
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Two-wheelers: the bars box could not be let go of on the way out. " + e.Message);
            }
        }

        // The game's per-object header, as it names a chauffeur's car; the ride's panel and messages are BikeNames'.
        private void Name()
        {
            _headerWas = _scooter.customOverlayHeaderKey;
            _scooter.customOverlayHeaderKey = Def.Name;
            _headerSet = true;
        }

        // The game marks the player's cars on the map and the street but not scooters (hire scooters to it): ours get
        // that marker, under their own name.
        private void Mark()
        {
            try
            {
                if (_scooter.poi != null)
                {
                    return;
                }
                var city = InstanceBehavior<CityManager>.Instance;
                var references = InstanceBehavior<GlobalReferences>.Instance;
                if (city == null || city.cityMap == null || references == null)
                {
                    return;
                }
                PointOfInterest poi = city.cityMap.AddPoi(transform, references.vehiclePOIIcon, references.vehiclePOIBackgroundColor, GarageText.T(Def.Name));
                if (poi == null)
                {
                    return;
                }
                poi.offset = new Vector3(0f, 0.3f, 0f);   // what the game gives a scooter's marker
                poi.SetPermanent();
                if (_scooter.controlledByPlayer)
                {
                    poi.SetHidden(true);   // as the game hides a vehicle's marker while it is driven
                }
                _scooter.poi = poi;
                _poi = poi;
            }
            catch (Exception e)
            {
                Logger?.Warn("Two-wheelers: the map marker of a " + Def.Kind + " could not be made (it is still there, unmarked). " + e.Message);
            }
        }

        public bool Marked => _poi != null;

        /// <summary>
        /// Where this two-wheeler is and whether it can be seen, as one log line. Builds a string: called when one is made
        /// and once shortly after.
        /// </summary>
        public string Report()
        {
            Vector3 at = transform.position;
            bool ground = BikePlaces.StandHeight(at, out float height, out bool pavement);
            int drawn = 0, total = 0, forcedOff = 0, noMaterial = 0;
            var bounds = new Bounds(at, Vector3.zero);
            bool any = false;
            if (_body != null && _body.Root != null)
            {
                Renderer[] ours = _body.Root.GetComponentsInChildren<Renderer>(true);
                total = ours.Length;
                for (int i = 0; i < ours.Length; i++)
                {
                    Renderer renderer = ours[i];
                    if (renderer.forceRenderingOff)
                    {
                        forcedOff++;
                    }
                    if (renderer.sharedMaterial == null)
                    {
                        noMaterial++;
                    }
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff)
                    {
                        drawn++;
                        if (any)
                        {
                            bounds.Encapsulate(renderer.bounds);
                        }
                        else
                        {
                            bounds = renderer.bounds;
                            any = true;
                        }
                    }
                }
            }
            int stillDrawn = 0;
            Renderer[] all = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if ((all[i] is MeshRenderer || all[i] is SkinnedMeshRenderer) && !all[i].forceRenderingOff && all[i].enabled
                    && (_body == null || _body.Root == null || !all[i].transform.IsChildOf(_body.Root.transform)))
                {
                    stillDrawn++;
                }
            }
            Collider collider = _scooter.vehicleCollider;
            Vector3 scale = transform.lossyScale;
            return $"{Def.Kind} at {at.ToString("F2", CultureInfo.InvariantCulture)}, heading {transform.eulerAngles.y:F0}, {Vector3.Angle(transform.up, Vector3.up):F0} deg off upright; "
                   + (ground ? FormattableString.Invariant(
                       $"ground under it at {height:F2} ({(pavement ? "pavement" : "not pavement")}), so it stands {at.y - height:F2} above it; ") : "NO GROUND found under it; ")
                   + $"body: {(_body == null ? "none" : _body.Modelled ? "modelled" : "stand-in")}, {drawn} of {total} renderers drawn, {forcedOff} forced off, {noMaterial} without a material, "
                   + (any ? $"bounds centre {bounds.center.ToString("F2", CultureInfo.InvariantCulture)} size {bounds.size.ToString("F2", CultureInfo.InvariantCulture)}; " : "no bounds; ")
                   + $"scooter's own: {_hidden.Count} hidden, {stillDrawn} still drawn; object {(gameObject.activeInHierarchy ? "active" : "INACTIVE")}, layer {LayerMask.LayerToName(gameObject.layer)}, "
                   + $"scale {scale.ToString("F2", CultureInfo.InvariantCulture)}, collider {(collider != null ? "on " + LayerMask.LayerToName(collider.gameObject.layer) + (_frame != null ? " " + _frame.size.ToString("F2", CultureInfo.InvariantCulture) + " turned " + _frame.transform.localEulerAngles.ToString("F0") : " (the scooter's)") : "none")}"
                   + $", bars {(_bars != null ? (_bars.enabled ? "on " : "off ") + LayerMask.LayerToName(_bars.gameObject.layer) : "none")}, "
                   + (_rigidbody != null ? $"body {(_rigidbody.isKinematic ? "kinematic" : "dynamic")} {_rigidbody.constraints}, " : "no rigidbody, ")
                   + (_rigidbody != null ? "inertia " + _rigidbody.inertiaTensor.ToString("F1", CultureInfo.InvariantCulture)
                                           + (_massKept ? " (the game's kept), " : " (none kept), ") : string.Empty)
                   + $"physics {(_nwh != null && _nwh.enabled ? "running" : "at rest")}, map marker {(_poi != null ? "yes" : "no")}.";
        }

        private void Tune()
        {
            _nwh = _scooter.GetComponent<NwhVehicle>();
            _rigidbody = _nwh != null ? _nwh.vehicleRigidbody : null;
            if (_nwh == null || _rigidbody == null || _nwh.powertrain == null || _nwh.powertrain.engine == null || _nwh.powertrain.clutch == null
                || _nwh.powertrain.transmission == null || _nwh.powertrain.wheels == null || _nwh.powertrain.wheels.Count < 2 || _nwh.brakes == null)
            {
                throw new InvalidOperationException("the scooter's drivetrain is not what was expected");
            }
            EngineComponent engine = _nwh.powertrain.engine;
            ClutchComponent clutch = _nwh.powertrain.clutch;
            TransmissionComponent box = _nwh.powertrain.transmission;
            var limiter = _scooter.GetComponent<SpeedLimiterModuleWrapper>();
            var lean = _scooter.GetComponent<LeanWrapper>();

            _power = engine.maxPower;
            _canRunWas = engine.canRun;
            _clutchTypeWas = clutch.controlType;
            _clutchInputWas = clutch.clutchInput;
            _final = box.finalGearRatio;
            _gears = box.gears.ToArray();
            _brake = _nwh.brakes.maxTorque;
            _limiter = limiter != null ? limiter.module : null;
            _limit = _limiter != null ? _limiter.speedLimit : 0f;
            _lean = lean != null ? lean.module : null;
            _leanMostWas = _lean != null ? _lean.maxLeanAngle : null;
            _leanRateWas = _lean != null ? _lean.leanAngleMaxDelta : null;
            _rolling = new float[_nwh.powertrain.wheels.Count];
            float wheelBase = 0f;
            for (int i = 0; i < _rolling.Length; i++)
            {
                WheelUAPI wheel = _nwh.powertrain.wheels[i].wheelUAPI;
                _rolling[i] = wheel != null ? wheel.RollingResistanceTorque : 0f;
                if (wheel != null)
                {
                    float z = transform.InverseTransformPoint(wheel.transform.position).z;
                    wheelBase = Mathf.Max(wheelBase, Mathf.Abs(z));
                    if (_front == null || z > transform.InverseTransformPoint(_front.transform.position).z)
                    {
                        _front = wheel;
                    }
                }
            }
            _autoDestroy = _scooter.GetComponent<AutoDestroyVehicle>();
            _autoDestroyWas = _autoDestroy != null && _autoDestroy.enabled;
            _changed = true;

            // The game takes a scooter away four hours after it was last looked at; a hired one is collected by TwoWheelers.
            if (_autoDestroy != null)
            {
                _autoDestroy.enabled = false;
            }

            // Leaning with the speed: the scooter leans fully from 1 m/s and swings there fast.
            if (_lean != null)
            {
                _lean.maxLeanAngle = _leanMostCurve ??= LeanCurve(BikeRules.LeanMost);
                _lean.leanAngleMaxDelta = _leanRateCurve ??= LeanCurve(BikeRules.LeanRate);
            }

            string line;
            if (Def.Bicycle)
            {
                var sound = _nwh.soundManager;
                if (sound != null)
                {
                    _sounds = new SoundComponent[]
                    {
                        sound.engineRunningComponent, sound.engineStartStopComponent, sound.engineFanComponent, sound.exhaustPopComponent,
                        sound.gearChangeComponent, sound.transmissionWhineComponent
                    };
                    _volumes = new float[_sounds.Length];
                    for (int i = 0; i < _sounds.Length; i++)
                    {
                        if (_sounds[i] != null)
                        {
                            _volumes[i] = _sounds[i].baseVolume;
                            _sounds[i].baseVolume = 0f;   // nobody's bicycle hums
                            if (_sounds[i].source != null)
                            {
                                _sounds[i].source.volume = 0f;
                            }
                        }
                    }
                    _noteRange = sound.engineRunningComponent.volumeRange;
                    sound.engineRunningComponent.volumeRange = 0f;
                }
                // The drivetrain let go: the engine off, the clutch out, a final drive so small that the engine and gearbox
                // no longer weigh on the rear wheel. The rider pushes it instead (OnFixedUpdate).
                engine.canRun = false;
                engine.StopEngine();
                clutch.controlType = ClutchComponent.ClutchControlType.Manual;
                clutch.clutchInput = 0f;
                box.finalGearRatio = BikeRules.FreeWheelRatio;
                _drivetrainLetGo = true;
                float rolling = 0f;
                for (int i = 0; i < _rolling.Length; i++)
                {
                    WheelUAPI wheel = _nwh.powertrain.wheels[i].wheelUAPI;
                    if (wheel != null && wheel.Radius > 0.05f)
                    {
                        wheel.RollingResistanceTorque = BicycleRolling;
                        rolling += BicycleRolling / wheel.Radius;
                    }
                }
                _drag = BikeRules.Drag(Def.Push, _rigidbody.mass, rolling);
                line = FormattableString.Invariant(
                    $"Two-wheelers: {Def.Kind}: pedalled ({Def.Push.PowerPerKg * _rigidbody.mass / 1000f:F1} kW of push, {Def.Push.StartPush:F1} m/s2 at most), cruising {Def.CruiseKmh:F0} km/h ")
                       + FormattableString.Invariant($"(drag {_drag:F2}, rolling {rolling:F0} N), {_rigidbody.mass:F0} kg; the scooter's engine off, clutch out, final drive {_final:F2} -> {BikeRules.FreeWheelRatio:F2}; engine sounds off.");
            }
            else if (GarageTuning.SolveBike(_nwh, Def.Seconds, Def.TopKmh, Setup, out CarRun run, out int firstForward, out string why)
                     && Setup.PowerKw >= 5f && Setup.PowerKw <= _rigidbody.mass)
            {
                float final = Setup.Ratios[Setup.Ratios.Length - 1] / box.gears[box.gears.Count - 1];
                if (!(final > 0.2f) || !(final < 60f) || Setup.Ratios.Length != box.gears.Count - firstForward)
                {
                    throw new InvalidOperationException("the sums came out at a final drive of " + final);
                }
                engine.maxPower = Setup.PowerKw;
                engine.UpdatePeakPowerAndTorque();
                for (int i = 0; i < box.gears.Count; i++)
                {
                    box.gears[i] = i >= firstForward ? Setup.Ratios[i - firstForward] / final : _gears[i] * _final / final;
                }
                box.finalGearRatio = final;
                _drag = Setup.Drag;
                if (_limiter != null)
                {
                    _limiter.speedLimit = 0f;   // the limiter's own "no limit": drag ends the speed
                }
                _nwh.brakes.maxTorque = _brake * 1.6f;   // it has three times the scooter's speed to lose
                line = FormattableString.Invariant($"Two-wheelers: {Def.Kind}: target 0-100 {Def.Seconds:F1} s, top {Def.TopKmh:F0} km/h; power {_power:F0} -> {Setup.PowerKw:F0} kW, ")
                       + FormattableString.Invariant($"final drive {_final:F2} -> {final:F2}, drag {_drag:F3}, limiter {_limit:F0} -> off, brakes x1.6; ")
                       + FormattableString.Invariant($"expect 0-50 {run.To50:F1} s, 0-100 {run.To100:F1} s, top {run.TopKmh:F0} km/h") + (Setup.TyreLimited ? " (the tyre cannot put down what the time needs)" : string.Empty) + ".";
            }
            else
            {
                line = "Two-wheelers: " + Def.Kind + " keeps the scooter's own performance (the model cannot drive it).";
            }
            line += _lean != null ? $" Lean: {BikeRules.LeanMostDeg:F0} degrees from {BikeRules.FullLeanAt * 3.6f:F0} km/h." : " Lean: the scooter has no lean module; as the game has it.";
            if (Mathf.Abs(wheelBase - BikeCatalogue.ScooterFrontZ) > 0.12f)
            {
                line += " The scooter's wheels are not where the bodies were made for (front at " + wheelBase.ToString("F2", CultureInfo.InvariantCulture) + " m).";
            }
            if (Logged.Add((int)Def.Kind))
            {
                Logger?.Info(line);
            }
        }

        // A lean rule as the game's module reads it (degrees against m/s), straight between the keys as the rule is.
        private static AnimationCurve LeanCurve(Func<float, float> rule)
        {
            var curve = new AnimationCurve(new Keyframe(0f, rule(0f)), new Keyframe(BikeRules.FullLeanAt, rule(BikeRules.FullLeanAt)), new Keyframe(90f, rule(90f)));
            for (int i = 0; i < curve.length; i++)
            {
                Linear(curve, i);
            }
            return curve;
        }

        private static void Linear(AnimationCurve curve, int i)
        {
            Keyframe key = curve[i];
            float inSlope = i > 0 ? (key.value - curve[i - 1].value) / (key.time - curve[i - 1].time) : 0f;
            float outSlope = i < curve.length - 1 ? (curve[i + 1].value - key.value) / (curve[i + 1].time - key.time) : 0f;
            key.inTangent = inSlope;
            key.outTangent = outSlope;
            curve.MoveKey(i, key);
        }

        /// <summary>The scooter exactly as the game made it, and this component gone.</summary>
        public void Detach()
        {
            if (_ridden)
            {
                Dismounted();
            }
            BarsOff();
            // First and each on its own, so that nothing further down can leave them changed.
            try
            {
                if (_frame != null)
                {
                    _frame.center = _frameCentreWas;
                    _frame.size = _frameSizeWas;
                    _frame = null;
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Two-wheelers: the scooter's box could not be put back. " + e.Message);
            }
            try
            {
                if (_obstacle != null)
                {
                    _obstacle.center = _obstacleCentreWas;
                    _obstacle.size = _obstacleSizeWas;
                    _obstacle = null;
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Two-wheelers: the scooter's obstacle could not be put back. " + e.Message);
            }
            try
            {
                if (_headerSet && _scooter != null)
                {
                    _scooter.customOverlayHeaderKey = _headerWas;
                    _headerSet = false;
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Two-wheelers: the scooter's name could not be put back. " + e.Message);
            }
            try
            {
                PutMassBack();
                _massKept = false;
            }
            catch (Exception e)
            {
                Logger?.Warn("Two-wheelers: the scooter's mass could not be put back. " + e.Message);
            }
            try
            {
                for (int i = 0; i < _hidden.Count; i++)
                {
                    if (_hidden[i] != null)
                    {
                        _hidden[i].forceRenderingOff = false;
                    }
                }
                _hidden.Clear();
                if (!ReferenceEquals(_poi, null))
                {
                    if (_scooter != null && ReferenceEquals(_scooter.poi, _poi))
                    {
                        _scooter.poi = null;
                    }
                    MapPlaces.Drop(_poi);   // out of the game's list of markers, then destroyed
                    _poi = null;
                }
                _bars = null;   // on our body, which goes next
                if (_body != null && _body.Root != null)
                {
                    Destroy(_body.Root);
                }
                _body = null;
                if (_changed && _nwh != null && _nwh.powertrain != null)
                {
                    _changed = false;
                    EngineComponent engine = _nwh.powertrain.engine;
                    TransmissionComponent box = _nwh.powertrain.transmission;
                    engine.maxPower = _power;
                    engine.UpdatePeakPowerAndTorque();
                    if (_drivetrainLetGo)
                    {
                        _drivetrainLetGo = false;
                        engine.canRun = _canRunWas;
                        _nwh.powertrain.clutch.controlType = _clutchTypeWas;
                        _nwh.powertrain.clutch.clutchInput = _clutchInputWas;
                    }
                    box.finalGearRatio = _final;
                    for (int i = 0; i < _gears.Length && i < box.gears.Count; i++)
                    {
                        box.gears[i] = _gears[i];
                    }
                    _nwh.brakes.maxTorque = _brake;
                    if (_limiter != null)
                    {
                        _limiter.speedLimit = _limit;
                    }
                    if (_lean != null)
                    {
                        _lean.maxLeanAngle = _leanMostWas;
                        _lean.leanAngleMaxDelta = _leanRateWas;
                    }
                    for (int i = 0; i < _rolling.Length && i < _nwh.powertrain.wheels.Count; i++)
                    {
                        WheelUAPI wheel = _nwh.powertrain.wheels[i].wheelUAPI;
                        if (wheel != null)
                        {
                            wheel.RollingResistanceTorque = _rolling[i];
                        }
                    }
                    if (_sounds != null)
                    {
                        for (int i = 0; i < _sounds.Length; i++)
                        {
                            if (_sounds[i] != null)
                            {
                                _sounds[i].baseVolume = _volumes[i];
                            }
                        }
                        _nwh.soundManager.engineRunningComponent.volumeRange = _noteRange;
                        _sounds = null;
                    }
                    if (_autoDestroy != null)
                    {
                        _autoDestroy.enabled = _autoDestroyWas;
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Two-wheelers: a scooter could not be put back completely. " + e);
            }
            Destroy(this);
        }

        /// <summary>The player has just got on: the game has attached them and given them its riding pose.</summary>
        public void Mounted(PlayerController player)
        {
            if (_ridden || player == null || _body == null)
            {
                return;
            }
            _ridden = true;
            Fell = 0f;
            _player = player.transform;
            _lastVelocity = _rigidbody != null ? _rigidbody.velocity : Vector3.zero;
            _lastPosition = transform.position;
            _steerShown = _front != null ? _front.SteerAngle : 0f;
            KeepMass();       // the game has just let it go and written its own values
            BarsOn();
            TurnCheck();
            HookExit(true);
            _animator = PlayerHelper.GetAnimator();
            float hip = 0.95f;
            if (_animator != null && _animator.isHuman)
            {
                Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
                _head = _animator.GetBoneTransform(HumanBodyBones.Head);
                _thighL = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                _shinL = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                _footL = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                _thighR = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                _shinR = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                _footR = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
                if (hips != null)
                {
                    // The standing hip height the saddle is fitted to.
                    hip = Mathf.Clamp(Vector3.Dot(hips.position - _player.position, _player.up), 0.75f, 1.15f);
                }
            }
            // The mod's riding clips when the bundle has them (CityAssets/README.md, "Garage"): each model's own, else the
            // shared one of its kind; without them the game's own riding pose.
            _seated = null;
            _rideClip = Loop(Def.Ride, Def.Motor ? "moto_ride" : "cycle_ride");
            _coastClip = Def.Bicycle ? Loop(Def.Coast, "cycle_coast") : null;
            _standClip = Loop(Def.Stand, Def.Motor ? "moto_stand" : "cycle_stand");
            _riding = _standClip != null && _nwh != null && Mathf.Abs(_nwh.Speed) < BikeRules.StopBelow ? BikeRules.Pose.Stand : BikeRules.Pose.Ride;
            _otherFor = 0f;
            _poseAge = BikeRules.PoseFade;
            _legs = _riding == BikeRules.Pose.Stand ? 0f : 1f;
            if (_animator != null && _rideClip != null && ActivityAnimators.Working)
            {
                ActivityAnimator seated = ActivityAnimators.For(_animator);
                if (seated != null && seated.Usable && seated.Play(_riding == BikeRules.Pose.Stand ? _standClip : _rideClip, 0.25f))
                {
                    _seated = seated;
                }
            }
            if (_seated == null)
            {
                _riding = BikeRules.Pose.Ride;
                _legs = 1f;
            }
            // The game seats the rider at the scooter's origin, standing on its deck: moved over the seat, and in the
            // game's pose with the hip joint some 8 cm above it. A riding clip brings its own height.
            float lift = _seated != null ? 0f : Mathf.Clamp(Def.Seat.x + 0.08f - hip, -0.3f, 0.3f);
            _player.localPosition = new Vector3(0f, lift, Def.Seat.y + 0.04f);

            // The game's hand rig, aimed at our grips, which take the scooter's grip rotation the rig was set up for.
            if (_scooter.LHandIKAttachmentPoint != null && _scooter.RHandIKAttachmentPoint != null && player.Character != null)
            {
                _body.HandLeft.rotation = _scooter.LHandIKAttachmentPoint.rotation;
                _body.HandRight.rotation = _scooter.RHandIKAttachmentPoint.rotation;
                player.Character.SetHandIKTargets(_body.HandLeft, _body.HandRight);
            }
            if (Def.Motor && GarageState.Helmet && _head != null)
            {
                _helmet = BikeBody.Helmet(transform);
                _headKnown = _helmet != null && HeadSquare(_animator, _head, out _headSquare);
            }
            enabled = true;
        }

        /// <summary>The player is off (the game has already let them go and dropped its hand targets).</summary>
        public void Dismounted()
        {
            if (!_ridden)
            {
                return;
            }
            _ridden = false;
            enabled = false;
            BarsOff();
            if (_exitHooked)
            {
                HookExit(false);
            }
            if (_helmet != null)
            {
                Destroy(_helmet);
                _helmet = null;
            }
            if (_seated != null)
            {
                _seated.Stop(0.2f);
                _seated = null;
            }
            _rideClip = _coastClip = _standClip = null;
            _riding = BikeRules.Pose.Ride;
            _legs = 1f;
            _player = null;
            _animator = null;
            _head = _thighL = _shinL = _footL = _thighR = _shinR = _footR = null;
            if (Record != null)
            {
                Record.Position = transform.position;
                Record.Heading = transform.eulerAngles.y;
            }
        }

        private static readonly int HotFixedUpdate = HotSpots.Slot("Garage: BikeMount.FixedUpdate");
        private void FixedUpdate()
        {
            using var hotSpot = HotSpots.Time(HotFixedUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
                return;
            }
            try
            {
                OnFixedUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnFixedUpdate()
        {
            if (!_ridden || _rigidbody == null)
            {
                return;
            }
            Vector3 velocity = _rigidbody.velocity;
            float speed = velocity.magnitude;
            if (_drag > 0f && speed > 1f)
            {
                _rigidbody.AddForce(velocity * (-BikeRules.Air(_drag, speed) / speed));
            }
            // A bicycle is pushed by its rider; with the gearbox in reverse at a standstill, walked back.
            if (_drivetrainLetGo && _nwh != null && _nwh.input != null)
            {
                Vector3 ahead = transform.forward;
                float along = Vector3.Dot(velocity, ahead);
                float back = _nwh.powertrain.transmission.Gear < 0 ? _nwh.input.InputSwappedThrottle : 0f;
                float push = BikeRules.Pedal(Def.Push, _rigidbody.mass, along, _nwh.input.Throttle, _effort, back);
                if (push != 0f)
                {
                    _rigidbody.AddForce(ahead * push);
                }
            }
            // Coming off: most of the speed gone in one step, or on its side while moving; not when the game has just moved
            // the bike (a tow, a way in or out of a building).
            float was = _lastVelocity.magnitude;
            Vector3 position = _rigidbody.position;
            bool moved = (position - _lastPosition).sqrMagnitude > 2f * 2f;
            _lastPosition = position;
            if (moved)
            {
                _lastVelocity = velocity;
                return;
            }
            if (Fell <= 0f && was > 7f && (_lastVelocity - velocity).magnitude > 6f)
            {
                Fell = was;
            }
            else if (Fell <= 0f && speed > 4f && Vector3.Dot(transform.up, Vector3.up) < 0.3f)
            {
                Fell = speed;
            }
            _lastVelocity = velocity;
        }
    }
}
