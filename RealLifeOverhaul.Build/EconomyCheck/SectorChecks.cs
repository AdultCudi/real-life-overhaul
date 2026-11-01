using System;
using System.Collections.Generic;
using RealLifeOverhaul.Population;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;

// The other tech firms under Living population (Scripts/Tech/Sector: SectorCatalogue, SectorRules, SectorStore, SectorViews;
// plans/tech-overhaul DRAFT2 section 3): one shared market for each kind of product and buyer, sized from the city's people
// and firms with the world they stand for; the curves in place of the old clamps; rivals by the same rules; the market for IT
// work; data centres and the cloud from the city's firms and labs; the fourteen desk sessions; an older save taken in with no
// jump; the save's line and the shared city's part; the screens. The ten years, the bands and the costs are in
// SectorChecks.Sims.cs, the texts and sources in SectorChecks.Texts.cs. Run with "--sector" alone, "sector" after it prints.
internal static partial class Program
{
    private static void SectorChecks()
    {
        int before = _run, failedBefore = _failed;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        bool print = Array.IndexOf(Environment.GetCommandLineArgs(), "sector") >= 0;
        SectorShape();
        SectorCity();
        SectorMarketsSized(print);
        SectorConserve();
        SectorCurves();
        SectorProductDay();
        SectorRivals();
        SectorTenders();
        SectorWork();
        SectorInfra();
        SectorDesk();
        SectorOldSave();
        SectorSave();
        SectorScreens();
        SectorSims(print);
        SectorTimings();
        SectorTexts();
        Console.WriteLine("  Tech firms' markets: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private const int SectorYear = 365;   // the sector checks' calendar: the game's longest year, on which the bands were set (the sims and the living years hold the game's default, 60, too)

    // A sector file over the documented city with the companies' products set up at their usual features, settled at their share.
    private static SectorFile SectorFresh(int seed, int year = SectorYear)
    {
        var f = new SectorFile { Seed = seed, FirstDay = 0, YearDays = year };
        SectorRules.Companies(f, 0, SectorRules.StartFeatures(null));
        SectorRules.BeginDay(f, 1, 1);
        SectorRules.Attract(f);
        SectorRules.Settle(f);
        return f;
    }

    // ---------------------------------------------------------------- the catalogue

    private static void SectorShape()
    {
        int companies = HiringCatalogue.RivalFirms.Length;
        Check(SectorCatalogue.CompanyMarkets.Length == companies && SectorCatalogue.CompanyTeam.Length == companies && SectorCatalogue.CompanyReputation.Length == companies
              && SectorCatalogue.CompanyMarketing.Length == companies && CompetitorRules.CompanyMakes.Length == companies,
            "sector: every company table runs in the order of the city's tech companies");
        bool covered = true, agrees = true;
        for (int m = 0; m < (int)SectorMarket.Count; m++)
        {
            bool any = false;
            for (int c = 0; c < companies; c++)
            {
                any |= (SectorCatalogue.CompanyMarkets[c] & (1 << m)) != 0;
            }
            covered &= any;
        }
        for (int c = 0; c < companies; c++)
        {
            bool sells = SectorCatalogue.CompanyMarkets[c] != 0, makes = (CompetitorRules.CompanyMakes[c] & ~(1 << (int)ProductKind.Game)) != 0;
            agrees &= sells == makes && (sells == SectorCatalogue.CompanyTeam[c] > 0);
        }
        Check(covered && agrees, "sector: every market has a listed company in it, and a company sells in a market just when the competitors' table says it makes software");
        var ids = new HashSet<string>();
        bool unique = SectorRules.DeskIds.Length == SectorRules.Desks && SectorRules.DeskFirm.Length == SectorRules.Desks && SectorRules.DeskEvery.Length == SectorRules.Desks;
        for (int k = 0; k < SectorRules.Desks; k++)
        {
            unique &= ids.Add(SectorRules.DeskIds[k]) && SectorRules.DeskIds[k].StartsWith("firm_", StringComparison.Ordinal) && SectorRules.DeskEvery[k] >= 1;
        }
        Check(unique, "sector: fourteen desk sessions, each id its own and every table as long");
        Check(SectorCatalogue.World.Length == (int)SectorBuyers.Count && SectorCatalogue.WorldGrowth.Length == (int)SectorBuyers.Count && SectorCatalogue.Pace.Length == (int)SectorBuyers.Count
              && SectorCatalogue.WordOfMouth.Length == (int)SectorBuyers.Count && SectorCatalogue.StartupMarket.Length == (int)Venture.Count
              && SectorCatalogue.Pace[0] > SectorCatalogue.Pace[1] && SectorCatalogue.Pace[1] > SectorCatalogue.Pace[2],
            "sector: the buyers' tables are as long as the buyers, a startup's market for every sector, and people move faster than firms, firms faster than companies");
        Check(SectorCatalogue.ProductsMost > FirmsCatalogue.ProductsMost && SectorCatalogue.ProductsPerFirm > FirmsCatalogue.ProductsPerFirm && SectorCatalogue.ContractsMost > FirmsCatalogue.ContractsMost
              && FirmsCatalogue.ProductsStored >= SectorCatalogue.ProductsMost + FirmsCatalogue.WithdrawnKept && StudioCatalogue.GamesStored >= FirmsCatalogue.ProductsStored,
            "sector: the limits kept for the day's work and the save are raised, and the save reads back every product either way (the studio's games too)");
        Check(SectorRules.MarketOf(ProductKind.Saas, 0) == 0 && SectorRules.MarketOf(ProductKind.Saas, 2) == (int)SectorMarket.EnterpriseSoftware && SectorRules.MarketOf(ProductKind.App, 1) == (int)SectorMarket.Apps
              && SectorRules.MarketOf(ProductKind.Licence, 0) == (int)SectorMarket.Licences && SectorRules.MarketOf(ProductKind.Model, 1) == (int)SectorMarket.BusinessAi
              && SectorRules.MarketOf(ProductKind.Game, 0) == -1 && SectorRules.BuyersOf((int)SectorMarket.Licences) == (int)SectorBuyers.Enterprises
              && SectorRules.BuyersOf((int)SectorMarket.Apps) == (int)SectorBuyers.People && SectorRules.BuyersOf((int)SectorMarket.BusinessAi) == (int)SectorBuyers.Businesses,
            "sector: each product sells in the market of its kind and buyers; games stay the studio's");
        Check(SectorRules.Sells((int)FirmKind.Software, (int)SectorMarket.Licences) && SectorRules.Sells((int)FirmKind.AiLab, (int)SectorMarket.EnterpriseAi)
              && !SectorRules.Sells((int)FirmKind.Design, (int)SectorMarket.Apps) && SectorRules.Sells((int)FirmKind.WebAgency, (int)SectorMarket.Apps)
              && !SectorRules.Sells((int)FirmKind.Games, (int)SectorMarket.ConsumerSoftware),
            "sector: a kind of firm sells in the markets of what it makes, and no other");
    }

    // ---------------------------------------------------------------- the city

    private static void SectorCity()
    {
        byte[] kinds =
        {
            (byte)EmployerKind.Business, (byte)EmployerKind.Business, (byte)EmployerKind.Business, (byte)EmployerKind.Tenants, (byte)EmployerKind.Public, (byte)EmployerKind.Public,
            (byte)EmployerKind.Building, (byte)EmployerKind.Mod, (byte)EmployerKind.Special
        };
        int[] slots = { 8, 120, 1500, 10000, 300, 40, 12, 900, 25 };
        SectorRules.FirmsOf(kinds, slots, kinds.Length, out int small, out int mid, out int large);
        int tenantSmall = (int)((10000L * (1000 - SectorCatalogue.TenantLargeShare - SectorCatalogue.TenantMidShare) / SectorCatalogue.SmallFirmJobs + 500) / 1000);
        Check(small == 2 + tenantSmall && mid == 2 + 10000 * SectorCatalogue.TenantMidShare / 1000 / SectorCatalogue.MidFirmJobs && large == 5,   // the big business, 1.8 of the tenants', the big public body, the mod's own company
            "sector city: a shop and a special service are one firm of their size, an office building's floors many in Manhattan's mix, a big public body and the mod's own companies large, a building's staff none ("
            + small + " small, " + mid + " mid, " + large + " large)");
        SectorRules.FirmsOf(null, null, 5, out int s0, out int m0, out int l0);
        SectorRules.FirmsOf(kinds, slots, 0, out int s1, out _, out _);
        Check(s0 == 0 && m0 == 0 && l0 == 0 && s1 == 0, "sector city: no employers, no firms; nothing breaks on none");
        long anchor = SectorRules.DigitalPerPerson(2750000, 55000);
        Check(anchor == 50000 && SectorRules.Digital(2750000, 55000, anchor) == 1000 && SectorRules.Digital(3300000, 55000, anchor) == 1200 && SectorRules.Digital(100, 55000, anchor) == SectorCatalogue.DigitalLeast
              && SectorRules.Digital(1L << 40, 55000, anchor) == SectorCatalogue.DigitalMost && SectorRules.Digital(0, 55000, anchor) == 1000 && SectorRules.Digital(5, 0, anchor) == 1000,
            "sector city: the households' mood for digital spending is a person's against the first day's, within its bounds, and even with nothing to go by");
        Check(SectorRules.WorldAt(0, 0, SectorYear) == SectorCatalogue.World[0] && SectorRules.WorldAt(0, SectorYear, SectorYear) == SectorCatalogue.World[0] * (1000 + SectorCatalogue.WorldGrowth[0]) / 1000
              && SectorRules.WorldAt(1, 180, SectorYear) > SectorRules.WorldAt(1, 0, SectorYear) && SectorRules.WorldAt(2, -5, SectorYear) == SectorCatalogue.World[2]
              && SectorRules.WorldAt(0, 365 * 10, SectorYear) > SectorRules.WorldAt(0, 365 * 9, SectorYear) && SectorRules.WorldAt(0, 1 << 30, SectorYear) > 0,
            "sector city: the world beyond the city grows by its rate each of the save's years, within a year straight, never before the first day, and stays in range after centuries");
        bool clock = true, filling = true;
        for (int w = 0; w < (int)SectorBuyers.Count; w++)
        {
            long ten = SectorRules.WorldAt(w, 3650, 365), full = SectorRules.WorldAt(w, 365 * 400, 365);
            clock &= SectorRules.WorldAt(w, 60, 60) == SectorRules.WorldAt(w, 365, 365) && SectorRules.WorldAt(w, 600, 60) == ten;
            filling &= full > ten && full <= (long)SectorCatalogue.World[w] * SectorCatalogue.WorldCeiling[w] / 1000 && SectorRules.WorldAt(w, 3650 * 2, 365) - ten < ten - SectorCatalogue.World[w];
        }
        Check(clock && filling, "sector city: the one clock: a year or ten of the save grow the world alike at 60 days a year and at 365, slowing as it fills towards its ceiling and never past it");
        var f = new SectorFile();
        SectorBridges.Reset();
        Check(!SectorBridges.Read(f) && !f.City.Known && f.City.People == SectorCatalogue.ReferencePeople && f.City.Small == SectorCatalogue.ReferenceSmall,
            "sector city: with no population the documented city stands in");
        SectorBridges.City = (out int p, out int sm, out int md, out int lg, out long digital) => { p = 60000; sm = 2800; md = 200; lg = 40; digital = 3000000; return true; };
        bool read = SectorBridges.Read(f);
        long kept = f.DigitalAnchor;
        SectorBridges.City = (out int p, out int sm, out int md, out int lg, out long digital) => { p = 60000; sm = 2800; md = 200; lg = 40; digital = 2400000; return true; };
        SectorBridges.Read(f);
        Check(read && f.City.Known && f.City.People == 60000 && f.City.Large == 40 && kept == 50000 && f.DigitalAnchor == kept && f.City.Digital == 800,
            "sector city: the population's city is read, its first day's digital spending kept as the anchor, a leaner day read as a leaner mood (" + f.City.Digital + ")");
        SectorBridges.City = (out int p, out int sm, out int md, out int lg, out long digital) => throw new InvalidOperationException("the population's fault");
        Check(!SectorBridges.Read(f) && !f.City.Known && f.City.People == SectorCatalogue.ReferencePeople, "sector city: a fault in the population reads as the documented city");
        SectorBridges.Reset();
        SectorBridges.Talk = (k, place, name, n) => throw new InvalidOperationException("Flare's fault");
        SectorBridges.Say(SectorNews.Launch, "p", "x", 1);
        SectorBridges.Reset();
        Check(SectorBridges.City == null && SectorBridges.Talk == null, "sector city: Flare's fault leaves the firm unheard; the bridges reset to none");
    }

    // ---------------------------------------------------------------- the markets sized

    private static void SectorMarketsSized(bool print)
    {
        var city = new SectorCityFigures();
        long people = SectorRules.Buyers((int)SectorMarket.ConsumerSoftware, city, 0, 1, SectorYear), firms = SectorRules.Buyers((int)SectorMarket.BusinessSoftware, city, 0, 1, SectorYear),
            companies = SectorRules.Buyers((int)SectorMarket.Licences, city, 0, 1, SectorYear);
        Check(people > 1000000 && firms > 8000 && companies > 300 && companies < 1000 && SectorRules.Buyers((int)SectorMarket.Apps, city, 0, 1, SectorYear) == people
              && SectorRules.Buyers((int)SectorMarket.BusinessAi, city, 0, 1, SectorYear) == firms && SectorRules.Buyers((int)SectorMarket.EnterpriseAi, city, 0, 1, SectorYear) == companies,
            "sector markets: the documented city's markets are millions of people, thousands of businesses and hundreds of companies, each shared by every product of its kind ("
            + people + ", " + firms + ", " + companies + ")");
        var bigger = new SectorCityFigures();
        bigger.Set(city.People * 2, city.Small * 2, city.Mid * 2, city.Large * 2, 1000, true);
        Check(SectorRules.Buyers(0, bigger, 0, 1, SectorYear) == people * 2 && SectorRules.Buyers(1, bigger, 0, 1, SectorYear) >= firms * 2 - 2 && SectorRules.Buyers(2, bigger, 0, 1, SectorYear) >= companies * 2 - 2,
            "sector markets: a city twice as big is a market twice as big: no cap but the city and the world");
        Check(SectorRules.Buyers(1, city, 0, 3, SectorYear) < SectorRules.Buyers(1, city, 0, 0, SectorYear) && SectorRules.Buyers(0, city, 0, 3, SectorYear) < SectorRules.Buyers(0, city, 0, 0, SectorYear),
            "sector markets: a recession shrinks the markets and a boom grows them (people by the cycle while their spending is not read)");
        var mood = new SectorCityFigures();
        mood.Set(city.People, city.Small, city.Mid, city.Large, 1500, true);
        Check(SectorRules.Buyers(0, mood, 0, 3, SectorYear) == people * 1500 / 1000 && SectorRules.Buyers(1, mood, 0, 1, SectorYear) == firms,
            "sector markets: with the population running, people's market follows their digital spending, not the cycle; firms' follows the cycle");
        Check(SectorRules.Buyers(0, city, 3650, 1, SectorYear) > people * 13 / 10 && SectorRules.Buyers(0, city, 3650, 1, SectorYear) < people * 2 && SectorRules.AnchorNow(3000, 10, 10) == 3000 && Math.Abs(SectorRules.AnchorNow(3000, 10, 10 + SectorRules.AnchorDays / 2) - 2000) <= 5
              && SectorRules.AnchorNow(3000, 10, 10 + SectorRules.AnchorDays) == 1000 && SectorRules.AnchorNow(800, 0, 0) == 1000,
            "sector markets: the world grows over ten years, filling towards its ceiling; an older save's anchor fades straight to the city's own market over a year");
        if (print)
        {
            Console.WriteLine("SECTOR MARKETS (documented city): consumers " + people.ToString("N0") + ", businesses " + firms.ToString("N0") + ", companies " + companies.ToString("N0")
                              + "; after ten years consumers " + SectorRules.Buyers(0, city, 3650, 1, SectorYear).ToString("N0"));
        }
    }

    // ---------------------------------------------------------------- markets conserve

    private static void SectorConserve()
    {
        var md = new SectorMarketDay { Buyers = 1000000 };
        long[] pulls = { 900, 600, 450, 300, 80, 2500, 1 };
        foreach (long a in pulls)
        {
            md.All += a;
        }
        long sum = 0;
        bool each = true;
        foreach (long a in pulls)
        {
            long t = SectorRules.Target(md, a);
            sum += t;
            each &= t <= md.Buyers && t >= 0;
        }
        Check(each && sum <= md.Buyers && sum > md.Buyers * 8 / 10, "sector conserve: every product's share of a market together never passes its buyers (" + sum + " of " + md.Buyers + ")");
        var alone = new SectorMarketDay { Buyers = 50000, All = 900 };
        var crowded = new SectorMarketDay { Buyers = 50000, All = 900 + 900 + 900 };
        Check(SectorRules.Target(alone, 900) > SectorRules.Target(crowded, 900) * 2 && SectorRules.Target(alone, 0) == 0 && SectorRules.Target(new SectorMarketDay(), 900) == 0,
            "sector conserve: a rival takes from the same buyers; no pull, no share; no buyers, no customers");
        SectorFile f = SectorFresh(7);
        bool settled = true;
        for (int m = 0; m < (int)SectorMarket.Count; m++)
        {
            long total = 0;
            for (int i = 0; i < f.Rivals.Count; i++)
            {
                total += f.Rivals[i].Market == m ? f.Rivals[i].Customers : 0;
            }
            settled &= total <= f.Markets[m].Buyers && total > 0;
        }
        Check(settled && f.Rivals.Count == 12, "sector conserve: the companies settle at their shares, every market's customers within its buyers (" + f.Rivals.Count + " rival products)");
    }

    // ---------------------------------------------------------------- the curves in place of the clamps

    private static void SectorCurves()
    {
        Check(SectorRules.FeatureAppeal(9, 9) == 1000 && SectorRules.FeatureAppeal(19, 9) == 1333 && SectorRules.FeatureAppeal(3, 11) == 500 && SectorRules.FeatureAppeal(0, 0) == 1000
              && SectorRules.FeatureAppeal(100000, 1) > 1990 && SectorRules.FeatureAppeal(100000, 1) <= 2000 && SectorRules.FeatureAppeal(0, 1000) > 0 && SectorRules.FeatureAppeal(-5, 3) == SectorRules.FeatureAppeal(0, 3),
            "sector curves: features against the going ones: even 1000, twice 1333, a third 500, more always a little more and never a clamp at either end");
        bool rising = true;
        for (int f = 1; f < 100; f++)
        {
            rising &= SectorRules.FeatureAppeal(f, 20) > SectorRules.FeatureAppeal(f - 1, 20);
        }
        Check(rising, "sector curves: each feature more appeals more, five times the going ones still more than four (the old clamp stopped at twice the rival)");
        Check(SectorRules.Soundness(0, 100) == 1000 && SectorRules.Soundness(50, 100) == 500 && SectorRules.Soundness(100, 100) == 333 && SectorRules.Soundness(1000000, 100) == 1
              && SectorRules.Soundness(10, 0) < 1000 && SectorRules.Soundness(-3, 100) == 1000,
            "sector curves: a product's bugs: sound with none, half at half its room, a third at its room, never nothing (the old floor of 300 gone)");
        Check(SectorRules.Lift(0, 40, 2000) == 0 && SectorRules.Lift(40, 40, 2000) == 1000 && SectorRules.Lift(1000000, 40, 2000) < 2000 && SectorRules.Lift(400, 40, 2000) > SectorRules.Lift(120, 40, 2000)
              && SectorRules.Lift(-5, 40, 2000) == 0 && SectorRules.Lift(10, 0, 2000) > 0,
            "sector curves: a boost rises with diminishing returns, half its gain at its half point, never past the gain, nothing at nothing");
        int one = SectorRules.Posture(FirmsCatalogue.ReliabilityBase, 150, SectorCatalogue.ReliabilityHalf), four = SectorRules.Posture(FirmsCatalogue.ReliabilityBase, 600, SectorCatalogue.ReliabilityHalf),
            ten = SectorRules.Posture(FirmsCatalogue.ReliabilityBase, 1500, SectorCatalogue.ReliabilityHalf), forty = SectorRules.Posture(FirmsCatalogue.ReliabilityBase, 6000, SectorCatalogue.ReliabilityHalf);
        Check(SectorRules.Posture(FirmsCatalogue.ReliabilityBase, 0, SectorCatalogue.ReliabilityHalf) == FirmsCatalogue.ReliabilityBase && one < four && four < ten && ten < forty && forty > FirmsCatalogue.ReliabilityMost
              && forty < 1000,
            "sector curves: each reliability engineer adds less than the last but always something, past the old top of 950 with a large team (" + one + ", " + four + ", " + ten + ", " + forty + ")");
        var c = new Crew { Engineering = 32, Product = 8 };
        Check(SectorRules.ProductBoost(c) > 200 && SectorRules.ProductBoost(new Crew { Engineering = 32 }) == 0 && SectorRules.ProductBoost(new Crew { Product = 1000 }) < SectorCatalogue.ProductGain,
            "sector curves: product people make features go further by a curve, never past its gain");
        var firm = new Firm { Reputation = 500 };
        for (int d = 0; d < 2000; d++)
        {
            SectorRules.Reputation(firm, 1000);
        }
        var poor = new Firm { Reputation = 900 };
        for (int d = 0; d < 2000; d++)
        {
            SectorRules.Reputation(poor, 200);
        }
        Check(firm.Reputation > 990 && poor.Reputation < 450 && SectorRules.Toward(1000, 1200) == 1000 && SectorRules.Toward(0, -50) == 0 && SectorRules.Toward(500, 501) == 501,
            "sector curves: a firm whose work is sound climbs past the old top of 800 towards 1000, slower near the top; poor work brings it down (" + firm.Reputation + ", " + poor.Reputation + ")");
    }

    // ---------------------------------------------------------------- a product's day in its market

    // A product of the player's sold for so many days in a market of so many buyers against the documented city's rivals.
    private static Product SectorSell(ProductKind kind, Segment segment, int features, long buyers, int days, int seed, out SectorFile f, Crew c, int tier = 1)
    {
        var ff = new FirmsFile { Seed = seed };
        f = SectorFresh(seed);
        var firm = new Firm { Place = "p", Reputation = 700 };
        ff.Firms.Add(firm);
        Product p = FirmsRules.NewProduct(ff, "p", kind, segment, 0, "Test");
        p.Features = features;
        p.Tier = tier;
        p.Marketing = 3;
        ff.Products.Add(p);
        FirmsRules.Launch(p, 1);
        int m = SectorRules.MarketOf(p);
        for (int day = 2; day <= days; day++)
        {
            SectorRules.BeginDay(f, day, 1);
            f.Markets[m].Buyers = buyers > 0 ? buyers : f.Markets[m].Buyers;
            SectorRules.Count(f, p);
            SectorRules.Attract(f);
            SectorMarketDay md = f.Markets[m];
            int appeal = SectorRules.Appeal(p, firm, c, md), aware = SectorRules.Awareness(FirmsCatalogue.MarketingDay[p.Marketing], md.Buyers, SectorRules.Have(p), md.Who, 0);
            SectorRules.Join(md, SectorRules.Attraction(appeal, aware));
            SectorRules.SellDay(ff, firm, p, c, 50, day, md, appeal, aware, 1000);
        }
        return p;
    }

    private static void SectorProductDay()
    {
        Crew c = Team(32);
        c.Selling = 16;
        c.Reliability = 950;
        c.Security = 950;
        Product small = SectorSell(ProductKind.Saas, Segment.Business, 40, 9000, 500, 3, out _, c), big = SectorSell(ProductKind.Saas, Segment.Business, 40, 90000, 500, 3, out _, c);
        Check(big.Users > FirmsCatalogue.Market[1] && big.Users > small.Users * 5,
            "sector day: a strong business product in a market ten times the size wins ten times the customers, past the old 8,000 a product could ever have (" + small.Users + " against " + big.Users + ")");
        Product lic = SectorSell(ProductKind.Licence, Segment.Enterprise, 40, 20000, 1500, 5, out _, c);
        Check(lic.Users > FirmsCatalogue.Market[2], "sector day: licences pass the old 400 deals when the companies are there to buy them (" + lic.Users + ")");
        Product few = SectorSell(ProductKind.Licence, Segment.Enterprise, 40, 0, 800, 5, out SectorFile lf, c);
        long target = SectorRules.Target(lf.Markets[(int)SectorMarket.Licences], SectorRules.Attraction(800, 800));
        Check(few.Users > 0 && few.Users <= lf.Markets[(int)SectorMarket.Licences].Buyers, "sector day: a licence sells only into its share of the companies there are (" + few.Users + " of "
              + lf.Markets[(int)SectorMarket.Licences].Buyers + ", " + target + ")");
        Product weak = SectorSell(ProductKind.Saas, Segment.Business, 2, 9000, 300, 3, out _, c), strong = SectorSell(ProductKind.Saas, Segment.Business, 60, 9000, 300, 3, out _, c);
        Check(strong.Users > weak.Users * 3, "sector day: more features than the market's going ones win more of the same buyers (" + weak.Users + " against " + strong.Users + ")");
        Product consumer = SectorSell(ProductKind.App, Segment.Consumer, 30, 0, 300, 9, out _, c);
        Check(consumer.Free > consumer.Users * 10 && consumer.Users > 0, "sector day: most of a consumer product's users do not pay (" + consumer.Users + " paying, " + consumer.Free + " free)");
        Product a = SectorSell(ProductKind.Saas, Segment.Business, 30, 9000, 200, 11, out _, c), b = SectorSell(ProductKind.Saas, Segment.Business, 30, 9000, 200, 11, out _, c);
        Check(a.Users == b.Users && a.Revenue == b.Revenue, "sector day: the same seed sells the same on every machine (" + a.Users + ")");
        // past its share: a product's customers drift down to it, not in a day
        var ff = new FirmsFile { Seed = 3 };
        SectorFile f = SectorFresh(3);
        var firm = new Firm { Place = "p", Reputation = 500 };
        Product over = FirmsRules.NewProduct(ff, "p", ProductKind.Saas, Segment.Business, 0, "Over");
        over.Features = 10;
        over.Users = 30000;
        FirmsRules.Launch(over, 1);
        SectorRules.BeginDay(f, 2, 1);
        SectorRules.Count(f, over);
        SectorRules.Attract(f);
        SectorMarketDay md = f.Markets[(int)SectorMarket.BusinessSoftware];
        int appeal = SectorRules.Appeal(over, firm, c, md), aware = SectorRules.Awareness(0, md.Buyers, over.Users, md.Who, 0);
        SectorRules.Join(md, SectorRules.Attraction(appeal, aware));
        long before = over.Users;
        ProductDay r = SectorRules.SellDay(ff, firm, over, c, 50, 2, md, appeal, aware, 1000);
        Check(r.Lost > 0 && over.Users < before && over.Users > before * 97 / 100, "sector day: a product past its share loses customers to the others a little a day, not at once (" + before + " to " + over.Users + ")");
    }

    // ---------------------------------------------------------------- the rivals

    private static void SectorRivals()
    {
        SectorFile f = SectorFresh(21);
        int again = SectorRules.Companies(f, 2, null);
        int start = 0;
        for (int i = 0; i < f.Rivals.Count; i++)
        {
            start += f.Rivals[i].Features;
        }
        var notes = new List<RivalNote>();
        for (int day = 2; day <= 200; day++)
        {
            SectorRules.BeginDay(f, day, 1);
            SectorRules.Attract(f);
            SectorRules.RivalDay(f, day, notes);
        }
        int after = 0;
        for (int i = 0; i < f.Rivals.Count; i++)
        {
            after += f.Rivals[i].Features;
        }
        Check(again == 0 && after > start + 50, "sector rivals: the companies' products are made once and keep shipping features by the player's own rule (" + start + " to " + after + " features)");
        // a rival losing its customers to a strong product cuts its price, and raises it back once it holds them again
        SectorFile g = SectorFresh(5);
        RivalProduct abacus = g.RivalOf(2, (int)SectorMarket.BusinessSoftware);
        bool cut = false, raised = false;
        for (int day = 2; day <= 400; day++)
        {
            SectorRules.BeginDay(g, day, 1);
            SectorRules.Attract(g);
            if (day < 200)
            {
                SectorRules.Join(g.Markets[(int)SectorMarket.BusinessSoftware], 40000);   // a player's product far stronger than every rival
            }
            SectorRules.RivalDay(g, day, notes);
            for (int n = 0; n < notes.Count; n++)
            {
                cut |= notes[n].Kind == SectorRules.NoteCut && g.Rivals[notes[n].Rival] == abacus;
                raised |= notes[n].Kind == SectorRules.NoteRaised && g.Rivals[notes[n].Rival] == abacus && cut;
            }
        }
        Check(cut && raised, "sector rivals: a rival that loses its customers cuts its price, and raises it back when it holds them again");
        // the same seed, the same rivals
        SectorFile x = SectorFresh(9), y = SectorFresh(9);
        for (int day = 2; day <= 300; day++)
        {
            foreach (SectorFile s in new[] { x, y })
            {
                SectorRules.BeginDay(s, day, 1);
                SectorRules.Attract(s);
                SectorRules.RivalDay(s, day, null);
            }
        }
        Check(SectorStore.Write(x) == SectorStore.Write(y), "sector rivals: the same seed makes the same rivals on every machine");
        // startups come and go with the startups
        var startups = new List<Startup>
        {
            new Startup { Id = 41, Name = "Fernbook", Sector = (int)Venture.Software, State = (int)StartupState.Running, Stage = 1 },
            new Startup { Id = 42, Name = "Pixel Lark", Sector = (int)Venture.Games, State = (int)StartupState.Running },
            new Startup { Id = 43, Name = "Aster AI", Sector = (int)Venture.Ai, State = (int)StartupState.Raising, Stage = 2 }
        };
        SectorRules.Startups(f, startups, 210, notes);
        RivalProduct fern = f.StartupProduct(41), aster = f.StartupProduct(43);
        Check(fern != null && fern.Market == (int)SectorMarket.BusinessSoftware && aster != null && aster.Market == (int)SectorMarket.BusinessAi && f.StartupProduct(42) == null
              && SectorRules.RivalPoints(f, aster) > SectorRules.RivalPoints(f, fern),
            "sector rivals: a running startup has a product in its market (games stay the studio's), its team growing with its rounds");
        startups[0].State = (int)StartupState.Failed;
        SectorRules.Startups(f, startups, 211, notes);
        Check(f.StartupProduct(41) == null && f.StartupProduct(43) != null, "sector rivals: a startup's product goes with the startup");
        SectorFile noted = SectorFresh(9);
        var news = new List<RivalNote>();
        var comers = new List<Startup> { new Startup { Id = 51, Name = "Old Oak", Sector = (int)Venture.Software, State = (int)StartupState.Running } };
        SectorRules.Startups(noted, comers, 5, news);
        comers[0].State = (int)StartupState.Failed;
        comers.Add(new Startup { Id = 52, Name = "New Elm", Sector = (int)Venture.Software, State = (int)StartupState.Running });
        SectorRules.Startups(noted, comers, 6, news);
        Check(news.Count == 1 && news[0].Kind == SectorRules.NoteNew && news[0].Rival >= 0 && news[0].Rival < noted.Rivals.Count
              && noted.Rivals[news[0].Rival].Startup == 52 && noted.StartupProduct(51) == null,
            "sector rivals: a new startup's product is noted where it stands once the closed startups' products are gone, so its story names it");
        int pull;
        RivalProduct leader = f.RivalOf(6, (int)SectorMarket.Apps);
        leader.LastShip = 300;
        int poacher = SectorRules.Poacher(f, (int)FirmKind.Software, 310, out pull);
        int none = SectorRules.Poacher(f, (int)FirmKind.Software, 300 + SectorRules.PoachDays + 250, out int calm);
        Check(poacher >= 0 && pull == SectorRules.PoachPull && SectorRules.Poacher(f, (int)FirmKind.Design, 310, out _) < 0 && calm == 1000,
            "sector rivals: a company that just shipped comes for the people of the firms that sell against it, harder; none for a kind it does not meet");
        Check(none < 0 || calm == SectorRules.PoachPull, "sector rivals: once its lead is old news it pulls as usual");
    }

    // ---------------------------------------------------------------- tenders by the same rule

    private static void SectorTenders()
    {
        int cheap = 0, dear = 0, pitched = 0;
        for (int i = 0; i < 300; i++)
        {
            var ff = new FirmsFile { Seed = 100 + i };
            var f = new SectorFile();
            SectorRules.Companies(f, 0, null);
            var firm = new Firm { Reputation = 600 };
            var t = new Contract { Id = i, Kind = (int)WorkKind.Integration, Value = 5000000, Rivals = 3, Stage = (int)WorkStage.Bid };
            t.Bid = FirmsRules.BidAt(t, 800);
            cheap += SectorRules.Decide(ff, f, t, firm, 1, 0, 0, out _) ? 1 : 0;
            var u = new Contract { Id = i, Kind = (int)WorkKind.Integration, Value = 5000000, Rivals = 3, Stage = (int)WorkStage.Bid };
            u.Bid = FirmsRules.BidAt(u, 1200);
            dear += SectorRules.Decide(ff, f, u, firm, 1, 0, 0, out _) ? 1 : 0;
            var v = new Contract { Id = i, Kind = (int)WorkKind.Integration, Value = 5000000, Rivals = 3, Stage = (int)WorkStage.Bid };
            v.Bid = FirmsRules.BidAt(v, 1200);
            pitched += SectorRules.Decide(ff, f, v, firm, 1, 0, SectorCatalogue.PitchSeniorWeight, out _) ? 1 : 0;
        }
        Check(cheap > dear * 3 && pitched > dear, "sector tenders: a lower bid wins more, and a pitch with seniors on it weighs better (" + cheap + ", " + dear + ", " + pitched + " of 300)");
        var g = new SectorFile();
        SectorRules.Companies(g, 0, null);
        var lost = new Contract { Id = 7, Kind = (int)WorkKind.Integration, Value = 5000000, Rivals = 3, Stage = (int)WorkStage.Bid, Council = true };
        lost.Bid = FirmsRules.BidAt(lost, 1250);
        bool won = SectorRules.Decide(new FirmsFile { Seed = 4 }, g, lost, new Firm { Reputation = 100 }, 50, 0, 0, out int winner);
        Check(!won && winner >= 0 && g.CompanyWon[winner] == 50 && lost.At == WorkStage.Lost, "sector tenders: a rival that wins is named and remembered (and comes for the losers' people)");
        var none = new Contract { Value = 100, Stage = (int)WorkStage.Tender };
        Check(!SectorRules.Decide(new FirmsFile(), g, none, new Firm(), 1, 0, 0, out _) && none.At == WorkStage.Lost, "sector tenders: no bid, no contract");
    }

    // ---------------------------------------------------------------- IT work from the city

    private static int SectorOffers(int reputation, int selling, SectorCityFigures city, int days, out long points, FirmKind kind = FirmKind.Consultancy)
    {
        var ff = new FirmsFile { Seed = 31 };
        var f = new SectorFile { Seed = 31 };
        f.City.CopyFrom(city);
        ff.Firms.Add(new Firm { Place = "c", Kind = (int)kind, Reputation = reputation });
        var crews = new List<Crew> { new Crew { Selling = selling } };
        var notes = new List<WorkNote>();
        int offers = 0;
        points = 0;
        for (int d = 1; d <= days; d++)
        {
            notes.Clear();
            SectorRules.Offers(ff, f, d, 1, crews, null, notes);
            for (int i = 0; i < notes.Count; i++)
            {
                Contract w = ff.ContractOf(notes[i].Contract);
                if (notes[i].Kind == FirmsRules.NoteOffered && w != null)
                {
                    offers++;
                    points += w.Points;
                    w.Stage = (int)WorkStage.Declined;   // answered at once, so the list never fills
                }
            }
            ff.Contracts.Clear();
        }
        return offers;
    }

    private static void SectorWork()
    {
        var city = new SectorCityFigures();
        int ordinary = SectorOffers(500, 8, city, 600, out long small);
        Check(ordinary > 600 / 12 && ordinary < 600 / 2, "sector work: an ordinary IT firm wins about an offer a week from the city's work, as before the shared market (" + ordinary + " in 600 days)");
        int known = SectorOffers(900, 40, city, 600, out _);
        var big = new SectorCityFigures();
        big.Set(city.People * 2, city.Small * 2, city.Mid * 2, city.Large * 2, 1000, true);
        int bigger = SectorOffers(500, 8, big, 600, out _);
        Check(known > ordinary * 3 / 2 && bigger > ordinary * 3 / 2, "sector work: a known firm with sellers wins more, and a bigger city puts out more (" + ordinary + ", " + known + ", " + bigger + ")");
        int agency = SectorOffers(500, 8, city, 600, out _, FirmKind.Design), web = SectorOffers(500, 8, city, 600, out _, FirmKind.WebAgency);
        Check(agency > ordinary * 3 / 2 && agency < 600 / 2 && web == agency,
            "sector work: a design studio or web agency wins its work among the city's fewer agencies, nearly twice an IT firm's offers, so its takings hold where the old rule had them ("
            + agency + " in 600 days against an IT firm's " + ordinary + ")");
        // The council's tenders go to firms that take IT work; branding is priced as designers are paid.
        int designTenders = 0, itTenders = 0;
        foreach (FirmKind kind in new[] { FirmKind.Design, FirmKind.Consultancy })
        {
            var tf = new FirmsFile { Seed = 32 };
            var ts = new SectorFile { Seed = 32 };
            tf.Firms.Add(new Firm { Place = "t", Kind = (int)kind, Reputation = 500 });
            var tnotes = new List<WorkNote>();
            for (int d = 1; d <= 200; d++)
            {
                tnotes.Clear();
                SectorRules.Offers(tf, ts, d, 1, new List<Crew> { new Crew { Selling = 8 } }, null, tnotes);
                foreach (WorkNote n in tnotes)
                {
                    Contract w = tf.ContractOf(n.Contract);
                    bool tender = n.Kind == FirmsRules.NoteTender && w != null && FirmsRules.Takes(kind, w.K);
                    designTenders += kind == FirmKind.Design && n.Kind == FirmsRules.NoteTender ? 1 : 0;
                    itTenders += kind == FirmKind.Consultancy && tender ? 1 : 0;
                }
                tf.Contracts.Clear();
            }
        }
        Check(designTenders == 0 && itTenders >= 200 / FirmsCatalogue.TenderEvery - 1,
            "sector work: the council tenders IT work only to firms that take it: a design studio alone none in 200 days, an IT consultancy one every " + FirmsCatalogue.TenderEvery + " days of work it can do (" + itTenders + ")");
        Check(SectorRules.WorkValue(WorkKind.Branding, 100) == FirmsRules.ValueOf(WorkKind.Branding, 100, false) * TalentCatalogue.Role(SectorCatalogue.DesignRole).Pay / 1000
              && TalentCatalogue.Role(SectorCatalogue.DesignRole).Key == "graphic_designer" && SectorRules.WorkValue(WorkKind.Website, 100) == FirmsRules.ValueOf(WorkKind.Website, 100, false),
            "sector work: a branding project is priced as a graphic designer is paid against an engineer, a website at the build rate");
        var bySize = new long[3];
        SectorRules.Demand(city, 1, bySize);
        int largePlain = 0, largeFresh = 0;
        for (int roll = 0; roll < 1000; roll++)
        {
            largePlain += SectorRules.ClientOf(bySize, false, roll) == SectorRules.ClientLarge ? 1 : 0;
            largeFresh += SectorRules.ClientOf(bySize, true, roll) == SectorRules.ClientLarge ? 1 : 0;
        }
        Check(largeFresh > largePlain && SectorRules.PointsFor(WorkKind.Integration, SectorRules.ClientLarge, 500) > SectorRules.PointsFor(WorkKind.Integration, SectorRules.ClientSmall, 500)
              && SectorRules.PointsFor(WorkKind.Website, SectorRules.ClientSmall, 0) >= 1,
            "sector work: a fresh portfolio draws more large clients, and a large client's project is bigger (" + largePlain + " to " + largeFresh + " in a thousand)");
        var ff = new FirmsFile { Seed = 8 };
        ff.Firms.Add(new Firm { Place = "s", Kind = (int)FirmKind.Security });
        for (int i = 0; i < 5; i++)
        {
            ff.Contracts.Add(new Contract { Id = ff.Next(), Place = "s", Kind = (int)WorkKind.Managed, Client = "Client " + i, Stage = (int)WorkStage.Active, Value = FirmsCatalogue.ManagedMonth });
        }
        int incidents = 0;
        var notes = new List<WorkNote>();
        for (int d = 1; d <= 400 && ff.Contracts.Count < SectorCatalogue.ContractsMost; d++)
        {
            incidents += SectorRules.Incidents(ff, d, notes);
        }
        Contract first = null;
        for (int i = 0; i < ff.Contracts.Count && first == null; i++)
        {
            first = ff.Contracts[i].K == WorkKind.Incident ? ff.Contracts[i] : null;
        }
        Check(incidents > 2 && first != null && first.Place == "s" && first.Stage == (int)WorkStage.Offered && first.Client.StartsWith("Client", StringComparison.Ordinal),
            "sector work: a security firm's managed clients are attacked now and then, each attack an incident offered to the firm watching over them (" + incidents + " in 400 days)");
        FirmsRules.Take(first, "s", 500);
        SectorRules.Urgent(first, 500);
        Check(first.Due == 500 + SectorCatalogue.IncidentDays, "sector work: an incident taken on is due in days");
    }

    // ---------------------------------------------------------------- data centres and the cloud

    private static void SectorInfra()
    {
        var f = SectorFresh(13);
        SectorRules.RivalCompute(f);
        int racks = SectorRules.RacksWanted(f.City, 1, 0), scaled = SectorRules.RacksWanted(f.City, 1, EndScaleRules.Full);
        int share = SectorRules.RackShare(1, 600), best = SectorRules.RackShare(3, 900);
        int toLet = racks * share / 1000;
        Check(toLet >= InfraCatalogue.ColoDemand / 2 && toLet <= InfraCatalogue.ColoDemand * 2 && scaled >= racks * 2 && best > share && share < 1000,
            "sector infra: the documented city's firms rent about the racks the old rule let at an even name ("
            + toLet + " of " + racks + " against the old " + InfraCatalogue.ColoDemand + "), more at the end game's scale, more to a better tier and name, never all");
        var cloud = new Cloud { Open = true, Price = 1000, RivalPrice = 1000, Reputation = 600 };
        long market = SectorRules.CloudMarket(f, false, 1, 0, false, 1), gpu = SectorRules.CloudMarket(f, true, 1, 0, false, 1);
        int sold = SectorRules.CloudDemand(cloud, market, int.MaxValue), oldSold = InfraRules.CityDemand(cloud, 1, int.MaxValue, false);
        int soldGpu = SectorRules.CloudDemand(cloud, gpu, int.MaxValue), oldGpu = InfraRules.CityDemand(cloud, 1, int.MaxValue, true);
        Check(sold > oldSold * 6 / 10 && sold < oldSold * 16 / 10 && soldGpu > oldGpu * 5 / 10 && soldGpu < oldGpu * 2,
            "sector infra: an open cloud at an even price and name sells about what the old rule sold, now as a share of the city's compute (" + sold + " against " + oldSold + "; AI " + soldGpu + " against " + oldGpu + ")");
        var cheap = new Cloud { Open = true, Price = 800, RivalPrice = 1000, Reputation = 600 };
        var known = new Cloud { Open = true, Price = 1000, RivalPrice = 1000, Reputation = 950 };
        Check(SectorRules.CloudShare(cheap) > SectorRules.CloudShare(cloud) && SectorRules.CloudShare(known) > SectorRules.CloudShare(cloud) && SectorRules.CloudShare(cheap) < 1000
              && SectorRules.CloudDemand(cloud, market, 10) == 10,
            "sector infra: a cheaper or better known cloud takes more of the city's compute, never all of it (no fixed half for the rivals), and never more than it has");
        long scaleMarket = SectorRules.CloudMarket(f, false, 1, EndScaleRules.Full, false, 1), hyper = SectorRules.CloudMarket(f, false, 1, EndScaleRules.Full, true, 5);
        Check(scaleMarket > market * 20 && hyper > scaleMarket * 3, "sector infra: at the end game's scale the cloud sells far beyond the city, a hyperscaler more and each region past three more");
        f.Rivals[0].Customers *= 2;
        long hostingBefore = f.Hosting;
        SectorRules.RivalCompute(f);
        Check(f.Hosting > hostingBefore && f.Training > 0 && SectorRules.CloudMarket(f, false, 1, 0, false, 1) > market,
            "sector infra: the rivals' software customers and labs need compute too: the cloud's market grows with them");
        var s = new SectorFile { Seed = 3 };
        int spikes = 0, longest = 0, gapLeast = int.MaxValue, last = -1;
        for (int day = 1; day <= 2000; day++)
        {
            if (SectorRules.SpikeDay(s, day))
            {
                spikes++;
                longest = Math.Max(longest, s.SpikeUntil - s.SpikeFrom);
                gapLeast = last >= 0 ? Math.Min(gapLeast, s.SpikeFrom - last) : gapLeast;
                last = s.SpikeFrom;
                bool inside = s.SpikeFactor >= SectorCatalogue.SpikeLeast && s.SpikeFactor <= SectorCatalogue.SpikeMost && SectorRules.PowerFactor(s, day) == s.SpikeFactor;
                s.Hedged = true;
                inside &= SectorRules.PowerFactor(s, day) == 1000;
                s.Hedged = false;
                Check(inside, "sector infra: a spike's tariff is in its range, and a price fixed at the desk pays the usual");
            }
        }
        Check(spikes >= 2000 / (SectorCatalogue.SpikeGapMost + SectorCatalogue.SpikeDaysMost) && longest <= SectorCatalogue.SpikeDaysMost && gapLeast >= SectorCatalogue.SpikeGapLeast,
            "sector infra: power price spikes come every one to three months and last days (" + spikes + " in 2,000 days)");
        Check(SectorRules.PowerFactor(s, s.SpikeUntil) == 1000 && SectorRules.GpuWorking(new SectorFile { ThrottleUntil = 5 }, 4) == 500 && SectorRules.OutageFactor(new SectorFile { FailoverUntil = 9 }, 3) == 600,
            "sector infra: a spike ends on its day; throttled AI racks give half, a post-mortem takes outages down");
    }
}
