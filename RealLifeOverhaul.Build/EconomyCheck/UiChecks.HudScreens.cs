using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Leisure;
using RealLifeOverhaul.Seasons;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    // Every command the HUD's live pieces answer (Ui/HudScreens/HudLive.Press): the bar's pills and its small buttons' pill,
    // the side panels' folds, buttons, tool and switch, the objectives' fold, bands, lines, locate links and Cancel, the
    // notice cards' links and buttons, and the popups' rows, switches and buttons.
    private static readonly int[] HudScreensCommands =
    {
        UiSidePanel.FoldCommand, UiSidePanel.LinkCommand, UiSidePanel.ActionCommand, UiSidePanel.ToolCommand, UiSidePanel.SwitchCommand,
        UiObjectives.FoldCommand, UiObjectives.GroupCommand, UiObjectives.LineCommand, UiObjectives.LocateCommand, UiObjectives.CloseCommand,
        UiHudNotice.LinkCommand, UiHudNotice.ActionCommand, UiHudPopup.RowCommand, UiHudPopup.SwitchCommand, UiHudPopup.ActionCommand,
        UiDraw.TimePillCommand, UiDraw.MoneyPillCommand, UiDraw.NeedsPillCommand, UiDraw.AlertsPillCommand, UiDraw.SettingsPillCommand, UiDraw.ToolPillCommand
    };

    // The HUD's live pieces, every state each has: laid out in every language, frame and input as any screen.
    static partial void UiCasesHudScreens(UiScreenDef def, List<UiScreenCase> cases)
    {
        switch (def.Id)
        {
            case "hud_side":
                Pieces(def, cases, "at work in the morning", (s, f, d, k, t) => UiSamples.PiecesSideScene(s, f, d, k, t, false));
                Pieces(def, cases, "after the shift at night", (s, f, d, k, t) => UiSamples.PiecesSideScene(s, f, d, k, t, true));
                break;
            case "hud_objectives":
                Pieces(def, cases, "every group", (s, f, d, k, t) => UiSamples.PiecesGoalsScene(s, f, d, k, t, false));
                Pieces(def, cases, "more than the screen holds", (s, f, d, k, t) => UiSamples.PiecesGoalsScene(s, f, d, k, t, true));
                break;
            case "hud_notices":
                Pieces(def, cases, "a goal, the day and the hospital", (s, f, d, k, t) => UiSamples.PiecesNotesScene(s, f, d, k, t, false));
                Pieces(def, cases, "the casino and the bridge", (s, f, d, k, t) => UiSamples.PiecesNotesScene(s, f, d, k, t, true));
                break;
            case "hud_popups":
                Pieces(def, cases, "the dances and the accessories", (s, f, d, k, t) => UiSamples.PiecesPopupsScene(s, f, d, k, t, false));
                Pieces(def, cases, "nothing worn, indoors", (s, f, d, k, t) => UiSamples.PiecesPopupsScene(s, f, d, k, t, true));
                Pieces(def, cases, "the game's hints as key chips", (s, f, d, k, t) => UiSamples.PiecesHintsScene(s, f, d, k, t));
                break;
        }
    }

    private static void Pieces(UiScreenDef def, List<UiScreenCase> cases, string name, System.Action<UiScene, UiFrame, bool, IUiKeys, UiWords> lay) =>
        cases.Add(new UiScreenCase { Def = def, Name = def.Id + " (" + name + ")", Title = "HUD", Commands = HudScreensCommands, Lay = lay });

    // The HUD's live pieces, game-free: the temperature's figure, the small buttons' pill and the panels rising from it, the
    // side panels' height as the game's notifications are told it, a high priority alert's mark, a card's fade, and the
    // live code's promises read from its source.
    private static void HudScreensChecks()
    {
        HudTemperatureChecks();
        HudToolChecks();
        HudPieceChecks();
        HudScreensSourceChecks();
        HudItemChecks();
        HudDanceChecks();
        HudItemSourceChecks();
    }

    // The HUD's small sums, game-free: today's money and its arrow, a band's count, a goal's progress, the rota shift a stay
    // in hospital went over, and a personal goal's reward by its tier.
    private static void HudItemChecks()
    {
        Check(!HudNoteRules.Today(1000.0, null, out _) && !HudNoteRules.Today(1000.0, new List<float>(), out _)
              && HudNoteRules.Today(1240.5, new List<float> { 9000f, 0.5f }, out double net) && Math.Abs(net - 1240.0) < 0.001,
            "hud money today: the money less the balance at the last midnight; none before a save's first midnight");
        Check(HudNoteRules.Trend(0.004) == 0 && HudNoteRules.Trend(-0.004) == 0 && HudNoteRules.Trend(0.01) == 1 && HudNoteRules.Trend(-3.0) == -1,
            "hud money today: its arrow up for a gain, down for a loss, none for nothing");
        const string done = "{done} of {count} done", of = "{current} of {amount}";
        Check(HudNoteRules.GroupNote(0, 3, done) == "3" && HudNoteRules.GroupNote(1, 3, done) == "1 of 3 done" && HudNoteRules.GroupNote(0, 0, done) == null
              && HudNoteRules.DoneNote(0, 96, done) == "0 of 96 done" && HudNoteRules.DoneNote(5, 0, done) == null,
            "hud objectives: a band counts its open lines, then how many are done; the goals' band all its goals done");
        Check(HudNoteRules.Progress("$31,400", "$50,000", "$", of) == "$31,400" && HudNoteRules.Progress("1", "2", "$", of) == "1 of 2"
              && HudNoteRules.Progress(null, "2", "$", of) == null && HudNoteRules.Progress("3", null, "$", of) == "3",
            "hud objectives: a goal's progress is its money figure alone, else its count of its target");
        const int mwf = 1 | 4 | 16;   // Monday, Wednesday and Friday; day 1 is a Monday
        Check(HudNoteRules.MissedShift(mwf, 9, 4, 1, 2 * 60, 1, 8 * 60) == 0 && HudNoteRules.MissedShift(mwf, 9, 4, 1, 10 * 60, 2, 8 * 60) == 1
              && HudNoteRules.MissedShift(mwf, 9, 4, 2, 23 * 60, 3, 8 * 60) == 0 && HudNoteRules.MissedShift(mwf, 18, 4, 3, 17 * 60, 4, 8 * 60) == 3
              && HudNoteRules.MissedShift(0, 9, 4, 1, 0, 9, 0) == 0 && HudNoteRules.MissedShift(mwf, 9, 4, 5, 0, 4, 0) == 0,
            "hud hospital: the rota shift a stay went over, collapsed before its end and out after its start; none outside it or without a rota");
        Check(GoalRewardRules.Reward(0) == 5000 && GoalRewardRules.Reward(1) == 20000 && GoalRewardRules.Reward(2) == 75000
              && GoalRewardRules.Reward(7) == 75000 && GoalRewardRules.Reward(-1) == 5000,
            "goal rewards: $5,000, $20,000 and $75,000 by the goal's tier at the start's prices, the third for any past it");
        float[] kind = { 50000f, 10000f, 250000f };
        Check(GoalRewardRules.Tier(10000f, kind, 3) == 0 && GoalRewardRules.Tier(50000f, kind, 3) == 1 && GoalRewardRules.Tier(250000f, kind, 3) == 2
              && GoalRewardRules.Tier(250000f, kind, 1) == 1 && GoalRewardRules.Tier(5f, kind, 0) == 0,
            "goal rewards: a goal's tier is how many goals of its kind ask for less");
    }

    // The dances the player knows: a new game's four, an older save's nine, the dance class's next dance in the game's
    // order, and the dance floor's picks among the known alone; the class itself as the catalogue's rules want a course.
    private static void HudDanceChecks()
    {
        Check(DanceRules.FirstSet(true) == DanceRules.Starter && DanceRules.FirstSet(false) == DanceRules.All && DanceRules.KnownCount(DanceRules.Starter) == 4
              && DanceRules.KnownCount(DanceRules.All) == DanceRules.Count && DanceRules.Sound(0) == DanceRules.Starter && DanceRules.Sound(-1) == DanceRules.All,
            "dances: a new game knows the first four, an older save every dance; a saved set holds the four and nothing past the nine");
        int set = DanceRules.Starter;
        bool order = true;
        for (int i = 4; i < DanceRules.Count; i++)
        {
            set = DanceRules.Learn(set, out int learned);
            order &= learned == i && DanceRules.Known(set, i);
        }
        set = DanceRules.Learn(set, out int none);
        Check(order && none == -1 && set == DanceRules.All && DanceRules.NextLocked(DanceRules.All) == -1 && DanceRules.NextLocked(DanceRules.Starter) == 4,
            "dances: each dance class teaches the next dance in the game's order, the fifth to the ninth, then none");
        bool picks = true, varied = false;
        for (int roll = -20; roll < 40; roll++)
        {
            int p = DanceRules.Pick(DanceRules.Starter, roll), o = DanceRules.Other(DanceRules.Starter, 2, roll);
            picks &= DanceRules.Known(DanceRules.Starter, p) && o != 2 && DanceRules.Known(DanceRules.Starter, o);
            varied |= p == 3;
        }
        Check(picks && varied && DanceRules.Other(1, 0, 5) == 0 && DanceRules.Pick(0, 3) == 0 && !DanceRules.Known(DanceRules.All, 9) && !DanceRules.Known(DanceRules.All, -1),
            "dances: the dance floor dances known dances alone, another one each time while there is one");
        CourseDef dance = StudyCatalogue.Find(StudyCatalogue.DanceClass);
        Check(dance != null && dance.Key == "dance_class" && dance.Kind == CourseKind.Short && dance.Campus == Campus.School && dance.Sessions >= DanceRules.Count - 4
              && dance.Homework == 0,
            "dances: the school's dance class is a short course of no homework with a class for each dance a new game does not know");
    }

    // What the new items promise, read from their source: an older save keeps every dance and only a new game starts with
    // four, the dance class teaches when a class counts, the dance floor dances known dances, a goal pays once, the goals
    // reached before pay nothing, and the dances held back are given back when the careers stand down.
    private static void HudItemSourceChecks()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string Read(string rel) => File.ReadAllText(System.IO.Path.Combine(scripts, rel));
        string leisure = Read(@"Leisure\LeisureState.cs"), state = Read(@"Core\ModState.cs");
        Check(leisure.Contains("Dances = _dancesRead ? DanceRules.Sound(dances) : DanceRules.All;") && leisure.Contains("if (!_dancesRead && Fresh && newGame)")
              && leisure.Contains("data[KeyDances] = Dances.ToString(CultureInfo.InvariantCulture);") && state.Contains("Leisure.LeisureState.SettleDances(save.Day <= 1);"),
            "dances: a save without the set keeps every dance; only a new game with no leisure data starts with four; the set is saved");
        string work = Read(@"Careers\CareersDirector.Coursework.cs"), careers = Read(@"Careers\CareersDirector.cs"), floor = Read(@"Leisure\Activities\NightOut.cs");
        int taught = work.IndexOf("AfterClass(e, day, true);", StringComparison.Ordinal);
        Check(taught > 0 && work.IndexOf("if (c.Id == StudyCatalogue.DanceClass)", taught, StringComparison.Ordinal) > taught
              && work.IndexOf("if (c.Id == StudyCatalogue.DanceClass)", taught, StringComparison.Ordinal) < taught + 200,
            "dances: a dance class teaches the next dance when its class counts, and only then");
        Check(careers.Contains("Leisure.DanceLocks.Keep(_studyOn);") && careers.Contains("Leisure.DanceLocks.Release();")
              && floor.Contains("DanceRules.Pick(DanceLocks.Mask") && floor.Contains("DanceRules.Other(DanceLocks.Mask") && !floor.Contains("Random.Range(0, 9)"),
            "dances: the dances not learned are held back while the courses run and given back when the careers stand down; the dance floor dances known ones");
        string rewards = Read(@"Economy\Banking\EconomyDirector.GoalRewards.cs"), econ = Read(@"Economy\Banking\EconomyDirector.cs");
        Check(rewards.Contains("reached.IndexOf(reached[i]) == i") && rewards.Contains("_goalsCounted = reached.Count;\r\n                return;")
              && rewards.Contains("if (goal == null || goal.isHidden)") && econ.Contains("KeepGoalRewards(Settings.Economy && _running);"),
            "goal rewards: each goal pays once, the goals a save held when read pay nothing, a hidden goal nothing, and the count is kept with the economy off");
    }

    // A figure for the eye from the calendar, the day's roll, the hour and the weather: summer warmer than winter by the
    // year's swing, the afternoon over the small hours, the weather's moves and floors, the same every time, inside its bounds.
    private static void HudTemperatureChecks()
    {
        int year = RealLifeOverhaul.CalendarRules.YearDefault;
        int Dry(int day, float hour, int spell = TemperatureRules.SpellNone) => TemperatureRules.Celsius(day, year, hour, 7, TemperatureRules.SkyDry, spell, false);
        int least = int.MaxValue, most = int.MinValue, steps = 0;
        bool same = true;
        for (int day = 1; day <= year; day++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                int c = Dry(day, hour);
                least = Math.Min(least, c);
                most = Math.Max(most, c);
                same &= c == Dry(day, hour);
                steps++;
            }
        }
        int span = TemperatureRules.Swing + TemperatureRules.DayRange + TemperatureRules.DaySwing;
        Check(least >= TemperatureRules.Mean - span && most <= TemperatureRules.Mean + span && same,
            "hud temperature: a dry year at every hour stays within " + (TemperatureRules.Mean - span) + " to " + (TemperatureRules.Mean + span) + " C (" + least + " to " + most + "), the same each time asked (" + steps + " hours)");
        int winter = Dry(1, 15f), summer = Dry(year / 2 + 1, 15f);
        Check(summer - winter >= TemperatureRules.Swing * 2 - TemperatureRules.DayRange * 2, "hud temperature: midsummer's afternoon is the year's swing warmer than midwinter's (" + winter + " C, " + summer + " C)");
        Check(Dry(20, 15f) - Dry(20, 3f) == TemperatureRules.DaySwing * 2, "hud temperature: three in the afternoon is the day's swing warmer than three at night");
        int dry = Dry(20, 15f);
        Check(TemperatureRules.Celsius(20, year, 15f, 7, TemperatureRules.SkyRain, TemperatureRules.SpellNone, false) == Math.Max(TemperatureRules.RainLeast, dry + TemperatureRules.Rain)
              && TemperatureRules.Celsius(1, year, 3f, 7, TemperatureRules.SkyRain, TemperatureRules.SpellColdSnap, false) >= TemperatureRules.RainLeast,
            "hud temperature: rain cools the air, and rain never falls under " + TemperatureRules.RainLeast + " C");
        Check(TemperatureRules.Celsius(30, year, 15f, 7, TemperatureRules.SkyDry, TemperatureRules.SpellNone, true) <= TemperatureRules.SnowMost
              && TemperatureRules.Celsius(30, year, 15f, 7, TemperatureRules.SkyDry, TemperatureRules.SpellIce, false) <= TemperatureRules.IceMost,
            "hud temperature: snow lies at freezing or under, icy roads under that, even on a summer's afternoon");
        Check(Dry(20, 15f, TemperatureRules.SpellHeatWave) == dry + TemperatureRules.HeatWave && Dry(20, 15f, TemperatureRules.SpellColdSnap) == dry + TemperatureRules.ColdSnap,
            "hud temperature: a heat wave and a cold snap move the whole day");
        Check(TemperatureRules.Text(18, false) == "18°C" && TemperatureRules.Text(18, true) == "64°F" && TemperatureRules.Fahrenheit(-40) == -40 && TemperatureRules.Fahrenheit(100) == 212,
            "hud temperature: written in the game's units, 18 C as 64 F");
    }

    // The top bar's small buttons in one pill after the place, on the HUD's own bar alone; each panel rises over its button.
    private static void HudToolChecks()
    {
        var s = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f);
        UiStatusModel bar = UiSamples.PiecesBar(false, 0, 1);
        UiPillPlan plan = UiDraw.PlanPills(bar, frame.Width);
        UiSamples.PiecesPopupsScene(s, frame, true, UiDefaultKeys.Keyboard, UiWords("en"), false);
        int found = 0;
        bool under = true;
        for (int i = 0; i < s.NodeCount; i++)
        {
            ref UiNode n = ref s.Nodes[i];
            if (n.Command == UiDraw.ToolPillCommand)
            {
                found++;
                under &= Math.Abs(n.X + n.W / 2f - UiDraw.ToolMiddle(plan, n.Arg)) < 0.5f;
            }
        }
        Check(found == bar.ToolCount && under && plan.ToolsX > 0f, "hud tools: the bar's small buttons stand in one pill after the place, each where its panel rises from (" + found + " of " + bar.ToolCount + ")");
        bool risen = true;
        UiHudPopupModel[] popups = UiSamples.PiecesPopups(UiWords("en"), bar, frame.Width, false);
        for (int p = 0; p < popups.Length; p++)
        {
            int action = s.FindNode(UiHudPopup.ActionCommand, p);
            bool rows = popups[p].RowCount == 0 || s.FindNode(UiHudPopup.RowCommand, p * 32) >= 0 || s.FindNode(UiHudPopup.SwitchCommand, p * 32) >= 0;
            risen &= action >= 0 && rows && s.Nodes[action].Y + s.Nodes[action].H <= frame.BandY - UiTokens.S2 + 0.5f;
        }
        Check(risen, "hud tools: the dances and the accessories rise over the bar, their rows and their buttons pressable");
        UiStatusModel app = UiSamples.PiecesBar(false);
        app.PillsPress = false;
        var plain = new UiScene();
        Func<UiStatusModel> was = UiChrome.Bar;
        UiChrome.Bar = () => app;
        try
        {
            plain.Begin("bar", frame.Width, frame.Height, true);
            UiDraw.StatusBar(plain, frame, UiDefaultKeys.Keyboard);
        }
        finally
        {
            UiChrome.Bar = was;
        }
        Check(plain.FindNode(UiDraw.ToolPillCommand, 0) < 0, "hud tools: the small buttons are the HUD's alone, never on a bar under an app");
    }

    // The side panels' height as the game's notifications are told it, an urgent alert's warning mark, and a card's fade.
    private static void HudPieceChecks()
    {
        UiWords en = UiWords("en");
        var s = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f);
        UiSidePanelModel[] side = UiSamples.PiecesSides(en, false);
        s.Begin("side", frame.Width, frame.Height, true);
        float foot = UiSidePanel.LayStack(s, UiAt.Screen, side, side.Length, frame.Width - UiHud.Left, UiTokens.S4, UiDefaultKeys.Keyboard);
        Check(Math.Abs(foot - UiTokens.S4 - UiSidePanel.StackHeight(side, side.Length)) < 0.5f,
            "hud side panels: the stack's height the game's notifications start under is the height laid out (" + UiSidePanel.StackHeight(side, side.Length) + " px)");
        UiObjectivesModel goals = UiSamples.PiecesGoals(en, false);
        s.Begin("goals", frame.Width, frame.Height, true);
        UiObjectives.Lay(s, UiAt.Screen, goals, UiHud.Left, UiTokens.S4);
        int warnings = 0, checks = 0;
        for (int i = 0; i < s.ItemCount; i++)
        {
            warnings += s.Items[i].Kind == UiKind.Icon && s.Items[i].Icon == UiIcon.Warning ? 1 : 0;
            checks += s.Items[i].Kind == UiKind.Icon && s.Items[i].Icon == UiIcon.Check ? 1 : 0;
        }
        Check(warnings == 1 && checks == 2, "hud objectives: an alert the game marks red carries the warning mark in its tick's place, a done line its check (" + warnings + ", " + checks + ")");
        UiHudNoticeModel[] cards = UiSamples.PiecesNotes(en, false);
        cards[1].Alpha = 0.5f;
        s.Begin("cards", frame.Width, frame.Height, true);
        UiHudNotice.LayStack(s, UiAt.Screen, cards, 1, frame.Width, UiTokens.S4);
        int second = s.ItemCount;
        UiHudNotice.LayStack(s, UiAt.Screen, new[] { cards[1] }, 1, frame.Width, 400f);
        bool faded = s.ItemCount > second;
        for (int i = second; i < s.ItemCount; i++)
        {
            faded &= s.Items[i].Alpha <= 0.5f + 0.001f;
        }
        Check(second > 0 && faded, "hud notices: a card fades with the game's own panel, every part of it");
    }

    // What the live HUD promises, read from its source: every piece given back as found, the phone and the notifications
    // put back, a read four times a second, its frame timed and guarded, the game's pieces never switched off or destroyed.
    private static void HudScreensSourceChecks()
    {
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui\HudScreens"));
        string live = File.ReadAllText(System.IO.Path.Combine(folder, "HudLive.cs"));
        int down = live.IndexOf("private static void StandDown(", StringComparison.Ordinal);
        string stand = down >= 0 ? live.Substring(down, Math.Min(1800, live.Length - down)) : string.Empty;
        Check(stand.Contains("HudTopGame.End();") && stand.Contains("HudSides.End();") && stand.Contains("HudGoals.End();") && stand.Contains("HudNotes.End();") && stand.Contains("HudHints.End();")
              && stand.Contains("PhoneKit.Lift = 0f;") && stand.Contains("NoticeRoom.Room = null;") && stand.Contains("HomeNeedsBar.Held = false;") && stand.Contains("SeasonBar.Held = false;"),
            "hud screens: standing down gives every piece back, the phone its place, the notifications theirs and the mod's own bars back");
        Check(live.Contains("LookEvery = 0.25f") && live.Contains("using var hotSpot = HotSpots.Time(Hot);") && live.Contains("Guard.Fail(Guarded, e, this);"),
            "hud screens: the HUD reads the game's pieces four times a second, its frame timed and guarded");
        bool kept = true;
        foreach (string name in new[] { "HudSides.cs", "HudGoals.cs", "HudNotes.cs", "HudHints.cs" })
        {
            string code = File.ReadAllText(System.IO.Path.Combine(folder, name));
            kept &= !code.Contains(".onClick.Invoke(") && !code.Contains("Destroy(") && !code.Contains("SetActive(false)");
        }
        Check(kept, "hud screens: the pieces press the game's own buttons through the host's library and never switch off or destroy the game's");
    }
}
