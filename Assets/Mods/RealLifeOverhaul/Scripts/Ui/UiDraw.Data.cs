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
            Text(s, at, x, y, w - fw - UiTokens.S3, UiStyle.Small, UiRole.Text2, stat.Label, UiFlags.Label | UiFlags.Truncate);
            Text(s, at, x + w - fw, y, fw, UiStyle.SmallStrong, UiRole.Text, stat.Figure, UiFlags.Right);
            float by = y + Snap(lh) + UiTokens.S2;
            float seg = (w - UiTokens.SegmentGap * (UiModels.Segments - 1)) / UiModels.Segments;
            int filled = UiModels.Filled(stat.Value);
            float shade = UiModels.Shade(stat.Value);
            for (int i = 0; i < UiModels.Segments; i++)
            {
                int b = Box(s, at, x + i * (seg + UiTokens.SegmentGap), by, seg, UiTokens.Segment, i < filled ? UiRole.Accent : UiRole.Track, UiRole.None,
                    UiTokens.RadiusSmall - UiTokens.Hairline, UiFlags.Share);
                s.Items[b].Alpha = i < filled ? shade : 1f;
                s.Items[b].Order = (short)i;
            }
            return by - y + UiTokens.Segment;
        }

        /// <summary>
        /// A dashboard's figure: its label in capitals, the number large, and its change with an arrow that follows the
        /// change's sign, green when the change is good for the player and red when bad (rent up: red, up), over an optional
        /// sparkline in the same colour; returns the height.
        /// </summary>
        public static float Figure(UiScene s, UiAt at, float x, float y, float w, ref UiFigureModel figure, bool large)
        {
            UiStyle number = large ? UiStyle.Display : UiStyle.Number;
            float lh = UiTextRules.LineHeight(UiStyle.SectionHeader);
            Text(s, at, x, y, w, UiStyle.SectionHeader, UiRole.Muted, figure.Label, UiFlags.Label | UiFlags.Truncate);
            float ny = y + Snap(lh) + UiTokens.S2;
            float nh = UiTextRules.LineHeight(number);
            var count = new UiItem
            {
                Kind = UiKind.Text, Flags = UiFlags.Truncate | UiFlags.CountUp, Ink = UiRole.Text, Style = number, X = x, Y = ny, W = w, H = nh,
                Text = figure.Value ?? string.Empty, Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f, Value = 1f
            };
            s.Add(ref count);
            float cy = ny + Snap(nh);
            if (!string.IsNullOrEmpty(figure.Change))
            {
                UiRole ink = figure.Good ? UiRole.OkText : UiRole.DangerText;
                float line = Math.Max(UiTextRules.LineHeight(UiStyle.SmallStrong), UiTokens.Icon);
                Icon(s, at, x, cy, UiTokens.Icon, UiModels.Rises(ref figure) ? UiIcon.TrendUp : UiIcon.TrendDown, ink);
                TextMiddle(s, at, x + UiTokens.Icon + UiTokens.S1, cy, line, w - UiTokens.Icon - UiTokens.S1, UiStyle.SmallStrong, ink, figure.Change, UiFlags.Truncate);
                cy += Snap(line);
            }
            if (figure.Trend != null && figure.Trend.Length > 1)
            {
                cy += UiTokens.S2;
                Spark(s, at, x, cy, w, UiTokens.S12, figure.Trend, figure.Good ? UiRole.Ok : UiRole.Danger);
                cy += UiTokens.S12;
            }
            return cy - y;
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
