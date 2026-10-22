using BigAmbitions.DayNightCycle;
using UnityEngine;

namespace RealLifeOverhaul.Events
{
    /// <summary>The kinds of street event; None on a day with nothing on.</summary>
    public enum EventKind { None, Market, FarmersMarket, Parade, Protest, FunRun, FilmShoot, Fireworks }

    /// <summary>One street event on the calendar: what, on which day, between which hours.</summary>
    public struct PlannedEvent
    {
        public EventKind Kind;
        public int Day;
        public int From;   // the hour it opens
        public int To;     // the hour it packs up (after From, on the same day)
    }

    /// <summary>
    /// The city's calendar. The game has a year of so many days (60 unless the save says otherwise) but no dates, so fixed
    /// days fall by how far through the year they are: New Year's Eve is the last day, the Fourth of July 50.7% of the way
    /// through. Saturdays have a farmers' market; other days roll on the day and a number kept in the save's modData, so
    /// the same save always has the same calendar and tomorrow can be announced today.
    /// </summary>
    public static class EventCalendar
    {
        /// <summary>This save's own number, from modData; 0 until it has been read.</summary>
        internal static int Salt;

        /// <summary>The year in days, from <see cref="GameCalendar"/>.</summary>
        public static int DaysPerYear => GameCalendar.DaysPerYear;

        /// <summary>The day of the year, 1 to DaysPerYear.</summary>
        public static int DayOfYear(int day) => GameCalendar.DayOfYear(day);

        /// <summary>How far through the year a day is, 0 to just under 1.</summary>
        public static float YearPart(int day) => GameCalendar.YearPart(day);

        public static bool IsNewYearsEve(int day)
        {
            return DayOfYear(day) == DaysPerYear;
        }

        public static bool IsIndependenceDay(int day)
        {
            return DayOfYear(day) == Mathf.Clamp(Mathf.RoundToInt(DaysPerYear * 0.507f) + 1, 1, DaysPerYear - 1);
        }

        /// <summary>A number from 0 to 1 that is always the same for a day and a purpose in this save.</summary>
        public static float Roll(int day, int purpose)
        {
            unchecked
            {
                uint hash = (uint)(day * 73856093) ^ (uint)(purpose * 19349663) ^ (uint)(Salt * 83492791);
                hash ^= hash >> 13;
                hash *= 0x5bd1e995u;
                hash ^= hash >> 15;
                return (hash & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>What is on that day, if anything.</summary>
        public static PlannedEvent For(int day)
        {
            var plan = new PlannedEvent { Kind = EventKind.None, Day = day };
            if (!Settings.StreetEvents || day < 1)
            {
                return plan;
            }
            if (IsNewYearsEve(day) || IsIndependenceDay(day))
            {
                plan.Kind = EventKind.Fireworks;
                plan.From = 21;
                plan.To = 24;
                return plan;
            }
            if (TimeHelper.GetDayOfWeek(day) == DayOfWeekOrdered.Saturday)
            {
                plan.Kind = EventKind.FarmersMarket;
                plan.From = 8;
                plan.To = 13;
                return plan;
            }
            if (Roll(day, 3) >= Settings.StreetEventFrequency * 0.08f * CivicPolicy.EventScale)   // 1 unless a council changed the events policy
            {
                return plan;
            }
            float which = Roll(day, 5);
            if (which < 0.26f)
            {
                plan.Kind = EventKind.Market; plan.From = 10; plan.To = 16;
            }
            else if (which < 0.46f)
            {
                plan.Kind = EventKind.Parade; plan.From = 12; plan.To = 15;
            }
            else if (which < 0.64f)
            {
                plan.Kind = EventKind.Protest; plan.From = 11; plan.To = 15;
            }
            else if (which < 0.82f)
            {
                plan.Kind = EventKind.FunRun; plan.From = 8; plan.To = 11;
            }
            else
            {
                plan.Kind = EventKind.FilmShoot; plan.From = 9; plan.To = 17;
            }
            return plan;
        }

        public static string Name(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.Market: return "reallife_event_name_market";
                case EventKind.FarmersMarket: return "reallife_event_name_farmers";
                case EventKind.Parade: return "reallife_event_name_parade";
                case EventKind.Protest: return "reallife_event_name_protest";
                case EventKind.FunRun: return "reallife_event_name_run";
                case EventKind.FilmShoot: return "reallife_event_name_film";
                default: return "reallife_event_name_fireworks";
            }
        }

        /// <summary>The line that announces it the day before.</summary>
        public static string Tomorrow(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.Market: return "reallife_event_tomorrow_market";
                case EventKind.FarmersMarket: return "reallife_event_tomorrow_farmers";
                case EventKind.Parade: return "reallife_event_tomorrow_parade";
                case EventKind.Protest: return "reallife_event_tomorrow_protest";
                case EventKind.FunRun: return "reallife_event_tomorrow_run";
                case EventKind.FilmShoot: return "reallife_event_tomorrow_film";
                default: return "reallife_event_tomorrow_fireworks";
            }
        }

        /// <summary>The "what's on" line for the day itself.</summary>
        public static string Today(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.Market: return "reallife_event_today_market";
                case EventKind.FarmersMarket: return "reallife_event_today_farmers";
                case EventKind.Parade: return "reallife_event_today_parade";
                case EventKind.Protest: return "reallife_event_today_protest";
                case EventKind.FunRun: return "reallife_event_today_run";
                case EventKind.FilmShoot: return "reallife_event_today_film";
                default: return "reallife_event_today_fireworks";
            }
        }

        /// <summary>
        /// Whether a kind of event goes without its notices (what is on, its start and its end): protests while their switch is
        /// off. The event itself, its closed street and its City Bulletin line are the same either way.
        /// </summary>
        public static bool Quiet(EventKind kind) => kind == EventKind.Protest && !Settings.StreetProtestNotes;
    }
}
