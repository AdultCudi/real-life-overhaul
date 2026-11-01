using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;
using UtilService = RealLifeOverhaul.Utilities.Service;
using UtilRules = RealLifeOverhaul.Utilities.UtilitiesRules;
using RealLifeOverhaul.Tech;
using InfraCat = RealLifeOverhaul.Tech.InfraCatalogue;

// The other tech firms over ten years under Living population (SectorChecks.cs runs them): a firm of each kind run as a
// player would run it well, against the rivals by the same rules, in the documented city's markets: its payback inside its
// kind's target (ScaleCatalogue.PaybackLeast and Most, the end game's for data centres and the cloud), its profit a day in
// its class (more than a good shop, never past the end game's ventures), every market's customers within its buyers each
// day, and the same run on every machine. "sector" after --sector prints each run.
internal static partial class Program
{
    private const long SectorWage = 30000;      // cents a day: a professional of the sims, a mid-level engineer as the game pays them
    private const long SectorRent = 50000;      // cents a day: a small office
    private const long SectorDeskCost = 300000;     // cents: a workstation, its chair and its share of the fit-out, once a person
    private const int SecurityRole = 9, SellerRole = 21;   // TalentCatalogue's security analyst and account manager: the services' builders and sellers paid as the game pays them

    private struct SectorRun
    {
        public long Cash, Capital;               // cents at the end, and the most it was down
        public int Payback;                      // the day the cash came back to nothing; -1 never
        public long Day;                         // whole money: its profit a day over the last year
        public long Customers, Buyers;           // its customers and its market's buyers at the end
        public bool Conserved;                   // every day, every market's customers within its buyers
        public int Features, Lead;               // its product's features and the market's most at the end
        public long Takings;                     // cents: everything its work brought over the run
    }

    // A firm making one product of a kind with a team, run for so many days in the documented city against the rivals: the
    // product built to launch and on, its marketing and price as set, an AI model trained on rented compute at a level.
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static SectorRun SectorProductSim(ProductKind kind, Segment segment, int engineers, int designers, int support, int sellers, int sres, int marketing, int days, int seed,
        int tier = 1, int train = 1, Action<SectorFile, int> cityDay = null, int year = SectorYear)
    {
        var ff = new FirmsFile { Seed = seed };
        var f = new SectorFile { Seed = seed, YearDays = year };
        var firm = new Firm { Place = "p", Kind = (int)(kind == ProductKind.Model ? FirmKind.AiLab : FirmKind.Software), Reputation = 500 };
        ff.Firms.Add(firm);
        SectorRules.Companies(f, 0, SectorRules.StartFeatures(ff));
        Product p = FirmsRules.NewProduct(ff, "p", kind, segment, 0, "Sim");
        p.Marketing = marketing;
        p.Tier = tier;
        p.Train = train;
        ff.Products.Add(p);
        int people = engineers + designers + support + sellers + sres, market = SectorRules.MarketOf(p);
        var c = new Crew
        {
            Engineering = engineers * FirmsCatalogue.PointsPerDay, Design = designers * FirmsCatalogue.PointsPerDay, Selling = sellers * FirmsCatalogue.PointsPerDay, Support = support,
            Seats = people, Faults = 150, SeniorShare = 300,
            Reliability = SectorRules.Posture(FirmsCatalogue.ReliabilityBase, sres * 150L, SectorCatalogue.ReliabilityHalf),
            Security = SectorRules.Posture(FirmsCatalogue.SecurityBase, 0, SectorCatalogue.SecurityHalf)
        };
        long cash = -people * SectorDeskCost, low = cash, lastYear = 0;
        var run = new SectorRun { Payback = -1, Conserved = true };
        for (int day = 1; day <= days; day++)
        {
            cityDay?.Invoke(f, day);   // (LivingYearsChecks: the living city's figures for the day; none, the documented city)
            SectorRules.BeginDay(f, day, 1);
            SectorRules.Count(f, p);
            SectorRules.Attract(f);
            if (day == 1)
            {
                SectorRules.Settle(f);
            }
            SectorMarketDay md = f.Markets[market];
            int appeal = SectorRules.Appeal(p, firm, c, md), aware = SectorRules.Awareness(FirmsCatalogue.MarketingDay[p.Marketing], md.Buyers, SectorRules.Have(p), md.Who, 0);
            if (p.Live)
            {
                SectorRules.Join(md, SectorRules.Attraction(appeal, aware));
            }
            SectorRules.RivalDay(f, day, null);
            long costs = people * (SectorWage + FirmsCatalogue.SoftwarePerSeat) + SectorRent, takings = 0;
            if (kind == ProductKind.Model)
            {
                int want = InfraRules.TrainLevels[p.Train] + (p.Live ? InfraRules.Inference(p) : 0);
                p.Compute += InfraRules.TrainLevels[p.Train];
                costs += (long)want * InfraCat.GpuUnitDay * InfraCat.RentedGpuShare / 1000;
            }
            FirmsRules.Build(firm, p, c, c.Engineering, c.Testing, SectorCatalogue.FeaturesMost, SectorRules.ProductBoost(c));
            if (!p.Live && FirmsRules.CanLaunch(p))
            {
                FirmsRules.Launch(p, day);
            }
            if (p.Live)
            {
                ProductDay r = SectorRules.SellDay(ff, firm, p, c, support, day, md, appeal, aware, 1000);
                takings = r.Takings;
                costs += r.Hosting + r.Marketing + r.Fine;
                SectorRules.Reputation(firm, SectorRules.Soundness(p.Bugs, FirmsRules.Allowance(p)));
            }
            cash += takings - costs;
            low = Math.Min(low, cash);
            if (run.Payback < 0 && cash >= 0)
            {
                run.Payback = day;
            }
            if (day > days - 365)
            {
                lastYear += takings - costs;
            }
            long total = SectorRules.Have(p);
            for (int i = 0; i < f.Rivals.Count; i++)
            {
                total += f.Rivals[i].Market == market ? f.Rivals[i].Customers : 0;
            }
            run.Conserved &= total <= f.Markets[market].Buyers + 1;
        }
        run.Cash = cash;
        run.Capital = Math.Max(1L, -low);
        run.Day = lastYear / 365 / 100;
        run.Customers = SectorRules.Have(p);
        run.Buyers = f.Markets[market].Buyers;
        run.Features = p.Features;
        for (int i = 0; i < f.Rivals.Count; i++)
        {
            run.Lead = Math.Max(run.Lead, f.Rivals[i].Market == market ? f.Rivals[i].Features : 0);
        }
        return run;
    }

