using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Seasons;
using RealLifeOverhaul.Suppliers;

// The logic review's findings on Suppliers and contracts (run with --suppliers, with SupplierChecks.cs): each check drives the shipped rules and the shipped engine through
// a world of the checker's own, and each was made to fail once by breaking the rule it holds (the fix checklist in the commit's notes quotes the lines).
internal static partial class Program
{
    private static void SupplierFixChecks()
    {
        SupplierListPriceChecks();
        SupplierRivalBaselineChecks();
        SupplierComingChecks();
        SupplierGateChecks();
        SupplierWeatherChecks();
        SupplierSettlementChecks();
        SupplierRulesChecks();
        SupplierReaderChecks();
        SupplierRankingChecks();
        SupplierKnewChecks();
        SupplierTimeSkipChecks();
        SupplierAtomicChecks();
        SupplierEntryChecks();
        SupplierCostChecks();
        SupplierRenewedRulesChecks();
    }

    // ---- 7. a renewed contract runs by its buyer's rules from its first judgement

    private static void SupplierRenewedRulesChecks()
    {
        var w0 = new SupplyWorld();
        w0.Sold["apples"] = w0.Sold["bread"] = 2400;
        int seed = -1;
        for (int candidate = 1; candidate < 40000 && seed < 0; candidate++)
        {
            var probe = new SupplierState { Seed = candidate };
            var none = new int[SupplierCatalogue.Kinds];
            Judged next = SupplierRules.Judge(candidate, SupplierCatalogue.Firm(1), probe.Firm(1), 109, 3, w0.Conditions(109), none);
            seed = WholeOn(probe, w0, 1, 101, 3) && WholeOn(probe, w0, 1, 108, 3) && next.Out == Arrival.Short && SupplierRules.Theirs(next.Why) ? candidate : -1;
        }
        Check(seed > 0, "suppliers buyer: a seed is found under which a renewed contract's first delivery will come short (" + seed + ")");
        var w = new SupplyWorld();
        w.Buyers.Add(SupplyShop);
        w.Sold["apples"] = w.Sold["bread"] = 2400;
        var s = new SupplierState { Seed = seed };
        Contract c = SupplierEngine.Sign(s, w, 1, SupplyShop, "Shop", 2, 2, out _);
        c.Renew = RenewRule.Same;
        c.RenewLimit = 4;
        c.Cover = true;
        c.LeaveRule = true;
        RunTo(s, w, 108);
        Contract renewed = s.Contracts.FirstOrDefault(x => x.Running && !x.Covering);
        Contract cover = s.Contracts.FirstOrDefault(x => x.Covering);
        Check(renewed != null && renewed.Due == 109 && renewed.Out == Arrival.Short && renewed.Cover && renewed.LeaveRule && cover != null && cover.Supplier != 1 && CountEvents(s, SupplyEventKind.Covered) == 1,
            "suppliers buyer: a renewal whose first delivery is tomorrow and comes short is covered at once, its rules having come in before it was judged");
        Check(cover != null && !cover.Cover && cover.Covering && cover.Out != Arrival.Short, "suppliers buyer: and the cover is never covered itself, even though its own first delivery is tomorrow too");
        string engine = ModSource(@"Suppliers\SupplierEngine.cs");
        Check(engine.Contains("Make(s, w, BestQuote, c.Place, c.PlaceName, c)") && engine.Contains("Make(s, w, BestQuote, c.Place, c.PlaceName, null, true)") && !engine.Contains("made.Cover = ") && !engine.Contains("made.Covering = "),
            "suppliers buyer: a renewal, a switch and a cover are made with their rules in the making, none set after");
    }

    // ---- 1. a supplier's price is not the importers' roll of the day

    // The offers a supplier makes for each term, over days on which the game's importers re-roll their price index of every product (0.5 to 1.3), as the lowest and highest
    // unit price of any line, per mille apart.
    private static long OfferSpread(bool leak, out long lowest)
    {
        var rng = new Random(77);
        var w = new SupplyWorld();
        w.LeakIndex = leak || w.LeakIndex;   // (the world's own default is not to leak)
        SupplierState s = CalmState();
        var q = new Quote();
        var low = new Dictionary<string, long>();
        var high = new Dictionary<string, long>();
        lowest = long.MaxValue;
        for (int day = 0; day < 400; day++)
        {
            for (int k = 0; k < w.Index.Length; k++)
            {
                w.Index[k] = 0.5 + rng.NextDouble() * 0.8;
            }
            foreach (int id in new[] { 1, 2, 3 })
            {
                foreach (int weeks in new[] { 0, 2, 4, 8 })
                {
                    SupplierEngine.Offer(s, w, id, SupplyShop, weeks, 4, q);
                    foreach (ContractLine l in q.Lines)
                    {
                        string key = id + "/" + weeks + "/" + l.Item;
                        low[key] = low.TryGetValue(key, out long a) ? Math.Min(a, l.Mills) : l.Mills;
                        high[key] = high.TryGetValue(key, out long b) ? Math.Max(b, l.Mills) : l.Mills;
                    }
                }
            }
        }
        long spread = 0;
        foreach (KeyValuePair<string, long> pair in low)
        {
            spread = Math.Max(spread, (high[pair.Key] - pair.Value) * 1000 / Math.Max(1, pair.Value));
            lowest = Math.Min(lowest, pair.Value);
        }
        return spread;
    }

    private static void SupplierListPriceChecks()
    {
        Check(SupplierRules.ListMills(1.25f, 1f) == 1250 && SupplierRules.ListMills(1.25f, 1.1f) == 1375 && SupplierRules.ListMills(1.25f, 1f, 1.5f) == 1875 && SupplierRules.ListMills(0f, 1f) == 0
              && SupplierRules.ListMills(-2f, 1f) == 0, "suppliers price: a list price is the item's wholesale price, the game's multiplier and the whole market's event, in mills, and nothing for no price");
        long spread = OfferSpread(false, out long lowest);
        Check(spread == 0 && lowest > 0, "suppliers price: over four hundred days of the importers' price index re-rolled between 0.5 and 1.3, every supplier's one-off and fixed prices stay as they were (spread " + spread + " per mille)");
        long leaking = OfferSpread(true, out _);
        Check(leaking > 300, "suppliers price: the same days with a price that took the importers' index would have moved prices by " + leaking + " per mille: the check above would fail on it");
        // A price fixed on the lowest day of the roll is the price fixed on any other: it cannot be locked at a momentary low.
        var calm = new SupplyWorld();
        var low = new SupplyWorld { LeakIndex = false };
        low.Index[0] = low.Index[1] = 0.5;
        var q1 = new Quote();
        var q2 = new Quote();
        SupplierEngine.Offer(CalmState(), calm, 1, SupplyShop, 8, 2, q1);
        SupplierEngine.Offer(CalmState(), low, 1, SupplyShop, 8, 2, q2);
        Check(q1.Lines.Count == 2 && q1.Lines.Zip(q2.Lines, (x, y) => x.Mills == y.Mills && x.Item == y.Item).All(b => b), "suppliers price: a fixed price written on the importers' lowest day of the roll is the price written on a day it stood at one");
        string game = ModSource(@"Suppliers\SupplierGame.cs");
        string basis = game.Substring(game.IndexOf("public static long BaseOf(string item)", StringComparison.Ordinal), 900);
        Check(basis.Contains("SupplierRules.ListMills(it.wholesalePrice, Multiplier)") && basis.Contains("SupplierRules.ListMills(it.wholesalePrice, Multiplier, ProductMarketHelper.GetProductMarketEventMultiplier(item))")
              && !basis.Contains("GetWholesalePrice"), "suppliers price: the game side prices from the item's own wholesale price and never from the game's GetWholesalePrice(), which holds the importers' daily index");
    }

