using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A tech firm's whole page (StudioViews.Page, SectorViews.Page: a FirmPage) as the face the Tech app's firm pages share
    /// (StudioFace): the title, what to do next in its weight's colour, the actions at the head's right, the figures with the
    /// takings' line, the main table over the second, the team as graded bars and the facts at the side. The actions are handed
    /// back in the order they stand, the main one last, so a press reaches the feature's own act. Cost: a model a page, when it
    /// opens or after a press.
    /// </summary>
    public static class StudioPages
    {
        public const int MostOthers = 2;   // actions beside the primary: a page keeps to a few

        public static StudioFaceModel Page(FirmPage page, UiWords t, List<ViewAction> acts)
        {
            StudioScreen v = page.Screen;
            acts.Clear();
            var m = new StudioFaceModel { Title = Fill(t, v.TitleKey, v.TitlePairs) };
            if (v.Next != null)
            {
                m.Next = Fill(t, v.Next.Key, v.Next.Pairs);
                m.NextInk = v.Next.Tone == ViewTone.Bad ? UiRole.DangerText : v.Next.Tone == ViewTone.Warn ? UiRole.WarnText : v.Next.Tone == ViewTone.Good ? UiRole.OkText : UiRole.Text;
            }
            m.Figures = new UiFigureModel[page.Figures.Count];
            for (int i = 0; i < page.Figures.Count; i++)
            {
                ViewFigure f = page.Figures[i];
                m.Figures[i] = new UiFigureModel { Label = t(f.LabelKey, f.LabelKey), Value = f.Value, Change = f.Change, Rising = f.Rising, Good = f.Good, Trend = f.Trend, Span = f.Wide ? 2 : 1 };
            }
            m.Main = Table(page.Main, t, UiIcon.GameController);
            m.More = Table(page.More, t, UiIcon.Flag);
            string[] groups = new string[page.Team.Count], figures = new string[page.Team.Count];
            int[] skills = new int[page.Team.Count];
            bool needed = false;
            for (int i = 0; i < page.Team.Count; i++)
            {
                ViewRole r = page.Team[i];
                string group = t(StudioViews.GroupKey(r.Group), StudioViews.GroupKey(r.Group));
                groups[i] = t(StudioViews.GroupCountKey, StudioViews.GroupCountKey).Replace("{group}", group).Replace("{count}", r.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                figures[i] = r.Needed ? t(StudioViews.NeededKey, StudioViews.NeededKey) : r.Skill.ToString(System.Globalization.CultureInfo.InvariantCulture);
                skills[i] = r.Skill;
                needed |= r.Needed;
            }
            m.Team = StudioFace.TeamTable(t(page.TeamKey, page.TeamKey), t("reallife_ui_tech_skill", "Skill"), groups, skills, figures, t("reallife_studio_page_none", "None"));
            // A "Needed" alone left the owner unsure how to fill it: the people are hired, then their roles are set in Open Roles.
            string how = t("reallife_studio_page_needed_how", "To fill a group, hire people in the Business app, then set their roles in Open Roles, on its Team tab.");
            m.Note = needed ? StudioFace.NoteCard(how) : null;
            if (page.Facts.Count > 0)
            {
                string[] labels = new string[page.Facts.Count], values = new string[page.Facts.Count];
                for (int i = 0; i < labels.Length; i++)
                {
                    labels[i] = t(page.Facts[i].LabelKey, page.Facts[i].LabelKey);
                    values[i] = Capital(page.Facts[i].Value);
                }
                m.Facts = StudioFace.FactsCard(t(page.FactsKey, page.FactsKey), labels, values);
            }
            for (int i = 0; i < v.Others.Count && acts.Count < MostOthers; i++)
            {
                if (Runs(v.Others[i].Act))
                {
                    acts.Add(v.Others[i]);
                }
            }
            int others = acts.Count;
            bool primary = v.Primary != null && Runs(v.Primary.Act);
            if (primary)
            {
                acts.Add(v.Primary);
            }
            m.Actions = new UiActionModel[acts.Count];
            for (int i = 0; i < acts.Count; i++)
            {
                ViewAction a = acts[i];
                bool main = primary && i == others;
                m.Actions[i] = new UiActionModel
                {
                    Label = t(a.Key, a.Key), Kind = main ? UiButtonKind.Primary : a.Danger ? UiButtonKind.Destructive : UiButtonKind.Secondary,
                    Key = main ? UiAction.Alt : i == 0 ? UiAction.Option : UiAction.Count, Command = StudioFace.ActionCommand, Arg = i, Disabled = !a.Enabled
                };
            }
            m.Prompts = Prompts(t, primary ? m.Actions[acts.Count - 1].Label : null);
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

        /// <summary>The page when its firm cannot be read (it was sold or closed since): its words, and the way back.</summary>
        public static StudioFaceModel Empty(UiWords t)
        {
            string empty = t("reallife_ui_page_empty", "Nothing here yet");
            return new StudioFaceModel
            {
                Title = t("reallife_ui_page_studio", "Your firm"),
                Main = StudioFace.Table(new[] { string.Empty }, new[] { string.Empty }, new[] { false }, new string[0][], empty, UiIcon.GameController),
                Prompts = Prompts(t, null)
            };
        }

        // A view's table in the face's: its columns named, figures to the right, its rows as written.
        private static UiDataTableModel Table(ViewTable v, UiWords t, UiIcon icon)
        {
            var titles = new string[v.ColumnKeys.Length];
            for (int i = 0; i < titles.Length; i++)
            {
                titles[i] = t(v.ColumnKeys[i], v.ColumnKeys[i]);
            }
            string empty = string.IsNullOrEmpty(v.EmptyKey) ? t("reallife_ui_page_empty", "Nothing here yet") : t(v.EmptyKey, v.EmptyKey);
            return StudioFace.Table(titles, v.ColumnKeys, v.Figures, v.Rows, empty, icon);
        }

        private static UiPrompt[] Prompts(UiWords t, string primary)
        {
            var prompts = new List<UiPrompt>(3) { new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") } };
            if (primary != null)
            {
                prompts.Add(new UiPrompt { Action = UiAction.Alt, Label = primary });
            }
            prompts.Add(new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") });
            return prompts.ToArray();
        }

        /// <summary>
        /// A sample studio's page, named as a sample, for the test that opens a firm before the player owns one: the page
        /// can be seen on a fresh save, and nothing on it acts, as none of it is the player's. Back closes it.
        /// </summary>
        public static StudioFaceModel Sample(UiWords t)
        {
            var m = new StudioFaceModel
            {
                Title = t("reallife_ui_sample_firm", "A sample studio"),
                Next = t("reallife_ui_sample_firm_note", "This is a sample, so the page can be seen. Open a tech firm to see your own here."),
                NextInk = UiRole.WarnText,
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
            m.Main = StudioFace.Table(
                new[] { t("reallife_studio_page_col_game", "Game"), t("reallife_studio_page_col_genre", "Genre"), t("reallife_studio_page_col_score", "Score"),
                    t("reallife_studio_page_col_copies", "Copies"), t("reallife_studio_page_col_kept", "Kept") },
                new[] { "page_col_game", "page_col_genre", "page_col_score", "page_col_copies", "page_col_kept" }, new[] { false, false, true, true, true },
                new[]
                {
                    new[] { "Ember Isle", t("reallife_studio_genre_adventure", "Adventure"), "78", "41,000", "$612,400" },
                    new[] { "Tiny Towns", t("reallife_studio_genre_simulation", "Simulation"), "74", "28,500", "$341,900" },
                    new[] { "Deep Signal", t("reallife_studio_genre_strategy", "Strategy"), "69", "12,600", "$128,300" },
                }, string.Empty, UiIcon.GameController);
            m.Team = StudioFace.TeamTable(t("reallife_studio_page_team", "Team"), t("reallife_ui_tech_skill", "Skill"),
                new[] { t("reallife_studio_page_group_engineers", "Engineers"), t("reallife_studio_page_group_designers", "Designers"), t("reallife_studio_page_group_testers", "Testers") },
                new[] { 64, 58, 52 }, new[] { "64", "58", "52" }, string.Empty);
            m.Facts = StudioFace.FactsCard(t("reallife_studio_page_facts", "Sales and support"),
                new[] { t("reallife_studio_page_pirated", "Pirated copies"), t("reallife_studio_page_engine", "Engine") },
                new[] { "9,800", t("reallife_ui_sample_engine", "Under licence") });
            m.Prompts = Prompts(t, null);
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
