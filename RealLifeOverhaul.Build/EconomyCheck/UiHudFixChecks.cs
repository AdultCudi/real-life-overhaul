using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Ui;
using ForeignApps = RealLifeOverhaul.Ui.UiForeignApps;   // Program has a check of that name

// The frame's parity fixes (HUD, full menu bar, pause, notices, tooltips): the clock in the player's option, the figure under the
// money from the game's own label, the season option, the money and clock hover cards, Help and Report bug on the full menu's bar,
// the held bug button's reason, Steam's keyboard from the pause name, an error notice's own look, the unread bell, and a second mod's
// app worded like another's.
internal static partial class Program
{
    private static void HudFrameFixChecks()
    {
        HudClockChecks();
        HudFrameSourceChecks();
        HudFrameSceneChecks();
        HudFrameTipChecks();
        HudForeignWordsChecks();
    }

    private static void HudClockChecks()
    {
        Check(HudClockRules.Format(8, 45f, false) == "08:45" && HudClockRules.Format(0, 5f, false) == "00:05" && HudClockRules.Format(23, 59.9f, false) == "23:59",
            "hud clock: the 24 hour clock writes two digits and cuts the minute, as the game's top bar does");
        Check(HudClockRules.Format(0, 5f, true) == "12:05 AM" && HudClockRules.Format(12, 0f, true) == "12:00 PM" && HudClockRules.Format(13, 30f, true) == "01:30 PM"
              && HudClockRules.Format(23, 59f, true) == "11:59 PM" && HudClockRules.Format(9, 0f, true) == "09:00 AM",
            "hud clock: the 12 hour option writes midnight as 12 AM, noon as 12 PM and the afternoon from 01 PM");
        Check(HudClockRules.Widest(true).EndsWith("PM") && HudClockRules.Widest(false) == "00:00", "hud clock: the time pill measures the widest time of the player's clock");
    }

    private static void HudFrameSourceChecks()
    {
        string game = ModSource(@"Ui\UiGame.cs"), pause = ModSource(@"Ui\Pause\PauseScreen.cs"), notices = ModSource(@"Ui\Shell\ShellNotices.cs");
        Check(!game.Contains("Hour.ToString(\"00\"") && game.Contains("UiClock.At(now.Hour, (int)now.Minute)"),
            "hud clock: the bar and the masthead write the time in the game's own option");
        Check(!pause.Contains("Hour.ToString(\"00\"") && pause.Contains("HudClockRules.Format(") && pause.Contains("_whenTwelve != TimeHelper.use12h"),
            "hud clock: the pause menu's line follows the option and is written again when the option changes");
        Check(!notices.Contains("Hour.ToString(\"00\")") && notices.Contains("HudClockRules.Format(n.date.Hour"), "hud clock: the notice list's ages follow the option");
        string status = ModSource(@"Ui\Hud\HudStatusGame.cs");
        string money = MemberText(status, "private static void MoneyDay(UiStatusModel m, UIs uis)");
        Check(money.Contains("uis.topBar.moneyChange") && !money.Contains("midnightBankBalances"),
            "hud money: the figure under the money is the game's own label (yesterday's result, or the day result option's), at every day of the save");
        string season = MemberText(status, "private static void Season(UiStatusModel m, UIs uis)");
        Check(season.Contains("!Settings.SeasonBar") && status.Contains("m.TimeWidest = HudClockRules.Widest(TimeHelper.use12h);"),
            "hud season: the season on the bar option takes the season line off the weather pill, and the time pill keeps one width in either clock");
        Check(MemberText(status, "private static int Unread()").Contains("_noticesSeen") && status.Contains("NoticesRead()") && ModSource(@"Ui\Hud\HudScreen.cs").Contains("HudStatusGame.NoticesRead();")
              && notices.Contains("HudStatusGame.NoticesRead();"),
            "hud bell: the number is the notices since the list was last opened, not every notice the game keeps");
        string typing = MemberText(pause, "public void TypingStarted()");
        Check(pause.Contains("IUiTypingStart") && typing.Contains("_name.onSelect.Invoke(_name.text)") && ModSource(@"Ui\UiHost.cs").Contains("(typing as IUiTypingStart)?.TypingStarted();"),
            "pause: typing in the save name runs the game's own select listener, which opens Steam's on-screen keyboard in Big Picture mode");
        string glyph = MemberText(notices, "public static UiIcon Glyph(string key, NotificationType type)");
        Check(glyph.Contains("NotificationType.Error") && notices.Contains("reallife_ui_notice_error") && ModSource(@"Ui\UiIcon.cs").Contains("case UiIcon.Heart: case UiIcon.Close:"),
            "notices: an error has its own glyph, header and tone, apart from a warning");
        Check(notices.Contains("_m.More = ") && ModSource(@"Ui\UiNotices.cs").Contains("m.More"), "notices: toasts past the three drawn are counted under the stack, not dropped silently");
        string tipHost = ModSource(@"Ui\Tooltips\TooltipHost.cs");
        Check(tipHost.Contains("_gameGroup.alpha <= 0f") && tipHost.Contains("host._gameGroup = gameGroup;"), "tooltips: a tooltip the game holds back (alpha 0 while still visible) draws no card");
        string top = ModSource(@"Ui\HudScreens\HudTopGame.cs");
        Check(top.Contains("UiWidgets.There(_bug)") && top.Contains("!_bug.interactable") && top.Contains("menu_report_bug_disabled_mods_tooltip"),
            "hud tools: the report button shows held, with the game's reason, in a modded save");
        string sides = ModSource(@"Ui\HudScreens\HudSides.cs");
        Check(sides.Split(new[] { "ParkedTypes(parking, ref rows, sig)" }, StringSplitOptions.None).Length == 3 && sides.Contains("LocalizedListTooltip"),
            "hud sides: the car park's panel lists the vehicle types the game's hover lists, in both places it reads the count");
        string shell = ModSource(@"Ui\Shell\UiShell.cs");
        Check(game.Contains("HudTopGame.FillShell(m)") && shell.Contains("UiDraw.ShellToolCommand") && shell.Contains("\"BottomBar/LeftButtons/Help\""),
            "shell: the game's Help and Report bug, hidden at the full menu's foot, stand on the kit's bar and press the game's own buttons");
    }

