using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Tech;

// Who an AI model is sold to (Tech/Firms/SegmentRules.cs, Tech/TechDirector.Segment.cs, both interfaces' new product and product pages): a new model chooses its segment, a model on
// sale can be re-launched for another at a cost in work, days and customers, with the figures shown first. Each source check is also run on a made-up wrong case, which must fail.
internal static partial class Program
{
    private static void Segments()
    {
        SegmentNew();
        SegmentPreview();
        SegmentApply();
        SegmentWork();
        SegmentSaves();
        SegmentSources();
    }

    private static Product SegmentModel(int segment, long users = 1000, int features = 5)
    {
        var f = new FirmsFile { Seed = 3 };
        Product p = FirmsRules.NewProduct(f, "Dock~1", ProductKind.Model, (Segment)segment, 0, "Atlas");
        p.Stage = 1;
        p.Features = features;
        p.Users = users;
        p.Tier = 1;
        return p;
    }

    private static void SegmentNew()
    {
        bool each = true;
        for (int s = 0; s < (int)Segment.Count; s++)
        {
            Product p = SegmentModel(s);
            each &= p.Segment == s && p.RivalFeatures == FirmsCatalogue.RivalFeaturesAtStart[s] && SectorRules.MarketOf(p) == (int)SectorMarket.ConsumerAi + s;
        }
        Check(each, "segment: a new AI model is made for the segment chosen, meets that segment's rival and sells in that segment's AI market");
        Check(FirmsRules.MonthPrice(SegmentModel(0)) == FirmsCatalogue.Price[0] * FirmsCatalogue.PriceTiers[1] / 1000 && FirmsRules.MonthPrice(SegmentModel(2)) == FirmsCatalogue.Price[2] * FirmsCatalogue.PriceTiers[1] / 1000,
            "segment: its list price is its segment's");
        Check(FirmsRules.NewProduct(new FirmsFile(), "x", ProductKind.Game, Segment.Enterprise, 0, "G").Segment == (int)Segment.Consumer
              && FirmsRules.NewProduct(new FirmsFile(), "x", ProductKind.Licence, Segment.Consumer, 0, "L").Segment == (int)Segment.Enterprise, "segment: a game and a licence keep the segment they are made for");
        Dictionary<string, string> en = LocaleFile("en");
        bool meaning = true;
        foreach (string k in new[] { "reallife_tech_segment_means_consumer", "reallife_tech_segment_means_business", "reallife_tech_segment_means_enterprise" })
        {
            meaning &= en.TryGetValue(k, out string said) && said.Contains("{price}") && said.Contains("{market}") && said.Contains("{churn}") && said.Contains("{tickets}");
        }
        Check(meaning && en["reallife_tech_segment_means_consumer"].Contains("{paying}"), "segment: each segment's words give its list price, its buyers, its churn and its support, and the people's the share who pay");
        // the figures a page prints are the ones the product is run by
        for (int s = 0; s < (int)Segment.Count; s++)
        {
            Console.WriteLine("segment: " + (Segment)s + " lists at " + FirmsCatalogue.Price[s] / 100 + " a month, " + FirmsCatalogue.Market[s] + " buyers, churn " + FirmsCatalogue.Churn[s] + " per mille, "
                              + FirmsCatalogue.Tickets[s] + " tickets a day per thousand");
        }
    }

