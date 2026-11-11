using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Tech;

// An AI lab's page showed its next feature as a negative number of points ("-211 points", then "-882", on a lab making 529 points a day), and a
// licensed product with more features than its rival showed no customers and no reason why (players' reports, 8 and 10 Nov 2026). The first is
// the work running past a feature's cost while the model waits for its training: a model's feature is done only when its generation's training is
// in too (FirmsRules.Build, held in InfraChecks), the work goes on building meanwhile, and the pages wrote the cost less the work. FirmsRules.Wait
// now gives the work still to put in, the work past the cost and the training still to come, none below zero (and nothing at all for a withdrawn
// model, which nothing builds or trains), and ProductViews chooses the lines that say them: both are run here, with the lines' keys and names held to
// the English texts. The second is not a rule gone wrong: a licence launched on better features than its rival's closes a deal in about the days its
// page says, in the firms' own rules and under the tech markets, with nobody selling it and no marketing; what was missing was the saying of it (a
// product not on sale yet, and the markets' licences, whose page had no line for their deals). That saying is held to what the sale does: not before
// the day has counted the market (a save just loaded), and in the niche a founded firm's sale weighs its product in (NicheRules.Market, the one
// working for the sale and the page). The checks that hold the rules' premise (the report's lab, the demand rule) say so; the others fail without
// the fix. The rules, the words, the sources and the texts. --tech-points runs these alone.
internal static partial class Program
{
    private static void TechPoints()
    {
        TechPointsLab();
        TechPointsWait();
        TechPointsAgrees();
        TechPointsWords();
        TechPointsLicence();
        TechPointsLines();
        TechPointsNiche();
        TechPointsSources();
        TechPointsTexts();
    }

    // A whole number as the words write it.
    private static string TechPointsWhole(long n) => n.ToString("N0", CultureInfo.InvariantCulture);

    // The key of a line, or "none" for no line.
    private static string TechPointsKey(ViewLine line) => line != null ? line.Key : "none";

