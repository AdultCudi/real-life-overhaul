using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Brands;

// Checks for brands and franchises (Scripts/Economy/Brands): standards from a shop's figures, recognition by district
// and city-wide, what a brand adds to a member shop's footfall and how that is bounded, brand campaigns and scandals,
// applicants from a seed, the terms they take, a franchised shop's day (takings, royalty, levy, support, the
// franchisee's cash and standards), every way an agreement changes or ends, a shop under a city chain, the save's line,
// and the time a city with sixty franchised shops takes.
internal static partial class Program
{
    private const string Midtown = "ba:neighborhood_midtown", Garment = "ba:neighborhood_garmentdistrict", Kitchen = "ba:neighborhood_hellskitchen";
    private const string CoffeeType = "ba:businesstype_coffeeshop";
    private static readonly string[] ThreeDistricts = { Midtown, Garment, Kitchen };

    private static void Brands()
    {
        BrandCatalogueTables();
        BrandStandards();
        BrandRecognition();
        BrandFootfall();
        BrandMembership();
        BrandCampaigns();
        BrandScandals();
        FranchiseApplicants();
        FranchiseTerms();
        FranchiseTakings();
        FranchiseMoney();
        FranchiseDays();
        FranchiseStandardsKept();
        FranchiseEndings();
        FranchiseAsks();
        FranchiseChains();
        KeptPriceRules();
        BrandSave();
        BrandBalance();
        BrandTiming();
    }

    /// <summary>A brand with so many member shops in Midtown, each with the same figures.</summary>
    private static BrandsState NewBrands(int members, out Brand brand, int standards = 80, int customers = 150, int takings = 6000)
    {
        var s = new BrandsState { Seed = 4242, FirstDay = 1, LastDay = 1 };
        brand = BrandRules.Create(s, "Copper Kettle", CoffeeType, "Main~1", Midtown, 1);
        for (int i = 1; i < members; i++)
        {
            BrandRules.Join(s, brand, "Main~" + (i + 1), CoffeeType, Midtown, 1);
        }
        foreach (Member m in s.Members)
        {
            m.Standards = standards;
            m.Customers = customers;
            m.Takings = takings;
        }
        brand.Ticket = BrandRules.Ticket(s, brand);
        brand.Standards = BrandRules.Standards(s, brand);
        return s;
    }

    private static Applicant Someone(BrandsState s, Brand b, Temper temper, int capital = 100000, int ability = 60, string district = Midtown)
    {
        var a = new Applicant { Id = s.NextId++, BrandId = b.Id, Name = "Harbour Lane Trading", Temper = temper, Capital = capital, Ability = ability, District = district, Until = 99 };
        s.Applicants.Add(a);
        return a;
    }

    private static Franchise Signed(BrandsState s, Brand b, Temper temper, int day = 10, int capital = 100000, int ability = 60, int royaltyTier = 1, int levyTier = 1, string district = Midtown, bool exclusive = false)
    {
        Franchise f = FranchiseRules.Sign(s, b, Someone(s, b, temper, capital, ability, district), 1, royaltyTier, levyTier, exclusive, 0, 1000, day, 60);
        f.Base = 150;
        return f;
    }

    private static void BrandCatalogueTables()
    {
        Check(BrandCatalogue.WeightSatisfaction + BrandCatalogue.WeightClean + BrandCatalogue.WeightStock + BrandCatalogue.WeightSkill == 100, "the four parts of a shop's standards add up to 100");
        Check(BrandCatalogue.BrandMeanShare + BrandCatalogue.BrandWorstShare == 1000, "the mean and the worst shop make the whole of a brand's standards");
        Check(BrandCatalogue.EntryFee.Length == BrandCatalogue.Tiers && BrandCatalogue.RoyaltyBp.Length == BrandCatalogue.Tiers && BrandCatalogue.LevyBp.Length == BrandCatalogue.Tiers
              && BrandCatalogue.LengthYears.Length == BrandCatalogue.Tiers, "three choices for each of the four terms");
        Check(BrandCatalogue.ChainFee.Length == BrandCatalogue.Chains && BrandCatalogue.ChainRoyaltyBp.Length == BrandCatalogue.Chains && BrandCatalogue.ChainRec.Length == BrandCatalogue.Chains
              && BrandCatalogue.ChainStandards.Length == BrandCatalogue.Chains && BrandCatalogue.ChainPriceLeast.Length == BrandCatalogue.Chains && BrandCatalogue.ChainPriceMost.Length == BrandCatalogue.Chains,
            "three city chains, each with every figure");
        bool dearer = true;
        for (int i = 1; i < BrandCatalogue.Chains; i++)
        {
            dearer &= BrandCatalogue.ChainFee[i] > BrandCatalogue.ChainFee[i - 1] && BrandCatalogue.ChainRec[i] > BrandCatalogue.ChainRec[i - 1] && BrandCatalogue.ChainStandards[i] > BrandCatalogue.ChainStandards[i - 1];
        }
        Check(dearer, "a dearer chain is better known and asks for higher standards");
        Check(BrandCatalogue.RecFootfallMost + BrandCatalogue.ToleranceMost > BrandCatalogue.FootfallMost - 1 && BrandCatalogue.FootfallMost == 15 && BrandCatalogue.FootfallMost <= 15,
            "a brand never asks for more than 15% more customers at a shop");
        Check(BrandCatalogue.Dice(7, 3, 9) == BrandCatalogue.Dice(7, 3, 9) && BrandCatalogue.Dice(7, 3, 9) >= 0 && BrandCatalogue.Dice(7, 3, 9) < 1000, "the dice give the same number for the same three, from 0 to 999");
        int low = 0;
        for (int i = 0; i < 4000; i++)
        {
            low += BrandCatalogue.Dice(99, i, 5) < 100 ? 1 : 0;
        }
        Check(low > 300 && low < 500, "one roll in ten is under a hundred, near enough (" + low + " of 4000)");
        Check(BrandCatalogue.Bp(5280, 600) == 317 && BrandCatalogue.Bp(25, 600) == 2 && BrandCatalogue.Bp(8, 600) == 0 && BrandCatalogue.Bp(-5280, 600) == -317, "a share in ten-thousandths rounds to the nearest whole unit, a half away from nothing");
    }

    private static void BrandStandards()
    {
        Check(BrandRules.ShopStandards(100, 100, 100, 100) == 100 && BrandRules.ShopStandards(0, 0, 0, 0) == 0, "a shop's standards run from 0 to 100");
        Check(BrandRules.ShopStandards(80, 60, 100, 40) == 73, "satisfaction 80, cleanliness 60, full shelves and skill 40 make 73 (got " + BrandRules.ShopStandards(80, 60, 100, 40) + ")");
        Check(BrandRules.ShopStandards(140, -5, 100, 100) == BrandRules.ShopStandards(100, 0, 100, 100), "a figure outside 0 to 100 is put inside it");
        Check(BrandRules.ShopStandards(80, 80, 50, 80) < BrandRules.ShopStandards(80, 80, 100, 80), "empty shelves lower a shop's standards");
        Check(BrandRules.ShopStandards(81, 80, 80, 80) >= BrandRules.ShopStandards(80, 80, 80, 80) && BrandRules.ShopStandards(80, 80, 80, 90) >= BrandRules.ShopStandards(80, 80, 80, 80), "no part lowers the figure by rising");
        Brand b;
        BrandsState s = NewBrands(4, out b, 80);
        Check(BrandRules.Standards(s, b) == 80, "four shops at 80 make a brand at 80");
        s.Members[3].Standards = 30;
        int dragged = BrandRules.Standards(s, b);
        Check(dragged == 53, "one shop at 30 among three at 80 drags the brand to 53: the mean is 68, the worst 30 (got " + dragged + ")");
        Check(dragged < (80 * 3 + 30) / 4, "one bad shop costs the brand more than its share of the mean");
        Franchise f = Signed(s, b, Temper.CornerCutting);
        f.True = 2000;
        Check(BrandRules.Standards(s, b) < dragged, "a franchised shop counts in the brand's standards with what it really is, inspected or not");
        var empty = new BrandsState();
        Check(BrandRules.Standards(empty, new Brand { Id = 9 }) == 0, "a brand with no shop has no standards");
    }

