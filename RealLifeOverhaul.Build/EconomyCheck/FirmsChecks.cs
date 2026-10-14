using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

// Checks for tech firms (Scripts/Tech/Firms): building features, bugs and debt; selling, churn and takings; games and
// licences; outages, breaches and fines; contracts, managed services and tenders; value, relief and the save line.
internal static partial class Program
{
    private static void Firms()
    {
        FirmsBuild();
        FirmsSell();
        FirmsGames();
        FirmsRisks();
        FirmsContracts();
        FirmsTenders();
        FirmsValue();
        FirmsSave();
        TechWork();
    }

    private static Crew Team(int engineering, int seniorShare = 500, int faults = 150)
    {
        return new Crew { Engineering = engineering, SeniorShare = seniorShare, Faults = faults, Security = 300, Reliability = 300, Seats = Math.Max(1, engineering / 8), Support = 2 };
    }

    private static Product Saas(FirmsFile f, Segment segment, int features, long users)
    {
        Product p = FirmsRules.NewProduct(f, "p", ProductKind.Saas, segment, 0, "Test");
        p.Features = features;
        p.Users = users;
        p.Stage = 1;
        f.Products.Add(p);
        return p;
    }

    private static void FirmsBuild()
    {
        Check(FirmsRules.FeatureCost(0) == FirmsCatalogue.FeatureBase && FirmsRules.FeatureCost(5) == FirmsCatalogue.FeatureBase + 5 * FirmsCatalogue.FeatureStep, "firms: each feature costs more than the last");
        var f = new FirmsFile { Seed = 3 };
        var firm = new Firm { Place = "p", Focus = (int)Focus.Features };
        Product p = FirmsRules.NewProduct(f, "p", ProductKind.Saas, Segment.Business, 0, null);
        int days = 0;
        while (!FirmsRules.CanLaunch(p) && days < 200)
        {
            FirmsRules.Build(firm, p, Team(32), 32, 0);
            days++;
        }
        Check(FirmsRules.CanLaunch(p) && days >= 3 && days <= 12, "firms: four engineers build a product to launch in a week or two (" + days + " days)");
        Check(p.Bugs > 0 && firm.Debt > 0, "firms: building leaves bugs and debt");
        var juniors = new Firm { Place = "j", Focus = (int)Focus.Features };
        var seniors = new Firm { Place = "s", Focus = (int)Focus.Features };
        Product pj = FirmsRules.NewProduct(f, "j", ProductKind.Saas, Segment.Business, 0, null), ps = FirmsRules.NewProduct(f, "s", ProductKind.Saas, Segment.Business, 0, null);
        for (int d = 0; d < 20; d++)
        {
            FirmsRules.Build(juniors, pj, Team(32, 0, 250), 32, 0);
            FirmsRules.Build(seniors, ps, Team(32, 900, 60), 32, 0);
        }
        Check(seniors.Debt < juniors.Debt && ps.Bugs < pj.Bugs, "firms: seniors build with less debt and fewer bugs");
        var fixer = new Firm { Place = "q", Focus = (int)Focus.Quality, Debt = 400 };
        Product pq = FirmsRules.NewProduct(f, "q", ProductKind.Saas, Segment.Business, 0, null);
        pq.Bugs = 200;
        pq.Features = 5;
        FirmsRules.Build(fixer, pq, Team(40), 40, 8);
        Check(pq.Bugs < 200 - 15, "firms: fixing takes bugs out (" + pq.Bugs + " left of 200)");
        var payer = new Firm { Place = "d", Focus = (int)Focus.Stability, Debt = 500 };
        FirmsRules.Build(payer, pq, Team(40), 40, 0);
        Check(payer.Debt < 500, "firms: a focus on debt pays it down");
        Product clean = FirmsRules.NewProduct(f, "c", ProductKind.Saas, Segment.Business, 0, null);
        clean.Features = 6;
        Check(FirmsRules.Quality(clean) == 1000 && FirmsRules.Quality(pq) < 1000, "firms: a bug-free product is at full quality");
        Product full = FirmsRules.NewProduct(f, "c", ProductKind.Saas, Segment.Business, 0, null);
        full.Features = FirmsCatalogue.FeaturesMost;
        FirmsRules.Build(new Firm(), full, Team(400), 400, 0);
        Check(full.Features == FirmsCatalogue.FeaturesMost, "firms: no more features than the most");
        var withPm = Team(32);
        withPm.Product = 8;
        Check(FirmsRules.ProductBoost(withPm) > 0 && FirmsRules.ProductBoost(Team(32)) == 0 && FirmsRules.ProductBoost(new Crew { Product = 100 }) <= 400, "firms: product managers make features go further, up to two fifths");
    }

