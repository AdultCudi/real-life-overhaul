using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>The player's money as the market moves it, in whole units; every movement is a transaction of the game's.</summary>
    public interface IMarketWallet
    {
        /// <summary>Cash in hand, never below nothing.</summary>
        long Cash { get; }
        /// <summary>False, and nothing taken, if the cash is not there.</summary>
        bool Take(long amount, string why, string name);
        /// <summary>True when the cash in hand covers a purchase paid in several transactions; may take back worked orders' money first.</summary>
        bool Ready(long amount);
        /// <summary>Even if the cash is not there: what is owed to the broker.</summary>
        void Force(long amount, string why, string name);
        void Give(long amount, string why, string name);
    }

    /// <summary>The transaction keys of the market's money; "shares_" files them under investments in the Finance app.</summary>
    public static class MarketMoney
    {
        public const string Bought = "reallife_transaction_shares_bought";
        public const string Sold = "reallife_transaction_shares_sold";
        public const string Commission = "reallife_transaction_shares_commission";
        public const string Dividend = "reallife_transaction_shares_dividend";
        public const string Payout = "reallife_transaction_shares_payout";
        public const string Interest = "reallife_transaction_shares_margin_interest";
        public const string BorrowFee = "reallife_transaction_shares_borrow_fee";
        public const string Deposit = "reallife_transaction_shares_deposit";
        public const string Withdrawal = "reallife_transaction_shares_withdrawal";
        public const string Shortfall = "reallife_transaction_shares_shortfall";
        public const string BotFee = "reallife_transaction_shares_bot_fee";       // a trading bot's monthly fee
        public const string BotShare = "reallife_transaction_shares_bot_share";   // its share of the profit
        public const string WorkedHeld = "reallife_transaction_shares_worked_held";   // a worked purchase's day of money, parked at its first slice...
        public const string WorkedBack = "reallife_transaction_shares_worked_back";   // ...and what is left of it, given back at the close or its end
        public const string WorkedSold = "reallife_transaction_shares_worked_sold";   // a worked sale's day of takings after commission, paid at the close or its end
    }

    /// <summary>A price to buy or sell at: the bid, the offer and the middle, in cents.</summary>
    public struct Quote
    {
        public int Bid, Ask, Mid;
        public double HalfSpread;     // as a share of the middle
    }

    /// <summary>What an order would come to before it is placed: the price, the commission, the market's move against it.</summary>
    public struct Estimate
    {
        public long Qty;
        public int PriceCents;        // now; the limit for a limit order
        public long Value;            // money for the shares, before commission
        public long Fee;
        public long Total;            // paid for a purchase, received for a sale
        public int ImpactBp;          // how far its size moves the price against it
        public OrderWhy Why;          // None: it can be placed
    }

    /// <summary>
    /// Trading: quotes, the cost of a trade, placing and filling orders, the account. What keeps the market from being
    /// played: a round trip costs the spread and two commissions; an order moves the price against itself by the square root
    /// of its size, recent trading counted, so splitting saves nothing; a resting order fills only at a price the step
    /// reached; cash is rounded against the player.
    /// </summary>
    public static class MarketTrading
    {
        /// <summary>
        /// The option's most of a company, in bp, set by the director from Settings.MarketsHolding once a second. One static
        /// value because every sizing of a holding (orders, worked orders, dividends put back, bots, share options, a rival's
        /// holding) goes through <see cref="MostHeld"/>, so none can follow an older figure. It bounds only the player's own
        /// positions, filled on their own machine, so in a Going Public session each player's choice is theirs and nothing is sent.
        /// </summary>
        public static int HoldingBp { get; private set; } = MarketCatalogue.HoldingShareBp;

        /// <summary>The option's choice (0 a tenth ... 3 all of it); anything else, as from an edited settings file, is read as the nearest.</summary>
        public static void HoldTo(int choice)
        {
            HoldingBp = MarketCatalogue.HoldingCapBp[Math.Max(0, Math.Min(MarketCatalogue.HoldingCapBp.Length - 1, choice))];
        }

        /// <summary>
        /// The most of one company the player may hold, in bp. A player's listed company keeps the tenth: there the founder's
        /// stake and the takeover and activist rules are counted against the other holders, and another player's company is
        /// theirs. A rival's holding has only its free float to sell, so a larger choice stops there.
        /// </summary>
        public static int MostHeldBp(Stock s)
        {
            if (s.Own != null)
            {
                return MarketCatalogue.HoldingShareBp;
            }
            return s.Def != null && s.Def.Holding ? Math.Min(HoldingBp, MarketCatalogue.HoldingFloatBp) : HoldingBp;
        }

        public static double DayVol(MarketState state, Stock s)
        {
            double m = MarketCatalogue.IndexDayVol * s.Def.Beta / 100.0;
            double sec = MarketCatalogue.Sectors[(int)s.Sector].SectorVolBp / 10000.0;
            double own = s.Def.IdioBp / 10000.0;
            return MarketCatalogue.VolScale(state.YearDays) * Math.Sqrt((m * m + sec * sec) * Math.Max(0.25, state.Variance) + own * own);
        }

        public static double HalfSpread(MarketState state, Stock s, int phase)
        {
            double bp = s.Def.SpreadBp * 0.5 * Math.Sqrt(Math.Max(0.5, state.Variance)) * (1.0 + 2.0 * s.Distress);
            if (phase <= MarketCatalogue.PhaseOpen)
            {
                bp *= 2.0;   // the open, and orders placed before it
            }
            else if (phase >= MarketCatalogue.PhaseClose - 6)
            {
                bp *= 1.3;   // the last half hour
            }
            if (s.BidCents > 0)
            {
                bp *= 0.6;   // a company with a bid for it trades close to the bid
            }
            return Math.Min(MarketCatalogue.SpreadMostBp, bp) / 10000.0;
        }

        public static Quote QuoteOf(MarketState state, Stock s, int phase)
        {
            double mid = s.Mid * 100.0;
            double half = HalfSpread(state, s, phase);
            var q = new Quote { HalfSpread = half, Mid = Math.Max(1, (int)Math.Round(mid)) };
            q.Bid = Math.Max(1, (int)Math.Floor(mid * (1.0 - half)));
            q.Ask = Math.Max(q.Bid + 1, (int)Math.Ceiling(mid * (1.0 + half)));
            return q;
        }

        /// <summary>How far an order moves the price against itself, a share of the price: the square root law on recent trading with it.</summary>
        public static double Impact(MarketState state, Stock s, long qty)
        {
            if (qty <= 0 || s.Adv <= 1.0)
            {
                return 0.0;
            }
            double c = MarketCatalogue.ImpactTemporary * DayVol(state, s);
            double before = Math.Max(0.0, s.Impact), after = before + qty;
            double cost = c * (after * Math.Sqrt(after / s.Adv) - before * Math.Sqrt(before / s.Adv));
            return cost / qty;
        }

        /// <summary>A share of the money, never less than the least at today's prices.</summary>
        public static long Commission(long value, int least)
        {
            if (value <= 0)
            {
                return 0;
            }
            long share = (value * MarketCatalogue.CommissionBp + 9999L) / 10000L;
            return Math.Max(least, share);
        }

        /// <summary>Money for so many shares at a price in cents: a purchase rounded up to the whole unit, a sale down.</summary>
        public static long Value(long qty, int cents, bool buying)
        {
            long total = qty * cents;
            return buying ? (total + 99L) / 100L : total / 100L;
        }

        /// <summary>In cents: the offer or the bid, moved against the order by its size.</summary>
        public static int MarketPrice(MarketState state, Stock s, bool buying, long qty, int phase)
        {
            Quote q = QuoteOf(state, s, phase);
            double impact = Impact(state, s, qty);
            return buying ? (int)Math.Ceiling(q.Ask * (1.0 + impact)) : Math.Max(1, (int)Math.Floor(q.Bid * (1.0 - impact)));
        }

        /// <summary>Every holding's value in money at the bid; a short at the offer, negative.</summary>
        public static long HoldingsValue(MarketState state, int phase, out long longs, out long shorts)
        {
            longs = shorts = 0;
            for (int i = 0; i < state.Positions.Count; i++)
            {
                Position p = state.Positions[i];
                Stock s = state.Stock(p.StockId);
                if (s == null || p.Shares == 0)
                {
                    continue;
                }
                Quote q = QuoteOf(state, s, phase);
                if (p.Shares > 0)
                {
                    longs += Value(p.Shares, q.Bid, false);
                }
                else
                {
                    shorts += Value(-p.Shares, q.Ask, true);
                }
            }
            return longs - shorts;
        }

        /// <summary>The day's change in cents of the holdings against the previous close, and in <paramref name="before"/> their worth then.</summary>
        public static long DayChange(MarketState state, out long before)
        {
            long change = 0;
            before = 0;
            for (int i = 0; i < state.Positions.Count; i++)
            {
                Position p = state.Positions[i];
                Stock s = state.Stock(p.StockId);
                if (s == null || p.Shares == 0 || s.PrevClose <= 0)
                {
                    continue;
                }
                change += p.Shares * (long)(s.Last - s.PrevClose);
                before += Math.Abs(p.Shares) * (long)s.PrevClose;
            }
            return change;
        }

        public static int DayChangeBp(long change, long before)
        {
            return before > 0 ? (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, change * 10000L / before)) : 0;
        }

        public static long Equity(MarketState state, int phase)
        {
            HoldingsValue(state, phase, out long longs, out long shorts);
            return state.Account.Cash + longs - shorts;
        }

        /// <summary>What a margin account must keep in equity: a quarter of what is held, three tenths of shorts, more for risky companies.</summary>
        public static long Maintenance(MarketState state, int phase)
        {
            long need = 0;
            for (int i = 0; i < state.Positions.Count; i++)
            {
                Position p = state.Positions[i];
                Stock s = state.Stock(p.StockId);
                if (s == null || p.Shares == 0)
                {
                    continue;
                }
                Quote q = QuoteOf(state, s, phase);
                int pct = (p.Shares > 0 ? MarketCatalogue.MaintenanceLongPct : MarketCatalogue.MaintenanceShortPct) + (Risky(s) ? MarketCatalogue.MaintenanceRiskyExtraPct : 0);
                long value = p.Shares > 0 ? Value(p.Shares, q.Bid, false) : Value(-p.Shares, q.Ask, true);
                need += (value * pct + 99) / 100;
            }
            return need;
        }

        public static bool Risky(Stock s)
        {
            return s.Size == MarketSize.Small || s.Distress > 0.3 || s.BidCents > 0;
        }

        /// <summary>Not a small, new or distressed company, one under a bid, or a rival's holding.</summary>
        public static bool Borrowable(MarketState state, Stock s, int day)
        {
            return s.Listed && s.Size != MarketSize.Small && s.Distress < 0.3 && s.BidCents <= 0 && s.RivalId == null && !MarketCatalogue.IsRival(s.Id)
                   && day - s.ListedDay >= Math.Max(5, state.YearDays / 12);
        }

        public static int BorrowFeeBp(MarketState state, Stock s)
        {
            double vol = DayVol(state, s) / 0.02;
            int bp = (int)(MarketCatalogue.BorrowFeeLeastBp * vol * (s.Size == MarketSize.Mid ? 2.0 : 1.0));
            return Math.Max(MarketCatalogue.BorrowFeeLeastBp, Math.Min(MarketCatalogue.BorrowFeeMostBp, bp));
        }

        /// <summary>Shares already asked to be sold or bought; a stop loss or take profit holds nothing back.</summary>
        public static long Reserved(MarketState state, int stockId, OrderSide side)
        {
            long reserved = 0;
            for (int i = 0; i < state.Orders.Count; i++)
            {
                Order o = state.Orders[i];
                if (o.IsOpen && !o.Closing && o.StockId == stockId && o.Side == side)
                {
                    reserved += o.Left;
                }
            }
            return reserved;
        }

        public static Estimate Check(MarketState state, Stock s, OrderSide side, OrderType type, long qty, int limitCents, int stopCents, long cash, int least, bool open, bool held, int phase,
            bool closing = false)
        {
            var e = new Estimate { Qty = qty };
            bool buying = side == OrderSide.Buy;
            if (held)
            {
                e.Why = OrderWhy.Held;
                return e;
            }
            if (s == null || !s.Listed)
            {
                e.Why = OrderWhy.NotListed;
                return e;
            }
            if (qty <= 0 || (type == OrderType.Limit && limitCents <= 0) || (type == OrderType.Stop && stopCents <= 0))
            {
                e.Why = OrderWhy.BadPrice;
                return e;
            }
            if (OpenOrders(state) >= MarketCatalogue.OrdersMost)
            {
                e.Why = OrderWhy.TooMany;
                return e;
            }
            if (qty > MostPerOrder(s))
            {
                e.Why = OrderWhy.TooLarge;
                return e;
            }
            long held2 = state.Held(s.Id);
            if (closing)
            {
                // A stop loss or a take profit: on what is held, never more, at a price beyond the market's.
                long holding = buying ? -held2 : held2;
                Quote now = QuoteOf(state, s, open ? phase : MarketCatalogue.PhaseOpen);
                e.Why = type == OrderType.Market ? OrderWhy.BadPrice : holding <= 0 ? OrderWhy.NothingHeld : qty > holding ? OrderWhy.NoShares
                    : type == OrderType.Stop && (buying ? stopCents <= now.Ask : stopCents >= now.Bid) ? OrderWhy.StopSide
                    : type == OrderType.Limit && (buying ? limitCents >= now.Ask : limitCents <= now.Bid) ? OrderWhy.ProfitSide : OrderWhy.None;
                if (e.Why != OrderWhy.None)
                {
                    return e;
                }
            }
            long after = held2 + (buying ? qty + Reserved(state, s.Id, OrderSide.Buy) : -qty - Reserved(state, s.Id, OrderSide.Sell));
            if (closing)
            {
                after = buying ? Math.Min(0L, held2 + qty) : Math.Max(0L, held2 - qty);   // it only closes: no limit it could break
            }
            if (buying && after > MostHeld(s))
            {
                e.Why = OrderWhy.HoldingLimit;
                return e;
            }
            if (!buying && after < 0)
            {
                if (!state.Account.Margin)
                {
                    e.Why = OrderWhy.NoShares;
                    return e;
                }
                if (!Borrowable(state, s, state.Day))
                {
                    e.Why = OrderWhy.NoBorrow;
                    return e;
                }
                if (-after > MostHeld(s))
                {
                    e.Why = OrderWhy.HoldingLimit;
                    return e;
                }
            }
            Quote q = QuoteOf(state, s, open ? phase : MarketCatalogue.PhaseOpen);
            double impact = Impact(state, s, qty);
            e.ImpactBp = (int)Math.Round(impact * 10000.0);
            int price;
            if (type == OrderType.Limit)
            {
                price = limitCents;
            }
            else if (type == OrderType.Stop)
            {
                price = buying ? Math.Max(stopCents, (int)Math.Ceiling(q.Ask * (1.0 + impact))) : Math.Min(stopCents, Math.Max(1, (int)Math.Floor(q.Bid * (1.0 - impact))));
            }
            else
            {
                price = buying ? (int)Math.Ceiling(q.Ask * (1.0 + impact)) : Math.Max(1, (int)Math.Floor(q.Bid * (1.0 - impact)));
            }
            e.PriceCents = price;
            e.Value = Value(qty, price, buying);
            e.Fee = Commission(e.Value, least);
            if (!buying)
            {
                e.Fee = Math.Min(e.Fee, e.Value);
            }
            e.Total = buying ? e.Value + e.Fee : e.Value - e.Fee;
            if (buying && !state.Account.Margin && e.Total > cash)
            {
                e.Why = OrderWhy.NoMoney;
                return e;
            }
            if (state.Account.Margin && !MarginAllows(state, s, buying, qty, e.Fee, phase))
            {
                e.Why = OrderWhy.BelowMargin;
                return e;
            }
            return e;
        }

        public static long MostPerOrder(Stock s)
        {
            return Math.Max(1L, (long)(s.Adv * MarketCatalogue.OrderShareOfVolumePct / 100.0));
        }

        public static long MostHeld(Stock s)
        {
            return Math.Max(1L, s.Shares * MostHeldBp(s) / 10000L);
        }

        public static int OpenOrders(MarketState state)
        {
            int count = 0;
            for (int i = 0; i < state.Orders.Count; i++)
            {
                count += state.Orders[i].IsOpen ? 1 : 0;
            }
            return count;
        }

        /// <summary>Whether a margin account may trade: closing always may; opening must leave half of everything held in equity.</summary>
        public static bool MarginAllows(MarketState state, Stock s, bool buying, long qty, long fee, int phase)
        {
            long held = state.Held(s.Id);
            long closing = buying ? Math.Min(qty, Math.Max(0L, -held)) : Math.Min(qty, Math.Max(0L, held));
            long opening = qty - closing;
            if (opening <= 0)
            {
                return true;
            }
            Quote q = QuoteOf(state, s, phase);
            int cents = buying ? q.Ask : q.Bid;
            long grossAfter = GrossExposure(state, phase) - Value(closing, cents, buying) + Value(opening, cents, buying);
            long equityAfter = Equity(state, phase) - fee;
            return equityAfter * 100 >= Math.Max(0L, grossAfter) * MarketCatalogue.InitialMarginPct;
        }

        public static long GrossExposure(MarketState state, int phase)
        {
            HoldingsValue(state, phase, out long longs, out long shorts);
            return longs + shorts;
        }

        /// <summary>The order placed if <see cref="Check"/> allows it, else null; the caller fills one that can fill at once.</summary>
        public static Order Place(MarketState state, Stock s, OrderSide side, OrderType type, OrderLife life, long qty, int limitCents, int stopCents, long cash, int least,
            bool open, bool held, int phase, bool test, out OrderWhy why, bool closing = false)
        {
            Estimate e = Check(state, s, side, type, qty, limitCents, stopCents, cash, least, open, held, phase, closing);
            why = e.Why;
            if (why != OrderWhy.None)
            {
                return null;
            }
            var order = new Order
            {
                Id = state.NextOrderId++, StockId = s.Id, Side = side, Type = type, Life = life, Qty = qty, LimitCents = type == OrderType.Limit ? limitCents : 0,
                StopCents = type == OrderType.Stop ? stopCents : 0, PlacedDay = state.Day, PlacedPhase = phase, State = OrderState.Open, Test = test, Closing = closing,
                ExpireDay = life == OrderLife.Day ? (open || phase < MarketCatalogue.PhaseOpen ? state.Day : NextTradingDay(state.Day)) : MarketDays(state.Day, MarketCatalogue.GoodTillDays)
            };
            state.Orders.Add(order);
            return order;
        }

        public static bool Cancel(MarketState state, int orderId, OrderWhy why = OrderWhy.Cancelled)
        {
            Order o = state.Order(orderId);
            if (o == null || !o.IsOpen)
            {
                return false;
            }
            o.State = o.Filled > 0 && why == OrderWhy.Cancelled ? OrderState.Cancelled : why == OrderWhy.Expired ? OrderState.Expired : OrderState.Cancelled;
            o.Why = why;
            return true;
        }

        public static int NextTradingDay(int day)
        {
            int d = day + 1;
            while (!MarketCatalogue.TradingDay(d))
            {
                d++;
            }
            return d;
        }

        public static int MarketDays(int day, int count)
        {
            int d = day;
            for (int i = 0; i < count; i++)
            {
                d = NextTradingDay(d);
            }
            return d;
        }

        /// <summary>False, changing nothing, if a purchase cannot be paid or breaks the margin.</summary>
        public static bool Fill(MarketState state, Stock s, Order order, long qty, int cents, int least, IMarketWallet wallet, int phase)
        {
            return Fill(state, s, order, qty, cents, least, wallet, phase, true, out _);
        }

        /// <summary>With <paramref name="move"/> false the lasting price move is given in <paramref name="kept"/> for a session's tape.</summary>
        public static bool Fill(MarketState state, Stock s, Order order, long qty, int cents, int least, IMarketWallet wallet, int phase, bool move, out double kept)
        {
            kept = 0.0;
            bool buying = order.Side == OrderSide.Buy;
            long value = Value(qty, cents, buying);
            long fee = Commission(value, least);
            if (!buying)
            {
                fee = Math.Min(fee, value);
            }
            Account a = state.Account;
            Position p = state.Position(s.Id);
            long held = p != null ? p.Shares : 0;
            if (buying)
            {
                if (!a.Margin && order.Worked)
                {
                    // A worked order's slice on a cash account is paid from the day's money parked for it (MarketEngine.Invest.cs).
                    if (order.Parked < value + fee)
                    {
                        return false;
                    }
                    order.Parked -= value + fee;
                }
                else if (!a.Margin)
                {
                    if (!wallet.Ready(value + fee))   // takes back from worked orders' money first
                    {
                        return false;
                    }
                    if (!wallet.Take(value, MarketMoney.Bought, s.Name))
                    {
                        return false;
                    }
                    wallet.Take(fee, MarketMoney.Commission, s.Name);
                }
                else
                {
                    if (!order.Forced && !MarginAllows(state, s, true, qty, fee, phase))
                    {
                        return false;
                    }
                    a.Cash -= value + fee;
                }
            }
            else
            {
                if (!a.Margin)
                {
                    if (held < qty)
                    {
                        return false;
                    }
                    if (order.Worked)
                    {
                        order.Due += value - fee;   // a worked sale's takings wait, paid at the close or its end
                    }
                    else
                    {
                        wallet.Give(value, MarketMoney.Sold, s.Name);
                        wallet.Take(fee, MarketMoney.Commission, s.Name);
                    }
                }
                else
                {
                    if (held < qty && !order.Forced && (!Borrowable(state, s, state.Day) || !MarginAllows(state, s, false, qty, fee, phase)))
                    {
                        return false;
                    }
                    a.Cash += value - fee;
                }
            }
            a.Commission += fee;
            if (p == null)
            {
                p = new Position { StockId = s.Id, OpenedDay = state.Day, Test = order.Test };
                state.Positions.Add(p);
            }
            Book(p, buying ? qty : -qty, buying ? value + fee : value - fee, a);
            if (p.Shares == 0)
            {
                state.Positions.Remove(p);
            }
            order.Filled += qty;
            order.FilledValue += value;
            if (order.Left <= 0)
            {
                order.State = OrderState.Filled;
            }
            // A part of the impact stays in the price as mood. A worked order's slices together move it as one order of
            // their size (SliceShare), not each as an order of its own.
            double share = order.Worked ? SliceShare(s.Impact, qty, s.Adv) : Math.Sqrt(qty / Math.Max(1.0, s.Adv));
            kept = (buying ? 1.0 : -1.0) * MarketCatalogue.ImpactPermanent * MarketCatalogue.ImpactTemporary * DayVol(state, s) * share;
            if (move)
            {
                s.Mis += kept;
                s.Sent += kept;
                s.DayVolume += qty;
            }
            s.Impact += qty;   // the player's own recent trading, whatever moves the price
            TradeKind kind = buying ? TradeKind.Bought : TradeKind.Sold;
            if (!order.Worked || !AddToLine(state, order.Id, s.Id, kind, qty, buying ? -value : value, fee, phase))
            {
                AddTrade(state, new Trade
                {
                    Day = state.Day, Phase = phase, StockId = s.Id, Kind = kind, Qty = qty, PriceCents = cents, Value = buying ? -value : value, Fee = fee, Test = order.Test,
                    Order = order.Worked ? order.Id : 0, Back = order.Back
                });
            }
            return true;
        }

        /// <summary>A worked slice's lasting move, so the slices of one order move the price as one order of their size.</summary>
        public static double SliceShare(double recent, long qty, double adv)
        {
            double a = Math.Max(1.0, adv), before = Math.Max(0.0, recent);
            return Math.Sqrt((before + Math.Max(0L, qty)) / a) - Math.Sqrt(before / a);
        }

        // A worked order's slice added to that order's history line of the day; false when there is none yet.
        private static bool AddToLine(MarketState state, int order, int stockId, TradeKind kind, long qty, long value, long fee, int phase)
        {
            for (int i = state.Trades.Count - 1; i >= 0; i--)
            {
                Trade t = state.Trades[i];
                if (t.Day != state.Day)
                {
                    return false;   // lines are in time order: none of today's is further back
                }
                if (t.Order == order && t.StockId == stockId && t.Kind == kind)
                {
                    t.Qty += qty;
                    t.Value += value;
                    t.Fee += fee;
                    t.Phase = phase;
                    t.PriceCents = t.Qty > 0 ? (int)Math.Min(int.MaxValue, Math.Abs(t.Value) * 100L / t.Qty) : t.PriceCents;
                    return true;
                }
            }
            return false;
        }

        /// <summary>A position changed by shares (positive bought) for money; what is kept keeps its average cost, what is closed is realised.</summary>
        public static void Book(Position p, long shares, long money, Account a)
        {
            long before = p.Shares;
            if (before == 0 || Math.Sign(before) == Math.Sign(shares))
            {
                // Opening or adding: a long's cost is what was paid; a short's is what selling brought.
                p.Shares = before + shares;
                p.Cost += money;
                return;
            }
            long closing = Math.Min(Math.Abs(shares), Math.Abs(before));
            long part = before != 0 ? p.Cost * closing / Math.Abs(before) : 0;   // rounded towards nothing
            long moneyClosing = money * closing / Math.Abs(shares);
            long gain = before > 0 ? moneyClosing - part : part - moneyClosing;
            p.Realised += gain;
            a.Realised += gain;
            p.Cost -= part;
            p.Shares = before + Math.Sign(shares) * closing;
            long rest = Math.Abs(shares) - closing;
            if (rest > 0)
            {
                // Through nothing: the rest opens a position the other way.
                p.Shares = Math.Sign(shares) * rest;
                p.Cost = money - moneyClosing;
            }
            else if (p.Shares == 0)
            {
                p.Cost = 0;
            }
        }

        public static void AddTrade(MarketState state, Trade trade)
        {
            state.Trades.Add(trade);
            while (state.Trades.Count > MarketCatalogue.TradesKept)
            {
                state.Trades.RemoveAt(0);
            }
        }

        public static long Worth(MarketState state, int phase, out long cost)
        {
            long worth = 0;
            cost = 0;
            for (int i = 0; i < state.Positions.Count; i++)
            {
                Position p = state.Positions[i];
                Stock s = state.Stock(p.StockId);
                if (s == null)
                {
                    continue;
                }
                Quote q = QuoteOf(state, s, phase);
                worth += p.Shares >= 0 ? Value(p.Shares, q.Bid, false) : -Value(-p.Shares, q.Ask, true);
                cost += p.Shares >= 0 ? p.Cost : -p.Cost;
            }
            return worth;
        }
    }
}
