using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.Calendar;
using RealLifeOverhaul.Planner;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What comes next, for the kit phone's "Up next": the Planner's entries for today and the coming week (the next
    /// shift and class among them) and the Calendar's dated days the Planner does not hold (pay days, bills, birthdays),
    /// each a when and what it is, soonest first. Read through the two directors, never from their apps' lists the player
    /// is looking at: the Planner fills a list of its own here, and the Calendar is asked for this month only while its
    /// app is closed (the phone shows only then). Read when the phone shows, on a new day and after the full menu closes;
    /// never a frame. Cost: one look of each director's sources a read.
    /// </summary>
    internal static class PhoneNextGame
    {
        private const int Days = 7;           // today and the six after
        private const int Most = 8;           // more than the tallest phone shows

        private struct Coming
        {
            public int At;                    // minutes since day 0, for the order
            public int Day;
            public int Start;                 // minutes since midnight, -1 for the whole day
            public string Title;
        }

        private static readonly PlannerList Planned = new PlannerList();
        private static readonly List<Coming> Found = new List<Coming>(32);
        private static readonly Comparison<Coming> Soonest = (a, b) => a.At != b.At ? a.At.CompareTo(b.At) : string.CompareOrdinal(a.Title, b.Title);

        /// <summary>Fills rows with what comes next; returns how many. A director that is off or not up gives nothing.</summary>
        internal static int Read(UiInfoRow[] rows)
        {
            Found.Clear();
            int now = PlannerDirector.Now();
            int today = PlannerTime.DayOf(now);
            FromPlanner(now, today);
            FromCalendar(now, today);
            Found.Sort(Soonest);
            int n = Math.Min(Math.Min(Found.Count, Most), rows.Length);
            for (int i = 0; i < n; i++)
            {
                Coming c = Found[i];
                rows[i].Label = When(c.Day, c.Start, today);
                rows[i].Value = c.Title;
                rows[i].Link = false;
                rows[i].Ink = UiRole.None;
            }
            return n;
        }

        private static void FromPlanner(int now, int today)
        {
            PlannerDirector planner = PlannerDirector.Instance;
            if (planner == null || !Settings.Planner)
            {
                return;
            }
            planner.LookFor(Planned, today, today + Days - 1, null);
            for (int i = 0; i < Planned.Items.Count; i++)
            {
                PlannerEntry e = Planned.Items[i];
                if (e.Done || string.IsNullOrEmpty(e.Title) || e.Ends < now)
                {
                    continue;
                }
                Found.Add(new Coming { At = e.Begins, Day = e.Day, Start = e.Start >= 0 ? e.Start : e.Deadline ? e.End : -1, Title = e.Title });
            }
        }

        // The Calendar's own sources for this month, less the Planner's (already read) and anything past.
        private static void FromCalendar(int now, int today)
        {
            CalendarDirector calendar = CalendarDirector.Instance;
            if (calendar == null)
            {
                return;
            }
            int year = GameCalendar.DaysPerYear;
            calendar.Look(CalendarRules.MonthOf(today, year));
            List<CalendarEntry> items = calendar.List.Items;
            for (int i = 0; i < items.Count; i++)
            {
                CalendarEntry e = items[i];
                if (e.Source == "planner" || string.IsNullOrEmpty(e.Title) || e.Day < today || e.Day >= today + Days)
                {
                    continue;
                }
                int at = PlannerTime.At(e.Day, 0) + (e.Start >= 0 ? e.Start : 0);
                if (e.Day == today && e.Start >= 0 && at < now)
                {
                    continue;
                }
                Found.Add(new Coming { At = at, Day = e.Day, Start = e.Start, Title = e.Title });
            }
        }

        // "16:00" today, "Today" for the whole day, "Tomorrow", then the weekday, as the game writes them.
        private static string When(int day, int start, int today)
        {
            if (day == today)
            {
                return start >= 0 ? UiClock.AtMinute(start) : "common_today".GetLocalization();
            }
            return day == today + 1 ? "common_tomorrow".GetLocalization() : PlannerText.Weekday(day);
        }
    }
}
