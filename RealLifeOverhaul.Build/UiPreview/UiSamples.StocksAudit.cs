using RealLifeOverhaul.Ui;

/// <summary>
/// The Stocks desk's states after the logic audit (4 Nov): the Portfolio's Pays and Account views, a margin call, the Market with its open
/// orders and the five day spark, the Bots page with its gains of the day and the week, the company page with an alert set at a level the player
/// chose (and under a drawn line), a sale's position after, the alert sheet and the order's own question. Names are the market's own.
/// </summary>
internal static partial class UiSamples
{
    public static readonly string[] StockAuditStates =
    {
        "market_orders", "portfolio_pays", "portfolio_account", "portfolio_call", "bots_figures", "company_alertline", "company_sell", "sheet_alert", "sheet_ticket"
    };

    private static UiDataTableModel AuditTable(UiWords t, string[] heads, UiCellKind[] kinds, float[] widths, string[][] cells, string[] subs, UiRole[][] tones = null, int icon = 0)
    {
        UiDataTableModel table = StockTable(t, heads, kinds, widths, cells, subs, icon, tones);
        return table;
    }

    public static bool StocksAudit(StocksModel m, UiWords t, string state)
    {
        switch (state)
        {
            case "market_orders":
                m.Page = StocksPage.Market;
                Market(m, t, true);
                m.Figures = new[]
                {
                    new UiFigureModel { Label = t("reallife_mkt_col_index", "Index"), Value = "14,280", Change = "+2.40%", Good = true, Trend = new[] { 1f, 2f, 2f, 3f, 4f } },
                    new UiFigureModel { Label = t("reallife_stk_small", "Small caps"), Value = "4,120" },
                    new UiFigureModel { Label = t("reallife_stk_all", "All shares"), Value = "86,400" },
                    new UiFigureModel { Label = t("reallife_stk_today", "Market today"), Value = "96 / 28 / 1.2m" }
                };
                m.OrdersTitle = t("reallife_stk_your_orders", "Your orders");
                m.OrdersCount = t("reallife_stk_orders_open_count", "{count} open").Replace("{count}", "2");
                m.OrderLines = new[] { "Buy 500 HSF, limit $52.80", "Sell 2,000 GMF, limit $32.50" };
                m.OrderValues = new[] { "$26,400", "$65,000" };
                m.Status = "Open 09:00 to 16:00   " + t("reallife_stk_your_day", "Your day") + " +$39,998";
                return true;
            case "portfolio_pays":
            case "portfolio_account":
            case "portfolio_call":
            {
                m.Page = StocksPage.Portfolio;
                m.ViewLabels = new[] { t("reallife_stk_view_held", "Held"), t("reallife_stk_view_pays", "Pays"), t("reallife_stk_view_account", "Account") };
                m.Status = "Open 09:00 to 16:00";
                m.Actions = new[]
                {
                    new UiActionModel { Label = t("reallife_mkt_withdraw", "Withdraw"), Kind = UiButtonKind.Secondary, Key = UiAction.Count },
                    new UiActionModel { Label = t("reallife_mkt_deposit", "Deposit"), Kind = UiButtonKind.Primary, Key = UiAction.Count }
                };
                if (state == "portfolio_pays")
                {
                    m.View = 1;
                    m.Figures = new[]
                    {
                        new UiFigureModel { Label = t("reallife_stk_pays_year", "Income over the next year"), Value = "$12,640" },
                        new UiFigureModel { Label = t("reallife_mkt_inv_yield", "Yield on the worth"), Value = "2.6%" },
                        new UiFigureModel { Label = t("reallife_stk_pays_next", "Payments to come"), Value = "6" }
                    };
                    m.RowsTitle = t("reallife_mkt_inv_sec_year", "Over the next year");
                    m.Rows = new[]
                    {
                        new StocksRow { Label = "Dividends from shares", Value = "$9,840" }, new StocksRow { Label = "Rent from property trusts", Value = "$1,900" },
                        new StocksRow { Label = "Interest on Treasury bonds", Value = "$1,300" }, new StocksRow { Label = t("reallife_mkt_inv_income_owed", "Owed by shares sold short"), Value = "-$400", Tone = UiRole.DangerText },
                        new StocksRow { Label = t("reallife_mkt_inv_income_all", "In all"), Value = "$12,640" }
                    };
                    m.Note = "Before tax, at today's dividends and rates: a board can change its dividend, and a bond pays its own rate to its end.";
                    m.TableTitle = t("reallife_ibox_sec_payments", "Payments");
                    m.Table = AuditTable(t, new[] { t("reallife_stk_pays_from", "Paid by"), t("reallife_mkt_col_day", "Day"), t("reallife_stk_sh_amount", "Amount") },
                        new[] { UiCellKind.Tile, UiCellKind.Figure, UiCellKind.Figure }, new[] { 0f, 100f, 140f }, new[]
                    {
                        new[] { "Merchants Union Bank", "291", "$7,440" }, new[] { "Saltmarsh Insurance Group", "294", "$6,800" }, new[] { "Codeharbour Software", "297", "$2,880" },
                        new[] { "Treasury bond, 1 year", "301", "$82" }
                    }, new[] { "MUB", "SIG", "CHS", "Repays at its end" });
                    return true;
                }
                m.View = 2;
                bool call = state == "portfolio_call";
                m.Figures = new[]
                {
                    new UiFigureModel { Label = t("reallife_stk_cash", "Cash"), Value = "$24,000" }, new UiFigureModel { Label = t("reallife_stk_sh_equity", "Equity"), Value = "$48,000" },
                    new UiFigureModel { Label = t("reallife_stk_acc_dividends", "Dividends paid to you"), Value = "$14,260" },
                    new UiFigureModel { Label = t("reallife_stk_acc_commission", "Commission paid"), Value = "$3,184" }
                };
                if (call)
                {
                    m.Banner = t("reallife_mkt_account_call", "Margin call: {amount} by the close of day {day}").Replace("<b>", string.Empty).Replace("</b>", string.Empty).Replace("{amount}", "$4,200").Replace("{day}", "291");
                    m.BannerTone = UiRole.DangerText;
                }
                m.RowsTitle = t("reallife_mkt_account_margin", "Your margin account");
                m.Rows = new[]
                {
                    new StocksRow { Label = t("reallife_stk_cash", "Cash"), Value = "$24,000" }, new StocksRow { Label = t("reallife_stk_sh_equity", "Equity"), Value = "$48,000" },
                    new StocksRow { Label = t("reallife_stk_sh_maintenance", "Must keep"), Value = call ? "$52,200" : "$9,000" }, new StocksRow { Label = t("reallife_stk_acc_power", "Buying power"), Value = "$84,000" },
                    new StocksRow { Label = t("reallife_stk_sh_withdrawable", "Can be withdrawn"), Value = "$3,100" },
                    new StocksRow { Label = t("reallife_stk_realised_all", "Realised, all time"), Value = "$212,400", Tone = UiRole.OkText },
                    new StocksRow { Label = t("reallife_stk_acc_dividends", "Dividends paid to you"), Value = "$14,260" }, new StocksRow { Label = t("reallife_stk_acc_commission", "Commission paid"), Value = "$3,184" },
                    new StocksRow { Label = t("reallife_stk_acc_interest", "Interest paid"), Value = "$412" }, new StocksRow { Label = t("reallife_stk_acc_borrow", "Borrowing fees paid"), Value = "$38" }
                };
                return true;
            }
            case "bots_figures":
                m.Page = StocksPage.Bots;
                m.Status = "Open 09:00 to 16:00";
                m.Actions = new[] { new UiActionModel { Label = t("reallife_stk_add_rule", "Add a rule"), Kind = UiButtonKind.Primary, Icon = UiIcon.Plus, Key = UiAction.Option } };
                m.Figures = new[]
                {
                    new UiFigureModel { Label = t("reallife_stk_budget", "Budget in use"), Value = "$400,000" },
                    new UiFigureModel { Label = t("reallife_stk_gain_today", "Gain realised today"), Value = "+$1,240", Good = true, Change = null },
                    new UiFigureModel { Label = t("reallife_stk_gain_week", "Gain realised this week"), Value = "-$310", Good = false, Change = null }
                };
                m.TableTitle = t("reallife_stk_sh_firms", "Trading firms");
                m.Table2Title = t("reallife_stk_sh_rules", "Rules");
                m.Table = AuditTable(t, new[] { t("reallife_mkt_bot_col_firm", "Firm"), t("reallife_stk_sh_fee_month", "Fee a month"), t("reallife_mkt_bot_col_capital", "Capital"), t("reallife_mkt_col_worth", "Worth"), t("reallife_mkt_bot_col_profit", "Profit"), t("reallife_mkt_col_state", "State") },
                    new[] { UiCellKind.Tile, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Chip }, new[] { 0f, 112f, 112f, 112f, 112f, 152f }, new[]
                {
                    new[] { "Meridian Trend Partners", "0.80%", "$200,000", "$214,600", "$14,600", "Running" }, new[] { "Kestrel Reversion", string.Empty, string.Empty, string.Empty, string.Empty, "Free" }
                }, new[] { "Trend following", "Reversion" }, new[] { new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Ok }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Muted } });
                m.Table2 = AuditTable(t, new[] { t("reallife_stk_sh_rule", "Rule"), t("reallife_stk_sh_company", "Company"), t("reallife_stk_sh_budget", "Budget"), t("reallife_stk_gain_today", "Gain realised today"), t("reallife_stk_gain_week", "Gain realised this week"), t("reallife_mkt_col_state", "State") },
                    new[] { UiCellKind.Tile, UiCellKind.Text, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Chip }, new[] { 0f, 100f, 112f, 136f, 136f, 152f }, new[]
                {
                    new[] { "Dip buyer", "GMF", "$100,000", "+$1,240", "+$960", "Waiting" }, new[] { "Trend rider", "HSF", "$100,000", "$0", "-$1,270", "Running" }, new[] { "Grid", "STC", "$100,000", "$0", "$0", "Out of budget" }
                }, new[] { string.Empty, string.Empty, string.Empty }, new[]
                {
                    new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.OkText, UiRole.OkText, UiRole.None }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.DangerText, UiRole.Ok },
                    new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Danger }
                });
                return true;
            case "company_alertline":
            case "company_sell":
            {
                Company(m, t, "company_full");
                m.Page = StocksPage.Company;
                StocksCompany c = m.Co;
                c.Meta = t("reallife_stk_cap_mid", "Mid cap");
                c.AlertSet = t("reallife_stk_alert_set", "Set an alert");
                c.Alerts = new[]
                {
                    new StocksAlertRow { Words = t("reallife_stk_alert_above", "Above {price}").Replace("{price}", "$62.00"), State = t("reallife_stk_alert_state_on", "on") },
                    new StocksAlertRow { Words = t("reallife_stk_alert_support", "Below the support {price}").Replace("{price}", "$38.65"), State = t("reallife_stk_alert_state_on", "on") }
                };
                if (state == "company_alertline")
                {
                    c.AlertLine = t("reallife_stk_alert_line", "Alert on the line");
                    c.AlertLineOff = false;
                    return true;
                }
                c.AlertLine = t("reallife_stk_alert_line_off", "Pick a line first");
                c.AlertLineOff = true;
                c.Order.SideAt = 1;
                c.Order.Buying = false;
                c.Order.Shares = "600";
                c.Order.Place = t("reallife_stk_place_sell", "Place sell order");
                c.Order.Rows = new[]
                {
                    new StocksRow { Label = t("reallife_stk_row_price", "Price"), Value = "$53.42" }, new StocksRow { Label = t("reallife_stk_row_cost", "Cost"), Value = "$32,052" },
                    new StocksRow { Label = t("reallife_stk_row_fee", "Fee"), Value = "$32" }, new StocksRow { Label = t("reallife_stk_row_total", "Total"), Value = "$32,020" },
                    new StocksRow { Label = t("reallife_stk_cash_after", "Cash after"), Value = "$1,236,320" },
                    new StocksRow { Label = t("reallife_stk_position_after", "Position after"), Value = t("reallife_stk_position_value", "{shares} at {price}").Replace("{shares}", "600").Replace("{price}", "$44.10") },
                    new StocksRow { Label = t("reallife_stk_row_most_sell", "Most you can sell"), Value = "1,200" }
                };
                m.PromptPlace = c.Order.Place;
                return true;
            }
            case "sheet_alert":
                m.Page = StocksPage.Companies;
                m.Table = Table(t, true, false);
                m.Sheet = new StocksSheetModel
                {
                    Kicker = t("reallife_stk_alerts_title", "Price alerts"), Title = t("reallife_stk_alert_sheet", "Set a price alert"), Emblem = UiIcon.Bell, Back = t("reallife_ui_prompt_back", "Back"),
                    Name = "Harbourside Freight", Sub = "HSF", Big = "$53.42",
                    SideLabels = new[] { t("reallife_stk_row_price", "Price"), t("reallife_stk_alerts_title", "Price alerts") }, SideValues = new[] { "$53.42", "2" },
                    About = t("reallife_stk_alert_about", "You are told when the price crosses the level. It is checked as the market moves."),
                    Fields = new[]
                    {
                        SPick(1, t("reallife_stk_alert_goes", "Tell me when the price goes"), t("reallife_stk_alert_over", "Over the level")),
                        SStep(0, t("reallife_stk_alert_level", "Level"), "$56.09", true)
                    },
                    Confirm = t("reallife_stk_alert_set_go", "Set the alert")
                };
                return true;
            case "sheet_ticket":
                m.Page = StocksPage.Companies;
                m.Table = Table(t, true, false);
                m.Sheet = new StocksSheetModel
                {
                    Kicker = t("reallife_mkt_tab_orders", "Orders"), Title = t("reallife_mkt_confirm_buy", "Buy shares of {name}").Replace("{name}", "Harbourside Freight"), Emblem = UiIcon.ChartLineUp, Back = t("reallife_ui_prompt_back", "Back"),
                    Name = "Harbourside Freight", Sub = "HSF", Big = "$26,737",
                    SideLabels = new[]
                    {
                        t("reallife_mkt_col_shares", "Shares"), t("reallife_mkt_col_kind", "Kind"), t("reallife_mkt_col_price", "Price"), t("reallife_stk_row_cost", "Cost"), t("reallife_stk_row_fee", "Fee"),
                        t("reallife_stk_row_total", "Total"), t("reallife_mkt_col_lasts", "Lasts")
                    },
                    SideValues = new[] { "500", "Market", "$53.42", "$26,710", "$27", "$26,737", "For the day" },
                    About = "Shares: 500 of Harbourside Freight, Market, For the day.\nAt $53.42 now: $26,710 for the shares, $27 commission, $26,737 in all.\nA price that moves before the order is filled changes what it comes to.",
                    Confirm = t("reallife_mkt_buy", "Buy")
                };
                return true;
        }
        return false;
    }
}
