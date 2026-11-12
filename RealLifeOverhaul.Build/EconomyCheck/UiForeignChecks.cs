using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;
using ForeignApps = RealLifeOverhaul.Ui.UiForeignApps;   // Program has a check of that name

namespace RealLifeOverhaul.Ui
{
    /// <summary>A screen of the kit's as the host stands round it: runs a press or a read inside the catch the host has, and says what it did.</summary>
    internal static class ForeignHarness
    {
        public static int Fallbacks;

        /// <summary>The host's guard for a press: another mod's fault is blamed and the screen opens its breaker, anything else would go the guard's way.</summary>
        public static void Host(string screenId, Action press, Func<Exception, string> blame)
        {
            try
            {
                press();
            }
            catch (Exception e) when (blame(e) != null)
            {
                UiForeign.Note(screenId, UiPieceKind.Fault, blame(e), e.GetType().Name, "the screen is the game's own for the session");
                UiBreaker.Trip(screenId);
                Fallbacks++;
            }
        }

        /// <summary>A fault of the kit's own code.</summary>
        public static void KitBug() => throw new InvalidOperationException("the kit's own fault");
    }
}

// What the kit does when another author's mod is on its screens (docs/reference/kit-for-other-mods.md), checked with a second
// mod that exists only here (UiPreview/UiSamples.FakeMod.cs): its phone apps with and without a picture, its tab and loose
// button on BizMan, its counter on the HUD's bar, a canvas it opens over a kit screen, and a button and a text that throw.
// Game-free: the rules the game files feed (UiForeignRules, UiBreaker, UiForeign, UiForeignMark), each case through them, the
// kit's own screens laid out with the mod's pieces on them, the public surface and its document, a switch for every screen
// the host draws over, and what the guards cost.
internal static partial class Program
{
    private static UiOrigin FakeOrigin(MethodBase m)
    {
        string ns = m.DeclaringType != null ? m.DeclaringType.Namespace : null;
        return ns == "FakeOtherModCode" ? UiOrigin.Other : ns != null && ns.StartsWith("RealLifeOverhaul", StringComparison.Ordinal) ? UiOrigin.Ours : UiOrigin.Framework;
    }

    private static string FakeBlame(Exception e) => UiForeignRules.Blame(e, FakeOrigin, m => FakeOtherMod.Name);

    private static void UiForeignChecks()
    {
        UiForeign.Reset();
        UiBreaker.Reset();
        var said = new List<string>();
        UiForeign.Log = said.Add;
        UiForeign.Warn = said.Add;
        UiForeignApps();
        UiForeignPlaces();
        UiForeignAdditions();
        UiForeignWindowRules();
        UiForeignIsolation(said);
        UiForeignSurface();
        UiForeignCost();
        UiForeign.Log = null;
        UiForeign.Warn = null;
        UiForeign.Reset();
        UiBreaker.Reset();
    }

    // ---------------------------------------------------------------- apps

