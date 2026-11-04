using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A line's values in the window drawn, from its first value on, kept from one layout to the next: a hover or a click lays
    /// the same window again, so the array is cut once and a spark gets the same array each time. Cost: a cut when the window moves.
    /// </summary>
    public struct UiChartCut
    {
        public float[] Points;
        public int From, Count;           // the window the cut is of
        public int Start;                 // the candle of its first point (an average has no value before its window)
        public float Lo, Hi;              // its lowest and highest value

        /// <summary>The points of a series in a window, cut again only when the window or the series is not the one cut.</summary>
        public bool Fit(float[] src, int first, int shown)
        {
            if (src == null)
            {
                Points = null;
                return false;
            }
            if (Points != null && From == first && Count == shown && _source == src)
            {
                return Points.Length > 1;
            }
            From = first;
            Count = shown;
            _source = src;
            int start = first, end = Math.Min(src.Length, first + shown);
            while (start < end && float.IsNaN(src[start]))
            {
                start++;
            }
            int len = Math.Max(0, end - start);
            if (Points == null || Points.Length != len)
            {
                Points = new float[len];
            }
            Lo = float.MaxValue;
            Hi = float.MinValue;
            for (int i = 0; i < len; i++)
            {
                float v = src[start + i];
                Points[i] = float.IsNaN(v) ? Points[Math.Max(0, i - 1)] : v;
                Lo = Math.Min(Lo, Points[i]);
                Hi = Math.Max(Hi, Points[i]);
            }
            Start = start;
            return len > 1;
        }

        private float[] _source;
    }

    /// <summary>
    /// One line over the price, or in a pane. NaN until the average has a window. A band (Bollinger) has a second line under
    /// the first and a tint between them. The cut is what the last layout drew of the window, kept so a hover lays the same
    /// arrays again and allocates nothing.
    /// </summary>
    public struct UiChartLine
    {
        public string Name;
        public float[] Values, Under;
        public UiRole Tint;
        public bool Tinted;
        public StocksInd Ind;
        public UiChartCut Main, Low;      // what the last layout drew of the window, for each line
    }

    /// <summary>A pane under the price: RSI, MACD and the rest. Its own scale, its own guides and the figures its legend reads.</summary>
    public struct UiChartPane
    {
        public string Name;
        public float[] Values, Second, Histogram;
        public UiRole Tint, SecondTint;
        public float[] Guides;            // levels drawn across it (70 and 30)
        public bool Fixed;                // a scale of 0 to 100
        public string Format;             // the figures' format ("0.0")
        public StocksInd Ind;
        public UiChartCut Main, Next, Bars;
    }

    /// <summary>What a drawing is. Saved as the market's own ChartMark number.</summary>
    public enum UiChartMarkKind : byte
    {
        Trend = 0,
        Level,
        Fibonacci,
        Ray,
        Rectangle,
        Measure,
        Note
    }

    /// <summary>A drawing, in candle index (of the whole set) and price in dollars. Drawn clipped to the price plot.</summary>
    public struct UiChartMark
    {
        public float FromX, ToX, FromY, ToY;
        public UiChartMarkKind Kind;
        public UiRole Tint;
        public string Label;              // a measure's move, a note's price, a level's price
        public bool Selected;
        public int Ordinal;               // which of the company's drawings it is, in the order they were made
    }

    /// <summary>
    /// A company's chart: candles, a line or an area, lines over them, a volume pane and up to two more, a last price tag,
    /// the drawings and the tool rail. The whole set of candles is in the model and the window (First, Shown) says which are
    /// drawn, so moving along or zooming only lays it out again. Plain data. Words are already the player's.
    /// </summary>
    public sealed class UiChartDeskModel
    {
        public int Kind;                  // 0 candles, 1 line, 2 area
        public string Head;               // "ARE, Days": the first words of the legend line
        public float[] Open, High, Low, Close, Volume;   // dollars
        public int[] Day;                 // each candle's first game day, 0 where it has none (a bar of the day)
        public string[] Stamp;            // each candle's date words: "Day 283", "09:30"
        public UiChartLine[] Lines = new UiChartLine[0];
        public UiChartPane[] Panes = new UiChartPane[0];
        public UiChartMark[] Marks = new UiChartMark[0];
        public int First, Shown;          // the window: the first candle drawn and how many
        public int Focus = -1;            // the candle the pointer or the keys rest on, or -1
        public int Tool;                  // the tool in hand (UiChartTool)
        public int Tip = -1;              // the rail's button whose name shows: a tool, or UiChartTools.Count for the delete
        public string[] ToolNames = new string[0];
        public string DeleteName;         // "Delete this drawing" or "Delete every drawing", whichever the delete does now
        public int Anchor = -1;           // a two click tool's first click: its candle...
        public float AnchorY;             // ...and price
        public Func<float, string> Price; // a price as the game writes it
        public string LastTag;            // the last price, on the axis
        public bool LastUp = true;
        public string VolumeLabel, DayLabel, OpenLabel, HighLabel, LowLabel, CloseLabel;
        public string EmptyTitle, EmptyBody;
        public UiChartCut CloseCut;       // the closes in the window, for a line or an area

        // Written back by the layout, for the screen's pointer: where the plot stands and what it shows.
        public float PlotX, PlotY, PlotW, PriceH, PlotLo, PlotHi, Slot;
        public short PlotClip = -1;
        public int Cells;                 // targets laid: one a candle drawn
        public int MarkFrom, MarkTo;      // the scene's items that are drawings: from this one up to (not including) that
    }

    /// <summary>
    /// The company chart, a block of the desk and of its big screen: a rail of drawing tools at the left, a legend line over
    /// the plot, the candles with their lines, the volume and the panes under them, the price axis in dollars at the right
    /// with the last price on it, and the days along the foot. Every candle is a target that reads it in a card by the mouse
    /// or the keys; the plot is a scroll region of its own, so the wheel over it zooms and nothing drawn can leave it.
    /// Cost: a layout a change; a hover lays the same arrays again and allocates nothing but its words.
    /// </summary>
    public static partial class UiChartDesk
    {
        public const int Command = 20;          // a candle: the argument is its index in the whole set
        public const int Rail = 21;             // a tool of the rail; the argument UiChartTools.Count is the delete
        public const float RailWidth = UiTokens.ControlSmall;
        public const int LeastShown = 12;       // candles the window may be zoomed in to
        private const float VolumeShare = 0.11f, PaneShare = 0.13f;
        private const float LeastPrice = 168f;  // px: the price plot's least height, or a pane gives way

        /// <summary>Lays the chart in the box. Returns the node of the plot's first candle, or -1.</summary>
        public static int Lay(UiScene s, UiAt at, UiChartDeskModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(band);
            m.Cells = 0;
            m.PlotClip = -1;
            m.MarkFrom = m.MarkTo = s.ItemCount;
            int n = m.Close != null ? m.Close.Length : 0;
            float railX = x + UiTokens.S2;
            float railY = y + UiTokens.S2, railH = h - UiTokens.S2 * 2f;
            RailTools(s, on, m, railX, railY, railH);
            float ix = railX + RailWidth + UiTokens.S3, iw = x + w - UiTokens.S4 - ix;
            float top = y + UiTokens.S2;
            float foot = y + h - UiTokens.S2;
            if (n == 0)
            {
                UiDraw.Empty(s, on, ix, top, iw, foot - top, m.EmptyTitle, m.EmptyBody);
                Tip(s, on, m, railX, railY, railH);
                return -1;
            }
            int first = Math.Max(0, Math.Min(m.First, n - 1));
            int shown = Math.Max(1, Math.Min(m.Shown <= 0 ? n : m.Shown, n - first));
            m.First = first;
            m.Shown = shown;
            float legendH = UiTokens.PairRow;
            float axisH = UiTokens.PairRow + UiTokens.S1;
            float plotTop = top + legendH + UiTokens.S1;
            float plotFoot = foot - axisH;
            Range(m, first, shown, out float lo, out float hi);
            float axisW = AxisWidth(m, lo, hi);
            float px = ix, pw = UiDraw.SnapDown(iw - axisW);
            float stack = plotFoot - plotTop;
            int panes = Math.Min(2, m.Panes != null ? m.Panes.Length : 0);
            float volH = m.Volume != null ? UiDraw.SnapDown(Math.Max(UiTokens.S12, stack * VolumeShare)) : 0f;
            float paneH = UiDraw.SnapDown(Math.Max(UiTokens.S16, stack * PaneShare));
            while (panes > 0 && stack - volH - panes * (paneH + UiTokens.S2) - (volH > 0f ? UiTokens.S2 : 0f) < LeastPrice)
            {
                panes--;
            }
            float priceH = stack - (volH > 0f ? volH + UiTokens.S2 : 0f) - panes * (paneH + UiTokens.S2);
            float slot = pw / shown;
            m.PlotX = px;
            m.PlotY = plotTop;
            m.PlotW = pw;
            m.PriceH = priceH;
            m.PlotLo = lo;
            m.PlotHi = hi;
            m.Slot = slot;
            Legend(s, on, m, ix, top, iw, legendH, first, shown);
            Axis(s, on, m, px, plotTop, pw, priceH, axisW, lo, hi);
            short clip = s.AddClip(px, plotTop, pw, stack, 0f);
            s.Clips[clip].ContentHeight = stack;
            m.PlotClip = clip;
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = on.Layer, Order = on.Order, Region = on.Region };
            int plot = UiDraw.Box(s, inClip, 0f, 0f, pw, stack, UiRole.None, UiRole.None, 0f, UiFlags.Share);
            UiAt p = inClip.In(plot);
            Grid(s, p, m, pw, priceH, lo, hi);
            Candles(s, p, m, first, shown, slot, priceH, lo, hi);
            if (volH > 0f)
            {
                Volume(s, p, m, first, shown, slot, priceH + UiTokens.S2, volH);
            }
            Overlays(s, p, m, first, shown, slot, priceH, lo, hi);
            float py = priceH + UiTokens.S2 + (volH > 0f ? volH + UiTokens.S2 : 0f);
            for (int k = 0; k < panes; k++)
            {
                PanePlot(s, p, m, ref m.Panes[k], first, shown, slot, pw, py, paneH);
                py += paneH + UiTokens.S2;
            }
            m.MarkFrom = s.ItemCount;
            Marks(s, p, m, first, slot, pw, priceH, lo, hi);
            m.MarkTo = s.ItemCount;
            LastLine(s, p, m, first, shown, slot, pw, priceH, lo, hi);
            Crosshair(s, p, m, first, shown, slot, stack, priceH, lo, hi);
            int firstNode = CellTargets(s, p, m, first, shown, slot, stack);
            // The words over the plot come after it, so they stand on top of it.
            SideWords(s, on, m, px, plotTop, pw, priceH, volH, paneH, panes, axisW, lo, hi, first, shown);
            TimeAxis(s, on, m, px, plotFoot + UiTokens.S1, pw, slot, first, shown);
            LastTag(s, on, m, px + pw + UiTokens.S1, plotTop, axisW - UiTokens.S1, priceH, lo, hi);
            Reading(s, on, m, px, plotTop, pw, stack, slot, first, priceH, lo, hi, x, w);
            Tip(s, on, m, railX, railY, railH);
            return firstNode;
        }

        // The price range of the window, the lines over it counted, padded; never flat.
        private static void Range(UiChartDeskModel m, int first, int shown, out float lo, out float hi)
        {
            lo = float.MaxValue;
            hi = float.MinValue;
            for (int i = first; i < first + shown; i++)
            {
                float low = m.Low != null && m.Kind == 0 ? m.Low[i] : m.Close[i];
                float high = m.High != null && m.Kind == 0 ? m.High[i] : m.Close[i];
                lo = Math.Min(lo, low);
                hi = Math.Max(hi, high);
            }
            for (int k = 0; m.Lines != null && k < m.Lines.Length; k++)
            {
                Widen(m.Lines[k].Values, first, shown, ref lo, ref hi);
                Widen(m.Lines[k].Under, first, shown, ref lo, ref hi);
            }
            if (hi - lo < 0.02f)
            {
                hi += 0.5f;
                lo = Math.Max(0f, lo - 0.5f);
            }
            float pad = (hi - lo) * 0.06f;
            lo = Math.Max(0f, lo - pad);
            hi += pad;
        }

        private static void Widen(float[] v, int first, int shown, ref float lo, ref float hi)
        {
            for (int i = first; v != null && i < first + shown && i < v.Length; i++)
            {
                if (float.IsNaN(v[i]))
                {
                    continue;
                }
                lo = Math.Min(lo, v[i]);
                hi = Math.Max(hi, v[i]);
            }
        }

        /// <summary>A price's height in the price plot: its foot at the plot's height, the top at 0.</summary>
        public static float PriceY(float v, float lo, float hi, float h) => h - (v - lo) / Math.Max(1e-4f, hi - lo) * h;

        /// <summary>The price at a height of the price plot, the inverse of <see cref="PriceY"/>.</summary>
        public static float PriceAt(float y, float lo, float hi, float h) => lo + (h - y) / Math.Max(1f, h) * (hi - lo);

        private static string Money(UiChartDeskModel m, float v) => m.Price != null ? m.Price(v) : "$" + v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        // The axis' ticks are round prices, so each is a figure a person would write.
        private static void Ticks(float lo, float hi, out float step, out float firstTick)
        {
            step = UiChart.NiceStep(hi - lo, 5);
            firstTick = (float)Math.Ceiling(lo / step - 1e-4) * step;
        }

        private static float AxisWidth(UiChartDeskModel m, float lo, float hi)
        {
            Ticks(lo, hi, out float step, out float tick);
            float most = m.LastTag != null ? UiTextRules.Width(m.LastTag, UiStyle.SmallStrong) + UiTokens.S3 : 0f;
            for (float v = tick; v <= hi && step > 0f; v += step)
            {
                most = Math.Max(most, UiTextRules.Width(Money(m, v), UiStyle.Small));
            }
            return UiDraw.Snap(most + UiTokens.S3);
        }

        private static float RailStep(float h)
        {
            float step = RailWidth + UiTokens.S1;
            return (UiChartTools.Count + 1) * step > h ? Math.Max(UiTokens.MinTarget, (h - RailWidth) / UiChartTools.Count) : step;
        }

        // The rail: the nine tools down the left, the delete at its foot. Each tool is a target; the chosen one is cyan.
        private static void RailTools(UiScene s, UiAt at, UiChartDeskModel m, float x, float y, float h)
        {
            float step = RailStep(h);
            for (int i = 0; i < UiChartTools.Count; i++)
            {
                UiDraw.IconButton(s, at, x, y + i * step, RailWidth, UiChartTools.Icons[i], UiRole.None, Rail, i, i == m.Tool);
            }
            UiDraw.IconButton(s, at, x, y + h - RailWidth, RailWidth, UiIcon.Trash, UiRole.None, Rail, UiChartTools.Count);
        }

        // The name of the rail button the pointer rests on, in a card beside it.
        private static void Tip(UiScene s, UiAt at, UiChartDeskModel m, float x, float y, float h)
        {
            if (m.Tip < 0 || m.Tip > UiChartTools.Count)
            {
                return;
            }
            string words = m.Tip == UiChartTools.Count ? m.DeleteName : m.ToolNames != null && m.Tip < m.ToolNames.Length ? m.ToolNames[m.Tip] : null;
            if (string.IsNullOrEmpty(words))
            {
                return;
            }
            float by = m.Tip == UiChartTools.Count ? y + h - RailWidth : y + m.Tip * RailStep(h);
            float tw = UiDraw.Snap(UiTextRules.Width(words, UiStyle.SmallStrong) + UiTokens.S4);
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SmallStrong) + UiTokens.S3);
            var over = new UiAt { Parent = at.Parent, Clip = -1, Layer = 1, Order = -1, Region = at.Region };
            float cx = x + RailWidth + UiTokens.S2;
            float cy = UiDraw.Snap(by + (RailWidth - th) / 2f);
            int card = UiDraw.Box(s, over, cx, cy, tw, th, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Decor);
            UiDraw.TextMiddle(s, over.In(card), cx + UiTokens.S2, cy, th, tw - UiTokens.S4, UiStyle.SmallStrong, UiRole.Text, words, UiFlags.Truncate);
        }
    }
}
