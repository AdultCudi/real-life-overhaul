using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul.Markets;

// Orders that wait and trading bots (Scripts/Markets: MarketTrading, MarketEngine, MarketBots, MarketEngine.Bots.cs):
// a stop loss and a take profit placed only on what is held and on the right side of the price, never opening a short,
// the other cancelled when nothing is left, a gap through a stop filled at the open's price under it; the bots' rules
// (settings held to their choices, the room a company leaves, what they may buy), their fills at the market's price of
// the moment and booked as the player's own trades, decided before the next day's chance exists, the same on every
// machine, their trades moving prices and shared as the player's are, their fees, their floor and a gap through it,
// splits, a company leaving, letting go, the save's line read by a version that knows no bots, and over many seeds
// and every strategy: losing runs and winning runs, and no strategy beating holding the market after its costs.
internal static partial class Program
{
    /// <summary>The player's cash, with every movement kept by the transaction it was booked as.</summary>
    private sealed class KeyedWallet : IMarketWallet
    {
        public long Money;
        public readonly Dictionary<string, long> By = new Dictionary<string, long>();

        public long Cash => Math.Max(0, Money);

        private void Add(string why, long amount)
        {
            By.TryGetValue(why, out long was);
            By[why] = was + amount;
        }

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
            Add(why, -amount);
            return true;
        }

        public void Force(long amount, string why, string name)
        {
            if (amount > 0)
            {
                Money -= amount;
                Add(why, -amount);
            }
        }

        public void Give(long amount, string why, string name)
        {
            if (amount > 0)
            {
                Money += amount;
                Add(why, amount);
            }
        }

