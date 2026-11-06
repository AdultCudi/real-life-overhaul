using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Planner
{
    // Game-free: PlannerCheck compiles this file as it is.

    /// <summary>The filter over the Planner's tabs: four groups of the kinds of entry.</summary>
    public enum PlannerGroup
    {
        Work = 0,       // shifts, working days, interviews, classes, exams
        Money = 1,      // bills, bonds, dividends, loans, taxes
        Life = 2,       // every other kind, the player's own entries too
        Business = 3,
        Count = 4
    }

    /// <summary>How often one of the player's own entries comes back.</summary>
    public enum PlannerRepeat
    {
        Once = 0,
        Daily = 1,
        Weekly = 2      // on the weekday of its first day
    }

    /// <summary>The Planner's sums, worked out when the app is opened or clicked, never per frame; no allocation.</summary>
    public static class PlannerRules
    {
        // A route is a search spread over frames, too dear for every entry: the way is the straight line between the
        // doors, made longer by the streets, at the cleaners' rounds' paces. A game minute is a second of play.
        public const int WalkPerMinute = 350;    // hundredths of a metre a game minute on foot
        public const int DrivePerMinute = 800;   // hundredths of a metre a game minute by car or taxi, junctions included
        public const int StreetFactor = 130;     // per cent of the straight line
        public const int SetOffMinutes = 5;      // game minutes: out of the door and in at the other
        public const float HereMetres = 40f;     // metres: nearer is there already

        public const int WakeHour = 7;           // free time is counted from this hour...
        public const int SleepHour = 23;         // ...to this one
        public const int FreeLeast = 30;         // game minutes: a shorter gap is no free time
        public const int MostGaps = 3;           // stretches of free time the day's box names

        public const int TabToday = 0, TabUpcoming = 1, TabWeek = 2;

        public const int MorningHour = 5;     // the morning line comes on waking, or from 05:00 for a player who is up...
        public const int MorningLast = 12;    // ...and not from noon on: a save loaded in the afternoon has had its morning

        /// <summary>Whether the morning line is due; <paramref name="told"/> is the saved day it was last sent.</summary>
        public static bool MorningDue(int day, int hour, bool asleep, int told)
        {
            return !asleep && hour >= MorningHour && hour < MorningLast && told != day;
        }

        /// <summary>The entries of a day upcoming or under way.</summary>
        public static int MorningCount(List<PlannerEntry> items, int day)
        {
            int n = 0;
            for (int i = 0; i < items.Count; i++)
            {
                PlannerEntry e = items[i];
                n += e.Day == day && (e.State == PlannerState.Upcoming || e.State == PlannerState.Now) ? 1 : 0;
            }
            return n;
        }

        /// <summary>The morning line's key; <paramref name="leave"/>: there is a time to leave by. Null for an empty day.</summary>
        public static string MorningKey(int count, bool timed, bool leave)
        {
            if (count <= 0)
            {
                return null;
            }
            string one = count == 1 ? "_one" : string.Empty;
            return !timed ? "reallife_planner_morning_all_day" + one : "reallife_planner_morning" + one + (leave ? string.Empty : "_here");
        }

        /// <summary>Today lists the day's entries, Upcoming the days after, Week seven days from today.</summary>
        public static bool OnTab(PlannerEntry e, int tab, int today)
        {
            switch (tab)
            {
                case TabToday: return e.Day == today;
                case TabWeek: return e.Day >= today && e.Day < today + WeekGrid.Days;
                default: return e.Day > today;
            }
        }

        /// <summary>Entries a tab lists under a filter (-1 all); as in GameUi.TableModel, an empty or only group lets it go.</summary>
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
            bool kinds = (held & (held - 1)) != 0;   // two groups or more
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

        public static float Metres(float ax, float az, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Game minutes between two doors at a pace (hundredths of a metre a game minute); 0 if here or no pace.</summary>
        public static int TravelMinutes(float metres, int pace)
        {
            if (pace <= 0 || !(metres > HereMetres))
            {
                return 0;   // a NaN too
            }
            double way = (double)metres * StreetFactor;   // hundredths of a metre along the streets
            double minutes = Math.Ceiling(way / pace);
            return minutes > 100000 ? 100000 + SetOffMinutes : (int)minutes + SetOffMinutes;
        }

        /// <summary>The last moment to set off and be there when an entry begins, in game minutes.</summary>
        public static int LeaveBy(PlannerEntry entry, int travelMinutes)
        {
            return entry.Begins - Math.Max(0, travelMinutes);
        }

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

        /// <summary>The first day on or after 'fromDay' an entry falls on; -1 if never again.</summary>
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

        /// <summary>The first entry of a day with an hour, still to come or on now; null if none.</summary>
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
        /// A day's free time from 'fromMinute' to SleepHour around the entries to attend or written down, yesterday's
        /// past midnight too; the list in time order. Writes the first MostGaps stretches, returns how many.
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
                    continue;   // yesterday's that ended before midnight, or inside busy time
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

        /// <summary>The money due on the days fromDay..toDay and not yet dealt with.</summary>
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

    /// <summary>The Week tab's seven columns of entries in time order; filled when the tab is shown or changes.</summary>
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