    // An IT firm of so many builders and sellers taking every offer it has people for and bidding at the fair price on every
    // council tender of work it takes (as the Tech app and the desk let it), working its contracts by the firms' own rules with
    // its desk sessions answered as a manager would (SectorRules.Desk: a project's scope creep at a consultancy, an agency's
    // revision round, each with its sound choice), for so many days in the documented city.
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static SectorRun SectorServicesSim(FirmKind kind, int builders, int sellers, int days, int seed, bool old = false, Action<SectorFile, int> cityDay = null, int year = SectorYear,
        bool sessions = true)
    {
        var ff = new FirmsFile { Seed = seed };
        var f = new SectorFile { Seed = seed, YearDays = year };
        var firm = new Firm { Place = "c", Kind = (int)kind, Reputation = 500 };
        ff.Firms.Add(firm);
        int people = builders + sellers;
        var c = new Crew
        {
            Engineering = kind == FirmKind.Design ? 0 : builders * FirmsCatalogue.PointsPerDay, Design = kind == FirmKind.Design ? builders * FirmsCatalogue.PointsPerDay : 0,
            Selling = sellers * FirmsCatalogue.PointsPerDay, Seats = people
        };
        var crews = new List<Crew> { c };
        var notes = new List<WorkNote>();
        int builderRole = kind == FirmKind.Design ? SectorCatalogue.DesignRole : kind == FirmKind.Security ? SecurityRole : 0;
        long wages = ((long)builders * TalentCatalogue.Role(builderRole).Pay + (long)sellers * TalentCatalogue.Role(SellerRole).Pay) * SectorWage / 1000;
        long cash = -people * SectorDeskCost, low = cash, lastYear = 0;
        var run = new SectorRun { Payback = -1, Conserved = true };
        for (int day = 1; day <= days; day++)
        {
            cityDay?.Invoke(f, day);   // (LivingYearsChecks: the living city's figures for the day; none, the documented city)
            notes.Clear();
            if (old)
            {
                FirmsRules.Offers(ff, day, crews, null, notes);   // the rule before the shared market, to hold the new one against
            }
            else
            {
                SectorRules.Offers(ff, f, day, 1, crews, null, notes);
            }
            if (kind == FirmKind.Security && !old)
            {
                SectorRules.Incidents(ff, day, notes);
            }
            long takings = 0;
            int load = 0;
            for (int i = 0; i < ff.Contracts.Count; i++)
            {
                Contract w = ff.Contracts[i];
                load += w.Stage == (int)WorkStage.Active ? FirmsRules.Need(w, day) : 0;
            }
            for (int i = 0; i < ff.Contracts.Count; i++)
            {
                Contract w = ff.Contracts[i];
                if (w.Stage == (int)WorkStage.Offered && load < c.Engineering + c.Design)
                {
                    takings += FirmsRules.Take(w, firm.Place, day);
                    if (!old)
                    {
                        SectorRules.Urgent(w, day);
                    }
                    load += FirmsRules.Need(w, day);
                }
                else if (w.Stage == (int)WorkStage.Tender && FirmsRules.Takes(kind, w.K))
                {
                    w.Bid = FirmsRules.BidAt(w, 1000);
                    w.Place = firm.Place;
                    w.Stage = (int)WorkStage.Bid;
                }
                else if (w.Stage == (int)WorkStage.Bid && day >= w.End && (old ? FirmsRules.Decide(ff, w, firm, day) : SectorRules.Decide(ff, f, w, firm, day, 0, 0, out _)))
                {
                    takings += FirmsRules.Take(w, firm.Place, day);
                }
            }
            int engineering = c.Engineering, design = c.Design, web = c.Web;
            for (int i = 0; i < ff.Contracts.Count; i++)
            {
                Contract w = ff.Contracts[i];
                if (w.Stage == (int)WorkStage.Active)
                {
                    if (sessions && !old && kind == FirmKind.Consultancy && SectorRules.Creeps(w))
                    {
                        SectorRules.Scope(w, firm, SectorRules.PickScope(w));
                    }
                    else if (sessions && !old && SectorRules.Agency(firm) && SectorRules.WantsChanges(w))
                    {
                        SectorRules.Revision(w, firm, SectorRules.PickRevision(w));
                    }
                    int give = FirmsRules.Give(w.K, FirmsRules.Need(w, day), ref engineering, ref design, ref web);
                    takings += FirmsRules.Work(w, firm, give, day, notes);
                }
            }
            FirmsRules.Lapse(ff, day, notes);
            long costs = wages + people * FirmsCatalogue.SoftwarePerSeat + SectorRent;
            cash += takings - costs;
            run.Takings += takings;
            low = Math.Min(low, cash);
            if (run.Payback < 0 && cash >= 0 && day > 1)
            {
                run.Payback = day;
            }
            if (day > days - 365)
            {
                lastYear += takings - costs;
            }
        }
        run.Cash = cash;
        run.Capital = Math.Max(1L, -low);
        run.Day = lastYear / 365 / 100;
        return run;
    }

