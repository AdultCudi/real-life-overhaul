using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The city's people pages as PopulationPages makes them from the views: sample models for the offline previews and EconomyCheck's layout
// checks. Fixtures, never shipped: the mod's screen builds its models from the live population. Words a page labels with go through the
// given text source, so the checks measure every language; the names, places and figures stay as the fixtures have them.
internal static partial class UiSamples
{
    /// <summary>The population page states the previews and the checks draw.</summary>
    public static readonly string[] PopulationStates = { "city", "contributors", "district", "building", "household", "person", "person_alone", "business", "gone" };

    /// <summary>The population pages as previews: each state with the d-pad's walk the focus path shot draws.</summary>
    public static List<Sample> Population(UiWords t, string lang = "en")
    {
        string tag = lang == "en" ? string.Empty : (lang == "zh-cn" ? "zh" : lang) + "_";
        var list = new List<Sample>(PopulationStates.Length);
        foreach (string state in PopulationStates)
        {
            string st = state;
            list.Add(new Sample
            {
                Name = st == "city" ? "population" + (tag.Length > 0 ? "_" + tag.TrimEnd('_') : string.Empty) : "population_" + tag + st, App = "The city's people",
                    Walk = st == "household" ? "RDDUL" : st == "gone" ? "D" : "DDU",
                Lay = (s, f, d, k) => PopulationFace.Lay(s, PopulationPage(t, st), f, d, k)
            });
        }
        return list;
    }

    private static string Age(UiWords t, string years) => t("reallife_ui_pop_age", "Age {age}").Replace("{age}", years);

    private static UiPairModel Pair(UiWords t, string key, string english, string value) => new UiPairModel { Key = t(key, english), Value = value };

    private static UiDataColumn Column(string title, UiCellKind kind, float width = 0f) => new UiDataColumn { Title = title, Kind = kind, Width = width };

    private static UiDataRow Place(UiIcon icon, string name, string sub, params string[] figures)
    {
        var cells = new string[figures.Length + 1];
        cells[0] = name;
        figures.CopyTo(cells, 1);
        var values = new int[cells.Length];
        values[0] = (int)icon;
        return new UiDataRow { Cells = cells, Values = values, Tones = new UiRole[cells.Length], Sub = sub };
    }

    private static UiDataRow Face(string name, float hue, string sub, string figure) =>
        new UiDataRow { Cells = new[] { name, figure }, Values = new int[2], Tones = new UiRole[2], Sub = sub, Hue = hue, Ring = UiRole.None };

    private static UiDataTableModel Rows(string empty, UiDataColumn[] columns, params UiDataRow[] rows) =>
        new UiDataTableModel { Columns = columns, Rows = rows, RowCount = rows.Length, Selected = -1, EmptyTitle = empty };

    private static UiProfileModel Head(string name, UiIcon glyph, string role, UiIcon roleIcon, params string[] facts)
    {
        var labels = new List<string>();
        var values = new List<string>();
        for (int i = 0; i + 1 < facts.Length; i += 2)
        {
            labels.Add(facts[i]);
            values.Add(facts[i + 1]);
        }
        return new UiProfileModel { Name = name, Glyph = glyph, Role = role, RoleIcon = roleIcon, FactLabels = labels.ToArray(), FactValues = values.ToArray(), Rating = -1 };
    }

