using System;
using UI.Notification;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Part of the market director: a large investor's tools, worked orders, dividends put back and the options looked at
    /// once a second. They are the player's own orders, filled on their own machine; nothing new is sent. Nothing per frame.
    /// </summary>
    public sealed partial class MarketDirector
    {
        private Action<InvestNote> _investTold;

        // Every engine made, at a load or a host's market taken on, tells its worked orders here and follows the options.
        private void HookInvest()
        {
            if (_investTold == null)
            {
                _investTold = InvestHeard;
            }
            _engine.InvestTold = _investTold;
            FollowOptions();
        }

        private void FollowOptions()
        {
            _engine.LargeOn = Settings.MarketsLarge;
            _engine.ReinvestOn = Settings.MarketsReinvest;
            _engine.BondsOn = Settings.MarketsBonds;
        }

        // Once a second: the options; one switched off, what is open of it is cancelled once the market is caught up.
        private void InvestSecond()
        {
            FollowOptions();
            if (Behind || _engine.Held || (Settings.MarketsLarge && Settings.MarketsReinvest))
            {
                return;
            }
            int stopped = (Settings.MarketsLarge ? 0 : _engine.StopAllOf(false, OrderWhy.Switched)) + (Settings.MarketsReinvest ? 0 : _engine.StopAllOf(true, OrderWhy.Switched));
            if (stopped > 0)
            {
                Logger?.Info("Stock market: " + stopped + " worked order(s) or dividend(s) to put back cancelled, as their option is off; what they bought or sold is kept.");
                Moved();
            }
        }

        public OrderWhy WorkedWhy(int stockId, OrderSide side, long qty)
        {
            if (!Running)
            {
                return OrderWhy.Closed;
            }
            if (Behind)
            {
                return OrderWhy.Behind;
            }
            if (!Settings.MarketsLarge)
            {
                return OrderWhy.Switched;
            }
            if (!Settings.MarketsMargin && _engine.State.Account.Margin && !Closes(stockId, side, qty))
            {
                return OrderWhy.NoMargin;   // with margin trading off an open account is only wound down
            }
            return _engine.WorkedWhy(_engine.State.Stock(stockId), side, qty, _engine.Held);
        }

        public Order PlaceWorked(int stockId, OrderSide side, long qty, out OrderWhy why)
        {
            FinishMoment();
            why = WorkedWhy(stockId, side, qty);
            if (why != OrderWhy.None)
            {
                return null;
            }
            try
            {
                Order order = _engine.PlaceWorked(stockId, side, qty, _engine.Held, false, out why);
                if (order != null)
                {
                    Stock k = _engine.State.Stock(stockId);
                    Logger?.Info("Stock market: a worked order placed (" + order.Id + "): " + side + " " + qty + " of " + (k != null ? k.Name : stockId.ToString()) + ", about "
                                 + InvestRules.MarketDays(qty, k != null ? k.Adv : 1.0) + " market day(s) at a fifth of each step's trading.");
                }
                Moved();
                return order;
            }
            catch (Exception e)
            {
                Fault("placing a worked order", e);
                return null;
            }
        }

        /// <summary>The share of each dividend put back into the same company at the next open, from its next one.</summary>
        public bool SetReinvest(int stockId, int pct, out OrderWhy why)
        {
            FinishMoment();
            why = !Running ? OrderWhy.Closed : Behind ? OrderWhy.Behind : !Settings.MarketsReinvest ? OrderWhy.Switched : OrderWhy.None;
            bool done = why == OrderWhy.None && _engine.SetReinvest(stockId, pct, out why);
            Moved();
            return done;
        }

        /// <summary>The Finance app's net worth, which counts nothing on the market, plus what is held on the market. Read on a click.</summary>
        public long PlayerWorth()
        {
            if (!Running)
            {
                return 0L;
            }
            Func<long> net = MarketBridges.NetWorth;
            return (net != null ? net() : MarketWallet.Wealth()) + InvestRules.MarketWorth(_engine.State, _engine.State.Phase);
        }

        public long Cash => Running ? _world.Wallet.Cash : 0L;

        private void InvestHeard(InvestNote n)
        {
            MarketState s = _engine != null ? _engine.State : null;
            if (s == null)
            {
                return;
            }
            Stock k = s.Stock(n.Stock);
            string name = k != null ? k.Name : string.Empty;
            if (Verbose)
            {
                // One line an event: its day's largest slice against the share of a step's trading one order may take.
                Logger?.Info("Stock market check (worked order " + n.Order + ", day " + s.Day + "): " + n.Kind + ", " + n.Qty + " today, " + n.Done + " of " + n.Total
                             + " in all; the day's largest slice " + n.SlicePct + "% of its step's trading, at most " + MarketCatalogue.OrderShareOfVolumePct + "% wanted: "
                             + (n.SlicePct <= MarketCatalogue.OrderShareOfVolumePct && n.Done <= n.Total ? "PASS" : "a slice over the limit"));
            }
            switch (n.Kind)
            {
                case InvestNoteKind.Day:
                    Queue(NotificationType.Info, n.Buy ? "reallife_mkt_inv_note_day_buy" : "reallife_mkt_inv_note_day_sell", "name", name, "count", Count(n.Qty),
                        "price", MarketText.Price(Average(n.Value, n.Qty)), "done", Count(n.Done), "total", Count(n.Total));
                    break;
                case InvestNoteKind.Done:
                    Queue(NotificationType.Success, n.Buy ? "reallife_mkt_inv_note_done_buy" : "reallife_mkt_inv_note_done_sell", "name", name, "total", Count(n.Total),
                        "price", MarketText.Price(Average(n.Filled, n.Done)));
                    break;
                default:
                    Queue(NotificationType.Warning, "reallife_mkt_inv_note_stopped", "name", name, "done", Count(n.Done), "total", Count(n.Total), "why", MarketText.Why(n.Why));
                    break;
            }
        }

        private static int Average(long money, long shares)
        {
            return shares > 0 ? (int)Math.Min(int.MaxValue, Math.Abs(money) * 100L / shares) : 0;
        }

        /// <summary>Test button: a market day's slices of every open worked order played now, each order's day told as at a close.</summary>
        public void TestWorkedDay()
        {
            FinishMoment();
            const string Test = "A day of my worked orders done now";
            if (!TestReady(Test, true))
            {
                return;
            }
            if (!Trading)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_mkt_inv_test_shut");
                return;
            }
            int worked = _engine.WorkDayNow();
            if (worked <= 0)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_mkt_inv_test_no_worked");
                return;
            }
            Logger?.Info(Test + " test: " + worked + " worked order(s) played for a market day now.");
            Moved();
        }

        /// <summary>Test button: the next dividend owed on a holding that puts some back, paid now and that share bought.</summary>
        public void TestPutBack()
        {
            FinishMoment();
            const string Test = "My next dividend paid now and put back";
            if (!TestReady(Test, true))
            {
                return;
            }
            int stock = _engine.PayNextDueNow();
            if (stock == 0)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_mkt_inv_test_no_due");
                return;
            }
            if (stock < 0)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_mkt_inv_test_no_share");   // owed, but on no holding that puts any back: none is paid early
                return;
            }
            Stock k = State.Stock(stock);
            Position p = State.Position(stock);
            Logger?.Info(Test + " test: the dividend owed on " + (k != null ? k.Name : stock.ToString()) + " paid now; " + (p != null ? p.Reinvest : 0) + "% of it put back "
                         + (Trading ? "at once" : "at the next open") + ".");
            Moved();
        }
    }
}
