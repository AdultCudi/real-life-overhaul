using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using IoPath = System.IO.Path;
using RealLifeOverhaul;

// A player at a desk of their own tech firm stood instead of sitting while they worked, and could not move when the work
// ended. The stint's seat (Careers/DeskGame.SitAt) let the game's desk go first with WorkstationController.UnassignEmployee,
// which throws with nobody at the desk; the work hold was already on, the stint was never marked begun, and so no end of
// it stood the player up. Now a seat is one pair, read here from the sources as the game cannot be run: SitAt and SitOn
// answer whether the player is really held, give them back free to walk themselves when not, and every caller marks its
// work begun no later than a true answer, so that each of its ends stands them up through StandFrom or StandUp.
internal static partial class Program
{
    private static void DeskSeat()
    {
        DeskSeatPose();
        DeskSeatCallers();
        DeskSeatEnds();
        DeskSeatTest();
        DeskSeatTexts();
    }

    // DeskGame: the pose and its release.
    private static void DeskSeatPose()
    {
        string game = ModSource(@"Careers\DeskGame.cs");
        string sitAt = MethodText(game, "public static bool SitAt(EmployeeStationController station)");
        string sitOn = MethodText(game, "public static bool SitOn(Transform seat, EmployeeStationController station, out Vector3 stood)");
        string standFrom = MethodText(game, "public static void StandFrom(EmployeeStationController station)");
        string standUp = MethodText(game, "public static void StandUp(Vector3 at, EmployeeStationController station)");
        string free = MethodText(game, "public static void Free(EmployeeStationController station)");
        Check(sitAt.Length > 0 && sitOn.Length > 0 && standFrom.Length > 0 && standUp.Length > 0 && free.Length > 0,
            "desk seat: the two poses answer whether the player is held (SitAt and SitOn return bool), with their two releases");

        // The fault itself: the game's desk let go with nobody at it.
        Check(sitAt.Contains("Free(station);") && !sitAt.Contains(".UnassignEmployee()") && free.Contains("!station.employee.isPlayer") && free.Contains("station.UnassignEmployee();"),
            "desk seat: the seat sends the game's person away only when there is one (Free), never the bare UnassignEmployee a desk's own throws in with nobody at it");
        Check(Occurs(game, ".UnassignEmployee()") == 2 && standFrom.Contains("if (IsPlayers(station))") && ComesBefore(standFrom, "if (IsPlayers(station))", "station.UnassignEmployee();"),
            "desk seat: a station is let go in two places only: the game's person in Free, the player in StandFrom while they are its employee");

        // A seat that is not held gives the player back itself, and says so.
        bool sitAtBack = ComesBefore(sitAt, "player.SetNavigationBlocker(NavigationBlocker.WorkActivity);", "try") && sitAt.Contains("if (Holds(station, player))")
                         && ComesBefore(sitAt, "catch (Exception e)", "StandFrom(station);") && ComesBefore(sitAt, "StandFrom(station);", "Refused();")
                         && sitAt.LastIndexOf("return false;", StringComparison.Ordinal) > sitAt.IndexOf("Refused();", StringComparison.Ordinal);
        bool sitOnBack = ComesBefore(sitOn, "player.SetNavigationBlocker(NavigationBlocker.WorkActivity);", "try") && sitOn.Contains("if (player.Character.isSittingOn == seat)")
                         && ComesBefore(sitOn, "catch (Exception e)", "StandUp(at, station);") && ComesBefore(sitOn, "StandUp(at, station);", "Refused();")
                         && sitOn.LastIndexOf("return false;", StringComparison.Ordinal) > sitOn.IndexOf("Refused();", StringComparison.Ordinal);
        string holds = MemberText(game, "private static bool Holds(EmployeeStationController station, PlayerController player)");
        Check(sitAtBack && sitOnBack && holds.Contains("IsPlayers(station)") && holds.Contains("station is DesktopWorkstationController") && holds.Contains("isSittingOn != null"),
            "desk seat: a seat the game would not give (not its employee, not on the desk's chair, a fault) gives the player back free to walk at once and says why; nothing is left held");
        Check(MethodText(game, "private static void Refused()").Contains("Notifications.ShowError(SeatRefusedKey, SeatRefusedKey);")
              && game.Contains("public const string SeatRefusedKey = \"reallife_desk_seat_refused\";"),
            "desk seat: the refusal is the game's own notice, once on screen however often it comes");

        // The releases: each step in its own try, the hold off in a finally, safe twice.
        Check(Occurs(standFrom, "try") == 2 && Occurs(standUp, "try") == 2 && FreesInFinally(standFrom, "Unhold(player);") && FreesInFinally(standUp, "Unhold(player);")
              && standFrom.Contains("player.Character.Reset();") && standUp.Contains("player.Character.Reset();"),
            "desk seat: a fault letting the station go never skips the pose's end, and the work hold comes off whatever fails");
        Check(standFrom.Contains("if (station != null && !Taken(station))") && standUp.Contains("if (station != null && !Taken(station))")
              && MethodText(game, "private static void Unhold(PlayerController player)").Contains("if (player != null && WorkHeld(player))")
              && game.Contains("GameContract.NavBlockers.Field"),
            "desk seat: standing up twice, or from a station the game has seated somebody at since, changes nothing of theirs and takes off only a hold that is on");
        string seen = MethodText(game, "private static void StoodSeen(PlayerController player, string what)");
        Check(seen.Contains("Meta.Dev.Verbose") && seen.Contains("\"Desk check (seat): stood up from the \"") && seen.Contains("PASS") && seen.Contains("FAIL")
              && standFrom.Contains("StoodSeen(player,") && standUp.Contains("StoodSeen(player,"),
            "desk seat: with Developer diagnostics on, every stand-up writes one Desk check (seat) line, free to walk or what still holds the player");
        string walks = MemberText(game, "private static bool Walks(PlayerController player, out string why)");
        Check(walks.Contains("NavigationBlocker.WorkActivity") && walks.Contains("navmeshAgent.enabled") && walks.Contains("isSittingOn != null") && walks.Contains("isKinematic"),
            "desk seat: free to walk means no work hold, the navigation agent on, not seated and not held still");
    }

