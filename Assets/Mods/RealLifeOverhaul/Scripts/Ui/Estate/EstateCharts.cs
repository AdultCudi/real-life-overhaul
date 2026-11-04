using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The charts the two apps put under a short page so it reads whole: bars of a page's own figures (a rent for each unit, a condition for each
    /// building, a price against its worth) or a line over days, on the kit's chart. A chart is made only from figures the page already has, so
    /// it never plots a day or a unit that does not exist, and with fewer than one bar it is not made. Game-free.
    /// </summary>
    public static class EstateCharts
    {
        /// <summary>Bars by name: one bar for each, in the chart's first hue or each its own tint. Null when there is nothing to plot.</summary>
        public static UiChartModel Bars(string title, string[] names, float[] values, string label, Func<float, string> format, UiRole[] tints = null)
        {
            if (names == null || values == null || names.Length == 0 || names.Length != values.Length)
            {
                return null;
            }
            return new UiChartModel
            {
                Kind = UiChartKind.Bars, Title = title, Points = names, Series = new[] { new UiSeries { Label = label ?? title, Values = values, Tint = UiRole.None } },
                BarTints = tints, Format = format, AxisFormat = format, Still = true
            };
        }

        /// <summary>A line over points in time (days, months): the points that exist. Null with fewer than two.</summary>
        public static UiChartModel Line(string title, string[] points, float[] values, string label, Func<float, string> format)
        {
            if (points == null || values == null || points.Length < 2 || points.Length != values.Length)
            {
                return null;
            }
            return new UiChartModel
            {
                Kind = UiChartKind.Line, Title = title, Points = points, Series = new[] { new UiSeries { Label = label ?? title, Values = values, Tint = UiRole.None } },
                Format = format, AxisFormat = format, Still = true
            };
        }

        private const int Most = 12;

        /// <summary>
        /// The chart of a table's own figures: bars of its graded column (satisfaction, record) where it has one, else of its money column, else its
        /// percentages, one bar for each of the first rows that hold a figure. Made from what the table shows, so it never plots anything else.
        /// </summary>
        public static UiChartModel FromTable(UiDataTableModel t)
        {
            if (t == null || t.Rows == null || t.Rows.Length == 0 || t.Columns == null)
            {
                return null;
            }
            for (int pass = 0; pass < 3; pass++)
            {
                for (int c = 0; c < t.Columns.Length; c++)
                {
                    UiCellKind kind = t.Columns[c].Kind;
                    bool want = pass == 0 ? kind == UiCellKind.Meter : pass == 1 ? kind == UiCellKind.Money : kind == UiCellKind.Figure;
                    if (!want)
                    {
                        continue;
                    }
                    var names = new System.Collections.Generic.List<string>(Most);
                    var values = new System.Collections.Generic.List<float>(Most);
                    var used = new System.Collections.Generic.List<int>(Most);
                    for (int r = 0; r < t.RowCount && r < t.Rows.Length && names.Count < Most; r++)
                    {
                        UiDataRow row = t.Rows[r];
                        float v;
                        int meter = row.Values != null && c < row.Values.Length ? row.Values[c] : -1;
                        bool got = pass == 0 ? (v = meter) >= 0 : Parse(row.Cells != null && c < row.Cells.Length ? row.Cells[c] : null, out v, out bool pct) && v > 0;
                        if (got && row.Cells != null && row.Cells.Length > 0 && !string.IsNullOrEmpty(row.Cells[0]))
                        {
                            names.Add(Short(row.Cells[0]));
                            values.Add(v);
                            used.Add(r);
                        }
                    }
                    if (names.Count >= 2 && Reads(values) && Varies(values))
                    {
                        Distinct(names, t, used);
                        return Bars(t.Columns[c].Title, names.ToArray(), values.ToArray(), t.Columns[c].Title, pass == 1 ? (Func<float, string>)Dollars : pass == 0 ? Percent : Percent);
                    }
                }
            }
            return t.RowCount == 1 ? Across(t) : t.RowCount >= 4 ? Counted(t) : null;
        }

        // The states a table's rows are in, counted: "Open 3, Booked 2, Sound 3", for a table whose figures are too far apart to draw as bars.
        private static UiChartModel Counted(UiDataTableModel t)
        {
            for (int c = 0; c < t.Columns.Length; c++)
            {
                if (t.Columns[c].Kind != UiCellKind.Chip)
                {
                    continue;
                }
                var names = new System.Collections.Generic.List<string>(Most);
                var counts = new System.Collections.Generic.List<float>(Most);
                for (int r = 0; r < t.RowCount && r < t.Rows.Length; r++)
                {
                    string chip = t.Rows[r].Cells != null && c < t.Rows[r].Cells.Length ? t.Rows[r].Cells[c] : null;
                    int at = chip == null ? -1 : names.IndexOf(chip);
                    if (at >= 0)
                    {
                        counts[at] += 1f;
                    }
                    else if (!string.IsNullOrEmpty(chip) && names.Count < Most)
                    {
                        names.Add(chip);
                        counts.Add(1f);
                    }
                }
                if (names.Count >= 2 && Reads(counts))
                {
                    return Bars(t.Columns[c].Title, names.ToArray(), counts.ToArray(), t.Columns[c].Title, Count);
                }
            }
            return null;
        }

        // Names that collide ("41 E 37th Street" for three flats) say the unit instead: the words after the street's comma, else the row's own second cell.
        private static void Distinct(System.Collections.Generic.List<string> names, UiDataTableModel t, System.Collections.Generic.List<int> used)
        {
            var before = names.ToArray();
            for (int i = 0; i < names.Count; i++)
            {
                bool twice = false;
                for (int k = 0; k < before.Length && !twice; k++)
                {
                    twice = k != i && before[k] == before[i];
                }
                if (twice && t.Rows[used[i]].Cells != null)
                {
                    string full = t.Rows[used[i]].Cells[0] ?? string.Empty;
                    int comma = full.IndexOf(',');
                    names[i] = comma > 0 ? full.Substring(comma + 1).Trim() : names[i];
                }
            }
        }

        /// <summary>A page's own few figures side by side, each named: the money (or, with a percent format, the shares) the page's words already say.</summary>
        public static UiChartModel Few(string title, string[] names, float[] values, bool percent = false)
        {
            // Figures too far apart draw a hair of a bar beside a wall: such a page keeps its words.
            return Reads(new System.Collections.Generic.List<float>(values)) ? Bars(title, names, values, null, percent ? (Func<float, string>)Percent : Dollars) : null;
        }

        // Bars that differ: a chart of figures all alike (every offer near the asking price) says nothing the table does not.
        private static bool Varies(System.Collections.Generic.List<float> v)
        {
            float lo = float.MaxValue, hi = 0f;
            for (int i = 0; i < v.Count; i++)
            {
                lo = Math.Min(lo, v[i]);
                hi = Math.Max(hi, v[i]);
            }
            return (hi - lo) * 6f >= hi;
        }

        // A table of one row: its money (else its percentages) side by side as bars, each named by its column.
        private static UiChartModel Across(UiDataTableModel t)
        {
            UiDataRow row = t.Rows[0];
            var names = new System.Collections.Generic.List<string>(6);
            var values = new System.Collections.Generic.List<float>(6);
            for (int c = 0; c < t.Columns.Length && row.Cells != null && c < row.Cells.Length; c++)
            {
                float v;
                bool pct;
                if ((t.Columns[c].Kind == UiCellKind.Money || t.Columns[c].Kind == UiCellKind.Figure) && Parse(row.Cells[c], out v, out pct) && v > 0 && !pct)
                {
                    names.Add(t.Columns[c].Title);
                    values.Add(v);
                }
            }
            return names.Count >= 2 && Reads(values) ? Bars(row.Cells[0], names.ToArray(), values.ToArray(), row.Cells[0], Dollars) : null;
        }

        /// <summary>
        /// The chart of a detail page's own figures: its labelled rows of money set side by side as bars (an offer against its asking price and its
        /// worth), or of percentages. Null with fewer than three such rows.
        /// </summary>
        public static UiChartModel FromCards(UiFactCardModel[] left, UiFactCardModel[] right, string title)
        {
            var names = new System.Collections.Generic.List<string>(8);
            var values = new System.Collections.Generic.List<float>(8);
            var pcts = new System.Collections.Generic.List<string>(8);
            var pctValues = new System.Collections.Generic.List<float>(8);
            foreach (UiFactCardModel[] cards in new[] { left, right })
            {
                for (int i = 0; cards != null && i < cards.Length; i++)
                {
                    for (int l = 0; l < cards[i].Lines.Length && names.Count < Most; l++)
                    {
                        UiFactLine line = cards[i].Lines[l];
                        float v;
                        bool pct;
                        if (line.Kind == UiFactKind.Row && !string.IsNullOrEmpty(line.Label) && Parse(line.Value, out v, out pct) && v > 0)
                        {
                            (pct ? pcts : names).Add(line.Label.TrimEnd(':'));
                            (pct ? pctValues : values).Add(v);
                        }
                    }
                }
            }
            if (names.Count >= 3 && Reads(values))
            {
                return Bars(title, names.ToArray(), values.ToArray(), title, Dollars);
            }
            return pcts.Count >= 3 && Reads(pctValues) ? Bars(title, pcts.ToArray(), pctValues.ToArray(), title, Percent) : null;
        }

        // A figure as a page writes it: "$2,600", "+$9,700", "4.1%", "86%". False for words and for anything else ("12,400 km", "Day 290").
        private static bool Parse(string text, out float value, out bool percent)
        {
            value = 0f;
            percent = false;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            string t = text.Trim();
            bool money = t.StartsWith("$", StringComparison.Ordinal) || t.StartsWith("+$", StringComparison.Ordinal) || t.StartsWith("-$", StringComparison.Ordinal);
            percent = t.EndsWith("%", StringComparison.Ordinal);
            if (!money && !percent)
            {
                return false;
            }
            var digits = new System.Text.StringBuilder(t.Length);
            for (int i = 0; i < t.Length; i++)
            {
                if (char.IsDigit(t[i]) || t[i] == '.')
                {
                    digits.Append(t[i]);
                }
                else if (t[i] != ',' && t[i] != '$' && t[i] != '%' && t[i] != '+' && t[i] != '-' && t[i] != ' ')
                {
                    return false;
                }
            }
            return float.TryParse(digits.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value)
                && !t.StartsWith("-", StringComparison.Ordinal);
        }

        // A bar must be tall enough to read beside the tallest: a figure under a seventh of it is a strip, and the column is not charted.
        private static bool Reads(System.Collections.Generic.List<float> values)
        {
            float most = 0f, least = float.MaxValue;
            for (int i = 0; i < values.Count; i++)
            {
                most = Math.Max(most, values[i]);
                least = Math.Min(least, values[i]);
            }
            return most > 0f && least * 12f >= most;
        }

        // A name for an axis: the street before the first comma, no longer than a label holds, cut at a word.
        public static string Short(string name)
        {
            int comma = name.IndexOf(',');
            string s = (comma > 0 ? name.Substring(0, comma) : name).Trim();
            if (s.Length > 22)
            {
                int at = s.LastIndexOf(' ', 22);
                s = at > 8 ? s.Substring(0, at) : s.Substring(0, 22);
            }
            return s;
        }

        /// <summary>A whole number with its grouping, as the game writes a count.</summary>
        public static string Count(float v) => ((int)Math.Round(v)).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>A share of a hundred.</summary>
        public static string Percent(float v) => ((int)Math.Round(v)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";

        /// <summary>Dollars.</summary>
        public static string Dollars(float v) => "$" + ((long)Math.Round(v)).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
