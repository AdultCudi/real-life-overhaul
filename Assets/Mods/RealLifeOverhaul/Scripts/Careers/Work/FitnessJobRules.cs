using System;

namespace RealLifeOverhaul.Careers
{
    /// <summary>What a day's look at a worker's fitness decides.</summary>
    public enum FitnessCheck
    {
        None,       // nothing to say
        Warn,       // far below what the job asks, first time: a warning, with days to put it right
        Strike,     // still far below when the days are up: one more warning against the worker (never the last)
        Clear       // back within reach of what the job asks: the warning is lifted
    }

    /// <summary>
    /// The fitness the physical jobs ask for, and what happens to a worker who falls far below it. Game-free: EconomyCheck (--fitness29) runs this file as it ships.
    ///
    /// Who asks: the jobs where the body is the work. The police ask the most (the academy's fitness test, which the application is judged against), then the warehouse and
    /// the security guard, then the building trades. Every other profession asks nothing. A new game starts at fitness 30, so every job but the police is open to a new
    /// player and stays open through a fortnight of doing nothing, and the police are a fortnight or so of running away, which is the point of them.
    ///
    /// Someone already in the job: far below (10 points under the minimum) is a warning with 14 days to put it right; still far below after that, one warning against
    /// the worker (the careers' own ladder of warnings) each 14 days; but a fitness strike can only ever bring the worker to the last warning, never past it, so
    /// fitness alone never loses anyone their job and the next missed day is what it would cost them. Back within reach lifts the warning. With the option off nothing here is asked.
    /// </summary>
    public static class FitnessJobRules
    {
        public const int PoliceMinimum = 45;           // the police academy's fitness test: well past a new game's 30, about a fortnight of regular running
        public const int HeavyMinimum = 20;            // the warehouse and the security guard: a new game's 30 is enough, and so is a fortnight of doing nothing past the grace (0.7 a day)
        public const int TradeMinimum = 15;            // the building trades: carrying and climbing, but all day at a lower pace
        public const int FarBelow = 10;                // points under the minimum that make a worker "far below"
        public const int WarnDays = 14;                // days from a warning, and between strikes, before the next step
        public const int WeeklyGain = 18;              // points a week of three sessions of exercise adds (a 40 minute machine session is 6): how long "to get there" is told

        /// <summary>The fitness a profession asks (a <see cref="ProCatalogue"/> id), or 0 for a profession that asks nothing.</summary>
        public static int Minimum(int prof)
        {
            switch (prof)
            {
                case ProCatalogue.Police: return PoliceMinimum;
                case ProCatalogue.Warehouse: return HeavyMinimum;
                case ProCatalogue.SecurityGuard: return HeavyMinimum;
                case ProCatalogue.Carpenter: return TradeMinimum;
                case ProCatalogue.Plumber: return TradeMinimum;
                case ProCatalogue.Electrician: return TradeMinimum;
                default: return 0;
            }
        }

        public static bool Physical(int prof) => Minimum(prof) > 0;

        /// <summary>
        /// Whether the fitness holds a player back from applying for a profession: true when the option is on, the profession asks for fitness, fitness is known (not -1) and
        /// under it. <paramref name="need"/> is the figure asked, <paramref name="have"/> the player's.
        /// </summary>
        public static bool Short(int prof, int fitness, bool on, out int need)
        {
            need = Minimum(prof);
            return on && need > 0 && fitness >= 0 && fitness < need;
        }

        /// <summary>Whole weeks of regular exercise (three sessions a week) it takes to close a gap of points: at least one.</summary>
        public static int Weeks(int gap) => gap <= WeeklyGain ? 1 : (gap + WeeklyGain - 1) / WeeklyGain;

        /// <summary>How far off the refusal says the player is: 0 a few runs, 1 a week or two, 2 a few weeks.</summary>
        public static int Tier(int gap) => gap <= 6 ? 0 : gap <= WeeklyGain ? 1 : 2;

        /// <summary>
        /// A day's look at a worker already in the job. <paramref name="warnDay"/> and <paramref name="strikeDay"/> are what the job remembers (0 none); a strike is only given
        /// while the worker has fewer than <paramref name="warningsToGo"/> - 1 warnings, so the last one is never made by fitness.
        /// </summary>
        public static FitnessCheck Judge(int prof, int fitness, bool on, int warnDay, int strikeDay, int warnings, int warningsToGo, int day)
        {
            int minimum = Minimum(prof);
            if (!on || minimum <= 0 || fitness < 0)
            {
                return FitnessCheck.None;
            }
            if (fitness >= minimum - FarBelow)
            {
                return warnDay > 0 ? FitnessCheck.Clear : FitnessCheck.None;
            }
            if (warnDay <= 0)
            {
                return FitnessCheck.Warn;
            }
            int last = Math.Max(warnDay, strikeDay);
            return day - last >= WarnDays && warnings < warningsToGo - 1 ? FitnessCheck.Strike : FitnessCheck.None;
        }

        /// <summary>The day the warning's time is up for a worker warned on <paramref name="warnDay"/>.</summary>
        public static int Deadline(int warnDay) => warnDay + WarnDays;

        // A refusal's variants: which of the wordings a day gives for a figure and a job, from a hash of the three: the same moment reads the same, another day another.
        public static int Variant(int prof, int fitness, int day, int count)
        {
            if (count <= 1)
            {
                return 0;
            }
            unchecked
            {
                uint h = (uint)prof * 2654435761u ^ (uint)fitness * 2246822519u ^ (uint)day * 3266489917u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (int)(h % (uint)count);
            }
        }

        public static readonly string[] NearTips =
        {
            "reallife_fitness_tip_near_1", "reallife_fitness_tip_near_2", "reallife_fitness_tip_near_3", "reallife_fitness_tip_near_4"
        };

        public static readonly string[] MidTips =
        {
            "reallife_fitness_tip_mid_1", "reallife_fitness_tip_mid_2", "reallife_fitness_tip_mid_3", "reallife_fitness_tip_mid_4"
        };

        public static readonly string[] FarTips =
        {
            "reallife_fitness_tip_far_1", "reallife_fitness_tip_far_2", "reallife_fitness_tip_far_3", "reallife_fitness_tip_far_4"
        };

        /// <summary>The key of the sentence that says how to get there, for a gap, chosen by the day.</summary>
        public static string TipKey(int prof, int fitness, int gap, int day)
        {
            string[] tips = Tier(gap) == 0 ? NearTips : Tier(gap) == 1 ? MidTips : FarTips;
            return tips[Variant(prof, fitness, day, tips.Length)];
        }

        public static readonly string[] WarnTexts =
        {
            "reallife_fitness_text_warning_1", "reallife_fitness_text_warning_2", "reallife_fitness_text_warning_3"
        };

        public static readonly string[] StrikeTexts =
        {
            "reallife_fitness_text_strike_1", "reallife_fitness_text_strike_2", "reallife_fitness_text_strike_3"
        };
    }
}
