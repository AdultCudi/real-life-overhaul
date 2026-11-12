using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What one block of a tooltip card is.</summary>
    public enum UiTipKind : byte
    {
        Header,    // the thing's name, a second word at the right, a line under them
        Rule,      // a hairline between two sections
        Category,  // a section's label in capitals
        Text,      // words that wrap
        Meter,     // a label and its figure, a bar under them
        Rows,      // label and value pairs
        List,      // lines of words
        Checks,    // lines with a tick or an empty box, a word at the right
        Chart,     // a line over a few days with its highest and lowest figure at the right
    }

    /// <summary>One line of a rows, list or checks block.</summary>
    public struct UiTipLine
    {
        public string Left;       // the label (rows, checks) or the words (list)
        public string Right;      // the value, or a checks line's word at the right (a priority)
        public UiRole Ink;        // the value's colour; None for the plain one
        public bool On;           // a checks line's tick
    }

    /// <summary>One block of a tooltip card; which of its fields count depends on <see cref="Kind"/>.</summary>
    public sealed class UiTipBlock
    {
        public UiTipKind Kind;
        public string Title, Sub, Detail;      // a header's name, its word at the right and its line under them; a meter's label (Title) and figure (Sub)
        public UiIcon Icon = UiIcon.None;      // a header's glyph
        public string Text;                    // a text block's words; a category's label
        public UiRole Ink = UiRole.None;       // a text block's colour; None for the plain one
        public bool Strong, Centre;            // bold words; centred words
        public int Value;                      // a meter's fill, 0 to 100
        public UiRole Tone = UiRole.Ok;        // a meter's fill colour
        public List<UiTipLine> Lines = new List<UiTipLine>(4);
        public float[] Points;                 // a chart's values, oldest first
        public string High, Low, From, To;     // a chart's highest and lowest figure at the right; its first and last day under it
    }

    /// <summary>
    /// What a tooltip says, as plain data: the blocks the game's TooltipSystem builds (a header, a label, a bar, lists), or
    /// the mod's own card (the net worth over the money pill). No colour or position: the card lays it out.
    /// </summary>
    public sealed class UiTooltipModel
    {
        public readonly List<UiTipBlock> Blocks = new List<UiTipBlock>(8);

        public void Clear() => Blocks.Clear();

        public UiTipBlock Add(UiTipKind kind)
        {
            var b = new UiTipBlock { Kind = kind };
            Blocks.Add(b);
            return b;
        }

        public UiTipBlock Header(string title, string sub = null, string detail = null, UiIcon icon = UiIcon.None)
        {
            UiTipBlock b = Add(UiTipKind.Header);
            b.Title = title;
            b.Sub = sub;
            b.Detail = detail;
            b.Icon = icon;
            return b;
        }

        public void Rule() => Add(UiTipKind.Rule);

        public void Category(string label) => Add(UiTipKind.Category).Text = label;

        public UiTipBlock Text(string text, UiRole ink = UiRole.None, bool strong = false, bool centre = false)
        {
            UiTipBlock b = Add(UiTipKind.Text);
            b.Text = text;
            b.Ink = ink;
            b.Strong = strong;
            b.Centre = centre;
            return b;
        }

        public UiTipBlock Meter(string label, string figure, int value, UiRole tone)
        {
            UiTipBlock b = Add(UiTipKind.Meter);
            b.Title = label;
            b.Sub = figure;
            b.Value = value;
            b.Tone = tone;
            return b;
        }

        public UiTipBlock Rows(UiTipKind kind = UiTipKind.Rows) => Add(kind);

        public UiTipBlock Chart(float[] points, string high, string low, string from, string to)
        {
            UiTipBlock b = Add(UiTipKind.Chart);
            b.Points = points;
            b.High = high;
            b.Low = low;
            b.From = from;
            b.To = to;
            return b;
        }

        /// <summary>True when the blocks say nothing a player would read (no header, no words, no rows).</summary>
        public bool Empty
        {
            get
            {
                for (int i = 0; i < Blocks.Count; i++)
                {
                    UiTipBlock b = Blocks[i];
                    if (b.Kind != UiTipKind.Rule && !(b.Kind == UiTipKind.Text && string.IsNullOrEmpty(b.Text)) && !(b.Kind == UiTipKind.Header && string.IsNullOrEmpty(b.Title))
                        && !((b.Kind == UiTipKind.Rows || b.Kind == UiTipKind.List || b.Kind == UiTipKind.Checks) && b.Lines.Count == 0))
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }

    /// <summary>
    /// The tooltip card: a small slate panel that stands beside the pointer, with the thing's name in the title style, rows
    /// of label and value, a meter, a list, a tick list and a short line chart, as the game's TooltipSystem builds them and
    /// as the net worth card over the money pill. It takes no target and no focus: it is laid out once when its words
    /// change, and following the pointer only moves it (UiTooltipRules.Place). Width follows the words, between
    /// <see cref="MinWidth"/> and <see cref="MaxWidth"/>; words past it wrap. Game-free. Cost: a layout a change; nothing a frame.
    /// </summary>
    public static class UiTooltipCard
    {
        public const float MinWidth = 192f;      // px: the narrowest card
        public const float MaxWidth = 320f;      // px: as wide as a world prompt, words past it wrap
        public const float ChartHeight = 64f;    // px: the line chart's plot
        public const float ChartWidth = 156f;    // px: ...and its width, its figures at the right of it
        private const float Pad = UiTokens.S3;   // px: the card's inside margin at its sides
        private const float PadY = UiTokens.S2;  // px: ...and above and below
        private const float RowHeight = UiTokens.PairRow;   // px: a label and its value
        private const float LineHeightPx = 20f;  // px: a line of a list
        private const float Join = UiTokens.S3;  // px: between a label and its value
        private const float BarHeight = UiTokens.S1;   // px: a meter's bar
        private const int WrapLines = 6;

        /// <summary>The card's width for its words.</summary>
        public static float Width(UiTooltipModel m)
        {
            float need = 0f;
            for (int i = 0; i < m.Blocks.Count; i++)
            {
                need = Math.Max(need, Needs(m.Blocks[i]));
            }
            float w = UiDraw.Snap(need + Pad * 2f);
            return Math.Max(MinWidth, Math.Min(MaxWidth, w));
        }

        /// <summary>The card's height for its words at a width.</summary>
        public static float Height(UiTooltipModel m, float width)
        {
            float inner = width - Pad * 2f;
            float h = PadY;
            for (int i = 0; i < m.Blocks.Count; i++)
            {
                h += BlockHeight(m.Blocks[i], inner);
            }
            return UiDraw.Snap(h + PadY);
        }

        /// <summary>Lays the card with its top left at a place; returns its panel.</summary>
        public static int Lay(UiScene s, UiAt at, UiTooltipModel m, float x, float y, float width)
        {
            float h = Height(m, width);
            int panel = UiDraw.Box(s, at, x, y, width, h, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);   // its edge parts it from a bright street
            UiAt on = at.In(panel);
            float inner = width - Pad * 2f, ix = x + Pad, cy = y + PadY;
            for (int i = 0; i < m.Blocks.Count; i++)
            {
                UiTipBlock b = m.Blocks[i];
                switch (b.Kind)
                {
                    case UiTipKind.Header: cy = LayHeader(s, on, b, ix, cy, inner); break;
                    case UiTipKind.Rule: cy = LayRule(s, on, x, cy, width); break;
                    case UiTipKind.Category: cy = LayCategory(s, on, b, ix, cy, inner); break;
                    case UiTipKind.Text: cy = LayText(s, on, b, ix, cy, inner); break;
                    case UiTipKind.Meter: cy = LayMeter(s, on, b, ix, cy, inner); break;
                    case UiTipKind.Rows: cy = LayRows(s, on, b, ix, cy, inner); break;
                    case UiTipKind.List: cy = LayList(s, on, b, ix, cy, inner); break;
                    case UiTipKind.Checks: cy = LayChecks(s, on, b, ix, cy, inner); break;
                    case UiTipKind.Chart: cy = LayChart(s, on, b, ix, cy, inner); break;
                }
            }
            return panel;
        }

        // ------------------------------------------------------------------------------------------------- measure

        private static float Needs(UiTipBlock b)
        {
            switch (b.Kind)
            {
                case UiTipKind.Header:
                {
                    float w = (b.Icon != UiIcon.None ? UiTokens.Icon + UiTokens.S2 : 0f) + UiTextRules.Width(b.Title, UiStyle.Title);
                    w += string.IsNullOrEmpty(b.Sub) ? 0f : Join + UiTextRules.Width(b.Sub, UiStyle.Small);
                    return Math.Max(w, UiTextRules.Width(b.Detail, UiStyle.Small)) + UiTokens.S1;
                }
                case UiTipKind.Category:
                    return UiTextRules.Width(b.Text, UiStyle.SectionHeader) + UiTokens.S1;
                case UiTipKind.Text:
                    return UiTextRules.Width(b.Text, b.Strong ? UiStyle.BodyStrong : UiStyle.Body) + UiTokens.S1;
                case UiTipKind.Meter:
                    return UiTextRules.Width(b.Title, UiStyle.Body) + Join + UiTextRules.Width(b.Sub, UiStyle.BodyStrong) + UiTokens.S1;
                case UiTipKind.Rows:
                case UiTipKind.Checks:
                {
                    float most = 0f;
                    for (int i = 0; i < b.Lines.Count; i++)
                    {
                        UiTipLine l = b.Lines[i];
                        float w = (b.Kind == UiTipKind.Checks ? UiTokens.IconSmall + UiTokens.S2 : 0f) + UiTextRules.Width(l.Left, UiStyle.Body)
                            + (string.IsNullOrEmpty(l.Right) ? 0f : Join + UiTextRules.Width(l.Right, UiStyle.Body)) + UiTokens.S1;
                        most = Math.Max(most, w);
                    }
                    return most;
                }
                case UiTipKind.List:
                {
                    float most = 0f;
                    for (int i = 0; i < b.Lines.Count; i++)
                    {
                        most = Math.Max(most, UiTextRules.Width(b.Lines[i].Left, UiStyle.Body) + UiTokens.S1);
                    }
                    return most;
                }
                case UiTipKind.Chart:
                    return ChartWidth + Join + Math.Max(UiTextRules.Width(b.High, UiStyle.Small), UiTextRules.Width(b.Low, UiStyle.Small)) + UiTokens.S1;
            }
            return 0f;
        }

        private static float BlockHeight(UiTipBlock b, float inner)
        {
            switch (b.Kind)
            {
                case UiTipKind.Header:
                    return UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title) + UiTokens.S1) + (string.IsNullOrEmpty(b.Detail) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small) + UiTokens.S1));
                case UiTipKind.Rule:
                    return UiTokens.S4;
                case UiTipKind.Category:
                    return UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader) + UiTokens.S2);
                case UiTipKind.Text:
                    return UiDraw.Snap(UiTextRules.WrappedHeight(b.Text, b.Strong ? UiStyle.BodyStrong : UiStyle.Body, inner, WrapLines) + UiTokens.S1);
                case UiTipKind.Meter:
                    return RowHeight + BarHeight + UiTokens.S2;
                case UiTipKind.Rows:
                case UiTipKind.Checks:
                    return b.Lines.Count * RowHeight;
                case UiTipKind.List:
                {
                    float h = 0f;
                    for (int i = 0; i < b.Lines.Count; i++)
                    {
                        h += UiDraw.Snap(UiTextRules.WrappedHeight(b.Lines[i].Left, UiStyle.Body, inner, 2) + UiTokens.S1);
                    }
                    return h;
                }
                case UiTipKind.Chart:
                    return ChartHeight + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S2;
            }
            return 0f;
        }

        // ------------------------------------------------------------------------------------------------- lay out

        private static float LayHeader(UiScene s, UiAt at, UiTipBlock b, float x, float y, float w)
        {
            float lh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title) + UiTokens.S1), tx = x, tw = w;
            if (b.Icon != UiIcon.None)
            {
                UiDraw.Icon(s, at, x, y + (lh - UiTokens.Icon) / 2f, UiTokens.Icon, b.Icon, UiRole.None, UiFlags.Middle);
                tx += UiTokens.Icon + UiTokens.S2;
                tw -= UiTokens.Icon + UiTokens.S2;
            }
            float sw = string.IsNullOrEmpty(b.Sub) ? 0f : UiDraw.Snap(UiTextRules.Width(b.Sub, UiStyle.Small) + UiTokens.S1);
            UiDraw.TextMiddle(s, at, tx, y, lh, tw - (sw > 0f ? sw + Join : 0f), UiStyle.Title, UiRole.Text, b.Title, UiFlags.Truncate);
            if (sw > 0f)
            {
                UiDraw.TextMiddle(s, at, x + w - sw, y, lh, sw, UiStyle.Small, UiRole.Text2, b.Sub, UiFlags.Right | UiFlags.Truncate);
            }
            y += lh;
            if (!string.IsNullOrEmpty(b.Detail))
            {
                float dh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small) + UiTokens.S1);
                UiDraw.TextMiddle(s, at, x, y, dh, w, UiStyle.Small, UiRole.Text2, b.Detail, UiFlags.Truncate);
                y += dh;
            }
            return y;
        }

        private static float LayRule(UiScene s, UiAt at, float x, float y, float w)
        {
            UiDraw.Box(s, at, x, y + UiTokens.S2, w, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            return y + UiTokens.S4;
        }

        private static float LayCategory(UiScene s, UiAt at, UiTipBlock b, float x, float y, float w)
        {
            float h = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader) + UiTokens.S2);
            UiDraw.TextMiddle(s, at, x, y, h, w, UiStyle.SectionHeader, UiRole.Muted, b.Text, UiFlags.Truncate);
            return y + h;
        }

        private static float LayText(UiScene s, UiAt at, UiTipBlock b, float x, float y, float w)
        {
            UiStyle style = b.Strong ? UiStyle.BodyStrong : UiStyle.Body;
            float h = UiDraw.Snap(UiTextRules.WrappedHeight(b.Text, style, w, WrapLines) + UiTokens.S1);
            UiRole ink = b.Ink == UiRole.None ? UiRole.Text : b.Ink;
            UiDraw.Text(s, at, x, y + UiTokens.S1 / 2f, w, style, ink, b.Text, UiFlags.Wrap | UiFlags.Middle | (b.Centre ? UiFlags.Centre : UiFlags.None), WrapLines);
            return y + h;
        }

        private static float LayMeter(UiScene s, UiAt at, UiTipBlock b, float x, float y, float w)
        {
            float fw = UiDraw.Snap(UiTextRules.Width(b.Sub, UiStyle.BodyStrong) + UiTokens.S1);
            UiDraw.TextMiddle(s, at, x, y, RowHeight, w - fw - Join, UiStyle.Body, UiRole.Text2, b.Title, UiFlags.Truncate);
            UiDraw.TextMiddle(s, at, x + w - fw, y, RowHeight, fw, UiStyle.BodyStrong, b.Tone == UiRole.None ? UiRole.Text : ToneInk(b.Tone), b.Sub, UiFlags.Right | UiFlags.Truncate);
            UiDraw.Meter(s, at, x, y + RowHeight, w, BarHeight, b.Value, b.Tone);
            return y + RowHeight + BarHeight + UiTokens.S2;
        }

        private static float LayRows(UiScene s, UiAt at, UiTipBlock b, float x, float y, float w)
        {
            for (int i = 0; i < b.Lines.Count; i++)
            {
                UiTipLine l = b.Lines[i];
                y += UiDraw.InfoRow(s, at, x, y, w, l.Left, l.Right, false, -1, 0, l.Ink == UiRole.None ? UiRole.Text : l.Ink);
            }
            return y;
        }

        private static float LayList(UiScene s, UiAt at, UiTipBlock b, float x, float y, float w)
        {
            for (int i = 0; i < b.Lines.Count; i++)
            {
                string words = b.Lines[i].Left;
                float h = UiDraw.Snap(UiTextRules.WrappedHeight(words, UiStyle.Body, w, 2) + UiTokens.S1);
                UiDraw.Text(s, at, x, y + UiTokens.S1 / 2f, w, UiStyle.Body, b.Lines[i].Ink == UiRole.None ? UiRole.Text : b.Lines[i].Ink, words, UiFlags.Wrap | UiFlags.Middle, 2);
                y += h;
            }
            return y;
        }

        private static float LayChecks(UiScene s, UiAt at, UiTipBlock b, float x, float y, float w)
        {
            for (int i = 0; i < b.Lines.Count; i++)
            {
                UiTipLine l = b.Lines[i];
                UiDraw.Icon(s, at, x, y + (RowHeight - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, l.On ? UiIcon.Check : UiIcon.Close, l.On ? UiRole.Ok : UiRole.Muted, UiFlags.Middle);
                float rw = string.IsNullOrEmpty(l.Right) ? 0f : UiDraw.Snap(UiTextRules.Width(l.Right, UiStyle.Body) + UiTokens.S1);
                float lx = x + UiTokens.IconSmall + UiTokens.S2;
                UiDraw.TextMiddle(s, at, lx, y, RowHeight, w - (lx - x) - (rw > 0f ? rw + Join : 0f), UiStyle.Body, UiRole.Text, l.Left, UiFlags.Truncate);
                if (rw > 0f)
                {
                    UiDraw.TextMiddle(s, at, x + w - rw, y, RowHeight, rw, UiStyle.Body, l.Ink == UiRole.None ? UiRole.Text2 : l.Ink, l.Right, UiFlags.Right | UiFlags.Truncate);
                }
                y += RowHeight;
            }
            return y;
        }

        private static float LayChart(UiScene s, UiAt at, UiTipBlock b, float x, float y, float w)
        {
            float figures = Math.Max(UiTextRules.Width(b.High, UiStyle.Small), UiTextRules.Width(b.Low, UiStyle.Small)) + UiTokens.S1;
            float cw = w - figures - Join;
            UiDraw.Box(s, at, x, y, cw, ChartHeight, UiRole.Band, UiRole.None, UiTokens.RadiusSmall, UiFlags.Decor);
            if (b.Points != null && b.Points.Length >= 2)
            {
                UiDraw.Spark(s, at, x + UiTokens.S2, y + UiTokens.S2, cw - UiTokens.S2 * 2f, ChartHeight - UiTokens.S2 * 2f, b.Points, UiRole.Accent);
            }
            float sh = UiTextRules.LineHeight(UiStyle.Small);
            UiDraw.TextMiddle(s, at, x + cw + Join, y, sh + UiTokens.S1, figures, UiStyle.Small, UiRole.Text2, b.High, UiFlags.Right | UiFlags.Truncate);
            UiDraw.TextMiddle(s, at, x + cw + Join, y + ChartHeight - sh - UiTokens.S1, sh + UiTokens.S1, figures, UiStyle.Small, UiRole.Text2, b.Low, UiFlags.Right | UiFlags.Truncate);
            float ly = y + ChartHeight + UiTokens.S1;
            float half = cw / 2f;
            UiDraw.Text(s, at, x, ly, half, UiStyle.Small, UiRole.Muted, b.From, UiFlags.Truncate);
            UiDraw.Text(s, at, x + half, ly, half, UiStyle.Small, UiRole.Muted, b.To, UiFlags.Right | UiFlags.Truncate);
            return y + ChartHeight + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S2;
        }

        // The fill's role as a figure's ink: a meter's green is the words' green too, as a graded figure is.
        private static UiRole ToneInk(UiRole fill)
        {
            switch (fill)
            {
                case UiRole.Ok: return UiRole.OkText;
                case UiRole.Warn: return UiRole.WarnText;
                case UiRole.Danger: return UiRole.DangerText;
                case UiRole.Accent: return UiRole.AccentText;
            }
            return fill;
        }
    }
}
