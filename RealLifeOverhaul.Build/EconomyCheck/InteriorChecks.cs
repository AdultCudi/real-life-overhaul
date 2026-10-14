using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Brands;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Interiors;
using RealLifeOverhaul.Tech;

// Interiors and decor that matter (Scripts/Interiors): the decor score from the placed items with diminishing returns
// for each kind, every category's cap and the reward for variety, the target by floor area, wear and dirt and what a
// refresh and a redecoration do, the district's standard, every effect's size and its cap, the parts the other two
// mods take over, the save's line, and the other features' rules that take the interior's part.
internal static partial class Program
{
    private static void Interiors()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        InteriorDecor();
        InteriorTarget();
        InteriorWear();
        InteriorStandard();
        InteriorEffects();
        InteriorStaffAndOffices();
        InteriorStepAside();
        InteriorStrengths();
        InteriorBound();
        InteriorPhaseIn();
        InteriorIncomeCase();
        InteriorBasket();
        InteriorSelling();
        InteriorOfficeClients();
        InteriorCustomerSplit();
        InteriorWork();
        InteriorSave();
        InteriorElsewhere();
        Console.WriteLine("  Interiors: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static void InteriorDecor()
    {
        Check(InteriorRules.Ids.Length == InteriorRules.Categories && Enum.GetValues(typeof(DecorCategory)).Length == InteriorRules.Categories
              && InteriorRules.ItemPoints.Length == InteriorRules.Categories && InteriorRules.CapShare.Length == InteriorRules.Categories,
            "interiors: one id, one item figure and one cap for every category");
        // A kind's copies: 10, 6, 3.6, 2.2, 1.3 tenths... and nothing past the fifth.
        Check(InteriorRules.KindPoints(DecorCategory.Art, 1) == 10 && InteriorRules.KindPoints(DecorCategory.Art, 2) == 16 && InteriorRules.KindPoints(DecorCategory.Art, 3) == 20,
            "interiors: each further copy of a kind counts 60% of the one before (" + InteriorRules.KindPoints(DecorCategory.Art, 2) + ", " + InteriorRules.KindPoints(DecorCategory.Art, 3) + ")");
        Check(InteriorRules.KindPoints(DecorCategory.Art, 5) == InteriorRules.KindPoints(DecorCategory.Art, 50), "interiors: no more than five copies of a kind count");
        Check(InteriorRules.KindPoints(DecorCategory.Art, 5) < 3 * InteriorRules.KindPoints(DecorCategory.Art, 1), "interiors: five copies of one painting count less than three first copies");
        bool rising = true;
        for (int n = 1; n < InteriorRules.RepeatMost; n++)
        {
            rising &= InteriorRules.KindPoints(DecorCategory.Greenery, n + 1) > InteriorRules.KindPoints(DecorCategory.Greenery, n);
        }
        Check(rising && InteriorRules.KindPoints(DecorCategory.Greenery, 0) == 0, "interiors: every counted copy adds something, none adds nothing");
        Check(InteriorRules.KindPoints(DecorCategory.Equipment, 1) < InteriorRules.KindPoints(DecorCategory.Furniture, 1)
              && InteriorRules.KindPoints(DecorCategory.Furniture, 1) < InteriorRules.KindPoints(DecorCategory.Art, 1),
            "interiors: equipment counts less than furniture, furniture less than decor");
        int target = InteriorRules.Target(100);
        int cap = InteriorRules.Capped(DecorCategory.Art, 100000, target);
        Check(cap == target * 300 / 1000 && InteriorRules.Capped(DecorCategory.Equipment, 100000, target) == target / 10
              && InteriorRules.Capped(DecorCategory.Furniture, 100000, target) == target * 150 / 1000,
            "interiors: art fills 30% of the target at most, furniture 15%, equipment 10%");
        Check(InteriorRules.Capped(DecorCategory.Audio, -5, target) == 0, "interiors: a category's points are never under nothing");
        // One category filled to its cap gives 30, never 100.
        var one = new int[InteriorRules.Categories];
        one[(int)DecorCategory.Art] = cap;
        Check(InteriorRules.DecorScore(one, target, 1) == 30, "interiors: one kind of thing alone scores its share only (" + InteriorRules.DecorScore(one, target, 1) + ")");
        var all = new int[InteriorRules.Categories];
        for (int i = 0; i < InteriorRules.Categories; i++)
        {
            all[i] = InteriorRules.Capped((DecorCategory)i, 100000, target);
        }
        Check(InteriorRules.DecorScore(all, target, 20) == 100, "interiors: every category full scores 100, never more");
        Check(InteriorRules.Variety(3) == 0 && InteriorRules.Variety(4) == 1 && InteriorRules.Variety(11) == 8 && InteriorRules.Variety(40) == 8,
            "interiors: variety adds a point for each kind over three, eight at most");
        Check(InteriorRules.DecorScore(new int[InteriorRules.Categories], target, 30) == 0, "interiors: nothing placed scores nothing, however many kinds are counted");
        var few = new int[InteriorRules.Categories];
        few[(int)DecorCategory.Greenery] = 20;
        Check(InteriorRules.DecorScore(few, target, 10) == InteriorRules.DecorScore(few, target, 3) + 7, "interiors: ten kinds score seven more than three");
        Check(InteriorRules.DecorScore(one, 0, 5) <= 100, "interiors: a place with no target does not divide by nothing");
    }

    private static void InteriorTarget()
    {
        Check(InteriorRules.Target(0) == 120 && InteriorRules.Target(24) == 120 && InteriorRules.Target(36) == 130, "interiors: 12 points for a small shop, one more for each 12 m2 over 24");
        Check(InteriorRules.Target(300) == (12 + 276 / 12) * 10, "interiors: 300 m2 needs " + InteriorRules.Target(300) / 10 + " points");
        Check(InteriorRules.Target(5000) == 900 && InteriorRules.Target(-10) == 120, "interiors: the target is 12 to 90 points");
        // The same decor scores less in a bigger place.
        var pts = new int[InteriorRules.Categories];
        for (int i = 0; i < 5; i++)
        {
            pts[i] = 30;
        }
        int small = InteriorRules.DecorScore(Caps(pts, InteriorRules.Target(60)), InteriorRules.Target(60), 5);
        int big = InteriorRules.DecorScore(Caps(pts, InteriorRules.Target(600)), InteriorRules.Target(600), 5);
        Check(small > big, "interiors: the same decor scores more in a small shop than in a large one (" + small + " against " + big + ")");
    }

    private static int[] Caps(int[] points, int target)
    {
        var c = new int[InteriorRules.Categories];
        for (int i = 0; i < InteriorRules.Categories; i++)
        {
            c[i] = InteriorRules.Capped((DecorCategory)i, points[i], target);
        }
        return c;
    }

    private static void InteriorWear()
    {
        Check(InteriorRules.WearToday(100, false) == 3 && InteriorRules.WearToday(0, false) == 7 && InteriorRules.WearToday(100, true) == 2,
            "interiors: wear 0.3% a day, up to 0.4% more on a dirty floor, half with the cleaning service (" + InteriorRules.WearToday(100, true) + ")");
        int w = 0;
        for (int d = 0; d < 1000; d++)
        {
            w = InteriorRules.Age(w, 100, false);
        }
        Check(w == InteriorRules.WearMost, "interiors: wear stops at its most");
        int clean = 0, dirty = 0;
        for (int d = 0; d < 100; d++)
        {
            clean = InteriorRules.Age(clean, 95, true);
            dirty = InteriorRules.Age(dirty, 20, false);
        }
        Check(clean < dirty && clean < 300 && dirty > 500, "interiors: a cleaned place wears far slower than a dirty one (" + clean + " against " + dirty + " in 100 days)");
        Check(InteriorRules.Effective(80, 0, 100) == 80 && InteriorRules.Effective(80, 1000, 100) == 64, "interiors: full wear takes 20% off a score");
        Check(InteriorRules.Effective(80, 0, 70) == 80 && InteriorRules.Effective(80, 0, 0) == 68, "interiors: dirt takes up to 15% off, nothing at cleanliness 70");
        Check(InteriorRules.Effective(100, 1000, 0) == 65 && InteriorRules.Effective(100, 1000, 0) > 0, "interiors: wear and dirt together take 35% at most");
        Check(InteriorRules.Redecorated(600, 40, 55) == 300 && InteriorRules.Redecorated(600, 40, 45) == 600 && InteriorRules.Redecorated(600, -1, 90) == 600,
            "interiors: a redecoration (the interior score up by 10) halves the wear; a small change or the first reading does not");
        Check(InteriorRules.RefreshCost(10) == 150 && InteriorRules.RefreshCost(200) == 400 && InteriorRules.RefreshCost(100000) == 5000,
            "interiors: a refresh costs $2 a square metre, $150 to $5,000 before the price level");
        Check(InteriorRules.InspectedClean(80, 600) == 75 && InteriorRules.InspectedClean(80, 599) == 80 && InteriorRules.InspectedClean(2, 900) == 0,
            "interiors: an inspector takes 5 off the cleanliness of a place worn 60% or more, never under nothing");
    }

    private static void InteriorStandard()
    {
        Check(InteriorRules.Standard(60, 0) == 15 && InteriorRules.Standard(140, 0) == 70 && InteriorRules.Standard(100, 0) == 42,
            "interiors: the standard runs 15 to 70 with the district's income (" + InteriorRules.Standard(100, 0) + " in the middle)");
        Check(InteriorRules.Standard(20, 0) == 15 && InteriorRules.Standard(300, 0) == 70, "interiors: an income outside 60 to 140 is put inside it");
        bool rising = true;
        for (int i = 61; i <= 140; i++)
        {
            rising &= InteriorRules.Standard(i, 0) >= InteriorRules.Standard(i - 1, 0);
        }
        Check(rising, "interiors: a richer district never expects less");
        Check(InteriorRules.Standard(90, 50) == 50 && InteriorRules.Standard(140, 50) == 70, "interiors: never under the game's own minimum (Midtown's 50)");
        Check(InteriorRules.Overall(80, 30) == 60 && InteriorRules.Overall(0, 100) == 40, "interiors: overall is six tenths interior, four tenths decor");
    }

    private static void InteriorEffects()
    {
        Check(InteriorRules.StandardFootfall(40, 40) == 0 && InteriorRules.StandardFootfall(37, 40) == 0 && InteriorRules.StandardFootfall(36, 40) == -1 && InteriorRules.StandardFootfall(32, 40) == -2,
            "interiors: 1% fewer customers for each 4 points short, rounded down (3 short costs nothing)");
        Check(InteriorRules.StandardFootfall(35, 60) == -6, "interiors: Midtown expects 60, a shop at 35: 6% fewer customers");
        Check(InteriorRules.StandardFootfall(0, 70) == -InteriorRules.ShortFootfallMost && InteriorRules.StandardFootfall(0, 70) == -6, "interiors: short of the standard costs 6% at most");
        Check(InteriorRules.StandardFootfall(48, 40) == 1 && InteriorRules.StandardFootfall(100, 15) == 3, "interiors: over it 1% for each 8 points, 3% at most");
        Check(InteriorRules.DecorFootfall(100, 50) == 5 && InteriorRules.DecorFootfall(0, 50) == 0 && InteriorRules.DecorFootfall(150, 50) == 5
              && InteriorRules.DecorFootfall(100, InteriorRules.DecorMille) == 10 && InteriorRules.DecorFootfall(35, InteriorRules.DecorMille) == 3,
            "interiors: decor brings up to 10% more customers by default, as Meaningful Ambience, rounded down");
        Check(InteriorRules.Footfall(3, 5) == 8 && InteriorRules.Footfall(-6, 0) == -6 && InteriorRules.Footfall(-6, 5) == -1, "interiors: the footfall parts are added");
        Check(InteriorRules.FootfallLeast == BusinessCatalogue.InteriorMostDip && InteriorRules.FootfallMost == BusinessCatalogue.InteriorMostBoost,
            "interiors: its footfall is business pressure's interior part, with the same bounds");
        Check(InteriorRules.Bears(60, 40) == 20 && InteriorRules.Bears(100, 15) == 30 && InteriorRules.Bears(40, 40) == 0 && InteriorRules.Bears(20, 40) == 0 && InteriorRules.Bears(0, 70) == 0,
            "interiors: the district bears 1 per mille a point over the standard (3% at most) and never less under it");
        Check(CityModel.WithBears(1000, 30) == 1030 && CityModel.WithBears(1000, 500) == 1100 && CityModel.WithBears(1000, 0) == 1000,
            "interiors: the interior's part goes on the district's own tolerance (the city's economy keeps it to a tenth whatever is asked)");
        Check(CityModel.Against(1030, CityModel.WithBears(1000, InteriorRules.BearsOverMost)) == 1000 && !CityModel.Over(CityModel.Against(1050, CityModel.WithBears(1000, InteriorRules.BearsOverMost))),
            "interiors: prices 3% over the district's are borne in full at a fine place");
        Check(InteriorRules.ComplaintChance(31, 40) == 0 && InteriorRules.ComplaintChance(30, 40) == 40 && InteriorRules.ComplaintChance(0, 70) == 120,
            "interiors: a complaint from 10 points short, 4 per mille a point, 12% a day at most");
        Check(InteriorRules.BrandPoints(50) == 0 && InteriorRules.BrandPoints(80) == 2 && InteriorRules.BrandPoints(100) == 3 && InteriorRules.BrandPoints(0) == -3,
            "interiors: a brand's shop counts up to 3 points either way");
        Check(InteriorRules.StandingPoints(70) == 1 && InteriorRules.StandingPoints(100) == 2 && InteriorRules.StandingPoints(0) == -2,
            "interiors: Standing's customers move up to 2 points either way");
        Check(InteriorRules.Newsworthy(90, 40, 100, true) == 1 && InteriorRules.Newsworthy(90, 40, 700, true) == 0 && InteriorRules.Newsworthy(50, 40, 850, true) == -1
              && InteriorRules.Newsworthy(10, 40, 0, true) == -1 && InteriorRules.Newsworthy(10, 40, 0, false) == 0 && InteriorRules.Newsworthy(50, 40, 0, true) == 0,
            "interiors: striking at 85 unless worn, shabby at 80% wear or 25 short of the standard (only while the standard counts)");
        Check(InteriorRules.Story(-1000, 5, 0) && !InteriorRules.Story(10, 60, 0) && InteriorRules.Story(10, 70, 99) && !InteriorRules.Story(10, 70, 100),
            "interiors: in the news once in 60 days at most, at 10% a day once due");
    }

    private static void InteriorStaffAndOffices()
    {
        Check(InteriorRules.Comfort(0, 0, 200, false) == 0 && InteriorRules.Comfort(1, 0, 200, false) == 1 && InteriorRules.Comfort(3, 0, 200, false) == 2,
            "interiors: plants give 1 point of comfort, 2 with three or more");
        Check(InteriorRules.Comfort(0, 30, 200, false) == 1 && InteriorRules.Comfort(0, 29, 200, false) == 0, "interiors: good light (half its share) gives 1");
        Check(InteriorRules.Comfort(5, 60, 200, true) == 5 && InteriorRules.Comfort(0, 0, 200, true) == 2, "interiors: a rest corner gives 2, and comfort is 5 at most");
        Check(BrandCatalogue.InteriorMost == InteriorRules.BrandMost && FameRules.InteriorMost == InteriorRules.StandingMost,
            "interiors: the brands and Standing take the interior's part with the rules' own bounds");
        Check(InteriorRules.Morale(5) == 40 && InteriorRules.Morale(9) == 40, "interiors: comfort lifts a professional's morale target by 4% at most");
        Check(InteriorRules.OfficeEsteem(59) == 0 && InteriorRules.OfficeEsteem(60) == 1 && InteriorRules.OfficeEsteem(80) == 2 && InteriorRules.OfficeEsteem(-1) == 0,
            "interiors: the best office adds 1 to the jobs board's esteem at 60, 2 at 80");
        Check(InteriorRules.OfficeBid(50) == 0 && InteriorRules.OfficeBid(75) == 10 && InteriorRules.OfficeBid(100) == 20, "interiors: an office over 50 weighs bids up to 2% better");
        Check(InteriorRules.OfficeBid(100) <= 50 && InteriorRules.Morale(InteriorRules.ComfortMost) <= 100 && InteriorRules.ComfortMost <= 10 && InteriorRules.OfficeEsteem(100) <= 3,
            "interiors: the office's and comfort's parts are inside what the company and the tech sector take");
    }

    // The parts at the options' defaults (every slider at 100%), the basket switched on or not; every part off; the
    // sliders at so many per cent each.
    private static InteriorParts InteriorDefaults(bool basket = true)
    {
        return InteriorAt(InteriorRules.StrengthDefault, basket);
    }

    private static InteriorParts InteriorAt(int percent, bool basket = true, bool interiorsMod = false, bool ambienceMod = false)
    {
        return InteriorRules.Parts(percent, percent, percent, percent, basket, true, true, interiorsMod, ambienceMod);
    }

    private static InteriorParts InteriorOff()
    {
        return InteriorRules.Parts(0, 0, 0, 0, false, false, false, false, false);
    }

    private static void InteriorStepAside()
    {
        InteriorParts mi = InteriorAt(InteriorRules.StrengthDefault, true, true, false);
        Check(mi.StandardScale == 0 && !mi.Basket && mi.DecorMille == 100 && mi.PriceScale == 1000 && mi.WearScale == 1000 && mi.Staff && mi.Standing,
            "interiors: with Meaningful Interiors on, the district standard (both ways) and the basket are left to it; the rest keeps running");
        InteriorParts ma = InteriorAt(InteriorRules.StrengthDefault, true, false, true);
        Check(ma.DecorMille == 0 && ma.StandardScale == 1000 && ma.Basket, "interiors: with Meaningful Ambience on, the customers decor brings are left to it; the rest keeps running");
        InteriorParts both = InteriorAt(InteriorRules.StrengthDefault, true, true, true);
        Check(both.StandardScale == 0 && both.DecorMille == 0 && !both.Basket && both.PriceScale == 1000 && both.WearScale == 1000,
            "interiors: with both on, only the prices over the standard and wear are this mod's");
        Check(!InteriorDefaults(false).Basket && !InteriorRules.Parts(100, 100, 0, 100, true, true, true, false, false).Basket,
            "interiors: the basket needs its own switch, and a prices' strength that is not off");
        // Each case of the two other mods at the defaults: never more lost than the bound, whichever of them is on, and
        // with both on nothing lost at all (the standard's part is theirs, and what is left only gains).
        bool[] onOff = { false, true };
        bool withinEach = true;
        foreach (bool interiorsMod in onOff)
        {
            foreach (bool ambienceMod in onOff)
            {
                InteriorParts p = InteriorAt(InteriorRules.StrengthDefault, true, interiorsMod, ambienceMod);
                InteriorShop bare = InteriorBare();
                InteriorRules.Work(bare, 1000, p);
                withinEach &= bare.Footfall >= -InteriorRules.ShortFootfallMost && bare.Bears >= 0 && bare.Basket >= 0 && InteriorRules.Takings(bare.Footfall, bare.Basket) >= -60
                              && (!interiorsMod || bare.Footfall >= 0);
            }
        }
        Check(withinEach, "interiors: a bare, worn shop in the richest district loses no more than 6% with either other mod on, off or both, and nothing at all while Meaningful Interiors is on");
    }

    private static void InteriorStrengths()
    {
        InteriorParts d = InteriorDefaults();
        Check(InteriorRules.StrengthDefault == 100 && d.StandardScale == 1000 && d.DecorMille == 100 && d.PriceScale == 1000 && d.WearScale == 1000 && d.Basket,
            "interiors: every slider's default (100%) gives the rules' own figures, decor 10% as Meaningful Ambience, the basket on");
        Check(InteriorRules.StrengthLeast == 0 && InteriorRules.StrengthMost == 200 && InteriorRules.Strength(-5) == 0 && InteriorRules.Strength(250) == 2000 && InteriorRules.Strength(73) == 730,
            "interiors: each slider runs from off to double, and a figure outside it is kept inside it");
        Check(string.Join(",", InteriorRules.StandardWas) == "0,50,100,150" && string.Join(",", InteriorRules.DecorWas) == "0,50,100,150,200"
              && string.Join(",", InteriorRules.PricesWas) == "0,50,75,100,125" && string.Join(",", InteriorRules.WearWas) == "0,50,100,150",
            "interiors: every choice of the old drop-downs has its slider figure (Gentle 50%, decor 5% 50%, Generous 125%, Fast 150%)");
        Check(InteriorRules.Was(InteriorRules.StandardWas, 2) == 100 && InteriorRules.Was(InteriorRules.DecorWas, 2) == 100 && InteriorRules.Was(InteriorRules.PricesWas, 3) == 100
              && InteriorRules.Was(InteriorRules.WearWas, 2) == 100, "interiors: each old drop-down's default comes to the slider's default");
        Check(InteriorRules.Was(InteriorRules.DecorWas, -1) == 0 && InteriorRules.Was(InteriorRules.DecorWas, 9) == 200, "interiors: an old choice outside its list is kept inside it");
        Check(InteriorRules.Scale(3, 1250) == 4 && InteriorRules.Scale(3, 500) == 2 && InteriorRules.Scale(-6, 1500) == -9 && InteriorRules.Scale(-7, 500) == -4 && InteriorRules.Scale(5, 0) == 0
              && InteriorRules.Scale(5, -1000) == 0, "interiors: a figure at a strength rounds half away from nothing, and a strength under nothing is nothing");
        Check(InteriorRules.StandardFootfall(0, 70, 2000) == -12 && InteriorRules.StandardFootfall(0, 70, 500) == -3 && InteriorRules.StandardFootfall(0, 70, 0) == 0,
            "interiors: short of the standard costs 12% at most at double, 3% at half, nothing off");
        Check(InteriorRules.StandardFootfall(100, 15, 2000) == 6 && InteriorRules.StandardFootfall(100, 15, 500) == 2 && InteriorRules.StandardFootfall(100, 15, 0) == 0,
            "interiors: over it gains 6% at double, 2% at half, nothing off");
        Check(InteriorRules.DecorFootfall(100, 200) == 20 && InteriorRules.DecorFootfall(60, 100) == 6 && InteriorRules.DecorFootfall(100, 999) == 20 && InteriorRules.DecorFootfall(100, 0) == 0
              && InteriorAt(200).DecorMille == 200 && InteriorAt(150).DecorMille == 150 && InteriorAt(0).DecorMille == 0,
            "interiors: decor brings up to 20% at double, in step with the decor score");
        Check(InteriorRules.Footfall(6, 20) == 24 && InteriorRules.Footfall(-12, 0) == -12 && InteriorRules.Footfall(-30, 0) == -12 && InteriorRules.Footfall(30, 0) == 24,
            "interiors: its footfall is -12% to +24% at the strongest");
        bool inside = true;
        for (int st = 0; st <= InteriorRules.StrengthMost; st += 25)
        {
            for (int dc = 0; dc <= InteriorRules.StrengthMost; dc += 25)
            {
                InteriorParts p = InteriorRules.Parts(st, dc, 100, 100, true, true, true, false, false);
                for (int overall = 0; overall <= 100; overall += 5)
                {
                    for (int standard = 15; standard <= 70; standard += 11)
                    {
                        int f = InteriorRules.Footfall(InteriorRules.StandardFootfall(overall, standard, p.StandardScale), InteriorRules.DecorFootfall(overall, p.DecorMille));
                        inside &= f >= InteriorRules.FootfallLeast && f <= InteriorRules.FootfallMost && f >= -InteriorRules.Bound(p.StandardScale);
                    }
                }
            }
        }
        Check(inside, "interiors: at every slider's figure its footfall stays inside its bounds, and never under the standard's bound");
        Check(InteriorRules.Bears(100, 15, 1250) == 38 && InteriorRules.Bears(0, 70, 2000) == 0 && InteriorRules.Bears(100, 15, 0) == 0 && InteriorRules.Bears(100, 15, 2000) == 60,
            "interiors: the price borne over the standard follows its strength, 6% at double; under it nothing is ever taken");
        Check(InteriorRules.ComplaintChance(0, 70, 1500) == 180 && InteriorRules.ComplaintChance(0, 70, 1000) == 120 && InteriorRules.ComplaintChance(0, 70, 990) == 0 && InteriorRules.ComplaintChance(0, 70, 0) == 0,
            "interiors: complaints follow the standard's strength, and none under the default, where the bound is smaller than a complaint");
        Check(InteriorRules.WearToday(100, false, 500) == 2 && InteriorRules.WearToday(100, false, 1500) == 5 && InteriorRules.WearToday(100, false, 0) == 0
              && InteriorRules.Age(300, 0, false, 0) == 300, "interiors: wear follows its strength: slower, faster, or none at all");
        float b = 0.25f;
        Check(BusinessCatalogue.ClampInterior(30) == 24 && BusinessCatalogue.ClampInterior(-20) == -12 && BusinessCatalogue.ClampInterior(5) == 5,
            "interiors: business pressure keeps the interior's part inside -12% to +24%");
        Check(BusinessCatalogue.PromotionFor(40, 0, 0, 10, b) == BusinessCatalogue.PromotionFor(40, 0, 10, b) && BusinessCatalogue.Written(40, 0, 0, 0, b) == 40
              && BusinessCatalogue.Written(40, 0, 7, 0, b) == BusinessCatalogue.Written(40, 0, 7, b), "interiors: its part counts as much as the same of the ambient one, and nothing changes nothing");
        Check(BusinessCatalogue.PromotionFor(30, 0, 15, 20, b) > BusinessCatalogue.PromotionFor(30, 0, 15, 0, b),
            "interiors: its part is not lost under the ambient one's bound when the rest of the mod already adds its most");
    }

    // The basket, as Meaningful Interiors has it: an order's products times a share of the way from the standard to 85.
    private static void InteriorBasket()
    {
        Check(InteriorRules.BasketMost(15) == 100 && InteriorRules.BasketMost(70) == 60 && InteriorRules.BasketMost(InteriorRules.Standard(100, 0)) == 81
              && InteriorRules.BasketMost(0) == 100 && InteriorRules.BasketMost(100) == 60, "interiors: the basket's most is 10% of an order where the standard is lowest, 6% where it is highest");
        Check(InteriorRules.BasketShare(40, 40) == 0 && InteriorRules.BasketShare(10, 40) == 0 && InteriorRules.BasketShare(50, 15) == 50 && InteriorRules.BasketShare(85, 15) == 100
              && InteriorRules.BasketShare(100, 15) == 100 && InteriorRules.BasketShare(77, 70) == 28 && InteriorRules.BasketShare(63, 42) == 39 && InteriorRules.BasketShare(95, 90) == 60,
            "interiors: the basket's share rises from nothing at the standard to the district's most at 85, and nothing under the standard");
        Check(InteriorRules.BasketShare(100, 15, 1250) == 125 && InteriorRules.BasketShare(100, 15, 750) == 75 && InteriorRules.BasketShare(100, 15, 500) == 50
              && InteriorRules.BasketShare(100, 15, 0) == 0 && InteriorDefaults().PriceScale == 1000,
            "interiors: the basket follows the prices' strength as Meaningful Interiors' own: 12.5% at most at 125%, 5% at half, nothing off");
        Check(InteriorRules.BasketExtra(3, 100, 299, false) == 1 && InteriorRules.BasketExtra(3, 100, 300, false) == 0 && InteriorRules.BasketExtra(10, 100, 999, false) == 1
              && InteriorRules.BasketExtra(25, 100, 0, false) == 3 && InteriorRules.BasketExtra(25, 100, 500, false) == 2 && InteriorRules.BasketExtra(25, 100, 499, false) == 3,
            "interiors: a customer's extra products are the whole part for certain and one more with the rest as its chance");
        Check(InteriorRules.BasketExtra(0, 100, 0, false) == 0 && InteriorRules.BasketExtra(5, 0, 0, false) == 0 && InteriorRules.BasketExtra(-2, 100, 0, false) == 0
              && InteriorRules.BasketExtra(5, InteriorRules.BasketShare(100, 15, 0), 0, false) == 0 && InteriorRules.BasketExtra(5, InteriorRules.BasketShare(30, 40), 0, false) == 0,
            "interiors: no products, no share, Off or under the standard: nothing extra");
        Check(InteriorRules.BasketExtra(25, 100, 0, true) == 1 && InteriorRules.BasketExtra(12, 100, 0, true) == 1 && InteriorRules.BasketExtra(3, 100, 999, true) == 0
              && InteriorRules.BasketExtra(40, 1000, 0, true) == InteriorRules.BasketOfficeMost, "interiors: an office's client takes one more at most");
        bool expected = true;
        int[] orders = { 1, 2, 3, 4, 6, 10 };
        int[] shares = { 0, 28, 39, 50, 81, 100, 125 };
        for (int o = 0; o < orders.Length; o++)
        {
            for (int k = 0; k < shares.Length; k++)
            {
                int sum = 0;
                for (int d = 0; d < 1000; d++)
                {
                    sum += InteriorRules.BasketExtra(orders[o], shares[k], d, false);
                }
                expected &= sum == orders[o] * shares[k];
            }
        }
        Check(expected, "interiors: over every draw a customer takes on average their order's products times the share (3 products at 8.1%: 0.243 more)");
        InteriorShop shop = InteriorSample(InteriorKind.Shop);
        InteriorRules.Work(shop, 0, InteriorDefaults());
        InteriorShop offBasket = InteriorSample(InteriorKind.Shop);
        InteriorRules.Work(offBasket, 0, InteriorDefaults(false));
        InteriorShop low = InteriorSample(InteriorKind.Shop);
        InteriorRules.Work(low, 0, InteriorRules.Parts(100, 100, 50, 100, true, true, true, false, false));
        Check(shop.Basket == InteriorRules.BasketShare(shop.Overall, shop.Standard) && shop.Basket > 0 && offBasket.Basket == 0 && low.Basket == InteriorRules.BasketShare(low.Overall, low.Standard, 500),
            "interiors: a worked shop's basket is the rules' share at the prices' strength (" + shop.Basket + " per mille), nothing with its switch off");
    }

    private static void InteriorSelling()
    {
        Check(InteriorRules.Stop(true, false, false, 8, 8) == SaleStop.Closed && InteriorRules.Stop(false, false, false, 8, 8) == SaleStop.Empty
              && InteriorRules.Stop(false, true, false, 8, 8) == SaleStop.NoStaff && InteriorRules.Stop(false, true, true, 8, 8) == SaleStop.Prices
              && InteriorRules.Stop(false, true, true, 8, 0) == SaleStop.None, "interiors: why a business sells nothing, the first reason that holds");
        Check(InteriorRules.Stop(false, true, true, 7, 7) == SaleStop.None && InteriorRules.Stop(false, true, true, 8, 7) == SaleStop.None
              && InteriorRules.Stop(false, true, true, 0, 0) == SaleStop.None, "interiors: prices count as turned down only when every one of the last 8 customers, and no fewer, turned them down");
        InteriorShop selling = InteriorSample(InteriorKind.Shop);
        InteriorRules.Work(selling, 0, InteriorDefaults());
        InteriorShop stopped = InteriorSample(InteriorKind.Shop);
        stopped.Stop = SaleStop.Empty;
        InteriorRules.Work(stopped, 0, InteriorDefaults());
        Check(selling.Footfall > 0 && selling.Basket > 0 && stopped.Footfall == 0 && stopped.DecorFootfall == 0 && stopped.StandardFootfall == 0 && stopped.Basket == 0
              && stopped.Bears == selling.Bears && stopped.Overall == selling.Overall, "interiors: selling nothing, a fine shop's interior brings nobody extra and adds nothing to a basket; its scores and prices stay");
        InteriorShop poor = InteriorSample(InteriorKind.Shop);
        poor.Interior = 5;
        Array.Clear(poor.Points, 0, poor.Points.Length);
        poor.Income = 140;
        poor.Stop = SaleStop.NoStaff;
        InteriorRules.Work(poor, 0, InteriorDefaults());
        Check(poor.Footfall == -6 && poor.ComplaintChance > 0, "interiors: selling nothing, a shortfall of the standard still costs what it costs");
    }

    private static void InteriorOfficeClients()
    {
        Check(InteriorRules.OfficeBoost(100, 200) == 200 && InteriorRules.OfficeBoost(80, 50) == 40 && InteriorRules.OfficeBoost(50, 0) == 0 && InteriorRules.OfficeBoost(100, 990) == 200
              && InteriorRules.OfficeBoost(150, 200) == InteriorRules.OfficeBoostMost && InteriorRules.OfficeBoost(100, InteriorRules.DecorMille) == 100,
            "interiors: an office's boost is the decor's strength at its overall score, 10% by default, 20% at most");
        int carry = 0;
        Check(InteriorRules.OfficeClients(10, 5, 200, ref carry) == 2 && carry == 0, "interiors: 20% more clients on the game's 10 is 2 more");
        carry = 0;
        int got = 0;
        for (int h = 0; h < 4; h++)
        {
            got += InteriorRules.OfficeClients(3, 5, 100, ref carry);
        }
        Check(got == 1 && carry == 200, "interiors: a fraction of a client is carried hour to hour until it makes one (" + got + ", " + carry + ")");
        carry = 0;
        Check(InteriorRules.OfficeClients(20, 1, 200, ref carry) == 1 && carry == 0, "interiors: never more clients than the staffed workstations left free, and nothing carried past them");
        carry = 500;
        Check(InteriorRules.OfficeClients(20, 0, 200, ref carry) == 0 && carry == 0 && InteriorRules.OfficeClients(20, -3, 200, ref carry) == 0, "interiors: no free station, no client");
        carry = 0;
        Check(InteriorRules.OfficeClients(0, 5, 200, ref carry) == 0 && InteriorRules.OfficeClients(10, 5, 0, ref carry) == 0, "interiors: no game clients or no boost, no extra clients");
        InteriorShop office = InteriorSample(InteriorKind.Office);
        InteriorRules.Work(office, 0, InteriorDefaults());
        InteriorShop shop = InteriorSample(InteriorKind.Shop);
        InteriorRules.Work(shop, 0, InteriorDefaults());
        InteriorShop idle = InteriorSample(InteriorKind.Office);
        idle.Stop = SaleStop.NoStaff;
        InteriorRules.Work(idle, 0, InteriorDefaults());
        InteriorShop ambience = InteriorSample(InteriorKind.Office);
        InteriorRules.Work(ambience, 0, InteriorAt(InteriorRules.StrengthDefault, true, false, true));
        Check(office.OfficeBoost == InteriorRules.OfficeBoost(office.Overall, 100) && office.OfficeBoost > 0 && shop.OfficeBoost == 0 && idle.OfficeBoost == 0 && ambience.OfficeBoost == 0,
            "interiors: only an office is booked more clients; not while it sells nothing, and not while Meaningful Ambience is on");
    }

    private static bool SplitAdds(CustomerSplit s)
    {
        return s.Game + s.Marketing + s.Interiors + s.Others + s.OtherMod == s.Total;
    }

    private static void InteriorCustomerSplit()
    {
        const int b = 250;   // the game's base multiplier, thousandths
        CustomerSplit plain = InteriorRules.Split(100, 0, 0, b, 40, 40, 40, 0, 0);
        Check(plain.Total == 100 && plain.Game == 100 && plain.Marketing == 0 && plain.Interiors == 0 && plain.Others == 0 && plain.OtherMod == 0,
            "interiors: with no marketing and nothing of the mod's, every customer is the street's");
        CustomerSplit marketing = InteriorRules.Split(100, 0, 0, b, 40, 60, 60, 0, 0);
        Check(marketing.Marketing == 21 && marketing.Game == 79 && SplitAdds(marketing), "interiors: marketing's customers are its part of the game's multiplier (" + marketing.Marketing + ")");
        int written = BusinessCatalogue.Written(40, 0, 0, 10, b / 1000f);
        CustomerSplit fine = InteriorRules.Split(110, 0, 0, b, 40, 40, written, 10, 0);
        Check(fine.Interiors == 10 && fine.Game == 100 && SplitAdds(fine), "interiors: 10% more for the interior shows as its tenth of the old customers (" + fine.Interiors + " of " + fine.Total + ")");
        int shortWritten = BusinessCatalogue.Written(40, 0, 0, -8, b / 1000f);
        CustomerSplit shabby = InteriorRules.Split(92, 0, 0, b, 40, 40, shortWritten, -8, 0);
        Check(shabby.Interiors < 0 && shabby.Game > shabby.Total && SplitAdds(shabby), "interiors: a shortfall shows as customers the interior kept away (" + shabby.Interiors + ")");
        int even = BusinessCatalogue.Written(40, 0, -8, 8, b / 1000f);
        CustomerSplit cancel = InteriorRules.Split(100, 0, 0, b, 40, 40, even, 8, -8);
        Check(cancel.Interiors > 0 && cancel.Others < 0 && Math.Abs(cancel.Interiors + cancel.Others) <= 1 && SplitAdds(cancel),
            "interiors: the interior's gain and the rest of the mod's loss each show, though they cancel (" + cancel.Interiors + ", " + cancel.Others + ")");
        CustomerSplit capped = InteriorRules.Split(150, 0, 0, b, 60, 95, 100, 24, 15);
        Check(capped.Interiors > 0 && capped.Others > 0 && capped.Interiors > capped.Others && SplitAdds(capped),
            "interiors: at the figure's top of 100 what is left is shared by the parts' sizes (" + capped.Interiors + ", " + capped.Others + ")");
        CustomerSplit booked = InteriorRules.Split(20, 3, 0, b, 40, 40, 40, 0, 0);
        Check(booked.Total == 23 && booked.Interiors == 3 && booked.Game == 20 && SplitAdds(booked), "interiors: an office's booked clients are the interior's");
        CustomerSplit ambience = InteriorRules.Split(50, 0, 7, b, 40, 40, 40, 0, 0);
        Check(ambience.Total == 57 && ambience.OtherMod == 7 && SplitAdds(ambience), "interiors: Meaningful Ambience's customers over the game's draw are its own");
        CustomerSplit nothing = InteriorRules.Split(12, 0, 0, 0, 0, 0, 0, 0, 0);
        Check(nothing.Game == 12 && SplitAdds(nothing) && InteriorRules.Split(0, 0, 0, b, 40, 60, 70, 10, 5).Total == 0, "interiors: with no multiplier to share by, or nobody, nothing is divided by nothing");
        bool always = true;
        for (int own = 0; own <= 100; own += 10)
        {
            for (int interior = -12; interior <= 24; interior += 6)
            {
                for (int other = -25; other <= 15; other += 10)
                {
                    int w = BusinessCatalogue.Written(own, 0, other, interior, b / 1000f);
                    CustomerSplit s = InteriorRules.Split(137, 2, 5, b, own / 2, own, w, interior, other);
                    always &= SplitAdds(s) && s.Total == 144;
                }
            }
        }
        Check(always, "interiors: the shares always add up to today's customers");
    }

    // A bare shop in the richest district: the game's interior score 5, nothing placed (standard 70).
    private static InteriorShop InteriorBare()
    {
        return new InteriorShop { Place = "Bare%20St~1", Name = "Bare", Kind = InteriorKind.Shop, Interior = 5, Sqm = 120, Clean = 90, Income = 140, GameMinimum = 0 };
    }

    // The bound: at the defaults the interior costs an established shop 6% of its customers at most, nothing of its
    // prices or baskets, and a complaint it brings counts inside the same 6%.
    private static void InteriorBound()
    {
        Check(InteriorRules.Bound(1000) == 6 && InteriorRules.Bound(2000) == 12 && InteriorRules.Bound(500) == 3 && InteriorRules.Bound(0) == 0,
            "interiors: the shortfall's bound is 6% at the default strength, 12% at double, 3% at half, nothing off");
        Check(InteriorRules.Bound(InteriorRules.ComplaintFromStrength) >= -BusinessCatalogue.ComplaintDip && InteriorRules.ComplaintFromStrength == InteriorRules.Strength(InteriorRules.StrengthDefault),
            "interiors: from the default strength, where the interior may bring complaints, its bound is at least what a complaint costs (" + -BusinessCatalogue.ComplaintDip + "%)");
        Check(InteriorRules.GiveWay(-6, -6, 6) == 0 && InteriorRules.GiveWay(-6, 0, 6) == -6 && InteriorRules.GiveWay(-6, -3, 6) == -3 && InteriorRules.GiveWay(-6, -10, 6) == 0
              && InteriorRules.GiveWay(3, -6, 6) == 3 && InteriorRules.GiveWay(-6, 5, 6) == -6 && InteriorRules.GiveWay(-12, -6, 12) == -6 && InteriorRules.GiveWay(-4, -1, 6) == -4,
            "interiors: the shortfall gives way to business pressure's dip so that the two together stay inside the bound; a gain or a boost is left alone");
        InteriorShop complained = InteriorBare();
        complained.IssueDip = BusinessCatalogue.ComplaintDip;
        InteriorRules.Work(complained, 0, InteriorDefaults());
        Check(complained.StandardFull == -6 && complained.StandardFootfall == 0 && complained.Footfall + complained.IssueDip == -6,
            "interiors: while a complaint costs a bare shop 6%, its shortfall takes nothing more: 6% in all");
        complained.IssueDip = 0;
        InteriorRules.Combine(complained);
        Check(complained.StandardFootfall == -6 && complained.Footfall == -6, "interiors: once the complaint is over the shortfall is back, asked again without reading the shop");
        bool bounded = true, takings = true, gains = true;
        int[] dips = { -12, -10, -6, -3, 0, 5, 10 };
        int[] ramps = { 0, 142, 571, 1000 };
        for (int strength = 0; strength <= InteriorRules.StrengthMost; strength += 10)
        {
            InteriorParts p = InteriorRules.Parts(strength, strength, strength, strength, true, true, true, false, false);
            int bound = InteriorRules.Bound(p.StandardScale);
            for (int interior = 0; interior <= 100; interior += 10)
            {
                for (int income = 60; income <= 140; income += 20)
                {
                    for (int wear = 0; wear <= 1000; wear += 500)
                    {
                        foreach (int dip in dips)
                        {
                            foreach (int ramp in ramps)
                            {
                                InteriorShop s = InteriorSample(InteriorKind.Shop);
                                s.Interior = interior;
                                s.Income = income;
                                s.IssueDip = dip;
                                s.Ramp = ramp;
                                if (interior < 30)
                                {
                                    Array.Clear(s.Points, 0, s.Points.Length);
                                }
                                InteriorRules.Work(s, wear, p);
                                bounded &= Math.Min(0, dip) + s.Footfall >= Math.Min(Math.Min(0, dip), -bound) && s.Footfall >= -bound;
                                gains &= s.Bears >= 0 && s.Basket >= 0;
                                if (strength == InteriorRules.StrengthDefault && dip >= 0)
                                {
                                    takings &= InteriorRules.Takings(s.Footfall, s.Basket) >= -InteriorRules.ShortFootfallMost * 10;
                                }
                                if (s.ComplaintChance > 0)
                                {
                                    bounded &= bound >= -BusinessCatalogue.ComplaintDip;
                                }
                            }
                        }
                    }
                }
            }
        }
        Check(bounded, "interiors: at every slider's figure, score, district, wear, dip and day of phasing in, the interior and business pressure's dip together never take more than the larger of the dip and the bound");
        Check(gains, "interiors: the price the district bears and the basket only ever gain");
        Check(takings, "interiors: at the defaults no shop's takings are ever more than 6% under what they would be without the interior");
        Check(InteriorRules.Takings(-6, 0) == -60 && InteriorRules.Takings(0, 0) == 0 && InteriorRules.Takings(10, 81) == 189 && InteriorRules.Takings(-6, 50) == -13,
            "interiors: its takings are its customers and their baskets together (" + InteriorRules.Takings(10, 81) + " per mille at +10% customers and 8.1% more products)");
    }

    // Phased in: a seventh a day from the day the rules first read a business, in full on the seventh, never a jump.
    private static void InteriorPhaseIn()
    {
        Check(InteriorRules.Ramp(116, 116) == 142 && InteriorRules.Ramp(116, 117) == 285 && InteriorRules.Ramp(116, 121) == 857 && InteriorRules.Ramp(116, 122) == 1000
              && InteriorRules.Ramp(116, 400) == 1000 && InteriorRules.Ramp(116, 115) == 0 && InteriorRules.FullFrom(116) == 122,
            "interiors: every effect comes in a seventh a day, in full on the seventh day");
        Check(InteriorRules.Ramp(InteriorFile.Unset, 5) == 1000 && InteriorRules.Ramp(InteriorFile.LongAgo, 5) == 1000, "interiors: with no day to come in from, the rules apply as they are");
        bool smooth = true, ends = true, quiet = true;
        foreach (bool bare in new[] { true, false })
        {
            InteriorShop whole = bare ? InteriorBare() : InteriorSample(InteriorKind.Shop);
            InteriorRules.Work(whole, 0, InteriorDefaults());
            int stepCustomers = Math.Abs(whole.Footfall) / InteriorRules.RampDays + 2;   // (each of the two parts rounds on its own)
            int stepTakings = Math.Abs(InteriorRules.Takings(whole.Footfall, whole.Basket)) / InteriorRules.RampDays + 20;
            int was = 0, wasTakings = 0;
            for (int day = 115; day <= 125; day++)
            {
                InteriorShop s = bare ? InteriorBare() : InteriorSample(InteriorKind.Shop);
                s.PhaseFrom = 116;
                s.Ramp = InteriorRules.Ramp(116, day);
                InteriorRules.Work(s, 0, InteriorDefaults());
                int t = InteriorRules.Takings(s.Footfall, s.Basket);
                if (day == 115)
                {
                    ends &= s.Footfall == 0 && s.Bears == 0 && s.Basket == 0 && s.ComplaintChance == 0;
                }
                else
                {
                    smooth &= Math.Abs(s.Footfall - was) <= stepCustomers && Math.Abs(t - wasTakings) <= stepTakings;
                }
                quiet &= s.Ramp >= 1000 || s.ComplaintChance == 0;
                if (day >= InteriorRules.FullFrom(116))
                {
                    InteriorShop full = bare ? InteriorBare() : InteriorSample(InteriorKind.Shop);
                    InteriorRules.Work(full, 0, InteriorDefaults());
                    ends &= s.Footfall == full.Footfall && s.Bears == full.Bears && s.Basket == full.Basket && s.ComplaintChance == full.ComplaintChance;
                }
                was = s.Footfall;
                wasTakings = t;
            }
        }
        Check(ends, "interiors: before its first day nothing applies, and from the seventh everything applies in full");
        Check(smooth, "interiors: coming in, no day moves a shop's customers or takings by more than a seventh of the whole, and a point for each part's rounding");
        Check(quiet, "interiors: no complaint while the rules are still coming in");
        InteriorShop first = InteriorBare();
        first.Ramp = InteriorRules.Ramp(116, 116);
        InteriorRules.Work(first, 0, InteriorDefaults());
        Check(first.StandardFull == -6 && first.Footfall == -1, "interiors: a bare shop on the first day of the rules loses 1% of its customers, not 6% (" + first.Footfall + ")");
    }

    // The case a player reported (a large save, four electronics stores' takings halved on one morning): what the
    // interiors of 0.9.10 could take from a plain electronics store of 300 square metres in Midtown, and what they take
    // now. Not the halving: 0.9.10's most at Standard was 8% of customers and 2% of what the district bears, so a drop
    // of a third of the customers and half the takings is not this feature's alone.
    private static void InteriorIncomeCase()
    {
        var store = new InteriorShop { Place = "Fifth%20Ave~10", Name = "Electronics", Kind = InteriorKind.Shop, Interior = 30, Sqm = 300, Clean = 85, Income = 125, GameMinimum = 50 };
        store.Points[(int)DecorCategory.Equipment] = 2 * 12;   // shelves and tills only: equipment counts for a little
        store.Points[(int)DecorCategory.Lighting] = 8;
        store.Kinds = 1;
        InteriorRules.Work(store, 0, InteriorDefaults());
        int gap = store.Standard - store.Overall;
        int oldShort = -Math.Min(8, (gap + 3) / 4);                  // 0.9.10: 1% for each 4 points short, rounded up, 8% at most
        int oldDecor = InteriorRules.DecorFootfall(store.Decor, 100);   // 0.9.10: decor 10% at its default
        int oldFootfall = InteriorRules.Footfall(oldShort, oldDecor);
        int oldBears = -Math.Min(20, gap);                            // 0.9.10: 1 per mille a point under, 2% at most
        InteriorShop firstDay = new InteriorShop { Place = store.Place, Name = store.Name, Kind = store.Kind, Interior = store.Interior, Sqm = store.Sqm, Clean = store.Clean, Income = store.Income, GameMinimum = store.GameMinimum };
        Array.Copy(store.Points, firstDay.Points, store.Points.Length);
        firstDay.Kinds = store.Kinds;
        firstDay.Ramp = InteriorRules.Ramp(116, 116);
        InteriorRules.Work(firstDay, 0, InteriorDefaults());
        Check(store.Standard == 59 && gap >= 30 && oldFootfall == -8 && oldBears == -20 && store.Footfall == -6 && store.Bears == 0 && firstDay.Footfall == -1,
            "interiors: the reported case, a plain electronics store of 300 m2 in Midtown (standard " + store.Standard + ", overall " + store.Overall + "): 0.9.10 took " + -oldFootfall
            + "% of its customers and " + -oldBears / 10 + "% of what its district bears from the first morning; now " + -store.Footfall + "% at most and nothing of its prices, " + -firstDay.Footfall
            + "% on the first day, in full after " + InteriorRules.RampDays + " days");
        int worstOld = -12;   // 0.9.10 at Strict, its strongest shortfall
        Check(worstOld > -30 && -InteriorRules.Bound(InteriorRules.Strength(InteriorRules.StrengthMost)) > -30,
            "interiors: neither 0.9.10 at its strictest (12%) nor these rules at double (12%) can take a third of a shop's customers, as the report saw");
    }

    private static InteriorShop InteriorSample(InteriorKind kind)
    {
        var s = new InteriorShop { Place = "Main%20St~1", Name = "Sample", Kind = kind, Interior = 70, Sqm = 120, Clean = 90, Income = 120, GameMinimum = 0 };
        s.Points[(int)DecorCategory.Art] = 60;
        s.Points[(int)DecorCategory.Greenery] = 40;
        s.Items[(int)DecorCategory.Greenery] = 4;
        s.Points[(int)DecorCategory.Lighting] = 60;
        s.Points[(int)DecorCategory.Floor] = 10;
        s.Points[(int)DecorCategory.Furniture] = 80;
        s.Points[(int)DecorCategory.Equipment] = 80;
        s.Kinds = 9;
        s.RestCorner = true;
        return s;
    }

    private static void InteriorWork()
    {
        InteriorShop s = InteriorSample(InteriorKind.Shop);
        InteriorRules.Work(s, 0, InteriorDefaults());
        Check(s.Target == 200 && s.Capped[(int)DecorCategory.Furniture] == 30 && s.Capped[(int)DecorCategory.Equipment] == 20,
            "interiors: a worked shop's target and caps (" + s.Target + ", " + s.Capped[(int)DecorCategory.Furniture] + ")");
        Check(s.DecorRaw == Math.Min(100, 100 * (60 + 40 + 60 + 0 + 10 + 30 + 20) / 200 + 6) && s.Decor == s.DecorRaw, "interiors: a worked shop's decor (" + s.DecorRaw + ")");
        Check(s.Standard == InteriorRules.Standard(120, 0) && s.Overall == InteriorRules.Overall(s.InteriorNow, s.Decor), "interiors: its overall figure and its district's standard");
        Check(s.Footfall == InteriorRules.Footfall(s.StandardFootfall, s.DecorFootfall) && s.Footfall >= -12 && s.Footfall <= 24 && s.Bears != 0 && s.Comfort == 5,
            "interiors: its effects are the rules' (" + s.Footfall + "%, " + s.Bears + " per mille, comfort " + s.Comfort + ")");
        InteriorShop worn = InteriorSample(InteriorKind.Shop);
        InteriorRules.Work(worn, 1000, InteriorDefaults());
        Check(worn.Overall < s.Overall && worn.Footfall <= s.Footfall, "interiors: the same place worn out scores less and draws no more");
        InteriorShop off = InteriorSample(InteriorKind.Shop);
        InteriorRules.Work(off, 0, InteriorOff());
        Check(off.Footfall == 0 && off.Bears == 0 && off.Basket == 0 && off.ComplaintChance == 0 && off.BrandPoints == 0 && off.StandingPoints == 0 && off.Comfort == 0 && off.OfficeBid == 0,
            "interiors: every part switched off does nothing");
        InteriorShop office = InteriorSample(InteriorKind.Office);
        InteriorRules.Work(office, 0, InteriorDefaults());
        Check(office.Footfall == 0 && office.Bears == 0 && office.Basket == 0 && office.ComplaintChance == 0 && office.Comfort == 5 && office.OfficeEsteem == InteriorRules.OfficeEsteem(office.Overall),
            "interiors: an office gets no customers' parts, only staff, hiring and bids");
        InteriorShop none = InteriorSample(InteriorKind.None);
        InteriorRules.Work(none, 0, InteriorDefaults());
        Check(none.Footfall == 0 && none.Comfort == 0 && none.OfficeBid == 0, "interiors: a place of neither kind gets nothing");
        InteriorShop poor = InteriorSample(InteriorKind.Shop);
        poor.Interior = 5;
        Array.Clear(poor.Points, 0, poor.Points.Length);
        poor.Income = 140;
        InteriorRules.Work(poor, 900, InteriorDefaults());
        Check(poor.Footfall == -6 && poor.ComplaintChance > 0 && poor.Bears == 0 && poor.Basket == 0, "interiors: a bare, worn shop in the richest district loses 6% of its customers and nothing of its prices, and draws complaints, never all its customers");
        Check(InteriorRules.Next(poor) == (int)DecorCategory.Art, "interiors: with nothing placed, art comes first to add");
        Check(InteriorRules.Fitting(10, 23) && !InteriorRules.Fitting(10, 24) && !InteriorRules.Fitting(InteriorFile.Unset, 5) && !InteriorRules.Fitting(InteriorFile.LongAgo, 1),
            "interiors: a new business fits out for 14 days; one trading already, or not read yet, does not");
        InteriorShop fitting = InteriorSample(InteriorKind.Shop);
        fitting.Interior = 5;
        Array.Clear(fitting.Points, 0, fitting.Points.Length);
        fitting.Income = 140;
        fitting.Fitting = true;
        InteriorRules.Work(fitting, 0, InteriorDefaults());
        Check(fitting.StandardFootfall == 0 && fitting.StandardFull == 0 && fitting.Bears == 0 && fitting.ComplaintChance == 0 && fitting.Overall < fitting.Standard,
            "interiors: fitting out, a bare shop loses nothing for falling short of its standard");
        InteriorShop fine = InteriorSample(InteriorKind.Shop);
        fine.Fitting = true;
        InteriorRules.Work(fine, 0, InteriorDefaults());
        Check(fine.StandardFootfall == s.StandardFootfall && fine.Bears == s.Bears && fine.Footfall == s.Footfall, "interiors: fitting out, being over the standard counts at once");
        Check(InteriorRules.Next(s) == (int)DecorCategory.Audio, "interiors: the sample shop most needs audio next (" + InteriorRules.Next(s) + ")");
    }

    private static void InteriorSave()
    {
        var store = new InteriorStore();
        InteriorFile a = store.File("Main%20St~1", true);
        a.Wear = 420;
        a.LastInterior = 55;
        a.LastDay = 30;
        a.StoryDay = 12;
        a.RefreshDay = 9;
        a.FirstDay = 4;
        store.File("Park%20Ave~22", true).Wear = 1;
        Check(store.File("bad|place~1", true) == null && store.File("bad,place~1", true) == null && store.File(string.Empty, true) == null,
            "interiors: a place that would break the line is not kept");
        InteriorStore back = InteriorStore.Read(store.Write());
        InteriorFile b = back.File("Main%20St~1", false);
        Check(back.Files.Count == 2 && b != null && b.Wear == 420 && b.LastInterior == 55 && b.LastDay == 30 && b.StoryDay == 12 && b.RefreshDay == 9 && b.FirstDay == 4,
            "interiors: the save's line reads back what it wrote");
        Check(InteriorStore.Read("3|x~1,1,1,1,1,1,1").Files.Count == 0 && InteriorStore.Read("2|x~1,1,1,1,1,1").Files.Count == 0
              && InteriorStore.Read("2|x~1,a,1,1,1,1,1|y~2,5,1,1,1,1,8").Files.Count == 1 && InteriorStore.Read(null).Files.Count == 0,
            "interiors: another version's line, or a file that cannot be read, is left out");
        var rules = new InteriorStore();
        Check(rules.WriteRules() == null && rules.PhaseFrom(a) == InteriorFile.Unset, "interiors: before the rules have read a save there is no rules' line and no day to phase in from");
        rules.RulesFrom = 116;
        var rulesBack = new InteriorStore();
        rulesBack.ReadRules(rules.WriteRules());
        Check(rules.WriteRules() == "1,116" && rulesBack.RulesFrom == 116, "interiors: the rules' line reads back the day they first read the save");
        rulesBack.ReadRules("2,116");
        Check(rulesBack.RulesFrom == InteriorFile.Unset, "interiors: another version's rules' line is left out (the save meets the rules again that day)");
        rulesBack.ReadRules("1,x");
        Check(rulesBack.RulesFrom == InteriorFile.Unset, "interiors: a rules' line that cannot be read is left out");
        Check(rules.PhaseFrom(new InteriorFile { FirstDay = InteriorFile.LongAgo }) == 116 && rules.PhaseFrom(new InteriorFile { FirstDay = 40 }) == 116
              && rules.PhaseFrom(new InteriorFile { FirstDay = 130 }) == 130 && rules.PhaseFrom(null) == 116,
            "interiors: a business trading before the rules read the save comes in from that day; one opened afterwards from its own first day");
        Check(InteriorStore.Read(store.Write()).Files.Count == store.Files.Count && InteriorStore.Key != InteriorStore.RulesKey,
            "interiors: the rules' day is a line of its own, so a save read again by an older version keeps every business's file");
        InteriorStore old = InteriorStore.Read("1|x~1,a,1,1,1,1|y~2,5,1,1,1,1");
        Check(old.Files.Count == 1 && old.File("y~2", false).Wear == 5 && old.File("y~2", false).FirstDay == InteriorFile.LongAgo,
            "interiors: the first version's line is read, its businesses as trading already");
        Check(InteriorStore.Read("2|x~1,99999,-50,1,1,1,1").File("x~1", false).Wear == InteriorRules.WearMost, "interiors: a wear read is kept inside its bounds");
        for (int i = 0; i < InteriorStore.Most + 20; i++)
        {
            store.File("Street~" + i, true);
        }
        Check(store.Files.Count == InteriorStore.Most, "interiors: no more than " + InteriorStore.Most + " files are kept");
        Check(store.Prune(p => p.StartsWith("Main", StringComparison.Ordinal)) == InteriorStore.Most - 1 && store.Files.Count == 1, "interiors: a business no longer the player's is dropped");
    }

    // The other features' rules that take the interior's part.
    private static void InteriorElsewhere()
    {
        Check(BrandRules.WithInterior(70, 3) == 73 && BrandRules.WithInterior(70, 9) == 73 && BrandRules.WithInterior(99, 3) == 100 && BrandRules.WithInterior(1, -3) == 0,
            "interiors: a brand's shop standards move 3 points at most, inside 0 to 100");
        var shops = new List<FameShop> { new FameShop { Figures = true, Satisfaction = 80, Pricing = 60, Interior = 2 } };
        var plain = new List<FameShop> { new FameShop { Figures = true, Satisfaction = 80, Pricing = 60 } };
        var much = new List<FameShop> { new FameShop { Figures = true, Satisfaction = 80, Pricing = 60, Interior = 9 } };
        Check(FameRules.CustomersTarget(shops) == FameRules.CustomersTarget(plain) + 2 && FameRules.CustomersTarget(much) == FameRules.CustomersTarget(shops),
            "interiors: Standing's customers' target moves 2 points at most for a shop's interior");
        StaffFile calm = Worker(1200, 1200), comfy = Worker(1200, 1200);
        calm.Standing = comfy.Standing = 40;
        for (int day = 1; day <= 60; day++)
        {
            StaffRules.Day(calm, CompanyCatalogue.Standard, day);
            StaffRules.Day(comfy, CompanyCatalogue.Standard, day, 5);
        }
        Check(calm.Standing == CompanyCatalogue.StandingCalm && comfy.Standing == CompanyCatalogue.StandingCalm + 5,
            "interiors: staff in a comfortable workplace settle 5 points higher (" + comfy.Standing + ")");
        Profile p = Person(0, 1, 50, 50, 50);
        WorkDay plainDay = Day(8, 3000, 3000), comfyDay = Day(8, 3000, 3000);
        comfyDay.Comfort = 40;
        WorkDay muchDay = comfyDay;
        muchDay.Comfort = 500;
        Check(TalentRules.MoraleTarget(p, comfyDay) == TalentRules.MoraleTarget(p, plainDay) + 40 && TalentRules.MoraleTarget(p, muchDay) == TalentRules.MoraleTarget(p, plainDay) + 100,
            "interiors: a professional's morale target takes the workplace's comfort, a tenth at most whatever is asked");
        int plainWins = 0, officeWins = 0;
        for (int i = 0; i < 400; i++)
        {
            var f = new FirmsFile { Seed = 500 + i };
            var t = new Contract { Id = i, Kind = (int)WorkKind.Integration, Value = 5000000, Rivals = 3, Stage = (int)WorkStage.Bid };
            t.Bid = FirmsRules.BidAt(t, 950);
            plainWins += FirmsRules.Decide(f, t, new Firm { Reputation = 550 }, 1) ? 1 : 0;
            var u = new Contract { Id = i, Kind = (int)WorkKind.Integration, Value = 5000000, Rivals = 3, Stage = (int)WorkStage.Bid };
            u.Bid = FirmsRules.BidAt(u, 950);
            officeWins += FirmsRules.Decide(f, u, new Firm { Reputation = 550 }, 1, InteriorRules.BidMost) ? 1 : 0;
        }
        Check(officeWins >= plainWins && officeWins < plainWins + 400 / 5, "interiors: a good office wins a few more tenders, not many (" + officeWins + " against " + plainWins + " of 400)");
    }
}
