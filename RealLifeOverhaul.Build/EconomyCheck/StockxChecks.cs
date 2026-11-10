using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using RealLifeOverhaul.Markets;

// The stock market's extras (Scripts/Markets/Extras): splits by each board's own level, shares put together under a unit and
// the founder's own split, with nobody's wealth, cost or part of a company changed and a save and load on the day; the index
// fund against its parts, its fee, its distributions and a company that fails; block trades by the market's own depth rule,
// what one holder may have and what is told; a bank of the player's own on the market from its own books; the rival tycoons'
// units, blocks and arms; the save's line; the sources, the texts and the cost. Every check drives the shipped rules.
internal static partial class Program
{
    private static void Stockx()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        StockxLevelChecks();
        StockxForwardChecks();
        StockxReverseChecks();
        StockxDecisionChecks();
        StockxFounderChecks();
        StockxFundChecks();
        StockxFundPlayChecks();
        StockxBlockChecks();
        StockxBankChecks();
        StockxFixChecks();
        StockxRivalChecks();
        StockxSaveChecks();
        StockxSourceChecks();
        StockxTextChecks();
        StockxCostChecks();
        Console.WriteLine("  stock market extras: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- helpers

    private static readonly MethodInfo StockxSplitNow = typeof(MarketEngine).GetMethod("SplitNow", BindingFlags.NonPublic | BindingFlags.Instance);

    // The engine's own split at an open, as OpenCompany calls it.
    private static void StockxSplit(MarketEngine m, Stock s, int ratio)
    {
        s.SplitRatio = ratio;
        s.SplitDay = m.State.Day;
        StockxSplitNow.Invoke(m, new object[] { s, m.State.Day });
    }

    // A market in session on a day, with money: the city's companies as the game opens them.
    // The engine's own split at an open for a split that came by the tape (its day and ratio already set by the announcement).
    private static void StockxSplitTaped(MarketEngine m, Stock s)
    {
        StockxSplitNow.Invoke(m, new object[] { s, m.State.Day });
    }

    private static MarketEngine StockxMarket(ulong seed, long money, out MarketWorld world, int step = 10)
    {
        world = new MarketWorld(seed);
        world.Purse.Money = money;
        MarketEngine m = NewMarket(seed, 60, 1, 0, world);
        RunTo(m, TradingOnOrAfter(8), MarketCatalogue.PhaseOpen + step);
        return m;
    }

    // The i-th city company that has traded long enough for anything: none of the rivals' holdings, no bid, no split to come.
    private static Stock StockxCity(MarketEngine m, int nth, MarketSize? size = null)
    {
        int seen = 0;
        foreach (Stock s in m.State.Stocks)
        {
            if (s.Listed && s.Own == null && s.Def != null && !s.Def.Holding && s.BidCents <= 0 && s.SplitDay <= 0 && !TrustRules.Is(s) && (size == null || s.Size == size.Value)
                && seen++ == nth)
            {
                return s;
            }
        }
        return null;
    }

    private static void StockxPrice(Stock s, double money)
    {
        s.Mis += Math.Log(money / s.Mid);
        s.Sent = s.Mis;
        s.Last = s.MidCents;
    }

    private static double StockxWealth(MarketEngine m, MarketWorld world)
    {
        double worth = world.Purse.Money + m.State.Account.Cash + m.State.Extras.OddCents / 100.0;
        foreach (Position p in m.State.Positions)
        {
            Stock k = m.State.Stock(p.StockId);
            worth += k != null ? p.Shares * k.Mid : 0.0;
        }
        return worth;
    }

    private static Order StockxOrder(MarketEngine m, Stock s, OrderSide side, OrderType type, long qty, int limit, int stop, bool closing = false)
    {
        return m.Place(s.Id, side, type, OrderLife.UntilCancelled, qty, limit, stop, true, false, false, out OrderWhy _, closing);
    }

    // ---------------------------------------------------------------- each board's own level

    private static void StockxLevelChecks()
    {
        var levels = new HashSet<int>();
        bool within = true, steps = true;
        for (int id = 1; id <= 200; id++)
        {
            int level = SplitRules.LevelOf(id);
            levels.Add(level);
            within &= level >= ExtrasCatalogue.SplitLevelLeastCents && level <= MarketCatalogue.SplitAboveCents;
            steps &= (level - ExtrasCatalogue.SplitLevelLeastCents) % ExtrasCatalogue.SplitLevelStepCents == 0;
            within &= level == SplitRules.LevelOf(id);
        }
        Check(within, "stock extras: every board's own level is between " + ExtrasCatalogue.SplitLevelLeastCents + " cents and the market's old " + MarketCatalogue.SplitAboveCents + ", the same each time asked");
        Check(steps && levels.Count >= 8, "stock extras: the levels differ from company to company, in steps: " + levels.Count + " different");
        Check(ExtrasCatalogue.SplitLevelLeastCents + ExtrasCatalogue.SplitLevelSteps * ExtrasCatalogue.SplitLevelStepCents == MarketCatalogue.SplitAboveCents,
            "stock extras: the highest board's level is the market's old level, so the old rule still stands behind every board");
        Check(SplitRules.ReverseRatio(40, 1000) == 50 && SplitRules.ReverseRatio(99, 1000) == 20 && SplitRules.ReverseRatio(100, 1000) == 10 && SplitRules.ReverseRatio(250, 1000) == 5
              && SplitRules.ReverseRatio(600, 1000) == 2 && SplitRules.ReverseRatio(1, 1000) == 50,
            "stock extras: shares are put together by the smallest ratio that takes the price to the target (40c by 50, 99c by 20, 1.00 by 10, 2.50 by 5, 6.00 by 2)");
        Check(SplitRules.ReverseRatio(100, 2000) == 20, "stock extras: the target is at today's prices: at twice the price level a share at 1.00 goes by 20");
        Check(SplitRules.Known(2) && SplitRules.Known(10) && SplitRules.Known(-2) && SplitRules.Known(-50) && !SplitRules.Known(1) && !SplitRules.Known(0) && !SplitRules.Known(-1) && !SplitRules.Known(11)
              && !SplitRules.Known(-51), "stock extras: a ratio the tape or a save may hold is 2 to 10 new for one, or 2 to 50 old into one");
        Check(ExtrasCatalogue.ReverseAfterDays < MarketCatalogue.PennyDays - MarketCatalogue.SplitNoticeDays - 1,
            "stock extras: a sound company puts its shares together before the penny rule would take it off the market, notice included");
        Check(ExtrasCatalogue.FounderReverseMostCents < ExtrasCatalogue.SplitLevelLeastCents, "stock extras: a founder's consolidation ends under every board's level, so it is never split again at once");
    }

    // ---------------------------------------------------------------- a split: nobody gains or loses

    private static void StockxForwardChecks()
    {
        int exact = 0, runs = 0, orders = 0, charts = 0, saves = 0;
        double worst = 0.0;
        for (ulong seed = 501; seed <= 512; seed++)
        {
            MarketEngine m = StockxMarket(seed, 50000000, out MarketWorld world);
            MarketState st = m.State;
            Stock s = StockxCity(m, (int)(seed % 7), MarketSize.Large) ?? StockxCity(m, 0);
            int ratio = seed % 3 == 0 ? 5 : seed % 3 == 1 ? 2 : 3;
            long qty = 137 + (long)(seed * 31 % 900);
            StockxOrder(m, s, OrderSide.Buy, OrderType.Market, qty, 0, 0);
            Position p = st.Position(s.Id);
            if (p == null)
            {
                Check(false, "stock extras: seed " + seed + " could buy shares to split");
                continue;
            }
            Quote q = MarketTrading.QuoteOf(st, s, st.Phase);
            Order limit = StockxOrder(m, s, OrderSide.Buy, OrderType.Limit, 101, Math.Max(1, q.Bid * 9 / 10 + 1), 0);
            Order stop = StockxOrder(m, s, OrderSide.Sell, OrderType.Stop, Math.Min(qty, 55), 0, Math.Max(1, q.Bid * 8 / 10 + 1), true);
            st.Charts.AddAlert(new PriceAlert { StockId = s.Id, Kind = AlertKind.Above, Cents = s.Last * 2 + 1, Drawing = -1 });
            st.Charts.AddDrawing(new ChartDrawing { StockId = s.Id, Kind = ChartMark.Level, FromCents = s.Last, ToCents = s.Last });
            st.Rules.Add(new RuleBot { Id = 900, Kind = RuleKind.Grid, StockId = s.Id, Level = s.Last, PerTrade = 10, Shares = 30, Cost = 1 });
            st.Extras.Rivals[0].Stakes.Add(new RivalStake { Stock = s.Id, Shares = 1000, Cost = 5, Day = st.Day });
            st.Extras.Units = 40;
            double navBefore = FundRules.Nav(st);
            long sharesBefore = p.Shares, costBefore = p.Cost, issued = s.Shares, limitQty = limit != null ? limit.Qty : 0;
            int limitBefore = limit != null ? limit.LimitCents : 0, stopBefore = stop != null ? stop.StopCents : 0, alertBefore = st.Charts.Alerts[st.Charts.Alerts.Count - 1].Cents;
            int lastBefore = s.Last;
            double before = StockxWealth(m, world), valueBefore = p.Shares * s.Mid;

            // A save on the day, before the split: read back, it splits to the same holding and wealth.
            s.SplitRatio = ratio;
            s.SplitDay = st.Day;
            MarketState loaded = MarketStore.Read(MarketStore.Write(st), out int _);
            ExtrasState extras = ExtrasStore.Read(ExtrasStore.Write(st.Extras), out int _);
            bool kept = loaded != null && extras != null && loaded.Stock(s.Id).SplitRatio == ratio && loaded.Stock(s.Id).SplitDay == st.Day && extras.Units == 40;

            StockxSplit(m, s, ratio);
            double after = StockxWealth(m, world), valueAfter = p.Shares * s.Mid;
            runs++;
            worst = Math.Max(worst, Math.Abs(after - before));
            bool same = Math.Abs(after - before) < 0.005 && Math.Abs(valueAfter - valueBefore) < 0.005 && p.Shares == sharesBefore * ratio && p.Cost == costBefore && s.Shares == issued * ratio;
            exact += same ? 1 : 0;
            Check(same, "stock extras: seed " + seed + ", a " + ratio + " for 1 split changes no wealth and no cost (" + before.ToString("0.00") + " to " + after.ToString("0.00") + ", " + sharesBefore
                        + " shares to " + p.Shares + ", cost " + costBefore + " to " + p.Cost + ")");
            Check(Math.Abs(FundRules.Nav(st) - navBefore) < 1e-6 * navBefore && st.Extras.Units == 40, "stock extras: seed " + seed + ", the fund's unit holds the same after the split, and the units are the same");
            bool ordersOk = limit == null || (limit.Qty == limitQty * ratio && limit.LimitCents <= limitBefore / ratio && limit.LimitCents >= limitBefore / ratio - 1);
            ordersOk &= stop == null || !stop.IsOpen || (stop.StopCents <= stopBefore / ratio && stop.StopCents >= stopBefore / ratio - 1);
            orders += ordersOk ? 1 : 0;
            Check(ordersOk, "stock extras: seed " + seed + ", an open order's shares follow the split and its price is never easier to reach than before");
            PriceAlert alert = st.Charts.Alerts[st.Charts.Alerts.Count - 1];
            ChartDrawing line = st.Charts.Drawings[st.Charts.Drawings.Count - 1];
            RuleBot rule = st.Rules[st.Rules.Count - 1];
            bool chartOk = Math.Abs(alert.Cents - alertBefore / (double)ratio) <= 1.0 && Math.Abs(line.FromCents - lastBefore / (double)ratio) <= 1.0 && rule.Shares == 30 * ratio && rule.PerTrade == 10 * ratio
                           && Math.Abs(rule.Level - lastBefore / (double)ratio) <= 1.0 && st.Extras.Rivals[0].Stakes[0].Shares == 1000 * ratio;
            charts += chartOk ? 1 : 0;
            Check(chartOk, "stock extras: seed " + seed + ", the player's alert, drawn line and rule and a rival's block follow the split");
            Check(st.Extras.LastSplit(s.Id, out SplitNote note) && note.New == ratio && note.Old == 1 && note.Day == st.Day && !note.Reverse, "stock extras: seed " + seed + ", the split is noted for the chart");

            // The loaded save splits alike.
            if (kept)
            {
                loaded.Extras.CopyFrom(extras);
                var again = new MarketEngine(loaded, new MarketTape(seed), new MarketWorld(seed));
                Stock t = loaded.Stock(s.Id);
                Position lp = loaded.Position(s.Id);
                StockxSplit(again, t, ratio);
                bool alike = lp != null && lp.Shares == p.Shares && lp.Cost == p.Cost && t.Shares == s.Shares && Math.Abs(t.Mid - s.Mid) < 1e-9 * s.Mid;
                saves += alike ? 1 : 0;
                Check(alike, "stock extras: seed " + seed + ", a save on the day of the split, loaded, splits to the same holding, cost and price");
            }
            else
            {
                Check(false, "stock extras: seed " + seed + ", the save's lines keep a split to come and the units");
            }
        }
        Check(runs == 12 && exact == 12 && orders == 12 && charts == 12 && saves == 12,
            "stock extras: twelve seeded holdings with open orders split with nothing gained or lost (the largest difference " + worst.ToString("0.0000") + ")");

        // A dividend declared and not yet out of the price: a split the board decided on this build (on the tape) waits for the morning after its record day.
        MarketEngine w = StockxMarket(540, 1000000, out MarketWorld _);
        Stock d = StockxCity(w, 2);
        d.DeclaredCents = 35;
        d.ExDone = false;
        d.RecordDay = w.State.Day + 2;
        long was = d.Shares;
        w.Event(MarketInputs.EventSplit, d.Id, 2);
        RunTo(w, w.State.Day, w.State.Phase + 1);
        Check(d.SplitDay > 0 && d.SplitRatio == 2 && w.State.Extras.SplitWaiting(d.Id, d.SplitDay), "stock extras: a split from the tape is announced, and marked as one that may wait for a dividend");
        StockxSplitTaped(w, d);
        Check(d.Shares == was && d.SplitDay > 0 && d.SplitRatio == 2 && d.DeclaredCents == 35, "stock extras: a split decided on this build waits while a dividend is declared and has not left the price (35 cents is not halved to 18)");
        d.ExDone = true;
        StockxSplitTaped(w, d);
        Check(d.Shares == was * 2 && d.SplitDay == 0 && !w.State.Extras.SplitWaiting(d.Id, d.SplitDay) && w.State.Extras.SplitWaits.Count == 0,
            "stock extras: ...and takes effect once the dividend has left the price, and waits no more");
        // The old rule's own split is not on the tape, so a day already played is played as it was: it takes effect on its day, dividend or not (a replay of an old tape).
        Stock olds = StockxCity(w, 4);
        olds.DeclaredCents = 35;
        olds.ExDone = false;
        olds.RecordDay = w.State.Day + 2;
        long oldShares = olds.Shares;
        StockxSplit(w, olds, 2);
        Check(olds.Shares == oldShares * 2 && olds.SplitDay == 0 && w.State.Extras.SplitWaits.Count == 0, "stock extras: the old rule's split takes effect on its day whatever dividend is declared: a day already played does not change");
        Stock u = StockxCity(w, 3);
        long uShares = u.Shares;
        StockxSplit(w, u, 1);
        StockxSplit(w, u, 77);
        Check(u.Shares == uShares && u.SplitDay == 0, "stock extras: a ratio the market does not know splits nothing and is cleared");
    }

    private static void StockxReverseChecks()
    {
        int exact = 0;
        double worst = 0.0;
        for (ulong seed = 601; seed <= 616; seed++)
        {
            MarketEngine m = StockxMarket(seed, 80000000, out MarketWorld world);
            MarketState st = m.State;
            Stock s = StockxCity(m, (int)(seed % 9));
            int n = ExtrasCatalogue.ReverseRatios[(int)(seed % (ulong)ExtrasCatalogue.ReverseRatios.Length)];
            long qty = 1003 + (long)(seed * 97 % 4000);   // never a round number of the ratio
            StockxOrder(m, s, OrderSide.Buy, OrderType.Market, qty, 0, 0);
            Position p = st.Position(s.Id);
            if (p == null)
            {
                Check(false, "stock extras: seed " + seed + " could buy shares to put together");
                continue;
            }
            Quote q = MarketTrading.QuoteOf(st, s, st.Phase);
            Order limit = StockxOrder(m, s, OrderSide.Buy, OrderType.Limit, 100 * n + 3, Math.Max(1, q.Bid * 9 / 10), 0);
            Order tiny = StockxOrder(m, s, OrderSide.Buy, OrderType.Limit, Math.Max(1, n - 1), Math.Max(1, q.Bid * 9 / 10), 0);
            st.Charts.AddAlert(new PriceAlert { StockId = s.Id, Kind = AlertKind.Above, Cents = s.Last * 2, Drawing = -1 });
            st.Extras.Rivals[1].Stakes.Add(new RivalStake { Stock = s.Id, Shares = 1000 * n, Cost = 5, Day = st.Day });
            st.Derivs.Contracts.Add(new DerivContract { Id = 77, Kind = ContractKind.Call, StockId = s.Id, Qty = 100 * n, Price = 5000000, Premium = 30000, End = ContractEnd.Open });
            long held = p.Shares, cost = p.Cost, realised = p.Realised, issued = s.Shares;
            int limitCents = limit != null ? limit.LimitCents : 0, dps = s.DpsCents, alertBefore = st.Charts.Alerts[st.Charts.Alerts.Count - 1].Cents;
            long cash = world.Purse.Money;
            double mid = s.Mid, before = StockxWealth(m, world), navBefore = FundRules.Nav(st);
            StockxSplit(m, s, -n);
            double after = StockxWealth(m, world);
            long paid = world.Purse.Money - cash, gained = p.Realised - realised;
            worst = Math.Max(worst, Math.Abs(after - before));
            bool whole = p.Shares == held / n && s.Shares == Math.Max(1L, issued / n) && Math.Abs(s.Mid - mid * n) < 1e-9 * mid * n;
            bool money = Math.Abs(after - before) < 0.011 && paid >= 0 && st.Extras.OddCents >= 0 && st.Extras.OddCents < 100;
            // The cost that went with the part of a share paid for is taken as a gain or a loss; the rest is the cost of what is kept.
            bool basis = p.Cost + (paid - gained) == cost && p.Cost <= cost && p.Cost >= cost * (held / n * n) / held - 1;
            exact += whole && money && basis ? 1 : 0;
            Check(whole, "stock extras: seed " + seed + ", " + n + " into 1: " + held + " shares become " + p.Shares + " at " + n + " times the price");
            Check(money, "stock extras: seed " + seed + ", the part of a share left over (" + held % n + ") is paid for at the price before, to the cent (" + before.ToString("0.00") + " to "
                         + after.ToString("0.00") + ", " + paid + " paid, " + st.Extras.OddCents + " cents kept)");
            Check(basis, "stock extras: seed " + seed + ", the cost splits between what is kept and what was paid for, nothing lost (" + cost + " = " + p.Cost + " + " + (paid - gained) + ")");
            Check(Math.Abs(FundRules.Nav(st) - navBefore) < 1e-6 * navBefore, "stock extras: seed " + seed + ", the fund's unit holds the same after the shares are put together");
            Check(limit == null || (limit.IsOpen && limit.Qty == (100L * n + 3) / n && limit.LimitCents == limitCents * n),
                "stock extras: seed " + seed + ", an open order keeps its whole shares at exactly " + n + " times its price");
            Check(tiny == null || (!tiny.IsOpen && tiny.State == OrderState.Cancelled), "stock extras: seed " + seed + ", an order for less than one new share is cancelled");
            DerivContract c = st.Derivs.Contracts[st.Derivs.Contracts.Count - 1];
            Check(c.Qty == 100 && c.Price == 5000000L * n && c.Premium == 30000L * n, "stock extras: seed " + seed + ", an option covers a " + n + "th of the shares at " + n + " times the strike: worth the same");
            Check(s.DpsCents == dps * n && st.Charts.Alerts[st.Charts.Alerts.Count - 1].Cents == alertBefore * n && st.Extras.Rivals[1].Stakes[0].Shares == 1000,
                "stock extras: seed " + seed + ", the dividend a share, the player's alert and a rival's block follow");
            Check(st.Extras.LastSplit(s.Id, out SplitNote note) && note.Reverse && note.Old == n && note.New == 1, "stock extras: seed " + seed + ", the consolidation is noted for the chart");
            bool candles = s.Daily.Count == 0 || s.Daily[s.Daily.Count - 1].C % n == 0;
            Check(candles, "stock extras: seed " + seed + ", the chart's candles are at the new share count, so no crash is drawn");
        }
        Check(exact == 16, "stock extras: sixteen seeded holdings put together with nothing gained or lost beyond a cent's rounding (the largest difference " + worst.ToString("0.0000") + ")");

        // A short in a margin account buys its part of a share back; a bot's holding is paid its part into its own money.
        MarketEngine g = StockxMarket(650, 5000000, out MarketWorld gw);
        Stock k = StockxCity(g, 1, MarketSize.Large) ?? StockxCity(g, 1);
        g.State.Account.Margin = true;
        g.State.Account.Cash = 2000000;
        g.State.Positions.Add(new Position { StockId = k.Id, Shares = -1007, Cost = 1007 * (long)k.Last / 100, OpenedDay = g.State.Day });
        double shortBefore = StockxWealth(g, gw);
        StockxSplit(g, k, -10);
        Position sp = g.State.Position(k.Id);
        Check(sp != null && sp.Shares == -100 && Math.Abs(StockxWealth(g, gw) - shortBefore) < 0.011 && g.State.Extras.OddCents >= 0 && g.State.Extras.OddCents < 100,
            "stock extras: a short of 1,007 shares becomes 100, the 7 bought back at the price before from the margin account, to the cent");
    }
}
