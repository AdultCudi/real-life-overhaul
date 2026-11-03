using System;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    public static readonly string[] NewsEconomyStates = { "overview", "prices", "jobs", "districts", "markets", "finances", "insights", "quiet" };
    public static readonly string[] NewsPeopleStates = { "people", "households", "jobs", "rent", "demand" };
    public static readonly string[] NewsStandStates = { "titles", "reading", "offline", "outage" };
    public static readonly string[] NewsPaperStates = { "busy", "quiet", "markets", "special" };
    public static readonly string[] NewsEndingStates = { "card" };
    public static readonly string[] NewsHomeStates = { "home" };
    public static readonly string[] NewsRentStates = { "renters" };

    public static NewsModel NewsEconomy(UiWords t, string state)
    {
        var m = new NewsModel { App = NewsApp.Economy, Icon = UiIcon.Chart, Title = t("reallife_econapp_app", "Economy"), Nav = NewsNav(t) };
        if (state == "quiet")
        {
            m.Shape = NewsShape.Notice;
            m.EmptyTitle = t("reallife_news_empty_city", "The city has not reported");
            m.EmptyBody = t("reallife_news_empty_city_body", "Figures appear after the first morning the economy runs.");
            m.EmptyIcon = UiIcon.Chart;
            m.Prompts = NewsPrompts(t);
            return m;
        }
        m.Tab = state == "prices" ? 1 : state == "jobs" ? 2 : state == "districts" ? 3 : state == "markets" ? 4 : state == "finances" ? 5 : state == "insights" ? 6 : 0;
        m.Shape = (NewsShape)m.Tab;
        m.Chip = t("reallife_econapp_verdict_slowing", "Slowing");
        m.ChipTone = UiRole.WarnText;
        m.Lead = t("reallife_news_lead", "The economy is cooling");
        m.Body = t("reallife_news_lead_body", "Prices are rising faster than wages. Shoppers are cutting back on treats.");
        m.Figures = NewsFigs();
        m.Chart = NewsChart(t, state == "jobs" || state == "finances" ? 30 : 24);
        m.Bands = new[]
        {
            new NewsBand { Share = 0.45f, Tint = UiRole.Ok, Label = t("reallife_macro_state_boom", "Growth") },
            new NewsBand { Share = 0.35f, Tint = UiRole.Warn, Label = t("reallife_macro_state_steady", "Peak") },
            new NewsBand { Share = 0.2f, Tint = UiRole.Danger, Label = t("reallife_macro_state_slowdown", "Slowdown") }
        };
        m.Marks = state == "overview" ? new[] { 8, 20 } : new int[0];
        m.RiseLine = t("reallife_news_rise", "The bank rate rose on day 240 and day 268.");
        m.AdviceTitle = t("reallife_news_advice", "What it means for you");
        m.Advice = state == "overview" || state == "insights" ? NewsAdvice(t) : new NewsAdvice[0];
        if (state == "overview")
        {
            m.Actions = new[]
            {
                new UiActionModel { Label = t("reallife_news_range_30", "30 days"), Kind = UiButtonKind.Secondary },
                new UiActionModel { Label = t("reallife_news_range_120", "120 days"), Kind = UiButtonKind.Chip },
                new UiActionModel { Label = t("reallife_news_range_year", "A year"), Kind = UiButtonKind.Secondary }
            };
        }
        m.Table = state == "districts" ? NewsDistricts(t) : NewsGoods(t);
        m.Table2 = state == "overview" ? NewsDistricts(t) : null;
        if (state == "markets")
        {
            m.Note = t("reallife_news_hold_body", "You hold 1,200 shares. Worth $64,104, up $11,185 since you bought.");
            m.Table = NewsGoods(t);
        }
        m.Prompts = NewsPrompts(t);
        return m;
    }

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
                    new UiInfoRow { Label = t("reallife_news_head_jobs", "Jobs"), Value = "96,800" },
                    new UiInfoRow { Label = t("reallife_news_head_rent", "Mean rent"), Value = "$2,140" }
                },
                ChartTitle = t("reallife_news_ages", "Age groups"),
                ChartLabels = new[] { t("reallife_news_age_young", "Under 25"), t("reallife_news_age_work", "25 to 54"), t("reallife_news_age_older", "55 and over") },
                ChartValues = new[] { "22%", "58%", "20%" },
                ChartShares = new[] { 0.22f, 0.58f, 0.20f },
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

    public static NewsModel NewsStand(UiWords t, string state)
    {
        var m = new NewsModel
        {
            App = NewsApp.Stand,
            Icon = UiIcon.Newspaper,
            Title = t("reallife_ledger_online_app", "Newsstand"),
            Across = true,
            Nav = new[]
            {
                new UiNavEntry { Icon = UiIcon.Newspaper, Label = t("reallife_ledger_online_tab_titles", "Titles") },
                new UiNavEntry { Icon = UiIcon.Books, Label = t("reallife_ledger_online_tab_reading", "Reading") }
            },
            Prompts = NewsPrompts(t)
        };
        if (state == "offline" || state == "outage")
        {
            m.Shape = NewsShape.Notice;
            m.EmptyTitle = t(state == "outage" ? "reallife_news_stand_out" : "reallife_news_stand_off", state == "outage" ? "The network is in an outage" : "The home connection is down");
            m.EmptyBody = t(state == "outage" ? "reallife_news_stand_out_body" : "reallife_news_stand_off_body", "Papers you have paid for are kept. They arrive again when the connection is back.");
            m.Note = m.EmptyBody;
            return m;
        }
        if (state == "reading")
        {
            m.Tab = 1;
            m.Shape = NewsShape.Reading;
            m.Reader = new UiReaderModel
            {
                Title = t("reallife_ledger_title", "City Ledger"),
                Blocks = new[]
                {
                    new UiReaderBlock { Kind = UiReaderKind.Heading, Text = t("reallife_news_lead", "The economy is cooling") },
                    new UiReaderBlock { Kind = UiReaderKind.Paragraph, Text = t("reallife_news_lead_body", "Prices are rising faster than wages. Shoppers are cutting back on treats.") }
                }
            };
            return m;
        }
        m.Shape = NewsShape.Stand;
        m.Note = t("reallife_news_stand_sum", "2 subscriptions, $11 a week");
        m.Note2 = t("reallife_news_stand_save", "The Pass saves $6 a week");
        m.Catalogue = new UiCatalogueModel
        {
            CardCount = 5,
            Cards = new[]
            {
                Card(t, "reallife_ledger_online_name_ledger", "City Ledger", "$6", true),
                Card(t, "reallife_ledger_online_name_exchange", "The Exchange", "$5", true),
                Card(t, "reallife_ledger_online_name_street", "Street", "$3", false),
                Card(t, "reallife_ledger_online_name_review", "Review", "$3", false),
                Card(t, "reallife_ledger_online_name_pass", "Pass", "$12", false)
            },
            Chosen = 0,
            DetailKicker = t("reallife_ledger_online_name_ledger", "City Ledger"),
            DetailNote = t("reallife_news_stand_renew", "Renews day 84"),
            Actions = new[]
            {
                new UiActionModel { Label = t("reallife_news_stand_sub", "Subscribe"), Kind = UiButtonKind.Primary, Key = UiAction.Alt },
                new UiActionModel { Label = t("reallife_news_stand_stop", "Stop"), Kind = UiButtonKind.Secondary, Key = UiAction.Option }
            }
        };
        return m;
    }

    public static NewsModel NewsPaper(UiWords t, string state)
    {
        bool quiet = state == "quiet";
        var m = new NewsModel
        {
            App = NewsApp.Paper,
            Icon = UiIcon.Newspaper,
            Title = t("reallife_ledger_title", "City Ledger") + "  3  1102",
            Shape = NewsShape.Paper,
            Prompts = NewsPrompts(t),
            Edition = new UiEditionModel
            {
                LeadIcon = UiIcon.Newspaper,
                LeadKicker = t("reallife_news_sec_city", "City"),
                LeadHeadline = quiet ? t("reallife_news_paper_quiet", "A quiet morning") : t("reallife_news_lead", "The economy is cooling"),
                LeadStandfirst = quiet
                    ? t("reallife_news_paper_quiet_body", "The city had little to report. The next edition is at 06:00.")
                    : t("reallife_news_lead_body", "Prices are rising faster than wages."),
                Markets = NewsFigs(),
                QuietLine = quiet ? t("reallife_news_paper_quiet_body", "The city had little to report. The next edition is at 06:00.") : null,
                Sections = quiet ? new UiEditionSection[0] : new[]
                {
                    new UiEditionSection
                    {
                        Icon = UiIcon.Store,
                        Label = t("reallife_news_sec_business", "Business"),
                        Stories = new[]
                        {
                            new UiEditionStory
                            {
                                Headline = t("reallife_news_open_title", "A new business opens"),
                                Body = t("reallife_news_open_body", "4 businesses opened in the city this month."),
                                Byline = "Midtown"
                            }
                        }
                    }
                }
            }
        };
        return m;
    }

    public static NewsModel NewsEnding(UiWords t, string state)
    {
        return new NewsModel
        {
            App = NewsApp.Ending,
            Shape = NewsShape.Ending,
            Decision = new UiDecisionModel
            {
                Kicker = t("reallife_news_end_kicker", "A life in the city"),
                Title = "Jordan Hale",
                Emblem = UiIcon.Person,
                Body = t("reallife_news_end_body", "After 412 days, the businesses and the people employed are what the life leaves."),
                Choices = new[]
                {
                    new UiDecisionChoice
                    {
                        Key = UiAction.Pick1,
                        Name = t("reallife_news_end_load", "Load the backup"),
                        Lines = new[] { new UiConsequence { Text = t("reallife_news_end_days", "Days in the city") + " 412", Tone = 0 } }
                    },
                    new UiDecisionChoice
                    {
                        Key = UiAction.Pick2,
                        Name = t("reallife_news_end_quit", "Quit to the desktop"),
                        Lines = new[] { new UiConsequence { Text = t("reallife_news_end_worth", "Net worth") + " $4.2m", Tone = 0 } }
                    }
                },
                Prompts = NewsPrompts(t),
                PromptCount = 3
            }
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

    private static UiFigureModel[] NewsFigs()
    {
        return new[]
        {
            new UiFigureModel { Label = "Inflation", Value = "6.0%", Change = "0.6 in a month", Rising = true, Good = false },
            new UiFigureModel { Label = "Bank rate", Value = "4.00%", Change = "0.50 in a month", Rising = true, Good = false },
            new UiFigureModel { Label = "Jobless", Value = "5.9%", Change = "0.4 in a month", Rising = true, Good = false }
        };
    }

    private static UiChartModel NewsChart(UiWords t, int n)
    {
        var a = new float[n];
        var b = new float[n];
        var c = new float[n];
        var points = new string[n];
        for (int i = 0; i < n; i++)
        {
            a[i] = 3.2f + i * 0.12f;
            b[i] = 3f + (i / 8) * 0.5f;
            c[i] = 2.6f + i * 0.04f;
            points[i] = (240 + i).ToString();
        }
        return new UiChartModel
        {
            Kind = UiChartKind.Line,
            Title = t("reallife_news_chart", "Prices, the bank rate and wages"),
            Points = points,
            Series = new[]
            {
                new UiSeries { Label = t("reallife_news_series_inflation", "Inflation"), Values = a, Tint = UiRole.Danger },
                new UiSeries { Label = t("reallife_news_series_rate", "Bank rate"), Values = b, Tint = UiRole.Accent },
                new UiSeries { Label = t("reallife_news_series_wages", "Wages"), Values = c, Tint = UiRole.Ok }
            },
            Format = v => v.ToString("0.0")
        };
    }

    private static NewsAdvice[] NewsAdvice(UiWords t)
    {
        return new[]
        {
            new NewsAdvice { Icon = UiIcon.People, Title = t("reallife_news_pay_title", "Expect pay talks"),
                Body = t("reallife_news_pay_body", "4 of your staff are paid under the going rate."),
                Link = t("reallife_news_open_staff", "Staff"), Target = 1 },
            new NewsAdvice { Icon = UiIcon.Bank, Title = t("reallife_news_fix_title", "Fix your loan's rate now"),
                Body = t("reallife_news_fix_body", "A fixed rate saves $860 a month."),
                Link = t("reallife_news_fix_do", "Fix rate"), Target = 4 },
            new NewsAdvice { Icon = UiIcon.MapPin, Title = t("reallife_news_pull_title", "Hold off on this district"),
                Body = t("reallife_news_pull_body", "Its pull fell 4 points this week."),
                Link = t("reallife_news_open_map", "Map"), Target = 3 },
            new NewsAdvice { Icon = UiIcon.ChartLineUp, Title = t("reallife_news_def_title", "Defensive shares hold up"),
                Body = t("reallife_news_def_body", "Food and utilities moved less than luxury."),
                Link = t("reallife_news_open_stocks", "Stocks"), Target = 2 }
        };
    }

    private static UiDataTableModel NewsGoods(UiWords t)
    {
        return new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("reallife_news_col_good", "Good"), Kind = UiCellKind.Text },
                new UiDataColumn { Title = t("reallife_news_col_today", "Price"), Kind = UiCellKind.Text, Width = 96f },
                new UiDataColumn { Title = t("reallife_news_col_month", "In a month"), Kind = UiCellKind.Trend, Width = 128f },
                new UiDataColumn { Title = t("reallife_news_col_spark", "120 days"), Kind = UiCellKind.Spark, Width = 96f }
            },
            Rows = new[]
            {
                Good("Burgers", "$8.40", "+6.2%", 1, new[] { 7.6f, 7.8f, 8.0f, 8.1f, 8.4f }),
                Good("Coffee", "$3.10", "+1.4%", 1, new[] { 3.0f, 3.02f, 3.05f, 3.08f, 3.10f }),
                Good("Clothes", "$34.00", "+1.0%", 1, new[] { 33.2f, 33.4f, 33.6f, 33.8f, 34f }),
                Good("Electronics", "$410", "-2.4%", -1, new[] { 430f, 424f, 418f, 414f, 410f })
            },
            RowCount = 4,
            EmptyIcon = UiIcon.Tag
        };
    }

    private static UiDataTableModel NewsDistricts(UiWords t)
    {
        return new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("reallife_news_col_district", "District"), Kind = UiCellKind.Text },
                new UiDataColumn { Title = t("reallife_news_col_pull", "Pull"), Kind = UiCellKind.Meter, Width = 160f },
                new UiDataColumn { Title = t("reallife_news_col_week", "Week"), Kind = UiCellKind.Trend, Width = 112f },
                new UiDataColumn { Title = t("reallife_news_col_rent", "Rent"), Kind = UiCellKind.Text, Width = 96f }
            },
            Rows = new[]
            {
                District("Midtown", "92", 92, "+3", 1, "$148"),
                District("Murray Hill", "81", 81, "+1", 1, "$96"),
                District("Garment District", "74", 74, "-4", -1, "$88"),
                District("Hell's Kitchen", "68", 68, "-2", -1, "$71")
            },
            RowCount = 4,
            EmptyIcon = UiIcon.MapTrifold
        };
    }

    private static UiDataRow Good(string name, string price, string month, int way, float[] line)
    {
        return new UiDataRow
        {
            Cells = new[] { name, price, month, string.Empty },
            Values = new[] { 0, 0, way, 0 },
            Tones = new[] { UiRole.None, UiRole.None, UiRole.Danger, way < 0 ? UiRole.Ok : UiRole.Danger },
            Line = line
        };
    }

    private static UiDataRow District(string name, string pull, int meter, string week, int way, string rent)
    {
        return new UiDataRow
        {
            Cells = new[] { name, pull, week, rent },
            Values = new[] { 0, meter, way, 0 },
            Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None }
        };
    }

    private static UiCatalogueCard Card(UiWords t, string key, string english, string price, bool on)
    {
        return new UiCatalogueCard { Icon = UiIcon.Newspaper, Name = t(key, english), Figure = price, State = on ? t("reallife_news_stand_on", "On") : null, StateTone = UiRole.Ok };
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
