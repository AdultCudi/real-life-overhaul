using System;

namespace RealLifeOverhaul.Population
{
    /// <summary>
    /// How the game's own customers follow the population (POPULATION.md section 7; the owner, 1 Nov 2026: yes to the narrow patch). Each business
    /// is anchored on the first day the population sees takings at it: that day the game's own count stands exactly (nothing jumps on the first
    /// load or for a business that opens), and from then on the game's count is moved by the business's takings today against its anchor's. The
    /// game's own formula multiplies its promotion in; the population carries the promotion since the anchor as an attention input instead (so a
    /// campaign shares the pool out towards the business rather than adding to it), so the game's promotion now is taken back to the one it was
    /// anchored at. The game's own caps stay (a shop's staffed furniture, an office's workers on shift): the count is fed in through the game's own
    /// arithmetic. Game-free. COST: a few operations a business an hour.
    /// </summary>
    public static class CustomerRules
    {
        public const int FactorMost = 20000;   // thousandths: twenty times the anchored count at the most, a sane bound for the arithmetic (the staffed furniture caps a shop long before)

        /// <summary>The population's factor on a business's own count, thousandths: today's takings over its anchor's; 1000 with no anchor yet.</summary>
        public static int Factor(long takings, long anchor, int anchorDay)
        {
            if (anchorDay < 0 || anchor <= 0)
            {
                return 1000;
            }
            long f = Math.Max(0, takings) * 1000 / anchor;
            return (int)Math.Min(FactorMost, f);
        }

        /// <summary>
        /// What the game's own "initial customers" of a business are multiplied by before its formula runs: the factor, and the promotion multiplier
        /// now taken back to the anchor's where the formula uses the promotion (the casino's does not). 1 with no factor (the game's own count).
        /// </summary>
        public static float InitialScale(int factor, int anchorPromotion, int promotionNow, bool usesPromotion)
        {
            double scale = Math.Max(0, factor) / 1000.0;
            if (usesPromotion && anchorPromotion > 0 && promotionNow > 0)
            {
                scale = scale * anchorPromotion / promotionNow;
            }
            return (float)scale;
        }

        /// <summary>The game's promotion multiplier, thousandths: its base plus 0.75 times the promotion (0 to 100) over 100 (CustomerEntriesCalculator.GetPromotionMultiplier).</summary>
        public static int PromotionMultiplier(float baseMultiplier, int promotion)
        {
            return (int)Math.Round((baseMultiplier + 0.75 * Math.Max(0, Math.Min(100, promotion)) / 100.0) * 1000);
        }

        /// <summary>
        /// The attention the game's own promotion gives a business since its anchor, thousandths: its promotion multiplier now against then, so a marketing
        /// campaign started after the anchor draws people from the business's pool as it drew customers from nowhere before.
        /// </summary>
        public static int PromotionAttention(int anchorPromotion, int promotionNow)
        {
            if (anchorPromotion <= 0 || promotionNow <= 0)
            {
                return 0;
            }
            return (int)((long)promotionNow * 1000 / anchorPromotion - 1000);
        }

        /// <summary>
        /// Thousandths of attention from the mod's levers of today, which wrote the promotion as per cent of customers (business pressure's issues, the
        /// company's campaigns, brands, faults and Flare: Business/BusinessCatalogue's bounds): the same per cent, now as attention.
        /// </summary>
        public static int LeverAttention(int percent)
        {
            return percent * 10;
        }
    }
}
