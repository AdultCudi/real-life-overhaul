using RealLifeOverhaul.Ui;

/// <summary>
/// The Stocks desk's pages of offers and its sheets, as the previews and the screen checks lay them out: what can be bought,
/// opened or hired over what is held, each sheet, and the search typing. Names are the market's own.
/// </summary>
internal static partial class UiSamples
{
    public static readonly string[] StockOfferStates =
    {
        "bonds", "bonds_held", "futures", "futures_held", "bots", "bots_rules", "orders_table", "search", "search_none",
        "sheet_bond", "sheet_ladder", "sheet_bond_held", "sheet_future", "sheet_option", "sheet_contract", "sheet_firm", "sheet_firm_held", "sheet_rule",
        "sheet_order", "sheet_margin", "sheet_listing", "sheet_founder"
    };

    private static UiDataTableModel StockTable(UiWords t, string[] heads, UiCellKind[] kinds, float[] widths, string[][] cells, string[] subs, int icon, UiRole[][] tones = null)
    {
        var columns = new UiDataColumn[heads.Length];
        for (int i = 0; i < heads.Length; i++)
        {
            columns[i] = new UiDataColumn { Title = heads[i], Kind = kinds[i], Width = widths[i] };
        }
        var rows = new UiDataRow[cells.Length];
        for (int i = 0; i < cells.Length; i++)
        {
            var values = new int[heads.Length];
            values[0] = icon;
            rows[i] = new UiDataRow { Cells = cells[i], Values = values, Tones = tones != null ? tones[i] : new UiRole[heads.Length], Sub = subs[i] };
        }
        return new UiDataTableModel { Columns = columns, Rows = rows, RowCount = rows.Length, EmptyIcon = UiIcon.Bank };
    }

    private static UiDataTableModel NoRows(UiWords t, string head, string empty, string body, UiIcon icon)
    {
        return new UiDataTableModel
        {
            Columns = new[] { new UiDataColumn { Title = head, Kind = UiCellKind.Tile }, new UiDataColumn { Title = t("reallife_mkt_col_state", "State"), Kind = UiCellKind.Text, Width = 140f } },
            EmptyTitle = empty, EmptyBody = body, EmptyIcon = icon
        };
    }

    private static UiFigureModel StockFig(string label, string value) => new UiFigureModel { Label = label, Value = value };

