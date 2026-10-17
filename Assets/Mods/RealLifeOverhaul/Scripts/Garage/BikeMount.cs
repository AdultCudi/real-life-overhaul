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
    /// One of the game's electric scooters turned into a bicycle or a motorbike. Everything that makes a
    /// scooter rideable stays the game's: its controller, its two-wheel physics (the lean, the steering),
    /// getting on and off, the hands on the bars, the camera, the save. This component gives it another
    /// body (the scooter's own renderers are switched to not draw, never removed), another name wherever the
    /// game names the thing pointed at (its own per-object header, as it names a chauffeur's car), other
    /// numbers, and a rider who sits it: a riding clip of the mod's own chosen by what they are doing
    /// (pedalling, freewheeling, stopped with a foot down; riding or stopped on a motorbike), or without the
    /// clip bundle the game's riding pose moved to the saddle; a bicycle's feet are put on the pedals after the
    /// animation.
    ///
    /// THE BODY THE WORLD MEETS (<see cref="BikeRules.Frame"/>, <see cref="BikeRules.Bars"/>). The scooter's own box
    /// (0.32 by 1.06 m, under its deck) is made the frame's, wheel to wheel, and a second box of the bars and the
    /// rider's knees is added while ridden, on the layer the game gives a ridden scooter (Vehicles: walls, poles,
    /// parked and moving cars, the player's own car). Its obstacle on the walkers' navigation is made as long.
    ///
    /// HOW IT GOES. A motorbike: power, gearing and drag from the same model the cars use. A bicycle: the scooter's
    /// drivetrain is let go and the rider pushes it (<see cref="BikeRules.Pedal"/>, once a physics step), against
    /// air drag that sets its cruising speed. Both lean with their speed (<see cref="BikeRules.LeanMost"/>).
    ///
    /// Every value changed is recorded and <see cref="Detach"/> puts the scooter back exactly.
    /// Costs nothing while parked: the per-frame and per-step work runs only while it is ridden (a force, a few
    /// comparisons and the legs' two bone turns each), and allocates nothing.
    /// </summary>
    [DefaultExecutionOrder(5000)]   // after the game has posed the rider, before the mod redraws the player (Character/PlayerDouble)
    public sealed class BikeMount : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("garage", "BikeMount", typeof(BikeMount));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        private const float CadencePerMetre = 1.41f;   // radians of crank per metre: 75 turns a minute at 20 km/h
        private const float AnkleAbovePedal = 0.10f;
        private const float BicycleRolling = 6f;       // N m per wheel: a bicycle coasts; the scooter's own tyres stop it in a few metres
        private const float LegsFade = 0.3f;           // seconds for the feet to leave the pedals for the clip's own legs, and back
        private const float CrankHome = 5f;            // radians a second: the cranks brought round to "right pedal forward" when stopped
        private const float SteerEase = 0.06f;         // seconds: the bars follow the physics' steering between its steps, not in steps
        private const string BarsName = "RealLifeOverhaul Bars";

        public static IModLogger Logger;

        private static readonly CarSetup Setup = new CarSetup();
        private static readonly HashSet<int> Logged = new HashSet<int>();
        private static AnimationCurve _leanMostCurve, _leanRateCurve;   // shared by every two-wheeler, made once

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
        private Vector3 _massCentre, _inertia;          // the body's centre of mass and inertia as the game set them (VariableCenterOfMass)
        private Quaternion _inertiaTurn;
        private Action<VehicleController> _onExit;      // the game's own getting off (GlobalEvents.onExitVehicle), made once
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
        private Quaternion _headSquare = Quaternion.identity;   // from the Head bone's own axes to the character's (helmet: visor +Z, crown +Y)
        private bool _headKnown;
        private WheelUAPI _front;
        private ActivityAnimator _seated;   // the mod's own riding clip on the rider, when the clip bundle has one
        private AnimationClip _rideClip, _coastClip, _standClip;   // by what the rider is doing; coast and stand may be missing
        private BikeRules.Pose _riding;
        private float _otherFor;            // seconds the rider has been doing something else than the clip shows
        private float _poseAge;             // seconds since the clip last changed: no change while one is still fading in
        private float _legs = 1f;           // how much of a cyclist's legs is the pedals' (1) and how much the clip's (0: a foot on the ground)

        public BikeRecord Record { get; private set; }

        public BikeDef Def { get; private set; }

        public ScooterController Scooter => _scooter;

        public bool Ridden => _ridden;

        /// <summary>Set when the rider has come off (the speed they were doing, m/s); the director deals with it and clears it.</summary>
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

        // ---------------------------------------------------------------- the body

        private void Dress()
        {
            // The scooter's own body: not drawn, and otherwise left as it is (the game still asks its renderers things).
            // Taken before our body exists, so nothing of ours can ever be among them: the renderers the scooter
            // itself lists, and every mesh under it that list leaves out (a wheel, the bag it carries).
            Hide(_scooter.Renderers);
            GameInstance save = SaveGameManager.Current;
            bool ridden = _scooter.controlledByPlayer || (save != null && _scooter.vehicleInstance != null && save.ActiveVehicleId == _scooter.vehicleInstance.id);
            if (!ridden)   // with somebody on it (a save made in the saddle) the rider is among its children: only the scooter's own list then
            {
                Hide(_scooter.GetComponentsInChildren<MeshRenderer>(true));
                Hide(_scooter.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            }

            _body = BikeBody.Build(Def, transform, Record.Colour);
            if (_body == null)
            {
                throw new InvalidOperationException("the game's lit shader was not found");
            }
            // And the other way round: whatever the scooter does to its children, ours are drawn.
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

        // What the world meets. The scooter's own box (the game's vehicleCollider: what its gas stations, garages, barriers
        // and parking look for) becomes the frame's, wheel to wheel; the bars and the rider's knees are a box of ours on the
        // same body, switched on while ridden (Mounted), on the layer the game gives a ridden scooter's box. The walkers'
        // obstacle is made the frame's length, so that nobody's path runs into the wheels of one standing.
        private void Shape()
        {
            BikeRules.Box frame = BikeRules.Frame(Def.RearZ, Def.FrontZ, Def.WheelRadius, Def.Seat.x, Def.Motor);
            _frame = _scooter.vehicleCollider as BoxCollider;
            if (_frame != null)
            {
                Transform space = _frame.transform;   // (the game's box sits at the scooter's origin, turned as it is)
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
                Vector3 was = SizeIn(space, _obstacleSizeWas, true);   // (the game's, in the bike's axes)
                _obstacle.size = SizeIn(space, new Vector3(Mathf.Max(was.x, frame.Width), Mathf.Max(was.y, frame.Height), frame.Length));
            }
            else
            {
                _obstacle = null;
            }
        }

        // A box's size given along the bike's own axes, as the size along the axes of the transform it is set on (whatever way
        // that is turned and scaled): each of its axes takes the parts of the bike's that lie along it. back: the other way.
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

        // The body's centre of mass and inertia are the game's (VariableCenterOfMass sets them); a collider changed on the body
        // must not move them. Read once before the boxes are touched, written back after each change to the boxes.
        private void KeepMass()
        {
            if (_rigidbody == null)
            {
                return;
            }
            _massCentre = _rigidbody.centerOfMass;
            _inertia = _rigidbody.inertiaTensor;
            _inertiaTurn = _rigidbody.inertiaTensorRotation;
            _massKept = true;
        }

        private void PutMassBack()
        {
            if (!_massKept || _rigidbody == null)
            {
                return;
            }
            _rigidbody.centerOfMass = _massCentre;
            _rigidbody.inertiaTensor = _inertia;
            _rigidbody.inertiaTensorRotation = _inertiaTurn;
        }

        // The bars box on: solid on the layer the game gives a ridden scooter's box.
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
        /// The bars box off. Traffic is told first, as the game tells it of any collider it takes away
        /// (Manager.TriggerColliderRemovedEvent): the engine says nothing to the traffic's sensors when a collider is switched
        /// off or destroyed, and a car that had sensed the box would wait for it for ever. Safe to ask twice.
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

        // The game's own getting off, from inside its ExitVehicle: the box is off before anything else of the exit runs on.
        private void Exited(VehicleController vehicle)
        {
            if (ReferenceEquals(vehicle, _scooter))
            {
                BarsOff();
            }
        }

        private void HookExit(bool on)
        {
            _onExit = _onExit ?? Guard.Wrap<VehicleController>(Exited);   // (made once, through the guard: Core/Guard)
            GlobalEvents.onExitVehicle = (Action<VehicleController>)Delegate.Remove(GlobalEvents.onExitVehicle, _onExit);
            if (on)
            {
                GlobalEvents.onExitVehicle = (Action<VehicleController>)Delegate.Combine(_onExit, GlobalEvents.onExitVehicle);
            }
            _exitHooked = on;
        }

        // The game destroying the scooter (or the scene going) with the box on: traffic is told it has gone.
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

        // Its name wherever the game names what the pointer is on: the game's own per-object header, which it gives
        // a chauffeur's car the same way (EntityController.customOverlayHeaderKey). The panel while it is ridden and the
        // game's few messages that name a vehicle are TwoWheelers' (they belong to the ride, not to the bike).
        private void Name()
        {
            _headerWas = _scooter.customOverlayHeaderKey;
            _scooter.customOverlayHeaderKey = Def.Name;
            _headerSet = true;
        }

        // The game marks the player's cars on the map and, within 100 m, on the street; it leaves scooters out
        // (they are hire scooters to it). Ours get that same marker, under their own name, so that they can be found.
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
        /// Everything about where this two-wheeler is and whether it can be seen, as one line for the log: its
        /// place, the ground found under it, what of its body is drawn and where that is, what of the scooter is
        /// hidden, and how the game holds it. Builds a string: called when one is made and once shortly after.
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
                   + $"physics {(_nwh != null && _nwh.enabled ? "running" : "at rest")}, map marker {(_poi != null ? "yes" : "no")}.";
        }

        // ---------------------------------------------------------------- the numbers

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

            // Record first.
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

            // The game takes a scooter away four hours after it was last looked at. These are kept: an owned one is
            // the player's, a hired one is collected by the mod (TwoWheelers), which also tells the player.
            if (_autoDestroy != null)
            {
                _autoDestroy.enabled = false;
            }

            // Leaning with the speed, gently at a crawl (BikeRules): the scooter leans fully from 1 m/s and swings there fast.
            if (_lean != null)
            {
                _lean.maxLeanAngle = LeanMostCurve();
                _lean.leanAngleMaxDelta = LeanRateCurve();
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
                // The scooter's drivetrain let go: its engine does not run, its clutch stays out, and its final drive is made
                // so small that the engine and gearbox no longer weigh on the rear wheel (through the gearbox they did, like
                // another 275 kg, and 600 with the clutch in). The rider pushes it instead (FixedUpdate, BikeRules.Pedal).
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
                    _limiter.speedLimit = 0f;   // the limiter's own "no limit": drag ends the speed now
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

        // The lean curves of BikeRules as the game's module reads them (degrees against m/s), made once for every two-wheeler.
        private static AnimationCurve LeanMostCurve()
        {
            if (_leanMostCurve == null)
            {
                _leanMostCurve = new AnimationCurve(new Keyframe(0f, BikeRules.LeanMost(0f)), new Keyframe(BikeRules.FullLeanAt, BikeRules.LeanMost(BikeRules.FullLeanAt)),
                    new Keyframe(90f, BikeRules.LeanMost(90f)));
                for (int i = 0; i < _leanMostCurve.length; i++)
                {
                    Linear(_leanMostCurve, i);
                }
            }
            return _leanMostCurve;
        }

        private static AnimationCurve LeanRateCurve()
        {
            if (_leanRateCurve == null)
            {
                _leanRateCurve = new AnimationCurve(new Keyframe(0f, BikeRules.LeanRate(0f)), new Keyframe(BikeRules.FullLeanAt, BikeRules.LeanRate(BikeRules.FullLeanAt)),
                    new Keyframe(90f, BikeRules.LeanRate(90f)));
                for (int i = 0; i < _leanRateCurve.length; i++)
                {
                    Linear(_leanRateCurve, i);
                }
            }
            return _leanRateCurve;
        }

        // Straight lines between the keys, as the rules have it.
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
            // First and each on its own, so that nothing further down can leave them changed: the game's box, its obstacle,
            // its header and the body's mass.
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
                    MapPlaces.Drop(_poi);   // out of the game's list of markers first, then destroyed (Core/MapPlaces)
                    _poi = null;
                }
                _bars = null;   // (on our body, which goes next)
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

        // ---------------------------------------------------------------- the rider

        /// <summary>The player has just got on (the game has attached them and given them its riding pose).</summary>
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
            BarsOn();         // as the game puts a ridden scooter's own box
            HookExit(true);   // off again inside the game's own getting off
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
                    // How high this character's hips are when standing: what the saddle is fitted to.
                    hip = Mathf.Clamp(Vector3.Dot(hips.position - _player.position, _player.up), 0.75f, 1.15f);
                }
            }
            // Riding clips of the mod's own, when the clip bundle has them: seated loops made with the feet's ground
            // under the saddle and the palms on this model's grips (CityAssets/README.md, "Garage"). One for riding
            // (pedalling, on a bicycle), one for freewheeling (bicycles), one for standing still with a foot down;
            // each model's own, else the town bicycle's or the roadster's. Without them the game's own riding pose.
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
            // The game seats the rider at the scooter's origin, standing on its deck. Moved over the seat, and (in the
            // game's standing pose) up or down so that the hip joint is just above it: the joint is some 8 cm above
            // what one sits on. A riding clip brings its own height.
            float lift = _seated != null ? 0f : Mathf.Clamp(Def.Seat.x + 0.08f - hip, -0.3f, 0.3f);
            _player.localPosition = new Vector3(0f, lift, Def.Seat.y + 0.04f);

            // The hands: the game's own hand rig, aimed at our grips instead of the scooter's bar. The grips
            // take the scooter's own grip rotation, since that is what the rig was set up to hold.
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

        // The Head bone's axes are the rig's, not the character's (other features measure them too: ServiceKit,
        // EventPeople.Wear). In the avatar's T-pose the head is held straight, so the bone's rotation there,
        // relative to the character, is what stands between the two: read once per mount from the avatar's own
        // skeleton. False when the avatar does not say (the helmet then keeps the player's heading, as it used to).
        private static bool HeadSquare(Animator animator, Transform head, out Quaternion square)
        {
            square = Quaternion.identity;
            try
            {
                SkeletonBone[] skeleton = animator != null && animator.avatar != null ? animator.avatar.humanDescription.skeleton : null;
                if (skeleton == null || skeleton.Length == 0)
                {
                    return false;
                }
                Quaternion inPose = Quaternion.identity;   // the Head bone's rotation relative to the character, in the T-pose
                bool found = false;
                for (Transform bone = head; bone != null && bone != animator.transform; bone = bone.parent)
                {
                    Quaternion local = bone.localRotation;   // a node the avatar does not list does not move
                    for (int i = 0; i < skeleton.Length; i++)
                    {
                        if (skeleton[i].name == bone.name)
                        {
                            local = skeleton[i].rotation;
                            found |= bone == head;
                            break;
                        }
                    }
                    inPose = local * inPose;
                }
                if (!found)
                {
                    return false;
                }
                square = Quaternion.Inverse(inPose);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // A looping clip by its id, or by the id every model of that kind can fall back on; null if the bundle has neither.
        private static AnimationClip Loop(string own, string shared)
        {
            if (!ClipLibrary.Ready)
            {
                return null;
            }
            string id = own != null && ClipLibrary.Has(own) ? own : ClipLibrary.Has(shared) ? shared : null;
            return id != null && ClipLibrary.TryGet(id, out CityClip clip) && clip.Loop ? clip.Clip : null;
        }

        // Which clip the rider shows (BikeRules.Want): riding (pedalling, on a bicycle), freewheeling, or stopped with a
        // foot down. Looked at each frame while ridden (a few comparisons); the engine is told only when it changes, a
        // change has to last its moment first (pushing off: at once), and none is made while the last is still fading in:
        // the animator replaces the clip that is fading out, which would show as a jump.
        private void Pose(float dt, float speed, bool pushing)
        {
            _poseAge += dt;
            BikeRules.Pose want = BikeRules.Want(_riding, speed, pushing, _standClip != null, _coastClip != null, Def.Bicycle);
            if (want == _riding)
            {
                _otherFor = 0f;
                return;
            }
            _otherFor += dt;
            if (_otherFor < BikeRules.HoldFor(_riding, want) || _poseAge < BikeRules.PoseFade)
            {
                return;
            }
            AnimationClip clip = want == BikeRules.Pose.Stand ? _standClip : want == BikeRules.Pose.Coast ? _coastClip : _rideClip;
            if (clip != null && _seated.Play(clip, BikeRules.PoseFade))
            {
                _riding = want;
                _poseAge = 0f;
            }
            _otherFor = 0f;
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
            BarsOff();   // standing, it is the frame the game can be pointed at, as with its own scooter
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
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnFixedUpdate();
            }
            catch (System.Exception e)
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
            // A bicycle is pushed by its rider (BikeRules.Pedal): hard from a standstill, no more than the rider's power as
            // it gathers speed, half as hard when tired; held at a standstill in the gearbox's reverse, walked back slowly.
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
            // Coming off: most of the speed gone in one physics step (it hit something), or the bike on its side while moving.
            // Not when the game has just moved the bike somewhere else (a tow, a way in or out of a building).
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

        private static readonly int HotLateUpdate = HotSpots.Slot("Garage: BikeMount.LateUpdate");
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()
        {
            if (!_ridden || _body == null || _nwh == null)
            {
                return;
            }
            float dt = Time.deltaTime;
            float speed = _nwh.Speed;

            bool pushing = _nwh.input != null && _nwh.input.Throttle > 0.1f;
            if (_seated != null)
            {
                Pose(dt, speed, pushing);
            }
            if (_body.FrontWheel != null && _body.RearWheel != null)   // a stand-in or a model in parts; a one-mesh body has no wheels to turn
            {
                _spin = Mathf.Repeat(_spin + speed / Def.WheelRadius * dt * Mathf.Rad2Deg, 360f);
                Quaternion roll = Quaternion.AngleAxis(_spin, Vector3.right);
                _body.FrontWheel.localRotation = roll;
                _body.RearWheel.localRotation = roll;
            }
            if (_front != null)
            {
                // The physics steers once a step; the bars are drawn every frame, so they follow it eased, not in steps.
                _steerShown = BikeRules.Ease(_steerShown, _front.SteerAngle, dt, SteerEase);
                _body.Steer.localRotation = Quaternion.AngleAxis(_steerShown, _body.SteerAxis);
            }

            Vector3 forward = transform.forward;
            if (Def.Bicycle)
            {
                bool stopped = _seated != null && _riding == BikeRules.Pose.Stand;
                if (stopped)
                {
                    // Standing astride with the left foot down: the clip has the right foot on its pedal, forward and
                    // level, so the cranks are brought round to there (backwards, as one does on a freewheel) and the
                    // legs are handed to the clip.
                    if (_crank != 0f)
                    {
                        _crank = Mathf.MoveTowards(_crank, _crank > Mathf.PI ? Mathf.PI * 2f : 0f, CrankHome * dt);
                        if (_crank >= Mathf.PI * 2f)
                        {
                            _crank = 0f;
                        }
                        _body.Pedals(Def, _crank);
                    }
                }
                else if (pushing && speed > 0.3f)
                {
                    _crank = Mathf.Repeat(_crank - speed * CadencePerMetre * dt, Mathf.PI * 2f);   // the top of the stroke goes forwards
                    _body.Pedals(Def, _crank);
                }
                _legs = Mathf.MoveTowards(_legs, stopped ? 0f : 1f, dt / LegsFade);
                if (_legs > 0f && _footL != null && _footR != null)
                {
                    float weight = BikeRules.Smooth(_legs);   // eased in and out: the feet leave and reach the pedals without a jolt
                    Vector3 up = transform.up * AnkleAbovePedal - forward * 0.03f;
                    LegIk.Solve(_thighR, _shinR, _footR, transform.TransformPoint(BikeBody.PedalAt(Def, _crank, 1f)) + up, forward, weight);
                    LegIk.Solve(_thighL, _shinL, _footL, transform.TransformPoint(BikeBody.PedalAt(Def, _crank + Mathf.PI, -1f)) + up, forward, weight);
                }
            }
            else if (_seated == null && _footL != null && _footR != null)
            {
                Vector3 right = transform.right;
                Vector3 up = transform.up * AnkleAbovePedal;
                LegIk.Solve(_thighR, _shinR, _footR, transform.TransformPoint(new Vector3(Def.Peg.x, Def.Peg.y, Def.Peg.z)) + up, forward + right * 0.25f);
                LegIk.Solve(_thighL, _shinL, _footL, transform.TransformPoint(new Vector3(-Def.Peg.x, Def.Peg.y, Def.Peg.z)) + up, forward - right * 0.25f);
            }

            if (_helmet != null && _head != null && _player != null)
            {
                // On the head, turn and all: the helmet's pivot is the Head bone, and it turns with the bone
                // (by the bone's own axes made square to the character: see HeadSquare).
                _helmet.transform.SetPositionAndRotation(_head.position, _headKnown ? _head.rotation * _headSquare : _player.rotation);
            }
        }
    }

    /// <summary>
    /// A leg put on a point after the animation: the thigh and shin are turned so that the ankle is on
    /// the target with the knee towards a given side. Unlike CityLife's TwoBoneReach (made for arms that
    /// are already bent the right way) this decides the bend itself: a standing leg is straight.
    /// </summary>
    public static class LegIk
    {
        /// <summary>weight: 1 puts the ankle on the target; less leaves that much of the pose the animation gave the leg.</summary>
        public static void Solve(Transform thigh, Transform shin, Transform foot, Vector3 target, Vector3 kneeTowards, float weight = 1f)
        {
            if (thigh == null || shin == null || foot == null)
            {
                return;
            }
            Vector3 hip = thigh.position;
            float upper = (shin.position - hip).magnitude;
            float lower = (foot.position - shin.position).magnitude;
            if (upper < 0.01f || lower < 0.01f)
            {
                return;
            }
            Quaternion footTurn = foot.rotation;
            Quaternion thighWas = thigh.localRotation, shinWas = shin.localRotation;
            Vector3 to = target - hip;
            float reach = Mathf.Clamp(to.magnitude, Mathf.Abs(upper - lower) + 0.01f, upper + lower - 0.005f);
            Vector3 along = to.sqrMagnitude > 1e-6f ? to.normalized : Vector3.down;
            Vector3 side = kneeTowards - along * Vector3.Dot(kneeTowards, along);
            if (side.sqrMagnitude < 1e-6f)
            {
                return;
            }
            side.Normalize();
            float cos = Mathf.Clamp((upper * upper + reach * reach - lower * lower) / (2f * upper * reach), -1f, 1f);
            Vector3 knee = hip + along * (upper * cos) + side * (upper * Mathf.Sqrt(1f - cos * cos));
            thigh.rotation = Quaternion.FromToRotation(shin.position - hip, knee - hip) * thigh.rotation;
            Vector3 ankle = hip + along * reach;
            shin.rotation = Quaternion.FromToRotation(foot.position - shin.position, ankle - shin.position) * shin.rotation;
            if (weight < 1f)
            {
                thigh.localRotation = Quaternion.Slerp(thighWas, thigh.localRotation, weight);
                shin.localRotation = Quaternion.Slerp(shinWas, shin.localRotation, weight);
            }
            foot.rotation = footTurn;   // the foot stays as flat as the animation had it
        }
    }
}
