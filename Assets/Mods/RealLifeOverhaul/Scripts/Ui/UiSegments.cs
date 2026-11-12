using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A segmented choice: a few words side by side on one track, the chosen one lit. One focus target for the whole track, so
    /// a row of them keeps a screen under the targets a layer may hold: the pointer's place picks the segment (the screen reads
    /// it through <see cref="At"/>, from the edges the layout writes back), Left and Right step it and Confirm takes the next.
    /// The chosen segment is the row-selected tone with its words in the text colour, or a fill of its own (Buy green, Sell
    /// red) with dark words. Game-free. Cost: a few items a segment, once a layout.
    /// </summary>
    public static class UiSegments
    {
        public const int Most = 7;                  // segments one control may hold: the edges the model keeps
        private const float Pad = UiTokens.S3;      // px: each side of a word, in a control sized by its words

        /// <summary>The width a control takes when each segment is as wide as its words.</summary>
        public static float Width(string[] labels, UiStyle style)
        {
            float w = 0f;
            for (int i = 0; labels != null && i < labels.Length; i++)
            {
                w += SegmentWidth(labels[i], style);
            }
            return w;
        }

        private static float SegmentWidth(string label, UiStyle style) => UiDraw.Snap(UiTextRules.Width(label ?? string.Empty, style) + Pad * 2f);

        /// <summary>
        /// Lays the control in a box. With <paramref name="even"/> every segment takes the same share, else each takes its words'
        /// width and the control ends where the last does (give <see cref="Width"/> as the width). The right edge of each segment,
        /// in px of the layout, goes into <paramref name="edges"/>. Returns the track's focus target.
        /// </summary>
        public static int Lay(UiScene s, UiAt at, float x, float y, float w, float h, string[] labels, int chosen, int command, float[] edges, bool even, UiRole[] fills = null,
            UiStyle style = UiStyle.Nav)
        {
            int n = Math.Min(labels != null ? labels.Length : 0, Most);
            int track = UiDraw.Box(s, at, x, y, w, h, UiRole.Field, UiRole.None, UiTokens.RadiusLarge);
            UiAt on = at.In(track);
            float cx = x;
            for (int i = 0; i < n; i++)
            {
                float sw = even ? w / n : SegmentWidth(labels[i], style);
                bool lit = i == chosen;
                UiRole fill = lit ? (fills != null && i < fills.Length && fills[i] != UiRole.None ? fills[i] : UiRole.RowSel) : UiRole.None;
                bool own = lit && fills != null && i < fills.Length && fills[i] != UiRole.None;
                int seg = UiDraw.Box(s, on, cx, y, sw, h, fill, UiRole.None, UiTokens.RadiusLarge, UiFlags.Share | (lit ? UiFlags.Selected : UiFlags.None));
                UiRole ink = own ? UiRole.OnAccent : lit ? UiRole.Text : UiRole.Text2;
                float room = sw - UiTokens.S1;
                if (UiTextRules.Width(labels[i], style) > room)
                {
                    // A longer translation keeps its words whole on two lines, as a button's do.
                    float lh = UiTextRules.LineHeight(style);
                    UiDraw.Text(s, on.In(seg), cx + UiTokens.S1 / 2f, y + (h - lh * 2f) / 2f, room, style, ink, labels[i], UiFlags.Label | UiFlags.Wrap | UiFlags.Centre | UiFlags.Middle, 2);
                }
                else
                {
                    UiDraw.TextMiddle(s, on.In(seg), cx + UiTokens.S1 / 2f, y, h, room, style, ink, labels[i], UiFlags.Centre | UiFlags.Label | UiFlags.Truncate);
                }
                cx += sw;
                if (edges != null && i < edges.Length)
                {
                    edges[i] = cx;
                }
            }
            return UiDraw.Node(s, at, x, y, w, h, command, 0, track);
        }

        /// <summary>The segment a place along the control falls in, from the edges the layout wrote back; -1 before the first layout.</summary>
        public static int At(float[] edges, int count, float x)
        {
            int n = Math.Min(count, edges != null ? edges.Length : 0);
            for (int i = 0; i < n; i++)
            {
                if (x < edges[i])
                {
                    return i;
                }
            }
            return n > 0 ? n - 1 : -1;
        }
    }
}
