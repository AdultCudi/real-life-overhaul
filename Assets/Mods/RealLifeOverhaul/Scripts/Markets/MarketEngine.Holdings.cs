namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// The rival tycoons' holdings on the market (Scripts/Rivals, through Wiring/RivalLinks): a holding comes to the market
    /// when its tycoon comes forward, as a pipeline company comes (at its price at today's level, its history from today),
    /// and a bid for the player's company can name the holding that makes it. Game-free, as the rest of the engine. Asked
    /// on the rivals' day or a test, never per frame; nothing while no tycoon has come forward.
    /// </summary>
    public sealed partial class MarketEngine
    {
        private int _bidderNext;   // the holding the next bid for the player's company is made by (OwnBid); 0 for the market's own pick

        /// <summary>A rival's holding (a catalogue company marked Holding) comes to the market now. True if it is listed (or already was).</summary>
        public bool ListHolding(int id)
        {
            Finish();   // (a moment half played is played out first: Midway)
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
            Reweigh(s, true);   // (before it is counted: the indices stay where they were)
            State.Stocks.Add(s);
            Say(new MarketNews { Day = State.Day, Kind = MarketNewsKind.Listing, Stock = s.Id, A = s.MidCents, B = State.Day });
            return true;
        }

        /// <summary>A bid for the player's company by a named holding (the test tools' bid, with its bidder given). True if the bid stands.</summary>
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
