using System;
using RealLifeOverhaul.Ui;

/// <summary>
/// The Stocks desk in every state the previews and the screen checks lay out. Names are the market's own companies.
/// Fees follow the commission the market charges. Plain words through keys.
/// </summary>
internal static partial class UiSamples
{
    public static readonly string[] StockStates =
    {
        "market_boom", "market_crash", "companies", "watch", "watch_empty", "company_today", "company_days", "company_weeks",
        "company_months", "company_years", "company_area", "ticket", "portfolio_empty", "portfolio_full", "orders", "history",
        "bonds", "futures", "bots", "listing", "long"
    };

    public static StocksModel Stocks(UiWords t, string state)
    {
        var m = new StocksModel { Title = t("reallife_mkt_app", "Stocks"), Page = PageOf(state) };
        Nav(m, t);
        m.PromptBack = t("reallife_ui_prompt_back", "Back");
        m.PromptMove = t("reallife_ui_prompt_move", "Move");
        m.PromptSelect = t("reallife_ui_prompt_select", "Select");
        switch (state)
        {
            case "market_crash": Market(m, t, false); break;
            case "market_boom": Market(m, t, true); break;
            case "companies": m.Table = Table(t, true, false); break;
            case "watch_empty":
                m.EmptyTitle = t("reallife_stk_watch_empty", "Nothing on the watch yet");
                m.EmptyBody = t("reallife_stk_watch_empty_body", "Open a company and press Watch. It stays in this short list.");
                m.EmptyIcon = UiIcon.Eye;
                break;
            case "watch": m.Table = Table(t, true, true); break;
            case "ticket": Ticket(m, t); break;
            case "portfolio_empty":
                m.EmptyTitle = t("reallife_stk_port_empty", "You hold nothing yet");
                m.EmptyBody = t("reallife_stk_port_empty_body", "Buy shares from a company. The holding, its gain and the sector split show here.");
                break;
            case "portfolio_full": Portfolio(m, t); break;
            case "orders": m.Statement = Statement(t, true); break;
            case "history": m.Statement = Statement(t, false); break;
            case "bonds":
                m.Table = EmptyTable(t, t("reallife_mkt_tab_bonds", "Bonds"), "reallife_stk_bonds_empty", "No bonds yet", "reallife_stk_bonds_empty_body", "Treasury and city bonds you buy show here, with what they pay.");
                break;
            case "futures":
                m.Table = EmptyTable(t, t("reallife_mkt_tab_futures", "Futures"), "reallife_stk_fut_empty", "No futures yet", "reallife_stk_fut_empty_body", "A future you open shows here until it settles.");
                break;
            case "bots": Bots(m, t); break;
            case "listing": Listing(m, t); break;
            default: Company(m, t, state); break;
        }
        return m;
    }

    private static StocksPage PageOf(string state)
    {
            if (state.StartsWith("company") || state == "long")
            {
                return StocksPage.Company;
            }
        switch (state)
        {
            case "companies": return StocksPage.Companies;
            case "watch":
            case "watch_empty": return StocksPage.Watch;
            case "ticket": return StocksPage.Ticket;
            case "portfolio_empty":
            case "portfolio_full": return StocksPage.Portfolio;
            case "orders": return StocksPage.Orders;
            case "history": return StocksPage.History;
            case "bonds": return StocksPage.Bonds;
            case "futures": return StocksPage.Futures;
            case "bots": return StocksPage.Bots;
            case "listing": return StocksPage.Listing;
            default: return StocksPage.Market;
        }
    }

