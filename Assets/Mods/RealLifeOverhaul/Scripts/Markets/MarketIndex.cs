using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>
    /// How the city's index weighs its companies, as real indices do: each by its free float, the shares the public can
    /// trade, at its price; and no company over <see cref="CapShare"/> of the whole. A rival tycoon's holding
    /// (CompanyDef.Holding) counts only the shares its tycoon does not own (<see cref="MarketCatalogue.HoldingFounderBp"/>);
    /// every other company counts all its shares, as it always has (a player's own company too: its founder's holding
    /// changes with each of their deals, which the index has no rebalancing for). With no holding listed and no company
    /// over the cap, the index is exactly what it was before this rule. The sectors' indices weigh by float and are not
    /// capped (a sector of one company is all that company). Game-free; the engine calls it at each step: a pass or two
    /// over the listed companies, nothing allocated once its scratch is as long as the list.
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
        /// The index's whole: every listed company's float value, each held to the cap of the whole, but <paramref name="skip"/>
        /// (a company leaving) and with <paramref name="add"/> (one joining, not in the list yet). <paramref name="capped"/> is
        /// the caller's scratch, grown as the list needs.
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
