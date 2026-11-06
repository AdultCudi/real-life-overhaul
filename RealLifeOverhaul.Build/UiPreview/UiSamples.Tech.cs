using System;
using System.Collections.Generic;
using RealLifeOverhaul.Ui;

// The Tech app's samples: the model each page writes, with real-looking names, long names, empty and full states. Every word goes through the screen's text source so the
// checks measure it in all eleven languages. Names here are placeholders and never ship.
internal static partial class UiSamples
{
    public static readonly string[] TechStates = JoinStates(new[]
    {
        "firms", "firms_empty", "studio", "game", "game_plan", "datacentre", "software", "products", "product", "contracts", "tender", "infra", "startups", "startup", "funding", "cap", "cap_account", "books", "books_free", "terms",
        "team", "rivals", "concept", "found", "found_angel", "found_none", "niches", "niche", "gtm", "gtm_placed", "off"
    }, TechExitStates());

    private static string[] JoinStates(string[] a, string[] b)
    {
        var all = new string[a.Length + b.Length];
        a.CopyTo(all, 0);
        b.CopyTo(all, a.Length);
        return all;
    }

    // The runs a sample page holds: each does nothing here, and the count is what the checks press.
    private sealed class TechRunList
    {
        public readonly TechRuns Runs = new TechRuns();
        public int Add() => Runs.Add(() => true);
    }

    // A key whose words have placeholders, filled with the sample's figures.
    private static string WF(UiWords t, string key, string english, params string[] pairs)
    {
        string text = t(key, english);
        for (int i = 0; i + 1 < pairs.Length; i += 2)
        {
            text = text.Replace("{" + pairs[i] + "}", pairs[i + 1]);
        }
        return text;
    }

    private static string TW(UiWords t, string key, string english) => t("reallife_ui_tech_" + key, english);

    private static UiActionModel TechAct(TechRunList runs, string label, UiButtonKind kind, UiAction key, UiIcon icon = UiIcon.None, bool disabled = false) =>
        new UiActionModel { Label = label, Kind = kind, Key = key, Icon = icon, Command = TechFace.ActionCommand, Arg = runs.Add(), Disabled = disabled };

    private static UiNavEntry[] TechTabNav(UiWords t, out string[] keys)
    {
        keys = new[] { "tab:0", "tab:1", "tab:2", "tab:3", "tab:4", "tab:5" };
        return new[]
        {
            new UiNavEntry { Icon = UiIcon.Building, Label = W(t, "reallife_tech_tab_firms", "Firms"), Count = "6", Group = TW(t, "group_sector", "Your sector") },
            new UiNavEntry { Icon = UiIcon.Package, Label = W(t, "reallife_tech_tab_products", "Products"), Count = "11" },
            new UiNavEntry { Icon = UiIcon.Handshake, Label = W(t, "reallife_tech_tab_contracts", "Contracts"), Count = "9" },
            new UiNavEntry { Icon = TechGlyphs.Centre, Label = W(t, "reallife_tech_tab_infra", "Infra"), Count = "2", Group = TW(t, "group_build", "Build") },
            new UiNavEntry { Icon = TechGlyphs.Startup, Label = W(t, "reallife_tech_tab_startups", "Startups"), Count = "2" },
            new UiNavEntry { Icon = UiIcon.Coin, Label = W(t, "reallife_tech_tab_funding", "Funding") }
        };
    }

    private static UiNavEntry[] TechFirmNav(UiWords t, bool studio, int selected, out string[] keys)
    {
        var nav = new List<UiNavEntry>
        {
            new UiNavEntry { Icon = UiIcon.Back, Label = TW(t, "all_firms_back", "All firms") },
            new UiNavEntry { Icon = UiIcon.Grid, Label = TW(t, "sec_overview", "Overview"), Group = TW(t, "group_firm", "This firm") },
            new UiNavEntry { Icon = studio ? UiIcon.GameController : UiIcon.Package, Label = studio ? TW(t, "sec_games", "Games") : W(t, "reallife_tech_tab_products", "Products"), Count = "6" },
            new UiNavEntry { Icon = UiIcon.People, Label = TW(t, "sec_team", "Team"), Count = "12" },
            new UiNavEntry { Icon = UiIcon.Globe, Label = TW(t, "sec_market", "Market") },
            new UiNavEntry { Icon = UiIcon.Flag, Label = TW(t, "sec_rivals", "Rivals"), Count = "8" },
            new UiNavEntry { Icon = UiIcon.Coin, Label = TW(t, "sec_money", "Money") }
        };
        var list = new List<string> { "tab:0", "sec:0", "sec:1", "sec:2", "sec:3", "sec:4", "sec:5" };
        if (studio)
        {
            nav.Add(new UiNavEntry { Icon = UiIcon.Trophy, Label = TW(t, "sec_awards", "Awards"), Count = "2" });
            list.Add("sec:6");
        }
        keys = list.ToArray();
        return nav.ToArray();
    }

