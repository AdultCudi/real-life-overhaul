using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using RealLifeOverhaul.Markets;

// A large investor's tools (Scripts/Markets: InvestRules, MarketEngine.Invest.cs, the bonds' rollover and ladder in
// MarketEngine.Bonds.cs and BondRules, MarketStore's part g): what each holding is and the spread across kinds on generated
// portfolios, what it pays over a year and the next payments; the most of one bond purchase at its edges; worked orders
// (each slice within a fifth of its step's own trading, done exactly when the sum is, stopped by the cash, the holding
// limit, the shares held or the margin, cancelled cleanly, one history line a day, their moves sent as a guest's trades
// are, the same on a seed and whether the market is played in one go or in pieces); a dividend put back (the shares and
// the cash of a payout and a market order of that size); a rollover at maturity and a ladder's pieces; the save's round
// trip with and without the new fields; their cost; and every new text in every language. Every expected figure is
// worked out from the rules or by hand.
internal static partial class Program
{
    private static void Invest()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        InvestKinds();
        InvestEmpty();
        InvestBondMost();
        InvestSliceRules();
        InvestTrustLimit();
        InvestWorkedSteps();
        InvestWorkedStops();
        InvestWorkedRefusals();
        InvestWorkedCancel();
        InvestWorkedLines();
        InvestWorkedGuest();
        InvestWorkedSplit();
        InvestPutBackRules();
        InvestPutBackMarket();
        InvestRollover();
        InvestLadder();
        InvestSameEverywhere();
        InvestInPieces();
        InvestStore();
        InvestCost();
        InvestWords();
        InvestHoldingRoom();
        InvestTwoWorked();
        InvestFillGuard();
        InvestPutBackRoom();
        InvestDayMoney();
        InvestGuestLines();
        InvestOptionOffOpen();
        InvestRollTexts();
        InvestBondCount();
        InvestPieces();
        InvestTestRefuses();
        InvestWorthWiring();
        InvestCashRunsOut();
        InvestBrokerStaysYours();
        InvestTakeBack();
        InvestForceTakesBack();
        InvestBusyList();
        InvestBackGivesWay();
        InvestBackReason();
        InvestSaveTools();
        InvestStandDown();
        InvestFloatFills();
        InvestFloatLadders();
        InvestFloatCharges();
        InvestWorkedPlacement();
        Console.WriteLine("  a large investor's tools: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // A market on a market day at a step of its session, with so much cash.
    private static MarketEngine InvestMarket(ulong seed, long money, out MarketWorld world, int from = 3, int step = 5)
    {
        world = new MarketWorld(seed);
        world.Purse.Money = money;
        MarketEngine m = NewMarket(seed, 60, 1, 0, world);
        SessionDay(m, from, step);
        return m;
    }

    private static Order Worked(MarketEngine m, Stock s, OrderSide side, long qty, out OrderWhy why)
    {
        return m.PlaceWorked(s.Id, side, qty, m.Held, false, out why);
    }

    // Plays the market until an order is no longer open (or so many market days have passed); the market days it took.
    private static int UntilDone(MarketEngine m, Order o, int mostDays = 80)
    {
        int day = m.State.Day, days = 0;
        while (o.IsOpen && days < mostDays)
        {
            day = TradingOnOrAfter(day + 1);
            RunTo(m, day);
            days++;
        }
        return days;
    }

    // ---------------------------------------------------------------- what each holding is, and the spread

    private static void InvestKinds()
    {
        int portfolios = 0, rows = 0, rowsMatch = 0, kinds = 0, worthOk = 0, yearOk = 0, sumsOk = 0, nextOk = 0, worthAll = 0, shortsOk = 0;
        var list = new List<PortfolioRow>();
        var sums = new PortfolioSums();
        var next = new List<ComingPayment>();
        for (ulong seed = 1; seed <= 6; seed++)
        {
            MarketEngine m = InvestMarket(seed * 7 + 300, 2000000000L, out MarketWorld world);
            var rng = new Random((int)seed);
            List<Stock> listed = m.State.Stocks.Where(k => k.Listed && k.BidCents <= 0).ToList();
            for (int i = 0; i < 6; i++)
            {
                Stock k = listed[rng.Next(listed.Count)];
                Trade(m, k, OrderSide.Buy, Math.Max(1, MarketTrading.MostPerOrder(k) / (2 + rng.Next(5))));
            }
            foreach (Stock t in Trusts(m).Take(2))
            {
                Trade(m, t, OrderSide.Buy, 500 + rng.Next(500));
            }
            m.BuyBond(OfferOf(m, 2), 100000, 0, m.Trading, true, out _);
            m.BuyBond(OfferOf(m, 12), 50000 + rng.Next(50000), 50, m.Trading, true, out _);
            m.BuyBond(OfferOf(m, 22, rng.Next(3)), 20000, 0, m.Trading, true, out _);
            m.Hire(1 + (int)(seed % 4), 400000, 25, 10, 35, out _);
            if (seed == 3)
            {
                // A margin account with a short: owed, not held.
                var a = m.State.Account;
                a.Margin = true;
                a.Cash = 5000000;
                Stock big = Liquid(m, MarketSize.Large, 3);
                if (big != null && MarketTrading.Borrowable(m.State, big, m.State.Day))
                {
                    Trade(m, big, OrderSide.Sell, 200);
                }
            }
            RunTo(m, m.State.Day + 10 + (int)seed, MarketCatalogue.PhaseOpen + 20);
            MarketState s = m.State;
            InvestRules.Holdings(s, s.Phase, list);
            InvestRules.Sums(list, sums);
            portfolios++;
            int positions = s.Positions.Count(p => p.Shares != 0), botHoldings = s.Bots.Sum(b => b.Holdings.Count(h => h.Shares > 0)), bonds = s.Bonds.Holdings.Count;
            rows += list.Count;
            rowsMatch += list.Count == positions + botHoldings + bonds ? 1 : 0;
            long total = 0, shorts = 0, owed = 0;
            var byKind = new long[(int)HoldingKind.Count];
            foreach (PortfolioRow r in list)
            {
                Stock k = r.Stock > 0 ? s.Stock(r.Stock) : null;
                BondHolding h = r.Bond > 0 ? s.Bonds.Holding(r.Bond) : null;
                BondDef d = h != null ? BondCatalogue.Find(h.Def) : null;
                HoldingKind want = k != null ? (TrustRules.Is(k) ? HoldingKind.Trust : HoldingKind.Stock)
                    : d.Kind == BondKind.Treasury ? HoldingKind.Treasury : d.Kind == BondKind.City ? HoldingKind.City : HoldingKind.Deposit;
                kinds += r.Kind == want ? 1 : 0;
                long worth, year;
                if (h != null)
                {
                    worth = m.BondSale(h) / 100;
                    year = 0;
                    for (int n = h.Paid + 1; n <= h.Payments; n++)
                    {
                        year += BondRules.PayDay(h, n) <= s.Day + s.YearDays ? BondRules.InterestCents(h, d) : 0;
                    }
                }
                else
                {
                    Quote q = MarketTrading.QuoteOf(s, k, s.Phase);
                    worth = r.Shares > 0 ? MarketTrading.Value(r.Shares, q.Bid, false) : -MarketTrading.Value(-r.Shares, q.Ask, true);
                    year = r.Shares * k.DpsCents * 4L;
                    bool same = r.Firm > 0 ? s.Bots.First(b => b.Firm == r.Firm).Holding(k.Id).Shares == r.Shares : s.Position(k.Id).Shares == r.Shares;
                    worth = same ? worth : long.MinValue;
                }
                worthOk += r.Worth == worth ? 1 : 0;
                yearOk += r.YearCents == year ? 1 : 0;
                if (r.Shares < 0)
                {
                    shorts += -worth;
                    owed += -r.YearCents;
                }
                else
                {
                    total += worth;
                    byKind[(int)r.Kind] += worth;
                }
            }
            bool shares = true;
            int bpSum = 0, held = 0;
            for (int k = 0; k < (int)HoldingKind.Count; k++)
            {
                shares &= sums.Worth[k] == byKind[k];
                bpSum += sums.ShareBp((HoldingKind)k);
                held += sums.Count[k] > 0 ? 1 : 0;
            }
            sumsOk += shares && sums.Total == total && bpSum <= 10000 && bpSum >= 10000 - held && sums.YieldBp == (int)((sums.TotalYearCents - sums.OwedCents) * 100L / Math.Max(1, sums.Total)) ? 1 : 0;
            shortsOk += sums.Shorts == shorts && sums.OwedCents == owed ? 1 : 0;
            int all = InvestRules.NextPayments(list, next, 3);
            bool sorted = next.Count <= 3 && all == list.Count(r => r.Next.Day > 0);
            for (int i = 1; i < next.Count; i++)
            {
                sorted &= next[i - 1].Day <= next[i].Day;
            }
            nextOk += sorted && (all == 0 || next.Count == Math.Min(3, all)) ? 1 : 0;
            worthAll += InvestRules.MarketWorth(s, s.Phase) == list.Sum(r => r.Worth) + (s.Account.Margin ? s.Account.Cash : 0) ? 1 : 0;
        }
        Console.WriteLine("  invest: " + portfolios + " portfolios of " + rows + " holdings in all, of every kind");
        Check(rowsMatch == portfolios && rows > 40, "invest: every holding is a row of the Portfolio tab, once: the player's shares, each bot's and each bond (" + rowsMatch + " of " + portfolios + ")");
        Check(kinds == rows, "invest: each holding is what it is: a stock, a property trust, a Treasury bond, a city bond or a deposit (" + kinds + " of " + rows + ")");
        Check(worthOk == rows && yearOk == rows, "invest: each one's worth is at the bid (a bond what it brings sold now) and its income over a year its dividend four times a share or its payments in the year ("
                                                 + worthOk + ", " + yearOk + " of " + rows + ")");
        Check(sumsOk == portfolios, "invest: the spread across kinds adds up to the whole, each kind's share of it to ten thousand basis points, and the yield is the year's income on the worth ("
                                    + sumsOk + " of " + portfolios + ")");
        Check(shortsOk == portfolios, "invest: shares sold short are owed, apart from what is held, with the dividends they will pay (" + shortsOk + " of " + portfolios + ")");
        Check(nextOk == portfolios, "invest: the next payments are the earliest first, three at most, of every holding with one (" + nextOk + " of " + portfolios + ")");
        Check(worthAll == portfolios, "invest: what the player holds on the market is the holdings' worth together, with the margin account's own money (" + worthAll + " of " + portfolios + ")");
    }

    private static void InvestEmpty()
    {
        var s = new MarketState();
        var list = new List<PortfolioRow> { new PortfolioRow() };
        var sums = new PortfolioSums();
        InvestRules.Holdings(s, MarketCatalogue.PhaseOpen, list);
        InvestRules.Sums(list, sums);
        var next = new List<ComingPayment> { new ComingPayment() };
        Check(list.Count == 0 && sums.Total == 0 && sums.Net == 0 && sums.Gain == 0 && sums.YieldBp == 0 && sums.ShareBp(HoldingKind.Stock) == 0 && InvestRules.NextPayments(list, next, 3) == 0
              && next.Count == 0 && InvestRules.MarketWorth(s, MarketCatalogue.PhaseOpen) == 0,
            "invest: nothing held is no rows, no worth, no yield, no share of anything and no payment");
        Check(InvestRules.KindOf((BondDef)null) == HoldingKind.Treasury && InvestRules.IsBond(HoldingKind.Deposit) && !InvestRules.IsBond(HoldingKind.Trust) && !InvestRules.IsBond(HoldingKind.Count)
              && InvestRules.YearCentsOf(null) == 0 && InvestRules.YearInterestCents(null, null, 1, 60) == 0,
            "invest: the kinds' edges: a bond of no kind is the Treasury's, a trust is no bond, nothing pays nothing");
    }

    // ---------------------------------------------------------------- the most of one bond purchase

    private static void InvestBondMost()
    {
        bool flat = true, monotone = true, capped = true, edges = true;
        foreach (int level in new[] { 100, 1000, 1370, 3000 })
        {
            long least = BondRules.Most(level), cap = BondRules.Cap(level);
            flat &= BondRules.Most(level, 0) == least && BondRules.Most(level, -5000000) == least && BondRules.Most(level, least * 4 - 400) == least && cap > least;
            edges &= BondRules.Most(level, least * 4) == least && BondRules.Most(level, least * 4 + 400) == least + 100 && BondRules.Most(level, cap * 4) == cap
                     && BondRules.Most(level, cap * 4 + 400) == cap && BondRules.Most(level, long.MaxValue) == cap && cap == (long)BondCatalogue.MostCapAtStart * level / 1000;
            long last = 0;
            for (long worth = 0; worth < cap * 6; worth += cap / 37 + 1)
            {
                long most = BondRules.Most(level, worth);
                monotone &= most >= last;
                capped &= most >= least && most <= cap;
                last = most;
            }
        }
        Check(flat, "bonds: with nothing worth counting the most of one purchase is the flat sum, and under four times it as well");
        Check(edges, "bonds: from four times the flat sum the most is a quarter of what the player is worth, up to the cap, and no worth overflows it");
        Check(monotone && capped, "bonds: the most never falls as what the player is worth grows, and stays between the flat sum and the cap");
        Check(BondCatalogue.MostWorthPct == 25 && BondCatalogue.HoldingsMost == 24 && BondCatalogue.HoldingsMostLarge == 48 && BondCatalogue.HoldingsMostLarge <= 2 * 24
              && BondRules.HoldingsMost(false) == 24 && BondRules.HoldingsMost(true) == 48,
            "bonds: a quarter of the player's worth, and 24 held at once or 48 with the option on, which a version from before reads whole (it reads twice its own 24)");
        MarketEngine m = BondMarket(321, 60, 2000000000L, out MarketWorld world);
        BondOffer o = OfferOf(m, 2);
        long flatNow = BondRules.Most(m.State.Env.PriceLevel), rich = flatNow * 20;
        Check(m.CanBuyBond(o, flatNow * 3, true, true) == BondWhy.TooMuch && m.CanBuyBond(o, flatNow * 3, true, true, rich) == BondWhy.None
              && m.CanBuyBond(o, flatNow * 3, true, true, 0) == BondWhy.TooMuch && m.CanBuyBond(o, flatNow * 5 + 1, true, true, rich) == BondWhy.TooMuch,
            "bonds: three times the flat sum is refused for a beginner and bought by a player worth twenty times it, and a quarter of their worth is the most");
        BondHolding h = m.BuyBond(o, flatNow * 4, 0, true, true, out BondWhy why, rich, true);
        Check(h != null && why == BondWhy.None && h.Face == flatNow * 400 && h.Rollover, "bonds: a large purchase is bought, set to roll over as asked (" + why + ")");
        var book = new BondBook();
        for (int i = 0; i < BondCatalogue.HoldingsMostLarge - 3; i++)
        {
            book.Holdings.Add(new BondHolding { Id = i + 1 });
        }
        Check(BondRules.CanBuy(book, o, 1000, 1000000, true, false, true, 500, 10000, 3, 48) == BondWhy.None && BondRules.CanBuy(book, o, 1000, 1000000, true, false, true, 500, 10000, 4, 48) == BondWhy.Full,
            "bonds: a purchase of several holdings needs room for every one of them");
    }

    // ---------------------------------------------------------------- the rules of a worked order

    private static void InvestSliceRules()
    {
        Check(InvestRules.Slice(0, 1000) == 0 && InvestRules.Slice(-5, 1000) == 0 && InvestRules.Slice(500, 0) == 0 && InvestRules.Slice(500, -3) == 0 && InvestRules.Slice(500, 4) == 0,
            "worked: nothing is taken with nothing left, or of a step that traded nothing (or too little for a share at a fifth)");
        bool share = true;
        var rng = new Random(5);
        for (int i = 0; i < 2000; i++)
        {
            long left = rng.Next(1, 2000000), v = rng.Next(0, 5000000);
            long slice = InvestRules.Slice(left, v);
            share &= slice >= 0 && slice <= left && slice * 100 <= v * MarketCatalogue.OrderShareOfVolumePct && slice == Math.Min(left, v * MarketCatalogue.OrderShareOfVolumePct / 100);
        }
        Check(share, "worked: a slice is what is left, never more than a fifth of the step's own trading");
        Check(InvestRules.MarketDays(0, 1000) == 0 && InvestRules.MarketDays(1, 1000) == 1 && InvestRules.MarketDays(200, 1000) == 1 && InvestRules.MarketDays(201, 1000) == 2
              && InvestRules.MarketDays(1000000, 1000) == 5000 && InvestRules.MarketDays(5, 0) == 5,
            "worked: its market days are its shares over a fifth of a day's usual volume, rounded up");
        // Its slices together move the price as one order of their size: the square root's steps add up to its whole.
        double adv = 250000.0, recent = 0.0, sumSlices = 0.0, sumOrders = 0.0;
        long total = 0;
        bool firstSame = Math.Abs(MarketTrading.SliceShare(0.0, 4000, adv) - Math.Sqrt(4000 / adv)) < 1e-15;
        for (int k = 0; k < 78; k++)
        {
            long q = 600 + k * 7;
            sumSlices += MarketTrading.SliceShare(recent, q, adv);
            sumOrders += Math.Sqrt(q / adv);
            recent += q;
            total += q;
        }
        Check(firstSame, "worked: its first slice, with nothing traded just before, moves the price as an order of its size does");
        Check(Math.Abs(sumSlices - Math.Sqrt(total / adv)) < 1e-9 && sumSlices < sumOrders / 5.0,
            "worked: with nothing faded between them its slices move the price as one order of their whole size, far less than as orders of their own ("
            + sumSlices.ToString("0.0000") + " against " + sumOrders.ToString("0.0000") + ")");
        Check(MarketTrading.SliceShare(-50.0, 100, 0.0) == Math.Sqrt(100.0) && MarketTrading.SliceShare(1000.0, -7, 1000.0) == 0.0, "worked: no recent trading under nothing, no volume under one, no slice under nothing");
    }

    // Why a trust's holding limit stays a tenth of its shares: the wall a player met was one order's most, which a worked
    // order takes away; a tenth of any trust is months of a fifth of its trading and far more money than its usual day.
    private static void InvestTrustLimit()
    {
        MarketEngine m = InvestMarket(305, 1000, out _, 3, 5);
        var lines = new List<string>();
        bool far = true;
        foreach (Stock t in Trusts(m))
        {
            long order = MarketTrading.MostPerOrder(t), most = MarketTrading.MostHeld(t);
            int days = InvestRules.MarketDays(most, t.Adv);
            far &= days >= 60 && most > order * 50;
            lines.Add(t.Ticker + " one order " + order.ToString("N0") + ", a tenth " + most.ToString("N0") + " (" + days + " days, " + MarketTrading.Value(most, t.Last, false).ToString("N0") + ")");
        }
        Console.WriteLine("  trusts' limits: " + string.Join("; ", lines));
        Check(lines.Count >= 4 && far, "worked: a tenth of any trust is fifty orders' most and sixty market days of worked orders or more (over a year of 60 days): "
                                       + "the limit that stopped a player was one order's, not the holding's");
    }

    // A worked purchase played step by step: every slice is the step's own share, and it is done exactly at its size.
    private static void InvestWorkedSteps()
    {
        MarketEngine m = InvestMarket(311, 50000000000L, out MarketWorld world, 3, 5);
        Stock s = Liquid(m, MarketSize.Large);
        long per = MarketTrading.MostPerOrder(s), qty = per * 3 + 777;
        long cashBefore = world.Purse.Money;
        Order o = Worked(m, s, OrderSide.Buy, qty, out OrderWhy why);
        Check(o != null && why == OrderWhy.None && o.Worked && o.Life == OrderLife.UntilDone && o.Type == OrderType.Market && o.Filled == 0 && world.Purse.Money == cashBefore,
            "worked: a worked order is placed for three days' most at once, at the market, until done, and nothing is bought before the next step (" + why + ")");
        int steps = 0, within = 0, exact = 0, day = m.State.Day, days = 0;
        var dayFilled = new List<long>();
        var dayMost = new List<long>();
        for (int d = day; o.IsOpen && d < day + 40; d++)
        {
            if (!MarketCatalogue.TradingDay(d))
            {
                continue;
            }
            days++;
            long dayStart = o.Filled, most = 0;
            for (int k = d == day ? m.State.Phase - MarketCatalogue.PhaseOpen + 1 : 1; k <= MarketCatalogue.Steps && o.IsOpen; k++)
            {
                long left = o.Left, before = o.Filled;
                RunTo(m, d, MarketCatalogue.PhaseOpen + k);
                long took = o.Filled - before, v = s.Bars[s.BarCount - 1].V;
                steps++;
                within += took * 100 <= v * MarketCatalogue.OrderShareOfVolumePct ? 1 : 0;
                exact += took == InvestRules.Slice(left, v) ? 1 : 0;
                most += v * MarketCatalogue.OrderShareOfVolumePct / 100;
            }
            dayFilled.Add(o.Filled - dayStart);
            dayMost.Add(most);
            RunTo(m, d);
        }
        Console.WriteLine("  worked: " + qty + " shares (" + per + " one order's most) in " + steps + " steps over " + days + " market days: " + string.Join(", ", dayFilled));
        Check(steps > 150 && within == steps, "worked: no slice ever takes more than a fifth of its step's own trading (" + within + " of " + steps + ")");
        Check(exact == steps, "worked: each slice is exactly what is left or that fifth, with the cash and the room there (" + exact + " of " + steps + ")");
        Check(o.State == OrderState.Filled && o.Filled == qty && o.Left == 0 && m.State.Held(s.Id) == qty && o.EndDay > 0,
            "worked: it is done exactly when its sum is: every share bought, none more, the order filled");
        bool fullDays = true;
        for (int i = 1; i < dayFilled.Count - 1; i++)
        {
            fullDays &= dayFilled[i] == dayMost[i];
        }
        Check(days >= 3 && days <= 6 && fullDays, "worked: a whole day's slices come to a fifth of the day's trading, about one order's most (" + days + " days)");
        long paid = cashBefore - world.Purse.Money, fees = paid - o.FilledValue;
        long share = (o.FilledValue * MarketCatalogue.CommissionBp + 9999) / 10000;
        Check(fees >= share && fees <= share + world.Least + steps, "worked: it pays the commission's share of what it bought, the least of it once, as one order (" + fees + " against " + share + ")");
    }

    // Stopped by the cash, the holding limit, the shares held, the margin; and its option off.
    private static void InvestWorkedStops()
    {
        // The cash runs out.
        MarketEngine m = InvestMarket(331, 1, out MarketWorld world, 3, 5);
        Stock s = Liquid(m, MarketSize.Large, 1);
        long per = MarketTrading.MostPerOrder(s);
        world.Purse.Money = MarketTrading.Value(per * 13 / 10, s.Last, true);
        Order o = Worked(m, s, OrderSide.Buy, per * 5, out OrderWhy why);
        UntilDone(m, o, 20);
        Check(o != null && o.State == OrderState.Cancelled && o.Why == OrderWhy.NoMoney && o.Filled > per && o.Filled < per * 2 && m.State.Held(s.Id) == o.Filled
              && world.Purse.Money < MarketTrading.Value(2, s.Last * 2, true) + world.Least && o.EndDay > 0,
            "worked: when the cash runs out it buys what the cash pays for, then stops, what it bought kept (" + (o != null ? o.Why + ", " + o.Filled : "none") + ")");
        // The holding limit is reached.
        m = InvestMarket(332, 50000000000L, out world, 3, 5);
        s = Liquid(m, MarketSize.Small);
        per = MarketTrading.MostPerOrder(s);
        long most = MarketTrading.MostHeld(s);
        m.State.Positions.Add(new Position { StockId = s.Id, Shares = most - per * 2, Cost = MarketTrading.Value(most - per * 2, s.Last, true), OpenedDay = m.State.Day });
        o = Worked(m, s, OrderSide.Buy, per * 5, out why);
        UntilDone(m, o, 20);
        Check(o != null && o.Why == OrderWhy.HoldingLimit && m.State.Held(s.Id) == most && o.Filled == per * 2,
            "worked: it stops at the most of a company the player may hold, to the share (" + (o != null ? o.Why + ", held " + m.State.Held(s.Id) + " of " + most : "none") + ")");
        // The shares run out (a worked sale holds its shares back from other sales: here a stop loss, which holds nothing
        // back, took some of them meanwhile, as it may).
        m = InvestMarket(333, 50000000000L, out world, 3, 5);
        s = Liquid(m, MarketSize.Large, 2);
        per = MarketTrading.MostPerOrder(s);
        m.State.Positions.Add(new Position { StockId = s.Id, Shares = per * 3, Cost = MarketTrading.Value(per * 3, s.Last, true), OpenedDay = m.State.Day });
        o = Worked(m, s, OrderSide.Sell, per * 3, out why);
        Order other = Trade(m, s, OrderSide.Sell, per / 2);
        RunTo(m, m.State.Day, MarketCatalogue.PhaseOpen + 30);
        long soldSoFar = o != null ? o.Filled : 0;
        m.State.Position(s.Id).Shares = per / 2;
        UntilDone(m, o, 20);
        Check(o != null && other == null && o.Why == OrderWhy.NoShares && m.State.Held(s.Id) == 0 && o.Filled == soldSoFar + per / 2 && o.State == OrderState.Cancelled,
            "worked: a worked sale holds its shares back from other sales, sells what is still held, and stops when they run out, never selling short ("
            + (o != null ? o.Why + ", " + o.Filled : "none") + ")");
        // The margin.
        m = InvestMarket(334, 1000, out world, 3, 5);
        s = Liquid(m, MarketSize.Large);
        per = MarketTrading.MostPerOrder(s);
        var a = m.State.Account;
        a.Margin = true;
        a.Cash = MarketTrading.Value(per, s.Last, true);
        o = Worked(m, s, OrderSide.Buy, per * 6, out why);
        UntilDone(m, o, 20);
        Check(o != null && o.Why == OrderWhy.BelowMargin && o.Filled > per && o.Filled < per * 3 && MarketTrading.Equity(m.State, MarketCatalogue.PhaseClose) > 0,
            "worked: in a margin account it buys until its initial margin would be broken, then stops (" + (o != null ? o.Why + ", " + o.Filled : "none") + ")");
        // Its option off: no slice; cancelled with the reason.
        m = InvestMarket(335, 50000000000L, out world, 3, 5);
        s = Liquid(m, MarketSize.Large);
        o = Worked(m, s, OrderSide.Buy, MarketTrading.MostPerOrder(s) * 2, out why);
        m.LargeOn = false;
        RunTo(m, m.State.Day);
        bool idle = o.Filled == 0 && o.IsOpen;
        int stopped = m.StopAllOf(false, OrderWhy.Switched);
        Check(idle && stopped == 1 && o.State == OrderState.Refused && o.Why == OrderWhy.Switched && Worked(m, s, OrderSide.Buy, 100, out why) == null && why == OrderWhy.Switched,
            "worked: with the option off a worked order takes no slice, is cancelled with its reason, and none is placed");
    }

    private static void InvestWorkedRefusals()
    {
        MarketEngine m = InvestMarket(341, 50000000000L, out MarketWorld world, 3, 5);
        Stock s = Liquid(m, MarketSize.Large);
        int orders = m.State.Orders.Count, next = m.State.NextOrderId;
        Check(Worked(m, s, OrderSide.Buy, 0, out OrderWhy why) == null && why == OrderWhy.BadPrice, "worked: no shares is refused");
        Check(m.PlaceWorked(999999, OrderSide.Buy, 100, false, false, out why) == null && why == OrderWhy.NotListed, "worked: a company not listed is refused");
        Check(Worked(m, s, OrderSide.Sell, 100, out why) == null && why == OrderWhy.NoShares, "worked: a sale of shares not held is refused (it never sells short)");
        Check(m.PlaceWorked(s.Id, OrderSide.Buy, 100, true, false, out why) == null && why == OrderWhy.Held, "worked: nothing is placed while orders wait after a load");
        world.Purse.Money = 0;
        Check(Worked(m, s, OrderSide.Buy, 100, out why) == null && why == OrderWhy.NoMoney, "worked: a purchase from no cash is refused");
        world.Purse.Money = 50000000000L;
        m.State.Positions.Add(new Position { StockId = s.Id, Shares = MarketTrading.MostHeld(s), Cost = 1, OpenedDay = m.State.Day });
        Check(Worked(m, s, OrderSide.Buy, 100, out why) == null && why == OrderWhy.HoldingLimit, "worked: a purchase at the holding limit is refused");
        m.State.Positions.Clear();
        Check(m.State.Orders.Count == orders && m.State.NextOrderId == next, "worked: a refused order makes nothing");
        Quote q = MarketTrading.QuoteOf(m.State, s, m.State.Phase);
        for (int i = m.State.Orders.Count(x => x.IsOpen); i < MarketCatalogue.OrdersMost; i++)
        {
            Trade(m, s, OrderSide.Buy, 1, OrderType.Limit, Math.Max(1, q.Bid / 3), 0, OrderLife.UntilCancelled);
        }
        Check(Worked(m, s, OrderSide.Buy, 100, out why) == null && why == OrderWhy.TooMany, "worked: it counts among the open orders, at most " + MarketCatalogue.OrdersMost);
    }

    private static void InvestWorkedCancel()
    {
        MarketEngine m = InvestMarket(351, 50000000000L, out MarketWorld world, 3, 5);
        Stock s = Liquid(m, MarketSize.Large);
        Order o = Worked(m, s, OrderSide.Buy, MarketTrading.MostPerOrder(s) * 4, out _);
        int day = m.State.Day;
        RunTo(m, day, MarketCatalogue.PhaseOpen + 40);
        long filled = o.Filled;
        long heldBefore = o.Parked, cashBefore = world.Purse.Money;
        Check(filled > 0 && heldBefore > 0 && m.Cancel(o.Id) && o.State == OrderState.Cancelled && o.Why == OrderWhy.Cancelled && o.Filled == filled, "worked: cancelled, what it bought is kept");
        Check(o.Parked == 0 && world.Purse.Money == cashBefore + heldBefore, "worked: and the day's money held for it comes back at once, in one transaction");
        RunTo(m, TradingOnOrAfter(day + 1));
        Check(o.Filled == filled && m.State.Held(s.Id) == filled && o.EndDay == day, "worked: and nothing more is bought; it ended the day it was cancelled");
        RunTo(m, day + 10);
        Check(m.State.Order(o.Id) != null, "worked: a stopped order is kept for the app ten days after it ended");
        RunTo(m, TradingOnOrAfter(day + 11));
        Check(m.State.Order(o.Id) == null, "worked: then it goes");
        // It never expires: a worked order whose day of expiry passed is still worked.
        Order w = Worked(m, s, OrderSide.Buy, MarketTrading.MostPerOrder(s) * 9, out _);
        w.ExpireDay = m.State.Day;
        RunTo(m, TradingOnOrAfter(m.State.Day + 2));
        Check(w.IsOpen && w.Filled > 0 && w.State == OrderState.Open, "worked: it never expires: it stands until it is done, stopped or cancelled");
    }

    // One history line a day for a worked order's slices, with another order's trade between them.
    private static void InvestWorkedLines()
    {
        MarketEngine m = InvestMarket(361, 50000000000L, out MarketWorld world, 3, 5);
        Stock s = Liquid(m, MarketSize.Large), other = Liquid(m, MarketSize.Large, 1);
        Order o = Worked(m, s, OrderSide.Buy, MarketTrading.MostPerOrder(s) * 2 + 55, out _);
        RunTo(m, m.State.Day, MarketCatalogue.PhaseOpen + 30);
        Trade(m, other, OrderSide.Buy, 100);
        UntilDone(m, o, 10);
        List<Trade> lines = m.State.Trades.Where(t => t.Order == o.Id).ToList();
        bool oneADay = lines.GroupBy(t => t.Day).All(g => g.Count() == 1) && lines.Count >= 2;
        Check(oneADay && lines.Sum(t => t.Qty) == o.Filled && -lines.Sum(t => t.Value) == o.FilledValue && lines.All(t => t.Kind == TradeKind.Bought && t.StockId == s.Id),
            "worked: its slices are one line of the History tab a day, their shares, money and commission added up (" + lines.Count + " lines)");
        Check(lines.All(t => Math.Abs(t.PriceCents - (int)(-t.Value * 100 / t.Qty)) <= 1) && m.State.Trades.Count(t => t.StockId == other.Id && t.Order == 0) == 1,
            "worked: each line's price is its slices' average, and another order's trade between them is a line of its own");
    }

    // A guest of a Going Public session: each slice's lasting move goes to the host as a trade's does, none put on its own prices.
    private static void InvestWorkedGuest()
    {
        MarketEngine guest = InvestMarket(371, 50000000000L, out _, 3, 5);
        MarketEngine alone = InvestMarket(371, 50000000000L, out _, 3, 5);
        var sent = new List<(int Stock, int Micros, long Shares)>();
        guest.TradeSink = (stock, micros, shares) => sent.Add((stock, micros, shares));
        Stock s = Liquid(guest, MarketSize.Large);
        Order o = Worked(guest, s, OrderSide.Buy, MarketTrading.MostPerOrder(s) * 2, out _);
        int day = guest.State.Day;
        RunTo(guest, day);
        RunTo(alone, day);
        Check(sent.Count > 30 && sent.All(x => x.Stock == s.Id && x.Micros >= 0) && sent.Sum(x => x.Shares) == o.Filled,
            "worked: in a guest's market every slice's move goes to the host as any trade's does, the shares with it (" + sent.Count + " slices)");
        Check(MarketShare.Checksum(guest.State) == MarketShare.Checksum(alone.State), "worked: and the guest's own prices take none of it: they come back on the host's tape");
        // The host: its slices' moves are events of its tape, for every market at the next moment.
        MarketEngine host = InvestMarket(372, 50000000000L, out _, 3, 5);
        host.TradesAsEvents = true;
        Stock hs = Liquid(host, MarketSize.Large);
        Worked(host, hs, OrderSide.Buy, MarketTrading.MostPerOrder(hs), out _);
        RunTo(host, host.State.Day);
        string tape = host.Tape.Whole();
        Check(tape.Contains(" " + MarketInputs.EventTrade + " " + hs.Id + " "), "worked: a host's slices are trades on its tape, as its other fills are");
    }

    private static void InvestWorkedSplit()
    {
        MarketEngine m = InvestMarket(381, 50000000000L, out _, 3, 5);
        Stock s = Liquid(m, MarketSize.Large);
        Order o = Worked(m, s, OrderSide.Buy, MarketTrading.MostPerOrder(s) * 3, out _);
        RunTo(m, m.State.Day, MarketCatalogue.PhaseOpen + 40);
        long qty = o.Qty, filled = o.Filled, value = o.FilledValue;
        MarketLife.Split(m.State, s, 2);
        Check(o.Qty == qty * 2 && o.Filled == filled * 2 && o.Left == (qty - filled) * 2 && o.FilledValue == value && o.IsOpen, "worked: a split doubles what it is for and what it has bought, the money the same");
    }

    // ---------------------------------------------------------------- dividends put back

    private static void InvestPutBackRules()
    {
        bool share = true;
        foreach (long money in new long[] { 0, 1, 99, 100, 12345, 1000000000 })
        {
            foreach (int step in new[] { -1, 0, 1 })
            {
                foreach (int pct in new[] { -5, 0, 10, 25, 50, 60, 75, 100, 150 })
                {
                    int chosen = BondRules.ReinvestOf(Math.Max(0, Math.Min(100, pct)));
                    long want = (money - ListingVotes.DividendTax(money, step)) * chosen / 100;
                    long got = InvestRules.PutBack(money, step, pct);
                    share &= got == want && got <= money && got >= 0;
                }
            }
        }
        Check(share && InvestRules.PutBack(-50, 0, 100) == 0, "put back: the share chosen of a dividend after its tax, held to the choices (a 60 is a 50), nothing of nothing");
        Check(InvestRules.ReinvestChoices.SequenceEqual(new[] { 0, 25, 50, 75, 100 }), "put back: none, a quarter, half, three quarters or all of it, as a bond's interest");
        bool most = true;
        var rng = new Random(9);
        for (int i = 0; i < 3000; i++)
        {
            long money = rng.Next(0, 2000000);
            int cents = rng.Next(1, 500000), least = rng.Next(0, 20);
            long q = InvestRules.SharesFor(money, cents, least);
            long cost = MarketTrading.Value(q, cents, true), next = MarketTrading.Value(q + 1, cents, true);
            most &= q >= 0 && (q == 0 || cost + MarketTrading.Commission(cost, least) <= money) && next + MarketTrading.Commission(next, least) > money;
        }
        Check(most, "put back: the shares a sum buys are the most whose cost with the commission it pays for");
        Check(InvestRules.SharesFor(0, 100, 5) == 0 && InvestRules.SharesFor(100, 0, 5) == 0 && InvestRules.SharesFor(5, 100, 5) == 0 && InvestRules.SharesFor(6, 100, 5) == 1
              && InvestRules.SharesFor(1000000000000L, 1, 5) > 99000000000000L,
            "put back: nothing of nothing, nothing at no price, one share at its price and the least commission, and the largest sums whole");
    }

    // A trust's distribution put back: the shares and the cash are those of the payout and a market order of that size.
    private static void InvestPutBackMarket()
    {
        MarketEngine a = InvestMarket(391, 5000000000L, out MarketWorld wa, 3, 5);
        MarketEngine b = InvestMarket(391, 5000000000L, out MarketWorld wb, 3, 5);
        Stock ta = Trusts(a).OrderByDescending(t => t.Adv).First(), tb = b.State.Stock(ta.Id);
        Trade(a, ta, OrderSide.Buy, 4000);
        Trade(b, tb, OrderSide.Buy, 4000);
        Check(a.SetReinvest(ta.Id, 100, out OrderWhy why) && a.State.Position(ta.Id).Reinvest == 100 && !a.SetReinvest(Liquid(a, MarketSize.Large).Id, 50, out why) && why == OrderWhy.NoShares,
            "put back: a holding's share is set; one of nothing held is refused");
        a.SetReinvest(ta.Id, 60, out _);
        Check(a.State.Position(ta.Id).Reinvest == 50, "put back: held to the choices (a 60 is a 50)");
        a.SetReinvest(ta.Id, 100, out _);
        // Day by day until the first payout put back waits for the open.
        int day = a.State.Day, payDay = 0;
        Order back = null;
        long dividend = 0;
        for (int d = day; d < day + 30 && back == null; d++)
        {
            long before = a.State.Position(ta.Id).Dividends;
            RunTo(a, d);
            RunTo(b, d);
            back = a.State.Orders.FirstOrDefault(o => o.Back && o.IsOpen);
            if (back != null)
            {
                payDay = d;
                dividend = a.State.Position(ta.Id).Dividends - before;
            }
        }
        Check(back != null && back.Qty == 0 && back.Budget == InvestRules.PutBack(dividend, a.State.Env.Lever(MarketInputs.BusinessTaxLever), 100) && back.ExpireDay == MarketTrading.NextTradingDay(payDay)
              && b.State.Orders.All(o => !o.Back),
            "put back: a distribution paid at a close puts its share after tax back as an order for the next open; the same market with none puts back nothing (" + dividend + " paid)");
        if (back == null)
        {
            return;
        }
        long cashA = wa.Purse.Money, cashB = wb.Purse.Money;
        int open = TradingOnOrAfter(payDay + 1);
        RunTo(a, open, MarketCatalogue.PhaseOpen);
        long qty = back.Qty;
        // The other market: the payout as it was, then an order at the market of that size, placed while shut.
        Order plain = Trade(b, tb, OrderSide.Buy, qty);
        RunTo(b, open, MarketCatalogue.PhaseOpen);
        Trade la = a.State.Trades.Last(t => t.StockId == ta.Id && t.Kind == TradeKind.Bought), lb = b.State.Trades.Last(t => t.StockId == tb.Id && t.Kind == TradeKind.Bought);
        Check(cashA == cashB && back.State == OrderState.Filled && qty > 0 && plain != null && plain.State == OrderState.Filled && plain.Id == back.Id,
            "put back: at the open it buys " + qty + " shares, the order the other market places by hand for as many");
        Check(a.State.Held(ta.Id) == b.State.Held(tb.Id) && wa.Purse.Money == wb.Purse.Money && la.PriceCents == lb.PriceCents && la.Value == lb.Value && la.Fee == lb.Fee
              && la.Back && !lb.Back && ta.Last == tb.Last,
            "put back: the shares bought and the cash left are those of the payout and a market order of that size, its line marked as a dividend put back");
        long cost = -la.Value + la.Fee, nextValue = MarketTrading.Value(qty + 1, la.PriceCents, true);
        Check(cost <= back.Budget && nextValue + MarketTrading.Commission(nextValue, wa.Least) > back.Budget, "put back: as many shares as its money pays for, with the commission, and no more");
        // With the option off, a short, or no share chosen: nothing is put back.
        MarketEngine off = InvestMarket(391, 5000000000L, out _, 3, 5);
        Stock to = off.State.Stock(ta.Id);
        Trade(off, to, OrderSide.Buy, 4000);
        off.SetReinvest(to.Id, 100, out _);
        off.ReinvestOn = false;
        RunTo(off, payDay);
        Check(off.State.Orders.All(o => !o.Back) && off.State.Position(to.Id).Dividends > 0, "put back: with the option off every dividend is paid in cash");
        // The test: the next dividend owed paid now, and put back at once while the market trades.
        MarketEngine t = InvestMarket(392, 5000000000L, out _, 3, 5);
        Stock tt = Trusts(t).OrderByDescending(x => x.Adv).First();
        Trade(t, tt, OrderSide.Buy, MarketTrading.MostPerOrder(tt));
        t.SetReinvest(tt.Id, 100, out _);
        int due = 0, first = t.State.Day;
        for (int d = first; d < first + 20 && due == 0; d++)
        {
            RunTo(t, d, MarketCatalogue.PhaseOpen + 30);
            due = MarketCatalogue.TradingDay(d) && t.State.Dues.Any(x => x.Bot == 0 && x.Cents > 0) ? d : 0;
        }
        long heldBefore = t.State.Held(tt.Id);
        int paid = due > 0 ? t.PayNextDueNow() : -1;
        Trade lastLine = t.State.Trades.LastOrDefault();
        Check(paid == tt.Id && t.State.Dues.All(x => x.StockId != tt.Id || x.Bot != 0) && t.State.Held(tt.Id) > heldBefore && lastLine != null && lastLine.Back,
            "put back: the test pays the next dividend owed now and puts its share back at once while the market trades (due on day " + due + ", paid " + paid + ", held "
            + heldBefore + " to " + t.State.Held(tt.Id) + ")");
        Check(InvestMarket(393, 1000, out _, 3, 5).PayNextDueNow() == 0, "put back: and with nothing owed it does nothing");
    }

    // ---------------------------------------------------------------- a rollover and a ladder

    private static void InvestRollover()
    {
        MarketEngine m = BondMarket(401, 60, 100000000, out MarketWorld world);
        var told = new List<BondNote>();
        m.BondTold = n => told.Add(n);
        BondHolding h = m.BuyBond(OfferOf(m, 1), 100000, 100, m.Trading, true, out _, 0, true);
        long face = h.Face, carry = h.Carry, cash = world.Purse.Money;
        int id = h.Id, end = h.EndDay;
        long interest = BondRules.InterestCents(h, BondCatalogue.Find(1));
        RunTo(m, TradingOnOrAfter(end) + 1);
        BondHolding next = m.State.Bonds.Holdings.Count == 1 ? m.State.Bonds.Holdings[0] : null;
        BondNote rolled = told.FirstOrDefault(n => n.Kind == BondNoteKind.Rolled);
        BondRules.Share(interest, 0, 100, out long tax, out long back, out long paid);
        Check(next != null && next.Id != id && next.Def == 1 && next.Rollover && next.Reinvest == 100 && next.BoughtDay == TradingOnOrAfter(end)
              && next.EndDay == next.BoughtDay + BondRules.TermDays(BondCatalogue.Find(1), 60) && next.Payments == 1,
            "rollover: at its end a bond set to roll over is bought again in its place, the same kind and term from that day, still rolling over");
        Check(next != null && rolled.Kind == BondNoteKind.Rolled && rolled.Holding == next.Id && next.RateBp == rolled.B && next.Face == face + back + carry && rolled.A == next.Face / 100
              && world.Purse.Money == cash && told.All(n => n.Kind != BondNoteKind.Repaid),
            "rollover: with what it repays and its last interest's share put back, at the day's rate, nothing paid out and nothing repaid (" + (next != null ? next.Face - face : 0) + " cents more)");
        // Again at its next end.
        int second = next != null ? next.EndDay : 0;
        RunTo(m, TradingOnOrAfter(second) + 1);
        Check(m.State.Bonds.Holdings.Count == 1 && m.State.Bonds.Holdings[0].BoughtDay == TradingOnOrAfter(second) && told.Count(n => n.Kind == BondNoteKind.Rolled) == 2,
            "rollover: and it rolls over again at each end until it is changed");
        // With nothing put back the last interest is paid; with bonds off it is repaid.
        m = BondMarket(402, 60, 100000000, out world);
        h = m.BuyBond(OfferOf(m, 1), 100000, 0, m.Trading, true, out _, 0, true);
        face = h.Face;
        cash = world.Purse.Money;
        BondRules.Share(BondRules.InterestCents(h, BondCatalogue.Find(1)), 0, 0, out _, out _, out long net);
        RunTo(m, TradingOnOrAfter(h.EndDay) + 1);
        Check(m.State.Bonds.Holdings.Count == 1 && m.State.Bonds.Holdings[0].Face == face + net % 100 && world.Purse.Money == cash + net / 100,
            "rollover: with none of its interest put back, the last interest is paid out and the face rolls over, the cents under a unit with it ("
            + (m.State.Bonds.Holdings.Count == 1 ? m.State.Bonds.Holdings[0].Face - face : -1) + ")");
        m = BondMarket(403, 60, 100000000, out world);
        h = m.BuyBond(OfferOf(m, 1), 100000, 100, m.Trading, true, out _, 0, true);
        cash = world.Purse.Money;
        m.BondsOn = false;
        RunTo(m, TradingOnOrAfter(h.EndDay) + 1);
        Check(m.State.Bonds.Holdings.Count == 0 && world.Purse.Money >= cash + 100000, "rollover: with bonds and deposits off it is repaid at its end, as before");
        // A deposit at its bank.
        m = BondMarket(404, 60, 100000000, out world);
        h = m.BuyBond(OfferOf(m, 21, 1), 50000, 50, m.Trading, true, out _, 0, true);
        RunTo(m, TradingOnOrAfter(h.EndDay) + 1);
        BondHolding dep = m.State.Bonds.Holdings.FirstOrDefault();
        Check(dep != null && dep.Def == 21 && dep.Bank == 1 && dep.Issuer == "Granite Bank" && dep.Reinvest == 50, "rollover: a deposit rolls over at the same bank");
        // Not set: repaid. The test: the first rolling bond brought to its end now.
        m = BondMarket(405, 60, 100000000, out world);
        BondHolding plain = m.BuyBond(OfferOf(m, 2), 100000, 0, m.Trading, true, out _);
        BondHolding rolling = m.BuyBond(OfferOf(m, 3), 100000, 0, m.Trading, true, out _, 0, true);
        int count = m.State.Bonds.Holdings.Count;
        int ended = m.EndBondNow();
        Check(ended == rolling.Id && m.State.Bonds.Holdings.Count == count && m.State.Bonds.Holding(rolling.Id) == null && m.State.Bonds.Holding(plain.Id) != null
              && m.State.Bonds.Holdings.Any(x => x.Def == 3 && x.BoughtDay == m.State.Day),
            "rollover: the test brings the first bond set to roll over to its end now, and it rolls over");
        Check(m.SetBondRollover(plain.Id, true, out BondWhy why) && plain.Rollover && !m.SetBondRollover(999, true, out why) && why == BondWhy.NotHeld, "rollover: it is set on a bond held, any time");
    }

    private static void InvestLadder()
    {
        var terms = new List<BondDef>();
        int n = BondRules.LadderTerms(terms);
        Check(n == 4 && terms.Select(d => d.Id).SequenceEqual(new[] { 1, 2, 3, 4 }) && terms.All(d => d.Kind == BondKind.Treasury), "ladder: the Treasury's four terms, shortest first");
        var pieces = new long[8];
        bool even = true;
        foreach (long money in new long[] { 0, 3, 4, 5, 1000003, 999999999 })
        {
            BondRules.Ladder(money, 4, pieces);
            even &= pieces.Take(4).Sum() == money && pieces.Take(4).Max() - pieces.Take(4).Min() <= 1 && pieces.Skip(4).All(p => p == 0);
            for (int i = 1; i < 4; i++)
            {
                even &= pieces[i] <= pieces[i - 1];
            }
        }
        BondRules.Ladder(-5, 4, pieces);
        Check(even && pieces.All(p => p == 0), "ladder: a sum spread evenly, to the unit, the units left over one each to the shortest terms");
        MarketEngine m = BondMarket(411, 60, 100000000, out MarketWorld world);
        long cash = world.Purse.Money, money1 = 1000003;
        var offers = terms.Select(d => m.Offer(d.Id, -1, null)).ToList();
        int bought = m.BuyLadder(money1, 50, true, m.Trading, true, out BondWhy why);
        List<BondHolding> held = m.State.Bonds.Holdings;
        BondRules.Ladder(money1, 4, pieces);
        bool each = bought == 4 && held.Count == 4;
        for (int i = 0; each && i < 4; i++)
        {
            each = held[i].Def == terms[i].Id && held[i].Face == pieces[i] * 100 && held[i].RateBp == offers[i].YieldBp && held[i].Reinvest == 50 && held[i].Rollover
                   && held[i].EndDay == m.State.Day + BondRules.TermDays(terms[i], 60);
        }
        Check(each && world.Purse.Money == cash - money1, "ladder: one sum bought as a bond of each term, each its piece at its own term's rate, with the choices made (" + why + ")");
        long least = BondRules.Least(m.State.Env.PriceLevel), most = BondRules.Most(m.State.Env.PriceLevel);
        Check(m.LadderWhy(least * 4 - 1, true, true) == BondWhy.TooLittle && m.LadderWhy(least * 4, true, true) == BondWhy.None && m.LadderWhy(most * 4 + 1, true, true) == BondWhy.TooMuch
              && m.LadderWhy(most * 4 + 1, true, true, most * 40) == BondWhy.None && m.LadderWhy(least * 4, false, true) == BondWhy.Closed && m.LadderWhy(least * 4, true, false) == BondWhy.Off,
            "ladder: each piece between the least and the most of one purchase (the most by worth), only while the market trades and bonds are on");
        world.Purse.Money = least * 4 - 1 + least;
        Check(m.LadderWhy(least * 8, true, true) == BondWhy.NoCash, "ladder: the cash for the whole of it");
        world.Purse.Money = 100000000;
        while (m.State.Bonds.Holdings.Count < BondCatalogue.HoldingsMostLarge - 3)   // (the option is on: 48)
        {
            m.BuyBond(OfferOf(m, 2), least, 0, true, true, out _);
        }
        int count = m.State.Bonds.Holdings.Count;
        cash = world.Purse.Money;
        Check(m.BuyLadder(least * 4, 0, false, true, true, out why) == 0 && why == BondWhy.Full && m.State.Bonds.Holdings.Count == count && world.Purse.Money == cash,
            "ladder: room for every piece, and a refused ladder takes nothing");
    }

    // The same tools in two markets of the same city: the same on every machine.
    private static void InvestSameEverywhere()
    {
        string[] lines = new string[2];
        long[] cash = new long[2];
        for (int run = 0; run < 2; run++)
        {
            MarketEngine m = InvestScenario(421, 5000000000L, out MarketWorld world);
            RunTo(m, 70);
            lines[run] = MarketStore.Write(m.State);
            cash[run] = world.Purse.Money;
        }
        Check(lines[0] == lines[1] && cash[0] == cash[1] && lines[0].Length > 10000,
            "invest: worked orders, dividends put back, rollovers and a ladder come to the same in two markets of the same city");
    }

    // A market with every tool at work: a worked purchase and a worked sale, a trust's payouts put back, a bond rolling over and a ladder.
    private static MarketEngine InvestScenario(ulong seed, long money, out MarketWorld world)
    {
        MarketEngine m = InvestMarket(seed, money, out world, 3, 10);
        Stock s = Liquid(m, MarketSize.Large), sold = Liquid(m, MarketSize.Large, 1);
        Stock t = Trusts(m).OrderByDescending(x => x.Adv).First();
        Trade(m, sold, OrderSide.Buy, MarketTrading.MostPerOrder(sold));
        Trade(m, t, OrderSide.Buy, 3000);
        m.SetReinvest(t.Id, 75, out _);
        Worked(m, s, OrderSide.Buy, MarketTrading.MostPerOrder(s) * 3 + 11, out _);
        Worked(m, sold, OrderSide.Sell, MarketTrading.MostPerOrder(sold) - 5, out _);
        m.BuyBond(m.Offer(1, -1, null), 50000, 100, m.Trading, true, out _, 0, true);
        m.BuyLadder(400000, 25, true, m.Trading, true, out _);
        return m;
    }

    // Played in one go, a piece at a time, and with no time a frame: the very same market.
    private static void InvestInPieces()
    {
        bool same = true, played = true;
        foreach (ulong seed in new ulong[] { 431, 432 })
        {
            string whole = InvestPlayed(seed, (m, target) =>
            {
                int work = int.MaxValue;
                return m.RunTo(target, ref work);
            }, out int c1);
            string pieces = InvestPlayed(seed, (m, target) =>
            {
                int one = 1;
                return m.RunTo(target, ref one);
            }, out int c2);
            string timed = InvestPlayed(seed, (m, target) => m.RunFor(target, 0), out int c3);
            same &= whole == pieces && whole == timed;
            played &= c2 > c1 * 50 && c3 == c2 && whole.Length > 10000;
        }
        Check(played, "invest: two months of a market with every tool at work are played in one go, a piece at a time, and with no time a frame");
        Check(same, "invest: and come to the very same market: the slices, the payouts put back, the rollovers, the notes and the money");
    }

    private static string InvestPlayed(ulong seed, Func<MarketEngine, long, bool> go, out int calls)
    {
        calls = 0;
        MarketEngine m = InvestScenario(seed, 5000000000L, out MarketWorld world);
        var notes = new System.Text.StringBuilder();
        m.InvestTold = n => notes.Append((int)n.Kind).Append(',').Append(n.Order).Append(',').Append(n.Qty).Append(',').Append(n.Value).Append(',').Append(n.Done).Append(';');
        m.BondTold = n => notes.Append('b').Append((int)n.Kind).Append(',').Append(n.Holding).Append(',').Append(n.A).Append(';');
        for (int d = 4; d < 64; d += 7)
        {
            PaceGo(m, MarketState.Time(d, MarketCatalogue.PhaseOpen + 3), go, ref calls);
            Stock s = m.State.Stocks.Where(x => x.Listed).Skip(d % 25).First();
            Worked(m, s, OrderSide.Buy, MarketTrading.MostPerOrder(s) + 3, out _);
        }
        PaceGo(m, MarketState.Time(64, MarketCatalogue.PhaseClose), go, ref calls);
        return MarketStore.Write(m.State) + "#" + m.Tape.Whole() + "#" + notes + "#" + world.Purse.Money + "#" + world.Fills + "#" + world.FilledQty;
    }

    // ---------------------------------------------------------------- the save

    private static void InvestStore()
    {
        MarketEngine m = InvestScenario(441, 5000000000L, out _);
        // Every new field there: a worked order done and one open with its day's slices, lines of both, a payout waiting to be put back.
        int d1 = TradingOnOrAfter(29), d2 = TradingOnOrAfter(d1 + 1);
        RunTo(m, d1, MarketCatalogue.PhaseOpen + 10);
        Stock big = Liquid(m, MarketSize.Large, 2), small = Liquid(m, MarketSize.Large, 3);
        Worked(m, big, OrderSide.Buy, MarketTrading.MostPerOrder(big) * 6, out _);
        Order done = Worked(m, small, OrderSide.Buy, MarketTrading.MostPerOrder(small) / 3, out _);
        RunTo(m, d2, MarketCatalogue.PhaseOpen + 40);
        Check(done != null && done.State == OrderState.Filled && done.EndDay == d1, "invest: a worked order done keeps the day it ended");
        Stock t = Trusts(m).OrderByDescending(x => x.Adv).First();
        m.State.Orders.Add(new Order
        {
            Id = m.State.NextOrderId++, StockId = t.Id, Side = OrderSide.Buy, Type = OrderType.Market, Life = OrderLife.Day, Back = true, Budget = 12345, PlacedDay = m.State.Day,
            PlacedPhase = MarketCatalogue.PhaseClose, ExpireDay = m.State.Day + 1, State = OrderState.Open
        });
        MarketState s = m.State;
        string line = MarketStore.Write(s);
        MarketState back = MarketStore.Read(line, out _);
        Check(back != null && MarketStore.Write(back) == line, "invest: the save's line with every new field reads back and writes again exactly the same");
        bool orders = back != null && back.Orders.Count == s.Orders.Count;
        for (int i = 0; orders && i < s.Orders.Count; i++)
        {
            Order x = s.Orders[i], y = back.Orders[i];
            orders = x.Id == y.Id && x.Worked == y.Worked && x.Back == y.Back && x.Budget == y.Budget && x.Qty == y.Qty && x.Filled == y.Filled && x.FilledValue == y.FilledValue && x.State == y.State
                     && x.Why == y.Why && x.Life == y.Life && x.EndDay == y.EndDay && x.DayOf == y.DayOf && x.DayQty == y.DayQty && x.DayValue == y.DayValue && x.Side == y.Side
                     && x.StockId == y.StockId && x.PlacedDay == y.PlacedDay;
        }
        Check(orders && s.Orders.Any(o => o.Worked && o.IsOpen) && s.Orders.Any(o => o.Back), "invest: worked orders and payouts to put back come back whole, each in its place by its number");
        Check(back != null && back.Positions.Select(p => p.Reinvest).SequenceEqual(s.Positions.Select(p => p.Reinvest)) && s.Positions.Any(p => p.Reinvest > 0)
              && back.Trades.Select(x => x.Order + "," + (x.Back ? 1 : 0)).SequenceEqual(s.Trades.Select(x => x.Order + "," + (x.Back ? 1 : 0))) && s.Trades.Any(x => x.Order > 0)
              && back.Bonds.Holdings.Select(h => h.Rollover).SequenceEqual(s.Bonds.Holdings.Select(h => h.Rollover)) && s.Bonds.Holdings.Any(h => h.Rollover),
            "invest: a holding's share put back, a history line's worked order and payout, and a bond's rollover come back");
        // Part o has none of them: a version from before never fills a worked order whole at the open.
        int o0 = line.IndexOf("~o:", StringComparison.Ordinal), o1 = line.IndexOf('~', o0 + 3);
        string partO = line.Substring(o0 + 3, o1 - o0 - 3);
        var idsInO = new HashSet<int>(partO.Split(';').Where(r => r.Length > 0).Select(r => int.Parse(r.Split('|')[0])));
        Check(s.Orders.Where(o => o.Worked || o.Back).All(o => !idsInO.Contains(o.Id)) && line.Contains("~g:") && !MarketStore.Write(s, true).Contains("~g:"),
            "invest: they are in part g, never in part o, and never in the city's line a Going Public host sends");
        // A line from before them: none of the new fields, read as it was.
        string old = StripInvest(line);
        MarketState before = MarketStore.Read(old, out _);
        Check(before != null && before.Orders.All(o => !o.Worked && !o.Back) && before.Orders.Count == s.Orders.Count(o => !o.Worked && !o.Back) && before.Positions.All(p => p.Reinvest == 0)
              && before.Trades.All(x => x.Order == 0 && !x.Back) && before.Bonds.Holdings.All(h => !h.Rollover) && before.Positions.Count == s.Positions.Count
              && before.Bonds.Holdings.Count == s.Bonds.Holdings.Count && before.Stocks.Count == s.Stocks.Count,
            "invest: a line from before them loads as before: no worked order, nothing put back, no rollover, everything else there");
        MarketState none = MarketStore.Read(MarketStore.Write(before), out _);
        string noneLine = MarketStore.Write(none);
        Check(!noneLine.Contains("~g:") && noneLine == MarketStore.Write(before) && MostFields(noneLine, 'p') == 7 && MostFields(noneLine, 't') <= 10 && MostFields(line, 'p') == 8
              && MostFields(line, 't') == 12,
            "invest: and a market with none of them writes none of their fields: a holding's record and a history line as before");
        // A damaged record of part g is dropped, the others kept.
        int g0 = line.IndexOf("~g:", StringComparison.Ordinal);
        int g1 = line.IndexOf('~', g0 + 3);
        string partG = line.Substring(g0 + 3, (g1 < 0 ? line.Length : g1) - g0 - 3);
        string[] records = partG.Split(';');
        string damaged = line.Replace("~g:" + partG, "~g:x|y|z;" + string.Join(";", records));
        MarketState kept = MarketStore.Read(damaged, out _);
        Check(kept != null && kept.Orders.Count == s.Orders.Count, "invest: a damaged record of part g is dropped, the others kept");
        // A guest in a Going Public session brings its worked orders into the host's market.
        MarketEngine host = InvestMarket(441, 1000, out _, 3, 10);
        MarketState adopted = MarketShare.Adopt(s, MarketShare.Write(host.State, host.Tape), new List<long>(), null, out _, out _);
        Check(adopted != null && adopted.Orders.Count(o => o.Worked || o.Back) == s.Orders.Count(o => o.Worked || o.Back), "invest: a Going Public guest's worked orders come with it into the host's market");
    }

    // The most fields a record of a part of the line has (0 for no part or no record).
    private static int MostFields(string line, char part)
    {
        int at = line.IndexOf("~" + part + ":", StringComparison.Ordinal);
        if (at < 0)
        {
            return 0;
        }
        int end = line.IndexOf('~', at + 3);
        string body = line.Substring(at + 3, (end < 0 ? line.Length : end) - at - 3);
        return body.Length == 0 ? 0 : body.Split(';').Max(r => r.Split('|').Length);
    }

    // The line without anything the large investor's tools added: part g, a holding's share put back, a line's worked order and payout, a bond's rollover.
    private static string StripInvest(string line)
    {
        var parts = line.Split('~').ToList();
        parts.RemoveAll(p => p.StartsWith("g:", StringComparison.Ordinal));
        for (int i = 0; i < parts.Count; i++)
        {
            string p = parts[i];
            if (p.StartsWith("p:", StringComparison.Ordinal) || p.StartsWith("t:", StringComparison.Ordinal) || p.StartsWith("f:", StringComparison.Ordinal))
            {
                int keep = p[0] == 'p' ? 7 : p[0] == 't' ? 10 : 17;
                string[] records = p.Substring(2).Split(';');
                for (int r = 0; r < records.Length; r++)
                {
                    string[] f = records[r].Split('|');
                    if (p[0] == 'f' && r == 0)
                    {
                        continue;   // (its next number)
                    }
                    if (f.Length > keep)
                    {
                        records[r] = string.Join("|", f.Take(keep));
                    }
                }
                parts[i] = p.Substring(0, 2) + string.Join(";", records);
            }
        }
        return string.Join("~", parts);
    }

    // ---------------------------------------------------------------- the cost

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void InvestCost()
    {
        double extra = double.MaxValue;
        long allocated = long.MaxValue;
        int workedCount = 0;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            MarketEngine plain = InvestMarket(451, 500000000000L, out _, 3, 1);
            MarketEngine busy = InvestMarket(451, 500000000000L, out _, 3, 1);
            List<Stock> listed = busy.State.Stocks.Where(x => x.Listed && x.BidCents <= 0).OrderByDescending(x => x.Adv).Take(MarketCatalogue.OrdersMost).ToList();
            foreach (Stock s in listed)
            {
                Worked(busy, s, OrderSide.Buy, MarketTrading.MostPerOrder(s) * 50, out _);
            }
            workedCount = busy.State.Orders.Count(o => o.Worked && o.IsOpen);
            int day = busy.State.Day;
            RunTo(plain, day, MarketCatalogue.PhaseOpen + 3);
            RunTo(busy, day, MarketCatalogue.PhaseOpen + 3);
            long a0 = GC.GetAllocatedBytesForCurrentThread(), t0 = Stopwatch.GetTimestamp();
            RunTo(plain, day, MarketCatalogue.PhaseOpen + 63);
            double plainMs = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            long plainBytes = GC.GetAllocatedBytesForCurrentThread() - a0;
            long b0 = GC.GetAllocatedBytesForCurrentThread(), t1 = Stopwatch.GetTimestamp();
            RunTo(busy, day, MarketCatalogue.PhaseOpen + 63);
            double busyMs = (Stopwatch.GetTimestamp() - t1) * 1000.0 / Stopwatch.Frequency;
            long busyBytes = GC.GetAllocatedBytesForCurrentThread() - b0;
            extra = Math.Min(extra, (busyMs - plainMs) / 60.0);
            allocated = Math.Min(allocated, busyBytes - plainBytes);
        }
        Console.WriteLine("  worked: " + workedCount + " worked orders cost " + extra.ToString("0.000") + " ms more a step, and " + allocated + " bytes more over sixty steps");
        Check(workedCount == MarketCatalogue.OrdersMost && extra < 0.5, "worked: the most orders there can be, all worked, cost under half a millisecond more a step (" + extra.ToString("0.000") + " ms)");
        Check(allocated <= 0, "worked: and allocate nothing more, their slices joining each order's line of the day (" + allocated + " bytes)");
        // The Portfolio tab's figures for a large portfolio, again and again: quick, and nothing made after the first.
        MarketEngine m = InvestScenario(452, 50000000000L, out _);
        foreach (Stock s in m.State.Stocks.Where(x => x.Listed).Take(60))
        {
            Trade(m, s, OrderSide.Buy, 10);
        }
        while (m.State.Bonds.Holdings.Count < BondCatalogue.HoldingsMostLarge)
        {
            m.BuyBond(m.Offer(4, -1, null), 1000, 0, m.Trading, true, out _);
        }
        var rows = new List<PortfolioRow>();
        var sums = new PortfolioSums();
        var next = new List<ComingPayment>();
        InvestRules.Holdings(m.State, m.State.Phase, rows);
        InvestRules.Sums(rows, sums);
        InvestRules.NextPayments(rows, next, 3);
        double fastest = double.MaxValue;
        long bytes = long.MaxValue;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            long a0 = GC.GetAllocatedBytesForCurrentThread(), t0 = Stopwatch.GetTimestamp();
            InvestRules.Holdings(m.State, m.State.Phase, rows);
            InvestRules.Sums(rows, sums);
            InvestRules.NextPayments(rows, next, 3);
            fastest = Math.Min(fastest, (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency);
            bytes = Math.Min(bytes, GC.GetAllocatedBytesForCurrentThread() - a0);
        }
        Console.WriteLine("  invest: a portfolio of " + rows.Count + " holdings taken apart in " + fastest.ToString("0.000") + " ms, " + bytes + " bytes");
        Check(rows.Count >= 100 && fastest < 1.0 && bytes == 0, "invest: the Portfolio tab's figures for " + rows.Count + " holdings take under a millisecond and make nothing after the first time");
    }

