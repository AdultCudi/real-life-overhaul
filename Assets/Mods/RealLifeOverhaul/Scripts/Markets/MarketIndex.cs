using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// How the city's index weighs its companies: each by its free float at its price, none over <see cref="CapShare"/>
    /// of the whole. A rival tycoon's holding counts only the shares its tycoon does not own; every other company, a
    /// player's own too, all its shares. The sectors' indices are not capped. Game-free; a pass or two over the listed
    /// companies at each step, nothing allocated once the scratch is long enough.
    /// </summary>
    public static class MarketIndex
    {
        public const double CapShare = 0.10;   // the most of the index one company may weigh

        /// <summary>The shares of a company the public can trade.</summary>
        public static double Float(Stock s)
        {
            return s.Def != null && s.Def.Holding ? s.Shares * (10000.0 - MarketCatalogue.HoldingFounderBp) / 10000.0 : s.Shares;
        }

        /// <summary>A company's free float at its price, in money.</summary>
        public static double Value(Stock s)
        {
            return Float(s) * s.Mid;
        }

        /// <summary>
        /// The index's whole: every listed company's float value held to the cap, without <paramref name="skip"/> and with
        /// <paramref name="add"/> (one joining, not in the list yet). <paramref name="capped"/> is the caller's scratch.
        /// </summary>
        public static double Total(List<Stock> stocks, Stock skip, Stock add, ref bool[] capped)
        {
            int n = stocks.Count + 1;
            if (capped == null || capped.Length < n)
            {
                capped = new bool[n * 2];
            }
            Array.Clear(capped, 0, n);
            double total = 0.0;
            for (int pass = 0; pass < 16; pass++)
            {
                double rest = 0.0;
                int k = 0;
                for (int i = 0; i < n; i++)
                {
                    Stock s = i < stocks.Count ? stocks[i] : add;
                    if (!Counts(s, skip))
                    {
                        continue;
                    }
                    if (capped[i])
                    {
                        k++;
                    }
                    else
                    {
                        rest += Value(s);
                    }
                }
                double free = 1.0 - CapShare * k;
                total = free > 0.0 ? rest / free : rest;
                bool more = false;
                for (int i = 0; i < n; i++)
                {
                    Stock s = i < stocks.Count ? stocks[i] : add;
                    if (Counts(s, skip) && !capped[i] && Value(s) > CapShare * total)
                    {
                        capped[i] = true;
                        more = true;
                    }
                }
                if (!more)
                {
                    break;
                }
            }
            return total;
        }

        /// <summary>One company's share of the index's whole, after the cap.</summary>
        public static double WeightOf(List<Stock> stocks, Stock s, ref bool[] capped)
        {
            double total = Total(stocks, null, null, ref capped);
            int at = s != null ? stocks.IndexOf(s) : -1;
            if (at < 0 || !s.Listed || total <= 0.0)
            {
                return 0.0;
            }
            return (capped[at] ? CapShare * total : Value(s)) / total;
        }

        private static bool Counts(Stock s, Stock skip)
        {
            return s != null && s != skip && s.Listed;
        }
    }
}
