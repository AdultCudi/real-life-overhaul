using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One notice: its kind's glyph, what happened, a line more, and how long ago.</summary>
    public struct UiNotice
    {
        public UiIcon Icon;       // the kind's one glyph, in its own colour (UiIconTints)
        public string Header;
        public string Body;       // the rest of it; null when the header says it all
        public string Age;        // "Day 412, 14:32", as the game stamps it; null for none
    }

    /// <summary>The notice cards the game is showing now, newest last, where its own stack stands.</summary>
    public sealed class UiNoticeCardsModel
    {
        public UiNotice[] Cards = new UiNotice[0];
        public int CardCount;
        public float Top = UiTokens.S4;   // px: where the game's stack starts (the mod's panels push it down)
    }

    /// <summary>The notice list: every notice the game kept, newest first, with Clear all.</summary>
    public sealed class UiNoticeListModel
    {
        public string Title;
        public UiNotice[] Rows = new UiNotice[0];
        public int RowCount;
        public int First;                 // the first row in view
        public string Empty;              // the sentence when there are none
        public string ClearLabel;
        public bool CanClear;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The game's notices in the kit's parts. The cards: slate cards where the game's stack sits at the top right, each
    /// with its kind's glyph on a tile in its own colour, the header in the title's style with its age at the right in the
    /// quiet grey, the rest in at most three lines; three at once stand S2 apart. They take no key (a toast never steals
    /// the player's walk): the pointer presses one, which runs the game's own entry. The list: a panel rising from the
    /// status bar's alerts pill at the bottom right, its title strip, the notices as rows of glyph, header, one line and
    /// age (the wheel and the tab keys move it, as the game's list takes no press), Clear all as its second action (P), and
    /// a sentence when there are none. Commands: <see cref="CardCommand"/> and <see cref="ClearCommand"/>. Game-free.
    /// Cost: a layout a change.
    /// </summary>
    public static class UiNotices
    {
        public const int CardCommand = 1, ClearCommand = 3;
        public const int MostCards = 3;           // cards at once: the game's stack shows a few, the rest wait their turn
        public const float CardWidth = 400f;      // px
        public const float ListWidth = 440f;      // px: as the info panel and All apps
        private const int BodyLines = 3;
        private const float Glyph = 40f;          // px: a card's tile
        private const float RowTile = 32f;        // px: a row's tile

        /// <summary>The cards over the world, no status bar of their own: whatever stands under them keeps its own.</summary>
        public static void LayCards(UiScene s, UiNoticeCardsModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("notices", frame.Width, frame.Height, dark);
            s.Pointer = true;   // the keys stay the player's: a card is pressed by pointing
            s.Floats = true;
            float x = UiDraw.SnapDown(frame.Width - UiTokens.S4 - CardWidth), y = UiDraw.Snap(m.Top);
            int from = Math.Max(0, m.CardCount - MostCards);
            for (int i = from; i < m.CardCount; i++)
            {
                y += Card(s, ref m.Cards[i], i, x, y) + UiTokens.S2;
            }
        }

        /// <summary>A card's height for its words.</summary>
        public static float CardHeight(ref UiNotice n)
        {
            float inner = CardWidth - UiTokens.S4 * 3f - Glyph;
            float h = UiTextRules.LineHeight(UiStyle.Title);
            if (!string.IsNullOrEmpty(n.Body))
            {
                h += UiTokens.S1 + UiTextRules.WrappedHeight(n.Body, UiStyle.Body, inner, BodyLines);
            }
            return UiDraw.Snap(Math.Max(Glyph, h) + UiTokens.S4 * 2f);
        }

        private static float Card(UiScene s, ref UiNotice n, int index, float x, float y)
        {
            float h = CardHeight(ref n);
            int card = UiDraw.Box(s, UiAt.Screen, x, y, CardWidth, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = UiAt.Screen.In(card);
            float tx = x + UiTokens.S4, ty = y + UiTokens.S4;
            int tile = UiDraw.Tile(s, at, tx, ty, Glyph, UiIconTints.Of(n.Icon), false);
            float g = UiTokens.S6;
            UiDraw.Icon(s, at.In(tile), tx + (Glyph - g) / 2f, ty + (Glyph - g) / 2f, g, n.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float cx = tx + Glyph + UiTokens.S4, cw = x + CardWidth - UiTokens.S4 - cx;
            float lh = UiTextRules.LineHeight(UiStyle.Title);
            float aw = string.IsNullOrEmpty(n.Age) ? 0f : UiDraw.Snap(UiTextRules.Width(n.Age, UiStyle.Small) + UiTokens.S1);
            UiDraw.Text(s, at, cx, ty, cw - (aw > 0f ? aw + UiTokens.S3 : 0f), UiStyle.Title, UiRole.Text, n.Header, UiFlags.Truncate | UiFlags.Middle);
            if (aw > 0f)
            {
                UiDraw.Text(s, at, cx + cw - aw, ty + (lh - UiTextRules.LineHeight(UiStyle.Small)) / 2f, aw, UiStyle.Small, UiRole.Muted, n.Age, UiFlags.Right | UiFlags.Middle);
            }
            if (!string.IsNullOrEmpty(n.Body))
            {
                UiDraw.Text(s, at, cx, ty + lh + UiTokens.S1, cw, UiStyle.Body, UiRole.Text2, n.Body, UiFlags.Wrap | UiFlags.Middle, BodyLines);
            }
            UiDraw.Node(s, UiAt.Screen, x, y, CardWidth, h, CardCommand, index, card);
            return h;
        }

        /// <summary>How many rows the list shows in its room.</summary>
        public static int RowsFit(UiFrame frame) => Math.Max(3, (int)((ListRoom(frame) - UiFrame.Strip - UiTokens.S2 * 2f - UiTokens.Control - UiTokens.S4) / RowHeight));

        private static float RowHeight => UiDraw.Snap(UiTokens.S2 * 2f + UiTextRules.LineHeight(UiStyle.BodyStrong) + UiTextRules.LineHeight(UiStyle.Small));

        // The list's tallest: the world's room above the status bar, less a margin top and bottom.
        private static float ListRoom(UiFrame frame) => UiDraw.SnapDown(Math.Min(frame.BandY - UiTokens.S16 * 2f, UiTokens.S16 * 9f));

        /// <summary>The notice list over the world, rising from the status bar at the right.</summary>
        public static void LayList(UiScene s, UiNoticeListModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("notice list", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            int fit = RowsFit(frame);
            int shown = Math.Min(fit, Math.Max(0, m.RowCount - m.First));
            float emptyH = UiDraw.Snap(UiTextRules.WrappedHeight(m.Empty, UiStyle.Body, ListWidth - UiTokens.S4 * 2f, 3)) + UiTokens.S4 * 2f;
            float bodyH = m.RowCount == 0 ? emptyH : shown * RowHeight;
            float h = UiDraw.Snap(UiFrame.Strip + UiTokens.S2 + bodyH + UiTokens.S2 + UiTokens.Control + UiTokens.S4);
            float x = UiDraw.SnapDown(frame.Width - UiTokens.S4 - ListWidth), y = UiDraw.SnapDown(frame.BandY - UiTokens.S4 - h);
            int panel = UiDraw.Box(s, UiAt.Screen, x, y, ListWidth, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = UiAt.Screen.In(panel);
            float cy = UiDraw.TitleStrip(s, at, x, y, ListWidth, UiIcon.Bell, m.Title, true) + UiTokens.S2;
            float ix = x + UiTokens.S4, iw = ListWidth - UiTokens.S4 * 2f;
            if (m.RowCount == 0)
            {
                UiDraw.Text(s, at, ix, cy + UiTokens.S4, iw, UiStyle.Body, UiRole.Text2, m.Empty, UiFlags.Wrap | UiFlags.Label | UiFlags.Middle | UiFlags.Centre, 3);
                cy += bodyH;
            }
            else
            {
                for (int i = 0; i < shown; i++)
                {
                    Row(s, at.Arriving(i), ref m.Rows[m.First + i], ix, cy + i * RowHeight, iw);
                }
                if (m.RowCount > shown)
                {
                    // More above or below: the thumb in the panel's right margin says where the rows in view stand.
                    UiDraw.ScrollThumb(s, at, x + ListWidth - UiTokens.S2, cy, bodyH, m.First * RowHeight, m.RowCount * RowHeight);
                }
                cy += bodyH;
            }
            cy += UiTokens.S2;
            UiDraw.Box(s, at, x, cy - UiTokens.Hairline, ListWidth, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            float bw = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.ClearLabel, UiIcon.Trash, UiAction.Option, keys);
            UiDraw.Button(s, at, x + ListWidth - UiTokens.S4 - bw, cy + UiTokens.S2, bw, UiTokens.Control, UiButtonKind.Secondary, m.ClearLabel, UiIcon.Trash,
                UiAction.Option, keys, ClearCommand, 0, !m.CanClear);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // A row: the kind's glyph on its tile, the header and its age, one line of the rest. The game's list takes no press,
        // so neither does the row: the wheel and the tab keys move the list.
        private static void Row(UiScene s, UiAt at, ref UiNotice n, float x, float y, float w)
        {
            float h = RowHeight;
            int tile = UiDraw.Tile(s, at, x, y + (h - RowTile) / 2f, RowTile, UiIconTints.Of(n.Icon), false);
            UiDraw.Icon(s, at.In(tile), x + (RowTile - UiTokens.Icon) / 2f, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, n.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float cx = x + RowTile + UiTokens.S3, cw = w - RowTile - UiTokens.S3;
            float lh = UiTextRules.LineHeight(UiStyle.BodyStrong);
            float ty = y + UiTokens.S2;
            float aw = string.IsNullOrEmpty(n.Age) ? 0f : UiDraw.Snap(UiTextRules.Width(n.Age, UiStyle.Small) + UiTokens.S1);
            UiDraw.Text(s, at, cx, ty, cw - (aw > 0f ? aw + UiTokens.S3 : 0f), UiStyle.BodyStrong, UiRole.Text, n.Header, UiFlags.Truncate | UiFlags.Middle);
            if (aw > 0f)
            {
                UiDraw.Text(s, at, cx + cw - aw, ty + (lh - UiTextRules.LineHeight(UiStyle.Small)) / 2f, aw, UiStyle.Small, UiRole.Muted, n.Age, UiFlags.Right | UiFlags.Middle);
            }
            if (!string.IsNullOrEmpty(n.Body))
            {
                UiDraw.Text(s, at, cx, ty + lh, cw, UiStyle.Small, UiRole.Text2, n.Body, UiFlags.Truncate | UiFlags.Middle);
            }
        }
    }
}
