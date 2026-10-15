using System;
using UI.Notification;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The player's trading bots in the game (MarketBots, MarketEngine.Bots.cs): hiring and letting go, their settings,
    /// and what they do told as the game's notifications once the frame's work is done. Nothing here runs per frame:
    /// the bots trade on the market's own steps, and the option is looked at once a second.
    ///
    /// IN A GOING PUBLIC SESSION a player's bots are theirs, as their holdings are: they live in their own save's line,
    /// a guest's come with them into the host's market (MarketShare.Adopt) and nothing of them is sent. Their trades
    /// move the city's prices as the player's own do, through the same messages a player's trade already uses.
    /// </summary>
    public sealed partial class MarketDirector
    {
        private int _botsSelling = -1;          // with the option off: bots still selling shares of the player's own company (told when it changes)
        private DividendTally _paid;            // dividends paid since the player was last told (one notification for them all)

        // Once a second: with the option off, every bot is let go (its shares become the player's own); one holding shares
        // of the player's own company sells them up by its founder's rules first, and is let go once it has.
        private void BotsSecond()
        {
            if (Settings.MarketsBots || _engine.State.Bots.Count == 0 || Behind || _engine.Held)
            {
                _botsSelling = Settings.MarketsBots ? -1 : _botsSelling;
                return;
            }
            int selling = 0;
            bool changed = false;
            for (int i = _engine.State.Bots.Count - 1; i >= 0; i--)
            {
                Bot b = _engine.State.Bots[i];
                if (_engine.Dismiss(b.Firm, out BotWhy why))
                {
                    changed = true;
                }
                else if (why == BotWhy.OwnShares && b.Status != BotStatus.SellingUp)
                {
                    changed |= _engine.SellUp(b.Firm, out _);   // (shares of the player's own company: sold by its rules, then let go here)
                }
                selling += why == BotWhy.OwnShares ? 1 : 0;
            }
            if (selling != _botsSelling)
            {
                _botsSelling = selling;
                changed = true;
                Logger?.Info("Stock market: trading bots switched off; every bot was let go and what it held is the player's own now"
                             + (selling > 0 ? ", but for " + selling + " selling its shares of the player's own company first, by its founder's rules." : "."));
            }
            if (changed)
            {
                Moved();   // (only when a bot went or began to sell up: a bot still selling, as it was, changes nothing the app shows)
            }
        }

        // What every ask of a bot needs: the market running, caught up and not held after a load.
        private BotWhy BotReady()
        {
            return !Running ? BotWhy.Closed : Behind ? BotWhy.Behind : _engine.Held ? BotWhy.Held : BotWhy.None;
        }

        /// <summary>A firm's bot hired with so much of the player's cash to trade and its settings. Null with the reason when it cannot be.</summary>
        public Bot HireBot(int firm, long capital, int size, int stop, int loss, out BotWhy why)
        {
            FinishMoment();
            why = !Settings.MarketsBots ? BotWhy.Off : BotReady();
            if (why != BotWhy.None)
            {
                return null;
            }
            try
            {
                Bot b = _engine.Hire(firm, capital, size, stop, loss, out why);
                if (b != null)
                {
                    Logger?.Info("Stock market: " + b.Def.Name + " hired with " + capital + " to trade (" + b.Size + "% a holding, stop " + b.Stop + "%, the most it may lose " + b.Loss + "%).");
                }
                Moved();
                return b;
            }
            catch (Exception e)
            {
                Fault("hiring a bot", e);
                return null;
            }
        }

        public bool SetBot(int firm, int size, int stop, int loss, out BotWhy why)
        {
            FinishMoment();
            why = BotReady();
            bool done = why == BotWhy.None && _engine.SetBot(firm, size, stop, loss, out why);
            Moved();
            return done;
        }

        /// <summary>More of the player's cash given to a running bot to trade (MarketEngine.AddCapital). False with the reason.</summary>
        public bool AddBotCapital(int firm, long amount, out BotWhy why)
        {
            FinishMoment();
            why = !Settings.MarketsBots ? BotWhy.Off : BotReady();
            Bot b = why == BotWhy.None ? MarketBots.Hired(_engine.State, firm) : null;
            long worth = b != null ? MarketBots.Worth(_engine.State, b, _engine.State.Phase) : 0, cash = _engine.Wallet.Cash, mark = b != null ? b.HighWater : 0;
            bool done = why == BotWhy.None && _engine.AddCapital(firm, amount, out why);
            if (done)
            {
                Logger?.Info("Stock market: " + amount + " added to " + b.Def.Name + "'s capital, now " + b.Capital + " (nothing sold; it stops under " + MarketBots.Floor(b) + ").");
                CapitalCheck(b, amount, worth, cash, mark);
            }
            Moved();
            return done;
        }

        // Developer diagnostics: capital added or taken out (less than nothing) moved the bot's worth and its high-water
        // mark by the sum and the player's cash not at all.
        private void CapitalCheck(Bot b, long amount, long worth, long cash, long mark)
        {
            if (!Verbose)
            {
                return;
            }
            long now = MarketBots.Worth(_engine.State, b, _engine.State.Phase), cashNow = _engine.Wallet.Cash;
            bool pass = now == worth + amount && cashNow == cash && b.HighWater == mark + amount;
            Logger?.Info("Stock market check (bot " + b.Def.Name + " capital " + (amount > 0 ? "added" : "taken out") + "): worth " + worth + " to " + now + " (wanted " + (worth + amount)
                         + "), the player's cash " + cash + " to " + cashNow + " (wanted the same), high-water mark " + mark + " to " + b.HighWater + ": "
                         + (pass ? "PASS" : "the bot's figures and the player's books do not agree"));
        }

        /// <summary>Capital taken out of a running bot's money not in shares (MarketEngine.TakeCapital). False with the reason.</summary>
        public bool TakeBotCapital(int firm, long amount, out BotWhy why)
        {
            FinishMoment();
            why = !Settings.MarketsBots ? BotWhy.Off : BotReady();
            Bot b = why == BotWhy.None ? MarketBots.Hired(_engine.State, firm) : null;
            long worth = b != null ? MarketBots.Worth(_engine.State, b, _engine.State.Phase) : 0, cash = _engine.Wallet.Cash, mark = b != null ? b.HighWater : 0;
            bool done = why == BotWhy.None && _engine.TakeCapital(firm, amount, out why);
            if (done)
            {
                Logger?.Info("Stock market: " + amount + " taken out of " + b.Def.Name + "'s capital, now " + b.Capital + " (nothing sold; it stops under " + MarketBots.Floor(b) + ").");
                CapitalCheck(b, -amount, worth, cash, mark);
            }
            Moved();
            return done;
        }

        /// <summary>The least added to a running bot or taken out at a time, and the most that can be taken out of one now; 0 while the market is not running.</summary>
        public long BotChangeLeast => Running ? _engine.BotChangeLeast : 0;

        public long BotMostOut(int firm) => Running ? _engine.BotMostOut(firm) : 0;

        public bool SellUpBot(int firm, out BotWhy why)
        {
            FinishMoment();
            why = BotReady();
            bool done = why == BotWhy.None && _engine.SellUp(firm, out why);
            Moved();
            return done;
        }

        public bool RestartBot(int firm, out BotWhy why)
        {
            FinishMoment();
            why = !Settings.MarketsBots ? BotWhy.Off : BotReady();
            bool done = why == BotWhy.None && _engine.Restart(firm, out why);
            Moved();
            return done;
        }

        public bool DismissBot(int firm, out BotWhy why)
        {
            FinishMoment();
            why = BotReady();
            bool done = why == BotWhy.None && _engine.Dismiss(firm, out why);
            Moved();
            return done;
        }

        /// <summary>The shares a bot trades: its strategy's market, the companies picked for it, or those the player holds.</summary>
        public bool SetBotScope(int firm, BotScope scope, out BotWhy why)
        {
            FinishMoment();
            why = BotReady();
            bool done = why == BotWhy.None && _engine.SetScope(firm, scope, out why);
            Moved();
            return done;
        }

        /// <summary>A company picked for a bot, or taken off its picks.</summary>
        public bool PickForBot(int firm, int stockId, bool on, out BotWhy why)
        {
            FinishMoment();
            why = BotReady();
            bool done = why == BotWhy.None && _engine.PickFor(firm, stockId, on, out why);
            Moved();
            return done;
        }

        /// <summary>A bot's payouts to the player: the share of its new profit and how often.</summary>
        public bool SetBotPayout(int firm, int pct, BotPayEvery every, out BotWhy why)
        {
            FinishMoment();
            why = BotReady();
            bool done = why == BotWhy.None && _engine.SetPayout(firm, pct, every, out why);
            Moved();
            return done;
        }

        /// <summary>The least a bot may be given now; 0 while the market is not running.</summary>
        public long BotCapitalLeast => Running ? _engine.BotCapitalLeast : 0;

        // ---------------------------------------------------------------- what the bots tell

        private void BotHeard(BotNote n)
        {
            BotFirm f = MarketBots.Firm(n.Firm);
            MarketState s = _engine != null ? _engine.State : null;
            if (f == null || s == null)
            {
                return;
            }
            Stock k = n.Stock > 0 ? s.Stock(n.Stock) : null;
            string name = k != null ? k.Name : string.Empty;
            Bot hired = MarketBots.Hired(s, n.Firm);
            if (Verbose && hired != null && n.Kind != BotNoteKind.Dividend)
            {
                // One line an event: what it did, and that its money and holdings add up to its worth.
                long worth = MarketBots.Worth(s, hired, s.Phase);
                long held = worth - hired.Cash;
                Logger?.Info("Stock market check (bot " + f.Name + ", day " + s.Day + " phase " + s.Phase + "): " + n.Kind + " " + n.A + "/" + n.B + "/" + n.C + "; worth " + worth + " = money "
                             + hired.Cash + " + " + hired.Holdings.Count + " holding(s) " + held + "; fees " + hired.Fees + ", trades " + hired.Trades + ": "
                             + (held >= 0 && (hired.Holdings.Count > 0 || held == 0) ? "PASS" : "the holdings are worth less than nothing"));
            }
            switch (n.Kind)
            {
                case BotNoteKind.Round:
                    Queue(NotificationType.Info, "reallife_mkt_bot_note_round", "firm", f.Name, "bought", n.A.ToString(), "sold", n.B.ToString(), "worth", MarketText.Money(n.C));
                    break;
                case BotNoteKind.Stop:
                    Queue(NotificationType.Warning, "reallife_mkt_bot_note_stop", "firm", f.Name, "name", name, "count", n.Qty.ToString("N0"), "stop", MarketText.Price((int)n.A),
                        "price", MarketText.Price(n.Cents));
                    break;
                case BotNoteKind.Limit:
                    Queue(NotificationType.Error, "reallife_mkt_bot_note_limit", "firm", f.Name, "worth", MarketText.Money(n.A));
                    break;
                case BotNoteKind.Statement:
                    Queue(NotificationType.Info, n.D > 0 ? "reallife_mkt_bot_note_statement_paid" : "reallife_mkt_bot_note_statement", "firm", f.Name, "worth", MarketText.Money(n.A),
                        "change", MarketText.Change((int)n.B), "count", n.Qty.ToString(), "fees", MarketText.Money(n.C), "paid", MarketText.Money(n.D));
                    break;
                case BotNoteKind.Paid:
                    Queue(NotificationType.Success, n.B > 0 ? "reallife_mkt_bot_note_paid_owed" : "reallife_mkt_bot_note_paid", "firm", f.Name, "amount", MarketText.Money(n.A),
                        "owed", MarketText.Money(n.B));
                    break;
                case BotNoteKind.Short:
                    Queue(NotificationType.Warning, "reallife_mkt_bot_note_short", "firm", f.Name, "amount", MarketText.Money(n.A));
                    break;
                case BotNoteKind.SoldUp:
                    Queue(NotificationType.Info, "reallife_mkt_bot_note_sold_up", "firm", f.Name, "worth", MarketText.Money(n.A));
                    break;
                case BotNoteKind.Payout:
                    Queue(NotificationType.Info, "reallife_mkt_bot_note_payout", "firm", f.Name, "name", name, "amount", MarketText.Money(n.A));
                    break;
                case BotNoteKind.Dividend:
                    {
                        // The player's dividend: taxed as their own (from their cash, and so from the bot's money), without a notification for each.
                        long tax = DividendTax(k, n.A, true);
                        Bot b = MarketBots.Hired(s, n.Firm);
                        if (b != null)
                        {
                            b.Cash -= tax;
                            b.Dividends -= tax;
                        }
                        _paid.Add(n.Stock, n.Firm, n.A);
                    }
                    break;
                case BotNoteKind.Dismissed:
                    Queue(NotificationType.Info, "reallife_mkt_bot_note_dismissed", "firm", f.Name, "fees", MarketText.Money(n.A), "count", n.B.ToString());
                    break;
            }
        }

        // The dividends paid since the last frame, told in one notification: the one with its company (and its bot), or
        // how many and how much in all. The market news has a line for each (MarketNewsKind.Paid).
        private void TellPaid()
        {
            if (_paid.Count == 0)
            {
                return;
            }
            DividendTally t = _paid;
            _paid.Clear();
            if (!Settings.MarketsDividendNotes)
            {
                return;
            }
            if (t.Count > 1)
            {
                Queue(NotificationType.Success, "reallife_mkt_note_dividends", "count", t.Count.ToString(), "amount", MarketText.Money(t.Total));
                return;
            }
            Stock k = _engine.State.Stock(t.Stock);
            BotFirm f = MarketBots.Firm(t.Firm);
            string name = k != null ? k.Name : string.Empty;
            if (f != null)
            {
                Queue(NotificationType.Success, "reallife_mkt_note_dividend_bot", "name", name, "firm", f.Name, "amount", MarketText.Money(t.Total));
            }
            else
            {
                Queue(NotificationType.Success, "reallife_mkt_note_dividend", "name", name, "amount", MarketText.Money(t.Total));
            }
        }

        // ---------------------------------------------------------------- the save tools and the tests

        // Leaving a save: every bot's shares settled at the bid with no commission (as the player's are); what that comes to.
        private long BotsSettled(System.Text.StringBuilder log)
        {
            MarketState s = _engine.State;
            long total = 0;
            for (int i = 0; i < s.Bots.Count; i++)
            {
                Bot b = s.Bots[i];
                for (int j = 0; j < b.Holdings.Count; j++)
                {
                    BotHolding h = b.Holdings[j];
                    Stock k = s.Stock(h.StockId);
                    if (k == null || !k.Listed)
                    {
                        continue;
                    }
                    long value = MarketTrading.Value(h.Shares, MarketTrading.QuoteOf(s, k, s.Phase).Bid, false);
                    total += value;
                    log.Append(' ').Append(h.Shares).Append(" of ").Append(k.Name).Append(" (").Append(b.Def != null ? b.Def.Name : "?").Append(") for ").Append(value).Append(';');
                }
            }
            s.Bots.Clear();
            return total;
        }

        /// <summary>Test button: every bot's month closed now, as at a month's end: its statement, its fee and its share of any profit.</summary>
        public void TestBotMonth()
        {
            FinishMoment();
            const string Test = "A month's statement for my bots";
            if (!TestReady(Test, true))
            {
                return;
            }
            if (State.Bots.Count == 0)
            {
                TestRefusals.TellKey(Logger, Test, "reallife_mkt_bot_test_none");
                return;
            }
            int count = _engine.BotMonthNow();
            Logger?.Info(Test + " test: " + count + " bot(s) settled for the month so far.");
            Moved();
        }
    }
}
