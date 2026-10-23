using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Money a buyer of a material will spend on a day, at today's prices: what a hedge is sized to.</summary>
    public struct HedgeNeed
    {
        public string Material;   // the exchange's name of it
        public int Day;           // the day it is to be bought
        public long Money;        // at today's prices
    }

    /// <summary>One future of a hedge: a material bought for a contract day, so many units, the money it stands for now.</summary>
    public struct HedgeLeg
    {
        public string Material;
        public int SettleDay;
        public long Units;
        public long Money;
    }

    /// <summary>
    /// A buyer's hedge on the raw materials exchange, game-free: each purchase to come is matched by a future bought for
    /// the last contract day on offer before it (that Friday close's price is what the merchants charge the week after),
    /// sized so the future's gain or loss is the purchase's dearer or cheaper price: units are the purchase's money at
    /// today's prices over the material's price today. A purchase before the first contract day is too near to hedge; one
    /// past the last is hedged to the last. Nothing here bends the exchange's rules: the futures are bought as any are.
    /// A few comparisons a purchase; asked when a page is written or a hedge is put on.
    /// </summary>
    public static class HedgeRules
    {
        /// <summary>The contract day a purchase on a day is hedged with, of the days on offer (nearest first); -1 for none before it.</summary>
        public static int SettleFor(int buyDay, int[] days, int count)
        {
            int best = -1;
            for (int i = 0; days != null && i < count && i < days.Length; i++)
            {
                if (days[i] < buyDay && days[i] > best)
                {
                    best = days[i];
                }
            }
            return best;
        }

        /// <summary>Units of a material that stand for an amount of money at its price now (micros a unit), rounded to the nearest.</summary>
        public static long Units(long money, long priceMicros)
        {
            if (money <= 0 || priceMicros <= 0)
            {
                return 0;
            }
            double units = (double)money * DerivCatalogue.Micro / priceMicros;
            return units >= long.MaxValue / 4 ? long.MaxValue / 4 : (long)Math.Round(units);
        }

        /// <summary>
        /// The hedge's futures, one a material and contract day, into <paramref name="into"/> (cleared), by material name then
        /// day, so a page lists them in the order they are bought. <paramref name="price"/> is a material's price now in micros, 0 for one the
        /// exchange does not price; what cannot be hedged is said in money: too near, and not priced. How many futures.
        /// </summary>
        public static int Plan(IList<HedgeNeed> needs, int[] days, int dayCount, Func<string, long> price, List<HedgeLeg> into, out long tooNear, out long unpriced)
        {
            into.Clear();
            tooNear = 0;
            unpriced = 0;
            for (int i = 0; needs != null && i < needs.Count; i++)
            {
                HedgeNeed n = needs[i];
                if (n.Money <= 0)
                {
                    continue;
                }
                long now = price != null && !string.IsNullOrEmpty(n.Material) ? price(n.Material) : 0;
                if (now <= 0)
                {
                    unpriced += n.Money;
                    continue;
                }
                int settle = SettleFor(n.Day, days, dayCount);
                if (settle < 0)
                {
                    tooNear += n.Money;
                    continue;
                }
                int at = Find(into, n.Material, settle);
                if (at < 0)
                {
                    into.Add(new HedgeLeg { Material = n.Material, SettleDay = settle });
                    at = into.Count - 1;
                }
                HedgeLeg leg = into[at];
                leg.Money += n.Money;
                leg.Units = Units(leg.Money, now);
                into[at] = leg;
            }
            Sort(into);
            for (int i = into.Count - 1; i >= 0; i--)
            {
                if (into[i].Units <= 0)
                {
                    tooNear += into[i].Money;   // under a unit's worth: nothing a future can hold
                    into.RemoveAt(i);
                }
            }
            return into.Count;
        }

        /// <summary>Each leg held to the units still free on its material (its room, less what earlier legs took); how many units were cut.</summary>
        public static long Fit(List<HedgeLeg> legs, Func<string, long> room)
        {
            long cut = 0;
            for (int i = 0; i < legs.Count; i++)
            {
                HedgeLeg leg = legs[i];
                long free = room != null ? room(leg.Material) : 0;
                for (int j = 0; j < i; j++)
                {
                    if (legs[j].Material == leg.Material)
                    {
                        free -= legs[j].Units;
                    }
                }
                free = Math.Max(0, free);
                if (leg.Units > free)
                {
                    cut += leg.Units - free;
                    leg.Money = leg.Units > 0 ? leg.Money * free / leg.Units : 0;
                    leg.Units = free;
                    legs[i] = leg;
                }
            }
            for (int i = legs.Count - 1; i >= 0; i--)
            {
                if (legs[i].Units <= 0)
                {
                    legs.RemoveAt(i);
                }
            }
            return cut;
        }

        /// <summary>What a hedge saved (over nothing) or cost (under), in money: its futures' results, their commission in them, cents in.</summary>
        public static long Saved(long cents)
        {
            return cents >= 0 ? cents / 100 : -((-cents + 99) / 100);
        }

        private static int Find(List<HedgeLeg> legs, string material, int settle)
        {
            for (int i = 0; i < legs.Count; i++)
            {
                if (legs[i].SettleDay == settle && legs[i].Material == material)
                {
                    return i;
                }
            }
            return -1;
        }

        // By name then day, an insertion sort: a hedge has a few legs.
        private static void Sort(List<HedgeLeg> legs)
        {
            for (int i = 1; i < legs.Count; i++)
            {
                HedgeLeg k = legs[i];
                int j = i - 1;
                while (j >= 0 && Before(k, legs[j]))
                {
                    legs[j + 1] = legs[j];
                    j--;
                }
                legs[j + 1] = k;
            }
        }

        private static bool Before(HedgeLeg a, HedgeLeg b)
        {
            int c = string.CompareOrdinal(a.Material, b.Material);
            return c < 0 || (c == 0 && a.SettleDay < b.SettleDay);
        }
    }
}
