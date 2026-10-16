using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's night, as Core/DayRollGuard keeps it whole. The game's new day (GameManager.NewDay) runs its steps one
    /// after another with nothing between them: the hour event, the day's wages (EmployeeHelper.PayDailyWages, each
    /// employee's hours of the day times their wage), the businesses' day, the rivals' new businesses
    /// (CompetitionHelper.RunDaily), the day event every feature of this mod waits for (GlobalEvents.onNewDay), and only
    /// near the end the employees' day (EmployeeHelper.RunDaily), which clears each one's hours of the day and, on a
    /// Monday, of the week. When one step throws, the rest of that night never runs. A business type of another mod that
    /// is no longer loaded leaves its goods in the city's market (GameInstance.productMarketEntries); the rivals' step
    /// looks such goods up, finds nothing and throws, on every night it is due. Then nobody's hours are ever cleared: each
    /// night pays every hour since the hire again, so a business's wages grow day by day until its staff are fired and
    /// hired again (a hire starts at nothing), the week's hours pass what anybody asks for, and the mod's day (brands,
    /// the company, the economy) never comes.
    ///
    /// These are the bounds the guard puts right by: an employee's hours of a day can be no more than the day has, nor
    /// more than their shifts on it; their hours of a week no more than their shifts from its Monday to that day. They are applied only where
    /// there is proof the clearing was missed (<see cref="Missed"/>, <see cref="Mend"/>), so a day that ran as the game
    /// meant is never touched.
    ///
    /// Game-free (no Unity or game types): checked offline (EconomyCheck, DayRollChecks.cs). Cost: a few integer
    /// operations for each employee once a game day; nothing per frame.
    /// </summary>
    public static class DayRollRules
    {
        public const int DayHours = 24;   // hours: the most anybody can be counted working in one day (EmployeeInstance.RunHourly adds one an hour on shift)

        /// <summary>
        /// True if the night before was seen to start (its first hour, on <paramref name="rollDay"/>) and its day event never
        /// came (the last one seen was for <paramref name="doneDay"/>): it stopped before the employees' day, so the hours
        /// it should have cleared were not. A first night after a load has nothing seen before it, and is not taken as missed.
        /// </summary>
        public static bool Missed(int rollDay, int doneDay, int today)
        {
            return rollDay > 0 && rollDay == today - 1 && doneDay < rollDay;
        }

        /// <summary>
        /// The hours of the day that has ended an employee is paid for tonight. With the clearing missed, or more counted
        /// than a day has (it can only be from nights before), no more than their shifts on that day and never more than a
        /// day; otherwise as the game counted them.
        /// </summary>
        public static int Mend(int worked, int shifts, bool missed)
        {
            worked = Math.Max(0, worked);
            if (!missed && worked <= DayHours)
            {
                return worked;
            }
            return Math.Min(worked, Clamp(shifts, 0, DayHours));
        }

        /// <summary>The hours of the week of an employee whose day was mended: no more than their shifts from the week's Monday to the day that ended (the hours a week they ask for are measured against this count, EmployeeInstance.workedHoursThisWeek).</summary>
        public static int Week(int week, int shifts)
        {
            return Math.Min(Math.Max(0, week), Math.Max(0, shifts));
        }

        /// <summary>
        /// True at the hour after a night (seen to start on <paramref name="today"/>) whose day event never came: it stopped
        /// before the employees' day, which the guard then does in its place.
        /// </summary>
        public static bool Stopped(int rollDay, int doneDay, int today)
        {
            return rollDay > 0 && rollDay == today && doneDay < today;
        }

        /// <summary>An employee's hours of the day as the employees' day leaves them: cleared (the night paid them).</summary>
        public static int Cleared(int worked)
        {
            return 0;
        }

        /// <summary>An employee's hours (or days) of the week as the employees' day leaves them: cleared on a Monday, otherwise as they are.</summary>
        public static int ClearedWeek(int week, bool monday)
        {
            return monday ? 0 : Math.Max(0, week);
        }

        /// <summary>The days of the week an employee whose day was mended is counted at work (EmployeeInstance.workedDays, which the days a week they ask for are measured against): no more than their days with a shift from the week's Monday to the day that ended.</summary>
        public static int Days(int days, int shiftDays)
        {
            return Math.Min(Math.Max(0, days), Math.Max(0, shiftDays));
        }

        /// <summary>
        /// True if an employee's counts are to be looked at tonight: the clearing was missed, or more is counted than a day
        /// has (it can only be from nights before). A day the game counted as it meant is not looked at: one comparison.
        /// </summary>
        public static bool Suspect(int worked, bool missed)
        {
            return missed || worked > DayHours;
        }

        private static int Clamp(int value, int least, int most)
        {
            return value < least ? least : value > most ? most : value;
        }
    }
}
