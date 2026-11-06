using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Population;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// A population view (PopulationViews, PopDemandViews) as the page of the city's people (PopulationFace), shaped to what it
    /// shows: the city as its figures with its districts, a place (a district, a building, a business, who contributes) as a
    /// head with its key figures over its rows and facts, a household as its people beside its month's ledger, and a person
    /// as a profile with their face. Names come in from the game's own name maker, places by the game's words (a record's key
    /// never reaches the screen) and money in the game's form. Game-free. Cost: a model a view, when a screen opens or moves.
    /// </summary>
    public static class PopulationPages
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const float FigureColumn = UiTokens.S16 * 2f + UiTokens.S8;   // px: a column of figures, wide enough for "$1,234,567"
        private const int MostFigures = 4;                                    // places in the figures' row: more and they stop being glanceable

        /// <summary>
        /// A view's page. <paramref name="name"/> is what the row that opened it called the household or person, so a page
        /// for one who has gone can say who.
        /// </summary>
        public static PopulationModel Model(PopulationView v, UiWords t, Func<long, string> money, Func<int, bool, string> nameOf, Func<string, string> place,
            string name = null)
        {
            var m = new PopulationModel();
            bool empty = v.Gone || (v.Hero.Count == 0 && v.Rows.Count == 0 && v.Details.Count == 0);
            if (empty)
            {
                m.Gone = true;
                m.GoneTitle = Clean(t(v.Empty ?? "reallife_ui_page_empty", "Nothing here yet"), place);
                if (v.Gone && !string.IsNullOrEmpty(name) && (v.Kind == ViewKind.Person || v.Kind == ViewKind.Household))
                {
                    m.GoneTitle = t("reallife_ui_pop_gone_named", "{name} has moved on.").Replace("{name}", name);   // who, as the row named them
                }
                // One sentence alone told the player nothing (the critic, 1 Nov): say what happened and the way on.
                m.GoneBody = t("reallife_ui_pop_gone_body", "People leave the city when they move away or pass on. Back takes you to where you were.");
                return Prompts(m, v, t, false);
            }
            string title = Title(v.Title, v.TitleKey, v.NameSeed, v.Female, t, nameOf, place);
            switch (v.Kind)
            {
                case ViewKind.City:
                    City(m, v, t, money);
                    break;
                case ViewKind.Household:
                    Household(m, v, t, money, nameOf, place);
                    break;
                case ViewKind.Person:
                    Person(m, v, title, t, money);
                    break;
                default:
                    Place(m, v, title, t, money, place);
                    break;
            }
            m.List = List(v, t, money, nameOf, place);
            if (v.Kind != ViewKind.Household)
            {
                m.Sections = new[] { new PopulationSection { Title = t("reallife_ui_page_details", "Details"), Lines = Pairs(v.Details, t, money, place) } };
            }
            if (!string.IsNullOrEmpty(v.Primary) && v.PrimaryTarget.Valid)
            {
                m.HasAction = true;
                m.Action = new UiActionModel { Label = t(v.Primary, v.Primary), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiAppWindow.ActionCommand };
            }
            return Prompts(m, v, t, true);
        }

        // The prompt bar: Select only where a row opens something.
        private static PopulationModel Prompts(PopulationModel m, PopulationView v, UiWords t, bool page)
        {
            bool leads = false;
            for (int i = 0; i < v.Rows.Count; i++)
            {
                leads |= v.Rows[i].Target.Valid;
            }
            var prompts = new List<UiPrompt>(4) { new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") } };
            if (leads && page)
            {
                prompts.Add(new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") });
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

        // The city at a glance: its four figures across; its districts and facts follow.
        private static void City(PopulationModel m, PopulationView v, UiWords t, Func<long, string> money)
        {
            int count = Math.Min(MostFigures, v.Hero.Count);
            m.Figures = new UiFigureModel[count];
            for (int i = 0; i < count; i++)
            {
                m.Figures[i] = new UiFigureModel { Label = t(v.Hero[i].Label, v.Hero[i].Label), Value = Write(v.Hero[i], t, money) };
            }
        }

        // A place's head: its glyph on a tile, its name, where it stands, and its key figures.
        private static void Place(PopulationModel m, PopulationView v, string title, UiWords t, Func<long, string> money, Func<string, string> place)
        {
            string role = string.IsNullOrEmpty(v.Subtitle) ? string.Empty : Clean(t(v.Subtitle, v.Subtitle), place);
            var head = new UiProfileModel
            {
                Name = title, Glyph = GlyphOf(v.Kind), Role = role, RoleIcon = role.Length > 0 ? UiIcon.MapPin : UiIcon.None, Rating = -1
            };
            Facts(head, v.Hero, false, t, money);
            m.Head = head;
        }

        // A household's head: its family's name and class; its month and its home stand as the facts beside its people.
        private static void Household(PopulationModel m, PopulationView v, UiWords t, Func<long, string> money, Func<int, bool, string> nameOf, Func<string, string> place)
        {
            string role = string.IsNullOrEmpty(v.Subtitle) ? string.Empty : Clean(t(v.Subtitle, v.Subtitle), place);
            m.Head = new UiProfileModel { Name = Household(v, t, nameOf), Glyph = UiIcon.Home, Role = role, RoleIcon = role.Length > 0 ? UiIcon.People : UiIcon.None, Rating = -1 };
            m.Sections = new[]
            {
                new PopulationSection { Title = t("reallife_ui_pop_last_month", "Last month"), Lines = Pairs(v.Hero, t, money, place) },
                new PopulationSection { Title = t("reallife_ui_pop_home", "Home and money"), Lines = Pairs(v.Details, t, money, place) }
            };
        }

        // A person as a profile: their face and name, their state, their age and pay and hours. Pay and hours of nothing are left out:
        // the state already says why.
        private static void Person(PopulationModel m, PopulationView v, string name, UiWords t, Func<long, string> money)
        {
            for (int i = 0; i < v.Hero.Count; i++)
            {
                Traits(name, v.NameSeed, v.Female, v.Hero[i].Format, v.Hero[i].Value);   // their age, from the figure that says it
            }
            var head = new UiProfileModel
            {
                Name = name, Hue = UiColour.HueOf(name), Ring = UiRole.None, Role = string.IsNullOrEmpty(v.Subtitle) ? string.Empty : t(v.Subtitle, v.Subtitle),
                RoleIcon = string.IsNullOrEmpty(v.Subtitle) ? UiIcon.None : UiIcon.Person, Rating = -1
            };
            Facts(head, v.Hero, true, t, money);
            m.Head = head;
        }

        private static void Facts(UiProfileModel head, List<ViewFigure> figures, bool skipNothing, UiWords t, Func<long, string> money)
        {
            var labels = new List<string>(4);
            var values = new List<string>(4);
            for (int i = 0; i < figures.Count && labels.Count < MostFigures; i++)
            {
                if (!skipNothing || figures[i].Value != 0 || figures[i].Format == ViewFormat.Years)
                {
                    labels.Add(t(figures[i].Label, figures[i].Label));
                    values.Add(Write(figures[i], t, money));
                }
            }
            head.FactLabels = labels.ToArray();
            head.FactValues = values.ToArray();
        }

        private static UiIcon GlyphOf(ViewKind kind)
        {
            switch (kind)
            {
                case ViewKind.District: return UiIcon.MapTrifold;
                case ViewKind.Building: return UiIcon.Building;
                case ViewKind.Business: return UiIcon.Store;
                case ViewKind.Contributors: return UiIcon.Briefcase;
                default: return UiIcon.ChartPie;   // what the mod's own businesses take from the people
            }
        }

        // What a row of a view's list stands for: the places it leads to, as the glyph on the row's tile.
        private static UiIcon TileOf(ViewKind kind)
        {
            switch (kind)
            {
                case ViewKind.City:
                case ViewKind.Business: return UiIcon.MapTrifold;     // districts
                case ViewKind.District: return UiIcon.Building;       // buildings
                case ViewKind.Contributors: return UiIcon.Store;      // employers
                default: return UiIcon.Chart;                         // a business type's demand
            }
        }

        // The rows a press opens as the big table's: places on tiles, people by their faces, each of the view's figures in a column of its own.
        private static UiDataTableModel List(PopulationView v, UiWords t, Func<long, string> money, Func<int, bool, string> nameOf, Func<string, string> place)
        {
            bool wide = v.Columns != null && v.ColumnFormats != null && v.Columns.Length == v.ColumnFormats.Length;
            bool people = false;
            for (int i = 0; i < v.Rows.Count; i++)
            {
                people |= v.Rows[i].NameSeed != 0;
            }
            int figures = wide ? v.Columns.Length : 1;
            var columns = new UiDataColumn[1 + figures];
            columns[0] = new UiDataColumn { Title = string.IsNullOrEmpty(v.RowsHeader) ? string.Empty : t(v.RowsHeader, v.RowsHeader), Kind = people ? UiCellKind.Person : UiCellKind.Tile };
            for (int c = 0; c < figures; c++)
            {
                ViewFormat format = wide ? v.ColumnFormats[c] : v.Rows.Count > 0 ? v.Rows[0].Format : ViewFormat.Count;
                string head = wide ? t(v.Columns[c], v.Columns[c]) : PeopleCounted(v.Kind) ? t("reallife_pop_fig_people", "People") : string.Empty;
                columns[c + 1] = new UiDataColumn
                {
                    Title = head, Width = FigureColumn,
                    Kind = format == ViewFormat.Money ? UiCellKind.Money : format == ViewFormat.Key ? UiCellKind.Text : UiCellKind.Figure
                };
            }
            var rows = new UiDataRow[v.Rows.Count];
            for (int i = 0; i < rows.Length; i++)
            {
                ViewRow r = v.Rows[i];
                string name = Title(r.Title, r.TitleKey, r.NameSeed, r.Female, t, nameOf, place);
                var cells = new string[columns.Length];
                cells[0] = name;
                if (wide && r.Figures != null)
                {
                    for (int c = 0; c < figures; c++)
                    {
                        cells[c + 1] = c < r.Figures.Length ? Write(ViewFigure.Of(null, r.Figures[c], v.ColumnFormats[c]), t, money) : string.Empty;
                    }
                }
                else
                {
                    string figure = Write(ViewFigure.Of(null, r.Value, r.Format), t, money);
                    cells[1] = r.Format == ViewFormat.Years ? t("reallife_ui_pop_age", "Age {age}").Replace("{age}", figure) : figure;
                }
                var values = new int[columns.Length];
                values[0] = people ? 0 : (int)TileOf(v.Kind);
                string sub = string.IsNullOrEmpty(r.Subtitle) ? null : Clean(t(r.Subtitle, r.Subtitle), place);
                rows[i] = new UiDataRow { Cells = cells, Values = values, Tones = new UiRole[columns.Length], Sub = sub };
                if (people)
                {
                    Traits(name, r.NameSeed, r.Female, r.Format, r.Value);
                    rows[i].Hue = UiColour.HueOf(name);
                    rows[i].Ring = UiRole.None;
                }
            }
            return new UiDataTableModel
            {
                Columns = columns, Rows = rows, RowCount = rows.Length, Selected = -1,   // a list that opens other pages marks nothing: the ring says where the keys are
                EmptyTitle = Clean(t(v.Empty ?? "reallife_ui_page_empty", "Nothing here yet"), place), EmptyIcon = people ? UiIcon.People : UiIcon.Info
            };
        }

        // A district's buildings and a building's households each carry the people who live there.
        private static bool PeopleCounted(ViewKind kind) => kind == ViewKind.District || kind == ViewKind.Building;

        private static UiPairModel[] Pairs(List<ViewFigure> figures, UiWords t, Func<long, string> money, Func<string, string> place)
        {
            var pairs = new UiPairModel[figures.Count];
            for (int i = 0; i < pairs.Length; i++)
            {
                pairs[i] = new UiPairModel { Key = t(figures[i].Label, figures[i].Label), Value = Clean(Write(figures[i], t, money), place) };
            }
            return pairs;
        }

        // The sex, and the age where a figure says it, that the living population records for a person it named: their painted
        // face follows them (UiFaceRules), not the name's hash. Told again with the same facts it changes nothing.
        private static void Traits(string name, int nameSeed, bool female, ViewFormat format, long value)
        {
            if (nameSeed != 0 && !string.IsNullOrEmpty(name))
            {
                UiFaceBook.KnowTraits(name, female, format == ViewFormat.Years ? (int)Math.Min(value, 120L) : 0);
            }
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
