using System;
using RealLifeOverhaul.Ui;

// The kit's sample view models: the owner's Contacts wireframe's own people and words (not-in-repo/design/wireframes,
// contacts_source.html), for the offline previews and EconomyCheck's layout checks. Fixtures, never shipped: the mod's
// screens build their models from the game's data and the locale keys. Words a screen labels with go through the given
// text source, so the checks measure every language; the fixtures' names and messages stay as the wireframe has them.
internal static class UiSamples
{
    /// <summary>The masthead's money, day and time in every preview and check: the game's own bar writes them so.</summary>
    public const string Status = "$48,210     Tuesday (Day 12)     14:32";

    /// <summary>A screen of the overhaul drawn from its own view model, with the app it stands in.</summary>
    public sealed class Sample
    {
        public string Name;
        public string App;
        public Action<UiScene, UiFrame, bool, IUiKeys> Lay;
        public string Walk = "DDRDLU";   // the d-pad's presses the focus path shot draws: U, D, L, R
    }

    /// <summary>The overhaul's screens as their own runners draw them from sample view models.</summary>
    public static System.Collections.Generic.List<Sample> Overhaul(UiWords t)
    {
        return new System.Collections.Generic.List<Sample>
        {
            new Sample { Name = "population", App = "Economy", Lay = (s, f, d, k) => UiDashboard.Lay(s, PopulationCity(t), f, d, k) },
            new Sample { Name = "flare_city", App = "Flare", Lay = (s, f, d, k) => UiDashboard.Lay(s, FlareCity(t), f, d, k) },
            new Sample { Name = "studio", App = "Tech", Lay = (s, f, d, k) => UiDashboard.Lay(s, StudioFirm(t), f, d, k), Walk = "LDDDDRDDLU" },
            new Sample { Name = "tech_firm", App = "Tech", Lay = (s, f, d, k) => UiDashboard.Lay(s, TechFirm(t), f, d, k), Walk = "DDDDRDDLU" },
            new Sample { Name = "session_panel", App = "Desk", Lay = (s, f, d, k) => UiSessionSteps.Lay(s, DeskPanel(t), f, d, k), Walk = "RDLUL" },
        };
    }

