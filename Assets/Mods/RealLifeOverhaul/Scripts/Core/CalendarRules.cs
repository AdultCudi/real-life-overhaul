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
    /// year makes nothing dearer or cheaper by the day, as the game's own daily rent and wages are not. The calendar's
    /// months (MonthOf, MonthStart, the Calendar app's) are twelfths of the year by the day of the year, as the stock
    /// market's are; what is paid by the month counts its month of MonthDays from its own first day.
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

        // ---------------------------------------------------------------- the months of the calendar

        public const int MonthsPerYear = 12;

        /// <summary>The months of a year of so many days: twelve, or one a day in a year shorter than twelve days.</summary>
        public static int MonthsIn(int year)
        {
            return Math.Max(1, Math.Min(MonthsPerYear, year));
        }

        /// <summary>
        /// The month a day is in, counted from 0 over the years (twelve a year): the day of the year in twelfths of the
        /// year, as the stock market counts its months (MarketCatalogue.MonthOf). A 60-day year has twelve months of 5
        /// days, a 365-day one months of 30 and 31. The days are the game's, from 1.
        /// </summary>
        public static int MonthOf(int day, int year)
        {
            year = Math.Max(1, year);
            int months = MonthsIn(year);
            int y = FloorDiv(day - 1, year);
            return y * months + (day - 1 - y * year) * months / year;
        }

        /// <summary>The first day of a month as <see cref="MonthOf"/> counts them: the first whose twelfth of the year is the month's.</summary>
        public static int MonthStart(int month, int year)
        {
            year = Math.Max(1, year);
            int months = MonthsIn(year);
            int y = FloorDiv(month, months);
            int inYear = month - y * months;
            return y * year + (inYear * year + months - 1) / months + 1;
        }

        /// <summary>The last day of a month: the day before the next one's first.</summary>
        public static int MonthEnd(int month, int year)
        {
            return MonthStart(month + 1, year) - 1;
        }

        /// <summary>How many days a month has.</summary>
        public static int MonthLength(int month, int year)
        {
            return MonthEnd(month, year) - MonthStart(month, year) + 1;
        }

        /// <summary>A month's place in its year, 0 (the first) to the year's months less one.</summary>
        public static int MonthInYear(int month, int year)
        {
            int months = MonthsIn(year);
            return month - FloorDiv(month, months) * months;
        }

        /// <summary>The year a month is in, from 1.</summary>
        public static int YearOfMonth(int month, int year)
        {
            return FloorDiv(month, MonthsIn(year)) + 1;
        }

        /// <summary>The year a day is in, from 1: days 1 to the year's length are the first year.</summary>
        public static int YearOf(int day, int year)
        {
            return FloorDiv(day - 1, Math.Max(1, year)) + 1;
        }

        /// <summary>
        /// The first day of the year after the one a day is in: the day after the year's last. The game closes its tax
        /// year on that last day (TaxHelper: the day a multiple of the year's length, the period from the day after the
        /// last one), so day 60 of a 60-day year is still the first year and day 61 begins the second, as the calendar,
        /// the weather and the stock market count it. The Planner's day box reads it from here.
        /// </summary>
        public static int NextYearStart(int day, int year)
        {
            return YearOf(day, year) * Math.Max(1, year) + 1;
        }

        private static int FloorDiv(int a, int b)
        {
            int q = a / b;
            return (a % b != 0 && (a < 0) != (b < 0)) ? q - 1 : q;
        }
    }
}
