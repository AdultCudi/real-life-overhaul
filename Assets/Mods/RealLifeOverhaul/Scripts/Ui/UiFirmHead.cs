using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A firm's head on a page, as the profile is a person's: its logo on a tile in the face's place (or its kind's glyph
    /// in its own colour), its name with the others to switch to, its kind and its place, its state as chips, what it still
    /// needs, its open switch and its actions.
    /// </summary>
    public sealed class UiFirmHeadModel
    {
        public string Name;
        public string Picture;                      // the firm's logo, a key UiPictures resolves, or null for the glyph
        public float Aspect = 1f;                   // the logo's width over its height
        public UiIcon Glyph = UiIcon.Store;         // its kind's glyph when it has no logo, in the glyph's own colour
        public UiIcon KindIcon = UiIcon.Store;
        public string Kind;                         // "Clothing store"
        public string Place;                        // where it is, a link that sets the destination; null for none
        public string[] Others = new string[0];     // the firms the name switches to, in the game's order; none: the name is no target
        public int Chosen = -1;
        public bool ListOpen;
        public float ListOffset;
        public string[] Chips = new string[0];      // its state ("Open", "Security: Good, 72%")
        public UiRole[] ChipTones = new UiRole[0];
        public string Todo;                         // what it still needs, a link that opens the list ("2 requirements missing"); null for none
        public UiRole TodoTone = UiRole.Warn;
        public bool TodoOpen;
        public string TodoTitle;                    // the list's title ("Requirements")
        public string[] Checks = new string[0];     // the requirements, met or not
        public bool[] Met = new bool[0];
        public string[] Alerts = new string[0];     // the things it asks to be done
        public bool[] Urgent = new bool[0];
        public bool HasSwitch;                      // the open switch: on is open
        public bool On;
        public string SwitchWord;                   // its state in words
        public bool SwitchDisabled;
        public UiActionModel[] Actions = new UiActionModel[0];
    }

    /// <summary>
    /// The firm's head, a block of an app window's page, after Football Manager's club header in the kit's look: the logo
    /// on a large slate tile where a person's face stands, the name in the figures' size with a chevron when it switches to
    /// another firm (the list opens under it, as a choice's does), the kind behind its glyph and the place as a cyan link,
    /// the state as soft chips with what it still needs as a link that opens its list (each requirement a check line, each
    /// alert behind the warning's glyph), and at the right the open switch and the actions, the main one last. Commands
    /// from <see cref="First"/>: <see cref="Name"/>, <see cref="Option"/> (a firm's index), <see cref="Place"/>,
    /// <see cref="Switch"/>, <see cref="Todo"/> and <see cref="Action"/> (an action's index). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiFirmHead
    {
        public const int First = 200;
        public const int Name = 0, Option = 1, Place = 2, Switch = 3, Todo = 4, Action = 5;
        public const float Logo = UiTokens.AvatarHero;   // px: the logo's tile, a profile's face
        private const float SwitchRoom = 96f;             // px: the open switch's track and its gaps besides its word
        private const float MostList = 360f;              // px: the widest the firms' list or the needs' list opens

        /// <summary>The head's height: the logo's tile, or its three lines when they stand taller.</summary>
        public static float Height => Logo;

        /// <summary>
        /// A logo laid since an item drawn at the words' strength: the game's logo shapes are white masks its sign tints, so
        /// on the slate they would stand as the brightest thing in the window.
        /// </summary>
        public static void Soften(UiScene s, int from)
        {
            for (int i = from; i < s.ItemCount; i++)
            {
                if (s.Items[i].Kind == UiKind.Picture)
                {
                    s.Items[i].Alpha = LogoStrength;
                }
            }
        }

        private const float LogoStrength = 0.88f;   // white at this strength over the tile reads as the primary text's colour

        /// <summary>Lays the head out from its left and top across a width. Returns the y under it.</summary>
        public static float Lay(UiScene s, UiAt at, UiFirmHeadModel m, float x, float y, float w, IUiKeys keys, int command = First)
        {
            int tile = UiDraw.Box(s, at, x, y, Logo, Logo, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge);
            var card = new UiCatalogueCard { Picture = m.Picture, Aspect = m.Aspect, Icon = m.Glyph };
            int from = s.ItemCount;
            UiCatalogue.Picture(s, at.In(tile), ref card, x, y, Logo, Logo, UiTokens.S12);
            Soften(s, from);
            float right = Actions(s, at, m, x + w, y, keys, command);
            float tx = x + Logo + UiTokens.S5, room = right - UiTokens.S6 - tx;
            float nameH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            float nameX = tx, nameW = NameLine(s, at, m, tx, y, room, nameH, command);
            float ly = y + nameH + UiTokens.S2;
            float kindW = 0f;
            if (!string.IsNullOrEmpty(m.Kind))
            {
                UiDraw.Icon(s, at, tx, ly + (UiTokens.PairRow - UiTokens.Icon) / 2f, UiTokens.Icon, m.KindIcon, UiRole.None, UiFlags.Middle, true);
                kindW = Math.Min(UiDraw.Snap(UiTextRules.Width(m.Kind, UiStyle.Body) + UiTokens.S1), Math.Max(0f, room - UiTokens.Icon - UiTokens.S2));
                UiDraw.TextMiddle(s, at, tx + UiTokens.Icon + UiTokens.S2, ly, UiTokens.PairRow, kindW, UiStyle.Body, UiRole.Text2, m.Kind, UiFlags.Truncate);
                kindW += UiTokens.Icon + UiTokens.S2 + UiTokens.S6;
            }
            if (!string.IsNullOrEmpty(m.Place))
            {
                PlaceLink(s, at, tx + kindW, ly, Math.Max(0f, room - kindW), m.Place, command + Place);
            }
            float cy = ly + UiTokens.PairRow + UiTokens.S3;
            float cx = tx, todoX = -1f;
            for (int i = 0; i < m.Chips.Length; i++)
            {
                if (string.IsNullOrEmpty(m.Chips[i]))
                {
                    continue;
                }
                float most = Math.Max(UiTokens.S16, x + w - cx);
                cx += UiDraw.Chip(s, at, cx, cy, m.Chips[i], i < m.ChipTones.Length ? m.ChipTones[i] : UiRole.Muted, most) + UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(m.Todo))
            {
                todoX = cx + (cx > tx ? UiTokens.S2 : 0f);
                TodoLink(s, at, todoX, cy, x + w - todoX, m, command + Todo);
            }
            float bottom = Math.Max(y + Logo, cy + UiTokens.Badge + UiTokens.S1);
            if (m.ListOpen && m.Others.Length > 0)
            {
                float lw = Math.Min(MostList, Math.Max(nameW, UiTokens.S16 * 4f));
                UiDraw.ChoiceList(s, at, nameX, y + nameH + UiTokens.S1, lw, m.Others, m.Others.Length, m.Chosen, m.ListOffset, command + Option);
            }
            else if (m.TodoOpen && todoX >= 0f)
            {
                Needs(s, at, m, todoX, cy + UiTokens.Badge + UiTokens.S2, Math.Min(MostList, x + w - todoX), command + Todo);
            }
            return UiDraw.Snap(bottom) + UiTokens.S6;
        }

        // The name in the figures' size; with others to switch to, a chevron after it and the two one target with a hover
        // face, as a choice's field. Returns the target's width.
        private static float NameLine(UiScene s, UiAt at, UiFirmHeadModel m, float x, float y, float room, float h, int command)
        {
            bool switches = m.Others.Length > 1;
            float chevron = switches ? UiTokens.S2 + UiTokens.Icon : 0f;
            float words = Math.Min(UiDraw.Snap(UiTextRules.Width(m.Name ?? string.Empty, UiStyle.Display) + UiTokens.S1), Math.Max(0f, room - chevron - UiTokens.S2 * 2f));
            if (!switches)
            {
                UiDraw.TextMiddle(s, at, x, y, h, words, UiStyle.Display, UiRole.Text, m.Name, UiFlags.Truncate);
                return words;
            }
            float fw = UiDraw.Snap(words + chevron + UiTokens.S2 * 2f);
            int face = UiDraw.Box(s, at, x - UiTokens.S2, y, fw, h, m.ListOpen ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge,
                m.ListOpen ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            UiDraw.TextMiddle(s, inside, x, y, h, words, UiStyle.Display, UiRole.Text, m.Name, UiFlags.Truncate);
            UiDraw.Icon(s, inside, x + words + UiTokens.S2, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Down, UiRole.AccentText, UiFlags.Middle | UiFlags.Decor);
            UiDraw.Node(s, at, x - UiTokens.S2, y, fw, h, command + Name, 0, face);
            return fw;
        }

        // The place as a link: the locate mark then the words, both cyan, a hover face round the two, the whole a target.
        private static void PlaceLink(UiScene s, UiAt at, float x, float y, float room, string place, int command)
        {
            float wordsRoom = room - UiTokens.IconSmall - UiDraw.LinkGap - UiTokens.S2;
            if (wordsRoom < UiTokens.S16)
            {
                return;
            }
            string shown = UiTextRules.Cut(place, UiStyle.BodyStrong, wordsRoom);
            float words = UiDraw.Snap(UiTextRules.Width(shown, UiStyle.BodyStrong) + UiTokens.S1);
            float fw = UiDraw.Snap(UiTokens.IconSmall + UiDraw.LinkGap + words + UiTokens.S2 * 2f), h = UiTokens.PairRow;
            int face = UiDraw.Box(s, at, x - UiTokens.S2, y, fw, h, UiRole.None, UiRole.None, UiTokens.RadiusMedium);
            UiAt inside = at.In(face);
            UiDraw.Icon(s, inside, x, y + (h - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Locate, UiRole.AccentText, UiFlags.Middle | UiFlags.Decor);
            UiDraw.TextMiddle(s, inside, x + UiTokens.IconSmall + UiDraw.LinkGap, y, h, words, UiStyle.BodyStrong, UiRole.AccentText, shown, UiFlags.Truncate);
            UiDraw.Node(s, at, x - UiTokens.S2, y, fw, h, command, 0, face);
        }

        // What it still needs, as a link on the chips' line: the warning's own glyph when something is missing, the words and
        // the chevron, the whole a target that opens the list under it.
        private static void TodoLink(UiScene s, UiAt at, float x, float y, float room, UiFirmHeadModel m, int command)
        {
            float h = UiTokens.Badge + UiTokens.S1;
            bool warns = m.TodoTone == UiRole.Warn || m.TodoTone == UiRole.Danger;
            UiIcon mark = warns ? UiIcon.Warning : UiIcon.Check;
            float wordsRoom = room - UiTokens.Icon - UiTokens.S1 - UiDraw.LinkGap - UiTokens.IconSmall - UiTokens.S2 * 2f;
            if (wordsRoom < UiTokens.S16)
            {
                return;
            }
            string shown = UiTextRules.Cut(m.Todo, UiStyle.SmallStrong, wordsRoom);
            float words = UiDraw.Snap(UiTextRules.Width(shown, UiStyle.SmallStrong) + UiTokens.S1);
            float fw = UiDraw.Snap(UiTokens.Icon + UiTokens.S1 + words + UiDraw.LinkGap + UiTokens.IconSmall + UiTokens.S2 * 2f);
            int face = UiDraw.Box(s, at, x - UiTokens.S2, y, fw, h, m.TodoOpen ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusMedium,
                m.TodoOpen ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            UiDraw.Icon(s, inside, x, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, mark, warns ? m.TodoTone : UiRole.None, UiFlags.Middle, true);
            float wx = x + UiTokens.Icon + UiTokens.S1;
            UiDraw.TextMiddle(s, inside, wx, y, h, words, UiStyle.SmallStrong, UiRole.AccentText, shown, UiFlags.Truncate);
            UiDraw.Icon(s, inside, wx + words + UiDraw.LinkGap - UiTokens.S1, y + (h - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, m.TodoOpen ? UiIcon.Up : UiIcon.Down,
                UiRole.AccentText, UiFlags.Middle | UiFlags.Decor);
            UiDraw.Node(s, at, x - UiTokens.S2, y, fw, h, command, 0, face);
        }

        // The needs' list over the page under its link: its title, each requirement a check line, then each alert behind the
        // warning's glyph in its tone. Read only; Back or the link again closes it.
        private static void Needs(UiScene s, UiAt under, UiFirmHeadModel m, float x, float y, float w, int command)
        {
            var at = new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1, Region = under.Region };
            float lines = (m.Checks.Length + m.Alerts.Length) * UiTokens.PairRow;
            float titleH = string.IsNullOrEmpty(m.TodoTitle) ? 0f : UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            float h = UiDraw.Snap(UiTokens.S4 * 2f + titleH + lines + (m.Checks.Length > 0 && m.Alerts.Length > 0 ? UiTokens.S3 : 0f));
            int card = UiDraw.Box(s, at, x, y, w, h, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
            UiDraw.Node(s, at, x, y, w, h, command, 1, card);   // the list is the open layer's one stop: the pad stays on it, a press closes it
            UiAt inside = at.In(card);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f, iy = y + UiTokens.S4;
            if (titleH > 0f)
            {
                UiDraw.Text(s, inside, ix, iy, iw, UiStyle.SectionHeader, UiRole.Text2, m.TodoTitle, UiFlags.Label | UiFlags.Truncate);
                iy += titleH;
            }
            for (int i = 0; i < m.Checks.Length; i++)
            {
                if (i < m.Met.Length && m.Met[i])
                {
                    iy += UiDraw.CheckLine(s, inside, ix, iy, iw, m.Checks[i], true);
                    continue;
                }
                // What is missing is what to act on: the cross in the warning's colour and the words at full strength.
                UiDraw.Icon(s, inside, ix, iy + (UiTokens.PairRow - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Close, UiRole.Warn, UiFlags.Middle, true);
                UiDraw.TextMiddle(s, inside, ix + UiTokens.Icon + UiTokens.S2, iy, UiTokens.PairRow, iw - UiTokens.Icon - UiTokens.S2, UiStyle.BodyStrong, UiRole.Text, m.Checks[i],
                    UiFlags.Label | UiFlags.Truncate);
                iy += UiTokens.PairRow;
            }
            if (m.Checks.Length > 0 && m.Alerts.Length > 0)
            {
                iy += UiTokens.S3;
            }
            for (int i = 0; i < m.Alerts.Length; i++)
            {
                bool urgent = i < m.Urgent.Length && m.Urgent[i];
                UiDraw.Icon(s, inside, ix, iy + (UiTokens.PairRow - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Warning, urgent ? UiRole.Danger : UiRole.Warn, UiFlags.Middle, true);
                UiDraw.TextMiddle(s, inside, ix + UiTokens.Icon + UiTokens.S2, iy, UiTokens.PairRow, iw - UiTokens.Icon - UiTokens.S2, UiStyle.Body, UiRole.Text, m.Alerts[i],
                    UiFlags.Label | UiFlags.Truncate);
                iy += UiTokens.PairRow;
            }
        }

        // The open switch and the actions at the top right, the main one last. Returns the left edge of the first.
        private static float Actions(UiScene s, UiAt at, UiFirmHeadModel m, float right, float y, IUiKeys keys, int command)
        {
            for (int i = m.Actions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.Actions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                right -= bw;
                UiDraw.Button(s, at, right, y, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, command + Action, i, a.Disabled);
                right -= UiTokens.S3;
            }
            if (m.HasSwitch)
            {
                float sw = UiDraw.Snap(UiTextRules.Width(m.SwitchWord ?? string.Empty, UiStyle.BodyStrong) + SwitchRoom);
                right -= sw;
                UiDraw.Switch(s, at, right, y, sw, m.On, m.SwitchWord, command + Switch, 0, m.SwitchDisabled);
                right -= UiTokens.S3;
            }
            return right;
        }
    }
}
