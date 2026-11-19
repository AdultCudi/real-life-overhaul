using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What changing who an AI model is sold to comes to, worked out before the player is asked (<see cref="SegmentRules.Preview"/>).</summary>
    public struct SegmentMove
    {
        public int From, To;               // Segment
        public int Work;                   // work points the re-launch costs
        public int GenerationCost;         // the work points of a generation, which the cost is a share of
        public int Days;                   // days the product is off sale
        public int ResumeDay;              // the day it is back on sale
        public long Customers, Lost, Kept; // paying customers now, those who leave, those who stay counted at the new segment's price
        public long MonthNow, MonthAfter;  // the monthly takings now and after, cents at the start's prices
        public int Aware;                  // thousandths of the new segment's buyers who know it when it is back on sale
        public int AwareDays;              // ...and the days that lasts
    }

    /// <summary>
    /// Changing who an AI model is sold to: a re-launch for another segment. A product is made for a segment (people, small businesses or large companies) and the segment fixes
    /// its list price, its churn and its support tickets (<see cref="FirmsRules.MonthPrice"/>), so moving it is a real change, and it costs: some of a generation's work, some days off
    /// sale, and some of its customers, who bought it as the old segment's. Every figure is taken from the tech sector's own balance, each with its reason. Game-free; checked offline.
    /// </summary>
    public static class SegmentRules
    {
        /// <summary>
        /// Thousandths of a generation's work (<see cref="FirmsRules.FeatureCost"/> of the features built) a re-launch costs: more than a launch's three weeks of polish, less than building
        /// a feature again, as a re-launch rewrites a product's price, sales and support for its buyers and not the model itself.
        /// </summary>
        public const int Work = 600;

        /// <summary>
        /// Days off sale: a fortnight, as a training run's gap and a big customer's feature take
        /// (<see cref="SectorCatalogue.TrainingEvery"/>, <see cref="SectorCatalogue.BigDivertDays"/>).
        /// </summary>
        public const int Days = 14;

        /// <summary>
        /// Thousandths of the paying customers who leave: four in ten. They bought it as the old segment's, and most of them need something else of the new one. That is far more than
        /// a safety incident (a tenth, <see cref="SectorCatalogue.SafetyIncident"/>) or an outage (<see cref="FirmsCatalogue.OutageChurn"/>) costs,
        /// as changing the buyer is a bigger thing than either.
        /// </summary>
        public const int Lost = 400;

        /// <summary>
        /// Thousandths of the new segment's buyers who know the product when it is back on sale: what a launch with an interface for developers gives
        /// (<see cref="SectorCatalogue.LaunchAwareness"/> at 1), as the model is known by name but not to these buyers.
        /// </summary>
        public const int Aware = 150;

        /// <summary>Days that awareness lasts before the product stands on its marketing and its customers: a launch's (<see cref="SectorCatalogue.LaunchDays"/>).</summary>
        public const int AwareDays = SectorCatalogue.LaunchDays;

        /// <summary>The segments a model can move to: every one but its own.</summary>
        public static bool Differs(Product p, int to) => p != null && to >= 0 && to < (int)Segment.Count && p.Segment != to;

        /// <summary>
        /// True if a product's segment can be changed now: an AI model on sale, not already moving, nor still paying off a re-launch. Anything else keeps its segment for good
        /// (a service or an app is begun for its own).
        /// </summary>
        public static bool Can(Product p, int day) => p != null && p.K == ProductKind.Model && p.Stage == 1 && day >= p.Moving && p.Owe <= 0;

        /// <summary>The product is off sale for its re-launch on a day: no customers won, none lost, no takings.</summary>
        public static bool Moving(Product p, int day) => p != null && p.Moving > 0 && day < p.Moving;

        /// <summary>The work points a re-launch costs a model: a share of its generation's.</summary>
        public static int WorkPoints(Product p) => (int)((long)FirmsRules.FeatureCost(p != null ? p.Features : 0) * Work / 1000);

        /// <summary>
        /// The customers kept after a move, counted at the new segment's price: those who stay, by what they paid, so the takings fall by the share lost and never rise
        /// (a person pays a few dollars and a company thousands, so the same count at a higher price would be a gift).
        /// </summary>
        public static long Kept(long customers, int from, int to)
        {
            long stay = Math.Max(0L, customers) * (1000 - Lost) / 1000;
            return stay * FirmsCatalogue.Price[Clamp(from)] / FirmsCatalogue.Price[Clamp(to)];
        }

        /// <summary>What a move to a segment costs and brings, for the page and its question box. The product is left as it is.</summary>
        public static SegmentMove Preview(Product p, int to, int day)
        {
            int cost = FirmsRules.FeatureCost(p.Features);
            long kept = Kept(p.Users, p.Segment, to);
            return new SegmentMove
            {
                From = p.Segment, To = Clamp(to), Work = WorkPoints(p), GenerationCost = cost, Days = Days, ResumeDay = day + Days,
                Customers = p.Users, Lost = Math.Max(0L, p.Users) - Math.Max(0L, p.Users) * (1000 - Lost) / 1000, Kept = kept,
                MonthNow = FirmsRules.MonthlyRevenue(p), MonthAfter = kept * FirmsCatalogue.Price[Clamp(to)] * FirmsCatalogue.PriceTiers[Clamp3(p.Tier)] / 1000,
                Aware = Aware, AwareDays = AwareDays
            };
        }

        /// <summary>
        /// The move done: the product is made for the new segment (its rival as a new product of that segment meets it), its customers are the kept ones, its free users
        /// (a consumer service's) are gone, it is off sale until the day, the work is owed, and the new segment's awareness starts from its share once it is back.
        /// <paramref name="sector"/> is its record under the tech markets, or null while they are off.
        /// </summary>
        public static SegmentMove Apply(Product p, SectorProduct sector, int to, int day)
        {
            SegmentMove m = Preview(p, to, day);
            p.Segment = m.To;
            p.Users = m.Kept;
            p.Free = 0;
            p.Carry = 0;
            p.Owe += m.Work;
            p.Moving = m.ResumeDay;
            p.RivalFeatures = FirmsCatalogue.RivalFeaturesAtStart[m.To];
            p.RivalTier = 1;
            if (sector != null)
            {
                sector.Boost = Math.Max(Aware, day < sector.BoostUntil ? sector.Boost : 0);
                sector.BoostUntil = Math.Max(sector.BoostUntil, m.ResumeDay + AwareDays);
                sector.Known = Math.Min(sector.Known, Aware);
            }
            return m;
        }

        /// <summary>The work a day of building gives to the re-launch before any feature: what its points pay off of what is owed. Returns the points left for the feature.</summary>
        public static int Pay(Product p, int points)
        {
            if (p.Owe <= 0 || points <= 0)
            {
                return points;
            }
            int paid = Math.Min(p.Owe, points);
            p.Owe -= paid;
            return points - paid;
        }

        private static int Clamp(int segment) => Math.Max(0, Math.Min((int)Segment.Count - 1, segment));

        private static int Clamp3(int tier) => Math.Max(0, Math.Min(FirmsCatalogue.PriceTiers.Length - 1, tier));
    }
}
