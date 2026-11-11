using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul.Markets;

// The stock market's extras, part: which splits are decided and how every machine hears of them, the founder's own split,
// the index fund and block trades (StockxChecks.cs has the splits themselves).
internal static partial class Program
{
    // ---------------------------------------------------------------- who splits, and how every machine hears of it

    private static void StockxDecisionChecks()
    {
        MarketEngine m = StockxMarket(701, 1000000, out MarketWorld world, 70);
        MarketEngine off = StockxMarket(701, 1000000, out MarketWorld _, 70);
        off.ExtrasOn = false;
        MarketState st = m.State;
        Stock high = null, low = null, weak = null;
        foreach (Stock s in st.Stocks)
        {
            if (!s.Listed || s.Own != null || s.Def == null || s.Def.Holding || s.BidCents > 0 || TrustRules.Is(s))
            {
                continue;
            }
            if (high == null && SplitRules.LevelOf(s.Id) <= 24000)
            {
                high = s;
            }
            else if (low == null)
            {
                low = s;
            }
            else if (weak == null)
            {
                weak = s;
            }
        }
        Check(high != null && low != null && weak != null, "stock extras: the market has companies to try the boards' rules on");
        if (high == null || low == null || weak == null)
        {
            return;
        }
        int day = st.Day;
        double over = SplitRules.LevelOf(high.Id) / 100.0 * 1.2;
        foreach (MarketEngine e in new[] { m, off })
        {
            StockxPrice(e.State.Stock(high.Id), over);
            StockxPrice(e.State.Stock(low.Id), 0.40);
            e.State.Stock(low.Id).LowDays = ExtrasCatalogue.ReverseAfterDays;
            e.State.Stock(low.Id).DpsCents = 0;   // it pays no dividend
            e.State.Stock(low.Id).DeclaredCents = 0;
            StockxPrice(e.State.Stock(weak.Id), 0.40);
            e.State.Stock(weak.Id).LowDays = ExtrasCatalogue.ReverseAfterDays - 5;   // not long enough under a unit yet
        }
        Check(SplitRules.WantsSplit(high) && !SplitRules.WantsSplit(low) && over * 100 <= MarketCatalogue.SplitAboveCents,
            "stock extras: a company over its own board's level (" + SplitRules.LevelOf(high.Id) + " cents) and under the market's old one wants a split");
        int next = MarketTrading.NextTradingDay(day);
        RunTo(m, next, MarketCatalogue.PhaseResults);
        RunTo(off, next, MarketCatalogue.PhaseResults);
        bool stillHigh = high.Listed && high.BidCents <= 0, stillLow = low.Listed && low.BidCents <= 0;
        Check(stillHigh && stillLow, "stock extras: the two companies are still on the market with no bid for them, so the rules below are really tried on them");
        Check(high.SplitDay == MarketTrading.MarketDays(next, MarketCatalogue.SplitNoticeDays) && high.SplitRatio == 2,
            "stock extras: its split is announced at the market's next moment, two for one, with the market's notice (day " + high.SplitDay + ")");
        Check(world.Heard.Any(n => n.Kind == MarketNewsKind.Split && n.Stock == high.Id && n.A == 2 && n.B == high.SplitDay), "stock extras: ...and is market news");
        Check(low.SplitRatio == -50 && low.SplitDay > next, "stock extras: a sound company under a unit for " + ExtrasCatalogue.ReverseAfterDays
                                                              + " market days puts its shares together, 50 into 1 at 40 cents (ratio " + low.SplitRatio + ")");
        Check(world.Heard.Any(n => n.Kind == MarketNewsKind.Reverse && n.Stock == low.Id && n.A == 50), "stock extras: ...and is market news of its own kind");
        Check(weak.SplitDay == 0, "stock extras: a company not yet long enough under a unit puts nothing together");
        Stock offHigh = off.State.Stock(high.Id), offLow = off.State.Stock(low.Id);
        Check(offHigh.SplitDay == 0 && offLow.SplitDay == 0, "stock extras: with the option off no board's own level and no consolidation is decided: the market is as before");

        // Another machine playing the same tape with the option off takes the same splits: the tape says them.
        var twin = new MarketWorld(701);
        MarketEngine replay = NewMarket(701, 60, 1, 0, twin, m.Tape);
        replay.ExtrasOn = false;
        RunTo(replay, next, MarketCatalogue.PhaseResults);
        Stock rHigh = replay.State.Stock(high.Id), rLow = replay.State.Stock(low.Id);
        Check(rHigh.SplitDay == high.SplitDay && rHigh.SplitRatio == high.SplitRatio && rLow.SplitDay == low.SplitDay && rLow.SplitRatio == low.SplitRatio && high.SplitDay > 0 && low.SplitDay > 0,
            "stock extras: a machine that replays the tape with its own option off announces the same splits on the same days");

        // The consolidation takes effect: the price is back over a unit and the penny rule lets the company be.
        {
            long shares = low.Shares;
            int effect = low.SplitDay;
            RunTo(m, effect, MarketCatalogue.PhaseOpen + 1);
            Check(low.Listed && low.SplitDay == 0 && low.Shares == Math.Max(1L, shares / 50) && low.Last > MarketCatalogue.PennyCents * 5,
                "stock extras: on its day the shares are put together and trade far over a unit (" + low.Last + " cents, " + shares + " shares to " + low.Shares + ")");
            RunTo(m, effect, MarketCatalogue.PhaseClose);
            Check(low.Listed && low.LowDays == 0, "stock extras: ...so the penny rule counts no more days against it");
        }

        // A company nearer failing than a consolidation allows is left to the penny rule; one over the old level is the old rule's, option on or off.
        MarketEngine d = StockxMarket(702, 1000000, out MarketWorld _, 70);
        Stock sick = StockxCity(d, 4), dear = StockxCity(d, 5);
        StockxPrice(sick, 0.40);
        sick.LowDays = ExtrasCatalogue.ReverseAfterDays + 1;
        sick.Distress = 0.9;
        Check(!SplitRules.WantsReverse(sick), "stock extras: a company near failing puts nothing together");
        sick.Distress = 0.1;
        sick.DpsCents = 0;
        sick.DeclaredCents = 0;
        Check(SplitRules.WantsReverse(sick), "stock extras: ...the same company, sound and paying no dividend, does");
        sick.DpsCents = 3;
        Check(!SplitRules.WantsReverse(sick), "stock extras: a company still paying a dividend on a collapsed price puts nothing together: the penny rule takes it, as before");
        sick.DpsCents = 0;
        sick.DeclaredCents = 3;
        sick.ExDone = false;
        Check(!SplitRules.WantsReverse(sick), "stock extras: ...nor one with a dividend declared and not yet out of the price");
        sick.DeclaredCents = 0;
        sick.LowDays = MarketCatalogue.PennyDays - MarketCatalogue.SplitNoticeDays;
        Check(!SplitRules.WantsReverse(sick), "stock extras: ...nor one so long under a unit that the notice would end after the penny rule's day");
        sick.LowDays = ExtrasCatalogue.ReverseAfterDays + 1;
        sick.BidCents = 50;
        Check(!SplitRules.WantsReverse(sick), "stock extras: ...and never while a bid for it stands");
        StockxPrice(dear, MarketCatalogue.SplitAboveCents / 100.0 * 1.1);
        Check(!SplitRules.WantsSplit(dear), "stock extras: over the market's old level the old rule splits a company, not the board's own level a second time");
        d.ExtrasOn = false;
        int dDay = d.State.Day;
        RunTo(d, dDay, MarketCatalogue.PhaseClose);
        Check(dear.Listed && dear.BidCents <= 0 && dear.Last > MarketCatalogue.SplitAboveCents && dear.SplitDay > 0 && dear.SplitRatio >= 2,
            "stock extras: with the option off a company over the old level still splits by the old rule (announced for day " + dear.SplitDay + ", " + dear.SplitRatio + " for 1)");
    }

