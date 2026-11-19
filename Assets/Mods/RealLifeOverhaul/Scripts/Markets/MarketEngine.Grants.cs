using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Part of the engine: shares put in the player's portfolio as part payment of something sold (the terms of a firm's sale, Tech/Venture/SaleTermRules), with the lock-up the terms
    /// stated. Asked once, on the day of the sale or a vesting year; never per frame.
    /// </summary>
    public sealed partial class MarketEngine
    {
        /// <summary>The price of a listed company's share now, whole cents; 0 when it is not listed.</summary>
        public int GrantPrice(int stockId)
        {
            Stock s = State.Stock(stockId);
            return s != null && s.Listed ? s.MidCents : 0;
        }

        /// <summary>
        /// Shares paid to the player, held from this price, none of them sellable before <paramref name="lockDay"/> (0: free). The cost is what they were worth when paid, so a later
        /// sale gains or loses against that. A lock is kept to the shares of one company and day; past the most locks the shares come without one only if there is no more room, which
        /// is refused instead. False when the company is not listed.
        /// </summary>
        public bool Grant(int stockId, long shares, int cents, int lockDay)
        {
            Finish();
            Stock s = State.Stock(stockId);
            if (s == null || !s.Listed || shares <= 0 || cents <= 0)
            {
                return false;
            }
            ShareLock found = null;
            for (int i = 0; lockDay > State.Day && i < State.Locks.Count && found == null; i++)
            {
                found = State.Locks[i].StockId == stockId && State.Locks[i].UntilDay == lockDay ? State.Locks[i] : null;
            }
            if (lockDay > State.Day && found == null && State.Locks.Count >= MarketState.LocksMost)
            {
                return false;
            }
            Position p = State.Position(stockId);
            if (p == null)
            {
                p = new Position { StockId = stockId, OpenedDay = State.Day };
                State.Positions.Add(p);
            }
            MarketTrading.Book(p, shares, MarketTrading.Value(shares, cents, true), State.Account);
            if (lockDay > State.Day)
            {
                if (found == null)
                {
                    State.Locks.Add(new ShareLock { StockId = stockId, UntilDay = lockDay });
                    found = State.Locks[State.Locks.Count - 1];
                }
                found.Shares += shares;
            }
            return true;
        }

        /// <summary>Locks that have run out, or that are of shares no longer held (sold in a takeover, a delisting, a short): taken off. Asked on the day; true if any went.</summary>
        public bool SweepLocks()
        {
            bool any = false;
            for (int i = State.Locks.Count - 1; i >= 0; i--)
            {
                ShareLock l = State.Locks[i];
                if (l.UntilDay <= State.Day || State.Held(l.StockId) <= 0 || State.Stock(l.StockId) == null)
                {
                    State.Locks.RemoveAt(i);
                    any = true;
                }
            }
            return any;
        }
    }
}
