using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The rules Core/DayRollGuard keeps the game's night whole by. GameManager.NewDay runs its steps with no
    /// guard between them: the hour event, the wages, the rivals' step, the day event, and near the end the employees'
    /// day (EmployeeHelper.RunDaily), which clears the hours. When a step throws (the rivals' step on goods of a mod no
    /// longer loaded), the rest never runs: nobody's hours are cleared, each night pays every hour since the hire again,
    /// and the mod's day never comes. The bounds: a day's hours no more than the day, nor than the shifts on it; a week's
    /// no more than its shifts so far; applied only with proof the clearing was missed.
    /// Game-free, checked by EconomyCheck. COST: a few integer operations an employee a game day.
    /// </summary>
    public static class DayRollRules
    {
        public const int DayHours = 24;   // hours: the most anybody can be counted working in a day

        /// <summary>
        /// True if the night before was seen to start (on <paramref name="rollDay"/>) and its day event never came: the
        /// clearing was missed. The first night after a load is never taken as missed.
        /// </summary>
        public static bool Missed(int rollDay, int doneDay, int today)
        {
            return rollDay > 0 && rollDay == today - 1 && doneDay < rollDay;
        }

        /// <summary>
        /// The hours of the ended day paid tonight: with the clearing missed or more than a day counted, no more than the
        /// shifts on it, nor than a day; otherwise as the game counted them.
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

        /// <summary>A mended employee's hours of the week: no more than their shifts from Monday to the ended day.</summary>
        public static int Week(int week, int shifts)
        {
            return Math.Min(Math.Max(0, week), Math.Max(0, shifts));
        }

        /// <summary>True at the hour after a night, seen to start today, whose day event never came.</summary>
        public static bool Stopped(int rollDay, int doneDay, int today)
        {
            return rollDay > 0 && rollDay == today && doneDay < today;
        }

        /// <summary>A day's hours as the employees' day leaves them: cleared.</summary>
        public static int Cleared(int worked)
        {
            return 0;
        }

        /// <summary>A week's hours or days as the employees' day leaves them: cleared on a Monday.</summary>
        public static int ClearedWeek(int week, bool monday)
        {
            return monday ? 0 : Math.Max(0, week);
        }

        /// <summary>A mended employee's days at work this week: no more than their days with a shift from Monday to the ended day.</summary>
        public static int Days(int days, int shiftDays)
        {
            return Math.Min(Math.Max(0, days), Math.Max(0, shiftDays));
        }

        /// <summary>True if an employee's counts are looked at tonight: the clearing was missed, or more than a day counted.</summary>
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
