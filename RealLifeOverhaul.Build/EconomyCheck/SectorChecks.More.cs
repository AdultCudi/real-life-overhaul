using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;

// The other tech firms' desk sessions, an older save taken in, the save's line and the shared city's part, and the screens
// (SectorChecks.cs runs them).
internal static partial class Program
{
    // ---------------------------------------------------------------- the desk sessions

    private static void SectorDesk()
    {
        // sprint planning
        var sf = new SectorFirm { Place = "p" };
        SectorRules.Sprint(sf, SectorRules.SprintRequests, SectorRules.ReleaseWeekly, 10);
        Check(SectorRules.SprintFocus(sf, (int)Focus.Balanced, 12) == (int)Focus.Features && SectorRules.SprintPoints(sf, 12) > 1000 && SectorRules.SprintFaults(sf, 12) > 1300
              && SectorRules.SprintFocus(sf, (int)Focus.Balanced, 17) == (int)Focus.Balanced && SectorRules.SprintPoints(sf, 17) == 1000 && SectorRules.SprintFaults(sf, 17) == 1000,
            "sector desk: a sprint of customer requests shipped weekly builds faster with more bugs, for its week only");
        SectorRules.Sprint(sf, SectorRules.SprintDebt, SectorRules.ReleaseReady, 20);
        Check(SectorRules.SprintFocus(sf, (int)Focus.Features, 21) == (int)Focus.Stability && SectorRules.SprintFaults(sf, 21) < 1000 && SectorRules.Reliable(sf, 500, 21) > 500
              && SectorRules.Reliable(sf, 500, 40) == 500,
            "sector desk: a sprint of debt and reliability pays debt down, ships fewer bugs when ready and keeps services sounder that week");
        Check(SectorRules.PickSprint(0, 600, false, false, 900) == SectorRules.SprintDebt && SectorRules.PickSprint(0, 100, false, true, 900) == SectorRules.SprintRequests
              && SectorRules.PickSprint(0, 100, false, false, 900) == SectorRules.SprintBalanced && SectorRules.PickSprint(1, 0, false, false, 500) == SectorRules.ReleaseReady,
            "sector desk: a manager plans debt first when deep in it, customers first behind the market, else balanced; ships when ready while bugs weigh");
        // an outage's response
        var ff = new FirmsFile { Seed = 4 };
        var firm = new Firm { Place = "p", Reputation = 600 };
        Product p = FirmsRules.NewProduct(ff, "p", ProductKind.Saas, Segment.Business, 0, "Down");
        p.Users = 1000;
        p.Features = 10;
        p.Progress = 50;
        FirmsRules.Launch(p, 1);
        sf.LostToOutage = 200;
        DeskResult rolled = SectorRules.Outage(sf, p, firm, SectorRules.OutageRollback, SectorRules.AfterPostMortem, 4, 30);
        Check(rolled.Done && p.Users == 1160 && p.Progress == 0 && sf.ReliableUntil == 30 + SectorCatalogue.PostMortemDays && sf.LostToOutage == 0,
            "sector desk: a roll back brings most of the lost customers back and loses the next feature's work; a post-mortem keeps the service sounder for two weeks");
        int risky = 0;
        for (int i = 0; i < 400; i++)
        {
            var s2 = new SectorFirm { LostToOutage = 100 };
            Product q = FirmsRules.NewProduct(ff, "p", ProductKind.Saas, Segment.Business, 0, "Q");
            q.Users = 1000;
            risky += SectorRules.Outage(s2, q, new Firm { Reputation = 600 }, SectorRules.OutageHotfix, SectorRules.AfterMoveOn, 4, i).Figure2;
        }
        Check(risky > 400 * SectorCatalogue.HotfixRisk / 2000 && risky < 400 * SectorCatalogue.HotfixRisk * 2 / 1000,
            "sector desk: a hotfix forward goes wrong about a quarter of the time (" + risky + " of 400)");
        DeskResult credited = SectorRules.Outage(new SectorFirm(), p, firm, SectorRules.OutageStatus, SectorRules.AfterCredit, 4, 40);
        Check(credited.Cost > 0 && credited.Figure == 0, "sector desk: a credit to customers costs money; a status page brings nobody back");
        // a big customer
        var big = new SectorFirm();
        DeskResult built = SectorRules.BigCustomer(big, p, SectorRules.BigBuild, 50);
        long had = p.Users;
        DeskResult refused = SectorRules.BigCustomer(big, p, SectorRules.BigRefuse, 64);
        Check(built.Income == SectorCatalogue.BigDeal * (long)FirmsCatalogue.Price[2] && SectorRules.Diverted(big, 40, 55) < 40 && SectorRules.Diverted(big, 40, 50 + SectorCatalogue.BigDivertDays) == 40
              && refused.Figure > 0 && p.Users < had && SectorRules.PickBigCustomer(32) == SectorRules.BigBuild && SectorRules.PickBigCustomer(8) == SectorRules.BigDiscount,
            "sector desk: building a big customer's feature pays now and takes engineers for two weeks; refusing loses customers; a manager builds with engineers to spare");
        // a training run and its benchmark
        SectorFile f = SectorFresh(17);
        Product model = FirmsRules.NewProduct(ff, "lab", ProductKind.Model, Segment.Business, 0, "Mind");
        model.Features = 60;
        var sp = new SectorProduct { Id = model.Id };
        var lab = new Firm { Place = "lab", Reputation = 500 };
        DeskResult run = SectorRules.Training(f, model, sp, lab, SectorRules.ComputeOwn, SectorRules.DataLicensed, SectorRules.SafetyFull, 0, 17, 80);
        Check(run.Done && model.Compute > 0 && run.Cost > SectorCatalogue.DataCost[0] && sp.Benchmark > 50 && run.Story == SectorRules.DeskStoryBenchmark && run.Rival >= 0 && lab.Reputation > 500
              && SectorRules.Held(sp, 80 + SectorCatalogue.SafetyDays[0] - 1) && !SectorRules.Held(sp, 80 + SectorCatalogue.SafetyDays[0]),
            "sector desk: a strong model's run (on rented compute with no accelerators of the player's) beats the rival labs' benchmark, earns a name and holds its next training for its checks (score "
            + sp.Benchmark + ")");
        Product weak = FirmsRules.NewProduct(ff, "lab", ProductKind.Model, Segment.Business, 0, "Small");
        weak.Features = 1;
        var wsp = new SectorProduct { Id = weak.Id };
        DeskResult lose = SectorRules.Training(f, weak, wsp, lab, SectorRules.ComputeSmaller, SectorRules.DataSynthetic, SectorRules.SafetySkip, 0, 17, 81);
        Check(lose.Story == SectorRules.DeskStoryNone && wsp.Benchmark < 50 && lose.Figure2 < run.Figure2 && wsp.Data / 10 == SectorRules.SafetySkip,
            "sector desk: a weak model on half a run loses the benchmark; skipped checks are remembered for its launch");
        // a model's launch
        weak.Stage = 0;
        weak.Features = FirmsCatalogue.LaunchFeatures;
        weak.Compute = 0;
        Check(SectorRules.HasLaunch(weak, wsp), "sector desk: a model ready to go on sale has a launch to hold");
        int incidents = 0;
        for (int i = 0; i < 300; i++)
        {
            Product m = FirmsRules.NewProduct(ff, "lab", ProductKind.Model, Segment.Business, 0, "M");
            m.Features = FirmsCatalogue.LaunchFeatures;
            m.Users = 100;
            var msp = new SectorProduct { Id = m.Id, Data = SectorRules.SafetySkip * 10 };
            DeskResult l = SectorRules.Launch(m, msp, new Firm { Reputation = 500 }, SectorRules.ReleasePartners, SectorRules.PriceHigh, 3, i);
            incidents += l.Figure2;
            if (i == 0)
            {
                Check(l.Launched && m.Live && m.Tier == 2 && msp.Generation == m.Features && SectorRules.Boost(msp, i + 1) == SectorCatalogue.LaunchAwareness[2] && !SectorRules.HasLaunch(m, msp),
                    "sector desk: a launch puts a ready model on sale at its price with partners signed, its awareness lifted for a month, and nothing to launch again until a new generation");
            }
        }
        Check(incidents > 300 * SectorCatalogue.SafetyIncident[2] / 2000 && incidents < 300 * SectorCatalogue.SafetyIncident[2] * 2 / 1000,
            "sector desk: a launch after skipped safety checks goes wrong about a fifth of the time (" + incidents + " of 300)");
        // a pitch and a tender
        var tender = new Contract { Id = 900, Kind = (int)WorkKind.Integration, Council = true, Stage = (int)WorkStage.Tender, Value = 4000000, Rivals = 3 };
        var csf = new SectorFirm();
        DeskResult bid = SectorRules.Pitch(tender, csf, "c", SectorRules.StaffSeniors, SectorRules.PriceLow, true, 5, 90);
        Check(bid.Figure == 1 && tender.Stage == (int)WorkStage.Bid && tender.Bid == 4000000L * SectorCatalogue.PitchPrice[0] / 1000 && SectorRules.PitchOf(csf, tender) == SectorCatalogue.PitchSeniorWeight
              && SectorRules.PitchOf(csf, new Contract { Id = 1 }) == 0,
            "sector desk: a pitch for a tender bids at its price and weighs better with seniors on it");
        var offer = new Contract { Id = 901, Kind = (int)WorkKind.Integration, Stage = (int)WorkStage.Offered, Points = 300, Value = 3300000 };
        DeskResult taken = SectorRules.Pitch(offer, csf, "c", SectorRules.StaffJuniors, SectorRules.PriceFair, false, 5, 90);
        Check(taken.Figure == 2 && offer.Stage == (int)WorkStage.Active && offer.Points > 300 && taken.Income == offer.Value * FirmsCatalogue.UpfrontShare / 1000,
            "sector desk: a pitch for an offer takes it; juniors take longer");
        // scope creep and revisions
        var project = new Contract { Id = 902, Kind = (int)WorkKind.Integration, Stage = (int)WorkStage.Active, Points = 400, Done = 200, Value = 4400000, Satisfaction = 800, Start = 10, Due = 55 };
        Check(SectorRules.Creeps(project) && SectorRules.PickScope(project) == SectorRules.ScopeCharge, "sector desk: a project half done invites scope creep; a manager charges a happy client");
        SectorRules.Scope(project, firm, SectorRules.ScopeCharge);
        Check(project.Points == 480 && project.Value > 4400000 && project.Due == 64 && project.Satisfaction == 750 && !SectorRules.Creeps(project),
            "sector desk: a change order adds the work, is paid for it and agrees a date a fifth later for it, once");
        var design = new Contract { Id = 903, Kind = (int)WorkKind.Branding, Stage = (int)WorkStage.Active, Points = 100, Done = 60, Value = 1100000, Satisfaction = 700 };
        Check(SectorRules.WantsChanges(design) && SectorRules.PickRevision(design) == SectorRules.RevisionFree, "sector desk: a design past half wants changes; a manager gives an unhappy client one free round");
        SectorRules.Revision(design, firm, SectorRules.RevisionFree);
        Check(design.Points == 115 && design.Satisfaction == 800 && !SectorRules.WantsChanges(design), "sector desk: a free round adds work and goodwill, once");
        // an incident
        var incident = new Contract { Id = 904, Kind = (int)WorkKind.Incident, Stage = (int)WorkStage.Active, Points = 40, Satisfaction = 700 };
        DeskResult answered = SectorRules.Incident(incident, firm, SectorRules.ContainTargeted, SectorRules.TellNow, true, 3, 95);
        Check(answered.Figure == 24 && incident.Done == 24 && incident.Satisfaction == 800 && SectorRules.PickIncident(0, true) == SectorRules.ContainTargeted && SectorRules.PickIncident(0, false) == SectorRules.ContainIsolate,
            "sector desk: targeted containment with seniors does most of an incident's work, telling the client at once wins their trust");
        // data centres
        var infra = new InfraFile();
        infra.Centres.Add(new DataCentre { Place = "dc", Tier = 1, Sqm = 6000, Racks = 50, Colo = 10 });
        infra.Cloud.Open = true;
        infra.Cloud.RivalPrice = 900;
        var sector = new SectorFile();
        DeskResult planned = SectorRules.Capacity(sector, infra, SectorRules.AheadBuy, SectorRules.PriceLow, 0, 100);
        Check(planned.Racks == SectorCatalogue.CapacityRacks && planned.Cost == planned.Racks * InfraCatalogue.RackCost * SectorCatalogue.CapacityAhead / 1000 && infra.Cloud.Price == 900 * 900 / 1000
              && sector.LastCapacity == 100,
            "sector desk: capacity planned buys racks ahead at a tenth off and sets the cloud's price against the rivals'");
        SectorRules.Capacity(sector, infra, SectorRules.AheadLet, SectorRules.PriceFair, 0, 114);
        Check(sector.ColoTarget > 10 && SectorRules.PickCapacity(0, 90, 100) == SectorRules.AheadBuy && SectorRules.PickCapacity(0, 10, 100) == SectorRules.AheadAsNeeded,
            "sector desk: letting more racks raises the racks let to the city's firms; a manager buys ahead when the cloud sells most of what it has");
        sector.SpikeFrom = 200;
        sector.SpikeUntil = 206;
        sector.SpikeFactor = 2400;
        Check(SectorRules.SpikeWaits(sector, 201) && SectorRules.PickPower(sector, 201) == SectorRules.PowerHedge, "sector desk: a big spike waits to be answered; a manager fixes its price");
        DeskResult hedged = SectorRules.Power(sector, SectorRules.PowerHedge, 3000000, 201);
        Check(hedged.Cost == 3000000L * SectorCatalogue.HedgeFee / 1000 && sector.Hedged && !SectorRules.SpikeWaits(sector, 202) && SectorRules.PowerFactor(sector, 202) == 1000,
            "sector desk: fixing the price for a spike costs a fee and the usual tariff is paid");
        var down = new Cloud { Reputation = 500 };
        SectorRules.Failover(sector, down, 1, SectorRules.FailOver, SectorRules.AfterPostMortem, 100000, 300);
        Check(down.Reputation == 480 && SectorRules.OutageFactor(sector, 310) < 1000, "sector desk: with one region there is nowhere to fail over to: restored in place; a post-mortem takes outages down");
        var two = new Cloud { Reputation = 500 };
        SectorRules.Failover(sector, two, 2, SectorRules.FailOver, SectorRules.AfterCredit, 100000, 300);
        Check(two.Reputation == 600, "sector desk: failing over to a second region keeps the cloud's name, and a credit earns trust");
        // the web agency and the design studio
        var pitch = new Contract { Id = 905, Kind = (int)WorkKind.Website, Stage = (int)WorkStage.Offered, Points = 100, Value = 1100000 };
        SectorRules.ClientPitch(pitch, "w", SectorRules.ConceptCheap, SectorRules.PaceFast, 3, 120);
        Check(pitch.Stage == (int)WorkStage.Active && pitch.Value == 880000 && pitch.Due - pitch.Start < FirmsCatalogue.WorkDays[0], "sector desk: a cheap pitch takes the client for a fifth off, a fast promise brings the date forward");
        var studio = new Firm { Place = "w", Reputation = 500 };
        var wsf = new SectorFirm();
        SectorRules.Portfolio(studio, wsf, SectorRules.PortfolioPolish, 3, 130);
        int awards = 0;
        for (int i = 0; i < 400; i++)
        {
            awards += SectorRules.Portfolio(new Firm { Place = "w" + i, Reputation = 500 }, new SectorFirm(), SectorRules.PortfolioAward, 3, i).Figure;
        }
        Check(studio.Reputation == 500 + SectorCatalogue.PortfolioReputation && wsf.PortfolioUntil == 130 + SectorCatalogue.PortfolioDays && awards > 400 * SectorCatalogue.AwardChance / 2000
              && awards < 400 * SectorCatalogue.AwardChance * 2 / 1000,
            "sector desk: a polished piece earns a name and bigger clients for two months; an award entry wins about a third of the time (" + awards + " of 400)");
        Check(SectorRules.Answer(new[] { 2, 9 }, 0, 3, 1) == 2 && SectorRules.Answer(new[] { 2, 9 }, 1, 3, 1) == 1 && SectorRules.Answer(null, 0, 3, 0) == 0,
            "sector desk: an answer out of range reads as the sound one");
    }

