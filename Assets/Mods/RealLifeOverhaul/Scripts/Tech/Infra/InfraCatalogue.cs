namespace RealLifeOverhaul.Tech
{
    /// <summary>The figures of data centres, the cloud and AI compute, with their end game's scale. Game-free; cents at the start's prices.</summary>
    public static class InfraCatalogue
    {
        // Fitting out
        public const int Tiers = 4;                                                   // of redundancy, Tier I to IV
        public static readonly int[] FitPerSqm = { 20000, 40000, 70000, 110000 };     // cents a square metre, by tier
        public static readonly int[] OutageChance = { 20, 10, 3, 1 };                 // per mille a day, by tier, fully staffed
        public const int SqmPerRack = 6;              // square metres
        public const int RackCost = 4000000;          // a rack of servers, cents
        public const int GpuRackCost = 25000000;      // a rack of AI accelerators
        public const int WriteDownDays = 1825;        // a rack is written down over five years
        public const int RackKw = 8, GpuRackKw = 32;  // kW drawn
        public const int WaterPerMwh = 1800;          // thousandths of a cubic metre of cooling water for each megawatt hour
        public const int RacksPerTechnician = 20;     // racks one technician keeps running
        public const int OutageShortMost = 3000;      // thousandths more outage chance with no technician at all (in step with how many are short)
        public const int CentresMost = 12;            // data centres
        public const int NetNeeded = 1000;            // Mbps of a business connection a data centre needs

        // Letting racks
        public const int ColoMonth = 240000;          // a rack let to a company, cents a month
        public const int ColoDemand = 40;             // racks the city's companies would take at that price in a steady economy
        public const int ColoChurn = 30;              // thousandths a month who go

        // The cloud
        public const int UnitsPerRack = 40;           // compute a rack of servers sells as
        public const int GpuUnitsPerRack = 25;        // AI compute a rack of accelerators sells as
        public const int UnitDay = 350;               // a unit of compute a day at the rivals' list price, cents
        public const int GpuUnitDay = 4000;           // a unit of AI compute a day
        public const int CloudDemand = 3000;          // units the city's companies buy at the list price in a steady economy
        public const int GpuDemand = 200;             // units of AI compute
        public const int OwnHostingShare = 650;       // thousandths of the rivals' list price the player's own firms pay their own cloud
        public const int LicenceFee = 5000000;        // a cloud services licence a year, cents
        public const int CloudRacksLeast = 20;        // racks across the data centres a cloud needs to open
        public const int CloudTierLeast = 1;          // ...at tier II or better (0-based)
        public const int CloudEngineersLeast = 2;     // ...and site reliability or DevOps people
        public const long CloudCashLeast = 50000000;  // ...and cash
        public const int PriceWarShare = 300;         // thousandths of the city's compute the cloud holds before the rivals cut their prices
        public const int PriceCut = 50;               // thousandths a month the rivals cut by
        public const int PriceFloor = 600;            // ...down to this share of their list price

        // The hyperscaler
        public const int HyperRegions = 3;            // districts with a data centre of the cloud's
        public const int HyperRacks = 300;            // racks across the data centres
        public const long HyperCash = 500000000;      // cents
        public const int HyperDemand = 3000;          // thousandths: a hyperscaler sells to the city's large companies too

        // AI models
        public const int TrainBase = 400;             // AI compute unit-days the first generation of a model takes
        public const int TrainGrowth = 2000;          // thousandths: each generation takes twice the last
        public const int RentedGpuShare = 1300;       // thousandths of the list price the player pays a rival cloud for AI compute
        public const int InferencePerCustomer = 60;   // AI compute unit-thousandths a customer of a model uses a day

        // At scale (Core/EndScale: real figures, mixed in by its share): a data centre costs about ten million a megawatt,
        // and a rack of the new kind sells far more compute than the old at the same price a unit. Racks bought before
        // the scale stay the old kind until they are replaced.
        public static readonly int[] FitPerSqmScale = { 700000, 1000000, 1300000, 1800000 };   // cents a square metre, by tier
        public const int RackCostScale = 30000000;        // a rack of dense servers, cents ($300,000)
        public const int GpuRackCostScale = 200000000;    // a rack of current AI accelerators ($2,000,000)
        public const int IncomeAtScale = 1000;            // thousandths: the one knob for what the new kind earns, its compute and its rent
        public const int UnitsPerRackScale = 800;         // compute a rack of the new kind sells as
        public const int GpuUnitsPerRackScale = 360;      // AI compute a new AI rack sells as
        public static readonly int[] PueScale = { 1700, 1500, 1350, 1200 };   // thousandths of the racks' own power drawn with the cooling, by tier
        public const int UpkeepRack = 17000;              // cents a day for a rack of the new kind: support, spares, transit and software licences
        public const int UpkeepGpu = 120000;              // ...for a new AI rack
        public const int FacilityYear = 40;               // thousandths of the fit-out's worth a 365-day year: the building's upkeep and insurance
        public const int ColoDemandScale = 120;           // racks the city's companies rent at scale
        public const int ColoMonthScale = 4800000;        // a rack of the new kind let as managed servers, cents a month ($48,000); the old kind keeps the old rent
        public const int CloudDemandScale = 120000;       // units the city buys at the list price in a steady economy, at scale
        public const int GpuDemandScale = 4000;           // units of AI compute
        public const int HyperDemandScale = 4000;         // thousandths: a hyperscaler at scale sells to the city's large companies and beyond it...
        public const int HyperRegionScale = 1500;         // ...and each region past the three it needs opens this much more
        public const long LicenceFeeScale = 50000000;     // the cloud's licences and certifications a game year, cents ($500,000)
        public const int CloudRacksLeastScale = 40;       // racks
        public const long CloudCashLeastScale = 5000000000;    // $50,000,000
        public const int HyperRacksScale = 400;           // racks
        public const long HyperCashScale = 100000000000;       // $1,000,000,000

        // The crews a data centre at scale hires besides its professionals, a share of the need a day, and the cloud's
        // operations people.
        public const int Operators = 0, Engineers = 1, Security = 2, CrewKinds = 3;
        public static readonly int[] CrewWage = { 26000, 34000, 17000 };   // cents a day each
        public const int OpsWage = 38000;                 // cents a day: the cloud's operations and support people
        public const int OperatorRacks = 10;               // racks of either kind an operator looks after
        public const int OperatorGpu = 2;                 // ...AI racks
        public const int OperatorsLeast = 4;              // round the clock, at the least
        public const int EngineerKw = 250;                // kW of draw an electrical and cooling engineer looks after
        public const int EngineersLeast = 2;              // electrical and cooling engineers
        public const int GuardsLeast = 4;                 // security on the site round the clock...
        public const int GuardSqm = 1000;                 // ...and one more for each this many square metres
        public const int OpsUnits = 1500;                 // units of the cloud's capacity one operations person looks after
        public const int CrewPace = 40;                   // thousandths of the need hired a day while short
        public const int RacksStep = 25;                  // racks bought at a time at scale (5 without it)
    }
}
