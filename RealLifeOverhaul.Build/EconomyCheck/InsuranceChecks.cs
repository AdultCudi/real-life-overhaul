using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using RealLifeOverhaul;
using RealLifeOverhaul.Insurance;
using RealLifeOverhaul.Ui;

// Insurance (Scripts/Insurance, run with --insurance): what a car's tier and its driver's record make of the Garage's premium (the standard tier of a clean record is the
// Garage's own), a home's premium by its size, district and record, what a claim pays and never pays (more than the loss, in a policy's first days, for damage from before the
// cover, over the year's limits), that no sequence of buying, claiming, cancelling and buying again pays out more than was lost, the no-claims discount that builds and is lost,
// the days of grace and the lapse, a tier moved up, the rival tycoons' cover, the save and old saves, the pages and every button, the sources (money only through the one call,
// nothing drawn at random, nothing per frame, every hook put back) and the texts.
internal static partial class Program
{
    private static readonly string[] InsuranceLanguages = { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" };
    private const string InsuranceAppId = "RealLifeInsurance";

    private static void Insurance()
    {
        InsurancePricingChecks();
        InsuranceHomePricingChecks();
        InsuranceClaimChecks();
        InsuranceWaitChecks();
        InsuranceLimitChecks();
        InsurancePreChecks();
        InsuranceNoProfitChecks();
        InsuranceGraceChecks();
        InsuranceHomeMonthChecks();
        InsuranceRivalChecks();
        InsuranceStoreChecks();
        InsurancePageChecks();
        InsuranceSourceChecks();
        InsuranceTextChecks();
    }

    // ---- the car's premium

    private static void InsurancePricingChecks()
    {
        Check(InsuranceRules.CarFactor(InsuranceCatalogue.StandardTier, 3, 0, 0, 0) == 1000, "insurance pricing: the standard tier with good credit, a clean licence and no crash is the Garage's own premium (1000)");
        bool same = true;
        foreach (float g in new[] { 40f, 65f, 215f, 1250f, 2500f })
        {
            same &= InsuranceRules.CarPremium(g, 1000) == g;
        }
        Check(same, "insurance pricing: at a factor of 1000 every premium of the Garage's is charged as the Garage wrote it");
        bool tiers = true, grades = true, points = true, crashes = true, bounds = true, discount = true;
        for (int grade = 0; grade <= 5; grade++)
        {
            for (int p = 0; p <= 14; p++)
            {
                for (int c = 0; c <= 7; c++)
                {
                    for (int m = 0; m <= 8; m++)
                    {
                        int third = InsuranceRules.CarFactor(0, grade, p, c, m), standard = InsuranceRules.CarFactor(1, grade, p, c, m), full = InsuranceRules.CarFactor(2, grade, p, c, m);
                        tiers &= third <= standard && standard <= full;
                        bounds &= third >= InsuranceCatalogue.FactorLeast && full <= InsuranceCatalogue.FactorMost;
                        grades &= grade == 0 || InsuranceRules.CarFactor(1, grade - 1, p, c, m) >= standard;
                        points &= p == 0 || InsuranceRules.CarFactor(1, grade, p - 1, c, m) <= standard;
                        crashes &= c == 0 || InsuranceRules.CarFactor(1, grade, p, c - 1, m) <= standard;
                        discount &= m == 0 || InsuranceRules.CarFactor(1, grade, p, c, m - 1) >= standard;
                    }
                }
            }
        }
        Check(tiers, "insurance pricing: more cover never costs less: third party, standard, full, whatever the record");
        Check(bounds, "insurance pricing: the factor stays inside its bounds over every record (" + InsuranceCatalogue.FactorLeast + " to " + InsuranceCatalogue.FactorMost + ")");
        Check(grades, "insurance pricing: a worse credit grade never makes a premium cheaper");
        Check(points, "insurance pricing: a point on the licence never makes a premium cheaper");
        Check(crashes, "insurance pricing: a crash on the record never makes a premium cheaper");
        Check(discount, "insurance pricing: a month more without a claim never makes a premium dearer");
        Check(InsuranceRules.CarFactor(1, 0, 0, 0, 0) > InsuranceRules.CarFactor(1, 5, 0, 0, 0) && InsuranceRules.CarFactor(1, 3, 6, 0, 0) == 1000 + 6 * InsuranceCatalogue.PointShare
              && InsuranceRules.CarFactor(1, 3, 0, 2, 0) == 1000 + 2 * InsuranceCatalogue.CrashShare,
            "insurance pricing: credit, points and crashes each move the premium by their own share (very poor " + InsuranceRules.CarFactor(1, 0, 0, 0, 0) + ", excellent "
            + InsuranceRules.CarFactor(1, 5, 0, 0, 0) + ", six points " + InsuranceRules.CarFactor(1, 3, 6, 0, 0) + ", two crashes " + InsuranceRules.CarFactor(1, 3, 0, 2, 0) + ")");
        Check(InsuranceRules.CarFactor(1, 3, InsuranceCatalogue.PointsMost + 5, 0, 0) == InsuranceRules.CarFactor(1, 3, InsuranceCatalogue.PointsMost, 0, 0)
              && InsuranceRules.CarFactor(1, 3, 0, InsuranceCatalogue.CrashesMost + 3, 0) == InsuranceRules.CarFactor(1, 3, 0, InsuranceCatalogue.CrashesMost, 0),
            "insurance pricing: points and crashes are counted up to their most and no further");
        Check(InsuranceRules.NoClaim(0) == 0 && InsuranceRules.NoClaim(1) == InsuranceCatalogue.NoClaimStep && InsuranceRules.NoClaim(99) == InsuranceCatalogue.NoClaimMost
              && InsuranceRules.NoClaim(-3) == 0, "insurance pricing: the no-claims discount builds a step a month to its most and is never under nothing");
        Check(float.IsNaN(InsuranceRules.CarPremium(float.NaN, 1200)) && InsuranceRules.CarPremium(0f, 1200) == 0f && InsuranceRules.CarPremium(-5f, 1200) == -5f
              && float.IsInfinity(InsuranceRules.CarPremium(float.PositiveInfinity, 1200)) && InsuranceRules.CarPremium(0.4f, 400) == 1f,
            "insurance pricing: a premium that is no number or not above nothing is left as the Garage gave it, and a real one is one at least");
        var days = new List<int>();
        for (int i = 0; i < 9; i++)
        {
            InsuranceRules.NoteCrash(days, 100 + i);
        }
        Check(days.Count == InsuranceCatalogue.CrashesMost && InsuranceRules.Crashes(days, 108) == InsuranceCatalogue.CrashesMost
              && InsuranceRules.Crashes(days, 108 + InsuranceCatalogue.CrashDays) == 0 && InsuranceRules.Crashes(days, 50) == 0,
            "insurance pricing: crashes are kept up to the most that count, count for their days and then no longer, and none counts before its day");
        var old = new List<int> { 10, 20 };
        InsuranceRules.NoteCrash(old, 10 + InsuranceCatalogue.CrashDays + 30);
        Check(old.Count == 1, "insurance pricing: a new crash lets go of the ones too old to count (" + old.Count + ")");
    }

    // ---- the home's premium

    private static void InsuranceHomePricingChecks()
    {
        bool tiers = true, size = true, district = true, claims = true, months = true, least = true;
        for (int sqm = 0; sqm <= 900; sqm += 60)
        {
            for (int income = 50; income <= 150; income += 10)
            {
                for (int c = 0; c <= 6; c++)
                {
                    int basic = InsuranceRules.HomePremium(0, sqm, income, c, 0, 1000, 28), standard = InsuranceRules.HomePremium(1, sqm, income, c, 0, 1000, 28),
                        complete = InsuranceRules.HomePremium(2, sqm, income, c, 0, 1000, 28);
                    tiers &= basic <= standard && standard <= complete;
                    least &= basic >= 1;
                    size &= sqm == 0 || InsuranceRules.HomePremium(1, sqm - 60, income, c, 0, 1000, 28) <= standard;
                    district &= income == 50 || InsuranceRules.HomePremium(1, sqm, income - 10, c, 0, 1000, 28) >= standard;
                    claims &= c == 0 || InsuranceRules.HomePremium(1, sqm, income, c - 1, 0, 1000, 28) <= standard;
                    months &= InsuranceRules.HomePremium(1, sqm, income, c, 3, 1000, 28) <= standard;
                }
            }
        }
        Check(tiers, "insurance home pricing: more cover never costs less: basic, standard, complete");
        Check(least, "insurance home pricing: a premium is one at least");
        Check(size, "insurance home pricing: a larger home never costs less to insure");
        Check(district, "insurance home pricing: a district of lower incomes never costs less to insure in");
        Check(claims, "insurance home pricing: a claim that still counts never makes the next premium cheaper");
        Check(months, "insurance home pricing: months without a claim never make a premium dearer");
        Check(InsuranceRules.HomeDistrict(100) == 1000 && InsuranceRules.HomeDistrict(0) == InsuranceCatalogue.HomeDistrictMost && InsuranceRules.HomeDistrict(400) == InsuranceCatalogue.HomeDistrictLeast,
            "insurance home pricing: the district moves a premium inside its bounds, and not at all at the middle income");
        Check(InsuranceRules.HomePremium(1, InsuranceCatalogue.HomeSqmMost, 100, 0, 0, 1000, 28) == InsuranceRules.HomePremium(1, InsuranceCatalogue.HomeSqmMost + 500, 100, 0, 0, 1000, 28),
            "insurance home pricing: floor area is counted up to its most");
        Check(InsuranceRules.HomePremium(1, 100, 100, 9, 0, 1000, 28) == InsuranceRules.HomePremium(1, 100, 100, InsuranceCatalogue.HomeLoadingMost / InsuranceCatalogue.HomeClaimLoading, 0, 1000, 28),
            "insurance home pricing: the claims' loading stops at its most");
        int month28 = InsuranceRules.HomePremium(2, 200, 100, 0, 0, 1000, 28), month5 = InsuranceRules.HomePremium(2, 200, 100, 0, 0, 1000, 5), dear = InsuranceRules.HomePremium(2, 200, 100, 0, 0, 2000, 28);
        Check(Math.Abs(month5 * 28 - month28 * 5) <= 28 * 2 && Math.Abs(dear - month28 * 2) <= 2, "insurance home pricing: a month of five days pays five twenty-eighths of the premium, and prices twice as high double it ("
            + month28 + ", " + month5 + ", " + dear + ")");
        Check(month28 == 38 + 24, "insurance home pricing: a complete policy for 200 square metres in a middle district is its base and its floor area (" + month28 + ")");
    }

    // ---- what a claim pays

    private static void InsuranceClaimChecks()
    {
        bool inside = true;
        foreach (long loss in new long[] { -5, 0, 1, 40, 99, 100, 101, 640, 3000, 250000 })
        {
            foreach (int excess in new[] { -10, 0, 50, 100, 5000 })
            {
                foreach (int share in new[] { -1, 0, 500, 800, 1000, 1700 })
                {
                    foreach (long paid in new long[] { 0, 500, 5999, 6000, 99999 })
                    {
                        long pays = InsuranceRules.Pays(loss, excess, share, paid, 6000, 1, 3, out long borne, out ClaimWhy why);
                        inside &= pays >= 0 && pays <= Math.Max(0L, loss) && borne >= 0 && borne <= Math.Max(0L, loss) && pays + borne <= Math.Max(0L, loss) && paid + pays <= Math.Max(paid, 6000)
                                  && (pays > 0) == (why == ClaimWhy.Paid);
                    }
                }
            }
        }
        Check(inside, "insurance claims: over every loss, excess, share and year, a claim pays between nothing and the loss, the holder's part and the payout together are never over the loss, "
                      + "and the year's cap is never passed");
        var p = new CarPolicy { Id = "a", Tier = 0, Year = 3 };
        long none = InsuranceRules.CarClaim(p, ClaimKind.Repair, 900, true, false, 200, 60, 1000, out long b0, out ClaimWhy w0);
        Check(none == 0 && w0 == ClaimWhy.NotCovered && p.ClaimsYear == 0, "insurance claims: third party cover pays nothing for the holder's own repair, and no claim is counted");
        long damages = InsuranceRules.CarClaim(p, ClaimKind.Liability, 900, true, false, 200, 60, 1000, out long b1, out ClaimWhy w1);
        Check(damages == 900 - InsuranceCatalogue.CarLiabilityExcess[0] && b1 == InsuranceCatalogue.CarLiabilityExcess[0] && w1 == ClaimWhy.Paid && p.ClaimsYear == 1,
            "insurance claims: third party cover pays the damages to another car over its excess (" + damages + " of 900)");
        Check(InsuranceRules.CarClaim(new CarPolicy { Tier = 0, Year = 3 }, ClaimKind.Tow, 180, true, false, 200, 60, 1000, out _, out _) == 0
              && InsuranceRules.CarClaim(new CarPolicy { Tier = 1, Year = 3 }, ClaimKind.Tow, 180, true, false, 200, 60, 1000, out _, out _) == 90
              && InsuranceRules.CarClaim(new CarPolicy { Tier = 2, Year = 3 }, ClaimKind.Tow, 180, true, false, 200, 60, 1000, out _, out _) == 180,
            "insurance claims: a tow is paid not at all, by half and in full by the three tiers");
        bool garage = true;
        foreach (float bill in new[] { 60f, 100f, 101f, 345f, 900f, 4321f })
        {
            long pays = InsuranceRules.CarClaim(new CarPolicy { Tier = 1, Year = 3 }, ClaimKind.Repair, (long)bill, true, false, 200, 60, 1000, out _, out _);
            long reported = InsuranceRules.CarClaim(new CarPolicy { Tier = 1, Year = 3 }, ClaimKind.Repair, (long)bill, true, true, 200, 60, 1000, out long borne, out _);
            garage &= Math.Abs(pays - (long)ReportedRepair.Payout(bill, 100f, 0.8f, false)) <= 1 && Math.Abs(reported - (long)ReportedRepair.Payout(bill, 100f, 0.8f, true)) <= 1 && borne == 0;
        }
        Check(garage, "insurance claims: the standard tier pays a repair as the Garage's policy always has: 80% over an excess of 100, and with a police report no excess");
        long uninsured = InsuranceRules.CarClaim(new CarPolicy { Tier = 2, Year = 3 }, ClaimKind.Repair, 900, false, false, 200, 60, 1000, out _, out ClaimWhy w2);
        Check(uninsured == 0 && w2 == ClaimWhy.NoCover && InsuranceRules.CarClaim(null, ClaimKind.Repair, 900, true, false, 200, 60, 1000, out _, out _) == 0,
            "insurance claims: a car the Garage does not hold insured today is paid nothing, whatever its tier");
        var streak = new CarPolicy { Tier = 2, Year = 3, NoClaimMonths = 5 };
        InsuranceRules.CarClaim(streak, ClaimKind.Repair, 40, true, false, 200, 60, 1000, out _, out ClaimWhy small);
        bool kept = streak.NoClaimMonths == 5 && small == ClaimWhy.UnderExcess;
        InsuranceRules.CarClaim(streak, ClaimKind.Repair, 900, true, false, 200, 60, 1000, out _, out _);
        Check(kept && streak.NoClaimMonths == 0, "insurance claims: a claim that pays ends the no-claims discount; a loss under the excess pays nothing and leaves it");
        var home = new HomePolicy { Place = "h", Tier = 0, Active = true, Year = 3 };
        long repair = InsuranceRules.HomeClaim(home, true, 200, 200, 60, 1000, out _, out ClaimWhy w3);
        Check(repair == 0 && w3 == ClaimWhy.NotCovered, "insurance claims: only the complete tier of a home's cover pays for the holder's things away from home");
        long atHome = InsuranceRules.HomeClaim(home, false, 140, 200, 60, 1000, out long hb, out _);
        Check(atHome == InsuranceCatalogue.Share(140 - 40, 700) && hb == 40 && home.ClaimDays.Count == 1 && home.ClaimsYear == 1,
            "insurance claims: a basic home policy pays 70% of a repair at home over its excess of 40 (" + atHome + " of 140), and the claim counts against the next premiums");
        var best = new HomePolicy { Place = "h", Tier = 2, Active = true, Year = 3 };
        Check(InsuranceRules.HomeClaim(best, false, 140, 200, 60, 1000, out _, out _) == 140 && InsuranceRules.HomeClaim(best, true, 180, 200, 60, 1000, out _, out _) == 144,
            "insurance claims: the complete tier pays a repair at home in full and 80% of a repair to the holder's things");
        Check(InsuranceRules.HomeClaim(new HomePolicy { Tier = 2, Active = false }, false, 140, 200, 60, 1000, out _, out ClaimWhy w4) == 0 && w4 == ClaimWhy.NoCover,
            "insurance claims: a home whose policy has ended is paid nothing");
        int[] seen = new int[3];
        bool steady = true;
        for (int day = 0; day < 600; day++)
        {
            int at = InsuranceRules.HomeAt(day, 3);
            steady &= at >= 0 && at < 3 && at == InsuranceRules.HomeAt(day, 3);
            seen[Math.Max(0, Math.Min(2, at))]++;
        }
        Check(steady && seen.All(n => n > 120) && InsuranceRules.HomeAt(77, 1) == 0 && InsuranceRules.HomeAt(77, 0) == 0,
            "insurance claims: a repair at home falls on one of the homes by the day alone, the same on every machine, each home in its turn (" + string.Join(", ", seen) + ")");
    }

    // ---- the first days

    private static void InsuranceWaitChecks()
    {
        var p = new CarPolicy { Tier = 2, Since = 100 };
        bool waits = true;
        for (int day = 100; day < 100 + InsuranceCatalogue.CarWaitDays; day++)
        {
            waits &= InsuranceRules.CarClaim(p, ClaimKind.Liability, 900, true, false, day, 60, 1000, out _, out ClaimWhy why) == 0 && why == ClaimWhy.Waiting;
        }
        long after = InsuranceRules.CarClaim(p, ClaimKind.Liability, 900, true, false, 100 + InsuranceCatalogue.CarWaitDays, 60, 1000, out _, out _);
        Check(waits && after > 0 && p.ClaimsYear == 1, "insurance first days: a new car policy pays nothing for " + InsuranceCatalogue.CarWaitDays + " days and from then on (" + after + ")");
        Check(InsuranceRules.CarClaim(new CarPolicy { Tier = 1, Since = 0 }, ClaimKind.Liability, 900, true, false, 1, 60, 1000, out _, out _) > 0,
            "insurance first days: a policy held from before the feature never waits");
        var h = new HomePolicy { Place = "h" };
        InsuranceRules.Take(h, 1, 30, 100, 5);
        bool homeWaits = true;
        for (int day = 100; day < 100 + InsuranceCatalogue.HomeWaitDays; day++)
        {
            homeWaits &= InsuranceRules.HomeClaim(h, false, 200, day, 60, 1000, out _, out ClaimWhy why) == 0 && why == ClaimWhy.Waiting;
        }
        long paid = InsuranceRules.HomeClaim(h, false, 200, 100 + InsuranceCatalogue.HomeWaitDays, 60, 1000, out _, out _);
        Check(homeWaits && paid > 0, "insurance first days: a new home policy pays nothing for " + InsuranceCatalogue.HomeWaitDays + " days and from then on (" + paid + ")");
        InsuranceRules.End(h);
        InsuranceRules.Take(h, 2, 40, 110, 5);
        Check(InsuranceRules.HomeClaim(h, false, 200, 110, 60, 1000, out _, out ClaimWhy again) == 0 && again == ClaimWhy.Waiting && h.ClaimsYear == 1 && h.PaidYear == paid && h.ClaimDays.Count == 1,
            "insurance first days: a policy ended and taken again waits anew, and keeps its year's claims and what it has paid: buying again gives no new year");
    }

    // ---- the year's limits

    private static void InsuranceLimitChecks()
    {
        for (int tier = 0; tier < InsuranceCatalogue.Tiers; tier++)
        {
            var p = new CarPolicy { Tier = tier };
            int paidClaims = 0;
            long total = 0;
            for (int i = 0; i < 12; i++)
            {
                long pays = InsuranceRules.CarClaim(p, ClaimKind.Liability, 3000, true, false, 130, 60, 1000, out _, out _);
                paidClaims += pays > 0 ? 1 : 0;
                total += pays;
            }
            Check(paidClaims <= InsuranceCatalogue.CarYearClaims[tier] && total <= InsuranceCatalogue.CarYearCap[tier] && total == p.PaidYear,
                "insurance limits: car tier " + tier + " pays " + paidClaims + " claims and " + total + " in a year, inside its " + InsuranceCatalogue.CarYearClaims[tier] + " claims and "
                + InsuranceCatalogue.CarYearCap[tier]);
            InsuranceRules.CarClaim(p, ClaimKind.Liability, 3000, true, false, 130, 60, 1000, out _, out ClaimWhy why);
            long next = InsuranceRules.CarClaim(p, ClaimKind.Liability, 3000, true, false, 130 + 60, 60, 1000, out _, out _);
            Check(why == ClaimWhy.YearLimit && next > 0 && p.ClaimsYear == 1 && p.PaidYear == next, "insurance limits: car tier " + tier + " refuses once its year is used up and pays again when the year turns");
        }
        for (int tier = 0; tier < InsuranceCatalogue.Tiers; tier++)
        {
            var h = new HomePolicy { Tier = tier, Active = true };
            int paidClaims = 0;
            long total = 0;
            for (int i = 0; i < 40; i++)
            {
                long pays = InsuranceRules.HomeClaim(h, false, 2000, 130, 60, 1000, out _, out _);
                paidClaims += pays > 0 ? 1 : 0;
                total += pays;
            }
            Check(paidClaims <= InsuranceCatalogue.HomeYearClaims[tier] && total <= InsuranceCatalogue.HomeYearCap[tier] && h.ClaimDays.Count <= 8,
                "insurance limits: home tier " + tier + " pays " + paidClaims + " claims and " + total + " in a year, inside its " + InsuranceCatalogue.HomeYearClaims[tier] + " claims and "
                + InsuranceCatalogue.HomeYearCap[tier]);
        }
        var dear = new CarPolicy { Tier = 2 };
        long atDouble = InsuranceRules.CarClaim(dear, ClaimKind.Repair, 2000, true, false, 130, 60, 2000, out long borne, out _);
        Check(borne == 100 && atDouble == InsuranceCatalogue.Share(1900, 950), "insurance limits: the excess and the caps move with the city's prices (an excess of 50 is 100 at twice the prices)");
    }

    // ---- damage from before the cover

    private static void InsurancePreChecks()
    {
        Check(InsuranceRules.CoveredDrop(400, 0, 400) == 0 && InsuranceRules.PreAfter(400, 0) == 0,
            "insurance before the cover: a car insured with 40% damage and then mended is mended at its owner's cost, and the old damage is gone");
        Check(InsuranceRules.CoveredDrop(700, 0, 400) == 300, "insurance before the cover: of a car insured at 40% damage, crashed to 70% and mended, the 30% that came after is the insurer's");
        Check(InsuranceRules.CoveredDrop(700, 500, 400) == 200 && InsuranceRules.PreAfter(400, 500) == 400, "insurance before the cover: a part repair down to 50% is all the insurer's, and the old 40% stays the owner's");
        Check(InsuranceRules.CoveredDrop(700, 200, 400) == 300 && InsuranceRules.PreAfter(400, 200) == 200, "insurance before the cover: a repair past the old damage is split, and what is left of the old damage is what remains");
        bool never = true;
        for (int before = 0; before <= 1000; before += 50)
        {
            for (int after = 0; after <= before; after += 50)
            {
                for (int pre = 0; pre <= 1000; pre += 100)
                {
                    int drop = InsuranceRules.CoveredDrop(before, after, pre);
                    never &= drop >= 0 && drop <= before - after && InsuranceRules.PreAfter(pre, after) <= pre;
                }
            }
        }
        Check(never, "insurance before the cover: the insurer's part of a repair is never more than what was mended, and the old damage only ever shrinks");
        Check(InsuranceRules.Damage(float.NaN) == 0 && InsuranceRules.Damage(-1f) == 0 && InsuranceRules.Damage(0.5f) == 500 && InsuranceRules.Damage(7f) == 1000,
            "insurance before the cover: a damage figure that is no number is none, and one over the whole car is the whole car");
        Check(InsuranceRules.Sound(0, false) && InsuranceRules.Sound(InsuranceCatalogue.SoundBelow - 1, false) && !InsuranceRules.Sound(InsuranceCatalogue.SoundBelow, false) && !InsuranceRules.Sound(0, true),
            "insurance before the cover: only a car with no damage to speak of and not broken down can be moved to a higher tier");
        Check(InsuranceRules.UpgradeCharge(60, 81, 3, 5) == 12 && InsuranceRules.UpgradeCharge(81, 60, 3, 5) == 0 && InsuranceRules.UpgradeCharge(60, 81, 0, 5) == 0
              && InsuranceRules.UpgradeCharge(60, 81, 99, 5) == 21 && InsuranceRules.UpgradeCharge(60, 81, 3, 0) == 21,
            "insurance before the cover: moving a tier up costs the difference for the days left of the month, never more than the difference, and moving down costs and returns nothing");
    }

    // ---- no way to profit

    private static uint InsuranceNext(ref uint state)
    {
        state = state * 1664525u + 1013904223u;
        return state >> 8;
    }

    private static void InsuranceNoProfitChecks()
    {
        bool carOk = true, homeOk = true;
        long carLoss = 0, carPaid = 0, homeLoss = 0, homePaid = 0, carPremiums = 0, homePremiums = 0;
        for (uint seed = 1; seed <= 6; seed++)
        {
            uint r = seed * 7919u;
            var car = new CarPolicy { Id = "c", Tier = (int)(seed % 3) };
            var home = new HomePolicy { Place = "h" };
            var crashes = new List<int>();
            bool insured = false;
            int until = 0, yearPaidAt = -1;
            long yearPaid = 0;
            for (int day = 1; day <= 360; day++)
            {
                uint roll = InsuranceNext(ref r) % 100;
                // the car: bought, renewed, cancelled and bought again at whim, the tier changed between policies
                if (!insured && roll < 30)
                {
                    car.Tier = (int)(InsuranceNext(ref r) % 3);
                    car.Since = day;
                    car.Pre = (int)(InsuranceNext(ref r) % 400);
                    insured = true;
                    until = day + 4;
                    carPremiums += (long)InsuranceRules.CarPremium(60f, InsuranceRules.CarFactor(car.Tier, 2, 3, InsuranceRules.Crashes(crashes, day), car.NoClaimMonths));
                }
                else if (insured && roll >= 96)
                {
                    insured = false;
                    car.Since = 0;
                }
                else if (insured && day > until)
                {
                    until = day + 4;
                    car.NoClaimMonths = Math.Min(car.NoClaimMonths + 1, 6);
                    carPremiums += (long)InsuranceRules.CarPremium(60f, InsuranceRules.CarFactor(car.Tier, 2, 3, InsuranceRules.Crashes(crashes, day), car.NoClaimMonths));
                }
                if (InsuranceNext(ref r) % 4 == 0)
                {
                    var kind = (ClaimKind)(InsuranceNext(ref r) % 3);
                    long loss = 50 + InsuranceNext(ref r) % 3000;
                    int year = InsuranceRules.YearOf(day, 60);
                    if (year != yearPaidAt)
                    {
                        yearPaidAt = year;
                        yearPaid = 0;
                    }
                    if (kind == ClaimKind.Liability)
                    {
                        InsuranceRules.NoteCrash(crashes, day);
                    }
                    long pays = InsuranceRules.CarClaim(car, kind, loss, insured, InsuranceNext(ref r) % 5 == 0, day, 60, 1000, out long borne, out ClaimWhy why);
                    yearPaid += pays;
                    carOk &= pays >= 0 && pays <= loss && pays + borne <= loss && (insured || pays == 0) && (!InsuranceRules.Waiting(car.Since, day, InsuranceCatalogue.CarWaitDays) || pays == 0)
                             && yearPaid <= InsuranceCatalogue.CarYearCap[2] && (pays == 0 || car.NoClaimMonths == 0);
                    carLoss += loss;
                    carPaid += pays;
                }
                // the home: taken, paid by the month, ended and taken again
                if (!home.Active && roll % 7 == 0)
                {
                    int premium = InsuranceRules.HomePremium((int)(roll % 3), 120, 90, InsuranceRules.HomeClaims(home.ClaimDays, day), home.NoClaimMonths, 1000, 5);
                    InsuranceRules.Take(home, (int)(roll % 3), premium, day, 5);
                    homePremiums += premium;
                }
                else if (home.Active && roll == 99)
                {
                    InsuranceRules.End(home);
                }
                else if (InsuranceRules.Due(home, day))
                {
                    int premium = InsuranceRules.HomePremium(home.Tier, 120, 90, InsuranceRules.HomeClaims(home.ClaimDays, day), home.NoClaimMonths, 1000, 5);
                    InsuranceRules.Paid(home, premium, day, 5);
                    homePremiums += premium;
                }
                if (InsuranceNext(ref r) % 3 == 0)
                {
                    long loss = 20 + InsuranceNext(ref r) % 400;
                    bool was = home.Active && !InsuranceRules.Waiting(home.Since, day, InsuranceCatalogue.HomeWaitDays);
                    long pays = InsuranceRules.HomeClaim(home, InsuranceNext(ref r) % 4 == 0, loss, day, 60, 1000, out long borne, out _);
                    homeOk &= pays >= 0 && pays <= loss && pays + borne <= loss && (was || pays == 0) && home.PaidYear <= InsuranceCatalogue.HomeYearCap[2]
                              && home.ClaimsYear <= InsuranceCatalogue.HomeYearClaims[2];
                    homeLoss += loss;
                    homePaid += pays;
                }
            }
        }
        Check(carOk && carPaid > 0 && carPaid < carLoss, "insurance no profit: over six lives of buying, renewing, cancelling, changing tier and claiming on a car, no claim paid more than its loss, none in a "
            + "policy's first days or with no policy, and the year's cap held (" + carPaid + " paid of " + carLoss + " lost, " + carPremiums + " in premiums)");
        Check(homeOk && homePaid > 0 && homePaid < homeLoss, "insurance no profit: the same for a home taken, ended and taken again (" + homePaid + " paid of " + homeLoss + " lost, " + homePremiums
            + " in premiums)");
        Check(carPremiums > 0 && homePremiums > 0, "insurance no profit: cover is paid for in every one of those lives");
    }

    // ---- a missed premium

    private static void InsuranceGraceChecks()
    {
        int from = 0;
        GraceStep first = InsuranceRules.Missed(ref from, 50);
        bool holds = first == GraceStep.Began && from == 50;
        for (int day = 51; day < 50 + InsuranceCatalogue.GraceDays; day++)
        {
            holds &= InsuranceRules.Missed(ref from, day) == GraceStep.Holds && from == 50;
        }
        Check(holds && InsuranceRules.GraceEnds(50) == 50 + InsuranceCatalogue.GraceDays - 1, "insurance lapse: a missed premium begins " + InsuranceCatalogue.GraceDays
            + " days of grace on which the cover holds, the last of them day " + InsuranceRules.GraceEnds(50));
        GraceStep last = InsuranceRules.Missed(ref from, 50 + InsuranceCatalogue.GraceDays);
        Check(last == GraceStep.Lapsed && from == 0, "insurance lapse: once the days of grace are over the policy lapses, and a later miss would begin them anew");
        int stale = 90;
        Check(InsuranceRules.Missed(ref stale, 40) == GraceStep.Began && stale == 40, "insurance lapse: a first morning recorded after today (a save edited or loaded back) begins the days anew, and never lapses at once");
    }

    private static void InsuranceHomeMonthChecks()
    {
        var p = new HomePolicy { Place = "h" };
        Check(!InsuranceRules.Due(p, 500), "insurance month: a home with no policy owes no premium");
        InsuranceRules.Take(p, 1, 30, 100, 5);
        Check(p.Active && p.Since == 100 && p.NextDue == 105 && !InsuranceRules.Due(p, 104) && InsuranceRules.Due(p, 105), "insurance month: a policy taken on day 100 is paid for a month and owes the next on day 105");
        InsuranceRules.Paid(p, 31, 105, 5);
        Check(p.NextDue == 110 && p.NoClaimMonths == 1 && p.Premium == 31, "insurance month: a premium paid on its day brings the next a month on and counts a month without a claim");
        InsuranceRules.Paid(p, 31, 123, 5);
        Check(p.NextDue == 125 && !InsuranceRules.Due(p, 124), "insurance month: a premium paid late, after days away, is one premium, and the next falls on the cycle's own day (" + p.NextDue + ")");
        p.GraceFrom = 126;
        InsuranceRules.Paid(p, 31, 127, 5);
        Check(p.GraceFrom == 0 && p.Active, "insurance month: a premium paid within the days of grace ends them and the cover goes on");
        for (int i = 0; i < 20; i++)
        {
            InsuranceRules.Paid(p, 31, p.NextDue, 5);
        }
        Check(InsuranceRules.NoClaim(p.NoClaimMonths) == InsuranceCatalogue.NoClaimMost, "insurance month: the no-claims discount stops at its most however many months are paid");
        InsuranceRules.End(p);
        Check(!p.Active && p.NextDue == 0 && !InsuranceRules.Due(p, 9999) && p.NoClaimMonths > 0, "insurance month: an ended policy owes nothing, and its record stays");
        Check(InsuranceRules.NextDueAfter(0, 40, 5) == 45 && InsuranceRules.NextDueAfter(105, 100, 5) == 110 && InsuranceRules.NextDueAfter(105, 105, 0) == 106,
            "insurance month: the next due day is always after today, whatever the month's length");
    }

    // ---- the rival tycoons

    private static void InsuranceRivalChecks()
    {
        Check(InsuranceRules.RivalPlan(1000, 1000) == 1 && InsuranceRules.RivalPlan(2000, 1000) == 2 && InsuranceRules.RivalPlan(4000, 1000) == 3 && InsuranceRules.RivalPlan(5, 0) == 3,
            "insurance rivals: a tycoon holds the basic plan, the standard from twice their floor and the full from four times");
        long worth = 2600, floor = 1000;
        Func<int, long> quote = plan => plan * 100L;
        Func<long, long> pay = amount =>
        {
            long paid = Math.Max(0L, Math.Min(amount, worth - floor));
            worth -= paid;
            return paid;
        };
        var c = new RivalCover();
        long firstPaid = InsuranceRules.RivalDay(c, worth, floor, 10, 60, 5, quote, pay);
        Check(firstPaid == 200 && c.Plan == 2 && c.NextDue == 15 && worth == 2400 && c.PremiumsYear == 200, "insurance rivals: a tycoon worth 2.6 floors takes the standard plan and pays its first month from their own worth");
        Check(InsuranceRules.RivalDay(c, worth, floor, 12, 60, 5, quote, pay) == 0 && worth == 2400, "insurance rivals: nothing is taken between two premiums");
        Check(InsuranceRules.RivalDay(c, worth, floor, 15, 60, 5, quote, pay) == 200 && c.NextDue == 20, "insurance rivals: the next month's premium is taken on its day");
        var resting = new RivalCover { Plan = 2, NextDue = 15 };
        Check(InsuranceRules.RivalDay(resting, -1, floor, 99, 60, 5, quote, pay) == 0 && resting.Plan == 2 && resting.Missed == 0,
            "insurance rivals: a tycoon who is not forward, or resting in a Going Public session, pays nothing and loses nothing");
        worth = 1100;
        bool held = InsuranceRules.RivalDay(c, worth, floor, 20, 60, 5, quote, pay) == 0 && c.Plan == 2 && c.Missed == 1 && worth == 1100;
        bool lapsed = InsuranceRules.RivalDay(c, worth, floor, 25, 60, 5, quote, pay) == 0 && c.Plan == 0;
        Check(held && lapsed, "insurance rivals: a tycoon who cannot pay from the worth over their floor misses the premium, and after " + InsuranceCatalogue.RivalLapseAfter + " running the plan lapses");
        worth = 9000;
        Check(InsuranceRules.RivalDay(c, worth, floor, 26, 60, 5, quote, pay) == 300 && c.Plan == 3, "insurance rivals: once they can pay again they take the plan their fortune goes with");
        Func<int, long, long, long> claim = (plan, bill, paidYear) => Math.Min(bill * (plan == 3 ? 100 : 70) / 100, Math.Max(0L, 5000 - paidYear));
        long theirs = InsuranceRules.RivalBill(c, 2000, 27, 60, claim);
        Check(theirs == 0 && c.PaidYear == 2000, "insurance rivals: a hospital bill costs an insured tycoon only what their plan leaves (" + theirs + " of 2,000 on the full plan)");
        long capped = InsuranceRules.RivalBill(c, 6000, 28, 60, claim);
        Check(capped == 3000 && c.PaidYear == 5000, "insurance rivals: the plan's own cap for the year holds for a tycoon as for the player (" + capped + " of 6,000 is theirs)");
        Check(InsuranceRules.RivalBill(new RivalCover(), 2000, 27, 60, claim) == 2000 && InsuranceRules.RivalBill(c, 2000, 27, 60, null) == 2000 && InsuranceRules.RivalBill(c, -5, 27, 60, claim) == 0,
            "insurance rivals: with no plan, or with Health off, the whole bill is theirs; a bill under nothing is none");
        Func<int, long, long, long> greedy = (plan, bill, paidYear) => bill * 3;
        Check(InsuranceRules.RivalBill(new RivalCover { Plan = 3, Year = 0 }, 2000, 27, 60, greedy) == 0, "insurance rivals: a plan never pays a tycoon more than the bill");
        long after = InsuranceRules.RivalBill(c, 1000, 27 + 60, 60, claim);
        Check(after == 0 && c.PaidYear == 1000, "insurance rivals: the year turning starts the plan's year again");
    }

    // ---- the save

    private static InsuranceState InsuranceSample()
    {
        var s = new InsuranceState { LastDay = 212 };
        s.Cars.Add(new CarPolicy { Id = "car|one;two^three", Tier = 2, Since = 200, Pre = 125, NoClaimMonths = 4, GraceFrom = 211, Year = 3, PaidYear = 640, ClaimsYear = 1, LastPremium = 71, Live = true });
        s.Cars.Add(new CarPolicy { Id = "b", Tier = 0 });
        var h = new HomePolicy { Place = "Elm%20Street~12", Name = "12 Elm Street; flat | 2 ^ top", Tier = 1, Active = true, Since = 190, NextDue = 215, GraceFrom = 0, NoClaimMonths = 2, Year = 3, PaidYear = 102, ClaimsYear = 1, Premium = 9 };
        h.ClaimDays.Add(198);
        h.ClaimDays.Add(205);
        s.Homes.Add(h);
        s.Homes.Add(new HomePolicy { Place = "Harbor%20Road~88", Name = "88 Harbor Road", Tier = 0 });
        InsuranceRules.Note(s, 198, CoverLine.Home, ClaimKind.HomeRepair, "12 Elm Street; flat | 2", 140, 20, 102);
        InsuranceRules.Note(s, 205, CoverLine.Car, ClaimKind.Repair, "Family hatchback", 900, 100, 640);
        s.CrashDays.Add(170);
        s.CrashDays.Add(205);
        s.Rivals[0].Plan = 3;
        s.Rivals[0].NextDue = 214;
        s.Rivals[0].Year = 3;
        s.Rivals[0].PaidYear = 4000;
        s.Rivals[0].PremiumsYear = 870;
        s.Rivals[2].Plan = 1;
        s.Rivals[2].Missed = 1;
        return s;
    }

    private static void InsuranceStoreChecks()
    {
        InsuranceState s = InsuranceSample();
        string line = InsuranceStore.Write(s);
        InsuranceState back = InsuranceStore.Read(line, out int version);
        Check(back != null && version == InsuranceStore.Version && InsuranceStore.Write(back) == line, "insurance save: a state written and read back writes the same line (version " + version + ")");
        Check(back != null && back.Cars.Count == 2 && back.Cars[0].Id == "car|one;two^three" && back.Cars[0].Tier == 2 && back.Cars[0].Pre == 125 && back.Cars[0].Live && back.Cars[0].GraceFrom == 211
              && back.Cars[0].PaidYear == 640, "insurance save: a car's policy comes back whole, its id unbroken by the line's own separators");
        Check(back != null && back.Homes.Count == 2 && back.Homes[0].Name == "12 Elm Street; flat | 2 ^ top" && back.Homes[0].Active && back.Homes[0].NextDue == 215 && back.Homes[0].ClaimDays.Count == 2
              && !back.Homes[1].Active, "insurance save: a home's policy comes back whole, its name and its claims' days with it");
        Check(back != null && back.Claims.Count == 2 && back.Claims[0].What == "12 Elm Street; flat | 2" && back.Claims[1].Payout == 640 && back.CrashDays.Count == 2 && back.Rivals[0].Plan == 3
              && back.Rivals[0].PaidYear == 4000 && back.Rivals[2].Missed == 1, "insurance save: the history, the crashes that count and the rivals' cover come back");
        Check(InsuranceStore.Read(null, out _) == null && InsuranceStore.Read(string.Empty, out _) == null && InsuranceStore.Read("garbage", out _) == null && InsuranceStore.Read("v=x^h:1", out _) == null,
            "insurance save: a line that is not this feature's is not read at all, so it is kept as it was (Core/SaveLines)");
        var data = new Dictionary<string, string> { { InsuranceStore.Key, "not a line" } };
        Check(InsuranceStore.Load(data, out _) == null && SaveLines.IsKept(data, InsuranceStore.Key) && data[InsuranceStore.Key] == "not a line",
            "insurance save: a line that cannot be read is reported to the save's kept lines and left in the save");
        Check(InsuranceStore.Load(new Dictionary<string, string>(), out int none) == null && none == 0 && InsuranceStore.Load(null, out _) == null, "insurance save: a save from before the feature has no line and starts with no policy");
        InsuranceState later = InsuranceStore.Read(line + "^z:9|9|9;7" + "^c:new|1|0|0|0|0|-1|0|0|0|1|77|88", out int v2);
        Check(later != null && later.Cars.Count == 3 && later.Cars[2].Id == "new" && later.Homes.Count == 2, "insurance save: a later version's new part and new fields at a record's end are passed over, the rest read");
        InsuranceState wild = InsuranceStore.Read("v=1^h:-9^c:x|99|-4|5000|88|-1|2|-50|-3|-7|1^m:p|-5|1|-3|0|0|99|2|-9|-9|-9|n|5,x,-2,7^k:5|99|99|100|500|900|w;6|0|0|100|0|0|none^x:0|-4|12^r:9|-1|-1|4|-5|-6", out _);
        Check(wild != null && wild.LastDay == 0 && wild.Cars[0].Tier == 2 && wild.Cars[0].Since == 0 && wild.Cars[0].Pre == 1000 && wild.Cars[0].NoClaimMonths == 6 && wild.Cars[0].PaidYear == 0
              && wild.Homes[0].Tier == 0 && !wild.Homes[0].Active && wild.Homes[0].ClaimDays.Count == 2 && wild.Claims.Count == 1 && wild.Claims[0].Payout == 100 && wild.Claims[0].Excess == 100
              && wild.Claims[0].Line == (int)CoverLine.Business && wild.CrashDays.Count == 1 && wild.Rivals[0].Plan == 3 && wild.Rivals[0].PaidYear == 0,
            "insurance save: every figure of a damaged line is put inside its bounds: no tier outside the three, no payout over its loss, no policy in force with no day its premium is due");
        CultureInfo was = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            string german = InsuranceStore.Write(s);
            Check(german == line && InsuranceStore.Write(InsuranceStore.Read(german, out _)) == line, "insurance save: the line is the same under a culture that writes a decimal comma");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = was;
        }
        var full = new InsuranceState();
        for (int i = 0; i < InsuranceCatalogue.ClaimsKept + 15; i++)
        {
            InsuranceRules.Note(full, i + 1, CoverLine.Car, ClaimKind.Tow, "car", 180, 0, 90);
        }
        Check(full.Claims.Count == InsuranceCatalogue.ClaimsKept && full.Claims[0].Day == 16 && InsuranceRules.PaidInYear(full, 55, 60) == 90L * InsuranceCatalogue.ClaimsKept,
            "insurance save: the history keeps its newest " + InsuranceCatalogue.ClaimsKept + " claims, and the year's payments are summed from it");
    }