    // Every caller of a seat, anywhere in the mod: the answer read, a release in the same file, and the list known here.
    private static void DeskSeatCallers()
    {
        string scripts = IoPath.GetFullPath(IoPath.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        var callers = new List<string>();
        var bare = new List<string>();
        var unreleased = new List<string>();
        var sit = new Regex(@"DeskGame\.Sit(At|On)\(");
        var statement = new Regex(@"^\s*DeskGame\.Sit(At|On)\(.*\);", RegexOptions.Multiline);
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string code = File.ReadAllText(path);
            if (!sit.IsMatch(code))
            {
                continue;
            }
            string name = IoPath.GetFileName(path);
            callers.Add(name);
            if (statement.IsMatch(code))
            {
                bare.Add(name);
            }
            if (!code.Contains("DeskGame.StandFrom(") && !code.Contains("DeskGame.StandUp("))
            {
                unreleased.Add(name);
            }
        }
        callers.Sort(StringComparer.Ordinal);
        string known = "DeskWork.cs, OwnBankDirector.Branches.cs, SessionPanel.cs, TechDirector.DeskSeat.cs, TechDirector.OwnDesk.cs";
        Check(string.Join(", ", callers) == known,
            "desk seat: the callers of a seat are the job's desk, the bank counter, a desk session at the owner's desk, the own firm's stint and its test; a new one is checked for its ends here first ("
            + string.Join(", ", callers) + ")");
        Check(bare.Count == 0, "desk seat: no caller drops a seat's answer: a seat refused ends the work at once (" + string.Join(", ", bare) + ")");
        Check(unreleased.Count == 0, "desk seat: every file that seats the player also stands them up (" + string.Join(", ", unreleased) + ")");
    }

