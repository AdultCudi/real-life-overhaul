using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    public static WorkScreen MarketScreenOf(UiWords t, string state)
    {
        UtilitiesWords w = UtilitiesWordsOf(t);
        UiNavEntry[] nav = MarketNav(w, state);
        var screen = new WorkScreen(new SampleWork("marketplace", w.T("reallife_hustle_app", "Marketplace"), UiIcon.Store, true, nav, t, v => WorkTabPages.Build(MarketAppOf(w, state, v), v, w)));
        screen.View.Tab = WkTabOf("marketplace", state);
        if (state == "listings_machines")
        {
            screen.View.Filter = 2;
        }
        else if (state == "listings_stock")
        {
            screen.View.Filter = 4;
        }
        else if (state == "listings")
        {
            screen.View.Row = 1;
        }
        else if (state == "haggle")
        {
            screen.View.Sub = 1;
        }
        return screen;
    }

    private static UiNavEntry[] MarketNav(UtilitiesWords w, string state) => new[] { WkNav(UiIcon.Tag, w.T("reallife_hustle_tab_listings", "Listings"), state == "listings_none" ? null : "15"),
        WkNav(UiIcon.Package, w.T("reallife_hustle_tab_yours", "Yours"), "6"), WkNav(UiIcon.Arrows, w.T("reallife_hustle_tab_offers", "Offers"), "2"), WkNav(UiIcon.Wrench, w.T("reallife_hustle_tab_freelance", "Freelance"), "4") };

    private static WorkApp MarketAppOf(UtilitiesWords w, string state, WorkView v)
    {
        var app = new WorkApp { Ready = true, Nav = MarketNav(w, state) };
        app.Tabs = new[] { v.Sub == 1 ? MarketHaggle(w, v) : MarketListings(w, state), MarketYours(w, v), MarketOffers(w), MarketFreelance(w) };
        return app;
    }

    private static WorkTab MarketListings(UtilitiesWords w, string state)
    {
        var t = new WorkTab { Shape = WorkShape.Cards, Search = w.T("reallife_work_search_listings", "Search listings"), AllWord = w.T("reallife_work_filter_all_word", "All"),
            Chips = new[] { w.T("reallife_work_cat_vehicles_word", "Vehicles"), w.T("reallife_work_cat_machines_word", "Machines"), w.T("reallife_work_cat_furniture_word", "Furniture"), w.T("reallife_work_cat_stock_word", "Stock") },
            EmptyTitle = w.T("reallife_work_no_listings_title", "Nothing is listed today"), EmptyBody = w.T("reallife_work_no_listings", "New things arrive each morning. Check Yours for what you can list."), SortWord = w.T("reallife_work_sort_newest", "Newest first") };
        if (state == "listings_none")
        {
            return t;
        }
        // name, kind glyph, price, against worth, seller, group, tag
        object[][] list =
        {
            new object[] { "Blue sport coupe", UiIcon.Car, "$38,500", -4, "Marco Bellini", 0, null }, new object[] { "Red delivery van", UiIcon.Package, "$14,200", -12, "Huang Guo", 0, "Rival" },
            new object[] { "Box truck", UiIcon.Truck, "$27,900", -6, "Harbourside Freight HSF", 0, null }, new object[] { "Tan minibus", UiIcon.Train, "$19,400", 3, "Amara Nwosu", 0, null },
            new object[] { "k:espresso_machine", UiIcon.Coffee, "$1,950", -9, "Sofia Marin", 1, null }, new object[] { "k:commercial_grill", UiIcon.Fire, "$2,300", -3, "Hana Okada", 1, null },
            new object[] { "k:display_fridge", UiIcon.Thermometer, "$1,150", -15, "Leo Park", 1, "Discount" }, new object[] { "k:dough_mixer", UiIcon.Factory, "$4,600", 5, "Colm Doyle", 1, null },
            new object[] { "k:chest_freezer", UiIcon.CloudSnow, "$1,380", -5, "Ada Voss", 1, null }, new object[] { "k:armchair", UiIcon.Home, "$320", -8, "Nia Brooks", 2, null },
            new object[] { "k:sofa", UiIcon.Sofa, "$890", 2, "Tomas Reyes", 2, null }, new object[] { "k:table", UiIcon.ForkKnife, "$740", -11, "Priya Shah", 2, null },
            new object[] { "k:coffee_pallet", UiIcon.Coffee, "$1,530", -15, "Colm Doyle", 3, null }, new object[] { "k:flour_pallet", UiIcon.ShoppingBag, "$1,140", -12, "Huang Guo", 3, "Rival" },
            new object[] { "k:milk_lot", UiIcon.Drop, "$690", -9, "Leo Park", 3, null }
        };
        for (int i = 0; i < list.Length; i++)
        {
            object[] p = list[i];
            string seller = (string)p[4], itemName = ((string)p[0]).StartsWith("k:") ? Sx(w, "reallife_hustle_thing_" + ((string)p[0]).Substring(2)) : (string)p[0];
            var r = new WorkRow { Id = i + 1, Icon = (UiIcon)p[1], Name = itemName, Price = (string)p[2], Under = WorkBuilder.Percent((int)p[3]), UnderValue = (int)p[3], Seller = seller, Group = (int)p[5],
                Status = (string)p[6] == null ? null : (string)p[6] == "Rival" ? w.T("reallife_work_tag_rival", "Rival") : w.T("reallife_work_tag_discount_short", "Discount"), Tone = UiRole.Muted };
            bool car = (int)p[5] == 0;
            r.Title = r.Name;
            r.Sub = (car ? w.T("reallife_work_kind_vehicle", "Vehicle") : (int)p[5] == 1 ? w.T("reallife_work_kind_machine", "Machine") : (int)p[5] == 2 ? w.T("reallife_work_kind_furniture", "Furniture") : w.T("reallife_work_kind_stock", "Stock lot")) + ", " + Cond(w, 82);
            int worthRaw = (int)((int.Parse(((string)p[2]).Replace("$", string.Empty).Replace(",", string.Empty)) * 100.0) / (100 + (int)p[3]));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_asking", "Asking"), (string)p[2]));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_market_price", "Market price"), Mny(worthRaw)));
            var sec = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_the_seller", "The seller") };
            sec.People.Add(new WorkPerson { Name = seller, Role = i == 1 || i == 13 ? w.F("reallife_work_rival_of", "{firm}, a rival", "firm", "Guo Logistics") : w.F("reallife_work_sold_n", "{n} sold", "n", (3 + i % 3).ToString()), Right = (3.6f + (i * 7 % 12) / 10f).ToString("0.0"), Halves = SHalves(36 + i * 7 % 12), Ring = UiRole.None });
            r.Sections.Add(sec);
            var facts = new WorkSection { Part = WorkPart.Pairs, Title = car ? w.T("reallife_work_the_vehicle", "The vehicle") : w.T("reallife_work_the_thing", "The thing") };
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_condition", "Condition as listed"), Cond(w, 82)));
            if (car)
            {
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_cargo", "Cargo"), "14"));
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_fuel_tank", "Fuel tank"), "90 l"));
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_top_speed", "Top speed"), "120 km/h"));
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_at", "At"), "Hell's Kitchen"));
            }
            else
            {
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_price_new", "Price new"), Mny(worthRaw * 14 / 10)));
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_at", "At"), "Midtown"));
            }
            r.Sections.Add(facts);
            if (i == 1)
            {
                var more = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_more_from", "More from this seller"), Note = w.F("reallife_work_n_listings", "{n} listings", "n", "2") };
                more.People.Add(new WorkPerson { Name = Sx(w, "reallife_hustle_thing_flour_pallet"), Role = w.T("reallife_work_kind_stock", "Stock lot"), Right = "$1,140", Icon = UiIcon.ShoppingBag });
                r.Sections.Add(more);
            }
            var before = new WorkSection { Part = WorkPart.Checks, Title = w.T("reallife_work_before_buy", "Before you buy") };
            before.Checks.Add(new WorkNeed { Words = w.F("reallife_work_you_have_money", "You have the {price}", "price", (string)p[2]), Met = true });
            before.Checks.Add(new WorkNeed { Words = w.T("reallife_work_room_to_keep", "Room for it among your things"), Met = true });
            if (!car)
            {
                before.Checks.Add(new WorkNeed { Words = w.F("reallife_work_delivery_adds_line", "Delivery adds {fee} and a night", "fee", "$120"), Met = false });
            }
            r.Sections.Add(before);
            r.Acts.Add(WkAct(100, w.F("reallife_work_buy_now", "Buy now, {price}", "price", (string)p[2]), UiButtonKind.Primary, UiIcon.Check, null, false, r.Id));
            r.Acts.Add(WkAct(101, w.T("reallife_work_haggle", "Haggle"), UiButtonKind.Secondary, UiIcon.Arrows, null, true, r.Id));
            r.Acts.Add(WkAct(102, w.T("reallife_hustle_button_way", "Show the way"), UiButtonKind.Secondary, UiIcon.Locate, null, true, r.Id));
            t.Rows.Add(r);
        }
        return t;
    }

    private static WorkTab MarketHaggle(UtilitiesWords w, WorkView v)
    {
        var t = new WorkTab { Shape = WorkShape.Haggle, Sub = true };
        int offer = v.Get(0, 12800);
        t.Amounts.Add(new WorkAmount { Id = 0, Value = offer, Min = 5000, Max = 14200, Step = 100, Figure = "$" + offer.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) });
        var r = new WorkRow { Id = 2, Name = "Red delivery van", Kicker = w.T("reallife_work_haggle_kicker", "Haggle with the seller"), Seller = "Huang Guo", Line = "Guo Logistics, " + w.F("reallife_work_rated", "rated {rating}", "rating", "4.3"), Status = w.T("reallife_work_tag_discount", "Sells at a discount"), Tone = UiRole.Muted,
            Sentence = w.F("reallife_work_haggle_line", "{seller} asks {asking} for the {thing}, {versus} against its market price. You may make up to {n} offers, and each one they turn down makes the next harder.", "seller", "Huang Guo", "asking", "$14,200", "thing", "Red delivery van", "versus", "-12%", "n", "3"), Hint = w.T("reallife_work_your_offer", "Your offer") };
        r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_their_price", "Their price"), "$14,200"));
        r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_market_price", "Market price"), Mny(16136)));
        r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_offers_left", "Offers left"), w.F("reallife_work_n_of_m", "{n} of {m}", "n", "3", "m", "3")));
        r.Acts.Add(new WorkAct { Kind = 103, Arg = 2, Label = w.F("reallife_work_make_offer", "Offer {amount}", "amount", "$" + offer.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)), Style = UiButtonKind.Primary, Icon = UiIcon.Check,
            Note = w.T("reallife_work_offer_note", "They may accept, counter or turn it down."), Choice = true });
        r.Acts.Add(new WorkAct { Kind = 100, Arg = 2, Label = w.T("reallife_work_pay_asking", "Pay the asking price"), Style = UiButtonKind.Secondary, Icon = UiIcon.Coin, Note = w.F("reallife_work_pay_asking_note", "Buy now at {price}, no more haggling.", "price", "$14,200"), Choice = true });
        r.Acts.Add(new WorkAct { Kind = WorkKinds.Back, Arg = 2, Label = w.T("reallife_work_walk_away_haggle", "Walk away"), Style = UiButtonKind.Destructive, Icon = UiIcon.Back, Opens = true,
            Note = w.F("reallife_work_walk_note", "Back to the listings. The {thing} stays for sale today.", "thing", "Red delivery van"), Choice = true });
        var known = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_the_seller", "The seller") };
        known.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_rating", "Rating"), w.F("reallife_work_rating_of", "{n} of 5", "n", "4.3")));
        known.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_sold_word", "Sold"), "5"));
        known.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_listed_now", "Listed now"), "2"));
        r.Sections.Add(known);
        var facts = new WorkFormBand { Title = w.T("reallife_work_the_vehicle", "The vehicle") };
        facts.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_condition", "Condition as listed"), Value = Cond(w, 82) });
        facts.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_km", "Driven"), Value = "61,400 km" });
        facts.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_cargo", "Cargo"), Value = "14" });
        facts.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_fuel_tank", "Fuel tank"), Value = "90 l" });
        r.Bands.Add(facts);
        r.Bands.Add(WorkBands.Tiles(w.T("reallife_work_more_from", "More from this seller"), new[] { UiIcon.ShoppingBag }, new[] { Sx(w, "reallife_hustle_thing_flour_pallet") },
            new[] { w.T("reallife_work_kind_stock", "Stock lot") }, new[] { "$1,140" }));
        var before = new WorkFormBand { Title = w.T("reallife_work_before_buy", "Before you buy") };
        before.Rows.Add(new WorkFormRow { Field = WorkField.Check, Label = w.F("reallife_work_you_have_money", "You have the {price}", "price", "$14,200"), On = true });
        before.Rows.Add(new WorkFormRow { Field = WorkField.Check, Label = w.T("reallife_work_room_to_keep", "Room for it among your things"), On = true });
        r.Bands.Add(before);
        t.Haggle = r;
        return t;
    }

    private static string Cond(UtilitiesWords w, int percent) => Sf(w, "reallife_hustle_condition_good", "percent", percent.ToString());

    private static string Mny(int n) => "$" + n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

    // What the player owns from the Marketplace: one fixture for Yours and Offers, so the thing a buyer bids on is a thing that is on sale.
    private static readonly object[][] MarketOwned =
    {
        new object[] { "k:sofa", UiIcon.Sofa, "kept", 890, 0, 960, 0, 405, "Tomas Reyes", 80 }, new object[] { "k:dough_mixer", UiIcon.Factory, "listed", 3900, 150, 4600, 4600, 398, "Colm Doyle", 78 },
        new object[] { "Old cargo van", UiIcon.Package, "waiting", 6200, 0, 6900, 0, 411, "Harbourside Freight HSF", 61 }, new object[] { "k:display_fridge", UiIcon.Thermometer, "listed", 980, 40, 1140, 1150, 401, "Leo Park", 84 },
        new object[] { "k:armchair", UiIcon.Home, "delivering", 300, 25, 320, 0, 412, "Nia Brooks", 90 }, new object[] { "k:espresso_machine", UiIcon.Coffee, "fix", 1700, 0, 1950, 0, 395, "Sofia Marin", 48 }
    };


    // A thing's name: "k:" and a key of the catalogue is its name in the player's words, anything else a name the game gives (a vehicle's).
    // What was sold lately and the trade's record, the foot of Yours and of Offers.
    private static void MarketSalesFoot(UtilitiesWords w, WorkTab t)
    {
        t.Below.Add(MarketBands.Sold(w, new[] { UiIcon.Monitor, UiIcon.Monitor, UiIcon.GameController },
            new[] { Sx(w, "reallife_hustle_thing_television"), Sx(w, "reallife_hustle_thing_laptop"), Sx(w, "reallife_hustle_thing_console") },
            new[] { w.F("reallife_work_sold_to", "To {buyer}, {day}", "buyer", "Leo Park", "day", SDay(w, 409)), w.F("reallife_work_sold_to", "To {buyer}, {day}", "buyer", "Nia Brooks", "day", SDay(w, 403)),
                w.F("reallife_work_sold_to", "To {buyer}, {day}", "buyer", "Tomas Reyes", "day", SDay(w, 399)) }, new[] { "$420 (+$70)", "$980 (+$120)", "$380 (+$80)" }));
        t.Below.Add(MarketBands.Trade(w, "$2,140", 34, 12));
    }

    private static string MName(UtilitiesWords w, string name) => name.StartsWith("k:") ? Sx(w, "reallife_hustle_thing_" + name.Substring(2)) : name;

    private static WorkTab MarketYours(UtilitiesWords w, WorkView v)
    {
        var t = new WorkTab { Shape = WorkShape.Table, Search = w.T("reallife_work_search_yours", "Search your things") };
        t.Columns = new[] { WkCol(w.T("reallife_work_col_thing", "Thing"), UiCellKind.Tile), WkCol(w.T("reallife_work_col_state", "State"), UiCellKind.Chip, 220), WkCol(w.T("reallife_work_col_paid", "Paid"), UiCellKind.Money, 88),
            WkCol(w.T("reallife_work_col_worth", "Worth now"), UiCellKind.Money, 96) };
        t.EmptyTitle = w.T("reallife_work_no_yours_title", "You own nothing from here");
        t.EmptyBody = w.T("reallife_work_no_yours", "What you buy comes here to fix, clean, list or keep.");
        int paidAll = 0, worthAll = 0;
        for (int i = 0; i < MarketOwned.Length; i++)
        {
            object[] p = MarketOwned[i];
            string name = MName(w, (string)p[0]), kind = (string)p[2];
            int paid = (int)p[3], spent = (int)p[4], worth = (int)p[5], asking = (int)p[6];
            string state = kind == "kept" ? w.T("reallife_hustle_state_kept", "Yours") : kind == "listed" ? w.T("reallife_hustle_state_listed", "For sale")
                : kind == "waiting" ? w.F("reallife_hustle_state_to_collect_by", "Collect by day {day}", "day", "415") : kind == "delivering" ? w.T("reallife_hustle_state_delivering", "On its way")
                : w.T("reallife_hustle_state_to_fix", "To fix at home");
            UiRole tone = kind == "listed" ? UiRole.Ok : kind == "waiting" ? UiRole.Warn : kind == "delivering" ? UiRole.Accent : UiRole.Muted;
            paidAll += paid + spent;
            worthAll += worth;
            var r = new WorkRow { Id = i + 1, Icon = (UiIcon)p[1], Name = name, Sub = w.F("reallife_work_bought_day", "Bought {day}", "day", SDay(w, (int)p[7])), Cells = new[] { null, state, Mny(paid), Mny(worth) }, Status = state, Tone = tone, Chip = state,
                ChipTone = tone };
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_paid", "Paid"), Mny(paid)));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_worth", "Worth now"), Mny(worth)));
            var who = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_bought_from", "Bought from") };
            who.People.Add(new WorkPerson { Name = (string)p[8], Ring = UiRole.Ok });
            r.Sections.Add(who);
            var facts = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_the_thing", "The thing") };
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_condition_now", "Condition now"), (int)p[9] + "%"));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_spent_on", "Spent on it"), Mny(spent)));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_profit_at_worth", "Profit at its worth"), Mny(worth - paid - spent)));
            if (kind == "listed")
            {
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_listed_at", "Listed at"), Mny(asking)));
            }
            r.Sections.Add(facts);
            if ((kind == "kept" || kind == "listed") && i == (v.Row >= 0 ? v.Row : 0))
            {
                int step = v.Get(0, 12);
                int ask = worth * (840 + step * 12) / 1000;
                t.Amounts.Clear();
                t.Amounts.Add(new WorkAmount { Id = 0, Value = step, Min = 0, Max = 20, Figure = Mny(ask) });
                r.Sections.Add(new WorkSection { Part = WorkPart.Amount, Title = w.T("reallife_work_your_asking", "Your asking price"), Stepper = 0 });
            }
            if (kind == "listed")
            {
                r.Acts.Add(WkAct(123, w.T("reallife_hustle_button_reprice", "Change the price"), UiButtonKind.Primary, UiIcon.Tag, null, false, r.Id));
                r.Acts.Add(WkAct(105, w.T("reallife_hustle_button_unlist", "Take it off sale"), UiButtonKind.Secondary, UiIcon.Close, null, false, r.Id));
            }
            else if (kind == "kept")
            {
                r.Acts.Add(WkAct(109, w.F("reallife_work_list_at", "List at {price}", "price", Mny(worth * (840 + v.Get(0, 12) * 12) / 1000)), UiButtonKind.Primary, UiIcon.Tag, null, false, r.Id));
                r.Acts.Add(WkAct(110, w.T("reallife_hustle_button_plan_fix", "Fix it at home"), UiButtonKind.Secondary, UiIcon.Wrench, null, false, r.Id));
                r.Acts.Add(WkAct(111, w.T("reallife_hustle_button_plan_clean", "Clean it at home"), UiButtonKind.Secondary, UiIcon.Drop, null, false, r.Id));
            }
            else if (kind == "waiting" || kind == "fix")
            {
                r.Acts.Add(WkAct(114, w.T("reallife_hustle_button_way", "Show the way"), UiButtonKind.Primary, UiIcon.Locate, null, true, r.Id));
                if (kind == "fix")
                {
                    r.Acts.Add(WkAct(113, w.T("reallife_hustle_button_cancel_plan", "Not now"), UiButtonKind.Secondary, UiIcon.Close, null, false, r.Id));
                }
            }
            t.Rows.Add(r);
        }
        MarketSalesFoot(w, t);
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_things", "Things"), MarketOwned.Length.ToString()));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_in_all", "Paid and spent"), Mny(paidAll)));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_worth_all", "Worth now"), Mny(worthAll)));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_profit", "Profit at worth"), Mny(worthAll - paidAll)));
        return t;
    }

    private static WorkTab MarketOffers(UtilitiesWords w)
    {
        var t = new WorkTab { Shape = WorkShape.Table };
        t.Columns = new[] { WkCol(w.T("reallife_work_col_thing", "Thing"), UiCellKind.Tile), WkCol(w.T("reallife_work_col_buyer", "Buyer"), UiCellKind.Text, 152), WkCol(w.T("reallife_work_col_asking", "Asking"), UiCellKind.Money, 88),
            WkCol(w.T("reallife_work_col_offer", "Offer"), UiCellKind.Money, 88), WkCol(w.T("reallife_work_col_against_ask", "Against asking"), UiCellKind.Trend, 104) };
        t.EmptyTitle = w.T("reallife_work_no_offers_title", "No offers");
        t.EmptyBody = w.T("reallife_work_no_offers", "List something of yours and buyers make offers over the day.");
        object[][] offers = { new object[] { 1, "Priya Shah", 4600, 4300 }, new object[] { 3, "Hana Okada", 1150, 1050 } };
        int best = 0;
        for (int i = 0; i < offers.Length; i++)
        {
            object[] o = offers[i];
            object[] item = MarketOwned[(int)o[0]];
            int asking = (int)o[2], offer = (int)o[3], cost = (int)item[3] + (int)item[4], pct = (offer - asking) * 100 / asking;
            best = System.Math.Max(best, offer);
            var r = new WorkRow { Id = i + 1, Icon = (UiIcon)item[1], Name = MName(w, (string)item[0]), Sub = w.F("reallife_work_bought_day", "Bought {day}", "day", SDay(w, (int)item[7])),
                Cells = new[] { null, (string)o[1], Mny(asking), Mny(offer), WorkBuilder.Percent(pct) }, Values = new[] { 0, 0, 0, 0, pct < 0 ? -1 : 1 } };
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_offer", "Offer"), Mny(offer)));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_asking", "Asking"), Mny(asking)));
            var sec = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_the_buyer", "The buyer") };
            sec.People.Add(new WorkPerson { Name = (string)o[1], Role = w.F("reallife_work_offer_lapses", "Lapses {day}, {time}", "day", SDay(w, 413 + i), "time", "09:00"), Ring = UiRole.Ok });
            r.Sections.Add(sec);
            var facts = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_the_deal", "The deal") };
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_what_it_cost", "What it cost you"), Mny(cost)));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_profit_at_offer", "Your profit at their offer"), Mny(offer - cost)));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_profit_at_asking", "Your profit at your asking"), Mny(asking - cost)));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_worth", "Worth now"), Mny((int)item[5])));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_condition_now", "Condition now"), (int)item[9] + "%"));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_counters_so_far", "Counters so far"), i.ToString()));
            r.Sections.Add(facts);
            r.Acts.Add(WkAct(102, w.F("reallife_work_accept_at", "Accept {offer}", "offer", Mny(offer)), UiButtonKind.Primary, UiIcon.Check, null, false, r.Id));
            r.Acts.Add(WkAct(103, w.T("reallife_hustle_button_counter", "Counter"), UiButtonKind.Secondary, UiIcon.Arrows, null, false, r.Id));
            r.Acts.Add(WkAct(104, w.T("reallife_hustle_button_decline", "Turn it down"), UiButtonKind.Destructive, UiIcon.Close, null, false, r.Id));
            t.Rows.Add(r);
        }
        MarketSalesFoot(w, t);
        t.Below.Add(WorkBands.Tiles(w.T("reallife_work_band_kept", "Yours, not yet listed"), new[] { UiIcon.Sofa, UiIcon.Coffee }, new[] { Sx(w, "reallife_hustle_thing_sofa"), Sx(w, "reallife_hustle_thing_espresso_machine") },
            new[] { w.T("reallife_work_not_listed", "Not for sale yet"), w.T("reallife_work_not_listed", "Not for sale yet") }, new[] { "$960", "$1,950" }));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_offers_open", "Offers open"), offers.Length.ToString()));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_best_offer", "Best offer"), Mny(best)));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_on_sale", "Things on sale"), "2"));
        t.NoteLead = w.T("reallife_work_offers_note_lead", "Buyers come back.");
        t.NoteText = w.T("reallife_work_offers_note", "An offer that is turned down may rise a little, or the buyer goes. A counter at your asking price is taken or not.");
        return t;
    }

    private static WorkTab MarketFreelance(UtilitiesWords w)
    {
        var t = new WorkTab { Shape = WorkShape.Table };
        t.Columns = new[] { WkCol(w.T("reallife_work_col_client", "Client"), UiCellKind.Person), WkCol(w.T("reallife_work_col_job", "Job"), UiCellKind.Text, 152), WkCol(w.T("reallife_work_col_when", "When"), UiCellKind.Text, 140),
            WkCol(w.T("reallife_work_col_pay", "Pay"), UiCellKind.Money, 80), WkCol(w.T("reallife_work_col_state", "State"), UiCellKind.Chip, 184) };
        t.EmptyTitle = w.T("reallife_work_no_gigs_title", "No visit is asked for today");
        t.EmptyBody = w.T("reallife_work_no_gigs", "Clients ask each morning for detailing, handyman jobs and moves.");
        // client, job, address, window, pay, state, helper fee
        object[][] gigs =
        {
            new object[] { "Rose Wilder", "reallife_hustle_gig_handyman", "14 Elm Street", "", 130, "asked", 39, 412, "14:00" }, new object[] { "Dev Malhotra", "reallife_hustle_gig_detailing", "8 Birch Row", "", 95, "booked", 29, 413, "10:00" },
            new object[] { "Imogen Pratt", "reallife_hustle_gig_moving", "3 Quay Road, then 21 Pine Street", "", 210, "asked", 63, 413, "15:00" }, new object[] { "Callum Reid", "reallife_hustle_gig_handyman", "40 Mill Lane", "", 120, "helper", 36, 415, "09:00" }
        };
        int worth = 0;
        for (int i = 0; i < gigs.Length; i++)
        {
            object[] g = gigs[i];
            string kind = (string)g[5];
            string job = Sx(w, (string)g[1]);
            string when = SDay(w, (int)g[7]) + " " + (string)g[8];
            string state = kind == "booked" ? w.T("reallife_hustle_state_booked", "Booked") : kind == "helper" ? w.F("reallife_hustle_state_helper", "A helper, done at {time}", "time", "12:00") : w.T("reallife_hustle_state_asked", "Asked for");
            UiRole tone = kind == "asked" ? UiRole.Accent : UiRole.Ok;
            worth += (int)g[4];
            var r = new WorkRow { Id = i + 1, Person = (string)g[0], Name = (string)g[0], Sub = (string)g[2], Ring = UiRole.Ok, Cells = new[] { null, job, when, Mny((int)g[4]), state }, Status = state, Tone = tone,
                Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone } };
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_pay", "Pay"), Mny((int)g[4])));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_when", "When"), when));
            var facts = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_the_visit", "The visit") };
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_job", "Job"), job));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_at", "At"), (string)g[2]));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_helper_fee", "A helper takes"), Mny((int)g[6])));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_your_take", "You keep with a helper"), Mny((int)g[4] - (int)g[6])));
            r.Sections.Add(facts);
            var before = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_your_record_at", "Your record at it") };
            before.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_done_before", "Done before"), "1"));
            before.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_stars_average", "Stars on average"), job == Sx(w, "reallife_hustle_gig_moving") ? "5.0" : job == Sx(w, "reallife_hustle_gig_detailing") ? "4.5" : "4.0"));
            r.Sections.Add(before);
            if (kind == "asked")
            {
                r.Acts.Add(WkAct(106, w.T("reallife_hustle_button_book", "Book it"), UiButtonKind.Primary, UiIcon.Check, null, false, r.Id));
            }
            if (kind != "helper")
            {
                r.Acts.Add(WkAct(108, w.T("reallife_hustle_button_helper", "Send a helper"), UiButtonKind.Secondary, UiIcon.People, null, false, r.Id));
            }
            if (kind == "booked")
            {
                r.Acts.Add(WkAct(107, w.T("reallife_hustle_button_cancel", "Call it off"), UiButtonKind.Destructive, UiIcon.Close, null, false, r.Id));
            }
            t.Rows.Add(r);
        }
        t.Below.Add(MarketBands.Done(w, new[] { "Hana Okada", "Leo Park", "Nia Brooks" }, new[] { Sx(w, "reallife_hustle_gig_moving") + ", " + SDay(w, 409), Sx(w, "reallife_hustle_gig_handyman") + ", " + SDay(w, 406),
            Sx(w, "reallife_hustle_gig_detailing") + ", " + SDay(w, 402) }, new[] { "$210", "$130", "$95" }, new[] { 10, 8, 9 }));
        t.Below.Add(MarketBands.Standing(w, "4.4", 17, 2));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_visits_open", "Visits open"), gigs.Length.ToString()));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_visits_pay", "They pay in all"), Mny(worth)));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_earned", "Earned so far"), Mny(2840)));
        return t;
    }
}