    // Data centres sold through the cloud at the end game's scale, as EndScaleChecks' CloudSim runs them, the city's racks and
    // compute from its firms and the rivals instead of the old fixed demand.
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static SectorRun SectorCloudSim(int share, int racks, int gpu, int regions, int tier, bool hyper, int days, int year = SectorYear)
    {
        int at = EndScaleRules.Buying(share > 0);
        SectorFile f = SectorFresh(23, year);
        var infra = new InfraFile();
        long capex = 0;
        for (int i = 0; i < regions; i++)
        {
            int r = racks / regions + (i < racks % regions ? 1 : 0), g = gpu / regions + (i < gpu % regions ? 1 : 0);
            var d = new DataCentre { Place = "p" + i, District = "d" + i, Tier = tier, Sqm = (r + 2 * g) * InfraCat.SqmPerRack, Technicians = 1000 };
            capex += InfraRules.FitCost(d.Sqm, tier, at);
            InfraRules.Buy(d, r, false, at);
            if (g > 0)
            {
                InfraRules.Buy(d, g, true, at);
            }
            capex += d.Book;
            infra.Centres.Add(d);
        }
        Cloud c = infra.Cloud;
        c.Open = true;
        c.Hyper = hyper;
        var run = new SectorRun { Capital = capex, Payback = -1, Conserved = true };
        long cash = -capex, lastYear = 0;
        for (int day = 1; day <= days; day++)
        {
            SectorRules.BeginDay(f, day, 1);
            SectorRules.Attract(f);
            SectorRules.RivalDay(f, day, null);
            SectorRules.RivalCompute(f);
            int wanted = SectorRules.RacksToLet(f, infra, 1, share), let = 0;
            int capacity = 0, gpuUnits = 0, outages = 0;
            long costs = 0, rent = 0;
            for (int i = 0; i < infra.Centres.Count; i++)
            {
                let += infra.Centres[i].Colo;
            }
            for (int i = 0; i < infra.Centres.Count; i++)
            {
                DataCentre d = infra.Centres[i];
                InfraRules.Hire(d, share);
                int cover = InfraRules.Cover(d, share), working = EndScaleRules.Working(cover);
                d.Technicians = InfraRules.TechniciansNeeded(d);
                bool down = InfraRules.Outage(23, d, day, cover);
                if (!down && let < wanted && d.Racks - d.Colo > d.Racks / 2)
                {
                    d.Colo++;
                    let++;
                }
                else if (d.Colo > 0 && (let > wanted || TechRandom.Mille(23, i, day, 9) < InfraCat.ColoChurn / FirmsCatalogue.MonthDays + (down ? 300 : 0)))
                {
                    d.Colo--;
                    let--;
                }
                if (down)
                {
                    outages++;
                }
                else
                {
                    rent += InfraRules.ColoDay(d, share);
                    capacity += (int)((long)InfraRules.Units(d, share) * working / 1000);
                    gpuUnits += (int)((long)InfraRules.GpuUnits(d, share) * working / 1000);
                }
                costs += UtilRules.DayCents(UtilService.Power, InfraRules.Power(d, share), PriceRules.Start, false) + UtilRules.DayCents(UtilService.Water, InfraRules.Water(d), PriceRules.Start, false)
                         + InfraRules.Upkeep(d, share) + InfraRules.Crews(d) + d.Technicians * ProWageTech;
            }
            int ops = InfraRules.OpsNeed((long)capacity + gpuUnits, share);
            c.Ops = EndScaleRules.Hire(c.Ops, ops, InfraCat.CrewPace);
            int cloudWorking = EndScaleRules.Working(EndScaleRules.Cover(c.Ops, ops));
            capacity = (int)((long)capacity * cloudWorking / 1000);
            gpuUnits = (int)((long)gpuUnits * cloudWorking / 1000);
            int engineers = InfraCat.CloudEngineersLeast + (hyper ? 3 : 0);
            costs += (long)c.Ops * InfraCat.OpsWage + engineers * ProWageEngineer + InfraRules.Licence(share) / ScaleYear;
            c.Price = c.RivalPrice;
            int regionsNow = InfraRules.Regions(infra.Centres);
            long market = SectorRules.CloudMarket(f, false, 1, share, hyper, regionsNow), gpuMarket = SectorRules.CloudMarket(f, true, 1, share, hyper, regionsNow);
            int city = SectorRules.CloudDemand(c, market, capacity), cityGpu = SectorRules.CloudDemand(c, gpuMarket, gpuUnits);
            long takings = InfraRules.Takings(c, city, cityGpu) + rent;
            InfraRules.Reputation(c, outages);
            InfraRules.PriceWar(c, city, day, market);
            cash += takings - costs;
            if (run.Payback < 0 && cash >= 0)
            {
                run.Payback = day;
            }
            if (day > days - 365)
            {
                lastYear += takings - costs;
            }
        }
        run.Cash = cash;
        run.Day = lastYear / 365 / 100;
        return run;
    }