    /// <summary>Flare's account in the city as FlareCityPages makes it: the tabs, the followers' figures, the posts that carried, the details.</summary>
    public static UiDashboardModel FlareCity(UiWords t)
    {
        var m = new UiDashboardModel
        {
            Title = "Bloom Cafe on Flare",
            Subtitle = "Followers in the city, by the residents who follow",
            Tabs = new[] { t("reallife_ui_flare_tab_account", "Account"), t("reallife_ui_flare_tab_trends", "Trends"), t("reallife_ui_flare_tab_offers", "Offers") },
            SideTitle = t("reallife_ui_page_details", "Details"),
            Figures = new[]
            {
                new UiFigureModel { Label = "Followers", Value = "2,418" },
                new UiFigureModel { Label = "This week", Value = "+164" },
                new UiFigureModel { Label = "Reach", Value = "11,900" },
                new UiFigureModel { Label = "Rating", Value = "4.3 / 5" },
            },
            Side = new[]
            {
                new UiPairModel { Key = "Visits from posts", Value = "312" },
                new UiPairModel { Key = "Takings from posts", Value = "$4,860" },
                new UiPairModel { Key = "Best district", Value = "Midtown" },
            },
        };
        m.FigureCount = m.Figures.Length;
        m.SideCount = m.Side.Length;
        m.Table = new UiTableModel
        {
            Columns = new[] { new UiColumn { Title = "Posts that carried" }, new UiColumn { Title = string.Empty }, new UiColumn { Title = string.Empty, Width = UiTokens.S16 * 3f, Right = true } },
            Rows = new[]
            {
                new[] { "Morning espresso, half price", "Monday", "1,240" },
                new[] { "The new grinder is in", "Tuesday", "860" },
                new[] { "Live music on Friday", "Wednesday", "790" },
                new[] { "Oat milk now free", "Thursday", "530" },
                new[] { "Closed for the parade", "Friday", "210" },
                new[] { "Our first birthday", "Saturday", "1,870" },
            },
            RowCount = 6,
            Selected = 0,
            EmptyTitle = t("reallife_ui_page_empty", "Nothing here yet"),
        };
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.TabPrev, Label = t("reallife_ui_prompt_page", "Page") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>
    /// A game studio's whole page as StudioPages makes it from StudioViews.Page, a studio well into the game: what to do
    /// next and its actions, its games out, copies and 30 days' takings, its games and the games market's newest, its
    /// team with the group it lacks, and its sales and support.
    /// </summary>
    public static UiDashboardModel StudioFirm(UiWords t)
    {
        var m = new UiDashboardModel
        {
            Title = "Lantern Bay Games",
            Subtitle = "Next: start a game. Draft a concept, then pitch it at your desk at Lantern Bay Games.",
            SubtitleInk = UiRole.Text,
            StatsFirst = true,
            Figures = new[]
            {
                new UiFigureModel { Label = t("reallife_studio_page_games_out", "Games out"), Value = "4" },
                new UiFigureModel { Label = t("reallife_studio_page_copies", "Copies sold"), Value = "86,400" },
                new UiFigureModel
                {
                    Label = t("reallife_studio_page_takings", "Takings, 30 days"), Value = "$184,200", Span = 2,
                    Trend = new[] { 38f, 39f, 41f, 40f, 42f, 44f, 47f, 51f, 49f, 46f, 44f, 43f, 45f, 46f, 44f, 42f, 41f, 43f, 45f, 48f, 50f, 49f, 47f, 46f }
                },
            },
            Table = FirmTable(t, new[] { "reallife_studio_page_col_game", "reallife_studio_page_col_genre", "reallife_studio_page_col_score", "reallife_studio_page_col_copies",
                "reallife_studio_page_col_kept", "reallife_studio_page_col_status" }, new[] { "Game", "Genre", "Score", "Copies", "Kept", "Status" }, 2,
                "reallife_studio_page_no_games", "No game yet. Draft a concept, then pitch it at your desk.", new[]
                {
                    new[] { "Cinder Valley", "Simulation", "", "", "", "64% made" },
                    new[] { "Ember Isle", "Adventure", "78", "41,000", "$612,400", "96 days" },
                    new[] { "Tiny Towns", "Simulation", "74", "28,500", "$341,900", "212 days" },
                    new[] { "Deep Signal", "Strategy", "69", "12,600", "$128,300", "340 days" },
                    new[] { "Pocket Courts", "Sports", "58", "4,300", "$31,200", "Withdrawn" },
                }),
            More = FirmTable(t, new[] { "reallife_studio_page_col_rival", "reallife_studio_page_col_studio", "reallife_studio_page_col_genre", "reallife_studio_page_col_score",
                "reallife_studio_page_col_copies" }, new[] { "Rival game", "Studio", "Genre", "Score", "Copies" }, 3, "reallife_studio_studio_rivals_none", "No rival game is out yet.", new[]
                {
                    new[] { "Iron Tide", "Pixel Harbour Games", "Action", "82", "41,000" },
                    new[] { "Lantern Puzzles", "Copperkettle Games", "Puzzle", "71", "12,300" },
                    new[] { "Harbour Days", "Tidewater Interactive", "Simulation", "76", "28,900" },
                    new[] { "The Quiet House", "Blue Kestrel Games", "Horror", "68", "9,400" },
                    new[] { "Crown and Ledger", "Saltmarsh Studios", "Strategy", "84", "57,200" },
                    new[] { "Ashen Road", "Hollowpine Interactive", "Role-playing", "79", "33,600" },
                    new[] { "Paper Kites", "Gilded Quill Games", "Family", "66", "14,800" },
                    new[] { "Signal Lost", "Pixel Harbour Games", "Shooter", "74", "36,100" },
                    new[] { "Tile Garden", "Copperkettle Games", "Casual", "62", "8,700" },
                    new[] { "Northern Line", "Tidewater Interactive", "Adventure", "70", "19,500" },
                    new[] { "Mire", "Blue Kestrel Games", "Puzzle", "65", "7,200" },
                    new[] { "Old Kingdoms", "Saltmarsh Studios", "Role-playing", "81", "48,800" },
                }),
            StatsTitle = t("reallife_studio_page_team", "Team"),
            Stats = new[]
            {
                Group(t, "reallife_studio_page_group_engineers", "Engineers", 5, 64),
                Group(t, "reallife_studio_page_group_designers", "Designers", 3, 58),
                Group(t, "reallife_studio_page_group_testers", "Testers", 2, 52),
                Group(t, "reallife_studio_page_group_product", "Product", 1, 61),
                Group(t, "reallife_studio_page_group_support", "Support", 0, 0),
            },
            SideTitle = t("reallife_studio_page_facts", "Sales and support"),
            Side = new[]
            {
                new UiPairModel { Key = t("reallife_studio_page_tickets", "Tickets a day"), Value = "120, 0 answered" },
                new UiPairModel { Key = t("reallife_studio_page_pirated", "Pirated copies"), Value = "9,800" },
                new UiPairModel { Key = t("reallife_studio_page_drm", "DRM"), Value = "On in 1 of 3" },
                new UiPairModel { Key = t("reallife_studio_page_asks", "Fan requests"), Value = "new levels, co-op play" },
                new UiPairModel { Key = t("reallife_studio_page_engine", "Engine"), Value = "Under licence" },
                new UiPairModel { Key = t("reallife_studio_page_trend", "Hot genre"), Value = "Strategy until Day 61" },
                new UiPairModel { Key = t("reallife_studio_page_known", "Team knows"), Value = "Simulation, Adventure, Strategy" },
            },
            Others = new[] { new UiActionModel { Label = "Build our own engine", Kind = UiButtonKind.Secondary, Key = UiAction.Option, Command = UiDashboard.OtherCommand } },
            HasAction = true,
            Action = new UiActionModel { Label = "Start a game", Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiDashboard.ActionCommand },
        };
        return Counted(m, t, "Start a game");
    }

    /// <summary>
    /// Another tech firm's whole page as StudioPages makes it from SectorViews.Page, a software firm with a product ready:
    /// what to do next in its good news's colour, its products, customers and takings, its products and the rivals in its
    /// markets, its team with the group it lacks, and its markets, support and limits.
    /// </summary>
    public static UiDashboardModel TechFirm(UiWords t)
    {
        var m = new UiDashboardModel
        {
            Title = "Signal Works",
            Subtitle = "Ledger Cloud can launch. Launch it and it starts selling.",
            SubtitleInk = UiRole.OkText,
            StatsFirst = true,
            Figures = new[]
            {
                new UiFigureModel { Label = t("reallife_tech_mkt_page_products", "Products"), Value = "3" },
                new UiFigureModel { Label = t("reallife_tech_mkt_page_customers", "Customers"), Value = "18,240" },
                new UiFigureModel
                {
                    Label = t("reallife_tech_mkt_page_takings", "Takings, 30 days"), Value = "$96,400", Span = 2,
                    Trend = new[] { 18f, 18.5f, 19f, 19.4f, 19.2f, 19.8f, 20.4f, 21f, 21.3f, 21.1f, 21.6f, 22f, 22.4f, 22.2f, 22.8f, 23.1f, 23.4f, 23.2f, 23.8f, 24.1f, 24.4f, 24.2f, 24.6f, 24.9f }
                },
            },
            Table = FirmTable(t, new[] { "reallife_tech_mkt_page_col_product", "reallife_tech_mkt_page_col_kind", "reallife_tech_mkt_page_col_customers", "reallife_tech_mkt_page_col_takings",
                "reallife_tech_mkt_page_col_status" }, new[] { "Product", "Kind", "Customers", "Takings", "Status" }, 2, "reallife_tech_mkt_page_no_products", "No product yet.", new[]
                {
                    new[] { "Ledgerly", "Subscription service", "12,400", "$412,000", "184 days" },
                    new[] { "Pocket Ledger", "Phone app", "5,840", "$61,300", "96 days" },
                    new[] { "Ledger Cloud", "Subscription service", "", "", "Being built" },
                }),
            More = FirmTable(t, new[] { "reallife_tech_mkt_page_col_rival", "reallife_tech_mkt_page_col_market", "reallife_tech_mkt_page_col_customers",
                "reallife_tech_mkt_page_col_features" }, new[] { "Rival", "Market", "Customers", "Features" }, 2, "reallife_tech_mkt_page_no_rivals", "No rival sells in these markets yet.", new[]
                {
                    new[] { "Skyvault Desk", "Business software", "38,200", "14" },
                    new[] { "Abacus Books", "Business software", "26,900", "12" },
                    new[] { "Tallyrow Insights", "Business software", "19,400", "11" },
                    new[] { "Shopfront Pay", "Apps", "17,800", "9" },
                    new[] { "Signpost Local", "Apps", "12,100", "8" },
                    new[] { "Codeharbour CRM", "Business software", "9,600", "10" },
                    new[] { "Ledgerline Billing", "Business software", "7,300", "9" },
                    new[] { "Circuitry Notes", "Apps", "5,900", "7" },
                    new[] { "Fibreway Status", "Apps", "4,200", "6" },
                    new[] { "Quartzline Hours", "Business software", "3,100", "7" },
                    new[] { "Signalhill Rota", "Apps", "2,600", "5" },
                    new[] { "Skyvault Forms", "Apps", "1,900", "5" },
                }),
            StatsTitle = t("reallife_studio_page_team", "Team"),
            Stats = new[]
            {
                Group(t, "reallife_studio_page_group_engineers", "Engineers", 6, 66),
                Group(t, "reallife_studio_page_group_designers", "Designers", 1, 60),
                Group(t, "reallife_studio_page_group_testers", "Testers", 1, 55),
                Group(t, "reallife_studio_page_group_infrastructure", "Infrastructure", 0, 0),
                Group(t, "reallife_studio_page_group_support", "Support", 2, 54),
                Group(t, "reallife_studio_page_group_sales", "Sales", 1, 49),
            },
            SideTitle = t("reallife_tech_mkt_page_facts", "Markets and support"),
            Side = new[]
            {
                new UiPairModel { Key = t("reallife_tech_mkt_market_businesssoftware", "Business software"), Value = "12%, the lead 31%" },
                new UiPairModel { Key = t("reallife_tech_mkt_market_apps", "Apps"), Value = "4%, the lead 22%" },
                new UiPairModel { Key = t("reallife_tech_mkt_page_support", "Support people"), Value = "2 of 3 needed" },
                new UiPairModel { Key = t("reallife_tech_mkt_page_limit", "Products here"), Value = "3 of 16" },
                new UiPairModel { Key = t("reallife_tech_mkt_page_desk", "Desk sessions"), Value = "1 due" },
                new UiPairModel { Key = t("reallife_tech_mkt_page_reputation", "Reputation"), Value = "62%" },
            },
            HasAction = true,
            Action = new UiActionModel { Label = "Launch", Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiDashboard.ActionCommand },
        };
        return Counted(m, t, "Launch");
    }

    // A firm page's table: its columns named by their keys, the figures from a column on to the right, its rows as written.
    private static UiTableModel FirmTable(UiWords t, string[] keys, string[] english, int figuresFrom, string emptyKey, string empty, string[][] rows)
    {
        var columns = new UiColumn[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            bool figure = i >= figuresFrom;
            columns[i] = new UiColumn { Title = t(keys[i], english[i]), Width = figure ? UiTokens.S16 * 2f + UiTokens.S4 : 0f, Right = figure };
        }
        return new UiTableModel { Columns = columns, Rows = rows, RowCount = rows.Length, Selected = -1, EmptyTitle = t(emptyKey, empty) };
    }

    // A team group's bar: its name and count, its mean skill, or the word for a group the firm needs and lacks.
    private static UiStatModel Group(UiWords t, string key, string english, int count, int skill)
    {
        string label = t("reallife_studio_page_group_count", "{group} ({count})").Replace("{group}", t(key, english)).Replace("{count}", count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return new UiStatModel { Label = label, Figure = count > 0 ? skill.ToString(System.Globalization.CultureInfo.InvariantCulture) : t("reallife_studio_page_needed", "Needed"), Value = skill };
    }

    // A firm page's counts and prompt bar: move, its primary action, back.
    private static UiDashboardModel Counted(UiDashboardModel m, UiWords t, string primary)
    {
        m.FigureCount = m.Figures.Length;
        m.StatCount = m.Stats.Length;
        m.SideCount = m.Side.Length;
        m.OtherCount = m.Others.Length;
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Alt, Label = primary },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>A desk session's step as ActivityScreen mirrors the game's panel: the step's words, the choices on 1, 2 and 3, Cancel on Esc.</summary>
    public static UiSessionModel DeskPanel(UiWords t)
    {
        var m = new UiSessionModel
        {
            Title = "Weekly team meeting",
            Question = "What is this week's meeting for?",
            Choices = new[]
            {
                new UiChoiceModel { Key = UiAction.Pick1, Name = "Lift morale", Trade = "Burnout eases for the people present; output this week is a little lower." },
                new UiChoiceModel { Key = UiAction.Pick2, Name = "Push output", Trade = "More gets done this week; burnout builds each day they work." },
            },
            Actions = new[]
            {
                new UiActionModel { Label = "Cancel", Kind = UiButtonKind.Secondary, Key = UiAction.Back, Command = UiSessionSteps.ActionCommand },
            },
        };
        m.ChoiceCount = m.Choices.Length;
        m.ActionCount = m.Actions.Length;
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_choose", "Choose") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>The population's city view as PopulationPages makes it from PopulationViews.City: the residents' city at a glance.</summary>
    public static UiDashboardModel PopulationCity(UiWords t)
    {
        var m = new UiDashboardModel
        {
            Title = "The city",
            Subtitle = "Day 47 · 8,230 households",
            SideTitle = t("reallife_ui_page_details", "Details"),
            Figures = new[]
            {
                new UiFigureModel { Label = "People", Value = "16,040" },
                new UiFigureModel { Label = "Households", Value = "8,230" },
                new UiFigureModel { Label = "In work", Value = "93.6%" },
                new UiFigureModel { Label = "Spent today", Value = "$1.42M" },
            },
            Side = new[]
            {
                new UiPairModel { Key = "Working class", Value = "41%" },
                new UiPairModel { Key = "Middle class", Value = "44%" },
                new UiPairModel { Key = "Upper class", Value = "15%" },
                new UiPairModel { Key = "Taxes today", Value = "$212,400" },
            },
            HasAction = true,
            Action = new UiActionModel { Label = "Who contributes", Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiDashboard.ActionCommand },
        };
        m.FigureCount = m.Figures.Length;
        m.SideCount = m.Side.Length;
        m.Table = new UiTableModel
        {
            Columns = new[]
            {
                new UiColumn { Title = "Districts" },
                new UiColumn { Title = string.Empty },
                new UiColumn { Title = string.Empty, Width = UiTokens.S16 * 3f, Right = true },
            },
            Rows = new[]
            {
                new[] { "Midtown", "Upper and middle", "3,610" },
                new[] { "Murray Hill", "Middle", "2,480" },
                new[] { "Garment District", "Working", "1,950" },
                new[] { "Hell's Kitchen", "Middle", "2,120" },
                new[] { "Lower Manhattan", "Working", "1,890" },
                new[] { "Industry City", "Working", "1,740" },
                new[] { "The Hamptons", "Upper", "310" },
                new[] { "Uptown", "Middle", "1,120" },
                new[] { "Cedar Heights", "Working", "820" },
            },
            RowCount = 9,
            Selected = 0,
            EmptyTitle = t("reallife_ui_page_empty", "Nothing here yet"),
        };
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.Alt, Label = "Who contributes" },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>The wireframe's Contacts with Maya Okafor selected, as the kit's list-and-detail template draws it.</summary>
    public static UiListDetailModel Contacts(UiWords t, bool composer)
    {
        var m = new UiListDetailModel
        {
            AppName = t("reallife_ui_contacts_app", "Contacts"),
            GroupsLabel = t("reallife_ui_contacts_groups", "Groups"),
            PlayerName = "Richy Man",
            PlayerHue = 40f,
            PlayerSummary = "13 contacts · 5 unread",
            ListTitle = t("reallife_ui_contacts_all", "All contacts"),
            ListCount = "13",
            SearchPlaceholder = t("reallife_ui_contacts_search", "Search people, shops, agencies"),
            SearchCount = "13",
            ListEmptyTitle = t("reallife_ui_contacts_none_title", "No contact matches"),
            ListEmptyBody = t("reallife_ui_contacts_none_body", "Try another name, or clear the search."),
            HasDetail = true,
            Name = "Maya Okafor",
            Hue = 12f,
            Role = "Customer Service · Bloom Café",
            ThreadEmptyTitle = t("reallife_ui_contacts_quiet_title", "Quiet line"),
            ThreadEmptyBody = t("reallife_ui_contacts_quiet_body", "No messages yet. They are still saved in your contacts."),
            ThreadStart = t("reallife_ui_contacts_start", "Start of your messages with {name}").Replace("{name}", "Maya Okafor"),
            ClassicLabel = t("reallife_ui_contacts_classic", "Answer in the game's view"),
            Composer = composer,
            ComposerPlaceholder = t("reallife_ui_contacts_message", "Message Maya"),
            SendLabel = t("reallife_ui_contacts_send", "Send"),
            ServicesTitle = t("reallife_ui_contacts_requests", "Requests and actions"),
            StatsTitle = t("reallife_ui_contacts_profile", "Profile"),
            DetailsTitle = t("reallife_ui_contacts_details", "Details"),
            NoDetailTitle = t("reallife_ui_contacts_pick_title", "Pick a contact"),
            NoDetailBody = t("reallife_ui_contacts_pick_body", "Their messages, actions and details show here."),
        };
        m.Groups = new[]
        {
            Nav(t("reallife_ui_contacts_all", "All contacts"), "13", 12f, true),
            Nav(t("contactcategoryname_general", "General"), "3", 250f, false),
            Nav(t("contactcategoryname_business", "Business"), "4", 320f, false),
            Nav(t("contactcategoryname_employees", "Employees"), "3", 155f, false),
            Nav(t("contactcategoryname_importsandgoods", "Imports / Goods"), "1", 75f, false),
            Nav(t("contactcategoryname_furnitureandequipment", "Furniture / Equipment"), "1", 45f, false),
            Nav(t("contactcategoryname_rivals", "Rivals"), "1", 25f, false),
        };
        m.GroupCount = m.Groups.Length;
        m.SideActions = new[]
        {
            new UiActionModel { Label = t("contacts_mark_all_as_read_button", "Mark all as read"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Check, Key = UiAction.Count, Command = 0 },
            new UiActionModel { Label = t("contacts_delete_all_button", "Delete all"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Trash, Key = UiAction.Count, Command = 1 },
        };
        m.SideActionCount = m.SideActions.Length;
        // The unread first, as the game lists them, then the rest: the two blocks the real screen draws.
        m.Rows = new[]
        {
            Row("Luis Romero", "Van needs fuel before the afternoon run. Okay to use the company card?", "10:02", 1, 155f, true, false, false, t("reallife_ui_contacts_new", "New messages")),
            Row("Uncle Fred", "Good. Once you’ve got keys, open your first shop. I’ll check in.", "09:02", 1, 40f, true, false, false, null),
            Row("Anderson Recruitment Corp.", "Shall I send the shortlist over?", "08:52", 2, 320f, true, true, false, null),
            Row("City Office", "Fine of $125 deducted from your bank account.", "07:40", 1, 300f, false, true, false, null),
            Row("Maya Okafor", "Happy to cover Tuesday evenings instead.", "09:15", 0, 12f, true, false, true, t("reallife_ui_contacts_earlier", "Earlier")),
            Row("NY AutoTow Service", "Stuck somewhere? One call and we’ll haul you out, any hour.", "Sun", 0, 230f, true, false, false, null),
            Row("City Workforce Inc.", "Your HQ application is approved. Let us know which roles to fill.", "Thu", 0, 275f, true, false, false, null),
            Row("CityAds", "Your billboard on 7th Ave ends Friday. Renew?", "Wed", 0, 350f, false, false, false, null),
            Row("McCain’s eMarketing", "Click-through on your last campaign was up 14%.", "Mon", 0, 200f, true, false, false, null),
            Row("Hana Sato", "Requesting a better desk chair. My back is filing a complaint.", "Mon", 0, 180f, false, false, false, null),
            Row("Harbor Line Wholesale", "Container of coffee beans landed. Your Monday delivery is on track.", "Sat", 0, 75f, true, false, false, null),
            Row("Lumen & Oak Interiors", "The walnut counters you asked about are back in stock.", "Fri", 0, 50f, true, false, false, null),
            Row("Vex Holdings", "Cute little café. Shame if the block got crowded.", "Thu", 0, 25f, false, false, false, null),
        };
        m.RowCount = m.Rows.Length;
        m.Actions = new[]
        {
            new UiActionModel { Label = t("phone_view_on_map_button", "View on map"), Kind = UiButtonKind.Secondary, Icon = UiIcon.MapPin, Key = UiAction.Option, Command = 0 },
            new UiActionModel { Label = t("phone_call_button", "Call"), Kind = UiButtonKind.Primary, Icon = UiIcon.Phone, Key = UiAction.Alt, Command = 1 },
        };
        m.ActionCount = m.Actions.Length;
        m.Messages = new[]
        {
            new UiMessageModel { From = UiFrom.Them, Text = "Morning! Café’s busy, the new grinder is great.", Time = "07:58" },
            new UiMessageModel { From = UiFrom.Them, Text = "Could I drop the Sunday shift? I’ve picked up a class on weekends.", Time = "09:14" },
            new UiMessageModel { From = UiFrom.Them, Text = "Happy to cover Tuesday evenings instead.", Time = "09:15" },
            new UiMessageModel { From = UiFrom.Me, Text = "Let me look at the rota tonight.", Time = "09:20" },
        };
        m.MessageCount = m.Messages.Length;
        m.Replies = new[] { "Approved", "Let’s talk Friday", "Thanks, Maya" };
        m.ReplyCount = m.Replies.Length;
        m.Services = new[]
        {
            new UiServiceModel { Title = "Approve schedule change", Price = t("reallife_ui_price_free", "Free"), About = "Remove her Sunday shift." },
            new UiServiceModel { Title = "Pay a bonus", Price = "$200", About = "Raises satisfaction for a few weeks." },
            new UiServiceModel { Title = "Negotiate salary", Price = t("reallife_ui_price_quote", "Quote"), About = "Currently $22/h. She’s asking for $24/h." },
        };
        m.ServiceCount = m.Services.Length;
        m.Stats = new[]
        {
            new UiStatModel { Label = "Skill", Figure = "82", Value = 82 },
            new UiStatModel { Label = "Satisfaction", Figure = "64", Value = 64 },
            new UiStatModel { Label = "Energy", Figure = "48", Value = 48 },
        };
        m.StatCount = m.Stats.Length;
        m.Details = new[]
        {
            new UiPairModel { Key = t("common_address", "Address"), Value = "14 Bleecker St" },
            new UiPairModel { Key = t("myemployees_hourlywage", "Hourly wage"), Value = "$22" },
            new UiPairModel { Key = t("contacts_schedule_demands", "Schedule demands"), Value = "No Sundays" },
        };
        m.DetailCount = m.Details.Length;
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.TabPrev, Label = t("reallife_ui_prompt_group", "Group") },
            new UiPrompt { Action = UiAction.Alt, Label = t("phone_call_button", "Call") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>The gallery on one of its pages: the screen's own model, so the previews and the checks see what the game shows.</summary>
    public static UiGalleryModel Gallery(UiWords t, int page) => UiGalleryScreen.Model(t, page, Dollars);

    /// <summary>Money as the game writes it in English: a dollar sign and thousands.</summary>
    public static string Dollars(float amount) => "$" + amount.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A dashboard of the city's people, the shape the Economy app's population view takes.</summary>
    public static UiDashboardModel Dashboard(UiWords t)
    {
        var m = new UiDashboardModel
        {
            Title = t("reallife_ui_sample_city", "The city"),
            Subtitle = "Day 47 · Autumn",
            Tabs = new[] { t("reallife_ui_gallery_tab_city", "City"), t("reallife_ui_gallery_tab_district", "District"), t("reallife_ui_gallery_tab_people", "People") },
            Figures = SampleFigures(t),
            TableTitle = t("reallife_ui_sample_districts", "Districts"),
            Table = SampleTable(t),
            SideTitle = t("reallife_ui_sample_share", "Your share"),
            Side = new[]
            {
                new UiPairModel { Key = "People employed", Value = "1,240" },
                new UiPairModel { Key = "Share of spending", Value = "6.2%" },
                new UiPairModel { Key = "Taxes paid", Value = "$41,200" },
                new UiPairModel { Key = "Customers a day", Value = "3,860" },
            },
            StatsTitle = t("reallife_ui_sample_mood", "How people feel"),
            Stats = new[]
            {
                new UiStatModel { Label = "Satisfaction", Figure = "71", Value = 71 },
                new UiStatModel { Label = "Rent against pay", Figure = "38", Value = 38 },
                new UiStatModel { Label = "Safety", Figure = "64", Value = 64 },
                new UiStatModel { Label = "Getting about", Figure = "82", Value = 82 },
            },
        };
        m.FigureCount = m.Figures.Length;
        m.SideCount = m.Side.Length;
        m.StatCount = m.Stats.Length;
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.TabPrev, Label = t("reallife_ui_prompt_page", "Page") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>A desk session's first step: the weekly team meeting's aim.</summary>
    public static UiSessionModel Session(UiWords t)
    {
        var m = new UiSessionModel
        {
            Title = "Weekly team meeting",
            About = "Twenty minutes with the team at Pixel Harbour Games: lift their morale, or push this week's output.",
            StepLabel = t("reallife_ui_gallery_step", "Step 1 of 2"),
            StepCount = 2,
            Step = 0,
            Question = "What is this week's meeting for?",
            Choices = new[]
            {
                new UiChoiceModel { Key = UiAction.Pick1, Name = "Lift morale", Trade = "Burnout eases for the people present. Output this week is a little lower.", Note = "20 min", Selected = true },
                new UiChoiceModel { Key = UiAction.Pick2, Name = "Push output", Trade = "More gets done this week. Burnout builds each day they work.", Note = "20 min" },
                new UiChoiceModel { Key = UiAction.Pick3, Name = "Hear them out", Trade = "No change to output. You learn who is close to leaving.", Note = "30 min" },
            },
            SummaryTitle = t("reallife_ui_sample_today", "Today"),
            Summary = new[]
            {
                new UiPairModel { Key = "Team", Value = "12 people" },
                new UiPairModel { Key = "Morale", Value = "61" },
                new UiPairModel { Key = "Burnout", Value = "Rising" },
            },
            Actions = new[]
            {
                new UiActionModel { Label = t("reallife_session_button_hand", "Hand to a manager"), Kind = UiButtonKind.Secondary, Key = UiAction.Option, Command = 0 },
                new UiActionModel { Label = t("reallife_session_button_begin", "Begin"), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = 1 },
            },
        };
        m.ChoiceCount = m.Choices.Length;
        m.SummaryCount = m.Summary.Length;
        m.ActionCount = m.Actions.Length;
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_choose", "Choose") },
            new UiPrompt { Action = UiAction.Alt, Label = t("reallife_session_button_begin", "Begin") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>A form: a campaign's settings before it is ordered.</summary>
    public static UiFormModel Form(UiWords t)
    {
        var m = new UiFormModel
        {
            Title = "Billboard campaign",
            About = "CityAds puts your shop on the billboards near it. More people walk past while it runs.",
            Fields = new[]
            {
                new UiFieldModel { Kind = UiFieldKind.Choice, Section = "Where", Label = "Billboards", Value = "Near Bloom Café" },
                new UiFieldModel { Kind = UiFieldKind.Slider, Label = "Weeks", Value = "4", Share = 0.5f },
                new UiFieldModel { Kind = UiFieldKind.Toggle, Section = "Options", Label = "Renew when it ends", On = true },
                new UiFieldModel { Kind = UiFieldKind.Toggle, Label = "Tell me each week", On = false },
                new UiFieldModel { Kind = UiFieldKind.Pair, Section = "Cost", Label = "A week", Value = "$2,000" },
                new UiFieldModel { Kind = UiFieldKind.Pair, Label = "In all", Value = "$8,000" },
            },
            Actions = new[]
            {
                new UiActionModel { Label = t("common_cancel", "Cancel"), Kind = UiButtonKind.Secondary, Key = UiAction.Back, Command = 0 },
                new UiActionModel { Label = t("reallife_ui_sample_order", "Order"), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = 1 },
            },
        };
        m.FieldCount = m.Fields.Length;
        m.ActionCount = m.Actions.Length;
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_change", "Change") },
            new UiPrompt { Action = UiAction.Alt, Label = t("reallife_ui_sample_order", "Order") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    private static UiFigureModel[] SampleFigures(UiWords t)
    {
        return new[]
        {
            new UiFigureModel { Label = t("reallife_ui_sample_people", "People"), Value = "62,800", Change = "+1.8%", Rising = true, Good = true, Trend = new[] { 60.1f, 60.6f, 61.0f, 61.5f, 61.4f, 62.2f, 62.8f } },
            new UiFigureModel { Label = t("reallife_ui_sample_jobs", "Jobs"), Value = "79,440", Change = "+0.6%", Rising = true, Good = true, Trend = new[] { 78.2f, 78.7f, 78.5f, 79.0f, 79.2f, 79.4f } },
            new UiFigureModel { Label = t("reallife_ui_sample_unemployed", "Out of work"), Value = "6.4%", Change = "+0.3", Rising = true, Good = false, Trend = new[] { 5.6f, 5.9f, 6.1f, 6.0f, 6.2f, 6.4f } },
            new UiFigureModel { Label = t("reallife_ui_sample_rent", "Average rent"), Value = "$2,140", Change = "+2.1%", Rising = true, Good = false, Trend = new[] { 2.0f, 2.02f, 2.05f, 2.09f, 2.12f, 2.14f } },
        };
    }

    private static UiTableModel SampleTable(UiWords t)
    {
        return new UiTableModel
        {
            Columns = new[]
            {
                new UiColumn { Title = t("reallife_ui_sample_district", "District") },
                new UiColumn { Title = t("reallife_ui_sample_people", "People"), Width = 120f, Right = true },
                new UiColumn { Title = t("reallife_ui_sample_jobs", "Jobs"), Width = 120f, Right = true },
                new UiColumn { Title = t("reallife_ui_sample_rent", "Average rent"), Width = 160f, Right = true },
            },
            Rows = new[]
            {
                new[] { t("ba:neighborhood_midtown", "Midtown"), "14,210", "28,400", "$2,620" },
                new[] { t("ba:neighborhood_murrayhill", "Murray Hill"), "9,880", "7,100", "$2,280" },
                new[] { t("ba:neighborhood_garmentdistrict", "Garment District"), "6,040", "11,900", "$1,960" },
                new[] { t("ba:neighborhood_hellskitchen", "Hell's Kitchen"), "8,310", "6,200", "$2,050" },
                new[] { t("ba:neighborhood_lowermanhattan", "Lower Manhattan"), "7,450", "9,800", "$1,880" },
                new[] { t("ba:neighborhood_industrycity", "Industry City"), "5,960", "8,700", "$1,420" },
                new[] { t("ba:neighborhood_thehamptons", "The Hamptons"), "1,180", "640", "$4,900" },
                new[] { t("reallife_neighborhood_uptown", "Uptown"), "5,200", "3,900", "$1,740" },
                new[] { t("reallife_neighborhood_cedarheights", "Cedar Heights"), "4,570", "2,800", "$1,610" },
            },
            RowCount = 9,
            Selected = 0,
            EmptyTitle = t("reallife_ui_sample_none", "Nothing here yet"),
            EmptyBody = t("reallife_ui_sample_none_body", "The city has no figures for this yet."),
        };
    }

    private static UiNavModel Nav(string label, string count, float hue, bool selected) => new UiNavModel { Label = label, Count = count, Hue = hue, Selected = selected };

    private static UiRowModel Row(string name, string preview, string time, int unread, float hue, bool online, bool verified, bool selected, string section)
    {
        return new UiRowModel { Name = name, Preview = preview, Time = time, Unread = unread, Hue = hue, Online = online, Verified = verified, Selected = selected, Section = section };
    }
}
