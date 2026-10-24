using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Uptown;

// Uptown's offices as places of work, and the district lived in from the first day (the owner, 24 Oct 2026: "Add more
// existing offices also, so I can work there"): the offices' census and its verdict, the office a test sends the player to,
// the opening pass's count of units past the share to let and its order, the working life employing at every office type
// the game opens there (travel agencies too), the store's mark that the pass has run, and the sources: the pass once a save
// after the leases, never a special building, a lease's or the player's, seeded and put back; the careers' office list with
// no cap; the tests registered and wired; and the texts in every language.
internal static partial class Program
{
    private static void UptownWorkChecks()
    {
        UptownWorkTally();
        UptownWorkPick();
        UptownWorkOpening();
        UptownWorkEmployers();
        UptownWorkStore();
        UptownWorkSources();
        UptownWorkTexts();
    }

    private static OfficeView UptownWorkOffice(string address, string type, bool staffed = true, bool laidOut = true, bool employs = true, float d2 = 100f)
    {
        return new OfficeView
        {
            Address = address, Street = "reallife:street_uptown_summit", Number = address.Length, Type = type, OnOfficeLot = true, Staffed = staffed, LaidOut = laidOut,
            Employs = employs, Distance2 = d2
        };
    }

    private static void UptownWorkTally()
    {
        Check(OfficesRules.Tally(null).Offices == 0 && OfficesRules.Verdict(OfficesRules.Tally(new List<OfficeView>()), 0, 0) != null,
            "uptown work: no office standing is a census of none, and a FAIL");
        var offices = new List<OfficeView>
        {
            UptownWorkOffice("1 Summit Avenue", "ba:businesstype_lawfirm"),
            UptownWorkOffice("3 Summit Avenue", "ba:businesstype_travelagency"),
            UptownWorkOffice("5 Summit Avenue", "ba:businesstype_lawfirm"),
            new OfficeView { Address = "7 Summit Avenue", Type = OfficesRules.EmptyType, ToLet = true },
            new OfficeView { Address = "9 Summit Avenue", Type = "ba:businesstype_webdevelopmentagency", Players = true }
        };
        OfficeTally t = OfficesRules.Tally(offices);
        Check(t.Offices == 5 && t.Businesses == 3 && t.Staffed == 3 && t.LaidOut == 3 && t.Employers == 3 && t.ToLet == 1 && t.Players == 1 && t.OnOfficeLots == 3,
            "uptown work: the census counts the offices, those with a business of the city's (never the player's), staffed, laid out, employing, to let and the player's");
        Check(t.Types == "lawfirm 2, travelagency 1", "uptown work: the census names each type without the game's prefix, with its count, in ordinal order (" + t.Types + ")");
        offices.Reverse();
        Check(OfficesRules.Tally(offices).Types == t.Types && OfficesRules.Tally(offices).Businesses == t.Businesses, "uptown work: the census is the same in any order of the buildings");
        Check(OfficesRules.Verdict(t, 86, 69) == null, "uptown work: three of four offices of the city's with a business, staffed and laid out, against 80 % in the rest of the city: PASS");
        Check(OfficesRules.Verdict(t, 86, 10) != null, "uptown work: a share far from the rest of the city's is a FAIL");
        offices.Add(UptownWorkOffice("11 Summit Avenue", "ba:businesstype_graphicdesigner", staffed: false));
        Check(OfficesRules.Verdict(OfficesRules.Tally(offices), 0, 0) != null, "uptown work: an office business with no staff of the game's is a FAIL");
        var none = new List<OfficeView> { UptownWorkOffice("1 Crest Avenue", "ba:businesstype_lawfirm", employs: false) };
        Check(OfficesRules.Verdict(OfficesRules.Tally(none), 0, 0) != null, "uptown work: offices where the working life employs nobody are a FAIL");
        Check(OfficesRules.Share(0, 0) == 0 && OfficesRules.Share(1, 2) == 50 && OfficesRules.Share(69, 86) == 80 && OfficesRules.Share(2, 3) == 67,
            "uptown work: a share is a whole percentage, halves up, nothing of none");
        Check(OfficesRules.Word("ba:businesstype_lawfirm") == "lawfirm" && OfficesRules.Word("reallife:businesstype_construction") == "reallife:businesstype_construction"
              && OfficesRules.Word(null) == "(none)", "uptown work: a type is named without the game's prefix, a mod's whole");
    }

