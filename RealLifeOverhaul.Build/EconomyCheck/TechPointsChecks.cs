using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Tech;

// An AI lab's page showed its next feature as a negative number of points ("-211 points", then "-882", on a lab making 529 points a day), and a
// licensed product with more features than its rival showed no customers and no reason why (players' reports, 8 and 10 Nov 2026). The first is
// the work running past a feature's cost while the model waits for its training: a model's feature is done only when its generation's training is
// in too (FirmsRules.Build, held in InfraChecks), the work goes on building meanwhile, and the pages wrote the cost less the work. FirmsRules.Wait
// now gives the work still to put in, the work past the cost and the training still to come, none below zero, and every page writes them through
// TechText. The second is not a rule gone wrong: a licence launched on better features than its rival's closes a deal in about the days its page
// says, in the firms' own rules and under the tech markets, with nobody selling it and no marketing; what was missing was the saying of it (a product
// not on sale yet, and the markets' licences, whose page had no line for their deals). The rules, the sources and the texts. --tech-points runs these alone.
internal static partial class Program
{
    private static void TechPoints()
    {
        TechPointsLab();
        TechPointsWait();
        TechPointsAgrees();
        TechPointsLicence();
        TechPointsLines();
        TechPointsSources();
        TechPointsTexts();
    }

    // ---------------------------------------------------------------- the report's lab

    private static void TechPointsLab()
    {
        var file = new FirmsFile { Seed = 7 };
        var lab = new Firm { Place = "lab", Kind = (int)FirmKind.AiLab, Focus = (int)Focus.Balanced, Reputation = 500 };
        Product m = FirmsRules.NewProduct(file, "lab", ProductKind.Model, Segment.Business, 0, "Atlas");
        file.Products.Add(m);
        Crew crew = Team(529);   // 529 points a day, as the report's lab made
        int lowest = 0, waiting = 0, mostAhead = 0;
        bool sound = true, same = true;
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
        }
        Check(lowest < -1000 && waiting >= 30 && m.Features >= 2,
            "tech points: the report's lab (529 points a day, 50 units of training a day) has its work far past a feature's cost while it trains: the old count of points to go fell to "
            + lowest + ", and the model waited for its training on " + waiting + " of 40 days (" + m.Features + " features built)");
        Check(sound && same && mostAhead > 1000,
            "tech points: through the same forty days what a feature waits for is never below zero (the work still to put in, the work past its cost, the training to come and its days), "
            + "the old count was exactly the first less the second, and the work past the cost reached " + mostAhead);
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
        Check(m.Train == 2 && w.Points == 0 && w.Ahead == cost * 2 && w.Training == need - 100 && w.Days == (int)((need - 100 + level2 - 1) / level2) && w.Trains && w.Done == 250,
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
    }