    private static void BrandRecognition()
    {
        Check(BrandRules.Gain(0, 500, 100, 0) == 0, "no shop in a district, no recognition gained there");
        Check(BrandRules.Gain(1, 0, 50, 0) == BrandCatalogue.PresencePerShop, "one shop with nobody in it and middling standards gains its presence alone");
        Check(BrandRules.Gain(9, 0, 50, 0) == BrandRules.Gain(4, 0, 50, 0) && BrandRules.Gain(4, 0, 50, 0) == 48, "presence counts four shops in a district and no more");
        Check(BrandRules.Gain(1, 400, 50, 0) == 12 + 16 && BrandRules.Gain(1, 4000, 50, 0) == 28, "customers add up to 16 a day, at 400 of them");
        Check(BrandRules.Gain(1, 0, 100, 0) == 26 && BrandRules.Gain(1, 0, 0, 0) == -2, "standards add 14 at 100 and take 14 away at nothing");
        Check(BrandRules.Gain(2, 200, 80, 150) == 24 + 8 + 8 + 150, "the parts add up, a campaign with them");
        Check(BrandRules.Step(0, 40, true) == 40 && BrandRules.Step(5000, 40, true) == 5000 + 20 - 25, "a gain counts for less the higher the figure stands, and half a per cent of it fades");
        Check(BrandRules.Step(10000, 500, true) == 9950 && BrandRules.Step(10000, 500, true) <= BrandCatalogue.RecMost, "the figure never passes 100");
        Check(BrandRules.Step(30, -200, true) == 0, "nor falls under nothing");
        Check(BrandRules.Step(4000, 0, false) == 4000 - 60 && BrandRules.Step(4000, 0, true) == 4000 - 20, "it fades three times as fast where the brand has no shop left");

        Brand b;
        BrandsState s = NewBrands(2, out b, 80, 100);
        int before = 0, days = 0;
        bool rising = true;
        for (int i = 0; i < 400; i++)
        {
            BrandRules.Day(s, b);
            int now = b.District(Midtown).Rec;
            rising &= now >= before;
            before = now;
            days++;
        }
        Check(rising, "with shops trading and no trouble a district's figure never falls on its way up");
        Check(before > 4150 && before < 4450, "two shops with good standards settle near 43 points (at " + before + " after " + days + " days)");
        Check(b.Age == 400, "the brand's own clock counts its days");
        Check(b.City > 500 && b.City < 560, "the city-wide figure settles at an eighth of the districts' figures added up (" + b.City + ")");

        Brand four;
        BrandsState busy = NewBrands(4, out four, 100, 200);
        for (int i = 0; i < 600; i++)
        {
            BrandRules.Day(busy, four);
        }
        int top = four.District(Midtown).Rec;
        Check(top > 5800 && top < 6200, "four busy shops with perfect standards settle higher, and still far short of 100 (" + top + ")");

        // The shops go: the figure fades.
        busy.Members.Clear();
        for (int i = 0; i < 60; i++)
        {
            BrandRules.Day(busy, four);
        }
        Check(four.District(Midtown).Rec < top * 45 / 100 && four.District(Midtown).Rec > 0, "sixty days after the last shop has gone the district remembers under half (" + four.District(Midtown).Rec + ")");

        Brand poor;
        BrandsState low = NewBrands(1, out poor, 10, 0);
        poor.District(Midtown).Rec = 3000;
        BrandRules.Day(low, poor);
        Check(poor.District(Midtown).Rec < 3000, "a brand with poor standards and no customers loses recognition even with a shop open");

        var known = new Brand { Id = 3, City = 6000 };
        Check(BrandRules.Effective(known, Kitchen) == 3000, "a brand the city knows counts for half of that where it has never traded");
        known.Districts.Add(new DistrictFile { Name = Kitchen, Rec = 4500 });
        Check(BrandRules.Effective(known, Kitchen) == 4500 && BrandRules.Best(known) == 4500, "a district's own figure counts where it is the higher");
        Check(BrandRules.District(known, Midtown, true).Rec == 3000 && known.Districts.Count == 2, "a district the brand comes to starts at half the city-wide figure");
        for (int i = 0; i < 40; i++)
        {
            BrandRules.District(known, "district " + i, true);
        }
        Check(known.Districts.Count == BrandCatalogue.DistrictsMost, "a brand keeps a figure for sixteen districts at most");
    }

    private static void BrandFootfall()
    {
        Check(BrandRules.RecFootfall(10000, 80) == 12 && BrandRules.RecFootfall(0, 80) == 0, "the name brings nothing unknown and 12% more customers at recognition of 100");
        Check(BrandRules.RecFootfall(5000, 80) == 6 && BrandRules.RecFootfall(4900, 80) == 6 && BrandRules.RecFootfall(6100, 80) == 7, "about 6% at 50 points and 7% at 61");
        Check(BrandRules.RecFootfall(10000, 39) == 6 && BrandRules.RecFootfall(10000, 40) == 12, "with standards under 40 the name is worth half");
        Check(BrandRules.RecFootfall(99999, 100) == 12, "recognition past 100 is counted as 100");
        bool rises = true;
        for (int r = 100; r <= 10000; r += 100)
        {
            rises &= BrandRules.RecFootfall(r, 80) >= BrandRules.RecFootfall(r - 100, 80);
        }
        Check(rises, "more recognition never brings fewer customers");
        Check(BrandRules.Tolerance(50) == 0 && BrandRules.Tolerance(100) == 4 && BrandRules.Tolerance(75) == 2 && BrandRules.Tolerance(20) == 0 && BrandRules.Tolerance(180) == 4, "a trusted brand gets back up to 4 points of what prices cost");
        Check(BrandRules.Forgiven(-10, 100) == 4 && BrandRules.Forgiven(-3, 100) == 3 && BrandRules.Forgiven(0, 100) == 0 && BrandRules.Forgiven(2, 100) == 0, "never more than prices cost: no loss, nothing given back");
        Check(BrandRules.Forgiven(-10, 50) == 0, "and nothing at all for a brand nobody trusts");
        Check(BrandRules.Cannibal(0) == 4 && BrandRules.Cannibal(75) == 2 && BrandRules.Cannibal(150) == 0 && BrandRules.Cannibal(4000) == 0 && BrandRules.Cannibal(-1) == 0, "a franchised shop in the books takes 4% next door, 2% at 75 m and nothing from 150 m");
        bool falls = true;
        for (int m = 1; m <= 200; m++)
        {
            falls &= BrandRules.Cannibal(m) <= BrandRules.Cannibal(m - 1);
        }
        Check(falls, "the further away, the less it takes");
        Check(BrandRules.Distance(0, 0, 30, 40) == 50 && BrandRules.Distance(-30, 10, 0, -30) == 50, "distance is the straight line in whole metres");
        Check(BrandRules.Footfall(12, 4, 0) == 15 && BrandRules.Footfall(12, 3, 0) == 15, "everything a brand asks for at a shop is kept to 15%");
        Check(BrandRules.Footfall(0, 0, 20) == BrandCatalogue.FootfallLeast, "and to 8% fewer at worst");
        Check(BrandRules.Footfall(6, 2, 3) == 5, "the name, what prices get back and what a near franchise takes are added up");

        Brand b;
        BrandsState s = NewBrands(2, out b, 90);
        b.District(Midtown).Rec = 5000;
        Member near = s.Members[0], far = s.Members[1];
        near.HasSpot = far.HasSpot = true;
        near.X = 0;
        far.X = 1000;
        near.PriceLoss = -6;
        Check(BrandRules.Apply(s, b, near) == 6 + 3 && near.Rec == 6 && near.Forgiven == 3 && near.Cannibal == 0, "a member shop gets the name's customers and some of what its prices cost");
        Franchise real = Signed(s, b, Temper.Careful);
        real.Hold = Hold.Opened;
        real.HasSpot = true;
        real.X = 10;
        Check(BrandRules.CannibalAll(s, near) == 0, "a franchised shop that is a business of the game's is left to the game's own competition");
        real.Hold = Hold.Books;
        Check(BrandRules.CannibalAll(s, near) == 4 && BrandRules.CannibalAll(s, far) == 0, "one in the books takes from the member shop beside it and not from one a kilometre off");
        for (int i = 0; i < 4; i++)
        {
            Franchise more = Signed(s, b, Temper.Careful);
            more.HasSpot = true;
            more.X = 5 + i;
        }
        Check(BrandRules.CannibalAll(s, near) == BrandCatalogue.CannibalMost, "all of them together take 8% at most");
        near.HasSpot = false;
        Check(BrandRules.CannibalAll(s, near) == 0, "a shop whose door could not be found is not counted near anything");

        // How it composes: a brand's share is added to the city's economy's and the company's, and the sum is what business pressure bounds.
        int boom = 6, campaign = 8, brand = BrandRules.Footfall(12, 0, 0);
        Check(Math.Min(15, boom + campaign + brand) == 15 && Math.Min(15, boom + campaign) == 14, "in a boom with a campaign running the sum is at its bound of 15 already: the brand adds one point, not twelve");
    }

