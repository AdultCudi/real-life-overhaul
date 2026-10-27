using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Outlook
{
    /// <summary>
    /// The daily history of the figures the Economy app draws: for each figure (a name) the last <see cref="OutlookCatalogue.HistoryDays"/>
    /// days, one whole number a day. It is a ring keyed by the game's day, so a day is always in the same cell, a day older than
    /// the window falls out by being written over, and nothing grows: at most <see cref="OutlookCatalogue.LinesMost"/> figures
    /// of 120 days each. A day with no reading stays empty (the city's economy was off, the figure's feature was). Game-free;
    /// every call is a few array reads, and none allocates once the figure has its line.
    /// </summary>
    public sealed class OutlookBook
    {
        private sealed class Line
        {
            public readonly int[] Value = new int[OutlookCatalogue.HistoryDays];
            public readonly int[] Stamp = new int[OutlookCatalogue.HistoryDays];   // the game's day each cell holds; 0: none
        }

        private readonly Dictionary<string, Line> _lines = new Dictionary<string, Line>(48, StringComparer.Ordinal);

        /// <summary>The newest day any figure was written for; 0 for an empty book.</summary>
        public int LastDay { get; private set; }

        /// <summary>How many figures the book keeps.</summary>
        public int Lines => _lines.Count;

        /// <summary>Figures refused because the book was full, since it was made.</summary>
        public int Refused { get; private set; }

        public void Clear()
        {
            _lines.Clear();
            LastDay = 0;
            Refused = 0;
        }

        /// <summary>The names of the figures kept, for the store to write; a copy.</summary>
        public List<string> Names()
        {
            var names = new List<string>(_lines.Keys);
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        /// <summary>True if the figure has a line, whatever days it holds.</summary>
        public bool Has(string key)
        {
            return key != null && _lines.ContainsKey(key);
        }

        /// <summary>
        /// Writes a figure for a day, over what was there. Refused (false) for no name, a day under 1, the no-value mark, a day the
        /// window has already passed, or a new figure when the book is full.
        /// </summary>
        public bool Set(string key, int day, int value)
        {
            if (string.IsNullOrEmpty(key) || day <= 0 || value == OutlookCatalogue.NoValue)
            {
                return false;
            }
            if (LastDay > 0 && day <= LastDay - OutlookCatalogue.HistoryDays)
            {
                return false;
            }
            Line line;
            if (!_lines.TryGetValue(key, out line))
            {
                if (_lines.Count >= OutlookCatalogue.LinesMost)
                {
                    Refused++;
                    return false;
                }
                line = new Line();
                _lines[key] = line;
            }
            int cell = day % OutlookCatalogue.HistoryDays;
            line.Value[cell] = value;
            line.Stamp[cell] = day;
            if (day > LastDay)
            {
                LastDay = day;
            }
            return true;
        }

        /// <summary>The figure on a day; false for a day with none, outside the window, or a figure the book does not keep.</summary>
        public bool Get(string key, int day, out int value)
        {
            value = OutlookCatalogue.NoValue;
            Line line;
            if (key == null || day <= 0 || day > LastDay || day <= LastDay - OutlookCatalogue.HistoryDays || !_lines.TryGetValue(key, out line))
            {
                return false;
            }
            int cell = day % OutlookCatalogue.HistoryDays;
            if (line.Stamp[cell] != day)
            {
                return false;
            }
            value = line.Value[cell];
            return true;
        }

        /// <summary>
        /// The figure's last <paramref name="count"/> days, oldest first, ending on <paramref name="endDay"/>, in
        /// <paramref name="into"/> (which must be that long); a day with no reading holds <see cref="OutlookCatalogue.NoValue"/>.
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
                    into[i] = OutlookCatalogue.NoValue;
                }
            }
            return found;
        }

        /// <summary>The newest reading of a figure and its day; false for none in the window.</summary>
        public bool Newest(string key, out int day, out int value)
        {
            for (int d = LastDay; d > LastDay - OutlookCatalogue.HistoryDays && d > 0; d--)
            {
                if (Get(key, d, out value))
                {
                    day = d;
                    return true;
                }
            }
            day = 0;
            value = OutlookCatalogue.NoValue;
            return false;
        }

        /// <summary>Days with a reading among the last <paramref name="back"/> up to <paramref name="endDay"/>.</summary>
        public int Days(string key, int endDay, int back)
        {
            int n = 0;
            for (int i = 0; i < back; i++)
            {
                n += Get(key, endDay - i, out _) ? 1 : 0;
            }
            return n;
        }
    }
}