    // ---------------------------------------------------------------- the words

    private static readonly string[] InvestKeys =
    {
        "reallife_mkt_why_switched", "reallife_mkt_inv_opt_large", "reallife_opt_tip_markets_large", "reallife_mkt_inv_opt_reinvest", "reallife_opt_tip_markets_reinvest",
        "reallife_mkt_inv_test_work", "reallife_mkt_inv_test_back", "reallife_mkt_inv_test_roll", "reallife_mkt_inv_test_shut", "reallife_mkt_inv_test_no_worked", "reallife_mkt_inv_test_no_due",
        "reallife_mkt_col_holding", "reallife_mkt_col_cost", "reallife_mkt_col_income", "reallife_mkt_inv_filter_kinds", "reallife_mkt_inv_kind_stock", "reallife_mkt_inv_kind_trust",
        "reallife_mkt_inv_kind_treasury", "reallife_mkt_inv_kind_city", "reallife_mkt_inv_kind_deposit", "reallife_mkt_inv_empty", "reallife_mkt_inv_mark_back", "reallife_mkt_inv_mark_rolls",
        "reallife_mkt_inv_next_end", "reallife_mkt_inv_title", "reallife_mkt_inv_page_held", "reallife_mkt_inv_page_pays", "reallife_mkt_inv_page_account", "reallife_mkt_inv_sec_kinds",
        "reallife_mkt_inv_share", "reallife_mkt_inv_none", "reallife_mkt_inv_shorts", "reallife_mkt_inv_founder", "reallife_mkt_inv_cash", "reallife_mkt_inv_margin_cash", "reallife_mkt_inv_sec_year",
        "reallife_mkt_inv_income_dividends", "reallife_mkt_inv_income_trust", "reallife_mkt_inv_income_treasury", "reallife_mkt_inv_income_city", "reallife_mkt_inv_income_deposit",
        "reallife_mkt_inv_income_owed", "reallife_mkt_inv_income_all", "reallife_mkt_inv_yield", "reallife_mkt_inv_income_note", "reallife_mkt_inv_income_none", "reallife_mkt_inv_no_payments",
        "reallife_mkt_inv_more_payments", "reallife_mkt_inv_type_worked", "reallife_mkt_inv_type_back", "reallife_mkt_inv_life_done", "reallife_mkt_inv_state_working", "reallife_mkt_inv_state_stopped",
        "reallife_mkt_inv_row_average", "reallife_mkt_inv_today_value", "reallife_mkt_inv_row_left", "reallife_mkt_inv_left_value", "reallife_mkt_inv_order_help", "reallife_mkt_inv_row_money",
        "reallife_mkt_inv_order_back_help", "reallife_mkt_inv_ticket_worked", "reallife_mkt_inv_ticket_about", "reallife_mkt_inv_confirm_buy", "reallife_mkt_inv_confirm_sell", "reallife_mkt_inv_confirm_body",
        "reallife_mkt_inv_work", "reallife_mkt_inv_set_back_none", "reallife_mkt_inv_set_back", "reallife_mkt_inv_mark_worked", "reallife_mkt_inv_mark_put_back", "reallife_mkt_inv_note_placed",
        "reallife_mkt_inv_note_day_buy", "reallife_mkt_inv_note_day_sell", "reallife_mkt_inv_note_done_buy", "reallife_mkt_inv_note_done_sell", "reallife_mkt_inv_note_stopped", "reallife_mkt_inv_note_back",
        "reallife_mkt_inv_note_back_refused", "reallife_mkt_bond_box_most", "reallife_mkt_bond_set_roll_no", "reallife_mkt_bond_set_roll_yes", "reallife_mkt_bond_confirm_roll",
        "reallife_mkt_bond_note_rolled", "reallife_mkt_bond_ladder", "reallife_mkt_bond_ladder_terms", "reallife_mkt_bond_ladder_pays", "reallife_mkt_bond_ladder_about", "reallife_mkt_bond_ladder_piece",
        "reallife_mkt_bond_ladder_limits", "reallife_mkt_bond_ladder_buy", "reallife_mkt_bond_ladder_confirm", "reallife_mkt_bond_ladder_confirm_body", "reallife_mkt_bond_ladder_confirm_roll",
        "reallife_mkt_bond_note_ladder", "reallife_transaction_shares_worked_held", "reallife_transaction_shares_worked_held_label", "reallife_transaction_shares_worked_back",
        "reallife_transaction_shares_worked_back_label", "reallife_transaction_shares_worked_sold", "reallife_transaction_shares_worked_sold_label", "reallife_mkt_inv_ticket_hold",
        "reallife_mkt_inv_ticket_due", "reallife_mkt_inv_held_money", "reallife_mkt_inv_due_money", "reallife_mkt_inv_row_held", "reallife_mkt_inv_row_due", "reallife_mkt_inv_note_back_part",
        "reallife_mkt_inv_test_no_share", "reallife_mkt_inv_next_roll", "reallife_mkt_bond_held_last_roll", "reallife_mkt_plan_bond_roll_title", "reallife_mkt_plan_bond_roll_what"
    };

