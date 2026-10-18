using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Part of the market director: what the player asks of the market, orders, the watch list and the margin account.</summary>
    public sealed partial class MarketDirector
    {
        public Estimate Estimate(int stockId, OrderSide side, OrderType type, long qty, int limitCents, int stopCents, bool closing = false)
        {
            if (!Running)
            {
                return new Estimate { Why = OrderWhy.Closed };
            }
            if (Behind)
            {
                return new Estimate { Why = OrderWhy.Behind };
            }
            MarketState s = _engine.State;
            // The money with the broker counts (MarketEngine.Wallet).
            return MarketTrading.Check(s, s.Stock(stockId), side, type, qty, limitCents, stopCents, _engine.Wallet.Cash, CommissionLeast, Trading, _engine.Held, s.Phase, closing);
        }

        /// <summary>An order placed, filled at once if it can be; null with the reason if it cannot be placed.</summary>
        public Order Place(int stockId, OrderSide side, OrderType type, OrderLife life, long qty, int limitCents, int stopCents, bool test, out OrderWhy why, bool closing = false)
        {
            FinishMoment();
            why = OrderWhy.Closed;
            if (!Running)
            {
                return null;
            }
            if (Behind)
            {
                why = OrderWhy.Behind;
                return null;
            }
            if (!Settings.MarketsMargin && _engine.State.Account.Margin && !closing && !Closes(stockId, side, qty))
            {
                why = OrderWhy.NoMargin;   // with margin trading off an open account is only wound down
                return null;
            }
            try
            {
                Order order = _engine.Place(stockId, side, type, life, qty, limitCents, stopCents, Trading, _engine.Held, test, out why, closing);
                if (Verbose && order != null)
                {
                    Logger?.Info("Stock market check (order " + order.Id + "): " + side + " " + qty + " of " + stockId + " (" + type + (closing ? ", closing" : string.Empty) + ", " + life + ") is "
                                 + order.State + ": PASS");
                }
                Moved();
                return order;
            }
            catch (Exception e)
            {
                Fault("placing an order", e);
                return null;
            }
        }

        private bool Closes(int stockId, OrderSide side, long qty)
        {
            long held = _engine.State.Held(stockId);
            return side == OrderSide.Buy ? held < 0 && qty <= -held : held > 0 && qty <= held;
        }

        /// <summary>An open order cancelled; false if it is not open or orders are held after a load.</summary>
        public bool Cancel(int orderId)
        {
            FinishMoment();
            if (!Running || Behind || _engine.Held)
            {
                return false;
            }
            bool done = _engine.Cancel(orderId);   // a worked order's money with the broker is settled at once
            Moved();
            return done;
        }

        /// <summary>A company on or off the watch list; false if the list is full.</summary>
        public bool Watch(int stockId, bool on)
        {
            FinishMoment();
            MarketState s = State;
            if (s == null || s.Stock(stockId) == null)
            {
                return false;
            }
            if (!on)
            {
                s.Watch.Remove(stockId);
            }
            else if (!s.Watch.Contains(stockId))
            {
                if (s.Watch.Count >= MarketCatalogue.WatchMost)
                {
                    return false;
                }
                s.Watch.Add(stockId);
            }
            Moved();
            return true;
        }

        /// <summary>From the player's cash; the first payment opens the account, at least the opening sum.</summary>
        public bool Deposit(long amount)
        {
            FinishMoment();
            MarketState s = State;
            if (s == null || amount <= 0 || !Settings.MarketsMargin || Behind || _engine.Held)
            {
                return false;
            }
            Account a = s.Account;
            if (!a.Margin && amount < Prices.At(MarketCatalogue.MarginOpenLeastAtStart))
            {
                return false;
            }
            if (!_engine.Wallet.Take(amount, MarketMoney.Deposit, string.Empty))   // takes back from worked orders' money first
            {
                return false;
            }
            if (!a.Margin)
            {
                a.Margin = true;
                a.Cash = 0;
                a.OpenedDay = s.Day;
            }
            a.Cash += amount;
            MarketTrading.AddTrade(s, new Trade { Day = s.Day, Phase = s.Phase, Kind = TradeKind.Deposit, Value = amount });
            Moved();
            return true;
        }

        /// <summary>While what is held keeps its initial margin.</summary>
        public long MostWithdrawable()
        {
            MarketState s = State;
            if (s == null || !s.Account.Margin || s.Account.Cash <= 0)
            {
                return 0;
            }
            long gross = MarketTrading.GrossExposure(s, s.Phase);
            long equity = MarketTrading.Equity(s, s.Phase);
            long free = equity - (gross * MarketCatalogue.InitialMarginPct + 99) / 100;
            return Math.Max(0, Math.Min(s.Account.Cash, free));
        }

        /// <summary>It closes when empty with nothing borrowed.</summary>
        public bool Withdraw(long amount)
        {
            FinishMoment();
            MarketState s = State;
            if (s == null || amount <= 0 || Behind || _engine.Held || amount > MostWithdrawable())
            {
                return false;
            }
            Account a = s.Account;
            a.Cash -= amount;
            _world.Wallet.Give(amount, MarketMoney.Withdrawal, string.Empty);
            MarketTrading.AddTrade(s, new Trade { Day = s.Day, Phase = s.Phase, Kind = TradeKind.Withdrawal, Value = -amount });
            if (a.Cash == 0 && s.Positions.Count == 0)
            {
                a.Margin = false;
                a.CallDay = 0;
                a.CallAmount = 0;
            }
            Moved();
            return true;
        }
    }
}
