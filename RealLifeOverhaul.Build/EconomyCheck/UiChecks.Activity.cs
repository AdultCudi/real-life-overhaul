using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;

// The activity kit (Scripts/Ui/Activity): the game's activity panel before it starts as the side card and while it runs as
// the strip, the time skip's veil, the funeral and the time of day tool, each state laid out and checked as every kit
// screen is; then the binding's rules: every activity of the game's and the mod's drawn through the one panel, the game's
// keys left to the game, the panel shown only while the game shows its own, a desk session's pages first, and Going
// Public's session left alone.
internal static partial class Program
{
    private static readonly int[] ActivityCardCommands2 = { UiActivity.ButtonCommand, UiActivity.StepCommand };
    private static readonly int[] FuneralCommands = { UiSideCard.ChoiceCommand, UiSideCard.ButtonCommand };
    private static readonly int[] DayToolCommands = { UiTimeOfDay.SliderCommand, UiTimeOfDay.StepCommand, UiDraw.CloseCommand };

    static partial void UiCasesActivity(UiScreenDef def, List<UiScreenCase> cases)
    {
        void Add(string name, string title, int[] commands, bool overlay, Action<UiScene, UiFrame, bool, IUiKeys, UiWords> lay) =>
            cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (" + name + ")", Title = title, Commands = commands, Overlay = overlay, Lay = lay });
        int[] strip = HudCommands.Concat(new[] { UiActivity.CancelCommand }).ToArray();
        switch (def.Id)
        {
            case "activity_panel":
                Add("a working day before it starts", "Work", ActivityCardCommands2, true, (s, f, d, k, t) => UiSamples.ActivityCardScene(s, f, d, k, t, UiSamples.ActivityWork(t, k, UiActivityState.NotStarted)));
                Add("a class before it starts", "Class", ActivityCardCommands2, true, (s, f, d, k, t) => UiSamples.ActivityCardScene(s, f, d, k, t, UiSamples.ActivityClass(t, k)));
                Add("a working day at night", "Work", ActivityCardCommands2, true, (s, f, d, k, t) => UiSamples.ActivityCardScene(s, f, d, k, t, UiSamples.ActivityWork(t, k, UiActivityState.NotStarted), true));
                foreach (UiActivityState state in new[] { UiActivityState.Moving, UiActivityState.Waiting, UiActivityState.Started, UiActivityState.Running, UiActivityState.Finished })
                {
                    UiActivityState st = state;
                    Add("a working day, " + st, "HUD", strip, true, (s, f, d, k, t) => UiSamples.ActivityStripScene(s, f, d, k, t, UiSamples.ActivityWork(t, k, st)));
                }
                Add("a desk session's time in the chair", "HUD", strip, true, (s, f, d, k, t) => UiSamples.ActivityStripScene(s, f, d, k, t, UiSamples.ActivityChair(t, k)));
                Add("cooking at home", "HUD", strip, true, (s, f, d, k, t) => UiSamples.ActivityStripScene(s, f, d, k, t, UiSamples.ActivityCook(t, k), true));
                Add("a bus ride", "HUD", strip, true, (s, f, d, k, t) => UiSamples.ActivityStripScene(s, f, d, k, t, UiSamples.ActivityBus(t, k)));
                break;
            case "time_skip":
                Add("to the end of a working day", "Skip time", TimeSkipCommands, false, (s, f, d, k, t) => UiSamples.ActivitySkipScene(s, f, d, k, t));
                Add("the game's own line, no Cancel", "Skip time", TimeSkipCommands, true, (s, f, d, k, t) => UiSamples.ActivitySkipScene(s, f, d, k, t, true));
                break;
            case "funeral":
                Add("with a backup", "Funeral", FuneralCommands, false, (s, f, d, k, t) => UiSamples.ActivityFuneralScene(s, f, d, k, t));
                Add("no backup", "Funeral", FuneralCommands, false, (s, f, d, k, t) => UiSamples.ActivityFuneralScene(s, f, d, k, t, false));
                break;
            case "time_of_day":
                Add("evening light", "Time of day", DayToolCommands, true, (s, f, d, k, t) => UiSamples.ActivityDayToolScene(s, f, d, k, t));
                break;
        }
    }

    private static void ActivityKitChecks()
    {
        int before = _run, failedBefore = _failed;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        ActivityEveryActivity();
        ActivityKitSources();
        ActivityStripRules();
        Console.WriteLine("  Activity kit: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static string ActivityModRoot => System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul"));

    private static string ActivityFolder => System.IO.Path.Combine(ActivityModRoot, "Scripts", "Ui", "Activity");

    // Every activity the mod gives the game's panel is drawn through the one panel screen, by what the panel asks of it
    // (IPlayerActivity), never by its type: the list below is every one there is, so a new one is seen and looked at.
    private static void ActivityEveryActivity()
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string file in Directory.GetFiles(System.IO.Path.Combine(ActivityModRoot, "Scripts"), "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"class\s+(\w+)\s*(?:<[^>]*>)?\s*:[^{]*\bIPlayerActivity\b"))
            {
                found.Add(m.Groups[1].Value);
            }
        }
        string[] known =
        {
            "BusRidePanel", "ComputerSitting", "DeskWork", "HomeActivity", "InterviewVisit", "SchoolDesk", "SessionPanel", "ShiftSkip", "TestActivity",
        };
        var missing = found.Where(n => Array.IndexOf(known, n) < 0).ToList();
        Console.WriteLine("    the mod's activities on the kit's panel: " + string.Join(", ", found));
        Check(missing.Count == 0, "activity kit: every activity of the mod's is one the kit was looked at with (new: " + (missing.Count == 0 ? "none" : string.Join(", ", missing)) + ")");
        string panel = File.ReadAllText(System.IO.Path.Combine(ActivityFolder, "ActivityPanelScreen.cs"));
        bool byType = known.Any(n => n != "TestActivity" && Regex.IsMatch(panel, @"\bis\s+" + n + @"\b"));
        Check(!byType && panel.Contains("a.GetButtons()") && panel.Contains("a.GetLabels()") && panel.Contains("a.GetHeadlineLabel()") && panel.Contains("a.HasSlider()")
              && panel.Contains("a.HasProgressBar()") && panel.Contains("a.HasTimeMachine()") && panel.Contains("a.HasFastForward()"),
            "activity kit: the panel draws every activity, the game's own and the mod's, by what the game's panel asks of it, never by its type");
    }

