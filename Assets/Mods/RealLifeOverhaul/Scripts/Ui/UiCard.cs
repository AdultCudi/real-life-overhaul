using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a box of the game's says, as the compact decision card draws it.</summary>
    public sealed class UiCardModel
    {
        public UiIcon Icon = UiIcon.Info;            // the emblem: the warning for a negative box, info otherwise
        public string Title;                         // the header, in capitals
        public string Body;
        public string Face;                          // the person it is about: their face in its ring takes the emblem's place; null for none
        public float FaceHue;
        public UiRole FaceRing = UiRole.None;
        public string Check;                         // a line to tick ("Do not show this again"); null for none
        public bool Checked;
        public bool HasField;                        // a text box: one field of words
        public string Field;                         // its words
        public string Placeholder;                   // what it asks for while empty
        public bool Typing;                          // the field takes the keys now: a caret shows where the words go (the host says so itself while it types; this is for a sample)
        public int Focus = -1;                       // the button the keys start on (a negative box: the way back), or -1 for the first target
        public int BodyLines = 8;                    // how far a long body wraps before it ends at a whole word (the mod's long notes take more)
        public UiActionModel[] Buttons = new UiActionModel[0];   // the way back first, the confirming one last
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The decision card in its compact form, for a moment that asks one thing and has no list of choices: the game's
    /// question box, its acknowledgement and its text box, and the mod's own boxes. Centred over the world dimmed to about
    /// half, an emblem on its tile at the left (the warning for a negative box), the header in cyan capitals, what it
    /// means under it, a line to tick or a field where the box has one, and its buttons at the foot's right, the
    /// confirming one last, Back on the way back and F on the confirm. Commands: <see cref="ButtonCommand"/> (a button's
    /// index), <see cref="CheckCommand"/> and <see cref="FieldCommand"/>. Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiCard
    {
        public const int ButtonCommand = 1, CheckCommand = 2, FieldCommand = 3;
        public const float Width = 640f;      // px: the card, as wide as a long sentence reads in one sweep
        private const float Emblem = 56f;     // px: the emblem's tile
        private const int TitleLines = 2;     // a long translation of a header takes a second line, never a cut word

        // The header's height: one line, or two for a long translation.
        private static float TitleHeight(UiCardModel m, float inner) => UiDraw.Snap(UiTextRules.WrappedHeight(m.Title, UiStyle.AppTitle, inner, TitleLines));

        public static void Lay(UiScene s, UiCardModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("card", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            Card(s, m, frame, keys, UiAt.Screen);
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>
        /// The card over a screen already laid out in the scene, on the overlay's layer, as the game stands its box over an
        /// app: the screen dimmed under it and its status bar left as it is. For the previews of a box over a window.
        /// </summary>
        public static void LayOver(UiScene s, UiCardModel m, UiFrame frame, IUiKeys keys)
        {
            s.PromptCount = 0;
            s.Pointer = frame.Pointer;
            s.Held = false;   // the window under it was laid held; the layer over it has the keys
            Card(s, m, frame, keys, new UiAt { Parent = -1, Clip = -1, Layer = 1, Order = -1 });
            UiDraw.PromptBar(s, frame, keys);   // its own bar over the window's, as its canvas stands over the window's in the game
        }

        private static void Card(UiScene s, UiCardModel m, UiFrame frame, IUiKeys keys, UiAt root)
        {
            UiDraw.Box(s, root, 0f, 0f, frame.Width, frame.BandY, UiRole.Scrim, UiRole.None, 0f, UiFlags.Decor);   // the world dimmed, the status bar left clear
            float inner = Inner();
            float h = Height(m, inner, keys);
            float x = UiDraw.SnapDown((frame.Width - Width) / 2f), y = UiDraw.SnapDown((frame.BandY - h) / 2f);
            int card = UiDraw.Box(s, root, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = root.In(card);
            float ex = x + UiTokens.S6, ey = y + UiTokens.S6;
            if (!string.IsNullOrEmpty(m.Face))
            {
                // A moment about one person shows their face, as the decision card does; the emblem is for a moment about no one.
                UiDraw.Portrait(s, at, ex + UiTokens.S1, ey + UiTokens.S1, Emblem - UiTokens.S2, m.Face, m.FaceHue, m.FaceRing);
            }
            else
            {
                int tile = UiDraw.Tile(s, at, ex, ey, Emblem, UiIconTints.Of(m.Icon), false);
                float g = UiDraw.Snap(Emblem * 0.57f);
                UiDraw.Icon(s, at.In(tile), ex + (Emblem - g) / 2f, ey + (Emblem - g) / 2f, g, m.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            }
            float cx = ex + Emblem + UiTokens.S5, cy = y + UiTokens.S6;
            float titleH = TitleHeight(m, inner);
            bool body = !string.IsNullOrEmpty(m.Body);
            if (!body)
            {
                cy = ey + UiDraw.SnapDown((Emblem - titleH) / 2f);   // a title alone stands level with its emblem, the field a step under both
            }
            UiDraw.Text(s, at, cx, cy, inner, UiStyle.AppTitle, UiRole.AccentText, m.Title, UiFlags.Label | UiFlags.Wrap, TitleLines);
            cy += titleH + UiTokens.S2;
            if (body)
            {
                float bh = UiTextRules.WrappedHeight(m.Body, UiStyle.Body, inner, m.BodyLines);
                UiDraw.Text(s, at, cx, cy, inner, UiStyle.Body, UiRole.Text2, m.Body, UiFlags.Label | UiFlags.Wrap | UiFlags.Middle, m.BodyLines);
                cy += UiDraw.Snap(bh);
            }
            cy = Math.Max(cy, ey + Emblem) + (body ? UiTokens.S5 : UiTokens.S4);
            if (m.HasField)
            {
                cy += Field(s, at, m, cx, cy, inner) + UiTokens.S4;
            }
            if (m.Check != null)
            {
                cy += CheckLine(s, at, m, cx, cy, inner) + UiTokens.S4;
            }
            float bx = x + Width - UiTokens.S6;
            for (int i = m.Buttons.Length - 1; i >= 0; i--)
            {
                ref UiActionModel b = ref m.Buttons[i];
                float bw = UiDraw.ButtonWidth(b.Kind, b.Label, b.Icon, b.Key, keys);
                bx -= bw;
                int node = UiDraw.Button(s, at, bx, cy, bw, UiTokens.Control, b.Kind, b.Label, b.Icon, b.Key, keys, ButtonCommand, i, b.Disabled);
                if (i == m.Focus)
                {
                    s.FocusFirst = node;   // a negative box starts on its way back, so a reflexive Enter never does what cannot be undone
                }
                bx -= UiTokens.S3;
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
        }

        /// <summary>The words' column: the card less its padding and the emblem's.</summary>
        public static float Inner() => Width - UiTokens.S6 * 2f - Emblem - UiTokens.S5;

        /// <summary>The card's height for its words, its field or line and its buttons.</summary>
        public static float Height(UiCardModel m, float inner, IUiKeys keys)
        {
            float h = TitleHeight(m, inner) + UiTokens.S2;
            bool body = !string.IsNullOrEmpty(m.Body);
            if (body)
            {
                h += UiDraw.Snap(UiTextRules.WrappedHeight(m.Body, UiStyle.Body, inner, m.BodyLines));
            }
            h = Math.Max(h, Emblem) + (body ? UiTokens.S5 : UiTokens.S4);
            if (m.HasField)
            {
                h += UiTokens.Control + UiTokens.S4;
            }
            if (m.Check != null)
            {
                h += CheckHeight(m, inner) + UiTokens.S4;
            }
            return UiDraw.Snap(UiTokens.S6 + h + UiTokens.Control + UiTokens.S6);
        }

        // The box's one field: its words or, while empty, what it asks for in the quiet grey; a cyan edge while typing.
        private static float Field(UiScene s, UiAt at, UiCardModel m, float x, float y, float w)
        {
            float h = UiTokens.Control;
            bool empty = string.IsNullOrEmpty(m.Field);
            int face = UiDraw.Box(s, at, x, y, w, h, UiRole.Field, UiRole.None, UiTokens.RadiusMedium);   // as the search field: the ring alone says where the keys are
            bool caret = m.Typing || UiDraw.Typing(s, FieldCommand, 0);
            float tx = x + UiTokens.S4, room = w - UiTokens.S4 * 2f;
            float lead = caret && empty ? UiDraw.CaretRoom : 0f;   // the caret before the words it asks for, not over their first letter
            string shown = caret && !empty ? UiDraw.Typed(m.Field, UiStyle.Body, room) : empty ? m.Placeholder : m.Field;
            UiDraw.TextMiddle(s, at.In(face), tx + lead, y, h, room - lead, UiStyle.Body, empty ? UiRole.Muted : UiRole.Text, shown,
                UiFlags.Truncate | (empty ? UiFlags.Label : UiFlags.None));
            if (caret)
            {
                UiDraw.CaretAfter(s, at.In(face), tx, y, h, empty ? null : shown, UiStyle.Body, room);   // a thin cyan line where the next letter goes
            }
            UiDraw.Node(s, at, x, y, w, h, FieldCommand, 0, face);
            return h;
        }

        // A line to tick: its box (cyan with the tick when ticked) and its words, the whole line the target.
        private static float CheckLine(UiScene s, UiAt at, UiCardModel m, float x, float y, float w)
        {
            float h = CheckHeight(m, w);
            int face = UiDraw.Box(s, at, x, y, w, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge);   // inside the words' column, its ring too
            UiAt inside = at.In(face);
            float box = UiTokens.KeyCap;   // 24 px: on the grid in a 40 px row
            float bx = x + UiTokens.S2, by = y + UiDraw.SnapDown((h - box) / 2f);
            int tick = UiDraw.Box(s, inside, bx, by, box, box, m.Checked ? UiRole.Accent : UiRole.Field, m.Checked ? UiRole.None : UiRole.PanelEdge,
                UiTokens.RadiusSmall, m.Checked ? UiFlags.Selected : UiFlags.None);
            if (m.Checked)
            {
                UiDraw.Icon(s, inside.In(tick), bx + (box - UiTokens.Icon) / 2f, by + (box - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Check, UiRole.OnAccent, UiFlags.Middle | UiFlags.Centre);
            }
            float tx = bx + box + UiTokens.S3;
            if (h > UiTokens.Control)
            {
                float lh = UiTextRules.WrappedHeight(m.Check, UiStyle.Body, x + w - tx, 2);
                UiDraw.Text(s, inside, tx, y + UiDraw.SnapDown((h - lh) / 2f), x + w - tx, UiStyle.Body, UiRole.Text, m.Check, UiFlags.Label | UiFlags.Wrap, 2);
            }
            else
            {
                UiDraw.TextMiddle(s, inside, tx, y, h, x + w - tx, UiStyle.Body, UiRole.Text, m.Check, UiFlags.Label | UiFlags.Truncate);
            }
            UiDraw.Node(s, at, x, y, w, h, CheckCommand, 0, face);
            return h;
        }

        // The line to tick's height: a control's, or two lines' where a long translation needs them, never cut.
        private static float CheckHeight(UiCardModel m, float w)
        {
            float room = w - UiTokens.S2 - UiTokens.KeyCap - UiTokens.S3;
            return UiTextRules.Width(m.Check, UiStyle.Body) + UiTokens.S1 <= room ? UiTokens.Control
                : Math.Max(UiTokens.Control, UiDraw.Snap(UiTextRules.WrappedHeight(m.Check, UiStyle.Body, room, 2) + UiTokens.S2));
        }
    }
}
