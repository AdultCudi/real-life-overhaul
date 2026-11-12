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
                Add("rest on a bench, the slider at its most", "Rest", ActivityCardCommands2, true, (s, f, d, k, t) => UiSamples.ActivityCardScene(s, f, d, k, t, UiSamples.ActivityRest(t, k)));
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
        ActivityDeskSources();
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

    // A working day at a desk: the hours to stay chosen on the panel's own slider before it starts (one to the rest of the
    // day, the right end the whole rest), the day then ending early and settled as a part of it as Stop working would, the
    // whole day only when the whole rest was worked; the card's facts read from the job's own file.
    private static void ActivityDeskSources()
    {
        string desk = File.ReadAllText(System.IO.Path.Combine(ActivityModRoot, "Scripts", "Careers", "DeskWork.cs"));
        string facts = File.ReadAllText(System.IO.Path.Combine(ActivityFolder, "ActivityFacts.cs"));
        string day = File.ReadAllText(System.IO.Path.Combine(ActivityModRoot, "Scripts", "Careers", "CareersDirector.DeskDay.cs"));
        Check(desk.Contains("return _state == PlayerActivityState.NotStarted && !_pressed && _canStart && MostHours >= 2;")
              && desk.Contains("_length = _hours * 60;") && desk.Contains("sitting.SkipEnded(Math.Min(_done, _length), _done >= _whole);")
              && desk.Contains("_hours = value >= MostHours ? 0 : Math.Max(1, value);") && day.Contains("internal int DeskPlan()") && !DeskPlanBody(day).Contains("ClockIn("),
            "activity kit: a working day's hours are chosen on the game's own slider, end early as a part of the day, and looking at them begins nothing");
        Check(facts.Contains("Prices.At(j.Pay)") && facts.Contains("j.Favour") && facts.Contains("CareersText.Hours(p)") && facts.Contains("desk.ChosenMinutes") && facts.Contains("desk.Done"),
            "activity kit: a working day's card and strip read the job's own pay, week, standing and the minutes worked");
    }

    // DeskPlan's own body: from its name to the next member's summary.
    private static string DeskPlanBody(string day)
    {
        int at = day.IndexOf("internal int DeskPlan()", StringComparison.Ordinal);
        int end = at < 0 ? -1 : day.IndexOf("/// <summary>", at, StringComparison.Ordinal);
        return at < 0 ? "ClockIn(" : end < 0 ? day.Substring(at) : day.Substring(at, end - at);
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
        // The running strips grow to their words: the headline, "Hour 4 of 6", "Earned $152", the place and every button whole.
        foreach (float aspect in new[] { 16f / 9f, 4f / 3f })
        {
            var wide = UiFrame.FullMenu(aspect, "HUD");
            foreach (UiActivityModel run in new[] { UiSamples.ActivityWork(t, UiDefaultKeys.Keyboard, UiActivityState.Running), UiSamples.ActivityChair(t, UiDefaultKeys.Keyboard) })
            {
                var ss = new UiScene();
                UiActivity.StripPlace(wide, run, out float sx, out float sy);
                UiActivity.DrawStrip(ss, UiAt.Screen, run, sx, sy);
                string cut = null;
                for (int i = 0; i < ss.ItemCount && cut == null; i++)
                {
                    ref UiItem it = ref ss.Items[i];
                    if (it.Kind == UiKind.Text && !string.IsNullOrEmpty(it.Text) && UiTextRules.Width(it.Text, it.Style) > it.W + 0.5f)
                    {
                        cut = "\"" + it.Text + "\" in " + it.W.ToString("0.#") + " px";
                    }
                }
                Check(cut == null && sx >= 0f && sx + run.StripW <= wide.Width,
                    "activity kit: the running strip at " + aspect.ToString("0.00") + " shows every word whole (" + (cut ?? "none cut") + ")");
            }
        }
        var s = new UiScene();
        UiActivityModel card = UiSamples.ActivityWork(t, UiDefaultKeys.Keyboard, UiActivityState.NotStarted);
        UiActivity.LayCard(s, card, frame, true, UiDefaultKeys.Keyboard);
        float left = UiActivity.SliderShare(card, card.Card.TrackX), right = UiActivity.SliderShare(card, card.Card.TrackX + card.Card.TrackW);
        Check(card.Card.TrackW > 0f && left <= 0.001f && right >= 0.999f, "activity kit: the card's slider reads 0 at its left end and 1 at its right, where it was laid");
        // The game's two-sentence slider line ("Rest for 24 hours, 0 minutes. Will stop resting at 19:15") splits into the value
        // and a note, and no word of the card is drawn over the slider, in every language of the check and the longest line.
        string v = UiActivity.SplitValue("Rest for 24 hours, 0 minutes. Will stop resting at 19:15", out string nt);
        Check(v == "Rest for 24 hours, 0 minutes" && nt == "Will stop resting at 19:15", "activity kit: the slider's two sentences split into its value and its note (" + v + " | " + nt + ")");
        v = UiActivity.SplitValue("1.5 hours", out nt);
        Check(v == "1.5 hours" && nt == null, "activity kit: a decimal point does not split the slider's value");
        v = UiActivity.SplitValue("休息24小时。将在19:15停止", out nt);
        Check(v == "休息24小时" && nt == "将在19:15停止", "activity kit: the Chinese full stop splits the slider's value too");
        foreach (bool german in new[] { false, true })
        {
            UiSamples.ActivityGerman = german;
            try
            {
                foreach (float aspect in new[] { 16f / 9f, 4f / 3f })
                {
                    UiActivityModel rest = UiSamples.ActivityRest(t, UiDefaultKeys.Keyboard);
                    var rs = new UiScene();
                    UiActivity.LayCard(rs, rest, UiFrame.FullMenu(aspect, "Rest"), true, UiDefaultKeys.Keyboard);
                    int slider = -1;
                    for (int i = 0; i < rs.NodeCount && slider < 0; i++)
                    {
                        slider = rs.Nodes[i].Command == UiActivity.StepCommand ? i : -1;
                    }
                    string over = slider < 0 ? "no slider" : null;
                    for (int i = 0; i < rs.ItemCount && over == null; i++)
                    {
                        ref UiItem it = ref rs.Items[i];
                        if (it.Kind == UiKind.Text && !string.IsNullOrEmpty(it.Text) && it.X < rs.Nodes[slider].X + rs.Nodes[slider].W && it.X + it.W > rs.Nodes[slider].X
                            && it.Y < rs.Nodes[slider].Y + rs.Nodes[slider].H && it.Y + it.H > rs.Nodes[slider].Y)
                        {
                            over = "\"" + it.Text + "\" is over the slider";
                        }
                    }
                    Check(over == null && rest.Card != null && rest.Card.SliderNote != null, "activity kit: rest at its most" + (german ? " in German" : "") + " at " + aspect.ToString("0.00") + ": no text over the slider (" + (over ?? "clear") + ")");
                }
            }
            finally
            {
                UiSamples.ActivityGerman = false;
            }
        }
    }
}
