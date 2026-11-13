using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ui;

// The charts whatever their values (Scripts/Ui/UiChart.cs, Stocks/UiChartDesk*.cs, and UiDraw's sparkline, donut, ring, meters and
// timing gauge), after a player's EconoView of 12 Nov 2026: a headquarters that sold nothing lost $3,737 to $3,741 on each of four
// days, the profit chart fitted its axis round those six dollars, and its bars, still drawn from nothing, ran from far above the
// plot through every card over it and out of the top of the window. Held here for every kind of chart and every hard case of values
// (all losses, all nearly equal at a large value, one, none, zeros only, a huge outlier, values crossing nothing, values that are no
// number, a very long series): every piece drawn in a plot lies inside the plot, a bar chart's axis holds nothing and each bar is a
// length from it, the axis's ticks are few and each reads apart from its neighbours, and the report's own figures on the EconoView
// business page draw inside their card at every frame. Then, in the whole run, every registered screen's sample once, held to the
// same rule (the flag alone leaves that out: building the screens' cases runs their own checks, a stopwatch among them).
internal static partial class Program
{
    private const float ChartSlack = UiTokens.Hairline + UiCheckRules.Tolerance;   // px: the stroke's half round a line's box, and a layout's rounding
    private const float ChartStub = UiTokens.Hairline * 2f;                        // px: the least a bar is drawn, so a value of nothing still shows
    private const int ChartLong = 5000;                                            // points in the long series: more than any plot has px

    private struct ChartCase
    {
        public string Name;
        public float[] Values;
        public int Points;   // the names along the axis: as many as the values, but for the case of names with no values
    }

    private struct ChartWords
    {
        public string Name;
        public Func<float, string> Format, Axis;
        public bool Whole;
    }

    private static readonly HashSet<string> ChartSaid = new HashSet<string>(StringComparer.Ordinal);

    private static void ChartSay(string line)
    {
        if (ChartSaid.Count < 40 && ChartSaid.Add(line))
        {
            Console.WriteLine("        " + line);   // each different finding once
        }
    }