    private static void UiForeignApps()
    {
        int tools = UiForeignMark.Of(FakeOtherMod.TitleWithoutPicture, FakeOtherMod.AppWithoutPicture);
        int chat = UiForeignMark.Of(null, "BAMP_Chat");
        Check(UiForeignMark.Is(tools) && UiForeignMark.Letter(tools) == "F" && UiForeignMark.Letter(chat) == "C" && UiForeignMark.Letter(UiForeignMark.Of(string.Empty, null)) == "?"
              && !UiForeignMark.Is(UiSamples.PictureBank) && UiForeignMark.Tint(UiSamples.PictureBank) == UiRole.None,
            "other mods: an app with no picture gets a mark of its name's first letter (the title's, else the id's after the author's prefix), a picture's handle is never a mark");
        var hues = new HashSet<int>();
        bool stable = true;
        foreach (string id in new[] { "FOM_Tools", "BAMP_Chat", "EmpireBankApp", "VoogleRoute_PhoneGps", "Immersion", "Notes", "Tasks", "AlunaPhone" })
        {
            stable &= UiForeignMark.Hue(id) == UiForeignMark.Hue(id) && UiForeignMark.Hue(id) >= 0 && UiForeignMark.Hue(id) < 8;
            hues.Add(UiForeignMark.Hue(id));
        }
        Check(stable && hues.Count >= 4 && UiForeignMark.Tint(tools) >= UiRole.Chart1 && UiForeignMark.Tint(tools) <= UiRole.Chart8,
            "other mods: a mark's hue is one of the chart's eight, the same for the same app on every machine, and apps spread over the hues (" + hues.Count + " of 8 for eight)");

        PhoneItems without = UiSamples.PhoneApps((key, english) => english, true);
        PhoneItems with = UiSamples.WithFake((key, english) => english);
        Check(with.Count == without.Count + 2 && with.IndexOf(FakeOtherMod.AppWithPicture) >= 0 && with.IndexOf(FakeOtherMod.AppWithoutPicture) >= 0
              && with.Items[with.IndexOf(FakeOtherMod.AppWithPicture)].Kind == PhoneItemKind.Other && with.Items[with.IndexOf(FakeOtherMod.AppWithPicture)].Picture == FakeOtherMod.PictureHandle
              && UiForeignMark.Is(with.Items[with.IndexOf(FakeOtherMod.AppWithoutPicture)].Picture),
            "other mods: the fake mod's two apps are among the phone's, the first with its picture, the second with the kit's mark");
        var rowWithout = new List<UiShellApp>();
        var rowWith = new List<UiShellApp>();
        UiShellApps.FromPhone(without, rowWithout, PagerRules.PhoneTiles);
        UiShellApps.FromPhone(with, rowWith, PagerRules.PhoneTiles);
        bool sameRow = rowWithout.Where(a => a.InRow).Select(a => a.Id).SequenceEqual(rowWith.Where(a => a.InRow).Select(a => a.Id));
        UiShellApp notes = rowWith.First(a => a.Id == FakeOtherMod.AppWithPicture), toolsApp = rowWith.First(a => a.Id == FakeOtherMod.AppWithoutPicture);
        Check(sameRow && rowWith.Count == with.Count && notes.Kind == UiShellKind.Other && toolsApp.Kind == UiShellKind.Other && notes.Folder == UiShellApps.Other && toolsApp.Folder == UiShellApps.Other
              && notes.Picture == FakeOtherMod.PictureHandle && UiForeignMark.Is(toolsApp.Picture) && !notes.InRow && !toolsApp.InRow,
            "other mods: All apps lists both under Other mods, each as the phone draws it, and the app row is the phone's first page as it was without them (the row tests hold)");
        var s = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Phone");
        var screen = new PhoneScreen(with, null, (key, english) => english);
        bool picture = false, mark = false, crowded = false;
        for (int page = 0; page < 4; page++)
        {
            screen.Show(-1, page);
            screen.Lay(s, frame, true, UiDefaultKeys.Keyboard);
            for (int i = 0; i < s.ItemCount; i++)
            {
                picture |= s.Items[i].Kind == UiKind.Picture && s.Items[i].Text == UiDraw.PictureKey(FakeOtherMod.PictureHandle);
                mark |= s.Items[i].Kind == UiKind.Text && s.Items[i].Text == "F";
            }
            crowded |= UiCheckRules.Crowded(s, null) != 0;
        }
        Check(picture && mark && !crowded, "other mods: the phone's grid draws the first app's own picture and the second's lettered mark, nothing overlapping");
        bool kinds = true;
        foreach (string locale in new[] { "en", "pt", "zh-cn" })
        {
            var tiles = new PhoneScreen(UiSamples.WithFake(UiWords(locale)), null, UiWords(locale));
            tiles.Show(-1, 1);
            tiles.Lay(s, frame, true, UiDefaultKeys.Keyboard);
            kinds &= UiCheckRules.Crowded(s, null) == 0;
        }
        Check(kinds, "other mods: the phone with the fake mod's apps is laid out whole in English, Portuguese and Chinese");
    }

    // ---------------------------------------------------------------- apps outside the phone's grid

