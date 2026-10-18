using System;

namespace RealLifeOverhaul
{
    /// <summary>The recorded engine a car is given, by its kind (<see cref="CarSoundRules.VoiceFor"/>).</summary>
    public enum EngineVoice
    {
        None,     // not a car the mod gives a recorded engine (a scooter, a hand truck): the game's own sound
        Petrol,   // a small four-cylinder petrol engine: economy cars and saloons
        V8,       // a big V8: luxury cars, SUVs and pickups, the muscle cars
        Sport,    // a high-revving sports engine: the sports cars
        Diesel    // a four-cylinder diesel: vans and trucks
    }

    /// <summary>One recorded loop of an engine at a steady speed: its file and the revs it was recorded at.</summary>
    public readonly struct EngineLoop
    {
        public readonly string Clip;      // the file's name in Sounds, without ".ogg"
        public readonly float Rpm;        // the revs the recording holds, from its firing frequency
        public readonly bool HasOff;      // a second file "<Clip>_off" is the same revs off the throttle

        public EngineLoop(string clip, float rpm, bool hasOff)
        {
            Clip = clip;
            Rpm = rpm;
            HasOff = hasOff;
        }
    }

    /// <summary>What an exhaust does to the sound: louder, lower and rawer off the throttle, and pops on lifting off.</summary>
    public readonly struct ExhaustSound
    {
        public readonly float Volume;       // times the stock engine's
        public readonly float Pitch;        // times the engine's pitch
        public readonly float OffLevel;     // the off-throttle layer's level against the on-throttle one
        public readonly float PopsPerSecond;   // while the pops are due after lifting off; 0: never
        public readonly float PopVolume;

        public ExhaustSound(float volume, float pitch, float offLevel, float popsPerSecond, float popVolume)
        {
            Volume = volume;
            Pitch = pitch;
            OffLevel = offLevel;
            PopsPerSecond = popsPerSecond;
            PopVolume = popVolume;
        }
    }

    /// <summary>What a turbo sounds like: how fast it builds boost, its whistle's level and pitch, its flutter's level.</summary>
    public readonly struct TurboSound
    {
        public readonly float SpoolSeconds;    // time constant of boost building with the throttle open
        public readonly float WhistleVolume;   // at full boost
        public readonly float PitchLow, PitchHigh;   // the whistle's pitch at no boost and at full boost
        public readonly float FlutterVolume;   // at full boost; the blow-off on lifting off

        public TurboSound(float spoolSeconds, float whistleVolume, float pitchLow, float pitchHigh, float flutterVolume)
        {
            SpoolSeconds = spoolSeconds;
            WhistleVolume = whistleVolume;
            PitchLow = pitchLow;
            PitchHigh = pitchHigh;
            FlutterVolume = flutterVolume;
        }
    }

    /// <summary>
    /// The recorded car sounds as numbers, with nothing of the game or the engine in them, so that NavCheck
    /// (CarSoundCheck.cs) checks the very code <see cref="CarSoundCar"/> plays by. The sounds themselves, where each was
    /// recorded and what was done to it, are in Sounds/SOURCES.md; this file names every clip the mod loads
    /// (<see cref="AllClips"/>).
    ///
    /// THE ENGINE. Each <see cref="EngineVoice"/> is a set of loops, each a real engine held at steady revs (idle, low,
    /// mid, high), with a second loop for every one above idle that is the same revs off the throttle. The loops are laid
    /// on the car's own rev range (<see cref="Place"/>): the idle loop at the car's idle, the others as far above it as
    /// they were recorded, spread wider if the top loop would otherwise be pitched past <see cref="TopPitch"/> at the rev
    /// limit. Between two neighbouring loops the crossfade (<see cref="Mix"/>) is equal-power over the middle half of the
    /// span on a scale of ratios, so every rev has one or two loops sounding, the power of the mix is always one, and no
    /// level steps; each loop is pitched by the revs over its own (<see cref="PitchOf"/>).
    ///
    /// LOAD. On and off the throttle are two layers at the same revs, crossed equal-power by the throttle
    /// (<see cref="OnShare"/>); off the throttle is quieter by the exhaust's <see cref="ExhaustSound.OffLevel"/>.
    ///
    /// THE PARTS. A sports or straight-through exhaust (<see cref="Exhaust"/>) is louder, a little lower, rawer off the
    /// throttle, and pops for a moment after lifting off from high revs (<see cref="PopChance"/>). A turbo
    /// (<see cref="Turbo"/>) builds boost with the throttle and the revs (<see cref="Boost"/>), whistles with it, and
    /// flutters when the throttle shuts on boost (<see cref="Flutters"/>).
    ///
    /// THE SHOP. On the turntable the car is held, so its revs are the sound's own (<see cref="ShopRevs"/>): the throttle
    /// key raises them towards most of the rev limit, letting go lets them fall back to idle.
    ///
    /// Everything here is a handful of multiplications asked once a frame while a car is held; nothing is allocated.
    /// </summary>
    public static class CarSoundRules
    {
        public const string Folder = "Sounds";        // next to the mod's DLL
        public const string Extension = ".ogg";
        public const string OffSuffix = "_off";

