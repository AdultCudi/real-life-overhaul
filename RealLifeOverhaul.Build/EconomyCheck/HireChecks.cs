using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Construction;

// A builder of the city's hired by the player (Scripts/Construction: CommissionRules, ConstructionDirector.Hire, .HireTests,
// ConstructionApp.Hire): the eight quotes and their week, the one bid loop the refit's contractor shares, the contract's money
// to the last unit, the penalty and its cap, the settling up, the builder's pace and the quality the inspector sees, the
// save's new fields read back and passed over; then, over the sources, the client's path through the company's own site,
// inspections and handover, the app without a company, the tests and their lines, and the texts in every language.
internal static partial class Program
{
    private static void Hire()
    {
        HireQuoteChecks();
        HireMoneyChecks();
        HirePaceChecks();
        HireStoreChecks();
        HireSourceChecks();
        HireTextChecks();
    }

    private static void HireQuoteChecks()
    {
        var bids = new TenderBid[BuildCatalogue.RivalCount];
        int count = CommissionRules.Bids(777, CommissionRules.Salt(12, 3), 5000000, 30, Speciality.Homes, null, bids);
        bool each = count == BuildCatalogue.RivalCount;
        for (int r = 0; r < count; r++)
        {
            TenderBid alone = TenderRules.RivalBid(777, CommissionRules.Salt(12, 3), r, 5000000, 30, Speciality.Homes, 0);
            each &= bids[r].Bidder == r && bids[r].Price == alone.Price && bids[r].Days == alone.Days && bids[r].Score == alone.Score && bids[r].Price > 0;
        }
        Check(each, "hire: each of the eight builders quotes, every quote its tender bid (TenderRules.RivalBid), none of them free");
        var again = new TenderBid[BuildCatalogue.RivalCount];
        CommissionRules.Bids(777, CommissionRules.Salt(12, 3), 5000000, 30, Speciality.Homes, null, again);
        bool same = true;
        for (int r = 0; r < count; r++)
        {
            same &= again[r].Price == bids[r].Price && again[r].Days == bids[r].Days;
        }
        Check(same, "hire: the same save, project and week give the same quotes on every look and every machine");
        Check(CommissionRules.Salt(12, 3) != CommissionRules.Salt(12, 4) && CommissionRules.Salt(12, 3) != CommissionRules.Salt(13, 3), "hire: a new week or another project draws its own quotes");
        Check(CommissionRules.StandUntil(1) == 7 && CommissionRules.StandUntil(7) == 7 && CommissionRules.StandUntil(8) == 14 && FirmRules.Week(CommissionRules.StandUntil(20)) == FirmRules.Week(20)
              && FirmRules.Week(CommissionRules.StandUntil(20) + 1) == FirmRules.Week(20) + 1, "hire: the quotes stand to the week's last day");
        var busy = new int[BuildCatalogue.RivalCount];
        busy[2] = 4;
        var busier = new TenderBid[BuildCatalogue.RivalCount];
        CommissionRules.Bids(777, CommissionRules.Salt(12, 3), 5000000, 30, Speciality.Homes, busy, busier);
        Check(busier[2].Price > bids[2].Price && busier[3].Price == bids[3].Price, "hire: a builder with the city's works on its hands asks more, the others as before");
        Check(CommissionRules.Bids(1, 2, 3, 4, Speciality.Homes, null, new TenderBid[3]) == 3 && CommissionRules.Bids(1, 2, 3, 4, Speciality.Homes, null, null) == 0,
            "hire: the bids fill only the caller's array");
        var picks = new[]
        {
            new TenderBid { Bidder = 0, Price = 500, Days = 20 }, new TenderBid { Bidder = 1, Price = 400, Days = 30 }, new TenderBid { Bidder = 2, Price = 450, Days = 15 },
            new TenderBid { Bidder = 3, Price = 600, Days = 15 }
        };
        Check(CommissionRules.Cheapest(picks, 4) == 1 && CommissionRules.Fastest(picks, 4) == 2 && CommissionRules.Cheapest(picks, 0) == -1 && CommissionRules.Fastest(null, 4) == -1,
            "hire: the cheapest and the fastest (the cheaper of two as fast), none of none");
        Check(CommissionRules.NeedOf(ProjectKind.Plot, BuildUse.Homes) == Speciality.Homes && CommissionRules.NeedOf(ProjectKind.Plot, BuildUse.Offices) == Speciality.Offices
              && CommissionRules.NeedOf(ProjectKind.Rebuild, BuildUse.Shops) == Speciality.Renovation, "hire: a block of homes suits a homes builder, offices an offices one, a rebuild a renovator");
        Check(CommissionRules.Value(1000000) == TenderRules.BuildingValue(1000000), "hire: the works are priced off the plans' estimate with a client's margin, as a client's tender is");
        // The refit's contractor quotes through the same loop: its quote is what it was.
        RefitQuote quote = RefitRules.Quote(321, 44, 380, 1000);
        int hours = RefitRules.Hours(380);
        var old = new TenderBid[BuildCatalogue.RivalCount];
        for (int r = 0; r < old.Length; r++)
        {
            old[r] = TenderRules.RivalBid(321, 44 * 31 + 7, r, RefitRules.Value(hours, 1000), TenderRules.StandardDays(hours), Speciality.Renovation, 0);
        }
        TenderBid won = old[TenderRules.Winner(old, old.Length)];
        Check(quote.Rival == won.Bidder && quote.Price == won.Price / 10L * 10L && quote.Days == Math.Max(1, won.Days), "hire: a refit's contractor quotes through the one bid loop, as before");
        Check(CommissionRules.Price(new TenderBid { Price = 123456789 }) == 123456780L && CommissionRules.Price(new TenderBid { Price = 3 }) == 10L, "hire: a contract's price is the bid to ten");
        var timer = System.Diagnostics.Stopwatch.StartNew();
        long sum = 0;
        for (int i = 0; i < 20000; i++)
        {
            CommissionRules.Bids(i, CommissionRules.Salt(i, i / 7), 1000000 + i, 10 + i % 90, (Speciality)(i % 6), null, bids);
            sum += bids[i % BuildCatalogue.RivalCount].Price;
        }
        timer.Stop();
        Check(sum > 0 && timer.Elapsed.TotalMilliseconds < 400.0, "hire: 20000 sets of quotes in " + timer.Elapsed.TotalMilliseconds.ToString("0") + " ms (a press asks for one)");
    }