    // Each caller: begun no later than the seat holds, refused ends at once, and every end through the release.
    private static void DeskSeatEnds()
    {
        // The own firm's stint (the report's desk).
        string own = ModSource(@"Tech\TechDirector.OwnDesk.cs");
        string started = MethodText(own, "private void StintStarted(OwnStint stint)");
        int refused = started.IndexOf("if (!DeskGame.SitAt(stint.Desk))", StringComparison.Ordinal);
        Check(ComesBefore(started, "stint.Begun = true;", "if (!DeskGame.SitAt(stint.Desk))") && refused > 0
              && started.IndexOf("EndStint(stint, false);", refused, StringComparison.Ordinal) > refused && started.IndexOf("stint.Skip?.Close();", refused, StringComparison.Ordinal) > refused
              && ComesBefore(started, "if (!DeskGame.SitAt(stint.Desk))", "FirmsGame.Tiring(stint.Desk, true);"),
            "desk seat: the own desk's stint is begun before the seat, so every end stands the player up; a seat refused closes the panel and ends it at once, before any energy is spent");
        string end = MethodText(own, "private void EndStint(OwnStint stint, bool told)");
        Check(ComesBefore(end, "if (!stint.Begun)", "DeskGame.StandFrom(desk);") && !end.Contains("PlayerAt(desk) ? desk : null"),
            "desk seat: the stint's end stands the player up whenever it began, the desk let go only while they are at it (StandFrom decides)");
        string watch = MethodText(own, "private void WatchStint(BuildingRegistration reg, string place)");
        Check(Occurs(watch, "EndStint(stint, false);") == 2 && MethodText(own, "private void DeskDown()").Contains("EndStint(stint, false);")
              && own.Contains("_director.EndStint(this, true);"),
            "desk seat: every end of the stint goes through EndStint: Stop, the time up, the skip and fast forward (SkipEnded), the panel letting it go,"
            + " leaving the firm, the desk's employee arriving, the firms standing down");

        // The bank counter.
        string bank = ModSource(@"Economy\OwnBank\Branch\OwnBankDirector.Branches.cs");
        string bankStart = MethodText(bank, "void ISkippableShift.SkipStarted()");
        int bankRefused = bankStart.IndexOf("if (!DeskGame.SitAt(_counterTill))", StringComparison.Ordinal);
        Check(ComesBefore(bankStart, "_counterPlace = BranchGame.Place(reg);", "if (!DeskGame.SitAt(_counterTill))") && bankRefused > 0
              && bankStart.IndexOf("EndCounter(false);", bankRefused, StringComparison.Ordinal) > bankRefused,
            "desk seat: the bank counter's stint is begun before the seat, and a seat refused ends it at once");
        string counterEnd = MethodText(bank, "private void EndCounter(bool told)");
        Check(ComesBefore(counterEnd, "if (place == null)", "DeskGame.StandFrom(till);") && !counterEnd.Contains("if (BranchGame.PlayerAt(till))"),
            "desk seat: the counter's end stands the player up whenever it began, also when the game let the till go first (it was skipped then, and the hold stayed on)");

        // The job's desk.
        string desk = ModSource(@"Careers\DeskWork.cs");
        string arrived = MethodText(desk, "private void Arrived()");
        int deskRefused = arrived.IndexOf("if (!(seated ? DeskGame.SitOn(seat, _station, out _stood) : DeskGame.SitAt(_station)))", StringComparison.Ordinal);
        Check(deskRefused > 0 && arrived.IndexOf("End(true, false);", deskRefused, StringComparison.Ordinal) > deskRefused
              && ComesBefore(arrived, "_seated = seated;", "if (!(seated ?") && ComesBefore(arrived, "if (!(seated ?", "_state = PlayerActivityState.Started;")
              && ComesBefore(arrived, "_state = PlayerActivityState.Started;", "_sitting.SkipStarted();"),
            "desk seat: a job's desk is begun the moment the seat holds (End stands the player up from then on), and a seat refused lets the day's sitting go with nothing counted");
        string deskEnd = MethodText(desk, "private void End(bool settle, bool walking)");
        Check(deskEnd.Contains("else if (begun && _seated)") && deskEnd.Contains("DeskGame.StandUp(") && deskEnd.Contains("else if (begun)") && deskEnd.Contains("DeskGame.StandFrom(_station);")
              && MethodText(desk, "public void Finish()").Contains("End(true, true);") && MethodText(desk, "public void Close()").Contains("End(false, true);"),
            "desk seat: a job's desk stands the player up through its one End however it ends: Stop, the time up, the game's panel, the job closing");

        // A desk session at the owner's desk (Living population: the team meeting, the studio's and the other firms' sessions, Flare's content plan).
        string session = ModSource(@"Sessions\SessionPanel.cs");
        string sat = MethodText(session, "private void Arrived()");
        int sessionRefused = sat.IndexOf("if (!DeskGame.SitOn(seat, _station, out _stood))", StringComparison.Ordinal);
        Check(sessionRefused > 0 && sat.IndexOf("End(false, false);", sessionRefused, StringComparison.Ordinal) > sessionRefused
              && ComesBefore(sat, "_seated = true;", "if (!DeskGame.SitOn(") && ComesBefore(sat, "if (!DeskGame.SitOn(", "_state = PlayerActivityState.Started;")
              && ComesBefore(sat, "_state = PlayerActivityState.Started;", "_director.Sat(Def);"),
            "desk seat: a desk session is begun the moment the seat holds (End stands the player up from then on), and a seat refused ends it with nothing spent");
        string sessionEnd = MethodText(session, "private void End(bool settle, bool walking)");
        Check(sessionEnd.Contains("else if (begun && _seated)") && sessionEnd.Contains("DeskGame.StandUp(_stood, _station);")
              && MethodText(session, "public void Finish()").Contains("End(true, true);") && MethodText(session, "public void Close()").Contains("End(false, true);"),
            "desk seat: a desk session stands the player up through its one End however it ends: Stop, the time up, the game's panel, the sessions stopping");

        // A job's start that throws half way.
        string skip = ModSource(@"Jobs\ShiftSkip.cs");
        string begin = MethodText(skip, "private IEnumerator Begin()");
        int caught = begin.IndexOf("catch (Exception e)", StringComparison.Ordinal);
        Check(caught > 0 && begin.IndexOf("Finish();", caught, StringComparison.Ordinal) > caught && MethodText(skip, "public void Finish()").Contains("shift?.SkipEnded("),
            "desk seat: a job whose start throws on the game's work panel is ended at once through its own end, never left running with the player held");
    }

