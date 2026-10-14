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
        // Once a second: with the option off, every bot is let go (its shares become the player's own).
        private void BotsSecond()
        {
            if (Settings.MarketsBots || _engine.State.Bots.Count == 0 || Behind || _engine.Held)
            {
                return;
            }
            for (int i = _engine.State.Bots.Count - 1; i >= 0; i--)
            {
                _engine.Dismiss(_engine.State.Bots[i].Firm, out _);
            }
            Logger?.Info("Stock market: trading bots switched off; every bot was let go and what it held is the player's own now.");
            Moved();
        }

        // What every ask of a bot needs: the market running, caught up and not held after a load.
        private BotWhy BotReady()
        {
            return !Running ? BotWhy.Closed : Behind ? BotWhy.Behind : _engine.Held ? BotWhy.Held : BotWhy.None;
        }

        /// <summary>A firm's bot hired with so much of the player's cash to trade and its settings. Null with the reason when it cannot be.</summary>
        public Bot HireBot(int firm, long capital, int size, int stop, int loss, out BotWhy why)
        {
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
            why = BotReady();
            bool done = why == BotWhy.None && _engine.SetBot(firm, size, stop, loss, out why);
            Moved();
            return done;
        }

        public bool SellUpBot(int firm, out BotWhy why)
        {
            why = BotReady();
            bool done = why == BotWhy.None && _engine.SellUp(firm, out why);
            Moved();
            return done;
        }

        public bool RestartBot(int firm, out BotWhy why)
        {
            why = !Settings.MarketsBots ? BotWhy.Off : BotReady();
            bool done = why == BotWhy.None && _engine.Restart(firm, out why);
            Moved();
            return done;
        }

        public bool DismissBot(int firm, out BotWhy why)
        {
            why = BotReady();
            bool done = why == BotWhy.None && _engine.Dismiss(firm, out why);
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
                    Queue(NotificationType.Info, "reallife_mkt_bot_note_statement", "firm", f.Name, "worth", MarketText.Money(n.A), "change", MarketText.Change((int)n.B), "count", n.Qty.ToString(),
                        "fees", MarketText.Money(n.C));
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
                    }
                    break;
                case BotNoteKind.Dismissed:
                    Queue(NotificationType.Info, "reallife_mkt_bot_note_dismissed", "firm", f.Name, "fees", MarketText.Money(n.A), "count", n.B.ToString());
                    break;
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
