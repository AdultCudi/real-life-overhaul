using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Part of the engine: the player's orders and the margin account.</summary>
    public sealed partial class MarketEngine
    {
        /// <summary>
        /// An order the player places now: checked, made, and filled at once if it is at the market while the market trades or
        /// a limit already met. Null with the reason when it cannot be placed.
        /// </summary>
        public Order Place(int stockId, OrderSide side, OrderType type, OrderLife life, long qty, int limitCents, int stopCents, bool open, bool held, bool test, out OrderWhy why,
            bool closing = false)
        {
            Finish();   // Midway
            Stock s = State.Stock(stockId);
            Order order = MarketTrading.Place(State, s, side, type, life, qty, limitCents, stopCents, Wallet.Cash, _world.CommissionLeast, open, held, State.Phase, test, out why, closing);
            if (order == null || !open)
            {
                return order;
            }
            if (type == OrderType.Market || (type == OrderType.Limit && Marketable(s, order)))
            {
                int cents = MarketTrading.MarketPrice(State, s, side == OrderSide.Buy, qty, State.Phase);
                if (type == OrderType.Limit && (side == OrderSide.Buy ? cents > limitCents : cents < limitCents))
                {
                    return order;   // its size moves the price past its limit: it waits
                }
                if (!FillNow(s, order, qty, cents, State.Phase))
                {
                    why = order.Why;
                }
            }
            return order;
        }

        private bool Marketable(Stock s, Order o)
        {
            Quote q = MarketTrading.QuoteOf(State, s, State.Phase);
            return o.Side == OrderSide.Buy ? q.Ask <= o.LimitCents : q.Bid >= o.LimitCents;
        }

        // A worked order's slices after its first pay no least commission: least is then given (MarketEngine.Invest.cs).
        private bool FillNow(Stock s, Order o, long qty, int cents, int phase, int least = -1)
        {
            if (o.Side == OrderSide.Buy && !o.Forced && State.Held(s.Id) + qty > MarketTrading.MostHeld(s))
            {
                // No purchase of the player's own takes a holding past the most, whatever changed since it was placed.
                o.State = o.Filled > 0 ? OrderState.Cancelled : OrderState.Refused;
                o.Why = OrderWhy.HoldingLimit;
                _world.Filled(o, s, 0, 0);
                return false;
            }
            // A trade's lasting move is the city's in a session, and on a replayed day that was played in one: its tape has it.
            bool city = TradesAsEvents || TradeSink != null || (Replaying && Tape != null && Tape.Traded(State.Day));
            if (!MarketTrading.Fill(State, s, o, qty, cents, least >= 0 ? least : _world.CommissionLeast, Wallet, phase, !city, out double kept))
            {
                o.State = o.Filled > 0 ? OrderState.Cancelled : OrderState.Refused;
                o.Why = o.Side == OrderSide.Buy ? (State.Account.Margin ? OrderWhy.BelowMargin : OrderWhy.NoMoney) : (State.Account.Margin ? OrderWhy.BelowMargin : OrderWhy.NoShares);
                _world.Filled(o, s, 0, 0);
                return false;
            }
            if (city)
            {
                Shared(s, kept, qty);
            }
            s.Last = s.MidCents;
            if (!o.Forced && State.Held(s.Id) == 0)
            {
                CloseClosing(s.Id, o.Id);   // nothing held now: a stop loss or take profit left on it has nothing to close
                if (o.Closing && o.IsOpen)
                {
                    o.State = OrderState.Filled;   // it closed what there was: it is done
                }
            }
            _world.Filled(o, s, qty, cents);
            return true;
        }

        // A trade's lasting move in a Going Public session: to the host from a guest, or on the host's tape for the next moment.
        private void Shared(Stock s, double kept, long qty)
        {
            int micros = MarketInputs.TradeMicros(kept);
            int shares = (int)Math.Min(qty, int.MaxValue);
            if (TradeSink != null)
            {
                TradeSink(s.Id, micros, qty);
            }
            else if (!Replaying)
            {
                Event(MarketInputs.EventTrade, s.Id, micros);
                Event(MarketInputs.EventVolume, s.Id, shares);
            }
        }

        // Every open stop loss and take profit on a company but one, cancelled: there is nothing left for them to close.
        private void CloseClosing(int stockId, int but)
        {
            for (int i = 0; i < State.Orders.Count; i++)
            {
                Order other = State.Orders[i];
                if (other.IsOpen && other.Closing && other.StockId == stockId && other.Id != but)
                {
                    other.State = OrderState.Cancelled;
                    other.Why = OrderWhy.NothingHeld;
                }
            }
        }

        // What of a stop loss or take profit can fill now, never more than is still held; with nothing held it is done.
        private long Closable(Order o, long wanted)
        {
            if (!o.Closing)
            {
                return wanted;
            }
            long held = State.Held(o.StockId);
            long holding = o.Side == OrderSide.Sell ? Math.Max(0L, held) : Math.Max(0L, -held);
            long qty = Math.Min(wanted, holding);
            if (qty <= 0)
            {
                o.State = o.Filled > 0 ? OrderState.Filled : OrderState.Cancelled;
                o.Why = o.Filled > 0 ? OrderWhy.None : OrderWhy.NothingHeld;
            }
            return qty;
        }

        /// <summary>The host: a guest's trade, played by every market at the next moment of the grid, bounded as a trade can be.</summary>
        public bool GuestTrade(int stockId, int micros, long shares)
        {
            Finish();   // Midway
            Stock s = State.Stock(stockId);
            if (s == null || !s.Listed || Replaying || shares <= 0)
            {
                return false;
            }
            Event(MarketInputs.EventTrade, stockId, Math.Max(-200000, Math.Min(200000, micros)));
            Event(MarketInputs.EventVolume, stockId, (int)Math.Min(shares, int.MaxValue));
            return true;
        }

        // At the open: orders waiting for it, and resting ones against the open's prices.
        private void OpenOrders(int day)
        {
            for (int i = 0; i < State.Orders.Count; i++)
            {
                Order o = State.Orders[i];
                Stock s = o.IsOpen ? State.Stock(o.StockId) : null;
                if (s == null || !s.Listed)
                {
                    continue;
                }
                Quote q = MarketTrading.QuoteOf(State, s, MarketCatalogue.PhaseOpen);
                bool buying = o.Side == OrderSide.Buy;
                long room = Math.Max(1L, (long)(s.Adv * _weight[1] * 3.0 * MarketCatalogue.ParticipationPct / 100.0));
                switch (o.Type)
                {
                    case OrderType.Market:
                        if (o.Back)
                        {
                            BackNow(o, s, MarketCatalogue.PhaseOpen);   // a dividend put back, if the option is still on
                        }
                        else if (!o.Worked)   // a worked order's slices are the steps'
                        {
                            FillNow(s, o, o.Left, MarketTrading.MarketPrice(State, s, buying, o.Left, MarketCatalogue.PhaseOpen), MarketCatalogue.PhaseOpen);
                        }
                        break;
                    case OrderType.Limit:
                        if (buying ? q.Ask <= o.LimitCents : q.Bid >= o.LimitCents)
                        {
                            long qty = Closable(o, Math.Min(o.Left, room));
                            if (qty > 0)
                            {
                                FillNow(s, o, qty, buying ? q.Ask : q.Bid, MarketCatalogue.PhaseOpen);
                            }
                        }
                        break;
                    case OrderType.Stop:
                        if (buying ? q.Ask >= o.StopCents : q.Bid <= o.StopCents)
                        {
                            // A gap through the stop: it goes at the open's price, whatever the stop was.
                            o.Triggered = true;
                            long qty = Closable(o, o.Left);
                            if (qty > 0)
                            {
                                FillNow(s, o, qty, MarketTrading.MarketPrice(State, s, buying, qty, MarketCatalogue.PhaseOpen), MarketCatalogue.PhaseOpen);
                            }
                        }
                        break;
                }
            }
        }

        // After a step: resting orders against the prices the step really reached.
        private void RestingOrders(int day, int k)
        {
            int phase = MarketCatalogue.PhaseOpen + k;
            for (int i = 0; i < State.Orders.Count; i++)
            {
                Order o = State.Orders[i];
                Stock s = o.IsOpen && o.Type != OrderType.Market ? State.Stock(o.StockId) : null;
                if (s == null || !s.Listed || s.BarCount <= 0)
                {
                    continue;
                }
                Candle bar = s.Bars[s.BarCount - 1];
                double half = MarketTrading.HalfSpread(State, s, phase);
                bool buying = o.Side == OrderSide.Buy;
                long room = Math.Max(1L, (long)(bar.V * MarketCatalogue.ParticipationPct / 100.0));
                int askOpen = (int)Math.Ceiling(bar.O * (1.0 + half)), askLow = (int)Math.Ceiling(bar.L * (1.0 + half)), askHigh = (int)Math.Ceiling(bar.H * (1.0 + half));
                int bidOpen = (int)Math.Floor(bar.O * (1.0 - half)), bidLow = (int)Math.Floor(bar.L * (1.0 - half)), bidHigh = (int)Math.Floor(bar.H * (1.0 - half));
                if (o.Type == OrderType.Limit)
                {
                    if (buying ? askLow <= o.LimitCents : bidHigh >= o.LimitCents)
                    {
                        long qty = Closable(o, Math.Min(o.Left, room));
                        if (qty > 0)
                        {
                            FillNow(s, o, qty, buying ? Math.Min(o.LimitCents, askOpen) : Math.Max(o.LimitCents, Math.Max(1, bidOpen)), phase);
                        }
                    }
                }
                else if (o.Type == OrderType.Stop)
                {
                    // Reached in the step: from then an order at the market, filled at the step's closing price with its move.
                    if (buying ? askHigh >= o.StopCents : bidLow <= o.StopCents)
                    {
                        o.Triggered = true;
                        long qty = Closable(o, o.Left);
                        if (qty > 0)
                        {
                            FillNow(s, o, qty, MarketTrading.MarketPrice(State, s, buying, qty, phase), phase);
                        }
                    }
                }
            }
        }

        // At the close, day orders and orders until cancelled past their time expire; a worked order stands until done.
        private void Expire(int day)
        {
            for (int i = State.Orders.Count - 1; i >= 0; i--)
            {
                Order o = State.Orders[i];
                if (o.IsOpen && o.Life != OrderLife.UntilDone && o.ExpireDay <= day)
                {
                    o.State = OrderState.Expired;
                    o.Why = OrderWhy.Expired;
                }
                if (!o.IsOpen && o.Worked && o.EndDay <= 0)
                {
                    o.EndDay = day;   // cancelled, or its company gone: kept from the day it ended
                }
                // Finished orders are kept a while for the app.
                if (!o.IsOpen && day - Math.Max(o.PlacedDay, o.EndDay) > 10)
                {
                    State.Orders.RemoveAt(i);
                }
            }
        }

        // The margin account

        private void Interest(int day)
        {
            Account a = State.Account;
            if (!a.Margin)
            {
                return;
            }
            double year = Year;
            if (a.Cash < 0)
            {
                double rate = MarketValuation.BankRate(State.Env) + MarketCatalogue.MarginRateOverBp / 10000.0;
                long interest = (long)Math.Ceiling(-a.Cash * Math.Max(0.0, rate) / year * DaysSincePrevious(day));
                if (interest > 0)
                {
                    a.Cash -= interest;
                    a.Interest += interest;
                    MarketTrading.AddTrade(State, new Trade { Day = day, Phase = MarketCatalogue.PhaseClose, Kind = TradeKind.Interest, Value = -interest });
                    _world.Paid(TradeKind.Interest, null, interest);
                }
            }
            for (int i = 0; i < State.Positions.Count; i++)
            {
                Position p = State.Positions[i];
                Stock s = State.Stock(p.StockId);
                if (s == null || p.Shares >= 0)
                {
                    continue;
                }
                long value = MarketTrading.Value(-p.Shares, s.Last, true);
                long fee = (long)Math.Ceiling(value * MarketTrading.BorrowFeeBp(State, s) / 10000.0 / year * DaysSincePrevious(day));
                if (fee > 0)
                {
                    a.Cash -= fee;
                    a.BorrowFees += fee;
                    MarketTrading.AddTrade(State, new Trade { Day = day, Phase = MarketCatalogue.PhaseClose, StockId = s.Id, Kind = TradeKind.BorrowFee, Value = -fee, Test = p.Test });
                    _world.Paid(TradeKind.BorrowFee, s, fee);
                }
            }
        }

        // Calendar days a day's interest is for: since the session before, so a Monday's counts the weekend.
        private static int DaysSincePrevious(int day)
        {
            int days = 1;
            for (int d = day - 1; d >= 1 && !MarketCatalogue.TradingDay(d); d--)
            {
                days++;
            }
            return days;
        }

        /// <summary>
        /// The margin account's health: under maintenance at the close a call is made for the next close; unmet then, or far
        /// under at any moment, the broker closes the largest positions at the market until it is healthy. What is still owed
        /// after everything is sold is taken from the player's cash.
        /// </summary>
        private void Margin(int day, int phase)
        {
            Account a = State.Account;
            if (!a.Margin || State.Positions.Count == 0 && a.Cash >= 0)
            {
                return;
            }
            long equity = MarketTrading.Equity(State, phase);
            long need = MarketTrading.Maintenance(State, phase);
            if (phase == MarketCatalogue.PhaseClose)
            {
                if (equity < need)
                {
                    if (a.CallDay <= 0)
                    {
                        a.CallDay = MarketTrading.MarketDays(day, MarketCatalogue.CallDays);
                        a.CallAmount = need - equity;
                        _world.Called(a.CallAmount, a.CallDay);
                    }
                    else
                    {
                        a.CallAmount = need - equity;
                    }
                }
                else
                {
                    a.CallDay = 0;
                    a.CallAmount = 0;
                }
                return;
            }
            bool forced = (a.CallDay > 0 && day > a.CallDay) || equity * 100 < need * MarketCatalogue.ForcedBelowPct;
            if (!forced && !(State.Positions.Count == 0 && a.Cash < 0))
            {
                return;
            }
            // Sell down, largest first, until the equity is a tenth over what must be kept.
            int guard = 0;
            while (State.Positions.Count > 0 && guard++ < 50)
            {
                equity = MarketTrading.Equity(State, phase);
                need = MarketTrading.Maintenance(State, phase);
                if (equity * 100 >= need * 110 && equity > 0)
                {
                    break;
                }
                Position biggest = null;
                long most = -1;
                for (int i = 0; i < State.Positions.Count; i++)
                {
                    Position p = State.Positions[i];
                    Stock s = State.Stock(p.StockId);
                    long value = s != null ? MarketTrading.Value(Math.Abs(p.Shares), s.Last, false) : 0;
                    if (s != null && s.Listed && value > most)
                    {
                        most = value;
                        biggest = p;
                    }
                }
                if (biggest == null)
                {
                    break;
                }
                Stock target = State.Stock(biggest.StockId);
                var order = new Order
                {
                    Id = State.NextOrderId++, StockId = target.Id, Side = biggest.Shares > 0 ? OrderSide.Sell : OrderSide.Buy, Type = OrderType.Market, Life = OrderLife.Day,
                    Qty = Math.Abs(biggest.Shares), PlacedDay = day, PlacedPhase = phase, ExpireDay = day, State = OrderState.Open, Forced = true, Test = biggest.Test
                };
                State.Orders.Add(order);
                int cents = MarketTrading.MarketPrice(State, target, order.Side == OrderSide.Buy, order.Qty, phase);
                if (!FillNow(target, order, order.Qty, cents, phase))
                {
                    break;
                }
            }
            if (State.Positions.Count == 0 && a.Cash < 0)
            {
                long owed = -a.Cash;
                Wallet.Force(owed, MarketMoney.Shortfall, string.Empty);
                a.Cash = 0;
                MarketTrading.AddTrade(State, new Trade { Day = day, Phase = phase, Kind = TradeKind.Shortfall, Value = -owed });
                _world.Paid(TradeKind.Shortfall, null, owed);
            }
            a.CallDay = 0;
            a.CallAmount = 0;
        }
    }
}
