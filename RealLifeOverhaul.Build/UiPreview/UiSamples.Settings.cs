using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    private static readonly string[] KitGroups =
    {
        "reallife_opt_group_start", "reallife_opt_group_you", "reallife_opt_group_city", "reallife_opt_group_around", "reallife_opt_group_work",
        "reallife_opt_group_money", "reallife_opt_group_business", "reallife_opt_group_controls", "reallife_opt_group_performance", "reallife_opt_group_trouble"
    };

    private static readonly string[] KitEnglish =
    {
        "Start here", "Your character and home", "The city", "Getting around", "Work",
        "Money", "Your businesses", "Controls", "Performance", "Troubleshooting"
    };

    private static readonly UiIcon[] KitIcons =
    {
        UiIcon.Info, UiIcon.Person, UiIcon.Building, UiIcon.Car, UiIcon.Briefcase, UiIcon.Coin, UiIcon.Store, UiIcon.Key, UiIcon.Cpu, UiIcon.Wrench
    };

    private static readonly int[] KitCounts = { 8, 48, 63, 31, 44, 40, 52, 18, 22, 24 };

    public static readonly string[] SettingsStates =
    {
        "city", "search", "differ", "interface", "tests", "key", "empty",
        "start", "you", "around", "work", "money", "business", "controls", "performance", "trouble"
    };

    public static void SettingsKit(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, string state)
    {
        s.Begin("mod settings", f.Width, f.Height, dark);
        f.Icon = UiIcon.Gear;
        UiAt window = UiAppWindow.Shell(s, f, t("reallife_set_app", "Mod settings"), UiIcon.Gear, new UiNavEntry[0], 0, false, k, out float _, out float _, out float _, out float _);
        int group = GroupOf(state);
        string[] names = Names(t, false);
        UiIcon[] icons = KitIcons;
        int chosen = state == "search" || state == "empty" || state == "tests" ? (state == "tests" ? 9 : -1) : group;
        int[] counts = state == "search" || state == "empty" ? FoundCounts(state) : KitCounts;
        UiSettingsList.LayWithGroups(s, window, SettingsModel(t, state), f, names, icons, chosen, k, counts);
        s.AddPrompt(UiAction.Move, t("reallife_ui_prompt_move", "Move"));
        s.AddPrompt(UiAction.Confirm, t("reallife_ui_prompt_select", "Select"));
        s.AddPrompt(UiAction.Back, t("reallife_ui_prompt_back", "Back"));
        UiDraw.PromptBar(s, f, k);
    }

    public static void GuideKit(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, string state)
    {
        s.Begin("guide", f.Width, f.Height, dark);
        f.Icon = UiIcon.Info;
        var nav = GuideNav(t, state);
        float offset = 0f;
        UiAt at = UiAppWindow.ShellFooted(s, f, t("reallife_guide_app", "Guide"), UiIcon.Info, nav, state == "search" ? -1 : 0, ref offset, null, 0, k,
            out float x, out float y, out float w, out float bottom);
        if (state == "empty")
        {
            UiDraw.Search(s, at, x, y, Math.Min(UiSettingsList.SearchWidth, w), t("reallife_guide_find", "Search the guide"), "zzqq", null, UiSettingsList.SearchCommand);
            float top = y + UiTokens.Control + UiTokens.S4;
            UiDraw.EmptyState(s, at, x, top, w, bottom - top, UiIcon.Search, t("reallife_guide_none", "No page has those words"), t("reallife_guide_none_body", "Try the name of a feature."));
        }
        else
        {
            string query = state == "search" ? "credit" : string.Empty;
            UiDraw.Search(s, at, x, y, Math.Min(UiSettingsList.SearchWidth, w), t("reallife_guide_find", "Search the guide"), query, null, UiSettingsList.SearchCommand);
            float top = y + UiTokens.Control + UiTokens.S4;
            UiReader.Lay(s, at, GuidePage(t, state), x, top, w, bottom - top, k);
        }
        s.AddPrompt(UiAction.Move, t("reallife_ui_prompt_move", "Move"));
        s.AddPrompt(UiAction.Confirm, t("reallife_ui_prompt_select", "Select"));
        s.AddPrompt(UiAction.Back, t("reallife_ui_prompt_back", "Back"));
        UiDraw.PromptBar(s, f, k);
    }

    public static void StaffKit(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, string state)
    {
        s.Begin("staff", f.Width, f.Height, dark);
        f.Icon = UiIcon.People;
        var nav = StaffNav(t, state == "empty");
        float offset = 0f;
        int selected = state == "empty" ? -1 : 1;
        UiAt at = UiAppWindow.ShellFooted(s, f, t("reallife_staff_app", "Staff and roles"), UiIcon.People, nav, selected, ref offset, null, 0, k,
            out float x, out float y, out float w, out float bottom);
        if (state == "empty")
        {
            UiDraw.Search(s, at, x, y, Math.Min(UiSettingsList.SearchWidth, w), t("reallife_staff_search", "Search staff and roles"), "zzqq", null, UiSettingsList.SearchCommand);
            float top = y + UiTokens.Control + UiTokens.S4;
            UiDraw.EmptyState(s, at, x, top, w, bottom - top, UiIcon.Search, t("reallife_staff_none", "No entry has those words"), t("reallife_staff_none_body", "Try a business or a role."));
        }
        else
        {
            string query = state == "search" ? "cafe" : string.Empty;
            UiDraw.Search(s, at, x, y, Math.Min(UiSettingsList.SearchWidth, w), t("reallife_staff_search", "Search staff and roles"), query, null, UiSettingsList.SearchCommand);
            float top = y + UiTokens.Control + UiTokens.S4;
            UiReader.Lay(s, at, StaffPage(t), x, top, w, bottom - top, k);
        }
        s.AddPrompt(UiAction.Move, t("reallife_ui_prompt_move", "Move"));
        s.AddPrompt(UiAction.Confirm, t("reallife_ui_prompt_select", "Select"));
        s.AddPrompt(UiAction.Back, t("reallife_ui_prompt_back", "Back"));
        UiDraw.PromptBar(s, f, k);
    }

    public static void BugKit(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, bool shot)
    {
        var m = new UiReportModel
        {
            Title = t("reallife_bug_title", "Report a bug"),
            Body = t("reallife_bug_card_body", "Tell us what went wrong. The report is saved as a file for you to post. Nothing is sent on its own."),
            HappenedLabel = t("reallife_bug_happened", "What happened"),
            Happened = "I opened Finance and went to Borrow. Every bank showed 0 as the most it would lend.",
            HappenedHint = t("reallife_bug_happened_hint", "A few sentences is enough."),
            ExpectedLabel = t("reallife_bug_expected", "What you expected"),
            Expected = "Each bank's offer, with its rate and the most it lends.",
            ExpectedHint = t("reallife_bug_expected_hint", "What you thought you would see."),
            WhereLabel = t("reallife_bug_where", "Where it happened"),
            Where = "Finance, Borrow",
            WhereOptions = new[] { "Finance, Borrow", t("reallife_bug_where_city", "The city") },
            Checks = new[]
            {
                new UiReportCheck { Text = t("reallife_bug_log_line", "Player.log attached, {size}, with your folder names left out").Replace("{size}", "2.4 MB"), Met = true },
                new UiReportCheck { Text = t("reallife_bug_version_line", "The mod version ({version}) and your game build are added").Replace("{version}", "0.63.9"), Met = true },
                new UiReportCheck { Text = t("reallife_bug_shot_line", "Screenshot of this screen"), Met = shot, Link = shot ? null : t("reallife_bug_shot_add", "Add one") }
            },
            Buttons = new[]
            {
                new UiActionModel { Label = t("reallife_ui_prompt_back", "Back"), Kind = UiButtonKind.Secondary, Key = UiAction.Back },
                new UiActionModel { Label = t("reallife_bug_save", "Save to a file"), Kind = UiButtonKind.Primary, Key = UiAction.Alt }
            },
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
                new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
                new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") }
            },
            PromptCount = 3
        };
        UiReport.Lay(s, m, f, dark, k);
    }

    public static void SaveKit(UiScene s, UiFrame f, bool dark, IUiKeys k, UiWords t, int which)
    {
        string title, body, yes;
        if (which == 1)
        {
            title = t("reallife_meta_resetdata_title", "Reset the mod's data in this save");
            body = t("reallife_save_reset_body", "This puts the save back to the day the mod was added, after a backup. Phone contacts stay.");
            yes = t("reallife_meta_resetdata_yes", "Back up and reset");
        }
        else if (which == 2)
        {
            title = t("reallife_meta_repair_title", "Repair");
            body = t("reallife_save_repair_body", "This puts back moods and phone contacts that have gone missing. Nothing else is changed.");
            yes = t("reallife_save_repair_yes", "Repair");
        }
        else
        {
            title = t("reallife_meta_remove_title", "Prepare this save for removing the mod");
            body = t("reallife_save_prepare_body", "This backs the save up and takes the mod out of it, so you can keep playing without the mod.");
            yes = t("reallife_meta_remove_yes", "Back up, clean and save");
        }
        var card = new UiCardModel
        {
            Icon = UiIcon.Warning,
            Title = title,
            Body = body,
            BodyLines = 6,
            Focus = 0,
            Buttons = new[]
            {
                new UiActionModel { Label = t("reallife_ui_prompt_back", "Back"), Kind = UiButtonKind.Secondary, Key = UiAction.Back },
                new UiActionModel { Label = yes, Kind = UiButtonKind.Destructive, Key = UiAction.Alt }
            },
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
                new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") }
            },
            PromptCount = 2
        };
        UiCard.Lay(s, card, f, dark, k);
    }

    private static int GroupOf(string state)
    {
        switch (state)
        {
            case "start": return 0;
            case "you": return 1;
            case "around": return 3;
            case "work": return 4;
            case "money": return 5;
            case "business": return 6;
            case "controls": return 7;
            case "performance": return 8;
            case "trouble": return 9;
            case "tests": return 9;
            default: return 2;
        }
    }

    private static string[] Names(UiWords t, bool tests)
    {
        var names = new string[tests ? KitGroups.Length + 1 : KitGroups.Length];
        for (int i = 0; i < KitGroups.Length; i++)
        {
            names[i] = t(KitGroups[i], KitEnglish[i]);
        }
        if (tests)
        {
            names[names.Length - 1] = t("reallife_meta_title_tests", "Test tools");
        }
        return names;
    }

    private static UiIcon[] WithTests()
    {
        var icons = new UiIcon[KitIcons.Length + 1];
        for (int i = 0; i < KitIcons.Length; i++)
        {
            icons[i] = KitIcons[i];
        }
        icons[icons.Length - 1] = UiIcon.Bug;
        return icons;
    }

    private static int[] WithCounts()
    {
        var counts = new int[KitCounts.Length + 1];
        for (int i = 0; i < KitCounts.Length; i++)
        {
            counts[i] = KitCounts[i];
        }
        counts[counts.Length - 1] = 12;
        return counts;
    }

    private static int[] FoundCounts(string state)
    {
        var counts = new int[KitCounts.Length];
        if (state == "search")
        {
            counts[2] = 4;
            counts[9] = 1;
        }
        return counts;
    }

    private static UiSettingsModel SettingsModel(UiWords t, string state)
    {
        var rows = new List<UiSettingRow>();
        string summary = t("reallife_set_group_count", "{sections} sections, {options} options").Replace("{sections}", "8").Replace("{options}", "63");
        if (state != "search" && state != "empty" && state != "differ")
        {
            rows.Add(new UiSettingRow { Name = summary, Icon = UiIcon.Info });
        }
        if (state == "tests")
        {
            rows.Add(new UiSettingRow { Name = t("reallife_set_box_tests", "Each test tool stages something in the city at once."), Icon = UiIcon.Bug });
            rows.Add(Row(t, "Open Mod settings on the kit", "Opens the options on the kit.", UiSettingControl.Action, "Run"));
            rows.Add(Row(t, "Search the settings", "Types a word and shows the matches.", UiSettingControl.Action, "Run"));
        }
        else if (state == "key")
        {
            rows.Add(Head("Controls"));
            rows.Add(new UiSettingRow
            {
                Control = UiSettingControl.Key, Name = "Photo mode", Listening = true,
                Value = t("reallife_set_press", "Press a key"),
                Tip = t("reallife_set_keys_note", "Click a key, then press the new key.")
            });
        }
        else if (state == "interface")
        {
            rows.Add(Head("RLO interface"));
            rows.Add(Row(t, "Mod settings on the new interface", "This screen. Off, the phone shows the older page.", UiSettingControl.Switch, t("reallife_meta_on", "On"), true));
            rows.Add(Row(t, "Guide on the new interface", "The Guide. Off, the phone shows the older page.", UiSettingControl.Switch, t("reallife_meta_on", "On"), true));
            rows.Add(Row(t, "Staff and roles on the new interface", "The reference. Off, the phone shows the older page.", UiSettingControl.Switch, t("reallife_meta_on", "On"), true));
        }
        else if (state == "search")
        {
            rows.Add(Marked("Police", "Patrol cars come to accidents and crimes."));
            rows.Add(Marked("Police on the streets", "How many parked patrol cars and officers you see."));
            rows.Add(Marked("Police stations", "Each district has a station with its sign."));
        }
        else if (state == "empty")
        {
        }
        else if (state == "differ")
        {
            rows.Add(new UiSettingRow
            {
                Control = UiSettingControl.Slider, Name = "How much is going on", Value = "8", Share = 0.7f, Differs = true,
                Tip = "1 is now and then, 10 is always something."
            });
            rows.Add(new UiSettingRow
            {
                Control = UiSettingControl.Switch, Name = "Easy needs", Tip = "Needs wear down half as fast.",
                Value = t("reallife_meta_off", "Off"), On = false, Differs = true
            });
        }
        else
        {
            rows.Add(Head("Street life"));
            rows.Add(new UiSettingRow
            {
                Control = UiSettingControl.Action, Name = t("reallife_set_about", "About this"),
                Tip = t("reallife_set_about_tip", "Opens this section in the Guide."), Value = t("reallife_set_about", "About this")
            });
            rows.Add(Row(t, "Street life", "People work and go about their day on the pavements.", UiSettingControl.Switch, t("reallife_meta_on", "On"), true));
            rows.Add(new UiSettingRow { Control = UiSettingControl.Slider, Name = "How much is going on", Tip = "1 is now and then, 10 is always something.", Value = "6", Share = 0.55f });
            rows.Add(Head("City sounds"));
            rows.Add(Row(t, "City sounds", "Birds, crickets and distant traffic.", UiSettingControl.Switch, t("reallife_meta_off", "Off"), false));
            string needs = t("reallife_set_box_needs_line", "This does nothing until {option} is on.").Replace("{option}", "City sounds");
            rows.Add(new UiSettingRow { Control = UiSettingControl.Slider, Name = "Birds by day", Tip = needs, Value = "60", Share = 0.6f, Greyed = true });
        }
        bool differ = state == "differ";
        return new UiSettingsModel
        {
            Rows = rows.ToArray(),
            RowCount = rows.Count,
            SearchPlaceholder = t("reallife_set_search_hint", "A word of an option"),
            Query = state == "search" ? "police" : state == "empty" ? "zzqq" : string.Empty,
            Found = state == "search" ? "3" : null,
            Preset = t("reallife_set_headline_in_use", "Preset in use: {preset}").Replace("{preset}", "Standard"),
            Differs = differ || state == "city" ? t("reallife_set_differ_count", "{count} differ").Replace("{count}", "4") : null,
            PresetActions = new[]
            {
                new UiActionModel { Label = t("reallife_set_list_differ", "List the differences"), Kind = UiButtonKind.Secondary, Key = UiAction.Option },
                new UiActionModel { Label = t("reallife_set_apply_preset", "Apply {preset}").Replace("{preset}", "Standard"), Kind = UiButtonKind.Primary, Key = UiAction.Alt }
            },
            CanBack = true,
            EmptyTitle = t("reallife_set_empty_found", "Nothing found. Try another word."),
            EmptyBody = t("reallife_set_box_found_none", "No option has that word. Try a shorter one, or the name of a section.")
        };
    }

    private static UiSettingRow Head(string name) => new UiSettingRow { Name = name, Icon = UiIcon.Building };

    private static UiSettingRow Row(UiWords t, string name, string tip, UiSettingControl control, string value, bool on = false)
    {
        return new UiSettingRow { Control = control, Name = name, Tip = tip, Value = value, On = on };
    }

    private static UiSettingRow Marked(string name, string tip)
    {
        return new UiSettingRow { Control = UiSettingControl.Switch, Name = name, Tip = tip, Value = "On", On = true, MatchStart = 0, MatchLength = 6 };
    }

    private static UiNavEntry[] GuideNav(UiWords t, string state)
    {
        if (state == "empty")
        {
            return new UiNavEntry[0];
        }
        string start = t("reallife_guide_own", "The Guide");
        string money = t("reallife_opt_group_money", "Money");
        return new[]
        {
            new UiNavEntry { Icon = UiIcon.Info, Label = t("reallife_guide_start_title", "Start here"), Group = start },
            new UiNavEntry { Icon = UiIcon.Newspaper, Label = t("reallife_guide_whatsnew_title", "What's new"), Group = start },
            new UiNavEntry { Icon = UiIcon.Coin, Label = "Your credit rating", Group = money },
            new UiNavEntry { Icon = UiIcon.Bank, Label = "Banks and loans", Group = money },
            new UiNavEntry { Icon = UiIcon.People, Label = "Staff and roles", Group = t("reallife_guide_own", "The Guide") }
        };
    }

    private static UiReaderModel GuidePage(UiWords t, string state)
    {
        if (state == "whatsnew" || state == "long")
        {
            string day = "2 " + t("reallife_calendar_month_11", "November") + " 2026";
            string title = t("reallife_changes_063_9_title", "0.63.9");
            string body = t("reallife_changes_063_9", "Six fixes from your reports.");
            return new UiReaderModel
            {
                Title = t("reallife_guide_whatsnew_title", "What's new"),
                Picture = UiIcon.Newspaper,
                Blocks = new[]
                {
                    new UiReaderBlock { Kind = UiReaderKind.Heading, Text = day, Chip = title },
                    new UiReaderBlock { Kind = UiReaderKind.Note, Text = t("reallife_guide_new_since", "New since you last read"), Icon = UiIcon.Spark },
                    new UiReaderBlock { Kind = UiReaderKind.Paragraph, Text = body }
                },
                FactsTitle = t("reallife_guide_on_page", "On this page"),
                Facts = new[] { new UiPairModel { Key = title, Value = day } },
                RelatedTitle = t("reallife_guide_next", "Next to read"),
                Related = new[] { t("reallife_guide_start_title", "Start here") }
            };
        }
        return new UiReaderModel
        {
            Title = t("reallife_guide_start_title", "Start here"),
            Picture = UiIcon.Info,
            Blocks = new[]
            {
                new UiReaderBlock { Kind = UiReaderKind.Heading, Text = "Presets" },
                new UiReaderBlock { Kind = UiReaderKind.Paragraph, Text = "Relaxed, Standard, Realistic and Hardcore set a whole page of options at once. You can change any of them after." },
                new UiReaderBlock { Kind = UiReaderKind.Heading, Text = "Where to look" },
                new UiReaderBlock { Kind = UiReaderKind.Paragraph, Text = "Each feature has its own page. The sidebar groups them the way Mod settings does." }
            },
            FactsTitle = t("reallife_guide_on_page", "On this page"),
            Facts = new[] { new UiPairModel { Key = "Presets", Value = "1" }, new UiPairModel { Key = "Where to look", Value = "2" } },
            RelatedTitle = t("reallife_guide_next", "Next to read"),
            Related = new[] { t("reallife_guide_whatsnew_title", "What's new") }
        };
    }

    private static UiNavEntry[] StaffNav(UiWords t, bool empty)
    {
        if (empty)
        {
            return new UiNavEntry[0];
        }
        string places = t("reallife_staff_businesses", "Businesses");
        string roles = t("reallife_staff_roles", "Roles");
        return new[]
        {
            new UiNavEntry { Icon = UiIcon.Store, Label = "Bloom Cafe", Group = places },
            new UiNavEntry { Icon = UiIcon.Store, Label = "Harbour Data Centre", Group = places },
            new UiNavEntry { Icon = UiIcon.Store, Label = "Midtown Supermarket", Group = places },
            new UiNavEntry { Icon = UiIcon.Person, Label = "Barista", Group = roles },
            new UiNavEntry { Icon = UiIcon.Person, Label = "Data engineer", Group = roles },
            new UiNavEntry { Icon = UiIcon.Person, Label = "Shift manager", Group = roles }
        };
    }

    private static UiReaderModel StaffPage(UiWords t)
    {
        return new UiReaderModel
        {
            Title = "Harbour Data Centre",
            Picture = UiIcon.Store,
            Blocks = new[]
            {
                new UiReaderBlock
                {
                    Kind = UiReaderKind.Paragraph,
                    Text = "A data centre keeps other firms' machines running. It needs engineers on shift and a manager who can answer a fault at night."
                },
                new UiReaderBlock { Kind = UiReaderKind.Heading, Text = "Who it needs" },
                new UiReaderBlock { Kind = UiReaderKind.Bullet, Text = "Data engineers cover the floor. Without one, faults wait until morning." },
                new UiReaderBlock { Kind = UiReaderKind.Bullet, Text = "A shift manager signs the night book." },
                new UiReaderBlock { Kind = UiReaderKind.Link, Text = "Data engineer" }
            },
            FactsTitle = t("reallife_guide_on_page", "On this page"),
            Facts = new[] { new UiPairModel { Key = "Who it needs", Value = "1" } },
            RelatedTitle = t("reallife_guide_next", "Next to read"),
            Related = new[] { "Data engineer" }
        };
    }
}
