namespace RealLifeOverhaul
{
    /// <summary>What the model needs to know about one car, read once from the vehicle when the player gets in.</summary>
    public sealed class CarPhysics
    {
        public const int CurveSamples = 33;

        public float Mass;               // kg
        public float WheelRadius;        // m
        public float RollingForce;       // N, all wheels: the tyres' rolling resistance
        public float TractionForce;      // N, the most the driven tyres can push with the car's weight on them
        public float RevLimitRpm;
        public float IdleRpm;
        public float LaunchRpm;          // where the engine sits while the clutch slips from a standstill
        public float DriveInertia;       // kg m2 turning with the engine (engine, clutch, gearbox)
        public float WheelInertiaMass;   // kg the turning wheels add to the car's mass
        public float EngineLoss;         // the engine's own loss, as a share of its power
        public float Boost = 1f;         // forced induction at full boost
        public int ForwardGears;
        public readonly float[] Curve = new float[CurveSamples];   // share of full power over 0..1 of the rev limit

        /// <summary>Share of full power at an engine speed given as a share of the rev limit.</summary>
        public float Power(float revs)
        {
            float at = (revs <= 0f ? 0f : revs >= 1f ? 1f : revs) * (CurveSamples - 1);
            int i = (int)at;
            if (i >= CurveSamples - 1)
            {
                return Curve[CurveSamples - 1];
            }
            return Curve[i] + (Curve[i + 1] - Curve[i]) * (at - i);
        }
    }

    /// <summary>The numbers that make a car meet its targets. <see cref="Ratios"/> are whole ratios, engine to wheel, first gear to top.</summary>
    public sealed class CarSetup
    {
        public float PowerKw;
        public float Drag;              // N per (m/s) squared
        public float UpshiftRpm;        // when driven gently; rises with throttle by ShiftIntensity
        public float DownshiftRpm;
        public float ShiftIntensity;
        public float ShiftSeconds;
        public bool TyreLimited;        // the target time is beyond what the tyres can put down
        public float[] Ratios = new float[0];
    }

    /// <summary>What a full-throttle run from a standstill comes to. Times are -1 when the speed was never reached.</summary>
    public struct CarRun
    {
        public float To50;
        public float To100;
        public float KmhAfter20;
        public float TopKmh;
    }

    /// <summary>
    /// A car in a straight line, the way the game's vehicle physics moves it: engine power over a curve,
    /// gears, a clutch that slips from rest, tyres that can only push so hard, rolling resistance and air
    /// drag. <see cref="Solve"/> finds the engine power, gearing and drag with which a car reaches 100 km/h
    /// in a given time and tops out at a given speed (power against drag in top gear, no cap);
    /// <see cref="Run"/> drives it. Game-free and allocation-free once the setup exists; run once per car,
    /// never per frame.
    /// </summary>
    public static class CarModel
    {
        public const float TopGearRevs = 0.95f;         // share of the rev limit the engine turns at top speed
        public const float ThrottleRise = 0.7f;         // seconds from nothing to full throttle with the pedal eased
        public const float ThrottleFall = 0.15f;        // ...and back
        public const float GentleThrottle = 0.5f;       // what the "gentle throttle" key holds the pedal to
        public const float PedalDeadZone = 0.06f;      // just over the 0.05 the game's gearbox reads as "no throttle"

        private const float Step = 0.02f;               // the game's physics step
        private const float RpmToRad = 0.10471976f;
        private const float SolveAtLeastKmh = 125f;
        private const float MaxPowerPerKg = 0.6f;      // kW per kg: more than any road car, the ceiling of the search
        private const float ClutchLocksAbove = 3f;      // m/s: the game's clutch stops slipping here

        /// <summary>
        /// The eased pedal: slow at first, quicker as it goes down (0 to 1 in <see cref="ThrottleRise"/>),
        /// and straight back up when released. Never past what is asked, so an analogue trigger keeps its feel.
        /// </summary>
        public static float Pedal(float current, float asked, float dt)
        {
            if (asked > current)
            {
                // Straight past the gearbox's dead zone: below it the box takes the pedal for released and drops
                // back to neutral, which in reverse (where the pedals swap) would never let the car pull away.
                if (current < PedalDeadZone && asked > PedalDeadZone)
                {
                    current = PedalDeadZone;
                }
                // The rate grows with the pedal: 0.924 is the time this takes from 0 to 1 at a rate of (0.5 + 1.5 x).
                float next = current + (0.5f + 1.5f * current) * (0.924f / ThrottleRise) * dt;
                return next > asked ? asked : next;
            }
            float back = current - dt / ThrottleFall;
            return back < asked ? asked : back;
        }

        /// <summary>The share of full power the engine gives at a throttle position (its own loss grows as the throttle closes).</summary>
        public static float ThrottlePower(float throttle, float loss)
        {
            float drag = throttle + 0.5f;
            return throttle * (1f + loss) - (drag > 1f ? 1f : drag) * loss;
        }

