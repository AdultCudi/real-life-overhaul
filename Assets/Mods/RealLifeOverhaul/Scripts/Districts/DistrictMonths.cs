using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RealLifeOverhaul.Districts
{
    /// <summary>
    /// The district view's monthly snapshots: each layer's figure for each district, kept once as a month begins (the first read of that month), so the view can say how a
    /// figure has moved this month. Ten layers by the districts by <see cref="Keep"/> months, as whole numbers, the districts' figures of the one source the view reads
    /// (rivals' and the city's rents are in the same figures). The line in the save, <c>reallife:district_months</c>:
    /// <c>1;month=day:district=v0,v1,...,v9:district=...|month=...</c>, a figure the district had none for as a dot. A line this version cannot read is left as it was
    /// read (Core/SaveLines). Game-free; its cost is a few lists of numbers, read once a day.
    /// </summary>
    public sealed class DistrictMonths
    {
        public const string Key = "reallife:district_months";
        public const int Keep = 12;                 // months kept, the oldest dropped first
        public const int Layers = DistrictRules.Layers;
        public const int DistrictsMost = 16;        // districts kept in a month: a city has seven to ten
        private const string Version = "1";
        private const long None = long.MinValue;
        private static readonly char[] Separators = { ';', '=', '|', ':' };

        private sealed class Month
        {
            public int Index, Day;                                                   // the month and the day it was first read
            public readonly Dictionary<string, long[]> Figures = new Dictionary<string, long[]>(16, StringComparer.Ordinal);
        }

        private readonly List<Month> _months = new List<Month>(Keep + 1);

        /// <summary>Months kept now.</summary>
        public int Count => _months.Count;

        /// <summary>The month a day is in, as the mod's calendar counts them (a twelfth of the save's year; a day before the first is the first).</summary>
        public static int MonthOf(int day, int year) => CalendarRules.MonthOf(Math.Max(1, day), year);

        /// <summary>
        /// Keeps what the districts' figures are now as the start of the month <paramref name="day"/> is in, when that month has none yet; nothing when it has. The figures are
        /// per district, one entry a layer, <paramref name="has"/> saying which are real. True when a month was kept.
        /// </summary>
        public bool Take(int day, int year, IList<string> districts, IList<long[]> figures, IList<bool[]> has)
        {
            int month = MonthOf(day, year);
            if (Find(month) != null || districts == null || districts.Count == 0)
            {
                return false;
            }
            var kept = new Month { Index = month, Day = day };
            for (int i = 0; i < districts.Count && kept.Figures.Count < DistrictsMost; i++)
            {
                if (string.IsNullOrEmpty(districts[i]) || districts[i].IndexOfAny(Separators) >= 0)
                {
                    continue;
                }
                var row = new long[Layers];
                for (int l = 0; l < Layers; l++)
                {
                    row[l] = l < figures[i].Length && l < has[i].Length && has[i][l] ? figures[i][l] : None;
                }
                kept.Figures[districts[i]] = row;
            }
            _months.Add(kept);
            _months.Sort((a, b) => a.Index.CompareTo(b.Index));
            while (_months.Count > Keep)
            {
                _months.RemoveAt(0);
            }
            return true;
        }

        private Month Find(int month)
        {
            for (int i = 0; i < _months.Count; i++)
            {
                if (_months[i].Index == month)
                {
                    return _months[i];
                }
            }
            return null;
        }

        /// <summary>
        /// How a district's figure has moved this month: <paramref name="start"/> what it was when the month began and the change in tenths of a per cent of that. False
        /// ("no change yet") when this month has no start for it, when the start was kept today (no day has passed), or when it had no figure or none now.
        /// </summary>
        public bool Change(int day, int year, string district, int layer, long now, bool hasNow, out long start, out int tenths)
        {
            start = 0;
            tenths = 0;
            Month m = Find(MonthOf(day, year));
            if (m == null || m.Day >= day || !hasNow || layer < 0 || layer >= Layers || district == null || !m.Figures.TryGetValue(district, out long[] row) || row[layer] == None)
            {
                return false;
            }
            start = row[layer];
            if (start == 0)
            {
                return false;
            }
            tenths = Tenths(now - start, start);
            return true;
        }

        /// <summary>The change of the whole city's figure this month: the districts that have a start and a figure now, summed. False when none has.</summary>
        public bool CityChange(int day, int year, IList<string> districts, int layer, IList<long> now, IList<bool> hasNow, out int tenths)
        {
            tenths = 0;
            long sumStart = 0, sumNow = 0;
            int n = 0;
            for (int i = 0; i < districts.Count; i++)
            {
                if (Change(day, year, districts[i], layer, now[i], hasNow[i], out long start, out int _))
                {
                    sumStart += start;
                    sumNow += now[i];
                    n++;
                }
            }
            if (n == 0 || sumStart == 0)
            {
                return false;
            }
            tenths = Tenths(sumNow - sumStart, sumStart);
            return true;
        }

        // The change as tenths of a per cent of the start, rounded to the nearest and kept within four figures.
        private static int Tenths(long delta, long start)
        {
            long half = Math.Abs(start) / 2;
            long t = (delta * 1000L + (delta >= 0 == start > 0 ? half : -half)) / start;
            return (int)Math.Max(-9999L, Math.Min(9999L, t));
        }

        /// <summary>The line for the save; null when nothing is kept (no key then).</summary>
        public string Write()
        {
            if (_months.Count == 0)
            {
                return null;
            }
            var w = new StringBuilder(64 + _months.Count * 256);
            w.Append(Version);
            for (int m = 0; m < _months.Count; m++)
            {
                w.Append(m == 0 ? ';' : '|').Append(_months[m].Index.ToString(CultureInfo.InvariantCulture)).Append('=').Append(_months[m].Day.ToString(CultureInfo.InvariantCulture));
                foreach (KeyValuePair<string, long[]> d in _months[m].Figures)
                {
                    w.Append(':').Append(d.Key).Append('=');
                    for (int l = 0; l < Layers; l++)
                    {
                        w.Append(l > 0 ? "," : string.Empty).Append(d.Value[l] == None ? "." : d.Value[l].ToString(CultureInfo.InvariantCulture));
                    }
                }
            }
            return w.ToString();
        }

        /// <summary>True when a line is one this version reads: none, or one of its own version.</summary>
        public static bool Readable(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return true;
            }
            int cut = line.IndexOf(';');
            return (cut < 0 ? line : line.Substring(0, cut)) == Version;
        }

        /// <summary>The months a line holds; a month or district that cannot be read is left out, a line of another version gives none.</summary>
        public static DistrictMonths Read(string line)
        {
            var months = new DistrictMonths();
            if (!Readable(line) || string.IsNullOrEmpty(line) || line.IndexOf(';') < 0)
            {
                return months;
            }
            string[] each = line.Substring(line.IndexOf(';') + 1).Split('|');
            for (int i = 0; i < each.Length; i++)
            {
                string[] parts = each[i].Split(':');
                string[] head = parts[0].Split('=');
                if (head.Length != 2 || !int.TryParse(head[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                    || !int.TryParse(head[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int day))
                {
                    continue;
                }
                var month = new Month { Index = index, Day = day };
                for (int d = 1; d < parts.Length && month.Figures.Count < DistrictsMost; d++)
                {
                    string[] pair = parts[d].Split('=');
                    string[] values = pair.Length == 2 ? pair[1].Split(',') : null;
                    if (values == null || values.Length != Layers || pair[0].Length == 0)
                    {
                        continue;
                    }
                    var row = new long[Layers];
                    bool ok = true;
                    for (int l = 0; l < Layers && ok; l++)
                    {
                        if (values[l] == ".")
                        {
                            row[l] = None;
                        }
                        else
                        {
                            ok = long.TryParse(values[l], NumberStyles.Integer, CultureInfo.InvariantCulture, out row[l]);
                        }
                    }
                    if (ok)
                    {
                        month.Figures[pair[0]] = row;
                    }
                }
                if (months.Find(index) == null)
                {
                    months._months.Add(month);
                }
            }
            months._months.Sort((a, b) => a.Index.CompareTo(b.Index));
            while (months._months.Count > Keep)
            {
                months._months.RemoveAt(0);
            }
            return months;
        }
    }
}
