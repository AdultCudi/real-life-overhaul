namespace RealLifeOverhaul.Tech
{
    /// <summary>A rival that sells into a niche, as the niche's page shows it: read when the page is drawn, from the day's figures. Game-free.</summary>
    public struct RivalryLine
    {
        public int Id;
        public string Name;
        public string Title;          // what its product is called (RivalProduct.Title)
        public string Company;        // the listed company behind it (that bought it, or that it is); empty for a startup on its own
        public bool Listed;           // a listed company's product that came into the niche
        public int Tier;              // its price tier
        public long Price;            // cents a customer a month at the start's prices
        public int Share;             // thousandths of the niche's buyers
        public long Customers;
        public long Cash;             // a startup's money, cents at the start's prices; -1 for a listed company
        public bool Funded;
    }
}
