namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The figures of founding a startup (the plan's section 7): the pre-seed sources, the articles, the stages of going to market and what moves fit. Cents at the start's prices,
    /// thousandths unless named. Every sum is a multiple of one the mod already holds (the venture catalogue's ticket, floor value and diligence fee). Game-free.
    /// </summary>
    public static class FoundCatalogue
    {
        public const string Key = "reallife:tech_founding";

        // Stages of going to market, in order. Saved as a number.
        public const int StageDiscovery = 0, StageBuild = 1, StageUsers = 2, StageLaunch = 3, StagePricing = 4, StageScaling = 5, Stages = 6;
        public static readonly string[] StageKeys =
        {
            "reallife_gtm_stage_discovery", "reallife_gtm_stage_build", "reallife_gtm_stage_users", "reallife_gtm_stage_launch", "reallife_gtm_stage_pricing", "reallife_gtm_stage_scaling"
        };

        // What each stage asks the player to do next, a whole key each.
        public static readonly string[] NextKeys =
        {
            "reallife_gtm_next_discovery", "reallife_gtm_next_build", "reallife_gtm_next_users", "reallife_gtm_next_launch", "reallife_gtm_next_pricing", "reallife_gtm_next_scaling"
        };

        // How a firm sells. Saved as a number.
        public const int ChannelNone = 0, ChannelSales = 1, ChannelProduct = 2, ChannelPartner = 3, Channels = 4;
        public static readonly string[] ChannelKeys =
        {
            "reallife_gtm_channel_none", "reallife_gtm_channel_sales", "reallife_gtm_channel_product", "reallife_gtm_channel_partner"
        };

        // Where the first money comes from. Saved as a number.
        public const int SourceSavings = 0, SourceFamily = 1, SourceAngels = 2, SourceAccelerator = 3, Sources = 4;
        public static readonly string[] SourceKeys =
        {
            "reallife_found_source_savings", "reallife_found_source_family", "reallife_found_source_angels", "reallife_found_source_accelerator"
        };

        // The articles of association. Saved as a number.
        public const int ArticlesStandard = 0, ArticlesFriendly = 1, Articles = 2;
        public static readonly string[] ArticlesKeys = { "reallife_found_articles_standard", "reallife_found_articles_friendly" };
        public static readonly string[] ArticlesAbout = { "reallife_found_articles_standard_about", "reallife_found_articles_friendly_about" };

        // ---------------------------------------------------------------- incorporation

        public const int FeeTickets = 3;             // the legal fees are this many due diligence reports (VentureCatalogue.DiligenceFee): a lawyer's days
        public const int FriendlyFeeExtra = 500;     // thousandths more lawyer's time for articles that hold the board back
        public const int LawDiscount = 3;            // thousandths off the fee for each point of the player's Law, to 100 points
        public const int FriendlyPriceCut = 80;      // thousandths off the value an investor puts on a firm whose articles tie the board's hands
        public const int CofounderPay = 500;         // thousandths of the going rate a co-founder is paid: they hold shares instead
        public const int SplitStep = 50, SplitMost = 400;   // the co-founders' share of the founders' shares, in steps, at most

        // ---------------------------------------------------------------- the pre-seed sources

        public static readonly int[] FamilyTickets = { 1, 2, 4 };   // multiples of the venture catalogue's least ticket a family can lend
        public const int FamilyValue = 1250;         // thousandths of the pre-seed floor value friends and family value the firm at: they do not haggle
        public const int FamilyStrain = 14;          // points of trust a failed firm costs with the partner, for each tenth of the money lost (0 to 100)
        public const int FamilyAffection = 8;        // ...and of affection
        public const int AccelTickets = 3;           // multiples of the least ticket an accelerator puts in
        public const int AccelShare = 70;            // thousandths of the firm it takes, set
        public const int AccelDays = 60;             // days of mentoring, ending with demo day
        public const int AccelLearn = 40;            // thousandths of a point a day each of its three skills grows
        public static readonly int[] AccelSkills = { TalentCatalogue.Selling, TalentCatalogue.ProductSense, TalentCatalogue.Leadership };
        public const int DemoSheets = 2;             // venture firms that meet the firm on demo day
        public const int DemoStage = 1;              // ...which lead the seed round (RoundStage.Seed)
        public const int AngelScoreBase = 350;       // how an angel scores a founder who has not pitched: the base, and the points each of Selling and Relationships adds
        public const int AngelScorePoint = 3;
        public const int AngelTickets = 4;           // multiples of the least ticket an angel puts in (the venture catalogue's own first cheque)

        // ---------------------------------------------------------------- going to market

        public const int DiscoveryRounds = 2;        // interviews rounds before there is something to build
        public const int LearnBase = 150;            // thousandths a round of interviews teaches, before the skills
        public const int LearnPoint = 3;             // ...and for each point of the skill it leans on
        public const int LearnSecond = 1;            // ...and of Product sense
        public const int SessionLearn = 40;          // thousandths of a point a session grows the skill it used, less as it rises (the study's own route)
        public const int PmfBar = 650;               // fit at which the product has found its market
        public const int PmfUsers = 5;               // paying customers it takes as well
        public const int FitPace = 40;               // thousandths of the gap a day fit moves
        public const int FitLearned = 150, FitRetention = 350, FitWord = 200, FitQuality = 200, FitName = 100;   // what fit is made of
        public const int WordShare = 20;             // fit's word of mouth: 1000 at one customer in fifty of the niche
        public const int WomEase = 20;               // thousandths of word of mouth a day eases off
        public const int WomBase = 200, WomPoint = 3;   // what asking users to spread the word adds, and per point of Relationships
        public const int FixBase = 150, FixPoint = 3;   // thousandths of the bugs a round of fixing clears, and per point of Product sense and Analysis
        public const int BuildMinutes = 480, BuildShort = 240;   // a long and a short night of building it oneself
        public const int FounderSell = 8;            // points of selling a day a founder brings at 100 Selling (one seller in good heart: SectorCatalogue.SellingHalf)
        public const int ProductBoost = 300;         // thousandths of awareness a product led launch adds
        public const int PartnerBoost = 400;         // ...and a partner's
        public const int PartnerCut = 250;           // thousandths of the product's takings a company that resells it keeps
        public const int PriceBear = 400;            // thousandths of the going price fit moves what customers will bear: 80 per cent at no fit, 120 at full
        public const int PriceLow = 800;

        // ---------------------------------------------------------------- how the investors see fit

        public const int FitCheap = 350;             // under this a raise is cheap money
        public const int CheapFactor = 650;          // thousandths of the value it gets then
        public const int RichFactor = 300;           // thousandths more at full fit
        public const int FitGrowthBp = 3;            // basis points of growth in the valuation's multiple for each thousandth of fit over 500

        /// <summary>The key of a stage.</summary>
        public static string StageKey(int stage) => StageKeys[stage < 0 ? 0 : stage >= Stages ? Stages - 1 : stage];

        public static string NextKey(int stage) => NextKeys[stage < 0 ? 0 : stage >= Stages ? Stages - 1 : stage];

        public static string ChannelKey(int channel) => ChannelKeys[channel < 0 ? 0 : channel >= Channels ? Channels - 1 : channel];

        public static string SourceKey(int source) => SourceKeys[source < 0 ? 0 : source >= Sources ? Sources - 1 : source];

        public static string ArticlesKey(int articles) => ArticlesKeys[articles <= 0 ? 0 : 1];

        public static string ArticlesAboutKey(int articles) => ArticlesAbout[articles <= 0 ? 0 : 1];
    }
}
