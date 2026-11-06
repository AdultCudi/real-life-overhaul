using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Ui;

// The game's Options in the city on the kit (--options-ui): the rules the page stands on (the action groups, the sentences, the shared keys, the
// search, a slider's steps, the line at the head), the rows of Controls from the game's own bindings, the texts in three languages, and the
// screen's code read as text, since the game is not there to ask: each press reaches the game's own control, Esc and the close go out through the
// game's own CloseOptions, a rebind swallows the key that ends it, the locks of Going Public are read, another mod's tab opens the game's own
// view, both resets ask, and nothing here quits the game or loads a save. The screen's layouts run in UiChecks (UiChecks.Options.cs).
internal static partial class Program
{
    private static void OptionsUiChecks()
    {
        OptionsRulesChecks();
        OptionsPageChecks();
        OptionsTextChecks();
        OptionsCodeChecks();
        UiOnly = "options";   // the two screens' layouts alone (UiChecks.Options.cs)
        UiScreenChecks();
    }

    private static OptionsKey OKey(string action, string label, string path, bool pad = false, bool composite = false)
        => new OptionsKey { Action = action, Label = label, Binding = label, Path = path, Gamepad = pad, Composite = composite };

    private static void OptionsRulesChecks()
    {
        foreach (string action in OptionsRules.Described)
        {
            if (action == "tool")
            {
                continue;
            }
            Check(OptionsRules.GroupOf(action) != OptionsRules.Other, "options: " + action + " has a group of its own");
            Check(OptionsRules.DescriptionKey(action) == "reallife_ui_opt_act_" + action, "options: " + action + " has its sentence's key");
        }
        Check(OptionsRules.GroupOf("Move") == OptionsRules.Movement && OptionsRules.GroupOf("Pause") == OptionsRules.Time
              && OptionsRules.GroupOf("Interact") == OptionsRules.Interaction && OptionsRules.GroupOf("OpenMap") == OptionsRules.Apps
              && OptionsRules.GroupOf("Handbrake") == OptionsRules.Vehicle, "options: the game's action names go to the mockup's groups, by any case");
        Check(OptionsRules.GroupOf("OpenHandTool") == OptionsRules.Designer && OptionsRules.DescriptionKey("OpenHandTool") == "reallife_ui_opt_act_tool"
              && OptionsRules.GroupOf("OpenWallTool") == OptionsRules.Designer, "options: the designer's tools share a group and one sentence");
        Check(OptionsRules.GroupOf("BoardMeeting") == OptionsRules.Other && OptionsRules.DescriptionKey("BoardMeeting") == null
              && OptionsRules.GroupOf(null) == OptionsRules.Other && OptionsRules.DescriptionKey("") == null,
            "options: an action of another mod's goes in Other and has no sentence to make up");
        Check(OptionsRules.GroupKeys.Length == OptionsRules.GroupCount && OptionsRules.GroupIcons.Length == OptionsRules.GroupCount
              && OptionsRules.CategoryKeys.Length == OptionsRules.Categories && OptionsRules.GameTabs.Length == OptionsRules.Categories
              && OptionsRules.CategoryIcons.Length == OptionsRules.Categories, "options: every group and category has its words, glyph and the game's tab");
        Check(OptionsRules.GameTabs[OptionsRules.Game] == "Others", "options: the Game category is the game's Others tab");

        var keys = new[]
        {
            OKey("Move", "Move", null, false, true), OKey("Interact", "F", "<Keyboard>/f"), OKey("Interact", "Enter", "<Keyboard>/enter"),
            OKey("Cancel", "Esc", "<Keyboard>/escape"), OKey("Menu", "Esc", "<Keyboard>/escape"), OKey("Confirm", "Enter", "<Keyboard>/enter"),
            OKey("Interact", "A", "<Gamepad>/buttonSouth", true), OKey("Confirm", "A", "<Gamepad>/buttonSouth", true), OKey("OpenMap", "M", "<Keyboard>/m")
        };
        var clash = new int[keys.Length];
        OptionsRules.Shared(keys, keys.Length, clash);
        Check(clash[3] == 4 && clash[4] == 3, "options: Cancel and Menu on Escape name each other");
        Check(clash[1] == -1 && clash[2] == 5 && clash[5] == 2, "options: Confirm and Interact on Enter name each other, F is nobody's, and two bindings of one action do not clash");
        Check(clash[0] == -1 && clash[8] == -1, "options: a composite's head and a key nobody shares have no mark");
        Check(clash[6] == 7 && clash[7] == 6, "options: the gamepad's keys clash with the gamepad's alone, never with the keyboard's");
        Check(OptionsRules.TwoSchemes(keys, keys.Length) && !OptionsRules.TwoSchemes(keys, 6), "options: the two tabs show only when the game's rows carry both schemes");
        var order = new int[keys.Length];
        int n = OptionsRules.Arrange(keys, keys.Length, false, true, order);
        bool grouped = true, padOut = true;
        for (int i = 0; i < n; i++)
        {
            padOut &= !keys[order[i]].Gamepad;
            grouped &= i == 0 || OptionsRules.GroupOf(keys[order[i - 1]].Action) <= OptionsRules.GroupOf(keys[order[i]].Action);
        }
        Check(n == 7 && grouped && padOut, "options: the keyboard's rows in group order, the gamepad's left out");
        Check(OptionsRules.Arrange(keys, keys.Length, true, true, order) == 2, "options: the gamepad's tab holds its two rows");
        Check(OptionsRules.GamepadScheme("Gamepad") && OptionsRules.GamepadScheme("Keyboard&Mouse;Gamepad") && !OptionsRules.GamepadScheme("Keyboard&Mouse") && !OptionsRules.GamepadScheme(null),
            "options: a binding's scheme is read from the game's groups");

        Check(OptionsRules.Matches("map open", "Open Map", null) && OptionsRules.Matches("MAP", "Open Map", null) && !OptionsRules.Matches("zzz", "Open Map", "Open or close the city map")
              && OptionsRules.Matches("city", "Open Map", "Open or close the city map") && !OptionsRules.Matches("  ", "Open Map", null) && !OptionsRules.Matches(null, "Open Map", null),
            "options: a search needs every word, in the name or the sentence, whatever the case, and an empty one finds nothing");
        Check(OptionsRules.MarkAt("map", "Open Map", out int start, out int length) && start == 5 && length == 3 && !OptionsRules.MarkAt("zz", "Open Map", out _, out _),
            "options: the letters found are marked in the name");

        Check(Math.Abs(OptionsRules.ShareOf(0.5f, 0f, 1f) - 0.5f) < 0.0001f && OptionsRules.ShareOf(-5f, 0f, 1f) == 0f && OptionsRules.ShareOf(9f, 0f, 1f) == 1f && OptionsRules.ShareOf(1f, 1f, 1f) == 0f,
            "options: a slider's share is kept between 0 and 1, a flat one 0");
        Check(OptionsRules.ValueAt(0.5f, 1f, 5f, true) == 3f && OptionsRules.ValueAt(0.37f, 1f, 5f, true) == 2f && Math.Abs(OptionsRules.ValueAt(0.25f, -0.5f, 0.5f, false) + 0.25f) < 0.0001f,
            "options: a share gives a whole number on a whole-number slider and any value on another");
        Check(OptionsRules.Nudged(5f, 1, 1f, 5f, true) == 5f && OptionsRules.Nudged(1f, -1, 1f, 5f, true) == 1f && OptionsRules.Nudged(2f, 1, 1f, 5f, true) == 3f
              && Math.Abs(OptionsRules.Nudged(0.5f, 1, 0f, 1f, false) - 0.55f) < 0.0001f, "options: a step moves a whole slider by one, another by a twentieth, and stops at the ends");
        Check(OptionsRules.BuildLine(3682, "Game build {build}, {mods}", "4 mods") == "Game build 3682, 4 mods" && OptionsRules.BuildLine(0, "Game build {build}, {mods}", "4 mods") == "4 mods",
            "options: the head says the build and the mods, or the mods alone while the build is not known");
        Check(OptionsRules.Changed(new[] { 1f, 2f, 3f }, new[] { 1f, 5f, 9f }, new[] { false, false, true }, 3) == 1, "options: the custom difficulty counts what differs from what is applied, the locked values left out");
        Check(OptionsRules.Step(0f, 1f, false) == 0.05f && OptionsRules.Step(1f, 5f, true) == 1f, "options: the steps of a slider");
    }