    // ---------------------------------------------------------------- the founder's own split

    private static void StockxFounderChecks()
    {
        var world = new OwnWorld(811);
        MarketEngine m = LListed(811, world, 2500, out Stock own);
        Check(own != null && own.Own != null, "stock extras: the player's company is listed for the founder's split");
        if (own == null)
        {
            return;
        }
        int level = m.State.Env.PriceLevel, last = own.Last;
        own.Last = 5000;
        Check(SplitRules.FounderWhy(own, 2, level) == SplitWhy.None && SplitRules.FounderWhy(own, -2, level) == SplitWhy.None, "stock extras: the founder who chairs the board may ask for a split or a consolidation");
        Check(SplitRules.FounderWhy(own, 7, level) == SplitWhy.Ratio && SplitRules.FounderWhy(own, -3, level) == SplitWhy.Ratio && SplitRules.FounderWhy(own, 1, level) == SplitWhy.Ratio,
            "stock extras: only the ratios a board may choose");
        own.Own.Board = false;
        Check(SplitRules.FounderWhy(own, 2, level) == SplitWhy.Board, "stock extras: a founder who has lost the chair cannot ask");
        own.Own.Board = true;
        own.BidCents = 100;
        Check(SplitRules.FounderWhy(own, 2, level) == SplitWhy.Bid, "stock extras: nor while a bid stands");
        own.BidCents = 0;
        own.SplitDay = 5;
        Check(SplitRules.FounderWhy(own, 2, level) == SplitWhy.Pending, "stock extras: nor while a split is announced");
        own.SplitDay = 0;
        own.Own.Firm = true;
        Check(SplitRules.FounderWhy(own, 2, level) == SplitWhy.Firm, "stock extras: a firm with investors is not split");
        own.Own.Firm = false;
        own.Last = 900;
        Check(SplitRules.FounderWhy(own, 2, level) == SplitWhy.TooLow && SplitRules.FounderWhy(own, 10, level) == SplitWhy.TooLow, "stock extras: no split takes a share under " + ExtrasCatalogue.FounderSplitLeastCents + " cents");
        own.Last = 10000;
        Check(SplitRules.FounderWhy(own, -2, level) == SplitWhy.TooHigh && SplitRules.FounderWhy(own, 2, level) == SplitWhy.None,
            "stock extras: no consolidation takes a share over " + ExtrasCatalogue.FounderReverseMostCents + " cents");
        own.Last = last;
        Check(SplitRules.FounderWhy(null, 2, level) == SplitWhy.NotMine && SplitRules.FounderWhy(StockxCity(m, 0), 2, level) == SplitWhy.NotMine, "stock extras: only a player's own company has a founder to ask");

        m.ExtrasOn = false;
        Check(m.AskSplit(own.Id, 2) == SplitWhy.Off, "stock extras: with the option off the founder's split is refused");
        m.ExtrasOn = true;
        m.State.Following = true;
        Check(m.AskSplit(own.Id, 2) == SplitWhy.Off, "stock extras: a guest in another player's city asks nothing of the market");
        m.State.Following = false;
        Check(m.AskSplit(StockxCity(m, 0).Id, 2) == SplitWhy.NotMine, "stock extras: nobody asks a split of a company that is not theirs");
        int ratio = SplitRules.FounderWhy(own, 2, level) == SplitWhy.None ? 2 : -2;
        Check(m.AskSplit(own.Id, ratio) == SplitWhy.None && own.SplitDay == 0, "stock extras: the founder's split goes on the tape and changes nothing yet");
        int next = MarketTrading.NextTradingDay(m.State.Day);
        RunTo(m, next, MarketCatalogue.PhaseResults);
        Check(own.SplitRatio == ratio && own.SplitDay == MarketTrading.MarketDays(next, MarketCatalogue.SplitNoticeDays), "stock extras: ...it is announced at the market's next moment with the market's notice");
        Check(world.Heard.Any(n => (n.Kind == MarketNewsKind.Split || n.Kind == MarketNewsKind.Reverse) && n.Stock == own.Id), "stock extras: ...as market news");

        // The split itself: the founder's part of the company and every holder's worth are what they were.
        own.DeclaredCents = 0;
        long founder = own.Own.Founder, shares = own.Shares;
        int stake = own.Own.StakeBp(own);
        double rps = own.Own.Rps, worth = own.Own.Founder * own.Mid, all = own.Shares * own.Mid;
        StockxSplit(m, own, 3);
        Check(own.Own.Founder == founder * 3 && own.Shares == shares * 3 && own.Own.StakeBp(own) == stake && Math.Abs(own.Own.Rps - rps / 3) < 1e-9 + Math.Abs(rps) * 1e-9,
            "stock extras: three for one, the founder holds three times the shares and the same " + stake + " bp of the company; the reserve a share is a third");
        Check(Math.Abs(own.Own.Founder * own.Mid - worth) < 0.005 && Math.Abs(own.Shares * own.Mid - all) < 0.05, "stock extras: ...and the founder's shares and the company are worth what they were");
        long cash = world.Purse.Money;
        founder = own.Own.Founder;
        shares = own.Shares;
        worth = own.Own.Founder * own.Mid + m.State.Extras.OddCents / 100.0;
        StockxSplit(m, own, -10);
        double after = own.Own.Founder * own.Mid + (world.Purse.Money - cash) + m.State.Extras.OddCents / 100.0;
        Check(own.Own.Founder == founder / 10 && Math.Abs(after - worth) < 0.011 && Math.Abs(own.Own.StakeBp(own) - stake) <= 1,
            "stock extras: ten into one, the founder's part of a share is paid for and their part of the company is the same to the basis point (" + worth.ToString("0.00") + " to "
            + after.ToString("0.00") + ")");
    }