    private static void SectorSims(bool print)
    {
        foreach (int year in new[] { SectorYear, CalendarRules.YearDefault })
        {
            SectorSimsAt(year, print);
        }
        SectorRun again = SectorProductSim(ProductKind.Saas, Segment.Business, 4, 1, 1, 1, 1, 2, 800, 11), same = SectorProductSim(ProductKind.Saas, Segment.Business, 4, 1, 1, 1, 1, 2, 800, 11);
        Check(again.Cash == same.Cash && again.Customers == same.Customers, "sector ten years: the same seed runs the same on every machine");
        SectorRun bigger = SectorProductSim(ProductKind.Saas, Segment.Business, 12, 3, 4, 3, 3, 4, 1200, 11), smaller = SectorProductSim(ProductKind.Saas, Segment.Business, 4, 1, 1, 1, 1, 2, 1200, 11);
        Check(bigger.Customers > smaller.Customers && bigger.Features > smaller.Features,
            "sector ten years: a bigger team builds more and wins more of the same market (" + smaller.Customers + " against " + bigger.Customers + ")");
    }

    // Each kind over the money scale's ten years (3,650 game days: a payback and a year's profit are money, by the game day) in a
    // calendar of so many days a year, the documented city's markets growing by its years (the one clock): each in its band.
    private static void SectorSimsAt(int year, bool print)
    {
        const int Years = 3650;
        int[] least = ScaleCatalogue.PaybackLeast, most = ScaleCatalogue.PaybackMost;
        string at = " (years of " + year + " days)";
        // software: a business service, a company service, an app for people, licences
        SectorRun business = SectorProductSim(ProductKind.Saas, Segment.Business, 4, 1, 1, 1, 1, 2, Years, 11, 1, 1, null, year);
        SectorRun enterprise = SectorProductSim(ProductKind.Saas, Segment.Enterprise, 5, 1, 2, 2, 1, 2, Years, 12, 1, 1, null, year);
        SectorRun app = SectorProductSim(ProductKind.App, Segment.Consumer, 4, 1, 1, 0, 1, 3, Years, 13, 1, 1, null, year);
        SectorRun licence = SectorProductSim(ProductKind.Licence, Segment.Enterprise, 4, 1, 1, 2, 0, 2, Years, 14, 1, 1, null, year);
        SectorRun model = SectorProductSim(ProductKind.Model, Segment.Enterprise, 4, 0, 1, 1, 1, 2, Years, 15, 1, 1, null, year);
        SectorRun consult = SectorServicesSim(FirmKind.Consultancy, 4, 1, Years, 16, false, null, year);
        SectorRun security = SectorServicesSim(FirmKind.Security, 4, 1, Years, 17, false, null, year);
        SectorRun design = SectorServicesSim(FirmKind.Design, 3, 1, Years, 18, false, null, year);
        SectorRun cloud = SectorCloudSim(EndScaleRules.Full, 120, 10, 1, 2, false, Years, year), hyper = SectorCloudSim(EndScaleRules.Full, 400, 24, 3, 2, true, Years, year);
        SectorRun[] software = { business, enterprise, app };
        string[] names = { "a business service", "a company service", "an app for people" };
        for (int i = 0; i < software.Length; i++)
        {
            SectorRun r = software[i];
            Check(r.Payback >= least[(int)PaybackKind.Software] && r.Payback <= most[(int)PaybackKind.Software],
                "sector ten years" + at + ": a software studio selling " + names[i] + " pays back in " + r.Payback + " days, inside the target of " + least[(int)PaybackKind.Software] + " to "
                + most[(int)PaybackKind.Software]);
            Check(r.Conserved && r.Customers > 0 && r.Customers <= r.Buyers, "sector ten years" + at + ": " + names[i] + ": every day the market's customers within its buyers (" + r.Customers + " of " + r.Buyers + ")");
        }
        // A licence is sold outright for a large sum: its first deals pay back the few weeks it took to build, then its upkeep and
        // the companies the market adds carry it, a good shop's day or more and never a chain's.
        Check(licence.Payback > 0 && licence.Payback < least[(int)PaybackKind.Software] && licence.Day > ScaleCatalogue.ShopDayLeast && licence.Day < ScaleCatalogue.ChainDayMost
              && licence.Conserved && licence.Customers <= licence.Buyers,
            "sector ten years" + at + ": licences pay their build back with their first deals (" + licence.Payback + " days), then earn " + licence.Day + " a day on upkeep and new companies, a shop's day and never a chain's");
        Check(model.Payback >= least[(int)PaybackKind.AiLab] && model.Payback <= most[(int)PaybackKind.AiLab],
            "sector ten years" + at + ": an AI lab selling a model to companies on rented compute pays back in " + model.Payback + " days, inside the target of " + least[(int)PaybackKind.AiLab] + " to "
            + most[(int)PaybackKind.AiLab]);
        // The services on their contracts alone: the web agency's and the design studio's walk-in customers come from the
        // population beside these, so their contracts must carry their own team and office at a fair price.
        SectorRun[] services = { consult, security, design };
        string[] snames = { "an IT consultancy", "a cybersecurity firm", "a design studio" };
        for (int i = 0; i < services.Length; i++)
        {
            Check(services[i].Payback >= least[(int)PaybackKind.Services] && services[i].Payback <= most[(int)PaybackKind.Services] && services[i].Day > 0,
                "sector ten years" + at + ": " + snames[i] + " on the city's work at a fair price pays back in " + services[i].Payback + " days, inside the target of " + least[(int)PaybackKind.Services] + " to "
                + most[(int)PaybackKind.Services] + ", earning " + services[i].Day + " a day in its last year");
        }
        string parity = string.Empty;
        if (year == SectorYear)
        {
            // The city's work held against the old offers: the same firm over the same ten years takes about what the old rule gave
            // it (a design studio among the city's fewer agencies, so its takings hold), within a quarter either way. Like for like:
            // neither with the living economy's desk sessions, the design studio's work priced back at the old rule's build rate
            // (SectorCatalogue.DesignRole), so it is the flow of work that is held. Neither rule reads the year: a firm's IT budget
            // is money, by the game day.
            FirmKind[] held = { FirmKind.Design, FirmKind.Consultancy, FirmKind.Security };
            int[] heldSeeds = { 18, 16, 17 }, heldTeams = { 3, 4, 4 };
            bool holds = true;
            for (int i = 0; i < held.Length; i++)
            {
                SectorRun before = SectorServicesSim(held[i], heldTeams[i], 1, Years, heldSeeds[i], true);
                SectorRun plain = SectorServicesSim(held[i], heldTeams[i], 1, Years, heldSeeds[i], false, null, year, false);
                long takings = held[i] == FirmKind.Design ? plain.Takings * 1000 / TalentCatalogue.Role(SectorCatalogue.DesignRole).Pay : plain.Takings;
                long ratio = takings * 1000 / Math.Max(1L, before.Takings);
                holds &= ratio >= 750 && ratio <= 1250;
                parity += (i > 0 ? ", " : string.Empty) + held[i] + " " + ratio;
            }
            Check(holds, "sector ten years: a design studio, an IT consultancy and a cybersecurity firm take within a quarter of what the old offers gave them over ten years, thousandths of it ("
                         + parity + ")");
        }
        Check(cloud.Payback >= EndScaleRules.PaybackDaysLeast && cloud.Payback <= EndScaleRules.PaybackDaysMost && hyper.Payback >= EndScaleRules.PaybackDaysLeast && hyper.Payback <= EndScaleRules.PaybackDaysMost,
            "sector ten years" + at + ": a cloud and a hyperscaler at the end game's scale, on the city's compute, pay back in " + cloud.Payback + " and " + hyper.Payback + " days, inside the end game's "
            + EndScaleRules.PaybackDaysLeast + " to " + EndScaleRules.PaybackDaysMost);
        long best = Math.Max(Math.Max(business.Day, enterprise.Day), Math.Max(app.Day, Math.Max(consult.Day, model.Day)));
        Check(business.Day > ScaleCatalogue.ShopDayLeast / 2 && best < ScaleCatalogue.ChainDayMost && best < hyper.Day,
            "sector ten years" + at + ": a firm of a few people with one product earns about a good shop's day or more, never a strong chain's or the end game's ventures' (best " + best + ", the hyperscaler's "
            + hyper.Day + ")");
        if (print)
        {
            Console.WriteLine("SECTOR TEN YEARS" + at.ToUpperInvariant() + " (payback days, profit a day in the last year, customers of buyers, features against the market's most):");
            for (int i = 0; i < software.Length; i++)
            {
                SectorPrint(names[i], software[i]);
            }
            SectorPrint("licences", licence);
            SectorPrint("a model for companies", model);
            for (int i = 0; i < services.Length; i++)
            {
                SectorPrint(snames[i] + " (contracts alone)", services[i]);
            }
            SectorPrint("a cloud at scale", cloud);
            SectorPrint("a hyperscaler at scale", hyper);
            if (parity.Length > 0)
            {
                Console.WriteLine("  the work's takings over ten years against the old offers, thousandths: " + parity);
            }
        }
    }

