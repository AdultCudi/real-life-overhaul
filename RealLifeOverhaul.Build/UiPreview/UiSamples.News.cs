using System;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    public static readonly string[] NewsEconomyStates = { "overview", "prices", "jobs", "districts", "markets", "finances", "insights", "quiet" };
    public static readonly string[] NewsPeopleStates = { "people", "households", "jobs", "rent", "demand" };
    public static readonly string[] NewsStandStates = { "titles", "reading", "offline", "outage" };
    public static readonly string[] NewsPaperStates = { "busy", "quiet", "markets", "prices", "special" };
    public static readonly string[] NewsEndingStates = { "card" };
    public static readonly string[] NewsHomeStates = { "home" };
    public static readonly string[] NewsRentStates = { "renters" };

    public static NewsModel NewsPeople(UiWords t, string state)
    {
        int view = state == "households" ? 1 : state == "jobs" ? 2 : state == "rent" ? 3 : state == "demand" ? 4 : 0;
        return new NewsModel
        {
            App = NewsApp.People,
            Shape = NewsShape.People,
            Title = t("reallife_news_people_title", "The city's people"),
            People = new UiInfoViewModel
            {
                Title = t("reallife_news_people_title", "The city's people"),
                Icon = UiIcon.People,
                Views = new[] { UiIcon.People, UiIcon.Home, UiIcon.Briefcase, UiIcon.Bank, UiIcon.Store },
                ViewTints = new[] { UiRole.Accent, UiRole.Ok, UiRole.Warn, UiRole.Chart3, UiRole.Chart2 },
                View = view,
                HeadLabel = t("reallife_news_head_people", "Residents"),
                Prompts = NewsPrompts(t),
                PromptCount = 3,
                HeadValue = "184,200",
                Rows = new[]
                {
                    new UiInfoRow { Label = t("reallife_news_head_homes", "Households"), Value = "71,400" },
                    new UiInfoRow { Label = t("reallife_pop_fig_unemployment", "Out of work"), Value = "6.4%" },
                    new UiInfoRow { Label = t("reallife_pop_fig_rent", "Mean rent"), Value = "$2,140" },
                    new UiInfoRow { Label = t("reallife_pop_fig_jobs", "Jobs"), Value = "96,800" },
                    new UiInfoRow { Label = t("reallife_pop_fig_moved_in", "Moved in this month"), Value = "312" },
                    new UiInfoRow { Label = t("reallife_pop_fig_moved_out", "Moved away this month"), Value = "278" },
                    new UiInfoRow { Label = t("reallife_news_busy_link", "The busiest district"), Value = "Midtown" }
                },
                ChartTitle = t("reallife_news_ages", "Age groups"),
                ChartLabels = new[] { t("reallife_news_age_young", "Under 25"), t("reallife_news_age_work", "25 to 54"), t("reallife_news_age_older", "55 and over") },
                ChartValues = new[] { "22%", "58%", "20%" },
                ChartShares = new[] { 0.22f, 0.58f, 0.20f },
                Chart2Title = t("reallife_news_income_title", "Incomes"),
                Chart2Labels = new[] { t("reallife_news_income_work", "Working"), t("reallife_news_income_middle", "Middle"), t("reallife_news_income_upper", "Upper") },
                Chart2Values = new[] { "41%", "46%", "13%" },
                Chart2Shares = new[] { 0.41f, 0.46f, 0.13f },
                LegendTitle = t("reallife_news_legend", "Map legend"),
                ScaleLabel = t("reallife_news_scale", "Rent by district"),
                ScaleLow = t("reallife_news_low", "Lower"),
                ScaleHigh = t("reallife_news_high", "Higher")
            },
            Actions = new[]
            {
                new UiActionModel { Label = t("reallife_news_rent_link", "Who pays the most rent"), Icon = UiIcon.Home, Command = NewsCommands.Rent },
                new UiActionModel { Label = t("reallife_news_busy_link", "The busiest district"), Icon = UiIcon.MapPin, Command = NewsCommands.Busy }
            },
            Prompts = NewsPrompts(t)
        };
    }

    public static NewsModel NewsHome(UiWords t, string state)
    {
        return new NewsModel
        {
            App = NewsApp.Household,
            Shape = NewsShape.Household,
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
                            new UiInfoRow { Label = t("reallife_news_col_rent", "Rent"), Value = "$2,480" },
                            new UiInfoRow { Label = t("reallife_news_col_district", "District"), Value = "Midtown" }
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
            Icon = UiIcon.Home,
            Title = t("reallife_news_rent_link", "Who pays the most rent"),
            Prompts = NewsPrompts(t),
            Table = new UiDataTableModel
            {
                Columns = new[]
                {
                    new UiDataColumn { Title = t("reallife_news_col_name", "Name"), Kind = UiCellKind.Text },
                    new UiDataColumn { Title = t("reallife_news_col_rent", "Rent"), Kind = UiCellKind.Text, Width = 140f }
                },
                Rows = new[]
                {
                    new UiDataRow { Cells = new[] { "Jordan Hale", "$4,800" }, Values = new int[2], Tones = new UiRole[2] },
                    new UiDataRow { Cells = new[] { "Priya Shah", "$4,200" }, Values = new int[2], Tones = new UiRole[2] },
                    new UiDataRow { Cells = new[] { "Luis Ortega", "$3,900" }, Values = new int[2], Tones = new UiRole[2] }
                },
                RowCount = 3,
                EmptyIcon = UiIcon.Home
            }
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
