using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The cards of the Economy app's pages that carry rows and tiles: the overview's headline tiles and facts, and the panels (costs, bank, wholesale, books,
    /// departments, tax, people) the old app drew under its charts. Game-free. Cost: a layout when the model changes.
    /// </summary>
    public static partial class NewsPages
    {
        private const float MeterH = UiTokens.S2;                         // px: a panel row's meter
        private static float CardHead => UiTokens.S3 + UiTokens.PairRow + UiTokens.S2;   // px: a card's label band, as Card lays it

        /// <summary>The height a card needs to hold a table of its rows (at least <paramref name="least"/>), so a short table does not stand in a card the height of the page.</summary>
        private static float TableNeed(UiDataTableModel t, int least)
        {
            if (t == null)
            {
                return 0f;
            }
            int rows = Math.Max(least, t.RowCount);
            return UiDraw.Snap(CardHead + UiDataTable.Chrome(t) + rows * UiDataTable.RowHeight(t) + UiTokens.S2);
        }

        // ---------------------------------------------------------------- the overview's chips and tiles

        // The chips of the facts that frame the economy (the month, the next rate decision, the busiest district), and at their right the cycle's effects.
        // Returns the height it took with the gap under it, 0 for none.
        private static float FactsRow(UiScene s, UiAt at, NewsModel m, float x, float y, float w)
        {
            bool chips = m.Facts != null && m.Facts.Length > 0;
            if (!chips && string.IsNullOrEmpty(m.FactsNote))
            {
                return 0f;
            }
            float h = UiTokens.Badge + UiTokens.S1;
            float cx = x;
            for (int i = 0; chips && i < m.Facts.Length; i++)
            {
                float cw = UiDraw.ChipWidth(m.Facts[i]);
                if (cx + cw > x + w)
                {
                    break;
                }
                UiRole dot = m.FactTones != null && i < m.FactTones.Length ? m.FactTones[i] : i == m.Facts.Length - 1 && m.Facts.Length > 2 ? UiRole.Accent : UiRole.Muted;
                cx += UiDraw.Chip(s, at, cx, y, m.Facts[i], dot) + UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(m.FactsNote))
            {
                float room = x + w - cx - UiTokens.S3;
                float nw = Math.Min(UiDraw.Snap(UiTextRules.Width(m.FactsNote, UiStyle.Small) + UiTokens.S1), room);
                if (nw > UiTokens.S16)
                {
                    UiDraw.TextMiddle(s, at, x + w - nw, y, h, nw, UiStyle.Small, UiRole.Text2, m.FactsNote, UiFlags.Right | UiFlags.Truncate);
                }
            }
            return h + UiTokens.S3;
        }

        // Whether both changes of every tile sit side by side in a slot of this width: when one tile's do not, all stack, so the tiles stay one height.
        private static bool TilesSideBySide(NewsTile[] tiles, float slot)
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                float both = Pill(tiles[i].Short) + Pill(tiles[i].Long) + UiTokens.S3;
                if (both > slot - UiTokens.S4 * 2f)
                {
                    return false;
                }
            }
            return true;
        }

        private static float Pill(string text) => UiTokens.IconSmall + UiTokens.S1 + UiDraw.Snap(UiTextRules.Width(text ?? string.Empty, UiStyle.SmallStrong) + UiTokens.S1);

        private static float TilesHeight(NewsTile[] tiles, float w, float gap)
        {
            if (tiles == null || tiles.Length == 0)
            {
                return 0f;
            }
            float slot = UiDraw.SnapDown((w - gap * (tiles.Length - 1)) / tiles.Length);
            float pills = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SmallStrong)) + UiTokens.S1;
            return UiDraw.Snap(UiTokens.S3 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S1 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Number)) + UiTokens.S2
                + (TilesSideBySide(tiles, slot) ? pills : pills * 2f) + UiTokens.S3);
        }

        // The tiles in one row: each its name in capitals, its figure with its days beside it, and how it moved over a week and over a month. A press opens its tab.
        private static void TilesLay(UiScene s, UiAt at, NewsTile[] tiles, float x, float y, float w, float h, float gap)
        {
            int n = tiles.Length;
            float slot = UiDraw.SnapDown((w - gap * (n - 1)) / n);
            bool side = TilesSideBySide(tiles, slot);
            for (int i = 0; i < n; i++)
            {
                float tx = x + i * (slot + gap);
                int box = UiDraw.Box(s, at, tx, y, slot, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.None);
                UiAt on = at.In(box);
                float ix = tx + UiTokens.S4, iw = slot - UiTokens.S4 * 2f;
                float labelH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
                float numberH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Number));
                float pillH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SmallStrong)) + UiTokens.S1;
                float cy = y + UiTokens.S3;
                UiDraw.Text(s, on, ix, cy, iw, UiStyle.SectionHeader, UiRole.Text2, tiles[i].Label, UiFlags.Label | UiFlags.Truncate);
                cy += labelH + UiTokens.S1;
                float vw = Math.Min(iw, UiDraw.Snap(UiTextRules.Width(tiles[i].Value ?? string.Empty, UiStyle.Number) + UiTokens.S1));
                UiDraw.Text(s, on, ix, cy, vw, UiStyle.Number, UiRole.Text, tiles[i].Value, UiFlags.Truncate);
                if (UiDraw.Drawn(tiles[i].Line) && iw - vw > UiTokens.S16)
                {
                    UiDraw.Spark(s, on, ix + vw + UiTokens.S3, cy, iw - vw - UiTokens.S3, numberH, tiles[i].Line, UiRole.Accent);
                }
                cy += numberH + UiTokens.S2;
                ChangeMark(s, on, ix, cy, side ? Pill(tiles[i].Short) : iw, pillH, tiles[i].Short, tiles[i].ShortWay, tiles[i].ShortTone);
                if (side)
                {
                    ChangeMark(s, on, ix + Pill(tiles[i].Short) + UiTokens.S3, cy, iw - Pill(tiles[i].Short) - UiTokens.S3, pillH, tiles[i].Long, tiles[i].LongWay, tiles[i].LongTone);
                }
                else
                {
                    ChangeMark(s, on, ix, cy + pillH, iw, pillH, tiles[i].Long, tiles[i].LongWay, tiles[i].LongTone);
                }
                UiDraw.Node(s, at, tx, y, slot, h, NewsCommands.Tab, tiles[i].Tab, box);
            }
        }

        // A change as an arrow and its words in the colour of what it means for the player; a change that is none or unknown has no arrow and a quiet colour.
        private static void ChangeMark(UiScene s, UiAt at, float x, float y, float w, float h, string text, int way, UiRole tone)
        {
            float lead = way != 0 ? UiTokens.IconSmall + UiTokens.S1 : 0f;
            if (way != 0)
            {
                UiDraw.Icon(s, at, x, y + (h - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, way > 0 ? UiIcon.TrendUp : UiIcon.TrendDown, tone, UiFlags.Middle);
            }
            UiDraw.TextMiddle(s, at, x + lead, y, h, Math.Max(0f, w - lead), UiStyle.SmallStrong, tone == UiRole.None ? UiRole.Text2 : tone, text, UiFlags.Truncate);
        }

        // ---------------------------------------------------------------- panels

        private static bool Stacked(NewsRow r, float w)
        {
            return UiTextRules.Width(r.Label ?? string.Empty, UiStyle.Body) + UiTextRules.Width(r.Value ?? string.Empty, UiStyle.BodyStrong) + UiTokens.S6 > w;
        }

        private static float RowHeight(NewsRow r, float w)
        {
            float h = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body));
            if (Stacked(r, w))
            {
                h += UiDraw.Snap(UiTextRules.WrappedHeight(r.Value, UiStyle.BodyStrong, w, 2));
            }
            if (r.Bar)
            {
                h += UiTokens.S2 + MeterH;
            }
            if (!string.IsNullOrEmpty(r.Detail))
            {
                h += UiTokens.S1 + UiDraw.Snap(UiTextRules.WrappedHeight(r.Detail, UiStyle.Small, w, 2));
            }
            return UiDraw.Snap(h + UiTokens.S3);
        }

        /// <summary>The height a panel needs for all its rows at this width.</summary>
        private static float PanelNeed(NewsPanel p, float w)
        {
            float iw = w - UiTokens.S4 * 2f;
            float h = CardHead;
            if (!string.IsNullOrEmpty(p.Note))
            {
                h += UiDraw.Snap(UiTextRules.WrappedHeight(p.Note, UiStyle.Small, iw, 3)) + UiTokens.S2;
            }
            for (int i = 0; p.Rows != null && i < p.Rows.Length; i++)
            {
                h += RowHeight(p.Rows[i], iw);
            }
            if (!string.IsNullOrEmpty(p.Foot))
            {
                h += UiTokens.S1 + UiDraw.Snap(UiTextRules.WrappedHeight(p.Foot, UiStyle.Small, iw, 3));
            }
            return UiDraw.Snap(h + UiTokens.S2);
        }

        // A card of rows: the title, a quiet sentence, then each row as its label and figure with a meter and a quiet line where it has them; a room that holds
        // fewer rows than there are says how many are left.
        private static void PanelLay(UiScene s, UiAt at, NewsPanel p, float x, float y, float w, float h)
        {
            UiAt on = Card(s, at, x, y, w, h, p.Title, null, out float cy);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f, limit = y + h - UiTokens.S2;
            if (!string.IsNullOrEmpty(p.Note))
            {
                float nh = UiDraw.Snap(UiTextRules.WrappedHeight(p.Note, UiStyle.Small, iw, 3));
                UiDraw.Text(s, on, ix, cy, iw, UiStyle.Small, UiRole.Text2, p.Note, UiFlags.Wrap | UiFlags.Truncate, 3);
                cy += nh + UiTokens.S2;
            }
            float footH = string.IsNullOrEmpty(p.Foot) ? 0f : UiTokens.S1 + UiDraw.Snap(UiTextRules.WrappedHeight(p.Foot, UiStyle.Small, iw, 3));
            int n = p.Rows == null ? 0 : p.Rows.Length;
            var heights = new float[n];
            for (int i = 0; i < n; i++)
            {
                heights[i] = RowHeight(p.Rows[i], iw);
            }
            float moreH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            int fit = NewsCardRules.RowsThatFit(heights, limit - footH - cy, out int left);
            if (left > 0 && !string.IsNullOrEmpty(p.MoreText))
            {
                fit = NewsCardRules.RowsThatFit(heights, limit - footH - moreH - cy, out left);
            }
            for (int i = 0; i < fit; i++)
            {
                Row(s, on, p.Rows[i], ix, cy, iw, heights[i]);
                cy += heights[i];
            }
            if (left > 0 && !string.IsNullOrEmpty(p.MoreText))
            {
                UiDraw.Text(s, on, ix, cy, iw, UiStyle.Small, UiRole.Muted, p.MoreText.Replace("{n}", left.ToString()), UiFlags.Truncate);
                cy += moreH;
            }
            if (footH > 0f && cy + footH <= limit + 0.5f)
            {
                UiDraw.Text(s, on, ix, cy + UiTokens.S1, iw, UiStyle.Small, UiRole.Text2, p.Foot, UiFlags.Wrap | UiFlags.Truncate, 3);
            }
        }

        private static void Row(UiScene s, UiAt at, NewsRow r, float x, float y, float w, float h)
        {
            float line = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body));
            UiRole ink = r.Ink == UiRole.None ? UiRole.Text : r.Ink;
            float cy = y;
            if (Stacked(r, w))
            {
                UiDraw.TextMiddle(s, at, x, cy, line, w, UiStyle.Body, UiRole.Text2, r.Label, UiFlags.Truncate);
                cy += line;
                float vh = UiDraw.Snap(UiTextRules.WrappedHeight(r.Value, UiStyle.BodyStrong, w, 2));
                UiDraw.Text(s, at, x, cy, w, UiStyle.BodyStrong, ink, r.Value, UiFlags.Wrap | UiFlags.Truncate, 2);
                cy += vh;
            }
            else
            {
                float vw = UiDraw.Snap(UiTextRules.Width(r.Value ?? string.Empty, UiStyle.BodyStrong) + UiTokens.S1);
                UiDraw.TextMiddle(s, at, x, cy, line, w - vw - UiTokens.S3, UiStyle.Body, UiRole.Text2, r.Label, UiFlags.Truncate);
                UiDraw.TextMiddle(s, at, x + w - vw, cy, line, vw, UiStyle.BodyStrong, ink, r.Value, UiFlags.Right | UiFlags.Truncate);
                cy += line;
            }
            if (r.Bar)
            {
                cy += UiTokens.S2;
                UiDraw.Box(s, at, x, cy, w, MeterH, UiRole.Track, UiRole.None, UiTokens.RadiusSmall, UiFlags.Decor | UiFlags.Share);
                float len = Math.Max(0f, Math.Min(1f, r.Share)) * w;
                if (len >= 1f)
                {
                    UiDraw.Box(s, at, x, cy, len, MeterH, r.Fill == UiRole.None ? UiRole.Accent : r.Fill, UiRole.None, UiTokens.RadiusSmall, UiFlags.Decor | UiFlags.Share);
                }
                if (r.MidAt >= 0f && r.MidAt <= 1f)
                {
                    UiDraw.Box(s, at, x + r.MidAt * w - UiTokens.Hairline, cy - UiTokens.S1, UiTokens.Hairline * 2f, MeterH + UiTokens.S2, UiRole.Text2, UiRole.None, 0f,
                        UiFlags.Decor | UiFlags.Share);
                }
                cy += MeterH;
            }
            if (!string.IsNullOrEmpty(r.Detail))
            {
                UiDraw.Text(s, at, x, cy + UiTokens.S1, w, UiStyle.Small, r.Ink == UiRole.DangerText ? UiRole.DangerText : UiRole.Muted, r.Detail, UiFlags.Wrap | UiFlags.Truncate, 2);
            }
        }

        // Panels one under the other in a column: each its own height, or all of them shrunk alike when the column is shorter than they need.
        private static void StackPanels(UiScene s, UiAt at, NewsPanel[] panels, int from, int to, float x, float y, float w, float h)
        {
            int n = to - from;
            if (n <= 0)
            {
                return;
            }
            float gap = UiTokens.S3;
            var need = new float[n];
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                need[i] = PanelNeed(panels[from + i], w);
                sum += need[i];
            }
            float room = h - gap * (n - 1);
            float scale = sum > room && sum > 0f ? room / sum : 1f;
            float cy = y;
            for (int i = 0; i < n; i++)
            {
                float ph = UiDraw.SnapDown(need[i] * scale);
                PanelLay(s, at, panels[from + i], x, cy, w, ph);
                cy += ph + gap;
            }
        }

        /// <summary>The height the panels of a column need together at this width, with the gaps between them.</summary>
        private static float StackNeed(NewsPanel[] panels, int from, int to, float w)
        {
            float sum = 0f;
            for (int i = from; i < to; i++)
            {
                sum += PanelNeed(panels[i], w) + (i > from ? UiTokens.S3 : 0f);
            }
            return sum;
        }
    }
}