    private static void UptownWorkPick()
    {
        var offices = new List<OfficeView>
        {
            UptownWorkOffice("b", "ba:businesstype_lawfirm", d2: 400f),
            UptownWorkOffice("a", "ba:businesstype_lawfirm", d2: 400f),
            UptownWorkOffice("c", "ba:businesstype_lawfirm", employs: false, d2: 1f),
            UptownWorkOffice("d", "ba:businesstype_lawfirm", laidOut: false, d2: 1f),
            new OfficeView { Address = "e", Type = "reallife:businesstype_construction", Leased = true, Staffed = true, LaidOut = true, Employs = true, Distance2 = 1f },
            new OfficeView { Address = "f", Type = "ba:businesstype_lawfirm", Players = true, Staffed = true, LaidOut = true, Employs = true, Distance2 = 1f },
            new OfficeView { Address = "g", Type = OfficesRules.EmptyType, ToLet = true, Distance2 = 1f }
        };
        int pick = OfficesRules.Workplace(offices);
        Check(pick >= 0 && offices[pick].Address == "a",
            "uptown work: the job test takes the nearest staffed, laid out office business of the city's the working life employs at, never a builder's, the player's or an empty one; ties by address");
        offices.Reverse();
        pick = OfficesRules.Workplace(offices);
        Check(pick >= 0 && offices[pick].Address == "a", "uptown work: the same office in any order");
        Check(OfficesRules.Workplace(null) == -1 && OfficesRules.Workplace(new List<OfficeView> { offices[0] }) == -1, "uptown work: none to take is -1");
    }

    private static void UptownWorkOpening()
    {
        Check(OfficesRules.ToOpen(0, 0, 8) == 0 && OfficesRules.ToOpen(10, 0, 8) == 0 && OfficesRules.ToOpen(-3, 5, 8) == 0,
            "uptown open: nothing to open where there are no units or none empty");
        // The owner's load of 24 Oct: some 127 shops, 23 empty to let, 8 % kept: 10 kept, 13 opened.
        Check(OfficesRules.ToOpen(127, 23, 8) == 13, "uptown open: shops past the 8 % the district keeps to let are opened, the share kept (" + OfficesRules.ToOpen(127, 23, 8) + ")");
        Check(OfficesRules.ToOpen(87, 14, 18) == 0 && OfficesRules.ToOpen(87, 20, 18) == 4, "uptown open: offices within the 18 % share stay to let; past it, the rest are opened");
        Check(OfficesRules.ToOpen(25, 10, 10) == 8 && OfficesRules.ToOpen(35, 10, 10) == 6, "uptown open: the share kept is rounded as the game rounds it, halves to even (2.5 to 2, 3.5 to 4)");
        Check(OfficesRules.ToOpen(10, 10, 150) == 0 && OfficesRules.ToOpen(10, 10, -5) == 10, "uptown open: a share is kept between none and all");
        Check(OfficesRules.OpenOrder("uptown_2_03_04", "seed") == OfficesRules.OpenOrder("uptown_2_03_04", "seed")
              && OfficesRules.OpenOrder("uptown_2_03_04", "seed") != OfficesRules.OpenOrder("uptown_2_03_04", "other"),
            "uptown open: the units' order is the lot's and the save's seed alone: the same on every machine of a save, another in another save");
        Check(OfficesRules.Pick(5, 0) == -1 && OfficesRules.Pick(7, 3) == 1 && OfficesRules.Pick(uint.MaxValue, 7) == (int)(uint.MaxValue % 7u),
            "uptown open: a pick of none is -1, else the hash's remainder");
    }

    private static void UptownWorkEmployers()
    {
        string[] types = { "ba:businesstype_lawfirm", "ba:businesstype_webdevelopmentagency", "ba:businesstype_graphicdesigner", "ba:businesstype_eventplanningagency",
                           "ba:businesstype_travelagency" };
        string missing = null;
        foreach (string type in types)
        {
            missing = ProCatalogue.AnyEmploys(type) ? missing : missing ?? type;
        }
        Check(missing == null, "uptown work: the working life employs at every office type the game opens in Uptown (" + (missing ?? "all five") + ")");
        Check(ProCatalogue.Employs(ProCatalogue.Find(ProCatalogue.Sales), "ba:businesstype_travelagency"), "uptown work: travel agencies take on sales staff");
    }

