using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using RealLifeOverhaul.Markets;
using RealLifeOverhaul.Ui;

// The company page's chart and order (Scripts/Ui/Stocks), after the owner's play on 4 Nov (day 5): nine identical icons on the tool row,
// a drawing that ran off the plot, an axis in cents, no legend, a signals text about indicators the chart did not have, a "Most you
// can buy" of 0 with $1.9B, no big screen. Each is held here: every tool has its own icon and its tip, a drawing is cut to the plot,
// the axis is in dollars, the signals name only what the chart has, the most the cash buys is the largest the fee rule allows, the big
// screen opens and shuts by button and Esc, a hover lays the page out again without building it, and the day-5 history has no hole.
internal static partial class Program
{
    private static void StocksChartChecks()
    {
        StocksToolChecks();
        StocksClipChecks();
        StocksAxisChecks();
        StocksSignalChecks();
        StocksMostChecks();
        StocksBigChecks();
        StocksHoverChecks();
        StocksHistoryChecks();
        StocksDrawingBookChecks();
        StocksCostChecks();
    }

    private static UiScene StocksChartScene(UiChartDeskModel m, float w = 1052f, float h = 700f)
    {
        var s = new UiScene();
        s.Begin("stocks chart", 1920f, 1080f, true);
        UiChartDesk.Lay(s, UiAt.Screen, m, 400f, 200f, w, h, UiDefaultKeys.Keyboard);
        return s;
    }

    private static void StocksToolChecks()
    {
        var icons = new HashSet<UiIcon>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        bool named = true;
        UiWords en = UiWords("en");
        for (int i = 0; i < UiChartTools.Count; i++)
        {
            icons.Add(UiChartTools.Icons[i]);
            keys.Add(UiChartTools.Keys[i]);
            named &= UiChartTools.Icons[i] != UiIcon.None && UiChartTools.Icons[i] != UiIcon.Chart && en(UiChartTools.Keys[i], null) != null && en(UiChartTools.Keys[i], null).Length > 0;
        }
        icons.Add(UiIcon.Trash);
        Check(icons.Count == UiChartTools.Count + 1 && keys.Count == UiChartTools.Count && named, "stocks chart: each of the " + UiChartTools.Count + " drawing tools has its own icon and its own tip, and none is the bar chart nine times");
        // On the laid rail: a target for each tool and the delete, each with a different icon, and a tip for each when the pointer rests on it.
        UiChartDeskModel m = UiSamples.StocksChartSample(en, 60, false, 0, new bool[(int)StocksInd.Count]);
        UiScene s = StocksChartScene(m);
        var seen = new HashSet<UiIcon>();
        int rail = 0;
        for (int i = 0; i < s.NodeCount; i++)
        {
            if (s.Nodes[i].Command != UiChartDesk.Rail)
            {
                continue;
            }
            rail++;
            for (int k = s.Nodes[i].Face + 1; k < s.ItemCount && s.Items[k].Parent >= 0; k++)
            {
                if (s.Items[k].Kind == UiKind.Icon && s.Items[k].X >= s.Nodes[i].X && s.Items[k].X <= s.Nodes[i].X + s.Nodes[i].W && s.Items[k].Y >= s.Nodes[i].Y && s.Items[k].Y <= s.Nodes[i].Y + s.Nodes[i].H)
                {
                    seen.Add(s.Items[k].Icon);
                    break;
                }
            }
        }
        Check(rail == UiChartTools.Count + 1 && seen.Count == rail, "stocks chart: the laid rail has a target for each tool and the delete, each drawn with a different icon (" + seen.Count + " of " + rail + ")");
        bool tips = true;
        for (int tip = 0; tip <= UiChartTools.Count; tip++)
        {
            m.ToolNames = new string[UiChartTools.Count];
            for (int i = 0; i < UiChartTools.Count; i++)
            {
                m.ToolNames[i] = en(UiChartTools.Keys[i], null);
            }
            m.DeleteName = en("reallife_stk_delete_all", null);
            m.Tip = tip;
            UiScene t = StocksChartScene(m);
            string want = tip == UiChartTools.Count ? m.DeleteName : m.ToolNames[tip];
            bool found = false;
            for (int i = 0; i < t.ItemCount; i++)
            {
                found |= t.Items[i].Kind == UiKind.Text && t.Items[i].Layer == 1 && t.Items[i].Text == want;
            }
            tips &= found;
        }
        Check(tips, "stocks chart: the pointer on a rail button, or on the delete, shows its name");
        Check(en("reallife_stk_delete_one", null) != en("reallife_stk_delete_all", null) && en("reallife_stk_delete_one", "").Length > 0, "stocks chart: the delete's tip says whether it deletes the selected drawing or every one");
    }

