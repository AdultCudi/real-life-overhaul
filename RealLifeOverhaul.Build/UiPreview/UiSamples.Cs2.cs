using RealLifeOverhaul.Ui;

/// <summary>
/// The prototype's screens in the Cities: Skylines II look (design/DIRECTION_CS2.md, section 7): a person's info panel,
/// the city's people as an info view, the game studio and Contacts as app windows, a desk session as a decision card and
/// the phone's home, each with the status bar's sample. Plain English, as the owner reads the previews.
/// </summary>
internal static partial class UiSamples
{
    /// <summary>The toolbar's apps in the status bar, in the phone's order, each in its category's colour.</summary>
    public static readonly UiIcon[] Apps =
    {
        UiIcon.AddressBook, UiIcon.Message, UiIcon.Briefcase, UiIcon.Bank, UiIcon.ChartPie, UiIcon.ChartLineUp, UiIcon.Cpu, UiIcon.Fire, UiIcon.MapTrifold,
        UiIcon.Calendar, UiIcon.ShoppingCart, UiIcon.Gear
    };

    public static readonly UiRole[] AppTints = System.Array.ConvertAll(Apps, UiIconTints.Of);   // each app in its icon's own colour, as the style sheet shows it

    public const int AppContacts = 0, AppBusiness = 2, AppTech = 6;

    /// <summary>The status bar on a Tuesday afternoon: the time and speed, the season, the place, the money and the player's needs.</summary>
    public static UiStatusModel Bar(int app) => new UiStatusModel
    {
        Time = "14:32", Day = "Tue, Day 12", Speed = 2, Season = "Autumn", Weather = UiIcon.CloudSun, Temperature = "14\u00b0C", Place = "Midtown", Money = "$48,210", MoneyTrend = 1, Energy = 72,
        Hunger = 48, Happiness = 64, Alerts = 3, Apps = Apps, AppTints = AppTints, App = app
    };

