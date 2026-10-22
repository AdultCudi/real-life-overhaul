using System;

namespace RealLifeOverhaul.Permadeath
{
    /// <summary>What ended a life, or nearly did. Saved as a number and as a bit of what was warned of: new causes go at the end.</summary>
    public enum DeathCause
    {
        None = 0,
        Crash = 1,       // the player's own car, at a very high speed of impact
        Struck = 2,      // on foot, hit by a fast vehicle
        Illness = 3,     // a flu left untreated long past its warnings
        Mugging = 4,     // a mugger refused, and it went badly
        Assault = 5,     // the private lender's men, over a debt long past its day
        Test = 6,        // the options page's rehearsal: nobody died
        Starvation = 7   // days on an empty stomach, long past its warnings
    }

    /// <summary>What one dangerous moment comes to while Permadeath is on.</summary>
    public enum Verdict
    {
        None,        // as without Permadeath: an injury at most
        NearMiss,    // the first of its kind: the injury, and a notice that the next could be fatal
        Death
    }

    /// <summary>The moment as the rules need it, read by PermadeathGame.</summary>
    public struct Moment
    {
        public bool Armed;              // Permadeath is on in this save
        public bool Dead;               // the character has died already
        public bool InSession;          // a Going Public session is on, hosted or joined
        public bool MissionOrTutorial;  // the game's tutorial, a side quest or one of its missions is running
        public bool Busy;               // loading, a hospital stay, its own funeral, a time skip, a death under way
    }

    /// <summary>
    /// The rules of Permadeath in plain numbers: when a dangerous moment can end a life, the odds, the warnings, the
    /// illness's course and the switch. Game-free; EconomyCheck runs this file as it ships, so the Guide's figures are these.
    /// Every danger has one shape: below its threshold it hurts as it always did; above it the first time is a near miss
    /// that says the next could be fatal, and only after that can it kill, with odds that rise and stop well short of
    /// certainty. Nothing the player cannot see coming is here.
    /// </summary>
    public static class PermadeathRules
    {
        // A crash in the player's own car: the relative speed of the impact (the game's collision).
        public const float CrashFrom = 25f;          // m/s, 90 km/h: below it a crash hurts, never kills (the hospital is from 61 km/h)
        public const float CrashWorst = 40f;         // m/s, 144 km/h: the odds stop rising here
        public const float CrashLeast = 0.05f;       // at CrashFrom, once warned
        public const float CrashMost = 0.35f;        // from CrashWorst

        // On foot, hit by a vehicle: the vehicle's speed.
        public const float StruckFrom = 55f / 3.6f;  // m/s, 55 km/h (the hospital is from 35 km/h)
        public const float StruckWorst = 90f / 3.6f; // m/s, 90 km/h
        public const float StruckLeast = 0.05f;
        public const float StruckMost = 0.30f;

        public const float MuggingOdds = 0.10f;      // of refusals that end with the player hurt (half of them), once warned
        public const float AssaultOdds = 0.08f;      // of the private lender's beatings, once warned

        // A flu left untreated: hours since it began with no medicine, doctor or hospital.
        public const int IllWarnFirst = 48;          // hours: it gets worse, and no longer clears up by itself
        public const int IllWarnSecond = 72;         // hours
        public const int IllWarnLast = 96;           // hours
        public const int IllFatalFrom = 120;         // hours: from here each further hour may be the last
        public const float IllPerDay = 0.25f;        // the chance over a whole further day untreated
        // Game minutes the illness is held ahead each hour once it no longer clears: past a night's sleep and what rest
        // takes off (Home/HomeBalance.RestCure), so sleeping is no cure.
        public const int IllHoldMinutes = 24 * 60;

        public const int IllStages = 4;              // 0 nothing yet, 1 to 3 the warnings had, 4 the fatal stretch

