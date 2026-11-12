using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.DaySeries
{
    /// <summary>One loan a rival or a city business took, as the feed shows it. The bank is a rank, resolved to the city's own bank when drawn.</summary>
    public struct DayLoan
    {
        public string Who;
        public int Rank;
        public int Day;
        public int Amount;
    }

    /// <summary>
    /// The daily history: for each named figure the last <see cref="DaySeriesCatalogue.Days"/> days, one whole number a day.
    /// A ring keyed by the game's day, so a day is always in the same cell and a day older than the window is written over.
    /// Nothing grows. A day with no reading stays empty. Game-free; a call is a few array reads, and none allocates once the
    /// figure has its line. The loan feed and the timed price cut sit on the same book so they share its one save line.
    /// </summary>
    public sealed class DaySeriesBook
    {
        private sealed class Line
        {
            public readonly int[] Value = new int[DaySeriesCatalogue.Days];
            public readonly int[] Stamp = new int[DaySeriesCatalogue.Days];
        }

        private readonly Dictionary<string, Line> _lines = new Dictionary<string, Line>(48, StringComparer.Ordinal);
        private readonly List<DayLoan> _loans = new List<DayLoan>(16);

        /// <summary>The newest day any figure was written for; 0 for an empty book.</summary>
        public int LastDay { get; private set; }

        /// <summary>The seed the loan feed rolls from, the same on every machine that shares the save.</summary>
        public int Seed { get; set; }

        /// <summary>The day a price cut of 5 per cent was taken; 0 for none.</summary>
        public int CutFrom { get; set; }

        /// <summary>The first day the price cut no longer holds.</summary>
        public int CutUntil { get; set; }

        /// <summary>Plan ids that were already holding their prices, so the cut's end does not turn their hold off.</summary>
        public string HeldAlready { get; set; } = string.Empty;

        /// <summary>Plan ids the price cut put on hold, so its end takes off those holds and no others.</summary>
        public string HeldByCut { get; set; } = string.Empty;

        /// <summary>How many figures the book keeps.</summary>
        public int Lines => _lines.Count;

        /// <summary>Figures refused because the book was full, since it was made.</summary>
        public int Refused { get; private set; }

        /// <summary>Bumped whenever a figure or a loan is written, so a chart rebuilds then and not every frame.</summary>
        public int Generation { get; private set; }

        public int LoanCount => _loans.Count;

        public void Clear()
        {
            _lines.Clear();
            _loans.Clear();
            LastDay = 0;
            Seed = 0;
            CutFrom = 0;
            CutUntil = 0;
            HeldAlready = string.Empty;
            HeldByCut = string.Empty;
            Refused = 0;
            Generation++;
        }

        /// <summary>The names of the figures kept, for the store to write; a copy, in order.</summary>
        public List<string> Names()
        {
            var names = new List<string>(_lines.Keys);
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        public bool Has(string key) => key != null && _lines.ContainsKey(key);

        /// <summary>
        /// Writes a figure for a day, over what was there. Refused for no name, a day under 1, the no-value mark, a day the
        /// window has already passed, or a new figure when the book is full.
        /// </summary>
        public bool Set(string key, int day, int value)
        {
            if (string.IsNullOrEmpty(key) || day <= 0 || value == DaySeriesCatalogue.NoValue)
            {
                return false;
            }
            if (LastDay > 0 && day <= LastDay - DaySeriesCatalogue.Days)
            {
                return false;
            }
            if (!_lines.TryGetValue(key, out Line line))
            {
                if (_lines.Count >= DaySeriesCatalogue.LinesMost)
                {
                    Refused++;
                    return false;
                }
                line = new Line();
                _lines[key] = line;
            }
            int cell = day % DaySeriesCatalogue.Days;
            bool changed = line.Stamp[cell] != day || line.Value[cell] != value;
            line.Value[cell] = value;
            line.Stamp[cell] = day;
            if (day > LastDay)
            {
                LastDay = day;
            }
            if (changed)
            {
                Generation++;
            }
            return true;
        }

        /// <summary>The figure on a day; false for a day with none, outside the window, or a figure the book does not keep.</summary>
        public bool Get(string key, int day, out int value)
        {
            value = DaySeriesCatalogue.NoValue;
            if (key == null || day <= 0 || day > LastDay || day <= LastDay - DaySeriesCatalogue.Days || !_lines.TryGetValue(key, out Line line))
            {
                return false;
            }
            int cell = day % DaySeriesCatalogue.Days;
            if (line.Stamp[cell] != day)
            {
                return false;
            }
            value = line.Value[cell];
            return true;
        }

        /// <summary>
        /// The figure's last <paramref name="count"/> days, oldest first, ending on <paramref name="endDay"/>, into
        /// <paramref name="into"/> (which must be that long). A day with no reading holds <see cref="DaySeriesCatalogue.NoValue"/>.
        /// Returns how many days had one.
        /// </summary>
        public int Window(string key, int endDay, int count, int[] into)
        {
            int found = 0;
            if (into == null)
            {
                return 0;
            }
            count = Math.Min(count, into.Length);
            for (int i = 0; i < count; i++)
            {
                int day = endDay - (count - 1 - i);
                if (Get(key, day, out int value))
                {
                    into[i] = value;
                    found++;
                }
                else
                {
                    into[i] = DaySeriesCatalogue.NoValue;
                }
            }
            return found;
        }

        public DayLoan LoanAt(int index) => index >= 0 && index < _loans.Count ? _loans[index] : default(DayLoan);

        /// <summary>Appends a loan, dropping the oldest past the cap. The same who, day and rank is kept once.</summary>
        public bool AddLoan(string who, int rank, int day, int amount)
        {
            who = DaySeriesCatalogue.Clean(who);
            if (who.Length == 0 || day <= 0 || amount <= 0 || rank < 0)
            {
                return false;
            }
            for (int i = 0; i < _loans.Count; i++)
            {
                DayLoan had = _loans[i];
                if (had.Day == day && had.Rank == rank && had.Who == who)
                {
                    return false;
                }
            }
            if (_loans.Count >= DaySeriesCatalogue.LoansMost)
            {
                _loans.RemoveAt(0);
            }
            _loans.Add(new DayLoan { Who = who, Rank = rank, Day = day, Amount = amount });
            Generation++;
            return true;
        }

        /// <summary>Copies the loans, oldest first, for the store.</summary>
        public void CopyLoans(List<DayLoan> into)
        {
            into.Clear();
            for (int i = 0; i < _loans.Count; i++)
            {
                into.Add(_loans[i]);
            }
        }
    }
}
