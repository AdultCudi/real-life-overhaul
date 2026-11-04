using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Ui;

/// <summary>
/// The full menu's frame and the boxes and notices every screen uses (Scripts/Ui/Shell), with real-looking words: the
/// status bar's app row with an app open and news on a tile, All apps on each folder (other mods' apps with their own
/// pictures, and none of them), the question box plain and negative and with a long body, the acknowledgement, the text box empty and
/// typed in, the mod's own message, the notice cards (every kind, three at once, a long one) and the notice list, full
/// and empty. Each word through its key, so every language is laid out.
/// </summary>
internal static partial class UiSamples
{
    private static string[] _rowIds;

    /// <summary>The app row's apps: the phone's first home page in its own order (the sample phone's, as the frame fills it).</summary>
    public static string[] RowIds
    {
        get
        {
            if (_rowIds == null)
            {
                var row = new List<string>();
                foreach (UiShellApp a in ShellApps((key, english) => english, true))
                {
                    if (a.InRow)
                    {
                        row.Add(a.Id);
                    }
                }
                _rowIds = row.ToArray();
            }
            return _rowIds;
        }
    }

    public static int RowContacts => System.Array.IndexOf(RowIds, "Contacts");

    public static int RowBizMan => System.Array.IndexOf(RowIds, "BizMan");

    /// <summary>
    /// The frame's apps as the game side fills them while the kit phone stands: the sample phone's own model through
    /// UiShellApps.FromPhone (its order, glyphs, colours, news and other mods' pictures), then the overhaul's pages.
    /// </summary>
    public static List<UiShellApp> ShellApps(UiWords t, bool others)
    {
        var into = new List<UiShellApp>(64);
        UiShellApps.FromPhone(PhoneApps(t, others), into, PagerRules.PhoneTiles);   // the apps' titles are their buttons' words: data, as on the phone
        Page(into, UiShellApps.ShellPeople, t("reallife_ui_page_population", "The city's people"));
        Page(into, UiShellApps.ShellFirm, t("reallife_ui_page_studio", "Your firm"));
        return into;
    }

    private static void Page(List<UiShellApp> into, string id, string title)
    {
        UiIcon icon = UiShellApps.IconOf(id, UiShellKind.Page);
        into.Add(new UiShellApp { Id = id, Title = title, Icon = icon, Tint = UiIconTints.Of(icon), Kind = UiShellKind.Page, Folder = UiShellApps.FolderOf(id, UiShellKind.Page, -1) });
    }

