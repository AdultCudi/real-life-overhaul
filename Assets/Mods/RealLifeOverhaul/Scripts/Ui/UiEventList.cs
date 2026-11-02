using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A dated line of what happened: its kind's glyph in its colour, what it was, when, and what it meant in a sentence or two.</summary>
    public struct UiEventRow
    {
        public UiIcon Icon;
        public UiRole Tint;      // None for the glyph's own colour
        public string Title;
        public string When;      // in the game's words ("Day 412")
        public string Body;
    }

    /// <summary>A dated list's rows, the one the cursor is on, how far it has scrolled, and its words while it holds nothing.</summary>
    public sealed class UiEventListModel
    {
        public UiEventRow[] Rows = new UiEventRow[0];
        public int RowCount;
        public int Selected = -1;
        public float Offset;                    // px scrolled; the block writes back what it took
        public int Reveal = -1;
        public UiIcon EmptyIcon = UiIcon.Calendar;
        public string EmptyTitle, EmptyBody;
        public int List = -1, Clip = -1;        // written back: the rows' list and their scroll region, so the page tells its wheel and reveals apart
    }

    /// <summary>
    /// A dated list, a block of an app window's page: what happened, newest first, each line its kind's glyph on a tile,
    /// what it was with its day at the right and its sentence under it over two lines, cut at a whole word. The rows
    /// scroll in their frame, only those in view laid out, and each is a target so the keys walk and scroll them. For a
    /// district's market events, a company's history, a log. Not for rows the player sorts or acts on (the big table).
    /// Commands: the row's (its index), given by the page. Game-free. Cost: a layout a change, the rows in view only.
    /// </summary>
    public static class UiEventList
    {
        public const float RowHeight = 80f;   // px: a line, its title and two lines of its sentence

        /// <summary>Lays the list into a room; writes back the scroll it took. Returns the list, or -1 while it is empty.</summary>
        public static short Lay(UiScene s, UiAt at, UiEventListModel m, float x, float y, float w, float h, int command)
        {
            float viewH = UiDraw.SnapDown(h);
            if (m.RowCount == 0)
            {
                UiDraw.EmptyState(s, at, x, y, w, viewH, m.EmptyIcon, m.EmptyTitle, m.EmptyBody);
                m.Offset = 0f;
                m.List = m.Clip = -1;
                return -1;
            }
            int rows = m.RowCount;
            float offset = m.Offset;
            if (m.Reveal >= 0 && m.Reveal < rows)
            {
                offset = Math.Min(offset, m.Reveal * RowHeight);
                offset = Math.Max(offset, (m.Reveal + 1) * RowHeight - viewH);
            }
            offset = Math.Max(0f, Math.Min((float)Math.Round(offset / RowHeight) * RowHeight, rows * RowHeight - viewH));
            m.Offset = offset;
            m.Reveal = -1;
            short clip = s.AddClip(x, y, w, viewH, offset);
            int first = Math.Max(0, (int)(offset / RowHeight));
            int last = Math.Min(rows - 1, (int)((offset + viewH - 0.5f) / RowHeight));
            short list = s.AddList(rows, first, Math.Max(0, last - first + 1), clip, m.Selected);
            m.List = list;
            m.Clip = clip;
            s.Clips[clip].ContentHeight = rows * RowHeight;
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            for (int r = first; r <= last; r++)
            {
                Row(s, inClip.Arriving(r - first), ref m.Rows[r], r * RowHeight, w, r == m.Selected, command, r, list);
            }
            UiDraw.ScrollThumb(s, at, x + w - UiTokens.S1, y, viewH, offset, rows * RowHeight);
            return list;
        }

        private static void Row(UiScene s, UiAt at, ref UiEventRow row, float ry, float w, bool sel, int command, int index, short list)
        {
            float h = RowHeight - UiTokens.S1;
            int face = UiDraw.Box(s, at, 0f, ry, w, h, sel ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge, sel ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            float tile = UiTokens.ControlSmall, tx = UiTokens.S2, ty = ry + UiTokens.S2;
            int t = UiDraw.Tile(s, inside, tx, ty, tile, row.Tint, false);
            float g = UiTokens.Icon;
            UiDraw.Icon(s, inside.In(t), tx + (tile - g) / 2f, ty + (tile - g) / 2f, g, row.Icon, row.Tint, UiFlags.Middle | UiFlags.Centre, true);
            float ix = tx + tile + UiTokens.S3, iw = w - UiTokens.S4 - ix;
            float ww = string.IsNullOrEmpty(row.When) ? 0f : UiDraw.Snap(UiTextRules.Width(row.When, UiStyle.Small) + UiTokens.S1);
            float lh = UiTokens.PairRow;
            UiDraw.TextMiddle(s, inside, ix, ty + (tile - lh) / 2f, lh, iw - ww - UiTokens.S3, UiStyle.Title, UiRole.Text, row.Title, UiFlags.Truncate);
            if (ww > 0f)
            {
                UiDraw.TextMiddle(s, inside, ix + iw - ww, ty + (tile - lh) / 2f, lh, ww, UiStyle.Small, sel ? UiRole.Text : UiRole.Text2, row.When, UiFlags.Right);
            }
            UiDraw.Text(s, inside, ix, ty + tile, iw, UiStyle.Small, sel ? UiRole.Text : UiRole.Text2, Lines(row.Body, UiStyle.Small, iw, 2), UiFlags.Wrap, 2);
            UiDraw.Node(s, at, 0f, ry, w, h, command, index, face, false, list, index);
        }

        /// <summary>
        /// Words cut so they wrap into at most a number of lines of a width, ending at a whole word with an ellipsis, as a
        /// player's words are cut on one line. Cost: a few measures a line, once a layout.
        /// </summary>
        public static string Lines(string text, UiStyle style, float width, int most)
        {
            if (string.IsNullOrEmpty(text) || UiTextRules.Lines(text, style, width, 0, out float _) <= most)
            {
                return text;
            }
            for (float room = width * most; room > width; room -= UiTokens.S2)
            {
                string cut = UiTextRules.Cut(text, style, room);
                if (UiTextRules.Lines(cut, style, width, 0, out float _) <= most)
                {
                    return cut;
                }
            }
            return UiTextRules.Cut(text, style, width);
        }
    }
}