    // ---------------------------------------------------------------- an older save taken in

    private static void SectorOldSave()
    {
        var ff = new FirmsFile { Seed = 6 };
        var firm = new Firm { Place = "p", Reputation = 650 };
        ff.Firms.Add(firm);
        Product p = FirmsRules.NewProduct(ff, "p", ProductKind.Saas, Segment.Business, 0, "Old");
        p.Features = 14;
        p.RivalFeatures = 17;
        p.Users = 6000;
        FirmsRules.Launch(p, 1);
        ff.Products.Add(p);
        Product app = FirmsRules.NewProduct(ff, "p", ProductKind.App, Segment.Consumer, 0, "Old app");
        app.Features = 12;
        app.RivalFeatures = 9;
        app.Users = 9000;
        app.Free = 300000;
        FirmsRules.Launch(app, 1);
        ff.Products.Add(app);
        int[] start = SectorRules.StartFeatures(ff);
        Check(start[(int)SectorMarket.BusinessSoftware] == 17 && start[(int)SectorMarket.Apps] == 9 && start[(int)SectorMarket.Licences] == FirmsCatalogue.RivalFeaturesAtStart[2],
            "sector old save: each market's rivals start as strong as the rival its products faced, else the market's usual one");
        var f = new SectorFile { Seed = 6 };
        SectorRules.Companies(f, 1, start);
        Crew c = Team(24);
        // the appeal each product has on the first day, as the director works it out, then the markets held up for them
        SectorRules.BeginDay(f, 1, 1);
        SectorRules.Count(f, p);
        SectorRules.Count(f, app);
        var products = new List<Product> { p, app };
        var appeals = new List<int>
        {
            SectorRules.Appeal(p, firm, c, f.Markets[SectorRules.MarketOf(p)]), SectorRules.Appeal(app, firm, c, f.Markets[SectorRules.MarketOf(app)])
        };
        var marketing = new List<long> { 0, 0 };
        SectorRules.Anchor(f, 1, 1, products, appeals, marketing);
        Check(f.Anchor[(int)SectorMarket.BusinessSoftware] > 1000 && f.Anchor[(int)SectorMarket.Licences] <= 1000 && SectorRules.AnchorFor(0, 5) == 1000 && SectorRules.AnchorFor(5, 0) == SectorRules.AnchorMost
              && SectorRules.AnchorFor(100, 1000) == 1000 && SectorRules.AnchorFor(3001, 1000) == 3001,
            "sector old save: a market whose products had more customers than a shared market gives is held up for them, others not");
        long before = p.Users, beforeApp = app.Users + app.Free;
        for (int day = 2; day <= 31; day++)
        {
            SectorRules.BeginDay(f, day, 1);
            SectorRules.Count(f, p);
            SectorRules.Count(f, app);
            SectorRules.Attract(f);
            foreach (Product q in new[] { p, app })
            {
                SectorMarketDay md = f.Markets[SectorRules.MarketOf(q)];
                int appeal = SectorRules.Appeal(q, firm, c, md), aware = SectorRules.Awareness(0, md.Buyers, SectorRules.Have(q), md.Who, 0);
                SectorRules.Join(md, SectorRules.Attraction(appeal, aware));
            }
            SectorRules.RivalDay(f, day, null);
            foreach (Product q in new[] { p, app })
            {
                SectorMarketDay md = f.Markets[SectorRules.MarketOf(q)];
                int appeal = SectorRules.Appeal(q, firm, c, md), aware = SectorRules.Awareness(0, md.Buyers, SectorRules.Have(q), md.Who, 0);
                SectorRules.SellDay(ff, firm, q, c, 50, day, md, appeal, aware, 1000);
            }
        }
        Check(p.Users > before * 95 / 100 && p.Users < before * 105 / 100 && app.Users + app.Free > beforeApp * 95 / 100,
            "sector old save: a month after the change an older save's products keep their customers within a few in a hundred, one with a share bigger than its customers growing into it at the market's pace: nothing jumps ("
            + before + " to " + p.Users + ", " + beforeApp + " to " + (app.Users + app.Free) + ")");
        long rivalBefore = 0, rivalHeld = 0;
        var g = new SectorFile { Seed = 2 };
        SectorRules.Companies(g, 1, null);
        SectorRules.BeginDay(g, 1, 1);
        SectorRules.Attract(g);
        SectorRules.Settle(g);
        RivalProduct codeharbour = g.RivalOf(0, (int)SectorMarket.EnterpriseSoftware);
        rivalBefore = codeharbour.Customers;
        SectorRules.Hold(g, (int)SectorMarket.EnterpriseSoftware, 3000, 1);
        rivalHeld = codeharbour.Customers;
        Check(rivalHeld == rivalBefore * 3 && g.Anchor[(int)SectorMarket.EnterpriseSoftware] == 3000 && g.AnchorDay == 1,
            "sector old save: a held up market holds its rivals up with it, so no share moves on the day of the change");
        SectorFile later = SectorFresh(5);
        RivalProduct added = later.Rivals[0], lost = later.Rivals[1];
        later.Rivals.RemoveAt(0);
        lost.Customers = 0;
        lost.Peak = 0;
        int addedFrom = later.NextRival, addedCount = SectorRules.Companies(later, 2, SectorRules.StartFeatures(null));
        SectorRules.BeginDay(later, 2, 1);
        SectorRules.Attract(later);
        SectorRules.Settle(later, addedFrom);
        RivalProduct back = later.RivalOf(added.Company, added.Market);
        Check(addedCount == 1 && back != null && back.Id >= addedFrom && back.Customers > 0 && lost.Customers == 0,
            "sector: a company product a newer version adds starts at its share on its first day, and one that has lost every customer is left as it stands");
    }