    private static void HudFrameSceneChecks()
    {
        UiFrame frame = UiFrame.FullMenu(16f / 9f);
        UiStatusModel hud = UiSamples.PiecesBar(false);
        hud.PillsPress = true;
        Func<UiStatusModel> was = UiChrome.Bar;
        try
        {
            UiChrome.Bar = () => hud;
            var s = new UiScene();
            s.Begin("bar", frame.Width, frame.Height, true);
            UiDraw.StatusBar(s, frame, UiDefaultKeys.Keyboard);
            int clock = s.FindNode(UiDraw.TimePillCommand, UiDraw.ClockArg), fast = s.FindNode(UiDraw.TimePillCommand, 2);
            Check(clock >= 0 && fast >= 0 && s.Nodes[clock].X > s.Nodes[fast].X, "hud clock: the time pill's clock and day are a hover target of their own, after the speed squares");

            UiStatusModel shellBar = UiSamples.PiecesBar(false);
            shellBar.PillsPress = false;
            shellBar.ToolsShell = true;
            shellBar.Tools = new[] { UiIcon.Info, UiIcon.Bug };
            shellBar.ToolsOn = new bool[2];
            shellBar.ToolsOff = new[] { false, true };
            shellBar.ToolCount = 2;
            UiChrome.Bar = () => shellBar;
            var f = new UiScene();
            f.Begin("bar", frame.Width, frame.Height, true);
            UiDraw.StatusBar(f, frame, UiDefaultKeys.Keyboard);
            Check(f.FindNode(UiDraw.ShellToolCommand, 0) >= 0 && f.FindNode(UiDraw.ShellToolCommand, 1) >= 0 && f.FindNode(UiDraw.ToolPillCommand, 0) < 0
                  && f.FindNode(UiDraw.TimePillCommand, UiDraw.ClockArg) < 0,
                "shell: Help and Report bug stand on the full menu's bar as targets of the frame, and nothing of the HUD's pills answers there");
            Check(UiDraw.IsChrome(UiDraw.ShellToolCommand) && !UiDraw.IsPill(UiDraw.ShellToolCommand), "shell: their command is one the host hands to the frame, not a HUD pill's");
        }
        finally
        {
            UiChrome.Bar = was;
        }
        UiStatusModel wide = UiSamples.PiecesBar(false);
        wide.TimeWidest = "00:00 PM";
        UiStatusModel narrow = UiSamples.PiecesBar(false);
        narrow.TimeWidest = null;
        Check(UiDraw.PlanPills(wide, frame.Width).Time >= UiDraw.PlanPills(narrow, frame.Width).Time, "hud clock: the time pill is never narrower for the 12 hour clock's AM and PM");
    }

    private static void HudFrameTipChecks()
    {
        UiWords en = UiWords("en");
        var summary = new WorthSummary { Now = 5000, Has = false, High = 5000, Low = 5000, HighDay = 3, LowDay = 3, FromDay = 3, Points = 1 };
        UiTooltipModel card = UiNetWorthTip.Model(en, summary, 3, null, a => "$" + a, "$1.2K", "-$300", -1);
        string all = string.Empty;
        UiRole yesterdayInk = UiRole.None;
        foreach (UiTipBlock b in card.Blocks)
        {
            foreach (UiTipLine l in b.Lines)
            {
                all += l.Left + "=" + l.Right + ";";
                if (l.Left == "Yesterday")
                {
                    yesterdayInk = l.Ink;
                }
            }
        }
        Check(all.Contains("Cash=$1.2K;") && all.Contains("Yesterday=-$300;") && yesterdayInk == UiRole.DangerText, "hud money card: the net worth card carries the game's cash and yesterday lines, a loss in the danger tone");
        UiTooltipModel plain = UiNetWorthTip.Model(en, summary, 3, null, a => "$" + a);
        string left = string.Empty;
        foreach (UiTipBlock b in plain.Blocks)
        {
            foreach (UiTipLine l in b.Lines)
            {
                left += l.Left + ";";
            }
        }
        Check(!left.Contains("Yesterday") && !left.Contains("Cash"), "hud money card: a caller that gives no figures gets the card as before");
    }

    private static void HudForeignWordsChecks()
    {
        var names = new List<string> { "BankApp" };
        var titles = new List<string> { "Bank" };
        var owners = new List<string> { "Empire Bank" };
        Check(ForeignApps.Listed("BankButton", "Bank", names, titles, 1, "Empire Bank", owners), "foreign apps: one mod's app worded like its own earlier one is one tile");
        Check(!ForeignApps.Listed("BankButton", "Bank", names, titles, 1, "Other Bank Mod", owners), "foreign apps: another mod's app worded the same keeps its own tile, so it stays reachable");
        Check(ForeignApps.Listed("BankButton", "bank ", names, titles, 1, null, owners) && ForeignApps.Listed("BankButton", "Bank", names, titles, 1),
            "foreign apps: an owner not known counts as the same, as before");
    }
}