    private static void Nav(StocksModel m, UiWords t)
    {
        string[] labels =
        {
            t("reallife_mkt_tab_market", "Market"), t("reallife_mkt_tab_companies", "Companies"), t("reallife_mkt_tab_watch", "Watchlist"),
            t("reallife_mkt_tab_portfolio", "Portfolio"), t("reallife_mkt_tab_orders", "Orders"), t("reallife_mkt_tab_history", "History"),
            t("reallife_mkt_tab_bots", "Bots"), t("reallife_stk_nav_listing", "Listing"), t("reallife_mkt_tab_bonds", "Bonds"), t("reallife_mkt_tab_futures", "Futures")
        };
        UiIcon[] icons = { UiIcon.ChartLineUp, UiIcon.Building, UiIcon.Eye, UiIcon.ChartPie, UiIcon.Receipt, UiIcon.Clock, UiIcon.Cpu, UiIcon.Presentation, UiIcon.Bank, UiIcon.Chart };
        m.Nav = new UiNavEntry[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            m.Nav[i] = new UiNavEntry { Icon = icons[i], Label = labels[i] };
        }
        m.Groups = new[] { t("reallife_stk_group_markets", "Markets"), t("reallife_stk_group_yours", "Yours"), t("reallife_stk_group_other", "Other markets") };
        m.GroupAt = new[] { 0, 3, 8 };
        m.NavAt = (int)(m.Page == StocksPage.Company ? StocksPage.Companies : m.Page == StocksPage.Ticket ? StocksPage.Companies : m.Page);
        if (m.NavAt > 9)
        {
            m.NavAt = 1;
        }
    }

    private static void Market(StocksModel m, UiWords t, bool boom)
    {
        string up = boom ? "+2.40%" : "-3.10%";
        m.Figures = new[]
        {
            new UiFigureModel { Label = t("reallife_mkt_col_index", "Index"), Value = boom ? "14,280" : "11,040", Change = up, Good = boom, Trend = boom ? new[] { 1f, 2f, 2f, 3f, 4f } : new[] { 4f, 3f, 3f, 2f, 1f } },
            new UiFigureModel { Label = t("reallife_stk_small", "Small caps"), Value = boom ? "4,120" : "3,440", Change = boom ? "+1.10%" : "-1.80%", Good = boom },
            new UiFigureModel { Label = t("reallife_stk_all", "All shares"), Value = boom ? "86,400" : "71,200", Change = boom ? "+0.90%" : "-2.20%", Good = boom },
            new UiFigureModel { Label = t("reallife_stk_today", "Market today"), Value = boom ? "96 / 28 / 1.2m" : "22 / 104 / 2.4m" }
        };
        m.TapeTitle = t("reallife_stk_tape", "Who is trading");
        m.Tape = new[]
        {
            t("reallife_stk_fund", "A fund") + " " + t("reallife_mkt_buy", "Buy").ToLowerInvariant() + " 4000 GMF",
            t("reallife_stk_you", "You") + " " + t("reallife_mkt_sell", "Sell").ToLowerInvariant() + " 200 STC"
        };
        m.Table = Table(t, boom, false);
    }

