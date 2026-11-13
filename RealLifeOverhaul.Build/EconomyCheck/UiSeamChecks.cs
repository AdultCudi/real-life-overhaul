using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;

// The seam between the two interfaces (the interface parity sweep of 13 Nov 2026): a notice or a button of the old apps that opens an app on a tab
// or a row must land the kit's page on the same thing, mapped by what the tab is and never by its number; the one way the kit writes a time of
// day (the game's 12 hour option); a screen that faults is counted against the guard once and is asked again in the next city; what a switch
// turned off in a session leaves; and the clean-ups the guard runs for the features that hold the player. Game-free rules are run, the rest is
// held to the sources that carry it out.
internal static partial class Program
{
    private static void UiSeam()
    {
        UiSeamRoutes();
        UiSeamClock();
        UiSeamFaults();
        UiSeamSources();
    }

    private static bool Has(string text, params string[] parts) => text.Length > 0 && parts.All(p => text.Contains(p));

    // ------------------------------------------------------------------------------------------------- routes

    private static void UiSeamRoutes()
    {
        // Career: old Start, Jobs, Applications, My job, CV, Own work, Ladder.
        int[] career = { 0, 1, 2, 3, 4, 5, 6 };
        int[] kit = career.Select(WorkRoutes.CareerTab).ToArray();
        Check(kit.SequenceEqual(new[] { 0, 1, 2, 3, 6, 5, 4 }) && WorkRoutes.CareerTab(7) == -1 && WorkRoutes.CareerTab(-1) == -1,
            "seam routes: Career's old tabs map by meaning (the old CV is the kit's last tab, the old Ladder is Management), never by number (" + string.Join(",", kit) + ")");
        Check(Enumerable.Range(0, 5).Select(WorkRoutes.RolesTab).SequenceEqual(new[] { 4, 0, 1, 2, 3 }) && WorkRoutes.RolesTab(5) == -1,
            "seam routes: Open Roles' old Team, Jobs, Applicants, Market and Search are the kit's last tab and the first four");
        Check(WorkRoutes.PickId("post:12", "post:") == 12 && WorkRoutes.PickId("post:12", "app:") == -1 && WorkRoutes.PickId("post:x", "post:") == -1 && WorkRoutes.PickId(null, "post:") == -1
              && WorkRoutes.PickId("app:0", "app:") == 0 && WorkRoutes.PickId("app:-3", "app:") == -1,
            "seam routes: a pick names a row by its kind and id, and nothing else is a row");
        Check(WorkRoutes.CareerTabOfPick(1, "app:5") == WorkRoutes.Applications && WorkRoutes.CareerTabOfPick(0, "post:9") == WorkRoutes.Jobs
              && WorkRoutes.CareerTabOfPick(3, "prof:2") == WorkRoutes.Professions && WorkRoutes.CareerTabOfPick(4, null) == WorkRoutes.Cv && WorkRoutes.CareerTabOfPick(6, "day:3") == WorkRoutes.Management,
            "seam routes: a picked profession, post or application opens its own list, any other pick keeps the mapped tab");

        string pages = ModSource(@"Ui\Work\CareerPages.cs"), work = ModSource(@"Careers\CareersDirector.Work.cs"), own = ModSource(@"Careers\CareersDirector.Own.cs"),
            ladder = ModSource(@"Careers\CareersDirector.Ladder.cs"), study = ModSource(@"Careers\CareersDirector.Study.cs"), hiring = ModSource(@"Tech\TechDirector.Hiring.cs"),
            people = ModSource(@"Tech\TechDirector.People.cs");
        Check(Has(pages, "public const int Professions = 0, Jobs = 1, Applications = 2, MyJob = 3, Management = 4, Own = 5, Cv = 6, Tabs = 7;")
              && Has(work, "TabStart = 0, TabJobs = 1, TabApplications = 2, TabJob = 3, TabCv = 4") && Has(own, "TabOwn = 5") && Has(ladder, "TabLadder = 6"),
            "seam routes: the numbers the mapping assumes are the ones the old Career director and the kit's pages have");
        Check(Has(people, "TabTeam = 0") && Has(hiring, "TabJobs = 1") && Has(hiring, "TabApplicants = 2") && Has(hiring, "TabMarket = 3") && Has(hiring, "TabSearch = 4")
              && Has(study, "TabCourses = 0, TabStudying = 1, TabResults = 2, TabSkills = 3"),
            "seam routes: Open Roles and Campus keep the numbers the mapping assumes");

        // Every director's way to open an app on a tab also asks the kit's page for it.
        var wants = new (string File, string Member, string Needle)[]
        {
            (@"Careers\CareersDirector.Work.cs", "public void OpenCareer(", "CareerLive.WantOld(tab, pick);"),
            (@"Careers\CareersDirector.Study.cs", "public void OpenCampus(", "CampusLive.WantOld(tab, pick);"),
            (@"Tech\TechDirector.Roles.cs", "public void OpenRoles(", "RolesLive.WantOld(tab, pick, _crewAsked);"),
            (@"Economy\OwnBank\OwnBankDirector.cs", "public void Open(int tab)", "OwnBankLive.Want(tab);"),
            (@"Utilities\UtilitiesDirector.cs", "public bool OpenInternet()", "UtilitiesLive.Want(UtilitiesApp.InternetTab);"),
            (@"Hustles\HustleDirector.cs", "public void Open(int tab, string pick)", "MarketLive.Want(tab);"),
            (@"Markets\MarketDirector.App.cs", "public void OpenCompany(", "StocksScreen.Want(stockId);"),
            (@"Planner\PlannerDirector.cs", "private void OpenAt(", "PlannerScreen.Want(id);"),
            (@"Outlook\OutlookDirector.App.cs", "public bool Open(int tab)", "NewsLive.Economy.Want(OutlookTab.Clamp(tab));"),
            (@"Social\Flare\FlareDirector.Live.cs", "public void OpenPost(", "KitPost = id;"),
        };
        foreach (var (file, member, needle) in wants)
        {
            Check(Has(MemberText(ModSource(file), member), needle), "seam routes: " + file.Replace('\\', '/') + " " + member.Trim() + " asks the kit's page too (" + needle + ")");
        }
        Check(Has(ModSource(@"Tech\TechDirector.Roles.cs"), "_crewAsked = centre;"), "seam routes: a data centre's crew brief names its centre to the kit's page");

        // The sweep: any director that tells its old view to want a tab says so to the kit as well, unless it is listed with why not.
        string scripts = ModScripts();
        var spared = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "Economy/Banking/BankApps/BanksDirector.cs", "only a test opens Banks on a tab" },
            { "Social/Flare/FlareDirector.cs", "nothing calls Flare's Open(tab); a post is opened by OpenPost" },
            { "Economy/Brands/BrandsDirector.Actions.cs", "prepares the old view after a brand is made on the page the player is on" },
            { "Economy/Brands/BrandsDirector.Franchises.cs", "prepares the old view after a franchise step on the page the player is on" },
            { "Construction/ConstructionDirector.HireTests.cs", "a test" },
            { "Markets/MarketDirector.Bonds.cs", "a test opens the old Stocks app on a tab" },
            { "Markets/MarketDirector.Derivatives.cs", "a test opens the old Stocks app on a tab" },
            { "Markets/MarketDirector.Tests.cs", "a test" },
            { "Meta/SettingsApp.cs", "left: the kit's mod settings page takes no section from outside yet" },
            { "Meta/StaffApp.cs", "left: the kit's staff page takes no entry from outside yet" },
            { "Social/Flare/FlareDirector.Camera.cs", "left: a photo shared to Flare from another feature names a draft the kit's compose sheet cannot take yet" },
        };
        var missing = new List<string>();
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string name = file.Substring(scripts.Length + 1).Replace('\\', '/');
            if (name.StartsWith("Ui/", StringComparison.Ordinal) || spared.ContainsKey(name))
            {
                continue;
            }
            string text = File.ReadAllText(file);
            if (Regex.IsMatch(text, @"_\w*[vV]iew\??\.Want\w*\(") && !Regex.IsMatch(text, @"(Live|Screen|Economy|Instance)\.Want\w*\(|Wants\[|KitPost|\.Wanted\("))
            {
                missing.Add(name);
            }
        }
        Check(missing.Count == 0, "seam routes: every old app opened on a tab or a row is asked of the kit's page too" + (missing.Count > 0 ? "; not: " + string.Join(", ", missing) : string.Empty));

        // The page takes the request once, in its own numbering.
        Check(Has(ModSource(@"Ui\Money\MoneyHost.cs"), "asked.TakeWanted()", "_model.Tab = wanted;") && Has(ModSource(@"Ui\Money\UtilitiesScreen.cs"), "asked.TakeWanted()", "ShowTab(wanted);")
              && Has(ModSource(@"Ui\Money\OwnBankLive.cs"), "IUiTabWant", "public static void Want(int tab)") && Has(ModSource(@"Ui\Money\UtilitiesLive.cs"), "IUiTabWant", "public static void Want(int tab)"),
            "seam routes: Your bank and Utilities take a tab asked for once, as their screens lay out");
        Check(Has(ModSource(@"Ui\Stocks\StocksScreen.cs"), "_wantedStock > 0", "OpenCompany(asked);") && Has(ModSource(@"Ui\Life\PlannerScreen.cs"), "_hasWanted", "_flow.ShowEntry(_wanted);")
              && Has(ModSource(@"Ui\Life\PlannerFlow.cs"), "public bool ShowEntry(EntryId id)", "OpenEntry(_items[i]);"),
            "seam routes: Stocks opens the company and the Planner the entry a notice named");
        Check(Has(ModSource(@"Ui\Flare\FlareScreen.cs"), "TakeKitPost()", "OpenThread(post)") && Has(ModSource(@"Ui\Life\GalleryScreen.cs"), "_app.TakeLatestForKit()", "LatestForKitAsked")
              && Has(ModSource(@"Photos\GalleryApp.cs"), "_latestKitAt", "internal bool TakeLatestForKit()"),
            "seam routes: Flare opens the post of a notice and the Gallery the photo just taken");
        string careerLive = ModSource(@"Ui\Work\CareerLive.cs"), campusLive = ModSource(@"Ui\Work\CampusLive.cs"), rolesLive = ModSource(@"Ui\Work\RolesLive.cs");
        Check(Has(careerLive, "public static void WantOld(int oldTab, string pick)", "private void ApplyPick(WorkView view)", "view.Row = i;")
              && Has(campusLive, "public static void WantOld(int tab, string pick)", "private bool ApplyPick(WorkView view, int tab, WorkTab built)")
              && Has(rolesLive, "public static void WantOld(int oldTab, string pick, string crewPlace)", "view.Sub = 1;", "view.Set(2, i);"),
            "seam routes: Career, Campus and Open Roles open on the row a pick names");
        string pool = ModSource(@"Careers\PeoplePoolGame.cs");
        Check(Has(MemberText(pool, "internal static bool Open(int id)"), "if (Ui.ShellPages.OpenPeople())", "Ui.PopulationScreen.Wanted = null;"),
            "seam routes: a person kept for the people page is dropped when the page is not offered (the old interface has none)");
    }

    // ------------------------------------------------------------------------------------------------- the clock

    private static void UiSeamClock()
    {
        Check(UiClockRules.Format(0, 5, false) == "00:05" && UiClockRules.Format(13, 30, false) == "13:30" && UiClockRules.Format(23, 59, false) == "23:59",
            "seam clock: the 24 hour clock reads as the game's top bar does");
        Check(UiClockRules.Format(0, 5, true) == "12:05 AM" && UiClockRules.Format(12, 0, true) == "12:00 PM" && UiClockRules.Format(13, 30, true) == "01:30 PM"
              && UiClockRules.Format(23, 59, true) == "11:59 PM" && UiClockRules.Format(9, 7, true) == "09:07 AM",
            "seam clock: the 12 hour clock reads as the game's own (midnight is 12:00 AM, noon 12:00 PM, the hour has two digits)");
        Check(UiClockRules.Format(0, false) == "00:00" && UiClockRules.Format(13 * 60 + 5, true) == "01:05 PM" && UiClockRules.Format(-60, false) == "23:00" && UiClockRules.Format(25, 61, false) == "01:59",
            "seam clock: minutes of the day, and a figure out of range, still read as a time");
        string clock = ModSource(@"Ui\UiClock.cs"), game = ModSource(@"Ui\UiGame.cs");
        Check(Has(clock, "TimeHelper.use12h", "public static string At(int hour, int minute)", "public static string AtMinute(int minuteOfDay)"),
            "seam clock: the kit's one helper reads the game's 12 hour option");
        Check(Has(game, "UiClock.At(now.Hour, (int)now.Minute)") && !game.Contains("now.Hour.ToString(\"00\""), "seam clock: the status bar's time goes through the helper");
    }

    // ------------------------------------------------------------------------------------------------- faults

    private static void UiSeamFaults()
    {
        UiFaultCount.Reset();
        Check(UiFaultCount.Counts("pause") && !UiFaultCount.Counts("pause") && UiFaultCount.Counts("buy") && UiFaultCount.Counts(null) && UiFaultCount.Counts(null) && UiFaultCount.Counts(""),
            "seam faults: a screen counts against the guard the first time it faults, never again, and a fault with no screen always counts");
        UiFaultCount.Reset();
        Check(UiFaultCount.Counts("pause"), "seam faults: a new session counts it again");
        UiBreaker.Reset();
        UiBreaker.Trip("pause");
        Check(UiBreaker.Tripped("pause") && UiBreaker.Untrip("pause") && !UiBreaker.Tripped("pause") && !UiBreaker.Untrip("pause") && !UiBreaker.Untrip(null),
            "seam faults: a screen's breaker can be closed again, once");
        UiBreaker.Reset();
    }

    // ------------------------------------------------------------------------------------------------- sources

    private static void UiSeamSources()
    {
        string host = ModSource(@"Ui\Host\UiGameHost.cs"), uiGame = ModSource(@"Ui\UiGame.cs"), director = ModSource(@"Ui\UiDirector.cs"), uiHost = ModSource(@"Ui\UiHost.cs");
        string read = MemberText(uiGame, "public void Read(ref UiInputState state)");
        Check(Has(read, "if (Guard.Missing(e))", "_failed = true;", "_faults++", "_pausedUntil") && !Regex.IsMatch(read, @"catch \(Exception e\)\s*\{\s*_failed = true;"),
            "seam faults: the keys and the pad are given up for the session only for a missing member; any other fault is said once and read again after a rest");
        string rearm = MemberText(host, "public void Rearm()"), fall = MemberText(host, "public void Fallback(Exception e)");
        Check(Has(rearm, "_faults < UiPieces.Tries", "_ownTrip", "UiBreaker.Untrip(_screen.Id)", "_gaveUp = false;") && Has(fall, "_faults++", "Guard.Tell(", "reallife_ui_note_piece_off"),
            "seam faults: a screen that threw once is asked again in the next city (after the second it stays the game's own), and the player is told once");
        Check(Has(host, "_conceal.Show();   // the game's app is seen behind its own box, not an empty area", "_conceal.Hide();"),
            "seam faults: a box of the game's over a kit app shows the game's app behind it, and the kit comes back as the box closes");
        Check(Has(uiHost, "if (UiFaultCount.Counts(LastFault))", "Guard.Fail(FrameGuard, e, this);", "public bool StepAside;", "SteppedAside();")
              && Has(ModSource(@"Ui\UiDirector.Tests.cs"), "host.StepAside = true;"),
            "seam faults: one screen is one fault for the whole interface's guard, and a card on a canvas of its own steps aside for a box of the game's");
        string closeCards = MemberText(director, "private static void CloseCards()");
        Check(Has(closeCards, "SessionKit.Hide(null)", "BugReportBox.Close", "ShellModBox.Close", "ViewfinderHost.Hide") && Has(MemberText(director, "internal static void LetGoAll(string why)"), "CloseCards();")
              && Has(director, "RemoveAll(SwitchOffWhy);", "CloseCards();   // the cards stand on canvases of their own"),
            "seam switch: a switch turned off in play and a guard stop both take the desk card, the report card, the mod box and the viewfinder away");
        Check(Has(ModSource(@"Ui\BuildMode\BuildKits.cs"), "why != UiDirector.SwitchOffWhy") && Has(director, "internal const string SwitchOffWhy = \"the switch is off\";"),
            "seam switch: a switch turned off mid build keeps the face that is up until Build mode is left, a stop or a city going does not");
        string take = MemberText(ModSource(@"Ui\Phone\PhoneKit.cs"), "private static void Take()"), leave = MemberText(ModSource(@"Ui\Phone\PhoneKit.cs"), "private static void Leave()");
        Check(Has(take, "Keys.Hold();") && Has(leave, "Keys.Release();") && Has(ModSource(@"Core\Keys.cs"), "public static void Hold() => _held++;", "get => _suspended || _held > 0;"),
            "seam keys: the phone's hold on the mod's keys is a counted one, so a field that types meanwhile never leaves the keys stuck");
        string windows = ModSource(@"Ui\Compat\UiForeignWindows.cs");
        Check(Has(windows, "Cursor.lockState = _wasLock;", "Cursor.visible = _wasVisible;", "LiftedTo[i]", "!windowUp"),
            "seam windows: a foreign window's cursor is given back once no window stands, and a canvas the mod moved since is left where the mod put it");
        Check(Regex.IsMatch(MemberText(ModSource(@"Ui\Hud\HudStatusGame.cs"), "public static bool FastOffered(UIs uis)"), @"try\s*\{[\s\S]*HasFastForward\(\)[\s\S]*catch \(Exception e\)"),
            "seam faults: the bar's per-frame read of an activity's fast forward never takes the bar off the screen");

        string phone = ModSource(@"Ui\Phone\PhoneKit.cs");
        Check(Has(MemberText(phone, "internal static void Frame("), "Unfold();") &&Has(MemberText(phone, "private static void Unfold()"), "UiWidgets.Hand(_collapse)", "_window.ChangeState(false)"),
            "seam phone: the phone key (and the pad's select) brings a put away phone back, by the game's own fold button");

        // The clean-ups the guard runs for the features that hold the player.
        var cleanups = new (string Source, string Needle, string Why)[]
        {
            (@"Meta\FeatureList.Life.cs", ".Cleanup(() => Law.LawDirector.Instance?.StopAll())", "law"),
            (@"Meta\FeatureList.Life.cs", ".Cleanup(() => TransitDirector.Instance?.GivePlayerBack())", "transport"),
            (@"Meta\FeatureList.Life.cs", ".Cleanup(() => SocialDirector.Instance?.StopAll())", "social"),
            (@"Meta\FeatureList.Life.cs", ".Cleanup(() => LicenceDirector.Instance?.StopAll())", "licences"),
            (@"Meta\FeatureList.Life.cs", ".Cleanup(() => CivicDirector.Instance?.StopAll())", "civic"),
            (@"Meta\FeatureList.Life.cs", ".Cleanup(() => Fame.FameDirector.Instance?.StopAll())", "standing"),
            (@"Meta\FeatureList.cs", ".Cleanup(() => PlayerKnockdown.StopAll())", "ragdoll"),
            (@"Permadeath\PermadeathSettings.cs", ".Cleanup(() => PermadeathGame.LetGoRehearsal())", "permadeath"),
        };
        foreach (var (source, needle, why) in cleanups)
        {
            Check(ModSource(source).Contains(needle), "seam guard: the " + why + " feature has a clean-up the guard runs");
        }
        Check(Has(MemberText(ModSource(@"Transport\TransitDirector.cs"), "private void Update()"), "GivePlayerBack();")
              && Has(MemberText(ModSource(@"Ragdoll\PlayerKnockdown.cs"), "private void Update()"), "Restore(stand: true);")
              && Has(MemberText(ModSource(@"Licences\LicenceDirector.cs"), "private void Update()"), "Shutdown();")
              && Has(MemberText(ModSource(@"Social\SocialDirector.cs"), "private void Update()"), "Body?.Release();")
              && Has(MemberText(ModSource(@"Law\LawDirector.cs"), "private void OnDisable()"), "_custody?.ForceRelease("),
            "seam guard: the frame in which a switched off feature finds itself off puts the player down (the bus, the knock down, the school desk, the scene, the cell)");
        Check(Has(MemberText(ModSource(@"Permadeath\PermadeathGame.cs"), "internal static void LetGoRehearsal()"), "!GameMembers.PlayerDead", "Hold(false, false);"),
            "seam guard: a rehearsal's walk hold is let go and a death's stays");
    }
}
