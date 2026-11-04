using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The charts the two apps draw: a line over points in time (the city's rents by month, a showroom's takings by day) and the bars of a unit's tax year.
    /// A chart is made only from figures that exist and are not printed beside it: never from a table's own column (that repeats the table). Game-free.
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

        /// <summary>A page's own few figures side by side, each named: the money (or, with a percent format, the shares) the page's words already say.</summary>
        public static UiChartModel Few(string title, string[] names, float[] values, bool percent = false)
        {
            // Figures too far apart draw a hair of a bar beside a wall: such a page keeps its words.
            return Reads(new System.Collections.Generic.List<float>(values)) ? Bars(title, names, values, null, percent ? (Func<float, string>)Percent : Dollars) : null;
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

        /// <summary>A whole number with its grouping, as the game writes a count.</summary>
        public static string Count(float v) => ((int)Math.Round(v)).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>A share of a hundred.</summary>
        public static string Percent(float v) => ((int)Math.Round(v)).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";

        /// <summary>Dollars.</summary>
        public static string Dollars(float v) => "$" + ((long)Math.Round(v)).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
