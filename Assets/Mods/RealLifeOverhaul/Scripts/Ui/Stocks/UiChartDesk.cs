using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One line over the candles, or a pane under them. NaN until the average has a window.</summary>
    public struct UiChartLine
    {
        public string Name;
        public float[] Values;
        public UiRole Tint;
    }

    /// <summary>A drawing in candle index and price, already the values the candles use.</summary>
    public struct UiChartMark
    {
        public float FromX, ToX, FromY, ToY;
        public UiRole Tint;
    }

    /// <summary>
    /// A company's chart: candles, a line or an area, several lines over them, up to two panes, a last price tag and
    /// the drawings. Laid when the set or the candle changes. Flat fills, the kit's good and bad for up and down.
    /// Game-free. Cost: a layout a change.
    /// </summary>
    public sealed class UiChartDeskModel
    {
        public int Kind;                 // 0 candles, 1 line, 2 area
        public string Title;
        public float[] Open, High, Low, Close, Volume;
        public UiChartLine[] Lines = new UiChartLine[0];
        public UiChartLine[] Panes = new UiChartLine[0];
        public UiChartMark[] Marks = new UiChartMark[0];
        public int Focus = -1;
        public string Read;
        public string LastTag;
        public bool LastUp = true;
        public string EmptyTitle, EmptyBody;
    }

    /// <summary>The company chart, a block of the desk. One target, the reading; the candles are decoration.</summary>
    public static class UiChartDesk
    {
        public const int Command = 20;

        /// <summary>Lays the chart in the box. Returns the reading's target, or -1.</summary>
        public static int Lay(UiScene s, UiAt at, UiChartDeskModel m, float x, float y, float w, float h)
        {
            int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(band);
            int n = m.Close != null ? m.Close.Length : 0;
            float ix = x + UiTokens.S4, iy = y + UiTokens.S3, iw = w - UiTokens.S4 * 2f;
            UiDraw.TextMiddle(s, on, ix, iy, UiTokens.PairRow, iw, UiStyle.SectionHeader, UiRole.Text, m.Title, UiFlags.Label | UiFlags.Truncate);
            float top = iy + UiTokens.PairRow + UiTokens.S2;
            float foot = y + h - UiTokens.S3;
            if (n == 0)
            {
                UiDraw.Empty(s, on, ix, top, iw, foot - top, m.EmptyTitle, m.EmptyBody);
                return -1;
            }
            int panes = m.Panes != null ? Math.Min(2, m.Panes.Length) : 0;
            float paneH = panes > 0 ? UiDraw.SnapDown((foot - top) * 0.22f) : 0f;
            float plotFoot = foot - panes * (paneH + UiTokens.S2) - UiTokens.PairRow - UiTokens.S4;
            Range(m, n, out float lo, out float hi);
            float axis = UiTokens.S16;
            float px = ix + axis, pw = UiDraw.SnapDown(iw - axis);
            float ph = plotFoot - top;
            Axis(s, on, m, ix, axis, top, ph, lo, hi);
            float slot = pw / Math.Max(1, n);
            float volH = m.Volume != null ? UiDraw.SnapDown(ph * 0.18f) : 0f;
            float priceH = ph - (volH > 0f ? volH + UiTokens.S2 : 0f);
            Plot(s, on, m, n, px, slot, top, priceH, lo, hi, volH);
            Lines(s, on, m.Lines, n, px, slot, top, priceH, lo, hi);
            Marks(s, on, m, px, slot, top, priceH, lo, hi, n);
            if (!string.IsNullOrEmpty(m.LastTag))
            {
                float ly = Y(m.Close[n - 1], lo, hi, top, priceH);
                UiDraw.TextMiddle(s, on, px + pw - UiTokens.S16 * 2f, ly - UiTokens.PairRow / 2f, UiTokens.PairRow, UiTokens.S16 * 2f, UiStyle.SmallStrong,
                    m.LastUp ? UiRole.OkText : UiRole.DangerText, m.LastTag, UiFlags.Right | UiFlags.Middle | UiFlags.Truncate | UiFlags.Decor);
            }
            float py = plotFoot + UiTokens.S2;
            for (int p = 0; p < panes; p++)
            {
                Pane(s, on, m.Panes[p], n, px, slot, py, paneH, axis, ix);
                py += paneH + UiTokens.S2;
            }
            int focus = m.Focus >= 0 && m.Focus < n ? m.Focus : n - 1;
            if (!string.IsNullOrEmpty(m.Read))
            {
                UiDraw.Text(s, on, ix, foot - UiTokens.PairRow, iw, UiStyle.Small, UiRole.Text2, m.Read, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
            }
            return UiDraw.Node(s, on, px, top, pw, priceH, Command, focus, band);
        }

        private static void Range(UiChartDeskModel m, int n, out float lo, out float hi)
        {
            lo = float.MaxValue;
            hi = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                float low = m.Low != null ? m.Low[i] : m.Close[i];
                float high = m.High != null ? m.High[i] : m.Close[i];
                if (low < lo)
                {
                    lo = low;
                }
                if (high > hi)
                {
                    hi = high;
                }
            }
            for (int k = 0; m.Lines != null && k < m.Lines.Length; k++)
            {
                float[] v = m.Lines[k].Values;
                for (int i = 0; v != null && i < v.Length && i < n; i++)
                {
                    if (float.IsNaN(v[i]))
                    {
                        continue;
                    }
                    if (v[i] < lo)
                    {
                        lo = v[i];
                    }
                    if (v[i] > hi)
                    {
                        hi = v[i];
                    }
                }
            }
            if (hi - lo < 1f)
            {
                hi += 1f;
                lo = Math.Max(0f, lo - 1f);
            }
            float pad = (hi - lo) * 0.06f;
            lo = Math.Max(0f, lo - pad);
            hi += pad;
        }

        private static float Y(float v, float lo, float hi, float top, float h) => top + h - (v - lo) / Math.Max(1e-4f, hi - lo) * h;

        private static void Axis(UiScene s, UiAt at, UiChartDeskModel m, float x, float w, float top, float h, float lo, float hi)
        {
            for (int g = 0; g <= 3; g++)
            {
                float v = lo + (hi - lo) * g / 3f;
                float y = Y(v, lo, hi, top, h);
                UiDraw.Box(s, at, x + w, y, UiTokens.Hairline, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                UiDraw.TextMiddle(s, at, x, y - UiTokens.PairRow / 2f, UiTokens.PairRow, w - UiTokens.S1, UiStyle.Small, UiRole.Muted, v.ToString("0"), UiFlags.Right | UiFlags.Middle);
            }
        }

        private static void Plot(UiScene s, UiAt at, UiChartDeskModel m, int n, float px, float slot, float top, float h, float lo, float hi, float volH)
        {
            float most = 1f;
            if (m.Volume != null)
            {
                for (int i = 0; i < n && i < m.Volume.Length; i++)
                {
                    if (m.Volume[i] > most)
                    {
                        most = m.Volume[i];
                    }
                }
            }
            for (int i = 0; i < n; i++)
            {
                float close = m.Close[i];
                float open = m.Open != null ? m.Open[i] : close;
                bool up = close >= open;
                UiRole tone = up ? UiRole.Ok : UiRole.Danger;
                float cx = px + slot * i;
                if (m.Kind == 0)
                {
                    float yH = Y(m.High != null ? m.High[i] : Math.Max(open, close), lo, hi, top, h);
                    float yL = Y(m.Low != null ? m.Low[i] : Math.Min(open, close), lo, hi, top, h);
                    UiDraw.Box(s, at, cx + slot * 0.5f, Math.Min(yH, yL), UiTokens.Hairline, Math.Abs(yL - yH) + UiTokens.Hairline, tone, UiRole.None, 0f, UiFlags.Decor);
                    float yO = Y(Math.Max(open, close), lo, hi, top, h);
                    float yC = Y(Math.Min(open, close), lo, hi, top, h);
                    UiDraw.Box(s, at, cx + slot * 0.2f, Math.Min(yO, yC), slot * 0.6f, Math.Max(UiTokens.Hairline, Math.Abs(yC - yO)), tone, UiRole.None, 0f, UiFlags.Decor);
                }
                else
                {
                    float y = Y(close, lo, hi, top, h);
                    if (m.Kind == 2)
                    {
                        UiDraw.Box(s, at, cx, y, slot, top + h - y, UiRole.Chart1, UiRole.None, 0f, UiFlags.Decor);
                    }
                }
                if (volH > 0f && m.Volume != null && i < m.Volume.Length)
                {
                    float vh = m.Volume[i] / most * volH;
                    UiDraw.Box(s, at, cx + slot * 0.2f, top + h + UiTokens.S2 + volH - vh, slot * 0.6f,
                        Math.Max(UiTokens.Hairline, vh), up ? UiRole.Ok : UiRole.Danger, UiRole.None, 0f, UiFlags.Decor);
                }
            }
            if (m.Kind != 0)
            {
                Line(s, at, m.Close, n, px, slot, top, h, lo, hi, UiRole.Accent);
            }
        }

        private static void Lines(UiScene s, UiAt at, UiChartLine[] lines, int n, float px, float slot, float top, float h, float lo, float hi)
        {
            for (int k = 0; lines != null && k < lines.Length; k++)
            {
                UiRole tint = lines[k].Tint != UiRole.None ? lines[k].Tint : UiTokens.Chart(k);
                Line(s, at, lines[k].Values, n, px, slot, top, h, lo, hi, tint);
            }
        }

        private static void Line(UiScene s, UiAt at, float[] v, int n, float px, float slot, float top, float h, float lo, float hi, UiRole tint)
        {
            if (v == null)
            {
                return;
            }
            int last = Math.Min(n, v.Length);
            for (int i = 1; i < last; i++)
            {
                if (float.IsNaN(v[i - 1]) || float.IsNaN(v[i]))
                {
                    continue;
                }
                float x0 = px + slot * (i - 0.5f), x1 = px + slot * (i + 0.5f);
                float y0 = Y(v[i - 1], lo, hi, top, h), y1 = Y(v[i], lo, hi, top, h);
                var seg = new UiItem
                {
                    Kind = UiKind.Spark, Fill = tint, X = x0, Y = Math.Min(y0, y1), W = Math.Max(UiTokens.Hairline, x1 - x0), H = Math.Max(UiTokens.Hairline, Math.Abs(y1 - y0)),
                    Points = new[] { v[i - 1], v[i] }, Flags = UiFlags.Decor, Clip = at.Clip, Node = -1, Layer = at.Layer, Order = -1, Parent = at.Parent, Alpha = 1f
                };
                s.Add(ref seg);
            }
        }

        private static void Marks(UiScene s, UiAt at, UiChartDeskModel m, float px, float slot, float top, float h, float lo, float hi, int n)
        {
            for (int i = 0; m.Marks != null && i < m.Marks.Length; i++)
            {
                UiChartMark mark = m.Marks[i];
                float x0 = px + slot * mark.FromX, x1 = px + slot * mark.ToX;
                float y0 = Y(mark.FromY, lo, hi, top, h), y1 = Y(mark.ToY, lo, hi, top, h);
                UiRole tint = mark.Tint != UiRole.None ? mark.Tint : UiRole.Accent;
                var seg = new UiItem
                {
                    Kind = UiKind.Spark, Fill = tint, X = Math.Min(x0, x1), Y = Math.Min(y0, y1), W = Math.Max(UiTokens.Hairline, Math.Abs(x1 - x0)),
                    H = Math.Max(UiTokens.Hairline, Math.Abs(y1 - y0)), Points = new[] { mark.FromY, mark.ToY }, Flags = UiFlags.Decor,
                    Clip = at.Clip, Node = -1, Layer = at.Layer, Order = -1, Parent = at.Parent, Alpha = 1f
                };
                s.Add(ref seg);
            }
        }

        private static void Pane(UiScene s, UiAt at, UiChartLine pane, int n, float px, float slot, float y, float h, float axis, float ix)
        {
            UiDraw.TextMiddle(s, at, ix, y, UiTokens.PairRow, axis, UiStyle.Small, UiRole.Muted, pane.Name, UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
            float[] v = pane.Values;
            if (v == null || v.Length == 0)
            {
                return;
            }
            float lo = float.MaxValue, hi = float.MinValue;
            int last = Math.Min(n, v.Length);
            for (int i = 0; i < last; i++)
            {
                if (float.IsNaN(v[i]))
                {
                    continue;
                }
                lo = Math.Min(lo, v[i]);
                hi = Math.Max(hi, v[i]);
            }
            if (hi - lo < 1e-3f)
            {
                hi += 1f;
                lo -= 1f;
            }
            Line(s, at, v, last, px, slot, y + UiTokens.PairRow, h - UiTokens.PairRow, lo, hi, pane.Tint != UiRole.None ? pane.Tint : UiRole.Chart2);
        }
    }
}