    private static void FirmsSell()
    {
        var f = new FirmsFile { Seed = 5 };
        var firm = new Firm { Place = "p", Reputation = 500 };
        Product strong = Saas(f, Segment.Business, 14, 0), weak = Saas(f, Segment.Business, 4, 0);
        Check(FirmsRules.Appeal(strong, firm, Team(16)) > FirmsRules.Appeal(weak, firm, Team(16)), "firms: more features than the rival appeal more");
        long sWon = 0, wWon = 0;
        for (int d = 1; d <= 30; d++)
        {
            sWon += FirmsRules.Sell(f, firm, strong, Team(16), 20, d, 1, 1000).Won;
            wWon += FirmsRules.Sell(f, firm, weak, Team(16), 20, d, 1, 1000).Won;
        }
        Check(sWon > wWon * 2 && sWon > 0, "firms: the stronger product wins more customers (" + sWon + " against " + wWon + ")");
        Product boom = Saas(f, Segment.Business, 10, 0), bust = Saas(f, Segment.Business, 10, 0);
        long boomWon = 0, bustWon = 0;
        for (int d = 1; d <= 30; d++)
        {
            boomWon += FirmsRules.Sell(f, firm, boom, Team(16), 20, d, 0, 1000).Won;
            bustWon += FirmsRules.Sell(f, firm, bust, Team(16), 20, d, 3, 1000).Won;
        }
        Check(boomWon > bustWon, "firms: a boom sells more software than a recession");
        Product clean = Saas(f, Segment.Business, 10, 5000), buggy = Saas(f, Segment.Business, 10, 5000);
        buggy.Bugs = 500;
        long cLost = 0, bLost = 0;
        for (int d = 1; d <= 30; d++)
        {
            cLost += FirmsRules.Sell(f, firm, clean, Team(16), 100, 100 + d, 1, 1000).Lost;
            bLost += FirmsRules.Sell(f, firm, buggy, Team(16), 100, 100 + d, 1, 1000).Lost;
        }
        Check(bLost > cLost, "firms: a buggy product loses more customers (" + cLost + " against " + bLost + ")");
        Product served = Saas(f, Segment.Business, 10, 5000), unserved = Saas(f, Segment.Business, 10, 5000);
        long sl = 0, ul = 0;
        for (int d = 1; d <= 30; d++)
        {
            sl += FirmsRules.Sell(f, firm, served, Team(16), 100, 200 + d, 1, 1000).Lost;
            ul += FirmsRules.Sell(f, firm, unserved, Team(16), 0, 200 + d, 1, 1000).Lost;
        }
        Check(ul > sl, "firms: a product short of support loses more customers");
        Product billed = Saas(f, Segment.Business, 10, 3000);
        long monthly = FirmsRules.MonthlyRevenue(billed);
        Check(monthly == 3000L * FirmsCatalogue.Price[1], "firms: recurring revenue is customers times the price");
        billed.Tier = 2;
        Check(FirmsRules.MonthlyRevenue(billed) > monthly, "firms: a premium tier charges more");
        Check(FirmsRules.SupportNeeded(Saas(f, Segment.Business, 5, 10000)) == 4, "firms: ten thousand businesses need four support people");
        // a year of a decent product with steady marketing lands in a sensible place
        Product year = Saas(f, Segment.Business, 10, 0);
        year.Marketing = 2;
        for (int d = 1; d <= 365; d++)
        {
            FirmsRules.Sell(f, firm, year, Team(16), 10, 300 + d, 1, 1000);
        }
        long mrr = FirmsRules.MonthlyRevenue(year);
        Check(mrr > 1000000 && mrr < 50000000, "firms: a year on sale makes a small business service worth ten thousand to half a million a month (" + mrr / 100 + ")");
        Product cheap = Saas(f, Segment.Business, 10, 0), dear = Saas(f, Segment.Business, 10, 0);
        dear.Tier = 2;
        cheap.Tier = 0;
        Check(FirmsRules.Appeal(cheap, firm, Team(8)) > FirmsRules.Appeal(dear, firm, Team(8)), "firms: a lower price appeals more");
        Product consumer = Saas(f, Segment.Consumer, 10, 0);
        long won = 0;
        for (int d = 1; d <= 30; d++)
        {
            won += FirmsRules.Sell(f, firm, consumer, Team(16), 10, 500 + d, 1, 1000).Won;
        }
        Check(consumer.Free > won * 10, "firms: most of a consumer service's users do not pay (" + consumer.Free + " free, " + won + " paying)");
    }