        public const float TopPitch = 1.6f;           // the most the top loop is pitched at the rev limit before the loops are spread
        public const float IdlePitch = 0.75f;         // the deepest the idle loop is played at the car's idle, to keep the top loop's pitch down
        public const float MinPitch = 0.5f;           // a loop is never pitched below or above these (an engine that stalls or over-revs)
        public const float MaxPitch = 2.2f;
        public const float FadeShare = 0.5f;          // of the span between two loops, on a scale of ratios, that they cross over in
        public const float EngineVolume = 0.7f;       // a stock engine's level on the car's own sound group (the game's own note runs at about 0.5 on louder clips)
        public const float IdleLevel = 0.62f;         // the engine's level at idle, against its level at the rev limit
        public const float OnFrom = 0.05f;            // throttle below which the engine is off the throttle
        public const float OnFull = 0.45f;            // throttle from which it is wholly on it

        public const float LiftFrom = 0.5f;           // throttle a lift-off starts from: pops and the flutter follow it
        public const float LiftTo = 0.1f;             // and goes below
        public const float LiftWithin = 0.35f;        // seconds the throttle has to fall in to be a lift-off
        public const float PopWindow = 1.2f;          // seconds after a lift-off that pops may come
        public const float PopFromRevs = 0.4f;        // of the rev limit: below it nothing pops
        public const float PopGap = 0.07f;            // seconds between two pops at least

        public const float FlutterFrom = 0.45f;       // boost from which shutting the throttle flutters
        public const float BoostFromRevs = 0.25f;     // of the rev limit where boost starts to build
        public const float BoostFullRevs = 0.7f;      // and where it can be full
        public const float BoostFall = 0.25f;         // seconds: boost dies away this fast with the throttle shut

        public const float ShopRevTop = 0.85f;        // of the rev limit: where the throttle key takes the revs in the shop
        public const float ShopRise = 0.35f;          // seconds: the time constant of the revs rising
        public const float ShopFall = 2600f;          // rpm a second the revs fall at, letting go
        public const float ShopFallTail = 0.25f;      // seconds: the last of the fall eases onto idle

        public const float SkidVolume = 0.7f;         // at full squeal
        public const float SkidFrom = 0.02f;          // below this level the squeal is stopped

        private static readonly EngineLoop[] PetrolLoops =
        {
            new EngineLoop("petrol_idle", 855f, false),
            new EngineLoop("petrol_low", 2076f, true),
            new EngineLoop("petrol_high", 2928f, true)
        };

        private static readonly EngineLoop[] V8Loops =
        {
            new EngineLoop("v8_idle", 996f, false),
            new EngineLoop("v8_low", 2177f, true),
            new EngineLoop("v8_mid", 2766f, true),
            new EngineLoop("v8_high", 3611f, true)
        };

        private static readonly EngineLoop[] SportLoops =
        {
            new EngineLoop("sport_idle", 804f, false),
            new EngineLoop("sport_low", 1970f, true),
            new EngineLoop("sport_mid", 2767f, true),
            new EngineLoop("sport_high", 4062f, true)
        };

        private static readonly EngineLoop[] DieselLoops =
        {
            new EngineLoop("diesel_idle", 783f, false),
            new EngineLoop("diesel_low", 1227f, true),
            new EngineLoop("diesel_mid", 1650f, true),
            new EngineLoop("diesel_high", 2577f, true)
        };

        private static readonly EngineLoop[] NoLoops = new EngineLoop[0];

        /// <summary>Exhaust pops, picked from in turn.</summary>
        public static readonly string[] Pops = { "pop_1", "pop_2", "pop_3", "pop_4" };

        public const string WhistleClip = "turbo_whistle";
        public const string FlutterClip = "turbo_flutter";
        public const string SquealClip = "tyre_squeal";

        public const int MostLoops = 7;               // the most clips one engine plays: four loops and three off the throttle

        /// <summary>Every clip the mod loads, in the order it loads them. Each must be in Sounds and have a line in its SOURCES.md.</summary>
        public static readonly string[] AllClips = Clips();

