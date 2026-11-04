using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Dealership;
using RealLifeOverhaul.Hustles;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Marketplace on the kit: listings as cards by category with the asking against worth, the seller as a person with a rating and "more from this
    /// seller", what to check before buying, a haggle page with an amount to offer, the player's own things, buyers' offers and freelance visits. Read from the
    /// Marketplace's own records; every button goes to the director that owns it, and buying, accepting and cancelling go through the game's question box.
    /// Nothing a frame.
    /// </summary>
    public sealed class MarketLive : IWorkSource
    {
        public static readonly MarketLive Instance = new MarketLive();

        // the actions of this app, from 100 (the screen's own are 1 to 3)
        private const int Buy = 100, Haggle = 101, Accept = 102, Counter = 103, Decline = 104, Unlist = 105, Book = 106, Cancel = 107, Send = 108,
            List = 109, FixHome = 110, CleanHome = 111, ToShop = 112, CancelPlan = 113, WayItem = 114, BuyDeliver = 120, WayListing = 121, MakeOffer = 122;
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
            HustleDirector d = HustleDirector.Instance;
            HustleState s = d != null ? d.State : null;
            if (_app.Ready && s != null)
            {
                Listing l = view.Sub == 1 ? s.ListingOf(view.Pick) : null;
                if (l != null && !l.Gone)
                {
                    _app.Tabs[0] = HaggleTab(d, s, l, view);
                }
                else
                {
                    view.Sub = 0;
                }
            }
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
            _app = new WorkApp { Ready = state != null, OffTitle = w.T("reallife_work_market_off_title", "The Marketplace is switched off"),
                OffBody = w.T("reallife_work_market_off", "Load a save with the Marketplace switched on to buy and sell.") };
            _app.Nav = new[]
            {
                new UiNavEntry { Icon = UiIcon.Tag, Label = w.T("reallife_hustle_tab_listings", "Listings"), Count = state != null && state.Listings.Count > 0 ? Alive(state).ToString(Inv) : null },
                new UiNavEntry { Icon = UiIcon.Package, Label = w.T("reallife_hustle_tab_yours", "Yours"), Count = state != null && state.Items.Count > 0 ? state.Items.Count.ToString(Inv) : null },
                new UiNavEntry { Icon = UiIcon.Arrows, Label = w.T("reallife_hustle_tab_offers", "Offers"), Count = state != null && state.Offers.Count > 0 ? state.Offers.Count.ToString(Inv) : null },
                new UiNavEntry { Icon = UiIcon.Wrench, Label = w.T("reallife_hustle_tab_freelance", "Freelance"), Count = state != null && Asked(state) > 0 ? Asked(state).ToString(Inv) : null }
            };
            if (!_app.Ready)
            {
                return;
            }
            _app.Tabs = new[] { Listings(d, state), Yours(d, state), Offers(d, state), Freelance(d, state) };
        }

        private static int Alive(HustleState s)
        {
            int n = 0;
            for (int i = 0; i < s.Listings.Count; i++)
            {
                n += s.Listings[i].Gone ? 0 : 1;
            }
            return n;
        }

        private static int Asked(HustleState s)
        {
            int n = 0;
            for (int i = 0; i < s.Gigs.Count; i++)
            {
                n += s.Gigs[i].Stage == GigStage.Offered ? 1 : 0;
            }
            return n;
        }

        private static WorkAct Act(int kind, int arg, string label, UiButtonKind style, UiIcon icon, bool opens = false, string why = null)
        {
            return new WorkAct { Kind = kind, Arg = arg, Label = label, Style = style, Icon = icon, Opens = opens, Why = why };
        }

        // ------------------------------------------------------------------------------------------------- Listings

        private WorkTab Listings(HustleDirector d, HustleState s)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Cards, Search = w.T("reallife_work_search_listings", "Search listings"), AllWord = w.T("reallife_work_filter_all_word", "All"),
                Chips = new[] { w.T("reallife_work_cat_vehicles_word", "Vehicles"), w.T("reallife_work_cat_machines_word", "Machines"), w.T("reallife_work_cat_furniture_word", "Furniture"),
                    w.T("reallife_work_cat_stock_word", "Stock") },
                EmptyTitle = w.T("reallife_work_no_listings_title", "Nothing is listed today"), EmptyBody = w.T("reallife_work_no_listings", "New things arrive each morning. Check Yours for what you can list."),
                SortWord = w.T("reallife_work_sort_newest", "Newest first") };
            var book = d.Sellers;
            var more = new List<Listing>(6);
            int level = HustleGame.Level;
            for (int i = 0; i < s.Listings.Count; i++)
            {
                Listing l = s.Listings[i];
                if (l.Gone)
                {
                    continue;
                }
                ThingDef def = l.Car ? null : HustleCatalogue.Thing(l.Thing);
                int category = SellerRules.CategoryOf(l, def);
                int worth = HustleRules.SaidWorth(l, level);
                int versus = SellerRules.VersusWorth(l.Asking, worth);
                Seller seller = l.SellerId > 0 && book != null ? book.Of(l.SellerId) : null;
                string name = HustleText.Thing(l.Thing, l.Car);
                string sellerName = seller != null ? seller.Name : l.Seller;
                bool rival = seller != null && !string.IsNullOrEmpty(seller.Rival);
                bool discount = seller != null && seller.Tag == (int)SellerTag.Discount;
                var r = new WorkRow { Id = l.Id, Icon = WorkGlyphs.Thing(l.Thing, l.Car), Name = name, Price = HustleText.Money(l.Asking), Under = worth > 0 ? WorkBuilder.Percent(versus) : null,
                    UnderValue = versus, Seller = sellerName, Group = category, Tone = UiRole.Muted,
                    Status = rival ? w.T("reallife_work_tag_rival", "Rival") : discount ? w.T("reallife_work_tag_discount_short", "Discount") : null, Title = name,
                    Sub = HustleText.Kind(l.Thing, l.Car) + ", " + HustleText.Condition(l.Shown) };
                int deliver = HustleRules.DeliveryFee(l.Asking, level);
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_asking", "Asking"), HustleText.Money(l.Asking)));
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_market_price", "Market price"), worth > 0 ? HustleText.Money(worth) : "-"));
                if (seller != null || !string.IsNullOrEmpty(l.Seller))
                {
                    var who = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_the_seller", "The seller") };
                    who.People.Add(new WorkPerson { Name = sellerName, Role = seller != null ? (rival ? seller.Rival + ", " + w.T("reallife_work_a_rival", "a rival") : w.F("reallife_work_sold_n", "{n} sold", "n", seller.Sold.ToString(Inv))) : w.T("reallife_work_seller", "Seller"),
                        Right = seller != null ? (seller.Rating / 10f).ToString("0.0", Inv) : null });
                    r.Sections.Add(who);
                }
                var facts = new WorkSection { Part = WorkPart.Pairs, Title = l.Car ? w.T("reallife_work_the_vehicle", "The vehicle") : w.T("reallife_work_the_thing", "The thing") };
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_condition", "Condition said"), HustleText.Condition(l.Shown)));
                if (l.Car)
                {
                    facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_colour", "Colour"), l.Colour ?? "-"));
                    facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_km", "Driven"), l.Km.ToString("N0", Inv) + " km"));
                    facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_price_new", "Price new"), HustleText.Money(HustleRules.AtPrices(l.Price, level))));
                }
                else
                {
                    facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_price_new", "Price new"), HustleText.Money(HustleRules.AtPrices(def != null ? def.NewPrice : l.Asking, level))));
                    facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_at", "At"), HustleText.Place(l.Street, l.Number)));
                }
                r.Sections.Add(facts);
                if (seller != null && SellerRules.MoreFrom(s.Listings, seller.Id, l.Id, more) > 0)
                {
                    var others = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_more_from", "More from this seller"), Note = w.F("reallife_work_n_listings", "{n} listings", "n", more.Count.ToString(Inv)) };
                    for (int k = 0; k < more.Count && k < 3; k++)
                    {
                        others.People.Add(new WorkPerson { Name = HustleText.Thing(more[k].Thing, more[k].Car), Role = HustleText.Kind(more[k].Thing, more[k].Car), Right = HustleText.Money(more[k].Asking),
                            Icon = WorkGlyphs.Thing(more[k].Thing, more[k].Car) });
                    }
                    r.Sections.Add(others);
                }
                bool room = s.Items.Count < HustleCatalogue.OwnedMost;
                var before = new WorkSection { Part = WorkPart.Checks, Title = w.T("reallife_work_before_buy", "Before you buy") };
                before.Checks.Add(new WorkNeed { Words = w.F("reallife_work_you_have_money", "You have the {price}", "price", HustleText.Money(l.Asking)), Met = HustleGame.Cash >= l.Asking,
                    Have = HustleText.Money(HustleGame.Cash) });
                before.Checks.Add(new WorkNeed { Words = w.T("reallife_work_room_to_keep", "Room for it among your things"), Met = room });
                if (!l.Car)
                {
                    before.Checks.Add(new WorkNeed { Words = w.F("reallife_work_delivery_adds_line", "Delivery adds {fee} and a night", "fee", HustleText.Money(deliver)), Met = HustleGame.Cash >= l.Asking + deliver });
                }
                r.Sections.Add(before);
                string noMoney = HustleGame.Cash < l.Asking ? w.T("reallife_work_no_money", "You do not have the money.") : !room ? w.T("reallife_work_too_many", "You hold as many things as you can.") : null;
                r.Acts.Add(Act(Buy, l.Id, w.F("reallife_work_buy_now", "Buy now, {price}", "price", HustleText.Money(l.Asking)), UiButtonKind.Primary, UiIcon.Check, false, noMoney));
                r.Acts.Add(Act(Haggle, l.Id, w.T("reallife_work_haggle", "Haggle"), UiButtonKind.Secondary, UiIcon.Arrows, true,
                    l.Haggles >= HustleCatalogue.HagglesMost ? w.T("reallife_work_haggled_out", "They will not haggle again.") : null));
                if (!l.Car)
                {
                    r.Acts.Add(Act(BuyDeliver, l.Id, w.F("reallife_work_buy_deliver", "Buy and deliver, {price}", "price", HustleText.Money(l.Asking + deliver)), UiButtonKind.Secondary, UiIcon.Truck, false,
                        HustleGame.Cash < l.Asking + deliver ? w.T("reallife_work_no_money", "You do not have the money.") : !room ? w.T("reallife_work_too_many", "You hold as many things as you can.") : null));
                }
                r.Acts.Add(Act(WayListing, l.Id, w.T("reallife_hustle_button_way", "Show the way"), UiButtonKind.Secondary, UiIcon.Locate, true));
                t.Rows.Add(r);
            }
            return t;
        }

        // ------------------------------------------------------------------------------------------------- the haggle

        private WorkTab HaggleTab(HustleDirector d, HustleState s, Listing l, WorkView v)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Haggle, Sub = true };
            int level = HustleGame.Level, worth = HustleRules.SaidWorth(l, level);
            Seller seller = l.SellerId > 0 && d.Sellers != null ? d.Sellers.Of(l.SellerId) : null;
            int steps = HustleRules.HaggleSteps;
            int start = Math.Max(0, steps - 3);
            int step = Math.Max(0, Math.Min(steps - 1, v.Get(0, start)));
            int offer = HustleRules.HaggleAt(l.Asking, step);
            t.Amounts.Add(new WorkAmount { Id = 0, Value = step, Min = 0, Max = steps - 1, Figure = HustleText.Money(offer) });
            string name = HustleText.Thing(l.Thing, l.Car);
            int left = Math.Max(0, HustleCatalogue.HagglesMost - l.Haggles);
            int versus = SellerRules.VersusWorth(l.Asking, worth);
            var r = new WorkRow { Id = l.Id, Name = name, Kicker = w.T("reallife_work_haggle_kicker", "Haggle with the seller"), Seller = seller != null ? seller.Name : l.Seller,
                Line = seller != null ? (string.IsNullOrEmpty(seller.Rival) ? w.F("reallife_work_rated", "rated {rating}", "rating", (seller.Rating / 10f).ToString("0.0", Inv))
                    : seller.Rival + ", " + w.F("reallife_work_rated", "rated {rating}", "rating", (seller.Rating / 10f).ToString("0.0", Inv))) : string.Empty,
                Status = seller != null && seller.Tag == (int)SellerTag.Discount ? w.T("reallife_work_tag_discount", "Sells at a discount") : null, Tone = UiRole.Muted,
                Sentence = w.F("reallife_work_haggle_line", "{seller} asks {asking} for the {thing}, {versus} against its market price. You may make up to {n} offers, and each one they turn down makes the next harder.",
                    "seller", seller != null ? seller.Name : l.Seller, "asking", HustleText.Money(l.Asking), "thing", name, "versus", worth > 0 ? WorkBuilder.Percent(versus) : "-", "n", HustleCatalogue.HagglesMost.ToString(Inv)),
                Hint = w.T("reallife_work_your_offer", "Your offer") };
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_their_price", "Their price"), HustleText.Money(l.Asking)));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_market_price", "Market price"), worth > 0 ? HustleText.Money(worth) : "-"));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_offers_left", "Offers left"), w.F("reallife_work_n_of_m", "{n} of {m}", "n", left.ToString(Inv), "m", HustleCatalogue.HagglesMost.ToString(Inv))));
            if (l.Countered > 0)
            {
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_their_counter", "Their last counter"), HustleText.Money(l.Countered)));
            }
            string noMoney = HustleGame.Cash < offer ? w.T("reallife_work_no_money", "You do not have the money.") : s.Items.Count >= HustleCatalogue.OwnedMost ? w.T("reallife_work_too_many", "You hold as many things as you can.") : null;
            r.Acts.Add(new WorkAct { Kind = MakeOffer, Arg = l.Id, Label = w.F("reallife_work_make_offer", "Offer {amount}", "amount", HustleText.Money(offer)), Style = UiButtonKind.Primary, Icon = UiIcon.Check,
                Note = w.T("reallife_work_offer_note", "They may accept, counter or turn it down."), Choice = true, Why = noMoney });
            r.Acts.Add(new WorkAct { Kind = Buy, Arg = l.Id, Label = w.T("reallife_work_pay_asking", "Pay the asking price"), Style = UiButtonKind.Secondary, Icon = UiIcon.Coin,
                Note = w.F("reallife_work_pay_asking_note", "Buy now at {price}, no more haggling.", "price", HustleText.Money(l.Asking)), Choice = true,
                Why = HustleGame.Cash < l.Asking ? w.T("reallife_work_no_money", "You do not have the money.") : null });
            r.Acts.Add(new WorkAct { Kind = WorkKinds.Back, Arg = l.Id, Label = w.T("reallife_work_walk_away_haggle", "Walk away"), Style = UiButtonKind.Destructive, Icon = UiIcon.Back, Opens = true,
                Note = w.F("reallife_work_walk_note", "Back to the listings. The {thing} stays for sale today.", "thing", name), Choice = true });
            t.Haggle = r;
            return t;
        }

        // ------------------------------------------------------------------------------------------------- Yours

        private static string StateOf(Owned o)
        {
            switch (o.State)
            {
                case OwnedState.Waiting: return HustleText.T("reallife_hustle_state_to_collect_by", "day", o.Until.ToString(Inv));
                case OwnedState.Delivering: return HustleText.T("reallife_hustle_state_delivering");
                case OwnedState.Listed: return HustleText.T("reallife_hustle_state_listed");
            }
            switch (o.Plan)
            {
                case OwnedPlan.FixAtHome: return HustleText.T("reallife_hustle_state_to_fix");
                case OwnedPlan.CleanAtHome: return HustleText.T("reallife_hustle_state_to_clean");
                case OwnedPlan.Shop: return HustleText.T("reallife_hustle_state_to_shop");
                default: return HustleText.T("reallife_hustle_state_kept");
            }
        }

        private WorkTab Yours(HustleDirector d, HustleState s)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Table, Search = w.T("reallife_work_search_yours", "Search your things") };
            t.Columns = new[] { WorkBuilder.Col(w.T("reallife_work_col_thing", "Thing"), UiCellKind.Tile), WorkBuilder.Col(w.T("reallife_work_col_state", "State"), UiCellKind.Chip, 152),
                WorkBuilder.Col(w.T("reallife_work_col_paid", "Paid"), UiCellKind.Money, 88), WorkBuilder.Col(w.T("reallife_work_col_worth", "Worth now"), UiCellKind.Money, 96) };
            t.EmptyTitle = w.T("reallife_work_no_yours_title", "You own nothing from here");
            t.EmptyBody = w.T("reallife_work_no_yours", "What you buy comes here to fix, clean, list or keep.");
            t.EmptyIcon = UiIcon.Package;
            for (int i = 0; i < s.Items.Count; i++)
            {
                Owned o = s.Items[i];
                int worth = d.WorthOf(o);
                string name = HustleText.Thing(o.Thing, o.Car);
                string state = StateOf(o);
                UiRole tone = o.State == OwnedState.Listed ? UiRole.Ok : o.State == OwnedState.Waiting ? UiRole.Warn : o.State == OwnedState.Delivering ? UiRole.Accent : UiRole.Muted;
                var r = new WorkRow { Id = o.Id, Icon = WorkGlyphs.Thing(o.Thing, o.Car), Name = name, Sub = w.F("reallife_work_bought_day", "Bought {day}", "day", FinanceText.Day(o.Bought)),
                    Cells = new[] { null, state, HustleText.Money(o.Paid), HustleText.Money(worth) }, Status = state, Tone = tone, Chip = state, ChipTone = tone };
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_paid", "Paid"), HustleText.Money(o.Paid)));
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_worth", "Worth now"), HustleText.Money(worth)));
                if (o.Spent > 0)
                {
                    r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_spent_on", "Spent on it"), HustleText.Money(o.Spent)));
                }
                if (o.State == OwnedState.Listed)
                {
                    r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_listed_at", "Listed at"), HustleText.Money(o.Asking)));
                    r.Acts.Add(Act(Unlist, o.Id, w.T("reallife_hustle_button_unlist", "Take it off sale"), UiButtonKind.Secondary, UiIcon.Close));
                }
                else if (o.State == OwnedState.Kept && o.Plan != OwnedPlan.None)
                {
                    r.Acts.Add(Act(CancelPlan, o.Id, w.T("reallife_hustle_button_cancel_plan", "Not now"), UiButtonKind.Secondary, UiIcon.Close));
                    r.Acts.Add(Act(WayItem, o.Id, w.T("reallife_hustle_button_way", "Show the way"), UiButtonKind.Secondary, UiIcon.Locate, true));
                }
                else if (o.State == OwnedState.Waiting)
                {
                    r.Acts.Add(Act(WayItem, o.Id, w.T("reallife_hustle_button_way", "Show the way"), UiButtonKind.Primary, UiIcon.Locate, true));
                }
                else if (o.State == OwnedState.Kept)
                {
                    ThingDef def = o.Car ? null : HustleCatalogue.Thing(o.Thing);
                    r.Acts.Add(Act(List, o.Id, w.F("reallife_work_list_at", "List at {price}", "price", HustleText.Money(Math.Max(1, worth))), UiButtonKind.Primary, UiIcon.Tag));
                    if (def != null && def.FixMinutes > 0)
                    {
                        r.Acts.Add(Act(def.ShopFix ? ToShop : FixHome, o.Id, def.ShopFix ? w.T("reallife_hustle_button_plan_shop", "Take it to a repair shop") : w.T("reallife_hustle_button_plan_fix", "Fix it at home"),
                            UiButtonKind.Secondary, UiIcon.Wrench));
                    }
                    if (def != null && def.CleanGain > 0 && !o.Cleaned)
                    {
                        r.Acts.Add(Act(CleanHome, o.Id, w.T("reallife_hustle_button_plan_clean", "Clean it at home"), UiButtonKind.Secondary, UiIcon.Drop));
                    }
                }
                t.Rows.Add(r);
            }
            return t;
        }

        // ------------------------------------------------------------------------------------------------- Offers

        private WorkTab Offers(HustleDirector d, HustleState s)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Table };
            t.Columns = new[] { WorkBuilder.Col(w.T("reallife_work_col_thing", "Thing"), UiCellKind.Tile), WorkBuilder.Col(w.T("reallife_work_col_buyer", "Buyer"), UiCellKind.Person, 152),
                WorkBuilder.Col(w.T("reallife_work_col_asking", "Asking"), UiCellKind.Money, 88), WorkBuilder.Col(w.T("reallife_work_col_offer", "Offer"), UiCellKind.Money, 88),
                WorkBuilder.Col(w.T("reallife_work_col_against_ask", "Against asking"), UiCellKind.Trend, 104) };
            t.EmptyTitle = w.T("reallife_work_no_offers_title", "No offers");
            t.EmptyBody = w.T("reallife_work_no_offers", "List something of yours and buyers make offers over the day.");
            t.EmptyIcon = UiIcon.Arrows;
            for (int i = 0; i < s.Offers.Count; i++)
            {
                BuyerOffer o = s.Offers[i];
                Owned item = s.Item(o.Item);
                string name = item == null ? "-" : HustleText.Thing(item.Thing, item.Car);
                int asking = item != null ? item.Asking : o.Offer;
                int pct = asking > 0 ? (int)Math.Round((o.Offer - asking) * 100.0 / asking) : 0;
                var r = new WorkRow { Id = o.Id, Icon = item != null ? WorkGlyphs.Thing(item.Thing, item.Car) : UiIcon.Package, Name = name, Sub = w.F("reallife_work_listed_at_line", "Listed at {price}", "price", HustleText.Money(asking)),
                    Cells = new[] { null, o.Buyer, HustleText.Money(asking), HustleText.Money(o.Offer), WorkBuilder.Percent(pct) }, Values = new[] { 0, 0, 0, 0, pct < 0 ? -1 : pct > 0 ? 1 : 0 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, pct < 0 ? UiRole.Danger : UiRole.Ok } };
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_offer", "Offer"), HustleText.Money(o.Offer)));
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_asking", "Asking"), HustleText.Money(asking)));
                var who = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_the_buyer", "The buyer") };
                who.People.Add(new WorkPerson { Name = o.Buyer, Role = w.F("reallife_work_buyer_counters", "{n} counters so far", "n", o.Counters.ToString(Inv)) });
                r.Sections.Add(who);
                r.Acts.Add(Act(Accept, o.Id, w.F("reallife_work_accept_at", "Accept {offer}", "offer", HustleText.Money(o.Offer)), UiButtonKind.Primary, UiIcon.Check));
                r.Acts.Add(Act(Counter, o.Id, w.T("reallife_hustle_button_counter", "Counter"), UiButtonKind.Secondary, UiIcon.Arrows, false,
                    item == null || item.Asking <= o.Offer ? w.T("reallife_work_offer_is_asking", "Their offer is already your asking price.") : null));
                r.Acts.Add(Act(Decline, o.Id, w.T("reallife_hustle_button_decline", "Turn it down"), UiButtonKind.Destructive, UiIcon.Close));
                t.Rows.Add(r);
            }
            return t;
        }

        // ------------------------------------------------------------------------------------------------- Freelance

        private WorkTab Freelance(HustleDirector d, HustleState s)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Table };
            t.Columns = new[] { WorkBuilder.Col(w.T("reallife_work_col_client", "Client"), UiCellKind.Person), WorkBuilder.Col(w.T("reallife_work_col_job", "Job"), UiCellKind.Text, 152),
                WorkBuilder.Col(w.T("reallife_work_col_when", "When"), UiCellKind.Text, 128), WorkBuilder.Col(w.T("reallife_work_col_pay", "Pay"), UiCellKind.Money, 80),
                WorkBuilder.Col(w.T("reallife_work_col_state", "State"), UiCellKind.Chip, 128) };
            t.EmptyTitle = w.T("reallife_work_no_gigs_title", "No visit is asked for today");
            t.EmptyBody = w.T("reallife_work_no_gigs", "Clients ask each morning for detailing, handyman jobs and moves.");
            t.EmptyIcon = UiIcon.Wrench;
            for (int i = 0; i < s.Gigs.Count; i++)
            {
                Gig g = s.Gigs[i];
                if (g.Stage == GigStage.Done || g.Stage == GigStage.Missed || g.Stage == GigStage.Cancelled)
                {
                    continue;
                }
                string state = g.Stage == GigStage.Booked ? HustleText.T("reallife_hustle_state_booked") : g.Stage == GigStage.Helper ? HustleText.T("reallife_hustle_state_helper", "time", HustleText.Clock(g.HelperDone))
                    : g.Stage == GigStage.Loaded ? HustleText.T("reallife_hustle_state_loaded") : HustleText.T("reallife_hustle_state_asked");
                UiRole tone = g.Stage == GigStage.Offered ? UiRole.Accent : UiRole.Ok;
                string when = FinanceText.Day(g.From / 24) + " " + HustleText.Clock(g.From);
                var r = new WorkRow { Id = g.Id, Person = g.Client, Name = g.Client, Sub = HustleText.Place(g.Street, g.Number), Ring = UiRole.Ok,
                    Cells = new[] { null, HustleText.Gig(g.Kind), when, HustleText.Money(g.Pay), state }, Status = state, Tone = tone, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone } };
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_pay", "Pay"), HustleText.Money(g.Pay)));
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_when", "When"), when));
                if (g.Stage == GigStage.Offered)
                {
                    r.Acts.Add(Act(Book, g.Id, w.T("reallife_hustle_button_book", "Book it"), UiButtonKind.Primary, UiIcon.Check));
                }
                if (g.Stage == GigStage.Offered || g.Stage == GigStage.Booked)
                {
                    r.Acts.Add(Act(Send, g.Id, w.T("reallife_hustle_button_helper", "Send a helper"), UiButtonKind.Secondary, UiIcon.People));
                }
                if (g.Stage == GigStage.Booked)
                {
                    r.Acts.Add(Act(Cancel, g.Id, w.T("reallife_hustle_button_cancel", "Call it off"), UiButtonKind.Destructive, UiIcon.Close));
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
                bool done = Carry(act, d, s, view);
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

        private bool Carry(WorkDo act, HustleDirector d, HustleState s, WorkView view)
        {
            Listing l = s.ListingOf(act.Arg);
            switch (act.Kind)
            {
                case Buy:
                case BuyDeliver:
                {
                    if (l == null)
                    {
                        return false;
                    }
                    bool deliver = act.Kind == BuyDeliver;
                    int id = l.Id, price = l.Asking, fee = deliver ? HustleRules.DeliveryFee(price, HustleGame.Level) : 0;
                    string thing = HustleText.Thing(l.Thing, l.Car), seller = l.Seller;
                    GameConfirm.Show("reallife_work_confirm_buy_title".Localize(new { thing }),
                        (deliver ? "reallife_work_confirm_buy_deliver_body" : "reallife_work_confirm_buy_body").Localize(new { thing, seller, price = HustleText.Money(price + fee), cash = HustleText.Money(HustleGame.Cash - price - fee) }),
                        () =>
                        {
                            if (d.Buy(id, price, deliver))
                            {
                                view.Sub = 0;
                                _local++;
                            }
                        }, null, null, null, true, false);
                    return true;
                }
                case Haggle:
                    if (l == null)
                    {
                        return false;
                    }
                    view.Sub = 1;
                    view.Pick = l.Id;
                    view.Forget(0);
                    return true;
                case MakeOffer:
                {
                    if (l == null)
                    {
                        return false;
                    }
                    int offer = HustleRules.HaggleAt(l.Asking, view.Get(0, Math.Max(0, HustleRules.HaggleSteps - 3)));
                    HaggleAnswer answer = d.Haggle(l.Id, offer, false, out int _);
                    if (answer != HaggleAnswer.Countered)
                    {
                        view.Sub = 0;
                    }
                    else
                    {
                        view.Forget(0);
                    }
                    return true;
                }
                case WayListing:
                    if (l == null)
                    {
                        return false;
                    }
                    d.ShowWay(l.Street, l.Number);
                    return true;
                case Accept:
                {
                    BuyerOffer o = s.OfferOf(act.Arg);
                    Owned item = o != null ? s.Item(o.Item) : null;
                    if (o == null || item == null)
                    {
                        return false;
                    }
                    int id = o.Id;
                    string thing = HustleText.Thing(item.Thing, item.Car), buyer = o.Buyer;
                    GameConfirm.Show("reallife_work_confirm_accept_title".Localize(new { thing }),
                        "reallife_work_confirm_accept_body".Localize(new { thing, buyer, offer = HustleText.Money(o.Offer), paid = HustleText.Money(item.Paid + item.Spent) }),
                        () =>
                        {
                            d.Accept(id);
                            _local++;
                        }, null, null, null, true, false);
                    return true;
                }
                case Counter:
                {
                    BuyerOffer o = s.OfferOf(act.Arg);
                    Owned item = o != null ? s.Item(o.Item) : null;
                    if (o == null || item == null)
                    {
                        return false;
                    }
                    d.Counter(o.Id, item.Asking, out bool _);
                    return true;
                }
                case Decline: d.Decline(act.Arg); return true;
                case Unlist: d.Unlist(act.Arg); return true;
                case List:
                {
                    Owned o = s.Item(act.Arg);
                    if (o == null)
                    {
                        return false;
                    }
                    d.List(act.Arg, Math.Max(1, d.WorthOf(o)));
                    return true;
                }
                case Book: d.Book(act.Arg); return true;
                case FixHome: d.PlanAtHome(act.Arg, true); return true;
                case CleanHome: d.PlanAtHome(act.Arg, false); return true;
                case ToShop: d.PlanShop(act.Arg); return true;
                case CancelPlan: d.CancelPlan(act.Arg); return true;
                case WayItem:
                {
                    Owned way = s.Item(act.Arg);
                    if (way == null)
                    {
                        return false;
                    }
                    d.ShowWay(way.Street, way.Number);
                    return true;
                }
                case Send: d.Send(act.Arg); return true;
                case Cancel:
                {
                    Gig g = s.GigOf(act.Arg);
                    if (g == null)
                    {
                        return false;
                    }
                    int id = g.Id;
                    string client = g.Client;
                    GameConfirm.Show("reallife_work_confirm_cancel_title".Localize(new { client }), "reallife_work_confirm_cancel_body".Localize(new { client, job = HustleText.Gig(g.Kind) }),
                        () =>
                        {
                            d.Cancel(id);
                            _local++;
                        }, null, null, null, true, true);
                    return true;
                }
                default: return false;
            }
        }
    }
}