    // Options > Tests: the nearest desk, a game minute, the release, the line.
    private static void DeskSeatTest()
    {
        string test = ModSource(@"Tech\TechDirector.DeskSeat.cs");
        string press = MethodText(test, "public void TestDeskSeat()");
        string run = MethodText(test, "private IEnumerator SeatTest(EmployeeStationController desk)");
        Check(press.Contains("FirmsGame.InsideOwn()") && press.Contains("FirmsGame.NearestFree(_seatDesks)") && press.Contains("\"reallife_tech_test_desk_seat_none\"")
              && press.Contains("TestRefusal.ScreenBusy") && press.Contains("TestRefusal.NotLoaded") && press.Contains("StartCoroutine(Guard.Routine(SeatTest(desk)));"),
            "desk seat test: it seats the player at the nearest free desk of the business of theirs they stand in, and says why not otherwise");
        Check(run.Contains("DeskGame.SitAt(desk) && DeskGame.SeatedAt(desk)") && run.Contains("FirmsGame.Clock - from < SeatTestMinutes") && run.Contains("Time.unscaledTime < until")
              && ComesBefore(run, "FirmsGame.Clock - from < SeatTestMinutes", "DeskGame.StandFrom(desk);") && ComesBefore(run, "DeskGame.StandFrom(desk);", "DeskGame.Walks(out why)")
              && run.Contains("\"Desk check (seat): the player seated at the \"") && run.Contains("PASS") && run.Contains("FAIL")
              && run.Contains("\"reallife_tech_test_desk_seat_pass\"") && run.Contains("\"reallife_tech_test_desk_seat_fail\""),
            "desk seat test: a game minute at the desk (two real minutes at most), stood up by the one release, then a Desk check (seat) line and a notice: PASS only when the player walks again");
        Check(test.Contains("private const int SeatTestMinutes = 1;") && MethodText(ModSource(@"Tech\TechDirector.OwnDesk.cs"), "private void DeskDown()").Contains("SeatTestDown();")
              && MethodText(test, "private void SeatTestDown()").Contains("DeskGame.StandFrom(desk);"),
            "desk seat test: the firms standing down while it holds the player stand them up at once");
        Check(ModSource(@"Core\RealLifeOverhaulMod.cs").Contains(".AddButton(\"reallife_tech_test_desk_seat\", () => Tech.TechDirector.Instance?.TestDeskSeat())")
              && ModSource(@"Meta\FeatureList.Economy.cs").Contains("\"reallife_tech_test_own_desk\", \"reallife_tech_test_desk_seat\")"),
            "desk seat test: it is in Options > Tests, on the tech sector's list");
    }