    // The game keeps its logic and its keys: the panel takes no key, presses what the game's own buttons press, moves the
    // slider as the game's keys do, and stands aside while the game's panel is faded out or a desk session's pages are up.
    private static void ActivityKitSources()
    {
        string kit = File.ReadAllText(System.IO.Path.Combine(ActivityFolder, "ActivityKit.cs"));
        string panel = File.ReadAllText(System.IO.Path.Combine(ActivityFolder, "ActivityPanelScreen.cs"));
        string skip = File.ReadAllText(System.IO.Path.Combine(ActivityFolder, "TimeSkipScreen.cs"));
        string funeral = File.ReadAllText(System.IO.Path.Combine(ActivityFolder, "FuneralScreen.cs"));
        string day = File.ReadAllText(System.IO.Path.Combine(ActivityFolder, "TimeOfDayScreen.cs"));
        string director = File.ReadAllText(System.IO.Path.Combine(ActivityModRoot, "Scripts", "Ui", "UiDirector.cs"));
        Check(kit.Contains("UiGameKind.Overlay") && kit.Contains("UiGameKind.Box") && kit.Contains("UiScreenSwitches.On(All[i].Id)") && director.Contains("ActivityKit.Install()")
              && director.Contains("ActivityKit.Remove(why)") && director.Contains("ActivityKit.Rearm()"),
            "activity kit: the panel and the time of day tool are overlays that take no key, the time skip and the funeral boxes, each with its switch, put on, taken off and rearmed with the interface");
        Check(panel.Contains("_ui.ForceTimeMachine()") && panel.Contains("_ui.OnPlayerActionPressed(PlayerAction.SecondaryInteract)") && panel.Contains("_ui.ChangeSliderValue(")
              && panel.Contains("_ui.CancelActivityMovement()") && panel.Contains("b.onClick()") && panel.Contains("public bool Back() => false;") && panel.Contains("public bool Act(UiAction action) => false;"),
            "activity kit: the panel presses what the game's own buttons press and leaves the game its own keys (Escape, F, P, the slider's keys with their repeat)");
        Check(panel.Contains("SessionKit.Up") && panel.Contains("GoingPublic.InSession") && panel.Contains("_fade.alpha < 0.5f"),
            "activity kit: the panel shows only while the game shows its own, never over a desk session's pages or a Going Public session's panel");
        Check(skip.Contains("_machine.CancelTimeMachine()") && skip.Contains("cancelButton.gameObject.activeSelf") && skip.Contains("GameLook.Clear()"),
            "activity kit: the time skip cancels as the game's own Cancel, only when the game allows one, with no blur under the veil");
        Check(funeral.Contains("_ui.LoadBackUpSave()") && funeral.Contains("_ui.BackToMainMenu()") && funeral.Contains("_ui.ExitToDesktop()") && funeral.Contains("_gone")
              && funeral.Contains("return false;   // the game's own Escape quits"),
            "activity kit: the funeral's ways on are the game's own, taken once, its F, P and Escape left to the game");
        Check(day.Contains("InGameTimeOfDayPanel.Close()") && day.Contains("_tool.DecreaseTimeButton()") && day.Contains("_tool.IncreaseTimeButton()") && day.Contains("_slider.value ="),
            "activity kit: the time of day tool moves through its own slider and buttons");
    }

    // The strip stands in one place over the bar whatever its state, and the card's slider reads its share where it was laid.
    private static void ActivityStripRules()
    {
        UiWords t = (key, english) => english;
        var frame = UiFrame.FullMenu(16f / 9f, "HUD");
        float height = -1f;
        bool same = true;
        foreach (UiActivityState state in new[] { UiActivityState.Moving, UiActivityState.Waiting, UiActivityState.Started, UiActivityState.Running, UiActivityState.Finished })
        {
            UiActivityModel m = UiSamples.ActivityWork(t, UiDefaultKeys.Keyboard, state);
            UiActivity.StripPlace(frame, m, out float x, out float y);
            float h = UiActivity.Height(m);
            same &= height < 0f || Math.Abs(h - height) < 0.01f;
            height = h;
            same &= y + h <= frame.BandY;
        }
        Check(same, "activity kit: the strip keeps one height above the status bar in every state, so it never jumps");
        var s = new UiScene();
        UiActivityModel card = UiSamples.ActivityWork(t, UiDefaultKeys.Keyboard, UiActivityState.NotStarted);
        UiActivity.LayCard(s, card, frame, true, UiDefaultKeys.Keyboard);
        float left = UiActivity.SliderShare(card, card.Card.TrackX), right = UiActivity.SliderShare(card, card.Card.TrackX + card.Card.TrackW);
        Check(card.Card.TrackW > 0f && left <= 0.001f && right >= 0.999f, "activity kit: the card's slider reads 0 at its left end and 1 at its right, where it was laid");
    }
}
