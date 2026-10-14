using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Planner
{
    // Free of the game and of the engine, like PlannerModel.cs: checked outside the game by PlannerCheck.

    /// <summary>The filter over the Planner's tabs: four groups of the kinds of entry ("everything" is no filter).</summary>
    public enum PlannerGroup
    {
        Work = 0,       // work and study: shifts, working days, interviews, classes, exams
        Money = 1,      // bills, bonds, dividends, loans, taxes
        Life = 2,       // family and life: family, friends, court, licences, travel, the city, health, the player's own entries
        Business = 3,   // the player's businesses
        Count = 4
    }

    /// <summary>How often one of the player's own entries comes back.</summary>
    public enum PlannerRepeat
    {
        Once = 0,
        Daily = 1,
        Weekly = 2      // on the weekday of its first day
    }

    /// <summary>
    /// The Planner's sums: which group a kind of entry is in, how long the way to a place is, when to leave, the free
    /// time in a day, when one of the player's own entries comes back, and what money falls due. No allocation: the
    /// app works these out when it is opened or clicked, never per frame.
    /// </summary>
    public static class PlannerRules
    {
        // The way about the city, by the same figures as the cleaners' rounds (Economy/Services/ServicesCatalogue). The
        // Planner has no route of its own: a route is a search spread over frames (Navigation/NavDirector), too dear for
        // every entry, so the way is the straight line between the two doors, made longer by the streets.
        public const int WalkPerMinute = 350;    // hundredths of a metre a game minute on foot: 3.5 m in a second of play (a game minute is a second at normal speed: GameManager.RunMainGameTick)
        public const int DrivePerMinute = 800;   // hundredths of a metre a game minute by car or taxi: 8 m in a second of play, what traffic makes over the streets with their junctions
        public const int StreetFactor = 130;     // per cent: the way along the streets against the straight line between two doors
        public const int SetOffMinutes = 5;      // game minutes to get out of the door and in at the other: the car parked, the taxi waved down
        public const float HereMetres = 40f;     // nearer than this the player is there already: no way to go

        // The day at a glance.
        public const int WakeHour = 7;           // free time is counted from this hour of the day...
        public const int SleepHour = 23;         // ...to this one
        public const int FreeLeast = 30;         // game minutes: a shorter gap between two entries is no free time
        public const int MostGaps = 3;           // the day's box names this many stretches of free time

        public const int TabToday = 0, TabUpcoming = 1, TabWeek = 2;   // the app's tabs, in their order

        /// <summary>Whether an entry is listed on a tab: Today the day's, Upcoming the days after, Week seven days from today.</summary>
        public static bool OnTab(PlannerEntry e, int tab, int today)
        {
            switch (tab)
            {
                case TabToday: return e.Day == today;
                case TabWeek: return e.Day >= today && e.Day < today + WeekGrid.Days;
                default: return e.Day > today;
            }
        }

        /// <summary>
        /// How many entries a tab lists under its filter (<paramref name="group"/>, a PlannerGroup, -1 for all), as the
        /// table shows them: a group with nothing on the tab, or a tab with entries of one group only, lists everything
        /// (GameUi.TableModel lets the filter go then). One pass over the looked-at entries, when the tables are filled.
        /// </summary>
        public static int TabCount(List<PlannerEntry> items, int tab, int today, int group)
        {
            int all = 0, ofGroup = 0, held = 0;
            for (int i = 0; i < items.Count; i++)
            {
                PlannerEntry e = items[i];
                if (!OnTab(e, tab, today))
                {
                    continue;
                }
                int g = (int)GroupOf(e.Category);
                all++;
                ofGroup += g == group ? 1 : 0;
                held |= 1 << g;
            }
            bool kinds = (held & (held - 1)) != 0;   // (two groups or more)
            return group >= 0 && ofGroup > 0 && kinds ? ofGroup : all;
        }

        public static PlannerGroup GroupOf(PlannerCategory category)
        {
            switch (category)
            {
                case PlannerCategory.Work: return PlannerGroup.Work;
                case PlannerCategory.Money: return PlannerGroup.Money;
                case PlannerCategory.Business: return PlannerGroup.Business;
                default: return PlannerGroup.Life;
            }
        }

        // ---------------------------------------------------------------- the way

        /// <summary>The straight line between two points of the city's ground, in metres.</summary>
        public static float Metres(float ax, float az, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Game minutes from one door to another this far apart at a pace (hundredths of a metre a game minute):
        /// along the streets, and the minutes to set off. 0 if the two are the same place or there is no pace.
        /// </summary>
        public static int TravelMinutes(float metres, int pace)
        {
            if (pace <= 0 || !(metres > HereMetres))
            {
                return 0;   // (a NaN is no distance either)
            }
            double way = (double)metres * StreetFactor;   // hundredths of a metre along the streets (metres, times the per cent)
            double minutes = Math.Ceiling(way / pace);
            return minutes > 100000 ? 100000 + SetOffMinutes : (int)minutes + SetOffMinutes;
        }

        /// <summary>The last moment to set off and be there when an entry begins (or by its deadline), in game minutes.</summary>
        public static int LeaveBy(PlannerEntry entry, int travelMinutes)
        {
            return entry.Begins - Math.Max(0, travelMinutes);
        }

        // ---------------------------------------------------------------- the player's own entries

        /// <summary>Whether an entry first on one day, repeated so, falls on another day.</summary>
        public static bool Occurs(PlannerRepeat repeat, int firstDay, int day)
        {
            if (day < firstDay)
            {
                return false;
            }
            switch (repeat)
            {
                case PlannerRepeat.Daily: return true;
                case PlannerRepeat.Weekly: return (day - firstDay) % 7 == 0;
                default: return day == firstDay;
            }
        }

        /// <summary>The first day on or after 'fromDay' an entry falls on; -1 if it never does again (one that was once).</summary>
        public static int NextOccurrence(PlannerRepeat repeat, int firstDay, int fromDay)
        {
            if (fromDay <= firstDay)
            {
                return firstDay;
            }
            switch (repeat)
            {
                case PlannerRepeat.Daily: return fromDay;
                case PlannerRepeat.Weekly: return fromDay + (7 - (fromDay - firstDay) % 7) % 7;
                default: return -1;
            }
        }

        // ---------------------------------------------------------------- the day at a glance

        /// <summary>The first entry of a day that is still to come or on now and has an hour: what the day's box leads with. Null if none.</summary>
        public static PlannerEntry FirstOf(List<PlannerEntry> items, int day)
        {
            for (int i = 0; i < items.Count; i++)
            {
                PlannerEntry e = items[i];
                if (e.Day == day && !e.AllDay && (e.State == PlannerState.Upcoming || e.State == PlannerState.Now))
                {
                    return e;   // the list is in time order
                }
            }
            return null;
        }

        /// <summary>
        /// The free time of a day between its entries, from a minute of the day ('fromMinute', at least WakeHour) to
        /// SleepHour: busy is every entry with an hour the player has to attend or wrote down, still to do, and one of the
        /// day before that runs past midnight. The first MostGaps stretches of at least FreeLeast minutes go into
        /// starts and ends (minutes of the day); 'total' is all of it. Returns how many stretches were written. The
        /// list must be in time order (PlannerLogic.Arrange).
        /// </summary>
        public static int FreeTime(List<PlannerEntry> items, int day, int fromMinute, int[] starts, int[] ends, out int total)
        {
            total = 0;
            int written = 0;
            int dayStart = day * PlannerTime.MinutesPerDay;
            int cursor = Math.Max(fromMinute, WakeHour * 60), stop = SleepHour * 60;
            for (int i = 0; i < items.Count && cursor < stop; i++)
            {
                PlannerEntry e = items[i];
                if ((e.Day != day && e.Day != day - 1) || e.Start < 0 || !(e.Attend || e.Own) || e.Done || e.State == PlannerState.Missed || e.State == PlannerState.Over)
                {
                    continue;
                }
                int begin = e.Begins - dayStart, end = e.Ends - dayStart;
                if (end <= cursor)
                {
                    continue;   // (yesterday's that ended before midnight, or one inside time already busy)
                }
                if (begin - cursor >= FreeLeast)
                {
                    Gap(cursor, Math.Min(begin, stop), starts, ends, ref written, ref total);
                }
                cursor = Math.Max(cursor, end);
            }
            if (stop - cursor >= FreeLeast)
            {
                Gap(cursor, stop, starts, ends, ref written, ref total);
            }
            return written;
        }

        private static void Gap(int from, int to, int[] starts, int[] ends, ref int written, ref int total)
        {
            if (to - from < FreeLeast)
            {
                return;
            }
            total += to - from;
            if (written < MostGaps && written < starts.Length && written < ends.Length)
            {
                starts[written] = from;
                ends[written] = to;
                written++;
            }
        }

        /// <summary>The money that falls due on the days fromDay..toDay and is not yet dealt with, as the sources give it (PlannerEntry.Owes).</summary>
        public static float OwedOn(List<PlannerEntry> items, int fromDay, int toDay)
        {
            float owed = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                PlannerEntry e = items[i];
                if (e.Day >= fromDay && e.Day <= toDay && e.Owes > 0f && !e.Done && e.State != PlannerState.Missed && e.State != PlannerState.Over)
                {
                    owed += e.Owes;
                }
            }
            return owed;
        }
    }

    /// <summary>
    /// The Week tab's seven columns, one for each day from the first: each day's entries in time order. Its lists are
    /// made once and used again; it is filled when the tab is shown or its entries change.
    /// </summary>
    public sealed class WeekGrid
    {
        public const int Days = 7;

        public readonly List<PlannerEntry>[] Columns = new List<PlannerEntry>[Days];
        public int FirstDay { get; private set; }

        public WeekGrid()
        {
            for (int d = 0; d < Days; d++)
            {
                Columns[d] = new List<PlannerEntry>(16);
            }
        }

        public void Begin(int firstDay)
        {
            FirstDay = firstDay;
            for (int d = 0; d < Days; d++)
            {
                Columns[d].Clear();
            }
        }

        /// <summary>Puts an entry in its day's column; false for one outside the week.</summary>
        public bool Add(PlannerEntry entry)
        {
            int column = entry != null ? entry.Day - FirstDay : -1;
            if (column < 0 || column >= Days)
            {
                return false;
            }
            Columns[column].Add(entry);
            return true;
        }

        /// <summary>Each column in time order, whatever order the entries came in.</summary>
        public void Finish()
        {
            for (int d = 0; d < Days; d++)
            {
                if (Columns[d].Count > 1)
                {
                    Columns[d].Sort(PlannerLogic.ByTime);
                }
            }
        }

        public int Count
        {
            get
            {
                int n = 0;
                for (int d = 0; d < Days; d++)
                {
                    n += Columns[d].Count;
                }
                return n;
            }
        }
    }
}