    private static void SegmentPreview()
    {
        bool never = true, lost = true;
        for (int from = 0; from < (int)Segment.Count; from++)
        {
            for (int to = 0; to < (int)Segment.Count; to++)
            {
                foreach (long users in new long[] { 0, 1, 7, 100, 5000, 123456 })
                {
                    Product p = SegmentModel(from, users);
                    SegmentMove m = SegmentRules.Preview(p, to, 100);
                    never &= m.MonthAfter <= m.MonthNow && (users == 0 || m.MonthAfter * 1000 <= m.MonthNow * (1000 - SegmentRules.Lost) + FirmsCatalogue.Price[to] * 1000);
                    lost &= m.Lost + Math.Max(0L, users) * (1000 - SegmentRules.Lost) / 1000 == users && m.Customers == users && m.Kept >= 0;
                }
            }
        }
        Check(never, "segment: no move ever raises a model's takings, and they fall by at least the share lost (a person pays a few dollars and a company thousands)");
        Check(lost, "segment: the customers who leave and those who stay make up the customers it has");
        Product biz = SegmentModel((int)Segment.Business, 1000);
        SegmentMove toPeople = SegmentRules.Preview(biz, (int)Segment.Consumer, 100), toCompanies = SegmentRules.Preview(biz, (int)Segment.Enterprise, 100);
        Check(toPeople.Lost == 400 && toPeople.Kept == 6000 && toPeople.MonthAfter == toPeople.MonthNow * 6 / 10, "segment: 1,000 businesses moved to people: 400 leave and the 600 who stay are counted as 6,000 people at the people's price, 60% of the takings ("
            + toPeople.Kept + ")");
        Check(toCompanies.Lost == 400 && toCompanies.Kept == 14 && toCompanies.MonthAfter <= toCompanies.MonthNow * 6 / 10, "segment: moved to large companies the 600 who stay are counted at a company's price (" + toCompanies.Kept + " companies)");
        Console.WriteLine("segment: 1,000 businesses to people " + toPeople.Kept + " people " + toPeople.MonthAfter / 100 + " a month against " + toPeople.MonthNow / 100 + "; to companies " + toCompanies.Kept + " companies "
                          + toCompanies.MonthAfter / 100 + " a month");
        SegmentMove at = SegmentRules.Preview(SegmentModel(1, 10, 5), 2, 100);
        Check(at.GenerationCost == FirmsRules.FeatureCost(5) && at.Work == FirmsRules.FeatureCost(5) * SegmentRules.Work / 1000 && at.Days == 14 && at.ResumeDay == 114 && at.Aware == 150 && at.AwareDays == SectorCatalogue.LaunchDays,
            "segment: the work is " + SegmentRules.Work + " thousandths of a generation's (" + at.Work + " of " + at.GenerationCost + " points), the days fourteen, and awareness starts at a launch's 15%");
        Check(SegmentRules.Work == 600 && SegmentRules.Days == 14 && SegmentRules.Lost == 400 && SegmentRules.Aware == SectorCatalogue.LaunchAwareness[1], "segment: the constants are the ones the guide and the texts say");
    }