    private static void ChartPlotChecks(bool screens)
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        var plots = new List<int>();
        ChartCase[] cases = ChartCases();
        ChartKindChecks(cases, plots);
        ChartReportChecks(cases, plots);
        ChartDeskChecks(cases);
        ChartSparkChecks(cases);
        ChartShareChecks(cases);
        ChartHeadChecks();
        if (screens)
        {
            ChartScreenChecks(plots);
        }
        Dictionary<string, string> en = LocaleFile("en");
        Check(WhatsNewHas(en, "EconoView's profit chart drew its bars over the whole window"), "charts: What's new tells of the fix");
        Console.WriteLine("  Charts: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- the head: a title and a legend
    // After a player's Economy page of 13 Nov 2026, on day 9 of a save: the goods chart's three long names ran out of the chart's left
    // edge over the table beside it, its axis read "Day -110", and the lead began with a small letter.
    private static void ChartHeadChecks()
    {
        var s = new UiScene();
        string[][] names =
        {
            new[] { "Classic cheap female clothing", "Cheap jewellery", "Apple" },
            new[] { "Klassische preiswerte Damenbekleidung", "Limitierte Sonderausgabe eines Buches", "Motivationsbuch fuer Fuehrungskraefte" },
            new[] { "Fuel", "Food", "Rent" }
        };
        float[] widths = { 300f, 424f, 560f, 1000f };
        string fault = null;
        int below = 0, beside = 0;
        foreach (string[] set in names)
        {
            foreach (float w in widths)
            {
                var m = new UiChartModel
                {
                    Kind = UiChartKind.Line, Title = "Index, day 1 = 100", Points = new[] { "Day 1", "", "", "", "", "Day 6", "", "", "" },
                    Series = new UiSeries[set.Length]
                };
                for (int k = 0; k < set.Length; k++)
                {
                    m.Series[k] = new UiSeries { Label = set[k], Values = new[] { 100f, 100f, 101f, 102f, 100f, 96f, 100f, 102f, 102f + k } };
                }
                s.Begin("chart head, " + set[0] + ", " + w, 1920f, 1080f, true);
                UiChart.Lay(s, UiAt.Screen, m, 400f, 300f, w, 320f);
                float left = 400f + UiTokens.S4 - ChartSlack, right = 400f + w - UiTokens.S4 + ChartSlack;
                bool under = UiChart.LegendBelow(m, w - UiTokens.S4 * 2f);
                below += under ? 1 : 0;
                beside += under ? 0 : 1;
                int title = -1;
                var legend = new List<int>();
                for (int i = 0; i < s.ItemCount; i++)
                {
                    if (s.Items[i].Text == m.Title && title < 0)
                    {
                        title = i;
                    }
                    else if (Array.IndexOf(set, s.Items[i].Text) >= 0 && s.Items[i].Y < 300f + UiTokens.S3 + UiChart.HeadHeight + UiTokens.PairRow && legend.Count < set.Length)
                    {
                        legend.Add(i);
                    }
                }
                string where = " (" + set[0] + ", " + w + " px)";
                if (title < 0 || legend.Count != set.Length)
                {
                    fault = fault ?? "the head lacks its title or a name" + where;
                    continue;
                }
                UiItem head = s.Items[title];
                for (int a = 0; a < legend.Count; a++)
                {
                    UiItem one = s.Items[legend[a]];
                    if (one.X < left || one.X + one.W > right)
                    {
                        fault = fault ?? "the name " + one.Text + " runs from " + one.X + " to " + (one.X + one.W) + ", outside " + left + " to " + right + where;
                    }
                    if (Math.Abs(one.Y - head.Y) < 1f && one.X < head.X + head.W - ChartSlack && head.X < one.X + one.W - ChartSlack)
                    {
                        fault = fault ?? "the name " + one.Text + " lies over the title" + where;
                    }
                    if (under != (one.Y > head.Y + 1f))
                    {
                        fault = fault ?? "the legend is on the wrong line" + where;
                    }
                    for (int b = a + 1; b < legend.Count; b++)
                    {
                        UiItem two = s.Items[legend[b]];
                        if (one.X < two.X + two.W - ChartSlack && two.X < one.X + one.W - ChartSlack)
                        {
                            fault = fault ?? "the names " + one.Text + " and " + two.Text + " lie over each other" + where;
                        }
                    }
                }
                if (under && head.W + ChartSlack < Math.Min(w - UiTokens.S4 * 2f, UiTextRules.Width(m.Title, UiStyle.SectionHeader)))
                {
                    fault = fault ?? "the title is cut though the legend left its line" + where;
                }
            }
        }
        Check(fault == null && below > 0 && beside > 0,
            "charts: a legend stays inside its chart at every width: beside the title where both fit, on its own line where not, no name over another or over the title ("
            + (fault ?? "held, " + below + " under and " + beside + " beside") + ")");
        float[] line = { 1f, 2f, 3f, 4f };
        Check(NewsRules.DaysShown(120, 9) == 9 && NewsRules.DaysShown(30, 9) == 9 && NewsRules.DaysShown(120, 300) == 120 && NewsRules.DaysShown(30, 30) == 30 && NewsRules.DaysShown(120, 0) == 1
              && ReferenceEquals(NewsRules.From(line, 0), line) && NewsRules.From(line, 3).Length == 1 && NewsRules.From(line, 3)[0] == 4f && NewsRules.From(line, 9).Length == 0
              && NewsRules.From(null, 2) == null,
            "charts: the Economy app's charts show no day before the save's first (120 days on day 9 are 9 days, never \"Day -110\"), and a line starts on its first day kept");
        string prices = ModSource(@"Ui\News\NewsLive.Prices.cs"), news = ModSource(@"Ui\News\NewsLive.cs");
        Check(prices.Contains("one.Values = NewsRules.From(one.Values, cut);") && prices.Contains("span -= cut;")
              && news.Contains("int span = NewsRules.DaysShown(NewsRules.Span(_range), end);") && !news.Contains(": i + 1).ToString(Inv)"),
            "charts: the goods chart begins on the first day a price was kept and the city's chart on the save's first day, each day named as the day it is");
        Check(NewsRules.Opening("fresh produce and flowers led") == "Fresh produce and flowers led" && NewsRules.Opening("Fuel led") == "Fuel led" && NewsRules.Opening("1 led") == "1 led"
              && NewsRules.Opening(string.Empty).Length == 0 && NewsRules.Opening(null) == null && prices.Contains("NewsRules.Opening(OutlookText.T(\"reallife_news_chip_led\""),
            "charts: the prices page's lead opens with a capital, whatever kind of goods led");
        Check(WhatsNewHas(LocaleFile("en"), "the Economy app's goods chart"), "charts: What's new tells of the Economy chart's fix");
    }

    // ---------------------------------------------------------------- the hard cases

    private static ChartCase[] ChartCases()
    {
        var wave = new float[ChartLong];
        for (int i = 0; i < wave.Length; i++)
        {
            wave[i] = (float)(4000.0 * Math.Sin(i * 0.01) - 500.0);
        }
        float nan = float.NaN, up = float.PositiveInfinity, down = float.NegativeInfinity;
        var list = new List<ChartCase>
        {
            new ChartCase { Name = "the report's four days, every one a loss within six dollars", Values = new[] { -3737.14f, -3737.14f, -3741.14f, -3741.14f } },
            new ChartCase { Name = "every value a loss", Values = new[] { -120f, -4800f, -950f, -2600f, -15f } },
            new ChartCase { Name = "nearly equal at a large value", Values = new[] { 1234560f, 1234562f, 1234561f, 1234566f } },
            new ChartCase { Name = "nearly equal losses at a large value", Values = new[] { -1234560f, -1234562f, -1234561f, -1234566f } },
            new ChartCase { Name = "one value", Values = new[] { 42f } },
            new ChartCase { Name = "one loss", Values = new[] { -42f } },
            new ChartCase { Name = "names and no values", Values = new float[0], Points = 4 },
            new ChartCase { Name = "nothing at all", Values = new float[0] },
            new ChartCase { Name = "zeros only", Values = new[] { 0f, 0f, 0f, 0f } },
            new ChartCase { Name = "the same value all through", Values = new[] { 29f, 29f, 29f } },
            new ChartCase { Name = "a huge outlier", Values = new[] { 10f, 12f, 9f, 1e9f, 11f } },
            new ChartCase { Name = "a huge loss among gains", Values = new[] { 10f, 12f, -1e9f, 9f } },
            new ChartCase { Name = "values crossing nothing", Values = new[] { -500f, 300f, -50f, 800f, -1200f, 0f } },
            new ChartCase { Name = "fractions under a dollar", Values = new[] { 0.001f, 0.004f, 0.002f } },
            new ChartCase { Name = "values that are no number among numbers", Values = new[] { 10f, nan, 30f, up, 20f, down, 25f } },
            new ChartCase { Name = "no value a number", Values = new[] { nan, nan, up } },
            new ChartCase { Name = "the last value no number", Values = new[] { 5f, 6f, nan } },
            new ChartCase { Name = "the largest a float holds", Values = new[] { 3e38f, -3e38f, 5f, 7f } },
            new ChartCase { Name = "a series of " + ChartLong + " crossing nothing", Values = wave },
        };
        for (int i = 0; i < list.Count; i++)
        {
            ChartCase c = list[i];
            c.Points = Math.Max(c.Points, c.Values.Length);
            list[i] = c;
        }
        return list.ToArray();
    }

    private static readonly CultureInfo ChartInv = CultureInfo.InvariantCulture;

    // The game's money in whole dollars, the short form an axis takes where a screen has one, the kit's own plain figures, and a count.
    private static ChartWords[] ChartFormats()
    {
        Func<float, string> money = v => (v < 0f ? "-" : string.Empty) + "$" + Math.Abs(Math.Round(v)).ToString("#,0", ChartInv);
        Func<float, string> brief = v =>
        {
            double a = Math.Abs(v);
            string sign = v < 0f ? "-" : string.Empty;
            return a >= 1e6 ? sign + "$" + (a / 1e6).ToString("0.0", ChartInv) + "M" : a >= 1e3 ? sign + "$" + (a / 1e3).ToString("0.0", ChartInv) + "k" : sign + "$" + a.ToString("0", ChartInv);
        };
        return new[]
        {
            new ChartWords { Name = "money", Format = money }, new ChartWords { Name = "short money on the axis", Format = money, Axis = brief },
            new ChartWords { Name = "plain figures" }, new ChartWords { Name = "a count", Format = v => Math.Round(v).ToString("0", ChartInv), Whole = true }
        };
    }

    // The independent test of a value that can be drawn: a number, and not one a float cannot add to another.
    private static bool ChartNumber(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) <= 1e30f;

    private static bool ChartPlaced(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

    private static UiChartModel ChartOf(UiChartKind kind, ChartCase c, ChartWords words)
    {
        float[] v = c.Values;
        var points = new string[c.Points];
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = "D" + (i + 1).ToString(ChartInv);
        }
        var m = new UiChartModel
        {
            Kind = kind, Title = "Profit, the last days", Points = points, Format = words.Format, AxisFormat = words.Axis, Whole = words.Whole, Focus = points.Length > 0 ? points.Length / 2 : -1,
            EmptyTitle = "No day counted yet", EmptyBody = "The first day comes in at midnight.", OpenLabel = "Open", HighLabel = "High", LowLabel = "Low", CloseLabel = "Close",
            VolumeLabel = "Volume"
        };
        if (kind != UiChartKind.Candles)
        {
            m.Series = new[] { new UiSeries { Label = "Profit", Values = v } };
            return m;
        }
        // A day's candle from the case: it opens on the day before's value and closes on its own, its wick a hundredth past both.
        m.Close = v;
        m.Open = new float[v.Length];
        m.High = new float[v.Length];
        m.Low = new float[v.Length];
        m.Volume = new float[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            m.Open[i] = i > 0 ? v[i - 1] : v[i];
            m.High[i] = Math.Max(m.Open[i], v[i]) + Math.Abs(v[i]) * 0.01f;
            m.Low[i] = Math.Min(m.Open[i], v[i]) - Math.Abs(v[i]) * 0.01f;
            m.Volume[i] = Math.Abs(v[i]) * 3f;
        }
        return m;
    }

    // ---------------------------------------------------------------- what a plot holds, on any scene

    // The plots of the charts in a scene: a box on a chart's band with a column for each point inside it, as UiChart.Lay lays it.
    private static void ChartPlots(UiScene s, List<int> plots)
    {
        plots.Clear();
        for (int i = 0; i < s.ItemCount; i++)
        {
            ref UiItem c = ref s.Items[i];
            if (c.Kind != UiKind.Box || c.Parent < 0 || c.Fill != UiRole.None || c.Flags != UiFlags.Share || c.Radius != UiTokens.RadiusSmall || plots.Contains(c.Parent))
            {
                continue;
            }
            ref UiItem p = ref s.Items[c.Parent];
            if (p.Kind == UiKind.Box && p.Fill == UiRole.None && p.Edge == UiRole.None && p.Parent >= 0 && s.Items[p.Parent].Fill == UiRole.Band && Math.Abs(p.Y - c.Y) < 0.01f
                && Math.Abs(p.H - c.H) < 0.01f)
            {
                plots.Add(c.Parent);
            }
        }
    }

    private static int ChartPlotOf(UiScene s, List<int> plots, int item)
    {
        for (int at = s.Items[item].Parent; at >= 0; at = s.Items[at].Parent)
        {
            if (plots.Contains(at))
            {
                return at;
            }
        }
        return -1;
    }

    // The items of a scene with no place on the screen (a NaN or an infinity in their box, a size under nothing), and the pieces drawn
    // in a chart's plot that lie outside it: bars, candles, a line's segments, washes and marks, the decoration the layout rules skip
    // included. A dot on a point may stand half over the plot's edge, as the point itself may be on it.
    private static int ChartOutside(UiScene s, List<int> plots, Action<string> say)
    {
        int found = 0;
        for (int i = 0; i < s.ItemCount; i++)
        {
            ref UiItem it = ref s.Items[i];
            if (!ChartPlaced(it.X) || !ChartPlaced(it.Y) || !ChartPlaced(it.W) || !ChartPlaced(it.H) || it.W < -UiCheckRules.Tolerance || it.H < -UiCheckRules.Tolerance)
            {
                found++;
                say?.Invoke(s.Name + ": item " + i + " (" + it.Kind + ") has no place: " + it.X + ", " + it.Y + ", " + it.W + " x " + it.H);
                continue;
            }
            int plot = ChartPlotOf(s, plots, i);
            if (plot < 0)
            {
                continue;
            }
            ref UiItem p = ref s.Items[plot];
            bool dot = (it.Flags & UiFlags.Circle) != 0;
            float sx = dot ? it.W / 2f + UiCheckRules.Tolerance : ChartSlack, sy = dot ? it.H / 2f + UiCheckRules.Tolerance : ChartSlack;
            if (it.X < p.X - sx || it.Y < p.Y - sy || it.X + it.W > p.X + p.W + sx || it.Y + it.H > p.Y + p.H + sy)
            {
                found++;
                say?.Invoke(s.Name + ": a " + it.Kind + " of " + it.Fill + " at " + it.X.ToString("0.#", ChartInv) + ", " + it.Y.ToString("0.#", ChartInv) + ", " + it.W.ToString("0.#", ChartInv) + " x "
                    + it.H.ToString("0.#", ChartInv) + " lies outside its plot at " + p.X.ToString("0.#", ChartInv) + ", " + p.Y.ToString("0.#", ChartInv) + ", " + p.W.ToString("0.#", ChartInv) + " x "
                    + p.H.ToString("0.#", ChartInv));
            }
        }
        return found;
    }

    // The value axis's ticks on a chart's band: the muted small figures at the plot's left, foot first.
    private static void ChartTicks(UiScene s, int band, List<int> ticks)
    {
        ticks.Clear();
        for (int i = band + 1; i < s.ItemCount; i++)
        {
            ref UiItem it = ref s.Items[i];
            if (it.Kind == UiKind.Text && it.Parent == band && it.Ink == UiRole.Muted && it.Style == UiStyle.Small && (it.Flags & UiFlags.Right) != 0 && (it.Flags & UiFlags.Label) == 0)
            {
                ticks.Add(i);
            }
        }
    }

    // What is wrong with an axis's ticks, or null: two at least and no more than the axis's most, each with its own words, each higher
    // than the last, every one beside the plot.
    private static string ChartTickFault(UiScene s, List<int> ticks, int plot)
    {
        if (ticks.Count < 2 || ticks.Count > UiChart.MostSteps + 1)
        {
            return ticks.Count + " ticks (2 to " + (UiChart.MostSteps + 1) + ")";
        }
        ref UiItem p = ref s.Items[plot];
        for (int k = 0; k < ticks.Count; k++)
        {
            ref UiItem t = ref s.Items[ticks[k]];
            float middle = t.Y + t.H / 2f;
            if (middle < p.Y - ChartSlack || middle > p.Y + p.H + ChartSlack)
            {
                return "the tick \"" + t.Text + "\" stands off the plot's height";
            }
            if (k > 0 && (string.Equals(t.Text, s.Items[ticks[k - 1]].Text, StringComparison.Ordinal) || t.Y >= s.Items[ticks[k - 1]].Y))
            {
                return "the ticks \"" + s.Items[ticks[k - 1]].Text + "\" and \"" + t.Text + "\" read or stand alike";
            }
        }
        return null;
    }

    // ---------------------------------------------------------------- the three kinds of chart

    private static void ChartKindChecks(ChartCase[] cases, List<int> plots)
    {
        var s = new UiScene();
        var ticks = new List<int>();
        float[][] boxes = { new[] { 560f, 240f }, new[] { 1000f, 420f }, new[] { 360f, 220f } };   // the business page's bars, a page's wide chart, a narrow column's
        double longest = 0.0;
        foreach (UiChartKind kind in new[] { UiChartKind.Bars, UiChartKind.Line, UiChartKind.Candles })
        {
            foreach (ChartCase c in cases)
            {
                string fault = null;
                foreach (ChartWords words in ChartFormats())
                {
                    foreach (float[] box in boxes)
                    {
                        if (fault != null)
                        {
                            break;
                        }
                        string where = " (" + words.Name + ", " + box[0] + " x " + box[1] + " px)";
                        try
                        {
                            UiChartModel m = ChartOf(kind, c, words);
                            s.Begin("chart of " + kind + ", " + c.Name, 1920f, 1080f, true);
                            var watch = Stopwatch.StartNew();
                            int band = UiChart.Lay(s, UiAt.Screen, m, 400f, 300f, box[0], box[1]);
                            if (c.Values.Length == ChartLong)
                            {
                                longest = Math.Max(longest, watch.Elapsed.TotalMilliseconds);
                            }
                            fault = ChartFault(s, band, m, c, plots, ticks);
                            fault = fault == null ? null : fault + where;
                        }
                        catch (Exception e)
                        {
                            fault = "it threw " + e.GetType().Name + ": " + e.Message + where;
                        }
                    }
                }
                Check(fault == null, "charts: " + kind + " of " + c.Name + ": every piece inside the plot, the axis few round ticks that read apart"
                    + (kind == UiChartKind.Bars ? ", nothing on the axis and each bar a length from it" : string.Empty) + " (" + (fault ?? "held") + ")");
            }
        }
        Console.WriteLine("        charts: a series of " + ChartLong + " lays out in " + longest.ToString("0.0", ChartInv) + " ms at the slowest (a layout a change, never a frame)");
    }

    // What is wrong with a chart laid from a case, or null.
    private static string ChartFault(UiScene s, int band, UiChartModel m, ChartCase c, List<int> plots, List<int> ticks)
    {
        float[] v = c.Values;
        int real = 0;
        for (int i = 0; i < v.Length && i < c.Points; i++)
        {
            real += ChartNumber(v[i]) ? 1 : 0;
        }
        ChartPlots(s, plots);
        int outside = ChartOutside(s, plots, ChartSay);
        if (outside > 0)
        {
            return outside + " pieces outside the plot or with no place";
        }
        ref UiItem b = ref s.Items[band];
        for (int i = band + 1; i < s.ItemCount; i++)
        {
            ref UiItem it = ref s.Items[i];
            if (it.Parent == band && it.Layer == 0 && (it.X < b.X - ChartSlack || it.Y < b.Y - ChartSlack || it.X + it.W > b.X + b.W + ChartSlack || it.Y + it.H > b.Y + b.H + ChartSlack))
            {
                return "a " + it.Kind + " \"" + it.Text + "\" of the chart lies outside its band";
            }
        }
        if (c.Points == 0 || real == 0)
        {
            bool said = false;
            for (int i = band + 1; i < s.ItemCount; i++)
            {
                said |= s.Items[i].Kind == UiKind.Text && s.Items[i].Text == m.EmptyTitle;
            }
            return plots.Count == 0 && said ? null : "nothing to draw, and the chart does not say so in its room";
        }
        if (plots.Count != 1 || s.Items[plots[0]].Parent != band)
        {
            return plots.Count + " plots on the band";
        }
        int plot = plots[0];
        ChartTicks(s, band, ticks);
        string tickFault = ChartTickFault(s, ticks, plot);
        if (tickFault != null)
        {
            return tickFault;
        }
        UiChart.Scale(m, out float lo, out float hi, out float step);
        if (UiPartRules.RoughTicks(m, ChartSay) != 0 || (int)Math.Round((hi - lo) / step) != ticks.Count - 1)
        {
            return "the axis steps by " + step + " from " + lo + " to " + hi + " under " + ticks.Count + " ticks";
        }
        ref UiItem p = ref s.Items[plot];
        if (m.Kind == UiChartKind.Bars && !(lo <= 0f && hi >= 0f))
        {
            return "the axis runs from " + lo + " to " + hi + " and leaves nothing out";
        }
        // The columns come in the points' order, each with what is drawn for its point inside it.
        float zero = p.Y + p.H - (0f - lo) / (hi - lo) * p.H;
        int column = -1, at = -1, drawn = 0, segments = 0;
        for (int i = plot + 1; i < s.ItemCount; i++)
        {
            ref UiItem it = ref s.Items[i];
            if (it.Parent == plot && it.Kind == UiKind.Box && it.Fill == UiRole.None && it.Flags == UiFlags.Share)
            {
                column = i;
                at++;
                continue;
            }
            segments += it.Parent == plot && it.Kind == UiKind.Spark ? 1 : 0;
            if (it.Parent != column || column < 0 || it.Kind != UiKind.Box || (it.Flags & UiFlags.Decor) != 0 || it.Fill == UiRole.None)
            {
                continue;
            }
            drawn++;
            if (m.Kind != UiChartKind.Bars)
            {
                continue;
            }
            float value = at < v.Length ? v[at] : float.NaN;
            if (!ChartNumber(value))
            {
                return "a bar is drawn for a value that is no number";
            }
            float length = Math.Max(ChartStub, Math.Abs(value) / (hi - lo) * p.H);
            float from = value < 0f ? it.Y : it.Y + it.H;   // the end of the bar on the line of nothing
            if (Math.Abs(from - zero) > ChartStub + UiCheckRules.Tolerance || Math.Abs(it.H - length) > ChartSlack)
            {
                return "the bar of " + value + " runs " + it.H.ToString("0.#", ChartInv) + " px from " + from.ToString("0.#", ChartInv) + ", not " + length.ToString("0.#", ChartInv)
                    + " px from the line of nothing at " + zero.ToString("0.#", ChartInv);
            }
        }
        if (m.Kind != UiChartKind.Line && drawn != real)
        {
            return drawn + " drawn for " + real + " values that are numbers";
        }
        if (m.Kind == UiChartKind.Line)
        {
            int pairs = 0;
            for (int i = 1; i < v.Length; i++)
            {
                pairs += ChartNumber(v[i - 1]) && ChartNumber(v[i]) ? 1 : 0;
            }
            if (segments != pairs)
            {
                return segments + " segments for " + pairs + " pairs of numbers side by side";
            }
        }
        return null;
    }

    // ---------------------------------------------------------------- the report's page

    // The EconoView business page with the player's figures, and with every hard case as its profit: the chart inside its card, the
    // card inside the page.
    private static void ChartReportChecks(ChartCase[] cases, List<int> plots)
    {
        UiWords en = UiWords("en");
        var s = new UiScene();
        var ticks = new List<int>();
        UiEconoModel m = UiSamples.Econo(en, "business_loss");
        float[] profit = m.Chart != null && m.Chart.Series.Length == 1 ? m.Chart.Series[0].Values : new float[0];
        float least = float.MaxValue, most = float.MinValue;
        for (int i = 0; i < profit.Length; i++)
        {
            least = Math.Min(least, profit[i]);
            most = Math.Max(most, profit[i]);
        }
        Check(m.Page == UiEcono.Business && m.Chart != null && m.Chart.Kind == UiChartKind.Bars && profit.Length == 4 && least >= -3742f && most <= -3736f && most - least > 1f
              && profit[3] == UiSamples.LossProfit[0], "charts: the EconoView sample of a headquarters holds the report's four days, each a loss between $3,736 and $3,742");
        if (m.Chart == null)
        {
            return;
        }
        UiChart.Scale(m.Chart, out float lo, out float hi, out float step);
        Check(lo == -4000f && hi == 0f && step == 1000f, "charts: the report's axis runs from -$4,000 to $0 by $1,000, not from -$3,742 to -$3,736 by a dollar (" + lo + " to " + hi
            + " by " + step + ")");
        foreach (float aspect in UiAspects)
        {
            UiFrame frame = UiFrame.FullMenu(aspect, "EconoView");
            UiEcono.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
            string fault = ChartPageFault(s, plots, 4, true);
            if (fault == null)
            {
                ChartTicks(s, s.Items[plots[0]].Parent, ticks);
                fault = ChartTickFault(s, ticks, plots[0]);
                fault = fault ?? (ticks.Count == 5 && s.Items[ticks[0]].Text == "-$4,000" && s.Items[ticks[4]].Text == "$0" ? null : "its ticks do not read -$4,000 to $0");
            }
            Check(fault == null, "charts: the report's page at " + aspect.ToString("0.00", ChartInv) + " draws its four bars inside their card, each hanging from $0 at the plot's top ("
                + (fault ?? "held") + ")");
        }
        // The same page with every hard case as the business's profit.
        UiFrame wide = UiFrame.FullMenu(16f / 9f, "EconoView");
        foreach (ChartCase c in cases)
        {
            string fault;
            try
            {
                UiEconoModel page = UiSamples.Econo(en, "business_loss");
                page.Chart.Series[0].Values = c.Values;
                page.Chart.Points = new string[c.Points];
                page.Chart.Reads = null;
                int real = 0;
                for (int i = 0; i < c.Points; i++)
                {
                    page.Chart.Points[i] = "Day " + (128 + i).ToString(ChartInv);
                    real += i < c.Values.Length && ChartNumber(c.Values[i]) ? 1 : 0;
                }
                UiEcono.Lay(s, page, wide, true, UiDefaultKeys.Keyboard);
                fault = ChartPageFault(s, plots, real, false);
            }
            catch (Exception e)
            {
                fault = "it threw " + e.GetType().Name + ": " + e.Message;
            }
            Check(fault == null, "charts: the EconoView business page with a profit of " + c.Name + " draws inside its card (" + (fault ?? "held") + ")");
        }
    }

    // What is wrong with a page that holds one bar chart of so many bars, or null: every piece inside the plot, the chart's band inside
    // the screen, every bar inside the band, and (hanging) every bar from the plot's top.
    private static string ChartPageFault(UiScene s, List<int> plots, int bars, bool hanging)
    {
        ChartPlots(s, plots);
        int outside = ChartOutside(s, plots, ChartSay);
        if (outside > 0)
        {
            return outside + " pieces outside the plot or with no place";
        }
        if (bars == 0)
        {
            return plots.Count == 0 ? null : "a plot with nothing to draw";
        }
        if (plots.Count != 1)
        {
            return plots.Count + " plots on the page";
        }
        int plot = plots[0], band = s.Items[plot].Parent, drawn = 0;
        ref UiItem p = ref s.Items[plot];
        ref UiItem b = ref s.Items[band];
        if (b.X < 0f || b.Y < 0f || b.X + b.W > s.Width + UiCheckRules.Tolerance || b.Y + b.H > s.Height + UiCheckRules.Tolerance)
        {
            return "the chart's card lies off the screen";
        }
        for (int i = plot + 1; i < s.ItemCount; i++)
        {
            ref UiItem it = ref s.Items[i];
            if (it.Kind != UiKind.Box || it.Parent < 0 || s.Items[it.Parent].Parent != plot || (it.Flags & UiFlags.Decor) != 0 || it.Fill == UiRole.None)
            {
                continue;
            }
            drawn++;
            if (it.X < b.X || it.Y < b.Y || it.X + it.W > b.X + b.W || it.Y + it.H > b.Y + b.H)
            {
                return "a bar lies outside the chart's card";
            }
            if (hanging && Math.Abs(it.Y - p.Y) > UiCheckRules.Tolerance)
            {
                return "a bar starts " + (it.Y - p.Y).ToString("0.#", ChartInv) + " px from the plot's top";
            }
        }
        if (drawn != bars)
        {
            return drawn + " bars for " + bars + " days";
        }
        return UiCheckRules.Spills(s, ChartSay) == 0 ? null : "something spills out of its parent";
    }

    // ---------------------------------------------------------------- the stocks desk's chart

    // The company chart as candles, a line and an area with every hard case as its prices: every item has a place, what is drawn in
    // the plot's region lies inside it, and the price axis holds a few ticks and always ends.
    private static void ChartDeskChecks(ChartCase[] cases)
    {
        var s = new UiScene();
        string[] kinds = { "candles", "a line", "an area" };
        foreach (ChartCase c in cases)
        {
            string fault = null;
            for (int kind = 0; kind < kinds.Length && fault == null; kind++)
            {
                try
                {
                    UiChartModel from = ChartOf(UiChartKind.Candles, c, default(ChartWords));
                    int n = c.Values.Length;
                    var d = new UiChartDeskModel
                    {
                        Kind = kind, Head = "HSF, Days", Open = from.Open, High = from.High, Low = from.Low, Close = from.Close, Volume = from.Volume, Day = new int[n], Stamp = new string[n],
                        Shown = n, First = 0, Focus = n / 2, Price = v => "$" + v.ToString("0.00", ChartInv), LastTag = "$53.42", DayLabel = "Day", OpenLabel = "Open", HighLabel = "High",
                        LowLabel = "Low", CloseLabel = "Close", VolumeLabel = "Volume", EmptyTitle = "No candles yet", EmptyBody = "Candles appear as the market trades."
                    };
                    for (int i = 0; i < n; i++)
                    {
                        d.Stamp[i] = "Day " + (i + 1).ToString(ChartInv);
                    }
                    s.Begin("stocks chart as " + kinds[kind] + ", " + c.Name, 1920f, 1080f, true);
                    UiChartDesk.Lay(s, UiAt.Screen, d, 400f, 200f, 1052f, 700f, UiDefaultKeys.Keyboard);
                    fault = ChartDeskFault(s, d, n);
                    fault = fault == null ? null : fault + " (as " + kinds[kind] + ")";
                }
                catch (Exception e)
                {
                    fault = "it threw " + e.GetType().Name + ": " + e.Message + " (as " + kinds[kind] + ")";
                }
            }
            Check(fault == null, "charts: the stocks chart of " + c.Name + ": every piece has a place inside the plot's region, the price axis a few ticks (" + (fault ?? "held") + ")");
        }
        // A price too large for a float to step by a quarter: the axis ends all the same (it once summed its steps until past the top).
        var flat = new UiChartDeskModel { Kind = 1, Close = new[] { 3e7f, 3e7f, 3e7f }, Shown = 3, Price = v => "$" + v.ToString("0.00", ChartInv), LastTag = "$30,000,000.00" };
        s.Begin("stocks chart, a flat price of thirty million", 1920f, 1080f, true);
        UiChartDesk.Lay(s, UiAt.Screen, flat, 400f, 200f, 1052f, 700f, UiDefaultKeys.Keyboard);
        Check(ChartDeskFault(s, flat, 3) == null, "charts: the stocks chart of a flat price of thirty million lays out, its axis ended (" + (ChartDeskFault(s, flat, 3) ?? "held") + ")");
    }

    private static string ChartDeskFault(UiScene s, UiChartDeskModel d, int n)
    {
        int ticks = 0;
        for (int i = 0; i < s.ItemCount; i++)
        {
            ref UiItem it = ref s.Items[i];
            if (!ChartPlaced(it.X) || !ChartPlaced(it.Y) || !ChartPlaced(it.W) || !ChartPlaced(it.H) || it.W < -UiCheckRules.Tolerance || it.H < -UiCheckRules.Tolerance)
            {
                return "item " + i + " (" + it.Kind + " \"" + it.Text + "\") has no place: " + it.X + ", " + it.Y + ", " + it.W + " x " + it.H;
            }
            ticks += it.Kind == UiKind.Text && it.Layer == 0 && it.Clip < 0 && it.Ink == UiRole.Muted && it.Style == UiStyle.Small && (it.Flags & UiFlags.Centre) == 0
                     && it.X >= d.PlotX + d.PlotW ? 1 : 0;
            if (n == 0 || d.PlotClip < 0 || it.Clip != d.PlotClip || (i >= d.MarkFrom && i < d.MarkTo))
            {
                continue;
            }
            bool dot = (it.Flags & UiFlags.Circle) != 0;
            float sx = dot ? it.W / 2f + UiCheckRules.Tolerance : ChartSlack, sy = dot ? it.H / 2f + UiCheckRules.Tolerance : ChartSlack;
            float stack = s.Clips[d.PlotClip].H;
            if (it.X < -sx || it.Y < -sy || it.X + it.W > d.PlotW + sx || it.Y + it.H > stack + sy)
            {
                return "a " + it.Kind + " of " + it.Fill + " at " + it.X.ToString("0.#", ChartInv) + ", " + it.Y.ToString("0.#", ChartInv) + ", " + it.W.ToString("0.#", ChartInv) + " x "
                    + it.H.ToString("0.#", ChartInv) + " lies outside the plot's " + d.PlotW.ToString("0.#", ChartInv) + " x " + stack.ToString("0.#", ChartInv);
            }
        }
        if (n == 0)
        {
            return d.PlotClip < 0 ? null : "a plot with no candles";
        }
        return ticks >= 1 && ticks <= 12 ? null : ticks + " ticks on the price axis (1 to 12)";
    }

    // ---------------------------------------------------------------- the sparkline

    // The sparkline's span and each value's share of its height, as its renderer draws them: a value that is no number is no part of
    // the span and has no point, every other stands between the box's foot and its top, and fewer than two numbers are no line.
    private static void ChartSparkChecks(ChartCase[] cases)
    {
        foreach (ChartCase c in cases)
        {
            float[] v = c.Values;
            int real = 0;
            float least = float.MaxValue, most = float.MinValue;
            for (int i = 0; i < v.Length; i++)
            {
                if (ChartNumber(v[i]))
                {
                    real++;
                    least = Math.Min(least, v[i]);
                    most = Math.Max(most, v[i]);
                }
            }
            bool line = UiDraw.SparkSpan(v, out float lo, out float hi);
            bool held = line == (real >= 2) && (!line || (lo == least && hi == most));
            for (int i = 0; line && i < v.Length; i++)
            {
                if (!ChartNumber(v[i]))
                {
                    held &= !UiChart.Real(v[i]);
                    continue;
                }
                float share = UiDraw.SparkShare(v[i], lo, hi);
                held &= UiChart.Real(v[i]) && share >= 0f && share <= 1f && (hi - lo < 1e-6f ? share == 0f : Math.Abs(share - (v[i] - lo) / (hi - lo)) < 1e-4f);
            }
            Check(held && (!UiDraw.Drawn(v) || line), "charts: a sparkline of " + c.Name + " spans its numbers alone, each between its box's foot and top (" + (line ? lo + " to " + hi : "no line") + ")");
        }
        Check(UiDraw.Unit(float.NaN) == 0f && UiDraw.Unit(-3f) == 0f && UiDraw.Unit(float.PositiveInfinity) == 1f && UiDraw.Unit(0.25f) == 0.25f
              && UiDraw.SparkShare(float.NaN, 0f, 10f) == 0f, "charts: a share that is no number is nothing, one past the whole is the whole");
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui"));
        string spark = File.ReadAllText(System.IO.Path.Combine(folder, "UiSpark.cs"));
        Check(spark.Contains("UiDraw.SparkSpan(p, out float lo, out float hi)") && spark.Contains("UiDraw.SparkShare(p[i], lo, hi)") && spark.Contains("if (!UiChart.Real(p[i]))")
              && !spark.Contains("Mathf.Min(lo, p[i])"), "charts: the sparkline's renderer draws by the span and the share checked here, and skips a value that is no number");
    }

    // ---------------------------------------------------------------- shares: the donut, the ring, the meters and the gauge

    private static void ChartShareChecks(ChartCase[] cases)
    {
        var s = new UiScene();
        foreach (ChartCase c in cases)
        {
            if (c.Values.Length == ChartLong)
            {
                continue;   // a donut of thousands of slices is no donut: its slices are checked on the cases a legend could name
            }
            float[] v = c.Values;
            float sum = 0f;
            int parts = 0;
            for (int i = 0; i < v.Length; i++)
            {
                if (ChartNumber(v[i]) && v[i] > 0f)
                {
                    sum += v[i];
                    parts++;
                }
            }
            string fault = null;
            try
            {
                s.Begin("shares of " + c.Name, 1920f, 1080f, true);
                int box = UiDraw.Box(s, UiAt.Screen, 400f, 300f, 400f, 400f, UiRole.Panel, UiRole.None, 0f);
                UiAt at = UiAt.Screen.In(box);
                int first = s.ItemCount;
                UiDraw.Donut(s, at, 408f, 308f, 104f, v, v.Length);
                float turn = 0f, last = 0f;
                int slices = 0;
                for (int i = first; i < s.ItemCount && fault == null; i++)
                {
                    ref UiItem it = ref s.Items[i];
                    if (it.Kind != UiKind.Slice)
                    {
                        continue;
                    }
                    slices++;
                    bool sound = it.Points != null && it.Points.Length == 2 && ChartPlaced(it.Points[0]) && ChartPlaced(it.Points[1]) && ChartPlaced(it.Value) && it.Value >= 0f
                                 && it.Points[0] >= last - 1e-4f && it.Points[1] >= it.Points[0] - 1e-4f && it.Points[1] <= 1f + 1e-4f;
                    fault = sound ? null : "a slice from " + (it.Points != null && it.Points.Length == 2 ? it.Points[0] + " to " + it.Points[1] : "nowhere") + " of the turn";
                    turn += it.Value;
                    last = sound ? it.Points[1] : last;
                }
                fault = fault ?? (parts == 0 ? (slices == 0 ? null : "slices of nothing") : slices == v.Length && Math.Abs(turn - 1f) < 1e-3f ? null : slices + " slices over " + turn + " of the turn");
                // Each value as a share of the largest on a ring, a meter with its words, a thin meter and a timing gauge's zone and marker.
                float largest = sum > 0f ? sum : 0f;
                int marks = s.ItemCount;
                for (int i = 0; i < v.Length && i < 8; i++)
                {
                    float share = v[i] / largest;   // a division by nothing where the case sums to nothing
                    float y = 420f + i * 32f;
                    UiDraw.Ring(s, at, 408f + i * 40f, 316f + 104f, 32f, share);
                    var bar = new UiProgressModel { Label = "Done", Value = "64%", Share = share };
                    UiDraw.Progress(s, at, 408f, y + 64f, 180f, ref bar);
                    var gauge = new UiTimingModel { Label = "Hit the mark", Value = "2 of 3", ZoneFrom = share, ZoneTo = share * 2f, Marker = share };
                    UiDraw.Timing(s, at, 600f, y + 64f, 180f, ref gauge);
                    UiDraw.Meter(s, at, 408f, y + 60f, 180f, UiTokens.S1, (int)Math.Round(share * 100f), UiRole.Accent);
                }
                for (int i = marks; i < s.ItemCount && fault == null; i++)
                {
                    ref UiItem it = ref s.Items[i];
                    bool placed = ChartPlaced(it.X) && ChartPlaced(it.Y) && ChartPlaced(it.W) && ChartPlaced(it.H) && it.W >= 0f && it.H >= 0f;
                    bool turned = it.Kind != UiKind.Slice || (it.Points != null && ChartPlaced(it.Points[0]) && ChartPlaced(it.Points[1]) && it.Points[1] >= 0f && it.Points[1] <= 1f);
                    bool inside = it.Parent < 0 || (it.Flags & UiFlags.Decor) != 0 || it.Kind == UiKind.Text
                                  || (it.X >= s.Items[it.Parent].X - UiCheckRules.Tolerance && it.X + it.W <= s.Items[it.Parent].X + s.Items[it.Parent].W + UiCheckRules.Tolerance);
                    fault = placed && turned && inside ? null : "a " + it.Kind + " of " + it.Fill + " at " + it.X + ", " + it.W + " wide" + (turned ? string.Empty : ", turned to no angle");
                }
            }
            catch (Exception e)
            {
                fault = "it threw " + e.GetType().Name + ": " + e.Message;
            }
            Check(fault == null, "charts: shares of " + c.Name + ": the donut's slices share one turn, and the ring, meters and gauge stay on their tracks (" + (fault ?? "held") + ")");
        }
    }

    // ---------------------------------------------------------------- every screen's own sample

    // Every registered screen in every state the checks lay out, once: wherever it holds a chart, every piece of the plot inside it.
    private static void ChartScreenChecks(List<int> plots)
    {
        var s = new UiScene();
        UiWords en = UiWords("en");
        int screens = 0, charts = 0;
        var names = new List<string>();
        foreach (UiScreenCase c in UiCases())
        {
            c.Lay(s, UiFrame.FullMenu(16f / 9f, c.Title), true, UiDefaultKeys.Keyboard, en);
            ChartPlots(s, plots);
            if (plots.Count == 0)
            {
                continue;
            }
            screens++;
            charts += plots.Count;
            if (!names.Contains(c.Def.Id))
            {
                names.Add(c.Def.Id);
            }
            int outside = ChartOutside(s, plots, ChartSay);
            Check(outside == 0, "charts: every piece of " + c.Name + "'s " + plots.Count + " charts lies inside its plot (" + outside + " outside)");
        }
        string held = charts + " charts in " + screens + " states of " + names.Count + " screens: " + string.Join(", ", names);
        Check(screens >= 10 && names.Contains("econoview"), "charts: the screens' samples hold charts to check (" + held + ")");
        Console.WriteLine("        charts: " + held);
    }
}
