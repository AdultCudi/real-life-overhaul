using System;
using System.Collections.Generic;
using BAModAPI;
using NWH.Common.Vehicles;
using NWH.VehiclePhysics2.Modules.SpeedLimiter;
using NWH.VehiclePhysics2.Powertrain;
using UnityEngine;
using NwhVehicle = NWH.VehiclePhysics2.VehicleController;

namespace RealLifeOverhaul
{
    /// <summary>What a car does by the model, for the shop's before-and-after figures; below zero: cannot be told.</summary>
    public struct GarageFigures
    {
        public float To100;          // seconds
        public float TopKmh;         // km/h
        public float StopMetres;     // from 100 km/h
        public float PowerKw;        // the engine's peak, at full boost
        public float TorqueNm;       // the engine's peak
        public float MassKg;
        public float ShiftSeconds;   // one gear change, no drive meanwhile
        public float RideMetres;     // the height at rest against standard, + up; 0 when it cannot be told
        public float Grip;           // the tyres' dry grip against standard
        public float WetGrip;

        public static GarageFigures Unknown => new GarageFigures
        {
            To100 = -1f, TopKmh = -1f, StopMetres = -1f, PowerKw = -1f, TorqueNm = -1f, MassKg = -1f, ShiftSeconds = -1f, Grip = 1f, WetGrip = 1f
        };
    }

    /// <summary>
    /// The performance parts on the car driven: <see cref="CarModel"/>'s sums find the power, gearing and drag for the
    /// build's 0-100 km/h time and top speed. Driving/CarTuning applies them once it calls <see cref="Claim"/>; until then
    /// this writes them itself, after every Update of the frame, so car tuning has claimed a car it takes before this
    /// looks and neither records the other's values as the game's. A few comparisons a frame while driving; the sums
    /// run once per car and build.
    /// </summary>
    public sealed class GarageTuning : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("garage", "GarageTuning", typeof(GarageTuning));   // its LateUpdate and FixedUpdate (Core/Guard)

        private const float MaxTopKmh = 350f;   // km/h, as Driving/CarTuning

        public static IModLogger Logger;

        private static bool _claimed;
        private static readonly CarPhysics Physics = new CarPhysics();
        private static readonly CarSetup Setup = new CarSetup();
        private static readonly List<WheelComponent> Driven = new List<WheelComponent>(4);
        private static readonly HashSet<string> Logged = new HashSet<string>();

        private NwhVehicle _seen;
        private NwhVehicle _seenTuned;
        private int _version = -1;
        private bool _option;

        private NwhVehicle _applied;
        private EngineComponent.PowerModifier _modifier;
        private float _powerFactor = 1f;
        private bool _gearsWritten;
        private float _finalBefore, _finalWritten;
        private float[] _gearsBefore;
        private float _dragDelta;
        private Rigidbody _body;
        private SpeedLimiterModule _limiter;
        private float _limitBefore, _limitWritten;
        private TransmissionComponent _shiftBox;
        private float _shiftBefore, _shiftWritten;
        private bool _carTuning;
        private bool _failed;

        private static GarageTuning _live;   // for the figures: the gear change it wrote, to read the standard one under it

        /// <summary>The hook for Driving/CarTuning: the car's modifiers, neutral when stock or off; from then CarTuning applies them.</summary>
        public static GarageModifiers Claim(VehicleInstance vehicle)
        {
            _claimed = true;
            return Of(vehicle != null ? vehicle.id : null);
        }

        /// <summary>For CarTuning's "have the options changed" number: changes whenever any build does.</summary>
        public static int Stamp => GarageState.Version & 0x1FFF;

        /// <summary>A car's modifiers by vehicle id; neutral when the option is off or it has no build.</summary>
        public static GarageModifiers Of(string vehicleId)
        {
            GarageBuild build = Settings.Garage && Settings.GarageTuning ? GarageState.Of(vehicleId) : null;
            return build != null ? build.Modifiers() : GarageModifiers.None;
        }

