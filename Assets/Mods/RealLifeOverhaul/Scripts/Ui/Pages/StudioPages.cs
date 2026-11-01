using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A tech firm's whole page (StudioViews.Page, SectorViews.Page: a FirmPage) as the kit's dashboard: the title with what
    /// to do next under it in its weight's colour, the primary action and the others at the head's right, the figures with
    /// the takings' line in a wide panel, the main table over the second, the team as bars and the facts as pairs at the side.
    /// The actions are handed back so a press reaches the feature's own act. Game-free. Cost: a model a page, when it opens
    /// or after a press.
    /// </summary>
    public static class StudioPages
    {
        public const int MostOthers = 2;                                   // actions beside the primary: a page keeps to a few
        public const float FigureColumn = UiTokens.S16 * 2f + UiTokens.S4;   // px: a table's column of figures or a short status

        public static UiDashboardModel Page(FirmPage page, UiWords t, List<ViewAction> others, out ViewAction primary)
        {
            StudioScreen v = page.Screen;
            others.Clear();
            primary = v.Primary;
            var m = new UiDashboardModel { Title = Fill(t, v.TitleKey, v.TitlePairs), StatsFirst = true };
            if (v.Next != null)
            {
                m.Subtitle = Fill(t, v.Next.Key, v.Next.Pairs);
                m.SubtitleInk = v.Next.Tone == ViewTone.Bad ? UiRole.DangerText : v.Next.Tone == ViewTone.Warn ? UiRole.WarnText : v.Next.Tone == ViewTone.Good ? UiRole.OkText : UiRole.Text;
            }
            m.Figures = new UiFigureModel[page.Figures.Count];
            for (int i = 0; i < page.Figures.Count; i++)
            {
                ViewFigure f = page.Figures[i];
                m.Figures[i] = new UiFigureModel { Label = t(f.LabelKey, f.LabelKey), Value = f.Value, Change = f.Change, Rising = f.Rising, Good = f.Good, Trend = f.Trend, Span = f.Wide ? 2 : 1 };
            }
            m.FigureCount = m.Figures.Length;
            m.Table = Table(page.Main, t);
            m.More = Table(page.More, t);
            m.StatsTitle = t(page.TeamKey, page.TeamKey);
            m.Stats = new UiStatModel[page.Team.Count];
            for (int i = 0; i < page.Team.Count; i++)
            {
                ViewRole r = page.Team[i];
                string group = t(StudioViews.GroupKey(r.Group), StudioViews.GroupKey(r.Group));
                m.Stats[i] = new UiStatModel
                {
                    Label = t(StudioViews.GroupCountKey, StudioViews.GroupCountKey).Replace("{group}", group).Replace("{count}", r.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    Figure = r.Needed ? t(StudioViews.NeededKey, StudioViews.NeededKey) : r.Skill.ToString(System.Globalization.CultureInfo.InvariantCulture), Value = r.Skill
                };
            }
            m.StatCount = m.Stats.Length;
            m.SideTitle = t(page.FactsKey, page.FactsKey);
            m.Side = new UiPairModel[page.Facts.Count];
            for (int i = 0; i < page.Facts.Count; i++)
            {
                m.Side[i] = new UiPairModel { Key = t(page.Facts[i].LabelKey, page.Facts[i].LabelKey), Value = page.Facts[i].Value };
            }
            m.SideCount = m.Side.Length;
            for (int i = 0; i < v.Others.Count && others.Count < MostOthers; i++)
            {
                // The Tech app's close button is the kit's Back, on the prompt bar already.
                if (v.Others[i].Act != StudioAct.Close && v.Others[i].Act != SectorAct.Close)
                {
                    others.Add(v.Others[i]);
                }
            }
            m.Others = new UiActionModel[others.Count];
            for (int i = 0; i < others.Count; i++)
            {
                ViewAction a = others[i];
                m.Others[i] = new UiActionModel
                {
                    Label = t(a.Key, a.Key), Kind = a.Danger ? UiButtonKind.Destructive : UiButtonKind.Secondary, Key = i == 0 ? UiAction.Option : UiAction.Count,
                    Command = UiDashboard.OtherCommand, Disabled = !a.Enabled
                };
            }
            m.OtherCount = m.Others.Length;
            m.HasAction = primary != null;
            if (m.HasAction)
            {
                m.Action = new UiActionModel { Label = t(primary.Key, primary.Key), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiDashboard.ActionCommand, Disabled = !primary.Enabled };
            }
            var prompts = new List<UiPrompt>(3) { new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") } };
            if (m.HasAction)
            {
                prompts.Add(new UiPrompt { Action = UiAction.Alt, Label = m.Action.Label });
            }
            prompts.Add(new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") });
            m.Prompts = prompts.ToArray();
            m.PromptCount = m.Prompts.Length;
            return m;
        }

        // A view's table as the kit's: its columns named, figures to the right in a column of their own width, its rows as written.
        private static UiTableModel Table(ViewTable v, UiWords t)
        {
            var columns = new UiColumn[v.ColumnKeys.Length];
            for (int i = 0; i < columns.Length; i++)
            {
                bool figure = i < v.Figures.Length && v.Figures[i];
                columns[i] = new UiColumn { Title = t(v.ColumnKeys[i], v.ColumnKeys[i]), Width = figure ? FigureColumn : 0f, Right = figure };
            }
            var table = new UiTableModel { Columns = columns, Rows = v.Rows.ToArray(), RowCount = v.Rows.Count, Selected = -1 };
            table.EmptyTitle = string.IsNullOrEmpty(v.EmptyKey) ? t("reallife_ui_page_empty", "Nothing here yet") : t(v.EmptyKey, v.EmptyKey);
            return table;
        }

        // A key's words with its names filled: name, value, name, value (as the game's own Fill does).
        private static string Fill(UiWords t, string key, string[] pairs)
        {
            string words = string.IsNullOrEmpty(key) ? string.Empty : t(key, key);
            for (int i = 0; pairs != null && i + 1 < pairs.Length; i += 2)
            {
                words = words.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return words;
        }
    }
}
