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
        Check(InteriorRules.StandardFootfall(40, 40) == 0 && InteriorRules.StandardFootfall(36, 40) == -1 && InteriorRules.StandardFootfall(35, 40) == -2,
            "interiors: 1% fewer customers for each 4 points short, rounded up");
        Check(InteriorRules.StandardFootfall(0, 70) == -InteriorRules.ShortFootfallMost && InteriorRules.StandardFootfall(0, 70) == -8, "interiors: short of the standard costs 8% at most");
        Check(InteriorRules.StandardFootfall(48, 40) == 1 && InteriorRules.StandardFootfall(100, 15) == 3, "interiors: over it 1% for each 8 points, 3% at most");
        Check(InteriorRules.DecorFootfall(100, 5) == 5 && InteriorRules.DecorFootfall(0, 5) == 0 && InteriorRules.DecorFootfall(150, 5) == 5, "interiors: decor brings up to 5% more customers by default");
        Check(InteriorRules.Footfall(3, 5) == 8 && InteriorRules.Footfall(-8, 0) == -8 && InteriorRules.Footfall(-8, 5) == -3, "interiors: the footfall parts are added");
        Check(InteriorRules.FootfallLeast == BusinessCatalogue.InteriorMostDip && InteriorRules.FootfallMost == BusinessCatalogue.InteriorMostBoost,
            "interiors: its footfall is business pressure's interior part, with the same bounds");
        Check(InteriorRules.Bears(60, 40) == 20 && InteriorRules.Bears(100, 15) == 30 && InteriorRules.Bears(20, 40) == -20 && InteriorRules.Bears(0, 70) == -20,
            "interiors: the district bears 1 per mille a point over the standard (3% at most) and 1 a point under (2% at most)");
        Check(CityModel.WithBears(1000, 30) == 1030 && CityModel.WithBears(1000, 500) == 1100 && CityModel.WithBears(1100, -20) == 1078 && CityModel.WithBears(1000, 0) == 1000,
            "interiors: the interior's part goes on the district's own tolerance (the city's economy keeps it to a tenth whatever is asked)");
        Check(CityModel.Against(1030, CityModel.WithBears(1000, InteriorRules.BearsOverMost)) == 1000 && !CityModel.Over(CityModel.Against(1050, CityModel.WithBears(1000, InteriorRules.BearsOverMost))),
            "interiors: prices 3% over the district's are borne in full at a fine place");
        Check(InteriorRules.ComplaintChance(31, 40) == 0 && InteriorRules.ComplaintChance(30, 40) == 40 && InteriorRules.ComplaintChance(0, 70) == 120,
            "interiors: a complaint from 10 points short, 4 per mille a point, 12% a day at most");
        Check(InteriorRules.BasketChance(40, 40) == 0 && InteriorRules.BasketChance(50, 40) == 20 && InteriorRules.BasketChance(100, 15) == 60 && InteriorRules.BasketChance(10, 40) == 0,
            "interiors: the basket's chance is 2 per mille a point over the standard, 6% at most, nothing under it");
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

    // The parts at the options' defaults, the basket switched on or not; and every part off.
    private static InteriorParts InteriorDefaults(bool basket = true)
    {
        return InteriorRules.Parts(InteriorRules.StandardDefault, InteriorRules.RewardDefault, InteriorRules.DecorDefault, InteriorRules.PriceDefault, InteriorRules.WearDefault,
            basket, true, true, false, false);
    }

    private static InteriorParts InteriorOff()
    {
        return InteriorRules.Parts(0, 0, 0, 0, 0, false, false, false, false, false);
    }

    private static void InteriorStepAside()
    {
        InteriorParts mi = InteriorRules.Parts(InteriorRules.StandardDefault, InteriorRules.RewardDefault, InteriorRules.DecorDefault, InteriorRules.PriceDefault, InteriorRules.WearDefault,
            true, true, true, true, false);
        Check(mi.StandardScale == 0 && mi.RewardScale == 0 && !mi.Basket && mi.DecorPercent == 5 && mi.PriceScale == 1000 && mi.WearScale == 1000 && mi.Staff && mi.Standing,
            "interiors: with Meaningful Interiors on, the shortfall, the reward over the standard and the basket are left to it; the rest keeps running");
        InteriorParts ma = InteriorRules.Parts(InteriorRules.StandardDefault, InteriorRules.RewardDefault, InteriorRules.DecorDefault, InteriorRules.PriceDefault, InteriorRules.WearDefault,
            true, true, true, false, true);
        Check(ma.DecorPercent == 0 && ma.StandardScale == 1000 && ma.RewardScale == 1000 && ma.Basket, "interiors: with Meaningful Ambience on, the customers decor brings are left to it; the rest keeps running");
        Check(!InteriorDefaults(false).Basket && !InteriorRules.Parts(2, 0, 1, 3, 2, true, true, true, false, false).Basket,
            "interiors: the basket needs its own switch, and a reward that is not Off");
    }

    private static void InteriorStrengths()
    {
        Check(InteriorRules.DecorPercents[InteriorRules.DecorDefault] == 5 && InteriorRules.RewardScales[InteriorRules.RewardDefault] == 1000
              && InteriorRules.StandardScales[InteriorRules.StandardDefault] == 1000 && InteriorRules.PriceScales[InteriorRules.PriceDefault] == 1000
              && InteriorRules.WearScales[InteriorRules.WearDefault] == 1000, "interiors: every strength's default gives the figures of before");
        Check(string.Join(",", InteriorRules.DecorPercents) == "0,5,10,15,20" && string.Join(",", InteriorRules.RewardScales) == "0,500,750,1000,1250"
              && string.Join(",", InteriorRules.StandardScales) == "0,500,1000,1500" && string.Join(",", InteriorRules.WearScales) == "0,500,1000,1500",
            "interiors: the choices cover the other mods' ranges (decor Off to 20%, the reward Off to 1.25 times)");
        Check(InteriorRules.Level(InteriorRules.DecorPercents, -1) == 0 && InteriorRules.Level(InteriorRules.DecorPercents, 9) == 20, "interiors: a choice outside the list is kept inside it");
        Check(InteriorRules.Scale(3, 1250) == 4 && InteriorRules.Scale(3, 500) == 2 && InteriorRules.Scale(-8, 1500) == -12 && InteriorRules.Scale(-7, 500) == -4 && InteriorRules.Scale(5, 0) == 0
              && InteriorRules.Scale(5, -1000) == 0, "interiors: a figure at a strength rounds half away from nothing, and a strength under nothing is nothing");
        Check(InteriorRules.StandardFootfall(0, 70, 1500, 1000) == -12 && InteriorRules.StandardFootfall(0, 70, 500, 1000) == -4 && InteriorRules.StandardFootfall(0, 70, 0, 1000) == 0,
            "interiors: short of the standard costs 12% at most when Strict, 4% when Gentle, nothing when Off");
        Check(InteriorRules.StandardFootfall(100, 15, 1000, 1250) == 4 && InteriorRules.StandardFootfall(100, 15, 1000, 500) == 2 && InteriorRules.StandardFootfall(100, 15, 1000, 0) == 0
              && InteriorRules.StandardFootfall(100, 15, 0, 1000) == 3, "interiors: over it gains 4% when Generous, 2% when Low, nothing when Off; the shortfall's strength does not touch it");
        Check(InteriorRules.DecorFootfall(100, 20) == 20 && InteriorRules.DecorFootfall(60, 10) == 6 && InteriorRules.DecorFootfall(100, 99) == 20 && InteriorRules.DecorFootfall(100, 0) == 0,
            "interiors: decor brings up to 20% at the strongest, in step with the decor score");
        Check(InteriorRules.Footfall(4, 20) == 24 && InteriorRules.Footfall(-12, 0) == -12 && InteriorRules.Footfall(-30, 0) == -12 && InteriorRules.Footfall(30, 0) == 24,
            "interiors: its footfall is -12% to +24% at the strongest choices");
        bool inside = true;
        for (int st = 0; st < InteriorRules.StandardScales.Length; st++)
        {
            for (int rw = 0; rw < InteriorRules.RewardScales.Length; rw++)
            {
                for (int dc = 0; dc < InteriorRules.DecorPercents.Length; dc++)
                {
                    for (int overall = 0; overall <= 100; overall += 10)
                    {
                        for (int standard = 15; standard <= 70; standard += 11)
                        {
                            int f = InteriorRules.Footfall(InteriorRules.StandardFootfall(overall, standard, InteriorRules.StandardScales[st], InteriorRules.RewardScales[rw]),
                                InteriorRules.DecorFootfall(overall, InteriorRules.DecorPercents[dc]));
                            inside &= f >= InteriorRules.FootfallLeast && f <= InteriorRules.FootfallMost;
                        }
                    }
                }
            }
        }
        Check(inside, "interiors: at every choice of every strength, its footfall stays inside its bounds");
        Check(InteriorRules.BasketChance(100, 15, 1250) == 75 && InteriorRules.BasketChance(100, 15, 500) == 30 && InteriorRules.BasketChance(100, 15, 0) == 0,
            "interiors: the basket's chance follows the reward: 7.5% at most when Generous, 3% when Low");
        Check(InteriorRules.Bears(100, 15, 1250) == 38 && InteriorRules.Bears(0, 70, 500) == -10 && InteriorRules.Bears(100, 15, 0) == 0,
            "interiors: the price borne follows its strength either way");
        Check(InteriorRules.ComplaintChance(0, 70, 1500) == 180 && InteriorRules.ComplaintChance(0, 70, 500) == 60 && InteriorRules.ComplaintChance(0, 70, 0) == 0,
            "interiors: complaints follow the standard's strength: 18% a day at most when Strict");
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
        Check(poor.Footfall == -8 && poor.ComplaintChance > 0 && poor.Bears == -20, "interiors: a bare, worn shop in the richest district loses 8% and 2% of its prices and draws complaints, never all its customers");
        Check(InteriorRules.Next(poor) == (int)DecorCategory.Art, "interiors: with nothing placed, art comes first to add");
        Check(InteriorRules.Fitting(10, 23) && !InteriorRules.Fitting(10, 24) && !InteriorRules.Fitting(InteriorFile.Unset, 5) && !InteriorRules.Fitting(InteriorFile.LongAgo, 1),
            "interiors: a new business fits out for 14 days; one trading already, or not read yet, does not");
        InteriorShop fitting = InteriorSample(InteriorKind.Shop);
        fitting.Interior = 5;
        Array.Clear(fitting.Points, 0, fitting.Points.Length);
        fitting.Income = 140;
        fitting.Fitting = true;
        InteriorRules.Work(fitting, 0, InteriorDefaults());
        Check(fitting.StandardFootfall == 0 && fitting.Bears == 0 && fitting.ComplaintChance == 0 && fitting.Overall < fitting.Standard,
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
