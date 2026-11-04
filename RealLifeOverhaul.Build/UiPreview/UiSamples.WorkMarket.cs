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
        WkNav(UiIcon.Package, w.T("reallife_hustle_tab_yours", "Yours"), "3"), WkNav(UiIcon.Arrows, w.T("reallife_hustle_tab_offers", "Offers"), "2"), WkNav(UiIcon.Wrench, w.T("reallife_hustle_tab_freelance", "Freelance"), "2") };

    private static WorkApp MarketAppOf(UtilitiesWords w, string state, WorkView v)
    {
        var app = new WorkApp { Ready = true, Nav = MarketNav(w, state) };
        app.Tabs = new[] { v.Sub == 1 ? MarketHaggle(w, v) : MarketListings(w, state), MarketYours(w), MarketOffers(w), MarketFreelance(w) };
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
            new object[] { "Blue sport coupe", UiIcon.Car, "$38,500", -4, "Marco Bellini", 0, null }, new object[] { "Red delivery van", UiIcon.Truck, "$14,200", -12, "Huang Guo", 0, "Rival" },
            new object[] { "Box truck", UiIcon.Truck, "$27,900", -6, "Harbourside Freight HSF", 0, null }, new object[] { "Tan minibus", UiIcon.Train, "$19,400", 3, "Amara Nwosu", 0, null },
            new object[] { "Espresso machine", UiIcon.Coffee, "$1,950", -9, "Sofia Marin", 1, null }, new object[] { "Commercial grill", UiIcon.Fire, "$2,300", -3, "Hana Okada", 1, null },
            new object[] { "Display fridge", UiIcon.Thermometer, "$1,150", -15, "Leo Park", 1, "Discount" }, new object[] { "Dough mixer", UiIcon.Cpu, "$4,600", 5, "Colm Doyle", 1, null },
            new object[] { "Lounge armchair", UiIcon.Sofa, "$320", -8, "Nia Brooks", 2, null }, new object[] { "Corner sofa", UiIcon.Sofa, "$890", 2, "Tomas Reyes", 2, null },
            new object[] { "Dining set", UiIcon.ForkKnife, "$740", -11, "Priya Shah", 2, null }, new object[] { "Chest freezer", UiIcon.Thermometer, "$1,380", -5, "Ada Voss", 2, null },
            new object[] { "Coffee pallet", UiIcon.Package, "$1,530", -15, "Colm Doyle", 3, null }, new object[] { "Flour, 60 sacks", UiIcon.Package, "$1,140", -12, "Huang Guo", 3, "Rival" },
            new object[] { "Milk crates, 40", UiIcon.Package, "$690", -9, "Leo Park", 3, null }
        };
        for (int i = 0; i < list.Length; i++)
        {
            object[] p = list[i];
            string seller = (string)p[4];
            var r = new WorkRow { Id = i + 1, Icon = (UiIcon)p[1], Name = (string)p[0], Price = (string)p[2], Under = WorkBuilder.Percent((int)p[3]), UnderValue = (int)p[3], Seller = seller, Group = (int)p[5],
                Status = (string)p[6] == null ? null : (string)p[6] == "Rival" ? w.T("reallife_work_tag_rival", "Rival") : w.T("reallife_work_tag_discount_short", "Discount"), Tone = UiRole.Muted };
            bool car = (int)p[5] == 0;
            r.Title = r.Name;
            r.Sub = car ? "Vehicle, 3 years old, 41,000 km" : (int)p[5] == 1 ? "Machine, 2 years old, works as new" : (int)p[5] == 2 ? "Furniture, lightly used" : "Stock lot, packed";
            int worthRaw = (int)((int.Parse(((string)p[2]).Replace("$", string.Empty).Replace(",", string.Empty)) * 100.0) / (100 + (int)p[3]));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_asking", "Asking"), (string)p[2]));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_market_price", "Market price"), "$" + worthRaw.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)));
            var sec = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_the_seller", "The seller") };
            sec.People.Add(new WorkPerson { Name = seller, Role = i == 1 || i == 13 ? "Guo Logistics, a rival" : "Seller, " + (3 + i % 3) + " sales", Right = "4.1", Ring = UiRole.None });
            r.Sections.Add(sec);
            var facts = new WorkSection { Part = WorkPart.Pairs, Title = car ? w.T("reallife_work_the_vehicle", "The vehicle") : w.T("reallife_work_the_thing", "The thing") };
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_condition", "Condition said"), "Good, 82%"));
            if (car)
            {
                facts.Pairs.Add(new KeyValuePair<string, string>("Cargo", "1.8 tonnes"));
                facts.Pairs.Add(new KeyValuePair<string, string>("Fuel", "Diesel, 9 l per 100 km"));
                facts.Pairs.Add(new KeyValuePair<string, string>("Parked at", "Hell's Kitchen"));
            }
            else
            {
                facts.Pairs.Add(new KeyValuePair<string, string>("Age", "2 years"));
                facts.Pairs.Add(new KeyValuePair<string, string>("Where", "Midtown"));
            }
            r.Sections.Add(facts);
            if (i == 1)
            {
                var more = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_more_from", "More from this seller"), Note = w.F("reallife_work_n_listings", "{n} listings", "n", "2") };
                more.People.Add(new WorkPerson { Name = "Flour, 60 sacks", Role = "Stock lot", Right = "$1,140", Icon = UiIcon.Package });
                r.Sections.Add(more);
            }
            var before = new WorkSection { Part = WorkPart.Checks, Title = w.T("reallife_work_before_buy", "Before you buy") };
            before.Checks.Add(new WorkNeed { Words = w.F("reallife_work_you_have_money", "You have the {price}", "price", (string)p[2]), Met = true });
            before.Checks.Add(new WorkNeed { Words = car ? "Hudson Warehouse has a free bay" : "A free place at one of your sites", Met = true });
            before.Checks.Add(new WorkNeed { Words = "Delivery adds $120 and a day", Met = false });
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
        var r = new WorkRow { Id = 2, Name = "Red delivery van", Kicker = w.T("reallife_work_haggle_kicker", "Haggle with the seller"), Seller = "Huang Guo", Line = "Guo Logistics, a rival, rated 4.1", Status = w.T("reallife_work_tag_discount", "Sells at a discount"), Tone = UiRole.Muted,
            Sentence = "Huang Guo asks $14,200 for the van, 12% under its market price. You may make up to 3 offers, and each one they turn down makes the next harder.", Hint = w.T("reallife_work_your_offer", "Your offer") };
        r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_their_price", "Their price"), "$14,200"));
        r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_market_price", "Market price"), "$16,100"));
        r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_offers_left", "Offers left"), "3 of 3"));
        r.Acts.Add(new WorkAct { Kind = 103, Arg = 2, Label = w.F("reallife_work_make_offer", "Offer {amount}", "amount", "$" + offer.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)), Style = UiButtonKind.Primary, Icon = UiIcon.Check,
            Note = w.T("reallife_work_offer_note", "They may accept, counter or turn it down."), Choice = true });
        r.Acts.Add(new WorkAct { Kind = 100, Arg = 2, Label = w.T("reallife_work_pay_asking", "Pay the asking price"), Style = UiButtonKind.Secondary, Icon = UiIcon.Coin, Note = w.F("reallife_work_pay_asking_note", "Buy now at {price}, no more haggling.", "price", "$14,200"), Choice = true });
        r.Acts.Add(new WorkAct { Kind = WorkKinds.Back, Arg = 2, Label = w.T("reallife_work_walk_away_haggle", "Walk away"), Style = UiButtonKind.Destructive, Icon = UiIcon.Back, Opens = true,
            Note = w.F("reallife_work_walk_note", "Back to the listings. The {thing} stays for sale today.", "thing", "van"), Choice = true });
        t.Haggle = r;
        return t;
    }

    private static WorkTab MarketYours(UtilitiesWords w)
    {
        var t = new WorkTab { Shape = WorkShape.Table, Search = w.T("reallife_work_search_yours", "Search your things") };
        t.Columns = new[] { WkCol(w.T("reallife_work_col_thing", "Thing"), UiCellKind.Tile), WkCol(w.T("reallife_work_col_state", "State"), UiCellKind.Chip, 136), WkCol(w.T("reallife_work_col_paid", "Paid"), UiCellKind.Money, 88),
            WkCol(w.T("reallife_work_col_worth", "Worth now"), UiCellKind.Money, 96) };
        t.EmptyTitle = w.T("reallife_work_no_yours_title", "You own nothing from here");
        t.EmptyBody = w.T("reallife_work_no_yours", "What you buy comes here to fix, clean, list or keep.");
        string[][] items =
        {
            new[] { "Corner sofa", "Kept at home", "$890", "$960", "kept" }, new[] { "Dough mixer", "On sale", "$3,900", "$4,600", "listed" }, new[] { "Old cargo van", "Waiting at the seller", "$6,200", "$6,900", "waiting" }
        };
        UiIcon[] icons = { UiIcon.Sofa, UiIcon.Cpu, UiIcon.Truck };
        for (int i = 0; i < items.Length; i++)
        {
            string[] p = items[i];
            UiRole tone = p[4] == "listed" ? UiRole.Ok : p[4] == "waiting" ? UiRole.Warn : UiRole.Muted;
            var r = new WorkRow { Id = i + 1, Icon = icons[i], Name = p[0], Sub = "Bought day " + (396 + i), Cells = new[] { null, p[1], p[2], p[3] }, Status = p[1], Tone = tone, Chip = p[1], ChipTone = tone };
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_paid", "Paid"), p[2]));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_worth", "Worth now"), p[3]));
            if (p[4] == "kept")
            {
                r.Acts.Add(WkAct(109, w.F("reallife_work_list_at", "List at {price}", "price", p[3]), UiButtonKind.Primary, UiIcon.Tag, null, false, r.Id));
                r.Acts.Add(WkAct(110, w.T("reallife_hustle_button_plan_fix", "Fix it at home"), UiButtonKind.Secondary, UiIcon.Wrench, null, false, r.Id));
                r.Acts.Add(WkAct(111, w.T("reallife_hustle_button_plan_clean", "Clean it at home"), UiButtonKind.Secondary, UiIcon.Drop, null, false, r.Id));
            }
            else if (p[4] == "listed")
            {
                r.Acts.Add(WkAct(105, w.T("reallife_hustle_button_unlist", "Take it off sale"), UiButtonKind.Secondary, UiIcon.Close, null, false, r.Id));
            }
            else
            {
                r.Acts.Add(WkAct(114, w.T("reallife_hustle_button_way", "Show the way"), UiButtonKind.Primary, UiIcon.Locate, null, true, r.Id));
            }
            t.Rows.Add(r);
        }
        return t;
    }

    private static WorkTab MarketOffers(UtilitiesWords w)
    {
        var t = new WorkTab { Shape = WorkShape.Table };
        t.Columns = new[] { WkCol(w.T("reallife_work_col_thing", "Thing"), UiCellKind.Tile), WkCol(w.T("reallife_work_col_buyer", "Buyer"), UiCellKind.Text, 152), WkCol(w.T("reallife_work_col_asking", "Asking"), UiCellKind.Money, 88),
            WkCol(w.T("reallife_work_col_offer", "Offer"), UiCellKind.Money, 88), WkCol(w.T("reallife_work_col_against_ask", "Against asking"), UiCellKind.Trend, 104) };
        t.EmptyTitle = w.T("reallife_work_no_offers_title", "No offers");
        t.EmptyBody = w.T("reallife_work_no_offers", "List something of yours and buyers make offers over the day.");
        string[][] offers = { new[] { "Dough mixer", "Priya Shah", "$4,600", "$4,300", "-7" }, new[] { "Dining set", "Colm Doyle", "$740", "$700", "-5" } };
        UiIcon[] icons = { UiIcon.Cpu, UiIcon.ForkKnife };
        for (int i = 0; i < offers.Length; i++)
        {
            string[] p = offers[i];
            var r = new WorkRow { Id = i + 1, Icon = icons[i], Name = p[0], Sub = "Listed at " + p[2], Cells = new[] { null, p[1], p[2], p[3], p[4] + "%" }, Values = new[] { 0, 0, 0, 0, -1 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.Danger } };
            r.Person = null;
            var sec = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_the_buyer", "The buyer") };
            sec.People.Add(new WorkPerson { Name = p[1], Role = "Buyer, 4 purchases", Right = "4.3" });
            r.Sections.Add(sec);
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_offer", "Offer"), p[3]));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_asking", "Asking"), p[2]));
            r.Acts.Add(WkAct(102, w.F("reallife_work_accept_at", "Accept {offer}", "offer", p[3]), UiButtonKind.Primary, UiIcon.Check, null, false, r.Id));
            r.Acts.Add(WkAct(103, w.T("reallife_hustle_button_counter", "Counter"), UiButtonKind.Secondary, UiIcon.Arrows, null, false, r.Id));
            r.Acts.Add(WkAct(104, w.T("reallife_hustle_button_decline", "Turn it down"), UiButtonKind.Destructive, UiIcon.Close, null, false, r.Id));
            t.Rows.Add(r);
        }
        return t;
    }

    private static WorkTab MarketFreelance(UtilitiesWords w)
    {
        var t = new WorkTab { Shape = WorkShape.Table };
        t.Columns = new[] { WkCol(w.T("reallife_work_col_client", "Client"), UiCellKind.Person), WkCol(w.T("reallife_work_col_job", "Job"), UiCellKind.Text, 152), WkCol(w.T("reallife_work_col_when", "When"), UiCellKind.Text, 128),
            WkCol(w.T("reallife_work_col_pay", "Pay"), UiCellKind.Money, 80), WkCol(w.T("reallife_work_col_state", "State"), UiCellKind.Chip, 128) };
        t.EmptyTitle = w.T("reallife_work_no_gigs_title", "No visit is asked for today");
        t.EmptyBody = w.T("reallife_work_no_gigs", "Clients ask each morning for detailing, handyman jobs and moves.");
        string[][] gigs = { new[] { "Rose Wilder", "Handyman", "Today 14:00", "$130", "Asked" }, new[] { "Dev Malhotra", "Detailing", "Tomorrow 10:00", "$95", "Booked" } };
        for (int i = 0; i < gigs.Length; i++)
        {
            string[] p = gigs[i];
            UiRole tone = p[4] == "Booked" ? UiRole.Ok : UiRole.Accent;
            var r = new WorkRow { Id = i + 1, Person = p[0], Name = p[0], Sub = "Client, 3rd visit", Ring = UiRole.Ok, Cells = new[] { null, p[1], p[2], p[3], p[4] }, Status = p[4], Tone = tone, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone } };
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_pay", "Pay"), p[3]));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_when", "When"), p[2]));
            if (p[4] == "Asked")
            {
                r.Acts.Add(WkAct(106, w.T("reallife_hustle_button_book", "Book it"), UiButtonKind.Primary, UiIcon.Check, null, false, r.Id));
                r.Acts.Add(WkAct(108, w.T("reallife_hustle_button_helper", "Send a helper"), UiButtonKind.Secondary, UiIcon.People, null, false, r.Id));
            }
            else
            {
                r.Acts.Add(WkAct(108, w.T("reallife_hustle_button_helper", "Send a helper"), UiButtonKind.Secondary, UiIcon.People, null, false, r.Id));
                r.Acts.Add(WkAct(107, w.T("reallife_hustle_button_cancel", "Call it off"), UiButtonKind.Destructive, UiIcon.Close, null, false, r.Id));
            }
            t.Rows.Add(r);
        }
        return t;
    }
}
