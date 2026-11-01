using System;
using RealLifeOverhaul.Ui;

// The kit's sample view models: the owner's Contacts wireframe's own people and words (not-in-repo/design/wireframes,
// contacts_source.html), for the offline previews and EconomyCheck's layout checks. Fixtures, never shipped: the mod's
// screens build their models from the game's data and the locale keys. Words a screen labels with go through the given
// text source, so the checks measure every language; the fixtures' names and messages stay as the wireframe has them.
internal static partial class UiSamples
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
            new Sample { Name = "population", App = "The city's people", Lay = (s, f, d, k) => UiDashboard.Lay(s, PopulationCity(t), f, d, k) },
            new Sample { Name = "population_contributors", App = "The city's people", Lay = (s, f, d, k) => UiDashboard.Lay(s, PopulationContributors(t), f, d, k) },
            new Sample { Name = "population_household", App = "The city's people", Lay = (s, f, d, k) => UiDashboard.Lay(s, PopulationHousehold(t), f, d, k), Walk = "RDDUL" },
            new Sample { Name = "population_person", App = "The city's people", Lay = (s, f, d, k) => UiDashboard.Lay(s, PopulationPerson(t), f, d, k), Walk = "DU" },
            new Sample { Name = "population_gone", App = "The city's people", Lay = (s, f, d, k) => UiDashboard.Lay(s, PopulationGone(t), f, d, k), Walk = "D" },
            new Sample { Name = "flare_city", App = "Flare in the city", Lay = (s, f, d, k) => UiDashboard.Lay(s, FlareCity(t), f, d, k) },
            new Sample { Name = "flare_trends", App = "Flare in the city", Lay = (s, f, d, k) => UiDashboard.Lay(s, FlareTrends(t), f, d, k), Walk = "RRL" },
            new Sample { Name = "flare_business", App = "Flare in the city", Lay = (s, f, d, k) => UiDashboard.Lay(s, FlareBusiness(t), f, d, k), Walk = "DDU" },
            new Sample { Name = "studio", App = "Your firm", Lay = (s, f, d, k) => UiDashboard.Lay(s, StudioFirm(t), f, d, k), Walk = "LDDDDRDDLU" },
            new Sample { Name = "studio_empty", App = "Your firm", Lay = (s, f, d, k) => UiDashboard.Lay(s, StudioEmpty(t), f, d, k), Walk = "DLR" },
            new Sample { Name = "tech_firm", App = "Your firm", Lay = (s, f, d, k) => UiDashboard.Lay(s, TechFirm(t), f, d, k), Walk = "DDDDRDDLU" },
            new Sample { Name = "session_panel", App = "Desk", Lay = (s, f, d, k) => UiSessionSteps.Lay(s, DeskPanel(t), f, d, k), Walk = "RDLUL" },
        };
    }

    /// <summary>Flare's account in the city as FlareCityPages makes it: the tabs, the followers' figures, the posts that carried, the details.</summary>
    public static UiDashboardModel FlareCity(UiWords t)
    {
        // As FlareCityPages makes the account's tab: a feed of what the posts did, the followers at the side, the tabs heading it.
        var m = FlareFeed(t, 0, "What your posts did", new[]
        {
            new[] { "Bloom Café", Did(t, "A post", "1,240", "38", "$412"), Ago(t, 0) },
            new[] { "Bloom Café", Did(t, "An ad", "6,800", "112", "$1,630"), Ago(t, 1) },
            new[] { "Copperline Books", Did(t, "A post", "410", "9", "$96"), Ago(t, 1) },
            new[] { "Night Owl Bar", Did(t, "A post", "880", "21", "$240"), Ago(t, 3) },
            new[] { "Pine Street Florist", Did(t, "A post", "190", "4", "$52"), Ago(t, 5) },
        }, "Nothing measured yet. Post about one of your businesses and look again tomorrow.");
        m.EntryLeads = new[] { true, true, true, true, true };
        return m;
    }

    // What a post did, as FlareCityPages writes it.
    private static string Did(UiWords t, string what, string seen, string came, string spent) =>
        t("reallife_ui_flare_did", "{what} · seen by {seen} · {came} came · {spent} spent").Replace("{what}", what).Replace("{seen}", seen).Replace("{came}", came)
            .Replace("{spent}", spent);

    // How long ago a post went up, as FlareCityPages.Ago writes it (the preview has the kit alone, not the pages).
    private static string Ago(UiWords t, int days) => days == 0 ? t("reallife_ui_when_today", "Today") : days == 1 ? t("reallife_ui_when_yesterday", "Yesterday")
        : t("reallife_ui_when_days", "{days} days ago").Replace("{days}", days.ToString(System.Globalization.CultureInfo.InvariantCulture));

    // How long a game or a product has been out, as the firms' pages write it.
    private static string Out(UiWords t, int days) => t("reallife_studio_page_age_days", "Out {days} days").Replace("{days}", days.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>Flare's trends tab with nothing trending yet: its long empty words wrapped in the feed, the account's followers at the side.</summary>
    public static UiDashboardModel FlareTrends(UiWords t)
    {
        return FlareFeed(t, 1, null, new string[0][], "Nothing is trending among the residents yet. A viral post can start a trend, and so can the city's own talk.");
    }

    /// <summary>A business's page on Flare as the residents see it: its name after the app's in the masthead, its reviews down the feed, its figures at the side.</summary>
    public static UiDashboardModel FlareBusiness(UiWords t)
    {
        var m = new UiDashboardModel
        {
            Shape = UiPageShape.Feed,
            Title = "Bloom Café",
            Subtitle = "Your business, as the residents see it",
            SideTitle = t("reallife_ui_page_details", "Details"),
            EntriesTitle = "Reviews by its customers",
            Figures = new[]
            {
                new UiFigureModel { Label = "Followers", Value = "2,418" },
                new UiFigureModel { Label = "Rating", Value = "4.3 / 5" },
                new UiFigureModel { Label = "Pull", Value = "+6.2%" },
            },
            Side = new[]
            {
                new UiPairModel { Key = "Heard of it", Value = "3,900" },
                new UiPairModel { Key = "Came from Flare", Value = "+41" },
                new UiPairModel { Key = "Spent from Flare", Value = "$612" },
            },
            Entries = new[]
            {
                new UiRowModel { Name = "Priya Raman", Preview = "Five stars. The new grinder shows.", Time = "5.0", Hue = 200f },
                new UiRowModel { Name = "Marcus Hale", Preview = "Busy at nine, worth the wait.", Time = "4.0", Hue = 40f },
                new UiRowModel { Name = "Elena Sokolova", Preview = "Oat milk is free now, nice touch.", Time = "4.5", Hue = 300f },
            },
            Table = new UiTableModel { EmptyTitle = "No reviews yet." },
        };
        m.FigureCount = m.Figures.Length;
        m.SideCount = m.Side.Length;
        m.EntryCount = m.Entries.Length;
        m.EntryLeads = new[] { false, false, false };
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    // A Flare tab as a feed: the tabs heading it, its entries with their faces, the account's two figures at the side.
    private static UiDashboardModel FlareFeed(UiWords t, int tab, string title, string[][] rows, string empty)
    {
        var m = new UiDashboardModel
        {
            Shape = UiPageShape.Feed,
            Tabs = new[] { t("reallife_ui_flare_tab_account", "Account"), t("reallife_ui_flare_tab_trends", "Trends"), t("reallife_ui_flare_tab_offers", "Offers") },
            Tab = tab,
            SideTitle = t("reallife_ui_page_details", "Details"),
            EntriesTitle = title,
            Figures = new[]
            {
                new UiFigureModel { Label = "Followers in the city", Value = "34" },
                new UiFigureModel { Label = "Followers beyond the city", Value = "4" },
            },
            Table = new UiTableModel { EmptyTitle = empty },
        };
        m.FigureCount = m.Figures.Length;
        m.Entries = new UiRowModel[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            m.Entries[i] = new UiRowModel { Name = rows[i][0], Preview = rows[i][1], Time = rows[i][2], Hue = UiColour.HueOf(rows[i][0]) };
        }
        m.EntryCount = rows.Length;
        var prompts = new System.Collections.Generic.List<UiPrompt> { new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") } };
        if (rows.Length > 0)
        {
            prompts.Add(new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") });
        }
        prompts.Add(new UiPrompt { Action = UiAction.TabPrev, Label = t("reallife_ui_prompt_page", "Page") });
        prompts.Add(new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") });
        m.Prompts = prompts.ToArray();
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>Who contributes as PopulationPages makes it: places by their addresses and names, never a record's key, the player's three shares in the strip.</summary>
    public static UiDashboardModel PopulationContributors(UiWords t)
    {
        var m = new UiDashboardModel
        {
            Title = "Who contributes",
            SideTitle = t("reallife_ui_page_details", "Details"),
            Figures = new[]
            {
                new UiFigureModel { Label = "People you employ", Value = "0%" },
                new UiFigureModel { Label = "Spending at your businesses", Value = "0%" },
                new UiFigureModel { Label = "Taxes from your businesses", Value = "0%" },
            },
            Side = new[]
            {
                new UiPairModel { Key = "District that spends most", Value = "Midtown" },
                new UiPairModel { Key = "Spent", Value = "$1,158,079" },
            },
            HasAction = true,
            Action = new UiActionModel { Label = "The district that spends most", Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiDashboard.ActionCommand },
            Table = new UiTableModel
            {
                Columns = new[]
                {
                    new UiColumn { Title = "Employers paying most" }, new UiColumn { Title = string.Empty },
                    new UiColumn { Title = string.Empty, Width = UiTokens.S16 * 2f + UiTokens.S8, Right = true },
                },
                Rows = new[]
                {
                    new[] { "70 Fifth Avenue", "The public sector", "$1,112,059" },
                    new[] { "17 Seventh Street", "The firms in an office building", "$895,271" },
                    new[] { "12 Broadway", "The firms in an office building", "$511,793" },
                    new[] { "Bloom Café", "A business", "$433,549" },
                    new[] { "6 Second Avenue", "A city service", "$410,923" },
                    new[] { "13 Third Avenue", "The firms in an office building", "$379,776" },
                    new[] { "1 First Avenue", "The firms in an office building", "$350,644" },
                    new[] { "37 Fourth Avenue", "The firms in an office building", "$323,109" },
                },
                RowCount = 8, Selected = -1, EmptyTitle = "Nobody pays wages in the city yet.",
            },
        };
        m.FigureCount = m.Figures.Length;
        m.SideCount = m.Side.Length;
        return Prompted(m, t, true);
    }

    /// <summary>A household as PopulationPages makes it: a ledger of its last month and its home beside its people, named by its family.</summary>
    public static UiDashboardModel PopulationHousehold(UiWords t)
    {
        var m = new UiDashboardModel
        {
            Shape = UiPageShape.Ledger,
            Title = t("reallife_ui_pop_household", "The {surname} household").Replace("{surname}", "Stanley"),
            Subtitle = "Middle class",
            Ledger = new[]
            {
                new UiLedgerSection
                {
                    Title = t("reallife_ui_pop_last_month", "Last month"),
                    Lines = new[]
                    {
                        new UiPairModel { Key = "Earned", Value = "$3,410" }, new UiPairModel { Key = "Paid in tax", Value = "$512" },
                        new UiPairModel { Key = "Spent", Value = "$2,188" }, new UiPairModel { Key = "Saved", Value = "$710" },
                    }
                },
                new UiLedgerSection
                {
                    Title = t("reallife_ui_pop_home", "Home and money"),
                    Lines = new[]
                    {
                        new UiPairModel { Key = "Rent paid", Value = "$1,140" }, new UiPairModel { Key = "Rent goes to", Value = t("reallife_pop_landlord_city", "A city landlord") },
                        new UiPairModel { Key = "Savings", Value = "$6,920" }, new UiPairModel { Key = "Debt", Value = "$0" },
                    }
                },
            },
            EntriesTitle = "Its people",
            Entries = new[]
            {
                new UiRowModel { Name = "John Stanley", Preview = "At work", Time = t("reallife_ui_pop_age", "Age {age}").Replace("{age}", "41"), Hue = 25f },
                new UiRowModel { Name = "Rosa Stanley", Preview = "At home", Time = t("reallife_ui_pop_age", "Age {age}").Replace("{age}", "39"), Hue = 160f },
                new UiRowModel { Name = "Leo Stanley", Preview = "At school", Time = t("reallife_ui_pop_age", "Age {age}").Replace("{age}", "12"), Hue = 260f },
            },
            HasAction = true,
            Action = new UiActionModel { Label = "Where they spent most", Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiDashboard.ActionCommand },
            Table = new UiTableModel { EmptyTitle = "This household has moved on." },
        };
        m.EntryCount = m.Entries.Length;
        m.EntryLeads = new[] { true, true, true };
        return Prompted(m, t, true);
    }

    /// <summary>A person as PopulationPages makes it: a profile, their face and state on a card, their figures and facts beside it.</summary>
    public static UiDashboardModel PopulationPerson(UiWords t)
    {
        var m = new UiDashboardModel
        {
            Shape = UiPageShape.Profile,
            Title = "Thomas Sanders",
            Hue = 210f,
            Subtitle = "Looking for work",
            SideTitle = t("reallife_ui_page_details", "Details"),
            Figures = new[] { new UiFigureModel { Label = "Age", Value = "27" } },
            Side = new[] { new UiPairModel { Key = "Schooling", Value = "Postgraduate degree" }, new UiPairModel { Key = t("reallife_pop_fig_field", "Line of work"), Value = "Office work" } },
            EntriesTitle = "Lives with",
            Entries = new[] { new UiRowModel { Name = "Mary Sanders", Preview = "At work", Time = t("reallife_ui_pop_age", "Age {age}").Replace("{age}", "29"), Hue = 120f } },
            HasAction = true,
            Action = new UiActionModel { Label = "Their household", Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiDashboard.ActionCommand },
            Table = new UiTableModel { EmptyTitle = "This person has moved on." },
        };
        m.FigureCount = m.Figures.Length;
        m.SideCount = m.Side.Length;
        m.EntryCount = m.Entries.Length;
        m.EntryLeads = new[] { true };
        return Prompted(m, t, true);
    }

    /// <summary>A person who has moved on: no title over nothing, just the words for it.</summary>
    public static UiDashboardModel PopulationGone(UiWords t)
    {
        var m = new UiDashboardModel
        {
            Table = new UiTableModel
            {
                EmptyTitle = t("reallife_ui_pop_gone_named", "{name} has moved on.").Replace("{name}", "Thomas Sanders"),
                EmptyBody = t("reallife_ui_pop_gone_body", "People leave the city when they move away or pass on. Back takes you to where you were."),
            }
        };
        m.Prompts = new[] { new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") } };
        m.PromptCount = 1;
        return m;
    }

    // A population page's prompt bar as PopulationPages writes it: Select only where a row opens something.
    private static UiDashboardModel Prompted(UiDashboardModel m, UiWords t, bool select)
    {
        var prompts = new System.Collections.Generic.List<UiPrompt> { new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") } };
        if (select)
        {
            prompts.Add(new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") });
        }
        if (m.HasAction)
        {
            prompts.Add(new UiPrompt { Action = UiAction.Alt, Label = m.Action.Label });
        }
        prompts.Add(new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") });
        m.Prompts = prompts.ToArray();
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
                    new[] { "Ember Isle", "Adventure", "78", "41,000", "$612,400", Out(t, 96) },
                    new[] { "Tiny Towns", "Simulation", "74", "28,500", "$341,900", Out(t, 212) },
                    new[] { "Deep Signal", "Strategy", "69", "12,600", "$128,300", Out(t, 340) },
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
            StatsNote = t("reallife_studio_page_needed_how", "To fill a group, hire people in the Business app, then set their roles in Open Roles, on its Team tab."),
            SideTitle = t("reallife_studio_page_facts", "Sales and support"),
            Side = new[]
            {
                new UiPairModel { Key = t("reallife_studio_page_tickets", "Tickets a day"), Value = "120, 0 answered" },
                new UiPairModel { Key = t("reallife_studio_page_pirated", "Pirated copies"), Value = "9,800" },
                new UiPairModel { Key = t("reallife_studio_page_drm", "DRM"), Value = "On in 1 of 3" },
                new UiPairModel { Key = t("reallife_studio_page_asks", "Fan requests"), Value = "New levels, co-op play" },
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
    /// A new studio as the owner opened it in beta 3: nobody in it, no game, nothing taken. Its next step is to hire, each
    /// needed group says how to fill it, its takings draw no flat line, and its facts say None in one case.
    /// </summary>
    public static UiDashboardModel StudioEmpty(UiWords t)
    {
        var m = new UiDashboardModel
        {
            Title = "Pioneer Game studio",
            Subtitle = t("reallife_studio_next_hire", "Next: hire engineers and designers for {studio}, so it can start a game.")
                .Replace("{studio}", "Pioneer Game studio"),
            SubtitleInk = UiRole.WarnText,
            StatsFirst = true,
            Figures = new[]
            {
                new UiFigureModel { Label = t("reallife_studio_page_games_out", "Games out"), Value = "0" },
                new UiFigureModel { Label = t("reallife_studio_page_copies", "Copies sold"), Value = "0" },
                new UiFigureModel { Label = t("reallife_studio_page_takings", "Takings, 30 days"), Value = "$0", Span = 2, Trend = new float[24] },
            },
            Table = FirmTable(t, new[] { "reallife_studio_page_col_game", "reallife_studio_page_col_genre", "reallife_studio_page_col_score", "reallife_studio_page_col_copies",
                "reallife_studio_page_col_kept", "reallife_studio_page_col_status" }, new[] { "Game", "Genre", "Score", "Copies", "Kept", "Status" }, 2,
                "reallife_studio_page_no_games_hire", "No game yet. Hire a team first, then draft a concept.", new string[0][]),
            More = FirmTable(t, new[] { "reallife_studio_page_col_rival", "reallife_studio_page_col_studio", "reallife_studio_page_col_genre", "reallife_studio_page_col_score",
                "reallife_studio_page_col_copies" }, new[] { "Rival game", "Studio", "Genre", "Score", "Copies" }, 3, "reallife_studio_studio_rivals_none", "No rival game is out yet.",
                new string[0][]),
            StatsTitle = t("reallife_studio_page_team", "Team"),
            Stats = new[]
            {
                Group(t, "reallife_studio_page_group_engineers", "Engineers", 0, 0),
                Group(t, "reallife_studio_page_group_designers", "Designers", 0, 0),
            },
            StatsNote = t("reallife_studio_page_needed_how", "To fill a group, hire people in the Business app, then set their roles in Open Roles, on its Team tab."),
            SideTitle = t("reallife_studio_page_facts", "Sales and support"),
            Side = new[]
            {
                new UiPairModel { Key = t("reallife_studio_page_tickets", "Tickets a day"), Value = "None" },
                new UiPairModel { Key = t("reallife_studio_page_pirated", "Pirated copies"), Value = "0" },
                new UiPairModel { Key = t("reallife_studio_page_drm", "DRM"), Value = "Off" },
                new UiPairModel { Key = t("reallife_studio_page_asks", "Fan requests"), Value = "None" },
                new UiPairModel { Key = t("reallife_studio_page_engine", "Engine"), Value = "Under licence" },
                new UiPairModel { Key = t("reallife_studio_page_trend", "Hot genre"), Value = "None" },
                new UiPairModel { Key = t("reallife_studio_page_known", "Team knows"), Value = "None" },
            },
            Others = new[] { new UiActionModel { Label = "Build our own engine", Kind = UiButtonKind.Secondary, Key = UiAction.Option, Command = UiDashboard.OtherCommand } },
        };
        m.FigureCount = m.Figures.Length;
        m.StatCount = m.Stats.Length;
        m.SideCount = m.Side.Length;
        m.OtherCount = m.Others.Length;
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
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
                    new[] { "Ledgerly", "Subscription service", "12,400", "$412,000", Out(t, 184) },
                    new[] { "Pocket Ledger", "Phone app", "5,840", "$61,300", Out(t, 96) },
                    new[] { "Ledger Cloud", "Subscription service", "", "", t("reallife_tech_mkt_page_ready", "Ready to launch") },
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
            StatsNote = t("reallife_studio_page_needed_how", "To fill a group, hire people in the Business app, then set their roles in Open Roles, on its Team tab."),
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

    /// <summary>The population's city view as PopulationPages makes it from PopulationViews.City: the residents' city at a glance.</summary>
    public static UiDashboardModel PopulationCity(UiWords t)
    {
        // As PopulationPages draws the city's view: its title is the app's own, so the masthead says it once.
        var m = new UiDashboardModel
        {
            Title = t("reallife_pop_view_city", "The city's people"),
            SideTitle = t("reallife_ui_page_details", "Details"),
            Figures = new[]
            {
                new UiFigureModel { Label = "People", Value = "56,666" },
                new UiFigureModel { Label = "Households", Value = "19,578" },
                new UiFigureModel { Label = "Out of work", Value = "5.4%" },
                new UiFigureModel { Label = "Rent a day", Value = "$86" },
            },
            Side = new[]
            {
                new UiPairModel { Key = "Births last month", Value = "41" },
                new UiPairModel { Key = "Deaths last month", Value = "17" },
                new UiPairModel { Key = "Households who came", Value = "212" },
                new UiPairModel { Key = "Households who left", Value = "96" },
                new UiPairModel { Key = "Jobs", Value = "109,605" },
                new UiPairModel { Key = "Open jobs", Value = "2,735" },
                new UiPairModel { Key = "Commuters", Value = "81,682" },
                new UiPairModel { Key = "Taxes last month", Value = "$1,212,400" },
            },
            HasAction = true,
            Action = new UiActionModel { Label = "See who contributes", Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiDashboard.ActionCommand },
        };
        m.FigureCount = m.Figures.Length;
        m.SideCount = m.Side.Length;
        m.Table = new UiTableModel
        {
            // Each district's own figures in columns, as PopulationViews.City now gives them, rather than one number a row.
            Columns = new[]
            {
                new UiColumn { Title = "Districts" },
                new UiColumn { Title = "People", Width = UiTokens.S16 * 2f + UiTokens.S8, Right = true },
                new UiColumn { Title = "Households", Width = UiTokens.S16 * 2f + UiTokens.S8, Right = true },
                new UiColumn { Title = "Out of work", Width = UiTokens.S16 * 2f + UiTokens.S8, Right = true },
                new UiColumn { Title = "Rent a day", Width = UiTokens.S16 * 2f + UiTokens.S8, Right = true },
            },
            Rows = new[]
            {
                new[] { "Cedar Heights", "11,190", "3,870", "6.1%", "$74" },
                new[] { "Uptown", "9,122", "3,140", "4.8%", "$91" },
                new[] { "Midtown", "8,213", "2,960", "3.9%", "$118" },
                new[] { "Hell's Kitchen", "8,148", "2,810", "5.2%", "$96" },
                new[] { "Murray Hill", "5,859", "2,050", "4.4%", "$102" },
                new[] { "Lower Manhattan", "5,855", "2,020", "5.7%", "$109" },
                new[] { "Garment District", "4,574", "1,560", "7.3%", "$68" },
                new[] { "Industry City", "3,601", "1,168", "8.9%", "$59" },
            },
            RowCount = 8,
            Selected = -1,
            EmptyTitle = t("reallife_ui_page_empty", "Nothing here yet"),
        };
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
            new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.Alt, Label = "See who contributes" },
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
            Row("Uncle Fred", "Good. Once you’ve got keys, open your first shop. I’ll check in.", "09:02", 1, 40f, true, false, false, null),
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

    /// <summary>A dashboard of the city's people, the shape the Economy app's population view takes.</summary>
    public static UiDashboardModel Dashboard(UiWords t)
    {
        var m = new UiDashboardModel
        {
            Tabs = new[] { t("reallife_ui_gallery_tab_city", "City"), t("reallife_ui_gallery_tab_district", "District"), t("reallife_ui_gallery_tab_people", "People") },
            Figures = SampleFigures(t),
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
            Selected = -1,
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