    private static void BrandMembership()
    {
        var s = new BrandsState { Seed = 1 };
        Check(BrandRules.Create(s, "  ", CoffeeType, "Main~1", Midtown, 5) == null && BrandRules.Create(s, "Kettle", CoffeeType, null, Midtown, 5) == null, "a brand needs a name and a shop");
        Brand b = BrandRules.Create(s, "  Copper|Kettle; and a very long name that goes on  ", CoffeeType, "Main~1", Midtown, 5);
        Check(b != null && b.Name == "Copper Kettle  and a ver" && b.Name.Length <= BrandCatalogue.NameMost, "a name is cut to 24 characters and loses the save's dividers (\"" + (b != null ? b.Name : "") + "\")");
        Check(b.Flagship == "Main~1" && s.Members.Count == 1 && s.Members[0].BrandId == b.Id && b.Minimum == 60 && b.District(Midtown) != null, "the shop it was made from is its first member and its flagship; the minimum starts at 60");
        Check(BrandRules.Create(s, "Other", CoffeeType, "Main~1", Midtown, 5) == null, "a shop under a brand cannot found another");
        Check(!BrandRules.Join(s, b, "Main~2", "ba:businesstype_giftshop", Midtown, 6), "a shop of another type cannot join");
        Check(BrandRules.Join(s, b, "Main~2", CoffeeType, Garment, 6) && !BrandRules.Join(s, b, "Main~2", CoffeeType, Garment, 6) && b.District(Garment) != null, "a shop of the brand's type joins once, and its district gets a figure");
        Check(BrandRules.Leave(s, "Main~1") && b.Flagship == "Main~2" && !BrandRules.Leave(s, "Main~1"), "when the flagship leaves, the oldest member left is the flagship");
        Check(FranchiseRules.Take(s, "Main~2", 1, 7, 60) == null && FranchiseRules.Take(s, "Main~3", 1, 7, 60) != null && !BrandRules.Join(s, b, "Main~3", CoffeeType, Midtown, 8),
            "a shop is under a brand of the player's or under a city chain, never both");
        for (int i = 0; i < 10; i++)
        {
            BrandRules.Create(s, "Brand " + i, CoffeeType, "Side~" + i, Midtown, 9);
        }
        Check(s.Brands.Count == BrandCatalogue.BrandsMost, "a save keeps six brands at most");
        Signed(s, b, Temper.Careful);
        Someone(s, b, Temper.Careful);
        BrandRules.Dissolve(s, b);
        Check(s.Brand(b.Id) == null && s.Member("Main~2") == null && s.Franchises.Count == 0 && s.Applicants.Count == 0, "a brand given up takes its members' membership, its agreements and its applicants with it");
    }

    private static void BrandCampaigns()
    {
        Check(BrandRules.PushCost(BrandCatalogue.PushLocal, 1, 1000) == 600 && BrandRules.PushCost(BrandCatalogue.PushLocal, 3, 1000) == 1800 && BrandRules.PushCost(BrandCatalogue.PushLocal, 0, 1000) == 600,
            "a brand campaign in a district costs four lots of flyers for each shop there");
        Check(BrandRules.PushCost(BrandCatalogue.PushCity, 9, 1000) == 2400 && BrandRules.PushCost(BrandCatalogue.PushCity, 9, 1100) == 2640, "a city-wide one costs two radio spots, at today's prices");
        Check(BrandRules.PushDays(BrandCatalogue.PushLocal) == 3 && BrandRules.PushDays(BrandCatalogue.PushCity) == 6, "they run as long as the company's own flyers and radio spot");
        Brand b;
        BrandsState s = NewBrands(2, out b, 80, 100);
        Brand plain;
        BrandsState t = NewBrands(2, out plain, 80, 100);
        BrandRules.BookPush(b, BrandCatalogue.PushLocal, Midtown);
        Check(b.Push == BrandCatalogue.PushLocal && b.PushFrom == b.Age + 1 && !b.Pushing, "booked today, it runs from the brand's next day");
        int ran = 0, ahead = 0;
        for (int i = 0; i < 6; i++)
        {
            BrandRules.Day(s, b);
            BrandRules.Day(t, plain);
            int now = b.District(Midtown).Rec - plain.District(Midtown).Rec;
            ran += now - ahead > 50 ? 1 : 0;
            ahead = now;
        }
        Check(b.Push == -1 && b.District(Midtown).Rec - plain.District(Midtown).Rec > 400 && b.District(Midtown).Rec - plain.District(Midtown).Rec < 460,
            "three days of it leave the district about four and a half points better known (" + (b.District(Midtown).Rec - plain.District(Midtown).Rec) + " hundredths)");
        Check(ran == 3, "it ran for exactly its three days");
        b.Fund = 500;
        Check(BrandRules.FromFund(b, 300) == 300 && b.Fund == 200 && BrandRules.FromFund(b, 900) == 200 && b.Fund == 0, "the levy collected goes to a campaign first, as far as it reaches");
        BrandRules.District(b, Garment, true);
        int garment = b.District(Garment).Rec;
        BrandRules.BookPush(b, BrandCatalogue.PushCity, Midtown);
        BrandRules.Day(s, b);
        Check(b.District(Garment).Rec <= garment, "a city-wide campaign does nothing in a district where the brand has no shop");
    }

    private static void BrandScandals()
    {
        var b = new Brand { Id = 1, City = 2000 };
        b.Districts.Add(new DistrictFile { Name = Midtown, Rec = 5000 });
        Check(BrandRules.Hit(b, Midtown, Scandal.Inspection) == 400 && b.District(Midtown).Rec == 4600 && b.City == 1900, "a failed inspection costs the district 4 points and the city 1");
        Check(BrandRules.Hit(b, Midtown, Scandal.Strike) == 300 && BrandRules.Hit(b, Midtown, Scandal.Fire) == 200 && b.District(Midtown).Rec == 4100, "a strike costs 3 and a fire 2");
        Check(BrandRules.Hit(b, Garment, Scandal.Fire) == 0 && b.City == 1900 - 75 - 50 - 50, "where the brand has no figure the city still hears of it");
        b.District(Midtown).Rec = 100;
        b.City = 10;
        Check(BrandRules.Hit(b, Midtown, Scandal.Inspection) == 100 && b.District(Midtown).Rec == 0 && b.City == 0, "recognition never goes under nothing");
    }

    private static void FranchiseApplicants()
    {
        Applicant a = FranchiseRules.Generate(4242, 17, 1, 30, ThreeDistricts), again = FranchiseRules.Generate(4242, 17, 1, 30, ThreeDistricts);
        Check(a.Name == again.Name && a.Capital == again.Capital && a.Temper == again.Temper && a.Ability == again.Ability && a.District == again.District && a.Experience == again.Experience,
            "the same seed and number always make the same applicant");
        Check(a.Until == 40 && a.From == 0 && a.BrandId == 1, "an applicant waits ten days");
        bool bounded = true, names = true, all = true;
        var tempers = new int[3];
        var seen = new HashSet<string>();
        for (int i = 1; i <= 300; i++)
        {
            Applicant x = FranchiseRules.Generate(977, i, 1, 1, ThreeDistricts);
            int t = (int)x.Temper;
            tempers[t]++;
            bounded &= x.Capital >= BrandCatalogue.CapitalLeast[t] / 1000 * 1000 && x.Capital <= BrandCatalogue.CapitalMost[t] && x.Capital % 1000 == 0 && x.Experience >= 0 && x.Experience <= BrandCatalogue.ExperienceMost
                       && x.Ability >= 5 && x.Ability <= 100 && x.ShopsClosed <= x.ShopsRun && x.ShopsRun <= 5;
            names &= x.Name.Split(' ').Length == 3 && x.Name.IndexOf('|') < 0;
            all &= Array.IndexOf(ThreeDistricts, x.District) >= 0;
            seen.Add(x.Name);
        }
        Check(bounded, "every applicant's capital, experience, record and ability are inside their bounds");
        Check(names && seen.Count > 100, "each trades under a firm's name of three words, and the names vary (" + seen.Count + " in 300)");
        Check(all, "each wants one of the city's districts");
        Check(tempers[0] > 60 && tempers[1] > 60 && tempers[2] > 60, "all three temperaments turn up (" + tempers[0] + ", " + tempers[1] + ", " + tempers[2] + ")");
        Check(FranchiseRules.Generate(977, 5, 1, 1, null).District == string.Empty, "with no district to want, an applicant wants none (and nothing breaks)");
        Check(FranchiseRules.Generate(977, 5, 1, 1, ThreeDistricts).Name != FranchiseRules.Generate(978, 5, 1, 1, ThreeDistricts).Name
              || FranchiseRules.Generate(977, 5, 1, 1, ThreeDistricts).Capital != FranchiseRules.Generate(978, 5, 1, 1, ThreeDistricts).Capital, "another save's seed makes other applicants");

        Brand unknown = new Brand { Id = 1, Offered = true }, famous = new Brand { Id = 1, Offered = true, City = 8000 }, closed = new Brand { Id = 1, City = 8000 };
        int few = 0, many = 0, none = 0;
        for (int day = 1; day <= 2000; day++)
        {
            few += FranchiseRules.Arrives(4242, unknown, day) ? 1 : 0;
            many += FranchiseRules.Arrives(4242, famous, day) ? 1 : 0;
            none += FranchiseRules.Arrives(4242, closed, day) ? 1 : 0;
        }
        Check(few > 80 && few < 160, "an unknown brand on offer hears from somebody about six days in a hundred (" + few + " in 2000)");
        Check(many > few * 3 && many < 520, "a famous one about twenty-two (" + many + ")");
        Check(none == 0, "a brand that is not on offer hears from nobody");
        var s = new BrandsState();
        s.Applicants.Add(new Applicant { Id = 1, Until = 10 });
        s.Applicants.Add(new Applicant { Id = 2, Until = 12 });
        Check(FranchiseRules.Lapse(s, 10) == 0 && FranchiseRules.Lapse(s, 11) == 1 && s.Applicants.Count == 1 && s.Applicants[0].Id == 2, "an applicant is gone the day after their last");
    }

