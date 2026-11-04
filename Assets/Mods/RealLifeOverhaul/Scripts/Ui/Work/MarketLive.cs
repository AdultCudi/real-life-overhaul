using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Hustles;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Marketplace on the kit: listings by category with the asking against worth, a persistent seller with a rating and "more from this
    /// seller", what to check before buying, the player's own things, buyers' offers and freelance visits. Read from the Marketplace's own
    /// records; every button goes to the director that owns it, buying and selling through the game's own question box. Nothing a frame.
    /// </summary>
    public sealed class MarketLive : IWorkSource
    {
        public static readonly MarketLive Instance = new MarketLive();

        private const int Buy = 100, Haggle = 101, Accept = 102, Counter = 103, Decline = 104, Unlist = 105, Book = 106, Cancel = 107, Send = 108,
            List = 109, FixHome = 110, CleanHome = 111, ToShop = 112, CancelPlan = 113, WayItem = 114;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private int _local, _wanted = -1, _readGeneration = -1;
        private WorkApp _app = new WorkApp();

        private MarketLive() { }

        public string Id => "marketplace";
        public string Title => W.T("reallife_hustle_app", "Marketplace");
        public UiIcon Icon => UiIcon.Store;
        public bool Across => true;
        public string T(string key, string english) => UiText.Words(key, english);
        private static UtilitiesWords W => CareerLive.Words();

        public int Generation
        {
            get
            {
                GameInstance save = SaveGameManager.Current;
                return DaySeriesDirector.Generation + _local + (save != null ? save.Day * 24 + save.Hour : 0);
            }
        }

        public static void Want(int tab)
        {
            Instance._wanted = tab;
            Instance._local++;
        }

        public int TakeWanted()
        {
            int tab = _wanted;
            _wanted = -1;
            return tab;
        }

        public UiNavEntry[] Nav(WorkView view)
        {
            Read();
            return _app.Nav;
        }

        public WorkPage Page(WorkView view)
        {
            Read();
            return WorkTabPages.Build(_app, view, W);
        }

        private void Read()
        {
            int generation = Generation;
            if (_readGeneration == generation)
            {
                return;
            }
            _readGeneration = generation;
            HustleDirector d = HustleDirector.Instance;
            HustleState state = d != null ? d.State : null;
            UtilitiesWords w = W;
            _app = new WorkApp { Ready = state != null, OffBody = w.T("reallife_work_market_off",
                "Load a save with the Marketplace switched on to buy and sell.") };
            _app.Nav = new[]
            {
                new UiNavEntry { Icon = UiIcon.Tag, Label = w.T("reallife_hustle_tab_listings", "Listings"), Count = state != null
                    && state.Listings.Count > 0 ? state.Listings.Count.ToString(Inv) : null },
                new UiNavEntry { Icon = UiIcon.Package, Label = w.T("reallife_hustle_tab_yours", "Yours"), Count = state != null
                    && state.Items.Count > 0 ? state.Items.Count.ToString(Inv) : null },
                new UiNavEntry { Icon = UiIcon.Arrows, Label = w.T("reallife_hustle_tab_offers", "Offers"), Count = state != null
                    && state.Offers.Count > 0 ? state.Offers.Count.ToString(Inv) : null },
                new UiNavEntry { Icon = UiIcon.Wrench, Label = w.T("reallife_hustle_tab_freelance", "Freelance") }
            };
            if (!_app.Ready)
            {
                return;
            }
            _app.Tabs = new[] { Listings(d, state), Yours(d, state), Offers(d, state), Freelance(d, state) };
        }

        private static UiColumn Col(string title, bool grow = false, bool right = false) => new UiColumn { Title = title, Grow = grow,
            Width = grow ? 0f : 184f, Right = right };

        private WorkTab Listings(HustleDirector d, HustleState s)
        {
            UtilitiesWords w = W;
            int[] counts = new int[SellerRules.Categories];
            var t = new WorkTab { TableTitle = w.T("reallife_hustle_tab_listings", "Listings"), Lead = w.T("reallife_work_no_listings",
                "Nothing is listed today. New things arrive each morning."),
                Columns = new[] { Col(w.T("reallife_work_col_thing", "Thing"), true), Col(w.T("reallife_work_col_seller", "Seller")),
                    Col(w.T("reallife_work_col_asking", "Asking")), Col(w.T("reallife_work_col_against_worth", "Against worth"), false, true) } };
            var book = d.Sellers;
            var more = new List<Listing>(6);
            for (int i = 0; i < s.Listings.Count; i++)
            {
                Listing l = s.Listings[i];
                ThingDef def = HustleCatalogue.Thing(l.Thing);
                int category = SellerRules.CategoryOf(l, def);
                counts[category]++;
                int worth = HustleRules.SaidWorth(l, HustleGame.Level);
                int versus = SellerRules.VersusWorth(l.Asking, worth);
                Seller seller = l.SellerId > 0 && book != null ? book.Of(l.SellerId) : null;
                string name = l.Car ? CarName(l) : HustleText.Thing(l.Thing, l.Car);
                var r = new WorkRow { Id = l.Id, Group = category,
                    Icon = l.Car ? UiIcon.Car : category == SellerRules.CategoryMachines ? UiIcon.Factory : category == SellerRules.CategoryStock ? UiIcon.Package : UiIcon.Sofa,
                    Cells = new[] { name, l.Seller, HustleText.Money(l.Asking), worth > 0 ? WorkBuilder.Percent(versus) : "-" },
                        Status = worth > 0 ? (versus <= 0 ? Math.Abs(versus).ToString(Inv) + "% " + w.T("reallife_work_under",
                        "under") : versus.ToString(Inv) + "% " + w.T("reallife_work_over", "over")) : null,
                    Tone = versus <= 0 ? UiRole.Ok : UiRole.Warn };
                string sellerLine = seller != null ? seller.Name + ", " + w.F("reallife_work_rated", "rated {rating}", "rating",
                    (seller.Rating / 10f).ToString("0.0", Inv)) + (seller.Tag == (int)SellerTag.Discount ? ", " + w.T("reallife_work_tag_discount",
                    "sells at a discount") : string.Empty) : l.Seller;
                r.Detail = w.F("reallife_work_listing_line", "{thing} from {seller}, asking {asking}. Market price {worth}. Before you buy: you have {cash}.", "thing", name, "seller", sellerLine,
                    "asking", HustleText.Money(l.Asking), "worth", worth > 0 ? HustleText.Money(worth) : "-", "cash", HustleText.Money(HustleGame.Cash));
                r.Acts.Add(new WorkAct { Kind = Buy, Arg = l.Id, Label = w.F("reallife_work_buy_now", "Buy now, {price}", "price",
                    HustleText.Money(l.Asking)), Style = UiButtonKind.Primary, Icon = UiIcon.Check,
                    Why = l.Gone ? w.T("reallife_work_sold_elsewhere",
                        "Sold to someone else today.") : HustleGame.Cash < l.Asking ? w.T("reallife_work_no_money", "You do not have the money.") : null });
                r.Acts.Add(new WorkAct { Kind = Haggle, Arg = l.Id, Label = w.T("reallife_hustle_button_offer", "Haggle"),
                    Style = UiButtonKind.Secondary, Icon = UiIcon.Arrows,
                    Why = l.Gone ? w.T("reallife_work_sold_elsewhere",
                        "Sold to someone else today.") : l.Haggles >= HustleCatalogue.HagglesMost ? w.T("reallife_work_haggled_out",
                        "They will not haggle again.") : null });
                int deliver = HustleRules.DeliveryFee(l.Asking, HustleGame.Level);
                r.Facts.Add(new KeyValuePair<string, string>(w.T("reallife_work_market_price", "Market price"), worth > 0 ? HustleText.Money(worth) : "-"));
                r.Facts.Add(new KeyValuePair<string, string>(w.T("reallife_work_condition", "Condition said"), HustleText.Condition(l.Shown)));
                r.Facts.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_seller", "Seller"),
                    seller != null ? seller.Name + ", " + (seller.Rating / 10f).ToString("0.0", Inv) + ", " + w.F("reallife_work_sold_n", "{n} sold",
                    "n", seller.Sold.ToString(Inv)) : l.Seller));
                r.Facts.Add(new KeyValuePair<string, string>(w.T("reallife_work_before_money", "You have the money"),
                    HustleGame.Cash >= l.Asking ? w.T("reallife_work_yes", "Yes") : w.T("reallife_work_no", "No")));
                r.Facts.Add(new KeyValuePair<string, string>(w.T("reallife_work_delivery_adds", "Delivery adds"),
                    HustleText.Money(deliver) + ", " + w.T("reallife_work_next_morning", "next morning")));
                r.Acts.Insert(1, new WorkAct { Kind = Buy + 20, Arg = l.Id, Label = w.F("reallife_work_buy_deliver", "Buy and deliver, {price}",
                    "price", HustleText.Money(l.Asking + deliver)), Style = UiButtonKind.Secondary, Icon = UiIcon.Truck,
                    Why = l.Gone ? w.T("reallife_work_sold_elsewhere",
                        "Sold to someone else today.") : HustleGame.Cash < l.Asking + deliver ? w.T("reallife_work_no_money", "You do not have the money.") : null });
                r.Acts.Add(new WorkAct { Kind = Buy + 21, Arg = l.Id, Label = w.T("reallife_hustle_button_way", "Show the way"),
                    Style = UiButtonKind.Secondary, Icon = UiIcon.Locate, Opens = true });
                if (seller != null && SellerRules.MoreFrom(s.Listings, seller.Id, l.Id, more) > 0)
                {
                    r.PeopleTitle = w.T("reallife_work_more_from", "More from this seller");
                    for (int k = 0; k < more.Count && k < 3; k++)
                    {
                        r.People.Add(new WorkPerson { Name = more[k].Car ? HustleText.Thing(more[k].Thing, true) : HustleText.Thing(more[k].Thing,
                            more[k].Car), Role = HustleText.Money(more[k].Asking), Icon = more[k].Car ? UiIcon.Truck : UiIcon.Package });
                    }
                }
                t.Rows.Add(r);
            }
            t.Chips = new[] { w.F("reallife_work_filter_all", "All {n}", "n", s.Listings.Count.ToString(Inv)), w.F("reallife_work_cat_vehicles",
                "Vehicles {n}", "n", counts[0].ToString(Inv)),
                w.F("reallife_work_cat_machines", "Machines {n}", "n", counts[1].ToString(Inv)), w.F("reallife_work_cat_furniture", "Household {n}",
                    "n", counts[2].ToString(Inv)),
                w.F("reallife_work_cat_stock", "Stock {n}", "n", counts[3].ToString(Inv)) };
            t.Grid = true;
            t.GridKicker = w.T("reallife_hustle_tab_listings", "Listings");
            return t;
        }

        private static string CarName(Listing l) => HustleText.Thing(l.Thing, true);

        private WorkTab Yours(HustleDirector d, HustleState s)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { TableTitle = w.T("reallife_hustle_tab_yours", "Yours"), Lead = w.T("reallife_work_no_yours",
                "Nothing bought yet. What you buy appears here to fix, clean, list or keep."),
                Columns = new[] { Col(w.T("reallife_work_col_thing", "Thing"), true), Col(w.T("reallife_work_col_state", "State")),
                    Col(w.T("reallife_work_col_paid", "Paid")), Col(w.T("reallife_work_col_worth", "Worth"), false, true) } };
            for (int i = 0; i < s.Items.Count; i++)
            {
                Owned o = s.Items[i];
                int worth = d.WorthOf(o);
                string name = o.Car ? HustleText.Thing(o.Thing, true) : HustleText.Thing(o.Thing, o.Car);
                var r = new WorkRow { Id = o.Id, Icon = o.Car ? UiIcon.Car : UiIcon.Package, Cells = new[] { name, o.State.ToString(),
                    HustleText.Money(o.Paid), HustleText.Money(worth) },
                    Detail = w.F("reallife_work_owned_line", "{thing}: paid {paid}, worth {worth} now.", "thing", name, "paid",
                        HustleText.Money(o.Paid), "worth", HustleText.Money(worth)) };
                if (o.State == OwnedState.Listed)
                {
                    r.Acts.Add(new WorkAct { Kind = Unlist, Arg = o.Id, Label = w.T("reallife_hustle_button_unlist", "Take it off sale"),
                        Style = UiButtonKind.Secondary, Icon = UiIcon.Close });
                }
                else if (o.State == OwnedState.Kept && o.Plan != OwnedPlan.None)
                {
                    r.Acts.Add(new WorkAct { Kind = CancelPlan, Arg = o.Id, Label = w.T("reallife_hustle_button_cancel_plan", "Cancel the plan"),
                        Style = UiButtonKind.Secondary, Icon = UiIcon.Close });
                    r.Acts.Add(new WorkAct { Kind = WayItem, Arg = o.Id, Label = w.T("reallife_hustle_button_way", "Show the way"),
                        Style = UiButtonKind.Secondary, Icon = UiIcon.Locate, Opens = true });
                }
                else if (o.State == OwnedState.Waiting)
                {
                    r.Acts.Add(new WorkAct { Kind = WayItem, Arg = o.Id, Label = w.T("reallife_hustle_button_way", "Show the way"),
                        Style = UiButtonKind.Primary, Icon = UiIcon.Locate, Opens = true });
                }
                else if (o.State == OwnedState.Kept)
                {
                    ThingDef def = o.Car ? null : HustleCatalogue.Thing(o.Thing);
                    if (def != null && def.FixMinutes > 0)
                    {
                        r.Acts.Add(new WorkAct { Kind = def.ShopFix ? ToShop : FixHome, Arg = o.Id,
                            Label = def.ShopFix ? w.T("reallife_hustle_button_plan_shop", "Take it to the repair shop") : w.T("reallife_hustle_button_plan_fix", "Mend it at home"),
                            Style = UiButtonKind.Secondary, Icon = UiIcon.Wrench });
                    }
                    if (def != null && def.CleanGain > 0 && !o.Cleaned)
                    {
                        r.Acts.Add(new WorkAct { Kind = CleanHome, Arg = o.Id, Label = w.T("reallife_hustle_button_plan_clean", "Clean it at home"),
                            Style = UiButtonKind.Secondary, Icon = UiIcon.Drop });
                    }
                    r.Acts.Add(new WorkAct { Kind = List, Arg = o.Id, Label = w.F("reallife_work_list_at", "List at {price}", "price",
                        HustleText.Money(Math.Max(1, worth))), Style = UiButtonKind.Primary, Icon = UiIcon.Tag });
                }
                t.Rows.Add(r);
            }
            return t;
        }

        private WorkTab Offers(HustleDirector d, HustleState s)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { TableTitle = w.T("reallife_hustle_tab_offers", "Offers"), Lead = w.T("reallife_work_no_offers",
                "No offers. List something of yours and buyers make offers over the day."),
                Columns = new[] { Col(w.T("reallife_work_col_thing", "Thing"), true), Col(w.T("reallife_work_col_buyer", "Buyer")),
                    Col(w.T("reallife_work_col_offer", "Offer"), false, true) } };
            for (int i = 0; i < s.Offers.Count; i++)
            {
                BuyerOffer o = s.Offers[i];
                Owned item = s.Item(o.Item);
                string name = item == null ? "-" : item.Car ? HustleText.Thing(item.Thing, true) : HustleText.Thing(item.Thing, item.Car);
                var r = new WorkRow { Id = o.Id, Icon = UiIcon.Arrows, Cells = new[] { name, o.Buyer, HustleText.Money(o.Offer) },
                    Detail = w.F("reallife_work_offer_line", "{buyer} offers {offer} for {thing}.", "buyer", o.Buyer, "offer",
                    HustleText.Money(o.Offer), "thing", name) };
                r.Acts.Add(new WorkAct { Kind = Accept, Arg = o.Id, Label = w.F("reallife_work_accept_at", "Accept {offer}", "offer",
                    HustleText.Money(o.Offer)), Style = UiButtonKind.Primary, Icon = UiIcon.Check });
                r.Acts.Add(new WorkAct { Kind = Decline, Arg = o.Id, Label = w.T("reallife_hustle_button_decline", "Turn down"),
                    Style = UiButtonKind.Destructive, Icon = UiIcon.Close });
                t.Rows.Add(r);
            }
            return t;
        }

        private WorkTab Freelance(HustleDirector d, HustleState s)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { TableTitle = w.T("reallife_hustle_tab_freelance", "Freelance"), Lead = w.T("reallife_work_no_gigs",
                "No visits asked for today. Clients ask each morning."),
                Columns = new[] { Col(w.T("reallife_work_col_client", "Client"), true), Col(w.T("reallife_work_col_job", "Job")),
                    Col(w.T("reallife_work_col_pay", "Pay"), false, true) } };
            for (int i = 0; i < s.Gigs.Count; i++)
            {
                Gig g = s.Gigs[i];
                var r = new WorkRow { Id = g.Id, Icon = UiIcon.Wrench, Cells = new[] { g.Client, HustleText.Gig(g.Kind), HustleText.Money(g.Pay) },
                    Detail = w.F("reallife_work_gig_line", "{client} asks for a visit: {job}, {pay}.", "client", g.Client, "job",
                    HustleText.Gig(g.Kind), "pay", HustleText.Money(g.Pay)) };
                if (g.Stage == GigStage.Offered)
                {
                    r.Acts.Add(new WorkAct { Kind = Book, Arg = g.Id, Label = w.T("reallife_hustle_button_book", "Book it"),
                        Style = UiButtonKind.Primary, Icon = UiIcon.Check });
                }
                if (g.Stage == GigStage.Offered || g.Stage == GigStage.Booked)
                {
                    r.Acts.Add(new WorkAct { Kind = Send, Arg = g.Id, Label = w.T("reallife_hustle_button_helper", "Send a helper"), Style = UiButtonKind.Secondary, Icon = UiIcon.People });
                }
                if (g.Stage == GigStage.Booked)
                {
                    r.Acts.Add(new WorkAct { Kind = Cancel, Arg = g.Id, Label = w.T("reallife_hustle_button_cancel", "Cancel"),
                        Style = UiButtonKind.Destructive, Icon = UiIcon.Close });
                }
                t.Rows.Add(r);
            }
            return t;
        }

        // ------------------------------------------------------------------------------------------------- doing

        public bool Do(WorkDo act, WorkView view)
        {
            HustleDirector d = HustleDirector.Instance;
            HustleState s = d != null ? d.State : null;
            if (s == null)
            {
                return false;
            }
            try
            {
                bool done = true;
                Listing l = s.ListingOf(act.Arg);
                switch (act.Kind)
                {
                    case Buy:
                        if (l == null) { return false; }
                        done = d.Buy(act.Arg, l.Asking, false);
                        break;
                    case Haggle:
                        if (l == null) { return false; }
                        d.Haggle(act.Arg, Math.Max(1, l.Asking * 9 / 10), false, out int _);
                        break;
                    case Buy + 20:
                        if (l == null) { return false; }
                        done = d.Buy(act.Arg, l.Asking, true);
                        break;
                    case Buy + 21:
                        if (l == null) { return false; }
                        d.ShowWay(l.Street, l.Number);
                        break;
                    case Accept: done = d.Accept(act.Arg); break;
                    case Decline: d.Decline(act.Arg); break;
                    case Unlist: d.Unlist(act.Arg); break;
                    case List:
                        Owned o = s.Item(act.Arg);
                        if (o == null) { return false; }
                        d.List(act.Arg, Math.Max(1, d.WorthOf(o)));
                        break;
                    case Book: d.Book(act.Arg); break;
                    case FixHome: d.PlanAtHome(act.Arg, true); break;
                    case CleanHome: d.PlanAtHome(act.Arg, false); break;
                    case ToShop: d.PlanShop(act.Arg); break;
                    case CancelPlan: d.CancelPlan(act.Arg); break;
                    case WayItem:
                        Owned way = s.Item(act.Arg);
                        if (way == null) { return false; }
                        d.ShowWay(way.Street, way.Number);
                        break;
                    case Send: d.Send(act.Arg); break;
                    case Cancel: d.Cancel(act.Arg); break;
                    default: return false;
                }
                if (done)
                {
                    _local++;
                }
                return done;
            }
            catch (Exception e)
            {
                HustleDirector.Logger?.Warn("Marketplace (kit): " + act.Kind + " failed: " + e.Message);
                return false;
            }
        }
    }
}