    private static void DeskSeatTexts()
    {
        string[] keys = { "reallife_desk_seat_refused", "reallife_tech_test_desk_seat", "reallife_tech_test_desk_seat_none", "reallife_tech_test_desk_seat_pass", "reallife_tech_test_desk_seat_fail" };
        Check(AllIn(keys, out string missing), "desk seat texts: every key in en, pt and zh-cn (" + missing + ")");
        Dictionary<string, string> en = LocaleFile("en");
        Check(Fits(en, "reallife_desk_seat_refused") && Fits(en, "reallife_tech_test_desk_seat_none") && Fits(en, "reallife_tech_test_desk_seat_pass") && Fits(en, "reallife_tech_test_desk_seat_fail"),
            "desk seat texts: the notices are 240 characters or fewer");
        Check(en.TryGetValue("reallife_tech_test_desk_seat_fail", out string fail) && fail.Contains("Desk check (seat)"), "desk seat texts: a failed test names the log line that says why");
        Check(FixLineEverywhere("working at a desk of your own tech firm now seats you on its chair", "trabalhar numa mesa da sua própria empresa de tecnologia agora faz você sentar",
                                "在你自己的科技公司的办公桌前工作时，现在会坐到它的椅子上",
                                "An einem Schreibtisch deiner eigenen Tech-Firma setzt du dich jetzt beim Arbeiten auf seinen Stuhl",
                                "travailler à un bureau de votre propre société tech vous assoit maintenant sur sa chaise",
                                "trabajar en un escritorio de tu propia empresa tecnológica ahora te sienta en su silla",
                                "за столом в вашей собственной технологической фирме вы теперь садитесь на его стул",
                                "werken aan een bureau van je eigen techbedrijf zet je nu op de stoel ervan",
                                "kendi teknoloji firmanızdaki bir masada çalışmak artık sizi masanın sandalyesine oturtuyor"),
            "desk seat texts: What's new has the line, with its fix mark, in some row of every language");
        bool later = true;
        var later2 = new Dictionary<string, string> { { "pl", "Naprawiono: praca przy biurku w twojej własnej firmie" }, { "ko", "수정: 내 테크 회사의 책상에서" } };
        foreach (KeyValuePair<string, string> lang in later2)
        {
            Dictionary<string, string> words = LoadWords(IoPath.Combine(LocalesFolder(), lang.Key + ".json"));
            later &= words != null && WhatsNewHas(words, lang.Value);
        }
        Check(later, "desk seat texts: What's new has the line in Polish and Korean too, in some row");
    }
}