    private static void FranchiseTerms()
    {
        Check(FranchiseRules.Burden(0, 0, 0, false) == 0 && FranchiseRules.Burden(2, 2, 2, false) == 10 && FranchiseRules.Burden(1, 1, 1, false) == 5, "the burden of terms runs from 0 to 10; the middle terms are 5");
        Check(FranchiseRules.Burden(1, 1, 1, true) == 3 && FranchiseRules.Burden(0, 0, 0, true) == 0, "a district of their own is worth 2 of it to an applicant");
        Check(FranchiseRules.Bears(Temper.Careful, 0) == 3 && FranchiseRules.Bears(Temper.Careful, 5000) == 5 && FranchiseRules.Bears(Temper.Ambitious, 10000) == 8, "a better known brand may ask more, and the ambitious bear more");
        Brand b;
        BrandsState s = NewBrands(3, out b);
        b.District(Midtown).Rec = 5000;
        Applicant a = Someone(s, b, Temper.Careful);
        Check(FranchiseRules.Answer(s, b, a, 1, 1, 1, false, 1000) == Refusal.None, "at 50 points a careful applicant takes the middle terms");
        Check(FranchiseRules.Answer(s, b, a, 2, 2, 2, false, 1000) == Refusal.TooDear, "and not the hardest");
        Check(FranchiseRules.Answer(s, b, a, 2, 1, 0, true, 1000) == Refusal.None, "the top entry fee goes down with a district of their own");
        b.District(Midtown).Rec = 0;
        b.City = 0;
        Check(FranchiseRules.Answer(s, b, a, 1, 1, 1, false, 1000) == Refusal.TooDear && FranchiseRules.Answer(s, b, a, 0, 1, 1, false, 1000) == Refusal.None, "an unknown brand must ask less");
        b.District(Midtown).Rec = 5000;
        a.Capital = 30000;
        Check(FranchiseRules.Answer(s, b, a, 1, 0, 0, false, 1000) == Refusal.NoCapital && FranchiseRules.Answer(s, b, a, 0, 0, 0, false, 1000) == Refusal.None, "the entry fee and the fit-out must be within their capital (15,000 and 20,000 against 30,000)");
        Check(FranchiseRules.EntryFee(a, 1, 1000) == 15000 && FranchiseRules.EntryFee(a, 1, 1200) == 18000 && FranchiseRules.EntryFee(a, 7, 1000) == 25000, "the entry fee follows prices; a tier out of range is the nearest");
        a.Capital = 100000;
        Franchise f = FranchiseRules.Sign(s, b, a, 1, 1, 1, true, 1, 1000, 20, 60);
        Check(f != null && s.Franchises.Count == 1 && s.Applicants.Count == 0, "signed: the applicant is a franchisee");
        Check(f.EntryFee == 15000 && f.RoyaltyBp == 600 && f.LevyBp == 100 && f.Exclusive && f.Signed == 20 && f.Term == 120 && f.Until == 140, "the agreement holds the terms: two years of sixty days from day 20");
        Check(f.Health == 100000 - 15000 - 20000, "what is left of their capital after the fee and the fit-out is their cash");
        Check(f.StandardsNow == FranchiseRules.Target(f, 20) && f.Seen == -1 && f.SeenDay == 20, "their standards start where their temperament puts them; nobody has inspected yet");
        Applicant other = Someone(s, b, Temper.Ambitious);
        Check(FranchiseRules.Answer(s, b, other, 0, 0, 0, false, 1000) == Refusal.Territory, "a district held alone is shut to the next applicant");
        Applicant elsewhere = Someone(s, b, Temper.Ambitious, 100000, 60, Garment);
        Check(FranchiseRules.Answer(s, b, elsewhere, 0, 0, 0, false, 1000) == Refusal.None, "the next district is open");
        f.Exclusive = false;
        Check(FranchiseRules.Answer(s, b, other, 0, 0, 0, true, 1000) == Refusal.Territory && FranchiseRules.Answer(s, b, other, 0, 0, 0, false, 1000) == Refusal.None,
            "nobody is given a district alone while another franchisee trades there");
        while (s.Franchises.Count < BrandCatalogue.FranchisesMost)
        {
            s.Franchises.Add(new Franchise { Id = s.NextId++, BrandId = 99 });
        }
        Check(FranchiseRules.Answer(s, b, elsewhere, 0, 0, 0, false, 1000) == Refusal.Full, "sixty franchised shops is as many as a save keeps");
    }

    private static void FranchiseTakings()
    {
        Check(FranchiseRules.Ordinary(150, 4000) == 6000 && FranchiseRules.Ordinary(0, 4000) == 0 && FranchiseRules.Ordinary(-5, 4000) == 0, "an ordinary day: 150 customers at 40.00 each take 6,000");
        int a = FranchiseRules.Takings(150, 4000, 5000, 100, CycleState.Steady, 60, 70, 1, 31, 12);
        Check(a == FranchiseRules.Takings(150, 4000, 5000, 100, CycleState.Steady, 60, 70, 1, 31, 12), "the same shop and day always take the same");
        Check(a > 4850 && a < 5700, "a middling franchisee under a brand at 50 points takes about 5,280, give or take the day (" + a + ")");
        long sum = 0;
        int least = int.MaxValue, most = 0;
        for (int day = 1; day <= 1000; day++)
        {
            int t = FranchiseRules.Takings(150, 4000, 5000, 100, CycleState.Steady, 60, 70, 1, 31, day);
            sum += t;
            least = Math.Min(least, t);
            most = Math.Max(most, t);
        }
        int mean = (int)(sum / 1000);
        Check(mean > 5200 && mean < 5360, "over a thousand days the average is 5,280 within a per cent and a half (" + mean + ")");
        Check(least >= 5278 * 920 / 1000 - 6 && most <= 5278 * 1081 / 1000 + 6, "no day is more than 8% off it (" + least + " to " + most + ")");
        bool up = true;
        for (int v = 1; v <= 100; v++)
        {
            up &= FranchiseRules.Takings(150, 4000, 5000, 100, CycleState.Steady, v, 70, 1, 31, 12) >= FranchiseRules.Takings(150, 4000, 5000, 100, CycleState.Steady, v - 1, 70, 1, 31, 12);
            up &= FranchiseRules.Takings(150, 4000, 5000, 100, CycleState.Steady, 60, v, 1, 31, 12) >= FranchiseRules.Takings(150, 4000, 5000, 100, CycleState.Steady, 60, v - 1, 1, 31, 12);
            up &= FranchiseRules.Takings(150, 4000, v * 100, 100, CycleState.Steady, 60, 70, 1, 31, 12) >= FranchiseRules.Takings(150, 4000, (v - 1) * 100, 100, CycleState.Steady, 60, 70, 1, 31, 12);
            up &= FranchiseRules.Takings(150, 4000, 5000, 60 + v, CycleState.Steady, 60, 70, 1, 31, 12) >= FranchiseRules.Takings(150, 4000, 5000, 59 + v, CycleState.Steady, 60, 70, 1, 31, 12);
            up &= FranchiseRules.Takings(100 + v, 4000, 5000, 100, CycleState.Steady, 60, 70, 1, 31, 12) >= FranchiseRules.Takings(99 + v, 4000, 5000, 100, CycleState.Steady, 60, 70, 1, 31, 12);
            up &= FranchiseRules.Takings(150, 3000 + v * 20, 5000, 100, CycleState.Steady, 60, 70, 1, 31, 12) >= FranchiseRules.Takings(150, 2980 + v * 20, 5000, 100, CycleState.Steady, 60, 70, 1, 31, 12);
        }
        Check(up, "takings never fall when ability, standards, recognition, the district's income, the customers or their spending rise");
        Check(FranchiseRules.Takings(150, 4000, 5000, 100, CycleState.Steady, 100, 70, 1, 31, 12) > FranchiseRules.Takings(150, 4000, 5000, 100, CycleState.Steady, 0, 70, 1, 31, 12) * 18 / 10,
            "the best franchisee takes near twice what the worst does in the same shop");
        bool crowd = true;
        for (int n = 1; n <= 8; n++)
        {
            crowd &= FranchiseRules.Takings(150, 4000, 5000, 100, CycleState.Steady, 60, 70, n, 31, 12) <= FranchiseRules.Takings(150, 4000, 5000, 100, CycleState.Steady, 60, 70, n - 1, 31, 12);
        }
        Check(crowd && FranchiseRules.Takings(150, 4000, 5000, 100, CycleState.Steady, 60, 70, 5, 31, 12) == FranchiseRules.Takings(150, 4000, 5000, 100, CycleState.Steady, 60, 70, 50, 31, 12),
            "each other shop of the brand in the district takes 6% of them, 30% at most");
        int boom = FranchiseRules.Takings(150, 4000, 5000, 100, CycleState.Boom, 60, 70, 1, 31, 12), slow = FranchiseRules.Takings(150, 4000, 5000, 100, CycleState.Slowdown, 60, 70, 1, 31, 12),
            bust = FranchiseRules.Takings(150, 4000, 5000, 100, CycleState.Recession, 60, 70, 1, 31, 12);
        Check(boom > a && a > slow && slow > bust, "a boom takes more than a steady economy, a slowdown less, a recession least");
        Check(FranchiseRules.Takings(0, 4000, 5000, 100, CycleState.Steady, 60, 70, 1, 31, 12) == 0 && FranchiseRules.Takings(150, 0, 5000, 100, CycleState.Steady, 60, 70, 1, 31, 12) == 0, "no customers or no spending: nothing taken");
        Check(FranchiseRules.Takings(2000000, 2000000, 99999, 999, CycleState.Boom, 999, 999, -5, 31, 12) > 0, "the largest figures do not run over");
    }

