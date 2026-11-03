using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A notice card at the HUD's top centre: what happened just now, in a few words (a personal goal reached, the day's
    /// summary, a stay in hospital, the casino's word, the way off a bridge). All parts are optional but the kicker.
    /// </summary>
    public sealed class UiHudNoticeModel
    {
        public string Kicker;                  // the strip's words at its left, set in capitals: "Personal goal"
        public string Meta;                    // the strip's quiet word at its right: "Just now", "Sunday, Day 1", "07:30"
        public UiIcon Icon = UiIcon.Info;      // the head's glyph on its tile
        public UiRole IconTone = UiRole.None;  // the tile's tone: None for the slate tile, Danger for a hospital's
        public string Title;                   // "You were taken to hospital"
        public string Body;                    // a sentence or two under it
        public string Figure;                  // "+$5,000" at the head's right; null for none
        public UiRole FigureTone = UiRole.OkText;

        public string BigLabel;                // a summary's left column: "Net for the day" in capitals...
        public string Big;                     // ...its figure, large...
        public UiRole BigTone = UiRole.OkText;
        public string BigUnder;                // ...and a line under it: "Income $3,860, costs $1,745"
        public UiInfoRow[] Rows = new UiInfoRow[0];   // label and value rows: the summary's right column, or under the head
        public int RowCount;

        public string[] Chips = new string[0]; // small chips in their tones under the body ("Missed 06:00 shift")
        public UiRole[] ChipTones = new UiRole[0];
        public int ChipCount;

        public string Footer;                  // the foot's quiet words at its left: "Best earner today: Bloom & Co"
        public string Link;                    // the foot's link at its right: "Open EconoView"
        public UiIcon LinkIcon = UiIcon.None;
        public string Action;                  // a button at the card's foot: "Continue"
        public float Alpha = 1f;               // the game's own fade, in quarters
    }

    /// <summary>
    /// Notice cards: slate cards at the top centre of the HUD, stacked a grid step apart, each with its strip (its kicker in
    /// capitals in the cyan at the left, when at the right), a head (its glyph on a tile, a title over a sentence, a figure
    /// at the right), a summary's two columns (a large figure with its words, label and value rows), chips in their tones,
    /// and a foot with quiet words and a link, or a button. They take no key; the pointer presses a link or a button.
    /// Commands: <see cref="LinkCommand"/> and <see cref="ActionCommand"/> (a card's index). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiHudNotice
    {
        public const int LinkCommand = 230, ActionCommand = 231;
        public const float Width = 560f;          // px: wide enough for a summary's two columns, narrow enough to leave the corners
        private const float TileSize = UiTokens.Control;
        private const float BigColumn = 200f;     // px: the summary's left column

        /// <summary>One card's height.</summary>
        public static float Height(UiHudNoticeModel m)
        {
            float iw = Width - UiTokens.S4 * 2f;
            float h = UiFrame.Strip;
            if (!string.IsNullOrEmpty(m.Title) || !string.IsNullOrEmpty(m.Body))
            {
                h += UiTokens.S3 + HeadHeight(m, iw);
            }
            if (!string.IsNullOrEmpty(m.Big) || m.RowCount > 0)
            {
                h += ColumnsGap(m) + ColumnsHeight(m);
            }
            if (m.ChipCount > 0)
            {
                h += UiTokens.S2 + UiTokens.Badge + UiTokens.S1;
            }
            if (!string.IsNullOrEmpty(m.Footer) || !string.IsNullOrEmpty(m.Link))
            {
                h += UiTokens.S3 + UiTokens.S1 + UiTokens.ControlSmall;
            }
            else if (!string.IsNullOrEmpty(m.Action))
            {
                h += UiTokens.S3 + UiTokens.ControlSmall;
            }
            return UiDraw.Snap(h + UiTokens.S3);
        }

        private static float TextRoom(UiHudNoticeModel m, float iw) => iw - TileSize - UiTokens.S3 - FigureWidth(m) - (FigureWidth(m) > 0f ? UiTokens.S3 : 0f);

        private static float FigureWidth(UiHudNoticeModel m) => string.IsNullOrEmpty(m.Figure) ? 0f : UiDraw.Snap(UiTextRules.Width(m.Figure, UiStyle.Number) + UiTokens.S1);

        private static float HeadHeight(UiHudNoticeModel m, float iw) => Math.Max(TileSize, HeadWords(m, TextRoom(m, iw)));

        // The head's words on the grid: the title's line, then the sentence's lines.
        private static float HeadWords(UiHudNoticeModel m, float tw) =>
            (string.IsNullOrEmpty(m.Title) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong)))
            + (string.IsNullOrEmpty(m.Body) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(m.Body, UiStyle.Body, tw, 3)));

        // A summary straight under the strip stands a larger step down, so its label never reads as the kicker's second line.
        private static float ColumnsGap(UiHudNoticeModel m) => string.IsNullOrEmpty(m.Title) && string.IsNullOrEmpty(m.Body) ? UiTokens.S4 : UiTokens.S3;

        // The large figure's column on the grid: its label, its figure, its words under it.
        private static float BigHeight(UiHudNoticeModel m) => string.IsNullOrEmpty(m.Big) ? 0f
            : (string.IsNullOrEmpty(m.BigLabel) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader))) + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display))
              + (string.IsNullOrEmpty(m.BigUnder) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)));

        private static float ColumnsHeight(UiHudNoticeModel m) => UiDraw.Snap(Math.Max(BigHeight(m), m.RowCount * UiTokens.PairRow));

        /// <summary>The stack's height, the steps between included.</summary>
        public static float StackHeight(UiHudNoticeModel[] cards, int count)
        {
            float h = 0f;
            for (int i = 0; i < count && i < cards.Length; i++)
            {
                h += Height(cards[i]) + (i > 0 ? UiTokens.S2 : 0f);
            }
            return h;
        }

        /// <summary>Lays the cards down from the top centre of a frame (px of the layout); returns the y under the last.</summary>
        public static float LayStack(UiScene s, UiAt at, UiHudNoticeModel[] cards, int count, float frameWidth, float top)
        {
            float x = UiDraw.SnapDown((frameWidth - Width) / 2f), y = top;
            for (int i = 0; i < count && i < cards.Length; i++)
            {
                int first = s.ItemCount;
                y = Lay(s, at, cards[i], i, x, y) + UiTokens.S2;
                float alpha = Math.Max(0f, Math.Min(1f, cards[i].Alpha));
                if (alpha < 1f)
                {
                    for (int k = first; k < s.ItemCount; k++)
                    {
                        s.Items[k].Alpha *= alpha;
                    }
                }
            }
            return y - (count > 0 ? UiTokens.S2 : 0f);
        }

        private static float Lay(UiScene s, UiAt at, UiHudNoticeModel m, int index, float x, float y)
        {
            float h = Height(m);
            int card = UiDraw.Box(s, at, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(card);
            UiDraw.Box(s, on, x, y, Width, UiFrame.Strip, UiRole.Header, UiRole.None, -UiTokens.RadiusPanel, UiFlags.Decor | UiFlags.Chrome);
            float ix = x + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            float mw = string.IsNullOrEmpty(m.Meta) ? 0f : Math.Min(iw * 0.4f, UiDraw.Snap(UiTextRules.Width(m.Meta, UiStyle.Small) + UiTokens.S1));
            if (mw > 0f)
            {
                UiDraw.TextMiddle(s, on, ix + iw - mw, y, UiFrame.Strip, mw, UiStyle.Small, UiRole.Text2, m.Meta, UiFlags.Right | UiFlags.Truncate);
            }
            UiDraw.TextMiddle(s, on, ix, y, UiFrame.Strip, iw - mw - UiTokens.S3, UiStyle.SectionHeader, UiRole.AccentText, m.Kicker, UiFlags.Label | UiFlags.Truncate);
            float cy = y + UiFrame.Strip;
            if (!string.IsNullOrEmpty(m.Title) || !string.IsNullOrEmpty(m.Body))
            {
                cy += UiTokens.S3;
                cy += HeadPart(s, on, m, ix, cy, iw);
            }
            if (!string.IsNullOrEmpty(m.Big) || m.RowCount > 0)
            {
                cy += ColumnsGap(m);
                cy += Columns(s, on, m, ix, cy, iw);
            }
            if (m.ChipCount > 0)
            {
                cy += UiTokens.S2;
                float cx = ix + (string.IsNullOrEmpty(m.Title) ? 0f : TileSize + UiTokens.S3);
                for (int c = 0; c < m.ChipCount && c < m.Chips.Length; c++)
                {
                    if (cx + UiSidePanel.ToneChipWidth(m.Chips[c]) > ix + iw)
                    {
                        break;   // a chip that would cross the card's edge waits; the body says it too
                    }
                    cx += UiSidePanel.ToneChip(s, on, cx, cy, m.Chips[c], c < m.ChipTones.Length ? m.ChipTones[c] : UiRole.None) + UiTokens.S2;
                }
                cy += UiTokens.Badge + UiTokens.S1;
            }
            bool foot = !string.IsNullOrEmpty(m.Footer) || !string.IsNullOrEmpty(m.Link);
            if (foot)
            {
                cy += UiTokens.S3;
                UiDraw.Box(s, on, x, cy, Width, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
                cy += UiTokens.S1;
                float lw = 0f;
                if (!string.IsNullOrEmpty(m.Link))
                {
                    lw = LinkWidth(m);
                    float lx = ix + iw - lw;
                    int face = UiDraw.Box(s, on, lx, cy, lw, UiTokens.ControlSmall, UiRole.None, UiRole.None, UiTokens.RadiusMedium, UiFlags.Right);
                    UiAt inside = on.In(face);
                    float gx = lx + UiTokens.S1;
                    if (m.LinkIcon != UiIcon.None)
                    {
                        UiDraw.Icon(s, inside, gx, cy + (UiTokens.ControlSmall - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, m.LinkIcon, UiRole.AccentText, UiFlags.Middle | UiFlags.Decor);
                        gx += UiTokens.IconSmall + UiTokens.S1;
                    }
                    float ink = UiTextRules.Width(m.Link, UiStyle.SmallStrong);
                    UiDraw.TextMiddle(s, inside, gx, cy, UiTokens.ControlSmall, UiDraw.Snap(ink + UiTokens.S1), UiStyle.SmallStrong, UiRole.AccentText, m.Link, UiFlags.Truncate);
                    UiDraw.Icon(s, inside, gx + ink + UiDraw.LinkGap, cy + (UiTokens.ControlSmall - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Next,
                        UiRole.AccentText, UiFlags.Middle | UiFlags.Decor);
                    int node = UiDraw.Node(s, on, lx, cy, lw, UiTokens.ControlSmall, LinkCommand, index, face);
                    s.Nodes[node].Left = s.Nodes[node].Right = node;
                }
                if (!string.IsNullOrEmpty(m.Footer))
                {
                    UiDraw.TextMiddle(s, on, ix, cy, UiTokens.ControlSmall, iw - lw - (lw > 0f ? UiTokens.S3 : 0f), UiStyle.Small, UiRole.Text2, m.Footer, UiFlags.Truncate);
                }
            }
            else if (!string.IsNullOrEmpty(m.Action))
            {
                cy += UiTokens.S3;
                float bw = Math.Min(iw, Math.Max(UiTokens.S16 * 2f, UiDraw.ButtonWidth(UiButtonKind.Secondary, m.Action, UiIcon.None, UiAction.Count, null)));
                int node = UiDraw.Button(s, on, ix + iw - bw, cy, bw, UiTokens.ControlSmall, UiButtonKind.Secondary, m.Action, UiIcon.None, UiAction.Count, null, ActionCommand, index);
                s.Nodes[node].Left = s.Nodes[node].Right = node;
            }
            return y + h;
        }

        private static float LinkWidth(UiHudNoticeModel m) => UiDraw.Snap(UiTokens.S1 + (m.LinkIcon != UiIcon.None ? UiTokens.IconSmall + UiTokens.S1 : 0f)
                                                                         + UiTextRules.Width(m.Link, UiStyle.SmallStrong) + UiTokens.S1 + UiTokens.IconSmall + UiTokens.S1);

        // The head: the glyph on its tile, the title over the sentence, the figure at the right.
        private static float HeadPart(UiScene s, UiAt at, UiHudNoticeModel m, float x, float y, float w)
        {
            float h = HeadHeight(m, w);
            UiRole fill = m.IconTone == UiRole.Danger ? UiRole.DangerSoft : m.IconTone == UiRole.Ok ? UiRole.OkSoft : m.IconTone == UiRole.Warn ? UiRole.WarnSoft : UiRole.Tile;
            int tile = UiDraw.Box(s, at, x, y, TileSize, TileSize, fill, UiRole.None, UiTokens.RadiusLarge);
            UiDraw.Icon(s, at.In(tile), x + (TileSize - UiTokens.Icon) / 2f, y + (TileSize - UiTokens.Icon) / 2f, UiTokens.Icon, m.Icon,
                m.IconTone == UiRole.Danger ? UiRole.DangerText : UiRole.None, UiFlags.Middle | UiFlags.Centre);
            float fw = FigureWidth(m);
            float tx = x + TileSize + UiTokens.S3, tw = TextRoom(m, w);
            float ty = y + UiDraw.SnapDown(Math.Max(0f, (h - HeadWords(m, tw)) / 2f));
            if (!string.IsNullOrEmpty(m.Title))
            {
                float line = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong));
                UiDraw.TextMiddle(s, at, tx, ty, line, tw, UiStyle.BodyStrong, UiRole.Text, m.Title, UiFlags.Truncate);
                ty += line;
            }
            if (!string.IsNullOrEmpty(m.Body))
            {
                UiDraw.Text(s, at, tx, ty, tw, UiStyle.Body, UiRole.Text2, m.Body, UiFlags.Wrap, 3);
            }
            if (fw > 0f)
            {
                UiDraw.TextMiddle(s, at, x + w - fw, y, h, fw, UiStyle.Number, m.FigureTone, m.Figure, UiFlags.Right);
            }
            return h;
        }

        // A summary's two columns: the large figure with its label over it and its words under it at the left, a rule, then
        // its rows at the right; the rows alone take the card's width.
        private static float Columns(UiScene s, UiAt at, UiHudNoticeModel m, float x, float y, float w)
        {
            float h = ColumnsHeight(m);
            float rx = x, rw = w;
            if (!string.IsNullOrEmpty(m.Big))
            {
                float left = BigColumn;
                float by = y + UiDraw.SnapDown((h - BigHeight(m)) / 2f);
                if (!string.IsNullOrEmpty(m.BigLabel))
                {
                    UiDraw.Text(s, at, x, by, left - UiTokens.S3, UiStyle.SectionHeader, UiRole.Text2, m.BigLabel, UiFlags.Label | UiFlags.Truncate);
                    by += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
                }
                UiDraw.Text(s, at, x, by, left - UiTokens.S3, UiStyle.Display, m.BigTone, m.Big, UiFlags.Truncate);
                by += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
                if (!string.IsNullOrEmpty(m.BigUnder))
                {
                    UiDraw.Text(s, at, x, by, left - UiTokens.S3, UiStyle.Small, UiRole.Text2, m.BigUnder, UiFlags.Truncate);
                }
                if (m.RowCount > 0)
                {
                    UiDraw.Box(s, at, x + left, y, UiTokens.Hairline, h, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
                }
                rx = x + left + UiTokens.S4;
                rw = w - left - UiTokens.S4;
            }
            float ry = y + UiDraw.SnapDown((h - m.RowCount * UiTokens.PairRow) / 2f);
            for (int r = 0; r < m.RowCount && r < m.Rows.Length; r++)
            {
                ref UiInfoRow row = ref m.Rows[r];
                ry += UiDraw.InfoRow(s, at, rx, ry, rw, row.Label, row.Value, false, -1, 0, row.Ink == UiRole.None ? UiRole.Text : row.Ink);
            }
            return h;
        }
    }
}
