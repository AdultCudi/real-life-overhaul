using System;
using System.Globalization;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The chart's plot: the grid, the candles, the volume, the lines over them and the panes under them. Game-free.</summary>
    public static partial class UiChartDesk
    {
        private static readonly float[] Rising = { 0f, 1f }, Falling = { 1f, 0f }, Flat = { 0f, 0f };   // a drawing's segment as a spark: only its slope matters
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>A count shortened as a trader reads it: 148.2K, 1.4M, 2.6B.</summary>
        public static string Compact(double v)
        {
            double a = Math.Abs(v);
            if (a >= 1e9)
            {
                return (v / 1e9).ToString("0.0", Inv) + "B";
            }
            if (a >= 1e6)
            {
                return (v / 1e6).ToString("0.0", Inv) + "M";
            }
            if (a >= 1e3)
            {
                return (v / 1e3).ToString("0.0", Inv) + "K";
            }
            return v.ToString("0", Inv);
        }

        // A line of one array in a box, from its first point to its last. The box is widened by the stroke's half each way, so the
        // spark's inset puts its ends on the points.
        private static void Spark(UiScene s, UiAt at, float[] points, float x, float y, float w, float h, UiRole tint, float alpha)
        {
            float t = UiTokens.Hairline;
            var seg = new UiItem
            {
                Kind = UiKind.Spark, Fill = tint, X = x - t, Y = y - t, W = w + t * 2f, H = h + t * 2f, Points = points, Flags = UiFlags.Decor,
                Clip = at.Clip, Node = -1, Layer = at.Layer, Order = -1, Parent = at.Parent, Alpha = alpha
            };
            s.Add(ref seg);
        }

        // One window's line over the price, or in a pane (lo and hi are the scale of the pane that draws it).
        private static void CutLine(UiScene s, UiAt at, ref UiChartCut cut, float slot, int first, float top, float h, float lo, float hi, UiRole tint, float alpha)
        {
            if (cut.Points == null || cut.Points.Length < 2)
            {
                return;
            }
            float y0 = top + PriceY(cut.Hi, lo, hi, h), y1 = top + PriceY(cut.Lo, lo, hi, h);
            Spark(s, at, cut.Points, slot * (cut.Start - first + 0.5f), y0, slot * (cut.Points.Length - 1), Math.Max(0f, y1 - y0), tint, alpha);
        }

        private static void Grid(UiScene s, UiAt at, UiChartDeskModel m, float pw, float h, float lo, float hi)
        {
            Ticks(lo, hi, out float step, out float tick);
            for (int k = 0; TickAt(tick, step, hi, k, out float v); k++)
            {
                var rule = new UiItem
                {
                    Kind = UiKind.Box, Flags = UiFlags.Decor, Fill = UiRole.Rule, X = 0f, Y = PriceY(v, lo, hi, h), W = pw, H = UiTokens.Hairline,
                    Clip = at.Clip, Node = -1, Layer = at.Layer, Order = -1, Parent = at.Parent, Alpha = 0.6f
                };
                s.Add(ref rule);
            }
        }

        private static void Candles(UiScene s, UiAt at, UiChartDeskModel m, int first, int shown, float slot, float h, float lo, float hi)
        {
            if (m.Kind != 0)
            {
                if (m.CloseCut.Fit(m.Close, first, shown))
                {
                    CutLine(s, at, ref m.CloseCut, slot, first, 0f, h, lo, hi, UiRole.Accent, 1f);
                }
                if (m.Kind == 2)
                {
                    // The columns of the fill stand in the scroll region with no box of their own to hug the edge of: they are a tint under a line.
                    var loose = new UiAt { Parent = -1, Clip = at.Clip, Layer = at.Layer, Order = -1, Region = at.Region };
                    for (int i = first; i < first + shown; i++)
                    {
                        if (!UiChart.Real(m.Close[i]))
                        {
                            continue;
                        }
                        float y = PriceY(m.Close[i], lo, hi, h);
                        Fill(s, loose, slot * (i - first), y, slot, h - y, UiRole.Accent, 0.16f);
                    }
                }
                return;
            }
            float body = Math.Max(1f, slot * 0.6f);
            for (int i = first; i < first + shown; i++)
            {
                float close = m.Close[i];
                if (!UiChart.Real(close))
                {
                    continue;   // a day whose close is no number has no candle
                }
                float open = m.Open != null && UiChart.Real(m.Open[i]) ? m.Open[i] : close;
                UiRole tone = close >= open ? UiRole.Ok : UiRole.Danger;
                float cx = slot * (i - first) + slot * 0.5f;
                float yH = PriceY(m.High != null && UiChart.Real(m.High[i]) ? m.High[i] : Math.Max(open, close), lo, hi, h);
                float yL = PriceY(m.Low != null && UiChart.Real(m.Low[i]) ? m.Low[i] : Math.Min(open, close), lo, hi, h);
                Fill(s, at, cx - UiTokens.Hairline / 2f, yH, UiTokens.Hairline, Math.Max(UiTokens.Hairline, yL - yH), tone, 1f);
                float yO = PriceY(open, lo, hi, h), yC = PriceY(close, lo, hi, h);
                Fill(s, at, cx - body / 2f, Math.Min(yO, yC), body, Math.Max(UiTokens.Hairline, Math.Abs(yC - yO)), tone, 1f);
            }
        }

        private static void Fill(UiScene s, UiAt at, float x, float y, float w, float h, UiRole fill, float alpha)
        {
            var box = new UiItem
            {
                Kind = UiKind.Box, Flags = UiFlags.Decor, Fill = fill, X = x, Y = y, W = w, H = h, Clip = at.Clip, Node = -1, Layer = at.Layer, Order = -1,
                Parent = at.Parent, Alpha = alpha
            };
            s.Add(ref box);
        }

        private static void Volume(UiScene s, UiAt at, UiChartDeskModel m, int first, int shown, float slot, float top, float h)
        {
            float most = 1f;
            for (int i = first; i < first + shown && i < m.Volume.Length; i++)
            {
                most = UiChart.Real(m.Volume[i]) ? Math.Max(most, m.Volume[i]) : most;
            }
            float body = Math.Max(1f, slot * 0.6f);
            for (int i = first; i < first + shown && i < m.Volume.Length; i++)
            {
                if (!UiChart.Real(m.Volume[i]))
                {
                    continue;
                }
                float open = m.Open != null ? m.Open[i] : m.Close[i];
                bool up = m.Kind == 0 ? m.Close[i] >= open : i == 0 || m.Close[i] >= m.Close[i - 1];
                float vh = Math.Max(UiTokens.Hairline, Math.Max(0f, m.Volume[i]) / most * (h - UiTokens.S4));
                Fill(s, at, slot * (i - first) + (slot - body) / 2f, top + h - vh, body, vh, up ? UiRole.Ok : UiRole.Danger, 0.55f);
            }
        }

        private static void Overlays(UiScene s, UiAt at, UiChartDeskModel m, int first, int shown, float slot, float h, float lo, float hi)
        {
            for (int k = 0; m.Lines != null && k < m.Lines.Length; k++)
            {
                ref UiChartLine line = ref m.Lines[k];
                if (line.Tinted && line.Values != null && line.Under != null)
                {
                    for (int i = first; i < first + shown && i < line.Values.Length && i < line.Under.Length; i++)
                    {
                        if (!UiChart.Real(line.Values[i]) || !UiChart.Real(line.Under[i]))
                        {
                            continue;
                        }
                        float yu = PriceY(line.Values[i], lo, hi, h), yl = PriceY(line.Under[i], lo, hi, h);
                        Fill(s, at, slot * (i - first), yu, slot, Math.Max(0f, yl - yu), line.Tint, 0.1f);
                    }
                }
                if (line.Main.Fit(line.Values, first, shown))
                {
                    CutLine(s, at, ref line.Main, slot, first, 0f, h, lo, hi, line.Tint, 1f);
                }
                if (line.Low.Fit(line.Under, first, shown))
                {
                    CutLine(s, at, ref line.Low, slot, first, 0f, h, lo, hi, line.Tint, 1f);
                }
            }
        }

        // A pane under the price: its own scale, its guides, the bars of a histogram and its lines.
        private static void PanePlot(UiScene s, UiAt at, UiChartDeskModel m, ref UiChartPane pane, int first, int shown, float slot, float pw, float top, float h)
        {
            Fill(s, at, 0f, top, pw, h, UiRole.Panel, 0.3f);
            bool main = pane.Main.Fit(pane.Values, first, shown);
            bool next = pane.Next.Fit(pane.Second, first, shown);
            bool bars = pane.Bars.Fit(pane.Histogram, first, shown);
            PaneScale(ref pane, main, next, bars, out float lo, out float hi);
            float inset = UiTokens.S2, room = h - inset * 2f;
            for (int g = 0; pane.Guides != null && g < pane.Guides.Length; g++)
            {
                Fill(s, at, 0f, top + inset + (1f - (pane.Guides[g] - lo) / (hi - lo)) * room, pw, UiTokens.Hairline, UiRole.Rule, 0.9f);
            }
            if (pane.Guides != null && pane.Guides.Length == 2)
            {
                float yTop = top + inset + (1f - (Math.Max(pane.Guides[0], pane.Guides[1]) - lo) / (hi - lo)) * room;
                float yBottom = top + inset + (1f - (Math.Min(pane.Guides[0], pane.Guides[1]) - lo) / (hi - lo)) * room;
                Fill(s, at, 0f, yTop, pw, Math.Max(0f, yBottom - yTop), pane.Tint, 0.07f);
            }
            if (bars)
            {
                float zero = top + inset + (1f - (0f - lo) / (hi - lo)) * room, body = Math.Max(1f, slot * 0.6f);
                for (int i = 0; i < pane.Bars.Points.Length; i++)
                {
                    float v = pane.Bars.Points[i];
                    float y = top + inset + (1f - (v - lo) / (hi - lo)) * room;
                    Fill(s, at, slot * (pane.Bars.Start + i - first) + (slot - body) / 2f, Math.Min(y, zero), body, Math.Max(UiTokens.Hairline, Math.Abs(y - zero)),
                        v >= 0f ? UiRole.Ok : UiRole.Danger, 0.6f);
                }
            }
            if (main)
            {
                CutLine(s, at, ref pane.Main, slot, first, top + inset, room, lo, hi, pane.Tint, 1f);
            }
            if (next)
            {
                CutLine(s, at, ref pane.Next, slot, first, top + inset, room, lo, hi, pane.SecondTint, 1f);
            }
        }

        // A pane's scale: 0 to 100 where it is fixed, else what its lines and bars cover with a little room (zero kept in view for bars).
        private static void PaneScale(ref UiChartPane pane, bool main, bool next, bool bars, out float lo, out float hi)
        {
            if (pane.Fixed)
            {
                lo = 0f;
                hi = 100f;
                return;
            }
            lo = float.MaxValue;
            hi = float.MinValue;
            if (main)
            {
                lo = Math.Min(lo, pane.Main.Lo);
                hi = Math.Max(hi, pane.Main.Hi);
            }
            if (next)
            {
                lo = Math.Min(lo, pane.Next.Lo);
                hi = Math.Max(hi, pane.Next.Hi);
            }
            if (bars)
            {
                lo = Math.Min(lo, Math.Min(0f, pane.Bars.Lo));
                hi = Math.Max(hi, Math.Max(0f, pane.Bars.Hi));
            }
            if (lo > hi)
            {
                lo = 0f;
                hi = 1f;
            }
            if (hi - lo < 1e-4f)
            {
                hi += 1f;
                lo -= 1f;
            }
            float pad = (hi - lo) * 0.08f;
            lo -= pad;
            hi += pad;
        }

        // The last price: a hairline across the plot at its height, in the colour of the day's move.
        private static void LastLine(UiScene s, UiAt at, UiChartDeskModel m, int first, int shown, float slot, float pw, float h, float lo, float hi)
        {
            int last = m.Close.Length - 1;
            if (last < first || last >= first + shown || m.LastTag == null || !UiChart.Real(m.Close[last]))
            {
                return;
            }
            Fill(s, at, 0f, PriceY(m.Close[last], lo, hi, h), pw, UiTokens.Hairline, m.LastUp ? UiRole.Ok : UiRole.Danger, 0.7f);
        }

        // The candle the pointer rests on: a rule down the plot, a rule across at its close and a dot where they cross.
        private static void Crosshair(UiScene s, UiAt at, UiChartDeskModel m, int first, int shown, float slot, float stack, float h, float lo, float hi)
        {
            int f = m.Focus;
            if (f < first || f >= first + shown)
            {
                return;
            }
            float cx = slot * (f - first) + slot * 0.5f;
            Fill(s, at, cx - UiTokens.Hairline / 2f, 0f, UiTokens.Hairline, stack, UiRole.Text2, 0.55f);
            if (!UiChart.Real(m.Close[f]))
            {
                return;   // the rule down the plot still says which day, and there is no price to cross it at
            }
            float y = PriceY(m.Close[f], lo, hi, h);
            Fill(s, at, 0f, y, slot * shown, UiTokens.Hairline, UiRole.Text2, 0.45f);
            var dot = new UiItem
            {
                Kind = UiKind.Box, Flags = UiFlags.Decor | UiFlags.Circle, Fill = UiRole.Text, Radius = UiTokens.S1, X = cx - UiTokens.S1, Y = y - UiTokens.S1, W = UiTokens.S2, H = UiTokens.S2,
                Clip = at.Clip, Node = -1, Layer = at.Layer, Order = -1, Parent = at.Parent, Alpha = 1f
            };
            s.Add(ref dot);
        }

        // The plot is one target: it reads the pointer through the host (IUiTracking), so the candles take no targets of their own.
        private static int CellTargets(UiScene s, UiAt at, UiChartDeskModel m, int first, int shown, float slot, float stack)
        {
            m.Cells = shown;
            return UiDraw.Node(s, at, 0f, 0f, slot * shown, stack, Command, 0, at.Parent);
        }
    }
}
