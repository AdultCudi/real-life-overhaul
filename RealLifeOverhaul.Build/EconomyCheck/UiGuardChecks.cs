using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Meta;
using RealLifeOverhaul.Ui;

// A part of the mod switched off by the guard, and what it leaves behind (a player's report of 13 Nov 2026 on 1.0.4): the RLO
// interface "ran into the same trouble again and again" and was switched off, and Report a bug then opened its card on the
// interface that had stopped. The card was laid out once and never answered (its host's Update slept, its presses were
// refused, its opening never faded in), and its clear picture over the whole screen took every click until the game was closed.
// Held here: a fault's words for one log line (Core/FaultWords); the register of what a part can hold over the screen, which the
// guard lets go of as it switches a part off (Core/ScreenHolds); the pieces of the interface's second, each with a breaker of its
// own, so one screen's fault is never the whole interface's (Ui/UiPieces); how the bug report asks for its words whatever is
// off; the sources that carry all of it out; every hold the mod can raise, listed from the sources so a new one cannot be
// forgotten; and the texts.
internal static partial class Program
{
    private static void UiGuard()
    {
        UiGuardWords();
        UiGuardHolds();
        UiGuardPieces();
        UiGuardReport();
        UiGuardSources();
        UiGuardTest();
        UiGuardRoots();
        UiGuardLook();
        UiGuardEveryHold();
        UiGuardTexts();
    }

    // ------------------------------------------------------------------------------------------------- a fault's words

