using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What an info view shows: its headline rows, a donut of shares with its legend, a map legend, and the other views beside it.</summary>
    public sealed class UiInfoViewModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.People;
        public UiIcon[] Views = new UiIcon[0];     // the info views a player can switch between, as a game's menu of them
        public UiRole[] ViewTints = new UiRole[0];
        public int View;
        public string HeadLabel;                   // the first row's label in capitals ("Population") and its figure
        public string HeadValue;
        public UiInfoRow[] Rows = new UiInfoRow[0];
        public string ChartTitle;                  // "Age groups"
        public string[] ChartLabels = new string[0];
        public string[] ChartValues = new string[0];
        public float[] ChartShares = new float[0];
        public string LegendTitle;                 // "Map legend"
        public string ScaleLabel;                  // what the city is coloured by ("Rent by block")
        public string ScaleLow, ScaleHigh;
        public UiRole[] ScaleSteps;                // the map's colours low to high, as the game file tints the city; null for the kit's five
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The info view, after Cities: Skylines II's population view (design/DIRECTION_CS2.md, section 4.2): a menu of the
    /// views as tinted icons, and beside it a panel at the left holding the headline rows, a donut with its coloured legend
    /// and the map's legend, while the city colours by that view where the mod can tint it. Commands:
    /// <see cref="ViewCommand"/> (a view's index). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiInfoView
    {
        public const int ViewCommand = 1;
        public const float Width = 400f;       // px: the panel
        private const float Menu = 104f;       // px: the views' menu, two icons across
        private const float Cell = 44f;        // px: a view's icon button
        private const float DonutSize = 112f;  // px

        private static readonly UiRole[] Steps = { UiRole.Chart5, UiRole.Chart1, UiRole.Chart3, UiRole.Chart4, UiRole.Chart2 };

        public static void Lay(UiScene s, UiInfoViewModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("info view", frame.Width, frame.Height, dark);
            s.Pointer = frame.Pointer;
            s.Floats = true;
            float x = UiTokens.S2, y = UiTokens.S4;
            float px = x;
            if (m.Views.Length > 0)
            {
                ViewsMenu(s, m, x, y);
                px = x + Menu + UiTokens.S2;
            }
            float h = Height(m);
            int panel = UiDraw.Box(s, UiAt.Screen, px, y, Width, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = UiAt.Screen.In(panel);
            float cy = UiDraw.TitleStrip(s, at, px, y, Width, m.Icon, m.Title, true);
            float ix = px + UiTokens.S4, iw = Width - UiTokens.S4 * 2f;
            cy += UiTokens.S2;
            if (!string.IsNullOrEmpty(m.HeadLabel))
            {
                cy += UiDraw.SectionRow(s, at, ix, cy, iw, m.HeadLabel, m.HeadValue, false, -1, 0);
            }
            for (int i = 0; i < m.Rows.Length; i++)
            {
                cy += UiDraw.InfoRow(s, at, ix, cy, iw, m.Rows[i].Label, m.Rows[i].Value, false, -1, 0, m.Rows[i].Ink == UiRole.None ? UiRole.Text : m.Rows[i].Ink);
            }
            cy += UiTokens.S2;
            if (m.ChartShares.Length > 0)
            {
                float bh = ChartHeight(m);
                UiDraw.Band(s, at, px, cy, Width, bh);
                float by = cy + UiTokens.S3;
                UiDraw.Text(s, at, ix, by, iw, UiStyle.SectionHeader, UiRole.Text, m.ChartTitle, UiFlags.Label | UiFlags.Truncate);
                by += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
                UiDraw.Donut(s, at, ix, by, DonutSize, m.ChartShares, m.ChartShares.Length);
                float lx = ix + DonutSize + UiTokens.S6, lw = ix + iw - lx;
                float lh = m.ChartLabels.Length * 20f;
                UiDraw.Legend(s, at, lx, by + (DonutSize - lh) / 2f, lw, m.ChartLabels, m.ChartValues, m.ChartLabels.Length);
                cy += bh + UiTokens.S1;
            }
            if (!string.IsNullOrEmpty(m.LegendTitle))
            {
                float by = cy + UiTokens.S3;
                UiDraw.Text(s, at, ix, by, iw, UiStyle.SectionHeader, UiRole.Text, m.LegendTitle, UiFlags.Label | UiFlags.Truncate);
                by += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
                UiDraw.Text(s, at, ix, by, iw, UiStyle.Body, UiRole.Text2, m.ScaleLabel, UiFlags.Label | UiFlags.Truncate);
                by += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)) + UiTokens.S2;
                UiDraw.Scale(s, at, ix, by, iw, m.ScaleLow, m.ScaleHigh, m.ScaleSteps ?? Steps);
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        /// <summary>The panel's height: its strip, its rows, its chart and its legend.</summary>
        public static float Height(UiInfoViewModel m)
        {
            float h = UiFrame.Strip + UiTokens.S2 + (string.IsNullOrEmpty(m.HeadLabel) ? 0f : UiTokens.PairRow) + m.Rows.Length * UiTokens.PairRow + UiTokens.S2;
            if (m.ChartShares.Length > 0)
            {
                h += ChartHeight(m) + UiTokens.S1;
            }
            if (!string.IsNullOrEmpty(m.LegendTitle))
            {
                h += UiTokens.S3 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Body)) + UiTokens.S2
                     + UiDraw.Snap(UiTokens.S2 + UiTokens.S1 + UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S4;
            }
            return UiDraw.Snap(h);
        }

        private static float ChartHeight(UiInfoViewModel m) =>
            UiTokens.S3 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3 + Math.Max(DonutSize, m.ChartLabels.Length * 20f) + UiTokens.S4;

        // The views as tinted icons two across, the shown one filled cyan, as a game's menu of info views.
        private static void ViewsMenu(UiScene s, UiInfoViewModel m, float x, float y)
        {
            int rows = (m.Views.Length + 1) / 2;
            float h = rows * Cell + (rows - 1) * UiTokens.S1 + UiTokens.S2 * 2f;
            int menu = UiDraw.Box(s, UiAt.Screen, x, y, Menu, h, UiRole.Panel, UiRole.None, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt at = UiAt.Screen.In(menu);
            for (int i = 0; i < m.Views.Length; i++)
            {
                float cx = x + UiTokens.S2 + (i % 2) * (Cell + UiTokens.S1), cy = y + UiTokens.S2 + (i / 2) * (Cell + UiTokens.S1);
                UiRole tint = i < m.ViewTints.Length ? m.ViewTints[i] : UiRole.None;
                int node = UiDraw.IconButton(s, at, cx, cy, Cell, m.Views[i], tint, ViewCommand, i, i == m.View);
                if (i == m.View)
                {
                    s.FocusFirst = node;
                }
            }
        }
    }
}