    private static void HireMoneyChecks()
    {
        bool whole = true;
        foreach (long price in new long[] { 10, 990, 12345, 1000000, 987654321, 25000000000L })
        {
            long paid = CommissionRules.Deposit(price) + CommissionRules.StagePay(price, 0) + CommissionRules.StagePay(price, 1) + CommissionRules.StagePay(price, 2) + CommissionRules.Retention(price);
            whole &= paid == price && CommissionRules.Retention(price) >= 0;
        }
        Check(whole, "hire: the deposit, the three stages and the retention come to the price to the last unit");
        Check(CommissionRules.Deposit(1000000) == 100000 && CommissionRules.StagePay(1000000, 0) == 250000 && CommissionRules.StagePay(1000000, 1) == 300000 && CommissionRules.StagePay(1000000, 2) == 300000
              && CommissionRules.Retention(1000000) == 50000 && CommissionRules.StagePay(1000000, 3) == 0 && CommissionRules.StagePay(1000000, -1) == 0,
            "hire: a tenth down, a quarter, three tenths and three tenths at the inspections, a twentieth kept to the handover");
        Check(CommissionRules.Penalty(1000000, 0) == 0 && CommissionRules.Penalty(1000000, -3) == 0 && CommissionRules.Penalty(1000000, 1) == 5000 && CommissionRules.Penalty(1000000, 4) == 20000
              && CommissionRules.Penalty(1000000, 20) == 100000 && CommissionRules.Penalty(1000000, 1000) == 100000 && CommissionRules.Penalty(0, 5) == 0,
            "hire: a late builder gives back half a per cent a day, a tenth of the price at most, nothing on time");
        long price2 = 1000000;
        long beforeHandover = price2 - CommissionRules.Retention(price2);
        long pay = CommissionRules.Settle(price2, beforeHandover, 0, out long back);
        Check(pay == 50000 && back == 0, "hire: on time the retention is paid at the handover");
        pay = CommissionRules.Settle(price2, beforeHandover, 20000, out back);
        Check(pay == 30000 && back == 0, "hire: a little late, the penalty comes off the retention");
        pay = CommissionRules.Settle(price2, beforeHandover, 100000, out back);
        Check(pay == 0 && back == 50000 && beforeHandover + pay - back == price2 - 100000, "hire: very late, the builder gives back what the retention cannot cover; the client pays the price less the penalty");
        Check(CommissionRules.Settle(price2, price2 + 5, 0, out back) == 0 && back == 0, "hire: nothing is paid twice");
        Check(BuildCatalogue.HireStagePermille.Length == (int)Inspection.Count, "hire: a stage payment for each of the three inspections");
    }

