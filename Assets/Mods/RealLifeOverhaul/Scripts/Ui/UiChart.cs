using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A chart's kind: a line over time, bars by category, or a market's candles over time with their volume.</summary>
    public enum UiChartKind : byte
    {
        Line = 0,
        Bars,
        Candles
    }

    /// <summary>One line of a chart: its name and values, oldest first, in its colour (None for the chart's hue in turn).</summary>
    public struct UiSeries
    {
        public string Label;
        public float[] Values;
        public UiRole Tint;
    }

    /// <summary>
    /// A chart's data: the points along its axis (months, districts, days), what each is called in its reading card, the
    /// lines or bars or candles, and how a value is written (the game's money, a count). Plain data.
    /// </summary>
    public sealed class UiChartModel
    {
        public UiChartKind Kind;
        public string Title;
        public string[] Points = new string[0];     // each point's name on its axis ("Mar", "Midtown")
        public string[] Reads;                      // each point's whole name in its card ("March"), or null for Points
        public UiSeries[] Series = new UiSeries[0]; // a line chart's one to four; a bar chart's first
        public UiRole[] BarTints;                   // a bar's own colour where its category has one, or null for the first hue
        public float[] Open, High, Low, Close, Volume;
        public string VolumeLabel;                  // the candles' second row ("Volume")
        public string OpenLabel, HighLabel, LowLabel, CloseLabel;
        public Func<float, string> Format;          // a value as the game writes it, in a card and at a line's end
        public Func<float, string> AxisFormat;      // a tick's value, shorter where the game has a short form ("$40k"), or null for Format
        public Func<float, string> CountFormat;     // a candle's volume as the game writes a count
        public float LeastSpan;                     // the least the value axis covers, or 0: an index rebased to 100 asks a few points either side, so one day's small move on a young save is no cliff
        public bool Whole;                          // the values are counts: the axis steps by whole numbers (1, 2, 5 of a power of ten), never 0.5
        public int Focus = -1;                      // the point read in its card, or -1
        public bool EvenAxis;                       // the point names stand at even steps counted back from the last point, which is always named (a series of days)
        public bool Still;                          // a chart that is read by its axes alone: its points are no targets (a figure beside a page's own, not a thing to explore)
        public UiPlotBand[] PlotBands;               // washes behind a run of the points, or null
        public int[] Marks;                          // point indices marked (a rate rise), or null
        public string EmptyTitle;
        public string EmptyBody;
    }

    /// <summary>A wash behind a run of a line chart, from and to as shares of its points (0 to 1), with a short name.</summary>
    public struct UiPlotBand
    {
        public float From;
        public float To;
        public UiRole Tint;
        public string Label;
    }

    /// <summary>
    /// Charts, a block of an app window's page on its darker band: a line over time (one to four series in the chart hues,
    /// the last value of each named at the line's end), bars by category (each in its category's colour where it has one,
    /// its value over it), and a market's candles (open, high, low and close, green up and red down) over their volume.
    /// The value axis steps by round figures (1, 2, 2.5 or 5 of a power of ten), its rules are hairlines, and the point
    /// axis names as many points as fit without crowding. Every point is a target that reads it in a small slate card
    /// beside it, by focus or by the mouse resting on it (IUiPointing). Flat strokes and fills, no gradient. A line is
    /// drawn as segments of the kit's spark, each in a box from one point to the next, so it needs no mesh of its own
    /// and allocates only when laid out. It stands in for the game's AwesomeCharts and the mod's own chart meshes.
    /// Whatever the values, nothing is drawn outside the plot: a bar is a length from nothing, so a bar chart's axis
    /// always holds nothing (losses hang down from it), every height is held between the plot's top and foot, a value
    /// that is no number (a division by nothing) is left out, and the axis takes a wider step until its ticks read apart.
    /// Commands: <see cref="First"/> plus <see cref="Point"/> (the point's index). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiChart
    {
        public const int First = 120;
        public const int Point = 0;
        public const int MostTargets = 31;      // points a chart makes targets of; a longer one reads by its axis alone
        public const int MostSteps = 8;         // the value axis's steps at the most (nine rules): more would crowd a short plot
        private const int Intervals = 4;        // the value axis's steps, about
        private const int MostWider = 32;       // times an axis takes the next round step to tell its ticks apart: eight powers of ten
        private const float Most = 1e30f;       // a value past this either way is no figure to draw, so no sum of two can overflow
        private const float NameSlot = UiTokens.S6;   // px: a narrower slot holds no category's name, so the axis names every so many
        private const float LeastSlot = 0.001f; // px: a slot is divided by, and a series longer than its plot has px gives each point under one
        private const float SwatchSize = 8f;    // px: a legend's or a last value's dot
        private const float BarShare = 0.56f;   // of a point's slot, a bar's width
        private const float CandleShare = 0.6f; // of a slot, a candle's body
        private const float VolumeShare = 0.2f; // of the plot, the candles' volume under them
        private const float Wick = 2f;          // px: a candle's high to low

        // The axis's words as Scale last read them. Scale reads them to tell the ticks apart, so the layout writes each once.
        private static readonly string[] TickWords = new string[MostSteps + 1];

        /// <summary>The height a chart's head takes: its title and its legend on one line.</summary>
        public static float HeadHeight => UiTokens.PairRow + UiTokens.S2;

        /// <summary>
        /// Lays a chart out on its band in a box. Returns the index of the band, so a page can lay it beside another.
        /// </summary>
        public static int Lay(UiScene s, UiAt at, UiChartModel m, float x, float y, float w, float h, int command = First)
        {
            int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(band);
            float ix = x + UiTokens.S4, iy = y + UiTokens.S3, iw = w - UiTokens.S4 * 2f, ih = h - UiTokens.S3 * 2f;
            Head(s, on, m, ix, iy, iw);
            float top = iy + HeadHeight + (LegendBelow(m, iw) ? UiTokens.PairRow : 0f);
            int n = m.Points != null ? m.Points.Length : 0;
            if (n == 0 || !Range(m, out float low, out float high))
            {
                UiDraw.Empty(s, on, ix, top, iw, iy + ih - top, m.EmptyTitle, m.EmptyBody);
                return band;
            }
            Scale(m, low, high, out float lo, out float hi, out float step);
            int steps = Steps(lo, hi, step);
            float axisW = AxisWidth(steps);
            float rightW = m.Kind == UiChartKind.Line ? LastWidth(m) : 0f;
            float labelH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small) * (m.Kind == UiChartKind.Bars ? 2f : 1f)) + UiTokens.S1;   // a category's name may take two lines
            float px = ix + axisW, pw = UiDraw.SnapDown(iw - axisW - rightW);
            float plotTop = top + UiTokens.S2, plotFoot = iy + ih - labelH;
            float volumeH = m.Kind == UiChartKind.Candles && m.Volume != null ? UiDraw.SnapDown((plotFoot - plotTop) * VolumeShare) : 0f;
            float ph = plotFoot - plotTop - (volumeH > 0f ? volumeH + UiTokens.S3 : 0f);
            float drawH = ph;
            Axis(s, on, ix, axisW, plotTop, drawH, px, pw, lo, hi, step, steps);
            float slot = pw / n;
            int plot = UiDraw.Box(s, on, px, plotTop, pw, ph, UiRole.None, UiRole.None, 0f);
            UiAt inPlot = on.In(plot);
            Phases(s, inPlot, m, px, plotTop, pw, drawH);
            RiseMarks(s, inPlot, m, px, slot, plotTop, drawH, n, lo, hi);
            for (int i = 0; i < n; i++)
            {
                float sx = px + slot * i;
                int column = UiDraw.Box(s, inPlot, sx, plotTop, slot, drawH, UiRole.None, UiRole.None, UiTokens.RadiusSmall, UiFlags.Share);
                UiAt inColumn = inPlot.In(column);
                if (m.Kind == UiChartKind.Bars)
                {
                    Bar(s, inColumn, m, i, sx, slot, plotTop, ph, lo, hi);
                }
                else if (m.Kind == UiChartKind.Candles)
                {
                    Candle(s, inColumn, m, i, sx, slot, plotTop, ph, lo, hi);
                }
                if (n <= MostTargets && !m.Still)
                {
                    UiDraw.Node(s, on, sx, plotTop, slot, drawH, command + Point, i, column);
                }
            }
            if (m.Kind == UiChartKind.Line)
            {
                Lines(s, inPlot, m, px, slot, plotTop, drawH, lo, hi);
                Lasts(s, on, m, px + pw + UiTokens.S2, rightW - UiTokens.S2, plotTop, drawH, lo, hi);
            }
            if (volumeH > 0f)
            {
                Volume(s, on, m, ix, axisW, px, slot, plotTop + ph + UiTokens.S3, volumeH);
            }
            PointLabels(s, on, m, px, slot, plotFoot + UiTokens.S1, labelH);
            if (m.Focus >= 0 && m.Focus < n && (m.Kind != UiChartKind.Candles || (m.Focus < m.Close.Length && Real(m.Close[m.Focus]))))
            {
                Reading(s, on, m, m.Focus, px, pw, slot, plotTop, ph, lo, hi, x, y, w, h);
            }
            return band;
        }

        /// <summary>
        /// Whether a value can be drawn: a number, and one in reach. A division by nothing leaves NaN or an infinity, and
        /// both fail this one comparison. Such a value is left out of the axis and of the plot.
        /// </summary>
        public static bool Real(float v) => Math.Abs(v) <= Most;

        // A candle's four prices. A price that is missing or no number is read from the close, and the high and low hold
        // the open and close between them. False when the close itself is no number: that day has no candle.
        private static bool CandleOf(UiChartModel m, int i, out float open, out float high, out float low, out float close)
        {
            close = m.Close[i];
            open = m.Open != null && i < m.Open.Length && Real(m.Open[i]) ? m.Open[i] : close;
            high = Math.Max(open, close);
            low = Math.Min(open, close);
            if (m.High != null && i < m.High.Length && Real(m.High[i]))
            {
                high = Math.Max(high, m.High[i]);
            }
            if (m.Low != null && i < m.Low.Length && Real(m.Low[i]))
            {
                low = Math.Min(low, m.Low[i]);
            }
            return Real(close);
        }

        // The title at the left, and for lines a legend of each series' dot and name at the right. A legend the title has no room
        // beside takes the line under it, and there each name gets no more than its share of the width.
        private static void Head(UiScene s, UiAt at, UiChartModel m, float x, float y, float w)
        {
            float h = UiTokens.PairRow;
            float right = x + w;
            if (LegendBelow(m, w))
            {
                float most = LegendShare(m, w);
                float lx = x, ly = y + h;
                for (int i = 0; i < m.Series.Length; i++)
                {
                    UiDraw.Circle(s, at, lx, ly + (h - SwatchSize) / 2f, SwatchSize, SwatchSize, Tint(m, i), UiFlags.Middle | UiFlags.Share);
                    lx += SwatchSize + UiTokens.S2;
                    float lw = Math.Min(most, UiTextRules.Width(m.Series[i].Label, UiStyle.Small) + UiTokens.S1);
                    UiDraw.TextMiddle(s, at, lx, ly, h, lw, UiStyle.Small, UiRole.Text2, m.Series[i].Label, UiFlags.Label | UiFlags.Share | UiFlags.Truncate);
                    lx += lw + UiTokens.S4;
                }
            }
            else if (LegendWidth(m) > 0f)
            {
                for (int i = m.Series.Length - 1; i >= 0; i--)
                {
                    float lw = UiTextRules.Width(m.Series[i].Label, UiStyle.Small) + UiTokens.S1;
                    right -= lw;
                    UiDraw.TextMiddle(s, at, right, y, h, lw, UiStyle.Small, UiRole.Text2, m.Series[i].Label, UiFlags.Label | UiFlags.Share);
                    right -= SwatchSize + UiTokens.S2;
                    UiDraw.Circle(s, at, right, y + (h - SwatchSize) / 2f, SwatchSize, SwatchSize, Tint(m, i), UiFlags.Middle | UiFlags.Share);
                    right -= UiTokens.S4;
                }
            }
            UiDraw.TextMiddle(s, at, x, y, h, Math.Max(UiTokens.S8, right - x - UiTokens.S2), UiStyle.SectionHeader, UiRole.Text, m.Title, UiFlags.Label | UiFlags.Truncate);
        }

        // What a legend takes beside its names: each series' dot, the gap to its name and the gap to the next.
        private const float LegendPiece = SwatchSize + UiTokens.S2 + UiTokens.S4;

        /// <summary>The width a chart's legend takes with every name whole. 0 for a chart that has none.</summary>
        public static float LegendWidth(UiChartModel m)
        {
            if (m.Kind != UiChartKind.Line || m.Series == null || m.Series.Length < 2)
            {
                return 0f;
            }
            float sum = 0f;
            for (int i = 0; i < m.Series.Length; i++)
            {
                sum += LegendPiece + UiTextRules.Width(m.Series[i].Label, UiStyle.Small) + UiTokens.S1;
            }
            return sum;
        }

        /// <summary>Whether the legend takes its own line: the title and the legend do not fit one line of this width.</summary>
        public static bool LegendBelow(UiChartModel m, float w)
        {
            float legend = LegendWidth(m);
            return legend > 0f && legend + UiTokens.S2 + UiTextRules.Width(m.Title, UiStyle.SectionHeader) > w;
        }

        /// <summary>
        /// The most a name takes in a legend on its own line of this width. Short names keep their width and the long ones
        /// share what is left, so the legend never passes the chart's edge.
        /// </summary>
        public static float LegendShare(UiChartModel m, float w)
        {
            int n = m.Series.Length;
            float room = Math.Max(0f, w - LegendPiece * n + UiTokens.S4);
            float most = room / n;
            for (int pass = 0; pass < n; pass++)
            {
                float used = 0f;
                int over = 0;
                for (int i = 0; i < n; i++)
                {
                    float lw = UiTextRules.Width(m.Series[i].Label, UiStyle.Small) + UiTokens.S1;
                    used += Math.Min(lw, most);
                    over += lw > most ? 1 : 0;
                }
                if (over == 0 || room - used < 0.5f)
                {
                    break;
                }
                most += (room - used) / over;
            }
            return most;
        }

        /// <summary>A series' colour: its own, or the chart's hue in its turn.</summary>
        public static UiRole Tint(UiChartModel m, int series) => m.Series[series].Tint != UiRole.None ? m.Series[series].Tint : UiTokens.Chart(series);

        // The span the values that can be drawn cover. False when there is none: the chart then shows its empty state.
        private static bool Range(UiChartModel m, out float lo, out float hi)
        {
            lo = float.MaxValue;
            hi = float.MinValue;
            if (m.Kind == UiChartKind.Candles)
            {
                for (int i = 0; m.Close != null && i < m.Close.Length; i++)
                {
                    if (CandleOf(m, i, out float _, out float high, out float low, out float _))
                    {
                        lo = Math.Min(lo, low);
                        hi = Math.Max(hi, high);
                    }
                }
                return lo <= hi;
            }
            for (int k = 0; m.Series != null && k < m.Series.Length; k++)
            {
                float[] v = m.Series[k].Values;
                for (int i = 0; v != null && i < v.Length; i++)
                {
                    if (Real(v[i]))
                    {
                        lo = Math.Min(lo, v[i]);
                        hi = Math.Max(hi, v[i]);
                    }
                }
            }
            return lo <= hi;
        }

        /// <summary>The value axis a chart is drawn on: its ends, each a whole number of its round step, and the step.</summary>
        public static void Scale(UiChartModel m, out float lo, out float hi, out float step)
        {
            if (!Range(m, out float low, out float high))
            {
                low = high = 0f;   // nothing to draw: a plain axis from nothing, as the empty state needs none
            }
            Scale(m, low, high, out lo, out hi, out step);
        }

        // The axis over a span of values. A bar is a length from nothing, so a bar chart's axis holds nothing whatever its
        // values are: an axis fitted round losses of $3,741 a day drew their bars from far above the window (12 Nov 2026).
        private static void Scale(UiChartModel m, float low, float high, out float lo, out float hi, out float step)
        {
            bool bars = m.Kind == UiChartKind.Bars;
            if (bars)
            {
                low = Math.Min(0f, low);
                high = Math.Max(0f, high);
            }
            else if (m.LeastSpan > 0f && high - low < m.LeastSpan)
            {
                float mid = (low + high) / 2f;
                low = mid - m.LeastSpan / 2f;
                high = mid + m.LeastSpan / 2f;
            }
            else if (high - low < 1e-6f)
            {
                // One value all through (a count that has not moved): the line stands in the middle of a real range, a tenth of the
                // value (at least one) either side, never on the foot of an axis from one whole number to the next.
                float half = Math.Max(1f, Math.Abs(low) * 0.1f);
                bool positive = low >= 0f;
                low -= half;
                high += half;
                if (positive && low < 0f)
                {
                    high -= low;   // a count at nothing keeps its foot at nothing
                    low = 0f;
                }
            }
            step = NiceStep(high - low, Intervals);
            if (m.Whole)
            {
                step = WholeStep(step);
            }
            Ends(low, high, step, bars, out lo, out hi);
            // Ticks that read alike say nothing ("$1.2M" five times, or a float's last figures at a large value): the next round
            // step, until each tick has its own words.
            bool apart = Readable(m, lo, hi, step);
            for (int wider = 0; wider < MostWider && !apart; wider++)
            {
                step = Wider(step, m.Whole);
                Ends(low, high, step, bars, out lo, out hi);
                apart = Readable(m, lo, hi, step);
            }
        }

        // The axis's ends for a step: each a whole number of it, holding the span between them.
        private static void Ends(float low, float high, float step, bool bars, out float lo, out float hi)
        {
            lo = (float)Math.Floor(low / step + 1e-4) * step;
            hi = (float)Math.Ceiling(high / step - 1e-4) * step;
            if (hi - lo < step * 0.5f)
            {
                hi = lo + step;
            }
            if (bars)
            {
                // The longest bar each way keeps room past its end for its value: a bar that ends on the axis's edge would lose its label.
                if (high > 0f && high > hi - step * 0.2f)
                {
                    hi += step;
                }
                if (low < 0f && low < lo + step * 0.2f)
                {
                    lo -= step;
                }
            }
        }

        // The steps an axis takes from its foot to its top.
        private static int Steps(float lo, float hi, float step) => (int)Math.Round((hi - lo) / step);

        // Whether an axis can be read: no more steps than a plot holds, and no two neighbouring ticks with the same words.
        // The words stay in TickWords for the layout.
        private static bool Readable(UiChartModel m, float lo, float hi, float step)
        {
            int steps = Steps(lo, hi, step);
            bool apart = steps >= 1 && steps <= MostSteps;
            for (int k = 0; k <= steps && k <= MostSteps; k++)
            {
                TickWords[k] = Tick(m, lo + k * step);
                apart &= k == 0 || !string.Equals(TickWords[k], TickWords[k - 1], StringComparison.Ordinal);
            }
            return apart;
        }

        // The next round step up: 1 to 2 to 2.5 to 5 to 10 of a power of ten, a count's never by a half.
        private static float Wider(float step, bool whole)
        {
            double power = Math.Pow(10, Math.Floor(Math.Log10(step) + 1e-6));
            double unit = step / power;
            return (float)((unit < 1.5 ? 2.0 : unit < 2.25 ? (whole ? 5.0 : 2.5) : unit < 3.75 ? 5.0 : 10.0) * power);
        }

        /// <summary>
        /// A round step for an axis over a span in about so many intervals: 1, 2, 2.5 or 5 of a power of ten, the nearest
        /// over the span's share, so every tick is a figure a person would write.
        /// </summary>
        public static float NiceStep(float span, int intervals)
        {
            if (span <= 0f || float.IsNaN(span) || float.IsInfinity(span))
            {
                return 1f;
            }
            double raw = span / Math.Max(1, intervals);
            double power = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double unit = raw / power;
            double nice = unit <= 1.0 ? 1.0 : unit <= 2.0 ? 2.0 : unit <= 2.5 ? 2.5 : unit <= 5.0 ? 5.0 : 10.0;
            return (float)(nice * power);
        }

        /// <summary>A round step for counts: 1, 2, 5 of a power of ten, never a half (so no two ticks of a count read alike).</summary>
        public static float WholeStep(float step)
        {
            if (step <= 1f)
            {
                return 1f;
            }
            double power = Math.Pow(10, Math.Floor(Math.Log10(step) + 1e-6));
            double unit = step / power;
            return (float)((unit <= 1.0 + 1e-6 ? 1.0 : unit <= 2.0 + 1e-6 ? 2.0 : 5.0) * power);
        }

        /// <summary>Whether a value is a whole number of a round step (the axis's own test).</summary>
        public static bool OnStep(float value, float step) => step > 0f && Math.Abs(value / step - Math.Round(value / step)) < 1e-3;

        // The value axis's width: its widest tick's words, as Scale read them.
        private static float AxisWidth(int steps)
        {
            float most = 0f;
            for (int k = 0; k <= steps && k <= MostSteps; k++)
            {
                most = Math.Max(most, UiTextRules.Width(TickWords[k], UiStyle.Small));
            }
            return UiDraw.Snap(most + UiTokens.S1 + UiTokens.S3);
        }

        private static string Write(UiChartModel m, float v) => m.Format != null ? m.Format(v) : v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>A tick's words: the axis's short form where the screen gives one.</summary>
        public static string Tick(UiChartModel m, float v) => m.AxisFormat != null ? m.AxisFormat(v) : Write(m, v);

        // The width the last values take at a line chart's right: the widest value with its dot.
        private static float LastWidth(UiChartModel m)
        {
            float most = 0f;
            for (int k = 0; k < m.Series.Length; k++)
            {
                float[] v = m.Series[k].Values;
                if (v != null && v.Length > 0 && Real(v[v.Length - 1]))
                {
                    most = Math.Max(most, UiTextRules.Width(Write(m, v[v.Length - 1]), UiStyle.SmallStrong));
                }
            }
            return most > 0f ? UiDraw.Snap(UiTokens.S2 + SwatchSize + UiTokens.S1 + most + UiTokens.S1) : 0f;
        }

        // The value axis: each round tick's words at the left, right aligned against the plot, and its hairline across.
        private static void Axis(UiScene s, UiAt at, float x, float axisW, float plotTop, float ph, float px, float pw, float lo, float hi, float step, int steps)
        {
            float lh = UiTextRules.LineHeight(UiStyle.Small);
            for (int k = 0; k <= steps && k <= MostSteps; k++)
            {
                float ty = Y(lo + k * step, lo, hi, plotTop, ph);
                UiDraw.Box(s, at, px, ty, pw, UiTokens.Hairline, k == 0 ? UiRole.Line : UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
                UiDraw.Text(s, at, x, ty - lh / 2f, axisW - UiTokens.S3, UiStyle.Small, UiRole.Muted, TickWords[k], UiFlags.Right | UiFlags.Middle);
            }
        }

        // A value's height in the plot, held between its foot and its top: a value past the axis is drawn to the plot's edge, never past it.
        private static float Y(float v, float lo, float hi, float top, float h)
        {
            float share = (v - lo) / Math.Max(1e-6f, hi - lo);
            return top + h - (share > 0f ? share < 1f ? share : 1f : 0f) * h;
        }

        // One flat, quiet tint for each phase behind the lines (UiTokens.PhaseTint), and its name small at the top of that tint.
        private static void Phases(UiScene s, UiAt at, UiChartModel m, float px, float top, float pw, float ph)
        {
            if (m.Kind != UiChartKind.Line || m.PlotBands == null)
            {
                return;
            }
            float labelH = UiTextRules.LineHeight(UiStyle.Small);
            for (int i = 0; i < m.PlotBands.Length; i++)
            {
                UiPlotBand band = m.PlotBands[i];
                float from = band.From < 0f ? 0f : band.From > 1f ? 1f : band.From;
                float to = band.To < from ? from : band.To > 1f ? 1f : band.To;
                float x0 = px + pw * from;
                float bw = pw * (to - from);
                if (bw < UiTokens.S2)
                {
                    continue;
                }
                var wash = new UiItem
                {
                    Kind = UiKind.Box, Flags = UiFlags.Decor, Fill = band.Tint, X = x0, Y = top, W = bw, H = ph, Alpha = UiTokens.PhaseTint,
                    Clip = at.Clip, Node = -1, Layer = at.Layer, Order = -1, Parent = at.Parent
                };
                s.Add(ref wash);
                if (string.IsNullOrEmpty(band.Label) || bw < UiTokens.S16)
                {
                    continue;
                }
                UiDraw.Text(s, at, x0 + UiTokens.S2, top + UiTokens.S1, bw - UiTokens.S2 * 2f, UiStyle.Small, UiRole.Muted, band.Label,
                    UiFlags.Label | UiFlags.Truncate | UiFlags.Decor);
            }
        }

        // A rate rise: a hairline through the plot and a dot on the second line where there is one (the bank rate).
        private static void RiseMarks(UiScene s, UiAt at, UiChartModel m, float px, float slot, float top, float ph, int n, float lo, float hi)
        {
            if (m.Kind != UiChartKind.Line || m.Marks == null)
            {
                return;
            }
            float[] rate = m.Series != null && m.Series.Length > 1 ? m.Series[1].Values : null;
            for (int i = 0; i < m.Marks.Length; i++)
            {
                int atPoint = m.Marks[i];
                if (atPoint < 0 || atPoint >= n)
                {
                    continue;
                }
                float x = px + slot * (atPoint + 0.5f);
                var line = new UiItem
                {
                    Kind = UiKind.Box, Flags = UiFlags.Decor, Fill = UiRole.Warn, X = x, Y = top, W = UiTokens.Hairline, H = ph, Alpha = 0.9f,
                    Clip = at.Clip, Node = -1, Layer = at.Layer, Order = -1, Parent = at.Parent
                };
                s.Add(ref line);
                float y = rate != null && atPoint < rate.Length && Real(rate[atPoint]) ? Y(rate[atPoint], lo, hi, top, ph) : top + UiTokens.S2;
                UiDraw.Circle(s, at, x - UiTokens.S1, y - UiTokens.S1, UiTokens.S2, UiTokens.S2, UiRole.Warn, UiFlags.Decor | UiFlags.Middle);
            }
        }

        // Each series as segments from point to point, each a spark of two values in the box between them, widened by the
        // stroke's half each way so the spark's inset puts its ends on the points. A point that is no number has no
        // segment to it or from it: the line breaks there rather than run to a height that means nothing.
        private static void Lines(UiScene s, UiAt at, UiChartModel m, float px, float slot, float top, float h, float lo, float hi)
        {
            float t = UiTokens.Hairline;
            for (int k = 0; k < m.Series.Length; k++)
            {
                float[] v = m.Series[k].Values;
                UiRole tint = Tint(m, k);
                for (int i = 1; v != null && i < v.Length; i++)
                {
                    if (!Real(v[i - 1]) || !Real(v[i]))
                    {
                        continue;
                    }
                    float x0 = px + slot * (i - 0.5f), x1 = px + slot * (i + 0.5f);
                    float y0 = Y(v[i - 1], lo, hi, top, h), y1 = Y(v[i], lo, hi, top, h);
                    var seg = new UiItem
                    {
                        Kind = UiKind.Spark, Fill = tint, X = x0 - t, Y = Math.Min(y0, y1) - t, W = x1 - x0 + t * 2f, H = Math.Abs(y1 - y0) + t * 2f,
                        Points = new[] { v[i - 1], v[i] }, Flags = UiFlags.Decor, Clip = at.Clip, Node = -1, Layer = at.Layer, Order = -1, Parent = at.Parent, Alpha = 1f
                    };
                    s.Add(ref seg);
                }
            }
        }

        // Each series' last value at the line's end, its dot and its figure, pushed apart where two end close together.
        private static void Lasts(UiScene s, UiAt at, UiChartModel m, float x, float w, float top, float h, float lo, float hi)
        {
            int n = m.Series.Length;
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SmallStrong));
            var ys = new float[n];
            var order = new int[n];
            for (int k = 0; k < n; k++)
            {
                float[] v = m.Series[k].Values;
                ys[k] = v != null && v.Length > 0 && Real(v[v.Length - 1]) ? Y(v[v.Length - 1], lo, hi, top, h) - lh / 2f : top;
                order[k] = k;
            }
            Array.Sort((float[])ys.Clone(), order);
            for (int pass = 0; pass < 2; pass++)
            {
                for (int j = 1; j < n; j++)
                {
                    int a = order[j - 1], b = order[j];
                    ys[b] = Math.Max(ys[b], ys[a] + lh);
                }
                for (int j = n - 1; j >= 0; j--)
                {
                    int b = order[j];
                    ys[b] = Math.Min(ys[b], top + h - lh - (n - 1 - j) * lh);
                    ys[b] = Math.Max(ys[b], top + j * lh);
                }
            }
            for (int k = 0; k < n; k++)
            {
                float[] v = m.Series[k].Values;
                if (v == null || v.Length == 0 || !Real(v[v.Length - 1]))
                {
                    continue;
                }
                UiDraw.Circle(s, at, x, ys[k] + (lh - SwatchSize) / 2f, SwatchSize, SwatchSize, Tint(m, k), UiFlags.Middle | UiFlags.Share);
                string last = Write(m, v[v.Length - 1]);
                UiDraw.Text(s, at, x + SwatchSize + UiTokens.S1, ys[k], Math.Min(w - SwatchSize - UiTokens.S1, UiTextRules.Width(last, UiStyle.SmallStrong) + UiTokens.S1), UiStyle.SmallStrong,
                    UiRole.Text, last, UiFlags.Middle | UiFlags.Share);
            }
        }

        // A category's bar from nothing to its value in its colour: up from the line of nothing for a gain, down from it for
        // a loss, never past the plot. Its value stands past its end (over a gain, under a loss) where the slot holds the words.
        private static void Bar(UiScene s, UiAt at, UiChartModel m, int i, float sx, float slot, float top, float h, float lo, float hi)
        {
            float[] v = m.Series.Length > 0 ? m.Series[0].Values : null;
            if (v == null || i >= v.Length || !Real(v[i]))
            {
                return;
            }
            float bw = Math.Min(slot, Math.Max(UiTokens.S2, slot * BarShare)), bx = sx + (slot - bw) / 2f;
            float zero = Y(0f, lo, hi, top, h), end = Y(v[i], lo, hi, top, h);
            bool loss = v[i] < 0f;
            float bh = Math.Min(h, Math.Max(UiTokens.Hairline * 2f, Math.Abs(zero - end)));
            float by = Math.Max(top, Math.Min(top + h - bh, loss ? zero : zero - bh));   // a bar too short to see still stands on its line, inside the plot
            UiRole tint = m.BarTints != null && i < m.BarTints.Length && m.BarTints[i] != UiRole.None ? m.BarTints[i] : Tint(m, 0);
            UiDraw.Box(s, at, bx, by, bw, bh, tint, UiRole.None, UiTokens.RadiusSmall, UiFlags.Share | UiFlags.Middle);
            string words = Write(m, v[i]);
            float lw = UiTextRules.Width(words, UiStyle.SmallStrong) + UiTokens.S1, lh = UiTextRules.LineHeight(UiStyle.SmallStrong);
            float ly = loss ? by + bh + UiTokens.S1 : by - lh - UiTokens.S1;
            if (lw <= slot - UiTokens.S1 && ly >= top && ly + lh <= top + h)
            {
                UiDraw.Text(s, at, sx + (slot - lw) / 2f, ly, lw, UiStyle.SmallStrong, UiRole.Text, words, UiFlags.Centre | UiFlags.Middle);
            }
        }

        // A day's candle: its wick from low to high, its body from open to close, green when it closed up and red when down.
        private static void Candle(UiScene s, UiAt at, UiChartModel m, int i, float sx, float slot, float top, float h, float lo, float hi)
        {
            if (i >= m.Close.Length || !CandleOf(m, i, out float open, out float high, out float low, out float close))
            {
                return;
            }
            UiRole tone = close >= open ? UiRole.Ok : UiRole.Danger;
            float cx = sx + slot / 2f;
            float wick = Math.Min(Wick, slot);
            float yh = Y(high, lo, hi, top, h), yl = Y(low, lo, hi, top, h);
            float wh = Math.Max(Wick, yl - yh);
            UiDraw.Box(s, at, cx - wick / 2f, Math.Min(yh, top + h - wh), wick, wh, tone, UiRole.None, 0f, UiFlags.Share | UiFlags.Middle | UiFlags.Decor);
            float bw = Math.Min(slot, Math.Max(UiTokens.S1, slot * CandleShare));
            float yo = Y(open, lo, hi, top, h), yc = Y(close, lo, hi, top, h);
            float bh = Math.Max(Wick, Math.Abs(yo - yc));
            UiDraw.Box(s, at, cx - bw / 2f, Math.Min(Math.Min(yo, yc), top + h - bh), bw, bh, tone, UiRole.None, UiTokens.RadiusSmall / 2f, UiFlags.Share | UiFlags.Middle);
        }

        // The volume under the candles: a bar a day in the quiet slate, its label at the strip's left.
        private static void Volume(UiScene s, UiAt at, UiChartModel m, float x, float axisW, float px, float slot, float y, float h)
        {
            float most = 0f;
            for (int i = 0; i < m.Volume.Length; i++)
            {
                most = Real(m.Volume[i]) ? Math.Max(most, m.Volume[i]) : most;
            }
            UiDraw.Text(s, at, x, y + (h - UiTextRules.LineHeight(UiStyle.Small)) / 2f, axisW - UiTokens.S3, UiStyle.Small, UiRole.Muted, m.VolumeLabel,
                UiFlags.Right | UiFlags.Middle | UiFlags.Label);
            UiDraw.Box(s, at, px, y + h, slot * m.Volume.Length, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            for (int i = 0; i < m.Volume.Length && most > 0f; i++)
            {
                if (!Real(m.Volume[i]))
                {
                    continue;
                }
                float bw = Math.Min(slot, Math.Max(UiTokens.S1, slot * CandleShare)), bh = Math.Max(UiTokens.Hairline, Math.Max(0f, m.Volume[i]) / most * h);
                UiDraw.Box(s, at, px + slot * i + (slot - bw) / 2f, y + h - bh, bw, bh, UiRole.Chart8, UiRole.None, 0f, UiFlags.Share | UiFlags.Middle);
            }
        }

        // The point axis: as many names as fit apart, the first and every so many after it, each centred under its point. A
        // bar chart names every category, on two lines where its name needs them, as a category left unnamed says nothing.
        // One with more bars than its width can name (slots under NameSlot) names every so many, as a line does.
        private static void PointLabels(UiScene s, UiAt at, UiChartModel m, float px, float slot, float y, float h)
        {
            int n = m.Points.Length;
            if (m.Kind == UiChartKind.Bars && slot >= NameSlot)
            {
                for (int i = 0; i < n; i++)
                {
                    float bw = slot - UiTokens.S1;
                    if (UiTextRules.Width(m.Points[i] ?? string.Empty, UiStyle.Small) <= bw)
                    {
                        // A name that fits one line is a single centred line (a wrapped one sits a little high in a script with taller lines).
                        UiDraw.TextMiddle(s, at, px + slot * i + UiTokens.S1 / 2f, y,
                            UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S1, bw, UiStyle.Small, UiRole.Muted, m.Points[i], UiFlags.Centre | UiFlags.Label);
                        continue;
                    }
                    UiDraw.Text(s, at, px + slot * i + UiTokens.S1 / 2f, y, bw, UiStyle.Small, UiRole.Muted, m.Points[i], UiFlags.Centre | UiFlags.Wrap | UiFlags.Middle | UiFlags.Label, 2);
                }
                return;
            }
            float most = 0f;
            bool stepped = false;   // the points already say which of them are named: the rest are empty
            for (int i = 0; i < n; i++)
            {
                most = Math.Max(most, UiTextRules.Width(m.Points[i], UiStyle.Small));
                stepped |= string.IsNullOrEmpty(m.Points[i]);
            }
            int every = stepped ? 1 : Math.Max(1, (int)Math.Ceiling((most + UiTokens.S3) / Math.Max(LeastSlot, slot)));
            float lw = stepped ? most + UiTokens.S1 : Math.Min(most + UiTokens.S1, slot * every - UiTokens.S1);
            if (m.EvenAxis && !stepped)
            {
                // From the last point back, an even number of points apart; a name that would start left of the plot is left out, none is moved off its point.
                for (int i = n - 1; i >= 0; i -= every)
                {
                    float ex = px + slot * (i + 0.5f) - lw / 2f;
                    if (ex >= px - 0.5f)
                    {
                        UiDraw.Text(s, at, ex, y, lw, UiStyle.Small, UiRole.Muted, m.Points[i], UiFlags.Centre | UiFlags.Truncate | UiFlags.Middle);
                    }
                }
                return;
            }
            float kept = float.MinValue;   // the right edge of the last label drawn
            for (int i = 0; i < n; i += every)
            {
                if (stepped && string.IsNullOrEmpty(m.Points[i]))
                {
                    continue;
                }
                float lx = Math.Max(px, Math.Min(px + slot * n - lw, px + slot * (i + 0.5f) - lw / 2f));   // the ends stay over the plot, clear of the value axis
                if (lx < kept + UiTokens.S2)
                {
                    continue;   // held in at an end it would crowd its neighbour: it goes, the neighbour keeps its place under its point
                }
                UiDraw.Text(s, at, lx, y, lw, UiStyle.Small, UiRole.Muted, m.Points[i], UiFlags.Centre | UiFlags.Truncate | UiFlags.Middle);
                kept = lx + lw;
            }
        }

        /// <summary>The points whose names the axis shows: the first and every so many after it, as <see cref="Lay"/> spaces them.</summary>
        public static int LabelEvery(UiChartModel m, float slot)
        {
            float most = 0f;
            for (int i = 0; i < m.Points.Length; i++)
            {
                if (string.IsNullOrEmpty(m.Points[i]))
                {
                    return 1;   // stepped by the model: every named point is shown
                }
                most = Math.Max(most, UiTextRules.Width(m.Points[i], UiStyle.Small));
            }
            return Math.Max(1, (int)Math.Ceiling((most + UiTokens.S3) / Math.Max(LeastSlot, slot)));
        }

        // The reading of a point: a hairline down through it, a dot on each line, and a slate card beside it on the layer
        // over the page with the point's name and each value, kept inside the chart's band.
        private static void Reading(UiScene s, UiAt at, UiChartModel m, int i, float px, float pw, float slot, float top, float h, float lo, float hi,
            float bx, float by, float bw, float bh)
        {
            float cx = px + slot * (i + 0.5f);
            if (m.Kind == UiChartKind.Line)
            {
                UiDraw.Box(s, at, cx - UiTokens.Hairline / 2f, top, UiTokens.Hairline, h, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                for (int k = 0; k < m.Series.Length; k++)
                {
                    float[] v = m.Series[k].Values;
                    if (v != null && i < v.Length && Real(v[i]))
                    {
                        float vy = Y(v[i], lo, hi, top, h);
                        UiDraw.Circle(s, at, cx - SwatchSize / 2f, vy - SwatchSize / 2f, SwatchSize, SwatchSize, Tint(m, k), UiFlags.Decor);
                    }
                }
            }
            string read = m.Reads != null && i < m.Reads.Length ? m.Reads[i] : m.Points[i];
            int rows;
            string[] labels, values;
            UiRole[] swatches;
            ReadRows(m, i, out rows, out labels, out values, out swatches);
            float lw = 0f, vw = 0f;
            for (int r = 0; r < rows; r++)
            {
                lw = Math.Max(lw, UiTextRules.Width(labels[r], UiStyle.Small));
                vw = Math.Max(vw, UiTextRules.Width(values[r], UiStyle.SmallStrong));
            }
            float row = UiTokens.S5;
            float cw = UiDraw.Snap(Math.Max(UiTextRules.Width(read, UiStyle.SmallStrong), SwatchSize + UiTokens.S2 + lw + UiTokens.S4 + vw) + UiTokens.S1 + UiTokens.S3 * 2f);
            float ch = UiDraw.Snap(UiTokens.S2 * 2f + UiTextRules.LineHeight(UiStyle.SmallStrong) + UiTokens.S1 + rows * row);
            float x = UiDraw.Snap(px + slot * (i + 1) + UiTokens.S3);   // past the column's edge, so the ring round the point's column stays whole
            if (x + cw > bx + bw - UiTokens.S2)
            {
                x = UiDraw.SnapDown(px + slot * i - UiTokens.S3 - cw);
            }
            x = Math.Max(bx + UiTokens.S2, x);
            float y = UiDraw.Snap(top);
            y = Math.Min(y, UiDraw.SnapDown(by + bh - UiTokens.S2 - ch));
            var over = new UiAt { Parent = at.Parent, Clip = -1, Layer = 1, Order = -1, Region = at.Region };
            int card = UiDraw.Box(s, over, x, y, cw, ch, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel);
            UiAt inCard = over.In(card);
            float ty = y + UiTokens.S2;
            UiDraw.Text(s, inCard, x + UiTokens.S3, ty, cw - UiTokens.S3 * 2f, UiStyle.SmallStrong, UiRole.Text, read, UiFlags.Truncate);
            ty += UiTextRules.LineHeight(UiStyle.SmallStrong) + UiTokens.S1;
            for (int r = 0; r < rows; r++)
            {
                float ry = ty + r * row;
                if (swatches[r] != UiRole.None)
                {
                    UiDraw.Circle(s, inCard, x + UiTokens.S3, ry + (row - SwatchSize) / 2f, SwatchSize, SwatchSize, swatches[r], UiFlags.Middle);
                }
                float tx = x + UiTokens.S3 + SwatchSize + UiTokens.S2;
                UiDraw.TextMiddle(s, inCard, tx, ry, row, lw + UiTokens.S1, UiStyle.Small, UiRole.Text2, labels[r], UiFlags.Label);
                UiDraw.TextMiddle(s, inCard, x + cw - UiTokens.S3 - vw - UiTokens.S1, ry, row, vw + UiTokens.S1, UiStyle.SmallStrong, UiRole.Text, values[r], UiFlags.Right);
            }
        }

        // What a point's card says: each line's name and value (none for a value that is no number); a bar's value; a
        // candle's open, high, low, close and volume.
        private static void ReadRows(UiChartModel m, int i, out int rows, out string[] labels, out string[] values, out UiRole[] swatches)
        {
            if (m.Kind == UiChartKind.Candles)
            {
                bool vol = m.Volume != null && i < m.Volume.Length && Real(m.Volume[i]);
                rows = vol ? 5 : 4;
                CandleOf(m, i, out float open, out float high, out float low, out float close);
                UiRole tone = close >= open ? UiRole.Ok : UiRole.Danger;
                labels = new[] { m.OpenLabel, m.HighLabel, m.LowLabel, m.CloseLabel, m.VolumeLabel };
                values = new[]
                {
                    Write(m, open), Write(m, high), Write(m, low),
                    Write(m, close), vol ? (m.CountFormat != null ? m.CountFormat(m.Volume[i]) : m.Volume[i].ToString("0", System.Globalization.CultureInfo.InvariantCulture)) : null
                };
                swatches = new[] { UiRole.None, UiRole.None, UiRole.None, tone, UiRole.None };
                return;
            }
            rows = 0;
            labels = new string[m.Series.Length];
            values = new string[m.Series.Length];
            swatches = new UiRole[m.Series.Length];
            for (int k = 0; k < m.Series.Length; k++)
            {
                float[] v = m.Series[k].Values;
                if (v == null || i >= v.Length || !Real(v[i]))
                {
                    continue;
                }
                labels[rows] = m.Series[k].Label;
                values[rows] = Write(m, v[i]);
                swatches[rows] = m.Kind == UiChartKind.Bars && m.BarTints != null && i < m.BarTints.Length ? m.BarTints[i] : Tint(m, k);
                rows++;
            }
        }
    }
}
