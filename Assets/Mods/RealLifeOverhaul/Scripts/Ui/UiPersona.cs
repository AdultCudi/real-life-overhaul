using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A row of the profile's summary that leads to a tab: what it is, where it stands, and the tab it opens.</summary>
    public struct UiPersonaLink
    {
        public string Label;
        public string Value;
        public int Tab;
    }

    /// <summary>
    /// What the player's own profile shows: the app's tabs across, the head (the face in the ring of their mood, their
    /// name, age, businesses and staff, their worth), and the tab's page: the wealth's split as a donut beside the weekly
    /// income's line, the needs and the way to the rest (the overview); the needs as graded meters over what moves the
    /// happiness (the needs); the courses (education); the goals with their tiers (goals).
    /// </summary>
    public sealed class UiPersonaModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.IdCard;
        public UiNavEntry[] Tabs = new UiNavEntry[0];
        public int Tab;
        public UiProfileModel Profile = new UiProfileModel();
        public string WealthTitle;
        public string[] WealthLabels = new string[0];
        public string[] WealthValues = new string[0];
        public float[] WealthShares = new float[0];
        public string LoansLabel, LoansValue;      // what is owed, under the parts it is taken from; null for none
        public string WealthEmpty;                 // the donut's words while nothing is owned yet
        public UiChartModel Income = new UiChartModel();
        public string NeedsTitle;
        public UiIcon[] NeedIcons = new UiIcon[0];
        public string[] NeedLabels = new string[0];
        public int[] NeedValues = new int[0];      // 0 to 100
        public string LinksTitle;
        public UiPersonaLink[] Links = new UiPersonaLink[0];
        public string ListTitle;                   // the tab's list: its title in capitals and how far along at the right
        public string ListCount;
        public UiDataTableModel Table = new UiDataTableModel();
        public UiPrompt[] Prompts = new UiPrompt[0];
    }

    /// <summary>
    /// The player's profile in the Persona app, after Football Manager's player profile in the Cities: Skylines II look:
    /// an app window with its tabs across and the profile head over every tab. The overview is the player at a glance:
    /// the worth's split as a donut with its legend and the debt under it, the weekly income over the last days as a
    /// line, the three needs graded and the way to the courses, the goals and what moves the happiness. The needs tab
    /// puts the three needs on cards with their glyphs over the table of what lifts or lowers the happiness, each with
    /// its arrow, its sign and how long it lasts; the education and goals tabs are the kit's big table, the goals with
    /// their tiers as pips. Commands: the window's (<see cref="UiAppWindow.NavCommand"/> a tab), <see cref="LinkCommand"/>
    /// (a summary row: its tab), the big table's from <see cref="UiDataTable.First"/> and the chart's from
    /// <see cref="UiChart.First"/>. Game-free. Cost: a layout a change, the table's rows in view only.
    /// </summary>
    public static class UiPersona
    {
        public const int Overview = 0, Needs = 1, Education = 2, Goals = 3;
        public const int LinkCommand = 210;
        private const float DonutMost = 256f;     // px: the wealth's donut at its largest
        private const float LegendMost = 360f;    // px: the legend's width, so each part's value stays by its name
        private const float NeedCard = 72f;       // px: a need's card, its tile and its two lines
        private const float LegendRow = 20f;      // px: a legend's row, as UiDraw.Legend draws it

        public static void Lay(UiScene s, UiPersonaModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("persona", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, m.Icon, m.Tabs, m.Tab, true, keys, out float x, out float y, out float w, out float bottom);
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
                    OverviewPage(s, at, m, x, y, w, bottom - y);
                    break;
            }
            for (int i = 0; i < m.Prompts.Length; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // Two columns of two rows, each a band with its title inside as the chart's: the wealth beside the income's line, then
        // the needs beside the way to the rest. The first row takes the room the second leaves, so the page meets the foot.
        private static void OverviewPage(UiScene s, UiAt at, UiPersonaModel m, float x, float y, float w, float h)
        {
            float gap = UiTokens.S6, cw = UiDraw.SnapDown((w - gap) / 2f), rx = x + cw + gap;
            int rows = Math.Max(m.NeedLabels.Length, m.Links.Length);
            float lowH = UiTokens.S3 + UiChart.HeadHeight + rows * UiTokens.PairRow + UiTokens.S4;
            float topH = UiDraw.SnapDown(h - lowH - gap);
            Wealth(s, at, m, x, y, cw, topH);
            UiChart.Lay(s, at, m.Income, rx, y, cw, topH);
            float ly = y + topH + gap;
            UiAt on = Block(s, at, x, ly, cw, lowH, m.NeedsTitle, out float ny);
            float inner = cw - UiTokens.S4 * 2f;
            float lw = UiDraw.LabelColumn(inner, m.NeedLabels);
            for (int i = 0; i < m.NeedLabels.Length; i++)
            {
                UiDraw.GradeBar(s, on, x + UiTokens.S4, ny + i * UiTokens.PairRow, inner, m.NeedLabels[i], m.NeedValues[i], lw);
            }
            UiAt onLinks = Block(s, at, rx, ly, cw, lowH, m.LinksTitle, out float ky);
            for (int i = 0; i < m.Links.Length; i++)
            {
                UiDraw.InfoRow(s, onLinks, rx + UiTokens.S4, ky + i * UiTokens.PairRow, inner, m.Links[i].Label, m.Links[i].Value, true, LinkCommand, m.Links[i].Tab);
            }
        }

        // A block of the overview: a band with its title in capitals at its top left, as a chart's head stands. Returns
        // where its rows go, and the y their first starts at.
        private static UiAt Block(UiScene s, UiAt at, float x, float y, float w, float h, string title, out float top)
        {
            int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(band);
            UiDraw.TextMiddle(s, on, x + UiTokens.S4, y + UiTokens.S3, UiTokens.PairRow, w - UiTokens.S4 * 2f, UiStyle.SectionHeader, UiRole.Text, title, UiFlags.Label | UiFlags.Truncate);
            top = y + UiTokens.S3 + UiChart.HeadHeight;
            return on;
        }

        // The worth's split on its band: the donut of what is owned and beside it the legend with each part's value, and what
        // is owed under them; the donut's words while nothing is owned yet.
        private static void Wealth(UiScene s, UiAt at, UiPersonaModel m, float x, float y, float w, float h)
        {
            UiAt on = Block(s, at, x, y, w, h, m.WealthTitle, out float top);
            float bh = y + h - UiTokens.S4 - top;
            float size = UiDraw.SnapDown(Math.Min(DonutMost, Math.Min(bh, w * 0.42f)));
            // The donut and its legend as one group in the middle of the band, as a game's info view sets them.
            float lw = UiDraw.SnapDown(Math.Min(LegendMost, w - UiTokens.S8 * 3f - size));
            float dx = x + UiDraw.SnapDown((w - size - UiTokens.S8 - lw) / 2f), dy = top + UiDraw.SnapDown((bh - size) / 2f);
            int n = Math.Min(m.WealthShares.Length, m.WealthLabels.Length);
            int slices = n;
            while (slices > 1 && m.WealthShares[slices - 1] <= 0f)
            {
                slices--;   // empty parts at the end are no slices: a lone part is a whole ring, with no seam cut in it
            }
            UiDraw.Donut(s, on, dx, dy, size, m.WealthShares, slices);
            float lx = dx + size + UiTokens.S8;
            float legendH = n * LegendRow + (string.IsNullOrEmpty(m.LoansValue) ? 0f : UiTokens.S3 + UiTokens.PairRow);
            bool empty = Sum(m.WealthShares, n) <= 0f && !string.IsNullOrEmpty(m.WealthEmpty);
            float wordsH = empty ? UiDraw.Snap(UiTextRules.WrappedHeight(m.WealthEmpty, UiStyle.Small, lw, 3)) + UiTokens.S3 : 0f;
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

        private static float Sum(float[] values, int n)
        {
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                sum += Math.Max(0f, values[i]);
            }
            return sum;
        }

        // The three needs on cards across, each its glyph on its tile, its name and figure and its graded meter; then what
        // lifts or lowers the happiness as the big table, filling the room under them.
        private static void NeedsPage(UiScene s, UiAt at, UiPersonaModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            int n = m.NeedLabels.Length;
            float gap = UiTokens.S4, cw = n > 0 ? UiDraw.SnapDown((w - gap * (n - 1)) / n) : 0f;
            for (int i = 0; i < n; i++)
            {
                NeedCardAt(s, at, x + i * (cw + gap), y, cw, m.NeedIcons[i], m.NeedLabels[i], m.NeedValues[i]);
            }
            float ty = y + (n > 0 ? NeedCard + UiTokens.S6 : 0f);
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

        // A tab of one list: its title and how far along at the right where it has one (the tab names it otherwise), then
        // the big table filling the room under them.
        private static void ListPage(UiScene s, UiAt at, UiPersonaModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            if (string.IsNullOrEmpty(m.ListTitle))
            {
                UiDataTable.Lay(s, at, m.Table, x, y, w, h, keys);
                return;
            }
            float headH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            float cw = string.IsNullOrEmpty(m.ListCount) ? 0f : UiDraw.Snap(UiTextRules.Width(m.ListCount, UiStyle.Small) + UiTokens.S1);
            UiDraw.Text(s, at, x, y, w - cw - UiTokens.S4, UiStyle.SectionHeader, UiRole.Text, m.ListTitle, UiFlags.Label | UiFlags.Truncate);
            if (cw > 0f)
            {
                UiDraw.Text(s, at, x + w - cw, y, cw, UiStyle.Small, UiRole.Text2, m.ListCount, UiFlags.Right);
            }
            float ty = y + headH + UiTokens.S3;
            UiDataTable.Lay(s, at, m.Table, x, ty, w, y + h - ty, keys);
        }
    }
}
