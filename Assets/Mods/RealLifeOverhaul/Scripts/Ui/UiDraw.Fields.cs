using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The controls the game's screens use, each in its control-only form: drawn inside one control's row at the right of
    /// a line whose words the line draws, so a host can mirror the game's widget and a settings list can line them up in
    /// one column. A choice (the game's Dropdown) with its open list, a slider (its Slider), an amount stepper (its
    /// AmountSelector), a switch (its Toggle), a check line (a requirement met or not), a text field (its TMP_InputField)
    /// and a key binding. Each draws its state from what it is given: chosen, disabled, on or off; hover, press and focus
    /// are the renderer's, so a control looks the same under every input. Game-free. Cost: a few item writes each.
    /// </summary>
    public static partial class UiDraw
    {
        private const float TrackW = 44f;       // px: a switch's track, a game's toggle
        private const float TrackH = 24f;       // px
        private const float Knob = 16f;         // px: a switch's or a slider's round knob
        internal const float StepButton = 32f;  // px: a stepper's minus and plus (a table's figure stands where its field's does)
        private const int ListRows = 8;         // options an open choice shows before it scrolls
        private const float FigureShare = 0.22f; // of a slider's width, the least its figure takes

        /// <summary>
        /// A switch: its state in words, then the track with the knob at its end, cyan when on, as a game's toggle. Returns
        /// the focus target.
        /// </summary>
        public static int Switch(UiScene s, UiAt at, float x, float y, float w, bool on, string word, int command, int arg, bool disabled)
        {
            float h = UiTokens.Control;
            // The target hugs the word and the track at the box's right, so its hover and ring frame the switch, not the room before it.
            float ww = string.IsNullOrEmpty(word) ? 0f : Math.Min(Snap(UiTextRules.Width(word, UiStyle.Body) + UiTokens.S1), Math.Max(0f, w - TrackW - UiTokens.S3 - UiTokens.S2 * 2f));
            float fw = Math.Min(w, UiTokens.S2 + (ww > 0f ? ww + UiTokens.S3 : 0f) + TrackW);
            x += w - fw;
            w = fw;
            int face = Box(s, at, x, y, w, h, UiRole.None, UiRole.None, UiTokens.RadiusLarge, disabled ? UiFlags.Disabled : UiFlags.None);
            UiAt inside = at.In(face);
            float tx = x + w - TrackW;   // flush with the column's right edge, where the fields and choices end
            if (ww > 0f)
            {
                TextMiddle(s, inside, tx - UiTokens.S3 - ww, y, h, ww, UiStyle.Body, disabled ? UiRole.Muted : UiRole.Text2, word,
                    UiFlags.Label | UiFlags.Right | (disabled ? UiFlags.Disabled : UiFlags.None));
            }
            SwitchLook(s, inside, x + w, y, on, disabled);
            return Node(s, at, x, y, w, h, command, arg, face, disabled);
        }

        /// <summary>
        /// A switch's track and knob alone, the track ending at <paramref name="right"/>: for a row that is the target itself and shows its state this way, so the
        /// switch makes no target of its own.
        /// </summary>
        public static void SwitchLook(UiScene s, UiAt at, float right, float y, bool on, bool disabled)
        {
            float h = UiTokens.Control;
            float tx = right - TrackW;
            UiRole fill = disabled ? UiRole.Track : on ? UiRole.Accent : UiRole.Track;
            int track = Box(s, at, tx, y + (h - TrackH) / 2f, TrackW, TrackH, fill, UiRole.None, TrackH / 2f, UiFlags.Circle);
            float kx = on ? tx + TrackW - UiTokens.S1 - Knob : tx + UiTokens.S1;
            Circle(s, at.In(track), kx, y + (h - Knob) / 2f, Knob, Knob, on && !disabled ? UiRole.OnAccent : UiRole.Text2, UiFlags.Middle);
        }

        /// <summary>
        /// A choice as a field: the value at its left and the chevron at its right, opening its list under it on a press or
        /// stepping with Left and Right (IUiStepping), as the game's Dropdown holds its options and its value. Returns the
        /// focus target.
        /// </summary>
        public static int Choice(UiScene s, UiAt at, float x, float y, float w, string value, int command, int arg, bool disabled)
        {
            float h = UiTokens.Control;
            int face = Box(s, at, x, y, w, h, UiRole.Field, UiRole.None, UiTokens.RadiusLarge, disabled ? UiFlags.Disabled : UiFlags.None);
            UiAt inside = at.In(face);
            float cx = x + w - UiTokens.S3 - UiTokens.Icon;
            Icon(s, inside, cx, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Down, disabled ? UiRole.Muted : UiRole.AccentText, UiFlags.Middle);
            TextMiddle(s, inside, x + UiTokens.S4, y, h, cx - UiTokens.S2 - x - UiTokens.S4, UiStyle.Body, disabled ? UiRole.Muted : UiRole.Text, value,
                UiFlags.Truncate | (disabled ? UiFlags.Disabled : UiFlags.None));
            return Node(s, at, x, y, w, h, command, arg, face, disabled);
        }

        /// <summary>The height an open choice's list takes for a number of options: whole rows, at most eight before it scrolls.</summary>
        public static float ChoiceListHeight(int count) => Math.Min(Math.Max(1, count), ListRows) * UiTokens.ControlSmall + UiTokens.S1 * 2f;

        /// <summary>
        /// An open choice's list on the overlay over its field: each option a row, the chosen one in the cyan at a fifth with
        /// its tick, eight rows in view and the rest scrolling inside, as the game's Dropdown opens. Each option is a row of
        /// one list (its index the argument). It opens on the overlay's layer, or on the one given over a sheet that stands
        /// there itself. Returns the list.
        /// </summary>
        public static short ChoiceList(UiScene s, UiAt under, float x, float y, float w, string[] options, int count, int chosen, float offset, int command, byte layer = 1, bool[] ticked = null)
        {
            var at = new UiAt { Parent = -1, Clip = -1, Layer = layer, Order = -1, Region = under.Region };
            float rowH = UiTokens.ControlSmall, h = ChoiceListHeight(count);
            int card = Box(s, at, x, y, w, h, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
            float viewH = h - UiTokens.S1 * 2f;
            float most = Math.Max(0f, count * rowH - viewH);
            offset = Math.Max(0f, Math.Min(offset, most));
            short clip = s.AddClip(x + UiTokens.S1, y + UiTokens.S1, w - UiTokens.S2, viewH, offset);
            int first = (int)(offset / rowH), last = Math.Min(count - 1, (int)((offset + viewH - 0.5f) / rowH));
            short list = s.AddList(count, first, Math.Max(0, last - first + 1), clip, chosen);
            s.Clips[clip].ContentHeight = count * rowH;
            var rows = new UiAt { Parent = -1, Clip = clip, Layer = layer, Order = -1, Region = under.Region };
            float rw = w - UiTokens.S2 - (count > ListRows ? UiTokens.S4 : 0f);   // a list that scrolls keeps its rows and their ring clear of the thumb
            for (int i = first; i <= last; i++)
            {
                bool on = ticked != null ? i < ticked.Length && ticked[i] : i == chosen;   // a list of several ticks each one stood on
                float ry = i * rowH;
                int face = Box(s, rows, 0f, ry, rw, rowH, on ? UiRole.RowSel : UiRole.Panel, UiRole.None, UiTokens.RadiusMedium, on ? UiFlags.Selected : UiFlags.None);
                UiAt inside = rows.In(face);
                if (on)
                {
                    Icon(s, inside, rw - UiTokens.S3 - UiTokens.Icon, ry + (rowH - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Check, UiRole.AccentText, UiFlags.Middle);
                }
                TextMiddle(s, inside, UiTokens.S3, ry, rowH, rw - UiTokens.S3 * 2f - UiTokens.Icon - UiTokens.S2, UiStyle.Body, UiRole.Text, options[i], UiFlags.Truncate);
                int node = Node(s, rows, 0f, ry, rw, rowH, command, i, face, false, list, i);
                if (on)
                {
                    s.FocusFirst = node;   // the keys start on the value it holds, as the game's Dropdown opens on it
                }
            }
            ScrollThumb(s, at.In(card), x + w - UiTokens.S2, y + UiTokens.S1, viewH, offset, count * rowH);
            return list;
        }

        /// <summary>
        /// A slider on one row: its track filled cyan to its share with the round knob there, and its value as a figure at
        /// its right, as the game's Slider holds a whole number or a sum between its ends. A press holds it (the quiet cyan
        /// fill), and while held Left and Right step it (IUiStepping) and a second press or Back lets it go, so the d-pad
        /// still crosses a page of sliders. Returns the focus target.
        /// </summary>
        public static int Slider(UiScene s, UiAt at, float x, float y, float w, string figure, float share, int command, int arg, bool disabled, bool held = false)
        {
            share = share < 0f ? 0f : share > 1f ? 1f : share;
            float h = UiTokens.Control;
            // Held is the keys' and the pad's state: the mouse drags the knob instead, so with the mouse leading no fill shows.
            int face = Box(s, at, x, y, w, h, held && !disabled && !s.Pointer ? UiRole.AccentSoft : UiRole.None, UiRole.None, UiTokens.RadiusLarge,
                disabled ? UiFlags.Disabled : UiFlags.None);
            UiAt inside = at.In(face);
            // The figure's room is a share of the control at least, so sliders in one column end their tracks at one edge.
            float fw = Math.Max(Snap(UiTextRules.Width(figure, UiStyle.BodyStrong) + UiTokens.S1), SnapDown(w * FigureShare));
            TextMiddle(s, inside, x + w - fw, y, h, fw, UiStyle.BodyStrong, disabled ? UiRole.Muted : UiRole.Text, figure,
                UiFlags.Right | (disabled ? UiFlags.Disabled : UiFlags.None));
            // The track from the box's left edge and the figure to its right edge, so a slider spans the column a choice or a field does.
            float tx = x + Knob / 2f, tw = Math.Max(UiTokens.S8, x + w - fw - UiTokens.S4 - tx);
            Track(s, inside, tx, y + h / 2f, tw, share, disabled);
            return Node(s, at, x, y, w, h, command, arg, face, disabled);
        }

        /// <summary>
        /// The share a slider stands at for a point across its face (the pointer's, 0 to 1 from its left), by the track the
        /// one-row <see cref="Slider"/> lays out at that width with that figure: the knob goes where the pointer is.
        /// </summary>
        public static float SliderShare(float w, string figure, float faceShare)
        {
            float fw = Math.Max(Snap(UiTextRules.Width(figure, UiStyle.BodyStrong) + UiTokens.S1), SnapDown(w * FigureShare));
            float tx = Knob / 2f, tw = Math.Max(UiTokens.S8, w - fw - UiTokens.S4 - tx);
            float share = (faceShare * w - tx) / tw;
            return share < 0f ? 0f : share > 1f ? 1f : share;
        }

        // A slider's thin track centred on a line, its fill to the share and the knob there.
        private static void Track(UiScene s, UiAt at, float x, float mid, float w, float share, bool disabled)
        {
            float th = UiTokens.S1;
            int track = Box(s, at, x, mid - th / 2f, w, th, UiRole.Track, UiRole.None, th / 2f, UiFlags.Circle | UiFlags.Middle);
            if (share > 0f)
            {
                Box(s, at.In(track), x, mid - th / 2f, Math.Max(th, w * share), th, disabled ? UiRole.Muted : UiRole.Accent, UiRole.None, th / 2f,
                    UiFlags.Circle | UiFlags.Share | UiFlags.Middle);
            }
            Circle(s, at.In(track), x + w * share - Knob / 2f, mid - Knob / 2f, Knob, Knob, disabled ? UiRole.Muted : UiRole.Text, UiFlags.Share | UiFlags.Middle | UiFlags.Overflow);
        }

        /// <summary>
        /// An amount between a minus and a plus, as the game's AmountSelector: the figure in a field typed through the host
        /// (IUiTyping) and the two buttons, each greyed at its end. Returns the figure's focus target; the buttons are
        /// targets of their own.
        /// </summary>
        public static int Stepper(UiScene s, UiAt at, float x, float y, float w, string figure, bool canLess, bool canMore, int lessCommand, int typeCommand,
            int moreCommand, int arg, bool disabled, string placeholder = null)
        {
            float h = UiTokens.Control, by = y + (h - StepButton) / 2f;
            StepKey(s, at, x, by, UiIcon.Minus, lessCommand, arg, disabled || !canLess);
            float fx = x + StepButton + UiTokens.S2, fw = w - (StepButton + UiTokens.S2) * 2f;
            int field = Box(s, at, fx, y, fw, h, UiRole.Field, UiRole.None, UiTokens.RadiusLarge, disabled ? UiFlags.Disabled : UiFlags.None);
            bool hint = string.IsNullOrEmpty(figure) && !string.IsNullOrEmpty(placeholder);   // an empty field says what to type, as a text field's hint
            TextMiddle(s, at.In(field), fx + UiTokens.S2, y, h, fw - UiTokens.S4, hint ? UiStyle.Body : UiStyle.BodyStrong, disabled || hint ? UiRole.Muted : UiRole.Text,
                hint ? placeholder : figure, UiFlags.Centre | UiFlags.Truncate | (disabled ? UiFlags.Disabled : UiFlags.None));
            if (!disabled && Typing(s, typeCommand, arg))
            {
                // The figure is centred: the caret stands after its last digit, or before the hint of an empty field.
                float room = fw - UiTokens.S4, words = hint ? UiTextRules.Width(placeholder, UiStyle.Body) : UiTextRules.Width(figure, UiStyle.BodyStrong);
                words = Math.Min(words, Math.Max(0f, room - CaretWidth - CaretGap));
                float left = fx + UiTokens.S2 + (room - words) / 2f;
                Caret(s, at.In(field), figure == null || figure.Length == 0 ? left - CaretWidth - CaretGap : left + words + CaretGap, y, h, UiStyle.BodyStrong);
            }
            int node = Node(s, at, fx, y, fw, h, typeCommand, arg, field, disabled);
            StepKey(s, at, x + w - StepButton, by, UiIcon.Plus, moreCommand, arg, disabled || !canMore);
            return node;
        }

        // A stepper's square button: its glyph on a slate tile, greyed and never pressable at its end.
        private static void StepKey(UiScene s, UiAt at, float x, float y, UiIcon icon, int command, int arg, bool disabled)
        {
            int face = Box(s, at, x, y, StepButton, StepButton, UiRole.Tile, UiRole.None, UiTokens.RadiusLarge, UiFlags.Middle | (disabled ? UiFlags.Disabled : UiFlags.None));
            float g = UiTokens.Icon;
            Icon(s, at.In(face), x + (StepButton - g) / 2f, y + (StepButton - g) / 2f, g, icon, disabled ? UiRole.Muted : UiRole.AccentText,
                UiFlags.Middle | UiFlags.Centre | (disabled ? UiFlags.Disabled : UiFlags.None));
            Node(s, at, x, y, StepButton, StepButton, command, arg, face, disabled);
        }

        /// <summary>
        /// A requirement on one compact row, read only: the tick in its own green when it is met, the warning's own glyph when
        /// it is not, and its words. Returns the height.
        /// </summary>
        public static float CheckLine(UiScene s, UiAt at, float x, float y, float w, string words, bool met, bool pending = false)
        {
            float h = UiTokens.PairRow;
            Icon(s, at, x, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, met ? UiIcon.Check : pending ? UiIcon.PadCircle : UiIcon.Warning,
                pending && !met ? UiRole.Muted : UiRole.None, UiFlags.Middle, true);
            TextMiddle(s, at, x + UiTokens.Icon + UiTokens.S2, y, h, w - UiTokens.Icon - UiTokens.S2, UiStyle.Body, met ? UiRole.Text : UiRole.Text2, words,
                UiFlags.Label | UiFlags.Truncate);
            return h;
        }

        /// <summary>
        /// A line of words to type, as the game's input field: what is typed, or its placeholder in the quiet grey, in a field
        /// set into the panel. Typing goes through the host's real text field (IUiTyping). Returns the focus target.
        /// </summary>
        public static int TextField(UiScene s, UiAt at, float x, float y, float w, string text, string placeholder, int command, int arg, bool disabled, bool typing = false)
        {
            float h = UiTokens.Control;
            int face = Box(s, at, x, y, w, h, UiRole.Field, UiRole.None, UiTokens.RadiusLarge, disabled ? UiFlags.Disabled : UiFlags.None);
            bool typed = !string.IsNullOrEmpty(text);
            bool caret = !disabled && (typing || Typing(s, command, arg));
            float tx = x + UiTokens.S4, room = w - UiTokens.S4 * 2f;
            float lead = caret && !typed ? CaretRoom : 0f;   // the caret before the placeholder, not over its first letter
            string shown = caret && typed ? Typed(text, UiStyle.Body, room) : typed ? text : placeholder;   // typed words end where the caret stands
            TextMiddle(s, at.In(face), tx + lead, y, h, room - lead, UiStyle.Body, typed && !disabled ? UiRole.Text : UiRole.Muted, shown,
                (typed ? UiFlags.Truncate : UiFlags.Label | UiFlags.Truncate) | (disabled ? UiFlags.Disabled : UiFlags.None));
            if (caret)
            {
                CaretAfter(s, at.In(face), tx, y, h, typed ? shown : null, UiStyle.Body, room);   // where the next letter goes, as the game's own field shows
            }
            return Node(s, at, x, y, w, h, command, arg, face, disabled);
        }

        /// <summary>
        /// A key binding: the bound key on its cap, or while it waits for the new key, its words on the field's
        /// quiet cyan, as a game's controls list asks for a key. Returns the focus target.
        /// </summary>
        public static int KeyBinding(UiScene s, UiAt at, float x, float y, float w, string keyWords, bool listening, int command, int arg, bool disabled)
        {
            float h = UiTokens.Control;
            int face = Box(s, at, x, y, w, h, listening ? UiRole.AccentSoft : UiRole.Field, UiRole.None, UiTokens.RadiusLarge, disabled ? UiFlags.Disabled : UiFlags.None);
            UiAt inside = at.In(face);
            if (listening)
            {
                TextMiddle(s, inside, x + UiTokens.S4, y, h, w - UiTokens.S4 * 2f, UiStyle.BodyStrong, UiRole.Text, keyWords, UiFlags.Label | UiFlags.Centre);   // soft white reads on the quiet cyan
            }
            else
            {
                float cw = Math.Min(w - UiTokens.S4, Math.Max(UiTokens.KeyCap, Snap(UiTextRules.Width(keyWords, UiStyle.KeyCap) + UiTokens.S2 * 2f)));
                float cx = x + SnapDown((w - cw) / 2f);
                int cap = Box(s, inside, cx, y + (h - UiTokens.KeyCap) / 2f, cw, UiTokens.KeyCap, UiRole.Tile, UiRole.None, UiTokens.RadiusSmall, UiFlags.Middle);
                TextMiddle(s, at.In(face).In(cap), cx + UiTokens.S1, y, h, cw - UiTokens.S2, UiStyle.KeyCap, disabled ? UiRole.Muted : UiRole.Text, keyWords,
                    UiFlags.Centre | UiFlags.Truncate | (disabled ? UiFlags.Disabled : UiFlags.None));
            }
            return Node(s, at, x, y, w, h, command, arg, face, disabled);
        }
    }
}