    private static UiDataTableModel Table(UiWords t, bool up, bool watch)
    {
        string[] names = { "Greenmarket Foods", "Steamline Coffee", "Dovemere Hotels", "Circuit Square Electronics" };
        string[] tickers = { "GMF", "STC", "DMH", "CSE" };
        int n = watch ? 2 : 4;
        var rows = new UiDataRow[n];
        for (int i = 0; i < n; i++)
        {
            bool rise = up ^ (i % 2 == 1);
            rows[i] = new UiDataRow
            {
                Cells = new[] { names[i], rise ? "$42.00" : "$18.50", rise ? "+1.80%" : "-0.90%", (12000 + i * 3400).ToString() },
                Values = new[] { (int)UiIcon.Building, 0, rise ? 1 : -1, 0 },
                Tones = new[] { UiTokens.Chart(i), UiRole.None, UiRole.None, UiRole.None },
                Sub = tickers[i]
            };
        }
        return new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("reallife_mkt_col_company", "Company"), Kind = UiCellKind.Tile, Sortable = true },
                new UiDataColumn { Title = t("reallife_mkt_col_price", "Price"), Kind = UiCellKind.Figure, Width = 120f, Sortable = true },
                new UiDataColumn { Title = t("reallife_mkt_col_today", "Today"), Kind = UiCellKind.Trend, Width = 140f, Sortable = true },
                new UiDataColumn { Title = t("reallife_mkt_col_volume", "Volume"), Kind = UiCellKind.Figure, Width = 120f, Sortable = true }
            },
            Rows = rows, RowCount = n,
            Filters = new[] { t("reallife_stk_filter_all", "All sectors"), t("reallife_stk_sort_movers", "Movers") },
            SearchPlaceholder = t("reallife_stk_search", "Find a company"),
            EmptyIcon = UiIcon.ChartLineUp
        };
    }

    private static void Company(StocksModel m, UiWords t, string state)
    {
        bool area = state == "company_area";
        m.Head = new UiFirmHeadModel { Name = "Greenmarket Foods", Glyph = UiIcon.Building, Kind = "GMF", KindIcon = UiIcon.Tag, Chips = new[] { t("reallife_mkt_sector_groceries", "Groceries") }, ChipTones = new[] { UiRole.None } };
        m.Note = t("reallife_stk_fund", "A fund") + " " + t("reallife_mkt_buy", "Buy").ToLowerInvariant() + " 4000 GMF";
        m.Figures = new[] { new UiFigureModel { Label = t("reallife_mkt_col_price", "Price"), Value = "$37.00", Change = "+1.40%", Good = true, Trend = new[] { 30f, 32f, 31f, 35f, 37f } } };
        m.FactLabels = new[] { t("reallife_mkt_col_pe", "P/E"), t("reallife_mkt_col_yield", "Yield"), t("reallife_stk_open", "Open"), t("reallife_stk_high", "High"), t("reallife_stk_low", "Low"), t("reallife_mkt_col_volume", "Volume") };
        m.FactValues = new[] { "18.0", "2.4%", "$36.20", "$37.40", "$35.90", "182400" };
        int n = 24;
        var close = new float[n];
        var open = new float[n];
        var high = new float[n];
        var low = new float[n];
        var vol = new float[n];
        var sma = new float[n];
        for (int i = 0; i < n; i++)
        {
            close[i] = 3400 + i * 12;
            open[i] = close[i] - 20;
            high[i] = close[i] + 30;
            low[i] = open[i] - 25;
            vol[i] = 1000 + i * 40;
            sma[i] = i < 5 ? float.NaN : 3380 + i * 10;
        }
        m.Desk = new UiChartDeskModel
        {
            Kind = area ? 2 : state == "company_days" ? 1 : 0,
            Title = "GMF", Open = open, High = high, Low = low, Close = close, Volume = vol, Focus = n - 1,
            LastTag = "$37.00", LastUp = true, Read = t("reallife_stk_read", "Last") + " $37.00",
            Lines = new[] { new UiChartLine { Name = t("reallife_mkt_overlay_sma20", "Average of 20"), Values = sma, Tint = UiTokens.Chart(0) } },
            Panes = state == "company_weeks" || state == "company_months" ? new[] { new UiChartLine { Name = t("reallife_stk_rsi", "RSI"), Values = sma, Tint = UiTokens.Chart(2) } } : new UiChartLine[0],
            Marks = new[] { new UiChartMark { FromX = 2, ToX = 20, FromY = 3450, ToY = 3600, Tint = UiRole.Accent } }
        };
        m.Chips = new[] { t("reallife_mkt_overlay_sma20", "Average of 20"), t("reallife_stk_add_indicator", "+ Indicator") };
        m.Tools = new[] { "1", "2", "3", "4", "5", "6", "7", "8", t("reallife_stk_clear", "Clear") };
        m.Tool = 1;
        m.Signals = new[] { t("reallife_stk_sig_sma_up", "Price is over its 50 day average."), t("reallife_stk_sig_rsi_mid", "RSI is in the middle.") };
        m.Ticket = new StocksTicket
        {
            Side = t("reallife_mkt_buy", "Buy"), Kind = t("reallife_stk_market_order", "Market"), Life = t("reallife_stk_day", "Day"),
            Shares = "100", Limit = t("reallife_stk_max", "Most you can buy"), Max = "2,400",
            Cost = "$3,700", Fee = "$5", Total = "$3,705", CashAfter = t("reallife_stk_cash_after", "Cash after"),
            After = t("reallife_stk_position_after", "Position after") + " 100",
            Place = t("reallife_stk_place", "Place order"), Instead = t("reallife_stk_sell_instead", "Sell instead")
        };
        m.PromptPlace = m.Ticket.Place;
        m.PromptOther = m.Ticket.Instead;
        m.Alerts = new[] { t("reallife_stk_alert", "Alert") + " $36.00" };
    }

    private static void Ticket(StocksModel m, UiWords t)
    {
        string place = t("reallife_stk_place", "Place order");
        string buy = t("reallife_mkt_buy", "Buy");
        string sell = t("reallife_mkt_sell", "Sell");
        m.Decision = new UiDecisionModel
        {
            Kicker = "GMF",
            Title = place,
            Emblem = UiIcon.ChartLineUp,
            EmblemLabel = "GMF",
            Body = t("reallife_stk_position_after", "Position after") + " 100. " + t("reallife_stk_cash_after", "Cash after") + " $8,695.",
            Choices = new[]
            {
                new UiDecisionChoice { Name = buy, Lines = new[] { new UiConsequence { Text = "$3,705", Tone = UiConsequence.Loss, Change = -1 } } },
                new UiDecisionChoice { Name = sell, Lines = new[] { new UiConsequence { Text = "$3,695", Tone = 1, Change = 1 } } }
            },
            Buttons = new[] { new UiActionModel { Label = place, Kind = UiButtonKind.Primary, Key = UiAction.Alt } },
            Prompts = new[] { new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") }, new UiPrompt { Action = UiAction.Alt, Label = place } },
            PromptCount = 2
        };
    }

    private static void Portfolio(StocksModel m, UiWords t)
    {
        m.Figures = new[] { new UiFigureModel { Label = t("reallife_stk_worth", "Worth"), Value = "$48,200", Change = "+3.20%", Good = true, Trend = new[] { 1f, 2f, 2f, 3f, 4f } } };
        var close = new float[40];
        for (int i = 0; i < close.Length; i++)
        {
            close[i] = 40000 + i * 200;
        }
        m.Chart = new UiChartModel
        {
            Kind = UiChartKind.Line, Title = t("reallife_stk_worth", "Worth"),
            Points = new string[40], Series = new[] { new UiSeries { Label = t("reallife_stk_worth", "Worth"), Values = close } },
            Format = v => v.ToString("0")
        };
        for (int i = 0; i < 40; i++)
        {
            m.Chart.Points[i] = (i + 1).ToString();
        }
        m.Donut = new[] { 0.5f, 0.3f, 0.2f };
        m.DonutLabels = new[] { t("reallife_mkt_sector_groceries", "Groceries"), t("reallife_mkt_sector_restaurants", "Restaurants"), t("reallife_mkt_sector_leisure", "Leisure") };
        m.DonutValues = new[] { "50%", "30%", "20%" };
        m.Table = Table(t, true, false);
    }

    private static UiStatementModel Statement(UiWords t, bool open)
    {
        var line = new UiStatementLine { When = "412", Icon = open ? UiIcon.Receipt : UiIcon.Clock, What = "GMF 100", Out = open ? "$3,700" : null, In = open ? null : "$420" };
        return new UiStatementModel
        {
            Account = t(open ? "reallife_mkt_tab_orders" : "reallife_mkt_tab_history", open ? "Orders" : "History"),
            AccountIcon = UiIcon.Receipt,
            BalanceLabel = t("reallife_stk_cash", "Cash"),
            Balance = "$12,400",
            Columns = new[] { t("reallife_mkt_col_day", "Day"), t("reallife_mkt_col_what", "What"), t("reallife_stk_in", "In"), t("reallife_stk_out", "Out"), t("reallife_stk_after", "After") },
            Groups = new[] { new UiStatementGroup { Label = t("reallife_stk_today", "Market today"), Lines = new[] { line, line } } }
        };
    }

    private static void Bots(StocksModel m, UiWords t)
    {
        m.Figures = new[]
        {
            new UiFigureModel { Label = t("reallife_stk_budget", "Budget in use"), Value = "$80,000" },
            new UiFigureModel { Label = t("reallife_stk_day_result", "Today"), Value = "$640", Change = "+0.80%", Good = true }
        };
        m.Table = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("reallife_mkt_col_company", "Company"), Kind = UiCellKind.Tile },
                new UiDataColumn { Title = t("reallife_mkt_col_state", "State"), Kind = UiCellKind.Chip, Width = 180f }
            },
            Rows = new[]
            {
                new UiDataRow { Cells = new[] { "Meridian Trend Partners", t("reallife_stk_running", "Running") }, Values = new[] { (int)UiIcon.Cpu, 0 }, Tones = new[] { UiRole.None, UiRole.Ok }, Sub = t("reallife_mkt_bot_strategy_trend", "Trend following") },
                new UiDataRow { Cells = new[] { t("reallife_stk_rule", "Rule") + " 1", t("reallife_stk_waiting", "Waiting") }, Values = new[] { (int)UiIcon.Cpu, 0 }, Tones = new[] { UiRole.None, UiRole.None }, Sub = "GMF" }
            },
            RowCount = 2, EmptyIcon = UiIcon.Cpu
        };
    }

    private static void Listing(StocksModel m, UiWords t)
    {
        m.Hero = new UiHeroModel { Icon = UiIcon.Presentation, Kicker = "GMF", Title = "Greenmarket Foods", Line = "$20.00", Progress = 60, Note = t("reallife_stk_now", "Now") };
        m.Note = t("reallife_stk_policy", "Pay out") + " 50%";
        m.Track = new UiTrackModel
        {
            Title = t("reallife_stk_stages", "Stages"),
            Steps = new[]
            {
                new UiTrackStep { Icon = UiIcon.Check, Label = t("reallife_lst_stage_advised", "advisers engaged"), State = UiStepState.Done },
                new UiTrackStep { Icon = UiIcon.Check, Label = t("reallife_lst_stage_auditing", "being audited"), State = UiStepState.Done },
                new UiTrackStep { Icon = UiIcon.Check, Label = t("reallife_lst_stage_building", "the book is open"), State = UiStepState.Now },
                new UiTrackStep { Icon = UiIcon.Check, Label = t("reallife_lst_stage_priced", "priced"), State = UiStepState.Later },
                new UiTrackStep { Icon = UiIcon.Check, Label = t("reallife_lst_stage_listed", "on the market"), State = UiStepState.Later }
            },
            StateWords = new[] { t("reallife_stk_done", "Done"), t("reallife_stk_now", "Now"), t("reallife_stk_next", "Next"), t("reallife_stk_later", "Later"), t("reallife_stk_later", "Later") }
        };
        m.Table = new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("reallife_stk_bidder", "Bidder"), Kind = UiCellKind.Tile },
                new UiDataColumn { Title = t("reallife_mkt_col_price", "Price"), Kind = UiCellKind.Figure, Width = 120f },
                new UiDataColumn { Title = t("reallife_mkt_col_shares", "Shares"), Kind = UiCellKind.Figure, Width = 120f }
            },
            Rows = new[]
            {
                new UiDataRow { Cells = new[] { t("reallife_stk_fund", "A fund"), "$20.50", "40000" }, Values = new[] { (int)UiIcon.Bank, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None } },
                new UiDataRow { Cells = new[] { t("reallife_stk_rival", "A rival"), "$20.00", "25000" }, Values = new[] { (int)UiIcon.Building, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None } }
            },
            RowCount = 2
        };
    }

    private static UiDataTableModel EmptyTable(UiWords t, string title, string emptyKey, string empty, string bodyKey, string body)
    {
        return new UiDataTableModel
        {
            Columns = new[] { new UiDataColumn { Title = title, Kind = UiCellKind.Text } },
            EmptyTitle = t(emptyKey, empty), EmptyBody = t(bodyKey, body), EmptyIcon = UiIcon.Bank
        };
    }
}
