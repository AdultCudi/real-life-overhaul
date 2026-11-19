using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// A raise for everyone shown on the Open Roles team page in one go: the shares to choose from, and the sum of what it would
    /// do, which the page shows and the confirmation repeats before anything is given. Each raise is one raise of the page's own
    /// rule (the 14 day rest and the cap of WageRules), so the sum is exactly what is given. Game-free: EconomyCheck runs it.
    /// No cost a frame: summed when the page is written, and once more when the raise is given.
    /// </summary>
    public static class BulkRaiseRules
    {
        public static readonly int[] Percents = { 3, 5, 8, 10, 15, 20 };   // per cent of the pay a raise for all can be
        public const int StartAt = 1;                                      // the share the page starts on: 5%

        public static int At(int index) => Math.Max(0, Math.Min(Percents.Length - 1, index));

        /// <summary>The raise of a share in basis points.</summary>
        public static int Bp(int index) => Percents[At(index)] * 100;

        /// <summary>The sum of a raise for all. Hourly figures are hundredths an hour; the month is the save's calendar month (<see cref="RivalMatchRules.Monthly"/>).</summary>
        public struct Plan
        {
            public int Shown, Raised, Rested, Leaving, Capped;
            public long BeforeHourly, AfterHourly;   // those who are raised, an hour

            public bool Any => Raised > 0;
            public int Left => Shown - Raised;
            public long ExtraMonthly(int monthDays) => RivalMatchRules.Monthly(AfterHourly - BeforeHourly, monthDays);
            public long TotalMonthly(int monthDays) => RivalMatchRules.Monthly(AfterHourly, monthDays);

            /// <summary>Adds one person: what they cost now and would after the raise, and whether they may have it.</summary>
            public void Add(int before, int after, bool rested, bool leaving)
            {
                Shown++;
                if (leaving)
                {
                    Leaving++;
                }
                else if (rested)
                {
                    Rested++;
                }
                else if (after <= before)
                {
                    Capped++;
                }
                else
                {
                    Raised++;
                    BeforeHourly += before;
                    AfterHourly += after;
                }
            }
        }
    }
}
