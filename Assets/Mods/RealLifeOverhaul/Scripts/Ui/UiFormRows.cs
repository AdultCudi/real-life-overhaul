using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One row of a game's form: what it asks for or shows, its words, its value and its steps, as the game's own widget holds them.</summary>
    public struct UiFormRow
    {
        public UiFormKind Kind;
        public string Label;        // the words at its left; a heading's, a note's or an entry's own words
        public string Value;        // a choice's value, a figure, the words typed, a slider's figure, a switch's word
        public string Hint;         // what an empty field asks for; a figure's words while the game has not written it
        public string Action;       // an entry's or an action's button
        public bool On;             // a switch's or an option's state
        public bool CanLess, CanMore;
        public bool Remove;         // an amount whose line can be taken away (an item of a delivery)
        public bool Strong;         // a total: its words stand out
        public bool Warn;           // a note that warns (a design needs mods the player lacks)
        public float Share;         // a slider's place between its ends, 0 to 1
        public string[] Swatches;   // the colours to pick, as their pictures' keys
        public int Chosen;          // the colour chosen, or -1; a segments row's option chosen
        public bool Disabled;
        public string[] Options;    // a segments row's options, in their words
        public string Side;         // a pick's figure at its right, in the green (an agent's discount)
        public string SideNote;     // ...and the small line under it (their skill)
        public string Reason;       // why a pick cannot be chosen, in a chip at its right
        public UiIcon Icon;         // a pick's mark; None for the initials of its name on a circle of their hue
        public string Picture;      // the game's own picture beside a line (a delivery's goods), a UiPictures key; null for none
    }

    /// <summary>What a form's screen keeps between layouts: the choice whose list is open, the slider the keys hold, how far it scrolled.</summary>
    public sealed class UiFormPick
    {
        public int Row = -1;                        // the choice whose list is open, or -1
        public string[] Options = new string[0];    // its options, in the game's words
        public int Count;
        public int Chosen = -1;
        public float ListOffset;                    // px its open list has scrolled
        public int Held = -1;                       // the slider the keys hold, or -1
        public float Offset;                        // px the form has scrolled, when it is taller than its room
        public float Most;                          // px the form may take before it scrolls; 0 for all it holds
        public int Reveal = -1;                     // a row the keys want in view, by its place among the rows that take a press; -1 for none
        public short Clip = -1;                     // set as it is laid out: the form's scroll region, or -1
        public short ListClip = -1;                 // ...the open list's
        public short List = -1;                     // ...and the rows that take a press as a list, while the form scrolls
        public float ControlX, ControlW;            // ...and the controls' column, across the screen, so a drag on a slider knows its place

        /// <summary>The list closed, the slider let go, the form at its top: a new form.</summary>
        public void Reset()
        {
            Row = -1;
            Count = 0;
            Chosen = -1;
            ListOffset = 0f;
            Held = -1;
            Offset = 0f;
            Reveal = -1;
        }
    }

    /// <summary>
    /// A game's whole form in a conversation's or a call's form slot, laid as the dialog lays its input out: a choice (the
    /// game's Dropdown, its list opening under it), an amount with its steps and the button that takes its line away,
    /// words or a figure to type, a switch, a slider, one of a few options, colours to pick, a figure read only (a fee, a
    /// total; its hint, muted, while the game has not written it), a note, a heading over the fields it opens, a line of a
    /// list with its button (a contract to cancel), a button alone (add an item) and the hairline the game draws between its
    /// parts. One band: the labels at the left (words too long for one line on two) and the controls in one column at the
    /// right, so a form reads as a game's settings rows do; a form taller than its room
    /// scrolls inside it, the rows the keys can reach kept whole. Commands: <see cref="FieldCommand"/> (a row's control, by
    /// its index), <see cref="LessCommand"/> and <see cref="MoreCommand"/> (an amount's steps), <see cref="RemoveCommand"/>
    /// (an amount's line taken away), <see cref="OptionCommand"/> (an option of the open list), <see cref="RowCommand"/> (an
    /// entry's or an action's button, an option) and <see cref="SwatchCommand"/> (a colour, <see cref="SwatchArg"/>).
    /// Game-free. Cost: a layout a change; a scrolled form lays out only the rows in view.
    /// </summary>
    public static class UiFormRows
    {
        public const int FieldCommand = UiConversation.FormCommand, LessCommand = UiConversation.FormLess, MoreCommand = UiConversation.FormMore;
        public const int OptionCommand = 66, RowCommand = 67, SwatchCommand = 68, RemoveCommand = 69;
        private const float ControlMost = 320f;   // px: the controls' column at most
        private const float Swatch = 24f;         // px: a colour's square
        private const float Ring = 16f;           // px: an option's ring
        private const float Dot = 8f;             // px: the chosen option's dot
        private const int AllLines = 0;   // UiTextRules: a most of none wraps a text to every line it takes
        private const float PictureWidth = UiTokens.ControlSmall + UiTokens.S2;   // px: a row's picture and the gap after it

        /// <summary>A colour's argument: its row and its place in the row.</summary>
        public static int SwatchArg(int row, int swatch) => (row << 8) | (swatch & 0xff);

        public static int SwatchRow(int arg) => arg >> 8;

        public static int SwatchIndex(int arg) => arg & 0xff;

        /// <summary>The controls' column at a band's width: one edge for every row.</summary>
        public static float ControlWidth(float w) => Math.Min(ControlMost, UiDraw.SnapDown((w - UiTokens.S3 * 2f) * 0.5f));

        /// <summary>Whether a row takes a press: the rows the keys move through (a line of a list only with its button).</summary>
        public static bool Takes(ref UiFormRow r) =>
            r.Kind == UiFormKind.Choice || r.Kind == UiFormKind.Amount || r.Kind == UiFormKind.Text || r.Kind == UiFormKind.Switch || r.Kind == UiFormKind.Slider
            || r.Kind == UiFormKind.Radio || r.Kind == UiFormKind.Action || r.Kind == UiFormKind.Pick
            || (r.Kind == UiFormKind.Swatches && r.Swatches != null && r.Swatches.Length > 0)
            || (r.Kind == UiFormKind.Segments && r.Options != null && r.Options.Length > 0)
            || (r.Kind == UiFormKind.Entry && !string.IsNullOrEmpty(r.Action));

        /// <summary>Whether a row's control is typed into (its words or its figure).</summary>
        public static bool Typed(UiFormKind kind) => kind == UiFormKind.Amount || kind == UiFormKind.Text;

        /// <summary>A row's height at the band's width.</summary>
        public static float RowHeight(ref UiFormRow r, float w)
        {
            float iw = w - UiTokens.S3 * 2f;
            switch (r.Kind)
            {
                case UiFormKind.Figure:
                    return Lead(ref r) > 0f ? Math.Max(UiTokens.PairRow, UiTokens.ControlSmall) : UiTokens.PairRow;
                case UiFormKind.Check:
                    return UiTokens.PairRow;
                case UiFormKind.Pick:
                    return PickHeight;
                case UiFormKind.Segments:
                    return Labelled(ref r, w);
                case UiFormKind.Radio:
                    return UiTokens.ControlSmall;
                case UiFormKind.Break:
                    return UiTokens.S4;
                case UiFormKind.Heading:
                    return UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
                case UiFormKind.Note:
                    return UiDraw.Snap(UiTextRules.WrappedHeight(r.Label, UiStyle.Small, iw - (r.Warn ? UiTokens.IconSmall + UiTokens.S2 : 0f), AllLines));   // every line: a note is never cut
                case UiFormKind.Entry:
                    return Math.Max(UiTokens.Control + UiTokens.S2 * 2f,
                        UiDraw.Snap(UiTextRules.WrappedHeight(r.Label, UiStyle.Body, EntryWords(ref r, iw), AllLines)) + UiTokens.S3 * 2f);
                case UiFormKind.Swatches:
                {
                    int n = r.Swatches != null ? r.Swatches.Length : 0;
                    int across = SwatchesAcross(ControlWidth(w));
                    int lines = Math.Max(1, (n + across - 1) / across);
                    return Math.Max(UiTokens.Control, lines * (Swatch + UiTokens.S2) + UiTokens.S2);
                }
                case UiFormKind.Choice:
                case UiFormKind.Amount:
                case UiFormKind.Text:
                case UiFormKind.Switch:
                case UiFormKind.Slider:
                    return Labelled(ref r, w);
                default:
                    return UiTokens.Control;
            }
        }

        // A control's row: one line of its words beside the control, or as many as they need when they do not fit on one (a
        // delivery's goods with a long name, a language with long words), the row as tall as they need; a chosen option too
        // long for its field is read whole in a line under the row.
        private static float Labelled(ref UiFormRow r, float w)
        {
            float lw = LabelWidth(w) - Lead(ref r);
            float h = UiTokens.Control;
            if (!string.IsNullOrEmpty(r.Label) && !UiTextRules.Fits(r.Label, UiStyle.Body, lw))
            {
                h = Math.Max(h, UiDraw.Snap(UiTextRules.WrappedHeight(r.Label, UiStyle.Body, lw, AllLines)) + UiTokens.S1 * 2f);
            }
            if (!string.IsNullOrEmpty(r.Picture))
            {
                h = Math.Max(h, UiTokens.ControlSmall + UiTokens.S1 * 2f);
            }
            return h + Overflow(ref r, w);
        }

        // The picture's width at a row's left with the gap after it; none where the game shows none.
        private static float Lead(ref UiFormRow r) => string.IsNullOrEmpty(r.Picture) ? 0f : PictureWidth;

        // The game's own picture at a row's left (a delivery's goods), on a tile in the middle of the row's height; the width it takes.
        private static float Picture(UiScene s, UiAt at, ref UiFormRow r, float x, float y, float h)
        {
            if (string.IsNullOrEmpty(r.Picture))
            {
                return 0f;
            }
            UiDraw.PictureFace(s, at, x, y + Math.Max(0f, UiDraw.SnapDown((h - UiTokens.ControlSmall) / 2f)), UiTokens.ControlSmall, r.Picture);
            return PictureWidth;
        }

        // What a choice's chosen words need under the row when its field cannot hold them (a business with a long name).
        private static float Overflow(ref UiFormRow r, float w)
        {
            if (r.Kind != UiFormKind.Choice || string.IsNullOrEmpty(r.Value) || UiTextRules.Fits(r.Value, UiStyle.Body, ControlWidth(w) - ChoiceInset))
            {
                return 0f;
            }
            // The room a scrolled form leaves, so no row is drawn taller than it was made.
            return UiTokens.S1 + UiDraw.Snap(UiTextRules.WrappedHeight(r.Value, UiStyle.Small, w - UiTokens.S3 * 3f, AllLines));
        }

        // The words' room inside a choice's field: its padding and the caret at its right (UiDraw.Choice).
        private const float ChoiceInset = UiTokens.S3 + UiTokens.Icon + UiTokens.S2 + UiTokens.S4;

        // The lines a text takes in a width, whole: what a drawn text is told, so its box holds every one.
        private static int Fit(string text, UiStyle style, float w) => Math.Max(1, UiTextRules.Lines(text, style, w, 0, out float _));

        // The words' room beside the controls' column, at a band's width: the narrower room a scrolled form leaves (its
        // thumb's lane), so a row is tall enough for its words whether the form scrolls or not.
        private static float LabelWidth(float w) => Math.Max(0f, w - UiTokens.S3 * 3f - ControlWidth(w) - UiTokens.S4);

        /// <summary>The rows' height at the band's width, a step between each.</summary>
        public static float ContentHeight(UiFormRow[] rows, int count, float w)
        {
            float h = 0f;
            for (int i = 0; i < count; i++)
            {
                h += RowHeight(ref rows[i], w) + (i > 0 ? UiTokens.S2 : 0f);
            }
            return h;
        }

        /// <summary>The band's height: all its rows, or the room it is given when they need more (then it scrolls).</summary>
        public static float Height(UiFormRow[] rows, int count, float w, UiFormPick pick)
        {
            float h = ContentHeight(rows, count, w) + UiTokens.S3 * 2f;
            if (pick != null && pick.Most > 0f)
            {
                h = Math.Min(h, Math.Max(UiTokens.Control * 2f + UiTokens.S3 * 2f, UiDraw.SnapDown(pick.Most)));
            }
            return UiDraw.Snap(h);
        }

        /// <summary>Lays the form into its band at a place: the rows, the open choice's list over them, the scroll when it needs one.</summary>
        public static void Lay(UiScene s, UiAt at, UiFormRow[] rows, int count, UiFormPick pick, float x, float y, float w)
        {
            pick = pick ?? new UiFormPick();
            float h = Height(rows, count, w, pick);
            UiDraw.Band(s, at, x, y, w, h);
            float content = ContentHeight(rows, count, w);
            float room = h - UiTokens.S3 * 2f;
            bool scrolls = content > room + 0.5f;
            pick.Clip = -1;
            pick.ListClip = -1;
            pick.List = -1;
            UiAt into = at;
            float ox = x, oy = y + UiTokens.S3, offset = 0f;
            float iw = w - UiTokens.S3 * 2f - (scrolls ? UiTokens.S3 : 0f);   // a scrolled form keeps its controls clear of the thumb
            if (scrolls)
            {
                offset = Math.Max(0f, Math.Min(Wanted(rows, count, w, pick, room), content - room));
                pick.Offset = offset;
                short clip = s.AddClip(x, y + UiTokens.S3, w, room, offset);
                s.Clips[clip].ContentHeight = content;
                pick.Clip = clip;
                UiDraw.ScrollThumb(s, at, x + w - UiTokens.S2, y + UiTokens.S3, room, offset, content);
                into = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
                ox = 0f;
                oy = 0f;
            }
            else
            {
                pick.Offset = 0f;
            }
            pick.Reveal = -1;
            int targets = 0, first = -1, shown = 0;
            short list = -1;
            if (scrolls)
            {
                for (int i = 0; i < count; i++)
                {
                    targets += Takes(ref rows[i]) ? 1 : 0;
                }
                list = s.AddList(targets, 0, 0, pick.Clip, -1);
                pick.List = list;
            }
            float cw = ControlWidth(w);
            float cx = ox + UiTokens.S3 + iw - cw;   // the controls' left edge, one for every row
            pick.ControlX = (scrolls ? x : 0f) + cx;
            pick.ControlW = cw;
            float ry = 0f;
            int ordinal = 0;
            float openY = -1f;
            for (int i = 0; i < count; i++)
            {
                ref UiFormRow r = ref rows[i];
                float rh = RowHeight(ref r, w);
                bool takes = Takes(ref r);
                // One cut by the band's edge waits until it scrolls in whole; one taller than the room (a very long note) shows as it scrolls past.
                bool whole = !scrolls || (ry >= offset - 0.5f && ry + rh <= offset + room + 0.5f) || (rh > room && ry < offset + room && ry + rh > offset);
                if (whole)
                {
                    float top = oy + ry;
                    Row(s, into, ref r, i, ox + UiTokens.S3, top, iw, cx, cw, rh, pick, list, ordinal);
                    if (takes && scrolls)
                    {
                        first = first < 0 ? ordinal : first;
                        shown++;
                    }
                    if (i == pick.Row && r.Kind == UiFormKind.Choice)
                    {
                        openY = (scrolls ? y + UiTokens.S3 - offset : 0f) + top + UiDraw.SnapDown((rh - UiTokens.Control) / 2f);   // under the field, in the middle of its row
                    }
                }
                ordinal += takes ? 1 : 0;
                ry += rh + UiTokens.S2;
            }
            if (list >= 0)
            {
                s.Lists[list].First = Math.Max(0, first);
                s.Lists[list].Shown = shown;
            }
            if (openY >= 0f && pick.Count > 0)
            {
                OpenList(s, at, pick, (scrolls ? x : 0f) + cx, openY, cw);
            }
        }

        // The scroll that keeps the row the keys asked for in view, or the scroll as it was.
        private static float Wanted(UiFormRow[] rows, int count, float w, UiFormPick pick, float room)
        {
            if (pick.Reveal < 0)
            {
                return pick.Offset;
            }
            float ry = 0f;
            int ordinal = 0;
            for (int i = 0; i < count; i++)
            {
                float rh = RowHeight(ref rows[i], w);
                if (Takes(ref rows[i]))
                {
                    if (ordinal == pick.Reveal)
                    {
                        return ry < pick.Offset ? ry : ry + rh > pick.Offset + room ? ry + rh - room : pick.Offset;
                    }
                    ordinal++;
                }
                ry += rh + UiTokens.S2;
            }
            return pick.Offset;
        }

        // An open choice's list over the form, under its field or over it when the screen ends first, as the game's opens.
        private static void OpenList(UiScene s, UiAt at, UiFormPick pick, float x, float fieldY, float w)
        {
            float lh = UiDraw.ChoiceListHeight(pick.Count);
            float below = fieldY + UiTokens.Control + UiTokens.S1;
            float ly = below + lh <= s.Height - UiTokens.S16 ? below : Math.Max(UiTokens.S4, fieldY - UiTokens.S1 - lh);
            float viewH = lh - UiTokens.S1 * 2f, rowH = UiTokens.ControlSmall;
            pick.ListOffset = Math.Max(0f, Math.Min(pick.ListOffset, Math.Max(0f, pick.Count * rowH - viewH)));
            int clips = s.ClipCount;
            short list = UiDraw.ChoiceList(s, at, x, UiDraw.Snap(ly), w, pick.Options, pick.Count, pick.Chosen, pick.ListOffset, OptionCommand);
            pick.ListClip = s.ClipCount > clips ? s.Lists[list].Clip : (short)-1;
        }

        // One row at its place: its words at the left, its control in the column at the right.
        private static void Row(UiScene s, UiAt at, ref UiFormRow r, int i, float x, float y, float w, float cx, float cw, float h, UiFormPick pick, short list, int ordinal)
        {
            float labelW = Math.Max(0f, cx - UiTokens.S4 - x);
            bool label = !string.IsNullOrEmpty(r.Label);
            switch (r.Kind)
            {
                case UiFormKind.Choice:
                case UiFormKind.Amount:
                case UiFormKind.Text:
                case UiFormKind.Switch:
                case UiFormKind.Slider:
                {
                    float fx = label ? cx : x, fw = label ? cw : w;
                    UiRole ink = r.Disabled ? UiRole.Muted : UiRole.Text;
                    float extra = Overflow(ref r, w + UiTokens.S3 * 2f + (pick.Clip >= 0 ? UiTokens.S3 : 0f));   // the band's width again, as its height was made
                    float rowH = h - extra;   // the row's own height: its control and words, above the chosen words read whole
                    float lead = Picture(s, at, ref r, x, y, rowH);
                    float lx = x + lead, lw = labelW - lead;
                    if (label && UiTextRules.Fits(r.Label, UiStyle.Body, lw))
                    {
                        UiDraw.TextMiddle(s, at, lx, y, rowH, lw, UiStyle.Body, ink, r.Label, UiFlags.Label | UiFlags.Truncate);
                    }
                    else if (label)
                    {
                        // Words too long for one line take as many as they need, in the middle of the row beside the control.
                        int lines = Fit(r.Label, UiStyle.Body, lw);
                        float th = UiDraw.Snap(UiTextRules.WrappedHeight(r.Label, UiStyle.Body, lw, lines));
                        UiDraw.Text(s, at, lx, y + Math.Max(0f, UiDraw.SnapDown((rowH - th) / 2f)), lw, UiStyle.Body, ink, r.Label, UiFlags.Label | UiFlags.Wrap | UiFlags.Truncate, lines);
                    }
                    int node = Control(s, at, ref r, i, fx, y + Math.Max(0f, UiDraw.SnapDown((rowH - UiTokens.Control) / 2f)), fw, pick);
                    if (extra > 0f)
                    {
                        float vw = w;
                        UiDraw.Text(s, at, x, y + rowH + UiTokens.S1, vw, UiStyle.Small, UiRole.Text2, r.Value, UiFlags.Wrap | UiFlags.Truncate, Fit(r.Value, UiStyle.Small, vw));
                    }
                    Listed(s, node, list, ordinal);
                    break;
                }
                case UiFormKind.Figure:
                {
                    // A figure the game has not written yet shows its hint, muted, in its place.
                    UiStyle style = r.Strong ? UiStyle.BodyStrong : UiStyle.Body;
                    bool unset = string.IsNullOrEmpty(r.Value);
                    string figure = (unset ? r.Hint : r.Value) ?? string.Empty;
                    UiStyle figureStyle = unset ? UiStyle.Body : UiStyle.BodyStrong;
                    float vw = Math.Min(UiDraw.Snap(UiTextRules.Width(figure, figureStyle) + UiTokens.S1), UiDraw.SnapDown(w * 0.6f));
                    float lead = Picture(s, at, ref r, x, y, h);
                    UiDraw.TextMiddle(s, at, x + lead, y, h, w - vw - UiTokens.S3 - lead, style, r.Strong ? UiRole.Text : UiRole.Text2, r.Label, UiFlags.Label | UiFlags.Truncate);
                    UiDraw.TextMiddle(s, at, x + w - vw, y, h, vw, figureStyle, unset ? UiRole.Muted : UiRole.Text, figure, UiFlags.Right | UiFlags.Truncate);
                    break;
                }
                case UiFormKind.Note:
                {
                    float tx = x;
                    if (r.Warn)
                    {
                        UiDraw.Icon(s, at, x, y + (UiTextRules.LineHeight(UiStyle.Small) - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Warning, UiRole.Warn, UiFlags.Middle, true);
                        tx += UiTokens.IconSmall + UiTokens.S2;
                    }
                    UiDraw.Text(s, at, tx, y, x + w - tx, UiStyle.Small, r.Warn ? UiRole.WarnText : UiRole.Text2, r.Label, UiFlags.Wrap | UiFlags.Truncate, Fit(r.Label, UiStyle.Small, x + w - tx));
                    break;
                }
                case UiFormKind.Heading:
                {
                    // Its words at the left; what the list under it holds, small at the right, where it says.
                    float vw = string.IsNullOrEmpty(r.Value) ? 0f : Math.Min(UiDraw.Snap(UiTextRules.Width(r.Value, UiStyle.Small) + UiTokens.S1), UiDraw.SnapDown(w * 0.5f));
                    UiDraw.Text(s, at, x, y, w - (vw > 0f ? vw + UiTokens.S3 : 0f), UiStyle.SectionHeader, UiRole.Text2, r.Label, UiFlags.Label | UiFlags.Truncate);
                    if (vw > 0f)
                    {
                        UiDraw.TextMiddle(s, at, x + w - vw, y, h, vw, UiStyle.Small, UiRole.Text2, r.Value, UiFlags.Right | UiFlags.Truncate);
                    }
                    break;
                }
                case UiFormKind.Pick:
                    Listed(s, Pick(s, at, ref r, i, x, y, w, h), list, ordinal);
                    break;
                case UiFormKind.Check:
                {
                    // A need: the tick where it is met, the warning where not, its words, and what meets it at the right.
                    float vw = string.IsNullOrEmpty(r.Value) ? 0f : Math.Min(UiDraw.Snap(UiTextRules.Width(r.Value, UiStyle.Small) + UiTokens.S1), UiDraw.SnapDown(w * 0.5f));
                    UiDraw.Icon(s, at, x, y + (h - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, r.On ? UiIcon.Check : UiIcon.Warning, r.On ? UiRole.OkText : UiRole.WarnText, UiFlags.Middle);
                    float tx = x + UiTokens.IconSmall + UiTokens.S2;
                    UiDraw.TextMiddle(s, at, tx, y, h, x + w - tx - (vw > 0f ? vw + UiTokens.S3 : 0f), UiStyle.Body, r.On ? UiRole.Text : UiRole.WarnText, r.Label,
                        UiFlags.Label | UiFlags.Truncate);
                    if (vw > 0f)
                    {
                        UiDraw.TextMiddle(s, at, x + w - vw, y, h, vw, UiStyle.Small, UiRole.Text2, r.Value, UiFlags.Right | UiFlags.Truncate);
                    }
                    break;
                }
                case UiFormKind.Segments:
                {
                    if (label)
                    {
                        UiDraw.TextMiddle(s, at, x, y, h, labelW, UiStyle.Body, UiRole.Text, r.Label, UiFlags.Label | UiFlags.Truncate);
                    }
                    Listed(s, Segments(s, at, ref r, i, label ? cx : x, y + Math.Max(0f, UiDraw.SnapDown((h - UiTokens.ControlSmall) / 2f)), label ? cw : w), list, ordinal);
                    break;
                }
                case UiFormKind.Break:
                    UiDraw.Box(s, at, x, y + UiTokens.S2, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                    break;
                case UiFormKind.Radio:
                    Listed(s, Option(s, at, ref r, i, x, y, w, h), list, ordinal);
                    break;
                case UiFormKind.Entry:
                    Listed(s, Entry(s, at, ref r, i, x, y, w, h), list, ordinal);
                    break;
                case UiFormKind.Action:
                {
                    float bw = Math.Min(w, UiDraw.ButtonWidth(UiButtonKind.Secondary, r.Action, UiIcon.Plus, UiAction.Count, null));
                    int node = UiDraw.Button(s, at, x, y, bw, UiTokens.Control, UiButtonKind.Secondary, r.Action, UiIcon.Plus, UiAction.Count, null, RowCommand, i, r.Disabled);
                    Listed(s, node, list, ordinal);
                    break;
                }
                case UiFormKind.Swatches:
                {
                    if (label)
                    {
                        UiDraw.TextMiddle(s, at, x, y, UiTokens.Control, labelW, UiStyle.Body, UiRole.Text, r.Label, UiFlags.Label | UiFlags.Truncate);
                    }
                    Listed(s, Swatches(s, at, ref r, i, label ? cx : x, y, label ? cw : w), list, ordinal);
                    break;
                }
            }
        }

        // A row's control in the column, as the kit's own controls draw it. Returns its focus target.
        private static int Control(UiScene s, UiAt at, ref UiFormRow r, int i, float x, float y, float w, UiFormPick pick)
        {
            switch (r.Kind)
            {
                case UiFormKind.Choice:
                    return UiDraw.Choice(s, at, x, y, w, string.IsNullOrEmpty(r.Value) ? r.Hint : r.Value, FieldCommand, i, r.Disabled);
                case UiFormKind.Amount:
                {
                    float sw = r.Remove ? w - UiTokens.ControlSmall - UiTokens.S2 : w;
                    int node = UiDraw.Stepper(s, at, x, y, sw, r.Value, r.CanLess, r.CanMore, LessCommand, FieldCommand, MoreCommand, i, r.Disabled, r.Hint);
                    if (r.Remove)
                    {
                        UiDraw.IconButton(s, at, x + w - UiTokens.ControlSmall, y + (UiTokens.Control - UiTokens.ControlSmall) / 2f, UiTokens.ControlSmall, UiIcon.Trash,
                            UiRole.Danger, RemoveCommand, i);
                    }
                    return node;
                }
                case UiFormKind.Switch:
                    return UiDraw.Switch(s, at, x, y, w, r.On, r.Value, FieldCommand, i, r.Disabled);
                case UiFormKind.Slider:
                    return UiDraw.Slider(s, at, x, y, w, r.Value, r.Share, FieldCommand, i, r.Disabled, pick.Held == i);
                default:
                    return UiDraw.TextField(s, at, x, y, w, r.Value, r.Hint, FieldCommand, i, r.Disabled);
            }
        }

        // One of a few options, as a radio row: the ring, filled when it is the one chosen, and its words.
        private static int Option(UiScene s, UiAt at, ref UiFormRow r, int i, float x, float y, float w, float h)
        {
            int face = UiDraw.Box(s, at, x, y, w, h, r.On ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge,
                (r.On ? UiFlags.Selected : UiFlags.None) | (r.Disabled ? UiFlags.Disabled : UiFlags.None));
            UiAt inside = at.In(face);
            float ry = y + (h - Ring) / 2f;
            int ring = UiDraw.Circle(s, inside, x + UiTokens.S2, ry, Ring, Ring, r.On ? UiRole.Accent : UiRole.Track, UiFlags.Middle);
            if (r.On)
            {
                UiDraw.Circle(s, inside.In(ring), x + UiTokens.S2 + (Ring - Dot) / 2f, ry + (Ring - Dot) / 2f, Dot, Dot, UiRole.OnAccent, UiFlags.Middle);
            }
            float tx = x + UiTokens.S2 + Ring + UiTokens.S3;
            UiDraw.TextMiddle(s, inside, tx, y, h, x + w - UiTokens.S2 - tx, UiStyle.Body, r.Disabled ? UiRole.Muted : UiRole.Text, r.Label, UiFlags.Truncate);
            return UiDraw.Node(s, at, x, y, w, h, RowCommand, i, face, r.Disabled);
        }

        // A line of a list (a contract, a delivery): its words at the left, wrapped, and its button at the right.
        private static int Entry(UiScene s, UiAt at, ref UiFormRow r, int i, float x, float y, float w, float h)
        {
            int face = UiDraw.Box(s, at, x, y, w, h, UiRole.Row, UiRole.None, UiTokens.RadiusLarge, UiFlags.None);
            UiAt inside = at.In(face);
            float tw = EntryWords(ref r, w);
            float lead = Picture(s, inside, ref r, x + UiTokens.S3, y, h);
            int lines = Fit(r.Label, UiStyle.Body, tw);
            float th = UiDraw.Snap(UiTextRules.WrappedHeight(r.Label, UiStyle.Body, tw, lines));
            UiDraw.Text(s, inside, x + UiTokens.S3 + lead, y + UiDraw.SnapDown((h - th) / 2f), tw, UiStyle.Body, UiRole.Text, r.Label, UiFlags.Wrap | UiFlags.Truncate, lines);
            if (string.IsNullOrEmpty(r.Action))
            {
                return -1;
            }
            float bw = UiDraw.ButtonWidth(UiButtonKind.Secondary, r.Action, UiIcon.None, UiAction.Count, null);
            return UiDraw.Button(s, inside, x + w - UiTokens.S3 - bw, y + (h - UiTokens.Control) / 2f, bw, UiTokens.Control, UiButtonKind.Secondary, r.Action, UiIcon.None,
                UiAction.Count, null, RowCommand, i, r.Disabled);
        }

        // The room an entry's words have beside its button.
        private static float EntryWords(ref UiFormRow r, float w) =>
            w - UiTokens.S3 * 2f - Lead(ref r) - (string.IsNullOrEmpty(r.Action) ? 0f : UiDraw.ButtonWidth(UiButtonKind.Secondary, r.Action, UiIcon.None, UiAction.Count, null) + UiTokens.S3);

        // Colours to pick, each a square of its own colour, the chosen one in the cyan's ring, as many lines as they need.
        // Returns the first colour's target, the row's place among those the keys reach.
        private static int Swatches(UiScene s, UiAt at, ref UiFormRow r, int i, float x, float y, float w)
        {
            int n = r.Swatches != null ? r.Swatches.Length : 0;
            int across = SwatchesAcross(w);
            int first = -1;
            for (int k = 0; k < n; k++)
            {
                float sx = x + (k % across) * (Swatch + UiTokens.S2), sy = y + UiTokens.S2 + (k / across) * (Swatch + UiTokens.S2);
                bool chosen = k == r.Chosen;
                int face = UiDraw.Box(s, at, sx, sy, Swatch, Swatch, UiRole.None, chosen ? UiRole.Accent : UiRole.Line, UiTokens.RadiusSmall,
                    chosen ? UiFlags.Selected : UiFlags.None);
                var picture = new UiItem
                {
                    Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre, X = sx + UiTokens.S1, Y = sy + UiTokens.S1, W = Swatch - UiTokens.S2, H = Swatch - UiTokens.S2,
                    Text = r.Swatches[k], Value = 1f, Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = face, Alpha = 1f
                };
                s.Add(ref picture);
                int node = UiDraw.Node(s, at, sx, sy, Swatch, Swatch, SwatchCommand, SwatchArg(i, k), face, r.Disabled);
                first = first < 0 ? node : first;
            }
            return first;
        }

        private static int SwatchesAcross(float w) => Math.Max(1, (int)((w + UiTokens.S2) / (Swatch + UiTokens.S2)));

        private static float PickHeight => UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong) + UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S2 * 2f;

        // A line of a list to choose from: its mark (an icon, or the initials of a name on its hue), its name and the line
        // under it, its figure at the right in the green with a small line under it, or why it cannot be chosen in a chip;
        // chosen, the cyan's tone, as an answer's row. Pressed as RowCommand. Returns its focus target.
        private static int Pick(UiScene s, UiAt at, ref UiFormRow r, int i, float x, float y, float w, float h)
        {
            int face = UiDraw.Box(s, at, x, y, w, h, r.On ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge,
                (r.On ? UiFlags.Selected : UiFlags.None) | (r.Disabled ? UiFlags.Disabled : UiFlags.None));
            UiAt inside = at.In(face);
            if (!r.On)
            {
                UiDraw.Box(s, inside, x + UiTokens.S2, y + h - UiTokens.Hairline, w - UiTokens.S2 * 2f, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            }
            float mark = UiTokens.AvatarSmall;
            float my = y + UiDraw.SnapDown((h - mark) / 2f);   // on the grid, its glyph too
            if (r.Icon != UiIcon.None)
            {
                int tile = UiDraw.Box(s, inside, x + UiTokens.S2, my, mark, mark, UiRole.Field, UiRole.None, UiTokens.RadiusMedium, UiFlags.Middle);
                UiDraw.Icon(s, inside.In(tile), x + UiTokens.S2 + (mark - UiTokens.IconSmall) / 2f, my + (mark - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, r.Icon,
                    r.Disabled ? UiRole.Muted : UiRole.AccentText, UiFlags.Middle);
            }
            else
            {
                UiDraw.Avatar(s, inside, x + UiTokens.S2, my, mark, r.Label ?? string.Empty, UiColour.HueOf(r.Label ?? string.Empty));
            }
            float tx = x + UiTokens.S2 + mark + UiTokens.S3;
            float right = x + w - UiTokens.S3;
            float sideW = 0f;
            if (r.Disabled && !string.IsNullOrEmpty(r.Reason))
            {
                float most = UiDraw.SnapDown((right - tx) * 0.5f);
                float cw = Math.Min(most, UiDraw.Snap(UiTextRules.Width(r.Reason, UiStyle.Small) + UiTokens.S3 * 2f));
                float ch = UiTokens.Badge + UiTokens.S1;
                int chip = UiDraw.Box(s, inside, right - cw, y + (h - ch) / 2f, cw, ch, UiRole.Pill, UiRole.None, ch / 2f, UiFlags.Middle);
                UiDraw.TextMiddle(s, inside.In(chip), right - cw + UiTokens.S3, y + (h - ch) / 2f, ch, cw - UiTokens.S3 * 2f, UiStyle.Small, UiRole.WarnText, r.Reason,
                    UiFlags.Centre | UiFlags.Truncate);
                sideW = cw + UiTokens.S3;
            }
            else if (!string.IsNullOrEmpty(r.Side))
            {
                Lines(h, out float top, out float nameBox, out float subBox);
                sideW = UiDraw.Snap(Math.Max(UiTextRules.Width(r.Side, UiStyle.BodyStrong), string.IsNullOrEmpty(r.SideNote) ? 0f : UiTextRules.Width(r.SideNote, UiStyle.Small)) + UiTokens.S1);
                sideW = Math.Min(sideW, UiDraw.SnapDown((right - tx) * 0.4f));
                if (string.IsNullOrEmpty(r.SideNote))
                {
                    UiDraw.TextMiddle(s, inside, right - sideW, y, h, sideW, UiStyle.BodyStrong, UiRole.OkText, r.Side, UiFlags.Right | UiFlags.Truncate);
                }
                else
                {
                    UiDraw.TextMiddle(s, inside, right - sideW, y + top, nameBox, sideW, UiStyle.BodyStrong, UiRole.OkText, r.Side, UiFlags.Right | UiFlags.Truncate);
                    UiDraw.TextMiddle(s, inside, right - sideW, y + top + nameBox, subBox, sideW, UiStyle.Small, r.On ? UiRole.Text : UiRole.Text2, r.SideNote,
                        UiFlags.Right | UiFlags.Truncate);
                }
                sideW += UiTokens.S3;
            }
            float tw = Math.Max(0f, right - sideW - tx);
            UiRole ink = r.Disabled ? UiRole.Muted : UiRole.Text;
            if (string.IsNullOrEmpty(r.Hint))
            {
                UiDraw.TextMiddle(s, inside, tx, y, h, tw, UiStyle.BodyStrong, ink, r.Label, UiFlags.Label | UiFlags.Truncate);
            }
            else
            {
                Lines(h, out float top, out float nameBox, out float subBox);
                UiDraw.TextMiddle(s, inside, tx, y + top, nameBox, tw, UiStyle.BodyStrong, ink, r.Label, UiFlags.Label | UiFlags.Truncate);
                UiDraw.TextMiddle(s, inside, tx, y + top + nameBox, subBox, tw, UiStyle.Small, r.Disabled ? UiRole.Muted : r.On ? UiRole.Text : UiRole.Text2, r.Hint,
                    UiFlags.Truncate);
            }
            return UiDraw.Node(s, at, x, y, w, h, RowCommand, i, face, r.Disabled);
        }

        // A pick's two lines on the grid: the name's box and the line's under it, held in the middle of the row.
        private static void Lines(float h, out float top, out float nameBox, out float subBox)
        {
            subBox = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small));
            nameBox = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.BodyStrong));
            top = UiDraw.SnapDown((h - nameBox - subBox) / 2f);
        }

        // A few short options side by side across the column, the chosen one in the cyan with its ink on it, the rest on
        // the field. Pressed as SwatchCommand with SwatchArg (its row and its place). Returns the first's target.
        private static int Segments(UiScene s, UiAt at, ref UiFormRow r, int i, float x, float y, float w)
        {
            int n = r.Options != null ? r.Options.Length : 0;
            float gap = UiTokens.S1;
            float each = n > 0 ? UiDraw.SnapDown((w - gap * (n - 1)) / n) : 0f;
            int first = -1;
            for (int k = 0; k < n; k++)
            {
                bool chosen = k == r.Chosen;
                float sx = x + k * (each + gap);
                int face = UiDraw.Box(s, at, sx, y, each, UiTokens.ControlSmall, chosen ? UiRole.Accent : UiRole.Field, UiRole.None, UiTokens.RadiusMedium,
                    (chosen ? UiFlags.Selected : UiFlags.None) | (r.Disabled ? UiFlags.Disabled : UiFlags.None));
                UiDraw.TextMiddle(s, at.In(face), sx + UiTokens.S1, y, UiTokens.ControlSmall, each - UiTokens.S1 * 2f, chosen ? UiStyle.SmallStrong : UiStyle.Small,
                    chosen ? UiRole.OnAccent : UiRole.Text, r.Options[k], UiFlags.Centre | UiFlags.Truncate);
                int node = UiDraw.Node(s, at, sx, y, each, UiTokens.ControlSmall, SwatchCommand, SwatchArg(i, k), face, r.Disabled);
                first = first < 0 ? node : first;
            }
            return first;
        }

        // A row's target as a row of the form's list while it scrolls, so the keys can ask for one scrolled away.
        private static void Listed(UiScene s, int node, short list, int ordinal)
        {
            if (list >= 0 && node >= 0 && node < s.NodeCount)
            {
                s.Nodes[node].List = list;
                s.Nodes[node].Index = ordinal;
            }
        }
    }
}
