using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>The player's money as the market moves it. Whole units of the game's currency; every movement is a transaction of the game's.</summary>
    public interface IMarketWallet
    {
        /// <summary>Cash in hand, never below nothing.</summary>
        long Cash { get; }
        /// <summary>Takes an amount. False, and nothing taken, if the cash is not there.</summary>
        bool Take(long amount, string why, string name);
        /// <summary>Takes an amount even if the cash is not there (what is owed to the broker).</summary>
        void Force(long amount, string why, string name);
        void Give(long amount, string why, string name);
    }

    /// <summary>The transaction keys of the market's money. Each has a text and a "_label" in en.json; "shares_" puts them under investments in the Finance app.</summary>
    public static class MarketMoney
    {
        public const string Bought = "reallife_transaction_shares_bought";
        public const string Sold = "reallife_transaction_shares_sold";
        public const string Commission = "reallife_transaction_shares_commission";
        public const string Dividend = "reallife_transaction_shares_dividend";
        public const string DividendOwed = "reallife_transaction_shares_dividend_owed";
        public const string Payout = "reallife_transaction_shares_payout";
        public const string Interest = "reallife_transaction_shares_margin_interest";
        public const string BorrowFee = "reallife_transaction_shares_borrow_fee";
        public const string Deposit = "reallife_transaction_shares_deposit";
        public const string Withdrawal = "reallife_transaction_shares_withdrawal";
        public const string Shortfall = "reallife_transaction_shares_shortfall";
        public const string BotFee = "reallife_transaction_shares_bot_fee";       // a trading bot's monthly fee (MarketBots)
        public const string BotShare = "reallife_transaction_shares_bot_share";   // its share of the profit
        public const string WorkedHeld = "reallife_transaction_shares_worked_held";   // a worked purchase's day of money, held at its first slice (MarketEngine.Invest.cs)...
        public const string WorkedBack = "reallife_transaction_shares_worked_back";   // ...and what is left of it, given back at the close or its end
        public const string WorkedSold = "reallife_transaction_shares_worked_sold";   // a worked sale's takings of the day after the commission, paid at the close or its end
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
        public int PriceCents;        // the price it would be filled at now (the limit for a limit order)
        public long Value;            // money for the shares, before commission
        public long Fee;
        public long Total;            // what is paid (a purchase) or received (a sale)
        public int ImpactBp;          // how far the price moves against the order because of its size
        public OrderWhy Why;          // None: it can be placed
    }

    /// <summary>
    /// Trading: quotes, the cost of a trade, placing orders and filling them, the account. Game-free.
    ///
    /// What protects the market from being played:
    ///   every purchase is at the offer and every sale at the bid, with a commission on each: a round trip always costs
    ///   the spread and two commissions; an order moves the price against itself by the square root of its size against
    ///   a day's volume, counting what the player traded in the company just before (so splitting an order saves
    ///   nothing); a part of that move stays in the price; one order is at most a fifth of a day's volume (a larger one
    ///   is worked, a fifth of each step's trading at a time, its slices moving the price as one order of their size and,
    ///   on a cash account, settling its money once a day: MarketEngine.Invest.cs) and a holding at most a tenth of a
    ///   company, which no purchase but the broker's own ever passes; nothing is traded while the market is shut,
    ///   behind or held after a load; a resting order fills only at a price the market reached in that step, at its limit
    ///   or the step's first price if that was better, a stop at the market's price of the step that reached it (the next
    ///   price: after a gap or a fast fall, under the stop), and a limit never more than a quarter of the step's volume; a
    ///   stop loss or a take profit only ever closes what is still held; cash is in whole units, rounded against the player.
    /// </summary>
    public static class MarketTrading
    {
        // ---------------------------------------------------------------- the price of a trade

        /// <summary>A company's ordinary daily move (its own, its sector's and the market's together) in today's markets.</summary>
        public static double DayVol(MarketState state, Stock s)
        {
            double m = MarketCatalogue.IndexDayVol * s.Def.Beta / 100.0;
            double sec = MarketCatalogue.Sectors[(int)s.Sector].SectorVolBp / 10000.0;
            double own = s.Def.IdioBp / 10000.0;
            return MarketCatalogue.VolScale(state.YearDays) * Math.Sqrt((m * m + sec * sec) * Math.Max(0.25, state.Variance) + own * own);
        }

        /// <summary>Half the bid-offer spread as a share of the middle: by size, wider in a nervous market, at the open and near the close, and for a company in distress.</summary>
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
            // At least a cent between bid and offer; the bid is never under a cent.
            q.Bid = Math.Max(1, (int)Math.Floor(mid * (1.0 - half)));
            q.Ask = Math.Max(q.Bid + 1, (int)Math.Ceiling(mid * (1.0 + half)));
            return q;
        }

        /// <summary>
        /// How far an order moves the price against itself, as a share of the price: the square root law of real markets,
        /// on what the player has just traded in the company and this order together, less what was already paid for
        /// the first part.
        /// </summary>
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

        /// <summary>Commission on a purchase or a sale of so much money: a share of it, never less than the least (at today's prices).</summary>
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

        /// <summary>The price an order at the market would be filled at now, in cents: the offer or the bid, moved against it by its size.</summary>
        public static int MarketPrice(MarketState state, Stock s, bool buying, long qty, int phase)
        {
            Quote q = QuoteOf(state, s, phase);
            double impact = Impact(state, s, qty);
            return buying ? (int)Math.Ceiling(q.Ask * (1.0 + impact)) : Math.Max(1, (int)Math.Floor(q.Bid * (1.0 - impact)));
        }

        // ---------------------------------------------------------------- the account

        /// <summary>The value of every holding at the bid (a short at the offer, under nothing), in money.</summary>
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

        /// <summary>
        /// The day's change of the player's own holdings against the previous close (<see cref="Stock.PrevClose"/>, set at
        /// the open), in cents: a holding gains as its price rises, a short one as it falls. <paramref name="before"/> is
        /// what they were worth at that close, for the per cent. One pass over the holdings, when the Portfolio is written.
        /// </summary>
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

        /// <summary>The day's change in basis points of what was held at the previous close; 0 with nothing held.</summary>
        public static int DayChangeBp(long change, long before)
        {
            return before > 0 ? (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, change * 10000L / before)) : 0;
        }

        /// <summary>A margin account's equity: its own money, what is held and what is owed on shares sold short.</summary>
        public static long Equity(MarketState state, int phase)
        {
            long longs, shorts;
            HoldingsValue(state, phase, out longs, out shorts);
            return state.Account.Cash + longs - shorts;
        }

        /// <summary>What a margin account must keep in equity: a quarter of what is held, three tenths of what is sold short, more for risky companies.</summary>
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

        /// <summary>A company a broker is wary of: small, new, in distress or with a bid on it.</summary>
        public static bool Risky(Stock s)
        {
            return s.Size == MarketSize.Small || s.Distress > 0.3 || s.BidCents > 0;
        }

        /// <summary>Whether shares of a company can be borrowed to sell short: not a small company, not one listed in the last month, not one in distress, under a bid, or one of the game's rivals.</summary>
        public static bool Borrowable(MarketState state, Stock s, int day)
        {
            return s.Listed && s.Size != MarketSize.Small && s.Distress < 0.3 && s.BidCents <= 0 && s.RivalId == null && !MarketCatalogue.IsRival(s.Id)
                   && day - s.ListedDay >= Math.Max(5, state.YearDays / 12);
        }

        /// <summary>The fee a year for borrowing a company's shares, in bp: more for smaller and more volatile companies.</summary>
        public static int BorrowFeeBp(MarketState state, Stock s)
        {
            double vol = DayVol(state, s) / 0.02;
            int bp = (int)(MarketCatalogue.BorrowFeeLeastBp * vol * (s.Size == MarketSize.Mid ? 2.0 : 1.0));
            return Math.Max(MarketCatalogue.BorrowFeeLeastBp, Math.Min(MarketCatalogue.BorrowFeeMostBp, bp));
        }

        // ---------------------------------------------------------------- placing an order

        /// <summary>
        /// Shares the player has asked to sell that are not sold yet (so the same shares are never sold twice). A stop loss
        /// or a take profit holds nothing back: it only ever sells what is still held when it fills.
        /// </summary>
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

        /// <summary>
        /// What an order would come to now, and whether it can be placed. Nothing is changed. 'open' is whether the
        /// market trades now; 'held' whether orders wait after a load; 'least' the least commission at today's prices.
        /// </summary>
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
                // A stop loss or a take profit: on what is held (a short is bought back), never more, at a price beyond the market's.
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
                after = buying ? Math.Min(0L, held2 + qty) : Math.Max(0L, held2 - qty);   // (it only closes: no limit it could break)
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

        /// <summary>The most shares of a company one order can be for: a fifth of a day's usual volume.</summary>
        public static long MostPerOrder(Stock s)
        {
            return Math.Max(1L, (long)(s.Adv * MarketCatalogue.OrderShareOfVolumePct / 100.0));
        }

        /// <summary>The most shares of a company the player may hold (or owe): a tenth of its shares in issue.</summary>
        public static long MostHeld(Stock s)
        {
            return Math.Max(1L, s.Shares * MarketCatalogue.HoldingShareBp / 10000L);
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

        /// <summary>
        /// Whether a margin account may make a trade: one that only closes (part of) a position always may; one that
        /// opens or adds must leave half of everything held in equity, after its commission.
        /// </summary>
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

        /// <summary>What every position is worth together, shorts counted as positive.</summary>
        public static long GrossExposure(MarketState state, int phase)
        {
            long longs, shorts;
            HoldingsValue(state, phase, out longs, out shorts);
            return longs + shorts;
        }

        /// <summary>
        /// The order placed. Checks again first: an order that cannot be placed is not made (the reason is in
        /// <paramref name="why"/>). An order at the market while the market is open, and a limit already met, are filled
        /// at once by <see cref="MarketEngine"/> (the caller does that); one placed while it is shut waits for the open.
        /// </summary>
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

        /// <summary>An open order cancelled. Nothing is charged and nothing in the market moves.</summary>
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

        /// <summary>The day so many market days after another.</summary>
        public static int MarketDays(int day, int count)
        {
            int d = day;
            for (int i = 0; i < count; i++)
            {
                d = NextTradingDay(d);
            }
            return d;
        }

        // ---------------------------------------------------------------- filling

        /// <summary>
        /// Shares bought or sold at a price, with the money moved and the position changed. Returns false, and changes
        /// nothing, if a purchase cannot be paid for (cash) or would take a margin account under its initial margin.
        /// A sale of more than is held, in a margin account, sells what is held and sells the rest short.
        /// </summary>
        public static bool Fill(MarketState state, Stock s, Order order, long qty, int cents, int least, IMarketWallet wallet, int phase)
        {
            return Fill(state, s, order, qty, cents, least, wallet, phase, true, out _);
        }

        /// <summary>
        /// As <see cref="Fill(MarketState, Stock, Order, long, int, int, IMarketWallet, int)"/>; with <paramref name="move"/>
        /// false the lasting move of the price and the shares traded are not put on the company here but given in
        /// <paramref name="kept"/> (in a Going Public session the city's market applies every player's, on its tape).
        /// </summary>
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
                    // A worked order's slice on a cash account: paid from the day's money held for it, with no transaction of
                    // its own (MarketEngine.Invest.cs: Hold and Settle). Short of it, nothing is bought.
                    if (order.Held < value + fee)
                    {
                        return false;
                    }
                    order.Held -= value + fee;
                }
                else if (!a.Margin)
                {
                    if (wallet.Cash < value + fee)
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
                        order.Due += value - fee;   // (a worked sale's takings wait on it, paid in one transaction at the close or its end)
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
            // The market moves: a part of the order's impact stays in the price (as the market's mood: it fades), and the
            // player's recent trading in it grows. A worked order's slice moves it by its part of what the order would have
            // as one (SliceShare): its slices together move it as one order of their size, not each as an order of its own.
            double share = order.Worked ? SliceShare(s.Impact, qty, s.Adv) : Math.Sqrt(qty / Math.Max(1.0, s.Adv));
            kept = (buying ? 1.0 : -1.0) * MarketCatalogue.ImpactPermanent * MarketCatalogue.ImpactTemporary * DayVol(state, s) * share;
            if (move)
            {
                s.Mis += kept;
                s.Sent += kept;
                s.DayVolume += qty;
            }
            s.Impact += qty;   // (the player's own recent trading, whatever moves the price)
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

        /// <summary>
        /// The lasting move of a worked order's slice, as a share of the square root law's unit: the square root of the
        /// player's recent trading in the company with the slice, against a day's volume, less without it. The slices of one
        /// worked order so come to the move of one order of their size (less the fading of the trading the market remembers),
        /// as its cost (Impact) already does; an order on its own (nothing traded just before) moves the price as ever.
        /// </summary>
        public static double SliceShare(double recent, long qty, double adv)
        {
            double a = Math.Max(1.0, adv), before = Math.Max(0.0, recent);
            return Math.Sqrt((before + Math.Max(0L, qty)) / a) - Math.Sqrt(before / a);
        }

        // A worked order's slice on the history line of that order's slices of the same day, if there is one: the shares, the
        // money and the commission added, its price their average. False when there is none yet (the slice is a new line).
        // Nothing is made for a slice that joins its line.
        private static bool AddToLine(MarketState state, int order, int stockId, TradeKind kind, long qty, long value, long fee, int phase)
        {
            for (int i = state.Trades.Count - 1; i >= 0; i--)
            {
                Trade t = state.Trades[i];
                if (t.Day != state.Day)
                {
                    return false;   // (lines are in the order of time: none of today's is further back)
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

        /// <summary>
        /// A position changed by a number of shares (more than nothing bought) for an amount of money (what was paid, or
        /// what was received). The cost of what is kept is the average cost of what was held; what is closed is realised.
        /// </summary>
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
            long part = before != 0 ? p.Cost * closing / Math.Abs(before) : 0;   // the cost of what is closed, rounded towards nothing
            long moneyClosing = money * closing / Math.Abs(shares);
            long gain = before > 0 ? moneyClosing - part : part - moneyClosing;
            p.Realised += gain;
            a.Realised += gain;
            p.Cost -= part;
            p.Shares = before + Math.Sign(shares) * closing;
            long rest = Math.Abs(shares) - closing;
            if (rest > 0)
            {
                // Through nothing: the rest opens a position the other way at its share of the money.
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

        /// <summary>What the holdings are worth at the bid, what they cost, and what the gain is, in money.</summary>
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
