using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>What a product's price at a kept shop was found to be against what the mod last wrote there.</summary>
    public enum PriceLook
    {
        Same = 0,     // as written, and as it should stand
        New = 1,      // nothing written for it yet: a product the shop has just begun to sell, or the first round
        Due = 2,      // as written, but what it should be has moved (the flagship's price, or the district)
        Moved = 3     // not what was written: somebody else has changed it since
    }

    /// <summary>What the mod last wrote at one kept shop, product by product: how a price that something else has moved is found. Kept for the session, not saved.</summary>
    public sealed class KeptBook
    {
        public readonly Dictionary<string, float> Written = new Dictionary<string, float>(24, StringComparer.Ordinal);
        public int Factor = 1000;   // a member shop: its district's adjustment against the flagship's prices, thousandths
    }

    /// <summary>
    /// Prices kept for a franchise or a brand, by rule. Game-free and whole numbers where it can be: shares are in
    /// thousandths of the game's own market price for a product (Item.DefaultMarketPrice), which is what a city chain
    /// judges a shop's prices against (<see cref="FranchiseRules.ChainBreach"/>: the mean of the shop's prices against
    /// it must lie inside the chain's range). A kept price is put inside the range product by product, so the mean is
    /// inside it whatever the shop sells.
    ///
    ///   a chain's price   what the district bears for the product (the game's own price index for the neighbourhood
    ///                     and its monopoly allowance, times what the city's economy says the district's incomes and
    ///                     spending bear), up to <see cref="BrandCatalogue.BearsOver"/> over it, where the city's economy
    ///                     takes no customers for prices; a little lower where the game's demand for it in the
    ///                     district is thin; never over the highest price the game's own pricing manager would set
    ///                     there; not under the wholesale price and a tenth where the range allows; and then inside
    ///                     the chain's range, <see cref="BrandCatalogue.ChainPriceMargin"/> in from each end, so that
    ///                     rounding to the cent never takes it out.
    ///   a member's price  the flagship's price for the product, times the district's adjustment when the brand's
    ///                     owner asks for one: what the member's district bears against the flagship's, kept within
    ///                     <see cref="BrandCatalogue.LocalBand"/> either way.
    ///
    /// COST: a few integer operations a product; nothing allocated.
    /// </summary>
    public static class KeptPrices
    {
        /// <summary>A price is the same as another when they are less than half a cent apart (the game's own prices are in cents).</summary>
        public const float Cent = 0.005f;

        /// <summary>The least a kept price is set at, thousandths of the market price: inside the chain's least.</summary>
        public static int Least(int chain)
        {
            chain = FranchiseRules.Chain(chain);
            return BrandCatalogue.ChainPriceLeast[chain] + BrandCatalogue.ChainPriceMargin;
        }

        /// <summary>The most a kept price is set at, thousandths of the market price: inside the chain's most.</summary>
        public static int Most(int chain)
        {
            chain = FranchiseRules.Chain(chain);
            return BrandCatalogue.ChainPriceMost[chain] - BrandCatalogue.ChainPriceMargin;
        }

        /// <summary>What a district bears for a product, thousandths of the market price: the game's price index for it (thousandths), its monopoly allowance where the player is the only seller, and what the city's economy says its customers bear (thousandths).</summary>
        public static int Bears(int index, int tolerance, bool monopoly)
        {
            if (index <= 0)
            {
                return 0;
            }
            long bears = (long)(index + (monopoly ? BrandCatalogue.MonopolyAllowance : 0)) * Math.Max(0, tolerance) / 1000;
            return (int)Math.Min(bears, 100000);
        }

        /// <summary>
        /// The share of the market price a chain's shop is kept at for one product, thousandths: see the class summary.
        /// <paramref name="bears"/> 0 for a district with nothing to judge by (then the middle of the range is aimed at);
        /// <paramref name="demand"/> the game's demand for it there, 0 to 100, or -1 for a product without one;
        /// <paramref name="ceiling"/> and <paramref name="cost"/> 0 for none.
        /// </summary>
        public static int ChainTarget(int chain, int bears, int demand, int ceiling, int cost)
        {
            int least = Least(chain), most = Most(chain);
            int want = bears > 0 ? (int)Math.Min((long)bears * (1000 + BrandCatalogue.BearsOver) / 1000, 100000) : (least + most) / 2;
            if (bears > 0 && demand >= 0 && demand < BrandCatalogue.DemandFull)
            {
                want -= (BrandCatalogue.DemandFull - demand) * BrandCatalogue.DemandPerPoint;
            }
            if (ceiling > 0)
            {
                want = Math.Min(want, ceiling);
            }
            if (cost > 0)
            {
                want = Math.Max(want, (int)Math.Min((long)cost * BrandCatalogue.KeepCostShare / 1000, 100000));
            }
            return BrandCatalogue.Clamp(want, least, most);
        }

        /// <summary>The district's adjustment for a member shop, thousandths of the flagship's price: 1000 unless the owner asks for one, and then what the member's district bears against the flagship's, within the band.</summary>
        public static int LocalFactor(bool local, int here, int flagship)
        {
            if (!local || here <= 0 || flagship <= 0)
            {
                return 1000;
            }
            return BrandCatalogue.Clamp((int)Math.Min((long)here * 1000 / flagship, 100000), 1000 - BrandCatalogue.LocalBand, 1000 + BrandCatalogue.LocalBand);
        }

        /// <summary>So many thousandths of a price, in whole cents.</summary>
        public static float AtShare(float basis, int thousandths)
        {
            if (basis <= 0f)
            {
                return 0f;
            }
            return (float)(Math.Round(basis * (double)thousandths / 10.0, MidpointRounding.AwayFromZero) / 100.0);
        }

        /// <summary>
        /// A chain's shop's price for a product, in whole cents, at the share given and never outside the chain's own
        /// range for it: the cent is rounded towards the inside of the range where the share's own cent would fall
        /// outside. 0 for a product without a market price.
        /// </summary>
        public static float ChainPrice(float market, int target, int chain)
        {
            if (market <= 0f)
            {
                return 0f;
            }
            chain = FranchiseRules.Chain(chain);
            double low = Math.Ceiling(market * (double)BrandCatalogue.ChainPriceLeast[chain] / 10.0 - 1e-6) / 100.0;
            double high = Math.Floor(market * (double)BrandCatalogue.ChainPriceMost[chain] / 10.0 + 1e-6) / 100.0;
            double price = AtShare(market, target);
            if (low > high)
            {
                return (float)(market * (BrandCatalogue.ChainPriceLeast[chain] + BrandCatalogue.ChainPriceMost[chain]) / 2000.0);   // (a product under a cent: no whole cent fits)
            }
            return (float)Math.Max(low, Math.Min(high, price));
        }

        /// <summary>True for a price inside a chain's range for its market price.</summary>
        public static bool Inside(float price, float market, int chain)
        {
            if (market <= 0f)
            {
                return true;
            }
            chain = FranchiseRules.Chain(chain);
            double share = price / (double)market * 1000.0;
            return share >= BrandCatalogue.ChainPriceLeast[chain] - 0.01 && share <= BrandCatalogue.ChainPriceMost[chain] + 0.01;
        }

        /// <summary>How a product's price stands: <paramref name="now"/> what the shop asks, <paramref name="want"/> what it should stand at now.</summary>
        public static PriceLook Look(KeptBook book, string item, float now, float want)
        {
            float written;
            if (book == null || item == null || !book.Written.TryGetValue(item, out written))
            {
                return PriceLook.New;
            }
            if (Math.Abs(now - written) >= Cent)
            {
                return PriceLook.Moved;
            }
            return Math.Abs(now - want) >= Cent ? PriceLook.Due : PriceLook.Same;
        }

        /// <summary>The worse of two looks: one moved price makes the shop's look a moved one.</summary>
        public static PriceLook Worse(PriceLook a, PriceLook b)
        {
            return (int)a >= (int)b ? a : b;
        }

        /// <summary>A price has been written: it is what the next look compares with.</summary>
        public static void Wrote(KeptBook book, string item, float price)
        {
            if (book != null && item != null)
            {
                book.Written[item] = price;
            }
        }
    }
}
