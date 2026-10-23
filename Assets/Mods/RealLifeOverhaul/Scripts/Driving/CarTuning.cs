using System;
using System.Collections.Generic;
using BAModAPI;
using Localizor;
using NWH.VehiclePhysics2.Modules.SpeedLimiter;
using NWH.VehiclePhysics2.Powertrain;
using UI.Notification;
using UnityEngine;
using UnityEngine.Events;
using NwhVehicle = NWH.VehiclePhysics2.VehicleController;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Gives the car the player drives the acceleration and top speed of its kind (<see cref="CarCatalogue"/>): the game's
    /// cars share one gearbox and a limiter, so <see cref="CarModel"/> works out the power, gearing and drag that meet the
    /// target, and the throttle key eases the pedal. It carries the handling too (<see cref="CarHandling"/>). Everything is
    /// recorded first and put back exactly on every way out; anything unexpected restores the car and leaves cars alone
    /// from then on. Per frame: one reference and two numbers compared.
    /// </summary>
    public class CarTuning : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("driving", "CarTuning", typeof(CarTuning));

        public const float BrakeOnset = 0.25f;    // seconds for the brakes to come on fully with the eased pedal
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
        private readonly CarHandling _handling = new CarHandling();

        private NwhVehicle _seen;        // the vehicle last looked at, tuned or not
        private int _options = -1;       // the options it was looked at with
        private long _handled = -1;      // and the handling's level and sliders
        private NwhVehicle _tuned;       // the vehicle that carries the mod's numbers, if any
        private Stock _stock;
        private CarDrag _drag;
        private UnityAction _easePedal;
        private NwhVehicle _pedalOn;     // the vehicle whose input the pedal listener is on
        private bool _easeThrottle;
        private float _pedal;
        private float _brakePedal;
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
            _easePedal = Guard.Listener(EasePedal);   // each physics step of the vehicle's input: a fault never reaches the vehicle's own
        }

        private static readonly int HotUpdate = HotSpots.Slot("Driving: CarTuning.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
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
            var gm = InstanceBehavior<GameManager>.Instance;
            var car = gm != null ? gm.selectedVehicle as CarController : null;
            NwhVehicle vehicle = car != null ? car.vehicleController : null;
            int options = !Settings.CarTuning
                ? 0
                : 1 | (Settings.SmoothThrottle ? 2 : 0) | (Mathf.Clamp(Settings.CarTopSpeed, 70, 150) << 2) | (Mathf.Clamp(Settings.CarAcceleration, 70, 150) << 10) | (GarageTuning.Stamp << 18);
            long handled = HandlingStamp();
            if (vehicle == _seen && options == _options && handled == _handled)
            {
                return;   // same car or none, same options
            }

            Restore();
            _seen = vehicle;
            _options = options;
            _handled = handled;
            if (vehicle == null || (options == 0 && handled == 0) || _failed)
            {
                return;
            }
            try
            {
                if (handled != 0)
                {
                    _handling.Apply(car, vehicle, HandlingRules.Level(Settings.Handling), Tuning());   // first: the power below is found for the car's new weight
                }
                if (options != 0)
                {
                    Apply(car, vehicle);
                }
                Ease(vehicle);
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
            _handling.Dispose();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // The handling's level and its sliders as one number; 0 with the handling off.
        private static long HandlingStamp()
        {
            HandlingLevel level = HandlingRules.Level(Settings.Handling);
            return level == HandlingLevel.Off ? 0L : (long)level + (Tuning().Stamp << 4);
        }

        private static HandlingTuning Tuning()
        {
            return new HandlingTuning(Settings.HandlingSoftness, Settings.HandlingBounce, Settings.HandlingRoll, Settings.HandlingGrip, Settings.HandlingSteering,
                Settings.HandlingBrakes);
        }

        /// <summary>A tuning slider moved: the figures written to Player.log, and the car being driven takes them at once.</summary>
        public static void TuningChanged()
        {
            HandlingTuning t = Tuning();
            Meta.Dev.Always(Logger, "Car handling tuning: softness " + t.Softness + " %, bounce " + t.Bounce + " %, roll " + t.Roll + " %, grip " + t.Grip + " %, steering "
                                    + t.Steering + " %, brakes " + t.Brakes + " %, at the level " + HandlingRules.Level(Settings.Handling) + ".");
        }

        /// <summary>For the custom shop: the car's stop from 100 km/h with a Garage build on top, by its handling; -1 without it.</summary>
        public static float HandlingStop(CarController car, float brakes, float mass, float grip)
        {
            CarTuning tuning = Instance;
            return tuning != null && !tuning._failed ? tuning._handling.ShopStop(car, brakes, mass, grip) : -1f;
        }

        /// <summary>The driving feature's clean-up: the car being driven as the game made it, until it or an option changes.</summary>
        public static void PutBack()
        {
            Instance?.Restore();
        }

        /// <summary>The test button: what the car being driven has from the handling, in a notice and in full in Player.log.</summary>
        public static void TestHandling()
        {
            CarTuning tuning = Instance;
            var gm = InstanceBehavior<GameManager>.Instance;
            var car = gm != null ? gm.selectedVehicle as CarController : null;
            if (tuning == null || car == null || car.vehicleController == null)
            {
                TestRefusals.Tell(Logger, "Car handling", TestRefusal.NotDriving);
                return;
            }
            if (HandlingRules.Level(Settings.Handling) == HandlingLevel.Off)
            {
                TestRefusals.Tell(Logger, "Car handling", TestRefusal.FeatureOff);
                return;
            }
            string name = car.vehicleType != null ? car.vehicleType.vehicleTypeName.GetLocalization() : car.name;
            var data = new Dictionary<string, string>(7) { { "car", name } };
            if (tuning._failed || !tuning._handling.Tell(data, Tuning()))
            {
                string why = tuning._failed ? "car tuning stood down after a fault" : tuning._handling.Refusal ?? "not a car the handling is for";
                Meta.Dev.Always(Logger, "Car handling test: " + name + " keeps the game's own handling (" + why + ").");
                Notifications.Show(NotificationType.Info, "reallife_handling_test_none", data, 8f);
                return;
            }
            Notifications.Show(NotificationType.Success, "reallife_handling_test_said", data, 14f);
        }

        // The listener on the car's input that eases its pedals: the throttle with smooth throttle on a tuned car, the brake with the handling.
        private void Ease(NwhVehicle vehicle)
        {
            _easeThrottle = _tuned == vehicle && Settings.SmoothThrottle;
            if (!_easeThrottle && !_handling.On)
            {
                return;
            }
            _pedal = 0f;
            _brakePedal = 0f;
            _pedalOn = vehicle;
            vehicle.input.inputModifyCallback.AddListener(_easePedal);
        }

        private void Apply(CarController car, NwhVehicle vehicle)
        {
            Vehicles.VehicleTypes.VehicleType type = car.vehicleType;
            Rigidbody body = vehicle.vehicleRigidbody;
            if (type == null || body == null || vehicle.powertrain == null)
            {
                return;
            }
            string typeName = type.vehicleTypeName;
            if (!GoingPublic.MineVehicle(car))
            {
                // The handling's rule (CarHandling.Apply): another Going Public player's car keeps the figures its owner's
                // machine gives it, whoever drives it here.
                Note(typeName, "Car tuning: " + typeName + " is left as the game made it (it is another player's car, Going Public).");
                return;
            }
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
            GarageModifiers garage = GarageTuning.Claim(car.vehicleInstance);   // what the custom shop fitted
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
                Note(typeName, FormattableString.Invariant(
                    $"Car tuning: {typeName} is left as the game made it (the sums came out at {_setup.PowerKw:F0} kW, final drive {finalDrive:F2}, drag {_setup.Drag:F3})."));
                return;
            }

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
            }
            TargetSeconds = seconds;
            TargetTopKmh = topKmh;

            float firstGearKmh = CarModel.TopGearRevs * _physics.RevLimitRpm * 0.10471976f * _physics.WheelRadius / _setup.Ratios[0] * 3.6f;
            Note(typeName, FormattableString.Invariant($"Car tuning: {typeName} ({spec.Kind}): target 0-100 {seconds:F1} s, top {topKmh:F0} km/h; ")
                + FormattableString.Invariant($"power {_stock.Power:F0} -> {_setup.PowerKw:F0} kW, final drive {_stock.FinalDrive:F2} -> {finalDrive:F2}, drag {_setup.Drag:F3}; ")
                + FormattableString.Invariant(
                    $"first gear to {firstGearKmh:F0} km/h, shifts at {_setup.UpshiftRpm:F0} to {_setup.UpshiftRpm * (1f + _setup.ShiftIntensity):F0} rpm in {_setup.ShiftSeconds:F2} s, ")
                + FormattableString.Invariant($"limiter {_stock.SpeedLimit:F0} -> off; {body.mass:F0} kg, {_driven.Count} of {vehicle.powertrain.wheels.Count} wheels driven, ")
                + FormattableString.Invariant($"tyres hold {_physics.TractionForce / (body.mass * 9.81f):F1} g; ")
                + FormattableString.Invariant($"expect 0-50 {run.To50:F1} s, 0-100 {run.To100:F1} s, {run.KmhAfter20:F0} km/h after 20 s, top {run.TopKmh:F0} km/h")
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

        // Run by the car's input handler each frame, between the keys and the drivetrain. In reverse the game swaps the
        // pedals: the one that drives is eased as the throttle, the one that brakes as the brake.
        private void EasePedal()
        {
            NwhVehicle vehicle = _pedalOn;
            if (vehicle == null)
            {
                return;
            }
            var input = vehicle.input;
            bool reversing = input.IsInputSwapped;
            if (_easeThrottle)
            {
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
            if (_handling.On)
            {
                _brakePedal = HandlingRules.BrakePedal(_brakePedal, reversing ? input.Throttle : input.Brakes, vehicle.deltaTime, _handling.BrakeRise);
                if (reversing)
                {
                    input.Throttle = _brakePedal;
                }
                else
                {
                    input.Brakes = _brakePedal;
                }
            }
        }

        private void Restore()
        {
            NwhVehicle vehicle = _tuned;
            _tuned = null;
            if (_drag != null)
            {
                _drag.End();
            }
            NwhVehicle pedalOn = _pedalOn;
            _pedalOn = null;
            _easeThrottle = false;
            if (pedalOn != null)
            {
                try
                {
                    pedalOn.input.inputModifyCallback.RemoveListener(_easePedal);
                }
                catch (Exception e)
                {
                    Logger?.Warn("Car tuning: the eased pedals could not be taken off the car's input. " + e);
                }
            }
            if (vehicle != null)
            {
                PutBackTuning(vehicle);   // or the car is gone, and its numbers with it
            }
            try
            {
                _handling.Restore();   // after the numbers worked out on top of it
            }
            catch (Exception e)
            {
                _failed = true;
                Logger?.Warn("Car tuning: the car's handling could not be put back completely; cars are left alone from now on. " + e);
            }
        }

        private void PutBackTuning(NwhVehicle vehicle)
        {
            try
            {
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
