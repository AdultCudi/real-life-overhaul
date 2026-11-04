using System.Collections.Generic;
using RealLifeOverhaul.Ui;

/// <summary>
/// Career, Campus, Open Roles and the Marketplace on the kit: every tab of each, as the apps read their records. A new player (no job,
/// nothing studied, no ads, nothing bought) and an established one, a long name, a full list and an empty one. Plain English.
/// </summary>
internal static partial class UiSamples
{
    public static readonly string[] CareerStates = { "professions", "jobs", "applications", "my_job", "management", "own", "cv", "my_job_new",
        "jobs_empty", "jobs_long" };
    public static readonly string[] CampusStates = { "courses", "studying", "results", "skills", "family", "schools", "courses_full", "results_new" };
    public static readonly string[] RolesStates = { "jobs", "applicants", "market", "search", "team", "person", "applicants_none" };
    public static readonly string[] MarketplaceStates = { "listings", "yours", "offers", "freelance", "listings_machines", "listings_none" };

    /// <summary>A work app's source for a sample: the page of a tab, built from the sample's facts; presses go nowhere.</summary>
    internal sealed class SampleWork : IWorkSource
    {
        private readonly string _id, _title;
        private readonly UiIcon _icon;
        private readonly bool _across;
        private readonly UiNavEntry[] _nav;
        private readonly System.Func<WorkView, WorkPage> _page;
        private readonly UiWords _t;
        public SampleWork(string id, string title, UiIcon icon, bool across, UiNavEntry[] nav, UiWords t, System.Func<WorkView, WorkPage> page)
        {
            _id = id; _title = title; _icon = icon; _across = across; _nav = nav; _t = t; _page = page;
        }
        public string Id => _id;
        public string Title => _title;
        public UiIcon Icon => _icon;
        public int Generation => 0;
        public bool Across => _across;
        public string T(string key, string english) => _t(key, english);
        public UiNavEntry[] Nav(WorkView view) => _nav;
        public WorkPage Page(WorkView view) => _page(view);
        public bool Do(WorkDo act, WorkView view) => true;
        public int TakeWanted() => -1;
    }

    private static int WkTabOf(string[] states, string state, string[] order)
    {
        int at = System.Array.IndexOf(order, state.Replace("_new", string.Empty).Replace("_empty", string.Empty).Replace("_long",
            string.Empty).Replace("_full", string.Empty).Replace("_none", string.Empty).Replace("_machines", string.Empty));
        return at < 0 ? 0 : at;
    }

    // ------------------------------------------------------------------------------------------------- Career

    public static WorkScreen CareerScreenOf(UiWords t, string state)
    {
        UtilitiesWords w = UtilitiesWordsOf(t);
        CareerFacts f = CareerFactsOf(state);
        var screen = new WorkScreen(new SampleWork("career", w.T("reallife_career_app", "Career"), UiIcon.Briefcase, false, CareerPages.Nav(f, w), t,
            v => CareerPages.Build(f, v, w)));
        screen.View.Tab = state == "professions" ? 0 : state.StartsWith("jobs") ? 1 : state == "applications" ? 2 : state.StartsWith("my_job") ? 3 : state == "management" ? 4 : state == "own" ? 5 : 6;
        return screen;
    }

