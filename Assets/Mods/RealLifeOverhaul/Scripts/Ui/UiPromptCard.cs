using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What a box over a place in the world says, as the world's mockup draws it: the thing's glyph on a tile (or a person's
    /// initials on their disc), its name over one line about it with the state at that line's end in its own colour, then
    /// up to four actions each led by the game's key. One model for the game's OverlayUI (its first line, its second line's
    /// left and right, its ButtonInfo) and the mod's WorldPanel and DoorPanel, which stand in it.
    /// </summary>
    public sealed class UiPromptCardModel
    {
        public const int MostActions = 4;

        public string Title;                                       // "Golden Bowl", "Lift", "Broadway and 45th"
        public string Sub;                                         // "Noodle house, open until 23:00", a line each for a bus stop's routes; null for none
        public string State;                                       // the game's right-hand words ("Closed"), at the end of the sub line; null for none
        public UiRole StateTone = UiRole.Text2;                    // their colour, from the game's own (green open, red closed)
        public UiIcon Icon = UiIcon.Building;
        public string Face;                                        // a person's name: their initials on a disc of their hue in place of the tile
        public float Hue;
        public UiPromptAction[] Actions = new UiPromptAction[0];
        public int ActionCount;
    }

    /// <summary>
    /// The world prompt as the mockup has it (previews/ui/mockups/world/world_prompts): a narrow slate card beside its point,
    /// its head a glyph tile or a face, the name in strong words and a quiet line under it, then its actions as rows parted
    /// by hairlines, each led by the game's key on its cap; the one the game's Interact does reads strong with its cap in the
    /// cyan, a greyed one is dimmed with the game's reason at its right. The game reads the keys; the mouse presses a row.
    /// No title strip and no close: the box goes when the player walks away, as the game's own does. Commands:
    /// <see cref="ActionCommand"/> (an action's index). Game-free. Cost: a layout a change; following its point moves it.
    /// </summary>
    public static class UiPromptCard
    {
        public const int ActionCommand = 206;
        public const float LeastWidth = 248f;     // px: a verb and a short name still stand clear of the edges
        public const float MostWidth = 336f;      // px: as narrow as a game's world box; a long address ends in an ellipsis
        private const float Pad = UiTokens.S3;    // px: the card's inner edge
        private const float Tile = 36f;           // px: the head's glyph tile or face
        private const float ActionRow = 36f;      // px: an action, its cap with room above and below
        private const int MostSubLines = 7;       // the sub's lines shown (a bus stop's routes, a building's facts, one a line); the rest end in an ellipsis

        private static int Actions(UiPromptCardModel m) => Math.Min(UiPromptCardModel.MostActions, Math.Min(m.ActionCount, m.Actions.Length));

        /// <summary>The card's width for its words: as wide as its longest line needs, between the least and the most.</summary>
        public static float Width(UiPromptCardModel m)
        {
            float sub = 0f;
            int lines = SubLines(m.Sub);
            for (int i = 0, start = 0; i < lines; i++)
            {
                int end = LineEnd(m.Sub, start);
                float w = UiTextRules.Width(m.Sub.Substring(start, end - start), UiStyle.Small);
                sub = Math.Max(sub, i == 0 && !string.IsNullOrEmpty(m.State) ? w + UiTextRules.Width(", " + m.State, UiStyle.SmallStrong) : w);
                start = end + 1;
            }
            if (lines == 0 && !string.IsNullOrEmpty(m.State))
            {
                sub = UiTextRules.Width(m.State, UiStyle.SmallStrong);
            }
            float head = Pad + Tile + UiTokens.S3 + Math.Max(UiTextRules.Width(m.Title, UiStyle.BodyStrong), sub) + Pad;
            float caps = Caps(m), rows = 0f;
            for (int i = 0; i < Actions(m); i++)
            {
                ref UiPromptAction a = ref m.Actions[i];
                float reason = a.Disabled && !string.IsNullOrEmpty(a.Reason) ? UiTextRules.Width(a.Reason, UiStyle.SmallStrong) + UiTokens.S3 : 0f;
                rows = Math.Max(rows, Pad + caps + UiTokens.S3 + UiTextRules.Width(a.Label, a.Main ? UiStyle.BodyStrong : UiStyle.Body) + reason + Pad);
            }
            return Math.Max(LeastWidth, Math.Min(MostWidth, UiDraw.Snap(Math.Max(head, rows) + UiTokens.S1)));
        }

        /// <summary>The card's height: its head, and its actions under a hairline each.</summary>
        public static float Height(UiPromptCardModel m) => Head(m) + Actions(m) * ActionRow;

        // The head: the tile's height with room round it, taller when the sub runs to more lines than one.
        private static float Head(UiPromptCardModel m)
        {
            int lines = SubLines(m.Sub);
            if (lines < 2)
            {
                return Tile + UiTokens.S2 * 2f;
            }
            return UiDraw.Snap(Math.Max(Tile, Block(lines))) + UiTokens.S2 * 2f;
        }

        // The name and the sub's lines under it, px.
        private static float Block(int lines) =>
            UiTextRules.LineHeight(UiStyle.BodyStrong) + (lines > 0 ? UiTokens.S1 / 2f + lines * UiTextRules.LineHeight(UiStyle.Small) : 0f);

        // The sub's lines, as the caller broke them, at most MostSubLines; 0 for none.
        private static int SubLines(string sub)
        {
            if (string.IsNullOrEmpty(sub))
            {
                return 0;
            }
            int lines = 1;
            for (int i = 0; i < sub.Length && lines < MostSubLines; i++)
            {
                lines += sub[i] == '\n' ? 1 : 0;
            }
            return lines;
        }

        // Where the line starting at start ends: its line break, or the words' end.
        private static int LineEnd(string sub, int start)
        {
            int end = sub.IndexOf('\n', start);
            return end < 0 ? sub.Length : end;
        }

        // The widest key among the actions, so every label starts on one edge whatever its key.
        private static float Caps(UiPromptCardModel m)
        {
            float caps = 0f;
            for (int i = 0; i < Actions(m); i++)
            {
                caps = m.Actions[i].Hint ? caps : Math.Max(caps, UiDraw.GameKeyWidth(m.Actions[i].Key));   // a hint has no key
            }
            return caps;
        }

        /// <summary>The card at a place (its top left, px of the layout); returns its panel.</summary>
        public static int Lay(UiScene s, UiAt at, UiPromptCardModel m, float x, float y)
        {
            float w = Width(m), h = Height(m);
            int panel = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            float ty = y + UiTokens.S2;
            if (!string.IsNullOrEmpty(m.Face))
            {
                UiDraw.Avatar(s, on, x + Pad, ty, Tile, m.Face, m.Hue);
            }
            else
            {
                int tile = UiDraw.Tile(s, on, x + Pad, ty, Tile, UiRole.None, false);
                float g = UiTokens.Icon;
                UiDraw.Icon(s, on.In(tile), x + Pad + (Tile - g) / 2f, ty + (Tile - g) / 2f, g, m.Icon, UiRole.Accent, UiFlags.Middle | UiFlags.Centre, true);
            }
            float tx = x + Pad + Tile + UiTokens.S3, tw = x + w - Pad - tx;
            int lines = Math.Max(SubLines(m.Sub), string.IsNullOrEmpty(m.State) ? 0 : 1);
            float lh = UiTextRules.LineHeight(UiStyle.BodyStrong), sh = UiTextRules.LineHeight(UiStyle.Small);
            float head = Head(m) - UiTokens.S2 * 2f, block = Block(lines), by = ty + (head - block) / 2f;
            UiDraw.Text(s, on, tx, by, tw, UiStyle.BodyStrong, UiRole.Text, m.Title, UiFlags.Truncate | UiFlags.Middle);
            float ly = by + lh + UiTokens.S1 / 2f;
            for (int i = 0, start = 0; i < lines; i++)
            {
                int end = string.IsNullOrEmpty(m.Sub) ? 0 : LineEnd(m.Sub, start);
                bool last = i == lines - 1 && end < (m.Sub ?? string.Empty).Length;   // more lines than are shown: this one ends in an ellipsis
                string line = string.IsNullOrEmpty(m.Sub) ? null : m.Sub.Substring(start, (last ? m.Sub.Length : end) - start).Replace('\n', ' ');
                SubLine(s, on, line, i == 0 ? m.State : null, m.StateTone, tx, ly, tw);
                ly += sh;
                start = end + 1;
            }
            float caps = Caps(m), ay = y + Head(m);
            for (int i = 0; i < Actions(m); i++)
            {
                Action(s, on, ref m.Actions[i], i, x, ay, w, caps);
                UiDraw.Box(s, on, x, ay, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);   // over the row, so its hover never hides the rule
                ay += ActionRow;
            }
            return panel;
        }

        // A line under the name: the game's left words in the quiet ink, its right words after the first in their own colour.
        private static void SubLine(UiScene s, UiAt at, string words, string stateWords, UiRole tone, float x, float y, float w)
        {
            bool state = !string.IsNullOrEmpty(stateWords);
            string tail = state ? (string.IsNullOrEmpty(words) ? stateWords : ", " + stateWords) : null;
            float sw = state ? Math.Min(w, UiDraw.Snap(UiTextRules.Width(tail, UiStyle.SmallStrong) + UiTokens.S1)) : 0f;
            float lw = string.IsNullOrEmpty(words) ? 0f : Math.Min(UiDraw.SnapDown(w - sw), UiDraw.Snap(UiTextRules.Width(words, UiStyle.Small) + UiTokens.S1));
            if (lw > 0f)
            {
                UiDraw.Text(s, at, x, y, lw, UiStyle.Small, UiRole.Text2, words, UiFlags.Truncate | UiFlags.Middle);
            }
            if (state)
            {
                UiDraw.Text(s, at, x + lw, y, sw, UiStyle.SmallStrong, tone == UiRole.None ? UiRole.Text2 : tone, tail, UiFlags.Truncate | UiFlags.Middle | UiFlags.Overflow);
            }
        }

        // An action's row: the game's key on its cap (the cyan one for Interact's), its words, a greyed one dimmed with its reason.
        private static void Action(UiScene s, UiAt at, ref UiPromptAction a, int index, float x, float y, float w, float caps)
        {
            if (a.Hint)
            {
                // How, not what: the words alone in the quiet ink, where a key's words would start.
                float hx = x + Pad + (caps > 0f ? caps + UiTokens.S3 : 0f);
                UiDraw.TextMiddle(s, at, hx, y, ActionRow, x + w - Pad - hx, UiStyle.Body, UiRole.Text2, a.Label, UiFlags.Truncate);
                return;
            }
            bool main = a.Main && !a.Disabled;
            int drawn = s.ItemCount;
            int face = UiDraw.Box(s, at, x, y, w, ActionRow, UiRole.None, UiRole.None, 0f);
            UiAt inside = at.In(face);
            float kx = x + Pad, cx = kx + (caps > 0f ? caps + UiTokens.S3 : 0f);
            float rw = a.Disabled && !string.IsNullOrEmpty(a.Reason) ? UiDraw.Snap(UiTextRules.Width(a.Reason, UiStyle.SmallStrong) + UiTokens.S1) : 0f;
            UiDraw.TextMiddle(s, inside, cx, y, ActionRow, x + w - Pad - cx - (rw > 0f ? rw + UiTokens.S3 : 0f), main ? UiStyle.BodyStrong : UiStyle.Body, UiRole.Text, a.Label,
                UiFlags.Label | UiFlags.Truncate);
            int reason = -1;
            if (rw > 0f)
            {
                reason = UiDraw.TextMiddle(s, inside, x + w - Pad - rw, y, ActionRow, rw, UiStyle.SmallStrong, UiRole.WarnText, a.Reason, UiFlags.Right | UiFlags.Truncate);
            }
            int node = UiDraw.Node(s, at, x, y, w, ActionRow, ActionCommand, index, face, a.Disabled);
            // After the target: the game, not the row, answers the key.
            float kw = UiDraw.GameKeyWidth(a.Key);
            Cap(s, inside, kx + UiDraw.SnapDown((caps - kw) / 2f), y + (ActionRow - UiTokens.KeyCap) / 2f, a.Key, main, a.Disabled);
            if (a.Disabled)
            {
                UiDraw.Dim(s, drawn, node);
                if (reason >= 0)
                {
                    s.Items[reason].Alpha = 1f;   // the reason reads whole: it says why the rest is dim
                }
            }
        }

        // The game's key on its cap: the main action's filled cyan with the dark ink, the others on the panel's track.
        private static void Cap(UiScene s, UiAt at, float x, float y, UiGameKey key, bool main, bool dim)
        {
            if (!main || dim)
            {
                UiDraw.GameKey(s, at, x, y, key, false, dim);
                return;
            }
            if (!key.Shown)
            {
                return;
            }
            var item = new UiItem
            {
                Kind = UiKind.KeyCap, Fill = UiRole.Accent, Edge = UiRole.Accent, Ink = UiRole.OnAccent, Style = UiStyle.KeyCap, Icon = key.Glyph,
                Text = key.Glyph != UiIcon.None ? string.Empty : key.Name ?? string.Empty, X = x, Y = y, W = UiDraw.GameKeyWidth(key), H = UiTokens.KeyCap,
                Radius = UiTokens.RadiusSmall, Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = at.Parent, Alpha = 1f, Value = (int)UiAction.Count,
                Flags = UiFlags.Middle
            };
            s.Add(ref item);
        }
    }
}