    private static void FranchiseMoney()
    {
        Check(FranchiseRules.Royalty(5280, 600) == 317 && FranchiseRules.Royalty(5280, 100) == 53 && FranchiseRules.Royalty(5280, 0) == 0, "6% of 5,280 is 317 and 1% is 53, to the nearest whole unit");
        Check(FranchiseRules.Royalty(5283, 400) == 211 && FranchiseRules.Royalty(5288, 400) == 212 && FranchiseRules.Royalty(-400, 600) == 0, "a half goes up; nothing is owed on a day that took nothing");
        Check(FranchiseRules.Support(5280, 1000) == 40 + 53 && FranchiseRules.Support(0, 1000) == 40 && FranchiseRules.Support(5280, 1250) == 50 + 53, "support is 40 at today's prices and 1% of the takings");
        int net = FranchiseRules.Royalty(5280, 600) + FranchiseRules.Royalty(5280, 100) - FranchiseRules.Support(5280, 1000);
        Check(net == 277, "the middle terms leave the brand 277 a day from such a shop (got " + net + ")");
        var b = new Brand { Id = 1 };
        for (int i = 0; i < 60; i++)
        {
            FranchiseRules.NoteNet(b, 700);
        }
        Check(b.Net >= 696 && b.Net <= 700, "the week's average of what the franchises bring in settles at what they bring (" + b.Net + ")");
        b.City = 3000;
        Check(FranchiseRules.BidPrice(b, 1000) == b.Net * 120 + 400 * 30, "a bid for the brand is 120 days of that and 400 for each point of city-wide recognition");
        b.Net = -50;
        Check(FranchiseRules.BidPrice(b, 1000) == 12000, "franchises that cost more than they bring add nothing to a bid");
    }

    private static void FranchiseDays()
    {
        Brand b;
        BrandsState s = NewBrands(3, out b, 80, 150, 6000);
        b.District(Midtown).Rec = 5000;
        Check(b.Ticket == 4000, "what a customer spends is the members' takings over their customers (" + b.Ticket + " hundredths)");
        Franchise f = Signed(s, b, Temper.Careful, 10, 100000, 60);
        int cash = f.Health;
        FranchiseDay r = FranchiseRules.Day(s, b, f, 11, 100, CycleState.Steady, 1000);
        Check(r.Takings > 3800 && r.Takings < 5400 && r.Customers == r.Takings * 100 / 4000, "a day's takings, and the customers that makes at the brand's spending (" + r.Takings + ")");
        Check(r.Royalty == FranchiseRules.Royalty(r.Takings, 600) && r.Levy == FranchiseRules.Royalty(r.Takings, 100) && r.Support == FranchiseRules.Support(r.Takings, 1000), "the royalty, the levy and the support are those of the day's takings");
        Check(f.Health == cash + BrandCatalogue.Bp(r.Takings, 2200) - r.Royalty - r.Levy - BrandCatalogue.Bp(6000, 1200), "the franchisee keeps 22% of takings, less the brand's share and 12% of an ordinary day for the fixed costs");
        Check(f.Takings == r.Takings && f.Royalty == r.Royalty + r.Levy && f.Paid == r.Royalty + r.Levy && r.Event == DayEvent.None, "the shop's own page shows the day");

        // Deterministic: the same save replays the same year.
        Brand b2;
        BrandsState s2 = NewBrands(3, out b2, 80, 150, 6000);
        b2.District(Midtown).Rec = 5000;
        Franchise f2 = Signed(s2, b2, Temper.Careful, 10, 100000, 60);
        FranchiseRules.Day(s2, b2, f2, 11, 100, CycleState.Steady, 1000);
        bool same = f2.Health == f.Health && f2.True == f.True;
        for (int day = 12; day < 60; day++)
        {
            FranchiseDay x = FranchiseRules.Day(s, b, f, day, 100, CycleState.Steady, 1000), y = FranchiseRules.Day(s2, b2, f2, day, 100, CycleState.Steady, 1000);
            same &= x.Takings == y.Takings && x.Event == y.Event && f.Health == f2.Health && f.True == f2.True;
        }
        Check(same, "two saves with the same seed live the same fifty days, to the unit");

        // Waived: nothing comes in, support still goes out.
        f.Ask = Ask.None;
        FranchiseRules.Waive(b, f, 60);
        FranchiseDay waived = FranchiseRules.Day(s, b, f, 61, 100, CycleState.Steady, 1000);
        Check(waived.Royalty == 0 && waived.Levy == 0 && waived.Support > 0 && f.WaivedUntil == 74, "while the royalty is waived nothing comes in for fourteen days, and the support is still paid");

        // A weak franchisee on hard terms runs out of money; a strong one on fair terms does not.
        Brand hard;
        BrandsState hs = NewBrands(3, out hard, 80, 150, 6000);
        Franchise weak = Signed(hs, hard, Temper.CornerCutting, 1, 60000, 15, 2, 2);
        weak.Until = 100000;
        int failedOn = 0, askedOn = 0;
        for (int day = 2; day < 900 && failedOn == 0; day++)
        {
            FranchiseDay x = FranchiseRules.Day(hs, hard, weak, day, 90, CycleState.Slowdown, 1000);
            askedOn = askedOn == 0 && x.Event == DayEvent.AsksHelp ? day : askedOn;
            failedOn = x.Event == DayEvent.Failed ? day : 0;
        }
        Check(askedOn > 0 && failedOn > askedOn, "a weak franchisee on the hardest terms in a slowdown first asks for help (day " + askedOn + ") and then closes (day " + failedOn + ")");
        Brand fair;
        BrandsState fs = NewBrands(3, out fair, 80, 150, 6000);
        fair.District(Midtown).Rec = 6000;
        Franchise strong = Signed(fs, fair, Temper.Careful, 1, 100000, 90, 1, 1);
        strong.Until = 100000;
        bool alive = true;
        for (int day = 2; day < 900; day++)
        {
            alive &= FranchiseRules.Day(fs, fair, strong, day, 100, CycleState.Steady, 1000).Event != DayEvent.Failed;
        }
        Check(alive && strong.Health > 65000, "an able one on the middle terms is still trading two and a half years on, and richer (" + strong.Health + ")");
    }

    private static void FranchiseStandardsKept()
    {
        Brand b;
        BrandsState s = NewBrands(3, out b);
        Franchise careful = Signed(s, b, Temper.Careful, 1, 100000, 70), cutter = Signed(s, b, Temper.CornerCutting, 1, 100000, 40);
        Check(FranchiseRules.Target(careful, 1) == 86 && FranchiseRules.Target(cutter, 1) == 50, "a careful franchisee of ability 70 heads for 86, a corner-cutter of ability 40 for 50");
        careful.Until = cutter.Until = 100000;
        for (int day = 2; day < 120; day++)
        {
            FranchiseRules.Day(s, b, careful, day, 100, CycleState.Steady, 1000);
            FranchiseRules.Day(s, b, cutter, day, 100, CycleState.Steady, 1000);
        }
        Check(careful.StandardsNow >= 80 && careful.StandardsNow <= 92, "after four months the careful one keeps standards in the eighties (" + careful.StandardsNow + ")");
        Check(cutter.StandardsNow >= 40 && cutter.StandardsNow <= 58, "and the corner-cutter's are about 50 (" + cutter.StandardsNow + ")");
        Check(cutter.Seen == -1 && !FranchiseRules.Evidence(b, cutter, 120), "nobody knows that until somebody looks");
        Check(FranchiseRules.InspectionDue(cutter, 31) && !FranchiseRules.InspectionDue(cutter, 30), "an inspection is due thirty days after the last (or after signing)");
        int found = FranchiseRules.Inspect(cutter, 120);
        Check(found == cutter.StandardsNow && cutter.Seen == found && cutter.SeenDay == 120 && !FranchiseRules.InspectionDue(cutter, 149), "an inspection finds what the standards are that day");
        Check(FranchiseRules.Evidence(b, cutter, 120) && FranchiseRules.Evidence(b, cutter, 130) && !FranchiseRules.Evidence(b, cutter, 131), "under the brand's minimum of 60: evidence for ten days");
        b.Minimum = 0;
        Check(!FranchiseRules.Evidence(b, cutter, 120) && !FranchiseRules.CanWarn(b, cutter, 120), "a brand with no required minimum has nothing to warn anybody about");
        b.Minimum = 60;
        FranchiseRules.Inspect(careful, 120);
        Check(!FranchiseRules.CanWarn(b, careful, 120) && FranchiseRules.CanWarn(b, cutter, 120), "only a shop found under the minimum can be warned");
        int target = FranchiseRules.Target(cutter, 121);
        FranchiseRules.Warn(cutter, 120);
        Check(cutter.Warnings == 1 && cutter.WarnedUntil == 140 && FranchiseRules.Target(cutter, 121) == target + 10 && FranchiseRules.Target(cutter, 140) == target, "a warning lifts where the standards head by 10 for twenty days");
        Check(!FranchiseRules.CanWarn(b, cutter, 125), "no second warning while the first still runs");
        Check(!FranchiseRules.HasCause(b, cutter, 125), "one warning is no cause to end an agreement");
        int before = cutter.True, ability = cutter.Ability;
        FranchiseRules.Retrain(cutter, 125);
        Check(cutter.True == before + 1500 && cutter.RetrainedUntil == 155 && cutter.Ability == ability + 3, "retraining gives fifteen points at once and a little ability for good");
        Check(FranchiseRules.RetrainFee(1000) == 1500 && FranchiseRules.InspectorFee(1000) == 250 && FranchiseRules.InspectorFee(1200) == 300, "retraining costs 1,500 and an inspector 250, at today's prices");
        cutter.True = 9500;
        FranchiseRules.Retrain(cutter, 126);
        Check(cutter.True == 10000, "standards never pass 100");
        cutter.True = 4000;
        FranchiseRules.Inspect(cutter, 141);
        FranchiseRules.Warn(cutter, 141);
        FranchiseRules.Inspect(cutter, 150);
        Check(cutter.Warnings == 2 && FranchiseRules.HasCause(b, cutter, 150), "two warnings and a fresh inspection that still finds it under the minimum: cause");
        Check(!FranchiseRules.HasCause(b, cutter, 161), "an inspection eleven days old is cause no longer");
    }

