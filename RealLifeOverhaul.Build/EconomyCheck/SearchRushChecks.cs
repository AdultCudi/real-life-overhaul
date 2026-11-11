using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

// Rushing a search firm's search (Tech/Hiring). A player asked (25 Oct 2026, the suggestions forum) to pay an extra fee, by the days saved, to have the
// people a search firm finds start sooner. The notice a hire works was already bought out by the days saved (BuyoutChecks.cs); what was left was the
// firm's own search, four to eight days for a senior one and two to four for a data centre's crew. Now the player may pay a share of the search's fee for
// each day saved to have its candidates tomorrow, and the price and the new day are asked before anything is paid. Checked here: the price, which
// searches can be rushed, that a rushed search delivers on the day it now says and is never rushed or charged twice, the one place that decides, the
// question each page asks, the option, the money's key, the test and the texts; and that no rival or startup waits on a search firm, so there is no
// search of theirs to rush (the notice a rival buys out is bought by the player's own rule, BuyoutChecks.cs).
internal static partial class Program
{
    private static void SearchRush()
    {
        SearchRushPrice();
        SearchRushRules();
        SearchRushDelivery();
        SearchRushParity();
        SearchRushSources();
        SearchRushOption();
        SearchRushTexts();
    }

    private static void SearchRushPrice()
    {
        Check(HiringCatalogue.SearchRushBp == 500, "search rush: the price is a twentieth of the search's fee for each day saved");
        Check(HiringRules.RushPrice(2800000, 1) == 140000 && HiringRules.RushPrice(2800000, 5) == 700000,
            "search rush: a fee of 28,000.00 costs 1,400.00 for one day saved and 7,000.00 for five");
        Check(HiringRules.RushPrice(0, 5) == 0 && HiringRules.RushPrice(-100, 5) == 0 && HiringRules.RushPrice(2800000, 0) == 0 && HiringRules.RushPrice(2800000, -3) == 0,
            "search rush: no fee or no day saved costs nothing, never less");
        Check(HiringRules.RushPrice(2800000, 6) > HiringRules.RushPrice(2800000, 5) && HiringRules.RushPrice(3000000, 5) > HiringRules.RushPrice(2800000, 5),
            "search rush: more days saved or a bigger fee cost more");
        Check(HiringRules.RushPrice(int.MaxValue, 1000000) == int.MaxValue, "search rush: the largest price is held at the most a sum can be");
        bool even = true;
        for (int days = 1; days <= HiringCatalogue.SearchDaysMost; days++)
        {
            even &= HiringRules.RushPrice(2000000, days) == days * HiringRules.RushPrice(2000000, 1);
        }
        Check(even, "search rush: each day saved costs the same, to the cent");
        int spare = HiringCatalogue.SearchDaysMost - HiringCatalogue.StartLeast;
        Check(HiringRules.RushPrice(2800000, spare) < 2800000 && HiringRules.RushPrice(2800000, spare) * 4 > 2800000,
            "search rush: all of a senior search's spare days cost less than its fee again and more than a quarter of it");
        Check(HiringRules.RushDelivery(10) == 10 + HiringCatalogue.StartLeast && HiringRules.RushDays(16, 11) == 5 && HiringRules.RushDays(11, 11) == 0 && HiringRules.RushDays(5, 11) == 0,
            "search rush: a rushed search delivers tomorrow, and the days saved are those between its day and tomorrow, none when that is not sooner");
    }