    // ---- the pages

    private static void InsurancePageChecks()
    {
        Check(UiScreens.All.Any(sc => sc.Id == "insurance" && sc.Section == UiScreenSwitches.Ventures), "insurance pages: the app has its own switch in Mod settings");
        foreach (string state in UiSamples.InsuranceStates)
        {
            var stub = new UiSamples.InsuranceStub();
            VentModel m = UiSamples.Insurance(ModEnglish(), state, null, stub);
            bool off = state == "off";
            Check(off ? m.Shape == VentShape.Off : m.Shape == VentShape.Page && m.Nav.Length == InsurePages.Tabs, "insurance pages: " + state + " builds as " + (off ? "the off page" : "a page of five tabs"));
            if (off)
            {
                continue;
            }
            Check(m.Left.Length >= 2 && m.Right.Length >= 1 && m.HeroFigures.Length == 3, "insurance pages: " + state + " opens on its three figures and its blocks (" + (m.Left.Length + m.Right.Length) + ")");
            VentBlock last = m.Right[m.Right.Length - 1];
            Check(last.Kind == VentBlockKind.Note && last.Text == "Guide: Insurance", "insurance pages: " + state + " ends on the pointer to the Guide, after what is held and what it costs");
            int primary = 0;
            foreach (VentBlock b in m.Right.Concat(m.Left))
            {
                if (b.Kind != VentBlockKind.Actions)
                {
                    continue;
                }
                foreach (UiActionModel a in b.Actions)
                {
                    primary += a.Kind == UiButtonKind.Primary ? 1 : 0;
                    if (a.Disabled)
                    {
                        continue;
                    }
                    int before = stub.Count;
                    bool ran = a.Arg >= 0 && a.Arg < m.Runs.Length && m.Runs[a.Arg] != null && m.Runs[a.Arg]();
                    Check(ran && stub.Count == before + 1, "insurance pages: " + state + ": the button '" + a.Label + "' reaches its call (" + stub.Last + ")");
                }
            }
            Check(primary <= 1, "insurance pages: " + state + " has one primary action at most (" + primary + ")");
        }
        var press = new UiSamples.InsuranceStub();
        VentModel change = UiSamples.Insurance(ModEnglish(), "cars_change", null, press);
        UiActionModel[] changeActions = change.Right.First(b => b.Kind == VentBlockKind.Actions).Actions;
        change.Runs[changeActions[changeActions.Length - 1].Arg]();
        Check(press.Last == "change_car" && press.Id == UiSamples.InsuranceCar && press.A == 2 && changeActions[changeActions.Length - 1].Label.Contains("$"),
            "insurance pages: a car moved up a tier asks the director for that tier, and the button names what it costs now");
        change.Runs[changeActions[0].Arg]();
        Check(press.Last == "cancel_car" && press.Id == UiSamples.InsuranceCar, "insurance pages: an insured car offers to cancel its cover");
        VentModel unsound = UiSamples.Insurance(ModEnglish(), "cars_unsound", null, press);
        UiActionModel[] unsoundActions = unsound.Right.First(b => b.Kind == VentBlockKind.Actions).Actions;
        Check(unsoundActions[unsoundActions.Length - 1].Disabled && unsound.Right.Any(b => b.Kind == VentBlockKind.Note && b.Tone == UiRole.Warn),
            "insurance pages: a damaged car cannot be moved up a tier, and the page says why");
        VentModel buy = UiSamples.Insurance(ModEnglish(), "cars_buy", null, press);
        UiActionModel[] buyActions = buy.Right.First(b => b.Kind == VentBlockKind.Actions).Actions;
        buy.Runs[buyActions[0].Arg]();
        Check(buyActions.Length == 1 && press.Last == "buy_car" && press.A == 0, "insurance pages: an uninsured car offers only to be insured, at the tier showing");
        var tierView = new VentView();
        VentModel cars = UiSamples.Insurance(ModEnglish(), "cars", tierView, press);
        VentBlock choice = cars.Right.First(b => b.Kind == VentBlockKind.Choice);
        cars.Runs[choice.Run]();
        VentModel stepped = UiSamples.Insurance(ModEnglish(), "cars", tierView, press);
        Check(tierView.A == 2 && stepped.Right.First(b => b.Kind == VentBlockKind.Choice).Value == "Full" && choice.Value == "Standard",
            "insurance pages: pressing the tier's row shows the next tier's terms, the car's own first");
        VentModel cheap = UiSamples.Insurance(ModEnglish(), "cars", new VentView { Pick = InsurePages.CarKey + UiSamples.InsuranceCarBare }, press);
        Check(cheap.Right.Any(b => b.Kind == VentBlockKind.Row && b.Value == "Not insured"), "insurance pages: a car with no policy says so before anything else");
        Check(cars.Left.Any(b => b.Kind == VentBlockKind.Items && b.Items.Length == 3 && b.Items[2].ChipTone == UiRole.Warn) && cars.Left.Count(b => b.Kind == VentBlockKind.Row) == 3,
            "insurance pages: the cars' tab lists every car with its state, a missed premium in the warning's tone, and the driver's record with what each part does to a premium");
        VentModel health = UiSamples.Insurance(ModEnglish(), "health", new VentView { Tab = InsurePages.TabHealth, Pick = InsurePages.PlanKey + 2 }, press);
        UiActionModel[] healthActions = health.Right.First(b => b.Kind == VentBlockKind.Actions).Actions;
        health.Runs[healthActions[healthActions.Length - 1].Arg]();
        Check(press.Last == "take_health" && press.A == 3, "insurance pages: the health tab asks Health for the plan picked");
        VentModel healthOff = UiSamples.Insurance(ModEnglish(), "health_off", null, press);
        Check(healthOff.Right.All(b => b.Kind != VentBlockKind.Actions) && healthOff.Left.Any(b => b.Kind == VentBlockKind.Note), "insurance pages: with Health off the health tab offers nothing and says why");
        VentModel homeBuy = UiSamples.Insurance(ModEnglish(), "homes_buy", null, press);
        UiActionModel[] homeActions = homeBuy.Right.First(b => b.Kind == VentBlockKind.Actions).Actions;
        homeBuy.Runs[homeActions[0].Arg]();
        Check(press.Last == "buy_home" && press.Id == UiSamples.InsuranceHome && press.A == 2, "insurance pages: a home is insured at the tier showing");
        VentModel bizBuy = UiSamples.Insurance(ModEnglish(), "business_buy", null, press);
        UiActionModel[] bizActions = bizBuy.Right.First(b => b.Kind == VentBlockKind.Actions).Actions;
        bizBuy.Runs[bizActions[0].Arg]();
        Check(press.Last == "buy_business" && press.Id == UiSamples.InsuranceShop && press.A == 2 && press.B == 2, "insurance pages: business cover is bought from the insurer showing, by the Company's own call");
        VentModel refused = UiSamples.Insurance(ModEnglish(), "business_refused", null, press);
        Check(refused.Right.First(b => b.Kind == VentBlockKind.Actions).Actions[0].Disabled, "insurance pages: an insurer that will not write a cover cannot be bought from");
        VentModel openClaim = UiSamples.Insurance(ModEnglish(), "claims_open", null, press);
        UiActionModel[] claimActions = openClaim.Right.First(b => b.Kind == VentBlockKind.Actions).Actions;
        openClaim.Runs[claimActions[0].Arg]();
        Check(press.Last == "file_claim" && press.A == 7 && openClaim.Nav[InsurePages.TabClaims].Count == "1", "insurance pages: a business claim still to be put in is counted on its tab and put in from it");
        VentModel claims = UiSamples.Insurance(ModEnglish(), "claims", null, press);
        Check(claims.Left.Any(b => b.Kind == VentBlockKind.Items && b.Items.Length == 4 && b.Items[1].Amount.Contains("640")) && claims.Right.All(b => b.Kind != VentBlockKind.Actions),
            "insurance pages: the history lists every claim with what was paid, and only an open business claim has a button");
        string app = ModSource(@"Insurance\InsuranceApp.cs"), live = ModSource(@"Ui\Ventures\InsuranceLive.cs");
        bool sameCalls = true;
        foreach (string call in new[] { "BuyCar(", "ChangeCar(", "CancelCar(", "TakeHealth(", "CancelHealth(", "BuyHome(", "ChangeHome(", "CancelHome(", "BuyBusiness(", "CancelBusiness(", "FileClaim(" })
        {
            sameCalls &= app.Contains("InsuranceLive.Instance." + call) && live.Contains("public void " + call);
        }
        Check(sameCalls && live.Contains("d.BuyCar(") && live.Contains("d.ChangeCar(") && live.Contains("d.CancelCar(") && live.Contains("d.BuyHome(") && live.Contains("d.ChangeHome(") && live.Contains("d.CancelHome("),
            "insurance pages: the app in the game's frame and the kit's page press the same calls of the director");
        Check(app.Contains("Meta.Guide.Point(box, GuideTitle)"), "insurance pages: the app in the game's frame ends on the game's own pointer to the Guide");
        string screens = ModSource(@"Ui\UiScreens.Ventures.cs");
        int at = screens.IndexOf("UiScreenDef(\"insurance\"", StringComparison.Ordinal);
        int a0 = at >= 0 ? screens.IndexOf("// KEYS BEGIN", at, StringComparison.Ordinal) : -1, b0 = a0 >= 0 ? screens.IndexOf("// KEYS END", a0, StringComparison.Ordinal) : -1;
        var listed = new SortedSet<string>(StringComparer.Ordinal);
        if (b0 > a0)
        {
            foreach (Match k in Regex.Matches(screens.Substring(a0, b0 - a0), "\"(reallife_[a-z0-9_]+)\""))
            {
                listed.Add(k.Groups[1].Value);
            }
        }
        var written = new SortedSet<string>(StringComparer.Ordinal) { "reallife_ui_prompt_back", "reallife_ui_prompt_move", "reallife_ui_prompt_select" };
        foreach (Match k in Regex.Matches(ModSource(@"Ui\Ventures\InsurePages.cs"), "\"(reallife_[a-z0-9_]+)\""))
        {
            written.Add(k.Groups[1].Value);
        }
        foreach (string k in InsuranceCatalogue.CarTierKeys.Concat(InsuranceCatalogue.HomeTierKeys).Concat(InsuranceCatalogue.ClaimKeys))
        {
            written.Add(k);
        }
        Check(listed.Count > 100 && listed.SetEquals(written), "insurance pages: the screen names every label its pages write with, and no other (" + listed.Count + " listed, " + written.Count
            + " written; " + string.Join(", ", listed.Except(written).Concat(written.Except(listed)).Take(6)) + ")");
    }

