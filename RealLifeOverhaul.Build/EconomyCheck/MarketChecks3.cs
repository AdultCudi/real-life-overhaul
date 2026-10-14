using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul.Markets;

// Trading: what a round trip costs, the market moving against a large order, nothing at all while the market is shut or
// held after a load, time that is skipped played out step by step, limit and stop orders filled only at prices the market
// reached, dividends only to the holders of the record day, splits exact, failures and takeovers paid out, caps, money
// never made by rounding or by cancelling, and the margin account with every safeguard.
internal static partial class Program
{
    // A listed company of a size, with no bid, no split and no results today; the most traded first.
    private static Stock Liquid(MarketEngine m, MarketSize size, int skip = 0)
    {
        var list = m.State.Stocks.Where(s => s.Listed && s.Size == size && s.BidCents <= 0 && s.SplitDay <= 0 && s.ResultsDay != m.State.Day).OrderByDescending(s => s.Adv).ToList();
        return list.Count > skip ? list[skip] : list.FirstOrDefault();
    }

    // The market on a market day at a step of its session.
    private static int SessionDay(MarketEngine m, int from, int step)
    {
        int day = TradingOnOrAfter(from);
        RunTo(m, day, MarketCatalogue.PhaseOpen + step);
        return day;
    }

    private static Order Trade(MarketEngine m, Stock s, OrderSide side, long qty, OrderType type = OrderType.Market, int limit = 0, int stop = 0, OrderLife life = OrderLife.Day)
    {
        OrderWhy why;
        return m.Place(s.Id, side, type, life, qty, limit, stop, m.Trading, m.Held, false, out why);
    }

    private static OrderWhy Why(MarketEngine m, Stock s, OrderSide side, long qty, OrderType type = OrderType.Market, int limit = 0, int stop = 0)
    {
        OrderWhy why;
        m.Place(s.Id, side, type, OrderLife.Day, qty, limit, stop, m.Trading, m.Held, false, out why);
        return why;
    }

    private static void MarketTradeCosts()
    {
        int trips = 0, cheap = 0;
        double worstShare = 1.0;
        for (ulong seed = 1; seed <= 4; seed++)
        {
            var world = new MarketWorld(seed);
            world.Purse.Money = 100000000;
            MarketEngine m = NewMarket(seed + 40, 60, 1, 0, world);
            int day = SessionDay(m, 3, 10);
            foreach (MarketSize size in new[] { MarketSize.Large, MarketSize.Mid, MarketSize.Small })
            {
                for (int k = 0; k < 4; k++)
                {
                    Stock s = Liquid(m, size, k);
                    foreach (long qty in new[] { 1L, 10L, 100L, 1000L, Math.Max(1, MarketTrading.MostPerOrder(s) / 2) })
                    {
                        long before = world.Purse.Money;
                        Quote q = MarketTrading.QuoteOf(m.State, s, m.State.Phase);
                        Order buy = Trade(m, s, OrderSide.Buy, qty);
                        Order sell = buy != null && buy.State == OrderState.Filled ? Trade(m, s, OrderSide.Sell, qty) : null;
                        if (sell == null || sell.State != OrderState.Filled)
                        {
                            continue;
                        }
                        trips++;
                        long lost = before - world.Purse.Money;
                        long spread = MarketTrading.Value(qty, q.Ask, true) - MarketTrading.Value(qty, q.Bid, false);
                        long fees = 2 * world.Least;
                        if (lost < spread + fees)
                        {
                            cheap++;
                        }
                        worstShare = Math.Min(worstShare, lost / (double)Math.Max(1, spread + fees));
                    }
                }
            }
            // A day later: bought one day, sold the next, many times: on average the costs are lost.
            _ = day;
        }
        Check(trips >= 50 && cheap == 0, "market: a round trip always costs at least the spread and two commissions (" + trips + " trips, " + cheap + " cheaper; least " + worstShare.ToString("0.00") + " times the costs)");

        // Bought and sold a step later, many times over: the costs are lost on average, whatever the price did.
        double net = 0.0, costs = 0.0;
        int n = 0;
        for (ulong seed = 11; seed <= 16; seed++)
        {
            var world = new MarketWorld(seed);
            world.Purse.Money = 100000000;
            MarketEngine m = NewMarket(seed, 60, 1, 0, world);
            for (int d = 3; d < 60; d += 3)
            {
                int day = SessionDay(m, d, 20);
                for (int k = 0; k < 6; k++)
                {
                    Stock s = Liquid(m, (MarketSize)(k % 3), k / 3);
                    long before = world.Purse.Money;
                    Order buy = Trade(m, s, OrderSide.Buy, 200);
                    if (buy == null || buy.State != OrderState.Filled)
                    {
                        continue;
                    }
                    RunTo(m, day, m.State.Phase + 1);   // a step later
                    Order sell = Trade(m, s, OrderSide.Sell, 200);
                    if (sell == null || sell.State != OrderState.Filled)
                    {
                        continue;
                    }
                    net += world.Purse.Money - before;
                    costs += 2 * world.Least;
                    n++;
                }
            }
        }
        Check(n > 100 && net < 0.0, "market: buying and selling a step later loses money on average (" + n + " trips, " + net.ToString("0") + " in all)");
    }