    private static void SegmentApply()
    {
        Product p = SegmentModel((int)Segment.Business, 1000, 5);
        p.Free = 300;
        p.Carry = 999;
        Check(SegmentRules.Can(p, 100) && SegmentRules.Differs(p, 0) && !SegmentRules.Differs(p, 1) && !SegmentRules.Differs(p, 3) && !SegmentRules.Differs(p, -1), "segment: a model on sale can move to either other segment, never its own");
        Product building = SegmentModel(1, 10);
        building.Stage = 0;
        Check(!SegmentRules.Can(building, 100), "segment: a model still being built keeps the segment it began with");
        Check(!SegmentRules.Can(new Product { Kind = (int)ProductKind.Saas, Stage = 1 }, 100) && !SegmentRules.Can(new Product { Kind = (int)ProductKind.Game, Stage = 1 }, 100) && !SegmentRules.Can(null, 100),
            "segment: a service, a game or nothing cannot be moved");
        var sector = new SectorProduct { Id = p.Id, Known = 700 };
        SegmentMove m = SegmentRules.Apply(p, sector, (int)Segment.Consumer, 100);
        Check(p.Segment == 0 && p.Users == m.Kept && p.Users == 6000 && p.Free == 0 && p.Carry == 0 && p.RivalFeatures == FirmsCatalogue.RivalFeaturesAtStart[0],
            "segment: moved to people, its customers are the kept ones counted at the people's price, its free users and its day's carry are gone, and it meets the people's rival");
        Check(p.Moving == 114 && p.Owe == m.Work && p.Owe == FirmsRules.FeatureCost(5) * 600 / 1000 && SegmentRules.Moving(p, 113) && !SegmentRules.Moving(p, 114) && SegmentRules.Moving(p, 100) && !SegmentRules.Moving(new Product(), 5),
            "segment: it is off sale for fourteen days (to day 114) and owes " + p.Owe + " work points");
        Check(sector.Boost == SegmentRules.Aware && sector.BoostUntil == 114 + SegmentRules.AwareDays && sector.Known == SegmentRules.Aware, "segment: awareness in the new segment starts from 15% for thirty days after it is back on sale, and the page's last figure is held down to it");
        Check(!SegmentRules.Can(p, 101) && !SegmentRules.Can(p, 114) && p.Owe > 0, "segment: a model cannot be moved again while it is off sale or still owes the work");
        p.Owe = 0;
        Check(!SegmentRules.Can(p, 113) && SegmentRules.Can(p, 114), "segment: ...nor until its days are over, and then it can be moved again");
        // a boost already running is not cut by a smaller one
        Product r = SegmentModel(1, 100);
        var running = new SectorProduct { Id = r.Id, Boost = 300, BoostUntil = 150 };
        SegmentRules.Apply(r, running, 2, 100);
        Check(running.Boost == 300 && running.BoostUntil == 150, "segment: a launch's bigger awareness still running is kept, and not cut short by the smaller one");
        // with the tech markets off there is no record: the move still works
        Product s2 = SegmentModel(1, 100);
        Check(SegmentRules.Apply(s2, null, 0, 100).To == 0 && s2.Segment == 0, "segment: with the tech markets off a model still moves, with no awareness to set");
        // the same twice from the same state gives the same (nothing is drawn)
        SegmentMove one = SegmentRules.Preview(SegmentModel(1, 777), 2, 50), two = SegmentRules.Preview(SegmentModel(1, 777), 2, 50);
        Check(one.Kept == two.Kept && one.Lost == two.Lost && one.Work == two.Work && one.MonthAfter == two.MonthAfter, "segment: a move is worked out the same on every machine (nothing is drawn)");
    }

    // The work is paid out of the day's points before any feature, and nothing else of the day changes.
    private static void SegmentWork()
    {
        var firm = new Firm();
        var crew = new Crew();
        Product p = SegmentModel(1, 100, 5);
        p.Owe = 50;
        int progress = p.Progress;
        FirmsRules.Build(firm, p, crew, 30, 0);
        Check(p.Owe == 20 && p.Progress == progress, "segment: a day's 30 points pay the owed work first and add no feature work (" + p.Owe + " left)");
        FirmsRules.Build(firm, p, crew, 30, 0);
        Check(p.Owe == 0 && p.Progress > progress, "segment: the next day pays the last of it, and what is over goes to the next feature");
        Check(SegmentRules.Pay(p, 40) == 40 && SegmentRules.Pay(new Product { Owe = 10 }, 0) == 0 && SegmentRules.Pay(new Product { Owe = 10 }, 4) == 0, "segment: paying with no work owed changes nothing, and a short day pays what it has");
        // the sector's day skips a product off sale; the firms' own day does too
        string sector = ModSource(@"Tech\TechDirector.Sector.cs"), firms = ModSource(@"Tech\TechDirector.Firms.cs");
        Func<string, bool> skips = t => t.Contains("SegmentRules.Moving(p, day)");
        Check(skips(MemberText(sector, "private ProductDay SectorProductDay(")) && skips(firms) && !skips(MemberText(sector, "private ProductDay SectorProductDay(").Replace("SegmentRules.Moving(p, day)", "false")),
            "segment: a model off sale for its re-launch wins, loses and takes nothing, in the markets' day and in the firms' own (and the check fails on a day that does not skip it)");
    }

