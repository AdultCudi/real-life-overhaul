using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Interiors;

internal static partial class Program
{
    private static void CompanionPortableVectors(string output)
    {
        var officeClients = new List<object>();
        var basketExtra = new List<object>();
        foreach (int count in new[] { 0, 1, 9, 13, 100 })
        {
            foreach (int carry in new[] { 0, 499, 999 })
            {
                foreach (int free in new[] { 0, 1, 100 })
                {
                    int after = carry;
                    int extra = InteriorRules.OfficeClients(count, free, 157, ref after);
                    officeClients.Add(CompanionSnapshot.Row("gameClients", count, "free", free, "boost", 157,
                        "carry", carry, "expected", extra, "expectedCarry", after));
                }
                foreach (bool office in new[] { false, true })
                {
                    basketExtra.Add(CompanionSnapshot.Row("products", count, "share", 157, "draw", carry,
                        "office", office, "expected", InteriorRules.BasketExtra(count, 157, carry, office)));
                }
            }
        }
        var promotions = new List<object>();
        foreach (int own in new[] { 0, 25, 100 })
        {
            foreach (int issue in new[] { -100, -12, 0, 10, 100 })
            {
                foreach (int ambient in new[] { -100, -25, 0, 15, 100 })
                {
                    foreach (int interior in new[] { -12, 0, 24 })
                    {
                        promotions.Add(CompanionSnapshot.Row("own", own, "issue", issue, "ambient", ambient,
                            "interior", interior, "baseMultiplier", "0.25",
                            "expected", BusinessCatalogue.Written(own, issue, ambient, interior, 0.25f)));
                    }
                }
            }
        }
        var adjustments = new List<object>();
        foreach (float amount in new[] { 0f, 0.01f, 49.5f, 50f, 50.5f, 100.01f, 12345.67f })
        {
            foreach (int factor in new[] { 980, 990, 991, 999, 1000, 1001, 1009, 1010, 1020, 1137 })
            {
                adjustments.Add(CompanionSnapshot.Row("amount", CompanionValues.Decimal(amount), "factor", factor,
                    "expectedAdjustmentMoney", CityAdjust.Of(amount, factor)));
            }
        }
        var share = new List<object>();
        foreach (long cents in new long[] { 0, 1, 499, 500, 501, 1701, -501, 2000000000 })
        {
            foreach (int factor in new[] { 0, 999, 1000, 1001, 1137, 4000 })
            {
                share.Add(CompanionSnapshot.Row("cents", cents, "factor", factor, "expected", CompanyCatalogue.Share(cents, factor)));
            }
        }
        var prices = new List<object>();
        foreach (float amount in new[] { 0.000125f, 0.9995f, 1f, 1.02f, 1.021f, 1.5f, 10f })
        {
            foreach (bool monopoly in new[] { false, true })
            {
                int ratio = CityModel.MeanPriceRatio(CityModel.PriceRatioTerm(amount, 1f, 1.001f, monopoly), 1);
                int tolerance = CityModel.WithBears(1137, 29);
                int against = CityModel.Against(ratio, tolerance);
                int price = CityModel.PriceFootfall(against);
                prices.Add(CompanionSnapshot.Row("amount", CompanionValues.Decimal(amount), "market", "1", "index", "1.001",
                    "monopoly", monopoly, "tolerance", 1137, "bears", 29, "cycle", -4, "district", 1,
                    "expected", CompanionSnapshot.Row("ratio", ratio, "tolerance", tolerance, "against", against,
                    "pricePercent", price, "footfallPercent", CityModel.Footfall(-4, 1, price))));
            }
        }
        var interiors = new List<object>();
        foreach (int sqm in new[] { 0, 24, 35, 36, 500, 2000 })
        {
            foreach (int copies in new[] { 0, 1, 2, 5, 20 })
            {
                int target = InteriorRules.Target(sqm);
                var points = new int[InteriorRules.Categories];
                var capped = new int[InteriorRules.Categories];
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = InteriorRules.KindPoints((DecorCategory)i, copies);
                    capped[i] = InteriorRules.Capped((DecorCategory)i, points[i], target);
                }
                int decor = InteriorRules.DecorScore(capped, target, 7);
                int overall = InteriorRules.Overall(73, decor);
                int standard = InteriorRules.Standard(113, 20);
                int standardPart = InteriorRules.StandardFootfall(overall, standard, 1500);
                int decorPart = InteriorRules.DecorFootfall(decor, 150);
                interiors.Add(CompanionSnapshot.Row("sqm", sqm, "copiesEachCategory", copies, "kinds", 7,
                    "interior", 73, "income", 113, "gameMinimum", 20, "standardScale", 1500,
                    "decorMille", 150, "priceScale", 750, "issuePercent", -4, "phaseFrom", 10, "day", 12,
                    "expected", CompanionSnapshot.Row("target", target, "points", points, "capped", capped,
                        "decor", decor, "overall", overall, "standard", standard, "standardPart", standardPart,
                        "decorPart", decorPart, "footfall", InteriorRules.Footfall(standardPart, decorPart),
                        "giveWay", InteriorRules.GiveWay(standardPart, -4, InteriorRules.Bound(1500)),
                        "ramp", InteriorRules.Ramp(10, 12), "bears", InteriorRules.Bears(overall, standard, 750),
                        "basket", InteriorRules.BasketShare(overall, standard, 750))));
            }
        }
        string json = CompanionSnapshot.Json(CompanionSnapshot.Row("officeClients", officeClients, "basketExtra", basketExtra, "priceToleranceTerms", CityCompanion.PriceTerms(), "promotionTerms", BusinessCompanion.Terms(), "promotions", promotions, "adjustmentQuiet", MacroCatalogue.AdjustQuiet, "cityAdjust", adjustments, "interiorTerms", InteriorCompanion.Terms(new InteriorParts { StandardScale = 1500, DecorMille = 150, PriceScale = 750, Basket = true }), "companyShare", share, "priceTolerance", prices, "interiors", interiors));
        string directory = output ?? System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "../../../../../contracts/companion/v1"));
        string path = System.IO.Path.Combine(directory, "planning-vectors.json");
        if (output != null)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, json + "\n");
        }
        else
        {
            Check(File.Exists(path) && File.ReadAllText(path) == json + "\n", "companion: portable vectors match production rules");
        }
    }
}