    // ---------------------------------------------------------------- the index fund against its parts

    // What the fund's slices would cost bought as shares at each company's own offer, and fetch sold at its bid: by the index's own weights (MarketIndex).
    private static void StockxParts(MarketState st, double money, out double atOffer, out double atBid)
    {
        bool[] scratch = null;
        atOffer = atBid = 0.0;
        foreach (Stock k in st.Stocks)
        {
            if (!k.Listed)
            {
                continue;
            }
            double w = MarketIndex.WeightOf(st.Stocks, k, ref scratch);
            Quote q = MarketTrading.QuoteOf(st, k, st.Phase);
            double shares = money * w / k.Mid;
            atOffer += shares * q.Ask / 100.0;
            atBid += shares * q.Bid / 100.0;
        }
    }

    private static void StockxFundChecks()
    {
        int fair = 0, runs = 0;
        for (ulong seed = 901; seed <= 908; seed++)
        {
            MarketEngine m = StockxMarket(seed, 2000000000, out MarketWorld world, 5 + (int)(seed % 60));
            MarketState st = m.State;
            double nav = FundRules.Nav(st);
            Check(Math.Abs(nav - st.IndexLast / (double)ExtrasCatalogue.FundUnitDivisor * st.Extras.Track) <= 0.06,
                "stock extras: seed " + seed + ", a unit holds the index of this very step over " + ExtrasCatalogue.FundUnitDivisor + " (" + nav.ToString("0.00") + " cents against the index's " + st.IndexLast + ")");
            foreach (long units in new[] { 1L, 100L, 5000L, 60000L })
            {
                FundDeal buy = FundRules.Quote(st, units, true, st.Phase, world.Least, long.MaxValue, 0, true, true, false);
                FundDeal sell = FundRules.Quote(st, units, false, st.Phase, world.Least, long.MaxValue, units, true, true, false);
                StockxParts(st, units * nav / 100.0, out double atOffer, out double atBid);
                runs++;
                bool ok = buy.PriceCents >= buy.NavCents && sell.PriceCents <= sell.NavCents && buy.Value >= atOffer - 1.0 && sell.Value <= atBid + 1.0 && buy.Total > sell.Total;
                fair += ok ? 1 : 0;
                Check(ok, "stock extras: seed " + seed + ", " + units + " units cost " + buy.Value + " (their parts at the offer " + atOffer.ToString("0") + ") and fetch " + sell.Value + " (their parts at the bid "
                          + atBid.ToString("0") + "): never cheaper to buy nor dearer to sell than the parts");
            }
        }
        Check(runs == 32 && fair == 32, "stock extras: at every size and moment tried the fund is dealt no better than its parts, so the two cannot be played against each other");

        MarketEngine e = StockxMarket(920, 3000000000, out MarketWorld w, 20);
        MarketState s = e.State;
        long money = w.Purse.Money;
        FundDeal bought = e.FundTrade(20000, true);
        Check(bought.Why == FundWhy.None && s.Extras.Units == 20000 && s.Extras.Cost == bought.Total && w.Purse.Money == money - bought.Total,
            "stock extras: units bought are paid from the cash with commission, and their cost is what was paid (" + bought.Total + ")");
        Check(InvestRules.MarketWorth(s, s.Phase) >= FundRules.Worth(s, 20000, s.Phase) && FundRules.Worth(s, 20000, s.Phase) > 0 && FundRules.Worth(s, 20000, s.Phase) <= bought.Value,
            "stock extras: the units count in what the player holds on the market, at what they would fetch");
        FundDeal sold = e.FundTrade(20000, false);
        Check(sold.Why == FundWhy.None && s.Extras.Units == 0 && s.Extras.Cost == 0 && w.Purse.Money < money && s.Extras.Realised == sold.Total - bought.Total && s.Extras.Realised < 0,
            "stock extras: bought and sold again at once, the units lose the spread, both moves and two commissions (" + (money - w.Purse.Money) + " lost)");
        Check(e.FundTrade(1, false).Why == FundWhy.NoUnits && e.FundTrade(0, true).Why == FundWhy.Bad, "stock extras: no unit is sold that is not held, and none asked for is none dealt");

        // One deal against the same units in five: the later slices pay for the earlier ones.
        MarketEngine one = StockxMarket(921, 3000000000, out MarketWorld w1, 20), five = StockxMarket(921, 3000000000, out MarketWorld w5, 20);
        long start = w1.Purse.Money;
        one.FundTrade(25000, true);
        for (int i = 0; i < 5; i++)
        {
            five.FundTrade(5000, true);
        }
        Check(one.State.Extras.Units == 25000 && five.State.Extras.Units == 25000 && start - w5.Purse.Money >= start - w1.Purse.Money - 25000 / 100,
            "stock extras: the same units in five deals cost what one deal costs or more, to the rounding of a cent a unit (" + (start - w5.Purse.Money) + " against " + (start - w1.Purse.Money) + ")");

        // Pushing a company up before selling the fund: the fund never deals at a price the player's own push made better.
        int pumped = 0;
        for (ulong seed = 931; seed <= 936; seed++)
        {
            MarketEngine plain = StockxMarket(seed, 4000000000, out MarketWorld wp, 30), push = StockxMarket(seed, 4000000000, out MarketWorld wq, 30);
            plain.FundTrade(30000, true);
            push.FundTrade(30000, true);
            foreach (MarketEngine x in new[] { plain, push })
            {
                foreach (Stock k in x.State.Stocks)
                {
                    k.Impact = 0.0;   // the units were bought long ago: no recent trading left
                }
            }
            for (int i = 0; i < 6; i++)
            {
                Stock big = StockxCity(push, i, MarketSize.Large);
                if (big != null)
                {
                    StockxOrder(push, big, OrderSide.Buy, OrderType.Market, MarketTrading.MostPerOrder(big), 0, 0);
                }
            }
            if (seed % 2 == 0)
            {
                foreach (Stock k in push.State.Stocks)
                {
                    k.Impact = 0.0;   // an hour on: the market has forgotten the purchases' size, but their lasting move is still in the price
                }
            }
            long a = wp.Purse.Money, b = wq.Purse.Money;
            FundDeal plainSale = plain.FundTrade(30000, false), pushSale = push.FundTrade(30000, false);
            bool held = FundRules.Nav(push.State) > FundRules.Nav(plain.State) && pushSale.Total <= plainSale.Total + 1;
            pumped += held ? 1 : 0;
            Check(held, "stock extras: seed " + seed + ", six large purchases lift what a unit holds, yet the units sell for no more than without them (" + pushSale.Total + " against " + plainSale.Total + ")");
            Check(wq.Purse.Money - b <= wp.Purse.Money - a + 1, "stock extras: seed " + seed + ", ...in the cash too");
        }
        Check(pumped == 6, "stock extras: on six seeds a player who pushes companies up before selling the fund gains nothing by it");

        // What stands in the way.
        MarketEngine r = StockxMarket(940, 5000000000, out MarketWorld wr, 20);
        double navR = FundRules.Nav(r.State);
        FundRules.IndexNow(r.State, out double whole);
        long most = FundRules.Most(r.State, true, 0, navR, whole);
        FundDeal over = r.FundQuote(most + 1, true);
        Check(most > 0 && (over.Why == FundWhy.HoldingLimit || over.Why == FundWhy.TooLarge) && r.FundQuote(Math.Max(1, most), true).Why == FundWhy.None,
            "stock extras: one deal is held to " + most + " units: no slice more of a company than one order, and at most " + ExtrasCatalogue.FundHoldingBp + " bp of the market in one holder's units");
        Check(FundRules.Most(r.State, true, long.MaxValue / 4, navR, whole) == 0, "stock extras: a holder at the most buys no more units");
        wr.Purse.Money = 10;
        Check(r.FundQuote(100, true).Why == FundWhy.NoMoney && r.FundTrade(100, true).Why == FundWhy.NoMoney && r.State.Extras.Units == 0 && wr.Purse.Money == 10,
            "stock extras: units the cash does not pay for are not bought, and nothing is booked");
        wr.Purse.Money = 5000000000;
        r.FundTrade(100, true);
        r.ExtrasOn = false;
        Check(r.FundQuote(10, true).Why == FundWhy.Off && r.FundQuote(10, false).Why == FundWhy.None, "stock extras: with the option off no unit is bought, and units held can still be sold");
        r.ExtrasOn = true;
        Check(FundRules.Quote(r.State, 10, true, r.State.Phase, 5, long.MaxValue, 0, true, true, true).Why == FundWhy.Held
              && FundRules.Quote(r.State, 10, true, r.State.Phase, 5, long.MaxValue, 0, true, false, false).Why == FundWhy.Closed,
            "stock extras: no unit is dealt after a load until the market is back where it was, nor while it is shut");
        RunTo(r, r.State.Day, MarketCatalogue.PhaseClose);
        Check(r.FundQuote(10, true).Why == FundWhy.Closed, "stock extras: ...as after the close");
    }

