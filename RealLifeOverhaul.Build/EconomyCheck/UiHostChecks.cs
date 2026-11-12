using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Ui;

// The host every game screen goes through (Scripts/Ui/Host) and what stands on it: the binding and the hiding written once
// (no second copy of the pattern in a screen's own file), the shared parents never given a group, another mod's objects
// told from the game's and the mod's, each runner with its own switch and that switch's words in all three languages, the
// status bar's app row and its commands kept to the frame's range, and the per-area registry parts in the mod.
internal static partial class Program
{
    private static void UiHostChecks()
    {
        Check(UiHostRules.Shared("AppsContainer") && UiHostRules.Shared("AppButtons") && UiHostRules.Shared("Topbar") && UiHostRules.Shared("Canvas")
              && !UiHostRules.Shared("Layout 30-70") && !UiHostRules.Shared("RaycastBlocker") && !UiHostRules.Shared(null),
            "ui host: the parents other mods fill are never given a group, the game's own leaves are");
        string[] game = { "Persona", "Contacts", "AppButtonTemplate" };
        Check(UiHostRules.Own("Contacts", game) && UiHostRules.Own("AppButtonTemplate(Clone)", game) && UiHostRules.Own("RealLife MoreApps", game)
              && UiHostRules.Own("RealLifePlanner", game) && !UiHostRules.Own("BAMP_Chat", game) && !UiHostRules.Own("EmpireBank", game) && !UiHostRules.Own(string.Empty, game),
            "ui host: another mod's object is told from the game's (a copy of a game's template too) and the mod's own");
        Check(UiHostRules.Above(3) == 4, "ui host: a kit canvas stands just above the screen it covers");
        // One way over the game's screens: the canvases walked and the leaves hidden only in the host.
        string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts\Ui"));
        string copies = "none";
        int files = 0;
        foreach (string path in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains(System.IO.Path.DirectorySeparatorChar + "Host" + System.IO.Path.DirectorySeparatorChar))
            {
                continue;
            }
            files++;
            string text = File.ReadAllText(path);
            if (text.Contains("GetComponentsInParent<Canvas>") || text.Contains(".blocksRaycasts = false") || text.Contains("UiBindRules.Stops("))
            {
                copies = System.IO.Path.GetFileName(path);
            }
        }
        Check(files >= 40 && copies == "none", "ui host: no screen binds over the game or hides its pieces its own way; every one goes through the host (" + copies + ")");
        string host = File.ReadAllText(System.IO.Path.Combine(folder, "Host", "UiGameHost.cs"));
        Check(host.Contains("GetComponentsInParent<Canvas>(true)") && host.Contains("UiBindRules.Stops(") && host.Contains("UiBindRules.Wait(") && host.Contains("menu.alpha")
              && host.Contains("_back()") && host.Contains("Guard.Fail(FrameGuard, e, null)") && host.Contains("UiHostRules.Above("),
            "ui host: the host binds over switched off screens, looks again, follows the menu's fade, hands a call back, falls back on a fault and stands just above the screen");
        ShellBoxChecks(folder);
        string kit = File.ReadAllText(System.IO.Path.Combine(folder, "Contacts", "ContactsKit.cs"));
        Check(kit.Contains("new UiGameHost(new ContactsGameScreen())") && kit.Contains("StepAside(") && kit.Contains("\"Layout 30-70\""),
            "ui host: Contacts runs on the host, its layout the one leaf it hides, its call handed back by the host");
        // Each runner has its own switch (the two galleries are the kit's tools, not screens of play), and each switch its words.
        var en = LocaleFile("en");
        var pt = LocaleFile("pt");
        var zh = LocaleFile("zh-cn");
        string unswitched = "none", unworded = "none";
        foreach (UiScreenDef def in UiScreens.All)
        {
            if (def.Runner != null && def.Id != "gallery" && def.Id != "kit_parts" && def.Section == null)
            {
                unswitched = def.Id;
            }
            if (def.Section == null)
            {
                continue;
            }
            string label = UiScreenSwitches.LabelOf(def.Id), tip = "reallife_opt_tip_" + UiScreenSwitches.OptionOf(def.Id);
            foreach (var locale in new[] { en, pt, zh })
            {
                if (!locale.ContainsKey(label) || !locale.ContainsKey(tip))
                {
                    unworded = def.Id;
                }
            }
            if (Array.IndexOf(UiScreenSwitches.Sections, def.Section) < 0 || !en.ContainsKey("reallife_opt_sec_" + def.Section))
            {
                unworded = def.Id + " (its section)";
            }
        }
        Check(unswitched == "none", "ui switches: every screen the game runs has its own switch in Mod settings (" + unswitched + ")");
        Check(unworded == "none", "ui switches: every switch has its name, its tip and its section's title in English, Portuguese and Chinese (" + unworded + ")");
        Check(UiScreenSwitches.OptionOf("contacts") == "ui_screen_contacts" && UiScreenSwitches.On("contacts") && UiScreenSwitches.On("no such screen"),
            "ui switches: a switch's id follows from its screen's, it starts on, and a screen without one is always on");
        UiScreenSwitches.Set("ui_screen_contacts", false);
        bool off = !UiScreenSwitches.On("contacts");
        UiScreenSwitches.Set("ui_screen_contacts", true);
        Check(off && UiScreenSwitches.On("contacts"), "ui switches: flipping one changes that screen alone");
        // Every section the page lists has its const, every const is listed, every switch starts on, and a section's switches
        // read back in it (a hand merge of the four branches that each added a section once lost a part of one).
        var consts = new List<string>();
        foreach (var field in typeof(UiScreenSwitches).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        {
            if (field.IsLiteral && field.FieldType == typeof(string) && ((string)field.GetRawConstantValue()).StartsWith("uiscreens_", StringComparison.Ordinal))
            {
                consts.Add((string)field.GetRawConstantValue());
            }
        }
        string sectionGap = "none";
        foreach (string c in consts)
        {
            if (Array.IndexOf(UiScreenSwitches.Sections, c) < 0 || !en.ContainsKey("reallife_opt_sec_" + c) || !pt.ContainsKey("reallife_opt_sec_" + c) || !zh.ContainsKey("reallife_opt_sec_" + c))
            {
                sectionGap = c;
            }
        }
        foreach (string listed in UiScreenSwitches.Sections)
        {
            if (!consts.Contains(listed))
            {
                sectionGap = listed;
            }
        }
        string offByDefault = "none", rowsGap = "none";
        foreach (UiScreenDef def in UiScreenSwitches.All)
        {
            if (!UiScreenSwitches.Value(UiScreenSwitches.OptionOf(def.Id)) || !UiScreenSwitches.On(def.Id))
            {
                offByDefault = def.Id;
            }
            if (Array.IndexOf(UiScreenSwitches.OptionIds(def.Section), UiScreenSwitches.OptionOf(def.Id)) < 0)
            {
                rowsGap = def.Id;
            }
        }
        Check(sectionGap == "none" && consts.Count == UiScreenSwitches.Sections.Length && offByDefault == "none" && rowsGap == "none",
            "ui switches: every section has its const, its place on the page and its title in three languages, every switch starts on and stands in its section (" + sectionGap + " " + offByDefault + " " + rowsGap + ")");
        // Every app drawn on the kit (a PhoneAppDef with a KitScreen) hands its face to the frame; without that call the phone shows the game's old panel.
        string unfaced = "none";
        int kitApps = 0;
        foreach (string path in Directory.GetFiles(DesignScripts, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            if (text.Contains("KitScreen = ") && !path.EndsWith("PhoneAppDef.cs", StringComparison.Ordinal))
            {
                kitApps++;
                // The overhaul's own pages (Ui/Shell/ShellPages) carry their kit host themselves and bind it through
                // ShellPages.Built, which UiShell.Built calls on the panel's first build; every other app hands its face over.
                bool pages = path.EndsWith("ShellPages.cs", StringComparison.Ordinal) && text.Contains("p.Host.Install()");
                if (!text.Contains("UiShell.Face(") && !pages)
                {
                    unfaced = System.IO.Path.GetFileName(path);
                }
            }
        }
        Check(kitApps >= 9 && unfaced == "none", "ui apps: every mod app drawn on the kit gives its face to the frame, so its button opens the kit screen when its switch is on (" + kitApps + " apps; " + unfaced + ")");
        foreach (UiScreenDef def in UiScreenSwitches.All)
        {
            if (def.Id == "flare")
            {
                Check(def.Section == UiScreenSwitches.Social && UiScreenSwitches.On("flare"), "ui switches: Flare's face is on by default, in the social section");
            }
        }
        // The app row: the phone's first page then All apps, its commands the frame's and never a screen's.
        var bar = new UiStatusModel { Apps = new UiIcon[12], AllApps = true };
        Check(UiDraw.AppsShown(bar, 1920f) == 10 && UiDraw.IsChrome(UiDraw.AppRowCommand) && UiDraw.IsChrome(UiDraw.AllAppsCommand) && !UiDraw.IsChrome(UiDraw.CloseCommand)
              && !UiDraw.IsChrome(1) && UiDraw.AppsShown(new UiStatusModel { Apps = new UiIcon[12] }, 1920f) == 12,
            "ui frame: the app row shows the phone's first page then All apps, a row without All apps shows what it is given, and its commands are the frame's alone");
        var s = new UiScene();
        UiChrome.Bar = () => UiSamples.ShellBar(UiSamples.RowContacts, false);
        UiListDetail.Lay(s, UiSamples.ContactsWindow(UiWords("en")), UiFrame.FullMenu(16f / 9f, "Contacts"), true, UiDefaultKeys.Keyboard);
        UiChrome.Bar = null;
        int row = 0, open = -1;
        for (int i = 0; i < s.NodeCount; i++)
        {
            if (s.Nodes[i].Command == UiDraw.AppRowCommand)
            {
                row++;
                int face = s.Nodes[i].Face;
                open = face >= 0 && (s.Items[face].Flags & UiFlags.Selected) != 0 ? s.Nodes[i].Arg : open;
            }
        }
        Check(row == UiSamples.RowIds.Length && open == UiSamples.RowContacts && s.FindNode(UiDraw.AllAppsCommand, 0) >= 0 && UiCheckRules.Crowded(s, null) == 0,
            "ui frame: the status bar under an app carries the app row (" + row + " apps, the open one filled) and All apps, counted as the frame's, not the page's targets");
        Check(UiShellApps.FolderOf("Contacts", UiShellKind.Game, -1) == UiShellApps.Life && UiShellApps.FolderOf("BizMan", UiShellKind.Game, -1) == UiShellApps.Business
              && UiShellApps.FolderOf("EconoView", UiShellKind.Game, -1) == UiShellApps.Money && UiShellApps.FolderOf("BAMP_Chat", UiShellKind.Other, UiShellApps.Money) == UiShellApps.Other
              && UiShellApps.FolderOf("RealLifeBanks", UiShellKind.Mod, UiShellApps.Setup) == UiShellApps.Setup
              && UiShellApps.FolderOf("RealLifeBanks", UiShellKind.Mod, -1, RealLifeOverhaul.GameUi.PhoneLayoutRules.DefaultOf("RealLifeBanks")) == RealLifeOverhaul.GameUi.PhoneLayoutRules.Money,
            "ui frame: every app stands in one All apps folder: where the player keeps it, else its kind's, another mod's in Other mods");
        bool same = UiShellApps.Other == RealLifeOverhaul.GameUi.PhoneLayoutRules.FolderCount && UiShellApps.Money == RealLifeOverhaul.GameUi.PhoneLayoutRules.Money
                    && UiShellApps.Setup == RealLifeOverhaul.GameUi.PhoneLayoutRules.Setup;
        for (int f = 0; f < RealLifeOverhaul.GameUi.PhoneLayoutRules.FolderCount; f++)
        {
            same &= UiShellApps.FolderKey(f) == RealLifeOverhaul.GameUi.PhoneLayoutRules.FolderKeys[f];
        }
        Check(same, "ui frame: All apps' folders are the phone's own, in their order and by their names");
    }
}

internal static partial class Program
{
    /// <summary>A recording stand-in for the game's interface: it says which boxes are built now and writes down every walk made for one.</summary>
    private sealed class RecordingBoxScene : IShellBoxScene
    {
        public readonly List<ShellBox> Walks = new List<ShellBox>();
        public readonly HashSet<ShellBox> Built_ = new HashSet<ShellBox>();
        public bool Ready = true;

