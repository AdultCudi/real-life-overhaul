using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A line of what a rival is doing against the player now (the game's rivalry marks): its glyph, its title and what it means.</summary>
    public struct UiRivalThreat
    {
        public UiIcon Icon;
        public string Title;
        public string Body;
    }

    /// <summary>
    /// What the Rivals app shows: its tabs across (the league, the chosen rival's businesses and buildings), the league's
    /// table beside the chosen rival's head, chart and rivalry, or the chosen one's head over one of their tables with the
    /// row's actions; and the prompts.
    /// </summary>
    public sealed class UiRivalsModel
    {
        public string Title;
        public UiIcon Icon = UiIcon.Flag;
        public UiNavEntry[] Tabs = new UiNavEntry[0];
        public int Tab;
        public string LeagueTitle;
        public UiProfileModel Profile = new UiProfileModel();
        public UiActionModel[] ProfileActions = new UiActionModel[0];   // Open in Contacts, on P, where the game shows it
        public string[] ChartTabs = new string[0];                      // the game's filters of its chart
        public int ChartTab;
        public UiChartModel Chart = new UiChartModel();
        public string ThreatsTitle;
        public UiRivalThreat[] Threats = new UiRivalThreat[0];
        public UiDataTableModel Table = new UiDataTableModel();
        public string RowName;                                           // the chosen row of a rival's table, over its actions
        public string RowLink;                                           // its locate link (Show on map); null where the game has none
        public UiActionModel[] RowActions = new UiActionModel[0];        // its actions at the right, the main one last on F
        public UiPrompt[] Prompts = new UiPrompt[0];
    }

    /// <summary>
    /// The Rivals app as a league you climb, in the Cities: Skylines II look: an app window with its tabs across. The league
    /// is the kit's big table (the rank, a face, the weekly income with its arrow, the businesses and the week's change
    /// graded) beside the chosen rival: their head (face, district behind its badge, a state, the key facts and stars by
    /// their place), the game's chart of their development with its filters as tabs, and what their rivalry is doing to
    /// the player now. Their businesses and buildings are tables with a tile a type, under the head, the chosen row's
    /// locate link and actions over them. Commands: the window's (<see cref="UiAppWindow.NavCommand"/> a tab,
    /// <see cref="UiAppWindow.ActionCommand"/> the head's action), <see cref="ChartTabCommand"/>, <see cref="RowActionCommand"/>,
    /// <see cref="RowLinkCommand"/>, the big table's from <see cref="UiDataTable.First"/> and the chart's from
    /// <see cref="UiChart.First"/>. Game-free. Cost: a layout a change, the table's rows in view only.
    /// </summary>
    public static class UiRivals
    {
        public const int League = 0, Businesses = 1, Buildings = 2;
        public const int ChartTabCommand = 220, RowActionCommand = 221, RowLinkCommand = 222;
        private const float LeagueShare = 0.48f;   // of the page's width, the league's table beside the chosen rival
        private const float ThreatRow = 48f;       // px: a rivalry's line, its tile and two lines of words

        public static void Lay(UiScene s, UiRivalsModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            s.Begin("rivals", frame.Width, frame.Height, dark);
            UiAt at = UiAppWindow.Shell(s, frame, m.Title, m.Icon, m.Tabs, m.Tab, true, keys, out float x, out float y, out float w, out float bottom);
            if (m.Tab == League)
            {
                LeaguePage(s, at, m, x, y, w, bottom - y, keys);
            }
            else
            {
                float ty = UiAppWindow.Profile(s, at, m.Profile, m.ProfileActions, x, y, w, keys);
                ty = RowBar(s, at, m, x, ty, w, keys);
                UiDataTable.Lay(s, at, m.Table, x, ty, w, bottom - ty, keys);
            }
            for (int i = 0; i < m.Prompts.Length; i++)
            {
                s.AddPrompt(m.Prompts[i].Action, m.Prompts[i].Label);
            }
            UiDraw.PromptBar(s, frame, keys);
        }

        // The league at the left under its title, the rivalry's lines under it; the chosen rival at the right: their head,
        // the chart's filters as tabs beside Open in Contacts, and the chart filling the column, so both columns end level.
        // An empty league is its empty words alone. The keys start on the chosen rival's row.
        private static void LeaguePage(UiScene s, UiAt at, UiRivalsModel m, float x, float y, float w, float h, IUiKeys keys)
        {
            if (m.Table.RowCount == 0)
            {
                UiDraw.EmptyState(s, at, x, y, w, h, m.Table.EmptyIcon, m.Table.EmptyTitle, m.Table.EmptyBody);
                return;
            }
            float gap = UiTokens.S8, lw = UiDraw.SnapDown((w - gap) * LeagueShare), rx = x + lw + gap, rw = x + w - rx;
            float headH = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            float threatsH = m.Threats.Length > 0 ? UiTokens.S6 + headH + UiTokens.S3 + m.Threats.Length * ThreatRow : 0f;
            UiDraw.Text(s, at, x, y, lw, UiStyle.SectionHeader, UiRole.Text, m.LeagueTitle, UiFlags.Label | UiFlags.Truncate);
            int firstLeague = s.NodeCount;
            float ty = y + headH + UiTokens.S3;
            UiDataTable.Lay(s, at, m.Table, x, ty, lw, y + h - threatsH - ty, keys);
            int lastLeague = s.NodeCount;
            if (m.Threats.Length > 0)
            {
                float my = y + h - threatsH + UiTokens.S6;
                UiDraw.Text(s, at, x, my, lw, UiStyle.SectionHeader, UiRole.Text, m.ThreatsTitle, UiFlags.Label | UiFlags.Truncate);
                my += headH + UiTokens.S3;
                for (int i = 0; i < m.Threats.Length; i++)
                {
                    Threat(s, at, ref m.Threats[i], x, my + i * ThreatRow, lw);
                }
            }
            int chosen = m.Table.Selected >= 0 ? s.FindNode(UiDataTable.First + UiDataTable.Row, m.Table.Selected) : -1;
            if (chosen >= 0)
            {
                s.FocusFirst = chosen;
            }
            float ry = UiAppWindow.Profile(s, at, m.Profile, null, rx, y, rw, keys);
            int firstRight = s.NodeCount;
            float right = rx + rw;
            for (int i = m.ProfileActions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.ProfileActions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                right -= bw;
                UiDraw.Button(s, at, right, ry, bw, UiTokens.Control, a.Kind, a.Label, a.Icon, a.Key, keys, UiAppWindow.ActionCommand, i, a.Disabled);
                right -= UiTokens.S3;
            }
            if (m.ChartTabs.Length > 1 || m.ProfileActions.Length > 0)
            {
                if (m.ChartTabs.Length > 1)
                {
                    UiDraw.Tabs(s, at, rx, ry, right - UiTokens.S3 - rx, m.ChartTabs, m.ChartTab, null, ChartTabCommand);
                }
                ry += UiTokens.Control + UiTokens.S3;
            }
            int firstPoint = s.NodeCount;
            UiChart.Lay(s, at, m.Chart, rx, ry, rw, UiDraw.SnapDown(y + h - ry));
            // The league is a column of its own: Right from a row goes over to the chosen rival's panel and Left comes back to
            // the chosen row; the chart's days stay in the window rather than dropping to the status bar.
            int back = chosen >= 0 ? chosen : firstLeague;
            for (int i = firstLeague; i < lastLeague; i++)
            {
                if (s.Nodes[i].Command == UiDataTable.First + UiDataTable.Row && firstRight < s.NodeCount)
                {
                    s.Nodes[i].Right = firstRight;
                }
            }
            for (int i = firstPoint; i < s.NodeCount; i++)
            {
                s.Nodes[i].Down = i;
                s.Nodes[i].Left = i > firstPoint ? i - 1 : back;
                s.Nodes[i].Right = i + 1 < s.NodeCount ? i + 1 : i;
            }
        }

        // A rivalry's line: its glyph on a tile, what it is and what it means under it, cut to the column.
        private static void Threat(UiScene s, UiAt at, ref UiRivalThreat t, float x, float y, float w)
        {
            float tile = UiTokens.ControlSmall;
            int tl = UiDraw.Tile(s, at, x, y + (ThreatRow - tile) / 2f, tile, UiRole.None, false);
            float g = UiTokens.Icon;
            UiDraw.Icon(s, at.In(tl), x + (tile - g) / 2f, y + (ThreatRow - g) / 2f, g, t.Icon, UiRole.None, UiFlags.Middle | UiFlags.Centre, true);
            float tx = x + tile + UiTokens.S3, tw = x + w - tx;
            float lh = UiTextRules.LineHeight(UiStyle.Title), sh = UiTextRules.LineHeight(UiStyle.Small);
            float top = y + (ThreatRow - lh - sh - UiTokens.S1) / 2f;
            UiDraw.Text(s, at, tx, top, tw, UiStyle.Title, UiRole.Text, t.Title, UiFlags.Truncate | UiFlags.Middle);
            UiDraw.Text(s, at, tx, top + lh + UiTokens.S1, tw, UiStyle.Small, UiRole.Text2, t.Body, UiFlags.Truncate | UiFlags.Middle);
        }

        // The chosen row of a rival's table over it: its name and locate link at the left, its actions at the right, the
        // main one last. Returns the y under it.
        private static float RowBar(UiScene s, UiAt at, UiRivalsModel m, float x, float y, float w, IUiKeys keys)
        {
            if (string.IsNullOrEmpty(m.RowName) && m.RowActions.Length == 0)
            {
                return y;
            }
            float h = UiTokens.Control, right = x + w;
            for (int i = m.RowActions.Length - 1; i >= 0; i--)
            {
                ref UiActionModel a = ref m.RowActions[i];
                float bw = UiDraw.ButtonWidth(a.Kind, a.Label, a.Icon, a.Key, keys);
                right -= bw;
                UiDraw.Button(s, at, right, y, bw, h, a.Kind, a.Label, a.Icon, a.Key, keys, RowActionCommand, i, a.Disabled);
                right -= UiTokens.S3;
            }
            float room = right - UiTokens.S6 - x;
            float linkW = string.IsNullOrEmpty(m.RowLink) ? 0f : Math.Min(room / 2f, UiDraw.Snap(UiTextRules.Width(m.RowLink, UiStyle.Body) + UiTokens.IconSmall + UiTokens.S3));
            float nameW = Math.Min(room - (linkW > 0f ? linkW + UiTokens.S4 : 0f), UiDraw.Snap(UiTextRules.Width(m.RowName ?? string.Empty, UiStyle.Title) + UiTokens.S1));
            UiDraw.TextMiddle(s, at, x, y, h, nameW, UiStyle.Title, UiRole.Text, m.RowName, UiFlags.Truncate);
            if (linkW > 0f)
            {
                UiDraw.LinkRow(s, at, x + nameW + UiTokens.S4, y, linkW, h, m.RowLink, RowLinkCommand, 0, false);
            }
            return y + h + UiTokens.S4;
        }
    }
}
