using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NWH.Common.CoM;
using NWH.Common.Vehicles;
using NWH.VehiclePhysics2.GroundDetection;
using NWH.VehiclePhysics2.Modules.ABS;
using NWH.VehiclePhysics2.Powertrain;
using NWH.VehiclePhysics2.Powertrain.Wheel;
using NWH.WheelController3D;
using UnityEngine;
using NwhVehicle = NWH.VehiclePhysics2.VehicleController;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The handling of the car the player drives (docs/features/driving.md, "Handling"): the weight of its kind, a soft
    /// long-travel ride that swings once or twice and settles, the body rolling in bends and pitching as it brakes and
    /// pulls, steering quick at walking pace and calmer at speed, tyres that give progressively and slide, and brakes that
    /// build without locking. <see cref="HandlingModel"/> works the values out for the car's kind; this puts them on the
    /// game's own vehicle physics: the body's mass and centre of mass, every wheel's spring and bump stop, dampers, roll
    /// centre, anti-squat, load rating, rolling resistance and tyres (a friction preset of the mod's for each axle), the
    /// axles' anti-roll bars and brake shares, the steering's curves by speed, the game's own ABS module, and a ground preset
    /// with no surfaces of its own so that the game's terrains never swap the tyres back.
    ///
    /// Everything is recorded first and put back exactly. The Garage's parts always lie on top: lifted before anything
    /// here changes and laid back after (<see cref="DrivingBridges"/>), so a sports spring is stiffer than this base and
    /// a grippier tyre grips more than it, and nothing is applied twice. Only the car the player drives, and only their
    /// own in a Going Public session. Driven by <see cref="CarTuning"/>: the cost is nothing per frame; the work runs once
    /// when the player gets in and once on a change of an option or a slider.
    /// </summary>
    public sealed class CarHandling
    {
        private const float TownKmh = 50f;   // km/h at which the self-check line reads the steering in town

        private struct WheelStock
        {
            public WheelUAPI Wheel;
            public WheelController Controller;
            public bool Front;
            public float Force, Length, Bump, Rebound, Application, Rating, Rolling, SideStiffness, AntiSquat;
            public float SlowBump, FastBump, BumpKnee, SlowRebound, FastRebound, ReboundKnee;
            public AnimationCurve Curve;
            public FrictionPreset Tyre;
        }

        private struct GroupStock
        {
            public WheelGroup Group;
            public bool Front;
            public float AntiRoll;
            public float Brake;
        }

        private readonly CarChassis _chassis = new CarChassis();
        private readonly HandlingSetup _setup = new HandlingSetup();
        private readonly List<WheelStock> _wheels = new List<WheelStock>(4);
        private readonly List<GroupStock> _groups = new List<GroupStock>(2);
        private readonly Dictionary<string, float> _brakeTorques = new Dictionary<string, float>();   // vehicle id: the game's brake torque as first met this session
        private readonly HashSet<string> _refused = new HashSet<string>();   // vehicle types said once to keep the game's handling
        private readonly StringBuilder _line = new StringBuilder(1024);

        private NwhVehicle _vehicle;     // the car the handling is on, or null
        private CarController _car;
        private Rigidbody _body;
        private VariableCenterOfMass _centre;
        private string _type;
        private CarKind _kind;
        private HandlingLevel _level;

        private float _mass, _baseMass;
        private Vector3 _com, _centreCom, _centreCombined;
        private Vector3 _inertia;
        private Quaternion _inertiaTurn;
        private bool _inertiaSet;        // the tensor had to be written: the engine did not scale it with the mass
        private bool _inertiaImplicit;   // the game left the tensor to the engine (worked out from the colliders)
        private float _inertiaRatio;     // the yaw inertia after the mass changed, over before: for the self-check
        private AnimationCurve _steerCurve, _smoothCurve;
        private GroundDetectionPreset _ground;
        private ABSModuleWrapper _abs;   // the game's ABS module, put on by the handling (null: none, or the car had its own)
        private ABSModule _absTaken;     // the module last taken off, for the self-check

        private AnimationCurve _frontSpring, _rearSpring, _steerMade, _smoothMade;
        private FrictionPreset _frontTyre, _rearTyre;   // the mod's own tyres, one preset for each axle of the car driven
        private GroundDetectionPreset _road;            // the mod's ground preset: the car's fallback surface, no surfaces of its own

        /// <summary>True while the handling is on a car.</summary>
        public bool On => _vehicle != null;

        /// <summary>The seconds the eased brake pedal takes from nothing to full on the car it is on.</summary>
        public float BrakeRise => _setup.BrakeRise;

        /// <summary>Why the handling last kept off a car, for the test button; null once it is on one.</summary>
        public string Refusal { get; private set; }

        // ---------------------------------------------------------------- on

        /// <summary>
        /// Puts the handling on a car at a level, the Garage's parts lifted first and laid back on top after. True if it is
        /// on. Refused (and said once in the log) for a car that is not a four-wheeled car of a kind the mod tunes, or not
        /// the player's own in a Going Public session; anything unexpected throws, and the caller puts the car back.
        /// </summary>
        public bool Apply(CarController car, NwhVehicle vehicle, HandlingLevel level, in HandlingTuning tuning)
        {
            Restore();
            if (level == HandlingLevel.Off || car == null || vehicle == null || car.vehicleType == null || vehicle.vehicleRigidbody == null)
            {
                Refusal = "the option is off, or it is not a car";
                return false;
            }
            if (!GoingPublic.MineVehicle(car))
            {
                Refusal = "it is another player's car (Going Public)";
                return false;
            }
            Rigidbody body = vehicle.vehicleRigidbody;
            Vehicles.VehicleTypes.VehicleType type = car.vehicleType;
            var traits = new CarTraits(type.IsMotorVehicle, type.spawnInPlayerObject, type.maxSpeed, type.maxCargoCapacity, type.isLuxuryCar, body.mass);
            CarSpec spec = CarCatalogue.For(type.vehicleTypeName, traits);
            if (spec.Kind == CarKind.Untouched)
            {
                Refusal = "a scooter, a hand truck or a flatbed is left as the game made it";
                return false;
            }
            LiftParts(car);
            try
            {
                string why = Read(car, vehicle, body);
                if (why == null)
                {
                    why = HandlingModel.Design(_chassis, HandlingRules.Target(spec.Kind, level, tuning), _setup);
                }
                if (why != null)
                {
                    Refused(type.vehicleTypeName, why);
                    return false;
                }
                Build(vehicle);
                Record(car, vehicle, body);
                _vehicle = vehicle;
                _type = type.vehicleTypeName;
                _kind = spec.Kind;
                _level = level;
                Refusal = null;
                PutOn(vehicle, body);
                if (Meta.Dev.Verbose)
                {
                    CarTuning.Logger?.Info(Line(tuning));
                }
                return true;
            }
            finally
            {
                LayParts(car);   // the Garage's parts back on top of whatever is under them now
            }
        }

        // Reads the car into the chassis the model designs for. Changes nothing. Why the car cannot be done, or null.
        private string Read(CarController car, NwhVehicle vehicle, Rigidbody body)
        {
            List<WheelGroup> groups = vehicle.powertrain != null ? vehicle.powertrain.wheelGroups : null;
            if (groups == null || groups.Count != 2 || vehicle.brakes == null || vehicle.steering == null || vehicle.groundDetection == null)
            {
                return "not a car of two axles";
            }
            Transform space = vehicle.transform;
            float z0 = AxleZ(space, groups[0]);
            float z1 = AxleZ(space, groups[1]);
            if (float.IsNaN(z0) || float.IsNaN(z1) || Mathf.Abs(z0 - z1) < 0.5f)
            {
                return "its wheels are not two axles of two";
            }
            string why = ReadAxle(space, z0 > z1 ? groups[0] : groups[1], _chassis.Front) ?? ReadAxle(space, z0 > z1 ? groups[1] : groups[0], _chassis.Rear);
            if (why != null)
            {
                return why;
            }
            Vector3 com = body.centerOfMass;
            _chassis.Mass = body.mass;
            _chassis.ComY = com.y;
            _chassis.ComZ = com.z;
            string id = car.vehicleInstance != null ? car.vehicleInstance.id : null;
            float torque = vehicle.brakes.maxTorque;
            if (id != null)
            {
                // As first met this session: before the car's wear (Transport/CarOwnership, a tick after getting in) or the Garage's brakes are on it.
                if (_brakeTorques.TryGetValue(id, out float first))
                {
                    torque = first;
                }
                else
                {
                    _brakeTorques[id] = torque;
                }
            }
            _chassis.BrakeTorque = torque;
            _chassis.SteerLock = vehicle.steering.maximumSteerAngle;
            return null;
        }

        private static float AxleZ(Transform space, WheelGroup group)
        {
            List<WheelComponent> wheels = group != null ? group.Wheels : null;
            if (wheels == null || wheels.Count != 2 || wheels[0] == null || wheels[1] == null || wheels[0].wheelUAPI == null || wheels[1].wheelUAPI == null)
            {
                return float.NaN;
            }
            return 0.5f * (space.InverseTransformPoint(wheels[0].wheelUAPI.transform.position).z + space.InverseTransformPoint(wheels[1].wheelUAPI.transform.position).z);
        }

        private static string ReadAxle(Transform space, WheelGroup group, AxleData axle)
        {
            WheelUAPI a = group.Wheels[0].wheelUAPI;
            WheelUAPI b = group.Wheels[1].wheelUAPI;
            var controller = a as WheelController;
            if (controller == null || !(b is WheelController) || controller.spring == null || controller.damper == null || controller.spring.forceCurve == null)
            {
                return "its wheels are not the game's own";
            }
            Vector3 pa = space.InverseTransformPoint(a.transform.position);
            Vector3 pb = space.InverseTransformPoint(b.transform.position);
            axle.Z = 0.5f * (pa.z + pb.z);
            axle.Track = Mathf.Abs(pa.x - pb.x);
            axle.Top = 0.5f * (pa.y + pb.y);
            axle.Radius = 0.5f * (a.Radius + b.Radius);
            axle.SpringForce = a.SpringMaxForce;
            axle.SpringLength = a.SpringMaxLength;
            axle.SideGrip = a.LateralFrictionGrip;
            axle.AlongGrip = a.LongitudinalFrictionGrip;
            axle.LoadRating = a.MaxLoad;
            axle.AntiRoll = group.antiRollBarForce;
            axle.BrakeShare = group.brakeCoefficient;
            axle.Bump = a.DamperBumpRate;
            axle.Rebound = a.DamperReboundRate;
            axle.SlowBump = controller.damper.slowBump;
            axle.SlowRebound = controller.damper.slowRebound;
            axle.Application = a.ForceApplicationPointDistance;
            return null;
        }

        // The mod's own objects for the setup: the spring curves, the steering curves, the tyre presets and the ground's.
        // Made before the car records anything, so a fault here changes nothing on it.
        private void Build(NwhVehicle vehicle)
        {
            _frontSpring = SpringCurve(_setup.Front);
            _rearSpring = SpringCurve(_setup.Rear);
            var steer = new Keyframe[HandlingModel.SteerKeys];
            for (int i = 0; i < steer.Length; i++)
            {
                steer[i] = Straight(_setup.SteerAt, _setup.SteerShare, i);
            }
            _steerMade = new AnimationCurve(steer);
            _smoothMade = new AnimationCurve(Straight(_setup.SmoothAt, _setup.SmoothTime, 0), Straight(_setup.SmoothAt, _setup.SmoothTime, 1));
            // Made once and filled again for each car; destroyed in Dispose.
            if (_frontTyre == null)
            {
                _frontTyre = ScriptableObject.CreateInstance<FrictionPreset>();
            }
            if (_rearTyre == null)
            {
                _rearTyre = ScriptableObject.CreateInstance<FrictionPreset>();
            }
            Tyre(_frontTyre, _setup.Front, "RealLifeOverhaul front tyres");
            Tyre(_rearTyre, _setup.Rear, "RealLifeOverhaul rear tyres");
            GroundDetectionPreset game = vehicle.groundDetection.groundDetectionPreset;
            if (_road == null)
            {
                _road = ScriptableObject.CreateInstance<GroundDetectionPreset>();
            }
            _road.name = "RealLifeOverhaul road";
            _road.surfaceMaps.Clear();   // no surface of its own: the game's terrains never put their tyres on the car
            if (game != null && game != _road)
            {
                _road.fallbackSurfacePreset = game.fallbackSurfacePreset;   // the skid and road sounds the car has everywhere else
                _road.particlePrefab = game.particlePrefab;
                _road.chunkPrefab = game.chunkPrefab;
            }
        }

        private static AnimationCurve SpringCurve(AxleSetup axle)
        {
            var keys = new Keyframe[HandlingModel.CurveKeys];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = Straight(axle.CurveAt, axle.CurveShare, i);
            }
            return new AnimationCurve(keys);
        }

        // A key whose tangents run straight to its neighbours, so that the game's curve reads straight lines between keys.
        private static Keyframe Straight(float[] at, float[] value, int i)
        {
            int last = at.Length - 1;
            float left = i > 0 ? (value[i] - value[i - 1]) / (at[i] - at[i - 1]) : (value[1] - value[0]) / (at[1] - at[0]);
            float right = i < last ? (value[i + 1] - value[i]) / (at[i + 1] - at[i]) : left;
            return new Keyframe(at[i], value[i], left, right);
        }

        // The game's own tyre sum on the mod's figures: the curve sampled from the magic formula for the side force, then
        // the D figure set to the peak along, which the game reads apart from the curve.
        private static void Tyre(FrictionPreset preset, AxleSetup axle, string name)
        {
            preset.name = name;
            preset.BCDE = new Vector4(axle.TyreB, axle.TyreC, axle.TyreSide, axle.TyreE);
            preset.UpdateFrictionCurve();
            preset.BCDE = new Vector4(axle.TyreB, axle.TyreC, axle.TyreAlong, axle.TyreE);
        }

        // Everything the handling will change, as the car has it now.
        private void Record(CarController car, NwhVehicle vehicle, Rigidbody body)
        {
            _car = car;
            _body = body;
            _mass = body.mass;
            _com = body.centerOfMass;
            _inertia = body.inertiaTensor;
            _inertiaTurn = body.inertiaTensorRotation;
            _centre = vehicle.GetComponent<VariableCenterOfMass>();
            _inertiaImplicit = _centre == null || _centre.useDefaultInertia;
            if (_centre != null)
            {
                _baseMass = _centre.baseMass;
                _centreCom = _centre.centerOfMass;
                _centreCombined = _centre.combinedCenterOfMass;
            }
            _wheels.Clear();
            _groups.Clear();
            List<WheelGroup> groups = vehicle.powertrain.wheelGroups;
            Transform space = vehicle.transform;
            bool firstFront = AxleZ(space, groups[0]) > AxleZ(space, groups[1]);
            for (int g = 0; g < groups.Count; g++)
            {
                WheelGroup group = groups[g];
                bool front = (g == 0) == firstFront;
                _groups.Add(new GroupStock { Group = group, Front = front, AntiRoll = group.antiRollBarForce, Brake = group.brakeCoefficient });
                foreach (WheelComponent component in group.Wheels)
                {
                    WheelUAPI wheel = component.wheelUAPI;
                    var controller = (WheelController)wheel;
                    Damper damper = controller.damper;
                    _wheels.Add(new WheelStock
                    {
                        Wheel = wheel, Controller = controller, Front = front,
                        Force = wheel.SpringMaxForce, Length = wheel.SpringMaxLength, Curve = controller.spring.forceCurve,
                        Bump = wheel.DamperBumpRate, Rebound = wheel.DamperReboundRate,
                        SlowBump = damper.slowBump, FastBump = damper.fastBump, BumpKnee = damper.bumpDivisionVelocity,
                        SlowRebound = damper.slowRebound, FastRebound = damper.fastRebound, ReboundKnee = damper.reboundDivisionVelocity,
                        Application = wheel.ForceApplicationPointDistance, AntiSquat = controller.antiSquat, Rating = wheel.MaxLoad,
                        Rolling = wheel.RollingResistanceTorque, Tyre = wheel.FrictionPreset, SideStiffness = wheel.LateralFrictionStiffness
                    });
                }
            }
            _steerCurve = vehicle.steering.speedSensitiveSteeringCurve;
            _smoothCurve = vehicle.steering.speedSensitiveSmoothingCurve;
            _ground = vehicle.groundDetection.groundDetectionPreset;
        }

        // The design on the car. Every write of a value of the game's here has its write back in TakeOff (NavCheck reads both).
        private void PutOn(NwhVehicle vehicle, Rigidbody body)
        {
            Vector3 before = body.inertiaTensor;
            body.mass = _setup.Mass;
            if (_centre != null)
            {
                _centre.baseMass = _setup.Mass;
                _centre.centerOfMass = new Vector3(_centreCom.x, _setup.ComY, _centreCom.z);
                _centre.combinedCenterOfMass = new Vector3(_centreCombined.x, _setup.ComY, _centreCombined.z);
            }
            body.centerOfMass = new Vector3(_com.x, _setup.ComY, _com.z);
            // A heavier car turns as heavily: its inertia grows with its mass. The engine scales a tensor it works out
            // itself; one it does not is written here, scaled the same.
            float ratio = _setup.Mass / _mass;
            _inertiaSet = Mathf.Abs(body.inertiaTensor.y - before.y * ratio) > before.y * ratio * 0.02f;
            if (_inertiaSet)
            {
                body.inertiaTensor = before * ratio;
                body.inertiaTensorRotation = _inertiaTurn;
            }
            _inertiaRatio = before.y > 0f ? body.inertiaTensor.y / before.y : 0f;

            for (int i = 0; i < _wheels.Count; i++)
            {
                WheelStock stock = _wheels[i];
                AxleSetup axle = stock.Front ? _setup.Front : _setup.Rear;
                WheelUAPI wheel = stock.Wheel;
                WheelController controller = stock.Controller;
                wheel.SpringMaxForce = axle.SpringForce;
                wheel.SpringMaxLength = axle.SpringLength;
                controller.spring.forceCurve = stock.Front ? _frontSpring : _rearSpring;
                wheel.DamperBumpRate = axle.Bump;
                wheel.DamperReboundRate = axle.Rebound;
                controller.damper.slowBump = 1f;
                controller.damper.fastBump = axle.Fast;
                controller.damper.bumpDivisionVelocity = axle.Knee;
                controller.damper.slowRebound = 1f;
                controller.damper.fastRebound = axle.Fast;
                controller.damper.reboundDivisionVelocity = axle.Knee;
                wheel.ForceApplicationPointDistance = axle.Application;
                controller.antiSquat = axle.AntiSquat;
                wheel.MaxLoad = axle.LoadRating;
                wheel.RollingResistanceTorque = axle.Rolling;
                wheel.FrictionPreset = stock.Front ? _frontTyre : _rearTyre;
                wheel.LateralFrictionStiffness = axle.SideStiffness;
            }
            for (int g = 0; g < _groups.Count; g++)
            {
                GroupStock stock = _groups[g];
                AxleSetup axle = stock.Front ? _setup.Front : _setup.Rear;
                stock.Group.antiRollBarForce = axle.AntiRoll;
                stock.Group.brakeCoefficient = axle.BrakeShare;
            }
            vehicle.steering.speedSensitiveSteeringCurve = _steerMade;
            vehicle.steering.speedSensitiveSmoothingCurve = _smoothMade;
            vehicle.groundDetection.groundDetectionPreset = _road;

            // The game's own ABS: the brakes eased off for a step whenever a wheel begins to lock, never with the handbrake.
            if (vehicle.GetComponent<ABSModuleWrapper>() == null)
            {
                _ = vehicle.moduleManager.Components.Count;   // the module list made before the wrapper is on, so the module joins it once
                _abs = vehicle.gameObject.AddComponent<ABSModuleWrapper>();
                vehicle.moduleManager.AddAndOnboardNewComponent(_abs.module);
                _abs.module.VC_Enable(false);
            }
        }

        // ---------------------------------------------------------------- off

        /// <summary>The car exactly as it was before <see cref="Apply"/>, the Garage's parts lifted first and laid back on top after. Safe with nothing on.</summary>
        public void Restore()
        {
            NwhVehicle vehicle = _vehicle;
            CarController car = _car;
            _vehicle = null;
            _car = null;
            if (vehicle == null)
            {
                return;   // nothing of the handling's anywhere (or the car is gone, and its values with it)
            }
            LiftParts(car);
            try
            {
                TakeOff(vehicle);
                if (Meta.Dev.Verbose)
                {
                    CheckOff(vehicle);
                }
            }
            finally
            {
                LayParts(car);   // the Garage's parts back on the car as the game made it
            }
        }

        private void TakeOff(NwhVehicle vehicle)
        {
            try
            {
                Rigidbody body = _body;
                if (body != null)
                {
                    body.mass = _mass;
                    body.centerOfMass = _com;
                    if (_inertiaSet)
                    {
                        if (_inertiaImplicit)
                        {
                            body.ResetInertiaTensor();   // the engine's own again, worked out from the colliders
                        }
                        else
                        {
                            body.inertiaTensor = _inertia;
                            body.inertiaTensorRotation = _inertiaTurn;
                        }
                    }
                }
                if (_centre != null)
                {
                    _centre.baseMass = _baseMass;
                    _centre.centerOfMass = _centreCom;
                    _centre.combinedCenterOfMass = _centreCombined;
                }
            }
            catch (Exception e)
            {
                CarTuning.Logger?.Warn("Handling: the car's weight could not be put back. " + e);
            }
            for (int i = 0; i < _wheels.Count; i++)
            {
                WheelStock stock = _wheels[i];
                try
                {
                    WheelUAPI wheel = stock.Wheel;
                    WheelController controller = stock.Controller;
                    if (wheel == null || controller == null)
                    {
                        continue;
                    }
                    wheel.SpringMaxForce = stock.Force;
                    wheel.SpringMaxLength = stock.Length;
                    controller.spring.forceCurve = stock.Curve;
                    wheel.DamperBumpRate = stock.Bump;
                    wheel.DamperReboundRate = stock.Rebound;
                    controller.damper.slowBump = stock.SlowBump;
                    controller.damper.fastBump = stock.FastBump;
                    controller.damper.bumpDivisionVelocity = stock.BumpKnee;
                    controller.damper.slowRebound = stock.SlowRebound;
                    controller.damper.fastRebound = stock.FastRebound;
                    controller.damper.reboundDivisionVelocity = stock.ReboundKnee;
                    wheel.ForceApplicationPointDistance = stock.Application;
                    controller.antiSquat = stock.AntiSquat;
                    wheel.MaxLoad = stock.Rating;
                    wheel.RollingResistanceTorque = stock.Rolling;
                    wheel.FrictionPreset = stock.Tyre;
                    wheel.LateralFrictionStiffness = stock.SideStiffness;
                }
                catch (Exception e)
                {
                    CarTuning.Logger?.Warn("Handling: a wheel could not be put back. " + e);
                }
            }
            for (int g = 0; g < _groups.Count; g++)
            {
                GroupStock stock = _groups[g];
                if (stock.Group != null)
                {
                    stock.Group.antiRollBarForce = stock.AntiRoll;
                    stock.Group.brakeCoefficient = stock.Brake;
                }
            }
            try
            {
                vehicle.steering.speedSensitiveSteeringCurve = _steerCurve;
                vehicle.steering.speedSensitiveSmoothingCurve = _smoothCurve;
                vehicle.groundDetection.groundDetectionPreset = _ground;
            }
            catch (Exception e)
            {
                CarTuning.Logger?.Warn("Handling: the steering or the ground could not be put back. " + e);
            }
            try
            {
                ABSModuleWrapper abs = _abs;
                _abs = null;
                _absTaken = abs != null ? abs.module : null;
                if (abs != null)
                {
                    abs.module.VC_Disable(false);   // its brake modifier off the car's brakes
                    vehicle.moduleManager.Components.Remove(abs.module);
                    UnityEngine.Object.Destroy(abs);
                }
            }
            catch (Exception e)
            {
                CarTuning.Logger?.Warn("Handling: the ABS module could not be taken off. " + e);
            }
        }

        /// <summary>When the city goes: the car put back, then the mod's own presets destroyed.</summary>
        public void Dispose()
        {
            Restore();
            if (_frontTyre != null)
            {
                UnityEngine.Object.Destroy(_frontTyre);
                _frontTyre = null;
            }
            if (_rearTyre != null)
            {
                UnityEngine.Object.Destroy(_rearTyre);
                _rearTyre = null;
            }
            if (_road != null)
            {
                UnityEngine.Object.Destroy(_road);
                _road = null;
            }
        }

        // ---------------------------------------------------------------- the Garage's parts on top

        private static void LiftParts(CarController car)
        {
            if (car != null && DrivingBridges.LiftParts != null)
            {
                DrivingBridges.LiftParts(car);
            }
        }

        private static void LayParts(CarController car)
        {
            if (car != null && DrivingBridges.LayParts != null)
            {
                DrivingBridges.LayParts(car);
            }
        }

        // ---------------------------------------------------------------- the self-check lines

        private void Refused(string type, string why)
        {
            Refusal = why;
            if (_refused.Add(type ?? string.Empty))
            {
                CarTuning.Logger?.Info("Handling: " + type + " keeps the game's own handling (" + why + ").");
            }
        }

        /// <summary>
        /// For the test button: the figures of the car the handling is on into <paramref name="data"/> for the notice, and
        /// the self-check line written to Player.log whatever the diagnostics say. False with the handling on no car.
        /// </summary>
        public bool Tell(Dictionary<string, string> data, in HandlingTuning tuning)
        {
            if (_vehicle == null)
            {
                return false;
            }
            data["mass"] = Num(_setup.Mass);
            data["hz"] = Num(_setup.Front.Frequency);
            data["zeta"] = Num(_setup.Front.Zeta);
            data["roll"] = Num(_setup.RollPerG);
            data["grip"] = Num(_setup.Front.SideG);
            data["brakes"] = Num(_setup.BrakeG);
            Meta.Dev.Always(CarTuning.Logger, Line(tuning));
            return true;
        }

        // The self-check line, getting in: what was put on against the game's own, the ride as the model steps it, and
        // the game's own values read back from the car against what was written; then PASS, or why not.
        private string Line(in HandlingTuning tuning)
        {
            AxleSetup f = _setup.Front;
            AxleSetup r = _setup.Rear;
            AxleData gf = _chassis.Front;
            AxleData gr = _chassis.Rear;
            float zeta = HandlingModel.Bounce(f, f.Load / HandlingModel.G, out int swings, out _);
            float kerb = HandlingModel.KerbLeft(f, f.Load / HandlingModel.G, HandlingModel.Kerb);
            float wheelbase = gf.Z - gr.Z;
            float gameCorner = _chassis.Mass * (_chassis.ComZ - gr.Z) / (2f * wheelbase);   // kg on a front wheel, the game's
            float gameRate = gf.SpringForce / gf.SpringLength;
            float gameZeta = gf.SlowBump * gf.Bump / (2f * Mathf.Sqrt(gameRate * gameCorner));
            WheelStock first = _wheels[0];
            FrictionPreset gameTyre = first.Tyre;
            float gameSide = gameTyre != null && gameTyre.Curve != null ? gameTyre.Curve.Evaluate(gameTyre.peakSlip) * HandlingModel.SideFactor * gf.SideGrip : 0f;

            string why = null;
            AxleSetup axle = first.Front ? f : r;
            AnimationCurve spring = first.Controller.spring.forceCurve;
            float mid = axle.CurveAt[1] * 0.5f;
            if (Mathf.Abs(spring.Evaluate(mid) - HandlingModel.SpringAt(axle, mid) / axle.SpringForce) > 0.01f)
            {
                why = "the spring's curve reads " + Num(spring.Evaluate(mid)) + " at " + Num(mid);
            }
            float peak = _frontTyre.Curve.Evaluate(f.PeakSlip);
            if (why == null && Mathf.Abs(peak - f.TyreSide) > f.TyreSide * 0.03f)
            {
                why = "the front tyres' curve peaks at " + Num(peak) + " where " + Num(f.TyreSide) + " was asked";
            }
            AnimationCurve steering = _vehicle.steering.speedSensitiveSteeringCurve;
            if (why == null && Mathf.Abs(steering.Evaluate(_setup.SteerAt[4]) - _setup.SteerShare[4]) > 0.01f)
            {
                why = "the steering's curve reads " + Num(steering.Evaluate(_setup.SteerAt[4])) + " at " + Num(_setup.SteerAt[4] * HandlingModel.SteerTop) + " m/s";
            }
            if (why == null && (first.Wheel.SpringMaxLength != axle.SpringLength || first.Wheel.FrictionPreset != (first.Front ? _frontTyre : _rearTyre)))
            {
                why = "the springs or tyres did not take";
            }
            if (why == null && _vehicle.groundDetection.groundDetectionPreset != _road)
            {
                why = "the ground's preset did not take";
            }
            if (why == null && _abs != null && !_abs.module.IsActive)
            {
                why = "the ABS module is not running";
            }
            if (why == null && Mathf.Abs(_inertiaRatio - _setup.Mass / _mass) > 0.03f * _setup.Mass / _mass)
            {
                why = "the inertia came to " + Num(_inertiaRatio) + " times the game's for " + Num(_setup.Mass / _mass) + " times the mass";
            }
            if (why == null && (zeta < 0.18f || zeta > 0.32f || kerb < 0.005f))
            {
                why = "the ride steps to a damping ratio of " + Num(zeta) + " with " + Num(kerb * 100f) + " cm left over a kerb";
            }

            float lockDegrees = _chassis.SteerLock;
            _line.Clear();
            _line.Append("Handling check (getting in): ").Append(_type).Append(" (").Append(_kind).Append(", ").Append(_level).Append(", sliders ")
                .Append(tuning.Softness).Append('/').Append(tuning.Bounce).Append('/').Append(tuning.Roll).Append('/').Append(tuning.Grip).Append('/')
                .Append(tuning.Steering).Append('/').Append(tuning.Brakes).Append(" %): ")
                .Append(Num(_mass)).Append(" -> ").Append(Num(_setup.Mass)).Append(" kg, centre of mass ").Append(Num(_setup.ComHeightBefore)).Append(" -> ")
                .Append(Num(_setup.ComHeight)).Append(" m up; springs ").Append(Num(gameRate)).Append(" -> ").Append(Num(f.Rate)).Append(" N/m front, ")
                .Append(Num(gr.SpringForce / gr.SpringLength)).Append(" -> ").Append(Num(r.Rate)).Append(" rear, ride ").Append(Num(f.Frequency)).Append(" Hz; damping ratio ")
                .Append(Num(gameZeta)).Append(" -> ").Append(Num(f.Zeta)).Append(" (as the body's swings die away: ").Append(Num(zeta)).Append(", ").Append(swings)
                .Append(" swings you can see); static sag ").Append(Num(f.Sag)).Append(" m of ").Append(Num(f.SpringLength)).Append(" m of spring, ").Append(Num(f.RestLength))
                .Append(" m of bump travel").Append(f.Raised > 0.001f ? " (raised " + Num(f.Raised * 100f) + " cm for it)" : string.Empty).Append(", a ")
                .Append(Num(HandlingModel.Kerb * 100f)).Append(" cm kerb leaves ").Append(Num(kerb * 100f)).Append(" cm; roll ").Append(Num(_setup.RollPerG))
                .Append(" degrees per g (roll centre ").Append(Num(_setup.RollCentre)).Append(" m, bars ").Append(Num(f.AntiRoll)).Append('/').Append(Num(r.AntiRoll))
                .Append(" N/m against the game's ").Append(Num(gf.AntiRoll)).Append('/').Append(Num(gr.AntiRoll)).Append("), dive ").Append(Num(_setup.DivePerG))
                .Append(" degrees per g, tips at ").Append(Num(_setup.TipG)).Append(" g; tyres ").Append(Num(f.SideG)).Append('/').Append(Num(r.SideG))
                .Append(" g to the side against the game's ").Append(Num(gameSide)).Append(", ").Append(Num(f.AlongG)).Append(" g along, at their peak at ")
                .Append(Num(f.PeakDegrees)).Append('/').Append(Num(r.PeakDegrees)).Append(" degrees; steering ").Append(Num(HandlingModel.SteerAt(_setup, lockDegrees, 1.4f)))
                .Append('/').Append(Num(HandlingModel.SteerAt(_setup, lockDegrees, TownKmh / 3.6f))).Append('/').Append(Num(HandlingModel.SteerAt(_setup, lockDegrees, 100f / 3.6f)))
                .Append(" degrees at 5/50/100 km/h; brakes ").Append(Num(_setup.BrakeG)).Append(" g at the full pedal, built in ").Append(Num(_setup.BrakeRise))
                .Append(" s, shares ").Append(Num(f.BrakeShare)).Append('/').Append(Num(r.BrakeShare)).Append(" against the game's ").Append(Num(gf.BrakeShare)).Append('/')
                .Append(Num(gr.BrakeShare)).Append(_abs != null ? ", the game's ABS on" : ", the car's own ABS").Append("; inertia ")
                .Append(_inertiaSet ? "scaled here" : "scaled by the engine").Append(": ").Append(why == null ? "PASS" : "FAIL, " + why).Append('.');
            return _line.ToString();
        }

        // Once, getting out: every value the handling wrote, read back from the car against the game's own.
        private void CheckOff(NwhVehicle vehicle)
        {
            int count = 0;
            string why = null;
            void Same(bool same, string what)
            {
                count++;
                if (!same && why == null)
                {
                    why = what;
                }
            }
            if (_body != null)
            {
                Same(_body.mass == _mass && _body.centerOfMass == _com, "the mass and centre of mass");
            }
            if (_centre != null)
            {
                Same(_centre.baseMass == _baseMass && _centre.centerOfMass == _centreCom && _centre.combinedCenterOfMass == _centreCombined, "the centre of mass's own figures");
            }
            for (int i = 0; i < _wheels.Count; i++)
            {
                WheelStock s = _wheels[i];
                if (s.Wheel == null || s.Controller == null)
                {
                    continue;
                }
                Damper d = s.Controller.damper;
                Same(s.Wheel.SpringMaxForce == s.Force && s.Wheel.SpringMaxLength == s.Length && s.Controller.spring.forceCurve == s.Curve, "a spring");
                Same(s.Wheel.DamperBumpRate == s.Bump && s.Wheel.DamperReboundRate == s.Rebound && d.slowBump == s.SlowBump && d.fastBump == s.FastBump
                     && d.bumpDivisionVelocity == s.BumpKnee && d.slowRebound == s.SlowRebound && d.fastRebound == s.FastRebound && d.reboundDivisionVelocity == s.ReboundKnee,
                    "a damper");
                Same(s.Wheel.ForceApplicationPointDistance == s.Application && s.Controller.antiSquat == s.AntiSquat, "a roll centre or anti-squat");
                Same(s.Wheel.MaxLoad == s.Rating && s.Wheel.RollingResistanceTorque == s.Rolling, "a tyre's rating or rolling resistance");
                Same(s.Wheel.FrictionPreset == s.Tyre && s.Wheel.LateralFrictionStiffness == s.SideStiffness, "a tyre");
            }
            for (int g = 0; g < _groups.Count; g++)
            {
                GroupStock s = _groups[g];
                Same(s.Group == null || (s.Group.antiRollBarForce == s.AntiRoll && s.Group.brakeCoefficient == s.Brake), "an axle's bar or brakes");
            }
            Same(vehicle.steering.speedSensitiveSteeringCurve == _steerCurve && vehicle.steering.speedSensitiveSmoothingCurve == _smoothCurve, "the steering");
            Same(vehicle.groundDetection.groundDetectionPreset == _ground, "the ground's preset");
            Same(_absTaken == null || (!_absTaken.IsActive && !vehicle.moduleManager.Components.Contains(_absTaken)), "the ABS module's removal");
            CarTuning.Logger?.Info("Handling check (getting out): " + _type + ": " + count + " sets of values read back as the game had them"
                                   + (_absTaken != null ? ", the game's ABS module taken off" : string.Empty) + ": " + (why == null ? "PASS" : "FAIL, " + why + " is not") + ".");
        }

        private static string Num(float value)
        {
            return value.ToString(value >= 100f || value <= -100f ? "0" : "0.###", CultureInfo.InvariantCulture);
        }
    }
}