        public bool Built => Ready;

        public bool Holds(ShellBox box)
        {
            Walks.Add(box);
            return Built_.Contains(box);
        }
    }

    // How the kit looks for the game's question boxes and its acknowledgement (ShellBoxes, ShellBoxLook): the first look found
    // the acknowledgement missing and the kit gave it up for the city. Now a box not there at the first look is looked for
    // again while one of its kind is up, and found when built. (The full menu's box, found at the first look, was refused
    // for a panel its scene never gives it: ShellBoxes.Check no longer asks for one.)
    private static void ShellBoxChecks(string folder)
    {
        // The full menu's box built after the first look: not found without a box up, found at the look made as it opens.
        var scene = new RecordingBoxScene();
        scene.Built_.Add(ShellBox.HudAsk);
        var look = new ShellBoxLook();
        bool hud = look.Seek(ShellBox.HudAsk, false, scene);
        bool menu1 = look.Seek(ShellBox.MenuAsk, false, scene);
        Check(hud && !menu1 && look.Looks(ShellBox.MenuAsk) == 1 && look.FirstMiss(ShellBox.MenuAsk) && !look.FirstMiss(ShellBox.MenuAsk),
            "ui boxes: the first look finds the HUD's box and misses the full menu's, the miss told once");
        for (int i = 0; i < 500; i++)
        {
            look.Seek(ShellBox.MenuAsk, false, scene);
            look.Seek(ShellBox.Acknowledge, false, scene);
        }
        Check(scene.Walks.Count == 3 && scene.Walks[0] == ShellBox.HudAsk && scene.Walks[1] == ShellBox.MenuAsk && scene.Walks[2] == ShellBox.Acknowledge,
            "ui boxes: nothing is walked while no box is up, after the first look (" + scene.Walks.Count + " walks: the three first looks, then none in 1000 asks)");
        scene.Built_.Add(ShellBox.MenuAsk);   // the game builds the full menu's box late
        bool stillOff = look.Seek(ShellBox.MenuAsk, false, scene);
        bool late = look.Seek(ShellBox.MenuAsk, true, scene);
        Check(!stillOff && late && look.Looks(ShellBox.MenuAsk) == 2 && look.FoundLate(ShellBox.MenuAsk) && !look.FoundLate(ShellBox.MenuAsk) && scene.Walks.Count == 4,
            "ui boxes: the full menu's box built after the first look is found at the look made when a question is up, and said once");
        int walks = scene.Walks.Count;
        look.Seek(ShellBox.MenuAsk, true, scene);
        Check(scene.Walks.Count == walks && look.Found(ShellBox.MenuAsk), "ui boxes: a box found stays found, with no more walks");
        // The acknowledgement built after the first look.
        var ack = new ShellBoxLook();
        var ackScene = new RecordingBoxScene();
        bool first = ack.Seek(ShellBox.Acknowledge, true, ackScene);
        ackScene.Built_.Add(ShellBox.Acknowledge);
        bool second = ack.Seek(ShellBox.Acknowledge, true, ackScene);
        Check(!first && second && ack.Looks(ShellBox.Acknowledge) == 2 && ack.FoundLate(ShellBox.Acknowledge) && ackScene.Walks.TrueForAll(w => w == ShellBox.Acknowledge),
            "ui boxes: the acknowledgement built after the first look is found at the second");
        // A second look after a miss happens only while a box is up, and only a few times a city.
        var never = new ShellBoxLook();
        var empty = new RecordingBoxScene();
        for (int i = 0; i < 40; i++)
        {
            never.Seek(ShellBox.MenuAsk, true, empty);
        }
        Check(empty.Walks.Count == ShellBoxLook.MostLooks && empty.Walks.TrueForAll(w => w == ShellBox.MenuAsk) && !never.Found(ShellBox.MenuAsk),
            "ui boxes: a box never built is looked for " + ShellBoxLook.MostLooks + " times a city at most, with a box up (" + empty.Walks.Count + ")");
        // Before the game's interface is there a look is not counted; a new city looks again.
        var early = new RecordingBoxScene { Ready = false };
        var pending = new ShellBoxLook();
        pending.Seek(ShellBox.MenuAsk, true, early);
        bool notCounted = pending.Looks(ShellBox.MenuAsk) == 0 && early.Walks.Count == 0;
        early.Ready = true;
        early.Built_.Add(ShellBox.MenuAsk);
        bool then = pending.Seek(ShellBox.MenuAsk, false, early);
        pending.Rearm();
        Check(notCounted && then && early.Walks.Count == 1 && pending.Looks(ShellBox.MenuAsk) == 0 && !pending.Found(ShellBox.MenuAsk) && !pending.FoundLate(ShellBox.MenuAsk),
            "ui boxes: a look before the interface is built is not counted, and a new city looks again");
        // The shell looks through the rule, and the full menu's box is no longer held to a panel it was never given.
        string boxes = File.ReadAllText(System.IO.Path.Combine(folder, "Shell", "ShellBoxes.cs"));
        Check(boxes.Contains("Seen.Seek(box, open, Walker)") && !boxes.Contains("_looked") && !boxes.Contains("AskPanel") && boxes.Contains("AskOf(_menu)")
              && boxes.Contains("AcknowledgeWarning.isOpen") && boxes.Contains("GameConfirm.IsOpen"),
            "ui boxes: the shell looks through the rule (a miss is looked at again while a box is up), no once-a-city flag and no panel required");
    }
}
