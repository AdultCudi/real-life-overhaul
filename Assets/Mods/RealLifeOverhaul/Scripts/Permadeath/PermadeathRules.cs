using System;

namespace RealLifeOverhaul.Permadeath
{
    /// <summary>What ended a life, or nearly did. Saved as a number (in the record and as a bit of what was warned of): a new cause goes at the end.</summary>
    public enum DeathCause
    {
        None = 0,
        Crash = 1,       // the player's own car, at a very high speed of impact
        Struck = 2,      // on foot, hit by a fast vehicle
        Illness = 3,     // a flu left untreated long past its warnings
        Mugging = 4,     // a mugger refused, and it went badly
        Assault = 5,     // the private lender's men, over a debt long past its day
        Test = 6         // the options page's rehearsal: nobody died
    }

    /// <summary>What one dangerous moment comes to while Permadeath is on.</summary>
    public enum Verdict
    {
        None,        // as without Permadeath: an injury at most
        NearMiss,    // the first time of its kind: the injury and a notice that says the next one could be fatal
        Death        // the character's life ends here
    }

    /// <summary>The moment as the rules need it, read by whoever asks (Permadeath/PermadeathGame): five flags.</summary>
    public struct Moment
    {
        public bool Armed;              // Permadeath is on in this save
        public bool Dead;               // the character has died already
        public bool InSession;          // a Going Public session is on, hosted or joined
        public bool MissionOrTutorial;  // the game's tutorial, a side quest or one of its missions is running
        public bool Busy;               // the game is elsewhere: loading, a hospital stay, its own funeral, a time skip, a death under way
    }

    /// <summary>
    /// The rules of Permadeath, in plain numbers: when a dangerous moment can end a life, the odds, the warnings that come
    /// first, the illness's course, and whom an option switched in the main menu applies to. No Unity or game types: the
    /// checks (EconomyCheck) run this file as it ships, so the Guide's figures are these figures.
    ///
    /// The shape of every danger is the same: below its threshold it is what it always was (an injury at most); at or
    /// above it the first time is a near miss, which hurts as before and says plainly that the next time could be fatal;
    /// only after that near miss can it kill, and then with odds that rise with how bad the moment was and stop well short
    /// of certainty. Nothing that the player cannot see coming is here: life events, crimes watched from the pavement,
    /// fires and other people's accidents never come to this file.
    /// </summary>
    public static class PermadeathRules
    {
        // ---- a crash in the player's own car: the relative speed of the impact, m/s (the game's collision)
        public const float CrashFrom = 25f;          // 90 km/h: below it a crash hurts, never kills (the hospital is from 61 km/h)
        public const float CrashWorst = 40f;         // 144 km/h: the odds stop rising here
        public const float CrashLeast = 0.05f;       // at CrashFrom, once warned
        public const float CrashMost = 0.35f;        // from CrashWorst

        // ---- on foot, hit by a vehicle: the vehicle's speed, m/s
        public const float StruckFrom = 55f / 3.6f;  // 55 km/h (the hospital is from 35 km/h)
        public const float StruckWorst = 90f / 3.6f; // 90 km/h
        public const float StruckLeast = 0.05f;
        public const float StruckMost = 0.30f;

        // ---- a mugger refused: of the refusals that end with the player hurt (half of them)
        public const float MuggingOdds = 0.10f;      // one hurt in ten, once warned

        // ---- the private lender's men: of their beatings
        public const float AssaultOdds = 0.08f;      // about one in twelve, once warned

        // ---- a flu left untreated: hours since it began with no medicine, doctor or hospital
        public const int IllWarnFirst = 48;          // two days: it gets worse, and no longer clears up by itself
        public const int IllWarnSecond = 72;         // three days
        public const int IllWarnLast = 96;           // four days
        public const int IllFatalFrom = 120;         // five days: from here each further hour may be the last
        public const float IllPerDay = 0.25f;        // the chance over a whole further day untreated
        public const int IllHoldMinutes = 24 * 60;   // how far ahead the illness is held each hour once it no longer clears: past a night's sleep and the hours rest takes off (Home/HomeBalance.RestCure), so that sleeping is no cure

