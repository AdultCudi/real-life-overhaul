using System;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The calendar's rules, game-free (checked by EconomyCheck). The game counts days and has a year of so many of
    /// them, chosen when a game is started (60 to 365; 60 in a save that says nothing) but no months, no seasons and
    /// no dates. The mod's month is a twelfth of that year, three days at least (5 days in a 60-day year, 30 in a
    /// 365-day one: MacroCatalogue.MonthDays), and its seasons are the weather's: the year begins in midwinter, winter
    /// is its last twelfth and first sixth, spring runs to five twelfths, summer to two thirds, autumn to eleven
    /// twelfths. Something paid by the month is priced by the day and paid a month's worth at a time, so that a longer
    /// year makes nothing dearer or cheaper by the day, as the game's own daily rent and wages are not.
    /// </summary>
    public static class CalendarRules
    {
        public const int YearDefault = 60;
        public const int YearLeast = 8;

        public const int Winter = 0, Spring = 1, Summer = 2, Autumn = 3;

        /// <summary>The year in days from what a save says: the save's own when it is a year at all, else the game's default.</summary>
        public static int Year(int saved)
        {
            return saved >= YearLeast ? saved : YearDefault;
        }

        /// <summary>A month in days: a twelfth of the year, three at least.</summary>
        public static int MonthDays(int year)
        {
            return MacroCatalogue.MonthDays(year);
        }

        /// <summary>The day of the year, 1 to the year's length.</summary>
        public static int DayOfYear(int day, int year)
        {
            year = Math.Max(1, year);
            return (Math.Max(1, day) - 1) % year + 1;
        }

        /// <summary>How far through the year a day is, 0 to just under 1.</summary>
        public static float YearPart(int day, int year)
        {
            return (DayOfYear(day, year) - 1) / (float)Math.Max(1, year);
        }

        /// <summary>The season of a day: Winter, Spring, Summer or Autumn (0 to 3).</summary>
        public static int Season(int day, int year)
        {
            float part = YearPart(day, year);
            return part < 0.17f || part >= 0.92f ? Winter : part < 0.42f ? Spring : part < 0.67f ? Summer : Autumn;
        }

        /// <summary>The same season counted from spring (0 spring, 1 summer, 2 autumn, 3 winter), as travel and property count theirs.</summary>
        public static int SeasonFromSpring(int day, int year)
        {
            return (Season(day, year) + 3) % 4;
        }

        /// <summary>A month's worth of something priced by the day.</summary>
        public static int Monthly(int perDay, int year)
        {
            return perDay * MonthDays(year);
        }
    }
}