        /// <summary>
        /// Finds the setup for a target. Power is searched (more power is never slower); for each power
        /// the gearing and drag follow from the top speed: top gear turns the engine at 95 % of its limit
        /// there, and drag is whatever makes the engine's power at that speed just hold it. False if no
        /// power gets this car to 100 km/h at all (then nothing should be done to it).
        /// </summary>
        public static bool Solve(CarPhysics car, CarKind kind, float seconds, float topKmh, bool easedThrottle, CarSetup setup, out CarRun run)
        {
            // The time to 100 km/h only means something for a car that goes well past it. With the top speed
            // turned down below that, the power is found as if it topped out at 125, and then geared and
            // dragged down to what was asked: the car pulls as it would have, and stops sooner.
            float solveAt = topKmh < SolveAtLeastKmh ? SolveAtLeastKmh : topKmh;
            float low = 5f;
            float high = car.Mass * MaxPowerPerKg;
            Design(car, kind, solveAt, high, setup);
            float best = Run(car, setup, easedThrottle).To100;
            bool tyreLimited = best < 0f || best > seconds;
            if (tyreLimited && best > 0f)
            {
                seconds = best * 1.02f;   // no power does it: the least power that comes within 2 % of the best the tyres allow
            }
            if (best > 0f)
            {
                for (int i = 0; i < 22; i++)
                {
                    float middle = (low + high) * 0.5f;
                    Design(car, kind, solveAt, middle, setup);
                    float time = Run(car, setup, easedThrottle).To100;
                    if (time < 0f || time > seconds)
                    {
                        low = middle;
                    }
                    else
                    {
                        high = middle;
                    }
                }
            }
            Design(car, kind, topKmh, high, setup);
            setup.TyreLimited = tyreLimited;
            run = Run(car, setup, easedThrottle);
            return best > 0f;
        }

        /// <summary>Gearing, drag and shift points for a car of this power and top speed.</summary>
        public static void Design(CarPhysics car, CarKind kind, float topKmh, float powerKw, CarSetup setup)
        {
            int gears = car.ForwardGears < 1 ? 1 : car.ForwardGears;
            if (setup.Ratios.Length != gears)
            {
                setup.Ratios = new float[gears];
            }
            float top = topKmh / 3.6f;
            float engineAtTop = TopGearRevs * car.RevLimitRpm * RpmToRad;
            float topRatio = engineAtTop * car.WheelRadius / top;

            setup.PowerKw = powerKw;
            float push = powerKw * car.Boost * car.Power(TopGearRevs) * 1000f / top - car.RollingForce;
            setup.Drag = push > 0f ? push / (top * top) : 0f;

            // First gear runs out at about a fifth of top speed (45 to 58 km/h), but is never so short that
            // the engine's best torque spins the tyres.
            float firstEnds = top * 0.2f;
            firstEnds = firstEnds < 12.5f ? 12.5f : firstEnds > 16.1f ? 16.1f : firstEnds;
            float firstRatio = engineAtTop * car.WheelRadius / firstEnds;
            float torque = PeakTorque(car, powerKw);
            if (torque > 0f)
            {
                float grip = 0.95f * car.TractionForce * car.WheelRadius / torque;
                firstRatio = firstRatio > grip ? grip : firstRatio;
            }
            firstRatio = firstRatio < topRatio ? topRatio : firstRatio;

            float step = gears > 1 ? Pow(firstRatio / topRatio, 1f / (gears - 1)) : 1f;
            float ratio = firstRatio;
            for (int i = 0; i < gears; i++)
            {
                setup.Ratios[i] = i == gears - 1 ? topRatio : ratio;
                ratio /= step;
            }

            // Driven gently the box changes up early; with the pedal down it holds a gear to 95 % of the limit.
            float gently = CarCatalogue.UpshiftAt(kind);
            setup.UpshiftRpm = gently * car.RevLimitRpm;
            float more = TopGearRevs / gently - 1f;
            setup.ShiftIntensity = more < 0f ? 0f : more > 1f ? 1f : more;
            float down = 0.85f * setup.UpshiftRpm / step;   // below where any change up lands, so the box never hunts
            float least = car.IdleRpm * 1.2f;
            float most = 0.30f * car.RevLimitRpm;
            setup.DownshiftRpm = down < least ? least : down > most ? most : down;
            setup.ShiftSeconds = CarCatalogue.ShiftSeconds(kind);
        }

