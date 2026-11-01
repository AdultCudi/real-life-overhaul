using RealLifeOverhaul.Ui;

/// <summary>
/// The full menu's frame and the boxes and notices every screen uses (Scripts/Ui/Shell), with real-looking words: the
/// status bar's app row with an app open and news on a tile, All apps on each folder (other mods' apps among them, an
/// empty folder), the question box plain and negative and with a long body, the acknowledgement, the text box empty and
/// typed in, the mod's own message, the notice cards (every kind, three at once, a long one) and the notice list, full
/// and empty. Each word through its key, so every language is laid out.
/// </summary>
internal static partial class UiSamples
{
    /// <summary>The phone's first home page in the app row, then All apps: the old dock's two, then the game's own.</summary>
    public static readonly string[] RowIds = { "RealLifePlanner", "RealLifeFlare", "Persona", "Contacts", "MyEmployees", "BizMan", "EconoView", "VoogleMaps" };

    public const int RowContacts = 3, RowBizMan = 5;

    /// <summary>The status bar with the frame's app row: the open app filled, news on Contacts and the Planner, the rest's on All apps.</summary>
    public static UiStatusModel ShellBar(int open, bool appsOpen)
    {
        UiStatusModel m = Bar(-1);
        m.Apps = new UiIcon[RowIds.Length];
        m.AppTints = new UiRole[RowIds.Length];
        m.AppBadges = new int[RowIds.Length];
        for (int i = 0; i < RowIds.Length; i++)
        {
            m.Apps[i] = UiShellApps.IconOf(RowIds[i], UiShellKind.Game);
            m.AppTints[i] = UiIconTints.Of(m.Apps[i]);
        }
        m.AppBadges[0] = 1;
        m.AppBadges[RowContacts] = 3;
        m.App = open;
        m.AllApps = true;
        m.AllAppsOpen = appsOpen;
        m.AllAppsBadge = 2;
        m.AppsPress = true;
        return m;
    }

    /// <summary>The status bar while walking: the HUD's pills, no app row (that is the full menu's).</summary>
    public static UiStatusModel WalkingBar()
    {
        UiStatusModel m = Bar(-1);
        m.Apps = null;
        m.AppTints = null;
        return m;
    }

    private struct ShellRow
    {
        public string Id, Key, English;
        public UiShellKind Kind;
        public int Badge;
        public string Status;
    }

    private static readonly ShellRow[][] Folders =
    {
        new[]
        {
            new ShellRow { Id = "EconoView", Key = "smartphone_econoview", English = "EconoView" },
            new ShellRow { Id = "RealLifeFinance", Key = "reallife_econ_app", English = "Finance" },
            new ShellRow { Id = "RealLifeBanks", Key = "reallife_banks_app", English = "Banks" },
            new ShellRow { Id = "RealLifeStocks", Key = "reallife_mkt_app", English = "Stocks" },
            new ShellRow { Id = "RealLifeEconomy", Key = "reallife_econapp_app", English = "Economy" },
        },
        new[]
        {
            new ShellRow { Id = "RealLifePlanner", Key = "reallife_planner_app", English = "Planner", Badge = 1 },
            new ShellRow { Id = "RealLifeCareer", Key = "reallife_career_app", English = "Career" },
            new ShellRow { Id = "RealLifeCampus", Key = "reallife_campus_app", English = "Campus" },
            new ShellRow { Id = "RealLifeOpenRoles", Key = "reallife_roles_app", English = "Open roles" },
        },
        new[]
        {
            new ShellRow { Id = "Persona", Key = "smartphone_persona", English = "Persona" },
            new ShellRow { Id = "Contacts", Key = "smartphone_contacts", English = "Contacts", Badge = 3, Status = "reallife_ui_shell_status_unread" },
            new ShellRow { Id = "VoogleMaps", Key = "smartphone_vooglemaps", English = "Voogle Maps" },
            new ShellRow { Id = "RealLifeFlare", Key = "reallife_flare_app", English = "Flare" },
            new ShellRow { Id = "RealLifeCalendar", Key = "reallife_calendar_app", English = "Calendar" },
            new ShellRow { Id = UiShellApps.ShellPeople, Key = "reallife_ui_page_population", English = "The city's people", Kind = UiShellKind.Page },
            new ShellRow { Id = UiShellApps.ShellFlare, Key = "reallife_ui_page_flare", English = "Flare in the city", Kind = UiShellKind.Page },
        },
        new[]
        {
            new ShellRow { Id = "MyEmployees", Key = "smartphone_myemployees", English = "MyEmployees" },
            new ShellRow { Id = "BizMan", Key = "smartphone_bizman", English = "BizMan" },
            new ShellRow { Id = "MarketInsider", Key = "smartphone_marketinsider", English = "MarketInsider" },
            new ShellRow { Id = "Rivals", Key = "smartphone_rivals", English = "Rivals" },
            new ShellRow { Id = "RealLifeCompany", Key = "reallife_co_app", English = "Company" },
            new ShellRow { Id = UiShellApps.ShellFirm, Key = "reallife_ui_page_studio", English = "Your firm", Kind = UiShellKind.Page },
        },
        new ShellRow[0],
        new[]
        {
            new ShellRow { Id = "BAMP_Chat", English = "Chat", Kind = UiShellKind.Other, Badge = 2 },
            new ShellRow { Id = "EmpireBank", English = "Empire Bank", Kind = UiShellKind.Other },
            new ShellRow { Id = "SmartRestock", English = "Smart Restock", Kind = UiShellKind.Other },
        },
    };

