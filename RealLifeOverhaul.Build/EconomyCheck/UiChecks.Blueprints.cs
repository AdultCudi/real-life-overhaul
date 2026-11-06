using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Ui;

// The pause menu's blueprints window and the game's help window on the kit: every state laid out at every frame in every language (UiCases), the rules the
// windows work out themselves (stars, what a layout holds, the search over a loaded page, the markdown of a help page), every target drawn is one the window
// answers with arguments that reach something, the two windows' commands are their own block, and, from their own text, that every press goes to the game's own
// button or call, that the Escape cascade is the game's own, that no path leaves a game-styled window standing, and that the pages the mod writes into the
// game's help stay in its list. The game is not there to ask, so the presses are proved from the layouts and from the screens' own source.
internal static partial class Program
{
    private static readonly int[] BlueprintCommandsAll = BlueprintsUiCommands();

    private static int[] BlueprintsUiCommands() => new List<int>(UiBlueprints.Commands).ToArray();

    static partial void UiCasesBlueprints(UiScreenDef def, List<UiScreenCase> cases)
    {
        if (def.Id == "blueprints")
        {
            foreach (string state in UiSamples.BlueprintStates)
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "blueprints (" + st + ")", Title = "Blueprints", Commands = BlueprintCommandsAll,
                    Lay = (s, f, dark, k, t) => UiBlueprints.Lay(s, UiSamples.Blueprints(t, st), f, dark, k)
                });
            }
        }
        else if (def.Id == "game_help")
        {
            foreach (string state in UiSamples.HelpStates)
            {
                string st = state;
                cases.Add(new UiScreenCase
                {
                    Def = def, Name = "game_help (" + st + ")", Title = "Help", Commands = new List<int>(UiHelp.Commands).ToArray(),
                    Lay = (s, f, dark, k, t) => UiHelp.Lay(s, UiSamples.Help(t, st), f, dark, k)
                });
            }
        }
    }

    private static string BlueprintsRoot()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10 && dir != null; i++)
        {
            string path = System.IO.Path.Combine(dir, "Assets", "Mods", "RealLifeOverhaul", "Scripts");
            if (Directory.Exists(path))
            {
                return path;
            }
            dir = System.IO.Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static string BlueprintsRead(string relative)
    {
        string root = BlueprintsRoot();
        string file = root != null ? System.IO.Path.Combine(root, relative) : null;
        return file != null && File.Exists(file) ? File.ReadAllText(file) : string.Empty;
    }

    private static UiScene BlueprintsScene(Action<UiScene> lay)
    {
        var s = new UiScene();
        lay(s);
        return s;
    }

    private static List<int> BlueprintArgs(UiScene s, int command)
    {
        var args = new List<int>();
        for (int i = 0; i < s.NodeCount; i++)
        {
            if (s.Nodes[i].Command == command)
            {
                args.Add(s.Nodes[i].Arg);
            }
        }
        return args;
    }

    /// <summary>The blueprints window's and the help window's own checks, alone: <c>--blueprints-ui</c> and <c>--help-ui</c>.</summary>
    private static void BlueprintsUiChecks()
    {
        UiWords en = (key, english) => english;
        // Stars: the game keeps a rating of 0 to 1, the window shows it out of five by halves and as one figure.
        Check(BlueprintsRules.Halves(0.96f) == 10 && BlueprintsRules.Halves(0.5f) == 5 && BlueprintsRules.Halves(0.14f) == 1 && BlueprintsRules.Halves(0f) == 0 && BlueprintsRules.Halves(2f) == 10
              && BlueprintsRules.Halves(float.NaN) == 0, "blueprints: a rating of 0 to 1 is stars by halves, never past five");
        Check(BlueprintsRules.RatingFigure(0.96f) == "4.8" && BlueprintsRules.RatingFigure(1f) == "5.0" && BlueprintsRules.RatingFigure(-1f) == "0.0", "blueprints: the figure beside the stars is the rating out of five");
        Check(!BlueprintsRules.Rated(false, 0.9f) && !BlueprintsRules.Rated(true, 0f) && BlueprintsRules.Rated(true, 0.4f), "blueprints: a blueprint nobody rated, or one never on the Workshop, carries no stars");
        // What a layout holds, counted by the game's item types (ItemType flags): one kind a thing, the first that fits.
        Check(BlueprintsRules.KindOf(0x8) == BlueprintsRules.Counters && BlueprintsRules.KindOf(0x40 | 0x1) == BlueprintsRules.Shelves && BlueprintsRules.KindOf(0x200000 | 0x1) == BlueprintsRules.Tables
              && BlueprintsRules.KindOf(0x1000) == BlueprintsRules.Seats && BlueprintsRules.KindOf(0x20000) == BlueprintsRules.Workstations && BlueprintsRules.KindOf(0x80) == BlueprintsRules.Machines
              && BlueprintsRules.KindOf(0x1) == -1 && BlueprintsRules.KindOf(0x8 | 0x200000) == BlueprintsRules.Counters, "blueprints: an item counts under one kind of the Inside list by its type, a lamp or a plant under none");
        Check(BlueprintsRules.KindKeys.Length == BlueprintsRules.KindCount && BlueprintsRules.KindIcons.Length == BlueprintsRules.KindCount, "blueprints: every kind of the Inside list has its words and its glyph");
        // The search over the page that is loaded: every word, whatever the case and the accents.
        Check(BlueprintsRules.Matches("Corner cafe, two floors", "CAFE") && BlueprintsRules.Matches("Café Corner", "cafe") && BlueprintsRules.Matches("Corner cafe", "corner cafe")
              && !BlueprintsRules.Matches("Bakery with seating", "cafe") && BlueprintsRules.Matches("anything", "  ") && !BlueprintsRules.Matches(null, "a"), "blueprints: the search finds every word of a name, whatever the case and the accents");
        Check(BlueprintsRules.PageLine(en, 2, 3) == "Page 2 of 3" && BlueprintsRules.PageLine(en, 3, -1) == "Page 3" && BlueprintsRules.PageLine(en, 0, 1) == "Page 1 of 1", "blueprints: the page's words, with no total while the Workshop has not said one");
        Check(BlueprintsRules.CountLine(en, 3, 16) == "3 of 16" && BlueprintsRules.SizeLine(en, "Medium2", 2) == "Medium2, 2 floors" && BlueprintsRules.SizeLine(en, "Small1", 1) == "Small1", "blueprints: how many the search leaves, and a size with its floors only when it has more than one");
        Check(BlueprintsRules.PageSize == 16, "blueprints: the game's page of sixteen stays");
        // The block of commands is this window's own, clear of the shell's and the reader's.
        foreach (int c in UiBlueprints.Commands)
        {
            Check(c == UiAppWindow.NavCommand || (c >= 9520 && c <= 9529), "blueprints: command " + c + " is the shell's tab or in the block 9520 to 9529");
        }
        // Every state: every target is one the window answers, and its arguments reach a card, an action, an option or a row.
        var tabsSeen = new HashSet<string>();
        foreach (string state in UiSamples.BlueprintStates)
        {
            UiBlueprintsModel m = UiSamples.Blueprints(en, state);
            UiScene s = BlueprintsScene(scene => UiBlueprints.Lay(scene, m, UiFrame.FullMenu(16f / 9f, "Blueprints"), true, UiDefaultKeys.Keyboard));
            for (int i = 0; i < s.NodeCount; i++)
            {
                int c = s.Nodes[i].Command;
                Check(c == UiDraw.CloseCommand || Array.IndexOf(BlueprintCommandsAll, c) >= 0, "blueprints (" + state + "): the target of command " + c + " is one the window answers");
            }
            List<int> tabs = BlueprintArgs(s, UiAppWindow.NavCommand);
            Check(tabs.Contains(0) && tabs.Contains(1), "blueprints (" + state + "): the Library and the Workshop are both drawn as tabs");
            foreach (int card in BlueprintArgs(s, UiBlueprints.CardCommand))
            {
                Check(card >= 0 && card < m.CardCount, "blueprints (" + state + "): a card's target " + card + " is one of the " + m.CardCount + " cards");
            }
            List<int> actions = BlueprintArgs(s, UiBlueprints.ActionCommand);
            Check(actions.Count == (m.HasDetail ? m.Actions.Length : 0), "blueprints (" + state + "): every action of the detail is drawn once (" + actions.Count + " of " + (m.HasDetail ? m.Actions.Length : 0) + ")");
            foreach (int a in actions)
            {
                Check(a >= 0 && a < m.Actions.Length && m.Actions[a].Command == UiBlueprints.ActionCommand, "blueprints (" + state + "): action " + a + " reaches its own button");
            }
            foreach (int row in BlueprintArgs(s, UiBlueprints.FilterRowCommand))
            {
                Check(row >= 0 && row < m.FilterRows.Length && !m.FilterRows[row].Group, "blueprints (" + state + "): a filter row's target " + row + " is an option, never a caption");
            }
            foreach (int option in BlueprintArgs(s, UiBlueprints.SortOptionCommand))
            {
                Check(option >= 0 && option < m.SortOptions.Length, "blueprints (" + state + "): a sort option's target " + option + " is one the game's sort holds");
            }
            foreach (int step in BlueprintArgs(s, UiBlueprints.PageCommand))
            {
                Check(step == -1 || step == 1, "blueprints (" + state + "): the page turner goes by one, back or on");
            }
            bool terms = BlueprintArgs(s, UiBlueprints.ConfirmCommand).Count == (m.Confirm ? 3 : 0);
            Check(terms, "blueprints (" + state + "): the Workshop's terms show their three buttons only while the game asks");
            tabsSeen.Add(string.Join("|", Array.ConvertAll(m.Cards, c => c.Name ?? string.Empty)) + m.Tab);
        }
        Check(tabsSeen.Count >= 2, "blueprints: the Library and the Workshop give pages of their own (" + tabsSeen.Count + " different)");
        // A page the turner cannot go to is drawn held; the way on shows on the Workshop's first page.
        UiBlueprintsModel first = UiSamples.Blueprints(en, "workshop");
        UiScene held = BlueprintsScene(scene => UiBlueprints.Lay(scene, first, UiFrame.FullMenu(16f / 9f, "Blueprints"), true, UiDefaultKeys.Keyboard));
        bool backHeld = false, nextOpen = false;
        for (int i = 0; i < held.NodeCount; i++)
        {
            backHeld |= held.Nodes[i].Command == UiBlueprints.PageCommand && held.Nodes[i].Arg == -1 && held.Nodes[i].Disabled;
            nextOpen |= held.Nodes[i].Command == UiBlueprints.PageCommand && held.Nodes[i].Arg == 1 && !held.Nodes[i].Disabled;
        }
        Check(backHeld && nextOpen, "blueprints: on the first page Previous is held and Next is open");
        // The source: every press goes to the game's own button, toggle, field or call, in the game's own order.
        string screen = BlueprintsRead(@"Ui\Blueprints\BlueprintsScreen.cs"), kits = BlueprintsRead(@"Ui\Blueprints\BlueprintsKits.cs"), pause = BlueprintsRead(@"Ui\Pause\PauseScreen.cs");
        Check(screen.Contains("element.ShowElementInfo();") && screen.Contains("UiWidgets.Press(_actions[arg])") && screen.Contains("UiWidgets.Set(_sorts[arg], true)") && screen.Contains("UiWidgets.Set(t, !t.isOn)"),
            "blueprints: a card, an action, a sort and a filter are pressed through the game's own element, buttons and toggles");
        Check(screen.Contains("UiWidgets.Press(_tabs[button])") && screen.Contains("UiWidgets.Press(b)") && screen.Contains("UiWidgets.Type(_search, text, false)"),
            "blueprints: the tabs, the page's buttons and the Library's search are the game's own buttons and field");
        Check(screen.Contains("_panel.IsWorkshopConfirmOpen") && screen.Contains("_panel.CloseWorkshopConfirm();") && screen.Contains("_panel.IsBlueprintInfoOpen") && screen.Contains("_panel.CloseBlueprintInfo();")
              && screen.Contains("_menu.CloseBlueprints();"), "blueprints: Back steps out of the terms, then the chosen blueprint, then closes the window, as the game's own Cancel does");
        Check(screen.Contains("_selected.UploadToSteamWorkshop();") && screen.Contains("_selected.ReadWorkshopTermsOfService();") && screen.Contains("_panel.CloseWorkshopConfirm();"),
            "blueprints: the Workshop's terms go to the game's own upload, terms and cancel");
        Check(screen.Contains("if (UiBoxes.Up)") && screen.Contains("public bool TakesBack => !UiBoxes.Up;"), "blueprints: a question of the game's over the window is the question's, not the window's");
        Check(!screen.Contains(".Classic(") && !kits.Contains("Classic(") && !screen.Contains("Application.Quit") && !screen.Contains("LoadScene"),
            "blueprints: no path hands the player the game's styled window, quits the game or loads a scene");
        Check(screen.Contains("Local(chosen)") && screen.Contains("chosen.GetLayout(false)") && !screen.Contains("Update("), "blueprints: a layout is read only for a blueprint with its own files, once, and nothing runs a frame");
        Check(kits.Contains("UiGameKind.Box") && kits.Contains("menu.blueprintsPanel") && kits.Contains("UiScreenSwitches.On(BlueprintsId)") && kits.Contains("Blueprints.Remove(\"its switch is off\")"),
            "blueprints: the window is a box over the pause menu's own panel, with its own switch giving the game's panel back");
        Check(pause.Contains("case \"BlueprintsButton\":") && pause.Contains("UiWidgets.Press(_buttons[arg])"), "blueprints: the pause menu's Blueprints row presses the game's own button, which opens the panel the kit then draws");
        Check(BlueprintsRead(@"Ui\UiDirector.cs").Contains("BlueprintsKits.Install();") && BlueprintsRead(@"Ui\UiDirector.cs").Contains("BlueprintsKits.Remove(why);")
              && BlueprintsRead(@"Ui\UiDirector.cs").Contains("BlueprintsKits.Rearm();"), "blueprints: the director installs, removes and re-arms the window with the others");
        Check(BlueprintsRead(@"Ui\UiGuide.cs").Contains("reallife_ui_test_blueprints") && BlueprintsRead(@"Ui\UiGuide.cs").Contains("reallife_ui_test_bp_switch"), "blueprints: Options, Tests opens the window and flips its switch");
        // Every game member the screen names is a line of the contract's part.
        string contract = BlueprintsRead(@"Core\GameContract.Blueprints.cs");
        int names = 0, missing = 0;
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(screen, @"GameContract\.(Bp\w+)"))
        {
            names++;
            if (!contract.Contains("GameMember " + match.Groups[1].Value + " "))
            {
                missing++;
            }
        }
        Check(names > 20 && missing == 0, "blueprints: every game member the screen reads is a line of the contract (" + names + " read, " + missing + " missing)");
        Check(UiScreens.All != null && Array.Exists(UiScreens.All, d => d.Id == "blueprints" && d.Section == UiScreenSwitches.Pause), "blueprints: the screen is registered with its own switch in the pause section");
    }

    private static void HelpUiChecks()
    {
        UiWords en = (key, english) => english;
        var slugs = new List<string>();
        // A page of the game's: a list of links is links, a sentence that names pages is bold with a link after it, a place is a link of its own kind.
        string page = "**Gift Shop** businesses operate out of retail buildings.\n\nSome items can be ordered from [Wholesalers](wholesalers-locations) and the rest from [Importers](importers-contract) in your [Headquarters](businesstypes-headquarters).\n\n"
                      + "The business requires:\n\n* [Stack of Shopping Baskets](furniture-stackofshoppingbaskets)\n* [Gift (Cheap)](products-cheapgift)\n* At least one product to sell\n*  [Square Appliances](address:16 4a)\n";
        UiReaderBlock[] blocks = HelpRules.Parse(page, slugs, out bool media);
        int links = 0, bullets = 0, paragraphs = 0;
        foreach (UiReaderBlock b in blocks)
        {
            links += b.Kind == UiReaderKind.Link ? 1 : 0;
            bullets += b.Kind == UiReaderKind.Bullet ? 1 : 0;
            paragraphs += b.Kind == UiReaderKind.Paragraph ? 1 : 0;
            Check(b.Text == null || (b.Text.IndexOf("**", StringComparison.Ordinal) < 0 && b.Text.IndexOf("](", StringComparison.Ordinal) < 0), "help: no mark of the markdown reaches the screen (" + b.Kind + ")");
        }
        Check(!media && links == 6 && bullets == 1 && paragraphs == 3, "help: a page's lists and sentences become links, a bullet and paragraphs (" + links + " links, " + bullets + " bullets, " + paragraphs + " paragraphs)");
        Check(slugs.Count == 6 && slugs.Contains("address:16 4a") && slugs.Contains("products-cheapgift"), "help: each page it links to is noted once, the map link among them");
        foreach (UiReaderBlock b in blocks)
        {
            Check(b.Kind != UiReaderKind.Link || (b.Target >= 0 && b.Target < slugs.Count), "help: a link block's target is one of the page's links");
        }
        Check(blocks[0].Kind == UiReaderKind.Paragraph && blocks[0].Text.StartsWith("<b>Gift Shop</b>", StringComparison.Ordinal), "help: a bold name is bold in the reader");
        Check(blocks[1].Text.Contains("<b>Wholesalers</b>") && blocks[2].Kind == UiReaderKind.Link && blocks[2].Text == "Wholesalers", "help: a link inside a sentence stands out in it and has its own link after the sentence");
        HelpRules.Parse("Text\n\n![a picture](x.png)\n\nMore", slugs, out bool picture);
        HelpRules.Parse("Text\n\n<video src=\"clip.mp4\"></video>", slugs, out bool video);
        Check(picture && video, "help: a picture or a video is told, for the game's own view");
        Check(HelpRules.Parse(null, slugs, out _).Length == 0 && HelpRules.Parse("# One\n\n## Two\n\n1. First\n\n> Note", slugs, out _).Length == 4, "help: an empty page is nothing, headings, numbers and notes are the reader's kinds");
        Check(HelpRules.IsPlace("address:16 4a") && !HelpRules.IsPlace("furniture-x") && !HelpRules.IsPlace(null), "help: a place link is told from a page");
        Check(HelpRules.IconOf("reallife_staff_cat_places") != HelpRules.IconOf("common_finance") && HelpRules.IconOf("help_general") == UiIcon.Info && HelpRules.IconOf("vehicles_title") == UiIcon.Car,
            "help: each category of the list has its own glyph, the mod's two among them");
        foreach (int c in UiHelp.Commands)
        {
            Check(c == UiAppWindow.NavCommand || c == UiReader.AnchorCommand || c == UiReader.RelatedCommand || (c >= 9530 && c <= 9539), "help: command " + c + " is the shell's, the reader's or in the block 9530 to 9539");
        }
        foreach (string state in UiSamples.HelpStates)
        {
            UiHelpModel m = UiSamples.Help(en, state);
            UiScene s = BlueprintsScene(scene => UiHelp.Lay(scene, m, UiFrame.FullMenu(16f / 9f, "Help"), true, UiDefaultKeys.Keyboard));
            for (int i = 0; i < s.NodeCount; i++)
            {
                int c = s.Nodes[i].Command;
                Check(c == UiDraw.CloseCommand || Array.IndexOf(UiHelp.Commands, c) >= 0, "help (" + state + "): the target of command " + c + " is one the window answers");
            }
            foreach (int p in BlueprintArgs(s, UiAppWindow.NavCommand))
            {
                Check(p >= 0 && p < m.Nav.Length, "help (" + state + "): a page's target " + p + " is one of the " + m.Nav.Length + " in the list");
            }
            Check(BlueprintArgs(s, UiHelp.SearchCommand).Count == 1, "help (" + state + "): the search is drawn once");
        }
        UiHelpModel none = UiSamples.Help(en, "none");
        Check(!none.HasPage && none.Nav.Length == 0, "help: a search nothing matches says so and lists no page");
        UiHelpModel found = UiSamples.Help(en, "search");
        Check(found.Nav.Length > 0 && found.Nav.Length < UiSamples.Help(en, "page").Nav.Length, "help: a search narrows the list of pages");
        UiHelpModel staff = UiSamples.Help(en, "staff");
        Check(Array.Exists(staff.Nav, e => e.Group != null && e.Group.IndexOf("Staff", StringComparison.Ordinal) >= 0), "help: the mod's Staff and roles lists stand in the list beside the game's own");
        // The source: the list is the game's own, every press goes to its own call, the video stays the game's, Escape is the game's own.
        string screen = BlueprintsRead(@"Ui\Help\HelpScreen.cs"), kits = BlueprintsRead(@"Ui\Help\HelpKits.cs"), staffGame = BlueprintsRead(@"Meta\StaffHelpGame.cs"), hud = BlueprintsRead(@"Ui\HudScreens\HudTopGame.cs");
        Check(screen.Contains("GameContract.HelpGenerated") && screen.Contains("cat.links") && screen.Contains("link.gameObject.activeSelf") && screen.Contains("link.rawTranslatedKey"),
            "help: the sidebar is the game's own list, as its search leaves it, with the pages the mod puts in it");
        Check(staffGame.Contains("categories.Add(group);") && staffGame.Contains("load.Invoke(help, null);") && staffGame.Contains("StaffIndex.Sorted("),
            "help: the Staff and roles lists are still written into the game's own list, and found by its own search");
        Check(screen.Contains("_help.OpenLink(slug);") && screen.Contains("_help.HistoryBack();") && screen.Contains("_help.HistoryForward();") && screen.Contains("UiWidgets.Type(_help.searchField,"),
            "help: a page, a link, the way back and forward and the search are the game's own calls");
        Check(screen.Contains("_help.Toggle(false);") && screen.Contains("string.IsNullOrEmpty(field.text)") && screen.Contains("public bool TakesBack => !UiBoxes.Up;"),
            "help: Back steps out of a search, then closes the help as the game's own Cancel does");
        Check(screen.Contains("HelpKits.Classic();") && screen.Contains("HelpRules.MediaLink") && !screen.Contains("Application.Quit"), "help: a page with a picture or a video is the game's own view, and the only hand-over");
        Check(screen.Contains("(\"help_\" + prefix + \"_content\").GetLocalization()"), "help: a page's words are the game's own table's, where the mod writes its pages too");
        Check(kits.Contains("UiGameKind.Box") && kits.Contains("help.container.transform") && kits.Contains("help.dimmer") && kits.Contains("UiScreenSwitches.On(HelpId)") && kits.Contains("Help.Remove(\"its switch is off\")"),
            "help: the window is a box over the game's help container, hiding its dimmer, with its own switch giving the game's help back");
        Check(hud.Contains("UiWidgets.Press(_help)"), "help: the HUD's info glyph presses the game's own help button, which opens the help the kit then draws");
        string shops = BlueprintsRead(@"Shops\ShopsGame.cs"), build = BlueprintsRead(@"Construction\BuildModeGame.cs");
        Check(shops.Contains("help.Toggle(true, slug)"), "help: the mod's own links to a kind of shop open the game's help, which the kit draws");
        Check(BlueprintsRead(@"Ui\UiDirector.cs").Contains("HelpKits.Install();") && BlueprintsRead(@"Ui\UiDirector.cs").Contains("HelpKits.Remove(why);") && BlueprintsRead(@"Ui\UiDirector.cs").Contains("HelpKits.Rearm();"),
            "help: the director installs, removes and re-arms the window with the others");
        Check(BlueprintsRead(@"Ui\UiGuide.cs").Contains("reallife_ui_test_help"), "help: Options, Tests opens the help on the kit");
        Check(Array.Exists(UiScreens.All, d => d.Id == "game_help" && d.Section == UiScreenSwitches.Help), "help: the screen is registered with its own switch in the help section");
        string contract = BlueprintsRead(@"Core\GameContract.Help.cs");
        Check(contract.Contains("_generatedHelpCategories") && contract.Contains("_backHistory") && contract.Contains("_forwardHistory"), "help: the list and the two stacks of pages are lines of the contract");
        Check(build.Length == 0 || build.Contains("HelpSystem"), "help: build mode's help link is still the game's own");
    }
}