    // ---- 2. a rival shop is charged against the price its day was valued at

    private static void SupplierRivalBaselineChecks()
    {
        var fair = new CityDay { Season = 1 };
        var none = new int[6];
        SupplierState s = CalmState(31);
        long goods = 100000L * SupplierCatalogue.RivalGoodsShare / 1000;
        bool zero = true;
        foreach (int factor in new[] { 700, 1000, 1300, 1500 })
        {
            foreach (SupplierDef d in SupplierCatalogue.Firms.Where(f => f.Trades(1)))
            {
                s.Firm(d.Id).ClosedDay = 5;   // none open: the shop buys at the wholesale stores, the very price its day was valued at
            }
            zero &= SupplierRules.Rival(31, 17, 1, s, 200, factor, fair, none, 100000).Cents == 0;
        }
        Check(zero, "suppliers rivals: a shop that buys at the wholesale stores pays what its day was valued at whatever the market's push, and the supply side takes nothing off it");
        s = CalmState(31);
        bool spotOnly = true, contractGain = true;
        int spots = 0, contracts = 0;
        for (int shop = 1; shop <= 600 && (spots < 80 || contracts < 80); shop++)
        {
            RivalDay r = SupplierRules.Rival(31, shop, 1, s, 3, 1400, fair, none, 100000);
            if (r.Failed || r.Firm == 0)
            {
                continue;
            }
            SupplierDef def = SupplierCatalogue.Firm(r.Firm);
            int level = SupplierRules.Level(def, s.Firm(r.Firm));
            if (!r.Contract)
            {
                spotOnly &= Math.Abs(r.Cents - goods * (level - 1000) / 1000) <= 2;   // the supplier's own level against the wholesale price, and no push
                spots++;
            }
            else
            {
                contractGain &= r.Cents < 0;   // written at the book (calm) it is under the market's price of 1400
                contracts++;
            }
        }
        Check(spots >= 80 && spotOnly, "suppliers rivals: a shop on the day's price in a shortage is charged only its supplier's level against the wholesale price, never the market's push again (" + spots + " shops)");
        Check(contracts >= 80 && contractGain, "suppliers rivals: a shop on a fixed price in a shortage gains the difference between the market it was valued at and the book it pays (" + contracts + " shops)");
        long tycoon = SupplierRules.Rival(31, 17, 1, s, 200, 1400, fair, none, 100000, 1000).Cents;
        foreach (SupplierDef d in SupplierCatalogue.Firms.Where(f => f.Trades(1)))
        {
            s.Firm(d.Id).ClosedDay = 5;
        }
        long wholesaleOutlet = SupplierRules.Rival(31, 17, 1, s, 200, 1100, fair, none, 100000, 1000).Cents;
        Check(wholesaleOutlet == goods * 100 / 1000 && tycoon != 0, "suppliers rivals: a tycoon's outlet, whose day is valued at calm prices, still pays the market's push (" + wholesaleOutlet + " cents on " + goods + " of goods)");
    }

    // ---- 3. what a manager's order counts as coming

    private static void SupplierComingChecks()
    {
        SupplierState Make(Func<Contract, Contract> tweak, int supplier = 1)
        {
            var st = new SupplierState();
            var c = new Contract { Id = 1, Supplier = supplier, Place = SupplyShop, Weeks = 4, Due = 50 };
            c.Lines.Add(new ContractLine { Item = "apples", Kind = 0, Units = 100, Mills = 1000 });
            st.Contracts.Add(tweak(c));
            return st;
        }
        long plenty = long.MaxValue / 4;
        Check(Make(c => c).Coming(SupplyShop, "apples", 50, 71, false, plenty) == 400, "suppliers coming: four whole deliveries of a term are four deliveries coming");
        Check(Make(c => { c.Out = Arrival.Short; c.OutShare = 600; c.OutKind = -1; return c; }).Coming(SupplyShop, "apples", 50, 50, false, plenty) == 60
              && Make(c => { c.Out = Arrival.Short; c.OutShare = 600; c.OutKind = -1; return c; }).Coming(SupplyShop, "apples", 50, 71, false, plenty) == 360,
            "suppliers coming: a delivery judged short counts only the share it will bring, and the weeks after it count whole");
        Check(Make(c => c).Coming(SupplyShop, "apples", 50, 71, true, plenty) == 0, "suppliers coming: nothing of a product the game has on backorder is expected");
        SupplierState gone = Make(c => c);
        gone.Firm(1).ClosedDay = 20;
        Check(gone.Coming(SupplyShop, "apples", 50, 71, false, plenty) == 0, "suppliers coming: nothing from a supplier that has gone under is expected");
        long value = SupplierEngine.Value(Make(c => c, 2).Contracts[0]);
        Check(Make(c => c, 2).Coming(SupplyShop, "apples", 50, 71, false, value - 1) == 0 && Make(c => c, 2).Coming(SupplyShop, "apples", 50, 71, false, value) == 400
              && Make(c => c, 1).Coming(SupplyShop, "apples", 50, 71, false, 0) == 400, "suppliers coming: a strict supplier is expected to bring only what the cash in hand pays for, a lenient one on account whatever the cash");
        // A short delivery and its cover together are the whole of what was meant to come, counted once.
        SupplierState both = Make(c => { c.Out = Arrival.Short; c.OutShare = 600; c.OutKind = -1; return c; });
        var cover = new Contract { Id = 2, Supplier = 3, Place = SupplyShop, Weeks = 0, Due = 50, Covering = true };
        cover.Lines.Add(new ContractLine { Item = "apples", Kind = 0, Units = 40, Mills = 1000 });
        both.Contracts.Add(cover);
        Check(both.Coming(SupplyShop, "apples", 50, 50, false, plenty) == 100, "suppliers coming: a short delivery and the cover bought for what it left missing together count the units meant to come, once");
    }