    private static void MarketImpact()
    {
        var world = new MarketWorld(3);
        world.Purse.Money = 1000000000;
        MarketEngine m = NewMarket(3, 60, 1, 0, world);
        SessionDay(m, 2, 30);
        bool rising = true, pricier = true;
        foreach (MarketSize size in new[] { MarketSize.Large, MarketSize.Mid, MarketSize.Small })
        {
            Stock s = Liquid(m, size);
            double last = 0.0;
            int lastPrice = 0;
            for (long q = 1; q <= MarketTrading.MostPerOrder(s); q = q * 3 + 1)
            {
                double impact = MarketTrading.Impact(m.State, s, q);
                int price = MarketTrading.MarketPrice(m.State, s, true, q, m.State.Phase);
                rising &= impact >= last;
                pricier &= price >= lastPrice;
                last = impact;
                lastPrice = price;
            }
        }
        Check(rising && pricier, "market: the larger the order, the further the price moves against it");
        // Ten small orders one after the other cost no less than one large one.
        Stock t = Liquid(m, MarketSize.Mid);
        long whole = MarketTrading.MostPerOrder(t) / 2;
        MarketEngine twin = NewMarket(3, 60, 1, 0, new MarketWorld(3) { Purse = { Money = 1000000000 } });
        SessionDay(twin, 2, 30);
        Stock tt = twin.State.Stock(t.Id);
        long beforeOne = WorldOf(twin).Purse.Money;
        Trade(twin, tt, OrderSide.Buy, whole);
        long one = beforeOne - WorldOf(twin).Purse.Money;
        long beforeTen = world.Purse.Money;
        for (int i = 0; i < 10; i++)
        {
            Trade(m, t, OrderSide.Buy, whole / 10);
        }
        long ten = beforeTen - world.Purse.Money - 9 * world.Least;   // (the nine commissions more are not the market's doing)
        Check(ten >= one * 0.99, "market: splitting an order saves nothing on the market's move (" + ten + " in ten against " + one + " in one)");
        Check(t.Impact > 0.0, "market: the player's recent trading in a company is remembered");
        double impact0 = t.Impact;
        RunTo(m, m.State.Day, m.State.Phase + 12);
        Check(t.Impact < impact0 * 0.5, "market: and it fades within the hour");
        // A part of the move stays in the price.
        Stock u = Liquid(m, MarketSize.Small);
        double mis = u.Mis;
        Trade(m, u, OrderSide.Buy, MarketTrading.MostPerOrder(u) / 2);
        Check(u.Mis > mis, "market: a large purchase leaves the price higher");
    }

    private static void MarketClosedAndHeld()
    {
        var world = new MarketWorld(5);
        world.Purse.Money = 10000000;
        MarketEngine m = NewMarket(5, 60, 1, 0, world);
        int day = TradingOnOrAfter(4);
        RunTo(m, day, MarketCatalogue.PhaseResults);   // 07:30, before the open
        Stock s = Liquid(m, MarketSize.Large);
        long before = world.Purse.Money;
        Order o = Trade(m, s, OrderSide.Buy, 100);
        Check(!m.Trading && o != null && o.IsOpen && o.Filled == 0 && world.Purse.Money == before, "market: an order at the market placed before the open waits; nothing is paid");
        RunTo(m, day, MarketCatalogue.PhaseOpen);
        Check(o.State == OrderState.Filled && world.Purse.Money < before, "market: it is filled at the open");
        int filledAt = o.Filled > 0 ? (int)(o.FilledValue * 100 / o.Filled) : 0;
        Check(Math.Abs(filledAt - s.Open) <= Math.Max(3, s.Open / 50), "market: at the open's price (" + filledAt + " against an open of " + s.Open + ")");
        // A weekend: placed on a Saturday, filled on the Monday.
        int saturday = day;
        while (MarketCatalogue.Weekday(saturday) != 5)
        {
            saturday++;
        }
        RunTo(m, saturday, MarketCatalogue.PhaseClose);
        long beforeSat = world.Purse.Money;
        int beforeLast = s.Last;
        Order w = Trade(m, s, OrderSide.Buy, 50);
        Check(w != null && w.IsOpen && world.Purse.Money == beforeSat, "market: nothing trades on a weekend");
        RunTo(m, saturday + 1, MarketCatalogue.PhaseClose);
        Check(w.IsOpen && s.Last == beforeLast, "market: and prices do not move on one");
        RunTo(m, saturday + 2, MarketCatalogue.PhaseOpen);
        Check(w.State == OrderState.Filled, "market: the Monday's open fills it");
        // Held after a load: no order of any kind until the market is past the furthest moment played.
        RunTo(m, saturday + 2, MarketCatalogue.PhaseOpen + 40);
        MarketTape tape = m.Tape;
        long frontier = tape.Frontier;
        string line = MarketStore.Write(m.State);
        RunTo(m, saturday + 3, MarketCatalogue.PhaseOpen + 10);
        MarketState loaded = MarketStore.Read(line, out _);
        var again = new MarketEngine(loaded, MarketTape.Read(tape.Id, tape.Whole()), new MarketWorld(5) { Purse = { Money = 10000000 } });
        again.HoldAfterLoad();
        Stock a = loaded.Stock(s.Id);
        Check(again.Held && Why(again, a, OrderSide.Buy, 10) == OrderWhy.Held, "market: after a load no order is taken while the moments played before are played again");
        RunTo(again, saturday + 3, MarketCatalogue.PhaseOpen + 9);
        Check(again.Held && Why(again, a, OrderSide.Buy, 10) == OrderWhy.Held, "market: not even a step before the furthest moment played");
        RunTo(again, saturday + 3, MarketCatalogue.PhaseOpen + 10);
        Check(!again.Held && Why(again, a, OrderSide.Buy, 10) == OrderWhy.None, "market: orders are taken again once the market is back at it (nothing past it was seen)");
        // A load at the moment of the save itself, the furthest played: orders at once (the clock stands still while the phone is open).
        MarketState fresh = MarketStore.Read(MarketStore.Write(again.State), out _);
        var third = new MarketEngine(fresh, MarketTape.Read(again.Tape.Id, again.Tape.Whole()), new MarketWorld(5) { Purse = { Money = 10000000 } });
        third.HoldAfterLoad();
        Check(!third.Held && Why(third, fresh.Stock(s.Id), OrderSide.Buy, 10) == OrderWhy.None, "market: a load at the moment of the save, the furthest played, takes orders at once");
        Check(frontier > 0, "market: the tape keeps the furthest moment played");
    }

