using System.Collections.Generic;

namespace RealLifeOverhaul.Calendar
{
    // Game-free: EconomyCheck compiles this file as it is.

    /// <summary>What a calendar entry is about: money that moves, the player's own time, or something on in the city.</summary>
    public enum CalendarGroup
    {
        Money = 0,
        Time = 1,
        City = 2
    }

    /// <summary>One thing on one day, from the list's pool (CalendarList.Add), filled with its words by its source.</summary>
    public sealed class CalendarEntry
    {
        public string Source;
        public int A, B;             // who it is within its source
        public CalendarGroup Group;
        public int Day;
        public int Start = -1;       // minutes since midnight; -1 for no hour
        public int Sign;             // +1 money in, -1 money out, 0 neither or not known
        public float Amount;         // whole money, never below 0; 0 when the source does not know it
        public string Title;
        public string Value;         // the words at its right: an hour, an amount, "By 17:00"; null for none
        public string App;           // the id of the mod's app that deals with it; null for none

        internal void Reset()
        {
            Source = Title = Value = App = null;
            A = B = Day = Sign = 0;
            Start = -1;
            Amount = 0f;
            Group = CalendarGroup.Time;
        }
    }

    /// <summary>Money that moves every night (rent, wages, loans): said once for the month, not on each day.</summary>
    public sealed class NightLine
    {
        public string Source;
        public string Title;
        public int Sign;             // +1 in, -1 out
        public float Amount;         // whole money a night
    }

    /// <summary>The entries of one look at the sources for FromDay to ToDay, and the nightly lines; both pooled between looks.</summary>
    public sealed class CalendarList
    {
        public const int MostEntries = 600;   // so a source gone wrong cannot flood the month
        public const int MostNights = 16;

        private readonly List<CalendarEntry> _pool = new List<CalendarEntry>(64);
        public readonly List<CalendarEntry> Items = new List<CalendarEntry>(64);
        public readonly List<NightLine> Nights = new List<NightLine>(8);
        private readonly List<NightLine> _nightPool = new List<NightLine>(8);

        public int FromDay, ToDay;

        public void Begin(int fromDay, int toDay)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                _pool.Add(Items[i]);
            }
            Items.Clear();
            for (int i = 0; i < Nights.Count; i++)
            {
                _nightPool.Add(Nights[i]);
            }
            Nights.Clear();
            FromDay = fromDay;
            ToDay = toDay;
        }

        /// <summary>
        /// Takes off everything added since the list held so many entries and nightly lines, back to their pools: what a source
        /// that failed part way through left behind (CalendarDirector.Look). Counts past what the list holds change nothing.
        /// </summary>
        public void Cut(int items, int nights)
        {
            for (int i = Items.Count - 1; i >= items && i >= 0; i--)
            {
                _pool.Add(Items[i]);
                Items.RemoveAt(i);
            }
            for (int i = Nights.Count - 1; i >= nights && i >= 0; i--)
            {
                _nightPool.Add(Nights[i]);
                Nights.RemoveAt(i);
            }
        }

        /// <summary>A blank entry on that day, already in the list; null for a day outside the look, or a full list.</summary>
        public CalendarEntry Add(string source, int a, int b, CalendarGroup group, int day)
        {
            if (day < FromDay || day > ToDay || Items.Count >= MostEntries)
            {
                return null;
            }
            CalendarEntry entry;
            if (_pool.Count > 0)
            {
                entry = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
                entry.Reset();
            }
            else
            {
                entry = new CalendarEntry();
            }
            entry.Source = source;
            entry.A = a;
            entry.B = b;
            entry.Group = group;
            entry.Day = day;
            Items.Add(entry);
            return entry;
        }

        /// <summary>A nightly line; nothing for an amount of nought or a full list.</summary>
        public void Night(string source, string title, int sign, float amount)
        {
            if (amount <= 0f || Nights.Count >= MostNights)
            {
                return;
            }
            NightLine line;
            if (_nightPool.Count > 0)
            {
                line = _nightPool[_nightPool.Count - 1];
                _nightPool.RemoveAt(_nightPool.Count - 1);
            }
            else
            {
                line = new NightLine();
            }
            line.Source = source;
            line.Title = title;
            line.Sign = sign < 0 ? -1 : 1;
            line.Amount = amount;
            Nights.Add(line);
        }
    }

    /// <summary>One source of the calendar's days: reads a feature's own schedule, never changes it, and is asked only when a month is shown.</summary>
    public interface ICalendarSource
    {
        /// <summary>Short and never changed ("bills", "game").</summary>
        string Id { get; }
        bool Available { get; }   // false while its feature is off or not loaded
        /// <summary>Adds what falls on fromDay..toDay (into.Add) and what moves every night (into.Night).</summary>
        void Collect(int fromDay, int toDay, CalendarList into);
    }
}