    // A mod with no button on the phone's grid: its apps stand on the full menu's own row alone (a player's report of
    // 1.0.0: two mods' apps "used to be above" and could not be found at all), or it hung a button on the phone itself.
    // The kit hides that row and covers the phone, so each is an app of the phone's model: on the kit phone, in All
    // apps and, the first of the row's, on the kit's own row.
    private static void UiForeignPlaces()
    {
        Check(ForeignApps.Others("EmpireMotors_Btn", false) && ForeignApps.Others(FakeOtherMod.RowApps[0], false)
              && !ForeignApps.Others("BizMan", true) && !ForeignApps.Others("AppButtonTemplate", false)
              && !ForeignApps.Others("RealLife MoreApps", false) && !ForeignApps.Others("RealLifeFinance", false)
              && !ForeignApps.Others(null, false) && !ForeignApps.Others(string.Empty, false),
            "other mods' places: a button of the full menu's row that is not the game's app, not the template and not the overhaul's own is another mod's");
        string[] names = { "Persona", "RealLifeFlare", "EmpireBank" };
        string[] titles = { null, null, " Empire Bank " };
        Check(ForeignApps.Listed("EmpireBank", null, names, titles, 3) && ForeignApps.Listed("EmpireBank_Btn", "empire bank", names, titles, 3)
              && !ForeignApps.Listed("EmpireMotors_Btn", "Empire Motors", names, titles, 3) && !ForeignApps.Listed("Flare_Btn", "Flare", names, titles, 3)
              && !ForeignApps.Listed("X", null, names, titles, 3) && !ForeignApps.Listed("EmpireBank", null, names, titles, 0),
            "other mods' places: one tile an app: a row button the phone's grid lists already, by its name or by another mod's words, is not listed again; any other is");
        Check(ForeignApps.Unique("Button", names, 3) == "Button" && ForeignApps.Unique("Persona", names, 3) == "Persona 2"
              && ForeignApps.Unique("A", new[] { "A", "A 2" }, 2) == "A 3" && ForeignApps.Unique(null, names, 3) == "?",
            "other mods' places: a second button of one name gets a number after it, so each tile presses its own button");

        UiWords en = (key, english) => english;
        PhoneItems grid = UiSamples.WithFake(en), all = UiSamples.WithFakeExtras(en);
        var rowGrid = new List<UiShellApp>();
        var rowAll = new List<UiShellApp>();
        UiShellApps.FromPhone(grid, rowGrid, PagerRules.PhoneTiles);
        UiShellApps.FromPhone(all, rowAll, PagerRules.PhoneTiles);
        List<string> first = rowGrid.Where(a => a.InRow).Select(a => a.Id).ToList(), now = rowAll.Where(a => a.InRow).Select(a => a.Id).ToList();
        Check(first.Count == PagerRules.PhoneTiles && now.Count == PagerRules.PhoneTiles + ForeignApps.RowMost && now.Take(PagerRules.PhoneTiles).SequenceEqual(first)
              && now[PagerRules.PhoneTiles] == FakeOtherMod.RowApps[0] && now[PagerRules.PhoneTiles + 1] == FakeOtherMod.RowApps[1],
            "other mods' places: the kit's row is the phone's first page, then the first two apps a mod put on the full menu's own row (" + string.Join(", ", now.Skip(PagerRules.PhoneTiles)) + ")");
        UiShellApp desk = rowAll.FirstOrDefault(a => a.Id == FakeOtherMod.RowApps[0]), yard = rowAll.FirstOrDefault(a => a.Id == FakeOtherMod.RowApps[2]);
        UiShellApp pin = rowAll.FirstOrDefault(a => a.Id == FakeOtherMod.Hung), notes = rowAll.FirstOrDefault(a => a.Id == FakeOtherMod.AppWithPicture);
        Check(rowAll.Count == all.Count && rowAll.Select(a => a.Id).Distinct().Count() == all.Count && desk.Kind == UiShellKind.Other && desk.Folder == UiShellApps.Other
              && desk.Picture == FakeOtherMod.RowPictureHandle && desk.From == PhoneItemFrom.Row && yard.Folder == UiShellApps.Other && !yard.InRow && UiForeignMark.Is(yard.Picture)
              && pin.Folder == UiShellApps.Other && pin.From == PhoneItemFrom.Frame && !pin.InRow && !notes.InRow && notes.From == PhoneItemFrom.Grid,
            "other mods' places: All apps lists every one once under Other mods with its own picture or the kit's mark; past the first two of the row they wait there, as a grid's app and a button hung on the phone do");
        var home = new int[96];
        int n = all.Home(home);
        var onHome = new HashSet<string>();
        for (int e = 0; e < n; e++)
        {
            if (home[e] >= 0)
            {
                onHome.Add(all.Items[home[e]].Id);
            }
        }
        Check(FakeOtherMod.RowApps.All(onHome.Contains) && onHome.Contains(FakeOtherMod.Hung), "other mods' places: each is a tile of the kit phone's home screen, with other mods' apps of the grid");
        var s = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Phone");
        var screen = new PhoneScreen(all, null, en);
        bool picture = false, title = false, crowded = false;
        for (int page = 0; page < 5; page++)
        {
            screen.Show(-1, page);
            screen.Lay(s, frame, true, UiDefaultKeys.Keyboard);
            for (int i = 0; i < s.ItemCount; i++)
            {
                picture |= s.Items[i].Kind == UiKind.Picture && s.Items[i].Text == UiDraw.PictureKey(FakeOtherMod.RowPictureHandle);
                title |= s.Items[i].Kind == UiKind.Text && s.Items[i].Text == FakeOtherMod.RowTitles[0];
            }
            crowded |= UiCheckRules.Crowded(s, null) != 0;
        }
        Check(picture && title && !crowded, "other mods' places: the kit phone draws the row's app with its own picture and its name, nothing overlapping");

        List<UiShellApp> inRow = rowAll.Where(a => a.InRow).ToList();
        var bar = new UiStatusModel
        {
            Apps = inRow.Select(a => a.Icon).ToArray(), AppTints = inRow.Select(a => a.Tint).ToArray(), AppBadges = inRow.Select(a => a.Badge).ToArray(),
            AppPictures = inRow.Select(a => a.Picture).ToArray(), AllApps = true, AppsPress = true, App = 1
        };
        UiChrome.Bar = () => bar;
        UiListDetail.Lay(s, UiSamples.ContactsWindow(UiWords("en")), UiFrame.FullMenu(16f / 9f, "Contacts"), true, UiDefaultKeys.Keyboard);
        UiChrome.Bar = null;
        int tiles = 0;
        bool own = false;
        for (int i = 0; i < s.NodeCount; i++)
        {
            tiles += s.Nodes[i].Command == UiDraw.AppRowCommand ? 1 : 0;
        }
        for (int i = 0; i < s.ItemCount; i++)
        {
            own |= s.Items[i].Kind == UiKind.Picture && s.Items[i].Text == UiDraw.PictureKey(FakeOtherMod.RowPictureHandle);
        }
        Check(UiDraw.AppsShown(bar, 1920f) == inRow.Count && tiles == inRow.Count && own && UiCheckRules.Crowded(s, null) == 0 && s.FindNode(UiDraw.AllAppsCommand, 0) >= 0,
            "other mods' places: the status bar's row has room for them before All apps at 1920 wide, each a tile to press, the first with its own picture (" + tiles + " tiles)");

        var phone = new List<string>();
        var hud = new List<string>();
        var bizman = new List<string>();
        var pieces = new List<UiForeign.Piece>
        {
            new UiForeign.Piece { Screen = "bizman", Kind = UiPieceKind.Tab, Owner = FakeOtherMod.Name, Where = "the business page's tab bar: " + FakeOtherMod.Tab },
            new UiForeign.Piece { Screen = "hud", Kind = UiPieceKind.Button, Owner = FakeOtherMod.Name, Where = "the building's panel: " + FakeOtherMod.PanelButton },
            new UiForeign.Piece { Screen = "phone", Kind = UiPieceKind.App, Owner = FakeOtherMod.AppWithPicture, Where = "the phone's grid" },
            new UiForeign.Piece { Screen = "bizman", Kind = UiPieceKind.Fault, Owner = FakeOtherMod.Name, Where = "a fault" },
        };
        ForeignApps.Census(rowAll, pieces, phone, hud, bizman);
        string line = ForeignApps.Line(phone, hud, bizman);
        Check(phone.Count == 7 && hud.Count == 2 && bizman.Count == 1 && line.StartsWith("Other mods' apps: 7 on the phone (Empire Bank, Chat, Fake Notes, Fake Tools, Fake Desk, Fake Vault, Fake Yard), 2 on the HUD (", StringComparison.Ordinal)
              && line.Contains(FakeOtherMod.TitleHung) && line.Contains(FakeOtherMod.Name + ": " + FakeOtherMod.PanelButton) && line.EndsWith(", 1 in BizMan (" + FakeOtherMod.Name + ": " + FakeOtherMod.Tab + ").", StringComparison.Ordinal),
            "other mods' places: Player.log's line counts them by where the player finds them, each by its name (" + line + ")");
        string none = ForeignApps.Line(new List<string>(), null, new List<string>());
        var many = Enumerable.Range(1, 15).Select(i => "App " + i).ToList();
        string long0 = ForeignApps.Line(many, null, null);
        Check(none == "Other mods' apps: 0 on the phone, 0 on the HUD, 0 in BizMan." && long0.Contains("15 on the phone (App 1, ") && long0.Contains("App 12 and 3 more)") && !long0.Contains("App 13"),
            "other mods' places: with none the line says so, and a long list names the first twelve and counts the rest");
        int before = ForeignApps.Signature(phone, hud, bizman);
        phone.Add("Late App");
        Check(before == ForeignApps.Signature(new List<string>(phone.Take(7)), hud, bizman) && before != ForeignApps.Signature(phone, hud, bizman),
            "other mods' places: the line is written again only when what it says changed (an app a mod adds late)");

        var panel = new UiSidePanelModel
        {
            Title = "7 5th Avenue", HeadName = "Lucky Seven", HeadSub = "Casino, Midtown",
            Actions = new[]
            {
                new UiActionModel { Label = "Open in BizMan", Kind = UiButtonKind.Primary, Key = UiAction.Count }, new UiActionModel { Label = "Interior designer", Kind = UiButtonKind.Secondary, Key = UiAction.Count },
                new UiActionModel { Label = "Casino floor", Kind = UiButtonKind.Secondary, Key = UiAction.Count },
                new UiActionModel { Label = "Fake Enter", Kind = UiButtonKind.Secondary, Key = UiAction.Count },
                new UiActionModel { Label = "One too many", Kind = UiButtonKind.Secondary, Key = UiAction.Count },
            },
            Tool = UiIcon.Grid,
        };
        // The game's two alone, to measure against; its first is plain here, as a screen has one primary action and the fuller panel holds it.
        var two = new UiSidePanelModel
        {
            Title = panel.Title, HeadName = panel.HeadName, HeadSub = panel.HeadSub, Tool = UiIcon.Grid,
            Actions = new[] { new UiActionModel { Label = "Open in BizMan", Kind = UiButtonKind.Secondary, Key = UiAction.Count }, panel.Actions[1] },
        };
        s.Begin("side", frame.Width, frame.Height, true);
        UiSidePanel.LayStack(s, UiAt.Screen, new[] { two, panel }, 2, frame.Width - UiHud.Left, UiTokens.S4, UiDefaultKeys.Keyboard);
        int actions = 0;
        bool third = false, fifth = false;
        for (int i = 0; i < s.NodeCount; i++)
        {
            actions += s.Nodes[i].Command == UiSidePanel.ActionCommand && s.Nodes[i].Arg / 4 == 1 ? 1 : 0;
        }
        for (int i = 0; i < s.ItemCount; i++)
        {
            third |= s.Items[i].Kind == UiKind.Text && s.Items[i].Text == "Casino floor";
            fifth |= s.Items[i].Kind == UiKind.Text && s.Items[i].Text == "One too many";
        }
        float step = UiTokens.ControlSmall + UiTokens.S2, grew = UiSidePanel.Height(panel) - UiSidePanel.Height(two);
        int crowd = UiCheckRules.Crowded(s, null);
        Check(UiSidePanelModel.MostActions == 4 && UiSidePanelModel.OwnActions == 2 && actions == 4 && third && !fifth && s.FindNode(UiSidePanel.ActionCommand, 1 * 4 + 3) >= 0
              && s.FindNode(UiSidePanel.ActionCommand, 0 * 4 + 1) >= 0 && Math.Abs(grew - 2f * step) < 1.5f && crowd == 0,
            "other mods' places: the building's panel carries two buttons of another mod's under the game's two, each with a command of its own, and grows by their rows alone ("
            + actions + " actions, " + grew + " px taller for two rows of " + step + ", " + crowd + " crowded, third " + third + ", fifth " + fifth + ")");

        string items = PhoneRead("Phone/PhoneItemsGame.cs"), extras = PhoneRead("Phone/PhoneExtras.cs"), kit = PhoneRead("Phone/PhoneKit.cs"), shell = PhoneRead("Shell/ShellApps.cs");
        string sides = PhoneRead("HudScreens/HudSides.cs"), check = PhoneRead("Compat/UiForeignCheck.cs"), director = PhoneRead("UiDirector.cs"), bizPage = PhoneRead("BizMan/BizManScreen.Page.cs");
        Check((PhoneBody(items, "internal static bool Read(PhoneGridPager pager)") ?? string.Empty).Contains("PhoneExtras.Add(Buttons, Kinds, Places, Ids, Froms);")
              && (PhoneBody(shell, "public static int FromPhone(List<UiShellApp> into)") ?? string.Empty).Contains("PhoneItemsGame.Fresh(PhoneGridPager.Hud);")
              && extras.Contains("canvas.Find(\"Topbar/AppButtons\")") && extras.Contains("UiForeignGame.ButtonOwner(button) != null"),
            "other mods' places (sources): the phone's reading takes in the buttons of the full menu's row and of the phone itself, and the frame's list asks for them afresh");
        string signature = PhoneBody(extras, "internal static int Signature()") ?? string.Empty, shape = PhoneBody(extras, "private static int Shape(") ?? string.Empty;
        Check(signature.Contains("GetInstanceID()") && !signature.Contains(".name") && !shape.Contains(".name") && !signature.Contains("GetComponent") && !shape.Contains("GetComponent")
              && !signature.Contains("new ") && !shape.Contains("new "),
            "other mods' places (sources): the look for a late app compares the objects alone: no name read, nothing searched, nothing made");
        Check((PhoneBody(kit, "internal static void Shown()") ?? string.Empty).Contains("PhoneItemsGame.Fresh(") && (PhoneBody(kit, "public static void Install()") ?? string.Empty).Contains("PhoneItemsGame.Fresh(")
              && !(PhoneBody(kit, "internal static void Frame(") ?? "PhoneItemsGame.Fresh(").Contains("PhoneItemsGame.Fresh(") && !extras.Contains("void Update") && !extras.Contains("void LateUpdate"),
            "other mods' places (sources): the look runs when the phone opens, when the full menu opens and once a second, never a frame");
        Check(!extras.Contains("SetParent(") && !extras.Contains("SetActive(") && !extras.Contains("Destroy(") && !extras.Contains("AddComponent") && !extras.Contains("SetSiblingIndex"),
            "other mods' places (sources): nothing of another mod's is moved, switched, added to or taken away: its button stays where its mod looks for it");
        string menuUp = PhoneBody(shell, "internal static bool MenuUp()") ?? string.Empty;
        Check((PhoneBody(items, "internal static bool Press(int index)") ?? string.Empty).Contains("Froms[index] == PhoneItemFrom.Row && !ShellApps.MenuUp()") && menuUp.Contains("uis.fullMenu.Toggle(true);")
              && menuUp.Contains("UiBoxes.Up") && menuUp.Contains("HotSpots.Game()") && (PhoneBody(shell, "public static bool Press(string id)") ?? string.Empty).Contains("PhoneItemsGame.Press(at)"),
            "other mods' places (sources): a row's app pressed while the full menu is closed has the menu opened first, as the game opens it, and never over a question");
        Check(sides.Contains("UiForeignGame.ButtonOwner(button)") && sides.Contains("Order.AddRange(Others);") && (PhoneBody(sides, "private static void OthersLook()") ?? string.Empty).Contains("if (shape == _othersShape)")
              && bizPage.Contains("UiForeignGame.ButtonOwner(button) != null") && bizPage.Contains("_judged.TryGetValue(id, out bool others)"),
            "other mods' places (sources): a button another mod put on the building's panel or inside a BizMan page is told once, when the panel's objects changed, and offered on the kit");
        Check((PhoneBody(check, "public static void Apps(IModLogger logger)") ?? string.Empty).Contains("UiForeignApps.Line(OnPhone, OnHud, InBizMan)") && director.Contains("UiForeignCheck.Apps(Logger);")
              && director.Contains("UiForeignCheck.Rearm();"),
            "other mods' places (sources): the line is written from the interface's slow tick, once a city and again on a change");
        Check(WhatsNewHas(LocaleFile("en"), "full menu's top bar"), "other mods' places: What's new tells of the fix");
    }

