namespace RealLifeOverhaul.Utilities
{
    /// <summary>
    /// The player's own broadband company's figures at the start's prices: a licence, fibre dug district by district, and a
    /// market won from the rivals month by month. Checked offline (EconomyCheck).
    /// </summary>
    public static class BroadbandCatalogue
    {
        /// <summary>
        /// The districts of the city's market, in the order of every table here: the game's seven, then the mod's own two (Uptown and Cedar Heights). The
        /// saved places of the first seven never move; a district a free update adds comes after these, by id (<see cref="Network.Register"/>).
        /// </summary>
        public static readonly string[] Districts =
        {
            "ba:neighborhood_garmentdistrict", "ba:neighborhood_hellskitchen", "ba:neighborhood_industrycity",
            "ba:neighborhood_lowermanhattan", "ba:neighborhood_midtown", "ba:neighborhood_murrayhill", "ba:neighborhood_thehamptons",
            "reallife_neighborhood_uptown", "reallife_neighborhood_cedarheights"
        };

        public const int Legacy = 7;                // the districts a save before the mod's own two kept by place: its record has these and no more
        public const int ExtraMax = 6;              // districts a free update may add that a network keeps a place for

        public static readonly int[] Market = { 15000, 20000, 12000, 25000, 30000, 20000, 3000, MarketUptown, MarketCedar };   // homes and businesses with a connection
        // a district's fibre (the Hamptons' houses stand far apart); the mod's own two by BuildPerConnection
        public static readonly int[] BuildCost = { 1200000, 1600000, 1000000, 2000000, 2400000, 1600000, 1200000, MarketUptown * 80, MarketCedar * 80 };

        /// <summary>The mod's own districts' connections at the start's prices: ten for each of their households, as the first seven's 125,000 are of
        /// theirs. The day-one band has 7,475 flats in 222 home buildings (docs/features/population.md), 106 of them in Uptown and 116 in Cedar
        /// Heights, so about 3,570 and 3,900 flats, 96% of them households: 34,000 and 37,000.</summary>
        public const int MarketUptown = 34000, MarketCedar = 37000;

        public const int PerHousehold = 10;         // connections (homes and businesses) in a district for each household the population counts there: the first seven's 125,000 over their households
        public const int BuildPerConnection = 8000; // cents, at the start's prices: what a connection's fibre costs to dig, as the first seven's costs bear to their markets (15,000 for 1,200,000)
        // a district no farther than this from the nearest the catalogue lists is dug at the usual cost per connection:
        // Industry City, the farthest of the usual ones, lies 1,452 m from Lower Manhattan
        public const int NearMetres = 1500;
        public const int FarMetres = 3000;          // ...from twice that far it is dug at FarMost, as the Hamptons are: the cost per connection rises in a line between
        public const int FarMost = 3000;            // thousandths of the usual cost per connection for the farthest: the Hamptons' (at scale, HomeCentsFarScale over HomeCentsScale)
        public const int DefaultMarket = 12000;     // a later district's connections when the population cannot say: the smallest of the first seven's non-rural districts (Industry City)

        /// <summary>The middle of each district of <see cref="Districts"/>, metres on the city's map (the first seven from the zones the population
        /// holds, the mod's two from its plan's lots), for a later district's distance.</summary>
        // thousandths of the usual cost per connection: the Hamptons' houses stand far apart, the rest lie one district's reach from another
        public static readonly int[] Far = { 1000, 1000, 1000, 1000, 1000, 1000, 3000, 1000, 1000 };

        public static readonly int[] MiddleX = { 291, 291, -1600, -409, -104, -39, -2691, 920, 140 };
        public static readonly int[] MiddleZ = { -294, 246, -1120, -289, 246, -289, -1359, 700, 700 };

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
        public const int HomeCentsFarScale = 105000;         // ...in the Hamptons, where the houses stand far apart (FarMost times the usual)
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
