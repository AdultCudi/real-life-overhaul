using System.Collections.Generic;

namespace RealLifeOverhaul.Calendar
{
    // Everything in this file is free of the game and of the engine: RealLifeOverhaul.Build/EconomyCheck compiles it as it
    // is and runs it outside the game.

    /// <summary>What a day of the calendar holds: money that moves, the player's own time, or something on in the city.</summary>
    public enum CalendarGroup
    {
        Money = 0,
        Time = 1,
        City = 2
    }

    /// <summary>
    /// One thing on one day of the calendar. Entries are taken from a pool (CalendarList.Add) and filled by a source;
    /// every source writes its words with them, since the calendar asks only when it is shown.
    /// </summary>
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

    /// <summary>
    /// What moves every night whatever the day: the game's rent, wages and loans, the rent the player's buildings bring
    /// in. These are said once for the month, not marked on each of its days.
    /// </summary>
    public sealed class NightLine
    {
        public string Source;
        public string Title;
        public int Sign;             // +1 in, -1 out
        public float Amount;         // whole money a night
    }

    /// <summary>
    /// The entries of one look at the calendar's sources, for the days FromDay to ToDay, and the lines of what moves every
    /// night. Its entries are kept and used again from look to look.
    /// </summary>
    public sealed class CalendarList
    {
        /// <summary>A look never holds more than this: a source gone wrong cannot fill the month.</summary>
        public const int MostEntries = 600;
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

        /// <summary>Money that moves every night, in or out; nothing for an amount of nought or a full list.</summary>
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

    /// <summary>
    /// One source of the calendar's days. A source reads a feature's (or the game's) own state, from its real schedule,
    /// and never changes it. It is asked only when the calendar is shown: when the app opens, a month is turned or a day
    /// begins while it is open. "Available" is false while its feature is off or not loaded, and then it costs nothing.
    /// </summary>
    public interface ICalendarSource
    {
        /// <summary>Short and never changed ("bills", "game").</summary>
        string Id { get; }
        bool Available { get; }
        /// <summary>Adds what falls on the days fromDay..toDay (into.Add gives a blank entry, or null for a day outside them), and what moves every night (into.Night).</summary>
        void Collect(int fromDay, int toDay, CalendarList into);
    }
}
