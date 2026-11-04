using System;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    public static readonly string[] NewsEconomyStates = { "overview", "prices", "jobs", "districts", "markets", "finances", "insights", "quiet" };
    public static readonly string[] NewsPeopleStates = { "day60", "day5", "district" };
    public static readonly string[] NewsStandStates = { "titles", "reading", "reading_days", "reading_not_held", "reading_none", "offline", "outage" };
    public static readonly string[] NewsPaperStates = { "busy", "quiet", "markets", "prices", "special" };
    public static readonly string[] NewsEndingStates = { "card" };
    public static readonly string[] NewsHomeStates = { "home" };
    public static readonly string[] NewsRentStates = { "renters" };

    // The city's people as a page of the Economy window, in three states: a city with a month behind it (day 60), a city a few days in (day 5, the moves not yet
    // counted), and the page of one district.
    public static NewsModel NewsPeople(UiWords t, string state)
    {
        bool early = state == "day5";
        bool district = state == "district";
        string notYet = NewsFill(t("reallife_news_not_yet", "Not yet: from day {day}"), "day", "6");
        var m = new NewsModel
        {
            App = NewsApp.People,
            Shape = NewsShape.People,
            Icon = UiIcon.Chart,
            Title = t("reallife_econapp_app", "Economy"),
            Nav = NewsNav(t),
            Tab = 7,
            Prompts = NewsPrompts(t),
            StripNote = NewsFill(t("reallife_econapp_day", "Day {day}"), "day", early ? "5" : "60")
        };
        string people = t("reallife_news_people_title", "The city's people");
        var rows = district
            ? new[]
            {
                new UiInfoRow { Label = t("reallife_news_head_homes", "Households"), Value = "9,420" },
                new UiInfoRow { Label = t("reallife_pop_fig_unemployment", "Out of work"), Value = "5.2%" },
                new UiInfoRow { Label = t("reallife_pop_fig_rent", "Rent a day"), Value = "$118" },
                new UiInfoRow { Label = t("reallife_pop_fig_jobs", "Jobs"), Value = "18,300" }
            }
            : new[]
            {
                new UiInfoRow { Label = t("reallife_news_head_homes", "Households"), Value = early ? "19,740" : "71,400" },
                new UiInfoRow { Label = t("reallife_pop_fig_unemployment", "Out of work"), Value = early ? "5.4%" : "6.4%" },
                new UiInfoRow { Label = t("reallife_pop_fig_rent", "Rent a day"), Value = early ? "$96" : "$104" },
                new UiInfoRow { Label = t("reallife_pop_fig_jobs", "Jobs"), Value = early ? "110,579" : "96,800" },
                new UiInfoRow { Label = t("reallife_pop_fig_moved_in", "Households who came"), Value = early ? notYet : "312", Ink = early ? UiRole.Muted : UiRole.None },
                new UiInfoRow { Label = t("reallife_pop_fig_moved_out", "Households who left"), Value = early ? notYet : "278", Ink = early ? UiRole.Muted : UiRole.None }
            };
        string midtown = "Midtown";
        m.People = new NewsPeople
        {
            Title = district ? midtown : people,
            HeadLabel = t("reallife_news_head_people", "Residents"),
            HeadValue = district ? "26,900" : early ? "57,115" : "184,200",
            Rows = rows,
            ChartTitle = t("reallife_news_ages", "Age groups"),
            ChartLabels = new[] { t("reallife_news_age_young", "Under 25"), t("reallife_news_age_work", "25 to 54"), t("reallife_news_age_older", "55 and over") },
            ChartValues = new[] { "22%", "58%", "20%" },
            ChartShares = new[] { 0.22f, 0.58f, 0.20f },
            Chart2Title = t("reallife_news_income_title", "Incomes"),
            Chart2Labels = new[] { t("reallife_news_income_work", "Working"), t("reallife_news_income_middle", "Middle"), t("reallife_news_income_upper", "Upper") },
            Chart2Values = new[] { "41%", "46%", "13%" },
            Chart2Shares = new[] { 0.41f, 0.46f, 0.13f },
            TableTitle = district ? t("reallife_news_rent_link", "Who pays the most rent") : t("reallife_news_where", "Where people live"),
            Table = district ? NewsRenters(t, 12) : NewsWhere(t)
        };
        if (district)
        {
            m.BackLabel = people;
            m.Actions = new UiActionModel[0];
        }
        else
        {
            m.Actions = new[]
            {
                new UiActionModel { Label = t("reallife_news_rent_link", "Who pays the most rent"), Icon = UiIcon.Home, Command = NewsCommands.Rent },
                new UiActionModel { Label = t("reallife_news_busy_link", "The busiest district") + ": " + midtown, Icon = UiIcon.MapPin, Command = NewsCommands.Busy }
            };
        }
        return m;
    }

    private static UiDataTableModel NewsWhere(UiWords t)
    {
        string[][] data =
        {
            new[] { "Midtown", "26,900", "9,420", "$118" }, new[] { "Murray Hill", "21,300", "7,610", "$104" }, new[] { "Garment District", "18,800", "6,300", "$96" },
            new[] { "Cedar Heights", "17,200", "5,990", "$92" }, new[] { "Hell's Kitchen", "15,400", "5,330", "$88" }, new[] { "Uptown", "12,700", "4,460", "$131" }
        };
        var rows = new UiDataRow[data.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = new UiDataRow
            {
                Cells = data[i],
                Values = new[] { (int)UiIcon.MapPin, 0, 0, 0 },
                Tones = new[] { UiTokens.Chart(i), UiRole.None, UiRole.None, UiRole.None }
            };
        }
        return new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("reallife_news_col_district", "District"), Kind = UiCellKind.Tile },
                new UiDataColumn { Title = t("reallife_news_head_people", "Residents"), Kind = UiCellKind.Text, Width = 120f },
                new UiDataColumn { Title = t("reallife_news_head_homes", "Households"), Kind = UiCellKind.Text, Width = 120f },
                new UiDataColumn { Title = t("reallife_pop_fig_rent", "Rent a day"), Kind = UiCellKind.Text, Width = 120f }
            },
            Rows = rows,
            RowCount = rows.Length,
            EmptyIcon = UiIcon.MapTrifold
        };
    }

    private static UiDataTableModel NewsRenters(UiWords t, int count)
    {
        string[] names = { "Jordan Hale", "Priya Shah", "Luis Ortega", "Mei Tan", "Omar Haddad", "Grace Okafor", "Tomas Novak", "Aiko Mori", "Ben Carter", "Lena Fischer", "Sam Rowe", "Ana Costa" };
        var rows = new UiDataRow[Math.Min(count, names.Length)];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = new UiDataRow { Cells = new[] { names[i], "$" + (480 - i * 22).ToString("N0") }, Values = new int[2], Tones = new UiRole[2] };
        }
        return new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("reallife_news_col_name", "Name"), Kind = UiCellKind.Text },
                new UiDataColumn { Title = t("reallife_pop_fig_rent", "Rent a day"), Kind = UiCellKind.Text, Width = 140f }
            },
            Rows = rows,
            RowCount = rows.Length,
            EmptyIcon = UiIcon.Home
        };
    }

    // A page opened from the people's page: the renters, and one household of them. Each has the window's tabs and a way back.
    public static NewsModel NewsHome(UiWords t, string state)
    {
        return new NewsModel
        {
            App = NewsApp.Household,
            Shape = NewsShape.Household,
            Icon = UiIcon.Chart,
            Title = t("reallife_econapp_app", "Economy"),
            Nav = NewsNav(t),
            Tab = 7,
            StripNote = NewsFill(t("reallife_econapp_day", "Day {day}"), "day", "60"),
            BackLabel = t("reallife_news_rent_link", "Who pays the most rent"),
            Prompts = NewsPrompts(t),
            Household = new UiInfoModel
            {
                Title = "Jordan Hale",
                Icon = UiIcon.Home,
                Prompts = NewsPrompts(t),
                PromptCount = 3,
                Sections = new[]
                {
                    new UiInfoSection
                    {
                        Label = t("reallife_news_home_title", "Household"),
                        Rows = new[]
                        {
                            new UiInfoRow { Label = t("reallife_news_head_people", "Residents"), Value = "3" },
                            new UiInfoRow { Label = t("reallife_news_col_district", "District"), Value = "Midtown" },
                            new UiInfoRow { Label = t("reallife_pop_fig_rent", "Rent a day"), Value = "$480" }
                        }
                    }
                }
            }
        };
    }

    public static NewsModel NewsRent(UiWords t, string state)
    {
        return new NewsModel
        {
            App = NewsApp.Renters,
            Shape = NewsShape.Renters,
            Icon = UiIcon.Chart,
            Title = t("reallife_econapp_app", "Economy"),
            Nav = NewsNav(t),
            Tab = 7,
            StripNote = NewsFill(t("reallife_econapp_day", "Day {day}"), "day", "60"),
            BackLabel = t("reallife_news_people_title", "The city's people"),
            PageTitle = t("reallife_news_rent_link", "Who pays the most rent"),
            Prompts = NewsPrompts(t),
            Table = NewsRenters(t, 12)
        };
    }

    private static UiNavEntry[] NewsNav(UiWords t)
    {
        return new[]
        {
            new UiNavEntry { Icon = UiIcon.Chart, Label = t("reallife_news_nav_overview", "Overview") },
            new UiNavEntry { Icon = UiIcon.Tag, Label = t("reallife_news_nav_prices", "Prices") },
            new UiNavEntry { Icon = UiIcon.Briefcase, Label = t("reallife_news_nav_jobs", "Jobs") },
            new UiNavEntry { Icon = UiIcon.MapTrifold, Label = t("reallife_news_nav_districts", "Districts") },
            new UiNavEntry { Icon = UiIcon.ChartLineUp, Label = t("reallife_news_nav_markets", "Markets") },
            new UiNavEntry { Icon = UiIcon.Bank, Label = t("reallife_news_nav_finances", "Finance") },
            new UiNavEntry { Icon = UiIcon.Lightbulb, Label = t("reallife_news_nav_insights", "Insights") },
            new UiNavEntry { Icon = UiIcon.People, Label = t("reallife_news_nav_people", "People") }
        };
    }

    private static UiPrompt[] NewsPrompts(UiWords t)
    {
        return new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") }
        };
    }
}
