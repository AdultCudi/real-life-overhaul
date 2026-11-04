using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    public static WorkScreen RolesScreenOf(UiWords t, string state)
    {
        UtilitiesWords w = UtilitiesWordsOf(t);
        UiNavEntry[] nav = RolesNav(w);
        var screen = new WorkScreen(new SampleWork("open_roles", w.T("reallife_roles_app", "Open Roles"), UiIcon.People, true, nav, t, v => WorkTabPages.Build(RolesAppOf(w, state, v), v, w)));
        screen.View.Tab = WkTabOf("roles", state);
        if (state == "person" || state == "person_contract")
        {
            screen.View.Sub = 1;
            screen.View.PageTab = state == "person" ? 0 : 2;
        }
        else if (state == "new_ad")
        {
            screen.View.Sub = 2;
        }
        return screen;
    }

    private static UiNavEntry[] RolesNav(UtilitiesWords w) => new[] { WkNav(UiIcon.ClipboardText, w.T("reallife_roles_tab_jobs", "Jobs"), "5"),
        WkNav(UiIcon.People, w.T("reallife_roles_tab_applicants", "Applicants"), "30"), WkNav(UiIcon.Globe, w.T("reallife_roles_tab_market", "Market")),
        WkNav(UiIcon.Search, w.T("reallife_roles_tab_search", "Search")), WkNav(UiIcon.Person, w.T("reallife_roles_tab_team", "Team"), "8") };

    private static WorkApp RolesAppOf(UtilitiesWords w, string state, WorkView v)
    {
        var app = new WorkApp { Ready = true, Nav = RolesNav(w) };
        WorkTab team = RolesTeam(w);
        WorkTab jobs = RolesJobs(w);
        if (v.Sub == 1)
        {
            team = RolesPerson(w, v);
        }
        else if (v.Sub == 2)
        {
            jobs = RolesNewAd(w, v);
        }
        app.Tabs = new[] { jobs, RolesApplicants(w, state, v), RolesMarket(w), RolesSearch(w, v), team };
        if (state == "applicants_none")
        {
            app.Nav[1].Count = null;
        }
        return app;
    }

    private static WorkTab RolesJobs(UtilitiesWords w)
    {
        var t = new WorkTab { Shape = WorkShape.Table };
        t.Columns = new[] { WkCol(w.T("reallife_work_col_role", "Role"), UiCellKind.Tile), WkCol(w.T("reallife_work_col_posts", "Posts to fill"), UiCellKind.Text, 112),
            WkCol(w.T("reallife_work_col_applicants", "Applicants"), UiCellKind.Figure, 96, true), WkCol(w.T("reallife_work_col_pay_up_to", "Up to"), UiCellKind.Money, 88),
            WkCol(w.T("reallife_work_col_state", "State"), UiCellKind.Chip, 136) };
        t.EmptyTitle = w.T("reallife_work_no_ads_title", "No role is open");
        t.EmptyBody = w.T("reallife_work_no_ads", "Post an ad and applicants arrive over a few days.");
        t.Acts.Add(WkAct(105, w.T("reallife_work_new_ad", "New ad"), UiButtonKind.Primary, UiIcon.Plus, null, true));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_open_posts", "Open posts"), "8"));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_waiting", "Applicants waiting"), "30"));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_ads_cost", "Ads this month"), "$1,480"));
        string[][] ads =
        {
            new[] { "Game designer", "Pioneer Game Studio", "2 of 2", "10", "$42", "Open", "p" }, new[] { "Support agent", "Pioneer Game Studio", "1 of 1", "8", "$24", "Open", "m" },
            new[] { "Data centre technician", "Hudson Data Hall", "3 of 4", "4", "$31", "Open", "d" }, new[] { "Network engineer", "Midtown Fibre Co", "1 of 1", "5", "$44", "Closes in 3 days", "n" },
            new[] { "Tester", "Pioneer Game Studio", "1 of 1", "3", "$26", "Open", "s" }
        };
        UiIcon[] icons = { UiIcon.PaintBrush, UiIcon.Headset, UiIcon.HardDrives, UiIcon.Globe, UiIcon.Bug };
        for (int i = 0; i < ads.Length; i++)
        {
            var r = new WorkRow { Id = i + 1, Icon = icons[i], Name = ads[i][0], Sub = ads[i][1], Cells = new[] { null, ads[i][2], ads[i][3],
                ads[i][4], ads[i][5] }, Values = new[] { 0, 0, int.Parse(ads[i][3]) * 8, 0, 0 },
                Status = ads[i][5], Tone = i == 3 ? UiRole.Warn : UiRole.Ok, Chip = ads[i][5], ChipTone = i == 3 ? UiRole.Warn : UiRole.Ok, Group = i };
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_pay_up_to", "Pay up to"), ads[i][4] + "/h"));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_posts", "Posts to fill"), ads[i][2]));
            var facts = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_the_ad", "The ad") };
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_ad_skill", "Minimum skill"), "60"));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_ad_arrangement", "Arrangement"), w.T("reallife_work_arr_onsite", "On site")));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_ad_closes", "Closes"), i == 3 ? "day 408" : "day 416"));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_ad_boosted", "Boosted"), i == 0 ? w.T("reallife_work_yes", "Yes") : w.T("reallife_work_no", "No")));
            r.Sections.Add(facts);
            var came = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_how_it_goes", "How it is going") };
            int[] cameBy = { 10, 8, 4, 5, 3 };
            came.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_applicants", "Applicants"), cameBy[i].ToString()));
            came.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_shortlisted_n", "On your shortlist"), (cameBy[i] / 3).ToString()));
            came.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_offers_out", "Offers out"), (cameBy[i] / 5).ToString()));
            r.Sections.Add(came);
            if (i == 0)
            {
                var hired = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_hired_month", "Hired this month"), Note = "2" };
                hired.People.Add(new WorkPerson { Name = "Kenji Mori", Role = "Producer, day 389", Ring = UiRole.Ok });
                hired.People.Add(new WorkPerson { Name = "Jonah Reyes", Role = "QA lead, day 394", Ring = UiRole.Ok });
                r.Sections.Add(hired);
            }
            r.Acts.Add(WkAct(WorkKinds.GoTab, w.T("reallife_hire_button_applicants", "Applicants"), UiButtonKind.Secondary, UiIcon.People, null, true, 1));
            r.Acts.Add(WkAct(104, w.T("reallife_work_close_ad", "Close the ad"), UiButtonKind.Destructive, UiIcon.Close, null, false, r.Id));
            t.Rows.Add(r);
        }
        var rival = new WorkRow { Id = 90, Icon = UiIcon.Flag, Name = "Game designer", Sub = "Guo Interactive", Cells = new[] { null, "-", "-", "$44",
            "Rival ad" }, Status = "Rival ad", Tone = UiRole.Muted, Chip = "Rival ad", ChipTone = UiRole.Muted, Group = 9 };
        rival.Sentence = "A rival firm hires the same line and pays $44 an hour, 4% over the going rate. Their applicants may take an offer before yours.";
        t.Rows.Add(rival);
        return t;
    }

    private static WorkTab RolesNewAd(UtilitiesWords w, WorkView v)
    {
        var t = new WorkTab { Shape = WorkShape.Form, Sub = true };
        string[] roles = { "Game designer", "Support agent", "Data centre technician", "Network engineer", "Tester" };
        string[] places = { "Pioneer Game Studio", "Hudson Data Hall", "Midtown Fibre Co" };
        string[] levels = { "Junior", "Mid", "Senior" };
        string[] works = { "On site", "Hybrid", "Remote" };
        int role = v.Get(0, 0), level = v.Get(1, 1), place = v.Get(2, 0), most = v.Get(3, 42), least = v.Get(4, 34), skill = v.Get(5, 60), work = v.Get(6, 0), days = v.Get(7, 14), posts = v.Get(8, 2);
        t.Amounts.Add(new WorkAmount { Id = 0, Value = role, Min = 0, Max = roles.Length - 1, Words = roles, Figure = roles[role] });
        t.Amounts.Add(new WorkAmount { Id = 1, Value = level, Min = 0, Max = 2, Words = levels, Figure = levels[level] });
        t.Amounts.Add(new WorkAmount { Id = 2, Value = place, Min = 0, Max = places.Length - 1, Words = places, Figure = places[place] });
        t.Amounts.Add(new WorkAmount { Id = 3, Value = most, Min = 10, Max = 120, Figure = "$" + most + " an hour" });
        t.Amounts.Add(new WorkAmount { Id = 4, Value = least, Min = 10, Max = 120, Figure = "$" + least + " an hour" });
        t.Amounts.Add(new WorkAmount { Id = 5, Value = skill, Min = 0, Max = 100, Step = 5, Figure = skill.ToString() });
        t.Amounts.Add(new WorkAmount { Id = 6, Value = work, Min = 0, Max = 2, Words = works, Figure = works[work] });
        t.Amounts.Add(new WorkAmount { Id = 7, Value = days, Min = 3, Max = 30, Figure = days + " days" });
        t.Amounts.Add(new WorkAmount { Id = 8, Value = posts, Min = 1, Max = 6, Figure = posts.ToString() });
        var form = new WorkFormModel { Lead = w.T("reallife_work_new_ad_lead", "An ad runs for the days you set and brings applicants over a few days. It is paid when it is posted.") };
        var role1 = new WorkFormBand { Title = w.T("reallife_work_the_role", "The role") };
        role1.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_col_role", "Role"), Stepper = 0 });
        role1.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_col_level", "Level"), Stepper = 1 });
        role1.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_ad_place", "At"), Stepper = 2 });
        role1.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_ad_posts", "Posts to fill"), Stepper = 8 });
        role1.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_ad_arrangement", "Arrangement"), Stepper = 6 });
        form.Left.Add(role1);
        var terms = new WorkFormBand { Title = w.T("reallife_work_the_terms", "Pay and terms") };
        terms.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_ad_pay_most", "Pay up to"), Stepper = 3 });
        terms.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_ad_pay_least", "Pay from"), Stepper = 4 });
        terms.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_ad_skill", "Minimum skill"), Stepper = 5 });
        terms.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_ad_days", "The ad runs"), Stepper = 7 });
        terms.Rows.Add(new WorkFormRow { Field = WorkField.Switch, Label = w.T("reallife_work_ad_boost", "Boost the ad"), On = false, Act = WkAct(106,
            w.T("reallife_work_ad_boost", "Boost the ad"), UiButtonKind.Secondary, UiIcon.Rocket) });
        form.Right.Add(terms);
        var sum = new WorkFormBand { Title = w.T("reallife_work_ad_summary", "What it comes to") };
        sum.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_ad_going", "Going rate"), Value = "$34.00" });
        sum.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_ad_pay_least", "Pay from"), Value = "$" + least + ".00" });
        sum.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_ad_cost", "It costs"), Value = "$" + (days * 40) });
        form.Right.Add(sum);
        t.Form = form;
        t.Acts.Add(WkAct(WorkKinds.Back, w.T("reallife_work_cancel", "Cancel"), UiButtonKind.Secondary, UiIcon.Back, null, true));
        t.Acts.Add(WkAct(107, w.T("reallife_hire_button_post", "Post the ad"), UiButtonKind.Primary, UiIcon.Check));
        return t;
    }

    private static WorkTab RolesApplicants(UtilitiesWords w, string state, WorkView v)
    {
        var t = new WorkTab { Shape = WorkShape.Table, Search = w.T("reallife_work_search_people", "Search people") };
        t.Columns = new[] { WkCol(w.T("reallife_work_col_applicant", "Applicant"), UiCellKind.Person), WkCol(w.T("reallife_work_skill_craft", "Craft"), UiCellKind.Meter, 120),
            WkCol(w.T("reallife_work_skill_ideas", "Ideas"), UiCellKind.Meter, 120), WkCol(w.T("reallife_work_skill_team", "Team"), UiCellKind.Meter,
                120), WkCol(w.T("reallife_work_col_asks", "Asks"), UiCellKind.Money, 72),
            WkCol(w.T("reallife_work_col_state", "State"), UiCellKind.Chip, 152) };
        t.EmptyTitle = w.T("reallife_work_no_applicants_title", "No applicants yet");
        t.EmptyBody = w.T("reallife_work_no_applicants", "Applicants arrive over a few days and other firms hire too. A boosted ad brings more.");
        t.EmptyIcon = UiIcon.People;
        var aside = new WorkAside { Title = w.T("reallife_work_open_roles", "Open roles"), Count = "5", ListTitle = w.T("reallife_work_hired_month", "Hired this month"), ListCount = "3",
            Foot = w.T("reallife_work_applicants_foot", "Applicants arrive over a few days and take other offers if you wait.") };
        string[][] roles = { new[] { "Game designer", "Pioneer Game Studio", "10" }, new[] { "Support agent", "Pioneer Game Studio", "8" }, new[] { "Data centre technician", "Hudson Data Hall", "4" },
            new[] { "Network engineer", "Midtown Fibre Co", "5" }, new[] { "Tester", "Pioneer Game Studio", "3" } };
        UiIcon[] icons = { UiIcon.PaintBrush, UiIcon.Headset, UiIcon.HardDrives, UiIcon.Globe, UiIcon.Bug };
        for (int i = 0; i < roles.Length; i++)
        {
            aside.Rows.Add(new WorkRow { Id = i + 1, Icon = icons[i], Name = roles[i][0], Sub = roles[i][1], Status = state == "applicants_none" ? "0" : roles[i][2] });
        }
        aside.List.Add(new WorkPerson { Name = "Kenji Mori", Role = "Producer, day 389", Ring = UiRole.Ok });
        aside.List.Add(new WorkPerson { Name = "Jonah Reyes", Role = "QA lead, day 394", Ring = UiRole.Ok });
        aside.List.Add(new WorkPerson { Name = "Aiko Kato", Role = "Cooling technician, day 398", Ring = UiRole.Ok });
        t.Aside = aside;
        if (state == "applicants_none")
        {
            return t;
        }
        string[][] people =
        {
            new[] { "Nora Whitfield", "Tidewater Interactive", "82", "88", "70", "$41", "Competing offer" }, new[] { "Kofi Mensah", "Pixel Pit", "76", "71", "84", "$36", "Available now" },
            new[] { "Camille Duval", "Copperkettle Games", "79", "69", "77", "$38", "Available now" }, new[] { "Linnea Berg", "Fernwood Games", "71", "80", "66", "$34", "Notice, 2 weeks" },
            new[] { "Mateo Silva", "Gridline Studio", "64", "66", "81", "$30", "Notice, 1 week" }, new[] { "Ravi Anand", "Self taught", "58", "74", "72", "$26", "Available now" },
            new[] { "Zoe Hartley", "Student at Hudson Tech", "49", "77", "79", "$22", "Available now" }, new[] { "Jonas Weber", "Pixel Harbour Games", "68", "62", "74", "$31", "Notice, 3 days" },
            new[] { "Imani Okoro", "Hollowpine Interactive", "73", "59", "68", "$33", "Available now" }, new[] { "Elif Demir", "Self taught", "52", "81", "70", "$24", "Available now" }
        };
        for (int i = 0; i < people.Length; i++)
        {
            string[] p = people[i];
            bool competing = i == 0;
            UiRole tone = competing ? UiRole.Warn : p[6].StartsWith("Notice") ? UiRole.Muted : UiRole.Ok;
            var r = new WorkRow { Id = i + 1, Person = p[0], Name = p[0], Sub = "From " + p[1], Ring = UiRole.Ok, Group = 1, Cells = new[] { null, p[2], p[3], p[4], p[5], StateWord(w, p[6]) },
                Values = new[] { 0, int.Parse(p[2]), int.Parse(p[3]), int.Parse(p[4]), 0, 0 }, Status = StateWord(w, p[6]), Tone = tone,
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone } };
            r.Open = WkAct(110, string.Empty, UiButtonKind.Secondary, UiIcon.Person, null, true, r.Id);
            r.Sub = "29, game designer at " + p[1] + " for 4 years";
            r.Sub = "From " + p[1];
            r.Title = p[0];
            r.Sentence = "29, game designer at " + p[1] + " for 4 years";
            var attrs = new WorkSection { Part = WorkPart.Bars, Title = w.T("reallife_work_attributes", "Attributes") };
            attrs.Bars.Add(new WorkBar { Label = w.T("reallife_work_skill_craft", "Craft"), Value = int.Parse(p[2]) });
            attrs.Bars.Add(new WorkBar { Label = w.T("reallife_work_skill_ideas", "Ideas"), Value = int.Parse(p[3]) });
            attrs.Bars.Add(new WorkBar { Label = w.T("reallife_work_skill_team", "Team"), Value = int.Parse(p[4]) });
            attrs.Bars.Add(new WorkBar { Label = w.T("reallife_work_skill_reliability", "Reliability"), Value = 76 });
            r.Sections.Add(attrs);
            if (i == 0)
            {
                var work = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_so_far_title", "Work so far") };
                work.Pairs.Add(new KeyValuePair<string, string>(w.F("reallife_work_job_line", "{role}, {firm}", "role",
                    w.T("reallife_work_role_designer", "Game designer"), "firm", "Tidewater Interactive"), w.F("reallife_work_years_now", "{n} years, now", "n", "4")));
                work.Pairs.Add(new KeyValuePair<string, string>(w.F("reallife_work_job_line", "{role}, {firm}", "role",
                    w.T("reallife_work_role_junior_designer", "Junior designer"), "firm", "Pixel Harbour Games"), w.F("reallife_work_n_years", "{n} years", "n", "2")));
                r.Sections.Add(work);
            }
            var fit = new WorkSection { Part = WorkPart.Checks, Title = w.T("reallife_work_fit_for_role", "Fit for the role") };
            fit.Checks.Add(new WorkNeed { Words = w.F("reallife_work_fit_craft", "Craft above your team's average of {n}", "n", "71"), Met = int.Parse(p[2]) > 71 });
            fit.Checks.Add(new WorkNeed { Words = w.F("reallife_work_fit_pay", "Asks less than the {pay} you set", "pay", "$42"), Met = true });
            r.Sections.Add(fit);
            if (competing)
            {
                var offer = new WorkSection { Part = WorkPart.Note, Lead = w.T("reallife_work_competing_offer_lead", "Competing offer"),
                    Text = w.F("reallife_work_rival_offer_line",
                    "{rival} offered {pay} an hour. They answer by {day}. Offer at least as much to hire them.", "rival", "Guo Interactive", "pay", "$44", "day", "day 405"), NoteIcon = UiIcon.Flag };
                r.Sections.Add(offer);
            }
            int ask = int.Parse(p[5].Substring(1));
            int offerValue = v.Get(0, competing ? 44 : ask);
            if (t.Amounts.Count == 0)
            {
                t.Amounts.Add(new WorkAmount { Id = 0, Value = offerValue, Min = 10, Max = 120, Figure = w.F("reallife_work_offer_figure", "{pay} an hour", "pay", "$" + offerValue) });
            }
            r.Sections.Add(new WorkSection { Part = WorkPart.Amount, Title = w.T("reallife_work_your_offer", "Your offer"), Stepper = 0 });
            r.Acts.Add(WkAct(101, w.F("reallife_work_hire_at", "Hire at {pay} an hour", "pay", "$" + offerValue), UiButtonKind.Primary, UiIcon.Check, null, false, r.Id));
            r.Acts.Add(WkAct(100, w.T("reallife_hire_button_shortlist", "Shortlist"), UiButtonKind.Secondary, UiIcon.Star, null, false, r.Id));
            t.Rows.Add(r);
        }
        return t;
    }

    private static string StateWord(UtilitiesWords w, string state)
    {
        if (state == "Competing offer")
        {
            return w.T("reallife_work_competing_offer", "Competing offer");
        }
        if (state == "Available now")
        {
            return w.T("reallife_work_available_now", "Available now");
        }
        if (state.StartsWith("Notice, "))
        {
            string n = state.Substring(8);
            return w.F("reallife_work_notice_of", "Notice, {n}", "n", n.Replace("weeks", w.T("reallife_work_weeks_word", "weeks")).Replace("week",
                w.T("reallife_work_week_word", "week")).Replace("days", w.T("reallife_work_days_word", "days")));
        }
        return state;
    }

    private static WorkTab RolesMarket(UtilitiesWords w)
    {
        var t = new WorkTab { Shape = WorkShape.Table, Search = w.T("reallife_work_search_people", "Search people"), AllWord = w.T("reallife_work_filter_all_word", "All"),
            Chips = new[] { w.T("reallife_work_family_creative", "Creative"), w.T("reallife_work_family_technical", "Technical"), w.T("reallife_work_family_support", "Support") } };
        t.Columns = new[] { WkCol(w.T("reallife_work_col_seeker", "Looking for work"), UiCellKind.Person), WkCol(w.T("reallife_work_col_level", "Level"), UiCellKind.Text, 96),
            WkCol(w.T("reallife_work_skill_craft", "Craft"), UiCellKind.Meter, 120), WkCol(w.T("reallife_work_col_asks", "Asks"), UiCellKind.Money,
                72), WkCol(w.T("reallife_work_col_state", "State"), UiCellKind.Chip, 152) };
        t.EmptyTitle = w.T("reallife_work_market_empty_title", "Nobody is on the market");
        t.EmptyBody = w.T("reallife_work_market_lead", "Professionals looking for work show here by line. Post an ad and the ones who fit apply.");
        string[][] seekers =
        {
            new[] { "Dana Whelan", "Game design", "Mid", "74", "$34", "Looking", "0" }, new[] { "Pavel Novak", "Game design", "Senior", "86", "$48", "In work, 2 weeks", "0" },
            new[] { "Mei Tanaka", "Support", "Junior", "55", "$21", "Looking", "2" }, new[] { "Owen Fraser", "Data centre", "Mid", "69", "$30",
                "Looking", "1" }, new[] { "Sade Bello", "Network", "Senior", "81", "$46", "In work, 4 weeks", "1" },
            new[] { "Luca Ferri", "Game design", "Junior", "48", "$22", "Looking", "0" }, new[] { "Hanna Voss", "Support", "Mid", "66", "$26",
                "Looking", "2" }, new[] { "Idris Cole", "Testing", "Mid", "63", "$27", "Looking", "2" }
        };
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_looking", "Looking"), seekers.Length.ToString()));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_rival_ads", "Rival ads"), "9"));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_unemployment", "Unemployment"), "6.2%"));
        for (int i = 0; i < seekers.Length; i++)
        {
            string[] s = seekers[i];
            var r = new WorkRow { Id = i + 1, Person = s[0], Name = s[0], Sub = s[1], Ring = UiRole.Ok, Group = int.Parse(s[6]), Cells = new[] { null,
                s[2], s[3], s[4], s[5] }, Values = new[] { 0, 0, int.Parse(s[3]), 0, 0 },
                Status = s[5], Tone = s[5].StartsWith("In work") ? UiRole.Muted : UiRole.Ok };
            r.Sentence = s[1] + ", " + s[2].ToLowerInvariant() + ". Asks " + s[4] + " an hour.";
            var attrs = new WorkSection { Part = WorkPart.Bars, Title = w.T("reallife_work_attributes", "Attributes") };
            attrs.Bars.Add(new WorkBar { Label = w.T("reallife_work_skill_craft", "Craft"), Value = int.Parse(s[3]) });
            attrs.Bars.Add(new WorkBar { Label = w.T("reallife_work_skill_ideas", "Ideas"), Value = 60 + i * 3 });
            var facts = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_the_seeker", "The seeker") };
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_asks", "Asks"), s[4] + "/h"));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_going_rate", "Going rate"), "$" + (int.Parse(s[4].Substring(1)) - 2) + "/h"));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_notice", "Notice"),
                s[5].StartsWith("In work") ? w.F("reallife_work_n_days", "{n} days", "n", (14 + i).ToString()) : w.T("reallife_work_none_word", "None")));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_rival_ads", "Rival ads"), (i % 3 + 1).ToString()));
            r.Sections.Add(attrs);
            r.Sections.Add(facts);
            r.Acts.Add(WkAct(WorkKinds.GoTab, w.T("reallife_work_approach", "Approach"), UiButtonKind.Primary, UiIcon.Handshake, null, true, 0));
            t.Rows.Add(r);
        }
        return t;
    }

    private static WorkTab RolesSearch(UtilitiesWords w, WorkView v)
    {
        var t = new WorkTab { Shape = WorkShape.Form };
        string[] roles = { "Game designer", "Network engineer", "Data centre technician" };
        string[] levels = { "Mid", "Senior", "Lead" };
        int role = v.Get(0, 0), level = v.Get(1, 1), crew = v.Get(2, 4);
        t.Amounts.Add(new WorkAmount { Id = 0, Value = role, Min = 0, Max = roles.Length - 1, Words = roles, Figure = roles[role] });
        t.Amounts.Add(new WorkAmount { Id = 1, Value = level, Min = 0, Max = 2, Words = levels, Figure = levels[level] });
        t.Amounts.Add(new WorkAmount { Id = 2, Value = crew, Min = 1, Max = 12, Figure = crew.ToString() });
        var form = new WorkFormModel { Lead = w.T("reallife_work_search_lead",
            "A search firm finds senior people, and a crew search finds technicians for a data centre. Its fee is a share of a year's pay.") };
        var brief = new WorkFormBand { Title = w.T("reallife_work_brief_title", "Brief a search firm") };
        brief.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_col_role", "Role"), Stepper = 0 });
        brief.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_col_level", "Level"), Stepper = 1 });
        brief.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_col_fee", "Fee"), Value = "$18,400, a third up front" });
        brief.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_col_due", "Delivers"), Value = "In 14 days" });
        brief.Foot = WkAct(120, w.T("reallife_hire_button_brief", "Brief the firm"), UiButtonKind.Primary, UiIcon.Search);
        form.Left.Add(brief);
        var crewBand = new WorkFormBand { Title = w.T("reallife_work_crew_title", "Data centre crew") };
        crewBand.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_crew_technicians", "Technicians"), Stepper = 2 });
        crewBand.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_col_fee", "Fee"), Value = "$22,000, paid as each starts" });
        crewBand.Foot = WkAct(121, w.T("reallife_hire_button_brief_crew", "Brief a crew search"), UiButtonKind.Secondary, UiIcon.HardDrives);
        form.Left.Add(crewBand);
        var running = new WorkFormBand { Title = w.T("reallife_work_running_searches", "Searches running"), Count = "1" };
        running.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = "Network engineer, senior", Hint = "Fee $16,800, delivers day 416", Value = "In 9 days", Icon = UiIcon.Search });
        form.Right.Add(running);
        var offices = new WorkFormBand { Title = w.T("reallife_work_office_campaigns", "Recruitment office"), Count = "1" };
        offices.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = "Cooling technicians", Hint = "Hudson Data Hall, 3 of 5 delivered", Value = "Day 420", Icon = UiIcon.Megaphone });
        form.Right.Add(offices);
        t.Form = form;
        return t;
    }

    private static WorkTab RolesTeam(UtilitiesWords w)
    {
        var t = new WorkTab { Shape = WorkShape.Table, Search = w.T("reallife_work_search_people", "Search people") };
        t.Columns = new[] { WkCol(w.T("reallife_work_col_name", "Name"), UiCellKind.Person), WkCol(w.T("reallife_work_col_role", "Role"),
            UiCellKind.Text, 168), WkCol(w.T("reallife_work_skill_craft", "Craft"), UiCellKind.Meter, 120),
            WkCol(w.T("reallife_work_col_pay", "Pay"), UiCellKind.Money, 80), WkCol(w.T("reallife_work_col_morale", "Morale"), UiCellKind.Meter, 112) };
        t.EmptyTitle = w.T("reallife_work_no_team_title", "Nobody is on your payroll");
        t.EmptyBody = w.T("reallife_work_no_team", "Hire from the applicants and they join here, each with a page for their file, career and contract.");
        string[][] team =
        {
            new[] { "Kenji Mori", "Producer, senior", "84", "$38", "82", "Pioneer Game Studio" }, new[] { "Jonah Reyes", "QA lead, mid", "77", "$32", "71", "Pioneer Game Studio" },
            new[] { "Aiko Kato", "Cooling technician, mid", "70", "$29", "88", "Hudson Data Hall" }, new[] { "Rhys Evans", "Network engineer, mid", "74", "$34", "64", "Midtown Fibre Co" },
            new[] { "Mira Solberg", "Game designer, mid", "72", "$31", "79", "Pioneer Game Studio" }, new[] { "Tomas Ruiz", "Support agent, junior", "58", "$22", "85", "Pioneer Game Studio" },
            new[] { "Priya Nair", "Tester, mid", "66", "$27", "73", "Pioneer Game Studio" }, new[] { "Gabe Osei", "Data centre technician, junior", "55", "$24", "90", "Hudson Data Hall" }
        };
        int[] hiredOn = { 389, 394, 398, 392, 385, 396, 400, 391 };
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_payroll", "Payroll an hour"), "$236"));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_morale", "Average morale"), "79"));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_leaving", "Leaving"), "0"));
        for (int i = 0; i < team.Length; i++)
        {
            string[] p = team[i];
            var r = new WorkRow { Id = i + 1, Person = p[0], Name = p[0], Sub = p[5], Ring = UiRole.Ok, Cells = new[] { null, p[1], p[2], p[3],
                p[4] }, Values = new[] { 0, 0, int.Parse(p[2]), 0, int.Parse(p[4]) } };
            r.Open = WkAct(111, string.Empty, UiButtonKind.Secondary, UiIcon.Person, null, true, r.Id);
            r.Sentence = p[1] + ", " + p[5];
            var file = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_person_file", "File") };
            file.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_hired_on", "Joined"), "day " + hiredOn[i]));
            file.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_pay", "Pay"), p[3] + "/h"));
            file.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_going_pay", "Going pay"), "$" + (int.Parse(p[3].Substring(1)) + 2) + "/h"));
            file.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_last_review", "Last review"), "4 / 5, day " + (hiredOn[i] + 21)));
            r.Sections.Add(file);
            var attrs = new WorkSection { Part = WorkPart.Bars, Title = w.T("reallife_work_attributes", "Attributes") };
            attrs.Bars.Add(new WorkBar { Label = w.T("reallife_work_skill_craft", "Craft"), Value = int.Parse(p[2]) });
            attrs.Bars.Add(new WorkBar { Label = w.T("reallife_work_col_morale", "Morale"), Value = int.Parse(p[4]) });
            attrs.Bars.Add(new WorkBar { Label = w.T("reallife_work_burnout", "Burnout"), Value = 100 - int.Parse(p[4]) });
            r.Sections.Add(attrs);
            r.Acts.Add(WkAct(111, w.T("reallife_work_open_page", "Open their page"), UiButtonKind.Primary, UiIcon.Person, null, true, r.Id));
            t.Rows.Add(r);
        }
        return t;
    }

    private static WorkTab RolesPerson(UtilitiesWords w, WorkView v)
    {
        var t = new WorkTab { Shape = WorkShape.Form, Sub = true, PersonTabs = new[] { w.T("reallife_work_person_file", "File"),
            w.T("reallife_work_person_career", "Career"), w.T("reallife_work_person_contract", "Contract") } };
        var form = new WorkFormModel { Head = new UiProfileModel { Name = "Kenji Mori", Hue = UiColour.HueOf("Kenji Mori"), Ring = UiRole.Ok, Role = "Producer, senior", RoleIcon = UiIcon.Briefcase,
            Employer = "Pioneer Game Studio", EmployerIcon = UiIcon.Store, Status = "Settled", StatusTone = UiRole.Ok, FactLabels = new[] { "Pay",
                "Hired", "Notice" }, FactValues = new[] { "$38 an hour", "Day 389", "30 days" } } };
        t.Acts.Add(WkAct(WorkKinds.Back, w.T("reallife_work_back_to_team", "Back to the team"), UiButtonKind.Secondary, UiIcon.Back, null, true));
        if (v.PageTab == 0)
        {
            var attrs = new WorkFormBand { Title = w.T("reallife_work_attributes", "Attributes") };
            string[] l = { "Craft", "Creativity", "Teamwork", "Reliability", "Leadership" };
            int[] val = { 84, 79, 82, 88, 61 };
            for (int i = 0; i < l.Length; i++)
            {
                attrs.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = l[i], Value = val[i] } });
            }
            form.Left.Add(attrs);
            var work = new WorkFormBand { Title = w.T("reallife_work_so_far_title", "Work so far") };
            work.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = "Producer, Lantern Bay Games", Value = "5 years" });
            work.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = "Associate producer, Pixel Pit", Value = "3 years" });
            form.Right.Add(work);
            var wants = new WorkFormBand { Title = w.T("reallife_work_wants", "What they want") };
            wants.Rows.Add(new WorkFormRow { Field = WorkField.Check, Label = "A raise at the next review", On = false });
            wants.Rows.Add(new WorkFormRow { Field = WorkField.Check, Label = "Hours kept to the contract", On = true });
            form.Right.Add(wants);
        }
        else if (v.PageTab == 1)
        {
            var career = new WorkFormBand { Title = w.T("reallife_work_person_career", "Career") };
            career.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = "Joined", Value = "Day 389" });
            career.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = "Last review", Value = "4 of 5, day 410" });
            career.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = "Next review", Value = "Day 440" });
            career.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = "Raised", Value = "Day 410, 5%" });
            form.Left.Add(career);
        }
        else
        {
            int pay = v.Get(0, 38);
            t.Amounts.Add(new WorkAmount { Id = 0, Value = pay, Min = 10, Max = 120, Figure = "$" + pay + " an hour" });
            var contract = new WorkFormBand { Title = w.T("reallife_work_person_contract", "Contract") };
            contract.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_col_pay", "Pay"), Stepper = 0 });
            contract.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = "Arrangement", Value = "On site" });
            contract.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = "Notice", Value = "30 days" });
            contract.Rows.Add(new WorkFormRow { Field = WorkField.Input, Label = w.T("reallife_work_rename", "Their name on your books"), Value = v.Text, Hint = "Kenji Mori", Field2 = 0 });
            contract.Foot = WkAct(132, w.T("reallife_work_raise_pay", "Raise their pay"), UiButtonKind.Primary, UiIcon.TrendUp);
            form.Left.Add(contract);
            var leave = new WorkFormBand { Title = w.T("reallife_work_leaving", "Letting them go") };
            leave.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = w.T("reallife_work_buyout_label", "Buy out their notice"),
                Act = WkAct(133, w.T("reallife_hire_button_buyout", "Buy out their notice"), UiButtonKind.Secondary, UiIcon.Coin) });
            leave.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = w.T("reallife_work_release_label", "Release them"), Act = WkAct(134,
                w.T("reallife_work_release", "Release"), UiButtonKind.Destructive, UiIcon.SignOut) });
            form.Right.Add(leave);
        }
        t.Form = form;
        return t;
    }
}
