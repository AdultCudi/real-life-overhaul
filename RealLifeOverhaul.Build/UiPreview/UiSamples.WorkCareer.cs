using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    public static WorkScreen CareerScreenOf(UiWords t, string state)
    {
        UtilitiesWords w = UtilitiesWordsOf(t);
        CareerFacts f = CareerFactsOf(state, t);
        var screen = new WorkScreen(new SampleWork("career", w.T("reallife_career_app", "Career"), UiIcon.Briefcase, false, CareerPages.Nav(f, w), t,
            v => CareerPages.Build(f, v, w)));
        screen.View.Tab = WkTabOf("career", state);
        if (state == "jobs_qualify")
        {
            screen.View.Filter = 2;
            screen.View.Row = 1;
        }
        return screen;
    }

    private static WorkRow WkJob(UtilitiesWords w, int id, UiIcon icon, string title, string employer, string where, string hours, string pay, int against, int needs)
    {
        string qualify = w.T("reallife_work_you_qualify", "You qualify");
        string status = needs == 0 ? qualify : needs == 1 ? w.F("reallife_work_needs_n_more", "Needs {n} more", "n", "1") : needs == 2 ? w.F("reallife_work_needs_n_more", "Needs {n} more", "n", "2")
            : w.T("reallife_work_needs_licence", "Needs a licence");
        UiRole tone = needs == 0 ? UiRole.Ok : UiRole.Warn;
        var r = new WorkRow { Id = id, Icon = icon, Name = title, Sub = employer, Cells = new[] { null, where, hours, pay, WorkBuilder.Percent(against), status },
            Values = new[] { 0, 0, 0, 0, against < 0 ? -1 : against > 0 ? 1 : 0, 0 },
            Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone }, Status = status, Tone = tone };
        r.Mask = 1 | (needs == 0 ? 2 : 0) | (id % 3 == 0 ? 4 : 0);
        r.Chip = employer;
        r.ChipTone = UiRole.Accent;
        r.HeadSub = where;
        r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_career_col_pay", "Pay"), pay + "/h"));
        r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_hours", "Hours"), hours));
        r.Acts.Add(new WorkAct { Kind = 100, Arg = id, Label = w.T("reallife_career_button_apply", "Apply"), Style = UiButtonKind.Primary, Icon = UiIcon.Check,
            Why = needs == 3 ? w.T("reallife_work_why_unqualified", "You do not hold what that post needs.") : null });
        r.Acts.Add(new WorkAct { Kind = 101, Arg = id, Label = w.T("reallife_career_button_map", "Show on map"), Style = UiButtonKind.Secondary, Icon = UiIcon.Locate, Opens = true });
        var checks = new WorkSection { Part = WorkPart.Checks, Title = w.T("reallife_work_what_it_needs", "What it needs") };
        checks.Checks.Add(new WorkNeed { Words = w.F("reallife_work_need_skill_points", "Skills at {n} points", "n", "60"), Met = true });
        checks.Checks.Add(new WorkNeed { Words = w.T("reallife_work_need_takes_you", "The employer takes people like you"), Met = needs != 3 });
        checks.Checks.Add(new WorkNeed { Words = w.F("reallife_work_need_performance", "Performance {n}", "n", "85"), Met = needs == 0, Have = needs == 0 ? null : w.F("reallife_work_you_have", "you have {n}", "n", "82") });
        checks.Checks.Add(new WorkNeed { Words = w.F("reallife_work_need_teamwork", "Teamwork {n}", "n", "50"), Met = true });
        int metCount = 0;
        foreach (WorkNeed need in checks.Checks)
        {
            metCount += need.Met ? 1 : 0;
        }
        checks.Note = w.F("reallife_work_needs_met", "{met} of {all} met", "met", metCount.ToString(), "all", checks.Checks.Count.ToString());
        r.Sections.Add(checks);
        var against2 = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_pay_against", "Against your pay") };
        against2.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_your_pay_now", "Your pay now"), "$21.50/h"));
        against2.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_difference", "The difference"), WorkBuilder.Percent(against)));
        r.Sections.Add(against2);
        return r;
    }

    public static CareerFacts CareerFactsOf(string state, UiWords t = null)
    {
        UtilitiesWords w = UtilitiesWordsOf(t ?? ((k, e) => e));
        bool none = state == "my_job_new" || state == "jobs_empty";
        var f = new CareerFacts { Ready = true, Player = "Jordan Hayes", Day = "Day 402" };
        if (!none)
        {
            var j = new CareerJob
            {
                Title = "Senior picker", Employer = "Harbourside Freight HSF", Pay = "$21.50 an hour", Hours = "Mon to Fri, 09:00 to 17:00",
                    Months = "14 months", Status = w.T("reallife_work_status_on_time", "On time"),
                NextTitle = w.T("reallife_work_next_shift", "Next shift"), NextLine = "Mon 09:00 to 17:00",
                    Desk = "Click your desk in the depot office to open the work panel and start the shift.",
                NextNote = "Hudson Yards depot", Progress = 0, Performance = 82, Punctuality = 94, Teamwork = 71, Reliability = 88,
                    WeekHours = "32 / 40", WeekPay = "$688", PaidNote = "Friday",
                RivalName = "Marcus Lee", Mine = new[] { 71, 72, 74, 73, 77, 78, 77, 79, 81, 80, 81, 82 }, Theirs = new[] { 70, 71, 72, 70, 74, 75,
                    76, 76, 77, 78, 78, 79 }, Burnout = 22
            };
            j.Rungs.Add(new CareerRung { Title = "Picker", Pay = "$17.00", State = 0 });
            j.Rungs.Add(new CareerRung { Title = "Senior picker", Pay = "$21.50", State = 1 });
            j.Rungs.Add(new CareerRung { Title = "Shift lead", Pay = "$26.00", State = 2 });
            j.Rungs.Add(new CareerRung { Title = "Depot supervisor", Pay = "$33.00", State = 2 });
            j.Rungs.Add(new CareerRung { Title = "Depot manager", Pay = "$44.00", State = 2 });
            j.Peers.Add(new CareerPeer { Name = "Marcus Lee", Role = "Senior picker", Tag = w.T("reallife_work_up_for_post", "up for the post"), Score = 79, Team = 72, Hue = UiColour.HueOf("Marcus Lee") });
            j.Peers.Add(new CareerPeer { Name = "Pilar Duran", Role = "Shift lead", Tag = w.T("reallife_work_your_lead", "your lead"), Score = 90, Team = 88, Hue = UiColour.HueOf("Pilar Duran") });
            j.Peers.Add(new CareerPeer { Name = "Oleg Kozlov", Role = "Forklift driver", Score = 84, Team = 76, Hue = UiColour.HueOf("Oleg Kozlov") });
            j.NextRung = "Shift lead";
            j.Needs.Add(new CareerNeed { Words = "12 months as a senior picker", Met = true, Have = "you have 14" });
            j.Needs.Add(new CareerNeed { Words = "Forklift licence", Met = true, Have = "held since day 311" });
            j.Needs.Add(new CareerNeed { Words = "Performance 85", Met = false, Have = "you have 82" });
            j.Needs.Add(new CareerNeed { Words = "Teamwork 75", Met = false, Have = "you have 71" });
            string[] days = { "Fri", "Thu", "Wed", "Tue" };
            int[] pay = { 172, 172, 86, 172 }, score = { 84, 81, 79, 83 };
            for (int i = 3; i >= 0; i--)
            {
                j.Shifts.Add(new CareerShift { Day = days[i], Hours = i == 2 ? "4h" : "8h", Score = score[i], Pay = "$" + pay[i] });
            }
            j.Life.Add(new KeyValuePair<string, string>("Next review", "Day 421"));
            j.Life.Add(new KeyValuePair<string, string>("Your boss", "Fair"));
            j.Life.Add(new KeyValuePair<string, string>("Overtime", "6 h this month"));
            j.Life.Add(new KeyValuePair<string, string>("Pension pot", "$4,120"));
            f.Job = j;
        }
        JobsTab(w, f, state);
        ApplicationsTab(w, f, none);
        ProfessionsTab(w, f);
        ManagementTab(w, f);
        OwnTab(w, f);
        CvTab(w, f);
        if (state == "my_job_new")
        {
            foreach (WorkRow r in f.Jobs.Rows)
            {
                if ((r.Cells[5] == "You qualify" || r.Cells[5] == w.T("reallife_work_you_qualify", "You qualify")) && f.Best.Count < 5)
                {
                    f.Best.Add(new WorkRow { Id = r.Id, Icon = r.Icon, Name = r.Name, Sub = r.Sub + ", " + r.Cells[1], Price = r.Cells[3] + "/h", Status = r.Cells[5], Tone = UiRole.Ok });
                }
            }
        }
        return f;
    }

    private static void JobsTab(UtilitiesWords w, CareerFacts f, string state)
    {
        WorkTab jobs = f.Jobs = CareerPages.JobsShell(w);
        if (state == "jobs_empty")
        {
            return;
        }
        string longName = state == "jobs_long" ? " of the Harbourside Freight and Cold Storage Company" : string.Empty;
        object[][] posts =
        {
            new object[] { UiIcon.Trophy, "Shift lead" + longName, "Harbourside Freight HSF", "Hell's Kitchen", "09:00 to 17:00", "$26.00", 21, 2 },
            new object[] { UiIcon.Coffee, "Barista", "Brewed Beans, 5th Avenue", "Midtown", "06:00 to 14:00", "$16.50", -23, 0 },
            new object[] { UiIcon.ForkKnife, "Line cook", "Burger Barn, Broadway", "Midtown", "11:00 to 19:00", "$19.00", -12, 0 },
            new object[] { UiIcon.Heartbeat, "Pharmacy assistant", "Murray Hill Pharmacy", "Murray Hill", "09:00 to 17:00", "$18.50", -14, 3 },
            new object[] { UiIcon.Bank, "Bank teller", "Uptown Savings", "Uptown", "09:00 to 17:00", "$22.00", 2, 0 },
            new object[] { UiIcon.PaintBrush, "Seamstress", "Garment District Weavers", "Garment District", "08:00 to 16:00", "$20.50", -5, 1 },
            new object[] { UiIcon.MusicNotes, "Box office clerk", "Broadway Box Office", "Midtown", "12:00 to 20:00", "$17.50", -19, 0 },
            new object[] { UiIcon.Code, "Software tester", "Quill Software", "Garment District", "09:00 to 17:00", "$24.00", 12, 0 },
            new object[] { UiIcon.Truck, "Courier", "Harbourside Freight HSF", "Hell's Kitchen", "07:00 to 15:00", "$18.00", -16, 0 },
            new object[] { UiIcon.ShieldCheck, "Night security", "Ironbridge Hosting", "Hell's Kitchen", "22:00 to 06:00", "$20.00", -7, 3 },
            new object[] { UiIcon.Heart, "Fitness trainer", "Midtown Fitness", "Midtown", "06:00 to 14:00", "$23.00", 7, 1 },
            new object[] { UiIcon.ShoppingBag, "Shop assistant", "Thrifty Threads, Lexington", "Murray Hill", "10:00 to 18:00", "$16.00", -26, 0 },
            new object[] { UiIcon.Building, "Concierge", "The Hudson Hotel", "Midtown", "15:00 to 23:00", "$21.00", -2, 0 },
            new object[] { UiIcon.Wrench, "Maintenance fitter", "Atlas Edge", "Uptown", "08:00 to 16:00", "$25.50", 19, 2 }
        };
        for (int i = 0; i < posts.Length; i++)
        {
            object[] p = posts[i];
            WorkRow r = WkJob(w, i + 1, (UiIcon)p[0], (string)p[1], (string)p[2], (string)p[3], (string)p[4], (string)p[5], (int)p[6], (int)p[7]);
            if (i == 0)
            {
                var also = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_also_applied", "Also applied"), Note = w.F("reallife_work_n_people", "{n} people", "n", "2") };
                also.People.Add(new WorkPerson { Name = "Marcus Lee", Role = "Senior picker, performance 79", Right = "Day 399", Ring = UiRole.Ok });
                also.People.Add(new WorkPerson { Name = "Nia Brooks", Role = "Picker, performance 74", Right = "Day 400", Ring = UiRole.Ok });
                r.Sections.Add(also);
                r.HintLead = w.T("reallife_work_still_apply", "You can still apply.");
                r.Hint = "A depot manager weighs your record, and Marcus Lee has applied too. When you get the job, start each shift at your desk in the depot office.";
            }
            f.Jobs.Rows.Add(r);
        }
    }

    private static void ApplicationsTab(UtilitiesWords w, CareerFacts f, bool none)
    {
        WorkTab apps = f.Applications = CareerPages.ApplicationsShell(w);
        if (none)
        {
            return;
        }
        string[][] sent =
        {
            new[] { "Shift lead", "Harbourside Freight HSF", "Day 399", "Day 404", "Interview" }, new[] { "Barista", "Brewed Beans, 5th Avenue", "Day 400", "Day 405", "Sent" },
            new[] { "Bank teller", "Uptown Savings", "Day 396", "Day 401", "Offered" }, new[] { "Courier", "Harbourside Freight HSF", "Day 390", "Day 395", "Declined" },
            new[] { "Line cook", "Burger Barn, Broadway", "Day 388", "Day 392", "Accepted" }
        };
        UiIcon[] icons = { UiIcon.Trophy, UiIcon.Coffee, UiIcon.Bank, UiIcon.Truck, UiIcon.ForkKnife };
        for (int i = 0; i < sent.Length; i++)
        {
            string st = sent[i][4];
            UiRole tone = st == "Interview" || st == "Offered" || st == "Accepted" ? UiRole.Ok : st == "Declined" ? UiRole.Danger : UiRole.Accent;
            var r = new WorkRow { Id = 10 + i, Icon = icons[i], Name = sent[i][0], Sub = sent[i][1], Cells = new[] { null, sent[i][2], sent[i][3], st }, Status = st, Tone = tone,
                Tones = new[] { UiRole.None, UiRole.None, UiRole.None, tone }, Chip = st, ChipTone = tone };
            var steps = new WorkSection { Part = WorkPart.Checks, Title = w.T("reallife_work_where_it_stands", "Where it stands") };
            steps.Checks.Add(new WorkNeed { Words = "Sent on " + sent[i][2].ToLowerInvariant(), Met = true });
            steps.Checks.Add(new WorkNeed { Words = "Interview", Met = st == "Interview" || st == "Offered" || st == "Accepted", Have = st == "Interview" ? "day 403, 10:00" : null });
            steps.Checks.Add(new WorkNeed { Words = "Offer", Met = st == "Offered" || st == "Accepted", Have = st == "Offered" ? "$22.00 an hour" : null });
            steps.Checks.Add(new WorkNeed { Words = "Start at your desk", Met = st == "Accepted" });
            r.Sections.Add(steps);
            var terms = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_the_post", "The post") };
            terms.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_pay", "Pay"), "$" + (18 + i * 2) + ".00"));
            terms.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_hours", "Hours"), i % 2 == 0 ? "09:00 to 17:00" : "06:00 to 14:00"));
            terms.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_applied", "Applied"), sent[i][2].ToLowerInvariant()));
            terms.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_answer_by", "Answer by"), sent[i][3].ToLowerInvariant()));
            r.Sections.Add(terms);
            if (st == "Interview")
            {
                r.Acts.Add(WkAct(102, w.T("reallife_career_button_interview", "Attend the interview"), UiButtonKind.Primary, UiIcon.Check, null, false, r.Id));
                r.Acts.Add(WkAct(101, w.T("reallife_career_button_map", "Show on map"), UiButtonKind.Secondary, UiIcon.Locate, null, true, r.Id));
                r.Acts.Add(WkAct(103, w.T("reallife_career_button_withdraw", "Withdraw"), UiButtonKind.Destructive, UiIcon.Close, null, false, r.Id));
            }
            else if (st == "Offered")
            {
                r.Acts.Add(WkAct(104, w.T("reallife_career_button_accept", "Accept"), UiButtonKind.Primary, UiIcon.Check, null, false, r.Id));
                r.Acts.Add(WkAct(105, w.T("reallife_career_button_ask_small", "Ask for 5% more"), UiButtonKind.Secondary, UiIcon.TrendUp, null, false, r.Id));
                r.Acts.Add(WkAct(107, w.T("reallife_career_button_decline", "Turn it down"), UiButtonKind.Destructive, UiIcon.Close, null, false, r.Id));
            }
            else if (st == "Sent")
            {
                r.Acts.Add(WkAct(101, w.T("reallife_career_button_map", "Show on map"), UiButtonKind.Secondary, UiIcon.Locate, null, true, r.Id));
                r.Acts.Add(WkAct(103, w.T("reallife_career_button_withdraw", "Withdraw"), UiButtonKind.Destructive, UiIcon.Close, null, false, r.Id));
            }
            apps.Rows.Add(r);
        }
        apps.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_waiting_answer", "Waiting for an answer"), "2"));
        apps.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_interviews", "Interviews"), "1"));
        apps.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_offers", "Offers"), "1"));
        apps.NoteLead = w.T("reallife_work_apps_note_lead", "An answer takes a day or two.");
        apps.NoteText = w.T("reallife_work_apps_note", "An employer weighs your record against anyone else who applied. Ask for a little more only when your record is strong.");
        f.ApplicationsWaiting = apps.Rows.Count;
    }

    private static void ProfessionsTab(UtilitiesWords w, CareerFacts f)
    {
        WorkTab pro = f.Professions = CareerPages.ProfessionsShell(w);
        object[][] trades =
        {
            new object[] { UiIcon.Package, "Picker", "Warehouse picker", "Mon to Fri, 09:00 to 17:00", "$17.00", 6, true }, new object[] { UiIcon.Truck, "Courier", "Courier", "07:00 to 15:00", "$18.00", 4, true },
            new object[] { UiIcon.Coffee, "Barista", "Barista", "06:00 to 14:00", "$16.50", 3, true }, new object[] { UiIcon.ForkKnife, "Line cook", "Line cook", "11:00 to 19:00", "$19.00", 5, true },
            new object[] { UiIcon.Bank, "Bank teller", "Bank teller", "09:00 to 17:00", "$22.00", 2, true }, new object[] { UiIcon.Code, "Software tester", "Tester", "09:00 to 17:00", "$24.00", 1, false },
            new object[] { UiIcon.Heartbeat, "Pharmacy", "Pharmacy assistant", "09:00 to 17:00", "$18.50", 1, false }, new object[] { UiIcon.Wrench, "Fitter", "Maintenance fitter", "08:00 to 16:00", "$25.50", 2, false }
        };
        for (int i = 0; i < trades.Length; i++)
        {
            object[] p = trades[i];
            bool open = (bool)p[6];
            string pay = (string)p[4];
            int whole = int.Parse(pay.Substring(1, 2));
            string status = open ? w.T("reallife_work_open_to_you", "Open to you") : w.T("reallife_work_needs_course", "Needs a course");
            var r = new WorkRow { Id = 20 + i, Icon = (UiIcon)p[0], Name = (string)p[1], Sub = w.F("reallife_work_first_post", "First post: {post}", "post", (string)p[2]),
                Cells = new[] { null, (string)p[3], pay, ((int)p[5]).ToString(), status }, Values = new[] { 0, 0, 0, (int)p[5] * 14, 0 }, Status = status, Tone = open ? UiRole.Ok : UiRole.Warn,
                Chip = status, ChipTone = open ? UiRole.Ok : UiRole.Warn };
            var ladder = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_the_ladder", "The ladder") };
            ladder.Pairs.Add(new KeyValuePair<string, string>((string)p[2], pay));
            ladder.Pairs.Add(new KeyValuePair<string, string>("Senior " + ((string)p[2]).ToLowerInvariant(), "$" + (whole + 4) + ".50"));
            ladder.Pairs.Add(new KeyValuePair<string, string>("Shift lead", "$" + (whole + 9) + ".00"));
            ladder.Pairs.Add(new KeyValuePair<string, string>("Supervisor", "$" + (whole + 16) + ".00"));
            r.Sections.Add(ladder);
            var needs = new WorkSection { Part = WorkPart.Checks, Title = w.T("reallife_work_to_start", "To start") };
            needs.Checks.Add(new WorkNeed { Words = open ? "No qualification is asked" : "A course in the trade", Met = open });
            r.Sections.Add(needs);
            var hiring = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_hiring_now", "Hiring now"), Note = ((int)p[5]).ToString() };
            string[] firms = { "Harbourside Freight HSF", "Brewed Beans, 5th Avenue", "Uptown Savings" };
            for (int k = 0; k < System.Math.Min(3, (int)p[5]); k++)
            {
                hiring.People.Add(new WorkPerson { Name = firms[(i + k) % 3], Role = (string)p[2], Right = pay, Icon = UiIcon.Store });
            }
            r.Sections.Add(hiring);
            r.Acts.Add(WkAct(WorkKinds.GoTab, w.T("reallife_career_button_vacancies", "Vacancies"), UiButtonKind.Primary, UiIcon.Search, null, true, CareerPages.Jobs));
            r.Acts.Add(WkAct(112, w.T("reallife_career_button_see_courses", "See the courses"), UiButtonKind.Secondary, UiIcon.Books, null, true, r.Id));
            pro.Rows.Add(r);
        }
        pro.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_open_posts", "Open posts"), "24"));
        pro.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_open_to_you", "Open to you"), "5"));
        pro.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_pay_range", "Starting pay"), "$16.50 to $25.50"));
    }

    private static void ManagementTab(UtilitiesWords w, CareerFacts f)
    {
        WorkTab man = f.Management;
        man.Shape = WorkShape.Form;
        var form = new WorkFormModel { Lead = "You run the depot floor. Decide what the staff ask, set the policy within your powers and keep the stores staffed." };
        var decide = new WorkFormBand { Title = w.T("reallife_work_group_decide", "To decide"), Count = "1" };
        decide.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = "Cover a gap on today's shift", Hint = "Pilar Duran, Hudson Yards depot", Value = "Day 406", Icon = UiIcon.People });
        decide.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = WkAct(113, w.T("reallife_ladder_button_yes", "Agree"), UiButtonKind.Primary, UiIcon.Check, null, false, 1) });
        decide.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = WkAct(114, w.T("reallife_ladder_button_no", "Refuse"), UiButtonKind.Secondary, UiIcon.Close, null, false, 1) });
        decide.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = WkAct(115, w.T("reallife_ladder_button_up", "Pass it up"), UiButtonKind.Secondary, UiIcon.Up, null, false, 1) });
        form.Left.Add(decide);
        var policy = new WorkFormBand { Title = w.T("reallife_work_group_policy", "Policy") };
        man.Amounts.Add(new WorkAmount { Id = 0, Value = 1, Min = 0, Max = 2, Words = new[] { "Lean", "Standard", "Full" }, Figure = "Standard" });
        man.Amounts.Add(new WorkAmount { Id = 1, Value = 3, Min = 1, Max = 7, Figure = "3 days" });
        policy.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = "Rota for the store", Stepper = 0 });
        policy.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = "Escalate after", Stepper = 1 });
        policy.Rows.Add(new WorkFormRow { Field = WorkField.Switch, Label = w.T("reallife_ladder_aiming", "Put my name forward"), On = true, Act = WkAct(119, w.T("reallife_ladder_aiming", "Put my name forward"), UiButtonKind.Secondary, UiIcon.Check) });
        form.Left.Add(policy);
        var ask = new WorkFormBand { Title = w.T("reallife_ladder_sec_requests", "Your requests") };
        ask.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = "A shift", Act = WkAct(128, w.T("reallife_work_ask_shift_earlier", "Ask for it earlier"), UiButtonKind.Secondary, UiIcon.Back) });
        ask.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = WkAct(129, w.T("reallife_work_ask_shift_later", "Ask for it later"), UiButtonKind.Secondary, UiIcon.Next) });
        ask.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = "Equipment", Act = WkAct(130, w.T("reallife_work_ask_equipment", "Ask for equipment"), UiButtonKind.Secondary, UiIcon.Wrench) });
        ask.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = "Insurance", Act = WkAct(131, w.T("reallife_work_ask_insurance", "Ask for insurance"), UiButtonKind.Secondary, UiIcon.ShieldCheck,
            w.T("reallife_work_granted", "Already granted.")) });
        form.Left.Add(ask);
        var staff = new WorkFormBand { Title = w.T("reallife_work_group_staff", "Staff"), Count = w.F("reallife_work_n_people", "{n} people", "n", "3") };
        string[] names = { "Oleg Kozlov", "Nia Brooks", "Priya Shah" };
        int[] morale = { 76, 66, 81 };
        for (int i = 0; i < names.Length; i++)
        {
            staff.Rows.Add(new WorkFormRow { Field = WorkField.Person, Person = new WorkPerson { Name = names[i], Role = "Picker, $" + (17 + i) + ".00 an hour", Right = "Morale " + morale[i], Ring = UiRole.Ok },
                Act = WkAct(132, w.T("reallife_work_raise_their_pay", "Raise their pay"), UiButtonKind.Secondary, UiIcon.TrendUp, null, false, i + 1) });
        }
        form.Right.Add(staff);
        var stores = new WorkFormBand { Title = w.T("reallife_work_group_stores", "Stores"), Count = "2" };
        stores.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = "Hudson Yards depot", Hint = "Standard rota, 8 of 10 staff", Value = "Results 82", Icon = UiIcon.Store });
        stores.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = "Pier 9 cold store", Hint = "Lean rota, 5 of 6 staff", Value = "Results 74", Icon = UiIcon.Store });
        form.Right.Add(stores);
        man.Form = form;
    }

    private static void OwnTab(UtilitiesWords w, CareerFacts f)
    {
        WorkTab own = f.Own;
        own.Shape = WorkShape.Form;
        var form = new WorkFormModel { Lead = w.T("reallife_own_practice_about", "Work for yourself: a practice of your profession, or a company of your own.") };
        var practice = new WorkFormBand { Title = w.T("reallife_own_practice", "Your own practice") };
        practice.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = "Profession", Value = "Carpenter" });
        practice.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = "Reputation", Value = "34" });
        practice.Foot = WkAct(134, w.T("reallife_own_button_close_practice", "Close the practice"), UiButtonKind.Destructive, UiIcon.Close);
        form.Left.Add(practice);
        var found = new WorkFormBand { Title = w.T("reallife_own_found_title", "Found a company"), Note = w.T("reallife_own_found_note", "Leave your job and start a company of your own. Colleagues who would come with you can follow.") };
        found.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = "On your own", Act = WkAct(135, w.T("reallife_own_button_found", "Leave to found a company"), UiButtonKind.Secondary, UiIcon.Building) });
        found.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = "With colleagues", Act = WkAct(136, w.T("reallife_own_button_found_with", "Found it with colleagues"), UiButtonKind.Secondary, UiIcon.People,
            w.T("reallife_work_no_colleagues", "No colleagues would come with you.")) });
        form.Left.Add(found);
        var contracts = new WorkFormBand { Title = w.T("reallife_own_contracts", "Contracts"), Count = "2" };
        contracts.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = "Rose Wilder", Hint = "Carpenter, a shelf and a door frame", Value = "$180", Icon = UiIcon.Wrench });
        contracts.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = "Due day 410", Act = WkAct(116, w.T("reallife_own_button_take", "Take it on"), UiButtonKind.Primary, UiIcon.Check, null, false, 40) });
        contracts.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = "Hollis and Sons", Hint = "Carpenter, a kitchen fit", Value = "$640", Icon = UiIcon.Hammer });
        contracts.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = "Due day 418", Act = WkAct(117, w.T("reallife_own_button_drop", "Drop it"), UiButtonKind.Destructive, UiIcon.Close, null, false, 41) });
        form.Right.Add(contracts);
        own.Form = form;
    }

    private static void CvTab(UtilitiesWords w, CareerFacts f)
    {
        WorkTab cv = f.Cv;
        cv.Shape = WorkShape.Reader;
        cv.Reader = new UiReaderModel
        {
            Title = "Jordan Hayes", Picture = UiIcon.Person, PictureCaption = w.T("reallife_work_cv_caption", "Your working life, as an employer reads it"), FactsTitle = w.T("reallife_work_cv_in_short", "In short"),
            Facts = new[] { new UiPairModel { Key = "Jobs held", Value = "3" }, new UiPairModel { Key = "Earned", Value = "$38,420" }, new UiPairModel { Key = "Days in work", Value = "412" },
                new UiPairModel { Key = "References", Value = "2 good" } }
        };
        cv.Reader.Blocks = new[]
        {
            new UiReaderBlock { Kind = UiReaderKind.Heading, Text = w.T("reallife_work_cv_working_life", "Working life") },
            new UiReaderBlock { Kind = UiReaderKind.Paragraph, Text = "Senior picker, Harbourside Freight HSF. Day 388 to now. Your job now." },
            new UiReaderBlock { Kind = UiReaderKind.Paragraph, Text = "Picker, Harbourside Freight HSF. Day 330 to day 388. Promoted. A good reference." },
            new UiReaderBlock { Kind = UiReaderKind.Paragraph, Text = "Courier, Quick Parcels. Day 250 to day 330. You left. A fair reference." },
            new UiReaderBlock { Kind = UiReaderKind.Heading, Text = w.T("reallife_work_cv_qualifications", "Qualifications") },
            new UiReaderBlock { Kind = UiReaderKind.Bullet, Text = "Forklift and Warehouse Safety, Garment District Trade School, day 311. Licence won." },
            new UiReaderBlock { Kind = UiReaderKind.Bullet, Text = "Business Maths, Midtown Community College, day 330. Passed with an A." },
            new UiReaderBlock { Kind = UiReaderKind.Heading, Text = w.T("reallife_work_cv_pension", "Pension") },
            new UiReaderBlock { Kind = UiReaderKind.Paragraph, Text = "Your pension pot holds $4,120. You can retire from age 60, and from 55 with a cut." }
        };
        cv.Acts.Add(WkAct(118, w.T("reallife_life_button_retire", "Retire"), UiButtonKind.Destructive, UiIcon.SignOut, w.T("reallife_work_why_retire", "You can retire from age 55.")));
    }
}