    /// <summary>A person's info panel, docked at the top left with its tabs: who they are, their household and home, their work.</summary>
    public static UiInfoModel Person(UiWords t)
    {
        var m = new UiInfoModel
        {
            Title = "Rosa Stanley", Icon = UiIcon.Person, Tabs = new[] { UiIcon.People, UiIcon.Person, UiIcon.Home, UiIcon.Briefcase },
            Tab = 1, Name = "Rosa Stanley",
            Hue = 160f, Ring = UiRole.Ok, Status = "Content", StatusIcon = UiIcon.SmileyHappy, StatusTone = UiRole.Ok, Facts = "Age 39, shift manager at Bloom Café",
            Sections = new[]
            {
                new UiInfoSection
                {
                    Label = "Citizen", Value = "At work",
                    Rows = new[]
                    {
                        new UiInfoRow { Label = "Schooling", Value = "Bachelor's degree" }, new UiInfoRow { Label = "Line of work", Value = "Food and drink" },
                        new UiInfoRow { Label = "Pay a day", Value = "$184, above the median", Ink = UiRole.OkText },
                    }
                },
                new UiInfoSection
                {
                    Label = "Household", Value = "The Stanley household", Link = true,
                    Rows = new[]
                    {
                        new UiInfoRow { Label = "Household wealth", Value = "Comfortable", Ink = UiRole.OkText }, new UiInfoRow { Label = "Residence", Value = "70 Fifth Avenue", Link = true },
                        new UiInfoRow { Label = "Rent", Value = "24% of income", Ink = UiRole.OkText },
                    }
                },
                new UiInfoSection
                {
                    Label = "Work", Value = "Bloom Café", Link = true,
                    Rows = new[] { new UiInfoRow { Label = "Hours a week", Value = "38" }, new UiInfoRow { Label = "Commute", Value = "22 min by subway" } }
                },
            },
            Footer = new[] { UiIcon.Locate, UiIcon.Eye, UiIcon.Phone },
        };
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>The city's people as an info view: the headline rows, the age groups as a donut, the map's legend, the other views beside it.</summary>
    public static UiInfoViewModel People(UiWords t)
    {
        var m = new UiInfoViewModel
        {
            Title = "The city's people", Icon = UiIcon.People,
            Views = new[] { UiIcon.People, UiIcon.ChartLineUp, UiIcon.Briefcase, UiIcon.Key, UiIcon.MapTrifold, UiIcon.Store, UiIcon.Car, UiIcon.ShieldCheck },
            View = 0, HeadLabel = "Population", HeadValue = "56,700",
            Rows = new[]
            {
                new UiInfoRow { Label = "Households", Value = "19,800" }, new UiInfoRow { Label = "Household size", Value = "2.86" },
                new UiInfoRow { Label = "Jobs in the city", Value = "110,800" }, new UiInfoRow { Label = "Filled from outside the city", Value = "73%" },
                new UiInfoRow { Label = "Out of work", Value = "6.4%", Ink = UiRole.WarnText },
                new UiInfoRow { Label = "Mean rent", Value = "$1,140" }, new UiInfoRow { Label = "Moved in this month", Value = "312" },
                new UiInfoRow { Label = "Moved away this month", Value = "278" },
            },
            ChartTitle = "Age groups", ChartLabels = new[] { "Children", "Young adults", "Adults", "Seniors" }, ChartValues = new[] { "18.4%", "21.9%", "41.2%", "18.5%" },
            ChartShares = new[] { 18.4f, 21.9f, 41.2f, 18.5f }, LegendTitle = "Map legend", ScaleLabel = "Residents per block", ScaleLow = "Few", ScaleHigh = "Many",
        };
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>The game studio as an app window: its sections down the side, its next step, its figures, its games with their genres and its team.</summary>
    public static UiAppModel Studio(UiWords t)
    {
        var m = new UiAppModel
        {
            Title = "Lantern Bay Games", Icon = UiIcon.GameController,
            Nav = new[]
            {
                new UiNavEntry { Icon = UiIcon.Grid, Label = "Overview" }, new UiNavEntry { Icon = UiIcon.GameController, Label = "Games", Count = "5" },
                new UiNavEntry { Icon = UiIcon.People, Label = "Team", Count = "12" }, new UiNavEntry { Icon = UiIcon.Globe, Label = "Market" },
                new UiNavEntry { Icon = UiIcon.Flag, Label = "Rivals", Count = "8" }, new UiNavEntry { Icon = UiIcon.Coin, Label = "Money" },
                new UiNavEntry { Icon = UiIcon.Trophy, Label = "Awards", Count = "2" },
            },
            Next = "Next: start a second game. Draft a concept, then pitch it at your desk.",
            Actions = new[]
            {
                new UiActionModel { Label = "Build our own engine", Kind = UiButtonKind.Secondary, Key = UiAction.Option, Command = UiAppWindow.ActionCommand },
                new UiActionModel { Label = "Start a game", Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiAppWindow.ActionCommand, Arg = 1 },
            },
            Hero = new UiHeroModel
            {
                Icon = UiIcon.GenreSimulation, Kicker = "In development", Title = "Cinder Valley", Line = "Simulation, 64% made, three of five milestones met", Progress = 64,
                Note = "Playtest in 6 days",
            },
            Figures = new[]
            {
                new UiFigureModel
                {
                    Label = "Takings, 30 days", Value = "$184,200",
                    Trend = new[] { 38f, 39f, 41f, 40f, 42f, 44f, 47f, 51f, 49f, 46f, 44f, 43f, 45f, 46f, 44f, 42f, 41f, 43f, 45f, 48f, 50f, 49f, 47f, 46f }
                },
            },
            TableTitle = "Games out",
            Columns = new[]
            {
                new UiColumn { Title = "Game" }, new UiColumn { Title = "Genre", Width = 112f }, new UiColumn { Title = "Score", Width = 56f, Right = true },
                new UiColumn { Title = "Copies", Width = 72f, Right = true }, new UiColumn { Title = "Earned", Width = 88f, Right = true },
                new UiColumn { Title = "Status", Width = 128f, Right = true },
            },
            GradeColumn = 2,
            Rows = new[]
            {
                Game(UiIcon.GenreAdventure, UiRole.Chart2, "Ember Isle", "Adventure", "78", "41,000", "$612,400", 78, Out(t, 96), UiRole.Ok),
                Game(UiIcon.GenreSimulation, UiRole.Chart3, "Tiny Towns", "Simulation", "74", "28,500", "$341,900", 74, Out(t, 212), UiRole.Ok),
                Game(UiIcon.GenreStrategy, UiRole.Chart5, "Deep Signal", "Strategy", "69", "12,600", "$128,300", 69, Out(t, 340), UiRole.Ok),
                Game(UiIcon.GenreSports, UiRole.Chart1, "Pocket Courts", "Sports", "58", "4,300", "$31,200", 58, "Withdrawn", UiRole.Warn),
            },
            MoreTitle = "The games market",
            MoreColumns = new[]
            {
                new UiColumn { Title = "Rival game" }, new UiColumn { Title = "Studio", Grow = true }, new UiColumn { Title = "Score", Width = 56f, Right = true },
                new UiColumn { Title = "Copies", Width = 72f, Right = true },
            },
            MoreGrade = 2,
            MoreRows = new[]
            {
                Rival(UiIcon.GenreAction, UiRole.Chart6, "Iron Tide", "Pixel Harbour Games", 82, "41,000"),
                Rival(UiIcon.GenreStrategy, UiRole.Chart5, "Crown and Ledger", "Saltmarsh Studios", 84, "57,200"),
                Rival(UiIcon.GenreRpg, UiRole.Chart7, "Ashen Road", "Hollowpine Interactive", 79, "33,600"),
                Rival(UiIcon.GenreSimulation, UiRole.Chart3, "Harbour Days", "Tidewater Interactive", 76, "28,900"),
                Rival(UiIcon.GenrePuzzle, UiRole.Chart4, "Lantern Puzzles", "Copperkettle Games", 71, "12,300"),
                Rival(UiIcon.GenreHorror, UiRole.CatIndustry, "The Quiet House", "Blue Kestrel Games", 68, "9,400"),
                Rival(UiIcon.GenreRacing, UiRole.None, "Neon Laps", "Gridline Studio", 66, "8,100"),
                Rival(UiIcon.GenreFamily, UiRole.None, "Picnic Party", "Little Acorn Games", 63, "6,700"),
            },
            PeopleTitle = "Team", PeopleCount = "12 people",
            People = new[]
            {
                Person("Ava Lindqvist", UiIcon.Code, UiRole.CatTech, "Lead engineer", 86, 72),
                Person("Marcus Hale", UiIcon.Code, UiRole.CatTech, "Engineer", 64, 58),
                Person("Priya Raman", UiIcon.PaintBrush, UiRole.CatRetail, "Game designer", 77, 81),
                Person("Tom Becker", UiIcon.PaintBrush, UiRole.CatRetail, "Level designer", 52, 44),
                Person("Elena Sokolova", UiIcon.Bug, UiRole.Chart3, "Tester", 61, 66),
                Person("Jonah Reyes", UiIcon.Lightbulb, UiRole.Chart4, "Producer", 70, 38),
                Person("Kenji Mori", UiIcon.Code, UiRole.CatTech, "Engineer", 58, 61),
                Person("Amara Nwosu", UiIcon.PaintBrush, UiRole.CatRetail, "Artist", 74, 70),
                Person("Sofia Rossi", UiIcon.MusicNotes, UiRole.Chart7, "Sound designer", 66, 77),
                Person("Arjun Patel", UiIcon.Bug, UiRole.Chart3, "Tester", 49, 55),
                Person("Mei Lin", UiIcon.Database, UiRole.CatTech, "Data analyst", 81, 64),
                Person("Diego Alvarez", UiIcon.Megaphone, UiRole.Chart6, "Marketing", 63, 59),
            },
            PeopleMore = "See all 12 in Team",
            Note = "Support is needed: 120 tickets a day go unanswered. Hire people, then give them roles in Open Roles.", NoteAction = "Hire in the Business app",
        };
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    private static UiAppRow Game(UiIcon icon, UiRole tint, string name, string genre, string score, string copies, string kept, int grade, string status, UiRole tone) =>
        new UiAppRow { Icon = icon, Tint = UiRole.None, Cells = new[] { name, genre, score, copies, kept, status }, Grade = grade, Status = status, StatusTone = tone };

    private static UiAppRow Rival(UiIcon icon, UiRole tint, string name, string studio, int score, string copies) =>
        new UiAppRow { Icon = icon, Tint = UiRole.None, Cells = new[] { name, studio, score.ToString(System.Globalization.CultureInfo.InvariantCulture), copies }, Grade = score };

    private static UiPersonRow Person(string name, UiIcon role, UiRole tint, string title, int skill, int morale) =>
        new UiPersonRow { Name = name, Hue = UiColour.HueOf(name), RoleIcon = role, RoleTint = UiRole.None, Role = title, FirstLabel = "Skill", First = skill, SecondLabel = "Morale", Second = morale };

    /// <summary>
    /// An employee's page in an app window with its tabs across the top, as Football Manager draws a player: her head with
    /// the face, the role, the facts and the rating, her attributes graded, the week's shifts and who works them with her.
    /// </summary>
    public static UiAppModel Employee(UiWords t)
    {
        var m = new UiAppModel
        {
            Title = "Bloom Caf\u00e9 staff", Icon = UiIcon.IdBadge, TabsAcross = true,
            Nav = new[]
            {
                new UiNavEntry { Icon = UiIcon.IdCard, Label = "Profile" }, new UiNavEntry { Icon = UiIcon.Calendar, Label = "Schedule" },
                new UiNavEntry { Icon = UiIcon.Coin, Label = "Pay" }, new UiNavEntry { Icon = UiIcon.Star, Label = "Reviews", Count = "3" },
                new UiNavEntry { Icon = UiIcon.Envelope, Label = "Requests", Count = "2" },
            },
            Profile = new UiProfileModel
            {
                Name = "Maya Okafor", Hue = UiColour.HueOf("Maya Okafor"), Ring = UiRole.Warn, Role = "Customer Service", RoleIcon = UiIcon.Headset,
                Employer = "Bloom Caf\u00e9", EmployerIcon = UiIcon.Coffee, Status = "At work", StatusTone = UiRole.Ok,
                FactLabels = new[] { "Age", "Hourly wage", "Hired", "Hours a week" }, FactValues = new[] { "29", "$22", "214 days ago", "36" },
                RatingLabel = "Ability", Rating = 7,
            },
            Actions = new[]
            {
                new UiActionModel { Label = "Pay a bonus", Kind = UiButtonKind.Secondary, Key = UiAction.Option, Command = UiAppWindow.ActionCommand },
                new UiActionModel { Label = "Raise pay", Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiAppWindow.ActionCommand, Arg = 1 },
            },
            AttributesTitle = "Attributes",
            AttributeLabels = new[] { "Customer care", "Speed", "Cleanliness", "Punctuality", "Teamwork", "Energy", "Satisfaction", "Skill", "Learning" },
            AttributeValues = new[] { 82, 74, 66, 90, 71, 48, 64, 82, 58 },
            TableTitle = "This week's shifts",
            Columns = new[]
            {
                new UiColumn { Title = "Day" }, new UiColumn { Title = "Hours", Width = 120f }, new UiColumn { Title = "Till", Grow = true },
                new UiColumn { Title = "Status", Width = 136f, Right = true },
            },
            Rows = new[]
            {
                Shift("Monday", "08:00 to 16:00", "Front", "Worked", UiRole.Ok),
                Shift("Tuesday", "08:00 to 16:00", "Front", "Late 12 min", UiRole.Warn),
                Shift("Wednesday", "12:00 to 20:00", "Drive-in", "Up next", UiRole.Accent),
                Shift("Thursday", "12:00 to 20:00", "Front", "Booked", UiRole.Text2),
                Shift("Sunday", "10:00 to 14:00", "Front", "Asked off", UiRole.Warn),
            },
            MoreTitle = "Reviews",
            MoreColumns = new[] { new UiColumn { Title = "Review" }, new UiColumn { Title = "Score", Width = 56f, Right = true }, new UiColumn { Title = "Outcome", Grow = true } },
            MoreGrade = 1,
            MoreRows = new[]
            {
                Review(UiIcon.Coin, "After 180 days", 78, "Pay raised to $22 an hour"), Review(UiIcon.Handshake, "After 90 days", 71, "Kept on, more hours"),
                Review(UiIcon.Cap, "First month", 64, "Training done"),
            },
            PeopleTitle = "On her shifts", PeopleCount = "6 people",
            People = new[]
            {
                Colleague("Nadia Haddad", UiIcon.Coffee, UiRole.CatFood, "Barista", 77, 68),
                Colleague("Hana Sato", UiIcon.Headset, UiRole.CatFood, "Customer Service", 63, 52),
                Colleague("Dana Whitfield", UiIcon.Clock, UiRole.Chart4, "Shift manager", 85, 74),
                Colleague("Theo Brandt", UiIcon.Truck, UiRole.CatIndustry, "Delivery", 58, 41),
                Colleague("Ines Duarte", UiIcon.Coffee, UiRole.CatFood, "Barista", 69, 80),
                Colleague("Sam Okoro", UiIcon.Package, UiRole.CatRetail, "Stock and tills", 47, 63),
            },
            Note = "Maya asks for Sundays off. Answer her soon, or she loses heart.", NoteTone = UiRole.Warn, NoteAction = "Open Requests",
        };
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    // The row's icon is its till, a counter or a car's window, so it says what differs from the row above.
    private static UiAppRow Shift(string day, string hours, string till, string status, UiRole tone) =>
        new UiAppRow { Icon = till == "Drive-in" ? UiIcon.Car : UiIcon.Store, Tint = UiRole.None, Cells = new[] { day, hours, till, status }, Grade = -1, Status = status, StatusTone = tone };

    private static UiAppRow Review(UiIcon outcome, string name, int score, string what) =>
        new UiAppRow { Icon = outcome, Tint = UiRole.None, Cells = new[] { name, score.ToString(System.Globalization.CultureInfo.InvariantCulture), what }, Grade = score };

    private static UiPersonRow Colleague(string name, UiIcon role, UiRole tint, string title, int skill, int mood) =>
        new UiPersonRow { Name = name, Hue = UiColour.HueOf(name), RoleIcon = role, RoleTint = UiRole.None, Role = title, FirstLabel = "Skill", First = skill, SecondLabel = "Satisfaction", Second = mood };

    /// <summary>A desk session as a decision card: the weekly meeting, its three choices with what each costs and gives.</summary>
    public static UiDecisionModel Meeting(UiWords t)
    {
        var m = new UiDecisionModel
        {
            Kicker = "Desk session, Lantern Bay Games", Title = "Weekly team meeting", Emblem = UiIcon.People, EmblemTint = UiRole.Accent, EmblemLabel = "12 people",
            Faces = new[]
            {
                "Ava Lindqvist", "Marcus Hale", "Priya Raman", "Tom Becker", "Elena Sokolova", "Jonah Reyes", "Kenji Mori", "Amara Nwosu", "Sofia Rossi", "Arjun Patel",
                "Mei Lin", "Diego Alvarez"
            },
            Body = "Twelve of your people are in the room. Morale has slipped since the crunch on Ember Isle, and the next milestone is nine days away. What is this week's meeting for?",
            Choices = new[]
            {
                new UiDecisionChoice
                {
                    Key = UiAction.Pick1, Name = "Lift morale", Note = "20 min",
                    Lines = new[]
                    {
                        new UiConsequence { Text = "Burnout eases for the people present", Tone = 1, Change = -1 },
                        new UiConsequence { Text = "Output this week is a little lower", Tone = -1, Change = -1 }
                    }
                },
                new UiDecisionChoice
                {
                    Key = UiAction.Pick2, Name = "Push output", Note = "20 min",
                    Lines = new[]
                    {
                        new UiConsequence { Text = "More gets done this week", Tone = 1, Change = 1 }, new UiConsequence { Text = "Burnout builds each day they work", Tone = -1, Change = 1 }
                    }
                },
                new UiDecisionChoice
                {
                    Key = UiAction.Pick3, Name = "Hear them out", Note = "30 min",
                    Lines = new[] { new UiConsequence { Text = "You learn what slows the team down", Tone = 1 }, new UiConsequence { Text = "No change to this week's output", Tone = 0 } }
                },
            },
            Selected = 0,
            Buttons = new[]
            {
                new UiActionModel { Label = "Not now", Kind = UiButtonKind.Secondary, Key = UiAction.Back, Command = UiDecision.ButtonCommand },
                new UiActionModel { Label = "Hold the meeting", Kind = UiButtonKind.Primary, Key = UiAction.Alt, Command = UiDecision.ButtonCommand, Arg = 1 },
            },
        };
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_choose", "Choose") },
            new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>Contacts as an app window: the groups with their icons, people with faces and businesses with theirs, Maya's thread open.</summary>
    public static UiListDetailModel ContactsWindow(UiWords t)
    {
        UiListDetailModel m = Contacts(t, false);
        UiIcon[] icons = { UiIcon.AddressBook, UiIcon.Person, UiIcon.Briefcase, UiIcon.IdBadge, UiIcon.Package, UiIcon.Sofa, UiIcon.Flag };
        for (int i = 0; i < m.GroupCount && i < icons.Length; i++)
        {
            m.Groups[i].Icon = icons[i];
            m.Groups[i].Tint = UiRole.None;
        }
        // Businesses and offices wear their family's icon, people their faces.
        string[] firms = { "Anderson Recruitment Corp.", "City Office", "NY AutoTow Service", "City Workforce Inc.", "CityAds", "McCain\u2019s eMarketing", "Harbor Line Wholesale",
            "Lumen & Oak Interiors", "Vex Holdings" };
        UiIcon[] firmIcons = { UiIcon.IdBadge, UiIcon.Bank, UiIcon.Truck, UiIcon.Briefcase, UiIcon.Megaphone, UiIcon.Globe, UiIcon.Package, UiIcon.Sofa, UiIcon.Flag };
        for (int i = 0; i < m.RowCount; i++)
        {
            int f = System.Array.IndexOf(firms, m.Rows[i].Name);
            if (f >= 0)
            {
                m.Rows[i].Icon = firmIcons[f];
                m.Rows[i].Tint = UiRole.None;
            }
        }
        return m;
    }

    /// <summary>The phone's home in the new tokens: the day, a page of apps on their tiles, the page bar fixed at its foot.</summary>
    public static UiPhoneModel PhoneHome(UiWords t)
    {
        var m = new UiPhoneModel
        {
            Name = "BizPhone", Time = "14:32", Page = 0, Pages = 2, NextTitle = "Up next",
            Next = new[]
            {
                new UiInfoRow { Label = "16:00", Value = "Weekly team meeting, Lantern Bay Games" }, new UiInfoRow { Label = "18:30", Value = "Gym at Iron Works" },
                new UiInfoRow { Label = "Wed", Value = "Cinder Valley playtest" }, new UiInfoRow { Label = "Thu", Value = "Rent due on 70 Fifth Avenue" },
                new UiInfoRow { Label = "Fri", Value = "Pay day for 26 staff" }, new UiInfoRow { Label = "Sat", Value = "Coffee beans delivered to Bloom Café" },
            },
            Apps = new[]
            {
                App(UiIcon.AddressBook, UiRole.Chart1, "Contacts", 5), App(UiIcon.Message, UiRole.CatServices, "Messages", 2), App(UiIcon.Briefcase, UiRole.CatOffice, "BizMan", 0),
                App(UiIcon.Bank, UiRole.Chart3, "Bank", 0), App(UiIcon.ChartPie, UiRole.Chart4, "Economy", 0), App(UiIcon.ChartLineUp, UiRole.Chart2, "Stocks", 0),
                App(UiIcon.Cpu, UiRole.CatTech, "Tech", 1), App(UiIcon.Fire, UiRole.Chart6, "Flare", 3), App(UiIcon.MapTrifold, UiRole.Chart5, "Map", 0),
                App(UiIcon.Calendar, UiRole.Chart7, "Calendar", 0), App(UiIcon.ShoppingCart, UiRole.CatRetail, "Market", 0), App(UiIcon.Building, UiRole.CatIndustry, "Estate", 0),
                App(UiIcon.Handshake, UiRole.None, "Jobs", 0), App(UiIcon.Newspaper, UiRole.CatIndustry, "News", 0), App(UiIcon.Camera, UiRole.Chart7, "Camera", 0),
                App(UiIcon.Gear, UiRole.Text2, "Settings", 0),
            },
        };
        m.Prompts = new[]
        {
            new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") }, new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_select", "Select") },
            new UiPrompt { Action = UiAction.TabPrev, Label = t("reallife_ui_prompt_page", "Page") }, new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
        };
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    private static UiPhoneApp App(UiIcon icon, UiRole tint, string label, int badge) => new UiPhoneApp { Icon = icon, Tint = UiRole.None, Label = label, Badge = badge };
}