    // ---------------------------------------------------------------- additions to replaced screens

    private static void UiForeignAdditions()
    {
        var into = new List<int>();
        string[] bar = FakeOtherMod.TabBar(true);
        bool[] on = Enumerable.Repeat(true, bar.Length).ToArray();
        int found = UiForeignRules.NotTheGames(bar, on, BizManRules.GameTabs, into);
        Check(found == 1 && bar[into[0]] == FakeOtherMod.Tab, "other mods: the fake mod's tab is the one child of BizMan's tab bar the game did not make (the game's tabs and the overhaul's own are not)");
        string[] clones = { "Insight(Clone)", "FOM_Tab(Clone)", "RealLifeAnything", string.Empty };
        found = UiForeignRules.NotTheGames(clones, new[] { true, true, true, true }, BizManRules.GameTabs, into);
        Check(found == 2 && clones[into[0]] == "FOM_Tab(Clone)" && clones[into[1]] == string.Empty && UiForeignRules.NotTheGames(FakeOtherMod.TabBar(false), Enumerable.Repeat(true, FakeOtherMod.TabBar(false).Length).ToArray(), BizManRules.GameTabs, into) == 0,
            "other mods: a copy of its tab is still its own and a copy of the game's is the game's; with no other mod the tab bar has nothing foreign");
        bool[] hidden = Enumerable.Repeat(true, bar.Length).ToArray();
        hidden[3] = false;
        Check(UiForeignRules.NotTheGames(bar, hidden, BizManRules.GameTabs, into) == 0, "other mods: a tab it keeps switched off is nothing to show");
        string[] page = FakeOtherMod.PageChildren(true);
        Check(UiForeignRules.NotTheGames(page, new[] { true, true }, new[] { "Layout 30-70" }, into) == 1 && page[into[0]] == FakeOtherMod.Button
              && UiForeignRules.NotTheGames(FakeOtherMod.PageChildren(false), new[] { true }, new[] { "Layout 30-70" }, into) == 0,
            "other mods: its loose button on the business page, outside the tab bar, is found by what the game's page does not have");
        string[] hud = FakeOtherMod.HudBar(true);
        string[] gameHud = FakeOtherMod.HudBar(false);
        Check(UiForeignRules.NotTheGames(hud, Enumerable.Repeat(true, hud.Length).ToArray(), gameHud, into) == 1 && hud[into[0]] == FakeOtherMod.HudChild,
            "other mods: its counter on the HUD's bar is found by what the game's bar does not have");
        string[] before = { "List", "Business", "Popup" };
        string[] after = { "List", "Business", "Popup", FakeOtherMod.Button, "Late", "RealLifeMine" };
        Check(UiForeignRules.NewSince(after, new[] { true, true, true, true, false, true }, new HashSet<string>(before), into) == 1 && after[into[0]] == FakeOtherMod.Button,
            "other mods: on a screen with no names of the game's, a child that came after the bind, shown and not the mod's own, is another mod's");
        UiWords en = UiWords("en");
        BizManModel m = UiSamples.BizMan(en, "store_foreign");
        var s = new UiScene();
        BizManPages.Lay(s, m, UiFrame.FullMenu(16f / 9f, "BizMan"), true, UiDefaultKeys.Keyboard);
        bool entry = m.Nav.Any(n => n.Label == FakeOtherMod.TabWords && n.Icon == UiIcon.Code);
        bool drawn = false;
        for (int i = 0; i < s.ItemCount; i++)
        {
            drawn |= s.Items[i].Kind == UiKind.Text && s.Items[i].Text == FakeOtherMod.TabWords;
        }
        Check(entry && drawn && UiCheckRules.Crowded(s, null) == 0, "other mods: BizMan's sidebar shows the fake mod's tab as an entry of its own, and its page hands over to the game's own view");
        var said = new List<string>();
        Action<string> before0 = UiForeign.Log;
        UiForeign.Log = said.Add;
        for (int i = 0; i < 50; i++)
        {
            UiForeign.Note("bizman", UiPieceKind.Tab, FakeOtherMod.Name, "the business page's tab bar: " + FakeOtherMod.Tab, "its own entry in the sidebar");
        }
        UiForeign.Note("hud", UiPieceKind.Child, FakeOtherMod.Name, "the left bar: " + FakeOtherMod.HudChild, "it stays where the game puts it");
        UiForeign.Log = before0;
        string line = UiForeign.Line();
        Check(said.Count == 2 && said[0].Contains(FakeOtherMod.Name) && said[0].Contains(FakeOtherMod.Tab) && said[1].Contains("hud") && UiForeign.Count("bizman") == 1
              && line.Contains(FakeOtherMod.Name) && line.Contains("bizman") && line.Contains("hud") && line.Contains("(2)"),
            "other mods: each piece is said once in Player.log with its mod and where, however often it is met, and the load's self-check line lists them");
    }

