using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Calendar
{
    /// <summary>What one day of a month adds up to: how much comes in and goes out, and how many things of each kind there are.</summary>
    public struct DayTally
    {
        public float In, Out;        // whole money with a known amount
        public int Money, Time, City;
        public int Count => Money + Time + City;
    }

    /// <summary>
    /// The month as the Calendar app lays it out, game-free (checked by EconomyCheck): the month a day is in and its days
    /// (Core/CalendarRules), the grid of weeks it stands in from Monday to Sunday (day 1 of the game is a Monday), the
    /// days of something that comes round every so many days, the order of a day's entries and what a day adds up to.
    /// Plain arithmetic; the order is made once and nothing here allocates.
    /// </summary>
    public static class MonthViewRules
    {
        public const int Columns = 7;     // Monday to Sunday
        public const int MostWeeks = 6;   // a month of 31 days that begins on a Sunday stands in six weeks
        public const int AheadMonths = 12;  // months the calendar can be turned on from today's: what is planned that far ahead

        /// <summary>0 for Monday to 6 for Sunday, as the game's TimeHelper.GetDayOfWeek has it (day 1 is a Monday). Any day.</summary>
        public static int Weekday(int day)
        {
            int index = (day - 1) % Columns;
            return index < 0 ? index + Columns : index;
        }

        /// <summary>The empty cells before a month's first day: its weekday.</summary>
        public static int Lead(int firstDay)
        {
            return Weekday(firstDay);
        }

        /// <summary>The rows of weeks a month of this length from this first day stands in.</summary>
        public static int Weeks(int firstDay, int length)
        {
            return Math.Max(1, (Lead(firstDay) + Math.Max(0, length) + Columns - 1) / Columns);
        }

        /// <summary>The day in a cell of the grid (row and column from 0), or 0 for a cell before or after the month.</summary>
        public static int DayAt(int firstDay, int length, int row, int column)
        {
            int index = row * Columns + column - Lead(firstDay);
            return index >= 0 && index < length ? firstDay + index : 0;
        }

        /// <summary>The cell of a day of the month (row and column from 0); false for a day outside it.</summary>
        public static bool CellOf(int firstDay, int length, int day, out int row, out int column)
        {
            int index = day - firstDay + Lead(firstDay);
            row = index / Columns;
            column = index % Columns;
            return day >= firstDay && day < firstDay + length;
        }

        /// <summary>
        /// The first day, from <paramref name="from"/> on, of something that comes on <paramref name="first"/> and then every
        /// <paramref name="every"/> days; <paramref name="first"/> itself when it is not before <paramref name="from"/>.
        /// </summary>
        public static int FirstFrom(int first, int every, int from)
        {
            if (every <= 0 || first >= from)
            {
                return first;
            }
            int steps = (from - first + every - 1) / every;
            return first + steps * every;
        }

        /// <summary>
        /// The first day from <paramref name="from"/> on that is a day of the year (1 to the year's length, as
        /// CalendarRules.DayOfYear counts it): a birthday kept as a day of the year.
        /// </summary>
        public static int DayOfYearFrom(int dayOfYear, int from, int year)
        {
            year = Math.Max(1, year);
            int now = CalendarRules.DayOfYear(from, year);
            return from + (((dayOfYear - now) % year) + year) % year;
        }

        /// <summary>
        /// The player's next birthday from today: the game adds a day to their age in days each night
        /// (PlayerHelper.IncreasePlayerAge), and a whole number of years is a birthday; today when today is one.
        /// </summary>
        public static int OwnBirthdayFrom(int today, int ageInDays, int year)
        {
            year = Math.Max(1, year);
            int over = ((ageInDays % year) + year) % year;
            return today + (year - over) % year;
        }

        /// <summary>
        /// Days until a child's next birthday: a child is a year of the family's older every <paramref name="yearDays"/> days,
        /// Family/Children.Grow adding a day's share to its age each day in the same single-precision sums, and the birthday
        /// is the day its age passes the next whole number. Counted as the family counts it, a day at a time (a year of the
        /// family's is at most 84 days). One at least: a birthday kept today is behind.
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

        /// <summary>The day to show when a month is shown: today in today's month, else its first day.</summary>
        public static int ChosenDay(int today, int firstDay, int lastDay)
        {
            return today >= firstDay && today <= lastDay ? today : firstDay;
        }

        /// <summary>A month turned to by so many steps: never before the first month of the save, nor further than <see cref="AheadMonths"/> from today's.</summary>
        public static int Step(int month, int steps, int todayMonth)
        {
            int to = month + steps;
            int most = Math.Max(0, todayMonth) + AheadMonths;
            return to < 0 ? 0 : to > most ? most : to;
        }

        /// <summary>The order of the calendar (made once): by day, then what has no hour first, then by the clock, then money, time and the city, then by who it is.</summary>
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

        /// <summary>What one day of the list adds up to. The list is in the calendar's order, so the day's entries stand together.</summary>
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

        /// <summary>How many of the list's entries fall on the day.</summary>
        public static int CountOn(List<CalendarEntry> items, int day)
        {
            int count = 0;
            for (int i = 0; i < items.Count; i++)
            {
                count += items[i].Day == day ? 1 : 0;
            }
            return count;
        }

        /// <summary>Days from today to a month's last day: 0 on that day, below 0 once it has gone.</summary>
        public static int DaysLeft(int today, int lastDay)
        {
            return lastDay - today;
        }
    }
}