    public static CareerFacts CareerFactsOf(string state)
    {
        bool none = state == "my_job_new" || state == "jobs_empty";
        var f = new CareerFacts { Ready = true, Player = "Jordan Hayes", Day = "Day 402", OpenNow = 17, Qualify = 9, NearYou = 6, ApplicationsWaiting = 2 };
        string longName = state == "jobs_long" ? " of the Harbourside Freight and Cold Storage Company" : string.Empty;
        if (!none)
        {
            var j = new CareerJob
            {
                Title = "Senior picker", Employer = "Harbourside Freight HSF", Pay = "$21.50 an hour", Hours = "Mon to Fri, 09:00 to 17:00",
                    Months = "14 months", Status = "On time all month",
                NextTitle = "Next shift, starts in 15 minutes", NextLine = "Mon 09:00 to 17:00",
                    Desk = "Click your desk in the depot office to open the work panel and start the shift.",
                NextNote = "Hudson Yards depot", Progress = 0, Performance = 82, Punctuality = 94, Teamwork = 71, Reliability = 88,
                    WeekHours = "32 of 40", WeekPay = "$688 before tax",
                RivalName = "Marcus Lee", Mine = new[] { 71, 72, 74, 73, 77, 78, 77, 79, 81, 80, 81, 82 }, Theirs = new[] { 70, 71, 72, 70, 74, 75,
                    76, 76, 77, 78, 78, 79 }
            };
            j.Rungs.Add(new CareerRung { Title = "Picker", Pay = "$17.00", State = 0 });
            j.Rungs.Add(new CareerRung { Title = "Senior picker", Pay = "$21.50", State = 1 });
            j.Rungs.Add(new CareerRung { Title = "Shift lead", Pay = "$26.00", State = 2 });
            j.Rungs.Add(new CareerRung { Title = "Depot supervisor", Pay = "$33.00", State = 2 });
            j.Rungs.Add(new CareerRung { Title = "Depot manager", Pay = "$44.00", State = 2 });
            j.Peers.Add(new CareerPeer { Name = "Marcus Lee", Role = "Senior picker", Tag = "up for the post", Score = 79 });
            j.Peers.Add(new CareerPeer { Name = "Pilar Duran", Role = "Shift lead", Tag = "your lead", Score = 90 });
            j.Peers.Add(new CareerPeer { Name = "Oleg Kozlov", Role = "Forklift driver", Score = 84 });
            f.Job = j;
        }
        var jobs = new[]
        {
            new[] { "Shift lead" + longName, "Hell's Kitchen", "09:00 to 17:00", "$26.00", "+21%", "Needs 2 more", "1" }, new[] { "Barista",
                "Midtown", "06:00 to 14:00", "$16.50", "-23%", "You qualify", "0" },
            new[] { "Line cook", "Midtown", "11:00 to 19:00", "$19.00", "-12%", "You qualify", "0" }, new[] { "Pharmacy assistant", "Murray Hill",
                "09:00 to 17:00", "$18.50", "-14%", "Needs a licence", "2" },
            new[] { "Bank teller", "Uptown", "09:00 to 17:00", "$22.00", "+2%", "You qualify", "0" }, new[] { "Courier", "Hell's Kitchen",
                "07:00 to 15:00", "$18.00", "-16%", "You qualify", "0" }
        };
        for (int i = 0; i < jobs.Length && state != "jobs_empty"; i++)
        {
            var r = new CareerRow { Id = i + 1, Cells = new[] { jobs[i][0], jobs[i][1], jobs[i][3], jobs[i][4] }, Status = jobs[i][5],
                Tone = jobs[i][6] == "0" ? UiRole.Ok : UiRole.Warn,
                Open = i != 2, Qualify = jobs[i][6] == "0", Near = i < 2, Detail = "Pays " + jobs[i][3] + " an hour at " + jobs[i][1] + "." };
            r.Needs.Add(new CareerNeed { Words = "12 months as a senior picker", Met = true });
            r.Needs.Add(new CareerNeed { Words = "Forklift licence", Met = true });
            r.Needs.Add(new CareerNeed { Words = "Performance 85", Met = false, Have = "you have 82" });
            r.Needs.Add(new CareerNeed { Words = "Teamwork 75", Met = false, Have = "you have 71" });
            if (i == 0)
            {
                r.Others.Add(new CareerPeer { Name = "Marcus Lee", Role = "Senior picker", Day = "Day 399", Score = 79 });
                r.Others.Add(new CareerPeer { Name = "Nia Brooks", Role = "Picker", Day = "Day 400", Score = 74 });
            }
            r.Acts.Add(CareerAct.Apply);
            r.Acts.Add(CareerAct.ShowPost);
            r.Why.Add(i == 3 ? "Needs a licence first." : string.Empty);
            f.Jobs.Add(r);
        }
        for (int i = 0; i < 2; i++)
        {
            var a = new CareerRow { Id = 10 + i, Icon = UiIcon.ClipboardText, Cells = new[] { i == 0 ? "Shift lead" : "Barista",
                i == 0 ? "Harbourside Freight HSF" : "Brewed Beans", "Day " + (399 + i), "Day " + (404 + i) },
                Status = i == 0 ? "Interview" : "Sent", Tone = i == 0 ? UiRole.Ok : UiRole.Warn,
                    Detail = i == 0 ? "Invited to an interview on day 403 at 10:00." : "With the employer: an answer in a day or two." };
            a.Acts.Add(i == 0 ? CareerAct.Interview : CareerAct.Withdraw);
            a.Acts.Add(CareerAct.ShowPost);
            a.Why.Add(string.Empty);
            f.Applications.Add(a);
        }
        string[] profs = { "Picker", "Courier", "Barista", "Line cook", "Bank teller", "Software tester" };
        for (int i = 0; i < profs.Length; i++)
        {
            var p = new CareerRow { Id = 20 + i, Icon = UiIcon.Briefcase, Cells = new[] { profs[i], "First post", "$" + (16 + i) + ".00",
                i < 3 ? "Open to you" : "Needs a course" }, Detail = profs[i] + ": the ladder starts at $" + (16 + i) + ".00 an hour." };
            p.Acts.Add(CareerAct.OpenCampus);
            p.Why.Add(string.Empty);
            f.Professions.Add(p);
        }
        var task = new CareerRow { Id = 30, Icon = UiIcon.People, Cells = new[] { "Cover a gap on today's shift", "Hudson Yards depot",
            "Pilar Duran", "Asked day 401" }, Status = "Decide", Tone = UiRole.Warn,
            Detail = "Pilar Duran asks you to cover a gap on today's shift." };
        task.Acts.Add(CareerAct.TaskYes);
        task.Acts.Add(CareerAct.TaskNo);
        task.Why.Add(string.Empty);
        f.Management.Add(task);
        var c = new CareerRow { Id = 40, Icon = UiIcon.Wrench, Cells = new[] { "Rose Wilder", "Carpenter", "$180", "Day 410" }, Status = "Open",
            Detail = "A shelf and a door frame for Rose Wilder, $180, due day 410." };
        c.Acts.Add(CareerAct.TakeContract);
        c.Acts.Add(CareerAct.DropContract);
        c.Why.Add(string.Empty);
        f.Own.Add(c);
        f.Cv.Add(new CareerRow { Id = 50, Icon = UiIcon.Briefcase, Cells = new[] { "Senior picker", "Harbourside Freight HSF", "Day 388", "now" },
            Detail = "Your job now." });
        f.Cv.Add(new CareerRow { Id = 51, Icon = UiIcon.Briefcase, Cells = new[] { "Picker", "Harbourside Freight HSF", "Day 330", "Day 388" },
            Detail = "A good reference." });
        f.Cv.Add(new CareerRow { Id = 52, Icon = UiIcon.Cap, Cells = new[] { "Forklift and Warehouse Safety", "Garment District Trade School",
            "Day 300", "Day 311" }, Detail = "Licence won." });
        return f;
    }

