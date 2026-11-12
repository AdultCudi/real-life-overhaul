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
        public float AvoidX, AvoidY, AvoidW, AvoidH;   // px of the layout: the mod's minimap card, kept clear of; 0 wide for none
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
    /// The rules of the game's toast stack the kit draws cards over, game-free so EconomyCheck holds them: which entry of the stack is a
    /// toast, which entry a card's press runs, and what the once-a-session line may call a pass. Cost: a few comparisons.
    /// </summary>
    public static class UiNoticeRules
    {
        /// <summary>An entry of the stack is a toast when it is switched on and has the game's Text child; a template or another mod's own child is not.</summary>
        public static bool IsToast(bool standing, bool hasText) => standing && hasText;

        /// <summary>The entry a press runs: a card's number, as drawn, when the command is a card's and the entry is still there; else -1.</summary>
        public static int EntryFor(int command, int arg, int entries) => command == UiNotices.CardCommand && arg >= 0 && arg < entries ? arg : -1;

        /// <summary>Why the cards are not what the once-a-session line should call a pass (null when they are): drawn, and every toast's own entry unseen.</summary>
        public static string Wrong(int toasts, int hidden, int drawn, bool showing)
        {
            if (!showing)
            {
                return "the kit's cards are not showing";
            }
            if (toasts <= 0 || drawn <= 0)
            {
                return "no card was drawn for the " + toasts + " toast(s)";
            }
            if (hidden < toasts)
            {
                return "the game's own entry is still seen in " + (toasts - hidden) + " of " + toasts + " toast(s)";
            }
            return null;
        }
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
            int from = Math.Max(0, m.CardCount - MostCards);
            StackBox(m, frame, out float x, out float y, out _, out _);
            for (int i = from; i < m.CardCount; i++)
            {
                y += Card(s, ref m.Cards[i], i, x, y) + UiTokens.S2;
            }
        }

        /// <summary>Where the stack of cards stands, px of the layout: at the top right under the game's stack top, off the minimap's card; 0 high for none.</summary>
        public static void StackBox(UiNoticeCardsModel m, UiFrame frame, out float x, out float y, out float w, out float h)
        {
            int from = Math.Max(0, m.CardCount - MostCards);
            y = UiDraw.Snap(m.Top);
            x = UiDraw.SnapDown(frame.Width - UiTokens.S4 - CardWidth);
            w = CardWidth;
            h = StackHeight(m, from);
            UiHudRules.Clear(ref x, ref y, w, h, frame.Width, m.AvoidX, m.AvoidY, m.AvoidW, m.AvoidH);
        }

        // The cards shown, the steps between them included: what the stack covers, to keep it off the minimap's card.
        private static float StackHeight(UiNoticeCardsModel m, int from)
        {
            float h = 0f;
            for (int i = from; i < m.CardCount; i++)
            {
                h += CardHeight(ref m.Cards[i]) + (i > from ? UiTokens.S2 : 0f);
            }
            return h;
        }

        /// <summary>A card's height for its words.</summary>
        public static float CardHeight(ref UiNotice n)
        {
            float inner = CardWidth - UiTokens.S4 * 3f - Glyph;
            float h = UiTextRules.LineHeight(UiStyle.Title);
            if (!string.IsNullOrEmpty(n.Body))
            {
                h += UiTokens.S1 + UiTextRules.WrappedHeight(Clip(n.Body, inner), UiStyle.Body, inner, BodyLines);
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
                UiDraw.Text(s, at, cx, ty + lh + UiTokens.S1, cw, UiStyle.Body, UiRole.Text2, Clip(n.Body, cw), UiFlags.Wrap | UiFlags.Middle, BodyLines);
            }
            UiDraw.Node(s, UiAt.Screen, x, y, CardWidth, h, CardCommand, index, card);
            return h;
        }

        // A card's body at most three lines, ending at a whole word: the rest is on the notice list. Once a layout.
        private static string Clip(string body, float width)
        {
            if (UiTextRules.Lines(body, UiStyle.Body, width, BodyLines + 1, out float _) <= BodyLines)
            {
                return body;
            }
            string cut = body;
            int space = cut.LastIndexOf(' ');
            while (space > 0)
            {
                cut = cut.Substring(0, space).TrimEnd(' ', ',', '.', ':', ';');
                if (UiTextRules.Lines(cut + "…", UiStyle.Body, width, BodyLines + 1, out float _) <= BodyLines)
                {
                    return cut + "…";
                }
                space = cut.LastIndexOf(' ');
            }
            return UiTextRules.Cut(body, UiStyle.Body, width);
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
