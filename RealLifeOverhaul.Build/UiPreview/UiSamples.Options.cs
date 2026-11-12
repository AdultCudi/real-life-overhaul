using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

/// <summary>
/// The game's Options in the city as a player meets it in Spring of the second year with four mods loaded, one of them another mod's tab:
/// every category, the Controls page with two keys on one action and a key shared by two, a key being rebound, a search, the Game page with
/// the difficulty and the game speed held by a shared session, the Mods page with the mod's groups and another mod's options, a drop-down
/// open, a search that finds nothing, and the custom difficulty panel on its three pages.
/// </summary>
internal static partial class UiSamples
{
    public static readonly string[] OptionsStates =
    {
        "graphics", "audio", "controls", "listening", "search", "game", "locked", "mods", "choice", "empty",
        "difficulty_presets", "difficulty", "difficulty_economy"
    };

    private static OptionsKey OptKey(string action, string label, string binding, string path, bool pad = false, bool composite = false)
        => new OptionsKey { Action = action, Label = label, Binding = binding, Path = path, Gamepad = pad, Composite = composite };

    /// <summary>The rebinding rows of the sample: the game's own actions, a key shared by two of them and an action of another mod's.</summary>
    public static OptionsKey[] OptionsKeys() => new[]
    {
        OptKey("Move", "Move", "W A S D", null, false, true),
        OptKey("Move", "Move", "Arrow keys", null, false, true),
        OptKey("RotateLeft", "Rotate Left", "Q", "<Keyboard>/q"),
        OptKey("RotateRight", "Rotate Right", "E", "<Keyboard>/e"),
        OptKey("Zoom", "Zoom", "Scroll", "<Mouse>/scroll"),
        OptKey("ToggleRunning", "Toggle Running", "Shift", "<Keyboard>/shift"),
        OptKey("AutoRun", "Toggle Auto Run", "R", "<Keyboard>/r"),
        OptKey("Pause", "Pause", "Space", "<Keyboard>/space"),
        OptKey("Sleep", "Sleep", "Z", "<Keyboard>/z"),
        OptKey("Interact", "Interact", "F", "<Keyboard>/f"),
        OptKey("Interact", "Interact", "Keypad Enter", "<Keyboard>/numpadEnter"),
        OptKey("SecondaryInteract", "Secondary Interact", "P", "<Keyboard>/p"),
        OptKey("Cancel", "Cancel", "Esc", "<Keyboard>/escape"),
        OptKey("Menu", "Menu", "Esc", "<Keyboard>/escape"),
        OptKey("Confirm", "Confirm", "Enter", "<Keyboard>/enter"),
        OptKey("OpenMap", "Open Map", "M", "<Keyboard>/m"),
        OptKey("OpenBizman", "Open bizman", "B", "<Keyboard>/b"),
        OptKey("OpenHelp", "Open Help", "F1", "<Keyboard>/f1"),
        OptKey("QuickSave", "Quick Save", "F5", "<Keyboard>/f5"),
        OptKey("BoardMeeting", "Board meeting", "F9", "<Keyboard>/f9"),
        OptKey("Steering", "Steering", "A D", null, false, true),
        OptKey("Handbrake", "Handbrake", "X", "<Keyboard>/x"),
        OptKey("Interact", "Interact", "A", "<Gamepad>/buttonSouth", true),
        OptKey("Cancel", "Cancel", "B", "<Gamepad>/buttonEast", true)
    };

    private static UiSettingRow OptRow(UiSettingControl control, string name, string value, string tip = null, bool on = false, float share = 0f, bool grey = false)
        => new UiSettingRow { Control = control, Name = name, Value = value, Tip = tip, On = on, Share = share, Greyed = grey };

    private static UiSettingRow OptHead(string name, UiIcon icon) => new UiSettingRow { Control = UiSettingControl.None, Name = name, Icon = icon };

    /// <summary>The window of Options in a state, laid out as the screen lays it.</summary>
    public static void OptionsKit(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, string state) => UiOptions.Lay(s, f, Options(t, state), dark, k, t);

