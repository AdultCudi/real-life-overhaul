using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Construction;
using RealLifeOverhaul.Uptown;

// The front desks (Scripts/Construction: FrontDeskRules, BuilderDesk, BuilderDeskGame, BuilderDeskPointer,
// ConstructionDirector.Desk and .DeskTests; Scripts/Uptown: who an office receives): what the desk talks about first for
// every way the player's building can stand, the front desk picked by the door, the office manager's counts, a leased
// office never receiving the player in a unit anybody rents or owns; then, over the sources, the one quote and the one hire
// the app has, the game's own click and panel, the time held as the game's desk staff hold it and never in a session,
// nothing per frame, the tests and their lines, and the texts in every language.
internal static partial class Program
{
    private static void BuilderDesk()
    {
        DeskStepChecks();
        DeskFrontChecks();
        DeskSourceChecks();
        DeskTextChecks();
    }

    private static ProjectFile DeskProject(int id, ProjectKind kind, ProjectStage stage, bool plans = false, long estimate = 0, int contractor = -1)
    {
        return new ProjectFile { Id = id, Kind = kind, Stage = stage, PlansDone = plans, Estimate = estimate, Contractor = contractor };
    }

    private static void DeskStepChecks()
    {
        Check(FrontDeskRules.Step(null, false, out int none) == DeskStep.Land && none == 0 && FrontDeskRules.Step(new List<ProjectFile>(), true, out none) == DeskStep.Plans && none == 0,
            "front desk: no ground and no project, the land first; ground of the player's, the plans");
        var list = new List<ProjectFile> { DeskProject(3, ProjectKind.Plot, ProjectStage.Design) };
        Check(FrontDeskRules.Step(list, false, out int id) == DeskStep.Plans && id == 3, "front desk: a project with no plans drawn needs its plans, named");
        list[0].PlansDone = true;
        Check(FrontDeskRules.Step(list, false, out id) == DeskStep.Permission && id == 3, "front desk: plans drawn, planning permission at City Hall next");
        list[0].Stage = ProjectStage.Planning;
        Check(FrontDeskRules.Step(list, false, out id) == DeskStep.Deciding && id == 3, "front desk: an application in, the council deciding");
        list[0].Stage = ProjectStage.Ready;
        Check(FrontDeskRules.Step(list, false, out id) == DeskStep.Plans && id == 3, "front desk: ready with no estimate is not quoted (a builder prices off the plans' estimate)");
        list[0].Estimate = 2500000;
        Check(FrontDeskRules.Step(list, false, out id) == DeskStep.Ready && id == 3, "front desk: a project ready to start is quoted");
        list.Insert(0, DeskProject(1, ProjectKind.Rebuild, ProjectStage.Design, true));
        list.Add(DeskProject(5, ProjectKind.Plot, ProjectStage.Ready, true, 900000));
        Check(FrontDeskRules.Step(list, true, out id) == DeskStep.Ready && id == 3, "front desk: the building furthest along, the first of two ready ones");
        var others = new List<ProjectFile>
        {
            DeskProject(7, ProjectKind.Contract, ProjectStage.Ready, true, 500000), DeskProject(8, ProjectKind.Refit, ProjectStage.Ready, true, 50000),
            DeskProject(9, ProjectKind.Plot, ProjectStage.Ready, true, 500000, 2), DeskProject(10, ProjectKind.Plot, ProjectStage.Finished, true, 500000)
        };
        Check(FrontDeskRules.Step(others, true, out id) == DeskStep.Plans && id == 0, "front desk: a tender's works, a refit, a building with a builder and a finished one are never quoted");
        others[2].Stage = ProjectStage.Building;
        Check(FrontDeskRules.Step(others, true, out id) == DeskStep.Building && id == 9, "front desk: with nothing else waiting, the building a builder of the city's is putting up");
        others.Add(DeskProject(11, ProjectKind.Rebuild, ProjectStage.Design));
        Check(FrontDeskRules.Step(others, true, out id) == DeskStep.Plans && id == 11, "front desk: a building of the player's still to draw comes before one being built");
        Check(FrontDeskRules.NeedKey(DeskStep.Ready) == null, "front desk: a ready building needs nothing first");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (DeskStep s in new[] { DeskStep.Land, DeskStep.Plans, DeskStep.Permission, DeskStep.Deciding, DeskStep.Building })
        {
            string key = FrontDeskRules.NeedKey(s);
            Check(key != null && key.StartsWith("reallife_bld_desk_need_", StringComparison.Ordinal) && keys.Add(key), "front desk: " + s + " has its own words of what to do first (" + key + ")");
        }
        int underWay = FrontDeskRules.UnderWay(new List<ProjectFile>
        {
            DeskProject(1, ProjectKind.Plot, ProjectStage.Building), DeskProject(2, ProjectKind.Contract, ProjectStage.Building), DeskProject(3, ProjectKind.Plot, ProjectStage.Design),
            DeskProject(4, ProjectKind.Plot, ProjectStage.Finished), DeskProject(5, ProjectKind.Rebuild, ProjectStage.Abandoned), null
        }, out int building);
        Check(underWay == 3 && building == 2 && FrontDeskRules.UnderWay(null, out int b0) == 0 && b0 == 0, "front desk: the office manager counts the projects under way and those being built, none of none");
        var many = new List<ProjectFile>(200);
        for (int i = 0; i < 200; i++)
        {
            many.Add(DeskProject(i + 1, (ProjectKind)(i % 4), (ProjectStage)(i % 6), i % 3 == 0, i % 5 * 1000, i % 7 == 0 ? 1 : -1));
        }
        var timer = System.Diagnostics.Stopwatch.StartNew();
        int sum = 0;
        for (int n = 0; n < 20000; n++)
        {
            sum += (int)FrontDeskRules.Step(many, (n & 1) == 0, out int p) + p;
        }
        timer.Stop();
        Check(sum > 0 && timer.Elapsed.TotalMilliseconds < 400.0, "front desk: 20000 looks over 200 projects in " + timer.Elapsed.TotalMilliseconds.ToString("0") + " ms (a press asks for one)");
    }