    // ---- 4. one gate for the buyer's orders

    private sealed class Renewal
    {
        public SupplyWorld W;
        public SupplierState S;
        public Contract C;
        public long Value;
    }

    // A two week contract with supplier 1 that renews by the buyer's rule on day 108, up to the point before its last delivery is made. Returns the world and the state.
    private static Renewal ReadyToRenew(Action<Renewal> tweak, int limit = 4)
    {
        var r = new Renewal { W = new SupplyWorld() };
        r.S = new SupplierState { Seed = QuietSeed(r.W, 1, 101, 6, 3) };
        r.W.Buyers.Add(SupplyShop);
        r.C = SupplierEngine.Sign(r.S, r.W, 1, SupplyShop, "Shop", 2, 2, out _);
        r.C.Renew = RenewRule.Same;
        r.C.RenewLimit = limit;
        r.C.Cover = true;
        r.Value = SupplierEngine.Value(r.C);
        tweak?.Invoke(r);
        RunTo(r.S, r.W, 108);
        return r;
    }

    private static void SupplierGateChecks()
    {
        Renewal open = ReadyToRenew(null);
        Contract renewed = open.S.Contracts.FirstOrDefault(x => x.Running);
        Check(renewed != null && CountEvents(open.S, SupplyEventKind.Renewed) == 1 && open.W.Spends >= 1 && open.W.PaidOverCash == 0, "suppliers gate: with cash, a decider and a budget the buyer renews, and the budget was asked");
        Renewal tight = ReadyToRenew(r => r.W.CashCents = r.Value * 2 + r.Value / 3);
        Check(tight.S.RunningCount() == 0 && tight.S.Events.Any(e => e.Kind == SupplyEventKind.NoCover && e.A == 1) && tight.W.Spends == 0 && tight.W.PaidOverCash == 0,
            "suppliers gate: a renewal whose first delivery the cash left after the deliveries still to pay cannot cover is not made, and the customer is told");
        Renewal reserved = ReadyToRenew(r => r.W.Reserve = r.W.CashCents - (r.Value * 2 + r.Value / 3));
        Check(reserved.S.RunningCount() == 0 && reserved.W.Spends == 0 && reserved.W.Pays.All(p => p <= reserved.Value) && reserved.W.CashCents >= reserved.W.Reserve,
            "suppliers gate: cash that is the investors' floor is not cash a buyer may promise or a payment may take (" + reserved.W.CashCents + " left over a floor of " + reserved.W.Reserve + ")");
        Renewal sick = ReadyToRenew(r => r.W.Deciding = false);
        Check(sick.S.RunningCount() == 0 && sick.W.Spends == 0 && CountEvents(sick.S, SupplyEventKind.Renewed) == 0 && sick.S.Events.Any(e => e.Kind == SupplyEventKind.Ending),
            "suppliers gate: with the manager off sick and nobody to stand in nothing is renewed that day, and the end of the term is told to the player");
        Renewal poor = ReadyToRenew(r => r.W.BudgetCents = r.Value / 100 - 1);
        Check(poor.S.RunningCount() == 0 && poor.W.Spends >= 1 && poor.W.BudgetCents == poor.Value / 100 - 1, "suppliers gate: a first delivery over the manager's budget is not ordered, and nothing of the budget is spent");
        Renewal budget = ReadyToRenew(r => r.W.BudgetCents = r.Value / 100 + 100000);
        Contract bought = budget.S.Contracts.FirstOrDefault(x => x.Running);
        Check(bought != null && budget.W.BudgetCents == budget.Value / 100 + 100000 - (SupplierEngine.Value(bought) + 99) / 100, "suppliers gate: an order inside the budget spends exactly its first delivery from it");
        string links = ModSource(@"Wiring\Wiring.SupplierLinks.cs"), company = ModSource(@"Economy\Company\CompanyDirector.SupplyBuyer.cs");
        Check(links.Contains("company.DecidesStock(CompanyGame.Place(reg))") && links.Contains("company.SpendStock(CompanyGame.Place(reg), money)") && !links.Contains("company.Manages(")
              && company.Contains("Manages(place, ManagerTask.Stock)") && company.Contains("Decider(mp, out _, out _, out _)") && company.Contains("ManagerRules.Left(BudgetOf(m), m.Spent)") && company.Contains("m.Spent += (int)money;"),
            "suppliers gate: the buyer is the managers' own decider (a manager off sick with nobody to stand in is nobody) and its budget is the managers' own month's budget");
        // The price limit holds for the switch after the right to leave and for a cover, as it does for a renewal.
        foreach (int limit in new[] { 2, 4 })
        {
            var w7 = new SupplyWorld { EventMille = 700 };   // the whole market is cheap today: a fixed price is over it
            w7.Buyers.Add(SupplyShop);
            w7.Sold["apples"] = w7.Sold["bread"] = 2400;
            SupplierState s7 = CalmState();
            Contract c7 = SupplierEngine.Sign(s7, w7, 1, SupplyShop, "Shop", 8, 2, out _);
            c7.LeaveRule = true;
            c7.RenewLimit = limit;
            c7.Failures = SupplierCatalogue.FailuresToLeave;
            w7.DayNow++;
            SupplierEngine.Day(s7, w7);
            Check(c7.State == ContractState.Left && (s7.RunningCount() == 1) == (limit == 4) && (limit == 4 || s7.Events.Any(e => e.Kind == SupplyEventKind.NoCover)),
                "suppliers gate: after leaving by right the buyer switches to an offer " + (limit == 4 ? "at any price" : "only inside the price limit, and says so when none is"));
        }
        var w5 = new SupplyWorld { DayNow = 300 };
        w5.Buyers.Add(SupplyShop);
        w5.Sold["apples"] = 2400;
        w5.Sold["bread"] = 2400;
        var s5 = new SupplierState { Seed = 11 };
        int failDay = Enumerable.Range(320, 3000).First(d => !WholeOn(s5, w5, 3, d, 3) && SupplierRules.Judge(11, SupplierCatalogue.Firm(3), s5.Firm(3), d, 3, w5.Conditions(d), new int[6]).Out == Arrival.Short
                                                             && WholeOn(s5, w5, 3, d - 7, 3));
        foreach (int limit in new[] { 0, 4 })
        {
            var wc = new SupplyWorld { DayNow = failDay - 9 };
            wc.Buyers.Add(SupplyShop);
            wc.Sold["apples"] = 2400;
            wc.Sold["bread"] = 2400;
            var sc = new SupplierState { Seed = 11 };
            Contract cc = SupplierEngine.Sign(sc, wc, 3, SupplyShop, "Shop", 8, 2, out _);
            cc.Cover = true;
            cc.RenewLimit = limit;
            RunTo(sc, wc, failDay - 1);
            Contract cover = sc.Contracts.FirstOrDefault(x => x.Covering);
            bool inside = cover == null || SupplierEngine.Value(cover) <= SupplierEngine.MarketValue(cover, wc);
            Check(limit == 0 ? inside : cover != null, "suppliers gate: a cover is bought " + (limit == 0 ? "only at or under the market's price (a quicker supplier over it is passed by)" : "at any price when the customer allows it"));
        }
        // The most that may be owed: 24 contracts with no cash behind a lenient supplier, eight weeks, in a world of small deliveries and in one of 12 products each.
        long cap = SupplierRules.AtLevel(SupplierCatalogue.OwedMost, 1000), topSmall = 0, topBig = 0, overCash = 0;
        int withheldEvents = 0, defaults = 0;
        foreach (bool big in new[] { false, true })
        {
            ISupplierWorld world;
            Action day;
            var small = new SupplyWorld { CashCents = 0 };
            var many = new SupplyWorldMany { CashCents = 0 };
            SupplyWorld clock = big ? many.Inner : small;
            world = big ? (ISupplierWorld)many : small;
            var sm = new SupplierState { Seed = 5 };
            for (int i = 0; i < SupplierCatalogue.ContractsMost; i++)
            {
                SupplierEngine.Sign(sm, world, 1, "Place~" + i, "Place " + i, 8, 2, out _);
            }
            long top = 0;
            while (clock.DayNow < 100 + 70)
            {
                clock.DayNow++;
                SupplierEngine.Day(sm, world);
                SupplierEngine.Hour(sm, world);
                top = Math.Max(top, sm.Owed.Where(o => !o.Settlement).Sum(o => o.Cents));   // what is owed on account; a settlement is the price of a contract ended unpaid
                withheldEvents += sm.Events.Count(e => e.Kind == SupplyEventKind.Withheld);
                sm.Events.Clear();
            }
            if (big)
            {
                topBig = top;
                overCash += many.PaidOverCash;
            }
            else
            {
                topSmall = top;
                overCash += small.PaidOverCash;
            }
            defaults += sm.Record.Defaults;
            Check(sm.Contracts.Count == SupplierCatalogue.ContractsMost, "suppliers gate: the 24 contracts of the check were signed (" + sm.Contracts.Count + ")");
        }
        Check(topSmall > 0 && topSmall <= cap && topBig <= cap && withheldEvents > 0 && overCash == 0 && defaults > 0,
            "suppliers gate: with no cash 24 contracts on account never owe more than " + cap + " cents in all (most owed " + topSmall + " in small deliveries, " + topBig + " in 12 products each); past it a supplier brings nothing, as a strict one does, and ends the contract after "
            + SupplierCatalogue.WithheldMost);
        // What is owed is paid at the contract's place, never with no place, and never over what may be paid with.
        var wp = new SupplyWorld { CashCents = 0 };
        SupplierState sp = CalmState();
        Contract cp = ForcedContract(sp, wp, 1, 4, Arrival.Whole, Cause.None, 0, 1000);
        wp.DayNow = cp.Due;
        SupplierEngine.Hour(sp, wp);
        wp.CashCents = SupplierEngine.Value(cp) * 10;
        wp.Reserve = wp.CashCents - SupplierEngine.Value(cp) / 2;
        long taken = SupplierEngine.Collect(sp, wp);
        Check(taken == SupplierEngine.Value(cp) / 2 && wp.CashCents >= wp.Reserve && wp.PayPlaces.Count == 1 && wp.PayPlaces[0] == SupplierEngine.OwedKey + "@" + SupplyShop && sp.OwedAll() == SupplierEngine.Value(cp) - taken,
            "suppliers gate: what is owed is collected from the cash over the investors' floor only, and booked at the contract's own business (" + taken + " taken)");
    }

