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
    /// The handling of the car the player drives: the game's own car with a little weight, roll and nose dive, smooth brakes
    /// that never lock and a little give past the limit. <see cref="HandlingModel"/> works the values out; this puts them on
    /// the game's vehicle physics, records everything first and puts it back exactly. The Garage's parts always lie on top
    /// (<see cref="DrivingBridges"/>). Only the player's own car. Driven by <see cref="CarTuning"/>: nothing per frame.
    /// </summary>
    public sealed class CarHandling
    {
        private const float TownKmh = 50f;   // km/h at which the self-check line reads the steering in town

        private struct WheelStock
        {
            public WheelUAPI Wheel;
            public WheelController Controller;
            public bool Front;
            public float Force, Length, Bump, Rebound, Application, Rating, Rolling, AntiSquat;
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
        private readonly StringBuilder _line = new StringBuilder(1400);

        private NwhVehicle _vehicle;     // the car the handling is on, or null
        private CarController _car;
        private Rigidbody _body;
        private VariableCenterOfMass _centre;
        private string _type;
        private CarKind _kind;
        private HandlingLevel _level;

        private float _mass, _baseMass;
        private Vector3 _inertia;
        private Quaternion _inertiaTurn;
        private bool _inertiaSet;        // the tensor had to be written: the engine did not scale it with the mass
        private bool _inertiaImplicit;   // the game left the tensor to the engine (worked out from the colliders)
        private float _inertiaRatio;     // the yaw inertia after the mass changed, over before: for the self-check
        private AnimationCurve _steerCurve, _smoothCurve;
        private GroundDetectionPreset _ground;
        private ABSModuleWrapper _abs;   // the game's ABS module, put on by the handling (null: none, or the car had its own)
        private ABSModule _absTaken;     // the module last taken off, for the self-check
        private string _took;            // what the handling wrote that did not read back, before the Garage's parts went on top; null: all of it
        private float _partsBrakes = 1f, _partsMass = 1f, _partsGrip = 1f;   // the Garage's parts on top, against what lies under them
        private float _partsBrakeG, _partsUse, _partsFront, _partsRear;     // the full pedal with them: g, the most of the tyres it asks, the axles' shares

        private AnimationCurve _steerMade;              // the steering curve the slider asked for, when it moved
        private FrictionPreset _tyre;                   // the mod's own tyres, one preset for the car driven
        private GroundDetectionPreset _road;            // the mod's ground preset: the car's fallback surface, no surfaces of its own

        /// <summary>True while the handling is on a car.</summary>
        public bool On => _vehicle != null;

        /// <summary>The seconds the eased brake pedal takes from nothing to full on the car it is on.</summary>
        public float BrakeRise => _setup.BrakeRise;

        /// <summary>Why the handling last kept off a car, for the test button; null once it is on one.</summary>
        public string Refusal { get; private set; }

        /// <summary>
        /// Puts the handling on a car, the Garage's parts lifted first and laid back on top after. True if it is on; refused,
        /// said once, for a car of a kind the mod does not tune or not the player's own; anything unexpected throws.
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
            float torqueUnder = 0f;
            float massUnder = 0f;
            float gripUnder = 0f;
            try
            {
                string why = Read(car, vehicle, body, type.maxSpeed);
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
                _took = Took(vehicle, body);   // what the handling wrote, read back before the Garage's parts lie on top of it
                torqueUnder = vehicle.brakes.maxTorque;   // what lies under the Garage's parts, to tell their brakes, weight and tyres by
                massUnder = body.mass;
                gripUnder = _wheels[0].Wheel.LongitudinalFrictionGrip;
            }
            finally
            {
                LayParts(car);   // the Garage's parts back on top of whatever is under them now
            }
            BrakesForParts(vehicle, body, torqueUnder, massUnder, gripUnder);
            if (Meta.Dev.Verbose)
            {
                CarTuning.Logger?.Info(Line(tuning));
            }
            return true;
        }

        // Reads the car into the chassis the model designs for. Changes nothing. Why the car cannot be done, or null.
        private string Read(CarController car, NwhVehicle vehicle, Rigidbody body, int topKmh)
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
            FrictionPreset tyre = (z0 > z1 ? groups[0] : groups[1]).Wheels[0].wheelUAPI.FrictionPreset;
            if (tyre == null || tyre.Curve == null || !Keys(tyre.Curve, _chassis.Tyre) || !Keys(vehicle.steering.speedSensitiveSteeringCurve, _chassis.Steer))
            {
                return "its tyres or its steering are not curves of the game's kind";
            }
            _chassis.TyreD = tyre.BCDE.z;
            _chassis.TyrePeakSlip = tyre.peakSlip;
            Vector3 com = body.centerOfMass;
            _chassis.Mass = body.mass;
            _chassis.ComY = com.y;
            _chassis.ComZ = com.z;
            _chassis.TopKmh = topKmh;
            string id = car.vehicleInstance != null ? car.vehicleInstance.id : null;
            float torque = vehicle.brakes.maxTorque;
            if (id != null)
            {
                // As first met this session: before the car's wear or the Garage's brakes are on it.
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
            if (controller == null || !(b is WheelController) || controller.spring == null || controller.damper == null || controller.spring.forceCurve == null
                || !Keys(controller.spring.forceCurve, axle.Spring))
            {
                return "its wheels are not the game's own";
            }
            Vector3 pa = space.InverseTransformPoint(a.transform.position);
            Vector3 pb = space.InverseTransformPoint(b.transform.position);
            Damper damper = controller.damper;
            axle.Z = 0.5f * (pa.z + pb.z);
            axle.Track = Mathf.Abs(pa.x - pb.x);
            axle.Top = 0.5f * (pa.y + pb.y);
            axle.Radius = 0.5f * (a.Radius + b.Radius);
            axle.SpringForce = a.SpringMaxForce;
            axle.SpringLength = a.SpringMaxLength;
            axle.SideGrip = a.LateralFrictionGrip;
            axle.AlongGrip = a.LongitudinalFrictionGrip;
            axle.SideStiffness = a.LateralFrictionStiffness;
            axle.LoadRating = a.MaxLoad;
            axle.Rolling = a.RollingResistanceTorque;
            axle.AntiRoll = group.antiRollBarForce;
            axle.BrakeShare = group.brakeCoefficient;
            axle.Bump = a.DamperBumpRate;
            axle.Rebound = a.DamperReboundRate;
            axle.SlowBump = damper.slowBump;
            axle.FastBump = damper.fastBump;
            axle.BumpKnee = damper.bumpDivisionVelocity;
            axle.SlowRebound = damper.slowRebound;
            axle.FastRebound = damper.fastRebound;
            axle.ReboundKnee = damper.reboundDivisionVelocity;
            axle.Application = a.ForceApplicationPointDistance;
            axle.AntiSquat = controller.antiSquat;
            return null;
        }

        // A curve's keys into the model's own, once per car; false for weighted keys, which the model does not read as the engine does.
        private static bool Keys(AnimationCurve curve, CurveKeys into)
        {
            into.Clear();
            if (curve == null)
            {
                return false;
            }
            Keyframe[] keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                Keyframe k = keys[i];
                if (k.weightedMode != WeightedMode.None || float.IsInfinity(k.inTangent) || float.IsInfinity(k.outTangent))
                {
                    return false;
                }
                into.Add(k.time, k.value, k.inTangent, k.outTangent);
            }
            return keys.Length > 0;
        }

        // The mod's own presets and curve, made before the car records anything, so a fault here changes nothing on it.
        private void Build(NwhVehicle vehicle)
        {
            FrictionPreset game = FrontTyre(vehicle);
            // Made once and filled again for each car; destroyed in Dispose.
            if (_tyre == null)
            {
                _tyre = ScriptableObject.CreateInstance<FrictionPreset>();
            }
            _tyre.name = "RealLifeOverhaul tyres";
            _tyre.BCDE = game.BCDE;   // the game's own D: the most along, and the side force's cap
            _tyre.UpdateFrictionCurve();
            _tyre.Curve.keys = Frames(_setup.Tyre);
            _tyre.peakSlip = game.peakSlip;   // the game's own friction circle
            _steerMade = _setup.SteerOwn ? null : new AnimationCurve(Frames(_setup.Steer));
            GroundDetectionPreset ground = vehicle.groundDetection.groundDetectionPreset;
            if (_road == null)
            {
                _road = ScriptableObject.CreateInstance<GroundDetectionPreset>();
            }
            _road.name = "RealLifeOverhaul road";
            _road.surfaceMaps.Clear();   // no surface of its own: the game's terrains never put their tyres on the car
            if (ground != null && ground != _road)
            {
                _road.fallbackSurfacePreset = ground.fallbackSurfacePreset;   // the skid and road sounds the car has everywhere else
                _road.particlePrefab = ground.particlePrefab;
                _road.chunkPrefab = ground.chunkPrefab;
            }
        }

        private static FrictionPreset FrontTyre(NwhVehicle vehicle)
        {
            List<WheelGroup> groups = vehicle.powertrain.wheelGroups;
            Transform space = vehicle.transform;
            return (AxleZ(space, groups[0]) > AxleZ(space, groups[1]) ? groups[0] : groups[1]).Wheels[0].wheelUAPI.FrictionPreset;
        }

        private static Keyframe[] Frames(CurveKeys curve)
        {
            var frames = new Keyframe[curve.Count];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = new Keyframe(curve.Time(i), curve.Value(i), curve.InSlope(i), curve.OutSlope(i));
            }
            return frames;
        }

        // Everything the handling will change, as the car has it now.
        private void Record(CarController car, NwhVehicle vehicle, Rigidbody body)
        {
            _car = car;
            _body = body;
            _mass = body.mass;
            _inertia = body.inertiaTensor;
            _inertiaTurn = body.inertiaTensorRotation;
            _centre = vehicle.GetComponent<VariableCenterOfMass>();
            _inertiaImplicit = _centre == null || _centre.useDefaultInertia;
            _baseMass = _centre != null ? _centre.baseMass : 0f;
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
                    _wheels.Add(new WheelStock
                    {
                        Wheel = wheel, Controller = controller, Front = front,
                        Force = wheel.SpringMaxForce, Length = wheel.SpringMaxLength, Bump = wheel.DamperBumpRate, Rebound = wheel.DamperReboundRate,
                        Application = wheel.ForceApplicationPointDistance, AntiSquat = controller.antiSquat, Rating = wheel.MaxLoad,
                        Rolling = wheel.RollingResistanceTorque, Tyre = wheel.FrictionPreset
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
            }
            // A heavier car turns as heavily: the engine scales a tensor it works out itself; one it does not is scaled here.
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
                wheel.SpringMaxForce = axle.SpringForce;
                wheel.SpringMaxLength = axle.SpringLength;
                wheel.DamperBumpRate = axle.Bump;
                wheel.DamperReboundRate = axle.Rebound;
                wheel.ForceApplicationPointDistance = axle.Application;
                stock.Controller.antiSquat = axle.AntiSquat;
                wheel.MaxLoad = axle.LoadRating;
                wheel.RollingResistanceTorque = axle.Rolling;
                wheel.FrictionPreset = _tyre;
            }
            for (int g = 0; g < _groups.Count; g++)
            {
                GroupStock stock = _groups[g];
                AxleSetup axle = stock.Front ? _setup.Front : _setup.Rear;
                stock.Group.antiRollBarForce = axle.AntiRoll;
                stock.Group.brakeCoefficient = axle.BrakeShare;
            }
            if (_steerMade != null)
            {
                vehicle.steering.speedSensitiveSteeringCurve = _steerMade;
            }
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

        // What PutOn wrote, read back before the Garage's parts go on top: the first value that did not take, or null.
        private string Took(NwhVehicle vehicle, Rigidbody body)
        {
            if (Mathf.Abs(body.mass - _setup.Mass) > 0.01f)
            {
                return "the mass read " + Num(body.mass) + " kg";
            }
            for (int i = 0; i < _wheels.Count; i++)
            {
                WheelStock stock = _wheels[i];
                AxleSetup axle = stock.Front ? _setup.Front : _setup.Rear;
                WheelUAPI wheel = stock.Wheel;
                if (wheel.SpringMaxForce != axle.SpringForce || wheel.SpringMaxLength != axle.SpringLength)
                {
                    return "a spring read " + Num(wheel.SpringMaxForce) + " N over " + Num(wheel.SpringMaxLength) + " m";
                }
                if (wheel.DamperBumpRate != axle.Bump || wheel.DamperReboundRate != axle.Rebound || wheel.ForceApplicationPointDistance != axle.Application
                    || stock.Controller.antiSquat != axle.AntiSquat || wheel.MaxLoad != axle.LoadRating)
                {
                    return "a damper, force point or load rating did not take";
                }
                if (wheel.FrictionPreset != _tyre)
                {
                    return "a wheel's tyres are " + (wheel.FrictionPreset != null ? wheel.FrictionPreset.name : "none");
                }
            }
            for (int g = 0; g < _groups.Count; g++)
            {
                if (_groups[g].Group.antiRollBarForce != (_groups[g].Front ? _setup.Front : _setup.Rear).AntiRoll)
                {
                    return "an anti-roll bar did not take";
                }
            }
            float knee = _tyre.Curve.Evaluate(_setup.Knee);
            if (Mathf.Abs(knee - _setup.Tyre.Evaluate(_setup.Knee)) > 0.01f || Mathf.Abs(_tyre.Curve.Evaluate(_setup.Knee * 0.5f) - _setup.Tyre.Evaluate(_setup.Knee * 0.5f)) > 0.01f)
            {
                return "the tyres' curve reads " + Num(knee) + " at its knee";
            }
            AnimationCurve steering = vehicle.steering.speedSensitiveSteeringCurve;
            if (steering != (_steerMade ?? _steerCurve) || Mathf.Abs(steering.Evaluate(0.3f) - _setup.Steer.Evaluate(0.3f)) > 0.01f)
            {
                return "the steering's curve did not take";
            }
            if (vehicle.groundDetection.groundDetectionPreset != _road)
            {
                return "the ground's preset did not take";
            }
            return null;
        }

        // Each axle's brake share for the Garage's brakes, weight and tyres on top, never past what the tyres take; TakeOff
        // writes it back with the rest.
        private void BrakesForParts(NwhVehicle vehicle, Rigidbody body, float torqueUnder, float massUnder, float gripUnder)
        {
            _partsBrakes = torqueUnder > 1f ? vehicle.brakes.maxTorque / torqueUnder : 1f;
            _partsMass = massUnder > 1f ? body.mass / massUnder : 1f;
            _partsGrip = gripUnder > 0.01f ? _wheels[0].Wheel.LongitudinalFrictionGrip / gripUnder : 1f;
            _partsBrakeG = HandlingModel.PartsBrakes(_chassis, _setup, _partsBrakes, _partsMass, _partsGrip, out _partsFront, out _partsRear, out _partsUse);
            for (int g = 0; g < _groups.Count; g++)
            {
                GroupStock stock = _groups[g];
                stock.Group.brakeCoefficient = stock.Front ? _partsFront : _partsRear;
            }
        }

        /// <summary>The car exactly as it was before <see cref="Apply"/>, the Garage's parts lifted first and laid back after. Safe with nothing on.</summary>
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
                    wheel.DamperBumpRate = stock.Bump;
                    wheel.DamperReboundRate = stock.Rebound;
                    wheel.ForceApplicationPointDistance = stock.Application;
                    controller.antiSquat = stock.AntiSquat;
                    wheel.MaxLoad = stock.Rating;
                    wheel.RollingResistanceTorque = stock.Rolling;
                    wheel.FrictionPreset = stock.Tyre;
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
            if (_tyre != null)
            {
                UnityEngine.Object.Destroy(_tyre);
                _tyre = null;
            }
            if (_road != null)
            {
                UnityEngine.Object.Destroy(_road);
                _road = null;
            }
        }

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

        private void Refused(string type, string why)
        {
            Refusal = why;
            if (_refused.Add(type ?? string.Empty))
            {
                CarTuning.Logger?.Info("Handling: " + type + " keeps the game's own handling (" + why + ").");
            }
        }

        /// <summary>For the test button: the car's figures for the notice, and the self-check line in Player.log. False with the handling on no car.</summary>
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
            data["grip"] = Num(_setup.Hold);
            data["brakes"] = Num(_partsBrakeG);
            Meta.Dev.Always(CarTuning.Logger, Line(tuning));
            return true;
        }

        /// <summary>For the custom shop: the metres from 100 km/h with a Garage build on top (factors, 1 for none); -1 without the handling.</summary>
        public float ShopStop(CarController car, float brakes, float mass, float grip)
        {
            if (_vehicle == null || car == null || car.vehicleController != _vehicle || _vehicle.brakes == null)
            {
                return -1f;
            }
            return HandlingModel.ShopStop(_chassis, _setup, brakes, mass, grip, _vehicle.brakes.actuationTime);
        }

        // The self-check line, getting in: what was put on against the game's own for this car, then PASS, or why not.
        private string Line(in HandlingTuning tuning)
        {
            AxleSetup f = _setup.Front;
            AxleSetup r = _setup.Rear;
            AxleData gf = _chassis.Front;
            AxleData gr = _chassis.Rear;
            float zeta = HandlingModel.Bounce(gf, f.SpringForce, f.SpringLength, f.Bump, f.Rebound, f.Load / HandlingModel.G, out int swings, out _);
            float kerb = HandlingModel.KerbLeft(gf, f.SpringForce, f.SpringLength, f.Bump, f.Rebound, f.Load / HandlingModel.G, HandlingModel.Kerb);
            float gameKerb = HandlingModel.KerbLeft(gf, gf.SpringForce, gf.SpringLength, gf.Bump, gf.Rebound, f.Load / (_setup.Mass / _chassis.Mass) / HandlingModel.G, HandlingModel.Kerb);

            string why = _took;
            if (why == null && _abs != null && !_abs.module.IsActive)
            {
                why = "the ABS module is not running";
            }
            if (why == null && Mathf.Abs(_inertiaRatio - _setup.Mass / _mass) > 0.03f * _setup.Mass / _mass)
            {
                why = "the inertia came to " + Num(_inertiaRatio) + " times the game's for " + Num(_setup.Mass / _mass) + " times the mass";
            }
            if (why == null && _setup.TipG < HandlingModel.TipMargin * _setup.Hold * 0.995f)
            {
                why = "it would roll over at " + Num(_setup.TipG) + " g where its tyres hold " + Num(_setup.Hold);
            }
            if (why == null && _partsUse > HandlingModel.MostPeak + 0.005f)
            {
                why = "the full pedal asks " + Num(_partsUse) + " of what the tyres hold";
            }
            if (why == null && Mathf.Abs(_groups[0].Group.brakeCoefficient - (_groups[0].Front ? _partsFront : _partsRear)) > 1e-4f)
            {
                why = "the brakes' shares did not take";
            }

            float lockDegrees = _chassis.SteerLock;
            _line.Clear();
            _line.Append("Handling check (getting in): ").Append(_type).Append(" (").Append(_kind).Append(", ").Append(_level).Append(", sliders ")
                .Append(tuning.Softness).Append('/').Append(tuning.Bounce).Append('/').Append(tuning.Roll).Append('/').Append(tuning.Grip).Append('/')
                .Append(tuning.Steering).Append('/').Append(tuning.Brakes).Append(" %): ")
                .Append(Num(_mass)).Append(" -> ").Append(Num(_setup.Mass)).Append(" kg, centre of mass ").Append(Num(_setup.ComHeight)).Append(" m up, the game's; springs ")
                .Append(Num(f.GameRate)).Append(" -> ").Append(Num(f.Rate)).Append(" N/m front, ").Append(Num(r.GameRate)).Append(" -> ").Append(Num(r.Rate))
                .Append(" rear, ride ").Append(Num(f.GameFrequency)).Append(" -> ").Append(Num(f.Frequency)).Append(" Hz; the body's swings die away at a damping ratio of ")
                .Append(Num(f.GameZeta)).Append(" -> ").Append(Num(zeta)).Append(" (").Append(swings).Append(" swings you can see), a ").Append(Num(HandlingModel.Kerb * 100f))
                .Append(" cm kerb leaves ").Append(Num(kerb * 100f)).Append(" cm of travel (the game's ").Append(Num(gameKerb * 100f)).Append("); roll ")
                .Append(Num(_setup.GameRollPerG)).Append(" -> ").Append(Num(_setup.RollPerG)).Append(" degrees per g (force points ").Append(Num(_setup.GameRollCentre))
                .Append(" -> ").Append(Num(_setup.RollCentre)).Append(" m up")
                .Append(_setup.Raised > 0.0005f ? ", raised " + Num(_setup.Raised * 100f) + " cm so it slides before it tips" : string.Empty)
                .Append(", bars ").Append(Num(gf.AntiRoll)).Append('/').Append(Num(gr.AntiRoll)).Append(" -> ").Append(Num(f.AntiRoll)).Append('/').Append(Num(r.AntiRoll))
                .Append(" N/m), dive ").Append(Num(_setup.GameDivePerG)).Append(" -> ").Append(Num(_setup.DivePerG)).Append(" degrees per g; rolls over at ")
                .Append(Num(_setup.TipG)).Append(" g (the game's ").Append(Num(_setup.GameTipG)).Append(") where its tyres hold ").Append(Num(_setup.Hold))
                .Append(" g (the game's ").Append(Num(_setup.GameHold)).Append(" sideways on); tyres the game's own curve to ").Append(Num(_setup.KneeDegrees))
                .Append(" degrees of slip, flat past it, where its own turn with the key held at ").Append(Num(_setup.CityKmh)).Append(" km/h (")
                .Append(Num(_setup.CityG)).Append(" g) needs ").Append(Num(_setup.CityCurve)).Append(" of ").Append(Num(_setup.Knee)).Append("; steering ")
                .Append(_setup.SteerOwn ? "the game's own, " : "moved, ").Append(Num(HandlingModel.SteerAt(_setup, lockDegrees, 1.4f))).Append('/')
                .Append(Num(HandlingModel.SteerAt(_setup, lockDegrees, TownKmh / 3.6f))).Append('/').Append(Num(HandlingModel.SteerAt(_setup, lockDegrees, 100f / 3.6f)))
                .Append(" degrees at 5/50/100 km/h; brakes ").Append(Num(_partsBrakeG)).Append(" g at the full pedal against the game's ").Append(Num(_setup.GameBrakeG))
                .Append(" (built for ").Append(Num(_setup.BrakeTarget))
                .Append(_partsBrakes != 1f || _partsMass != 1f || _partsGrip != 1f
                    ? ", with the custom shop's brakes " + Num(_partsBrakes) + ", weight " + Num(_partsMass) + " and tyres " + Num(_partsGrip) : string.Empty)
                .Append("), asking at most ").Append(Num(_partsUse)).Append(" of the tyres as the body dives (front ").Append(Num(_setup.FrontLag)).Append(", rear ")
                .Append(Num(_setup.RearLag)).Append(" times their settled share at their worst moment), built in ").Append(Num(_setup.BrakeRise))
                .Append(" s, shares ").Append(Num(_partsFront)).Append('/').Append(Num(_partsRear)).Append(" against the game's ").Append(Num(gf.BrakeShare)).Append('/')
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
                Same(_body.mass == _mass, "the mass");
            }
            if (_centre != null)
            {
                Same(_centre.baseMass == _baseMass, "the centre of mass's own weight");
            }
            for (int i = 0; i < _wheels.Count; i++)
            {
                WheelStock s = _wheels[i];
                if (s.Wheel == null || s.Controller == null)
                {
                    continue;
                }
                Same(s.Wheel.SpringMaxForce == s.Force && s.Wheel.SpringMaxLength == s.Length, "a spring");
                Same(s.Wheel.DamperBumpRate == s.Bump && s.Wheel.DamperReboundRate == s.Rebound, "a damper");
                Same(s.Wheel.ForceApplicationPointDistance == s.Application && s.Controller.antiSquat == s.AntiSquat, "a force point or anti-squat");
                Same(s.Wheel.MaxLoad == s.Rating && s.Wheel.RollingResistanceTorque == s.Rolling, "a tyre's rating or rolling resistance");
                Same(s.Wheel.FrictionPreset == s.Tyre, "a tyre");
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
