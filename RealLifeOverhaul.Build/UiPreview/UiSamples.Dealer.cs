using System;
using System.Collections.Generic;
using RealLifeOverhaul;
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
        string[] counts = { "2", offers == "0" ? null : offers == "20" ? "20" : "12", null, offers == "0" ? null : "6", null, null, null };
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
                m.OffIcon = UiIcon.Car;
                m.OffTitle = W(t, "reallife_dealer_off_title", "The Car Showroom is off");
                m.OffBody = W(t, "reallife_dealer_off_body", "Turn it on in Mod settings, or load a city to run a showroom.");
                break;
            case "stock":
            case "stock_empty":
            case "stock_full":
                m = DealerBase(t, 1, state == "stock_empty" ? "0" : state == "stock_full" ? "20" : "3");
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
        int amount = 0;
        foreach (char ch in price)
        {
            if (char.IsDigit(ch))
            {
                amount = amount * 10 + (ch - '0');
            }
        }
        // The same rule as the live pages: CarClass, given the kind the game's own vehicle type would have.
        CarKind kind = SampleKind(name);
        return new UiCatalogueCard { Icon = CarClass.Glyph(kind), Tint = CarClass.Tint(kind, amount), Name = name, Figure = price, State = days, StateTone = tone };
    }

    // The kind the game's table (CarCatalogue) gives the sample cars' stand-ins: a van and a truck work, the rest by body.
    private static CarKind SampleKind(string name)
    {
        string n = name.ToLowerInvariant();
        return n.Contains("truck") ? CarKind.Truck : n.Contains("van") ? CarKind.Van : n.Contains("coupe") || n.Contains("roadster") ? CarKind.Sports
            : n.Contains("hatchback") ? CarKind.Economy : n.Contains("suv") || n.Contains("pickup") ? CarKind.Utility : CarKind.Saloon;
    }

    private static void Stock(UiWords t, EstateModel m, List<Func<bool>> runs, string state)
    {
        m.Shape = EstateShape.Catalogue;
        var cards = new List<UiCatalogueCard>();
        string[] names = { "Meridian GT coupe", "Atlas pickup", "Halden sedan", "Sora hatchback", "Volta SUV", "Pinecrest saloon", "Courier van", "Marlow sedan", "Boxer box truck", "Brisa roadster", "Dune camper van", "Ranger pickup" };
        string[] prices = { "$84,500", "$38,900", "$27,400", "$19,800", "$44,200", "$16,900", "$34,900", "$14,300", "$52,000", "$22,700", "$18,900", "$25,600" };
        int[] days = { 6, 21, 9, 3, 33, 12, 17, 41, 5, 14, 27, 8 };
        int count = state == "stock_empty" ? 0 : state == "stock_full" ? 20 : 12;
        for (int i = 0; i < count; i++)
        {
            int d = days[i % days.Length];
            cards.Add(Car(names[i % names.Length], prices[i % prices.Length], Fx(W(t, "reallife_ui_dealer_days_chip", "{days} days"), "days", d.ToString()), d >= 30 ? UiRole.Danger : d >= 14 ? UiRole.Warn : UiRole.Notice));
        }
        var c = new UiCatalogueModel
        {
            Cards = cards.ToArray(), CardCount = cards.Count, Chosen = cards.Count > 0 ? 0 : -1,
            EmptyTitle = W(t, "reallife_dealer_empty_stock", "No car stands on the lots of the showrooms shown."), EmptyBody = W(t, "reallife_ui_dealer_empty_stock_body", "The cars you buy appear here with their pictures, their prices and their days on the lot."), EmptyIcon = UiIcon.Car
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
                W(t, "reallife_ui_dealer_l_condition", "Condition"), W(t, "reallife_ui_dealer_l_km", "Mileage"), W(t, "reallife_ui_dealer_l_source", "Came from"),
                W(t, "reallife_ui_dealer_l_rival", "Cheapest rival"), W(t, "reallife_ui_dealer_l_looked_today", "Looked at today"), W(t, "reallife_ui_dealer_l_looked_yesterday", "Looked at yesterday"),
                W(t, "reallife_ui_dealer_l_offer_waiting", "Offer waiting")
            };
            c.DetailValues = new[] { "$72,300", "$83,800", "$84,500", "+$12,200", "92%", "12,400 km", W(t, "reallife_dealer_kind_used", "A private owner") + ", " + Fx(W(t, "reallife_ui_dealer_days_on_lot", "{days} days on the lot"), "days", "6"),
                "Halden Motors, $86,900", "4", "7", "Marcus Bell, $82,000" };
            c.DetailInks = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.OkText };
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
        string[][] chips = { new[] { "reallife_ui_dealer_filter_all", "All {count}", count.ToString() }, new[] { "reallife_ui_dealer_filter_new", "New {count}", (count * 3 / 12).ToString() },
            new[] { "reallife_ui_dealer_filter_used", "Used {count}", (count - count * 3 / 12 - count * 2 / 12).ToString() }, new[] { "reallife_ui_dealer_filter_shop", "In the workshop {count}", (count * 2 / 12).ToString() } };
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
        m.HeadSub = Fx(W(t, "reallife_ui_dealer_on_lot", "On the lot, {cars} of {bays} bays"), "cars", count.ToString(), "bays", "20");
        m.FigLabels = new[] { W(t, "reallife_ui_dealer_stock_worth", "Stock worth") };
        m.FigValues = new[] { count == 0 ? "$0" : count == 20 ? "$668,500" : "$400,100" };
    }

    private static void DealerLots(UiWords t, EstateModel m, List<Func<bool>> runs, bool yard)
    {
        if (yard)
        {
            m.Shape = EstateShape.Detail;
            m.Back = W(t, "reallife_ui_dealer_back", "Back");
            m.DetailIcon = UiIcon.Store;
            m.DetailTitle = "Pioneer Motors";
            m.DetailSub = Fx(W(t, "reallife_ui_dealer_rep_n", "Reputation {rep}"), "rep", "340");
            m.DetailChip = W(t, "reallife_dealer_step_2", "Trusted");
            m.DetailChipTone = UiRole.Notice;
            m.Strip = new[]
            {
                Fig(W(t, "reallife_ui_dealer_fig_visitors_today", "Visitors today"), "31"), Fig(W(t, "reallife_ui_dealer_fig_looked_after", "Looked after"), "24"),
                Fig(W(t, "reallife_ui_dealer_fig_offers_today", "Offers today"), "5"), Fig(W(t, "reallife_ui_dealer_fig_sold_today", "Sold today"), "2")
            };
            m.Left = new[]
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
            m.Right = new[]
            {
                new UiFactCardModel
                {
                    Title = W(t, "reallife_ibox_sec_this_week", "This week"), Heading = true,
                    Lines = new[]
                    {
                        UiFactLine.Row(W(t, "reallife_ui_dealer_fig_cars_sold", "Cars sold"), "9"), UiFactLine.Row(W(t, "reallife_dealer_report_revenue", "Revenue"), "$318,400"),
                        UiFactLine.Row(W(t, "reallife_ui_dealer_fig_bought", "Bought"), "$244,100"), UiFactLine.Row(W(t, "reallife_ui_dealer_fig_margin", "Margin"), "$41,900", UiRole.OkText),
                        UiFactLine.Row(W(t, "reallife_ui_dealer_fig_claims", "Warranty claims"), "$1,240")
                    }
                }
            };
            m.ShareTitle = W(t, "reallife_ibox_sec_now", "Now");
            m.ShareLabels = new[] { W(t, "reallife_ibox_lot", "Places taken"), W(t, "reallife_ui_dealer_sh_looked_after", "Visitors looked after"), W(t, "reallife_ui_dealer_sh_next_step", "On the way to the next step") };
            m.ShareValues = new[] { 60, 77, 85 };
            m.Lower = DayLine(t, "reallife_dealer_report_revenue", "Takings from cars", 55000f, 14000f, 0f, EstateCharts.Dollars);
            m.RelatedTitle = W(t, "reallife_ui_dealer_sort_days", "Longest on the lot");
            m.RelatedCount = "3";
            string[] oldest = { "Marlow sedan", "Dune camper van", "Volta SUV" };
            string[] asks = { "$14,300", "$18,900", "$44,200" };
            string[] stood = { "41", "27", "33" };
            var longest = new EstateRow[oldest.Length];
            for (int i = 0; i < longest.Length; i++)
            {
                runs.Add(Yes);
                CarKind kind = SampleKind(oldest[i]);
                longest[i] = new EstateRow
                {
                    Icon = CarClass.Glyph(kind), Tint = CarClass.Tint(kind, 0), Title = oldest[i], Sub = Fx(W(t, "reallife_ui_dealer_days_on_lot", "{days} days on the lot"), "days", stood[i]),
                    Amount = asks[i], Run = runs.Count - 1
                };
            }
            m.Related = longest;
            return;
        }
        var rows = new List<UiDataRow>
        {
            Row(new[] { "Pioneer Motors", Fx(W(t, "reallife_dealer_lot_count", "{cars} cars on {places} places"), "cars", "12", "places", "20"), "3", "31", "2", "$55,000", "6" }, new[] { (int)UiIcon.Store, 0, 0, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.WarnText }, new[] { W(t, "reallife_dealer_step_2", "Trusted"), null, null, null, null, null, null }),
            Row(new[] { "Eastside Auto", Fx(W(t, "reallife_dealer_lot_count", "{cars} cars on {places} places"), "cars", "4", "places", "8"), "0", "9", "0", "$0", "0" }, new[] { (int)UiIcon.Store, 0, 0, 0, 0, 0, 0 },
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
        TableOf(m, UiIcon.Store, W(t, "reallife_dealer_tab_lots", "Dealerships"), Fx(W(t, "reallife_ui_dealer_lots_sub", "{count} showrooms"), "count", "2"), cols, rows, null, W(t, "reallife_dealer_row_no_dealership", "No dealership yet"),
            W(t, "reallife_dealer_lots_about", "Each showroom keeps its own lot, staff and figures."));
        m.FigLabels = new[] { W(t, "reallife_ui_dealer_fig_cars", "Cars"), W(t, "reallife_ui_dealer_fig_sold_today", "Sold today"), W(t, "reallife_ui_dealer_fig_takings_today", "Takings today"), W(t, "reallife_ui_dealer_fig_offers", "Offers waiting") };
        m.FigValues = new[] { "16", "2", "$55,000", "6" };
        m.Lower = DayLine(t, "reallife_dealer_report_revenue", "Takings from cars", 55000f, 14000f, 0f, EstateCharts.Dollars);
    }

    // A line over fourteen days that a page's foot shows: the day's own figures, as the showroom keeps them. Each figure has a shape of its own, and the last day is the day's figure on the page.

    // A line over fourteen days that a page's foot shows: the day's own figures, as the showroom keeps them.
    private static UiChartModel DayLine(UiWords t, string key, string english, float last, float swing, float climb, Func<float, string> format)
    {
        var days = new string[14];
        var values = new float[14];
        uint seed = 2166136261u;
        foreach (char ch in key)
        {
            seed = (seed ^ ch) * 16777619u;
        }
        float walk = 0f;
        for (int i = 0; i < days.Length; i++)
        {
            days[i] = W(t, "reallife_ui_dealer_chart_day", "Day {day}").Replace("{day}", (399 + i).ToString());
            seed = seed * 1664525u + 1013904223u;
            walk = walk * 0.6f + ((seed >> 16) % 1000 / 1000f - 0.5f) * swing;
            values[i] = Math.Max(0f, last * 0.9f + walk + (i - 13) * climb);
        }
        values[13] = last;
        bool whole = last < 100f;
        if (whole)
        {
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = (float)Math.Round(values[i]);
            }
        }
        return new UiChartModel
        {
            Kind = UiChartKind.Line, Title = W(t, key, english), Points = days, Still = true, Whole = whole, EvenAxis = true,
            Series = new[] { new UiSeries { Label = W(t, key, english), Values = values, Tint = UiRole.None } }, Format = format, AxisFormat = format
        };
    }

    // A vehicle colour in the language shown: the game gives its own names, the preview the locale's nearest.
    private static string Colour(UiWords t, string name)
    {
        string key = name == "Blue" ? "cobalt" : name == "Silver" ? "heather" : name == "Yellow" ? "mustard" : name == "Green" ? "forest" : name.ToLowerInvariant();
        return W(t, "reallife_colour_" + key, name);
    }

    private static string DayWord(UiWords t, int day) => Fx(W(t, "reallife_ui_estate_day", "Day {day}"), "day", day.ToString());

    private static void DealerMarket(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        var rows = new List<UiDataRow>();
        string[][] l =
        {
            new[] { "Volta SUV", "Red", "new", "4 km", "100", "$38,200", "today", "new" },
            new[] { "Marlow sedan", "Black", "used", "88,200 km", "61", "$11,900", "today", "ni" },
            new[] { "Courier van", "White", "auction", "31,800 km", "86", "$29,400", "2 h", "rival" },
            new[] { "Linden coupe", "Blue", "auction", "12,400 km", "92", "$21,800", "5 h", "lead" },
            new[] { "Atlas pickup", "Grey", "new", "6 km", "100", "$36,400", "today", "new" },
            new[] { "Halden sedan", "Silver", "used", "64,900 km", "72", "$18,600", "today", "ni" },
            new[] { "Brisa roadster", "Yellow", "used", "41,300 km", "80", "$20,900", "today", "ni" },
            new[] { "Pinecrest saloon", "Green", "auction", "52,700 km", "69", "$12,300", "7 h", "rival" },
            new[] { "Boxer box truck", "White", "auction", "97,100 km", "58", "$27,800", "9 h", "rival" },
            new[] { "Sora hatchback", "Red", "new", "3 km", "100", "$21,200", "today", "new" },
            new[] { "Marlow sedan", "Blue", "used", "112,400 km", "49", "$7,900", "today", "ni" },
            new[] { "Dune camper van", "Cream", "used", "76,500 km", "66", "$16,400", "today", "ni" },
            new[] { "Ranger pickup", "Green", "new", "5 km", "100", "$25,100", "today", "new" }
        };
        foreach (string[] r in l)
        {
            string kind = r[2] == "new" ? W(t, "reallife_dealer_kind_wholesale", "New") : r[2] == "used" ? W(t, "reallife_dealer_kind_used", "Used") : W(t, "reallife_dealer_kind_auction", "Auction");
            string chip = r[7] == "new" ? W(t, "reallife_dealer_state_new", "New") : r[7] == "ni" ? W(t, "reallife_dealer_state_not_inspected", "Not inspected") : r[7] == "rival" ? Fx(W(t, "reallife_dealer_state_rival", "{rival} leads"), "rival", "Halden Motors") : W(t, "reallife_dealer_state_leading", "You lead");
            string time = r[6] == "today" ? W(t, "reallife_ui_dealer_today", "Today") : Fx(W(t, "reallife_ui_dealer_n_hours_short", "{hours} h"), "hours", r[6].Split(' ')[0]);
            // The car's glyph by its class, as on every other tab: the kind (new, used, auction) is a column of its own.
            CarKind carKind = SampleKind(r[0]);
            int price = int.Parse(r[5].Replace("$", string.Empty).Replace(",", string.Empty));
            rows.Add(Row(new[] { r[0], kind, r[3], r[4], r[5], time, chip }, new[] { (int)CarClass.Glyph(carKind), 0, 0, int.Parse(r[4]), 0, 0, 0 },
                new[] { CarClass.Tint(carKind, price), UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, r[7] == "lead" ? UiRole.Ok : r[7] == "rival" ? UiRole.Warn : UiRole.Notice }, new[] { Colour(t, r[1]), null, null, null, null, null, null }));
        }
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_car", "Car"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_kind", "Kind"), Kind = UiCellKind.Text, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_km", "Kilometres"), Kind = UiCellKind.Figure, Width = 96f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_condition", "Condition"), Kind = UiCellKind.Meter, Width = 136f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_price", "Price"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_time", "Time"), Kind = UiCellKind.Text, Width = 96f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_state", "State"), Kind = UiCellKind.Chip, Width = 216f }
        };
        TableOf(m, UiIcon.Tag, W(t, "reallife_dealer_market_title", "The car market"), Fx(W(t, "reallife_ui_dealer_market_sub", "{new} new, {used} used, {lots} lots at auction"), "new", "4", "used", "5", "lots", "4"), cols, rows, null,
            W(t, "reallife_ui_dealer_empty_market", "Nothing is on the market today"), W(t, "reallife_dealer_market_about", "New cars come from the wholesaler."));
        m.FigLabels = new[] { W(t, "reallife_dealer_cash", "Cash"), W(t, "reallife_ui_dealer_fig_new", "New"), W(t, "reallife_ui_dealer_fig_used", "Used"), W(t, "reallife_ui_dealer_fig_lots", "At auction") };
        m.FigValues = new[] { "$48,210", "4", "5", "4" };
    }

    private static void DealerUsed(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        m.Shape = EstateShape.Detail;
        m.Back = W(t, "reallife_ui_dealer_back", "Back");
        m.DetailIcon = UiIcon.Car;
        m.DetailTitle = "Marlow sedan";
        m.DetailSub = Colour(t, "Black");
        m.DetailChip = W(t, "reallife_ibox_chip_not_inspected", "Not inspected");
        m.DetailChipTone = UiRole.Warn;
        m.Card = true;
        m.FootBack = W(t, "reallife_ui_estate_not_now", "Not now");
        m.Strip = new[]
        {
            Fig(W(t, "reallife_ibox_asking", "Asking"), "$11,900"), Fig(W(t, "reallife_ui_dealer_last_counter", "Their last counter"), W(t, "reallife_dealer_none", "None")),
            Fig(W(t, "reallife_ui_dealer_offers_left", "Offers left"), "3"), Fig(W(t, "reallife_ui_dealer_free_places", "Free places"), "8")
        };
        m.Left = new[]
        {
            new UiFactCardModel
            {
                Title = W(t, "reallife_ibox_sec_offer", "The offer"), Heading = true,
                Lines = new[] { EstateStep(runs, W(t, "reallife_ui_dealer_your_offer", "Your offer"), "$10,700", Fx(W(t, "reallife_dealer_goes_to", "Goes to {name}, {free} places free"), "name", "Pioneer Motors", "free", "8")) },
                Actions = new[]
                {
                    EstateButton(runs, W(t, "reallife_dealer_button_inspect", "Inspect it"), UiButtonKind.Secondary, UiIcon.Eye, UiAction.Count),
                    EstateButton(runs, W(t, "reallife_dealer_button_buy", "Buy"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option),
                    EstateButton(runs, W(t, "reallife_dealer_button_offer", "Make an offer"), UiButtonKind.Primary, UiIcon.Handshake, UiAction.Alt)
                }
            }
        };
        m.Right = new[]
        {
            new UiFactCardModel
            {
                Title = W(t, "reallife_ibox_sec_car", "The car"), Heading = true,
                Lines = new[]
                {
                    UiFactLine.Row(W(t, "reallife_ui_dealer_l_km", "Mileage"), "88,200 km"), UiFactLine.Row(W(t, "reallife_ui_dealer_l_source", "Came from"), W(t, "reallife_dealer_kind_used", "A private owner")),
                    UiFactLine.Say(W(t, "reallife_ui_dealer_faults_unknown", "Its faults are not known until it is inspected."))
                }
            }
        };
        m.ShareTitle = W(t, "reallife_ui_dealer_sh_said", "As the owner says");
        m.ShareLabels = new[] { W(t, "reallife_ui_dealer_l_condition", "Condition") };
        m.ShareValues = new[] { 61 };
        m.Related2Title = W(t, "reallife_ui_dealer_like_it", "Others like it on the market");
        m.Related2Count = "2";
        runs.Add(Yes);
        runs.Add(Yes);
        m.Related2 = new[]
        {
            new EstateRow { Icon = CarClass.Glyph(SampleKind("Marlow sedan")), Tint = CarClass.Tint(SampleKind("Marlow sedan"), 0), Title = "Marlow sedan", Sub = Colour(t, "Blue") + ", 112,400 km", Chip = W(t, "reallife_dealer_state_not_inspected", "Not inspected"), ChipTone = UiRole.Warn, Amount = "$7,900", Run = runs.Count - 2 },
            new EstateRow { Icon = CarClass.Glyph(SampleKind("Halden sedan")), Tint = CarClass.Tint(SampleKind("Halden sedan"), 0), Title = "Halden sedan", Sub = Colour(t, "Silver") + ", 64,900 km", Chip = W(t, "reallife_dealer_state_not_inspected", "Not inspected"), ChipTone = UiRole.Warn, Amount = "$18,600", Run = runs.Count - 1 }
        };
    }

    private static void DealerAuction(UiWords t, EstateModel m, List<Func<bool>> runs, bool nobid)
    {
        m.Shape = EstateShape.Auction;
        m.Card = true;
        m.FootBack = W(t, "reallife_ui_estate_not_now", "Not now");
        var a = new EstateAuction { Kicker = W(t, "reallife_ui_dealer_auction_kicker", "Live auction, the trade car auction"), Title = "Courier van", Icon = UiIcon.Truck, Aspect = 1.8f, Back = W(t, "reallife_ui_dealer_back", "Back") };
        a.Left = new UiFactCardModel
        {
            Lines = new[]
            {
                UiFactLine.Row(W(t, "reallife_ui_dealer_l_worth", "Market value"), "$33,800"), UiFactLine.Bar(W(t, "reallife_ui_dealer_l_condition", "Condition"), 86), UiFactLine.Row(W(t, "reallife_ui_dealer_l_km", "Mileage"), "31,800 km"),
                UiFactLine.Row(W(t, "reallife_ui_dealer_free_bays", "Your free bays"), "8"), UiFactLine.Section(W(t, "reallife_ui_dealer_if_you_win", "If you win")),
                UiFactLine.Row(W(t, "reallife_ui_dealer_your_bid", "Your bid"), "$30,000"), UiFactLine.Row(Fx(W(t, "reallife_ui_dealer_buyers_fee", "Auction fee, {share}"), "share", "5%"), "$1,500"),
                UiFactLine.Row(W(t, "reallife_ui_dealer_you_pay", "You pay"), "$31,500"), UiFactLine.Row(W(t, "reallife_ui_dealer_worth_lot", "Worth on your lot"), "$33,800"),
                UiFactLine.Row(W(t, "reallife_ui_dealer_left_for_you", "Left for you"), "+$2,300", UiRole.OkText)
            }
        };
        var lines = new List<UiFactLine>
        {
            UiFactLine.Row(W(t, "reallife_ui_dealer_current_bid", "Current bid"), nobid ? "$21,600" : "$29,400"),
            UiFactLine.Row(W(t, "reallife_ui_dealer_held_by", "Held by"), nobid ? W(t, "reallife_dealer_state_no_bid", "No bid yet") : "Halden Motors"),
            UiFactLine.Bar(W(t, "reallife_ui_dealer_time_left_label", "Time left"), 40, Fx(W(t, "reallife_ui_dealer_n_hours", "{hours} hours"), "hours", "2")),
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
        if (nobid)
        {
            lines.Add(UiFactLine.Section(W(t, "reallife_ui_dealer_bids_chart", "Bids so far")));   // as the live page says it
            lines.Add(UiFactLine.Say(W(t, "reallife_ui_dealer_no_bids", "Nobody has bid yet.")));
        }
        if (!nobid)
        {
            lines.Add(UiFactLine.Section(Fx(W(t, "reallife_ui_dealer_bidders", "Bidders, the last {count}"), "count", "4")));
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
            Title = Fx(W(t, "reallife_ui_dealer_bidding_on", "Bidding on the {car}"), "car", "Courier van"), Sub = Fx(W(t, "reallife_ui_dealer_colour_line", "{colour}"), "colour", Colour(t, "White")), Lines = lines.ToArray(),
            Actions = new[]
            {
                EstateButton(runs, W(t, "reallife_ui_dealer_walk_away", "Walk away"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option),
                EstateButton(runs, W(t, "reallife_dealer_button_inspect", "Inspect it"), UiButtonKind.Secondary, UiIcon.Eye, UiAction.Count),
                EstateButton(runs, Fx(W(t, "reallife_ui_dealer_bid_button", "Bid {amount}"), "amount", "$30,000"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt)
            }
        };
        m.RelatedTitle = W(t, "reallife_ui_dealer_other_lots", "Other lots at this auction");
        m.RelatedCount = "3";
        runs.Add(Yes);
        runs.Add(Yes);
        runs.Add(Yes);
        m.Related = new[]
        {
            new EstateRow { Icon = CarClass.Glyph(SampleKind("Linden coupe")), Tint = CarClass.Tint(SampleKind("Linden coupe"), 0), Title = "Linden coupe", Sub = Fx(W(t, "reallife_ui_dealer_n_hours_left", "{hours} hours left"), "hours", "5"), Chip = W(t, "reallife_dealer_state_leading", "You lead"), ChipTone = UiRole.Ok, Amount = "$21,800", Run = runs.Count - 3 },
            new EstateRow { Icon = CarClass.Glyph(SampleKind("Pinecrest saloon")), Tint = CarClass.Tint(SampleKind("Pinecrest saloon"), 0), Title = "Pinecrest saloon", Sub = Fx(W(t, "reallife_ui_dealer_n_hours_left", "{hours} hours left"), "hours", "7"), Chip = Fx(W(t, "reallife_dealer_state_rival", "{rival} leads"), "rival", "Halden Motors"), ChipTone = UiRole.Warn, Amount = "$12,300", Run = runs.Count - 2 },
            new EstateRow { Icon = CarClass.Glyph(SampleKind("Boxer box truck")), Tint = CarClass.Tint(SampleKind("Boxer box truck"), 0), Title = "Boxer box truck", Sub = Fx(W(t, "reallife_ui_dealer_n_hours_left", "{hours} hours left"), "hours", "9"), Chip = Fx(W(t, "reallife_dealer_state_rival", "{rival} leads"), "rival", "Crossway Cars"), ChipTone = UiRole.Warn, Amount = "$27,800", Run = runs.Count - 1 }
        };
        m.Auction = a;
    }

    // A longer list for a preview: the rows again for other people, so a table stands as full as a busy showroom's.
    // Rows beyond the hand-made few, each its own person with a car of its own: a table of nine must not repeat three.
    // The glyph and colour of the car of each row by the class its sample kind gives, and no face for a sale to the trade: the rule of the live pages.
    private static void TintCars(List<UiDataRow> rows, string trade)
    {
        foreach (UiDataRow row in rows)
        {
            if (row.Values[1] != (int)UiIcon.Car)
            {
                continue;
            }
            CarKind kind = SampleKind(row.Cells[1]);
            row.Values[1] = (int)CarClass.Glyph(kind);
            row.Tones[1] = CarClass.Tint(kind, 0);
            if (row.Cells[0] == trade)
            {
                row.Values[0] = UiDataTable.GlyphFace(UiIcon.Store);
            }
        }
    }

    // The cars of the waiting offers are cars of the lot (Stock's own, at its own asking prices); a car sold on a plan or with a warranty has left the lot, so the finance
    // rows draw on a pool of their own and a sold row's car is set by SoldOffers.
    private static readonly string[] LotCars = { "Halden sedan", "Sora hatchback", "Atlas pickup", "Courier van", "Dune camper van", "Ranger pickup", "Pinecrest saloon", "Boxer box truck" };
    private static readonly int[] LotAsks = { 27400, 19800, 38900, 34900, 18900, 25600, 16900, 52000 };
    private static readonly string[] PlanCars = { "Wren roadster", "Linden pickup", "Orion saloon", "Stoat van", "Pell hatchback", "Dale hatchback" };
    private static readonly int[] PlanAsks = { 22700, 25600, 16900, 34900, 14300, 18900 };

    private static void Pad(UiWords t, List<UiDataRow> rows, int to, string[] cars, int[] asks, params string[] names)
    {
        int[] pct = { 88, 91, 79, 94, 85, 72, 90, 83 };
        string[] left = { HoursShort(t, "6 h"), HoursShort(t, "2 h"), HoursShort(t, "9 h"), HoursShort(t, "3 h"), HoursShort(t, "5 h"), HoursShort(t, "1 h") };
        int first = rows.Count;
        for (int i = 0; rows.Count < to; i++)
        {
            UiDataRow from = rows[i % first];
            UiDataRow copy = from;
            copy.Cells = (string[])from.Cells.Clone();
            copy.Values = (int[])from.Values.Clone();
            copy.Tones = (UiRole[])from.Tones.Clone();   // a padded row has tones of its own: the class colour set on one must not repaint the rows it was copied from
            copy.Cells[0] = names[i % names.Length];
            copy.Hue = UiColourHue(copy.Cells[0]);
            copy.Cells[1] = cars[i % cars.Length];
            int ask = asks[i % asks.Length];
            if (from.Cells.Length == 7 && from.Values[4] > 0)
            {
                copy.Values[4] = pct[i % pct.Length];
                copy.Cells[4] = copy.Values[4].ToString();
                copy.Cells[2] = "$" + (ask * copy.Values[4] / 100).ToString("N0");
                copy.Cells[3] = "$" + ask.ToString("N0");
                copy.Cells[5] = left[i % left.Length];
            }
            else if (from.Cells.Length == 7 && from.Cells[4].Length == 0)
            {
                copy.Cells[2] = "$" + (ask + ask / 14).ToString("N0");
                copy.Cells[3] = "$" + ask.ToString("N0");
                copy.Cells[5] = DayWord(t, 385 + i * 3);
                copy.Cells[6] = copy.Cells[6].Replace("$7,100", "$" + (ask / 14).ToString("N0"));
            }
            else
            {
                copy.Cells[2] = "$" + (ask / 2 + i * 700).ToString("N0");
                copy.Cells[3] = "$" + ((ask / 24 + i * 37) / 10 * 10).ToString("N0");   // a week's payment of its own for each plan
            }
            rows.Add(copy);
        }
    }

    // The three sales kept, each of a car that has left the lot: two today (their margins make the figure of the page) and one older. A padded copy of the sold row gets its own car.
    private static void SoldOffers(UiWords t, List<UiDataRow> rows)
    {
        string[][] sold =
        {
            new[] { "Kestrel SUV", "$36,100", "$41,800", "412", "$7,100" }, new[] { "Orion hatchback", "$18,900", "$19,600", "412", "$1,900" }, new[] { "Tamsin sedan", "$15,300", "$15,900", "400", "$1,200" }
        };
        int k = 0;
        foreach (UiDataRow row in rows)
        {
            if (row.Cells.Length == 7 && row.Cells[4].Length == 0 && k < sold.Length)
            {
                row.Cells[1] = sold[k][0];
                row.Cells[2] = sold[k][1];
                row.Cells[3] = sold[k][2];
                row.Cells[5] = DayWord(t, int.Parse(sold[k][3]));
                row.Cells[6] = Fx(W(t, "reallife_dealer_state_sold", "Sold, margin {margin}"), "margin", sold[k][4]);
                k++;
            }
        }
    }

    private static void DealerOffers(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        var rows = new List<UiDataRow>
        {
            Row(new[] { "Marcus Bell", "Meridian GT coupe", "$82,000", "$84,500", "97", HoursShort(t, "4 h"), W(t, "reallife_dealer_state_waiting", "Waiting") }, new[] { 0, (int)UiIcon.Car, 0, 0, 97, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Warn }, new[] { W(t, "reallife_dealer_profile_bargain", "A bargain hunter"), "Pioneer Motors", null, null, null, null, null }, UiColourHue("Marcus Bell")),
            Row(new[] { "Amara Kimani", "Atlas pickup", "$36,900", "$38,900", "95", HoursShort(t, "1 h"), W(t, "reallife_dealer_state_waiting", "Waiting") }, new[] { 0, (int)UiIcon.Car, 0, 0, 95, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.DangerText, UiRole.Warn }, new[] { W(t, "reallife_dealer_profile_fair", "A family"), "Pioneer Motors", null, null, null, null, null }, UiColourHue("Amara Kimani")),
            Row(new[] { W(t, "reallife_dealer_to_trade", "The trade"), "Kestrel SUV", "$36,100", "$41,800", string.Empty, DayWord(t, 412), Fx(W(t, "reallife_dealer_state_sold", "Sold, margin {margin}"), "margin", "$7,100") }, new[] { 0, (int)UiIcon.Car, 0, 0, -1, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Ok }, new[] { "Pioneer Motors", string.Empty, null, null, null, null, null })
        };
        Pad(t, rows, 9, LotCars, LotAsks, "Priya Nair", "Theo Brandt", "Lena Hoffmann", "Imani Cole", "Owen Park", "Yuki Tanaka");
        SoldOffers(t, rows);
        TintCars(rows, W(t, "reallife_dealer_to_trade", "The trade"));
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_buyer", "Buyer"), Kind = UiCellKind.Person, Width = 288f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_car", "Car"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_offer", "Offer"), Kind = UiCellKind.Money, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_asking", "Asking"), Kind = UiCellKind.Money, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_against", "Against asking"), Kind = UiCellKind.Meter, Width = 120f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_left", "Time left"), Kind = UiCellKind.Text, Width = 80f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_state", "State"), Kind = UiCellKind.Chip, Width = 200f }
        };
        TableOf(m, UiIcon.Handshake, W(t, "reallife_dealer_offers_title", "Offers and sales"), Fx(W(t, "reallife_ui_dealer_offers_sub", "{waiting} offers waiting, {sold} sales kept"), "waiting", "6", "sold", "3"), cols, rows, null,
            W(t, "reallife_ui_dealer_empty_offers", "No offers or sales yet"), W(t, "reallife_dealer_offers_about", "Buyers make an offer and wait for your answer."));
        m.FigLabels = new[] { W(t, "reallife_dealer_sum_waiting", "Waiting for you"), W(t, "reallife_ui_dealer_fig_best_offer", "Best offer"), W(t, "reallife_ui_dealer_fig_sold_today", "Sold today"), W(t, "reallife_ui_dealer_fig_margin_today", "Margin today") };
        m.FigValues = new[] { "6", "$82,000", "2", "$9,000" };
        m.FigInks = new[] { UiRole.WarnText, UiRole.None, UiRole.None, UiRole.OkText };
        m.RelatedTitle = W(t, "reallife_ui_dealer_rivals_ask", "What the other dealers ask for the same cars");
        m.RelatedCount = "6";
        m.RelatedRows = 6;
        m.Related = new[]
        {
            new EstateRow { Icon = CarClass.Glyph(SampleKind("Meridian GT coupe")), Tint = CarClass.Tint(SampleKind("Meridian GT coupe"), 0), Title = "Meridian GT coupe", Sub = "Halden Motors", Chip = Fx(W(t, "reallife_ui_dealer_over_you", "{amount} over yours"), "amount", "$2,400"), ChipTone = UiRole.Ok, Amount = "$86,900", Run = -1 },
            new EstateRow { Icon = CarClass.Glyph(SampleKind("Atlas pickup")), Tint = CarClass.Tint(SampleKind("Atlas pickup"), 0), Title = "Atlas pickup", Sub = "Crossway Cars", Chip = Fx(W(t, "reallife_ui_dealer_under_you", "{amount} under yours"), "amount", "$1,100"), ChipTone = UiRole.Warn, Amount = "$37,800", Run = -1 },
            new EstateRow { Icon = CarClass.Glyph(SampleKind("Sora hatchback")), Tint = CarClass.Tint(SampleKind("Sora hatchback"), 0), Title = "Sora hatchback", Sub = "Pier Street Autos", Chip = Fx(W(t, "reallife_ui_dealer_over_you", "{amount} over yours"), "amount", "$600"), ChipTone = UiRole.Ok, Amount = "$20,400", Run = -1 },
            new EstateRow { Icon = CarClass.Glyph(SampleKind("Courier van")), Tint = CarClass.Tint(SampleKind("Courier van"), 0), Title = "Courier van", Sub = "Halden Motors", Chip = Fx(W(t, "reallife_ui_dealer_over_you", "{amount} over yours"), "amount", "$1,000"), ChipTone = UiRole.Ok, Amount = "$35,900", Run = -1 },
            new EstateRow { Icon = CarClass.Glyph(SampleKind("Dune camper van")), Tint = CarClass.Tint(SampleKind("Dune camper van"), 0), Title = "Dune camper van", Sub = "Crossway Cars", Chip = Fx(W(t, "reallife_ui_dealer_under_you", "{amount} under yours"), "amount", "$700"), ChipTone = UiRole.Warn, Amount = "$18,200", Run = -1 },
            new EstateRow { Icon = CarClass.Glyph(SampleKind("Ranger pickup")), Tint = CarClass.Tint(SampleKind("Ranger pickup"), 0), Title = "Ranger pickup", Sub = "Pier Street Autos", Chip = Fx(W(t, "reallife_ui_dealer_over_you", "{amount} over yours"), "amount", "$800"), ChipTone = UiRole.Ok, Amount = "$26,400", Run = -1 }
        };
    }

    private static void DealerDeal(UiWords t, EstateModel m, List<Func<bool>> runs)
    {
        DealerDecide(t, m, runs);   // an offer is a decision: UiSamples.Estate.Decide.cs
    }

    private static void DealerFinance(UiWords t, EstateModel m)
    {
        var rows = new List<UiDataRow>
        {
            Row(new[] { "Daniel Ortiz", "Alder sedan", "$18,200", "$900", DayWord(t, 418), Fx(W(t, "reallife_dealer_plan_paying", "Paying, rated {grade}"), "grade", "B") }, new[] { 0, (int)UiIcon.Car, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Ok }, new[] { "Pioneer Motors", string.Empty, null, null, null, null }, UiColourHue("Daniel Ortiz")),
            Row(new[] { "Lena Hoffmann", "Quill hatchback", "$7,300", "$420", DayWord(t, 411), Fx(W(t, "reallife_dealer_plan_missed", "{weeks} weeks missed"), "weeks", "2") }, new[] { 0, (int)UiIcon.Car, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Danger }, new[] { "Pioneer Motors", string.Empty, null, null, null, null }, UiColourHue("Lena Hoffmann")),
            Row(new[] { "Theo Brandt", "Fenn SUV", "$1,240", string.Empty, DayWord(t, 490), W(t, "reallife_dealer_tier_extended", "Silver") }, new[] { 0, (int)UiIcon.ShieldCheck, 0, 0, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.Warn, UiRole.None, UiRole.None, UiRole.Notice }, new[] { "Pioneer Motors", Fx(W(t, "reallife_ui_dealer_claims", "{count} claims"), "count", "1"), null, null, null, null }, UiColourHue("Theo Brandt"))
        };
        Pad(t, rows, 9, PlanCars, PlanAsks, "Sofia Marchetti", "Marcus Bell", "Owen Park", "Imani Cole", "Priya Nair", "Yuki Tanaka");
        TintCars(rows, W(t, "reallife_dealer_to_trade", "The trade"));
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_customer", "Customer"), Kind = UiCellKind.Person, Width = 288f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_car", "Car"), Kind = UiCellKind.Tile, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_owed_paid", "Owed or paid out"), Kind = UiCellKind.Money, Width = 112f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_weekly", "A week"), Kind = UiCellKind.Money, Width = 104f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_until", "Until"), Kind = UiCellKind.Date, Width = 96f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_state", "State"), Kind = UiCellKind.Chip, Width = 200f }
        };
        TableOf(m, UiIcon.Receipt, W(t, "reallife_dealer_finance_title", "Finance and warranties"), Fx(W(t, "reallife_ui_dealer_finance_sub", "{plans} plan(s), {covers} warranty(ies)"), "plans", "6", "covers", "3"), cols, rows, null,
            W(t, "reallife_ui_dealer_empty_finance", "No finance plans or warranties yet"), W(t, "reallife_dealer_finance_about", "Your own finance and warranties are sold with the cars."));
        m.FigLabels = new[] { W(t, "reallife_ui_dealer_fig_owed", "Owed to you"), W(t, "reallife_ui_dealer_fig_a_week", "Paid in a week"), W(t, "reallife_ui_dealer_fig_behind", "Plans behind"), W(t, "reallife_ui_dealer_fig_paid_out", "Warranties paid out") };
        m.FigValues = new[] { "$25,500", "$1,320", "1", "$1,240" };
        m.FigInks = new[] { UiRole.None, UiRole.None, UiRole.DangerText, UiRole.None };
        m.SharePlain = true;   // the live page: a mix of buyers is no grade
        m.ShareTitle = W(t, "reallife_ui_dealer_sh_grades", "How your buyers are rated");
        m.ShareLabels = new[] { Fx(W(t, "reallife_ui_dealer_grade_n", "Rated {grade}"), "grade", "A"), Fx(W(t, "reallife_ui_dealer_grade_n", "Rated {grade}"), "grade", "B"), Fx(W(t, "reallife_ui_dealer_grade_n", "Rated {grade}"), "grade", "C") };
        m.ShareValues = new[] { 33, 50, 17 };
        m.RelatedTitle = W(t, "reallife_ui_dealer_warranties_end", "Warranties ending soonest");
        // The warranties are the table's own rows (the ones with a shield), so a car named here is a car named there.
        var warranties = new List<EstateRow>();
        foreach (UiDataRow row in rows)
        {
            if (row.Values[1] == (int)UiIcon.ShieldCheck)
            {
                warranties.Add(new EstateRow { Icon = UiIcon.ShieldCheck, Title = row.Cells[1], Sub = row.Cells[0], Chip = row.Cells[4], ChipTone = UiRole.Notice, Amount = row.Cells[2], Run = -1 });
            }
        }
        m.RelatedCount = warranties.Count.ToString();
        m.Related = warranties.ToArray();
    }

    private static void DealerGrowth(UiWords t, EstateModel m, List<Func<bool>> runs, bool eventPage)
    {
        if (eventPage)
        {
            m.Shape = EstateShape.Detail;
            m.Back = W(t, "reallife_ui_dealer_back", "Back");
            m.DetailIcon = UiIcon.Megaphone;
            m.DetailTitle = W(t, "reallife_dealer_event_0", "A sales weekend");
            m.DetailSub = W(t, "reallife_dealer_event_0_about", "Buyers come from further away for a few days.");
            m.Strip = new[]
            {
                Fig(W(t, "reallife_ibox_cost", "Cost"), "$4,500"), Fig(W(t, "reallife_ui_dealer_fig_runs", "It runs"), Fx(W(t, "reallife_dealer_days", "{days} days"), "days", "3")),
                Fig(W(t, "reallife_ui_dealer_fig_rests", "The showroom rests"), Fx(W(t, "reallife_dealer_days", "{days} days"), "days", "14"))
            };
            m.Left = new[]
            {
                new UiFactCardModel
                {
                    Lines = new[] { EstateStep(runs, W(t, "reallife_ui_dealer_sec_showroom", "Showroom"), "Pioneer Motors", null) },
                    Actions = new[] { EstateButton(runs, W(t, "reallife_dealer_button_event", "Book the event"), UiButtonKind.Primary, UiIcon.Megaphone, UiAction.Alt) }
                }
            };
            m.Lower = DayLine(t, "reallife_dealer_report_visitors", "Visitors", 35f, 9f, 0.4f, v => ((int)Math.Round(v)).ToString());
            return;
        }
        var rows = new List<UiDataRow>
        {
            Row(new[] { W(t, "reallife_dealer_event_0", "Open weekend"), string.Empty, string.Empty, W(t, "reallife_dealer_event_0_short", "More visitors, and more of them make an offer"), W(t, "reallife_dealer_state_event_ready", "Ready to book") },
                new[] { (int)UiIcon.Megaphone, 0, -1, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Notice }, new[] { W(t, "reallife_ui_dealer_growth_event", "Event") + ", " + Fx(W(t, "reallife_dealer_days", "{days} days"), "days", "3") + ", $4,500", null, null, null, null }),
            Row(new[] { W(t, "reallife_dealer_mission_0", "Sell ten cars"), "Pioneer Motors", "70", W(t, "reallife_dealer_mission_0_about", "Sell ten cars to buyers.") + " 7 of 10.", W(t, "reallife_dealer_state_mission_open", "Under way") },
                new[] { (int)UiIcon.Trophy, 0, 70, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Notice }, new[] { Fx(W(t, "reallife_ui_dealer_growth_mission", "Mission, {points} points"), "points", "30"), null, null, null, null }),
            Row(new[] { W(t, "reallife_dealer_mission_0", "Sell ten cars"), "Eastside Auto", "20", W(t, "reallife_dealer_mission_0_about", "Sell ten cars to buyers.") + " 2 of 10.", W(t, "reallife_dealer_state_mission_open", "Under way") },
                new[] { (int)UiIcon.Trophy, 0, 20, 0, 0 }, new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Notice }, new[] { Fx(W(t, "reallife_ui_dealer_growth_mission", "Mission, {points} points"), "points", "30"), null, null, null, null }),
            Row(new[] { W(t, "reallife_dealer_step_2", "Trusted"), "Pioneer Motors", "85", W(t, "reallife_dealer_perk_2", "Cheaper wholesale") + ", 340 / 400", string.Empty }, new[] { (int)UiIcon.Star, 0, 85, 0, 0 },
                new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None }, new[] { W(t, "reallife_ui_dealer_growth_rep", "Reputation"), null, null, null, null })
        };
        var cols = new[]
        {
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_what", "What"), Kind = UiCellKind.Tile, Width = 216f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_showroom", "Showroom"), Kind = UiCellKind.Text, Width = 176f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_progress", "Progress"), Kind = UiCellKind.Meter, Width = 176f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_about", "What it does"), Kind = UiCellKind.Text, Width = 0f },
            new UiDataColumn { Title = W(t, "reallife_ui_dealer_col_state", "State"), Kind = UiCellKind.Chip, Width = 152f }
        };
        TableOf(m, UiIcon.Rocket, W(t, "reallife_dealer_growth_title", "Growth"), W(t, "reallife_ui_dealer_growth_sub", "Events to book, missions and reputation"), cols, rows, null, W(t, "reallife_dealer_row_no_dealership", "No dealership yet"), W(t, "reallife_dealer_growth_events", "Events bring buyers."));
        m.FigLabels = new[] { W(t, "reallife_ui_dealer_fig_events_on", "Events on"), W(t, "reallife_ui_dealer_fig_missions_done", "Missions done"), W(t, "reallife_ui_dealer_fig_sold_today", "Sold today") };
        m.FigValues = new[] { "0", "0 / 3", "2" };
        m.Lower = DayLine(t, "reallife_dealer_report_sold", "Cars sold", 2f, 3f, 0.05f, v => ((int)Math.Round(v)).ToString());
    }

    private static void DealerReports(UiWords t, EstateModel m, List<Func<bool>> runs, bool fresh)
    {
        m.Shape = EstateShape.Chart;
        m.HeadIcon = UiIcon.Chart;
        m.HeadTitle = W(t, "reallife_dealer_report_profit", "Profit");
        m.HeadSub = Fx(W(t, "reallife_ui_dealer_report_sub", "{place}: a point a day for the last {days} days, with the average of the last {average} days. Point at a day to read it."), "place", W(t, "reallife_dealer_report_all", "All showrooms"), "days", "60", "average", "7");
        int days = fresh ? 1 : 60;
        var points = new string[days];
        var line = new float[days];
        var avg = new float[days];
        for (int i = 0; i < days; i++)
        {
            points[i] = Fx(W(t, "reallife_ui_dealer_chart_day", "Day {day}"), "day", (353 + i).ToString());
            line[i] = 4200f + (float)Math.Sin(i / 5.0) * 1500f + ((i * 37 % 7) - 3) * 120f + i * 45f;
            float sum = 0f;
            int n = 0;
            for (int k = Math.Max(0, i - 6); k <= i; k++)
            {
                sum += 4200f + (float)Math.Sin(k / 5.0) * 1500f + ((k * 37 % 7) - 3) * 120f + k * 45f;
                n++;
            }
            avg[i] = sum / n;
        }
        m.Chart = new UiChartModel
        {
            Kind = UiChartKind.Line, Title = W(t, "reallife_dealer_report_profit", "Profit"), EvenAxis = true, Points = fresh ? new string[0] : points,
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
        m.ChartKindsTitle = W(t, "reallife_ui_dealer_plot_what", "What to plot");
        m.ChartPlacesTitle = W(t, "reallife_ui_dealer_plot_where", "Which showroom");
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
            Title = W(t, "reallife_ibox_sec_this_week", "This week"), Heading = true,
            Lines = new[]
            {
                UiFactLine.Row(W(t, "reallife_ui_dealer_fig_cars_sold", "Cars sold"), "11"), UiFactLine.Row(W(t, "reallife_dealer_report_revenue", "Revenue"), "$401,800"),
                UiFactLine.Row(W(t, "reallife_ui_dealer_fig_margin", "Margin"), "$53,200", UiRole.OkText), UiFactLine.Row(W(t, "reallife_ui_dealer_fig_extras", "Extras"), "$4,900"),
                UiFactLine.Row(W(t, "reallife_ui_dealer_fig_spent", "Events and the workshop"), "$9,000"), UiFactLine.Row(W(t, "reallife_dealer_report_visitors", "Visitors"), "188"),
                UiFactLine.Row(W(t, "reallife_dealer_report_conversion", "Came to buy"), "5.9%"),
                UiFactLine.Section(W(t, "reallife_ibox_sec_now", "Now")),
                UiFactLine.Row(W(t, "reallife_ui_dealer_fig_cars_lot", "Cars on the lots"), "12"), UiFactLine.Row(W(t, "reallife_ui_dealer_fig_age", "Days on the lot, on average"), "14"),
                UiFactLine.Row(W(t, "reallife_ui_dealer_fig_arrears", "In arrears"), "$7,300", UiRole.DangerText), UiFactLine.Row(W(t, "reallife_ui_dealer_fig_late", "Plans behind"), "1", UiRole.DangerText)
            }
        };
    }
}