    // ---------------------------------------------------------------- the save's line and the shared city's part

    private static void SectorSave()
    {
        SectorFile f = SectorFresh(44);
        f.Converted = true;
        f.DigitalAnchor = 51234;
        f.AnchorDay = 9;
        f.Anchor[3] = 2500;
        f.NextSpike = 77;
        f.SpikeFrom = 60;
        f.SpikeUntil = 66;
        f.SpikeFactor = 1900;
        f.Hedged = true;
        f.ThrottleUntil = 66;
        f.FailoverUntil = 90;
        f.LastCapacity = 55;
        f.ColoTarget = 12;
        f.City.Set(61000, 2700, 170, 33, 1150, true);
        f.CompanyWon[3] = 70;
        f.Rivals[2].Customers = 123456;
        f.Rivals[2].Tier = 0;
        f.Rivals.Add(new RivalProduct { Id = f.NextRival++, Startup = 41, Stage = 2, Market = 1, Name = "Fern;book|x", Features = 7, Carry = 999999 });
        SectorFirm firm = f.FirmFor("Long%20Rd~7");
        firm.LastOutage = 40;
        firm.LostToOutage = 321;
        firm.OfferCarry = 777777;
        firm.PortfolioUntil = 99;
        firm.PitchTender = 12;
        firm.PitchWeight = -100;
        firm.SprintUntil = 47;
        firm.Sprint = 2;
        firm.Release = 0;
        firm.DivertUntil = 51;
        firm.ReliableUntil = 52;
        firm.IncidentFrom = 8;
        SectorProduct prod = f.ProductFor(31);
        prod.Benchmark = 66;
        prod.Generation = 8;
        prod.BoostUntil = 70;
        prod.Boost = 150;
        prod.DiscountUntil = 400;
        prod.TrainedDay = 63;
        prod.Data = 21;
        string line = SectorStore.Write(f);
        SectorFile back = SectorStore.Read(line, out int version);
        SectorFirm bf = back.FirmAt("Long%20Rd~7");
        SectorProduct bp = back.ProductAt(31);
        RivalProduct startup = back.StartupProduct(41);
        Check(version == SectorStore.Version && back.Converted && back.DigitalAnchor == 51234 && back.AnchorDay == 9 && back.Anchor[3] == 2500 && back.NextSpike == 77 && back.SpikeFactor == 1900
              && back.Hedged && back.ThrottleUntil == 66 && back.FailoverUntil == 90 && back.LastCapacity == 55 && back.ColoTarget == 12 && back.City.People == 61000 && back.City.Digital == 1150
              && back.City.Known && back.CompanyWon[3] == 70,
            "sector save: the header, the city, the anchors and the companies come back as written");
        Check(bf != null && bf.LastOutage == 40 && bf.LostToOutage == 321 && bf.OfferCarry == 777777 && bf.PortfolioUntil == 99 && bf.PitchTender == 12 && bf.PitchWeight == -100 && bf.SprintUntil == 47
              && bf.Sprint == 2 && bf.Release == 0 && bf.DivertUntil == 51 && bf.ReliableUntil == 52 && bf.IncidentFrom == 8
              && bp != null && bp.Benchmark == 66 && bp.Generation == 8 && bp.BoostUntil == 70 && bp.Boost == 150 && bp.DiscountUntil == 400 && bp.TrainedDay == 63 && bp.Data == 21,
            "sector save: a firm's record and a product's come back as written");
        Check(back.Rivals.Count == f.Rivals.Count && back.Rivals[2].Customers == 123456 && back.Rivals[2].Tier == 0 && startup != null && startup.Stage == 2 && startup.Carry == 999999
              && startup.Name.IndexOf('|') < 0 && SectorStore.Write(back) == SectorStore.Write(SectorStore.Read(SectorStore.Write(back), out _)),
            "sector save: the rivals come back as written, names cleaned, and a second round trip is the same line");
        var kept = new SectorFile();
        kept.FirmFor("open");
        kept.FirmFor("closed");
        kept.ProductFor(1);
        kept.ProductFor(2);
        Check(kept.Prune(p => p == "open", id => id == 2) == 2 && kept.FirmAt("open") != null && kept.FirmAt("closed") == null && kept.ProductAt(2) != null && kept.ProductAt(1) == null
              && kept.Prune(null, null) == 0,
            "sector save: records of firms and products no longer the player's are let go, so the save keeps only what still stands");
        var data = new Dictionary<string, string> { { SectorStore.Key, line.Substring(0, line.Length / 3) + "\u0001broken" } };
        SectorFile damaged = SectorStore.Load(data, out _);
        Check(SectorStore.Load(new Dictionary<string, string>(), out _) == null && (damaged == null || damaged.Rivals.Count <= SectorCatalogue.RivalProductsMost),
            "sector save: a save without the line loads with none (the markets are set up then); a damaged line never throws");
        // the shared city's part: the host's city, companies and rivals, never a player's own
        string part = SectorShare.Write(f);
        var guest = new SectorFile { Seed = 9, Hedged = true, SpikeFrom = 3 };
        guest.FirmFor("theirs").OfferCarry = 5;
        guest.ProductFor(77).Benchmark = 40;
        Check(SectorShare.Apply(guest, part) && guest.City.People == 61000 && guest.Rivals.Count == f.Rivals.Count && guest.Rivals[2].Customers == 123456 && guest.CompanyWon[3] == 70
              && guest.SpikeFrom == 60 && guest.SpikeFactor == 1900 && !guest.Hedged && guest.FirmAt("theirs").OfferCarry == 5 && guest.ProductAt(77).Benchmark == 40 && guest.Seed == 9,
            "sector share: a guest takes the host's city, companies, rivals and power price, keeps its own firms, products and seed, and answers a new spike itself");
        var newer = new TechWriter(SectorShare.Version + 1);
        newer.Part('h').Record().Add(1);
        int rivalsBefore = guest.Rivals.Count;
        Check(!SectorShare.Apply(guest, newer.ToString()) && !SectorShare.Apply(guest, "\u0001broken") && !SectorShare.Apply(guest, null) && !SectorShare.Apply(null, part)
              && guest.Rivals.Count == rivalsBefore && SectorShare.Part == 'R',
            "sector share: a line of another version, a broken line or nothing changes nothing");
    }