        /// <summary>Full throttle from a standstill for thirty seconds, in the game's own physics steps.</summary>
        public static CarRun Run(CarPhysics car, CarSetup setup, bool easedThrottle)
        {
            var run = new CarRun { To50 = -1f, To100 = -1f };
            int gears = setup.Ratios.Length;
            int gear = 0;
            float speed = 0f;
            float pedal = easedThrottle ? 0f : 1f;
            float held = 0f;          // the gearbox's own smoothed throttle, which moves its shift points
            float shifting = 0f;
            float limit = car.RevLimitRpm;
            for (int i = 1; i <= 1500; i++)
            {
                if (easedThrottle)
                {
                    pedal = Pedal(pedal, 1f, Step);
                }
                held += (pedal - held) * (Step * 2f);
                float ratio = setup.Ratios[gear];
                float wheelRpm = speed / car.WheelRadius * ratio / RpmToRad;
                if (shifting <= 0f && gear < gears - 1)
                {
                    float rise = held * setup.ShiftIntensity;
                    float changeUp = setup.UpshiftRpm * (1f + (rise > 1f ? 1f : rise));
                    changeUp = changeUp > limit * 0.97f ? limit * 0.97f : changeUp;
                    if (wheelRpm > changeUp)
                    {
                        gear++;
                        shifting = setup.ShiftSeconds;
                        ratio = setup.Ratios[gear];
                        wheelRpm = speed / car.WheelRadius * ratio / RpmToRad;
                    }
                }

                float force = 0f;
                if (shifting > 0f)
                {
                    shifting -= Step;
                }
                else if (wheelRpm < limit)
                {
                    force = Drive(car, setup, ratio, wheelRpm, speed, pedal);
                }
                float resist = setup.Drag * speed * speed;
                if (speed > 0.05f)
                {
                    resist += car.RollingForce;
                }
                else if (force < car.RollingForce)
                {
                    force = 0f;   // standing: rolling resistance holds, it does not push back
                }
                else
                {
                    resist += car.RollingForce;
                }
                float mass = car.Mass + car.WheelInertiaMass + car.DriveInertia * ratio * ratio / (car.WheelRadius * car.WheelRadius);
                float before = speed;
                speed += (force - resist) / mass * Step;
                speed = speed < 0f ? 0f : speed;

                float time = i * Step;
                if (run.To50 < 0f && speed >= 13.8889f)
                {
                    run.To50 = time - Step * (speed - 13.8889f) / (speed - before);
                }
                if (run.To100 < 0f && speed >= 27.7778f)
                {
                    run.To100 = time - Step * (speed - 27.7778f) / (speed - before);
                }
                if (i == 1000)
                {
                    run.KmhAfter20 = speed * 3.6f;
                }
            }
            run.TopKmh = TopSpeed(car, setup) * 3.6f;
            return run;
        }

        /// <summary>Where drive and resistance meet in top gear, in m/s.</summary>
        public static float TopSpeed(CarPhysics car, CarSetup setup)
        {
            float ratio = setup.Ratios[setup.Ratios.Length - 1];
            float low = ClutchLocksAbove + 1f;
            float high = car.RevLimitRpm * RpmToRad * car.WheelRadius / ratio;   // the rev limit in top gear
            for (int i = 0; i < 30; i++)
            {
                float middle = (low + high) * 0.5f;
                float wheelRpm = middle / car.WheelRadius * ratio / RpmToRad;
                if (Drive(car, setup, ratio, wheelRpm, middle, 1f) > setup.Drag * middle * middle + car.RollingForce)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }
            return low;
        }

        // The push at the wheels in a gear, in newtons.
        private static float Drive(CarPhysics car, CarSetup setup, float ratio, float wheelRpm, float speed, float pedal)
        {
            // The clutch slips from rest and holds the engine up; once rolling it locks and the engine turns with the wheels.
            bool locked = speed > ClutchLocksAbove && wheelRpm > car.IdleRpm * 1.1f;
            float rpm = locked || wheelRpm > car.LaunchRpm ? wheelRpm : car.LaunchRpm;
            float kilowatts = setup.PowerKw * car.Boost * car.Power(rpm / car.RevLimitRpm) * ThrottlePower(pedal, car.EngineLoss);
            float force = kilowatts * 1000f / (rpm * RpmToRad) * ratio / car.WheelRadius;
            return force > car.TractionForce ? car.TractionForce : force;
        }

        // The engine's best torque at full throttle, in newton metres, from where the clutch bites to the limit.
        private static float PeakTorque(CarPhysics car, float powerKw)
        {
            float best = 0f;
            float from = car.LaunchRpm / car.RevLimitRpm;
            for (int i = 0; i < CarPhysics.CurveSamples; i++)
            {
                float revs = i / (float)(CarPhysics.CurveSamples - 1);
                if (revs < from || revs <= 0f)
                {
                    continue;
                }
                float torque = car.Curve[i] / (revs * car.RevLimitRpm * RpmToRad);
                best = torque > best ? torque : best;
            }
            return best * powerKw * car.Boost * 1000f;
        }

        private static float Pow(float value, float power)
        {
            return (float)System.Math.Pow(value, power);
        }
    }
}