    private static void DeskFrontChecks()
    {
        float[] x = { 10f, 2f, -3f, 2f }, z = { 0f, 1f, 9f, -1f };
        Check(FrontDeskRules.Front(x, z, 4, 0f, 0f) == 1, "front desk: the desk nearest the door the player comes in by, the first of two as near");
        Check(FrontDeskRules.Front(x, z, 4, -3f, 8f) == 2 && FrontDeskRules.Front(x, z, 1, -3f, 8f) == 0, "front desk: only the desks somebody sits at are counted");
        Check(FrontDeskRules.Front(x, z, 0, 0f, 0f) == -1 && FrontDeskRules.Front(null, z, 4, 0f, 0f) == -1 && FrontDeskRules.Front(x, new float[2], 4, 0f, 0f) >= 0
              && FrontDeskRules.Front(x, new float[2], 4, 0f, 0f) < 2, "front desk: no desk with nobody at one; never past the places given");
        Check(BuildingsRules.Visitable(true, false, false, false) && !BuildingsRules.Visitable(false, false, false, false) && !BuildingsRules.Visitable(true, true, false, false)
              && !BuildingsRules.Visitable(true, false, true, false) && !BuildingsRules.Visitable(true, false, false, true),
            "front desk: a builder's office receives the player only while it is the builder's, never in a unit the player or another player rents or owns");
    }

