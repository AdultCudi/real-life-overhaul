using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Tech;

// Rushing a search firm's search (Tech/Hiring). A player asked (25 Oct 2026, the suggestions forum) to pay an extra fee, by the days saved, to have the
// people a search firm finds start sooner. The notice a hire works was already bought out by the days saved (BuyoutChecks.cs); what was left was the
// firm's own search, four to eight days for a senior one and two to four for a data centre's crew. Now the player may pay a share of the search's fee for
// each day saved to have its candidates tomorrow, and the price and the new day are asked before anything is paid. Checked here: the price, which
// searches can be rushed, that a rushed search delivers on the day it now says and is never rushed or charged twice, the one place that decides, the
// question each page asks, the option, the money's key, the test and the texts; and that no rival or startup waits on a search firm, so there is no
// search of theirs to rush (the notice a rival buys out is bought by the player's own rule, BuyoutChecks.cs). The texts are also held, in all eleven
// languages, to the names the screen has: the Guide's page bolds Open Roles' tabs, the hire button and the Infra tab as the language's own texts of them,
// and the tip, the test's notice and What's new's sentence name the app and its Search tab so (SearchRushLabels).
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
        SearchRushLabels();
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

    // The texts of the screen that the Guide's page on hiring bolds by name: Open Roles' Jobs, Applicants and Search tabs, the Hire technicians button on a data centre's page and the Tech app's Infra tab.
    private static readonly string[] RushPageLabels =
    {
        "reallife_roles_tab_jobs", "reallife_roles_tab_applicants", "reallife_roles_tab_search", "reallife_infra_button_hire", "reallife_tech_tab_infra"
    };

    // The word for a tab in each language, as the stem its forms begin with (Polish and Russian bend it), and whether it stands after a tab's name ("the Search tab") or before it ("Reiter Suche").
    private static readonly Dictionary<string, (bool After, string[] Stems)> RushTabWords = new Dictionary<string, (bool After, string[] Stems)>(StringComparer.Ordinal)
    {
        { "en", (true, new[] { "tab" }) }, { "pt", (false, new[] { "aba" }) }, { "zh-cn", (true, new[] { "标签" }) }, { "de", (false, new[] { "Reiter" }) }, { "fr", (false, new[] { "onglet" }) },
        { "es", (false, new[] { "pestaña" }) }, { "ru", (false, new[] { "вкладк" }) }, { "nl", (false, new[] { "tabblad" }) }, { "tr", (true, new[] { "sekme" }) },
        { "pl", (false, new[] { "zakład", "kart", "karc" }) }, { "ko", (true, new[] { "탭" }) }
    };

    // A piece of What's new's sentence on the rush in each language, the words that send the reader to the app: the check holds each piece to the app's own name, and the row to the piece.
    private static readonly Dictionary<string, string> RushNewsPieces = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "en", "from its page in Open Roles" }, { "pt", "pela página dela no app Cargos abertos" }, { "zh-cn", "在“职位空缺”中该搜寻的页面上加急" }, { "de", "auf ihrer Seite unter Offene Stellen" },
        { "fr", "depuis sa page dans Postes ouverts" }, { "es", "desde su página en Puestos vacantes" }, { "ru", "с её страницы в приложении «Вакансии»" }, { "nl", "vanaf de pagina in Vacatures" },
        { "tr", "Açık Pozisyonlar içindeki sayfasından" }, { "pl", "z jej strony w aplikacji Wolne stanowiska" }, { "ko", "채용 공고의 해당 페이지에서" }
    };

    // The names the rush's texts send the reader to. Open Roles, its tabs, the Tech app's Infra tab and the button on a data centre's page are on the screen under the language's own words
    // (RolesLive's title and tabs, TechLive's tab and button), and a reader looks for what a page names. So the Guide's page on hiring bolds each of them as the language's own text of it, and
    // the option's tip, the test's notice and What's new's sentence name the app and its Search tab as the screen does. A translator's own word for one (Bolsa for the Jobs tab in Spanish,
    // Infra where Spanish says Infraestructura, Poszukiwania for Poszukiwanie in Polish, an app called Puestos abiertos where it is Puestos vacantes) sends the reader to nothing, and no other
    // check reads a Guide page's bold names in any language but English (StandardCheck's RLO-TXT-013).
    private static void SearchRushLabels()
    {
        string[] pageEn = LocaleFile("en").TryGetValue("reallife_guide_tech_jobs", out string englishPage) ? englishPage.Split('\n') : new string[0];
        foreach (string code in RealLifeOverhaul.ModLanguages.All)
        {
            Dictionary<string, string> words = LocaleFile(code);
            var labels = new List<string>();
            foreach (string key in RushPageLabels)
            {
                labels.Add(words.TryGetValue(key, out string label) ? label : string.Empty);
            }
            string app = words.TryGetValue("reallife_roles_app", out string appName) ? appName : string.Empty;
            string tab = words.TryGetValue("reallife_roles_tab_search", out string tabName) ? tabName : string.Empty;
            Check(app.Length > 0 && !labels.Contains(string.Empty), "search rush labels: " + code + " has the texts of Open Roles, its tabs, the hire button and the Infra tab");
            // The Guide's page: its bold names are those five texts, each of them, and no other name; and it keeps the English page's lines, heads, points and figures.
            string page = words.TryGetValue("reallife_guide_tech_jobs", out string pageText) ? pageText : string.Empty;
            List<string> bold = Regex.Matches(page, "<b>(.*?)</b>").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            // The page may also name the option that seats tech hires, by its own text in the language (the options page shows it so).
            string seatOption = words.TryGetValue("reallife_hire_opt_seat", out string seatText) ? seatText : null;
            string odd = string.Join(", ", bold.Where(b => !labels.Contains(b) && b != seatOption).Distinct());
            string absent = string.Join(", ", labels.Where(l => !bold.Contains(l)));
            Check(odd.Length == 0 && absent.Length == 0,
                "search rush labels: the Guide's page on hiring names the Jobs, Applicants and Search tabs, the Hire technicians button and the Infra tab in bold as " + code + " has them, and no other name (not a label: "
                + odd + "; not named: " + absent + ")");
            string[] lines = page.Split('\n');
            bool shaped = pageEn.Length > 0 && lines.Length == pageEn.Length;
            for (int i = 0; shaped && i < lines.Length; i++)
            {
                shaped &= string.CompareOrdinal(lines[i], 0, pageEn[i], 0, 2) == 0 && RushFields(lines[i]) == RushFields(pageEn[i]);
            }
            Check(shaped, "search rush labels: the Guide's page on hiring has the English page's lines, heads, points and figures in " + code + " (" + lines.Length + " lines)");
            // The page's line on the rush names the Search tab in bold and the app by its own name.
            string rush = lines.FirstOrDefault(l => l.Contains("{hire_rush}")) ?? string.Empty;
            Check(rush.Contains("<b>" + tab + "</b>") && NamesRushLabel(rush, app, code, false),
                "search rush labels: the Guide's line on the rush names the Search tab in bold and Open Roles as " + code + " has them (" + tab + ", " + app + ")");
            // The option's tip, the test's notice and What's new's sentence.
            string tip = words.TryGetValue("reallife_opt_tip_tech_search_rush", out string tipText) ? tipText : string.Empty;
            string ready = words.TryGetValue("reallife_hire_test_rush_ready", out string readyText) ? readyText : string.Empty;
            Check(NamesRushLabel(tip, app, code, false) && NamesRushLabel(tip, tab, code, true),
                "search rush labels: the option's tip names Open Roles and its Search tab as " + code + " has them (" + app + ", " + tab + ")");
            Check(NamesRushLabel(ready, tab, code, true), "search rush labels: the test's notice names the Search tab as " + code + " has it (" + tab + ")");
            Check(RushNewsPieces.TryGetValue(code, out string piece) && NamesRushLabel(piece, app, code, false) && WhatsNewHas(words, piece),
                "search rush labels: What's new's sentence on the rush names Open Roles as " + code + " has it (" + app + "), in any of its rows");
        }
        // The finder, on texts of its own so that it can fail: a label is named only as itself (Turkish Ara is no part of Arama), a tab only beside the word for a tab (a Polish sentence that opens
        // with the noun Poszukiwanie, or a Korean one that has 헤드헌팅 회사, names no tab), and another name for the app is no name for it.
        Check(NamesRushLabel("Açık Pozisyonlar uygulamasının Ara sekmesinden", "Ara", "tr", true) && !NamesRushLabel("Açık Pozisyonlar ekranının Arama sekmesinden", "Ara", "tr", true)
              && NamesRushLabel("Przyspieszenie kosztuje 5, z jego strony w zakładce Poszukiwanie.", "Poszukiwanie", "pl", true)
              && !NamesRushLabel("Poszukiwanie roli przyniesie kandydatów. Kosztuje 5, w zakładce Poszukiwania.", "Poszukiwanie", "pl", true)
              && NamesRushLabel("헤드헌팅 탭에서, 아낀 날마다 수수료의 일부를 내고", "헤드헌팅", "ko", true) && !NamesRushLabel("검색 탭에서 헤드헌팅 회사 수수료의 일부를 내고", "헤드헌팅", "ko", true)
              && NamesRushLabel("在“职位空缺”的猎头标签页中", "猎头", "zh-cn", true) && !NamesRushLabel("在空缺职位的搜寻标签页中，猎头公司", "猎头", "zh-cn", true)
              && NamesRushLabel("Desde la pestaña Búsqueda de Puestos vacantes, pagas", "Búsqueda", "es", true) && NamesRushLabel("Desde la pestaña Búsqueda de Puestos vacantes, pagas", "Puestos vacantes", "es", false)
              && !NamesRushLabel("Desde la pestaña Búsqueda de Puestos abiertos, pagas", "Puestos vacantes", "es", false) && !NamesRushLabel("Desde la pestaña Búsqueda de Puestos abiertos, pagas", "Puestos vacantes", "es", true)
              && NamesRushLabel("From the Search tab of Open Roles, pay", "Search", "en", true) && !NamesRushLabel("From the Search page of Open Roles, pay", "Search", "en", true),
            "search rush labels: a text names a label only as the label itself, a tab only beside the word for a tab, and not by another name");
    }

    // Whether a text names a label as its language writes it: the label as a word of its own (it is not the start of a longer word), and, for a tab, with the word for a tab within sixteen characters
    // of it on the side the language puts it. Korean and Chinese write no spaces and join their particles to a name, so there the label is looked for as it stands.
    private static bool NamesRushLabel(string text, string label, string code, bool asTab)
    {
        if (label.Length == 0)
        {
            return false;
        }
        bool spaced = code != "ko" && code != "zh-cn";
        for (int at = text.IndexOf(label, StringComparison.Ordinal); at >= 0; at = text.IndexOf(label, at + 1, StringComparison.Ordinal))
        {
            int end = at + label.Length;
            if (spaced && ((at > 0 && char.IsLetter(text[at - 1])) || (end < text.Length && char.IsLetter(text[end]))))
            {
                continue;
            }
            if (!asTab)
            {
                return true;
            }
            (bool after, string[] stems) = RushTabWords[code];
            int lo = Math.Max(0, at - 16);
            string near = after ? text.Substring(end, Math.Min(16, text.Length - end)) : text.Substring(lo, at - lo);
            foreach (string stem in stems)
            {
                if (near.IndexOf(stem, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
        }
        return false;
    }

    // The {figures} of a Guide line, by name, as one string.
    private static string RushFields(string line)
    {
        return string.Join(" ", Regex.Matches(line, @"\{[a-z0-9_]+\}").Cast<Match>().Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal));
    }
}