        private static readonly ExhaustSound[] Exhausts =
        {
            new ExhaustSound(1.00f, 1.00f, 0.55f, 0f, 0f),     // stock
            new ExhaustSound(1.20f, 0.98f, 0.70f, 1.0f, 0.55f),   // sports: deeper and louder, the odd pop
            new ExhaustSound(1.40f, 0.96f, 0.85f, 3.0f, 0.95f)    // straight-through: loud and raw, pops and bangs
        };

        private static readonly TurboSound[] Turbos =
        {
            new TurboSound(0f, 0f, 1f, 1f, 0f),                   // none
            new TurboSound(0.6f, 0.30f, 0.80f, 1.25f, 0.45f),     // a turbocharger: quick to spool, a higher whistle
            new TurboSound(1.0f, 0.42f, 0.65f, 1.05f, 0.65f)      // a big turbo: slower, lower and louder, a bigger flutter
        };

        /// <summary>The loops of an engine, idle first, revs rising. Empty for <see cref="EngineVoice.None"/>.</summary>
        public static EngineLoop[] Loops(EngineVoice voice)
        {
            switch (voice)
            {
                case EngineVoice.Petrol: return PetrolLoops;
                case EngineVoice.V8: return V8Loops;
                case EngineVoice.Sport: return SportLoops;
                case EngineVoice.Diesel: return DieselLoops;
                default: return NoLoops;
            }
        }

        /// <summary>
        /// The engine a car is given: by its kind, with the muscle cars (the Vord Pony and the Mustang GT of another mod) a
        /// V8 among the sports cars. <paramref name="typeName"/> is the game's whole vehicle type name.
        /// </summary>
        public static EngineVoice VoiceFor(string typeName, CarKind kind)
        {
            switch (kind)
            {
                case CarKind.Economy:
                case CarKind.Saloon: return EngineVoice.Petrol;
                case CarKind.Luxury:
                case CarKind.Utility: return EngineVoice.V8;
                case CarKind.Sports:
                    return typeName != null && (typeName.EndsWith("vordpony", StringComparison.Ordinal) || typeName.EndsWith("mustanggt", StringComparison.Ordinal))
                        ? EngineVoice.V8
                        : EngineVoice.Sport;
                case CarKind.Van:
                case CarKind.Truck: return EngineVoice.Diesel;
                default: return EngineVoice.None;
            }
        }

        /// <summary>The exhaust's sound by its loudness (0 stock, 1 sports, 2 straight-through); anything else is stock.</summary>
        public static ExhaustSound Exhaust(int loudness)
        {
            return Exhausts[loudness >= 0 && loudness < Exhausts.Length ? loudness : 0];
        }

        /// <summary>The turbo's sound by its size (0 none, 1 a turbocharger, 2 a big turbo); anything else is none.</summary>
        public static TurboSound Turbo(int size)
        {
            return Turbos[size >= 0 && size < Turbos.Length ? size : 0];
        }

        // ---------------------------------------------------------------- the engine

        /// <summary>
        /// The revs each loop of an engine stands for on a car with this idle and rev limit, into <paramref name="at"/>
        /// (at least as long as the loops). The loops keep the recordings' ratios to each other, so two loops crossing over
        /// sound at one pitch; the idle loop is at the car's idle unless the top loop would then be pitched past
        /// <see cref="TopPitch"/> at the limit, when every loop is moved up by one factor (the idle a little deeper, at most
        /// down to <see cref="IdlePitch"/>). Returns the count.
        /// </summary>
        public static int Place(EngineVoice voice, float idleRpm, float limitRpm, float[] at)
        {
            EngineLoop[] loops = Loops(voice);
            if (loops.Length == 0)
            {
                return 0;
            }
            float idle = idleRpm > 100f ? idleRpm : 800f;
            float limit = limitRpm > idle * 1.5f ? limitRpm : idle * 6f;
            float span = loops[loops.Length - 1].Rpm / loops[0].Rpm;
            float shift = limit / (TopPitch * idle * span);
            shift = shift < 1f ? 1f : shift > 1f / IdlePitch ? 1f / IdlePitch : shift;
            for (int i = 0; i < loops.Length; i++)
            {
                at[i] = idle * shift * loops[i].Rpm / loops[0].Rpm;
            }
            return loops.Length;
        }

