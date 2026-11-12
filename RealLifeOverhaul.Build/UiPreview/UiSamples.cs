using System;
using RealLifeOverhaul.Ui;

// The kit's sample view models: the owner's Contacts wireframe's own people and words (not-in-repo/design/wireframes,
// contacts_source.html), for the offline previews and EconomyCheck's layout checks. Fixtures, never shipped: the mod's
// screens build their models from the game's data and the locale keys. Words a screen labels with go through the given
// text source, so the checks measure every language; the fixtures' names and messages stay as the wireframe has them.
internal static partial class UiSamples
{
    /// <summary>The masthead's money, day and time in every preview and check: the game's own bar writes them so.</summary>
    public static string Status => "$48,210     " + DayOf(SampleWords, 1, 412) + "     14:32";

    /// <summary>The language the sample status bars are written in: the runner or a check sets it for a shot, English otherwise.</summary>
    public static UiWords SampleWords = (key, english) => english;

    private static readonly string[] WeekdayKeys =
        { "common_monday", "common_tuesday", "common_wednesday", "common_thursday", "common_friday", "common_saturday", "common_sunday" };
    private static readonly string[] WeekdayEnglish = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
    private static readonly string[] SeasonKeys =
        { "reallife_season_name_winter", "reallife_season_name_spring", "reallife_season_name_summer", "reallife_season_name_autumn" };
    private static readonly string[] SeasonEnglish = { "Winter", "Spring", "Summer", "Autumn" };

    /// <summary>A weekday (0 Monday) and day number as the game's top bar writes them, in the language's own words.</summary>
    internal static string DayOf(UiWords t, int weekday, int day) =>
        t("topbar_date_format", "{DayOfWeek} (Day {CurrentNumberDay})")
            .Replace("{DayOfWeek}", t(WeekdayKeys[weekday], WeekdayEnglish[weekday])).Replace("{CurrentNumberDay}", day.ToString());