    // ------------------------------------------------------------------------------------------------- the other three apps

    public static WorkScreen CampusScreenOf(UiWords t, string state)
    {
        UtilitiesWords w = UtilitiesWordsOf(t);
        WorkApp app = CampusAppOf(w, state);
        var screen = new WorkScreen(new SampleWork("campus", w.T("reallife_campus_app", "Campus"), UiIcon.Cap, true, app.Nav, t,
            v => WorkTabPages.Build(app, v, w)));
        screen.View.Tab = WkTabOf(CampusStates, state, new[] { "courses", "studying", "results", "skills", "family", "schools" });
        return screen;
    }

    public static WorkScreen RolesScreenOf(UiWords t, string state)
    {
        UtilitiesWords w = UtilitiesWordsOf(t);
        WorkApp app = RolesAppOf(w, state);
        var screen = new WorkScreen(new SampleWork("open_roles", w.T("reallife_roles_app", "Open Roles"), UiIcon.People, true, app.Nav, t,
            v => WorkTabPages.Build(app, v, w)));
        screen.View.Tab = state == "person" ? 1 : WkTabOf(RolesStates, state, new[] { "jobs", "applicants", "market", "search", "team" });
        return screen;
    }

    public static WorkScreen MarketScreenOf(UiWords t, string state)
    {
        UtilitiesWords w = UtilitiesWordsOf(t);
        WorkApp app = MarketAppOf(w, state);
        var screen = new WorkScreen(new SampleWork("marketplace", w.T("reallife_hustle_app", "Marketplace"), UiIcon.Store, true, app.Nav, t,
            v => WorkTabPages.Build(app, v, w)));
        screen.View.Tab = WkTabOf(MarketplaceStates, state, new[] { "listings", "yours", "offers", "freelance" });
        return screen;
    }

