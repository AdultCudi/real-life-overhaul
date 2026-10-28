namespace RealLifeOverhaul.Tech
{
    /// <summary>The kinds of tech firm: the sector's five business types (in TechTypes.All's order) and the game's two office businesses.</summary>
    public enum FirmKind
    {
        Software = 0, Games = 1, Consultancy = 2, Security = 3, AiLab = 4, WebAgency = 5, Design = 6, Count = 7
    }

    /// <summary>What a product is: a subscription service, a mobile app, licensed software, a game or an AI model.</summary>
    public enum ProductKind
    {
        Saas = 0, App = 1, Licence = 2, Game = 3, Model = 4, Count = 5
    }

    /// <summary>Who a product is sold to.</summary>
    public enum Segment
    {
        Consumer = 0, Business = 1, Enterprise = 2, Count = 3
    }

    /// <summary>What a firm's engineers spend their time on.</summary>
    public enum Focus
    {
        Balanced = 0, Features = 1, Quality = 2, Stability = 3, Count = 4
    }

    /// <summary>A contract's kind: what is done for the client and how it is paid.</summary>
    public enum WorkKind
    {
        Website = 0, Integration = 1, Managed = 2, Audit = 3, PenTest = 4, Incident = 5, Branding = 6, Count = 7
    }

    /// <summary>Where a contract stands: the saved number.</summary>
    public enum WorkStage
    {
        Offered = 0, Tender = 1, Bid = 2, Active = 3, Done = 4, Failed = 5, Lost = 6, Declined = 7
    }

    /// <summary>The figures of tech firms: products, contracts, risks and the rivals. Game-free; cents at the start's prices.</summary>
    public static class FirmsCatalogue
    {
        public const int MonthDays = 30;   // whatever the game's year: subscriptions run by the calendar, as wages and rents do

        /// <summary>The products each kind of firm can make, one bit per ProductKind.</summary>
        public static readonly int[] MakesProducts =
        {
            (1 << (int)ProductKind.Saas) | (1 << (int)ProductKind.App) | (1 << (int)ProductKind.Licence),
            1 << (int)ProductKind.Game,
            1 << (int)ProductKind.Saas,
            1 << (int)ProductKind.Saas,
            1 << (int)ProductKind.Model,
            1 << (int)ProductKind.App,
            0
        };

        /// <summary>The contracts each kind of firm takes on, one bit per WorkKind.</summary>
        public static readonly int[] TakesWork =
        {
            (1 << (int)WorkKind.Website) | (1 << (int)WorkKind.Integration),
            0,
            (1 << (int)WorkKind.Website) | (1 << (int)WorkKind.Integration) | (1 << (int)WorkKind.Managed),
            (1 << (int)WorkKind.Audit) | (1 << (int)WorkKind.PenTest) | (1 << (int)WorkKind.Incident) | (1 << (int)WorkKind.Managed),
            (1 << (int)WorkKind.Integration),
            (1 << (int)WorkKind.Website),
            1 << (int)WorkKind.Branding
        };

        /// <summary>The customers a segment can have in the city and online: people, businesses, large companies.</summary>
        public static readonly int[] Market = { 400000, 8000, 400 };

        /// <summary>A subscription's price a month in cents, by segment: per person, per business, per company.</summary>
        public static readonly int[] Price = { 600, 6000, 250000 };

        /// <summary>Of a consumer service's users, the share who pay, thousandths; businesses and companies all pay.</summary>
        public const int ConsumerPaying = 50;

        /// <summary>Churn a month by segment, thousandths of the paying customers, for a sound product.</summary>
        public static readonly int[] Churn = { 60, 30, 12 };

        /// <summary>Customers won a day, millionths of those not yet won, at an attraction of 1 and no marketing.</summary>
        public static readonly int[] Acquire = { 900, 1000, 1200 };

        public static readonly int[] Tickets = { 2, 12, 60 };   // support tickets a day per thousand customers, by segment
        public const int TicketsPerPerson = 30;                 // tickets one support person answers a day

        /// <summary>What hosting costs a customer a month, cents, by segment (at the cloud's list price).</summary>
        public static readonly int[] HostingCost = { 5, 150, 4000 };

        public const int AppAds = 400;                // advertising beside subscriptions, thousandths of the paying users' revenue
        public const int AppStoreCut = 300;           // thousandths the app stores take of an app's and a game's sales

        // Licences (enterprise software sold outright)
        public const int LicenceDeal = 4000000;       // a licence's price to a company, cents
        public const int LicenceMaintenance = 200;    // thousandths of a licence a year for its upkeep, billed monthly
        public const int LicenceLeadsPerSeller = 150; // thousandths of a deal a day one seller closes at an appeal of 1
        public const int LicenceInbound = 500;        // thousandths of a seller the enquiries that come in by themselves are worth, beside the sellers
        public const int LicenceSellersMost = 8000;   // thousandths: more than eight sellers find no more companies to call on

        // Prices, marketing and focus
        public static readonly int[] PriceTiers = { 700, 1000, 1400 };            // thousandths of the segment's price
        public static readonly int[] PriceAppeal = { 1250, 1000, 780 };           // what the price does to the appeal
        public static readonly int[] MarketingDay = { 0, 5000, 20000, 60000, 150000 };   // cents a day
        public static readonly int[] MarketingReach = { 1000, 1300, 1700, 2300, 3000 };  // thousandths
        /// <summary>The share of engineering time each focus gives to features, to fixing bugs and to paying down debt, thousandths.</summary>
        public static readonly int[][] FocusSplit =
        {
            new[] { 600, 250, 150 }, new[] { 800, 150, 50 }, new[] { 300, 550, 150 }, new[] { 200, 300, 500 }
        };

        // Building
        public const int FeatureBase = 40, FeatureStep = 12;   // work points a feature costs: the base and more for each one already built
        public const int FeaturesMost = 24;                    // features a product can have
        public const int LaunchFeatures = 3;                   // a product can launch with this many
        public const int PointsPerDay = 8;                     // a mid-level's points in a working day in good heart
        public const int BugsPerFeature = 6;                   // the bugs a feature can hold before its quality falls far (Quality)
        public const int PointsPerFix = 2;                     // points of fixing a bug takes (a tester's point fixes one)
        public const int DebtPerFeature = 35;                  // thousandths of debt a feature adds when juniors build it alone
        public const int DebtPerPoint = 4;                     // thousandths a point of paying down takes out
        public const int SoftwarePerSeat = 300;                // tools and licences a day per professional at a tech firm, cents
        public const int ProductsMost = 36;                    // products being built or on sale across the player's firms (it was 16 for all firms together)
        public const int ProductsPerFirm = 12;                 // ...and at any one firm, so a third firm is not locked out by the first two
        public const int WithdrawnKept = 12;                   // withdrawn products kept in the list; the oldest go when more are withdrawn
        public const int ProductsStored = ProductsMost + WithdrawnKept;   // the most the save reads back, live and withdrawn together

        /// <summary>
        /// How hard each kind of firm leans on its office's connection (NetSpeedRules), by FirmKind: an AI lab moves training
        /// data and a game studio its builds and assets, while a consultancy and a design studio do much of theirs at the
        /// client's or on paper.
        /// </summary>
        public static readonly int[] NetLean =
        {
            NetSpeedRules.Usual, NetSpeedRules.Heavy, NetSpeedRules.Light, NetSpeedRules.Usual, NetSpeedRules.Heaviest, NetSpeedRules.Usual, NetSpeedRules.Light
        };

        // Games
        public static readonly int[] GameScope = { 600, 1800, 4500 };        // work points: a small game, a mid-sized one, a big one
        public static readonly int[] GamePrice = { 999, 2499, 5999 };        // cents
        public const int GamePlayers = 300000;                               // people who might buy a game
        public const int GameHalfLife = 25;                                  // days for a game's daily sales to halve
        public const int GameLaunchShare = 600;                              // billionths of the players a day at launch per thousandth of appeal (it was 2000 while every game sat at the floor)
        public static readonly int[] GameFeatures = { 4, 6, 8 };             // features a finished game counts for against its rival (RivalFeaturesAtStart), by size: a big one matches it
        public const int GameBugRoom = 90;                                   // thousandths of a bug a game holds for each work point built into it (about a service's features)
        public const int HypePerMarketingDay = 2;                            // thousandths of hype a day of marketing before launch adds per reach step

        // Contracts
        public const int PointRate = 11000;            // cents a work point is charged at for build work
        public const int SecurityRate = 15000;         // ...for security work
        public static readonly int[] WorkLeast = { 60, 200, 0, 60, 80, 40, 40 };     // points of a contract, least
        public static readonly int[] WorkMost = { 200, 700, 0, 160, 200, 90, 120 };
        public static readonly int[] WorkDays = { 20, 45, 0, 15, 15, 5, 15 };        // days allowed (Managed: none, it runs by the month)
        public const int ManagedMonth = 400000;        // a managed service's fee a month for a business, cents
        public const int ManagedPoints = 2;            // points a day a managed contract needs to keep the client happy
        public const int UpfrontShare = 300;           // thousandths of a project paid when it is signed
        public const int LatePenalty = 20;             // thousandths of a project's value off for each day late, up to half
        public const int OffersMost = 6;               // offers and tenders open at once
        public const int ContractsMost = 24;           // contracts kept, finished ones included
        public const int OfferDays = 5;                // an offer waits this long
        public const int OfferChance = 150;            // per mille a day a firm that takes contracts is offered one, at a reputation of 500 and one seller
        public const int TenderEvery = 20;             // days between the council's tenders
        public const int TenderDays = 5;               // days to bid
        public const int CouncilShare = 1500;          // thousandths: a council job is this much bigger
        public const int OwnShare = 300;               // thousandths of the player's own businesses among the clients

        // Risks
        public const int OutageChance = 8;             // per mille a day for a live service, at no debt and no one keeping it running
        public const int OutageChurn = 20;             // thousandths of customers lost to an outage
        public const int SlaCredit = 50;               // thousandths of a month's enterprise revenue given back for an outage
        public const int BreachChance = 2;             // per mille a day for a product with personal data, before security
        public const int FinePerCustomer = 50;         // cents of a data protection fine for each customer affected
        public const int FineShareOfYear = 40;         // thousandths of a year's revenue at most
        public const int FineLeast = 500000;           // cents at least
        public const int RegisterFee = 40000;          // the data protection register's fee a year, cents
        public const int UnregisteredFine = 1500;      // thousandths: the fine is half again for an unregistered firm
        public const int RivalLaunchChance = 30;       // per mille a day that a product's rival ships a feature
        public const int ReliefShare = 300;            // thousandths of qualifying research and development costs given back as an extra tax deduction

        // The rivals are companies of the city's stock market (Markets/MarketCatalogue).
        /// <summary>A product's rival, as a stock market company's id.</summary>
        public static int RivalStock(ProductKind kind, Segment segment)
        {
            switch (kind)
            {
                case ProductKind.App: return 112;        // Signpost Search
                case ProductKind.Licence: return 106;    // Codeharbour Software
                case ProductKind.Game: return 110;       // Pixel Harbour Games
                case ProductKind.Model: return 112;
                default: return segment == Segment.Consumer ? 109 : segment == Segment.Business ? 108 : 106;   // Shopfront Online, Abacus Lane Software, Codeharbour Software
            }
        }

        /// <summary>A rival stock company's name as the market lists it (the few the tech sector names).</summary>
        public static string RivalName(int stock)
        {
            switch (stock)
            {
                case 106: return "Codeharbour Software";
                case 107: return "Skyvault Systems";
                case 108: return "Abacus Lane Software";
                case 109: return "Shopfront Online";
                case 110: return "Pixel Harbour Games";
                case 111: return "Tallyrow Analytics";
                case 112: return "Signpost Search";
                default: return "Codeharbour Software";
            }
        }

        public static readonly int[] RivalFeaturesAtStart = { 8, 10, 12 };   // by segment: the rival's product as it stands

        // Names
        public static readonly string[] NameFirst =
        {
            "Bright", "Quill", "Harbour", "Lantern", "Copper", "Tide", "Ember", "Cobalt", "Juniper", "Meadow", "Summit", "Willow", "Ridge", "Arbor", "Marble", "Orbit", "Pebble", "Kestrel"
        };

        public static readonly string[] NameSecond =
        {
            "desk", "ledger", "flow", "board", "stack", "notes", "pass", "track", "cast", "loop", "post", "mint", "wave", "base", "kit", "path", "bloom", "forge"
        };

        public static readonly string[] Clients =
        {
            "Elm Street Dental", "Northside Freight", "Garnet Insurance", "Harbour View Hotel", "Midtown Legal Partners", "Pinecrest Clinic", "Blue Anchor Logistics", "Copperfield Retail",
            "Riverside Accountants", "Oakline Property", "Sunset Fitness", "Greenway Grocers", "Maple Court Estates", "Ironbridge Engineering", "Lakeside College", "Cedar Health"
        };

        public const string Council = "City Council";
    }

    /// <summary>What the steps from a studio to a hyperscaler ask. Game-free.</summary>
    public static class TechPath
    {
        public const long ScaleUpMonthly = 5000000;   // a scale-up's recurring revenue a month, cents at the start's prices
        public const int ScaleUpPeople = 15;          // a scale-up's professionals
    }
}