    // ---- 5. the weather a delivery is judged by

    private static void SupplierWeatherChecks()
    {
        var storm = new CityDay { Season = 2, Weather = CityWeather.Storm };
        var ice = new CityDay { Season = 0, Weather = CityWeather.Ice };
        Check(SupplierRules.Forecast(storm, true, true, false).Weather == CityWeather.Fair && SupplierRules.Forecast(storm, true, true, true).Weather == CityWeather.Storm
              && SupplierRules.Forecast(new CityDay { Season = 2 }, true, true, true).Weather == CityWeather.Storm && SupplierRules.Forecast(new CityDay { Season = 2 }, false, true, true).Weather == CityWeather.Fair
              && SupplierRules.Forecast(new CityDay { Season = 2 }, true, false, true).Weather == CityWeather.Fair && SupplierRules.Forecast(ice, true, true, true).Weather == CityWeather.Ice
              && SupplierRules.Forecast(new CityDay { Season = 2, Weather = CityWeather.Heat }, true, true, true).Weather == CityWeather.Heat,
            "suppliers weather: a storm that rages today is no forecast and is dropped; the seasons' seeded storm day is one; a spell stays; with the weather or the seasons off there is none");
        int days = 0, stormDays = 0, summers = 0;
        bool same = true, onlyInSummer = true;
        int[] other = new int[600];
        for (int day = 1; day <= 600; day++)
        {
            bool a = SeasonLookRules.RainOn(day, 60, 12345).Storm, b = SeasonLookRules.RainOn(day, 60, 12345).Storm;
            same &= a == b;
            onlyInSummer &= !a || SeasonRules.SeasonOf(day, 60) == RealLifeOverhaul.CalendarRules.Summer;
            summers += SeasonRules.SeasonOf(day, 60) == RealLifeOverhaul.CalendarRules.Summer ? 1 : 0;
            stormDays += a ? 1 : 0;
            other[day - 1] = SeasonLookRules.RainOn(day, 60, 777).Storm ? 1 : 0;
            days++;
        }
        Check(same && onlyInSummer && stormDays > summers / 10 && stormDays < summers / 3 && other.Sum() != stormDays,
            "suppliers weather: the seasons' storm days are the save's own seed's, the same every time asked, only in summer (" + stormDays + " of " + summers + " summer days)");
        string links = ModSource(@"Wiring\Wiring.SupplierLinks.cs");
        Check(!links.Contains("Weather.Sky") && !links.Contains("Sky.Storm") && links.Contains("SupplierRules.Forecast(d, Settings.WeatherMatters, Settings.Seasons, stormDay)")
              && links.Contains("SeasonLookRules.RainOn(day, GameCalendar.DaysPerYear, EventCalendar.Salt).Storm"),
            "suppliers weather: the supplier side reads the weather of a day from the seeded forecast and never the live sky, for the player's deliveries and the rival shops alike");
    }

