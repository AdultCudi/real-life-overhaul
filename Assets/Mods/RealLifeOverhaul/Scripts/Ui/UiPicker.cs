using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>An entry of a picker: its glyph on a tile, its name and a line under it, its figure or state, and why it is greyed.</summary>
    public struct UiPickEntry
    {
        public UiIcon Icon;
        public string Name;        // "Hudson Street station"
        public string Detail;      // "4 min, line 2"; null for none
        public string Figure;      // "$2.75" or "Open"; null for none
        public bool Disabled;      // seen but not chosen: its reason says why
        public string Reason;      // "Closed until 06:00": a chip in place of its figure
        public bool Note;          // a line of the list that is no entry (the game's note rows): no tile, no target
        public string Face;        // a person's name: their painted face in place of the tile (a lawyer, a desk's occupant); null for none
        public float Hue;          // the face's ground
        public string Mark;        // a short code on the tile in place of the glyph (a subway line's "2"), as a bullet; null for the glyph
        public UiRole MarkTone;    // the bullet's colour, the line's own: a chart hue, with the code in the dark ink on it
    }

    /// <summary>
    /// What a picker shows: its title, two rows of context, its entries a page at a time, and whether the player may leave
    /// it. One model for the mod's TravelList and DeskList, the game's subway station list and ItemsList.
    /// </summary>
    public sealed class UiPickerModel
    {
        public const int MostContext = 2;

        public string Title;                               // "Take the subway"
        public UiIcon Icon = UiIcon.MapTrifold;
        public UiInfoRow[] Context = new UiInfoRow[0];
        public UiPickEntry[] Entries = new UiPickEntry[0];
        public int PerPage = 8;
        public int Page;                                   // the page shown, from 0
        public string PageWords = "{0} of {1}";            // the page counter, {0} this page and {1} the pages
        public bool Locked;                                // custody: no close and no Back, the player must pick
        public string LockWords;                           // the strip's chip while locked ("In custody"); null for none
        public string Said;                                // what the desk says (a list's lines under its title), wrapped under the strip; null for none
        public string Speaker;                             // who says it: their face beside the words; null for none
        public bool Numbered;                              // each entry led by its number on the page, which the number keys choose
        public string FootNote;                            // a line at the panel's foot ("You cannot leave until this is settled"); null for none
        public string EmptyTitle, EmptyBody;
        public UiIcon EmptyIcon = UiIcon.MapTrifold;      // what would fill the list, on the empty state's tile
        public string EmptyAction;                         // the way out of an empty list as a button ("Call a taxi"), where the keys start; null for none
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
        public bool Surfaces;                              // each entry on its own row surface, a step apart, as the world's lists set them
        public int Chosen = -1;                            // the entry shown chosen at rest (the one F takes), or -1 for none
        public string Choose;                              // the foot's main button on F, taking the chosen entry ("Choose"); null for none
        public string Second;                              // the foot's second button on P ("Read the file"); null for none
    }

    /// <summary>
    /// The picker: a slate panel centred over the running game for choosing one of a list (where to travel, which desk,
    /// which station, which item): its title strip (no close while locked), two rows of context on a band, the entries as
    /// rows (a glyph on its tile, the name over its line, the figure at the right, or the reason it is greyed as a chip),
    /// and, when the list is long, the pages in a bar fixed at its foot with the dimmed arrow where no page lies; a foot of
    /// its own may carry its note and its buttons (F Choose, P a second). Commands: <see cref="EntryCommand"/> (an entry's
    /// index), <see cref="PageCommand"/> (-1 or 1), <see cref="ChooseCommand"/>, <see cref="SecondCommand"/> and the
    /// strip's close (Back).
    /// Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiPicker
    {
        public const int EntryCommand = 230, PageCommand = 231, EmptyCommand = 232;   // the empty state's way out
        public const int ChooseCommand = 233, SecondCommand = 234;                     // the foot's buttons: the chosen entry, the list's second action
        public const float Width = 640f;          // px: two columns of words beside a tile, as the world's mockup sets the lists
        private const float SaidFace = 48f;       // px: the speaker's face beside their words
        private const int SaidLines = 4;          // what the desk says, at most, before it ends in an ellipsis
        private const float Number = UiTokens.S4; // px: an entry's number's column
        private const float Row = UiTokens.Row;   // px: an entry, its tile with room round it
        private const int NoteLines = 14;         // a note's words under its heading, at most, before they end in an ellipsis (a bill, a charge sheet)
        private const float Foot = UiTokens.Control + UiTokens.S2;   // px: the pages' bar
        private const byte ListRegion = 1, FootRegion = 2;

        public static int Pages(UiPickerModel m) => Math.Max(1, (m.Entries.Length + Math.Max(1, m.PerPage) - 1) / Math.Max(1, m.PerPage));

        /// <summary>The panel's height for its model: the strip, the context, a page of entries and the pages' bar when there are several.</summary>
        public static float Height(UiPickerModel m)
        {
            float h = UiFrame.Strip + SaidHeight(m);
            int context = Math.Min(UiPickerModel.MostContext, m.Context.Length);
            if (context > 0)
            {
                h += UiTokens.S2 * 2f + context * UiTokens.PairRow;
            }
            int shown = Shown(m);
            h += shown == 0 ? EmptyRoom(m) : UiTokens.S2 + PageHeight(m) + UiTokens.S2;
            if (Pages(m) > 1)
            {
                h += Foot;
            }
            if (Footed(m) && shown > 0)
            {
                h += Foot;
            }
            return UiDraw.Snap(h);
        }

        // A foot of its own under the entries: the note, the buttons, or both on one line.
        private static bool Footed(UiPickerModel m) => !string.IsNullOrEmpty(m.FootNote) || !string.IsNullOrEmpty(m.Choose) || !string.IsNullOrEmpty(m.Second);

        // What the desk says under the strip: the words wrapped beside the speaker's face, a step of room round them.
        private static float SaidHeight(UiPickerModel m) => string.IsNullOrEmpty(m.Said) ? 0f
            : UiDraw.Snap(Math.Max(string.IsNullOrEmpty(m.Speaker) ? 0f : SaidFace, UiTextRules.WrappedHeight(m.Said, UiStyle.Body, SaidWidth(m), SaidLines))) + UiTokens.S3 * 2f;

        private static float SaidWidth(UiPickerModel m) => Width - UiTokens.S4 * 2f - (string.IsNullOrEmpty(m.Speaker) ? 0f : SaidFace + UiTokens.S3);

        // The empty state's room: its glyph's tile and its words with a step of room round them, so nothing floats in a hole.
        private static float EmptyRoom(UiPickerModel m) =>
            UiDraw.EmptyStateHeight(Width - UiTokens.S6 * 2f, m.EmptyTitle, m.EmptyBody, m.EmptyAction) + UiTokens.S6 * 2f;

        // A tile says something only where the entries differ: a list of one kind of thing reads by its names.
        private static bool Differ(UiPickerModel m)
        {
            for (int i = 1; i < m.Entries.Length; i++)
            {
                if (!m.Entries[i].Note && (m.Entries[i].Icon != m.Entries[0].Icon || m.Entries[i].Mark != m.Entries[0].Mark || m.Entries[i].Face != null))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// An entry's height: a row for an entry, and for a note with words under its heading (a bill, a charge sheet, what
        /// a desk's records say) as many lines as its words take, up to <see cref="NoteLines"/>.
        /// </summary>
        public static float EntryHeight(ref UiPickEntry e)
        {
            if (!e.Note || string.IsNullOrEmpty(e.Detail))
            {
                return Row;
            }
            float w = NoteWidth;
            float h = UiTextRules.WrappedHeight(e.Detail, UiStyle.Small, w, NoteLines) + (string.IsNullOrEmpty(e.Name) ? 0f : UiTextRules.LineHeight(UiStyle.BodyStrong) + UiTokens.S1);
            return Math.Max(Row, UiDraw.Snap(h + UiTokens.S2 * 2f));
        }

        // A note's words: the entry's width less the room its words keep from its edges.
        private const float NoteWidth = Width - UiTokens.S2 * 2f - UiTokens.S4;

        // The entries of the page shown, one under another.
        private static float PageHeight(UiPickerModel m)
        {
            int first = Math.Max(0, Math.Min(m.Page, Pages(m) - 1)) * Math.Max(1, m.PerPage), shown = Shown(m);
            float h = 0f;
            for (int i = 0; i < shown; i++)
            {
                h += EntryHeight(ref m.Entries[first + i]);
            }
            return h;
        }

        /// <summary>The most a page shows (at most <paramref name="most"/>) so that every page of the entries stands in <paramref name="room"/> px.</summary>
        public static int PerPageFor(UiPickerModel m, int most, float room)
        {
            for (int per = Math.Max(1, most); per > 1; per--)
            {
                bool fits = true;
                for (int first = 0; first < m.Entries.Length && fits; first += per)
                {
                    float h = 0f;
                    for (int i = first; i < Math.Min(m.Entries.Length, first + per); i++)
                    {
                        h += EntryHeight(ref m.Entries[i]);
                    }
                    fits = h <= room;
                }
                if (fits)
                {
                    return per;
                }
            }
            return 1;
        }

        private static int Shown(UiPickerModel m)
        {
            int first = Math.Max(0, Math.Min(m.Page, Pages(m) - 1)) * Math.Max(1, m.PerPage);
            return Math.Max(0, Math.Min(m.PerPage, m.Entries.Length - first));
        }

        /// <summary>The picker as a screen of its own: centred over the world above the status bar, with the bar and its prompts.</summary>
        public static void Lay(UiScene s, UiPickerModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("picker", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            float h = Height(m);
            float x = UiDraw.SnapDown((frame.Width - Width) / 2f), y = UiDraw.SnapDown(Math.Max(UiTokens.S8, (frame.BandY - h) / 2f));
            Draw(s, UiAt.Screen, m, x, y, keys);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>The panel alone at a place (its top left); returns its height.</summary>
        public static float Draw(UiScene s, UiAt at, UiPickerModel m, float x, float y, IUiKeys keys = null)
        {
            float h = Height(m);
            int panel = UiDraw.Box(s, at, x, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt on = at.In(panel);
            float cy = UiDraw.TitleStrip(s, on, x, y, Width, m.Icon, m.Title, !m.Locked);
            if (m.Locked && !string.IsNullOrEmpty(m.LockWords))
            {
                // Held: the strip says so at its right, where its close would stand.
                float most = UiDraw.SnapDown(Width * 0.3f), ch = UiTokens.Badge + UiTokens.S1;
                float cw = Math.Min(most, UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(m.LockWords, UiStyle.SmallStrong) + UiTokens.S1));
                UiDraw.Chip(s, on, x + Width - UiTokens.S3 - cw, y + UiDraw.SnapDown((UiFrame.Strip - ch) / 2f), m.LockWords, UiRole.Warn, most);
            }
            float ix = x + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            float said = SaidHeight(m);
            if (said > 0f)
            {
                float sx = ix, sw = SaidWidth(m), wh = UiTextRules.WrappedHeight(m.Said, UiStyle.Body, sw, SaidLines);
                if (!string.IsNullOrEmpty(m.Speaker))
                {
                    UiDraw.Avatar(s, on, ix, cy + UiTokens.S3, SaidFace, m.Speaker, UiColour.HueOf(m.Speaker));
                    sx = ix + SaidFace + UiTokens.S3;
                }
                UiDraw.Text(s, on, sx, cy + (said - wh) / 2f, sw, UiStyle.Body, UiRole.Text2, m.Said, UiFlags.Wrap | UiFlags.Middle, SaidLines);
                cy += said;
            }
            int context = Math.Min(UiPickerModel.MostContext, m.Context.Length);
            if (context > 0)
            {
                float bh = UiTokens.S2 * 2f + context * UiTokens.PairRow;
                UiDraw.Band(s, on, x, cy, Width, bh);
                float ry = cy + UiTokens.S2;
                for (int i = 0; i < context; i++)
                {
                    ry += UiDraw.InfoRow(s, on, ix, ry, iw, m.Context[i].Label, m.Context[i].Value, false, -1, 0, m.Context[i].Ink == UiRole.None ? UiRole.Text : m.Context[i].Ink);
                }
                cy += bh;
            }
            int pages = Pages(m), page = Math.Max(0, Math.Min(m.Page, pages - 1));
            int first = page * Math.Max(1, m.PerPage), shown = Shown(m);
            if (shown == 0)
            {
                // With a way out it is a button inside the picker, so the keys start there and never on the bar below.
                UiDraw.EmptyState(s, on, x + UiTokens.S6, cy, Width - UiTokens.S6 * 2f, EmptyRoom(m), m.EmptyIcon, m.EmptyTitle, m.EmptyBody, m.EmptyAction, EmptyCommand,
                    keys ?? UiDefaultKeys.Keyboard);
                return h;
            }
            float ey = cy + UiTokens.S2;
            UiAt list = on.InRegion(ListRegion);
            bool tiles = Differ(m);
            int number = 0;
            for (int i = 0; i < shown; i++)
            {
                bool counted = m.Numbered && !m.Entries[first + i].Note;
                Entry(s, list, ref m.Entries[first + i], first + i, x + UiTokens.S2, ey, Width - UiTokens.S2 * 2f, tiles, counted ? ++number : 0, m.Surfaces, m.Chosen == first + i);
                ey += EntryHeight(ref m.Entries[first + i]);
            }
            float foot = y + h;
            if (Footed(m))
            {
                foot -= Foot;
                FootRow(s, on.InRegion(FootRegion), m, x, foot - (pages > 1 ? Foot : 0f), keys ?? UiDefaultKeys.Keyboard);
            }
            if (pages > 1)
            {
                Pager(s, on.InRegion(FootRegion), m, page, pages, x, y + h - Foot);
            }
            return h;
        }

        // The panel's foot over a hairline: the buttons at its right (P the second, F Choose the main), and in the room left
        // the line saying why the player cannot leave, in the quiet ink by its glyph.
        private static void FootRow(UiScene s, UiAt at, UiPickerModel m, float x, float y, IUiKeys keys)
        {
            UiDraw.Box(s, at, x, y, Width, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            float right = x + Width - UiTokens.S4, by = y + (Foot - UiTokens.Control) / 2f;
            if (!string.IsNullOrEmpty(m.Choose))
            {
                bool none = m.Chosen < 0 || m.Chosen >= m.Entries.Length || m.Entries[m.Chosen].Note || m.Entries[m.Chosen].Disabled;
                float bw = UiDraw.ButtonWidth(UiButtonKind.Primary, m.Choose, UiIcon.None, UiAction.Alt, keys);
                right -= bw;
                UiDraw.Button(s, at, right, by, bw, UiTokens.Control, UiButtonKind.Primary, m.Choose, UiIcon.None, UiAction.Alt, keys, ChooseCommand, 0, none);
                right -= UiTokens.S2;
            }
            if (!string.IsNullOrEmpty(m.Second))
            {
                float bw = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.Second, UiIcon.None, UiAction.Option, keys);
                right -= bw;
                UiDraw.Button(s, at, right, by, bw, UiTokens.Control, UiButtonKind.Secondary, m.Second, UiIcon.None, UiAction.Option, keys, SecondCommand, 0);
                right -= UiTokens.S2;
            }
            if (string.IsNullOrEmpty(m.FootNote))
            {
                return;
            }
            UiDraw.Icon(s, at, x + UiTokens.S4, y + (Foot - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Info, UiRole.Muted, UiFlags.Middle);
            float tx = x + UiTokens.S4 + UiTokens.Icon + UiTokens.S2;
            UiDraw.Text(s, at, tx, y + (Foot - UiTextRules.WrappedHeight(m.FootNote, UiStyle.Small, right - UiTokens.S2 - tx, 2)) / 2f, right - UiTokens.S2 - tx, UiStyle.Small,
                UiRole.Text2, m.FootNote, UiFlags.Wrap | UiFlags.Middle, 2);
        }

        // An entry: its glyph on a tile, its name over its line, its figure at the right, or the reason it is greyed as a chip.
        private static void Entry(UiScene s, UiAt at, ref UiPickEntry e, int index, float x, float y, float w, bool tiles, int number, bool surfaces, bool chosen)
        {
            float tile = UiTokens.IconTile;
            float Row = surfaces ? UiPicker.Row - UiTokens.S2 : UiPicker.Row;   // on surfaces, a grid step between the rows
            if (e.Note && !string.IsNullOrEmpty(e.Detail))
            {
                // A note with words under its heading: the heading, then every line of its words in the quiet ink.
                float ly = y + UiTokens.S2, tw = NoteWidth;
                if (!string.IsNullOrEmpty(e.Name))
                {
                    UiDraw.Text(s, at, x + UiTokens.S2, ly, tw, UiStyle.BodyStrong, UiRole.Text, e.Name, UiFlags.Truncate | UiFlags.Middle);
                    ly += UiTextRules.LineHeight(UiStyle.BodyStrong) + UiTokens.S1;
                }
                UiDraw.Text(s, at, x + UiTokens.S2, ly, tw, UiStyle.Small, UiRole.Text2, e.Detail, UiFlags.Wrap | UiFlags.Middle, NoteLines);
                return;
            }
            if (e.Note)
            {
                UiDraw.Text(s, at, x + UiTokens.S2, y + (Row - UiTextRules.WrappedHeight(e.Name, UiStyle.Body, w - UiTokens.S4, 2)) / 2f, w - UiTokens.S4, UiStyle.Body, UiRole.Text2, e.Name,
                    UiFlags.Wrap | UiFlags.Middle, 2);
                return;
            }
            int drawn = s.ItemCount, markFrom = -1, markTo = -1;
            UiRole ground = chosen && !e.Disabled ? UiRole.RowSel : surfaces ? UiRole.Row : UiRole.None;
            int face = UiDraw.Box(s, at, x, y, w, Row, ground, UiRole.None, UiTokens.RadiusLarge, chosen && !e.Disabled ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            float tx = x + UiTokens.S2, nx = x + UiTokens.S3;
            if (number > 0)
            {
                // Its number on the page in the cyan, which its number key chooses, before its tile.
                UiDraw.TextMiddle(s, inside, tx, y, Row, Number, UiStyle.BodyStrong, UiRole.AccentText, number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    UiFlags.Centre);
                tx += Number + UiTokens.S2;
                nx = tx + UiTokens.S1;
            }
            if (tiles && !string.IsNullOrEmpty(e.Face))
            {
                UiDraw.Portrait(s, inside, tx, y + (Row - tile) / 2f, tile, e.Face, e.Hue, UiRole.None);
                nx = tx + tile + UiTokens.S3;
            }
            else if (tiles)
            {
                int t = UiDraw.Tile(s, inside, tx, y + (Row - tile) / 2f, tile, UiRole.None, false);
                if (!string.IsNullOrEmpty(e.Mark))
                {
                    // A line's bullet, as the subway signs it: its colour on a small disc, its code in the dark ink.
                    float d = UiTokens.ControlSmall, dy = y + (Row - d) / 2f;
                    markFrom = s.ItemCount;
                    int disc = UiDraw.Circle(s, inside.In(t), tx + (tile - d) / 2f, dy, d, d, e.MarkTone == UiRole.None ? UiRole.Accent : e.MarkTone, UiFlags.Middle);
                    UiDraw.TextMiddle(s, inside.In(disc), tx + (tile - d) / 2f, dy, d, d, UiStyle.BodyStrong, UiRole.OnAccent, e.Mark, UiFlags.Centre | UiFlags.Truncate);
                    markTo = s.ItemCount;
                }
                else
                {
                    UiDraw.Icon(s, inside.In(t), tx + (tile - UiTokens.Icon) / 2f, y + (Row - UiTokens.Icon) / 2f, UiTokens.Icon, e.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                }
                nx = tx + tile + UiTokens.S3;
            }
            float right = x + w - UiTokens.S3;
            if (e.Disabled && !string.IsNullOrEmpty(e.Reason))
            {
                float most = Math.Min(UiDraw.SnapDown(w * 0.42f), 280f);
                float cw = UiDraw.Snap(UiTokens.S2 + UiTokens.StatusDot + UiTokens.S2 + UiTextRules.Width(e.Reason, UiStyle.SmallStrong) + UiTokens.S1 + UiTokens.S2);
                cw = Math.Min(cw, most);
                float chipH = UiTokens.Badge + UiTokens.S1;
                UiDraw.Chip(s, inside, right - cw, y + (Row - chipH) / 2f, e.Reason, UiRole.Warn, most);
                right -= cw + UiTokens.S3;
            }
            else if (!string.IsNullOrEmpty(e.Figure))
            {
                float fw = Math.Min(UiTextRules.Width(e.Figure, UiStyle.BodyStrong) + UiTokens.S1, w * 0.3f);
                UiDraw.TextMiddle(s, inside, right - fw, y, Row, fw, UiStyle.BodyStrong, UiRole.Text, e.Figure, UiFlags.Right | UiFlags.Truncate);
                right -= fw + UiTokens.S3;
            }
            float nw = right - nx;
            bool detail = !string.IsNullOrEmpty(e.Detail);
            float nh = UiTextRules.LineHeight(UiStyle.Title), dh = detail ? UiTextRules.LineHeight(UiStyle.Small) : 0f;
            float block = nh + (detail ? UiTokens.S1 + dh : 0f), ny = y + (Row - block) / 2f;
            UiDraw.Text(s, inside, nx, ny, nw, UiStyle.Title, UiRole.Text, e.Name, UiFlags.Truncate | UiFlags.Middle);
            if (detail)
            {
                UiDraw.Text(s, inside, nx, ny + nh + UiTokens.S1, nw, UiStyle.Small, ground == UiRole.RowSel ? UiRole.Text : UiRole.Text2, e.Detail, UiFlags.Truncate | UiFlags.Middle);
            }
            UiAction key = number > 0 && number <= 9 ? (UiAction)((int)UiAction.Pick1 + number - 1) : UiAction.Count;
            int node = UiDraw.Node(s, at, x, y, w, Row, EntryCommand, index, face, e.Disabled, -1, 0, key);
            if (e.Disabled)
            {
                // Greyed as the game greys it, the reason's chip kept whole so it still reads.
                for (int i = drawn; i < s.ItemCount; i++)
                {
                    bool mark = i >= markFrom && i < markTo;   // the line's bullet stays readable: which line is closed is the point
                    bool surface = surfaces && i == face;   // the row's surface keeps its shade: the chip on it still reads
                    if (!mark && !surface && (s.Items[i].Kind != UiKind.Text || s.Items[i].Style != UiStyle.SmallStrong))
                    {
                        s.Items[i].Alpha = UiDraw.DimAlpha;
                    }
                    s.Items[i].Flags |= UiFlags.Disabled;
                }
                s.Nodes[node].Disabled = true;
            }
        }

        // The pages' bar fixed at the panel's foot: the arrows either side of the counter, the one with no page that way dimmed.
        private static void Pager(UiScene s, UiAt at, UiPickerModel m, int page, int pages, float x, float y)
        {
            UiDraw.Box(s, at, x, y, Width, UiTokens.Hairline, UiRole.Rule, UiRole.None, 0f, UiFlags.Decor);
            float b = UiTokens.ControlSmall, by = y + (Foot - b) / 2f;
            float cx = x + Width / 2f;
            string words = string.Format(System.Globalization.CultureInfo.InvariantCulture, m.PageWords ?? "{0} of {1}", page + 1, pages);
            float tw = UiDraw.Snap(UiTextRules.Width(words, UiStyle.Small) + UiTokens.S1);
            int drawn = s.ItemCount;
            int back = UiDraw.IconButton(s, at, UiDraw.SnapDown(cx - tw / 2f - UiTokens.S3 - b), by, b, UiIcon.Back, UiRole.Text, PageCommand, -1, false, UiAction.Count, false, true);
            if (page == 0)
            {
                UiDraw.Dim(s, drawn, back);
            }
            UiDraw.TextMiddle(s, at, UiDraw.SnapDown(cx - tw / 2f), y, Foot, tw, UiStyle.Small, UiRole.Text2, words, UiFlags.Centre);
            drawn = s.ItemCount;
            int next = UiDraw.IconButton(s, at, UiDraw.Snap(cx + tw / 2f + UiTokens.S3), by, b, UiIcon.Next, UiRole.Text, PageCommand, 1, false, UiAction.Count, false, true);
            if (page >= pages - 1)
            {
                UiDraw.Dim(s, drawn, next);
            }
        }
    }
}
