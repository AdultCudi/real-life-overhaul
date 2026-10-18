using System;
using System.Collections.Generic;

// The mod's activities that walk the player to their place on the game's own panel (Scripts/Careers: SchoolDesk for a
// class at a desk, DeskWork for a working day at the employer's, ComputerSitting for a class, homework or a working day at
// the computer at home), read from their source, as the game's panel cannot be run here: each sets off as the game's own
// activities do (docs/features/careers.md, "Setting off"), ends however the panel ends it, and lets the player go whatever
// the game's calls before it throw; the player sits at a desk computer; the university's class is on the game's Start key;
// a day from home is begun from the home computer's own button, and counts as at the desk, whatever the player's energy.
// Then What's new's lines for them.
internal static partial class Program
{
    private static void SetOff()
    {
        string careers = System.IO.Path.Combine(LocalesFolder(), @"..\Scripts\Careers");
        string Read(string file)
        {
            string path = System.IO.Path.Combine(careers, file);
            return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : string.Empty;
        }
        string school = Read("SchoolDesk.cs"), desk = Read("DeskWork.cs"), computer = Read("ComputerSitting.cs");
        string game = Read("DeskGame.cs"), buttons = Read("ComputerButtons.cs"), director = Read("CareersDirector.Desk.cs");
        Check(school.Length > 0 && desk.Length > 0 && computer.Length > 0 && game.Length > 0 && buttons.Length > 0 && director.Length > 0,
            "set off: the class's desk, the work desk, the computer at home, the desk's game side, the computer's buttons and the director's desks are read");
        string walk = "private IEnumerator Walk()", changeState = "public void ChangeState(PlayerActivityState state)", end = "private void End(bool settle, bool walking)";
        string finished = "_state = PlayerActivityState.Finished;", study = "UnsetNavigationBlocker(NavigationBlocker.StudyActivity)", unset = "UnsetNavigationBlocker(";

        // ---------------------------------------------------------------- setting off
        bool still = true;
        foreach (string press in new[] { MethodText(school, "private void Press()"), MethodText(desk, "private void Begin(bool late)"), MethodText(computer, "public void Go()") })
        {
            still &= press.Length > 0 && !press.Contains("PlayerActivityState.MovingTowardsActivity");
        }
        Check(still, "set off: a press leaves the activity not started for the rest of its frame (SchoolDesk.Press, DeskWork.Begin, ComputerSitting.Go)");
        Check(SetsOffInSteps(MethodText(school, walk), "player.SetGoal(_door") && SetsOffInSteps(MethodText(desk, walk), "DeskGame.WalkTo(")
              && SetsOffInSteps(MethodText(computer, walk), "player.SetGoal(target"),
            "set off: on its way a frame after the press, and the walk order given in the frame after that, its frame noted (SchoolDesk, DeskWork, ComputerSitting)");
        string said = MethodText(game, "public static void SetOffSeen(");
        bool seen = said.Contains("Careers check (set off)") && said.Contains("Meta.Dev.Verbose");
        foreach (string source in new[] { school, desk, computer })
        {
            seen &= Occurrences(source, "DeskGame.SetOffSeen(") == 2 && source.Contains(", true, _orderFrame);") && source.Contains(", false, _orderFrame);");
        }
        Check(seen, "set off: with Developer diagnostics on, each walk's end says whether the panel kept it (DeskGame.SetOffSeen, on arrival and when called off)");

        // ---------------------------------------------------------------- the end and standing up
        Check(EndsWhenFinished(MethodText(school, changeState), "Finish();") && EndsWhenFinished(MethodText(desk, changeState), "End(true, true);")
              && EndsWhenFinished(MethodText(computer, changeState), "Finish();"),
            "stand up: the game's panel ending an activity (ChangeState to Finished) settles its minutes and stands the player up, as its Finish does");
        string deskEnd = MethodText(desk, end);
        Check(ComesBefore(MethodText(school, "public void Finish()"), finished, "StandUp();") && ComesBefore(deskEnd, finished, "DeskGame.StandUp(")
              && ComesBefore(deskEnd, finished, "DeskGame.StandFrom(") && ComesBefore(MethodText(computer, end), finished, "StandUp(player);"),
            "stand up: an activity is finished before the player stands up, so that nothing the game's calls do can end it twice");
        Check(FreesInFinally(MethodText(school, "private void StandUp()"), study) && Occurrences(school, unset) == 1
              && FreesInFinally(MethodText(computer, "private void StandUp(PlayerController player)"), study) && Occurrences(computer, unset) == 1,
            "stand up: at a class's desk and at the computer at home the study blocker goes in a finally, and nowhere else");
        string unhold = MethodText(game, "private static void Unhold(PlayerController player)");
        Check(FreesInFinally(MethodText(game, "public static void StandFrom(EmployeeStationController station)"), "Unhold(player)")
              && FreesInFinally(MethodText(game, "public static void StandUp(Vector3 at, EmployeeStationController station)"), "Unhold(player)")
              && unhold.Contains("UnsetNavigationBlocker(NavigationBlocker.WorkActivity)") && Occurrences(game, unset) == 1,
            "stand up: at a station and at a desk the work blocker goes in a finally (DeskGame.Unhold), and nowhere else");

        // ---------------------------------------------------------------- the pose, the keys and the home computer
        string seat = MethodText(game, "public static Transform SeatOf(EmployeeStationController station)");
        string kind = MethodText(game, "public static StationKind KindOf(EmployeeStationController station)");
        string arrived = MethodText(desk, "private void Arrived()");
        bool desks = seat.Contains("case WorkstationController") && seat.Contains("case DesktopWorkstationController")
                     && kind.Contains("case WorkstationController") && kind.Contains("case DesktopWorkstationController");
        Check(desks && arrived.Contains("DeskGame.SeatOf(_station)") && arrived.Contains("DeskGame.SitOn(") && arrived.Contains("DeskGame.SitAt(_station)"),
            "seated: at every desk computer a job's desk is chosen from, and at a fitted desk, the player sits on its chair; at a till or a counter they stand");
        string keys = MethodText(school, "public ButtonInfo[] GetButtons()");
        int only = keys.IndexOf("if (_offer.Only)", StringComparison.Ordinal);
        int rest = keys.IndexOf("ButtonInfo[] game = _inner.GetButtons();", StringComparison.Ordinal);
        string room = only >= 0 && rest > only ? keys.Substring(only, rest - only) : string.Empty;
        string schoolDesk = rest >= 0 ? keys.Substring(rest) : string.Empty;
        Check(room.Contains("PlayerAction.Confirm, _offer.Can") && !room.Contains("PlayerAction.SecondaryInteract") && schoolDesk.Contains("PlayerAction.SecondaryInteract, _offer.Can"),
            "set off: a classroom of the university has its class on the game's Start key, apart from the panel's fast forward; the school's desks on the secondary key");
        Check(!game.Contains("HomeDesk") && !director.Contains("HomeDesk") && buttons.Contains("director.WorkAtComputer(computer, late)")
              && buttons.Contains("director.ComputerWork(out canWork, out _workLate)") && buttons.Contains("public static bool Failed => _failed || FrameGuard.Off;")
              && MethodText(director, "internal string DeskFallbackWhy(Job j)").Contains("ComputerButtons.Failed"),
            "home computer: no click of the job's over it any more; a day from home begun from its own button, else from the Career app (the buttons not there or stood down)");
        Check(computer.Contains("_work = sitting.Kind == SittingKind.Work || sitting.Kind == SittingKind.Overtime;") && computer.Contains("public bool RequiresEnergy() => !_work;")
              && desk.Contains("public bool RequiresEnergy() => false;"),
            "home computer: a working day there is not ended by the player's energy running out, as at the desk; a class or homework is, as the game's studying");

        // ---------------------------------------------------------------- What's new
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("set off", langs, codes))
        {
            return;
        }
        var lines = new[]
        {
            "has a Start the working day button beside Pass Time", "Fixed: the game's panel no longer closes when you press a class's button at a desk",
            "Fixed: at a desk computer at work you now sit on its chair", "Fixed: a working day at home no longer leaves you unable to move"
        };
        var lacking = new List<string>();
        foreach (string line in lines)
        {
            bool found = false;
            foreach (KeyValuePair<string, string> row in langs[0])
            {
                found |= row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains(line);
            }
            if (!found)
            {
                lacking.Add(line);
            }
        }
        Check(lacking.Count == 0, "set off: What's new has the lines for the day from home at the computer, the class's walk, the seat and the stand-up, in some"
                                  + " reallife_changes_ row (" + string.Join("; ", lacking) + ")");
    }

    // The text of a method of a source file, from its signature to its closing brace (on a line of its own, indented eight
    // spaces, as every method of the mod's: RLO-STY-012); empty if it is not there.
    private static string MethodText(string source, string signature)
    {
        int at = source.IndexOf(signature, StringComparison.Ordinal);
        int end = at < 0 ? -1 : source.IndexOf("\n        }", at, StringComparison.Ordinal);
        return end < 0 ? string.Empty : source.Substring(at, end - at);
    }

    // A walk set off in its three steps: a frame, then on its way, a frame, then the frame noted and the walk order.
    private static bool SetsOffInSteps(string walk, string order)
    {
        int first = walk.IndexOf("yield return null;", StringComparison.Ordinal);
        int moving = walk.IndexOf("_state = PlayerActivityState.MovingTowardsActivity;", StringComparison.Ordinal);
        int second = moving < 0 ? -1 : walk.IndexOf("yield return null;", moving, StringComparison.Ordinal);
        int noted = second < 0 ? -1 : walk.IndexOf("_orderFrame = Time.frameCount;", second, StringComparison.Ordinal);
        int given = noted < 0 ? -1 : walk.IndexOf(order, noted, StringComparison.Ordinal);
        int third = second < 0 ? -1 : walk.IndexOf("yield return", second + 1, StringComparison.Ordinal);
        return first >= 0 && moving > first && second > moving && noted > second && given > noted && (third < 0 || third > given);
    }

    // The panel's Finished, in ChangeState, ends the activity its own way.
    private static bool EndsWhenFinished(string changeState, string end)
    {
        int finished = changeState.IndexOf("if (state == PlayerActivityState.Finished)", StringComparison.Ordinal);
        return finished >= 0 && changeState.IndexOf(end, finished, StringComparison.Ordinal) > finished;
    }

    // What a method does in its finally.
    private static bool FreesInFinally(string body, string release)
    {
        int at = body.IndexOf("finally", StringComparison.Ordinal);
        return at >= 0 && body.IndexOf(release, at, StringComparison.Ordinal) > at;
    }

    private static bool ComesBefore(string body, string first, string then)
    {
        int a = body.IndexOf(first, StringComparison.Ordinal), b = body.IndexOf(then, StringComparison.Ordinal);
        return a >= 0 && b > a;
    }

    private static int Occurrences(string text, string what)
    {
        int n = 0;
        for (int at = text.IndexOf(what, StringComparison.Ordinal); at >= 0; at = text.IndexOf(what, at + what.Length, StringComparison.Ordinal))
        {
            n++;
        }
        return n;
    }
}