    private static void FirmsGames()
    {
        var f = new FirmsFile { Seed = 7 };
        var firm = new Firm { Place = "g", Reputation = 600 };
        Product g = FirmsRules.NewProduct(f, "g", ProductKind.Game, Segment.Business, 0, null);
        Check(g.Segment == (int)Segment.Consumer, "firms: games are sold to consumers");
        int days = 0;
        while (!FirmsRules.GameReady(g) && days < 400)
        {
            g.Marketing = 3;
            FirmsRules.Build(firm, g, Team(48), 48, 8);
            FirmsRules.Hype(g);
            days++;
        }
        Check(FirmsRules.CanLaunch(g) && g.Hype > 0, "firms: a small game is built whole and hyped before launch (" + days + " days)");
        FirmsRules.Launch(g, 100);
        long first = FirmsRules.Sell(f, firm, g, Team(48), 5, 100, 1, 1000).Won, later = FirmsRules.Sell(f, firm, g, Team(48), 5, 100 + FirmsCatalogue.GameHalfLife * 2, 1, 1000).Won;
        Check(first > 0 && later < first / 2, "firms: a game's sales fall away after launch (" + first + " to " + later + ")");
        Product lic = FirmsRules.NewProduct(f, "g", ProductKind.Licence, Segment.Consumer, 0, null);
        Check(lic.Segment == (int)Segment.Enterprise, "firms: licences are sold to large companies");
        lic.Features = 12;
        lic.Stage = 1;
        var sellers = Team(8);
        sellers.Selling = 32;
        long deals = 0, takings = 0;
        for (int d = 1; d <= 200; d++)
        {
            ProductDay r = FirmsRules.Sell(f, firm, lic, sellers, 10, d, 1, 1000);
            deals += r.Won;
            takings += r.Takings;
        }
        Check(deals > 0 && takings >= deals * FirmsCatalogue.LicenceDeal * 700 / 1000, "firms: sellers close licence deals that pay in full (" + deals + ")");
        Check(FirmsRules.MonthlyRevenue(lic) > 0, "firms: licences bring upkeep each month");
    }

    private static void FirmsRisks()
    {
        int weakOut = 0, strongOut = 0, weakBreach = 0, strongBreach = 0;
        for (int run = 0; run < 3; run++)
        {
            var f = new FirmsFile { Seed = 11 + run };
            var fragile = new Firm { Place = "a", Debt = 800 };
            var solid = new Firm { Place = "b", Debt = 0 };
            Product a = Saas(f, Segment.Business, 10, 5000), b = Saas(f, Segment.Business, 10, 5000);
            var weak = Team(16);
            weak.Reliability = 0;
            weak.Security = 0;
            var strong = Team(16);
            strong.Reliability = 900;
            strong.Security = 900;
            for (int d = 1; d <= 1000; d++)
            {
                a.Users = b.Users = 5000;
                ProductDay ra = FirmsRules.Sell(f, fragile, a, weak, 100, d, 1, 1000), rb = FirmsRules.Sell(f, solid, b, strong, 100, d, 1, 1000);
                weakOut += ra.Outage ? 1 : 0;
                strongOut += rb.Outage ? 1 : 0;
                weakBreach += ra.Breach ? 1 : 0;
                strongBreach += rb.Breach ? 1 : 0;
            }
        }
        Check(weakOut > strongOut * 3 && weakOut > 0, "firms: debt and nobody keeping it running mean outages (" + weakOut + " against " + strongOut + ")");
        Check(weakBreach > strongBreach && weakBreach > 0, "firms: a weak security posture means breaches (" + weakBreach + " against " + strongBreach + ")");
        Check(FirmsRules.Fine(10, 0, true) == FirmsCatalogue.FineLeast, "firms: a fine is never under the floor");
        Check(FirmsRules.Fine(10000000, 1000000000, true) == 1000000000L * FirmsCatalogue.FineShareOfYear / 1000, "firms: a fine is at most a share of a year's revenue");
        Check(FirmsRules.Fine(200000, 0, false) == FirmsRules.Fine(200000, 0, true) * FirmsCatalogue.UnregisteredFine / 1000, "firms: half again off the register");
    }