    private static void UptownWorkStore()
    {
        var s = new LifeState { Opened = true };
        string line = LifeStore.Write(s);
        LifeState back = LifeStore.Read(line, out int v);
        Check(back != null && back.Opened && v == LifeStore.Version && line.EndsWith("^o:1", StringComparison.Ordinal),
            "uptown open: the save's line says the opening pass has run, and reads it back");
        LifeState old = LifeStore.Read("v=1^f:", out int _);
        Check(old != null && !old.Opened && !LifeStore.Write(old).Contains("^o:"), "uptown open: a line from before the pass reads as not run, and writes as before");
    }

    private static void UptownWorkSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(string file)
        {
            string path = System.IO.Path.Combine(scripts, file);
            return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : string.Empty;
        }
        string director = Read("Uptown/LifeDirector.cs"), opening = Read("Uptown/LifeGame.Opening.cs"), offices = Read("Uptown/LifeDirector.Offices.cs");
        string gameOffices = Read("Uptown/LifeGame.Offices.cs"), guide = Read("Uptown/LifeGuide.cs"), wiring = Read("Wiring/CareersLinks.cs");
        string work = Read("Careers/Work/WorkGame.cs"), tests = Read("Careers/CareersDirector.Tests.cs");

        string firstLife = MemberText(director, "private static bool FirstLife()");
        Check(firstLife.IndexOf("Leases(save, out int renamed)", StringComparison.Ordinal) >= 0
              && firstLife.IndexOf("LifeGame.OpenUnits(Standing, Another, save", StringComparison.Ordinal) > firstLife.IndexOf("Leases(save, out int renamed)", StringComparison.Ordinal)
              && firstLife.Contains("!_state.Opened && !_stateUnread") && firstLife.Contains("_state.Opened = true") && firstLife.Contains("WriteState()"),
            "uptown open: the opening pass runs once a save, after the generator and the leases, and its mark is written with the district's line");
        string another = MemberText(director, "private static bool Another(MadeBuilding made)");
        Check(another.Contains("LeaseOf(made.Spec.Id)") && another.Contains("SpecialBuildings.TakenBy("), "uptown open: the pass leaves a lease's lot and a place the mod holds alone");
        string units = MemberText(opening, "public static int OpenUnits(List<MadeBuilding> standing, Func<MadeBuilding, bool> skip, GameInstance save, out string said)");
        Check(units.Contains("record.SpecialService != null") && units.Contains("reg.RentedByPlayer || reg.BuildingOwnedByPlayer || GoingPublic.Foreign(reg)")
              && units.Contains("CompetitionHelper.GetIdealPercentageOfEmptyBuildings(type, DistrictValues.Id)") && units.Contains("OfficesRules.ToOpen("),
            "uptown open: never a special building, the player's or another player's, and the game's own share to let kept");
        string open = MemberText(opening, "private static bool Open(MadeBuilding made, string seed, GameInstance save)");
        Check(open.Contains("UnityEngine.Random.InitState(") && open.Contains("finally") && open.Contains("UnityEngine.Random.state = was")
              && open.Contains("CompetitionHelper.StartNewCompetitorBusiness(pick.businessTypeName, reg, impactMarket: false, pick, rival)"),
            "uptown open: a unit is opened by the game's own start of a business, its chance seeded and put back, the market not moved");
        string fitting = MemberText(opening, "private static bool Fitting(AiBusinessDefault d, Building record, BuildingSizeInfo size)");
        Check(fitting.Contains("corporationRivalId") && fitting.Contains("allowplayercreation") && fitting.Contains("suitableBuildingType == record.BuildingType")
              && fitting.Contains("GetOrLoadBusinessLayoutSet("), "uptown open: only a business of no corporation whose layout the game has for the unit's structure");
        Check(!opening.Contains("System.Random") && !Regex.IsMatch(opening + offices + gameOffices, @"void (Late|Fixed)?Update\(") && !opening.Contains("Random.Range("),
            "uptown open: no unseeded chance and nothing a frame");
        Check(work.Contains("if (office)\n") && !work.Contains("offices.Count < 64"), "uptown work: every office of the city is a firm's office, Uptown's past the save's first 64 too");
        Check(guide.Contains(".Check(\"reallife_uptown_test_office_census\", LifeDirector.TestOfficeCensus, \"Uptown check (offices)\")")
              && guide.Contains(".Test(\"reallife_uptown_test_office_job\", LifeDirector.TestOfficeJob)"),
            "uptown work: the offices' census is in the test run, the job test a button of its own");
        Check(offices.Contains("Uptown check (offices): ") && offices.Contains("Uptown check (a job at an office): ") && offices.Contains("LifeGame.Destination(made)"),
            "uptown work: each test writes its check line, and the job test sets the destination at the office's door");
        string connect = MemberText(wiring, "internal static void Connect()"), disconnect = MemberText(wiring, "internal static void Disconnect()");
        Check(connect.Contains("Uptown.DistrictWork.Employs = ProCatalogue.AnyEmploys") && connect.Contains("Uptown.DistrictWork.HireAt = ")
              && connect.Contains("Uptown.DistrictWork.Openings = UptownOpenings") && disconnect.Contains("Uptown.DistrictWork.Reset()"),
            "uptown work: the working life's bridges to Uptown's tests are wired, and taken off again");
        string hire = MemberText(tests, "public string TestJobAt(string street, int number)");
        Check(hire.Contains("WorkGame.EmployerAt(street, number)") && hire.Contains("(int)WorkMode.OnSite") && hire.Contains("Begin(a, Save.Day)"),
            "uptown work: the job is at the business at that address, worked on site, begun as a hire is");
        string at = MemberText(work, "public static EmployerRef EmployerAt(string street, int number)");
        Check(at.Contains("reg.RentedByPlayer") && at.Contains("ProCatalogue.AnyEmploys(type)") && at.Contains("GameDlc.Gated(reg)"),
            "uptown work: never a job at the player's own business, nor at one the working life employs nobody at");
    }

    private static void UptownWorkTexts()
    {
        string folder = LocalesFolder();
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json")), pt = LoadWords(System.IO.Path.Combine(folder, "pt.json")), zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        string[] keys =
        {
            "reallife_uptown_test_office_census", "reallife_uptown_test_office_job", "reallife_uptown_test_office_census_done", "reallife_uptown_test_office_job_done",
            "reallife_uptown_test_office_job_none", "reallife_uptown_test_office_job_no_work"
        };
        bool all = en != null && pt != null && zh != null, short240 = true;
        string missing = null;
        foreach (string key in keys)
        {
            bool there = all && en.ContainsKey(key) && pt.ContainsKey(key) && zh.ContainsKey(key) && en[key].Length > 0 && SameHoles(en[key], pt[key], zh[key]);
            missing = there ? missing : missing ?? key;
            all &= there;
            short240 &= en == null || !en.ContainsKey(key) || en[key].Length <= 240;
        }
        Check(all && short240, "uptown work: every new text in en, pt and zh-cn with the same {holes}, every notice at most 240 characters (" + (missing ?? keys.Length + " texts") + ")");
        var langs = new[] { "en", "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr" };
        string lineMissing = null;
        for (int i = 0; i < langs.Length; i++)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(folder, langs[i] + ".json"));
            bool found = words != null && words.TryGetValue("reallife_changes_next", out string next) && next.Contains(UptownWorkMark[i]);
            lineMissing = found ? lineMissing : lineMissing ?? langs[i];
        }
        Check(lineMissing == null && UptownWorkMark.Length == langs.Length, "uptown work: What's new says Uptown's offices are places to work in all nine languages"
                                                                          + (lineMissing != null ? " (not in " + lineMissing + ")" : string.Empty));
    }

    // A few words of the offices' line in each language's What's new, in the order en, pt, zh-cn, de, fr, es, ru, nl, tr.
    private static readonly string[] UptownWorkMark = { "Uptown's offices are places to work", "escritórios do Uptown são lugares para trabalhar", "上城的办公楼可以上班了",
                                                        "Uptowns Büros sind Arbeitsplätze", "bureaux d'Uptown sont des lieux de travail", "oficinas de Uptown son lugares de trabajo",
                                                        "В офисах Uptown можно работать", "kantoren van Uptown zijn plekken om te werken", "Uptown'daki ofisler artık çalışılacak yerler" };
}