    // ---------------------------------------------------------------- their own windows

    private static void UiForeignWindowRules()
    {
        Check(UiForeignRules.Lifted(FakeOtherMod.CanvasOrder, 50) == 50 + UiForeignRules.LiftOver && UiForeignRules.Lifted(500, 50) == 500 && UiForeignRules.Lifted(51, 50) == 50 + UiForeignRules.LiftOver
              && UiForeignRules.Lifted(52, 50) == 52 && UiForeignRules.Lifted(int.MinValue, 50) == 52,
            "other mods: a canvas it opens under the kit's is lifted just over it, one already over is left where it is");
        Check(UiForeignRules.IsWindow(false, false, false, true, true) && !UiForeignRules.IsWindow(true, false, false, true, true) && !UiForeignRules.IsWindow(false, true, false, true, true)
              && !UiForeignRules.IsWindow(false, false, true, true, true) && !UiForeignRules.IsWindow(false, false, false, false, true) && !UiForeignRules.IsWindow(false, false, false, true, false),
            "other mods: a window is a canvas that opened after the kit screen, is neither the kit's nor the game's, is up and takes the mouse");
        Check(!UiForeignRules.KitHasKeys(true, false, false) && !UiForeignRules.KitHasKeys(true, true, true) && !UiForeignRules.KitHasKeys(false, true, false) && !UiForeignRules.KitHasKeys(false, false, true)
              && UiForeignRules.KitHasKeys(false, false, false),
            "other mods: Escape and every key go to a window of its own first, then the game's box, then the kit's All apps, and the kit has them only when nothing stands over it");
        Check(UiForeignRules.Words(UiPieceKind.Canvas) == "a window of its own" && UiForeignRules.Words(UiPieceKind.Tab) == "a tab" && UiForeignRules.Words(UiPieceKind.Fault) == "a fault",
            "other mods: every kind of piece has its words in the log");
    }

