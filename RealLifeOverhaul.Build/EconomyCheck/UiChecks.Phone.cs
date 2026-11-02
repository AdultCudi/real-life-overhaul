using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;

// The kit phone (Scripts/Ui/UiPhone.cs, Phone/PhoneScreen.cs, Phone/PhoneItems.cs, PhoneLooks.cs, Phone/PhoneKit.cs): every
// state laid out by its own screen and checked as every kit screen is; the nav bar with the page arrows in the same place on
// every page and in every folder (the owner, 1 Nov 2026: "The next button must be at the bottom instead it is random"); every
// app on exactly one page; pages round; Back out of a folder, then the keys; the next arrow's badge; the phone's own height
// whatever another mod does to the game's phone (a player's log: a mod grew it by 3,377 units for 37 apps) and never taller
// than the screen; every app's one glyph and colour, apart from its neighbours' and readable under colour blindness; the
// sources: the grid hidden by its own group alone, presses through the buttons, nothing sized from the game's container.
internal static partial class Program
{
    // The phone's states, each through PhoneScreen as the game lays it.
    static partial void UiCasesPhone(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id != "phone")
        {
            return;
        }
        void Add(string name, Func<UiWords, PhoneScreen> make) =>
            cases.Add(new UiScreenCase { Def = def, Name = "phone (" + name + ")", Title = "Phone", Lay = (s, f, dark, k, t) => make(t).Lay(s, f, dark, k) });
        Add("home", t => UiSamples.Phone(t));
        Add("home, page 2", t => UiSamples.Phone(t, -1, 1));
        Add("a folder", t => UiSamples.Phone(t, PhoneLayoutRules.Business));
        Add("a folder, page 2", t => UiSamples.Phone(t, PhoneLayoutRules.Business, 1, true, false, true, false, true, UiSamples.PhoneApps(t, true, null, null, 2)));
        Add("one page", t => UiSamples.Phone(t, PhoneLayoutRules.Work));
        Add("the radio, tall", t => UiSamples.Phone(t, -1, 0, true));
        Add("the radio and the driver", t => UiSamples.Phone(t, -1, 0, true, true));
        Add("nothing coming", t => UiSamples.Phone(t, -1, 0, false, false, false));
        Add("folded down", t => UiSamples.Phone(t, -1, 0, false, false, true, true));
        Add("no other mods", t => UiSamples.Phone(t, -1, 0, false, false, true, false, false));
    }

    private static void PhoneKitChecks()
    {
        int before = _run, failedBefore = _failed;
        var watch = Stopwatch.StartNew();
        PhoneLooksChecks();
        PhoneColourChecks();
        PhoneHeightChecks();
        PhoneScreenChecks();
        PhoneNavStill();
        PhoneNextKept();
        PhoneShellApps();
        PhoneKitSources();
        PhoneKitTexts();
        PhoneKitTiming();
        Console.WriteLine("  Kit phone: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    // The frame's app row and All apps read the phone's own model: the row is the home screen's first apps in the phone's
    // order, every app stands once in All apps (a folder's in its folder), other mods' in Other mods with their pictures.
    private static void PhoneShellApps()
    {
        PhoneItems items = UiSamples.PhoneApps((key, english) => english, true);
        var apps = new List<UiShellApp>();
        int version = UiShellApps.FromPhone(items, apps, PagerRules.PhoneTiles);
        var home = new int[64];
        int n = items.Home(home);
        var want = new List<string>();
        for (int e = 0; e < n && want.Count < PagerRules.PhoneTiles; e++)
        {
            if (home[e] >= 0)
            {
                want.Add(items.Items[home[e]].Id);
            }
        }
        List<string> row = apps.Where(a => a.InRow).Select(a => a.Id).ToList();
        Check(row.SequenceEqual(want) && row.Count == PagerRules.PhoneTiles && row[0] == "RealLifePlanner" && row[1] == "RealLifeFlare",
            "phone and frame: the app row is the phone's home screen's first apps in its own order (" + string.Join(", ", row) + ")");
        bool once = apps.Count == items.Count && apps.Select(a => a.Id).Distinct().Count() == items.Count;
        bool looks = true, folders = true;
        foreach (UiShellApp a in apps)
        {
            int i = items.IndexOf(a.Id);
            looks &= i >= 0 && a.Icon == (items.Items[i].Icon != UiIcon.None ? items.Items[i].Icon : PhoneLooks.OtherMods) && a.Picture == items.Items[i].Picture;
            folders &= a.Kind == UiShellKind.Other ? a.Folder == UiShellApps.Other : items.Items[i].Folder < 0 || a.Folder == items.Items[i].Folder;
        }
        UiShellApp bank = apps.FirstOrDefault(a => a.Id == "EmpireBankApp");
        Check(once && looks && folders && bank.Picture == UiSamples.PictureBank && bank.Folder == UiShellApps.Other && bank.Icon == PhoneLooks.OtherMods,
            "phone and frame: All apps holds every app of the phone once, with the phone's glyph and colour, in the folder the phone keeps it in, other mods' with their own pictures");
        items.Begin();
        items.End(true);
        Check(UiShellApps.FromPhone(items, new List<UiShellApp>(), PagerRules.PhoneTiles) != version, "phone and frame: the frame's list moves with the phone's model");
    }

    // ---------------------------------------------------------------- the looks

    private static readonly string[] GameApps = { "Persona", "Contacts", "MyEmployees", "BizMan", "EconoView", "VoogleMaps", "MarketInsider", "Rivals" };

    private static void PhoneLooksChecks()
    {
        Check(PhoneLooks.Ids.Length == PhoneLooks.Glyphs.Length, "phone looks: a glyph for each app (" + PhoneLooks.Ids.Length + " apps)");
        var missing = GameApps.Concat(PagerRules.ModOrder).Where(id => PhoneLooks.Glyph(id) == UiIcon.None).ToList();
        Check(missing.Count == 0, "phone looks: the game's eight and every app of the mod's have their glyph (missing: " + string.Join(", ", missing) + ")");
        var all = PhoneLooks.Glyphs.Concat(PhoneLooks.FolderGlyphs).Concat(new[] { PhoneLooks.AllApps, PhoneLooks.OtherMods }).ToList();
        var twice = all.GroupBy(g => g).Where(g => g.Count() > 1).Select(g => g.Key.ToString()).ToList();
        Check(twice.Count == 0 && all.All(g => g != UiIcon.None && UiIconRaster.Has(g)),
            "phone looks: two apps, folders or tabs never share a glyph, and each is an icon of the family (shared: " + string.Join(", ", twice) + ")");
        Check(PhoneLooks.FolderGlyphs.Length == PhoneLayoutRules.FolderCount && PhoneLooks.Folder(PhoneLayoutRules.Setup, out UiIcon setup, out UiRole tint) && setup == UiIcon.Wrench
              && tint == UiIconTints.Of(UiIcon.Wrench) && !PhoneLooks.Folder(9, out _, out _),
            "phone looks: each of the five folders has its glyph in its own colour, and no folder past them");
        Check(!PhoneLooks.Of("BAMP_Chat", out UiIcon none, out UiRole noTint) && none == UiIcon.None && noTint == UiRole.None && PhoneLooks.Glyph(null) == UiIcon.None,
            "phone looks: another mod's app has no glyph of the kit's: it shows its own picture");
        bool own = true;
        for (int i = 0; i < PhoneLooks.Ids.Length; i++)
        {
            own &= PhoneLooks.Of(PhoneLooks.Ids[i], out UiIcon g, out UiRole r) && r == UiIconTints.Of(g) && r != UiRole.None;
        }
        Check(own, "phone looks: an app's colour is its glyph's own (one glyph, one colour, on every screen)");
    }

    // ---------------------------------------------------------------- the colours

    private static readonly double[][,] Eyes =
    {
        new[,] { { 1.0, 0, 0 }, { 0, 1.0, 0 }, { 0, 0, 1.0 } },
        new[,] { { 0.152286, 1.052583, -0.204868 }, { 0.114503, 0.786281, 0.099216 }, { -0.003882, -0.048116, 1.051998 } },
        new[,] { { 0.367322, 0.860646, -0.227968 }, { 0.280085, 0.672501, 0.047413 }, { -0.011820, 0.042940, 0.968881 } },
        new[,] { { 1.255528, -0.076749, -0.178779 }, { -0.078411, 0.930809, 0.147602 }, { 0.004733, 0.691367, 0.303900 } },
    };

    private static double Apart(UiRole a, UiRole b, int eye) => LabDistance(SeenLab(UiTokens.Colour(a), Eyes[eye]), SeenLab(UiTokens.Colour(b), Eyes[eye]));

    private static double Worst(UiRole a, UiRole b)
    {
        double least = double.MaxValue;
        for (int e = 1; e < Eyes.Length; e++)
        {
            least = Math.Min(least, Apart(a, b, e));
        }
        return least;
    }

    private static UiRole TintOf(string id) => PhoneLooks.Of(id, out _, out UiRole tint) ? tint : UiRole.None;

    private static void PhoneColourChecks()
    {
        // Each app's glyph reads on its slate tile: 3:1 as an icon must, to every eye.
        float faintest = float.MaxValue;
        string faint = null;
        foreach (string id in PhoneLooks.Ids)
        {
            float c = UiContrast(TintOf(id), UiRole.Tile, true);
            if (c < faintest)
            {
                faintest = c;
                faint = id;
            }
        }
        Check(faintest >= UiCheckRules.LargeContrast, "phone colours: every app's glyph reads on its tile (faintest " + faint + ", " + faintest.ToString("0.00") + ":1; at least 3)");
        // In one folder no two apps share a colour, and every two stand apart to the eye.
        string[][] folders = Enumerable.Range(0, PhoneLayoutRules.FolderCount)
            .Select(f => PagerRules.ModOrder.Where(id => PhoneLayoutRules.DefaultOf(id) == f && id != "RealLifeLedger").ToArray()).ToArray();
        int sameInFolder = 0;
        double closest = double.MaxValue;
        string close = null;
        foreach (string[] apps in folders)
        {
            for (int i = 0; i < apps.Length; i++)
            {
                for (int j = i + 1; j < apps.Length; j++)
                {
                    UiRole a = TintOf(apps[i]), b = TintOf(apps[j]);
                    sameInFolder += a == b ? 1 : 0;
                    double d = Apart(a, b, 0);
                    if (d < closest)
                    {
                        closest = d;
                        close = apps[i] + " and " + apps[j];
                    }
                }
            }
        }
        Check(sameInFolder == 0 && closest >= 15.0, "phone colours: no two apps in one folder share a colour, and every two stand apart (closest " + close + ", "
            + closest.ToString("0.0") + "; at least 15)");
        // Tiles beside each other, across or down, on the home screen's first page and on every folder's page, and each
        // pair side by side in the status bar's app row: apart to the eye, and apart under colour blindness.
        var home = new List<string> { "RealLifePlanner", "RealLifeFlare" };
        home.AddRange(GameApps);
        var rows = new List<string[]> { home.ToArray() };
        rows.AddRange(folders);
        double near = double.MaxValue, nearWorst = double.MaxValue;
        string nearPair = null, worstPair = null;
        foreach (string[] page in rows)
        {
            for (int i = 0; i < page.Length; i++)
            {
                foreach (int j in new[] { i + 1, i + UiPhone.Across })
                {
                    if (j >= page.Length || j == i + 1 && i % UiPhone.Across == UiPhone.Across - 1 && page != rows[0])
                    {
                        continue;   // the next row's first tile is no neighbour on a page; in the status bar's row it is
                    }
                    UiRole a = TintOf(page[i]), b = TintOf(page[j]);
                    double d = Apart(a, b, 0), w = Worst(a, b);
                    if (d < near)
                    {
                        near = d;
                        nearPair = page[i] + " and " + page[j];
                    }
                    if (w < nearWorst)
                    {
                        nearWorst = w;
                        worstPair = page[i] + " and " + page[j];
                    }
                }
            }
        }
        // On the home screen's page no two apps share a colour at all, and no app wears a state's colour (the energy's amber
        // is the Utilities' own glyph): a green or a red glyph would read as good or bad news.
        var homeTints = home.Select(TintOf).ToList();
        Check(homeTints.Distinct().Count() == homeTints.Count, "phone colours: no two apps on the home screen's first page share a colour ("
            + string.Join(", ", home.Where((id, i) => homeTints.IndexOf(homeTints[i]) != i)) + ")");
        var stated = PhoneLooks.Ids.Where(id => id != "RealLifeOverhaulUtilities" && (TintOf(id) == UiRole.Ok || TintOf(id) == UiRole.Danger || TintOf(id) == UiRole.Warn))
            .Concat(Enumerable.Range(0, PhoneLayoutRules.FolderCount).Where(f => PhoneLooks.Folder(f, out _, out UiRole r) && (r == UiRole.Ok || r == UiRole.Danger || r == UiRole.Warn)).Select(f => "folder " + f))
            .ToList();
        Check(stated.Count == 0, "phone colours: no app or folder wears a state's colour, good, bad or warning (" + string.Join(", ", stated) + ")");
        Check(near >= 20.0, "phone colours: tiles side by side never look alike (closest " + nearPair + ", " + near.ToString("0.0") + "; at least 20)");
        Check(nearWorst >= 8.0, "phone colours: tiles side by side stay apart under protanopia, deuteranopia and tritanopia (closest " + worstPair + ", "
            + nearWorst.ToString("0.0") + "; at least 8, the chart's closest pair)");
    }

    // ---------------------------------------------------------------- the phone's own height

    private static void PhoneHeightChecks()
    {
        Check(UiPhone.DeviceHeight(false, 1080f) == UiPhone.HeightShort && UiPhone.DeviceHeight(true, 1080f) == UiPhone.HeightTall
              && UiPhone.HeightShort * UiTokens.CanvasPerPx >= 1050f + 61f && UiPhone.HeightTall * UiTokens.CanvasPerPx >= 1230f + 61f
              && UiPhone.Width * UiTokens.CanvasPerPx >= 627.699f + 21f,
            "phone height: the game's phone with its frame (1,050 units, 1,230 with the radio row, its frame's 61 above and 21 across), covered whole");
        // The game's container as other mods leave it: Going Public adds a row (225), a mod that shows every button grows it
        // by thousands. The kit's height is asked of the radio and the screen alone, so none of them reaches it.
        bool held = true;
        foreach (float container in new[] { 1050f, 1230f, 1275f, 1455f, 4427f, 9000f })
        {
            held &= UiPhone.DeviceHeight(container > 1230f && container < 1300f, 1080f) <= UiPhone.HeightTall;
        }
        Check(held, "phone height: whatever another mod makes of the game's phone (a row more, 3,377 units more for 37 apps), the kit phone keeps its own height");
        bool fits = true;
        foreach (float screen in new[] { 1080f, 960f, 720f, 600f, 540f, 480f })
        {
            foreach (bool radio in new[] { false, true })
            {
                float h = UiPhone.DeviceHeight(radio, screen);
                fits &= h <= screen - UiPhone.ScreenMargin && h > 0f && UiPhone.PerPage(h, radio) >= UiPhone.Across;
            }
        }
        Check(fits, "phone height: never taller than the screen (the interface at 200% leaves 540 px), and a row of apps always fits");
        string kit = PhoneRead("Phone/PhoneKit.cs");
        string size = PhoneBody(kit, "private static void Size()") ?? string.Empty;
        Check(size.Contains("UiPhone.DeviceHeight(radio, screen)") && !size.Contains("_container") && !kit.Contains("_container.rect") && !kit.Contains("_container.sizeDelta"),
            "phone height: the kit's frame is sized from the radio and the screen, never from the game's container");
    }

    // ---------------------------------------------------------------- the screen

    private sealed class Pressed : IPhoneActions
    {
        public readonly List<int> Opened = new List<int>();
        public int Collapses, Skips, Volume, Drivers, Left;
        public bool Focus;

        public void Open(int item) => Opened.Add(item);
        public void Collapse() => Collapses++;
        public void RadioSkip() => Skips++;
        public void RadioVolume(int step) => Volume += step;
        public void Driver() => Drivers++;
        public bool Focused => Focus;

        public void Leave()
        {
            Left++;
            Focus = false;
        }
    }

    private static PhoneScreen PhoneWith(PhoneItems items, Pressed actions, UiScene s)
    {
        var screen = new PhoneScreen(items, actions, (key, english) => english);
        screen.Readings.Time = "14:32";
        screen.Lay(s, UiFrame.FullMenu(16f / 9f, "Phone"), true, UiDefaultKeys.Keyboard);
        return screen;
    }

    private static void PhoneScreenChecks()
    {
        PhoneItems items = UiSamples.PhoneApps((key, english) => english, true);
        var acts = new Pressed();
        var s = new UiScene();
        PhoneScreen screen = PhoneWith(items, acts, s);
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Phone");
        // Every app on exactly one page of exactly one view.
        var seen = new int[items.Count];
        int folderTiles = 0;
        for (int folder = -1; folder < PhoneLayoutRules.FolderCount; folder++)
        {
            screen.Show(folder, 0);
            screen.Lay(s, frame, true, UiDefaultKeys.Keyboard);
            int pages = screen.Pages;
            for (int p = 0; p < pages; p++)
            {
                screen.Show(folder, p);
                screen.Lay(s, frame, true, UiDefaultKeys.Keyboard);
                for (int i = 0; i < screen.Model.Tiles; i++)
                {
                    UiPhoneApp tile = screen.Model.Apps[i];
                    if (tile.Folder)
                    {
                        folderTiles++;
                        continue;
                    }
                    int at = Array.FindIndex(items.Items, 0, items.Count, it => it.Title == tile.Label);
                    if (at >= 0)
                    {
                        seen[at]++;
                    }
                }
            }
        }
        Check(seen.All(n => n == 1) && folderTiles == PhoneLayoutRules.FolderCount,
            "phone screen: every app of " + items.Count + " on exactly one page of the home screen or one folder, and each folder's tile once ("
            + string.Join(", ", Enumerable.Range(0, items.Count).Where(i => seen[i] != 1).Select(i => items.Items[i].Id + " " + seen[i])) + ")");
        // The home screen's order: the two kept first, the game's eight, the folders, other mods' last.
        screen.Show(-1, 0);
        screen.Lay(s, frame, true, UiDefaultKeys.Keyboard);
        var order = new int[64];
        int homeCount = items.Home(order);
        Check(homeCount > 11 && items.Items[order[0]].Id == "RealLifePlanner" && items.Items[order[1]].Id == "RealLifeFlare" && items.Items[order[2]].Id == "Persona"
              && items.Items[order[9]].Id == "Rivals" && order[10] == -1 - PhoneLayoutRules.Money && items.Items[order[homeCount - 1]].Kind == PhoneItemKind.Other
              && screen.Model.Apps[0].Label == "Planner",
            "phone screen: the home screen leads with the Planner and Flare, then the game's eight, then the folders, other mods' last");
        // Pages round, both ways; the next arrow carries the other pages' numbers.
        int total = screen.Pages;
        Check(total >= 2 && screen.Turn(-1) && SafeLay(screen, s, frame) && screen.Page == total - 1, "phone screen: back from the first page is the last");
        Check(screen.Turn(1) && SafeLay(screen, s, frame) && screen.Page == 0, "phone screen: on from the last page is the first");
        int onPage = 0, all = 0;
        for (int i = 0; i < screen.Model.Tiles; i++)
        {
            onPage += screen.Model.Apps[i].Badge;
        }
        for (int i = 0; i < items.Count; i++)
        {
            all += items.Items[i].Badge;
        }
        Check(screen.Model.OtherPagesBadge == all - onPage && screen.Model.OtherPagesBadge > 0,
            "phone screen: the next arrow carries the numbers of the pages not shown (" + screen.Model.OtherPagesBadge + " of " + all + ")");
        // A tile opens its app through the game file; a folder's tile opens the folder; Back leaves it for the page it was on.
        screen.Show(-1, 1);
        screen.Lay(s, frame, true, UiDefaultKeys.Keyboard);
        int folderAt = Array.FindIndex(screen.Model.Apps, 0, screen.Model.Tiles, t => t.Folder);
        Check(folderAt >= 0 && screen.Press(UiPhone.AppCommand, folderAt) && screen.Folder >= 0 && acts.Opened.Count == 0, "phone screen: a folder's tile opens the folder, no app");
        screen.Lay(s, frame, true, UiDefaultKeys.Keyboard);
        Check(screen.Press(UiPhone.AppCommand, 0) && acts.Opened.Count == 1 && items.Items[acts.Opened[0]].Folder == screen.Folder,
            "phone screen: an app's tile is pressed through the game file, the app of that tile");
        Check(screen.Back() && screen.Folder < 0 && SafeLay(screen, s, frame) && screen.Page == 1, "phone screen: Back leaves the folder for the home screen's page it was on");
        Check(!screen.Back() && acts.Left == 0, "phone screen: Back at home without the keys is the game's");
        acts.Focus = true;
        Check(screen.Back() && acts.Left == 1, "phone screen: Back at home with the keys gives them back to the game");
        Check(screen.Press(UiPhone.ControlCommand, UiPhone.Collapse) && acts.Collapses == 1 && screen.Press(UiPhone.ControlCommand, UiPhone.RadioQuieter) && acts.Volume == -1
              && screen.Press(UiPhone.ControlCommand, UiPhone.RadioSkip) && acts.Skips == 1 && screen.Press(UiPhone.ControlCommand, UiPhone.DriverButton) && acts.Drivers == 1,
            "phone screen: the chevron, the radio and the private driver press the game's own controls");
        // Focus never starts on the chevron: the first app has it.
        screen.Show(-1, 0);
        screen.Lay(s, frame, true, UiDefaultKeys.Keyboard);
        int firstNode = UiFocusRules.First(s);
        Check(firstNode >= 0 && s.Nodes[firstNode].Command == UiPhone.AppCommand && s.Nodes[firstNode].Arg == 0, "phone screen: focus starts on the first app, never on the fold-down chevron");
        // A folder emptied while open closes on its own; with the folders off every app is on the home screen.
        var bare = new PhoneItems();
        bare.Begin();
        for (int i = 0; i < items.Count; i++)
        {
            bare.Add() = items.Items[i];
            bare.Items[i].Folder = -1;
            bare.Items[i].First = false;
        }
        bare.End(true);
        PhoneScreen plain = PhoneWith(bare, new Pressed(), s);
        plain.Show(PhoneLayoutRules.Money, 0);
        plain.Lay(s, frame, true, UiDefaultKeys.Keyboard);
        Check(plain.Folder < 0, "phone screen: a folder with no app left shows the home screen");
        Check(bare.Home(new int[64]) == bare.Count, "phone screen: with the folders off every app is on the home screen");
    }

    private static bool SafeLay(PhoneScreen screen, UiScene s, UiFrame frame)
    {
        screen.Lay(s, frame, true, UiDefaultKeys.Keyboard);
        return true;
    }

    // The nav bar's arrows and the chevron stand in one place on every page and in every folder, as on the game's phone of
    // the same height: nothing on the phone moves when the page, the folder or the dock changes.
    // What comes next keeps its title and a row whatever else the game shows: the radio row, the private driver, both.
    private static void PhoneNextKept()
    {
        var s = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Phone");
        string lost = null;
        foreach (bool radio in new[] { false, true })
        {
            foreach (bool driver in new[] { false, true })
            {
                PhoneScreen screen = UiSamples.Phone((key, english) => english, -1, 0, radio, driver);
                screen.Lay(s, frame, true, UiDefaultKeys.Keyboard);
                bool title = false, row = false;
                for (int i = 0; i < s.ItemCount; i++)
                {
                    title |= s.Items[i].Kind == UiKind.Text && s.Items[i].Text == "Up next";
                    row |= s.Items[i].Kind == UiKind.Text && s.Items[i].Text == UiSamples.PhoneNextRows[0].Value;
                }
                if (!title || !row)
                {
                    lost = lost ?? (radio ? "with the radio" : "without the radio") + (driver ? " and the driver" : string.Empty);
                }
            }
        }
        Check(lost == null, "phone screen: what comes next keeps its title and its first row with the radio, the private driver, both or neither (lost " + (lost ?? "nowhere") + ")");
    }

    private static void PhoneNavStill()
    {
        var s = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Phone");
        string[] said = null;
        int views = 0, moved = 0;
        string first = null;
        foreach (bool radio in new[] { false, true })
        {
            foreach (int folder in new[] { -1, PhoneLayoutRules.Business, PhoneLayoutRules.Work, PhoneLayoutRules.Money })
            {
                for (int page = 0; page < 3; page++)
                {
                    PhoneScreen screen = UiSamples.Phone((key, english) => english, folder, page, radio);
                    screen.Lay(s, frame, true, UiDefaultKeys.Keyboard);
                    var here = new[] { NodeAt(s, UiPhone.PageCommand, -1), NodeAt(s, UiPhone.PageCommand, 1), NodeAt(s, UiPhone.ControlCommand, UiPhone.Collapse), radio ? "radio" : "" };
                    views++;
                    if (said == null || said[3] != here[3])
                    {
                        said = here;
                        continue;
                    }
                    if (!said.SequenceEqual(here))
                    {
                        moved++;
                        first ??= (folder < 0 ? "home" : "folder " + folder) + " page " + page + ": " + string.Join(" / ", here) + " against " + string.Join(" / ", said);
                    }
                }
            }
        }
        Check(moved == 0, "phone nav bar: the page arrows and the chevron stand in the same place in " + views + " views, home and three folders, every page, with the radio and without: "
            + moved + " moved" + (first != null ? " (" + first + ")" : string.Empty));
    }

    private static string NodeAt(UiScene s, int command, int arg)
    {
        int n = s.FindNode(command, arg);
        return n < 0 ? "none" : s.Nodes[n].X + "," + s.Nodes[n].Y + "," + s.Nodes[n].W + "," + s.Nodes[n].H;
    }

    // ---------------------------------------------------------------- the sources

    private static string PhoneRead(string underUi)
    {
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui", underUi));
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static string PhoneBody(string source, string head)
    {
        int at = source.IndexOf(head, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }
        int open = source.IndexOf('{', at), depth = 0;
        for (int i = open; i >= 0 && i < source.Length; i++)
        {
            depth += source[i] == '{' ? 1 : source[i] == '}' ? -1 : 0;
            if (depth == 0)
            {
                return source.Substring(at, i - at + 1);
            }
        }
        return null;
    }

    private static void PhoneKitSources()
    {
        string kit = PhoneRead("Phone/PhoneKit.cs"), items = PhoneRead("Phone/PhoneItemsGame.cs"), screen = PhoneRead("Phone/PhoneScreen.cs");
        Check(kit.Length > 0 && items.Length > 0 && screen.Length > 0, "phone sources: the kit phone's files are where they were");
        string conceal = PhoneBody(kit, "private static void Conceal()") ?? string.Empty;
        Check(kit.Contains("new UiConceal()") && conceal.Contains("Buttons.Add(_grid.GetChild(i));") && conceal.Contains("Buttons.Prune();")
              && !kit.Contains("AddComponent<CanvasGroup>") && !kit.Contains("SetActive(false)"),
            "phone sources: the game's buttons are hidden one by one through the host's UiConceal, never the grid other mods fill, and nothing of the game's is switched off");
        Check(PhoneBody(kit, "public static void Remove(")?.Contains("Buttons.Clear();") == true && PhoneBody(kit, "internal static void Shown()")?.Contains("Buttons.Hide();") == true
              && PhoneBody(kit, "internal static void Frame(")?.Contains("Conceal();") == true,
            "phone sources: a button added while the phone shows hides at once, and every one is given back exactly as found");
        string hand = PhoneBody(PhoneRead("Host/UiWidgets.cs"), "public static void Hand(Button b)") ?? string.Empty;
        Check(PhoneBody(items, "internal static bool Press(int index)")?.Contains("UiWidgets.Hand(button);") == true && kit.Contains("UiWidgets.Hand(_collapse);")
              && kit.Contains("UiWidgets.Hand(_skip);") && kit.Contains("UiWidgets.Hand(_driver);")
              && hand.Contains("b.onClick.Invoke();") && hand.Contains("HotSpots.Game()"),
            "phone sources: every press goes to the game's own button, so its handlers and other mods' run as a click would (through UiWidgets.Hand, the game's own work)");
        Check(kit.Contains("rect.SetParent(_container, false);") && kit.Contains("window.onStateChange.AddListener(_onCollapse);")
              && kit.Contains("_window.onStateChange.RemoveListener(_onCollapse);"),
            "phone sources: the kit stands inside the game's phone container (it folds and hides with it), and hears the fold once and lets it go");
        Check(kit.Contains("pager.SteppedAside") && kit.Contains("\"VoogleRoute_PhoneGps\"") && PhoneBody(kit, "private static void StandDown(string why)")?.Contains("_stopped = true;") == true,
            "phone sources: another pager of the phone or another mod's screen on it has the kit stand down to the classic phone for the session");
        Check(!System.Text.RegularExpressions.Regex.IsMatch(kit, @"\bvoid (Update|LateUpdate|FixedUpdate)\(\)\s*\{(?![^}]*HotSpots)"), "phone sources: its frame is timed (HotSpots)");
        // While the phone holds the keys the game's walking, camera and shortcuts leave them alone, by the game's own path:
        // its keys object selected on the interface's layer (GameManager.HasInputSelected); every way out gives it back.
        string take = PhoneBody(kit, "private static void Take()") ?? string.Empty, leave = PhoneBody(kit, "private static void Leave()") ?? string.Empty;
        Check(take.Contains("events.SetSelectedGameObject(_keys);") && take.Contains("Keys.Suspended = true;") && take.Contains("Typing(events)")
              && kit.Contains("_keys.layer = ui >= 0 ? ui : UiLayer;") && kit.Contains("private const int UiLayer = 5;"),
            "phone sources: taking the keys selects the phone's keys object on the interface's layer, so the game stops the player, the camera and its shortcuts, and never over a field being typed in");
        Check(leave.Contains("events.currentSelectedGameObject == _keys") && leave.Contains("Keys.Suspended = _suspendedBefore;") && leave.Contains("_selectedBefore")
              && leave.Contains("_host.Exclusive = false;"),
            "phone sources: giving the keys back restores the selection only while it is the phone's, and the mod's keys as they were");
        string frame = PhoneBody(kit, "internal static void Frame(") ?? string.Empty;
        Check(frame.Contains("if (Gate.Focused && !Held())") && PhoneBody(kit, "public static void Remove(string why)")?.Contains("Leave();") == true
              && PhoneBody(kit, "internal static void Hidden()")?.Contains("Leave();") == true && PhoneBody(kit, "private static void Collapsed(bool collapsed)")?.Contains("Leave();") == true
              && PhoneBody(kit, "public void Leave()")?.Contains("PhoneKit.Leave();") == true,
            "phone sources: the keys go back on every way out: the key again, Back at home, a click away, the phone folding or hidden, a fault, the switch off");
        Check(kit.Contains("[DefaultExecutionOrder(-31000)]") && kit.Contains("Guard.Fail(FrameGuard, e, this);") && kit.Contains("PhoneFolderKeys.KeyIsFree()"),
            "phone sources: its frame runs before the game's keys, behind the guard, and Esc in a folder gives way to the game first");
        string poll = PhoneBody(items, "internal static bool Poll(") ?? string.Empty;
        Check(poll.Contains("ReferenceEquals(badge, SeenBadge[i])") && !poll.Contains("GetComponent"),
            "phone sources: the look at the buttons compares what it saw, searching nothing and making nothing");
    }

    private static void PhoneKitTexts()
    {
        string locales = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales"));
        string[] keys =
        {
            "reallife_ui_screen_phone", "reallife_opt_tip_ui_screen_phone", "reallife_keys_phone", "reallife_phone_next", "reallife_phone_next_none", "reallife_phone_other_mods",
            "reallife_phone_other_app", "reallife_phone_driver", "reallife_phone_test_second", "reallife_phone_test_kit_folder", "reallife_phone_test_classic",
            "reallife_phone_kit_test_none", "reallife_phone_kit_test_pass", "reallife_phone_kit_test_fail", "reallife_phone_kit_test_on", "reallife_phone_kit_test_off",
            "reallife_phone_place_first", "reallife_phone_move_info_first", "reallife_phone_move_first_full"
        };
        foreach (string lang in new[] { "en", "pt", "zh-cn" })
        {
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(locales, lang + ".json"))))
            {
                var missing = keys.Where(k => !doc.RootElement.TryGetProperty(k, out JsonElement v) || string.IsNullOrWhiteSpace(v.GetString())).ToList();
                Check(missing.Count == 0, "phone texts: every text of the kit phone is in " + lang + ".json (missing: " + string.Join(", ", missing) + ")");
                if (lang == "en")
                {
                    var tooLong = keys.Where(k => k.Contains("_test_") && doc.RootElement.TryGetProperty(k, out JsonElement v) && v.GetString().Length > 240).ToList();
                    Check(tooLong.Count == 0, "phone texts: every notice is at most 240 characters (" + string.Join(", ", tooLong) + ")");
                }
            }
        }
    }

    private static void PhoneKitTiming()
    {
        PhoneScreen screen = UiSamples.Phone((key, english) => english, -1, 0, true, true);
        var s = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Phone");
        for (int i = 0; i < 50; i++)
        {
            screen.Lay(s, frame, true, UiDefaultKeys.Keyboard);
        }
        var clock = Stopwatch.StartNew();
        const int times = 400;
        for (int i = 0; i < times; i++)
        {
            screen.Show(i % 3 == 0 ? PhoneLayoutRules.Business : -1, i % 2);
            screen.Lay(s, frame, true, UiDefaultKeys.Keyboard);
        }
        double each = clock.Elapsed.TotalMilliseconds / times;
        Check(each < 1.0, "phone timing: a layout of the phone takes " + each.ToString("0.000") + " ms (at most 1; it runs only when a reading changed)");
    }
}