    private static void FranchiseEndings()
    {
        Brand b;
        BrandsState s = NewBrands(3, out b);
        b.District(Midtown).Rec = 5000;
        b.City = 2000;
        Franchise f = Signed(s, b, Temper.Careful, 10);
        Check(FranchiseRules.Compensation(b, f, 10) == 60 * 300 && FranchiseRules.Compensation(b, f, 60) == 10 * 300 + 4500, "ending without cause costs 5% of an ordinary day for each day left, sixty at most, and half the entry fee at least");
        Check(FranchiseRules.Compensation(b, f, 500) == 7500, "even on its last day it costs half the entry fee");
        Check(FranchiseRules.End(s, b, f, false, 30) == 600 && b.District(Midtown).Rec == 4400 && b.City == 1850 && s.Franchises.Count == 0, "and 6 points in the district and 1.5 city-wide");
        Franchise g = Signed(s, b, Temper.Careful, 10);
        Check(FranchiseRules.End(s, b, g, true, 30) == 0 && b.District(Midtown).Rec == 4400 && s.Franchises.Count == 0, "ended with cause, nobody thinks less of the brand");
        Franchise h = Signed(s, b, Temper.Ambitious, 10);
        b.Bid = 5000;
        b.BidBy = h.Id;
        h.Ask = Ask.BuyBrand;
        Check(FranchiseRules.Fail(s, b, h, 40) == 800 && b.District(Midtown).Rec == 3600 && b.City == 1650 && b.Failed == 1 && b.Bid == 0, "a franchisee that closes costs 8 points in the district and 2 city-wide, and its bid goes with it");

        // Running out: a renewal is asked for fourteen days before, once; unanswered, the agreement lapses on its day.
        Franchise r = Signed(s, b, Temper.Careful, 10);
        r.Health = 500000;
        DayEvent asked = DayEvent.None, ended = DayEvent.None;
        int askedDay = 0, endedDay = 0, asks = 0;
        for (int day = 11; day <= 80 && ended == DayEvent.None; day++)
        {
            FranchiseDay x = FranchiseRules.Day(s, b, r, day, 100, CycleState.Steady, 1000);
            if (x.Event == DayEvent.AsksRenewal)
            {
                asked = x.Event;
                askedDay = day;
                asks++;
            }
            if (x.Event == DayEvent.Lapsed)
            {
                ended = x.Event;
                endedDay = day;
            }
        }
        Check(asked == DayEvent.AsksRenewal && askedDay == 70 - 14 && asks == 1 && r.AskAmount == 3750, "a renewal is asked for once, fourteen days before the end, at a quarter of the entry fee");
        Check(ended == DayEvent.Lapsed && endedDay == 70, "unanswered, the agreement runs out on its day");
        FranchiseRules.Lapse(s, b, r, 70);
        Check(s.Franchises.Count == 0 && b.District(Midtown).Rec == 3600, "a shop that leaves when its agreement runs out costs the brand no recognition");
        Franchise again = Signed(s, b, Temper.Careful, 10);
        again.Ask = Ask.Renewal;
        FranchiseRules.Renew(again);
        Check(again.Until == 70 + 60 && again.Ask == Ask.None && again.Term == 60, "renewed, it runs for the same length again");
    }

