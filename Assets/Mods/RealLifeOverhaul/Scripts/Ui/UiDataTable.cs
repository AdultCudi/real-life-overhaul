using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a big table's cell shows: words, a figure, money, a date, a status chip, a meter or a person.</summary>
    public enum UiCellKind : byte
    {
        Text = 0,     // words at the left
        Figure,       // a count, tabular and at the right; graded by Values when the column is graded, or in a Tones text tone (OkText, WarnText, DangerText)
        Money,        // the game's own money words, at the right
        Date,         // the game's own date words, at the right
        Chip,         // a soft status chip in its tone (Tones)
        Meter,        // a thin bar filled to Values, graded red to green, its figure beside it
        Person,       // a face in its mood ring, the name and the role under it (Subs)
        Tile,         // what the row is, as an icon on a slate tile in its own colour (Values: the icon, Tones: its tint), the name and a line under it (Sub)
        Stepper,      // an amount to change on the row: minus, the typed figure and plus (Values: CanLess and CanMore, 0 while held), each a target of its own;
                      // a Warn or Danger tone puts the warning's glyph before it where the column has MarkRoom
        Action,       // a thing to do with the row, as an icon button at its right (Values: the icon, Tones: its tint; Cells: its words, unseen): set the destination
        Trend,        // a figure at the right with its direction's arrow (Values: 1, -1 or 0; Tones: Danger where a rise is bad) (UiDataCells)
        Steps,        // a short ladder as a bar of segments (Values: the steps reached, the column's Steps: how many, the row's Share)
        Spark,        // a sparkline of the row's Line, oldest first
        Stars         // a rating as five stars by halves (Values: 0 to 10 halves), as Football Manager rates; Cells: its words, unseen, for the sort and the reader
    }

    /// <summary>A column of a big table: what its cells are, how wide it stands and whether its head sorts it.</summary>
    public struct UiDataColumn
    {
        public string Title;
        public UiCellKind Kind;
        public float Width;      // px on the grid, or 0 to share the room the fixed columns leave
        public bool Sortable;    // its head is a target that sorts by it
        public bool OnChosen;    // a stepper on the chosen row alone (Selected): the other rows show its figure and are targets that choose them
        public bool Graded;      // a figure in its grade's colour (Values), as a management game colours a skill
        public int Steps;        // a steps column's ladder: how many pips
    }

    /// <summary>
    /// A row of a big table, by column: the words of each cell, a value of 0 to 100 for a meter or a graded figure, a tone
    /// for a chip, and the person the row is about (a role under the name, a hue for the face, the ring of their mood).
    /// </summary>
    public struct UiDataRow
    {
        public string[] Cells;
        public int[] Values;
        public UiRole[] Tones;
        public string Sub;       // a person cell's second line: their role
        public string Face;      // the key a person cell's face is drawn by where the name alone would tell two people apart; null: the name
        public string[] Subs;    // a cell's own second line where a row holds a tile and a person (a machine and who works it); null: Sub
        public float Hue;
        public UiRole Ring;      // their mood, as every portrait's ring says it
        public bool Picked;      // picked for a mass action
        public float Share;      // a steps cell's way through the step after those reached, 0 to 1
        public string[] Pictures;   // by column, a tile cell's picture of the game's (a good, a machine) by its UiPictures key, over its glyph; null: the glyph
        public float[] Line;        // a spark column's values, oldest first, or null
    }

    /// <summary>
    /// A table that holds thousands of rows (the game's staff lists, a market's goods): its columns from a spec, the
    /// order it is sorted in, a filter row of choices and a search, a row that drives a detail, rows picked for a mass
    /// action, totals at the foot and the words for an empty table. Plain data; the screen sorts and filters, the block
    /// only draws what it is given.
    /// </summary>
    public sealed class UiDataTableModel
    {
        public UiDataColumn[] Columns = new UiDataColumn[0];
        public UiDataRow[] Rows = new UiDataRow[0];
        public int RowCount;
        public int SortColumn = -1;
        public bool SortDown;                       // the highest first (a wage, a skill), or the last first (a date)
        public int Selected = -1;                   // the row the detail beside shows
        public string[] Filters = new string[0];    // each filter's value as a choice field ("All roles")
        public string SearchPlaceholder;
        public string Query;
        public string Count;                        // "40 people", "3 of 40"
        public bool Picking;                        // a press picks a row for a mass action instead of opening it
        public string PickWords;                    // the button that starts picking ("Pick several")
        public string PickedWords;                  // "2 picked"
        public UiActionModel[] Mass = new UiActionModel[0];   // the mass actions while picking, the last the main one
        public string DoneWords;                    // ends picking
        public string TotalsLabel;
        public string[] Totals;                     // by column, the foot's figures, or null for no foot
        public float Offset;                        // px scrolled; the block writes back the offset it took
        public int Reveal = -1;                     // a row scrolled into view as it is laid out
        public string EmptyTitle;
        public string EmptyBody;
        public UiIcon EmptyIcon = UiIcon.People;   // what would fill it, over its words
        public string EmptyAction;                  // the button that fills it again ("Clear search"), or null
        public string MassFieldTitle;               // while a mass action asks one thing more (the business to assign to): what it asks
        public string MassField;                    // that answer's value as a choice field; null while no mass action asks
        public UiActionModel[] MassFieldActions = new UiActionModel[0];   // its way back and its confirm, the confirm last
        public float MassFieldX, MassFieldW;        // written back: where the field stood, so the page opens its list under it
    }

    /// <summary>
    /// The big table, a block of an app window's page (as <see cref="UiAppWindow.Table"/> is, for the tables a game's
    /// staff lists and markets need): a tool row of filter choices, a search and how many it shows, or while rows are
    /// picked, how many and the mass actions; a head of column titles where a sortable one is a target and the sorted one
    /// carries its order's arrow; rows of words, figures, money, dates, chips, graded meters and people with their faces,
    /// only those in view laid out, so thousands cost what a screenful does; the row the detail shows in the cyan at a
    /// fifth; and the totals at the foot. An empty table keeps its head and says in its room what would fill it. Figures
    /// stand tabular at the right. The screen sorts and filters; the block draws what it is given. Commands from
    /// <see cref="First"/>: <see cref="Head"/>, <see cref="Filter"/>, <see cref="Search"/>, <see cref="Row"/>,
    /// <see cref="Pick"/>, <see cref="Mass"/> and <see cref="Done"/> added to it, and while a mass action asks one thing
    /// more (the game's panel of choices: the business to assign to), <see cref="MassChoice"/> and <see cref="MassAnswer"/>.
    /// Game-free. Cost: a layout a change, the rows in view only.
    /// </summary>
    public static class UiDataTable
    {
        public const int First = 110;
        public const int Head = 0, Filter = 1, Search = 2, Row = 3, Pick = 4, Mass = 5, Done = 6, Clear = 7, MassChoice = 8, MassAnswer = 9;
        // A stepper cell's and a row action's commands stand past the other blocks' ranges (from 170), so a table's never
        // meets a chart's or a catalogue's on one page.
        public const int Less = 60, Type = 61, More = 62, Act = 63;
        public const int CanLess = 1, CanMore = 2;   // a stepper cell's Values: which way it may step now
        private const float LeastShare = 128f;     // px: the least a shared column keeps when a longer translation widens the fixed ones
        public const float StepperWidth = 176f;  // px: a stepper cell's widest, room for a price between its buttons
        public const float MarkRoom = 40f;       // px: and the room before it for its warning's glyph, when its column gives it
        /// <summary>A person cell whose value is this draws its words with no face: the row has nobody in it.</summary>
        public const int NoFace = -2;

        /// <summary>A person cell whose value is <see cref="GlyphFace"/> of an icon draws that glyph on a tile where the face would stand, and its words after it at the same
        /// left as a face's words: a row of nobody in particular (the trade, an empty unit) keeps its edge with the rows that have a person.</summary>
        public const int GlyphFaceBase = -100;
        public static int GlyphFace(UiIcon icon) => GlyphFaceBase - (int)icon;

        private const float PersonRow = 48f;     // px: a row with a face and two lines
        private const float PlainRow = 40f;      // px: a row of one line, as the app window's table
        private const float PickBox = 20f;       // px: a row's pick box while picking
        private const float LeastFilter = 160f;  // px: a filter choice's narrowest
        private const float MostFilter = 240f;   // px: and widest
        private const float MostSearch = 320f;   // px: a search field's widest

        /// <summary>The height a row takes in this table: taller when a column holds people.</summary>
        public static float RowHeight(UiDataTableModel m)
        {
            if (Amounts(m))
            {
                return UiTokens.Control + UiTokens.S2;   // a stepper's field is a control's height: the rows keep a gap between the fields
            }
            for (int c = 0; c < m.Columns.Length; c++)
            {
                if (m.Columns[c].Kind == UiCellKind.Person || m.Columns[c].Kind == UiCellKind.Tile)
                {
                    return PersonRow;
                }
            }
            return PlainRow;
        }

        /// <summary>
        /// True when a row holds amounts to change: its steppers are its targets, so the row itself is none. A stepper on the
        /// chosen row alone (<see cref="UiDataColumn.OnChosen"/>) leaves every row its own target.
        /// </summary>
        public static bool Steps(UiDataTableModel m)
        {
            for (int c = 0; c < m.Columns.Length; c++)
            {
                if (m.Columns[c].Kind == UiCellKind.Stepper && !m.Columns[c].OnChosen)
                {
                    return true;
                }
            }
            return false;
        }

        // Whether a row may hold a stepper, every row's or the chosen one's: the rows then stand a control's height and a gap.
        private static bool Amounts(UiDataTableModel m)
        {
            for (int c = 0; c < m.Columns.Length; c++)
            {
                if (m.Columns[c].Kind == UiCellKind.Stepper)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The height a table's own parts take besides its rows: the tool row, the head and the foot.</summary>
        public static float Chrome(UiDataTableModel m) => (HasTools(m) ? UiTokens.Control + UiTokens.S3 : 0f) + UiTokens.Control + UiTokens.S1 + (m.Totals != null ? Foot : 0f);

        /// <summary>True when the table has a tool row to show: a filter, a search, picking or the button that starts it. A table of none starts at its head.</summary>
        public static bool HasTools(UiDataTableModel m) => m.Picking || m.Filters.Length > 0 || !string.IsNullOrEmpty(m.SearchPlaceholder) || !string.IsNullOrEmpty(m.PickWords);

        // A person's or a tile's column head stands over the names, as its rows' words start after the face or the tile.
        private static float FaceLead(UiCellKind kind) => kind == UiCellKind.Person ? UiTokens.S1 + UiTokens.AvatarSmall + UiTokens.S3
            : kind == UiCellKind.Tile ? UiTokens.ControlSmall + UiTokens.S3 : 0f;

        // The face's lead under a head, or none where a long translation needs the whole column to fit on two lines.
        private static float HeadLead(UiDataColumn col, float room)
        {
            float lead = FaceLead(col.Kind);
            return lead > 0f && UiTextRules.Lines(col.Title, UiStyle.SectionHeader, room - lead, 0, out float _) > 2 ? 0f : lead;
        }

        // The head's height: a compact row, or two lines' room when a shared column's title takes two in this language.
        private static float HeadHeight(UiDataTableModel m, float[] cw)
        {
            for (int c = 0; c < m.Columns.Length; c++)
            {
                float arrow = c == m.SortColumn ? UiTokens.IconSmall + UiTokens.S1 : 0f;
                if (UiTextRules.Width(m.Columns[c].Title, UiStyle.SectionHeader) + UiTokens.S1 > cw[c] - arrow - HeadLead(m.Columns[c], cw[c] - arrow))
                {
                    return UiTokens.Control;
                }
            }
            return UiTokens.ControlSmall;
        }

        private const float Foot = UiTokens.ControlSmall + UiTokens.S1;   // px: the totals' row under its rule

        /// <summary>
        /// Lays the table out in a box, its rows filling the room between its head and its foot. Returns the list of its
        /// rows, or -1 when it is empty; the offset it took is written back to the model.
        /// </summary>
        public static short Lay(UiScene s, UiAt at, UiDataTableModel m, float x, float y, float w, float h, IUiKeys keys, int command = First)
        {
            float ty = HasTools(m) ? Tools(s, at, m, x, y, w, keys, command) : y;
            float rowH = RowHeight(m);
            // A table that can pick keeps its pick boxes' room either way, so its columns stand still when picking starts.
            float lead = !string.IsNullOrEmpty(m.PickWords) ? PickBox + UiTokens.S3 : 0f;
            float[] cx = Columns(m.Columns, x + UiTokens.S2 + lead, w - UiTokens.S4 - lead, out float[] cw);
            float headH = HeadHeight(m, cw);
            HeadRow(s, at, m, cx, cw, ty, headH, command);
            ty += headH;
            UiDraw.Box(s, at, x, ty, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            ty += UiTokens.S1;   // the rule in a grid step of its own, so the rows start on the grid
            float footH = m.Totals != null ? Foot : 0f;
            float room = UiDraw.SnapDown(y + h - footH - ty);
            float viewH = m.RowCount > 0 ? Math.Max(rowH, Math.Min(m.RowCount, (float)Math.Floor(room / rowH)) * rowH) : room;   // whole rows, the totals right under the last
            short list = -1;
            if (m.RowCount == 0)
            {
                UiDraw.EmptyState(s, at, x, ty, w, viewH, m.EmptyIcon, m.EmptyTitle, m.EmptyBody, m.EmptyAction, command + Clear, keys);
                m.Offset = 0f;
            }
            else
            {
                list = Rows(s, at, m, x, ty, w, viewH, rowH, cx, cw, command);
            }
            if (m.Totals != null)
            {
                Totals(s, at, m, x, ty + viewH, w, cx, cw);
            }
            return list;
        }

        // The tool row: the filters, the search and the count, with the button that starts picking at the right; or while
        // picking, how many are picked, the mass actions and Done. One row either way, so nothing under it moves.
        private static float Tools(UiScene s, UiAt at, UiDataTableModel m, float x, float y, float w, IUiKeys keys, int command)
        {
            if (!HasTools(m))
            {
                return y;
            }
            float h = UiTokens.Control;
            if (m.Picking)
            {
                // How many are picked and Done at the left, the mass actions at the right with the main one last.
                float lx = x;
                if (!string.IsNullOrEmpty(m.PickedWords))
                {
                    float pw = UiDraw.Snap(UiTextRules.Width(m.PickedWords, UiStyle.BodyStrong) + UiTokens.S1);
                    UiDraw.Icon(s, at, x, y + (h - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Check, UiRole.None, UiFlags.Middle, true);
                    UiDraw.TextMiddle(s, at, x + UiTokens.Icon + UiTokens.S2, y, h, pw, UiStyle.BodyStrong, UiRole.Text, m.PickedWords, UiFlags.Truncate);
                    lx = x + UiTokens.Icon + UiTokens.S2 + pw + UiTokens.S4;
                }
                if (!string.IsNullOrEmpty(m.DoneWords))
                {
                    float dw = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.DoneWords, UiIcon.None, UiAction.Count, keys);
                    UiDraw.Button(s, at, lx, y, dw, h, UiButtonKind.Secondary, m.DoneWords, UiIcon.None, UiAction.Count, keys, command + Done, 0);
                    lx += dw + UiTokens.S6;
                }
                if (m.MassField != null)
                {
                    MassAsks(s, at, m, lx, x + w, y, keys, command);
                    return y + h + UiTokens.S3;
                }
                float right = x + w;
                for (int i = m.Mass.Length - 1; i >= 0; i--)
                {
                    ref UiActionModel a = ref m.Mass[i];
                    float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                    if (right - bw < lx)
                    {
                        break;   // a narrow window keeps the main actions, the last ones
                    }
                    right -= bw;
                    UiDraw.Button(s, at, right, y, bw, h, a.Kind, a.Label, a.Icon, a.Key, keys, command + Mass, i, a.Disabled);
                    right -= UiTokens.S3;
                }
                return y + h + UiTokens.S3;
            }
            float fx = x;
            for (int i = 0; i < m.Filters.Length; i++)
            {
                float fw = FilterWidth(m.Filters[i]);
                UiDraw.Choice(s, at, fx, y, fw, m.Filters[i], command + Filter, i, false);
                fx += fw + UiTokens.S2;
            }
            float end = x + w;
            if (!string.IsNullOrEmpty(m.PickWords) && m.RowCount > 0)
            {
                float bw = UiDraw.ButtonWidth(UiButtonKind.Secondary, m.PickWords, UiIcon.Check, UiAction.Count, keys);
                end -= bw;
                UiDraw.Button(s, at, end, y, bw, h, UiButtonKind.Secondary, m.PickWords, UiIcon.Check, UiAction.Count, keys, command + Pick, 0);
                end -= UiTokens.S4;
            }
            if (!string.IsNullOrEmpty(m.SearchPlaceholder))
            {
                float sx = fx + (m.Filters.Length > 0 ? UiTokens.S2 : 0f);
                float sw = UiDraw.SnapDown(Math.Min(MostSearch, end - sx));
                if (sw >= LeastFilter)
                {
                    UiDraw.Search(s, at, sx, y, sw, m.SearchPlaceholder, m.Query, m.Count, command + Search);
                }
            }
            return y + h + UiTokens.S3;
        }

        // A mass action that asks one thing more before its question (the game's panel of choices): what it asks, the choice
        // and its answers on the same tool row, the confirm last, so nothing under the row moves while it asks.
        private static void MassAsks(UiScene s, UiAt at, UiDataTableModel m, float left, float right, float y, IUiKeys keys, int command)
        {
            float h = UiTokens.Control;
            for (int i = m.MassFieldActions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.MassFieldActions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                right -= bw;
                UiDraw.Button(s, at, right, y, bw, h, a.Kind, a.Label, a.Icon, a.Key, keys, command + MassAnswer, i, a.Disabled);
                right -= UiTokens.S3;
            }
            float fw = UiDraw.SnapDown(Math.Max(LeastFilter, Math.Min(MostSearch, right - UiTokens.S3 - left - LeastFilter)));
            float fx = right - UiTokens.S3 - fw;
            m.MassFieldX = fx;
            m.MassFieldW = fw;
            if (!string.IsNullOrEmpty(m.MassFieldTitle) && fx - UiTokens.S4 - left >= UiTokens.S16)
            {
                // A long translation of what it asks takes a second line in the row rather than being cut.
                float tw = fx - UiTokens.S4 - left, th = UiTextRules.WrappedHeight(m.MassFieldTitle, UiStyle.Body, tw, 2);
                UiDraw.Text(s, at, left, y + (h - th) / 2f, tw, UiStyle.Body, UiRole.Text2, m.MassFieldTitle, UiFlags.Label | UiFlags.Wrap | UiFlags.Middle | UiFlags.Right, 2);
            }
            UiDraw.Choice(s, at, fx, y, fw, m.MassField, command + MassChoice, 0, false);
        }

        /// <summary>The left edge of a filter's choice on the tool row, so the page opens that filter's list under it.</summary>
        public static float FilterLeft(UiDataTableModel m, int index, float x)
        {
            for (int i = 0; i < index && i < m.Filters.Length; i++)
            {
                x += FilterWidth(m.Filters[i]) + UiTokens.S2;
            }
            return x;
        }

        /// <summary>A filter's width on the tool row for the value it shows.</summary>
        public static float FilterWidthOf(string value) => FilterWidth(value);

        private static float FilterWidth(string value) =>
            Math.Max(LeastFilter, Math.Min(MostFilter, UiDraw.Snap(UiTokens.S4 + UiTextRules.Width(value, UiStyle.Body) + UiTokens.S1 + UiTokens.S2 + UiTokens.Icon + UiTokens.S3)));

        // The columns' lefts and widths: the fixed ones as given, the rest sharing what they leave, a gutter between each.
        private static float[] Columns(UiDataColumn[] columns, float x, float w, out float[] widths)
        {
            int n = columns.Length;
            var lefts = new float[n];
            widths = new float[n];
            float fixedW = 0f, baseW = 0f, grow = 0f;
            int shares = 0;
            for (int c = 0; c < n; c++)
            {
                // A fixed column is never narrower than its head's words with the sort's arrow: a longer translation widens it.
                widths[c] = columns[c].Width > 0f ? Math.Max(columns[c].Width, UiDraw.Snap(UiTextRules.Width(columns[c].Title, UiStyle.SectionHeader) + UiTokens.S1
                                                                                                + (columns[c].Sortable ? UiTokens.IconSmall + UiTokens.S1 : 0f))) : 0f;
                baseW += columns[c].Width > 0f ? columns[c].Width : 0f;
                grow += widths[c] - (columns[c].Width > 0f ? columns[c].Width : 0f);
                fixedW += widths[c];
                shares += columns[c].Width <= 0f ? 1 : 0;
            }
            // The widening may take only what the shared columns can spare above their least, so the row never runs past its width: what is
            // over the room stays a second line of the head (or is cut) rather than pushing the last column out.
            float room = w - baseW - UiTokens.S4 * (n - 1) - shares * LeastShare;
            if (grow > 0f && grow > room)
            {
                float keep = Math.Max(0f, room) / grow;
                fixedW = baseW;
                for (int c = 0; c < n; c++)
                {
                    if (columns[c].Width > 0f)
                    {
                        widths[c] = columns[c].Width + UiDraw.SnapDown((widths[c] - columns[c].Width) * keep);
                        fixedW += widths[c] - columns[c].Width;
                    }
                }
            }
            float spare = Math.Max(UiTokens.S16, w - fixedW - UiTokens.S4 * (n - 1));
            float share = UiDraw.SnapDown(spare / Math.Max(1, shares));
            float cx = x;
            for (int c = 0; c < n; c++)
            {
                widths[c] = columns[c].Width > 0f ? widths[c] : share;
                lefts[c] = cx;
                cx += widths[c] + UiTokens.S4;
            }
            return lefts;
        }

        private static bool RightAligned(UiCellKind kind) => kind == UiCellKind.Figure || kind == UiCellKind.Money || kind == UiCellKind.Date || kind == UiCellKind.Meter
                                                              || kind == UiCellKind.Chip || kind == UiCellKind.Stepper || kind == UiCellKind.Trend || kind == UiCellKind.Stars;

        // The head: each title in capitals, a sortable one a target, the sorted one in the words' colour with its order's
        // arrow beside it. Figures' titles stand at the right, over their figures.
        private static void HeadRow(UiScene s, UiAt at, UiDataTableModel m, float[] cx, float[] cw, float y, float h, int command)
        {
            for (int c = 0; c < m.Columns.Length; c++)
            {
                ref UiDataColumn col = ref m.Columns[c];
                bool right = RightAligned(col.Kind), sorted = c == m.SortColumn;
                float arrow = sorted ? UiTokens.IconSmall + UiTokens.S1 : 0f, lead = HeadLead(col, cw[c] - arrow);
                float words = UiTextRules.Width(col.Title, UiStyle.SectionHeader) + UiTokens.S1;
                bool two = words > cw[c] - arrow - lead;   // a long translation takes a second line rather than being cut
                float tw = Math.Min(cw[c] - arrow - lead, words);
                int face = col.Sortable ? UiDraw.Box(s, at, cx[c] - UiTokens.S1, y, cw[c] + UiTokens.S2, h, UiRole.None, UiRole.None, UiTokens.RadiusMedium) : -1;
                UiAt on = face >= 0 ? at.In(face) : at;
                float wordsX = right ? cx[c] + cw[c] - tw : cx[c] + lead;
                if (two)
                {
                    float lh = UiTextRules.WrappedHeight(col.Title, UiStyle.SectionHeader, tw, 2);
                    UiDraw.Text(s, on, wordsX, y + (h - lh) / 2f, tw, UiStyle.SectionHeader, sorted ? UiRole.Text : UiRole.Muted, col.Title,
                        UiFlags.Label | UiFlags.Wrap | UiFlags.Middle | (right ? UiFlags.Right : UiFlags.None), 2);
                }
                else
                {
                    UiDraw.TextMiddle(s, on, wordsX, y, h, tw, UiStyle.SectionHeader, sorted ? UiRole.Text : UiRole.Muted, col.Title,
                        UiFlags.Label | UiFlags.Truncate | (right ? UiFlags.Right : UiFlags.None));
                }
                if (sorted)
                {
                    float ax = right ? wordsX - arrow : wordsX + tw;
                    UiDraw.Icon(s, on, ax, y + (h - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, m.SortDown ? UiIcon.Down : UiIcon.Up,
                        UiRole.AccentText, UiFlags.Middle | UiFlags.Share);
                }
                if (face >= 0)
                {
                    UiDraw.Node(s, at, cx[c] - UiTokens.S1, y, cw[c] + UiTokens.S2, h, command + Head, c, face);
                }
            }
        }

        // The rows in view, in a region that scrolls inside its frame, each a row of one list.
        private static short Rows(UiScene s, UiAt at, UiDataTableModel m, float x, float y, float w, float viewH, float rowH, float[] cx, float[] cw, int command)
        {
            int rows = m.RowCount;
            float offset = m.Offset;
            if (m.Reveal >= 0 && m.Reveal < rows)
            {
                offset = Math.Min(offset, m.Reveal * rowH);
                offset = Math.Max(offset, (m.Reveal + 1) * rowH - viewH);
            }
            offset = Math.Max(0f, Math.Min((float)Math.Round(offset / rowH) * rowH, rows * rowH - viewH));   // by whole rows, never a sliver of one
            m.Offset = offset;
            m.Reveal = -1;
            short clip = s.AddClip(x, y, w, viewH, offset);
            int first = Math.Max(0, (int)(offset / rowH));
            int last = Math.Min(rows - 1, (int)((offset + viewH - 0.5f) / rowH));
            short list = s.AddList(rows, first, Math.Max(0, last - first + 1), clip, m.Selected);
            s.Clips[clip].ContentHeight = rows * rowH;
            var inClip = new UiAt { Parent = -1, Clip = clip, Layer = at.Layer, Order = -1, Region = at.Region };
            bool steps = Steps(m);
            for (int r = first; r <= last; r++)
            {
                ref UiDataRow row = ref m.Rows[r];
                bool sel = r == m.Selected && !m.Picking;   // while picking, the picked boxes are the mark: one selection model on screen
                float ry = r * rowH;
                UiAt arriving = inClip.Arriving(r - first);
                // On an overlay's layer a row stands on its sheet's own colour, as a choice list's rows do: its words on a card.
                UiRole fill = sel ? UiRole.RowSel : at.Layer > 0 ? UiRole.Panel : UiRole.None;
                int face = UiDraw.Box(s, arriving, 0f, ry, w, rowH, fill, UiRole.None, UiTokens.RadiusLarge, sel ? UiFlags.Selected : UiFlags.None);
                UiAt inside = arriving.In(face);
                if (m.Picking)
                {
                    float px = UiTokens.S2, py = ry + (rowH - PickBox) / 2f;
                    int box = UiDraw.Box(s, inside, px, py, PickBox, PickBox, row.Picked ? UiRole.Accent : UiRole.Field, row.Picked ? UiRole.None : UiRole.PanelEdge,
                        UiTokens.RadiusSmall, UiFlags.Middle | (row.Picked ? UiFlags.Selected : UiFlags.None));
                    if (row.Picked)
                    {
                        UiDraw.Icon(s, inside.In(box), px + UiTokens.S1 / 2f, py + UiTokens.S1 / 2f, PickBox - UiTokens.S1, UiIcon.Check, UiRole.OnAccent, UiFlags.Middle | UiFlags.Centre);
                    }
                }
                int stop = -1;
                for (int c = 0; c < m.Columns.Length; c++)
                {
                    int node = Cell(s, inside, m, ref row, c, cx[c] - x, ry, cw[c], rowH, sel, command, r);
                    stop = stop < 0 ? node : stop;
                }
                int rowNode = -1;
                if (!steps)
                {
                    rowNode = UiDraw.Node(s, inClip, 0f, ry, w, rowH, command + Row, r, face, false, list, r);
                }
                else if (stop >= 0)
                {
                    // A row of amounts is stepped through by its typed figure, so up and down walk the list and scroll it.
                    s.Nodes[stop].List = list;
                    s.Nodes[stop].Index = r;
                }
                for (int c = 0; c < m.Columns.Length; c++)
                {
                    if (m.Columns[c].Kind == UiCellKind.Action)
                    {
                        // Laid after the row's own target, so the button over it takes its press.
                        int button = ActButton(s, inside, ref row, c, cx[c] - x, ry, cw[c], rowH, command, r, list);
                        if (button >= 0 && rowNode >= 0)
                        {
                            // The pad goes from the row to its button and back by links of their own: the nearest-target search never leaves a list for its own row's button.
                            s.Nodes[rowNode].Right = button;
                            s.Nodes[button].Left = rowNode;
                        }
                    }
                    else if (sel && m.Columns[c].Kind == UiCellKind.Stepper && m.Columns[c].OnChosen)
                    {
                        ChosenStepper(s, inside, ref row, c, cx[c] - x, ry, cw[c], rowH, command, r);   // over the row's target too
                    }
                }
            }
            UiDraw.ScrollThumb(s, at, x + w - UiTokens.S1, y, viewH, offset, rows * rowH);
            return list;
        }

        // The chosen row's stepper where its figure stands on the other rows: minus, the typed figure and plus, each a target
        // over the row's own, right from the row and left back as a row's action is.
        private static void ChosenStepper(UiScene s, UiAt at, ref UiDataRow row, int c, float cx, float ry, float cw, float rowH, int command, int r)
        {
            int value = row.Values != null && c < row.Values.Length ? row.Values[c] : -1;
            string words = row.Cells != null && c < row.Cells.Length ? row.Cells[c] : null;
            float sw = Math.Min(cw, StepperWidth);
            UiDraw.Stepper(s, at, cx + cw - sw, ry + (rowH - UiTokens.Control) / 2f, sw, words, (value & CanLess) != 0, (value & CanMore) != 0,
                command + Less, command + Type, command + More, r, value <= 0);
        }

        // A row's action at the right of its cell: its icon on a slate tile in its colour, a target of its own.
        private static int ActButton(UiScene s, UiAt at, ref UiDataRow row, int c, float cx, float ry, float cw, float rowH, int command, int r, short list)
        {
            int value = row.Values != null && c < row.Values.Length ? row.Values[c] : 0;
            if (value <= 0)
            {
                return -1;
            }
            UiRole tint = row.Tones != null && c < row.Tones.Length ? row.Tones[c] : UiRole.None;
            float size = UiTokens.ControlSmall;
            int node = UiDraw.IconButton(s, at, cx + cw - size, ry + (rowH - size) / 2f, size, (UiIcon)value, tint, command + Act, r);
            if (node >= 0 && node < s.NodeCount && s.Nodes[node].Command == command + Act)
            {
                s.Nodes[node].List = list;   // a row's own button is of its list: the pad that reaches the list reaches it
                s.Nodes[node].Index = r;
                return node;
            }
            return -1;
        }

        // A cell by its column's kind, its box from cx to cx + cw in the row. Returns a stepper's typed figure, the target that
        // stands for its row, or -1.
        private static int Cell(UiScene s, UiAt at, UiDataTableModel m, ref UiDataRow row, int c, float cx, float ry, float cw, float rowH, bool sel, int command, int r)
        {
            ref UiDataColumn col = ref m.Columns[c];
            string words = row.Cells != null && c < row.Cells.Length ? row.Cells[c] : null;
            int value = row.Values != null && c < row.Values.Length ? row.Values[c] : -1;
            string line = row.Subs != null && c < row.Subs.Length && row.Subs[c] != null ? row.Subs[c] : row.Sub;
            switch (col.Kind)
            {
                case UiCellKind.Person:
                {
                    float face = UiTokens.AvatarSmall;
                    bool nobody = value == NoFace;   // no one stands in the row: the words alone, with no face drawn for nobody
                    bool glyph = value <= GlyphFaceBase;
                    if (glyph)
                    {
                        float gy = ry + (rowH - face) / 2f, g = UiTokens.Icon;
                        int gt = UiDraw.Tile(s, at, cx + UiTokens.S1, gy, face, UiRole.None, false);
                        UiDraw.Icon(s, at.In(gt), cx + UiTokens.S1 + (face - g) / 2f, gy + (face - g) / 2f, g, (UiIcon)(GlyphFaceBase - value), UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
                    }
                    else if (!nobody)
                    {
                        UiDraw.Portrait(s, at, cx + UiTokens.S1, ry + (rowH - face) / 2f, face, row.Face ?? words, row.Hue, row.Ring);   // its ring's stroke inside the step before it
                    }
                    float tx = nobody ? cx + UiTokens.S1 : cx + UiTokens.S1 + face + UiTokens.S3, tw = cw - (tx - cx);
                    float lh = UiTextRules.LineHeight(UiStyle.Title), sh = UiTextRules.LineHeight(UiStyle.Small);
                    float top = ry + (rowH - lh - sh) / 2f;
                    UiDraw.Text(s, at, tx, top, tw, UiStyle.Title, UiRole.Text, words, UiFlags.Truncate | UiFlags.Middle);
                    UiDraw.Text(s, at, tx, top + lh, tw, UiStyle.Small, sel ? UiRole.Text : UiRole.Text2, line, UiFlags.Truncate | UiFlags.Middle);
                    break;
                }
                case UiCellKind.Tile:
                {
                    float tile = UiTokens.ControlSmall;
                    UiRole tint = row.Tones != null && c < row.Tones.Length ? row.Tones[c] : UiRole.None;
                    string picture = row.Pictures != null && c < row.Pictures.Length ? row.Pictures[c] : null;
                    if (!string.IsNullOrEmpty(picture))
                    {
                        // The game's own picture of the thing on its tile, letterboxed, where the screen found one.
                        int t = UiDraw.Tile(s, at, cx, ry + (rowH - tile) / 2f, tile, UiRole.None, false);
                        var item = new UiItem
                        {
                            Kind = UiKind.Picture, Flags = UiFlags.Middle | UiFlags.Centre, X = cx + UiTokens.Hairline, Y = ry + (rowH - tile) / 2f + UiTokens.Hairline,
                            W = tile - UiTokens.Hairline * 2f, H = tile - UiTokens.Hairline * 2f, Text = picture, Value = 1f, Clip = at.Clip, Node = -1, Layer = at.Layer,
                            Order = at.Order, Parent = t, Alpha = 1f
                        };
                        s.Add(ref item);
                    }
                    else if (value > 0)
                    {
                        int t = UiDraw.Tile(s, at, cx, ry + (rowH - tile) / 2f, tile, tint, false);
                        float g = UiTokens.Icon;
                        UiDraw.Icon(s, at.In(t), cx + (tile - g) / 2f, ry + (rowH - g) / 2f, g, (UiIcon)value, tint, UiFlags.Middle | UiFlags.Centre, true);
                    }
                    float tx = cx + tile + UiTokens.S3, tw = cw - (tx - cx);
                    float lh = UiTextRules.LineHeight(UiStyle.Title), sh = UiTextRules.LineHeight(UiStyle.Small);
                    bool sub = !string.IsNullOrEmpty(line);
                    float top = ry + (rowH - lh - (sub ? sh : 0f)) / 2f;
                    UiDraw.Text(s, at, tx, top, tw, UiStyle.Title, UiRole.Text, words, UiFlags.Truncate | UiFlags.Middle);
                    if (sub)
                    {
                        UiDraw.Text(s, at, tx, top + lh, tw, UiStyle.Small, sel ? UiRole.Text : UiRole.Text2, line, UiFlags.Truncate | UiFlags.Middle);
                    }
                    break;
                }
                case UiCellKind.Stepper:
                {
                    float sw = Math.Min(cw, StepperWidth);
                    UiRole mark = row.Tones != null && c < row.Tones.Length ? row.Tones[c] : UiRole.None;
                    if ((mark == UiRole.Warn || mark == UiRole.Danger) && cw - sw >= MarkRoom)
                    {
                        // What the owner marks about the amount (a price above the market) as the warning's glyph before it.
                        UiDraw.Icon(s, at, cx + cw - sw - UiTokens.S1 - UiTokens.Icon, ry + (rowH - UiTokens.Icon) / 2f, UiTokens.Icon, UiIcon.Warning, mark, UiFlags.Middle, true);
                    }
                    if (col.OnChosen)
                    {
                        if (!sel)
                        {
                            // Not the chosen row: its figure where the chosen row's field shows it, so choosing a row moves nothing.
                            float fx = cx + cw - sw + UiDraw.StepButton + UiTokens.S2, fw = sw - (UiDraw.StepButton + UiTokens.S2) * 2f;
                            UiDraw.TextMiddle(s, at, fx + UiTokens.S2, ry, rowH, fw - UiTokens.S4, UiStyle.Body, UiRole.Text, words, UiFlags.Centre | UiFlags.Truncate);
                        }
                        return -1;   // the chosen row's stepper stands over its row's own target (ChosenStepper)
                    }
                    return UiDraw.Stepper(s, at, cx + cw - sw, ry + (rowH - UiTokens.Control) / 2f, sw, words, (value & CanLess) != 0, (value & CanMore) != 0,
                        command + Less, command + Type, command + More, r, value <= 0);
                }
                case UiCellKind.Action:
                    break;   // laid after the row's own target (Act)
                case UiCellKind.Chip:
                {
                    if (string.IsNullOrEmpty(words))
                    {
                        break;
                    }
                    UiRole tone = row.Tones != null && c < row.Tones.Length ? row.Tones[c] : UiRole.Muted;
                    float chip = Math.Min(UiDraw.SnapDown(cw), UiDraw.Snap(UiTokens.S2 * 3f + UiTokens.StatusDot + UiTextRules.Width(words, UiStyle.SmallStrong) + UiTokens.S1));
                    UiDraw.Chip(s, at, cx + cw - chip, ry + (rowH - UiTokens.Badge - UiTokens.S1) / 2f, words, tone, chip);
                    break;
                }
                case UiCellKind.Meter:
                {
                    if (value < 0 && string.IsNullOrEmpty(words))
                    {
                        break;   // a row the figure does not exist for (a repair call has no condition) draws neither a figure nor an empty track
                    }
                    UiRole grade = UiTokens.Grade(Math.Max(0, value));
                    // The figure's room is that of "100" (or "100%" for a per cent) or of its own word when that is wider ("Patchy"), so its bar never ends under it.
                    float fw = Math.Max(UiTextRules.Width(words != null && words.EndsWith("%", StringComparison.Ordinal) ? "100%" : "100", UiStyle.BodyStrong),
                        UiTextRules.Width(words ?? string.Empty, UiStyle.BodyStrong)) + UiTokens.S1;
                    // On the chosen row's cyan tone a low grade's red or orange falls under 4.5:1: the figure beside its meter
                    // takes the row's words' colour there, and the meter keeps the grade.
                    UiDraw.TextMiddle(s, at, cx + cw - fw, ry, rowH, fw, UiStyle.BodyStrong, sel ? UiRole.Text : grade, words, UiFlags.Right);
                    float bw = UiDraw.SnapDown(cw - fw - UiTokens.S2);
                    if (bw >= UiTokens.S8)
                    {
                        UiDraw.Meter(s, at, cx, ry + (rowH - UiTokens.S1) / 2f, bw, UiTokens.S1, value, grade);
                    }
                    break;
                }
                case UiCellKind.Figure:
                {
                    // A figure the row tones says its own good or bad; a graded figure alone is its column's only sign of good or
                    // bad, so on the chosen row it keeps its grade in the step that reads on that tone.
                    UiRole told = row.Tones != null && c < row.Tones.Length ? row.Tones[c] : UiRole.None;
                    bool toned = told == UiRole.OkText || told == UiRole.WarnText || told == UiRole.DangerText;
                    UiRole ink = toned ? told : col.Graded && value >= 0 ? (sel ? UiTokens.GradeOnChosen(UiTokens.Grade(value)) : UiTokens.Grade(value)) : sel ? UiRole.Text : UiRole.Text2;
                    UiDraw.TextMiddle(s, at, cx, ry, rowH, cw, col.Graded ? UiStyle.BodyStrong : UiStyle.Body, ink, words, UiFlags.Right | UiFlags.Truncate);
                    break;
                }
                case UiCellKind.Trend:
                    UiDataCells.Trend(s, at, words, value, row.Tones != null && c < row.Tones.Length ? row.Tones[c] : UiRole.None, cx, ry, cw, rowH, sel);
                    break;
                case UiCellKind.Steps:
                    UiDataCells.Steps(s, at, words, value, row.Share, col.Steps, cx, ry, cw, rowH, sel);
                    break;
                case UiCellKind.Stars:
                {
                    float size = UiTokens.IconSmall, starsW = 5f * size + 4f * UiTokens.Hairline * 2f;
                    if (cw >= starsW)
                    {
                        UiDraw.Stars(s, at, cx + cw - starsW, ry + (rowH - size) / 2f, size, Math.Max(0, Math.Min(10, value)));
                    }
                    break;
                }
                case UiCellKind.Spark:
                    if (row.Line != null && row.Line.Length > 1 && cw >= UiTokens.S8)
                    {
                        UiRole spark = row.Tones != null && c < row.Tones.Length && row.Tones[c] != UiRole.None ? row.Tones[c] : UiRole.Accent;
                        UiDraw.Spark(s, at, cx, ry + UiTokens.S2, cw, rowH - UiTokens.S4, row.Line, spark);
                    }
                    break;
                case UiCellKind.Money:
                case UiCellKind.Date:
                {
                    // A sum the game writes in its red (a loss) keeps that tone off the chosen row, where the red would not read.
                    bool loss = col.Kind == UiCellKind.Money && !sel && row.Tones != null && c < row.Tones.Length && row.Tones[c] == UiRole.DangerText;
                    UiRole ink = loss ? UiRole.DangerText : sel || col.Kind == UiCellKind.Money ? UiRole.Text : UiRole.Text2;
                    UiDraw.TextMiddle(s, at, cx, ry, rowH, cw, UiStyle.Body, ink, words, UiFlags.Right | UiFlags.Truncate);
                    break;
                }
                default:
                    UiDraw.TextMiddle(s, at, cx, ry, rowH, cw, c == 0 ? UiStyle.Title : UiStyle.Body, c == 0 || sel ? UiRole.Text : UiRole.Text2, words, UiFlags.Truncate);
                    break;
            }
            return -1;
        }

        // The totals at the foot under a rule: the label in the first column's place, each figure under its column.
        private static void Totals(UiScene s, UiAt at, UiDataTableModel m, float x, float y, float w, float[] cx, float[] cw)
        {
            UiDraw.Box(s, at, x, y, w, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            float ry = y + UiTokens.S1, h = UiTokens.ControlSmall;
            for (int c = 0; c < m.Columns.Length; c++)
            {
                string words = c < m.Totals.Length ? m.Totals[c] : null;
                if (c == 0 && string.IsNullOrEmpty(words))
                {
                    words = m.TotalsLabel;
                    UiDraw.TextMiddle(s, at, cx[c], ry, h, cw[c], UiStyle.SectionHeader, UiRole.Text2, words, UiFlags.Label | UiFlags.Truncate);
                    continue;
                }
                if (!string.IsNullOrEmpty(words))
                {
                    UiDraw.TextMiddle(s, at, cx[c], ry, h, cw[c], UiStyle.BodyStrong, UiRole.Text, words, UiFlags.Truncate | (RightAligned(m.Columns[c].Kind) ? UiFlags.Right : UiFlags.None));
                }
            }
        }
    }
}