    // ---------------------------------------------------------------- isolation

    private static void UiForeignIsolation(List<string> said)
    {
        said.Clear();
        Exception click = Thrown(FakeOtherModCode.Code.Click), label = Thrown(() => FakeOtherModCode.Code.Label()), nested = Thrown(FakeOtherModCode.Code.Nested);
        Exception kit = Thrown(ForeignHarness.KitBug), kitUnderMod = Thrown(() => FakeOtherModCode.Code.Calls(ForeignHarness.KitBug));
        Check(FakeBlame(click) == FakeOtherMod.Name && FakeBlame(label) == FakeOtherMod.Name && FakeBlame(nested) == FakeOtherMod.Name && FakeBlame(kit) == null && FakeBlame(kitUnderMod) == null
              && FakeBlame(null) == null,
            "other mods: a fault from its button, its text and a handler of its calling its own is blamed on it by name; the kit's own, even when called through it, is not");
        ForeignHarness.Fallbacks = 0;
        bool escaped = false;
        try
        {
            ForeignHarness.Host("bizman", FakeOtherModCode.Code.Click, FakeBlame);
            ForeignHarness.Host("bizman", () => FakeOtherModCode.Code.Label(), FakeBlame);
        }
        catch (Exception)
        {
            escaped = true;
        }
        Check(!escaped && ForeignHarness.Fallbacks == 2 && UiBreaker.Tripped("bizman") && !UiScreenSwitches.On("bizman") && UiScreenSwitches.On("contacts") && UiScreenSwitches.On("phone") && UiBreaker.Count == 1,
            "other mods: a fault of its button or its text is caught at the screen, the screen alone is the game's own for the session, and no other screen is touched");
        bool kitFaultGoesOn = false;
        try
        {
            ForeignHarness.Host("contacts", ForeignHarness.KitBug, FakeBlame);
        }
        catch (InvalidOperationException)
        {
            kitFaultGoesOn = true;
        }
        Check(kitFaultGoesOn && UiScreenSwitches.On("contacts"), "other mods: the kit's own fault is not taken for its fault: it goes the guard's way and opens no breaker");
        int fault = said.Count(l => l.Contains("threw") && l.Contains(FakeOtherMod.Name));
        Check(fault == 2 && said.All(l => l.Contains("bizman")), "other mods: its faults are logged with its name and the screen (" + said.Count + " lines: one for its button, one for its text, each once)");
        string first = said.Count > 0 ? said[0] : string.Empty;
        Check(first.Contains("game's own for the session") && first.Contains("(Said once.)"), "other mods: the fault's line says the screen is the game's own for the session");
        UiBreaker.Reset();
        Check(UiScreenSwitches.On("bizman") && UiBreaker.Count == 0 && !UiBreaker.Trip(null) && !UiBreaker.Tripped(null), "other mods: the breaker closes with a new session, and a screen with no id has none");
    }