        private void Awake()
        {
            _modifier = PowerFactor;
            _live = this;
        }

        // Car tuning carries the parts only while it is on: switched off later in the session, this applies them again.
        private static bool Claimed => _claimed && Settings.CarTuning;

        private float PowerFactor()
        {
            return _powerFactor;
        }

        private static readonly int HotLateUpdate = HotSpots.Slot("Garage: GarageTuning.LateUpdate");
        private void LateUpdate()
        {
            using var hotSpot = HotSpots.Time(HotLateUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults (Core/Guard)
                return;
            }
            try
            {
                OnLateUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnLateUpdate()

        {
            if (Claimed || _failed)
            {
                if (_applied != null)
                {
                    Restore();
                }
                return;
            }
            var gm = InstanceBehavior<GameManager>.Instance;
            var car = gm != null ? gm.selectedVehicle as CarController : null;
            NwhVehicle vehicle = car != null ? car.vehicleController : null;
            CarTuning tuning = CarTuning.Instance;
            NwhVehicle tuned = tuning != null ? tuning.Tuned : null;
            bool option = Settings.Garage && Settings.GarageTuning;
            bool overwritten = _applied != null && _gearsWritten && _applied.powertrain.transmission.finalGearRatio != _finalWritten;
            if (vehicle == _seen && tuned == _seenTuned && GarageState.Version == _version && option == _option && Settings.CarTuning == _carTuning && !overwritten)
            {
                return;
            }
            Restore();
            _seen = vehicle;
            _seenTuned = tuned;
            _version = GarageState.Version;
            _option = option;
            _carTuning = Settings.CarTuning;
            if (vehicle == null || !option)
            {
                return;
            }
            try
            {
                Apply(car, vehicle, tuning, tuned == vehicle);
            }
            catch (Exception e)
            {
                _failed = true;
                Restore();
                Logger?.Warn("Garage: performance parts could not be applied to this car; it is back as it was and parts are left alone from now on. " + e);
            }
        }

        private static readonly int HotFixedUpdate = HotSpots.Slot("Garage: GarageTuning.FixedUpdate");
        private void FixedUpdate()
        {
            using var hotSpot = HotSpots.Time(HotFixedUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults (Core/Guard)
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
            if (_dragDelta == 0f || _body == null)
            {
                return;
            }
            Vector3 velocity = _body.velocity;
            float speed = velocity.magnitude;
            if (speed > 1f)
            {
                _body.AddForce(velocity * (-_dragDelta * speed));   // the difference in air drag between the built car and the stock one
            }
        }

        private void OnDestroy()
        {
            Restore();
            if (_live == this)
            {
                _live = null;
            }
        }

        // A quicker gearbox: the change the car has now (the game's own, or car tuning's for its kind) times the build's.
        private void WriteShift(TransmissionComponent box, float factor)
        {
            if (box == null || factor == 1f || !(box.shiftDuration > 0f))
            {
                return;
            }
            _shiftBox = box;
            _shiftBefore = box.shiftDuration;
            _shiftWritten = TuningRules.ShiftSeconds(_shiftBefore, factor);
            box.shiftDuration = _shiftWritten;
        }

        /// <summary>The time a gear change takes on this car without the build, or -1 if it has no gearbox to read.</summary>
        public static float StandardShift(NwhVehicle vehicle)
        {
            TransmissionComponent box = vehicle != null && vehicle.powertrain != null ? vehicle.powertrain.transmission : null;
            if (box == null)
            {
                return -1f;
            }
            GarageTuning live = _live;
            return live != null && live._shiftBox == box && box.shiftDuration == live._shiftWritten ? live._shiftBefore : box.shiftDuration;
        }

        private void Apply(CarController car, NwhVehicle vehicle, CarTuning tuning, bool tuned)
        {
            string id = car.vehicleInstance != null ? car.vehicleInstance.id : null;
            GarageModifiers m = Of(id);
            if (!m.ChangesPerformance || vehicle.powertrain == null || vehicle.powertrain.engine == null || vehicle.vehicleRigidbody == null)
            {
                return;
            }
            EngineComponent engine = vehicle.powertrain.engine;
            TransmissionComponent box = vehicle.powertrain.transmission;
            string typeName = car.vehicleType != null ? car.vehicleType.vehicleTypeName : car.name;
            _applied = vehicle;
            _body = vehicle.vehicleRigidbody;
            _powerFactor = 1f;

            if (tuned && box != null && Read(vehicle, _body, out int firstForward) == null)
            {
                // CarTuning has given this car its stock targets' numbers: the same sums for those and the build's.
                CarKind kind = GarageShop.KindOf(car);
                bool eased = Settings.SmoothThrottle;
                float seconds = tuning.TargetSeconds;
                float top = tuning.TargetTopKmh;
                if (CarModel.Solve(Physics, kind, seconds, top, eased, Setup, out CarRun stock))
                {
                    float stockPower = Setup.PowerKw;
                    float stockDrag = Setup.Drag;
                    float newTop = Mathf.Min(top * m.TopSpeed, MaxTopKmh);
                    if (CarModel.Solve(Physics, kind, seconds / m.Acceleration, newTop, eased, Setup, out CarRun run)
                        && stockPower > 1f && Setup.PowerKw >= 5f && Setup.PowerKw <= _body.mass)
                    {
                        float final = Setup.Ratios[Setup.Ratios.Length - 1] / box.gears[box.gears.Count - 1];
                        if (final > 0.2f && final < 60f && Setup.Ratios.Length == box.gears.Count - firstForward)
                        {
                            _powerFactor = Setup.PowerKw / stockPower;
                            _finalBefore = box.finalGearRatio;
                            _gearsBefore = box.gears.ToArray();
                            for (int i = 0; i < box.gears.Count; i++)
                            {
                                box.gears[i] = i >= firstForward
                                    ? Setup.Ratios[i - firstForward] / final
                                    : _gearsBefore[i] * _finalBefore / final;   // reverse keeps the whole ratio it had
                            }
                            box.finalGearRatio = final;
                            _finalWritten = final;
                            _gearsWritten = true;
                            _dragDelta = Setup.Drag - stockDrag;
                            engine.powerModifiers.Add(_modifier);
                            WriteShift(box, m.Shift);
                            Note(id, FormattableString.Invariant($"Garage: {typeName}: 0-100 {stock.To100:F1} -> {run.To100:F1} s, top {stock.TopKmh:F0} -> {run.TopKmh:F0} km/h ")
                                     + FormattableString.Invariant(
                                         $"(power x{_powerFactor:F2} to {Setup.PowerKw:F0} kW, final drive {_finalBefore:F2} -> {final:F2}, drag {stockDrag:F3} -> {Setup.Drag:F3})."));
                            return;
                        }
                    }
                }
            }

            // A car the model does not drive: more power, and the game's speed limiter moved by the build's top speed.
            _powerFactor = m.Acceleration;
            engine.powerModifiers.Add(_modifier);
            var limiter = vehicle.GetComponent<SpeedLimiterModuleWrapper>();
            if (limiter != null && limiter.module != null && limiter.module.speedLimit > 1f && m.TopSpeed != 1f)
            {
                _limiter = limiter.module;
                _limitBefore = _limiter.speedLimit;
                _limitWritten = _limitBefore * m.TopSpeed;
                _limiter.speedLimit = _limitWritten;
            }
            WriteShift(box, m.Shift);
            Note(id, FormattableString.Invariant($"Garage: {typeName}: engine power x{_powerFactor:F2}")
                     + (_limiter != null ? $", speed limiter {_limitBefore:F0} -> {_limitWritten:F0}" : string.Empty)
                     + (_shiftBox != null ? FormattableString.Invariant($", gear change {_shiftBefore:F2} -> {_shiftWritten:F2} s") : string.Empty)
                     + " (the car model is not driving this car).");
        }

        private void Restore()
        {
            NwhVehicle vehicle = _applied;
            _applied = null;
            _dragDelta = 0f;
            _body = null;
            bool gears = _gearsWritten;
            _gearsWritten = false;
            SpeedLimiterModule limiter = _limiter;
            _limiter = null;
            TransmissionComponent shiftBox = _shiftBox;
            _shiftBox = null;
            if (vehicle == null)
            {
                return;   // nothing of ours anywhere, or the car is gone with its numbers
            }
            try
            {
                if (vehicle.powertrain != null && vehicle.powertrain.engine != null)
                {
                    vehicle.powertrain.engine.powerModifiers.Remove(_modifier);
                }
                TransmissionComponent box = vehicle.powertrain != null ? vehicle.powertrain.transmission : null;
                // Only if the ratios are still ours: CarTuning may have put its own back since.
                if (gears && box != null && box.finalGearRatio == _finalWritten)
                {
                    box.finalGearRatio = _finalBefore;
                    for (int i = 0; i < _gearsBefore.Length && i < box.gears.Count; i++)
                    {
                        box.gears[i] = _gearsBefore[i];
                    }
                }
                if (limiter != null && limiter.speedLimit == _limitWritten)
                {
                    limiter.speedLimit = _limitBefore;
                }
                // Only if the change time is still ours, as the ratios above.
                if (shiftBox != null && shiftBox.shiftDuration == _shiftWritten)
                {
                    shiftBox.shiftDuration = _shiftBefore;
                }

            }
            catch (Exception e)
            {
                _failed = true;
                Logger?.Warn("Garage: a car's performance parts could not be taken off completely; parts are left alone from now on. " + e);
            }
            _powerFactor = 1f;
        }

        private static void Note(string id, string line)
        {
            if (Logged.Add((id ?? string.Empty) + ":" + GarageState.Version))
            {
                Logger?.Info(line);
            }
        }

        /// <summary>What this car does with a build by the model: the engine, 0-100, top speed, the stop, the gear change, weight and height. Runs the sums: never per frame.</summary>
        public static GarageFigures Figures(CarController car, GarageBuild build)
        {
            GarageFigures figures = GarageFigures.Unknown;
            NwhVehicle vehicle = car != null ? car.vehicleController : null;
            if (vehicle == null || vehicle.vehicleRigidbody == null || vehicle.powertrain == null || build == null)
            {
                return figures;
            }
            GarageModifiers m = build.Modifiers();
            figures.Grip = m.Grip;
            figures.WetGrip = m.WetGrip;
            GarageCar fitted = GarageCar.Of(car, false);
            float mass = vehicle.vehicleRigidbody.mass;
            float torque = vehicle.brakes != null ? vehicle.brakes.maxTorque : 0f;
            if (fitted != null)
            {
                fitted.StockFigures(out mass, out torque);
                figures.RideMetres = fitted.RideChange(m);
            }
            figures.MassKg = mass > 1f ? mass * m.Mass : -1f;
            try
            {
                CarTuning tuning = CarTuning.Instance;
                if (tuning != null && tuning.Tuned == vehicle && Read(vehicle, vehicle.vehicleRigidbody, out _) == null)
                {
                    // CarTuning's targets are the stock car's, unless the hook has taken this car's build into them.
                    float seconds = tuning.TargetSeconds;
                    float stockTop = tuning.TargetTopKmh;
                    if (Claimed)
                    {
                        GarageModifiers own = Of(car.vehicleInstance != null ? car.vehicleInstance.id : null);
                        seconds *= own.Acceleration;
                        stockTop /= own.TopSpeed;
                    }
                    float top = Mathf.Min(stockTop * m.TopSpeed, MaxTopKmh);
                    bool eased = Settings.SmoothThrottle;
                    if (CarModel.Solve(Physics, GarageShop.KindOf(car), seconds / m.Acceleration, top, eased, Setup, out CarRun run))
                    {
                        figures.PowerKw = CarModel.PeakPower(Physics, Setup.PowerKw);
                        figures.TorqueNm = CarModel.PeakTorque(Physics, Setup.PowerKw);
                        if (m.Shift != 1f)
                        {
                            // As car tuning drives it: the power found with the kind's own change, the quicker one on top.
                            Setup.ShiftSeconds = TuningRules.ShiftSeconds(Setup.ShiftSeconds, m.Shift);
                            run = CarModel.Run(Physics, Setup, eased);
                        }
                        figures.ShiftSeconds = Setup.ShiftSeconds;
                        figures.To100 = run.To100;
                        figures.TopKmh = run.TopKmh;
                    }
                }
                else
                {
                    var limiter = vehicle.GetComponent<SpeedLimiterModuleWrapper>();
                    if (limiter != null && limiter.module != null && limiter.module.speedLimit > 1f && (car.vehicleType == null || car.vehicleType.maxSpeed > 0))
                    {
                        figures.TopKmh = (car.vehicleType != null ? car.vehicleType.maxSpeed : limiter.module.speedLimit) * m.TopSpeed;
                    }
                    if (!Settings.CarTuning)
                    {
                        // The game's own engine and gearbox drive it, with the parts' power factor and change on top.
                        EngineComponent engine = vehicle.powertrain.engine;
                        if (engine != null && engine.EstimatedPeakPower > 0f)
                        {
                            figures.PowerKw = engine.EstimatedPeakPower * m.Acceleration;
                            figures.TorqueNm = engine.EstimatedPeakTorque * m.Acceleration;
                        }
                        float standard = StandardShift(vehicle);
                        figures.ShiftSeconds = standard >= 0f ? TuningRules.ShiftSeconds(standard, m.Shift) : -1f;
                    }
                }


                // As the handling stops this car; else the brakes' torque at the tyre or what the tyres hold, the lesser.
                float handled = CarTuning.HandlingStop(car, m.Brakes, m.Mass, m.Grip);
                if (handled > 0f)
                {
                    figures.StopMetres = handled;
                }
                else
                {
                    float grip = 0f;
                    float brake = 0f;
                    int wheels = 0;
                    foreach (WheelComponent component in vehicle.powertrain.wheels)
                    {
                        WheelUAPI wheel = component != null ? component.wheelUAPI : null;
                        if (wheel == null || !(wheel.Radius > 0.05f))
                        {
                            continue;
                        }
                        float surface = wheel.FrictionPreset != null ? wheel.FrictionPreset.BCDE.z : 1f;
                        grip += surface * 1.35f;   // the tyre's own grip factor is left out: it may carry this build already
                        brake += 1f / wheel.Radius;
                        wheels++;
                    }
                    if (wheels > 0 && mass > 1f && torque > 0f)
                    {
                        float held = grip / wheels * 9.81f * m.Grip;
                        float braked = torque * m.Brakes * brake / (mass * m.Mass);
                        float slowing = Mathf.Min(held, braked);
                        const float speed = 27.7778f;
                        figures.StopMetres = speed * speed / (2f * slowing) + speed * 0.12f;   // plus 0.12 s while the brakes come on
                    }
                }
            }
            catch (Exception e)
            {
                Logger?.Warn("Garage: the figures for this car could not be worked out. " + e.Message);
            }
            return figures;
        }

        /// <summary>Reads the car into <see cref="Physics"/>. Returns why the model cannot drive it, or null.</summary>
        private static string Read(NwhVehicle vehicle, Rigidbody body, out int firstForward)
        {
            firstForward = 0;
            Powertrain train = vehicle.powertrain;
            EngineComponent engine = train.engine;
            TransmissionComponent box = train.transmission;
            ClutchComponent clutch = train.clutch;
            if (engine == null || box == null || clutch == null || train.wheels == null || train.wheels.Count == 0 || box.gears == null)
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
            List<float> gears = box.gears;
            while (firstForward < gears.Count && gears[firstForward] <= 0f)
            {
                firstForward++;
            }
            int forward = gears.Count - firstForward;
            if (forward < 1 || forward > 12)
            {
                return "it has " + forward + " forward gears";
            }

            Driven.Clear();
            FindDriven(box.Output, 0);
            if (Driven.Count == 0)
            {
                Driven.AddRange(train.wheels);
            }
            float radius = 0f;
            float traction = 0f;
            float weight = body.mass * 9.81f / train.wheels.Count;
            for (int i = 0; i < Driven.Count; i++)
            {
                WheelUAPI wheel = Driven[i].wheelUAPI;
                if (wheel == null || !(wheel.Radius > 0.05f))
                {
                    return "a wheel is missing";
                }
                radius += wheel.Radius;
                float surface = wheel.FrictionPreset != null ? wheel.FrictionPreset.BCDE.z : 1f;
                traction += surface * 1.35f * wheel.LongitudinalFrictionGrip * Mathf.Min(weight, wheel.MaxLoad > 0f ? wheel.MaxLoad : weight);
            }
            float rolling = 0f;
            float turning = 0f;
            for (int i = 0; i < train.wheels.Count; i++)
            {
                WheelUAPI wheel = train.wheels[i].wheelUAPI;
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

            Physics.Mass = body.mass;
            Physics.WheelRadius = radius / Driven.Count;
            Physics.RollingForce = rolling;
            Physics.TractionForce = traction;
            Physics.WheelInertiaMass = turning;
            Physics.RevLimitRpm = engine.revLimiterRPM;
            Physics.IdleRpm = engine.idleRPM;
            Physics.LaunchRpm = clutch.controlType == ClutchComponent.ClutchControlType.Automatic
                ? clutch.engagementRPM + clutch.throttleEngagementOffsetRPM + clutch.engagementRange * 0.5f
                : engine.idleRPM * 1.5f;
            Physics.LaunchRpm = Mathf.Clamp(Physics.LaunchRpm, engine.idleRPM, engine.revLimiterRPM * 0.6f);
            Physics.DriveInertia = engine.inertia + clutch.inertia + box.inertia;
            Physics.EngineLoss = engine.engineLossPercent;
            Physics.Boost = engine.forcedInduction != null && engine.forcedInduction.useForcedInduction ? engine.forcedInduction.powerGainMultiplier : 1f;
            Physics.ForwardGears = forward;
            float best = 0f;
            for (int i = 0; i < CarPhysics.CurveSamples; i++)
            {
                float value = Mathf.Max(0f, engine.powerCurve.Evaluate(i / (float)(CarPhysics.CurveSamples - 1)));
                Physics.Curve[i] = value;
                best = Mathf.Max(best, value);
            }
            return best > 0.2f ? null : "its power curve is empty";
        }

        private static void FindDriven(PowertrainComponent part, int depth)
        {
            if (part == null || depth > 4)
            {
                return;
            }
            if (part is WheelComponent wheel)
            {
                if (!Driven.Contains(wheel))
                {
                    Driven.Add(wheel);
                }
            }
            else if (part is DifferentialComponent differential)
            {
                FindDriven(differential.Output, depth + 1);
                FindDriven(differential.OutputB, depth + 1);
            }
        }

        /// <summary>The same model for a two-wheeler from the scooter's drivetrain. False, with why, if it cannot drive it.</summary>
        public static bool SolveBike(NwhVehicle vehicle, float seconds, float topKmh, CarSetup setup, out CarRun run, out int firstForward, out string why)
        {
            run = default;
            why = Read(vehicle, vehicle.vehicleRigidbody, out firstForward);
            if (why != null)
            {
                return false;
            }
            if (!CarModel.Solve(Physics, CarKind.Sports, seconds, topKmh, false, setup, out run))
            {
                why = "no engine power gets it to 100 km/h";
                return false;
            }
            return true;
        }
    }
}
