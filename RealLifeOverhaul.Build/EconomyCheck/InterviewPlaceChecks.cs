using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;

// A job interview where it is held (Scripts/Careers: Work/InterviewRules, InterviewVisit, InterviewGame, the director's
// Interview). The character used to stand at the entrance, or wherever they were at home, while the game's panel passed the
// hour. Now at an employer's they walk to its entrance door and in, the screen goes black as the game's door has it, the
// minutes pass behind it (the game's own skip alone, the shared clock's pace in a Going Public session) and they come back out
// at the same door facing the street; a video interview sits them at the home's computer, or with none stays on the panel
// with one word. First the game-free rules, then the sources, read as the game's panel and door cannot be run here, then
// the texts in every language and What's new in any row.
internal static partial class Program
{
    private static void InterviewPlaces()
    {
        InterviewPlaceRules();
        InterviewPlaceEnds();
        InterviewPlaceSources();
        InterviewPlaceTexts();
    }

    private static string CareersFile(string relative)
    {
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts", relative));
        return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path).Replace("\r\n", "\n") : string.Empty;
    }

    // ---------------------------------------------------------------- where it is held

    private static void InterviewPlaceRules()
    {
        Check(InterviewRules.Way(false, false, true, false) == InterviewWay.Door, "interview places: outside by the employer with its door found, in at the door");
        Check(InterviewRules.Way(false, false, false, false) == InterviewWay.InPlace, "interview places: outside with no door found, the black screen where the player stands");
        Check(InterviewRules.Way(false, true, true, false) == InterviewWay.InPlace && InterviewRules.Way(false, true, false, true) == InterviewWay.InPlace,
            "interview places: inside the employer's building, held where the player stands, a door or a computer nowhere in it");
        Check(InterviewRules.Way(true, true, false, true) == InterviewWay.Computer && InterviewRules.Way(true, false, true, true) == InterviewWay.Computer,
            "interview places: a video interview with a computer at home is at the computer, whatever the door");
        Check(InterviewRules.Way(true, true, false, false) == InterviewWay.Panel, "interview places: a video interview from a home with no computer stays on the panel where the player stands");
        bool same = true;
        for (int i = 0; i < 16; i++)
        {
            bool r = (i & 1) != 0, inside = (i & 2) != 0, door = (i & 4) != 0, pc = (i & 8) != 0;
            same &= InterviewRules.Way(r, inside, door, pc) == InterviewRules.Way(r, inside, door, pc);
            same &= r || InterviewRules.Way(r, inside, door, pc) != InterviewWay.Computer;   // never a computer for a post at a place
            same &= !r || (InterviewRules.Way(r, inside, door, pc) != InterviewWay.Door && InterviewRules.Way(r, inside, door, pc) != InterviewWay.InPlace);
        }
        Check(same, "interview places: the way is the same for the same place every time, and a place and a video interview never swap their ways");
        Check(InterviewRules.SkipBehind(false) && !InterviewRules.SkipBehind(true),
            "interview places: the game's skip behind the black screen alone; in a Going Public session the shared clock is never skipped");
        Check(InterviewRules.TellNoComputer(true, false, false) && !InterviewRules.TellNoComputer(true, false, true) && !InterviewRules.TellNoComputer(true, true, false)
              && !InterviewRules.TellNoComputer(false, false, false),
            "interview places: told once, and only for a video interview from a home with no computer");
        Check(InterviewRules.OutAtDoor(0f, 1f) && InterviewRules.OutAtDoor(InterviewRules.OutReach, InterviewRules.OutFacing) && !InterviewRules.OutAtDoor(InterviewRules.OutReach + 0.01f, 1f)
              && !InterviewRules.OutAtDoor(0f, InterviewRules.OutFacing - 0.01f) && !InterviewRules.OutAtDoor(0f, -1f),
            "interview places: the self-check counts the player out at the door only on its step, facing the street");
    }

    // ---------------------------------------------------------------- every end

    private static void InterviewPlaceEnds()
    {
        Check(InterviewRules.End(true, true, true) == InterviewEnd.Finished && InterviewRules.End(true, true, false) == InterviewEnd.GivenUp
              && InterviewRules.End(true, false, false) == InterviewEnd.CalledOff && InterviewRules.End(true, false, true) == InterviewEnd.CalledOff,
            "interview places: its time passed is finished, part of it given up, none of it called off on the way");
        Check(InterviewRules.End(false, true, true) == InterviewEnd.LetGo && InterviewRules.End(false, false, false) == InterviewEnd.LetGo,
            "interview places: a load, the option switched off, a fault or the city going lets it go, whatever had passed");
        Check(InterviewRules.Count(InterviewEnd.Finished) == InterviewCount.Whole && InterviewRules.Count(InterviewEnd.GivenUp) == InterviewCount.Left
              && InterviewRules.Count(InterviewEnd.CalledOff) == InterviewCount.Nothing && InterviewRules.Count(InterviewEnd.LetGo) == InterviewCount.Nothing,
            "interview places: the whole counts an offer or a no, one given up the notice, the rest nothing, as the standing interview did");
        var ways = new[] { InterviewWay.Door, InterviewWay.InPlace };
        var ends = new[] { InterviewEnd.Finished, InterviewEnd.GivenUp, InterviewEnd.CalledOff, InterviewEnd.LetGo };
        bool back = true;
        foreach (InterviewWay way in ways)
        {
            foreach (InterviewEnd end in ends)
            {
                // every end of one that went black gives the screen back; the door's comes out at the door in the same city
                InterviewOut here = InterviewRules.Out(way, true, true), gone = InterviewRules.Out(way, true, false);
                back &= here != InterviewOut.Nothing && gone == InterviewOut.Cleared && (way == InterviewWay.Door ? here == InterviewOut.AtDoor : here == InterviewOut.Unfade);
                back &= InterviewRules.Count(end) != InterviewCount.Whole || end == InterviewEnd.Finished;
            }
        }
        Check(back, "interview places: every end of an interview that went black gives the black screen back, out at the door for one gone in at it");
        Check(InterviewRules.Out(InterviewWay.Door, false, true) == InterviewOut.Nothing && InterviewRules.Out(InterviewWay.InPlace, false, false) == InterviewOut.Nothing,
            "interview places: one ended before the screen went black (the walk called off) has nothing to give back and moves nobody");
    }

    // ---------------------------------------------------------------- the sources

    private static void InterviewPlaceSources()
    {
        string applying = CareersFile(@"Careers\CareersDirector.Applying.cs");
        string director = CareersFile(@"Careers\CareersDirector.cs");
        string visit = CareersFile(@"Careers\InterviewVisit.cs");
        string game = CareersFile(@"Careers\InterviewGame.cs");
        string computer = CareersFile(@"Careers\ComputerSitting.cs");
        string mod = CareersFile(@"Core\RealLifeOverhaulMod.cs");
        string tests = CareersFile(@"Careers\CareersDirector.Tests.cs");

        string interview = CompanyMethod(applying, "public string Interview(int id)");
        Check(InOrder(interview, "CanInterview(a, out minutes)", "if (a.Post.Work == (int)WorkMode.Remote)", "return VideoInterview(a, sitting);",
                "InterviewGame.Door(a.Post.Street, a.Post.Number)", "InterviewRules.Way(false, inside, door != null, false)", "Visit(sitting, wanted, door, a.Post.Employer)",
                "WayCheck("),
            "interview places: an interview at a place goes in at the employer's door by the rule, with its self-check line");
        string video = CompanyMethod(applying, "private string VideoInterview(");
        Check(InOrder(video, "DeskGame.UsableComputer()", "InterviewRules.Way(true, true, false, computer != null)", "SeatBusy()", "StartAtComputer(computer, sitting",
                "InterviewRules.TellNoComputer(", "\"reallife_work_note_interview_no_computer\"", "Offer(sitting)"),
            "interview places: a video interview sits at the home's computer, else the panel where the player stands with the word said once");
        string place = CompanyMethod(applying, "private string Visit(");
        Check(InOrder(place, "SeatBusy()", "new InterviewVisit(", "DeskGame.Show(visit)", "_visit = visit;", "Hold(sitting, visit);"),
            "interview places: the interview at a place is the game's panel's activity, in the book of sittings so every end settles it once");
        Check(InOrder(CompanyMethod(director, "private void CloseSitting()"), "InterviewVisit visit = _visit;", "_visit = null;", "visit?.Close();"),
            "interview places: the careers letting every sitting go (switched off, a fault, the city going) bring the player out of an interview too");
        Check(computer.Contains("|| sitting.Kind == SittingKind.Interview;") && computer.Contains("public bool RequiresEnergy() => !_work;"),
            "interview places: a video interview at the computer does not end when the energy runs low, as the standing one did not");

        Check(!visit.Contains("void Update(") && !visit.Contains("void LateUpdate(") && !visit.Contains("FindObjects"),
            "interview places: the interview has no frame method and no search of its own: the game's panel calls it");
        string end = CompanyMethod(visit, "private void End(bool settle, bool walking)");
        Check(InOrder(end, "_state = PlayerActivityState.Finished;", "Unblock();", "_tally.Settle(", "InterviewRules.End(settle", "GiveBack();", "InterviewRules.Count(_end)",
                "sitting.SkipEnded(minutes, whole);"),
            "interview places: every end gives back the navigation and the black screen before it counts anything");
        Check(InOrder(CompanyMethod(visit, "public void Close()"), "InterviewGame.CancelSkip();", "End(false, true);"),
            "interview places: closed by the careers, the game's skip is called off and the interview let go with everything given back");
        string giveBack = CompanyMethod(visit, "private void GiveBack()");
        Check(InOrder(giveBack, "ReferenceEquals(PlayerHelper.PlayerController, _player) && !InterviewGame.Loading", "InterviewRules.Out(_way, _faded, sameCity)",
                "case InterviewOut.Cleared:", "InterviewGame.Clear(false, _overlay);", "StartCoroutine(Guard.Routine(Out(how)))", "InterviewGame.PutOut(_player, _door);", "InterviewGame.Clear(true, _overlay);"),
            "interview places: the screen and the player are given back by the rule, at once where no fade back can run, and nobody of a new city is moved");
        string inside = CompanyMethod(visit, "private IEnumerator Inside()");
        Check(InOrder(inside, "GoingPublic.ClockShared", "InterviewGame.Fade(shared ? \"reallife_work_interview_fade\" : null)", "GiveBack();", "InterviewRules.SkipBehind(shared)",
                "InterviewGame.Skip(this)"),
            "interview places: the black screen, then the game's own skip only while the clock is this player's");
        Check(InOrder(CompanyMethod(visit, "private IEnumerator Out(InterviewOut how)"), "InterviewGame.PutOut(_player, _door);", "InterviewGame.DoorSound(_door, true);",
                "InterviewGame.Unfade();", "Check(how, put, false);"),
            "interview places: out at the door facing the street under the black screen, the door's sound, then the fade back and the self-check line");
        Check(InOrder(CompanyMethod(game, "public static bool Skip("), "ReferenceEquals(uis.playerActivityUI.GetCurrentActivity, activity)", "uis.playerActivityUI.ForceTimeMachine();")
              && !game.Contains("StartTimeMachine(") && !visit.Contains("StartTimeMachine("),
            "interview places: the skip is the game's panel's own press (its minutes counted from the press), never a skip started by the mod");
        Check(InOrder(CompanyMethod(game, "public static void PutOut("), "player.ResetNavigation();", "navmeshAgent.Warp(door.Step);", "player.transform.rotation = door.Facing;"),
            "interview places: out on the door's step with the door's heading, as the game puts a player coming out of a building");
        Check(InOrder(CompanyMethod(game, "private static Vector3 FreeStep("), "door.forward * (i * StepLength)", "Physics.CheckSphere(", "LayerHelper.buildingsLayerMask"),
            "interview places: the step outside the door is the game's own exit's, the first free of buildings");
        Check(visit.Contains("Guard.For(\"careers_jobs\", \"InterviewVisit\"") && visit.Contains("return Guard.Ended(this);"),
            "interview places: the game's calls of the interview are behind the guard, which ends it as its own Finish would");

        Check(mod.Contains("\"reallife_work_test_interview_place\", () => Careers.CareersDirector.Instance?.TestInterviewPlace()")
              && mod.Contains("\"reallife_work_test_interview_home\", () => Careers.CareersDirector.Instance?.TestInterviewHome()")
              && tests.Contains("public void TestInterviewPlace()") && tests.Contains("public void TestInterviewHome()"),
            "interview places: Options > Tests start an interview at the nearest employer and a video interview at home");
        Check(InOrder(CompanyMethod(tests, "public void TestInterviewPlace()"), "InterviewGame.Distance(", "TestInterviewOf(pick, false)", "WorkGame.AtWork(", "DeskGame.Route(",
                "\"reallife_work_test_interview_go\"", "Interview(a.Post.Id)"),
            "interview places: the place test sets the nearest employer's interview for this hour and begins it by its entrance, else sends the player there");
    }

    // ---------------------------------------------------------------- the texts

    private static void InterviewPlaceTexts()
    {
        string[] keys = { "reallife_work_note_interview_no_computer", "reallife_work_interview_fade", "reallife_work_test_interview_place",
            "reallife_work_test_interview_home", "reallife_work_test_interview_go" };
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, code + ".json"));
            bool all = words != null;
            for (int i = 0; all && i < keys.Length; i++)
            {
                all = words.TryGetValue(keys[i], out string t) && t.Length > 0 && t.Length <= 240 && !t.Contains('!') && !t.Contains((char)0x2014) && !t.Contains((char)0x2013);
            }
            Check(all && words["reallife_work_test_interview_go"].Contains("{employer}") && words["reallife_work_test_interview_go"].Contains("{address}"),
                "interview places: the five texts in " + code + ", the destination's with its employer and address");
        }
        var whatsNew = new Dictionary<string, string>
        {
            { "en", "you come back out at the same door" }, { "pt", "você sai de volta pela mesma porta" }, { "zh-cn", "你从同一扇门出来" },
            { "de", "durch dieselbe Tür wieder heraus" }, { "fr", "vous ressortez par la même porte" }, { "es", "sales por la misma puerta" },
            { "ru", "выходите через ту же дверь" }, { "nl", "door dezelfde deur weer naar buiten" }, { "tr", "aynı kapıdan geri çıkarsınız" }
        };
        foreach (KeyValuePair<string, string> line in whatsNew)
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(DesignLocales, line.Key + ".json"));
            Check(AnyChangesRow(words, line.Value), "interview places: What's new says so in " + line.Key);
            Check(words != null && words.TryGetValue("reallife_guide_careers_jobs", out string guide) && guide.Contains(line.Key == "zh-cn" ? "从同一扇门出来" : line.Value),
                "interview places: the Guide's Careers page says how an interview is held in " + line.Key);
        }
    }
}
