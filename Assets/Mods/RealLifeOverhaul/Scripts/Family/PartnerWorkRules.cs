using System;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// The partner's work as the player's fortune grows, game-free: once the player's daily income has held at a sum all
    /// week and their cash reaches another, a partner who lives with them asks to cut back to half days; a no is asked again
    /// a month later; a yes halves their working day and their pay, and they can go back to full days from a family
    /// conversation. Nothing here is read per frame.
    /// </summary>
    public static class PartnerWorkRules
    {
        public const int FullTime = 0, PartTime = 1;
        public const int AskAtCash = 10000;      // the player's cash at the start's prices when the partner first asks...
        public const int AskAtIncome = 800;      // ...with the game's daily income held at least this all week (IncomeRules, the start's prices)
        public const int AskAgainDays = 30;      // days after a no before they ask again
        public const int PartTimeHours = 4;      // hours of a working day on half days
        public const float HarmonyYes = 6f;      // the home, when the player says yes
        public const float HarmonyNo = -3f;      // ...and when they say not yet
        public const float AffectionYes = 3f;

        /// <summary>Whether the partner asks today: the option on, living together, on full days, the income held all week and the cash reached, and no recent no.</summary>
        public static bool Asks(bool on, bool livesIn, int work, bool asking, float cash, int atCash, int day, int lastAskDay, int held, int atIncome)
        {
            return on && livesIn && work == FullTime && !asking && IncomeRules.Reached(held, cash, atIncome, atCash)
                   && (lastAskDay <= 0 || day - lastAskDay >= AskAgainDays);
        }

        /// <summary>The hour a working day ends: on half days four hours after it begins, else as it was.</summary>
        public static int Until(int from, int to, int work)
        {
            return work == PartTime ? Math.Min(to, from + PartTimeHours) : to;
        }

        /// <summary>What a day of their work brings in: half on half days.</summary>
        public static int Income(int fullDay, int work)
        {
            return work == PartTime ? fullDay / 2 : fullDay;
        }

        /// <summary>Whether they go to work at all: not once they have stopped, nor while they are studying.</summary>
        public static bool Works(int work) => work == FullTime || work == PartTime;
    }
}
