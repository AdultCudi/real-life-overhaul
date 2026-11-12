using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The Newsstand's titles: a row for each paper and one for the Pass. Game-free.</summary>
    public static partial class NewsPages
    {
        private const float TitleMark = 72f;         // px: a paper's mark, its initials on a square
        private const float TitleMarkSmall = 48f;    // px: the mark when the window is short
        private const float EditionColumn = 192f;    // px: today's edition: its name over its state
        private const float PriceColumn = 96f;       // px: the price and "a week"
        private const float ChipColumn = 168f;       // px: the chip and the day it renews
        private const float ActionLeast = 120f;      // px: the action's least width

        // The connection and the week's sum over a row for each paper and one for the Pass. A paper's one action is Subscribe, or Stop when
        // it is taken: never both. Each row is as tall as its words, shorter when the window is, and the window never scrolls them away.
        private static void Stand(UiScene s, UiAt at, NewsModel m, float x, float y, float w, float bottom, IUiKeys keys)
        {
            if (m.Titles == null || m.Titles.Length == 0)
            {
                Notice(s, at, m, x, y, w, bottom - y);
                return;
            }
            y = StandHead(s, at, m, x, y, w);
            int papers = 0;
            bool pass = false;
            for (int i = 0; i < m.Titles.Length; i++)
            {
                papers += m.Titles[i].Pass ? 0 : 1;
                pass |= m.Titles[i].Pass;
            }
            float gap = UiTokens.S2;
            float room = bottom - y - (pass ? gap + PassHeight : 0f);
            float rowH = UiDraw.SnapDown(Math.Min(RowFull, (room - gap * papers) / Math.Max(1, papers)));
            bool compact = rowH < RowFull;
            for (int i = 0; i < m.Titles.Length; i++)
            {
                float h = m.Titles[i].Pass ? PassHeight : rowH;
                if (y + h > bottom + 0.5f)
                {
                    break;
                }
                TitleRow(s, at, ref m.Titles[i], i, x, y, w, h, compact, keys);
                y += h + gap;
            }
        }

        private const float RowFull = 128f;           // px: a paper's row with its words in full
        private const float PassHeight = 96f;         // px: the Pass's row

        // The connection with its dot, and the week's sum at the right.
        private static float StandHead(UiScene s, UiAt at, NewsModel m, float x, float y, float w)
        {
            float h = UiTokens.PairRow;
            float nw = string.IsNullOrEmpty(m.Note) ? 0f : UiDraw.Snap(UiTextRules.Width(m.Note, UiStyle.Small) + UiTokens.S1);
            if (!string.IsNullOrEmpty(m.StandLine))
            {
                UiDraw.Circle(s, at, x + UiTokens.S1, y + (h - UiTokens.StatusDot) / 2f, UiTokens.StatusDot, UiTokens.StatusDot, UiRole.Ok, UiFlags.Middle);
                UiDraw.TextMiddle(s, at, x + UiTokens.S1 + UiTokens.StatusDot + UiTokens.S2, y, h, w - UiTokens.StatusDot - UiTokens.S4 - nw - UiTokens.S3, UiStyle.Body, UiRole.Text2,
                    m.StandLine, UiFlags.Truncate);
            }
            if (nw > 0f)
            {
                UiDraw.TextMiddle(s, at, x + w - nw, y, h, nw, UiStyle.Small, UiRole.Text2, m.Note, UiFlags.Right | UiFlags.Truncate);
            }
            return y + h + UiTokens.S2;
        }

        // One title: its mark, its name, what it covers and today's lead; its edition, its price, its chip and its action.
        private static void TitleRow(UiScene s, UiAt at, ref NewsTitle t, int index, float x, float y, float w, float h, bool compact, IUiKeys keys)
        {
            int box = UiDraw.Box(s, at, x, y, w, h, t.Pass ? UiRole.Header : UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(box);
            float pad = UiTokens.S5;
            float mark = compact || t.Pass ? TitleMarkSmall : TitleMark;
            float mx = x + pad, my = y + UiDraw.SnapDown((h - mark) / 2f);
            Mark(s, on, ref t, index, mx, my, mark);
            float right = x + w - pad;
            float actionW = Math.Max(ActionLeast, string.IsNullOrEmpty(t.Action) ? 0f : UiDraw.ButtonWidth(t.ActionKind, t.Action, UiIcon.None, UiAction.Count, keys));
            if (!string.IsNullOrEmpty(t.Action))
            {
                float bh = UiTokens.Control;
                UiDraw.Button(s, on, right - actionW, y + UiDraw.SnapDown((h - bh) / 2f), actionW, bh, t.ActionKind, t.Action, UiIcon.None, UiAction.Count, keys,
                    NewsCommands.Title, index, false);
            }
            right -= actionW + UiTokens.S4;
            float chipX = right - ChipColumn;
            ChipBlock(s, on, ref t, chipX, y, ChipColumn, h);
            right = chipX - UiTokens.S3;
            float priceX = right - PriceColumn;
            PriceBlock(s, on, ref t, priceX, y, PriceColumn, h);
            right = priceX - UiTokens.S3;
            if (!t.Pass && !string.IsNullOrEmpty(t.Edition))
            {
                float ex = right - EditionColumn;
                EditionBlock(s, on, ref t, ex, y, EditionColumn, h);
                right = ex - UiTokens.S3;
            }
            float tx = mx + mark + UiTokens.S4;
            Words(s, on, ref t, tx, y, right - tx, h, compact);
        }

        // The mark: the paper's initials on a square in its own hue, or the Pass's glyph on a tile.
        private static void Mark(UiScene s, UiAt at, ref NewsTitle t, int index, float x, float y, float size)
        {
            if (t.Pass)
            {
                int tile = UiDraw.Tile(s, at, x, y, size, UiRole.None, false);
                UiDraw.Icon(s, at.In(tile), x + (size - UiTokens.Icon) / 2f, y + (size - UiTokens.Icon) / 2f, UiTokens.Icon, t.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                return;
            }
            UiRole hue = t.Tint != UiRole.None ? t.Tint : UiTokens.Chart(index);
            int sq = UiDraw.Box(s, at, x, y, size, size, hue, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiDraw.TextMiddle(s, at.In(sq), x, y, size, size, size >= TitleMark ? UiStyle.Title : UiStyle.BodyStrong, UiRole.OnAccent, t.Mark, UiFlags.Centre);
        }

        // The name, what it covers and today's lead, as many lines as the row has height for.
        private static void Words(UiScene s, UiAt at, ref NewsTitle t, float x, float y, float w, float h, bool compact)
        {
            float nameH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
            float taglineH = compact || string.IsNullOrEmpty(t.Tagline) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            float coversH = string.IsNullOrEmpty(t.Covers) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body));
            bool lead = !string.IsNullOrEmpty(t.Lead) && !t.Pass;
            float leadH = lead ? UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong)) : 0f;
            if (compact)
            {
                coversH = 0f;
            }
            float total = nameH + taglineH + coversH + leadH + (taglineH > 0f ? UiTokens.S1 : 0f) + (coversH > 0f ? UiTokens.S1 : 0f) + (leadH > 0f ? UiTokens.S1 : 0f);
            float cy = y + UiDraw.SnapDown((h - total) / 2f);
            UiDraw.Text(s, at, x, cy, w, UiStyle.Title, UiRole.Text, t.Name, UiFlags.Truncate);
            cy += nameH;
            if (taglineH > 0f)
            {
                UiDraw.Text(s, at, x, cy + UiTokens.S1, w, UiStyle.Small, UiRole.Text2, t.Tagline, UiFlags.Truncate);
                cy += taglineH + UiTokens.S1;
            }
            if (coversH > 0f)
            {
                UiDraw.Text(s, at, x, cy + UiTokens.S1, w, UiStyle.Body, UiRole.Text, t.Covers, UiFlags.Truncate);
                cy += coversH + UiTokens.S1;
            }
            if (leadH > 0f)
            {
                float lw = UiDraw.Snap(UiTextRules.Width(t.LeadLabel ?? string.Empty, UiStyle.SectionHeader) * 1.16f + UiTokens.S2);
                float ly = cy + UiTokens.S1;
                UiDraw.TextMiddle(s, at, x, ly, leadH, lw, UiStyle.SectionHeader, UiRole.Text2, t.LeadLabel, UiFlags.Label | UiFlags.Truncate);
                UiDraw.TextMiddle(s, at, x + lw + UiTokens.S2, ly, leadH, Math.Max(0f, w - lw - UiTokens.S2), UiStyle.BodyStrong, UiRole.Text, t.Lead, UiFlags.Truncate);
            }
        }

        // Today's edition: its name over its state, in the tone of how it stands.
        private static void EditionBlock(UiScene s, UiAt at, ref NewsTitle t, float x, float y, float w, float h)
        {
            float labelH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            float stateH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong));
            float top = y + UiDraw.SnapDown((h - labelH - stateH) / 2f);
            UiDraw.Text(s, at, x, top, w, UiStyle.SectionHeader, UiRole.Text2, t.EditionLabel, UiFlags.Label | UiFlags.Truncate);
            UiDraw.Text(s, at, x, top + labelH + UiTokens.S1, w, UiStyle.BodyStrong, t.EditionTone == UiRole.None ? UiRole.Text2 : t.EditionTone, t.Edition, UiFlags.Truncate);
        }

        // The price large and "a week" under it, centred in its column.
        private static void PriceBlock(UiScene s, UiAt at, ref NewsTitle t, float x, float y, float w, float h)
        {
            float priceH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Title));
            float weekH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            float top = y + UiDraw.SnapDown((h - priceH - weekH) / 2f);
            UiDraw.Text(s, at, x, top, w, UiStyle.Title, UiRole.Text, t.Price, UiFlags.Centre | UiFlags.Truncate);
            UiDraw.Text(s, at, x, top + priceH, w, UiStyle.Small, UiRole.Text2, t.PerWeek, UiFlags.Centre | UiFlags.Truncate);
        }

        // Subscribed as a chip, and the day it renews under it, or nothing at all when it is not taken.
        private static void ChipBlock(UiScene s, UiAt at, ref NewsTitle t, float x, float y, float w, float h)
        {
            bool chip = !string.IsNullOrEmpty(t.Chip);
            bool renews = !string.IsNullOrEmpty(t.Renews);
            if (!chip && !renews)
            {
                return;
            }
            float chipH = UiTokens.Badge + UiTokens.S1;
            float renewH = renews ? UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) : 0f;
            float top = y + UiDraw.SnapDown((h - (chip ? chipH : 0f) - renewH - (chip && renews ? UiTokens.S1 : 0f)) / 2f);
            if (chip)
            {
                UiDraw.Chip(s, at, x + UiDraw.SnapDown((w - ChipWidth(t.Chip)) / 2f), top, t.Chip, UiRole.Ok, w);
                top += chipH + UiTokens.S1;
            }
            if (renews)
            {
                UiDraw.Text(s, at, x, top, w, UiStyle.Small, UiRole.Muted, t.Renews, UiFlags.Centre | UiFlags.Truncate);
            }
        }
    }
}
