using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;

// An AI model's training against the compute behind it. A lab making 529 points a day showed its next feature at -211, -882 and -1,000
// points: its work was done long before its training, which came at most 200 units a day whatever its AI racks (360 units each), and the
// racks fed the labs only through an open, licensed cloud. Now the player's own AI racks train the models first, a cloud or not; a model
// may be set as high as its own compute and 200 rented on top cover (InfraRules.TrainMost); a day short of own compute trains less rather
// than rent past 200; the generations are the same size; rival labs train by the same rule on their own racks, as fast as before.
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
    // count, at the highest level its AI racks allow: the director's day (InfraRules.TrainDay on the racks' compute) and the feature's rule.
    private static int AiGenerationDays(int racks, int share, int from, out long pace)
    {
        var file = new FirmsFile { Seed = 5 };
        var lab = new Firm { Place = "lab" };
        Product p = FirmsRules.NewProduct(file, "lab", ProductKind.Model, Segment.Business, 0, "Atlas");
        file.Products.Add(p);
        p.Features = from;
        List<DataCentre> centres = AiRacks(racks, share);
        long own = InfraRules.GpuOwned(centres, share);
        p.Train = InfraRules.TrainMost(InfraRules.TrainOwnFor(file.Products, p, own));
        pace = InfraRules.TrainLevels[p.Train];
        var crew = new Crew { Engineering = 0, SeniorShare = 500, Faults = 100 };
        for (int day = 1; day <= 5000; day++)
        {
            p.Progress += 529;
            long free = own;
            p.Compute += InfraRules.TrainDay(p.Train, ref free, 0, out _);
            FirmsRules.Build(lab, p, crew, 0, 0);
            if (p.Features >= from + 2)
            {
                return day;
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

    // The director's day for the models (TechDirector.Cloud): each model's part past what it may rent kept for it, then the day in the
    // products' order (or the reverse). The rented part of each in rented.
    private static long[] AiDay(List<Product> models, long racks, bool reverse, long[] rented)
    {
        long free = racks, reserve = 0;
        for (int i = 0; i < models.Count; i++)
        {
            reserve += InfraRules.TrainBeyond(models[i].Train);
        }
        var paces = new long[models.Count];
        for (int k = 0; k < models.Count; k++)
        {
            int i = reverse ? models.Count - 1 - k : k;
            reserve -= InfraRules.TrainBeyond(models[i].Train);
            paces[i] = InfraRules.TrainDay(models[i].Train, ref free, reserve, out rented[i]);
        }
        return paces;
    }

    private static void AiTrainingShared()
    {
        bool held = true, paced = true, short1 = true;
        int states = 0;
        string bad = "none";
        uint seed = 17;
        foreach (long racks in new long[] { 0, 25, 360, 500, 1800, 7200, 21600, 100000 })
        {
            for (int n = 1; n <= 4; n++)
            {
                for (int round = 0; round < 12; round++)
                {
                    var file = new FirmsFile { Seed = 9 };
                    var models = new List<Product>();
                    for (int m = 0; m < n; m++)
                    {
                        Product p = FirmsRules.NewProduct(file, "lab", ProductKind.Model, Segment.Business, 0, "M" + m);
                        p.Train = 0;
                        file.Products.Add(p);
                        models.Add(p);
                    }
                    // the player sets the models one after another, each to a level its page allows at the time, some more than once
                    for (int step = 0; step < n * 3; step++)
                    {
                        seed = unchecked(seed * 1103515245u + 12345u);
                        Product p = models[(int)(seed >> 8) % n];
                        int most = InfraRules.TrainMost(InfraRules.TrainOwnFor(file.Products, p, racks));
                        p.Train = (int)((seed >> 16) % (uint)(most + 1));
                    }
                    states++;
                    foreach (Product p in models)
                    {
                        if (p.Train > InfraRules.TrainMost(InfraRules.TrainOwnFor(file.Products, p, racks)))
                        {
                            held = false;
                            bad = racks + " racks, " + n + " models";
                        }
                    }
                    foreach (bool reverse in new[] { false, true })
                    {
                        var rented = new long[n];
                        long[] paces = AiDay(models, racks, reverse, rented);
                        for (int i = 0; i < n; i++)
                        {
                            paced &= paces[i] == InfraRules.TrainLevels[models[i].Train] && rented[i] <= InfraCatalogue.RentedMost && rented[i] >= 0;
                        }
                        // a day short of own compute (an outage, a crew short): it trains less, never renting past the most
                        var less = new long[n];
                        long[] slow = AiDay(models, racks / 2, reverse, less);
                        for (int i = 0; i < n; i++)
                        {
                            short1 &= slow[i] <= InfraRules.TrainLevels[models[i].Train] && less[i] <= InfraCatalogue.RentedMost;
                        }
                    }
                }
            }
        }
        Check(held, "ai training: setting one model to what its page allows never takes another's own compute: every model stays within its most (" + states + " states, " + bad + ")");
        Check(paced, "ai training: on a day of full compute every model trains at its level, in either order, renting no more than " + InfraCatalogue.RentedMost + " units a day");
        Check(short1, "ai training: on a day short of own compute a model trains less and still rents no more than " + InfraCatalogue.RentedMost);
        // no racks: as before, every model rents its whole level
        var renter = new List<Product> { new Product { Kind = (int)ProductKind.Model, Train = 2 }, new Product { Kind = (int)ProductKind.Model, Train = 4 } };
        var all = new long[2];
        long[] rentedPaces = AiDay(renter, 0, false, all);
        Check(rentedPaces[0] == 50 && all[0] == 50 && rentedPaces[1] == 200 && all[1] == 200, "ai training: with no AI racks every model's training is rented at its level, as before");
        // a withdrawn model takes nothing of the racks
        var two = new FirmsFile { Seed = 3 };
        Product live = FirmsRules.NewProduct(two, "lab", ProductKind.Model, Segment.Business, 0, "Live"), gone = FirmsRules.NewProduct(two, "lab", ProductKind.Model, Segment.Business, 0, "Gone");
        two.Products.Add(live);
        two.Products.Add(gone);
        gone.Train = InfraRules.TrainLevels.Length - 1;
        gone.Stage = 2;
        Check(InfraRules.TrainOwnFor(two.Products, live, 7200) == 7200, "ai training: a withdrawn model's level keeps nothing of the racks from the others");
    }

    // ---------------------------------------------------------------- rival labs

    private static void AiTrainingRivals()
    {
        long listed = InfraRules.TrainAlone(SectorCatalogue.RivalLabOwn, out long listedRent), startup = InfraRules.TrainAlone(SectorCatalogue.StartupLabOwn, out long startupRent);
        Check(listed == 1500 && listedRent == 75 && startup == 200 && startupRent == 10,
            "ai training: a rival lab trains by the player's rule on its own racks, as fast as before: a listed company's " + listed + " units a day (" + listedRent + " rented), a startup's " + startup + " ("
            + startupRent + ")");
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
        // a lab copying the player's lead trains the features it copies too
        RivalProduct copier = labs.Count > 0 ? labs[0] : null;
        if (copier == null)
        {
            Check(false, "ai training: the sector has a rival lab to copy a lead");
            return;
        }
        copier.Features = 3;
        copier.Compute = 0;
        long need = 0;
        for (int k = 3; k < 9; k++)
        {
            need += InfraRules.TrainNeed(k);
        }
        long pace = InfraRules.TrainAlone(SectorRules.LabOwn(copier), out _);
        int trainDays = (int)Math.Min(RivalryCatalogue.CopyMost, (need + pace - 1) / pace);
        int slow = RivalryRules.CopyDelay(f, copier, 9);
        copier.Compute = need;
        int fast = RivalryRules.CopyDelay(f, copier, 9);
        Check(slow >= trainDays && slow >= fast && fast >= RivalryCatalogue.CopyLeast,
            "ai training: a rival lab copying a lead takes at least the days its training needs (" + slow + " days for " + need + " unit-days at " + pace + " a day, " + fast + " once trained)");
        Check(ModSource(@"Tech\Rivals\RivalryRules.Reactions.cs").Contains("r.Compute = Math.Max(0L, r.Compute - InfraRules.TrainNeed(k));"),
            "ai training: a copy spends the copied features' training, as a feature built does");
    }

    // ---------------------------------------------------------------- the desk's training run

    private static void AiTrainingDesk()
    {
        SectorFile f = SectorFresh(23);
        var ff = new FirmsFile { Seed = 23 };
        var lab = new Firm { Place = "lab", Reputation = 500 };
        long Run(int train, int compute, bool own)
        {
            Product model = FirmsRules.NewProduct(ff, "lab", ProductKind.Model, Segment.Business, 0, "Run");
            model.Features = 10;
            model.Train = train;
            long before = model.Compute;
            SectorRules.Training(f, model, new SectorProduct { Id = model.Id }, lab, compute, SectorRules.DataSynthetic, SectorRules.SafetySkip, own, 23, 80);
            return model.Compute - before;
        }
        int high = InfraRules.TrainMost(7200), days = SectorCatalogue.TrainRunDays;
        long rentedHigh = Run(high, SectorRules.ComputeRented, true), ownHigh = Run(high, SectorRules.ComputeOwn, true), noRacks = Run(high, SectorRules.ComputeOwn, false), old = Run(2, SectorRules.ComputeRented, false);
        Check(rentedHigh == (long)InfraCatalogue.RentedMost * days && ownHigh == (long)InfraRules.TrainLevels[high] * days && noRacks == rentedHigh && old == 50L * days,
            "ai training: a training run at the desk rents no more a day than a model may (" + rentedHigh + " unit-days), runs the level on the player's own racks (" + ownHigh
            + "), and at the old levels is as before (" + old + ")");
    }

    // ---------------------------------------------------------------- the page's lines

    private static void AiTrainingViews()
    {
        var fmt = new PlainWords();
        Dictionary<string, string> en = LocaleFile("en");
        int top = InfraRules.TrainLevels.Length - 1, states = 0;
        bool named = true, chosen = true;
        string odd = "none";
        foreach (long own in new long[] { 0, 25, 150, 360, 1800, 5900, 7200, 50000, 100000000 })
        {
            int most = InfraRules.TrainMost(own);
            for (int level = 0; level <= top; level++)
            {
                states++;
                ViewLine words = TrainingViews.Level(level, own, fmt), room = TrainingViews.Room(level, own, 360);
                long units = InfraRules.TrainLevels[level], mine = Math.Min(units, own);
                string wantLevel = units == 0 ? "reallife_tech_product_train_none" : mine == 0 && units <= InfraCatalogue.RentedMost ? "reallife_tech_product_train"
                    : units - mine <= 0 ? "reallife_tech_product_train_own" : "reallife_tech_product_train_split";
                string wantRoom = level > most ? "reallife_tech_product_train_short" : level < most ? (own > 0 ? "reallife_tech_product_train_room" : "reallife_tech_product_train_rent_room")
                    : most == top ? "reallife_tech_product_train_peak" : own > 0 ? "reallife_tech_product_train_top" : "reallife_tech_product_train_rent_top";
                bool tone = room.Tone == (level > most ? ViewTone.Warn : ViewTone.Plain);
                bool racks = TrainingViews.WantsRacks(level, own) == (wantRoom == "reallife_tech_product_train_short" || wantRoom == "reallife_tech_product_train_top"
                                                                      || wantRoom == "reallife_tech_product_train_rent_top");
                if (words.Key != wantLevel || room.Key != wantRoom || !tone || !racks)
                {
                    chosen = false;
                    odd = own + " own, level " + level + ": " + words.Key + ", " + room.Key;
                }
                foreach (ViewLine line in new[] { words, room })
                {
                    if (!AiNamesMatch(en, line))
                    {
                        named = false;
                        odd = line.Key;
                    }
                }
            }
        }
        Check(chosen, "ai training: the page says a level's split and cost, what holds the model at its most and what the next level needs, or that it trains below its level (" + states + " states, " + odd + ")");
        Check(named, "ai training: every line's names are its English text's, no more and no fewer (" + odd + ")");
        ViewLine twenty = TrainingViews.Room(InfraRules.TrainMost(7200), 7200, 360);
        string said = AiTrainingFill(en, twenty);
        Check(twenty.Key == "reallife_tech_product_train_top" && said.Contains("Infra tab") && said.Contains("12,000") && said.Contains("4,600"),
            "ai training: the report's lab at its most is told the next level needs 4,600 more units of its own racks and where to buy them (" + said + ")");
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

    private static void AiTrainingSources()
    {
        string cloud = ModSource(@"Tech\TechDirector.Infra.cs"), old = ModSource(@"Tech\Firms\TechApp.Pages.cs"), kit = ModSource(@"Ui\Tech\TechLive.Products.cs");
        string day = MemberText(cloud, "private void Cloud(int day, int cycle, int capacity, int gpu, int outages, int share)");
        Check(day.Contains("reserve += q.K == ProductKind.Model && q.Stage < 2 ? InfraRules.TrainBeyond(q.Train) : 0;") && day.Contains("reserve -= InfraRules.TrainBeyond(p.Train);")
              && day.Contains("InfraRules.TrainDay(p.Train, ref free, reserve, out long bought)") && day.Contains("gpu = (int)free;") && !day.Contains("open ? Math.Min(gpu, want) : 0")
              && day.Contains("p.K != ProductKind.Model || p.Stage >= 2") && day.IndexOf("InfraRules.TrainDay(", StringComparison.Ordinal) < day.IndexOf("if (!open)", StringComparison.Ordinal),
            "ai training sources: the cloud's day trains the models on the player's own AI racks first, a cloud or not, as this check's day does, and leaves the rest for the cloud to sell");
        Check(cloud.Contains("p.Train = TalentCatalogue.Clamp(level, 0, Math.Max(InfraRules.TrainLevel(p.Train), TrainingMost(p, out _, out _)));")
              && cloud.Contains("InfraRules.TrainOwnFor(_firmsFile.Products, p, InfraRules.GpuOwned(_infra.Centres, share))"),
            "ai training sources: a level is set up to the most the model's compute covers, reckoned on the racks less the other models' parts");
        Check(old.Contains("box.Selector(Mathf.Max(most, train) + 1, train, i => Words(TrainingViews.Level(i, own, _studioWords))") && old.Contains("TrainingViews.Room(train, own, perRack)")
              && kit.Contains("Words(TrainingViews.Level(train, own, W)), null, train > 0, train < most") && kit.Contains("TrainingViews.Room(train, own, perRack)")
              && kit.Contains("TrainingViews.WantsRacks(train, own)") && kit.Contains("v.Open(\"tab:\" + TabInfra)"),
            "ai training sources: the old page and the kit's list the levels to the most, say what holds it there, and the kit links to the Infra tab");
        Check(ModSource(@"Tech\Sector\SectorRules.Rivals.cs").Contains("r.Compute += InfraRules.TrainAlone(LabOwn(r), out _);")
              && ModSource(@"Tech\Sector\SectorRules.Infra.cs").Contains("InfraRules.TrainAlone(LabOwn(r), out long rent);")
              && !MemberText(ModSource(@"Tech\TechDirector.SectorDesk.cs"), "private bool OwnGpu()").Contains("Cloud.Open"),
            "ai training sources: rival labs train by the rule on their own racks, and the desk's run uses the player's AI racks with or without a cloud");
        Check(ModSource(@"Tech\Firms\FirmsRules.cs").Contains("p.Compute >= InfraRules.TrainNeed(p.Features)"), "ai training sources: a model's feature still waits for its generation's training");
    }

    // ---------------------------------------------------------------- the texts

    private static void AiTrainingTexts()
    {
        string[] keys =
        {
            "reallife_tech_product_train", "reallife_tech_product_train_none", "reallife_tech_product_train_own", "reallife_tech_product_train_split", "reallife_tech_product_train_room",
            "reallife_tech_product_train_rent_room", "reallife_tech_product_train_top", "reallife_tech_product_train_rent_top", "reallife_tech_product_train_peak",
            "reallife_tech_product_train_short", "reallife_ui_tech_open_infra", "reallife_tech_needs_model", "reallife_tech_desk_training_own_trade"
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
        Check(listed, "ai training texts: the Tech screen names every key its training settings write");
        Check(!en["reallife_tech_product_train"].Contains("if rented") && en["reallife_tech_needs_model"].Contains("AI racks") && en["reallife_tech_desk_training_own_trade"].Contains("AI racks")
              && !en["reallife_tech_desk_training_own_trade"].Contains("cloud"),
            "ai training texts: the level's words, the new product's and the desk's say training runs on your own AI racks, not only a cloud's");
        Check(WhatsNewHas(en, "own AI racks now train its models, with or without a cloud"), "ai training texts: What's new tells of the fix");
    }
}