    private static void DeskSourceChecks()
    {
        string desk = ModSource(@"Construction\BuilderDesk.cs"), game = ModSource(@"Construction\BuilderDeskGame.cs"), pointer = ModSource(@"Construction\BuilderDeskPointer.cs");
        string director = ModSource(@"Construction\ConstructionDirector.Desk.cs"), tests = ModSource(@"Construction\ConstructionDirector.DeskTests.cs");
        string main = ModSource(@"Construction\ConstructionDirector.cs"), guide = ModSource(@"Construction\ConstructionGuide.cs"), rules = ModSource(@"Construction\FrontDeskRules.cs");
        string leases = ModSource(@"Uptown\LifeDirector.Leases.cs"), player = ModSource(@"Uptown\LifeGame.Player.cs"), contract = ModSource(@"Core\GameContract.cs");
        string links = ModSource(@"Wiring\Wiring.ConstructionLinks.cs"), hireApp = ModSource(@"Construction\ConstructionApp.Hire.cs");
        Check(desk.Length > 0 && game.Length > 0 && pointer.Length > 0 && director.Length > 0 && tests.Length > 0 && rules.Length > 0, "front desk sources: the dialog, the game's side, the pointer, the director's part, its tests and the rules are there");
        Check(!Regex.IsMatch(rules, @"\bUnityEngine\b|\bRandom\.|DateTime|TickCount|\bTime\.|GetHashCode|new [A-Z][A-Za-z]*\["), "front desk sources: FrontDeskRules is game-free, draws nothing and allocates nothing");
        // One quote and one hire: the app's own.
        string quote = MethodText(director, "internal bool DeskQuote(");
        Check(quote.Contains("Quotes(p, _deskQuotes)") && !Regex.IsMatch(desk + director, @"TenderRules\.|CommissionRules\.Bids\(|CommissionRules\.Pace\("),
            "front desk sources: the desk's quote is the director's Quotes, the app's own; nothing is priced a second way");
        Check(MethodText(desk, "private static void Hired(").Contains("d.HireBuilder(p, rival, false)") && hireApp.Contains("_director.HireBuilder(p, r, false)"),
            "front desk sources: the desk signs through the app's own HireBuilder, money and all");
        string sign = MethodText(desk, "private static DialogEntry Sign(");
        Check(sign.Contains("FinishDialog()") && sign.Contains("GameConfirm.Show(\"reallife_bld_confirm_hire\".Localize(), \"reallife_bld_confirm_hire_body\".Localize(body)")
              && hireApp.Contains("Confirm(\"reallife_bld_confirm_hire\", \"reallife_bld_confirm_hire_body\""), "front desk sources: the contract is asked in the game's question box with the app's own words, the dialogue closed first");
        string open = MethodText(desk, "public static bool Open(");
        Check(open.Contains("ShowDialog(type, NavigationBlocker.SpecialEmployeeDialog, null, Closed, with)") && open.Contains("BuilderDeskGame.Hold();") && desk.Contains("Guard.Way(BuilderDeskGame.Release)")
              && desk.Contains("DialogController.current.ShowEntry(Guard.Entry(BuilderDesk.First()))"), "front desk sources: the game's dialogue panel as its special desks open it, facing the person, the time held and given back as it closes");
        Check(MethodText(game, "internal static void Hold(").Contains("GoingPublic.InSession") && MethodText(game, "internal static void Release(").Contains("if (_paused)"),
            "front desk sources: the game is paused as its desk staff pause it, never in a Going Public session, and runs again only if the talk paused it");
        Check(game.Contains("public const string Talk = \"click_to_talk\";") && MethodText(game, "internal static bool Install(").Contains("_list.Insert(0, _behaviour);")
              && game.Contains("return entityController is BuilderDeskPointer;") && contract.Contains("ClickDesk, ClickBuilderDesk)"),
            "front desk sources: the game's own call to talk, first in its list and answering only for the desk's pointer, a part of the game contract");
        Check(MethodText(game, "internal static EmployeeStationController Front(").Contains("FrontDeskRules.Front(Xs, Zs, Stations.Count, door.x, door.z)") && MethodText(game, "internal static ThirdPersonCharacter Seated(").Contains("!e.isPlayer && !e.IsAway"),
            "front desk sources: the front desk is the game's own staff nearest the door, never a desk the player works at or its person away");
        Check(pointer.Contains("customOverlayHeaderKey = headerKey") && pointer.Contains("SimpleOverlayType.ClickToAction") && MethodText(pointer, "public override bool ShouldReactToIoEnter(").Contains("PlacementSystem.IsInPlacementMode")
              && MethodText(pointer, "public override bool ShouldReactToIoEnter(").Contains("InteriorDesignerUI.IsOpen"), "front desk sources: the pointer has the game's own card and stands aside while furniture is placed or the interior designer is open");
        Check(MethodText(main, "private void Slow(").Contains("DeskSecond();") && MethodText(main, "public void StandDown(").Contains("DesksDown();") && MethodText(director, "private void DesksDown(").Contains("BuilderDeskGame.Remove();"),
            "front desk sources: the desk is looked after on the director's second and put away with it, its click taken out of the game's list");
        Check(leases.Contains("LifeGame.PlayerIn(LeasedUp[i]) && LifeGame.Visitable(LeasedUp[i], Lessee(i))") && MethodText(player, "public static bool Visitable(").Contains("reg.BuildingOwnedByPlayer, GoingPublic.Foreign(reg)")
              && links.Contains("ConstructionBridges.RivalHere = RivalHere;") && links.Contains("ConstructionBridges.RivalHere = null;"),
            "front desk sources: only a standing builder's office it may receive the player in, across the bridge the wiring connects and takes off");
        Check(MethodText(director, "private void DeskSecond(").Contains("if (_deskPointer != null && _deskPointer.Staffed)") && director.Contains("private const float DeskLookAgain = 15f;"),
            "front desk sources: once a second one look at where the player is; the building's items looked through only when they come in and now and then while nobody sits there");
        Check(!Regex.IsMatch(desk + game + pointer + director + tests, @"using System\.Linq|\.Where\(|\.Select\(|void Update\(|void LateUpdate\(|FindObjectsOfType|FindObjectsByType|FindObjectOfType"),
            "front desk sources: no LINQ, no frame method, no scene search");
        // Every test is a public static method, a button, and (but the conversation, which needs a press to close) a run's check.
        int found = 0;
        foreach (Match m in Regex.Matches(tests, @"public static bool (TestDesk[A-Za-z]*Now)\(\)"))
        {
            found++;
            Check(guide.Contains("() => ConstructionDirector." + m.Groups[1].Value + "()"), "front desk sources: " + m.Groups[1].Value + " is a button of the feature");
        }
        Check(found == 3, "front desk sources: three tests (the way to a desk, the conversation, every builder's pages): " + found);
        int runs = 0;
        foreach (Match m in Regex.Matches(guide, @"\.Check\(""reallife_bld_test_desk_[a-z_]+"", \(\) => ConstructionDirector\.TestDesk[A-Za-z]*Now\(\), ""Construction check \(([^)]+)\)"""))
        {
            runs++;
            Check(tests.Contains("\"" + m.Groups[1].Value + "\""), "front desk sources: the line \"Construction check (" + m.Groups[1].Value + ")\" is written by its test");
        }
        Check(runs == 2 && guide.Contains(".Test(\"reallife_bld_test_desk_talk\", () => ConstructionDirector.TestDeskTalkNow())") && tests.Contains("\"talk to the builder at the desk now\""),
            "front desk sources: the way to a desk and the pages are checks of the run; the conversation is a button");
        Check(tests.Contains("private const string DeskCheck = \"Construction check (\";") && Regex.Matches(tests, @"\(pass \? "": PASS\."" : "": FAIL\.""\)").Count == 2
              && tests.Contains(": PASS.\" : \", but the game's destination could not be set: FAIL.\""), "front desk sources: each test ends its line PASS or FAIL");
    }

