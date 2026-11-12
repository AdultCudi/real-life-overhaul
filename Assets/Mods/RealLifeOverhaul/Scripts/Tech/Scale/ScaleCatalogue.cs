namespace RealLifeOverhaul.Tech
{
    /// <summary>A product's size, by its budget: an indie game or app, a mid-sized one, a big one made by a large studio.</summary>
    public enum Scope
    {
        Indie = 0, Mid = 1, Big = 2, Count = 3
    }

    /// <summary>Where a product is sold: the stores of computers, consoles and phones, or straight from the firm's own site.</summary>
    public enum Platform
    {
        Pc = 0, Console = 1, Mobile = 2, Direct = 3, Count = 4
    }

    /// <summary>How a single product did against its budget (ScaleRules.BandOf).</summary>
    public enum ReturnBand
    {
        Flop = 0, Poor = 1, Average = 2, Good = 3, Hit = 4, Masterpiece = 5
    }

    /// <summary>The kinds of firm a payback target is set for (ScaleCatalogue.PaybackLeast and PaybackMost).</summary>
    public enum PaybackKind
    {
        Shop = 0, Chain = 1, GameStudio = 2, Software = 3, Services = 4, AiLab = 5, Infrastructure = 6, Network = 7, Count = 8
    }

    /// <summary>
    /// The tech sector's money scale (plans/tech-overhaul DRAFT2 section 1): tech returns as the end game does
    /// (EndScaleRules' payback aims) with more swing, one shared market per kind of product, real budgets. Every figure the
    /// studio and the other kinds of firm will be built on is here, in cents at the start's prices and thousandths unless
    /// named. Game-free, read by <see cref="ScaleRules"/> and held by EconomyCheck's TechScaleChecks.
    /// </summary>
    public static class ScaleCatalogue
    {
        // Budgets: a product's whole cost (the team's pay for the months it takes, licences, marketing and the launch)
        public static readonly long[] BudgetLeast = { 5000000L, 100000000L, 2000000000L };       // 50 thousand, 1 million, 20 million
        public static readonly long[] BudgetMost = { 50000000L, 1000000000L, 15000000000L };     // 500 thousand, 10 million, 150 million
        public static readonly long[] BudgetTypical = { 20000000L, 300000000L, 6000000000L };    // where a scope's budgets usually sit: what the sims and the checks use

        // The bands a single product's return falls in: its money back after the stores and refunds over its budget
        public const int FlopLeast = 100;          // a flop returns a tenth of its budget at the least...
        public const int FlopMost = 500;           // ...and half at the most
        public const int AverageLeast = 1000;      // an average product earns its budget back...
        public const int AverageMost = 1500;       // ...and half again at the most
        public const int HitLeast = 3000;          // a hit three times its budget...
        public const int HitMost = 10000;          // ...to ten times; past that a masterpiece, with no cap but the audience

        // From the audience to sales
        // people in the city and the world a product of the scope reaches per thousand of its budget (whole money): a niche game, a broad one, one for everybody
        public static readonly int[] PeoplePerMoney = { 34806, 15648, 5893 };
        public const int ReachFloor = 100;         // thousandths of the audience who hear of a product with no marketing at all: the stores' pages and word of mouth
        public const int ReachMost = 850;          // thousandths reached at the most, however much is spent
        public const int ReachHalfCents = 60;      // cents of marketing a person of the audience takes to get half way from the floor to the most
        public static readonly int[] MarketingShare = { 200, 250, 300 };          // thousandths of a budget usually spent on marketing, by scope
        public static readonly int[] Conversion = { 3, 3, 5, 7, 10, 16, 20, 25, 34, 50, 90 };   // thousandths of those reached who buy in the launch window, by quality 0, 10, ... 100
        // thousandths: the whole life's sales over the launch window's, by quality: a great product sells for years
        public static readonly int[] Tail = { 1100, 1150, 1250, 1350, 1500, 1700, 2000, 2400, 3000, 3800, 5000 };
        public const int LaunchDays = 30;          // days of the launch window
        public const int HalfLifeBase = 30;        // days for the sales after the launch to halve, at a quality of nothing...
        public const int HalfLifePerPoint = 1;     // ...and a day longer for each point of quality
        public static readonly long[] Price = { 1500, 3000, 6000 };               // cents: the usual price of a product of the scope
        public const int PriceAppealLeast = 400;   // thousandths: the most a dear price takes off the share who buy...
        public const int PriceAppealMost = 2000;   // ...and the most a cheap one adds

        // Real costs beyond the team
        public static readonly int[] PlatformFee = { 300, 300, 300, 120 };        // thousandths of each sale the store takes, by Platform; selling direct costs the payments and the site
        public const int Refunds = 40;             // thousandths of sales given back
        public const int EngineRoyalty = 50;       // thousandths of sales a licensed engine takes...
        public const long EngineRoyaltyFree = 100000000L;   // ...past the first million of a product's sales (cents)
        public const long OwnEngineCost = 50000000L;        // what building an engine of one's own costs once (cents), in place of the royalty
        public const int ServerCentsPerPlayerMonth = 20;    // an online product's servers a month for each player still playing (cents)

        // Marketing: what it costs to reach a share of an audience (the reach above, turned round for a plan)
        public const int MarketingStepsMost = 5;   // the steps of a marketing plan, none to the most
        public static readonly int[] MarketingReachStep = { 0, 300, 500, 650, 750, 800 };   // thousandths of the way from the floor to the most each step aims at

        // The sims (EconomyCheck), and what a studio's ordinary team gives
        public static readonly int[] DevDays = { 90, 180, 300 };      // game days an ordinary team takes to make a product of the scope
        public static readonly int[] TeamQuality = { 55, 58, 62 };    // the quality an ordinary team of that size averages: larger studios have leads with more products behind them
        public const int QualitySpread = 15;                           // points one product's quality falls either side of the team's, as a standard deviation
        public const int SimDays = 3650;                               // ten years of 365 days

        // One market per kind: every product of a kind shares the people who want it, rivals included
        public const int OutsideShare = 500;       // thousandths of appeal the market's people give to buying nothing of the kind

        // Payback targets, game days: what each kind of business should take to earn back what was put in, well run
        public static readonly int[] PaybackLeast = { 15, 30, 120, 120, 60, 180, EndScaleRules.PaybackDaysLeast, EndScaleRules.PaybackYearsLeast * 365 };
        public static readonly int[] PaybackMost = { 90, 180, 360, 360, 240, 540, EndScaleRules.PaybackDaysMost, EndScaleRules.PaybackYearsMost * 365 };

        // What the research measured of today's businesses (DRAFT2 section 1), whole money a day, so the sims can hold tech against them
        public const long ShopDayLeast = 1500, ShopDayMost = 3000;        // a good shop's profit a day
        public const long ShopCapitalLeast = 30000, ShopCapitalMost = 80000;
        public const long ChainDayLeast = 20000, ChainDayMost = 60000;    // a strong chain of ten to twenty shops
        public const long BankDay = 276000;                               // the player's national bank at scale
        public const long BroadbandDayLeast = 750000, BroadbandDayMost = 2000000;   // the broadband company at scale, billed by the save's month (a 60-day year)
    }
}