        /// <summary>
        /// How loud each loop is at these revs (<paramref name="gains"/>, the first <paramref name="count"/>): one loop
        /// below the first span and above the last, two equal-power across the middle of each span (on a scale of ratios),
        /// one elsewhere. The squares always add up to one.
        /// </summary>
        public static void Mix(float rpm, float[] at, int count, float[] gains)
        {
            for (int i = 0; i < count; i++)
            {
                gains[i] = 0f;
            }
            if (count <= 0)
            {
                return;
            }
            if (count == 1 || rpm <= at[0])
            {
                gains[0] = 1f;
                return;
            }
            if (rpm >= at[count - 1])
            {
                gains[count - 1] = 1f;
                return;
            }
            int lower = 0;
            while (lower < count - 2 && rpm >= at[lower + 1])
            {
                lower++;
            }
            double span = Math.Log(at[lower + 1] / at[lower]);
            double into = Math.Log(rpm / at[lower]) / span;            // 0 at the lower loop, 1 at the upper
            double from = 0.5 - FadeShare * 0.5;
            double t = (into - from) / FadeShare;
            t = t < 0.0 ? 0.0 : t > 1.0 ? 1.0 : t;
            gains[lower] = (float)Math.Cos(t * Math.PI * 0.5);
            gains[lower + 1] = (float)Math.Sin(t * Math.PI * 0.5);
        }

        /// <summary>A loop's pitch at these revs: the revs over its own, kept within <see cref="MinPitch"/> and <see cref="MaxPitch"/>.</summary>
        public static float PitchOf(float rpm, float loopRpm)
        {
            float pitch = loopRpm > 1f ? rpm / loopRpm : 1f;
            return pitch < MinPitch ? MinPitch : pitch > MaxPitch ? MaxPitch : pitch;
        }

        /// <summary>The on-throttle layer's share of the power (the off layer has the rest): 0 off the throttle, 1 on it, smooth between.</summary>
        public static float OnShare(float throttle)
        {
            float t = (throttle - OnFrom) / (OnFull - OnFrom);
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }

        /// <summary>The engine's level, 0 to 1 before the exhaust: rising with the revs, quieter off the throttle.</summary>
        public static float Level(float revs, float onShare, in ExhaustSound exhaust)
        {
            return ByRevs(revs) * (exhaust.OffLevel + (1f - exhaust.OffLevel) * onShare);
        }

        /// <summary>The engine's level by its revs alone, <see cref="IdleLevel"/> at a standstill to 1 at the limit: the idle loop's (idle is idle, on the throttle or off it).</summary>
        public static float ByRevs(float revs)
        {
            revs = revs < 0f ? 0f : revs > 1f ? 1f : revs;
            return IdleLevel + (1f - IdleLevel) * revs;
        }

        /// <summary>The volume a loop is played at: its share of the mix, of its layer, the level, the exhaust; never above 1.</summary>
        public static float Volume(float gain, float layer, float level, in ExhaustSound exhaust)
        {
            float v = gain * layer * level * exhaust.Volume * EngineVolume;
            return v < 0f ? 0f : v > 1f ? 1f : v;
        }

        // ---------------------------------------------------------------- lifting off: pops and the flutter

        /// <summary>Whether the throttle has just been lifted: it was at least <see cref="LiftFrom"/> within <see cref="LiftWithin"/> s and is below <see cref="LiftTo"/> now.</summary>
        public static bool Lifted(float throttle, float lastHighAgo)
        {
            return throttle < LiftTo && lastHighAgo >= 0f && lastHighAgo <= LiftWithin;
        }

        /// <summary>
        /// The chance of a pop in this frame, <paramref name="sinceLift"/> s after a lift-off at these revs (of the limit),
        /// <paramref name="sinceLastPop"/> s after the last one. None from a stock exhaust, below <see cref="PopFromRevs"/>,
        /// back on the throttle, or after <see cref="PopWindow"/>.
        /// </summary>
        public static float PopChance(in ExhaustSound exhaust, float revs, float throttle, float sinceLift, float sinceLastPop, float dt)
        {
            if (exhaust.PopsPerSecond <= 0f || revs < PopFromRevs || throttle >= LiftTo || sinceLift < 0f || sinceLift > PopWindow || sinceLastPop < PopGap)
            {
                return 0f;
            }
            float fade = 1f - sinceLift / PopWindow;                 // most just after the lift, none at the window's end
            float chance = exhaust.PopsPerSecond * fade * (0.5f + revs) * dt;
            return chance > 1f ? 1f : chance;
        }

        // ---------------------------------------------------------------- the turbo

