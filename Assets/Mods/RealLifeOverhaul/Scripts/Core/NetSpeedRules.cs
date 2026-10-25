using System;

namespace RealLifeOverhaul
{
    /// <summary>
    /// What a connection's speed does to the work that runs over it: one table for the tech firms' work (Tech/Firms), an
    /// online shop's orders a day (Economy/Online), the Utilities app's plan picker and the Guide, so a figure shown is the
    /// figure used. The tiers sit where the providers' plans fall (Utilities/InternetCatalogue, BroadbandCatalogue): copper
    /// and the slowest wireless under 100 Mbps, cable and the faster wireless at 100 to 150, the usual business plan at 300
    /// (Kestrel's middle, Brightvein's and the player's own company's first), fibre and the best cable at 1000 and 2000. So
    /// every step up a provider's list lifts the work a tier, and the price of each step buys something. A lean stretches
    /// the same curve for work that leans on the line more or less (an AI lab moving training data, a consultancy mostly at
    /// its clients). Game-free; a few integer operations, nothing per frame (read on the daily or hourly step).
    /// </summary>
    public static class NetSpeedRules
    {
        public const int Unknown = -1;   // nothing to ask (utilities off, the switch off): the work runs as it did before speed counted
        public const int Grace = -2;     // a place still in the internet's grace with no working plan: as before speed counted, until it ends

        public static readonly int[] Floors = { 1, 50, 100, 300, 1000, 2000 };       // Mbps from which each tier counts
        public static readonly int[] Work = { 700, 800, 900, 1000, 1050, 1080 };     // thousandths of the usual work at each tier, at the usual lean

        public const int Light = 500;      // thousandths of the curve's pull: work done mostly away from the line (consultancy, design)
        public const int Usual = 1000;     // software, security, web agencies
        public const int Heavy = 1500;     // game studios: builds and assets sent back and forth
        public const int Heaviest = 2000;  // AI labs and online shops: training data, and every order's page and payment over the line
        public const int Shop = Heaviest;  // an online shop's day's bound (OnlineCatalogue.NetLean), named here so the plan picker reads the same lean
        public const int Least = 300, Most = 1200;   // thousandths: the bounds whatever the lean, so no line stops work or doubles it

        /// <summary>A speed's tier, 0 to the fastest; -1 for no connection or nothing known.</summary>
        public static int Tier(int mbps)
        {
            if (mbps < Floors[0])
            {
                return -1;
            }
            int tier = 0;
            while (tier + 1 < Floors.Length && mbps >= Floors[tier + 1])
            {
                tier++;
            }
            return tier;
        }

        /// <summary>
        /// Thousandths of the usual work at a speed for a lean: 1000 with nothing known or in the grace (as before speed
        /// counted), 0 with no connection, otherwise the tier's figure stretched by the lean and kept within the bounds.
        /// </summary>
        public static int Factor(int mbps, int lean)
        {
            if (mbps < 0)
            {
                return 1000;
            }
            int tier = Tier(mbps);
            if (tier < 0)
            {
                return 0;
            }
            long stretched = 1000 + (long)(Work[tier] - 1000) * Math.Max(0, lean) / 1000;
            return (int)Math.Max(Least, Math.Min(Most, stretched));
        }

        /// <summary>The least speed of the tier above a speed's (the usual tier's for no connection); 0 at the fastest or with nothing known.</summary>
        public static int NextFloor(int mbps)
        {
            if (mbps < 0)
            {
                return 0;
            }
            if (mbps < Floors[0])
            {
                return Floors[UsualTier];
            }
            int tier = Tier(mbps);
            return tier + 1 < Floors.Length ? Floors[tier + 1] : 0;
        }

        /// <summary>The tier the usual business plan falls in, where the work is 1000 at every lean.</summary>
        public static int UsualTier
        {
            get
            {
                for (int i = 0; i < Work.Length; i++)
                {
                    if (Work[i] == 1000)
                    {
                        return i;
                    }
                }
                return 0;
            }
        }
    }
}
