namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// Racks of a data centre let to one named company of the city (a MarketCatalogue company, so the name is the city's own): how many, the fee a month, the term and what a rival
    /// data centre has offered its client. Saved with the infrastructure part. Game-free.
    /// </summary>
    public sealed class ColoContract
    {
        public int Id;
        public string Place = string.Empty;   // the data centre
        public int Client;                    // a MarketCatalogue company's id
        public int Racks;
        public long Fee;                      // cents a month at the start's prices
        public int Start, End;                // the day it began, and the day its term ends
        public int Rival = -1;                // the rival data centre that has bid for it (an index of HiringCatalogue.RivalFirms), or -1
        public bool Weighed;                  // the offers before the term's end have been looked at
    }
}
