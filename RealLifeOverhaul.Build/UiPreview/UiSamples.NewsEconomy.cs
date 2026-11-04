using System;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    private static string NewsFill(string text, params string[] pairs)
    {
        for (int i = 0; i + 1 < pairs.Length; i += 2)
        {
            text = text.Replace("{" + pairs[i] + "}", pairs[i + 1]);
        }
        return text;
    }

    // The Economy app as the live fill writes it: a verdict, then each page's own blocks.
    public static NewsModel NewsEconomy(UiWords t, string state)
    {
        var m = new NewsModel { App = NewsApp.Economy, Icon = UiIcon.Chart, Title = t("reallife_econapp_app", "Economy"), Nav = NewsNav(t), Prompts = NewsPrompts(t) };
        m.StripNote = NewsFill(t("reallife_econapp_day", "Day {day}"), "day", "289");
        if (state == "quiet")
        {
            m.Shape = NewsShape.Notice;
            m.EmptyTitle = t("reallife_news_empty_city", "The city has not reported");
            m.EmptyBody = t("reallife_news_empty_city_body", "Figures appear after the first morning the economy runs.");
            m.EmptyIcon = UiIcon.Chart;
            return m;
        }
        m.Tab = state == "prices" ? 1 : state == "jobs" ? 2 : state == "districts" ? 3 : state == "markets" ? 4 : state == "finances" ? 5 : state == "insights" ? 6 : 0;
        m.Shape = (NewsShape)m.Tab;
        m.Chip = t("reallife_econapp_verdict_slowing", "Slowing");
        m.ChipTone = UiRole.WarnText;
        m.Lead = t("reallife_news_head_slowing", "The economy is cooling");
        m.Body = NewsFill(t("reallife_econapp_verdict_line_slowing", "Shops are {footfall} quieter than usual and customers spend {spend} less. Prices rise {inflation} a year."),
            "footfall", "8%", "spend", "4%", "inflation", "6.0%");
        string month = " " + t("reallife_news_in_month", "in a month");
        m.Figures = new[]
        {
            new UiFigureModel { Label = t("reallife_econapp_tile_inflation", "Inflation a year"), Value = "6.0%", Change = "+0.6" + month, Rising = true, Good = false },
            new UiFigureModel { Label = t("reallife_econapp_tile_rate", "Bank rate"), Value = "4.00%", Change = "+0.50" + month, Rising = true, Good = false },
            new UiFigureModel { Label = t("reallife_econapp_tile_unemployment", "Unemployment"), Value = "5.9%", Change = "+0.4 " + NewsFill(t("reallife_news_in_days", "in {days} days"), "days", "29"), Rising = true, Good = false }
        };
        m.Chart = NewsChart(t, state == "jobs" || state == "finances" ? 30 : 120, state);
        if (state == "overview")
        {
            m.Bands = new[]
            {
                new NewsBand { Share = 0.42f, Tint = UiRole.Ok, Label = t("reallife_macro_state_boom", "Growth") },
                new NewsBand { Share = 0.3f, Tint = UiRole.Warn, Label = t("reallife_macro_state_steady", "Peak") },
                new NewsBand { Share = 0.28f, Tint = UiRole.Danger, Label = t("reallife_macro_state_slowdown", "Slowdown") }
            };
            m.Marks = new[] { 38, 71, 98 };
            m.AdviceTitle = t("reallife_news_advice", "What it means for you");
            m.Advice = NewsAdvice(t);
            m.Actions = new[]
            {
                new UiActionModel { Label = t("reallife_news_range_30", "30 days"), Kind = UiButtonKind.Secondary },
                new UiActionModel { Label = t("reallife_news_range_120", "120 days"), Kind = UiButtonKind.Chip },
                new UiActionModel { Label = t("reallife_news_range_year", "A year"), Kind = UiButtonKind.Secondary }
            };
            m.Table = NewsGoods(t, false);
            m.TableTitle = t("reallife_news_prices_today", "Prices today");
            m.Table2 = NewsDistricts(t);
            m.Table2Title = t("reallife_news_districts_pull", "Districts by pull on customers");
        }
        else if (state == "insights")
        {
            m.AdviceTitle = t("reallife_news_advice", "What it means for you");
            m.Advice = NewsAdvice(t);
            m.Figures = new UiFigureModel[0];
        }
        else if (state == "prices")
        {
            NewsPricesPage(t, m);
        }
        else if (state == "markets")
        {
            NewsMarketsPage(t, m);
        }
        else if (state == "districts")
        {
            m.Table = NewsDistricts(t);
            m.TableTitle = t("reallife_news_districts_pull", "Districts by pull on customers");
        }
        else if (state == "finances")
        {
            string days = " " + NewsFill(t("reallife_news_in_days", "in {days} days"), "days", "119");
            m.Figures = new[]
            {
                new UiFigureModel { Label = t("reallife_news_series_reserve", "Reserve"), Value = "$4,210,000", Change = "+$310,000" + days, Rising = true, Good = true },
                new UiFigureModel { Label = t("reallife_news_series_revenue", "Revenue"), Value = "$612,000", Change = "+$48,000" + days, Rising = true, Good = true },
                new UiFigureModel { Label = t("reallife_news_series_spending", "Spending"), Value = "$574,000", Change = "+$61,000" + days, Rising = true, Good = false }
            };
        }
        else if (state == "jobs")
        {
            m.Figures = new UiFigureModel[0];
            m.Table = NewsJobs(t);
            m.TableTitle = null;
        }
        else
        {
            m.Figures = new UiFigureModel[0];
        }
        return m;
    }

    private static void NewsPricesPage(UiWords t, NewsModel m)
    {
        m.Chip = NewsFill(t("reallife_news_chip_led", "{good} led"), "good", "Food");
        m.ChipTone = UiRole.WarnText;
        m.Lead = NewsFill(t("reallife_news_basket", "Shopping costs {change} more than a month ago."), "change", "4.8%");
        m.Body = NewsFill(t("reallife_news_led", "Led by {good}."), "good", "Food");
        m.Figures = new[]
        {
            new UiFigureModel
            {
                Label = t("reallife_news_basket_label", "The basket"), Value = "+4.8%", Change = t("reallife_news_in_month", "in a month"), Rising = true, Good = false
            }
        };
        m.Chart = null;
        m.Bands = new NewsBand[0];
        m.Marks = new int[0];
        m.TableTitle = t("reallife_news_prices_shops", "Prices in the shops");
        m.Table = NewsGoods(t, true);
        m.SideChart = NewsGoodsChart(t);
        m.MovesTitle = t("reallife_news_moved", "Who moved prices this week");
        string rival = t("reallife_news_moved_rival", "{who} raised prices");
        string detail = t("reallife_news_moved_detail", "{firm}, day {day}");
        m.Moves = new[]
        {
            new NewsMove { Mark = "HG", Head = NewsFill(rival, "who", "Huang Guo"), Detail = NewsFill(detail, "firm", "Guo Holdings", "day", "284") },
            new NewsMove { Mark = "IB", Head = NewsFill(rival, "who", "Imani Brooks"), Detail = NewsFill(detail, "firm", "Brooks Group", "day", "286") },
            new NewsMove { Mark = "KM", Head = NewsFill(rival, "who", "Kwame Mensah"), Detail = NewsFill(detail, "firm", "Mensah Auto Group", "day", "288") },
            new NewsMove { Mark = "PL", Head = NewsFill(t("reallife_news_moved_firm_up", "{firm} raised its prices"), "firm", "Pizzeria Luigi"),
                Detail = NewsFill(t("reallife_news_moved_firm_detail", "by {percent}%, day {day}"), "percent", "6.0", "day", "287") },
            new NewsMove { Mark = "CB", Head = NewsFill(t("reallife_news_moved_firm_down", "{firm} cut its prices"), "firm", "Corner Bakery"),
                Detail = NewsFill(t("reallife_news_moved_firm_detail", "by {percent}%, day {day}"), "percent", "3.5", "day", "288") }
        };
    }

    private static void NewsMarketsPage(UiWords t, NewsModel m)
    {
        m.Lead = null;
        m.Body = null;
        m.Chip = null;
        m.Figures = new UiFigureModel[0];
        m.Chart = NewsIndexChart(t);
        m.BigValue = "1,298.9";
        m.BigChanges = new[]
        {
            NewsFill(t("reallife_news_on_day", "{change} on the day"), "change", "0.8%"),
            NewsFill(t("reallife_ledger_week", "{change} over five sessions"), "change", "2.4%")
        };
        m.BigGood = new[] { true, true };
        m.BarsTitle = t("reallife_news_sectors", "Sectors");
        m.BarsNote = t("reallife_news_five_note", "five sessions");
        m.Bars = new[]
        {
            new NewsBar { Label = "Power", Value = "+3.1%", Share = 1f },
            new NewsBar { Label = "Healthcare", Value = "+2.2%", Share = 0.71f },
            new NewsBar { Label = "Logistics", Value = "+1.9%", Share = 0.61f },
            new NewsBar { Label = "Groceries", Value = "+1.1%", Share = 0.35f },
            new NewsBar { Label = "Telecoms", Value = "-0.7%", Share = -0.23f },
            new NewsBar { Label = "Property", Value = "-1.2%", Share = -0.39f },
            new NewsBar { Label = "Stores", Value = "-1.8%", Share = -0.58f },
            new NewsBar { Label = "Banks", Value = "-2.9%", Share = -0.94f }
        };
        m.Boards = new[]
        {
            NewsBoard(t, 0, new[] { "BWU|Brightwater Utilities|$38.10|2.40%", "AUP|Aurora Pharma|$112.80|1.90%", "MVN|Marrow & Vine|$24.65|1.60%", "HSF|Harbourside Freight|$53.42|1.31%" }),
            NewsBoard(t, 1, new[] { "KST|Kestrel Motors|$67.20|2.80%", "NGR|Northgate Retail|$19.45|2.10%", "CBT|Cobalt Telecom|$41.30|1.40%", "GRR|Granite Realty|$55.90|0.90%" }),
            NewsBoard(t, 2, new[] { "HSF|Harbourside Freight|$53.42|148,200", "KST|Kestrel Motors|$67.20|121,500", "NGR|Northgate Retail|$19.45|96,800", "MVN|Marrow & Vine|$24.65|74,300" })
        };
        m.BoardTitles = new[] { t("reallife_ledger_risers", "Risers"), t("reallife_ledger_fallers", "Fallers"), t("reallife_news_traded", "Most traded") };
        m.MovesTitle = t("reallife_news_holdings", "What you hold");
        m.Moves = new[]
        {
            new NewsMove
            {
                Mark = "HS", Head = NewsFill(t("reallife_news_hold_head", "You hold {shares} {ticker} shares"), "shares", "1,200", "ticker", "HSF"),
                Detail = NewsFill(t("reallife_news_hold_up", "Worth {worth}, up {gain} since you bought"), "worth", "$64,104", "gain", "$11,185")
            },
            new NewsMove
            {
                Mark = "AU", Head = NewsFill(t("reallife_news_hold_head", "You hold {shares} {ticker} shares"), "shares", "400", "ticker", "AUP"),
                Detail = NewsFill(t("reallife_news_hold_down", "Worth {worth}, down {gain} since you bought"), "worth", "$45,120", "gain", "$1,380")
            }
        };
        m.MovesLink = t("reallife_news_open_stocks", "Stocks");
        m.MovesTarget = 2;
    }

    private static UiDataTableModel NewsBoard(UiWords t, int kind, string[] lines)
    {
        var rows = new UiDataRow[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            string[] c = lines[i].Split('|');
            rows[i] = new UiDataRow { Cells = c, Values = new[] { 0, 0, 0, kind == 2 ? 0 : kind == 0 ? 1 : -1 }, Tones = new UiRole[4] };
        }
        return new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("reallife_news_col_code", "Code"), Kind = UiCellKind.Text, Width = 80f },
                new UiDataColumn { Title = t("reallife_news_col_company", "Company"), Kind = UiCellKind.Text },
                new UiDataColumn { Title = t("reallife_news_col_price", "Price"), Kind = UiCellKind.Figure, Width = 80f },
                new UiDataColumn { Title = t(kind == 2 ? "reallife_news_col_shares" : "reallife_news_col_move", kind == 2 ? "Shares" : "Move"), Kind = kind == 2 ? UiCellKind.Figure : UiCellKind.Trend, Width = 96f }
            },
            Rows = rows,
            RowCount = rows.Length,
            EmptyIcon = UiIcon.ChartLineUp
        };
    }

    private static string[] NewsDays(UiWords t, int first, int n)
    {
        var whole = new string[n];
        for (int i = 0; i < n; i++)
        {
            whole[i] = NewsFill(t("reallife_news_axis_day", "Day {day}"), "day", (first + i).ToString());
        }
        return whole;
    }

    private static UiChartModel NewsIndexChart(UiWords t)
    {
        int n = 60;
        var v = new float[n];
        for (int i = 0; i < n; i++)
        {
            v[i] = 1215f + (float)(Math.Sin(i / 5.0) * 18.0) + i * 1.1f + (i > 50 ? (i - 50) * 3.5f : 0f);
        }
        return new UiChartModel
        {
            Kind = UiChartKind.Line,
            Title = t("reallife_news_index_close", "Index at the close"),
            Points = NewsText.AxisPoints(NewsDays(t, 230, n), 15),
            Reads = NewsDays(t, 230, n),
            Series = new[] { new UiSeries { Label = t("reallife_news_series_index", "City index"), Values = v, Tint = UiRole.Accent } },
            Format = x => x.ToString("N1")
        };
    }

    private static UiChartModel NewsGoodsChart(UiWords t)
    {
        int n = 120;
        var fuel = new float[n];
        var food = new float[n];
        var rent = new float[n];
        var tech = new float[n];
        for (int i = 0; i < n; i++)
        {
            fuel[i] = 100f + i * 0.09f + (float)Math.Sin(i / 7.0) * 1.2f;
            food[i] = 100f + i * 0.065f + (float)Math.Sin(i / 9.0) * 1f;
            rent[i] = 100f + i * 0.033f;
            tech[i] = 100f - (float)Math.Sin(i / 11.0) * 1.4f;
        }
        return new UiChartModel
        {
            Kind = UiChartKind.Line,
            Title = NewsFill(t("reallife_news_goods_index", "Index, day {day} = 100"), "day", "170"),
            Points = NewsText.AxisPoints(NewsDays(t, 170, n), 40),
            Reads = NewsDays(t, 170, n),
            Series = new[]
            {
                new UiSeries { Label = "Fuel", Values = fuel, Tint = UiRole.Danger },
                new UiSeries { Label = "Food", Values = food, Tint = UiRole.Warn },
                new UiSeries { Label = "Rent", Values = rent, Tint = UiRole.Accent }
            },
            Format = x => x.ToString("0")
        };
    }

    private static UiChartModel NewsChart(UiWords t, int n, string state)
    {
        var a = new float[n];
        var b = new float[n];
        var c = new float[n];
        for (int i = 0; i < n; i++)
        {
            float f = i / (float)(n - 1);
            a[i] = 2.4f + f * 3.6f + (float)Math.Sin(i / 6.0) * 0.12f;
            b[i] = 3f + (f > 0.32f ? 0.5f : 0f) + (f > 0.6f ? 0.5f : 0f) + (f > 0.82f ? 0.5f : 0f);
            c[i] = 2.8f + f * 1.1f + (float)Math.Cos(i / 8.0) * 0.1f;
        }
        UiSeries[] series;
        string title = t("reallife_news_chart", "Prices, the bank rate and wages");
        if (state == "jobs")
        {
            title = t("reallife_news_jobs_table", "The labour market");
            series = new[] { new UiSeries { Label = t("reallife_news_series_jobless", "Jobless"), Values = a, Tint = UiRole.Danger } };
        }
        else if (state == "finances")
        {
            title = t("reallife_news_nav_finances", "Finance");
            series = new[]
            {
                new UiSeries { Label = t("reallife_news_series_reserve", "Reserve"), Values = a, Tint = UiRole.Accent },
                new UiSeries { Label = t("reallife_news_series_revenue", "Revenue"), Values = b, Tint = UiRole.Ok },
                new UiSeries { Label = t("reallife_news_series_spending", "Spending"), Values = c, Tint = UiRole.Danger }
            };
        }
        else
        {
            series = new[]
            {
                new UiSeries { Label = t("reallife_news_series_inflation", "Inflation"), Values = a, Tint = UiRole.Danger },
                new UiSeries { Label = t("reallife_news_series_rate", "Bank rate"), Values = b, Tint = UiRole.Accent },
                new UiSeries { Label = t("reallife_news_series_wages", "Wages"), Values = c, Tint = UiRole.Ok }
            };
        }
        string[] whole = NewsDays(t, 289 - n + 1, n);
        return new UiChartModel
        {
            Kind = UiChartKind.Line,
            Title = title,
            Points = NewsText.AxisPoints(whole, NewsText.AxisStep(n)),
            Reads = whole,
            Series = series,
            Format = v => v.ToString("0.0")
        };
    }

    private static NewsAdvice[] NewsAdvice(UiWords t)
    {
        return new[]
        {
            new NewsAdvice { Icon = UiIcon.People, Title = t("reallife_news_pay_title", "Expect pay talks"),
                Body = NewsFill(t("reallife_news_pay_body", "{count} of your staff are paid under the going rate."), "count", "4"),
                Link = t("reallife_news_open_staff", "Staff"), Target = 1 },
            new NewsAdvice { Icon = UiIcon.Bank, Title = t("reallife_news_fix_title", "Fix your loan's rate now"),
                Body = NewsFill(t("reallife_news_fix_body", "A fixed rate saves {money} a month."), "money", "$3,100"),
                Link = t("reallife_news_fix_do", "Fix rate"), Target = 4 },
            new NewsAdvice { Icon = UiIcon.MapPin, Title = t("reallife_news_pull_title", "Hold off on this district"),
                Body = "Garment District " + NewsFill(t("reallife_news_pull_body", "pull fell {points} points this week."), "points", "4"),
                Link = t("reallife_news_open_map", "Map"), Target = 3 },
            new NewsAdvice { Icon = UiIcon.ChartLineUp, Title = t("reallife_news_def_title", "Defensive shares hold up"),
                Body = t("reallife_news_def_body", "Food and utilities moved less than luxury."),
                Link = t("reallife_news_open_stocks", "Stocks"), Target = 2 }
        };
    }

    private static UiDataTableModel NewsGoods(UiWords t, bool full)
    {
        UiDataColumn[] columns = full
            ? new[]
            {
                new UiDataColumn { Title = t("reallife_news_col_good", "Good"), Kind = UiCellKind.Text },
                new UiDataColumn { Title = t("reallife_news_col_today", "Today"), Kind = UiCellKind.Text, Width = 96f },
                new UiDataColumn { Title = t("reallife_news_col_week", "Week"), Kind = UiCellKind.Trend, Width = 112f },
                new UiDataColumn { Title = t("reallife_news_col_month", "Month"), Kind = UiCellKind.Trend, Width = 112f },
                new UiDataColumn { Title = t("reallife_news_col_spark", "120 days"), Kind = UiCellKind.Spark, Width = 96f },
                new UiDataColumn { Title = t("reallife_news_col_yours", "Your price"), Kind = UiCellKind.Text, Width = 112f }
            }
            : new[]
            {
                new UiDataColumn { Title = t("reallife_news_col_good", "Good"), Kind = UiCellKind.Text },
                new UiDataColumn { Title = t("reallife_news_col_today", "Today"), Kind = UiCellKind.Text, Width = 80f },
                new UiDataColumn { Title = t("reallife_news_col_month", "Month"), Kind = UiCellKind.Trend, Width = 96f },
                new UiDataColumn { Title = t("reallife_news_col_spark", "120 days"), Kind = UiCellKind.Spark, Width = 88f }
            };
        var rows = new[]
        {
            Good(full, "Burgers", "$8.40", "+1.4%", "+6.2%", 1, "$7.90", 7.6f, 8.4f),
            Good(full, "Coffee", "$3.10", "+0.8%", "+4.1%", 1, "$2.95", 2.9f, 3.1f),
            Good(full, "Croissants", "$3.60", "+0.4%", "+2.0%", 1, "", 3.4f, 3.6f),
            Good(full, "Bouquets", "$24.00", "+0.6%", "+1.8%", 1, "", 23f, 24f),
            Good(full, "Ice cream", "$3.80", "-0.6%", "-1.5%", -1, "", 3.9f, 3.8f),
            Good(full, "Fresh produce", "$6.90", "+1.8%", "+7.3%", 1, "", 6.2f, 6.9f),
            Good(full, "Clothes", "$34.00", "+0.1%", "+1.0%", 1, "", 33.5f, 34f),
            Good(full, "Electronics", "$410", "-0.9%", "-2.4%", -1, "", 428f, 410f),
            Good(full, "Fuel, a litre", "$4.02", "+2.2%", "+9.8%", 1, "", 3.6f, 4.02f),
            Good(full, "Cheese platters", "$12.50", "+1.1%", "+4.4%", 1, "", 11.8f, 12.5f)
        };
        if (!full)
        {
            Array.Resize(ref rows, 6);
        }
        return new UiDataTableModel { Columns = columns, Rows = rows, RowCount = rows.Length, EmptyIcon = UiIcon.Tag };
    }

    private static UiDataRow Good(bool full, string name, string price, string week, string month, int way, string yours, float from, float to)
    {
        var line = new float[40];
        for (int i = 0; i < line.Length; i++)
        {
            float f = i / 39f;
            line[i] = from + (to - from) * f + (float)Math.Sin(i * 0.7 + name.Length) * (to - from) * 0.12f;
        }
        UiRole tone = way < 0 ? UiRole.Ok : UiRole.Danger;
        return full
            ? new UiDataRow
            {
                Cells = new[] { name, price, week, month, string.Empty, yours },
                Values = new[] { 0, 0, way, way, 0, 0 },
                Tones = new[] { UiRole.None, UiRole.None, UiRole.Danger, UiRole.Danger, tone, UiRole.None },
                Line = line
            }
            : new UiDataRow
            {
                Cells = new[] { name, price, month, string.Empty },
                Values = new[] { 0, 0, way, 0 },
                Tones = new[] { UiRole.None, UiRole.None, UiRole.Danger, tone },
                Line = line
            };
    }

    private static UiDataTableModel NewsJobs(UiWords t)
    {
        return new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("reallife_news_col_name", "Name"), Kind = UiCellKind.Text },
                new UiDataColumn { Title = t("reallife_news_col_today", "Today"), Kind = UiCellKind.Text, Width = 120f }
            },
            Rows = new[]
            {
                new UiDataRow { Cells = new[] { t("reallife_news_series_jobless", "Jobless"), "5.9%" }, Values = new int[2], Tones = new UiRole[2] },
                new UiDataRow { Cells = new[] { t("reallife_news_series_vacancies", "Vacancies"), "412" }, Values = new int[2], Tones = new UiRole[2] }
            },
            RowCount = 2,
            EmptyIcon = UiIcon.Briefcase
        };
    }

    private static UiDataTableModel NewsDistricts(UiWords t)
    {
        return new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("reallife_news_col_district", "District"), Kind = UiCellKind.Tile },
                new UiDataColumn { Title = t("reallife_news_col_pull", "Pull"), Kind = UiCellKind.Meter, Width = 96f },
                new UiDataColumn { Title = t("reallife_news_col_week", "Week"), Kind = UiCellKind.Trend, Width = 80f },
                new UiDataColumn { Title = t("reallife_news_col_rent", "Rent"), Kind = UiCellKind.Text, Width = 80f }
            },
            Rows = new[]
            {
                District(0, "Midtown", "92", 92, "+3", 1, "$148"),
                District(1, "Murray Hill", "81", 81, "+1", 1, "$96"),
                District(2, "Garment District", "74", 74, "-4", -1, "$88"),
                District(3, "Hell's Kitchen", "68", 68, "-2", -1, "$71"),
                District(4, "Uptown", "63", 63, "+6", 1, "$64")
            },
            RowCount = 5,
            EmptyIcon = UiIcon.MapTrifold
        };
    }

    private static UiDataRow District(int i, string name, string pull, int meter, string week, int way, string rent)
    {
        return new UiDataRow
        {
            Cells = new[] { name, pull, week, rent },
            Values = new[] { (int)UiIcon.MapPin, meter, way, 0 },
            Tones = new[] { UiTokens.Chart(i), UiRole.None, UiRole.None, UiRole.None }
        };
    }
}