    // The rows of Controls as the page builds them from the game's own bindings (the sample's), and what each carries.
    private static void OptionsPageChecks()
    {
        OptionsKey[] keys = UiSamples.OptionsKeys();
        var clash = new int[keys.Length];
        OptionsRules.Shared(keys, keys.Length, clash);
        var rows = new List<UiSettingRow>();
        var source = new List<int>();
        OptionsPages.KeyRows(keys, keys.Length, clash, false, true, (k, e) => e, rows, source);
        int headings = 0, keyRows = 0, described = 0, another = 0, marked = 0;
        var groups = new HashSet<string>();
        for (int i = 0; i < rows.Count; i++)
        {
            if (source[i] < 0)
            {
                headings++;
                groups.Add(rows[i].Name);
                continue;
            }
            keyRows++;
            described += !string.IsNullOrEmpty(rows[i].Tip) && rows[i].Tip != "Another key for the same action" ? 1 : 0;
            another += rows[i].Tip == "Another key for the same action" ? 1 : 0;
            marked += !string.IsNullOrEmpty(rows[i].Mark) ? 1 : 0;
            Check(!string.IsNullOrEmpty(rows[i].Name) && rows[i].Control == UiSettingControl.Key && !keys[source[i]].Gamepad, "options: a key row of the keyboard's page has a name, a key control and no pad binding (" + rows[i].Name + ")");
        }
        Check(headings == groups.Count && headings >= 5, "options: each group is one heading, in the page's order (" + headings + ")");
        Check(keyRows == 22 && another == 2, "options: the keyboard's page holds each binding of its actions, the second of one marked as another key (" + keyRows + ", " + another + ")");
        Check(marked == 2, "options: the two actions on Escape each carry the mark (" + marked + ")");
        Check(described == keyRows - another - 1, "options: every action of the game has its sentence and only the other mod's has none (" + described + ")");
        var padRows = new List<UiSettingRow>();
        OptionsPages.KeyRows(keys, keys.Length, clash, true, true, (k, e) => e, padRows, new List<int>());
        Check(padRows.Count > 0 && padRows.Count < rows.Count, "options: the gamepad's tab is a page of its own, not the keyboard's again");
    }

