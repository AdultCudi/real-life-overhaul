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
        /// commission; a margin account pays back what it holds (or what it owes is taken from the cash), and every order
        /// goes. Each is listed in the log. Then the market stops; the timer starts it again while the option is on.
        /// </summary>
        public void StopAll()
        {
            try
            {
                if (_running && Save != null && ReferenceEquals(Save, _save))
                {
                    MarketState s = _engine.State;
                    var log = new StringBuilder(256);
                    long total = 0;
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
            _running = false;
            _engine = null;
            _save = null;
            _notes.Clear();
            Behind = false;
            RemoveApp();
        }
    }
}