    private static void FranchiseAsks()
    {
        Brand b;
        BrandsState s = NewBrands(3, out b);
        b.District(Midtown).Rec = 5000;
        Franchise f = Signed(s, b, Temper.Careful, 10);
        f.Until = 100000;
        f.Health = 6000;   // one ordinary day's takings in hand
        FranchiseDay x = FranchiseRules.Day(s, b, f, 11, 100, CycleState.Steady, 1000);
        Check(x.Event == DayEvent.AsksHelp && f.Ask == Ask.Help && f.AskAmount == 30000 && f.AskUntil == 21 && f.Trouble, "with under two ordinary days' takings in hand a franchisee asks for five days' worth");
        Check(FranchiseRules.Target(f, 12) == 82 + 2 - BrandCatalogue.TroubleDrop, "and its standards head 8 points lower while it is short");
        Check(FranchiseRules.Day(s, b, f, 12, 100, CycleState.Steady, 1000).Event == DayEvent.None, "while an ask waits nothing else is asked");
        int cash = f.Health;
        FranchiseRules.Help(b, f, f.AskAmount, 12);
        Check(f.Health == cash + 30000 && f.Ask == Ask.None && f.AskRest == 42, "a grant goes into its cash, and it asks nothing more for thirty days");
        f.Health = 6000;
        bool quiet = true;
        for (int day = 13; day < 42; day++)
        {
            quiet &= FranchiseRules.Day(s, b, f, day, 100, CycleState.Steady, 1000).Event == DayEvent.None;
        }
        Check(quiet, "true to that even though it is short again");
        f.Health = 6000;
        Check(FranchiseRules.Day(s, b, f, 42, 100, CycleState.Steady, 1000).Event == DayEvent.AsksHelp, "then it asks again");
        bool dropped = true;
        for (int day = 43; day <= 53; day++)
        {
            f.Health = 500000;
            dropped &= f.Ask == Ask.Help;
            FranchiseRules.Day(s, b, f, day, 100, CycleState.Steady, 1000);
        }
        Check(dropped && f.Ask == Ask.None, "an ask that is not answered in ten days is dropped");

        // A second shop.
        Brand b2;
        BrandsState s2 = NewBrands(3, out b2);
        b2.District(Midtown).Rec = 5000;
        Franchise rich = Signed(s2, b2, Temper.Ambitious, 1, 160000, 80);
        rich.Until = 100000;
        int second = 0;
        for (int day = 2; day < 200 && second == 0; day++)
        {
            second = FranchiseRules.Day(s2, b2, rich, day, 100, CycleState.Steady, 1000).Event == DayEvent.AsksSecond ? day : 0;
        }
        Check(second >= 31, "a franchisee with cash in hand asks for a second shop, never in its first thirty days (day " + second + ")");
        int had = rich.Health;
        Applicant a = FranchiseRules.Second(s2, b2, rich, second);
        Check(a != null && a.From == rich.Id && a.Capital == had / 2 && a.District == rich.District && a.Name == rich.Name && rich.Ask == Ask.None, "agreed, the franchisee joins the applicants with half its cash, for the same district");
        Check(FranchiseRules.EntryFee(a, 1, 1000) == 7500, "a second shop pays half the entry fee");
        Check(FranchiseRules.Answer(s2, b2, a, 1, 1, 1, false, 1000) == Refusal.None, "and it is not shut out by its own first shop");
        rich.Exclusive = true;
        Check(FranchiseRules.Answer(s2, b2, a, 1, 1, 1, true, 1000) == Refusal.None, "not even where it holds the district alone");
        Franchise shop2 = FranchiseRules.Sign(s2, b2, a, 1, 1, 1, false, 0, 1000, second, 60);
        Check(shop2 != null && rich.Health == had - had / 2 && shop2.Health == had / 2 - 7500 - 20000 && s2.Franchises.Count == 2, "the second shop's capital comes out of the first one's cash");
        while (s2.Applicants.Count < BrandCatalogue.ApplicantsMost)
        {
            Someone(s2, b2, Temper.Careful);
        }
        rich.Ask = Ask.SecondShop;
        Check(FranchiseRules.Second(s2, b2, rich, second) == null && rich.Ask == Ask.SecondShop, "with four applicants waiting there is no room: the ask stands");

        // A dispute.
        Brand b3;
        BrandsState s3 = NewBrands(3, out b3);
        b3.District(Midtown).Rec = 9000;
        Franchise cross = Signed(s3, b3, Temper.CornerCutting, 1, 500000, 60, 2, 2);
        cross.Until = 100000;
        int disputed = 0;
        for (int day = 2; day < 600 && disputed == 0; day++)
        {
            cross.Health = 500000;
            disputed = FranchiseRules.Day(s3, b3, cross, day, 100, CycleState.Steady, 1000).Event == DayEvent.Disputes ? day : 0;
        }
        Check(disputed > 0 && cross.Ask == Ask.Dispute && cross.AskAmount == (480 + 120) * 30, "a corner-cutter on the top royalty disputes the terms in time, for thirty days of royalty and levy (day " + disputed + ", " + cross.AskAmount + ")");
        Check(FranchiseRules.Settle(b3, cross, disputed) && cross.RoyaltyBp == 600 && cross.LevyBp == 200 && cross.Ask == Ask.None, "settled by giving way: the royalty goes down a step");
        Franchise calm = Signed(s3, b3, Temper.Careful, 1, 500000, 60, 2, 2, Garment);
        calm.Until = 100000;
        bool never = true;
        for (int day = 2; day < 600; day++)
        {
            calm.Health = 30000;
            never &= FranchiseRules.Day(s3, b3, calm, day, 100, CycleState.Steady, 1000).Event != DayEvent.Disputes;
        }
        Check(never, "a careful franchisee does not dispute");
        var low = new Franchise { RoyaltyBp = 400, LevyBp = 0, Ask = Ask.Dispute };
        Check(!FranchiseRules.Settle(b3, low, 5) && low.Ask == Ask.Dispute, "at the lowest royalty and no levy there is nothing left to give");
        low.True = 6000;
        low.Suit = 7;
        FranchiseRules.Stand(b3, low, 5);
        Check(low.True == 5500 && low.Ask == Ask.None && low.Suit == 0, "a dispute turned down, or dealt with by the court, costs five points of the shop's standards");
        var idle = new Brand { Id = 5, Fund = 5001 };
        var plain = new Franchise { RoyaltyBp = 600 };
        Check(FranchiseRules.Grievance(idle, plain, 1000) && !FranchiseRules.Grievance(new Brand { Id = 6, Fund = 5000 }, plain, 1000), "levy collected and left unspent past 5,000 is a grievance");

        // A bid for the brand.
        Brand b4;
        BrandsState s4 = NewBrands(3, out b4);
        b4.City = 4000;
        b4.Net = 900;
        Franchise bidder = Signed(s4, b4, Temper.Ambitious, 1, 160000, 90);
        bidder.Until = 100000;
        int bid = 0;
        for (int day = 2; day < 1500 && bid == 0; day++)
        {
            bidder.Health = 200000;
            FranchiseDay y = FranchiseRules.Day(s4, b4, bidder, day, 100, CycleState.Steady, 1000);
            if (y.Event == DayEvent.AsksSecond)
            {
                FranchiseRules.Drop(b4, bidder, day);
            }
            bid = y.Event == DayEvent.Bids ? day : 0;
        }
        Check(bid >= 91 && b4.Bid == 900 * 120 + 400 * 40 && b4.BidBy == bidder.Id && b4.BidUntil == bid + 10 && bidder.Ask == Ask.BuyBrand, "an ambitious franchisee bids for the brand, never in its first ninety days (day " + bid + ", " + b4.Bid + ")");
        FranchiseRules.Drop(b4, bidder, bid + 1);
        Check(b4.Bid == 0 && b4.BidBy == 0 && bidder.Ask == Ask.None, "turned down, the bid is withdrawn");
    }

    private static void FranchiseChains()
    {
        Check(FranchiseRules.ChainFootfall(0) == 5 && FranchiseRules.ChainFootfall(1) == 7 && FranchiseRules.ChainFootfall(2) == 8, "the three chains bring 5%, 7% and 8% more customers from the first day");
        Check(FranchiseRules.ChainFee(0, 1000) == 4000 && FranchiseRules.ChainFee(2, 1100) == 17600 && FranchiseRules.ChainFee(9, 1000) == 16000, "their fees follow prices");
        Check(!FranchiseRules.ChainBreach(1, 60, 1000) && FranchiseRules.ChainBreach(1, 59, 1000), "the middle chain asks for standards of 60");
        Check(FranchiseRules.ChainBreach(1, 80, 1101) && FranchiseRules.ChainBreach(1, 80, 949) && !FranchiseRules.ChainBreach(1, 80, 1100) && !FranchiseRules.ChainBreach(1, 80, 950), "and prices from 95% to 110% of the game's own");
        Check(!FranchiseRules.ChainBreach(1, 80, 0), "a shop with no prices to judge by is not in breach for them");
        var s = new BrandsState();
        Taken t = FranchiseRules.Take(s, "Main~1", 1, 10, 60);
        Check(t != null && t.Until == 70 && FranchiseRules.Take(s, "Main~1", 0, 10, 60) == null, "joined for a year; a shop is under one chain at a time");
        Check(FranchiseRules.ChainRoyalty(t, 6000) == 360 && t.Paid == 360, "the middle chain takes 6% of the shop's takings");
        for (int i = 0; i < 40; i++)
        {
            FranchiseRules.ChainRoyalty(t, 6000);
        }
        Check(t.Average >= 357 && t.Average <= 360, "the week's average of the royalty settles at a day's royalty (" + t.Average + ")");
        Check(FranchiseRules.ChainExit(t, 1000) == t.Average * 20 && FranchiseRules.ChainExit(new Taken { Chain = 1 }, 1000) == 4500, "leaving costs twenty days of royalty, and half the fee at least");
        var steps = new List<int>();
        for (int day = 11; day <= 24; day++)
        {
            steps.Add(FranchiseRules.ChainStep(t, true, day, 60));
        }
        Check(steps[2] == 1 && steps[6] == 2 && steps[13] == 3 && steps[0] == 0 && steps[3] == 0 && steps[12] == 0, "three days in breach and the chain writes, seven and it fines, fourteen and it ends the agreement");
        Check(!FranchiseRules.ChainGives(t) && FranchiseRules.ChainFine(t, 1000) == t.Average * 5, "from the day it writes the name brings nothing; the fine is five days of royalty");
        Check(FranchiseRules.ChainStep(t, false, 25, 60) == 0 && t.Breach == 0 && FranchiseRules.ChainGives(t), "a day back inside clears the count");
        Check(FranchiseRules.ChainFine(new Taken(), 1000) == BrandCatalogue.ChainFineLeast, "a fine is never under 200");
        Check(FranchiseRules.ChainFine(new Taken(), 2000) == CompanyCatalogue.AtPrices(BrandCatalogue.ChainFineLeast, 2000), "a chain's least fine follows prices");
        FranchiseRules.ChainStep(t, false, 70, 60);
        Check(t.Until == 130, "the year renews itself");
        for (int i = 2; i <= 20; i++)
        {
            FranchiseRules.Take(s, "Main~" + i, 0, 10, 60);
        }
        Check(s.Taken.Count == BrandCatalogue.TakenMost, "eight shops under city chains at most");
    }