    private static string LocalePath(string code) => System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Locales", code + ".json"));

    private static void OptionsTextChecks()
    {
        var wanted = new List<string>();
        string[] screens = { "options_city", "options_difficulty" };
        foreach (string id in screens)
        {
            UiScreenDef def = UiScreens.Find(id);
            Check(def != null && def.Section == UiScreenSwitches.Pause && def.Runner == "OptionsScreen", "options: " + id + " is registered with its switch in the pause section");
            wanted.Add("reallife_ui_screen_" + id);
            wanted.Add("reallife_opt_tip_ui_screen_" + id);
            if (def != null)
            {
                foreach (string key in def.Keys)
                {
                    if (key.StartsWith("reallife_", StringComparison.Ordinal))
                    {
                        wanted.Add(key);
                    }
                }
            }
        }
        foreach (string action in OptionsRules.Described)
        {
            wanted.Add("reallife_ui_opt_act_" + action);
        }
        wanted.AddRange(new[] { "reallife_ui_test_options", "reallife_ui_test_options_switch", "reallife_ui_test_options_none", "reallife_ui_test_options_refused", "reallife_ui_test_options_kit",
            "reallife_ui_test_options_game", "reallife_ui_test_options_kit_on", "reallife_ui_test_options_game_on" });
        var seen = new HashSet<string>();
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            var texts = new Dictionary<string, string>(StringComparer.Ordinal);
            string path = LocalePath(code);
            Check(File.Exists(path), "options: the " + code + " locale is there");
            if (!File.Exists(path))
            {
                continue;
            }
            using (var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)))
            {
                foreach (System.Text.Json.JsonProperty p in doc.RootElement.EnumerateObject())
                {
                    if (p.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        texts[p.Name] = p.Value.GetString();
                    }
                }
            }
            Dictionary<string, string> en = code == "en" ? texts : EnglishOf();
            foreach (string key in wanted)
            {
                if (!seen.Add(code + key))
                {
                    continue;
                }
                bool there = texts.TryGetValue(key, out string text) && !string.IsNullOrWhiteSpace(text);
                Check(there, "options: " + key + " is written in " + code);
                if (!there)
                {
                    continue;
                }
                Check(text.IndexOf('—') < 0 && text.IndexOf('–') < 0 && text.IndexOf('!') < 0 && text.IndexOf('！') < 0, "options: " + key + " has no dash or exclamation mark in " + code);
                if (en.TryGetValue(key, out string english))
                {
                    foreach (Match m in Regex.Matches(english, "\\{[a-z]+\\}"))
                    {
                        Check(text.Contains(m.Value), "options: " + key + " keeps " + m.Value + " in " + code);
                    }
                }
                if (key.StartsWith("reallife_ui_test_options_", StringComparison.Ordinal) || key.StartsWith("reallife_ui_opt_reset_category_body", StringComparison.Ordinal))
                {
                    Check(text.Length <= 240, "options: " + key + " is a notice of at most 240 characters in " + code);
                }
            }
        }
    }

    private static Dictionary<string, string> _optionsEnglish;

    private static Dictionary<string, string> EnglishOf()
    {
        if (_optionsEnglish == null)
        {
            _optionsEnglish = new Dictionary<string, string>(StringComparer.Ordinal);
            using (var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(LocalePath("en"))))
            {
                foreach (System.Text.Json.JsonProperty p in doc.RootElement.EnumerateObject())
                {
                    if (p.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        _optionsEnglish[p.Name] = p.Value.GetString();
                    }
                }
            }
        }
        return _optionsEnglish;
    }

    private static void OptionsCodeChecks()
    {
        string game = ModSource(@"Ui\Options\OptionsGame.cs"), kits = ModSource(@"Ui\Options\OptionsKits.cs"), screen = ModSource(@"Ui\Options\OptionsScreen.cs");
        string pages = ModSource(@"Ui\Options\OptionsScreen.Pages.cs"), input = ModSource(@"Ui\Options\OptionsScreen.Input.cs"), controls = ModSource(@"Ui\Options\OptionsScreen.Controls.cs");
        string diff = ModSource(@"Ui\Options\OptionsScreen.Difficulty.cs"), mods = ModSource(@"Ui\Options\OptionsScreen.Mods.cs");
        string host = ModSource(@"Ui\Host\UiGameHost.cs"), director = ModSource(@"Ui\UiDirector.cs"), all = screen + pages + input + controls + diff + mods + game + kits;

        // Where it stands: the city's window alone, over the game's own object, never the main menu's.
        Check(game.Contains("return uis != null ? uis.options : null;") && !game.Contains("MainMenuController") && game.Contains("UiGameKind.Overlay") && game.Contains("public bool Blocks => true;"),
            "options: the host binds the city's own Options object and holds the world under it, and the main menu's is never looked for");
        Check(game.Contains("conceal.Add(root.GetChild(i));") && game.Contains("conceal.Add(root.GetComponent<Graphic>());") && game.Contains("internal void Hide(Transform piece)"),
            "options: its own children are hidden under the kit by the host's conceal, the late ones another mod adds too, nothing switched off");
        Check(director.Contains("OptionsKits.Install();") && director.Contains("OptionsKits.Remove(why);") && director.Contains("OptionsKits.Rearm();") && director.Contains("OptionsKits.States()"),
            "options: the director puts the host on, takes it off, looks again each city and says its state");
        Check(kits.Contains("UiScreenSwitches.On(CityId)") && kits.Contains("City.Remove(\"its switch is off\")") && screen.Contains("UiScreenSwitches.On(OptionsKits.DifficultyId)")
              && screen.Contains("OptionsKits.Classic();"), "options: each screen has its switch, and the difficulty panel with its switch off is the game's own view until Options closes");
        Check(host.Contains("bool options = global::Scenes.MainMenu.Options.IsVisible && _screen.Id != OptionsKits.CityId;") && host.Contains("KitHasOverlayKeys(pause || options"),
            "options: Options being open holds the other overlays' keys but never its own screen's");

        // The press reaches the game's own control.
        Check(input.Contains("t.isOn = on;") && input.Contains("s.D.SelectOption(option);") && input.Contains("UiWidgets.Slide(s.S, value)") && input.Contains("UiWidgets.Hand(b);")
              && Occurs(input, "HotSpots.Game()") >= 5, "options: a switch, a drop-down, a slider and a button are set through the game's own control, as its own work");
        Check(!Regex.IsMatch(all, "Application\\.Quit|QuitToDesktop|LoadTransitionToSave|SaveGameManager\\.Load|\\.SaveGame\\(|OpenMainMenu"), "options: nothing on these screens quits the game, loads a save or leaves for the main menu");

        // Esc and the close.
        Check(input.Contains("menu.CloseOptions();") && MemberText(input, "public bool Back()").Contains("if (Busy)") && MemberText(input, "public bool Back()").Contains("return Close();")
              && MemberText(input, "public bool Back()").IndexOf("_choice >= 0", StringComparison.Ordinal) < MemberText(input, "public bool Back()").IndexOf("return Close();", StringComparison.Ordinal),
            "options: Back steps out of a list, a held slider, a search and a key being awaited first, then the game's own CloseOptions closes the window (its custom panel asks first)");
        Check(MemberText(input, "private bool Close()").Contains("Scenes.MainMenu.Options.IsVisible") && input.Contains("if (_modsOpen)") && mods.Contains("_settings.Back()"),
            "options: Back goes out of the mod's own list to this window before it closes it, and closes only a window that is open");

        // Rebinding: the game's own, with its clash and its Escape.
        Check(controls.Contains("GameContract.RebindStart.Method.Invoke(_binds[s.Index].Row, null);") && controls.Contains("private bool Busy => _listen >= 0 || Time.frameCount - _endFrame <= 1;")
              && MemberText(input, "private bool RowPress(int arg)").Contains("if (Busy)") && MemberText(input, "public bool Act(UiAction action)").Contains("if (Busy)")
              && controls.Contains("GameContract.RebindOngoing.Property.GetValue(_binds[_listen].Row, null) != null") && controls.Contains("GameContract.RebindCollision.Property.GetValue"),
            "options: a key is rebound by the game's own rebinding, its Escape and its taken-key answer are read, and the key that ends it presses nothing of the kit's");
        Check(controls.Contains("OptionsRules.Shared(_keys, _keyCount, _clash);") && controls.Contains("OptionsPages.KeyRows(") && controls.Contains("OptionsRules.TwoSchemes(_keys, _keyCount)")
              && pages.Contains("Kind.Scheme") && input.Contains("_scheme = option == 1 ? 1 : 0;"), "options: Controls marks shared keys, groups the rows and has its scheme choice only when the game has two");

        // Every row that has an action reaches it.
        var acts = new List<string>();
        foreach (Match m in Regex.Matches(MemberText(pages, "private enum Do : byte"), "\\b([A-Z][A-Za-z]+),?\\s*(?://.*)?$", RegexOptions.Multiline))
        {
            if (m.Groups[1].Value != "None")
            {
                acts.Add(m.Groups[1].Value);
            }
        }
        string run = MemberText(input, "private bool Run(Slot s)") + MemberText(diff, "private bool RunDifficulty(Do act)");
        foreach (string act in acts)
        {
            Check(run.Contains("Do." + act) || act == "Unstuck", "options: the action " + act + " of a row is run by a handler");
        }
        Check(acts.Count >= 10, "options: the actions of the rows are listed (" + acts.Count + ")");
        Check(pages.Contains("Do.Unstuck, Field<Button>(GameContract.OptUnstuck)") && input.Contains("return Hit(s.B, null);"), "options: Unstuck presses the game's own button");
        Check(input.Contains("_o.ResetWindowsPosition()") && input.Contains("_o.OpenRadioFolder()") && input.Contains("_o.RefreshSongs()") && input.Contains("_o.OpenDifficultyCustomizeUI()")
              && input.Contains("_o.RequestDataDeletion()"), "options: each button without a handle of its own falls back on the public method the game's button calls");

        // The resets ask, through the game.
        Check(MemberText(input, "private bool AskReset()").Contains("GameConfirm.Show(") && MemberText(input, "private void ResetNow()").Contains("\"ResetSelectedCategoryToDefaults\"")
              && MemberText(input, "private bool AskResetAll()").Contains("\"ResetAllCategoriesToDefaultsWithConfirm\"") && screen.Contains("reallife_ui_opt_reset_all") && screen.Contains("reallife_ui_opt_reset_category"),
            "options: Reset this category asks through the game's question and Reset all is the game's own question");

        // Going Public's locks, mirrored.
        Check(screen.Contains("private static bool Held(UI.Elements.Dropdown d)") && screen.Contains("g != null && !g.interactable") && pages.Contains("r.Greyed = Held(d);")
              && pages.Contains("r.Greyed = !sl.interactable;") && pages.Contains("(!s.B.interactable || !Free(s.B))") && pages.Contains("r.Greyed = s.T == null || !s.T.interactable;")
              && pages.Contains("T(\"reallife_ui_opt_locked\")"), "options: a drop-down, a slider, a switch or a button the game holds (Going Public's locks on the difficulty and the game speed) reads greyed and takes no press");
        Check(input.Contains("if (s.Row.Greyed)") && input.Contains("_slots[arg].Row.Greyed"), "options: a greyed row takes no press and no drag");

        // Another mod's pieces.
        Check(screen.Contains("UiHostRules.Own(c.name, OptionsRules.GameTabs)") && input.Contains("private bool OpenForeign(int tab)") && MemberText(input, "private bool OpenForeign(int tab)").Contains("OptionsKits.Classic();")
              && MemberText(input, "private bool OpenForeign(int tab)").Contains("UiWidgets.Hand(b);"), "options: another mod's tab is pressed in the game's own menu and the game's own Options view shows until it closes");
        Check(mods.Contains("OptionsService.RegisteredEntries") && mods.Contains("entry.Key == own") && mods.Contains("GameContract.ModsToggleLabel") && mods.Contains("Do.OpenClassic"),
            "options: the Mods page lists the other mods' registered options on the game's own controls for them, and a row only the game's list can draw opens that list");
        Check(mods.Contains("SettingsScreen.Make()") && mods.Contains("_settings.ShowGroup(group);") && input.Contains("_settings.Press(command, arg)"),
            "options: the mod's own groups open Mod settings' list inside this window");

        // The custom difficulty.
        Check(diff.Contains("_o.ConfirmDifficultyCustomizeUI();") && diff.Contains("GameContract.DiffFromCurrent") && diff.Contains("UiWidgets.Press(confirm)") && diff.Contains("GameContract.DiffSave")
              && diff.Contains("GetInvalidFileNameChars"), "options: the custom difficulty is applied by the game's own confirm, discarded by its own call, and a preset saved through its naming box");
        Check(diff.Contains("Locked = true") && diff.Contains("Def(GameContract.DiffDays") && diff.Contains("Def(GameContract.DiffMoney") && diff.Contains("Def(GameContract.DiffAge"),
            "options: the three values the game fixes when it begins are shown locked");
        Check(Occurs(diff, "Def(GameContract.Diff") == 22, "options: all twenty two options of the custom difficulty are on its pages (" + Occurs(diff, "Def(GameContract.Diff") + ")");

        // Commands: this job's block, nobody else's.
        Check(screen.Contains("public const int NameCommand = 9501;"), "options: its own command is in the block 9500 to 9519");
        var taken = new List<string>();
        foreach (string file in Directory.GetFiles(System.IO.Path.GetDirectoryName(ModSourcePath(@"Ui\UiDraw.cs")), "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            if (!file.Contains(@"\Options\") && Regex.IsMatch(text, "const int \\w*Command\\w* = 95(0[0-9]|1[0-9])\\b"))
            {
                taken.Add(System.IO.Path.GetFileName(file));
            }
        }
        Check(taken.Count == 0, "options: nobody else uses the commands 9500 to 9519 (" + string.Join(", ", taken) + ")");

        // A look at the game's controls a few times a second, nothing a frame but while a key is awaited.
        Check(screen.Contains("private const float Every = 0.25f;") && MemberText(screen, "public bool Dirty").Contains("if (_listen >= 0)") && MemberText(screen, "public bool Dirty").Contains("Time.unscaledTime >= _next"),
            "options: the game's controls are looked at four times a second, and each frame only while a key is awaited");
    }
}