    private static UiNavEntry WkNav(UiIcon icon, string label, string count = null) => new UiNavEntry { Icon = icon, Label = label, Count = count };

    private static WorkAct WkAct(int kind, string label, UiButtonKind style, UiIcon icon, string why = null, bool opens = false, int arg = 0)
    {
        return new WorkAct { Kind = kind, Label = label, Style = style, Icon = icon, Why = why, Opens = opens, Arg = arg };
    }

    private static UiColumn[] WkCols(params string[] titles)
    {
        var c = new UiColumn[titles.Length];
        for (int i = 0; i < c.Length; i++)
        {
            c[i] = new UiColumn { Title = titles[i], Grow = i == 0, Width = i == 0 ? 0f : 184f, Right = i == c.Length - 1 && i > 1 };
        }
        return c;
    }

    private static WorkApp CampusAppOf(UtilitiesWords w, string state)
    {
        var app = new WorkApp { Ready = true };
        app.Nav = new[] { WkNav(UiIcon.Books, "Courses"), WkNav(UiIcon.Calendar, "Studying", "3"), WkNav(UiIcon.ClipboardText, "Results"),
            WkNav(UiIcon.Star, "Skills"), WkNav(UiIcon.Heart, "Family"), WkNav(UiIcon.Building, "Schools", "3") };
        var courses = new WorkTab { TableTitle = "Courses", Columns = WkCols("Course", "School", "Length", "Seats left", "Fee"),
            Lead = "Courses by kind and campus. Classes start at your desk; click it in the classroom when the bell rings.",
            Chips = new[] { "All 24", "Business", "Trades", "Tech" } };
        string[][] list = { new[] { "Introduction to Accounting", "Midtown Community College", "12 weeks", "6 of 24", "$1,850" },
            new[] { "Professional Cooking", "Garment District Trade School", "16 weeks", "11 of 24", "$2,400" },
            new[] { "Forklift and Warehouse Safety", "Garment District Trade School", "3 weeks", state == "courses_full" ? "Full" : "9 of 24",
                "$650" }, new[] { "Python for Business", "Hudson Tech Institute", "10 weeks", "14 of 24", "$1,600" } };
        for (int i = 0; i < list.Length; i++)
        {
            var r = new WorkRow { Id = i + 1, Icon = i == 2 ? UiIcon.Wrench : UiIcon.Cap, Cells = list[i], Status = i == 2 ? "Licence" : "Beginner",
                Tone = i == 2 ? UiRole.Warn : UiRole.Accent,
                Detail = list[i][0] + " at " + list[i][1] + ". Before you enrol: you have the fee, your evenings are free. Your teacher: Dr. Elena Marsh, 12 years teaching, rated 4.7 by students.", Group = i < 2 ? i : 2 };
            r.Acts.Add(WkAct(100, state == "courses_full" && i == 2 ? "Full" : "Enrol, " + list[i][4], UiButtonKind.Primary, UiIcon.Check,
                state == "courses_full" && i == 2 ? "Every seat of this course is taken." : null));
            r.People.Add(new WorkPerson { Name = "Ava Lindqvist", Role = "From your Pioneer team, in this class", Label = "Skill", Score = 52 });
            r.PeopleTitle = "In this class";
            courses.Rows.Add(r);
        }
        var studying = new WorkTab { Lead = "Nothing under way. Enrol on a course and its classes, homework and exam appear here.",
            TableTitle = "Studying", Columns = WkCols("Course", "Next class", "Attended", "Exam") };
        studying.Rows.Add(new WorkRow { Id = 1, Icon = UiIcon.Cap, Cells = new[] { "Business Maths", "Mon 18:00", "96%", "Day 421" },
            Status = "Under way", Tone = UiRole.Accent, Detail = "Business Maths: next class Monday at 18:00, exam on day 421." });
        var results = new WorkTab { Lead = state == "results_new" ? "No results yet. A finished course adds its grade, credits and place in the class here." : null,
            Profile = state == "results_new" ? null : new UiProfileModel { Name = "Jordan Hayes", Hue = UiColour.HueOf("Jordan Hayes"),
                Role = "Midtown Community College", FactLabels = new[] { "GPA", "Credits earned", "Class rank" }, FactValues = new[] { "3.4", "48",
                "7 of 32" } },
            TableTitle = "Transcript", Columns = WkCols("Course", "Credits", "Exam", "Grade") };
        if (state != "results_new")
        {
            results.Rows.Add(new WorkRow { Id = 1, Icon = UiIcon.Cap, Cells = new[] { "Business Maths", "6", "88", "A 88%" }, Status = "Passed",
                Detail = "Business Maths: A, 88%, 6 credits." });
            results.Rows.Add(new WorkRow { Id = 2, Icon = UiIcon.Cap, Cells = new[] { "Spanish, Level 1", "4", "52", "C 52%" }, Status = "Repeat",
                Tone = UiRole.Warn, Detail = "Spanish, Level 1: C, 52%. A retake can be sat." });
            results.PeopleTitle = "Your class";
            results.PeopleCount = "by grade point average";
            results.People.Add(new WorkPerson { Name = "Priya Shah", Role = "1", Label = "GPA", Score = 100 });
            results.People.Add(new WorkPerson { Name = "Hana Okada", Role = "2", Label = "GPA", Score = 97 });
            results.People.Add(new WorkPerson { Name = "Jordan Hayes (you)", Role = "7", Label = "GPA", Score = 85 });
        }
        var skills = new WorkTab { Lead = "What your courses and work have taught you.", AttrTitle = "Skills", AttrLabels = new[] { "Numeracy",
            "Customer care", "Warehouse safety", "Spanish" }, AttrValues = new[] { 71, 88, 74, 31 } };
        var family = new WorkTab { Lead = "No children at university yet." };
        var schools = new WorkTab { Lead = "The city's schools.", TableTitle = "Schools", Columns = WkCols("School", "District", "Courses", "Rating") };
        schools.Rows.Add(new WorkRow { Id = 1, Icon = UiIcon.Building, Cells = new[] { "Midtown Community College", "Midtown", "9", "4.4" },
            Detail = "Midtown Community College, Midtown." });
        schools.Rows[0].Acts.Add(WkAct(WorkKinds.Locate, "Show on map", UiButtonKind.Secondary, UiIcon.Locate, null, true));
        app.Tabs = new[] { courses, studying, results, skills, family, schools };
        return app;
    }