    /// <summary>The status bar with the frame's app row, as UiGame fills it: the open app filled, the rest's news on All apps.</summary>
    public static UiStatusModel ShellBar(int open, bool appsOpen)
    {
        UiStatusModel m = Bar(-1);
        List<UiShellApp> apps = ShellApps((key, english) => english, true);
        var row = apps.FindAll(a => a.InRow);
        m.Apps = new UiIcon[row.Count];
        m.AppTints = new UiRole[row.Count];
        m.AppBadges = new int[row.Count];
        m.AppPictures = new int[row.Count];
        for (int i = 0; i < row.Count; i++)
        {
            m.Apps[i] = row[i].Icon;
            m.AppTints[i] = row[i].Tint;
            m.AppBadges[i] = row[i].Badge;
            m.AppPictures[i] = row[i].Picture;
        }
        foreach (UiShellApp a in apps)
        {
            m.AllAppsBadge += a.InRow ? 0 : a.Badge;
        }
        m.App = open;
        m.AllApps = true;
        m.AllAppsOpen = appsOpen;
        m.AppsPress = true;
        m.AllAppsLabel = "All apps";
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

    /// <summary>All apps on a folder (0 Money .. 4 Settings, 5 Other mods), Contacts open; without other mods, Other mods stands empty.</summary>
    public static UiAllAppsModel AllApps(UiWords t, int folder, bool others = true)
    {
        var m = new UiAllAppsModel { Title = t("reallife_ui_shell_all_apps", "All apps"), Tab = folder, Tabs = new UiAllAppsTab[UiShellApps.Folders] };
        List<UiShellApp> apps = ShellApps(t, others);
        for (int f = 0; f < UiShellApps.Folders; f++)
        {
            int badge = 0;
            foreach (UiShellApp a in apps)
            {
                badge += a.Folder == f ? a.Badge : 0;
            }
            UiIcon icon = UiShellApps.FolderIcon(f);
            m.Tabs[f] = new UiAllAppsTab { Icon = icon, Tint = UiIconTints.Of(icon), Label = FolderName(t, f), Badge = badge };
        }
        List<UiShellApp> rows = apps.FindAll(a => a.Folder == folder);
        m.Rows = new UiAllAppsRow[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            UiShellApp a = rows[i];
            m.Rows[i] = new UiAllAppsRow
            {
                Icon = a.Icon, Tint = a.Tint, Title = a.Title, Badge = a.Badge, Open = a.Id == "Contacts", Picture = a.Picture,
                Status = a.Id == "Contacts" ? t("reallife_ui_shell_status_unread", "{count} unread").Replace("{count}", a.Badge.ToString()) : null
            };
        }
        m.RowCount = rows.Count;
        m.Empty = folder == UiShellApps.Other ? t("reallife_ui_shell_none_other", "No other mod has put an app on the phone. Apps of other mods show here.") : t("reallife_ui_shell_none", "No apps in this folder yet. Apps you put in it on the phone show here.");
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

    /// <summary>
    /// The game's question box, with words of real boxes: the mod's sale of shares (plain), the game's unsaved changes
    /// (negative: the danger's look, the keys starting on the way back) and the mod's Permadeath question (a long body).
    /// </summary>
    public static UiCardModel Question(UiWords t, bool negative, bool longBody)
    {
        string title = negative ? t("hud_confirm_are_you_sure", "Are you sure?") : longBody ? t("reallife_pd_ask_header", "Switch Permadeath on?")
            : t("reallife_vc_confirm_sell_title", "Sell a tenth of your shares?");
        string body = negative ? t("change_character_clothes_unsaved_changes_warning", "Unsaved changes will be lost. Continue?") : longBody ? t("reallife_pd_ask_body", "From now on this character can die for good, after a warning: a crash at very high speed, being hit by fast traffic on foot, a mugging refused, an illness left untreated or days without food. A death is saved at once and the save can never be played again. This is for this save only, and you can switch it off again while your character lives.")
            : t("reallife_vc_confirm_sell_body", "The lead investor buys them at a fifth under the last round's price. You keep the rest.");
        var m = new UiCardModel
        {
            Icon = negative ? UiIcon.Warning : UiIcon.Info, Title = title, Body = body, Focus = negative ? 0 : -1,
            Buttons = new[]
            {
                new UiActionModel { Label = t("common_cancel", "Cancel"), Kind = UiButtonKind.Secondary, Key = UiAction.Back, Command = UiCard.ButtonCommand, Arg = 0 },
                new UiActionModel { Label = t("common_confirm", "Confirm"), Kind = negative ? UiButtonKind.Destructive : UiButtonKind.Primary, Key = UiAction.Alt, Command = UiCard.ButtonCommand, Arg = 1 },
            },
            Prompts = CardPrompts(t)
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>The game's own acknowledgement (its warning about mods made by players), its line to tick, Cancel and I agree.</summary>
    public static UiCardModel Acknowledge(UiWords t, bool ticked)
    {
        var m = new UiCardModel
        {
            Icon = UiIcon.Warning, Title = t("main_menu_mods_warning_header", "Community Mods Warning"), Body = t("main_menu_mods_warning_body", "You are about to access user generated content\n\n- These mods are created by the community. They are not developed, endorsed, or supported by Hovgaard Games.\n\n- Hovgaard Games does not review, monitor, or scan mods for security, stability, or content.\n\n- Mods may cause crashes, game instability, save file corruption, or other unexpected behavior. In rare cases, malicious mods could pose security risks to your computer. Install and use mods at your own risk.\n\n- We strongly recommend backing up your save files before using mods."), BodyLines = 20,
            Check = t("reallife_ui_box_dont_show", "Do not show this again"), Checked = ticked, Focus = 1,
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

    /// <summary>The game's text box as the mod's brands ask for a name: the field empty or typed in, Cancel and Rename (held while empty, no letter's cap while typing).</summary>
    public static UiCardModel TextBox(UiWords t, string typed)
    {
        bool empty = string.IsNullOrEmpty(typed);
        var m = new UiCardModel
        {
            Icon = UiIcon.Message, Title = t("reallife_brand_name_placeholder", "The brand's name"), HasField = true, Field = typed,
            Placeholder = t("reallife_ui_box_type_here", "Type here"),
            Buttons = new[]
            {
                new UiActionModel { Label = t("reallife_ui_box_cancel", "Cancel"), Kind = UiButtonKind.Secondary, Key = UiAction.Back, Command = UiCard.ButtonCommand, Arg = 0 },
                new UiActionModel { Label = t("reallife_brand_name_confirm", "Rename"), Kind = UiButtonKind.Primary, Key = UiAction.Count, Command = UiCard.ButtonCommand, Arg = 1, Disabled = empty },
            },
            Prompts = CardPrompts(t)
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>The mod's own message on the card, its longest real one: the reset of its data, marked as a danger (it cannot be undone), Cancel first.</summary>
    public static UiCardModel ModBox(UiWords t)
    {
        var m = new UiCardModel
        {
            Icon = UiIcon.Warning, Focus = 0, Title = t("reallife_meta_resetdata_title", "Reset the mod's data in this save"), Body = t("reallife_meta_resetdata_explain", "This makes the save as if the mod had just been added to it, and keeps the mod running:\n\n- everything the mod has going is ended (accidents and other incidents, jobs, the mod's people and things on the street);\n- its moods are taken off;\n- its stored data is removed: which life events you have seen, illness, fines owed, job and career progress, your driving licence, your record and court cases, the people you know, custom shop work, and the clothes picked for this save (the picks on the options page stay).\n\nPhone contacts and their messages stay. A backup of the save file is made first (if that fails, nothing is done). The save file itself is not changed until you save."), BodyLines = 28,
            Buttons = new[]
            {
                new UiActionModel { Label = t("reallife_meta_cancel", "Cancel"), Kind = UiButtonKind.Secondary, Key = UiAction.Back, Command = UiCard.ButtonCommand, Arg = 1 },
                new UiActionModel { Label = t("reallife_meta_resetdata_yes", "Back up and reset"), Kind = UiButtonKind.Destructive, Key = UiAction.Alt, Command = UiCard.ButtonCommand, Arg = 0 },
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