    private static Exception Thrown(Action act)
    {
        try
        {
            act();
        }
        catch (Exception e)
        {
            return e;
        }
        return null;
    }

    // ---------------------------------------------------------------- the public surface and every screen's switch

    private static void UiForeignSurface()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui"));
        string repo = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\"));
        string api = File.ReadAllText(System.IO.Path.Combine(scripts, "Compat", "KitForOtherMods.cs"));
        string[] surface =
        {
            "public const int ApiVersion = 1;", "public static bool IsOn", "public static bool IsScreenOn(string screenId)",
            "public static bool RegisterApp(string id, string name, Sprite icon, Func<Transform, GameObject> panel, Func<IUiScreen> screen = null)",
            "public static bool AddPage(string appId, string pageId, string title, Action open)", "public static bool AddSetting(string id, string label, Func<bool> get, Action<bool> set)"
        };
        string docPath = System.IO.Path.Combine(repo, "docs", "reference", "kit-for-other-mods.md");
        string doc = File.Exists(docPath) ? File.ReadAllText(docPath) : string.Empty;
        string sdk = File.ReadAllText(System.IO.Path.Combine(repo, "docs", "SDK.md"));
        Check(surface.All(api.Contains) && doc.Length > 1500 && new[] { "ApiVersion", "IsOn", "IsScreenOn", "RegisterApp", "AddPage", "AddSetting" }.All(doc.Contains)
              && doc.Contains("## A minimal example") && doc.Contains("KitForOtherMods.RegisterApp(") && sdk.Contains("docs/reference/kit-for-other-mods.md"),
            "other mods: the public API is the six members the document lists, with a minimal example, and the SDK's page points to it");
        var consts = new Dictionary<string, string>();
        var ids = new List<string>();
        string[] files = Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories);
        foreach (string path in files)
        {
            foreach (Match c in Regex.Matches(File.ReadAllText(path), @"const string (\w+)\s*=\s*""([a-z_0-9]+)"""))
            {
                consts[c.Groups[1].Value] = c.Groups[2].Value;
            }
        }
        foreach (string path in files)
        {
            string text = File.ReadAllText(path);
            if (!text.Contains(": IUiGameScreen") || path.EndsWith("ModAppScreen.cs", StringComparison.Ordinal) || path.Contains(System.IO.Path.DirectorySeparatorChar + "Host" + System.IO.Path.DirectorySeparatorChar))
            {
                continue;
            }
            foreach (Match m in Regex.Matches(text, @"public string Id => ([^;]+);"))
            {
                string value = m.Groups[1].Value.Trim();
                if (value.StartsWith("\"", StringComparison.Ordinal))
                {
                    ids.Add(value.Trim('"'));
                }
                else if (consts.TryGetValue(value.Substring(value.LastIndexOf('.') + 1), out string resolved))
                {
                    ids.Add(resolved);
                }
            }
        }
        string noSwitch = string.Join(", ", ids.Where(id => !UiScreens.All.Any(d => d.Id == id && d.Section != null)).Distinct());
        Check(ids.Count >= 25 && noSwitch.Length == 0, "other mods: each of the " + ids.Count + " game screens the host draws over has its own switch in Mod options, for a player whose other mod needs the classic one (" + noSwitch + ")");
        var en = LocaleFile("en");
        string plain = string.Join(", ", ids.Distinct().Where(id => !en.ContainsKey(UiScreenSwitches.LabelOf(id)) || en[UiScreenSwitches.LabelOf(id)].Length < 6));
        Check(plain.Length == 0, "other mods: each of those switches is named in the options texts (" + plain + ")");
    }

    // ---------------------------------------------------------------- what the guards cost

    // The measured loops stand in methods compiled in full at once: left in the check's own long method, the runtime swapped
    // the running loop for a faster copy part way (on-stack replacement) and its own bytes were counted as the mod's, one run
    // in three (12,336 bytes on 13 Nov 2026), as NavCheck's hot-spot loops once were.
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static bool AskSwitches(int times)
    {
        bool any = false;
        for (int r = 0; r < times; r++)
        {
            any |= !UiScreenSwitches.On("bizman") || !UiScreenSwitches.On("phone");
        }
        return any;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static bool AskBreaker(int times)
    {
        bool any = false;
        for (int r = 0; r < times; r++)
        {
            any |= UiBreaker.Tripped("bizman");
        }
        return any;
    }

    private static void UiForeignCost()
    {
        var into = new List<int>(16);
        string[] kids = new string[200];
        bool[] up = new bool[200];
        for (int i = 0; i < kids.Length; i++)
        {
            kids[i] = i % 25 == 0 ? "FOM_" + i : i % 2 == 0 ? "Insight" : "RealLife Row " + i;
            up[i] = true;
        }
        UiForeignRules.NotTheGames(kids, up, BizManRules.GameTabs, into);
        var watch = Stopwatch.StartNew();
        for (int r = 0; r < 2000; r++)
        {
            UiForeignRules.NotTheGames(kids, up, BizManRules.GameTabs, into);
        }
        double scanMs = watch.Elapsed.TotalMilliseconds / 2000;
        Check(into.Count == 8 && scanMs < 0.5, "other mods cost: a scan of 200 children for what the game did not make takes " + scanMs.ToString("0.0000") + " ms, once a screen's open");
        UiBreaker.Reset();
        AskSwitches(1000);   // made ready before the measure: the first calls set the switches' table up
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        watch.Restart();
        bool any = AskSwitches(1000000);
        double askMs = watch.Elapsed.TotalMilliseconds;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
        Check(!any && allocated == 0 && askMs < 400, "other mods cost: the breaker's question, asked a million times with none open, allocates nothing (" + allocated + " bytes) and takes " + askMs.ToString("0") + " ms");
        UiBreaker.Trip("elsewhere");
        AskBreaker(1000);
        bytes = GC.GetAllocatedBytesForCurrentThread();
        any |= AskBreaker(1000000);
        allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
        UiBreaker.Reset();
        Check(!any && allocated == 0, "other mods cost: the breaker's question with one open for another screen allocates nothing either");
        Exception click = Thrown(FakeOtherModCode.Code.Click);
        FakeBlame(click);
        watch.Restart();
        for (int r = 0; r < 200; r++)
        {
            FakeBlame(Thrown(FakeOtherModCode.Code.Nested));
        }
        double faultMs = watch.Elapsed.TotalMilliseconds / 200;
        Check(faultMs < 5, "other mods cost: naming the mod behind a fault takes " + faultMs.ToString("0.000") + " ms, once, on the fault's own path");
        watch.Restart();
        int marks = 0;
        for (int r = 0; r < 100000; r++)
        {
            marks += UiForeignMark.Of("Fake Tools", "FOM_Tools") < 0 ? 1 : 0;
        }
        Check(marks == 100000 && watch.Elapsed.TotalMilliseconds < 400, "other mods cost: a mark is made in " + (watch.Elapsed.TotalMilliseconds * 10).ToString("0") + " ns, when an app is read");
    }
}
