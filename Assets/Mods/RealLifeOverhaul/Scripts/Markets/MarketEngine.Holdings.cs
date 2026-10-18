using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// Part of the engine: the rival tycoons' holdings, listed when their tycoon comes forward as a pipeline company is, and
    /// a bid for the player's company made by a named holding. Asked on the rivals' day or a test, never per frame.
    /// </summary>
    public sealed partial class MarketEngine
    {
        private int _bidderNext;   // the holding the next bid for the player's company is made by; 0 the market's own pick

        /// <summary>
        /// A rival's holding comes to the market now, the whole company scaled so the tycoon's own stake
        /// (<see cref="MarketCatalogue.HoldingFounderBp"/>) at its price is their fortune <paramref name="worth"/> (0: the
        /// catalogue's size); only its free float trades. True if it is listed, or already was.
        /// </summary>
        public bool ListHolding(int id, long worth)
        {
            Finish();   // Midway
            CompanyDef d = MarketCatalogue.Find(id);
            if (d == null || !d.Holding)
            {
                return false;
            }
            Stock old = State.Stock(id);
            if (old != null && old.Listed)
            {
                return true;
            }
            if (old == null && State.Stocks.Count >= MarketCatalogue.CompaniesMost)
            {
                return false;
            }
            if (old != null)
            {
                State.Stocks.Remove(old);
            }
            Stock s = MarketLife.Make(d, null, State.Day, State.Env, Year, _secret, 0);
            s.Daily.Clear();
            s.Monthly.Clear();
            double cap = s.Shares * (double)s.MidCents / 100.0;
            if (worth > 0 && cap > 0.0)
            {
                Scale(s, MarketValuation.Clamp(worth * 10000.0 / MarketCatalogue.HoldingFounderBp / cap, 0.001, 100000.0));
            }
            Reweigh(s, true);   // before it is counted: the indices stay where they were
            State.Stocks.Add(s);
            Say(new MarketNews { Day = State.Day, Kind = MarketNewsKind.Listing, Stock = s.Id, A = s.MidCents, B = State.Day });
            return true;
        }

        // A company scaled as a whole: every sum and its shares by the same factor, so each share's figures are unchanged.
        private static void Scale(Stock s, double k)
        {
            s.Rev *= k;
            s.Debt *= k;
            s.Equity *= k;
            s.QRev *= k;
            s.QNi *= k;
            s.Adv *= k * (10000.0 - MarketCatalogue.HoldingFounderBp) / 10000.0;   // only its free float trades
            s.Shares = Math.Max(100000L, (long)Math.Round(s.Shares * k));
            for (int i = 0; i < s.Results.Count; i++)
            {
                QuarterResult r = s.Results[i];
                r.RevenueK = (long)Math.Round(r.RevenueK * k);
                r.ProfitK = (long)Math.Round(r.ProfitK * k);
                s.Results[i] = r;
            }
        }

        /// <summary>A rival's holding with no tycoon in play leaves the market at its price, its holders paid at it. True if it did.</summary>
        public bool DelistHolding(int id)
        {
            CompanyDef d = MarketCatalogue.Find(id);
            Stock s = State.Stock(id);
            return d != null && d.Holding && s != null && s.Listed && Delist(id, GoneReason.Delisted, s.MidCents);
        }

        /// <summary>A bid for the player's company by a named holding (the test tools). True if the bid stands.</summary>
        public bool BidBy(int stockId, int bidderId)
        {
            Stock s = State.Stock(stockId);
            if (s == null || s.Own == null || !s.Listed || s.BidCents > 0)
            {
                return false;
            }
            _bidderNext = bidderId;
            bool made = BidFor(stockId);
            _bidderNext = 0;
            return made && s.BidCents > 0;
        }
    }
}
