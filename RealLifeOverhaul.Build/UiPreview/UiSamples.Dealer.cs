using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Car Showroom's samples: the stock as a catalogue (empty, a few cars, a full lot), the market, an auction, the offers, the money and the reports.
internal static partial class UiSamples
{
    private static EstateModel DealerBase(UiWords t, int tab, string offers)
    {
        UiIcon[] icons = { UiIcon.Store, UiIcon.Car, UiIcon.Tag, UiIcon.Handshake, UiIcon.Receipt, UiIcon.Rocket, UiIcon.Chart };
        string[] labels =
        {
            W(t, "reallife_dealer_tab_lots", "Dealerships"), W(t, "reallife_dealer_tab_stock", "Stock"), W(t, "reallife_dealer_tab_market", "Market"), W(t, "reallife_dealer_tab_offers", "Offers"),
            W(t, "reallife_dealer_tab_finance", "Finance"), W(t, "reallife_dealer_tab_growth", "Growth"), W(t, "reallife_dealer_tab_reports", "Reports")
        };
        string[] counts = { "2", "12", null, offers, null, null, null };
        var nav = new UiNavEntry[7];
        for (int i = 0; i < nav.Length; i++)
        {
            nav[i] = new UiNavEntry { Icon = icons[i], Label = labels[i], Count = counts[i] };
        }
        return new EstateModel { Title = W(t, "reallife_dealer_app", "Car Showroom"), Icon = UiIcon.Car, Nav = nav, Tab = tab, TabsAcross = true, Prompts = EstatePrompts(t) };
    }

    public static EstateModel Dealer(UiWords t, string state)
    {
        var runs = new List<Func<bool>>();
        EstateModel m;
        switch (state)
        {
            case "off":
                m = DealerBase(t, 1, null);
                m.Nav = new UiNavEntry[0];
                m.Shape = EstateShape.Off;
                m.OffTitle = W(t, "reallife_dealer_off_title", "The Car Showroom is off");
                m.OffBody = W(t, "reallife_dealer_off_body", "Turn it on in Mod settings, or load a city to run a showroom.");
                break;
            case "stock":
            case "stock_empty":
            case "stock_full":
                m = DealerBase(t, 1, "3");
                Stock(t, m, runs, state);
                break;
            case "market":
                m = DealerBase(t, 2, "3");
                DealerMarket(t, m, runs);
                break;
            case "used":
                m = DealerBase(t, 2, "3");
                DealerUsed(t, m, runs);
                break;
            case "auction":
            case "auction_new":
                m = DealerBase(t, 2, "3");
                DealerAuction(t, m, runs, state == "auction_new");
                break;
            case "offers":
                m = DealerBase(t, 3, "3");
                DealerOffers(t, m, runs);
                break;
            case "deal":
                m = DealerBase(t, 3, "3");
                DealerDeal(t, m, runs);
                break;
            case "finance":
                m = DealerBase(t, 4, "3");
                DealerFinance(t, m);
                break;
            case "growth":
            case "event":
                m = DealerBase(t, 5, "3");
                DealerGrowth(t, m, runs, state == "event");
                break;
            case "reports":
            case "reports_new":
                m = DealerBase(t, 6, "3");
                DealerReports(t, m, runs, state == "reports_new");
                break;
            default:
                m = DealerBase(t, 0, "3");
                DealerLots(t, m, runs, state == "yard");
                break;
        }
        m.Runs = runs.ToArray();
        return m;
    }

    private static UiCatalogueCard Car(string name, string price, string days, UiRole tone)
    {
        return new UiCatalogueCard { Icon = UiIcon.Car, Name = name, Figure = price, State = days, StateTone = tone };
    }

