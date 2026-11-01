using System;
using System.Globalization;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A population view (PopulationViews, PopDemandViews) as the kit's dashboard, shaped to what it shows: a place (the
    /// city, a district, a building, a business, who contributes) at a glance with its figures, its list and its facts, a
    /// household as a ledger of its month beside its people, and a person as a profile. The place stands in the masthead
    /// after the app's name. Names come in from the game's own name maker, places by the game's words (a record's key never
    /// reaches the screen) and money in the game's form. Only rows a press opens are targets. Game-free. Cost: a model a
    /// view, when a screen opens or moves.
    /// </summary>
    public static class PopulationPages
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const float FigureColumn = UiTokens.S16 * 2f + UiTokens.S8;   // px: a column of figures, wide enough for "$1,234,567"

        public static UiDashboardModel Model(PopulationView v, UiWords t, Func<long, string> money, Func<int, bool, string> nameOf, Func<string, string> place)
        {
            bool empty = v.Gone || (v.Hero.Count == 0 && v.Rows.Count == 0 && v.Details.Count == 0);
            var m = new UiDashboardModel
            {
                Title = empty ? null : Title(v.Title, v.TitleKey, v.NameSeed, v.Female, t, nameOf, place),
                SideTitle = t("reallife_ui_page_details", "Details"),
            };
            m.Table = new UiTableModel { EmptyTitle = Clean(t(v.Empty ?? "reallife_ui_page_empty", "Nothing here yet"), place) };
            if (v.Gone)
            {
                // One sentence alone told the player nothing (the critic, 1 Nov): say what happened and the way on.
                m.Table.EmptyBody = t("reallife_ui_pop_gone_body", "People leave the city when they move away or pass on. Back takes you to where you were.");
            }
            if (!empty && v.Kind == ViewKind.Household)
            {
                Ledger(m, v, t, money, nameOf, place);
            }
            else if (!empty && v.Kind == ViewKind.Person)
            {
                Profile(m, v, t, money, nameOf);
            }
            else
            {
                Board(m, v, t, money, nameOf, place);
            }
            m.HasAction = !string.IsNullOrEmpty(v.Primary) && v.PrimaryTarget.Valid;
            if (m.HasAction)
            {
                m.Action = new UiActionModel { Label = t(v.Primary, v.Primary), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiDashboard.ActionCommand };
            }
            bool leads = false;
            for (int i = 0; i < v.Rows.Count; i++)
            {
                leads |= v.Rows[i].Target.Valid;
            }
            var prompts = new System.Collections.Generic.List<UiPrompt>(4) { new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") } };
            if (leads && !empty)
            {
                prompts.Add(new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") });   // only where a row opens something
            }
            if (m.HasAction)
            {
                prompts.Add(new UiPrompt { Action = UiAction.Alt, Label = m.Action.Label });
            }
            prompts.Add(new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") });
            m.Prompts = prompts.ToArray();
            m.PromptCount = m.Prompts.Length;
            return m;
        }

        // A place at a glance: its figures, its list as a table (each of the view's figures in a column of its own), its facts.
        private static void Board(UiDashboardModel m, PopulationView v, UiWords t, Func<long, string> money, Func<int, bool, string> nameOf, Func<string, string> place)
        {
            m.Shape = UiPageShape.Board;
            m.Subtitle = string.IsNullOrEmpty(v.Subtitle) ? null : Clean(t(v.Subtitle, v.Subtitle), place);
            Figures(m, v, t, money);
            bool wide = v.Columns != null && v.ColumnFormats != null && v.Columns.Length == v.ColumnFormats.Length;
            if (wide)
            {
                m.Table.Columns = new UiColumn[v.Columns.Length + 1];
                m.Table.Columns[0] = new UiColumn { Title = string.IsNullOrEmpty(v.RowsHeader) ? string.Empty : t(v.RowsHeader, v.RowsHeader) };
                for (int c = 0; c < v.Columns.Length; c++)
                {
                    m.Table.Columns[c + 1] = new UiColumn { Title = t(v.Columns[c], v.Columns[c]), Width = FigureColumn, Right = true };
                }
            }
            else
            {
                m.Table.Columns = new[]
                {
                    new UiColumn { Title = string.IsNullOrEmpty(v.RowsHeader) ? string.Empty : t(v.RowsHeader, v.RowsHeader) },
                    new UiColumn { Title = string.Empty },
                    new UiColumn { Title = string.Empty, Width = FigureColumn, Right = true },
                };
            }
            m.Table.Rows = new string[v.Rows.Count][];
            m.Table.Leads = new bool[v.Rows.Count];
            for (int i = 0; i < v.Rows.Count; i++)
            {
                ViewRow r = v.Rows[i];
                string title = Title(r.Title, r.TitleKey, r.NameSeed, r.Female, t, nameOf, place);
                if (wide && r.Figures != null)
                {
                    var cells = new string[v.Columns.Length + 1];
                    cells[0] = title;
                    for (int c = 0; c < v.Columns.Length; c++)
                    {
                        cells[c + 1] = c < r.Figures.Length ? Write(ViewFigure.Of(null, r.Figures[c], v.ColumnFormats[c]), t, money) : string.Empty;
                    }
                    m.Table.Rows[i] = cells;
                }
                else
                {
                    string sub = string.IsNullOrEmpty(r.Subtitle) ? string.Empty : Clean(t(r.Subtitle, r.Subtitle), place);
                    m.Table.Rows[i] = new[] { title, sub, Write(ViewFigure.Of(null, r.Value, r.Format), t, money) };
                }
                m.Table.Leads[i] = r.Target.Valid;
            }
            m.Table.RowCount = v.Rows.Count;
            m.Table.Selected = -1;   // a list that opens other pages marks nothing: the ring says where the keys are, Back brings it to the row it left
            Details(m, v, t, money, place);
        }

        // A household as a ledger entry: its last month and its home and money down a ledger, its people beside it.
        private static void Ledger(UiDashboardModel m, PopulationView v, UiWords t, Func<long, string> money, Func<int, bool, string> nameOf, Func<string, string> place)
        {
            m.Shape = UiPageShape.Ledger;
            m.Title = Household(v, t, nameOf);
            m.Subtitle = string.IsNullOrEmpty(v.Subtitle) ? null : Clean(t(v.Subtitle, v.Subtitle), place);
            var month = new UiPairModel[v.Hero.Count];
            for (int i = 0; i < month.Length; i++)
            {
                month[i] = new UiPairModel { Key = t(v.Hero[i].Label, v.Hero[i].Label), Value = Write(v.Hero[i], t, money) };
            }
            var home = new UiPairModel[v.Details.Count];
            for (int i = 0; i < home.Length; i++)
            {
                home[i] = new UiPairModel { Key = t(v.Details[i].Label, v.Details[i].Label), Value = Clean(Write(v.Details[i], t, money), place) };
            }
            m.Ledger = new[]
            {
                new UiLedgerSection { Title = t("reallife_ui_pop_last_month", "Last month"), Lines = month },
                new UiLedgerSection { Title = t("reallife_ui_pop_home", "Home and money"), Lines = home },
            };
            People(m, v, t, money, nameOf, place);
        }

        // The people a page lists (a household's, those a person lives with) as rows with their faces, each one's age said as an age.
        private static void People(UiDashboardModel m, PopulationView v, UiWords t, Func<long, string> money, Func<int, bool, string> nameOf, Func<string, string> place)
        {
            m.EntriesTitle = string.IsNullOrEmpty(v.RowsHeader) ? null : t(v.RowsHeader, v.RowsHeader);
            m.Entries = new UiRowModel[v.Rows.Count];
            m.EntryLeads = new bool[v.Rows.Count];
            for (int i = 0; i < v.Rows.Count; i++)
            {
                ViewRow r = v.Rows[i];
                string name = Title(r.Title, r.TitleKey, r.NameSeed, r.Female, t, nameOf, place);
                string figure = Write(ViewFigure.Of(null, r.Value, r.Format), t, money);
                m.Entries[i] = new UiRowModel
                {
                    Name = name, Preview = string.IsNullOrEmpty(r.Subtitle) ? string.Empty : Clean(t(r.Subtitle, r.Subtitle), place),
                    Time = r.Format == ViewFormat.Years ? t("reallife_ui_pop_age", "Age {age}").Replace("{age}", figure) : figure, Hue = UiColour.HueOf(name)
                };
                m.EntryLeads[i] = r.Target.Valid;
            }
            m.EntryCount = m.Entries.Length;
        }

        // A person as a profile: their face, name and state across its head, their figures and facts, and the people they live
        // with (or the words that they live alone). Pay and hours of nothing are left out: the state already says why.
        private static void Profile(UiDashboardModel m, PopulationView v, UiWords t, Func<long, string> money, Func<int, bool, string> nameOf)
        {
            m.Shape = UiPageShape.Profile;
            m.Hue = UiColour.HueOf(m.Title);
            m.Subtitle = string.IsNullOrEmpty(v.Subtitle) ? null : t(v.Subtitle, v.Subtitle);
            var figures = new System.Collections.Generic.List<UiFigureModel>(UiDashboard.MostFigures);
            for (int i = 0; i < v.Hero.Count && figures.Count < UiDashboard.MostFigures; i++)
            {
                if (v.Hero[i].Value != 0 || v.Hero[i].Format == ViewFormat.Years)
                {
                    figures.Add(new UiFigureModel { Label = t(v.Hero[i].Label, v.Hero[i].Label), Value = Write(v.Hero[i], t, money) });
                }
            }
            m.Figures = figures.ToArray();
            m.FigureCount = m.Figures.Length;
            Details(m, v, t, money, null);
            People(m, v, t, money, nameOf, null);
        }

        private static void Figures(UiDashboardModel m, PopulationView v, UiWords t, Func<long, string> money)
        {
            int count = Math.Min(UiDashboard.MostFigures, v.Hero.Count);
            m.Figures = new UiFigureModel[count];
            for (int i = 0; i < count; i++)
            {
                m.Figures[i] = new UiFigureModel { Label = t(v.Hero[i].Label, v.Hero[i].Label), Value = Write(v.Hero[i], t, money) };
            }
            m.FigureCount = count;
        }

        private static void Details(UiDashboardModel m, PopulationView v, UiWords t, Func<long, string> money, Func<string, string> place)
        {
            m.Side = new UiPairModel[v.Details.Count];
            for (int i = 0; i < v.Details.Count; i++)
            {
                m.Side[i] = new UiPairModel { Key = t(v.Details[i].Label, v.Details[i].Label), Value = Clean(Write(v.Details[i], t, money), place) };
            }
            m.SideCount = m.Side.Length;
        }

        /// <summary>
        /// A household's name: "The Stanley household" by its head's family name, so its page never repeats the name of
        /// the one person it lists. Plain "Household" when the name maker gives no family name.
        /// </summary>
        public static string Household(PopulationView v, UiWords t, Func<int, bool, string> nameOf)
        {
            string head = v.NameSeed != 0 && nameOf != null ? nameOf(v.NameSeed, v.Female) : null;
            int cut = string.IsNullOrEmpty(head) ? -1 : head.TrimEnd().LastIndexOf(' ');
            if (cut <= 0)
            {
                return t("reallife_ui_pop_household_plain", "Household");
            }
            return t("reallife_ui_pop_household", "The {surname} household").Replace("{surname}", head.Substring(cut + 1).Trim());
        }

        /// <summary>A figure in words: a count, money in the game's form, a share as a per cent, a key's words, years or hours.</summary>
        public static string Write(ViewFigure f, UiWords t, Func<long, string> money)
        {
            switch (f.Format)
            {
                case ViewFormat.Money: return money(f.Value);
                case ViewFormat.Permille: return (f.Value / 10.0).ToString("0.#", Inv) + "%";
                case ViewFormat.Key: return string.IsNullOrEmpty(f.Text) ? string.Empty : t(f.Text, f.Text);
                default: return f.Value.ToString("#,0", Inv);
            }
        }

        // A record's key never reaches the screen: a place by the game's words, anything else as plain words.
        private static string Clean(string text, Func<string, string> place)
        {
            if (!UiPlaces.IsKey(text))
            {
                return text ?? string.Empty;
            }
            string named = place != null ? place(text) : null;
            return !string.IsNullOrEmpty(named) && !UiPlaces.IsKey(named) ? named : UiPlaces.Address(text, null);
        }

        private static string Title(string title, bool key, int seed, bool female, UiWords t, Func<int, bool, string> nameOf, Func<string, string> place)
        {
            if (seed != 0 && nameOf != null)
            {
                return nameOf(seed, female);
            }
            return Clean(key ? t(title, title) : title ?? string.Empty, place);
        }
    }
}
