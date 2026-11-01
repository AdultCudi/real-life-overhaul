using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The kit's figures: stat bars of ten segments, a number with its change, a sparkline and a step progress.</summary>
    public static partial class UiDraw
    {
        /// <summary>A figure over ten segments: its label and value, then the bar, filled to the value and shaded by it; returns the height.</summary>
        public static float StatBar(UiScene s, UiAt at, float x, float y, float w, ref UiStatModel stat)
        {
            float lh = UiTextRules.LineHeight(UiStyle.Small);
            float fw = UiTextRules.Width(stat.Figure, UiStyle.SmallStrong) + UiTokens.S1;
            UiRole grade = UiTokens.Grade(stat.Value);   // graded red to green, as a management game colours a skill
            Text(s, at, x, y, w - fw - UiTokens.S3, UiStyle.Small, UiRole.Text2, stat.Label, UiFlags.Label | UiFlags.Truncate);
            Text(s, at, x + w - fw, y, fw, UiStyle.SmallStrong, grade, stat.Figure, UiFlags.Right);
            float by = y + Snap(lh) + UiTokens.S2;
            // The grade bar's thin track and full colour, so an attribute reads the same on every screen.
            int value = Math.Max(0, Math.Min(100, stat.Value));
            int track = Box(s, at, x, by, w, UiTokens.S1, UiRole.Track, UiRole.None, UiTokens.S1 / 2f, UiFlags.Share);
            Box(s, at.In(track), x, by, Math.Max(UiTokens.S1, w * value / 100f), UiTokens.S1, grade, UiRole.None, UiTokens.S1 / 2f, UiFlags.Share);
            return by - y + UiTokens.S1;
        }

        /// <summary>
        /// A dashboard's figure: its label in capitals, the number large, and its change with an arrow that follows the
        /// change's sign, green when the change is good for the player and red when bad (rent up: red, up), over an optional
        /// sparkline in the same colour; returns the height. A figure with room beside its number draws its line there instead,
        /// as tall as the number and its change, so a wide panel shows the days without growing the row.
        /// </summary>
        public static float Figure(UiScene s, UiAt at, float x, float y, float w, ref UiFigureModel figure, bool large, bool beside = false)
        {
            UiStyle number = large ? UiStyle.Display : UiStyle.Number;
            bool line = Drawn(figure.Trend);
            float lh = UiTextRules.LineHeight(UiStyle.SectionHeader);
            float nw = line && beside ? SnapDown((w - UiTokens.S8) / 2f) : w;
            Text(s, at, x, y, w, UiStyle.SectionHeader, UiRole.Muted, figure.Label, UiFlags.Label | UiFlags.Truncate);
            float ny = y + Snap(lh) + UiTokens.S2;
            float nh = UiTextRules.LineHeight(number);
            var count = new UiItem
            {
                Kind = UiKind.Text, Flags = UiFlags.Truncate | UiFlags.CountUp, Ink = UiRole.Text, Style = number, X = x, Y = ny, W = nw, H = nh,
                Text = figure.Value ?? string.Empty, Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f, Value = 1f
            };
            s.Add(ref count);
            float cy = ny + Snap(nh);
            if (!string.IsNullOrEmpty(figure.Change))
            {
                UiRole ink = figure.Good ? UiRole.OkText : UiRole.DangerText;
                float lineH = Math.Max(UiTextRules.LineHeight(UiStyle.SmallStrong), UiTokens.Icon);
                Icon(s, at, x, cy, UiTokens.Icon, UiModels.Rises(ref figure) ? UiIcon.TrendUp : UiIcon.TrendDown, ink);
                TextMiddle(s, at, x + UiTokens.Icon + UiTokens.S1, cy, lineH, nw - UiTokens.Icon - UiTokens.S1, UiStyle.SmallStrong, ink, figure.Change, UiFlags.Truncate);
                cy += Snap(lineH);
            }
            // A line under a change takes the change's colour; one without a change says nothing good or bad, so it takes the accent.
            UiRole tint = string.IsNullOrEmpty(figure.Change) ? UiRole.Accent : figure.Good ? UiRole.Ok : UiRole.Danger;
            if (line && beside)
            {
                float sx = x + nw + UiTokens.S8;
                Spark(s, at, sx, ny, x + w - sx, cy - ny, figure.Trend, tint);
            }
            else if (line)
            {
                cy += UiTokens.S2;
                Spark(s, at, x, cy, w, UiTokens.S12, figure.Trend, tint);
                cy += UiTokens.S12;
            }
            return cy - y;
        }

        /// <summary>
        /// Whether a trend has a line to draw: two values or more that differ. A firm with nothing taken yet has thirty
        /// zeros, and their flat line said nothing (beta 3 drew it across into the next panel).
        /// </summary>
        public static bool Drawn(float[] trend)
        {
            if (trend == null || trend.Length < 2)
            {
                return false;
            }
            float low = trend[0], high = trend[0];
            for (int i = 1; i < trend.Length; i++)
            {
                low = Math.Min(low, trend[i]);
                high = Math.Max(high, trend[i]);
            }
            return high - low > 1e-6f;
        }

        /// <summary>A sparkline of values, oldest first, drawn as one line in a role's colour.</summary>
        public static int Spark(UiScene s, UiAt at, float x, float y, float w, float h, float[] values, UiRole tint)
        {
            var item = new UiItem
            {
                Kind = UiKind.Spark, Fill = tint, X = x, Y = y, W = w, H = h, Points = values, Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order,
                Parent = at.Parent, Alpha = 1f
            };
            return s.Add(ref item);
        }

        /// <summary>A session's progress: "Step 2 of 3" in capitals and a bar a step, done, now and to come; returns the height.</summary>
        public static float Steps(UiScene s, UiAt at, float x, float y, float w, string label, int count, int current)
        {
            float lh = UiTextRules.LineHeight(UiStyle.SectionHeader);
            Text(s, at, x, y, w, UiStyle.SectionHeader, UiRole.Muted, label, UiFlags.Label);
            float by = y + Snap(lh) + UiTokens.S2;
            float seg = (w - UiTokens.S2 * (count - 1)) / Math.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                int b = Box(s, at, x + i * (seg + UiTokens.S2), by, seg, UiTokens.Segment, i <= current ? UiRole.Accent : UiRole.Track, UiRole.None, UiTokens.RadiusSmall,
                    UiFlags.Share);
                s.Items[b].Alpha = i < current ? 0.6f : 1f;
            }
            return by - y + UiTokens.Segment;
        }
    }
}