    public static bool StocksOffers(StocksModel m, UiWords t, string state)
    {
        UiCellKind[] bondKinds = { UiCellKind.Tile, UiCellKind.Text, UiCellKind.Figure, UiCellKind.Text, UiCellKind.Figure, UiCellKind.Figure };
        float[] bondW = { 0f, 152f, 88f, 152f, 112f, 88f };
        string[] bondHeads =
        {
            t("reallife_mkt_bond_col_bond", "Bond"), t("reallife_stk_sh_term", "Term"), t("reallife_mkt_col_yield", "Yield"), t("reallife_stk_sh_pays", "Pays"),
            t("reallife_stk_sh_least", "Least"), t("reallife_mkt_col_price", "Price")
        };
        switch (state)
        {
            case "bonds":
            case "bonds_held":
            {
                bool held = state == "bonds_held";
                m.Status = "Open 09:00 to 16:00";
                m.Figures = new[] { StockFig(t("reallife_stk_worth", "Worth"), held ? "$24,600" : "$0"), StockFig(t("reallife_stk_sh_bank_rate", "Bank rate"), "3.00%"), StockFig(t("reallife_stk_sh_next", "Next payment"), held ? "Day 34" : "-") };
                m.TableTitle = t("reallife_mkt_bond_filter_offers", "On offer");
                m.Table2Title = t("reallife_mkt_bond_filter_held", "Yours");
                m.Table = StockTable(t, bondHeads, bondKinds, bondW, new[]
                {
                    new[] { "Treasury bond, 3 months", "3 months", "2.10%", "At its end", "$500", "100.00" },
                    new[] { "Treasury bond, 1 year", "1 year", "2.65%", "Every quarter", "$500", "100.00" },
                    new[] { "Treasury bond, 5 years", "5 years", "3.40%", "Every quarter", "$500", "100.00" },
                    new[] { "City bond, 2 years", "2 years", "3.55%", "Every quarter", "$500", "100.00" },
                    new[] { "Deposit at First City Bank, 1 year", "1 year", "2.40%", "Every month", "$500", "100.00" },
                    new[] { "Treasury ladder", "3 months to 5 years", "2.95%", "As each bond", "$500", "100.00" }
                }, new[] { "Treasury", "Treasury", "Treasury", "City", "Bank deposit", "Treasury" }, (int)UiIcon.Bank);
                if (held)
                {
                    m.Table2 = StockTable(t, new[] { bondHeads[0], t("reallife_stk_worth", "Worth"), bondHeads[2], t("reallife_stk_sh_ends", "Ends on day"), t("reallife_stk_sh_next", "Next payment") },
                        new[] { UiCellKind.Tile, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Text }, new[] { 0f, 120f, 88f, 128f, 192f }, new[]
                    {
                        new[] { "Treasury bond, 1 year", "$12,400", "2.65%", "88", "$82, day 34" },
                        new[] { "City bond, 2 years", "$12,200", "3.55%", "142", "$108, day 41" }
                    }, new[] { "Treasury", "City" }, (int)UiIcon.Bank);
                }
                else
                {
                    m.Table2 = NoRows(t, bondHeads[0], t("reallife_stk_bonds_empty", "No bonds yet"), t("reallife_stk_bonds_empty_body", "Treasury and city bonds you buy show here, with what they pay."), UiIcon.Bank);
                }
                return true;
            }
            case "futures":
            case "futures_held":
            {
                bool held = state == "futures_held";
                m.Status = "Open 09:00 to 16:00";
                m.Figures = new[]
                {
                    StockFig(t("reallife_stk_sh_open", "Open contracts"), held ? "2" : "0"), StockFig(t("reallife_stk_worth", "Worth"), held ? "$8,400" : "$0"),
                    StockFig(t("reallife_stk_sh_made", "Made so far"), held ? "+$620" : "$0"), StockFig(t("reallife_stk_sh_account", "Margin account"), held ? "$1,900" : "$0")
                };
                m.TableTitle = t("reallife_mkt_bond_filter_offers", "On offer");
                m.Table2Title = t("reallife_stk_sh_your_contracts", "Your contracts");
                m.Table = StockTable(t, new[] { t("reallife_mkt_drv_col_name", "Material or company"), t("reallife_mkt_drv_col_kind", "Group"), t("reallife_mkt_col_price", "Price"), t("reallife_mkt_col_today", "Today"), t("reallife_mkt_drv_col_next", "Next week") },
                    new[] { UiCellKind.Tile, UiCellKind.Text, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure }, new[] { 0f, 192f, 112f, 100f, 120f }, new[]
                {
                    new[] { "Wheat", "Farm and field", "$0.41", "+0.8%", "$0.42" },
                    new[] { "Steel", "Metals, glass and stone", "$1.15", "-0.4%", "$1.14" },
                    new[] { "Coffee beans", "Drinks and flavourings", "$3.10", "+1.9%", "$3.16" },
                    new[] { "Greenmarket Foods", "Groceries", "$37.00", "+1.4%", string.Empty }
                }, new[] { "Raw materials", "Raw materials", "Raw materials", "GMF" }, (int)UiIcon.Chart);
                if (held)
                {
                    m.Table2 = StockTable(t, new[] { t("reallife_mkt_tab_futures", "Futures"), t("reallife_mkt_col_kind", "Kind"), t("reallife_stk_sh_qty", "Size"), t("reallife_stk_worth", "Worth"), t("reallife_stk_sh_made", "Made so far"), t("reallife_stk_sh_settles", "Settles on day") },
                        new[] { UiCellKind.Tile, UiCellKind.Text, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure }, new[] { 0f, 152f, 100f, 112f, 120f, 112f }, new[]
                    {
                        new[] { "Steel, the week from day 90", "Future bought", "5,000", "$5,750", "+$480", "95" },
                        new[] { "Call on Greenmarket Foods at $38.00, day 96", "Call", "500", "$2,650", "+$140", "96" }
                    }, new string[] { null, null }, (int)UiIcon.Chart);
                }
                else
                {
                    m.Table2 = NoRows(t, t("reallife_mkt_tab_futures", "Futures"), t("reallife_stk_fut_empty", "No futures yet"), t("reallife_stk_fut_empty_body", "A future you open shows here until it settles."), UiIcon.Chart);
                }
                return true;
            }
            case "bots":
            case "bots_rules":
            {
                bool rules = state == "bots_rules";
                m.Status = "Open 09:00 to 16:00";
                m.Figures = new[] { StockFig(t("reallife_stk_budget", "Budget in use"), rules ? "$412,000" : "$0"), StockFig(t("reallife_stk_day_result", "Today"), rules ? "+$2,300" : "$0"), StockFig(t("reallife_stk_sh_this_week", "This week"), rules ? "+$5,102" : "$0") };
                m.Actions = new[] { new UiActionModel { Label = t("reallife_stk_add_rule", "Add a rule"), Kind = UiButtonKind.Primary, Icon = UiIcon.Plus, Key = UiAction.Option } };
                m.TableTitle = t("reallife_stk_sh_firms", "Trading firms");
                m.Table2Title = t("reallife_stk_sh_rules", "Rules");
                UiRole[] free = { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Muted };
                UiRole[] run = { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Ok };
                m.Table = StockTable(t, new[] { t("reallife_mkt_bot_col_firm", "Firm"), t("reallife_stk_sh_fee_month", "Fee a month"), t("reallife_mkt_bot_col_capital", "Capital"), t("reallife_mkt_col_worth", "Worth"), t("reallife_mkt_bot_col_profit", "Profit"), t("reallife_mkt_col_state", "State") },
                    new[] { UiCellKind.Tile, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Chip }, new[] { 0f, 112f, 112f, 112f, 112f, 152f }, new[]
                {
                    new[] { "Meridian Trend Partners", "0.10%", rules ? "$100,000" : string.Empty, rules ? "$108,400" : string.Empty, rules ? "$8,400" : string.Empty, rules ? "Running" : "Free" },
                    new[] { "Harbour Reversion Fund", "0.12%", string.Empty, string.Empty, string.Empty, "Free" },
                    new[] { "Lantern Income Desk", "0.08%", string.Empty, string.Empty, string.Empty, "Free" }
                }, new[] { "Trend following", "Reversion", "Income" }, (int)UiIcon.Cpu, new[] { rules ? run : free, free, free });
                if (rules)
                {
                    m.Table2 = StockTable(t, new[] { t("reallife_stk_sh_rule", "Rule"), t("reallife_stk_sh_company", "Company"), t("reallife_stk_sh_budget", "Budget"), t("reallife_stk_day_result", "Today"), t("reallife_stk_sh_this_week", "This week"), t("reallife_mkt_col_state", "State") },
                        new[] { UiCellKind.Tile, UiCellKind.Text, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Chip }, new[] { 0f, 100f, 112f, 100f, 100f, 152f }, new[]
                    {
                        new[] { "Dip buyer", "GMF", "$200,000", "+$1,240", "+$3,508", "Running" },
                        new[] { "Stop guard", "STC", "$50,000", "$0", "$0", "Waiting" },
                        new[] { "Trend rider", "DMH", "$100,000", "+$310", "+$1,000", "A rival" }
                    }, new string[] { null, null, "A rival" }, (int)UiIcon.Cpu);
                }
                else
                {
                    m.Table2 = NoRows(t, t("reallife_stk_sh_rule", "Rule"), t("reallife_stk_bots_empty", "No bots yet"), t("reallife_stk_bots_empty_body", "Hire a firm, or add a rule. Rivals run the same rules."), UiIcon.Cpu);
                }
                return true;
            }
            case "orders_table":
                m.Status = "Open 09:00 to 16:00";
                m.Table = StockTable(t, new[] { t("reallife_mkt_col_company", "Company"), t("reallife_mkt_col_kind", "Kind"), t("reallife_mkt_col_shares", "Shares"), t("reallife_mkt_col_price", "Price"), t("reallife_mkt_col_lasts", "Lasts"), t("reallife_mkt_col_state", "State"), t("reallife_stk_sh_placed", "Placed on day") },
                    new[] { UiCellKind.Tile, UiCellKind.Text, UiCellKind.Figure, UiCellKind.Figure, UiCellKind.Text, UiCellKind.Chip, UiCellKind.Figure }, new[] { 0f, 192f, 112f, 100f, 120f, 120f, 88f }, new[]
                {
                    new[] { "Greenmarket Foods", "Buy  Limit", "100", "$36.20", "Until day 96", "Open", "412" }
                }, new[] { "GMF" }, (int)UiIcon.Receipt);
                m.Table.Filters = new[] { t("reallife_mkt_filter_open", "Open") };
                return true;
            case "search":
            case "search_none":
                m.Table = Table(t, true, false);
                m.Table.Query = state == "search" ? "gre" : "zzz";
                m.Table.Count = state == "search" ? "1 of 4" : "0 of 4";
                if (state == "search")
                {
                    m.Table.Rows = new[] { m.Table.Rows[0] };
                    m.Table.RowCount = 1;
                }
                else
                {
                    m.Table.RowCount = 0;
                    m.Table.EmptyTitle = t("reallife_stk_search_none", "No company matches");
                    m.Table.EmptyBody = t("reallife_stk_search_none_body", "Nothing here has that in its name or ticker.");
                    m.Table.EmptyAction = t("reallife_stk_search_clear", "Clear search");
                }
                return true;
        }
        return false;
    }

