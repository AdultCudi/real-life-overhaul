using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A line of the profile's overview: what moves the happiness (its arrow, its share and its cause) or a goal on its way
    /// (its glyph on a tile, the goal, its tiers as a bar and the tier reached).
    /// </summary>
    public struct UiPersonaLine
    {
        public UiIcon Icon;          // a goal's glyph; None for a mood, whose arrow says which way it moves
        public string Words;         // the cause, or the goal
        public string Figure;        // the share ("+15%"), or the tier reached
        public int Sign;             // a mood's direction: 1 lifts, -1 lowers
        public int Reached;          // a goal's tiers reached
        public float Share;          // a goal's way through its next tier, 0 to 1
    }

    /// <summary>
    /// What the player's own profile shows: the app's tabs across, the head (the face in the ring of their mood, their
    /// name, age, worth, businesses and staff), and the tab's page: the weekly income's line beside the wealth's donut, then
    /// the goals nearest their next tier and what moves the happiness most, each with the way to its tab (the overview);
    /// the needs as graded meters over what moves the happiness (the needs); the courses (education); the goals (goals).
    /// </summary>
    public sealed class UiPersonaModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.IdCard;
        public UiNavEntry[] Tabs = new UiNavEntry[0];
        public int Tab;
        public UiProfileModel Profile = new UiProfileModel();
        public string WealthTitle;
        public string[] WealthLabels = new string[0];   // the parts owned, none of nothing
        public string[] WealthValues = new string[0];
        public float[] WealthShares = new float[0];
        public string LoansLabel, LoansValue;          // what is owed, under the parts it is taken from; null for none
        public string WealthEmpty;                     // the donut's words while nothing is owned yet
        public UiChartModel Income = new UiChartModel();
        public string MoodsTitle, MoodsMore, MoodsEmpty;
        public UiPersonaLine[] Moods = new UiPersonaLine[0];
        public string GoalsTitle, GoalsMore, GoalsEmpty;
        public UiPersonaLine[] Goals = new UiPersonaLine[0];
        public int GoalSteps = 3;
        public UiIcon[] NeedIcons = new UiIcon[0];
        public string[] NeedLabels = new string[0];
        public int[] NeedValues = new int[0];          // 0 to 100
        public string ListTitle;                       // the needs' table: its title in capitals
        public UiDataTableModel Table = new UiDataTableModel();
        public UiPrompt[] Prompts = new UiPrompt[0];
    }

    /// <summary>
    /// The player's profile in the Persona app, after Football Manager's player profile in the Cities: Skylines II look:
    /// an app window with its tabs across and the profile head over every tab. The overview is the player at a glance: the
    /// weekly income over the last days as a line beside the worth's split as a donut with its legend and the debt under
    /// it, then the goals nearest their next tier and the few things that move the happiness most, each block with the way
    /// to the whole list on its tab, as a management game's overview leads to its pages. The needs tab puts the three
    /// needs on cards with their glyphs over the table of what lifts or lowers the happiness; the education and goals tabs
    /// are the kit's big table, the goals with their tiers as a bar. Commands: the window's (<see cref="UiAppWindow.NavCommand"/>
    /// a tab), <see cref="LinkCommand"/> (a block's way to its tab), the big table's from <see cref="UiDataTable.First"/>
    /// and the chart's from <see cref="UiChart.First"/>. Game-free. Cost: a layout a change, the table's rows in view only.
    /// </summary>
    public static class UiPersona
    {
        public const int Overview = 0, Needs = 1, Education = 2, Goals = 3;
        public const int LinkCommand = 210;
        public const int MostLines = 3;           // the overview's lines a block: the few that matter most, the rest on their tab
        private const float DonutMost = 320f;     // px: the wealth's donut at its largest, so it fills its band
        private const float LegendMost = 360f;    // px: the legend's width, so each part's value stays by its name
        private const float NeedCard = 72f;       // px: a need's card, its tile and its two lines
        private const float LegendRow = 20f;      // px: a legend's row, as UiDraw.Legend draws it
        private const float LineRow = 40f;        // px: an overview line, its tile and its words

        public static void Lay(UiScene s, UiPersonaModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("persona", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, m.Icon, m.Tabs, m.Tab, true, keys, out float x, out float y, out float w, out float bottom);
            int tab = s.FocusFirst;
            y = UiAppWindow.Profile(s, at, m.Profile, null, x, y, w, keys);
            switch (m.Tab)
            {
                case Needs:
                    NeedsPage(s, at, m, x, y, w, bottom - y, keys);
                    break;
                case Education:
                case Goals:
                    ListPage(s, at, m, x, y, w, bottom - y, keys);
                    break;
                default:
                    OverviewPage(s, at, m, x, y, w, bottom - y, tab);
                    break;
            }
            for (int i = 0; i < m.Prompts.Length; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // Two columns of two rows, each a band with its title inside as the chart's: the income's line beside the wealth,
        // then the goals on their way beside the moods. The first row takes the room the second leaves, so the page meets
        // the window's foot. The keys walk it as it reads: the tabs, the chart's days, then each block's way to its tab.
        private static void OverviewPage(UiScene s, UiAt at, UiPersonaModel m, float x, float y, float w, float h, int tab)
        {
            float gap = UiTokens.S6, cw = UiDraw.SnapDown((w - gap) / 2f), rx = x + cw + gap;
            float lowH = UiTokens.S3 + UiChart.HeadHeight + MostLines * LineRow + UiTokens.S2 + UiTokens.Control + UiTokens.S2;
            float topH = UiDraw.SnapDown(h - lowH - gap);
            int firstPoint = s.NodeCount;
            UiChart.Lay(s, at, m.Income, x, y, cw, topH);
            int lastPoint = s.NodeCount - 1;
            Wealth(s, at, m, rx, y, cw, topH);
            float ly = y + topH + gap;
            int goals = Block(s, at, x, ly, cw, lowH, m.GoalsTitle, m.Goals, m.GoalsEmpty, m.GoalsMore, Goals, m.GoalSteps);
            int moods = Block(s, at, rx, ly, cw, lowH, m.MoodsTitle, m.Moods, m.MoodsEmpty, m.MoodsMore, Needs, 0);
            for (int i = firstPoint; i <= lastPoint; i++)
            {
                s.Nodes[i].Up = tab >= 0 ? tab : i;
                s.Nodes[i].Down = goals >= 0 ? goals : moods >= 0 ? moods : i;
            }
            if (goals >= 0)
            {
                s.Nodes[goals].Up = lastPoint >= firstPoint ? firstPoint : tab;
                s.Nodes[goals].Left = s.Nodes[goals].Down = goals;
                s.Nodes[goals].Right = moods >= 0 ? moods : goals;
            }
            if (moods >= 0)
            {
                s.Nodes[moods].Up = lastPoint >= firstPoint ? lastPoint : tab;
                s.Nodes[moods].Right = s.Nodes[moods].Down = moods;
                s.Nodes[moods].Left = goals >= 0 ? goals : moods;
            }
        }

        // A band with its title in capitals at its top left, as a chart's head stands; returns where its rows go and their top.
        private static UiAt Band(UiScene s, UiAt at, float x, float y, float w, float h, string title, out float top)
        {
            int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(band);
            UiDraw.TextMiddle(s, on, x + UiTokens.S4, y + UiTokens.S3, UiTokens.PairRow, w - UiTokens.S4 * 2f, UiStyle.SectionHeader, UiRole.Text, title, UiFlags.Label | UiFlags.Truncate);
            top = y + UiTokens.S3 + UiChart.HeadHeight;
            return on;
        }

        // An overview block: its few lines, or its empty words, and the way to its tab under them. Returns the way's target.
        private static int Block(UiScene s, UiAt at, float x, float y, float w, float h, string title, UiPersonaLine[] lines, string empty, string more, int tab,
            int steps)
        {
            UiAt on = Band(s, at, x, y, w, h, title, out float top);
            float ix = x + UiTokens.S4, iw = w - UiTokens.S4 * 2f;
            int n = Math.Min(MostLines, lines.Length);
            if (n == 0)
            {
                // Nothing to list: its words in the middle of the block's room, as the chart beside it says its own.
                float eh = UiDraw.Snap(UiTextRules.WrappedHeight(empty, UiStyle.Small, iw, 3));
                UiDraw.Text(s, on, ix, top + UiDraw.SnapDown((y + h - UiTokens.S4 - top - eh) / 2f), iw, UiStyle.Small, UiRole.Text2, empty, UiFlags.Label | UiFlags.Wrap | UiFlags.Centre, 3);
                return -1;
            }
            for (int i = 0; i < n; i++)
            {
                float ly = top + i * LineRow;
                if (steps > 0)
                {
                    GoalLine(s, on, ref lines[i], steps, ix, ly, iw);
                }
                else
                {
                    MoodLine(s, on, ref lines[i], ix, ly, iw);
                }
            }
            if (string.IsNullOrEmpty(more))
            {
                return -1;
            }
            int first = s.NodeCount;
            UiDraw.LinkRow(s, on, ix, top + MostLines * LineRow + UiTokens.S2, iw, UiTokens.Control, more, LinkCommand, tab);
            return s.NodeCount > first ? first : -1;
        }

        // A mood: its arrow and share in good or bad's colour, then its cause.
        private static void MoodLine(UiScene s, UiAt at, ref UiPersonaLine line, float x, float y, float w)
        {
            float g = UiTokens.IconSmall, fw = UiDraw.Snap(UiTextRules.Width("+100%", UiStyle.BodyStrong) + UiTokens.S1);
            bool up = line.Sign >= 0;
            UiDraw.Icon(s, at, x, y + (LineRow - g) / 2f, g, up ? UiIcon.TrendUp : UiIcon.TrendDown, up ? UiRole.Ok : UiRole.Danger, UiFlags.Middle);
            UiDraw.TextMiddle(s, at, x + g + UiTokens.S2, y, LineRow, fw, UiStyle.BodyStrong, up ? UiRole.OkText : UiRole.DangerText, line.Figure, UiFlags.Right);
            float tx = x + g + UiTokens.S2 + fw + UiTokens.S4;
            UiDraw.TextMiddle(s, at, tx, y, LineRow, x + w - tx, UiStyle.Body, UiRole.Text, line.Words, UiFlags.Truncate);
        }

        // A goal on its way: its glyph on a tile, the goal, and its tiers as a bar with the tier reached.
        private static void GoalLine(UiScene s, UiAt at, ref UiPersonaLine line, int steps, float x, float y, float w)
        {
            float tile = UiTokens.ControlSmall;
            int t = UiDraw.Tile(s, at, x, y + (LineRow - tile) / 2f, tile, UiRole.None, false);
            float g = UiTokens.Icon;
            UiDraw.Icon(s, at.In(t), x + (tile - g) / 2f, y + (LineRow - g) / 2f, g, line.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float bw = UiDataCells.StepsWidth(steps);
            float tx = x + tile + UiTokens.S3;
            UiDraw.TextMiddle(s, at, tx, y, LineRow, x + w - bw - UiTokens.S4 - tx, UiStyle.Body, UiRole.Text, line.Words, UiFlags.Truncate);
            UiDataCells.Steps(s, at, line.Figure, line.Reached, line.Share, steps, x + w - bw, y, bw, LineRow, false);
        }

        // The worth's split on its band: the donut of what is owned and beside it the legend with each part's value, and what
        // is owed under them; the donut's words while nothing is owned yet.
        private static void Wealth(UiScene s, UiAt at, UiPersonaModel m, float x, float y, float w, float h)
        {
            UiAt on = Band(s, at, x, y, w, h, m.WealthTitle, out float top);
            float bh = y + h - UiTokens.S4 - top;
            float size = UiDraw.SnapDown(Math.Min(DonutMost, Math.Min(bh, w * 0.42f)));
            // The donut and its legend as one group in the middle of the band, as a game's info view sets them.
            float lw = UiDraw.SnapDown(Math.Min(LegendMost, w - UiTokens.S8 * 3f - size));
            float dx = x + UiDraw.SnapDown((w - size - UiTokens.S8 - lw) / 2f), dy = top + UiDraw.SnapDown((bh - size) / 2f);
            int n = Math.Min(m.WealthShares.Length, m.WealthLabels.Length);
            UiDraw.Donut(s, on, dx, dy, size, m.WealthShares, n);
            float lx = dx + size + UiTokens.S8;
            float legendH = n * LegendRow + (string.IsNullOrEmpty(m.LoansValue) ? 0f : UiTokens.S3 + UiTokens.PairRow);
            bool empty = n == 0 && !string.IsNullOrEmpty(m.WealthEmpty);
            float wordsH = empty ? UiDraw.Snap(UiTextRules.WrappedHeight(m.WealthEmpty, UiStyle.Small, lw, 3)) : 0f;
            float ly = top + UiDraw.SnapDown((bh - legendH - wordsH) / 2f);
            if (empty)
            {
                UiDraw.Text(s, on, lx, ly, lw, UiStyle.Small, UiRole.Text2, m.WealthEmpty, UiFlags.Label | UiFlags.Wrap, 3);
                ly += wordsH;
            }
            UiDraw.Legend(s, on, lx, ly, lw, m.WealthLabels, m.WealthValues, n);
            ly += n * LegendRow;
            if (!string.IsNullOrEmpty(m.LoansValue))
            {
                UiDraw.Box(s, on, lx, ly + UiTokens.S1, lw, UiTokens.Hairline, UiRole.Line, UiRole.None, 0f, UiFlags.Decor);
                UiDraw.InfoRow(s, on, lx, ly + UiTokens.S3, lw, m.LoansLabel, m.LoansValue, false, -1, 0, UiRole.DangerText);
            }
        }

        // The three needs on cards across, each its glyph on its tile, its name and figure and its graded meter; then what
        // lifts or lowers the happiness under its title, filling the room under them.
        private static void NeedsPage(UiScene s, UiAt at, UiPersonaModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            int n = m.NeedLabels.Length;
            float gap = UiTokens.S4, cw = n > 0 ? UiDraw.SnapDown((w - gap * (n - 1)) / n) : 0f;
            for (int i = 0; i < n; i++)
            {
                NeedCardAt(s, at, x + i * (cw + gap), y, cw, m.NeedIcons[i], m.NeedLabels[i], m.NeedValues[i]);
            }
            float ty = y + (n > 0 ? NeedCard + UiTokens.S6 : 0f);
            if (!string.IsNullOrEmpty(m.ListTitle))
            {
                UiDraw.Text(s, at, x, ty, w, UiStyle.SectionHeader, UiRole.Text, m.ListTitle, UiFlags.Label | UiFlags.Truncate);
                ty += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
            }
            ListPage(s, at, m, x, ty, w, y + h - ty, keys);
        }

        private static void NeedCardAt(UiScene s, UiAt at, float x, float y, float w, UiIcon icon, string label, int value)
        {
            int band = UiDraw.Box(s, at, x, y, w, NeedCard, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(band);
            float tile = UiTokens.IconTile, tx = x + UiTokens.S4, ty = y + (NeedCard - tile) / 2f;
            int t = UiDraw.Tile(s, on, tx, ty, tile, UiRole.None, false);
            float g = UiTokens.Icon + UiTokens.S1;
            UiDraw.Icon(s, on.In(t), tx + (tile - g) / 2f, ty + (tile - g) / 2f, g, icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float ix = tx + tile + UiTokens.S4, iw = x + w - UiTokens.S4 - ix;
            UiRole grade = UiTokens.Grade(value);
            string figure = Math.Max(0, Math.Min(100, value)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            float fw = UiTextRules.Width("100", UiStyle.BodyStrong) + UiTokens.S1;
            float lh = UiTokens.PairRow, top = y + UiDraw.SnapDown((NeedCard - lh - UiTokens.S2 - UiTokens.S2) / 2f);
            UiDraw.TextMiddle(s, on, ix, top, lh, iw - fw - UiTokens.S2, UiStyle.Title, UiRole.Text, label, UiFlags.Label | UiFlags.Truncate);
            UiDraw.TextMiddle(s, on, ix + iw - fw, top, lh, fw, UiStyle.BodyStrong, grade, figure, UiFlags.Right);
            UiDraw.Meter(s, on, ix, top + lh + UiTokens.S2, iw, UiTokens.S2, value, grade);
        }

        // A tab's list as the kit's big table filling the room; with nothing in it, its empty words alone in that room, with
        // no column heads over nothing.
        private static void ListPage(UiScene s, UiAt at, UiPersonaModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            if (m.Table.RowCount == 0)
            {
                UiDraw.EmptyState(s, at, x, y, w, h, m.Table.EmptyIcon, m.Table.EmptyTitle, m.Table.EmptyBody);
                return;
            }
            int first = s.NodeCount;
            UiDataTable.Lay(s, at, m.Table, x, y, w, h, keys);
            // A sortable head is the game's own sort: Up from the first row reaches it, and Down from it comes back.
            int head = -1, row = -1;
            for (int i = first; i < s.NodeCount; i++)
            {
                head = head < 0 && s.Nodes[i].Command == UiDataTable.First + UiDataTable.Head ? i : head;
                row = row < 0 && s.Nodes[i].Command == UiDataTable.First + UiDataTable.Row ? i : row;
            }
            if (head >= 0 && row >= 0)
            {
                s.Nodes[row].Up = head;
                int last = head;
                for (int i = first; i < s.NodeCount; i++)
                {
                    if (s.Nodes[i].Command == UiDataTable.First + UiDataTable.Head)
                    {
                        s.Nodes[i].Down = row;
                        last = i;
                    }
                }
                s.Nodes[last].Right = last;   // the last head is the row's end: the window's close is not the way on
            }
        }
    }
}