    /// <summary>A season (0 winter .. 3 autumn) in the language's own word.</summary>
    internal static string SeasonOf(UiWords t, int season) => t(SeasonKeys[season], SeasonEnglish[season]);

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
            new Sample { Name = "studio", App = "Your firm", Lay = (s, f, d, k) => StudioFace.Lay(s, StudioFirm(t), f, d, k), Walk = "LDDDDRDDLU" },
            new Sample { Name = "studio_team", App = "Your firm", Lay = (s, f, d, k) => StudioFace.Lay(s, StudioTeam(t), f, d, k), Walk = "DDDUL" },
            new Sample { Name = "studio_empty", App = "Your firm", Lay = (s, f, d, k) => StudioFace.Lay(s, StudioEmpty(t), f, d, k), Walk = "DLR" },
            new Sample { Name = "tech_firm", App = "Your firm", Lay = (s, f, d, k) => StudioFace.Lay(s, TechFirm(t), f, d, k), Walk = "DDDDRDDLU" },
            new Sample { Name = "session_panel", App = "Desk", Lay = (s, f, d, k) => UiDecision.Lay(s, DeskCard(t), f, d, k), Walk = "RDLUL" },
            new Sample { Name = "session_panel_start", App = "Desk", Lay = (s, f, d, k) => UiDecision.Lay(s, DeskStart(t), f, d, k), Walk = "RDLUL" },
        };
    }

    // How long a game or a product has been out, as the firms' pages write it.
    private static string Out(UiWords t, int days) => t("reallife_studio_page_age_days", "Out {days} days").Replace("{days}", days.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// A game studio's whole page as StudioPages makes it from StudioViews.Page, a studio well into the game: what to do
    /// next and its actions, its games out, copies and 30 days' takings, its games and the games market's newest, its
    /// team with the group it lacks, and its sales and support.
    /// </summary>
    public static StudioFaceModel StudioFirm(UiWords t)
    {
        var m = new StudioFaceModel
        {
            Title = "Lantern Bay Games",
            Next = "Next: start a game. Draft a concept, then pitch it at your desk at Lantern Bay Games.",
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
            Main = FirmTable(t, new[] { "reallife_studio_page_col_game", "reallife_studio_page_col_genre", "reallife_studio_page_col_score", "reallife_studio_page_col_copies",
                "reallife_studio_page_col_kept", "reallife_studio_page_col_status" }, new[] { "Game", "Genre", "Score", "Copies", "Kept", "Status" }, 2,
                "reallife_studio_page_no_games", "No game yet. Draft a concept, then pitch it at your desk.", UiIcon.GameController, new[]
                {
                    new[] { "Cinder Valley", "Simulation", "", "", "", "64% made" },
                    new[] { "Ember Isle", "Adventure", "78", "41,000", "$612,400", Out(t, 96) },
                    new[] { "Tiny Towns", "Simulation", "74", "28,500", "$341,900", Out(t, 212) },
                    new[] { "Deep Signal", "Strategy", "69", "12,600", "$128,300", Out(t, 340) },
                    new[] { "Pocket Courts", "Sports", "58", "4,300", "$31,200", "Withdrawn" },
                }),
            More = FirmTable(t, new[] { "reallife_studio_page_col_rival", "reallife_studio_page_col_studio", "reallife_studio_page_col_genre", "reallife_studio_page_col_score",
                "reallife_studio_page_col_copies" }, new[] { "Rival game", "Studio", "Genre", "Score", "Copies" }, 3, "reallife_studio_studio_rivals_none",
                "No rival game is out yet.", UiIcon.Flag, new[]
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
            Team = TeamOf(t, Group(t, "reallife_studio_page_group_engineers", "Engineers", 5, 64), Group(t, "reallife_studio_page_group_designers", "Designers", 3, 58),
                Group(t, "reallife_studio_page_group_testers", "Testers", 2, 52), Group(t, "reallife_studio_page_group_product", "Product", 1, 61),
                Group(t, "reallife_studio_page_group_support", "Support", 0, 0)),
            Note = Needed(t, true),
            Facts = FactsOf(t, "reallife_studio_page_facts", "Sales and support",
                ("reallife_studio_page_tickets", "Tickets a day", "120, 0 answered"), ("reallife_studio_page_pirated", "Pirated copies", "9,800"),
                ("reallife_studio_page_drm", "DRM", "On in 1 of 3"), ("reallife_studio_page_asks", "Fan requests", "New levels, co-op play"),
                ("reallife_studio_page_engine", "Engine", "Under licence"), ("reallife_studio_page_trend", "Hot genre", "Strategy until Day 61"),
                ("reallife_studio_page_known", "Team knows", "Simulation, Adventure, Strategy")),
            Actions = new[]
            {
                new UiActionModel { Label = "Build our own engine", Kind = UiButtonKind.Secondary, Key = UiAction.Option, Command = StudioFace.ActionCommand, Arg = 0 },
                new UiActionModel { Label = "Start a game", Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = StudioFace.ActionCommand, Arg = 1 },
            },
        };
        Extras(m, t, true);
        return Prompted(m, t, "Start a game");
    }


    /// <summary>The same studio's Team view: its people with their skill and morale, the note about what the team lacks beside them.</summary>
    public static StudioFaceModel StudioTeam(UiWords t)
    {
        StudioFaceModel m = StudioFirm(t);
        m.Tab = 1;
        return m;
    }
    /// <summary>
    /// A new studio as the owner opened it in beta 3: nobody in it, no game, nothing taken. Its next step is to hire, each
    /// needed group says how to fill it, its takings draw no flat line, and its facts say None in one case.
    /// </summary>
    public static StudioFaceModel StudioEmpty(UiWords t)
    {
        var m = new StudioFaceModel
        {
            Title = "Pioneer Game studio",
            Next = t("reallife_studio_next_hire", "Next: hire engineers and designers for {studio}, so it can start a game.").Replace("{studio}", "Pioneer Game studio"),
            NextInk = UiRole.WarnText,
            Figures = new[]
            {
                new UiFigureModel { Label = t("reallife_studio_page_games_out", "Games out"), Value = "0" },
                new UiFigureModel { Label = t("reallife_studio_page_copies", "Copies sold"), Value = "0" },
                new UiFigureModel { Label = t("reallife_studio_page_takings", "Takings, 30 days"), Value = "$0", Span = 2, Trend = new float[24] },
            },
            Main = FirmTable(t, new[] { "reallife_studio_page_col_game", "reallife_studio_page_col_genre", "reallife_studio_page_col_score", "reallife_studio_page_col_copies",
                "reallife_studio_page_col_kept", "reallife_studio_page_col_status" }, new[] { "Game", "Genre", "Score", "Copies", "Kept", "Status" }, 2,
                "reallife_studio_page_no_games_hire", "No game yet. Hire a team first, then draft a concept.", UiIcon.GameController, new string[0][]),
            More = FirmTable(t, new[] { "reallife_studio_page_col_rival", "reallife_studio_page_col_studio", "reallife_studio_page_col_genre", "reallife_studio_page_col_score",
                "reallife_studio_page_col_copies" }, new[] { "Rival game", "Studio", "Genre", "Score", "Copies" }, 3, "reallife_studio_studio_rivals_none",
                "No rival game is out yet.", UiIcon.Flag, new string[0][]),
            Team = TeamOf(t, Group(t, "reallife_studio_page_group_engineers", "Engineers", 0, 0), Group(t, "reallife_studio_page_group_designers", "Designers", 0, 0)),
            Note = Needed(t),
            Facts = FactsOf(t, "reallife_studio_page_facts", "Sales and support",
                ("reallife_studio_page_tickets", "Tickets a day", "None"), ("reallife_studio_page_pirated", "Pirated copies", "0"), ("reallife_studio_page_drm", "DRM", "Off"),
                ("reallife_studio_page_asks", "Fan requests", "None"), ("reallife_studio_page_engine", "Engine", "Under licence"), ("reallife_studio_page_trend", "Hot genre", "None"),
                ("reallife_studio_page_known", "Team knows", "None")),
            Actions = new[] { new UiActionModel { Label = "Build our own engine", Kind = UiButtonKind.Secondary, Key = UiAction.Option, Command = StudioFace.ActionCommand, Arg = 0 } },
        };
        return Prompted(m, t, null);
    }

    /// <summary>
    /// Another tech firm's whole page as StudioPages makes it from SectorViews.Page, a software firm with a product ready:
    /// what to do next in its good news's colour, its products, customers and takings, its products and the rivals in its
    /// markets, its team with the group it lacks, and its markets, support and limits.
    /// </summary>
    public static StudioFaceModel TechFirm(UiWords t)
    {
        var m = new StudioFaceModel
        {
            Title = "Signal Works",
            Next = "Ledger Cloud can launch. Launch it and it starts selling.",
            NextInk = UiRole.OkText,
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
            Main = FirmTable(t, new[] { "reallife_tech_mkt_page_col_product", "reallife_tech_mkt_page_col_kind", "reallife_tech_mkt_page_col_customers", "reallife_tech_mkt_page_col_takings",
                "reallife_tech_mkt_page_col_status" }, new[] { "Product", "Kind", "Customers", "Takings", "Status" }, 2, "reallife_tech_mkt_page_no_products", "No product yet.",
                UiIcon.Package, new[]
                {
                    new[] { "Ledgerly", "Subscription service", "12,400", "$412,000", Out(t, 184) },
                    new[] { "Pocket Ledger", "Phone app", "5,840", "$61,300", Out(t, 96) },
                    new[] { "Ledger Cloud", "Subscription service", "", "", t("reallife_tech_mkt_page_ready", "Ready to launch") },
                }),
            More = FirmTable(t, new[] { "reallife_tech_mkt_page_col_rival", "reallife_tech_mkt_page_col_market", "reallife_tech_mkt_page_col_customers",
                "reallife_tech_mkt_page_col_features" }, new[] { "Rival", "Market", "Customers", "Features" }, 2, "reallife_tech_mkt_page_no_rivals",
                "No rival sells in these markets yet.", UiIcon.Flag, new[]
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
            Team = TeamOf(t, Group(t, "reallife_studio_page_group_engineers", "Engineers", 6, 66), Group(t, "reallife_studio_page_group_designers", "Designers", 1, 60),
                Group(t, "reallife_studio_page_group_testers", "Testers", 1, 55), Group(t, "reallife_studio_page_group_infrastructure", "Infrastructure", 0, 0),
                Group(t, "reallife_studio_page_group_support", "Support", 2, 54), Group(t, "reallife_studio_page_group_sales", "Sales", 1, 49)),
            Note = Needed(t),
            Facts = FactsOf(t, "reallife_tech_mkt_page_facts", "Markets and support",
                ("reallife_tech_mkt_market_businesssoftware", "Business software", "12%, the lead 31%"), ("reallife_tech_mkt_market_apps", "Apps", "4%, the lead 22%"),
                ("reallife_tech_mkt_page_support", "Support people", "2 of 3 needed"), ("reallife_tech_mkt_page_limit", "Products here", "3 of 16"),
                ("reallife_tech_mkt_page_desk", "Desk sessions", "1 due"), ("reallife_tech_mkt_page_reputation", "Reputation", "62%")),
            Actions = new[] { new UiActionModel { Label = "Launch", Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = StudioFace.ActionCommand, Arg = 0 } },
        };
        Extras(m, t, false);
        return Prompted(m, t, "Launch");
    }

    // A firm page's table: its columns named by their keys, the figures from a column on to the right, its rows as written.
    private static UiDataTableModel FirmTable(UiWords t, string[] keys, string[] english, int figuresFrom, string emptyKey, string empty, UiIcon icon, string[][] rows)
    {
        var titles = new string[keys.Length];
        var figures = new bool[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            titles[i] = t(keys[i], english[i]);
            figures[i] = i >= figuresFrom;
        }
        return StudioFace.Table(titles, keys, figures, rows, t(emptyKey, empty), icon);
    }

    // A team group's bar: its name and count, its mean skill, or the word for a group the firm needs and lacks.
    private static (string Name, int Skill, string Figure) Group(UiWords t, string key, string english, int count, int skill)
    {
        string label = t("reallife_studio_page_group_count", "{group} ({count})").Replace("{group}", t(key, english)).Replace("{count}", count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return (label, skill, count > 0 ? skill.ToString(System.Globalization.CultureInfo.InvariantCulture) : t("reallife_studio_page_needed", "Needed"));
    }

    private static UiDataTableModel TeamOf(UiWords t, params (string Name, int Skill, string Figure)[] groups)
    {
        var names = new string[groups.Length];
        var skills = new int[groups.Length];
        var figures = new string[groups.Length];
        for (int i = 0; i < groups.Length; i++)
        {
            names[i] = groups[i].Name;
            skills[i] = groups[i].Skill;
            figures[i] = groups[i].Figure;
        }
        return StudioFace.TeamTable(t("reallife_studio_page_team", "Team"), t("reallife_ui_tech_skill", "Skill"), names, skills, figures, t("reallife_studio_page_none", "None"));
    }

    private static UiFactCardModel Needed(UiWords t, bool tickets = false) => StudioFace.NoteCard(
        tickets
            ? new[] { t("reallife_ui_tech_note_support", "Support is needed: {tickets} tickets a day go unanswered. Hire people, then give them roles in Open Roles.").Replace("{tickets}", "120"),
                t("reallife_studio_page_needed_how", "To fill a group, hire people in the Business app, then set their roles in Open Roles, on its Team tab.") }
            : new[] { t("reallife_studio_page_needed_how", "To fill a group, hire people in the Business app, then set their roles in Open Roles, on its Team tab.") },
        t("reallife_ui_tech_open_roles", "Open Roles"));

    // The firm's people as the Tech app's overview writes them, with the in-development hero over the figures.
    private static void Extras(StudioFaceModel m, UiWords t, bool hero)
    {
        var people = new[]
        {
            new UiPersonRow { Name = "Ava Lindqvist", Hue = 0.1f, RoleIcon = UiIcon.Code, Role = "Lead engineer", First = 86, Second = 72 },
            new UiPersonRow { Name = "Marcus Hale", Hue = 0.3f, RoleIcon = UiIcon.Code, Role = "Engineer", First = 64, Second = 58 },
            new UiPersonRow { Name = "Priya Raman", Hue = 0.6f, RoleIcon = UiIcon.PaintBrush, Role = "Game designer", First = 77, Second = 81 },
            new UiPersonRow { Name = "Tom Becker", Hue = 0.8f, RoleIcon = UiIcon.PaintBrush, Role = "Level designer", First = 52, Second = 44 },
            new UiPersonRow { Name = "Elena Sokolova", Hue = 0.45f, RoleIcon = UiIcon.Code, Role = "Tester", First = 61, Second = 66 },
        };
        m.People = StudioFace.PeopleTable(t("reallife_ui_tech_col_person", "Person"), t("reallife_ui_tech_skill", "Skill"), t("reallife_ui_tech_morale", "Morale"), people, "None");
        m.Tabs = StudioFace.TabEntries(t("reallife_ui_tech_sec_overview", "Overview"), t("reallife_ui_tech_sec_team", "Team"), people.Length);
        if (hero)
        {
            m.Hero = new UiHeroModel
            {
                Icon = UiIcon.GenreSimulation, Kicker = t("reallife_ui_tech_in_development", "In development"), Title = "Cinder Valley", Progress = 64,
                Line = "Simulation, 64% made, three of five milestones met", Note = t("reallife_ui_tech_at_desk", "At your desk"), NoteIcon = UiIcon.Locate
            };
        }
    }

    private static UiFactCardModel FactsOf(UiWords t, string titleKey, string title, params (string Key, string English, string Value)[] facts)
    {
        var labels = new string[facts.Length];
        var values = new string[facts.Length];
        for (int i = 0; i < facts.Length; i++)
        {
            labels[i] = t(facts[i].Key, facts[i].English);
            values[i] = facts[i].Value;
        }
        return StudioFace.FactsCard(t(titleKey, title), labels, values);
    }

    // A firm page's prompt bar: move, its primary action, back.
    private static StudioFaceModel Prompted(StudioFaceModel m, UiWords t, string primary)
    {
        var prompts = new System.Collections.Generic.List<UiPrompt> { new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") } };
        if (primary != null)
        {
            prompts.Add(new UiPrompt { Action = UiAction.Alt, Label = primary });
        }
        prompts.Add(new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") });
        m.Prompts = prompts.ToArray();
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
                new UiChoiceModel { Key = UiAction.Pick1, Name = "Lift morale", Trade = "Burnout eases for the people present. Output this week is a little lower." },
                new UiChoiceModel { Key = UiAction.Pick2, Name = "Push output", Trade = "More gets done this week. Burnout builds each day they work." },
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

    /// <summary>The wireframe's Contacts with Maya Okafor selected, as the kit's list-and-detail template draws it.</summary>
    public static UiListDetailModel Contacts(UiWords t, bool composer)
    {
        var m = new UiListDetailModel
        {
            AppName = t("reallife_ui_contacts_app", "Contacts"),
            GroupsLabel = t("reallife_ui_contacts_groups", "Groups"),
            PlayerName = "Alex Carter",
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
            new UiActionModel { Label = t("contacts_mark_all_as_read_button", "Mark all as read"), Kind = UiButtonKind.Secondary, Icon = UiIcon.Check, Key = UiAction.Count,
                Command = UiListDetail.SideAction, Arg = ContactsRoutes.MarkReadArg },
            new UiActionModel { Label = t("contacts_delete_all_button", "Delete all"), Kind = UiButtonKind.Destructive, Icon = UiIcon.Trash, Key = UiAction.Count,
                Command = UiListDetail.SideAction, Arg = ContactsRoutes.DeleteAllArg },
        };
        m.SideActionCount = m.SideActions.Length;
        // The unread first, as the game lists them, then the rest: the two blocks the real screen draws.
        m.Rows = new[]
        {
            Row("Luis Romero", "Van needs fuel before the afternoon run. Okay to use the company card?", "10:02", 1, 155f, true, false, false, t("reallife_ui_contacts_new", "New messages")),
            Row("Uncle Fred", "Proud of you, kid. Dinner on Sunday? Your aunt is making lasagne.", "09:02", 1, 40f, true, false, false, null),
            Row("Anderson Recruitment Corp.", "Shall I send the shortlist over?", "08:52", 2, 320f, true, false, false, null),
            Row("City Office", "Fine of $125 deducted from your bank account.", "07:40", 1, 300f, false, false, false, null),
            Row("Maya Okafor", "You: Let me look at the rota tonight.", "09:20", 0, 12f, true, false, true, t("reallife_ui_contacts_earlier", "Earlier")),
            // Newest first on a Tuesday: yesterday, then each day back, weekday names only for the last six days.
            Row("McCain’s eMarketing", "Click-through on your last campaign was up 14%.", "Mon", 0, 200f, true, false, false, null),
            Row("Hana Sato", "Requesting a better desk chair. My back is filing a complaint.", "Mon", 0, 180f, false, false, false, null),
            Row("NY AutoTow Service", "Stuck somewhere? One call and we’ll haul you out, any hour.", "Sun", 0, 230f, true, false, false, null),
            Row("Harbor Line Wholesale", "Container of coffee beans landed. Your Monday delivery is on track.", "Sat", 0, 75f, true, false, false, null),
            Row("Lumen & Oak Interiors", "The walnut counters you asked about are back in stock.", "Fri", 0, 50f, true, false, false, null),
            Row("City Workforce Inc.", "Your HQ application is approved. Let us know which roles to fill.", "Thu", 0, 275f, true, false, false, null),
            Row("Vex Holdings", "Cute little café. Shame if the block got crowded.", "Thu", 0, 25f, false, false, false, null),
            Row("CityAds", "Your billboard on 7th Ave ends Friday. Renew?", "Wed", 0, 350f, false, false, false, null),
        };
        m.RowCount = m.Rows.Length;
        m.Actions = new[]
        {
            new UiActionModel { Label = t("phone_view_on_map_button", "View on map"), Kind = UiButtonKind.Secondary, Icon = UiIcon.MapPin, Key = UiAction.Option,
                Command = UiListDetail.Action, Arg = ContactsRoutes.MapArg },
            new UiActionModel { Label = t("phone_call_button", "Call"), Kind = UiButtonKind.Primary, Icon = UiIcon.Phone, Key = UiAction.Alt,
                Command = UiListDetail.Action, Arg = ContactsRoutes.CallArg },
        };
        m.ActionCount = m.Actions.Length;
        m.Messages = new[]
        {
            new UiMessageModel { From = UiFrom.Them, Text = "Morning. The café is busy today.", Time = "07:58" },
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
            new UiServiceModel { Title = "Negotiate salary", Price = "$24/h", About = "Currently $22/h. She’s asking for $24/h." },
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

    /// <summary>The board's question to a founder whose firm account is short, as the desk session's one step.</summary>
    public static UiSessionModel BoardSession(UiWords t)
    {
        var m = new UiSessionModel
        {
            Title = "The board's question",
            About = "The investors of a firm have found money missing from its account and ask the founder about it at the founder's desk. Repay what was taken, explain it, or say nothing.",
            StepLabel = t("reallife_ui_gallery_step", "Step 1 of 1"),
            StepCount = 1,
            Step = 0,
            Question = "What do you tell the board?",
            Choices = new[]
            {
                new UiChoiceModel { Key = UiAction.Pick1, Name = "Repay what you can", Trade = "Puts your spare cash back into the firm account and calms the board.", Note = "20 min", Selected = true },
                new UiChoiceModel { Key = UiAction.Pick2, Name = "Explain", Trade = "Believed more easily with Accounting and Law and a small sum. A large sum is not believed.", Note = "30 min" },
                new UiChoiceModel { Key = UiAction.Pick3, Name = "Say nothing", Trade = "Costs nothing now. The board demands the money back within 14 days." },
            },
            SummaryTitle = t("reallife_ui_sample_today", "Today"),
            Summary = new[]
            {
                new UiPairModel { Key = "Firm", Value = "Pioneer Game Studio" },
                new UiPairModel { Key = "Counted as taken", Value = "$360,000" },
                new UiPairModel { Key = "Spare cash", Value = "$120,000" },
            },
            Actions = new[]
            {
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

    /// <summary>A desk session's first step: the weekly team meeting's aim.</summary>
    public static UiSessionModel Session(UiWords t)
    {
        var m = new UiSessionModel
        {
            Title = "Weekly team meeting",
            About = "Up to half an hour with the team at Pixel Harbour Games: lift their morale, push this week's output or hear them out.",
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

    private static UiNavModel Nav(string label, string count, float hue, bool selected) => new UiNavModel { Label = label, Count = count, Hue = hue, Selected = selected };

    private static UiRowModel Row(string name, string preview, string time, int unread, float hue, bool online, bool verified, bool selected, string section)
    {
        return new UiRowModel { Name = name, Preview = preview, Time = time, Unread = unread, Hue = hue, Online = online, Verified = verified, Selected = selected, Section = section };
    }
}
