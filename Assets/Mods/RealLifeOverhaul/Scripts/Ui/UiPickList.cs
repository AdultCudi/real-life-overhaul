using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A row of a short list to choose one from: its glyph on a tile, its name with a chip after it, a quiet line under the
    /// name, a meter under that where the row has one (a loan's share paid), and at the right a figure over a note (a fund's
    /// value over its return), each in its own ink.
    /// </summary>
    public struct UiPickRow
    {
        public UiIcon Icon;
        public UiRole Tint;          // None for the icon's own colour
        public string Title;
        public string Sub;           // the quiet line under the name, or null
        public string Chip;          // a state after the name ("High risk"), or null
        public UiRole ChipTone;
        public string Figure;        // the row's one figure at the right, or null
        public UiRole FigureInk;     // None for the soft white
        public string Note;          // a line under the figure, or null
        public UiRole NoteInk;       // None for the quiet grey blue
        public int Meter;            // 0 to 100 filled, or -1 for no meter
        public UiRole MeterTone;
    }

    /// <summary>What a pick list shows: its rows, the one chosen, and how far it has scrolled.</summary>
    public sealed class UiPickListModel
    {
        public string Title;         // its label in capitals over the rows, or null
        public string Count;         // a quiet count at the right of the label, or null
        public UiPickRow[] Rows = new UiPickRow[0];
        public int Chosen = -1;      // the row the page beside it shows, in the cyan at a fifth
        public float Offset;         // px scrolled; the block writes back the offset it took
        public int Reveal = -1;      // a row scrolled into view as it is laid out
    }

    /// <summary>
    /// A short list to choose one thing from (loans, investment funds, accounts), a block of an app window's page as
    /// Football Manager lists a squad's few: each row its glyph on a slate tile in its own colour, its name and a quiet line,
    /// a meter where the row has one, and its figure at the right; the chosen row in the cyan at a fifth, never a border or
    /// a bar. The rows are one list that scrolls inside its room, so up and down walk them and only the rows in view are
    /// laid out. It answers one command, the row's index its argument; the page beside it shows the chosen one. Not for
    /// a table of many columns (<see cref="UiAppWindow.Table"/>, <see cref="UiDataTable"/>) or a list of people (people
    /// rows). Game-free. Cost: a layout a change, the rows in view only.
    /// </summary>
    public static class UiPickList
    {
        public const float RowHeight = 64f;      // px: a tile, two lines and a meter, a grid step of room round them
        private const float RowGap = 4f;         // px: between rows, so two chosen-looking hovers never touch
        private const float FigureShare = 0.34f; // of a row's width, the most its figure and note take

        /// <summary>The height the label over the rows takes, or 0 without one.</summary>
        public static float HeadHeight(UiPickListModel m) => string.IsNullOrEmpty(m.Title) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;

        /// <summary>The height a list of a number of rows takes whole, its label included.</summary>
        public static float Height(UiPickListModel m, int rows) => HeadHeight(m) + Math.Max(1, rows) * (RowHeight + RowGap) - RowGap;

        /// <summary>Lays the list out in a box. Returns its rows' list, or -1 when it has none.</summary>
        public static short Lay(UiScene s, UiAt at, UiPickListModel m, float x, float y, float w, float h, int command)
        {
            float ty = y;
            if (!string.IsNullOrEmpty(m.Title))
            {
                float cw = string.IsNullOrEmpty(m.Count) ? 0f : UiDraw.Snap(UiTextRules.Width(m.Count, UiStyle.Small) + UiTokens.S1);
                UiDraw.Text(s, at, x, ty, w - cw - (cw > 0f ? UiTokens.S3 : 0f), UiStyle.SectionHeader, UiRole.Text, m.Title, UiFlags.Label | UiFlags.Truncate);
                if (cw > 0f)
                {
                    UiDraw.Text(s, at, x + w - cw, ty, cw, UiStyle.Small, UiRole.Muted, m.Count, UiFlags.Right);
                }
                ty += HeadHeight(m);
            }
            int rows = m.Rows.Length;
            if (rows == 0)
            {
                m.Offset = 0f;
                return -1;
            }
            const float step = RowHeight + RowGap;
            float viewH = Math.Max(RowHeight, (float)Math.Floor((y + h - ty + RowGap) / step) * step - RowGap);   // whole rows, never a sliver of one
            float content = rows * step - RowGap;
            float offset = m.Offset;
            if (m.Reveal >= 0 && m.Reveal < rows)
            {
                offset = Math.Min(offset, m.Reveal * step);
                offset = Math.Max(offset, m.Reveal * step + RowHeight - viewH);
            }
            offset = Math.Max(0f, Math.Min((float)Math.Round(offset / step) * step, Math.Max(0f, content - viewH)));
            m.Offset = offset;
            m.Reveal = -1;
            bool scrolls = content > viewH + 0.5f;
            float rw = scrolls ? w - UiTokens.S4 : w;   // a list that scrolls keeps its rows and their ring clear of the thumb
            short clip = s.AddClip(x, ty, w, Math.Min(viewH, content), offset);
            s.Clips[clip].ContentHeight = content;
            int first = Math.Min(rows - 1, (int)(offset / step));
            int last = Math.Min(rows - 1, (int)((offset + viewH - 0.5f) / step));
            short list = s.AddList(rows, first, last - first + 1, clip, m.Chosen);
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            float fw = FigureWidth(m, rw);
            for (int r = first; r <= last; r++)
            {
                RowAt(s, inClip.Arriving(r - first), ref m.Rows[r], r == m.Chosen, r * step, rw, fw, list, r, command);
            }
            UiDraw.ScrollThumb(s, at, x + w - UiTokens.S1, ty, Math.Min(viewH, content), offset, content);
            return list;
        }

        // A row: the tile, the name with its chip and the line under it, the meter, and the figure over its note at the right.
        // One figure column for every row, as wide as the widest figure or note, so every row's meter and words end at one edge.
        private static float FigureWidth(UiPickListModel m, float w)
        {
            float most = 0f;
            for (int i = 0; i < m.Rows.Length; i++)
            {
                most = Math.Max(most, Math.Max(UiTextRules.Width(m.Rows[i].Figure, UiStyle.BodyStrong), UiTextRules.Width(m.Rows[i].Note, UiStyle.Small)));
            }
            return most > 0f ? Math.Min(UiDraw.Snap(most + UiTokens.S1), UiDraw.SnapDown(w * FigureShare)) : 0f;
        }

        private static void RowAt(UiScene s, UiAt at, ref UiPickRow r, bool chosen, float y, float w, float fw, short list, int index, int command)
        {
            int face = UiDraw.Box(s, at, 0f, y, w, RowHeight, chosen ? UiRole.RowSel : UiRole.Band, UiRole.None, UiTokens.RadiusLarge, chosen ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            float tile = UiTokens.IconTile, tx = UiTokens.S3, ty = y + (RowHeight - tile) / 2f;
            int t = UiDraw.Tile(s, inside, tx, ty, tile, r.Tint, false);
            float g = UiTokens.S6;
            UiDraw.Icon(s, inside.In(t), tx + (tile - g) / 2f, ty + (tile - g) / 2f, g, r.Icon, r.Tint, UiFlags.Middle | UiFlags.Centre, true);
            float right = w - UiTokens.S4;
            float lx = tx + tile + UiTokens.S3, lw = right - (fw > 0f ? fw + UiTokens.S4 : 0f) - lx;
            float th = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title)), sh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            bool meter = r.Meter >= 0;
            float block = th + (string.IsNullOrEmpty(r.Sub) ? 0f : UiTokens.S1 + sh) + (meter ? UiTokens.S2 + UiTokens.S1 : 0f);
            float top = y + UiDraw.SnapDown((RowHeight - block) / 2f);
            float chip = string.IsNullOrEmpty(r.Chip) ? 0f : UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(r.Chip, UiStyle.SmallStrong) + UiTokens.S1);
            chip = Math.Min(chip, UiDraw.SnapDown(lw * 0.45f));
            float nameW = Math.Min(UiDraw.Snap(UiTextRules.Width(r.Title, UiStyle.Title) + UiTokens.S1), lw - (chip > 0f ? chip + UiTokens.S3 : 0f));
            UiDraw.TextMiddle(s, inside, lx, top, th, nameW, UiStyle.Title, UiRole.Text, r.Title, UiFlags.Truncate);
            if (chip > 0f)
            {
                UiDraw.Chip(s, inside, lx + nameW + UiTokens.S3, top + UiDraw.SnapDown((th - UiTokens.Badge - UiTokens.S1) / 2f), r.Chip, r.ChipTone, chip);
            }
            float ly = top + th;
            if (!string.IsNullOrEmpty(r.Sub))
            {
                ly += UiTokens.S1;
                UiDraw.TextMiddle(s, inside, lx, ly, sh, lw, UiStyle.Small, chosen ? UiRole.Text : UiRole.Text2, r.Sub, UiFlags.Truncate);   // the quiet grey does not read on the chosen tone
                ly += sh;
            }
            if (meter)
            {
                UiDraw.Meter(s, inside, lx, ly + UiTokens.S2, lw, UiTokens.S1, r.Meter, r.MeterTone == UiRole.None ? UiRole.Accent : r.MeterTone);
            }
            if (fw > 0f && (!string.IsNullOrEmpty(r.Figure) || !string.IsNullOrEmpty(r.Note)))
            {
                bool both = !string.IsNullOrEmpty(r.Figure) && !string.IsNullOrEmpty(r.Note);
                float fh = both ? th + UiTokens.S1 + sh : string.IsNullOrEmpty(r.Figure) ? sh : th;
                float fy = y + UiDraw.SnapDown((RowHeight - fh) / 2f);
                if (!string.IsNullOrEmpty(r.Figure))
                {
                    UiDraw.TextMiddle(s, inside, right - fw, fy, th, fw, UiStyle.BodyStrong, r.FigureInk == UiRole.None ? UiRole.Text : r.FigureInk, r.Figure, UiFlags.Right | UiFlags.Truncate);
                    fy += th + UiTokens.S1;
                }
                if (!string.IsNullOrEmpty(r.Note))
                {
                    UiRole ink = r.NoteInk != UiRole.None ? r.NoteInk : chosen ? UiRole.Text : UiRole.Text2;
                    UiDraw.TextMiddle(s, inside, right - fw, fy, sh, fw, UiStyle.Small, ink, r.Note, UiFlags.Right | UiFlags.Truncate);
                }
            }
            UiDraw.Node(s, at, 0f, y, w, RowHeight, command, index, face, false, list, index);
        }
    }
}