    // The order ticket's path, as the Stocks app takes it: what the ticket draws (MarketDirector.Estimate) and what its Buy
    // button places (MarketDirector.Place) for a market order.
    private static Estimate TicketEstimate(MarketEngine m, MarketWorld world, Stock k, OrderSide side, long qty)
    {
        bool held = m.Held;
        return MarketTrading.Check(m.State, k, side, OrderType.Market, qty, 0, 0, world.Wallet.Cash, world.Least, m.Trading && !held, held, m.State.Phase);
    }

    private static Order TicketPlace(MarketEngine m, Stock k, OrderSide side, long qty, out OrderWhy why)
    {
        bool held = m.Held;
        return m.Place(k.Id, side, OrderType.Market, OrderLife.Day, qty, 0, 0, m.Trading && !held, held, false, out why);
    }

    // A save loaded at its own moment, the furthest the market played, with the clock standing still (the phone is open:
    // the game is paused) takes an order from the ticket at once: filled in a session, waiting for the open when the
    // market is shut. A load of an older save holds orders back, with a reason the ticket says. The ticket's button is
    // always lit and says why when it cannot place an order, and every reason has its sentence.
    private static void MarketTicketPath()
    {
        var world = new MarketWorld(77);
        world.Purse.Money = 100000000;
        MarketEngine m = NewMarket(77, 60, 1, 0, world);
        int day = SessionDay(m, 3, 20);
        Stock s = Liquid(m, MarketSize.Large);
        string older = MarketStore.Write(m.State);
        string olderTape = m.Tape.Whole();
        RunTo(m, day, MarketCatalogue.PhaseOpen + 30);
        var loaded = new MarketEngine(MarketStore.Read(MarketStore.Write(m.State), out _), MarketTape.Read(m.Tape.Id, m.Tape.Whole()), world);
        loaded.HoldAfterLoad();
        Stock k = loaded.State.Stock(s.Id);
        Estimate e = TicketEstimate(loaded, world, k, OrderSide.Buy, 10);
        OrderWhy why;
        Order bought = e.Why == OrderWhy.None ? TicketPlace(loaded, k, OrderSide.Buy, 10, out why) : null;
        Check(!loaded.Held && e.Why == OrderWhy.None && bought != null && bought.State == OrderState.Filled && loaded.State.Held(k.Id) == 10,
            "market ticket: a save loaded at its own moment, the clock standing still, buys from the ticket at once (" + e.Why + ")");
        Estimate sell = TicketEstimate(loaded, world, k, OrderSide.Sell, 10);
        Order sold = sell.Why == OrderWhy.None ? TicketPlace(loaded, k, OrderSide.Sell, 10, out why) : null;
        Check(sold != null && sold.State == OrderState.Filled && loaded.State.Held(k.Id) == 0, "market ticket: and sells (" + sell.Why + ")");
        // Shut: loaded after the close, the order waits for the next open, and is filled there.
        RunTo(loaded, day, MarketCatalogue.PhaseClose);
        var evening = new MarketEngine(MarketStore.Read(MarketStore.Write(loaded.State), out _), MarketTape.Read(loaded.Tape.Id, loaded.Tape.Whole()), world);
        evening.HoldAfterLoad();
        Stock ek = evening.State.Stock(s.Id);
        Estimate shut = TicketEstimate(evening, world, ek, OrderSide.Buy, 10);
        Order waits = shut.Why == OrderWhy.None ? TicketPlace(evening, ek, OrderSide.Buy, 10, out why) : null;
        Check(!evening.Trading && waits != null && waits.IsOpen && waits.Filled == 0, "market ticket: with the market shut the order is taken and waits for the open (" + shut.Why + ")");
        RunTo(evening, TradingOnOrAfter(day + 1), MarketCatalogue.PhaseOpen);
        Check(waits != null && waits.State == OrderState.Filled, "market ticket: and the open fills it");
        // An older save: held, with its reason, until the market is back at the furthest moment played.
        var back = new MarketEngine(MarketStore.Read(older, out _), MarketTape.Read(m.Tape.Id, m.Tape.Whole()), world);
        back.HoldAfterLoad();
        Stock bk = back.State.Stock(s.Id);
        Check(olderTape.Length > 0 && back.Held && TicketEstimate(back, world, bk, OrderSide.Buy, 10).Why == OrderWhy.Held && TicketPlace(back, bk, OrderSide.Buy, 10, out why) == null && why == OrderWhy.Held,
            "market ticket: an older save loaded holds orders back, with the reason");
        RunTo(back, day, MarketCatalogue.PhaseOpen + 30);
        Check(!back.Held && TicketEstimate(back, world, bk, OrderSide.Buy, 10).Why == OrderWhy.None, "market ticket: until the market is back at the furthest moment it played");

        // The ticket itself (MarketApp.Box): its button is never shut off, and pressed it says the reason in the game's notification.
        string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\"));
        string box = System.IO.Path.Combine(dir, @"Scripts\Markets\MarketApp.Box.cs");
        string code = System.IO.File.Exists(box) ? System.IO.File.ReadAllText(box) : null;
        int confirm = code != null ? code.IndexOf("private void Confirm(", StringComparison.Ordinal) : -1;
        int place = code != null ? code.IndexOf("private void Place(", StringComparison.Ordinal) : -1;
        Check(code != null && !code.Contains(".interactable") && confirm > 0 && place > confirm && code.IndexOf("Error(e.Why, k)", confirm, place - confirm, StringComparison.Ordinal) > 0
              && code.IndexOf("Error(why, k)", place, StringComparison.Ordinal) > 0 && code.Contains("reallife_mkt_note_refused"),
            "market ticket: the Buy or Sell button is always lit and says why it cannot place an order (" + box + ")");
        string locale = System.IO.Path.Combine(dir, @"Locales\en.json");
        Dictionary<string, string> words = System.IO.File.Exists(locale)
            ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(System.IO.File.ReadAllText(locale).TrimStart('\uFEFF')) : null;
        var missing = new List<string>();
        for (OrderWhy w = OrderWhy.Closed; w < OrderWhy.Count; w++)
        {
            string name = w.ToString();
            var key = new System.Text.StringBuilder("reallife_mkt_why_");
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                {
                    key.Append('_');
                }
                key.Append(char.ToLowerInvariant(name[i]));
            }
            if (words == null || !words.TryGetValue(key.ToString(), out string line) || string.IsNullOrWhiteSpace(line))
            {
                missing.Add(key.ToString());
            }
        }
        Check(words != null && missing.Count == 0 && words.ContainsKey("reallife_mkt_note_refused"), "market ticket: every reason an order cannot be placed has its sentence" + (missing.Count > 0 ? " (missing " + string.Join(", ", missing) + ")" : string.Empty));
    }

    // Time skipped (a sleep, a stay in prison) is played step by step: the market is the same whether it was watched or not.
    private static void MarketSkippedTime()
    {
        MarketEngine watched = NewMarket(21, 60);
        MarketEngine skipped = NewMarket(21, 60);
        MarketEngine sliced = NewMarket(21, 60);
        for (int d = 1; d <= 75; d++)
        {
            for (int p = 0; p <= MarketCatalogue.PhaseClose; p += 7)
            {
                RunTo(watched, d, p);
            }
        }
        RunTo(watched, 75);
        RunTo(skipped, 75);
        long target = MarketState.Time(75, MarketCatalogue.PhaseClose);
        int rounds = 0;
        while (sliced.State.Now < target && rounds < 100000)
        {
            int work = 300;
            sliced.RunTo(target, ref work);
            rounds++;
        }
        bool same = true, sameSliced = true;
        for (int i = 0; i < watched.State.Stocks.Count; i++)
        {
            Stock w = watched.State.Stocks[i];
            Stock s = skipped.State.Stock(w.Id);
            Stock c = sliced.State.Stock(w.Id);
            same &= s != null && s.Last == w.Last && s.Daily.Count == w.Daily.Count && s.Status == w.Status;
            sameSliced &= c != null && c.Last == w.Last && c.Status == w.Status;
        }
        Check(same && skipped.State.IndexLast == watched.State.IndexLast, "market: seventy-five days skipped in one go are the same days as played step by step");
        Check(sameSliced && rounds > 10, "market: and in slices of a few hundred company-steps (" + rounds + " slices)");
        Check(skipped.State.Stocks.Sum(s => s.Daily.Count) > 0 && skipped.State.IndexDaily.Count > watched.State.IndexDaily.Count - 1, "market: skipped days leave their candles");
    }

    private static void MarketLimitsAndStops()
    {
        int limitFills = 0, badLimit = 0, stopFills = 0, badStop = 0, untouched = 0, early = 0, underStop = 0;
        double stopSlip = 0.0;
        for (ulong seed = 31; seed <= 36; seed++)
        {
            var world = new MarketWorld(seed);
            world.Purse.Money = 1000000000;
            MarketEngine m = NewMarket(seed, 60, 1, 0, world);
            int day = SessionDay(m, 2, 5);
            var orders = new List<(Order o, Stock s)>();
            for (int k = 0; k < 24; k++)
            {
                Stock s = Liquid(m, (MarketSize)(k % 3), k / 3);
                Quote q = MarketTrading.QuoteOf(m.State, s, m.State.Phase);
                long qty = 100;
                Order buy = Trade(m, s, OrderSide.Buy, qty, OrderType.Limit, (int)(q.Ask * 0.985), 0, OrderLife.UntilCancelled);
                Trade(m, s, OrderSide.Buy, qty * 2);   // something to sell at a stop
                Order stop = Trade(m, s, OrderSide.Sell, qty, OrderType.Stop, 0, (int)(q.Bid * 0.985), OrderLife.UntilCancelled);
                if (buy != null)
                {
                    orders.Add((buy, s));
                }
                if (stop != null)
                {
                    orders.Add((stop, s));
                }
            }
            int phase = m.State.Phase;
            for (int step = 0; step < 300 && orders.Any(x => x.o.IsOpen); step++)
            {
                var filledBefore = orders.ToDictionary(x => x.o.Id, x => x.o.Filled);
                long target = MarketState.Time(m.State.Day, m.State.Phase) + 1;
                if (m.State.Phase >= MarketCatalogue.PhaseClose)
                {
                    target = MarketState.Time(TradingOnOrAfter(m.State.Day + 1), MarketCatalogue.PhaseOpen + 1);
                }
                int work = int.MaxValue;
                m.RunTo(target, ref work);
                foreach ((Order o, Stock s) in orders)
                {
                    long now = o.Filled - filledBefore[o.Id];
                    if (now <= 0 || s.BarCount == 0)
                    {
                        continue;
                    }
                    // (another order of the company filled in the same step moved the price a little for this one: its lasting move)
                    bool another = orders.Count(x => x.s == s && x.o.Filled > filledBefore[x.o.Id]) > 1;
                    double moved = another ? MarketCatalogue.ImpactPermanent * MarketCatalogue.ImpactTemporary * MarketTrading.DayVol(m.State, s) * Math.Sqrt(200.0 / Math.Max(1.0, s.Adv)) : 0.0;
                    Candle bar = s.Bars[s.BarCount - 1];
                    double half = MarketTrading.HalfSpread(m.State, s, m.State.Phase);
                    TradeKind kind = o.Side == OrderSide.Buy ? TradeKind.Bought : TradeKind.Sold;
                    Trade last = m.State.Trades.LastOrDefault(t => t.StockId == s.Id && t.Kind == kind && t.Qty == now);
                    int price = last != null ? last.PriceCents : 0;
                    if (o.Type == OrderType.Limit)
                    {
                        limitFills++;
                        int askLow = (int)Math.Ceiling(bar.L * (1.0 + half));
                        badLimit += price > o.LimitCents || askLow > o.LimitCents ? 1 : 0;
                    }
                    else
                    {
                        // At the market's price the step ended on (the next price), less its move: never better than that bid.
                        stopFills++;
                        int bidLow = (int)Math.Floor(bar.L * (1.0 - half));
                        int bidLast = (int)Math.Floor(bar.C * (1.0 - half) * (1.0 + moved)) + (another ? 1 : 0);
                        badStop += price > bidLast || bidLow > o.StopCents ? 1 : 0;
                        stopSlip += (price - o.StopCents) / (double)o.StopCents;
                        underStop += price < o.StopCents ? 1 : 0;
                    }
                }
            }
            untouched += orders.Count(x => x.o.IsOpen && x.o.Filled == 0);
            _ = day;
            _ = phase;
            // A limit never touched by the market is never filled.
            Stock far = Liquid(m, MarketSize.Large);
            if (far != null && m.Trading)
            {
                Order never = Trade(m, far, OrderSide.Buy, 10, OrderType.Limit, Math.Max(1, far.Last / 3), 0, OrderLife.UntilCancelled);
                RunTo(m, m.State.Day + 3);
                early += never != null && never.Filled > 0 ? 1 : 0;
            }
        }
        Check(limitFills > 20 && badLimit == 0, "market: a limit to buy fills only when the offer reached it, at it or better (" + limitFills + " fills, " + badLimit + " wrong)");
        Check(stopFills > 20 && badStop == 0, "market: a stop to sell fills only when the bid reached it, at the market's next price: the step's last bid less its move (" + stopFills + " fills, "
              + badStop + " wrong)");
        Check(stopFills > 0 && stopSlip / stopFills < 0.0 && underStop > 0, "market: a stop is not a price promised: on average it goes under the stop ("
              + (stopSlip / Math.Max(1, stopFills) * 100.0).ToString("0.00") + "%), " + underStop + " of " + stopFills + " under it");
        Check(early == 0, "market: a limit the market never reached is never filled");
        Console.WriteLine("  market orders: " + limitFills + " limit fills and " + stopFills + " stop fills checked against the step's prices; " + untouched + " left open");
    }

    private static void MarketDividends()
    {
        int entitled = 0, wrong = 0, cases = 0, wholeOnRecord = 0, goneAfter = 0;
        for (ulong seed = 41; seed <= 52; seed++)
        {
            var world = new MarketWorld(seed);
            world.Purse.Money = 1000000000;
            MarketEngine m = NewMarket(seed, 60, 1, 0, world);
            // Wait for a company to declare a dividend.
            Stock s = null;
            for (int d = 1; d < 80 && s == null; d++)
            {
                RunTo(m, d, MarketCatalogue.PhaseOpen);
                s = m.State.Stocks.FirstOrDefault(k => k.Listed && k.DeclaredCents > 0 && !k.ExDone && k.RecordDay > d + 1 && k.Size != MarketSize.Small && k.BidCents <= 0 && k.SplitDay <= 0);
            }
            if (s == null)
            {
                continue;
            }
            cases++;
            int record = s.RecordDay, pay = s.PayDay, ex = MarketTrading.NextTradingDay(record);
            int cents = s.DeclaredCents;
            // The price holds all of the dividend on its record day and none of it the morning after.
            RunTo(m, record, MarketCatalogue.PhaseOpen + 70);
            wholeOnRecord += Math.Abs(MarketLife.Accrued(s, record, 60) - cents / 100.0) < 1e-9 ? 1 : 0;
            long qty = 1000;
            Trade(m, s, OrderSide.Buy, qty);
            RunTo(m, ex, MarketCatalogue.PhaseOpen);
            goneAfter += s.ExDone && MarketLife.Accrued(s, ex, 60) < 1e-9 ? 1 : 0;
            Trade(m, s, OrderSide.Sell, qty);
            // The second buyer, in a twin market, buys on the morning after the record day.
            var other = new MarketWorld(seed);
            other.Purse.Money = 1000000000;
            MarketEngine twin = NewMarket(seed, 60, 1, 0, other);
            RunTo(twin, ex, MarketCatalogue.PhaseOpen + 2);
            Trade(twin, twin.State.Stock(s.Id), OrderSide.Buy, qty);
            RunTo(twin, pay);
            RunTo(m, pay);
            bool firstPaid = m.State.Trades.Any(t => t.Kind == TradeKind.Dividend && t.StockId == s.Id && t.Value == qty * cents / 100);
            bool secondPaid = twin.State.Trades.Any(t => t.Kind == TradeKind.Dividend && t.StockId == s.Id);
            entitled += firstPaid ? 1 : 0;
            wrong += secondPaid || !firstPaid ? 1 : 0;
        }
        Check(cases >= 6 && wrong == 0, "market: a dividend goes to whoever held at the close of its record day, sold or not since, and not to who bought after (" + entitled + " paid, " + wrong + " wrong)");
        Check(wholeOnRecord == cases && goneAfter == cases, "market: the price holds the whole dividend on its record day and drops by all of it the morning after, so buying for it gains nothing");
    }

    private static void MarketSplits()
    {
        var world = new MarketWorld(61);
        world.Purse.Money = 1000000000;
        MarketEngine m = NewMarket(61, 60, 1, 0, world);
        SessionDay(m, 2, 10);
        Stock s = Liquid(m, MarketSize.Large);
        Trade(m, s, OrderSide.Buy, 999);
        Quote q = MarketTrading.QuoteOf(m.State, s, m.State.Phase);
        Order buy = Trade(m, s, OrderSide.Buy, 301, OrderType.Limit, q.Bid - 37, 0, OrderLife.UntilCancelled);
        Order stop = Trade(m, s, OrderSide.Sell, 333, OrderType.Stop, 0, q.Bid - 51, OrderLife.UntilCancelled);
        Position p = m.State.Position(s.Id);
        long shares = p.Shares, cost = p.Cost;
        double mid = s.Mid;
        long volume = s.Daily.Sum(c => c.V);
        int lastClose = s.Daily[s.Daily.Count - 1].C;
        int limit = buy.LimitCents, stopAt = stop.StopCents;
        long fairShares = s.Shares;
        MarketLife.Split(m.State, s, 3);
        Check(p.Shares == shares * 3 && p.Cost == cost, "market: a three-for-one split triples the shares held and leaves what they cost (" + p.Shares + ", " + p.Cost + ")");
        Check(Math.Abs(s.Mid * 3 - mid) < 1e-9 * mid && s.Shares == fairShares * 3, "market: the price is divided exactly and the shares in issue multiplied");
        Check(buy.Qty == 903 && buy.LimitCents == limit / 3 && stop.Qty == 999 && stop.StopCents == stopAt / 3 && stop.StopCents * 3 <= stopAt,
            "market: open orders are for three times the shares, their prices rounded so that none is easier to fill (" + buy.LimitCents + ", " + stop.StopCents + ")");
        Check(Math.Abs(s.Daily[s.Daily.Count - 1].C * 3 - lastClose) <= 2 && s.Daily.Sum(c => c.V) == volume * 3, "market: the history is in the new shares");
        // Splits come by themselves when a price runs high.
        Stock dear = m.State.Stocks.First(k => k.Listed && k.Size == MarketSize.Mid);
        dear.Mis += Math.Log(MarketCatalogue.SplitAboveCents * 1.5 / 100.0 / dear.Mid);
        dear.Last = dear.MidCents;
        RunTo(m, m.State.Day);
        Check(dear.SplitDay > m.State.Day && dear.SplitRatio >= 2, "market: a board announces a split when its shares trade high");
        int splitDay = dear.SplitDay, ratio = dear.SplitRatio;
        long dearShares = dear.Shares;
        RunTo(m, splitDay, MarketCatalogue.PhaseOpen);
        Check(dear.Shares == dearShares * ratio && dear.SplitDay == 0, "market: and it takes effect at the open of its day");
    }

    private static void MarketFailuresAndTakeovers()
    {
        // A failure: holders get what is left, here nothing; a holder's orders go.
        var world = new MarketWorld(71);
        world.Purse.Money = 1000000000;
        MarketEngine m = NewMarket(71, 60, 1, 0, world);
        SessionDay(m, 2, 10);
        Stock s = Liquid(m, MarketSize.Mid);
        Trade(m, s, OrderSide.Buy, 500);
        Order resting = Trade(m, s, OrderSide.Sell, 500, OrderType.Limit, s.Last * 2, 0, OrderLife.UntilCancelled);
        long before = world.Purse.Money;
        int indexBefore = m.State.IndexLast;
        m.Delist(s.Id, GoneReason.Failed, 0);
        Check(!s.Listed && m.State.Position(s.Id) == null && world.Purse.Money == before, "market: a company that fails pays nothing when nothing is left, and the holding is gone");
        Check(!resting.IsOpen && resting.Why == OrderWhy.CompanyGone, "market: its orders are cancelled");
        Check(m.State.Trades.Any(t => t.Kind == TradeKind.Payout && t.StockId == s.Id && t.Value == 0), "market: the account says what it paid (nothing)");
        RunTo(m, m.State.Day, m.State.Phase + 1);
        Check(Math.Abs(m.State.IndexLast - indexBefore) < indexBefore / 20, "market: the index does not jump when a company leaves it");
        // A failure that pays something: exactly the shares times what each is paid, rounded down.
        Stock t = Liquid(m, MarketSize.Mid);
        Trade(m, t, OrderSide.Buy, 333);
        long beforePaid = world.Purse.Money;
        m.Delist(t.Id, GoneReason.Failed, 17);
        Check(world.Purse.Money - beforePaid == 333 * 17 / 100, "market: holders get what is left, to the unit (" + (world.Purse.Money - beforePaid) + ")");
        // A takeover completes at the bid, or falls through and the price comes back down; the market prices the chance.
        int done = 0, failed = 0, rightPaid = 0;
        for (ulong seed = 81; seed <= 100; seed++)
        {
            var w = new MarketWorld(seed);
            w.Purse.Money = 1000000000;
            MarketEngine e = NewMarket(seed, 60, 1, 0, w);
            int day = SessionDay(e, 2, 30);
            Stock target = Liquid(e, MarketSize.Small);
            e.BidFor(target.Id);
            RunTo(e, MarketTrading.NextTradingDay(day), MarketCatalogue.PhaseOpen + 5);
            Check(target.Mid * 100 < target.BidCents * 1.02 && target.Mid * 100 > target.BidCents * 0.85, "market: a company under a bid trades close to the bid (" + target.MidCents + " against " + target.BidCents + ")");
            Trade(e, target, OrderSide.Buy, 200);
            int bid = target.BidCents;
            RunTo(e, target.BidEndDay);
            if (!target.Listed)
            {
                done++;
                rightPaid += e.State.Trades.Any(r => r.Kind == TradeKind.Payout && r.StockId == target.Id && r.Value == 200L * bid / 100 && r.Qty == 200) ? 1 : 0;
            }
            else
            {
                failed++;
            }
        }
        Check(done > 10 && failed > 0 && rightPaid == done, "market: takeovers complete at the bid, paid to the unit, and some fall through (" + done + " completed, " + failed + " fell through)");
    }

    // News stays and mood fades: the close never moves a price; a company's own news moves its price at the next open
    // and its business for good; a share that trades under a unit for weeks leaves the market at its last price.
    private static void MarketNewsAndMood()
    {
        MarketEngine m = NewMarket(171, 60);
        int moved = 0, checkedPrices = 0;
        for (int d = 2; d <= 40; d++)
        {
            RunTo(m, d);
            if (!MarketCatalogue.TradingDay(d))
            {
                continue;
            }
            var newsAfterClose = new HashSet<int>(WorldOf(m).Heard.Where(n => n.Day == d && (n.Kind == MarketNewsKind.Crisis || n.Kind == MarketNewsKind.Windfall || n.Kind == MarketNewsKind.Bid)).Select(n => n.Stock));
            foreach (Stock s in m.State.Stocks)
            {
                if (!s.Listed || s.BidCents > 0 || newsAfterClose.Contains(s.Id))
                {
                    continue;   // (news after the close is the price from the next open)
                }
                checkedPrices++;
                moved += Math.Abs(Math.Exp(s.LogFair + s.Mis) * 100.0 - s.Last) > 0.5 + s.Last * 1e-9 ? 1 : 0;
                moved += Math.Abs(s.Mis - s.Sent) > 1e-12 ? 1 : 0;
            }
        }
        Check(checkedPrices > 3000 && moved == 0, "market: the day's news goes into what the market expects at the close without moving any price, and the mood is all that is left");
        // A crisis after the close: the business loses part of its usual margin for good; the price falls at the next open
        // by what that does to its worth.
        int day = TradingOnOrAfter(45);
        RunTo(m, day);
        Stock c = Liquid(m, MarketSize.Mid);
        double shift = c.MarginShift, worthBefore = MarketValuation.Fair(c, m.State.Env);
        int lastBefore = c.Last;
        Check(m.NewsFor(c.Id, true) && c.MarginShift < shift, "market: a crisis takes part of a company's usual margin for good");
        double fall = 1.0 - MarketValuation.Fair(c, m.State.Env) / worthBefore;
        var told = WorldOf(m).Heard.LastOrDefault(n => n.Kind == MarketNewsKind.Crisis && n.Stock == c.Id);
        Check(fall > 0.1 && told.Stock == c.Id && Math.Abs(told.A - fall * 10000.0) < 2.0, "market: and the news says what it does to its worth (" + (fall * 100).ToString("0") + "%)");
        Check(c.Last == lastBefore, "market: nothing moves before the market has heard it at the open");
        RunTo(m, MarketTrading.NextTradingDay(day), MarketCatalogue.PhaseOpen);
        double atOpen = c.Open / (double)lastBefore - 1.0;
        Check(atOpen < -fall * 0.6 && atOpen > -fall * 1.6, "market: its price falls by about that at the next open (" + (atOpen * 100).ToString("0") + "% against " + (-fall * 100).ToString("0") + "%)");
        Stock w = Liquid(m, MarketSize.Mid, 1);
        double worthW = MarketValuation.Fair(w, m.State.Env);
        Check(m.NewsFor(w.Id, false) && MarketValuation.Fair(w, m.State.Env) > worthW * 1.05, "market: a windfall lifts a company's worth for good");
        // A share under a unit for four weeks running leaves the market at its last price; a holder is paid that.
        var world = new MarketWorld(172);
        world.Purse.Money = 100000000;
        MarketEngine e = NewMarket(172, 60, 1, 0, world);
        SessionDay(e, 2, 10);
        Stock p = Liquid(e, MarketSize.Small);
        Trade(e, p, OrderSide.Buy, 1000);
        double toPenny = Math.Log(0.6 / p.Mid);
        p.Mis += toPenny;
        p.Sent += toPenny;
        p.Last = p.MidCents;
        int start = e.State.Day;
        for (int d = start; d <= start + 60 && p.Listed; d++)
        {
            RunTo(e, d);
            if (p.Listed && p.Last >= MarketCatalogue.PennyCents)
            {
                p.Mis -= 0.5;   // (kept under a unit, as a company in trouble would be)
                p.Sent -= 0.5;
            }
        }
        Check(!p.Listed && p.Reason == GoneReason.Delisted, "market: shares that trade under a unit for weeks leave the market");
        Check(e.State.Trades.Any(t => t.Kind == TradeKind.Payout && t.StockId == p.Id && t.Value == 1000L * p.GoneCents / 100 && p.GoneCents > 0),
            "market: and their holders are paid the last price (" + p.GoneCents + " cents)");
    }

    private static void MarketCaps()
    {
        var world = new MarketWorld(91);
        world.Purse.Money = 100000000000;
        MarketEngine m = NewMarket(91, 60, 1, 0, world);
        SessionDay(m, 2, 10);
        Stock s = Liquid(m, MarketSize.Small);
        Check(Why(m, s, OrderSide.Buy, MarketTrading.MostPerOrder(s) + 1) == OrderWhy.TooLarge, "market: an order of more than a fifth of a day's volume is refused");
        Check(Why(m, s, OrderSide.Sell, 10) == OrderWhy.NoShares, "market: nothing can be sold that is not held (a cash account)");
        Trade(m, s, OrderSide.Buy, 100);
        Check(Why(m, s, OrderSide.Sell, 60, OrderType.Limit, s.Last * 3) == OrderWhy.None && Why(m, s, OrderSide.Sell, 60, OrderType.Limit, s.Last * 3) == OrderWhy.NoShares,
            "market: shares asked to be sold in one order cannot be sold again in another");
        // The holding limit: a tenth of the company.
        long most = MarketTrading.MostHeld(s);
        Stock big = Liquid(m, MarketSize.Large);
        long room = MarketTrading.MostHeld(big);
        Check(most > 0 && room > most && MarketTrading.MostHeld(big) == big.Shares / 10, "market: a holding is at most a tenth of a company's shares");
        var p = new Position { StockId = big.Id, Shares = room - 5, Cost = 1 };
        m.State.Positions.Add(p);
        Check(Why(m, big, OrderSide.Buy, 10) == OrderWhy.HoldingLimit, "market: an order that would take a holding past it is refused");
        m.State.Positions.Remove(p);
        var poor = new MarketWorld(92);
        MarketEngine broke = NewMarket(92, 60, 1, 0, poor);
        SessionDay(broke, 2, 10);
        Check(Why(broke, Liquid(broke, MarketSize.Large), OrderSide.Buy, 10) == OrderWhy.NoMoney, "market: a purchase that cannot be paid for is refused");
        for (int i = 0; i < MarketCatalogue.OrdersMost; i++)
        {
            Trade(m, big, OrderSide.Buy, 1, OrderType.Limit, Math.Max(1, big.Last / 4), 0, OrderLife.UntilCancelled);
        }
        Check(Why(m, big, OrderSide.Buy, 1, OrderType.Limit, Math.Max(1, big.Last / 4)) == OrderWhy.TooMany, "market: no more than " + MarketCatalogue.OrdersMost + " open orders");
        Check(Why(m, big, OrderSide.Buy, 1, OrderType.Limit, 0) == OrderWhy.BadPrice || Why(m, big, OrderSide.Buy, 1, OrderType.Limit, 0) == OrderWhy.TooMany, "market: a limit of nothing is no order");
    }

    // No money is ever made by rounding: every purchase is rounded up and every sale down, every fee up, every dividend down.
    private static void MarketRounding()
    {
        var world = new MarketWorld(101);
        world.Purse.Money = 1000000000;
        MarketEngine m = NewMarket(101, 60, 1, 0, world);
        SessionDay(m, 2, 10);
        int bad = 0, fills = 0;
        var rnd = new Random(7);
        for (int i = 0; i < 400; i++)
        {
            Stock s = m.State.Stocks[rnd.Next(m.State.Stocks.Count)];
            if (!s.Listed)
            {
                continue;
            }
            long qty = 1 + rnd.Next(37);
            bool selling = m.State.Held(s.Id) >= qty && rnd.Next(2) == 0;
            Order o = Trade(m, s, selling ? OrderSide.Sell : OrderSide.Buy, qty);
            if (o == null || o.State != OrderState.Filled)
            {
                continue;
            }
            Trade t = m.State.Trades[m.State.Trades.Count - 1];
            fills++;
            double exact = qty * t.PriceCents / 100.0;
            long value = Math.Abs(t.Value);
            bad += selling ? (value > exact ? 1 : 0) : (value < exact ? 1 : 0);
            bad += t.Fee < Math.Min(value, value * MarketCatalogue.CommissionBp / 10000.0) || t.Fee < Math.Min(value, (long)world.Least) ? 1 : 0;
        }
        Check(fills > 200 && bad == 0, "market: a purchase is rounded up, a sale down and a commission up, every time (" + fills + " trades)");
        Check(MarketTrading.Value(3, 333, true) == 10 && MarketTrading.Value(3, 333, false) == 9 && MarketTrading.Commission(1, 5) == 5 && MarketTrading.Commission(100000, 5) == 100,
            "market: three shares at 3.33 cost 10 and fetch 9; the commission is at least the least and a tenth of a per cent");
        // A holding bought and sold in many small pieces never comes out ahead of the market's own prices.
        long spent = 0, back = 0;
        var w2 = new MarketWorld(102);
        w2.Purse.Money = 1000000000;
        MarketEngine e = NewMarket(102, 60, 1, 0, w2);
        SessionDay(e, 2, 10);
        Stock k = Liquid(e, MarketSize.Large);
        for (int i = 0; i < 50; i++)
        {
            long a = w2.Purse.Money;
            Trade(e, k, OrderSide.Buy, 3);
            spent += a - w2.Purse.Money;
        }
        for (int i = 0; i < 50; i++)
        {
            long a = w2.Purse.Money;
            Trade(e, k, OrderSide.Sell, 3);
            back += w2.Purse.Money - a;
        }
        Check(back < spent, "market: fifty purchases of three shares and fifty sales give back less than they cost (" + back + " of " + spent + ")");
    }

    // Placing and cancelling orders does nothing: no money, no move of a price.
    private static void MarketCancelling()
    {
        var world = new MarketWorld(111);
        world.Purse.Money = 100000000;
        MarketEngine m = NewMarket(111, 60, 1, 0, world);
        MarketEngine twin = NewMarket(111, 60, 1, 0, new MarketWorld(111) { Purse = { Money = 100000000 } });
        int day = SessionDay(m, 2, 10);
        SessionDay(twin, 2, 10);
        for (int i = 0; i < 200; i++)
        {
            Stock s = m.State.Stocks[i % m.State.Stocks.Count];
            if (!s.Listed)
            {
                continue;
            }
            Order o = Trade(m, s, OrderSide.Buy, 50, OrderType.Limit, Math.Max(1, s.Last * 9 / 10), 0, OrderLife.UntilCancelled);
            if (o != null)
            {
                MarketTrading.Cancel(m.State, o.Id);
            }
        }
        RunTo(m, day + 10);
        RunTo(twin, day + 10);
        bool same = world.Purse.Money == 100000000;
        for (int i = 0; i < m.State.Stocks.Count; i++)
        {
            Stock a = m.State.Stocks[i];
            Stock b = twin.State.Stock(a.Id);
            same &= b != null && a.Last == b.Last && Math.Abs(a.Mis - b.Mis) < 1e-12;
        }
        Check(same, "market: two hundred orders placed and cancelled leave the money and every price as they were");
        Check(!MarketTrading.Cancel(m.State, 999999), "market: an order that is not there cannot be cancelled");
    }
}
