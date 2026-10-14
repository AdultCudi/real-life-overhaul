using System;
using UI.Notification;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// A large investor's tools in the game (InvestRules, MarketEngine.Invest.cs): worked orders placed from a company's
    /// page and told as they work (a day's part at the close, done or stopped as it comes), the share of each dividend a
    /// holding puts back, what the player is worth for the most of one bond purchase (MarketDirector.Bonds.cs), the options
    /// looked at once a second (one switched off: what is open of it is cancelled, what it bought or sold kept), and the
    /// tests. Nothing here runs per frame: the orders work on the market's own steps.
    ///
    /// IN A GOING PUBLIC SESSION worked orders and dividends put back are the player's own orders: they fill on the
    /// player's own machine as the market's steps are played, their moves of the city's prices go to the host as every
    /// fill's do (MarketEngine.TradeSink), nothing new is sent, and what the player is worth is read from their own game.
    /// </summary>
    public sealed partial class MarketDirector
    {
        private Action<InvestNote> _investTold;

        // Every market engine made (a load, or a Going Public host's market taken on) tells its worked orders here, and
        // follows the options.
        private void HookInvest()
        {
            if (_investTold == null)
            {
                _investTold = InvestHeard;
            }
            _engine.InvestTold = _investTold;
            FollowOptions();
        }

        // The options the engine follows: worked orders working, dividends put back, bonds rolling over.
        private void FollowOptions()
        {
            _engine.WorkedOn = Settings.MarketsLarge;
            _engine.ReinvestOn = Settings.MarketsReinvest;
            _engine.BondsOn = Settings.MarketsBonds;
        }

        // Once a second: the options; one switched off, what is open of it is cancelled (once the market is caught up).
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

        // ---------------------------------------------------------------- what the player does

        /// <summary>Whether a worked order could be placed now (nothing is changed): None, or the reason.</summary>
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
                return OrderWhy.NoMargin;   // (with margin trading off an account that is open is only wound down)
            }
            return _engine.WorkedWhy(_engine.State.Stock(stockId), side, qty, _engine.Held);
        }

        /// <summary>A worked order placed: its first slice at the market's next step. Null with the reason when it cannot be.</summary>
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

        /// <summary>The share of each dividend a holding of shares puts back into the same company at the next open, from its next one on.</summary>
        public bool SetReinvest(int stockId, int pct, out OrderWhy why)
        {
            FinishMoment();
            why = !Running ? OrderWhy.Closed : Behind ? OrderWhy.Behind : !Settings.MarketsReinvest ? OrderWhy.Switched : OrderWhy.None;
            bool done = why == OrderWhy.None && _engine.SetReinvest(stockId, pct, out why);
            Moved();
            return done;
        }

        /// <summary>
        /// What the player is worth, in money: the game's own figure (cash, its investment funds, vehicles, boats and property,
        /// less loans) and what they hold on the market (InvestRules.MarketWorth). Read on a click, never per frame.
        /// </summary>
        public long PlayerWorth()
        {
            return Running ? MarketWallet.Wealth() + InvestRules.MarketWorth(_engine.State, _engine.State.Phase) : 0L;
        }

        /// <summary>The player's cash in hand, as the market reads it (the Portfolio tab's box).</summary>
        public long Cash => Running ? _world.Wallet.Cash : 0L;

        // ---------------------------------------------------------------- what worked orders tell

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
                Logger?.Info("Stock market check (worked order " + n.Order + ", day " + s.Day + "): " + n.Kind + ", " + n.Qty + " today, " + n.Done + " of " + n.Total + " in all; the day's largest slice "
                             + n.SlicePct + "% of its step's trading, at most " + MarketCatalogue.OrderShareOfVolumePct + "% wanted: "
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

        // The average price of so much money for so many shares, in cents.
        private static int Average(long money, long shares)
        {
            return shares > 0 ? (int)Math.Min(int.MaxValue, Math.Abs(money) * 100L / shares) : 0;
        }

        // ---------------------------------------------------------------- the tests

        /// <summary>Test button: a market day's slices of every open worked order played now, and each order's day told as at a close.</summary>
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

        /// <summary>Test button: the next dividend owed on the player's own shares paid now, as on its day, and its share put back (at once while the market trades).</summary>
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
            Stock k = State.Stock(stock);
            Position p = State.Position(stock);
            Logger?.Info(Test + " test: the dividend owed on " + (k != null ? k.Name : stock.ToString()) + " paid now; "
                         + (p != null && p.Reinvest > 0 && Settings.MarketsReinvest ? p.Reinvest + "% of it put back " + (Trading ? "at once" : "at the next open") : "nothing put back (the holding puts back none)")
                         + ".");
            Moved();
        }
    }
}