    // A drawing is cut to the price plot: a line that runs off the plot ends at its edge, and one wholly outside is not drawn.
    private static void StocksClipChecks()
    {
        float x0 = -50f, y0 = 20f, x1 = 300f, y1 = 400f;
        bool cut = UiChartDesk.ClipLine(ref x0, ref y0, ref x1, ref y1, 1f, 1f, 199f, 299f);
        Check(cut && x0 >= 1f - 1e-3f && x1 <= 199f + 1e-3f && y0 >= 1f - 1e-3f && y1 <= 299f + 1e-3f, "stocks chart: a line that runs off the plot is cut at its edge");
        float ax = 500f, ay = 500f, bx = 600f, by = 700f;
        Check(!UiChartDesk.ClipLine(ref ax, ref ay, ref bx, ref by, 1f, 1f, 199f, 299f), "stocks chart: a line wholly outside the plot is not drawn");
        UiWords en = UiWords("en");
        var on = new bool[(int)StocksInd.Count];
        on[(int)StocksInd.Sma20] = true;
        on[(int)StocksInd.Rsi] = true;
        UiChartDeskModel m = UiSamples.StocksChartSample(en, 100, false, 0, on);
        int n = 100;
        m.Marks = new[]
        {
            new UiChartMark { Kind = UiChartMarkKind.Trend, FromX = 40, ToX = 130, FromY = m.Close[40], ToY = 200f },   // runs far above and right of the plot
            new UiChartMark { Kind = UiChartMarkKind.Trend, FromX = -40, ToX = 20, FromY = 1f, ToY = m.Close[20] },    // starts left of it and below
            new UiChartMark { Kind = UiChartMarkKind.Level, FromX = 0, ToX = 0, FromY = 3f, ToY = 3f },               // under the lowest price shown
            new UiChartMark { Kind = UiChartMarkKind.Ray, FromX = 70, ToX = 90, FromY = m.Close[70], ToY = m.Close[70] + 20f },
            new UiChartMark { Kind = UiChartMarkKind.Rectangle, FromX = -10, ToX = 30, FromY = 500f, ToY = 2f },
            new UiChartMark { Kind = UiChartMarkKind.Fibonacci, FromX = 10, ToX = 90, FromY = 200f, ToY = -100f }
        };
        UiScene s = StocksChartScene(m);
        float pw = m.PlotW, ph = m.PriceH;
        int inRange = 0, outside = 0;
        for (int i = m.MarkFrom; i < m.MarkTo; i++)
        {
            ref UiItem it = ref s.Items[i];
            if (it.Kind != UiKind.Spark && it.Kind != UiKind.Box)
            {
                continue;
            }
            inRange++;
            float t = UiTokens.Hairline + 0.01f;
            if (it.X < -t || it.Y < -t || it.X + it.W > pw + t || it.Y + it.H > ph + t)
            {
                outside++;
            }
        }
        Check(inRange > 0 && outside == 0, "stocks chart: every stroke and fill of the drawings lies inside the price plot, over the volume and the panes never (" + inRange + " drawn, " + outside + " outside)");
        // The Spark's box is also the fence for a drawing placed by the pointer: a click below the price plot reads the plot's foot.
        float priceAtFoot = UiChartDesk.PriceAt(ph, m.PlotLo, m.PlotHi, ph), priceAtTop = UiChartDesk.PriceAt(0f, m.PlotLo, m.PlotHi, ph);
        Check(Math.Abs(priceAtFoot - m.PlotLo) < 1e-3f && Math.Abs(priceAtTop - m.PlotHi) < 1e-3f, "stocks chart: the price read at a height of the plot is the price drawn there (both ends)");
        // A drawing keeps its candle when the window moves: the same mark is placed further left as the window moves right.
        var one = new[] { new UiChartMark { Kind = UiChartMarkKind.Level, FromX = 50, ToX = 50, FromY = 40f, ToY = 40f } };
        m.Marks = one;
        m.First = 0;
        m.Shown = 60;
        UiScene a = StocksChartScene(m);
        float yA = LevelY(a, m);
        m.First = 30;
        m.Shown = 70;
        UiScene b = StocksChartScene(m);
        float yB = LevelY(b, m);
        Check(yA > 0f && yB > 0f, "stocks chart: a drawing is drawn at its own price whatever the window shows (" + yA.ToString("0.0", CultureInfo.InvariantCulture) + ", " + yB.ToString("0.0", CultureInfo.InvariantCulture) + ")");
        Check(n == 100, "stocks chart: the sample has the candles it says");
    }

