using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a line of a tool panel is.</summary>
    public enum UiToolKind : byte
    {
        Text = 0,   // a paragraph: what the panel is waiting for, or what is happening
        Note,       // a quieter line under a paragraph or a row
        Link,       // a cyan link with the locate glyph: it moves the camera or opens the place
        Entry,      // a thing on a row: its picture or glyph on a tile, its name, a line under it, and one button
        Choice,     // what it sets over a choice (the game's Dropdown)
        Swatches,   // what it colours over a row of colours, the add button leading it
        Buttons,    // one or two buttons across the panel, the main one on the cyan
        Foot        // a figure on the darker band at the panel's foot: what it costs, when it is paid
    }

    /// <summary>One line of a tool panel. Which fields count depends on its kind, as each says.</summary>
    public struct UiToolLine
    {
        public UiToolKind Kind;
        public string Label;       // Text and Note: the words; Link: its words; Entry: the name; Choice and Swatches: what it sets; Buttons: the main button; Foot: what the figure is
        public string Value;       // Entry: the line under the name; Choice: the value shown; Buttons: the second button, null for one; Foot: the figure
        public string Picture;     // Entry: the game's own picture (UiPictures) on its tile, null for its glyph
        public UiIcon Icon;        // Entry: the glyph on its tile; Buttons: the main button's glyph
        public string Action;      // Entry: its button's words ("Call"), null for none
        public UiGameKey Key;      // Buttons: the game's key on the main button's cap, which the game answers
        public bool Main;          // Entry: its button on the cyan
        public bool Chosen;        // Entry: the row in the cyan at a fifth (the one in use)
        public bool Disabled;      // the button or the control takes no press now; a second button's state is SecondOff
        public bool SecondOff;     // Buttons: the second button takes no press now
        public string[] Swatches;  // Swatches: each colour's picture key (UiDraw.SwatchKey)
        public int SwatchCount;
        public int SwatchChosen;   // the colour in use, -1 for none
        public bool CanAdd;        // Swatches: the button that adds a colour of the player's own leads the row
    }

    /// <summary>
    /// What a tool panel shows: its title strip (glyph, title, the state in a word or two at its right, or a close), its
    /// lines down the panel, and, while a choice is open, that choice's options on the overlay under it.
    /// </summary>
    public sealed class UiToolPanelModel
    {
        public const int MostSwatchLines = 8;

        public string Title;                               // "Private driver"
        public UiIcon Icon = UiIcon.Info;
        public string State;                               // "Your cars", at the strip's right; shown only without a close
        public bool Close = true;                          // the strip's close, which the host takes as Back
        public UiToolLine[] Lines = new UiToolLine[0];
        public int LineCount;
        public int Open = -1;                              // the Choice line whose options are open, -1 for none
        public string[] Options = new string[0];
        public int OptionCount;
        public int OptionChosen = -1;
        public float OptionOffset;                         // px the open options are scrolled
    }

    /// <summary>
    /// A small tool panel over the world, as the game's little windows are (the private driver beside the phone, a sign's
    /// look, the preview terminal, the preview's bar, a colour picker's frame): a slate panel with its title strip, then
    /// its lines, each shaped by what it is (a paragraph, a link, a thing on a row with one button, a choice, a row of
    /// colours, the buttons across, a figure on the band at its foot). The panel only draws and reports; the screen behind
    /// it presses the game's own widgets. Commands: <see cref="LineCommand"/> (a line's index: its link, its entry's button,
    /// its choice, its main button), <see cref="SecondCommand"/> (a Buttons line's second), <see cref="AddCommand"/> (a
    /// Swatches line's add), <see cref="OptionCommand"/> (an open choice's option) and <see cref="SwatchCommand"/> plus the
    /// line's index (a colour, its index the argument). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiToolPanel
    {
        public const int LineCommand = 400, SecondCommand = 401, AddCommand = 402, OptionCommand = 403;
        public const int SwatchCommand = 410;     // plus the line's index, below SwatchCommand + MostLines
        public const int MostLines = 16;
        public const float Width = 360f;          // px: narrower than an info panel, as the game's little windows are
        private const float EntryRow = UiTokens.Row;   // px: a thing's row, its 40 px tile with room round it
        private const float Thumb = UiTokens.IconTile; // px: a thing's tile
        private const int TextLines = 6, NoteLines = 3;
        private const byte Region = 220;          // the panel's own column for the d-pad

        /// <summary>Whether a command is one of a Swatches line's colours, and which line.</summary>
        public static bool IsSwatch(int command, out int line)
        {
            line = command - SwatchCommand;
            return line >= 0 && line < MostLines;
        }

        /// <summary>The panel's height at a width, with every line.</summary>
        public static float Height(UiToolPanelModel m, float w)
        {
            float iw = w - UiTokens.S4 * 2f;
            float h = UiFrame.Strip + LinesHeight(m, iw);
            return UiDraw.Snap(h);
        }

        // The lines' room under the strip: a step above the first, each line's own height, a gap between two (none between two
        // entries, which read as one list, and none before the foot's band), and the padding under the last unless it is the foot.
        private static float LinesHeight(UiToolPanelModel m, float iw)
        {
            int n = Math.Min(m.LineCount, Math.Min(m.Lines.Length, MostLines));
            if (n == 0)
            {
                return UiTokens.S2;
            }
            float h = Lead(m.Lines[0].Kind);
            for (int i = 0; i < n; i++)
            {
                if (i > 0)
                {
                    h += Gap(m.Lines[i - 1].Kind, m.Lines[i].Kind);
                }
                h += LineHeight(ref m.Lines[i], iw);
            }
            return h + (m.Lines[n - 1].Kind == UiToolKind.Foot ? 0f : UiTokens.S4);
        }

        // An entry or the foot's band runs to the panel's edges and from the strip's foot; anything else stands a step in.
        private static float Lead(UiToolKind kind) => kind == UiToolKind.Entry || kind == UiToolKind.Foot ? 0f : UiTokens.S3;

        private static float Gap(UiToolKind before, UiToolKind now)
        {
            if (now == UiToolKind.Foot || (before == UiToolKind.Entry && now == UiToolKind.Entry))
            {
                return 0f;
            }
            if (now == UiToolKind.Note && before == UiToolKind.Text)
            {
                return UiTokens.S1;
            }
            return UiTokens.S3;
        }

        /// <summary>One line's height inside the panel's padding.</summary>
        public static float LineHeight(ref UiToolLine line, float iw)
        {
            switch (line.Kind)
            {
                case UiToolKind.Text:
                    return UiDraw.Snap(UiTextRules.WrappedHeight(line.Label, UiStyle.Body, iw, TextLines));
                case UiToolKind.Note:
                    return UiDraw.Snap(UiTextRules.WrappedHeight(line.Label, UiStyle.Small, iw, NoteLines));
                case UiToolKind.Link:
                    return UiTokens.PairRow;
                case UiToolKind.Entry:
                    return EntryRow;
                case UiToolKind.Choice:
                    return UiTokens.PairRow + UiTokens.Control;
                case UiToolKind.Swatches:
                    return UiTokens.PairRow + SwatchRoom(ref line, iw);
                case UiToolKind.Buttons:
                    return Stacked(ref line, iw) ? UiTokens.Control * 2f + UiTokens.S2 : UiTokens.Control;
                case UiToolKind.Foot:
                    return UiTokens.PairRow + UiTokens.S2 * 2f;
            }
            return 0f;
        }

        // A row of colours wraps under its add button's column, so the add stays first and the colours line up under each other.
        private static float SwatchRoom(ref UiToolLine line, float iw)
        {
            float lead = line.CanAdd ? UiDraw.Swatch + UiTokens.S2 : 0f;
            return UiDraw.SwatchesHeight(iw - lead, Math.Max(1, line.SwatchCount));
        }

        /// <summary>
        /// The panel as a screen of its own over the running game at a place (its top left, px of the layout), the open
        /// choice's options over it. The HUD under it draws the status bar.
        /// </summary>
        public static void Lay(UiScene s, UiToolPanelModel m, UiFrame frame, bool dark, IUiKeys keys, string name, float x, float y, float w = Width)
        {
            s.Begin(name, frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            Draw(s, UiAt.Screen, m, x, y, w, keys);
        }

        /// <summary>The panel alone at a place (its top left); returns its panel's item.</summary>
        public static int Draw(UiScene s, UiAt at, UiToolPanelModel m, float x, float y, float w, IUiKeys keys)
        {
            float h = Height(m, w);
            UiAt column = at.InRegion(Region);
            int panel = UiDraw.Box(s, column, x, y, w, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = column.In(panel);
            Strip(s, on, m, x, y, w);
            Lines(s, on, m, x, y + UiFrame.Strip, w, keys);
            return panel;
        }

        /// <summary>
        /// The panel drawn as a frame round a hole the game's own control shows through (a colour picker the kit cannot draw
        /// flat): the strip over the hole, a margin either side of it and the lines under it, one slate round the game's piece.
        /// The hole is px of the layout. Returns the strip's piece.
        /// </summary>
        public static int Around(UiScene s, UiAt at, UiToolPanelModel m, float holeX, float holeY, float holeW, float holeH, IUiKeys keys)
        {
            holeX = UiDraw.SnapDown(holeX);
            holeY = UiDraw.SnapDown(holeY);
            holeW = UiDraw.Snap(holeW);
            holeH = UiDraw.Snap(holeH);
            float pad = UiTokens.S3, w = UiDraw.Snap(holeW + pad * 2f), x = UiDraw.SnapDown(holeX - pad);
            float top = UiFrame.Strip + pad, under = LinesHeight(m, w - UiTokens.S4 * 2f);
            float y = UiDraw.SnapDown(holeY - top), foot = UiDraw.Snap(holeY + holeH);
            UiAt column = at.InRegion(Region);
            // Four pieces of one slate: the head and the foot whole, the sides overlapping both by a corner, so the hole's
            // corners are square and the frame's outer corners the panel's own.
            int head = UiDraw.Box(s, column, x, y, w, top, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            int bottom = UiDraw.Box(s, column, x, foot, w, under, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            float sy = y + top - UiTokens.RadiusPanel, sh = foot - y - top + UiTokens.RadiusPanel * 2f;
            UiDraw.Box(s, column, x, sy, holeX - x, sh, UiRole.Panel, UiRole.None, 0f, UiFlags.Decor);
            UiDraw.Box(s, column, holeX + holeW, sy, x + w - holeX - holeW, sh, UiRole.Panel, UiRole.None, 0f, UiFlags.Decor);
            UiAt on = column.In(head);
            Strip(s, on, m, x, y, w);
            Lines(s, column.In(bottom), m, x, foot, w, keys);
            return head;
        }

        /// <summary>The height a frame round a hole adds under it, for a screen that keeps the frame on the screen.</summary>
        public static float AroundFoot(UiToolPanelModel m, float holeW) => LinesHeight(m, UiDraw.Snap(holeW + UiTokens.S3 * 2f) - UiTokens.S4 * 2f);

        // The strip: its glyph, the title in cyan capitals and the close, as every panel's; or, with the state in its place, the
        // title after the glyph and the state in a word or two at the right, as the mockup's little windows read.
        private static void Strip(UiScene s, UiAt at, UiToolPanelModel m, float x, float y, float w)
        {
            if (m.Close || string.IsNullOrEmpty(m.State))
            {
                UiDraw.TitleStrip(s, at, x, y, w, m.Icon, m.Title, m.Close);
                return;
            }
            UiDraw.TitleStrip(s, at, x, y, w, m.Icon, null, false);
            float sw = Math.Min(UiDraw.Snap(UiTextRules.Width(m.State, UiStyle.Small) + UiTokens.S1), UiDraw.SnapDown(w * 0.4f));
            float tx = x + UiTokens.S3 + UiTokens.Icon + UiTokens.S2, tw = x + w - UiTokens.S3 - sw - UiTokens.S3 - tx;
            UiDraw.TextMiddle(s, at, tx, y, UiFrame.Strip, tw, UiStyle.SectionTitle, UiRole.AccentText, m.Title, UiFlags.Truncate | UiFlags.Chrome);
            UiDraw.TextMiddle(s, at, x + w - UiTokens.S3 - sw, y, UiFrame.Strip, sw, UiStyle.Small, UiRole.Text2, m.State, UiFlags.Right | UiFlags.Truncate | UiFlags.Chrome);
        }

        // The lines from the strip's foot down, the open choice's options last so they lie over everything under it.
        private static void Lines(UiScene s, UiAt at, UiToolPanelModel m, float x, float y, float w, IUiKeys keys)
        {
            int n = Math.Min(m.LineCount, Math.Min(m.Lines.Length, MostLines));
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f;
            float ly = y + (n > 0 ? Lead(m.Lines[0].Kind) : 0f);
            float openX = 0f, openY = 0f, openW = 0f;
            UiAt openAt = at;
            for (int i = 0; i < n; i++)
            {
                ref UiToolLine line = ref m.Lines[i];
                if (i > 0)
                {
                    ly += Gap(m.Lines[i - 1].Kind, line.Kind);
                }
                float lh = LineHeight(ref line, iw);
                switch (line.Kind)
                {
                    case UiToolKind.Text:
                        UiDraw.Text(s, at, ix, ly, iw, UiStyle.Body, UiRole.Text, line.Label, UiFlags.Wrap | UiFlags.Middle, TextLines);
                        break;
                    case UiToolKind.Note:
                        UiDraw.Text(s, at, ix, ly, iw, UiStyle.Small, UiRole.Text2, line.Label, UiFlags.Wrap | UiFlags.Middle, NoteLines);
                        break;
                    case UiToolKind.Link:
                        Link(s, at, ix, ly, iw, line.Label, i);
                        break;
                    case UiToolKind.Entry:
                        Entry(s, at, ref line, i, x, ly, w);
                        break;
                    case UiToolKind.Choice:
                        UiDraw.TextMiddle(s, at, ix, ly, UiTokens.PairRow, iw, UiStyle.Small, UiRole.Text2, line.Label, UiFlags.Label | UiFlags.Truncate);
                        UiDraw.Choice(s, at, ix, ly + UiTokens.PairRow, iw, line.Value, LineCommand, i, line.Disabled);
                        if (i == m.Open)
                        {
                            openX = ix;
                            openY = ly + UiTokens.PairRow + UiTokens.Control + UiTokens.S1;
                            openW = iw;
                            openAt = at;
                        }
                        break;
                    case UiToolKind.Swatches:
                        Colours(s, at, ref line, i, ix, ly, iw);
                        break;
                    case UiToolKind.Buttons:
                        Buttons(s, at, ref line, i, ix, ly, iw, keys);
                        break;
                    case UiToolKind.Foot:
                        UiDraw.Band(s, at, x, ly, w, lh);
                        UiDraw.InfoRow(s, at, ix, ly + UiTokens.S2, iw, line.Label, line.Value);
                        break;
                }
                ly += lh;
            }
            if (m.Open >= 0 && openW > 0f && m.OptionCount > 0)
            {
                UiDraw.ChoiceList(s, openAt, openX, openY, openW, m.Options, Math.Min(m.OptionCount, m.Options.Length), m.OptionChosen, m.OptionOffset, OptionCommand);
            }
        }

        // A link: the locate glyph and the words in the cyan, the whole line a target that moves the camera to the place.
        private static void Link(UiScene s, UiAt at, float x, float y, float w, string words, int line)
        {
            float h = UiTokens.PairRow;
            float room = w - UiTokens.IconSmall - UiDraw.LinkGap;
            string shown = UiTextRules.Cut(words, UiStyle.BodyStrong, room);
            float tw = Math.Min(UiDraw.Snap(UiTextRules.Width(shown, UiStyle.BodyStrong) + UiTokens.S1), room);
            float fw = UiTokens.S1 + UiTokens.IconSmall + UiDraw.LinkGap + tw + UiTokens.S1;
            int face = UiDraw.Box(s, at, x - UiTokens.S1, y, fw, h, UiRole.None, UiRole.None, UiTokens.RadiusMedium);
            UiAt inside = at.In(face);
            UiDraw.Icon(s, inside, x, y + (h - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Locate, UiRole.AccentText, UiFlags.Middle | UiFlags.Decor);
            UiDraw.TextMiddle(s, inside, x + UiTokens.IconSmall + UiDraw.LinkGap, y, h, tw, UiStyle.BodyStrong, UiRole.AccentText, shown, UiFlags.Truncate);
            UiDraw.Node(s, at, x - UiTokens.S1, y, fw, h, LineCommand, line, face);
        }

        // A thing on its row across the panel: the game's picture (or its glyph) on a tile, its name over its line, its
        // button at the right; the one in use on the cyan at a fifth, never a mark down its side.
        private static void Entry(UiScene s, UiAt at, ref UiToolLine e, int line, float x, float y, float w)
        {
            int row = UiDraw.Box(s, at, x, y, w, EntryRow, e.Chosen ? UiRole.RowSel : UiRole.None, UiRole.None, 0f, e.Chosen ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(row);
            float tx = x + UiTokens.S4, ty = y + (EntryRow - Thumb) / 2f;
            Thumbnail(s, inside, tx, ty, e.Picture, e.Icon);
            float right = x + w - UiTokens.S4;
            if (!string.IsNullOrEmpty(e.Action))
            {
                float bw = UiDraw.ButtonWidth(e.Main ? UiButtonKind.Primary : UiButtonKind.Secondary, e.Action, UiIcon.None, UiAction.Count, null);
                bw = Math.Min(bw, UiDraw.SnapDown(w * 0.4f));
                UiDraw.Button(s, inside, right - bw, y + (EntryRow - UiTokens.ControlSmall) / 2f, bw, UiTokens.ControlSmall, e.Main ? UiButtonKind.Primary : UiButtonKind.Secondary,
                    e.Action, UiIcon.None, UiAction.Count, null, LineCommand, line, e.Disabled);
                right -= bw + UiTokens.S3;
            }
            float nx = tx + Thumb + UiTokens.S3, nw = Math.Max(0f, right - nx);
            bool sub = !string.IsNullOrEmpty(e.Value);
            float nh = UiTextRules.LineHeight(UiStyle.BodyStrong), sh = sub ? UiTextRules.LineHeight(UiStyle.Small) : 0f;
            float ny = y + (EntryRow - nh - sh) / 2f;
            UiDraw.Text(s, inside, nx, ny, nw, UiStyle.BodyStrong, UiRole.Text, e.Label, UiFlags.Truncate | UiFlags.Middle);
            if (sub)
            {
                UiDraw.Text(s, inside, nx, ny + nh, nw, UiStyle.Small, UiRole.Text2, e.Value, UiFlags.Truncate | UiFlags.Middle);
            }
        }

        /// <summary>
        /// A thing's tile: the game's own picture on it, letterboxed in its own shape (a car, a sofa), or the glyph in its own
        /// colour when the game has none. Returns the tile.
        /// </summary>
        public static int Thumbnail(UiScene s, UiAt at, float x, float y, string picture, UiIcon icon, float size = Thumb)
        {
            int tile = UiDraw.Tile(s, at, x, y, size, UiRole.None, false);
            if (!string.IsNullOrEmpty(picture))
            {
                float inset = UiTokens.S1, side = size - inset * 2f;
                var item = new UiItem
                {
                    Kind = UiKind.Picture, Text = picture, Value = 1f, X = x + inset, Y = y + inset, W = side, H = side, Flags = UiFlags.Middle | UiFlags.Centre,
                    Clip = at.Clip, Node = -1, Layer = at.Layer, Order = at.Order, Parent = tile, Alpha = 1f
                };
                s.Add(ref item);
                return tile;
            }
            float g = UiTokens.Icon;
            UiDraw.Icon(s, at.In(tile), x + (size - g) / 2f, y + (size - g) / 2f, g, icon == UiIcon.None ? UiIcon.Package : icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            return tile;
        }

        // What a row of colours sets, then the add button (a plus on its tile) and the colours wrapping after it.
        private static void Colours(UiScene s, UiAt at, ref UiToolLine line, int index, float x, float y, float w)
        {
            UiDraw.TextMiddle(s, at, x, y, UiTokens.PairRow, w, UiStyle.Small, UiRole.Text2, line.Label, UiFlags.Label | UiFlags.Truncate);
            float cy = y + UiTokens.PairRow, cx = x, cw = w;
            int first = s.NodeCount;
            if (line.CanAdd)
            {
                UiDraw.IconButton(s, at, x, cy, UiDraw.Swatch, UiIcon.Plus, UiRole.AccentText, AddCommand, index);
                cx += UiDraw.Swatch + UiTokens.S2;
                cw -= UiDraw.Swatch + UiTokens.S2;
            }
            UiDraw.Swatches(s, at, cx, cy, cw, line.Swatches, line.SwatchCount, line.SwatchChosen, SwatchCommand + index, line.Disabled);
            if (line.CanAdd && s.NodeCount > first + 1)
            {
                // The add and the colours are one row for the d-pad: Right from the add reaches the first colour.
                s.Nodes[first].Left = first;
                s.Nodes[first].Right = first + 1;
                s.Nodes[first + 1].Left = first;
            }
        }

        // A main button's room for its words: its padding, the game's key on its cap and its glyph.
        private static float MainNeed(ref UiToolLine line) =>
            UiTextRules.Width(line.Label, UiStyle.Button) + UiTokens.S5 * 2f + (line.Key.Shown ? UiDraw.GameKeyWidth(line.Key) + UiTokens.S2 : 0f)
            + (line.Icon != UiIcon.None ? UiTokens.Icon + UiTokens.S2 : 0f);

        /// <summary>Whether a Buttons line's two buttons stand one over the other: either one's words would not fit half the width.</summary>
        public static bool Stacked(ref UiToolLine line, float iw)
        {
            if (string.IsNullOrEmpty(line.Value))
            {
                return false;
            }
            float half = UiDraw.SnapDown((iw - UiTokens.S2) / 2f);
            return MainNeed(ref line) > half || UiDraw.ButtonWidth(UiButtonKind.Secondary, line.Value, UiIcon.None, UiAction.Count, null) > half;
        }

        // One button across the panel, or two halves side by side (the second on the field at the left, the main one on the
        // cyan at the right), or, when either's words would not fit half, the main one over the second, each across the panel.
        private static void Buttons(UiScene s, UiAt at, ref UiToolLine line, int index, float x, float y, float w, IUiKeys keys)
        {
            bool two = !string.IsNullOrEmpty(line.Value), stacked = Stacked(ref line, w);
            float half = two && !stacked ? UiDraw.SnapDown((w - UiTokens.S2) / 2f) : w;
            float mx = two && !stacked ? x + w - half : x;
            if (two)
            {
                float sy = stacked ? y + UiTokens.Control + UiTokens.S2 : y;
                UiDraw.Button(s, at, x, sy, half, UiTokens.Control, UiButtonKind.Secondary, line.Value, UiIcon.None, UiAction.Count, keys, SecondCommand, index, line.SecondOff);
            }
            int drawn = s.ItemCount;
            int face = UiDraw.Box(s, at, mx, y, half, UiTokens.Control, UiRole.Accent, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            float kw = line.Key.Shown ? UiDraw.GameKeyWidth(line.Key) + UiTokens.S2 : 0f;
            float gw = line.Icon != UiIcon.None ? UiTokens.Icon + UiTokens.S2 : 0f;
            float words = Math.Min(UiDraw.Snap(UiTextRules.Width(line.Label, UiStyle.Button) + UiTokens.S1), half - UiTokens.S4 * 2f - kw - gw);
            float cx = mx + UiDraw.SnapDown((half - kw - gw - words) / 2f);
            int node = UiDraw.Node(s, at, mx, y, half, UiTokens.Control, LineCommand, index, face, line.Disabled);
            // After the target: the game, not the button, answers the key on the cap.
            UiDraw.GameKey(s, inside, cx, y + (UiTokens.Control - UiTokens.KeyCap) / 2f, line.Key, !line.Disabled, line.Disabled);
            if (gw > 0f)
            {
                UiDraw.Icon(s, inside, cx + kw, y + (UiTokens.Control - UiTokens.Icon) / 2f, UiTokens.Icon, line.Icon, UiRole.OnAccent, UiFlags.Middle);
            }
            UiDraw.TextMiddle(s, inside, cx + kw + gw, y, UiTokens.Control, words, UiStyle.Button, UiRole.OnAccent, line.Label, UiFlags.Label | UiFlags.Truncate);
            if (line.Disabled)
            {
                UiDraw.Dim(s, drawn, node);
            }
        }
    }
}
