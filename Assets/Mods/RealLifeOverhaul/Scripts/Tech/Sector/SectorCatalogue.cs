namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The markets the tech firms other than the game studio sell into under Living population: one market for each kind of
    /// product and buyer, shared by every product in it, the player's and the rivals'. Saved as a number: never renumber.
    /// </summary>
    public enum SectorMarket
    {
        ConsumerSoftware = 0, BusinessSoftware = 1, EnterpriseSoftware = 2, Apps = 3, Licences = 4, ConsumerAi = 5, BusinessAi = 6, EnterpriseAi = 7, Count = 8
    }

    /// <summary>Who buys in a market: the city's people (and the world they stand for), its small and mid-sized businesses, its large organisations.</summary>
    public enum SectorBuyers
    {
        People = 0, Businesses = 1, Enterprises = 2, Count = 3
    }

    /// <summary>
    /// The figures of the other tech firms under Living population (plans/tech-overhaul DRAFT2 section 3, "Ceilings
    /// replaced"): the shared markets sized from the city's people and firms, the curves that take the place of the old
    /// clamps, the rivals that sell by the same rules, the market for IT services, the data centres' and the cloud's
    /// demand from the city's firms and labs, and the desk sessions' effects. Cents at the start's prices and thousandths
    /// unless named. Game-free, read by <see cref="SectorRules"/> and held by EconomyCheck's SectorChecks.
    /// </summary>
    public static class SectorCatalogue
    {
        public const int MarketYear = 365;           // days of the markets' year: the world beyond the city grows by it, whatever the calendar's length

        // ---------------------------------------------------------------- the city a market is sized from

        // The documented city while the population is not running (it is generating, or a city it does not know): the base
        // city as the owner chose it on 1 Nov 2026 (POPULATION.md section 14 and its recalibration, 50 to 65 thousand people),
        // its firms as SectorRules.FirmsOf counts them from such a city's employers.
        public const int ReferencePeople = 55000;
        public const int ReferenceSmall = 2500;      // businesses under 50 people: the shops, venues and the small firms on the office floors
        public const int ReferenceMid = 160;         // ...50 to 999 people
        public const int ReferenceLarge = 30;        // ...1,000 and more: the large firms, the public bodies and the mod's own companies

        // How the population's employers become firms by size (SectorRules.FirmsOf): an office building's other floors hold
        // many firms, which Manhattan's office market splits about so between small, mid-sized and large ones.
        public const int SmallLeast = 50, LargeLeast = 1000;   // people: a firm of fewer than 50 is small, of 1,000 or more large
        public const int TenantLargeShare = 450;     // thousandths of an office building's tenant jobs in large firms' offices (US County Business Patterns, New York County)
        public const int TenantMidShare = 300;       // ...in mid-sized firms; the rest in small ones
        public const int LargeFirmJobs = 2500;       // people at a large firm, on average: so many tenant jobs make one
        public const int MidFirmJobs = 150;          // ...at a mid-sized one
        public const int SmallFirmJobs = 12;         // ...at a small one
        public const int PublicLargeLeast = 200;     // a public body of this many people buys as a large organisation

        /// <summary>
        /// How many buyers beyond the city each of the city's stands for, thousandths, by SectorBuyers: software is sold online,
        /// so a resident stands for some 60 people of the region and the internet who could use a consumer product, a business
        /// for five like it in the region, a large organisation for thirteen across the country. Sized so the documented city's
        /// markets on the first day hold a firm of a few people with one good product inside the software payback target
        /// (EconomyCheck's SectorChecks.Sims), now that every product of a kind shares one market instead of each having its own
        /// (the old 400 thousand people, 8 thousand businesses and 400 companies a product).
        /// </summary>
        public static readonly int[] World = { 60000, 5000, 13000 };

        public static readonly int[] WorldGrowth = { 50, 30, 20 };   // thousandths more buyers each market year beyond the city, by SectorBuyers: the internet's people grow fastest

        // The city's mood for digital spending: its households' spending on software and services against the first day's, a person
        // (the population's Digital kind), within these bounds; 1000 while the population is not running.
        public const int DigitalLeast = 500, DigitalMost = 2000;

        // ---------------------------------------------------------------- a product in its market

        /// <summary>Thousandths of the gap between a product's customers and its share of the market that closes in a day, by SectorBuyers (millionths): people move fast, companies slowly.</summary>
        public static readonly int[] Pace = { 30000, 14000, 6000 };

        /// <summary>Thousandths of a product's awareness each customer adds, over the market's buyers, by SectorBuyers: word of mouth, strongest among people.</summary>
        public static readonly int[] WordOfMouth = { 3000, 1000, 500 };

        public const int FeatureCurve = 1000;        // thousandths: a product with its market's leader's features appeals 1000; twice as many 1333, a third 500 (2r/(1+r), with no clamp)
        public const int DesignGain = 400;           // thousandths of appeal designers lift a product towards: a designer's points for an engineer's give half (200), with no cap but the curve
        public const int SellingGain = 2000;         // thousandths faster business and company buyers sign with sellers: half of it at SellingHalf points
        public const int SellingHalf = 40;           // ...a seller in good heart gives 8 points a day, so five sellers bring half
        public const int ProductGain = 400;          // thousandths more feature points product people give towards: half at one for every engineer
        public const int SupportGain = 1000;         // thousandths more churn with no support at all: the curve of the support short
        public const int ReliabilityHalf = 300;      // thousandths the people bring at which a service is half way from its base to sound: two engineers of average skill
        public const int SecurityHalf = 300;         // ...the same for analysts and security posture

        /// <summary>A product's features have no cap under Living population: each costs more than the last (FirmsCatalogue.FeatureStep), so they grow slower as they go.</summary>
        public const int FeaturesMost = 100000;

        // Reputation: earned towards a target set by the quality of what a firm sells and does, with no top of 800; climbing gets slower near the top.
        public const int ReputationBase = 250;       // the target with nothing to show
        public const int ReputationSpan = 750;       // ...and the quality's part of it: a firm whose products are all sound heads for 1000
        public const int ReputationPace = 20;        // a twentieth of the way a day at the bottom...
        public const int ReputationSlow = 60;        // ...and a sixtieth near the top

        // ---------------------------------------------------------------- the products' limits that stay (performance and the save), raised

        public const int ProductsMost = 48;          // products being built or on sale across the player's firms (36 before; each is a few comparisons a day)
        public const int ProductsPerFirm = 16;       // ...and at any one firm (12 before)
        public const int ContractsMost = 40;         // contracts kept, finished ones a month after (24 before; a small record each)

        // ---------------------------------------------------------------- the rivals (the market's listed companies and the startups)

        /// <summary>
        /// The markets each listed company sells in, one bit per SectorMarket, in the order of HiringCatalogue.RivalFirms (as
        /// CompetitorRules.CompanyMakes says what they make): Codeharbour Software's company software and licences, Abacus
        /// Lane's business software, Shopfront Online's services and apps for people, Tallyrow Analytics' analytics and its
        /// models, Signpost Search's apps and assistant, Ledgerline Payments' payments and app. The rest make no software.
        /// </summary>
        public static readonly int[] CompanyMarkets =
        {
            (1 << (int)SectorMarket.EnterpriseSoftware) | (1 << (int)SectorMarket.Licences),
            0,
            1 << (int)SectorMarket.BusinessSoftware,
            (1 << (int)SectorMarket.ConsumerSoftware) | (1 << (int)SectorMarket.Apps),
            0,
            (1 << (int)SectorMarket.BusinessSoftware) | (1 << (int)SectorMarket.BusinessAi) | (1 << (int)SectorMarket.EnterpriseAi),
            (1 << (int)SectorMarket.Apps) | (1 << (int)SectorMarket.ConsumerAi),
            0, 0,
            (1 << (int)SectorMarket.BusinessSoftware) | (1 << (int)SectorMarket.Apps),
            0, 0
        };

        /// <summary>Each company's engineering points a day (about so many engineers in good heart), shared over its products, in the order of HiringCatalogue.RivalFirms.</summary>
        public static readonly int[] CompanyTeam = { 160, 0, 96, 120, 0, 140, 200, 0, 0, 110, 0, 0 };

        /// <summary>Each company's name with clients and customers on the first day, thousandths, in the order of HiringCatalogue.RivalFirms.</summary>
        public static readonly int[] CompanyReputation = { 720, 780, 650, 680, 700, 660, 800, 700, 760, 690, 740, 710 };

        /// <summary>Each company's marketing a day, cents: what keeps its products known.</summary>
        public static readonly int[] CompanyMarketing = { 90000, 0, 40000, 70000, 0, 60000, 150000, 0, 0, 50000, 0, 0 };

        public const int RivalReliability = 700;     // thousandths: a company's services are kept running about as well as a firm with two good site reliability engineers
        public const int RivalFeatureShare = 250;    // thousandths of a rival's points that go to new features (a large company keeps most of its team on what it has, its sales and its old clients)
        public const int RivalFaults = 120;          // thousandths of a bug a rival's feature point leaves (an ordinary team's)
        public const int RivalFixShare = 300;        // thousandths of its points that fix bugs
        public const int RivalCutLoss = 300;         // thousandths of its customers a rival product loses from its peak before it cuts its price a step...
        public const int RivalCutGap = 30;           // ...at most once in so many days...
        public const int RivalRaiseGap = 90;         // ...and raises it back once it has held its peak again this long
        public const int RivalPeakDays = 60;         // days a peak is remembered for
        public const int RivalStockShip = 20;        // basis points a listed rival moves when it overtakes the player's product with a feature
        public const int RivalStockCut = -30;        // ...and when it cuts its price
        public const int StartupTeamSeed = 24, StartupTeamPerStage = 24;   // a startup's points a day: a seed team, and more for each round it has raised
        public const int StartupMarketing = 8000;    // a startup's marketing a day, cents
        public const int StartupReputation = 500;    // a startup's name to begin with
        public const int RivalProductsMost = 64;     // rival products kept (twelve of the companies' and a startup's each while it runs); the save reads no more back

        /// <summary>The market a startup of each sector sells in (Venture's order), -1 for one that sells no software of these kinds (games are the studio's).</summary>
        public static readonly int[] StartupMarket =
        {
            (int)SectorMarket.BusinessSoftware, (int)SectorMarket.ConsumerSoftware, (int)SectorMarket.ConsumerSoftware, (int)SectorMarket.BusinessAi, -1,
            (int)SectorMarket.Apps, (int)SectorMarket.EnterpriseSoftware, (int)SectorMarket.EnterpriseSoftware, (int)SectorMarket.BusinessSoftware
        };

        // ---------------------------------------------------------------- IT services: the market for contracts

        /// <summary>Projects a year each firm of the city puts out, thousandths, by SectorBuyers (people none, small businesses, large organisations), and the mid-sized ones between.</summary>
        public const int ProjectsSmall = 300, ProjectsMid = 2000, ProjectsLarge = 6000;
        public const int ServicesWorld = 1000;       // thousandths: IT work is bought near at hand, so the city's own firms are its market
        public const int RivalServiceReputation = 700;   // what the city's rival IT firms weigh as, each, in the market for contracts
        // thousandths: the weight of all the rival IT firms together against one of the player's: some twenty of them,
        // so an ordinary firm of the player's wins about one project in twenty
        public const int RivalServiceWeight = 20000;
        // thousandths: the city's rival agencies together against one web agency or design studio of the player's: some ten
        // of them, as branding and websites are bought from fewer, smaller shops, so an agency's takings from its work stay
        // where the old rule had them
        public const int RivalAgencyWeight = 10000;
        public const int ClientSmallWork = 700;      // thousandths of a kind's usual points a small client's project has...
        public const int ClientLargeWork = 1600;     // ...and a large one's
        public const int PortfolioReach = 400;       // thousandths more of the large clients' share a portfolio piece brings while it is fresh
        public const int IncidentChance = 4;         // per mille a day that a security firm's managed client is attacked: an incident to answer
        public const int IncidentDays = 3;           // days an incident's response is given
        public const int IncidentPoints = 40;        // points an incident takes

        // ---------------------------------------------------------------- data centres and the cloud

        // Racks the city's firms rent, thousandths of a rack a firm by size: a large firm a rack and a half, a mid-sized one an eighth, a small one hardly any.
        public const int RacksLarge = 1500, RacksMid = 120, RacksSmall = 4;
        public const int ColoScale = 3000;           // thousandths: the region a data centre at the end game's scale lets to (ColoDemandScale over ColoDemand)
        // Compute the city's firms buy a day at the list price, thousandths of a unit a firm, and AI compute: with the rivals' hosting and
        // labs, about what the old fixed demand was in the documented city at an even price and name (EconomyCheck's SectorInfra).
        public const int CloudLarge = 40000, CloudMid = 6000, CloudSmall = 300;
        public const int GpuLarge = 2000, GpuMid = 200, GpuSmall = 8;
        public const int CloudScale = 40000;         // thousandths: what the end game's scale opens beyond the city (CloudDemandScale over CloudDemand)
        public const int GpuScale = 20000;           // ...of AI compute (GpuDemandScale over GpuDemand)
        public static readonly int[] HostingUnits = { 1, 20, 500 };   // thousandths of a unit of compute a customer of a rival's software takes a day, by Segment
        public const int RivalLabCompute = 1500;     // AI compute unit-days a listed company's model trains on a day...
        public const int StartupLabCompute = 200;    // ...and a startup's
        public const int RivalCloudRent = 50;        // thousandths of the rival labs' training rented from the clouds the player's competes with (the rest on their own racks)
        public const int RivalCloudReputation = 700; // the rival clouds' name
        public const int RivalCloudWeight = 1000;    // thousandths: the rival clouds' weight together against the player's at an even price and name
        public static readonly int[] TierAppeal = { 700, 1000, 1250, 1500 };   // thousandths: how a data centre's tier weighs with a company letting racks
        public const int RivalRackAppeal = 1200;     // the rival data centres' weight together
        public const int SpikeGapLeast = 45, SpikeGapMost = 90;   // days between power price spikes
        public const int SpikeDaysLeast = 4, SpikeDaysMost = 8;   // ...and how long one lasts
        public const int SpikeLeast = 1500, SpikeMost = 2500;     // thousandths of the usual tariff a spike costs
        public const int HedgeFee = 120;             // thousandths of a month's power at the usual tariff that fixing the price for the spike costs
        public const int ThrottleCut = 500;          // thousandths of the AI racks' power throttling saves (and of their compute)

        // ---------------------------------------------------------------- the desk sessions

        public const int SprintDays = 7;             // a sprint's week
        public const int SprintPush = 250;           // thousandths more feature points with customer requests first...
        public const int SprintPushBugs = 300;       // ...thousandths more bugs from them...
        public const int SprintDebt = 3;             // ...times the debt paid down with debt and reliability first
        public const int SprintWeekly = 150;         // thousandths more feature points shipping weekly, and as many more bugs
        public const int SprintReady = 250;          // thousandths fewer bugs shipping when ready
        public const int OutageWindow = 3;           // days after an outage its response can be held
        public const int RollbackSaved = 800;        // thousandths of the customers an outage cost that a roll back brings back...
        public const int HotfixSaved = 500;          // ...a hotfix forward
        public const int HotfixRisk = 250;           // per mille a hotfix forward goes wrong: a second outage
        public const int PostMortemDays = 14;        // days a post-mortem's fixes keep a service sounder
        public const int PostMortemReliability = 300;   // thousandths of the way to sound they add
        public const int CreditShare = 500;          // thousandths of a day of revenue per customer lost that a credit costs
        public const int BigCustomerLeast = 50;      // business or company customers a product needs before a big one asks for something
        public const int BigCustomerEvery = 14;      // days between big customers' asks
        public const int BigDeal = 6;                // months of a company's subscription a big customer pays for its own feature
        public const int BigDivert = 300;            // thousandths of the engineering points the big customer's feature takes...
        public const int BigDivertDays = 14;         // ...for so many days
        public const int BigDiscount = 200;          // thousandths off a discount takes for a year
        public const int BigLost = 50;               // thousandths of a product's business customers a refusal costs
        public const int TrainingEvery = 14;         // days between training runs
        public static readonly int[] TrainCompute = { 1000, 1000, 500 };   // thousandths of a run's usual compute: own, rented, smaller
        public const int TrainRunDays = 10;          // a run gives so many days of the model's usual training at once
        public static readonly int[] DataQuality = { 1500, 700, 1100 };    // thousandths of a run's quality from licensed, scraped and synthetic data
        public static readonly int[] DataCost = { 2000000, 0, 600000 };    // cents a run of each kind of data costs
        public const int ScrapeRisk = 120;           // per mille scraped data brings a claim: a fine like a breach's
        public static readonly int[] SafetyIncident = { 0, 40, 220 };      // per mille a launch after full, standard or no safety checks goes wrong
        public static readonly int[] SafetyDays = { 3, 1, 0 };             // days a model's training waits for its checks
        public const int SafetyReputation = 40;      // reputation full checks earn, and an incident costs three times
        public const int BenchmarkWin = 30;          // reputation a benchmark won earns
        public static readonly int[] LaunchAwareness = { 300, 150, 100 };  // thousandths of awareness a launch adds: open weights, an interface for developers, partners first
        public const int LaunchDays = 30;            // days a launch's awareness lasts
        public const int OpenWeightsPrice = 600;     // thousandths of the price open weights leave (most run them on their own)
        public const int PartnersCompanies = 3;      // large customers a partners first launch signs at once
        public const int PitchSeniorWeight = 150;    // thousandths better a bid weighs with seniors on it
        public const int PitchJuniorWeight = -100;   // ...and worse with juniors
        public const int PitchJuniorWork = 150;      // thousandths more work a junior team takes
        public static readonly int[] PitchPrice = { 850, 1000, 1150 };     // thousandths of the fair price a low, fair and high bid asks
        public const int ScopeFrom = 300;            // thousandths of a project done before a client asks for more
        public const int ScopeMore = 200;            // thousandths more work the client asks for
        public const int ScopeChange = 250;          // thousandths of that work's value a change order adds
        public const int IncidentIsolateDone = 600;  // thousandths of an incident's work isolating everything does at once...
        public const int IncidentTargetDone = 400;   // ...targeted containment (more with seniors)
        public const int IncidentTraceRisk = 300;    // per mille tracing the attacker lets them do more harm
        public const int CapacityRacks = 5;          // racks capacity planning buys ahead (more at the end game's scale, as InfraRules.RacksStep)
        public const int CapacityAhead = 900;        // thousandths of the price racks bought ahead cost
        public static readonly int[] CapacityPrice = { 900, 1000, 1150 };  // thousandths of the rivals' price the cloud undercuts, matches or asks
        public const int FailoverRegions = 2;        // regions a failover needs
        public const int FailoverDays = 30;          // days a post-mortem keeps a data centre's outages down
        public const int FailoverCut = 400;          // thousandths of the outages it takes off
        public const int RevisionFrom = 500;         // thousandths of a design project done before the client asks for changes
        public const int RevisionMore = 150;         // thousandths more work a round of changes takes
        public const int PortfolioEvery = 30;        // days between portfolio pieces
        public const int PortfolioDays = 60;         // days a piece keeps winning bigger clients
        public const int PortfolioReputation = 30;   // reputation a piece earns
        public const int AwardChance = 350;          // per mille an award entry wins
        public const int AwardReputation = 90;       // reputation an award earns
        public const long AwardFee = 250000;         // cents an award entry costs
    }
}
