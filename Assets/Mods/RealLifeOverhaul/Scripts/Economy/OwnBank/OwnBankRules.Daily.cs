namespace RealLifeOverhaul.Economy
{
    /// <summary>The bank's profit a day, worked out of the month's sums it already keeps (nothing more is saved). Game-free: EconomyCheck runs it.</summary>
    public static partial class OwnBankRules
    {
        /// <summary>The days of the month being added up that have been run: none on the first morning of a month.</summary>
        public static int DaysRun(OwnBank b)
        {
            return b.LastDay - b.MonthFrom + 1 > 0 ? b.LastDay - b.MonthFrom + 1 : 0;
        }

        /// <summary>This month's profit so far over the days run, cents a day; nothing with no day run yet.</summary>
        public static long ProfitPerDayNow(OwnBank b)
        {
            int days = DaysRun(b);
            return days > 0 ? (b.MonthIncome - b.MonthCosts) / days : 0;
        }

        /// <summary>The month before's profit over its days, cents a day.</summary>
        public static long ProfitPerDayLast(OwnBank b, int monthDays)
        {
            return (b.LastIncome - b.LastCosts) / (monthDays > 0 ? monthDays : 1);
        }
    }
}
