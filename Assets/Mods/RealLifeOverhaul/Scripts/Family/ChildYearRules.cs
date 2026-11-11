using System;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// A year of a child's age, the options page's weeks per year of a child's age: how long it can be, the days it holds, how a
    /// child grows older by the day, the day of the next birthday, how long a pregnancy and an adoption take and how far apart the
    /// texts that fill them fall. One table for ageing (Children.Grow), the Family page, the Calendar and the Guide, so they agree
    /// at every length of year. A child's age is kept in years but counted in whole days of this year, so a year of N weeks is
    /// exactly 7 x N days at every N (sums of a day's share in single precision came a day late at most lengths). Game-free
    /// (EconomyCheck, ChildYearChecks.cs). COST: a little arithmetic, on the day's round or when a page is built.
    /// </summary>
    public static class ChildYearRules
    {
        public const int WeekDays = 7;             // days in a week
        public const int GameYearMost = 365;       // days: the longest year the game's own setting allows
        public const int WeeksLeast = 1;           // weeks: the option's lowest
        public const int WeeksMost = 52;           // weeks: the option's highest, a year of 364 days, the most whole weeks inside GameYearMost
        public const int PacedWeeks = 4;           // weeks: the year the texts of a pregnancy and an adoption are paced for, the option's shipped value
        public const int SpanLeast = 6;            // days: a pregnancy or an adoption takes this long at the least
        public const int PregnancyTextDays = 4;    // days between the texts of a pregnancy, at the paced year
        public const int AdoptionTextDays = 5;     // days between the texts of an adoption's wait, at the paced year

        /// <summary>The option's weeks kept inside its ends: a value from outside the page (an old or edited setting) reads as the nearest end.</summary>
        public static int Weeks(int setting)
        {
            return Math.Max(WeeksLeast, Math.Min(WeeksMost, setting));
        }

        /// <summary>The days in a year of the option's weeks.</summary>
        public static int Days(int setting)
        {
            return Weeks(setting) * WeekDays;
        }

        /// <summary>The day of their life an age comes to, counted in days of the year: the nearest whole day.</summary>
        public static long DayOfLife(float age, int yearDays)
        {
            return (long)Math.Round(Math.Max(0.0, age) * Math.Max(1, yearDays));
        }

        /// <summary>
        /// An age after <paramref name="years"/> more, counted in whole days of the year and divided once in double precision, so
        /// after N more days a child is exactly N / <paramref name="yearDays"/> years older whatever the size of the steps between,
        /// and a birthday falls on the day a whole number of years of days has passed. An age between two days (an adopted child's,
        /// or one kept from before the year was changed) is taken to the nearest day.
        /// </summary>
        public static float Aged(float age, float years, int yearDays)
        {
            yearDays = Math.Max(1, yearDays);
            return (float)((DayOfLife(age, yearDays) + Math.Round(years * (double)yearDays)) / yearDays);
        }

        /// <summary>Days until a child's next birthday, from 1 to a year: the day their age next comes to a whole number of years, as <see cref="Aged"/> counts it.</summary>
        public static int BirthdayIn(float age, int yearDays)
        {
            yearDays = Math.Max(1, yearDays);
            long day = DayOfLife(age, yearDays);
            return (int)((day / yearDays + 1) * yearDays - day);
        }

        /// <summary>The age in whole years that a child's next birthday makes them.</summary>
        public static int Turning(float age, int yearDays)
        {
            yearDays = Math.Max(1, yearDays);
            return (int)(DayOfLife(age, yearDays) / yearDays) + 1;
        }

        /// <summary>Days a pregnancy or an adoption takes: a share of the year, at least <see cref="SpanLeast"/>.</summary>
        public static int Span(float years, int yearDays)
        {
            return Math.Max(SpanLeast, (int)Math.Round(years * Math.Max(1, yearDays)));
        }

        /// <summary>
        /// Days between the texts that fill a pregnancy or an adoption's wait: <paramref name="pacedDays"/> at the paced year, in
        /// proportion longer for a longer year (so about as many fall in the whole of one), never shorter than at the paced year.
        /// </summary>
        public static int TextGap(int pacedDays, int yearDays)
        {
            int paced = PacedWeeks * WeekDays;
            return Math.Max(pacedDays, (int)(((long)pacedDays * Math.Max(1, yearDays) + paced / 2) / paced));
        }
    }
}
