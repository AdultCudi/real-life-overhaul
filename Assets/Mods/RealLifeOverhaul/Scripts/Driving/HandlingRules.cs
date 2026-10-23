namespace RealLifeOverhaul
{
    /// <summary>The handling option's choices: how much weight, roll and give the driven car gets on top of the game's own.</summary>
    public enum HandlingLevel
    {
        Off = 0,     // the game's own handling, untouched
        Light = 1,   // almost exactly the game's own: a touch of weight, smooth brakes that never lock (the Relaxed preset)
        Full = 2,    // the game's own with a little weight, roll, dive and give (the default and the Realistic preset)
        Heavy = 3    // more weight, roll and give, still never off the road or over on a city turn (the Hardcore preset)
    }

    /// <summary>The six sliders of the test tools, in per cent of the design; the values chosen are logged so the design can be set from them.</summary>
    public readonly struct HandlingTuning
    {
        public const int Least = 50;    // per cent: every slider's lowest setting
        public const int Most = 150;    // per cent: and its highest

        public readonly int Softness;   // the spring rate is divided by it: 150 is two thirds the rate, a softer ride
        public readonly int Bounce;     // the damping is divided by it: 150 is two thirds the damping, more bounce
        public readonly int Roll;       // body roll per g, times it
        public readonly int Grip;       // the tyres' grip to the side, times it
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

    /// <summary>What the handling adds to one car at one level, each figure against the game's own for that car, the sliders folded in.</summary>
    public struct HandlingTarget
    {
        public float Mass;           // kg a car of the kind weighs
        public float MassShare;      // of the way from the game's weight to it, never lighter than the game made the car
        public float KneeDegrees;    // degrees of front slip angle past which the tyres hold no more: the give at the very limit
        public float Damping;        // the damping ratio the body's swings die away at, at most; 0 keeps the game's own dampers
        public float DamperScale;    // the dampers' rates, times it (the bounce slider)
        public float RollAdd;        // degrees of body roll per g on top of the game's own
        public float RollScale;      // the body roll per g, times it (the roll slider)
        public float DiveScale;      // the game's own nose dive per g of braking, times it (and its squat per g of pull)
        public float Softness;       // the spring rate is divided by it (the softness slider): 1 is the game's own rate for the weight
        public float Grip;           // the tyres' grip to the side, times it (the grip slider)
        public float Steering;       // the steering's reach at speed, times it (the steering slider): 1 is the game's own curve
        public float BrakeFactor;    // the full pedal's deceleration against the game's own brakes on this car
        public float BrakeRise;      // s from nothing to the full pedal, held down
    }

    /// <summary>
    /// The handling's levels on top of the game's own car, more of each at Heavy, almost none at Light, and the eased brake
    /// pedal. Game-free: NavCheck compiles this file.
    /// </summary>
    public static class HandlingRules
    {
        public const float GameMass = 1400f;        // kg: what the game's own cars weigh (the Vord Pony 1200), for the Guide
        public const float BrakeFall = 0.12f;       // s for the brake pedal to come back up from full
        public const float PedalDeadZone = 0.06f;   // just over the 0.05 the game's gearbox reads as "no pedal"

        /// <summary>The level a choice of the option stands for; anything else is the default.</summary>
        public static HandlingLevel Level(int choice)
        {
            return choice >= (int)HandlingLevel.Off && choice <= (int)HandlingLevel.Heavy ? (HandlingLevel)choice : HandlingLevel.Full;
        }

        /// <summary>What a kind of car weighs, kg: where its weight heads, a share of the way from the game's own.</summary>
        public static float KindMass(CarKind kind)
        {
            switch (kind)
            {
                case CarKind.Economy: return 1250f;   // small hatchback
                case CarKind.Luxury: return 2050f;    // big saloons and the limousine
                case CarKind.Utility: return 2350f;   // SUVs and pickups
                case CarKind.Van: return 2400f;
                case CarKind.Truck: return 3000f;     // box and freight trucks, lightly laden
                default: return 1550f;                // saloons and sports cars
            }
        }

        /// <summary>The figures one kind of car is given at one level, with the tuning sliders folded in. Untouched kinds get none.</summary>
        public static HandlingTarget Target(CarKind kind, HandlingLevel level, in HandlingTuning tuning)
        {
            var t = new HandlingTarget { Mass = KindMass(kind), DamperScale = 1f, RollScale = 1f, DiveScale = 1f, Softness = 1f, Grip = 1f, Steering = 1f, BrakeFactor = 1f };
            switch (level)
            {
                case HandlingLevel.Light:
                    t.MassShare = 0.1f;        // a touch of the kind's weight
                    t.KneeDegrees = 28f;       // the game's own tyres until the car is well sideways
                    t.Damping = 0f;            // the game's own dampers
                    t.RollAdd = 0.15f;
                    t.DiveScale = 1.05f;
                    t.BrakeRise = 0.15f;
                    break;
                case HandlingLevel.Heavy:
                    t.MassShare = 0.5f;        // half the way to the kind's weight
                    t.KneeDegrees = 16f;       // the tyres give sooner past a hard turn
                    t.Damping = 0.42f;         // a swing anyone sees after a kerb, then still
                    t.RollAdd = 0.8f;
                    t.DiveScale = 1.4f;        // as a share: on stiff springs a little more dive is a long arm, and the game's brakes are strong
                    t.BrakeFactor = 0.92f;     // a little less bite at the full pedal
                    t.BrakeRise = 0.4f;
                    break;
                default:
                    t.MassShare = 0.25f;       // a quarter of the way to the kind's weight
                    t.KneeDegrees = 20f;       // past the most slip the game's own car uses with the key held, at any speed
                    t.Damping = 0.55f;         // one small swing after a kerb, then still
                    t.RollAdd = 0.4f;
                    t.DiveScale = 1.2f;
                    t.BrakeRise = 0.25f;
                    break;
            }

            // The test tools' sliders, around the level's own figures.
            t.Softness = tuning.Softness / 100f;
            t.DamperScale = 100f / tuning.Bounce;
            t.RollScale = tuning.Roll / 100f;
            t.Grip = tuning.Grip / 100f;
            t.Steering = tuning.Steering / 100f;
            t.BrakeRise *= 100f / tuning.Brakes;
            return t;
        }

        /// <summary>The weight a car of a kind and of the game's own weight carries at a level, kg (for the Guide: the game's cars).</summary>
        public static float LevelMass(CarKind kind, HandlingLevel level, float gameMass)
        {
            HandlingTarget t = Target(kind, level, HandlingTuning.Neutral);
            return gameMass + t.MassShare * (t.Mass > gameMass ? t.Mass - gameMass : 0f);
        }

        /// <summary>
        /// The eased brake pedal, as the throttle's: nothing to full in <paramref name="rise"/> seconds, quickly back up, never
        /// past what is asked, and straight past the gearbox's dead zone so a tap counts.
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
    }
}