    // ---------------------------------------------------------------- the screens

    /// <summary>Offline words for the screens: the key itself, figures as plain numbers.</summary>
    private sealed class SectorWords : IStudioFormat
    {
        public string Money(long cents) => (cents / 100).ToString();

        public string Whole(long n) => n.ToString();

        public string Mille(int thousandths) => (thousandths / 10) + "%";

        public string Day(int day) => "day " + day;

        public string Text(string key) => key;
    }

    // Every key a screen holds: its lines, choices and buttons.
    private static void ScreenKeys(StudioScreen v, HashSet<string> into)
    {
        into.Add(v.TitleKey);
        if (v.ChipKey != null)
        {
            into.Add(v.ChipKey);
        }
        if (v.Next != null)
        {
            into.Add(v.Next.Key);
        }
        if (v.Primary != null)
        {
            into.Add(v.Primary.Key);
        }
        for (int i = 0; i < v.Others.Count; i++)
        {
            into.Add(v.Others[i].Key);
        }
        for (int s = 0; s < v.Sections.Count; s++)
        {
            SectionKeys(v.Sections[s], into);
        }
    }

    private static void SectionKeys(ViewSection s, HashSet<string> into)
    {
        into.Add(s.TitleKey);
        for (int i = 0; i < s.Lines.Count; i++)
        {
            into.Add(s.Lines[i].Key);
        }
    }