    // The page and the build must wait for the same things: a feature is finished by a day with no new work just when the page says it waits for nothing.
    private static void TechPointsAgrees()
    {
        var firm = new Firm { Place = "x", Focus = (int)Focus.Balanced };
        Crew crew = Team(0);
        int cases = 0, disagree = 0;
        string first = string.Empty;
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
        Check(cases == 240 && disagree == 0,
            "tech points: what a page says a feature waits for is what the build waits for, in " + cases + " states of four kinds of product (" + (disagree == 0 ? "all agree" : first) + ")");
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

    private static void TechPointsLicence()
    {
        // the firms' own rules: ahead of its rival by four features, a licence closes a deal when its page says, and goes on closing them; the weakest launch comes a long way after
        int firstBetter = TechPointsFirstDeal(FirmsCatalogue.FeaturesMost, FirmsCatalogue.FeaturesMost - 4, 31, 90, out int promised, out long users, out int appeal);
        int firstWeak = TechPointsFirstDeal(FirmsCatalogue.LaunchFeatures, FirmsCatalogue.FeaturesMost - 4, 31, 90, out _, out _, out _);
        Check(appeal > 1000 && firstBetter > 0 && firstBetter >= promised - 1 && firstBetter <= promised * 5 / 4 + 2 && users >= 5 && (firstWeak < 0 || firstBetter < firstWeak),
            "tech points: a licence with more features than its rival and nobody selling it closes its first deal when its page says (day " + firstBetter + ", the page said " + promised
            + ", appeal " + appeal + "), has " + users + " companies in 90 days, and the weakest launch's first deal comes on day " + firstWeak);
        // the tech markets: the same product with no marketing at all and nobody selling it still earns a share of the companies, and sells
        Product better = TechPointsSold(15, 0, 0, 90, out int sFirst, out SectorFile sf, out long target, out int sPromised);
        TechPointsSold(3, 0, 0, 90, out int wFirst, out _, out _, out _);
        Check(target >= 3 && sFirst > 0 && sFirst >= sPromised - 1 && sFirst <= sPromised * 5 / 4 + 2 && better.Users >= 3 && (wFirst < 0 || sFirst < wFirst),
            "tech points: under the tech markets a licence with fifteen features against its rival's twelve, no marketing and nobody selling it earns a share of " + target
            + " companies and closes its first deal when its page says (day " + sFirst + ", the page said " + sPromised + "), " + better.Users + " in 90 days; the weakest launch's comes on day " + wFirst);
        Check(sf.Markets[(int)SectorMarket.Licences].Buyers > 100, "tech points: the documented city's licence market has some hundreds of companies (" + sf.Markets[(int)SectorMarket.Licences].Buyers + ")");
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
    }

    // ---------------------------------------------------------------- the sources

    private static void TechPointsSources()
    {
        string pages = ModSource(@"Tech\Firms\TechApp.Pages.cs"), live = ModSource(@"Ui\Tech\TechLive.Products.cs"), hero = ModSource(@"Ui\Tech\TechLive.FirmPage.cs");
        string words = ModSource(@"Tech\TechText.cs"), desk = ModSource(@"Tech\TechDirector.SectorDesk.cs"), keys = ModSource(@"Ui\UiScreens.Tech.cs"), rules = ModSource(@"Tech\Firms\FirmsRules.cs");
        // no page counts the points to go as the cost less the work: that was below zero while a model waited for its training
        var counted = new Regex(@"FeatureCost\([^()]*\)\s*-\s*\w+\.Progress", RegexOptions.CultureInvariant);
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts")), at = "none";
        int scanned = 0;
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
                at = counted.IsMatch(File.ReadAllText(file)) ? System.IO.Path.GetFileName(file) : at;
            }
        }
        Check(scanned > 100 && at == "none", "tech points sources: no page of the tech part writes a feature's cost less the work done (" + scanned + " files read; " + (at == "none" ? "none does" : at + " does") + ")");
        Check(pages.Contains("TechText.NextFeature(p, true)") && pages.Contains("TechText.NextFeature(p, false)") && pages.Contains("TechText.TrainingWait(p)") && pages.Contains("TechText.OffSale(p)"),
            "tech points sources: the old product page writes the next feature, a model's wait for training and why a product has no customers through the tech words");
        Check(live.Contains("TechText.NextFeature(p, true)") && live.Contains("TechText.NextFeature(p, false)") && live.Contains("TechText.TrainingWait(p)") && Occurs(live, "TechText.OffSale(p)") == 2,
            "tech points sources: the kit's product page does the same, for the tech markets' wording and the firms' own");
        Check(hero.Contains("FirmsRules.Wait(best).Done / 10") && !hero.Contains("best.Progress * 100 / need"), "tech points sources: a firm's page shows a product's progress as the lesser of its work and its training");
        Check(words.Contains("public static string NextFeature(Product p, bool markets)") && words.Contains("public static string TrainingWait(Product p)") && words.Contains("public static string OffSale(Product p)")
              && words.Contains("FirmsRules.Wait(p)"), "tech points sources: the words are written once, from the rule's three figures");
        Check(desk.Contains("SectorViews.Market(_sector, p, appeal, aware, fmt, firm != null ? firm.Crew.Selling : -1)"), "tech points sources: the tech markets' section is given the firm's selling points");
        Check(rules.Contains("public static FeatureWait Wait(Product p)") && rules.Contains("p.Compute >= InfraRules.TrainNeed(p.Features)"),
            "tech points sources: the rule that waits stands beside the build that waits for the same training");
        string[] named =
        {
            "reallife_tech_product_build_wait", "reallife_tech_mkt_product_build_wait", "reallife_tech_product_training_days", "reallife_tech_product_training_off",
            "reallife_tech_product_off_sale_ready", "reallife_tech_product_off_sale_short", "reallife_tech_mkt_product_deals_full"
        };
        bool all = true;
        string absent = "none";
        foreach (string key in named)
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