    private static void SegmentSaves()
    {
        var f = new FirmsFile { Seed = 9 };
        f.Firms.Add(new Firm { Place = "Dock~1", Kind = (int)FirmKind.AiLab });
        Product p = SegmentModel(2, 40, 6);
        p.Place = "Dock~1";
        p.Moving = 214;
        p.Owe = 31;
        f.Products.Add(p);
        FirmsFile back = FirmsStore.Read(FirmsStore.Write(f), out _);
        Product got = back != null && back.Products.Count == 1 ? back.Products[0] : null;
        Check(got != null && got.Moving == 214 && got.Owe == 31 && got.Segment == 2 && got.Users == 40, "segment: a re-launch under way survives the save (off sale until day 214, 31 points owed)");
        // a save made before the re-launch: its product records end at the training level
        string text = FirmsStore.Write(f);
        int part = text.IndexOf("~p:", StringComparison.Ordinal), stop = text.IndexOf('~', part + 3);
        var records = new List<string>(2);
        foreach (string record in (stop < 0 ? text.Substring(part + 3) : text.Substring(part + 3, stop - part - 3)).Split(';'))
        {
            int cut = record.LastIndexOf('|');
            records.Add(record.Substring(0, record.LastIndexOf('|', cut - 1)));   // without the last two figures: the move's day and its work
        }
        FirmsFile older = FirmsStore.Read(text.Substring(0, part + 3) + string.Join(";", records) + (stop < 0 ? string.Empty : text.Substring(stop)), out _);
        Product oldGot = older != null && older.Products.Count == 1 ? older.Products[0] : null;
        Check(oldGot != null && oldGot.Moving == 0 && oldGot.Owe == 0 && oldGot.Train == p.Train && oldGot.Segment == 2, "segment: a save made before the re-launch loads with no move under way and every other figure as it was");
        Check(ModSource(@"Tech\Firms\FirmsRepair.cs").Contains("Moving = p.Moving, Owe = p.Owe"), "segment: a rebuilt record keeps a move under way");
    }