    private static void HirePaceChecks()
    {
        bool same = true, bounded = true, quickerFaster = true, steadier = true;
        int leastLow = int.MaxValue, leastHigh = int.MaxValue;
        for (int seed = 1; seed < 400; seed++)
        {
            int a = CommissionRules.Pace(seed, 9, seed % 8, 20, 20, 60), b = CommissionRules.Pace(seed, 9, seed % 8, 20, 20, 60);
            same &= a == b;
            bounded &= a >= BuildCatalogue.HirePaceLeast && a <= BuildCatalogue.HirePaceMost;
            quickerFaster &= CommissionRules.Pace(seed, 9, seed % 8, 15, 20, 60) > CommissionRules.Pace(seed, 9, seed % 8, 25, 20, 60);
            leastLow = Math.Min(leastLow, CommissionRules.Pace(seed, 9, 2, 20, 20, 0));
            leastHigh = Math.Min(leastHigh, CommissionRules.Pace(seed, 9, 2, 20, 20, 100));
        }
        steadier = leastHigh > leastLow;
        Check(same && bounded, "hire: a builder's pace is the same for the same save, project and builder, and in its bounds");
        Check(quickerFaster, "hire: a builder that quotes fewer days works faster");
        Check(steadier && leastLow >= 850 && leastHigh >= 900, "hire: a builder of standing keeps closer to its quote (the slowest of 400: " + leastLow + " against " + leastHigh + " in 1000)");
        Check(CommissionRules.Pace(1, 1, 1, 1, 1000, 50) == BuildCatalogue.HirePaceMost && CommissionRules.Pace(1, 1, 1, 1000, 1, 50) == BuildCatalogue.HirePaceLeast, "hire: a pace beyond reason is bounded");
        Check(CommissionRules.Quality(0) == 35 && CommissionRules.Quality(100) == 95 && CommissionRules.Quality(50) == 65 && CommissionRules.Quality(200) == 95 && CommissionRules.Quality(-5) == 35,
            "hire: the inspector sees a builder's standing in its work, bounded");
        var hired = new ProjectFile { Kind = ProjectKind.Plot, Contractor = 3 };
        var refit = new ProjectFile { Kind = ProjectKind.Refit, Contractor = 3 };
        var own = new ProjectFile { Kind = ProjectKind.Rebuild };
        Check(hired.Hired && !refit.Hired && !own.Hired && new ProjectFile { Kind = ProjectKind.Rebuild, Contractor = 0 }.Hired,
            "hire: a building of the player's with a builder of the city's is hired; a contractor's refit and the company's own work are not");
    }

