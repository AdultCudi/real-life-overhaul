using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The charts the two apps draw: a line over points in time (the city's rents by month, a showroom's takings by day) and the bars of a unit's tax year.
    /// A chart is made only from figures that exist and are not printed beside it: never from a table's own column (that repeats the table). Game-free.
    /// </summary>
    public static class EstateCharts
    {
        public static UiChartModel Line(string title, string[] points, float[] values, string label, Func<float, string> format)
        {
            if (points == null || values == null || points.Length < 2 || points.Length != values.Length)
            {
                return null;
            }
            return new UiChartModel
            {
                Kind = UiChartKind.Line, Title = title, Points = points, Series = new[] { new UiSeries { Label = label ?? title, Values = values, Tint = UiRole.None } },
                Format = format, AxisFormat = format, Still = true, EvenAxis = true
            };
        }

        /// <summary>Dollars.</summary>
        public static string Dollars(float v) => "$" + ((long)Math.Round(v)).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }
}