    private static readonly string[] InvestNotes =
    {
        "reallife_mkt_inv_note_placed", "reallife_mkt_inv_note_day_buy", "reallife_mkt_inv_note_day_sell", "reallife_mkt_inv_note_done_buy", "reallife_mkt_inv_note_done_sell", "reallife_mkt_inv_note_stopped",
        "reallife_mkt_inv_note_back", "reallife_mkt_inv_note_back_refused", "reallife_mkt_bond_note_rolled", "reallife_mkt_bond_note_ladder", "reallife_mkt_inv_test_shut", "reallife_mkt_inv_test_no_worked",
        "reallife_mkt_inv_test_no_due", "reallife_mkt_inv_note_back_part", "reallife_mkt_inv_test_no_share"
    };

    private static void InvestWords()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("invest", langs, codes))
        {
            return;
        }
        Dictionary<string, string> en = langs[0], pt = langs[1], zh = langs[2];
        string missing = string.Join(", ", InvestKeys.Where(k => LanguagesLacking(langs, codes, k).Length > 0).Select(k => k + " (" + LanguagesLacking(langs, codes, k) + ")"));
        Check(missing.Length == 0, "invest: every new text is in every language with the same {names} (" + missing + ")");
        string[] kinds = { "reallife_mkt_inv_kind_stock", "reallife_mkt_inv_kind_trust", "reallife_mkt_inv_kind_treasury", "reallife_mkt_inv_kind_city", "reallife_mkt_inv_kind_deposit" };
        string textFile = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Markets\InvestText.cs"));
        string code = System.IO.File.Exists(textFile) ? System.IO.File.ReadAllText(textFile) : string.Empty;
        int at = 0;
        bool inOrder = kinds.All(k => (at = code.IndexOf("\"" + k + "\"", at, StringComparison.Ordinal)) >= 0);
        Check(kinds.All(en.ContainsKey) && kinds.Length == (int)HoldingKind.Count && inOrder, "invest: a name for each kind of holding, in the kinds' order (InvestText.KindKeys)");
        Check(InvestNotes.All(k => en.ContainsKey(k) && en[k].Length <= 200), "invest: every notification is short (240 characters with its names at most)");
        // A part put back says so with its reason: with realistic names and its longest reason, 240 characters at most in each language.
        string[] whys = { "reallife_mkt_why_too_large", "reallife_mkt_why_holding_limit", "reallife_mkt_why_no_money" };
        bool fits = langs.All(w => whys.All(y => w["reallife_mkt_inv_note_back_part"].Replace("{count}", "1,234,567").Replace("{name}", "Harbourview Property Trust")
            .Replace("{price}", "$1,234.56").Replace("{why}", w[y]).Length <= 240));
        Check(fits, "invest: a dividend put back in part says so, with its reason and realistic names, in 240 characters at most in each language");
        string guide = en.TryGetValue("reallife_guide_markets", out string g) ? g : string.Empty, bots = en.TryGetValue("reallife_guide_markets_bots", out string b) ? b : string.Empty;
        Check(guide.Contains("{bond_most_share}") && guide.Contains("{bond_most_cap}") && guide.Contains("{bond_ladder_terms}") && guide.Contains("- Portfolio:") && guide.Contains("roll over")
              && pt["reallife_guide_markets"].Contains("{bond_most_share}") && zh["reallife_guide_markets"].Contains("{bond_ladder_terms}") && bots.Contains("is worked:")
              && pt["reallife_guide_markets_bots"].Contains("fracionada") && zh["reallife_guide_markets_bots"].Contains("分批执行")
              && guide.Contains("{bond_holdings_large}") && pt["reallife_guide_markets"].Contains("{bond_holdings_large}") && zh["reallife_guide_markets"].Contains("{bond_holdings_large}")
              && bots.Contains("what is not spent comes back at the close") && pt["reallife_guide_markets_bots"].Contains("volta no fechamento") && zh["reallife_guide_markets_bots"].Contains("收盘时退回")
              && en["reallife_opt_tip_markets_large"].Contains("still yours for other Stocks purchases") && en["reallife_mkt_inv_ticket_hold"].Contains("still use this money first")
              && bots.Contains("still use that money first") && pt["reallife_guide_markets_bots"].Contains("ainda usam esse dinheiro primeiro")
              && zh["reallife_guide_markets_bots"].Contains("仍可优先使用这笔钱"),
            "invest: the Stocks pages of the Guide tell of the portfolio, worked orders and their day's money, the most of a bond purchase, the bonds held, rollovers and ladders, with their figures, in each language");
        bool line = false;
        foreach (KeyValuePair<string, string> row in en)
        {
            if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains("Portfolio tab shows what each holding is"))
            {
                line = !row.Value.StartsWith("New: ", StringComparison.Ordinal) && !row.Value.StartsWith("Fixed: ", StringComparison.Ordinal);
                break;
            }
        }
        Check(line, "invest: What's new has the feature's line, with no prefix (in the next row, or its version's row once it is made)");
    }

    // ---------------------------------------------------------------- the review: the holding limit, the day's money,
    // a guest's lines, the options at the open, a rolling bond's payments, the bonds held, large sums, the test, the worth

    /// <summary>The player's cash with every transaction kept: what it was for, its money (taken: under nothing) and the market's day.</summary>
    private sealed class LogWallet : IMarketWallet
    {
        public long Money;
        public long Low = long.MaxValue;   // the least the cash came to after a transaction
        public Func<int> Day = () => 0;
        public Func<long> Broker = () => 0L;   // the money worked orders have with the broker at the moment (LogMarket sets it)
        public int ShortWhileBroker;           // transactions that left the cash under nothing by a worked order's own money, or while money was with the broker
        public readonly List<(string Why, long Amount, int Day)> Moves = new List<(string Why, long Amount, int Day)>();

        public long Cash => Math.Max(0, Money);

        public bool Take(long amount, string why, string name)
        {
            if (amount <= 0)
            {
                return true;
            }
            if (Money < amount)
            {
                return false;
            }
            Money -= amount;
            Log(why, -amount);
            return true;
        }

        public bool Ready(long amount) => amount <= 0 || Money >= amount;

        public void Force(long amount, string why, string name)
        {
            if (amount > 0)
            {
                Money -= amount;
                Log(why, -amount);
            }
        }

        public void Give(long amount, string why, string name)
        {
            if (amount > 0)
            {
                Money += amount;
                Log(why, amount);
            }
        }

        private void Log(string why, long amount)
        {
            Moves.Add((why, amount, Day()));
            Low = Math.Min(Low, Money);
            if (Money < 0 && (why == MarketMoney.WorkedHeld || Broker() > 0))
            {
                ShortWhileBroker++;
            }
        }

        public int Count(string why, int day = -1) => Moves.Count(x => x.Why == why && (day < 0 || x.Day == day));

        public long Sum(string why) => Moves.Where(x => x.Why == why).Sum(x => x.Amount);
    }

    /// <summary>The checks' city and secrets with a wallet that keeps every transaction, and every order told of (refusals too, with their reason then).</summary>
    private sealed class LogWorld : IMarketWorld
    {
        public readonly MarketWorld Inner;
        public readonly LogWallet Purse = new LogWallet();
        public readonly List<(Order Order, long Qty, OrderWhy Why)> Told = new List<(Order Order, long Qty, OrderWhy Why)>();

        public LogWorld(ulong seed)
        {
            Inner = new MarketWorld(seed);
        }

        public ulong NewSecret() => Inner.NewSecret();
        public MarketEnv Env(int day) => Inner.Env(day);
        public IMarketWallet Wallet => Purse;
        public int CommissionLeast => Inner.CommissionLeast;
        public void Said(in MarketNews news) => Inner.Said(news);
        public void Paid(TradeKind kind, Stock stock, long amount) => Inner.Paid(kind, stock, amount);
        public void Called(long amount, int byDay) => Inner.Called(amount, byDay);

        public void Filled(Order order, Stock stock, long qty, int cents)
        {
            Told.Add((order, qty, order.Why));
            Inner.Filled(order, stock, qty, cents);
        }
    }

    // A market on a market day at a step of its session, with so much cash in a wallet that keeps every transaction.
    private static MarketEngine LogMarket(ulong seed, long money, out LogWorld world, int from = 3, int step = 5)
    {
        world = new LogWorld(seed);
        world.Purse.Money = money;
        MarketState state = MarketSetup.Open(seed, 1, 0, 60, world.Env(1), seed, null, null, null);
        var m = new MarketEngine(state, new MarketTape(seed), world);
        world.Purse.Day = () => m.State.Day;
        world.Purse.Broker = () => InvestRules.WithBroker(m.State, out _, out _);
        SessionDay(m, from, step);
        return m;
    }

    // A holding put straight into the market (as a load would bring it), at the offer's price.
    private static Position PutHolding(MarketEngine m, Stock s, long shares, int reinvest = 0)
    {
        Quote q = MarketTrading.QuoteOf(m.State, s, m.State.Phase);
        var p = new Position { StockId = s.Id, Shares = shares, Cost = MarketTrading.Value(shares, q.Ask, true), OpenedDay = m.State.Day, Reinvest = reinvest };
        m.State.Positions.Add(p);
        return p;
    }

    // A dividend put back, waiting for the next open with so much money (as a payout makes it: MarketEngine.Invest's PutBack).
    private static Order WaitingBack(MarketEngine m, Stock s, long budget)
    {
        var o = new Order
        {
            Id = m.State.NextOrderId++, StockId = s.Id, Side = OrderSide.Buy, Type = OrderType.Market, Life = OrderLife.Day, Back = true, Budget = budget, PlacedDay = m.State.Day,
            PlacedPhase = MarketCatalogue.PhaseClose, ExpireDay = MarketTrading.NextTradingDay(m.State.Day), State = OrderState.Open
        };
        m.State.Orders.Add(o);
        return o;
    }

    // Every part of the market's engine (MarketEngine.cs and MarketEngine.*.cs), as text.
    private static string[] EngineSources()
    {
        string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Markets"));
        return System.IO.Directory.Exists(dir) ? System.IO.Directory.GetFiles(dir, "MarketEngine*.cs").Select(System.IO.File.ReadAllText).ToArray() : new string[0];
    }

    private static string ModSource(string relative)
    {
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts", relative));
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : string.Empty;
    }

    // The reviewer's case (seed 332): a worked purchase filled to the holding limit, then an earlier limit purchase filled on
    // top of it, 92,626 shares over. Now a worked purchase leaves other open purchases the room they were placed with: it
    // fills to the rest and waits, open; the limit fills when the market comes to it; the holding comes to the most and never
    // past it; then the worked order stops at the limit.
    private static void InvestHoldingRoom()
    {
        MarketEngine m = InvestMarket(332, 500000000000L, out MarketWorld world, 3, 5);
        Stock s = Liquid(m, MarketSize.Mid);
        long most = MarketTrading.MostHeld(s), per = MarketTrading.MostPerOrder(s);
        PutHolding(m, s, most - per * 3);
        Quote q = MarketTrading.QuoteOf(m.State, s, m.State.Phase);
        Order limit = Trade(m, s, OrderSide.Buy, per / 2, OrderType.Limit, Math.Max(1, q.Bid / 2), 0, OrderLife.UntilCancelled);
        Order w = Worked(m, s, OrderSide.Buy, per * 5, out OrderWhy why);
        long high = 0;
        int day = m.State.Day;
        for (int i = 0; i < 6; i++)
        {
            day = TradingOnOrAfter(day + 1);
            RunTo(m, day);
            high = Math.Max(high, m.State.Held(s.Id));
        }
        bool waited = limit != null && limit.IsOpen && w != null && w.IsOpen && m.State.Held(s.Id) == most - limit.Left;
        Check(waited, "worked: a worked purchase leaves an earlier limit purchase its room: it fills to the rest and waits, open (" + (w != null ? w.Filled : -1) + " bought, "
                      + (most - m.State.Held(s.Id)) + " left under the most for the limit's " + (limit != null ? limit.Left : -1) + ")");
        if (limit == null || w == null)
        {
            return;
        }
        limit.LimitCents = int.MaxValue / 2;   // (the market comes to it)
        for (int i = 0; i < 6 && (limit.IsOpen || w.IsOpen); i++)
        {
            day = TradingOnOrAfter(day + 1);
            RunTo(m, day);
            high = Math.Max(high, m.State.Held(s.Id));
        }
        Console.WriteLine("  worked: room left to an earlier limit (seed 332): the worked order bought " + w.Filled + ", the limit " + limit.Filled + "; the holding " + m.State.Held(s.Id)
                          + " of the most " + most + " (at most " + high + " on the way); the worked order " + (w.IsOpen ? "open" : w.Why.ToString()));
        Check(limit.State == OrderState.Filled && limit.Filled == per / 2 && m.State.Held(s.Id) == most && high <= most && !w.IsOpen && w.Why == OrderWhy.HoldingLimit,
            "worked: then the limit fills when the market comes to it, the holding comes to the most and never past it (the seed that went 92,626 over), and the worked order stops there");
    }

    // Two worked purchases of one company never wait for each other: the earlier one is served first, and the later one
    // takes the room left beside it and stops at the limit. (Each waiting for the room the other is still for, they would
    // stand for ever with room unused.)
    private static void InvestTwoWorked()
    {
        MarketEngine m = InvestMarket(336, 500000000000L, out MarketWorld world, 3, 5);
        Stock s = Liquid(m, MarketSize.Mid, 2);
        long most = MarketTrading.MostHeld(s), per = MarketTrading.MostPerOrder(s);
        PutHolding(m, s, most - per * 4);
        Order a = Worked(m, s, OrderSide.Buy, per * 3, out _);
        Order b = Worked(m, s, OrderSide.Buy, per * 3, out _);
        if (a == null || b == null)
        {
            Check(false, "worked: two worked purchases of one company are placed");
            return;
        }
        UntilDone(m, b, 12);
        Check(a.State == OrderState.Filled && !b.IsOpen && b.Why == OrderWhy.HoldingLimit && b.Filled == per && m.State.Held(s.Id) == most,
            "worked: two worked purchases of one company never wait for each other: the earlier is served first, the later takes the room left and stops at the limit ("
            + a.Filled + " and " + b.Filled + ")");
    }

    // No purchase of the player's own takes a holding past the most, whatever changed after it was placed: a limit within
    // the most, then a twentieth of the company's shares bought back, so the holding is over the new most; the limit is
    // refused at its fill with the reason, and nothing is bought. (Every other check of the market's runs with this guard:
    // none of them comes to it.)
    private static void InvestFillGuard()
    {
        MarketEngine m = InvestMarket(333, 500000000000L, out MarketWorld world, 3, 5);
        Stock s = Liquid(m, MarketSize.Mid, 1);
        long most = MarketTrading.MostHeld(s), per = MarketTrading.MostPerOrder(s);
        PutHolding(m, s, most - per);
        Quote q = MarketTrading.QuoteOf(m.State, s, m.State.Phase);
        Order limit = Trade(m, s, OrderSide.Buy, per / 2, OrderType.Limit, Math.Max(1, q.Bid / 2), 0, OrderLife.UntilCancelled);
        long held = m.State.Held(s.Id), cash = world.Purse.Money;
        s.Shares -= s.Shares / 20;
        limit.LimitCents = int.MaxValue / 2;
        RunTo(m, TradingOnOrAfter(m.State.Day + 1));
        Check(limit != null && limit.State == OrderState.Refused && limit.Why == OrderWhy.HoldingLimit && limit.Filled == 0 && m.State.Held(s.Id) == held && world.Purse.Money == cash,
            "market: no purchase of the player's own takes a holding past the most, whatever changed after it was placed (refused at its fill, with the reason)");
    }

    // A trust at the most the player may hold, putting all of each payout back, buys none of it: each is refused with the
    // reason and stays in the cash (the reviewer's case: over at every payout). One payout put back with money for twice
    // one order's most buys one order's most (the reviewer's: 348,168 shares where one order's most was 158,431), the rest
    // left in the cash, and the player is told it was in part and why.
    private static void InvestPutBackRoom()
    {
        MarketEngine m = LogMarket(334, 1000000000L, out LogWorld world, 3, 5);
        Stock t = Trusts(m).OrderByDescending(x => x.DpsCents).First();
        long most = MarketTrading.MostHeld(t);
        PutHolding(m, t, most, 100);
        long high = 0;
        int day = m.State.Day;
        for (int i = 0; i < 12; i++)
        {
            day = TradingOnOrAfter(day + 1);
            RunTo(m, day);
            high = Math.Max(high, m.State.Held(t.Id));
        }
        int refused = world.Told.Count(x => x.Order.Back && x.Qty <= 0 && x.Why == OrderWhy.HoldingLimit), bought = world.Told.Count(x => x.Order.Back && x.Qty > 0);
        Check(refused >= 2 && bought == 0 && high == most && world.Purse.Sum(MarketMoney.Dividend) > 0,
            "put back: a trust at the most you may hold, putting all of each payout back, buys none of it: each is refused with the reason and stays in the cash (" + refused + " payouts)");
        MarketEngine n = LogMarket(335, 50000000000L, out LogWorld w2, 3, 5);
        Stock s = Liquid(n, MarketSize.Large);
        long per = MarketTrading.MostPerOrder(s);
        Quote q = MarketTrading.QuoteOf(n.State, s, n.State.Phase);
        Order back = WaitingBack(n, s, MarketTrading.Value(per * 2, q.Ask, true) * 2);
        long cash = w2.Purse.Money;
        RunTo(n, MarketTrading.NextTradingDay(n.State.Day), MarketCatalogue.PhaseOpen + 1);
        Trade line = n.State.Trades.LastOrDefault(x => x.Back && x.StockId == s.Id);
        per = MarketTrading.MostPerOrder(s);   // (as the open had it: a day's usual volume moves at each close)
        Console.WriteLine("  put back: a trust at the most, all of each payout put back: " + refused + " payouts refused, " + bought + " bought; money for twice one order's most bought "
                          + back.Filled + " (one order's most " + per + "), " + (back.Budget - (line != null ? -line.Value + line.Fee : 0)) + " left in the cash");
        Check(back.State == OrderState.Filled && back.Filled == per && back.Why == OrderWhy.TooLarge && w2.Told.Any(x => x.Order == back && x.Qty == per && x.Why == OrderWhy.TooLarge)
              && line != null && cash - w2.Purse.Money == -line.Value + line.Fee && back.Budget - (-line.Value + line.Fee) > 0,
            "put back: one payout put back buys one order's most at the most, the rest of its money left in the cash, and the player told it was in part and why ("
            + back.Filled + " of one order's most " + per + ")");
    }

    // A worked order on a cash account settles its money once a market day: one transaction holds a purchase's day of money
    // at its first slice and one gives back what is left at the close or its end, none for a slice; a sale's takings after
    // the commission come in one at each close and one at its end; the money held is what the slices cost and what came
    // back, to the unit; the cash never goes under nothing; one that runs short buys what the cash pays for, a day at a
    // time, and stops; a margin account moves none of the game's cash; a save in the middle of a day keeps both.
    private static void InvestDayMoney()
    {
        MarketEngine m = LogMarket(341, 50000000000L, out LogWorld world, 3, 5);
        Stock s = Liquid(m, MarketSize.Large);
        long per = MarketTrading.MostPerOrder(s);
        Order o = m.PlaceWorked(s.Id, OrderSide.Buy, per * 3, m.Held, false, out _);
        UntilDone(m, o, 10);
        LogWallet purse = world.Purse;
        List<Trade> lines = m.State.Trades.Where(t => t.Order == o.Id).ToList();
        List<int> days = lines.Select(t => t.Day).Distinct().ToList();
        int holds = purse.Count(MarketMoney.WorkedHeld), backs = purse.Count(MarketMoney.WorkedBack);
        bool daily = days.All(d => purse.Count(MarketMoney.WorkedHeld, d) == 1 && purse.Count(MarketMoney.WorkedBack, d) <= 1);
        Check(o.State == OrderState.Filled && daily && holds == days.Count && backs <= days.Count && purse.Count(MarketMoney.Bought) == 0 && purse.Count(MarketMoney.Commission) == 0,
            "worked: a purchase's money is held in one transaction a market day and what is left given back in one, none for a slice (" + holds + " held and " + backs + " back over "
            + days.Count + " days, for " + world.Inner.Fills + " slices)");
        long spent = lines.Sum(t => -t.Value) + lines.Sum(t => t.Fee);
        Check(-purse.Sum(MarketMoney.WorkedHeld) == spent + purse.Sum(MarketMoney.WorkedBack) && o.Parked == 0 && purse.Low >= 0,
            "worked: what was held is what its slices cost with the commission and what came back, to the unit, and the cash never went under nothing");

        MarketEngine n = LogMarket(342, 1000L, out LogWorld w2, 3, 5);
        Stock s2 = Liquid(n, MarketSize.Large);
        long qty = MarketTrading.MostPerOrder(s2) * 2 + 777;
        PutHolding(n, s2, qty);
        Order sale = n.PlaceWorked(s2.Id, OrderSide.Sell, qty, n.Held, false, out _);
        UntilDone(n, sale, 10);
        List<Trade> sold = n.State.Trades.Where(t => t.Order == sale.Id).ToList();
        List<int> saleDays = sold.Select(t => t.Day).Distinct().ToList();
        bool eachDay = saleDays.All(d => w2.Purse.Count(MarketMoney.WorkedSold, d) == 1);
        Check(sale.State == OrderState.Filled && eachDay && w2.Purse.Count(MarketMoney.WorkedSold) == saleDays.Count && w2.Purse.Sum(MarketMoney.WorkedSold) == sold.Sum(t => t.Value) - sold.Sum(t => t.Fee)
              && w2.Purse.Count(MarketMoney.Sold) == 0 && w2.Purse.Count(MarketMoney.Commission) == 0 && sale.Due == 0,
            "worked: a sale's takings after the commission come in one transaction at each close and one at its end, none for a slice (" + w2.Purse.Count(MarketMoney.WorkedSold) + " over "
            + saleDays.Count + " days)");

        Console.WriteLine("  worked: its money on a cash account: a purchase " + holds + " held and " + backs + " given back over " + days.Count + " market days for " + world.Inner.Fills
                          + " slices; a sale's takings in " + w2.Purse.Count(MarketMoney.WorkedSold) + " over " + saleDays.Count + " days");
        MarketEngine r = LogMarket(343, 0L, out LogWorld w3, 3, 5);
        Stock s3 = Liquid(r, MarketSize.Large);
        long per3 = MarketTrading.MostPerOrder(s3);
        long start = MarketTrading.Value(per3, MarketTrading.QuoteOf(r.State, s3, r.State.Phase).Ask, true) * 3 / 2;
        w3.Purse.Money = start;
        Order o3 = r.PlaceWorked(s3.Id, OrderSide.Buy, per3 * 5, r.Held, false, out _);
        UntilDone(r, o3, 12);
        List<Trade> lines3 = r.State.Trades.Where(t => t.Order == o3.Id).ToList();
        bool daily3 = lines3.Select(t => t.Day).Distinct().All(d => w3.Purse.Count(MarketMoney.WorkedHeld, d) == 1 && w3.Purse.Count(MarketMoney.WorkedBack, d) <= 1);
        Check(o3 != null && !o3.IsOpen && o3.Why == OrderWhy.NoMoney && o3.Filled > per3 && daily3 && w3.Purse.Low >= 0 && o3.Parked == 0
              && start - w3.Purse.Money == lines3.Sum(t => -t.Value) + lines3.Sum(t => t.Fee),
            "worked: short of cash it holds what there is, buys what that pays for a day at a time, and stops when the cash runs out, nothing lost (" + (o3 != null ? o3.Filled : -1) + " bought)");

        MarketEngine g = LogMarket(344, 50000000000L, out LogWorld w4, 3, 5);
        Stock s4 = Liquid(g, MarketSize.Large);
        g.State.Account.Margin = true;
        g.State.Account.Cash = 50000000000L;
        Order o4 = g.PlaceWorked(s4.Id, OrderSide.Buy, MarketTrading.MostPerOrder(s4) * 2, g.Held, false, out _);
        UntilDone(g, o4, 6);
        Check(o4 != null && o4.Filled > 0 && w4.Purse.Moves.Count == 0 && o4.Parked == 0, "worked: in a margin account it moves none of the game's cash: no transaction at all");

        MarketEngine k = LogMarket(345, 50000000000L, out LogWorld w5, 3, 5);
        Stock buy = Liquid(k, MarketSize.Large), sell = Liquid(k, MarketSize.Large, 1);
        PutHolding(k, sell, MarketTrading.MostPerOrder(sell) * 3);
        Order b5 = k.PlaceWorked(buy.Id, OrderSide.Buy, MarketTrading.MostPerOrder(buy) * 3, k.Held, false, out _);
        Order s5 = k.PlaceWorked(sell.Id, OrderSide.Sell, MarketTrading.MostPerOrder(sell) * 3, k.Held, false, out _);
        RunTo(k, k.State.Day, MarketCatalogue.PhaseOpen + 30);
        string text = MarketStore.Write(k.State);
        MarketState back = MarketStore.Read(text, out _);
        Order rb = back != null ? back.Order(b5.Id) : null, rs = back != null ? back.Order(s5.Id) : null;
        Check(b5.Parked > 0 && s5.Due > 0 && rb != null && rs != null && rb.Parked == b5.Parked && rb.ParkedDay == b5.ParkedDay && rs.Due == s5.Due && MarketStore.Write(back) == text,
            "worked: a save in the middle of a market day keeps the money held for its purchases and the takings due from its sales, and writes them again the same");
        int g0 = text.IndexOf("~g:", StringComparison.Ordinal), g1 = text.IndexOf('~', g0 + 3);
        string part = text.Substring(g0 + 3, (g1 < 0 ? text.Length : g1) - g0 - 3);
        string shorter = string.Join(";", part.Split(';').Select(x => string.Join("|", x.Split('|').Take(26))));
        MarketState older = MarketStore.Read(text.Replace("~g:" + part, "~g:" + shorter), out _);
        Check(older != null && older.Orders.Where(x => x.Worked).All(x => x.Parked == 0 && x.Due == 0 && x.ParkedDay == 0) && older.Orders.Count(x => x.Worked) == 2,
            "worked: a record of part g without them reads nothing held and nothing due");
    }

    // A guest's trades by company: the same moves and shares on the host as one line a fill (the host's moves only add up);
    // a sum over what a line's move may be goes over several lines with none of it clamped away; a catch-up of many slices
    // (the reviewer's: 92 of 156 lines dropped, 911,831 of 1,640,666 shares never moving the city's price) loses nothing,
    // at most 64 lines a request and the rest at the next.
    private static void InvestGuestLines()
    {
        var rng = new Random(156);
        MarketEngine a = InvestMarket(377, 1000, out _, 3, 5), b = InvestMarket(377, 1000, out _, 3, 5);
        Stock[] three = { Liquid(a, MarketSize.Large), Liquid(a, MarketSize.Large, 1), Liquid(a, MarketSize.Mid) };
        var fills = new List<(int Stock, int Micros, long Shares)>();
        for (int i = 0; i < 156; i++)
        {
            fills.Add((three[i % 3].Id, rng.Next(-150000, 150001), rng.Next(1000, 20000)));
        }
        foreach ((int Stock, int Micros, long Shares) f in fills)
        {
            a.GuestTrade(f.Stock, f.Micros, f.Shares);   // (one line a fill, all of it taken: none over a line's most)
        }
        var lines = new GuestTrades();
        foreach ((int Stock, int Micros, long Shares) f in fills)
        {
            lines.Add(f.Stock, f.Micros, f.Shares);
        }
        int requests = 0, sent = 0, widest = 0;
        while (!lines.Empty && requests < 50)
        {
            var request = new List<string>();
            lines.Send(request.Add, GuestTrades.LinesMost);
            requests++;
            sent += request.Count;
            widest = Math.Max(widest, request.Count);
            foreach (string line in request)
            {
                string[] f = line.Split('|');
                b.GuestTrade(int.Parse(f[1]), int.Parse(f[2]), long.Parse(f[3]));
            }
        }
        RunTo(a, a.State.Day, a.State.Phase + 1);
        RunTo(b, b.State.Day, b.State.Phase + 1);
        bool same = three.All(s => Math.Abs(a.State.Stock(s.Id).Mis - b.State.Stock(s.Id).Mis) < 1e-9 && a.State.Stock(s.Id).DayVolume == b.State.Stock(s.Id).DayVolume);
        Check(same && sent <= fills.Count / 10 && requests == 1 && widest <= GuestTrades.LinesMost,
            "guest: a guest's trades sent by company move the host's prices and volume as one line a fill would (" + fills.Count + " fills in " + sent + " lines)");
        // Large moves: over several lines, none clamped; what does not fit in a request waits for the next.
        var big = new GuestTrades();
        int stock = three[0].Id;
        long micros = 0, shares = 0;
        for (int i = 0; i < 40; i++)
        {
            int m = i % 5 == 4 ? -150000 : 1000000;
            big.Add(stock, m, 10 + i);
            micros += m;
            shares += 10 + i;
        }
        long gotMicros = 0, gotShares = 0;
        int calls = 0, over = 0, total = 0;
        while (!big.Empty && calls < 100)
        {
            var request = new List<string>();
            big.Send(request.Add, 4);
            calls++;
            total += request.Count;
            foreach (string line in request)
            {
                string[] f = line.Split('|');
                long lm = long.Parse(f[2]), ls = long.Parse(f[3]);
                gotMicros += lm;
                gotShares += ls;
                over += Math.Abs(lm) > GuestTrades.MicrosMost || ls < 1 ? 1 : 0;
            }
        }
        Check(gotMicros == micros && gotShares == shares && over == 0 && total == (int)((micros + GuestTrades.MicrosMost - 1) / GuestTrades.MicrosMost) && calls == (total + 3) / 4,
            "guest: a sum of moves over a line's most goes over several lines, none clamped and no share lost, and what a request cannot take waits for the next (" + total + " lines)");
        // The catch-up: 156 slices of two companies in one second, all of them taken by a host that reads 64 lines of a request.
        var catchUp = new GuestTrades();
        long all = 0, oneLineAFill = 0;
        for (int i = 0; i < 156; i++)
        {
            long q = 10000 + i * 7;
            catchUp.Add(i % 2 == 0 ? 11 : 12, 900, q);
            all += q;
            oneLineAFill += i < GuestTrades.LinesMost ? q : 0;   // (as before: a line a fill, the host reading the first 64)
        }
        long taken = 0;
        int asked = 0;
        while (!catchUp.Empty && asked < 10)
        {
            var request = new List<string>();
            catchUp.Send(request.Add, GuestTrades.LinesMost);
            asked++;
            foreach (string line in request.Take(GuestTrades.LinesMost))
            {
                taken += long.Parse(line.Split('|')[3]);
            }
        }
        Console.WriteLine("  guest: " + fills.Count + " fills of three companies in " + sent + " line(s); 40 large moves in " + total + " lines of at most " + GuestTrades.MicrosMost
                          + " micros; a catch-up of 156 slices: " + taken + " of " + all + " shares taken (a line a fill: " + oneLineAFill + ")");
        Check(taken == all && asked == 1 && oneLineAFill < all,
            "guest: a catch-up of 156 slices in one second loses none of them on the host (" + all + " shares; a line a fill lost " + (all - oneLineAFill) + ")");
    }

    // The options switched off while the market catches up (the second's look has not come): at the next open a dividend
    // waiting to be put back is refused there with the reason, its money left in the cash, and a worked order takes no
    // slice and holds no money; the look then cancels it with its money back.
    private static void InvestOptionOffOpen()
    {
        MarketEngine m = LogMarket(351, 50000000000L, out LogWorld world, 3, 5);
        Stock s = Liquid(m, MarketSize.Large), t = Liquid(m, MarketSize.Large, 1);
        Order w = m.PlaceWorked(s.Id, OrderSide.Buy, MarketTrading.MostPerOrder(s) * 4, m.Held, false, out _);
        RunTo(m, m.State.Day);
        Order back = WaitingBack(m, t, 5000000L);
        m.ReinvestOn = false;
        m.LargeOn = false;
        long filled = w.Filled, held = m.State.Held(t.Id), cash = world.Purse.Money;
        int holds = world.Purse.Count(MarketMoney.WorkedHeld);
        RunTo(m, MarketTrading.NextTradingDay(m.State.Day), MarketCatalogue.PhaseOpen + 20);
        Check(back.State == OrderState.Refused && back.Why == OrderWhy.Switched && back.Filled == 0 && m.State.Held(t.Id) == held && world.Told.Any(x => x.Order == back && x.Qty == 0 && x.Why == OrderWhy.Switched),
            "put back: a dividend waiting while the option was switched off is refused at the open with the reason, its money left in the cash");
        Check(w.IsOpen && w.Filled == filled && world.Purse.Count(MarketMoney.WorkedHeld) == holds && world.Purse.Money == cash && w.Parked == 0,
            "worked: with the option off at the open a worked order takes no slice and holds no money");
        Check(m.StopAllOf(false, OrderWhy.Switched) == 1 && !w.IsOpen && w.Why == OrderWhy.Switched, "worked: and the option's look cancels it with its reason");
    }

    // A bond set to roll over repays nothing into the cash at its end: its last payment is its interest alone, marked as
    // rolling over, wherever what is coming is shown (the Portfolio, the Bonds tab, the Planner), and as before with bonds
    // switched off or the rollover not set.
    private static void InvestRollTexts()
    {
        MarketEngine m = BondMarket(361, 60, 1000000000L, out MarketWorld world);
        BondHolding h = m.BuyBond(OfferOf(m, 2), 100000, 0, true, true, out BondWhy why, 0, true);
        if (h == null)
        {
            Check(false, "rollover: a bond to roll over is bought (" + why + ")");
            return;
        }
        h.Paid = h.Payments - 1;
        BondDef d = BondCatalogue.Find(h.Def);
        long interest = BondRules.InterestCents(h, d);
        var rows = new List<PortfolioRow>();
        InvestRules.Holdings(m.State, m.State.Phase, rows, true);
        ComingPayment rolls = rows.First(x => x.Bond == h.Id).Next;
        InvestRules.Holdings(m.State, m.State.Phase, rows, false);
        ComingPayment off = rows.First(x => x.Bond == h.Id).Next;
        h.Rollover = false;
        InvestRules.Holdings(m.State, m.State.Phase, rows, true);
        ComingPayment ends = rows.First(x => x.Bond == h.Id).Next;
        Check(rolls.Rolls && !rolls.Ends && rolls.Cents == interest && off.Ends && !off.Rolls && off.Cents == interest + h.Face && ends.Ends && ends.Cents == interest + h.Face
              && InvestRules.Rolls(null, true) == false,
            "rollover: a bond set to roll over shows its last payment without what it repays, marked as rolling over; with bonds off or no rollover, with it");
        string bonds = ModSource(@"Markets\MarketApp.Bonds.cs"), portfolio = ModSource(@"Markets\MarketApp.Portfolio.cs"), planner = ModSource(@"Wiring\MarketLinks.cs");
        Check(bonds.Contains("\"reallife_mkt_bond_held_last_roll\"") && bonds.Contains("\"reallife_mkt_inv_next_roll\"") && bonds.Contains("InvestRules.Rolls(h, Settings.MarketsBonds)")
              && portfolio.Contains("InvestRules.Holdings(s, s.Phase, _holdings, Settings.MarketsBonds)") && portfolio.Contains("\"reallife_mkt_inv_next_roll\"")
              && planner.Contains("\"reallife_mkt_plan_bond_roll_title\"") && planner.Contains("\"reallife_mkt_plan_bond_roll_what\"") && planner.Contains("InvestRules.Rolls(h, Settings.MarketsBonds)"),
            "rollover: the Bonds tab, the Portfolio and the Planner say a rolling bond rolls over and count none of what it repays");
    }

    // The bonds held at once: 24, and 48 with the option on. One who holds more than 24 and switches it off keeps them and
    // buys none (a ladder neither) until under 24.
    private static void InvestBondCount()
    {
        MarketEngine m = BondMarket(362, 60, 100000000000L, out MarketWorld world);
        BondOffer o = OfferOf(m, 2);
        long least = BondRules.Least(m.State.Env.PriceLevel);
        m.LargeOn = true;
        while (m.State.Bonds.Holdings.Count < 30 && m.BuyBond(o, least, 0, true, true, out _) != null)
        {
        }
        bool large = m.State.Bonds.Holdings.Count == 30 && m.CanBuyBond(o, least, true, true) == BondWhy.None;
        m.LargeOn = false;
        bool kept = m.State.Bonds.Holdings.Count == 30 && m.CanBuyBond(o, least, true, true) == BondWhy.Full && m.LadderWhy(least * 4, true, true) == BondWhy.Full;
        while (m.State.Bonds.Holdings.Count > 23 && m.SellBond(m.State.Bonds.Holdings[0].Id, true, out _, out _))
        {
        }
        bool again = m.State.Bonds.Holdings.Count == 23 && m.CanBuyBond(o, least, true, true) == BondWhy.None && m.LadderWhy(least * 4, true, true) == BondWhy.Full;
        while (m.State.Bonds.Holdings.Count > 20 && m.SellBond(m.State.Bonds.Holdings[0].Id, true, out _, out _))
        {
        }
        bool ladder = m.LadderWhy(least * 4, true, true) == BondWhy.None;
        Check(large && kept && again && ladder,
            "bonds: 48 held at once with the option on, 24 with it off: one holding 30 keeps them and buys none, a bond again under 24, a ladder of four under 21");
    }

    // A sum larger than the game moves in one transaction is moved in pieces of int.MaxValue at most, none of it lost; the
    // game's wallet does so for taking, forcing and giving alike.
    private static void InvestPieces()
    {
        long left = 5000000000L;
        var pieces = new List<long>();
        while (left > 0 && pieces.Count < 10)
        {
            pieces.Add(InvestRules.Piece(ref left));
        }
        long one = 1L << 30, two = (1L << 30) + 1, none = 0, under = -5;
        long p1 = InvestRules.Piece(ref one), p2 = InvestRules.Piece(ref two);
        Check(pieces.SequenceEqual(new[] { 1073741824L, 1073741824L, 1073741824L, 1073741824L, 705032704L }) && left == 0 && p1 == 1L << 30 && one == 0 && p2 == 1L << 30 && two == 1
              && InvestRules.Piece(ref none) == 0 && none == 0 && InvestRules.Piece(ref under) == 0 && under == -5,
            "money: a sum over what the game moves at once goes in pieces of 2^30 at most, to the unit; nothing of nothing");
        // The game's money is a float: a piece of int.MaxValue would become 2^31 there, a unit too many each time; 2^30 is
        // held exactly, so a sum in whole pieces moves exactly what it is (a float's own rounding aside, as for any sum).
        float exact = InvestRules.PieceMost, wide = int.MaxValue;
        Check((long)exact == InvestRules.PieceMost && (long)wide != int.MaxValue && (double)(float)(InvestRules.PieceMost * 4) == InvestRules.PieceMost * 4,
            "money: a piece is a sum a float holds exactly (2^30), where int.MaxValue becomes 2^31 as a float");
        string game = ModSource(@"Markets\MarketGame.cs");
        int at = game.IndexOf("public sealed class MarketWallet", StringComparison.Ordinal), end = at >= 0 ? game.IndexOf("public static long Wealth()", at, StringComparison.Ordinal) : -1;
        string wallet = at >= 0 && end > at ? game.Substring(at, end - at) : string.Empty;
        Check(wallet.Contains("InvestRules.Piece(ref left)") && !wallet.Contains("Math.Min(amount, int.MaxValue)") && !wallet.Contains("amount <= int.MaxValue") && wallet.Contains("Move(-1, amount")
              && wallet.Contains("Move(1, amount"),
            "money: the game's wallet takes, forces and gives a large sum in pieces, never cut at int.MaxValue");
    }

    // The put-back test pays nothing early when the dividend owed is on a holding that puts none back (nor with the option
    // off): it is refused with its own notice; with a share set it pays and puts it back; with none owed it says so.
    private static void InvestTestRefuses()
    {
        MarketEngine m = LogMarket(371, 1000000000L, out LogWorld world, 3, 5);
        Stock t = Trusts(m).OrderByDescending(x => x.DpsCents).First();
        Position p = PutHolding(m, t, 50000);
        int day = m.State.Day;
        for (int i = 0; i < 15 && !m.State.Dues.Any(x => x.StockId == t.Id && x.Bot == 0 && x.Cents > 0); i++)
        {
            day = TradingOnOrAfter(day + 1);
            RunTo(m, day, MarketCatalogue.PhaseOpen + 5);
        }
        long cash = world.Purse.Money;
        int dues = m.State.Dues.Count;
        bool none = dues > 0 && m.PayNextDueNow() == -1 && world.Purse.Money == cash && m.State.Dues.Count == dues;
        p.Reinvest = 50;
        m.ReinvestOn = false;
        bool off = m.PayNextDueNow() == -1 && world.Purse.Money == cash;
        m.ReinvestOn = true;
        bool paid = m.PayNextDueNow() == t.Id && world.Purse.Money > cash && m.State.Dues.Count == dues - 1;
        m.State.Dues.Clear();
        bool owedNone = m.PayNextDueNow() == 0;
        string director = ModSource(@"Markets\MarketDirector.Invest.cs");
        Check(none && off && paid && owedNone && director.Contains("TestRefusals.TellKey(Logger, Test, \"reallife_mkt_inv_test_no_share\")"),
            "put back: the test refuses with its own notice, paying nothing early, when the dividend owed is on a holding that puts none back or the option is off; with a share set it pays");
    }

    // The most of one bond purchase counts what the player is worth as the Finance app does (the game's figure less the
    // mod's own debts), through the wiring, with what the market holds and the money with the broker added once; the day's
    // money of worked orders counts in what the market holds; the new transactions are filed under investments.
    private static void InvestWorthWiring()
    {
        MarketEngine m = InvestMarket(381, 50000000000L, out MarketWorld world, 3, 5);
        Stock s = Liquid(m, MarketSize.Large);
        long before = InvestRules.MarketWorth(m.State, m.State.Phase);
        Order o = Worked(m, s, OrderSide.Buy, MarketTrading.MostPerOrder(s) * 3, out _);
        o.Parked = 1234;
        o.Due = 567;
        long with = InvestRules.MarketWorth(m.State, m.State.Phase), broker = InvestRules.WithBroker(m.State, out long parked, out long due);
        Check(with - before == 1801 && broker == 1801 && parked == 1234 && due == 567, "invest: the money with the broker for worked orders counts in what the player holds on the market");
        Check(RealLifeOverhaul.Economy.CashCategory.For(MarketMoney.WorkedHeld) == RealLifeOverhaul.Economy.CashCategory.Investments && RealLifeOverhaul.Economy.CashCategory.For(MarketMoney.WorkedBack) == RealLifeOverhaul.Economy.CashCategory.Investments
              && RealLifeOverhaul.Economy.CashCategory.For(MarketMoney.WorkedSold) == RealLifeOverhaul.Economy.CashCategory.Investments,
            "invest: the worked orders' transactions are filed under investments in the Finance app, with the other shares");
        string director = ModSource(@"Markets\MarketDirector.Invest.cs"), bridges = ModSource(@"Markets\MarketBridges.cs"), links = ModSource(@"Wiring\MarketLinks.cs");
        string books = ModSource(@"Economy\Finance\Books.cs"), overview = ModSource(@"Economy\Finance\FinanceApp.Overview.cs");
        Check(director.Contains("Func<long> net = MarketBridges.NetWorth;") && director.Contains("(net != null ? net() : MarketWallet.Wealth()) + InvestRules.MarketWorth(")
              && bridges.Contains("public static Func<long> NetWorth;") && bridges.Contains("NetWorth = null;") && links.Contains("MarketBridges.NetWorth = Books.NetWorth;")
              && books.Contains("public static long NetWorth()") && books.Contains("Owing(file.Accounts[i])") && books.Contains("EconomyBridges.OtherDebts?.Invoke(Debts);")
              && books.Contains("file.Shark.Active") && overview.Contains("if (!Books.Owing(a))") && overview.Contains("FinanceText.Money(Books.NetWorth())")
              && EngineSources().Length >= 9 && !EngineSources().Any(e => e.Contains("RealLifeOverhaul.Economy")),
            "invest: the most of one bond purchase reads the Finance app's net worth through the wiring (the same accounts, the same debts), the market adding its own once, and no feature calls the other");
    }

    // ---------------------------------------------------------------- the second review: the cash running out, the money
    // with the broker still the player's, a busy list on load, a payout giving way, the save tools, the market stopping

    // The reviewer's case, seed 691: a worked purchase of ten days' most with half a day's cost in cash and a fifth of it
    // coming in each night. It buys what the cash pays for and stops when the cash runs out, as before the day's money was
    // parked (450,850 shares, stopped on its first market day), and every night's takings after stay in the cash: it never
    // parks them, day after day.
    private static void InvestCashRunsOut()
    {
        MarketEngine m = InvestMarket(691, 0L, out MarketWorld w, 3, 5);
        Stock s = Liquid(m, MarketSize.Large);
        long per = MarketTrading.MostPerOrder(s);
        long dayCost = MarketTrading.Value(per, MarketTrading.QuoteOf(m.State, s, m.State.Phase).Ask, true);
        w.Purse.Money = dayCost / 2;
        Order o = m.PlaceWorked(s.Id, OrderSide.Buy, per * 10, m.Held, false, out _);
        int first = m.State.Day, day = first;
        long close = -1;
        bool kept = true;
        for (int i = 0; i < 6; i++)
        {
            RunTo(m, day);
            if (close >= 0 && o != null && !o.IsOpen)
            {
                kept &= w.Purse.Money >= close + dayCost / 5;   // (the night's takings are still there at the next close)
            }
            close = w.Purse.Money;
            w.Purse.Give(dayCost / 5, "takings", string.Empty);
            day = TradingOnOrAfter(day + 1);
        }
        Check(o != null && !o.IsOpen && o.Why == OrderWhy.NoMoney && o.Filled == 450850 && o.EndDay == first && kept && o.Parked == 0,
            "worked: far over the cash, it buys what the cash pays for and stops when it runs out, as before its money was parked (seed 691: "
            + (o != null ? o.Filled + " shares, stopped on day " + o.EndDay : "none") + "), and leaves every night's takings after in the cash");
    }

    // The money a worked order has with the broker stays the player's for everything else in the market: the reviewer's
    // case, seed 661, where a worked purchase parks the whole cash and a resting limit and a ticket purchase of another
    // company then still fill (both did before the money was parked); seed 681, where four trading bots buy about as often
    // beside a worked purchase over the cash as before (21 times in 12 days); and the cash never goes under nothing by a
    // worked order's own money, nor while any money is still with the broker.
    private static void InvestBrokerStaysYours()
    {
        MarketEngine m = InvestMarket(661, 0L, out MarketWorld w, 3, 5);
        Stock s = Liquid(m, MarketSize.Large), t = Liquid(m, MarketSize.Large, 1);
        long per = MarketTrading.MostPerOrder(s);
        w.Purse.Money = MarketTrading.Value(per, MarketTrading.QuoteOf(m.State, s, m.State.Phase).Ask, true);
        Quote qt = MarketTrading.QuoteOf(m.State, t, m.State.Phase);
        Order limit = Trade(m, t, OrderSide.Buy, 2000, OrderType.Limit, Math.Max(1, qt.Bid / 2), 0, OrderLife.UntilCancelled);
        Order o = m.PlaceWorked(s.Id, OrderSide.Buy, per * 3, m.Held, false, out _);
        RunTo(m, m.State.Day, MarketCatalogue.PhaseOpen + 20);
        long cash20 = w.Purse.Money;
        limit.LimitCents = int.MaxValue / 2;
        RunTo(m, m.State.Day, MarketCatalogue.PhaseOpen + 24);
        Order now = Trade(m, t, OrderSide.Buy, 1000);
        Check(cash20 == 0 && o != null && o.Parked > 0 && limit.State == OrderState.Filled && limit.Filled == 2000 && now != null && now.State == OrderState.Filled && now.Filled == 1000,
            "worked: with all the cash parked for the day, a resting limit and a ticket purchase of another company still fill, from the worked order's money (seed 661)");
        int[] buys = new int[2];
        int shortWhile = 0;
        for (int with = 0; with < 2; with++)
        {
            MarketEngine b = LogMarket(681, 0L, out LogWorld lw, 3, 5);
            Stock bs = Liquid(b, MarketSize.Large);
            long bper = MarketTrading.MostPerOrder(bs);
            long dayCost = MarketTrading.Value(bper, MarketTrading.QuoteOf(b.State, bs, b.State.Phase).Ask, true);
            lw.Purse.Money = dayCost * 3;
            foreach (BotFirm f in MarketBots.All)
            {
                b.Hire(f.Id, dayCost / 2, f.Size, f.Stop, f.Loss, out _);
            }
            if (with == 1)
            {
                b.PlaceWorked(bs.Id, OrderSide.Buy, bper * 40, b.Held, false, out _);
            }
            RunTo(b, TradingOnOrAfter(b.State.Day + 12));
            buys[with] = b.State.Trades.Count(x => x.Bot != 0 && x.Kind == TradeKind.Bought);
            shortWhile += lw.Purse.ShortWhileBroker;
        }
        Console.WriteLine("  worked: four bots bought " + buys[0] + " times in 12 days alone and " + buys[1] + " beside a worked purchase over the cash (seed 681; 21 before its money was parked)");
        Check(buys[1] * 5 >= buys[0] * 4 && shortWhile == 0,
            "worked: trading bots buy about as often beside a worked purchase over the cash as without it, and the cash never goes under nothing by its money or while money is with the broker");
    }

    // A purchase or a charge in the market that needs more than the cash in hand takes back what it needs of the money with
    // the broker (with the margin for the game's rounding), and no more: a worked sale's takings first, then the parked
    // money of the worked purchase placed last, one transaction for each order; what it took back the order goes on
    // without, and the money is conserved to the unit.
    private static void InvestTakeBack()
    {
        MarketEngine m = LogMarket(662, 0L, out LogWorld w, 3, 5);
        Stock a = Liquid(m, MarketSize.Mid), b = Liquid(m, MarketSize.Mid, 1), sold = Liquid(m, MarketSize.Mid, 2), c = Liquid(m, MarketSize.Large);
        long perA = MarketTrading.MostPerOrder(a), perB = MarketTrading.MostPerOrder(b);
        PutHolding(m, sold, MarketTrading.MostPerOrder(sold) * 2);
        w.Purse.Money = MarketTrading.Value(perA, MarketTrading.QuoteOf(m.State, a, m.State.Phase).Ask, true)
                        + MarketTrading.Value(perB, MarketTrading.QuoteOf(m.State, b, m.State.Phase).Ask, true);
        Order first = m.PlaceWorked(a.Id, OrderSide.Buy, perA * 3, m.Held, false, out _);
        Order last = m.PlaceWorked(b.Id, OrderSide.Buy, perB * 3, m.Held, false, out _);
        Order sale = m.PlaceWorked(sold.Id, OrderSide.Sell, MarketTrading.MostPerOrder(sold) * 2, m.Held, false, out _);
        RunTo(m, m.State.Day, MarketCatalogue.PhaseOpen + 30);
        long free = w.Purse.Money, due = sale.Due, lastParked = last.Parked, firstParked = first.Parked;
        Quote qc = MarketTrading.QuoteOf(m.State, c, m.State.Phase);
        long qty = Math.Max(1L, (free + due + lastParked / 2) * 100L / Math.Max(1, qc.Ask));   // (more than the cash in hand and the takings: some of the parked money too)
        int movesBefore = w.Purse.Moves.Count;
        Order buy = Trade(m, c, OrderSide.Buy, Math.Min(qty, MarketTrading.MostPerOrder(c)));
        var taken = w.Purse.Moves.Skip(movesBefore).ToList();
        int fromSale = taken.Count(x => x.Why == MarketMoney.WorkedSold), fromParked = taken.Count(x => x.Why == MarketMoney.WorkedBack);
        bool order = buy != null && buy.State == OrderState.Filled && due > 0 && lastParked > 0 && sale.Due == 0 && fromSale == 1 && fromParked == 1 && first.Parked == firstParked
                     && last.Parked < lastParked && last.Parked > 0 && w.Purse.Money >= 0 && qty <= MarketTrading.MostPerOrder(c);
        Check(order, "worked: a purchase short of cash takes back what it needs: a worked sale's takings first, then the parked money of the worked purchase placed last, one transaction each ("
                     + fromSale + " and " + fromParked + ")");
        UntilDone(m, first, 10);
        UntilDone(m, last, 10);
        RunTo(m, TradingOnOrAfter(m.State.Day + 1));
        long held = -w.Purse.Sum(MarketMoney.WorkedHeld), back = w.Purse.Sum(MarketMoney.WorkedBack);
        long cost = m.State.Trades.Where(x => x.Order == first.Id || x.Order == last.Id).Sum(x => -x.Value + x.Fee);
        long takings = m.State.Trades.Where(x => x.Order == sale.Id).Sum(x => x.Value - x.Fee);
        Check(held == cost + back && w.Purse.Sum(MarketMoney.WorkedSold) == takings && first.Parked == 0 && last.Parked == 0 && sale.Due == 0 && w.Purse.Low >= 0
              && w.Purse.ShortWhileBroker == 0,
            "worked: with money taken back in the middle of a day, what was parked is still what the slices cost and what came back, to the unit, and a sale's takings what its slices brought");
    }

    // A charge the market forces (a bot's fee, a listing's fee, a margin call's shortfall) takes what it needs of the money
    // with the broker before it takes the cash in hand under nothing, with the margin for the game's rounding left in the
    // cash: one transaction back from the order, the cash never under nothing while money is still with the broker. A
    // ladder's pieces are one purchase: what is short comes back once.
    private static void InvestForceTakesBack()
    {
        MarketEngine m = LogMarket(663, 0L, out LogWorld w, 3, 5);
        Stock s = Liquid(m, MarketSize.Large);
        long per = MarketTrading.MostPerOrder(s);
        w.Purse.Money = MarketTrading.Value(per, MarketTrading.QuoteOf(m.State, s, m.State.Phase).Ask, true);
        Order o = m.PlaceWorked(s.Id, OrderSide.Buy, per * 3, m.Held, false, out _);
        RunTo(m, m.State.Day, MarketCatalogue.PhaseOpen + 10);
        long free = w.Purse.Money, parked = o != null ? o.Parked : 0, fee = free + parked / 3;
        int before = w.Purse.Moves.Count;
        m.Wallet.Force(fee, MarketMoney.BotFee, "a bot");
        int backs = w.Purse.Moves.Skip(before).Count(x => x.Why == MarketMoney.WorkedBack);
        long margin = BrokerWallet.Margin(fee);
        Check(o != null && parked > 0 && w.Purse.Money == margin && o.Parked == parked - (fee - free) - margin && backs == 1 && w.Purse.ShortWhileBroker == 0,
            "worked: a charge the market forces takes what it needs of the money with the broker first (the margin for the game's rounding left in the cash), in one transaction, and never takes the cash under nothing while any is there");
        long least = BondRules.Least(m.State.Env.PriceLevel), most = BondRules.Most(m.State.Env.PriceLevel);
        long sum = o != null ? Math.Min(o.Parked / 2, most * 4) / 4 * 4 : 0L;
        before = w.Purse.Moves.Count;
        int bought = sum >= least * 4 ? m.BuyLadder(sum, 0, false, true, true, out BondWhy why) : 0;
        int ladderBacks = w.Purse.Moves.Skip(before).Count(x => x.Why == MarketMoney.WorkedBack);
        Check(bought == 4 && ladderBacks == 1 && w.Purse.Money >= 0, "bonds: a ladder bought with the money with the broker takes it back once for its four pieces (" + bought + " bought, " + ladderBacks + " back)");
    }

    // The reviewer's case, seed 701: an active trader's list of orders (141, most of them done and kept a while) and a worked
    // order with money parked at the save. Read back, the worked order is there with its money: part g has a limit of its
    // own, and an open worked order or one with money with the broker is never dropped.
    private static void InvestBusyList()
    {
        MarketEngine m = LogMarket(701, 90000000000L, out LogWorld w, 3, 5);
        Stock s = Liquid(m, MarketSize.Large), t = Liquid(m, MarketSize.Large, 1);
        for (int d = 0; d < 10; d++)
        {
            for (int i = 0; i < 20; i++)
            {
                RunTo(m, m.State.Day, MarketCatalogue.PhaseOpen + 5 + i * 2);
                Trade(m, t, OrderSide.Buy, 10);
            }
            RunTo(m, m.State.Day);
            RunTo(m, TradingOnOrAfter(m.State.Day + 1), MarketCatalogue.PhaseOpen + 2);
        }
        Order o = m.PlaceWorked(s.Id, OrderSide.Buy, MarketTrading.MostPerOrder(s) * 3, m.Held, false, out _);
        RunTo(m, m.State.Day, MarketCatalogue.PhaseOpen + 50);
        long before = InvestRules.WithBroker(m.State, out _, out _);
        MarketState back = MarketStore.Read(MarketStore.Write(m.State), out _);
        Order r = back != null && o != null ? back.Order(o.Id) : null;
        Check(m.State.Orders.Count > MarketCatalogue.OrdersMost * 2 && o != null && o.Parked > 0 && r != null && r.Parked == o.Parked && r.IsOpen
              && InvestRules.WithBroker(back, out _, out _) == before,
            "store: with a busy list of orders (" + m.State.Orders.Count + "), a worked order with money parked comes back whole with its money (seed 701)");
    }

    // The reviewer's case, seed 721: a limit purchase placed after the close, when a dividend put back already waits for the
    // open. The payout gives way to it as to every open purchase of the company: it buys only the room left beside the
    // limit, the limit is not refused and fills, and the holding never passes the most.
    private static void InvestBackGivesWay()
    {
        MarketEngine m = LogMarket(721, 90000000000L, out LogWorld w, 3, 5);
        Stock s = Liquid(m, MarketSize.Large);
        long most = MarketTrading.MostHeld(s), per = MarketTrading.MostPerOrder(s);
        PutHolding(m, s, most - per, 100);
        RunTo(m, m.State.Day);
        Quote q = MarketTrading.QuoteOf(m.State, s, MarketCatalogue.PhaseOpen);
        Order back = WaitingBack(m, s, MarketTrading.Value(per, q.Ask, true) * 3);
        Order limit = m.Place(s.Id, OrderSide.Buy, OrderType.Limit, OrderLife.UntilCancelled, per / 2, q.Ask * 2, 0, m.Trading, m.Held, false, out OrderWhy why);
        long high = 0;
        int day = MarketTrading.NextTradingDay(m.State.Day);
        for (int i = 0; i < 3; i++)
        {
            RunTo(m, day);
            high = Math.Max(high, m.State.Held(s.Id));
            day = TradingOnOrAfter(day + 1);
        }
        Check(limit != null && limit.State == OrderState.Filled && limit.Filled == per / 2 && back.Filled <= per - per / 2 && high <= most,
            "put back: a dividend waiting for the open gives way to a limit placed after the close: it buys only the room left beside it, the limit fills whole, the holding never passes the most (seed 721: "
            + back.Filled + " put back, the limit " + (limit != null ? limit.Filled.ToString() : why.ToString()) + ")");
    }

    // The reviewer's case, seed 671: a worked purchase whose rest is over the room, and a dividend put back on the same
    // company: refused for the holding limit while the holding itself is far from it. The reason says so: it is the holding
    // with the open orders for it, in each language. (OrderWhy is unchanged: a saved reason reads as ever.)
    private static void InvestBackReason()
    {
        MarketEngine m = LogMarket(671, 90000000000L, out LogWorld w, 3, 5);
        Stock s = Liquid(m, MarketSize.Large);
        long most = MarketTrading.MostHeld(s);
        PutHolding(m, s, 1000);
        Order o = m.PlaceWorked(s.Id, OrderSide.Buy, most * 2, m.Held, false, out _);
        Order back = WaitingBack(m, s, 5000000L);
        RunTo(m, MarketTrading.NextTradingDay(m.State.Day), MarketCatalogue.PhaseOpen + 1);
        var langs = LanguageWords(out string[] codes);
        Dictionary<string, string> en = langs[0], pt = langs[1], zh = langs[2];
        bool words = LanguagesRead("put back", langs, codes) && LanguagesLacking(langs, codes, "reallife_mkt_why_holding_limit").Length == 0 && en["reallife_mkt_why_holding_limit"].Contains("with your open orders for it")
                     && pt["reallife_mkt_why_holding_limit"].Contains("com suas ordens abertas") && zh["reallife_mkt_why_holding_limit"].Contains("未成交订单");
        Check(o != null && back.State == OrderState.Refused && back.Why == OrderWhy.HoldingLimit && m.State.Held(s.Id) * 100 < most && words && (int)OrderWhy.HoldingLimit == 7,
            "put back: refused for the room the open orders hold, its reason says it is the holding with the open orders for it, in each language (seed 671)");
    }

    // The save tools settle everything for one sum: the money worked orders have with the broker is in it, taken off them
    // first, so nothing they do after gives it back a second time (MarketDirector.StopAll).
    private static void InvestSaveTools()
    {
        MarketEngine m = LogMarket(731, 90000000000L, out LogWorld w, 3, 5);
        Stock s = Liquid(m, MarketSize.Large), t = Liquid(m, MarketSize.Large, 1);
        PutHolding(m, t, MarketTrading.MostPerOrder(t) * 2);
        Order buy = m.PlaceWorked(s.Id, OrderSide.Buy, MarketTrading.MostPerOrder(s) * 3, m.Held, false, out _);
        Order sale = m.PlaceWorked(t.Id, OrderSide.Sell, MarketTrading.MostPerOrder(t) * 2, m.Held, false, out _);
        RunTo(m, m.State.Day, MarketCatalogue.PhaseOpen + 30);
        long parkedThen = buy.Parked, dueThen = sale.Due;
        long all = InvestRules.EmptyBroker(m.State, out long parked, out long due);
        string tools = ModSource(@"Markets\MarketDirector.Tests.cs");
        int at = tools.IndexOf("public void StopAll()", StringComparison.Ordinal);
        int empty = tools.IndexOf("long total = InvestRules.EmptyBroker(s,", at, StringComparison.Ordinal), settle = tools.IndexOf("OwnSettled(log)", at, StringComparison.Ordinal),
            give = tools.IndexOf("_world.Wallet.Give(total", at, StringComparison.Ordinal), clear = tools.IndexOf("s.Orders.Clear()", at, StringComparison.Ordinal);
        Check(parkedThen > 0 && dueThen > 0 && all == parkedThen + dueThen && parked == parkedThen && due == dueThen && buy.Parked == 0 && sale.Due == 0
              && InvestRules.WithBroker(m.State, out _, out _) == 0 && at > 0 && empty > at && empty < settle && settle < give && give < clear,
            "save tools: the money with the broker is in the one sum everything is settled for, taken off the worked orders before anything else, and never given back twice");
    }

    // The market stopping (its option switched off in the middle of a day): every worked order's money with the broker is
    // settled into the cash first, so it is not short while the market is off, and the order parks again when it runs. A
    // guest's trades not yet sent are dropped as the market stands down, never reaching the next host.
    private static void InvestStandDown()
    {
        MarketEngine m = LogMarket(741, 90000000000L, out LogWorld w, 3, 5);
        Stock s = Liquid(m, MarketSize.Large), t = Liquid(m, MarketSize.Large, 1);
        PutHolding(m, t, MarketTrading.MostPerOrder(t) * 2);
        Order buy = m.PlaceWorked(s.Id, OrderSide.Buy, MarketTrading.MostPerOrder(s) * 3, m.Held, false, out _);
        Order sale = m.PlaceWorked(t.Id, OrderSide.Sell, MarketTrading.MostPerOrder(t) * 2, m.Held, false, out _);
        RunTo(m, m.State.Day, MarketCatalogue.PhaseOpen + 30);
        long cash = w.Purse.Money, parked = buy.Parked, due = sale.Due;
        long back = m.SettleAll();
        bool settled = parked > 0 && due > 0 && back == parked + due && w.Purse.Money == cash + back && buy.Parked == 0 && sale.Due == 0 && buy.ParkedDay == 0 && buy.IsOpen;
        long filled = buy.Filled;
        RunTo(m, m.State.Day, MarketCatalogue.PhaseOpen + 40);
        string director = ModSource(@"Markets\MarketDirector.cs");
        int at = director.IndexOf("private void StandDown(bool keep)", StringComparison.Ordinal), end = at >= 0 ? director.IndexOf("private void KeepTape()", at, StringComparison.Ordinal) : -1;
        string body = at >= 0 && end > at ? director.Substring(at, end - at) : string.Empty;
        int settleAt = body.IndexOf("_engine.SettleAll()", StringComparison.Ordinal), saveAt = body.IndexOf("MarketStore.Save(", StringComparison.Ordinal);
        Check(settled && buy.Filled > filled && buy.Parked > 0 && settleAt > 0 && settleAt < saveAt && body.Contains("_guestTrades.Clear()"),
            "market: stopping, every worked order's money with the broker is settled into the cash before the market is saved, the order parks again when it runs, and a guest's unsent trades are dropped");
    }

    // ---------------------------------------------------------------- the third review: the game's float money, charges in
    // a row, a worked purchase's placement

    /// <summary>
    /// The player's cash kept as the game keeps it: a float (the save's Money), moved a piece at a time as MarketWallet.Move
    /// moves it (InvestRules.Piece: the game's parameter is a float, and so is the sum, GameManager.ChangeMoney), read
    /// floored as MarketWallet.Cash reads it. Every transaction kept, with whose it was.
    /// </summary>
    private sealed class FloatWallet : IMarketWallet
    {
        public float Money;
        public readonly List<(string Why, string Name, long Amount)> Moves = new List<(string Why, string Name, long Amount)>();

        public long Cash => (long)Math.Floor(Math.Max(0.0, Money));

        public bool Take(long amount, string why, string name)
        {
            if (amount <= 0)
            {
                return true;
            }
            if (Cash < amount)
            {
                return false;
            }
            Move(-1, amount, why, name);
            return true;
        }

        public bool Ready(long amount) => amount <= 0 || Cash >= amount;

        public void Force(long amount, string why, string name)
        {
            if (amount > 0)
            {
                Move(-1, amount, why, name);
            }
        }

        public void Give(long amount, string why, string name)
        {
            if (amount > 0)
            {
                Move(1, amount, why, name);
            }
        }

        private void Move(int sign, long amount, string why, string name)
        {
            Moves.Add((why, name, sign * amount));
            for (long left = amount; left > 0;)
            {
                float piece = sign * InvestRules.Piece(ref left);   // (the long a float, as the game's call takes it)
                Money = (float)(Money + piece);                      // (and added as a float: the save's Money += amount)
            }
        }

        /// <summary>The most give-backs one worked order made since a point of the list (by its company: one order a company here).</summary>
        public int MostBacks(int from)
        {
            return Moves.Skip(from).Where(x => x.Why == MarketMoney.WorkedBack || x.Why == MarketMoney.WorkedSold).GroupBy(x => x.Name).Select(g => g.Count()).DefaultIfEmpty(0).Max();
        }
    }

    /// <summary>The checks' city and secrets with the cash kept as the game keeps it.</summary>
    private sealed class FloatWorld : IMarketWorld
    {
        public readonly MarketWorld Inner;
        public readonly FloatWallet Purse = new FloatWallet();

        public FloatWorld(ulong seed)
        {
            Inner = new MarketWorld(seed);
        }

        public ulong NewSecret() => Inner.NewSecret();
        public MarketEnv Env(int day) => Inner.Env(day);
        public IMarketWallet Wallet => Purse;
        public int CommissionLeast => Inner.CommissionLeast;
        public void Said(in MarketNews news) => Inner.Said(news);
        public void Paid(TradeKind kind, Stock stock, long amount) => Inner.Paid(kind, stock, amount);
        public void Called(long amount, int byDay) => Inner.Called(amount, byDay);
        public void Filled(Order order, Stock stock, long qty, int cents) => Inner.Filled(order, stock, qty, cents);
    }

    // A market on a market day at a step of its session, its cash kept as the game keeps it.
    private static MarketEngine FloatMarket(ulong seed, out FloatWorld world, int from = 3, int step = 5)
    {
        world = new FloatWorld(seed);
        MarketState state = MarketSetup.Open(seed, 1, 0, 60, world.Env(1), seed, null, null, null);
        var m = new MarketEngine(state, new MarketTape(seed), world);
        SessionDay(m, from, step);
        return m;
    }

    // The sums the float checks run over: evenly on a log scale (1,000 to 20 billion unless given), every third just under
    // a power of two, where a float's sum rounds the most.
    private static long FloatSize(int i, int n, long least = 1000L, long most = 20000000000L)
    {
        double at = Math.Exp(Math.Log(least) + (Math.Log(most) - Math.Log(least)) * i / Math.Max(1, n - 1));
        long a = Math.Max(least, Math.Min(most, (long)at));
        if (i % 3 == 2)
        {
            int e = (int)Math.Floor(Math.Log(a, 2.0));
            long under = (1L << e) - 1L - a % 977L;
            a = under >= least ? under : a;
        }
        return a;
    }

    // A case's cash in hand: a part of the sum, never all of it, often with cents (the game's money has them).
    private static float FloatHand(long sum, Random r)
    {
        double share = r.Next(4) == 0 ? r.NextDouble() / 1000.0 : r.NextDouble();
        double cash = Math.Floor((sum - 1) * share) + (r.Next(2) == 0 ? r.Next(100) / 100.0 : 0.0);
        float f = (float)cash;
        return (long)Math.Floor((double)f) >= sum ? 0f : f;
    }

    // A case's money with the broker: so much on one to three worked orders of different companies (a sale's takings due,
    // purchases' money parked), the orders of the case before taken out.
    private static void BrokerMoney(MarketState s, Stock[] on, long total, Random r)
    {
        s.Orders.RemoveAll(x => x.Worked);
        int n = 1 + r.Next(Math.Min(3, on.Length));
        long left = total;
        for (int i = 0; i < n; i++)
        {
            long part = i == n - 1 ? left : (long)(left * r.NextDouble());
            left -= part;
            bool sale = i == 0 && n > 1 && r.Next(2) == 0;
            s.Orders.Add(new Order
            {
                Id = s.NextOrderId++, StockId = on[i].Id, Side = sale ? OrderSide.Sell : OrderSide.Buy, Type = OrderType.Market, Life = OrderLife.UntilDone, Qty = 1000,
                PlacedDay = s.Day, State = OrderState.Open, Worked = true, Parked = sale ? 0L : part, Due = sale ? part : 0L, ParkedDay = sale ? 0 : s.Day
            });
        }
    }

    // The reviewer's case: the game's money is a float, so a give-back of exactly the shortfall often leaves the cash a few
    // units short of a purchase over 2^24 (50,000,000 in hand and 10,000,001 back come to 60,000,000), and the purchase was
    // refused after the money came back. With the margin (BrokerWallet.Margin) a purchase that the cash in hand and the
    // money with the broker cover with the margin goes through, from 1,000 to 20 billion, as a fill takes it (its price,
    // then its commission): both taken, one give-back at most from any order, the cash never under nothing after it.
    private static void InvestFloatFills()
    {
        MarketEngine m = FloatMarket(771, out FloatWorld w, 3, 5);
        Stock buy = Liquid(m, MarketSize.Large);
        Stock[] on = { Liquid(m, MarketSize.Large, 1), Liquid(m, MarketSize.Mid), Liquid(m, MarketSize.Mid, 1) };
        var r = new Random(771);
        float exact = 50000000f;
        exact = (float)(exact + (float)10000001L);
        w.Purse.Money = 50000000f;
        BrokerMoney(m.State, new[] { on[0] }, 10000001L + BrokerWallet.Margin(60000001L), r);
        bool example = (long)Math.Floor((double)exact) == 60000000L && m.Wallet.Ready(60000001L) && w.Purse.Cash >= 60000001L;
        const int cents = 10000;   // (a share at 100: sums as fine as a share's price)
        int least = w.CommissionLeast, n = 240, cases = 0, refused = 0, unpaid = 0, twice = 0, under = 0;
        for (int i = 0; i < n; i++)
        {
            long qty = Math.Max(1L, FloatSize(i, n) / 101L);
            long value = MarketTrading.Value(qty, cents, true), fee = MarketTrading.Commission(value, least), sum = value + fee;
            w.Purse.Money = FloatHand(sum, r);
            long need = sum - w.Purse.Cash;
            if (need <= 0)
            {
                continue;
            }
            cases++;
            long extra = r.Next(3) == 0 ? 0L : (long)(sum * r.NextDouble());
            BrokerMoney(m.State, on, need + BrokerWallet.Margin(sum) + extra, r);
            var order = new Order { Id = m.State.NextOrderId++, StockId = buy.Id, Side = OrderSide.Buy, Type = OrderType.Market, Life = OrderLife.Day, Qty = qty, PlacedDay = m.State.Day, State = OrderState.Open };
            int from = w.Purse.Moves.Count;
            bool filled = MarketTrading.Fill(m.State, buy, order, qty, cents, least, m.Wallet, m.State.Phase, false, out _);
            var moves = w.Purse.Moves.Skip(from).ToList();
            refused += filled ? 0 : 1;
            unpaid += filled && (moves.Count(x => x.Why == MarketMoney.Bought && x.Amount == -value) != 1 || moves.Count(x => x.Why == MarketMoney.Commission && x.Amount == -fee) != 1) ? 1 : 0;
            twice += w.Purse.MostBacks(from) > 1 ? 1 : 0;
            under += w.Purse.Money < 0f ? 1 : 0;
        }
        Check(example && cases > 150 && refused == 0 && unpaid == 0 && twice == 0 && under == 0,
            "worked: in the game's float money, a purchase the cash in hand and the money with the broker cover with the margin goes through, its price and its commission taken with one give-back at most from any order, from 1,000 to 20 billion ("
            + cases + " cases; refused " + refused + ", unpaid " + unpaid + ", twice " + twice + ", under nothing " + under + ")");
    }

    // A ladder bought with the money with the broker, from its least to the most its four purchases may be, with the cash
    // kept as the game keeps it: all four bought, one give-back at most from any order, the cash never under nothing.
    private static void InvestFloatLadders()
    {
        MarketEngine m = FloatMarket(772, out FloatWorld w, 3, 5);
        Stock[] on = { Liquid(m, MarketSize.Large), Liquid(m, MarketSize.Large, 1), Liquid(m, MarketSize.Mid) };
        var r = new Random(772);
        int level = m.State.Env.PriceLevel, n = 90, cases = 0, notAll = 0, twice = 0, under = 0;
        long least = BondRules.Least(level) * 4L, most = BondRules.Cap(level) * 4L, worth = 1L << 50;
        for (int i = 0; i < n; i++)
        {
            long sum = Math.Max(least, FloatSize(i, n, least, most) / 4L * 4L);
            m.State.Bonds.Holdings.Clear();
            w.Purse.Money = FloatHand(sum, r);
            long need = sum - w.Purse.Cash;
            if (need <= 0)
            {
                continue;
            }
            cases++;
            long extra = r.Next(3) == 0 ? 0L : (long)(sum * r.NextDouble());
            BrokerMoney(m.State, on, need + BrokerWallet.Margin(sum) + extra, r);
            int from = w.Purse.Moves.Count;
            int bought = m.BuyLadder(sum, 0, false, true, true, out BondWhy why, worth);
            notAll += bought == 4 ? 0 : 1;
            twice += w.Purse.MostBacks(from) > 1 ? 1 : 0;
            under += w.Purse.Money < 0f ? 1 : 0;
        }
        Check(cases > 60 && notAll == 0 && twice == 0 && under == 0,
            "bonds: in the game's float money, a ladder bought with the money with the broker buys its four with one give-back at most from any order, from its least to its most ("
            + cases + " cases; not all bought " + notAll + ", twice " + twice + ", under nothing " + under + ")");
    }

    // A charge the market forces, alone or with another in a row (a listing's two yearly fees, a bot's monthly fee and its
    // share), with the cash kept as the game keeps it, from 1,000 to 20 billion: one the cash in hand and the money with the
    // broker cover with the margin never leaves the cash under nothing; charges in a row covered first (BrokerWallet.Cover)
    // make one give-back at most from any order, whether or not the money with the broker pays them all. The listing's two
    // fees and a bot's fee and share are covered so (their source).
    private static void InvestFloatCharges()
    {
        MarketEngine m = FloatMarket(773, out FloatWorld w, 3, 5);
        Stock[] on = { Liquid(m, MarketSize.Large), Liquid(m, MarketSize.Large, 1), Liquid(m, MarketSize.Mid) };
        var r = new Random(773);
        int n = 240, cases = 0, under = 0, twice = 0, shortTwice = 0;
        for (int i = 0; i < n; i++)
        {
            long sum = FloatSize(i, n);
            w.Purse.Money = FloatHand(sum, r);
            long need = sum - w.Purse.Cash;
            if (need <= 0)
            {
                continue;
            }
            cases++;
            long extra = r.Next(3) == 0 ? 0L : (long)(sum * r.NextDouble());
            BrokerMoney(m.State, on, need + BrokerWallet.Margin(sum) + extra, r);
            int from = w.Purse.Moves.Count;
            m.Wallet.Force(sum, MarketMoney.BotFee, "a charge");
            under += w.Purse.Money < 0f ? 1 : 0;
            twice += w.Purse.MostBacks(from) > 1 ? 1 : 0;
            long first = 1L + (long)((sum - 1) * r.NextDouble());
            w.Purse.Money = FloatHand(sum, r);
            BrokerMoney(m.State, on, Math.Max(0L, sum - w.Purse.Cash) + BrokerWallet.Margin(sum) + extra, r);
            from = w.Purse.Moves.Count;
            m.Wallet.Cover(sum);
            m.Wallet.Force(first, MarketMoney.BotFee, "two charges");
            m.Wallet.Force(sum - first, MarketMoney.BotShare, "two charges");
            under += w.Purse.Money < 0f ? 1 : 0;
            twice += w.Purse.MostBacks(from) > 1 ? 1 : 0;
            w.Purse.Money = FloatHand(sum, r);
            BrokerMoney(m.State, on, Math.Max(1L, (sum - w.Purse.Cash) / 2), r);
            from = w.Purse.Moves.Count;
            m.Wallet.Cover(sum);
            m.Wallet.Force(first, MarketMoney.BotFee, "two charges");
            m.Wallet.Force(sum - first, MarketMoney.BotShare, "two charges");
            shortTwice += w.Purse.MostBacks(from) > 1 ? 1 : 0;
        }
        string own = ModSource(@"Markets\MarketDirector.Own.cs"), bots = ModSource(@"Markets\MarketEngine.Bots.cs");
        int cover = own.IndexOf("_engine.Wallet.Cover(exchange + audit);", StringComparison.Ordinal), exchange = own.IndexOf("_engine.Wallet.Force(exchange, ListingMoney.Exchange", StringComparison.Ordinal);
        int coverBot = bots.IndexOf("Wallet.Cover(fee + share);", StringComparison.Ordinal), fee = bots.IndexOf("Wallet.Force(fee, MarketMoney.BotFee", StringComparison.Ordinal);
        Check(cases > 150 && under == 0 && twice == 0 && shortTwice == 0 && cover > 0 && exchange > cover && exchange - cover < 200 && coverBot > 0 && fee > coverBot && fee - coverBot < 300,
            "worked: in the game's float money, a charge the market forces that the money with the broker covers never takes the cash under nothing, and charges in a row (a listing's two fees, a bot's fee and share) give back once at most from any order ("
            + cases + " cases; under nothing " + under + ", twice " + twice + ", twice when short " + shortTwice + ")");
    }

    // A worked purchase is placed only on what its own day of money is parked from: the cash in hand and worked sales'
    // takings due, never another worked purchase's parked money (placed on it, it would stop at its first slice). With all
    // the cash parked by another it is refused for the cash at once; with a worked sale's takings due it is placed, and it
    // parks from them at its first slice. A ticket purchase still uses the parked money (InvestBrokerStaysYours).
    private static void InvestWorkedPlacement()
    {
        MarketEngine m = LogMarket(751, 0L, out LogWorld w, 3, 5);
        Stock s = Liquid(m, MarketSize.Large), t = Liquid(m, MarketSize.Large, 1), u = Liquid(m, MarketSize.Mid);
        long per = MarketTrading.MostPerOrder(s), qt = MarketTrading.MostPerOrder(t) * 2;
        PutHolding(m, u, MarketTrading.MostPerOrder(u) * 2);
        w.Purse.Money = MarketTrading.Value(per, MarketTrading.QuoteOf(m.State, s, m.State.Phase).Ask, true);
        Order first = m.PlaceWorked(s.Id, OrderSide.Buy, per * 3, m.Held, false, out _);
        RunTo(m, m.State.Day, MarketCatalogue.PhaseOpen + 10);
        long cash = w.Purse.Money, parked = first != null ? first.Parked : 0L;
        OrderWhy alone = m.WorkedWhy(t, OrderSide.Buy, qt, m.Held);
        Order refused = m.PlaceWorked(t.Id, OrderSide.Buy, qt, m.Held, false, out OrderWhy placedWhy);
        Order sale = m.PlaceWorked(u.Id, OrderSide.Sell, MarketTrading.MostPerOrder(u) * 2, m.Held, false, out _);
        RunTo(m, m.State.Day, MarketCatalogue.PhaseOpen + 16);
        long due = sale != null ? sale.Due : 0L;
        OrderWhy withTakings = m.WorkedWhy(t, OrderSide.Buy, qt, m.Held);
        Order second = m.PlaceWorked(t.Id, OrderSide.Buy, qt, m.Held, false, out _);
        RunTo(m, m.State.Day, MarketCatalogue.PhaseOpen + 20);
        Check(cash == 0 && parked > 0 && alone == OrderWhy.NoMoney && refused == null && placedWhy == OrderWhy.NoMoney && due > 0 && withTakings == OrderWhy.None
              && second != null && (second.Filled > 0 || second.Parked > 0),
            "worked: a worked purchase is placed only on what its own day of money parks from: refused for the cash while another has parked it all, placed on a worked sale's takings and parking from them ("
            + alone + ", " + withTakings + "; cash " + cash + ", parked " + parked + ", due " + due + ")");
    }
}