    // ---- 6. a strict supplier's fee and an unpaid settlement

    private static void SupplierSettlementChecks()
    {
        Check(SupplierCatalogue.LateFee[2] == 50 && SupplierRules.WithFee(100000, LatePolicy.Strict) == 105000, "suppliers settlement: the strict supplier's fee is the catalogue's own and is charged");
        // A broke customer ends a contract with a strict supplier: the settlement is owed with the fee, two days to pay, and unpaid past them it marks the record once.
        var w = new SupplyWorld { CashCents = 0 };
        SupplierState s = CalmState();
        w.Sold["apples"] = w.Sold["bread"] = 2400;
        Contract c = SupplierEngine.Sign(s, w, 2, SupplyShop, "Shop", 4, 4, out _);
        long settle = SupplierEngine.BreakCost(c);
        SupplierEngine.Break(s, w, c.Id);
        OwedLine o = s.Owed.Count == 1 ? s.Owed[0] : null;
        Check(o != null && o.Settlement && !o.Acted && o.Cents == SupplierRules.WithFee(settle, LatePolicy.Strict) && o.Due == w.DayNow + SupplierCatalogue.GraceDays[2] && s.Record.Defaults == 0,
            "suppliers settlement: a settlement a strict customer cannot pay is owed with the supplier's fee, to be paid in its two days");
        RunTo(s, w, o.Due);
        Check(s.Record.Defaults == 0 && s.Firm(2).SourUntil == 0, "suppliers settlement: on its last day of grace nothing has happened yet");
        RunTo(s, w, o.Due + 1);
        Check(s.Record.Defaults == 1 && s.Firm(2).SourUntil == w.DayNow + SupplierCatalogue.SourDays && s.Owed.Count == 1 && o.Acted, "suppliers settlement: unpaid past its days it marks the record and the supplier offers nothing for a while");
        RunTo(s, w, w.DayNow + 40);
        Check(s.Record.Defaults == 1 && s.Owed.Count == 1 && s.Owed[0].Cents == SupplierRules.WithFee(settle, LatePolicy.Strict), "suppliers settlement: the mark is made once, and the debt neither grows nor goes");
        // A place closed with no cash behind a firm supplier: the same.
        var wc = new SupplyWorld { CashCents = 0 };
        SupplierState sc = CalmState();
        Contract cc = SupplierEngine.Sign(sc, wc, 3, SupplyShop, "Shop", 4, 2, out _);
        long settleC = SupplierEngine.BreakCost(cc);
        wc.Shut.Add(SupplyShop);
        wc.DayNow++;
        SupplierEngine.Day(sc, wc);
        Check(sc.Owed.Count == 1 && sc.Owed[0].Cents == SupplierRules.WithFee(settleC, LatePolicy.Firm) && sc.Record.Defaults == 0, "suppliers settlement: a closed place's settlement is owed with the firm supplier's fee");
        RunTo(sc, wc, wc.DayNow + SupplierCatalogue.GraceDays[1] + 1);
        Check(sc.Record.Defaults == 1 && sc.Firm(3).SourUntil > 0, "suppliers settlement: and unpaid past its four days it counts as a default");
    }

    // ---- when the director runs what: the rules it asks

    private static void SupplierRulesChecks()
    {
        Check(SupplierRules.On(true, true) && !SupplierRules.On(true, false) && !SupplierRules.On(false, true) && SupplierRules.Running(true, false, true, true) && !SupplierRules.Running(false, false, true, true)
              && !SupplierRules.Running(true, true, true, true) && !SupplierRules.Running(true, false, true, false), "suppliers pause: the feature runs only with the living economy and its own option on, a save read, and nothing failed");
        Check(SupplierRules.DayDue(100, 101) && !SupplierRules.DayDue(101, 101) && SupplierRules.HourDue(101, 7, 101, 8) && SupplierRules.HourDue(101, 8, 102, 8) && !SupplierRules.HourDue(101, 8, 101, 8),
            "suppliers pause: the day's pass is due once a day and the hour's once an hour, and looking again in the same hour does nothing");
        int from = SupplierRules.PlayFrom(100, 101, out bool paused1);
        int skip = SupplierRules.PlayFrom(100, 104, out bool paused2);
        int far = SupplierRules.PlayFrom(100, 140, out bool paused3);
        int edge = SupplierRules.PlayFrom(100, 100 + SupplierCatalogue.CatchUpDays + 1, out bool paused4);
        Check(from == 101 && !paused1 && skip == 101 && !paused2 && far == 140 - SupplierCatalogue.CatchUpDays && paused3 && edge == 101 && !paused4,
            "suppliers time skip: the days that went by are played from the next one, at most " + SupplierCatalogue.CatchUpDays + " of them, and the days before are a pause");
        Check(!SupplierRules.RivalsMay(true, 99, 100, false, 5, false) && !SupplierRules.RivalsMay(false, 100, 100, false, 5, false) && !SupplierRules.RivalsMay(false, 99, 100, false, 0, false)
              && !SupplierRules.RivalsMay(false, 99, 100, false, 5, true) && SupplierRules.RivalsMay(false, 99, 100, false, 1, false) && SupplierRules.RivalsMay(false, 99, 100, true, 0, false)
              && !SupplierRules.RivalsMay(true, 99, 100, true, 5, false), "suppliers session: a guest leaves the city's shops to the host; otherwise once a day from 01:00, after the game's valuation, and a day under way is finished");
        Check(!SupplierRules.TycoonsMove(true) && SupplierRules.TycoonsMove(false) && !SupplierRules.OwnerGainMay(5000, true) && !SupplierRules.OwnerGainMay(99, false) && SupplierRules.OwnerGainMay(100, false),
            "suppliers session: the tycoons rest in a session and no margin reaches one, and a margin under a dollar is not moved");
        Check(SupplierRules.PayMay(100, 100) && !SupplierRules.PayMay(101, 100) && !SupplierRules.PayMay(int.MaxValue, long.MaxValue / 4) && SupplierRules.PayMay(0, 0),
            "suppliers money: a payment is never over the cash that may be paid with");
        string director = ModSource(@"Suppliers\SupplierDirector.cs"), rivals = ModSource(@"Suppliers\SupplierDirector.Rivals.cs"), game = ModSource(@"Suppliers\SupplierGame.cs");
        Check(director.Contains("SupplierRules.On(Settings.LivingPopulation, Settings.LivingSuppliers)") && director.Contains("SupplierRules.Running(_loaded, _broken, Settings.LivingPopulation, Settings.LivingSuppliers)")
              && director.Contains("SupplierRules.DayDue(_state.LastDay, day)") && director.Contains("SupplierRules.HourDue(_hourDay, _hour, day, hour)") && director.Contains("SupplierEngine.CatchUp(_state, _world, day)")
              && rivals.Contains("SupplierRules.RivalsMay(GoingPublic.IsGuest, _state.RivalDay, today") && rivals.Contains("SupplierRules.TycoonsMove(GoingPublic.InSession)")
              && game.Contains("SupplierRules.PayMay(cents, Spare)") && game.Contains("SupplierRules.OwnerGainMay(cents, GoingPublic.InSession)"),
            "suppliers pause: the director asks those very rules, nothing else, when to run the day, the hour, a time skip, the rival shops and a payment");
        Check(director.Contains("else if (_state.LastDay < day - 1)") && director.Contains("SupplierEngine.Resume(_state, day)"), "suppliers pause: coming back after the option was off is a pause, not days to play");
    }

