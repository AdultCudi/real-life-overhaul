using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Markets;

// The shares a trading bot trades, its payouts and the dividend news (Scripts/Markets: MarketBots, MarketEngine.Bots.cs,
// MarketStore): its whole market, the companies picked for it or the player's own holdings, a holding outside them sold;
// the player's own company traded only by its founder's rules and within a share of its day's volume, never another
// player's; a share of new profit paid daily, weekly or monthly with losses carried forward and no money made or lost by
// it; a line of the market news for each dividend paid to the player or a bot of theirs, one notification for several;
// the save's line with all of it, a line from before it, and the city's line without the player's own news.
internal static partial class Program
{
    private static void MarketBotsPlus()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        BotScopeRules();
        BotScopeRuns();
        BotOwnCompany();
        BotPayoutRules();
        BotPayoutRuns();
        BotDividendNews();
        BotPlusStore();
        BotPlusSameEverywhere();
        BotPlusWords();
        BotPlusCost();
        Console.WriteLine("  trading bots' shares, payouts and dividend news: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // ---------------------------------------------------------------- the shares a bot trades

    private static void BotScopeRules()
    {
        MarketEngine m = BotMarket(601, 5000000, out BotWorld w);
        SessionDay(m, 3, 10);
        Bot b = m.Hire(1, 500000, 20, 10, 35, out _);
        Check(b.Scope == BotScope.Market && b.Picks.Count == 0 && b.PayPct == 0 && b.PayEvery == BotPayEvery.Monthly, "bots plus: a bot is hired on its whole market, with nothing picked and no payout");
        Stock big = Liquid(m, MarketSize.Large), small = m.State.Stocks.First(s => s.Listed && s.Size == MarketSize.Small && s.BidCents <= 0 && m.State.Day - s.ListedDay > 30 && s.Last > 1000);
        Check(MarketBots.Allowed(m.State, b, big, m.State.Day) == MarketBots.Buyable(m.State, b.Def, big, m.State.Day)
              && !MarketBots.Allowed(m.State, b, small, m.State.Day), "bots plus: on its whole market a bot buys what its firm buys (no small company for the trend firm)");
        Check(m.SetScope(1, BotScope.Picked, out BotWhy why) && m.PickFor(1, small.Id, true, out why) && MarketBots.Allowed(m.State, b, small, m.State.Day)
              && !MarketBots.Allowed(m.State, b, big, m.State.Day), "bots plus: with picks it buys only what was picked, a small company too");
        Check(!m.PickFor(1, small.Id, true, out why) && why == BotWhy.None, "bots plus: a company is picked once");
        foreach (Stock s in m.State.Stocks.Where(s => s.Listed && s.Id != small.Id).Take(MarketBots.PicksMost - 1))
        {
            m.PickFor(1, s.Id, true, out _);
        }
        Stock more = m.State.Stocks.First(s => s.Listed && !b.Picks.Contains(s.Id));
        Check(b.Picks.Count == MarketBots.PicksMost && !m.PickFor(1, more.Id, true, out why) && why == BotWhy.Picks, "bots plus: no more than " + MarketBots.PicksMost + " companies are picked for a bot");
        Check(m.PickFor(1, small.Id, false, out _) && !b.Picks.Contains(small.Id) && !MarketBots.Allowed(m.State, b, small, m.State.Day), "bots plus: a company taken off its picks is not bought");
        b.Picks.Clear();
        small.BidCents = small.Last * 2;
        b.Picks.Add(small.Id);
        Check(!MarketBots.Allowed(m.State, b, small, m.State.Day), "bots plus: a picked company under a bid is not bought");
        small.BidCents = 0;
        int listed = small.ListedDay;
        small.ListedDay = m.State.Day - MarketBots.ListedDaysLeast + 1;
        Check(!MarketBots.Allowed(m.State, b, small, m.State.Day), "bots plus: nor one listed fewer than " + MarketBots.ListedDaysLeast + " days");
        small.ListedDay = listed;
        // Another player's company is never a bot's: not picked, not in its scope.
        Stock other = m.State.Stocks.First(s => s.Listed && s.Id != small.Id && s.Size == MarketSize.Large);
        m.State.Me = 0x77UL;
        other.Own = new OwnListing { Owner = 0x78UL, Founder = other.Shares / 2 };
        Check(!m.PickFor(1, other.Id, true, out why) && why == BotWhy.NotListed && !MarketBots.InScope(m.State, b, other), "bots plus: another player's company cannot be picked");
        b.Picks.Add(other.Id);
        Check(!MarketBots.InScope(m.State, b, other) && !MarketBots.Allowed(m.State, b, other, m.State.Day) && MarketBots.OwnRoom(m.State, other, true, m.State.Day, 60) == 0,
            "bots plus: and is never traded, even if it were among the picks");
        other.Own = null;
        b.Picks.Clear();
        // The player's holdings.
        m.SetScope(1, BotScope.Held, out _);
        Check(!MarketBots.InScope(m.State, b, big), "bots plus: on the player's holdings, a company they hold none of is not in its scope");
        Trade(m, big, OrderSide.Buy, 50);
        Check(MarketBots.InScope(m.State, b, big) && MarketBots.Allowed(m.State, b, big, m.State.Day), "bots plus: and one they hold is");
        Check(!m.SetScope(9, BotScope.Picked, out why) && why == BotWhy.NotHired, "bots plus: a bot not hired has no settings to change");
        m.HoldAfterLoad();
        m.Tape.Reach(m.State.Day + 3, MarketCatalogue.PhaseOpen, true);
        m.HoldAfterLoad();
        Check(m.Held && !m.SetScope(1, BotScope.Market, out why) && why == BotWhy.Held && !m.PickFor(1, big.Id, true, out _) && !m.SetPayout(1, 50, BotPayEvery.Daily, out _),
            "bots plus: nothing about what it trades or pays changes while the market replays after a load");
    }

    // Over runs: a bot on its picks trades only them, one on the player's holdings only those, and a holding outside its scope is sold.
    private static void BotScopeRuns()
    {
        int trades = 0, outside = 0, heldTrades = 0, heldOutside = 0;
        for (ulong seed = 611; seed <= 614; seed++)
        {
            MarketEngine m = BotMarket(seed, 20000000, out _);
            SessionDay(m, 3, 10);
            List<int> picks = m.State.Stocks.Where(s => s.Listed && s.Own == null && s.BidCents <= 0).OrderByDescending(s => s.Adv).Take(MarketBots.PicksMost).Select(s => s.Id).ToList();
            m.Hire(3, 2000000, 25, 10, 50, out _);
            foreach (int id in picks)
            {
                m.PickFor(3, id, true, out _);
            }
            m.SetScope(3, BotScope.Picked, out _);
            List<int> mine = m.State.Stocks.Where(s => s.Listed && s.Own == null && s.BidCents <= 0 && s.Size != MarketSize.Small && !picks.Contains(s.Id)).OrderByDescending(s => s.Adv).Take(5).Select(s => s.Id).ToList();
            foreach (int id in mine)
            {
                Trade(m, m.State.Stock(id), OrderSide.Buy, 20);
            }
            m.Hire(1, 2000000, 20, 0, 50, out _);
            m.SetScope(1, BotScope.Held, out _);
            m.Hire(4, 2000000, 20, 0, 50, out _);
            m.SetScope(4, BotScope.Held, out _);
            RunTo(m, 70);
            foreach (Trade t in m.State.Trades.Where(t => t.Kind == TradeKind.Bought && t.Bot > 0))
            {
                if (t.Bot == 3)
                {
                    trades++;
                    outside += picks.Contains(t.StockId) ? 0 : 1;
                }
                else
                {
                    heldTrades++;
                    heldOutside += mine.Contains(t.StockId) ? 0 : 1;
                }
            }
        }
        Check(trades > 0 && outside == 0, "bots plus: a bot on its picks buys only the companies picked (" + trades + " purchases, " + outside + " outside)");
        Check(heldOutside == 0, "bots plus: a bot on the player's holdings buys only what they hold (" + heldTrades + " purchases, " + heldOutside + " outside)");
        Console.WriteLine("  bots plus: " + trades + " purchases on picks and " + heldTrades + " on the player's holdings over four seeds");
        // A holding outside its scope is sold at the next session.
        MarketEngine e = BotMarket(615, 5000000, out _);
        int day = SessionDay(e, 3, 10);
        Bot b = e.Hire(1, 600000, 20, 10, 35, out _);
        Stock x = Liquid(e, MarketSize.Large);
        b.Holdings.Add(new BotHolding { StockId = x.Id, Shares = 100, Cost = 100L * x.Last / 100, OpenedDay = day });
        e.SetScope(1, BotScope.Picked, out _);
        RunTo(e, day);
        Check(b.Pending.Any(o => o.StockId == x.Id && !o.Buy), "bots plus: at the close a holding outside its picks is to be sold");
        RunTo(e, MarketTrading.NextTradingDay(day), MarketCatalogue.PhaseOpen + 2);
        Check(b.Holding(x.Id) == null, "bots plus: and is sold at the next session's first step");
    }

    // ---------------------------------------------------------------- the player's own company

    private static void BotOwnCompany()
    {
        var world = new OwnWorld(621);
        MarketEngine m = LListed(621, world, 2500, out Stock own);
        Check(own != null, "bots plus: the player's company is listed for the checks");
        if (own == null)
        {
            return;
        }
        var notes = new List<BotNote>();
        m.BotTold = n => notes.Add(n);
        int day = MarketTrading.MarketDays(m.State.Day, MarketBots.ListedDaysLeast + 2);
        RunTo(m, day, MarketCatalogue.PhaseOpen);
        for (int tries = 0; tries < 40 && ListingRules.Closed(own, m.State.Day, LYear); tries++)
        {
            day = MarketTrading.NextTradingDay(m.State.Day);
            RunTo(m, day, MarketCatalogue.PhaseOpen);
        }
        day = m.State.Day;
        Check(!ListingRules.Closed(own, day, LYear) && own.Listed, "bots plus: a day the founder may deal on, for the checks (day " + day + ")");
        Bot b = m.Hire(1, 5000000, 100, 0, 90, out BotWhy why);
        Check(b != null, "bots plus: a bot is hired beside the player's company (" + why + ")");
        if (b == null)
        {
            return;
        }
        Check(!MarketBots.InScope(m.State, b, own) && !MarketBots.Allowed(m.State, b, own, day), "bots plus: on its whole market a bot never trades the player's own company");
        m.SetScope(1, BotScope.Held, out _);
        Check(own.Own.Founder > 0 && MarketBots.InScope(m.State, b, own), "bots plus: on the player's holdings it may trade their own company, whose founder they are");
        Check(m.SetScope(1, BotScope.Picked, out _) && m.PickFor(1, own.Id, true, out why) && MarketBots.Allowed(m.State, b, own, day), "bots plus: and the player's own company can be picked (" + why + ")");
        Stock plain = Liquid(m, MarketSize.Large);
        Check(MarketBots.OwnRoom(m.State, plain, true, day, LYear) == long.MaxValue, "bots plus: a company not a player's has no limit of the founder's");
        long cap = (long)Math.Floor(own.Adv * MarketBots.OwnVolumePct / 100.0);
        long room = MarketBots.OwnRoom(m.State, own, true, day, LYear);
        Check(cap > 0 && room <= cap, "bots plus: a day's trading in the player's company is at most " + MarketBots.OwnVolumePct + "% of its ordinary day's volume (" + room + " of " + cap + ")");
        // The closed time, another machine's view and the founder's limits.
        int pending = own.PendingQuarter;
        own.PendingQuarter = own.Quarter;
        Check(MarketBots.OwnRoom(m.State, own, true, day, LYear) == 0 && MarketBots.OwnRoom(m.State, own, false, day, LYear) == 0, "bots plus: nothing in the closed time before its results");
        own.PendingQuarter = pending;
        m.State.Following = true;
        Check(MarketBots.OwnRoom(m.State, own, true, day, LYear) == 0 && !MarketBots.InScope(m.State, b, own), "bots plus: nothing while the market follows another player's city");
        m.State.Following = false;
        OwnListing o = own.Own;
        int dealYear = o.DealYear, bought = o.BoughtBp;
        o.DealYear = MarketCatalogue.YearOf(day, LYear);
        o.BoughtBp = ListingCatalogue.FounderDealMostBpYear;
        Check(MarketBots.OwnRoom(m.State, own, true, day, LYear) == 0, "bots plus: nothing once the founder's own deals have used the year's limit");
        o.DealYear = dealYear;
        o.BoughtBp = bought;
        long founder = o.Founder;
        o.Founder = own.Shares * ListingCatalogue.FounderMostBp / 10000L;
        Check(MarketBots.OwnRoom(m.State, own, true, day, LYear) == 0 && MarketBots.OwnRoom(m.State, own, false, day, LYear) >= 0, "bots plus: no purchase past the most the founder may hold");
        o.Founder = founder;
        // A purchase through the market: within the day's share, counted, and no sale back within the short-swing time.
        b.Pending.Clear();
        b.Pending.Add(new BotOrder { StockId = own.Id, Buy = true, Qty = 10000000 });
        double before = own.Last;
        RunTo(m, day, MarketCatalogue.PhaseOpen + 2);
        BotHolding h = b.Holding(own.Id);
        long got = h != null ? h.Shares : 0;
        Check(got > 0 && got <= cap && b.OwnQty == got && b.OwnBuyDay == day && b.OwnBoughtBp == ListingRules.Bp(got, own.Shares),
            "bots plus: a bot's purchase of the player's company is held to the day's share and counted (" + got + " of at most " + cap + ")");
        Check(MarketBots.OwnRoom(m.State, own, true, day, LYear) <= cap - got, "bots plus: and what it bought counts against the rest of the day");
        Check(m.State.Trades.Where(t => t.Bot == 1 && t.StockId == own.Id && t.Kind == TradeKind.Bought).Sum(t => t.Qty) == got
              && m.State.Trades.Where(t => t.Bot == 1 && t.StockId == own.Id && t.Kind == TradeKind.Bought).All(t => t.Value < 0 && t.Fee > 0), "bots plus: bought as the player's own trade, with their cash and the commission");
        Console.WriteLine("  bots plus: a bot bought " + got + " shares of the player's company (a day's share " + cap + "); its price " + (own.Last / before * 100.0 - 100.0).ToString("0.00") + "% after the step");
        int window = MarketBots.OwnSwingDays(LYear);
        Check(MarketBots.OwnRoom(m.State, own, false, day, LYear) == 0, "bots plus: no sale of the player's company within " + window + " days of a purchase (the founder's short-swing time)");
        b.OwnBuyDay = day - window + 1;
        Check(MarketBots.OwnRoom(m.State, own, false, day, LYear) == 0, "bots plus: not on the last day of it either");
        b.OwnBuyDay = day - window;
        b.OwnDay = 0;
        Check(MarketBots.OwnRoom(m.State, own, false, day, LYear) > 0, "bots plus: after it, a sale may be made");
        b.OwnBuyDay = day;
        b.OwnDay = day;
        // Letting go: refused while it holds them; sold up by the rules, then let go.
        Check(!m.Dismiss(1, out why) && why == BotWhy.OwnShares && MarketBots.Hired(m.State, 1) != null, "bots plus: a bot holding the player's company's shares is not let go");
        m.SellUp(1, out _);
        RunTo(m, day, MarketCatalogue.PhaseOpen + 4);
        Check(b.Status == BotStatus.SellingUp && b.Holding(own.Id) != null && b.Holding(own.Id).Shares == got, "bots plus: sold up within the short-swing time, it keeps them and sells nothing yet");
        b.OwnBuyDay = day - window;
        b.OwnDay = 0;
        RunTo(m, day, MarketCatalogue.PhaseOpen + 5);
        Check(b.Holding(own.Id) == null && b.Status == BotStatus.Stopped && b.OwnSellDay == day, "bots plus: once it may, it sells them and stops");
        Check(m.Dismiss(1, out why) && MarketBots.Hired(m.State, 1) == null && o.Founder == founder, "bots plus: then it is let go, and the founder's shares are as they were");
        _ = notes;
    }

    // ---------------------------------------------------------------- payouts

    private static void BotPayoutRules()
    {
        // Their times: every day, a week's last market day, a month's last market day.
        bool daily = true, weekly = true, monthly = true;
        int weeks = 0, months = 0;
        for (int d = 1; d < 240; d++)
        {
            if (!MarketCatalogue.TradingDay(d))
            {
                continue;
            }
            int next = MarketTrading.NextTradingDay(d);
            daily &= MarketBots.PayDue(BotPayEvery.Daily, d, 60);
            bool w = MarketBots.PayDue(BotPayEvery.Weekly, d, 60), mo = MarketBots.PayDue(BotPayEvery.Monthly, d, 60);
            weekly &= w == (MarketCatalogue.WeekOf(next) != MarketCatalogue.WeekOf(d)) && (!w || MarketCatalogue.Weekday(d) == 4);
            monthly &= mo == (MarketCatalogue.MonthOf(next, 60) != MarketCatalogue.MonthOf(d, 60));
            weeks += w ? 1 : 0;
            months += mo ? 1 : 0;
        }
        Check(daily && weekly && monthly && weeks >= 30 && months >= 40 && months < weeks + 20, "bots plus: a payout falls due each market day, at a week's last market day, or at a month's (" + weeks + " weeks, " + months + " months)");
        // The arithmetic: a share of new profit only, losses carried forward, what its money in shares cannot pay waits.
        var b = new Bot { Firm = 1, Cash = 10000, Capital = 10000, Base = 10000, Peak = 10000, HighWater = 10000, MonthWorth = 10000, PayPct = 50 };
        b.Realised = 2000;
        long paid = MarketBots.Payout(b);
        Check(paid == 1000 && b.Cash == 9000 && b.PaidOut == 1000 && b.MonthPaid == 1000 && b.PayMark == 2000 && b.PayOwed == 0, "bots plus: half of a profit of 2,000 is 1,000 paid out, the rest kept");
        Check(b.Base == 9000 && b.Peak == 9000 && b.HighWater == 9000 && b.MonthWorth == 9000, "bots plus: and what its limits, its high-water mark and its month are reckoned from come down with it: a payout is never a loss");
        b.Realised = 1200;   // a loss of 800
        Check(MarketBots.Payout(b) == 0 && b.PayMark == 2000 && MarketBots.LossesToMakeGood(b) == 800, "bots plus: after a loss nothing is paid, and the loss is carried forward");
        b.Realised = 1900;
        Check(MarketBots.Payout(b) == 0 && MarketBots.LossesToMakeGood(b) == 100, "bots plus: nor while part of it is still to be made good");
        b.Realised = 2600;
        Check(MarketBots.Payout(b) == 300 && b.PayMark == 2600 && MarketBots.LossesToMakeGood(b) == 0, "bots plus: once it is made good, half of the profit past it is paid (300)");
        b.Fees += 400;
        b.Dividends += 1000;
        Check(MarketBots.Payout(b) == 300 && b.PayMark == 3200, "bots plus: its profit counts its dividends and takes off its fees");
        b.Cash = 100;
        b.Realised += 1000;
        Check(MarketBots.Payout(b) == 100 && b.PayOwed == 400 && b.Cash == 0, "bots plus: what its money not in shares cannot pay waits");
        b.Cash = 1000;
        Check(MarketBots.Payout(b) == 400 && b.PayOwed == 0 && b.Cash == 600, "bots plus: and is paid at the next time");
        b.Cash = -50;
        b.Realised += 1000;
        Check(MarketBots.Payout(b) == 0 && b.PayOwed == 500 && b.Cash == -50, "bots plus: nothing is paid from money under nothing");
        var keep = new Bot { Firm = 1, Cash = 5000, PayPct = 0, Realised = 3000 };
        Check(MarketBots.Payout(keep) == 0 && keep.PayMark == 3000 && keep.Cash == 5000, "bots plus: a bot that pays nothing keeps its profit, and it is never paid later");
        keep.PayPct = 100;
        keep.Realised = 3500;
        Check(MarketBots.Payout(keep) == 500, "bots plus: all of it, set later, pays only what is new since");
        Check(MarketBots.Choice(MarketBots.PayPcts, 55) == 50 && MarketBots.Choice(MarketBots.PayPcts, 150) == 100 && MarketBots.Choice(MarketBots.PayPcts, -1) == 0
              && MarketBots.PayPcts.First() == 0 && MarketBots.PayPcts.Last() == 100, "bots plus: a payout's share is held to its steps, none to all of it");
    }

    // Over runs: what is paid is the share of new profit, no money is made or lost by paying, statements say what was paid.
    private static void BotPayoutRuns()
    {
        bool conserved = true, share = true, told = true, statements = true;
        long paidAll = 0;
        int weeklyNotes = 0, dailyNotes = 0;
        for (ulong seed = 631; seed <= 636; seed++)
        {
            MarketEngine m = BotMarket(seed, 20000000, out BotWorld w);
            SessionDay(m, 3, 10);
            long money0 = w.Purse.Money;
            BotPayEvery[] every = { BotPayEvery.Weekly, BotPayEvery.Daily, BotPayEvery.Monthly, BotPayEvery.Weekly };
            int[] pct = { 50, 100, 30, 70 };
            for (int i = 0; i < MarketBots.All.Length; i++)
            {
                BotFirm f = MarketBots.All[i];
                m.Hire(f.Id, 1500000, f.Size, f.Stop, f.Loss, out _);
                m.SetPayout(f.Id, pct[i], every[i], out _);
            }
            RunTo(m, 3 + 120);
            long botMoney = 0;
            foreach (Bot b in m.State.Bots)
            {
                botMoney += b.Cash + b.PaidOut - b.Capital;
                long due = b.PayMark * b.PayPct / 100;
                share &= b.PaidOut + b.PayOwed <= due && b.PaidOut + b.PayOwed >= due - 130 && b.PayMark >= 0;
                long fromNotes = w.Notes.Where(n => n.Kind == BotNoteKind.Statement && n.Firm == b.Firm).Sum(n => n.D) + b.MonthPaid;
                statements &= fromNotes == b.PaidOut;
                long weekly = w.Notes.Where(n => n.Kind == BotNoteKind.Paid && n.Firm == b.Firm).Sum(n => n.A);
                told &= b.PayEvery == BotPayEvery.Weekly ? weekly <= b.PaidOut : weekly == 0;   // (a weekly bot's month-end payout is in its statement)
                paidAll += b.PaidOut;
                weeklyNotes += b.PayEvery == BotPayEvery.Weekly ? w.Notes.Count(n => n.Kind == BotNoteKind.Paid && n.Firm == b.Firm) : 0;
                dailyNotes += b.PayEvery == BotPayEvery.Daily ? w.Notes.Count(n => n.Kind == BotNoteKind.Paid && n.Firm == b.Firm) : 0;
            }
            conserved &= w.Purse.Money - money0 == botMoney;
        }
        Check(paidAll > 0 && share, "bots plus: what a bot pays and owes is its share of its new profit, to the unit and its rounding (" + paidAll + " paid in all)");
        Check(conserved, "bots plus: a payout moves no money: the player's cash is what the bots' money and what they paid out did");
        Check(statements, "bots plus: the months' statements say, together, every unit a bot paid out");
        Check(told && weeklyNotes > 0 && dailyNotes == 0, "bots plus: a weekly payout is told as it is paid (" + weeklyNotes + " notes), a daily one in the month's statement");
    }

    // ---------------------------------------------------------------- dividend news

    private static void BotDividendNews()
    {
        // The tally: the one dividend, or how many and how much in all.
        var t = new DividendTally();
        t.Add(5, 0, 0);
        Check(t.Count == 0, "bots plus: a dividend of nothing is not told");
        t.Add(7, 2, 120);
        Check(t.Count == 1 && t.Stock == 7 && t.Firm == 2 && t.Total == 120, "bots plus: one dividend is told with its company and its bot");
        t.Add(9, 0, 30);
        t.Add(11, 3, 50);
        Check(t.Count == 3 && t.Total == 200 && t.Stock == 7, "bots plus: several that land at once are told as one, with how many and how much in all");
        t.Clear();
        Check(t.Count == 0 && t.Total == 0 && t.Stock == 0, "bots plus: and once told, nothing is left");
        int cases = 0, right = 0, botRight = 0, extra = 0;
        bool kept = true, notCity = true;
        for (ulong seed = 641; seed <= 650; seed++)
        {
            MarketEngine m = BotMarket(seed, 1000000000, out BotWorld w);
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
            int cents = s.DeclaredCents, pay = s.PayDay;
            RunTo(m, m.State.Day, MarketCatalogue.PhaseOpen + 3);
            Trade(m, s, OrderSide.Buy, 1000);
            Bot b = m.Hire(2, 500000, 20, 0, 90, out _);
            b.Holdings.Add(new BotHolding { StockId = s.Id, Shares = 400, Cost = 400L * s.Last / 100, OpenedDay = m.State.Day });
            b.Status = BotStatus.Stopped;   // (it keeps the holding to the record day: a stopped bot trades nothing, and is paid its dividends)
            int heard = w.Inner.Heard.Count;
            RunTo(m, pay);
            List<MarketNews> paid = w.Inner.Heard.Skip(heard).Where(n => n.Kind == MarketNewsKind.Paid).ToList();
            right += paid.Any(n => n.Stock == s.Id && n.B == 0 && n.A == 1000L * cents / 100) ? 1 : 0;
            botRight += paid.Any(n => n.Stock == s.Id && n.B == 2 && n.A == 400L * cents / 100) && b.Holding(s.Id) != null ? 1 : 0;
            int dividends = m.State.Trades.Count(x => x.Kind == TradeKind.Dividend && !x.Test && x.Day >= m.State.Day - 6);   // (the news keeps the last week's)
            int lines = m.State.News.Count(n => n.Kind == MarketNewsKind.Paid);
            extra += lines == dividends ? 0 : 1;
            kept &= m.State.News.Any(n => n.Kind == MarketNewsKind.Paid);
            string line = MarketStore.Write(m.State), city = MarketStore.Write(m.State, true);
            MarketState back = MarketStore.Read(line, out _), shared = MarketStore.Read(city, out _);
            kept &= back != null && back.News.Count(n => n.Kind == MarketNewsKind.Paid) == lines;
            notCity &= shared != null && shared.News.All(n => n.Kind != MarketNewsKind.Paid) && shared.News.Count == m.State.News.Count - lines;
        }
        Check(cases >= 6 && right == cases, "bots plus: a dividend paid on the player's shares is a line of the market news with its money (" + right + " of " + cases + ")");
        Check(botRight == cases, "bots plus: and one on a bot's shares, with its firm (" + botRight + " of " + cases + ")");
        Check(extra == 0, "bots plus: one line for each dividend paid to the player, no more");
        Check(kept, "bots plus: the lines are kept in the save's line");
        Check(notCity, "bots plus: and never in the city's line a Going Public host sends: a dividend paid to the host is theirs alone");
    }

    // ---------------------------------------------------------------- the save's line

    private static void BotPlusStore()
    {
        MarketEngine m = BotMarket(661, 5000000, out _);
        int day = SessionDay(m, 3, 10);
        Bot b = m.Hire(3, 900000, 25, 15, 50, out _);
        List<int> ids = m.State.Stocks.Where(s => s.Listed).Take(3).Select(s => s.Id).ToList();
        foreach (int id in ids)
        {
            m.PickFor(3, id, true, out _);
        }
        m.SetScope(3, BotScope.Picked, out _);
        m.SetPayout(3, 70, BotPayEvery.Weekly, out _);
        b.PayMark = 4321;
        b.PayOwed = 55;
        b.PaidOut = 1234;
        b.MonthPaid = 99;
        b.OwnDay = day;
        b.OwnQty = 77;
        b.OwnYear = 2;
        b.OwnBoughtBp = 12;
        b.OwnSoldBp = 3;
        b.OwnBuyDay = day - 1;
        b.OwnSellDay = day - 4;
        Bot c = m.Hire(1, 600000, 20, 10, 35, out _);
        c.Realised = 5000;
        c.Dividends = 300;
        c.Fees = 800;
        string line = MarketStore.Write(m.State);
        MarketState back = MarketStore.Read(line, out _);
        Bot y = back != null ? MarketBots.Hired(back, 3) : null;
        Check(back != null && MarketStore.Write(back) == line, "bots plus: the line with the new settings reads back and writes again the same" + FirstDifference(line, back != null ? MarketStore.Write(back) : null));
        Check(y != null && y.Scope == BotScope.Picked && y.Picks.SequenceEqual(ids) && y.PayPct == 70 && y.PayEvery == BotPayEvery.Weekly && y.PayMark == 4321 && y.PayOwed == 55
              && y.PaidOut == 1234 && y.MonthPaid == 99 && y.OwnDay == day && y.OwnQty == 77 && y.OwnYear == 2 && y.OwnBoughtBp == 12 && y.OwnSoldBp == 3 && y.OwnBuyDay == day - 1
              && y.OwnSellDay == day - 4, "bots plus: a bot comes back with the shares it trades, its payouts and its trading in the player's company");
        // A line from before them: each bot's record ends after what it waits to trade.
        string old = Regex.Replace(line, @"~b:[^~]*", match => "~b:" + string.Join(";", match.Value.Substring(3).Split(';').Select(r => string.Join("|", r.Split('|').Take(27)))));
        MarketState older = MarketStore.Read(old, out _);
        Bot oy = older != null ? MarketBots.Hired(older, 3) : null, oc = older != null ? MarketBots.Hired(older, 1) : null;
        Check(old != line && oy != null && oc != null && oy.Scope == BotScope.Market && oy.Picks.Count == 0 && oy.PayPct == 0 && oy.PayEvery == BotPayEvery.Monthly && oy.PaidOut == 0
              && oy.PayOwed == 0 && oy.OwnBuyDay == 0, "bots plus: a save from before them loads: its whole market, nothing picked and no payout");
        Check(oc != null && oc.PayMark == MarketBots.Profit(oc) && oc.PayMark == 4500, "bots plus: and what a bot made before is reckoned already: only profit from then on is ever paid out");
        Check(older != null && older.Bots.Count == 2 && older.Positions.Count == back.Positions.Count, "bots plus: and everything else of it is as it was");
        // Picks of a company the line no longer has are dropped, as its holdings are.
        string gone = line.Replace("|" + string.Join(":", ids) + "|", "|" + string.Join(":", ids) + ":987654|");
        MarketState lost = MarketStore.Read(gone, out _);
        Check(gone != line && lost != null && MarketBots.Hired(lost, 3).Picks.SequenceEqual(ids), "bots plus: a pick of a company that is not in the line is dropped");
        string city = MarketStore.Write(m.State, true);
        Check(!city.Contains("~b:"), "bots plus: a city's line has no bot in it, nor their settings");
    }

    // Two machines with the same market and the same bot on picks with payouts come to the same prices, worth and payouts.
    private static void BotPlusSameEverywhere()
    {
        MarketEngine a = BotMarket(671, 9000000, out _), b = BotMarket(671, 9000000, out _);
        foreach (MarketEngine m in new[] { a, b })
        {
            SessionDay(m, 3, 10);
            m.Hire(3, 1500000, 25, 10, 50, out _);
            foreach (Stock s in m.State.Stocks.Where(s => s.Listed && s.Own == null).OrderByDescending(s => s.Adv).ThenBy(s => s.Id).Take(10))
            {
                m.PickFor(3, s.Id, true, out _);
            }
            m.SetScope(3, BotScope.Picked, out _);
            m.SetPayout(3, 60, BotPayEvery.Daily, out _);
            RunTo(m, 60);
        }
        Bot x = a.State.Bots[0], y = b.State.Bots[0];
        Check(a.State.Stocks.All(s => b.State.Stock(s.Id).Last == s.Last) && MarketBots.Worth(a.State, x, a.State.Phase) == MarketBots.Worth(b.State, y, b.State.Phase) && x.PaidOut == y.PaidOut
              && x.Trades == y.Trades, "bots plus: the same market and the same bot on its picks with payouts come to the same prices, worth and payouts (" + x.Trades + " trades, " + x.PaidOut + " paid)");
    }

    // ---------------------------------------------------------------- the texts

    private static readonly string[] BotPlusKeys =
    {
        "reallife_mkt_opt_dividend_notes", "reallife_opt_tip_markets_dividend_notes", "reallife_mkt_note_dividend", "reallife_mkt_note_dividend_bot", "reallife_mkt_note_dividends",
        "reallife_mkt_news_paid", "reallife_mkt_news_paid_bot", "reallife_mkt_bot_page_trading", "reallife_mkt_bot_page_shares", "reallife_mkt_bot_page_payouts",
        "reallife_mkt_bot_scope_market", "reallife_mkt_bot_scope_picked", "reallife_mkt_bot_scope_held", "reallife_mkt_bot_plan_picked", "reallife_mkt_bot_plan_pays",
        "reallife_mkt_bot_every_day", "reallife_mkt_bot_every_week", "reallife_mkt_bot_every_month", "reallife_mkt_bot_set_pay", "reallife_mkt_bot_set_pay_none", "reallife_mkt_bot_set_every",
        "reallife_mkt_bot_box_plan", "reallife_mkt_bot_box_paid", "reallife_mkt_bot_pick_none", "reallife_mkt_bot_pick_yours", "reallife_mkt_bot_pick_on", "reallife_mkt_bot_pick_off",
        "reallife_mkt_bot_pick", "reallife_mkt_bot_unpick", "reallife_mkt_bot_note_statement_paid", "reallife_mkt_bot_note_paid", "reallife_mkt_bot_note_paid_owed",
        "reallife_mkt_bot_why_own_shares", "reallife_mkt_bot_why_not_listed", "reallife_mkt_bot_why_picks"
    };

    private static void BotPlusWords()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("bots plus", langs, codes))
        {
            return;
        }
        Dictionary<string, string> en = langs[0];
        var missing = new List<string>();
        foreach (string key in BotPlusKeys)
        {
            string lacking = LanguagesLacking(langs, codes, key);
            if (lacking.Length > 0)
            {
                missing.Add(key + " (" + lacking + ")");
            }
        }
        Check(missing.Count == 0, "bots plus: every text is in every language with the same {names} (" + BotPlusKeys.Length + " keys" + (missing.Count > 0 ? "; " + string.Join(", ", missing) : string.Empty) + ")");
        string[] notes = { "reallife_mkt_note_dividend_bot", "reallife_mkt_note_dividends", "reallife_mkt_bot_note_statement_paid", "reallife_mkt_bot_note_paid", "reallife_mkt_bot_note_paid_owed" };
        Check(notes.All(k => en.ContainsKey(k) && en[k].Length + 40 <= 240), "bots plus: each new notification fits the game's toast with its names filled (240 characters)");
        // Every figure the Guide's bots page gives is filled from the code, in each language.
        string[] facts = { "{bot_picks_most}", "{bot_own_swing}", "{bot_deal_year}", "{bot_founder_most}", "{bot_own_volume}" };
        bool guide = true;
        foreach (Dictionary<string, string> words in langs)
        {
            guide &= words.TryGetValue("reallife_guide_markets_bots", out string g) && facts.All(g.Contains);
        }
        Check(guide, "bots plus: the Guide's bots page says what the shares, the player's own company and the payouts do, its figures from the code, in each language");
        bool news = en.TryGetValue("reallife_guide_markets", out string mg) && mg.Contains("line of the market news");
        Check(news, "bots plus: the Guide's Stocks page says each dividend paid is a line of the market news");
        // In the next row until its version is made, and in that version's own row after (the release empties the next row).
        bool line = false;
        foreach (KeyValuePair<string, string> row in en)
        {
            if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains("have part of its profit paid to you daily, weekly or monthly"))
            {
                line = !row.Value.StartsWith("New: ", StringComparison.Ordinal);
                break;
            }
        }
        Check(line, "bots plus: What's new has the feature's line, with no prefix (in the next row, or its version's row once it is made)");
    }

    // Four bots on picks and on the player's holdings with daily payouts: a market day stays cheap.
    private static void BotPlusCost()
    {
        MarketEngine m = BotMarket(681, 10000000, out _);
        SessionDay(m, 3, 10);
        List<int> picks = m.State.Stocks.Where(s => s.Listed && s.Own == null).OrderByDescending(s => s.Adv).Take(MarketBots.PicksMost).Select(s => s.Id).ToList();
        foreach (int id in picks.Take(4))
        {
            Trade(m, m.State.Stock(id), OrderSide.Buy, 10);
        }
        foreach (BotFirm f in MarketBots.All)
        {
            m.Hire(f.Id, 1000000, 10, 10, 50, out _);
            foreach (int id in picks)
            {
                m.PickFor(f.Id, id, true, out _);
            }
            m.SetScope(f.Id, f.Id % 2 == 0 ? BotScope.Picked : BotScope.Held, out _);
            m.SetPayout(f.Id, 50, BotPayEvery.Daily, out _);
        }
        RunTo(m, 12);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        int day = 12;
        for (int i = 0; i < 20; i++)
        {
            day = TradingOnOrAfter(day + 1);
            RunTo(m, day);
        }
        double perDay = watch.Elapsed.TotalMilliseconds / 20.0;
        long bytes = (GC.GetAllocatedBytesForCurrentThread() - allocated) / 20;
        Console.WriteLine("  bots plus' cost: a market day with four bots on picks and holdings, paying out daily, " + perDay.ToString("0.00") + " ms; " + bytes + " bytes allocated a day");
        Check(perDay < 16.0, "bots plus: a market day with four bots on their picks costs a few milliseconds (" + perDay.ToString("0.00") + " ms)");
        Check(bytes < 96 * 1024, "bots plus: and allocates little (" + bytes + " bytes a day)");
    }
}
