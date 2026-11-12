using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The chart's drawings, clipped to the price plot, and the words round the plot: the legend, the axes and the reading card. Game-free.</summary>
    public static partial class UiChartDesk
    {
        private static readonly float[] Levels = { 0f, 0.236f, 0.382f, 0.5f, 0.618f, 1f };   // a Fibonacci retracement's lines

        /// <summary>
        /// A segment cut to a box (Liang and Barsky): false when none of it is inside. The ends are moved onto the box's edge, so
        /// a line that runs off the plot stops at its edge and never draws over the volume or the axis.
        /// </summary>
        public static bool ClipLine(ref float x0, ref float y0, ref float x1, ref float y1, float left, float top, float right, float bottom)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float t0 = 0f, t1 = 1f;
            if (!Edge(-dx, x0 - left, ref t0, ref t1) || !Edge(dx, right - x0, ref t0, ref t1) || !Edge(-dy, y0 - top, ref t0, ref t1) || !Edge(dy, bottom - y0, ref t0, ref t1))
            {
                return false;
            }
            float ax = x0 + t0 * dx, ay = y0 + t0 * dy, bx = x0 + t1 * dx, by = y0 + t1 * dy;
            x0 = ax;
            y0 = ay;
            x1 = bx;
            y1 = by;
            return true;
        }

        private static bool Edge(float p, float q, ref float t0, ref float t1)
        {
            if (Math.Abs(p) < 1e-6f)
            {
                return q >= 0f;
            }
            float r = q / p;
            if (p < 0f)
            {
                if (r > t1)
                {
                    return false;
                }
                t0 = Math.Max(t0, r);
            }
            else
            {
                if (r < t0)
                {
                    return false;
                }
                t1 = Math.Min(t1, r);
            }
            return true;
        }

        // One stroke of a drawing, cut to the price plot: nothing of it is drawn beyond the plot's edge.
        private static void Stroke(UiScene s, UiAt at, float x0, float y0, float x1, float y1, float pw, float h, UiRole tint, float alpha)
        {
            float t = UiTokens.Hairline;
            if (!ClipLine(ref x0, ref y0, ref x1, ref y1, t, t, pw - t, h - t))
            {
                return;
            }
            if (x1 < x0)
            {
                float sx = x0, sy = y0;
                x0 = x1;
                y0 = y1;
                x1 = sx;
                y1 = sy;
            }
            float[] slope = y1 < y0 - 0.01f ? Rising : y1 > y0 + 0.01f ? Falling : Flat;
            Spark(s, at, slope, x0, Math.Min(y0, y1), x1 - x0, Math.Abs(y1 - y0), tint, alpha);
        }

        /// <summary>The colour a drawing has when its model gives it none.</summary>
        public static UiRole MarkTint(UiChartMarkKind kind)
        {
            switch (kind)
            {
                case UiChartMarkKind.Trend: return UiRole.Gold;
                case UiChartMarkKind.Level: return UiRole.Warn;
                case UiChartMarkKind.Fibonacci: return UiRole.Chart2;
                case UiChartMarkKind.Ray: return UiRole.Chart1;
                case UiChartMarkKind.Rectangle: return UiRole.Chart7;
                default: return UiRole.Text;
            }
        }

        /// <summary>
        /// The drawing nearest a place on the screen within a radius, or -1: what the pointer tool selects. The distances are the
        /// layout's px, from where the last layout put the plot.
        /// </summary>
        public static int HitMark(UiChartDeskModel m, float x, float y, float radius)
        {
            int best = -1;
            float most = radius;
            float slot = m.Slot, lo = m.PlotLo, hi = m.PlotHi, h = m.PriceH, right = m.PlotX + m.PlotW;
            for (int i = 0; m.Marks != null && i < m.Marks.Length; i++)
            {
                UiChartMark k = m.Marks[i];
                float x0 = m.PlotX + slot * (k.FromX - m.First + 0.5f), x1 = m.PlotX + slot * (k.ToX - m.First + 0.5f);
                float y0 = m.PlotY + PriceY(k.FromY, lo, hi, h), y1 = m.PlotY + PriceY(k.ToY, lo, hi, h);
                float d;
                switch (k.Kind)
                {
                    case UiChartMarkKind.Level:
                        d = Math.Abs(y - y0);
                        break;
                    case UiChartMarkKind.Note:
                        d = (float)Math.Sqrt((x - x0) * (x - x0) + (y - y0) * (y - y0));
                        break;
                    case UiChartMarkKind.Fibonacci:
                        d = float.MaxValue;
                        for (int l = 0; l < Levels.Length; l++)
                        {
                            float ly = m.PlotY + PriceY(k.FromY + (k.ToY - k.FromY) * Levels[l], lo, hi, h);
                            d = Math.Min(d, Away(x, y, Math.Min(x0, x1), ly, right, ly));
                        }
                        break;
                    case UiChartMarkKind.Ray:
                        d = Away(x, y, x0, y0, x1 > x0 + 0.5f ? right : x1, x1 > x0 + 0.5f ? y0 + (y1 - y0) / (x1 - x0) * (right - x0) : y1);
                        break;
                    case UiChartMarkKind.Rectangle:
                    {
                        float l = Math.Min(x0, x1), r = Math.Max(x0, x1), t = Math.Min(y0, y1), b = Math.Max(y0, y1);
                        d = Math.Min(Math.Min(Away(x, y, l, t, r, t), Away(x, y, l, b, r, b)), Math.Min(Away(x, y, l, t, l, b), Away(x, y, r, t, r, b)));
                        break;
                    }
                    default:
                        d = Away(x, y, x0, y0, x1, y1);
                        break;
                }
                if (d <= most)
                {
                    most = d;
                    best = i;
                }
            }
            return best;
        }

        // The distance from a place to a segment.
        private static float Away(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float len = dx * dx + dy * dy;
            float t = len < 1e-6f ? 0f : Math.Max(0f, Math.Min(1f, ((px - ax) * dx + (py - ay) * dy) / len));
            float cx = ax + t * dx - px, cy = ay + t * dy - py;
            return (float)Math.Sqrt(cx * cx + cy * cy);
        }

        private static void Marks(UiScene s, UiAt at, UiChartDeskModel m, int first, float slot, float pw, float h, float lo, float hi)
        {
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            for (int i = 0; m.Marks != null && i < m.Marks.Length; i++)
            {
                UiChartMark mark = m.Marks[i];
                UiRole tint = mark.Tint != UiRole.None ? mark.Tint : MarkTint(mark.Kind);
                float alpha = mark.Selected ? 1f : 0.85f;
                float x0 = slot * (mark.FromX - first + 0.5f), x1 = slot * (mark.ToX - first + 0.5f);
                float y0 = PriceY(mark.FromY, lo, hi, h), y1 = PriceY(mark.ToY, lo, hi, h);
                switch (mark.Kind)
                {
                    case UiChartMarkKind.Level:
                        Stroke(s, at, 0f, y0, pw, y0, pw, h, tint, alpha);
                        Chip(s, at, UiTokens.S1, y0 - lh, pw, h, mark.Label, tint);
                        break;
                    case UiChartMarkKind.Fibonacci:
                        float named = float.MaxValue;   // the last line named: a close one stays unnamed, as its words would lie over the last
                        for (int k = 0; k < Levels.Length; k++)
                        {
                            float price = mark.FromY + (mark.ToY - mark.FromY) * Levels[k];
                            float y = PriceY(price, lo, hi, h);
                            Stroke(s, at, Math.Min(x0, x1), y, pw, y, pw, h, tint, k == 0 || k == Levels.Length - 1 ? alpha : alpha * 0.8f);
                            if (y > 0f && y < h && Math.Abs(y - named) >= lh)
                            {
                                Words(s, at, Math.Max(UiTokens.S1, Math.Min(x0, x1)) + UiTokens.S1, y - lh, pw, Levels[k].ToString("0.000", Inv) + " " + Money(m, price), tint);
                                named = y;
                            }
                        }
                        break;
                    case UiChartMarkKind.Ray:
                        float rx = x1, ry = y1;
                        if (x1 > x0 + 0.5f)
                        {
                            float run = pw - x0;
                            rx = pw;
                            ry = y0 + (y1 - y0) / (x1 - x0) * run;
                        }
                        Stroke(s, at, x0, y0, rx, ry, pw, h, tint, alpha);
                        break;
                    case UiChartMarkKind.Rectangle:
                        float left = Math.Min(x0, x1), right = Math.Max(x0, x1), up = Math.Min(y0, y1), down = Math.Max(y0, y1);
                        float cl = Math.Max(0f, left), cr = Math.Min(pw, right), cu = Math.Max(0f, up), cd = Math.Min(h, down);
                        if (cr > cl && cd > cu)
                        {
                            Fill(s, at, cl, cu, cr - cl, cd - cu, tint, 0.08f);
                        }
                        Stroke(s, at, left, up, right, up, pw, h, tint, alpha);
                        Stroke(s, at, left, down, right, down, pw, h, tint, alpha);
                        Stroke(s, at, left, up, left, down, pw, h, tint, alpha);
                        Stroke(s, at, right, up, right, down, pw, h, tint, alpha);
                        break;
                    case UiChartMarkKind.Measure:
                        Stroke(s, at, x0, y0, x1, y1, pw, h, tint, alpha);
                        Chip(s, at, Math.Max(UiTokens.S1, Math.Min(x0, x1)), Math.Min(y0, y1) - lh - UiTokens.S1, pw, h, mark.Label, mark.ToY >= mark.FromY ? UiRole.Ok : UiRole.Danger);
                        break;
                    case UiChartMarkKind.Note:
                        Dot(s, at, x0, y0, pw, h, tint);
                        Chip(s, at, x0 + UiTokens.S2, y0 - lh - UiTokens.S1, pw, h, mark.Label, tint);
                        break;
                    default:
                        Stroke(s, at, x0, y0, x1, y1, pw, h, tint, alpha);
                        break;
                }
                if (mark.Selected)
                {
                    Dot(s, at, x0, y0, pw, h, UiRole.Accent);
                    if (mark.Kind != UiChartMarkKind.Level && mark.Kind != UiChartMarkKind.Note)
                    {
                        Dot(s, at, x1, y1, pw, h, UiRole.Accent);
                    }
                }
            }
            if (m.Anchor >= first && m.Anchor < first + m.Shown)
            {
                Dot(s, at, slot * (m.Anchor - first + 0.5f), PriceY(m.AnchorY, lo, hi, h), pw, h, UiRole.Accent);
            }
        }

        // A handle or a point of a drawing: a dot, drawn only where the plot holds it whole.
        private static void Dot(UiScene s, UiAt at, float x, float y, float pw, float h, UiRole tint)
        {
            float r = UiTokens.S2;
            if (x < r || x > pw - r || y < r || y > h - r)
            {
                return;
            }
            var dot = new UiItem
            {
                Kind = UiKind.Box, Flags = UiFlags.Decor | UiFlags.Circle, Fill = tint, Radius = r, X = x - r, Y = y - r, W = r * 2f, H = r * 2f, Clip = at.Clip, Node = -1,
                Layer = at.Layer, Order = -1, Parent = at.Parent, Alpha = 1f
            };
            s.Add(ref dot);
        }

        // A short word on a drawing, in its colour and clear of the plot's edges.
        private static void Words(UiScene s, UiAt at, float x, float y, float pw, string words, UiRole tint)
        {
            if (string.IsNullOrEmpty(words))
            {
                return;
            }
            float w = UiTextRules.Width(words, UiStyle.Small) + UiTokens.S1;
            if (x + w > pw)
            {
                x = pw - w;
            }
            UiDraw.Text(s, at, Math.Max(0f, x), y, w, UiStyle.Small, tint, words, UiFlags.Label | UiFlags.Decor);
        }

        // A drawing's tag: its words on a chip of its colour, kept inside the price plot.
        private static void Chip(UiScene s, UiAt at, float x, float y, float pw, float h, string words, UiRole tint)
        {
            if (string.IsNullOrEmpty(words))
            {
                return;
            }
            float ch = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small) + UiTokens.S1);
            float cw = UiDraw.Snap(UiTextRules.Width(words, UiStyle.Small) + UiTokens.S3);
            x = Math.Max(0f, Math.Min(x, pw - cw));
            y = Math.Max(0f, Math.Min(y, h - ch));
            int chip = UiDraw.Box(s, at, x, y, cw, ch, tint, UiRole.None, UiTokens.RadiusSmall, UiFlags.Decor);
            UiDraw.TextMiddle(s, at.In(chip), x + UiTokens.S2, y, ch, cw - UiTokens.S3, UiStyle.Small, UiRole.OnAccent, words, UiFlags.Label | UiFlags.Decor);
        }

        // The legend line: the chart's name, then each line over the price in its colour with its value at the candle read.
        private static void Legend(UiScene s, UiAt at, UiChartDeskModel m, float x, float y, float w, float h, int first, int shown)
        {
            int f = m.Focus >= 0 && m.Focus < m.Close.Length ? m.Focus : first + shown - 1;
            float cx = x;
            string head = m.Head;
            if (!string.IsNullOrEmpty(head))
            {
                float hw = Math.Min(UiDraw.SnapDown(w), UiDraw.Snap(UiTextRules.Width(head, UiStyle.SmallStrong) + UiTokens.S2));
                UiDraw.TextMiddle(s, at, cx, y, h, hw, UiStyle.SmallStrong, UiRole.Text2, head, UiFlags.Label | UiFlags.Truncate);
                cx += hw + UiTokens.S3;
            }
            for (int k = 0; m.Lines != null && k < m.Lines.Length; k++)
            {
                UiChartLine line = m.Lines[k];
                string words = line.Name + " " + Value(line.Values, f, "0.00") + (line.Under != null ? " / " + Value(line.Under, f, "0.00") : string.Empty);
                float lw = UiDraw.Snap(UiTextRules.Width(words, UiStyle.SmallStrong) + UiTokens.S1);
                if (cx + lw > x + w)
                {
                    break;
                }
                UiDraw.TextMiddle(s, at, cx, y, h, lw, UiStyle.SmallStrong, line.Tint, words, UiFlags.Label | UiFlags.Truncate);
                cx += lw + UiTokens.S3;
            }
        }

        private static string Value(float[] v, int i, string format) => v != null && i >= 0 && i < v.Length && !float.IsNaN(v[i]) ? v[i].ToString(format, Inv) : "-";

        // The price axis at the right: each round price at its height, in dollars.
        private static void Axis(UiScene s, UiAt at, UiChartDeskModel m, float px, float top, float pw, float h, float axisW, float lo, float hi)
        {
            Ticks(lo, hi, out float step, out float tick);
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            for (int k = 0; TickAt(tick, step, hi, k, out float v); k++)
            {
                float y = top + PriceY(v, lo, hi, h);
                UiDraw.Text(s, at, px + pw + UiTokens.S1, y - lh / 2f, axisW - UiTokens.S1, UiStyle.Small, UiRole.Muted, Money(m, v), UiFlags.Middle | UiFlags.Label);
            }
        }

        // The words that stand over the plot's panes, and the pane guides at the axis; laid after the plot so they are on top of it.
        private static void SideWords(UiScene s, UiAt at, UiChartDeskModel m, float px, float top, float pw, float priceH, float volH, float paneH, int panes, float axisW,
            float lo, float hi, int first, int shown)
        {
            int f = m.Focus >= first && m.Focus < first + shown ? m.Focus : first + shown - 1;
            float y = top + priceH + UiTokens.S2;
            float row = UiTokens.PairRow;
            if (volH > 0f)
            {
                string vol = (m.VolumeLabel ?? string.Empty) + " " + Compact(m.Volume != null && f < m.Volume.Length ? m.Volume[f] : 0f);
                UiDraw.TextMiddle(s, at, px + UiTokens.S2, y, row, UiTextRules.Width(vol, UiStyle.SmallStrong) + UiTokens.S2, UiStyle.SmallStrong, UiRole.Text2, vol, UiFlags.Label | UiFlags.Truncate);
                y += volH + UiTokens.S2;
            }
            for (int k = 0; k < panes; k++)
            {
                UiChartPane pane = m.Panes[k];
                string words = pane.Name + "  " + Value(pane.Values, f, pane.Format ?? "0.0") + (pane.Second != null ? "  " + Value(pane.Second, f, pane.Format ?? "0.0") : string.Empty)
                    + (pane.Histogram != null ? "  " + Value(pane.Histogram, f, pane.Format ?? "0.0") : string.Empty);
                UiDraw.TextMiddle(s, at, px + UiTokens.S2, y, row, Math.Min(pw - UiTokens.S4, UiTextRules.Width(words, UiStyle.SmallStrong) + UiTokens.S2), UiStyle.SmallStrong, pane.Tint, words,
                    UiFlags.Label | UiFlags.Truncate);
                float inset = UiTokens.S2, room = paneH - inset * 2f;
                for (int g = 0; pane.Guides != null && g < pane.Guides.Length; g++)
                {
                    float gy = y + inset + (1f - pane.Guides[g] / 100f) * room;
                    UiDraw.Text(s, at, px + pw + UiTokens.S1, gy - UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) / 2f, axisW - UiTokens.S1, UiStyle.Small, UiRole.Muted,
                        pane.Guides[g].ToString("0", Inv), UiFlags.Middle | UiFlags.Label);
                }
                y += paneH + UiTokens.S2;
            }
        }

        // The days along the foot: as many as stand clear of each other, the candle each names under it.
        private static void TimeAxis(UiScene s, UiAt at, UiChartDeskModel m, float px, float y, float pw, float slot, int first, int shown)
        {
            if (m.Stamp == null)
            {
                return;
            }
            float most = 0f;
            for (int i = first; i < first + shown && i < m.Stamp.Length; i++)
            {
                most = Math.Max(most, UiTextRules.Width(m.Stamp[i] ?? string.Empty, UiStyle.Small));
            }
            float cell = most + UiTokens.S8;
            int count = Math.Max(1, Math.Min(8, (int)(pw / Math.Max(1f, cell))));
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            for (int k = 0; k < count; k++)
            {
                int i = first + (int)Math.Round((shown - 1) * (k + 0.5f) / count);
                if (i < 0 || i >= m.Stamp.Length || string.IsNullOrEmpty(m.Stamp[i]))
                {
                    continue;
                }
                float lw = UiTextRules.Width(m.Stamp[i], UiStyle.Small) + UiTokens.S1;
                float lx = Math.Max(px, Math.Min(px + pw - lw, px + slot * (i - first + 0.5f) - lw / 2f));
                UiDraw.Text(s, at, lx, y, lw, UiStyle.Small, UiRole.Muted, m.Stamp[i], UiFlags.Label | UiFlags.Centre);
            }
        }

        // The last price on the axis, on a tag of the day's colour.
        private static void LastTag(UiScene s, UiAt at, UiChartDeskModel m, float x, float top, float w, float h, float lo, float hi)
        {
            int last = m.Close.Length - 1;
            if (string.IsNullOrEmpty(m.LastTag) || !UiChart.Real(m.Close[last]))
            {
                return;   // no tag without a price to stand at: Math.Min passes NaN on, and the tag would have no place
            }
            float y = top + PriceY(Math.Max(lo, Math.Min(hi, m.Close[last])), lo, hi, h);
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SmallStrong) + UiTokens.S1);
            int tag = UiDraw.Box(s, at, x, y - th / 2f, w, th, m.LastUp ? UiRole.Ok : UiRole.Danger, UiRole.None, UiTokens.RadiusSmall, UiFlags.Decor | UiFlags.Middle);
            UiDraw.TextMiddle(s, at.In(tag), x + UiTokens.S1, y - th / 2f, th, w - UiTokens.S2, UiStyle.SmallStrong, UiRole.OnAccent, m.LastTag, UiFlags.Centre | UiFlags.Decor | UiFlags.Middle);
        }

        // The candle under the pointer, in a card beside it: its day, open, high, low, close and volume.
        private static void Reading(UiScene s, UiAt at, UiChartDeskModel m, float px, float top, float pw, float stack, float slot, int first, float priceH, float lo, float hi,
            float boxX, float boxW)
        {
            int f = m.Focus;
            if (f < 0 || f >= m.Close.Length || f < first || f >= first + m.Shown)
            {
                return;
            }
            string[] labels = { m.DayLabel, m.OpenLabel, m.HighLabel, m.LowLabel, m.CloseLabel, m.VolumeLabel };
            float open = m.Open != null ? m.Open[f] : m.Close[f];
            string[] values =
            {
                m.Stamp != null && f < m.Stamp.Length ? m.Stamp[f] : string.Empty, Money(m, open), Money(m, m.High != null ? m.High[f] : m.Close[f]),
                Money(m, m.Low != null ? m.Low[f] : m.Close[f]), Money(m, m.Close[f]), m.Volume != null ? Compact(m.Volume[f]) : string.Empty
            };
            int rows = m.Volume != null ? 6 : 5;
            float lw = 0f, vw = 0f;
            for (int r = 0; r < rows; r++)
            {
                lw = Math.Max(lw, UiTextRules.Width(labels[r] ?? string.Empty, UiStyle.Small));
                vw = Math.Max(vw, UiTextRules.Width(values[r], UiStyle.SmallStrong));
            }
            float row = UiTokens.S5;
            float cw = UiDraw.Snap(lw + UiTokens.S4 + vw + UiTokens.S1 + UiTokens.S3 * 2f);
            float ch = UiDraw.Snap(UiTokens.S2 * 2f + rows * row);
            float cx = px + slot * (f - first + 1) + UiTokens.S3;
            if (cx + cw > px + pw)
            {
                cx = px + slot * (f - first) - UiTokens.S3 - cw;
            }
            cx = UiDraw.Snap(Math.Max(boxX + UiTokens.S2, cx));
            float cy = UiDraw.Snap(top + UiTokens.S2);
            var over = new UiAt { Parent = at.Parent, Clip = -1, Layer = 1, Order = -1, Region = at.Region };
            int card = UiDraw.Box(s, over, cx, cy, cw, ch, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Decor);
            UiAt inCard = over.In(card);
            for (int r = 0; r < rows; r++)
            {
                float ry = cy + UiTokens.S2 + r * row;
                UiDraw.TextMiddle(s, inCard, cx + UiTokens.S3, ry, row, lw + UiTokens.S1, UiStyle.Small, UiRole.Text2, labels[r] ?? string.Empty, UiFlags.Label);
                UiRole ink = r == 4 ? (m.Close[f] >= open ? UiRole.OkText : UiRole.DangerText) : UiRole.Text;
                UiDraw.TextMiddle(s, inCard, cx + cw - UiTokens.S3 - vw - UiTokens.S1, ry, row, vw + UiTokens.S1, UiStyle.SmallStrong, ink, values[r], UiFlags.Right);
            }
        }
    }
}
