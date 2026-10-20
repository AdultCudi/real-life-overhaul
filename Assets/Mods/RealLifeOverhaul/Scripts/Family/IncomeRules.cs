using System.Collections.Generic;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// The player's income held over a week, for what the partner says and wishes as the player prospers: every step wants a
    /// daily income kept for a week and the cash in hand, never the cash alone. FamilyState keeps the week (the game's own
    /// daily figure, once a day); the steps' figures are each feature's, at the start's prices.
    /// </summary>
    public static class IncomeRules
    {
        public const int Days = 7;   // the days an income must be held

        /// <summary>The week with today's income put in at the end, the oldest day let go past <see cref="Days"/>.</summary>
        public static void Record(List<int> week, float income)
        {
            week.Add(income > int.MaxValue ? int.MaxValue : income < 0f ? 0 : (int)income);
            while (week.Count > Days)
            {
                week.RemoveAt(0);
            }
        }

        /// <summary>The income held all week: the least of its days; nothing until a whole week is known.</summary>
        public static int Held(List<int> week)
        {
            if (week == null || week.Count < Days)
            {
                return 0;
            }
            int least = int.MaxValue;
            for (int i = week.Count - Days; i < week.Count; i++)
            {
                least = System.Math.Min(least, week[i]);
            }
            return least;
        }

        /// <summary>Whether a step is reached: the income held at <paramref name="atIncome"/> or more and the cash at <paramref name="atCash"/> or more.</summary>
        public static bool Reached(int held, float cash, int atIncome, int atCash) => held >= atIncome && cash >= atCash;
    }
}
