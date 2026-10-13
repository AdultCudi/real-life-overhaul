using System;
using System.Collections.Generic;
using BAModAPI;
using NWH.VehiclePhysics2.Modules.SpeedLimiter;
using NWH.VehiclePhysics2.Powertrain;
using UnityEngine;
using UnityEngine.Events;
using NwhVehicle = NWH.VehiclePhysics2.VehicleController;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Gives the car the player is driving the acceleration and top speed of its kind. The game's cars all
    /// weigh the same, share one gearbox and are stopped by a speed limiter at 50 to 100 km/h; here each
    /// vehicle type has a target (0-100 km/h time and top speed, <see cref="CarCatalogue"/>), and
    /// <see cref="CarModel"/> works out from the car's own mass, wheels and power curve the engine power,
    /// gearing and air drag that meet it. Top speed is where power runs out against drag, so acceleration
    /// tapers the way a real car's does. The throttle key eases the pedal down instead of flooring it.
    ///
    /// Only the car being driven is touched (never traffic). Everything changed is recorded first and put
    /// back exactly when the player gets out, changes car, switches the option off, or the mod unloads.
    /// Anything unexpected restores the car, says so once in the log and leaves cars alone from then on.
    /// Per frame this compares one reference and one number; the working out runs once per car.
    /// </summary>
    public class CarTuning : MonoBehaviour
    {
        private const float BrakeOnset = 0.25f;   // seconds for the brakes to come on fully with the eased pedal
        private const float MaxTopKmh = 350f;     // whatever the slider says: beyond this a car covers two metres a physics step

        public static IModLogger Logger;

        public static CarTuning Instance { get; private set; }

        // What the car was like before, to put back.
        private struct Stock
        {
            public float Power;
            public float FinalDrive;
            public float[] Gears;
            public float UpshiftRpm;
            public float DownshiftRpm;
            public bool VariableShift;
            public float ShiftIntensity;
            public float ShiftSeconds;
            public float BrakeActuation;
            public SpeedLimiterModule Limiter;
            public float SpeedLimit;
        }

        private readonly CarPhysics _physics = new CarPhysics();
        private readonly CarSetup _setup = new CarSetup();
        private readonly List<WheelComponent> _driven = new List<WheelComponent>(4);
        private readonly Dictionary<string, int> _logged = new Dictionary<string, int>();   // vehicle type -> options last logged for it

        private NwhVehicle _seen;        // the vehicle last looked at, tuned or not
        private int _options = -1;       // the options it was looked at with
        private NwhVehicle _tuned;       // the vehicle that carries our numbers, if any
        private Stock _stock;
        private CarDrag _drag;
        private UnityAction _easePedal;
        private bool _easing;
        private float _pedal;
        private bool _failed;

        /// <summary>The car being driven if it carries the mod's numbers, else null.</summary>
        public NwhVehicle Tuned => _tuned;

        /// <summary>What the tuned car is meant to do, for the measuring button.</summary>
        public float TargetSeconds { get; private set; }

        public float TargetTopKmh { get; private set; }

        private void Awake()
        {
            Instance = this;
            _drag = gameObject.AddComponent<CarDrag>();
            _drag.enabled = false;
            _easePedal = EasePedal;
        }

        private static readonly int HotUpdate = HotSpots.Slot("Driving: CarTuning.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            var gm = InstanceBehavior<GameManager>.Instance;
            var car = gm != null ? gm.selectedVehicle as CarController : null;
            NwhVehicle vehicle = car != null ? car.vehicleController : null;
            int options = !Settings.CarTuning
                ? 0
                : 1 | (Settings.SmoothThrottle ? 2 : 0) | (Mathf.Clamp(Settings.CarTopSpeed, 70, 150) << 2) | (Mathf.Clamp(Settings.CarAcceleration, 70, 150) << 10) | (GarageTuning.Stamp << 18);
            if (vehicle == _seen && options == _options)
            {
                return;   // same car (or none), same options: nothing to do
            }

            Restore();
            _seen = vehicle;
            _options = options;
            if (vehicle == null || options == 0 || _failed)
            {
                return;
            }
            try
            {
                Apply(car, vehicle);
            }
            catch (Exception e)
            {
                _failed = true;
                Restore();
                Logger?.Warn("Car tuning: something about this car was not as expected; it is back as the game made it and cars are left alone from now on. " + e);
            }
        }

        private void OnDestroy()
        {
            Restore();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ---------------------------------------------------------------- applying

        private void Apply(CarController car, NwhVehicle vehicle)
        {
            Vehicles.VehicleTypes.VehicleType type = car.vehicleType;
            Rigidbody body = vehicle.vehicleRigidbody;
            if (type == null || body == null || vehicle.powertrain == null)
            {
                return;
            }
            string typeName = type.vehicleTypeName;
            var traits = new CarTraits(type.IsMotorVehicle, type.spawnInPlayerObject, type.maxSpeed, type.maxCargoCapacity, type.isLuxuryCar, body.mass);
            CarSpec spec = CarCatalogue.For(typeName, traits);
            if (spec.Kind == CarKind.Untouched)
            {
                return;
            }
            string refusal = Read(vehicle, body, out int firstForward);
            if (refusal != null)
            {
                Note(typeName, "Car tuning: " + typeName + " is left as the game made it (" + refusal + ").");
                return;
            }

            float seconds = spec.Seconds * 100f / Mathf.Clamp(Settings.CarAcceleration, 70, 150);
            float topKmh = Mathf.Min(spec.TopKmh * Mathf.Clamp(Settings.CarTopSpeed, 70, 150) / 100f, MaxTopKmh);
            GarageModifiers garage = GarageTuning.Claim(car.vehicleInstance);   // what the custom shop fitted (Scripts/Garage)
            seconds /= garage.Acceleration;
            topKmh = Mathf.Min(topKmh * garage.TopSpeed, MaxTopKmh);
            bool eased = Settings.SmoothThrottle;
            if (!CarModel.Solve(_physics, spec.Kind, seconds, topKmh, eased, _setup, out CarRun run))
            {
                Note(typeName, "Car tuning: " + typeName + " is left as the game made it (no engine power gets it to 100 km/h).");
                return;
            }

            EngineComponent engine = vehicle.powertrain.engine;
            TransmissionComponent box = vehicle.powertrain.transmission;
            float finalDrive = _setup.Ratios[_setup.Ratios.Length - 1] / box.gears[box.gears.Count - 1];   // top gear keeps its own ratio
            if (!(_setup.PowerKw >= 5f) || !(_setup.PowerKw <= body.mass) || !(finalDrive > 0.2f) || !(finalDrive < 60f) || !(_setup.Drag >= 0f) || !(_setup.Drag < 20f))
            {
                Note(typeName, $"Car tuning: {typeName} is left as the game made it (the sums came out at {_setup.PowerKw:F0} kW, final drive {finalDrive:F2}, drag {_setup.Drag:F3}).");
                return;
            }

            // Record, then change.
            var limiter = vehicle.GetComponent<SpeedLimiterModuleWrapper>();
            _stock = new Stock
            {
                Power = engine.maxPower,
                FinalDrive = box.finalGearRatio,
                Gears = box.gears.ToArray(),
                UpshiftRpm = box.UpshiftRPM,
                DownshiftRpm = box.DownshiftRPM,
                VariableShift = box.variableShiftPoint,
                ShiftIntensity = box.variableShiftIntensity,
                ShiftSeconds = box.shiftDuration,
                BrakeActuation = vehicle.brakes.actuationTime,
                Limiter = limiter != null ? limiter.module : null,
                SpeedLimit = limiter != null && limiter.module != null ? limiter.module.speedLimit : 0f
            };
            _tuned = vehicle;

            engine.maxPower = _setup.PowerKw;
            engine.UpdatePeakPowerAndTorque();
            box.finalGearRatio = finalDrive;
            for (int i = 0; i < box.gears.Count; i++)
            {
                // Forward gears take the new ratios; reverse keeps the whole ratio it had.
                box.gears[i] = i >= firstForward
                    ? _setup.Ratios[i - firstForward] / finalDrive
                    : _stock.Gears[i] * _stock.FinalDrive / finalDrive;
            }
            box.UpshiftRPM = _setup.UpshiftRpm;
            box.DownshiftRPM = _setup.DownshiftRpm;
            box.variableShiftPoint = true;
            box.variableShiftIntensity = _setup.ShiftIntensity;
            box.shiftDuration = _setup.ShiftSeconds;
            if (_stock.Limiter != null)
            {
                _stock.Limiter.speedLimit = 0f;   // 0 is the limiter's own "no limit": drag ends the speed now
            }
            _drag.Begin(body, _setup.Drag);
            if (eased)
            {
                vehicle.brakes.actuationTime = Mathf.Max(_stock.BrakeActuation, BrakeOnset);
                _pedal = 0f;
                vehicle.input.inputModifyCallback.AddListener(_easePedal);
                _easing = true;
            }
            TargetSeconds = seconds;
            TargetTopKmh = topKmh;

            float firstGearKmh = CarModel.TopGearRevs * _physics.RevLimitRpm * 0.10471976f * _physics.WheelRadius / _setup.Ratios[0] * 3.6f;
            Note(typeName, $"Car tuning: {typeName} ({spec.Kind}): target 0-100 {seconds:F1} s, top {topKmh:F0} km/h; "
                + $"power {_stock.Power:F0} -> {_setup.PowerKw:F0} kW, final drive {_stock.FinalDrive:F2} -> {finalDrive:F2}, drag {_setup.Drag:F3}; "
                + $"first gear to {firstGearKmh:F0} km/h, shifts at {_setup.UpshiftRpm:F0} to {_setup.UpshiftRpm * (1f + _setup.ShiftIntensity):F0} rpm in {_setup.ShiftSeconds:F2} s, "
                + $"limiter {_stock.SpeedLimit:F0} -> off; {body.mass:F0} kg, {_driven.Count} of {vehicle.powertrain.wheels.Count} wheels driven, tyres hold {_physics.TractionForce / (body.mass * 9.81f):F1} g; "
                + $"expect 0-50 {run.To50:F1} s, 0-100 {run.To100:F1} s, {run.KmhAfter20:F0} km/h after 20 s, top {run.TopKmh:F0} km/h"
                + (_setup.TyreLimited ? " (the tyres cannot put down what the target time needs)" : string.Empty) + ".");
        }

        /// <summary>Reads the car into <see cref="_physics"/>. Returns why it cannot be tuned, or null.</summary>
        private string Read(NwhVehicle vehicle, Rigidbody body, out int firstForward)
        {
            firstForward = 0;
            Powertrain train = vehicle.powertrain;
            EngineComponent engine = train.engine;
            TransmissionComponent box = train.transmission;
            ClutchComponent clutch = train.clutch;
            if (engine == null || box == null || clutch == null || vehicle.brakes == null || vehicle.input == null || train.wheels == null || train.wheels.Count == 0)
            {
                return "part of its drivetrain is missing";
            }
            if (engine.engineType != EngineComponent.EngineType.ICE || box.transmissionType != TransmissionComponent.TransmissionShiftType.Automatic)
            {
                return "not a combustion engine with an automatic gearbox";
            }
            if (engine.powerCurve == null || engine.powerCurve.length < 2 || !(engine.revLimiterRPM > engine.idleRPM) || !(engine.idleRPM > 0f) || !(body.mass > 1f))
            {
                return "its engine data is incomplete";
            }

            // The gear list must be reverse gears, neutral, forward gears, as the game's are.
            List<float> gears = box.gears;
            if (gears == null)
            {
                return "it has no gears";
            }
            while (firstForward < gears.Count && gears[firstForward] <= 0f)
            {
                firstForward++;
            }
            int forward = gears.Count - firstForward;
            for (int i = firstForward; i < gears.Count; i++)
            {
                if (!(gears[i] > 0f))
                {
                    return "its gear list is not in the usual order";
                }
            }
            if (forward < 1 || forward > 12)
            {
                return "it has " + forward + " forward gears";
            }

            _driven.Clear();
            Driven(box.Output, 0);
            if (_driven.Count == 0)
            {
                _driven.AddRange(train.wheels);   // could not follow the drivetrain: taken as all-wheel drive
            }
            float radius = 0f;
            float traction = 0f;
            float weight = body.mass * 9.81f / train.wheels.Count;
            for (int i = 0; i < _driven.Count; i++)
            {
                NWH.Common.Vehicles.WheelUAPI wheel = _driven[i].wheelUAPI;
                if (wheel == null || !(wheel.Radius > 0.05f))
                {
                    return "a wheel is missing";
                }
                radius += wheel.Radius;
                // The wheel's own sum: what the tyre can push is its surface's peak times 1.35 times its load and grip.
                float surface = wheel.FrictionPreset != null ? wheel.FrictionPreset.BCDE.z : 1f;
                traction += surface * 1.35f * wheel.LongitudinalFrictionGrip * Mathf.Min(weight, wheel.MaxLoad > 0f ? wheel.MaxLoad : weight);
            }
            float rolling = 0f;
            float turning = 0f;
            for (int i = 0; i < train.wheels.Count; i++)
            {
                NWH.Common.Vehicles.WheelUAPI wheel = train.wheels[i].wheelUAPI;
                if (wheel == null || !(wheel.Radius > 0.05f))
                {
                    return "a wheel is missing";
                }
                rolling += wheel.RollingResistanceTorque / wheel.Radius;
                turning += 0.5f * wheel.Mass;
            }
            if (!(traction > 0f))
            {
                return "its tyres have no grip figures";
            }

            _physics.Mass = body.mass;
            _physics.WheelRadius = radius / _driven.Count;
            _physics.RollingForce = rolling;
            _physics.TractionForce = traction;
            _physics.WheelInertiaMass = turning;
            _physics.RevLimitRpm = engine.revLimiterRPM;
            _physics.IdleRpm = engine.idleRPM;
            _physics.LaunchRpm = clutch.controlType == ClutchComponent.ClutchControlType.Automatic
                ? clutch.engagementRPM + clutch.throttleEngagementOffsetRPM + clutch.engagementRange * 0.5f
                : engine.idleRPM * 1.5f;
            _physics.LaunchRpm = Mathf.Clamp(_physics.LaunchRpm, engine.idleRPM, engine.revLimiterRPM * 0.6f);
            _physics.DriveInertia = engine.inertia + clutch.inertia + box.inertia;
            _physics.EngineLoss = engine.engineLossPercent;
            _physics.Boost = engine.forcedInduction != null && engine.forcedInduction.useForcedInduction ? engine.forcedInduction.powerGainMultiplier : 1f;
            _physics.ForwardGears = forward;
            float best = 0f;
            for (int i = 0; i < CarPhysics.CurveSamples; i++)
            {
                float value = Mathf.Max(0f, engine.powerCurve.Evaluate(i / (float)(CarPhysics.CurveSamples - 1)));
                _physics.Curve[i] = value;
                best = Mathf.Max(best, value);
            }
            return best > 0.2f ? null : "its power curve is empty";
        }

        // The wheels the gearbox turns, through whatever differentials lie between.
        private void Driven(PowertrainComponent part, int depth)
        {
            if (part == null || depth > 4)
            {
                return;
            }
            if (part is WheelComponent wheel)
            {
                if (!_driven.Contains(wheel))
                {
                    _driven.Add(wheel);
                }
            }
            else if (part is DifferentialComponent differential)
            {
                Driven(differential.Output, depth + 1);
                Driven(differential.OutputB, depth + 1);
            }
        }

        // One line per vehicle type and set of options, however often the player gets in and out.
        private void Note(string typeName, string line)
        {
            string key = typeName ?? string.Empty;
            if (_logged.TryGetValue(key, out int options) && options == _options)
            {
                return;
            }
            _logged[key] = _options;
            Logger?.Info(line);
        }

        // ---------------------------------------------------------------- the eased pedal

        // Called by the car's own input handler each frame, after it has read the keys and before the
        // drivetrain sees them. In reverse the game swaps the two pedals, so the one that drives is eased.
        private void EasePedal()
        {
            NwhVehicle vehicle = _tuned;
            if (vehicle == null)
            {
                return;
            }
            var input = vehicle.input;
            bool reversing = input.IsInputSwapped;
            float asked = reversing ? input.Brakes : input.Throttle;
            if (asked > CarModel.GentleThrottle && Keys.Held(KeyAction.GentleThrottle))
            {
                asked = CarModel.GentleThrottle;   // the "gentle throttle" key: half the pedal, about a third of the power
            }
            _pedal = CarModel.Pedal(_pedal, asked, vehicle.deltaTime);
            if (reversing)
            {
                input.Brakes = _pedal;
            }
            else
            {
                input.Throttle = _pedal;
            }
        }

        // ---------------------------------------------------------------- putting back

        private void Restore()
        {
            NwhVehicle vehicle = _tuned;
            _tuned = null;
            if (_drag != null)
            {
                _drag.End();
            }
            bool easing = _easing;
            _easing = false;
            if (vehicle == null)
            {
                return;   // nothing of ours anywhere (or the car is gone, and its numbers with it)
            }
            try
            {
                if (easing)
                {
                    vehicle.input.inputModifyCallback.RemoveListener(_easePedal);
                }
                EngineComponent engine = vehicle.powertrain.engine;
                TransmissionComponent box = vehicle.powertrain.transmission;
                engine.maxPower = _stock.Power;
                engine.UpdatePeakPowerAndTorque();
                box.finalGearRatio = _stock.FinalDrive;
                for (int i = 0; i < _stock.Gears.Length && i < box.gears.Count; i++)
                {
                    box.gears[i] = _stock.Gears[i];
                }
                box.UpshiftRPM = _stock.UpshiftRpm;
                box.DownshiftRPM = _stock.DownshiftRpm;
                box.variableShiftPoint = _stock.VariableShift;
                box.variableShiftIntensity = _stock.ShiftIntensity;
                box.shiftDuration = _stock.ShiftSeconds;
                vehicle.brakes.actuationTime = _stock.BrakeActuation;
                if (_stock.Limiter != null)
                {
                    _stock.Limiter.speedLimit = _stock.SpeedLimit;
                }
            }
            catch (Exception e)
            {
                _failed = true;
                Logger?.Warn("Car tuning: a car could not be put back completely; cars are left alone from now on. " + e);
            }
        }
    }
}
