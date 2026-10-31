using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a dashboard shows: its title and tabs, a row of big figures with their changes, a table and a side panel.</summary>
    public sealed class UiDashboardModel
    {
        public string Title;
        public string Subtitle;
        public string[] Tabs = new string[0];
        public int Tab;
        public UiFigureModel[] Figures = new UiFigureModel[0];
        public int FigureCount;
        public string TableTitle;
        public UiTableModel Table = new UiTableModel();
        public string SideTitle;
        public UiPairModel[] Side = new UiPairModel[0];
        public int SideCount;
        public string StatsTitle;
        public UiStatModel[] Stats = new UiStatModel[0];
        public int StatCount;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The dashboard template, for figures over time (the Economy app's city, district and population views): the title
    /// and its tabs as bars over a row of framed figure panels (the number large, its change green up or red down, a
    /// sparkline under it), then the table that stays readable at the left and a side panel of key figures and shares at
    /// the right. Commands: <see cref="TabCommand"/> (the tab's index) and <see cref="RowCommand"/> (the row's). Game-free.
    /// Cost: a layout a change.
    /// </summary>
    public static class UiDashboard
    {
        public const int TabCommand = 1, RowCommand = 2;
        public const float SideWidth = 380f;     // px
        public const int MostFigures = 4;        // figures in the row: more and they stop being glanceable

        /// <summary>Lays a dashboard out into a scene.</summary>
        public static void Lay(UiScene s, UiDashboardModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("dashboard", frame.Width, frame.Height + frame.Band, dark);
            frame.Content(out float cx, out float cy, out float cw, out float ch);
            UiAt screen = UiAt.Screen;
            int bg = UiDraw.Box(s, screen, 0f, 0f, frame.Width, frame.Height, UiRole.Bg, UiRole.None, 0f, UiFlags.Decor);   // the host's whole area
            UiAt at = screen.In(bg);
            float y = cy;
            float titleH = UiTextRules.LineHeight(UiStyle.AppTitle);
            UiDraw.Text(s, at, cx, y, cw * 0.5f, UiStyle.SectionTitle, UiRole.Text, m.Title, UiFlags.Label | UiFlags.Truncate);
            if (!string.IsNullOrEmpty(m.Subtitle))
            {
                UiDraw.Text(s, at, cx, y + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionTitle)), cw * 0.5f, UiStyle.Small, UiRole.Text2, m.Subtitle, UiFlags.Label | UiFlags.Truncate);
            }
            if (m.Tabs.Length > 0)
            {
                float tabsW = TabsWidth(m.Tabs, keys);
                UiDraw.Tabs(s, at, cx + cw - tabsW, y, tabsW, m.Tabs, m.Tab, keys, TabCommand);
            }
            y += UiDraw.Snap(Math.Max(titleH, UiTokens.Control)) + UiTokens.S6;
            int count = Math.Min(m.FigureCount, MostFigures);
            float fh = 0f;
            if (count > 0)
            {
                float fw = UiDraw.SnapDown((cw - UiTokens.S4 * (count - 1)) / count);
                fh = FigureHeight(m);
                for (int i = 0; i < count; i++)
                {
                    float fx = cx + i * (fw + UiTokens.S4);
                    int panel = UiDraw.Box(s, at, fx, y, fw, fh, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel);
                    UiAt inside = at.In(panel).Arriving(i);
                    UiDraw.Figure(s, inside, fx + UiTokens.S5, y + UiTokens.S5, fw - UiTokens.S5 * 2f, ref m.Figures[i], true);
                }
                y += fh + UiTokens.S4;
            }
            float bodyH = UiDraw.SnapDown(cy + ch - y);
            float tw = cw - SideWidth - UiTokens.S4;
            int tablePanel = UiDraw.Box(s, at, cx, y, tw, bodyH, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel);
            UiAt inTable = at.In(tablePanel);
            float headH = UiDraw.PanelTitle(s, inTable, cx + UiTokens.S5, y + UiTokens.S5, tw - UiTokens.S5 * 2f, m.TableTitle, null);
            float tableY = y + UiTokens.S5 + headH + UiTokens.S3;
            UiDraw.Table(s, inTable, cx + UiTokens.S2, tableY, tw - UiTokens.S4, UiDraw.SnapDown(y + bodyH - UiTokens.S4 - tableY), m.Table, RowCommand);
            float sx = cx + tw + UiTokens.S4;
            int side = UiDraw.Box(s, at, sx, y, SideWidth, bodyH, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel);
            UiAt inSide = at.In(side);
            float iy = y + UiTokens.S5, iw = SideWidth - UiTokens.S5 * 2f;
            if (m.SideCount > 0)
            {
                UiDraw.Text(s, inSide, sx + UiTokens.S5, iy, iw, UiStyle.SectionHeader, UiRole.Muted, m.SideTitle, UiFlags.Label);
                iy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2;
                for (int i = 0; i < m.SideCount; i++)
                {
                    iy += UiDraw.KeyValue(s, inSide, sx + UiTokens.S5, iy, iw, m.Side[i].Key, m.Side[i].Value);
                }
                iy += UiTokens.S6;
            }
            if (m.StatCount > 0)
            {
                UiDraw.Text(s, inSide, sx + UiTokens.S5, iy, iw, UiStyle.SectionHeader, UiRole.Muted, m.StatsTitle, UiFlags.Label);
                iy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S3;
                for (int i = 0; i < m.StatCount && iy < y + bodyH - UiTokens.S12; i++)
                {
                    iy += UiDraw.Snap(UiDraw.StatBar(s, inSide.Arriving(i), sx + UiTokens.S5, iy, iw, ref m.Stats[i])) + UiTokens.S4;
                }
            }
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, 0f, frame.Height, frame.Width, frame.Band, keys);
        }

        private static float FigureHeight(UiDashboardModel m)
        {
            bool trend = false, change = false;
            for (int i = 0; i < m.FigureCount && i < MostFigures; i++)
            {
                trend |= m.Figures[i].Trend != null && m.Figures[i].Trend.Length > 1;
                change |= !string.IsNullOrEmpty(m.Figures[i].Change);
            }
            float h = UiTokens.S5 * 2f + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + UiTokens.S2 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Display));
            h += change ? UiDraw.Snap(Math.Max(UiTextRules.LineHeight(UiStyle.SmallStrong), UiTokens.Icon)) : 0f;
            h += trend ? UiTokens.S2 + UiTokens.S12 : 0f;
            return UiDraw.Snap(h);
        }

        private static float TabsWidth(string[] tabs, IUiKeys keys)
        {
            float w = keys != null ? UiDraw.KeyCapWidth(UiAction.TabPrev, keys) + UiTokens.S3 + UiTokens.S2 + UiDraw.KeyCapWidth(UiAction.TabNext, keys) : 0f;
            for (int i = 0; i < tabs.Length; i++)
            {
                w += UiDraw.Snap(UiTextRules.Width(tabs[i], UiStyle.Nav) + UiTokens.S4 * 2f) + UiTokens.S1;
            }
            return UiDraw.Snap(w);
        }
    }
}