    private static void SegmentSources()
    {
        string old = ModSource(@"Tech\Firms\TechApp.Pages.cs"), kit = ModSource(@"Ui\Tech\TechLive.Products.cs"), dir = ModSource(@"Tech\TechDirector.Segment.cs"), founding = ModSource(@"Tech\TechDirector.Founding.cs");
        // the choice at the start, on both interfaces, where no niche decides
        string oldNew = MemberText(old, "private void WriteNewProduct(");
        string kitNew = MemberText(kit, "private void NewProductPage(");
        Func<string, bool> offered = t => t.Contains("kind == ProductKind.Saas || kind == ProductKind.Model") && t.Contains("TechText.SegmentMeans(");
        Check(offered(oldNew) && offered(kitNew) && !offered(oldNew.Replace("|| kind == ProductKind.Model", string.Empty)), "segment: a new AI model's page offers the choice of segment on the old interface and the kit, with each segment's price and meaning (and the check fails without the model)");
        Check(oldNew.IndexOf("forced >= 0", StringComparison.Ordinal) < oldNew.IndexOf("kind == ProductKind.Saas || kind == ProductKind.Model", StringComparison.Ordinal)
              && kitNew.IndexOf("forced >= 0", StringComparison.Ordinal) < kitNew.IndexOf("kind == ProductKind.Saas || kind == ProductKind.Model", StringComparison.Ordinal)
              && MemberText(ModSource(@"Tech\TechDirector.FirmsApp.cs"), "internal Product NewProduct(").Contains("int forced = NicheSegment(place, kind, out _);"),
            "segment: a founded firm's niche still decides first: where it forces a segment the choice is not offered and the director overrides it");
        // the re-launch, on both
        Check(old.Contains("_director.ChangeSegment(id, to)") && old.Contains("reallife_tech_button_move") && old.Contains("_director.MoveLines(p, to)") && old.Contains("_director.MoveQuestion(p, to, out string title, out string body)")
              && old.IndexOf("_director.MoveLines(p, to)", StringComparison.Ordinal) < old.IndexOf("GameConfirm.Show(title, body", StringComparison.Ordinal),
            "segment: the old product page shows the figures before the game's question box and changes the segment only when it is confirmed");
        Check(kit.Contains("_d.ChangeSegment(id, to)") && kit.Contains("reallife_tech_button_move") && kit.Contains("_d.MoveLines(p, to)") && kit.Contains("Ask(title, body, () => _d.ChangeSegment(id, to))")
              && kit.IndexOf("_d.MoveLines(p, to)", StringComparison.Ordinal) < kit.IndexOf("Ask(title, body", StringComparison.Ordinal),
            "segment: the kit's product page shows the figures before the game's question box and changes the segment only when it is confirmed");
        // the niche's warning is the 1.0.17 line
        string lines = MemberText(dir, "internal List<ViewLine> MoveLines(");
        Check(lines.Contains("reallife_tech_mkt_product_niche_off") && lines.Contains("NicheOff(p, to, out int niche)") && MemberText(dir, "internal bool NicheOff(").Contains("NicheServes(f, SectorRules.MarketOf(p.K, to))")
              && founding.Contains("private static bool NicheServes(FoundedFirm f, Product p) => NicheServes(f, SectorRules.MarketOf(p));"), "segment: a move to a segment the founded firm's niche does not buy in says the niche will not count, by the line a product on sale already has");
        string change = MemberText(dir, "internal bool ChangeSegment(");
        Check(change.Contains("SegmentRules.Can(p, Save.Day)") && change.Contains("SegmentRules.Differs(p, to)") && change.Contains("_sector.ProductFor(p.Id)") && change.Contains("FirmOf(p.Place) == null"),
            "segment: the re-launch is refused for a product that cannot be moved, the same segment or a firm that is gone, and sets awareness through the sector's record");
        Check(!change.Contains("InfraGame.Pay") && !change.Contains("FirmsGame.Pay") && !change.Contains("Network") && !change.Contains("Rpc"), "segment: the re-launch costs no money and sends nothing over the network");
        Check(!ModSource(@"Core\Settings.cs").Contains("TechSegment") , "segment: a purely additive button needs no option");
        Dictionary<string, string> en = LocaleFile("en");
        string[] keys =
        {
            "reallife_tech_new_segment_model", "reallife_tech_segment_means_consumer", "reallife_tech_segment_means_business", "reallife_tech_segment_means_enterprise", "reallife_tech_move_section",
            "reallife_tech_move_about", "reallife_tech_move_label", "reallife_tech_move_to", "reallife_tech_move_work", "reallife_tech_move_days", "reallife_tech_move_customers", "reallife_tech_move_aware",
            "reallife_tech_move_under_way", "reallife_tech_button_move", "reallife_tech_confirm_move_title", "reallife_tech_confirm_move_body", "reallife_tech_state_relaunch", "reallife_tech_note_moved"
        };
        bool all = true;
        foreach (string k in keys)
        {
            all &= en.TryGetValue(k, out string said) && !string.IsNullOrWhiteSpace(said);
        }
        Check(all, "segment: every text of the re-launch is in the English file");
        Check(en.TryGetValue("reallife_tech_note_moved", out string note) && note.Length <= 240 && en["reallife_tech_move_customers"].Contains("{lost}") && en["reallife_tech_move_customers"].Contains("{kept}")
              && en["reallife_tech_move_work"].Contains("{points}") && en["reallife_tech_move_days"].Contains("{days}") && en["reallife_tech_move_aware"].Contains("{aware}"),
            "segment: the lines name the work, the days off sale, the customers who leave and stay, and the awareness it starts from");
        Check(en.TryGetValue("reallife_changes_next", out string next) && next.Contains("AI model can now be made"), "segment: What's new tells of the segment choice and the re-launch");
    }
}
