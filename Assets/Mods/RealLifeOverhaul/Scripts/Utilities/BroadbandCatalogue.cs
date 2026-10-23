namespace RealLifeOverhaul.Utilities
{
    /// <summary>
    /// The player's own broadband company's figures at the start's prices: a licence, fibre dug district by district, and a
    /// market won from the rivals month by month. Checked offline (EconomyCheck).
    /// </summary>
    public static class BroadbandCatalogue
    {
        /// <summary>The districts of the city's market, in the order of every table here.</summary>
        public static readonly string[] Districts =
        {
            "ba:neighborhood_garmentdistrict", "ba:neighborhood_hellskitchen", "ba:neighborhood_industrycity",
            "ba:neighborhood_lowermanhattan", "ba:neighborhood_midtown", "ba:neighborhood_murrayhill", "ba:neighborhood_thehamptons"
        };

        public static readonly int[] Market = { 15000, 20000, 12000, 25000, 30000, 20000, 3000 };                     // homes and businesses with a connection
        public static readonly int[] BuildCost = { 1200000, 1600000, 1000000, 2000000, 2400000, 1600000, 1200000 };   // a district's fibre (the Hamptons' houses stand far apart)

        public const int LicenceFee = 1000000;   // the city's operator's licence, once
        public const int BuildMonths = 2;        // a district's network is dug in this many of the game's months
        public const int UpkeepPerMille = 8;     // of what a district's network costs to build, a month, at the usual maintenance
        public const int StaffCents = 1200;      // engineers and the call centre, a connection a month
        public const int NetworkCents = 1800;    // the backbone, its transit and the box in each home, a connection a month
        public const int InstallCents = 12000;   // an engineer's visit and the box, for each new connection

        // the company's plans, the share of customers on each, and their usual prices a month
        public static readonly int[] Mbps = { 300, 1000, 2000 };
        public static readonly int[] TierMix = { 500, 350, 150 };
        public static readonly int[] DefaultCents = { 5500, 7500, 10500 };
        public const int PriceLeast = 500, PriceMost = 2000, PriceStep = 50;   // the prices the player may set, thousandths of the usual

        // maintenance spent, thousandths of the usual, sets how often the network fails (in a thousand days)
        public const int MaintenanceLeast = 500, MaintenanceMost = 1500, MaintenanceStep = 100;
        public const int OutageBase = 20, OutageLeast = 5, OutageMost = 60;

        public static readonly int[] Marketing = { 0, 10000, 25000, 50000, 100000, 250000, 500000 };   // the budgets to choose from, a month
        public const int MarketingUnit = 25000, MarketingPull = 150;   // each natural log of (1 + budget / unit) adds this many thousandths to the weight

        // a district's choice: each provider's hold by its price (and the company's trust, reliability, marketing); shares follow
        public static readonly int[] RivalHold = { 450, 300, 100, 150 };   // InternetCatalogue.Providers
        public const int OwnHold = 300;          // the company's, trusted fully: as Brightvein's
        public const int TrustMonths = 9;        // a district trusts a new network fully this many months after it opens there
        public const int PriceElasticity = 2000, ReliabilityElasticity = 500;
        public const int Follow = 200;

        public const int Answer = 1000;          // a rival's price is held under its highest by this share of the company's share of its markets

        public const int HistoryMonths = 12;     // the months of profit a buyer looks at
        public const int SaleMultiple = 4;       // a buyer pays four years of the last year's profit...
        public const int SaleFloorShare = 300;   // ...or, for a company that has made none, this share of what its network cost

        // the company's money, by transaction type (CashCategory reads takings, wages, investments, marketing, fees)
        public const string TakingsKey = "reallife_transaction_broadband_takings";
        public const string WagesKey = "reallife_transaction_broadband_wages";
        public const string NetworkKey = "reallife_transaction_broadband_network";
        public const string InstallsKey = "reallife_transaction_broadband_installs";
        public const string MarketingKey = "reallife_transaction_broadband_marketing";
        public const string LicenceKey = "reallife_transaction_broadband_licence";
        public const string BuildKey = "reallife_transaction_broadband_investment";
        public const string SaleKey = "reallife_transaction_broadband_investment_sale";
        public const string NetworkRow = "reallife_books_broadband_network";   // the headquarters' statement: the network's cost among its goods

        public const string OwnNameKey = "reallife_net_provider_own", SoldNameKey = "reallife_net_provider_sold";

        // At scale (Core/EndScale): the city's real number of connections, a network to every one, a licence with its core
        // network and crews the company hires itself. A district dug before the scale reaches the old number until extended.
        public const int MarketScale = 5000;                 // thousandths of a district's connections at the city's real size
        public const int HomeCentsScale = 35000;             // a home or business passed by the fibre, cents
        public const int HomeCentsFarScale = 105000;         // ...in the Hamptons, where the houses stand far apart
        public const int FarDistrict = 6;                    // the Hamptons, in the order of Districts
        public const int LicenceFeeScale = 40000000;         // the operator's licence and the core network, once, money ($40,000,000)
        public const int UpkeepPerMilleScale = 3;            // of what a district's network costs at scale, a month, at the usual maintenance
        public const int InstallCentsScale = 60000;          // an engineer's visit, the box and the drop to the home

        public const int Field = 0, Calls = 1, Operations = 2, CrewKinds = 3;
        public static readonly int[] CrewWage = { 30000, 18000, 38000 };   // cents a day each in a WageMonthDays month: field, call centre, operations
        public const int WageMonthDays = 5;                  // wages are written for the 60-day year's five-day month, so a longer year earns and pays alike
        public const int FieldConnections = 1500;            // connections a field engineer looks after
        public const int CallConnections = 800;              // ...a call centre agent
        public const int OperationsPerDistrict = 6;          // network operations engineers for each district open, round the clock
        public const int CrewPace = 40;                      // thousandths of the need hired a day while short
        public const int IncomeAtScale = 1000;               // thousandths: the one knob for what the company takes at scale, tuned in one edit
        public const int SaleMultipleScale = 3;   // a buyer at scale pays three years of profit: less than running on, so digging to sell never pays
    }
}