    private static void StockxFundPlayChecks()
    {
        // The fee: a market day's share of it at each close, once.
        MarketEngine m = StockxMarket(951, 1000000000, out MarketWorld world, 20);
        MarketState st = m.State;
        m.FundTrade(10000, true);
        double year = MarketCatalogue.TradingDaysPerYear(60), fee = ExtrasCatalogue.FundFeeBp / 10000.0 / year;
        int day = st.Day, listed = st.Stocks.Count(k => k.Listed);
        double opened = st.Extras.Track;
        RunTo(m, day, MarketCatalogue.PhaseClose);
        bool nobodyLeft = st.Stocks.Count(k => k.Listed) == listed;
        Check(opened > 0.999 && opened <= 1.0 && (!nobodyLeft || Math.Abs(st.Extras.Track - opened * (1.0 - fee)) < 1e-12), "stock extras: a close takes a market day's share of the yearly " + ExtrasCatalogue.FundFeeBp + " bp out of the fund's worth ("
                                                                           + st.Extras.Track.ToString("0.000000000") + ")");
        double track = st.Extras.Track;
        FundRules.Close(st, day, year);
        Check(st.Extras.Track == track, "stock extras: a close counted twice takes no second fee");
        double product = track;
        int closes = 1;
        for (int i = 0; i < 12; i++)
        {
            int before = st.Stocks.Count(k => k.Listed);
            double was = st.Extras.Track;
            int next = MarketTrading.NextTradingDay(st.Day);
            RunTo(m, next, MarketCatalogue.PhaseClose);
            if (st.Stocks.Count(k => k.Listed) == before && st.Stocks.All(k => !k.Listed || k.GoneDay != next))
            {
                Check(Math.Abs(st.Extras.Track - was * (1.0 - fee)) < 1e-12, "stock extras: day " + next + ", the fund's worth against the index falls by the day's fee and nothing else");
            }
            product *= 1.0 - fee;
            closes++;
        }
        int leftBy = listed - st.Stocks.Count(k => k.Listed);
        Check(leftBy > 0 ? st.Extras.Track <= 1.0 + 1e-9 + 0.05 * leftBy : Math.Abs(st.Extras.Track - product) < 1e-9,
            "stock extras: over " + closes + " closes the fund stands exactly where the days' fees put it when no company left (" + st.Extras.Track.ToString("0.000000000") + " against " + product.ToString("0.000000000")
            + "), and never over its index but for a company that paid over its price (" + leftBy + " left)");
        Check(Math.Pow(1.0 - fee, year) > 0.9969 && Math.Pow(1.0 - fee, year) < 0.9971, "stock extras: a year of closes takes " + ExtrasCatalogue.FundFeeBp + " bp, no more");

        // A dividend: owed to the units held at the record day's close, paid on the company's own payment day, and not to units bought after.
        MarketEngine d = StockxMarket(952, 1000000000, out MarketWorld dw, 20);
        MarketState ds = d.State;
        d.FundTrade(50000, true);
        long paidOut = 0;
        d.FundPaid = money => paidOut += money;
        Stock payer = StockxCity(d, 0, MarketSize.Large) ?? StockxCity(d, 0);
        foreach (Stock k in ds.Stocks)
        {
            if (k.RecordDay >= ds.Day)
            {
                k.RecordDay = 0;   // only the one below pays in these days
                k.DeclaredCents = 0;
            }
        }
        int payDay = MarketTrading.MarketDays(ds.Day, 2);
        payer.DeclaredCents = 80;
        payer.ExDone = false;
        payer.RecordDay = ds.Day;
        payer.PayDay = payDay;
        bool[] scratch = null;
        RunTo(d, ds.Day, MarketCatalogue.PhaseClose - 1);
        double expected = 50000 * FundRules.Nav(ds) / 100.0 * MarketIndex.WeightOf(ds.Stocks, payer, ref scratch) / payer.Mid * 80.0;   // cents: the shares behind the units times the dividend
        RunTo(d, ds.Day, MarketCatalogue.PhaseClose);
        long owed = ds.Extras.Dues.Where(x => x.PayDay == payDay).Sum(x => x.Cents);
        Check(owed > 0 && Math.Abs(owed - expected) <= expected * 0.02 + 2, "stock extras: the units held at a record day's close are owed the dividend of the shares behind them (" + owed + " cents against "
                                                                           + expected.ToString("0") + ")");
        RunTo(d, MarketTrading.NextTradingDay(ds.Day), MarketCatalogue.PhaseOpen + 5);
        d.FundTrade(50000, true);
        long cash = dw.Purse.Money;
        Check(ds.Extras.Dues.Where(x => x.PayDay == payDay).Sum(x => x.Cents) == owed, "stock extras: units bought after the record day are owed nothing of it");
        RunTo(d, payDay, MarketCatalogue.PhaseClose - 1);
        long due = ds.Extras.Dues.Where(x => x.PayDay <= payDay).Sum(x => x.Cents) + ds.Extras.OddCents;   // with what any other company's record day added meanwhile
        cash = dw.Purse.Money;
        long paidBefore = paidOut;
        RunTo(d, payDay, MarketCatalogue.PhaseClose);
        Check(due >= owed && paidOut - paidBefore == due / 100 && dw.Purse.Money == cash + due / 100 && ds.Extras.OddCents == due % 100 && !ds.Extras.Dues.Any(x => x.PayDay <= payDay) && ds.Extras.Paid == paidOut,
            "stock extras: the dividend is passed on, on its payment day, into the cash to the whole unit of money, the cents under it kept for the next (" + (paidOut - paidBefore) + " paid of " + due + " cents)");

        // A company that fails: the index's divisor keeps the index where it was, but the fund held it and is paid what its holders are.
        MarketEngine f = StockxMarket(953, 1000000, out MarketWorld _, 20);
        MarketState fs = f.State;
        Stock gone = StockxCity(f, 1, MarketSize.Large) ?? StockxCity(f, 1);
        double weight = MarketIndex.WeightOf(fs.Stocks, gone, ref scratch), navBefore = FundRules.Nav(fs);
        f.Delist(gone.Id, GoneReason.Failed, 0);
        Check(weight > 0 && Math.Abs(FundRules.Nav(fs) - navBefore * (1.0 - weight)) < navBefore * 1e-6,
            "stock extras: a company that fails paying nothing takes its weight (" + (weight * 100).ToString("0.00") + "%) out of what a unit holds, though the index stays where it was");
        Stock half = StockxCity(f, 2);
        weight = MarketIndex.WeightOf(fs.Stocks, half, ref scratch);
        navBefore = FundRules.Nav(fs);
        int paid = half.MidCents / 2;
        double share = 1.0 - paid / (half.Mid * 100.0);
        f.Delist(half.Id, GoneReason.Failed, paid);
        Check(Math.Abs(FundRules.Nav(fs) - navBefore * (1.0 - weight * share)) < navBefore * 1e-6, "stock extras: one that pays half its price takes half its weight out");
        Stock fresh = StockxCity(f, 3);
        navBefore = FundRules.Nav(fs);
        f.Delist(fresh.Id, GoneReason.TakenOver, fresh.MidCents);
        Check(Math.Abs(FundRules.Nav(fs) - navBefore) < navBefore * 1e-4, "stock extras: one taken over at its price takes nothing out: the fund is paid its price");
    }