    private static void SearchRushRules()
    {
        var search = new JobAd { Id = 3, Place = "Main%20St~1", Role = TalentCatalogue.Cto, Level = TalentCatalogue.Staff, Search = true, Fee = 90000, FeePaid = 30000, From = 10, To = 31, Due = 16 };
        Check(HiringRules.CanRush(search, 10) == HiringRules.Rushable, "search rush: a search that has brought nobody and is due after tomorrow can be rushed");
        int quote = HiringRules.RushQuote(search, 10);
        Check(quote == HiringRules.RushPrice(90000, 5) && quote == 22500, "search rush: the quote is the rule's price for the days saved, day 16 against tomorrow's day 11");
        Check(HiringRules.RushQuote(search, 11) == HiringRules.RushPrice(90000, 4) && HiringRules.RushQuote(search, 11) < quote,
            "search rush: a day later saves a day less and costs less, never more");
        Check(HiringRules.Rush(search, 10) == 5 && search.Due == 11 && search.Fee == 90000 && search.FeePaid == 30000,
            "search rush: rushed, it delivers tomorrow, five days sooner, and its fee and what is paid of it are as they were");
        Check(HiringRules.CanRush(search, 10) == HiringRules.RushTooSoon && HiringRules.RushQuote(search, 10) == 0 && HiringRules.Rush(search, 10) == 0 && search.Due == 11,
            "search rush: never twice, and nothing changes the second time");
        foreach (int due in new[] { 11, 10, 3 })
        {
            var near = new JobAd { Search = true, Fee = 90000, Due = due };
            if (HiringRules.CanRush(near, 10) != HiringRules.RushTooSoon || HiringRules.RushQuote(near, 10) != 0 || HiringRules.Rush(near, 10) != 0 || near.Due != due)
            {
                Check(false, "search rush: a search due tomorrow or sooner has no day to save and is left as it was (due " + due + ")");
            }
        }
        var done = new JobAd { Search = true, Fee = 90000, Due = 20, Came = 3 };
        Check(HiringRules.CanRush(done, 10) == HiringRules.RushDelivered && HiringRules.RushQuote(done, 10) == 0 && HiringRules.Rush(done, 10) == 0 && done.Due == 20,
            "search rush: a search that has brought its candidates is not rushed");
        var ad = new JobAd { Search = false, Fee = 0, Due = 20 };
        Check(HiringRules.CanRush(ad, 10) == HiringRules.RushNotSearch && HiringRules.Rush(ad, 10) == 0 && ad.Due == 20 && HiringRules.CanRush(null, 10) == HiringRules.RushNotSearch
              && HiringRules.RushQuote(null, 10) == 0 && HiringRules.Rush(null, 10) == 0, "search rush: a job ad, or nothing, is not a search to rush");
        var crew = new JobAd
        {
            Id = 4, Place = "Dock%20St~4", Role = TalentCatalogue.Technician, Level = TalentCatalogue.Mid, Search = true, Crew = 4, Fee = 60000, FeePaid = 20000, From = 10, To = 31, Due = 13
        };
        Check(HiringRules.CanRush(crew, 10) == HiringRules.Rushable && HiringRules.RushQuote(crew, 10) == HiringRules.RushPrice(60000, 2) && HiringRules.Rush(crew, 10) == 2 && crew.Due == 11,
            "search rush: a data centre's crew search is rushed by the same rule, on its own fee");
        bool onlyForward = true;
        for (int due = 0; due <= 30; due++)
        {
            for (int day = 5; day <= 12; day++)
            {
                var s = new JobAd { Search = true, Fee = 50000, Due = due };
                int before = s.Due, saved = HiringRules.Rush(s, day);
                onlyForward &= saved >= 0 && s.Due <= before && (saved > 0 ? s.Due == day + 1 && saved == before - s.Due : s.Due == before);
            }
        }
        Check(onlyForward, "search rush: a rush only ever brings a search forward, to tomorrow, by exactly the days it saved (due 0 to 30, from day 5 to 12)");
    }

    private static void SearchRushDelivery()
    {
        HiringFile f = NewLabour(31, 10);
        var search = new JobAd
        {
            Id = f.Next(), Place = "Main%20St~1", Role = TalentCatalogue.Sre, Level = TalentCatalogue.Senior, Search = true, From = 10, To = 31, Due = 17, Fee = 80000, FeePaid = 26640
        };
        f.Ads.Add(search);
        Check(HiringRules.Rush(search, 10) == 6 && !(10 >= search.Due) && 11 >= search.Due,
            "search rush: a rushed search is not due today and is due tomorrow, which is when the day's run (day >= Due) brings its candidates");
        int made = HiringRules.Deliver(f, search, 11, FlatRate, 1000, null);
        Check(made == HiringCatalogue.SearchCandidates && search.Came == made && HiringRules.CanRush(search, 11) == HiringRules.RushDelivered && HiringRules.RushQuote(search, 11) == 0,
            "search rush: the candidates come tomorrow as any search's do, and a search that has delivered cannot be rushed or charged again");
        var crew = new JobAd
        {
            Id = f.Next(), Place = "Dock%20St~4", Role = TalentCatalogue.Technician, Level = TalentCatalogue.Mid, Search = true, Crew = 4, From = 10, To = 31, Due = 14, Fee = 60000
        };
        f.Ads.Add(crew);
        Check(HiringRules.Rush(crew, 10) == 3 && crew.Due == 11 && HiringRules.DeliverCrew(f, crew, 11, FlatRate, 1000, null) == HiringRules.CrewCandidates(4) && crew.Came > 0
              && HiringRules.CanRush(crew, 11) == HiringRules.RushDelivered, "search rush: a crew search rushed brings its candidates tomorrow by its own rule, and is not rushed again");
        Seeker one = null;
        for (int i = 0; i < f.Applicants.Count && one == null; i++)
        {
            Seeker s = f.SeekerOf(f.Applicants[i].Seeker);
            one = s != null && f.Applicants[i].Ad == search.Id && s.Employed ? s : null;
        }
        Check(one != null && one.Passive && one.Notice == HiringRules.NoticeFor(one.P.Level),
            "search rush: a rushed search's candidate still works the notice of their level, which the notice buy out shortens by the days saved (BuyoutChecks.cs)");
    }