    private static void HireStoreChecks()
    {
        var s = new BuildState { Seed = 99, NextId = 30 };
        var p = new ProjectFile
        {
            Id = 21, Kind = ProjectKind.Plot, Stage = ProjectStage.Building, Lot = "uptown_2_04_01", Use = BuildUse.Offices, Area = 2400, Storeys = 6, Hours = 300, Contractor = 6,
            ContractorPrice = 7654320, HireDays = 34, HirePace = 1012, HireStages = 3, HirePaid = 5358020, HirePenalty = 0, HireFree = true, StartDay = 40, DueDay = 78, SiteId = 51
        };
        p.Insp[0] = InspectionState.Passed;
        p.Insp[1] = InspectionState.Passed;
        p.Insp[2] = InspectionState.Booked;
        s.Projects.Add(p);
        s.Projects.Add(new ProjectFile { Id = 22, Kind = ProjectKind.Refit, Stage = ProjectStage.Finished, Storeys = 1, Hours = 40, Contractor = 2, ContractorPrice = 45670, Street = "ba:street_x", Number = 4 });
        string line = BuildStore.Write(s);
        BuildState back = BuildStore.Read(line, out _);
        ProjectFile q = back != null ? back.Project(21) : null;
        Check(back != null && BuildStore.Write(back) == line, "hire store: a line with a hired builder's contract reads back to the same line");
        Check(q != null && q.Hired && q.Contractor == 6 && q.ContractorPrice == 7654320 && q.HireDays == 34 && q.HirePace == 1012 && q.HireStages == 3 && q.HirePaid == 5358020
              && q.HireFree && q.Insp[2] == InspectionState.Booked && back.Project(22) != null && !back.Project(22).Hired && back.Project(22).HirePace == 0,
            "hire store: the contract comes back whole; a contractor's refit is still a refit");
        // A line from before the contract's fields: each record's last six fields gone; it reads as no contract.
        string older = Regex.Replace(line, @"\|34\|1012\|3\|1\|5358020\|0(?=;|\^|$)", string.Empty);
        older = Regex.Replace(older, @"\|0\|0\|0\|0\|0\|0(?=;|\^|$)", string.Empty);
        BuildState old = BuildStore.Read(older, out _);
        Check(older != line && old != null && old.Project(21) != null && old.Project(21).HireDays == 0 && old.Project(21).HirePaid == 0 && old.Project(21).HirePace == 1000
              && old.Project(22) != null && old.Project(22).HirePace == 0, "hire store: a line written before the contract's fields reads, a hired site with no pace going at the city's");
        // A newer line's fields after the contract's are passed over.
        string newer = line.Replace("|5358020|0", "|5358020|0|77|abc");
        BuildState later = BuildStore.Read(newer, out _);
        Check(later != null && later.Project(21) != null && later.Project(21).HirePaid == 5358020 && later.Project(21).HirePenalty == 0, "hire store: a newer line's added fields are passed over");
        Check(!Regex.IsMatch(ModSource(@"Construction\CommissionRules.cs"), @"\bUnityEngine\b|\bRandom\.|DateTime|TickCount|\bTime\.|GetHashCode|new [A-Z][A-Za-z]*\["),
            "hire sources: CommissionRules is game-free, draws only from its seed and allocates nothing");
    }