    private static WorkApp RolesAppOf(UtilitiesWords w, string state)
    {
        var app = new WorkApp { Ready = true };
        app.Nav = new[] { WkNav(UiIcon.ClipboardText, "Jobs", "5"), WkNav(UiIcon.People, "Applicants", "31"), WkNav(UiIcon.Globe, "Market"),
            WkNav(UiIcon.Search, "Search"), WkNav(UiIcon.Person, "Team", "12") };
        var jobs = new WorkTab { Lead = "Your open roles. An ad runs for a week or more and brings applicants over a few days.",
            TableTitle = "Open roles", Columns = WkCols("Role", "Place", "Posts", "Applicants") };
        jobs.Rows.Add(new WorkRow { Id = 1, Icon = UiIcon.Code, Cells = new[] { "Game designer", "Pioneer Game Studio", "2 posts to fill", "7" },
            Status = "Open", Detail = "Game designer at Pioneer Game Studio, up to $42 an hour, 2 posts to fill." });
        var applicants = new WorkTab { Lead = state == "applicants_none" ? "No applicants yet. They arrive over a few days; other firms hire too." : null, TableTitle = "Applicants", Columns = WkCols("Applicant", "From", "Skill", "Asks", "Stage") };
        string[][] people = { new[] { "Nora Whitfield", "Tidewater Interactive", "82", "$41/h", "Competing offer" }, new[] { "Kofi Mensah",
            "Pixel Pit", "76", "$36/h", "Available now" }, new[] { "Camille Duval", "Copperkettle Games", "79", "$38/h", "Available now" } };
        for (int i = 0; i < people.Length && state != "applicants_none"; i++)
        {
            var r = new WorkRow { Id = i + 1, Icon = UiIcon.Person, Cells = people[i], Status = people[i][4],
                Tone = i == 0 ? UiRole.Warn : UiRole.Ok, Grade = int.Parse(people[i][2]),
                Detail = i == 0 ? "Guo Interactive offered her $44 an hour; she answers by day 405. Beat it to hire her. Work so far: Game designer, Tidewater Interactive, 4 years." : people[i][0] + " from " + people[i][1] + ", asking " + people[i][3] + "." };
            r.Acts.Add(WkAct(100, i == 0 ? "Hire at $44 an hour" : "Hire at " + people[i][3], UiButtonKind.Primary, UiIcon.Check));
            r.Acts.Add(WkAct(101, "Shortlist", UiButtonKind.Secondary, UiIcon.Star));
            applicants.Rows.Add(r);
        }
        applicants.GradeColumn = 2;
        applicants.Sub = state == "person";
        var market = new WorkTab { Lead = "Professionals looking for work, by line and level. Ask the going rate and wait for them to apply.",
            TableTitle = "On the market", Columns = WkCols("Line", "Level", "Looking", "Going rate") };
        market.Rows.Add(new WorkRow { Id = 1, Icon = UiIcon.Code, Cells = new[] { "Game design", "Mid", "23", "$34/h" } });
        var search = new WorkTab { Lead = "A search firm finds senior people and crews; it charges a share of a year's pay.", Acts = { WkAct(100,
            "Brief a search", UiButtonKind.Primary, UiIcon.Search) } };
        var team = new WorkTab { Lead = "Your team, with a page for each person: file, career and contract.", TableTitle = "Team",
            Columns = WkCols("Name", "Role", "Pay", "Satisfaction") };
        team.Rows.Add(new WorkRow { Id = 1, Icon = UiIcon.Person, Cells = new[] { "Kenji Mori", "Producer", "$38/h", "82" }, Grade = 82,
            Detail = "Kenji Mori, producer, hired day 389." });
        team.GradeColumn = 3;
        app.Tabs = new[] { jobs, applicants, market, search, team };
        return app;
    }