    // ---- the sources

    private static void InsuranceSourceChecks()
    {
        string folder = System.IO.Path.GetDirectoryName(ModPath(@"Insurance\InsuranceDirector.cs"));
        string[] files = Directory.GetFiles(folder, "*.cs");
        Check(files.Length >= 15, "insurance sources: the feature's files are found (" + files.Length + ")");
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            string name = System.IO.Path.GetFileName(file);
            Check(!text.Contains("GameManager.ChangeMoney") && !text.Contains("Update()") && !text.Contains("FindObjectsByType"),
                "insurance sources: " + name + " books no money outside the one call and has no per-frame code or scene search");
            Check(!text.Contains("ProjectSettings") && !text.Contains("TODO") && !text.Contains("NotImplemented"), "insurance sources: " + name + " has no stub and no note of work left");
            Check(!text.Contains("Random.") && !text.Contains("new Random") && !text.Contains("DateTime.Now") && !text.Contains("Guid.NewGuid"),
                "insurance sources: " + name + " draws nothing at random and reads no clock of the machine: the same on every machine");
            Check(name == "InsuranceGame.cs" || !text.Contains("BizBooks.Book("), "insurance sources: " + name + " moves money only through InsuranceGame");
        }
        string game = ModSource(@"Insurance\InsuranceGame.cs");
        Check(Regex.Matches(game, @"BizBooks\.Book\(").Count == 1 && game.Contains("force: false") && game.Contains("if (amount > Cash"),
            "insurance sources: the one booking call takes a premium only from cash that is there, never by force");
        Check(game.Contains("GoingPublic.Mine(reg)"), "insurance sources: only the player's own homes are read, never another player's in a Going Public session");
        string links = ModSource(@"Wiring\Wiring.InsuranceLinks.cs");
        Check(links.Contains("GoingPublic.InSession ? null : TycoonAt(index)") && links.Contains("CarPhone.Runs(car)"),
            "insurance sources: the rivals rest in a Going Public session, and only the cars the Garage keeps for the player are read");
        Check(links.Contains("_rivalPayBefore = HealthBridges.RivalPay;") && links.Contains("HealthBridges.RivalPay = RivalHospital;") && links.Contains("HealthBridges.RivalPay = _rivalPayBefore;")
              && links.Contains("InsuranceBridges.Reset();") && links.Contains("CarCover.Reset();") && links.Contains("CrashConsequences.Cover = null;") && links.Contains("VehicleTow.Cover = null;")
              && links.Contains("LifeEvents.RepairCover = null;"), "insurance sources: every hook the wiring sets is taken off again, and what it wrapped of the rivals' bills is put back");
        string wiring = ModSource(@"Wiring\Wiring.cs");
        int connect = wiring.IndexOf("InsuranceLinks.Connect", StringComparison.Ordinal), healthConnect = wiring.IndexOf("HealthLinks.Connect", StringComparison.Ordinal);
        int disconnect = wiring.IndexOf("InsuranceLinks.Disconnect", StringComparison.Ordinal), healthDisconnect = wiring.IndexOf("HealthLinks.Disconnect", StringComparison.Ordinal);
        Check(connect > healthConnect && healthConnect > 0 && disconnect > 0 && disconnect < healthDisconnect, "insurance sources: its wiring goes on after Health's and comes off before it");
        string owner = ModSource(@"Transport\CarOwnership.cs"), phone = ModSource(@"Transport\CarPhone.cs"), cover = ModSource(@"Transport\CarCover.cs");
        Check(phone.Contains("return CarCover.PremiumOf(car.id, PremiumOf(car.VehicleType.price, record.Claims, GarageBridges.InsuranceOf(car.id)));") && phone.Contains("CarCover.Bought?.Invoke(car.id, car.damage, fresh, premium);")
              && cover.Contains("return premium != null ? premium(id, garage) : garage;"),
            "insurance sources: the Garage's one premium asks for the car's tier, and is the Garage's own where nothing answers");
        Check(owner.Contains("!claim(id, _carName, _price, before, after, reported, out payout, out excess)") && owner.Contains("payout = CarRules.Payout(bill, reported);")
              && owner.Contains("CarCover.Renewed?.Invoke(instance.id, premium);") && owner.Contains("else if (CarCover.Missed != null && CarCover.Missed(instance.id, name, premium))"),
            "insurance sources: a repair, a renewal and a missed premium ask Insurance, and fall back to the Garage's own terms where it does not answer");
        Check(ModSource(@"Wiring\Wiring.CalendarMoney.cs").Contains("float premium = CarPhone.PremiumOf(car, record);"), "insurance sources: the Calendar shows a car's renewal at the premium the Garage will charge");
        string crash = ModSource(@"Traffic\CrashConsequences.cs"), tow = ModSource(@"Driving\VehicleTow.cs"), runner = ModSource(@"Life\LifeEventRunner.cs"), life = ModSource(@"Life\LifeEvents.cs");
        Check(crash.Contains("Book(-(liability - paid),") && crash.Contains("return Mathf.Clamp(cover(liability), 0, liability);") && crash.Contains("notificationKey + CoveredSuffix"),
            "insurance sources: the damages to another car are taken less what the policy pays, never less than nothing, and the bill's own message says so");
        Check(tow.Contains("Charge(fee - paid, transactionKey);") && tow.Contains("return Mathf.Clamp(cover(id, fee), 0, fee);") && tow.Contains("bool insured = false)")
              && ModSource(@"Justice\PrisonStay.cs").Contains("VehicleTow.BringNear(car, near, insured: false);"),
            "insurance sources: a tow's fee is taken less what the policy pays; the police's impound and a tow after prison are nobody's insurer's");
        Check(runner.Contains("outcome.Txn == LifeEvents.RepairTxn ? LifeEvents.Covered(-amount, (lifeEvent.Needs & Need.HasHome) != 0) : 0") && runner.Contains("LifeEvents.ChangeMoney(amount + covered,")
              && life.Contains("return Mathf.Clamp(cover(bill, atHome), 0, bill);"), "insurance sources: a life event's repair bill is taken less what the home's policy pays, never less than nothing");
        string director = ModSource(@"Insurance\InsuranceDirector.cs");
        Check(director.Contains("if (!Settings.Insurance)") && director.Contains("PutAway();") && director.Contains("public bool Running => _loaded && !_broken && Settings.Insurance;")
              && director.Contains("new WaitForSecondsRealtime(1f)") && !director.Contains("void Update"), "insurance sources: with the option off nothing runs and the app leaves the phone; with it on it looks once a second and never per frame");
        string car = ModSource(@"Insurance\InsuranceDirector.Car.cs");
        Check(car.Contains("else if (!insured && p.Live && look.InsuredUntil < day - 1)") && car.Contains("p.Live = true;   // the cover holds through the days of grace"),
            "insurance sources: a policy whose month ended yesterday is the Garage's to renew or to ask about this morning, whichever of the two looks first, so its days of grace are never begun twice");
        Check(Regex.Matches(car, @"if \(!Running").Count >= 9, "insurance sources: every entry the Garage, a crash and a tow call answers as the feature off while it does not run");
        Check(ModSource(@"Core\Settings.cs").Contains("public static bool Insurance = true;") && ModSource(@"Meta\OptionsTree.cs").Contains("S(\"insurance\").For(\"insurance\").Opts(\"insurance\")")
              && ModSource(@"Meta\FeatureCities.cs").Contains("new Line(\"insurance\", CityScope.AnyCity") && ModSource(@"Meta\FeatureList.cs").Contains("Insurance.InsuranceGuide.Register();"),
            "insurance sources: the feature has its option, on by default, its place in the options tree, its line of cities and its Guide page");
        Check(ModSource(@"Rivals\RivalPage.cs").Contains("RivalBridges.CoverLine(who)") && links.Contains("RivalBridges.CoverLine = tycoon =>"), "insurance sources: a tycoon's panel says what cover they hold");
        // The app's place on the phone: each table that goes by app has its entry, and the tables that run side by side are as long as each other.
        Check(PhoneLooks.Ids.Length == PhoneLooks.Glyphs.Length && PhoneLooks.Glyph(InsuranceAppId) == UiIcon.Lock && UiIconTints.Of(UiIcon.Lock) != UiRole.Ok
              && UiIconTints.Of(UiIcon.Lock) != UiRole.Danger && UiIconTints.Of(UiIcon.Lock) != UiRole.Warn,
            "insurance sources: the phone has a glyph for every app it names, the Insurance app's among them and in no colour of good or bad news (" + PhoneLooks.Ids.Length + " names, " + PhoneLooks.Glyphs.Length + " glyphs)");
        Check(RealLifeOverhaul.GameUi.PagerRules.IndexOf(InsuranceAppId) >= 0 && RealLifeOverhaul.GameUi.PhoneLayoutRules.Defaults.Length == RealLifeOverhaul.GameUi.PagerRules.ModOrder.Length
              && RealLifeOverhaul.GameUi.PhoneLayoutRules.DefaultOf(InsuranceAppId) == RealLifeOverhaul.GameUi.PhoneLayoutRules.DefaultOf("RealLifeFinance"),
            "insurance sources: the Insurance app has its place in the mod's order of apps and starts in the money folder, as Finance does");
        string look = ModSource(@"Core\GameUi\LookRules.cs");
        int ids = look.IndexOf("AccentIds =", StringComparison.Ordinal), colours = look.IndexOf("AccentColours =", StringComparison.Ordinal), end = colours >= 0 ? look.IndexOf("};", colours, StringComparison.Ordinal) : -1;
        Check(ids >= 0 && colours > ids && end > colours && Regex.Matches(look.Substring(ids, colours - ids), "\"[A-Za-z]+\"").Count == Regex.Matches(look.Substring(colours, end - colours), "0x[0-9A-F]{6}").Count
              && look.Substring(ids, colours - ids).Contains("\"" + InsuranceAppId + "\""), "insurance sources: the app has its accent colour, and every app that has one has one each");
        Check(ModSource(@"Insurance\InsuranceDirector.App.cs").Contains("public const string AppId = \"" + InsuranceAppId + "\";") && ModSource(@"Ui\UiShellApps.cs").Contains("case \"" + InsuranceAppId + "\": return UiIcon.Lock;"),
            "insurance sources: the app's id is the one its tables name, and the kit's shell shows it with its own glyph");
    }

    // ---- the texts

    private static void InsuranceTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        string folder = System.IO.Path.GetDirectoryName(ModPath(@"Insurance\InsuranceDirector.cs"));
        var sources = new List<string>(Directory.GetFiles(folder, "*.cs")) { ModPath(@"Ui\Ventures\InsurePages.cs"), ModPath(@"Ui\Ventures\InsuranceLive.cs"), ModPath(@"Wiring\Wiring.InsuranceLinks.cs") };
        foreach (string source in sources)
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(source), "\"(reallife_[a-z0-9_]+)\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        foreach (string k in new[] { "reallife_opt_sec_insurance", "reallife_opt_tip_insurance", "reallife_ui_screen_insurance", "reallife_opt_tip_ui_screen_insurance", "reallife_transaction_ins_home_label", "reallife_transaction_ins_car_label",
            "reallife_crash_liability_covered", "reallife_crash_stopped_covered", "reallife_event_car_towed_covered", "reallife_event_car_towed_shop_covered", "reallife_roadside_towed_covered" })
        {
            keys.Add(k);
        }
        keys.Remove("reallife_planner_costs");
        keys.Remove("reallife_transaction_repair");
        keys.Remove("reallife_transaction_accident_liability");
        keys.Remove("reallife_transaction_car_insurance");
        Check(keys.Count >= 150, "insurance texts: its keys are found: " + keys.Count);
        string[] notices = { "reallife_ins_note_car_bought", "reallife_ins_note_home_bought", "reallife_ins_note_changed", "reallife_ins_note_changed_paid", "reallife_ins_note_cancelled", "reallife_ins_note_no_money",
            "reallife_ins_note_car_grace", "reallife_ins_note_home_grace", "reallife_ins_note_home_lapsed", "reallife_ins_note_home_claim", "reallife_ins_note_refused_wait", "reallife_ins_note_refused_limit",
            "reallife_ins_car_unsound", "reallife_ins_test_off", "reallife_ins_test_home", "reallife_ins_test_crash", "reallife_ins_test_no_home", "reallife_ins_test_back", "reallife_ins_test_back_none",
            "reallife_crash_liability_covered", "reallife_crash_stopped_covered", "reallife_event_car_towed_covered", "reallife_event_car_towed_shop_covered", "reallife_roadside_towed_covered" };
        string[] facts = { "{ins_grace}", "{ins_car_wait}", "{ins_home_wait}", "{ins_noclaim_step}", "{ins_noclaim_most}", "{ins_point}", "{ins_crash}", "{ins_crash_days}", "{ins_home_claim}", "{ins_car_claims}",
            "{ins_home_claims}" };
        Dictionary<string, string> english = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Check(english != null, "insurance texts: en.json is read");
        if (english == null)
        {
            return;
        }
        foreach (string key in keys)
        {
            bool there = english.TryGetValue(key, out string value) && value.Length > 0;
            Check(there, "insurance texts: " + key + " is in en.json");
            if (!there)
            {
                continue;
            }
            Check(!value.Contains('!') && !value.Contains((char)0x2014) && !value.Contains((char)0x2013) && !value.Contains(" - "), "insurance texts: " + key + " has no exclamation mark or dash");
        }
        foreach (string key in notices)
        {
            Check(english.TryGetValue(key, out string text) && text.Length <= 240, "insurance texts: the notice " + key + " is 240 characters at most (" + (text != null ? text.Length : 0) + ")");
        }
        string guide = english.TryGetValue("reallife_guide_insurance", out string page) ? page : string.Empty;
        foreach (string fact in facts)
        {
            Check(guide.Contains(fact), "insurance texts: the Guide page states " + fact + " from the code");
        }
        Check(WhatsNewHas(english, "A new Insurance app gathers cover"), "insurance texts: What's new says the Insurance app has come");
        // The pages' own English is what the offline pages are laid out with: it is the text of en.json, word for word.
        string pages = ModSource(@"Ui\Ventures\InsurePages.cs");
        int differs = 0, compared = 0;
        string firstDiffers = string.Empty;
        const string literal = "\"(?:[^\"\\\\]|\\\\.)*\"";   // one string literal; a long text is several joined by a plus
        foreach (Match m in Regex.Matches(pages, "w\\.[TF]\\(\"(reallife_ins_[a-z0-9_]+)\",\\s*(" + literal + "(?:\\s*\\+\\s*" + literal + ")*)"))
        {
            string key = m.Groups[1].Value;
            string said = string.Concat(Regex.Matches(m.Groups[2].Value, literal).Select(x => x.Value.Substring(1, x.Value.Length - 2))).Replace("\\\"", "\"");
            compared++;
            if (!english.TryGetValue(key, out string value) || value != said)
            {
                differs++;
                firstDiffers = firstDiffers.Length == 0 ? key : firstDiffers;
            }
        }
        Check(compared >= 120 && differs == 0, "insurance texts: the pages' own English is en.json's, word for word (" + compared + " compared, " + differs + " differ"
            + (differs > 0 ? ", the first " + firstDiffers : string.Empty) + ")");
        // The other languages come from the translation step: where one has a key, its names in curly brackets are the English ones.
        foreach (string code in InsuranceLanguages)
        {
            if (code == "en")
            {
                continue;
            }
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            if (words == null)
            {
                continue;
            }
            int wrong = 0;
            foreach (string key in keys)
            {
                if (!words.TryGetValue(key, out string value) || !english.TryGetValue(key, out string en))
                {
                    continue;
                }
                var want = new SortedSet<string>(Regex.Matches(en, "\\{[a-z_0-9]+\\}").Select(x => x.Value), StringComparer.Ordinal);
                var have = new SortedSet<string>(Regex.Matches(value, "\\{[a-z_0-9]+\\}").Select(x => x.Value), StringComparer.Ordinal);
                wrong += want.SetEquals(have) ? 0 : 1;
            }
            Check(wrong == 0, "insurance texts: " + code + ".json keeps the names in curly brackets of every key it has (" + wrong + " wrong)");
        }
    }
}