    /// <summary>The window of Options in a state: a category's page, or the custom difficulty.</summary>
    public static UiOptionsModel Options(UiWords t, string state)
    {
        bool diff = state.StartsWith("difficulty", StringComparison.Ordinal);
        var m = new UiOptionsModel { Title = t(diff ? "reallife_ui_opt_diff_title" : "reallife_ui_opt_title", diff ? "Custom difficulty" : "Options"), Icon = diff ? UiIcon.Sliders : UiIcon.Gear };
        var rows = new List<UiSettingRow>(64);
        UiSettingsModel l = m.List;
        l.SearchPlaceholder = t("reallife_ui_opt_search", "Search the options");
        if (!diff)
        {
            m.Build = t("reallife_ui_opt_build", "Game build {build}, {mods}").Replace("{build}", "3682").Replace("{mods}", t("reallife_ui_opt_mods", "{count} mods").Replace("{count}", "4"));
            m.Groups = new[] { t("menu_options_graphics", "Graphics"), t("menu_options_audio", "Audio"), t("menu_options_controls", "Controls"), t("reallife_ui_opt_cat_game", "Game"),
                t("menu_options_mods", "Mods"), "Seasons" };
            m.Icons = new[] { UiIcon.Monitor, UiIcon.SpeakerHigh, UiIcon.Keyboard, UiIcon.Sliders, UiIcon.Cube, UiIcon.Code };
            l.Preset = t("reallife_ui_opt_apply_note", "Changes apply as you make them");
            l.PresetActions = new[]
            {
                new UiActionModel { Label = t("reallife_ui_opt_reset_category", "Reset this category"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Reset, Key = UiAction.Option },
                new UiActionModel { Label = t("reallife_ui_opt_reset_all", "Reset all options"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Trash, Key = UiAction.Count }
            };
        }
        switch (state)
        {
            case "graphics":
                m.Chosen = 0;
                OptionsGraphics(t, rows);
                break;
            case "audio":
                m.Chosen = 1;
                OptionsAudio(t, rows);
                break;
            case "controls":
            case "listening":
            case "search":
            case "empty":
                m.Chosen = state == "search" || state == "empty" ? -1 : 2;
                OptionsControls(t, rows, state);
                if (state == "search")
                {
                    l.Query = "key";
                    l.Found = rows.Count.ToString();
                    m.Found = new[] { 0, 0, rows.Count, 0, 0, 0 };
                    l.PresetActions = new UiActionModel[0];
                }
                else if (state == "empty")
                {
                    rows.Clear();
                    l.Query = "zzqq";
                    l.EmptyTitle = t("reallife_ui_opt_found_none", "Nothing found");
                    l.EmptyBody = t("reallife_ui_opt_found_none_body", "No option has that word in its name or its sentence. Try a shorter one.");
                    m.Found = new[] { 0, 0, 0, 0, 0, 0 };
                    l.PresetActions = new UiActionModel[0];
                }
                break;
            case "game":
            case "locked":
            case "choice":
                m.Chosen = 3;
                OptionsGame(t, rows, state == "locked");
                if (state == "choice")
                {
                    m.Choices = new[] { "English", "Português (Brasil)", "Deutsch", "Français", "Español", "Nederlands", "Polski", "Русский", "Türkçe", "한국어", "简体中文" };
                    m.ChoiceCount = m.Choices.Length;
                    m.ChoiceValue = 0;
                    m.ChoiceRow = 1;
                }
                break;
            case "mods":
                m.Chosen = 4;
                OptionsMods(t, rows);
                break;
            default:
                OptionsDifficulty(t, m, rows, state);
                break;
        }
        l.Rows = rows.ToArray();
        l.RowCount = rows.Count;
        return m;
    }

    private static void OptionsGraphics(UiWords t, List<UiSettingRow> rows)
    {
        rows.Add(OptHead(t("reallife_ui_opt_sec_display", "Display"), UiIcon.Monitor));
        rows.Add(OptRow(UiSettingControl.Switch, t("menu_options_graphics_fullscreen", "Full screen"), t("reallife_meta_on", "On"), null, true));
        rows.Add(OptRow(UiSettingControl.Choice, t("menu_options_graphics_resolution", "Resolution"), "2560x1440"));
        rows.Add(OptRow(UiSettingControl.Choice, t("menu_options_graphics_fps_limit", "FPS Limit"), "Vsync (max 144FPS)"));
        rows.Add(OptRow(UiSettingControl.Slider, t("menu_options_ui_zooming", "UI Zooming"), "100%", null, false, 0.33f));
        rows.Add(OptRow(UiSettingControl.Slider, t("menu_options_brightness", "Brightness"), "50%", null, false, 0.5f));
        rows.Add(OptRow(UiSettingControl.Switch, t("menu_options_graphics_lowdetailcitymap", "Low detailed city map"), t("reallife_meta_off", "Off")));
        rows.Add(OptRow(UiSettingControl.Switch, t("menu_options_graphics_showfps", "Show FPS"), t("reallife_meta_off", "Off")));
        rows.Add(OptRow(UiSettingControl.Action, t("menu_options_reset_windows", "Reset windows positions"), t("menu_options_reset_windows_button", "Reset windows"),
            t("menu_options_reset_windows_tip", "Sets the movable windows back to the default positions")));
        rows.Add(OptHead(t("reallife_ui_opt_sec_quality", "Quality"), UiIcon.Sliders));
        rows.Add(OptRow(UiSettingControl.Choice, t("menu_options_graphics_gfx_quality", "GFX Quality"), "High"));
        rows.Add(OptRow(UiSettingControl.Choice, t("menu_options_graphics_particle_quality", "Particle Quality"), "High"));
        rows.Add(OptRow(UiSettingControl.Choice, t("menu_options_graphics_texture_quality", "Texture Quality"), "High"));
        rows.Add(OptRow(UiSettingControl.Choice, t("menu_options_graphics_aa", "Anti-Aliasing"), "TAA Medium"));
        rows.Add(OptRow(UiSettingControl.Choice, t("menu_options_graphics_shadows", "Shadows"), "All"));
        rows.Add(OptRow(UiSettingControl.Choice, t("menu_options_graphics_hbao", "Ambient Occlusion"), "Medium"));
    }

    private static void OptionsAudio(UiWords t, List<UiSettingRow> rows)
    {
        rows.Add(OptHead(t("reallife_ui_opt_sec_volume", "Volume"), UiIcon.SpeakerHigh));
        rows.Add(OptRow(UiSettingControl.Slider, t("menu_options_audio_global_volume", "Global volume"), "80%", null, false, 0.8f));
        rows.Add(OptRow(UiSettingControl.Slider, t("menu_options_audio_menumusic_volume", "Menu music volume"), "60%", null, false, 0.6f));
        rows.Add(OptRow(UiSettingControl.Slider, t("menu_options_audio_radio_volume", "Radio volume"), "70%", null, false, 0.7f));
        rows.Add(OptRow(UiSettingControl.Slider, t("menu_options_audio_sfx_volume", "SFX Volume"), "100%", null, false, 1f));
        rows.Add(OptRow(UiSettingControl.Slider, t("menu_options_audio_ai_music_volume", "Competitor Store Music Volume"), "40%", null, false, 0.4f));
        rows.Add(OptHead(t("reallife_ui_opt_sec_radio", "Radio"), UiIcon.MusicNotes));
        rows.Add(OptRow(UiSettingControl.Action, t("menu_options_local_radio_songs", "Local Radio Songs"), t("menu_options_open_folder", "Open Folder"), "Supported files found: 12"));
        rows.Add(OptRow(UiSettingControl.Action, t("reallife_ui_opt_songs_again", "Look for new songs"), t("common_refresh", "Refresh")));
    }

    private static void OptionsControls(UiWords t, List<UiSettingRow> rows, string state)
    {
        rows.Add(OptHead(t("reallife_ui_opt_sec_play", "Play"), UiIcon.GameController));
        rows.Add(OptRow(UiSettingControl.Switch, t("menu_options_controls_vehicle_mouse_input", "Enable Mouse-Controlled Driving"), t("reallife_meta_off", "Off")));
        rows.Add(OptRow(UiSettingControl.Switch, t("menu_options_controls_invert_rotation", "Invert Rotation"), t("reallife_meta_off", "Off")));
        rows.Add(OptRow(UiSettingControl.Switch, t("menu_options_controls_run_by_default_indoor", "Run by default indoor"), t("reallife_meta_on", "On"), null, true));
        rows.Add(OptRow(UiSettingControl.Switch, t("menu_options_controls_steering_assist", "Enable Steering Assist"), t("reallife_meta_on", "On"), null, true));
        rows.Add(OptRow(UiSettingControl.Choice, t("reallife_ui_opt_scheme", "Controls for"), t("menu_options_controls_keyboard", "Keyboard")));
        OptionsKey[] keys = OptionsKeys();
        var clash = new int[keys.Length];
        OptionsRules.Shared(keys, keys.Length, clash);
        if (state == "listening")
        {
            keys[9].Listening = true;
            keys[9].Binding = t("reallife_set_press", "Press a key");
        }
        var keyRows = new List<UiSettingRow>();
        OptionsPages.KeyRows(keys, keys.Length, clash, false, true, t, keyRows, new List<int>());
        if (state == "search")
        {
            rows.Clear();
            foreach (UiSettingRow r in keyRows)
            {
                if (r.Control == UiSettingControl.Key && OptionsRules.Matches("key", r.Name, r.Tip))
                {
                    rows.Add(r);
                }
            }
            if (rows.Count == 0)
            {
                rows.AddRange(keyRows.GetRange(1, Math.Min(6, keyRows.Count - 1)));
            }
            return;
        }
        rows.AddRange(keyRows);
    }

    private static void OptionsGame(UiWords t, List<UiSettingRow> rows, bool locked)
    {
        rows.Add(OptHead(t("reallife_ui_opt_sec_formats", "Language and formats"), UiIcon.Globe));
        rows.Add(OptRow(UiSettingControl.Choice, t("menu_options_others_language", "Language"), "English"));
        rows.Add(OptRow(UiSettingControl.Choice, t("menu_options_others_number_formatting", "Number format"), "1,234.56"));
        rows.Add(OptRow(UiSettingControl.Switch, t("menu_options_others_time_format", "Use 12h Time Format"), t("reallife_meta_off", "Off")));
        rows.Add(OptRow(UiSettingControl.Switch, t("menu_options_others_unit_system", "Use Imperial System"), t("reallife_meta_off", "Off")));
        rows.Add(OptRow(UiSettingControl.Switch, t("menu_options_others_seasonal_decorations", "Seasonal Decorations"), t("reallife_meta_on", "On"), null, true));
        rows.Add(OptRow(UiSettingControl.Switch, t("ba:menu_options_others_control_hints", "Control hints"), t("reallife_meta_on", "On"), null, true));
        rows.Add(OptHead(t("reallife_ui_opt_sec_difficulty", "Difficulty"), UiIcon.Trophy));
        UiSettingRow diff = OptRow(UiSettingControl.Action, t("ba:menu_options_others_difficulty", "Difficulty"), t("ba:difficulty_customize", "Customize..."), t("reallife_ui_opt_diff_tip", "Opens the custom difficulty"));
        diff.Greyed = locked;
        if (locked)
        {
            diff.Tip = t("reallife_ui_opt_locked", "Not available now");
        }
        rows.Add(diff);
        rows.Add(OptHead(t("reallife_ui_opt_sec_saving", "Saving and speed"), UiIcon.FloppyDisk));
        rows.Add(OptRow(UiSettingControl.Slider, t("menu_options_time_between_auto_saves", "Time between auto saves"), "10 mins", null, false, 0.15f));
        rows.Add(OptRow(UiSettingControl.Slider, t("menu_options_max_auto_saves_per_game", "Max auto saves per game"), "3 saves", null, false, 0.22f));
        UiSettingRow speed = OptRow(UiSettingControl.Slider, t("menu_options_game_speed", "Game Speed"), "100%", null, false, 0.5f, locked);
        if (locked)
        {
            speed.Tip = t("reallife_ui_opt_locked", "Not available now");
        }
        rows.Add(speed);
        rows.Add(OptHead(t("reallife_ui_opt_sec_help", "Help"), UiIcon.Wrench));
        rows.Add(OptRow(UiSettingControl.Action, t("options_unstuck_title", "Unstuck character"), t("options_unstuck_button", "Unstuck"),
            t("options_unstuck_description", "Moves the character to the exit of the current or closest building")));
        rows.Add(OptHead(t("reallife_ui_opt_sec_privacy", "Privacy"), UiIcon.ShieldCheck));
        rows.Add(OptRow(UiSettingControl.Switch, t("menu_options_others_allow_tracking", "Allow anonymous gameplay tracking"), t("reallife_meta_on", "On"), null, true));
        rows.Add(OptRow(UiSettingControl.Action, t("menu_options_others_request_data_deletion", "Request Data Deletion"), t("menu_options_others_request_data_deletion", "Request Data Deletion")));
    }

    private static void OptionsMods(UiWords t, List<UiSettingRow> rows)
    {
        string[] groups = { "Start here", "Your character and home", "The city", "Getting around", "Work", "Money", "Your businesses", "Controls", "Performance", "Troubleshooting" };
        int[] counts = { 8, 48, 63, 31, 44, 40, 52, 18, 22, 24 };
        UiIcon[] icons = { UiIcon.Info, UiIcon.Person, UiIcon.Building, UiIcon.Car, UiIcon.Briefcase, UiIcon.Coin, UiIcon.Store, UiIcon.Key, UiIcon.Cpu, UiIcon.Wrench };
        rows.Add(OptHead(t("reallife_ui_opt_mods_rlo", "Real Life Overhaul, {count} groups").Replace("{count}", groups.Length.ToString()), UiIcon.Cube));
        for (int i = 0; i < groups.Length; i++)
        {
            UiSettingRow r = OptRow(UiSettingControl.Action, groups[i], t("reallife_ui_opt_open", "Open"), t("reallife_ui_opt_mods_options", "{count} options").Replace("{count}", counts[i].ToString()));
            r.Icon = icons[i];
            rows.Add(r);
        }
        rows.Add(OptHead("Seasons", UiIcon.Code));
        rows.Add(OptRow(UiSettingControl.Switch, "Snow in winter", t("reallife_meta_on", "On"), null, true));
        rows.Add(OptRow(UiSettingControl.Slider, "Season length in days", "30", null, false, 0.3f));
        rows.Add(OptRow(UiSettingControl.Choice, "Weather mood", "Mild"));
        rows.Add(OptRow(UiSettingControl.Action, "Open the weather log", t("reallife_ui_opt_open_classic", "Open in the game's list")));
    }

    private static void OptionsDifficulty(UiWords t, UiOptionsModel m, List<UiSettingRow> rows, string state)
    {
        m.Groups = new[] { t("reallife_ui_opt_diff_presets", "Presets"), t("reallife_ui_opt_diff_general", "General"), t("reallife_ui_opt_diff_character", "Character"), t("reallife_ui_opt_diff_economy", "Economy") };
        m.Icons = new[] { UiIcon.Bookmark, UiIcon.Gear, UiIcon.Person, UiIcon.Coin };
        UiSettingsModel l = m.List;
        l.NoHead = true;
        l.Preset = t("reallife_ui_opt_diff_note", "Changes apply when you press Apply changes");
        l.Differs = t("reallife_ui_opt_diff_changed", "{count} not applied yet").Replace("{count}", "4");
        l.PresetActions = new[]
        {
            new UiActionModel { Label = t("common_discard_changes", "Discard Changes"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Reset, Key = UiAction.Option },
            new UiActionModel { Label = t("common_apply_changes", "Apply Changes"), Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt }
        };
        UiSettingRow Changed(UiSettingRow r) { r.Differs = true; return r; }
        UiSettingRow Locked(string name, string value) => OptRow(UiSettingControl.Action, name, value, t("reallife_ui_opt_diff_locked", "Set when the game began"), false, 0f, true);
        if (state == "difficulty_presets")
        {
            m.Chosen = 0;
            rows.Add(OptHead(t("reallife_ui_opt_diff_presets", "Presets"), UiIcon.Bookmark));
            rows.Add(OptRow(UiSettingControl.Choice, t("reallife_ui_opt_diff_preset", "Difficulty preset"), t("reallife_ui_opt_diff_unsaved", "Unsaved settings")));
            UiSettingRow name = OptRow(UiSettingControl.Text, t("reallife_ui_opt_diff_name", "Save your own as"), string.Empty, t("reallife_ui_opt_diff_name_hint", "Name your preset"));
            name.Command = 9501;
            rows.Add(name);
            rows.Add(OptRow(UiSettingControl.Action, t("custom_game_presets_save_preset", "Save preset"), t("custom_game_presets_save_preset", "Save preset")));
            rows.Add(OptRow(UiSettingControl.Action, t("custom_game_presets_tooltip_copy", "Copy to clipboard"), t("custom_game_presets_tooltip_copy", "Copy to clipboard")));
            rows.Add(OptRow(UiSettingControl.Action, t("custom_game_presets_tooltip_paste", "Paste from clipboard"), t("custom_game_presets_tooltip_paste", "Paste from clipboard")));
            rows.Add(OptRow(UiSettingControl.Action, t("custom_game_presets_tooltip_delete", "Delete preset"), t("custom_game_presets_tooltip_delete", "Delete preset")));
        }
        else if (state == "difficulty")
        {
            m.Chosen = 2;
            rows.Add(OptHead(t("reallife_ui_opt_diff_character", "Character"), UiIcon.Person));
            rows.Add(Locked("Starting age", "18"));
            rows.Add(OptRow(UiSettingControl.Switch, "Disable aging", t("reallife_meta_off", "Off")));
            rows.Add(Changed(OptRow(UiSettingControl.Switch, "Disable happiness", t("reallife_meta_on", "On"), null, true)));
            rows.Add(Changed(OptRow(UiSettingControl.Switch, "Disable energy", t("reallife_meta_on", "On"), null, true)));
            rows.Add(OptRow(UiSettingControl.Switch, "All courses unlocked", t("reallife_meta_off", "Off"), "All business school courses automatically completed"));
            rows.Add(OptRow(UiSettingControl.Switch, "Unlock all contacts", t("reallife_meta_off", "Off")));
            rows.Add(OptRow(UiSettingControl.Switch, "Disable vehicle damage", t("reallife_meta_off", "Off")));
            rows.Add(OptRow(UiSettingControl.Switch, "Disable vehicle fuel consumption", t("reallife_meta_off", "Off")));
        }
        else
        {
            m.Chosen = 3;
            rows.Add(OptHead(t("reallife_ui_opt_diff_economy", "Economy"), UiIcon.Coin));
            rows.Add(Changed(OptRow(UiSettingControl.Slider, "Tax percentage", "15%", "Impacts annual tax rate from the IRS", false, 0.15f)));
            rows.Add(OptRow(UiSettingControl.Slider, "Employee hourly salary multiplier", "1.0", null, false, 0.5f));
            rows.Add(OptRow(UiSettingControl.Slider, "Public prices multiplier", "1.0", "Impacts cost of various public expenses like wholesale/imported goods, hospital fees, etc", false, 0.5f));
            rows.Add(OptRow(UiSettingControl.Slider, "Bank interest multiplier", "1.0", "Impacts interest payments on loans and investments", false, 0.5f));
            rows.Add(Changed(OptRow(UiSettingControl.Slider, "Base Customer Promotion Multiplier", "0.8", "Impacts base level of customers without traffic or marketing", false, 0.7f)));
            rows.Add(OptRow(UiSettingControl.Slider, "Wholesale Urgent Fee Multiplier", "0.2", null, false, 0.2f));
            rows.Add(OptRow(UiSettingControl.Slider, "Importer Urgent Fee Multiplier", "0.75", null, false, 0.75f));
            rows.Add(OptRow(UiSettingControl.Switch, "Disable Wholesale / Import Limits", t("reallife_meta_off", "Off"), "Disables the weekly limit for wholesale and import suppliers."));
            rows.Add(OptRow(UiSettingControl.Switch, "All products available from importers", t("reallife_meta_off", "Off"), "Allows all products to be purchased from importers."));
            rows.Add(OptRow(UiSettingControl.Slider, "Export Price Multiplier", "0.65", null, false, 0.65f));
            rows.Add(OptRow(UiSettingControl.Slider, "Resale Value", "75%", "Percentage of an item's or vehicle's value received when selling it.", false, 0.75f));
        }
    }
}