    private static readonly HashSet<string> SectorScreenKeys = new HashSet<string>();

    private static void SectorScreens()
    {
        var fmt = new SectorWords();
        SectorFile f = SectorFresh(3);
        var ff = new FirmsFile { Seed = 3 };
        bool nexts = true;
        var seen = new HashSet<string>();
        for (int kind = 0; kind < (int)FirmKind.Count; kind++)
        {
            if (kind == (int)FirmKind.Games)
            {
                continue;
            }
            for (int state = 0; state < 9; state++)
            {
                var s = new SectorPlace { Place = "p", Name = "Firm", Kind = kind, Crew = Team(32) };
                s.Team = new SectorTeam { People = state == 0 ? 0 : 6, Builders = state == 1 ? 0 : 4, Designers = 1, Support = state == 6 ? 0 : 2, Sellers = state == 8 ? 0 : 1, Reliability = 1 };
                s.Idle = state == 2 ? (int)Idle.NoStations : (int)Idle.None;
                s.IdleWords = "no desks";
                s.Products = state == 3 ? 0 : 2;
                s.ProductsAll = 5;
                s.Ready = state == 4 ? 1 : 0;
                s.ReadyName = "Ready";
                s.ReadyId = 7;
                s.Customers = state == 6 ? 5000 : 0;
                s.SupportNeeded = state == 6 ? 4 : 0;
                s.Offers = state == 7 ? 1 : 0;
                s.OfferClient = "Elm Street Dental";
                s.OffersMonth = 4500;
                if (state == 5)
                {
                    s.Due.Add(new SectorDue { Kind = SectorRules.DeskSprint, Due = true });
                }
                s.Due.Add(new SectorDue { Kind = SectorRules.DeskOutage, Blocked = "reallife_tech_desk_why_no_outage" });
                s.Due.Add(new SectorDue { Kind = SectorRules.DeskBigCustomer, Days = 4 });
                StudioScreen v = SectorViews.Firm(f, ff, s, 10, fmt);
                nexts &= v.Next != null && v.Sections.Count >= 2 && v.Others.Count > 0;
                seen.Add(v.Next.Key);
                ScreenKeys(v, SectorScreenKeys);
            }
        }
        Check(nexts && seen.Count >= 8, "sector screens: every firm's page always says what to do next, in the order a new firm meets things (" + seen.Count + " different next steps)");
        var s0 = new SectorPlace { Kind = (int)FirmKind.Consultancy, Team = new SectorTeam { People = 0 } };
        SectorViews.Next(s0, fmt, out ViewAction hire);
        var s1 = new SectorPlace { Kind = (int)FirmKind.Software, Team = new SectorTeam { People = 5, Builders = 4, Sellers = 1, Support = 1, Reliability = 1 }, Products = 0 };
        SectorViews.Next(s1, fmt, out ViewAction start);
        Check(hire != null && hire.Act == SectorAct.OpenRoles && start != null && start.Act == SectorAct.NewProduct, "sector screens: a firm with nobody sends the player to hire; one with nothing to build to start a product");
        var ffp = new FirmsFile { Seed = 3 };
        Product p = FirmsRules.NewProduct(ffp, "p", ProductKind.Saas, Segment.Business, 0, "Shown");
        p.Features = 9;
        p.Users = 500;
        FirmsRules.Launch(p, 1);
        SectorRules.BeginDay(f, 5, 1);
        SectorRules.Count(f, p);
        SectorRules.Attract(f);
        // A firm's whole page for the mod's design system: a software firm with a product on sale, and an agency with work.
        var reading = new FirmReading();
        reading.Add(TalentCatalogue.Role(0), 60);
        var sp = new SectorPlace { Place = "p", Name = "Firm", Kind = (int)FirmKind.Software, Products = 1, Customers = 2000, SupportNeeded = 2, Team = new SectorTeam { People = 1, Builders = 1 } };
        var ffs = new FirmsFile { Seed = 3 };
        ffs.Products.Add(p);
        FirmPage page = SectorViews.Page(f, ffs, sp, reading, 10, fmt);
        bool support = false, security = false;
        foreach (ViewRole r in page.Team)
        {
            support |= r.Group == TeamGroup.Support && r.Needed;
            security |= r.Group == TeamGroup.Security && r.Needed;
        }
        var ffw = new FirmsFile { Seed = 3 };
        ffw.Contracts.Add(new Contract { Id = 1, Place = "w", Kind = (int)WorkKind.Website, Client = "Elm Street Dental", Value = 400000, Due = 40, Stage = (int)WorkStage.Active });
        ffw.Contracts.Add(new Contract { Id = 2, Place = "w", Kind = (int)WorkKind.Branding, Client = "Corner Bakery", Paid = 150000, Stage = (int)WorkStage.Done });
        FirmPage agency = SectorViews.Page(f, ffw, new SectorPlace { Place = "w", Name = "Agency", Kind = (int)FirmKind.WebAgency, Contracts = 1 }, reading, 10, fmt);
        FirmPage design = SectorViews.Page(f, ffw, new SectorPlace { Place = "w", Name = "Studio", Kind = (int)FirmKind.Design, Contracts = 1 }, reading, 10, fmt);
        Check(page.Figures.Count == 3 && page.Figures[2].Wide && page.Main.Rows.Count == 1 && page.Main.Rows[0][0] == "Shown" && support && security
              && agency.Main.Rows.Count == 1 && agency.Main.Rows[0][1] == "Elm Street Dental" && agency.More.ColumnKeys[0] == "reallife_tech_mkt_page_col_product"
              && design.More.Rows.Count == 1 && design.More.Rows[0][1] == "Corner Bakery",
            "sector pages: a software house's products, customers and rivals with the groups its work lacks; an agency's work under way over its apps; a design studio's work done");
        bool pageWords = PageWords(page, out string pageMissing), agencyWords = PageWords(agency, out string agencyMissing), designWords = PageWords(design, out string designMissing);
        Check(pageWords && agencyWords && designWords, "sector pages: every word the pages use is written in English, Portuguese and Chinese (" + pageMissing + ", " + agencyMissing + ", "
              + designMissing + ")");
        ViewSection market = SectorViews.Market(f, p, 900, 400, fmt);
        Check(market.Lines.Count >= 5, "sector screens: a product's market says who buys, its share and where it is heading, how known it is, the going features and the rivals, and that no cap but the share holds it");
        SectionKeys(market, SectorScreenKeys);
        var infra = new InfraFile();
        infra.Centres.Add(new DataCentre { Tier = 2, Racks = 40, Colo = 5 });
        f.SpikeFrom = 4;
        f.SpikeUntil = 9;
        f.SpikeFactor = 2000;
        ViewSection racks = SectorViews.Infra(f, infra, 1, 0, 5, fmt);
        Check(racks.Lines.Count == 3, "sector screens: the data centres' markets say the racks, the compute and the power price");
        SectionKeys(racks, SectorScreenKeys);
        Check(SectorViews.MarketKey(99) == SectorViews.MarketKey((int)SectorMarket.Count - 1) && SectorViews.DeskKey(0).EndsWith("_sprint_title", StringComparison.Ordinal),
            "sector screens: a market's or session's key from its number, out of range the last");
    }
}