        // Going without food: hours with the game's hunger bar at its bottom. The bar falls about 3.6 an hour just living
        // (1.5 per energy point, EnergyHelper.SpentEnergyOnce), so a full stomach is empty within a day; at its bottom the
        // game already burns energy faster and walks the player as a zombie once energy is gone too.
        public const float HungerEmpty = 5f;          // under this the bar is at its worst (the game's own "too low" face)
        public const float HungerAte = 0.5f;          // a rise of this much in a second is something eaten
        public const int StarveWarnFirst = 24;        // hours: weak with hunger (a low mood)
        public const int StarveWarnSecond = 48;       // hours: faint, and tiring faster
        public const int StarveWarnLast = 72;         // hours: starving
        public const int StarveFatalFrom = 96;        // hours: from here each further hour may be the last
        public const float StarvePerDay = 0.20f;      // the chance over a whole further day without food
        public const float StarveDrainFaint = 0.02f;  // energy a game minute from the second warning (just living is 0.04)
        public const float StarveDrainWorst = 0.04f;  // from the third
        public const int StarveMood = -10;            // happiness while weak with hunger, from the first warning

        /// <summary>The chance in one game hour of starvation's fatal stretch, so that a day comes to <see cref="StarvePerDay"/>.</summary>
        public static float StarvePerHour => (float)(1.0 - Math.Pow(1.0 - StarvePerDay, 1.0 / 24.0));

        /// <summary>The chance in one game hour of the illness's fatal stretch, so that a day comes to <see cref="IllPerDay"/>.</summary>
        public static float IllPerHour => (float)(1.0 - Math.Pow(1.0 - IllPerDay, 1.0 / 24.0));

        /// <summary>True while a moment may end a life at all: on in this save, alive, alone, no mission, nothing under way.</summary>
        public static bool Allowed(Moment m)
        {
            return m.Armed && !m.Dead && !m.InSession && !m.MissionOrTutorial && !m.Busy;
        }

        /// <summary>True when a moment of this kind is bad enough to be fatal; <paramref name="measure"/> is the speed of a collision.</summary>
        public static bool Fatal(DeathCause cause, float measure)
        {
            switch (cause)
            {
                case DeathCause.Crash: return measure >= CrashFrom;
                case DeathCause.Struck: return measure >= StruckFrom;
                case DeathCause.Mugging:
                case DeathCause.Assault: return true;
                default: return false;   // the illness and hunger have their own course; a test kills nobody
            }
        }

        /// <summary>The chance that a moment this bad ends a life once its near miss has been had: zero below the threshold.</summary>
        public static float Odds(DeathCause cause, float measure)
        {
            if (!Fatal(cause, measure))
            {
                return 0f;
            }
            switch (cause)
            {
                case DeathCause.Crash: return Lerp(CrashLeast, CrashMost, Between(CrashFrom, CrashWorst, measure));
                case DeathCause.Struck: return Lerp(StruckLeast, StruckMost, Between(StruckFrom, StruckWorst, measure));
                case DeathCause.Mugging: return MuggingOdds;
                case DeathCause.Assault: return AssaultOdds;
                default: return 0f;
            }
        }

        /// <summary>
        /// One dangerous moment: nothing unless allowed and bad enough, a near miss unless <paramref name="warned"/>, then a
        /// death if <paramref name="roll"/> (0 to 1) falls under the odds.
        /// </summary>
        public static Verdict Judge(DeathCause cause, float measure, bool warned, Moment m, float roll)
        {
            if (!Allowed(m) || !Fatal(cause, measure))
            {
                return Verdict.None;
            }
            if (!warned)
            {
                return Verdict.NearMiss;
            }
            return roll < Odds(cause, measure) ? Verdict.Death : Verdict.None;
        }

        /// <summary>How far a flu has gone after so many hours untreated: 0 nothing yet, 1 to 3 a warning each, 4 the fatal stretch.</summary>
        public static int IllStage(float hoursUntreated)
        {
            if (hoursUntreated >= IllFatalFrom)
            {
                return 4;
            }
            if (hoursUntreated >= IllWarnLast)
            {
                return 3;
            }
            if (hoursUntreated >= IllWarnSecond)
            {
                return 2;
            }
            if (hoursUntreated >= IllWarnFirst)
            {
                return 1;
            }
            return 0;
        }