    // ---- 8. the reader never drops a running contract or a debt

    private static void SupplierReaderChecks()
    {
        var s = new SupplierState { Seed = 9 };
        for (int i = 1; i <= 90; i++)
        {
            var c = new Contract { Id = i, Supplier = 1 + i % 9, Place = "P~" + i, Weeks = 4, Due = 200 + i, Made = 1, State = i <= 24 ? ContractState.Running : ContractState.Done, Ended = 150 };
            c.Lines.Add(new ContractLine { Item = "apples", Kind = 0, Units = 12, Mills = 1250 });
            s.Contracts.Add(c);
            if (i > 24 && i <= 54)
            {
                s.Owed.Add(new OwedLine { Supplier = c.Supplier, Contract = i, Cents = 1000 + i, Since = 140, Due = 147, Acted = i % 2 == 0 });
            }
        }
        for (int i = 0; i < 40; i++)
        {
            s.Owed.Add(new OwedLine { Supplier = 1 + i % 9, Contract = 0, Cents = 500 + i, Since = 100, Due = 107 });
        }
        SupplierState back = SupplierStore.Read(SupplierStore.Write(s), out _);
        bool running = Enumerable.Range(1, 24).All(i => back.ContractOf(i) != null && back.ContractOf(i).Running);
        bool debts = Enumerable.Range(25, 30).All(i => back.ContractOf(i) != null);
        int plain = back.Contracts.Count(c => !c.Running && back.OwedOn(c.Id) == 0);
        Check(running && debts && plain <= SupplierCatalogue.ContractsKept && back.Owed.Count == 70 && back.OwedAll() == s.OwedAll() && back.Owed.Count(o => o.Acted) == s.Owed.Count(o => o.Acted),
            "suppliers save: a save with 24 running contracts, 30 ended with money owed and 70 lines owed is read whole: nothing running or owed is dropped, and only the ended ones nobody owes are let go (" + back.Contracts.Count + " contracts)");
        // The engine's debt lines stay inside the lines the save keeps by joining the oldest of their contract.
        var w = new SupplyWorldMany { CashCents = 0 };
        var sm = new SupplierState { Seed = 5 };
        for (int i = 0; i < SupplierCatalogue.OwedLinesMost + 20; i++)
        {
            sm.Owed.Add(new OwedLine { Supplier = 1, Contract = 1 + i % 3, Cents = 100, Since = 1, Due = 5 });
        }
        long before = sm.OwedAll();
        w.Inner.DayNow = 500;
        SupplierEngine.Collect(sm, w);
        Check(sm.OwedAll() == before, "suppliers save: lines owed are never lost by being counted or collected with no cash");
    }

    // ---- 9. the best sellers first

