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
        Dividend = 7,    // a dividend on its shares paid to the player: Stock, A the money, taxed as the player's own
        Dismissed = 8,   // let go: A its fee and share taken, B the holdings handed to the player
        Paid = 9         // a weekly payout to the player: A the money, B what it still owes (its money was in shares)
    }

    /// <summary>A bot's doing for the director to tell; what A to D hold depends on <see cref="Kind"/>.</summary>
    public struct BotNote
    {
        public BotNoteKind Kind;
        public int Firm;
        public int Stock;
        public long Qty;
        public int Cents;
        public long A, B, C;
        public long D;               // a statement: what it paid the player in the month
    }

    /// <summary>Why something asked of a bot cannot be done. Each has a sentence (reallife_mkt_bot_why_...).</summary>
    public enum BotWhy
    {
        None = 0, Closed, Held, Behind, Hired, Full, TooLittle, NoCash, NotHired, NotStopped, TooLittleLeft, Off, OwnShares, NotListed, Picks, NotRunning, ChangeTooLittle,
        TooMuchOut, Count
    }

    /// <summary>Part of the engine: the player's trading bots, their asks and their part of the open, steps and close.</summary>
    public sealed partial class MarketEngine
    {
        /// <summary>Told of what the player's bots do (the director's notifications). Null: nobody is told.</summary>
        public Action<BotNote> BotTold;

        private readonly MarketBots.Work _botWork = new MarketBots.Work();

        private void Tell(in BotNote note)
        {
            BotTold?.Invoke(note);
        }

        // Money at today's prices from a figure at the start's, by the least commission, which is one at the start.
        private int AtPrices(int atStart)
        {
            return Math.Max(1, (int)((long)atStart * _world.CommissionLeast / Math.Max(1, MarketCatalogue.CommissionLeastAtStart)));
        }

        /// <summary>The least a bot may be given now, at today's prices.</summary>
        public long BotCapitalLeast => AtPrices(MarketBots.CapitalLeastAtStart);

        // The player's asks

        /// <summary>
        /// A firm's bot hired with so much capital and its settings, each held to its choices, the capital out of the cash not
        /// already given to other bots (<see cref="BotCashFree"/>). Null with the reason when it cannot be.
        /// </summary>
        public Bot Hire(int firm, long capital, int size, int stop, int loss, out BotWhy why)
        {
            Finish();   // Midway
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
            else if (capital > BotCashFree)
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

        /// <summary>A bot's settings changed, each held to its choices; the stops of what it holds follow at once.</summary>
        public bool SetBot(int firm, int size, int stop, int loss, out BotWhy why)
        {
            Finish();   // Midway
            Bot b = Asked(firm, out why);
            if (b == null)
            {
                return false;
            }
            Set(b, size, stop, loss);
            return true;
        }

        /// <summary>The shares a bot trades: its strategy's market, the companies picked for it, or those the player holds.</summary>
        public bool SetScope(int firm, BotScope scope, out BotWhy why)
        {
            Finish();   // Midway
            Bot b = Asked(firm, out why);
            if (b == null)
            {
                return false;
            }
            b.Scope = (BotScope)Math.Max(0, Math.Min((int)BotScope.Held, (int)scope));
            Rethink(b);
            return true;
        }

        /// <summary>A company picked for a bot, or taken off its picks; false with the reason when not (None: nothing changed).</summary>
        public bool PickFor(int firm, int stockId, bool on, out BotWhy why)
        {
            Finish();   // Midway
            Bot b = Asked(firm, out why);
            if (b == null)
            {
                return false;
            }
            Stock s = State.Stock(stockId);
            if (on && (s == null || !s.Listed || (s.Own != null && !Mine(s))))
            {
                why = BotWhy.NotListed;
                return false;
            }
            if (!MarketBots.Pick(b, stockId, on))
            {
                why = on && b.Picks.Count >= MarketBots.PicksMost ? BotWhy.Picks : BotWhy.None;
                return false;
            }
            Rethink(b);
            return true;
        }

        /// <summary>A bot's payouts: the share of its new profit, held to its choices, and how often.</summary>
        public bool SetPayout(int firm, int pct, BotPayEvery every, out BotWhy why)
        {
            Finish();   // Midway
            Bot b = Asked(firm, out why);
            if (b == null)
            {
                return false;
            }
            b.PayPct = MarketBots.Choice(MarketBots.PayPcts, pct);
            b.PayEvery = (BotPayEvery)Math.Max(0, Math.Min((int)BotPayEvery.Monthly, (int)every));
            return true;
        }

        // A hired bot to change, or null with the reason.
        private Bot Asked(int firm, out BotWhy why)
        {
            Bot b = MarketBots.Hired(State, firm);
            why = b == null ? BotWhy.NotHired : Held ? BotWhy.Held : BotWhy.None;
            return why == BotWhy.None ? b : null;
        }

        // What it trades changed while the market is shut: it decides again on the last close.
        private void Rethink(Bot b)
        {
            if (!Trading && b.Status == BotStatus.Running)
            {
                MarketBots.Decide(State, b, State.Day, Year, _world.CommissionLeast, _botWork);
            }
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

        /// <summary>The cash a hire or capital added may use: what the market can afford less what the bots already have.</summary>
        public long BotCashFree => Math.Max(0L, Wallet.Cash - MarketBots.Committed(State));

        /// <summary>The least added to a running bot or taken out of it at a time, at today's prices.</summary>
        public long BotChangeLeast => AtPrices(MarketBots.ChangeLeastAtStart);

        /// <summary>
        /// More of the player's cash given to a running bot: nothing sold or moved, a line of history, put to work from its
        /// next decision. False with the reason: not running, under <see cref="BotChangeLeast"/>, or over <see cref="BotCashFree"/>.
        /// </summary>
        public bool AddCapital(int firm, long amount, out BotWhy why)
        {
            Finish();   // Midway
            Bot b = Asked(firm, out why);
            if (b != null)
            {
                why = b.Status != BotStatus.Running ? BotWhy.NotRunning : amount < BotChangeLeast ? BotWhy.ChangeTooLittle : amount > BotCashFree ? BotWhy.NoCash : BotWhy.None;
            }
            if (why != BotWhy.None)
            {
                return false;
            }
            MarketBots.Add(b, amount);
            MarketTrading.AddTrade(State, new Trade { Day = State.Day, Phase = State.Phase, Kind = TradeKind.BotAdded, Value = amount, Bot = firm });
            Rethink(b);
            return true;
        }

        /// <summary>The most that can be taken out of a hired bot now without selling anything; 0 for none.</summary>
        public long BotMostOut(int firm)
        {
            Bot b = MarketBots.Hired(State, firm);
            return b != null ? MarketBots.MostOut(State, b, BotCapitalLeast, State.Phase) : 0;
        }

        /// <summary>
        /// Capital taken out of a running bot off its money not in shares: nothing sold or moved, a line of history. False with
        /// the reason: not running, under <see cref="BotChangeLeast"/>, or over <see cref="BotMostOut"/>.
        /// </summary>
        public bool TakeCapital(int firm, long amount, out BotWhy why)
        {
            Finish();   // Midway
            Bot b = Asked(firm, out why);
            if (b != null)
            {
                why = b.Status != BotStatus.Running ? BotWhy.NotRunning : amount < BotChangeLeast ? BotWhy.ChangeTooLittle
                    : amount > MarketBots.MostOut(State, b, BotCapitalLeast, State.Phase) ? BotWhy.TooMuchOut : BotWhy.None;
            }
            if (why != BotWhy.None)
            {
                return false;
            }
            MarketBots.TakeOut(b, amount);
            MarketTrading.AddTrade(State, new Trade { Day = State.Day, Phase = State.Phase, Kind = TradeKind.BotTaken, Value = -amount, Bot = firm });
            Rethink(b);
            return true;
        }

        /// <summary>A bot asked to sell everything at the market's next step and stop.</summary>
        public bool SellUp(int firm, out BotWhy why)
        {
            Finish();   // Midway
            Bot b = Asked(firm, out why);
            if (b == null)
            {
                return false;
            }
            b.Pending.Clear();
            b.TopUp = 0;
            b.Status = b.Holdings.Count > 0 ? BotStatus.SellingUp : BotStatus.Stopped;
            return true;
        }

        /// <summary>A stopped bot started again with what it has now, the most it may lose reckoned from that.</summary>
        public bool Restart(int firm, out BotWhy why)
        {
            Finish();   // Midway
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
        /// A bot let go: its fee for the month's days and its share over the high-water mark taken, and its holdings handed
        /// to the player at cost. Not while it holds the player's own company's shares (<see cref="BotWhy.OwnShares"/>): they
        /// cannot sit beside the founder's, and handing them over would change the company on this machine alone.
        /// </summary>
        public bool Dismiss(int firm, out BotWhy why)
        {
            Finish();   // Midway
            Bot b = MarketBots.Hired(State, firm);
            why = b == null ? BotWhy.NotHired : Held ? BotWhy.Held : HoldsOwn(b) ? BotWhy.OwnShares : BotWhy.None;
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

        /// <summary>True while a bot holds shares of a player's company.</summary>
        public bool HoldsOwn(Bot b)
        {
            for (int i = 0; i < b.Holdings.Count; i++)
            {
                Stock s = State.Stock(b.Holdings[i].StockId);
                if (s != null && s.Own != null && b.Holdings[i].Shares > 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Every bot's month closed now as at a month's end (the test tools): statement, fee and share.</summary>
        public int BotMonthNow()
        {
            for (int i = 0; i < State.Bots.Count; i++)
            {
                Settle(State.Bots[i], State.Day, true);
            }
            return State.Bots.Count;
        }

        // On the market's clock

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

        // After a step: what was decided at the close (the first step), selling up, stops the step reached, and the floor.
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
                if (k == 1 && MarketBots.TopUpDue(b))
                {
                    MarketBots.TopUpTraded(b, b.ShortDay == day);   // once more the next market day if the cash was short of it
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

        // At the close, a piece of its own per bot: the month's statement at a month's end, the drawdown, and its decision for
        // the next session (it scores every company: tens of microseconds).
        private void BotClose(Bot b, int day)
        {
            if (b.Status != BotStatus.Stopped)
            {
                b.MonthDays++;   // a stopped bot costs nothing for those days
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
                Settle(b, day, true);   // its payout, whatever its time, with the statement
            }
            else if (b.PayEvery != BotPayEvery.Monthly && MarketBots.PayDue(b.PayEvery, day, Year))
            {
                long paid = MarketBots.Payout(b);
                if (paid > 0 && b.PayEvery == BotPayEvery.Weekly)
                {
                    Tell(new BotNote { Kind = BotNoteKind.Paid, Firm = b.Firm, A = paid, B = b.PayOwed });   // a daily payout is told in the month's statement
                }
            }
            b.LastDay = day;
            MarketBots.Decide(State, b, day, Year, _world.CommissionLeast, _botWork);
        }

        // The month's fee for the market days hired and the share of profit over the high-water mark, taken from the bot's
        // money and booked on the player's; with 'statement' the month is told and a new one begun. Returns what was taken.
        private long Settle(Bot b, int day, bool statement)
        {
            BotFirm f = b.Def;
            if (f == null)
            {
                return 0;
            }
            long worth = MarketBots.Worth(State, b, MarketCatalogue.PhaseClose);
            int days = MarketBots.MarketDaysIn(b.Month, Year);
            int hired = Math.Max(b.MonthDays, b.MonthTrades > 0 ? 1 : 0);   // a day it traded counts, close or not
            long full = MarketBots.Fee(f, MarketBots.FeeWorth(b, worth, hired), AtPrices(MarketBots.FeeLeastAtStart));   // capital added or taken out counts for its days
            long fee = (full * Math.Min(days, hired) + days - 1) / days;
            long share = 0;
            long after = worth - fee;
            if (after > b.HighWater)
            {
                share = ((after - b.HighWater) * f.ShareBp + 9999L) / 10000L;
                b.HighWater = after - share;
            }
            Wallet.Cover(fee + share);   // one give-back from worked orders' money for both
            if (fee > 0)
            {
                Wallet.Force(fee, MarketMoney.BotFee, f.Name);
            }
            if (share > 0)
            {
                Wallet.Force(share, MarketMoney.BotShare, f.Name);
            }
            b.Cash -= fee + share;
            b.Fees += fee + share;
            if (statement)
            {
                long paid = MarketBots.Payout(b);   // after the fee and share: what is paid out is what it kept
                long now = worth - fee - share - paid;
                int bp = b.MonthWorth > 0 ? (int)Math.Max(-1000000L, Math.Min(1000000L, (now - b.MonthWorth) * 10000L / b.MonthWorth)) : 0;
                Tell(new BotNote { Kind = BotNoteKind.Statement, Firm = b.Firm, A = now, B = bp, C = fee + share, Qty = b.MonthTrades, D = b.MonthPaid });
                b.MonthWorth = now;
                b.MonthPaid = 0;
                b.MonthTrades = 0;
                b.MonthDays = 0;
                b.MonthAdded = 0;
                b.Month = MarketCatalogue.MonthOf(MarketTrading.NextTradingDay(day), Year);
            }
            return fee + share;
        }

        // What was decided at the close: sales first, then purchases out of the bot's money, at the market's price now.
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
                if (s == null || f == null || !MarketBots.Allowed(State, b, s, day) || (b.Holding(s.Id) != null && !MarketBots.TopUpDue(b)))
                {
                    // More of what it holds only when capital added was decided on (MarketBots.Decide).
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
                // The player's own company: no more than its day's limit at a time, at the price of what went.
                BotHolding left = b.Holding(s.Id);
                Trade t = State.Trades.Count > 0 ? State.Trades[State.Trades.Count - 1] : null;
                Tell(new BotNote { Kind = BotNoteKind.Stop, Firm = b.Firm, Stock = s.Id, Qty = qty - (left != null ? left.Shares : 0), Cents = t != null ? t.PriceCents : cents, A = stop });
            }
        }

        // Under its floor: everything sold at the market now and the bot stops; a gap can take it further under.
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
            b.TopUp = 0;
            b.Status = b.Holdings.Count > 0 ? BotStatus.SellingUp : BotStatus.Stopped;   // the player's company's shares it may not sell yet go when they may
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

        // A bot's trade; its lasting move of the price is shared as the player's own trades' are.
        private bool BotFillNow(Bot b, Stock s, bool buying, long qty, int cents, int phase, out bool shortOfCash)
        {
            shortOfCash = false;
            if (s.Own != null)
            {
                // The player's own company: within its founder's rules and a share of its day's volume.
                long room = MarketBots.OwnRoom(State, s, buying, State.Day, Year);
                if (room <= 0)
                {
                    return false;
                }
                if (qty > room)
                {
                    qty = room;
                    cents = MarketTrading.MarketPrice(State, s, buying, qty, phase);
                }
            }
            bool city = TradesAsEvents || TradeSink != null || (Replaying && Tape != null && Tape.Traded(State.Day));
            if (!MarketBots.Fill(State, b, s, buying, qty, cents, _world.CommissionLeast, Wallet, phase, !city, out double kept, out shortOfCash))
            {
                return false;
            }
            if (s.Own != null)
            {
                MarketBots.OwnCount(b, s, buying, qty, State.Day, Year);
            }
            if (city)
            {
                Shared(s, kept, qty);
            }
            s.Last = s.MidCents;
            return true;
        }

        // Dividends and companies leaving

        // At the close of a record day: what the bots' shares are owed, paid to the player on its day and into the bot.
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
            Wallet.Give(money, MarketMoney.Dividend, (s != null ? s.Name : string.Empty) + (f != null ? " (" + f.Name + ")" : string.Empty));
            if (b != null)
            {
                b.Cash += money;
                b.Dividends += money;
            }
            MarketTrading.AddTrade(State, new Trade { Day = day, Phase = MarketCatalogue.PhaseClose, StockId = due.StockId, Kind = TradeKind.Dividend, Value = money, Bot = due.Bot });
            // News for this player alone, never in the city's line, and the director's notification.
            Say(new MarketNews { Day = day, Kind = MarketNewsKind.Paid, Stock = due.StockId, A = (int)Math.Min(int.MaxValue, money), B = due.Bot });
            Tell(new BotNote { Kind = BotNoteKind.Dividend, Firm = due.Bot, Stock = due.StockId, A = money });
        }

        // A company leaves: the bots' holdings of it are paid out at its price.
        private void BotsLeave(Stock s, int day)
        {
            for (int i = 0; i < State.Bots.Count; i++)
            {
                Bot b = State.Bots[i];
                b.Pending.RemoveAll(o => o.StockId == s.Id);
                b.Picks.Remove(s.Id);
                BotHolding h = b.Holding(s.Id);
                if (h == null)
                {
                    continue;
                }
                long money = MarketTrading.Value(h.Shares, s.GoneCents, false);
                if (money > 0)
                {
                    Wallet.Give(money, MarketMoney.Payout, s.Name + " (" + (b.Def != null ? b.Def.Name : string.Empty) + ")");
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