    private static void DeskTextChecks()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string name in new[] { @"Construction\BuilderDesk.cs", @"Construction\BuilderDeskGame.cs", @"Construction\FrontDeskRules.cs", @"Construction\ConstructionDirector.Desk.cs",
                     @"Construction\ConstructionDirector.DeskTests.cs" })
        {
            foreach (Match m in Regex.Matches(ModSource(name), "\"(reallife_[a-z0-9_]+[a-z0-9])\""))
            {
                if (!m.Groups[1].Value.EndsWith("_calldialogtype", StringComparison.Ordinal))
                {
                    keys.Add(m.Groups[1].Value);   // a dialogue's type is a name hashed for the game, never a text
                }
            }
        }
        keys.Add("reallife_bld_test_desk_go");
        keys.Add("reallife_bld_test_desk_talk");
        keys.Add("reallife_bld_test_desk_pages");
        keys.Add("reallife_contract_part_click_builder_desk");
        Check(keys.Contains("reallife_bld_desk_quote") && keys.Contains("reallife_bld_desk_need_land") && keys.Contains("reallife_bld_desk_npc"), "front desk texts: their keys are found: " + keys.Count);
        Dictionary<string, string>[] words = LanguageWords(out string[] codes);
        if (!LanguagesRead("front desk texts", words, codes))
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
            Check(missing == null, "front desk texts: every key of the front desks is in " + codes[l] + ".json, in the style, with the English names (" + (missing ?? "all") + ")");
        }
        foreach (string key in new[] { "reallife_bld_desk_no_way", "reallife_bld_test_desk_here", "reallife_bld_test_desk_none", "reallife_bld_test_desk_busy" })
        {
            string text = words[0].TryGetValue(key, out string t) ? t : string.Empty;
            Check(text.Length > 0 && text.Length + 40 <= 240, "front desk texts: the notice " + key + " stays under 240 characters with a long builder's name (" + text.Length + ")");
        }
        string part = words[0].TryGetValue("reallife_contract_part_click_builder_desk", out string p) ? p : string.Empty;
        Check(ModSource(@"Core\GameContract.cs").Contains("\"click_builder_desk\", \"construction\", \"" + part + "\""), "front desk texts: the contract part's words are its English in the game contract");
        var guideLines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "- Or go to a builder's office in Uptown or Cedar Heights and click the person at the front desk:" }, { "pt", "- Ou vá ao escritório de uma construtora em Uptown" },
            { "zh-cn", "- 也可以去上城或雪松高地的一家建筑商的办公室" }
        };
        for (int l = 0; l < words.Length && l < 3; l++)
        {
            string page = words[l].TryGetValue("reallife_guide_construction", out string g) ? g : string.Empty;
            int line = guideLines.TryGetValue(codes[l], out string start) ? page.IndexOf(start, StringComparison.Ordinal) : -1;
            int section = page.IndexOf("{bld_hire_retention}", StringComparison.Ordinal);
            Check(line > section && section >= 0 && page.IndexOf("\n#", section, StringComparison.Ordinal) > line,
                "front desk texts: the Guide's construction page in " + codes[l] + " has the front desk in its section on hiring a builder");
        }
        var starts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "en", "Uptown's builders now have someone at the front desk." }, { "pt", "As construtoras de Uptown agora têm alguém na recepção." }, { "zh-cn", "上城的建筑商现在有前台接待了。" },
            { "de", "Die Bauunternehmen in Uptown haben jetzt jemanden am Empfang." }, { "fr", "Les constructeurs d'Uptown ont maintenant quelqu'un à l'accueil." },
            { "es", "Las constructoras de Uptown ahora tienen a alguien en recepción." }, { "ru", "У строительных компаний Uptown теперь есть ресепшен." },
            { "nl", "De bouwbedrijven van Uptown hebben nu iemand aan de balie." }, { "tr", "Uptown'daki inşaat şirketlerinin artık bir resepsiyonu var." }
        };
        for (int l = 0; l < words.Length; l++)
        {
            string next = starts.TryGetValue(codes[l], out string start) ? FixRow(words[l], start) : string.Empty;   // the row that holds it
            int line = start != null ? next.IndexOf(start, StringComparison.Ordinal) : -1;
            int fix = FixMarks.TryGetValue(codes[l], out string mark) ? next.IndexOf(mark, StringComparison.Ordinal) : -1;
            Check(line >= 0 && (fix < 0 || line < fix), "front desk texts: What's new in " + codes[l] + " has the front desks, before its fixes");
        }
    }
}