    private static void SectorPrint(string name, SectorRun r)
    {
        Console.WriteLine("  " + name + ": payback " + r.Payback + ", capital $" + (r.Capital / 100).ToString("N0") + ", $" + r.Day.ToString("N0") + " a day, " + r.Customers.ToString("N0") + " of "
                          + r.Buyers.ToString("N0") + ", features " + r.Features + " against " + r.Lead);
    }

    // ---------------------------------------------------------------- what it costs

    private static void SectorTimings()
    {
        SectorFile f = SectorFresh(5);
        var ff = new FirmsFile { Seed = 5 };
        var firm = new Firm { Place = "p", Reputation = 600 };
        var startups = new List<Startup>();
        for (int i = 0; i < 30; i++)
        {
            startups.Add(new Startup { Id = 100 + i, Name = "S" + i, Sector = i % 9, State = (int)StartupState.Running, Stage = i % 3 });
        }
        SectorRules.Startups(f, startups, 1, null);
        for (int i = 0; i < SectorCatalogue.ProductsMost; i++)
        {
            Product p = FirmsRules.NewProduct(ff, "p", (ProductKind)(i % 5 == 3 ? 0 : i % 5), (Segment)(i % 3), 0, "P" + i);
            p.Features = 10 + i;
            p.Users = 1000 + i * 50;
            FirmsRules.Launch(p, 1);
            ff.Products.Add(p);
        }
        Crew c = Team(40);
        var notes = new List<RivalNote>();
        SectorDayBurst(f, ff, firm, c, notes, 30, 2);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        SectorDayBurst(f, ff, firm, c, notes, 200, 40);
        double each = watch.Elapsed.TotalMilliseconds / 200;
        Check(each < 0.5, "sector timing: a day of the markets with " + SectorCatalogue.ProductsMost + " products of the player's and " + f.Rivals.Count + " rival products costs "
                          + each.ToString("0.000") + " ms, under 0.5 (once a game day)");
        watch.Restart();
        string line = null;
        for (int i = 0; i < 50; i++)
        {
            line = SectorStore.Write(f);
            SectorStore.Read(line, out _);
        }
        double save = watch.Elapsed.TotalMilliseconds / 50;
        Check(save < 2.0 && line.Length < 20000, "sector timing: the save's line written and read in " + save.ToString("0.000") + " ms, " + line.Length + " characters");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void SectorDayBurst(SectorFile f, FirmsFile ff, Firm firm, Crew c, List<RivalNote> notes, int days, int from)
    {
        for (int day = from; day < from + days; day++)
        {
            SectorRules.BeginDay(f, day, 1);
            for (int i = 0; i < ff.Products.Count; i++)
            {
                SectorRules.Count(f, ff.Products[i]);
            }
            SectorRules.Attract(f);
            for (int i = 0; i < ff.Products.Count; i++)
            {
                Product p = ff.Products[i];
                SectorMarketDay md = f.Markets[SectorRules.MarketOf(p)];
                int appeal = SectorRules.Appeal(p, firm, c, md), aware = SectorRules.Awareness(FirmsCatalogue.MarketingDay[2], md.Buyers, SectorRules.Have(p), md.Who, 0);
                SectorRules.Join(md, SectorRules.Attraction(appeal, aware));
            }
            SectorRules.RivalDay(f, day, notes);
            SectorRules.RivalCompute(f);
            for (int i = 0; i < ff.Products.Count; i++)
            {
                Product p = ff.Products[i];
                SectorMarketDay md = f.Markets[SectorRules.MarketOf(p)];
                int appeal = SectorRules.Appeal(p, firm, c, md), aware = SectorRules.Awareness(FirmsCatalogue.MarketingDay[2], md.Buyers, SectorRules.Have(p), md.Who, 0);
                SectorRules.SellDay(ff, firm, p, c, 20, day, md, appeal, aware, 1000);
            }
        }
    }
}