    private static void FirmsContracts()
    {
        var f = new FirmsFile { Seed = 13 };
        f.Firms.Add(new Firm { Place = "c", Kind = (int)FirmKind.Consultancy, Reputation = 700 });
        f.Firms.Add(new Firm { Place = "s", Kind = (int)FirmKind.Software });
        var crews = new List<Crew> { Team(24), Team(24) };
        var own = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("shop~1", "My Shop") };
        var notes = new List<WorkNote>();
        for (int d = 1; d <= 60; d++)
        {
            FirmsRules.Offers(f, d, crews, own, notes);
            FirmsRules.Lapse(f, d, notes);
        }
        int offers = 0, tenders = 0, mine = 0;
        for (int i = 0; i < f.Contracts.Count; i++)
        {
            offers += f.Contracts[i].Council ? 0 : 1;
            tenders += f.Contracts[i].Council ? 1 : 0;
            mine += f.Contracts[i].OwnPlace.Length > 0 ? 1 : 0;
        }
        Check(offers > 0 && tenders >= 2 && f.Contracts.Count <= FirmsCatalogue.ContractsMost, "firms: firms are offered work and the council tenders (" + offers + " offers, " + tenders + " tenders)");
        Check(mine > 0, "firms: some clients are the player's own businesses");
        bool kinds = true;
        for (int i = 0; i < f.Contracts.Count; i++)
        {
            Contract w = f.Contracts[i];
            Firm to = f.FirmAt(w.Place);
            kinds &= w.Council || to != null && FirmsRules.Takes(to.K, w.K);
        }
        Check(kinds, "firms: an offer is for work its firm takes on");
        var firm = new Firm { Place = "c", Reputation = 500 };
        var job = new Contract { Id = 1, Kind = (int)WorkKind.Website, Points = 100, Value = FirmsRules.ValueOf(WorkKind.Website, 100, false), Stage = (int)WorkStage.Offered };
        long upfront = FirmsRules.Take(job, "c", 10);
        Check(upfront == job.Value * FirmsCatalogue.UpfrontShare / 1000 && job.Due == 10 + FirmsCatalogue.WorkDays[0], "firms: a project pays its first share when taken");
        long rest = 0;
        for (int d = 10; d < 30 && job.Stage == (int)WorkStage.Active; d++)
        {
            rest += FirmsRules.Work(job, firm, FirmsRules.Need(job, d), d, notes);
        }
        Check(job.At == WorkStage.Done && job.Paid == job.Value && upfront + rest == job.Value, "firms: a project done on time is paid in full");
        var late = new Contract { Id = 2, Kind = (int)WorkKind.Website, Points = 100, Value = 1000000 };
        FirmsRules.Take(late, "c", 0);
        FirmsRules.Work(late, firm, 50, 5, notes);
        long paidLate = FirmsRules.Work(late, firm, 50, late.Due + 5, notes);
        Check(late.At == WorkStage.Done && late.Paid == 1000000 - 1000000 * FirmsCatalogue.LatePenalty / 1000 * 5, "firms: a project late by five days loses a tenth");
        var stalled = new Contract { Id = 3, Kind = (int)WorkKind.Website, Points = 100, Value = 1000000 };
        FirmsRules.Take(stalled, "c", 0);
        FirmsRules.Work(stalled, firm, 0, stalled.Due * 2 + 1, notes);
        Check(stalled.At == WorkStage.Failed, "firms: a project not done in twice its time fails");
        var managed = new Contract { Id = 4, Kind = (int)WorkKind.Managed, Value = FirmsCatalogue.ManagedMonth };
        FirmsRules.Take(managed, "c", 0);
        long billed = 0;
        for (int d = 1; d <= 90; d++)
        {
            billed += FirmsRules.Work(managed, firm, FirmsCatalogue.ManagedPoints, d, notes);
        }
        Check(billed == 3 * FirmsCatalogue.ManagedMonth && managed.At == WorkStage.Active, "firms: a managed service bills each month while it is kept up");
        var neglected = new Contract { Id = 5, Kind = (int)WorkKind.Managed, Value = FirmsCatalogue.ManagedMonth };
        FirmsRules.Take(neglected, "c", 0);
        for (int d = 1; d <= 40; d++)
        {
            FirmsRules.Work(neglected, firm, 0, d, notes);
        }
        Check(neglected.At == WorkStage.Failed, "firms: a neglected client goes");
        Check(FirmsRules.ValueOf(WorkKind.Audit, 100, false) > FirmsRules.ValueOf(WorkKind.Website, 100, false) && FirmsRules.ValueOf(WorkKind.Website, 100, true) > FirmsRules.ValueOf(WorkKind.Website, 100, false),
            "firms: security work is dearer, a council job bigger");
    }

    private static void FirmsTenders()
    {
        int cheapWins = 0, dearWins = 0;
        for (int i = 0; i < 300; i++)
        {
            var f = new FirmsFile { Seed = 100 + i };
            var t = new Contract { Id = i, Kind = (int)WorkKind.Integration, Value = 5000000, Rivals = 3, Stage = (int)WorkStage.Bid };
            var firm = new Firm { Reputation = 600 };
            t.Bid = FirmsRules.BidAt(t, 800);
            cheapWins += FirmsRules.Decide(f, t, firm, 1) ? 1 : 0;
            var u = new Contract { Id = i, Kind = (int)WorkKind.Integration, Value = 5000000, Rivals = 3, Stage = (int)WorkStage.Bid };
            u.Bid = FirmsRules.BidAt(u, 1200);
            dearWins += FirmsRules.Decide(f, u, firm, 1) ? 1 : 0;
        }
        Check(cheapWins > dearWins * 3, "firms: a lower bid wins more tenders (" + cheapWins + " against " + dearWins + " of 300)");
        var g = new FirmsFile { Seed = 1 };
        var none = new Contract { Value = 100, Stage = (int)WorkStage.Tender };
        Check(!FirmsRules.Decide(g, none, new Firm(), 1) && none.At == WorkStage.Lost, "firms: no bid, no contract");
    }

    private static void FirmsValue()
    {
        var f = new FirmsFile { Seed = 2 };
        Product p = Saas(f, Segment.Business, 10, 1000);
        long arr = FirmsRules.Arr(f, "p");
        Check(arr == FirmsRules.MonthlyRevenue(p) * 12, "firms: recurring revenue a year is twelve months'");
        var growing = new Firm { Debt = 0 };
        var indebted = new Firm { Debt = 900 };
        Check(FirmsRules.Value(growing, arr, 2000) > FirmsRules.Value(growing, arr, 0) && FirmsRules.Value(growing, arr, 0) > FirmsRules.Value(indebted, arr, 0), "firms: growth adds to a firm's value and debt takes from it");
        Check(FirmsRules.Relief(1000000) == 1000000 * FirmsCatalogue.ReliefShare / 1000 && FirmsRules.Relief(-5) == 0, "firms: the relief is a share of the qualifying costs");
        var roll = new Firm();
        for (int d = 0; d < 45; d++)
        {
            FirmsRules.Roll(roll, d, 100);
        }
        Check(roll.Revenue30 == 3000, "firms: the last thirty days of takings roll");
    }

    private static void FirmsSave()
    {
        var f = new FirmsFile { Seed = 9, FirstDay = 2, LastDay = 40, LastTender = 33 };
        var firm = new Firm { Place = "Long%20Rd~7", Kind = (int)FirmKind.Security, Debt = 120, Reputation = 640, RegisteredUntil = 99, RnD = 123456789, Revenue30 = 5000 };
        firm.Days[3] = 777;
        f.Firms.Add(firm);
        Product p = Saas(f, Segment.Enterprise, 7, 12);
        p.Place = "Long%20Rd~7";
        p.Name = "Quill|desk";
        p.Carry = 999999;
        f.Contracts.Add(new Contract { Id = f.Next(), Place = "Long%20Rd~7", Kind = (int)WorkKind.PenTest, Client = "A; B", OwnPlace = "x~1", Points = 80, Value = 1200000, Stage = (int)WorkStage.Active, Due = 50 });
        int version;
        FirmsFile back = FirmsStore.Read(FirmsStore.Write(f), out version);
        Firm b = back.FirmAt("Long%20Rd~7");
        Check(back.Seed == 9 && back.LastTender == 33 && b != null && b.Kind == (int)FirmKind.Security && b.RnD == 123456789 && b.Days[3] == 777 && b.Revenue30 == 5000, "firms: a firm comes back as written");
        Product bp = back.ProductOf(p.Id);
        Check(bp != null && bp.Features == 7 && bp.Users == 12 && bp.Segment == (int)Segment.Enterprise && bp.Carry == 999999 && bp.Name.IndexOf('|') < 0, "firms: a product comes back as written");
        Contract bc = back.Contracts[0];
        Check(bc.K == WorkKind.PenTest && bc.OwnPlace == "x~1" && bc.Value == 1200000 && bc.Due == 50, "firms: a contract comes back as written");
    }
}