    private static StocksFieldModel SStep(int id, string label, string value, bool typed = false) =>
        new StocksFieldModel { Kind = StocksFieldKind.Stepper, Id = id, Label = label, Value = value, CanLess = true, CanMore = true, Typed = typed, Min = 0, Max = 99, Step = 1 };

    private static StocksFieldModel SPick(int id, string label, string value) => new StocksFieldModel { Kind = StocksFieldKind.Choice, Id = id, Label = label, Value = value, Count = 3 };

    private static StocksFieldModel SSwitch(int id, string label, bool on, UiWords t) =>
        new StocksFieldModel { Kind = StocksFieldKind.Switch, Id = id, Label = label, On = on, Value = on ? t("reallife_stk_sh_on", "On") : t("reallife_stk_sh_off", "Off") };

    private static StocksFieldModel SFact(string label, string value, int tone = 0) => new StocksFieldModel { Kind = StocksFieldKind.Fact, Label = label, Value = value, Tone = tone };

    private static StocksFieldModel SNote(string text) => new StocksFieldModel { Kind = StocksFieldKind.Note, Value = text };

    public static bool StocksSheet(StocksModel m, UiWords t, string state)
    {
        string back = t("reallife_ui_prompt_back", "Back");
        switch (state)
        {
            case "sheet_bond":
            case "sheet_ladder":
            {
                bool ladder = state == "sheet_ladder";
                m.Page = StocksPage.Bonds;
                StocksOffers(m, t, "bonds");
                m.Sheet = new StocksSheetModel
                {
                    Kicker = t("reallife_mkt_tab_bonds", "Bonds"), Title = ladder ? t("reallife_mkt_bond_ladder", "Treasury ladder") : t("reallife_stk_sh_buy_bond", "Buy a bond"), Emblem = UiIcon.Bank, Back = back,
                    Name = ladder ? "Treasury ladder" : "Treasury bond, 5 years", Big = ladder ? "2.95%" : "3.40%", Sub = ladder ? "3 months to 5 years" : "Treasury",
                    SideLabels = ladder ? new[] { "3 months", "1 year", "5 years" } : new[] { t("reallife_stk_sh_term", "Term"), t("reallife_stk_sh_pays", "Pays"), t("reallife_mkt_col_price", "Price"), t("reallife_stk_sh_ends", "Ends on day"), t("reallife_stk_sh_least", "Least"), t("reallife_stk_sh_most", "Most") },
                    SideValues = ladder ? new[] { "$2,000 at 2.10%", "$2,000 at 2.65%", "$2,000 at 3.40%" } : new[] { "5 years", "Every quarter", "100.00", "880", "$500", "$2,000,000" },
                    About = "The city's Treasury: the safest bond there is. Bought at 100 for each 100 it repays.",
                    Fields = new[]
                    {
                        SStep(0, t("reallife_stk_sh_amount", "Amount"), "$10,000", true), SPick(1, t("reallife_stk_sh_putback", "Interest"), "Pay all its interest to me"),
                        SSwitch(2, t("reallife_stk_sh_roll", "At its end"), false, t), SFact(t("reallife_stk_sh_each", "Each payment"), "$85, 20 in all", 1)
                    },
                    Confirm = t("reallife_mkt_bond_buy", "Buy")
                };
                return true;
            }
            case "sheet_bond_held":
                m.Page = StocksPage.Bonds;
                StocksOffers(m, t, "bonds_held");
                m.Sheet = new StocksSheetModel
                {
                    Kicker = t("reallife_mkt_tab_bonds", "Bonds"), Title = t("reallife_stk_sh_bond", "Your bond"), Emblem = UiIcon.Bank, Back = back,
                    Name = "City bond, 2 years", Big = "$12,200", Sub = "3.55%  City",
                    SideLabels = new[] { t("reallife_stk_sh_face", "Face"), t("reallife_stk_sh_ends", "Ends on day"), t("reallife_stk_sh_paid", "Payments made"), t("reallife_stk_sh_received", "Paid to you"), t("reallife_stk_sh_tax", "Tax withheld"), t("reallife_stk_sh_next", "Next payment") },
                    SideValues = new[] { "$12,000", "142", "2 / 8", "$212", "$21", "$108, day 41" },
                    About = "Its price today is 101.20 for each 100. Sold now it brings $12,200.",
                    Fields = new[] { SPick(0, t("reallife_stk_sh_putback", "Interest"), "Put 50% of its interest back into it"), SSwitch(1, t("reallife_stk_sh_roll", "At its end"), true, t) },
                    Extras = new[] { new UiActionModel { Label = t("reallife_mkt_bond_sell", "Sell"), Kind = UiButtonKind.Destructive, Key = UiAction.Count } }
                };
                return true;
            case "sheet_future":
            case "sheet_option":
            case "sheet_contract":
            {
                m.Page = StocksPage.Futures;
                StocksOffers(m, t, state == "sheet_contract" ? "futures_held" : "futures");
                if (state == "sheet_contract")
                {
                    m.Sheet = new StocksSheetModel
                    {
                        Kicker = t("reallife_mkt_tab_futures", "Futures"), Title = t("reallife_stk_sh_contract", "Your contract"), Emblem = UiIcon.Chart, Back = back,
                        Name = "Steel, the week from day 90", Big = "$5,750", Sub = "Future bought",
                        SideLabels = new[] { t("reallife_stk_sh_qty", "Size"), t("reallife_stk_sh_strike_price", "Agreed price"), t("reallife_stk_sh_settles", "Settles on day"), t("reallife_stk_sh_made", "Made so far"), t("reallife_stk_sh_margin", "Held as margin") },
                        SideValues = new[] { "5,000", "$1.05", "95", "+$480", "$900" },
                        About = "Settled in cash at the close of day 95: that day's price against $1.05.",
                        Extras = new[] { new UiActionModel { Label = t("reallife_mkt_drv_close", "Close it"), Kind = UiButtonKind.Destructive, Key = UiAction.Count } }
                    };
                    return true;
                }
                bool option = state == "sheet_option";
                m.Sheet = new StocksSheetModel
                {
                    Kicker = t("reallife_mkt_tab_futures", "Futures"), Title = option ? t("reallife_stk_sh_open_option", "Open an option") : t("reallife_stk_sh_open_contract", "Open a contract"), Emblem = UiIcon.Chart, Back = back,
                    Name = option ? "Greenmarket Foods" : "Steel", Big = option ? "$37.00" : "$1.15", Sub = option ? "GMF  +1.4%" : "Metals, glass and stone  -0.4%",
                    SideLabels = new[] { "The week from day 90", "The week from day 97", "The week from day 104", "The week from day 111" },
                    SideValues = new[] { "$1.14", "$1.14", "$1.13", "$1.15" },
                    Fields = new[]
                    {
                        SPick(0, t("reallife_stk_sh_what", "What"), option ? "Buy a call" : "Buy a future"), SPick(1, t("reallife_stk_sh_week", "Settles"), "Day 95: the week from day 90"),
                        SPick(2, t("reallife_stk_sh_strike", "Strike"), "At the price: $37.00"), SPick(3, t("reallife_stk_sh_units", "Size"), option ? "500 shares" : "2,500 units"),
                        new StocksFieldModel { Kind = StocksFieldKind.Head, Label = t("reallife_stk_sh_quote", "Quote") },
                        SFact(t("reallife_mkt_col_price", "Price"), "$1.15"), SFact(t("reallife_stk_sh_value", "Worth"), "$2,875"), SFact(t("reallife_stk_sh_margin", "Held as margin"), "$575"),
                        SFact(t("reallife_stk_sh_fee", "Commission"), "$3"), SNote("Settled in cash at the close of day 95: that day's price against $1.15.")
                    },
                    Confirm = t("reallife_mkt_drv_buy", "Buy")
                };
                return true;
            }
            case "sheet_firm":
            case "sheet_firm_held":
            {
                bool held = state == "sheet_firm_held";
                m.Page = StocksPage.Bots;
                StocksOffers(m, t, held ? "bots_rules" : "bots");
                var fields = new System.Collections.Generic.List<StocksFieldModel>();
                if (!held)
                {
                    fields.Add(SStep(0, t("reallife_mkt_bot_col_capital", "Capital"), "$10,000", true));
                }
                fields.Add(SPick(1, t("reallife_stk_sh_size", "Largest holding"), "20% of its worth"));
                fields.Add(SPick(2, t("reallife_stk_sh_stop", "Stop"), "Sell at 10% under cost"));
                fields.Add(SPick(3, t("reallife_stk_sh_loss", "Most it may lose"), "35% of its capital"));
                fields.Add(SPick(4, t("reallife_stk_sh_scope", "Trades"), "The whole market"));
                fields.Add(SPick(7, t("reallife_stk_sh_payout", "Payout"), "Pay me 20% of new profit"));
                fields.Add(SPick(8, t("reallife_stk_sh_every", "Paid"), "Every month"));
                if (held)
                {
                    fields.Add(SStep(9, t("reallife_stk_sh_change", "Add or take out"), "$5,000", true));
                }
                m.Sheet = new StocksSheetModel
                {
                    Kicker = t("reallife_mkt_tab_bots", "Bots"), Title = t("reallife_stk_sh_firm", "Trading firm"), Emblem = UiIcon.Cpu, Back = back,
                    Name = "Meridian Trend Partners", Big = held ? "$108,400" : "Free", Sub = "Trend following" + (held ? "  Running  +8.40%" : string.Empty),
                    SideLabels = held ? new[] { t("reallife_stk_sh_fee_month", "Fee a month"), t("reallife_stk_sh_profit_share", "Share of profit"), t("reallife_mkt_bot_col_capital", "Capital"), t("reallife_mkt_bot_col_trades", "Trades"), t("reallife_stk_sh_paid_out", "Paid to you") }
                        : new[] { t("reallife_stk_sh_fee_month", "Fee a month"), t("reallife_stk_sh_profit_share", "Share of profit"), t("reallife_stk_sh_fee_least", "Fee at least"), t("reallife_stk_sh_capital_least", "Capital at least") },
                    SideValues = held ? new[] { "0.10%", "10.00%", "$100,000", "41 / 25 / 22", "$3,200" } : new[] { "0.10%", "10.00%", "$5", "$1,000" },
                    About = "Buys when the 20 day average rises over the 50 day average and sells when it falls under.",
                    Fields = fields.ToArray(),
                    Confirm = held ? null : t("reallife_mkt_bot_hire", "Hire"),
                    Extras = held ? new[]
                    {
                        new UiActionModel { Label = t("reallife_mkt_bot_add", "Add"), Kind = UiButtonKind.Secondary, Key = UiAction.Count },
                        new UiActionModel { Label = t("reallife_mkt_bot_take", "Take out"), Kind = UiButtonKind.Secondary, Key = UiAction.Count },
                        new UiActionModel { Label = t("reallife_mkt_bot_sell_up", "Sell up"), Kind = UiButtonKind.Secondary, Key = UiAction.Count },
                        new UiActionModel { Label = t("reallife_mkt_bot_dismiss", "Dismiss"), Kind = UiButtonKind.Destructive, Key = UiAction.Count }
                    } : new UiActionModel[0]
                };
                return true;
            }
            case "sheet_rule":
                m.Page = StocksPage.Bots;
                StocksOffers(m, t, "bots_rules");
                m.Sheet = new StocksSheetModel
                {
                    Kicker = t("reallife_mkt_tab_bots", "Bots"), Title = t("reallife_stk_sh_new_rule", "New rule"), Emblem = UiIcon.Cpu, Back = back,
                    Name = "Dip buyer", Sub = "Greenmarket Foods  GMF", About = "Buys while the 14 day RSI is under the level, and sells when it climbs over 70.",
                    Fields = new[]
                    {
                        SPick(1, t("reallife_stk_sh_rule", "Rule"), "Dip buyer"), SPick(0, t("reallife_stk_sh_company", "Company"), "Greenmarket Foods  GMF"),
                        SStep(2, t("reallife_stk_sh_rsi_under", "RSI under"), "30"), SPick(4, t("reallife_stk_sh_order_type", "Order type"), "Market"),
                        SStep(5, t("reallife_stk_sh_budget", "Budget"), "$20,000", true), SStep(6, t("reallife_stk_sh_per_trade", "Shares a trade"), "10"),
                        SStep(11, t("reallife_stk_sh_max_open", "Most open"), "1"), SSwitch(8, t("reallife_stk_sh_only_open", "Only while open"), true, t),
                        SSwitch(9, t("reallife_stk_sh_reinvest", "Reinvest gains"), false, t), SSwitch(10, t("reallife_stk_sh_tell_me", "Tell me"), true, t)
                    },
                    Confirm = t("reallife_stk_sh_add_rule", "Add the rule")
                };
                return true;
            case "sheet_order":
                m.Page = StocksPage.Orders;
                StocksOffers(m, t, "orders_table");
                m.Sheet = new StocksSheetModel
                {
                    Kicker = t("reallife_mkt_tab_orders", "Orders"), Title = t("reallife_stk_sh_order", "Order"), Emblem = UiIcon.Receipt, Back = back,
                    Name = "Greenmarket Foods", Big = "Open", Sub = "GMF  Buy  Limit",
                    SideLabels = new[] { t("reallife_mkt_col_shares", "Shares"), t("reallife_stk_limit", "Limit"), t("reallife_mkt_col_lasts", "Lasts"), t("reallife_stk_sh_placed", "Placed on day") },
                    SideValues = new[] { "100", "$36.20", "Until day 96", "412" },
                    Extras = new[]
                    {
                        new UiActionModel { Label = t("reallife_mkt_open_company", "Open the company"), Kind = UiButtonKind.Secondary, Key = UiAction.Count },
                        new UiActionModel { Label = t("reallife_mkt_cancel_order", "Cancel the order"), Kind = UiButtonKind.Secondary, Key = UiAction.Count }
                    }
                };
                return true;
            case "sheet_margin":
                m.Page = StocksPage.Portfolio;
                Portfolio(m, t);
                m.Sheet = new StocksSheetModel
                {
                    Kicker = t("reallife_mkt_tab_portfolio", "Portfolio"), Title = t("reallife_mkt_deposit", "Deposit"), Emblem = UiIcon.Bank, Back = back,
                    Name = "Margin account", Big = "$12,000",
                    SideLabels = new[] { t("reallife_stk_sh_equity", "Equity"), t("reallife_stk_sh_maintenance", "Must keep"), t("reallife_stk_sh_withdrawable", "Can be withdrawn"), t("reallife_stk_cash", "Cash") },
                    SideValues = new[] { "$48,000", "$9,000", "$3,100", "$24,000" },
                    Fields = new[] { SStep(0, t("reallife_stk_sh_amount", "Amount"), "$5,000", true) },
                    Confirm = t("reallife_mkt_deposit", "Deposit")
                };
                return true;
            case "sheet_listing":
            case "sheet_founder":
            {
                m.Page = StocksPage.Listing;
                Listing(m, t);
                bool founder = state == "sheet_founder";
                m.Sheet = new StocksSheetModel
                {
                    Kicker = t("reallife_stk_nav_listing", "Listing"), Title = founder ? t("reallife_stk_sh_deal", "Deal in your shares") : t("reallife_lst_publish", "Open the book"), Emblem = UiIcon.Presentation, Back = back,
                    Name = "Greenmarket Foods", Big = founder ? "$20.00" : null, Sub = founder ? "GMF" : "Audited",
                    SideLabels = founder ? new[] { t("reallife_stk_sh_you_hold", "You hold"), t("reallife_stk_sh_stake", "Your stake"), t("reallife_stk_cash", "Cash") } : new[] { t("reallife_stk_sh_offered", "Shares offered"), t("reallife_stk_sh_range", "Price range"), t("reallife_stk_sh_raise", "Raised"), t("reallife_stk_sh_fee", "Commission"), t("reallife_stk_sh_keep", "You keep") },
                    SideValues = founder ? new[] { "800,000", "80%", "$24,000" } : new[] { "200,000", "$18.00 - $22.00", "$4,000,000", "$220,000", "80%" },
                    Fields = founder
                        ? new[] { SPick(1, t("reallife_mkt_col_side", "Side"), "Buy"), SStep(0, t("reallife_mkt_col_shares", "Shares"), "1,000", true), SFact(t("reallife_mkt_col_price", "Price"), "$20.00"), SFact(t("reallife_stk_sh_total", "Total"), "$20,060", -1) }
                        : new[] { new StocksFieldModel { Kind = StocksFieldKind.Text, Id = 4, Label = t("reallife_stk_sh_name", "Name"), Value = "Greenmarket Foods" }, SPick(2, t("reallife_stk_sh_float", "Offered"), "20% of the company"), SPick(3, t("reallife_stk_sh_walk", "Least price"), "The bottom of the range") },
                    Confirm = founder ? t("reallife_stk_sh_deal", "Deal in your shares") : t("reallife_lst_publish", "Open the book")
                };
                return true;
            }
        }
        return false;
    }
}