    private static void SupplierRankingChecks()
    {
        var w = new SupplyWorldMany();
        var s = CalmState();
        var q = new Quote();
        SupplierEngine.Offer(s, w, 1, SupplyShop, 4, 2, q);
        var expected = Enumerable.Range(0, 20).OrderByDescending(i => (long)SupplyWorldMany.UnitsOf(i) * (1000 + 150 * i)).ThenBy(i => i).Take(SupplierCatalogue.LinesMost).Select(i => "item" + i.ToString("00")).ToList();
        Check(q.Lines.Count == SupplierCatalogue.LinesMost && q.Lines.Select(l => l.Item).OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x)) && q.LeftOut == 8,
            "suppliers offer: a place that sells twenty products is offered the twelve it sells most, by units at the day's price, and eight are said to be left out (" + q.LeftOut + ")");
        Check(!q.Lines.Select(l => l.Item).SequenceEqual(Enumerable.Range(0, 12).Select(i => "item" + i.ToString("00"))), "suppliers offer: the twelve are not simply the first twelve by name");
        var few = new SupplyWorld();
        SupplierEngine.Offer(s, few, 1, SupplyShop, 4, 2, q);
        Check(q.LeftOut == 0, "suppliers offer: nothing is left out when the contract holds everything the place sells that the supplier trades");
        var cover = new List<ContractLine>();
        for (int i = 0; i < 20; i++)
        {
            cover.Add(new ContractLine { Item = "item" + i.ToString("00"), Kind = 1, Units = 10 });
        }
        SupplierEngine.OfferFor(s, w, 1, SupplyShop, cover, q);
        Check(q.Lines.Count == SupplierCatalogue.LinesMost && q.LeftOut == 8, "suppliers offer: an order for goods named one by one counts the products it left out the same way");
        string pages = ModSource(@"Ui\Ventures\SupplyPages.cs"), app = ModSource(@"Suppliers\SupplierApp.cs");
        Check(pages.Contains("reallife_supply_left_out") && app.Contains("reallife_supply_left_out") && ModSource(@"Suppliers\SupplierDirector.Facts.cs").Contains("fact.LeftOut = _quote.LeftOut;"),
            "suppliers offer: both pages, the kit's and the game's frame, say how many products were left out");
    }

    // ---- 10. a credit is the honest loss, and a customer who knew gets no exit from it

    private static void SupplierKnewChecks()
    {
        var wt = new SupplyWorld();
        SupplierState st = CalmState();
        st.Firm(1).TroubleKind = 1;
        st.Firm(1).TroubleUntil = 99999;
        Contract knew = SupplierEngine.Sign(st, wt, 1, SupplyShop, "Shop", 8, 2, out _);
        long cash = wt.CashCents;
        for (int i = 0; i < 4; i++)
        {
            Force(knew, i % 2 == 0 ? Arrival.Late : Arrival.Short, Cause.Trouble, 2, 500);
            wt.DayNow = knew.Due + knew.Slip;
            SupplierEngine.Hour(st, wt);
        }
        Check(knew.Knew && knew.Failures == 0 && knew.Credit == 0 && !SupplierEngine.MayLeave(knew) && CountEvents(st, SupplyEventKind.Credit) == 0 && CountEvents(st, SupplyEventKind.MayLeave) == 0,
            "suppliers remedy: signing with a supplier visibly on strike earns no credit and no right to leave from the strike's failures");
        var wh = new SupplyWorld();
        SupplierState sh = CalmState();
        Contract healthy = SupplierEngine.Sign(sh, wh, 1, SupplyShop, "Shop", 8, 2, out _);
        sh.Firm(1).TroubleKind = 1;
        sh.Firm(1).TroubleUntil = 99999;   // the strike starts after signing
        for (int i = 0; i < 3; i++)
        {
            Force(healthy, Arrival.Short, Cause.Trouble, 0, 500);
            wh.DayNow = healthy.Due;
            SupplierEngine.Hour(sh, wh);
        }
        Check(!healthy.Knew && healthy.Failures == 3 && SupplierEngine.MayLeave(healthy) && CountEvents(sh, SupplyEventKind.Credit) == 3, "suppliers remedy: a strike that starts after signing is the supplier's failure to a customer who did not know of it");
        var ww = new SupplyWorld();
        SupplierState sw = CalmState();
        sw.Firm(1).Health = SupplierCatalogue.HealthWeak - 1;
        Check(SupplierEngine.Sign(sw, ww, 1, SupplyShop, "Shop", 4, 2, out _).Knew, "suppliers remedy: a supplier short of money when signed is a trouble the customer saw");
        // The expected rebate of signing with a supplier that is out on strike is nothing, not a gain.
        long gained = 0;
        var wg = new SupplyWorld();
        SupplierState sg = CalmState();
        sg.Firm(1).TroubleKind = 2;
        sg.Firm(1).TroubleUntil = 99999;
        sg.Seed = 3;
        for (int i = 0; i < 30; i++)
        {
            Contract c = SupplierEngine.Sign(sg, wg, 1, "Place~" + i, "Place", 0, 2, out _);
            if (c == null)
            {
                break;
            }
            Force(c, Arrival.Late, Cause.Trouble, 3, 1000);
            long value = SupplierEngine.Value(c);
            long cashBefore = wg.CashCents;
            wg.DayNow = c.Due + 3;
            SupplierEngine.Hour(sg, wg);
            gained += value - (cashBefore - wg.CashCents);
        }
        Check(gained == 0, "suppliers remedy: late deliveries from a supplier on strike signed with, whole when they come, are paid in full: no rebate for signing into the trouble (" + gained + " cents)");
    }

    // ---- 11. a time skip plays each day

    private static void SupplierTimeSkipChecks()
    {
        SupplierState Start(out SupplyWorld w)
        {
            w = new SupplyWorld { DayNow = 100 };
            w.Buyers.Add(SupplyShop);
            var s = new SupplierState { Seed = QuietSeed(w, 1, 101, 3, 3) + 0 };
            Contract c = SupplierEngine.Sign(s, w, 1, SupplyShop, "Shop", 4, 2, out _);
            c.Cover = true;
            s.LastDay = 100;
            return s;
        }
        SupplierState byDay = Start(out SupplyWorld wd);
        RunTo(byDay, wd, 110);
        SupplierState skipped = Start(out SupplyWorld ws);
        ws.DayNow = 110;
        int played = SupplierEngine.CatchUp(skipped, ws, 110);
        SupplierEngine.Day(skipped, ws);
        SupplierEngine.Hour(skipped, ws);
        Check(played == 9 && ws.DayNow == 110 && ws.HourNow == SupplierCatalogue.DeliveryHour && SupplierStore.Write(byDay) == SupplierStore.Write(skipped) && wd.CashCents == ws.CashCents,
            "suppliers time skip: ten days slept through are the same ten days as lived one by one: the same line in the save and the same cash (" + played + " days played)");
        SupplierState once = Start(out SupplyWorld wo);
        wo.DayNow = 110;
        SupplierEngine.Day(once, wo);
        SupplierEngine.Hour(once, wo);
        Check(SupplierStore.Write(once) != SupplierStore.Write(byDay), "suppliers time skip: the day's pass run once for the ten days gives another line, as it did: the check above would fail on it");
        SupplierState far = Start(out SupplyWorld wf);
        wf.DayNow = 160;
        int playedFar = SupplierEngine.CatchUp(far, wf, 160);
        Check(playedFar == SupplierCatalogue.CatchUpDays && wf.DayNow == 160 && far.Contracts[0].Due >= 160 - SupplierCatalogue.CatchUpDays && far.Contracts[0].Made <= 4,
            "suppliers time skip: a skip of sixty days plays the last " + SupplierCatalogue.CatchUpDays + " and pauses the rest, the contract going on at its next weekday (" + playedFar + " played)");
        SupplierState none = Start(out SupplyWorld wn);
        wn.DayNow = 101;
        Check(SupplierEngine.CatchUp(none, wn, 101) == 0, "suppliers time skip: the next day is not a skip");
    }

    // ---- 12. the money and the contract's step move together

    private static void SupplierAtomicChecks()
    {
        var w = new SupplyWorld();
        var s = new SupplierState { Seed = QuietSeed(w, 1, 101, 4, 3) };
        Contract c = SupplierEngine.Sign(s, w, 1, SupplyShop, "Shop", 4, 2, out _);
        long value = SupplierEngine.Value(c), cash = w.CashCents;
        w.DayNow = c.Due;
        w.DeliverThrows = true;
        bool threw = false;
        try
        {
            SupplierEngine.Hour(s, w);
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        Check(threw && cash - w.CashCents == value && c.Made == 1 && c.Due == 108 && c.Paid == value, "suppliers atomic: the game's delivery fails after the payment, and the contract has stepped with it: paid once, the next delivery a week on");
        SupplierState reloaded = SupplierStore.Read(SupplierStore.Write(s), out _);
        w.DeliverThrows = false;
        long cashAfter = w.CashCents;
        SupplierEngine.Hour(reloaded, w);
        SupplierEngine.Hour(reloaded, w);
        Check(w.CashCents == cashAfter && reloaded.Contracts[0].Made == 1 && w.Pays.Count == 1, "suppliers atomic: after a save and a load the delivery is not charged again");
        var w2 = new SupplyWorld();
        w2.Buyers.Add(SupplyShop);
        var s2 = new SupplierState { Seed = QuietSeed(w2, 1, 101, 6, 3) };
        Contract c2 = SupplierEngine.Sign(s2, w2, 1, SupplyShop, "Shop", 2, 2, out _);
        c2.Renew = RenewRule.Same;
        c2.RenewLimit = 4;
        RunTo(s2, w2, 107);
        w2.SpendThrows = true;
        bool threw2 = false;
        try
        {
            RunTo(s2, w2, 108);
        }
        catch (InvalidOperationException)
        {
            threw2 = true;
        }
        long cash2 = w2.CashCents;
        w2.SpendThrows = false;
        SupplierState back2 = SupplierStore.Read(SupplierStore.Write(s2), out _);
        SupplierEngine.Hour(back2, w2);
        Check(threw2 && c2.State == ContractState.Done && c2.Made == 2 && w2.CashCents == cash2 && back2.ContractOf(c2.Id).State == ContractState.Done,
            "suppliers atomic: a fault in the buyer's renewal after the last delivery leaves that contract done and paid, never delivered and charged a third time");
        string engine = ModSource(@"Suppliers\SupplierEngine.cs");
        string deliver = engine.Substring(engine.IndexOf("private static void Deliver(", StringComparison.Ordinal), 6000);
        Check(deliver.IndexOf("w.Pay(net, GoodsKey", StringComparison.Ordinal) > 0 && deliver.IndexOf("Step(w, c);", StringComparison.Ordinal) > deliver.IndexOf("w.Pay(net, GoodsKey", StringComparison.Ordinal)
              && deliver.IndexOf("w.Deliver(c.Place", StringComparison.Ordinal) > deliver.IndexOf("Step(w, c);", StringComparison.Ordinal),
            "suppliers atomic: in a delivery the payment is followed by the contract's step before any other call of the world, and the goods are put down after");
    }

    // ---- 13. a rival shop's valued day is charged once

    private static void SupplierEntryChecks()
    {
        var w = new SupplyWorld { DayNow = 700 };
        w.Factors[1] = 1400;
        SupplierState s = CalmState(31);
        var customers = new int[9];
        int shop = 0;
        RivalDay first = default;
        for (int id = 1; id < 400 && first.Cents == 0; id++)
        {
            first = SupplierEngine.RivalShop(s, w, id, 1, 100000, customers, 10, 1234);
            shop = id;
        }
        Check(first.Cents != 0 && !first.Skipped, "suppliers rivals: a shop's valued day is charged (" + first.Cents + " cents)");
        SupplierEngine.RivalCharged(s, shop, 10, 5678);
        SupplierState back = SupplierStore.Read(SupplierStore.Write(s), out _);
        var customersAgain = new int[9];
        RivalDay again = SupplierEngine.RivalShop(back, w, shop, 1, 100000, customersAgain, 10, 5678);
        Check(again.Skipped && again.Cents == 0 && !again.Failed && customersAgain.Sum() == 1, "suppliers rivals: after a save and a load in the middle of the game's valuation the very entry already charged is not charged again, and its supplier still counts the customer");
        RivalDay newDay = SupplierEngine.RivalShop(back, w, shop, 1, 100000, new int[9], 11, 5678);
        RivalDay changed = SupplierEngine.RivalShop(back, w, shop, 1, 100000, new int[9], 10, 5679);
        RivalDay other = SupplierEngine.RivalShop(back, w, shop + 1, 1, 100000, new int[9], 10, 5678);
        Check(!newDay.Skipped && !changed.Skipped && !other.Skipped, "suppliers rivals: a new valued day, an entry the game has changed and another shop are charged as they should be");
        Check(back.WasCharged(shop, 10, 5678) && !back.WasCharged(shop, 10, 1234), "suppliers rivals: the state remembers which entry it charged for each shop, and it is saved");
    }

    // ---- the cost of the 08:00 pass with the caps full, and of the rivals' pass

    private static void SupplierCostChecks()
    {
        // 24 contracts of 12 products each, all due on the same morning.
        var many = new SupplyWorldMany { CashCents = long.MaxValue / 8 };
        many.Inner.DayNow = 100;
        var s = new SupplierState { Seed = 5 };
        for (int i = 0; i < SupplierCatalogue.ContractsMost; i++)
        {
            Contract c = SupplierEngine.Sign(s, many, 1, "Place~" + i, "Place " + i, 8, 2, out _);
            if (c != null)
            {
                c.Due = 101;
            }
        }
        string line = SupplierStore.Write(s);
        int lines = s.Contracts.Sum(c => c.Lines.Count);
        var hours = new List<double>();
        var days = new List<double>();
        for (int run = 0; run < 12; run++)
        {
            SupplierState copy = SupplierStore.Read(line, out _);
            many.Inner.DayNow = 100;
            many.Inner.HourNow = SupplierCatalogue.DeliveryHour;
            many.Inner.DayNow = 101;
            var clock = Stopwatch.StartNew();
            SupplierEngine.Day(copy, many);
            days.Add(clock.Elapsed.TotalMilliseconds);
            clock.Restart();
            SupplierEngine.Hour(copy, many);
            hours.Add(clock.Elapsed.TotalMilliseconds);
        }
        double hour = hours.Skip(2).Average(), day = days.Skip(2).Average();
        Check(lines == SupplierCatalogue.ContractsMost * SupplierCatalogue.LinesMost && hour < 20 && day < 20,
            "suppliers cost: the 08:00 pass of " + SupplierCatalogue.ContractsMost + " contracts of " + SupplierCatalogue.LinesMost + " products takes " + hour.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
            + " ms and the day's pass " + day.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " ms (" + lines + " lines)");
        // The rivals' pass: five hundred shops, nothing allocated in the per shop loop.
        var w = new SupplyWorld { DayNow = 700 };
        SupplierState rs = CalmState(31);
        var customers = new int[9];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int shop = 0; shop < 500; shop++)
            {
                SupplierEngine.RivalShop(rs, w, SupplierRules.ShopId("Shop Street", shop), shop % 6, 100000, customers, 3, shop);
            }
        }
        var timer = Stopwatch.StartNew();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int pass = 0; pass < 20; pass++)
        {
            for (int shop = 0; shop < 500; shop++)
            {
                SupplierEngine.RivalShop(rs, w, SupplierRules.ShopId("Shop Street", shop), shop % 6, 100000, customers, 3, shop);
            }
        }
        double per = timer.Elapsed.TotalMilliseconds / 10000 * 1000;
        long used = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Console.WriteLine("suppliers cost: day pass " + day.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " ms, hour pass " + hour.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
                          + " ms (24 contracts of 12 products), rival shop " + per.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " microseconds, " + used + " bytes allocated");
        Check(used == 0 && per < 100, "suppliers cost: the rival shops' pass allocates nothing in its per shop loop (" + used + " bytes over 10,000 shops) and takes " + per.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " microseconds a shop");
    }
}