        public const int IllStages = 4;              // 0 nothing yet, 1 to 3 the warnings had, 4 the fatal stretch

        /// <summary>The chance in one game hour of the fatal stretch, so that a whole day comes to <see cref="IllPerDay"/>.</summary>
        public static float IllPerHour => (float)(1.0 - Math.Pow(1.0 - IllPerDay, 1.0 / 24.0));

        /// <summary>True while a dangerous moment may end a life at all: on in this save, alive, alone, the game's own story not running, nothing else under way.</summary>
        public static bool Allowed(Moment m)
        {
            return m.Armed && !m.Dead && !m.InSession && !m.MissionOrTutorial && !m.Busy;
        }

        /// <summary>True when a moment of this kind is bad enough to be fatal: <paramref name="measure"/> is a speed for the two collisions, ignored for the others.</summary>
        public static bool Fatal(DeathCause cause, float measure)
        {
            switch (cause)
            {
                case DeathCause.Crash: return measure >= CrashFrom;
                case DeathCause.Struck: return measure >= StruckFrom;
                case DeathCause.Mugging:
                case DeathCause.Assault: return true;
                default: return false;   // the illness has its own course (IllStage); a test kills nobody
            }
        }

        /// <summary>The chance that a moment this bad ends a life, once its near miss has been had. Zero below the threshold; never above 0.35.</summary>
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
        /// One dangerous moment: nothing unless it is allowed and bad enough; the first of its kind a near miss; after that a
        /// death if <paramref name="roll"/> (0 to 1) falls under the odds. <paramref name="warned"/> is whether this save has
        /// had the near miss of this kind.
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

        // ---------------------------------------------------------------- the illness

        /// <summary>How far a flu has gone after so many hours untreated: 0 nothing yet, 1 to 3 a warning each, 4 the fatal stretch.</summary>
        public static int IllStage(float hoursUntreated)
        {
            if (hoursUntreated >= IllFatalFrom) return 4;
            if (hoursUntreated >= IllWarnLast) return 3;
            if (hoursUntreated >= IllWarnSecond) return 2;
            if (hoursUntreated >= IllWarnFirst) return 1;
            return 0;
        }

        /// <summary>True once a flu no longer clears up by itself: from the first warning, while it stays untreated.</summary>
        public static bool IllHeld(int stage)
        {
            return stage >= 1;
        }

        /// <summary>
        /// One hour of a flu: the next warning due, if the one shown last is behind the course (<paramref name="told"/>, 0 to
        /// 3), or 0; and whether this hour is the last. Only one warning a call, so that a long sleep that jumped two stages
        /// still says each in turn, an hour apart, before the fatal stretch can begin; and never death before the third
        /// warning has been shown.
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
                warn = told + 1;   // the next warning, in order
                return Verdict.NearMiss;
            }
            if (stage >= IllStages && told >= IllStages - 1 && roll < IllPerHour)
            {
                return Verdict.Death;
            }
            return Verdict.None;
        }

        // ---------------------------------------------------------------- the switch

        /// <summary>
        /// What a save starts with the first time it is read with this feature: only a game just begun, and only when the
        /// switch was moved on in the main menu of this run; any save that was already going starts with it off.
        /// </summary>
        public static bool FirstArmed(bool fresh, bool newGame, bool armNext)
        {
            return fresh && newGame && armNext;
        }

        /// <summary>
        /// What the switch, moved to <paramref name="on"/>, does to the save: true if it changes it. A death is never undone
        /// and nothing is switched on in a session; switching off is always allowed while the character lives.
        /// </summary>
        public static bool Switches(bool on, bool armed, bool dead, bool inSession)
        {
            if (dead || on == armed)
            {
                return false;
            }
            return !on || !inSession;
        }

        /// <summary>True when a save is read as ended: its own record says so, or it is an older save of a character who died since, from a time it was on.</summary>
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