    private static void HireSourceChecks()
    {
        string sites = ModSource(@"Construction\ConstructionDirector.Sites.cs"), projects = ModSource(@"Construction\ConstructionDirector.Projects.cs");
        string hire = ModSource(@"Construction\ConstructionDirector.Hire.cs"), tests = ModSource(@"Construction\ConstructionDirector.HireTests.cs");
        string app = ModSource(@"Construction\ConstructionApp.Hire.cs"), appProjects = ModSource(@"Construction\ConstructionApp.Projects.cs");
        string guide = ModSource(@"Construction\ConstructionGuide.cs"), refits = ModSource(@"Construction\RefitRules.cs");
        Check(hire.Length > 0 && tests.Length > 0 && app.Length > 0, "hire sources: the director's part, its tests and the app's page are there");
        string workOn = MethodText(sites, "public float WorkOn(");
        string work = MethodText(sites, "private float Work(");
        Check(workOn.Contains("if (p.Contractor >= 0 && !p.Hired)") && workOn.Contains("Work(p, done, needed, hours)") && work.Contains("HiredStage(p, stage);")
              && work.Contains("int pace = p.Hired ? p.HirePace : Pace(p, stage, day);") && work.Contains("p.Hired ? 1.0 : SiteRules.MaterialsShare(p.Have, p.Need)"),
            "hire sources: a hired builder's site goes through the company's own working hour: its stages, its inspections' holds, its rework, at the builder's pace");
        Check(MethodText(sites, "private void InspectionDue(").Contains("HiredBooks(p, inspection);") && MethodText(sites, "private void Passed(").Contains("HireStagePaid(p, inspection);")
              && MethodText(sites, "private void Inspect(").Contains("CommissionRules.Quality(_state.Rivals[p.Contractor].Reputation)"),
            "hire sources: the builder books the inspector, the verdict weighs its standing, and its stage is paid as each inspection passes");
        string handover = MethodText(projects, "private void Handover(");
        Check(handover.Contains("if (p.Contractor >= 0 && !p.Hired)") && handover.Contains("HireSettled(p, day);") && handover.Contains("hired ? p.Contractor : -1")
              && handover.Contains("if (!hired)\r\n            {\r\n                DefectAhead(p, quality, day);") && handover.Contains("HandToDistrict(p);"),
            "hire sources: the handover is the company's own: the retention settled, the builder in the year's buildings, no fault claimed of the company, the building to the district");
        string start = MethodText(hire, "public string HireBuilder(");
        Check(start.Contains("StartSite(p, r, out string why)") && MethodText(projects, "public string StartBuilding(").Contains("StartSite(p, 0, out string why)")
              && start.Contains("ConstructionGame.Take(deposit, \"reallife_transaction_bld_builder\"") && start.Contains("LoanDraw(p, 0);") && start.Contains("FreeProject(p);")
              && start.Contains("if (!GoingPublic.InSession)"), "hire sources: one site starter for the company and a builder; the deposit through the game's money call; nothing sent in a session");
        Check(!start.Contains("Licensed") && !MethodText(hire, "public string HireBlock(").Contains("Founded") && MethodText(hire, "private void HireStagePaid(").Contains("ConstructionGame.Force(pay,")
              && MethodText(hire, "private void HireSettled(").Contains("CommissionRules.Penalty(p.ContractorPrice, late)"), "hire sources: no company or licence is asked; the stages and the penalty are the rules'");
        Check(refits.Contains("CommissionRules.Bids(seed, day * 31 + 7, value, days, Speciality.Renovation, null, bids)"), "hire sources: the refit's contractor quotes through the one bid loop");
        string ready = MethodText(appProjects, "private void WriteReady(");
        Check(ready.Contains("bool hire = ConstructionDirector.HireKind(p);") && ready.Contains("\"reallife_bld_button_hire_builder\"") && !Regex.IsMatch(ready, @"if \(company\)\s*\{\s*[^}]*reallife_bld_button_hire_builder")
              && MethodText(appProjects, "private void WriteProject(").Contains("WriteQuotes(box, p);"), "hire sources: a ready project offers Hire a builder with or without a company, and its quotes page");
        string glance = MethodText(ModSource(@"Construction\ConstructionApp.Overview.cs"), "private void WriteGlance(");
        Check(glance.Contains("\"reallife_bld_glance_client\"") && glance.Contains("ShowTab(ConstructionDirector.TabLand)") && glance.Contains("ShowTab(ConstructionDirector.TabRivals)"),
            "hire sources: the overview without a company says how to build without one and opens the Land and Rivals tabs");
        Check(ModSource(@"Construction\ConstructionApp.Rivals.cs").Contains("_director.HiredBy(r)") && app.Contains("CommissionRules.Cheapest(_shownQuotes, count)") && app.Contains("CommissionRules.Fastest(_shownQuotes, count)"),
            "hire sources: the Rivals tab counts what each builder built for the player; the quotes page picks the cheapest and the fastest");
        // Every test is a public static method, a button and a run's check, writing its line.
        int found = 0;
        foreach (Match m in Regex.Matches(tests, @"public static bool (TestHire[A-Za-z]*Now)\(\)"))
        {
            found++;
            Check(guide.Contains("() => ConstructionDirector." + m.Groups[1].Value + "(), \"Construction check ("), "hire sources: " + m.Groups[1].Value + " is a button of the feature and a check of the run");
        }
        Check(found == 3, "hire sources: three tests (the quotes, a builder start to finish, a late builder): " + found);
        foreach (Match m in Regex.Matches(guide, @"\.Check\(""reallife_bld_test_hire[a-z_]*"", \(\) => ConstructionDirector\.TestHire[A-Za-z]*Now\(\), ""Construction check \(([^)]+)\)"""))
        {
            Check(tests.Contains("\"" + m.Groups[1].Value + "\""), "hire sources: the line \"Construction check (" + m.Groups[1].Value + ")\" is written by its test");
        }
        Check(tests.Contains("private const string HireCheck = \"Construction check (\";") && Regex.Matches(tests, @"\(pass \? "": PASS\."" : "": FAIL\.""\)").Count == 2
              && MethodText(tests, "private bool HireThrough(").Contains("HireBuilder(p, cheapest, true)"), "hire sources: each test ends its line PASS or FAIL; a test's contract moves no money");
        Check(!Regex.IsMatch(hire + tests + app, @"using System\.Linq|\.Where\(|\.Select\(|void Update\(|FindObjectsOfType|FindObjectsByType"), "hire sources: no LINQ, no Update, no scene search");
    }

