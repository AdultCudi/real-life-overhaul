using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Sessions;
using RealLifeOverhaul.Ui;

// The desk session's card (a player's report of 14 Nov: after "Work here" a card with its title and nothing under it, Esc the only way off, then
// the work's choice box that Esc did not close; and the status bar's clock 25 minutes behind the phone's). What it holds, offline:
//   every state of a session's pages: SessionRules.Buttons always has Cancel, the series is never empty, the first page is a page of the series,
//     a skip always moves on, and every step has at least one choice, so no page has no lines or no buttons;
//   the card's own source: it is never opened over a page without lines or buttons, it closes with the panel leaving its pages (the walk, the chair,
//     over), Esc and the pad's back always close it, an empty page has the one way out, and each of these writes a "Sessions:" line;
//   the two features at one desk (the sessions' panel and the own work): one answer, a stated choice between a contract and the firm's work;
//   the keys: every hold of the keys is released on every exit path, and a card that stepped aside for a box takes none of the box's keys;
//   the clock: one stamp for the game's minute, kept by every layout that drew the bar, the phone's time looked at every frame.
internal static partial class Program
{
    private static void SessionCard()
    {
        int before = _run, failedBefore = _failed;
        SessionCardPages();
        SessionCardSource();
        SessionCardDesk();
        SessionCardKeys();
        SessionCardClock();
        Console.WriteLine("  Session card: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    // ---------------------------------------------------------------- every state of a page

    private static void SessionCardPages()
    {
        bool cancel = true, labelled = true;
        int states = 0;
        foreach (bool can in new[] { false, true })
        {
            for (int count = 1; count <= 17; count++)
            {
                for (int hand = -1; hand <= 1; hand++)
                {
                    foreach (bool work in new[] { false, true })
                    {
                        SessionRules.PageButtons b = SessionRules.Buttons(can, count, hand, work);
                        int n = 1 + (b.Begin ? 1 : 0) + (b.Next ? 1 : 0) + (b.Hand ? 1 : 0) + (b.Work ? 1 : 0);
                        cancel &= b.Cancel && n >= 1;
                        labelled &= b.Begin == can && b.Next == (count > 1) && b.Hand == (hand != 0) && b.Work == work;
                        states++;
                    }
                }
            }
        }
        Check(cancel && labelled && states == 2 * 17 * 3 * 2,
            "session card pages: in every state of a page (can begin or not, 1 to 17 kinds, hand over, take back or none, work here or not: " + states
            + " states) it has Cancel and the buttons its state says");

        // the series the panel draws, the first page and the skip, over every premise pattern of up to six kinds
        bool series = true, first = true, skip = true;
        for (int count = 1; count <= 6; count++)
        {
            for (int mask = 0; mask < (1 << count); mask++)
            {
                var met = new bool[count];
                var can = new bool[count];
                var due = new bool[count];
                for (int i = 0; i < count; i++)
                {
                    met[i] = (mask & (1 << i)) != 0;
                    can[i] = met[i] && (i % 2 == 0);
                    due[i] = i % 3 != 1;
                }
                var into = new int[count];
                int n = SessionRules.Series(met, count, into);
                series &= n >= 1 && n <= count && into.Take(n).All(x => x >= 0 && x < count);
                int at = SessionRules.FirstPage(can.Take(n).ToArray(), due.Take(n).ToArray(), n);
                first &= at >= 0 && at < n;
                int next = at;
                var seen = new HashSet<int>();
                for (int k = 0; k < n; k++)
                {
                    next = SessionRules.NextPage(next, n);
                    seen.Add(next);
                    skip &= next >= 0 && next < n;
                }
                skip &= n < 2 ? seen.Count == 1 : seen.Count == n;
            }
        }
        Check(series && first && skip, "session card pages: with every pattern of met premises the series draws at least one kind, opens on a page of it, and the skip goes round all of them");

        // a step with no choice or more than the panel has keys for is not a whole kind: no step page can be empty
        var empty = new SessionDef { Id = "x", Types = new[] { "t" }, Apply = (a, c) => default(SessionOutcome), Steps = new[] { new SessionStep { Choices = new SessionChoice[0] } } };
        var many = new SessionDef
        {
            Id = "y", Types = new[] { "t" }, Apply = (a, c) => default(SessionOutcome),
            Steps = new[] { new SessionStep { Choices = new SessionChoice[SessionRules.StepChoicesMost + 1] } }
        };
        var one = new SessionDef { Id = "z", Types = new[] { "t" }, Apply = (a, c) => default(SessionOutcome), Steps = new[] { new SessionStep { Choices = new[] { new SessionChoice() } } } };
        Check(!SessionRules.Whole(empty) && !SessionRules.Whole(many) && SessionRules.Whole(one), "session card pages: a step with no choice is not a whole kind, so no step page lacks its buttons");

        // the pages in the source: the first page's lines (the kind's words and its status) and Cancel first are built for every state
        string panel = ModSource(@"Sessions\SessionPanel.cs"), director = ModSource(@"Sessions\SessionDirector.Desk.cs");
        string page = MethodText(panel, "private void Page()");
        Check(page.Contains("new[] { DeskGame.Label(SessionText.T(def.AboutKey), 0), DeskGame.Label(status, colour) }") && page.Contains("_buttons = new ButtonInfo[n];")
              && ComesBefore(page, "_buttons[k++] = new ButtonInfo(\"CancelRealLifeSession\"", "if (pb.Begin)") && page.Contains("_buttons = new ButtonInfo[1 + step.Choices.Length];"),
            "session card pages: the first page always has the kind's words and its status line, and Cancel first; a step page has its question and every choice");
        string status = MethodText(director, "internal string Status(SessionDef def, out int colour, out bool can, out int hand)");
        Check(status.Length > 0 && !status.Contains("return null") && !status.Contains("return string.Empty") && !status.Contains("return \"\"") && Occurrences(status, "return ") == 6,
            "session card pages: the status line says something in each of its six states (desks short, no desk of one's own, blocked, not due, due with a manager there or away or alone)");
        string open = MethodText(director, "private bool OpenPanel(EmployeeStationController desk)");
        Check(open.Contains("if (!panel.HasPage)") && ComesBefore(open, "if (!panel.HasPage)", "if (!DeskGame.Show(panel))") && ComesBefore(open, "if (_hereKinds.Count == 0)", "var met = new bool[n];")
              && Occurrences(open, "\"reallife_session_note_nothing\"") == 2,
            "session card pages: with nothing to hold, or a first page without lines or buttons, a notice says so and neither the panel nor the card opens");
        Check(panel.Contains("public bool HasPage => _state == PlayerActivityState.NotStarted && _labels != null && _labels.Length > 0 && _buttons != null && _buttons.Length > 0;"),
            "session card pages: a page is one with lines and buttons, on the page's own state");
    }

    // ---------------------------------------------------------------- the card itself

    private static void SessionCardSource()
    {
        string kit = ModSource(@"Ui\Pages\SessionKit.cs"), panel = ModSource(@"Sessions\SessionPanel.cs");
        string show = MethodText(kit, "public static void Show(IPlayerActivity panel)");
        string offers = MethodText(kit, "private static bool Offers(IPlayerActivity panel, out string why)");
        Check(show.Contains("if (!Offers(panel, out string why))") && ComesBefore(show, "if (!Offers(panel, out string why))", "UiDirector.Instance.Overlay(") && show.Contains("the card was not opened: ")
              && offers.Contains("PlayerActivityState.NotStarted") && offers.Contains("the page has no headline") && offers.Contains("the page has no lines") && offers.Contains("the page has no buttons"),
            "session card source: the card is never opened over a panel that is not on a page, or a page without a headline, lines or buttons, and the log says which");

        // every change of the panel's state goes through Move, which puts the card away
        string move = MethodText(panel, "private void Move(PlayerActivityState to)");
        Check(move.Contains("_state = to;") && move.Contains("if (to != PlayerActivityState.NotStarted && Ui.SessionKit.Up)") && move.Contains("Ui.SessionKit.Hide(this);") && move.Contains("the card is put away")
              && Regex.Matches(panel, @"\b_state = PlayerActivityState\.").Count == 1,
            "session card source: the panel's state is set in one place, and leaving the pages (the walk, the chair, over) puts the card away with a line in the log");
        string end = MethodText(panel, "private void End(bool settle, bool walking)"), walk = MethodText(panel, "private IEnumerator Walk()"), arrived = MethodText(panel, "private void Arrived()");
        Check(end.Contains("Move(PlayerActivityState.Finished);") && walk.Contains("Move(PlayerActivityState.MovingTowardsActivity);") && arrived.Contains("Move(PlayerActivityState.Started);")
              && MethodText(panel, "public void ChangeState(PlayerActivityState state)").Contains("Move(state);") && MethodText(panel, "private void Cancel()").Contains("End(false, false);"),
            "session card source: Cancel, Work here, a refused seat, a walk called off, the walk itself, the chair and the end all leave through Move, so none leaves the card up empty");

        // Esc and the pad's back
        string screen = kit.Substring(kit.IndexOf("public sealed class ActivityScreen", StringComparison.Ordinal));
        string back = MethodText(screen, "public bool Back()"), watch = MethodText(screen, "private void Watch()"), leave = MethodText(screen, "private void Leave(string why)");
        Check(back.Contains("_cancel.onClick();") && ComesBefore(back, "_cancel.onClick();", "Leave(\"Back pressed\");") && back.Contains("return true;") && !back.Contains("return Close != null"),
            "session card source: Esc and the pad's back always close the card, after the panel's own Cancel has let the panel go, and are always taken");
        Check(watch.Contains("_panel.GetState()") && watch.Contains("Leave(\"the panel is \" + state);") && watch.Contains("EmptyPollsMost") && watch.Contains("Leave(\"the panel gave no button on its page\");")
              && leave.Contains("_left = true;") && leave.Contains("Sessions.SessionDirector.Note(\"the card is closed: \" + why);") && leave.Contains("Close?.Invoke();")
              && ComesBefore(screen, "Watch();", "return _dirty;"),
            "session card source: the card closes itself, once, when the panel leaves its pages or gives no button for two looks, and says which in the log");
        string build = MethodText(screen, "private UiDecisionModel Build()");
        string press = MethodText(screen, "public bool Press(int command, int arg)");
        Check(build.Contains("if (actions.Count == 0 && choices.Count == 0)") && build.Contains("_actions.Add(null);") && build.Contains("Key = UiAction.Back") && press.Contains("_actions[arg] == null")
              && ComesBefore(press, "_actions[arg] == null", "b.onClick();") && press.Contains("Leave(\"the way out of an empty page pressed\");"),
            "session card source: a page that gives no button still shows the one way out, and pressing it closes the card");
        Check(kit.Contains("EmptyPollsMost = 2"), "session card source: a page may give no button for two looks (the moments between a press and the panel's next state), not more");
        Check(ModSource(@"Ui\UiDirector.cs").Contains("Quietly(() => SessionKit.Hide(null));"), "session card source: the guard, the unloading and the switch turned off close the card (CloseCards)");
    }

    // ---------------------------------------------------------------- the two features at one desk

    private static void SessionCardDesk()
    {
        Check(OwnPlaceRules.ChoiceAt(true, true) == DeskChoice.Ask && OwnPlaceRules.ChoiceAt(true, false) == DeskChoice.Contract
              && OwnPlaceRules.ChoiceAt(false, true) == DeskChoice.Firm && OwnPlaceRules.ChoiceAt(false, false) == DeskChoice.Firm,
            "session card desk: with a contract and the firm's work both open the player is asked which; with one, that one; with neither, the firm's reason (one stated rule, never both)");
        string panel = ModSource(@"Sessions\SessionPanel.cs"), desk = ModSource(@"Sessions\SessionDirector.Desk.cs"), own = ModSource(@"Tech\TechDirector.OwnDesk.cs");
        string work = MethodText(panel, "private void Work()");
        Check(ComesBefore(work, "SessionDirector.Note(\"work here pressed", "End(false, false);") && ComesBefore(work, "End(false, false);", "_director.WorkAfter(_station);") && work.Contains("the card "),
            "session card desk: Work here lets the session panel and its card go first, and only then offers the work, so the choice box is never answered with a card over it");
        string wait = MethodText(desk, "private IEnumerator WorkWhenFree(EmployeeStationController desk)");
        Check(wait.Contains("Note(\"work here: the game's panel \"") && wait.Contains("the card ") && wait.Contains("SessionBridges.WorkNow(desk);") && wait.Contains("yield break;")
              && ComesBefore(wait, "SessionBridges.WorkNow(desk);", "\"reallife_session_note_work_lost\""),
            "session card desk: the wait for the game's panel says in the log how long it took and what it found, and a work that cannot be offered is said in a notice, never dropped");
        string workAt = MethodText(own, "public void WorkAt(EmployeeStationController desk)");
        Check(workAt.Contains("Sessions: work here is offered") && workAt.Contains("Sessions: work here was pressed, but the work cannot be offered") && workAt.Contains("\"reallife_session_note_work_lost\"")
              && ComesBefore(workAt, "DeskClicked(desk);", "return;") && ComesBefore(workAt, "return;", "Sessions: work here was pressed, but"),
            "session card desk: the own work answers a Work here press once (the choice box when a contract rides on the desk, else its own walk) or says why not, in the log and a notice");
        // one click per desk: the sessions' desk is never also the own work's click target
        string offer = MethodText(own, "private void OfferDesk(BuildingRegistration reg)");
        Check(offer.Contains("_sharedDesk = free != null && ReferenceEquals(free, session) ? free : null;") && offer.Contains("if (free == null || _sharedDesk != null)")
              && ComesBefore(offer, "if (free == null || _sharedDesk != null)", "DeskGame.OtherTarget = free;"),
            "session card desk: at the sessions' desk the own work's click is off and only their panel offers the work; at any other free desk the own work's click is the only one");
        Check(MethodText(desk, "private void DeskClicked()").Contains("(_panel != null && _panel.Open)") && MethodText(desk, "private bool OpenPanel(EmployeeStationController desk)").Contains("DeskGame.Show(panel)")
              && ModSource(@"Careers\DeskGame.cs").Contains("if (PlayerActivityUI.IsPanelOpen)"),
            "session card desk: a click on the desk while the player is busy (a stint, a job shift, another panel) opens no second panel and says so");
    }

    // ---------------------------------------------------------------- the keys

    private static void SessionCardKeys()
    {
        string scripts = ModScripts();
        int holds = 0, releases = 0;
        var files = new List<string>();
        foreach (string file in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string text = System.IO.File.ReadAllText(file);
            int h = Occurrences(text, "Keys.Hold();"), r = Occurrences(text, "Keys.Release();");
            holds += h;
            releases += r;
            if (h > 0)
            {
                files.Add(file.Substring(scripts.Length + 1).Replace('\\', '/'));
            }
        }
        Check(holds == 1 && releases == 1 && files.SequenceEqual(new[] { "Ui/Phone/PhoneKit.cs" }),
            "session card keys: the one counted hold of the keys is the phone's, with its one release (found " + holds + " holds and " + releases + " releases in " + string.Join(", ", files) + ")");
        string phone = ModSource(@"Ui\Phone\PhoneKit.cs");
        string take = MethodText(phone, "private static void Take()"), leave = MethodText(phone, "private static void Leave()");
        Check(take.Contains("Keys.Hold();") && take.Contains("Gate.Focused = true;") && leave.Contains("if (!Gate.Focused)") && ComesBefore(leave, "Gate.Focused = false;", "Keys.Release();"),
            "session card keys: the phone releases exactly what it held, once, whichever way it leaves (Leave is a no-op unless it holds)");
        bool exits = true;
        foreach (string way in new[] { "public static void Remove(string why)", "internal static void Hidden()", "private static void Collapsed(bool collapsed)" })
        {
            exits &= MethodText(phone, way).Contains("Leave();");
        }
        Check(exits && MethodText(phone, "internal static void Frame(float now, ref float nextLook)").Contains("Leave();") && phone.Contains("PhoneKit.Leave();")
              && MethodText(phone, "public static void Fallback(Exception e)").Contains("Remove(\"a fault\");"),
            "session card keys: every exit of the phone (removed, put away, folded, a click away, the key again, a fault) gives the keys back");
        string sessions = string.Concat(new[] { @"Sessions\SessionPanel.cs", @"Sessions\SessionDirector.Desk.cs", @"Ui\Pages\SessionKit.cs" }.Select(ModSource));
        Check(!sessions.Contains("Keys.Hold") && !sessions.Contains("Keys.Suspended") && !sessions.Contains("SetNavigationBlocker(") && !sessions.Contains("SetPause(") && !sessions.Contains("DisableTimeControl("),
            "session card keys: the desk session and its card hold no keys, walk or clock of their own: the game's panel's own holds go with the panel, and the seat's through DeskGame");
        string host = ModSource(@"Ui\UiHost.cs");
        string aside = MethodText(host, "private void SteppedAside()");
        string update = MethodText(host, "private void OnUpdate()");
        Check(aside.Contains("bool aside = UiBoxes.Up;") && aside.Contains("_aside = aside;") && update.Contains("if (PointerOnly || _held || _aside)")
              && ComesBefore(update, "if (PointerOnly || _held || _aside)", "_input.TakeAll();"),
            "session card keys: a card that stepped aside for any box of the game's reads and takes no key, so Esc and Enter are the box's, and a card is never over a box");
    }

    // ---------------------------------------------------------------- the clock

    private static void SessionCardClock()
    {
        bool distinct = true;
        var seen = new HashSet<int>();
        for (int day = 67; day <= 68; day++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                for (int minute = 0; minute < 60; minute++)
                {
                    foreach (bool twelve in new[] { false, true })
                    {
                        distinct &= seen.Add(UiClockRules.Stamp(day, hour, minute, twelve));
                    }
                }
            }
        }
        Check(distinct && UiClockRules.Stamp(68, 12, 39, true) != UiClockRules.Stamp(68, 13, 4, true) && UiClockRules.Stamp(68, 12, 39, true) != UiClockRules.Stamp(68, 12, 39, false)
              && UiClockRules.Stamp(68, 12, 59, false) < UiClockRules.Stamp(68, 13, 0, false),
            "session card clock: the clock's stamp names the day, the minute and the 12 hour option, so a minute on moves it (the report's 12:39 against 01:04)");
        string bar = ModSource(@"Ui\UiDraw.Chrome.cs"), host = ModSource(@"Ui\UiHost.cs"), scene = ModSource(@"Ui\UiScene.cs");
        Check(MethodText(bar, "public static void StatusBar(UiScene s, UiFrame frame, IUiKeys keys, bool prompts)").Contains("s.ClockStamp = UiChrome.Stamp != null ? UiChrome.Stamp() : -1;") && scene.Contains("ClockStamp = -1;")
              && scene.Contains("public int ClockStamp = -1;"),
            "session card clock: a layout that draws the status bar keeps the minute it drew, and a layout without the bar keeps none");
        string moved = MethodText(host, "private bool ClockMoved(float now)");
        Check(moved.Contains("_scene.ClockStamp < 0") && moved.Contains("UiClock.Stamp() != _scene.ClockStamp") && Occurrences(host, "ClockMoved(now)") == 2,
            "session card clock: the host lays a screen that drew the bar out again when the minute moves (at most a tenth of a second late), in its normal and its held frame");
        string phone = ModSource(@"Ui\Phone\PhoneKit.cs");
        string frame = MethodText(phone, "internal static void Frame(float now, ref float nextLook)");
        Check(ComesBefore(frame, "Clock(_screen.Readings, false)", "if (now >= nextLook)") && MethodText(phone, "private static bool Clock(UiPhoneModel m, bool all)").Contains("PlannerDirector.Now()")
              && MethodText(phone, "private static void Readings(bool all)").Contains("changed |= Clock(m, all);"),
            "session card clock: the phone's time is looked at every frame it shows, not at the readings' look, so it and the bar change their minute together");
        string game = ModSource(@"Ui\UiGame.cs");
        Check(game.Contains("Timestamp now = TimeHelper.Now();") && game.Contains("UiClock.At(now.Hour, (int)now.Minute)") && ModSource(@"Planner\PlannerText.cs").Contains("return (m / 60).GetFormattedTime(m % 60);"),
            "session card clock: the bar and the phone read the game's one clock (TimeHelper) and write it in the game's own format");
    }
}
