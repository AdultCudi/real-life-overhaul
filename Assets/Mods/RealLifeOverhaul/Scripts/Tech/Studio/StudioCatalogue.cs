namespace RealLifeOverhaul.Tech
{
    /// <summary>What a game is: each genre has its own audience on each platform, its usual budget and what its players care about.</summary>
    public enum Genre
    {
        Action = 0, Adventure = 1, Rpg = 2, Strategy = 3, Simulation = 4, Sports = 5, Racing = 6, Puzzle = 7, Horror = 8, Shooter = 9, Casual = 10, Family = 11, Count = 12
    }

    /// <summary>How a game earns: sold once, free with purchases inside, by the month, or sold early while it is still being made.</summary>
    public enum GameModel
    {
        Premium = 0, FreeToPlay = 1, Subscription = 2, EarlyAccess = 3, Count = 4
    }

    /// <summary>What a game is built on: an engine licensed from others, or the studio's own once it has built one.</summary>
    public enum GameEngine
    {
        Licensed = 0, Own = 1, Count = 2
    }

    /// <summary>Where a game stands: the three phases of making it, finished and waiting for its day, on sale, and over.</summary>
    public enum GamePhase
    {
        Pre = 0, Production = 1, Polish = 2, Ready = 3, Live = 4, Ended = 5
    }

    /// <summary>The parts a game's quality is made of; each genre weighs them its own way.</summary>
    public enum Part
    {
        Design = 0, Tech = 1, Art = 2, Content = 3, Polish = 4, Count = 5
    }

    /// <summary>What the players of a game ask for after its launch: the studio answers with a free update or a paid add-on.</summary>
    public enum FanAsk
    {
        Levels = 0, HardMode = 1, PhotoMode = 2, CoOp = 3, ModTools = 4, Characters = 5, Soundtrack = 6, Comfort = 7, Count = 8
    }

    /// <summary>
    /// The game studio's figures (plans/tech-overhaul DRAFT2 section 3, "Game studio"): the twelve genres with their settings,
    /// audiences, budgets and the parts their players care about; the platforms' installed bases; the phases, the desk
    /// sessions' effects, hype, reviews, sales, piracy, life after launch, bonuses and the rival studios. Our own design on
    /// the mod's staff, skills, money scale (<see cref="ScaleCatalogue"/>) and economy, no copy of another game's tables.
    /// Cents at the start's prices and thousandths unless named. Game-free, read by <see cref="StudioRules"/> and held by
    /// EconomyCheck's StudioChecks.
    /// </summary>
    public static class StudioCatalogue
    {
        /// <summary>The games outlets that review every game on its release: twelve, each its own name. Invented, none a real paper or site.</summary>
        public static readonly string[] Outlets =
        {
            "Pixel Post", "Joystick Weekly", "The Cartridge", "Loadscreen", "Hardmode", "Patchnotes", "Side Quest Daily", "Co-op Corner", "The Save Point", "Boss Rush", "Frame Rate",
            "Replay Magazine"
        };

        /// <summary>The days of a game's copies a day the page draws, and the ring that holds them.</summary>
        public const int SalesDays = 90;

        // The games market's years are the save's (CalendarRules.MoneyYearDays's note on the one clock): installed bases grow, the critics'
        // bar rises and the year's award is given by them, as the studio's people's careers count them (TalentCatalogue.Scaled); a game's
        // own life (its window, its tail, the days it is looked after) is money, by the game day.
        public const int PostLaunchDays = 730;      // days after its launch a game on sale is still looked after at the desk (patches, the fans' asks): two years of its sales by the game day

        // ---------------------------------------------------------------- genres

        /// <summary>The settings a game can have; each genre offers four of them (<see cref="GenreSettings"/>).</summary>
        public const int Settings = 16;             // the settings there are, named by SettingKeys

        public static readonly string[] SettingKeys =
        {
            "fantasy", "scifi", "modern", "historical", "postapocalyptic", "cartoon", "space", "military", "haunted", "mystery", "pirates", "wildwest", "cyberpunk", "underwater", "stadium", "city"
        };

        /// <summary>The four settings each genre offers, by Genre: the order is the menu's, not how well each fits.</summary>
        public static readonly int[][] GenreSettings =
        {
            new[] { 0, 1, 2, 10 }, new[] { 10, 9, 0, 13 }, new[] { 12, 4, 0, 11 }, new[] { 3, 6, 7, 5 }, new[] { 15, 2, 6, 3 }, new[] { 14, 5, 15, 2 },
            new[] { 15, 12, 2, 5 }, new[] { 5, 9, 6, 13 }, new[] { 8, 1, 4, 5 }, new[] { 7, 1, 11, 0 }, new[] { 5, 15, 13, 3 }, new[] { 5, 0, 13, 7 }
        };

        /// <summary>
        /// How well each of those settings fits its genre, thousandths of the sales and a little of the reviews. Never shown
        /// as a table: a studio learns a pairing by shipping a game in it, and the page then says what it learned.
        /// </summary>
        public static readonly int[][] GenreFit =
        {
            new[] { 1000, 1150, 950, 850 }, new[] { 1000, 1150, 950, 850 }, new[] { 1000, 950, 1150, 850 }, new[] { 1150, 1000, 950, 850 }, new[] { 1150, 950, 1000, 850 },
            new[] { 1150, 950, 850, 1000 }, new[] { 1000, 950, 1150, 850 }, new[] { 1000, 1150, 950, 850 }, new[] { 1150, 950, 1000, 850 }, new[] { 1000, 1150, 950, 850 },
            new[] { 1150, 950, 1000, 850 }, new[] { 1150, 1000, 950, 850 }
        };

        /// <summary>
        /// How each genre weighs the five parts of quality, thousandths by Part (design, tech, art, content, polish): what its
        /// players care about. An action game lives on tech and polish, a role-playing game on design and content.
        /// </summary>
        public static readonly int[][] PartWeights =
        {
            new[] { 150, 250, 200, 150, 250 }, new[] { 300, 100, 250, 250, 100 }, new[] { 300, 100, 150, 350, 100 }, new[] { 400, 200, 100, 200, 100 },
            new[] { 350, 250, 100, 200, 100 }, new[] { 150, 250, 200, 150, 250 }, new[] { 100, 300, 250, 150, 200 }, new[] { 450, 50, 200, 150, 150 },
            new[] { 250, 150, 300, 150, 150 }, new[] { 150, 300, 200, 100, 250 }, new[] { 300, 50, 250, 150, 250 }, new[] { 250, 100, 300, 200, 150 }
        };

        /// <summary>The scope a genre's games are usually made at (Scope): its players expect that much of a game.</summary>
        public static readonly int[] UsualScope = { 2, 1, 2, 1, 1, 2, 1, 0, 0, 2, 0, 1 };

        /// <summary>Genres whose players play together online: their games need servers, and a subscription suits them.</summary>
        public static readonly bool[] Online = { false, false, false, false, false, true, true, false, false, true, false, false };

        /// <summary>Where the critics' bar stands for each genre on the first day, points out of 100: a game of that quality reviews at 50.</summary>
        public static readonly int[] ExpectBase = { 54, 52, 56, 54, 52, 55, 54, 50, 52, 56, 48, 50 };
        public const int ExpectRise = 1200;         // thousandths of a point the bar rises each market year: players expect more as games get better
        public const int ExpectMost = 80;           // ...up to this
        public const int SequelExpect = 3;          // points more expected of a sequel: it must beat the first

        // ---------------------------------------------------------------- platforms (Platform: PC, console, mobile)

        public const int GamePlatforms = 3;         // a game is sold on PC, consoles and phones; Platform.Direct is not a games store
        public static readonly long[] InstalledBase = { 1000000000L, 650000000L, 2600000000L };   // people who play on each, in the city and the world, on the first day
        public static readonly int[] BaseGrowth = { 30, 40, 50 };   // thousandths more each of the save's years at first...
        // ...towards these thousandths of the first day's at the most (ScaleRules.Grown): the world's players fill out to some 1.3 billion on
        // PCs, 900 million on consoles and 3.9 billion on phones (some 4.5 billion players in all, from about 3.4 billion now), so a long game
        // in a short year keeps the studio's markets in proportion; ten of the save's years bring a fifth to a third more players
        public static readonly int[] BaseCeiling = { 1300, 1400, 1500 };

        /// <summary>Of each platform's players, thousandths who like each genre, by platform then Genre.</summary>
        public static readonly int[][] GenreShare =
        {
            new[] { 300, 200, 300, 280, 250, 120, 120, 150, 150, 350, 200, 100 },
            new[] { 400, 300, 300, 80, 120, 300, 250, 80, 180, 380, 100, 220 },
            new[] { 120, 80, 150, 180, 120, 80, 100, 450, 50, 100, 500, 150 }
        };

        /// <summary>
        /// The average genre's PC players on the first day (InstalledBase[PC] x the average of GenreShare[PC]): the audience
        /// ScaleRules' bands are set for. A game is seen by its share of its genre's players as its budget is of this
        /// (<see cref="StudioRules.Visibility"/>), so a typical PC game of each scope meets the bands' audience.
        /// </summary>
        public const long ReferenceMarket = 210000000L;

        public static readonly int[] PlatformPrice = { 1000, 1150, 250 };   // thousandths of the scope's usual price a game sells at on each platform
        public const int MobilePremium = 250;       // thousandths of the buyers a game sold for a price finds on phones, where players expect free games
        public const int CertificationDays = 10;    // days a console's certification holds back a finished game
        public static readonly long[] CertificationCost = { 300000L, 2500000L, 10000000L };   // cents, by scope

        // ---------------------------------------------------------------- the plan and production

        public const long PersonDayCents = 25000;   // a mid-level professional's day at the start's prices: what a budget's team share buys
        public const int PointCents = 3125;         // ...so a work point costs this many cents (a mid-level in good heart gives FirmsCatalogue.PointsPerDay)
        public const int LaunchShare = 50;          // thousandths of a budget kept for the launch: the build, the stores, the press kit

        /// <summary>The budgets the concept offers, cents: the slider's steps across the three scopes (ScaleRules.ScopeOf names each).</summary>
        public static readonly long[] Budgets =
        {
            5000000L, 10000000L, 20000000L, 35000000L, 50000000L,
            100000000L, 200000000L, 300000000L, 600000000L, 1000000000L,
            2000000000L, 4000000000L, 6000000000L, 10000000000L, 15000000000L
        };

        public static readonly int[] PriceSteps = { 500, 750, 1000, 1250, 1500 };   // thousandths of the usual price the concept offers
        public static readonly int[] PhaseShare = { 150, 700, 150 };   // thousandths of the work in pre-production, production and polish
        public static readonly int[] PartNeed = { 150, 250, 200, 300, 100 };   // thousandths of the work each part needs, by Part, whatever the genre

        public const int AmbitionLean = 850;        // thousandths of the planned work a lean concept takes...
        public const int AmbitionBold = 1200;       // ...and an ambitious one
        public const int CraftBase = 20;            // points of craft anybody brings to a part...
        public const int CraftPerSkill = 600;       // ...thousandths of a point more for each point of their skill...
        public const int CraftPerLevel = 4;         // ...points more for each level above junior...
        public const int CraftExperienceMost = 12;  // ...and up to this many for the genre's experience
        public const int ExperienceFull = 600;      // person-days in a genre that bring the whole of it (about two games' worth)
        public const int ExperienceMost = 2000;     // person-days a file keeps per genre
        public const int OwnEngineCraft = 8;        // points of tech craft a studio's own engine adds
        public const int OwnEngineWork = 900;       // thousandths of the work a game takes on the studio's own engine (its tools)
        public const int EngineShare = 300;         // thousandths of a studio's engineering points that go to building its own engine while a game is made
        public static readonly long[] EngineFee = { 0L, 2000000L, 20000000L };   // a licensed engine's fee a game, cents, by scope (the royalty is ScaleCatalogue's)

        // the points of a day by family, thousandths to each Part (design, tech, art, content, polish)
        public static readonly int[] EngineeringSplit = { 0, 400, 0, 600, 0 };
        public static readonly int[] DesignSplit = { 300, 0, 500, 200, 0 };
        public static readonly int[] ProductSplit = { 1000, 0, 0, 0, 0 };
        public const int PolishPhaseShare = 500;    // thousandths of every point that goes to polish in the polish phase

        public const int Coverage = 1000;           // thousandths: a part built as planned counts in full...
        public const int CoverageMost = 1100;       // ...and work past the plan adds half as much, up to this

        // bugs
        public const int BugRoom = 90;              // thousandths of a bug a game holds for each work point before reviews see them in full (as FirmsCatalogue.GameBugRoom)
        public const int BugPenaltyMost = 20;       // points off the reviews for a game shipped at its bug room or past it
        public const int FixShareBuild = 100;       // thousandths of the points that fix found bugs while a game is built...
        public const int FixSharePolish = 500;      // ...in polish
        public static readonly int[] FixShares = { 500, 250, 0 };   // ...or as a playtest decided: fix everything, the worst, ship them
        public const int ReportPerDay = 50;         // thousandths of the bugs still hidden that players report each day after launch

        // the milestone review
        public const int MilestoneGraceDays = 10;   // days a milestone waits for its review before the team holds the course by itself
        public const int CutWork = 900;             // thousandths of the work left after cutting a feature (the parts' plan stays, so it shows)
        public const int DelayWork = 1100;          // ...after a delay
        public const int CrunchDays = 14, CrunchOutput = 250, CrunchBurnout = 15, CrunchPolish = 5;   // a crunch: days, more output, burnout a day worked, polish craft lost
        public const int BoostDays = 30, BoostOutput = 200;   // more budget: contractors for so many days adding so much output, paid at PointCents a point

        // the playtest
        public static readonly int[] PlaytestFinds = { 300, 600, 850 };   // thousandths of the hidden bugs the team, a closed beta and an open beta find
        public const int BetaHype = 30;             // hype a closed beta adds
        public const int LeakChance = 100;          // per mille a closed beta leaks: hype moves by the game's quality

        // ---------------------------------------------------------------- hype and marketing

        public const int HypeMost = 1000;           // thousandths of the audience excited: the most there is
        public const int HypeAnnounce = 60, HypeSequel = 60;   // hype an announcement adds, and more for a sequel of a known game
        public const int HypeTrailer = 80, TrailerGap = 30;    // a trailer, and the days before another
        public const int TrailerWork = 300, ExpoWork = 500, PreviewWork = 700;   // thousandths of the work done before there is something to show
        public static readonly long[] TrailerCost = { 500000L, 5000000L, 40000000L };   // cents, by scope
        public static readonly long[] ExpoCost = { 1500000L, 15000000L, 100000000L };
        public const int ExpoHype = 60, ExpoPerPoint = 3;      // an expo demo: hype, and more or less for each point of quality above or below 50
        public const int ExpoFrom = 450, ExpoTo = 550;          // thousandths of the calendar's year the expo runs in (early summer)
        public const int PreviewPerPoint = 4;       // press previews: hype for each point of quality above or below 50
        public const int HypeQuietDays = 21;        // days without news before hype fades...
        public const int HypeFade = 30;             // ...thousandths of it a day
        public static readonly int[] CampaignShare = { 0, 100, 200, 300, 500, 800 };   // a paid campaign: thousandths of the budget spent, through ScaleRules.Reach
        public const int CampaignDays = 90;         // ...spread over this many days, sixty before launch and the launch window
        public const int CampaignHype = 2;          // hype a day of campaign adds per step
        public const int HypeReach = 1000;          // thousandths more reach at full hype (twice the reach): the excited tell others
        public const int BacklashPerQuality = 9;    // hype a game's quality carries (quality x this): above it the critics punish the gap...
        public const int BacklashStep = 50;         // ...a point for each this much over...
        public const int BacklashMost = 12;         // ...up to this many

        // ---------------------------------------------------------------- reviews

        public const int ReviewBase = 50;           // a game of the bar's quality reviews at this
        public const int ReviewPerQuality = 1200;   // thousandths of a review point for each point of quality under the bar (over it the score shares out the room left: StudioRules.CriticsScore)
        public const int DrmReview = 3;             // points DRM costs a game's reviews
        public const int FitReviewStep = 25;        // a point of review for each this many thousandths of the setting's fit over or under a good fit
        public const int ReputationReviewStep = 100;   // a point for each this much of the studio's reputation over or under 500
        public const int CriticsSpread = 3;         // points the critics fall either side
        public const int AcclaimScore = 90, FlopScore = 40;   // scores the paper and the market make news of
        public const int HitScore = 85;             // a score that starts a trend in its genre and counts as a hit
        public const int TrendDays = 180, TrendLift = 200;     // a trend: days, and thousandths more of the genre's audience

        // ---------------------------------------------------------------- sales

        public const int TypicalRivalAppeal = 2000; // the appeal of the live games a genre usually has (two typical ones): the share the bands are set at
        public const int AppealBase = 200, AppealPerScore = 16;   // a game's appeal in its genre: base and per point of score (a 50 is 1000)
        public const int FreshDays = 30, FreshHalfLife = 120;     // a game is new for so many days, then its appeal halves over this
        public const int WindowDays = 30;           // days of a launch window (ScaleCatalogue.LaunchDays)
        public const int WindowWeights = 465;       // the window's days weighed 30 down to 1, summed: front loaded
        public const int SeasonHoliday = 1150, SeasonSummer = 920;   // thousandths of the day's buyers at the year's end and in summer
        public const int HolidayFrom = 830, SummerFrom = 450, SummerTo = 600;   // thousandths of the calendar's year
        // A great game's word of mouth is the money scale's tail (ScaleCatalogue.Tail: a whole life of up to five times its launch
        // window), so a high score does not widen its reach as well: that would count it twice and lift a game of 80 or 90 far past the
        // scale's return for its quality.
        public static readonly int[] Piracy = { 200, 50, 120 };   // thousandths of would-be buyers who pirate a game sold for a price, by platform
        public const int DrmEarly = 300, DrmLater = 800, DrmDays = 90;   // what DRM leaves of piracy in its first days and after it is cracked
        public const int BugRefundsMost = 60;       // thousandths more refunds for a game left with its reported bugs
        public const int SaleDays = 7, SalePrice = 600, SaleBuyers = 1600;   // a sale: days, price and buyers brought forward from those still to come
        public const int F2PPlayers = 4000;         // thousandths: a free game finds four players for each buyer a paid one would
        public const int F2PSpend = 200;            // thousandths of the usual price a free game's player spends over their time in it
        public static readonly int[] SubscribeTake = { 150, 500 };   // thousandths of buyers who would subscribe, an offline genre and an online one
        public const int SubscribeMonth = 250;      // a month's subscription, thousandths of the scope's usual price
        public const int ChurnBase = 160, ChurnPerScore = 1200;   // a month's churn of subscribers: thousandths, less a thousandth of a thousandth per point of score
        public const int EarlyPrice = 750;          // thousandths of the price while in early access
        public const int EarlyFrom = 600;           // thousandths of the work done before a game can go out in early access
        public const int EarlyRelaunch = 600;       // thousandths of a full launch's window that the full release of an early access game brings
        public const int TicketsPerThousand = 5;    // support tickets a day per thousand active players
        public const int TicketReputationStep = 200; // a point of the studio's reputation lost a day for each fifth of its tickets left unanswered

        // ---------------------------------------------------------------- after launch

        public static readonly int[] UpdateWork = { 80, 150 };   // thousandths of the game's work a free update and a paid add-on take
        public const int UpdateLift = 200;          // thousandths more of the sales still to come a free update brings, and a new window
        public const int UpdateReputation = 20;     // reputation a free update earns
        public const int AddOnPrice = 250;          // thousandths of the game's price an add-on sells at
        public const int AttachLeast = 50, AttachPerScore = 3;   // thousandths of owners who buy an add-on: the least and per point of score
        public const int PortWork = 250;            // thousandths of the game's work a port takes
        public const int LocaliseWork = 100, LocaliseLift = 150;   // localisation: work, and thousandths more of the world's audience
        public const int FansPerScore = 4;          // thousandths of a game's buyers who follow its sequel, per point of score (a 90 brings 360)
        public const int BurnGap = 15, BurnScore = 55;   // a sequel this far under the first, or under this, burns the name: its fans halve
        public const int AwardReach = 80, AwardReputation = 60, AwardScore = 70;   // an award: reach, reputation, and the least score to be in the running

        // publishers
        public const int AdvanceShare = 500;        // thousandths of the budget a publisher advances at the greenlight...
        public const int Royalty = 350;             // ...the studio's royalty of the net once the advance is earned back...
        public const int PublisherMarketing = 250;  // ...and thousandths of the budget the publisher spends on marketing of its own

        // ---------------------------------------------------------------- bonuses

        public static readonly int[] BonusPool = { 0, 50, 100 };   // thousandths of a release's profit paid as a bonus on its day
        public static readonly int[] LeadRoyalty = { 0, 10, 20 };  // thousandths of the net paid to the leads each month
        public const int BonusDay = 90;             // days after launch the bonus pool is paid
        public const int BonusMorale = 80;          // morale a bonus adds at once
        public const int HitReturn = 3000;          // thousandths of its budget a release returns by its bonus day to count as a hit
        public const int SnubPull = 3000;           // thousandths: rivals pull this hard on the team of a hit that paid no bonus...
        public const int SnubDays = 180;            // ...for so many days...
        public const int SnubMorale = 50;           // ...and morale falls this much once

        // ---------------------------------------------------------------- the rival studios (the market's game companies and new ones)

        public static readonly string[] RivalNames =
        {
            "Pixel Harbour Games", "Copperkettle Games", "Tidewater Interactive", "Blue Kestrel Games", "Saltmarsh Studios", "Hollowpine Interactive", "Gilded Quill Games"
        };

        public static readonly int[] RivalStock = { 110, 0, 0, 0, 0, 0, 0 };   // its company on the stock market, 0 for none
        public static readonly int[] RivalScope = { 2, 0, 1, 0, 1, 2, 0 };
        public static readonly int[][] RivalGenres =
        {
            new[] { 0, 9 }, new[] { 7, 10 }, new[] { 1, 4 }, new[] { 8, 7 }, new[] { 3, 2 }, new[] { 2, 0 }, new[] { 11, 10 }
        };

        public static readonly int[] RivalGapLeast = { 140, 260, 420 };   // days between a rival's releases, by its scope: the least...
        public static readonly int[] RivalGapMost = { 220, 380, 600 };    // ...and the most
        public static readonly int[] RivalQuality = { 52, 57, 62 };       // the quality its teams average, by scope
        public const int RivalGamesKept = 24;       // rival games kept: the newest; older ones have stopped selling
        public const int RivalStockPerPoint = 3;    // basis points a listed rival moves for each point of a release's score over or under 60
        public const int OwnStockPerPoint = 4;      // ...and the player's own listed company for each of theirs

        public static readonly string[] TitleFirst =
        {
            "Ember", "Iron", "Hollow", "Star", "Crimson", "Silent", "Wild", "Copper", "Frost", "Sunken", "Neon", "Paper", "Thunder", "Velvet", "Lantern", "Granite"
        };

        public static readonly string[] TitleSecond =
        {
            "Crown", "Frontier", "Tides", "Circuit", "Garden", "Legion", "Harbour", "Echoes", "Kingdom", "Rally", "Heist", "Orchard", "Signal", "Vault", "Pilgrim", "League"
        };

        public const int GamesStored = 64;          // the studio's games the save reads back: past every product the firms may hold (FirmsCatalogue.ProductsStored)
        // (no bound on the professionals' genre experience: it follows the people part's files, which have none, and StudioRules.Prune lets go the experience of those who have gone)
    }
}
