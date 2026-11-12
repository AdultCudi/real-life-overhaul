using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;

// An AI model's training against the compute behind it. A lab making 529 points a day showed its next feature at -211, -882 and -1,000
// points: its work was done long before its training, which came at most 200 units a day whatever its AI racks (360 units each), and the
// racks fed the labs only through an open, licensed cloud. Now the player's own AI racks train the models first, a cloud or not; a model
// may be set as high as its own compute and 200 rented on top cover (InfraRules.TrainMost); a day short of own compute trains less rather
// than rent past 200; the generations are the same size; rival labs train by the same rule on their own racks, as fast as before, and copy
// a lead as fast as before. The cloud's day, a model's page and the desk's training run share the racks out by one rule (InfraRules.ModelsDay)
// and a level is set by one rule (InfraRules.TrainSet): these checks call those rules, the ones the director calls, never a copy of them.
internal static partial class Program
{
    private static void AiTraining()
    {
        int before = _run, failedBefore = _failed;
        AiTrainingLevels();
        AiTrainingPace();
        AiTrainingShared();
        AiTrainingRivals();
        AiTrainingDesk();
        AiTrainingViews();
        AiTrainingSources();
        AiTrainingTexts();
        Console.WriteLine("  an AI model's training against its compute: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    // A data centre with so many AI racks bought at a share of the end game's scale (the new kind at any share above none).
    private static List<DataCentre> AiRacks(int racks, int share)
    {
        var centres = new List<DataCentre>();
        if (racks > 0)
        {
            var d = new DataCentre { Place = "ai" + racks, District = "a", Tier = 1, Sqm = 60000 };
            InfraRules.Buy(d, racks, true, share);
            centres.Add(d);
        }
        return centres;
    }

    // An AI model of the player's lab, being built at a level of training, added to the lab's products.
    private static Product AiModel(FirmsFile file, int train, string name)
    {
        Product p = FirmsRules.NewProduct(file, "lab", ProductKind.Model, Segment.Business, 0, name);
        p.Train = train;
        file.Products.Add(p);
        return p;
    }

    // A lab's products: an AI model being built with ten features at each of these levels, in this order.
    private static FirmsFile AiModels(params int[] levels)
    {
        var file = new FirmsFile { Seed = 23 };
        for (int i = 0; i < levels.Length; i++)
        {
            AiModel(file, levels[i], "M" + i).Features = 10;
        }
        return file;
    }

    // ---------------------------------------------------------------- the levels

    private static void AiTrainingLevels()
    {
        int[] t = InfraRules.TrainLevels;
        Check(t.Length > 5 && t[0] == 0 && t[1] == 20 && t[2] == 50 && t[3] == 100 && t[4] == 200 && InfraCatalogue.RentedMost == t[4],
            "ai training: levels none to 4 are those of before (0, 20, 50, 100, 200 units a day), so a saved level means what it did, and a model may rent the fifth's");
        bool rising = true;
        for (int i = 1; i < t.Length; i++)
        {
            rising &= t[i] > t[i - 1];
        }
        Check(rising && InfraRules.TrainLevel(-3) == 0 && InfraRules.TrainLevel(99) == t.Length - 1 && InfraRules.TrainLevel(2) == 2,
            "ai training: every level past the last trains more, and a damaged saved level reads as none or the top");
        Check(InfraRules.TrainMost(0) == 4 && InfraRules.TrainMost(-500) == 4 && InfraRules.TrainMost(199) == 4,
            "ai training: with no AI racks of its own a model goes to level 4, 200 units a day rented, as before");
        Product fresh = FirmsRules.NewProduct(new FirmsFile { Seed = 4 }, "lab", ProductKind.Model, Segment.Business, 0, "Fresh");
        Check(fresh.Train == 2 && InfraRules.TrainLevels[fresh.Train] == 50, "ai training: a new model starts at level 2, 50 units a day, as before");
        int full = EndScaleRules.Full;
        long one = InfraRules.GpuOwned(AiRacks(1, full), full), five = InfraRules.GpuOwned(AiRacks(5, full), full), twenty = InfraRules.GpuOwned(AiRacks(20, full), full);
        long oldTwenty = InfraRules.GpuOwned(AiRacks(20, 0), 0);
        int perRack = InfraRules.GpuRackUnits(full);
        Check(one == perRack && five == 5L * perRack && twenty == 20L * perRack && perRack == InfraCatalogue.GpuUnitsPerRackScale * InfraCatalogue.IncomeAtScale / 1000
              && oldTwenty == 20L * InfraCatalogue.GpuUnitsPerRack && InfraRules.GpuRackUnits(0) == InfraCatalogue.GpuUnitsPerRack,
            "ai training: an AI rack of the new kind gives " + perRack + " units a day, the old kind " + InfraCatalogue.GpuUnitsPerRack + " (" + one + ", " + five + ", " + twenty + ", " + oldTwenty + ")");
        long at1 = t[InfraRules.TrainMost(one)], at5 = t[InfraRules.TrainMost(five)], at20 = t[InfraRules.TrainMost(twenty)], old20 = t[InfraRules.TrainMost(oldTwenty)];
        Check(at1 > t[4] && at5 > at1 && at20 > at5 && at20 >= 20L * perRack / 2 && at20 <= twenty + InfraCatalogue.RentedMost && at1 <= one + InfraCatalogue.RentedMost && old20 > t[4],
            "ai training: the most a model may train on grows with its own AI racks: " + at1 + " units a day with one, " + at5 + " with five, " + at20 + " with twenty (the old kind's twenty, " + old20 + ")");
    }

    // ---------------------------------------------------------------- the report's lab, a generation by its racks

    // The days the report's lab (529 points a day, so its work is never what waits) takes to train a generation (two features) from a feature
    // count, set as high as its page allows (InfraRules.TrainSet) and trained each day as the cloud's day trains it (InfraRules.ModelsDay, InfraRules.Train).
    private static int AiGenerationDays(int racks, int share, int from, out long pace)
    {
        var file = new FirmsFile { Seed = 5 };
        var lab = new Firm { Place = "lab" };
        Product p = AiModel(file, 2, "Atlas");
        p.Features = from;
        long own = InfraRules.GpuOwned(AiRacks(racks, share), share);
        var day = new List<ModelCompute>();
        InfraRules.ModelsDay(file.Products, own, day);
        p.Train = InfraRules.TrainSet(InfraRules.TrainLevels.Length - 1, p.Train, InfraRules.TrainMost(day[0].Keep));
        pace = InfraRules.TrainLevels[p.Train];
        var crew = new Crew { Engineering = 0, SeniorShare = 500, Faults = 100 };
        for (int d = 1; d <= 5000; d++)
        {
            p.Progress += 529;
            InfraRules.ModelsDay(file.Products, own, day);
            InfraRules.Train(file.Products, day);
            FirmsRules.Build(lab, p, crew, 0, 0);
            if (p.Features >= from + 2)
            {
                return d;
            }
        }
        return int.MaxValue;
    }

    private static void AiTrainingPace()
    {
        int full = EndScaleRules.Full;
        int none = AiGenerationDays(0, full, 6, out _), one = AiGenerationDays(1, full, 6, out long p1), five = AiGenerationDays(5, full, 6, out _),
            twenty = AiGenerationDays(20, full, 6, out long p20);
        // the report's fault: twenty racks of 360 units, and the fourth generation (3,200 unit-days a feature) still took a month at 200 a day
        Check(twenty <= 7 && one > 7,
            "ai training: a lab with twenty AI racks trains its fourth generation within a week, one with a single rack does not (" + twenty + " and " + one + " days, at "
            + p20 + " and " + p1 + " units a day)");
        Check(twenty < five && five < one && one < none && none == (int)(2 * InfraRules.TrainNeed(6) / InfraRules.TrainLevels[4]),
            "ai training: more AI racks train it sooner and a lab with none waits as it did at the most it could set (" + none + ", " + one + ", " + five + ", " + twenty + " days)");
        int first = AiGenerationDays(20, full, 0, out _), seventh = AiGenerationDays(20, full, 12, out _), seventhOne = AiGenerationDays(1, full, 12, out _);
        Check(first <= 1 && seventh < seventhOne && seventhOne > 60 && InfraRules.TrainNeed(12) == InfraRules.TrainNeed(0) * 64,
            "ai training: the generations keep their sizes, so a well equipped lab trains the early ones in a day and an under equipped one still waits for the later (" + first + " days for the first with twenty racks, "
            + seventh + " and " + seventhOne + " for the seventh with twenty and one)");
        Console.WriteLine("    days to train a generation (two features) from features 0, 6, 12 and 18, at the most a lab may set:");
        foreach (int racks in new[] { 0, 1, 5, 20 })
        {
            Console.WriteLine("      " + racks + " AI racks at scale: " + AiGenerationDays(racks, full, 0, out long pace) + ", " + AiGenerationDays(racks, full, 6, out _) + ", "
                              + AiGenerationDays(racks, full, 12, out _) + ", " + AiGenerationDays(racks, full, 18, out _) + " days at " + pace + " units a day");
        }
    }

    // ---------------------------------------------------------------- several models on the same racks

    private static void AiTrainingShared()
    {
        bool held = true, tight = true, paced = true, served = true, whole = true, idle = true, short1 = true, page = true;
        int states = 0, top = InfraRules.TrainLevels.Length - 1, perRack = InfraRules.GpuRackUnits(EndScaleRules.Full);
        string bad = "none";
        uint seed = 17;
        var day = new List<ModelCompute>();
        var other = new List<ModelCompute>();
        var work = new List<ModelCompute>();
        foreach (long racks in new long[] { 0, 25, 360, 500, 1800, 7200, 21600, 100000 })
        {
            for (int n = 1; n <= 4; n++)
            {
                for (int round = 0; round < 12; round++)
                {
                    var file = new FirmsFile { Seed = 9 };
                    for (int m = 0; m < n; m++)
                    {
                        Product p = AiModel(file, 0, "M" + m);
                        if (m % 2 == 1)
                        {
                            FirmsRules.Launch(p, 1);   // on sale: its customers use AI compute too
                            p.Users = 400 * m;
                        }
                    }
                    List<Product> models = file.Products;
                    // the player sets the models one after another, each to any level, by the setter the page's levels go through, some more than once
                    for (int step = 0; step < n * 3; step++)
                    {
                        seed = unchecked(seed * 1103515245u + 12345u);
                        int j = (int)((seed >> 8) % (uint)n);
                        InfraRules.ModelsDay(models, racks, day);
                        models[j].Train = InfraRules.TrainSet((int)((seed >> 16) % (uint)(top + 1)), models[j].Train, InfraRules.TrainMost(day[j].Keep));
                    }
                    states++;
                    string state = racks + " racks, " + n + " models";
                    long left = InfraRules.ModelsDay(models, racks, day), used = 0;
                    bool rents = false;
                    for (int i = 0; i < n; i++)
                    {
                        Product p = models[i];
                        ModelCompute c = day[i];
                        if (p.Train > InfraRules.TrainMost(c.Keep))
                        {
                            held = false;
                            bad = state;
                        }
                        paced &= c.Trained == InfraRules.TrainLevels[p.Train] && c.Own >= 0 && c.Rented >= 0 && c.Rented <= InfraCatalogue.RentedMost;
                        served &= c.RunOwn >= 0 && c.RunRented >= 0 && c.RunOwn + c.RunRented == (p.Live ? InfraRules.Inference(p) : 0);
                        used += c.Own + c.RunOwn;
                        rents |= c.Bought > 0;
                    }
                    whole &= left >= 0 && left == racks - used;
                    idle &= !rents || left == 0;
                    // the most is as high as it may be: one level above it, a model would train less
                    for (int i = 0; i < n; i++)
                    {
                        int most = InfraRules.TrainMost(day[i].Keep), was = models[i].Train;
                        if (most + 1 > top)
                        {
                            continue;
                        }
                        models[i].Train = most + 1;
                        InfraRules.ModelsDay(models, racks, other);
                        bool less = false;
                        for (int k = 0; k < n; k++)
                        {
                            less |= other[k].Trained < InfraRules.TrainLevels[models[k].Train];
                        }
                        tight &= less;
                        models[i].Train = was;
                    }
                    // the products in the reverse order: every model still at its level
                    var back = new List<Product>(models);
                    back.Reverse();
                    InfraRules.ModelsDay(back, racks, other);
                    for (int i = 0; i < n; i++)
                    {
                        paced &= other[i].Trained == InfraRules.TrainLevels[back[i].Train] && other[i].Rented <= InfraCatalogue.RentedMost;
                    }
                    // a day short of own compute (an outage, a crew short): it trains less, never renting past the most
                    InfraRules.ModelsDay(models, racks / 2, other);
                    for (int i = 0; i < n; i++)
                    {
                        short1 &= other[i].Trained <= InfraRules.TrainLevels[models[i].Train] && other[i].Rented <= InfraCatalogue.RentedMost;
                    }
                    // the page's figures at every level are what the day gives the model once it is set there
                    for (int i = 0; i < n; i++)
                    {
                        TrainingLook t = TrainingViews.Look(models, i, racks, perRack, work);
                        int was = models[i].Train;
                        for (int level = 0; level <= top; level++)
                        {
                            models[i].Train = level;
                            InfraRules.ModelsDay(models, racks, other);
                            if (t.Own[level] != other[i].Own || t.Rented[level] != other[i].Rented || t.Keep != other[i].Keep)
                            {
                                page = false;
                                bad = state + ", model " + i + " at level " + level;
                            }
                        }
                        models[i].Train = was;
                        page &= t.Level == was && t.Most == InfraRules.TrainMost(t.Keep) && t.PerRack == perRack;
                    }
                }
            }
        }
        Check(held, "ai training: setting one model to what its page allows never takes another's own compute: every model stays within its most (" + states + " states, " + bad + ")");
        Check(tight, "ai training: the most a model may be set to is as high as it can be: one level above it, a model would train less");
        Check(paced, "ai training: on a day of full compute every model trains at its level, in either order of the products, renting no more than " + InfraCatalogue.RentedMost + " units a day");
        Check(served && whole && idle, "ai training: the day serves every model's customers in full, shares the racks out to the unit and rents nothing while they have compute left");
        Check(short1, "ai training: on a day short of own compute a model trains less and still rents no more than " + InfraCatalogue.RentedMost);
        Check(page, "ai training: a model's page shows at every level what the day gives it there, on the racks and rented (" + bad + ")");
        // no racks: as before, every model rents its whole level
        FirmsFile renter = AiModels(2, 4);
        InfraRules.ModelsDay(renter.Products, 0, day);
        Check(day[0].Trained == 50 && day[0].Rented == 50 && day[1].Trained == 200 && day[1].Rented == 200 && day[0].Own + day[1].Own == 0,
            "ai training: with no AI racks every model's training is rented at its level, as before");
        // a withdrawn model takes nothing of the racks
        FirmsFile two = AiModels(0, top);
        two.Products[1].Stage = 2;
        InfraRules.ModelsDay(two.Products, 7200, day);
        Check(day[0].Keep == 7200 && day[1].Trained == 0 && day[1].Bought == 0, "ai training: a withdrawn model's level keeps nothing of the racks from the others");
        // two models on one rack, the first at 50 a day and the second at 400: the day keeps 200 of the rack for the second's part past what it
        // may rent, gives the first its 50 and the second 110 more, so 310 on the rack and 90 rented. Its page said 360 and 40.
        FirmsFile pair = AiModels(2, 5);
        long rack = InfraRules.GpuOwned(AiRacks(1, EndScaleRules.Full), EndScaleRules.Full);
        long spare = InfraRules.ModelsDay(pair.Products, rack, day);
        TrainingLook second = TrainingViews.Look(pair.Products, 1, rack, perRack, work);
        var fmt = new PlainWords();
        ViewLine said = TrainingViews.Level(second, 5, fmt);
        Check(rack == 360 && day[0].Own == 50 && day[0].Rented == 0 && day[1].Own == 310 && day[1].Rented == 90 && spare == 0 && second.Own[5] == 310 && second.Rented[5] == 90
              && said.Key == "reallife_tech_product_train_split" && AiPair(said, "own") == "310" && AiPair(said, "rented") == "90" && AiPair(said, "cost") == fmt.Money(InfraRules.RentCost(90)),
            "ai training: two models on one rack, the second at 400 a day: the day and its page both give it " + day[1].Own + " on the rack and " + day[1].Rented + " rented at "
            + AiPair(said, "cost") + " a day (the page's " + AiPair(said, "own") + " and " + AiPair(said, "rented") + ")");
    }

    // ---------------------------------------------------------------- rival labs

    private static void AiTrainingRivals()
    {
        long listed = InfraRules.TrainAlone(SectorCatalogue.RivalLabOwn, out long listedRent), startup = InfraRules.TrainAlone(SectorCatalogue.StartupLabOwn, out long startupRent);
        Check(listed == 1500 && listedRent == 75 && startup == 200 && startupRent == 10,
            "ai training: a rival lab trains by the player's rule on its own racks, as fast as before: a listed company's " + listed + " units a day (" + listedRent + " rented), a startup's " + startup + " ("
            + startupRent + ")");
        bool alone = true;
        var day = new List<ModelCompute>();
        foreach (long own in new long[] { 0, 10, 190, 360, 1425, 7200, 100000 })
        {
            FirmsFile one = AiModels(InfraRules.TrainMost(own));
            InfraRules.ModelsDay(one.Products, own, day);
            alone &= InfraRules.TrainAlone(own, out long rent) == day[0].Trained && rent == day[0].Rented;
        }
        Check(alone, "ai training: a rival lab's training is the day's own rule for one model at the highest level its compute covers");
        SectorFile f = SectorFresh(31);
        var labs = new List<RivalProduct>();
        foreach (RivalProduct r in f.Rivals)
        {
            if (r.Market >= (int)SectorMarket.ConsumerAi)
            {
                labs.Add(r);
            }
        }
        bool trained = labs.Count > 0;
        long rentedAll = 0;
        foreach (RivalProduct r in labs)
        {
            InfraRules.TrainAlone(SectorRules.LabOwn(r), out long rent);
            rentedAll += rent;
        }
        SectorRules.RivalCompute(f);
        Check(trained && f.Training == rentedAll && rentedAll > 0, "ai training: the clouds' market takes in the rival labs' rented training by the same rule (" + f.Training + " units a day, " + labs.Count + " labs)");
        foreach (RivalProduct r in labs)
        {
            r.Features = SectorCatalogue.FeaturesMost;   // nothing more to build, so the day's training stays
            r.Compute = 0;
        }
        SectorRules.BeginDay(f, 2, 1);
        SectorRules.Attract(f);
        SectorRules.RivalDay(f, 2, null);
        foreach (RivalProduct r in labs)
        {
            trained &= r.Compute == InfraRules.TrainAlone(SectorRules.LabOwn(r), out _);
        }
        Check(trained, "ai training: a rival lab's day adds its training by the player's rule on its own compute");
        // a lab copies a lead in the days its team needs to build the features, as every rival does and as before: its training plays no part
        RivalProduct copier = labs.Count > 0 ? labs[0] : null;
        if (copier == null)
        {
            Check(false, "ai training: the sector has a rival lab to copy a lead");
            return;
        }
        copier.Features = 10;
        var twin = new RivalProduct { Company = copier.Company, Stage = copier.Stage, PushPoints = copier.PushPoints, Market = (int)SectorMarket.BusinessSoftware, Features = 10 };
        copier.Compute = 0;
        int untrained = RivalryRules.CopyDelay(f, copier, 14);
        copier.Compute = 1000000;
        int ready = RivalryRules.CopyDelay(f, copier, 14), soft = RivalryRules.CopyDelay(f, twin, 14);
        var seedLab = new RivalProduct { Company = -1, Stage = 0, Market = (int)SectorMarket.BusinessAi, Features = 12 };
        var seedSoft = new RivalProduct { Company = -1, Stage = 0, Market = (int)SectorMarket.BusinessSoftware, Features = 12 };
        int small = RivalryRules.CopyDelay(f, seedLab, 14), smallSoft = RivalryRules.CopyDelay(f, seedSoft, 14);
        Check(untrained == soft && ready == soft && small == smallSoft && small < RivalryCatalogue.CopyMost,
            "ai training: a rival lab copies a lead in the days its team needs, as a software rival with the same team does, whatever its training (" + untrained + " and " + ready + " days for a listed company's lab, "
            + soft + " for its twin; " + small + " for a seed startup's lab, " + smallSoft + " for its twin)");
        // the copy lands on its day and leaves the lab's training as it was
        RivalSim s = RivalNew(41, 2, NicheCatalogue.Shops, 800, true);
        for (int d = 0; d < 3; d++)
        {
            RivalStep(s);
        }
        RivalProduct lander = null;
        foreach (RivalProduct r in s.F.Rivals)
        {
            if (lander == null && r.Market >= (int)SectorMarket.ConsumerAi)
            {
                lander = r;
            }
        }
        if (lander == null)
        {
            Check(false, "ai training: the rivals' world has a lab to copy a lead");
            return;
        }
        int target = lander.Features + 4, landing = s.Day + 1;
        RivalryRec rec = s.R.RecFor(lander.Id);
        rec.CopyTarget = target;
        rec.CopyAt = landing;
        rec.CopyPlace = "Mine~1";
        lander.Compute = 1000000;
        s.Day = landing;
        SectorRules.BeginDay(s.F, landing, 1);
        RivalryRules.Prepare(s.W, landing);
        SectorRules.Attract(s.F);
        RivalryRules.Views(s.W, landing);
        s.W.You.Clear();
        RivalryRules.Day(s.W, landing, s.VNotes);
        Check(lander.Features == target && rec.CopyTarget == 0 && rec.CopyDone == landing && lander.Compute == 1000000,
            "ai training: a lab's copy lands on its day and leaves its training as it was, as before (" + lander.Features + " of " + target + " features, " + lander.Compute + " unit-days of 1,000,000)");
    }

    // ---------------------------------------------------------------- the desk's training run

    // A training run at the desk for the last of a lab's models, with so much of the player's AI racks a day: its own part is the model's share of
    // them by the day's own rule at the run's level, as the director passes it (TechDirector.RunOwn). Returns the unit-days it trained; its compute's
    // cost (the data's left out) and its own part a day come out.
    private static long AiRun(SectorFile f, FirmsFile lab, long racks, int compute, out long cost, out long own)
    {
        int at = lab.Products.Count - 1;
        Product model = lab.Products[at];
        var share = new List<ModelCompute>();
        InfraRules.ModelsDay(lab.Products, racks, share, at, SectorRules.RunLevel(model));
        own = share[at].Own;
        long before = model.Compute;
        DeskResult r = SectorRules.Training(f, model, new SectorProduct { Id = model.Id }, new Firm { Place = "lab", Reputation = 500 }, compute, SectorRules.DataSynthetic, SectorRules.SafetySkip,
            own, 23, 80);
        cost = r.Cost - SectorCatalogue.DataCost[SectorRules.DataSynthetic];
        return model.Compute - before;
    }

    // What so much AI compute costs on the player's own racks: the price the player's firms pay their own cloud.
    private static long AiOwnPrice(long units) => units * InfraCatalogue.GpuUnitDay * InfraCatalogue.OwnHostingShare / 1000;

    private static void AiTrainingDesk()
    {
        SectorFile f = SectorFresh(23);
        int full = EndScaleRules.Full, days = SectorCatalogue.TrainRunDays;
        long twenty = InfraRules.GpuOwned(AiRacks(20, full), full), rack = InfraRules.GpuOwned(AiRacks(1, full), full), rent = InfraCatalogue.RentedMost;
        int high = InfraRules.TrainMost(twenty);
        long units = InfraRules.TrainLevels[high];
        // covered: twenty racks give a lone model at the most they allow its whole level, so the run is all on them at the player's own price
        FirmsFile coveredLab = AiModels(high);
        long covered = AiRun(f, coveredLab, twenty, SectorRules.ComputeOwn, out long coveredCost, out long coveredOwn);
        Check(covered == units * days && coveredOwn == units && coveredCost == AiOwnPrice(covered)
              && SectorRules.PickTraining(0, SectorRules.RunCovered(coveredLab.Products[0], coveredOwn)) == SectorRules.ComputeOwn,
            "ai training: a run the player's own AI racks cover trains the model's level on them for ten days at the player's own price, and a manager runs it there (" + covered + " unit-days)");
        // uncovered: one rack under the same level (set when there were more racks): the run takes of the rack what the day gives the model, the
        // rest rented, no more than a model may rent a day and at the rival clouds' price
        FirmsFile oneLab = AiModels(high);
        long under = AiRun(f, oneLab, rack, SectorRules.ComputeOwn, out long underCost, out long underOwn);
        Check(underOwn == rack && under == (rack + rent) * days && underCost == AiOwnPrice(rack * days) + InfraRules.RentCost(rent * days)
              && SectorRules.PickTraining(0, SectorRules.RunCovered(oneLab.Products[0], underOwn)) == SectorRules.ComputeRented,
            "ai training: a run the racks do not cover takes of them what the day gives the model, " + underOwn + " a day, and rents the rest, " + rent + " a day at most, at the rival clouds' price ("
            + under + " unit-days, not " + units * days + "); a manager rents it");
        // shared: two models at 3,000 a day on 5,900 units of racks: the day gives the second 2,900 of them and 100 rented, and so does its run
        FirmsFile shared = AiModels(8, 8);
        var day = new List<ModelCompute>();
        InfraRules.ModelsDay(shared.Products, 5900, day);
        long both = AiRun(f, shared, 5900, SectorRules.ComputeOwn, out long bothCost, out long bothOwn);
        Check(bothOwn == day[1].Own && day[1].Own == 2900 && both == day[1].Trained * days && bothCost == AiOwnPrice(day[1].Own * days) + InfraRules.RentCost(day[1].Rented * days),
            "ai training: beside another model a run takes of the racks the share the day gives the model, " + bothOwn + " a day, and rents the rest (" + both + " unit-days)");
        long rented = AiRun(f, AiModels(high), twenty, SectorRules.ComputeRented, out long rentedCost, out _);
        long none = AiRun(f, AiModels(high), 0, SectorRules.ComputeOwn, out long noneCost, out long noneOwn);
        long old = AiRun(f, AiModels(2), 0, SectorRules.ComputeRented, out _, out _);
        Check(rented == rent * days && rentedCost == InfraRules.RentCost(rented) && none == rented && noneCost == rentedCost && noneOwn == 0 && old == 50L * days,
            "ai training: a rented run takes no more a day than a model may rent (" + rented + " unit-days), one with no AI racks is rented, and at the old levels a run is as before (" + old + ")");
    }

    // ---------------------------------------------------------------- the page's lines

    private static void AiTrainingViews()
    {
        var fmt = new PlainWords();
        Dictionary<string, string> en = LocaleFile("en");
        int top = InfraRules.TrainLevels.Length - 1, states = 0, perRack = InfraRules.GpuRackUnits(EndScaleRules.Full);
        bool named = true, chosen = true;
        string odd = "none";
        var day = new List<ModelCompute>();
        var work = new List<ModelCompute>();
        var seen = new HashSet<string>();
        // a lone model on so much of the racks, and a lab whose racks were sold under its two models' levels (the first still trains at its level, the second less)
        var labs = new List<FirmsFile>();
        var racksOf = new List<long>();
        foreach (long racks in new long[] { 0, 25, 150, 360, 1800, 5900, 7200, 50000, 100000000 })
        {
            labs.Add(AiModels(0));
            racksOf.Add(racks);
        }
        labs.Add(AiModels(8, 6));
        racksOf.Add(3000);
        for (int s = 0; s < labs.Count; s++)
        {
            List<Product> models = labs[s].Products;
            long racks = racksOf[s];
            for (int i = 0; i < models.Count; i++)
            {
                int set = models[i].Train;
                for (int level = 0; level <= top; level++)
                {
                    states++;
                    models[i].Train = level;
                    TrainingLook t = TrainingViews.Look(models, i, racks, perRack, work);
                    ViewLine words = TrainingViews.Level(t, level, fmt), room = TrainingViews.Room(t);
                    // what the day gives it at this level, and on the racks at the most it may be set to
                    InfraRules.ModelsDay(models, racks, day);
                    ModelCompute c = day[i];
                    int most = InfraRules.TrainMost(c.Keep);
                    models[i].Train = most;
                    InfraRules.ModelsDay(models, racks, day);
                    long ownMost = day[i].Own, units = InfraRules.TrainLevels[level];
                    models[i].Train = level;
                    string wantLevel = units == 0 ? "reallife_tech_product_train_none" : c.Own == 0 && units <= InfraCatalogue.RentedMost ? "reallife_tech_product_train"
                        : c.Rented == 0 ? "reallife_tech_product_train_own" : "reallife_tech_product_train_split";
                    string wantRoom = c.Trained < units ? "reallife_tech_product_train_short" : level > most ? "reallife_tech_product_train_over"
                        : level < most ? (ownMost > 0 ? "reallife_tech_product_train_room" : "reallife_tech_product_train_rent_room")
                        : most == top ? "reallife_tech_product_train_peak" : c.Keep > 0 ? "reallife_tech_product_train_top" : "reallife_tech_product_train_rent_top";
                    bool warn = wantRoom == "reallife_tech_product_train_short" || wantRoom == "reallife_tech_product_train_over";
                    bool tone = room.Tone == (warn ? ViewTone.Warn : ViewTone.Plain);
                    bool racksLink = TrainingViews.WantsRacks(t) == (warn || wantRoom == "reallife_tech_product_train_top" || wantRoom == "reallife_tech_product_train_rent_top");
                    if (words.Key != wantLevel || room.Key != wantRoom || !tone || !racksLink)
                    {
                        chosen = false;
                        odd = racks + " racks, model " + i + " at level " + level + ": " + words.Key + ", " + room.Key;
                    }
                    seen.Add(words.Key);
                    seen.Add(room.Key);
                    foreach (ViewLine line in new[] { words, room })
                    {
                        if (!AiNamesMatch(en, line))
                        {
                            named = false;
                            odd = line.Key;
                        }
                    }
                }
                models[i].Train = set;
            }
        }
        Check(chosen && seen.Count == 11,
            "ai training: the page says a level's split and cost as the day gives them, what holds the model at its most and what the next level needs, or that it trains less than its level, or that a model after it does ("
            + states + " states, " + seen.Count + " lines, " + odd + ")");
        Check(named, "ai training: every line's names are its English text's, no more and no fewer (" + odd + ")");
        TrainingLook report = TrainingViews.Look(AiModels(InfraRules.TrainMost(7200)).Products, 0, 7200, perRack, work);
        ViewLine twenty = TrainingViews.Room(report);
        string said = AiTrainingFill(en, twenty);
        Check(twenty.Key == "reallife_tech_product_train_top" && said.Contains("Infra tab") && said.Contains("12,000") && said.Contains("4,600"),
            "ai training: the report's lab at its most is told the next level needs 4,600 more units of its own racks and where to buy them (" + said + ")");
        // racks sold under two models' levels: the first trains at its level and its page says a model after it trains less; the second's says it trains on less
        FirmsFile sold = AiModels(8, 6);
        InfraRules.ModelsDay(sold.Products, 3000, day);
        ViewLine first = TrainingViews.Room(TrainingViews.Look(sold.Products, 0, 3000, perRack, work)), second = TrainingViews.Room(TrainingViews.Look(sold.Products, 1, 3000, perRack, work));
        Check(day[0].Trained == 3000 && day[1].Trained == 400 && first.Key == "reallife_tech_product_train_over" && first.Tone == ViewTone.Warn
              && second.Key == "reallife_tech_product_train_short" && AiPair(second, "pace") == "400" && AiPair(second, "own") == "200",
            "ai training: with racks sold under two models' levels the first still trains at its level and its page says a model after it trains less; the second's says it trains on "
            + AiPair(second, "pace") + " a day (" + AiTrainingFill(en, first) + ")");
        // the cloud's page: what the player's own models take of the AI racks before the cloud sells any (one on sale, 60 units a day for its customers)
        FirmsFile cloudLab = AiModels(2, 5);
        FirmsRules.Launch(cloudLab.Products[0], 1);
        cloudLab.Products[0].Users = 1000;
        long left = InfraRules.ModelsDay(cloudLab.Products, 360, day);
        ViewLine racksLine = TrainingViews.Racks(360, day, 77);
        var empty = new List<ModelCompute>();
        InfraRules.ModelsDay(new List<Product>(), 360, empty);
        Check(racksLine != null && racksLine.Key == "reallife_infra_cloud_gpu_models" && AiPair(racksLine, "train") == "300" && AiPair(racksLine, "run") == "60" && AiPair(racksLine, "sold") == "77"
              && left == 0 && AiNamesMatch(en, racksLine) && TrainingViews.Racks(0, day, 77) == null && TrainingViews.Racks(360, empty, 77) == null,
            "ai training: the cloud's page says what the player's own models take of the AI racks a day before it sells any, and nothing without racks or models ("
            + (racksLine != null ? AiTrainingFill(en, racksLine) : "no line") + ")");
    }

    // A line's value for one of its names; null if it has none.
    private static string AiPair(ViewLine line, string name)
    {
        for (int i = 0; line != null && line.Pairs != null && i + 1 < line.Pairs.Length; i += 2)
        {
            if (line.Pairs[i] == name)
            {
                return line.Pairs[i + 1];
            }
        }
        return null;
    }

    // Whether a line's names are those its English text asks for.
    private static bool AiNamesMatch(Dictionary<string, string> en, ViewLine line)
    {
        if (!en.TryGetValue(line.Key, out string text))
        {
            return false;
        }
        var asked = new HashSet<string>();
        foreach (Match m in Regex.Matches(text, "\\{([a-z_]+)\\}"))
        {
            asked.Add(m.Groups[1].Value);
        }
        var given = new HashSet<string>();
        for (int i = 0; line.Pairs != null && i + 1 < line.Pairs.Length; i += 2)
        {
            given.Add(line.Pairs[i]);
        }
        return asked.SetEquals(given);
    }

    // A line written with its English text, for the checks' messages.
    private static string AiTrainingFill(Dictionary<string, string> en, ViewLine line)
    {
        string text = en.TryGetValue(line.Key, out string t) ? t : line.Key;
        for (int i = 0; line.Pairs != null && i + 1 < line.Pairs.Length; i += 2)
        {
            text = text.Replace("{" + line.Pairs[i] + "}", line.Pairs[i + 1]);
        }
        return text;
    }

    // ---------------------------------------------------------------- the sources

    // The director is not built here: these hold its calls to the rules the checks above run.
    private static void AiTrainingSources()
    {
        string cloud = ModSource(@"Tech\TechDirector.Infra.cs"), desk = ModSource(@"Tech\TechDirector.SectorDesk.cs");
        string old = ModSource(@"Tech\Firms\TechApp.Pages.cs"), kit = ModSource(@"Ui\Tech\TechLive.Products.cs");
        string day = MemberText(cloud, "private void Cloud(int day, int cycle, int capacity, int gpu, int outages, int share)");
        int share = day.IndexOf("InfraRules.ModelsDay(", StringComparison.Ordinal);
        Check(day.Contains("long left = InfraRules.ModelsDay(products, Math.Max(0, gpu), _cloudDay);") && day.Contains("InfraRules.Train(products, _cloudDay);")
              && day.Contains("long cost = InfraRules.RentCost(_cloudDay[i].Bought) * level / 1000;") && day.Contains("gpu = (int)Math.Min(int.MaxValue, left);")
              && share >= 0 && share < day.IndexOf("if (!open)", StringComparison.Ordinal) && !day.Contains("TrainDay") && !day.Contains("reserve"),
            "ai training sources: the cloud's day shares the AI racks out by the rule the page and the desk read, a cloud or not, pays for what is rented and leaves the rest for the cloud to sell");
        Check(cloud.Contains("p.Train = InfraRules.TrainSet(level, p.Train, TrainingMost(p));")
              && MemberText(cloud, "private int TrainingMost(Product p)").Contains("InfraRules.ModelsDay(_firmsFile.Products, OwnRacks(), _modelsDay);")
              && MemberText(cloud, "private int TrainingMost(Product p)").Contains("return InfraRules.TrainMost(_modelsDay[index].Keep);")
              && MemberText(cloud, "internal TrainingLook Training(Product p)").Contains("TrainingViews.Look(products, products != null && p != null ? products.IndexOf(p) : -1, OwnRacks(), InfraRules.GpuRackUnits(BuyScale), _modelsDay)")
              && MemberText(cloud, "private long OwnRacks()").Contains("InfraRules.GpuOwned(_infra.Centres, share)"),
            "ai training sources: a level is set by the setter the checks run, up to the most the day's rule leaves it, and the page's look is the day's rule on the racks at full work");
        Check(old.Contains("TrainingLook t = _director.Training(p);") && old.Contains("box.Selector(Mathf.Max(t.Most, t.Level) + 1, t.Level, i => Words(TrainingViews.Level(t, i, _studioWords))")
              && old.Contains("TrainingViews.Room(t)") && kit.Contains("TrainingLook t = _d.Training(p);") && kit.Contains("Words(TrainingViews.Level(t, train, W)), null, train > 0, train < t.Most")
              && kit.Contains("TrainingViews.Room(t)") && kit.Contains("TrainingViews.WantsRacks(t)") && kit.Contains("v.Open(\"tab:\" + TabInfra)"),
            "ai training sources: the old page and the kit's list the levels to the most from the day's figures, say what holds it there, and the kit links to the Infra tab");
        string pick = MemberText(desk, "internal int SectorPick(int kind, string place, int step)"), runOwn = MemberText(desk, "private long RunOwn(Product model)");
        Check(pick.Contains("SectorRules.PickTraining(step, run != null && SectorRules.RunCovered(run, RunOwn(run)))")
              && desk.Contains("SectorRules.Answer(choices, 2, 3, SectorRules.SafetyStandard), RunOwn(model), seed, day, strength)")
              && runOwn.Contains("InfraRules.ModelsDay(_firmsFile.Products, OwnRacks(), _modelsDay, index, SectorRules.RunLevel(model));") && runOwn.Contains("return _modelsDay[index].Own;")
              && !desk.Contains("OwnGpu"),
            "ai training sources: the desk's run takes the model's share of the racks by the day's rule, and a manager runs it there only when they cover it");
        string racks = MemberText(cloud, "internal ViewLine RacksLine()");
        Check(racks.Contains("InfraRules.ModelsDay(_firmsOn ? _firmsFile.Products : null, racks, _modelsDay);") && racks.Contains("TrainingViews.Racks(racks, _modelsDay, _infra != null ? _infra.Cloud.GpuSold : 0)")
              && ModSource(@"Tech\Infra\TechApp.Infra.cs").Contains("ViewLine racks = _director.RacksLine();") && ModSource(@"Tech\Infra\TechApp.Infra.cs").Contains("box.Line(Words(racks));")
              && ModSource(@"Ui\Tech\TechLive.Infra.cs").Contains("ViewLine racks = _d.RacksLine();") && ModSource(@"Ui\Tech\TechLive.Infra.cs").Contains("card.Say(Words(racks));"),
            "ai training sources: the cloud's page on the old Tech app and the kit's says what the player's own models take of the AI racks by the day's rule");
    }

    // ---------------------------------------------------------------- the texts

    private static void AiTrainingTexts()
    {
        string[] keys =
        {
            "reallife_tech_product_train", "reallife_tech_product_train_none", "reallife_tech_product_train_own", "reallife_tech_product_train_split", "reallife_tech_product_train_room",
            "reallife_tech_product_train_rent_room", "reallife_tech_product_train_top", "reallife_tech_product_train_rent_top", "reallife_tech_product_train_peak",
            "reallife_tech_product_train_short", "reallife_ui_tech_open_infra", "reallife_tech_needs_model", "reallife_tech_desk_training_own_trade", "reallife_tech_product_train_over",
            "reallife_infra_cloud_gpu_models"
        };
        Check(AllIn(keys, out string missing), "ai training texts: every key in en, pt and zh-cn (" + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        bool plain = true;
        string rough = "none";
        foreach (string key in keys)
        {
            string text = en.TryGetValue(key, out string v) ? v : string.Empty;
            if (text.Length == 0 || text.Contains("!") || text.Contains("—") || text.Contains("–") || text.Contains(" - ") || text.Contains("--") || text.Contains("  "))
            {
                plain = false;
                rough = key;
            }
        }
        Check(plain, "ai training texts: British plain words, no dashes and no exclamation marks (" + rough + ")");
        string screen = ModSource(@"Ui\UiScreens.Tech.cs");
        int a = screen.IndexOf("// KEYS BEGIN", StringComparison.Ordinal), b = screen.IndexOf("// KEYS END", StringComparison.Ordinal);
        bool listed = a >= 0 && b > a;
        foreach (string key in keys)
        {
            listed &= key == "reallife_tech_needs_model" || key == "reallife_tech_desk_training_own_trade" || screen.Substring(Math.Max(0, a), Math.Max(0, b - a)).Contains("\"" + key + "\"");
        }
        Check(listed, "ai training texts: the Tech screen names every key its training settings and its cloud page write");
        string trade = en.TryGetValue("reallife_tech_desk_training_own_trade", out string own) ? own : string.Empty;
        Check(en.TryGetValue("reallife_tech_product_train", out string train) && !train.Contains("if rented") && en.TryGetValue("reallife_tech_needs_model", out string needs) && needs.Contains("AI racks")
              && trade.Contains("AI racks") && trade.Contains("rented") && !trade.Contains("cloud") && !trade.Contains("if you have none"),
            "ai training texts: the level's words, the new product's and the desk's say training runs on your own AI racks, and the desk's that a run rents what they do not give");
        Check(WhatsNewHas(en, "own AI racks now train its models, with or without a cloud"), "ai training texts: What's new tells of the fix");
    }
}