        /// <summary>True once a flu no longer clears up by itself: from the first warning, while it stays untreated.</summary>
        public static bool IllHeld(int stage)
        {
            return stage >= 1;
        }

        /// <summary>
        /// One hour of a flu: <paramref name="warn"/> is the next warning due after the <paramref name="told"/> shown, or 0,
        /// and the verdict says whether this hour is the last. One warning a call, so a long sleep still shows each in turn,
        /// and never death before the third.
        /// </summary>
        public static Verdict IllHour(float hoursUntreated, int told, Moment m, float roll, out int warn)
        {
            warn = 0;
            if (!Allowed(m))
            {
                return Verdict.None;
            }
            int stage = IllStage(hoursUntreated);
            if (told < stage && told < IllStages - 1)
            {
                warn = told + 1;
                return Verdict.NearMiss;
            }
            if (stage >= IllStages && told >= IllStages - 1 && roll < IllPerHour)
            {
                return Verdict.Death;
            }
            return Verdict.None;
        }

        /// <summary>How far going without food has gone after so many hours at the bar's bottom, staged as <see cref="IllStage"/>.</summary>
        public static int StarveStage(float hoursEmpty)
        {
            if (hoursEmpty >= StarveFatalFrom)
            {
                return 4;
            }
            if (hoursEmpty >= StarveWarnLast)
            {
                return 3;
            }
            if (hoursEmpty >= StarveWarnSecond)
            {
                return 2;
            }
            if (hoursEmpty >= StarveWarnFirst)
            {
                return 1;
            }
            return 0;
        }

        public static bool Empty(float hunger)
        {
            return hunger < HungerEmpty;
        }

        /// <summary>True when the bar rose enough from one look to the next to be something eaten (only food and the hospital raise it).</summary>
        public static bool Ate(float before, float now)
        {
            return now - before >= HungerAte;
        }

        /// <summary>The extra energy a game minute the weakness costs after <paramref name="told"/> warnings (0 to 3).</summary>
        public static float StarveDrain(int told)
        {
            return told >= 3 ? StarveDrainWorst : told >= 2 ? StarveDrainFaint : 0f;
        }

        /// <summary>
        /// One hour on an empty stomach, as <see cref="IllHour"/> is for a flu. An hour that is not <paramref name="safe"/>
        /// for a death (asleep, a dialog, a ride or activity, in hospital) warns but never kills.
        /// </summary>
        public static Verdict StarveHour(float hoursEmpty, int told, Moment m, bool safe, float roll, out int warn)
        {
            warn = 0;
            if (!Allowed(m))
            {
                return Verdict.None;
            }
            int stage = StarveStage(hoursEmpty);
            if (told < stage && told < IllStages - 1)
            {
                warn = told + 1;
                return Verdict.NearMiss;
            }
            if (safe && stage >= IllStages && told >= IllStages - 1 && roll < StarvePerHour)
            {
                return Verdict.Death;
            }
            return Verdict.None;
        }

        /// <summary>
        /// Whether a save read for the first time with this feature starts on: only a game just begun, with the switch moved
        /// on in this run's main menu.
        /// </summary>
        public static bool FirstArmed(bool fresh, bool newGame, bool armNext)
        {
            return fresh && newGame && armNext;
        }

        /// <summary>
        /// True if moving the switch to <paramref name="on"/> changes the save: a death is never undone, nothing is switched
        /// on in a session, and switching off is always allowed while the character lives.
        /// </summary>
        public static bool Switches(bool on, bool armed, bool dead, bool inSession)
        {
            if (dead || on == armed)
            {
                return false;
            }
            return !on || !inSession;
        }

        /// <summary>True when a save is read as ended: its record says so, or it is an older save, from a time it was on, of a character who died since.</summary>
        public static bool Ended(bool dead, bool armed, bool diedElsewhere)
        {
            return dead || (armed && diedElsewhere);
        }

        private static float Between(float from, float to, float value)
        {
            if (to <= from)
            {
                return value >= to ? 1f : 0f;
            }
            float t = (value - from) / (to - from);
            return t < 0f ? 0f : t > 1f ? 1f : t;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }
    }
}
