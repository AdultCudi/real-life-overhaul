using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One thing on a day of the diary: its kind's glyph in its colour, its words and what it shows at its right.</summary>
    public struct UiDiaryEntry
    {
        public int Day;          // the day of the month it stands on, 1 to the month's length
        public UiIcon Icon;      // its kind (money, an appointment, the city), in the glyph's own colour
        public UiRole Tint;      // None for the icon's own
        public string Title;     // "Rent to Hudson Realty"
        public string Value;     // at its right in the chosen day's rows: "$1,240", "09:00"; null for none
        public string Action;    // the link under it that deals with it ("Open the bank"); null when nothing does
    }

    /// <summary>
    /// A month as a diary page: its name, the weekdays' short names in the game's order (Monday first), where its first
    /// day falls, its length, today, the chosen day and what stands on each day, sorted by day as the calendar sorts them.
    /// </summary>
    public sealed class UiDiaryModel
    {
        public string Month;                           // "March, year 3"
        public string[] Weekdays = new string[0];      // seven short names, Monday first as the game's calendar
        public int Lead;                               // empty cells before the first day (MonthViewRules.Lead)
        public int Length = 30;                        // days in the month
        public int Today;                              // today's day of this month, or 0 when today is in another month
        public int Chosen = 1;                         // the day whose things stand beside the grid
        public int PastBefore;                         // days before this one are gone by and read quieter; 0 for none
        public UiDiaryEntry[] Entries = new UiDiaryEntry[0];
        public string More = "+{0} more";              // the words under a full day's lines, {0} the count
        public string ChosenTitle;                     // "Tuesday 14 March"
        public UiPairModel[] ChosenFigures = new UiPairModel[0];   // the day's money in and out, as the calendar tallies it
        public string ChosenEmpty;                     // a useful sentence for a day with nothing on it
        public string AheadTitle;                      // "Coming up": the next days' things under the chosen day's
        public bool CanBack = true, CanOn = true;      // a month before and after can be turned to (MonthViewRules.Step)
    }

    /// <summary>
    /// A month as a diary page, a block of an app window's page: the month's name between the arrows that turn it (Q and E
    /// turn it too), the weekdays in the game's order, a cell a day with its date large, up to three of its things as short
    /// lines behind their kinds' glyphs and how many more, today's date on the cyan, the chosen day filled; beside the grid
    /// the chosen day's money and its things as rows that lead where each is dealt with. For the Calendar (MonthViewRules
    /// holds the arithmetic) and a campus term. Commands: <see cref="DayCommand"/> (a day), <see cref="MonthCommand"/>
    /// (-1 or 1) and <see cref="EntryCommand"/> (an entry's index). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiDiary
    {
        public const int DayCommand = 20, MonthCommand = 21, EntryCommand = 22;
        public const int MostLines = 3;              // things a day's cell shows before it says how many more
        public const float AsideWidth = 360f;        // px: the chosen day's column, an entry's name beside its figure
        public const float CellGap = 4f;             // px between two days, the grid's hairline of panel
        public const float EntryLine = 20f;          // px: a thing's short line in a cell, its glyph and its words
        private const float EntryRow = 48f;          // px: a thing in the chosen day's column: two pair rows
        private const byte RegionGrid = 1, RegionAside = 2;

        /// <summary>Lays the diary into the room an app window's Shell gave its page.</summary>
        public static void Lay(UiScene s, UiAt at, UiDiaryModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            float gw = w - AsideWidth - UiTokens.S6;
            float top = Head(s, at.InRegion(RegionGrid), m, x, y, gw, keys);
            Grid(s, at.InRegion(RegionGrid), m, x, top, gw, y + h - top);
            float ax = x + w - AsideWidth;
            UiDraw.Box(s, at, ax - UiTokens.S3, y, UiTokens.Hairline, h, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
            Aside(s, at.InRegion(RegionAside), m, ax, y, AsideWidth, h);
        }

        // The month's name between the arrows that turn it (the tab keys turn it too: the status bar names them). Returns the y under it.
        private static float Head(UiScene s, UiAt at, UiDiaryModel m, float x, float y, float w, IUiKeys keys)
        {
            float h = UiTokens.Control, b = UiTokens.ControlSmall;
            int drawn = s.ItemCount;
            int back = UiDraw.IconButton(s, at, x, y + (h - b) / 2f, b, UiIcon.Back, UiRole.Text, MonthCommand, -1);
            if (!m.CanBack)
            {
                UiDraw.Dim(s, drawn, back);
            }
            float tx = x + b + UiTokens.S3;
            float tw = UiDraw.Snap(UiTextRules.Width(m.Month, UiStyle.Number) + UiTokens.S1);
            UiDraw.TextMiddle(s, at, tx, y, h, tw, UiStyle.Number, UiRole.Text, m.Month, UiFlags.Truncate);
            drawn = s.ItemCount;
            int on = UiDraw.IconButton(s, at, tx + tw + UiTokens.S3, y + (h - b) / 2f, b, UiIcon.Next, UiRole.Text, MonthCommand, 1);
            if (!m.CanOn)
            {
                UiDraw.Dim(s, drawn, on);
            }
            return y + h + UiTokens.S3;
        }

        /// <summary>The weeks a month stands in, its first day after so many empty cells.</summary>
        public static int Weeks(int lead, int length) => Math.Max(1, (Math.Max(0, lead) + Math.Max(0, length) + 6) / 7);

        /// <summary>The lines a day's cell shows of so many things in a room of so many lines: its things, the last line "+n more" when they do not fit.</summary>
        public static int Shown(int things, int room)
        {
            int lines = Math.Max(0, Math.Min(MostLines, room));
            return things <= lines ? things : Math.Max(0, lines - 1);
        }

        // The weekdays' names over seven columns, then a cell a day: its date, its first things, how many more.
        private static void Grid(UiScene s, UiAt at, UiDiaryModel m, float x, float y, float w, float h)
        {
            float cw = UiDraw.SnapDown((w - CellGap * 6f) / 7f);
            for (int c = 0; c < 7 && c < m.Weekdays.Length; c++)
            {
                UiDraw.Text(s, at, x + c * (cw + CellGap) + UiTokens.S3, y, cw - UiTokens.S3, UiStyle.SectionHeader, UiRole.Muted, m.Weekdays[c], UiFlags.Label | UiFlags.Truncate);
            }
            float cy = y + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            int weeks = Weeks(m.Lead, m.Length);
            float ch = UiDraw.SnapDown((y + h - cy - CellGap * (weeks - 1)) / weeks);
            float dateH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Number)) + UiTokens.S1;
            int room = (int)((ch - UiTokens.S2 * 2f - dateH) / EntryLine);
            int e = 0;
            for (int day = 1; day <= m.Length; day++)
            {
                int cell = m.Lead + day - 1;
                float dx = x + (cell % 7) * (cw + CellGap), dy = cy + (cell / 7) * (ch + CellGap);
                int first = e, things = 0;
                while (e < m.Entries.Length && m.Entries[e].Day < day)
                {
                    e++;
                    first = e;
                }
                while (e < m.Entries.Length && m.Entries[e].Day == day)
                {
                    e++;
                    things++;
                }
                Cell(s, at, m, day, first, things, room, dx, dy, cw, ch, dateH);
            }
        }

        private static void Cell(UiScene s, UiAt at, UiDiaryModel m, int day, int first, int things, int room, float x, float y, float w, float h, float dateH)
        {
            bool chosen = day == m.Chosen;
            // The day on its band, its face clear over it, so the pointer's still lift reads on the band as on a row.
            UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            int face = UiDraw.Box(s, at, x, y, w, h, chosen ? UiRole.RowSel : UiRole.None, UiRole.None, UiTokens.RadiusLarge, chosen ? UiFlags.Selected : UiFlags.None);
            UiAt inside = at.In(face);
            string date = day.ToString(System.Globalization.CultureInfo.InvariantCulture);
            float dw = UiDraw.Snap(UiTextRules.Width(date, UiStyle.Number) + UiTokens.S2 * 2f);
            if (day == m.Today)
            {
                // Today on the cyan, as the game marks its own day: dark words where a white would not read.
                int chip = UiDraw.Box(s, inside, x + UiTokens.S2, y + UiTokens.S2, dw, dateH, UiRole.Accent, UiRole.None, UiTokens.RadiusMedium);
                UiDraw.TextMiddle(s, at.In(chip), x + UiTokens.S2, y + UiTokens.S2, dateH, dw, UiStyle.Number, UiRole.OnAccent, date, UiFlags.Centre);
            }
            else
            {
                bool gone = day < m.PastBefore;
                UiDraw.TextMiddle(s, inside, x + UiTokens.S2, y + UiTokens.S2, dateH, dw, UiStyle.Number, gone && !chosen ? UiRole.Muted : UiRole.Text, date, UiFlags.Centre);
            }
            int shown = Shown(things, room);
            float ly = y + UiTokens.S2 + dateH + UiTokens.S1;
            float lx = x + UiTokens.S2, lw = w - UiTokens.S2 * 2f;
            for (int i = 0; i < shown; i++)
            {
                ref UiDiaryEntry entry = ref m.Entries[first + i];
                UiDraw.Icon(s, inside, lx, ly + (EntryLine - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, entry.Icon, entry.Tint, UiFlags.Middle, true);
                UiDraw.TextMiddle(s, inside, lx + UiTokens.IconSmall + UiTokens.S1, ly, EntryLine, lw - UiTokens.IconSmall - UiTokens.S1, UiStyle.Small, UiRole.Text, entry.Title,
                    UiFlags.Truncate);
                ly += EntryLine;
            }
            if (things > shown)
            {
                string more = string.Format(System.Globalization.CultureInfo.InvariantCulture, m.More ?? "+{0}", things - shown);
                UiDraw.TextMiddle(s, inside, lx + UiTokens.IconSmall + UiTokens.S1, ly, EntryLine, lw - UiTokens.IconSmall - UiTokens.S1, UiStyle.Small, chosen ? UiRole.Text : UiRole.Muted,
                    more, UiFlags.Label | UiFlags.Truncate);
            }
            UiDraw.Node(s, at, x, y, w, h, DayCommand, day, face);
        }

        // The chosen day: its name, its money on a band, its things as rows that each lead where it is dealt with, then the
        // days ahead's things, each a press away from being the chosen day.
        private static void Aside(UiScene s, UiAt at, UiDiaryModel m, float x, float y, float w, float h)
        {
            UiDraw.Text(s, at, x, y + (UiTokens.Control - UiTextRules.LineHeight(UiStyle.SectionHeader)) / 2f, w, UiStyle.SectionHeader, UiRole.Text, m.ChosenTitle,
                UiFlags.Label | UiFlags.Truncate | UiFlags.Middle);
            float cy = y + UiTokens.Control + UiTokens.S3, foot = y + h;
            if (m.ChosenFigures.Length > 0)
            {
                float bh = UiTokens.S2 * 2f + m.ChosenFigures.Length * UiTokens.PairRow;
                UiDraw.Band(s, at, x, cy, w, bh);
                for (int i = 0; i < m.ChosenFigures.Length; i++)
                {
                    UiDraw.InfoRow(s, at, x + UiTokens.S3, cy + UiTokens.S2 + i * UiTokens.PairRow, w - UiTokens.S3 * 2f, m.ChosenFigures[i].Key, m.ChosenFigures[i].Value);
                }
                cy += bh + UiTokens.S4;
            }
            int count = 0, ahead = 0;
            for (int i = 0; i < m.Entries.Length; i++)
            {
                count += m.Entries[i].Day == m.Chosen ? 1 : 0;
                ahead += m.Entries[i].Day > m.Chosen ? 1 : 0;
            }
            if (count == 0 && ahead == 0)
            {
                UiDraw.EmptyState(s, at, x, cy, w, UiDraw.SnapDown(foot - cy), UiIcon.Calendar, null, m.ChosenEmpty);
                return;
            }
            if (count == 0)
            {
                UiDraw.Text(s, at, x, cy, w, UiStyle.Body, UiRole.Text2, m.ChosenEmpty, UiFlags.Label | UiFlags.Wrap, 3);
                cy += UiDraw.Snap(UiTextRules.WrappedHeight(m.ChosenEmpty, UiStyle.Body, w, 3)) + UiTokens.S4;
            }
            for (int i = 0, shown = 0; i < m.Entries.Length && cy + EntryRow <= foot; i++)
            {
                if (m.Entries[i].Day == m.Chosen)
                {
                    Entry(s, at.Arriving(shown++), ref m.Entries[i], i, x, cy, w);
                    cy += EntryRow + UiTokens.S1;
                }
            }
            if (ahead == 0 || string.IsNullOrEmpty(m.AheadTitle) || cy + UiTokens.S6 + AheadRow * 2f > foot)
            {
                return;
            }
            cy += UiTokens.S5;
            UiDraw.Text(s, at, x, cy, w, UiStyle.SectionHeader, UiRole.Text, m.AheadTitle, UiFlags.Label | UiFlags.Truncate);
            cy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
            for (int i = 0; i < m.Entries.Length && cy + AheadRow <= foot; i++)
            {
                if (m.Entries[i].Day > m.Chosen)
                {
                    Ahead(s, at, m, ref m.Entries[i], x, cy, w);
                    cy += AheadRow;
                }
            }
        }

        private const float AheadRow = 32f;   // px: a thing of the days ahead, its date, its glyph, its name and its figure

        // A thing of the days ahead: its date and weekday, its kind's glyph, its name, its figure; a press makes its day the chosen one.
        private static void Ahead(UiScene s, UiAt at, UiDiaryModel m, ref UiDiaryEntry entry, float x, float y, float w)
        {
            int face = UiDraw.Box(s, at, x - UiTokens.S2, y, w + UiTokens.S4, AheadRow, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            string weekday = m.Weekdays.Length == 7 ? m.Weekdays[(m.Lead + entry.Day - 1) % 7] : string.Empty;
            string date = entry.Day.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + weekday;
            float dw = UiDraw.Snap(UiTextRules.Width("30 Wed", UiStyle.SmallStrong) + UiTokens.S2);
            UiDraw.TextMiddle(s, inside, x, y, AheadRow, dw, UiStyle.SmallStrong, UiRole.Text2, date, UiFlags.Truncate);
            float gx = x + dw;
            UiDraw.Icon(s, inside, gx, y + (AheadRow - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, entry.Icon, entry.Tint, UiFlags.Middle, true);
            float tx = gx + UiTokens.IconSmall + UiTokens.S2;
            float vw = string.IsNullOrEmpty(entry.Value) ? 0f : UiDraw.Snap(UiTextRules.Width(entry.Value, UiStyle.Small) + UiTokens.S1);
            UiDraw.TextMiddle(s, inside, tx, y, AheadRow, x + w - tx - (vw > 0f ? vw + UiTokens.S3 : 0f), UiStyle.Body, UiRole.Text, entry.Title, UiFlags.Truncate);
            if (vw > 0f)
            {
                UiDraw.TextMiddle(s, inside, x + w - vw, y, AheadRow, vw, UiStyle.Small, UiRole.Text2, entry.Value, UiFlags.Right);
            }
            UiDraw.Node(s, at, x - UiTokens.S2, y, w + UiTokens.S4, AheadRow, DayCommand, entry.Day, face);
        }

        // A thing of the chosen day: its kind's glyph on a tile, its name over its figure, and the words that lead where it is dealt with.
        private static void Entry(UiScene s, UiAt at, ref UiDiaryEntry entry, int index, float x, float y, float w)
        {
            bool leads = !string.IsNullOrEmpty(entry.Action);
            int face = UiDraw.Box(s, at, x - UiTokens.S2, y, w + UiTokens.S4, EntryRow, UiRole.None, UiRole.None, UiTokens.RadiusLarge);
            UiAt inside = at.In(face);
            float tile = UiTokens.ControlSmall;
            int t = UiDraw.Tile(s, inside, x, y + (EntryRow - tile) / 2f, tile, entry.Tint, false);
            UiDraw.Icon(s, inside.In(t), x + (tile - UiTokens.Icon) / 2f, y + (EntryRow - UiTokens.Icon) / 2f, UiTokens.Icon, entry.Icon, entry.Tint, UiFlags.Middle | UiFlags.Centre, true);
            float tx = x + tile + UiTokens.S3;
            float vw = string.IsNullOrEmpty(entry.Value) ? 0f : UiDraw.Snap(UiTextRules.Width(entry.Value, UiStyle.BodyStrong) + UiTokens.S1);
            float nameH = UiTextRules.LineHeight(UiStyle.Title), subH = UiTextRules.LineHeight(UiStyle.Small);
            float top = y + (EntryRow - nameH - (leads ? UiTokens.S1 + subH : 0f)) / 2f;
            UiDraw.Text(s, inside, tx, top, x + w - tx - (vw > 0f ? vw + UiTokens.S3 : 0f), UiStyle.Title, UiRole.Text, entry.Title, UiFlags.Truncate | UiFlags.Middle);
            if (vw > 0f)
            {
                UiDraw.Text(s, inside, x + w - vw, top, vw, UiStyle.BodyStrong, UiRole.Text, entry.Value, UiFlags.Right | UiFlags.Middle);
            }
            if (leads)
            {
                // Where it is dealt with, as a link reads: the cyan words and the arrow one gap after them.
                float ly = top + nameH + UiTokens.S1;
                string shown = UiTextRules.Cut(entry.Action, UiStyle.Small, x + w - tx - UiTokens.IconSmall - UiDraw.LinkGap);
                float drawn = UiTextRules.Width(shown, UiStyle.Small);
                UiDraw.Text(s, inside, tx, ly, x + w - tx - UiTokens.IconSmall - UiDraw.LinkGap, UiStyle.Small, UiRole.AccentText, shown, UiFlags.Truncate | UiFlags.Middle);
                UiDraw.Icon(s, inside, tx + drawn + UiDraw.LinkGap, ly + (subH - UiTokens.IconSmall) / 2f, UiTokens.IconSmall, UiIcon.Next, UiRole.AccentText,
                    UiFlags.Middle | UiFlags.Share | UiFlags.Decor);
                UiDraw.Node(s, at, x - UiTokens.S2, y, w + UiTokens.S4, EntryRow, EntryCommand, index, face);
            }
        }
    }
}