    private static UiPrompt[] TechPrompts(UiWords t) => new[]
    {
        new UiPrompt { Action = UiAction.Move, Label = W(t, "reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = W(t, "reallife_ui_prompt_select", "Select") },
        new UiPrompt { Action = UiAction.Back, Label = W(t, "reallife_ui_prompt_back", "Back") }
    };

    private static UiDataRow TechRow(int cells) => new UiDataRow { Cells = new string[cells], Values = new int[cells], Tones = new UiRole[cells] };

    private static UiDataColumn TechCol(UiWords t, string key, string english, UiCellKind kind, float width = 0f, bool graded = false) =>
        new UiDataColumn { Title = t(key, english), Kind = kind, Width = width, Graded = graded };

    private static float[] TechSpark(int n, float from, float to, float wobble)
    {
        var v = new float[n];
        for (int i = 0; i < n; i++)
        {
            v[i] = from + (to - from) * i / Math.Max(1, n - 1) + (float)Math.Sin(i * 1.7) * wobble;
        }
        return v;
    }

    public static TechModel Tech(UiWords t, string state)
    {
        var runs = new TechRunList();
        var m = new TechModel { Title = W(t, "reallife_tech_app", "Tech"), Icon = UiIcon.Monitor, Prompts = TechPrompts(t) };
        switch (state)
        {
            case "firms": TechFirms(t, m, runs, false); break;
            case "firms_empty": TechFirms(t, m, runs, true); break;
            case "studio": TechStudio(t, m, runs); break;
            case "game": TechGame(t, m, runs); break;
            case "game_plan": TechGamePlan(t, m, runs); break;
            case "datacentre": TechCentre(t, m, runs); break;
            case "software": TechSoftware(t, m, runs); break;
            case "products": TechProducts(t, m, runs); break;
            case "product": TechProduct(t, m, runs); break;
            case "contracts": TechContracts(t, m, runs); break;
            case "tender": TechTender(t, m, runs); break;
            case "infra": TechInfra(t, m, runs); break;
            case "startups": TechStartups(t, m, runs); break;
            case "startup": TechStartup(t, m, runs); break;
            case "funding": TechFunding(t, m, runs); break;
            case "cap": TechCap(t, m, runs); break;
            case "cap_account": TechCapAccount(t, m, runs); break;
            case "books": TechBooks(t, m, runs, true); break;
            case "books_free": TechBooks(t, m, runs, false); break;
            case "terms": TechTerms(t, m, runs); break;
            case "found": TechFound(t, m, runs); break;
            case "found_angel": TechFoundAngel(t, m, runs); break;
            case "found_none": TechFoundNone(t, m, runs); break;
            case "niches": TechNiches(t, m, runs); break;
            case "niche": TechNiche(t, m, runs); break;
            case "gtm": TechGtm(t, m, runs, false); break;
            case "gtm_placed": TechGtm(t, m, runs, true); break;
            case "team": TechTeam(t, m, runs); break;
            case "rivals": TechRivals(t, m, runs); break;
            case "concept": TechConcept(t, m, runs); break;
            case "exits": case "exits_market": case "exits_listed": case "exits_hire": TechExits(t, m, runs, state); break;
            case "startup_place": TechStartupPlace(t, m, runs); break;
            default:
                m.Shape = TechShape.Off;
                m.OffTitle = W(t, "reallife_roles_off_title", "Tech is off");
                m.OffBody = W(t, "reallife_tech_off_body", "The tech sector is switched off in the options.");
                m.OffAction = TW(t, "close", "Close");
                break;
        }
        m.Runs = runs.Runs.ToArray();
        return m;
    }

    // ------------------------------------------------------------------------------------------------- the firms

    private static void TechFirms(UiWords t, TechModel m, TechRunList runs, bool empty)
    {
        m.Shape = TechShape.Table;
        m.Nav = TechTabNav(t, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 0;
        var rows = new List<UiDataRow>();
        var rowKeys = new List<string>();
        void Firm(UiIcon icon, string name, string sub, string line, string takings, string change, int up, string staff, string status, UiRole tone, string key)
        {
            UiDataRow r = TechRow(6);
            r.Cells[0] = name; r.Sub = sub; r.Values[0] = (int)icon;
            r.Cells[1] = line; r.Cells[2] = takings; r.Tones[2] = UiRole.OkText; r.Cells[3] = change; r.Values[3] = up; r.Tones[3] = UiRole.OkText;
            r.Cells[4] = staff; r.Values[4] = 1; r.Cells[5] = status; r.Tones[5] = tone;
            rows.Add(r);
            rowKeys.Add(key);
        }
        if (!empty)
        {
            Firm(UiIcon.GameController, "Pioneer Game Studio", "Game studio", "5 games out, 1 in development", "$184,200", "+12%", 1, "12", TW(t, "status_0", "In production"), UiRole.Ok, "firm:a");
            Firm(TechGlyphs.Centre, "Hudson Data Hall", "Tier III data centre, Hell's Kitchen", "184 racks of 220", "$96,400", "+6%", 1, "9", TW(t, "status_4", "Nearly full"), UiRole.Warn, "dc:b");
            Firm(UiIcon.Handshake, "Midtown Fibre Consultancy", "Consultancy", "Services and contracts", "$41,800", "-2%", -1, "6", TW(t, "status_1", "Running"), UiRole.Ok, "firm:c");
            Firm(UiIcon.Code, "Atlas Edge Software", "Software", "2 out, 1 in development", "$27,600", "+9%", 1, "5", TW(t, "status_6", "Expanding"), UiRole.Ok, "firm:d");
            Firm(TechGlyphs.Startup, "Quill Software", "Startup, 22% stake, Software", "Seed", "$12,900", "", 0, "-", TW(t, "status_7", "Growing"), UiRole.Ok, "start:1");
            Firm(TechGlyphs.Startup, "Loop Labs", "Startup, 40% stake, Games", "Pre-seed", "$3,200", "", 0, "-", TW(t, "status_8", "Seed funded"), UiRole.Notice, "start:2");
        }
        m.Table = new UiDataTableModel
        {
            Columns = new[]
            {
                TechCol(t, "reallife_ui_tech_col_firm", "Firm", UiCellKind.Tile), TechCol(t, "reallife_tech_col_products", "Products", UiCellKind.Text, 232f),
                TechCol(t, "reallife_ui_tech_col_takings", "Takings, 30 days", UiCellKind.Money, 112f), TechCol(t, "reallife_ui_tech_col_change", "Change", UiCellKind.Trend, 80f),
                TechCol(t, "reallife_ui_tech_col_staff", "Staff", UiCellKind.Figure, 72f), TechCol(t, "reallife_ui_tech_col_status", "Status", UiCellKind.Chip, 176f)
            },
            Rows = rows.ToArray(), RowCount = rows.Count, Selected = -1,
            TotalsLabel = empty ? null : TW(t, "all_firms", "All firms"), Totals = empty ? null : new[] { "", "", "$366,100", "", "32", "" },
            EmptyTitle = TW(t, "no_firm", "No firm yet"), EmptyBody = TW(t, "no_firm_body", "Start a firm at the desk of an empty office."), EmptyIcon = UiIcon.Building
        };
        m.RowKeys = rowKeys.ToArray();
        m.Next = empty ? TW(t, "next_start", "Next: start a tech firm at the desk of an empty office.")
            : "Next: Hudson Data Hall is at 91% of its racks. Order more before it fills.";
        if (!empty)
        {
            m.Actions = new[]
            {
                TechAct(runs, TW(t, "open_contracts", "Open contracts"), UiButtonKind.Secondary, UiAction.Option),
                TechAct(runs, TW(t, "order_racks", "Order racks"), UiButtonKind.Primary, UiAction.Alt)
            };
            m.Figures = new[] { new UiFigureModel { Label = TW(t, "group_takings", "Group takings, 30 days"), Value = "$366,100", Span = 2, Trend = TechSpark(24, 38f, 54f, 3f) } };
            var days = TechSpark(30, 8000f, 14000f, 900f);
            var points = new string[days.Length];
            for (int i = 0; i < days.Length; i++)
            {
                points[i] = "Day " + (372 + i);
            }
            m.Chart = new UiChartModel
            {
                Kind = UiChartKind.Line, Title = TW(t, "takings_by_day", "Takings by day, all firms"), Points = points,
                Series = new[] { new UiSeries { Label = TW(t, "all_firms", "All firms"), Values = days } }, Format = v => "$" + ((long)v).ToString("N0"), AxisFormat = v => "$" + (long)(v / 1000) + "k",
                EvenAxis = true, Still = true
            };
            var league = new TechCard(runs.Runs, TW(t, "league_title", "Sector league, 30 days")) { Sub = "You are 3 of 7" };
            league.Who("Codeharbour Software", "412 customers", "$412,000").Who("Abacus Lane Software", "380 customers", "$356,800")
                .Who(TW(t, "league_you", "Your firms"), "4 firms", "$354,000", UiRole.AccentText).Who("Shopfront Online", "9,200 customers", "$291,400")
                .Who("Tallyrow Analytics", "120 customers", "$187,900").Who("Signpost Search", "88,000 customers", "$142,300").Who("Ledgerline Payments", "21,000 customers", "$128,600");
            league.Space().Say("Codeharbour Software is hiring engineering people.", UiRole.WarnText).Go(TW(t, "open_roles", "Open Roles"), () => true);
            m.Side = league.Build();
        }
    }

    // ------------------------------------------------------------------------------------------------- one firm

    private static UiPersonRow TechPerson(string name, UiIcon icon, string role, int skill, int morale) => new UiPersonRow
    {
        Name = name, Hue = UiColour.HueOf(name), RoleIcon = icon, RoleTint = UiRole.None, Role = role, FirstLabel = "Skill", First = skill, SecondLabel = "Morale", Second = morale
    };

    private static UiAppRow TechGame(UiIcon icon, string name, string genre, int score, string copies, string earned, string status, UiRole tone) => new UiAppRow
    {
        Icon = icon, Tint = UiRole.None, Grade = score, Status = status, StatusTone = tone, Cells = new[] { name, genre, score.ToString(), copies, earned, status }
    };

    private static void TechStudio(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Overview;
        m.Title = "Pioneer Game Studio";
        m.Icon = UiIcon.GameController;
        m.Nav = TechFirmNav(t, true, 1, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 1;
        m.Next = "Next: Ferry Tycoon's playtest waits at your desk. Book the testers, then sign off its next milestone.";
        m.Actions = new[]
        {
            TechAct(runs, TW(t, "open_roles", "Open Roles"), UiButtonKind.Secondary, UiAction.Option),
            TechAct(runs, W(t, "reallife_studio_button_desk", "Go to the desk"), UiButtonKind.Primary, UiAction.Alt)
        };
        var a = new UiAppModel
        {
            Hero = new UiHeroModel
            {
                Icon = UiIcon.GenreSimulation, Kicker = TW(t, "in_development", "In development"), Title = "Ferry Tycoon", Line = "Simulation, 64% made, 3 of 5 milestones met", Progress = 64,
                Note = TW(t, "at_desk", "Waiting at your desk"), NoteIcon = UiIcon.Locate
            },
            Figures = new[]
            {
                new UiFigureModel { Label = TW(t, "takings_30", "Takings, 30 days"), Value = "$184,200", Change = "+12%", Rising = true, Good = true, Trend = TechSpark(24, 38f, 50f, 4f) }
            },
            TableTitle = TW(t, "games_out", "Games out"),
            Columns = new[]
            {
                new UiColumn { Title = W(t, "reallife_studio_page_col_game", "Game") }, new UiColumn { Title = W(t, "reallife_studio_page_col_genre", "Genre"), Width = 112f },
                new UiColumn { Title = W(t, "reallife_studio_page_col_score", "Score"), Width = 64f, Right = true }, new UiColumn { Title = W(t, "reallife_studio_page_col_copies", "Copies"), Width = 80f, Right = true },
                new UiColumn { Title = W(t, "reallife_studio_page_col_kept", "Kept"), Width = 96f, Right = true }, new UiColumn { Title = TW(t, "col_status", "Status"), Width = 136f, Right = true }
            },
            GradeColumn = 2,
            Rows = new[]
            {
                TechGame(UiIcon.GenreFamily, "Block Party Panic", "Family", 81, "38,400", "$812,300", "Out 41 days", UiRole.Ok),
                TechGame(UiIcon.GenreSimulation, "Tiny Subway", "Simulation", 78, "41,000", "$612,400", "Out 96 days", UiRole.Ok),
                TechGame(UiIcon.GenreRacing, "Dockside Dash", "Racing", 72, "22,800", "$228,900", "Out 212 days", UiRole.Ok),
                TechGame(UiIcon.GenreStrategy, "Harbour Heist", "Strategy", 69, "12,600", "$128,300", "Out 340 days", UiRole.Ok),
                TechGame(UiIcon.GenrePuzzle, "Moonlit Maze", "Puzzle", 58, "4,300", "$31,200", "Withdrawn", UiRole.Warn)
            },
            MoreTitle = TW(t, "games_market", "The games market"),
            MoreColumns = new[]
            {
                new UiColumn { Title = W(t, "reallife_studio_page_col_rival", "Rival game") }, new UiColumn { Title = W(t, "reallife_studio_page_col_studio", "Studio"), Grow = true },
                new UiColumn { Title = W(t, "reallife_studio_page_col_score", "Score"), Width = 64f, Right = true }, new UiColumn { Title = W(t, "reallife_studio_page_col_copies", "Copies"), Width = 80f, Right = true }
            },
            MoreGrade = 2,
            MoreRows = new[]
            {
                new UiAppRow { Icon = UiIcon.GenreStrategy, Grade = 86, Cells = new[] { "Tower of Tides", "Pixel Harbour Games", "86", "57,200" } },
                new UiAppRow { Icon = UiIcon.GenreAction, Grade = 82, Cells = new[] { "Iron Harbour", "Saltmarsh Studios", "82", "41,000" } },
                new UiAppRow { Icon = UiIcon.GenreRpg, Grade = 79, Cells = new[] { "Dust and Dynamite", "Tidewater Interactive", "79", "33,600" } },
                new UiAppRow { Icon = UiIcon.GenrePuzzle, Grade = 71, Cells = new[] { "Lantern Puzzles", "Copperkettle Games", "71", "12,300" } },
                new UiAppRow { Icon = UiIcon.GenreCasual, Grade = 66, Cells = new[] { "Pixel Plumbers", "Blue Kestrel Games", "66", "8,100" } }
            },
            PeopleTitle = TW(t, "sec_team", "Team"), PeopleCount = "12 people",
            People = new[]
            {
                TechPerson("Ava Lindqvist", UiIcon.Code, "Lead engineer", 86, 72), TechPerson("Marcus Hale", UiIcon.Code, "Engineer", 64, 58), TechPerson("Priya Raman", UiIcon.PaintBrush, "Game designer", 77, 81),
                TechPerson("Tom Becker", UiIcon.PaintBrush, "Level designer", 52, 44), TechPerson("Elena Sokolova", UiIcon.Bug, "Tester", 61, 66), TechPerson("Kenji Mori", UiIcon.Lightbulb, "Producer", 58, 61),
                TechPerson("Jonah Reyes", UiIcon.Bug, "QA lead", 70, 38)
            },
            PeopleMore = "See all 12 in Team",
            Note = "Support is needed: 120 tickets a day go unanswered. Hire people, then give them roles in Open Roles.", NoteAction = TW(t, "open_roles", "Open Roles")
        };
        m.App = a;
        m.RowKeys = new[] { "prod:1", "prod:2", "prod:3", "prod:4", "prod:5" };
        m.PersonKeys = new[] { "sec:2", "sec:2", "sec:2", "sec:2", "sec:2", "sec:2", "sec:2" };
        m.RowKeysMore = new string[5];
        m.NoteRun = runs.Add();
    }

    private static void TechSoftware(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Overview;
        m.Title = "Atlas Edge Software";
        m.Icon = UiIcon.Code;
        m.Nav = TechFirmNav(t, false, 1, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 1;
        m.Next = "Next: Atlas Sync has 3 features and needs 5 to launch. The team is on it.";
        m.Actions = new[] { TechAct(runs, W(t, "reallife_tech_button_new_product", "New product"), UiButtonKind.Primary, UiAction.Alt) };
        m.App = new UiAppModel
        {
            Hero = new UiHeroModel { Icon = UiIcon.Cloud, Kicker = TW(t, "in_development", "In development"), Title = "Atlas Sync", Line = "Subscription service, 3 features, 40% to the next", Progress = 40 },
            Figures = new[] { new UiFigureModel { Label = TW(t, "takings_30", "Takings, 30 days"), Value = "$27,600", Change = "+9%", Rising = true, Good = true, Trend = TechSpark(24, 20f, 30f, 2f) } },
            TableTitle = TW(t, "products_out", "Products out"),
            Columns = new[]
            {
                new UiColumn { Title = TW(t, "col_product", "Product") }, new UiColumn { Title = TW(t, "col_kind", "Kind"), Width = 112f }, new UiColumn { Title = TW(t, "col_quality", "Quality"), Width = 88f, Right = true },
                new UiColumn { Title = TW(t, "col_customers", "Customers"), Width = 96f, Right = true }, new UiColumn { Title = TW(t, "col_month", "A month"), Width = 96f, Right = true },
                new UiColumn { Title = TW(t, "col_status", "Status"), Width = 136f, Right = true }
            },
            GradeColumn = 2,
            Rows = new[]
            {
                new UiAppRow { Icon = UiIcon.Cloud, Grade = 72, Status = "Out 120 days", StatusTone = UiRole.Ok, Cells = new[] { "Atlas Backup", "Subscription service", "72", "1,840", "$22,100", "Out 120 days" } },
                new UiAppRow { Icon = UiIcon.Key, Grade = 64, Status = "Out 44 days", StatusTone = UiRole.Ok, Cells = new[] { "Atlas Licence Kit", "Licensed software", "64", "12", "$5,500", "Out 44 days" } }
            },
            PeopleTitle = TW(t, "sec_team", "Team"), PeopleCount = "5 people",
            People = new[]
            {
                TechPerson("Dana Whitfield", UiIcon.Code, "Software engineer", 74, 70), TechPerson("Sam Okafor", UiIcon.Code, "Software engineer", 61, 55), TechPerson("Mei Lin", UiIcon.Database, "Data analyst", 81, 64),
                TechPerson("Luca Bianchi", UiIcon.Megaphone, "Sales", 58, 62), TechPerson("Nora Haddad", UiIcon.Headset, "Support", 49, 51)
            },
            Note = "Support is needed: 40 tickets a day go unanswered. Hire people, then give them roles in Open Roles.", NoteAction = TW(t, "open_roles", "Open Roles"),
            MoreTitle = TW(t, "markets", "Markets"),
            MoreColumns = new[]
            {
                new UiColumn { Title = "Segment" }, new UiColumn { Title = "Leader", Grow = true }, new UiColumn { Title = "Features", Width = 88f, Right = true }
            },
            MoreRows = new[]
            {
                new UiAppRow { Icon = UiIcon.Globe, Grade = -1, Cells = new[] { "Business software", "Abacus Lane Software", "6" } },
                new UiAppRow { Icon = UiIcon.Globe, Grade = -1, Cells = new[] { "Licences", "Codeharbour Software", "5" } }
            }
        };
        m.RowKeys = new[] { "prod:6", "prod:7" };
        m.PersonKeys = new[] { "sec:2", "sec:2", "sec:2", "sec:2", "sec:2" };
        m.RowKeysMore = new string[2];
        m.NoteRun = runs.Add();
    }

    // ------------------------------------------------------------------------------------------------- a game

    private static void TechGame(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Game;
        m.Title = "Pioneer Game Studio";
        m.Icon = UiIcon.GameController;
        m.Nav = TechFirmNav(t, true, 2, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 2;
        m.Next = "Next: the post-launch review at your desk covers the co-op lag the reviewers mention.";
        m.Actions = new[]
        {
            TechAct(runs, TW(t, "plan", "Plan and marketing"), UiButtonKind.Secondary, UiAction.Option),
            TechAct(runs, W(t, "reallife_studio_button_desk", "Go to the desk"), UiButtonKind.Primary, UiAction.Alt)
        };
        m.Hero = new UiHeroModel { Icon = UiIcon.GenreFamily, Kicker = "OUT 41 DAYS, PC AND CONSOLE", Title = "Block Party Panic", Line = "Family, party games", Progress = -1 };
        m.Figures = new[]
        {
            new UiFigureModel { Label = TW(t, "price", "Price"), Value = "$24.99" }, new UiFigureModel { Label = "Copies", Value = "38,400" }, new UiFigureModel { Label = "Kept", Value = "$812,300" }
        };
        m.Track = new UiTrackModel
        {
            Title = TW(t, "milestones", "Milestones"), Line = "5 of 5 met", StateWords = new[] { "Met", "Now", "Next", "Locked", "Later" },
            Steps = new[]
            {
                new UiTrackStep { Icon = UiIcon.Lightbulb, Label = TW(t, "ms_pitch", "Pitch"), Note = "Day 240", State = UiStepState.Done },
                new UiTrackStep { Icon = UiIcon.ClipboardText, Label = TW(t, "ms_review1", "First review"), Note = "", State = UiStepState.Done },
                new UiTrackStep { Icon = UiIcon.Bug, Label = TW(t, "ms_playtest", "Playtest"), Note = "", State = UiStepState.Done },
                new UiTrackStep { Icon = UiIcon.ClipboardText, Label = TW(t, "ms_review2", "Second review"), Note = "", State = UiStepState.Done },
                new UiTrackStep { Icon = UiIcon.Rocket, Label = TW(t, "ms_launch", "Launch"), Note = "Day 361", State = UiStepState.Done }
            }
        };
        m.QuotesTitle = TW(t, "reviews", "Reviews");
        m.QuotesCount = "12 outlets";
        m.QuotesAverage = "81";
        m.QuotesAverageLabel = TW(t, "average_score", "average score");
        m.Quotes = new[]
        {
            new TechQuote { Outlet = "Pixel Post", Day = "Day 369", Score = 90, Line = "“So much to do, and all of it good.”" },
            new TechQuote { Outlet = "Joystick Weekly", Day = "Day 366", Score = 87, Line = "“Smart design with a strong hook.”" },
            new TechQuote { Outlet = "The Cartridge", Day = "Day 365", Score = 85, Line = "“Polished down to the last detail.”" },
            new TechQuote { Outlet = "Loadscreen", Day = "Day 364", Score = 78, Line = "“Not enough to do.”" },
            new TechQuote { Outlet = "Hardmode", Day = "Day 372", Score = 74, Line = "“Crashes and glitches ruin the fun.”" }
        };
        var days = TechSpark(40, 3600f, 600f, 260f);
        var rival = TechSpark(40, 1250f, 1550f, 90f);
        var points = new string[days.Length];
        for (int i = 0; i < days.Length; i++)
        {
            points[i] = "Day " + (361 + i);
        }
        m.Chart = new UiChartModel
        {
            Kind = UiChartKind.Line, Title = TW(t, "copies_a_day", "Copies sold a day"), Points = points, EvenAxis = true, Whole = true, Format = v => ((long)v).ToString("N0"),
            Series = new[] { new UiSeries { Label = "Block Party Panic", Values = days }, new UiSeries { Label = "Tower of Tides, Pixel Harbour Games", Values = rival } }
        };
        m.ChartNotes = "Day 361 launch, Day 369 first review";
    }

    private static void TechGamePlan(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Sheet;
        m.Title = "Pioneer Game Studio";
        m.Icon = UiIcon.GameController;
        m.Nav = TechFirmNav(t, true, 2, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 2;
        m.Back = TW(t, "back_game", "Game");
        m.Head = new UiHeadModel { Name = "Block Party Panic", Icon = UiIcon.GenreFamily, Role = TW(t, "plan", "Plan and marketing"), Status = "On sale", StatusTone = UiRole.Ok };
        m.HeadActions = new[] { TechAct(runs, W(t, "reallife_tech_button_withdraw", "Withdraw"), UiButtonKind.Destructive, UiAction.Count) };
        var cards = runs.Runs;
        var market = new TechCard(cards, "Marketing");
        market.Say("Hype at 64%. A game this good carries it.").Step("Beat", "Trailer", "A trailer costs $12,000 and adds hype.", true, true, () => true, () => true)
            .Step("Campaign", "$40,000", "Spent $0 so far over 30 days.", true, true, () => true, () => true).Button("Run the beat", UiButtonKind.Secondary, UiAction.Count, () => true);
        var sales = new TechCard(cards, "Sales");
        sales.Say("Score 81. Sold 38,400 copies for $812,300 kept.").Say("Bugs reported: 14.", UiRole.WarnText).Say("Return on its cost: 410%.", UiRole.OkText);
        var team = new TechCard(cards, "Team");
        team.Step("Bonus", "5%", "Paid on the bonus day if the game lands.", true, true, () => true, () => true).Step("Share of the studio", "75%", "The rest goes to other work.", true, true, () => true, () => true);
        m.Left = new[] { market.Build(), team.Build() };
        m.Right = new[] { sales.Build() };
    }

    // ------------------------------------------------------------------------------------------------- a data centre

    private static void TechCentre(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Overview;
        m.Title = "Hudson Data Hall";
        m.Icon = TechGlyphs.Centre;
        m.Nav = TechTabNav(t, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 3;
        m.Back = TW(t, "back_infra", "Infra");
        m.Next = "Hudson Data Hall uses 84% of its floor. Order up to 10 more racks.";
        m.Actions = new[]
        {
            TechAct(runs, TW(t, "dc_details", "Details"), UiButtonKind.Secondary, UiAction.Count), TechAct(runs, TW(t, "see_contracts", "See contracts"), UiButtonKind.Secondary, UiAction.Option),
            TechAct(runs, "Order 10 racks", UiButtonKind.Primary, UiAction.Alt)
        };
        m.Meters = new[]
        {
            new TechMeter { Label = "Racks", Fill = 84, Figure = "184 of 220", Tone = UiRole.Warn }, new TechMeter { Label = "Cooling", Fill = 71, Figure = "71% of plant", Tone = UiRole.Ok },
            new TechMeter { Label = "Power", Fill = 84, Figure = "2.9 of 3.5 MW", Tone = UiRole.Warn }, new TechMeter { Label = "Bandwidth", Fill = 62, Figure = "6.2 of 10.0 Gbps", Tone = UiRole.Ok }
        };
        m.MetersTitle = TW(t, "capacity", "Capacity");
        m.App = new UiAppModel
        {
            Hero = new UiHeroModel { Icon = TechGlyphs.Centre, Kicker = "TIER III DATA CENTRE", Title = "Hudson Data Hall", Line = "Racks in use: 184 of 220. Uptime 99.7% over 90 days.", Progress = 84 },
            Figures = new[] { new UiFigureModel { Label = TW(t, "revenue_30", "Revenue, 30 days"), Value = "$96,400", Change = "+6%", Rising = true, Good = true, Trend = TechSpark(24, 60f, 96f, 4f) } },
            TableTitle = TW(t, "colo_title", "Colocation contracts"),
            Columns = new[]
            {
                new UiColumn { Title = "Client" }, new UiColumn { Title = TW(t, "col_racks", "Racks"), Width = 72f, Right = true }, new UiColumn { Title = TW(t, "col_per_month", "Per month"), Width = 96f, Right = true },
                new UiColumn { Title = TW(t, "col_ends", "Ends"), Width = 88f, Right = true }, new UiColumn { Title = TW(t, "col_status", "Status"), Width = 144f, Right = true }
            },
            GradeColumn = -1,
            Rows = new[]
            {
                new UiAppRow { Icon = UiIcon.Building, Grade = -1, Status = "Moving in", StatusTone = UiRole.Accent, Cells = new[] { "Harbourside Freight", "24", "$38,400", "Day 460", "Moving in" } },
                new UiAppRow { Icon = UiIcon.Building, Grade = -1, Status = "Rival offer", StatusTone = UiRole.Warn, Cells = new[] { "Midtown Box Office", "34", "$40,800", "Day 431", "Rival offer" } },
                new UiAppRow { Icon = UiIcon.Building, Grade = -1, Status = "Ends in 16 days", StatusTone = UiRole.Warn, Cells = new[] { "Saltmarsh Studios", "24", "$28,800", "Day 418", "Ends in 16 days" } },
                new UiAppRow { Icon = UiIcon.Building, Grade = -1, Status = "Active", StatusTone = UiRole.Ok, Cells = new[] { "Broadway Ticketing", "16", "$19,200", "Day 455", "Active" } },
                new UiAppRow { Icon = UiIcon.Building, Grade = -1, Status = "Active", StatusTone = UiRole.Ok, Cells = new[] { "Garment District Weavers", "10", "$6,500", "Day 500", "Active" } }
            },
            PeopleTitle = TW(t, "crew", "Crew"), PeopleCount = "9 people",
            People = new[]
            {
                TechPerson("Nadia Rahman", UiIcon.Wrench, "Site manager", 84, 76), TechPerson("Owen Pike", UiIcon.HardDrives, "Network engineer", 79, 68), TechPerson("Leila Haddad", UiIcon.Lightning, "Electrician", 88, 72),
                TechPerson("Ben Okoye", UiIcon.HardDrives, "Ops engineer", 66, 59), TechPerson("Ines Duarte", UiIcon.ShieldCheck, "Security", 72, 81), TechPerson("Joe Tran", UiIcon.Wrench, "Technician", 54, 47)
            },
            Note = "A rack costs $40,000, an accelerator rack $250,000.", NoteTone = UiRole.Ok, NoteAction = TW(t, "open_funding", "Open Funding")
        };
        m.RowKeys = new string[5];
        m.PersonKeys = new string[6];
        m.NoteRun = runs.Add();
    }

    // ------------------------------------------------------------------------------------------------- the tabs

    private static UiDataTableModel TechTable(UiWords t, UiDataColumn[] columns, List<UiDataRow> rows, string emptyTitle, string emptyBody) =>
        new UiDataTableModel { Columns = columns, Rows = rows.ToArray(), RowCount = rows.Count, Selected = -1, EmptyTitle = emptyTitle, EmptyBody = emptyBody, EmptyIcon = UiIcon.Package };

    private static void TechProducts(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Table;
        m.Nav = TechTabNav(t, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 1;
        var rows = new List<UiDataRow>();
        void Add(UiIcon icon, string name, string sub, string firm, string state, UiRole tone, string progress, string customers, string month, int quality, string bugs)
        {
            UiDataRow r = TechRow(8);
            r.Cells[0] = name; r.Sub = sub; r.Values[0] = (int)icon; r.Cells[1] = firm; r.Cells[2] = state; r.Tones[2] = tone; r.Cells[3] = progress; r.Cells[4] = customers; r.Cells[5] = month;
            r.Cells[6] = quality.ToString(); r.Values[6] = quality; r.Cells[7] = bugs;
            rows.Add(r);
        }
        Add(UiIcon.GameController, "Ferry Tycoon", "Game", "Pioneer Game Studio", "Building", UiRole.Notice, "64%", "0", "$0", 70, "3");
        Add(UiIcon.GameController, "Block Party Panic", "Game", "Pioneer Game Studio", "On sale", UiRole.Ok, "100%", "38,400", "$0", 81, "14");
        Add(UiIcon.Cloud, "Atlas Backup", "Subscription service, Business", "Atlas Edge Software", "On sale", UiRole.Ok, "5 vs 4", "1,840", "$22,100", 72, "6");
        Add(UiIcon.Brain, "Atlas Sense", "AI model", "Atlas Edge Software", "Ready", UiRole.Ok, "4 vs 5", "310", "$8,900", 64, "2");
        Add(UiIcon.Key, "Atlas Licence Kit", "Licensed software", "Atlas Edge Software", "Withdrawn", UiRole.Warn, "5 vs 5", "12", "$0", 55, "9");
        m.Table = TechTable(t, new[]
        {
            TechCol(t, "reallife_tech_col_product", "Product", UiCellKind.Tile), TechCol(t, "reallife_tech_col_firm", "Firm", UiCellKind.Text, 132f), TechCol(t, "reallife_ui_tech_col_status", "Status", UiCellKind.Chip, 112f),
            TechCol(t, "reallife_ui_tech_col_progress", "Progress", UiCellKind.Text, 96f), TechCol(t, "reallife_ui_tech_col_customers", "Customers", UiCellKind.Figure, 84f),
            TechCol(t, "reallife_ui_tech_col_month", "A month", UiCellKind.Money, 96f), TechCol(t, "reallife_ui_tech_col_quality", "Quality", UiCellKind.Figure, 72f, true),
            TechCol(t, "reallife_tech_col_bugs", "Bugs", UiCellKind.Figure, 56f)
        }, rows, "No products yet", "");
        m.RowKeys = new[] { "prod:1", "prod:2", "prod:3", "prod:4", "prod:5" };
        m.Next = "11 products may be built or on sale across your firms, 4 for each firm, and withdrawn ones stay on the list for 10.";
        m.Actions = new[] { TechAct(runs, W(t, "reallife_tech_button_new_product", "New product"), UiButtonKind.Primary, UiAction.Alt) };
    }

    private static void TechProduct(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Sheet;
        m.Nav = TechTabNav(t, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 1;
        m.Back = W(t, "reallife_tech_tab_products", "Products");
        m.Head = new UiHeadModel { Name = "Atlas Backup", Icon = UiIcon.Cloud, Role = "Subscription service, Atlas Edge Software", Status = "On sale", StatusTone = UiRole.Ok };
        m.HeadActions = new[]
        {
            TechAct(runs, W(t, "reallife_tech_button_withdraw", "Withdraw"), UiButtonKind.Destructive, UiAction.Count),
            TechAct(runs, W(t, "reallife_tech_button_launch", "Launch"), UiButtonKind.Primary, UiAction.Alt)
        };
        m.Figures = new[]
        {
            new UiFigureModel { Label = "Customers", Value = "1,840" }, new UiFigureModel { Label = "A month", Value = "$22,100" }, new UiFigureModel { Label = "Quality", Value = "72%" },
            new UiFigureModel { Label = "Bugs", Value = "6" }
        };
        var cards = runs.Runs;
        var now = new TechCard(cards, "Now");
        now.Say("5 features, 1 rival product at 4. The next feature needs 120 more work points.").Say("1,840 customers, 3,100 free. Takings $22,100 a month. Support needs 4 people, you have 3.", UiRole.WarnText)
            .Say("Quality 72% with 6 bugs, appeal 68%.");
        var settings = new TechCard(cards, TW(t, "settings", "Settings"));
        settings.Step(TW(t, "price", "Price"), "Fair, $60 a month", null, true, true, () => true, () => true).Step(TW(t, "marketing", "Marketing"), "Some ads", null, true, true, () => true, () => true)
            .Step(TW(t, "time_share", "Share of time"), "75% of the team's time", null, true, true, () => true, () => true);
        m.Left = new[] { now.Build() };
        m.Right = new[] { settings.Build() };
    }

    private static void TechContracts(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Table;
        m.Nav = TechTabNav(t, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 2;
        var rows = new List<UiDataRow>();
        void Add(UiIcon icon, string client, string work, string firm, string value, string progress, string due, int sat, string stage, UiRole tone)
        {
            UiDataRow r = TechRow(7);
            r.Cells[0] = client; r.Sub = work; r.Values[0] = (int)icon; r.Cells[1] = firm; r.Cells[2] = value; r.Cells[3] = progress; r.Cells[4] = due; r.Cells[5] = sat + "%"; r.Values[5] = sat;
            r.Cells[6] = stage; r.Tones[6] = tone;
            rows.Add(r);
        }
        Add(UiIcon.Globe, "Hargreave Hardware", "Website build", "Midtown Fibre Consultancy", "$18,000", "-", "Day 412", 0, "Offered", UiRole.Ok);
        Add(UiIcon.ClipboardText, "City Council", "Security audit", "-", "$64,000", "-", "Day 419", 0, "Tender", UiRole.Notice);
        Add(UiIcon.Gear, "Wells and Brook Law", "Managed service", "Midtown Fibre Consultancy", "$4,800", "-", "-", 82, "Under way", UiRole.Ok);
        Add(UiIcon.Arrows, "Corner Bakery Group", "Systems integration", "Atlas Edge Software", "$26,500", "60%", "Day 430", 74, "Under way", UiRole.Ok);
        Add(UiIcon.ShieldCheck, "Pike Dental", "Penetration test", "Atlas Edge Software", "$9,200", "100%", "Day 388", 90, "Done", UiRole.Ok);
        m.Table = TechTable(t, new[]
        {
            TechCol(t, "reallife_tech_col_client", "Client", UiCellKind.Tile), TechCol(t, "reallife_tech_col_firm", "Firm", UiCellKind.Text, 132f), TechCol(t, "reallife_tech_col_value", "Value", UiCellKind.Money, 100f),
            TechCol(t, "reallife_ui_tech_col_progress", "Progress", UiCellKind.Text, 80f), TechCol(t, "reallife_ui_tech_col_due", "Due", UiCellKind.Text, 88f),
            TechCol(t, "reallife_tech_col_satisfaction", "Satisfaction", UiCellKind.Meter, 120f), TechCol(t, "reallife_ui_tech_col_status", "Status", UiCellKind.Chip, 120f)
        }, rows, "Contracts", "");
        m.RowKeys = new[] { "work:1", "work:2", "work:3", "work:4", "work:5" };
        m.Next = "2 contract offers and tenders wait for your answer.";
    }

    private static void TechTender(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Sheet;
        m.Nav = TechTabNav(t, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 2;
        m.Back = TW(t, "back_contracts", "Contracts");
        m.Head = new UiHeadModel { Name = "City Council", Icon = UiIcon.ClipboardText, Role = "Security audit", Status = "Tender", StatusTone = UiRole.Notice };
        m.HeadActions = new[]
        {
            TechAct(runs, W(t, "reallife_tech_button_decline", "Decline"), UiButtonKind.Destructive, UiAction.Count), TechAct(runs, W(t, "reallife_tech_button_bid", "Bid"), UiButtonKind.Primary, UiAction.Alt)
        };
        var cards = runs.Runs;
        var now = new TechCard(cards, "Now");
        now.Say("The council wants three bids by day 419. 2 other firms may bid.").Say("Also bidding: Circuitry Works, Fibreway Networks.");
        var terms = new TechCard(cards, "Terms");
        terms.Say("A fixed price of $64,000 for 320 work points over 21 days, 30% paid up front.")
            .Step("Firm", "Atlas Edge Software", null, true, true, () => true, () => true)
            .Step(TW(t, "bid", "Bid"), "$64,000 at 100% of the fair price", null, true, true, () => true, () => true);
        m.Left = new[] { now.Build() };
        m.Right = new[] { terms.Build() };
    }

    private static void TechInfra(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Table;
        m.Nav = TechTabNav(t, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 3;
        var rows = new List<UiDataRow>();
        UiDataRow cloud = TechRow(7);
        cloud.Cells[0] = "Harbourline Cloud"; cloud.Sub = TW(t, "cloud_sub", "Cloud services"); cloud.Values[0] = (int)TechGlyphs.Cloud; cloud.Cells[6] = "Cloud"; cloud.Tones[6] = UiRole.Ok;
        rows.Add(cloud);
        void Centre(string name, string sub, string racks, int gpu, int let, string techs, string takings, string status, UiRole tone)
        {
            UiDataRow r = TechRow(7);
            r.Cells[0] = name; r.Sub = sub; r.Values[0] = (int)TechGlyphs.Centre; r.Cells[1] = racks; r.Cells[2] = gpu.ToString(); r.Values[2] = gpu; r.Cells[3] = let.ToString(); r.Values[3] = let;
            r.Cells[4] = techs; r.Cells[5] = takings; r.Cells[6] = status; r.Tones[6] = tone;
            rows.Add(r);
        }
        Centre("Hudson Data Hall", "Tier III data centre, Hell's Kitchen", "184 / 220", 12, 56, "9 / 9", "$96,400", "Nearly full", UiRole.Warn);
        Centre("Garment District Annex", "Tier II data centre, Garment District", "48 / 120", 0, 18, "2 / 3", "$21,300", "Running", UiRole.Ok);
        UiDataRow warehouse = TechRow(7);
        warehouse.Cells[0] = "Pier 9 Warehouse"; warehouse.Sub = "Harbour"; warehouse.Values[0] = (int)TechGlyphs.Warehouse; warehouse.Cells[6] = "Warehouse"; warehouse.Tones[6] = UiRole.Notice;
        rows.Add(warehouse);
        m.Table = TechTable(t, new[]
        {
            TechCol(t, "reallife_ui_tech_col_site", "Site", UiCellKind.Tile), TechCol(t, "reallife_infra_col_racks", "Racks", UiCellKind.Text, 100f), TechCol(t, "reallife_infra_col_gpu", "AI racks", UiCellKind.Figure, 72f),
            TechCol(t, "reallife_infra_col_let", "Let", UiCellKind.Figure, 80f), TechCol(t, "reallife_infra_col_technicians", "Technicians", UiCellKind.Text, 84f),
            TechCol(t, "reallife_ui_tech_col_takings", "Takings, 30 days", UiCellKind.Money, 104f), TechCol(t, "reallife_ui_tech_col_status", "Status", UiCellKind.Chip, 136f)
        }, rows, "", "");
        m.RowKeys = new[] { "cloud", "dc:a", "dc:b", "fit:c" };
        m.Next = "Your cloud sold 480 units yesterday, 120 to your own firms, 40 AI units, at 92% of the rivals' price.";
        var side = new TechCard(new TechRuns(), "Racks in the city's firms");
        side.Say("The city's firms rent 640 racks and 5,200 units of compute a day. The power price is steady.");
        m.Side = side.Build();
    }

    private static void TechStartups(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Table;
        m.Nav = TechTabNav(t, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 4;
        var rows = new List<UiDataRow>();
        void Add(string name, string founder, string sector, string stage, string revenue, string value, string share, string yours, string state, UiRole tone)
        {
            UiDataRow r = TechRow(8);
            r.Cells[0] = name; r.Sub = founder; r.Values[0] = (int)TechGlyphs.Startup; r.Cells[1] = sector; r.Cells[2] = stage; r.Cells[3] = revenue; r.Cells[4] = value; r.Cells[5] = share;
            r.Cells[6] = yours; r.Cells[7] = state; r.Tones[7] = tone;
            rows.Add(r);
        }
        Add("Quill Software", "Priya Raman", "Software", "Seed", "$12,900", "$2,400,000", "22%", "$528,000", "Running", UiRole.Notice);
        Add("Loop Labs", "Tomas Ondrej", "Games", "Pre-seed", "$3,200", "$900,000", "40%", "$360,000", "Running", UiRole.Notice);
        Add("Pocket Orbit", "Sara Bell", "Fintech", "Series A", "$88,000 *", "$18,500,000", "-", "$0", "Asks you", UiRole.Ok);
        Add("Greenleaf Grid", "Marta Haig", "Climate", "Seed", "$21,000 *", "$4,100,000", "-", "$0", "Closed", UiRole.Warn);
        m.Table = TechTable(t, new[]
        {
            TechCol(t, "reallife_vc_col_startup", "Startup", UiCellKind.Tile), TechCol(t, "reallife_vc_col_sector", "Sector", UiCellKind.Text, 120f), TechCol(t, "reallife_vc_col_stage", "Stage", UiCellKind.Text, 96f),
            TechCol(t, "reallife_vc_col_revenue", "Revenue", UiCellKind.Money, 100f), TechCol(t, "reallife_vc_col_value", "Value", UiCellKind.Money, 112f), TechCol(t, "reallife_vc_col_share", "Share", UiCellKind.Text, 72f),
            TechCol(t, "reallife_vc_col_yours", "Yours", UiCellKind.Money, 100f), TechCol(t, "reallife_ui_tech_col_status", "Status", UiCellKind.Chip, 120f)
        }, rows, "", "");
        m.RowKeys = new[] { "start:1", "start:2", "start:3", "start:4" };
        m.Next = "You hold 2 stakes, put in $750,000, worth $888,000 now. Hype 120%.";
        m.Actions = new[]
        {
            TechAct(runs, W(t, "reallife_niche_button_niches", "Niches"), UiButtonKind.Secondary, UiAction.Option), TechAct(runs, W(t, "reallife_found_button_open", "Found a startup"), UiButtonKind.Primary, UiAction.Alt)
        };
        var fund = new TechCard(runs.Runs, TW(t, "fund_title", "Your fund"));
        fund.Say("Committed $5,000,000, cash $3,200,000, invested $1,800,000, returned $0.")
            .Step(TW(t, "fund_step", "Put in"), "$5,000,000", null, true, true, () => true, () => true).Button("Put money in", UiButtonKind.Secondary, UiAction.Count, () => true)
            .Button("Take cash out", UiButtonKind.Secondary, UiAction.Count, () => true);
        m.Side = fund.Build();
    }

    private static void TechStartup(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Sheet;
        m.Nav = TechTabNav(t, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 4;
        m.Back = TW(t, "back_startups", "Startups");
        m.Head = new UiHeadModel { Name = "Pocket Orbit", Icon = TechGlyphs.Startup, Role = "Fintech, Sara Bell", Status = "Asks you", StatusTone = UiRole.Ok };
        m.HeadActions = new[]
        {
            TechAct(runs, W(t, "reallife_vc_button_diligence", "Check the books"), UiButtonKind.Secondary, UiAction.Option), TechAct(runs, W(t, "reallife_vc_button_meet", "Meet the founders"), UiButtonKind.Primary, UiAction.Alt)
        };
        m.Figures = new[]
        {
            new UiFigureModel { Label = "Revenue", Value = "$88,000" }, new UiFigureModel { Label = "Value", Value = "$18,500,000" }, new UiFigureModel { Label = "Stage", Value = "Series A" },
            new UiFigureModel { Label = "Yours", Value = "-" }
        };
        var cards = runs.Runs;
        var facts = new TechCard(cards, TW(t, "startup_facts", "The startup"));
        facts.Say("Asks $2,000,000 at $16,500,000 before the money, for 10.8%. The offer lapses on day 420.", UiRole.OkText).Say("Founded by Sara Bell on day 88, at Series A. It says it takes $88,000 a month.")
            .Say("Its books have not been checked. A check costs $5,000.")
            .Say(WF(t, "reallife_niche_startup", "Sells to: Restaurants.", "niche", W(t, "reallife_niche_restaurants", "Restaurants")));
        m.Left = new[] { facts.Build() };
    }

    private static void TechFunding(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Table;
        m.Nav = TechTabNav(t, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 5;
        var rows = new List<UiDataRow>();
        void Add(UiIcon icon, string name, string kind, string round, string share, string value, string raised, string board, string runway, string state, UiRole tone, UiRole runTone = UiRole.None)
        {
            UiDataRow r = TechRow(8);
            r.Cells[0] = name; r.Sub = kind; r.Values[0] = (int)icon; r.Cells[1] = round; r.Cells[2] = share; r.Cells[3] = value; r.Cells[4] = raised; r.Cells[5] = board; r.Cells[6] = runway;
            r.Tones[6] = runTone; r.Cells[7] = state; r.Tones[7] = tone;
            rows.Add(r);
        }
        Add(UiIcon.GameController, "Pioneer Game Studio", "Game studio", "Series A", "68%", "$6,200,000", "$1,500,000", "1 / 3", "190", "Raising", UiRole.Notice);
        Add(UiIcon.Code, "Atlas Edge Software", "Software", "Bootstrapped", "100%", "$0", "$0", "-", "-", "Steady", UiRole.Ok);
        Add(UiIcon.Handshake, "Midtown Fibre Consultancy", "Consultancy", "Seed", "81%", "$1,100,000", "$250,000", "0 / 2", "42", "Warned", UiRole.Warn, UiRole.DangerText);
        m.Table = TechTable(t, new[]
        {
            TechCol(t, "reallife_tech_col_firm", "Firm", UiCellKind.Tile), TechCol(t, "reallife_vc_col_stage", "Round", UiCellKind.Text, 104f), TechCol(t, "reallife_vc_col_share", "Share", UiCellKind.Text, 72f),
            TechCol(t, "reallife_vc_col_value", "Value", UiCellKind.Money, 100f), TechCol(t, "reallife_vc_col_raised", "Raised", UiCellKind.Money, 100f), TechCol(t, "reallife_vc_col_board", "Board", UiCellKind.Text, 72f),
            TechCol(t, "reallife_vc_col_runway", "Runway", UiCellKind.Figure, 76f), TechCol(t, "reallife_ui_tech_col_status", "Status", UiCellKind.Chip, 120f)
        }, rows, "", "");
        m.RowKeys = new[] { "cap:a", "cap:b", "cap:c" };
        m.Next = "A round takes 30 days and up to 3 meetings. Investors want 20% growth a month to a target in 180 days.";
        m.Actions = new[] { TechAct(runs, W(t, "reallife_found_button_open", "Found a startup"), UiButtonKind.Primary, UiAction.Alt) };
    }

    private static void TechCap(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Sheet;
        m.Nav = TechTabNav(t, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 5;
        m.Back = TW(t, "back_funding", "Funding");
        m.Head = new UiHeadModel { Name = "Pioneer Game Studio: owners", Icon = UiIcon.GameController, Role = "Game studio" };
        m.HeadActions = new[] { TechAct(runs, W(t, "reallife_vc_button_raise_page", "Raise money"), UiButtonKind.Primary, UiAction.Alt) };
        var cards = runs.Runs;
        var cap = new TechCard(cards, TW(t, "cap_table", "Who owns it"));
        cap.Say("It makes $12,000 a day more than it spends, so there is no runway worry.").Row("You", "68%").Row("Option pool", "8%").Row("Northgate Ventures", "24%")
            .Say("Board of 3 seats, 1 theirs. Target: $60,000 a month by day 520, now $41,000, 1 miss.")
            .Step("Chief executive", "You", null, true, true, () => true, () => true).Button("Grant options", UiButtonKind.Secondary, UiAction.Count, () => true)
            .Button("Sell the investors' shares", UiButtonKind.Secondary, UiAction.Count, () => true);
        m.Left = new[] { cap.Build() };
    }

    // A funded firm's page: the cap table with the firm account under it, the board's mood and what is counted as taken.
    private static void TechCapAccount(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Sheet;
        m.Nav = TechTabNav(t, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 5;
        m.Back = TW(t, "back_funding", "Funding");
        m.Head = new UiHeadModel { Name = "Pioneer Game Studio: owners", Icon = UiIcon.GameController, Role = "Game studio" };
        m.HeadActions = new[]
        {
            TechAct(runs, W(t, "reallife_vc_button_books", "Firm account"), UiButtonKind.Secondary, UiAction.Option),
            TechAct(runs, W(t, "reallife_vc_button_raise_page", "Raise money"), UiButtonKind.Primary, UiAction.Alt)
        };
        var cap = new TechCard(runs.Runs, TW(t, "cap_table", "Who owns it"));
        cap.Say("It spends $4,200 a day more than it makes, enough for 61 days.", UiRole.WarnText).Row("You", "68%").Row("Option pool", "8%").Row("Northgate Ventures", "24%")
            .Say("Board of 3 seats, 1 theirs. Target: $60,000 a month by day 520, now $41,000, 1 miss.")
            .Say(WF(t, "reallife_vc_acct_line", "Firm account $1,640,000, of which investors' $1,640,000. Your wallet must keep $1,640,000 while this firm runs.", "balance", "$1,640,000", "investors", "$1,640,000", "floor", "$1,640,000"))
            .Say(W(t, "reallife_vc_mood_2", "The board is asking about the books."), UiRole.WarnText)
            .Say(WF(t, "reallife_vc_cap_taken", "Investors' money counted as taken: $360,000.", "taken", "$360,000"), UiRole.WarnText)
            .Step("Chief executive", "You", null, true, true, () => true, () => true).Button("Grant options", UiButtonKind.Secondary, UiAction.Count, () => true);
        m.Left = new[] { cap.Build() };
    }

    // The firm account's own page: the books on the left, the lawful ways out and the board's question on the right.
    private static void TechBooks(UiWords t, TechModel m, TechRunList runs, bool asked)
    {
        m.Shape = TechShape.Sheet;
        m.Nav = TechTabNav(t, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 5;
        m.Back = TW(t, "back_cap", "Back to the firm");
        m.Head = new UiHeadModel { Name = WF(t, "reallife_vc_acct_title", "Firm account: Pioneer Game Studio", "firm", "Pioneer Game Studio"), Icon = UiIcon.Coin, Role = "Game studio" };
        var books = new TechCard(runs.Runs, TW(t, "books", "Firm account"));
        books.Say(WF(t, "reallife_vc_acct_line", "Firm account $1,640,000, of which investors' $1,640,000. Your wallet must keep $1,640,000 while this firm runs.", "balance", "$1,640,000", "investors", "$1,640,000", "floor", "$1,640,000"))
            .Row(TW(t, "books_wallet", "Your wallet"), "$1,280,000")
            .Say(WF(t, "reallife_vc_acct_taken", "Counted as taken: $360,000. Latest: Speedy Bites running costs, day 214. Putting cash back clears it.", "taken", "$360,000", "what", "Speedy Bites running costs", "day", "214"), UiRole.WarnText)
            .Say(WF(t, "reallife_vc_acct_pack", "Board pack: burning $4,200 a day, 61 days of runway, revenue $41,000 a month against a target of $60,000. Next pack on day 241.", "burn", "$4,200", "runway", "61", "revenue", "$41,000", "target", "$60,000", "day", "241"))
            .Say(WF(t, "reallife_vc_acct_related", "Deals with your other businesses: 2, $8,000 paid, of which $0 over the fair price and counted as taken.", "count", "2", "paid", "$8,000", "over", "$0"))
            .Say(WF(t, "reallife_vc_acct_salary", "Salary this month $9,000 of the $14,000 the board allows.", "drawn", "$9,000", "band", "$14,000"))
            .Say(WF(t, "reallife_vc_acct_loan_free", "A founder's loan of up to $100,000 can be taken once, repaid within 180 days with interest.", "max", "$100,000", "days", "180"))
            .Say(asked ? W(t, "reallife_vc_mood_2", "The board is asking about the books.") : W(t, "reallife_vc_mood_1", "The board is a little uneasy about the books."), asked ? UiRole.WarnText : UiRole.None);
        m.Left = new[] { books.Build() };
        var out1 = new TechCard(runs.Runs, TW(t, "books_out", "Taking money out"));
        if (asked)
        {
            out1.Say(W(t, "reallife_vc_ask_line", "The board has asked about the books. Answer here or at your desk."), UiRole.WarnText)
                .Button(W(t, "reallife_vc_button_answer_repay", "Repay"), UiButtonKind.Primary, UiAction.Count, () => true)
                .Button(W(t, "reallife_vc_button_answer_explain", "Explain"), UiButtonKind.Secondary, UiAction.Count, () => true)
                .Button(W(t, "reallife_vc_button_answer_stonewall", "Say nothing"), UiButtonKind.Secondary, UiAction.Count, () => true);
            m.Right = new[] { out1.Build() };
            return;
        }
        out1.Button(W(t, "reallife_vc_button_repay", "Put cash back"), UiButtonKind.Primary, UiAction.Count, () => true)
            .Button(W(t, "reallife_vc_button_salary", "Draw salary"), UiButtonKind.Secondary, UiAction.Count, () => true)
            .Button(W(t, "reallife_vc_button_loan", "Take a founder's loan"), UiButtonKind.Secondary, UiAction.Count, () => true)
            .Button(W(t, "reallife_vc_button_dividend", "Pay a dividend"), UiButtonKind.Secondary, UiAction.Count, () => true)
            .Button(W(t, "reallife_vc_button_delay", "Delay the board pack"), UiButtonKind.Secondary, UiAction.Count, () => true)
            .Step(TW(t, "books_from", "Work from"), "Speedy Bites", null, true, true, () => true, () => true)
            .Step(TW(t, "books_price", "Price"), WF(t, "reallife_vc_related_tier_0", "At the fair price, $4,000", "price", "$4,000"), null, true, true, () => true, () => true)
            .Button(W(t, "reallife_vc_button_related", "Pay for work"), UiButtonKind.Secondary, UiAction.Count, () => true);
        m.Right = new[] { out1.Build() };
    }

    // A raise page with an offer on the table: its terms on the right, what they cost with Law, and the offer against the market.
    private static void TechTerms(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Sheet;
        m.Nav = TechTabNav(t, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 5;
        m.Back = TW(t, "back_cap", "Back to the firm");
        m.Head = new UiHeadModel { Name = "Pioneer Game Studio: raise", Icon = UiIcon.Coin, Role = "Game studio" };
        var left = new TechCard(runs.Runs, TW(t, "raise_round", "Raise a round"));
        left.Say("Ironbark Growth offers $2,400,000 for 18%.").Button(W(t, "reallife_vc_button_take_sheet", "Take the offer"), UiButtonKind.Primary, UiAction.Count, () => true);
        m.Left = new[] { left.Build() };
        var terms = new TechCard(runs.Runs, WF(t, "reallife_vc_terms_title", "Terms from Ironbark Growth", "vc", "Ironbark Growth"));
        terms.Row(W(t, "reallife_vc_terms_label_pref", "Liquidation preference"), W(t, "reallife_vc_terms_pref_2", "2x, paid back first"), UiRole.WarnText)
            .Row(W(t, "reallife_vc_terms_label_part", "Participation"), W(t, "reallife_vc_terms_part_1", "Participating: takes its preference, then shares as common"), UiRole.WarnText)
            .Row(W(t, "reallife_vc_terms_label_anti", "Anti-dilution"), W(t, "reallife_vc_terms_anti_2", "Full ratchet"), UiRole.WarnText)
            .Row(W(t, "reallife_vc_terms_label_vest", "Founder vesting"), W(t, "reallife_vc_terms_vest_1", "Your vesting starts again"), UiRole.WarnText)
            .Row(W(t, "reallife_vc_terms_label_info", "Information rights"), W(t, "reallife_vc_terms_info_2", "Monthly pack and a look on demand"))
            .Row(W(t, "reallife_vc_terms_label_prov", "Board approval"), WF(t, "reallife_vc_terms_prov", "Spending over $120,000 at once", "amount", "$120,000"))
            .Say(WF(t, "reallife_vc_terms_cost_pref", "The 2x preference costs you $310,000 in a sale for $13,000,000 and $0 in a sale for $39,000,000.", "multiple", "2x", "at1", "$310,000", "value1", "$13,000,000", "at3", "$0", "value3", "$39,000,000"))
            .Say(WF(t, "reallife_vc_terms_cost_part", "Participating costs you a further $540,000 and $1,150,000.", "at1", "$540,000", "at3", "$1,150,000"))
            .Say(WF(t, "reallife_vc_terms_cost_anti", "If a later round prices 40% lower, you would hold 52% of the firm instead of 47%.", "cut", "40%", "with", "47%", "without", "52%"))
            .Say(WF(t, "reallife_vc_terms_hard", "Hard terms: Liquidation preference, Anti-dilution.", "terms", "Liquidation preference, Anti-dilution"), UiRole.WarnText)
            .Say(WF(t, "reallife_vc_terms_market", "A fair value for the firm today is $9,800,000 before the money. This offer values it at $10,600,000, which is 108% of that.", "market", "$9,800,000", "pre", "$10,600,000", "percent", "108"));
        m.Right = new[] { terms.Build() };
    }

    private static void TechTeam(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Table;
        m.Title = "Pioneer Game Studio";
        m.Icon = UiIcon.GameController;
        m.Nav = TechFirmNav(t, true, 3, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 3;
        var rows = new List<UiDataRow>();
        void Add(string name, string role, string level, int skill, int morale, int hours, UiRole ring)
        {
            UiDataRow r = TechRow(5);
            r.Cells[0] = name; r.Sub = role; r.Hue = UiColour.HueOf(name); r.Ring = ring; r.Cells[1] = level; r.Cells[2] = skill + "%"; r.Values[2] = skill; r.Cells[3] = morale + "%"; r.Values[3] = morale;
            r.Cells[4] = hours.ToString(); r.Values[4] = hours;
            rows.Add(r);
        }
        Add("Ava Lindqvist", "Lead engineer", "Senior", 86, 72, 8, UiRole.Ok);
        Add("Marcus Hale", "Software engineer", "Mid", 64, 58, 8, UiRole.Warn);
        Add("Priya Raman", "Game designer", "Senior", 77, 81, 7, UiRole.Ok);
        Add("Tom Becker", "Level designer", "Junior", 52, 44, 8, UiRole.Danger);
        Add("Elena Sokolova", "QA tester", "Mid", 61, 66, 6, UiRole.Ok);
        m.Table = TechTable(t, new[]
        {
            TechCol(t, "reallife_ui_tech_col_person", "Person", UiCellKind.Person), TechCol(t, "reallife_ui_tech_col_level", "Level", UiCellKind.Text, 120f), TechCol(t, "reallife_ui_tech_skill", "Skill", UiCellKind.Meter, 160f),
            TechCol(t, "reallife_ui_tech_morale", "Morale", UiCellKind.Meter, 160f), TechCol(t, "reallife_ui_tech_col_hours", "Hours", UiCellKind.Figure, 96f)
        }, rows, "Nobody works here yet", "");
        m.RowKeys = new string[0];
        m.Next = TW(t, "team_next", "Next: keep the team skilled and in good heart.");
        m.Actions = new[] { TechAct(runs, TW(t, "open_roles", "Open Roles"), UiButtonKind.Primary, UiAction.Alt) };
    }

    private static void TechRivals(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Table;
        m.Title = "Atlas Edge Software";
        m.Icon = UiIcon.Code;
        m.Nav = TechFirmNav(t, false, 5, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 5;
        var rows = new List<UiDataRow>();
        void Add(UiIcon icon, string name, string kind, string meet, int products, int tenders, string where)
        {
            UiDataRow r = TechRow(5);
            r.Cells[0] = name; r.Sub = kind; r.Values[0] = (int)icon; r.Cells[1] = meet; r.Cells[2] = products.ToString(); r.Values[2] = products; r.Cells[3] = tenders.ToString(); r.Values[3] = tenders; r.Cells[4] = where;
            rows.Add(r);
        }
        Add(UiIcon.Building, "Codeharbour Software", "A listed company", "Licensed software, Systems integration", 2, 1, "Skyline Tower, floor 12");
        Add(UiIcon.Building, "Abacus Lane Software", "A listed company", "Subscription service", 1, 0, "Harbour Court, floor 4");
        Add(TechGlyphs.Startup, "Quill Software", "A startup", "Software", 0, 0, "-");
        m.Table = TechTable(t, new[]
        {
            TechCol(t, "reallife_tech_col_rival", "Competitor", UiCellKind.Tile), TechCol(t, "reallife_tech_col_meet", "Where you meet", UiCellKind.Text, 220f), TechCol(t, "reallife_tech_col_products", "Products", UiCellKind.Figure, 96f),
            TechCol(t, "reallife_tech_col_tenders", "Tenders", UiCellKind.Figure, 96f), TechCol(t, "reallife_tech_col_where", "Where", UiCellKind.Text, 160f)
        }, rows, "No competitors yet", "");
        m.RowKeys = new[] { "rival:a|c0", "rival:a|c2", "rival:a|s1" };
        m.Next = "Competitors of Atlas Edge Software.";
    }

    private static void TechConcept(UiWords t, TechModel m, TechRunList runs)
    {
        m.Shape = TechShape.Sheet;
        m.Title = "Pioneer Game Studio";
        m.Icon = UiIcon.GameController;
        m.Nav = TechFirmNav(t, true, 2, out string[] keys);
        m.NavKeys = keys;
        m.NavSelected = 2;
        m.Back = TW(t, "back_products", "Products");
        m.Head = new UiHeadModel { Name = "A new game for Pioneer Game Studio", Icon = UiIcon.GameController, Role = "Draft" };
        m.HeadActions = new[] { TechAct(runs, W(t, "reallife_studio_button_desk", "Pitch it at the desk"), UiButtonKind.Primary, UiAction.Alt) };
        var cards = runs.Runs;
        var game = new TechCard(cards, "The game");
        game.Step("Genre", "Simulation", "Players care about design and content. A usual game is mid-sized.", true, true, () => true, () => true)
            .Step("Setting", "Harbour", "A harbour setting suits builders.", true, true, () => true, () => true)
            .Step("Platform", "PC and console", "About 52,000 people play on PC.", true, true, () => true, () => true)
            .Step("Engine", "Licensed", "A licence costs $20,000 and 5% of sales past the first million.", true, true, () => true, () => true);
        var money = new TechCard(cards, "Budget and price");
        money.Step("Budget", "$400,000, mid-sized", "The team's pace makes it 140 days.", true, true, () => true, () => true).Step("Price", "$24.99", "The usual price is $24.99.", true, true, () => true, () => true);
        var outlook = new TechCard(cards, "What to expect");
        outlook.Say("About 14% of the audience will see it, 7,300 people.").Say("Simulation critics expect a score of 74.").Say("2 rival simulation games come out in the 60 days either side.", UiRole.WarnText);
        m.Left = new[] { game.Build(), money.Build() };
        m.Right = new[] { outlook.Build() };
    }
}
