namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// Data centres, the cloud, the hyperscaler and AI compute: a warehouse of the player's fitted out as a data centre
    /// (to a tier of redundancy), racks of servers and of AI accelerators bought and written down, power and cooling water
    /// at the utilities' tariff, the technicians it needs, uptime and outages; racks let to the city's companies; a cloud
    /// of the player's that sells compute from its data centres' regions to the city's companies and to the player's own
    /// software firms, against rival clouds that cut their prices when it gains on them; the hyperscaler it can grow into;
    /// and AI models trained on compute. Game-free (the checkers compile it); amounts in cents at the start's prices.
    /// </summary>
    public static class InfraCatalogue
    {
        // ---------------------------------------------------------------- fitting out
        public const int Tiers = 4;
        public static readonly int[] FitPerSqm = { 20000, 40000, 70000, 110000 };     // cents a square metre, by tier
        public static readonly int[] OutageChance = { 20, 10, 3, 1 };                 // per mille a day, by tier, fully staffed
        public const int SqmPerRack = 6;
        public const int RackCost = 4000000;          // a rack of servers, cents
        public const int GpuRackCost = 25000000;      // a rack of AI accelerators
        public const int WriteDownDays = 1825;        // a rack is written down over five years
        public const int RackKw = 8, GpuRackKw = 32;  // power drawn
        public const int WaterPerMwh = 1800;          // thousandths of a cubic metre of cooling water for each megawatt hour
        public const int RacksPerTechnician = 20;     // racks one technician keeps running
        public const int RacksMost = 2000;
        public const int CentresMost = 12;
        public const int NetNeeded = 1000;            // Mbps of a business connection a data centre needs

        // ---------------------------------------------------------------- letting racks
        public const int ColoMonth = 240000;          // a rack let to a company, cents a month
        public const int ColoDemand = 40;             // racks the city's companies would take at that price in a steady economy
        public const int ColoChurn = 30;              // thousandths a month who go

        // ---------------------------------------------------------------- the cloud
        public const int UnitsPerRack = 40;           // compute a rack of servers sells as
        public const int GpuUnitsPerRack = 25;        // AI compute a rack of accelerators sells as
        public const int UnitDay = 350;               // a unit of compute a day at the rivals' list price, cents
        public const int GpuUnitDay = 4000;           // a unit of AI compute a day
        public const int CloudDemand = 3000;          // units the city's companies buy at the list price in a steady economy
        public const int GpuDemand = 200;
        public const int OwnHostingShare = 650;       // thousandths of the rivals' list price the player's own firms pay their own cloud
        public const int LicenceFee = 5000000;        // a cloud services licence a year, cents
        public const int CloudRacksLeast = 20;        // racks across the data centres a cloud needs to open
        public const int CloudTierLeast = 1;          // ...at tier II or better (0-based)
        public const int CloudEngineersLeast = 2;     // ...and site reliability or DevOps people
        public const long CloudCashLeast = 50000000;  // ...and cash
        public const int PriceWarShare = 300;         // thousandths of the city's compute the cloud holds before the rivals cut their prices
        public const int PriceCut = 50;               // thousandths a month the rivals cut by
        public const int PriceFloor = 600;            // ...down to this share of their list price

        // ---------------------------------------------------------------- the hyperscaler
        public const int HyperRegions = 3;            // districts with a data centre of the cloud's
        public const int HyperRacks = 300;
        public const long HyperCash = 500000000;
        public const int HyperDemand = 3000;          // thousandths: a hyperscaler sells to the city's large companies too

        // ---------------------------------------------------------------- AI models
        public const int TrainBase = 400;             // AI compute unit-days the first generation of a model takes
        public const int TrainGrowth = 2000;          // thousandths: each generation takes twice the last
        public const int RentedGpuShare = 1300;       // thousandths of the list price the player pays a rival cloud for AI compute
        public const int InferencePerCustomer = 60;   // AI compute unit-thousandths a customer of a model uses a day
    }
}
