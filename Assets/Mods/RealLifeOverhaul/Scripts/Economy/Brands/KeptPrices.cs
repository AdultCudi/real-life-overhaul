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

    /// <summary>What the mod last wrote at one kept shop, product by product, so that a price moved by something else is found. Not saved.</summary>
    public sealed class KeptBook
    {
        public readonly Dictionary<string, float> Written = new Dictionary<string, float>(24, StringComparer.Ordinal);
        public int Factor = 1000;   // a member shop: its district's adjustment against the flagship's prices, thousandths
    }

    /// <summary>
    /// Prices kept for a chain or a brand, by rule; game-free. Shares are thousandths of the game's market price. A chain's
    /// shop is kept inside the range product by product, so its mean is inside whatever it sells.
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

        /// <summary>What a district bears for a product, thousandths of the market price: its price index and monopoly allowance times what its customers bear.</summary>
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
        /// A chain's shop's share of the market price for one product, thousandths: a little over what the district bears,
        /// lower where demand is thin, within the pricing manager's ceiling and the wholesale price, inside the chain's
        /// range. <paramref name="bears"/> 0 aims at the middle; <paramref name="demand"/> -1, <paramref name="ceiling"/> or <paramref name="cost"/> 0 for none.
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

        /// <summary>A member shop's district adjustment, thousandths of the flagship's price: 1000 unless asked for, else within the band.</summary>
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

        /// <summary>A chain's shop's price in whole cents at the share given, rounded towards the inside of the range. 0 without a market price.</summary>
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
                return (float)(market * (BrandCatalogue.ChainPriceLeast[chain] + BrandCatalogue.ChainPriceMost[chain]) / 2000.0);   // no whole cent fits
            }
            return (float)Math.Max(low, Math.Min(high, price));
        }

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

        /// <summary>The worse of two looks: one moved price makes the shop's look moved.</summary>
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
