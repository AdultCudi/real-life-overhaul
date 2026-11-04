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

    // A post of the board: the profession, the rank, the employer, where, the hours, the pay in cents, the pay against the player's, what is open of its needs
    // (0 none, 1 the performance, 2 the performance and the skills, 3 a licence the player lacks).
    private sealed class WkPost
    {
        public UiIcon Icon;
        public string Prof;
        public int Rank;
        public string Employer, Where;
        public int From, To, Pay, Against, Open;
        public string Suffix = string.Empty;
    }

    private static string WkRate(int cents) => "$" + (cents / 100) + "." + (cents % 100).ToString("00");

    private static WorkRow WkJob(UtilitiesWords w, int id, WkPost p)
    {
        string qualify = w.T("reallife_work_you_qualify", "You qualify");
        string title = SRank(w, p.Prof, p.Rank) + p.Suffix, pay = WkRate(p.Pay), hours = SHours(w, p.From, p.To);
        bool sameEmployer = p.Employer == "Harbourside Freight HSF" && p.Prof == "warehouse";
        // what it needs, as the live source lists it: the qualification, the skill points, the employer's wish, and at the player's own employer the promotion's bars
        var checks = new WorkSection { Part = WorkPart.Checks, Title = w.T("reallife_work_what_it_needs", "What it needs") };
        if (p.Open == 3)
        {
            checks.Checks.Add(new WorkNeed { Words = SCourse(w, "forklift"), Met = false });
        }
        checks.Checks.Add(new WorkNeed { Words = Sf(w, "reallife_work_need_skill_points", "n", "60"), Met = p.Open != 2, Have = p.Open == 2 ? SHave(w, 52) : null });
        checks.Checks.Add(new WorkNeed { Words = w.T("reallife_work_need_takes_you", "The employer takes people like you"), Met = p.Open != 3 });
        if (sameEmployer)
        {
            checks.Checks.Add(new WorkNeed { Words = Sf(w, "reallife_work_need_performance", "n", "85"), Met = false, Have = SHave(w, 82) });
        }
        int met = 0;
        foreach (WorkNeed need in checks.Checks)
        {
            met += need.Met ? 1 : 0;
        }
        int open = checks.Checks.Count - met;
        checks.Note = Sf(w, "reallife_work_needs_met", "met", met.ToString(), "all", checks.Checks.Count.ToString());
        string status = open == 0 ? qualify : p.Open == 3 ? w.T("reallife_work_needs_licence", "Needs a licence") : Sf(w, "reallife_work_needs_n_more", "n", open.ToString());
        UiRole tone = open == 0 ? UiRole.Ok : UiRole.Warn;
        var r = new WorkRow { Id = id, Order = p.Pay, Icon = p.Icon, Name = title, Sub = p.Employer, Cells = new[] { null, p.Where, hours, pay, WorkBuilder.Percent(p.Against), status },
            Values = new[] { 0, 0, 0, 0, p.Against < 0 ? -1 : p.Against > 0 ? 1 : 0, 0 },
            Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone }, Status = status, Tone = tone };
        r.Mask = 1 | (open == 0 ? 2 : 0) | (id % 3 == 0 ? 4 : 0);
        r.Chip = p.Employer;
        r.ChipTone = UiRole.Accent;
        r.HeadSub = p.Where;
        r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_career_col_pay", "Pay"), SPerHour(w, pay)));
        r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_hours", "Hours"), hours));
        r.Acts.Add(new WorkAct { Kind = 100, Arg = id, Label = w.T("reallife_career_button_apply", "Apply"), Style = UiButtonKind.Primary, Icon = UiIcon.Check,
            Why = p.Open == 3 ? w.T("reallife_work_why_unqualified", "You do not hold what that post needs.") : null });
        r.Acts.Add(new WorkAct { Kind = 101, Arg = id, Label = w.T("reallife_career_button_map", "Show on map"), Style = UiButtonKind.Secondary, Icon = UiIcon.Locate, Opens = true });
        r.Sections.Add(checks);
        var shift = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_the_shift", "The shift") };
        shift.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_shift_days", "Days"), Sf(w, "reallife_career_row_hours", "from", SWd(w, 0), "to", SWd(w, 4))));
        r.Sections.Add(shift);
        var against2 = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_pay_against", "Against your pay") };
        against2.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_your_pay_now", "Your pay now"), SPerHour(w, "$21.50")));
        against2.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_difference", "The difference"), WorkBuilder.Percent(p.Against)));
        r.Sections.Add(against2);
        return r;
    }

    private static string WkApplicantRole(UtilitiesWords w, string prof, int rank, int score) => SRank(w, prof, rank) + ", " + Sf(w, "reallife_work_performance_n", "n", score.ToString());

    public static CareerFacts CareerFactsOf(string state, UiWords t = null)
    {
        UtilitiesWords w = UtilitiesWordsOf(t ?? ((k, e) => e));
        bool none = state == "my_job_new" || state == "jobs_empty";
        var f = new CareerFacts { Ready = true, Player = "Jordan Hayes", Day = SDay(w, Today) };
        if (!none)
        {
            // the timeline, against today (412): a courier at Quick Parcels from day 18 to 80, a forklift operator at Harbourside from 82, the team leader since 190
            var j = new CareerJob
            {
                Title = SRank(w, "warehouse", 1), Employer = "Harbourside Freight HSF", Pay = SPerHour(w, "$21.50"), Hours = SWeek(w, 0, 4, 9, 17),
                    Months = SMonths(w, (Today - 82) / 30), Status = w.T("reallife_work_status_on_time", "On time"),
                NextTitle = w.T("reallife_work_next_shift", "Next shift"), NextLine = SWeek(w, 0, 4, 9, 17),
                    Desk = Sx(w, "reallife_career_desk_how"), ShiftTicks = new[] { "09:00", "13:00", "17:00" },
                NextNote = "Hudson Yards depot", Progress = 0, Performance = 82, Punctuality = 94, Teamwork = 71, Reliability = 88,
                    WeekHours = "32 / 40", WeekPay = "$688", PaidNote = SWd(w, 4),
                RivalName = "Marcus Lee", Mine = new[] { 71, 72, 74, 73, 77, 78, 77, 79, 81, 80, 81, 82 }, Theirs = new[] { 70, 71, 72, 70, 74, 75,
                    76, 76, 77, 78, 78, 79 }, Burnout = 22, SafetyKept = false
            };
            j.Rungs.Add(new CareerRung { Title = SRank(w, "warehouse", 0), Pay = "$17.00", State = 0 });
            j.Rungs.Add(new CareerRung { Title = SRank(w, "warehouse", 1), Pay = "$21.50", State = 1 });
            j.Rungs.Add(new CareerRung { Title = SRank(w, "warehouse", 2), Pay = "$33.00", State = 2 });
            j.Peers.Add(new CareerPeer { Name = "Marcus Lee", Role = SRank(w, "warehouse", 1), Tag = Sx(w, "reallife_work_rival_for_post"), Tone = UiRole.Warn, Score = 79, Team = 72, Hue = UiColour.HueOf("Marcus Lee") });
            j.Peers.Add(new CareerPeer { Name = "Pilar Duran", Role = SRank(w, "warehouse", 2), Tag = Sx(w, "reallife_work_your_lead_chip"), Tone = UiRole.Accent, Score = 90, Team = 88, Hue = UiColour.HueOf("Pilar Duran") });
            j.Peers.Add(new CareerPeer { Name = "Oleg Kozlov", Role = SRank(w, "warehouse", 0), Score = 84, Team = 76, Hue = UiColour.HueOf("Oleg Kozlov") });
            j.NextRung = SRank(w, "warehouse", 2);
            j.Needs.Add(new CareerNeed { Words = Sf(w, "reallife_work_need_days", "n", "180"), Met = true, Have = SHave(w, Today - 190) });
            j.Needs.Add(new CareerNeed { Words = Sf(w, "reallife_work_need_skill_points", "n", "72"), Met = false, Have = SHave(w, 66) });
            j.Needs.Add(new CareerNeed { Words = SCourse(w, "forklift"), Met = true });
            j.Needs.Add(new CareerNeed { Words = Sf(w, "reallife_work_need_rating", "n", "4"), Met = false, Have = SHave(w, 3) });
            int[] pay = { 172, 86, 172, 172 }, score = { 84, 81, 79, 83 };
            for (int i = 3; i >= 0; i--)
            {
                j.Shifts.Add(new CareerShift { Day = SDay(w, Today - 1 - i), Hours = i == 1 ? "4h" : "8h", Score = score[i], Pay = "$" + pay[i] });
            }
            j.Life.Add(new KeyValuePair<string, string>(Sx(w, "reallife_ibox_next_review"), SDay(w, 421)));
            j.Life.Add(new KeyValuePair<string, string>(Sx(w, "reallife_ibox_boss"), Sx(w, "reallife_life_boss_mentor")));
            j.Life.Add(new KeyValuePair<string, string>(Sx(w, "reallife_ibox_overtime"), Sf(w, "reallife_study_hours_minutes", "hours", "6", "minutes", "00")));
            j.Life.Add(new KeyValuePair<string, string>(Sx(w, "reallife_life_sum_pension"), "$4,120"));
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
            var rows = new List<WorkRow>(f.Jobs.Rows);
            WorkBuilder.BestPaid(rows, 2);
            for (int i = 0; i < rows.Count && i < 5; i++)
            {
                WorkRow r = rows[i];
                f.Best.Add(new WorkRow { Id = r.Id, Icon = r.Icon, Name = r.Name, Sub = r.Sub + ", " + r.Cells[1], Price = r.Cells[3] + "/h", Status = r.Cells[5], Tone = UiRole.Ok });
            }
            CareerPages.NewPlayerBands(w, f, 62, 2, 1, 0, new[] { SCourse(w, "forklift"), SCourse(w, "customer_service"), SCourse(w, "business") },
                new[] { SRank(w, "warehouse", 0), SRank(w, "bar", 0), SRank(w, "accountancy", 0) });
            CareerPages.NewPlayerSupport(w, f, "$310", SDay(w, 440), SDay(w, 431), new[] { UiIcon.Package, UiIcon.Coffee },
                new[] { SRank(w, "warehouse", 1), SRank(w, "bar", 0) }, new[] { "Harbourside Freight HSF", "The Anchor" },
                new[] { Sx(w, "reallife_work_leaving_redundant"), Sx(w, "reallife_work_leaving_resigned") });
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
        string longName = state == "jobs_long" ? " / " + SRank(w, "chef", 4) + " / " + SRank(w, "sales", 4) : string.Empty;
        WkPost[] posts =
        {
            new WkPost { Icon = UiIcon.Trophy, Prof = "warehouse", Rank = 2, Suffix = longName, Employer = "Harbourside Freight HSF", Where = "Hell's Kitchen", From = 9, To = 17, Pay = 3300, Against = 53, Open = 1 },
            new WkPost { Icon = UiIcon.Coffee, Prof = "bar", Rank = 0, Employer = "Brewed Beans, 5th Avenue", Where = "Midtown", From = 6, To = 14, Pay = 1650, Against = -23, Open = 0 },
            new WkPost { Icon = UiIcon.ForkKnife, Prof = "chef", Rank = 0, Employer = "Burger Barn, Broadway", Where = "Midtown", From = 11, To = 19, Pay = 1900, Against = -12, Open = 0 },
            new WkPost { Icon = UiIcon.Heartbeat, Prof = "nursing", Rank = 0, Employer = "Murray Hill Pharmacy", Where = "Murray Hill", From = 9, To = 17, Pay = 1850, Against = -14, Open = 3 },
            new WkPost { Icon = UiIcon.Bank, Prof = "accountancy", Rank = 0, Employer = "Uptown Savings", Where = "Uptown", From = 9, To = 17, Pay = 2200, Against = 2, Open = 0 },
            new WkPost { Icon = UiIcon.PaintBrush, Prof = "design", Rank = 0, Employer = "Garment District Weavers", Where = "Garment District", From = 8, To = 16, Pay = 2050, Against = -5, Open = 2 },
            new WkPost { Icon = UiIcon.MusicNotes, Prof = "sales", Rank = 0, Employer = "Broadway Box Office", Where = "Midtown", From = 12, To = 20, Pay = 1750, Against = -19, Open = 0 },
            new WkPost { Icon = UiIcon.Code, Prof = "it_support", Rank = 0, Employer = "Quill Software", Where = "Garment District", From = 9, To = 17, Pay = 2400, Against = 12, Open = 0 },
            new WkPost { Icon = UiIcon.Truck, Prof = "warehouse", Rank = 0, Employer = "Harbourside Freight Docks", Where = "Hell's Kitchen", From = 7, To = 15, Pay = 1800, Against = -16, Open = 0 },
            new WkPost { Icon = UiIcon.ShieldCheck, Prof = "guard", Rank = 0, Employer = "Ironbridge Hosting", Where = "Hell's Kitchen", From = 22, To = 6, Pay = 2000, Against = -7, Open = 3 },
            new WkPost { Icon = UiIcon.Heart, Prof = "marketing", Rank = 0, Employer = "Midtown Fitness", Where = "Midtown", From = 6, To = 14, Pay = 2300, Against = 7, Open = 1 },
            new WkPost { Icon = UiIcon.ShoppingBag, Prof = "hr", Rank = 0, Employer = "Thrifty Threads, Lexington", Where = "Murray Hill", From = 10, To = 18, Pay = 1600, Against = -26, Open = 0 },
            new WkPost { Icon = UiIcon.Building, Prof = "teaching", Rank = 0, Employer = "The Hudson Hotel", Where = "Midtown", From = 15, To = 23, Pay = 2100, Against = -2, Open = 0 },
            new WkPost { Icon = UiIcon.Wrench, Prof = "chef", Rank = 1, Employer = "Atlas Edge", Where = "Uptown", From = 8, To = 16, Pay = 2550, Against = 19, Open = 2 }
        };
        for (int i = 0; i < posts.Length; i++)
        {
            WorkRow r = WkJob(w, i + 1, posts[i]);
            if (i == 0)
            {
                var also = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_also_applied", "Also applied"), Note = w.F("reallife_work_n_people", "{n} people", "n", "2") };
                also.People.Add(new WorkPerson { Name = "Marcus Lee", Role = WkApplicantRole(w, "warehouse", 1, 79), Right = SDay(w, 409), Ring = UiRole.Ok });
                also.People.Add(new WorkPerson { Name = "Nia Brooks", Role = WkApplicantRole(w, "warehouse", 0, 74), Right = SDay(w, 410), Ring = UiRole.Ok });
                r.Sections.Add(also);
                r.HintLead = w.T("reallife_work_still_apply", "You can still apply.");
                r.Hint = Sf(w, "reallife_work_weighs_all", "name", "Marcus Lee");
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
        // against today (412): the answers are still to come, an offer or an interview is ahead; a closed one has no answer day
        object[][] sent =
        {
            new object[] { "warehouse", 2, "Harbourside Freight HSF", 408, 413, 1, UiIcon.Trophy, 3300 }, new object[] { "bar", 0, "Brewed Beans, 5th Avenue", 410, 414, 0, UiIcon.Coffee, 1650 },
            new object[] { "accountancy", 0, "Uptown Savings", 406, 416, 2, UiIcon.Bank, 2200 }, new object[] { "warehouse", 0, "Harbourside Freight Docks", 396, 401, 3, UiIcon.Truck, 1800 },
            new object[] { "chef", 0, "Burger Barn, Broadway", 388, 392, 4, UiIcon.ForkKnife, 1900 }
        };
        string[] stepKeys = { "reallife_work_step_sent", "reallife_work_step_interview", "reallife_work_step_offered", "reallife_work_step_declined", "reallife_work_step_accepted" };
        for (int i = 0; i < sent.Length; i++)
        {
            string prof = (string)sent[i][0];
            int rank = (int)sent[i][1], sentDay = (int)sent[i][3], answerDay = (int)sent[i][4], step = (int)sent[i][5];
            string st = Sx(w, stepKeys[step]), title = SRank(w, prof, rank);
            bool live = step <= 2;
            UiRole tone = step == 1 || step == 2 || step == 4 ? UiRole.Ok : step == 3 ? UiRole.Danger : UiRole.Accent;
            string rate = WkRate((int)sent[i][7]);
            var r = new WorkRow { Id = 10 + i, Order = answerDay, Icon = (UiIcon)sent[i][6], Name = title, Sub = (string)sent[i][2],
                Cells = new[] { null, SDay(w, sentDay), live ? SDay(w, answerDay) : Sx(w, "reallife_work_answer_closed"), st },
                Status = st, Tone = tone, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, tone }, Chip = st, ChipTone = tone };
            var stands = new WorkSection { Part = WorkPart.Checks, Title = w.T("reallife_work_where_it_stands", "Where it stands") };
            stands.Checks.Add(new WorkNeed { Words = Sf(w, "reallife_work_sent_on", "day", SDay(w, sentDay)), Met = true });
            stands.Checks.Add(new WorkNeed { Words = Sx(w, "reallife_work_step_interview"), Met = step == 1 || step == 2 || step == 4, Pending = true, Have = step == 1 ? SDay(w, 413) + ", 10:00" : null });
            stands.Checks.Add(new WorkNeed { Words = Sx(w, "reallife_work_step_offer"), Met = step == 2 || step == 4, Pending = true, Have = step == 2 ? SPerHour(w, rate) : null });
            stands.Checks.Add(new WorkNeed { Words = Sx(w, "reallife_work_step_start_desk"), Met = step == 4, Pending = true });
            r.Sections.Add(stands);
            var terms = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_the_post", "The post") };
            terms.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_pay", "Pay"), SPerHour(w, rate)));
            terms.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_hours", "Hours"), SHours(w, i % 2 == 0 ? 9 : 6, i % 2 == 0 ? 17 : 14)));
            terms.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_applied", "Applied"), SDay(w, sentDay)));
            if (live)
            {
                terms.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_answer_by", "Answer by"), SDay(w, answerDay)));
            }
            r.Sections.Add(terms);
            if (live)
            {
                var also = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_also_applied", "Also applied"), Note = w.F("reallife_work_n_people", "{n} people", "n", "2") };
                also.People.Add(new WorkPerson { Name = "Marcus Lee", Role = WkApplicantRole(w, prof, rank, 79), Right = SDay(w, sentDay + 1), Ring = UiRole.Ok });
                also.People.Add(new WorkPerson { Name = "Nia Brooks", Role = WkApplicantRole(w, prof, System.Math.Max(0, rank - 1), 74), Right = SDay(w, sentDay + 2), Ring = UiRole.Ok });
                r.Sections.Add(also);
            }
            if (step == 1)
            {
                r.Acts.Add(WkAct(102, w.T("reallife_career_button_interview", "Attend the interview"), UiButtonKind.Primary, UiIcon.Check, null, false, r.Id));
                r.Acts.Add(WkAct(101, w.T("reallife_career_button_map", "Show on map"), UiButtonKind.Secondary, UiIcon.Locate, null, true, r.Id));
                r.Acts.Add(WkAct(103, w.T("reallife_career_button_withdraw", "Withdraw"), UiButtonKind.Destructive, UiIcon.Close, null, false, r.Id));
            }
            else if (step == 2)
            {
                r.Acts.Add(WkAct(104, w.T("reallife_career_button_accept", "Accept"), UiButtonKind.Primary, UiIcon.Check, null, false, r.Id));
                r.Acts.Add(WkAct(105, w.T("reallife_career_button_ask_small", "Ask for 5% more"), UiButtonKind.Secondary, UiIcon.TrendUp, null, false, r.Id));
                r.Acts.Add(WkAct(107, w.T("reallife_career_button_decline", "Turn it down"), UiButtonKind.Destructive, UiIcon.Close, null, false, r.Id));
            }
            else if (step == 0)
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
        CareerPages.ApplicationsBands(w, apps, new[] { 413, 414, 416 }, new[] { SDay(w, 413) + ", 10:00", SDay(w, 414), SDay(w, 416) }, new[] { 1, 0, 2 },
            new[] { SRank(w, "warehouse", 2) + ", Harbourside Freight HSF", SRank(w, "bar", 0) + ", Brewed Beans, 5th Avenue", SRank(w, "accountancy", 0) + ", Uptown Savings" },
            new[] { 82, 94, 71, 88 });
        f.ApplicationsWaiting = apps.Rows.Count;
    }

    private static void ProfessionsTab(UtilitiesWords w, CareerFacts f)
    {
        WorkTab pro = f.Professions = CareerPages.ProfessionsShell(w);
        // profession key, glyph, hours from and to, first pay in cents, posts open, open to the player, the pay of each rank
        object[][] trades =
        {
            new object[] { "warehouse", UiIcon.Package, 9, 17, 1700, 6, true, new[] { 1700, 2150, 3300 } }, new object[] { "bar", UiIcon.Coffee, 6, 14, 1650, 3, true, new[] { 1650, 1950, 2600 } },
            new object[] { "chef", UiIcon.ForkKnife, 11, 19, 1900, 5, true, new[] { 1900, 2300, 2800, 3500 } }, new object[] { "accountancy", UiIcon.Bank, 9, 17, 2200, 2, true, new[] { 2200, 2800, 3400, 4300 } },
            new object[] { "it_support", UiIcon.Code, 9, 17, 2400, 1, false, new[] { 2400, 3000, 3900 } }, new object[] { "nursing", UiIcon.Heartbeat, 9, 17, 1850, 1, false, new[] { 1850, 2400, 3000, 3700 } },
            new object[] { "guard", UiIcon.ShieldCheck, 22, 6, 2000, 2, false, new[] { 2000, 2500, 3200 } }, new object[] { "sales", UiIcon.ShoppingBag, 10, 18, 1800, 3, true, new[] { 1800, 2300, 2900, 3600 } }
        };
        string[] firms = { "Harbourside Freight HSF", "Brewed Beans, 5th Avenue", "Uptown Savings", "Quill Software", "Midtown Fitness", "The Hudson Hotel" };
        int posts = 0, openTo = 0;
        for (int i = 0; i < trades.Length; i++)
        {
            object[] p = trades[i];
            string key = (string)p[0];
            bool open = (bool)p[6];
            int count = (int)p[5];
            posts += count;
            openTo += open ? 1 : 0;
            int[] ranks = (int[])p[7];
            string pay = WkRate((int)p[4]);
            string status = open ? w.T("reallife_work_open_to_you", "Open to you") : w.T("reallife_work_needs_course", "Needs a course");
            var r = new WorkRow { Id = 20 + i, Icon = (UiIcon)p[1], Name = SPro(w, key), Sub = Sf(w, "reallife_work_first_post", "post", SRank(w, key, 0)),
                Cells = new[] { null, SHours(w, (int)p[2], (int)p[3]), pay, count.ToString(), status }, Values = new[] { 0, 0, 0, count * 14, 0 }, Status = status, Tone = open ? UiRole.Ok : UiRole.Warn,
                Chip = status, ChipTone = open ? UiRole.Ok : UiRole.Warn };
            var ladder = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_the_ladder", "The ladder") };
            for (int k = 0; k < ranks.Length; k++)
            {
                ladder.Pairs.Add(new KeyValuePair<string, string>(SRank(w, key, k), WkRate(ranks[k])));
            }
            r.Sections.Add(ladder);
            var needs = new WorkSection { Part = WorkPart.Checks, Title = w.T("reallife_work_to_start", "To start") };
            needs.Checks.Add(new WorkNeed { Words = open ? w.T("reallife_work_no_qualification", "No qualification is asked") : SCourse(w, "forklift"), Met = open });
            needs.Checks.Add(new WorkNeed { Words = Sf(w, "reallife_work_need_skill_points", "n", "40"), Met = true });
            r.Sections.Add(needs);
            var hiring = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_hiring_now", "Hiring now"), Note = count.ToString() };
            for (int k = 0; k < System.Math.Min(6, count); k++)
            {
                hiring.People.Add(new WorkPerson { Name = firms[(i + k) % firms.Length], Role = SRank(w, key, 0), Right = pay, Icon = UiIcon.Store });
            }
            r.Sections.Add(hiring);
            r.Acts.Add(WkAct(WorkKinds.GoTab, w.T("reallife_career_button_vacancies", "Vacancies"), UiButtonKind.Primary, UiIcon.Search, null, true, CareerPages.Jobs));
            r.Acts.Add(WkAct(112, w.T("reallife_career_button_see_courses", "See the courses"), UiButtonKind.Secondary, UiIcon.Books, null, true, r.Id));
            pro.Rows.Add(r);
        }
        pro.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_open_posts", "Open posts"), posts.ToString()));
        pro.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_open_to_you", "Open to you"), openTo.ToString()));
        pro.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_pay_range", "Starting pay"), Sf(w, "reallife_career_row_hours", "from", "$16.50", "to", "$24.00")));
        CareerPages.ProfessionsBands(w, pro, new[] { SRank(w, "warehouse", 2) + ", Harbourside Freight HSF", SRank(w, "accountancy", 0) + ", Uptown Savings", SRank(w, "chef", 0) + ", Burger Barn, Broadway" },
            new[] { "$33.00", "$22.00", "$19.00" }, new[] { UiIcon.Trophy, UiIcon.Bank, UiIcon.ForkKnife },
            new[] { SCourse(w, "forklift"), SCourse(w, "customer_service"), SCourse(w, "business") }, new[] { SRank(w, "guard", 0), SRank(w, "nursing", 0), SRank(w, "it_support", 0) });
        CareerPages.ProfessionsBands(w, f.Jobs, new[] { SRank(w, "warehouse", 2) + ", Harbourside Freight HSF", SRank(w, "accountancy", 0) + ", Uptown Savings", SRank(w, "chef", 0) + ", Burger Barn, Broadway" },
            new[] { "$33.00", "$22.00", "$19.00" }, new[] { UiIcon.Trophy, UiIcon.Bank, UiIcon.ForkKnife },
            new[] { SCourse(w, "forklift"), SCourse(w, "customer_service"), SCourse(w, "business") }, new[] { SRank(w, "guard", 0), SRank(w, "nursing", 0), SRank(w, "it_support", 0) });
    }

    private static void ManagementTab(UtilitiesWords w, CareerFacts f)
    {
        WorkTab man = f.Management;
        man.Shape = WorkShape.Form;
        var form = new WorkFormModel { Lead = Sx(w, "reallife_ladder_row_manager") + ". " + Sf(w, "reallife_ladder_sub", "company", "Harbourside Freight HSF", "stores", "2") };
        var decide = new WorkFormBand { Title = w.T("reallife_work_group_decide", "To decide"), Count = "1" };
        decide.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = Sx(w, "reallife_ladder_task_cover"), Hint = "Pilar Duran, Hudson Yards depot", Value = SDay(w, 414), Icon = UiIcon.People });
        decide.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = WkAct(113, w.T("reallife_ladder_button_yes", "Agree"), UiButtonKind.Primary, UiIcon.Check, null, false, 1) });
        decide.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = WkAct(114, w.T("reallife_ladder_button_no", "Refuse"), UiButtonKind.Secondary, UiIcon.Close, null, false, 1) });
        decide.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = WkAct(115, w.T("reallife_ladder_button_up", "Pass it up"), UiButtonKind.Secondary, UiIcon.Up, null, false, 1) });
        form.Left.Add(decide);
        var stand = new WorkFormBand { Title = Sx(w, "reallife_ladder_sec_position") };
        stand.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = Sx(w, "reallife_ladder_row_reports"), Value = "Pilar Duran" });
        stand.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = Sx(w, "reallife_ladder_row_score"), Value = 78 } });
        stand.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = Sx(w, "reallife_ladder_row_staffing"), Value = 81 } });
        stand.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = Sx(w, "reallife_ladder_row_morale_avg"), Value = 74 } });
        form.Left.Add(stand);
        var next = new WorkFormBand { Title = Sx(w, "reallife_ladder_sec_next") };
        next.Rows.Add(new WorkFormRow { Field = WorkField.Check, Label = Sf(w, "reallife_ladder_need_score", "have", "78", "need", "80"), On = false });
        next.Rows.Add(new WorkFormRow { Field = WorkField.Check, Label = Sf(w, "reallife_ladder_need_stores", "have", "2", "need", "2"), On = true });
        next.Rows.Add(new WorkFormRow { Field = WorkField.Check, Label = Sf(w, "reallife_ladder_need_recommend", "boss", "Pilar Duran"), On = true });
        next.Rows.Add(new WorkFormRow { Field = WorkField.Switch, Label = w.T("reallife_ladder_aiming", "Put my name forward"), On = true, Act = WkAct(119, w.T("reallife_ladder_aiming", "Put my name forward"), UiButtonKind.Secondary, UiIcon.Check) });
        form.Left.Add(next);
        var ask = new WorkFormBand { Title = w.T("reallife_ladder_sec_requests", "Your requests") };
        ask.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = Sx(w, "reallife_work_ask_shift"), Act = WkAct(128, w.T("reallife_work_ask_shift_earlier", "Ask for it earlier"), UiButtonKind.Secondary, UiIcon.Back) });
        ask.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = WkAct(129, w.T("reallife_work_ask_shift_later", "Ask for it later"), UiButtonKind.Secondary, UiIcon.Next) });
        ask.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = Sx(w, "reallife_work_ask_equipment_label"), Act = WkAct(130, w.T("reallife_work_ask_equipment", "Ask for equipment"), UiButtonKind.Secondary, UiIcon.Wrench) });
        ask.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = Sx(w, "reallife_work_ask_insurance_label"), Act = WkAct(131, w.T("reallife_work_ask_insurance", "Ask for insurance"), UiButtonKind.Secondary, UiIcon.ShieldCheck,
            w.T("reallife_work_granted", "Already granted.")) });
        form.Right.Add(ask);
        string[] names = { "Oleg Kozlov", "Nia Brooks", "Priya Shah", "Tomas Reyes", "Ada Voss", "Leo Park" };
        int[] morale = { 76, 66, 81, 72, 88, 69 };
        var staff = new WorkFormBand { Title = w.T("reallife_work_group_staff", "Staff"), Count = w.F("reallife_work_n_people", "{n} people", "n", names.Length.ToString()) };
        for (int i = 0; i < names.Length; i++)
        {
            staff.Rows.Add(new WorkFormRow { Field = WorkField.Person, Person = new WorkPerson { Name = names[i], Role = (i < 4 ? "Hudson Yards depot" : "Pier 9 cold store") + ", $" + (17 + i % 3) + ".00",
                Right = Sf(w, "reallife_work_morale_n", "n", morale[i].ToString()), Ring = morale[i] < 70 ? UiRole.Warn : UiRole.Ok },
                Act = WkAct(132, w.T("reallife_work_raise_their_pay", "Raise their pay"), UiButtonKind.Secondary, UiIcon.TrendUp, null, false, i + 1) });
        }
        form.Right.Add(staff);
        var stores = new WorkFormBand { Title = w.T("reallife_work_group_stores", "Stores"), Count = "2" };
        stores.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = "Hudson Yards depot", Hint = Sx(w, "reallife_ladder_rota_1") + ", 8 / 10", Value = Sf(w, "reallife_work_results_n", "n", "82"), Icon = UiIcon.Store });
        stores.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = "Pier 9 cold store", Hint = Sx(w, "reallife_ladder_rota_0") + ", 5 / 6", Value = Sf(w, "reallife_work_results_n", "n", "74"), Icon = UiIcon.Store });
        form.Right.Add(stores);
        man.Form = form;
    }

    private static void OwnTab(UtilitiesWords w, CareerFacts f)
    {
        WorkTab own = f.Own;
        own.Shape = WorkShape.Form;
        var form = new WorkFormModel { Lead = w.T("reallife_own_practice_about", "Work for yourself: a practice of your profession, or a company of your own.") };
        var practice = new WorkFormBand { Title = w.T("reallife_own_practice", "Your own practice") };
        practice.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_career_col_profession", "Profession"), Value = SPro(w, "carpenter") });
        practice.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = Sx(w, "reallife_ibox_since"), Value = SDay(w, 371) });
        practice.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = Sx(w, "reallife_ibox_name"), Value = 34 } });
        practice.Foot = WkAct(134, w.T("reallife_own_button_close_practice", "Close the practice"), UiButtonKind.Destructive, UiIcon.Close);
        form.Left.Add(practice);
        var found = new WorkFormBand { Title = w.T("reallife_own_found_title", "Found a company") };
        found.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = WkAct(135, w.T("reallife_own_button_found", "Leave to found a company"), UiButtonKind.Secondary, UiIcon.Building) });
        found.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = WkAct(136, Sx(w, "reallife_own_button_found_colleagues"), UiButtonKind.Secondary, UiIcon.People,
            w.T("reallife_work_no_colleagues", "No colleagues would come with you.")) });
        form.Left.Add(found);
        var contracts = new WorkFormBand { Title = w.T("reallife_own_contracts", "Contracts"), Count = "3" };
        contracts.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = "Rose Wilder", Hint = SPro(w, "carpenter") + ", " + Sx(w, "reallife_own_step_offered"), Value = "$180", Icon = UiIcon.Wrench });
        contracts.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = Sf(w, "reallife_work_due_day", "day", SDay(w, 415)), Act = WkAct(116, w.T("reallife_own_button_take", "Take it on"), UiButtonKind.Primary, UiIcon.Check, null, false, 40) });
        contracts.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = "Hollis and Sons", Hint = SPro(w, "carpenter") + ", " + Sx(w, "reallife_own_step_taken"), Value = "$640", Icon = UiIcon.Hammer });
        contracts.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = Sf(w, "reallife_work_due_day", "day", SDay(w, 418)), Act = WkAct(117, w.T("reallife_own_button_drop", "Drop it"), UiButtonKind.Destructive, UiIcon.Close, null, false, 41) });
        contracts.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = "Ingrid Solberg", Hint = SPro(w, "carpenter") + ", " + Sx(w, "reallife_own_step_taken"), Value = "$310", Icon = UiIcon.Wrench });
        contracts.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = Sf(w, "reallife_work_due_day", "day", SDay(w, 421)), Act = WkAct(117, w.T("reallife_own_button_drop", "Drop it"), UiButtonKind.Destructive, UiIcon.Close, null, false, 42) });
        form.Right.Add(contracts);
        CareerPages.OwnBands(w, form, 5, 1280, new[] { "Rose Wilder", "Hollis and Sons", "Aldous Burke" }, new[] { "$180", "$640", "$420" },
            new[] { SDay(w, 380), SDay(w, 392), SDay(w, 404) });
        CareerPages.OwnLifeBands(w, form, "$28,400", 188, 3, new[] { UiIcon.Package, UiIcon.Coffee },
            new[] { SRank(w, "warehouse", 1) + ", Harbourside Freight HSF", SRank(w, "bar", 0) + ", The Anchor" },
            new[] { SDay(w, 301) + " " + Sx(w, "reallife_work_cv_to") + " " + SDay(w, 388), SDay(w, 188) + " " + Sx(w, "reallife_work_cv_to") + " " + SDay(w, 290) }, new[] { "$16,200", "$9,100" });
        own.Form = form;
    }

    private static void CvTab(UtilitiesWords w, CareerFacts f)
    {
        WorkTab cv = f.Cv;
        // the timeline: a courier at Quick Parcels day 18 to 80 (a fair reference), a forklift operator at Harbourside from day 82, the team leader from day 190
        var spells = new List<CareerSpell>
        {
            new CareerSpell { Title = SRank(w, "warehouse", 1), Employer = "Harbourside Freight HSF", Dates = SDay(w, 190) + " " + w.T("reallife_work_cv_to_now", "to now"), Note = Sx(w, "reallife_work_cv_now"), Icon = UiIcon.Trophy, Current = true },
            new CareerSpell { Title = SRank(w, "warehouse", 0), Employer = "Harbourside Freight HSF", Dates = SDay(w, 82) + " " + w.T("reallife_work_cv_to", "to") + " " + SDay(w, 190),
                Note = Sf(w, "reallife_career_row_spell", "leaving", Sx(w, "reallife_work_leaving_none"), "reference", Sx(w, "reallife_work_reference_3")), Icon = UiIcon.Package, Tone = UiRole.Ok },
            new CareerSpell { Title = SRank(w, "sales", 0), Employer = "Quick Parcels", Dates = SDay(w, 18) + " " + w.T("reallife_work_cv_to", "to") + " " + SDay(w, 80),
                Note = Sf(w, "reallife_career_row_spell", "leaving", Sx(w, "reallife_work_leaving_resigned"), "reference", Sx(w, "reallife_work_reference_2")), Icon = UiIcon.Truck, Tone = UiRole.Warn }
        };
        var quals = new List<CareerSpell>
        {
            new CareerSpell { Title = SCourse(w, "forklift"), Employer = SDay(w, 150), Dates = Sx(w, "reallife_study_grade_3"), Icon = UiIcon.Cap, Tone = UiRole.Ok },
            new CareerSpell { Title = SCourse(w, "customer_service"), Employer = SDay(w, 205), Dates = Sx(w, "reallife_study_grade_2"), Icon = UiIcon.Cap, Tone = UiRole.Ok }
        };
        CareerPages.CvPage(w, cv, "Jordan Hayes", SRank(w, "warehouse", 1), "Harbourside Freight HSF", spells, quals, 3, 13, "$38,420", 2, "$4,120", 40, 55,
            new[] { SSkill(w, "handling"), SSkill(w, "first_aid"), SSkill(w, "driving") }, new[] { 74, 55, 66 }, w.T("reallife_work_why_retire", "You can retire from age 55."), false, f.Job,
            new[] { UiIcon.Coffee, UiIcon.Heart, UiIcon.ForkKnife }, new[] { SCourse(w, "barista"), SCourse(w, "care_worker"), SCourse(w, "chef") },
            new[] { w.T("reallife_work_under_way", "Under way"), w.T("reallife_work_under_way", "Under way"), w.T("reallife_work_not_started", "Not started") },
            new[] { w.F("reallife_work_attended_pct", "{n}% attended", "n", "100"), w.F("reallife_work_attended_pct", "{n}% attended", "n", "91"), w.T("reallife_work_not_started", "Not started") });
    }
}
