using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a dashboard shows: its title and tabs, a row of big figures with their changes, a table or two and a side panel.</summary>
    public sealed class UiDashboardModel
    {
        public string Title;
        public string Subtitle;
        public UiRole SubtitleInk = UiRole.Text2;   // the line under the title: a page's next step takes its weight's colour
        public string[] Tabs = new string[0];
        public int Tab;
        public UiFigureModel[] Figures = new UiFigureModel[0];
        public int FigureCount;
        public string TableTitle;
        public UiTableModel Table = new UiTableModel();
        public UiTableModel More;             // a second table under the first, its columns naming it, or null
        public string SideTitle;
        public UiPairModel[] Side = new UiPairModel[0];
        public int SideCount;
        public string StatsTitle;
        public UiStatModel[] Stats = new UiStatModel[0];
        public int StatCount;
        public bool StatsFirst;               // the bars before the pairs in the side panel
        public UiActionModel Action;          // the page's one primary action, drawn where tabs would be
        public bool HasAction;
        public UiActionModel[] Others = new UiActionModel[0];   // the page's other actions, at the primary's left
        public int OtherCount;
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }

    /// <summary>
    /// The dashboard template, for figures over time (the Economy app's city, district and population views, a tech firm's
    /// page): the title and its tabs or actions over a row of framed figure panels (the number large, its change with an
    /// arrow that follows its sign and a colour that says whether it is good for the player, a sparkline under it or, in a
    /// wide panel, beside it), then the table that stays readable at the left and a side panel of key figures, shares and
    /// bars at the right. Each frame fits what it holds: the table's its whole rows (scrolling when they outnumber the room,
    /// breathing into calmer rows when a short table would stop well over the page's foot), the side's the lines that fit,
    /// and the two share a foot when they nearly do. A second table stands under the first and takes the room the first
    /// leaves in whole rows, a long list that fills the column without a stretched row. Commands: <see cref="TabCommand"/>
    /// (the tab's index), <see cref="RowCommand"/> and <see cref="MoreCommand"/> (a row's), <see cref="ActionCommand"/> and
    /// <see cref="OtherCommand"/> (the primary, or another action's index). Game-free. Cost: a layout a change.
    /// </summary>
    public static class UiDashboard
    {
        public const int TabCommand = 1, RowCommand = 2, ActionCommand = 3, OtherCommand = 4, MoreCommand = 5;
        public const float SideWidth = 380f;     // px
        public const int MostFigures = 4;        // places in the figures' row: more and they stop being glanceable
        public const int MoreLeast = 3;          // rows the second table keeps when the first runs long

        /// <summary>Lays a dashboard out into a scene.</summary>
        public static void Lay(UiScene s, UiDashboardModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("dashboard", frame.Width, frame.Height, dark);
            frame.Content(out float cx, out float cy, out float cw, out float ch);
            UiAt screen = UiAt.Screen;
            int bg = UiDraw.Box(s, screen, 0f, 0f, frame.Width, frame.Height, UiRole.Bg, UiRole.None, 0f, UiFlags.Decor);   // the host's whole area
            UiAt at = screen.In(bg);
            float y = cy;
            float headW = cw * 0.5f;
            if (m.Tabs.Length > 0)
            {
                float tabsW = TabsWidth(m.Tabs, keys);
                UiDraw.Tabs(s, at, cx + cw - tabsW, y, tabsW, m.Tabs, m.Tab, keys, TabCommand);
            }
            else if (m.HasAction || m.OtherCount > 0)
            {
                headW = cw - Actions(s, at, m, cx + cw, y, keys) - UiTokens.S8;
            }
            float headH = UiDraw.PageHead(s, at, cx, y, headW, m.Title, m.Subtitle, m.SubtitleInk);
            y += headH + UiTokens.S6;
            y = FigureRow(s, at, m, cx, y, cw);
            float bodyH = UiDraw.SnapDown(cy + ch - y);
            float tw = cw - SideWidth - UiTokens.S4;
            float sideH = SideFit(m, bodyH, out int pairs, out int stats);
            int foot;
            float leftH;
            if (m.More != null && m.More.Columns.Length > 0)
            {
                Tables(s, at, m, cx, y, tw, bodyH, out foot, out leftH);
            }
            else
            {
                // A table whose columns name it needs no title over it: the head row is the frame's first line then.
                bool titled = !string.IsNullOrEmpty(m.TableTitle);
                float tableTop = titled ? UiTokens.S5 + UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionTitle)) + UiTokens.S3 : UiTokens.S4;
                float room = bodyH - tableTop - UiTokens.S4;
                m.Table.RowHeight = 0f;
                if (Math.Abs(tableTop + UiDraw.TableHeight(m.Table, room) + UiTokens.S4 - sideH) > UiTokens.MostSlack)
                {
                    UiDraw.Breathe(m.Table, room);   // a table level with its side keeps that one foot, and one well short of the page grows into it
                }
                float tableH = UiDraw.TableHeight(m.Table, room);
                leftH = tableTop + tableH + UiTokens.S4;
                foot = UiDraw.Box(s, at, cx, y, tw, leftH, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
                UiAt inTable = at.In(foot);
                if (titled)
                {
                    UiDraw.PanelTitle(s, inTable, cx + UiTokens.S5, y + UiTokens.S5, tw - UiTokens.S5 * 2f, m.TableTitle, null);
                }
                UiDraw.Table(s, inTable, cx + UiTokens.S2, y + tableTop, tw - UiTokens.S4, tableH, m.Table, RowCommand);
            }
            // Nearly level: one foot reads tidier than a step of a few px. Two tables level only by what their padding and the
            // step together keep within the slack a frame may hold.
            float level = m.More != null && m.More.Columns.Length > 0 ? UiTokens.MostSlack - UiTokens.S6 : UiTokens.MostSlack;
            if (Math.Abs(leftH - sideH) <= level)
            {
                s.Items[foot].H += Math.Max(0f, sideH - leftH);
                sideH = Math.Max(sideH, leftH);
            }
            Side(s, at, m, cx + tw + UiTokens.S4, y, sideH, pairs, stats);
            for (int i = 0; i < m.PromptCount; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, 0f, frame.BandY, frame.Width, frame.Band, keys);
            UiDraw.Chrome(s, frame);
        }

        // The page's actions at the head's right, the primary at the far right and the others at its left. Returns their width.
        private static float Actions(UiScene s, UiAt at, UiDashboardModel m, float right, float y, IUiKeys keys)
        {
            float bx = right;
            if (m.HasAction)
            {
                bx -= UiDraw.ButtonWidth(m.Action.Kind, m.Action.Label, m.Action.Icon, m.Action.Key, keys);
                UiDraw.Button(s, at, bx, y, right - bx, UiTokens.Control, m.Action.Kind, m.Action.Label, m.Action.Icon, m.Action.Key, keys, ActionCommand, 0, m.Action.Disabled);
                bx -= UiTokens.S3;
            }
            for (int i = m.OtherCount - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.Others[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                bx -= bw;
                UiDraw.Button(s, at, bx, y, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, OtherCommand, i, a.Disabled);
                bx -= UiTokens.S3;
            }
            return right - bx - UiTokens.S3;
        }

        // The figures' row: each in its places of the row's four, the last one flush with the right. Returns the y under it.
        private static float FigureRow(UiScene s, UiAt at, UiDashboardModel m, float x, float y, float w)
        {
            int count = Math.Min(m.FigureCount, MostFigures), places = 0;
            for (int i = 0; i < count; i++)
            {
                places += Span(ref m.Figures[i]);
            }
            if (count == 0)
            {
                return y;
            }
            float slot = UiDraw.SnapDown((w - UiTokens.S4 * (places - 1)) / places);
            float fh = FigureHeight(m), fx = x;
            for (int i = 0; i < count; i++)
            {
                int span = Span(ref m.Figures[i]);
                float fw = i == count - 1 ? x + w - fx : span * slot + (span - 1) * UiTokens.S4;
                int panel = UiDraw.Box(s, at, fx, y, fw, fh, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
                UiAt inside = at.In(panel).Arriving(i);
                UiDraw.Figure(s, inside, fx + UiTokens.S5, y + UiTokens.S5, fw - UiTokens.S5 * 2f, ref m.Figures[i], true, span > 1);
                fx += fw + UiTokens.S4;
            }
            return y + fh + UiTokens.S4;
        }

        // Two framed tables down the left: the first as tall as its rows (scrolling once they would push the second under
        // its least), the second taking what the first leaves in whole rows. Gives the lower frame and the column's height.
        private static void Tables(UiScene s, UiAt at, UiDashboardModel m, float x, float y, float w, float most, out int foot, out float height)
        {
            float top = UiTokens.S4;
            m.Table.RowHeight = 0f;
            m.More.RowHeight = 0f;
            float least = top + UiTokens.ControlSmall + MoreLeast * UiTokens.TableRow + UiTokens.S4;
            float firstH = UiDraw.TableHeight(m.Table, most - UiTokens.S4 - least - top - UiTokens.S4);
            float upper = top + firstH + UiTokens.S4;
            int first = UiDraw.Box(s, at, x, y, w, upper, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
            UiDraw.Table(s, at.In(first), x + UiTokens.S2, y + top, w - UiTokens.S4, firstH, m.Table, RowCommand);
            float y2 = y + upper + UiTokens.S4;
            float secondH = UiDraw.TableHeight(m.More, most - upper - UiTokens.S4 - top - UiTokens.S4);
            float lower = top + secondH + UiTokens.S4;
            foot = UiDraw.Box(s, at, x, y2, w, lower, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
            UiDraw.Table(s, at.In(foot), x + UiTokens.S2, y2 + top, w - UiTokens.S4, secondH, m.More, MoreCommand);
            height = upper + UiTokens.S4 + lower;
        }

        // The side panel with the pairs and bars that fit, in the model's order.
        private static void Side(UiScene s, UiAt at, UiDashboardModel m, float sx, float y, float sideH, int pairs, int stats)
        {
            int side = UiDraw.Box(s, at, sx, y, SideWidth, sideH, UiRole.Panel, UiRole.PanelEdge, UiTokens.RadiusPanel, UiFlags.Panel);
            UiAt inSide = at.In(side);
            float iy = y + UiTokens.S5, iw = SideWidth - UiTokens.S5 * 2f;
            bool first = true;
            for (int block = 0; block < 2; block++)
            {
                bool bars = (block == 0) == m.StatsFirst;
                int n = bars ? stats : pairs;
                if (n == 0)
                {
                    continue;
                }
                iy += first ? 0f : UiTokens.S6;
                first = false;
                UiDraw.Text(s, inSide, sx + UiTokens.S5, iy, iw, UiStyle.SectionHeader, UiRole.Muted, bars ? m.StatsTitle : m.SideTitle, UiFlags.Label);
                iy += UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader)) + (bars ? UiTokens.S3 : UiTokens.S2);
                for (int i = 0; i < n; i++)
                {
                    iy += bars ? UiDraw.Snap(UiDraw.StatBar(s, inSide.Arriving(i), sx + UiTokens.S5, iy, iw, ref m.Stats[i])) + (i < n - 1 ? UiTokens.S4 : 0f)
                        : UiDraw.KeyValue(s, inSide, sx + UiTokens.S5, iy, iw, m.Side[i].Key, m.Side[i].Value);
                }
            }
        }

        // How many of the side's pairs and bars fit a height, the first block before the second, and the height they take
        // with the panel's padding: a block stops at its last whole line, so nothing spills from the frame.
        private static float SideFit(UiDashboardModel m, float most, out int pairs, out int stats)
        {
            float head = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            float bar = UiDraw.Snap(UiDraw.Snap(UiTextRules.LineHeight(UiStyle.Small)) + UiTokens.S2 + UiTokens.Segment);
            float h = UiTokens.S5;
            pairs = stats = 0;
            bool first = true;
            for (int block = 0; block < 2; block++)
            {
                bool bars = (block == 0) == m.StatsFirst;
                int n = bars ? m.StatCount : m.SideCount;
                if (n == 0)
                {
                    continue;
                }
                float start = h + (first ? 0f : UiTokens.S6) + head + (bars ? UiTokens.S3 : UiTokens.S2);
                float each = bars ? bar + UiTokens.S4 : UiTokens.PairRow, trail = bars ? UiTokens.S4 : 0f;
                int fit = 0;
                while (fit < n && start + (fit + 1) * each - trail + UiTokens.S5 <= most)
                {
                    fit++;
                }
                if (fit == 0)
                {
                    break;
                }
                h = start + fit * each - trail;
                if (bars)
                {
                    stats = fit;
                }
                else
                {
                    pairs = fit;
                }
                first = false;
            }
            return h + UiTokens.S5;
        }

        private static int Span(ref UiFigureModel figure) => figure.Span > 1 ? figure.Span : 1;

        // The row's height: a change's line when any figure has one, and a sparkline's when one stands under its number.
        private static float FigureHeight(UiDashboardModel m)
        {
            bool trend = false, change = false;
            for (int i = 0; i < m.FigureCount && i < MostFigures; i++)
            {
                trend |= m.Figures[i].Trend != null && m.Figures[i].Trend.Length > 1 && Span(ref m.Figures[i]) == 1;
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