    private static void HireTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string name in new[] { @"Construction\ConstructionDirector.Hire.cs", @"Construction\ConstructionDirector.HireTests.cs", @"Construction\ConstructionApp.Hire.cs",
                     @"Construction\ConstructionApp.Projects.cs", @"Construction\ConstructionApp.Overview.cs", @"Construction\ConstructionApp.Rivals.cs", @"Construction\ConstructionGuide.cs",
                     @"Construction\ConstructionDirector.Sites.cs", @"Construction\ConstructionDirector.Projects.cs" })
        {
            foreach (Match m in Regex.Matches(ModSource(name), "\"(reallife_[a-z0-9_]+[a-z0-9])\""))
            {
                keys.Add(m.Groups[1].Value);
            }
        }
        keys.Add("reallife_transaction_bld_builder_label");
        Check(keys.Contains("reallife_bld_notice_hire_late") && keys.Contains("reallife_bld_quote_line") && keys.Contains("reallife_bld_test_hire"), "hire texts: their keys are found: " + keys.Count);
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("hire texts", words, codes))
        {
            return;
        }
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            string missing = null;
            foreach (string key in keys)
            {
                if (!words[l].TryGetValue(key, out string value) || value.Length == 0 || value.Contains('!') || value.Contains((char)0x2014) || value.Contains((char)0x2013) || value.Contains(" - "))
                {
                    missing = missing ?? key;
                }
                else if (l > 0 && words[0].TryGetValue(key, out string en) && NamesOf(en) != NamesOf(value))
                {
                    missing = missing ?? key + " (its names in curly brackets)";
                }
            }
            Check(missing == null, "hire texts: every key of a hired builder is in " + codes[l] + ".json, in the style, with the English names (" + (missing ?? "all") + ")");
        }
        foreach (string key in new[] { "reallife_bld_notice_hire_stage", "reallife_bld_notice_hire_failed", "reallife_bld_notice_hire_handed", "reallife_bld_notice_hire_late" })
        {
            string text = words[0].TryGetValue(key, out string t) ? t : string.Empty;
            Check(text.Length > 0 && text.Length + 120 <= 240, "hire texts: " + key + " stays under 240 characters with a long name and builder (" + text.Length + ")");
        }
        string page = words[0].TryGetValue("reallife_guide_construction", out string g) ? g : string.Empty;
        string guide = ModSource(@"Construction\ConstructionGuide.cs");
        Check(page.Contains("# Hiring a builder") && page.Contains("{bld_hire_deposit}") && page.Contains("{bld_hire_penalty_most}") && page.IndexOf("# Hiring a builder", StringComparison.Ordinal) < page.IndexOf("# Money", StringComparison.Ordinal),
            "hire texts: the Guide's construction page has its Hiring a builder section, after Building and before Money");
        foreach (Match m in Regex.Matches(page, @"\{(bld_hire_[a-z_]+)\}"))
        {
            Check(guide.Contains(".Fact(\"" + m.Groups[1].Value + "\""), "hire texts: the Guide's figure {" + m.Groups[1].Value + "} is a fact of the feature list");
        }
        for (int l = 1; l < words.Length && l < 3; l++)
        {
            string p = words[l].TryGetValue("reallife_guide_construction", out string pg) ? pg : string.Empty;
            Check(p.Contains("{bld_hire_deposit}") && p.Contains("{bld_hire_retention}") && p.Contains("{bld_hire_penalty}"), "hire texts: the Guide's page in " + codes[l] + " has the hire section's figures");
        }
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Build without a company:" }, { "pt", "Construa sem empresa:" }, { "zh-cn", "不开公司也能盖楼：" }, { "de", "Bauen ohne eigene Firma:" }, { "fr", "Construire sans entreprise :" },
            { "es", "Construye sin empresa:" }, { "ru", "Стройка без своей компании:" }, { "nl", "Bouwen zonder bedrijf:" }, { "tr", "Şirketsiz inşaat:" }
        };
        for (int l = 0; l < words.Length; l++)
        {
            string next = words[l].TryGetValue("reallife_changes_next", out string row) ? row : string.Empty;
            int line = starts.TryGetValue(codes[l], out string start) ? next.IndexOf(start, StringComparison.Ordinal) : -1;
            int fix = FixMarks.TryGetValue(codes[l], out string mark) ? next.IndexOf(mark, StringComparison.Ordinal) : -1;
            Check(line >= 0 && (fix < 0 || line < fix), "hire texts: What's new's next row in " + codes[l] + " has building without a company, before its fixes");
        }
    }
}
