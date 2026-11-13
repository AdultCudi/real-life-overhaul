using System;
using RealLifeOverhaul.Ui;

/// <summary>
/// The Stocks desk in every state the previews and the screen checks lay out. Names are the market's own companies.
/// Fees follow the commission the market charges. Plain words through keys.
/// </summary>
internal static partial class UiSamples
{
    public static string[] StockStates => Join(Join(Join(Join(StockBaseStates, StockCompanyStates), StockOfferStates), StockAuditStates), StockExtrasStates);   // joined on asking: static fields of two files start in no fixed order

    private static string[] Join(string[] a, string[] b)
    {
        var all = new string[a.Length + b.Length];
        a.CopyTo(all, 0);
        b.CopyTo(all, a.Length);
        return all;
    }

    private static readonly string[] StockBaseStates =
    {
        "market_boom", "market_crash", "companies", "watch", "watch_empty", "portfolio_empty", "portfolio_full", "orders", "history", "listing"
    };

    public static StocksModel Stocks(UiWords t, string state)
    {
        var m = new StocksModel { Title = t("reallife_mkt_app", "Stocks"), Page = PageOf(state) };
        Nav(m, t);
        m.PromptBack = t("reallife_ui_prompt_back", "Back");
        m.PromptMove = t("reallife_ui_prompt_move", "Move");
        m.PromptSelect = t("reallife_ui_prompt_select", "Select");
        if (StocksOffers(m, t, state) || StocksSheet(m, t, state) || StocksAudit(m, t, state) || StocksExtras(m, t, state))
        {
            Nav(m, t);
            return m;
        }
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
            case "portfolio_empty":
                m.EmptyTitle = t("reallife_stk_port_empty", "You hold nothing yet");
                m.EmptyBody = t("reallife_stk_port_empty_body", "Buy shares from a company. The holding, its gain and the sector split show here.");
                break;
            case "portfolio_full": Portfolio(m, t); break;
            case "orders": m.Table = Table(t, true, false); break;
            case "history": History(m, t); break;
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
            case "portfolio_empty":
            case "portfolio_full": return StocksPage.Portfolio;
            case "orders":
            case "orders_table": return StocksPage.Orders;
            case "search":
            case "search_none": return StocksPage.Companies;
            case "bonds_held": return StocksPage.Bonds;
            case "futures_held": return StocksPage.Futures;
            case "bots_rules": return StocksPage.Bots;
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
        m.NavAt = (int)(m.Page == StocksPage.Company ? StocksPage.Companies : m.Page);
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

    // The account's lines as the History page says them: day, company, what, shares, price, money and commission, newest first.
    private static void History(StocksModel m, UiWords t)
    {
        m.Note = "Commission $3,184, dividends $14,260, gains taken $212,400";
        m.Table = StockTable(t, new[] { t("reallife_mkt_col_company", "Company"), t("reallife_mkt_col_day", "Day"), t("reallife_mkt_col_what", "What"), t("reallife_mkt_col_shares", "Shares"),
                t("reallife_mkt_col_price", "Price"), t("reallife_mkt_col_money", "Money"), t("reallife_mkt_col_commission", "Commission") },
            new[] { UiCellKind.Tile, UiCellKind.Figure, UiCellKind.Text, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure }, new[] { 0f, 88f, 224f, 104f, 100f, 120f, 112f }, new[]
        {
            new[] { "Greenmarket Foods", "Day 288", "Bought", "300", "$31.80", "-$9,540", "$10" }, new[] { "Merchants Union Bank", "Day 287", "Dividend", string.Empty, string.Empty, "+$7,440", string.Empty },
            new[] { "Harbourside Freight", "Day 285", "Sold", "500", "$52.90", "+$26,450", "$27" }, new[] { "None", "Day 280", "Deposit", string.Empty, string.Empty, "-$5,000", string.Empty }
        }, new[] { "GMF", "MUB", "HSF", null }, (int)UiIcon.Building, new[]
        {
            new[] { UiTokens.Chart(1), UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.DangerText, UiRole.None },
            new[] { UiTokens.Chart(2), UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.OkText, UiRole.None },
            new[] { UiTokens.Chart(3), UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.OkText, UiRole.None },
            new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.DangerText, UiRole.None }
        });
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
                Cells = new[] { names[i], rise ? "$42.00" : "$18.50", rise ? "+1.80%" : "-0.90%", string.Empty, (12000 + i * 3400).ToString() },
                Values = new[] { (int)UiIcon.Building, 0, rise ? 1 : -1, 0, 0 },
                Tones = new[] { UiTokens.Chart(i), UiRole.None, UiRole.None, rise ? UiRole.Ok : UiRole.Danger, UiRole.None },
                Sub = tickers[i], Line = rise ? new[] { 40.1f, 40.6f, 40.2f, 41.3f, 42f } : new[] { 19.4f, 19.0f, 18.9f, 18.7f, 18.5f }
            };
        }
        return new UiDataTableModel
        {
            Columns = new[]
            {
                new UiDataColumn { Title = t("reallife_mkt_col_company", "Company"), Kind = UiCellKind.Tile, Sortable = true },
                new UiDataColumn { Title = t("reallife_mkt_col_price", "Price"), Kind = UiCellKind.Figure, Width = 120f, Sortable = true },
                new UiDataColumn { Title = t("reallife_mkt_col_today", "Today"), Kind = UiCellKind.Trend, Width = 140f, Sortable = true },
                new UiDataColumn { Title = t("reallife_stk_five_days", "5 days"), Kind = UiCellKind.Spark, Width = 120f },
                new UiDataColumn { Title = t("reallife_mkt_col_volume", "Volume"), Kind = UiCellKind.Figure, Width = 120f, Sortable = true }
            },
            Rows = rows, RowCount = n,
            Filters = new[] { t("reallife_stk_sort_movers", "Movers"), t("reallife_stk_filter_all", "All sectors"), t("reallife_stk_all", "All shares") },
            SearchPlaceholder = t("reallife_stk_search", "Find a company"),
            EmptyIcon = UiIcon.ChartLineUp
        };
    }

    private static void Portfolio(StocksModel m, UiWords t)
    {
        m.ViewLabels = new[] { t("reallife_stk_view_held", "Held"), t("reallife_stk_view_pays", "Pays"), t("reallife_stk_view_account", "Account") };
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
        m.Note = t("reallife_stk_policy_board", "The board pays out {payout} of the reserve at each results").Replace("{payout}", "50%") + "  " + t("reallife_stk_expected_range", "Expected price {low} to {high}").Replace("{low}", "$18.00").Replace("{high}", "$22.00");
        m.Track = new UiTrackModel
        {
            Title = t("reallife_stk_stages", "Stages"),
            Steps = new[]
            {
                new UiTrackStep { Icon = UiIcon.Check, Label = t("reallife_lst_stage_advised", "advisers engaged") + ", " + t("reallife_mkt_day", "Day {day}").Replace("{day}", "262"), State = UiStepState.Done },
                new UiTrackStep { Icon = UiIcon.Check, Label = t("reallife_lst_stage_auditing", "being audited") + ", " + t("reallife_stk_days_span", "days {from} to {to}").Replace("{from}", "263").Replace("{to}", "270"), State = UiStepState.Done },
                new UiTrackStep { Icon = UiIcon.Check, Label = t("reallife_lst_stage_building", "the book is open") + ", " + t("reallife_stk_days_span", "days {from} to {to}").Replace("{from}", "271").Replace("{to}", "276"), State = UiStepState.Now },
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