    // Whether any figure a line carries is written below zero.
    private static bool TechPointsNegative(ViewLine line)
    {
        for (int i = 1; line != null && line.Pairs != null && i < line.Pairs.Length; i += 2)
        {
            if (line.Pairs[i] != null && line.Pairs[i].StartsWith("-", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    // Whether a line's names are exactly the {names} its English text holds: no text left with a hole, no value written for nothing.
    private static bool TechPointsFills(ViewLine line, Dictionary<string, string> en)
    {
        if (line == null || !en.TryGetValue(line.Key, out string text))
        {
            return false;
        }
        var holes = new HashSet<string>();
        foreach (Match hole in Regex.Matches(text, @"\{([a-z_]+)\}"))
        {
            holes.Add(hole.Groups[1].Value);
        }
        var given = new HashSet<string>();
        for (int i = 0; line.Pairs != null && i + 1 < line.Pairs.Length; i += 2)
        {
            given.Add(line.Pairs[i]);
        }
        return holes.SetEquals(given);
    }

    // ---------------------------------------------------------------- the report's lab

    private static void TechPointsLab()
    {
        Dictionary<string, string> en = LocaleFile("en");
        var file = new FirmsFile { Seed = 7 };
        var lab = new Firm { Place = "lab", Kind = (int)FirmKind.AiLab, Focus = (int)Focus.Balanced, Reputation = 500 };
        Product m = FirmsRules.NewProduct(file, "lab", ProductKind.Model, Segment.Business, 0, "Atlas");
        file.Products.Add(m);
        Crew crew = Team(529);   // 529 points a day, as the report's lab made
        int lowest = 0, waiting = 0, mostAhead = 0, wrongDays = 0;
        bool sound = true, same = true;
        string firstWrong = "none";
        for (int day = 1; day <= 40; day++)
        {
            FirmsRules.Build(lab, m, crew, 529, 0);                  // the firm's day builds first...
            m.Compute += InfraRules.TrainLevels[m.Train];            // ...then the cloud's day adds the model's training (TechDirector.Infra)
            FeatureWait w = FirmsRules.Wait(m);
            int shown = FirmsRules.FeatureCost(m.Features) - m.Progress;   // what the pages once wrote as the points to go
            lowest = Math.Min(lowest, shown);
            waiting += w.Trains ? 1 : 0;
            mostAhead = Math.Max(mostAhead, w.Ahead);
            sound &= w.Points >= 0 && w.Ahead >= 0 && w.Training >= 0 && w.Days >= 0 && w.Done >= 0 && w.Done <= 1000 && (w.Points == 0 || w.Ahead == 0);
            same &= shown == w.Points - w.Ahead;
            // the words the page writes that day: the old page's and the tech markets' next feature, and the model's training
            ViewLine old = ProductViews.NextFeature(m, false), markets = ProductViews.NextFeature(m, true), training = ProductViews.TrainingWait(m);
            bool says = w.Trains
                ? old != null && old.Key == "reallife_tech_product_build_wait" && markets != null && markets.Key == "reallife_tech_mkt_product_build_wait"
                  && TechPointsPair(old, "ahead") == TechPointsWhole(w.Ahead) && TechPointsPair(markets, "ahead") == TechPointsWhole(w.Ahead)
                : old != null && old.Key == "reallife_tech_product_build" && markets != null && markets.Key == "reallife_tech_mkt_product_build"
                  && TechPointsPair(old, "next") == TechPointsWhole(w.Points) && TechPointsPair(markets, "next") == TechPointsWhole(w.Points);
            bool trains = w.Training > 0
                ? training != null && training.Key == "reallife_tech_product_training_days" && TechPointsPair(training, "days") == TechPointsWhole(w.Days)
                  && TechPointsPair(training, "units") == InfraRules.TrainLevels[m.Train].ToString(CultureInfo.InvariantCulture)
                : training == null;
            bool right = says && trains && !TechPointsNegative(old) && !TechPointsNegative(markets) && !TechPointsNegative(training)
                         && TechPointsFills(old, en) && TechPointsFills(markets, en) && (training == null || TechPointsFills(training, en));
            wrongDays += right ? 0 : 1;
            firstWrong = !right && firstWrong == "none" ? "day " + day + " (" + TechPointsKey(old) + ")" : firstWrong;
        }
        Check(lowest < -1000 && waiting >= 30 && m.Features >= 2,
            "tech points, the premise: the report's lab (529 points a day, 50 units of training a day) has its work far past a feature's cost while it trains: the old count of points to go fell to "
            + lowest + ", and the model waited for its training on " + waiting + " of 40 days (" + m.Features + " features built)");
        Check(sound && same && mostAhead > 1000,
            "tech points: through the same forty days what a feature waits for is never below zero (the work still to put in, the work past its cost, the training to come and its days), "
            + "the old count was exactly the first less the second, and the work past the cost reached " + mostAhead);
        Check(wrongDays == 0,
            "tech points words: through the same forty days the lab's page never carries a figure below zero, says the work is done and waits for the training on the days the rule says so, "
            + "and gives the training's days at its level (" + wrongDays + " days wrong; first: " + firstWrong + ")");
    }

    // ---------------------------------------------------------------- the rule itself

    private static void TechPointsWait()
    {
        var file = new FirmsFile { Seed = 3 };
        int cost = FirmsRules.FeatureCost(0);
        long need = InfraRules.TrainNeed(0);
        long level2 = InfraRules.TrainLevels[2], level4 = InfraRules.TrainLevels[4];
        Product m = FirmsRules.NewProduct(file, "lab", ProductKind.Model, Segment.Business, 0, "Atlas");
        m.Progress = cost * 3;
        m.Compute = 100;
        FeatureWait w = FirmsRules.Wait(m);   // the work in with two features' cost over, a quarter of the training, the default level
        Check(m.Train == 2 && w.Points == 0 && w.Ahead == cost * 2 && w.Training == need - 100 && w.Days == (int)((need - 100 + level2 - 1) / level2) && w.Trains && w.Done == 250 && !w.Closed,
            "tech points: a model whose work is in and whose training is a quarter done waits for the training alone (" + w.Ahead + " points over, " + w.Training + " unit-days to come, "
            + w.Days + " days, " + w.Done / 10 + "% done)");
        m.Train = 0;
        Check(FirmsRules.Wait(m).Days == -1 && FirmsRules.Wait(m).Training == need - 100, "tech points: training switched off gives no days, and the training still to come is the same");
        m.Train = 4;
        Check(FirmsRules.Wait(m).Days == (int)((need - 100 + level4 - 1) / level4) && FirmsRules.Wait(m).Days < w.Days, "tech points: a higher training level takes fewer days");
        m.Compute = need;
        FeatureWait trained = FirmsRules.Wait(m);
        Check(trained.Training == 0 && trained.Days == 0 && !trained.Trains && trained.Done == 1000 && trained.Points == 0, "tech points: a model whose training is in waits for nothing");
        m.Progress = cost / 2;
        FeatureWait half = FirmsRules.Wait(m);
        Check(half.Points == cost - cost / 2 && half.Ahead == 0 && half.Training == 0 && !half.Trains && half.Done == (cost / 2) * 1000 / cost,
            "tech points: a feature with its training in and half its work waits for points, as every product's does");
        Product soft = FirmsRules.NewProduct(file, "s", ProductKind.Saas, Segment.Business, 0, "Soft");
        soft.Progress = cost * 3;
        soft.Compute = 0;
        FeatureWait other = FirmsRules.Wait(soft);
        Check(other.Training == 0 && other.Days == 0 && !other.Trains && other.Done == 1000, "tech points: a product that is not a model needs no training, whatever its compute");
        Product odd = FirmsRules.NewProduct(file, "o", ProductKind.Model, Segment.Business, 0, "Odd");
        odd.Progress = -50;
        odd.Compute = -9;
        odd.Train = 99;
        FeatureWait bad = FirmsRules.Wait(odd);
        Check(bad.Points == cost && bad.Ahead == 0 && bad.Training == need && bad.Days >= 0 && bad.Done == 0,
            "tech points: a damaged record (negative work, negative compute, a level out of range) reads as none and as the top level, never below zero");
        // a withdrawn model: the day does not build it and the cloud does not train it, so its banked work waits for nothing
        Product gone = FirmsRules.NewProduct(file, "lab", ProductKind.Model, Segment.Business, 0, "Gone");
        gone.Progress = cost * 3;
        gone.Compute = 100;
        bool beforeTrains = FirmsRules.Wait(gone).Trains;
        gone.Stage = 2;
        FeatureWait closed = FirmsRules.Wait(gone);
        Check(beforeTrains && closed.Closed && !closed.Trains && closed.Points == 0 && closed.Ahead == 0 && closed.Training == 0 && closed.Days == 0 && closed.Done == 0,
            "tech points: the same model withdrawn waits for nothing: its banked work is not a wait for training, as nothing builds it and nothing trains it");
    }

    // The page and the build must wait for the same things: a feature is finished by a day with no new work just when the page says it waits for nothing.
    private static void TechPointsAgrees()
    {
        var firm = new Firm { Place = "x", Focus = (int)Focus.Balanced };
        Crew crew = Team(0);
        int cases = 0, disagree = 0;
        string first = string.Empty;
        foreach (int stage in new[] { 0, 1 })
        {
            foreach (ProductKind kind in new[] { ProductKind.Model, ProductKind.Saas, ProductKind.Licence, ProductKind.App })
            {
                foreach (int features in new[] { 0, 1, 4 })
                {
                    int cost = FirmsRules.FeatureCost(features);
                    long need = InfraRules.TrainNeed(features);
                    foreach (int progress in new[] { 0, cost - 1, cost, cost + 1, cost * 3 })
                    {
                        foreach (long compute in new[] { 0L, need - 1, need, need + 1 })
                        {
                            Product p = FirmsRules.NewProduct(new FirmsFile { Seed = 3 }, "x", kind, Segment.Business, 0, "P");
                            p.Stage = stage;
                            p.Features = features;
                            p.Progress = progress;
                            p.Compute = compute;
                            FeatureWait wait = FirmsRules.Wait(p);
                            int built = FirmsRules.Build(firm, p, crew, 0, 0);
                            bool ready = wait.Points == 0 && wait.Training == 0;
                            cases++;
                            if ((built > 0) != ready)
                            {
                                disagree++;
                                first = first.Length == 0 ? kind + " at " + features + " features, " + progress + " work, " + compute + " compute" : first;
                            }
                        }
                    }
                }
            }
        }
        Check(cases == 480 && disagree == 0,
            "tech points: what a page says a feature waits for is what the build waits for, in " + cases + " states of four kinds of product, being built and on sale (" + (disagree == 0 ? "all agree" : first) + ")");
    }

    // ---------------------------------------------------------------- the words

    // The lines a product's page says, chosen by ProductViews and held to the rule's figures and to the English texts: in a grid of products of four kinds at three stages, with the work in
    // and short, the training in and short, and the level none and the default, and a withdrawn product (which nothing builds or trains) saying none of it.
    private static void TechPointsWords()
    {
        Dictionary<string, string> en = LocaleFile("en");
        var file = new FirmsFile { Seed = 3 };
        int nextCases = 0, nextWrong = 0, trainCases = 0, trainWrong = 0, saleCases = 0, saleWrong = 0;
        string nextFirst = "none", trainFirst = "none", saleFirst = "none";
        foreach (ProductKind kind in new[] { ProductKind.Model, ProductKind.Saas, ProductKind.Licence, ProductKind.App })
        {
            foreach (int stage in new[] { 0, 1, 2 })
            {
                foreach (int features in new[] { 0, 1, 4 })
                {
                    int cost = FirmsRules.FeatureCost(features);
                    long need = InfraRules.TrainNeed(features);
                    foreach (int progress in new[] { 0, cost - 1, cost, cost + 1, cost * 3 })
                    {
                        foreach (long compute in new[] { 0L, need - 1, need, need + 1 })
                        {
                            foreach (int train in new[] { 0, 2 })
                            {
                                Product p = FirmsRules.NewProduct(file, "x", kind, Segment.Business, 0, "P");
                                p.Stage = stage;
                                p.Features = features;
                                p.Progress = progress;
                                p.Compute = compute;
                                p.Train = train;
                                FeatureWait w = FirmsRules.Wait(p);
                                string at = kind + " at stage " + stage + ", " + features + " features, " + progress + " work, " + compute + " compute, level " + train;
                                foreach (bool markets in new[] { false, true })
                                {
                                    ViewLine line = ProductViews.NextFeature(p, markets);
                                    string key = markets ? (w.Trains ? "reallife_tech_mkt_product_build_wait" : "reallife_tech_mkt_product_build")
                                        : (w.Trains ? "reallife_tech_product_build_wait" : "reallife_tech_product_build");
                                    bool right = stage >= 2
                                        ? line == null
                                        : line != null && line.Key == key && TechPointsPair(line, "features") == features.ToString(CultureInfo.InvariantCulture) && !TechPointsNegative(line)
                                          && (w.Trains ? TechPointsPair(line, "ahead") == TechPointsWhole(w.Ahead) && TechPointsPair(line, "next") == null
                                              : TechPointsPair(line, "next") == TechPointsWhole(w.Points) && TechPointsPair(line, "ahead") == null)
                                          && markets == (TechPointsPair(line, "rival") == null) && TechPointsFills(line, en);
                                    nextCases++;
                                    nextWrong += right ? 0 : 1;
                                    nextFirst = !right && nextFirst == "none" ? (markets ? "markets: " : "old page: ") + at : nextFirst;
                                }
                                ViewLine training = ProductViews.TrainingWait(p);
                                bool owed = kind == ProductKind.Model && stage < 2 && w.Training > 0;
                                bool rightTraining = !owed
                                    ? training == null
                                    : w.Days < 0
                                        ? training != null && training.Key == "reallife_tech_product_training_off" && training.Tone == ViewTone.Warn && TechPointsFills(training, en)
                                        : training != null && training.Key == "reallife_tech_product_training_days" && training.Tone == ViewTone.Plain
                                          && TechPointsPair(training, "units") == InfraRules.TrainLevels[train].ToString(CultureInfo.InvariantCulture)
                                          && TechPointsPair(training, "days") == TechPointsWhole(w.Days) && TechPointsFills(training, en);
                                trainCases++;
                                trainWrong += rightTraining ? 0 : 1;
                                trainFirst = !rightTraining && trainFirst == "none" ? at : trainFirst;
                            }
                        }
                    }
                }
            }
        }
        foreach (ProductKind kind in new[] { ProductKind.Model, ProductKind.Saas, ProductKind.Licence, ProductKind.App, ProductKind.Game })
        {
            foreach (int stage in new[] { 0, 1, 2 })
            {
                foreach (int features in new[] { 0, 2, 3, 4 })
                {
                    Product p = FirmsRules.NewProduct(file, "x", kind, Segment.Business, 0, "P");
                    p.Stage = stage;
                    p.Features = features;
                    ViewLine sale = ProductViews.OffSale(p);
                    bool unsold = stage == 0 && kind != ProductKind.Game;
                    bool ready = FirmsRules.CanLaunch(p);
                    bool right = !unsold
                        ? sale == null
                        : sale != null && TechPointsFills(sale, en)
                          && (ready ? sale.Key == "reallife_tech_product_off_sale_ready" && sale.Pairs.Length == 0
                              : sale.Key == "reallife_tech_product_off_sale_short" && TechPointsPair(sale, "launch") == FirmsCatalogue.LaunchFeatures.ToString(CultureInfo.InvariantCulture)
                                && TechPointsPair(sale, "features") == features.ToString(CultureInfo.InvariantCulture));
                    saleCases++;
                    saleWrong += right ? 0 : 1;
                    saleFirst = !right && saleFirst == "none" ? kind + " at stage " + stage + ", " + features + " features" : saleFirst;
                }
            }
        }
        Check(nextCases == 2880 && nextWrong == 0,
            "tech points words: the next feature's line says the work is done and waits for the training just when the rule says so, writes the points still to put in or the points to spare, never a figure below zero, "
            + "names the rival only on the old page, and is none for a withdrawn product, in " + nextCases + " states (" + nextWrong + " wrong; first: " + nextFirst + ")");
        Check(trainCases == 1440 && trainWrong == 0,
            "tech points words: a model's training line gives the days at its level, or says that none is run when the level is none, only while training is short and the model is not withdrawn, in "
            + trainCases + " states (" + trainWrong + " wrong; first: " + trainFirst + ")");
        Check(saleCases == 60 && saleWrong == 0,
            "tech points words: a product being built says it is not on sale and what puts it on sale (press Launch, or the features it needs), and a product on sale, a withdrawn one and a game say nothing, in "
            + saleCases + " states (" + saleWrong + " wrong; first: " + saleFirst + ")");
    }

    // ---------------------------------------------------------------- a licence better than its rival's

    // The first day of a launched licence's deals in the firms' own rules with nobody selling it: the days its page promised at launch, the companies it has by the end and its appeal.
    private static int TechPointsFirstDeal(int features, int rival, int seed, int days, out int promised, out long users, out int appeal)
    {
        var f = new FirmsFile { Seed = seed };
        var firm = new Firm { Place = "l", Reputation = 500 };
        Product lic = FirmsRules.NewProduct(f, "l", ProductKind.Licence, Segment.Enterprise, 0, null);
        lic.RivalFeatures = rival;
        lic.Features = features;
        f.Products.Add(lic);
        FirmsRules.Launch(lic, 10);
        Crew c = Team(16);
        appeal = FirmsRules.Appeal(lic, firm, c);
        promised = FirmsRules.LicenceDealDays(FirmsRules.LicenceRate(appeal, FirmsRules.CycleSpend[1], c.Selling));
        int first = -1;
        for (int d = 11; d <= 10 + days; d++)
        {
            long won = FirmsRules.Sell(f, firm, lic, c, 10, d, 1, 1000).Won;
            first = won > 0 && first < 0 ? d - 10 : first;
        }
        users = lic.Users;
        return first;
    }

    // A licence launched on day 1 in the documented city's Licences market (Codeharbour's, at twelve features) with so many features, marketing level and selling points, sold for
    // so many days by the tech markets' day: the day of its first deal, the pace its page would promise at the start and the companies its pull earned it at the end.
    private static Product TechPointsSold(int features, int marketing, int selling, int days, out int first, out SectorFile f, out long target, out int promised)
    {
        var ff = new FirmsFile { Seed = 5 };
        f = SectorFresh(5);
        var firm = new Firm { Place = "p", Reputation = 700 };
        ff.Firms.Add(firm);
        Product p = FirmsRules.NewProduct(ff, "p", ProductKind.Licence, Segment.Enterprise, 0, "Test");
        p.Features = features;
        p.Marketing = marketing;
        ff.Products.Add(p);
        FirmsRules.Launch(p, 1);
        Crew c = Team(16);
        c.Selling = selling;
        int m = SectorRules.MarketOf(p);
        first = -1;
        target = 0;
        promised = 0;
        for (int day = 2; day <= days; day++)
        {
            SectorRules.BeginDay(f, day, 1);
            SectorRules.Count(f, p);
            SectorRules.Attract(f);
            SectorMarketDay md = f.Markets[m];
            int appeal = SectorRules.Appeal(p, firm, c, md), aware = SectorRules.Awareness(FirmsCatalogue.MarketingDay[p.Marketing], md.Buyers, SectorRules.Have(p), md.Who, 0);
            long pull = SectorRules.Attraction(appeal, aware);
            SectorRules.Join(md, pull);
            target = SectorRules.Target(md, pull);
            promised = promised == 0 ? FirmsRules.LicenceDealDays(SectorRules.LicenceRate(appeal, md.Spend, c.Selling)) : promised;
            ProductDay r = SectorRules.SellDay(ff, firm, p, c, 50, day, md, appeal, aware, 1000);
            first = r.Won > 0 && first < 0 ? day - 1 : first;
        }
        return p;
    }

    // The premise of the second report: the demand rule is right, so what was missing was the saying of it (the lines below).
    private static void TechPointsLicence()
    {
        // the firms' own rules: ahead of its rival by four features, a licence closes a deal when its page says, and goes on closing them; the weakest launch comes a long way after
        int firstBetter = TechPointsFirstDeal(FirmsCatalogue.FeaturesMost, FirmsCatalogue.FeaturesMost - 4, 31, 90, out int promised, out long users, out int appeal);
        int firstWeak = TechPointsFirstDeal(FirmsCatalogue.LaunchFeatures, FirmsCatalogue.FeaturesMost - 4, 31, 90, out _, out _, out _);
        Check(appeal > 1000 && firstBetter > 0 && firstBetter >= promised - 1 && firstBetter <= promised * 5 / 4 + 2 && users >= 5 && (firstWeak < 0 || firstBetter < firstWeak),
            "tech points, the premise: a licence with more features than its rival and nobody selling it closes its first deal when its page says (day " + firstBetter + ", the page said " + promised
            + ", appeal " + appeal + "), has " + users + " companies in 90 days, and the weakest launch's first deal comes on day " + firstWeak);
        // the tech markets: the same product with no marketing at all and nobody selling it still earns a share of the companies, and sells
        Product better = TechPointsSold(15, 0, 0, 90, out int sFirst, out SectorFile sf, out long target, out int sPromised);
        TechPointsSold(3, 0, 0, 90, out int wFirst, out _, out _, out _);
        Check(target >= 3 && sFirst > 0 && sFirst >= sPromised - 1 && sFirst <= sPromised * 5 / 4 + 2 && better.Users >= 3 && (wFirst < 0 || sFirst < wFirst),
            "tech points, the premise: under the tech markets a licence with fifteen features against its rival's twelve, no marketing and nobody selling it earns a share of " + target
            + " companies and closes its first deal when its page says (day " + sFirst + ", the page said " + sPromised + "), " + better.Users + " in 90 days; the weakest launch's comes on day " + wFirst);
        Check(sf.Markets[(int)SectorMarket.Licences].Buyers > 100, "tech points, the premise: the documented city's licence market has some hundreds of companies (" + sf.Markets[(int)SectorMarket.Licences].Buyers + ")");
    }

    private static ViewLine TechPointsLine(ViewSection section, string key)
    {
        for (int i = 0; section != null && i < section.Lines.Count; i++)
        {
            if (section.Lines[i].Key == key)
            {
                return section.Lines[i];
            }
        }
        return null;
    }

    // The value written after a name in a line's pairs, or null.
    private static string TechPointsPair(ViewLine line, string name)
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

    // Whether a section says where a product's customers are heading or how its deals close: the lines that state what its pull earns it.
    private static bool TechPointsClaims(ViewSection section)
    {
        foreach (string key in new[]
                 {
                     "reallife_tech_mkt_product_heading_up", "reallife_tech_mkt_product_heading_down", "reallife_tech_mkt_product_heading_flat",
                     "reallife_tech_product_deals", "reallife_tech_product_deals_none", "reallife_tech_mkt_product_deals_full"
                 })
        {
            if (TechPointsLine(section, key) != null)
            {
                return true;
            }
        }
        return false;
    }

    // What a licence's market section says of its deals, and when it is silent.
    private static void TechPointsLines()
    {
        Product lic = TechPointsSold(15, 0, 0, 40, out _, out SectorFile f, out _, out _);
        SectorMarketDay md = f.Markets[(int)SectorMarket.Licences];
        var firm = new Firm { Place = "p", Reputation = 700 };
        Crew c = Team(16);
        int appeal = SectorRules.Appeal(lic, firm, c, md), aware = SectorRules.Awareness(FirmsCatalogue.MarketingDay[0], md.Buyers, SectorRules.Have(lic), md.Who, 0);
        var fmt = new SectorWords();
        string alone = FirmsRules.LicenceDealDays(SectorRules.LicenceRate(appeal, md.Spend, 0)).ToString(), sold = FirmsRules.LicenceDealDays(SectorRules.LicenceRate(appeal, md.Spend, 16)).ToString();
        ViewLine none = TechPointsLine(SectorViews.Market(f, lic, appeal, aware, fmt, 0), "reallife_tech_product_deals_none");
        ViewLine sellers = TechPointsLine(SectorViews.Market(f, lic, appeal, aware, fmt, 16), "reallife_tech_product_deals");
        ViewSection silent = SectorViews.Market(f, lic, appeal, aware, fmt);
        Check(TechPointsPair(none, "days") == alone && TechPointsPair(sellers, "days") == sold && int.Parse(sold) < int.Parse(alone)
              && TechPointsLine(silent, "reallife_tech_product_deals_none") == null && TechPointsLine(silent, "reallife_tech_product_deals") == null,
            "tech points: a licence on sale under the tech markets says how many days a deal takes (" + alone + " alone, " + sold + " with sellers) and is silent when the firm's selling is not given");
        // a licence that holds every company its pull earns it says so, and a pull that earns less than one company says that
        long users = lic.Users;
        lic.Users = 100000;
        ViewSection holds = SectorViews.Market(f, lic, appeal, aware, fmt, 0);
        ViewLine full = TechPointsLine(holds, "reallife_tech_mkt_product_deals_full");
        lic.Users = 0;
        long buyers = md.Buyers;
        md.Buyers = 3;
        ViewSection tiny = SectorViews.Market(f, lic, appeal, aware, fmt, 0);
        ViewLine none0 = TechPointsLine(tiny, "reallife_tech_mkt_product_deals_full");
        md.Buyers = buyers;
        lic.Users = users;
        Check(full != null && TechPointsPair(full, "have") == "100000" && TechPointsLine(holds, "reallife_tech_product_deals_none") == null
              && none0 != null && TechPointsPair(none0, "target") == "0" && TechPointsPair(none0, "have") == "0" && TechPointsLine(tiny, "reallife_tech_product_deals_none") == null,
            "tech points: a licence past the companies its pull earns it, or whose pull earns it fewer than one, says what it earns and has instead of a pace it cannot keep");
        // only a licence on sale has a deals line
        Product off = FirmsRules.NewProduct(new FirmsFile { Seed = 8 }, "p", ProductKind.Licence, Segment.Enterprise, 0, "Off");
        off.Features = 15;
        Product soft = Saas(new FirmsFile { Seed = 4 }, Segment.Enterprise, 15, 0);
        ViewSection offSection = SectorViews.Market(f, off, appeal, aware, fmt, 0), softSection = SectorViews.Market(f, soft, appeal, aware, fmt, 0);
        Check(TechPointsLine(offSection, "reallife_tech_product_deals_none") == null && TechPointsLine(offSection, "reallife_tech_mkt_product_deals_full") == null
              && TechPointsLine(softSection, "reallife_tech_product_deals_none") == null && TechPointsLine(softSection, "reallife_tech_mkt_product_deals_full") == null,
            "tech points: a licence not on sale yet, and a product that is not a licence, have no deals line");
        // a product not on sale has no customers heading anywhere: the section keeps its buyers and going features, and drops the line that says it grows
        bool heads = false, headsOff = false;
        foreach (string way in new[] { "up", "down", "flat" })
        {
            heads |= TechPointsLine(softSection, "reallife_tech_mkt_product_heading_" + way) != null;
            headsOff |= TechPointsLine(offSection, "reallife_tech_mkt_product_heading_" + way) != null;
        }
        Check(heads && !headsOff && TechPointsLine(offSection, "reallife_tech_mkt_product_buyers") != null && TechPointsLine(offSection, "reallife_tech_mkt_product_known") != null,
            "tech points: a product on sale says where its customers are heading, and one not on sale yet does not, though its market's buyers and how known it is stay");
        // a save just loaded: the markets are worked out each day and not saved, so until the day counts them they hold no buyers, and the pull a product earns against nothing says
        // nothing of the product: no heading, no pace and no "no more deals close", for a licence with companies or with none, while on a counted market the same licence is told both
        var cold = new SectorFile { Seed = 5, YearDays = SectorYear };
        bool coldClaims = false, countedSays = true;
        foreach (long companies in new[] { 0L, 5L })
        {
            Product loaded = FirmsRules.NewProduct(new FirmsFile { Seed = 8 }, "p", ProductKind.Licence, Segment.Enterprise, 0, "Loaded");
            loaded.Features = 15;
            loaded.Users = companies;
            FirmsRules.Launch(loaded, 1);
            int coldAppeal = SectorRules.Appeal(loaded, firm, c, cold.Markets[(int)SectorMarket.Licences]);
            ViewSection saved = SectorViews.Market(cold, loaded, coldAppeal, 0, fmt, 0);
            coldClaims |= TechPointsClaims(saved) || TechPointsLine(saved, "reallife_tech_mkt_product_buyers") == null || TechPointsLine(saved, "reallife_tech_mkt_product_no_cap") == null;
            ViewSection counted = SectorViews.Market(f, loaded, appeal, aware, fmt, 0);
            bool heading = false;
            foreach (string way in new[] { "up", "down", "flat" })
            {
                heading |= TechPointsLine(counted, "reallife_tech_mkt_product_heading_" + way) != null;
            }
            countedSays &= heading && (TechPointsLine(counted, "reallife_tech_product_deals_none") != null || TechPointsLine(counted, "reallife_tech_mkt_product_deals_full") != null);
        }
        Check(!coldClaims && countedSays,
            "tech points: a licence on a save just loaded (its markets not counted yet) is told nothing of where its customers are heading or how its deals close, with no companies or with five, "
            + "though its market's buyers and the rule that there is no cap but the market stay; on a counted market the same licence is told both");
    }

    // ---------------------------------------------------------------- a founded firm's licence in its niche

    // A licence of fifteen features on sale in the documented city's counted market (the day's pulls give it the whole market's appeal and awareness), and the view the sale weighs it in for a
    // firm founded in the Headquarters niche with so many businesses there, a launch channel and the founder's selling points a sales channel adds.
    private sealed class TechPointsScene
    {
        public SectorFile File;
        public Product Licence;
        public Firm Firm;
        public Crew Whole, Niche;            // the firm's crew as the whole market sees it, and as the niche's sale does
        public int Appeal, Aware;            // the whole market's, from the day's pulls
        public int NicheAppeal, NicheAware;  // the niche's
        public SectorMarketDay View;         // the niche's market; the whole market's when the product sells into no niche
        public int Served;
        public long WholeTarget, NicheTarget;
    }

    private static TechPointsScene TechPointsMakeScene(long users, long nicheBuyers, int niche, int channel, int founder, RivalryWorld world, bool placed = false)
    {
        var s = new TechPointsScene { File = SectorFresh(5), Firm = new Firm { Place = "p", Reputation = 700 } };
        var ff = new FirmsFile { Seed = 5 };
        s.Licence = FirmsRules.NewProduct(ff, "p", ProductKind.Licence, Segment.Enterprise, 0, "Founded");
        s.Licence.Features = 15;
        ff.Products.Add(s.Licence);
        FirmsRules.Launch(s.Licence, 1);
        s.Licence.Users = users;
        s.Whole = Team(16);
        SectorMarketDay md = s.File.Markets[SectorRules.MarketOf(s.Licence)];
        s.Appeal = SectorRules.Appeal(s.Licence, s.Firm, s.Whole, md);
        s.Aware = SectorRules.Awareness(FirmsCatalogue.MarketingDay[0], md.Buyers, SectorRules.Have(s.Licence), md.Who, 0);
        long pulled = SectorRules.Attraction(s.Appeal, s.Aware);
        SectorRules.Join(md, pulled);   // its pull joins the market's, as the day's pulls do
        s.WholeTarget = SectorRules.Target(md, pulled);
        var founded = new FoundedFirm { Place = "p", Niche = niche, Fit = 600, Channel = channel, Placed = placed };
        var today = new long[NicheCatalogue.Count];
        today[NicheCatalogue.Headquarters] = nicheBuyers;
        s.NicheAppeal = s.Appeal;
        s.NicheAware = s.Aware;
        s.Niche = s.Whole;
        s.View = NicheRules.Market(new SectorMarketDay(), founded, s.Licence, s.Firm, md, today, null, world, 2, pulled, founder, ref s.NicheAppeal, ref s.NicheAware, ref s.Niche, out s.Served);
        s.NicheTarget = SectorRules.Target(s.View, SectorRules.Attraction(s.NicheAppeal, s.NicheAware));
        return s;
    }

    // The sale and the page weigh a founded firm's licence in the same market. In a niche of six businesses with five companies already signed, the whole market would have the licence
    // closing a deal every few days while its niche holds all it can: the page says that, and the sale closes none and banks nothing. In a niche with room, and a sales channel adding
    // the founder's selling points, the page's days are the days to the sale's first deal. A niche with no businesses today, a founding that does not count and a niche that does not buy
    // in the product's market are left out of it, or say nothing of the pull.
    private static void TechPointsNiche()
    {
        var fmt = new SectorWords();
        TechPointsScene small = TechPointsMakeScene(5, 6, NicheCatalogue.Headquarters, FoundCatalogue.ChannelNone, 0, null);
        SectorMarketDay whole = small.File.Markets[SectorRules.MarketOf(small.Licence)];
        ViewSection wholePage = SectorViews.Market(small.File, small.Licence, small.Appeal, small.Aware, fmt, small.Whole.Selling);   // the whole market's inputs, as the page once took
        ViewSection nichePage = SectorViews.Market(small.File, small.Licence, small.NicheAppeal, small.NicheAware, fmt, small.Niche.Selling, small.View);
        ViewLine full = TechPointsLine(nichePage, "reallife_tech_mkt_product_deals_full");
        ProductDay day = SectorRules.SellDay(new FirmsFile { Seed = 5 }, small.Firm, small.Licence, small.Niche, 50, 2, small.View, small.NicheAppeal, small.NicheAware, 1000);
        Check(!ReferenceEquals(small.View, whole) && small.Served == 1 << NicheCatalogue.Headquarters && small.View.Buyers == 6 && small.WholeTarget > 5 && small.NicheTarget <= 5
              && TechPointsLine(wholePage, "reallife_tech_product_deals_none") != null
              && full != null && TechPointsPair(full, "target") == small.NicheTarget.ToString(CultureInfo.InvariantCulture) && TechPointsPair(full, "have") == "5"
              && TechPointsLine(nichePage, "reallife_tech_product_deals_none") == null && day.Won == 0 && small.Licence.Carry == 0,
            "tech points niche: a licence with five companies in a niche of six businesses is told its niche holds all it earns (about " + small.NicheTarget + "), where the whole market's inputs "
            + "would have had it closing a deal every few days (it earns about " + small.WholeTarget + " there), and the sale closes none and banks none");
        TechPointsScene roomy = TechPointsMakeScene(0, 80, NicheCatalogue.Headquarters, FoundCatalogue.ChannelSales, 24, null);
        ViewSection roomyPage = SectorViews.Market(roomy.File, roomy.Licence, roomy.NicheAppeal, roomy.NicheAware, fmt, roomy.Niche.Selling, roomy.View);
        ViewLine pace = TechPointsLine(roomyPage, "reallife_tech_product_deals");
        ViewLine plain = TechPointsLine(SectorViews.Market(roomy.File, roomy.Licence, roomy.Appeal, roomy.Aware, fmt, roomy.Whole.Selling), "reallife_tech_product_deals_none");
        int promised = pace != null ? int.Parse(TechPointsPair(pace, "days"), CultureInfo.InvariantCulture) : 0;
        int plainDays = plain != null ? int.Parse(TechPointsPair(plain, "days"), CultureInfo.InvariantCulture) : 0;
        int first = -1;
        var sale = new FirmsFile { Seed = 5 };
        for (int d = 1; d <= promised + 10 && first < 0; d++)
        {
            first = SectorRules.SellDay(sale, roomy.Firm, roomy.Licence, roomy.Niche, 50, d + 1, roomy.View, roomy.NicheAppeal, roomy.NicheAware, 1000).Won > 0 ? d : first;
        }
        Check(pace != null && promised > 0 && first == promised && roomy.Niche.Selling == roomy.Whole.Selling + 24 && promised < plainDays && roomy.NicheTarget > 0,
            "tech points niche: in a niche with room and a sales channel adding the founder's selling points, the page's days to a deal (" + promised + ") are the days to the sale's first (" + first
            + "), where the whole market's inputs said " + plainDays);
        TechPointsScene empty = TechPointsMakeScene(5, 0, NicheCatalogue.Headquarters, FoundCatalogue.ChannelNone, 0, null);
        ViewSection emptyPage = SectorViews.Market(empty.File, empty.Licence, empty.NicheAppeal, empty.NicheAware, fmt, empty.Niche.Selling, empty.View);
        Check(empty.View.Buyers == 0 && !TechPointsClaims(emptyPage) && TechPointsLine(emptyPage, "reallife_tech_mkt_product_buyers") != null,
            "tech points niche: a niche with no businesses today has no pull to speak of: the page says neither where the customers are heading nor how deals close");
        // no niche: a founding that does not count (an older save's firm not taken up), and a niche whose businesses do not buy in the product's market, leave the market, the appeal, the awareness
        // and the crew as they were, and say so with no niche served
        TechPointsScene placed = TechPointsMakeScene(5, 6, NicheCatalogue.Headquarters, FoundCatalogue.ChannelSales, 24, null, true);
        TechPointsScene elsewhere = TechPointsMakeScene(5, 6, NicheCatalogue.Restaurants, FoundCatalogue.ChannelSales, 24, null);
        bool unchanged = true;
        foreach (TechPointsScene s in new[] { placed, elsewhere })
        {
            unchanged &= ReferenceEquals(s.View, s.File.Markets[SectorRules.MarketOf(s.Licence)]) && s.Served == 0 && s.NicheAppeal == s.Appeal && s.NicheAware == s.Aware
                         && s.Niche.Selling == s.Whole.Selling && s.NicheTarget == s.WholeTarget;
        }
        Check(unchanged, "tech points niche: a founding that does not count, and a niche that does not buy in the product's market, weigh the product in the whole market as before");
        // competition not begun on the save (the pool fades in from the day it first runs): the shared pool gives the niche the pull it gave before it
        var world = new RivalryWorld { Sector = small.File, Rec = new RivalryFile() };
        TechPointsScene pooled = TechPointsMakeScene(5, 6, NicheCatalogue.Headquarters, FoundCatalogue.ChannelNone, 0, world);
        Check(world.Rec.Since < 0 && pooled.View.Buyers == small.View.Buyers && pooled.View.All == small.View.All && pooled.NicheAppeal == small.NicheAppeal && pooled.NicheAware == small.NicheAware,
            "tech points niche: with competition not begun, the rivals' pool weighs the niche as it did before (" + pooled.View.All + " against " + small.View.All + ")");
    }

    // ---------------------------------------------------------------- the sources

    private static void TechPointsSources()
    {
        string pages = ModSource(@"Tech\Firms\TechApp.Pages.cs"), live = ModSource(@"Ui\Tech\TechLive.Products.cs"), hero = ModSource(@"Ui\Tech\TechLive.FirmPage.cs");
        string words = ModSource(@"Tech\TechText.cs"), desk = ModSource(@"Tech\TechDirector.SectorDesk.cs"), keys = ModSource(@"Ui\UiScreens.Tech.cs"), rules = ModSource(@"Tech\Firms\FirmsRules.cs");
        string founding = ModSource(@"Tech\TechDirector.Founding.cs"), day = ModSource(@"Tech\TechDirector.Firms.cs"), cloud = ModSource(@"Tech\TechDirector.Infra.cs"), views = ModSource(@"Tech\Firms\ProductViews.cs");
        // only the rules name a feature's cost: a page that works out the points to go itself (the cost less the work, in whatever shape) was below zero while a model waited for its training
        var named = new Regex(@"\bFeatureCost\s*\(", RegexOptions.CultureInvariant);
        var rulesOnly = new HashSet<string> { "FirmsRules.cs", "SectorRules.Rivals.cs", "RivalryRules.Reactions.cs", "TechDirector.FoundDesk.cs" };
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts")), stray = "none";
        int scanned = 0, users = 0;
        foreach (string folder in new[] { "Tech", System.IO.Path.Combine("Ui", "Tech") })
        {
            string dir = System.IO.Path.Combine(root, folder);
            if (!Directory.Exists(dir))
            {
                continue;
            }
            foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                scanned++;
                string name = System.IO.Path.GetFileName(file);
                if (named.IsMatch(File.ReadAllText(file)))
                {
                    users++;
                    stray = rulesOnly.Contains(name) ? stray : name;
                }
            }
        }
        Check(scanned > 100 && users == rulesOnly.Count && stray == "none",
            "tech points sources: only the rules name a feature's cost, no page of the tech part works out the points to go itself (" + scanned + " files read, " + users + " name it; "
            + (stray == "none" ? "no page does" : stray + " does") + ")");
        Check(pages.Contains("TechText.NextFeature(p, true)") && pages.Contains("TechText.NextFeature(p, false)") && pages.Contains("ProductViews.TrainingWait(p)") && pages.Contains("TechText.OffSale(p)"),
            "tech points sources: the old product page writes the next feature, a model's wait for training and why a product has no customers through the tech words");
        Check(live.Contains("TechText.NextFeature(p, true)") && live.Contains("TechText.NextFeature(p, false)") && live.Contains("ProductViews.TrainingWait(p)") && Occurs(live, "TechText.OffSale(p)") == 2,
            "tech points sources: the kit's product page does the same, for the tech markets' wording and the firms' own");
        Check(hero.Contains("FirmsRules.Wait(best).Done / 10") && !hero.Contains("best.Progress * 100 / need"), "tech points sources: a firm's page shows a product's progress as the lesser of its work and its training");
        Check(words.Contains("ProductViews.NextFeature(p, markets)") && words.Contains("ProductViews.OffSale(p)") && !words.Contains("reallife_tech_product_build") && !words.Contains("reallife_tech_product_off_sale")
              && !words.Contains("reallife_tech_product_training") && !words.Contains("FirmsRules.Wait("),
            "tech points sources: the tech words write the lines ProductViews chooses and choose none themselves, so the choice is the one the checks run");
        Check(views.Contains("public static ViewLine NextFeature(Product p, bool markets)") && views.Contains("public static ViewLine TrainingWait(Product p)") && views.Contains("public static ViewLine OffSale(Product p)")
              && views.Contains("FirmsRules.Wait(p)"), "tech points sources: the lines are chosen once, from the rule's figures");
        Check(desk.Contains("SectorViews.Market(_sector, p, appeal, aware, fmt, selling, frame)") && desk.Contains("int selling = firm != null ? firm.Crew.Selling : -1;")
              && desk.Contains("NicheFrame(firm, p, md, Save.Day, SectorRules.Attraction(appeal, aware), _nicheShown, ref appeal, ref aware, ref crew, out _)"),
            "tech points sources: the tech markets' section is given the firm's selling points and, for a founded firm's live product, the niche view the sale weighs it in");
        Check(founding.Contains("NicheFrame(firm, p, md, day, pulled, _nicheMd, ref appeal, ref aware, ref crew, out _servedNow)") && founding.Contains("NicheRules.Market(into, f, p, firm.File, md, today,")
              && !founding.Contains("NicheRules.View("), "tech points sources: the sale weighs a founded firm's product in the view NicheRules.Market works out, the one the page reads");
        Check(rules.Contains("public static FeatureWait Wait(Product p)") && rules.Contains("p.Compute >= InfraRules.TrainNeed(p.Features)") && rules.Contains("if (p.Stage >= 2)"),
            "tech points sources: the rule that waits stands beside the build that waits for the same training, and knows a withdrawn product waits for none");
        Check(day.Contains("p.Place != file.Place || p.Stage >= 2") && cloud.Contains("p.K != ProductKind.Model || p.Stage >= 2"),
            "tech points sources: the day's build and the cloud's training still skip a withdrawn product, which is what its page saying it waits for nothing rests on");
        string[] named2 =
        {
            "reallife_tech_product_build_wait", "reallife_tech_mkt_product_build_wait", "reallife_tech_product_training_days", "reallife_tech_product_training_off",
            "reallife_tech_product_off_sale_ready", "reallife_tech_product_off_sale_short", "reallife_tech_mkt_product_deals_full"
        };
        bool all = true;
        string absent = "none";
        foreach (string key in named2)
        {
            all &= keys.Contains("\"" + key + "\"");
            absent = keys.Contains("\"" + key + "\"") ? absent : key;
        }
        Check(all, "tech points sources: the Tech screen names every new word its pages use (" + (all ? "all seven" : absent) + ")");
    }

    // ---------------------------------------------------------------- the texts

    // A text of a language, or nothing when it has none.
    private static string TechPointsSaid(Dictionary<string, string> texts, string key) => texts.TryGetValue(key, out string said) ? said : string.Empty;

    private static void TechPointsTexts()
    {
        Dictionary<string, string> en = LocaleFile("en");
        var wanted = new Dictionary<string, string[]>
        {
            { "reallife_tech_product_build", new[] { "{features}", "{rival}", "{theirs}", "{next}" } },
            { "reallife_tech_mkt_product_build", new[] { "{features}", "{next}" } },
            { "reallife_tech_product_build_wait", new[] { "{features}", "{rival}", "{theirs}", "{ahead}" } },
            { "reallife_tech_mkt_product_build_wait", new[] { "{features}", "{ahead}" } },
            { "reallife_tech_product_training_days", new[] { "{units}", "{days}" } },
            { "reallife_tech_product_training_off", new string[0] },
            { "reallife_tech_product_off_sale_ready", new string[0] },
            { "reallife_tech_product_off_sale_short", new[] { "{launch}", "{features}" } },
            { "reallife_tech_mkt_product_deals_full", new[] { "{target}", "{have}", "{who}" } }
        };
        string missing = "none missing";
        bool holes = true, plain = true;
        foreach (KeyValuePair<string, string[]> pair in wanted)
        {
            if (!en.TryGetValue(pair.Key, out string text) || string.IsNullOrEmpty(text))
            {
                missing = pair.Key;
                continue;
            }
            foreach (string hole in pair.Value)
            {
                holes &= text.Contains(hole);
            }
            plain &= !text.Contains("  ") && !text.Contains("!") && !text.Contains("—") && !text.Contains("–") && !text.Contains(" - ") && !text.Contains("--") && !text.Contains("...");
        }
        Check(missing == "none missing" && holes, "tech points texts: every word is in en.json with the names the code fills (" + missing + ")");
        Check(plain, "tech points texts: no double spaces, exclamation marks, dashes or dots in a row");
        Check(TechPointsSaid(en, "reallife_tech_product_build_wait").Contains("waits for") && TechPointsSaid(en, "reallife_tech_mkt_product_build_wait").Contains("waits for")
              && TechPointsSaid(en, "reallife_tech_product_training_days").Contains("training") && TechPointsSaid(en, "reallife_tech_product_training_off").Contains("off")
              && TechPointsSaid(en, "reallife_tech_product_off_sale_ready").Contains("Launch") && TechPointsSaid(en, "reallife_tech_product_off_sale_short").Contains("no customers"),
            "tech points texts: the waiting feature says it waits, the training says what it is and when it is off, and a product not on sale says why it has no customers");
        // a language that has the texts keeps their names too (one that lacks them reads them in English until translated, and is not held to them)
        bool translated = true;
        foreach (string code in new[] { "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" })
        {
            Dictionary<string, string> texts = LocaleFile(code);
            foreach (KeyValuePair<string, string[]> pair in wanted)
            {
                if (texts.TryGetValue(pair.Key, out string said))
                {
                    foreach (string hole in new[] { "{features}", "{rival}", "{theirs}", "{next}", "{ahead}", "{units}", "{days}", "{launch}", "{target}", "{have}", "{who}" })
                    {
                        translated &= TechPointsSaid(en, pair.Key).Contains(hole) == said.Contains(hole);
                    }
                }
            }
        }
        Check(translated, "tech points texts: a language that has the words keeps the same {names} as the English");
        // What's new tells of both fixes, with their fix marks, in some row
        string[] lines =
        {
            "Fixed: a model at an AI lab no longer shows a negative number of points for its next feature while its engineers wait for the training",
            "Fixed: a product with no customers now says why"
        };
        bool every = true, clean = true;
        foreach (string line in lines)
        {
            every &= WhatsNewHas(en, line);
            foreach (KeyValuePair<string, string> row in en)
            {
                if (row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains(line))
                {
                    clean &= !row.Value.Contains("  ") && !row.Value.Contains("!") && !row.Value.Contains("—") && !row.Value.Contains("–") && Occurs(row.Value, line) == 1;
                }
            }
        }
        Check(every, "tech points texts: What's new tells of both fixes, with their fix marks, in some row of the English");
        Check(clean, "tech points texts: each fix line is said once in its row, with no double spaces, exclamation marks or dashes");
    }
}
