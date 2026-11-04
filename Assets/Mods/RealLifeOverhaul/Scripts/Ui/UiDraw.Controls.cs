using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A button's kind: one primary action a screen, the rest quieter.</summary>
    public enum UiButtonKind : byte
    {
        Primary = 0,    // the accent, with a glow: the screen's one main action
        Secondary,      // a field's face with a clear edge
        Destructive,    // quiet until pressed once, then armed: a second press does it
        Armed,          // a destructive button waiting for its second press
        Chip,           // a quick reply or a filter
        Quiet,          // words only (a composer's Send while there is nothing to send)
        Chosen          // the one chosen of a group of choices: the accent with dark words, marked as chosen and so never a second primary action
    }

    /// <summary>The kit's controls: buttons, nav items, search, tabs, toggles, sliders and the left-right selector.</summary>
    public static partial class UiDraw
    {
        private const float HeldAlpha = 0.4f;   // a held button's face and words: plainly not pressable, still readable

        /// <summary>
        /// A button: its face, the bound key's cap at its left when it has one (as a game shows "[F] Call"), an icon and its
        /// label; width from its words when <paramref name="w"/> is 0. Returns the focus target.
        /// </summary>
        public static int Button(UiScene s, UiAt at, float x, float y, float w, float h, UiButtonKind kind, string label, UiIcon icon, UiAction key,
            IUiKeys keys, int command, int arg, bool disabled = false)
        {
            bool cap = !disabled && !s.Held && UiKeyGlyphs.Ownable(key) && HasKey(key, keys);   // a held button, or one under a layer with the keys, shows no cap
            float inner = UiTextRules.Width(label, UiStyle.Button);
            int firstItem = s.ItemCount;
            float capW = cap ? KeyCapWidth(key, keys) : 0f;
            float iconW = icon != UiIcon.None ? UiTokens.Icon : 0f;
            float pad = kind == UiButtonKind.Chip ? UiTokens.S4 : UiTokens.S5;
            float content = inner + (cap ? capW + UiTokens.S2 : 0f) + (iconW > 0f ? iconW + UiTokens.S2 : 0f);
            if (w <= 0f)
            {
                w = Snap(content + pad * 2f);
            }
            else if (content > w - pad * 2f && iconW > 0f)
            {
                iconW = 0f;   // a longer translation: the words keep their room, the icon goes first
                icon = UiIcon.None;
                content = inner + (cap ? capW + UiTokens.S2 : 0f);
            }
            bool two = content > w - pad * 2f;
            if (two)
            {
                inner = w - pad * 2f - (cap ? capW + UiTokens.S2 : 0f);
                content = w - pad * 2f;
            }
            UiRole fill, edge, ink;
            switch (kind)
            {
                case UiButtonKind.Primary: fill = UiRole.Accent; edge = UiRole.None; ink = UiRole.OnAccent; break;
                case UiButtonKind.Chosen: fill = UiRole.Accent; edge = UiRole.None; ink = UiRole.OnAccent; break;
                case UiButtonKind.Destructive: fill = UiRole.Field; edge = UiRole.None; ink = UiRole.DangerText; break;
                case UiButtonKind.Armed: fill = UiRole.DangerSoft; edge = UiRole.Danger; ink = UiRole.DangerText; break;
                case UiButtonKind.Chip: fill = UiRole.AccentSoft; edge = UiRole.None; ink = UiRole.AccentText; break;
                case UiButtonKind.Quiet: fill = UiRole.None; edge = UiRole.None; ink = UiRole.Text2; break;
                default: fill = UiRole.Field; edge = UiRole.None; ink = UiRole.Text; break;
            }
            float radius = kind == UiButtonKind.Chip ? UiTokens.RadiusMedium : UiTokens.RadiusLarge;   // a chip's corners crisper: a game's tag, not a pill
            int face = Box(s, at, x, y, w, h, fill, edge, radius, (disabled ? UiFlags.Disabled : UiFlags.None) | (kind == UiButtonKind.Chosen ? UiFlags.Selected : UiFlags.None));
            UiAt inside = at.In(face);
            float cx = x + Math.Max(pad, (w - content) / 2f);
            if (cap)
            {
                KeyCap(s, inside, cx, y + (h - UiTokens.KeyCap) / 2f, key, keys, kind == UiButtonKind.Primary ? UiRole.OnAccent : UiRole.Track, ink);
                OnCyan(ref s.Items[s.ItemCount - 1], kind == UiButtonKind.Primary);
                s.Items[s.ItemCount - 1].Flags |= UiFlags.Middle | UiFlags.Centre;
                cx += capW + UiTokens.S2;
            }
            if (iconW > 0f)
            {
                Icon(s, inside, cx, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, icon, ink, UiFlags.Middle | UiFlags.Centre);
                cx += iconW + UiTokens.S2;
            }
            if (two)
            {
                float lh = UiTextRules.LineHeight(UiStyle.Button);
                Text(s, inside, cx, y + (h - lh * 2f) / 2f, inner, UiStyle.Button, ink, label,
                    UiFlags.Label | UiFlags.Wrap | UiFlags.Centre | UiFlags.Middle | (disabled ? UiFlags.Disabled : UiFlags.None), 2);
            }
            else
            {
                TextMiddle(s, inside, cx, y, h, inner + UiTokens.S1, UiStyle.Button, ink, label, UiFlags.Label | UiFlags.Centre | (disabled ? UiFlags.Disabled : UiFlags.None));
            }
            for (int i = firstItem; disabled && i < s.ItemCount; i++)
            {
                s.Items[i].Alpha *= HeldAlpha;   // held as the game holds it: the face and its words faded, so it reads as not pressable now
            }
            return Node(s, at, x, y, w, h, command, arg, face, disabled, -1, 0, key);
        }

        /// <summary>
        /// A key cap on the cyan: a keyboard key's word in the dark ink on a faint cap, a pad's glyph as a solid dark disc with
        /// the glyph in the cyan, since the faint cap leaves a pad's letter at about 3:1 there.
        /// </summary>
        internal static void OnCyan(ref UiItem cap, bool onAccent)
        {
            if (!onAccent)
            {
                return;
            }
            bool pad = cap.Icon != UiIcon.None;
            cap.Alpha = pad ? 1f : 0.16f;
            if (pad)
            {
                cap.Ink = UiRole.Accent;
                cap.Edge = UiRole.Accent;
            }
        }

        /// <summary>The height a button of a fixed width needs: a control's, or two lines' when its words take two there.</summary>
        public static float ButtonHeight(UiButtonKind kind, string label, UiAction key, IUiKeys keys, float w, float height)
        {
            bool cap = key != UiAction.Move && key != UiAction.Count && keys != null;
            float pad = kind == UiButtonKind.Chip ? UiTokens.S4 : UiTokens.S5;
            float room = w - pad * 2f - (cap ? KeyCapWidth(key, keys) + UiTokens.S2 : 0f);
            return UiTextRules.Width(label, UiStyle.Button) <= room ? height : Math.Max(height, Snap(UiTextRules.LineHeight(UiStyle.Button) * 2f + UiTokens.S3 * 2f));
        }

        /// <summary>A button's width before it is drawn, for a row of buttons laid out from the right.</summary>
        public static float ButtonWidth(UiButtonKind kind, string label, UiIcon icon, UiAction key, IUiKeys keys)
        {
            bool cap = key != UiAction.Move && key != UiAction.Count && keys != null;
            float content = UiTextRules.Width(label, UiStyle.Button) + (cap ? KeyCapWidth(key, keys) + UiTokens.S2 : 0f) + (icon != UiIcon.None ? UiTokens.Icon + UiTokens.S2 : 0f);
            return Snap(content + (kind == UiButtonKind.Chip ? UiTokens.S4 : UiTokens.S5) * 2f);
        }

        /// <summary>
        /// A group or a tab in a sidebar: its colour dot, its name and its count; the selected one is a filled bar with the
        /// accent selection bar at its left. Returns the focus target.
        /// </summary>
        public static int NavItem(UiScene s, UiAt at, float x, float y, float w, string label, string count, float hue, bool selected, int command, int arg,
            bool loudCount = false, UiIcon icon = UiIcon.None, UiRole tint = UiRole.Text2)
        {
            float h = UiTokens.Control;
            int face = Box(s, at, x, y, w, h, selected ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge, selected ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            float lead = UiTokens.StatusDot;
            if (icon != UiIcon.None)
            {
                // A game's menu: the group's icon on its tile in its colour.
                float tile = UiTokens.ControlSmall;
                int t = Tile(s, inside, x + UiTokens.S3, y + (h - tile) / 2f, tile, tint, selected);
                Icon(s, inside.In(t), x + UiTokens.S3 + (tile - UiTokens.Icon) / 2f, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, icon, selected ? UiRole.OnAccent : tint,
                    UiFlags.Middle | UiFlags.Centre, true);
                lead = tile - UiTokens.S1;
            }
            else
            {
                HueDot(s, inside, x + UiTokens.S4, y + (h - UiTokens.StatusDot) / 2f, UiTokens.StatusDot, hue);
            }
            float cw = 0f;
            if (!string.IsNullOrEmpty(count))
            {
                if (loudCount)
                {
                    cw = Badge(s, inside, x + w - UiTokens.S3, y + (h - UiTokens.Badge) / 2f, ParseCount(count), true);
                    s.Items[s.ItemCount - 1].Flags |= UiFlags.Middle;
                    s.Items[s.ItemCount - 2].Flags |= UiFlags.Middle;
                }
                else
                {
                    cw = UiTextRules.Width(count, UiStyle.Small) + UiTokens.S1;
                    TextMiddle(s, inside, x + w - UiTokens.S4 - cw, y, h, cw, UiStyle.Small, selected ? UiRole.Text : UiRole.Muted, count, UiFlags.Right);
                }
            }
            float tx = x + UiTokens.S4 + lead + UiTokens.S3;
            float lw = w - (tx - x) - cw - UiTokens.S6;
            UiRole ink = UiRole.Text;   // the chosen row's fill marks it: cyan words on the cyan read worst on the one row that matters
            if (UiTextRules.Width(label, UiStyle.Nav) <= lw)
            {
                TextMiddle(s, inside, tx, y, h, lw, UiStyle.Nav, ink, label, UiFlags.Label | UiFlags.Truncate);
            }
            else
            {
                // A long name of the game's ("Importations / Marchandises") on two smaller lines rather than cut short.
                float lh = UiTextRules.WrappedHeight(label, UiStyle.SmallStrong, lw, 2);
                Text(s, inside, tx, y + (h - lh) / 2f, lw, UiStyle.SmallStrong, ink, label, UiFlags.Label | UiFlags.Wrap | UiFlags.Middle, 2);
            }
            return Node(s, at, x, y, w, h, command, arg, face);
        }

        /// <summary>
        /// A search box: its glyph, the words typed or its placeholder, and how many it finds; its target takes the command
        /// with an argument (0 where a screen has one search). Returns the focus target.
        /// </summary>
        public static int Search(UiScene s, UiAt at, float x, float y, float w, string placeholder, string query, string count, int command, int arg = 0)
        {
            float h = UiTokens.Control;
            int face = Box(s, at, x, y, w, h, UiRole.Field, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            Icon(s, inside, x + UiTokens.S4, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Search, UiRole.Muted, UiFlags.Middle);
            float cw = string.IsNullOrEmpty(count) ? 0f : UiTextRules.Width(count, UiStyle.Small) + UiTokens.S1;
            if (cw > 0f)
            {
                TextMiddle(s, inside, x + w - UiTokens.S4 - cw, y, h, cw, UiStyle.Small, UiRole.Muted, count, UiFlags.Right);
            }
            float tx = x + UiTokens.S4 + UiTokens.Icon + UiTokens.S3;
            bool typed = !string.IsNullOrEmpty(query);
            bool caret = Typing(s, command, arg);
            float room = w - (tx - x) - cw - UiTokens.S6;
            float lead = caret && !typed ? CaretRoom : 0f;   // the caret before the placeholder, not over its first letter
            string shown = caret && typed ? Typed(query, UiStyle.Body, room) : typed ? query : placeholder;
            TextMiddle(s, inside, tx + lead, y, h, room - lead, UiStyle.Body, typed ? UiRole.Text : UiRole.Muted, shown, typed ? UiFlags.Truncate : UiFlags.Label | UiFlags.Truncate);
            if (caret)
            {
                CaretAfter(s, inside, tx, y, h, typed ? shown : null, UiStyle.Body, room);
            }
            return Node(s, at, x, y, w, h, command, arg, face);
        }

        /// <summary>
        /// Tabs: the previous tab's key, the tabs' names, the next tab's key; the shown tab filled cyan with dark words, as a
        /// city builder marks the tool in hand, the rest quieter, a rule under all. Each tab is a focus target (its command
        /// with its index). Returns the height.
        /// </summary>
        public static float Tabs(UiScene s, UiAt at, float x, float y, float w, string[] labels, int shown, IUiKeys keys, int command)
        {
            float h = UiTokens.Control;
            float cx = x;
            // A lone key cap means nothing to the mouse: it shows while the keys lead, and its room stays so nothing moves.
            if (keys != null)
            {
                cx += (s.Pointer ? KeyCapWidth(UiAction.TabPrev, keys) : KeyCap(s, at, cx, y + (h - UiTokens.KeyCap) / 2f - UiTokens.S1, UiAction.TabPrev, keys, UiRole.Track, UiRole.Text2))
                      + UiTokens.S3;
                if (!s.Pointer)
                {
                    s.Items[s.ItemCount - 1].Flags |= UiFlags.Middle;
                }
            }
            for (int i = 0; i < labels.Length; i++)
            {
                bool on = i == shown;
                float tw = Snap(UiTextRules.Width(labels[i], UiStyle.Nav) + UiTokens.S4 * 2f);
                int face = Box(s, at, cx, y + UiTokens.S1, tw, h - UiTokens.S2, on ? UiRole.Accent : UiRole.None, UiRole.None, UiTokens.RadiusLarge, on ? UiFlags.Selected : UiFlags.None);
                TextMiddle(s, at.In(face), cx + UiTokens.S4, y, h, tw - UiTokens.S4 * 2f + UiTokens.S1, UiStyle.Nav, on ? UiRole.OnAccent : UiRole.Text2, labels[i], UiFlags.Label);
                Node(s, at, cx, y, tw, h, command, i, face);
                cx += tw + UiTokens.S1;
            }
            if (keys != null && !s.Pointer)
            {
                KeyCap(s, at, cx + UiTokens.S2, y + (h - UiTokens.KeyCap) / 2f - UiTokens.S1, UiAction.TabNext, keys, UiRole.Track, UiRole.Text2);
                s.Items[s.ItemCount - 1].Flags |= UiFlags.Middle;
            }
            return h;
        }

        /// <summary>A setting that is on or off: its words at the left, the switch at the right. Returns the focus target.</summary>
        public static int Toggle(UiScene s, UiAt at, float x, float y, float w, string label, bool on, int command, int arg)
        {
            float h = UiTokens.Control;
            int face = Box(s, at, x, y, w, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            const float trackW = UiTokens.S12 - UiTokens.S1, trackH = UiTokens.S6;
            float tx = x + w - UiTokens.S3 - trackW;
            TextMiddle(s, inside, x + UiTokens.S3, y, h, tx - x - UiTokens.S6, UiStyle.Body, UiRole.Text, label, UiFlags.Label);
            int track = Box(s, inside, tx, y + (h - trackH) / 2f, trackW, trackH, on ? UiRole.Accent : UiRole.Track, UiRole.None, trackH / 2f, UiFlags.Circle);
            float knob = trackH - UiTokens.S2;
            Circle(s, at.In(track), on ? tx + trackW - UiTokens.S1 - knob : tx + UiTokens.S1, y + (h - knob) / 2f, knob, knob, on ? UiRole.OnAccent : UiRole.Text2);
            return Node(s, at, x, y, w, h, command, arg, face);
        }

        /// <summary>A value on a track: its words and figure over the track, the fill and the knob at its share. Returns the focus target.</summary>
        public static int Slider(UiScene s, UiAt at, float x, float y, float w, string label, string figure, float share, int command, int arg)
        {
            share = share < 0f ? 0f : share > 1f ? 1f : share;
            float h = UiTokens.Control + UiTokens.S4;
            int face = Box(s, at, x, y, w, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            float fw = UiTextRules.Width(figure, UiStyle.SmallStrong) + UiTokens.S1;
            Text(s, inside, x + UiTokens.S3, y + UiTokens.S2, w - fw - UiTokens.S8, UiStyle.Body, UiRole.Text, label, UiFlags.Label);
            Text(s, inside, x + w - UiTokens.S3 - fw, y + UiTokens.S2, fw, UiStyle.SmallStrong, UiRole.AccentText, figure, UiFlags.Right);
            float ty = y + h - UiTokens.S4 - UiTokens.S2, tw = w - UiTokens.S3 * 2f;
            Track(s, inside, x + UiTokens.S3, ty + UiTokens.S1, tw, share, false);   // the one slider's thin track and knob (UiDraw.Fields)
            return Node(s, at, x, y, w, h, command, arg, face);
        }

        /// <summary>
        /// A choice of a few, changed with left and right as games do (no list that drops down): its words at the left, the
        /// choice between two chevrons at the right. Returns the focus target.
        /// </summary>
        public static int Selector(UiScene s, UiAt at, float x, float y, float w, string label, string choice, int command, int arg)
        {
            float h = UiTokens.Control;
            int face = Box(s, at, x, y, w, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            float cw = SnapDown(Math.Min(w / 2f, UiTokens.S16 * 4f));
            float cx = x + w - UiTokens.S3 - cw;
            TextMiddle(s, inside, x + UiTokens.S3, y, h, cx - x - UiTokens.S6, UiStyle.Body, UiRole.Text, label, UiFlags.Label);
            int box = Box(s, inside, cx, y + UiTokens.S1, cw, h - UiTokens.S2, UiRole.Field, UiRole.None, UiTokens.RadiusMedium);
            Icon(s, at.In(box), cx + UiTokens.S2, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Back, UiRole.AccentText, UiFlags.Middle);
            Icon(s, at.In(box), cx + cw - UiTokens.S2 - UiTokens.Icon, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Next, UiRole.AccentText, UiFlags.Middle);
            TextMiddle(s, at.In(box), cx + UiTokens.S2 + UiTokens.Icon, y, h, cw - (UiTokens.S2 + UiTokens.Icon) * 2f, UiStyle.SmallStrong, UiRole.Text, choice,
                UiFlags.Centre | UiFlags.Label);
            return Node(s, at, x, y, w, h, command, arg, face);
        }

        /// <summary>Chips in rows within a width (quick replies, filters); returns the height they take.</summary>
        public static float Chips(UiScene s, UiAt at, float x, float y, float w, string[] labels, int count, int command)
        {
            float cx = x, cy = y;
            for (int i = 0; i < count; i++)
            {
                float cw = Math.Min(w, ButtonWidth(UiButtonKind.Chip, labels[i], UiIcon.None, UiAction.Count, null));
                if (cx > x && cx + cw > x + w)
                {
                    cx = x;
                    cy += UiTokens.Chip + UiTokens.S2;
                }
                Button(s, at, cx, cy, cw, UiTokens.Chip, UiButtonKind.Chip, labels[i], UiIcon.None, UiAction.Count, null, command, i);
                cx += cw + UiTokens.S2;
            }
            return count > 0 ? cy - y + UiTokens.Chip : 0f;
        }

        private static int ParseCount(string count)
        {
            int n = 0;
            for (int i = 0; i < count.Length; i++)
            {
                char c = count[i];
                if (c >= '0' && c <= '9')
                {
                    n = n * 10 + (c - '0');
                }
            }
            return n;
        }
    }
}