    private static void UiGuardWords()
    {
        Check(FaultWords.Frame("  at RealLifeOverhaul.Ui.HudSides.OthersLook () [0x00012] in <d44f62f1b022416c95582d63b1721d0c>:0 ") == "HudSides.OthersLook",
            "fault words: a frame of the game's runtime is named by its class and method");
        Check(FaultWords.Frame("  at RealLifeOverhaul.Ui.UiDirector+<Tick>d__12.MoveNext () [0x00056] in <x>:0") == "UiDirector.Tick",
            "fault words: a coroutine's step is named by the method it was written in");
        Check(FaultWords.Frame("  at RealLifeOverhaul.Ui.PhoneKit+<>c.<Build>b__0_0 () [0x00000] in <x>:0") == "PhoneKit.Build",
            "fault words: a lambda is named by the method it was written in");
        Check(FaultWords.Frame("  at System.Collections.Generic.Dictionary`2[TKey,TValue].FindEntry (TKey key) [0x00008] in <1071>:0") == "Dictionary.FindEntry"
              && FaultWords.Frame("  at System.Collections.Generic.Dictionary`2[System.String,System.Int32].get_Item (System.String key) [0x0] in <1071>:0") == "Dictionary.get_Item",
            "fault words: a class with type arguments is named without them, whatever dots their names hold");
        Check(FaultWords.Frame("  at RealLifeOverhaul.Ui.UiView..ctor (UnityEngine.Transform parent, System.String name) [0x00000] in <x>:0") == "UiView.(made)",
            "fault words: a constructor is named as the making of its class");
        Check(FaultWords.Frame("   at RealLifeOverhaul.FaultWords.Of(Exception e) in D:\\x\\FaultWords.cs:line 12") == "FaultWords.Of",
            "fault words: a frame as the checker's own runtime writes it reads the same");
        Check(FaultWords.Frame("  at (wrapper managed-to-native) UnityEngine.Transform.get_childCount(UnityEngine.Transform)") == null && FaultWords.Frame("   ") == null
              && FaultWords.Frame(null) == null && FaultWords.Frame("UnityEngine.Debug:LogException(Exception)") == null,
            "fault words: a wrapper's line, a blank one and a line that is no frame name nothing");
        const string stack = "  at UnityEngine.Transform.get_childCount () [0x0] in <u>:0 \n  at RealLifeOverhaul.Ui.HudSides.Shape (UnityEngine.Transform under, System.Int32 depth, System.Int32 hash) [0x0] in <m>:0 \n"
                             + "  at RealLifeOverhaul.Ui.HudSides.OthersLook () [0x0] in <m>:0 \n  at RealLifeOverhaul.Ui.HudSides.BuildingPanel (RealLifeOverhaul.Ui.UiSidePanelModel m) [0x0] in <m>:0 \n"
                             + "  at RealLifeOverhaul.Ui.HudSides.Look (RealLifeOverhaul.Ui.UiHudModel model) [0x0] in <m>:0 \n  at RealLifeOverhaul.Ui.HudLive.Tick () [0x0] in <m>:0 ";
        Check(FaultWords.Where(stack, FaultWords.Frames) == "Transform.get_childCount < HudSides.Shape < HudSides.OthersLook < HudSides.BuildingPanel",
            "fault words: the first " + FaultWords.Frames + " methods of a stack are named, the one that threw first (" + FaultWords.Where(stack, FaultWords.Frames) + ")");
        Check(FaultWords.Where(stack, 1) == "Transform.get_childCount" && FaultWords.Where(stack, 0).Length == 0 && FaultWords.Where(null, 4).Length == 0 && FaultWords.Where("no frames here", 4).Length == 0,
            "fault words: as many methods as asked, and none from a stack that has none");
        string line = FaultWords.Line("NullReferenceException", "Object reference not set to an instance of an object", stack, 2);
        Check(line == "NullReferenceException: Object reference not set to an instance of an object, at Transform.get_childCount < HudSides.Shape",
            "fault words: the line is the fault's kind, its message and its first methods (" + line + ")");
        string longLine = FaultWords.Line("IOException", new string('x', 400) + "\nsecond line", null, 4);
        Check(longLine.Length <= "IOException: ".Length + FaultWords.MessageMost + 3 && longLine.EndsWith("...", StringComparison.Ordinal) && !longLine.Contains('\n')
              && FaultWords.Line(null, null, null, 4) == "Exception" && FaultWords.Short("a\r\nb") == "a b",
            "fault words: a long message is cut to " + FaultWords.MessageMost + " characters on one line, and a fault with no words is still named");
        Exception thrown = null;
        try
        {
            UiGuardThrows();
        }
        catch (Exception e)
        {
            thrown = e;
        }
        string of = FaultWords.Of(thrown);
        Check(of.StartsWith("InvalidOperationException: the check's own fault, at Program.UiGuardThrows < Program.UiGuardWords", StringComparison.Ordinal),
            "fault words: a fault thrown here is said with this checker's own methods (" + of + ")");
        var wrapped = new TypeInitializationException("Some.Type", thrown);
        Check(FaultWords.Of(wrapped).StartsWith("InvalidOperationException: the check's own fault, at Program.UiGuardThrows", StringComparison.Ordinal) && FaultWords.Of(null) == "no fault given",
            "fault words: a wrapper with no stack of its own is said by the fault inside it, and no fault at all in words");
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void UiGuardThrows()
    {
        throw new InvalidOperationException("the check's own fault");
    }

    // ------------------------------------------------------------------------------------------------- what a part holds

    private static void UiGuardHolds()
    {
        ScreenHolds.ForgetForTests();
        bool card = true, keys = true, other = true, quiet = false;
        int cardLet = 0, keysLet = 0, otherLet = 0, quietLet = 0;
        ScreenHolds.Add("ui", "a card", () => { card = false; cardLet++; }, () => card);
        ScreenHolds.Add("ui", "a card that cannot be closed", () => throw new InvalidOperationException("it is stuck"), () => true);
        ScreenHolds.Add("ui", "the keys", () => { keys = false; keysLet++; });
        ScreenHolds.Add("ui", "nothing held now", () => quietLet++, () => quiet);
        ScreenHolds.Add("camera", "the cursor", () => { other = false; otherLet++; }, () => other);
        Check(ScreenHolds.List.Count == 5 && ScreenHolds.Any("ui") && ScreenHolds.Any("camera") && !ScreenHolds.Any("law") && !ScreenHolds.Any(null),
            "screen holds: each hold is registered under its part");
        var released = new List<string>();
        var failed = new List<string>();
        int asked = ScreenHolds.Release("ui", released, (what, e) => failed.Add(what + ": " + e.Message));
        Check(asked == 4 && !card && !keys && cardLet == 1 && keysLet == 1 && quietLet == 1, "screen holds: a part switched off lets go of every hold of its own, the one after a hold that failed too");
        Check(other && otherLet == 0, "screen holds: and of no other part's");
        Check(released.SequenceEqual(new[] { "a card", "the keys" }) && failed.Count == 1 && failed[0] == "a card that cannot be closed: it is stuck",
            "screen holds: the line names what was held, and a hold that could not be let go of is said by itself (" + string.Join("; ", released) + " | " + string.Join("; ", failed) + ")");
        released.Clear();
        failed.Clear();
        ScreenHolds.Release("ui", released, (what, e) => failed.Add(what));
        Check(cardLet == 2 && released.SequenceEqual(new[] { "the keys" }) && failed.Count == 1,
            "screen holds: letting go again is safe: every way is asked again, and only a hold that cannot say whether it is held is named");
        Check(ScreenHolds.Release("law", released, null) == 0 && ScreenHolds.Release(null, null, null) == 0 && ScreenHolds.Release("camera", null, null) == 1 && !other,
            "screen holds: a part with no hold lets go of nothing, and no list or listener is needed");
        int again = 0;
        ScreenHolds.Add("ui", "a card", () => again++, null);
        ScreenHolds.Add(null, "x", () => { }, null);
        ScreenHolds.Add("ui", null, () => { }, null);
        ScreenHolds.Add("ui", "no way", null, null);
        ScreenHolds.Release("ui", null, null);
        Check(ScreenHolds.List.Count == 5 && again == 1 && cardLet == 2, "screen holds: the same hold registered again takes the place of the first, and a hold with no part, words or way is not taken");
        ScreenHolds.ForgetForTests();
    }

    // ------------------------------------------------------------------------------------------------- the second's pieces

    private static void UiGuardPieces()
    {
        UiPieces.ForgetForTests();
        int phone = UiPieces.Id("the phone", "label_phone"), hud = UiPieces.Id("the status bar"), fonts = UiPieces.Id("the fonts");
        Check(phone == 0 && hud == 1 && fonts == 2 && UiPieces.Id("the phone") == phone && UiPieces.Count == 3 && UiPieces.Name(hud) == "the status bar" && UiPieces.Label(phone) == "label_phone"
              && UiPieces.Label(hud) == null && UiPieces.Name(99) == "a piece" && UiPieces.Id(null) == -1,
            "ui pieces: a piece has a number, its name for the log and the key of its name for a notice");
        Check(!UiPieces.Off(phone) && UiPieces.OffCount == 0 && UiPieces.OffNames() == "none" && !UiPieces.Off(-1) && !UiPieces.Trip(-1) && !UiPieces.Trip(99), "ui pieces: every piece is on at first, and a number that is none is nothing");
        Check(UiPieces.Trip(hud) && UiPieces.Off(hud) && !UiPieces.Off(phone) && !UiPieces.Off(fonts) && UiPieces.OffCount == 1 && UiPieces.OffNames() == "the status bar" && !UiPieces.Spent(hud),
            "ui pieces: a piece that failed is off, and every other piece goes on");
        Check(!UiPieces.Trip(hud) && UiPieces.FaultsOf(hud) == 2 && UiPieces.Spent(hud), "ui pieces: a second fault while it is off is counted and not said again");
        UiPieces.ForgetForTests();
        phone = UiPieces.Id("the phone");
        hud = UiPieces.Id("the status bar");
        UiPieces.Trip(hud);
        Check(UiPieces.Rearm() == 1 && !UiPieces.Off(hud) && UiPieces.FaultsOf(hud) == 1, "ui pieces: in the next city a piece that failed once is asked again");
        UiPieces.Trip(hud);
        Check(UiPieces.Spent(hud) && UiPieces.Rearm() == 0 && UiPieces.Off(hud) && !UiPieces.Off(phone), "ui pieces: after its second fault it stays off until the game is started again (" + UiPieces.Tries + " tries)");

        // The second's look as it runs: thirty pieces, one that throws at every look. As it was (one try round all of them) the
        // whole look failed each second and the guard switched the whole interface off at the fifth; now the piece alone goes.
        UiPieces.ForgetForTests();
        const int pieces = 30, bad = 17, looks = 120;
        var ids = new int[pieces];
        var calls = new int[pieces];
        for (int i = 0; i < pieces; i++)
        {
            ids[i] = UiPieces.Id("piece " + i);
        }
        var whole = new FeatureGuard("ui", "UiDirector");
        int wholeFaults = 0, saidOnce = 0;
        for (int look = 0; look < looks; look++)
        {
            if (look == 60)
            {
                UiPieces.Rearm();   // a new city half way
            }
            try
            {
                for (int i = 0; i < pieces; i++)
                {
                    if (UiPieces.Off(ids[i]))
                    {
                        continue;
                    }
                    try
                    {
                        calls[i]++;
                        if (i == bad)
                        {
                            throw new InvalidOperationException("this piece throws at every look");
                        }
                    }
                    catch (Exception)
                    {
                        saidOnce += UiPieces.Trip(ids[i]) ? 1 : 0;
                    }
                }
            }
            catch (Exception)
            {
                wholeFaults++;
                whole.Count(look);
            }
        }
        bool othersRan = true;
        for (int i = 0; i < pieces; i++)
        {
            othersRan &= i == bad || calls[i] == looks;
        }
        Check(calls[bad] == UiPieces.Tries && saidOnce == UiPieces.Tries && othersRan && wholeFaults == 0 && !whole.Off && UiPieces.OffCount == 1,
            "ui pieces: a piece that throws at every look is asked " + UiPieces.Tries + " times in a session and said each time, every other piece runs at every look, and the interface stays on ("
            + calls[bad] + " calls, " + wholeFaults + " faults of the look)");
        var asItWas = new FeatureGuard("ui", "UiDirector");
        int offAt = -1;
        for (int look = 0; look < looks && offAt < 0; look++)
        {
            try
            {
                for (int i = 0; i < pieces; i++)
                {
                    if (i == bad)
                    {
                        throw new InvalidOperationException("this piece throws at every look");
                    }
                }
            }
            catch (Exception)
            {
                offAt = asItWas.Count(look) ? look : -1;   // a look a second
            }
        }
        Check(offAt == FeatureGuard.Most - 1 && asItWas.Off, "ui pieces: as it was, with one try round the whole look, the same piece switched the whole interface off at the fifth second (look " + offAt + ")");
        UiPieces.ForgetForTests();
    }

    // ------------------------------------------------------------------------------------------------- the bug report's words

    // The card as the report sees it: up or not, registered as a hold of the interface's part, closed by its own buttons or from outside.
    private sealed class PretendCard
    {
        public bool Up;
        public int Closed;
        public bool HostRuns = true;     // false: the kit's hosts cannot run (the part stopped)

        public bool TryShow(bool wanted, bool stopped, bool cardSwitch, bool fonts)
        {
            if (!BugReportRules.CardAsks(wanted && !stopped, cardSwitch, fonts))
            {
                return false;
            }
            Up = HostRuns;
            if (!Up)
            {
                Close();
            }
            return Up;
        }

        public void Close()
        {
            Up = false;
            Closed++;
        }
    }

    private static void UiGuardReport()
    {
        Check(BugReportRules.CardAsks(true, true, true) && !BugReportRules.CardAsks(false, true, true) && !BugReportRules.CardAsks(true, false, true) && !BugReportRules.CardAsks(true, true, false),
            "bug report: the card asks only while the interface is drawn, its switch is on and its fonts are in");
        Check(BugReportRules.AskWay(true, true) == BugReportRules.Ask.Card && BugReportRules.AskWay(false, true) == BugReportRules.Ask.Box && BugReportRules.AskWay(false, false) == BugReportRules.Ask.None
              && BugReportRules.AskWay(true, false) == BugReportRules.Ask.Card,
            "bug report: the card where it came up, else the game's own text box, else no words: one of the three always");

        // Every state of the interface, in a city and in the main menu: the report is always asked for or made, and nothing is left up.
        int states = 0, blocked = 0, lost = 0, wrongWay = 0;
        foreach (bool wanted in new[] { false, true })
        {
            foreach (bool stopped in new[] { false, true })
            {
                foreach (bool hostRuns in new[] { false, true })
                {
                    foreach (bool city in new[] { false, true })
                    {
                        ScreenHolds.ForgetForTests();
                        var card = new PretendCard { HostRuns = hostRuns };
                        ScreenHolds.Add("ui", "the bug report's card", card.Close, () => card.Up);
                        bool up = card.TryShow(wanted, stopped, true, true);
                        BugReportRules.Ask way = BugReportRules.AskWay(up, city);
                        states++;
                        wrongWay += way == BugReportRules.Ask.Card && (!wanted || stopped || !hostRuns) ? 1 : 0;   // a card on an interface that is off or stopped
                        lost += way == BugReportRules.Ask.None && city ? 1 : 0;                                    // in a city the game's box is always there
                        blocked += card.Up && way != BugReportRules.Ask.Card ? 1 : 0;                              // a card left up while another way asks
                        // The part is switched off while the card is up: it goes, and the next press takes another way.
                        var released = new List<string>();
                        ScreenHolds.Release("ui", released, null);
                        blocked += card.Up ? 1 : 0;
                        wrongWay += up && released.Count != 1 ? 1 : 0;
                        bool again = card.TryShow(wanted, true, true, true);
                        wrongWay += again || BugReportRules.AskWay(again, city) == BugReportRules.Ask.Card ? 1 : 0;
                    }
                }
            }
        }
        Check(states == 16 && blocked == 0 && lost == 0 && wrongWay == 0,
            "bug report: with the interface off, on, stopped by the guard or its hosts not running, in a city or the menu, the report is asked for one way, no card is left up, and a card up when the part is "
            + "switched off is closed with it (" + states + " states, " + blocked + " left up, " + lost + " lost, " + wrongWay + " wrong)");
        ScreenHolds.ForgetForTests();

        string box = ModSource(@"Ui\Settings\BugReportBox.cs"), report = ModSource(@"Meta\BugReport.cs"), game = ModSource(@"Meta\BugReportGame.cs");
        string show = MemberText(box, "public static bool TryShow(string where, long bytes)");
        Check(show.Contains("if (!BugReportRules.CardAsks(UiDirector.On, UiScreenSwitches.On(BugReportScreen.ScreenId), UiFonts.Ready))")
              && StatementsInOrder(show, "_host.Show();", "if (_host.Up)", "return true;", "Close();", "return false;") && StatementsInOrder(show, "catch (Exception e)", "Close();", "return false;"),
            "bug report sources: the card is asked for only while the interface is drawn, and one whose host does not run is closed at once and the game's box asked instead");
        string close = MemberText(box, "internal static void Close()");
        Check(close.Contains("_host.Hide();") && close.Contains("_root.SetActive(false);") && box.Contains("_host.Failed = e => Close();") && box.Contains("_screen.Closing = Close;"),
            "bug report sources: the card closes by its own buttons, on a fault of its screen, and from outside");
        string ask = MemberText(report, "private static void Ask()");
        Check(StatementsInOrder(ask, "if (Ui.BugReportBox.TryShow(BugReportGame.Place(), BugReportGame.LogBytes()))", "return;", "asked = BugReportGame.AskWords(Make);", "if (!asked)", "Make(null);"),
            "bug report sources: the report asks by the card, else by the game's text box, else is made with no words");
        Check(MemberText(game, "public static bool AskWords(Action<string> done)").Contains("GameUi.TextBox.Ask(") && !game.Contains("UiDirector.On") && !report.Contains("UiDirector.On"),
            "bug report sources: the game's own text box is asked whatever the interface's state is");
    }

    // ------------------------------------------------------------------------------------------------- the sources

    private static void UiGuardSources()
    {
        string director = ModSource(@"Ui\UiDirector.cs"), host = ModSource(@"Ui\UiHost.cs"), tweens = ModSource(@"Ui\UiTweens.cs"), guard = ModSource(@"Core\Guard.Faults.cs"),
            holds = ModSource(@"Core\ScreenHolds.cs"), modBox = ModSource(@"Ui\Shell\ShellModBox.cs"), meta = ModSource(@"Meta\MetaUi.cs");
        Check(director.Length > 0 && host.Length > 0 && tweens.Length > 0 && guard.Length > 0 && holds.Length > 0 && modBox.Length > 0 && meta.Length > 0, "guard sources: the sources are read");

        // One answer to "may a kit screen be drawn": the player's switch and the part still running.
        Check(director.Contains("public static bool Wanted => Settings.LivingPopulation && Settings.UiInterface;") && director.Contains("public static bool Stopped => FrameGuard.Off || UiHost.Stopped;")
              && director.Contains("public static bool On => Wanted && !Stopped;") && host.Contains("public static bool Stopped => FrameGuard.Off;"),
            "guard sources: the interface is drawn only while its switch is on and its part runs: the guard's switch-off and hosts that cannot run both stop it (UiDirector.On)");
        // The game's method throws on an unset list or a destroyed entry, and the item card asked it every frame.
        var askers = new List<string>();
        foreach (string file in Directory.GetFiles(ModScripts(), "*.cs", SearchOption.AllDirectories))
        {
            if (Regex.IsMatch(File.ReadAllText(file), @"\.GetPrimaryInteractable\s*\("))
            {
                askers.Add(System.IO.Path.GetFileName(file));
            }
        }
        string cardClick = ModSource(@"Ui\Overlays\OverlayCardsLive.Click.cs");
        Check(askers.Count == 0 && cardClick.Contains("FirstShown(_parts[i].interactablePriority)") && cardClick.Contains("list[i] != null && list[i].gameObject.activeSelf"),
            "guard sources: the item card finds a part's first shown piece itself and no source asks the game's GetPrimaryInteractable, which throws on an unset list or a destroyed entry ("
            + string.Join(", ", askers) + ")");
        var readers = new List<string>();
        foreach (string file in Directory.GetFiles(ModScripts(), "*.cs", SearchOption.AllDirectories))
        {
            string name = file.Substring(ModScripts().Length + 1).Replace('\\', '/');
            if (name != "Core/Settings.cs" && name != "Core/RealLifeOverhaulMod.cs" && name != "Ui/UiDirector.cs" && Regex.IsMatch(File.ReadAllText(file), @"Settings\.UiInterface\b"))
            {
                readers.Add(name);
            }
        }
        Check(readers.Count == 0, "guard sources: nobody reads the interface's switch past the director, so nobody can draw on an interface that stopped"
                                  + (readers.Count > 0 ? " (read in " + string.Join(", ", readers) + ")" : string.Empty));

        // A host never stands with a dead Update.
        string show = MemberText(host, "public void Show()"), update = MemberText(host, "private void Update()"), stop = MemberText(host, "private void Stop()");
        Check(StatementsInOrder(show, "if (FrameGuard.Off)", "Stop();", "return;", "Layout();") && StatementsInOrder(update, "if (FrameGuard.Off)", "Stop();", "return;", "OnUpdate();"),
            "guard sources: a host whose guard is off never shows, and one that is showing goes (UiHost.Show, Update)");
        Check(StatementsInOrder(update, "Guard.Fail(FrameGuard, e, this);", "if (Failed != null)", "Failed(e);", "else", "Stop();"),
            "guard sources: a screen that failed is given back by its owner, and with no owner the host's own canvas goes: no screen is left faulting every frame");
        Check(StatementsInOrder(stop, "enabled = false;", "if (!_gone)", "EndTyping();", "Failed?.Invoke(null);", "if (this != null && gameObject.activeSelf)", "gameObject.SetActive(false);") && !stop.Contains("if (_gone)"),
            "guard sources: a host that stops lets go of the keys and tells its owner once, and switches off what it stands on every time, so its clear picture takes no click however often its owner shows it again");

        // Nothing is left at the start of its fade.
        string add = MemberText(tweens, "private void Add(ref Entry e)"), tick = MemberText(tweens, "private void Update()"), run = MemberText(tweens, "private void OnUpdate()");
        Check(StatementsInOrder(add, "if (_count == Most || FrameGuard.Off)", "Finish(ref e);", "return;") && StatementsInOrder(tick, "if (FrameGuard.Off)", "Land();", "enabled = false;", "return;")
              && MemberText(tweens, "public void Land()").Contains("Finish(ref _entries[i]);"),
            "guard sources: with the tweens off an opening lands at once and what was moving is put where it ends: no screen stays unseen at the start of its fade");
        Check(StatementsInOrder(run, "try", "more = Step(ref _entries[i], now);", "catch (Exception e)", "more = false;", "Dropped(e);"),
            "guard sources: one entry's fault drops that entry alone, where it threw every frame before");

        // The guard: the fault's words on its lines, and the holds let go after the clean-up.
        string fail = MemberText(guard, "public static void Fail(FeatureGuard guard, Exception e, Behaviour component = null)");
        string off = MemberText(guard, "private static void SwitchedOff(FeatureGuard guard, Exception last)");
        Check(Occurs(fail, "FaultWords.Of(e)") == 2 && fail.Contains("\" failed (\" + guard.Faults + \"): \" + FaultWords.Of(e)") && fail.Contains("SwitchedOff(guard, e);")
              && off.Contains("\"The last fault: \" + FaultWords.Of(last)"),
            "guard sources: each of the guard's lines carries the fault's kind, message and first methods, the line that switches a part off too");
        Check(StatementsInOrder(off, "CleanupAction?.Invoke();", "LetGo(feature ?? guard.Where);", "Tell(") && StatementsInOrder(fail, "if (Missing(e))", "guard.SwitchOff();", "LetGo(guard.Where);", "return;")
              && MemberText(guard, "private static void LetGo(string part)").Contains("ScreenHolds.Release(part, released,"),
            "guard sources: a part switched off lets go of everything it holds over the screen, after its own clean-up and before the player is told");
        Check(off.Contains("string what = Place(guard.Where);") && off.Contains("\"reallife_guard_notice_fault_at\".GetLocalization().Replace(\"{feature}\", name).Replace(\"{what}\", what)")
              && off.Contains("\"reallife_guard_notice_fault\".GetLocalization().Replace(\"{feature}\", name)") && guard.Contains("public static void Named(string where, Func<string> words)"),
            "guard sources: the notice names what failed where its place has plain words, and says it without them elsewhere");

        // The interface's own letting go.
        string letGo = MemberText(director, "internal static void LetGoAll(string why)");
        Check(StatementsInOrder(letGo, "RemoveAll(why);", "UiGameHost.RemoveEvery(why)", "BugReportBox.Close", "ShellModBox.Close", "ViewfinderHost.Hide", "HudHost.Remove(why)", "Land()", "UiRoots.HideAll()"),
            "guard sources: the interface lets go of every kit's screen, every host, its cards, its modal piece, what was fading in, and any canvas of its own still standing");
        Check(director.Contains("ScreenHolds.Add(\"ui\", \"the RLO interface's screens, cards and canvases\", () => LetGoAll(\"its part was switched off\"), UiRoots.AnyStanding);")
              && StatementsInOrder(MemberText(director, "private IEnumerator Tick()"), "if (Stopped)", "if (!_letGo)", "LetGoAll(\"its part stopped\");", "continue;")
              && MemberText(director, "private void OnDestroy()").Contains("LetGoAll(\"the mod unloading\");"),
            "guard sources: that letting go is the interface's hold for the guard, and it runs too when the hosts stop and when the city goes");
        string every = MemberText(ModSource(@"Ui\Host\UiGameHost.cs"), "internal static void RemoveEvery(string why)");
        Check(StatementsInOrder(every, "for (int i = Hosts.Count - 1; i >= 0; i--)", "try", "Hosts[i].Remove(why);", "catch (Exception e)"), "guard sources: every host there is gives its screen back, each in a try of its own");
        Check(MemberText(modBox, "private static bool Show(string title, string body, KeyValuePair<string, Action>[] buttons, bool danger)").Contains("if (!UiDirector.On || !UiScreenSwitches.On(\"mod_box\") || !UiFonts.Ready)")
              && StatementsInOrder(modBox, "_host.Show();", "if (_host.Up)", "return true;", "Close();", "return false;"),
            "guard sources: the mod's messages go to the mod's own panel when the interface cannot draw their card");
        Check(meta.Contains("ScreenHolds.Add(FrameGuard.Where, ") && StatementsInOrder(MemberText(meta, "private static void LetGo()"), "ui.CloseAll();", "finally", "ui._canvas.enabled = false;", "ui.Hold(false);"),
            "guard sources: the mod's own panels are a hold of their class: closed, their canvas off and the player's walk given back if their guard stops");

        // The repeats found beside it: each said or noted before the part that can throw.
        string say = MemberText(ModSource(@"Ui\UiDirector.Fonts.cs"), "private static void FontsSay()");
        Check(StatementsInOrder(say, "try", "drawn = UiFontProbe.Drawn(sample);", "catch (Exception e)", "drawn = -1;", "UiFonts.FontsLine(given, drawn, GameBuild.Running)"),
            "guard sources: the fonts' line is written, and with it said, even when its probe throws: it is not asked for again every second");
        string read = MemberText(ModSource(@"Ui\Phone\PhoneItemsGame.cs"), "internal static bool Read(PhoneGridPager pager)");
        Check(StatementsInOrder(read, "_extrasSeen = PhoneExtras.Signature();", "pager.Arranged(Buttons, Kinds, Places);", "if (Buttons[i] == null)", "Ids.Add(Buttons[i].name);"),
            "guard sources: the phone's reading notes what it saw before it reads, and skips a button that went, so a reading that throws is not made again every second");
        string order = MemberText(ModSource(@"Ui\UiFonts.Fallbacks.cs"), "private static void Order()");
        Check(StatementsInOrder(order, "try", "face.ReadFontAssetDefinition();", "catch (Exception e)", "Said?.Invoke("), "guard sources: a face that cannot read its table again is said once and keeps its chain");
        string faces = MemberText(ModSource(@"Ui\Faces\FaceTurns.cs"), "private void OnUpdate()");
        Check(StatementsInOrder(faces, "try", "started = ask.Start(ask.Back);", "catch (Exception)", "_drawing = null;", "PortraitTurn.Give(this, now, 0f);", "throw;"),
            "guard sources: a face whose asking throws gives the camera's turn back and is let go before its fault is counted");

        // A face let go no longer holds the camera's turn for its whole wait, which had kept the faces behind it from meeting the same
        // fault: so after a fault the next face waits, or a fault every face meets is counted at the pace of the faces (0.35 s) and the
        // fifth switches the whole interface off in two seconds.
        string turns = ModSource(@"Ui\Faces\FaceTurns.cs");
        Match restText = Regex.Match(turns, @"FaultRestSeconds = (\d+(\.\d+)?)f;"), pace = Regex.Match(turns, @"\bRestSeconds = (\d+(\.\d+)?)f;");
        float rest = restText.Success ? float.Parse(restText.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0f,
            quick = pace.Success ? float.Parse(pace.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0f;
        var restGuard = new FeatureGuard("ui", "FaceTurns");
        var paceGuard = new FeatureGuard("ui", "FaceTurns");
        bool restOff = false, paceOff = false;
        float at = 0f, quickAt = 0f;
        for (int i = 0; i < 400; i++)
        {
            restOff |= restGuard.Count(at);          // a fault every face meets, one face after another
            paceOff |= paceGuard.Count(quickAt);
            at += rest;
            quickAt += quick;
        }
        Check(restText.Success && rest > 0f && StatementsInOrder(MemberText(turns, "private void Update()"), "Guard.Fail(FrameGuard, e, this);", "_next = Time.realtimeSinceStartup + FaultRestSeconds;")
              && !restOff && paceOff,
            "guard sources: after a fault the next face waits " + rest + " s, so a fault that every face meets is never counted five times in the guard's " + FeatureGuard.Seconds
            + " s, as it would be at the faces' own pace of " + quick + " s");
    }

    // Options, Tests: the part switched off the way the guard does it, so that what Report a bug does then can be seen in the game.
    private static void UiGuardTest()
    {
        string guide = ModSource(@"Ui\UiGuide.cs"), tests = ModSource(@"Ui\UiDirector.GuardTests.cs");
        string run = MemberText(tests, "public void TestGuardOff()");
        Check(guide.Contains(".Test(\"reallife_ui_test_guard\", () => UiDirector.Instance?.TestGuardOff())"),
            "guard test: Options, Tests has an entry that switches the interface's part off as the guard does");
        Check(StatementsInOrder(run, "if (Stopped)", "\"reallife_ui_test_guard_stopped\"", "return;", "for (int i = 0; i < FeatureGuard.Most; i++)", "Guard.Fail(FrameGuard, e, this);")
              && Occurs(run, "throw new InvalidOperationException(") == 1,
            "guard test: it says so when the part is off already, and otherwise gives the part as many faults as switch it off");
        var part = new FeatureGuard("ui", "UiDirector");
        bool went = false;
        for (int i = 0; i < FeatureGuard.Most; i++)
        {
            went = part.Count(i) || went;   // a second apart, as the director's looks are
        }
        Check(went && part.Off && part.Faults == FeatureGuard.Most,
            "guard test: " + FeatureGuard.Most + " faults of one entry point a second apart switch it off (" + part.Faults + " faults)");
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        bool label = en != null && en.TryGetValue("reallife_ui_test_guard", out string name) && name.Length > 0 && name.Length <= 80 && UiOffPlain(name);
        bool said = en != null && en.TryGetValue("reallife_ui_test_guard_stopped", out string stopped) && stopped.Contains("started again") && stopped.Length <= UiOffRules.NoticeMost
                    && UiOffPlain(stopped);
        Check(label && said, "guard test: its label and its notice are in en.json, plain, and the notice says the game must be started again");
    }

    // Every canvas of the kit's own that takes the pointer is registered where it is made, and every host has an owner's way out.
    private static void UiGuardRoots()
    {
        string ui = System.IO.Path.Combine(ModScripts(), "Ui");
        int canvases = 0, hosts = 0;
        var unregistered = new List<string>();
        var ownerless = new List<string>();
        foreach (string file in Directory.GetFiles(ui, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file), name = file.Substring(ui.Length + 1).Replace('\\', '/');
            int made = Occurs(text, "AddComponent<GraphicRaycaster>()"), taken = Occurs(text, "UiRoots.Take(");
            canvases += made;
            if (made != taken && name != "Host/UiRoots.cs")
            {
                unregistered.Add(name + " (" + made + " made, " + taken + " registered)");
            }
            int added = Occurs(text, "AddComponent<UiHost>()"), owned = Regex.Matches(text, @"\.Failed = ").Count;
            hosts += added;
            if (owned < added)
            {
                ownerless.Add(name + " (" + added + " hosts, " + owned + " ways out)");
            }
        }
        Check(canvases >= 9 && unregistered.Count == 0,
            "guard roots: each of the " + canvases + " canvases the kit makes to take the pointer is registered where it is made (UiRoots.Take), so the part's switch-off switches every one off"
            + (unregistered.Count > 0 ? "; not: " + string.Join(", ", unregistered) : string.Empty));
        Check(hosts >= 10 && ownerless.Count == 0,
            "guard roots: each of the " + hosts + " hosts the kit makes is told what to do when its screen fails (Failed)" + (ownerless.Count > 0 ? "; not: " + string.Join(", ", ownerless) : string.Empty));
        string roots = ModSource(@"Ui\Host\UiRoots.cs");
        Check(StatementsInOrder(MemberText(roots, "internal static int HideAll()"), "if (root.activeSelf)", "root.SetActive(false);") && MemberText(roots, "internal static GameObject Take(GameObject root)").Contains("Roots.Add(root);"),
            "guard roots: every registered canvas is switched off when asked");
        var raisers = new List<string>();
        foreach (string file in Directory.GetFiles(ui, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file), name = file.Substring(ui.Length + 1).Replace('\\', '/');
            if (Regex.IsMatch(text, @"Cursor\.lockState\s*=\s*CursorLockMode\.Locked|Time\.timeScale\s*=|SetNavigationBlocker\(|DisableTimeControl\(|\.SetPause\("))
            {
                raisers.Add(name);
            }
        }
        Check(raisers.Count == 0, "guard roots: the interface takes neither the cursor, the clock nor the player's walk" + (raisers.Count > 0 ? " (it does in " + string.Join(", ", raisers) + ")" : string.Empty));
    }

    // The second's look: every kit in a try of its own, and nothing of the kit asked while the interface is off.
    private static void UiGuardLook()
    {
        string director = ModSource(@"Ui\UiDirector.cs");
        Match count = Regex.Match(director, @"Phone = (\d+), Count = (\d+);");
        int kits = count.Success ? int.Parse(count.Groups[2].Value) : 0;
        string install = MemberText(director, "private static void InstallOne(int kit)"), remove = MemberText(director, "private static void RemoveOne(int kit, string why)"),
            slow = MemberText(director, "private void Slow()"), all = MemberText(director, "internal static void RemoveAll(string why)");
        Match orderText = Regex.Match(director, @"RemoveOrder =\s*\{([^}]*)\}");
        int ordered = orderText.Success ? Regex.Matches(orderText.Groups[1].Value, @"Kit\.\w+").Cast<Match>().Select(m => m.Value).Distinct().Count() : 0;
        Check(kits >= 27 && count.Groups[1].Value == (kits - 1).ToString() && Regex.Matches(install, @"case Kit\.\w+:").Count == kits && Regex.Matches(remove, @"case Kit\.\w+:").Count == kits && ordered == kits,
            "guard look: each of the " + kits + " kits is put on, taken off and listed in the order of taking off (" + Regex.Matches(install, @"case Kit\.\w+:").Count + ", "
            + Regex.Matches(remove, @"case Kit\.\w+:").Count + ", " + ordered + ")");
        Check(Regex.Matches(install, @"\.Install\(\);|UiShell\.Tick\(\);").Count == kits && Regex.Matches(remove, @"\.Remove\(why\);").Count == kits,
            "guard look: every kit's own Install and Remove is called there and nowhere beside it");
        Check(Regex.Matches(director, @"\b\w+\.Install\(\);").Count == Regex.Matches(install, @"\b\w+\.Install\(\);").Count,
            "guard look: no kit is put on outside the one place that runs each in a try of its own");
        Check(StatementsInOrder(slow, "for (int kit = 0; kit < Kit.Count; kit++)", "if (UiPieces.Off(PieceOf[kit]))", "continue;", "try", "InstallOne(kit);", "catch (Exception e)", "PieceFailed(PieceOf[kit], e);"),
            "guard look: each kit of the look runs in a try of its own, and one that failed is not asked");
        Check(StatementsInOrder(all, "for (int i = 0; i < RemoveOrder.Length; i++)", "try", "RemoveOne(RemoveOrder[i], why);", "catch (Exception e)"),
            "guard look: each kit gives its screens back in a try of its own, so one that cannot keeps no other from it");
        string failed = MemberText(director, "internal static void PieceFailed(int piece, Exception e)");
        Check(StatementsInOrder(failed, "bool was = UiPieces.Trip(piece);", "FaultWords.Of(e)", "Logger?.Error(e);", "RemoveOne(kit, \"a fault\");") && !failed.Contains("Guard.Fail("),
            "guard look: a piece that failed is off, said once with what failed, its kit's screens given back, and the guard does not count it against the whole interface");
        int off = slow.IndexOf("if (!On)", StringComparison.Ordinal), once = slow.IndexOf("if (_said != 1)", StringComparison.Ordinal),
            removal = slow.IndexOf("RemoveAll(\"the switch is off\");", StringComparison.Ordinal), kitsLoop = slow.IndexOf("for (int kit = 0;", StringComparison.Ordinal);
        Check(off > 0 && once > off && removal > once && kitsLoop > removal && Occurs(slow, "RemoveAll(\"the switch is off\");") == 1,
            "guard look: with the interface off every kit is taken off once, as it goes off, and not at every second");
        Check(StatementsInOrder(slow, "if (On && !UiPieces.Off(_fontsPiece))", "try", "UiFonts.Step()", "ShadersLook();", "FontsStep();", "catch (Exception e)", "PieceFailed(_fontsPiece, e);", "bool city ="),
            "guard look: the fonts' steps are a piece of their own, and run only while the interface is drawn");
        Check(StatementsInOrder(slow, "if (!UiPieces.Off(_playedPiece))", "try", "PlayTime.Tick();", "catch (Exception e)", "PieceFailed(_playedPiece, e);", "if (!UiPieces.Off(_worthPiece))", "try",
                  "Economy.WorthWatch.Tick();", "catch (Exception e)", "PieceFailed(_worthPiece, e);", "if (!On)"),
            "guard look: the time played and the day's net worth are counted with the interface on or off, each in a try of its own");
        Check(StatementsInOrder(slow, "if (!city)", "RemoveAll(\"no city\");", "RearmOne(RemoveOrder[i]);", "UiPieces.Rearm();", "return;"),
            "guard look: a new city asks again a piece that failed once");

        // The pieces that stand themselves up each second tell the same breaker.
        string live = ModSource(@"Ui\HudScreens\HudLive.cs"), hud = ModSource(@"Ui\Hud\HudHost.cs"), world = ModSource(@"Ui\World\WorldHost.cs");
        Check(StatementsInOrder(MemberText(live, "private void Update()"), "Guard.Fail(Guarded, e, this);", "UiDirector.PieceFailed(UiDirector.HudPiece, e);")
              && StatementsInOrder(MemberText(hud, "private void Update()"), "Guard.Fail(FrameGuard, e, this);", "Remove(\"a fault\");", "UiDirector.PieceFailed(UiDirector.HudPiece, e);")
              && Occurs(world, "UiDirector.PieceFailed(UiDirector.WorldPiece, e);") == 2,
            "guard look: the HUD and the world's boxes, which stood up again each second after a fault and met it again, go off with their piece until the next city");
        string install2 = MemberText(live, "public static void Install()");
        Check(install2.Contains("_watch.enabled = true;") && StatementsInOrder(MemberText(director, "private static void InstallOne(int kit)"), "case Kit.Hud:", "HudLive.Install();"),
            "guard look: the HUD's watch is woken only by the look, which does not ask a piece that is off");

        // Each piece's name for a notice is a text the game has, and the notice it makes fits the toast.
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        string pieces = MemberText(director, "private static int[] MakePieces()");
        MatchCollection labels = Regex.Matches(pieces, "UiPieces\\.Id\\(\"([^\"]+)\", \"(reallife_[a-z_]+)\"\\)");
        int named = 0, missing = 0, longest = 0;
        string notice = en != null && en.TryGetValue("reallife_ui_note_piece_off", out string n) ? n : string.Empty,
            at = en != null && en.TryGetValue("reallife_guard_notice_fault_at", out string a) ? a : string.Empty,
            part = en != null && en.TryGetValue("reallife_meta_title_ui", out string t) ? t : string.Empty;
        foreach (Match m in labels)
        {
            named++;
            if (en == null || !en.TryGetValue(m.Groups[2].Value, out string words) || words.Length == 0)
            {
                missing++;
                continue;
            }
            longest = Math.Max(longest, Math.Max(notice.Replace("{what}", words).Length, at.Replace("{feature}", part).Replace("{what}", words).Length));
        }
        Check(named == kits && missing == 0 && Regex.Matches(pieces, @"UiPieces\.Id\(").Count == kits + 4,
            "guard look: each of the " + named + " kits' pieces has its name in the player's language from a text the game has, and the look's four own pieces need none (" + missing + " missing)");
        Check(notice.Length > 0 && at.Length > 0 && part.Length > 0 && longest > 0 && longest <= UiOffRules.NoticeMost,
            "guard look: the notice of a piece taken off, and the guard's notice naming it, fit the game's toast with the longest name (" + longest + " of " + UiOffRules.NoticeMost + " characters)");
    }

    // ------------------------------------------------------------------------------------------------- every hold of the mod

    // What can be left standing over the game's screen or holding of its input, as the sources raise it.
    private static readonly (string Kind, Regex Raised)[] HoldKinds =
    {
        ("the player's walk", new Regex(@"\.SetNavigationBlocker\(", RegexOptions.Compiled)),
        ("the clock", new Regex(@"DisableTimeControl\(\s*(true|locked)|\.SetPause\(\s*true", RegexOptions.Compiled)),
        ("the cursor", new Regex(@"Cursor\.lockState\s*=\s*CursorLockMode\.Locked|Cursor\.visible\s*=\s*false", RegexOptions.Compiled)),
        ("the mod's keys", new Regex(@"Keys\.Suspended\s*=\s*(true|typing|on)\b", RegexOptions.Compiled)),
        ("a canvas that takes the pointer", new Regex(@"AddComponent<GraphicRaycaster>\(\)|UiOverCanvas\.Make\(", RegexOptions.Compiled)),
    };

    // Each file that raises a hold, with how it is let go of when the guard switches its part off: "hold:" a hold registered in
    // Core/ScreenHolds under that part; "cleanup:" its feature's own clean-up, which the guard runs first; "gap:" a feature with no
    // clean-up, whose hold stays if the guard switches it off while it is held (known, each to be closed with its feature); "none:"
    // a tool of the developer's own. A file that raises a hold and is not here fails the check, as does a line here that no longer holds.
    private static readonly Dictionary<string, string> HoldFiles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "Ui/Buy/ItemPanelKit.cs", "hold:ui" }, { "Ui/Host/UiGameHost.cs", "hold:ui" }, { "Ui/Host/UiOverCanvas.cs", "hold:ui" }, { "Ui/Hud/HudHost.cs", "hold:ui" },
        { "Ui/Life/ViewfinderHost.cs", "hold:ui" }, { "Ui/Phone/PhoneKit.cs", "hold:ui" }, { "Ui/Settings/BugReportBox.cs", "hold:ui" }, { "Ui/Shell/ShellModBox.cs", "hold:ui" },
        { "Ui/Shell/UiShell.cs", "hold:ui" }, { "Ui/UiDirector.Tests.cs", "hold:ui" }, { "Ui/World/WorldHost.cs", "hold:ui" }, { "Ui/World/WorldPicker.cs", "hold:ui" },
        { "Ui/Tooltips/TooltipHost.cs", "hold:ui" },
        { "Meta/MetaUi.cs", "hold:MetaUi" },
        { "Meta/OptionsLayout.Search.cs", "none:a search box of the options page, which gives the keys back as its typing ends, whatever is off" },
        { "Meta/SettingsView.Find.cs", "none:a search box of the Mod settings app, which gives the keys back as its typing ends, whatever is off" },
        { "Meta/StaffView.cs", "none:a search box of the Staff app, which gives the keys back as its typing ends, whatever is off" },
        { "Camera/ChaseCamera.cs", "cleanup:camera" }, { "Camera/ThirdPersonCamera.cs", "cleanup:camera" },
        { "Careers/ComputerSitting.cs", "cleanup:careers_study" }, { "Careers/DeskGame.cs", "cleanup:careers_study" }, { "Careers/InterviewVisit.cs", "cleanup:careers_study" },
        { "Careers/SchoolDesk.cs", "cleanup:careers_study" },
        { "Construction/BuildModeGame.cs", "cleanup:construction" }, { "Construction/BuilderDeskGame.cs", "cleanup:construction" },
        { "Economy/Dealer/Floor/ShowroomGame.cs", "cleanup:economy_dealer" }, { "Economy/Ventures/PitchMeeting.cs", "cleanup:ventures" },
        { "Jobs/JobBody.cs", "cleanup:jobs" }, { "Leisure/LeisureBody.cs", "cleanup:leisure" }, { "Leisure/Photo/PhotoMode.cs", "cleanup:leisure" },
        { "Navigation/NavHud.Make.cs", "cleanup:navigation" },
        { "Photos/CameraView.cs", "cleanup:photos" }, { "Photos/Cinematic/CineDirector.cs", "cleanup:photos" },
        { "Photos/Cinematic/CineGame.cs", "cleanup:photos" }, { "Photos/PhotosGame.cs", "cleanup:photos" },
        { "Tech/Hiring/InterviewMeeting.cs", "cleanup:tech_sector" }, { "Tech/Venture/VentureMeeting.cs", "cleanup:tech_sector" },
        { "Travel/TravelDirector.cs", "cleanup:travel" },
        { "People/Seating.cs", "cleanup:family" },
        { "Civic/OfficePanel.cs", "gap:civic" }, { "Fame/FamePanel.cs", "gap:standing" },
        { "Justice/CommunityService.cs", "gap:justice" }, { "Justice/JusticeHold.cs", "gap:justice" },
        { "Law/Custody.Scene.cs", "gap:law" }, { "Law/StationDesk.cs", "gap:law" },
        { "Licences/SchoolDesk.cs", "gap:licences" }, { "Licences/TheoryTest.cs", "gap:licences" },
        { "Permadeath/PermadeathGame.cs", "gap:permadeath" }, { "Ragdoll/PlayerKnockdown.cs", "gap:ragdoll" }, { "Social/SocialBody.cs", "gap:social" },
        { "Transport/BusRider.Ride.cs", "gap:transport" }, { "Transport/BusRider.cs", "gap:transport" }, { "Transport/TransitRide.cs", "gap:transport" }, { "Transport/TransitSeat.cs", "gap:transport" },
        { "Phone/Contacts/ContactSearch.cs", "gap:phone" },
        { "TestRun/RunDirector.cs", "none:the developer's own test run, never started in play" }, { "TestRun/RunGame.cs", "none:the developer's own test run, never started in play" },
        { "Photos/Cinematic/CineGame.cs#clock", "cleanup:photos" },
    };

    private static void UiGuardEveryHold()
    {
        string scripts = ModScripts();
        var raised = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var all = new System.Text.StringBuilder();
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file), name = file.Substring(scripts.Length + 1).Replace('\\', '/');
            all.Append(text).Append('\n');
            var kinds = new List<string>();
            foreach ((string kind, Regex r) in HoldKinds)
            {
                if (r.IsMatch(text))
                {
                    kinds.Add(kind);
                }
            }
            if (kinds.Count > 0 && name != "Ui/Host/UiRoots.cs")
            {
                raised[name] = string.Join(", ", kinds);
            }
        }
        var unlisted = raised.Keys.Where(f => !HoldFiles.ContainsKey(f)).Select(f => f + " (" + raised[f] + ")").ToList();
        var stale = HoldFiles.Keys.Where(f => !f.Contains('#') && !raised.ContainsKey(f)).ToList();
        Check(raised.Count >= 50 && unlisted.Count == 0,
            "every hold: each of the " + raised.Count + " files that take the player's walk, the clock, the cursor, the mod's keys or the pointer is listed with how it is let go of when its part is switched off"
            + (unlisted.Count > 0 ? "; not listed: " + string.Join("; ", unlisted) : string.Empty));
        Check(stale.Count == 0, "every hold: every line of the list still raises a hold" + (stale.Count > 0 ? " (stale: " + string.Join(", ", stale) + ")" : string.Empty));

