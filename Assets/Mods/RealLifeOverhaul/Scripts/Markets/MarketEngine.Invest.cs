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
    ///                      reached (a sale: the shares held run out; it never sells short), or it is cancelled. What the
    ///                      open orders on the same side of the company placed before it are still for is theirs: while
    ///                      they hold the rest of the room, a step goes by and the order waits (never for one placed after
    ///                      it, so two worked orders never wait for each other). Its day is told at the close; it never expires.
    ///                      One history line a day holds its slices.
    ///   its money          on a cash account, settled once a day, never by the slice (each of the game's transactions is
    ///                      a sound, a line of the books and an event): a purchase holds its day's money from the cash in
    ///                      one transaction at its first slice of a market day (a day's most at the price of the moment,
    ///                      the commission and a tenth more, never more than the cash), its slices draw on it, and what is
    ///                      left is given back in one at the close or as it ends; short of it, the day's slices stop and
    ///                      the order goes on the next day. A sale's takings, after the commission, wait on it and are paid
    ///                      in one at the close or as it ends. A margin account moves no cash of the game's at all.
    ///   dividends put back a dividend paid to a holding whose share to put back is set: that share of it after tax
    ///                      becomes an order at the market for the next open, for as many shares as it buys then, one
    ///                      order's most at the most and never past the holding limit; what it cannot buy stays in the cash.
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

        /// <summary>
        /// Worked orders and larger bond purchases (Settings.MarketsLarge, set by the director): off, no worked order is
        /// placed or takes a slice, one bond purchase is the flat sum and the bonds held at once the smaller number.
        /// </summary>
        public bool LargeOn = true;

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
            if (!LargeOn)
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
        /// An open order cancelled by the player (MarketTrading.Cancel): what it filled is kept, and a worked order's money
        /// with the broker is settled at once (its day's money back, its takings paid). False if it is not open.
        /// </summary>
        public bool Cancel(int orderId)
        {
            Finish();   // (a moment half played is played out first: Midway)
            Order o = State.Order(orderId);
            if (o == null || !MarketTrading.Cancel(State, orderId))
            {
                return false;
            }
            if (o.Worked)
            {
                o.EndDay = State.Day;
                Settle(o, NameOf(o.StockId));
            }
            return true;
        }

        /// <summary>
        /// Every open worked order (or with 'back', every payout waiting to be put back) cancelled with a reason: their option
        /// was switched off. What they filled is kept, and a worked order's money with the broker settled. How many.
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
                    if (o.Worked)
                    {
                        Settle(o, NameOf(o.StockId));
                    }
                    stopped++;
                }
            }
            return stopped;
        }

        private string NameOf(int stockId)
        {
            Stock s = State.Stock(stockId);
            return s != null ? s.Name : string.Empty;
        }

        // ---------------------------------------------------------------- worked orders, on the market's clock

        // After a step: each open worked order's slice of the step's trading.
        private void WorkedOrders(int day, int k)
        {
            if (!LargeOn)
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

        // What open orders on the same side of a company, placed before this one, are still for (as MarketTrading.Reserved
        // counts them): the room they were placed with is theirs. Orders placed after it were placed with its own counted,
        // so it never waits for them: two worked purchases of one company never wait for each other.
        private long Others(Order o, int stockId)
        {
            long reserved = 0L;
            for (int i = 0; i < State.Orders.Count; i++)
            {
                Order x = State.Orders[i];
                if (x.Id < o.Id && x.IsOpen && !x.Closing && x.StockId == stockId && x.Side == o.Side)
                {
                    reserved += Math.Max(0L, x.Left);
                }
            }
            return reserved;
        }

        // One slice: held to the holding limit less what other open purchases are still for (a sale: to the shares held less
        // what other sales are for), on a cash account to the day's money held for it, at the market's price now with its
        // move; the least commission only on the order's first slice. A step whose room other orders hold, or whose day's
        // money is spent, goes by; the order stops when nothing of it can ever be done.
        private void Work(Order o, Stock s, long qty, long volume, int day, int phase)
        {
            bool buying = o.Side == OrderSide.Buy;
            long have = State.Held(s.Id);
            if (buying)
            {
                long most = MarketTrading.MostHeld(s);
                if (have >= most)
                {
                    Stop(o, s, OrderWhy.HoldingLimit, day);   // (the holding itself has come to the most)
                    return;
                }
                long room = most - have - Others(o, s.Id);
                if (room <= 0)
                {
                    return;   // (other orders hold the rest of the room: it waits for them)
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
                long room = have - Others(o, s.Id);
                if (room <= 0)
                {
                    return;   // (the shares left are other sales': it waits for them)
                }
                qty = Math.Min(qty, room);
            }
            int least = o.Filled > 0 ? 0 : _world.CommissionLeast;
            int cents = MarketTrading.MarketPrice(State, s, buying, qty, phase);
            if (buying && !State.Account.Margin)
            {
                if (o.HeldDay != day && !Hold(o, s, day, phase, least))
                {
                    Stop(o, s, OrderWhy.NoMoney, day);   // (the cash does not pay for a share: it ran out)
                    return;
                }
                long afford = InvestRules.SharesFor(o.Held, cents, least);
                if (afford < qty)
                {
                    qty = afford;
                    if (qty <= 0)
                    {
                        return;   // (the day's money is spent: the order goes on at the next market day)
                    }
                    cents = MarketTrading.MarketPrice(State, s, true, qty, phase);   // (fewer shares move it less: never dearer)
                }
            }
            long valueBefore = o.FilledValue;
            if (!FillNow(s, o, qty, cents, phase, least))
            {
                o.EndDay = day;   // (FillNow has stopped it: the margin, or what the cash or the shares came to)
                Settle(o, s.Name);
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
                Settle(o, s.Name);
                Tell(o, s, InvestNoteKind.Done, day);
            }
        }

        // A worked purchase's money for a market day, held from the cash in one transaction at its first slice of the day:
        // what a day's most (or what is left of the order, or of the room it has, if less) costs at the price of the moment
        // with the move its size makes, the commission and a tenth more, never more than the cash. False, and nothing held,
        // when the cash does not pay for one share.
        private bool Hold(Order o, Stock s, int day, int phase, int least)
        {
            long room = MarketTrading.MostHeld(s) - State.Held(s.Id) - Others(o, s.Id);
            long qty = Math.Max(1L, Math.Min(o.Left, Math.Min(MarketTrading.MostPerOrder(s), room)));
            long amount = Math.Min(_world.Wallet.Cash, InvestRules.DayHold(qty, MarketTrading.MarketPrice(State, s, true, qty, phase), least));
            if (InvestRules.SharesFor(amount, MarketTrading.MarketPrice(State, s, true, 1, phase), least) <= 0 || !_world.Wallet.Take(amount, MarketMoney.WorkedHeld, s.Name))
            {
                return false;
            }
            o.Held += amount;
            o.HeldDay = day;
            return true;
        }

        // A worked order's money with the broker settled, one transaction each: what is left of the day's held money given
        // back, the day's takings of its sales paid. At the close, and as it ends (done, stopped, cancelled, switched off,
        // its company gone).
        private void Settle(Order o, string name)
        {
            if (o.Held > 0)
            {
                long back = o.Held;
                o.Held = 0;
                _world.Wallet.Give(back, MarketMoney.WorkedBack, name);
            }
            if (o.Due > 0)
            {
                long due = o.Due;
                o.Due = 0;
                _world.Wallet.Give(due, MarketMoney.WorkedSold, name);
            }
        }

        private void Stop(Order o, Stock s, OrderWhy why, int day)
        {
            o.State = o.Filled > 0 ? OrderState.Cancelled : OrderState.Refused;
            o.Why = why;
            o.EndDay = day;
            Settle(o, s.Name);
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

        // At the close: every worked order's money with the broker settled (what is left of the day's held money back, the
        // day's takings paid), and each still open that worked today told.
        private void WorkedClose(int day)
        {
            for (int i = 0; i < State.Orders.Count; i++)
            {
                Order o = State.Orders[i];
                if (!o.Worked)
                {
                    continue;
                }
                Stock s = State.Stock(o.StockId);
                if (o.Held > 0 || o.Due > 0)
                {
                    Settle(o, s != null ? s.Name : string.Empty);
                }
                if (o.IsOpen && o.DayOf == day && o.DayQty > 0 && s != null)
                {
                    Tell(o, s, InvestNoteKind.Day, day);
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
        // commission, as an order at the market for that many would be, but never more than one order's most nor past the
        // most the player may hold (what other open purchases of it are still for left to them). What it cannot buy stays in
        // the cash, and the player is told why (Why on a filled one: bought in part). With dividends put back switched off
        // since it waited (the second's look may not have come), refused.
        private void BackNow(Order o, Stock s, int phase)
        {
            if (!ReinvestOn)
            {
                Refuse(o, s, OrderWhy.Switched);
                return;
            }
            int least = _world.CommissionLeast;
            long per = MarketTrading.MostPerOrder(s);
            long cap = Math.Min(per, Math.Max(0L, MarketTrading.MostHeld(s) - State.Held(s.Id) - Others(o, s.Id)));
            long most = InvestRules.SharesFor(o.Budget, MarketTrading.MarketPrice(State, s, true, 1, phase), least);
            long qty = most > 0 ? InvestRules.SharesFor(o.Budget, MarketTrading.MarketPrice(State, s, true, most, phase), least) : 0;   // (at the price of the most: never more than the money)
            OrderWhy part = OrderWhy.None;
            if (qty > cap)
            {
                part = cap < per ? OrderWhy.HoldingLimit : OrderWhy.TooLarge;
                qty = cap;
            }
            if (qty <= 0)
            {
                Refuse(o, s, part != OrderWhy.None ? part : OrderWhy.NoMoney);
                return;
            }
            o.Qty = qty;
            o.Why = part;   // (said with what it bought: MarketDirector.OrderFilled)
            FillNow(s, o, qty, MarketTrading.MarketPrice(State, s, true, qty, phase), phase);
        }

        // A payout put back that buys nothing: its money stays in the cash, and the player is told why.
        private void Refuse(Order o, Stock s, OrderWhy why)
        {
            o.State = OrderState.Refused;
            o.Why = why;
            _world.Filled(o, s, 0, 0);
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
        /// The test tools: the next dividend owed to the player on their own shares of a holding that puts a share of its
        /// dividends back, paid now as on its day, and that share bought at once while the market trades (else at the next
        /// open). The company's id; 0 when no dividend is owed to the player; -1 when one is, but on no holding that puts any
        /// back (or with dividends put back switched off): nothing is paid then.
        /// </summary>
        public int PayNextDueNow()
        {
            Finish();   // (a moment half played is played out first: Midway)
            int at = -1;
            bool owed = false;
            for (int i = 0; i < State.Dues.Count; i++)
            {
                DividendDue due = State.Dues[i];
                if (due.Bot != 0 || due.Cents <= 0)
                {
                    continue;
                }
                owed = true;
                Position p = State.Position(due.StockId);
                if (ReinvestOn && p != null && p.Shares > 0 && p.Reinvest > 0 && (at < 0 || due.PayDay < State.Dues[at].PayDay))
                {
                    at = i;
                }
            }
            if (at < 0)
            {
                return owed ? -1 : 0;
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