    // No rival or startup briefs a search firm: the rivals' ads are their own list and fill by chance from the open market, and a startup's hire takes
    // from the open market too, so no rival waits on a search for there to be a rush to buy. Their one wait, the notice of a person they poach, is bought
    // out by the player's own rule (BuyoutChecks.cs, "rival buy out").
    private static void SearchRushParity()
    {
        HiringFile f = NewLabour(41, 5, 0);
        bool none = true;
        for (int day = 6; day < 120; day++)
        {
            HiringRules.Day(f, day, day % 4, false, HiringCatalogue.RivalFirms, FlatRate, new[] { "d1", "d2" }, null);
            none &= f.Ads.Count == 0;
        }
        Check(none && f.RivalAds.Count > 0, "search rush parity: in 114 days of the market the rivals post their own ads and brief no search firm, so there is no search of theirs to rush");
        string offers = ModSource(@"Tech\Hiring\HireOffers.cs");
        string exits = ModSource(@"Tech\Venture\ExitPlaces.cs");
        Check(offers.Contains("a.RivalBid == 0 && a.Live && a.Stage < (int)ApplyStage.Offered && !s.Passive") && exits.Contains("if (c.Taken || c.Passive || Held(h, c.Id))"),
            "search rush parity: a rival's offer and a startup's hire never reach a search firm's candidate, who is the player's alone");
        string people = ModSource(@"Tech\TechDirector.People.cs");
        Check(MethodText(people, "private bool RivalBuyout(Person person, int day)").Contains("HiringRules.BuyoutPrice(") && MethodText(ModSource(@"Tech\TechDirector.Hiring.cs"),
            "internal bool BuyOutNotice(int id)").Contains("HiringRules.BuyoutQuote("),
            "search rush parity: what a rival does buy out, a poached person's notice, is priced by the one rule the player's notice buy out uses");
    }