    private static WorkApp MarketAppOf(UtilitiesWords w, string state)
    {
        var app = new WorkApp { Ready = true };
        app.Nav = new[] { WkNav(UiIcon.Tag, "Listings", "86"), WkNav(UiIcon.Package, "Yours", "4"), WkNav(UiIcon.Arrows, "Offers", "2"),
            WkNav(UiIcon.Wrench, "Freelance") };
        var listings = new WorkTab { Lead = state == "listings_none" ? "Nothing is listed today. New things arrive each morning." : null,
            TableTitle = "Listings", Columns = WkCols("Thing", "Seller", "Asking", "Against worth"),
            Chips = new[] { "All 86", "Vehicles 18", "Machines 22", "Furniture 31", "Stock 15" } };
        string[][] things = state == "listings_machines"
            ? new[] { new[] { "Espresso machine", "Sofia Marin", "$1,950", "-9%" }, new[] { "Commercial grill", "Hana Okada", "$2,300", "-3%" },
                new[] { "Display fridge", "Leo Park", "$1,150", "-15%" } }
            : new[] { new[] { "Red delivery van", "Huang Guo", "$14,200", "-12%" }, new[] { "Box truck", "Harbourside Freight HSF", "$27,900",
                "-6%" }, new[] { "Blue sport coupe", "Marco Bellini", "$38,500", "-4%" } };
        for (int i = 0; i < things.Length && state != "listings_none"; i++)
        {
            var r = new WorkRow { Id = i + 1, Icon = state == "listings_machines" ? UiIcon.Factory : UiIcon.Car, Cells = things[i],
                Status = things[i][3].StartsWith("-") ? things[i][3].Substring(1) + " under" : things[i][3] + " over",
                Tone = things[i][3].StartsWith("-") ? UiRole.Ok : UiRole.Warn,
                Detail = things[i][0] + " from " + things[i][1] + ", asking " + things[i][2] + ". Before you buy: you have the money, a free bay at Hudson Warehouse, delivery adds $120 and a day." };
            r.Acts.Add(WkAct(100, "Buy now, " + things[i][2], UiButtonKind.Primary, UiIcon.Check));
            r.Acts.Add(WkAct(101, "Haggle", UiButtonKind.Secondary, UiIcon.Arrows));
            if (i == 0)
            {
                r.PeopleTitle = "More from this seller";
                r.People.Add(new WorkPerson { Name = "Box truck", Role = "$27,900", Icon = UiIcon.Truck });
            }
            listings.Rows.Add(r);
        }
        var yours = new WorkTab { Lead = "Things you bought and what to do with them: fix, clean, list or keep.", TableTitle = "Yours",
            Columns = WkCols("Thing", "State", "Paid", "Worth") };
        yours.Rows.Add(new WorkRow { Id = 1, Icon = UiIcon.Sofa, Cells = new[] { "Corner sofa", "Kept", "$890", "$960" },
            Detail = "Corner sofa, kept at home." });
        var offers = new WorkTab { Lead = "Buyers' offers for things you listed.", TableTitle = "Offers", Columns = WkCols("Thing", "Buyer", "Offer",
            "Asking") };
        offers.Rows.Add(new WorkRow { Id = 1, Icon = UiIcon.Arrows, Cells = new[] { "Dining set", "Priya Shah", "$700", "$740" },
            Detail = "Priya Shah offers $700 for the dining set." });
        offers.Rows[0].Acts.Add(WkAct(100, "Accept $700", UiButtonKind.Primary, UiIcon.Check));
        offers.Rows[0].Acts.Add(WkAct(101, "Counter", UiButtonKind.Secondary, UiIcon.Arrows));
        var freelance = new WorkTab { Lead = "Clients ask for visits: detailing, handyman jobs and moves.", TableTitle = "Freelance",
            Columns = WkCols("Client", "Job", "Pay", "When") };
        freelance.Rows.Add(new WorkRow { Id = 1, Icon = UiIcon.Wrench, Cells = new[] { "Rose Wilder", "Handyman", "$130", "Today 14:00" },
            Detail = "Rose Wilder wants a handyman visit today at 14:00, $130." });
        freelance.Rows[0].Acts.Add(WkAct(100, "Book it", UiButtonKind.Primary, UiIcon.Check));
        app.Tabs = new[] { listings, yours, offers, freelance };
        return app;
    }
}