        string sources = all.ToString();
        var cleanups = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(sources, "Features\\.Add\\(\"(\\w+)\""))
        {
            int depth = 0, i = m.Index + m.Length;
            for (; i < sources.Length; i++)
            {
                char c = sources[i];
                depth += c == '{' ? 1 : c == '}' ? -1 : 0;
                if (c == ';' && depth == 0)
                {
                    break;
                }
            }
            if (sources.Substring(m.Index, i - m.Index).Contains(".Cleanup("))
            {
                cleanups.Add(m.Groups[1].Value);
            }
        }
        var wrong = new List<string>();
        var gaps = new SortedSet<string>(StringComparer.Ordinal);
        int held = 0, cleaned = 0;
        foreach (KeyValuePair<string, string> line in HoldFiles)
        {
            string how = line.Value;
            if (how.StartsWith("hold:", StringComparison.Ordinal))
            {
                string part = how.Substring(5);
                held++;
                bool registered = part == "ui" ? sources.Contains("ScreenHolds.Add(\"ui\", ") : part == "MetaUi" && ModSource(@"Meta\MetaUi.cs").Contains("ScreenHolds.Add(FrameGuard.Where, ")
                                                                                              && ModSource(@"Meta\MetaUi.cs").Contains("Guard.For(null, \"MetaUi\", typeof(MetaUi))");
                if (!registered)
                {
                    wrong.Add(line.Key + ": no hold is registered for '" + part + "'");
                }
            }
            else if (how.StartsWith("cleanup:", StringComparison.Ordinal))
            {
                cleaned++;
                if (!cleanups.Contains(how.Substring(8)))
                {
                    wrong.Add(line.Key + ": the feature '" + how.Substring(8) + "' has no clean-up");
                }
            }
            else if (how.StartsWith("gap:", StringComparison.Ordinal))
            {
                gaps.Add(how.Substring(4));
                if (cleanups.Contains(how.Substring(4)))
                {
                    wrong.Add(line.Key + ": the feature '" + how.Substring(4) + "' has a clean-up now: list it as cleaned up");
                }
            }
        }
        Check(wrong.Count == 0 && held >= 14 && cleaned >= 20,
            "every hold: " + held + " are holds the guard lets go of itself and " + cleaned + " belong to a feature whose own clean-up the guard runs first"
            + (wrong.Count > 0 ? "; wrong: " + string.Join("; ", wrong) : string.Empty));
        Check(gaps.SetEquals(new[] { "civic", "justice", "law", "licences", "permadeath", "phone", "ragdoll", "social", "standing", "transport" }),
            "every hold: the features known to hold the player's walk, the clock or the mod's keys with no clean-up for the guard to run are these ten and no more (" + string.Join(", ", gaps)
            + "): none of them takes the pointer or the cursor");
        var gapKinds = new SortedSet<string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> line in HoldFiles)
        {
            if (line.Value.StartsWith("gap:", StringComparison.Ordinal) && raised.TryGetValue(line.Key, out string kinds))
            {
                foreach (string kind in kinds.Split(new[] { ", " }, StringSplitOptions.None))
                {
                    gapKinds.Add(kind);
                }
            }
        }
        Check(!gapKinds.Contains("the cursor") && !gapKinds.Contains("a canvas that takes the pointer"),
            "every hold: everything that can make the game take no click (a canvas over the screen, the cursor) is let go of by the guard or by a clean-up it runs (the gaps hold: "
            + string.Join(", ", gapKinds) + ")");
    }

    // ------------------------------------------------------------------------------------------------- the texts

    private static void UiGuardTexts()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Check(en != null, "guard texts: en.json is read");
        if (en == null)
        {
            return;
        }
        bool plain = en.TryGetValue("reallife_guard_notice_fault", out string fault), named = en.TryGetValue("reallife_guard_notice_fault_at", out string at),
            piece = en.TryGetValue("reallife_ui_note_piece_off", out string off), status = en.TryGetValue("reallife_guard_status_faults", out string faults);
        Check(plain && named && piece && status, "guard texts: the notices and the status line are in en.json");
        if (!plain || !named || !piece || !status)
        {
            return;
        }
        Check(fault.Contains("{feature}") && !fault.Contains("{what}") && at.Contains("{feature}") && at.Contains("{what}") && off.Contains("{what}") && faults.Contains("{features}"),
            "guard texts: each text has the names it is filled with");
        Check(fault.Contains("until you start the game again") && at.Contains("until you start the game again") && faults.Contains("until the game is started again")
              && !fault.Contains("load a game") && !at.Contains("load a game") && !faults.Contains("loaded again"),
            "guard texts: a part switched off after errors is said to stay off until the game is started again, which is what the guard does, and never until a save is loaded");
        Check(fault.Contains("Player.log") && at.Contains("Player.log") && off.Contains("Player.log") && off.Contains("the game's own") && off.Contains("rest of the interface goes on"),
            "guard texts: each notice points at Player.log, and a piece's says the rest of the interface goes on");
        foreach (string text in new[] { fault, at, off, faults })
        {
            Check(text.Length <= UiOffRules.NoticeMost && UiOffPlain(text), "guard texts: at most " + UiOffRules.NoticeMost + " characters, no dash and no exclamation mark (" + text.Length + ")");
        }
        string guard = ModSource(@"Core\Guard.Faults.cs");
        Check(guard.Contains("switched off until the game is started again. Its saved data is left as it was. \"") && !guard.Contains("until the game is loaded again"),
            "guard texts: Player.log's own line says the same");
        Check(en.TryGetValue("reallife_guide_trouble_update", out string page) && page.Contains("until you start the game again") && !page.Contains("load a game again")
              && page.Contains("lets go of whatever it held over the screen") && page.Contains("goes back to the game's own alone") && UiOffPlain(page),
            "guard texts: the Guide's page about the guard says the same, and that one screen of the RLO interface goes back alone");
        const string sentence = "Report a bug opened a card that could not be seen or answered";
        string row = en.Keys.FirstOrDefault(k => k.StartsWith("reallife_changes_", StringComparison.Ordinal) && !k.EndsWith("_title", StringComparison.Ordinal) && en[k].Contains(sentence));
        Check(row != null && en[row].Contains("Fixed: after a part of the RLO interface had been switched off") && en[row].Contains("Fixed: an error in one screen of the RLO interface")
              && en[row].Contains("Fixed: the notice about a part switched off after repeated errors") && UiOffPlain(en[row]),
            "guard texts: What's new has the three fixes in English");

        // The other languages come with the translation step: where one has a notice, it has its names and fits the toast.
        foreach (string file in Directory.GetFiles(DesignLocales, "*.json"))
        {
            Dictionary<string, string> words = LoadWords(file);
            if (words == null || System.IO.Path.GetFileName(file) == "en.json")
            {
                continue;
            }
            if (words.TryGetValue("reallife_guard_notice_fault_at", out string theirs))
            {
                Check(theirs.Contains("{feature}") && theirs.Contains("{what}") && theirs.Length <= UiOffRules.NoticeMost,
                    "guard texts: the named notice in " + System.IO.Path.GetFileName(file) + " has its two names and fits the toast (" + theirs.Length + ")");
            }
            if (words.TryGetValue("reallife_ui_note_piece_off", out string theirPiece))
            {
                Check(theirPiece.Contains("{what}") && theirPiece.Length <= UiOffRules.NoticeMost,
                    "guard texts: the piece's notice in " + System.IO.Path.GetFileName(file) + " has its name and fits the toast (" + theirPiece.Length + ")");
            }
        }
    }
}