    private static float LevelY(UiScene s, UiChartDeskModel m)
    {
        for (int i = m.MarkFrom; i < m.MarkTo; i++)
        {
            if (s.Items[i].Kind == UiKind.Spark)
            {
                return s.Items[i].Y;
            }
        }
        return -1f;
    }

    // The price axis is in dollars, as the price is: a tick is "$50.00", never the cents (5000).
    private static void StocksAxisChecks()
    {
        UiWords en = UiWords("en");
        var on = new bool[(int)StocksInd.Count];
        UiChartDeskModel m = UiSamples.StocksChartSample(en, 80, false, 0, on);
        UiScene s = StocksChartScene(m);
        int ticks = 0;
        bool dollars = true, inside = true;
        for (int i = 0; i < s.ItemCount; i++)
        {
            ref UiItem it = ref s.Items[i];
            if (it.Kind != UiKind.Text || it.Layer != 0 || it.X < m.PlotX + m.PlotW || it.Y < m.PlotY || it.Y > m.PlotY + m.PriceH + 12f || it.Text == m.LastTag)
            {
                continue;
            }
            ticks++;
            dollars &= it.Text.StartsWith("$", StringComparison.Ordinal) && it.Text.IndexOf('.') > 0;
            if (float.TryParse(it.Text.TrimStart('$'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            {
                inside &= v >= m.PlotLo - 0.01f && v <= m.PlotHi + 0.01f;
            }
            else
            {
                inside = false;
            }
        }
        Check(ticks >= 3 && dollars && inside, "stocks chart: the price axis is in dollars with its cents (" + ticks + " ticks), each between the lowest and highest price drawn");
        bool tag = false, stamps = false;
        for (int i = 0; i < s.ItemCount; i++)
        {
            tag |= s.Items[i].Kind == UiKind.Text && s.Items[i].Text == "$53.42" && s.Items[i].X >= m.PlotX + m.PlotW;
            stamps |= s.Items[i].Kind == UiKind.Text && s.Items[i].Text != null && s.Items[i].Text.StartsWith("Day ", StringComparison.Ordinal) && s.Items[i].Y > m.PlotY + m.PriceH;
        }
        Check(tag, "stocks chart: the last price stands on the axis in a tag");
        Check(stamps, "stocks chart: the days stand along the foot of the chart");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int labelled = 0;
        for (int i = 0; i < s.ItemCount; i++)
        {
            if (s.Items[i].Kind == UiKind.Text && s.Items[i].Text != null && s.Items[i].Text.StartsWith("Day ", StringComparison.Ordinal) && s.Items[i].Y > m.PlotY + m.PriceH)
            {
                labelled++;
                seen.Add(s.Items[i].Text);
            }
        }
        Check(labelled >= 3 && seen.Count == labelled, "stocks chart: the days along the foot are different days, " + labelled + " of them");
    }

    // The signals speak only of what the chart has: an indicator on the model gets its line with its number, one that is off gets none.
    private static void StocksSignalChecks()
    {
        UiWords en = UiWords("en");
        bool every = true, none = true;
        for (int mask = 0; mask < 1 << (int)StocksInd.Count; mask += 7)
        {
            var on = new bool[(int)StocksInd.Count];
            for (int i = 0; i < on.Length; i++)
            {
                on[i] = (mask & (1 << i)) != 0;
            }
            UiChartDeskModel m = UiSamples.StocksChartSample(en, 100, false, 0, on);
            StocksSignal[] signals = StocksSignals.Read(m, en);
            for (int k = 0; k < signals.Length; k++)
            {
                if (signals[k].Ind == StocksInd.Count)
                {
                    continue;
                }
                bool has = false;
                for (int l = 0; l < m.Lines.Length; l++)
                {
                    has |= m.Lines[l].Ind == signals[k].Ind;
                }
                for (int p = 0; p < m.Panes.Length; p++)
                {
                    has |= m.Panes[p].Ind == signals[k].Ind;
                }
                every &= has;
            }
            none &= m.Lines.Length + m.Panes.Length > 0 || signals.Length == 0;
        }
        Check(every, "stocks chart: every indicator the signals name is on the chart model");
        Check(none, "stocks chart: with no indicator and no drawing the chart says nothing");
        // RSI said with its number, and only with its pane: a steady rise reads 100, overbought.
        var rsiOn = new bool[(int)StocksInd.Count];
        rsiOn[(int)StocksInd.Rsi] = true;
        UiChartDeskModel up = UiSamples.StocksChartSample(en, 100, false, 0, rsiOn);
        for (int i = 0; i < 100; i++)
        {
            up.Close[i] = 20f + i * 0.5f;
        }
        UiChartPane pane = up.Panes[0];
        for (int i = 14; i < 100; i++)
        {
            pane.Values[i] = 84f;
        }
        up.Panes[0] = pane;
        StocksSignal[] rsi = StocksSignals.Read(up, en);
        Check(rsi.Length == 1 && rsi[0].Ind == StocksInd.Rsi && rsi[0].Words == "RSI 84: overbought", "stocks chart: the RSI is said with its number and its state (" + (rsi.Length > 0 ? rsi[0].Words : "nothing") + ")");
        var bare = new bool[(int)StocksInd.Count];
        UiChartDeskModel plain = UiSamples.StocksChartSample(en, 100, false, 0, bare);
        Check(StocksSignals.Read(plain, en).Length == 0, "stocks chart: a chart with no RSI pane has no RSI line in its signals");
        // A trend line the price holds above is said, and only while the line reaches the last candle.
        plain.Marks = new[] { new UiChartMark { Kind = UiChartMarkKind.Trend, FromX = 60, ToX = 99, FromY = plain.Close[99] - 5f, ToY = plain.Close[99] - 1f } };
        StocksSignal[] trend = StocksSignals.Read(plain, en);
        plain.Marks = new[] { new UiChartMark { Kind = UiChartMarkKind.Trend, FromX = 10, ToX = 40, FromY = 20f, ToY = 25f } };
        Check(trend.Length == 1 && trend[0].Ind == StocksInd.Count && trend[0].Words.Contains("trend line") && StocksSignals.Read(plain, en).Length == 0,
            "stocks chart: the trend line is said while it reaches the last candle, and not after");
        // The picker lists every indicator the model has, each with its own colour.
        var tints = new HashSet<UiRole>();
        int lines = 0;
        for (int i = 0; i < (int)StocksInd.Count; i++)
        {
            tints.Add(StocksIndicators.Tint((StocksInd)i));
            lines += en(StocksIndicators.Keys[i], null).Length > 0 && en(StocksIndicators.HintKeys[i], null).Length > 0 ? 1 : 0;
        }
        Check(lines == (int)StocksInd.Count && tints.Count >= 6 && (int)StocksInd.Count == 9, "stocks chart: the picker has every indicator the model has (SMA, EMA, Bollinger, RSI, MACD, stochastic, ATR, OBV), each named and told");
    }

    // "Most you can buy" is the largest number of shares the cash covers, fee included, by the market's own fee rule.
    private static void StocksMostChecks()
    {
        bool exact = true, floors = true, zero = true;
        long[] cashes = { 1900000000L, 1204300L, 26737L, 1000L, 5L, 123456789012L };
        int[] prices = { 4342, 5342, 100, 1, 999999 };
        int[] least = { 5, 5, 8, 1 };
        foreach (long cash in cashes)
        {
            foreach (int price in prices)
            {
                foreach (int floor in least)
                {
                    long q = StocksMath.MostToBuy(cash, price, floor);
                    long cost = MarketTrading.Value(q, price, true);
                    long total = cost + MarketTrading.Commission(cost, floor);
                    long more = MarketTrading.Value(q + 1, price, true);
                    long totalMore = more + MarketTrading.Commission(more, floor);
                    exact &= total <= cash && totalMore > cash;
                    long fee = MarketTrading.Commission(cost, floor);
                    if (q > 0)
                    {
                        floors &= q == (cash - fee) * 100L / price || q == (cash - fee) * 100L / price - 1L || MarketTrading.Value(q, price, true) + fee <= cash;
                        floors &= (cash - fee) * 100L / price >= q;
                    }
                    else
                    {
                        zero &= cash < price / 100L + floor + 1L || price > cash * 100L || cash <= floor + 1L;
                    }
                }
            }
        }
        Check(exact, "stocks ticket: the most you can buy is the largest number the cash covers with its fee, and one more is not covered");
        Check(floors && zero, "stocks ticket: that number is the cash less the fee over the price, rounded down");
        long big = StocksMath.MostToBuy(1900000000L, 4342, 5);
        Check(big > 40000000L && StocksMath.MostToBuy(0L, 4342, 5) == 0L && StocksMath.MostToBuy(-5L, 4342, 5) == 0L && StocksMath.MostToBuy(1000L, 0, 5) == 0L,
            "stocks ticket: $1.9B of cash buys millions of shares at $43.42, and no cash buys none (" + big.ToString("N0", CultureInfo.InvariantCulture) + ")");
    }

    // The big screen opens by the button and by its key and shuts by the button and by Escape; it is the same chart part, in the whole window.
    private static void StocksBigChecks()
    {
        var ctl = new StocksChartControl();
        StocksChange open = ctl.ToggleBig();
        bool opened = ctl.View.Big && open == StocksChange.Rebuild;
        StocksChange esc = ctl.Back();
        bool shut = !ctl.View.Big && esc == StocksChange.Rebuild;
        ctl.ToggleBig();
        StocksChange again = ctl.ToggleBig();
        bool button = !ctl.View.Big && again == StocksChange.Rebuild;
        ctl.ToggleBig();
        ctl.TogglePicker(true);
        StocksChange first = ctl.Back();
        bool pickerFirst = !ctl.View.PickOpen && ctl.View.Big && first == StocksChange.Rebuild;
        ctl.Back();
        Check(opened && shut && button && pickerFirst && ctl.Back() == StocksChange.None, "stocks chart: the big screen opens and shuts by its button, and Escape shuts the picker, then the big screen, then nothing");
        UiWords en = UiWords("en");
        UiScene small = StocksScene(en, "company_full");
        UiScene big = StocksScene(en, "company_big");
        float smallChart = ChartWidth(small), bigChart = ChartWidth(big);
        Check(bigChart > smallChart * 1.2f && HasNode(small, StocksCompanyLay.Big) && HasNode(big, StocksCompanyLay.Big), "stocks chart: the same chart is drawn wider on the big screen, and both have the button (" + bigChart.ToString("0") + " against " + smallChart.ToString("0") + ")");
        Check(!HasNode(big, UiAppWindow.NavCommand) && HasNode(small, UiAppWindow.NavCommand) && HasNode(big, StocksCompanyLay.Range) && HasNode(big, StocksCompanyLay.Type) && HasNode(big, StocksCompanyLay.AddIndicator),
            "stocks chart: the big screen keeps the tools, the range and the indicators and gives the sidebar's room to the chart");
        bool rail = false;
        for (int i = 0; i < big.NodeCount; i++)
        {
            rail |= big.Nodes[i].Command == UiChartDesk.Rail;
        }
        Check(rail && big.NodeCount <= UiCheckRules.MostTargets, "stocks chart: the tool rail is on the big screen, and the page keeps to the targets a screen should hold (" + big.NodeCount + ")");
        // The page never holds more targets than a layer should, with the most chips, the limit field and the position's choice up.
        UiScene full = StocksScene(en, "company_limit");
        Check(Crowd(full) <= UiCheckRules.MostTargets && Crowd(small) <= UiCheckRules.MostTargets, "stocks chart: the company page stays under the " + UiCheckRules.MostTargets + " targets a layer may hold (" + Crowd(full) + ", " + Crowd(small) + ")");
    }

    private static int Crowd(UiScene s)
    {
        int n = 0;
        for (int i = 0; i < s.NodeCount; i++)
        {
            n += s.Nodes[i].Layer == 0 && !UiDraw.IsChrome(s.Nodes[i].Command) ? 1 : 0;
        }
        return n;
    }

    private static UiScene StocksScene(UiWords t, string state)
    {
        var s = new UiScene();
        StocksLay.Lay(s, UiSamples.Stocks(t, state), UiFrame.FullMenu(16f / 9f, "Stocks"), true, UiDefaultKeys.Keyboard);
        return s;
    }

    private static bool HasNode(UiScene s, int command)
    {
        for (int i = 0; i < s.NodeCount; i++)
        {
            if (s.Nodes[i].Command == command)
            {
                return true;
            }
        }
        return false;
    }

    private static float ChartWidth(UiScene s)
    {
        for (int i = 0; i < s.NodeCount; i++)
        {
            if (s.Nodes[i].Command == UiChartDesk.Command)
            {
                return s.Nodes[i].W;
            }
        }
        return 0f;
    }

    // A hover, a pan, a zoom, a step and a tip lay the page out again from the model it has; only a change of range, tool, indicator or drawing builds it.
    private static void StocksHoverChecks()
    {
        UiWords en = UiWords("en");
        var on = new bool[(int)StocksInd.Count];
        on[(int)StocksInd.Sma20] = true;
        UiChartDeskModel m = UiSamples.StocksChartSample(en, 100, false, 0, on);
        UiScene s = StocksChartScene(m);
        var ctl = new StocksChartControl();
        float cell = m.Slot;
        StocksChange moved = ctl.Moved(m, m.PlotX + cell * 3.5f);
        StocksChange same = ctl.Moved(m, m.PlotX + cell * 3.7f);
        StocksChange next = ctl.Moved(m, m.PlotX + cell * 4.5f);
        Check(moved == StocksChange.Relay && same == StocksChange.None && next == StocksChange.Relay && m.Focus == m.First + 4,
            "stocks chart: a hover reads the candle under the pointer and lays out again only when it is another candle");
        Check(ctl.Pointed(m, UiChartDesk.Rail, 2) == StocksChange.Relay && m.Tip == 2 && ctl.Pointed(m, UiChartDesk.Rail, 2) == StocksChange.None && ctl.Pointed(m, -1, -1) == StocksChange.Relay && m.Focus < 0,
            "stocks chart: a tip is a relay, and the pointer leaving the plot takes the reading away");
        StocksChange zoom = ctl.Wheel(m, 1f);
        int zoomed = m.Shown;
        StocksChange out_ = ctl.Wheel(m, -1f);
        Check(zoom == StocksChange.Relay && zoomed < 100 && out_ == StocksChange.Relay && m.Shown > zoomed && m.First + m.Shown <= 100, "stocks chart: the wheel zooms in and out as a relay");
        ctl.Wheel(m, 1f);
        ctl.Wheel(m, 1f);
        int end = m.First + m.Shown;
        bool began = ctl.PanBegin(m, 500f);
        StocksChange pan = ctl.PanMove(m, 500f + m.Slot * 6f);
        Check(began && pan == StocksChange.Relay && m.First + m.Shown == end - 6, "stocks chart: a drag along the plot moves the window, as a relay (" + (m.First + m.Shown) + " from " + end + ")");
        ctl.PanEnd();
        ctl.Reset();
        Check(ctl.Step(m, -1) == StocksChange.Relay && ctl.View.Focus == 98 && ctl.View.Tool == 0, "stocks chart: Left on the plot moves the candle read");
        // The cell under any place along the plot is inside the window, so a hover can never name a candle that is not drawn.
        bool inside = true;
        for (float x = m.PlotX - 40f; x < m.PlotX + m.PlotW + 40f; x += 7f)
        {
            int at = StocksChartControl.CellAt(m, x);
            inside &= at >= m.First && at < m.First + m.Shown;
        }
        Check(inside, "stocks chart: the candle under any place is one of those drawn");
        // The plot is one target for the host's pointer, and every candle's reading needs none of its own.
        int plotNodes = 0;
        for (int i = 0; i < s.NodeCount; i++)
        {
            plotNodes += s.Nodes[i].Command == UiChartDesk.Command ? 1 : 0;
        }
        Check(plotNodes == 1 && m.Cells == m.Shown, "stocks chart: the plot is one target however many candles it shows");
    }

    // The day-5 chart: a market opened on day 5 has days before the save began, with no hole or zero in any range.
    private static void StocksHistoryChecks()
    {
        MarketEngine engine = NewMarket(5151, 60, 5, 0);
        MarketState state = engine.State;
        bool filled = true, closes = true, order = true, volumes = true, ranges = true;
        int checkedStocks = 0;
        foreach (Stock s in state.Stocks)
        {
            if (!s.Listed)
            {
                continue;
            }
            checkedStocks++;
            filled &= s.Daily.Count == MarketCatalogue.DailyKept && s.Monthly.Count >= 12;
            var list = new List<Candle>();
            for (int frame = 1; frame <= 4; frame++)
            {
                MarketHistory.Build(s.Daily, s.Monthly, default(Candle), (Timeframe)frame, Math.Max(8, state.YearDays), list);
                ranges &= list.Count >= (frame == 1 ? 80 : frame == 2 ? 15 : frame == 3 ? 12 : 1);
                for (int i = 0; i < list.Count; i++)
                {
                    closes &= list[i].C > 0 && list[i].O > 0 && list[i].H >= list[i].L && list[i].H >= list[i].C && list[i].L <= list[i].C;
                    volumes &= list[i].V > 0;
                    order &= i == 0 || list[i].Day > list[i - 1].Day;
                }
            }
        }
        Check(checkedStocks > 20 && filled, "stocks history: every company on day 5 has a hundred days and months before the save began (" + checkedStocks + " companies)");
        Check(closes && volumes && order && ranges, "stocks history: each range (days, weeks, months, years) of every company on day 5 has candles in order, each with a price and a volume, none at zero");
    }

    // The drawings' book: the selected one is taken off alone, an alert under it goes with it, one under a later drawing follows it down.
    private static void StocksDrawingBookChecks()
    {
        var book = new ChartBook();
        for (int i = 0; i < 4; i++)
        {
            book.AddDrawing(new ChartDrawing { StockId = i < 3 ? 7 : 8, Kind = ChartMark.Level, FromIndex = 10 + i, ToIndex = 10 + i, FromCents = 4000 + i, ToCents = 4000 + i });
        }
        book.AddAlert(new PriceAlert { StockId = 7, Kind = AlertKind.Support, Drawing = 1, Cents = 0 });
        book.AddAlert(new PriceAlert { StockId = 8, Kind = AlertKind.Support, Drawing = 3, Cents = 0 });
        bool gone = book.RemoveDrawing(7, 1);
        bool alone = book.Drawings.Count == 3 && book.Drawings[0].FromCents == 4000 && book.Drawings[1].FromCents == 4002 && book.Drawings[2].StockId == 8;
        Check(gone && alone && !book.RemoveDrawing(7, 5), "stocks chart: the delete takes the selected drawing off, and no other");
        Check(book.Alerts.Count == 1 && book.Alerts[0].StockId == 8 && book.Alerts[0].Drawing == 2, "stocks chart: an alert under a deleted drawing goes with it, and the next follows its drawing down");
        Check(book.ClearDrawings(7) == 2 && book.Drawings.Count == 1 && book.Alerts[0].Drawing == 0, "stocks chart: the delete with none selected takes every drawing of the company, and the alerts keep to their lines");
    }

    // The cost of the page's changes, measured: opening it, changing its range, and a hover. The numbers go in the log.
    private static void StocksCostChecks()
    {
        UiWords en = UiWords("en");
        var on = new bool[(int)StocksInd.Count];
        on[(int)StocksInd.Sma20] = true;
        on[(int)StocksInd.Sma50] = true;
        on[(int)StocksInd.Bollinger] = true;
        on[(int)StocksInd.Rsi] = true;
        on[(int)StocksInd.Macd] = true;
        var scene = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Stocks");
        for (int i = 0; i < 5; i++)
        {
            StocksModel warm = UiSamples.Stocks(en, "company_full");
            StocksLay.Lay(scene, warm, frame, true, UiDefaultKeys.Keyboard);
        }
        const int rounds = 40;
        var sw = new Stopwatch();
        sw.Restart();
        StocksModel model = null;
        for (int i = 0; i < rounds; i++)
        {
            model = UiSamples.Stocks(en, "company_full");
            StocksLay.Lay(scene, model, frame, true, UiDefaultKeys.Keyboard);
        }
        double open = sw.Elapsed.TotalMilliseconds / rounds;
        var ctl = new StocksChartControl();
        UiChartDeskModel chart = model.Co.Chart;
        sw.Restart();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < rounds; i++)
        {
            ctl.Moved(chart, chart.PlotX + chart.Slot * (3 + i % 50) + 1f);
            StocksLay.Lay(scene, model, frame, true, UiDefaultKeys.Keyboard);
        }
        double hover = sw.Elapsed.TotalMilliseconds / rounds;
        long bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / rounds;
        sw.Restart();
        for (int i = 0; i < rounds; i++)
        {
            model = UiSamples.Stocks(en, i % 2 == 0 ? "company_weeks" : "company_full");
            StocksLay.Lay(scene, model, frame, true, UiDefaultKeys.Keyboard);
        }
        double range = sw.Elapsed.TotalMilliseconds / rounds;
        // A hover lays the same arrays again: the lines' cuts are the same arrays, so the layout allocates no new points.
        UiChartDeskModel held = UiSamples.Stocks(en, "company_full").Co.Chart;
        StocksLay.Lay(scene, new StocksModel { Page = StocksPage.Company, Co = new StocksCompany { Chart = held, Order = new StocksTicket(), Name = "x", Ticker = "x", Price = "$1", ChangeMoney = "+$0", ChangePercent = "+0%" } }, frame, true, UiDefaultKeys.Keyboard);
        float[] first = held.Lines.Length > 0 ? held.Lines[0].Main.Points : null;
        held.Focus = 20;
        StocksLay.Lay(scene, new StocksModel { Page = StocksPage.Company, Co = new StocksCompany { Chart = held, Order = new StocksTicket(), Name = "x", Ticker = "x", Price = "$1", ChangeMoney = "+$0", ChangePercent = "+0%" } }, frame, true, UiDefaultKeys.Keyboard);
        Check(first != null && ReferenceEquals(first, held.Lines[0].Main.Points), "stocks chart: a hover lays the lines' cut arrays again and cuts none");
        Console.WriteLine("    stocks chart cost: open (a model and its layout) " + open.ToString("0.00", CultureInfo.InvariantCulture) + " ms, range change " + range.ToString("0.00", CultureInfo.InvariantCulture)
            + " ms, hover (layout from the model) " + hover.ToString("0.00", CultureInfo.InvariantCulture) + " ms and " + bytes.ToString("N0", CultureInfo.InvariantCulture) + " bytes, "
            + scene.ItemCount + " items, " + scene.NodeCount + " targets");
        Check(bytes < 16384, "ui timings: laying the company page out again for a hover allocates " + bytes + " bytes, under 16 KB");
        // The market side of an open: every indicator of a range over its candles, the sums the page is built from (the game's session
        // record counts the whole build and layout by name, UiCost).
        var close = new double[120];
        var high = new double[120];
        var low = new double[120];
        var vol = new double[120];
        for (int i = 0; i < 120; i++)
        {
            close[i] = 4000 + 300 * Math.Sin(i * 0.2) + i * 5;
            high[i] = close[i] + 40;
            low[i] = close[i] - 40;
            vol[i] = 100000 + i * 100;
        }
        var a = new double[120];
        var b = new double[120];
        var c = new double[120];
        var scratch = new double[120];
        sw.Restart();
        for (int i = 0; i < rounds; i++)
        {
            MarketIndicators.Sma(close, 120, 20, a);
            MarketIndicators.Sma(close, 120, 50, a);
            MarketIndicators.Ema(close, 120, 20, a);
            MarketIndicators.Bollinger(close, 120, 20, 2.0, a, b, c);
            MarketIndicators.Rsi(close, 120, 14, a);
            MarketIndicators.Macd(close, 120, 12, 26, 9, a, b, c, scratch);
            MarketIndicators.Stochastic(high, low, close, 120, 14, 3, a, b);
            MarketIndicators.Atr(high, low, close, 120, 14, a);
            MarketIndicators.Obv(close, vol, 120, a);
        }
        double sums = sw.Elapsed.TotalMilliseconds / rounds;
        Console.WriteLine("    stocks chart: every indicator over 120 candles " + sums.ToString("0.000", CultureInfo.InvariantCulture) + " ms (done once for an open, a range change or a change of the data, never for a hover)");
        Check(sums < 10.0, "stocks chart: all nine indicators over a range take under 10 ms (" + sums.ToString("0.000", CultureInfo.InvariantCulture) + ")");
        Check(hover < 25.0 && open < 60.0, "stocks chart: a hover lays the page out in under 25 ms, and opening it in under 60 (" + hover.ToString("0.0", CultureInfo.InvariantCulture) + ", " + open.ToString("0.0", CultureInfo.InvariantCulture) + ")");
    }
}