        public long Of(string why)
        {
            return By.TryGetValue(why, out long v) ? v : 0;
        }
    }

    /// <summary>The checks' city and secrets, with a keyed wallet.</summary>
    private sealed class BotWorld : IMarketWorld
    {
        public readonly MarketWorld Inner;
        public readonly KeyedWallet Purse = new KeyedWallet();
        public readonly List<BotNote> Notes = new List<BotNote>();

        public BotWorld(ulong seed)
        {
            Inner = new MarketWorld(seed);
        }

        public ulong NewSecret() => Inner.NewSecret();
        public MarketEnv Env(int day) => Inner.Env(day);
        public IMarketWallet Wallet => Purse;
        public int CommissionLeast => Inner.CommissionLeast;
        public void Said(in MarketNews news) => Inner.Said(news);
        public void Filled(Order order, Stock stock, long qty, int cents) => Inner.Filled(order, stock, qty, cents);
        public void Paid(TradeKind kind, Stock stock, long amount) => Inner.Paid(kind, stock, amount);
        public void Called(long amount, int byDay) => Inner.Called(amount, byDay);
    }

    private static MarketEngine BotMarket(ulong seed, long money, out BotWorld world, MarketTape tape = null)
    {
        world = new BotWorld(seed);
        world.Purse.Money = money;
        MarketState state = MarketSetup.Open(seed, 1, 0, 60, world.Env(1), seed, null, null, null);
        var m = new MarketEngine(state, tape ?? new MarketTape(seed), world);
        BotWorld w = world;
        m.BotTold = n => w.Notes.Add(n);
        return m;
    }

    private static void MarketTrading2()
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        MarketClosingOrders();
        MarketGapThroughStop();
        MarketBotRules();
        MarketBotFills();
        MarketBotSameEverywhere();
        MarketBotFees();
        MarketBotFloor();
        MarketBotLife();
        MarketBotStore();
        MarketBotRuns();
        MarketBotCost();
        Console.WriteLine("  orders and trading bots: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static OrderWhy Closing(MarketEngine m, Stock s, OrderSide side, long qty, OrderType type, int cents, out Order order, OrderLife life = OrderLife.UntilCancelled)
    {
        OrderWhy why;
        order = m.Place(s.Id, side, type, life, qty, type == OrderType.Limit ? cents : 0, type == OrderType.Stop ? cents : 0, m.Trading, m.Held, false, out why, true);
        return why;
    }

    // ---------------------------------------------------------------- a stop loss and a take profit

    private static void MarketClosingOrders()
    {
        var world = new MarketWorld(201);
        world.Purse.Money = 100000000;
        MarketEngine m = NewMarket(201, 60, 1, 0, world);
        SessionDay(m, 2, 10);
        Stock s = Liquid(m, MarketSize.Large), other = Liquid(m, MarketSize.Large, 1);
        Trade(m, s, OrderSide.Buy, 100);
        Quote q = MarketTrading.QuoteOf(m.State, s, m.State.Phase);
        Check(Closing(m, other, OrderSide.Sell, 10, OrderType.Stop, q.Bid / 2, out _) == OrderWhy.NothingHeld, "orders: a stop loss on a company not held is refused");
        Check(Closing(m, s, OrderSide.Sell, 101, OrderType.Stop, q.Bid * 95 / 100, out _) == OrderWhy.NoShares, "orders: a stop loss for more than is held is refused");
        Check(Closing(m, s, OrderSide.Sell, 100, OrderType.Stop, q.Bid + 5, out _) == OrderWhy.StopSide, "orders: a stop loss to sell over the bid is refused");
        Check(Closing(m, s, OrderSide.Sell, 100, OrderType.Limit, q.Bid - 1, out _) == OrderWhy.ProfitSide, "orders: a take profit to sell at or under the bid is refused");
        Check(Closing(m, s, OrderSide.Buy, 100, OrderType.Stop, q.Ask * 105 / 100, out _) == OrderWhy.NothingHeld, "orders: a stop loss to buy back needs shares sold short");
        Check(Closing(m, s, OrderSide.Sell, 100, OrderType.Market, 0, out _) == OrderWhy.BadPrice, "orders: there is no stop loss or take profit at the market");
        OrderWhy a = Closing(m, s, OrderSide.Sell, 100, OrderType.Stop, Math.Max(1, (int)(q.Bid * 0.6)), out Order stop);
        OrderWhy b = Closing(m, s, OrderSide.Sell, 100, OrderType.Limit, q.Bid * 3, out Order profit);
        Check(a == OrderWhy.None && b == OrderWhy.None && stop.Closing && profit.Closing, "orders: a stop loss and a take profit on the same shares are both placed");
        Check(MarketTrading.Reserved(m.State, s.Id, OrderSide.Sell) == 0 && Trade(m, s, OrderSide.Sell, 40) != null && m.State.Held(s.Id) == 60,
            "orders: they hold nothing back: the shares can still be sold at the market");
        Check(stop.IsOpen && profit.IsOpen, "orders: and they stay open while something is held");
        Trade(m, s, OrderSide.Sell, 60);
        Check(m.State.Held(s.Id) == 0 && !stop.IsOpen && !profit.IsOpen && stop.Why == OrderWhy.NothingHeld && profit.Why == OrderWhy.NothingHeld,
            "orders: with nothing left held both are cancelled, nothing left to close");
        // A margin account: a stop loss never sells more than is held (no short is opened).
        var mw = new MarketWorld(202);
        mw.Purse.Money = 100000000;
        MarketEngine mm = NewMarket(202, 60, 1, 0, mw);
        int day = SessionDay(mm, 2, 10);
        Stock k = Liquid(mm, MarketSize.Large);
        OrderWhy deposit;
        mm.State.Account.Margin = true;
        mm.State.Account.Cash = 5000000;
        Trade(mm, k, OrderSide.Buy, 300);
        Quote kq = MarketTrading.QuoteOf(mm.State, k, mm.State.Phase);
        deposit = Closing(mm, k, OrderSide.Sell, 300, OrderType.Limit, kq.Bid + 1, out Order tp);
        // the holding falls to 100 by a sale at the market, then the take profit is reached by a price set over it
        Trade(mm, k, OrderSide.Sell, 200);
        tp.LimitCents = 1;   // (as if the market had come to it)
        RunTo(mm, day, mm.State.Phase + 2);
        Check(deposit == OrderWhy.None && mm.State.Held(k.Id) == 0 && tp.Filled == 100 && !tp.IsOpen, "orders: in a margin account a take profit sells what is still held and no more (" + tp.Filled
              + " sold, " + mm.State.Held(k.Id) + " held)");
        // A day order on the right side that is never reached expires at the close.
        Stock d = Liquid(m, MarketSize.Mid);
        Trade(m, d, OrderSide.Buy, 50);
        Quote dq = MarketTrading.QuoteOf(m.State, d, m.State.Phase);
        Closing(m, d, OrderSide.Sell, 50, OrderType.Stop, Math.Max(1, dq.Bid / 10), out Order dayStop, OrderLife.Day);
        RunTo(m, m.State.Day);
        Check(dayStop != null && dayStop.State == OrderState.Expired, "orders: a stop loss for the day that is not reached expires at the close");
        // Saved and read back as what it is.
        MarketState back = MarketStore.Read(MarketStore.Write(m.State), out _);
        Order again = back?.Order(dayStop.Id);
        Check(again != null && again.Closing && again.Type == OrderType.Stop, "orders: a stop loss reads back from the save as a stop loss");
    }

    // A night that opens under a stop loss: it goes at the open's price, under the stop.
    private static void MarketGapThroughStop()
    {
        int gaps = 0, under = 0, tried = 0;
        double worst = 0.0;
        for (ulong seed = 211; seed <= 216; seed++)
        {
            var world = new MarketWorld(seed);
            world.Purse.Money = 100000000;
            MarketEngine m = NewMarket(seed, 60, 1, 0, world);
            int day = SessionDay(m, 2, 70);
            Stock s = Liquid(m, MarketSize.Mid);
            Trade(m, s, OrderSide.Buy, 100);
            Quote q = MarketTrading.QuoteOf(m.State, s, m.State.Phase);
            OrderWhy why = Closing(m, s, OrderSide.Sell, 100, OrderType.Stop, Math.Max(1, q.Bid * 97 / 100), out Order stop);
            tried++;
            if (why != OrderWhy.None)
            {
                continue;
            }
            RunTo(m, day);   // the close
            if (!stop.IsOpen)
            {
                continue;
            }
            m.NewsFor(s.Id, true);   // a crisis after the close: the price follows at the open
            int next = MarketTrading.NextTradingDay(day);
            RunTo(m, next, MarketCatalogue.PhaseOpen);
            if (stop.Filled > 0)
            {
                gaps++;
                Trade t = m.State.Trades.Last(x => x.StockId == s.Id && x.Kind == TradeKind.Sold);
                under += t.PriceCents < stop.StopCents && t.Phase == MarketCatalogue.PhaseOpen ? 1 : 0;
                worst = Math.Min(worst, t.PriceCents / (double)stop.StopCents - 1.0);
            }
        }
        Check(gaps >= 3 && under == gaps, "orders: a crisis overnight takes the open under a stop loss, and it goes at the open's price under the stop (" + under + " of " + gaps + " of " + tried
              + ", the worst " + (worst * 100).ToString("0.0") + "% under)");
    }

    // ---------------------------------------------------------------- the bots' rules

    private static void MarketBotRules()
    {
        Check(MarketBots.All.Length == MarketBots.FirmsMost && MarketBots.All.Select(f => f.Id).Distinct().Count() == MarketBots.All.Length
              && MarketBots.All.Select(f => f.Strategy).Distinct().Count() == 4, "bots: four firms, one for each strategy, each with its own id");
        Check(MarketBots.All.All(f => f.FeeBp > 0 && f.FeeBp <= 50 && f.ShareBp > 0 && f.ShareBp <= 3000 && MarketBots.Sizes.Contains(f.Size) && MarketBots.Stops.Contains(f.Stop)
              && MarketBots.Losses.Contains(f.Loss)), "bots: every firm's fee, share and settings are inside the rules");
        Check(MarketBots.Choice(MarketBots.Sizes, 0) == 10 && MarketBots.Choice(MarketBots.Sizes, 30) == 25 && MarketBots.Choice(MarketBots.Sizes, 1000) == 100
              && MarketBots.Choice(MarketBots.Losses, 99) == 90 && MarketBots.Choice(MarketBots.Losses, -5) == 20 && MarketBots.Choice(MarketBots.Stops, 7) == 5,
            "bots: a setting is held to its choices");
        MarketEngine m = BotMarket(221, 1000000, out BotWorld w);
        SessionDay(m, 2, 10);
        long least = m.BotCapitalLeast;
        Check(m.Hire(1, least - 1, 20, 10, 35, out BotWhy why) == null && why == BotWhy.TooLittle, "bots: less than the least capital is refused");
        Check(m.Hire(1, w.Purse.Cash + 1, 20, 10, 35, out why) == null && why == BotWhy.NoCash, "bots: more capital than the player's cash is refused");
        Bot b = m.Hire(1, 50000, 30, 99, 0, out why);
        Check(b != null && why == BotWhy.None && b.Size == 25 && b.Stop == 25 && b.Loss == 20 && b.Cash == 50000 && w.Purse.Money == 1000000,
            "bots: a bot is hired with its settings held to their choices, and no money moves until it trades");
        Check(m.Hire(1, 50000, 20, 10, 35, out why) == null && why == BotWhy.Hired, "bots: one bot a firm");
        Check(m.Restart(1, out why) == false && why == BotWhy.NotStopped, "bots: only a stopped bot is started again");
        m.HoldAfterLoad();
        m.Tape.Reach(m.State.Day + 3, MarketCatalogue.PhaseOpen, true);
        m.HoldAfterLoad();
        Check(m.Held && m.Hire(2, 50000, 20, 10, 35, out why) == null && why == BotWhy.Held && !m.SetBot(1, 10, 0, 90, out why) && !m.Dismiss(1, out why) && !m.SellUp(1, out why),
            "bots: nothing about a bot changes while the market replays after a load");
        // What a bot may buy.
        Stock own = m.State.Stocks.First(s => s.Listed);
        BotFirm trend = MarketBots.Firm(1), momentum = MarketBots.Firm(3);
        Stock small = m.State.Stocks.First(s => s.Listed && s.Size == MarketSize.Small && s.BidCents <= 0 && m.State.Day - s.ListedDay > 30 && s.Last > 1000);
        Check(!MarketBots.Buyable(m.State, trend, small, m.State.Day) && MarketBots.Buyable(m.State, momentum, small, m.State.Day), "bots: only the momentum firm buys small companies");
        var mine = new OwnListing();
        own.Own = mine;
        Check(!MarketBots.Buyable(m.State, momentum, own, m.State.Day), "bots: no bot buys a player's own company");
        own.Own = null;
        own.BidCents = own.Last * 2;
        Check(!MarketBots.Buyable(m.State, momentum, own, m.State.Day), "bots: nor a company under a bid");
        own.BidCents = 0;
        // The room a company leaves: the player's holding and the bots' together are held to the most one may hold.
        Stock k = Liquid(m, MarketSize.Large);
        long most = MarketTrading.MostHeld(k), order = MarketTrading.MostPerOrder(k);
        b.Holdings.Add(new BotHolding { StockId = k.Id, Shares = most - 10, Cost = 1 });
        Check(MarketBots.Room(m.State, k) == 10 && Math.Min(order, most) >= 10, "bots: a bot buys no more of a company than the player may hold, the bots' shares with theirs");
        b.Holdings.Clear();
    }

    // Every fill a bot makes is at the market's price of its moment, and booked as the player's own trades are.
    private static void MarketBotFills()
    {
        int fills = 0, wrong = 0, booked = 0;
        long cashMoved = 0, botMoney = 0;
        bool decidedBlind = true;
        for (ulong seed = 231; seed <= 234; seed++)
        {
            MarketEngine m = BotMarket(seed, 2000000, out BotWorld w);
            SessionDay(m, 3, 10);
            foreach (BotFirm f in MarketBots.All)
            {
                m.Hire(f.Id, 300000, f.Size, f.Stop, f.Loss, out _);
            }
            long money0 = w.Purse.Money;
            Trade last = m.State.Trades.LastOrDefault();
            for (int step = 0; step < 81 * 40; step++)
            {
                int day = m.State.Day, phase = m.State.Phase;
                long target = MarketState.Time(day, phase) + 1;
                if (phase >= MarketCatalogue.PhaseClose)
                {
                    target = MarketState.Time(day + 1, 0);
                }
                int work = int.MaxValue;
                m.RunTo(target, ref work);
                if (m.State.Phase == MarketCatalogue.PhaseClose && MarketCatalogue.TradingDay(m.State.Day))
                {
                    // Decided at the close, before the next session's chance exists.
                    int next = MarketTrading.NextTradingDay(m.State.Day);
                    decidedBlind &= !m.Tape.Secret(next, out _);
                }
                int from = last != null ? m.State.Trades.IndexOf(last) + 1 : 0;
                var fresh = m.State.Trades.Skip(from).ToList();
                last = m.State.Trades.LastOrDefault();
                foreach (Trade t in fresh)
                {
                    Stock s = m.State.Stock(t.StockId);
                    if (t.Bot == 0 || (t.Kind != TradeKind.Bought && t.Kind != TradeKind.Sold) || s == null || !s.Listed || s.BarCount == 0 || t.Phase <= MarketCatalogue.PhaseOpen
                        || fresh.Count(x => x.StockId == t.StockId) > 1)
                    {
                        continue;   // (a company traded twice in a step moved between the two)
                    }
                    fills++;
                    // The step's last price, at the offer or the bid: the market's price of the moment, before the trade's own move.
                    Candle bar = s.Bars[s.BarCount - 1];
                    double half = MarketTrading.HalfSpread(m.State, s, t.Phase);
                    bool buying = t.Kind == TradeKind.Bought;
                    wrong += buying ? (t.PriceCents < (int)Math.Ceiling(bar.C * (1.0 + half)) - 1 ? 1 : 0) : (t.PriceCents > (int)Math.Floor(bar.C * (1.0 - half)) + 1 ? 1 : 0);
                    booked += t.Fee >= Math.Min(Math.Abs(t.Value), MarketTrading.Commission(Math.Abs(t.Value), w.CommissionLeast)) && t.Fee > 0 ? 1 : 0;
                }
            }
            cashMoved += w.Purse.Money - money0;
            foreach (Bot b in m.State.Bots)
            {
                botMoney += b.Cash - b.Capital;   // what its trades, dividends and fees did to its money
            }
            Check(w.Purse.Of(MarketMoney.Bought) < 0 && w.Purse.Of(MarketMoney.Sold) > 0 && w.Purse.Of(MarketMoney.Commission) < 0,
                "bots: a bot's purchases, sales and commission are the player's own transactions of the same kinds (seed " + seed + ")");
        }
        Check(fills > 100 && wrong == 0, "bots: every fill is at the offer or the bid of its moment, never better (" + fills + " fills, " + wrong + " wrong)");
        Check(booked == fills, "bots: and every one pays the commission (" + booked + " of " + fills + ")");
        Check(cashMoved == botMoney, "bots: what the bots' money did is exactly what the player's cash did (" + cashMoved + " against " + botMoney + ")");
        Check(decidedBlind, "bots: a bot decides at the close before the next session's chance is drawn: it cannot know tomorrow");
    }

    // Two machines with the same market and the same bot play the same days, to the cent; a host's bot trades are on the
    // tape as a player's are, for every market.
    private static void MarketBotSameEverywhere()
    {
        MarketEngine a = BotMarket(241, 2000000, out _), b = BotMarket(241, 2000000, out _), none = BotMarket(241, 2000000, out _);
        foreach (MarketEngine m in new[] { a, b, none })
        {
            SessionDay(m, 3, 10);
        }
        a.Hire(3, 400000, 50, 10, 50, out _);
        b.Hire(3, 400000, 50, 10, 50, out _);
        foreach (MarketEngine m in new[] { a, b, none })
        {
            RunTo(m, 40);
        }
        bool same = a.State.Stocks.All(s => b.State.Stock(s.Id).Last == s.Last) && MarketBots.Worth(a.State, a.State.Bots[0], a.State.Phase) == MarketBots.Worth(b.State, b.State.Bots[0], b.State.Phase);
        Check(same && a.State.Bots[0].Trades > 0, "bots: the same market and the same bot come to the same prices and the same worth (" + a.State.Bots[0].Trades + " trades)");
        int moved = a.State.Stocks.Count(s => none.State.Stock(s.Id).Last != s.Last);
        Check(moved > 0, "bots: a bot's trades move the prices of what it trades (" + moved + " companies away from the market without it)");
        // A Going Public host: the bot's lasting moves go on the tape as events, as the player's do.
        MarketEngine host = BotMarket(242, 2000000, out _);
        SessionDay(host, 3, 10);
        host.TradesAsEvents = true;
        host.Hire(1, 400000, 20, 10, 35, out _);
        RunTo(host, 30);
        int events = host.Tape.Whole().Split('\n').Count(l => l.StartsWith("E ") && l.Split(' ').Length > 3 && l.Split(' ')[3] == MarketInputs.EventTrade.ToString());
        Check(host.State.Bots[0].Trades > 0 && events >= host.State.Bots[0].Trades, "bots: in a shared city a bot's trades are the tape's trade events, as the player's are (" + events + " for "
              + host.State.Bots[0].Trades + " trades)");
        // A guest: its bot's moves go to the host through the trade the player's own trades already use.
        MarketEngine guest = BotMarket(243, 2000000, out _);
        SessionDay(guest, 3, 10);
        int sent = 0;
        guest.TradeSink = (stock, micros, shares) => sent++;
        guest.Hire(1, 400000, 20, 10, 35, out _);
        RunTo(guest, 30);
        Check(guest.State.Bots[0].Trades > 0 && sent == guest.State.Bots[0].Trades, "bots: a guest's bot sends each trade's move to the host as a player's trade (" + sent + " sent)");
    }

    // The month's fee for the days traded and the share of profit over the high-water mark, booked as the bot's.
    private static void MarketBotFees()
    {
        MarketEngine m = BotMarket(251, 3000000, out BotWorld w);
        SessionDay(m, 3, 10);
        Bot b = m.Hire(4, 1000000, 10, 0, 90, out _);
        int month = MarketCatalogue.MonthOf(m.State.Day, 60);
        int end = m.State.Day;
        while (MarketCatalogue.MonthOf(MarketTrading.NextTradingDay(end), 60) == month)
        {
            end = MarketTrading.NextTradingDay(end);
        }
        RunTo(m, end);
        BotNote st = w.Notes.LastOrDefault(n => n.Kind == BotNoteKind.Statement);
        long fees = -w.Purse.Of(MarketMoney.BotFee), share = -w.Purse.Of(MarketMoney.BotShare);
        BotFirm f = b.Def;
        long worst = MarketBots.Fee(f, 2000000, (int)(m.BotCapitalLeast * MarketBots.FeeLeastAtStart / MarketBots.CapitalLeastAtStart));
        Check(st.Kind == BotNoteKind.Statement && st.Firm == 4 && fees > 0 && fees <= worst && b.Fees == fees + share && st.C == fees + share,
            "bots: the month's statement takes the fee for the days it traded (" + fees + "), and its share of profit (" + share + "), booked as its own transactions");
        Check(b.MonthTrades == 0 && b.MonthDays == 0, "bots: and a new month begins");
        // The share only of profit over the high-water mark: a month at a loss takes none.
        long hw = b.HighWater;
        b.HighWater = MarketBots.Worth(m.State, b, MarketCatalogue.PhaseClose) * 2;
        long shareBefore = -w.Purse.Of(MarketMoney.BotShare);
        m.BotMonthNow();
        Check(-w.Purse.Of(MarketMoney.BotShare) == shareBefore, "bots: no share is taken while it is under its high-water mark");
        b.HighWater = 1;
        m.BotMonthNow();
        Check(-w.Purse.Of(MarketMoney.BotShare) > shareBefore && b.HighWater > 1, "bots: over it the share is taken and the mark moves up");
        _ = hw;
    }

    // Under its floor a bot sells everything and stops; a fall that jumps takes it further under than its limit.
    private static void MarketBotFloor()
    {
        MarketEngine m = BotMarket(261, 3000000, out BotWorld w);
        int day = SessionDay(m, 3, 10);
        Bot b = m.Hire(3, 500000, 100, 0, 20, out _);
        RunTo(m, day);
        RunTo(m, MarketTrading.NextTradingDay(day), MarketCatalogue.PhaseOpen + 3);
        Check(b.Holdings.Count == 1, "bots: a bot with all its worth in one holding buys one company (" + b.Holdings.Count + ")");
        if (b.Holdings.Count == 0)
        {
            return;
        }
        Stock s = m.State.Stock(b.Holdings[0].StockId);
        s.Mis -= 0.8;   // the price more than halves at once
        s.Last = s.MidCents;
        RunTo(m, m.State.Day, m.State.Phase + 1);
        long worth = MarketBots.Worth(m.State, b, m.State.Phase);
        BotNote limit = w.Notes.LastOrDefault(n => n.Kind == BotNoteKind.Limit);
        Check(b.Status == BotStatus.Stopped && b.Holdings.Count == 0 && limit.Kind == BotNoteKind.Limit, "bots: under the most it may lose, it sells everything and stops, and says so");
        Check(worth < MarketBots.Floor(b) && worth < b.Capital / 2, "bots: a fall that jumps takes it under its floor: it lost most of what it was given (" + worth + " of " + b.Capital + ")");
        Check(m.Restart(3, out BotWhy why), "bots: a stopped bot with enough left starts again (" + why + ")");
        Check(b.Base == b.Cash && b.Status == BotStatus.Running, "bots: from what it has now");
    }

    // Splits, a company leaving, selling up and letting go.
    private static void MarketBotLife()
    {
        MarketEngine m = BotMarket(271, 3000000, out BotWorld w);
        int day = SessionDay(m, 3, 10);
        Bot b = m.Hire(1, 600000, 20, 10, 35, out _);
        Stock s = Liquid(m, MarketSize.Large);
        b.Holdings.Add(new BotHolding { StockId = s.Id, Shares = 100, Cost = 100L * s.Last / 100, StopCents = s.Last / 2, OpenedDay = day });
        int stop = s.Last / 2;
        MarketLife.Split(m.State, s, 3);
        BotHolding h = b.Holding(s.Id);
        Check(h.Shares == 300 && h.StopCents == stop / 3, "bots: a split multiplies a bot's shares and divides its stop, rounded down");
        long cash = w.Purse.Money, botCash = b.Cash;
        m.Delist(s.Id, GoneReason.TakenOver, 1000);
        Check(b.Holding(s.Id) == null && b.Cash == botCash + 3000 && w.Purse.Money == cash + 3000, "bots: a company that leaves pays out a bot's shares to the player and into the bot's money");
        // Selling up, then letting go.
        RunTo(m, day);
        RunTo(m, MarketTrading.NextTradingDay(day), MarketCatalogue.PhaseOpen + 2);
        int held = b.Holdings.Count;
        m.SellUp(1, out _);
        RunTo(m, m.State.Day, m.State.Phase + 1);
        Check(b.Holdings.Count == 0 && b.Status == BotStatus.Stopped, "bots: sold up at the next step it holds nothing and stops (it held " + held + ")");
        Bot c = m.Hire(2, 400000, 50, 0, 50, out _);
        Stock t = Liquid(m, MarketSize.Mid);
        c.Holdings.Add(new BotHolding { StockId = t.Id, Shares = 70, Cost = 7000, OpenedDay = day });
        long before = m.State.Held(t.Id), playerMoney = w.Purse.Money;
        Check(m.Dismiss(2, out _) && MarketBots.Hired(m.State, 2) == null && m.State.Held(t.Id) == before + 70, "bots: a bot let go hands what it holds to the player as their own");
        Check(w.Purse.Money <= playerMoney && w.Purse.Money >= playerMoney - c.Fees - 1 && w.Notes.Any(n => n.Kind == BotNoteKind.Dismissed),
            "bots: and nothing is sold: only its fee to date is taken");
    }

    // The save's line: exact, and read by a version that knows no bots as the player's own shares.
    private static void MarketBotStore()
    {
        MarketEngine m = BotMarket(281, 5000000, out BotWorld w);
        int day = SessionDay(m, 3, 10);
        Stock s = Liquid(m, MarketSize.Large);
        Trade(m, s, OrderSide.Buy, 40);
        Bot b = m.Hire(1, 800000, 20, 10, 35, out _);
        b.Holdings.Add(new BotHolding { StockId = s.Id, Shares = 25, Cost = 2500, StopCents = 5, OpenedDay = day });
        m.Hire(3, 600000, 25, 15, 50, out _);
        RunTo(m, 30, MarketCatalogue.PhaseOpen + 20);
        string line = MarketStore.Write(m.State);
        MarketState back = MarketStore.Read(line, out _);
        Check(back != null && MarketStore.Write(back) == line, "bots: the save's line with bots reads back and writes again the same" + FirstDifference(line, back != null ? MarketStore.Write(back) : null));
        bool same = back != null && back.Bots.Count == m.State.Bots.Count;
        for (int i = 0; same && i < back.Bots.Count; i++)
        {
            Bot x = m.State.Bots[i], y = back.Bots[i];
            same = x.Firm == y.Firm && x.Cash == y.Cash && x.Capital == y.Capital && x.Holdings.Count == y.Holdings.Count && x.Trades == y.Trades && x.Fees == y.Fees && x.HighWater == y.HighWater
                   && x.Holdings.All(hh => y.Holding(hh.StockId) != null && y.Holding(hh.StockId).Shares == hh.Shares && y.Holding(hh.StockId).Cost == hh.Cost) && x.Pending.Count == y.Pending.Count;
        }
        Check(same, "bots: every bot comes back with its money, settings, record and holdings");
        Check(back != null && m.State.Positions.All(p => back.Position(p.StockId) != null && back.Position(p.StockId).Shares == p.Shares) && back.Positions.Count == m.State.Positions.Count,
            "bots: and the player's own holdings come back as theirs alone");
        // A version that knows no bots skips part b: every share a bot held is the player's.
        int at = line.IndexOf("~b:", StringComparison.Ordinal);
        int end = line.IndexOf('~', at + 3);
        string old = end < 0 ? line.Substring(0, at) : line.Substring(0, at) + line.Substring(end);
        MarketState older = MarketStore.Read(old, out _);
        long sharesNow = m.State.Positions.Sum(p => p.Shares) + m.State.Bots.Sum(x => x.Holdings.Sum(hh => hh.Shares));
        long sharesOld = older != null ? older.Positions.Sum(p => p.Shares) : -1;
        Check(older != null && older.Bots.Count == 0 && sharesOld == sharesNow, "bots: a version without bots reads every share a bot held as the player's own (" + sharesOld + " of " + sharesNow + ")");
        // A city's line has none of it.
        string city = MarketStore.Write(m.State, true);
        Check(!city.Contains("~b:") && MarketStore.Read(city, out _).Bots.Count == 0, "bots: a Going Public host's line for its guests has no bot in it");
        // A guest's market from the host's: the guest's bots come with them.
        string share = MarketShare.Write(BotMarket(282, 1, out _).State, null);
        MarketState adopted = MarketShare.Adopt(m.State, share, new List<long>(), null, out _, out _);
        Check(adopted != null && adopted.Bots.Count == m.State.Bots.Count, "bots: a guest's bots come with them into the host's market");
        // A damaged record is dropped, not a fault.
        string damaged = line.Replace("~b:1|", "~b:9|");
        Check(MarketStore.Read(damaged, out _) != null, "bots: a bot record that is not one is dropped, not a fault");
    }

    // Over many seeds, every strategy with its own settings: some runs lose and some win, none beats holding the market.
    private static void MarketBotRuns()
    {
        var gains = new Dictionary<int, List<double>>();
        var excess = new Dictionary<int, List<double>>();
        foreach (BotFirm f in MarketBots.All)
        {
            gains[f.Id] = new List<double>();
            excess[f.Id] = new List<double>();
        }
        double worst = 0.0;
        int stopped = 0;
        for (ulong seed = 300; seed < 324; seed++)
        {
            MarketEngine m = BotMarket(seed, 10000000, out _);
            SessionDay(m, 3, 10);
            foreach (BotFirm f in MarketBots.All)
            {
                m.Hire(f.Id, 1000000, f.Size, f.Stop, f.Loss, out _);
            }
            double index0 = m.State.IndexLast;
            double value0 = m.State.Stocks.Where(s => s.Listed).Sum(s => s.MarketValue);
            int heard = WorldOf2(m).Inner.Heard.Count;
            RunTo(m, 3 + 120);   // two 60-day years
            // Holding the market: the index and the dividends its companies paid out, on what the market was worth.
            double paid = WorldOf2(m).Inner.Heard.Skip(heard).Where(n => n.Kind == MarketNewsKind.Dividend)
                .Sum(n => m.State.Stock(n.Stock) != null ? n.A / 100.0 * m.State.Stock(n.Stock).Shares : 0.0);
            double index = m.State.IndexLast / index0 - 1.0 + paid / Math.Max(1.0, value0);
            foreach (Bot b in m.State.Bots)
            {
                double g = MarketBots.Worth(m.State, b, m.State.Phase) / (double)b.Capital - 1.0;
                gains[b.Firm].Add(g);
                excess[b.Firm].Add(g - index);
                worst = Math.Min(worst, g);
                stopped += b.Status == BotStatus.Stopped ? 1 : 0;
            }
        }
        foreach (BotFirm f in MarketBots.All)
        {
            List<double> g = gains[f.Id], e = excess[f.Id];
            Console.WriteLine("  bots, " + f.Name + " over two 60-day years and " + g.Count + " seeds: mean " + (g.Average() * 100).ToString("0.0") + "%, from " + (g.Min() * 100).ToString("0.0")
                              + "% to " + (g.Max() * 100).ToString("0.0") + "%; against holding the market " + (e.Average() * 100).ToString("0.0") + "%");
            Check(g.Any(x => x < 0.0) && g.Any(x => x > 0.0), "bots: " + f.Name + " loses in some runs and gains in others: never a sure win");
            Check(e.Average() < 0.0, "bots: " + f.Name + " does not beat holding the market after its costs and fees (" + (e.Average() * 100).ToString("0.0") + "% over two years)");
        }
        Console.WriteLine("  bots: the worst run lost " + (-worst * 100).ToString("0.0") + "%, " + stopped + " bot(s) stopped at the most they may lose");
    }

    private static BotWorld WorldOf2(MarketEngine m)
    {
        return (BotWorld)typeof(MarketEngine).GetField("_world", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(m);
    }

    private static void MarketBotCost()
    {
        MarketEngine m = BotMarket(321, 10000000, out _);
        SessionDay(m, 3, 10);
        foreach (BotFirm f in MarketBots.All)
        {
            m.Hire(f.Id, 1000000, 10, 10, 50, out _);
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
        Console.WriteLine("  bots' cost: a market day with four bots, " + perDay.ToString("0.00") + " ms; " + bytes + " bytes allocated a day");
        Check(perDay < 16.0, "bots: a market day with four bots costs a few milliseconds (" + perDay.ToString("0.00") + " ms)");
        Check(bytes < 96 * 1024, "bots: and allocates little (" + bytes + " bytes a day: trades and notes)");
    }
}
