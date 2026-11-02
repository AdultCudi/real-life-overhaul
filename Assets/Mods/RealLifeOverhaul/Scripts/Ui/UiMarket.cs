using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What Market Insider shows: the districts down a sidebar, the chosen one's head (its people by income as a donut, its
    /// buildings' prices as rows, the way to it on the map), its market as the big table (demand, or real estate with the
    /// for sale switch), its events as dated lines, an open filter's list, and the prompts.
    /// </summary>
    public sealed class UiMarketModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.ChartLineUp;
        public UiNavEntry[] Districts = new UiNavEntry[0];
        public int District;
        public string CitizenTitle;
        public string[] ClassLabels = new string[0];
        public string[] ClassValues = new string[0];
        public float[] ClassShares = new float[0];
        public string EstateTitle;
        public string[] EstateLabels = new string[0];
        public string[] EstateValues = new string[0];
        public string MapLink;                     // the way to the district on the city map
        public string[] TableTabs = new string[0];
        public int TableTab;
        public string SwitchLabel;                 // for sale only, beside the tabs on the real estate's; null for none
        public bool SwitchOn;
        public UiDataTableModel Table = new UiDataTableModel();
        public UiEventListModel Events = new UiEventListModel();
        public int OpenList = -1;                  // the filter whose list is open, or -1
        public string[] Options = new string[0];
        public int OptionChosen = -1;
        public float OptionOffset;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public float TableX, TableY;               // written back: where the table's tool row stood
    }

    /// <summary>
    /// Market Insider, after Cities: Skylines II's demand view for a business owner: an app window with the city's districts
    /// down its sidebar, each its glyph in the district's colour, and for the one chosen a head of its key figures (who
    /// lives there by income as a donut, the buildings and their average prices, the way to it on the map), its market as
    /// the kit's big table (each product's demand as a graded meter with the events' arrow and the import price graded,
    /// or its real estate with the for sale switch, each row opening the building), and its events as dated lines beside
    /// it. Commands: the window's (<see cref="UiAppWindow.NavCommand"/> a district), <see cref="MapCommand"/>,
    /// <see cref="TableTabCommand"/>, <see cref="SwitchCommand"/>, <see cref="EventCommand"/>, <see cref="OptionCommand"/>
    /// (an open list's option) and the big table's from <see cref="UiDataTable.First"/>. Game-free. Cost: a layout a
    /// change, the rows in view only.
    /// </summary>
    public static class UiMarket
    {
        public const int MapCommand = 230, TableTabCommand = 231, SwitchCommand = 232, EventCommand = 233, OptionCommand = 234;
        public const int Demands = 0, RealEstate = 1, Events = 2;
        private const float CitizenShare = 0.36f;   // of the page's width, the people's donut beside the buildings' prices
        private const float DonutMost = 120f;       // px: the people's donut
        private const float LegendRow = 20f;        // px: a legend's row, as UiDraw.Legend draws it

        public static void Lay(UiScene s, UiMarketModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("market insider", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, m.Icon, m.Districts, m.District, false, keys, out float x, out float y, out float w, out float bottom);
            float headH = Head(s, at, m, x, y, w);
            float ty = y + headH + UiTokens.S6;
            Market(s, at, m, x, ty, w, bottom - ty);
            OpenList(s, at, m);
            for (int i = 0; i < m.Prompts.Length; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // The district's key figures on two bands across: who lives there by income as a donut with its legend, and the
        // buildings with their average prices in two columns of rows under the way to it on the map. Returns the height.
        private static float Head(UiScene s, UiAt at, UiMarketModel m, float x, float y, float w)
        {
            int rows = (m.EstateLabels.Length + 1) / 2;
            float inner = Math.Max(Math.Max(m.ClassLabels.Length * LegendRow, rows * UiTokens.PairRow), DonutMost);
            float h = UiTokens.S3 + UiChart.HeadHeight + inner + UiTokens.S4;
            float gap = UiTokens.S6, cw = UiDraw.SnapDown((w - gap) * CitizenShare), ex = x + cw + gap, ew = x + w - ex;
            UiAt on = Block(s, at, x, y, cw, h, m.CitizenTitle, out float top);
            float size = Math.Min(DonutMost, inner);
            int n = Math.Min(m.ClassShares.Length, m.ClassLabels.Length);
            UiDraw.Donut(s, on, x + UiTokens.S5, top, size, m.ClassShares, n);
            float lx = x + UiTokens.S5 + size + UiTokens.S6;
            UiDraw.Legend(s, on, lx, top + UiDraw.SnapDown((size - n * LegendRow) / 2f), x + cw - UiTokens.S5 - lx, m.ClassLabels, m.ClassValues, n);
            float lw = string.IsNullOrEmpty(m.MapLink) ? 0f : UiDraw.Snap(UiTextRules.Width(m.MapLink, UiStyle.Body) + UiTokens.IconSmall + UiTokens.S3);
            UiAt onEstate = Block(s, at, ex, y, ew, h, m.EstateTitle, out float etop, lw > 0f ? lw + UiTokens.S4 : 0f);
            if (lw > 0f)
            {
                UiDraw.LinkRow(s, onEstate, ex + ew - UiTokens.S4 - lw, y + UiTokens.S3, lw, UiTokens.PairRow, m.MapLink, MapCommand, 0, false);
            }
            float colW = UiDraw.SnapDown((ew - UiTokens.S4 * 2f - UiTokens.S8) / 2f);
            for (int i = 0; i < m.EstateLabels.Length; i++)
            {
                float rx = ex + UiTokens.S4 + (i / Math.Max(1, rows)) * (colW + UiTokens.S8);
                UiDraw.InfoRow(s, onEstate, rx, etop + (i % Math.Max(1, rows)) * UiTokens.PairRow, colW, m.EstateLabels[i], i < m.EstateValues.Length ? m.EstateValues[i] : null);
            }
            return h;
        }

        // A band with its title in capitals at its top left, as a chart's head stands, leaving room at its right for a link;
        // returns where its rows go and their top.
        private static UiAt Block(UiScene s, UiAt at, float x, float y, float w, float h, string title, out float top, float right = 0f)
        {
            int band = UiDraw.Box(s, at, x, y, w, h, UiRole.Band, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt on = at.In(band);
            UiDraw.TextMiddle(s, on, x + UiTokens.S4, y + UiTokens.S3, UiTokens.PairRow, w - UiTokens.S4 * 2f - right, UiStyle.SectionHeader, UiRole.Text, title,
                UiFlags.Label | UiFlags.Truncate);
            top = y + UiTokens.S3 + UiChart.HeadHeight;
            return on;
        }

        // The market: its demand, its real estate and its events as tabs (the for sale switch beside them on the real
        // estate's), then the big table or the dated events filling the room.
        private static void Market(UiScene s, UiAt at, UiMarketModel m, float x, float y, float w, float h)
        {
            float tabsH = UiDraw.Tabs(s, at, x, y, w, m.TableTabs, m.TableTab, null, TableTabCommand);
            if (!string.IsNullOrEmpty(m.SwitchLabel))
            {
                float sw = UiDraw.SnapDown(Math.Min(w / 2f, UiTextRules.Width(m.SwitchLabel, UiStyle.Body) + UiTokens.S16 * 2f));
                UiDraw.Switch(s, at, x + w - sw, y, sw, m.SwitchOn, m.SwitchLabel, SwitchCommand, 0, false);
            }
            float ty = y + tabsH + UiTokens.S3;
            m.TableX = x;
            m.TableY = ty;
            if (m.TableTab == Events)
            {
                UiEventList.Lay(s, at, m.Events, x, ty, w, y + h - ty, EventCommand);
                return;
            }
            UiDataTable.Lay(s, at, m.Table, x, ty, w, y + h - ty, null);
        }

        private static void OpenList(UiScene s, UiAt at, UiMarketModel m)
        {
            if (m.TableTab == Events || m.OpenList < 0 || m.OpenList >= m.Table.Filters.Length || m.Options.Length == 0)
            {
                return;
            }
            float fx = UiDataTable.FilterLeft(m.Table, m.OpenList, m.TableX);
            float fw = Math.Max(UiDataTable.FilterWidthOf(m.Table.Filters[m.OpenList]), UiTokens.S16 * 4f);
            UiDraw.ChoiceList(s, at, fx, m.TableY + UiTokens.Control + UiTokens.S1, fw, m.Options, m.Options.Length, m.OptionChosen, m.OptionOffset, OptionCommand);
        }
    }
}