    /// <summary>All apps on a folder (0 Money .. 4 Settings, 5 Other mods), Contacts open.</summary>
    public static UiAllAppsModel AllApps(UiWords t, int folder)
    {
        var m = new UiAllAppsModel { Title = t("reallife_ui_shell_all_apps", "All apps"), Tab = folder, Tabs = new UiAllAppsTab[UiShellApps.Folders] };
        for (int f = 0; f < UiShellApps.Folders; f++)
        {
            int badge = 0;
            foreach (ShellRow r in Folders[f])
            {
                badge += r.Badge;
            }
            UiIcon icon = UiShellApps.FolderIcon(f);
            m.Tabs[f] = new UiAllAppsTab { Icon = icon, Tint = UiIconTints.Of(icon), Label = FolderName(t, f), Badge = badge };
        }
        ShellRow[] rows = Folders[folder];
        m.Rows = new UiAllAppsRow[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            ShellRow r = rows[i];
            UiIcon icon = UiShellApps.IconOf(r.Id, r.Kind);
            m.Rows[i] = new UiAllAppsRow
            {
                Icon = icon, Tint = UiIconTints.Of(icon), Title = r.Key != null ? t(r.Key, r.English) : r.English, Badge = r.Badge, Open = r.Id == "Contacts",
                Status = r.Status != null ? t(r.Status, "{count} unread").Replace("{count}", "3") : null
            };
        }
        m.RowCount = rows.Length;
        m.Empty = t(folder == UiShellApps.Other ? "reallife_ui_shell_none_other" : "reallife_ui_shell_none", "No apps in this folder yet.");
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.TabPrev, Label = t("reallife_ui_shell_prompt_folder", "Folder") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    private static string FolderName(UiWords t, int folder)
    {
        switch (folder)
        {
            case 0: return t("reallife_phone_folder_money", "Money");
            case 1: return t("reallife_phone_folder_work", "Work and study");
            case 2: return t("reallife_phone_folder_life", "Social and life");
            case 3: return t("reallife_phone_folder_business", "Business");
            case 4: return t("reallife_phone_folder_setup", "Settings");
            default: return t("reallife_ui_shell_other_mods", "Other mods");
        }
    }

    private static UiPrompt[] CardPrompts(UiWords t) => new[]
    {
        new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
        new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
        new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
    };

    /// <summary>The game's question box: plain, negative (the danger's look) or with a long body.</summary>
    public static UiCardModel Question(UiWords t, bool negative, bool longBody)
    {
        string body = negative
            ? t("reallife_ui_test_question_negative_body", "This is a test of a question about something that cannot be undone. Nothing is lost either way.")
            : t("reallife_ui_test_question_body", "This is a test question. Pick either answer: nothing else happens.");
        if (longBody)
        {
            body = body + " " + t("reallife_ui_test_ack_body", "This is a test of a warning you agree to. Ticking the line changes nothing here, as the test is never remembered.")
                   + " " + t("reallife_ui_test_mod_box_body", "This is the mod's own message on the RLO interface's card. Either button closes it.");
        }
        var m = new UiCardModel
        {
            Icon = negative ? UiIcon.Warning : UiIcon.Info, Title = t("hud_confirm_are_you_sure", "Are you sure?"), Body = body,
            Buttons = new[]
            {
                new UiActionModel { Label = t("common_cancel", "Cancel"), Kind = UiButtonKind.Secondary, Key = UiAction.Back, Command = UiCard.ButtonCommand, Arg = 0 },
                new UiActionModel { Label = t("common_confirm", "Confirm"), Kind = negative ? UiButtonKind.Armed : UiButtonKind.Primary, Key = UiAction.Alt, Command = UiCard.ButtonCommand, Arg = 1 },
            },
            Prompts = CardPrompts(t)
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>The game's acknowledgement: its warning, the line to tick, Cancel and I agree.</summary>
    public static UiCardModel Acknowledge(UiWords t, bool ticked)
    {
        var m = new UiCardModel
        {
            Icon = UiIcon.Warning, Title = t("reallife_ui_test_ack_title", "A test warning"),
            Body = t("reallife_ui_test_ack_body", "This is a test of a warning you agree to. Ticking the line changes nothing here, as the test is never remembered."),
            Check = t("reallife_ui_box_dont_show", "Do not show this again"), Checked = ticked,
            Buttons = new[]
            {
                new UiActionModel { Label = t("reallife_ui_box_cancel", "Cancel"), Kind = UiButtonKind.Secondary, Key = UiAction.Back, Command = UiCard.ButtonCommand, Arg = 0 },
                new UiActionModel { Label = t("common_agree", "I agree"), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiCard.ButtonCommand, Arg = 1 },
            },
            Prompts = CardPrompts(t)
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>The game's text box: what it asks, the field empty or typed in, Cancel and its own confirm (held while empty).</summary>
    public static UiCardModel TextBox(UiWords t, string typed)
    {
        bool empty = string.IsNullOrEmpty(typed);
        var m = new UiCardModel
        {
            Icon = UiIcon.Message, Title = t("reallife_ui_test_text_box_ask", "A name for the test"), HasField = true, Field = typed, Typing = true,
            Placeholder = t("reallife_ui_box_type_here", "Type here"),
            Buttons = new[]
            {
                new UiActionModel { Label = t("reallife_ui_box_cancel", "Cancel"), Kind = UiButtonKind.Secondary, Key = UiAction.Back, Command = UiCard.ButtonCommand, Arg = 0 },
                new UiActionModel { Label = t("reallife_ui_test_text_box_ok", "Done"), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiCard.ButtonCommand, Arg = 1, Disabled = empty },
            },
            Prompts = CardPrompts(t)
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>The mod's own message on the card: its title, its words, Cancel and OK.</summary>
    public static UiCardModel ModBox(UiWords t)
    {
        var m = new UiCardModel
        {
            Icon = UiIcon.Warning, Title = t("reallife_ui_test_mod_box_title", "A test message"),
            Body = t("reallife_ui_test_mod_box_body", "This is the mod's own message on the RLO interface's card. Either button closes it."),
            Buttons = new[]
            {
                new UiActionModel { Label = t("reallife_meta_cancel", "Cancel"), Kind = UiButtonKind.Secondary, Key = UiAction.Back, Command = UiCard.ButtonCommand, Arg = 1 },
                new UiActionModel { Label = t("reallife_meta_ok", "OK"), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiCard.ButtonCommand, Arg = 0 },
            },
            Prompts = CardPrompts(t)
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    private static readonly UiIcon[] NoticeGlyphs =
    {
        UiIcon.Car, UiIcon.Scales, UiIcon.Heartbeat, UiIcon.Coin, UiIcon.Home, UiIcon.Briefcase, UiIcon.Store, UiIcon.Cpu, UiIcon.Newspaper, UiIcon.People, UiIcon.Spark,
        UiIcon.Check, UiIcon.Warning, UiIcon.Info
    };

    private static readonly string[] NoticeKeys =
    {
        "reallife_ui_notice_road", "reallife_ui_notice_police", "reallife_ui_notice_health", "reallife_ui_notice_money", "reallife_ui_notice_property",
        "reallife_ui_notice_work", "reallife_ui_notice_business", "reallife_ui_notice_tech", "reallife_ui_notice_news", "reallife_ui_notice_people",
        "reallife_ui_notice_other", "reallife_ui_notice_done", "reallife_ui_notice_warning", "reallife_ui_notice_info"
    };

    private static readonly string[] NoticeEnglish =
    {
        "On the road", "Police and courts", "Health", "Money", "Home and property", "Work and study", "Your business", "Tech", "News", "Family and friends",
        "Life in the city", "Done", "Take note", "For your information"
    };

    // What each kind of notice says, as the game and the mod write them (the game's own words stay as the game wrote them).
    private static readonly string[] NoticeBodies =
    {
        "Your car needs a service soon. The garage on 5th Avenue has a slot tomorrow.", "A parking fine of $120 is due within 7 days.",
        "You feel run down. A night's sleep at home helps.", "Rent of $1,850 for 70 Fifth Avenue was paid.", "A buyer offered $412,000 for your flat in Midtown.",
        "Your shift at Bloom Cafe starts at 09:00.", "Bloom Cafe sold out of coffee beans at 14:10.", "Lantern Bay Games hired a new engineer.",
        "The City Ledger: the council votes on the harbour plan today.", "Your mother called. She would like to hear from you this week.",
        "A street market opens in Garment District on Saturday.", "Destination set to 70 Fifth Avenue.", "Not enough energy to work out.", "The game was saved."
    };

    /// <summary>The notice cards over the world: three at once, or one of each kind from <paramref name="first"/>, or one long one.</summary>
    public static UiNoticeCardsModel NoticeCards(UiWords t, int first, int count, bool longBody)
    {
        var m = new UiNoticeCardsModel { Cards = new UiNotice[count], CardCount = count, Top = 64f };
        for (int i = 0; i < count; i++)
        {
            int k = (first + i) % NoticeGlyphs.Length;
            string body = NoticeBodies[k];
            if (longBody)
            {
                body = "Your loan payment of $2,400 to Blue Harbor Bank is due on Day 415. Pay it from the bank's app before then, or the bank adds a late fee and your credit score falls.";
            }
            m.Cards[i] = new UiNotice { Icon = NoticeGlyphs[k], Header = t(NoticeKeys[k], NoticeEnglish[k]), Body = body, Age = "Day 412, 14:" + (10 + i * 7).ToString("00") };
        }
        return m;
    }

    /// <summary>The notice list: a day's notices newest first, or none.</summary>
    public static UiNoticeListModel NoticeList(UiWords t, bool empty)
    {
        int count = empty ? 0 : NoticeGlyphs.Length;
        var m = new UiNoticeListModel
        {
            Title = t("reallife_ui_notice_list", "Notifications"), Rows = new UiNotice[count], RowCount = count,
            Empty = t("reallife_ui_notice_none", "No notifications yet. New ones show here as they come."), ClearLabel = t("reallife_ui_notice_clear", "Clear all"), CanClear = !empty,
            Prompts = new[]
            {
                new UiPrompt { Action = UiAction.TabPrev, Label = t("reallife_ui_prompt_page", "Page") },
                new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
            },
            PromptCount = 2
        };
        for (int i = 0; i < count; i++)
        {
            m.Rows[i] = new UiNotice
            {
                Icon = NoticeGlyphs[i], Header = t(NoticeKeys[i], NoticeEnglish[i]), Body = NoticeBodies[i],
                Age = "Day 412, " + ((18 * 60 - i * 13) / 60).ToString("00") + ":" + ((18 * 60 - i * 13) % 60).ToString("00")   // newest first, each a little earlier
            };
        }
        return m;
    }
}