    private static void Stock(UiWords t, EstateModel m, List<Func<bool>> runs, string state)
    {
        m.Shape = EstateShape.Catalogue;
        var cards = new List<UiCatalogueCard>();
        string[] names = { "Meridian GT coupe", "Atlas pickup", "Halden sedan", "Sora hatchback", "Volta SUV", "Pinecrest saloon", "Courier van", "Marlow sedan", "Boxer box truck", "Brisa roadster", "Dune camper van", "Ranger pickup" };
        string[] prices = { "$84,500", "$38,900", "$27,400", "$19,800", "$44,200", "$16,900", "$34,900", "$14,300", "$52,000", "$22,700", "$18,900", "$25,600" };
        int[] days = { 6, 21, 9, 3, 33, 12, 17, 41, 5, 14, 27, 8 };
        int count = state == "stock_empty" ? 0 : 12;
        for (int i = 0; i < count; i++)
        {
            int d = days[i % days.Length];
            cards.Add(Car(names[i % names.Length], prices[i % prices.Length], Fx(W(t, "reallife_ui_dealer_days_chip", "{days} days"), "days", d.ToString()), d >= 30 ? UiRole.Danger : d >= 14 ? UiRole.Warn : UiRole.Notice));
        }
        var c = new UiCatalogueModel
        {
            Cards = cards.ToArray(), CardCount = cards.Count, Chosen = cards.Count > 0 ? 0 : -1,
            EmptyTitle = W(t, "reallife_dealer_row_no_dealership", "No dealership yet"), EmptyBody = W(t, "reallife_ui_dealer_empty_stock_body", "The cars you buy appear here with their pictures, their prices and their days on the lot."), EmptyIcon = UiIcon.Car
        };
        var keys = new string[cards.Count];
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = "car:" + i;
        }
        m.RowKeys = keys;
        if (cards.Count > 0)
        {
            c.DetailKicker = Fx(W(t, "reallife_ui_dealer_kicker_bay", "{name}, bay {bay}, {state}"), "name", "Pioneer Motors", "bay", "4", "state", W(t, "reallife_dealer_state_on_show", "On show"));
            c.DetailLabels = new[]
            {
                W(t, "reallife_ui_dealer_l_cost", "Cost, all in"), W(t, "reallife_ibox_worth", "Worth"), W(t, "reallife_ibox_asking", "Asking"), W(t, "reallife_ui_dealer_l_margin", "Margin at asking"),
                W(t, "reallife_ui_dealer_l_condition", "Condition"), W(t, "reallife_ui_dealer_l_km", "Mileage"), W(t, "reallife_ui_dealer_l_source", "Came from")
            };
            c.DetailValues = new[] { "$72,300", "$83,800", "$84,500", "+$12,200", "92%", "12,400 km", W(t, "reallife_dealer_kind_used", "A private owner") + ", " + Fx(W(t, "reallife_ui_dealer_days_on_lot", "{days} days on the lot"), "days", "6") };
            c.DetailNote = Fx(W(t, "reallife_ui_dealer_rival_asks", "{name} asks {price} for the same model."), "name", "Halden Motors", "price", "$86,900") + " "
                + Fx(W(t, "reallife_ui_dealer_views", "Looked at by {today} today and {yesterday} yesterday."), "today", "4", "yesterday", "7") + " "
                + Fx(W(t, "reallife_ui_dealer_looked_times", "{name} looked at it {times} times and left an offer of {offer}, in Offers."), "name", "Marcus Bell", "times", "2", "offer", "$82,000");
            runs.Add(Yes);
            runs.Add(Yes);
            runs.Add(Yes);
            runs.Add(Yes);
            c.Actions = new[]
            {
                new UiActionModel { Label = Fx(W(t, "reallife_ui_dealer_ask_to", "Ask {price}"), "price", "$81,900"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Minus, Key = UiAction.Count, Command = UiCatalogue.First + UiCatalogue.Action, Arg = 0 },
                new UiActionModel { Label = Fx(W(t, "reallife_ui_dealer_ask_to", "Ask {price}"), "price", "$86,100"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Plus, Key = UiAction.Count, Command = UiCatalogue.First + UiCatalogue.Action, Arg = 1 },
                new UiActionModel { Label = W(t, "reallife_dealer_button_prepare", "Prepare it"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Wrench, Key = UiAction.Option, Command = UiCatalogue.First + UiCatalogue.Action, Arg = 2 },
                new UiActionModel { Label = W(t, "reallife_dealer_button_trade", "Sell to the trade"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Coin, Key = UiAction.Count, Command = UiCatalogue.First + UiCatalogue.Action, Arg = 3 }
            };
            m.CatalogueRuns = new[] { 0, 1, 2, 3 };
        }
        m.Catalogue = c;
        string[][] chips = { new[] { "reallife_ui_dealer_filter_all", "All {count}", "12" }, new[] { "reallife_ui_dealer_filter_new", "New {count}", "3" }, new[] { "reallife_ui_dealer_filter_used", "Used {count}", "7" },
            new[] { "reallife_ui_dealer_filter_shop", "In the workshop {count}", "2" } };
        m.FilterChips = new UiActionModel[count > 0 ? chips.Length : 0];
        for (int i = 0; i < m.FilterChips.Length; i++)
        {
            runs.Add(Yes);
            m.FilterChips[i] = new UiActionModel { Label = Fx(W(t, chips[i][0], chips[i][1]), "count", chips[i][2]), Kind = UiButtonKind.Chip, Command = EstateFace.ActionCommand, Arg = runs.Count - 1 };
        }
        string[][] sorts = { new[] { "reallife_ui_dealer_sort_bay", "By bay number" }, new[] { "reallife_ui_dealer_sort_price", "By price" }, new[] { "reallife_ui_dealer_sort_days", "Longest on the lot" } };
        m.SortChips = new UiActionModel[count > 0 ? sorts.Length : 0];
        for (int i = 0; i < m.SortChips.Length; i++)
        {
            runs.Add(Yes);
            m.SortChips[i] = new UiActionModel { Label = W(t, sorts[i][0], sorts[i][1]), Kind = UiButtonKind.Chip, Command = EstateFace.ActionCommand, Arg = runs.Count - 1 };
        }
        m.SearchHint = count > 0 ? W(t, "reallife_ui_dealer_search", "Search a model") : null;
        m.HeadSub = Fx(W(t, "reallife_ui_dealer_on_lot", "On the lot, {cars} of {bays} bays"), "cars", count.ToString(), "bays", state == "stock_full" ? "12" : "20");
        m.FigLabels = new[] { W(t, "reallife_ui_dealer_stock_worth", "Stock worth") };
        m.FigValues = new[] { count == 0 ? "$0" : "$400,100" };
    }

    private static void DealerLots(UiWords t, EstateModel m, List<Func<bool>> runs, bool yard)
    {
        if (yard)
        {
            m.Shape = EstateShape.Detail;
            m.Back = W(t, "reallife_ui_dealer_back", "Back");
            m.DetailIcon = UiIcon.Store;
            m.DetailTitle = "Pioneer Motors";
            m.DetailSub = Fx(W(t, "reallife_ui_dealer_rep_line", "Reputation {rep}, {step}"), "rep", "340", "step", W(t, "reallife_dealer_step_2", "Trusted"));
            m.DetailChip = W(t, "reallife_dealer_step_2", "Trusted");
            m.DetailChipTone = UiRole.Notice;
            m.Left = new[]
            {
                new UiFactCardModel
                {
                    Title = W(t, "reallife_ibox_sec_now", "Now"), Heading = true,
                    Lines = new[]
                    {
                        UiFactLine.Say(Fx(W(t, "reallife_dealer_yard_rep", "Reputation {rep}, {step}. {next}"), "rep", "340", "step", W(t, "reallife_dealer_step_2", "Trusted"), "next", "60 points to the next step")),
                        UiFactLine.Bar(W(t, "reallife_ibox_lot", "The lot"), 60), UiFactLine.Section(W(t, "reallife_ibox_today", "Today")),
                        UiFactLine.Say(Fx(W(t, "reallife_dealer_yard_today", "{visitors} visitors, {served} served, {lost} lost, {offers} offers, {sold} sold."), "visitors", "31", "served", "24", "lost", "7", "offers", "5", "sold", "2"))
                    }
                }
            };
            m.Right = new[]
            {
                new UiFactCardModel
                {
                    Title = W(t, "reallife_ui_dealer_terms", "Terms"), Heading = true,
                    Lines = new[]
                    {
                        EstateSwitch(runs, W(t, "reallife_ui_dealer_sw_fixed", "Fixed prices"), false),
                        EstateStep(runs, W(t, "reallife_ui_dealer_staff_floor", "Staff may close at"), "92%", W(t, "reallife_ui_dealer_staff_floor_what", "The staff close a deal on their own at this share of the asking price.")),
                        EstateSwitch(runs, W(t, "reallife_ui_dealer_sw_finance", "Offer finance"), true), EstateSwitch(runs, W(t, "reallife_ui_dealer_sw_warranty", "Offer warranties"), true),
                        EstateSwitch(runs, W(t, "reallife_ui_dealer_sw_extras", "Offer extras"), false)
                    },
                    Actions = new[] { EstateButton(runs, W(t, "reallife_dealer_button_relay", "Lay the lot out again"), UiButtonKind.Secondary, UiIcon.Grid, UiAction.Count) }
                }
            };
            return;
        }
        var rows = new List<UiDataRow>
        {
            Row(new[] { "Pioneer Motors", Fx(W(t, "reallife_dealer_lot_count", "{cars} cars on {places} places"), "cars", "12", "places", "20"), "3", "31", "2", "$93,500", "5" }, new[] { (int)UiIcon.Store, 0, 0, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.WarnText }, new[] { W(t, "reallife_dealer_step_2", "Trusted"), null, null, null, null, null, null }),
            Row(new[] { "Eastside Auto, a showroom with a long name", Fx(W(t, "reallife_dealer_lot_count", "{cars} cars on {places} places"), "cars", "4", "places", "8"), "0", "9", "0", "$0", "0" }, new[] { (int)UiIcon.Store, 0, 0, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.DangerText, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_dealer_step_0", "New"), null, null, null, null, null, null })
        };
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_showroom", "Showroom"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_lot", "Lot"), Kind = UiCellKind.Text, Width = 168f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_staff", "Staff"), Kind = UiCellKind.Figure, Width = 72f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_visitors", "Visitors"), Kind = UiCellKind.Figure, Width = 88f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_sold", "Sold"), Kind = UiCellKind.Figure, Width = 72f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_takings", "Takings today"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_offers", "Offers"), Kind = UiCellKind.Figure, Width = 80f }
        };
        var side = new UiFactCardModel
        {
            Title = W(t, "reallife_dealer_lots_title", "Your showrooms"), Heading = true,
            Lines = new[] { UiFactLine.Say(W(t, "reallife_dealer_lots_about", "Each showroom keeps its own lot, staff and figures.")), UiFactLine.Say(W(t, "reallife_dealer_lots_staff", "A showroom without a salesperson sells nothing.")) }
        };
        TableOf(m, UiIcon.Store, W(t, "reallife_dealer_tab_lots", "Dealerships"), Fx(W(t, "reallife_ui_dealer_lots_sub", "{count} showrooms"), "count", "2"), cols, rows, side, W(t, "reallife_dealer_row_no_dealership", "No dealership yet"),
            W(t, "reallife_dealer_lots_about", "Each showroom keeps its own lot, staff and figures."));
    }

    private static void DealerMarket(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        var rows = new List<UiDataRow>();
        string[][] l =
        {
            new[] { "Volta SUV", "Red", "new", "4 km", "100%", "$38,200", "today", "new" },
            new[] { "Marlow sedan", "Black", "used", "88,200 km", "61%", "$11,900", "today", "ni" },
            new[] { "Courier van", "White", "auction", "31,800 km", "86%", "$29,400", "2 h", "rival" },
            new[] { "Meridian GT coupe", "Blue", "auction", "12,400 km", "92%", "$21,800", "5 h", "lead" },
            new[] { "Atlas pickup", "Grey", "new", "6 km", "100%", "$36,400", "today", "new" },
            new[] { "Halden sedan", "Silver", "used", "64,900 km", "72%", "$18,600", "today", "ni" },
            new[] { "Brisa roadster", "Yellow", "used", "41,300 km", "80%", "$20,900", "today", "ni" },
            new[] { "Pinecrest saloon", "Green", "auction", "52,700 km", "69%", "$12,300", "7 h", "rival" },
            new[] { "Boxer box truck", "White", "auction", "97,100 km", "58%", "$27,800", "9 h", "rival" },
            new[] { "Sora hatchback", "Red", "new", "3 km", "100%", "$21,200", "today", "new" },
            new[] { "Marlow sedan", "Blue", "used", "112,400 km", "49%", "$7,900", "today", "ni" }
        };
        foreach (string[] r in l)
        {
            string kind = r[2] == "new" ? W(t, "reallife_dealer_kind_wholesale", "New") : r[2] == "used" ? W(t, "reallife_dealer_kind_used", "Used") : W(t, "reallife_dealer_kind_auction", "Auction");
            string chip = r[7] == "new" ? W(t, "reallife_dealer_state_new", "New") : r[7] == "ni" ? W(t, "reallife_dealer_state_not_inspected", "Not inspected") : r[7] == "rival" ? Fx(W(t, "reallife_dealer_state_rival", "{rival} leads"), "rival", "Halden Motors") : W(t, "reallife_dealer_state_leading", "You lead");
            rows.Add(Row(new[] { r[0], kind, r[3], r[4], r[5], r[6], chip }, new[] { (int)(r[2] == "auction" ? UiIcon.Tag : r[2] == "used" ? UiIcon.Car : UiIcon.Store), 0, 0, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, r[7] == "lead" ? UiRole.Ok : r[7] == "rival" ? UiRole.Warn : UiRole.Notice }, new[] { r[1], null, null, null, null, null, null }));
        }
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_car", "Car"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_kind", "Kind"), Kind = UiCellKind.Text, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_km", "Kilometres"), Kind = UiCellKind.Figure, Width = 96f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_condition", "Condition"), Kind = UiCellKind.Text, Width = 136f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_price", "Price"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_time", "Time"), Kind = UiCellKind.Text, Width = 96f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_state", "State"), Kind = UiCellKind.Chip, Width = 216f }
        };
        var side = new UiFactCardModel
        {
            Title = W(t, "reallife_ibox_sec_now", "Now"), Heading = true,
            Lines = new[]
            {
                UiFactLine.Row(W(t, "reallife_dealer_cash", "Cash"), "$1,903,000"),
                UiFactLine.Say(Fx(W(t, "reallife_dealer_market_sum", "{new} new, {used} used and {lots} lots at the auction today."), "new", "4", "used", "5", "lots", "4")),
                UiFactLine.Say(W(t, "reallife_dealer_market_about", "New cars come from the wholesaler, used ones from their owners, and the auction's lots go to the highest bid."))
            }
        };
        TableOf(m, UiIcon.Tag, W(t, "reallife_dealer_market_title", "The car market"), Fx(W(t, "reallife_ui_dealer_market_sub", "{new} new, {used} used, {lots} lots at auction"), "new", "4", "used", "5", "lots", "4"), cols, rows, side,
            W(t, "reallife_ui_dealer_empty_market", "Nothing is on the market today"), W(t, "reallife_dealer_market_about", "New cars come from the wholesaler."));
    }

    private static void DealerUsed(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        m.Shape = EstateShape.Detail;
        m.Back = W(t, "reallife_ui_dealer_back", "Back");
        m.DetailIcon = UiIcon.Car;
        m.DetailTitle = "Marlow sedan";
        m.DetailSub = "Black";
        m.DetailChip = W(t, "reallife_ibox_chip_not_inspected", "Not inspected");
        m.DetailChipTone = UiRole.Warn;
        m.Right = new[]
        {
            new UiFactCardModel
            {
                Title = W(t, "reallife_ibox_sec_car", "The car"), Heading = true,
                Lines = new[] { UiFactLine.Say(Fx(W(t, "reallife_dealer_used_said", "{km}, said to be in {condition} condition. Its faults are not known until it is inspected."), "km", "88,200 km", "condition", "61%")) }
            }
        };
        m.Left = new[]
        {
            new UiFactCardModel
            {
                Title = W(t, "reallife_ibox_sec_money", "Money"), Heading = true,
                Lines = new[]
                {
                    UiFactLine.Say(Fx(W(t, "reallife_dealer_used_asking", "Asking {price}. Their last counter: {counter}. {left} offers left."), "price", "$11,900", "counter", W(t, "reallife_dealer_none", "none"), "left", "3")),
                    UiFactLine.Say(Fx(W(t, "reallife_dealer_goes_to", "Goes to {name}, {free} places free"), "name", "Pioneer Motors", "free", "8")),
                    EstateStep(runs, W(t, "reallife_ui_dealer_your_offer", "Your offer"), "$10,700", null)
                },
                Actions = new[]
                {
                    EstateButton(runs, W(t, "reallife_dealer_button_inspect", "Inspect it"), UiButtonKind.Secondary, UiIcon.Eye, UiAction.Count),
                    EstateButton(runs, W(t, "reallife_dealer_button_buy", "Buy"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option),
                    EstateButton(runs, W(t, "reallife_dealer_button_offer", "Make an offer"), UiButtonKind.Primary, UiIcon.Handshake, UiAction.Alt)
                }
            }
        };
    }

    private static void DealerAuction(UiWords t, EstateModel m, List<Func<bool>> runs, bool nobid)
    {
        m.Shape = EstateShape.Auction;
        var a = new EstateAuction { Kicker = W(t, "reallife_ui_dealer_auction_kicker", "Live auction, the trade car auction"), Title = "Courier van", Icon = UiIcon.Car, Aspect = 1.8f, Back = W(t, "reallife_ui_dealer_back", "Back") };
        a.Left = new UiFactCardModel
        {
            Lines = new[]
            {
                UiFactLine.Row(W(t, "reallife_ui_dealer_l_worth", "Market value"), "$33,800"), UiFactLine.Row(W(t, "reallife_ui_dealer_l_condition", "Condition"), "86%"), UiFactLine.Row(W(t, "reallife_ui_dealer_l_km", "Mileage"), "31,800 km"),
                UiFactLine.Row(W(t, "reallife_ui_dealer_free_bays", "Your free bays"), "8"), UiFactLine.Section(W(t, "reallife_ui_dealer_if_you_win", "If you win")),
                UiFactLine.Row(W(t, "reallife_ui_dealer_your_bid", "Your bid"), "$30,000"), UiFactLine.Row(Fx(W(t, "reallife_ui_dealer_buyers_fee", "Auction fee, {share}"), "share", "5%"), "$1,500"),
                UiFactLine.Row(W(t, "reallife_ui_dealer_you_pay", "You pay"), "$31,500"), UiFactLine.Row(W(t, "reallife_ui_dealer_worth_lot", "Worth on your lot"), "$33,800"),
                UiFactLine.Row(W(t, "reallife_ui_dealer_left_for_you", "Left for you"), "+$2,300", UiRole.OkText)
            }
        };
        var lines = new List<UiFactLine>
        {
            UiFactLine.Row(W(t, "reallife_ui_dealer_current_bid", "Current bid"), nobid ? "$21,600" : "$29,400"),
            UiFactLine.Row(W(t, "reallife_ui_dealer_held_by", "Held by"), nobid ? W(t, "reallife_dealer_state_no_bid", "No bid yet") : Fx(W(t, "reallife_dealer_state_rival", "{rival} leads"), "rival", "Halden Motors")),
            UiFactLine.Bar(Fx(W(t, "reallife_ui_dealer_time_left", "Time left, {time}"), "time", "2 hours"), 40),
            EstateStep(runs, W(t, "reallife_ui_dealer_your_bid", "Your bid"), "$30,000", Fx(W(t, "reallife_ui_dealer_bid_hint", "Bids go up by {step}, so the least you can bid is {least}."), "step", "$200", "least", nobid ? "$21,600" : "$29,600")),
        };
        int win = Array.FindIndex(a.Left.Lines, f => f.Kind == UiFactKind.Heading);
        if (win >= 0)
        {
            var ifWin = new UiFactLine[a.Left.Lines.Length - win];
            Array.Copy(a.Left.Lines, win, ifWin, 0, ifWin.Length);
            Array.Resize(ref a.Left.Lines, win);
            lines.AddRange(ifWin);
        }
        if (!nobid)
        {
            lines.Add(UiFactLine.Section(Fx(W(t, "reallife_ui_dealer_bidders", "Bidders, the last {count}"), "count", "4")));
        }
        if (nobid)
        {
            lines.Add(UiFactLine.Say(W(t, "reallife_ui_dealer_no_bids", "Nobody has bid yet.")));
        }
        else
        {
            string[][] bids =
            {
                new[] { "Halden Motors", "20", "$29,400" }, new[] { "Crossway Cars", "60", "$29,000" }, new[] { Fx(W(t, "reallife_ui_dealer_you", "You"), string.Empty), "180", "$28,200" }, new[] { "Pier Street Autos", "300", "$27,500" }
            };
            foreach (string[] b in bids)
            {
                int ago = int.Parse(b[1]) / 60 + 1;
                lines.Add(UiFactLine.Who(b[0], ago == 1 ? W(t, "reallife_ui_dealer_hour_ago", "an hour ago") : Fx(W(t, "reallife_ui_dealer_hours_ago", "{hours} hours ago"), "hours", ago.ToString()), b[2]));
            }
        }
        a.Right = new UiFactCardModel
        {
            Title = Fx(W(t, "reallife_ui_dealer_bidding_on", "Bidding on the {car}"), "car", "Courier van"), Sub = Fx(W(t, "reallife_ui_dealer_colour_line", "{colour}"), "colour", "White"), Lines = lines.ToArray(),
            Actions = new[]
            {
                EstateButton(runs, W(t, "reallife_ui_dealer_walk_away", "Walk away"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option),
                EstateButton(runs, W(t, "reallife_dealer_button_inspect", "Inspect it"), UiButtonKind.Secondary, UiIcon.Eye, UiAction.Count),
                EstateButton(runs, Fx(W(t, "reallife_ui_dealer_bid_button", "Bid {amount}"), "amount", "$30,000"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt)
            }
        };
        m.Auction = a;
    }

    // A longer list for a preview: the rows again for other people, so a table stands as full as a busy showroom's.
    private static void Pad(List<UiDataRow> rows, int to, params string[] names)
    {
        int first = rows.Count;
        for (int i = 0; rows.Count < to; i++)
        {
            UiDataRow from = rows[i % first];
            UiDataRow copy = from;
            copy.Cells = (string[])from.Cells.Clone();
            copy.Cells[0] = names[i % names.Length];
            copy.Hue = UiColourHue(copy.Cells[0]);
            rows.Add(copy);
        }
    }

    private static void DealerOffers(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        var rows = new List<UiDataRow>
        {
            Row(new[] { "Marcus Bell", "Meridian GT coupe", "$82,000", "$84,500", "97", "4 h", W(t, "reallife_dealer_state_waiting", "Waiting") }, new[] { 0, (int)UiIcon.Car, 0, 0, 97, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Warn }, new[] { W(t, "reallife_dealer_profile_bargain", "A bargain hunter"), "Pioneer Motors", null, null, null, null, null }, UiColourHue("Marcus Bell")),
            Row(new[] { "Amara Kimani", "Atlas pickup", "$36,900", "$38,900", "95", "1 h", W(t, "reallife_dealer_state_waiting", "Waiting") }, new[] { 0, (int)UiIcon.Car, 0, 0, 95, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.DangerText, UiRole.Warn }, new[] { W(t, "reallife_dealer_profile_fair", "A family"), "Pioneer Motors", null, null, null, null, null }, UiColourHue("Amara Kimani")),
            Row(new[] { W(t, "reallife_dealer_to_trade", "The trade"), "Volta SUV", "$41,200", "$34,100", string.Empty, "Day 211", Fx(W(t, "reallife_dealer_state_sold", "Sold, margin {margin}"), "margin", "$7,100") }, new[] { 0, (int)UiIcon.Car, 0, 0, -1, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Ok }, new[] { "Pioneer Motors", string.Empty, null, null, null, null, null })
        };
        Pad(rows, 9, "Priya Nair", "Theo Brandt", "Lena Hoffmann", "Imani Cole", "Owen Park", "Yuki Tanaka");
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_buyer", "Buyer"), Kind = UiCellKind.Person, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_car", "Car"), Kind = UiCellKind.Tile, Width = 184f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_offer", "Offer"), Kind = UiCellKind.Money, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_asking", "Asking"), Kind = UiCellKind.Money, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_against", "Against asking"), Kind = UiCellKind.Meter, Width = 120f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_left", "Time left"), Kind = UiCellKind.Text, Width = 80f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_state", "State"), Kind = UiCellKind.Chip, Width = 200f }
        };
        var side = new UiFactCardModel
        {
            Title = W(t, "reallife_dealer_offers_title", "Offers and sales"), Heading = true,
            Lines = new[]
            {
                UiFactLine.Row(W(t, "reallife_dealer_sum_waiting", "Waiting for you"), "2", UiRole.WarnText), UiFactLine.Say(Fx(W(t, "reallife_dealer_offers_today", "{sold} sold today for a margin of {margin}."), "sold", "1", "margin", "$7,100")),
                UiFactLine.Say(W(t, "reallife_dealer_offers_about", "Buyers make an offer and wait for your answer."))
            }
        };
        TableOf(m, UiIcon.Handshake, W(t, "reallife_dealer_offers_title", "Offers and sales"), Fx(W(t, "reallife_ui_dealer_offers_sub", "{waiting} offers waiting, {sold} sales kept"), "waiting", "2", "sold", "1"), cols, rows, side,
            W(t, "reallife_ui_dealer_empty_offers", "No offers or sales yet"), W(t, "reallife_dealer_offers_about", "Buyers make an offer and wait for your answer."));
    }

    private static void DealerDeal(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        m.Shape = EstateShape.Detail;
        m.Back = W(t, "reallife_ui_dealer_back", "Back");
        m.DetailIcon = UiIcon.Handshake;
        m.DetailTitle = "Marcus Bell";
        m.DetailSub = W(t, "reallife_dealer_profile_bargain", "A bargain hunter") + ", Meridian GT coupe, Pioneer Motors";
        m.DetailChip = W(t, "reallife_dealer_state_waiting", "Waiting");
        m.DetailChipTone = UiRole.Warn;
        m.Left = new[]
        {
            new UiFactCardModel
            {
                Title = W(t, "reallife_ibox_sec_offer", "The offer"), Heading = true,
                Lines = new[]
                {
                    UiFactLine.Row(W(t, "reallife_ibox_offer", "Offer"), "$82,000", UiRole.AccentText), UiFactLine.Row(W(t, "reallife_ibox_asking", "Asking"), "$84,500"),
                    UiFactLine.Say(W(t, "reallife_ui_dealer_deal_looked", "They looked at the car {times} times before they made it.").Replace("{times}", "2")),
                    EstateStep(runs, W(t, "reallife_ui_dealer_your_counter", "Your counter"), "$83,200", Fx(W(t, "reallife_ui_dealer_counters_left", "Counters left: {left}"), "left", "2"))
                },
                Actions = new[]
                {
                    EstateButton(runs, W(t, "reallife_dealer_button_decline", "Decline"), UiButtonKind.Destructive, UiIcon.None, UiAction.Count), EstateButton(runs, W(t, "reallife_dealer_button_drive", "Test drive"), UiButtonKind.Secondary, UiIcon.Car, UiAction.Count),
                    EstateButton(runs, W(t, "reallife_dealer_button_counter", "Counter"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option), EstateButton(runs, W(t, "reallife_dealer_button_accept", "Accept"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt)
                }
            }
        };
        m.Right = new[]
        {
            new UiFactCardModel
            {
                Title = W(t, "reallife_ui_dealer_with_it", "With the car"), Heading = true,
                Lines = new[] { UiFactLine.Say(Fx(W(t, "reallife_dealer_deal_finance", "They would put down {deposit}% and pay over {weeks} weeks. Their rating is {grade}."), "deposit", "20", "weeks", "8", "grade", "B")) }
            }
        };
    }

    private static void DealerFinance(UiWords t, EstateModel m)
    {
        var rows = new List<UiDataRow>
        {
            Row(new[] { "Daniel Ortiz", "Halden sedan", "$18,200", "$900", "Day 218", Fx(W(t, "reallife_dealer_plan_paying", "Paying, rated {grade}"), "grade", "B") }, new[] { 0, (int)UiIcon.Car, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Ok }, new[] { "Pioneer Motors", string.Empty, null, null, null, null }, UiColourHue("Daniel Ortiz")),
            Row(new[] { "Lena Hoffmann", "Sora hatchback", "$7,300", "$420", "Day 211", Fx(W(t, "reallife_dealer_plan_missed", "{weeks} weeks missed"), "weeks", "2") }, new[] { 0, (int)UiIcon.Car, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Danger }, new[] { "Pioneer Motors", string.Empty, null, null, null, null }, UiColourHue("Lena Hoffmann")),
            Row(new[] { "Theo Brandt", "Volta SUV", "$1,240", string.Empty, "Day 290", W(t, "reallife_dealer_tier_extended", "Silver") }, new[] { 0, (int)UiIcon.ShieldCheck, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.Warn, UiRole.None, UiRole.None, UiRole.Notice }, new[] { "Pioneer Motors", Fx(W(t, "reallife_ui_dealer_claims", "{count} claims"), "count", "1"), null, null, null, null }, UiColourHue("Theo Brandt"))
        };
        Pad(rows, 9, "Sofia Marchetti", "Marcus Bell", "Owen Park", "Imani Cole", "Priya Nair", "Yuki Tanaka");
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_customer", "Customer"), Kind = UiCellKind.Person, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_car", "Car"), Kind = UiCellKind.Tile, Width = 184f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_owed_paid", "Owed or paid out"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_weekly", "A week"), Kind = UiCellKind.Money, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_until", "Until"), Kind = UiCellKind.Date, Width = 96f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_state", "State"), Kind = UiCellKind.Chip, Width = 200f }
        };
        var side = new UiFactCardModel
        {
            Title = W(t, "reallife_dealer_finance_title", "Finance and warranties"), Heading = true,
            Lines = new[]
            {
                UiFactLine.Say(Fx(W(t, "reallife_dealer_finance_plans", "{plans} plans, {owed} owed, {weekly} a week, {missed} behind."), "plans", "2", "owed", "$25,500", "weekly", "$1,320", "missed", "1")),
                UiFactLine.Say(Fx(W(t, "reallife_dealer_finance_covers", "{covers} warranties, {paid} paid out."), "covers", "1", "paid", "$1,240")), UiFactLine.Say(W(t, "reallife_dealer_finance_about", "Your own finance and warranties are sold with the cars."))
            }
        };
        TableOf(m, UiIcon.Receipt, W(t, "reallife_dealer_finance_title", "Finance and warranties"), Fx(W(t, "reallife_ui_dealer_finance_sub", "{plans} plans, {covers} warranties"), "plans", "2", "covers", "1"), cols, rows, side,
            W(t, "reallife_ui_dealer_empty_finance", "No finance plans or warranties yet"), W(t, "reallife_dealer_finance_about", "Your own finance and warranties are sold with the cars."));
    }

    private static void DealerGrowth(UiWords t, EstateModel m, List<Func<bool>> runs, bool eventPage)
    {
        if (eventPage)
        {
            m.Shape = EstateShape.Detail;
            m.Back = W(t, "reallife_ui_dealer_back", "Back");
            m.DetailIcon = UiIcon.Megaphone;
            m.DetailTitle = W(t, "reallife_dealer_event_0", "A sales weekend");
            m.DetailSub = W(t, "reallife_dealer_event_0_short", "More visitors for a few days");
            m.Left = new[]
            {
                new UiFactCardModel
                {
                    Title = W(t, "reallife_ibox_sec_terms", "Terms"), Heading = true,
                    Lines = new[]
                    {
                        UiFactLine.Say(W(t, "reallife_dealer_event_0_about", "Buyers come from further away for a few days.")), UiFactLine.Row(W(t, "reallife_ibox_cost", "Cost"), "$4,500"),
                        UiFactLine.Say(Fx(W(t, "reallife_dealer_event_terms", "It costs {cost}, runs {days} days and the showroom rests {rest} days after."), "cost", "$4,500", "days", "3", "rest", "14"))
                    }
                }
            };
            m.Right = new[]
            {
                new UiFactCardModel
                {
                    Title = W(t, "reallife_ibox_sec_where", "Where"), Heading = true,
                    Lines = new[] { EstateStep(runs, W(t, "reallife_ibox_sec_where", "Where"), Fx(W(t, "reallife_dealer_event_at", "At {name}"), "name", "Pioneer Motors"), null) },
                    Actions = new[] { EstateButton(runs, W(t, "reallife_dealer_button_event", "Book the event"), UiButtonKind.Primary, UiIcon.Megaphone, UiAction.Alt) }
                }
            };
            return;
        }
        var rows = new List<UiDataRow>
        {
            Row(new[] { W(t, "reallife_dealer_event_0", "A sales weekend"), Fx(W(t, "reallife_dealer_days", "{days} days"), "days", "3"), "$4,500", W(t, "reallife_dealer_event_0_short", "More visitors"), W(t, "reallife_dealer_state_event_ready", "Ready to book") },
                new[] { (int)UiIcon.Megaphone, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Notice }, new[] { W(t, "reallife_ui_dealer_growth_event", "Event"), null, null, null, null }),
            Row(new[] { W(t, "reallife_dealer_mission_0", "Sell ten cars"), "Pioneer Motors", Fx(W(t, "reallife_dealer_progress", "{done} of {target}"), "done", "7", "target", "10"), W(t, "reallife_dealer_mission_0_about", "Sell ten cars to buyers."), W(t, "reallife_dealer_state_mission_open", "Under way") },
                new[] { (int)UiIcon.Trophy, 0, 0, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Notice }, new[] { Fx(W(t, "reallife_ui_dealer_growth_mission", "Mission, {points} points"), "points", "30"), null, null, null, null }),
            Row(new[] { W(t, "reallife_dealer_step_2", "Trusted"), "Pioneer Motors", "340 / 400", W(t, "reallife_dealer_perk_2", "Cheaper wholesale"), string.Empty }, new[] { (int)UiIcon.Star, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_ui_dealer_growth_rep", "Reputation"), null, null, null, null })
        };
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_what", "What"), Kind = UiCellKind.Tile, Width = 216f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_showroom", "Showroom"), Kind = UiCellKind.Text, Width = 176f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_progress", "Progress"), Kind = UiCellKind.Text, Width = 144f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_about", "What it does"), Kind = UiCellKind.Text, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_state", "State"), Kind = UiCellKind.Chip, Width = 152f }
        };
        var side = new UiFactCardModel
        {
            Title = W(t, "reallife_dealer_growth_title", "Growth"), Heading = true,
            Lines = new[] { UiFactLine.Say(W(t, "reallife_dealer_growth_events", "Events bring buyers.")), UiFactLine.Say(W(t, "reallife_dealer_growth_rep", "Reputation grows with sales.")), UiFactLine.Say(W(t, "reallife_dealer_growth_missions", "Missions pay in reputation.")) }
        };
        TableOf(m, UiIcon.Rocket, W(t, "reallife_dealer_growth_title", "Growth"), W(t, "reallife_ui_dealer_growth_sub", "Events to book, missions and reputation"), cols, rows, side, W(t, "reallife_dealer_row_no_dealership", "No dealership yet"), W(t, "reallife_dealer_growth_events", "Events bring buyers."));
    }

    private static void DealerReports(UiWords t, EstateModel m, List<Func<bool>> runs, bool fresh)
    {
        m.Shape = EstateShape.Chart;
        m.HeadIcon = UiIcon.Chart;
        m.HeadTitle = W(t, "reallife_dealer_report_profit", "Profit");
        m.HeadSub = Fx(W(t, "reallife_ui_dealer_report_sub", "{place}: a point a day for the last {days} days, with the average of the last {average} days. Point at a day to read it."), "place", W(t, "reallife_dealer_report_all", "All showrooms"), "days", "60", "average", "7");
        int days = fresh ? 1 : 28;
        var points = new string[days];
        var line = new float[days];
        var avg = new float[days];
        for (int i = 0; i < days; i++)
        {
            points[i] = Fx(W(t, "reallife_ui_dealer_chart_day", "Day {day}"), "day", (187 + i).ToString());
            line[i] = 4200f + ((i * 137 % 9) - 4) * 650f + i * 45f;
            float sum = 0f;
            int n = 0;
            for (int k = Math.Max(0, i - 6); k <= i; k++)
            {
                sum += 4200f + ((k * 137 % 9) - 4) * 650f + k * 45f;
                n++;
            }
            avg[i] = sum / n;
        }
        m.Chart = new UiChartModel
        {
            Kind = UiChartKind.Line, Title = W(t, "reallife_dealer_report_profit", "Profit"), Points = fresh ? new string[0] : points,
            Series = fresh ? new UiSeries[0] : new[]
            {
                new UiSeries { Label = W(t, "reallife_dealer_report_profit", "Profit"), Values = line }, new UiSeries { Label = Fx(W(t, "reallife_ui_dealer_report_average", "Average of {days} days"), "days", "7"), Values = avg }
            },
            Format = v => "$" + ((int)v).ToString("N0", System.Globalization.CultureInfo.InvariantCulture), AxisFormat = v => "$" + ((int)v).ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
            EmptyTitle = W(t, "reallife_ui_dealer_chart_empty", "Not enough days yet"), EmptyBody = W(t, "reallife_dealer_report_none", "The chart fills as days are kept.")
        };
        string[] kinds = { "reallife_dealer_report_sold|Cars sold", "reallife_dealer_report_revenue|Revenue", "reallife_dealer_report_profit|Profit", "reallife_dealer_report_age|Days on the lot", "reallife_dealer_report_visitors|Visitors",
            "reallife_dealer_report_conversion|Conversion", "reallife_dealer_report_arrears|Arrears" };
        m.ChartKinds = new UiActionModel[kinds.Length];
        for (int i = 0; i < kinds.Length; i++)
        {
            string[] k = kinds[i].Split('|');
            runs.Add(Yes);
            m.ChartKinds[i] = new UiActionModel { Label = W(t, k[0], k[1]), Kind = UiButtonKind.Chip, Command = EstateFace.ActionCommand, Arg = runs.Count - 1 };
        }
        m.ChartKind = 2;
        runs.Add(Yes);
        runs.Add(Yes);
        m.ChartPlaces = new[]
        {
            new UiActionModel { Label = W(t, "reallife_dealer_report_all", "All showrooms"), Kind = UiButtonKind.Chip, Command = EstateFace.ActionCommand, Arg = runs.Count - 2 },
            new UiActionModel { Label = "Pioneer Motors", Kind = UiButtonKind.Chip, Command = EstateFace.ActionCommand, Arg = runs.Count - 1 }
        };
        m.ChartPlace = 0;
        m.Side = new UiFactCardModel
        {
            Title = W(t, "reallife_ui_dealer_sums", "Sums"), Heading = true,
            Lines = new[]
            {
                UiFactLine.Section(W(t, "reallife_ibox_sec_this_week", "This week")),
                UiFactLine.Say(Fx(W(t, "reallife_dealer_reports_week", "{days} days: {sold} cars sold for {revenue}, a margin of {margin}, extras {extras}, {spent} spent, {visitors} visitors, {conversion} came to buy."), "days", "7", "sold", "11", "revenue", "$401,800", "margin", "$53,200", "extras", "$4,900", "spent", "$9,000", "visitors", "188", "conversion", "5.9%")),
                UiFactLine.Section(W(t, "reallife_ibox_sec_now", "Now")), UiFactLine.Say(Fx(W(t, "reallife_dealer_reports_now", "{cars} cars, {age} days on the lot on average, {arrears} in arrears over {late} plans."), "cars", "12", "age", "14", "arrears", "$7,300", "late", "1"))
            }
        };
    }
}