    // ---------------------------------------------------------------- block trades

    private static void StockxBlockChecks()
    {
        int lost = 0, cheaper = 0;
        for (ulong seed = 1001; seed <= 1008; seed++)
        {
            MarketEngine m = StockxMarket(seed, 20000000000, out MarketWorld world, 10 + (int)(seed % 50));
            MarketState st = m.State;
            Stock s = StockxCity(m, (int)(seed % 5), seed % 2 == 0 ? MarketSize.Large : MarketSize.Mid) ?? StockxCity(m, 0);
            long order = MarketTrading.MostPerOrder(s), qty = order * (2 + (long)(seed % 4));
            Quote now = MarketTrading.QuoteOf(st, s, st.Phase);
            BlockQuote q = m.BlockQuoteOf(s.Id, true, qty);
            Check(q.Why == BlockWhy.None && q.Cents >= now.Ask && q.ImpactBp > 0 && q.Total == q.Value + q.Fee, "stock extras: seed " + seed + ", a block of " + qty + " (an order's most is " + order
                + ") is quoted over the offer, moved by its size (" + q.Cents + " cents against " + now.Ask + ")");
            long money = world.Purse.Money;
            double mid = s.Mid;
            BlockQuote bought = m.Block(s.Id, true, qty);
            Check(bought.Why == BlockWhy.None && st.Held(s.Id) == qty && world.Purse.Money == money - bought.Total && s.Mid > mid && s.Impact >= qty,
                "stock extras: seed " + seed + ", the block is paid at its quote, held, and on the price as one order of its size");
            Check(st.Held(s.Id) <= MarketTrading.MostHeld(s) && st.Trades.Any(t => t.StockId == s.Id && t.Kind == TradeKind.Bought && t.Qty == qty), "stock extras: seed " + seed + ", ...within the most one holder may have, and in the history");
            BlockQuote sold = m.Block(s.Id, false, qty);
            bool loses = sold.Why == BlockWhy.None && st.Held(s.Id) == 0 && world.Purse.Money < money && sold.Cents < bought.Cents;
            lost += loses ? 1 : 0;
            Check(loses, "stock extras: seed " + seed + ", bought and sold again the block loses money: " + (money - world.Purse.Money) + " (" + bought.Cents + " cents paid, " + sold.Cents + " fetched)");

            // The same shares as orders placed at the same moment cost at least as much.
            MarketEngine a = StockxMarket(seed, 20000000000, out MarketWorld wa, 10 + (int)(seed % 50)), b = StockxMarket(seed, 20000000000, out MarketWorld wb, 10 + (int)(seed % 50));
            Stock sa = a.State.Stock(s.Id), sb = b.State.Stock(s.Id);
            long before = wa.Purse.Money;
            a.Block(sa.Id, true, qty);
            long left = qty;
            while (left > 0)
            {
                long part = Math.Min(left, MarketTrading.MostPerOrder(sb));
                Order o = StockxOrder(b, sb, OrderSide.Buy, OrderType.Market, part, 0, 0);
                if (o == null || o.Filled != part)
                {
                    break;
                }
                left -= part;
            }
            bool noCheaper = left == 0 && b.State.Held(s.Id) == qty && before - wb.Purse.Money >= before - wa.Purse.Money;
            cheaper += noCheaper ? 1 : 0;
            Check(noCheaper, "stock extras: seed " + seed + ", the block's shares as " + (qty + order - 1) / order + " orders cost " + (before - wb.Purse.Money) + ", the block " + (before - wa.Purse.Money)
                             + ": splitting it saves nothing");
        }
        Check(lost == 8 && cheaper == 8, "stock extras: on eight seeds a block bought and sold loses, and its shares as small orders cost at least as much");

        // What stands in a block's way.
        MarketEngine r = StockxMarket(1020, 20000000000, out MarketWorld wr, 20);
        MarketState rs = r.State;
        Stock k = StockxCity(r, 0, MarketSize.Large);
        long least = BlockRules.Least(k);
        Check(r.BlockQuoteOf(k.Id, true, least - 1).Why == BlockWhy.TooSmall && r.BlockQuoteOf(k.Id, true, least).Why == BlockWhy.None, "stock extras: a block is more than one order may be; less is the order ticket's");
        long most = BlockRules.Most(rs, k, true);
        Check(most == MarketTrading.MostHeld(k) && r.BlockQuoteOf(k.Id, true, most + 1).Why == BlockWhy.HoldingLimit && r.Block(k.Id, true, most + 1).Why == BlockWhy.HoldingLimit && rs.Held(k.Id) == 0,
            "stock extras: a block is no way round the most one holder may have (" + MarketTrading.MostHeldBp(k) + " bp): one share over is refused and nothing is bought");
        Check(r.BlockQuoteOf(k.Id, false, least).Why == BlockWhy.NoShares, "stock extras: no block is sold of shares not held");
        k.BidCents = k.Last;
        Check(r.BlockQuoteOf(k.Id, true, least).Why == BlockWhy.Bid, "stock extras: no block is dealt in a company under a bid");
        k.BidCents = 0;
        int listedDay = k.ListedDay;
        k.ListedDay = rs.Day - 3;
        Check(r.BlockQuoteOf(k.Id, true, least).Why == BlockWhy.Young, "stock extras: nor in one that has not traded " + ExtrasCatalogue.BlockListedDays + " market days");
        k.ListedDay = listedDay;
        k.Own = new OwnListing { Owner = 7, Founder = k.Shares / 2 };
        Check(r.BlockQuoteOf(k.Id, true, least).Why == BlockWhy.Founders && r.BlockQuoteOf(k.Id, false, least).Why == BlockWhy.Founders && r.Block(k.Id, true, least).Why == BlockWhy.Founders,
            "stock extras: nobody takes or sells a block of a player's own company: its founder cannot push it with blocks, and deals by the founder's rules");
        k.Own = null;
        r.ExtrasOn = false;
        Check(r.BlockQuoteOf(k.Id, true, least).Why == BlockWhy.Off, "stock extras: with the option off no block is dealt");
        r.ExtrasOn = true;
        wr.Purse.Money = 100;
        Check(r.BlockQuoteOf(k.Id, true, least).Why == BlockWhy.NoMoney && r.Block(k.Id, true, least).Why == BlockWhy.NoMoney && rs.Held(k.Id) == 0 && wr.Purse.Money == 100,
            "stock extras: a block the cash does not pay for is refused before anything is booked");
        wr.Purse.Money = 20000000000;
        Check(BlockRules.Quote(rs, k, true, least, long.MaxValue, 5, rs.Phase, true, false, false).Why == BlockWhy.Closed && BlockRules.Quote(rs, k, true, least, long.MaxValue, 5, rs.Phase, true, true, true).Why
              == BlockWhy.Held && BlockRules.Quote(rs, null, true, least, long.MaxValue, 5, rs.Phase, true, true, false).Why == BlockWhy.NotListed,
            "stock extras: nor while the market is shut, held after a load, or of a company that is not listed");

        // A rival's holding has only its free float, whatever the option lets a holder have of other companies; and a holding is told at every 5%.
        int holdingId = MarketCatalogue.All.First(d => d.Holding).Id;
        r.ListHolding(holdingId, 60000000);
        Stock holding = rs.Stock(holdingId);
        holding.ListedDay = rs.Day - 60;
        MarketTrading.HoldTo(3);
        try
        {
            Check(holding != null && BlockRules.Most(rs, holding, true) == holding.Shares * MarketCatalogue.HoldingFloatBp / 10000L, "stock extras: of a rival's holding a block takes at most its free float, with every limit lifted ("
                                                                                                                               + MarketCatalogue.HoldingFloatBp + " bp)");
            Stock big = StockxCity(r, 2, MarketSize.Large);
            int heard = wr.Heard.Count(n => n.Kind == MarketNewsKind.Block);
            long small = BlockRules.Least(big);
            r.Block(big.Id, true, small);
            Check(BlockRules.StakeBp(rs.Held(big.Id), big.Shares) < ExtrasCatalogue.DiscloseStepBp && wr.Heard.Count(n => n.Kind == MarketNewsKind.Block) == heard, "stock extras: a holding under 5% is nobody's business");
            long toSix = big.Shares * 600 / 10000 - rs.Held(big.Id);
            BlockQuote six = r.Block(big.Id, true, toSix);
            Check(six.Why == BlockWhy.None && six.StakeBp >= 500 && wr.Heard.Count(n => n.Kind == MarketNewsKind.Block && n.Stock == big.Id && n.A >= 500 && n.B == 1 && n.C == 0) == 1,
                "stock extras: a holding that passes 5% is told to the market once (" + six.StakeBp + " bp)");
            r.Block(big.Id, true, small);
            Check(wr.Heard.Count(n => n.Kind == MarketNewsKind.Block && n.Stock == big.Id) == 1, "stock extras: ...and not again inside the same step");
            long toEleven = big.Shares * 1100 / 10000 - rs.Held(big.Id);
            r.Block(big.Id, true, toEleven);
            Check(wr.Heard.Count(n => n.Kind == MarketNewsKind.Block && n.Stock == big.Id && n.B == 1) == 2 && rs.Extras.ToldBp(big.Id, 0) == 1000, "stock extras: ...again at 10%");
            r.Block(big.Id, false, rs.Held(big.Id) - big.Shares * 300 / 10000);
            Check(wr.Heard.Any(n => n.Kind == MarketNewsKind.Block && n.Stock == big.Id && n.B == -1) && rs.Extras.ToldBp(big.Id, 0) == 0, "stock extras: ...and when it is sold down under 5%");
        }
        finally
        {
            MarketTrading.HoldTo(0);
        }
        Check(BlockRules.StepOf(499) == 0 && BlockRules.StepOf(500) == 500 && BlockRules.StepOf(1249) == 1000 && BlockRules.StakeBp(50, 1000) == 500 && BlockRules.StakeBp(-5, 1000) == 0,
            "stock extras: the steps a holding is told at are whole 5%s of the company");
    }
}
