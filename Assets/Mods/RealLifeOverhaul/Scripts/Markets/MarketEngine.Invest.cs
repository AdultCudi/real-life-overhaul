using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>What a worked order did that the player is told of (MarketDirector.Invest.cs). Never saved.</summary>
    public enum InvestNoteKind
    {
        Day = 0,         // its slices of a market day, at the close: Qty and Value; Done of Total so far
        Done = 1,        // all of it filled: Total the shares, Filled their money
        Stopped = 2      // stopped before done (cash, holding limit, shares held, margin): Why, Done of Total
    }

    /// <summary>A worked order's doing for the director to tell; what the fields hold depends on <see cref="Kind"/>.</summary>
    public struct InvestNote
    {
        public InvestNoteKind Kind;
        public int Order;
        public int Stock;
        public bool Buy;
        public long Qty, Value;      // the day's shares and money
        public long Done, Total;     // shares filled so far, and the order's size
        public long Filled;          // money of all it filled; its average price is Filled * 100 / Done cents
        public OrderWhy Why;
        public int SlicePct;         // the day's largest slice against its step's trading, per cent: the self-check
    }

    /// <summary>
    /// Part of the engine: a large investor's orders (InvestRules). A worked order is for more than one order may be: at
    /// each step, after the resting orders, it takes a slice of the step's trading (a fifth at most), paying the least
    /// commission only on its first, until done, out of cash or room, or cancelled; it never waits for an order placed after
    /// it. On a cash account its money settles once a day, not by the slice: a purchase parks a day's money at its first
    /// slice and gives back the rest at the close, a sale's takings wait until then, and both stay the player's for other
    /// purchases (<see cref="BrokerWallet"/>). A dividend put back becomes an order at the market for the next open. Game-free;
    /// a look at each open order a step, nothing per frame.
    /// </summary>
    public sealed partial class MarketEngine
    {
        /// <summary>Told of what worked orders do (the director's notifications). Null: nobody is told.</summary>
        public Action<InvestNote> InvestTold;

        /// <summary>
        /// Worked orders and larger bond purchases (Settings.MarketsLarge, set by the director); off, no worked order is placed
        /// or works, a bond purchase is the flat sum and fewer bonds are held at once.
        /// </summary>
        public bool LargeOn = true;

        /// <summary>Dividends are put back (Settings.MarketsReinvest, set by the director); off, every one is paid in cash.</summary>
        public bool ReinvestOn = true;

        // The player's asks

        /// <summary>A worked order placed, a slice at each step from the next; null with the reason (<see cref="WorkedWhy"/>).</summary>
        public Order PlaceWorked(int stockId, OrderSide side, long qty, bool held, bool test, out OrderWhy why)
        {
            Finish();   // Midway
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
        /// Whether a worked order can be placed now; nothing is changed. A sale is only of shares held and not already being
        /// sold; a purchase needs room under the holding limit and, from cash, one share's price in the cash in hand and worked
        /// sales' takings due, never another worked purchase's parked money.
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
                InvestRules.WithBroker(State, out _, out long due);
                if (InvestRules.SharesFor(Wallet.Free + due, q.Ask, _world.CommissionLeast) <= 0)
                {
                    return OrderWhy.NoMoney;   // else it would stop at its first slice: another's parked money is not its own
                }
            }
            return OrderWhy.None;
        }

        /// <summary>The share of each dividend a holding puts back from its next one, held to the choices; false with the reason.</summary>
        public bool SetReinvest(int stockId, int pct, out OrderWhy why)
        {
            Finish();   // Midway
            Position p = State.Position(stockId);
            why = p == null || p.Shares <= 0 ? OrderWhy.NoShares : Held ? OrderWhy.Held : OrderWhy.None;
            if (why != OrderWhy.None)
            {
                return false;
            }
            p.Reinvest = InvestRules.ReinvestOf(Math.Max(0, Math.Min(100, pct)));
            return true;
        }

        /// <summary>An open order cancelled by the player: what it filled is kept, a worked order's money settled. False if not open.</summary>
        public bool Cancel(int orderId)
        {
            Finish();   // Midway
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
        /// Every open worked order, or with <paramref name="back"/> every payout waiting to be put back, cancelled because its
        /// option was switched off; what they filled is kept and their money settled. Returns how many.
        /// </summary>
        public int StopAllOf(bool back, OrderWhy why)
        {
            Finish();   // Midway
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

        /// <summary>
        /// Every worked order's money with the broker settled now, the market stopping, so the cash is not short of it while
        /// it is off; an open order parks again when the market runs. Returns how much came back.
        /// </summary>
        public long SettleAll()
        {
            Finish();   // Midway
            long back = 0L;
            for (int i = 0; i < State.Orders.Count; i++)
            {
                Order o = State.Orders[i];
                if (o.Worked && (o.Parked > 0 || o.Due > 0))
                {
                    back += o.Parked + o.Due;
                    Settle(o, NameOf(o.StockId));
                    o.ParkedDay = 0;
                }
            }
            return back;
        }

        private string NameOf(int stockId)
        {
            Stock s = State.Stock(stockId);
            return s != null ? s.Name : string.Empty;
        }

        // Worked orders, on the market's clock

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
                long volume = s.Bars[s.BarCount - 1].V;   // the bar this step has just made
                long qty = InvestRules.Slice(o.Left, volume);
                if (qty > 0)
                {
                    Work(o, s, qty, volume, day, phase);
                }
            }
        }

        // What open orders on the same side, placed before this one, are still for: their room is theirs. Orders placed
        // after it counted it, so two worked orders never wait for each other.
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

        // One slice, held to the room other open orders leave and on a cash account to the day's parked money, at the market
        // price with its move. A step whose room or money is spent goes by; the order stops when nothing more can be done.
        private void Work(Order o, Stock s, long qty, long volume, int day, int phase)
        {
            bool buying = o.Side == OrderSide.Buy;
            long have = State.Held(s.Id);
            if (buying)
            {
                long most = MarketTrading.MostHeld(s);
                if (have >= most)
                {
                    Stop(o, s, OrderWhy.HoldingLimit, day);   // the holding itself has come to the most
                    return;
                }
                long room = most - have - Others(o, s.Id);
                if (room <= 0)
                {
                    return;   // other orders hold the rest of the room: it waits
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
                    return;   // the shares left are other sales': it waits
                }
                qty = Math.Min(qty, room);
            }
            int least = o.Filled > 0 ? 0 : _world.CommissionLeast;
            int cents = MarketTrading.MarketPrice(State, s, buying, qty, phase);
            if (buying && !State.Account.Margin)
            {
                if (o.ParkedDay != day && !Park(o, s, day, phase, least))
                {
                    Stop(o, s, OrderWhy.NoMoney, day);   // the cash in hand does not pay for a share: it ran out
                    return;
                }
                long afford = InvestRules.SharesFor(o.Parked, cents, least);
                if (afford < qty)
                {
                    qty = afford;
                    if (qty <= 0)
                    {
                        // The day's money cannot pay a slice. With no cash in hand for one share either it has run out and
                        // stops; else it goes on at the next market day, so it never parks day after day.
                        if (InvestRules.SharesFor(Wallet.Free, MarketTrading.MarketPrice(State, s, true, 1, phase), least) <= 0)
                        {
                            Stop(o, s, OrderWhy.NoMoney, day);
                        }
                        return;
                    }
                    cents = MarketTrading.MarketPrice(State, s, true, qty, phase);   // fewer shares move it less: never dearer
                }
            }
            long valueBefore = o.FilledValue;
            if (!FillNow(s, o, qty, cents, phase, least))
            {
                o.EndDay = day;   // FillNow has stopped it
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

        // A worked purchase's money for a market day, parked in one transaction at its first slice: what a day's most costs
        // with its move, the commission and a tenth more, never over the cash in hand (worked sales' takings paid in first).
        // False, nothing parked, when the cash in hand does not pay for one share.
        private bool Park(Order o, Stock s, int day, int phase, int least)
        {
            long room = MarketTrading.MostHeld(s) - State.Held(s.Id) - Others(o, s.Id);
            long qty = Math.Max(1L, Math.Min(o.Left, Math.Min(MarketTrading.MostPerOrder(s), room)));
            long need = InvestRules.DayPark(qty, MarketTrading.MarketPrice(State, s, true, qty, phase), least);
            if (Wallet.Free < need)
            {
                PayTakings();
            }
            long amount = Math.Min(Wallet.Free, need);
            if (InvestRules.SharesFor(amount, MarketTrading.MarketPrice(State, s, true, 1, phase), least) <= 0 || !Wallet.TakeFree(amount, MarketMoney.WorkedHeld, s.Name))
            {
                return false;
            }
            o.Parked += amount;
            o.ParkedDay = day;
            return true;
        }

        // The day's takings of every worked sale paid into the cash now, one transaction each.
        private void PayTakings()
        {
            for (int i = 0; i < State.Orders.Count; i++)
            {
                Order x = State.Orders[i];
                if (x.Worked && x.Due > 0)
                {
                    long due = x.Due;
                    x.Due = 0;
                    Wallet.Give(due, MarketMoney.WorkedSold, NameOf(x.StockId));
                }
            }
        }

        // A worked order's money with the broker settled, one transaction each: the day's parked money left given back and
        // its sales' takings paid. At the close, and as it ends.
        private void Settle(Order o, string name)
        {
            if (o.Parked > 0)
            {
                long back = o.Parked;
                o.Parked = 0;
                Wallet.Give(back, MarketMoney.WorkedBack, name);
            }
            if (o.Due > 0)
            {
                long due = o.Due;
                o.Due = 0;
                Wallet.Give(due, MarketMoney.WorkedSold, name);
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

        // At the close: every worked order's money settled, and each still open that worked today told.
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
                if (o.Parked > 0 || o.Due > 0)
                {
                    Settle(o, s != null ? s.Name : string.Empty);
                }
                if (o.IsOpen && o.DayOf == day && o.DayQty > 0 && s != null)
                {
                    Tell(o, s, InvestNoteKind.Day, day);
                }
            }
        }

        // Dividends put back

        // A dividend paid into the player's cash on their own holding: the share put back after tax waits for the next open
        // as an order at the market. Nothing while the option is off, for a short or a gone holding, or under one share.
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

        // A payout put back, filled now: as many shares as its money buys at the market price with its move and commission,
        // never over one order's most nor into room any open purchase of the company is for. What it cannot buy stays in the
        // cash and the player is told why; refused if putting back was switched off while it waited.
        private void BackNow(Order o, Stock s, int phase)
        {
            if (!ReinvestOn)
            {
                Refuse(o, s, OrderWhy.Switched);
                return;
            }
            int least = _world.CommissionLeast;
            long per = MarketTrading.MostPerOrder(s);
            long cap = Math.Min(per, Math.Max(0L, MarketTrading.MostHeld(s) - State.Held(s.Id) - MarketTrading.Reserved(State, s.Id, OrderSide.Buy)));
            long most = InvestRules.SharesFor(o.Budget, MarketTrading.MarketPrice(State, s, true, 1, phase), least);
            long qty = most > 0 ? InvestRules.SharesFor(o.Budget, MarketTrading.MarketPrice(State, s, true, most, phase), least) : 0;   // at the price of the most: never more than the money
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
            o.Why = part;   // said with what it bought (MarketDirector.OrderFilled)
            FillNow(s, o, qty, MarketTrading.MarketPrice(State, s, true, qty, phase), phase);
        }

        // A payout put back that buys nothing: its money stays in the cash, and the player is told why.
        private void Refuse(Order o, Stock s, OrderWhy why)
        {
            o.State = OrderState.Refused;
            o.Why = why;
            _world.Filled(o, s, 0, 0);
        }

        // The test tools

        /// <summary>
        /// The test tools: a market day's slices of every open worked order played now at this moment's price, and the day
        /// told. Returns how many orders worked; -1 while the market does not trade.
        /// </summary>
        public int WorkDayNow()
        {
            Finish();   // Midway
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
                    o.DayQty = o.DayValue = 0;   // told: the close tells only what comes after
                }
            }
            return worked;
        }

        /// <summary>
        /// The test tools: the next dividend owed on a holding that puts some back, paid now and that share bought at once
        /// while the market trades. The company's id; 0 when none is owed; -1 when one is but none puts any back.
        /// </summary>
        public int PayNextDueNow()
        {
            Finish();   // Midway
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
