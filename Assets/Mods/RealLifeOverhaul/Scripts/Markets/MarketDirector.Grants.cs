namespace RealLifeOverhaul.Markets
{
    /// <summary>The director's side of shares paid in a sale (MarketEngine.Grants): asked on a click or a game day, never per frame.</summary>
    public sealed partial class MarketDirector
    {
        /// <summary>The market's price of a listed company's share now, whole cents; 0 where shares cannot be paid into the portfolio (the market is off, or the player is a guest).</summary>
        public int GrantPrice(int stockId)
        {
            return FirmsOpen ? _engine.GrantPrice(stockId) : 0;
        }

        /// <summary>The tax withheld from a dividend of whole money paid to the player: the market's rate for dividends, moved by the council's
        /// business tax. Nothing while the market is off.</summary>
        public long StakeDividendTax(long money)
        {
            MarketState s = Running ? _engine.State : null;
            return s != null && money > 0 ? ListingVotes.DividendTax(money, s.Env.Lever(MarketInputs.BusinessTaxLever)) : 0L;
        }

        /// <summary>Shares paid to the player with a lock-up to a day (0: free); true once they are in the portfolio.</summary>
        public bool Grant(int stockId, long shares, int cents, int lockDay)
        {
            if (!FirmsOpen)
            {
                return false;
            }
            FinishMoment();
            bool done = _engine.Grant(stockId, shares, cents, lockDay);
            if (done)
            {
                Logger?.Info("Stock market: " + shares + " share(s) of stock " + stockId + " paid to the player at " + cents + " cents"
                    + (lockDay > 0 ? ", locked to day " + lockDay : string.Empty) + ".");
                Changes();
            }
            return done;
        }
    }
}
