namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// The partner's bigger wish about work once the player is rich, game-free: at a million in cash a partner who lives with
    /// them and still works asks once either to stop working or to retrain for the job they always wanted (which of the two,
    /// and which job, from their seed); a no is asked again a month later. Retraining takes some days without pay, then they
    /// start the new job on full days. Nothing here is read per frame.
    /// </summary>
    public static class PartnerWishRules
    {
        public const int None = 0, Stop = 1, Retrain = 2;
        public const int AskAtCash = 1000000;    // the player's cash at the start's prices (the fourth of Social's wealth steps)
        public const int RetrainDays = 20;       // days of study without pay before the new job
        public const float HarmonyYes = 8f;
        public const float HarmonyNo = -4f;
        public const float AffectionYes = 4f;

        /// <summary>Whether they ask today: the option on, living together, still working, the cash reached, the wish not yet granted and no recent no.</summary>
        public static bool Asks(bool on, bool livesIn, int work, bool granted, bool asking, float cash, int atCash, int day, int lastAskDay)
        {
            return on && livesIn && (work == PartnerWorkRules.FullTime || work == PartnerWorkRules.PartTime) && !granted && !asking && cash >= atCash
                   && (lastAskDay <= 0 || day - lastAskDay >= PartnerWorkRules.AskAgainDays);
        }

        /// <summary>What they wish for, from their seed: about half would stop, half would retrain.</summary>
        public static int WishOf(int seed) => ((seed >> 4) & 1) == 0 ? Stop : Retrain;

        /// <summary>The job they always wanted, from their seed: never the one they have.</summary>
        public static int DreamOf(int seed, int current, int jobs)
        {
            if (jobs <= 1)
            {
                return current;
            }
            int dream = ((seed >> 12) & 0x7fffffff) % jobs;
            return dream == current ? (dream + 1) % jobs : dream;
        }

        // Chipping in for the rent while they earn nothing (stopped, or retired), from their savings, when the player asks.
        public const float ChipInHarmony = 60f;  // the home's harmony at which they agree
        public const int ChipInWeeks = 4;        // weeks they pay towards it
        public const int ChipInAgainDays = 14;   // days after an ask before the player can ask again
        public const float ChipInNo = -3f;       // the home, when they would rather not

        /// <summary>Whether they agree to chip in: only in a happy enough home.</summary>
        public static bool ChipsIn(float harmony) => harmony >= ChipInHarmony;

        /// <summary>What they put towards the rent a week: a quarter of a week's pay at the job they had, rounded to tens.</summary>
        public static int ChipInWeek(int dayPay) => (int)(System.Math.Round(dayPay * 7 / 4f / 10f) * 10);

        /// <summary>Whether the player can ask now: not while they chip in already, nor within a while of the last ask.</summary>
        public static bool CanAskChipIn(int day, int until, int lastAsk) => day >= until && (lastAsk <= 0 || day - lastAsk >= ChipInAgainDays);

        /// <summary>Studying for the new job until that day (exclusive).</summary>
        public static bool Studying(int retrainUntil, int day) => retrainUntil > day;
    }
}
