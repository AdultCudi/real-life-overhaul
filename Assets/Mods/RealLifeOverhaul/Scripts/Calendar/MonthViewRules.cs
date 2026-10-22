using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Calendar
{
    /// <summary>What one day adds up to: the money in and out, and how many things of each kind.</summary>
    public struct DayTally
    {
        public float In, Out;        // whole money with a known amount
        public int Money, Time, City;
        public int Count => Money + Time + City;
    }

    /// <summary>The Calendar's month, game-free for EconomyCheck: the week grid (day 1 is a Monday), repeats, order and tallies.</summary>
    public static class MonthViewRules
    {
        public const int Columns = 7;     // Monday to Sunday
        public const int MostWeeks = 6;   // a month of 31 days that begins on a Sunday stands in six weeks
        public const int AheadMonths = 12;  // months the calendar can be turned on past today's

        /// <summary>0 for Monday to 6 for Sunday, as the game's TimeHelper.GetDayOfWeek.</summary>
        public static int Weekday(int day)
        {
            int index = (day - 1) % Columns;
            return index < 0 ? index + Columns : index;
        }

        /// <summary>The empty cells before a month's first day.</summary>
        public static int Lead(int firstDay)
        {
            return Weekday(firstDay);
        }

        public static int Weeks(int firstDay, int length)
        {
            return Math.Max(1, (Lead(firstDay) + Math.Max(0, length) + Columns - 1) / Columns);
        }

        /// <summary>The day in a cell (row and column from 0), or 0 outside the month.</summary>
        public static int DayAt(int firstDay, int length, int row, int column)
        {
            int index = row * Columns + column - Lead(firstDay);
            return index >= 0 && index < length ? firstDay + index : 0;
        }

        /// <summary>The cell of a day (row and column from 0); false for a day outside the month.</summary>
        public static bool CellOf(int firstDay, int length, int day, out int row, out int column)
        {
            int index = day - firstDay + Lead(firstDay);
            row = index / Columns;
            column = index % Columns;
            return day >= firstDay && day < firstDay + length;
        }

        /// <summary>The first day from <paramref name="from"/> on of something due on <paramref name="first"/> and every <paramref name="every"/> days after.</summary>
        public static int FirstFrom(int first, int every, int from)
        {
            if (every <= 0 || first >= from)
            {
                return first;
            }
            int steps = (from - first + every - 1) / every;
            return first + steps * every;
        }

        /// <summary>The first day from <paramref name="from"/> on that falls on a day of the year (CalendarRules.DayOfYear): a birthday.</summary>
        public static int DayOfYearFrom(int dayOfYear, int from, int year)
        {
            year = Math.Max(1, year);
            int now = CalendarRules.DayOfYear(from, year);
            return from + (((dayOfYear - now) % year) + year) % year;
        }

        /// <summary>The player's next birthday, today included: a whole number of years of age in days (PlayerHelper.IncreasePlayerAge).</summary>
        public static int OwnBirthdayFrom(int today, int ageInDays, int year)
        {
            year = Math.Max(1, year);
            int over = ((ageInDays % year) + year) % year;
            return today + (year - over) % year;
        }

        /// <summary>
        /// Days until a child's next birthday, at least one. Counted a day at a time in the same single-precision sums as
        /// Family/Children.Grow, which adds 1 / <paramref name="yearDays"/> a day, so the day matches its own.
        /// </summary>
        public static int ChildBirthdayIn(float age, int yearDays)
        {
            yearDays = Math.Max(1, yearDays);
            float step = 1 / (float)yearDays;
            int now = (int)Math.Floor(age);
            for (int days = 1; days <= yearDays * 2 + 2; days++)
            {
                age += step;
                if ((int)Math.Floor(age) > now)
                {
                    return days;
                }
            }
            return yearDays;
        }

        /// <summary>Today in today's month, else the month's first day.</summary>
        public static int ChosenDay(int today, int firstDay, int lastDay)
        {
            return today >= firstDay && today <= lastDay ? today : firstDay;
        }

        /// <summary>A month turned by so many steps, kept between the save's first and <see cref="AheadMonths"/> past today's.</summary>
        public static int Step(int month, int steps, int todayMonth)
        {
            int to = month + steps;
            int most = Math.Max(0, todayMonth) + AheadMonths;
            return to < 0 ? 0 : to > most ? most : to;
        }

        /// <summary>By day, then no hour first, then the clock, then money, time and the city, then by source and who.</summary>
        public static readonly Comparison<CalendarEntry> ByTime = Compare;

        private static int Compare(CalendarEntry a, CalendarEntry b)
        {
            if (a.Day != b.Day)
            {
                return a.Day < b.Day ? -1 : 1;
            }
            if (a.Start != b.Start)
            {
                return a.Start < b.Start ? -1 : 1;
            }
            if (a.Group != b.Group)
            {
                return a.Group < b.Group ? -1 : 1;
            }
            int source = string.CompareOrdinal(a.Source, b.Source);
            if (source != 0)
            {
                return source;
            }
            return a.A != b.A ? (a.A < b.A ? -1 : 1) : a.B.CompareTo(b.B);
        }

        public static DayTally Tally(List<CalendarEntry> items, int day)
        {
            var tally = new DayTally();
            for (int i = 0; i < items.Count; i++)
            {
                CalendarEntry e = items[i];
                if (e.Day != day)
                {
                    continue;
                }
                switch (e.Group)
                {
                    case CalendarGroup.Money:
                        tally.Money++;
                        if (e.Sign > 0)
                        {
                            tally.In += e.Amount;
                        }
                        else if (e.Sign < 0)
                        {
                            tally.Out += e.Amount;
                        }
                        break;
                    case CalendarGroup.City:
                        tally.City++;
                        break;
                    default:
                        tally.Time++;
                        break;
                }
            }
            return tally;
        }

        public static int CountOn(List<CalendarEntry> items, int day)
        {
            int count = 0;
            for (int i = 0; i < items.Count; i++)
            {
                count += items[i].Day == day ? 1 : 0;
            }
            return count;
        }

        /// <summary>Days from today to a month's last day: 0 on it, below 0 after.</summary>
        public static int DaysLeft(int today, int lastDay)
        {
            return lastDay - today;
        }
    }
}
