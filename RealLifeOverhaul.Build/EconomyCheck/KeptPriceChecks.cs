using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Brands;

// Checks for prices kept for a franchise or a brand (Scripts/Economy/Brands/KeptPrices): a chain's shop's every price
// inside the chain's range whatever the district bears, its demand, the game's own ceiling and the wholesale price, and
// so never in breach for its prices; the district's adjustment for a member shop bounded; a price changed by something
// else found and put back; the switches in the save's line.
internal static partial class Program
{
    private static void KeptPriceRules()
    {
        Check(BrandCatalogue.BearsOver == MacroCatalogue.OverBand && BrandCatalogue.MonopolyAllowance == MacroCatalogue.MonopolyAllowance,
            "kept prices aim where the city's economy takes no customers for prices, with the game's own monopoly allowance");
        for (int c = 0; c < BrandCatalogue.Chains; c++)
        {
            Check(KeptPrices.Least(c) > BrandCatalogue.ChainPriceLeast[c] && KeptPrices.Most(c) < BrandCatalogue.ChainPriceMost[c] && KeptPrices.Least(c) < KeptPrices.Most(c),
                "chain " + c + ": a kept price is aimed inside its range, in from either end");
        }
        Check(KeptPrices.Bears(1000, 1000, false) == 1000 && KeptPrices.Bears(1200, 1050, false) == 1260 && KeptPrices.Bears(1000, 1000, true) == 1300 && KeptPrices.Bears(0, 1000, true) == 0,
            "what a district bears: the game's index, what its customers bear, and the monopoly allowance");

        // Every price inside the chain's range, for any district, demand, ceiling and cost, at any market price.
        int[] bears = { 0, 1, 400, 850, 930, 1000, 1080, 1250, 1600, 5000, 99999 };
        int[] demands = { -1, 0, 20, 50, 100 };
        int[] ceilings = { 0, 1, 500, 900, 1050, 3000 };
        int[] costs = { 0, 300, 800, 950, 1200, 4000 };
        float[] markets = { 0.01f, 0.05f, 0.07f, 0.33f, 0.99f, 1f, 1.234f, 2.5f, 3.49f, 7.77f, 12.4f, 19.99f, 64.5f, 250f, 999.99f };
        int targets = 0, outside = 0, prices = 0, priceOutside = 0;
        for (int c = 0; c < BrandCatalogue.Chains; c++)
        {
            foreach (int b in bears)
            {
                foreach (int d in demands)
                {
                    foreach (int ceiling in ceilings)
                    {
                        foreach (int cost in costs)
                        {
                            int target = KeptPrices.ChainTarget(c, b, d, ceiling, cost);
                            targets++;
                            outside += target < KeptPrices.Least(c) || target > KeptPrices.Most(c) ? 1 : 0;
                            foreach (float m in markets)
                            {
                                float price = KeptPrices.ChainPrice(m, target, c);
                                prices++;
                                priceOutside += price <= 0f || !KeptPrices.Inside(price, m, c) ? 1 : 0;
                            }
                        }
                    }
                }
            }
        }
        Check(outside == 0, "a kept share is always inside the chain's range (" + targets + " tried, " + outside + " outside)");
        Check(priceOutside == 0, "a kept price in whole cents is always inside the chain's range (" + prices + " tried, " + priceOutside + " outside)");

        // A shop all of whose prices are kept is never in breach for them: the mean the chain judges is inside its range.
        for (int c = 0; c < BrandCatalogue.Chains; c++)
        {
            foreach (int b in new[] { 0, 500, 1000, 2000 })
            {
                double sum = 0;
                foreach (float m in markets)
                {
                    sum += KeptPrices.ChainPrice(m, KeptPrices.ChainTarget(c, b, 30, 0, 0), c) / (double)m;
                }
                int ratio = (int)Math.Round(sum / markets.Length * 1000.0);
                Check(!FranchiseRules.ChainBreach(c, 100, ratio), "chain " + c + ", a district that bears " + b + ": the shop's prices stand at " + ratio + " and the chain does not write about them");
            }
        }

        // The best price inside the range: what the district bears and a little over it, less for thin demand, never over the game's ceiling, not under the cost.
        Check(KeptPrices.ChainTarget(1, 1000, 50, 0, 0) == 1020 && KeptPrices.ChainTarget(1, 1000, 30, 0, 0) == 1000, "chain 1 in a district that bears the market price: 2% over it, a little less where demand is thin");
        Check(KeptPrices.ChainTarget(2, 1100, 80, 1080, 0) == 1080 && KeptPrices.ChainTarget(2, 1100, 80, 900, 0) == KeptPrices.Least(2), "never over the game's own pricing manager's ceiling, but never out of the range for it");
        Check(KeptPrices.ChainTarget(1, 900, 80, 0, 980) == 1078 && KeptPrices.ChainTarget(1, 900, 80, 0, 1500) == KeptPrices.Most(1), "not under the wholesale price and a tenth where the range allows");
        Check(KeptPrices.ChainTarget(0, 0, -1, 0, 0) == (KeptPrices.Least(0) + KeptPrices.Most(0)) / 2, "a district with nothing to judge by: the middle of the range");
        Check(KeptPrices.ChainTarget(2, 1300, 100, 0, 0) == KeptPrices.Most(2) && KeptPrices.ChainTarget(0, 700, 100, 0, 0) == KeptPrices.Least(0), "a rich district at the top of the range, a poor one at the bottom");
        Check(KeptPrices.ChainPrice(2.50f, 1020, 1) == 2.55f && KeptPrices.ChainPrice(0f, 1020, 1) == 0f, "a share in whole cents; nothing for a product without a market price");

        // The district's adjustment for a member shop: none unless asked for, and then within the band.
        Check(KeptPrices.LocalFactor(false, 1300, 900) == 1000 && KeptPrices.LocalFactor(true, 0, 900) == 1000, "no district adjustment unless the owner asks, or with nothing to judge by");
        int worst = 0;
        foreach (int here in new[] { 1, 300, 800, 990, 1000, 1020, 1300, 4000, 99999 })
        {
            foreach (int there in new[] { 1, 300, 800, 990, 1000, 1020, 1300, 4000, 99999 })
            {
                worst = Math.Max(worst, Math.Abs(KeptPrices.LocalFactor(true, here, there) - 1000));
            }
        }
        Check(worst == BrandCatalogue.LocalBand && BrandCatalogue.LocalBand <= 100, "the district's adjustment is never more than " + BrandCatalogue.LocalBand + " thousandths either way (at most " + worst + ")");
        Check(KeptPrices.LocalFactor(true, 1030, 1000) == 1030 && KeptPrices.LocalFactor(true, 980, 1000) == 980, "inside the band it follows what the district bears against the flagship's");
        Check(KeptPrices.AtShare(4.00f, 1030) == 4.12f && KeptPrices.AtShare(4.00f, 1000) == 4.00f, "a member's price: the flagship's at the district's share, in whole cents");

        // A price changed by something else is found, and put back.
        var book = new KeptBook();
        Check(KeptPrices.Look(book, "ba:itemname_coffee", 3.10f, 3.10f) == PriceLook.New, "a product with nothing written is new (the first round, or one just stocked)");
        KeptPrices.Wrote(book, "ba:itemname_coffee", 3.10f);
        Check(KeptPrices.Look(book, "ba:itemname_coffee", 3.10f, 3.10f) == PriceLook.Same && KeptPrices.Look(book, "ba:itemname_coffee", 3.104f, 3.10f) == PriceLook.Same, "as written: nothing to do (half a cent is the same price)");
        Check(KeptPrices.Look(book, "ba:itemname_coffee", 3.10f, 3.25f) == PriceLook.Due, "the flagship or the district moved: due, and not counted as somebody else's change");
        float now = 3.79f;   // another mod sets its own price
        PriceLook look = KeptPrices.Look(book, "ba:itemname_coffee", now, 3.10f);
        Check(look == PriceLook.Moved, "a price that is not what was written has been changed by something else");
        float written;
        book.Written.TryGetValue("ba:itemname_coffee", out written);
        now = written;   // the round puts back what was written
        Check(KeptPrices.Look(book, "ba:itemname_coffee", now, 3.10f) == PriceLook.Same && now == 3.10f, "put back: the next round finds it as written");
        Check(KeptPrices.Worse(PriceLook.Due, PriceLook.Moved) == PriceLook.Moved && KeptPrices.Worse(PriceLook.New, PriceLook.Same) == PriceLook.New, "one moved price makes the shop's look a moved one");

        // The switches in the save's line.
        Brand brand;
        BrandsState s = NewBrands(2, out brand);
        brand.Keep = true;
        brand.Local = true;
        s.Members[1].Told = true;
        Taken t = FranchiseRules.Take(s, "Side~4", 1, 3, 60);
        t.Keep = true;
        t.Told = true;
        Taken u = FranchiseRules.Take(s, "Side~5", 0, 3, 60);
        int version;
        BrandsState back = BrandStore.Read(BrandStore.Write(s), out version);
        Check(back != null && back.Brands[0].Keep && back.Brands[0].Local && back.Members[1].Told && !back.Members[0].Told, "a brand's keeping, its district adjustment and a shop told once are saved");
        Check(back != null && back.TakenAt("Side~4").Keep && back.TakenAt("Side~4").Told && !back.TakenAt("Side~5").Keep && !back.TakenAt("Side~5").Told, "a chain's shop's keeping is saved, and one not kept is not");
        BrandsState old = BrandStore.Read("v=1^h:5|1|9|4^b:1|Old|" + CoffeeType + "|Main~1|1|0|0|50|0|0|-1|0|0||0|0|0|0|0|0|0|0|0^m:Main~1|1|1^t:Side~4|1|3|63|0|0|0", out version);
        Check(old != null && !old.Brands[0].Keep && !old.Brands[0].Local && !old.Members[0].Told && !old.TakenAt("Side~4").Keep, "a save from before keeps nothing until the player switches it on");
    }
}