    private static void SearchRushSources()
    {
        string hiring = ModSource(@"Tech\TechDirector.Hiring.cs");
        string page = ModSource(@"Tech\People\RolesApp.Hiring.cs");
        string crewPage = ModSource(@"Tech\People\RolesApp.Crew.cs");
        string live = ModSource(@"Ui\Work\RolesLive.People.cs");
        string liveHead = ModSource(@"Ui\Work\RolesLive.cs");
        string game = ModSource(@"Tech\Hiring\HiringGame.cs");
        string lines = ModSource(@"Economy\BizLines.cs");
        string tests = ModSource(@"Tech\TechDirector.Tests.cs");
        string guide = ModSource(@"Tech\TechGuide.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string list = ModSource(@"Meta\FeatureList.Economy.cs");
        string work = ModSource(@"Ui\UiScreens.Work.cs");
        string sample = System.IO.File.ReadAllText(System.IO.Path.Combine(FindRoot(), "RealLifeOverhaul.Build/UiPreview/UiSamples.WorkRoles.cs"));
        string rush = MethodText(hiring, "internal bool RushSearch(int id)");
        Check(rush.Contains("string refusal = RushRefusal(ad);") && rush.Contains("HiringRules.RushQuote(ad, day)") && rush.Contains("HiringGame.Pay(price, HiringGame.RushKey, at, role)")
              && rush.IndexOf("HiringGame.Pay(", StringComparison.Ordinal) < rush.IndexOf("HiringRules.Rush(ad, day);", StringComparison.Ordinal)
              && rush.Contains("\"reallife_hire_rush_short\"") && rush.Contains("\"reallife_hire_rush_gone\"") && rush.Contains("\"reallife_hire_note_rush\"") && rush.Contains("RushCheck("),
            "search rush sources: refused with the reason, priced afresh, paid whole before the day moves, told and checked");
        Check(rush.Contains("ad.CrewSearch ? (HiringGame.Headquarters() ?? centre) : centre"),
            "search rush sources: a senior search is paid by the business it is for, a crew search by headquarters as its retainer was, and by the centre if there is none now");
        string refuse = MethodText(hiring, "internal string RushRefusal(JobAd ad)");
        Check(refuse.Contains("!_hiringOn") && refuse.Contains("!Settings.TechSearchRush") && refuse.Contains("\"reallife_hire_rush_off\"") && refuse.Contains("HiringRules.CanRush(ad, Save.Day)")
              && refuse.Contains("\"reallife_hire_rush_soon\"") && refuse.Contains("\"reallife_hire_rush_delivered\""),
            "search rush sources: the option or the hiring off, candidates already in, or none due after tomorrow, each refused with its own words");
        string check = MethodText(hiring, "private void RushCheck(string role, int day, int was, int now, int wanted, int price, int fee)");
        Check(check.Contains("if (!Verbose)") && check.Contains("Tech check (search rush, ") && check.Contains("(ok ? \"PASS\" : \"FAIL\")") && check.Contains("HiringRules.RushPrice(fee, saved)"),
            "search rush sources: one self-check line a rush under Developer diagnostics, the price against the rule's");
        Check(Occurs(hiring + page + crewPage + live, "HiringRules.Rush(") == 1, "search rush sources: one place rushes a search, the director's, never a page");
        Check(page.Contains("_director.RushRefusal(ad)") && page.Contains("GameConfirm.Show(") && page.Contains("\"reallife_hire_rush_body\"") && page.Contains("_director.RushSearch(id)")
              && page.Contains("WriteRush(box, picked, day);") && crewPage.Contains("WriteRush(box, ad, day);") && !page.Contains("HudConfirm"),
            "search rush sources: a search's page and a crew search's ask through the game's question box with the price and the new day, as a notice buy out does");
        Check(page.Contains("if (Settings.TechSearchRush)") && page.Contains("\"reallife_hire_rush_about\""),
            "search rush sources: the Search tab and the new search's page say what a rush is while the option is on");
        Check(live.Contains("case Rush:") && live.Contains("d.RushSearch(id);") && live.Contains("GameConfirm.Show(\"reallife_hire_rush_title\".Localize(new { role })")
              && live.Contains("private static WorkFormBand RushBand(") && live.Contains("if (!Settings.TechSearchRush)") && liveHead.Contains("Rush = 117") && Occurs(liveHead, "= 117") == 1,
            "search rush sources: the kit's Search tab lists the searches that can be rushed with the price on each button, asks before it pays, and its action number is its own");
        Check(live.Contains("SearchHasLive(h, h.Ads[i])") && !live.Contains("ad.Search != false"),
            "search rush sources: the kit lists a search firm's candidates among the applicants, so a hire from a search can be made and its notice bought out there");
        Check(game.Contains("public const string RushKey = \"reallife_transaction_tech_recruitment_rush\";") && lines.Contains("P + \"tech_recruitment_rush\"")
              && RealLifeOverhaul.Economy.BizLines.Of("reallife_transaction_tech_recruitment_rush") == RealLifeOverhaul.Economy.BizLine.Cost,
            "search rush sources: the money has a key of its own, a recruiting cost in BizMan");
        Check(tests.Contains("public void TestSearchRush()") && tests.Contains("Tech check (search rush test, ") && tests.Contains("\"reallife_hire_rush_off\"")
              && mod.Contains("\"reallife_hire_test_rush\", () => Tech.TechDirector.Instance?.TestSearchRush()") && list.Contains("\"reallife_hire_test_rush\""),
            "search rush sources: the test is in Options, on the hiring feature's list, and says why when it cannot run");
        Check(guide.Contains(".Fact(\"hire_rush\", () => Bp(HiringCatalogue.SearchRushBp))"), "search rush sources: the Guide's share comes from the catalogue");
        Check(work.Contains("\"reallife_work_rush_for\"") && work.Contains("\"reallife_work_rush_title\"") && work.Contains("\"reallife_hire_rush_about\"")
              && sample.Contains("\"reallife_work_rush_title\"") && sample.Contains("\"reallife_work_rush_for\""),
            "search rush sources: the kit's Search screen names its new texts and its sample lays the same band out");
    }

    private static void SearchRushOption()
    {
        string settings = ModSource(@"Core\Settings.cs");
        string mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string tree = ModSource(@"Meta\OptionsTree.cs");
        string list = ModSource(@"Meta\FeatureList.Economy.cs");
        Check(settings.Contains("public const string TechSearchRushId = \"tech_search_rush\";") && settings.Contains("public static bool TechSearchRush = true;")
              && settings.Contains("TechSearchRush = Bool(modId, TechSearchRushId, TechSearchRush);"),
            "search rush option: it is a saved option, on unless the player turns it off");
        Check(mod.Contains("AddToggle(Settings.TechSearchRushId, \"reallife_hire_rush_enabled\", Settings.TechSearchRush,"), "search rush option: it is a switch on the options page with its label");
        Check(tree.Contains(".Opt(\"tech_search_rush\", \"tech_jobs\")") && list.Contains(".Owns(Settings.TechJobsId, Settings.TechSearchRushId)"),
            "search rush option: it stands in the jobs board's section, needs the jobs board, and is the hiring feature's");
        string docs = System.IO.Path.Combine(FindRoot(), "docs/features");
        string readme = System.IO.File.Exists(System.IO.Path.Combine(docs, "README.md")) ? System.IO.File.ReadAllText(System.IO.Path.Combine(docs, "README.md")) : string.Empty;
        string text = System.IO.File.Exists(System.IO.Path.Combine(docs, "tech-hiring.md")) ? System.IO.File.ReadAllText(System.IO.Path.Combine(docs, "tech-hiring.md")) : string.Empty;
        Check(text.Contains("SearchRushBp") && text.Contains("TechDirector.RushSearch") && text.Contains("tech_search_rush") && readme.Contains("(tech-hiring.md)"),
            "search rush option: its page is under docs/features and the list there names it");
    }

    private static void SearchRushTexts()
    {
        string[] keys =
        {
            "reallife_hire_rush_enabled", "reallife_opt_tip_tech_search_rush", "reallife_hire_button_rush", "reallife_hire_rush_line", "reallife_hire_rush_title", "reallife_hire_rush_body",
            "reallife_hire_note_rush", "reallife_hire_rush_short", "reallife_hire_rush_delivered", "reallife_hire_rush_soon", "reallife_hire_rush_off", "reallife_hire_rush_gone",
            "reallife_hire_rush_about", "reallife_work_rush_title", "reallife_work_rush_for", "reallife_transaction_tech_recruitment_rush",
            "reallife_transaction_tech_recruitment_rush_label", "reallife_hire_test_rush", "reallife_hire_test_rush_ready"
        };
        Check(AllIn(keys, out string missing), "search rush texts: every key in en, pt and zh-cn (" + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        bool fits = true;
        foreach (string key in new[] { "reallife_hire_note_rush", "reallife_hire_rush_short", "reallife_hire_rush_delivered", "reallife_hire_rush_soon", "reallife_hire_rush_off",
                     "reallife_hire_rush_gone", "reallife_hire_test_rush_ready" })
        {
            fits &= Fits(en, key);
        }
        Check(fits, "search rush texts: the notices are 240 characters or fewer");
        Check(en.TryGetValue("reallife_hire_rush_line", out string line) && line.Contains("{day}") && line.Contains("{was}") && line.Contains("{amount}") && line.Contains("{share}")
              && en.TryGetValue("reallife_hire_rush_body", out string body) && body.Contains("{amount}") && body.Contains("{day}") && body.Contains("{was}") && body.Contains("{share}"),
            "search rush texts: the page's line and the question give the price, the share, the day it comes and the day it was to");
        Check(en.TryGetValue("reallife_guide_tech_jobs", out string page) && page.Contains("can be rushed, a senior one or a data centre's crew") && page.Contains("a rush brings its candidates to tomorrow")
              && page.Contains("{hire_rush}") && page.Contains("The price and the new day are asked before you pay"),
            "search rush texts: the Guide's page on hiring tells of the rush for both kinds of search, its share and that the price is asked first");
        Check(WhatsNewHas(en, "A search firm's search can now be rushed from its page in Open Roles"), "search rush texts: What's new tells of it (any of its rows)");
    }
}
