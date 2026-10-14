using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>What a bot did that the player is told of (MarketDirector.Bots.cs). Never saved.</summary>
    public enum BotNoteKind
    {
        Round = 0,       // its trades at the session's first step: A bought, B sold, C its worth after
        Stop = 1,        // a holding sold at its stop: Stock, Qty, Cents the price it went at, A the stop
        Limit = 2,       // it lost the most it may: sold everything and stopped. A its worth after, B the floor
        Statement = 3,   // its month: A its worth, B the month's move in bp, C the fee and the share taken, Qty the trades
        Short = 4,       // a purchase it could not make: the player's cash was short of it. A the money it wanted
        SoldUp = 5,      // sold up as asked, and stopped: A its worth
        Payout = 6,      // a company it held left the market: Stock, A what its shares were paid
        Dividend = 7,    // a dividend on its shares paid to the player: Stock, A the money (taxed as the player's own)
        Dismissed = 8    // let go: A its fee and share taken, B the holdings handed to the player
    }

    public struct BotNote
    {
        public BotNoteKind Kind;
        public int Firm;
        public int Stock;
        public long Qty;
        public int Cents;
        public long A, B, C;
    }

    /// <summary>Why something asked of a bot cannot be done. Each has a sentence (reallife_mkt_bot_why_...).</summary>
    public enum BotWhy { None = 0, Closed, Held, Behind, Hired, Full, TooLittle, NoCash, NotHired, NotStopped, TooLittleLeft, Off, Count }

    public sealed partial class MarketEngine
    {
        /// <summary>Told of what the player's bots do (the director's notifications). Null: nobody is told.</summary>
        public Action<BotNote> BotTold;

        private readonly MarketBots.Work _botWork = new MarketBots.Work();

        private void Tell(in BotNote note)
        {
            BotTold?.Invoke(note);
        }

        // Money at today's prices from a figure at the start's: by the least commission, which is one.
        private int AtPrices(int atStart)
        {
            return Math.Max(1, (int)((long)atStart * _world.CommissionLeast / Math.Max(1, MarketCatalogue.CommissionLeastAtStart)));
        }

        /// <summary>The least a bot may be given now, at today's prices.</summary>
        public long BotCapitalLeast => AtPrices(MarketBots.CapitalLeastAtStart);

        // ---------------------------------------------------------------- the player's asks

        /// <summary>A firm's bot hired with so much capital and its settings (each held to its choices). Null with the reason when it cannot be.</summary>
        public Bot Hire(int firm, long capital, int size, int stop, int loss, out BotWhy why)
        {
            BotFirm f = MarketBots.Firm(firm);
            why = BotWhy.None;
            if (Held)
            {
                why = BotWhy.Held;
            }
            else if (f == null || MarketBots.Hired(State, firm) != null)
            {
                why = BotWhy.Hired;
            }
            else if (State.Bots.Count >= MarketBots.FirmsMost)
            {
                why = BotWhy.Full;
            }
            else if (capital < BotCapitalLeast)
            {
                why = BotWhy.TooLittle;
            }
            else if (capital > _world.Wallet.Cash)
            {
                why = BotWhy.NoCash;
            }
            if (why != BotWhy.None)
            {
                return null;
            }
            var b = new Bot
            {
                Firm = firm, Capital = capital, Base = capital, Cash = capital, Peak = capital, HighWater = capital, MonthWorth = capital, HiredDay = State.Day, LastDay = State.Day,
                Month = MarketCatalogue.MonthOf(MarketCatalogue.TradingDay(State.Day) ? State.Day : MarketTrading.NextTradingDay(State.Day), Year)
            };
            Set(b, size, stop, loss);
            State.Bots.Add(b);
            if (!Trading)
            {
                // Hired while the market is shut: it decides now on the last close, for the next session's first step.
                MarketBots.Decide(State, b, State.Day, Year, _world.CommissionLeast, _botWork);
            }
            return b;
        }

        /// <summary>A bot's settings changed (each held to its choices): the stops of what it holds follow at once.</summary>
        public bool SetBot(int firm, int size, int stop, int loss, out BotWhy why)
        {
            Bot b = MarketBots.Hired(State, firm);
            why = b == null ? BotWhy.NotHired : Held ? BotWhy.Held : BotWhy.None;
            if (why != BotWhy.None)
            {
                return false;
            }
            Set(b, size, stop, loss);
            return true;
        }

        private static void Set(Bot b, int size, int stop, int loss)
        {
            b.Size = MarketBots.Choice(MarketBots.Sizes, size);
            b.Stop = MarketBots.Choice(MarketBots.Stops, stop);
            b.Loss = MarketBots.Choice(MarketBots.Losses, loss);
            for (int i = 0; i < b.Holdings.Count; i++)
            {
                BotHolding h = b.Holdings[i];
                h.StopCents = MarketBots.StopFor(b, h.Shares, h.Cost);
            }
        }

        /// <summary>A bot asked to sell everything at the market's next step and stop.</summary>
        public bool SellUp(int firm, out BotWhy why)
        {
            Bot b = MarketBots.Hired(State, firm);
            why = b == null ? BotWhy.NotHired : Held ? BotWhy.Held : BotWhy.None;
            if (why != BotWhy.None)
            {
                return false;
            }
            b.Pending.Clear();
            b.Status = b.Holdings.Count > 0 ? BotStatus.SellingUp : BotStatus.Stopped;
            return true;
        }

        /// <summary>A stopped bot started again with what it has now, the most it may lose reckoned from that.</summary>
        public bool Restart(int firm, out BotWhy why)
        {
            Bot b = MarketBots.Hired(State, firm);
            why = b == null ? BotWhy.NotHired : Held ? BotWhy.Held : b.Status != BotStatus.Stopped || b.Holdings.Count > 0 ? BotWhy.NotStopped : BotWhy.None;
            if (why == BotWhy.None && b.Cash < BotCapitalLeast)
            {
                why = BotWhy.TooLittleLeft;
            }
            if (why != BotWhy.None)
            {
                return false;
            }
            b.Status = BotStatus.Running;
            b.Base = b.Cash;
            b.Peak = Math.Max(b.Peak, b.Cash);
            if (!Trading)
            {
                MarketBots.Decide(State, b, State.Day, Year, _world.CommissionLeast, _botWork);
            }
            return true;
        }

        /// <summary>
        /// A bot let go: its fee for the days of this month and its share of any profit over its high-water mark are
        /// taken, and what it holds becomes the player's own holding at what it cost (nothing is sold, nothing charged).
        /// </summary>
        public bool Dismiss(int firm, out BotWhy why)
        {
            Bot b = MarketBots.Hired(State, firm);
            why = b == null ? BotWhy.NotHired : Held ? BotWhy.Held : BotWhy.None;
            if (why != BotWhy.None)
            {
                return false;
            }
            long taken = Settle(b, State.Day, false);
            long handed = 0;
            for (int i = 0; i < b.Holdings.Count; i++)
            {
                BotHolding h = b.Holdings[i];
                if (h.Shares <= 0 || State.Stock(h.StockId) == null)
                {
                    continue;
                }
                Position p = State.Position(h.StockId);
                if (p == null)
                {
                    p = new Position { StockId = h.StockId, OpenedDay = h.OpenedDay };
                    State.Positions.Add(p);
                }
                MarketTrading.Book(p, h.Shares, h.Cost, State.Account);
                if (p.Shares == 0)
                {
                    State.Positions.Remove(p);
                }
                handed++;
            }
            State.Bots.Remove(b);
            Tell(new BotNote { Kind = BotNoteKind.Dismissed, Firm = firm, A = taken, B = handed });
            return true;
        }

        /// <summary>Every bot's month closed now, as at a month's end (the test tools): its statement, its fee and its share.</summary>
        public int BotMonthNow()
        {
            for (int i = 0; i < State.Bots.Count; i++)
            {
                Settle(State.Bots[i], State.Day, true);
            }
            return State.Bots.Count;
        }

        // ---------------------------------------------------------------- on the market's clock

        // At the open: a stop the night went through is sold at the open's price, and a bot under its floor sells everything.
        private void BotsOpen()
        {
            for (int i = 0; i < State.Bots.Count; i++)
            {
                Bot b = State.Bots[i];
                for (int j = b.Holdings.Count - 1; j >= 0; j--)
                {
                    BotHolding h = b.Holdings[j];
                    Stock s = State.Stock(h.StockId);
                    if (s != null && s.Listed && h.StopCents > 0 && MarketTrading.QuoteOf(State, s, MarketCatalogue.PhaseOpen).Bid <= h.StopCents)
                    {
                        BotStop(b, h, s, MarketCatalogue.PhaseOpen);
                    }
                }
                BotLimit(b, MarketCatalogue.PhaseOpen);
            }
        }

        // After a step: what was decided at the close (the first step), what is sold up, stops the step reached (sold at
        // the step's last price), and the floor.
        private void BotsStep(int day, int k)
        {
            int phase = MarketCatalogue.PhaseOpen + k;
            for (int i = 0; i < State.Bots.Count; i++)
            {
                Bot b = State.Bots[i];
                if (b.Status == BotStatus.SellingUp)
                {
                    SellAll(b, phase);
                    if (b.Holdings.Count == 0)
                    {
                        b.Status = BotStatus.Stopped;
                        Tell(new BotNote { Kind = BotNoteKind.SoldUp, Firm = b.Firm, A = b.Cash });
                    }
                    continue;
                }
                if (k == 1 && b.Pending.Count > 0 && b.Status == BotStatus.Running)
                {
                    Execute(b, day, phase);
                }
                for (int j = b.Holdings.Count - 1; j >= 0; j--)
                {
                    BotHolding h = b.Holdings[j];
                    Stock s = State.Stock(h.StockId);
                    if (s == null || !s.Listed || h.StopCents <= 0 || s.BarCount <= 0)
                    {
                        continue;
                    }
                    Candle bar = s.Bars[s.BarCount - 1];
                    double half = MarketTrading.HalfSpread(State, s, phase);
                    if ((int)Math.Floor(bar.L * (1.0 - half)) <= h.StopCents)
                    {
                        BotStop(b, h, s, phase);
                    }
                }
                BotLimit(b, phase);
            }
        }

        // At the close: the month's statement when the month ends, the drawdown, and what to do at the next session.
        private void BotsClose(int day)
        {
            for (int i = 0; i < State.Bots.Count; i++)
            {
                Bot b = State.Bots[i];
                if (b.Status != BotStatus.Stopped)
                {
                    b.MonthDays++;   // (a bot stopped costs nothing for the days it is stopped)
                }
                long worth = MarketBots.Worth(State, b, MarketCatalogue.PhaseClose);
                if (worth > b.Peak)
                {
                    b.Peak = worth;
                }
                else if (b.Peak > 0)
                {
                    b.DrawdownBp = Math.Max(b.DrawdownBp, (int)Math.Min(10000L, (b.Peak - worth) * 10000L / b.Peak));
                }
                if (MarketCatalogue.MonthOf(MarketTrading.NextTradingDay(day), Year) != MarketCatalogue.MonthOf(day, Year))
                {
                    Settle(b, day, true);
                }
                b.LastDay = day;
                MarketBots.Decide(State, b, day, Year, _world.CommissionLeast, _botWork);
            }
        }

        // The month's fee for the market days hired in it and the share of the profit over the high-water mark, taken
        // from the bot's money and booked on the player's; with 'statement' the month is told and a new one begun. What was taken.
        private long Settle(Bot b, int day, bool statement)
        {
            BotFirm f = b.Def;
            if (f == null)
            {
                return 0;
            }
            long worth = MarketBots.Worth(State, b, MarketCatalogue.PhaseClose);
            int days = MarketBots.MarketDaysIn(b.Month, Year);
            long full = MarketBots.Fee(f, worth, AtPrices(MarketBots.FeeLeastAtStart));
            int hired = Math.Max(b.MonthDays, b.MonthTrades > 0 ? 1 : 0);   // (a day it traded is a day, whether or not a close came after it)
            long fee = (full * Math.Min(days, hired) + days - 1) / days;
            long share = 0;
            long after = worth - fee;
            if (after > b.HighWater)
            {
                share = ((after - b.HighWater) * f.ShareBp + 9999L) / 10000L;
                b.HighWater = after - share;
            }
            if (fee > 0)
            {
                _world.Wallet.Force(fee, MarketMoney.BotFee, f.Name);
            }
            if (share > 0)
            {
                _world.Wallet.Force(share, MarketMoney.BotShare, f.Name);
            }
            b.Cash -= fee + share;
            b.Fees += fee + share;
            if (statement)
            {
                long now = worth - fee - share;
                int bp = b.MonthWorth > 0 ? (int)Math.Max(-1000000L, Math.Min(1000000L, (now - b.MonthWorth) * 10000L / b.MonthWorth)) : 0;
                Tell(new BotNote { Kind = BotNoteKind.Statement, Firm = b.Firm, A = now, B = bp, C = fee + share, Qty = b.MonthTrades });
                b.MonthWorth = now;
                b.MonthTrades = 0;
                b.MonthDays = 0;
                b.Month = MarketCatalogue.MonthOf(MarketTrading.NextTradingDay(day), Year);
            }
            return fee + share;
        }

        // What was decided at the close: sales first, then purchases out of the money the bot has, at the market's price now.
        private void Execute(Bot b, int day, int phase)
        {
            int bought = 0, sold = 0;
            BotFirm f = b.Def;
            for (int i = 0; i < b.Pending.Count; i++)
            {
                BotOrder o = b.Pending[i];
                BotHolding h = o.Buy ? null : b.Holding(o.StockId);
                Stock s = State.Stock(o.StockId);
                if (h == null || s == null || !s.Listed)
                {
                    continue;
                }
                long qty = Math.Min(o.Qty, h.Shares);
                if (BotFillNow(b, s, false, qty, MarketTrading.MarketPrice(State, s, false, qty, phase), phase))
                {
                    sold++;
                }
            }
            long tradeLeast = AtPrices(MarketBots.TradeLeastAtStart);
            for (int i = 0; i < b.Pending.Count; i++)
            {
                BotOrder o = b.Pending[i];
                Stock s = o.Buy ? State.Stock(o.StockId) : null;
                if (s == null || f == null || !MarketBots.Buyable(State, f, s, day) || b.Holding(s.Id) != null)
                {
                    continue;
                }
                long qty = Math.Min(o.Qty, MarketBots.Room(State, s));
                int cents = 0;
                for (int pass = 0; pass < 3 && qty > 0; pass++)
                {
                    cents = MarketTrading.MarketPrice(State, s, true, qty, phase);
                    long value = MarketTrading.Value(qty, cents, true);
                    long fee = MarketTrading.Commission(value, _world.CommissionLeast);
                    if (value + fee <= b.Cash)
                    {
                        break;
                    }
                    qty = Math.Min(qty - 1, (long)((b.Cash - fee) * 100.0 / cents));
                }
                if (qty <= 0 || MarketTrading.Value(qty, cents, true) < tradeLeast)
                {
                    continue;
                }
                if (BotFillNow(b, s, true, qty, cents, phase, out bool shortOfCash))
                {
                    bought++;
                }
                else if (shortOfCash)
                {
                    if (b.ShortDay != day)
                    {
                        b.ShortDay = day;
                        Tell(new BotNote { Kind = BotNoteKind.Short, Firm = b.Firm, Stock = s.Id, A = MarketTrading.Value(qty, cents, true) });
                    }
                    break;
                }
            }
            b.Pending.Clear();
            if (bought + sold > 0)
            {
                Tell(new BotNote { Kind = BotNoteKind.Round, Firm = b.Firm, A = bought, B = sold, C = MarketBots.Worth(State, b, phase) });
            }
        }

        // A holding sold at its stop: at the market's price now, which is under the stop when the price went through it.
        private void BotStop(Bot b, BotHolding h, Stock s, int phase)
        {
            long qty = h.Shares;
            int stop = h.StopCents;
            int cents = MarketTrading.MarketPrice(State, s, false, qty, phase);
            if (BotFillNow(b, s, false, qty, cents, phase))
            {
                Tell(new BotNote { Kind = BotNoteKind.Stop, Firm = b.Firm, Stock = s.Id, Qty = qty, Cents = cents, A = stop });
            }
        }

        // Under its floor: everything sold at the market now, and the bot stops. (A gap can take it further under than the floor.)
        private void BotLimit(Bot b, int phase)
        {
            if (b.Status != BotStatus.Running || b.Holdings.Count == 0)
            {
                return;
            }
            long floor = MarketBots.Floor(b);
            if (MarketBots.Worth(State, b, phase) > floor)
            {
                return;
            }
            SellAll(b, phase);
            b.Pending.Clear();
            b.Status = BotStatus.Stopped;
            Tell(new BotNote { Kind = BotNoteKind.Limit, Firm = b.Firm, A = MarketBots.Worth(State, b, phase), B = floor });
        }

        private void SellAll(Bot b, int phase)
        {
            for (int j = b.Holdings.Count - 1; j >= 0; j--)
            {
                BotHolding h = b.Holdings[j];
                Stock s = State.Stock(h.StockId);
                if (s != null && s.Listed)
                {
                    BotFillNow(b, s, false, h.Shares, MarketTrading.MarketPrice(State, s, false, h.Shares, phase), phase);
                }
            }
        }

        private bool BotFillNow(Bot b, Stock s, bool buying, long qty, int cents, int phase)
        {
            return BotFillNow(b, s, buying, qty, cents, phase, out _);
        }

        // A bot's trade, its lasting move of the price shared as the player's own trades' are.
        private bool BotFillNow(Bot b, Stock s, bool buying, long qty, int cents, int phase, out bool shortOfCash)
        {
            bool city = TradesAsEvents || TradeSink != null || (Replaying && Tape != null && Tape.Traded(State.Day));
            if (!MarketBots.Fill(State, b, s, buying, qty, cents, _world.CommissionLeast, _world.Wallet, phase, !city, out double kept, out shortOfCash))
            {
                return false;
            }
            if (city)
            {
                Shared(s, kept, qty);
            }
            s.Last = s.MidCents;
            return true;
        }

        // ---------------------------------------------------------------- dividends and companies leaving

        // At the close of a record day: what the bots' shares are owed, paid to the player on its day (and into the bot's money).
        private void BotDues(Stock s)
        {
            for (int i = 0; i < State.Bots.Count; i++)
            {
                BotHolding h = State.Bots[i].Holding(s.Id);
                if (h != null && h.Shares > 0)
                {
                    State.Dues.Add(new DividendDue { StockId = s.Id, Cents = h.Shares * s.DeclaredCents, PayDay = s.PayDay, Bot = State.Bots[i].Firm });
                }
            }
        }

        private void BotDividend(in DividendDue due, Stock s, int day)
        {
            long money = due.Cents / 100;   // rounded down
            if (money <= 0)
            {
                return;
            }
            Bot b = MarketBots.Hired(State, due.Bot);
            BotFirm f = MarketBots.Firm(due.Bot);
            _world.Wallet.Give(money, MarketMoney.Dividend, (s != null ? s.Name : string.Empty) + (f != null ? " (" + f.Name + ")" : string.Empty));
            if (b != null)
            {
                b.Cash += money;
                b.Dividends += money;
            }
            MarketTrading.AddTrade(State, new Trade { Day = day, Phase = MarketCatalogue.PhaseClose, StockId = due.StockId, Kind = TradeKind.Dividend, Value = money, Bot = due.Bot });
            Tell(new BotNote { Kind = BotNoteKind.Dividend, Firm = due.Bot, Stock = due.StockId, A = money });
        }

        // A company leaves: what the bots hold of it is paid out at its price, and nothing more is waited for in it.
        private void BotsLeave(Stock s, int day)
        {
            for (int i = 0; i < State.Bots.Count; i++)
            {
                Bot b = State.Bots[i];
                b.Pending.RemoveAll(o => o.StockId == s.Id);
                BotHolding h = b.Holding(s.Id);
                if (h == null)
                {
                    continue;
                }
                long money = MarketTrading.Value(h.Shares, s.GoneCents, false);
                if (money > 0)
                {
                    _world.Wallet.Give(money, MarketMoney.Payout, s.Name + " (" + (b.Def != null ? b.Def.Name : string.Empty) + ")");
                }
                b.Cash += money;
                long gain = money - h.Cost;
                b.Realised += gain;
                h.Gain += gain;
                b.Closed++;
                b.Wins += h.Gain > 0 ? 1 : 0;
                b.Holdings.Remove(h);
                MarketTrading.AddTrade(State, new Trade { Day = day, Phase = MarketCatalogue.PhaseClose, StockId = s.Id, Kind = TradeKind.Payout, Qty = h.Shares, PriceCents = s.GoneCents,
                    Value = money, Bot = b.Firm });
                Tell(new BotNote { Kind = BotNoteKind.Payout, Firm = b.Firm, Stock = s.Id, A = money });
            }
        }
    }
}
