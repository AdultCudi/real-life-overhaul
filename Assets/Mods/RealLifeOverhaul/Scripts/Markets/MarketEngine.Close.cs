using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Part of the engine: the close, dividends, the companies' lives and the day's news.</summary>
    public sealed partial class MarketEngine
    {
        private readonly MarketValuation.Terms _terms = new MarketValuation.Terms();   // a company's worth taken apart for its settling

        // A company's day as a candle of its history.
        private void Candles(Stock s, int day)
        {
            if (!s.Listed)
            {
                return;
            }
            var candle = new Candle(day, s.Open, s.High, s.Low, s.Last, s.DayVolume);
            MarketHistory.AddDay(s.Daily, s.Monthly, candle, Year);
            s.Adv = s.Adv * 0.95 + Math.Max(s.Adv * 0.3, s.DayVolume) * 0.05;
        }

        // The index's day and the sectors' closes, after every company's.
        private void CloseIndices(int day)
        {
            MarketHistory.AddDay(State.IndexDaily, State.IndexMonthly, new Candle(day, State.IndexOpen, State.IndexHigh, State.IndexLow, State.IndexLast, 0), Year);
            for (int sec = 0; sec < Sectors; sec++)
            {
                List<int> closes = State.SectorDaily[sec];
                closes.Add(State.SectorLast[sec]);
                while (closes.Count > MarketCatalogue.DailyKept)
                {
                    closes.RemoveAt(0);
                }
            }
        }

        // After every company is settled: the money of the day, the companies' lives, tomorrow's variance, the account.
        private void CloseBooks(int day)
        {
            Dividends(day);
            Income(day);   // the player's bonds (MarketEngine.Bonds.cs)
            Corporate(day);
            Variance();
            Interest(day);
            Margin(day, MarketCatalogue.PhaseClose);
            Expire(day);
            WorkedClose(day);   // MarketEngine.Invest.cs
        }

        // The close's last piece, after the bots: the day's news, and old news goes.
        private void CloseEnd(int day)
        {
            CloseNews(day);
            // The closes the day's moves are measured from stay until the next open (MarketLife.DayBases).
            for (int i = State.News.Count - 1; i >= 0; i--)
            {
                if (State.News[i].Day < day - 6)
                {
                    State.News.RemoveAt(i);
                }
            }
        }

        // After the close the day's news goes into the business's Outlook, worked out so the price stays exactly where it
        // closed; what is left between price and worth is the mood alone.
        private void Settle(Stock s, int day)
        {
            if (!s.Listed || s.BidCents > 0)
            {
                return;
            }
            MarketEnv env = State.Env;
            double logPrice = s.LogFair + s.Mis;
            double accrued = MarketLife.Accrued(s, day, Year);
            double rest = Math.Exp(logPrice - s.Sent) - accrued;
            _terms.Fill(s, env);
            if (rest > 0.0)
            {
                s.Outlook = MarketValuation.OutlookFor(_terms, rest, s.Outlook);
            }
            s.LogFair = MarketLife.LogFairAt(s, _terms.Worth(s.Outlook), day, Year);
            s.Mis = logPrice - s.LogFair;
            s.Sent = s.Mis;
        }

        // The market's and the sectors' variance for tomorrow, from today's moves.
        private void Variance()
        {
            double z = State.DayVar > 0.0 ? State.DayZ / Math.Sqrt(State.DayVar) : 0.0;
            double omega = 1.0 - MarketCatalogue.GarchA - MarketCatalogue.GarchB - MarketCatalogue.GarchGamma * 0.5;
            double v = State.Variance;
            v = omega + (MarketCatalogue.GarchA + (z < 0.0 ? MarketCatalogue.GarchGamma : 0.0)) * z * z * v + MarketCatalogue.GarchB * v;
            State.Variance = MarketValuation.Clamp(v, MarketCatalogue.VarianceLeast, MarketCatalogue.VarianceMost);
            State.LastShock = z;
            double sectorOmega = 1.0 - MarketCatalogue.SectorGarchA - MarketCatalogue.SectorGarchB;
            for (int sec = 0; sec < Sectors; sec++)
            {
                double sz = State.DayVar > 0.0 ? State.SectorDayZ[sec] / Math.Sqrt(State.DayVar) : 0.0;
                double sv = State.SectorVar[sec] <= 0.0 ? 1.0 : State.SectorVar[sec];
                sv = sectorOmega + MarketCatalogue.SectorGarchA * sz * sz * sv + MarketCatalogue.SectorGarchB * sv;
                State.SectorVar[sec] = MarketValuation.Clamp(sv, MarketCatalogue.VarianceLeast, MarketCatalogue.VarianceMost);
                State.SectorShock[sec] = sz;
                State.SectorDayZ[sec] = 0.0;
            }
            State.DayZ = 0.0;
            State.DayVar = 0.0;
        }

        // Dividends

        private void Dividends(int day)
        {
            // Holders at today's close of a company whose record day it is are owed its dividend.
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (!s.Listed || s.RecordDay != day || s.DeclaredCents <= 0)
                {
                    continue;
                }
                Position p = State.Position(s.Id);
                if (p != null && p.Shares != 0)
                {
                    State.Dues.Add(new DividendDue { StockId = s.Id, Cents = p.Shares * s.DeclaredCents, PayDay = s.PayDay, Test = p.Test });
                }
                BotDues(s);
                // Paid out of its book; a player's company's out of its reserve.
                s.Equity -= s.Shares * s.DeclaredCents / 100.0;
                if (s.Own != null)
                {
                    ListingRules.PaidOut(s, s.DeclaredCents);
                }
            }
            for (int i = State.Dues.Count - 1; i >= 0; i--)
            {
                DividendDue due = State.Dues[i];
                if (due.PayDay > day)
                {
                    continue;
                }
                State.Dues.RemoveAt(i);
                PayDue(due, day);
            }
        }

        // A dividend owed, paid to the player (a bot's: into the bot's money too) or by them on a short; the share put back
        // is bought at the next open (MarketEngine.Invest.cs).
        private void PayDue(in DividendDue due, int day)
        {
            Stock s = State.Stock(due.StockId);
            if (due.Bot > 0)
            {
                BotDividend(due, s, day);
                return;
            }
            Position p = State.Position(due.StockId);
            if (due.Cents > 0)
            {
                long money = due.Cents / 100;   // rounded down
                if (money <= 0)
                {
                    return;
                }
                if (State.Account.Margin)
                {
                    State.Account.Cash += money;
                }
                else
                {
                    Wallet.Give(money, MarketMoney.Dividend, s != null ? s.Name : string.Empty);
                }
                State.Account.Dividends += money;
                if (p != null)
                {
                    p.Dividends += money;
                }
                MarketTrading.AddTrade(State, new Trade { Day = day, Phase = MarketCatalogue.PhaseClose, StockId = due.StockId, Kind = TradeKind.Dividend, Value = money, Test = due.Test });
                if (!due.Test)
                {
                    Say(new MarketNews { Day = day, Kind = MarketNewsKind.Paid, Stock = due.StockId, A = (int)Math.Min(int.MaxValue, money) });   // the player's own news
                }
                _world.Paid(TradeKind.Dividend, s, money);
                PutBack(s, p, money, day, due.Test);
            }
            else if (due.Cents < 0)
            {
                long money = (-due.Cents + 99) / 100;   // rounded up: what a short owes the lender
                State.Account.Cash -= money;
                State.Account.DividendsPaid += money;
                if (p != null)
                {
                    p.Dividends -= money;
                }
                MarketTrading.AddTrade(State, new Trade { Day = day, Phase = MarketCatalogue.PhaseClose, StockId = due.StockId, Kind = TradeKind.DividendPaid, Value = -money, Test = due.Test });
                _world.Paid(TradeKind.DividendPaid, s, money);
            }
        }

        // The companies' lives

        private void Corporate(int day)
        {
            double year = TradingYear;
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (!s.Listed)
                {
                    continue;
                }
                if (s.Own != null)
                {
                    OwnCorporate(s, day);   // its meetings, investors and a bid for it
                    continue;
                }
                bool holding = s.Def != null && s.Def.Holding;   // a rival tycoon's leaves only with its tycoon
                // A bid due today completes or falls through, decided now.
                if (s.BidCents > 0 && s.BidEndDay <= day)
                {
                    if (MarketRandom.Unit(_secret, MarketRandom.Takeover, s.Id, 1) < MarketCatalogue.TakeoverFailChance)
                    {
                        // Back to what it is worth on its own, from the next open.
                        s.BidCents = 0;
                        s.BidEndDay = 0;
                        s.BidDay = 0;
                        Say(new MarketNews { Day = day, Kind = MarketNewsKind.BidFailed, Stock = s.Id });
                    }
                    else
                    {
                        Leave(s, day, GoneReason.TakenOver, s.BidCents);
                        continue;
                    }
                }
                // Shares that have traded under a unit for weeks leave the market at their last price.
                s.LowDays = s.Last * 1000L < (long)MarketCatalogue.PennyCents * Math.Max(100, State.Env.PriceLevel) ? s.LowDays + 1 : 0;
                if (s.LowDays >= MarketCatalogue.PennyDays && !holding)
                {
                    Leave(s, day, GoneReason.Delisted, s.Last);
                    continue;
                }
                // A company in distress may fail; the market prices the chance.
                double hazard = MarketValuation.Hazard(s.Distress);
                if (!holding && hazard > 0.0 && MarketRandom.Unit(_secret, MarketRandom.Failure, s.Id, 0) < hazard / year)
                {
                    int payout = 0;
                    if (MarketRandom.Unit(_secret, MarketRandom.Recovery, s.Id, 0) < MarketCatalogue.RecoveryChance)
                    {
                        payout = (int)(s.Last * MarketCatalogue.RecoveryMost * (1.0 - s.Distress) * MarketRandom.Unit(_secret, MarketRandom.Recovery, s.Id, 1));
                    }
                    Leave(s, day, GoneReason.Failed, payout);
                    continue;
                }
                // A crisis or a windfall of its own, priced too (MarketValuation.OwnNewsLoss).
                if (s.BidCents <= 0)
                {
                    double scale = MarketCatalogue.CrisisSizeScale[(int)s.Size] / year;
                    double draw = MarketRandom.Unit(_secret, MarketRandom.Crisis, s.Id, 0);
                    if (draw < MarketCatalogue.CrisisChanceYear * scale)
                    {
                        CompanyNews(s, day, true, false);
                    }
                    else if (draw < (MarketCatalogue.CrisisChanceYear + MarketCatalogue.WindfallChanceYear) * scale)
                    {
                        CompanyNews(s, day, false, false);
                    }
                }
                // A bid for it, likelier for small companies (MarketValuation.TakeoverYield).
                if (!holding && s.BidCents <= 0 && day - s.ListedDay > 10 && MarketValuation.TakeoverYield(s) > 0.0)
                {
                    double chance = MarketCatalogue.TakeoverChanceYear / year * MarketCatalogue.TakeoverSizeScale[(int)s.Size];
                    if (MarketRandom.Unit(_secret, MarketRandom.Takeover, s.Id, 0) < chance)
                    {
                        Bid(s, day);
                    }
                }
                // A board splits its shares when they trade high.
                if (s.SplitDay <= 0 && s.BidCents <= 0 && s.Last > MarketCatalogue.SplitAboveCents)
                {
                    s.SplitRatio = MarketLife.SplitRatioAt(s.Last);
                    s.SplitDay = MarketTrading.MarketDays(day, MarketCatalogue.SplitNoticeDays);
                    Say(new MarketNews { Day = day, Kind = MarketNewsKind.Split, Stock = s.Id, A = s.SplitRatio, B = s.SplitDay });
                }
            }
            // A new listing announced while the pipeline has a company.
            if (State.ListingId <= 0 && MarketRandom.Unit(_secret, MarketRandom.Listing, 0, 0) < MarketCatalogue.ListingChanceYear / year && State.Stocks.Count < MarketCatalogue.CompaniesMost)
            {
                CompanyDef next = NextListing(day);
                if (next != null)
                {
                    State.ListingId = next.Id;
                    State.ListingDay = MarketTrading.MarketDays(day, MarketCatalogue.ListingNoticeDays);
                    Say(new MarketNews { Day = day, Kind = MarketNewsKind.Listing, Stock = next.Id, A = 0, B = State.ListingDay });
                }
            }
        }

        // A company's own big news after the close: its usual margin moves for good, its price at the next open.
        private void CompanyNews(Stock s, int day, bool crisis, bool session)
        {
            MarketEnv env = State.Env;
            double u = MarketRandom.Unit(_secret, MarketRandom.CrisisTerms, s.Id, 0);
            double usual = Math.Max(0.02, s.Def.MarginBp / 10000.0);
            double hit = (crisis ? -(MarketCatalogue.CrisisMarginLeast + (MarketCatalogue.CrisisMarginMost - MarketCatalogue.CrisisMarginLeast) * u)
                                 : MarketCatalogue.WindfallMarginLeast + (MarketCatalogue.WindfallMarginMost - MarketCatalogue.WindfallMarginLeast) * u) * usual;
            double before = MarketValuation.Fair(s, env);
            double shift = MarketValuation.Clamp(s.MarginShift + hit, -2.0 * usual, usual + 0.05);
            double change = shift - s.MarginShift;
            s.MarginShift = shift;
            s.Margin = MarketValuation.Clamp(s.Margin + change, -0.5, 0.8);
            s.MarginTarget += change;
            s.Distress = MarketValuation.DistressOf(s, env);
            double after = MarketValuation.Fair(s, env);
            // Priced at once: what the shares are worth now, traded from the next open.
            s.LogFair = MarketLife.LogFairOf(s, env, State.Day, Year);
            if (session)
            {
                s.Last = s.MidCents;
            }
            s.ResultsBoostDay = MarketTrading.NextTradingDay(day);   // its own moves are larger the day after, as on a results day
            Say(new MarketNews { Day = day, Kind = crisis ? MarketNewsKind.Crisis : MarketNewsKind.Windfall, Stock = s.Id, A = (int)Math.Round(Math.Abs(after / before - 1.0) * 10000.0) });
        }

        // A takeover bid at a premium, completing in some weeks; from the next open the price is the bid less the chance it
        // falls through, and the mood no longer counts.
        private void Bid(Stock s, int day)
        {
            double premium = MarketCatalogue.TakeoverPremiumLeast
                             + (MarketCatalogue.TakeoverPremiumMost - MarketCatalogue.TakeoverPremiumLeast) * MarketRandom.Unit(_secret, MarketRandom.TakeoverTerms, s.Id, 0);
            s.BidCents = Math.Max(1, (int)Math.Round(s.Mid * (1.0 + premium) * 100.0));
            s.Mis -= s.Sent;
            s.Sent = 0.0;
            s.BidDay = day;
            s.BidEndDay = MarketTrading.MarketDays(day, MarketCatalogue.TakeoverDaysLeast + MarketRandom.Below(_secret, MarketRandom.TakeoverTerms, s.Id, 1,
                MarketCatalogue.TakeoverDaysMost - MarketCatalogue.TakeoverDaysLeast + 1));
            // The bidder: a larger company of the same sector if there is one; it pays a premium and its price eases.
            _scratch.Clear();
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock other = State.Stocks[i];
                if (other.Listed && other != s && other.Sector == s.Sector && other.MarketValue > s.MarketValue * 3.0 && other.BidCents <= 0 && (other.Def == null || !other.Def.Holding))
                {
                    _scratch.Add(other);
                }
            }
            s.BidderId = 0;
            if (_scratch.Count > 0 && MarketRandom.Unit(_secret, MarketRandom.Bidder, s.Id, 0) < 0.7)
            {
                Stock bidder = _scratch[MarketRandom.Below(_secret, MarketRandom.Bidder, s.Id, 1, _scratch.Count)];
                s.BidderId = bidder.Id;
                bidder.Outlook -= 0.02;   // the premium it pays, from the next open
            }
            _scratch.Clear();
            Say(new MarketNews { Day = day, Kind = MarketNewsKind.Bid, Stock = s.Id, A = s.BidCents, B = s.BidEndDay, C = s.BidderId });
        }

        /// <summary>A takeover bid for a company now (the test tools); from the next open its price is what the bid is worth.</summary>
        public bool BidFor(int stockId)
        {
            Finish();   // Midway
            Stock s = State.Stock(stockId);
            if (s == null || !s.Listed || s.BidCents > 0)
            {
                return false;
            }
            if (s.Own != null)
            {
                OwnBid(s, State.Day);   // a player's company: its own bid, decided by its founder's holding
                return true;
            }
            Bid(s, State.Day);
            return true;
        }

        /// <summary>A crisis or a windfall at a company now (the test tools); its price follows at the next open.</summary>
        public bool NewsFor(int stockId, bool crisis)
        {
            Finish();   // Midway
            Stock s = State.Stock(stockId);
            if (s == null || !s.Listed || s.BidCents > 0 || s.Own != null)   // a player's company's news is its books
            {
                return false;
            }
            CompanyNews(s, State.Day, crisis, Trading);
            return true;
        }

        /// <summary>A company leaves the market now (the test tools): failed paying <paramref name="cents"/> a share, or taken over at it.</summary>
        public bool Delist(int stockId, GoneReason reason, int cents)
        {
            Finish();   // Midway
            Stock s = State.Stock(stockId);
            if (s == null || !s.Listed)
            {
                return false;
            }
            Leave(s, State.Day, reason, cents);
            return true;
        }

        // A company leaves the market: what is left is paid to its holders (a short closed at the same price) and its orders
        // go. The news is the reason's, or the one given.
        private void Leave(Stock s, int day, GoneReason reason, int centsEach, MarketNewsKind kind = MarketNewsKind.Count)
        {
            Reweigh(s, false);
            s.Status = StockStatus.Gone;
            s.GoneDay = day;
            s.Reason = reason;
            s.GoneCents = Math.Max(0, centsEach);
            s.BidCents = 0;
            s.SplitDay = 0;
            Position p = State.Position(s.Id);
            if (p != null && p.Shares != 0)
            {
                long held = p.Shares;
                long money = MarketTrading.Value(Math.Abs(held), s.GoneCents, held < 0);
                if (held > 0)
                {
                    if (State.Account.Margin)
                    {
                        State.Account.Cash += money;
                    }
                    else if (money > 0)
                    {
                        Wallet.Give(money, MarketMoney.Payout, s.Name);
                    }
                    MarketTrading.Book(p, -held, money, State.Account);
                }
                else
                {
                    State.Account.Cash -= money;
                    MarketTrading.Book(p, -held, money, State.Account);
                }
                State.Account.Payouts += held > 0 ? money : -money;
                MarketTrading.AddTrade(State, new Trade { Day = day, Phase = MarketCatalogue.PhaseClose, StockId = s.Id, Kind = TradeKind.Payout, Qty = Math.Abs(held), PriceCents = s.GoneCents,
                    Value = held > 0 ? money : -money, Test = p.Test });
                State.Positions.Remove(p);
                _world.Paid(TradeKind.Payout, s, held > 0 ? money : -money);
            }
            BotsLeave(s, day);
            for (int i = 0; i < State.Orders.Count; i++)
            {
                Order o = State.Orders[i];
                if (o.StockId == s.Id && o.IsOpen)
                {
                    o.State = OrderState.Cancelled;
                    o.Why = OrderWhy.CompanyGone;
                    if (o.Worked)
                    {
                        o.EndDay = day;
                        Settle(o, s.Name);   // its day's money back, its takings paid
                    }
                }
            }
            // Its history goes but for its last day: the save keeps no charts of companies that are gone.
            if (s.Daily.Count > 1)
            {
                s.Daily.RemoveRange(0, s.Daily.Count - 1);
            }
            s.Monthly.Clear();
            s.BarCount = s.LastBarCount = 0;
            // A dividend it already owes the player is still paid on its day: it is in the dues.
            if (kind == MarketNewsKind.Count)
            {
                kind = reason == GoneReason.TakenOver ? MarketNewsKind.BidDone : reason == GoneReason.Delisted ? MarketNewsKind.Delisted : MarketNewsKind.Failure;
            }
            Say(new MarketNews { Day = day, Kind = kind, Stock = s.Id, A = s.GoneCents });
        }

        // The next pipeline company; when it is empty, one gone two years or more may come back.
        private CompanyDef NextListing(int day)
        {
            int free = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                int pick = pass == 1 && free > 0 ? MarketRandom.Below(_secret, MarketRandom.ListingPick, 0, 0, free) : -1;
                int seen = 0;
                for (int i = 0; i < MarketCatalogue.All.Length; i++)
                {
                    CompanyDef d = MarketCatalogue.All[i];
                    Stock s = State.Stock(d.Id);
                    bool can = d.Holding ? false : d.Pipeline ? s == null : s != null && !s.Listed && day - s.GoneDay > 2 * Year;   // a rival's holding comes only with its tycoon
                    if (!can)
                    {
                        continue;
                    }
                    if (pass == 0)
                    {
                        free++;
                    }
                    else if (seen++ == pick)
                    {
                        return d;
                    }
                }
                if (free == 0)
                {
                    return null;
                }
            }
            return null;
        }

        // The listing announced comes to the market at its price, at today's price level.
        private void List(int day)
        {
            CompanyDef d = MarketCatalogue.Find(State.ListingId);
            State.ListingId = 0;
            State.ListingDay = 0;
            if (d == null)
            {
                return;
            }
            Stock old = State.Stock(d.Id);
            if (old != null)
            {
                State.Stocks.Remove(old);
            }
            Stock s = MarketLife.Make(d, null, day, State.Env, Year, _secret, 0);
            s.Daily.Clear();
            s.Monthly.Clear();
            Reweigh(s, true);   // before it is counted: the indices stay where they were
            State.Stocks.Add(s);
            Say(new MarketNews { Day = day, Kind = MarketNewsKind.Listing, Stock = s.Id, A = s.MidCents, B = day });
        }

        // News

        private void Say(in MarketNews news)
        {
            State.News.Add(news);
            if (State.News.Count > MarketCatalogue.NewsKept)
            {
                State.News.RemoveAt(0);
            }
            _world.Said(news);
        }

        // The day's close: the index, the biggest movers either way and the sector that moved most.
        private void CloseNews(int day)
        {
            int prev = State.IndexPrevClose > 0 ? State.IndexPrevClose : State.IndexOpen;
            int change = prev > 0 ? (int)((long)(State.IndexLast - prev) * 10000L / prev) : 0;
            Say(new MarketNews { Day = day, Kind = MarketNewsKind.Close, A = State.IndexLast, B = change });
            Stock up = null, down = null;
            int upBp = 0, downBp = 0;
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (!s.Listed || s.PrevClose <= 0 || s.ListedDay == day)
                {
                    continue;
                }
                int bp = MarketHistory.DayMoveBp(s);   // the app's own rule, so the movers are the ones it shows
                if (bp > upBp)
                {
                    upBp = bp;
                    up = s;
                }
                if (bp < downBp)
                {
                    downBp = bp;
                    down = s;
                }
            }
            if (up != null)
            {
                Say(new MarketNews { Day = day, Kind = MarketNewsKind.Mover, Stock = up.Id, A = upBp });
            }
            if (down != null)
            {
                Say(new MarketNews { Day = day, Kind = MarketNewsKind.Mover, Stock = down.Id, A = downBp });
            }
            int bestSector = -1, bestBp = 0;
            for (int sec = 0; sec < Sectors; sec++)
            {
                int before = State.SectorPrevClose[sec] > 0 ? State.SectorPrevClose[sec] : _sectorOpen[sec];
                if (before <= 0)
                {
                    continue;
                }
                int bp = (int)((long)(State.SectorLast[sec] - before) * 10000L / before);
                if (Math.Abs(bp) > Math.Abs(bestBp))
                {
                    bestBp = bp;
                    bestSector = sec;
                }
            }
            if (bestSector >= 0)
            {
                Say(new MarketNews { Day = day, Kind = MarketNewsKind.Sector, A = bestSector, B = bestBp });
            }
        }
    }
}
