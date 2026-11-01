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
            primary = v.Primary != null && Runs(v.Primary.Act) ? v.Primary : null;
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
            bool needed = false;
            for (int i = 0; i < page.Team.Count; i++)
            {
                needed |= page.Team[i].Needed;
            }
            // A "Needed" alone left the owner unsure how to fill it: the people are hired, then their roles are set in Open Roles.
            m.StatsNote = needed ? t("reallife_studio_page_needed_how", "To fill a group, hire people in the Business app, then set their roles in Open Roles, on its Team tab.") : null;
            m.SideTitle = t(page.FactsKey, page.FactsKey);
            m.Side = new UiPairModel[page.Facts.Count];
            for (int i = 0; i < page.Facts.Count; i++)
            {
                m.Side[i] = new UiPairModel { Key = t(page.Facts[i].LabelKey, page.Facts[i].LabelKey), Value = Capital(page.Facts[i].Value) };
            }
            m.SideCount = m.Side.Length;
            for (int i = 0; i < v.Others.Count && others.Count < MostOthers; i++)
            {
                if (Runs(v.Others[i].Act))
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

        /// <summary>
        /// Whether the page can run an act itself, through the director. The Tech app's own screens (a new product, the
        /// contracts, the concept) open only inside that app, and its close button is the kit's Back already, so those are
        /// left off the page rather than drawn as buttons that do nothing.
        /// </summary>
        public static bool Runs(int act)
        {
            switch (act)
            {
                case SectorAct.OpenRoles:
                case SectorAct.Launch:
                case SectorAct.Desk:
                case SectorAct.Schedule:
                    return true;
                case StudioAct.Concept:
                case StudioAct.Close:
                case StudioAct.BeatPick:
                case StudioAct.ProjectPick:
                    return false;
                default:
                    return act >= StudioAct.Genre && act <= StudioAct.Greenlight;
            }
        }

        // A view's table as the kit's: its columns named, figures to the right in a column of their own width, its rows as
        // written. Its rows open nothing, so none is a target.
        private static UiTableModel Table(ViewTable v, UiWords t)
        {
            var columns = new UiColumn[v.ColumnKeys.Length];
            for (int i = 0; i < columns.Length; i++)
            {
                bool figure = i < v.Figures.Length && v.Figures[i];
                columns[i] = new UiColumn { Title = t(v.ColumnKeys[i], v.ColumnKeys[i]), Width = figure ? FigureColumn : 0f, Right = figure };
            }
            var table = new UiTableModel { Columns = columns, Rows = v.Rows.ToArray(), RowCount = v.Rows.Count, Selected = -1, Inert = true };
            table.EmptyTitle = string.IsNullOrEmpty(v.EmptyKey) ? t("reallife_ui_page_empty", "Nothing here yet") : t(v.EmptyKey, v.EmptyKey);
            return table;
        }

        /// <summary>
        /// A sample studio's page, named as a sample, for the test that opens a firm before the player owns one: the page
        /// can be seen on a fresh save, and nothing on it acts, as none of it is the player's. Back closes it.
        /// </summary>
        public static UiDashboardModel Sample(UiWords t)
        {
            var m = new UiDashboardModel
            {
                Title = t("reallife_ui_sample_firm", "A sample studio"),
                Subtitle = t("reallife_ui_sample_firm_note", "This is a sample, so the page can be seen. Open a tech firm to see your own here."),
                SubtitleInk = UiRole.WarnText,
                StatsFirst = true,
                Figures = new[]
                {
                    new UiFigureModel { Label = t("reallife_studio_page_games_out", "Games out"), Value = "3" },
                    new UiFigureModel { Label = t("reallife_studio_page_copies", "Copies sold"), Value = "82,100" },
                    new UiFigureModel
                    {
                        Label = t("reallife_studio_page_takings", "Takings, 30 days"), Value = "$184,200", Span = 2,
                        Trend = new[] { 38f, 41f, 40f, 44f, 47f, 51f, 49f, 46f, 44f, 45f, 43f, 45f, 48f, 50f, 49f, 47f }
                    },
                },
            };
            m.FigureCount = m.Figures.Length;
            m.Table = new UiTableModel
            {
                Columns = new[]
                {
                    new UiColumn { Title = t("reallife_studio_page_col_game", "Game") }, new UiColumn { Title = t("reallife_studio_page_col_genre", "Genre") },
                    new UiColumn { Title = t("reallife_studio_page_col_score", "Score"), Width = FigureColumn, Right = true },
                    new UiColumn { Title = t("reallife_studio_page_col_copies", "Copies"), Width = FigureColumn, Right = true },
                    new UiColumn { Title = t("reallife_studio_page_col_kept", "Kept"), Width = FigureColumn, Right = true },
                },
                Rows = new[]
                {
                    new[] { "Ember Isle", t("reallife_studio_genre_adventure", "Adventure"), "78", "41,000", "$612,400" },
                    new[] { "Tiny Towns", t("reallife_studio_genre_simulation", "Simulation"), "74", "28,500", "$341,900" },
                    new[] { "Deep Signal", t("reallife_studio_genre_strategy", "Strategy"), "69", "12,600", "$128,300" },
                },
                RowCount = 3, Selected = -1, Inert = true,
            };
            m.StatsTitle = t("reallife_studio_page_team", "Team");
            m.Stats = new[]
            {
                new UiStatModel { Label = t("reallife_studio_page_group_engineers", "Engineers"), Figure = "64", Value = 64 },
                new UiStatModel { Label = t("reallife_studio_page_group_designers", "Designers"), Figure = "58", Value = 58 },
                new UiStatModel { Label = t("reallife_studio_page_group_testers", "Testers"), Figure = "52", Value = 52 },
            };
            m.StatCount = m.Stats.Length;
            m.SideTitle = t("reallife_studio_page_facts", "Sales and support");
            m.Side = new[]
            {
                new UiPairModel { Key = t("reallife_studio_page_pirated", "Pirated copies"), Value = "9,800" },
                new UiPairModel { Key = t("reallife_studio_page_engine", "Engine"), Value = t("reallife_ui_sample_engine", "Under licence") },
            };
            m.SideCount = m.Side.Length;
            m.Prompts = new[] { new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") } };
            m.PromptCount = 1;
            return m;
        }

        // A value standing alone starts with a capital, as its neighbours do ("New levels, co-op play" beside "Under licence"),
        // though the same words sit lower case inside a sentence elsewhere.
        private static string Capital(string value)
        {
            return string.IsNullOrEmpty(value) || !char.IsLower(value[0]) ? value : char.ToUpper(value[0], System.Globalization.CultureInfo.CurrentCulture) + value.Substring(1);
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
