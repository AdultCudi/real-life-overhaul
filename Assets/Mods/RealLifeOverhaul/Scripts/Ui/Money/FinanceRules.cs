using System;
using RealLifeOverhaul.DaySeries;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Finance screen's arithmetic that needs no game: which days of a series exist (a chart never draws a day before the save began
    /// as zero), the row a choice maps to, and the sums the loan page shows. The same inputs always give the same answers. Game-free.
    /// </summary>
    public static class FinanceRules
    {
        /// <summary>
        /// The days of a series that exist, oldest first, ending on <paramref name="endDay"/>, into <paramref name="values"/> and
        /// <paramref name="days"/>: nothing before the first reading, and, with <paramref name="positiveOnly"/>, nothing for a day
        /// that read zero or less (a rival not yet come forward). A day with no reading between two that have one keeps the one before.
        /// Returns how many were written.
        /// </summary>
        public static int ExistingDays(DaySeriesBook book, string key, int endDay, int most, bool positiveOnly, float[] values, int[] days)
        {
            if (book == null || values == null || days == null || endDay <= 0 || most <= 0)
            {
                return 0;
            }
            most = Math.Min(most, Math.Min(values.Length, days.Length));
            int n = 0;
            float last = 0f;
            for (int i = 0; i < most; i++)
            {
                int day = endDay - (most - 1 - i);
                if (day <= 0)
                {
                    continue;
                }
                bool there = book.Get(key, day, out int raw) && (!positiveOnly || raw > 0);
                if (there)
                {
                    last = raw;
                }
                else if (n == 0)
                {
                    continue;   // before the first reading: not a zero, not a day
                }
                values[n] = last;
                days[n] = day;
                n++;
            }
            return n;
        }

        /// <summary>
        /// The net of each cash category over the last <paramref name="window"/> days ending on <paramref name="endDay"/>, from the saved ledger (series
        /// <c>cat.N</c>), into <paramref name="net"/>. A day the ledger does not hold is not counted, and is not a zero. Returns how many days it held;
        /// <paramref name="first"/> is the earliest of them, 0 with none.
        /// </summary>
        public static int CategoryNet(DaySeriesBook book, int endDay, int window, long[] net, out int first)
        {
            first = 0;
            if (net == null)
            {
                return 0;
            }
            Array.Clear(net, 0, net.Length);
            int have = 0;
            for (int d = Math.Max(1, endDay - window + 1); book != null && d <= endDay; d++)
            {
                bool any = false;
                for (int i = 0; i < net.Length; i++)
                {
                    if (book.Get(DaySeriesCatalogue.Category(i), d, out int value))
                    {
                        net[i] += value;
                        any = true;
                    }
                }
                if (any)
                {
                    have++;
                    first = first == 0 ? d : first;
                }
            }
            return have;
        }

        /// <summary>The row a choice stands on, kept inside the table (-1 when there is no row).</summary>
        public static int Clamp(int row, int rows) => rows <= 0 ? -1 : row < 0 ? 0 : row >= rows ? rows - 1 : row;

        /// <summary>The worth a series unit stands for, in dollars (the series keeps worth in thousands).</summary>
        public static long Dollars(float units) => (long)Math.Round(units * DaySeriesCatalogue.WorthScale);

        /// <summary>The step after or before an amount on the list of round sums, kept within its range; -1 where there is none.</summary>
        public static int StepAmount(int amount, int least, int most, bool up, int[] steps)
        {
            if (steps == null || steps.Length == 0)
            {
                return -1;
            }
            if (up)
            {
                if (amount >= most)
                {
                    return -1;
                }
                for (int i = 0; i < steps.Length; i++)
                {
                    if (steps[i] > amount)
                    {
                        return Math.Min(most, steps[i]);
                    }
                }
                return most;
            }
            if (amount <= least)
            {
                return -1;
            }
            for (int i = steps.Length - 1; i >= 0; i--)
            {
                if (steps[i] < amount)
                {
                    return Math.Max(least, steps[i]);
                }
            }
            return least;
        }

        public const int ChartFew = 12, ChartLong = 32;   // a chart of more than 12 and under 32 points is sampled; 32 or more reads by its axis alone (UiChart.MostTargets)

        /// <summary>
        /// A line chart of the days that exist, one series for each of <paramref name="values"/>: all the days when there are 12 or fewer, or 32 or
        /// more (a long chart reads by its axis); between, every few days, always ending on the last, so no point is a target too small to press.
        /// Under two days there is nothing to draw and the chart says so.
        /// </summary>
        public static UiChartModel Chart(string title, string[] labels, float[][] values, int[] days, int n, Func<float, string> format, string emptyTitle, string emptyBody)
        {
            var model = new UiChartModel { Kind = UiChartKind.Line, Title = title, Format = format, AxisFormat = format, EmptyTitle = emptyTitle, EmptyBody = emptyBody };
            if (n < 2 || values == null || values.Length == 0)
            {
                return model;
            }
            int step = n > ChartFew && n < ChartLong ? (n + ChartFew - 1) / ChartFew : 1;
            int count = 0;
            for (int i = n - 1; i >= 0; i -= step)
            {
                count++;
            }
            var points = new string[count];
            var series = new UiSeries[values.Length];
            for (int k = 0; k < series.Length; k++)
            {
                series[k] = new UiSeries { Label = labels != null && k < labels.Length ? labels[k] : title, Values = new float[count] };
            }
            int at = count - 1;
            for (int i = n - 1; i >= 0 && at >= 0; i -= step, at--)
            {
                points[at] = days[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
                for (int k = 0; k < series.Length; k++)
                {
                    series[k].Values[at] = values[k][i];
                }
            }
            model.Points = points;
            model.Series = series;
            return model;
        }

        /// <summary>A figure as a whole share of a whole, 0 to 100.</summary>
        public static int Share(float part, float whole) => whole <= 0f ? 0 : (int)Math.Max(0f, Math.Min(100f, (float)Math.Round(part / whole * 100f)));
    }
}
