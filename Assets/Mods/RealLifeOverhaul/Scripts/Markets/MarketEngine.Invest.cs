using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>What a worked order did that the player is told of (MarketDirector.Invest.cs). Never saved.</summary>
    public enum InvestNoteKind
    {
        Day = 0,         // its slices of a market day, at the close: Qty the shares and Value their money; Done of Total so far
        Done = 1,        // all of it filled: Total the shares, Filled their money
        Stopped = 2      // stopped before it was done (the cash, the holding limit, the shares held, the margin): Why, Done of Total
    }

    public struct InvestNote
    {
        public InvestNoteKind Kind;
        public int Order;
        public int Stock;
        public bool Buy;
        public long Qty, Value;      // the day's shares and money (Day)
        public long Done, Total;     // shares filled so far, and the order's size
        public long Filled;          // the money of all it filled (its average price: Filled * 100 / Done cents)
        public OrderWhy Why;
        public int SlicePct;         // the day's largest slice against its step's trading, per cent (the self-check)
    }

    /// <summary>
    /// A large investor's orders on the market's clock (InvestRules):
    ///
    ///   worked orders      placed for more shares than one order may be for: at each step of the session, after the resting
    ///                      orders, each takes a slice of the step's own trading (a fifth at most) at the market's price of
    ///                      the moment with its impact, the least commission only on its first slice (it is one order),
    ///                      until it is done, the cash runs out (in a margin account, its margin), the holding limit is
    ///                      reached (a sale: the shares held run out; it never sells short), or it is cancelled. Its day
    ///                      is told at the close; it never expires. One history line a day holds its slices.
    ///   dividends put back a dividend paid to a holding whose share to put back is set: that share of it after tax
    ///                      becomes an order at the market for the next open, for as many shares as it buys then.
    ///
    /// Both are the player's own orders and fill as every order does: their moves of the price go the way a fill's go (in a
    /// Going Public session to the host, as events of its tape), nothing new is sent, and they live in the save's part g
    /// (MarketStore). Game-free; the same on every machine and whether a moment is played in one frame or in pieces (each is
    /// inside one piece: a step, the open's end or the close's books). The cost: a look at each open order at each step,
    /// a few microseconds for sixty, within the step's own work; nothing per frame.
    /// </summary>
    public sealed partial class MarketEngine
    {
        /// <summary>Told of what worked orders do (the director's notifications). Null: nobody is told.</summary>
        public Action<InvestNote> InvestTold;

        /// <summary>Worked orders work (Settings.MarketsLarge, set by the director); off, none is placed or takes a slice.</summary>
        public bool WorkedOn = true;

        /// <summary>Dividends are put back (Settings.MarketsReinvest, set by the director); off, every one is paid in cash.</summary>
        public bool ReinvestOn = true;

        // ---------------------------------------------------------------- the player's asks

        /// <summary>
        /// A worked order placed: to buy or sell so many shares at the market, a slice at each step from the next one. Null
        /// with the reason when it cannot be (<see cref="WorkedWhy"/>).
        /// </summary>
        public Order PlaceWorked(int stockId, OrderSide side, long qty, bool held, bool test, out OrderWhy why)
        {
            Finish();   // (a moment half played is played out first: Midway)
            Stock s = State.Stock(stockId);
            why = WorkedWhy(s, side, qty, held);
            if (why != OrderWhy.None)
            {
                return null;
            }
            var o = new Order
            {
                Id = State.NextOrderId++, StockId = s.Id, Side = side, Type = OrderType.Market, Life = OrderLife.UntilDone, Qty = qty, PlacedDay = State.Day, PlacedPhase = State.Phase,
                State = OrderState.Open, Test = test, Worked = true
            };
            State.Orders.Add(o);
            return o;
        }

        /// <summary>
        /// Whether a worked order can be placed now (nothing is changed): checked as an order at the market is, but for its
        /// size. A sale is only of shares held and not already being sold, never short; a purchase needs room under the
        /// holding limit (it stops there) and, from cash, the price of one share at least (it buys what the cash pays for).
        /// </summary>
        public OrderWhy WorkedWhy(Stock s, OrderSide side, long qty, bool held)
        {
            if (held)
            {
                return OrderWhy.Held;
            }
            if (!WorkedOn)
            {
                return OrderWhy.Switched;
            }
            if (s == null || !s.Listed)
            {
                return OrderWhy.NotListed;
            }
            if (qty <= 0)
            {
                return OrderWhy.BadPrice;
            }
            if (MarketTrading.OpenOrders(State) >= MarketCatalogue.OrdersMost)
            {
                return OrderWhy.TooMany;
            }
            long have = State.Held(s.Id);
            if (side == OrderSide.Sell)
            {
                return qty > Math.Max(0L, have) - MarketTrading.Reserved(State, s.Id, OrderSide.Sell) ? OrderWhy.NoShares : OrderWhy.None;
            }
            if (have + MarketTrading.Reserved(State, s.Id, OrderSide.Buy) >= MarketTrading.MostHeld(s))
            {
                return OrderWhy.HoldingLimit;
            }
            if (!State.Account.Margin)
            {
                Quote q = MarketTrading.QuoteOf(State, s, Trading ? State.Phase : MarketCatalogue.PhaseOpen);
                if (InvestRules.SharesFor(_world.Wallet.Cash, q.Ask, _world.CommissionLeast) <= 0)
                {
                    return OrderWhy.NoMoney;
                }
            }
            return OrderWhy.None;
        }

        /// <summary>The share of each dividend a holding of shares puts back, from its next one on (held to the choices). False with the reason when it cannot be.</summary>
        public bool SetReinvest(int stockId, int pct, out OrderWhy why)
        {
            Finish();   // (a moment half played is played out first: Midway)
            Position p = State.Position(stockId);
            why = p == null || p.Shares <= 0 ? OrderWhy.NoShares : Held ? OrderWhy.Held : OrderWhy.None;
            if (why != OrderWhy.None)
            {
                return false;
            }
            p.Reinvest = InvestRules.ReinvestOf(Math.Max(0, Math.Min(100, pct)));
            return true;
        }

        /// <summary>
        /// Every open worked order (or with 'back', every payout waiting to be put back) cancelled with a reason: their option
        /// was switched off. What they filled is kept. How many.
        /// </summary>
        public int StopAllOf(bool back, OrderWhy why)
        {
            Finish();   // (a moment half played is played out first: Midway)
            int stopped = 0;
            for (int i = 0; i < State.Orders.Count; i++)
            {
                Order o = State.Orders[i];
                if (o.IsOpen && (back ? o.Back : o.Worked))
                {
                    o.State = o.Filled > 0 ? OrderState.Cancelled : OrderState.Refused;
                    o.Why = why;
                    o.EndDay = State.Day;
                    stopped++;
                }
            }
            return stopped;
        }

        // ---------------------------------------------------------------- worked orders, on the market's clock

        // After a step: each open worked order's slice of the step's trading.
        private void WorkedOrders(int day, int k)
        {
            if (!WorkedOn)
            {
                return;
            }
            int phase = MarketCatalogue.PhaseOpen + k;
            for (int i = 0; i < State.Orders.Count; i++)
            {
                Order o = State.Orders[i];
                if (!o.IsOpen || !o.Worked)
                {
                    continue;
                }
                Stock s = State.Stock(o.StockId);
                if (s == null || !s.Listed || s.BarCount <= 0)
                {
                    continue;
                }
                long volume = s.Bars[s.BarCount - 1].V;   // (the step's own trading: the bar this step has just made)
                long qty = InvestRules.Slice(o.Left, volume);
                if (qty > 0)
                {
                    Work(o, s, qty, volume, day, phase);
                }
            }
        }

        // One slice: held to the holding limit (a sale: to the shares held), to what the cash pays for, at the market's price
        // now with its move; the least commission only on the order's first slice. Stopped when nothing of it can be done.
        private void Work(Order o, Stock s, long qty, long volume, int day, int phase)
        {
            bool buying = o.Side == OrderSide.Buy;
            long have = State.Held(s.Id);
            if (buying)
            {
                long room = MarketTrading.MostHeld(s) - have;
                if (room <= 0)
                {
                    Stop(o, s, OrderWhy.HoldingLimit, day);
                    return;
                }
                qty = Math.Min(qty, room);
            }
            else
            {
                if (have <= 0)
                {
                    Stop(o, s, OrderWhy.NoShares, day);
                    return;
                }
                qty = Math.Min(qty, have);
            }
            int least = o.Filled > 0 ? 0 : _world.CommissionLeast;
            int cents = MarketTrading.MarketPrice(State, s, buying, qty, phase);
            if (buying && !State.Account.Margin)
            {
                long afford = InvestRules.SharesFor(_world.Wallet.Cash, cents, least);
                if (afford < qty)
                {
                    qty = afford;
                    if (qty <= 0)
                    {
                        Stop(o, s, OrderWhy.NoMoney, day);
                        return;
                    }
                    cents = MarketTrading.MarketPrice(State, s, true, qty, phase);   // (fewer shares move it less: never dearer)
                }
            }
            long valueBefore = o.FilledValue;
            if (!FillNow(s, o, qty, cents, phase, least))
            {
                o.EndDay = day;   // (FillNow has stopped it: the margin, or what the cash or the shares came to)
                Tell(o, s, InvestNoteKind.Stopped, day);
                return;
            }
            if (o.DayOf != day)
            {
                o.DayOf = day;
                o.DayQty = o.DayValue = 0;
                o.SlicePct = 0;
            }
            o.DayQty += qty;
            o.DayValue += o.FilledValue - valueBefore;
            o.SlicePct = Math.Max(o.SlicePct, volume > 0 ? (int)Math.Min(1000L, qty * 100L / volume) : 0);
            if (o.Left <= 0)
            {
                o.EndDay = day;
                Tell(o, s, InvestNoteKind.Done, day);
            }
        }

        private void Stop(Order o, Stock s, OrderWhy why, int day)
        {
            o.State = o.Filled > 0 ? OrderState.Cancelled : OrderState.Refused;
            o.Why = why;
            o.EndDay = day;
            Tell(o, s, InvestNoteKind.Stopped, day);
        }

        private void Tell(Order o, Stock s, InvestNoteKind kind, int day)
        {
            InvestTold?.Invoke(new InvestNote
            {
                Kind = kind, Order = o.Id, Stock = s.Id, Buy = o.Side == OrderSide.Buy, Qty = o.DayOf == day ? o.DayQty : 0, Value = o.DayOf == day ? o.DayValue : 0, Done = o.Filled,
                Total = o.Qty, Filled = o.FilledValue, Why = o.Why, SlicePct = o.DayOf == day ? o.SlicePct : 0
            });
        }

        // At the close: each worked order still open that worked today, told.
        private void WorkedClose(int day)
        {
            for (int i = 0; i < State.Orders.Count; i++)
            {
                Order o = State.Orders[i];
                if (o.IsOpen && o.Worked && o.DayOf == day && o.DayQty > 0)
                {
                    Stock s = State.Stock(o.StockId);
                    if (s != null)
                    {
                        Tell(o, s, InvestNoteKind.Day, day);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- dividends put back

        // A dividend paid into the player's cash on their own holding: the share it puts back (after tax) waits for the next
        // open as an order at the market. Nothing while the option is off, for a short or a holding gone, or for too little to
        // buy a share (it stays in the cash).
        private void PutBack(Stock s, Position p, long money, int day, bool test)
        {
            if (!ReinvestOn || s == null || !s.Listed || p == null || p.Shares <= 0 || p.Reinvest <= 0)
            {
                return;
            }
            long budget = InvestRules.PutBack(money, State.Env.Lever(MarketInputs.BusinessTaxLever), p.Reinvest);
            if (budget <= 0 || InvestRules.SharesFor(budget, MarketTrading.QuoteOf(State, s, MarketCatalogue.PhaseOpen).Ask, _world.CommissionLeast) <= 0
                || MarketTrading.OpenOrders(State) >= MarketCatalogue.OrdersMost)
            {
                return;
            }
            State.Orders.Add(new Order
            {
                Id = State.NextOrderId++, StockId = s.Id, Side = OrderSide.Buy, Type = OrderType.Market, Life = OrderLife.Day, Back = true, Budget = budget, PlacedDay = day,
                PlacedPhase = MarketCatalogue.PhaseClose, ExpireDay = MarketTrading.NextTradingDay(day), State = OrderState.Open, Test = test
            });
        }

        // A payout put back, filled now: as many shares as its money buys at the market's price, with its move and the
        // commission, as an order at the market for that many would be. Too little for a share now: cancelled, the money kept.
        private void BackNow(Order o, Stock s, int phase)
        {
            int least = _world.CommissionLeast;
            long most = InvestRules.SharesFor(o.Budget, MarketTrading.MarketPrice(State, s, true, 1, phase), least);
            long qty = most > 0 ? InvestRules.SharesFor(o.Budget, MarketTrading.MarketPrice(State, s, true, most, phase), least) : 0;   // (at the price of the most: never more than the money)
            if (qty <= 0)
            {
                o.State = OrderState.Cancelled;
                o.Why = OrderWhy.NoMoney;
                return;
            }
            o.Qty = qty;
            FillNow(s, o, qty, MarketTrading.MarketPrice(State, s, true, qty, phase), phase);
        }

        // ---------------------------------------------------------------- the test tools

        /// <summary>
        /// The test tools: a market day's slices of every open worked order played now, at the market's price of this moment
        /// (each step's usual trading, the recent trading fading between slices as between steps), and the day told. False
        /// while the market does not trade. How many orders worked.
        /// </summary>
        public int WorkDayNow()
        {
            Finish();   // (a moment half played is played out first: Midway)
            if (!Trading || Held)
            {
                return -1;
            }
            int worked = 0, day = State.Day;
            double decay = 1.0 - 1.0 / MarketCatalogue.ImpactMemorySteps;
            for (int i = 0; i < State.Orders.Count; i++)
            {
                Order o = State.Orders[i];
                Stock s = o.IsOpen && o.Worked ? State.Stock(o.StockId) : null;
                if (s == null || !s.Listed)
                {
                    continue;
                }
                worked++;
                for (int k = 1; k <= Steps && o.IsOpen; k++)
                {
                    long volume = (long)(s.Adv * _weight[k]);
                    long qty = InvestRules.Slice(o.Left, volume);
                    if (qty > 0)
                    {
                        Work(o, s, qty, volume, day, State.Phase);
                    }
                    if (s.Impact > 0.0)
                    {
                        s.Impact = s.Impact > 1.0 ? s.Impact * decay : 0.0;
                    }
                }
                if (o.IsOpen && o.DayOf == day && o.DayQty > 0)
                {
                    Tell(o, s, InvestNoteKind.Day, day);
                    o.DayQty = o.DayValue = 0;   // (told: the close tells only what comes after)
                }
            }
            return worked;
        }

        /// <summary>
        /// The test tools: the next dividend owed to the player on their own shares paid now, as on its day, and the share of
        /// it the holding puts back bought at once while the market trades (else at the next open). The company's id, or 0
        /// when none is owed.
        /// </summary>
        public int PayNextDueNow()
        {
            Finish();   // (a moment half played is played out first: Midway)
            int at = -1;
            for (int i = 0; i < State.Dues.Count; i++)
            {
                DividendDue due = State.Dues[i];
                if (due.Bot == 0 && due.Cents > 0 && (at < 0 || due.PayDay < State.Dues[at].PayDay))
                {
                    at = i;
                }
            }
            if (at < 0)
            {
                return 0;
            }
            DividendDue next = State.Dues[at];
            State.Dues.RemoveAt(at);
            int before = State.Orders.Count;
            PayDue(next, State.Day);
            for (int i = before; i < State.Orders.Count && Trading && !Held; i++)
            {
                Order o = State.Orders[i];
                Stock s = State.Stock(o.StockId);
                if (o.IsOpen && o.Back && s != null && s.Listed)
                {
                    BackNow(o, s, State.Phase);
                }
            }
            return next.StockId;
        }
    }
}