        /// <summary>Boost after this frame, 0 to 1: building towards the throttle times the revs' share with the turbo's spool, dying with the throttle shut.</summary>
        public static float Boost(float boost, float throttle, float revs, float dt, in TurboSound turbo)
        {
            if (turbo.SpoolSeconds <= 0f)
            {
                return 0f;
            }
            float byRevs = (revs - BoostFromRevs) / (BoostFullRevs - BoostFromRevs);
            byRevs = byRevs < 0f ? 0f : byRevs > 1f ? 1f : byRevs;
            float want = (throttle < 0f ? 0f : throttle > 1f ? 1f : throttle) * byRevs;
            float tau = want > boost ? turbo.SpoolSeconds : BoostFall;
            float k = dt <= 0f ? 0f : 1f - (float)Math.Exp(-dt / tau);
            float next = boost + (want - boost) * k;
            return next < 0f ? 0f : next > 1f ? 1f : next;
        }

        /// <summary>The whistle's volume at this boost (rising as its square: a whisper at low boost).</summary>
        public static float WhistleVolume(float boost, in TurboSound turbo)
        {
            return turbo.WhistleVolume * boost * boost;
        }

        /// <summary>The whistle's pitch at this boost.</summary>
        public static float WhistlePitch(float boost, in TurboSound turbo)
        {
            return turbo.PitchLow + (turbo.PitchHigh - turbo.PitchLow) * boost;
        }

        /// <summary>Whether shutting the throttle now flutters: a turbo fitted, a lift-off, and boost of at least <see cref="FlutterFrom"/>.</summary>
        public static bool Flutters(in TurboSound turbo, bool lifted, float boost)
        {
            return turbo.FlutterVolume > 0f && lifted && boost >= FlutterFrom;
        }

        // ---------------------------------------------------------------- the shop's turntable

        /// <summary>The revs after this frame on the turntable: towards <see cref="ShopRevTop"/> of the limit while the key is held, back to idle when it is not.</summary>
        public static float ShopRevs(float rpm, bool held, float idleRpm, float limitRpm, float dt)
        {
            float idle = idleRpm > 100f ? idleRpm : 800f;
            float top = limitRpm > idle ? limitRpm * ShopRevTop : idle * 5f;
            if (rpm < idle)
            {
                rpm = idle;
            }
            if (dt <= 0f)
            {
                return rpm;
            }
            if (held)
            {
                return rpm + (top - rpm) * (1f - (float)Math.Exp(-dt / ShopRise));
            }
            float above = rpm - idle;
            if (above > ShopFall * ShopFallTail)
            {
                float next = rpm - ShopFall * dt;                         // a steady fall
                return next < idle ? idle : next;
            }
            return idle + above * (float)Math.Exp(-dt / ShopFallTail);   // the last of it eases onto idle, at the same rate where the two meet
        }

        // ---------------------------------------------------------------- tyres

        /// <summary>
        /// One wheel's squeal, 0 to 1, as the game's own skid sound reckons it (WheelSkidComponent): its slip, times how fast
        /// it moves, so a car spinning on the spot or crawling squeals little.
        /// </summary>
        public static float Squeal(float slip, float speed, float wheelSpin)
        {
            float s = slip < 0f ? 0f : slip > 1f ? 1f : slip;
            float moving = speed * 0.33f + Math.Abs(wheelSpin) * 0.05f;
            return s * (moving > 1f ? 1f : moving);
        }

        /// <summary>The squeal heard after this frame: eased towards the loudest wheel's at ten a second, as the game's.</summary>
        public static float SquealEased(float heard, float loudest, float dt)
        {
            float k = dt * 10f;
            k = k < 0f ? 0f : k > 1f ? 1f : k;
            return heard + (loudest - heard) * k;
        }

        // ---------------------------------------------------------------- the clips

        private static string[] Clips()
        {
            int n = Pops.Length + 3;
            EngineVoice[] voices = { EngineVoice.Petrol, EngineVoice.V8, EngineVoice.Sport, EngineVoice.Diesel };
            foreach (EngineVoice v in voices)
            {
                foreach (EngineLoop loop in Loops(v))
                {
                    n += loop.HasOff ? 2 : 1;
                }
            }
            var all = new string[n];
            int k = 0;
            foreach (EngineVoice v in voices)
            {
                foreach (EngineLoop loop in Loops(v))
                {
                    all[k++] = loop.Clip;
                    if (loop.HasOff)
                    {
                        all[k++] = loop.Clip + OffSuffix;
                    }
                }
            }
            foreach (string pop in Pops)
            {
                all[k++] = pop;
            }
            all[k++] = WhistleClip;
            all[k++] = FlutterClip;
            all[k] = SquealClip;
            return all;
        }
    }
}