    private static PopulationModel Prompted(PopulationModel m, UiWords t, bool select)
    {
        var prompts = new List<UiPrompt> { new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") } };
        if (select)
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

    private static void Action(PopulationModel m, UiWords t, string key, string english)
    {
        m.HasAction = true;
        m.Action = new UiActionModel { Label = t(key, english), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiAppWindow.ActionCommand };
    }

    /// <summary>One state of the page: the city, who contributes, a district, a building, a household, a person (living alone or not), a business, one who moved on.</summary>
    public static PopulationModel PopulationPage(UiWords t, string state)
    {
        string details = t("reallife_ui_page_details", "Details");
        float figure = UiTokens.S16 * 2f + UiTokens.S8;
        var m = new PopulationModel();
        switch (state)
        {
            case "contributors":
                m.Head = Head(t("reallife_pop_view_contributors", "Who contributes"), UiIcon.Briefcase, string.Empty, UiIcon.None,
                    t("reallife_pop_fig_your_people", "People you employ"), "0%", t("reallife_pop_fig_your_spending", "Spending at your businesses"), "0%",
                    t("reallife_pop_fig_your_taxes", "Taxes from your businesses"), "0%");
                m.List = Rows("Nobody pays wages in the city yet.", new[] { Column(t("reallife_pop_rows_employers", "Employers paying most"), UiCellKind.Tile), Column(string.Empty,
                    UiCellKind.Money, figure) },
                    Place(UiIcon.Store, "70 Fifth Avenue", t("reallife_pop_employer_public", "The public sector"), "$1,112,059"),
                    Place(UiIcon.Store, "17 Seventh Street", t("reallife_pop_employer_tenants", "The firms in an office building"), "$895,271"),
                    Place(UiIcon.Store, "12 Broadway", t("reallife_pop_employer_tenants", "The firms in an office building"), "$511,793"),
                    Place(UiIcon.Store, "Bloom Café", t("reallife_pop_employer_business", "A business"), "$433,549"),
                    Place(UiIcon.Store, "6 Second Avenue", t("reallife_pop_employer_special", "A city service"), "$410,923"),
                    Place(UiIcon.Store, "13 Third Avenue", t("reallife_pop_employer_tenants", "The firms in an office building"), "$379,776"),
                    Place(UiIcon.Store, "1 First Avenue", t("reallife_pop_employer_tenants", "The firms in an office building"), "$350,644"),
                    Place(UiIcon.Store, "37 Fourth Avenue", t("reallife_pop_employer_tenants", "The firms in an office building"), "$323,109"));
                m.Sections = new[]
                {
                    new PopulationSection
                    {
                        Title = details, Lines = new[] { Pair(t, "reallife_pop_fig_top_district", "District that spends most", "Midtown"), Pair(t, "reallife_pop_fig_spent", "Spent",
                            "$1,158,079") }
                    }
                };
                Action(m, t, "reallife_pop_act_top_district", "The district that spends most");
                return Prompted(m, t, true);
            case "district":
                m.Head = Head("Midtown", UiIcon.MapTrifold, string.Empty, UiIcon.None, t("reallife_pop_fig_people", "People"), "8,213", t("reallife_pop_fig_unemployment", "Out of work"), "3.9%",
                    t("reallife_pop_fig_rent", "Rent a day"), "$118", t("reallife_pop_fig_vacant", "Flats free"), "2.1%");
                m.List = Rows("Nobody lives in this district.", new[] { Column(t("reallife_pop_rows_buildings", "Fullest buildings"), UiCellKind.Tile), Column(t("reallife_pop_fig_people",
                    "People"), UiCellKind.Figure, figure) },
                    Place(UiIcon.Building, "70 Fifth Avenue", null, "412"), Place(UiIcon.Building, "17 Seventh Street", null, "388"), Place(UiIcon.Building, "12 Broadway", null, "341"),
                    Place(UiIcon.Building, "6 Second Avenue", null, "297"), Place(UiIcon.Building, "13 Third Avenue", null, "262"), Place(UiIcon.Building, "1 First Avenue", null, "240"),
                    Place(UiIcon.Building, "37 Fourth Avenue", null, "219"), Place(UiIcon.Building, "9 Sixth Avenue", null, "198"));
                m.Sections = new[]
                {
                    new PopulationSection
                    {
                        Title = details, Lines = new[]
                        {
                            Pair(t, "reallife_pop_fig_households", "Households", "2,960"), Pair(t, "reallife_pop_fig_income", "Income a day", "$212"), Pair(t, "reallife_pop_fig_jobs",
                                "Jobs", "18,402"),
                            Pair(t, "reallife_pop_fig_births", "Births last month", "7"), Pair(t, "reallife_pop_fig_deaths", "Deaths last month", "3"), Pair(t, "reallife_pop_fig_taxes",
                                "Taxes last month", "$184,200")
                        }
                    }
                };
                Action(m, t, "reallife_pop_act_busiest", "The busiest business here");
                return Prompted(m, t, true);
            case "building":
                m.Head = Head("70 Fifth Avenue", UiIcon.Building, "Midtown", UiIcon.MapPin, t("reallife_pop_fig_flats", "Flats"), "120", t("reallife_pop_fig_people", "People"), "412",
                    t("reallife_pop_fig_free", "Free"), "6");
                m.List = Rows("Nobody lives in this building.", new[] { Column(t("reallife_pop_rows_households", "Who lives here"), UiCellKind.Person), Column(t("reallife_pop_fig_people",
                    "People"), UiCellKind.Figure, figure) },
                    Face("John Stanley", 25f, t("reallife_pop_class_middle", "Middle class"), "3"), Face("Maria Alvarez", 140f, t("reallife_pop_class_working", "Working class"), "4"),
                    Face("Daniel Okafor", 200f, t("reallife_pop_class_upper", "Upper class"), "2"), Face("Priya Nair", 300f, t("reallife_pop_class_middle", "Middle class"), "1"),
                    Face("Tom Becker", 60f, t("reallife_pop_class_working", "Working class"), "5"), Face("Lena Fischer", 330f, t("reallife_pop_class_middle", "Middle class"), "2"),
                    Face("Omar Haddad", 100f, t("reallife_pop_class_working", "Working class"), "3"), Face("Sofia Rossi", 240f, t("reallife_pop_class_upper", "Upper class"), "2"));
                m.Sections = new[]
                {
                    new PopulationSection
                    {
                        Title = details, Lines = new[] { Pair(t, "reallife_pop_fig_flat_rent", "A flat's rent a day", "$62"), Pair(t, "reallife_pop_fig_flat_size", "A flat's size in m2", "58") }
                    }
                };
                return Prompted(m, t, true);
            case "household":
                m.Head = Head(t("reallife_ui_pop_household", "The {surname} household").Replace("{surname}", "Stanley"), UiIcon.Home, t("reallife_pop_class_middle", "Middle class"),
                    UiIcon.People);
                m.List = Rows("This household has moved on.", new[] { Column(t("reallife_pop_rows_members", "Its people"), UiCellKind.Person), Column(string.Empty, UiCellKind.Figure, figure) },
                    Face("John Stanley", 25f, t("reallife_pop_status_worker", "In work"), Age(t, "41")), Face("Rosa Stanley", 160f, t("reallife_pop_status_home", "At home"), Age(t, "39")),
                    Face("Leo Stanley", 260f, t("reallife_pop_status_pupil", "At school"), Age(t, "12")));
                m.Sections = new[]
                {
                    new PopulationSection
                    {
                        Title = t("reallife_ui_pop_last_month", "Last month"), Lines = new[]
                        {
                            Pair(t, "reallife_pop_fig_earned", "Earned", "$3,410"), Pair(t, "reallife_pop_fig_tax_paid", "Paid in tax", "$512"),
                            Pair(t, "reallife_pop_fig_spent", "Spent", "$2,188"), Pair(t, "reallife_pop_fig_saved", "Saved", "$710")
                        }
                    },
                    new PopulationSection
                    {
                        Title = t("reallife_ui_pop_home", "Home and money"), Lines = new[]
                        {
                            Pair(t, "reallife_pop_fig_rent_paid", "Rent paid", "$1,140"), Pair(t, "reallife_pop_fig_landlord", "Rent goes to", t("reallife_pop_landlord_city",
                                "A city landlord")),
                            Pair(t, "reallife_pop_fig_savings", "Savings", "$6,920"), Pair(t, "reallife_pop_fig_debt", "Debt", "$0"),
                            Pair(t, "reallife_pop_fig_top_spend", "Spent at their favourite place today", "$38")
                        }
                    }
                };
                Action(m, t, "reallife_pop_act_where", "Where they spend most");
                return Prompted(m, t, true);
            case "person":
            case "person_alone":
                m.Head = Head("Thomas Sanders", UiIcon.None, t("reallife_pop_status_unemployed", "Looking for work"), UiIcon.Person, t("reallife_pop_fig_age", "Age"), "27");
                m.Head.Hue = 210f;
                m.Head.Ring = UiRole.None;
                UiDataColumn[] lives = { Column(t("reallife_pop_rows_lives_with", "Lives with"), UiCellKind.Person), Column(string.Empty, UiCellKind.Figure, figure) };
                m.List = state == "person"
                    ? Rows(t("reallife_pop_empty_alone", "They live alone."), lives, Face("Mary Sanders", 120f, t("reallife_pop_status_worker", "In work"), Age(t, "29")))
                    : Rows(t("reallife_pop_empty_alone", "They live alone."), lives);
                m.Sections = new[]
                {
                    new PopulationSection
                    {
                        Title = details, Lines = new[]
                        {
                            Pair(t, "reallife_pop_fig_education", "Schooling", t("reallife_pop_education_postgraduate", "Postgraduate degree")),
                            Pair(t, "reallife_pop_fig_field", "Line of work", t("reallife_pop_field_office", "Office work"))
                        }
                    }
                };
                Action(m, t, "reallife_pop_act_household", "Their household");
                return Prompted(m, t, state == "person");
            case "business":
                m.Head = Head("Bloom Café", UiIcon.Store, "Coffeeshop", UiIcon.MapPin, t("reallife_pop_fig_customers", "Customers today"), "312", t("reallife_pop_fig_takings",
                    "Takings today"), "$4,820",
                    t("reallife_pop_fig_against_first", "Against its first day"), "128%");
                m.List = Rows("No customers from the city's people today.", new[] { Column(t("reallife_pop_rows_from", "Where its customers live"), UiCellKind.Tile), Column(string.Empty,
                    UiCellKind.Money, figure) },
                    Place(UiIcon.MapTrifold, "Midtown", null, "$1,840"), Place(UiIcon.MapTrifold, "Murray Hill", null, "$1,120"), Place(UiIcon.MapTrifold, "Uptown", null, "$860"),
                    Place(UiIcon.MapTrifold, "Hell's Kitchen", null, "$610"), Place(UiIcon.MapTrifold, "Lower Manhattan", null, "$390"));
                m.Sections = new[]
                {
                    new PopulationSection
                    {
                        Title = details, Lines = new[]
                        {
                            Pair(t, "reallife_pop_fig_working", "Working class customers", "22%"), Pair(t, "reallife_pop_fig_middle", "Middle class customers", "58%"),
                            Pair(t, "reallife_pop_fig_upper", "Upper class customers", "20%"), Pair(t, "reallife_pop_fig_month", "Takings last month", "$118,400"),
                            Pair(t, "reallife_pop_fig_sales_tax", "Sales tax last month", "$9,472"), Pair(t, "reallife_pop_fig_wage_bill", "Wages last month", "$21,300"),
                            Pair(t, "reallife_pop_fig_residents", "Residents working here", "9"), Pair(t, "reallife_pop_fig_commuters", "Commuters", "3")
                        }
                    }
                };
                Action(m, t, "reallife_pop_act_staff", "Its best paid resident");
                return Prompted(m, t, true);
            case "gone":
                m.Gone = true;
                m.GoneTitle = t("reallife_ui_pop_gone_named", "{name} has moved on.").Replace("{name}", "Thomas Sanders");
                m.GoneBody = t("reallife_ui_pop_gone_body", "People leave the city when they move away or pass on. Back takes you to where you were.");
                m.Prompts = new[] { new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Back,
                    Label = t("reallife_ui_prompt_back", "Back") } };
                m.PromptCount = 2;
                return m;
            default:
                m.Figures = new[]
                {
                    new UiFigureModel { Label = t("reallife_pop_fig_people", "People"), Value = "56,666" },
                    new UiFigureModel { Label = t("reallife_pop_fig_households", "Households"), Value = "19,578" },
                    new UiFigureModel { Label = t("reallife_pop_fig_unemployment", "Out of work"), Value = "5.4%" },
                    new UiFigureModel { Label = t("reallife_pop_fig_rent", "Rent a day"), Value = "$86" },
                };
                m.List = Rows(t("reallife_ui_page_empty", "Nothing here yet"), new[]
                    {
                        Column(t("reallife_pop_rows_districts", "Districts"), UiCellKind.Tile), Column(t("reallife_pop_fig_people", "People"), UiCellKind.Figure, figure),
                        Column(t("reallife_pop_fig_households", "Households"), UiCellKind.Figure, figure), Column(t("reallife_pop_fig_unemployment", "Out of work"), UiCellKind.Figure, figure),
                        Column(t("reallife_pop_fig_rent", "Rent a day"), UiCellKind.Money, figure)
                    },
                    Place(UiIcon.MapTrifold, "Cedar Heights", null, "11,190", "3,870", "6.1%", "$74"), Place(UiIcon.MapTrifold, "Uptown", null, "9,122", "3,140", "4.8%", "$91"),
                    Place(UiIcon.MapTrifold, "Midtown", null, "8,213", "2,960", "3.9%", "$118"), Place(UiIcon.MapTrifold, "Hell's Kitchen", null, "8,148", "2,810", "5.2%", "$96"),
                    Place(UiIcon.MapTrifold, "Murray Hill", null, "5,859", "2,050", "4.4%", "$102"), Place(UiIcon.MapTrifold, "Lower Manhattan", null, "5,855", "2,020", "5.7%", "$109"),
                    Place(UiIcon.MapTrifold, "Garment District", null, "4,574", "1,560", "7.3%", "$68"), Place(UiIcon.MapTrifold, "Industry City", null, "3,601", "1,168", "8.9%", "$59"));
                m.Sections = new[]
                {
                    new PopulationSection
                    {
                        Title = details, Lines = new[]
                        {
                            Pair(t, "reallife_pop_fig_births", "Births last month", "41"), Pair(t, "reallife_pop_fig_deaths", "Deaths last month", "17"),
                            Pair(t, "reallife_pop_fig_moved_in", "Households who came", "212"), Pair(t, "reallife_pop_fig_moved_out", "Households who left", "96"),
                            Pair(t, "reallife_pop_fig_jobs", "Jobs", "109,605"), Pair(t, "reallife_pop_fig_vacancies", "Open jobs", "2,735"),
                            Pair(t, "reallife_pop_fig_commuters", "Commuters", "81,682"), Pair(t, "reallife_pop_fig_taxes", "Taxes last month", "$1,212,400"),
                            Pair(t, "reallife_pop_fig_homes_wanted", "Homes wanted a year", "840"), Pair(t, "reallife_pop_fig_homes_built", "Homes built this year", "512"),
                            Pair(t, "reallife_pop_fig_firms_opened", "Firms opened this year", "64"), Pair(t, "reallife_pop_fig_firms_closed", "Firms closed this year", "41")
                        }
                    }
                };
                Action(m, t, "reallife_pop_act_contributors", "See who contributes");
                return Prompted(m, t, true);
        }
    }
}
