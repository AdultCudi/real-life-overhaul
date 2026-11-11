using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    public static WorkScreen RolesScreenOf(UiWords t, string state)
    {
        UtilitiesWords w = UtilitiesWordsOf(t);
        UiNavEntry[] nav = RolesNav(w, state);
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

    private static UiNavEntry[] RolesNav(UtilitiesWords w, string state) => new[] { WkNav(UiIcon.ClipboardText, w.T("reallife_roles_tab_jobs", "Jobs"), "5"),
        WkNav(UiIcon.People, w.T("reallife_roles_tab_applicants", "Applicants"), state == "applicants_none" ? null : "30"), WkNav(UiIcon.Globe, w.T("reallife_roles_tab_market", "Market")),
        WkNav(UiIcon.Search, w.T("reallife_roles_tab_search", "Search")), WkNav(UiIcon.Person, w.T("reallife_roles_tab_team", "Team"), "8") };

    private static WorkApp RolesAppOf(UtilitiesWords w, string state, WorkView v)
    {
        var app = new WorkApp { Ready = true, Nav = RolesNav(w, state) };
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

    private static string RRole(UtilitiesWords w, string key) => Sx(w, "reallife_talent_role_" + key);

    private static string RLevel(UtilitiesWords w, string key) => Sx(w, "reallife_talent_level_" + key);

    private static string RFamily(UtilitiesWords w, string key) => Sx(w, "reallife_talent_family_" + key);

    private static string RMoney(int dollars) => "$" + dollars + ".00";

    // The ads of the sample: role, level, place, posts left, posts, applicants, pay up to, going rate, the day it closes, glyph. Against today (412), one closes in three days.
    private static readonly object[][] WkAds =
    {
        new object[] { "ux_designer", "mid", "Pioneer Game Studio", 2, 2, 10, 42, 38, 416, UiIcon.PaintBrush }, new object[] { "it_support", "junior", "Pioneer Game Studio", 1, 1, 8, 24, 22, 417, UiIcon.Headset },
        new object[] { "data_centre_technician", "mid", "Hudson Data Hall", 3, 4, 4, 31, 29, 418, UiIcon.HardDrives }, new object[] { "network_engineer", "senior", "Midtown Fibre Co", 1, 1, 5, 44, 40, 415, UiIcon.Globe },
        new object[] { "qa_tester", "mid", "Pioneer Game Studio", 1, 1, 3, 26, 25, 419, UiIcon.Bug }
    };

    private static string AdName(UtilitiesWords w, object[] ad) => RRole(w, (string)ad[0]) + ", " + RLevel(w, (string)ad[1]);

    private static readonly string[] WkSeekerNames = { "Pavel Novak", "Sade Bello", "Owen Fraser", "Mei Tanaka", "Luca Moretti", "Amara Obi", "Elif Demir", "Noah Fischer", "Rosa Vidal", "Idris Cole" };

    // The strongest applicants of an ad, as the live source ranks them by the craft on the CV.
    private static void RolesBest(UtilitiesWords w, WorkRow r, int at)
    {
        var best = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_best_applicants", "Strongest applicants") };
        string[] stages = { Sx(w, "reallife_hire_stage_shortlisted"), Sx(w, "reallife_hire_stage_interviewed"), Sx(w, "reallife_hire_stage_new") };
        for (int k = 0; k < 3; k++)
        {
            best.People.Add(new WorkPerson { Name = WkSeekerNames[(at * 3 + k) % WkSeekerNames.Length], Role = stages[k] + ", " + w.F("reallife_work_craft_n", "craft {n}", "n", (84 - k * 6 - at).ToString()),
                Right = "$" + (30 + at * 2 + k * 3) + ".00", Ring = UiRole.Ok });
        }
        r.Sections.Add(best);
    }

    // The rival firms with an ad open in the line of the role, with what each pays over the going rate.
    private static void RolesRivals(UtilitiesWords w, WorkRow r, int at, int count)
    {
        var rivals = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_rivals_hiring", "Rivals hiring in this line") };
        string[] firms = { "Lumen Systems", "Redwater Digital", "Atlas Hosting" };
        for (int k = 0; k < count && k < 3; k++)
        {
            rivals.People.Add(new WorkPerson { Name = firms[(at + k) % firms.Length], Role = RLevel(w, k == 0 ? "mid" : "senior") + ", " + w.F("reallife_work_closes_on", "Closes {day}", "day", SDay(w, 416 + k * 3)),
                Right = "+" + (4 + k * 3) + "%", Ring = UiRole.Warn });
        }
        r.Sections.Add(rivals);
    }

    private static WorkTab RolesJobs(UtilitiesWords w)
    {
        var t = new WorkTab { Shape = WorkShape.Table };
        t.Columns = new[] { WkCol(w.T("reallife_work_col_role", "Role"), UiCellKind.Tile), WkCol(w.T("reallife_work_col_posts", "Posts to fill"), UiCellKind.Text, 112),
            WkCol(w.T("reallife_work_col_applicants", "Applicants"), UiCellKind.Figure, 96, true), WkCol(w.T("reallife_work_col_pay_up_to", "Up to"), UiCellKind.Money, 88),
            WkCol(w.T("reallife_work_col_state", "State"), UiCellKind.Chip, 168) };
        t.EmptyTitle = w.T("reallife_work_no_ads_title", "No role is open");
        t.EmptyBody = w.T("reallife_work_no_ads", "Post an ad and applicants arrive over a few days.");
        t.Acts.Add(WkAct(105, w.T("reallife_work_new_ad", "New ad"), UiButtonKind.Primary, UiIcon.Plus, null, true));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_open_posts", "Open posts"), "8"));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_waiting", "Applicants waiting"), "30"));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_hired_month", "Hired this month"), "3"));
        // who was hired this month through which ad: the data centre technician and the tester, the third through an ad that has closed
        string[] hiredName = { null, null, "Aiko Kato", null, "Jonah Reyes" };
        int[] hiredDay = { 0, 0, 398, 0, 394 };
        for (int i = 0; i < WkAds.Length; i++)
        {
            object[] ad = WkAds[i];
            int left = (int)ad[3], posts = (int)ad[4], came = (int)ad[5], pay = (int)ad[6], closes = (int)ad[8];
            string state = closes - Today <= 3 ? w.F("reallife_work_ad_closes_in", "Closes in {n} days", "n", (closes - Today).ToString()) : w.T("reallife_work_ad_open", "Open");
            UiRole tone = closes - Today <= 3 ? UiRole.Warn : UiRole.Ok;
            var r = new WorkRow { Id = i + 1, Icon = (UiIcon)ad[9], Name = AdName(w, ad), Sub = (string)ad[2], Cells = new[] { null, left + " / " + posts, came.ToString(), "$" + pay, state },
                Values = new[] { 0, 0, System.Math.Min(100, came * 8), 0, 0 }, Status = state, Tone = tone, Chip = state, ChipTone = tone, Group = i };
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_pay_up_to", "Pay up to"), Sf(w, "reallife_career_row_rate", "rate", "$" + pay)));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_posts", "Posts to fill"), left + " / " + posts));
            var facts = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_the_ad", "The ad") };
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_ad_skill", "Minimum skill"), "60"));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_ad_arrangement", "Arrangement"), w.T("reallife_work_arr_onsite", "On site")));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_ad_closes", "Closes"), SDay(w, closes)));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_ad_boosted", "Boosted"), i == 0 ? w.T("reallife_work_yes", "Yes") : w.T("reallife_work_no", "No")));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_ad_going", "Going rate"), "$" + (int)ad[7] + ".00"));
            r.Sections.Add(facts);
            var going = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_how_it_goes", "How it is going") };
            going.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_applicants", "Applicants"), came.ToString()));
            going.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_shortlisted_n", "On your shortlist"), (came / 3).ToString()));
            going.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_offers_out", "Offers out"), (came / 5).ToString()));
            r.Sections.Add(going);
            RolesBest(w, r, i);
            RolesRivals(w, r, i, 2);
            if (hiredName[i] != null)
            {
                var hired = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_hired_via_ad", "Hired through this ad"), Note = "1" };
                hired.People.Add(new WorkPerson { Name = hiredName[i], Role = (i == 2 ? RRole(w, "data_centre_technician") : RRole(w, "qa_tester")) + ", " + SDay(w, hiredDay[i]), Ring = UiRole.Ok });
                r.Sections.Add(hired);
            }
            r.Acts.Add(WkAct(WorkKinds.GoTab, w.T("reallife_hire_button_applicants", "Applicants"), UiButtonKind.Secondary, UiIcon.People, null, true, 1));
            r.Acts.Add(WkAct(104, w.T("reallife_work_close_ad", "Close the ad"), UiButtonKind.Destructive, UiIcon.Close, null, false, r.Id));
            t.Rows.Add(r);
        }
        var rival = new WorkRow { Id = 90, Icon = UiIcon.Flag, Name = RFamily(w, "design") + ", " + RLevel(w, "mid"), Sub = "Guo Interactive", Cells = new[] { null, string.Empty, string.Empty, "$44",
            w.T("reallife_work_rival_ad", "Rival ad") }, Status = w.T("reallife_work_rival_ad", "Rival ad"), Tone = UiRole.Muted, Chip = w.T("reallife_work_rival_ad", "Rival ad"), ChipTone = UiRole.Muted, Group = 9 };
        rival.Sentence = w.F("reallife_work_rival_ad_line", "{rival} hires the same line and pays {bp} over the going rate. Their applicants may take an offer before yours.", "rival", "Guo Interactive", "bp", "4%");
        t.Rows.Add(rival);
        // the foot: the pay of each ad against the going rate, and the ads about to close
        var icons = new UiIcon[WkAds.Length];
        var names = new string[WkAds.Length];
        var hints = new string[WkAds.Length];
        var values = new string[WkAds.Length];
        for (int i = 0; i < WkAds.Length; i++)
        {
            int pay = (int)WkAds[i][6], going = (int)WkAds[i][7];
            icons[i] = (UiIcon)WkAds[i][9];
            names[i] = AdName(w, WkAds[i]);
            hints[i] = w.F("reallife_work_going_n", "Going rate {rate}", "rate", "$" + going);
            values[i] = "$" + pay + " (" + WorkBuilder.Percent((pay - going) * 100 / going) + ")";
        }
        WorkBands.AddIfAny(t.Below, WorkBands.Tiles(w.T("reallife_work_band_pay_vs_going", "Your pay against the going rate"), icons, names, hints, values));
        WorkBands.AddIfAny(t.Below, WorkBands.Tiles(w.T("reallife_work_band_closing", "Closing soon"), new[] { UiIcon.Globe }, new[] { AdName(w, WkAds[3]) },
            new[] { w.F("reallife_work_closes_on", "Closes {day}", "day", SDay(w, 415)) }, new[] { w.F("reallife_work_n_applicants", "{n} applicants", "n", "5") }));
        return t;
    }

    private static WorkTab RolesNewAd(UtilitiesWords w, WorkView v)
    {
        var t = new WorkTab { Shape = WorkShape.Form, Sub = true };
        string[] roleKeys = { "ux_designer", "it_support", "data_centre_technician", "network_engineer", "qa_tester" };
        var roles = new string[roleKeys.Length];
        for (int i = 0; i < roles.Length; i++)
        {
            roles[i] = RRole(w, roleKeys[i]);
        }
        string[] places = { "Pioneer Game Studio", "Hudson Data Hall", "Midtown Fibre Co" };
        string[] levels = { w.T("reallife_work_level_junior", "Junior"), w.T("reallife_work_level_mid", "Mid"), w.T("reallife_work_level_senior", "Senior") };
        string[] works = { w.T("reallife_work_arr_onsite", "On site"), w.T("reallife_work_mode_hybrid", "Hybrid"), w.T("reallife_work_mode_remote", "From home") };
        int role = v.Get(0, 0), level = v.Get(1, 1), place = v.Get(2, 0), most = v.Get(3, 42), least = v.Get(4, 34), skill = v.Get(5, 60), work = v.Get(6, 0), days = v.Get(7, 14), posts = v.Get(8, 2);
        t.Amounts.Add(new WorkAmount { Id = 0, Value = role, Min = 0, Max = roles.Length - 1, Words = roles, Figure = roles[role] });
        t.Amounts.Add(new WorkAmount { Id = 1, Value = level, Min = 0, Max = 2, Words = levels, Figure = levels[level] });
        t.Amounts.Add(new WorkAmount { Id = 2, Value = place, Min = 0, Max = places.Length - 1, Words = places, Figure = places[place] });
        t.Amounts.Add(new WorkAmount { Id = 3, Value = most, Min = 10, Max = 120, Figure = Sf(w, "reallife_career_row_rate", "rate", "$" + most) });
        t.Amounts.Add(new WorkAmount { Id = 4, Value = least, Min = 10, Max = 120, Figure = Sf(w, "reallife_career_row_rate", "rate", "$" + least) });
        t.Amounts.Add(new WorkAmount { Id = 5, Value = skill, Min = 0, Max = 100, Step = 5, Figure = skill.ToString() });
        t.Amounts.Add(new WorkAmount { Id = 6, Value = work, Min = 0, Max = 2, Words = works, Figure = works[work] });
        t.Amounts.Add(new WorkAmount { Id = 7, Value = days, Min = 3, Max = 30, Figure = w.F("reallife_work_n_days", "{n} days", "n", days.ToString()) });
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
        sum.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_ad_going", "Going rate"), Value = RMoney(38) });
        sum.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_ad_pay_least", "Pay from"), Value = RMoney(least) });
        sum.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_ad_cost", "It costs"), Value = "$" + (days * 40) });
        form.Right.Add(sum);
        // the foot: who is already on the team in this role, and what the market holds for it
        WorkBands.AddIfAny(form.Below, WorkBands.People(w.T("reallife_work_band_already_team", "Already on your team"), new[] { "Mira Solberg" },
            new[] { RRole(w, "ux_designer") + ", " + RLevel(w, "mid") + ", Pioneer Game Studio" }, new[] { "$31.00" }));
        WorkBands.AddIfAny(form.Left, WorkBands.Pairs(w.T("reallife_work_band_market_for", "The market for it"),
            new[] { w.T("reallife_work_col_looking", "Looking"), w.T("reallife_work_col_rival_ads", "Rival ads"), w.T("reallife_work_shortage", "Shortage") }, new[] { "6", "2", "4.5%" }));
        t.Form = form;
        t.Acts.Add(WkAct(WorkKinds.Back, w.T("reallife_work_cancel", "Cancel"), UiButtonKind.Secondary, UiIcon.Back, null, true));
        t.Acts.Add(WkAct(107, w.T("reallife_hire_button_post", "Post the ad"), UiButtonKind.Primary, UiIcon.Check));
        return t;
    }

    private static WorkTab RolesApplicants(UtilitiesWords w, string state, WorkView v)
    {
        var t = new WorkTab { Shape = WorkShape.Table, Search = w.T("reallife_work_search_people", "Search people") };
        t.Columns = new[] { WkCol(w.T("reallife_work_col_applicant", "Applicant"), UiCellKind.Person), WkCol(w.T("reallife_work_skill_craft", "Craft"), UiCellKind.Meter, 80),
            WkCol(w.T("reallife_work_skill_ideas", "Ideas"), UiCellKind.Meter, 80), WkCol(w.T("reallife_work_skill_team", "Team"), UiCellKind.Meter,
                80), WkCol(w.T("reallife_work_col_asks", "Asks"), UiCellKind.Money, 64),
            WkCol(w.T("reallife_work_col_state", "State"), UiCellKind.Chip, 176) };
        t.EmptyTitle = w.T("reallife_work_no_applicants_title", "No applicants yet");
        t.EmptyBody = w.T("reallife_work_no_applicants", "Applicants arrive over a few days and other firms hire too. A boosted ad brings more.");
        t.EmptyIcon = UiIcon.People;
        var aside = new WorkAside { Title = w.T("reallife_work_open_roles", "Open roles"), Count = "5", ListTitle = w.T("reallife_work_hired_month", "Hired this month"), ListCount = "3",
            Foot = w.T("reallife_work_applicants_foot", "Applicants arrive over a few days and take other offers if you wait.") };
        for (int i = 0; i < WkAds.Length; i++)
        {
            aside.Rows.Add(new WorkRow { Id = i + 1, Icon = (UiIcon)WkAds[i][9], Name = AdName(w, WkAds[i]), Sub = (string)WkAds[i][2], Status = state == "applicants_none" ? "0" : ((int)WkAds[i][5]).ToString() });
        }
        aside.List.Add(new WorkPerson { Name = "Kenji Mori", Role = RRole(w, "software_engineer"), Ring = UiRole.Ok });
        aside.List.Add(new WorkPerson { Name = "Jonah Reyes", Role = RRole(w, "qa_tester"), Ring = UiRole.Ok });
        aside.List.Add(new WorkPerson { Name = "Aiko Kato", Role = RRole(w, "data_centre_technician"), Ring = UiRole.Ok });
        t.Aside = aside;
        if (state == "applicants_none")
        {
            return t;
        }
        // name, where from (null: self taught), craft, ideas, team, ask, kind (0 available, 1 competing offer, 2 notice in weeks, 3 and 4 notice in days), the notice
        object[][] people =
        {
            new object[] { "Nora Whitfield", "Tidewater Interactive", 82, 88, 70, 41, 1, 0 }, new object[] { "Kofi Mensah", "Pixel Pit", 76, 71, 84, 36, 0, 0 },
            new object[] { "Camille Duval", "Copperkettle Games", 79, 69, 77, 38, 0, 0 }, new object[] { "Linnea Berg", "Fernwood Games", 71, 80, 66, 34, 2, 2 },
            new object[] { "Mateo Silva", "Gridline Studio", 64, 66, 81, 30, 2, 1 }, new object[] { "Ravi Anand", null, 58, 74, 72, 26, 0, 0 },
            new object[] { "Zoe Hartley", "Hudson Tech Institute", 49, 77, 79, 22, 0, 0 }, new object[] { "Jonas Weber", "Pixel Harbour Games", 68, 62, 74, 31, 3, 3 },
            new object[] { "Imani Okoro", "Hollowpine Interactive", 73, 59, 68, 33, 0, 0 }, new object[] { "Elif Demir", null, 52, 81, 70, 24, 0, 0 }
        };
        var shownNames = new List<string>(12);
        var shownFrom = new List<string>(12);
        var shownCraft = new List<int>(12);
        var shownAsk = new List<int>(12);
        var shownAskText = new List<string>(12);
        for (int i = 0; i < people.Length; i++)
        {
            object[] p = people[i];
            string name = (string)p[0], firm = (string)p[1];
            int craft = (int)p[2], ideas = (int)p[3], team = (int)p[4], ask = (int)p[5], kind = (int)p[6], when = (int)p[7];
            shownNames.Add(name);
            shownFrom.Add(firm == null ? w.T("reallife_work_self_taught", "Self taught") : w.F("reallife_work_from_firm", "From {firm}", "firm", firm));
            shownCraft.Add(craft);
            shownAsk.Add(ask);
            shownAskText.Add("$" + ask + ".00");
            bool competing = kind == 1;
            string stateWord = competing ? w.T("reallife_work_competing_offer", "Competing offer") : kind == 0 ? w.T("reallife_work_available_now", "Available now")
                : w.F("reallife_work_notice_of", "Notice, {n}", "n", kind == 2 ? WorkBuilder.Weeks(w, when) : w.F("reallife_work_n_days", "{n} days", "n", when.ToString()));
            UiRole tone = competing ? UiRole.Warn : kind >= 2 ? UiRole.Muted : UiRole.Ok;
            string from = firm == null ? w.T("reallife_work_self_taught", "Self taught") : w.F("reallife_work_from_firm", "From {firm}", "firm", firm);
            var r = new WorkRow { Id = i + 1, Person = name, Name = name, Sub = from, Title = name, Ring = UiRole.Ok, Group = 1, Cells = new[] { null, craft.ToString(), ideas.ToString(), team.ToString(), "$" + ask, stateWord },
                Values = new[] { 0, craft, ideas, team, 0, 0 }, Status = stateWord, Tone = tone, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone } };
            r.Open = WkAct(110, string.Empty, UiButtonKind.Secondary, UiIcon.Person, null, true, r.Id);
            r.Sentence = RRole(w, "ux_designer") + ", " + RLevel(w, "mid") + ", " + Sf(w, "reallife_career_row_rate", "rate", "$" + ask);
            var attrs = new WorkSection { Part = WorkPart.Bars, Title = w.T("reallife_work_attributes", "Attributes") };
            attrs.Bars.Add(new WorkBar { Label = w.T("reallife_work_skill_craft", "Craft"), Value = craft });
            attrs.Bars.Add(new WorkBar { Label = w.T("reallife_work_skill_ideas", "Ideas"), Value = ideas });
            attrs.Bars.Add(new WorkBar { Label = w.T("reallife_work_skill_team", "Team"), Value = team });
            attrs.Bars.Add(new WorkBar { Label = w.T("reallife_work_skill_reliability", "Reliability"), Value = 76 });
            r.Sections.Add(attrs);
            if (i == 0)
            {
                var work = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_so_far_title", "Work so far") };
                work.Pairs.Add(new KeyValuePair<string, string>(w.F("reallife_work_job_line", "{role}, {firm}", "role", RRole(w, "ux_designer"), "firm", "Tidewater Interactive"), w.F("reallife_work_years_now", "{n} years, now", "n", "4")));
                work.Pairs.Add(new KeyValuePair<string, string>(w.F("reallife_work_job_line", "{role}, {firm}", "role", RRole(w, "graphic_designer"), "firm", "Pixel Harbour Games"), w.F("reallife_work_n_years", "{n} years", "n", "2")));
                r.Sections.Add(work);
            }
            var fit = new WorkSection { Part = WorkPart.Checks, Title = w.T("reallife_work_fit_for_role", "Fit for the role") };
            fit.Checks.Add(new WorkNeed { Words = w.F("reallife_work_fit_craft", "Craft above your team's average of {n}", "n", "71"), Met = craft > 71 });
            fit.Checks.Add(new WorkNeed { Words = w.F("reallife_work_fit_pay", "Asks less than the {pay} you set", "pay", "$42"), Met = true });
            r.Sections.Add(fit);
            if (competing)
            {
                r.Sections.Add(new WorkSection { Part = WorkPart.Note, Lead = w.T("reallife_work_competing_offer_lead", "Competing offer"),
                    Text = w.F("reallife_work_rival_offer_line", "{rival} offered {pay} an hour. They answer by {day}. Offer at least as much to hire them.", "rival", "Guo Interactive", "pay", "$44",
                        "day", SDay(w, 415)), NoteIcon = UiIcon.Flag });
            }
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
        WorkBands.Candidates(t.Below, w, shownNames.ToArray(), shownFrom.ToArray(), shownCraft.ToArray(), shownAsk.ToArray(), shownAskText.ToArray());
        return t;
    }

    // The market as the live source lays it: by line, with the people looking by level, the rival ads and the going rate.
    private static WorkTab RolesMarket(UtilitiesWords w)
    {
        var t = new WorkTab { Shape = WorkShape.Table };
        t.Columns = new[] { WkCol(w.T("reallife_work_col_line", "Line"), UiCellKind.Tile), WkCol(w.T("reallife_work_col_looking", "Looking"), UiCellKind.Figure, 80, true),
            WkCol(w.T("reallife_work_level_junior", "Junior"), UiCellKind.Figure, 72), WkCol(w.T("reallife_work_level_mid", "Mid"), UiCellKind.Figure, 72),
            WkCol(w.T("reallife_work_level_senior", "Senior"), UiCellKind.Figure, 80), WkCol(w.T("reallife_work_col_rival_ads", "Rival ads"), UiCellKind.Figure, 88),
            WkCol(w.T("reallife_work_col_going_rate", "Going rate"), UiCellKind.Money, 96) };
        t.EmptyTitle = w.T("reallife_work_market_empty_title", "Nobody is on the market");
        t.EmptyBody = w.T("reallife_work_market_lead", "Professionals looking for work show here by line. Post an ad and the ones who fit apply.");
        // the family, its glyph, looking, junior, mid, senior, rival ads, going rate, your open ads, the shortage in tenths of a per cent
        object[][] lines =
        {
            new object[] { "engineering", UiIcon.Code, 14, 5, 6, 3, 3, 38, 0, 12 }, new object[] { "data", UiIcon.Database, 9, 3, 4, 2, 1, 41, 0, 20 },
            new object[] { "security", UiIcon.ShieldCheck, 5, 1, 2, 2, 2, 44, 0, 31 }, new object[] { "infrastructure", UiIcon.HardDrives, 8, 2, 4, 2, 2, 36, 1, 18 },
            new object[] { "product", UiIcon.Layout, 4, 1, 2, 1, 1, 40, 0, 8 }, new object[] { "design", UiIcon.PaintBrush, 11, 5, 4, 2, 2, 34, 1, 5 },
            new object[] { "commercial", UiIcon.Handshake, 12, 6, 4, 2, 2, 31, 0, 0 }, new object[] { "support", UiIcon.Headset, 16, 9, 5, 2, 1, 22, 1, 0 },
            new object[] { "corporate", UiIcon.Building, 7, 2, 3, 2, 1, 35, 0, 6 }, new object[] { "built", UiIcon.Hammer, 3, 1, 1, 1, 1, 37, 0, 14 }
        };
        for (int i = 0; i < lines.Length; i++)
        {
            object[] l = lines[i];
            string name = RFamily(w, (string)l[0]);
            int looking = (int)l[2], jr = (int)l[3], mid = (int)l[4], sr = (int)l[5], rivalAds = (int)l[6], going = (int)l[7], yours = (int)l[8], shortage = (int)l[9];
            var r = new WorkRow { Id = i, Icon = (UiIcon)l[1], Name = name, Sub = w.F("reallife_work_your_ads_n", "{n} of your ads open", "n", yours.ToString()),
                Cells = new[] { null, looking.ToString(), jr.ToString(), mid.ToString(), sr.ToString(), rivalAds.ToString(), "$" + going }, Values = new[] { 0, System.Math.Min(100, looking * 6), 0, 0, 0, 0, 0 } };
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_looking", "Looking"), looking.ToString()));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_going_rate", "Going rate"), "$" + going + ".00"));
            var facts = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_the_line", "The line") };
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_shortage", "Shortage"), (shortage / 10f).ToString("0.#") + "%"));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_rival_ads", "Rival ads"), rivalAds.ToString()));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_your_ads", "Your open ads"), yours.ToString()));
            r.Sections.Add(facts);
            var here = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_looking_here", "Looking in this line") };
            for (int k = 0; k < 3 && k < System.Math.Max(1, (int)l[2] / 3); k++)
            {
                here.People.Add(new WorkPerson { Name = WkSeekerNames[(i + k * 2) % WkSeekerNames.Length], Role = RRole(w, i % 3 == 0 ? "software_engineer" : i % 3 == 1 ? "qa_tester" : "ux_designer") + ", " + RLevel(w, k == 0 ? "senior" : "mid"),
                    Right = "$" + (going - k * 3 + 2) + ".00", Ring = UiRole.Ok });
            }
            r.Sections.Add(here);
            RolesRivals(w, r, i, rivalAds);
            r.Sections.Add(new WorkSection { Part = WorkPart.Note, Lead = w.T("reallife_work_market_how_lead", "How to hire from here."), NoteIcon = UiIcon.Info,
                Text = w.T("reallife_work_market_how", "Post an ad for a role in this line and the seekers who fit apply over a few days. A shortage makes them ask more.") });
            r.Acts.Add(WkAct(105, w.T("reallife_work_new_ad", "New ad"), UiButtonKind.Primary, UiIcon.Plus, null, true));
            t.Rows.Add(r);
        }
        var byShortage = new List<object[]>(lines);
        byShortage.Sort((a, b) => ((int)b[9]).CompareTo((int)a[9]));
        var byLooking = new List<object[]>(lines);
        byLooking.Sort((a, b) => ((int)b[2]).CompareTo((int)a[2]));
        for (int pass = 0; pass < 2; pass++)
        {
            List<object[]> sorted = pass == 0 ? byShortage : byLooking;
            var icons = new UiIcon[3];
            var names = new string[3];
            var hints = new string[3];
            var values = new string[3];
            for (int i = 0; i < 3; i++)
            {
                icons[i] = (UiIcon)sorted[i][1];
                names[i] = RFamily(w, (string)sorted[i][0]);
                hints[i] = pass == 0 ? w.F("reallife_work_shortage_n", "Shortage {n}", "n", ((int)sorted[i][9] / 10f).ToString("0.#") + "%") : w.F("reallife_work_looking_n", "{n} looking", "n", sorted[i][2].ToString());
                values[i] = "$" + (int)sorted[i][7];
            }
            WorkBands.AddIfAny(t.Below, WorkBands.Tiles(pass == 0 ? w.T("reallife_work_band_hardest", "Hardest to hire in") : w.T("reallife_work_band_most_looking", "Most people looking"), icons, names, hints, values));
        }
        return t;
    }

    private static WorkTab RolesSearch(UtilitiesWords w, WorkView v)
    {
        var t = new WorkTab { Shape = WorkShape.Form };
        string[] roleKeys = { "software_engineer", "network_engineer", "data_centre_technician" };
        var roles = new string[roleKeys.Length];
        for (int i = 0; i < roles.Length; i++)
        {
            roles[i] = RRole(w, roleKeys[i]);
        }
        string[] levels = { w.T("reallife_work_level_senior", "Senior"), RLevel(w, "staff"), RLevel(w, "principal") };
        string[] places = { "Pioneer Game Studio", "Hudson Data Hall", "Midtown Fibre Co" };
        int role = v.Get(0, 0), level = v.Get(1, 0), place = v.Get(2, 0), crew = v.Get(3, 4);
        t.Amounts.Add(new WorkAmount { Id = 0, Value = role, Min = 0, Max = roles.Length - 1, Words = roles, Figure = roles[role] });
        t.Amounts.Add(new WorkAmount { Id = 1, Value = level, Min = 0, Max = 2, Words = levels, Figure = levels[level] });
        t.Amounts.Add(new WorkAmount { Id = 2, Value = place, Min = 0, Max = places.Length - 1, Words = places, Figure = places[place] });
        t.Amounts.Add(new WorkAmount { Id = 3, Value = crew, Min = 1, Max = 12, Figure = crew.ToString() });
        var form = new WorkFormModel { Lead = w.T("reallife_work_search_lead",
            "A search firm finds senior people, and a crew search finds technicians for a data centre. Its fee is a share of a year's pay.") };
        var brief = new WorkFormBand { Title = w.T("reallife_work_brief_title", "Brief a search firm") };
        brief.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_ad_place", "At"), Stepper = 2 });
        brief.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_col_role", "Role"), Stepper = 0 });
        brief.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_col_level", "Level"), Stepper = 1 });
        brief.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_col_fee", "Fee"), Value = "$18,400, " + w.F("reallife_work_up_front", "{n} up front", "n", "$6,100") });
        brief.Foot = WkAct(120, w.T("reallife_hire_button_brief", "Brief the firm"), UiButtonKind.Primary, UiIcon.Search);
        form.Left.Add(brief);
        var crewBand = new WorkFormBand { Title = w.T("reallife_work_crew_title", "Data centre crew") };
        crewBand.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_crew_technicians", "Technicians"), Stepper = 3 });
        crewBand.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_col_fee", "Fee"), Value = "$22,000, " + w.F("reallife_work_each_n", "{n} each", "n", "$5,500") });
        crewBand.Foot = WkAct(121, w.T("reallife_hire_button_brief_crew", "Brief a crew search"), UiButtonKind.Secondary, UiIcon.HardDrives);
        form.Left.Add(crewBand);
        var running = new WorkFormBand { Title = w.T("reallife_work_running_searches", "Searches running"), Count = "1" };
        running.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = RRole(w, "network_engineer") + ", " + RLevel(w, "senior"), Hint = w.F("reallife_work_fee_n", "Fee {n}", "n", "$16,800"), Value = SDay(w, 416), Icon = UiIcon.Search });
        form.Right.Add(running);
        // a search still looking, due in 416 against today's 412: three days to save, at a twentieth of its fee a day
        var rush = new WorkFormBand { Title = w.T("reallife_work_rush_title", "Rush a search"), Count = "1" };
        rush.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = RRole(w, "network_engineer") + ", " + RLevel(w, "senior"),
            Act = WkAct(117, w.F("reallife_work_rush_for", "Rush for {n}", "n", "$2,520"), UiButtonKind.Secondary, UiIcon.FastForward) });
        form.Right.Add(rush);
        var line = new WorkFormBand { Title = w.T("reallife_work_band_the_line_briefed", "The line you brief") };
        line.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_ad_going", "Going rate"), Value = "$44.00" });
        line.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_shortage", "Shortage"), Value = "3.1%" });
        line.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_col_looking", "Looking"), Value = "9" });
        form.Right.Add(line);
        var offices = new WorkFormBand { Title = w.T("reallife_work_office_campaigns", "Recruitment office"), Count = "1" };
        offices.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = RRole(w, "data_centre_technician"), Hint = "Hudson Data Hall, 3 / 5", Value = SDay(w, 420), Icon = UiIcon.Megaphone });
        form.Right.Add(offices);
        // the foot: the seekers of the line the brief is for, the ones a search firm would put first
        WorkBands.AddIfAny(form.Below, WorkBands.People(w.T("reallife_work_band_on_market", "On the market now"), new[] { "Pavel Novak", "Sade Bello", "Owen Fraser" },
            new[] { RRole(w, "software_engineer") + ", " + RLevel(w, "senior"), RRole(w, "network_engineer") + ", " + RLevel(w, "senior"), RRole(w, "data_centre_technician") + ", " + RLevel(w, "mid") },
            new[] { "$48.00", "$46.00", "$30.00" }));
        t.Form = form;
        return t;
    }

    // The team: each person as the live source reads them (a name, the role and level, craft, pay and morale), the foot: who is paid under the going rate and who is wearing thin.
    // name, role, level, craft, pay, morale, place, the day joined, the going pay, burnout
    private static readonly object[][] WkTeam =
    {
        new object[] { "Kenji Mori", "software_engineer", "senior", 84, 38, 82, "Pioneer Game Studio", 389, 40, 5 }, new object[] { "Jonah Reyes", "qa_tester", "mid", 77, 32, 71, "Pioneer Game Studio", 394, 32, 20 },
        new object[] { "Aiko Kato", "data_centre_technician", "mid", 70, 29, 88, "Hudson Data Hall", 398, 30, 12 }, new object[] { "Rhys Evans", "network_engineer", "mid", 74, 34, 64, "Midtown Fibre Co", 392, 36, 41 },
        new object[] { "Mira Solberg", "ux_designer", "mid", 72, 31, 79, "Pioneer Game Studio", 385, 33, 15 }, new object[] { "Tomas Ruiz", "it_support", "junior", 58, 22, 85, "Pioneer Game Studio", 396, 22, 8 },
        new object[] { "Priya Nair", "qa_tester", "mid", 66, 27, 73, "Pioneer Game Studio", 400, 29, 24 }, new object[] { "Gabe Osei", "data_centre_technician", "junior", 55, 24, 90, "Hudson Data Hall", 391, 24, 6 }
    };

    private static WorkTab RolesTeam(UtilitiesWords w)
    {
        var t = new WorkTab { Shape = WorkShape.Table, Search = w.T("reallife_work_search_people", "Search people") };
        t.Columns = new[] { WkCol(w.T("reallife_work_col_name", "Name"), UiCellKind.Person), WkCol(w.T("reallife_work_col_role", "Role"),
            UiCellKind.Text, 232), WkCol(w.T("reallife_work_skill_craft", "Craft"), UiCellKind.Meter, 120),
            WkCol(w.T("reallife_work_col_pay", "Pay"), UiCellKind.Money, 80), WkCol(w.T("reallife_work_col_morale", "Morale"), UiCellKind.Meter, 112) };
        t.EmptyTitle = w.T("reallife_work_no_team_title", "Nobody is on your payroll");
        t.EmptyBody = w.T("reallife_work_no_team", "Hire from the applicants and they join here, each with a page for their file, career and contract.");
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_payroll", "Payroll an hour"), "$237"));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_morale", "Average morale"), "79"));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_leaving", "Leaving"), "0"));
        var gapNames = new List<string>(3);
        var gapRoles = new List<string>(3);
        var gapRights = new List<string>(3);
        var wornNames = new List<string>(3);
        var wornRoles = new List<string>(3);
        var wornRights = new List<string>(3);
        var allNames = new List<string>(8);
        var allRoles = new List<string>(8);
        var allJoined = new List<int>(8);
        var allJoinedText = new List<string>(8);
        for (int i = 0; i < WkTeam.Length; i++)
        {
            object[] p = WkTeam[i];
            string name = (string)p[0], role = RRole(w, (string)p[1]) + ", " + RLevel(w, (string)p[2]);
            int craft = (int)p[3], pay = (int)p[4], morale = (int)p[5], going = (int)p[8], burnout = (int)p[9];
            allNames.Add(name);
            allRoles.Add(role);
            allJoined.Add((int)p[7]);
            allJoinedText.Add(SDay(w, (int)p[7]));
            var r = new WorkRow { Id = i + 1, Person = name, Name = name, Sub = (string)p[6], Ring = UiRole.Ok, Cells = new[] { null, role, craft.ToString(), "$" + pay, morale.ToString() },
                Values = new[] { 0, 0, craft, 0, morale } };
            r.Open = WkAct(111, string.Empty, UiButtonKind.Secondary, UiIcon.Person, null, true, r.Id);
            r.Title = name;
            r.Sentence = role + ", " + (string)p[6];
            // what the row does not say: what the person is worth on the market, when they were last looked at, and how worn they are
            var now = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_now", "Now") };
            now.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_going_pay", "Going pay"), "$" + going + ".00"));
            now.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_last_review", "Last review"), "4 / 5, " + SDay(w, (int)p[7] + 21)));
            now.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_hired_on", "Joined"), SDay(w, (int)p[7])));
            r.Sections.Add(now);
            var worn = new WorkSection { Part = WorkPart.Bars, Title = w.T("reallife_work_skills_word", "Skills and wear") };
            string[][] pair = { new[] { "coding", "system_design" }, new[] { "testing", "troubleshooting" }, new[] { "networking", "reliability" }, new[] { "networking", "reliability" },
                new[] { "visual_design", "user_research" }, new[] { "troubleshooting", "networking" }, new[] { "testing", "troubleshooting" }, new[] { "networking", "reliability" } };
            worn.Bars.Add(new WorkBar { Label = SSkill(w, "t:" + pair[i][0]), Value = System.Math.Min(100, craft + 2) });
            worn.Bars.Add(new WorkBar { Label = SSkill(w, "t:" + pair[i][1]), Value = System.Math.Max(0, craft - 6) });
            worn.Bars.Add(new WorkBar { Label = w.T("reallife_work_burnout", "Burnout"), Value = burnout });
            r.Sections.Add(worn);
            var career = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_person_career", "Career") };
            career.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_experience", "Experience"), w.F("reallife_work_n_years", "{n} years", "n", (3 + i).ToString())));
            career.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_school", "Schooling"), Sx(w, "reallife_talent_school_bachelor")));
            career.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_certificates", "Certificates"), w.T("reallife_work_none_word", "None")));
            r.Sections.Add(career);
            r.Acts.Add(WkAct(111, w.T("reallife_work_open_page", "Open their page"), UiButtonKind.Primary, UiIcon.Person, null, true, r.Id));
            t.Rows.Add(r);
            if (going - pay >= 2)
            {
                gapNames.Add(name);
                gapRoles.Add(w.F("reallife_work_paid_going", "Paid {paid}, going {going}", "paid", "$" + pay, "going", "$" + going));
                gapRights.Add("-$" + (going - pay) + ".00");
            }
            if (burnout >= 20)
            {
                wornNames.Add(name);
                wornRoles.Add(role);
                wornRights.Add(w.T("reallife_work_burnout", "Burnout") + " " + burnout);
            }
        }
        WorkBands.AddIfAny(t.Below, WorkBands.People(w.T("reallife_work_band_under_going", "Paid under the going rate"), gapNames.ToArray(), gapRoles.ToArray(), gapRights.ToArray(), 2));
        WorkBands.AddIfAny(t.Below, WorkBands.People(w.T("reallife_work_band_wearing", "Wearing thin"), wornNames.ToArray(), wornRoles.ToArray(), wornRights.ToArray(), 2));
        WorkBands.Tenure(t.Below, w, allNames.ToArray(), allRoles.ToArray(), allJoined.ToArray(), allJoinedText.ToArray());
        return t;
    }

    private static WorkTab RolesPerson(UtilitiesWords w, WorkView v)
    {
        var t = new WorkTab { Shape = WorkShape.Form, Sub = true, PersonTabs = new[] { w.T("reallife_work_person_file", "File"),
            w.T("reallife_work_person_career", "Career"), w.T("reallife_work_person_contract", "Contract") } };
        string role = RRole(w, "software_engineer") + ", " + RLevel(w, "senior");
        var form = new WorkFormModel { Head = new UiProfileModel { Name = "Kenji Mori", Hue = UiColour.HueOf("Kenji Mori"), Ring = UiRole.Ok, Role = role, RoleIcon = UiIcon.Code,
            Employer = "Pioneer Game Studio", EmployerIcon = UiIcon.Store, Status = Sx(w, "reallife_roles_state_fine"), StatusTone = UiRole.Ok,
            FactLabels = new[] { w.T("reallife_work_col_pay", "Pay"), w.T("reallife_work_hired_on", "Joined"), w.T("reallife_work_col_morale", "Morale") }, FactValues = new[] { "$38.00", SDay(w, 389), "82%" } } };
        t.Acts.Add(WkAct(WorkKinds.Back, w.T("reallife_work_back_to_team", "Back to the team"), UiButtonKind.Secondary, UiIcon.Back, null, true));
        if (v.PageTab == 0)
        {
            var attrs = new WorkFormBand { Title = w.T("reallife_work_attributes", "Attributes") };
            attrs.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = SSkill(w, "t:coding"), Value = 86 } });
            attrs.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = SSkill(w, "t:system_design"), Value = 79 } });
            attrs.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = w.T("reallife_work_skill_craft", "Craft"), Value = 84 } });
            attrs.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = w.T("reallife_work_col_morale", "Morale"), Value = 82 } });
            attrs.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = w.T("reallife_work_burnout", "Burnout"), Value = 5 } });
            form.Left.Add(attrs);
            var now = new WorkFormBand { Title = w.T("reallife_work_now", "Now") };
            now.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_going_pay", "Going pay"), Value = "$40.00" });
            now.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_last_review", "Last review"), Value = "4 / 5, " + SDay(w, 410) });
            form.Right.Add(now);
        }
        else if (v.PageTab == 1)
        {
            var career = new WorkFormBand { Title = w.T("reallife_work_person_career", "Career") };
            career.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_col_role", "Role"), Value = role });
            career.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_experience", "Experience"), Value = w.F("reallife_work_n_years", "{n} years", "n", "8") });
            career.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_school", "Schooling"), Value = Sx(w, "reallife_talent_school_bachelor") });
            career.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_certificates", "Certificates"), Value = w.T("reallife_work_none_word", "None") });
            career.Rows.Add(new WorkFormRow { Field = WorkField.Check, Label = Sf(w, "reallife_roles_career_no_promote", "rating", "4", "least", "85%"), On = false });
            var rises = new WorkFormRow { Field = WorkField.Button, Label = w.T("reallife_work_rises_label", "Pay"), Act = WkAct(122, w.T("reallife_roles_button_promote", "Promote"), UiButtonKind.Primary, UiIcon.TrendUp,
                w.T("reallife_work_not_ready_promote", "They are not ready for a step up.")) };
            rises.More.Add(WkAct(123, w.T("reallife_roles_button_rise_3", "Rise of 3%"), UiButtonKind.Secondary, UiIcon.Plus));
            rises.More.Add(WkAct(124, w.T("reallife_roles_button_rise_8", "Rise of 8%"), UiButtonKind.Secondary, UiIcon.Plus));
            career.Rows.Add(rises);
            form.Left.Add(career);
            var learn = new WorkFormBand { Title = w.T("reallife_work_learning", "Learning") };
            var courses = new WorkFormRow { Field = WorkField.Button, Label = w.T("reallife_work_courses_label", "A course"), Act = WkAct(125, SSkill(w, "t:coding") + ", $1,800", UiButtonKind.Secondary, UiIcon.Books) };
            courses.More.Add(WkAct(126, SSkill(w, "t:system_design") + ", $2,100", UiButtonKind.Secondary, UiIcon.Books));
            learn.Rows.Add(courses);
            form.Right.Add(learn);
        }
        else
        {
            var contract = new WorkFormBand { Title = w.T("reallife_work_person_contract", "Contract") };
            string[] works = { w.T("reallife_work_arr_onsite", "On site"), w.T("reallife_work_mode_hybrid", "Hybrid"), w.T("reallife_work_mode_remote", "From home") };
            int work = v.Get(0, 0);
            t.Amounts.Add(new WorkAmount { Id = 0, Value = work, Min = 0, Max = 2, Words = works, Figure = works[work] });
            contract.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_ad_arrangement", "Arrangement"), Stepper = 0 });
            contract.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_they_want", "They want"), Value = works[1] });
            contract.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_notice", "Notice"), Value = w.F("reallife_work_n_days", "{n} days", "n", "30") });
            contract.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_severance", "Severance"), Value = "$9,120" });
            contract.Foot = WkAct(127, w.T("reallife_work_set_arrangement", "Set this arrangement"), UiButtonKind.Primary, UiIcon.Check, work == 0 ? w.T("reallife_work_already_set", "It is set so.") : null);
            form.Left.Add(contract);
            var terms = new WorkFormBand { Title = w.T("reallife_work_terms_title", "Terms and leaving") };
            terms.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = Sf(w, "reallife_roles_contract_clause_off", "percent", "10%") });
            terms.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = string.Empty,
                Act = WkAct(128, w.T("reallife_roles_button_clause", "Add a restraint clause"), UiButtonKind.Secondary, UiIcon.Lock) });
            terms.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = w.T("reallife_work_lay_off_label", "Lay them off"),
                Act = WkAct(129, w.T("reallife_roles_button_lay_off", "Lay off"), UiButtonKind.Destructive, UiIcon.SignOut) });
            form.Right.Add(terms);
        }
        // the foot, on every page of the person: the others on the same team
        WorkBands.AddIfAny(form.Below, WorkBands.People(w.T("reallife_work_band_same_team", "On the same team"), new[] { "Jonah Reyes", "Mira Solberg", "Priya Nair" },
            new[] { RRole(w, "qa_tester") + ", " + RLevel(w, "mid"), RRole(w, "ux_designer") + ", " + RLevel(w, "mid"), RRole(w, "qa_tester") + ", " + RLevel(w, "mid") },
            new[] { "$32.00", "$31.00", "$27.00" }));
        t.Form = form;
        return t;
    }
}
