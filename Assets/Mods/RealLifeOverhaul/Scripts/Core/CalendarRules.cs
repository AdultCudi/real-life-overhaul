using System;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The calendar's rules, game-free (checked by EconomyCheck). The game has days and a year of 60 to 365 of them, but
    /// no months or seasons. The mod's months are twelfths of the year, as the stock market's are; its seasons are the
    /// weather's (the year begins in midwinter). Monthly costs are priced by the day and paid <see cref="MonthDays"/> at
    /// a time, so a longer year makes nothing dearer by the day.
    /// </summary>
    public static class CalendarRules
    {
        public const int YearDefault = 60;   // days: the game's year in a save that says nothing
        public const int YearLeast = 8;      // days

        public const int Winter = 0, Spring = 1, Summer = 2, Autumn = 3;

        /// <summary>The year in days from what a save says, else the game's default.</summary>
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

        public const int MonthsPerYear = 12;

        /// <summary>The months of a year of so many days: twelve, or one a day in a year shorter than twelve days.</summary>
        public static int MonthsIn(int year)
        {
            return Math.Max(1, Math.Min(MonthsPerYear, year));
        }

        /// <summary>The month a day (from 1) is in, counted from 0 over the years, as MarketCatalogue.MonthOf counts.</summary>
        public static int MonthOf(int day, int year)
        {
            year = Math.Max(1, year);
            int months = MonthsIn(year);
            int y = FloorDiv(day - 1, year);
            return y * months + (day - 1 - y * year) * months / year;
        }

        /// <summary>The first day of a month as <see cref="MonthOf"/> counts them.</summary>
        public static int MonthStart(int month, int year)
        {
            year = Math.Max(1, year);
            int months = MonthsIn(year);
            int y = FloorDiv(month, months);
            int inYear = month - y * months;
            return y * year + (inYear * year + months - 1) / months + 1;
        }

        public static int MonthEnd(int month, int year)
        {
            return MonthStart(month + 1, year) - 1;
        }

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
        /// The first day of the year after the one a day is in. The game's TaxHelper closes the year on a multiple of its
        /// length, so day 60 of a 60-day year is still the first year.
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
