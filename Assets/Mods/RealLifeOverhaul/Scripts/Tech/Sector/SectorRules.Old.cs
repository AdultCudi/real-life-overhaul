using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    public static partial class SectorRules
    {
        public const int AnchorMost = 50000;         // thousandths: an older save's market is never held at more than fifty times the city's
        public const int AnchorRoom = 1100;          // thousandths of its customers a product's share is held at: churn keeps customers about a tenth under their share, so they stay where they were

        // ---------------------------------------------------------------- an older save taken in

        /// <summary>
        /// The features each market's rivals start with for an older save: the strongest rival its products faced (the old
        /// rule gave each product a rival of its own, Product.RivalFeatures), else the market's usual rival, so no product
        /// meets a rival stronger or weaker than the one it had.
        /// </summary>
        public static int[] StartFeatures(FirmsFile ff)
        {
            var features = new int[(int)SectorMarket.Count];
            for (int m = 0; m < features.Length; m++)
            {
                features[m] = FirmsCatalogue.RivalFeaturesAtStart[BuyersOf(m)];
            }
            bool[] seen = new bool[features.Length];
            for (int i = 0; ff != null && i < ff.Products.Count; i++)
            {
                Product p = ff.Products[i];
                int m = MarketOf(p);
                if (m < 0 || p.Stage >= 2)
                {
                    continue;
                }
                features[m] = seen[m] ? Math.Max(features[m], p.RivalFeatures) : Math.Max(1, p.RivalFeatures);
                seen[m] = true;
            }
            return features;
        }

        /// <summary>
        /// The anchor a market needs so a product's customers of an older save sit at its share on the first day (thousandths
        /// of the city's market, at least 1000, at most <see cref="AnchorMost"/>): the old per product markets held more than a
        /// shared one does, and the anchor then fades over <see cref="AnchorDays"/> so the customers drift to the city's own
        /// market by churn, not in a day.
        /// </summary>
        public static int AnchorFor(long have, long target)
        {
            if (have <= 0)
            {
                return 1000;
            }
            if (target <= 0)
            {
                return AnchorMost;
            }
            return (int)Math.Max(1000L, Math.Min(AnchorMost, (have * 1000 + target - 1) / target));
        }

        /// <summary>
        /// A market held up for an older save's customers: its anchor set from this day, and the rivals already settled in it
        /// scaled by the same anchor, so every product's share of it, the player's and the rivals', is what it was the day
        /// before: a rival never grows into the held up market and takes the player's customers with it.
        /// </summary>
        public static void Hold(SectorFile f, int market, int anchor, int day)
        {
            int m = Clamp(market, 0, f.Anchor.Length - 1), a = Math.Max(1000, Math.Min(AnchorMost, anchor)), was = Math.Max(1000, f.Anchor[m]);
            f.Anchor[m] = a;
            f.AnchorDay = day;
            for (int i = 0; a != was && i < f.Rivals.Count; i++)
            {
                RivalProduct r = f.Rivals[i];
                if (r.Market == m)
                {
                    r.Customers = r.Customers * a / was;
                    r.Peak = Math.Max(r.Peak, r.Customers);
                }
            }
        }

        /// <summary>
        /// The markets set up for an older save on its first day: the rivals settled at their shares, then each market held up
        /// (<see cref="Hold"/>) until every product of the player's in it sits at its share with the customers it has. A
        /// product's word of mouth is its customers over the held up market's buyers, so the anchor is found by a few rounds,
        /// each from the last. The player's products' appeal and marketing a day are given (the director has the firms' crews).
        /// COST: six rounds of a day's markets, once a save.
        /// </summary>
        public static void Anchor(SectorFile f, int day, int cycle, IList<Product> products, IList<int> appeals, IList<long> marketing)
        {
            int n = products != null ? products.Count : 0;
            var pulls = new long[n];
            for (int round = 0; round < 6; round++)
            {
                BeginDay(f, day, cycle);
                for (int i = 0; i < n; i++)
                {
                    Count(f, products[i]);
                }
                Attract(f);
                if (round == 0)
                {
                    Settle(f);
                }
                for (int i = 0; i < n; i++)
                {
                    Product p = products[i];
                    int m = MarketOf(p);
                    pulls[i] = 0;
                    if (m < 0 || !p.Live)
                    {
                        continue;
                    }
                    SectorMarketDay md = f.Markets[m];
                    pulls[i] = Attraction(appeals[i], Awareness(marketing[i], md.Buyers, Have(p), md.Who, 0));
                    Join(md, pulls[i]);
                }
                bool moved = false;
                for (int m = 0; m < f.Markets.Length; m++)
                {
                    int need = 1000;
                    for (int i = 0; i < n; i++)
                    {
                        if (pulls[i] > 0 && MarketOf(products[i]) == m)
                        {
                            need = Math.Max(need, AnchorFor(Have(products[i]) * AnchorRoom / 1000, Target(f.Markets[m], pulls[i])));
                        }
                    }
                    if (need > 1002)
                    {
                        Hold(f, m, (int)Math.Min(AnchorMost, (long)Math.Max(1000, f.Anchor[m]) * need / 1000), day);
                        moved = true;
                    }
                }
                if (!moved)
                {
                    break;
                }
            }
        }

        /// <summary>The customers a product of the player's has in its market's count: a consumer product's free users too.</summary>
        public static long Have(Product p) => p.Users + (p.Segment == (int)Segment.Consumer ? p.Free : 0);
    }
}
