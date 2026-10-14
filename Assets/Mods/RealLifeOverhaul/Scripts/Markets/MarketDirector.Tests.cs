using System;
using System.Text;

namespace RealLifeOverhaul.Markets
{
    public sealed partial class MarketDirector
    {
        // ---------------------------------------------------------------- for the save tools

        /// <summary>
        /// For the save tools ("prepare this save for removing the mod", "reset the mod's data"): shares do not exist in
        /// the plain game, so every holding is sold at today's bid and every short bought back at the offer, with no
        /// commission; a margin account pays back what it holds (or what it owes is taken from the cash), worked orders' money
        /// with the broker comes back, and every order goes. Each is listed in the log. Then the market stops; the timer
        /// starts it again while the option is on.
        /// </summary>
        public void StopAll()
        {
            FinishMoment();
            try
            {
                if (_running && Save != null && ReferenceEquals(Save, _save))
                {
                    MarketState s = _engine.State;
                    var log = new StringBuilder(256);
                    long total = InvestRules.EmptyBroker(s, out long parked, out long dueThen);   // worked orders' money with the broker, first: nothing after gives it back twice
                    if (total > 0)
                    {
                        log.Append(" the money with the broker for worked orders ").Append(total).Append(" (").Append(parked).Append(" parked, ").Append(dueThen).Append(" due);");
                    }
                    for (int i = s.Positions.Count - 1; i >= 0; i--)
                    {
                        Position p = s.Positions[i];
                        Stock k = s.Stock(p.StockId);
                        if (k == null || p.Test)
                        {
                            continue;
                        }
                        Quote q = MarketTrading.QuoteOf(s, k, s.Phase);
                        long value = p.Shares > 0 ? MarketTrading.Value(p.Shares, q.Bid, false) : -MarketTrading.Value(-p.Shares, q.Ask, true);
                        total += value;
                        log.Append(' ').Append(p.Shares).Append(" of ").Append(k.Name).Append(" for ").Append(value).Append(';');
                    }
                    for (int i = 0; i < s.Dues.Count; i++)
                    {
                        DividendDue due = s.Dues[i];
                        long money = due.Cents >= 0 ? due.Cents / 100 : -((-due.Cents + 99) / 100);
                        if (!due.Test)
                        {
                            total += money;
                            log.Append(" a dividend of ").Append(money).Append(';');
                        }
                    }
                    if (s.Account.Margin)
                    {
                        total += s.Account.Cash;
                        log.Append(" the margin account's ").Append(s.Account.Cash).Append(';');
                    }
                    total += OwnSettled(log);   // the player's own listed company taken private at its last price (MarketDirector.Own.cs)
                    total += BotsSettled(log);  // the player's bots' shares (MarketDirector.Bots.cs)
                    total += BondsSettled(log); // the player's bonds (MarketDirector.Bonds.cs)
                    if (total > 0)
                    {
                        _world.Wallet.Give(total, MarketMoney.Sold, string.Empty);
                    }
                    else if (total < 0)
                    {
                        _world.Wallet.Force(-total, MarketMoney.Shortfall, string.Empty);
                    }
                    s.Positions.Clear();
                    s.Orders.Clear();
                    s.Dues.Clear();
                    s.Account.Margin = false;
                    s.Account.Cash = 0;
                    Logger?.Warn("Stock market: leaving this save. Everything held settled for " + total + " in all, with no commission." + log);
                }
                StandDown(true);
            }
            catch (Exception e)
            {
                Fault("the save tools", e);
            }
        }

        /// <summary>For the save tools: the save's line has been removed, and so is what was read from it.</summary>
        public void Forget()
        {
            FinishMoment();
            _running = false;
            _engine = null;
            _save = null;
            _notes.Clear();
            Behind = false;
            RemoveApp();
        }

        // ---------------------------------------------------------------- the owner's test buttons

        /// <summary>Test button: the Stocks app opened on the market's tab.</summary>
        public void TestOpen()
        {
            FinishMoment();
            const string Test = "Open the Stocks app";
            if (!TestReady(Test, false))
            {
                return;
            }
            if (_view == null)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_mkt_test_no_app");
                return;
            }
            _view.Want(MarketApp.TabMarket);
            OpenAppPartial();
            Logger?.Info(Test + " test: the app is open on the market (" + State.Stocks.Count + " companies, day " + State.Day + ", phase " + State.Phase + ").");
        }

        /// <summary>
        /// Test button: a crisis (or a windfall) at a company the player holds, else one they watch, else the largest
        /// listed: the news is out at once, as a real one is (the notification, the app, tomorrow's City Ledger), and the
        /// price follows at the next open.
        /// </summary>
        public void TestNews(bool crisis)
        {
            FinishMoment();
            string test = crisis ? "A crisis at a company" : "A windfall at a company";
            if (!TestReady(test, true))
            {
                return;
            }
            Stock k = TestCompany();
            if (k == null || !_engine.NewsFor(k.Id, crisis))
            {
                TestRefusals.TellKey(Logger, test, "reallife_mkt_test_no_company");
                return;
            }
            Logger?.Info(test + " test: " + k.Name + " (" + k.Ticker + "), outlook now " + k.Outlook.ToString("F3") + "; the price follows at the next open.");
            OpenCompany(k.Id);
        }

        /// <summary>Test button: a takeover bid for a company the player holds, else one they watch, else the largest listed, made as a real one is.</summary>
        public void TestBid()
        {
            FinishMoment();
            const string Test = "A takeover bid";
            if (!TestReady(Test, true))
            {
                return;
            }
            Stock k = TestCompany();
            if (k == null || !_engine.BidFor(k.Id))
            {
                TestRefusals.TellKey(Logger, Test, "reallife_mkt_test_no_company");
                return;
            }
            Logger?.Info(Test + " test: " + k.Name + " (" + k.Ticker + ") bid for at " + k.BidCents + " cents a share, to complete on day " + k.BidEndDay
                         + (k.BidderId > 0 ? " by " + (State.Stock(k.BidderId) != null ? State.Stock(k.BidderId).Name : "?") : " by a private buyer") + ".");
            OpenCompany(k.Id);
        }

        // A company a test can work on: listed and not under a bid, the player's own first, then a watched one, then the largest.
        private Stock TestCompany()
        {
            MarketState s = State;
            for (int i = 0; i < s.Positions.Count; i++)
            {
                Stock k = s.Stock(s.Positions[i].StockId);
                if (k != null && k.Listed && k.BidCents == 0)
                {
                    return k;
                }
            }
            for (int i = 0; i < s.Watch.Count; i++)
            {
                Stock k = s.Stock(s.Watch[i]);
                if (k != null && k.Listed && k.BidCents == 0)
                {
                    return k;
                }
            }
            Stock largest = null;
            for (int i = 0; i < s.Stocks.Count; i++)
            {
                Stock k = s.Stocks[i];
                if (k.Listed && k.BidCents == 0 && (largest == null || k.MarketValue > largest.MarketValue))
                {
                    largest = k;
                }
            }
            return largest;
        }

        private bool TestReady(string test, bool settled)
        {
            if (!Settings.Markets)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.FeatureOff);
                return false;
            }
            if (!Running || Save == null)
            {
                TestRefusals.Tell(Logger, test, TestRefusal.NotLoaded);
                return false;
            }
            if (settled && (Behind || _engine.Replaying))
            {
                TestRefusals.TellKey(Logger, test, "reallife_mkt_test_busy");
                return false;
            }
            return true;
        }
    }
}
