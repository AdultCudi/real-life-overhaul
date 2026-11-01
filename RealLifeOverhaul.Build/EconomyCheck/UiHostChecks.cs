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