    private static void BrandSave()
    {
        Brand b;
        BrandsState s = NewBrands(3, out b);
        s.LastDay = 77;
        b.District(Midtown).Rec = 5123;
        BrandRules.District(b, Garment, true).Rec = 801;
        b.City = 1500;
        b.Offered = true;
        b.Minimum = 70;
        b.Fund = 420;
        b.Net = -35;
        BrandRules.BookPush(b, BrandCatalogue.PushLocal, Garment);
        Franchise f = Signed(s, b, Temper.Ambitious, 12, 120000, 71, 2, 0, Garment, true);
        f.Place = "5th%20Avenue~12";
        f.GameName = "A shop; with | odd ^ marks";
        f.Hold = Hold.Opened;
        f.Born = 12;
        f.X = -350;
        f.Z = 1200;
        f.HasSpot = true;
        f.Health = -4000;
        f.Ask = Ask.Dispute;
        f.AskUntil = 30;
        f.AskAmount = 9000;
        f.Suit = 4;
        f.Warnings = 2;
        FranchiseRules.Inspect(f, 20);
        Applicant a = FranchiseRules.Generate(s.Seed, s.NextId++, b.Id, 15, ThreeDistricts);
        s.Applicants.Add(a);
        Taken t = FranchiseRules.Take(s, "Side~9", 2, 14, 60);
        t.Breach = 4;
        t.Average = 123;
        string line = BrandStore.Write(s);
        int version;
        BrandsState back = BrandStore.Read(line, out version);
        Check(back != null && version == BrandStore.Version, "the save's line reads back");
        Check(BrandStore.Write(back) == line, "and writes the same line again: nothing is lost or changed");
        Brand bb = back.Brand(b.Id);
        Check(bb != null && bb.Name == "Copper Kettle" && bb.Type == CoffeeType && bb.Offered && bb.Minimum == 70 && bb.City == 1500 && bb.Fund == 420 && bb.Net == -35 && bb.Push == BrandCatalogue.PushLocal
              && bb.PushDistrict == Garment && bb.District(Midtown).Rec == 5123 && bb.District(Garment).Rec == 801, "the brand comes back with its recognition, its settings and its campaign");
        Franchise ff = back.Franchise(f.Id);
        Check(ff != null && ff.Place == "5th%20Avenue~12" && ff.GameName == "A shop  with   odd   marks" && ff.Hold == Hold.Opened && ff.X == -350 && ff.Z == 1200 && ff.HasSpot && ff.Health == -4000
              && ff.Born == 12 && ff.Ask == Ask.Dispute && ff.Suit == 4 && ff.Exclusive && ff.RoyaltyBp == 800 && ff.LevyBp == 0 && ff.Seen == f.Seen && ff.True == f.True && ff.Term == f.Term,
            "the agreement comes back whole, and a name with the line's dividers in it could not break the line");
        Check(back.Members.Count == 3 && back.Applicants.Count == 1 && back.Applicants[0].Name == a.Name && back.Applicants[0].Capital == a.Capital && back.Taken.Count == 1 && back.Taken[0].Breach == 4
              && back.Taken[0].Chain == 2 && back.NextId == s.NextId && back.LastDay == 77 && back.Seed == 4242, "the members, the applicant, the shop under a chain and the header come back");
        Check(BrandStore.Read(null, out version) == null && BrandStore.Read("", out version) == null && BrandStore.Read("something else", out version) == null && BrandStore.Read("v=0^h:1|1|1|1", out version) == null,
            "a line that is not this feature's is not read");
        Check(BrandStore.Read("v=1^b:1|Lone|type|Main~1", out version) == null, "a line without its header is not read");
        BrandsState later = BrandStore.Read(line + "|extra^z:a part from a later version", out version);
        Check(later != null && later.Brands.Count == 1 && later.Franchises.Count == 1, "a line from a later version, with more fields and a part this one does not know, still reads");
        BrandsState odd = BrandStore.Read("v=1^h:5|1|9|3^b:1|Kettle|type|Main~1|1|4|999999|400|55|1|7^d:1|" + Midtown + "|-40;9|" + Midtown + "|50^m:Main~1|1|1;Main~2|9|1^f:2|9|x|0|0|d|p|g|0|0|0|0|0|0|0|0|0|0|0|0", out version);
        Check(odd != null && odd.Brands[0].City == BrandCatalogue.RecMost && odd.Brands[0].Standards == 100 && odd.Brands[0].Minimum == 60 && odd.Brands[0].Push == -1 && odd.Brands[0].District(Midtown).Rec == 0,
            "figures out of their bounds are put inside them");
        Check(odd.Members.Count == 1 && odd.Franchises.Count == 0, "a member or an agreement of a brand that is not in the line is dropped");
        var data = new Dictionary<string, string> { { BrandStore.Key, line } };
        Check(BrandStore.Load(data, BrandStore.Key, out version) != null && BrandStore.Load(new Dictionary<string, string>(), BrandStore.Key, out version) == null && BrandStore.Load(null, BrandStore.Key, out version) == null,
            "a save made without the feature has no line, and that is no error");
        Check(BrandStore.Key == "reallife:economy_brands", "the save's key is the feature's own");
        BrandsState old = BrandStore.Read("v=1^h:5|1|9|8^b:1|Kettle|type|Main~1^f:2|1|Harbour Lane Trading|0|60|d|p|g|1|0|0|0|150|1|61|60|15000|600|100|0", out version);
        Check(old != null && old.Franchises.Count == 1 && old.Franchises[0].Born == -1 && old.Franchises[0].True == 6000 && old.Franchises[0].Seen == -1 && !old.Franchises[0].NoRenewal,
            "an agreement written with fewer fields reads with the rest at their start");
    }

    // The balance the rules file writes down, worked from the rules themselves.
    private static void BrandBalance()
    {
        Brand b;
        BrandsState s = NewBrands(10, out b, 70, 150, 6000);
        b.District(Midtown).Rec = 5000;
        long royalty = 0, levy = 0, support = 0, takings = 0;
        var five = new List<Franchise>();
        for (int i = 0; i < 5; i++)
        {
            Franchise f = Signed(s, b, Temper.Careful, 1, 100000, 60, 1, 1, i == 0 ? Midtown : "district " + i);
            f.Until = 100000;
            f.True = 7000;
            five.Add(f);
        }
        const int Days = 360;
        for (int day = 2; day < 2 + Days; day++)
        {
            foreach (Franchise f in five)
            {
                f.True = 7000;
                FranchiseDay r = FranchiseRules.Day(s, b, f, day, 100, CycleState.Steady, 1000);
                royalty += r.Royalty;
                levy += r.Levy;
                support += r.Support;
                takings += r.Takings;
                FranchiseRules.Drop(b, f, 0);
            }
        }
        int perShop = (int)((royalty - support) / (5 * Days)), withLevy = (int)((royalty + levy - support) / (5 * Days));
        Check(perShop > 150 && perShop < 300, "a franchised shop on the middle terms leaves the brand about 224 a day to keep (" + perShop + "; " + withLevy + " with the levy)");
        const int OwnedProfit = 1500;
        Check(perShop * 100 / OwnedProfit >= 10 && perShop * 100 / OwnedProfit <= 20, "that is between a tenth and a fifth of what a shop the player runs makes (" + perShop * 100 / OwnedProfit + "%)");
        Check(10 * OwnedProfit + 5 * withLevy < 15 * OwnedProfit * 80 / 100, "ten shops and five franchises bring under four fifths of what fifteen shops do: franchising is not the richer way");
        Check(5 * FranchiseRules.EntryFee(null, 1, 1000) == 75000, "but it brings 75,000 in at signing where five more shops would cost capital");
        int cheapest = FranchiseRules.Royalty(6000 * 60 / 100, BrandCatalogue.RoyaltyBp[0]) - FranchiseRules.Support(6000 * 60 / 100, 1000);
        Check(cheapest > 0 && cheapest < 100, "the poorest shop on the lowest royalty still covers its support, barely (" + cheapest + " a day)");
        Check(FranchiseRules.Compensation(b, five[0], 2) >= 7500, "and getting rid of a bad one without cause costs at least a month of what a good one brings");
    }

    private static void BrandTiming()
    {
        var s = new BrandsState { Seed = 99 };
        var brands = new List<Brand>();
        for (int k = 0; k < 3; k++)
        {
            Brand b = BrandRules.Create(s, "Brand " + k, CoffeeType, "Home~" + k, "district 0", 1);
            for (int i = 1; i < 10; i++)
            {
                BrandRules.Join(s, b, "Home~" + k + "-" + i, CoffeeType, "district " + (i % 6), 1);
            }
            brands.Add(b);
        }
        foreach (Member m in s.Members)
        {
            m.Standards = 75;
            m.Customers = 150;
            m.Takings = 6000;
            m.HasSpot = true;
        }
        for (int i = 0; i < 60; i++)
        {
            Brand b = brands[i % 3];
            b.Ticket = 4000;
            Franchise f = Signed(s, b, (Temper)(i % 3), 1, 100000, 40 + i % 50, i % 3, i % 3, "district " + (i % 6));
            f.Until = 100000;
            f.HasSpot = true;
            f.Hold = Hold.Books;
            f.X = i * 20;
        }
        Check(s.Franchises.Count == 60 && s.Members.Count == 30, "a city with three brands, thirty member shops and sixty franchised shops");
        const int Runs = 400;
        long guard = 0;
        // (once to warm up, then timed)
        for (int pass = 0; pass < 2; pass++)
        {
            var watch = Stopwatch.StartNew();
            for (int run = 0; run < Runs; run++)
            {
                int day = 2 + pass * Runs + run;
                for (int k = 0; k < brands.Count; k++)
                {
                    BrandRules.Day(s, brands[k]);
                }
                for (int i = 0; i < s.Franchises.Count; i++)
                {
                    Franchise f = s.Franchises[i];
                    FranchiseDay r = FranchiseRules.Day(s, s.Brand(f.BrandId), f, day, 100, CycleState.Steady, 1000);
                    guard += r.Royalty;
                    f.Health = 100000;
                    f.Ask = Ask.None;
                }
                for (int i = 0; i < s.Members.Count; i++)
                {
                    guard += BrandRules.Apply(s, s.Brand(s.Members[i].BrandId), s.Members[i]);
                }
            }
            watch.Stop();
            if (pass == 1)
            {
                double each = watch.Elapsed.TotalMilliseconds / Runs;
                Check(each < 0.2, "a day of that city's rules takes under 0.2 ms (" + each.ToString("0.000") + " ms, " + guard + ")");
            }
        }
    }
}
