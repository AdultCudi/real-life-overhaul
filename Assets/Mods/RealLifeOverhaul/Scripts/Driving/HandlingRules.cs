namespace RealLifeOverhaul
{
    /// <summary>The handling option's choices: how much of the weight, the soft ride and the sliding the driven car is given.</summary>
    public enum HandlingLevel
    {
        Off = 0,     // the game's own handling, untouched
        Light = 1,   // lighter: half the extra weight, firmer and grippier (the Relaxed preset)
        Full = 2,    // the whole feel (the default and the Realistic preset)
        Heavy = 3    // heavier and slidier (the Hardcore preset)
    }

    /// <summary>
    /// The six tuning sliders of the test tools, each in per cent of the design: 100 is the handling as designed, and the
    /// owner's chosen values are written to the log so that the design can be set from them afterwards.
    /// </summary>
    public readonly struct HandlingTuning
    {
        public const int Least = 50;    // per cent: every slider's lowest setting
        public const int Most = 150;    // per cent: and its highest

        public readonly int Softness;   // the spring rate is divided by it: 150 is two thirds the rate, a softer ride
        public readonly int Bounce;     // the damping is divided by it: 150 is two thirds the damping, more bounce
        public readonly int Roll;       // body roll per g, times it
        public readonly int Grip;       // the tyres' grip to the side and along, times it
        public readonly int Steering;   // how far the steering reaches at speed, times it (the lock at walking pace stays)
        public readonly int Brakes;     // how quickly the brakes build: 150 builds in two thirds of the time

        public HandlingTuning(int softness, int bounce, int roll, int grip, int steering, int brakes)
        {
            Softness = Clamp(softness);
            Bounce = Clamp(bounce);
            Roll = Clamp(roll);
            Grip = Clamp(grip);
            Steering = Clamp(steering);
            Brakes = Clamp(brakes);
        }

        public static HandlingTuning Neutral => new HandlingTuning(100, 100, 100, 100, 100, 100);

        /// <summary>One number for "the sliders changed": each slider's per cent in 8 bits.</summary>
        public long Stamp => (long)Softness + ((long)Bounce << 8) + ((long)Roll << 16) + ((long)Grip << 24) + ((long)Steering << 32) + ((long)Brakes << 40);

        private static int Clamp(int value)
        {
            return value < Least ? Least : value > Most ? Most : value;
        }
    }

    /// <summary>
    /// What the handling aims for on one kind of car at one level, as physical figures. <see cref="HandlingModel.Design"/>
    /// turns them into the game's spring, damper, anti-roll bar, tyre, steering and brake values for one car's own
    /// geometry.
    /// </summary>
    public struct HandlingTarget
    {
        public float Mass;            // kg the car weighs at this level
        public float MassShare;       // of the way from the game's mass to Mass (the Light level goes half way)
        public float ComHeight;       // m: the least height of the centre of mass above the road at rest
        public float ComShare;        // of the way the centre of mass is raised towards ComHeight when the game's is lower
        public float RideHz;          // the body's natural frequency on its springs (heave), Hz
        public float Damping;         // damping ratio, the mean of bump and rebound
        public float RollPerG;        // degrees of body roll per g of cornering
        public float DivePerG;        // degrees of nose dive per g of braking (and of squat per g of pull)
        public float RollCentre;      // m above the road where the tyres' side force acts on the body
        public float SideGrip;        // the front tyres' peak grip to the side, in g
        public float RearSurplus;     // how much more the rear tyres hold: the car runs wide at the limit, not round
        public float AlongGrip;       // the tyres' peak grip along, in g (braking and pulling away)
        public float PeakSlip;        // degrees of slip angle at which the front tyres give their most
        public float RearPeakFactor;  // the rear tyres' peak slip angle against the front's (later: they let go gently)
        public float Plateau;         // share of the peak a tyre keeps well past it: high, so no snap
        public float BrakeShare;      // the full pedal's deceleration as a share of the tyres' grip along
        public float BrakeRise;       // s from nothing to the full pedal, held down
        public float SteerReach;      // at speed, the full steering is this many times the front tyres' peak slip angle past the bend's own angle
        public float SteerSmoothing;  // s: how gently the wheels follow the key at 180 km/h (the game's own is 0.05 s at a standstill)
        public float RollingShare;    // the tyres' rolling resistance as a share of the load on them
    }

    /// <summary>
    /// The handling of the driven car by kind of car (<see cref="CarCatalogue"/>) and by level: the figures each kind is
    /// built to, from what such cars weigh and how they ride, with the game's soft, heavy, rolling and sliding feel
    /// asked for (docs/features/driving.md, "Handling"). Game-free: NavCheck compiles this file.
    /// </summary>
    public static class HandlingRules
    {
        public const float BrakeFall = 0.12f;       // s for the brake pedal to come back up from full
        public const float PedalDeadZone = 0.06f;   // just over the 0.05 the game's gearbox reads as "no pedal"
        private const float LightBrakes = 8f / 7f;  // Light's full pedal against the whole feel's, as a share of the tyres' grip
        private const float HeavyBrakes = 13f / 14f;   // and Heavy's

        /// <summary>The level a choice of the option stands for; anything else is the default.</summary>
        public static HandlingLevel Level(int choice)
        {
            return choice >= (int)HandlingLevel.Off && choice <= (int)HandlingLevel.Heavy ? (HandlingLevel)choice : HandlingLevel.Full;
        }

        /// <summary>The figures one kind of car is built to at one level, with the tuning sliders applied. Untouched kinds get none.</summary>
        public static HandlingTarget Target(CarKind kind, HandlingLevel level, in HandlingTuning tuning)
        {
            HandlingTarget t = Full(kind);
            switch (level)
            {
                case HandlingLevel.Light:
                    t.MassShare = 0.5f;          // half the extra weight
                    t.ComShare = 0.5f;           // the centre of mass raised half the way
                    t.RideHz *= 1.12f;           // firmer springs
                    t.Damping = t.Damping + 0.03f < 0.27f ? t.Damping + 0.03f : 0.27f;   // a swing less, still inside the band with the Garage's lightest build
                    t.RollPerG *= 0.65f;
                    t.DivePerG *= 0.65f;
                    t.SideGrip *= 1.12f;         // nearer the game's own grip (and the tallest van still never tips on the grippiest tyres)
                    t.AlongGrip *= 1.1f;
                    t.RearSurplus = 0.08f;       // the tail stays put unless the handbrake swings it
                    t.PeakSlip *= 0.9f;          // the tyres bite sooner
                    t.Plateau = 0.95f;
                    t.BrakeShare = t.BrakeShare * LightBrakes < HandlingModel.MostUse ? t.BrakeShare * LightBrakes : HandlingModel.MostUse;   // a seventh more of grippier tyres
                    t.BrakeRise = 0.4f;
                    t.SteerReach = 1.0f;
                    t.SteerSmoothing = 0.15f;
                    break;
                case HandlingLevel.Heavy:
                    t.Mass *= 1.08f;             // heavier still
                    t.RideHz *= 0.96f;
                    t.Damping = t.Damping - 0.03f > 0.21f ? t.Damping - 0.03f : 0.21f;   // a swing more, still settling within two seconds
                    t.RollPerG *= 1.1f;
                    t.DivePerG *= 1.1f;
                    t.SideGrip *= 0.92f;         // slidier
                    t.AlongGrip *= 0.95f;
                    t.RearSurplus = 0.015f;      // the tail comes round when the weight leaves it
                    t.PeakSlip *= 1.12f;
                    t.Plateau = 0.91f;
                    t.BrakeShare *= HeavyBrakes;   // a fourteenth less of slidier tyres
                    t.BrakeRise = 0.6f;
                    t.SteerReach = 1.2f;
                    t.SteerSmoothing = 0.24f;
                    break;
            }

            // The owner's sliders, around the level's own figures.
            t.RideHz *= Sqrt(100f / tuning.Softness);   // the rate is divided by the slider, the frequency by its root
            t.Damping *= 100f / tuning.Bounce;
            t.RollPerG *= tuning.Roll / 100f;
            t.SideGrip *= tuning.Grip / 100f;
            t.AlongGrip *= tuning.Grip / 100f;
            t.SteerReach *= tuning.Steering / 100f;
            t.BrakeRise *= 100f / tuning.Brakes;
            return t;
        }

        // The whole feel for each kind: what such cars weigh, how high their weight sits, how they ride and grip.
        private static HandlingTarget Full(CarKind kind)
        {
            //                         kg    com h  ride Hz  zeta   roll  dive  centre  side   along  peak deg  brakes g
            switch (kind)
            {
                case CarKind.Economy: return Make(1250f, 0.50f, 1.25f, 0.24f, 6.5f, 2.4f, 0.05f, 0.95f, 1.00f, 12f, 0.86f);   // small hatchback
                case CarKind.Saloon: return Make(1550f, 0.52f, 1.20f, 0.24f, 6.0f, 2.4f, 0.06f, 1.00f, 1.00f, 12f, 0.88f);    // family and full-size saloons
                case CarKind.Luxury: return Make(2050f, 0.52f, 1.05f, 0.23f, 6.5f, 2.6f, 0.08f, 1.00f, 1.00f, 13f, 0.88f);    // big, soft and floating
                case CarKind.Sports: return Make(1550f, 0.45f, 1.45f, 0.26f, 4.5f, 1.8f, 0.05f, 1.12f, 1.10f, 10f, 0.92f);    // low, firm, wide tyres
                case CarKind.Utility: return Make(2350f, 0.62f, 1.15f, 0.24f, 7.5f, 2.6f, 0.14f, 0.90f, 0.95f, 14f, 0.85f);   // SUVs and pickups: a live rear axle sits its roll centre higher
                case CarKind.Van: return Make(2400f, 0.75f, 1.20f, 0.24f, 7.0f, 2.6f, 0.25f, 0.85f, 0.95f, 14f, 0.75f);       // tall and heavy, on a beam axle
                case CarKind.Truck: return Make(3000f, 0.80f, 1.25f, 0.24f, 6.5f, 2.2f, 0.22f, 0.80f, 0.85f, 15f, 0.65f);     // box and freight trucks, lightly laden, on beam axles
                default: return Make(1550f, 0.52f, 1.20f, 0.24f, 6.0f, 2.4f, 0.06f, 1.00f, 1.00f, 12f, 0.88f);
            }
        }

        private static HandlingTarget Make(float mass, float comHeight, float rideHz, float damping, float roll, float dive, float rollCentre,
            float side, float along, float peakSlip, float brakes)
        {
            return new HandlingTarget
            {
                Mass = mass,
                MassShare = 1f,
                ComHeight = comHeight,
                ComShare = 1f,
                RideHz = rideHz,
                Damping = damping,
                RollPerG = roll,
                DivePerG = dive,
                RollCentre = rollCentre,
                SideGrip = side,
                RearSurplus = 0.04f,       // a little more at the rear: the car runs wide when pushed, and lifting off brings the tail round
                AlongGrip = along,
                PeakSlip = peakSlip,
                RearPeakFactor = 1.1f,
                Plateau = 0.93f,
                BrakeShare = brakes / along,   // the full pedal's deceleration: firm, inside the tyres (HandlingModel.MostUse), the ABS for the wet
                BrakeRise = 0.5f,
                SteerReach = 1.1f,         // a little past the front tyres' peak: pushed too hard, the car runs wide
                SteerSmoothing = 0.2f,
                RollingShare = 0.012f      // a road tyre's rolling resistance: the car carries its speed
            };
        }

        /// <summary>
        /// The eased brake pedal: as the throttle's (<see cref="CarModel.Pedal"/>), slow at first and quicker as it goes
        /// down, from nothing to full in <paramref name="rise"/> seconds, and quickly back up when let go. Never past what
        /// is asked, so an analogue trigger keeps its own feel; straight past the gearbox's dead zone, so a tap counts.
        /// </summary>
        public static float BrakePedal(float current, float asked, float dt, float rise)
        {
            if (asked > current)
            {
                if (current < PedalDeadZone && asked > PedalDeadZone)
                {
                    current = PedalDeadZone;
                }
                // 0.924 is the time this takes from 0 to 1 at a rate of (0.5 + 1.5 x), as for the throttle.
                float next = current + (0.5f + 1.5f * current) * (0.924f / (rise > 0.05f ? rise : 0.05f)) * dt;
                return next > asked ? asked : next;
            }
            float back = current - dt / BrakeFall;
            return back < asked ? asked : back;
        }

        private static float Sqrt(float value)
        {
            return (float)System.Math.Sqrt(value);
        }
    }
}
