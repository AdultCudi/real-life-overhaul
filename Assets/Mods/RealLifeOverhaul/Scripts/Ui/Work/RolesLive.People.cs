using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.Tech;
using TFamily = RealLifeOverhaul.Tech.Family;

namespace RealLifeOverhaul.Ui
{
    // Open Roles' people: the applicants of a role as a squad page and a person's page for each, the market by line, the searches and the team.
    public sealed partial class RolesLive
    {
        private static WorkAct Act(int kind, int arg, string label, UiButtonKind style, UiIcon icon, bool opens = false, string why = null)
        {
            return new WorkAct { Kind = kind, Arg = arg, Label = label, Style = style, Icon = icon, Opens = opens, Why = why };
        }

        private static string StageName(Applicant a)
        {
            switch (a.At)
            {
                case ApplyStage.New: return TechText.T("reallife_hire_stage_new");
                case ApplyStage.Shortlisted: return TechText.T("reallife_hire_stage_shortlisted");
                case ApplyStage.Interviewed: return TechText.T("reallife_hire_stage_interviewed");
                case ApplyStage.Offered: return TechText.T("reallife_hire_stage_offered");
                case ApplyStage.Countered: return TechText.T("reallife_hire_stage_countered", "amount", TechText.Rate(a.Counter));
                case ApplyStage.Accepted: return TechText.T("reallife_hire_stage_accepted", "day", a.Start.ToString());
                case ApplyStage.Started: return TechText.T("reallife_hire_stage_started");
                case ApplyStage.Declined: return TechText.T("reallife_hire_stage_declined");
                case ApplyStage.Rejected: return TechText.T("reallife_hire_stage_rejected");
                case ApplyStage.Withdrawn: return TechText.T("reallife_hire_stage_withdrawn");
                default: return TechText.T("reallife_hire_stage_lapsed");
            }
        }

        // The mean of the main skill of the player's own people, whole points; 0 with nobody on the payroll.
        private static int TeamAverage(TechDirector d)
        {
            int sum = 0;
            for (int i = 0; i < d.People.Count; i++)
            {
                sum += d.People[i].MainPoints;
            }
            return d.People.Count == 0 ? 0 : sum / d.People.Count;
        }

        // ------------------------------------------------------------------------------------------------- Applicants

        private WorkTab Applicants(TechDirector d, HiringFile h, int day, WorkView view)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Table, Search = w.T("reallife_work_search_people", "Search people") };
            // the roles asked for, as a list at the left; the applicants shown are the chosen role's
            var aside = new WorkAside { Title = w.T("reallife_work_open_roles", "Open roles"), Foot = w.T("reallife_work_applicants_foot",
                "Applicants arrive over a few days and take other offers if you wait.") };
            var roleAds = new List<JobAd>(8);
            for (int i = 0; i < h.Ads.Count; i++)
            {
                if (!h.Ads[i].Search)
                {
                    roleAds.Add(h.Ads[i]);
                }
            }
            for (int i = 0; i < roleAds.Count; i++)
            {
                JobAd ad = roleAds[i];
                int came = 0;
                for (int k = 0; k < h.Applicants.Count; k++)
                {
                    came += h.Applicants[k].Ad == ad.Id && h.Applicants[k].Live ? 1 : 0;
                }
                aside.Rows.Add(new WorkRow { Id = ad.Id, Icon = RoleGlyph(ad.Role), Name = TechText.Role(ad.Role), Sub = TalentGame.NameOf(TalentGame.Find(ad.Place)), Status = came.ToString(Inv) });
            }
            aside.Count = roleAds.Count.ToString(Inv);
            aside.ListTitle = w.T("reallife_work_hired_month", "Hired this month");
            for (int i = 0; i < h.Applicants.Count && aside.List.Count < 4; i++)
            {
                Applicant a = h.Applicants[i];
                Seeker hired = h.SeekerOf(a.Seeker);
                if (a.Stage == (int)ApplyStage.Started && a.Start >= day - 30 && hired != null)
                {
                    aside.List.Add(new WorkPerson { Name = hired.P.Name, Role = TechText.Role(hired.P.Role) + ", " + TechText.Day(a.Start), Ring = UiRole.Ok });
                }
            }
            aside.ListCount = aside.List.Count.ToString(Inv);
            t.Aside = aside;
            int roleIndex = Math.Max(0, Math.Min(Math.Max(0, roleAds.Count - 1), view.Aside));
            RoleDef shown = roleAds.Count > 0 ? TalentCatalogue.Role(roleAds[roleIndex].Role) : null;
            t.Columns = new[] { WorkBuilder.Col(w.T("reallife_work_col_applicant", "Applicant"), UiCellKind.Person),
                WorkBuilder.Col(w.T("reallife_work_skill_claimed", "Craft on the CV"), UiCellKind.Meter, 104),
                WorkBuilder.Col(w.T("reallife_work_skill_found", "Found at interview"), UiCellKind.Meter, 104), WorkBuilder.Col(w.T("reallife_work_col_asks", "Asks"), UiCellKind.Money, 72),
                WorkBuilder.Col(w.T("reallife_work_col_state", "State"), UiCellKind.Chip, 192) };
            t.EmptyTitle = w.T("reallife_work_no_applicants_title", "No applicants yet");
            t.EmptyBody = w.T("reallife_work_no_applicants", "Applicants arrive over a few days and other firms hire too. A boosted ad brings more.");
            t.EmptyIcon = UiIcon.People;
            int teamAverage = TeamAverage(d);
            for (int i = 0; i < h.Applicants.Count; i++)
            {
                Applicant a = h.Applicants[i];
                Seeker s = h.SeekerOf(a.Seeker);
                JobAd ad = h.AdOf(a.Ad);
                if (s == null || !a.Live || ad == null || ad.Search != false || (roleAds.Count > 0 && ad.Id != roleAds[roleIndex].Id))
                {
                    continue;
                }
                bool competing = HireOffers.Competing(a, day);
                bool found = (a.Revealed & HiringRules.RevealSkill) != 0;
                UiRole tone = competing ? UiRole.Warn : s.Notice > 0 ? UiRole.Muted : UiRole.Ok;
                string state = competing ? w.T("reallife_work_competing_offer", "Competing offer") : StageName(a);
                var r = new WorkRow { Id = a.Id, Person = s.P.Name, Name = s.P.Name, Ring = UiRole.Ok, Group = ad.Id, Status = state, Tone = tone,
                    Sub = string.IsNullOrEmpty(s.Employer) ? w.T("reallife_work_self_taught", "Self taught") : w.F("reallife_work_from_firm", "From {firm}", "firm", s.Employer),
                    Cells = new[] { null, s.CvMain.ToString(Inv), found ? (s.P.Main / 1000).ToString(Inv) : "?", TechText.Rate(s.Ask), state },
                        Values = new[] { 0, s.CvMain, found ? s.P.Main / 1000 : 0, 0, 0 },
                    Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, tone }, Open = Act(OpenApplicant, a.Id, string.Empty, UiButtonKind.Secondary, UiIcon.Person, true) };
                r.Title = s.P.Name;
                r.Sentence = TechText.Role(s.P.Role) + ", " + TechText.Level(s.P.Level) + ", " + w.F("reallife_career_row_rate", "{rate} an hour", "rate", TechText.Rate(s.Ask));
                var attrs = new WorkSection { Part = WorkPart.Bars, Title = w.T("reallife_work_attributes", "Attributes") };
                attrs.Bars.Add(new WorkBar { Label = w.T("reallife_work_skill_claimed", "Craft on the CV"), Value = s.CvMain });
                if (found)
                {
                    attrs.Bars.Add(new WorkBar { Label = w.T("reallife_work_skill_found", "Found at interview"), Value = Math.Min(100, s.P.Main / 1000) });
                }
                r.Sections.Add(attrs);
                var work = WorkSoFar(d, s, w);
                if (work != null)
                {
                    r.Sections.Add(work);
                }
                var fit = new WorkSection { Part = WorkPart.Checks, Title = w.T("reallife_work_fit_for_role", "Fit for the role") };
                fit.Checks.Add(new WorkNeed { Words = w.F("reallife_work_fit_craft", "Craft above your team's average of {n}", "n", teamAverage.ToString(Inv)), Met = s.CvMain > teamAverage });
                fit.Checks.Add(new WorkNeed { Words = w.F("reallife_work_fit_pay", "Asks less than the {pay} you set", "pay", TechText.Rate(ad.PayMost)), Met = s.Ask <= ad.PayMost });
                fit.Checks.Add(new WorkNeed { Words = w.T("reallife_work_fit_refs", "References checked"), Met = a.Refs });
                r.Sections.Add(fit);
                if (competing)
                {
                    r.Sections.Add(new WorkSection { Part = WorkPart.Note, Lead = w.T("reallife_work_competing_offer_lead", "Competing offer"), NoteIcon = UiIcon.Flag,
                        Text = w.F("reallife_work_rival_offer_line",
                            "{rival} offered {pay} an hour. They answer by {day}. Offer at least as much to hire them.", "rival", a.RivalBy, "pay", TechText.Rate(a.RivalBid),
                            "day", TechText.Day(a.RivalLapse)) });
                }
                int start = competing ? Math.Max(a.RivalBid, s.Ask) : s.Ask;
                int dollars = Math.Max(1, Math.Min(200, view.Get(0, start / 100)));
                if (t.Amounts.Count == 0)
                {
                    t.Amounts.Add(new WorkAmount { Id = 0, Value = dollars, Min = 1, Max = 200, Figure = "$" + dollars.ToString(Inv) + " " + w.T("reallife_work_an_hour", "an hour") });
                }
                r.Sections.Add(new WorkSection { Part = WorkPart.Amount, Title = w.T("reallife_work_your_offer", "Your offer"), Stepper = 0 });
                bool done = a.Stage >= (int)ApplyStage.Accepted;
                r.Acts.Add(Act(Offer, a.Id, w.F("reallife_work_hire_at", "Hire at {pay} an hour", "pay", "$" + dollars.ToString(Inv)), UiButtonKind.Primary, UiIcon.Check, false,
                    done ? w.T("reallife_work_already_hired", "They have already accepted.") : !HireOffers.Beats(a,
                        dollars * 100) ? w.F("reallife_work_must_beat", "Offer at least {pay} to beat the rival.", "pay", TechText.Rate(a.RivalBid)) : null));
                r.Acts.Add(a.Stage == (int)ApplyStage.Countered ? Act(Counter, a.Id, w.T("reallife_work_take_counter", "Take their counter"), UiButtonKind.Secondary, UiIcon.Check)
                    : Act(Shortlist, a.Id, w.T("reallife_hire_button_shortlist", "Shortlist"), UiButtonKind.Secondary, UiIcon.Star, false,
                        a.Stage == (int)ApplyStage.Shortlisted ? w.T("reallife_work_already_shortlisted", "Already on your shortlist.") : null));
                r.Acts.Add(Act(Walk, a.Id, w.T("reallife_work_walk_away", "Let them go"), UiButtonKind.Destructive, UiIcon.Close));
                t.Rows.Add(r);
            }
            return t;
        }

        // Past jobs the CV lists, made and kept the first time they are read (HireOffers.HistoryOf).
        private static WorkSection WorkSoFar(TechDirector d, Seeker s, UtilitiesWords w)
        {
            var past = new List<PastJob>(HireOffers.HistoryMost);
            if (HireOffers.Read(HireOffers.HistoryOf(d.TalentSeed, s), past) == 0)
            {
                return null;
            }
            var sec = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_so_far_title", "Work so far") };
            for (int k = 0; k < past.Count; k++)
            {
                sec.Pairs.Add(new KeyValuePair<string, string>(TechText.Role(s.P.Role) + ", " + TechText.Level(past[k].Level) + ", " + past[k].Firm,
                    past[k].Years.ToString(Inv) + " " + w.T("reallife_work_years", "years")));
            }
            return sec;
        }

        // ------------------------------------------------------------------------------------------------- an applicant's page

        private WorkTab ApplicantPage(TechDirector d, HiringFile h, int day, WorkView view)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Form, Sub = true, PersonTabs = new[] { w.T("reallife_work_person_file", "File"), w.T("reallife_work_person_career", "Career"),
                w.T("reallife_work_person_contract", "Contract") } };
            Applicant a = h.ApplicantOf(view.Pick);
            Seeker s = a != null ? h.SeekerOf(a.Seeker) : null;
            JobAd ad = a != null ? h.AdOf(a.Ad) : null;
            t.Acts.Add(Act(WorkKinds.Back, 0, w.T("reallife_work_back_to_applicants", "Back to the applicants"), UiButtonKind.Secondary, UiIcon.Back, true));
            if (a == null || s == null || ad == null)
            {
                t.Form = new WorkFormModel { Lead = w.T("reallife_work_applicant_gone", "This applicant has gone.") };
                return t;
            }
            bool competing = HireOffers.Competing(a, day);
            var form = new WorkFormModel { Head = new UiProfileModel { Name = s.P.Name, Hue = UiColour.HueOf(s.P.Name), Ring = UiRole.Ok,
                Role = TechText.Role(s.P.Role) + ", " + TechText.Level(s.P.Level),
                RoleIcon = RoleGlyph(s.P.Role), Employer = string.IsNullOrEmpty(s.Employer) ? null : s.Employer, EmployerIcon = UiIcon.Store,
                    Status = competing ? w.T("reallife_work_competing_offer", "Competing offer") : StageName(a),
                StatusTone = competing ? UiRole.Warn : UiRole.Ok, FactLabels = new[] { w.T("reallife_work_col_asks", "Asks"),
                    w.T("reallife_work_notice", "Notice"), w.T("reallife_work_your_top_pay", "Your top pay") },
                FactValues = new[] { TechText.Rate(s.Ask), w.F("reallife_work_n_days", "{n} days", "n", s.Notice.ToString(Inv)), TechText.Rate(ad.PayMost) } } };
            t.Form = form;
            if (view.PageTab == 0)
            {
                var attrs = new WorkFormBand { Title = w.T("reallife_work_attributes", "Attributes") };
                attrs.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = w.T("reallife_work_skill_claimed", "Craft on the CV"), Value = s.CvMain } });
                if ((a.Revealed & HiringRules.RevealSkill) != 0)
                {
                    attrs.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = w.T("reallife_work_skill_found",
                        "Found at interview"), Value = Math.Min(100, s.P.Main / 1000) } });
                }
                if ((a.Revealed & HiringRules.RevealFit) != 0)
                {
                    attrs.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = w.T("reallife_work_fit_settle", "Settling in"), Value = Math.Min(100, s.Fit / 10) } });
                }
                if (attrs.Rows.Count == 1)
                {
                    attrs.Note = w.T("reallife_work_not_interviewed", "An interview finds out their true skill, their experience and how they would settle.");
                }
                form.Left.Add(attrs);
                var steps = new WorkFormBand { Title = w.T("reallife_work_checks_title", "Checks") };
                steps.Rows.Add(new WorkFormRow { Field = WorkField.Check, Label = w.T("reallife_work_fit_refs", "References checked"), On = a.Refs });
                steps.Rows.Add(new WorkFormRow { Field = WorkField.Check, Label = w.T("reallife_work_background_checked", "Background checked"), On = a.Background });
                steps.Rows.Add(new WorkFormRow { Field = WorkField.Check, Label = w.T("reallife_work_interviewed", "Interviewed"), On = a.Interviewed });
                form.Right.Add(steps);
                var past = WorkSoFar(d, s, w);
                if (past != null)
                {
                    var band = new WorkFormBand { Title = w.T("reallife_work_so_far_title", "Work so far") };
                    for (int k = 0; k < past.Pairs.Count; k++)
                    {
                        band.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = past.Pairs[k].Key, Value = past.Pairs[k].Value });
                    }
                    form.Right.Add(band);
                }
                bool open = a.Stage < (int)ApplyStage.Accepted;
                if (open && !a.Interviewed)
                {
                    t.Acts.Add(Act(Interview, a.Id, w.T("reallife_hire_button_interview", "Interview"), UiButtonKind.Primary, UiIcon.Check));
                }
                if (open && !a.Refs)
                {
                    t.Acts.Add(Act(Refs, a.Id, w.T("reallife_hire_button_refs", "Ask for references"), UiButtonKind.Secondary, UiIcon.Search));
                }
                if (open && !a.Background)
                {
                    t.Acts.Add(Act(Background, a.Id, w.T("reallife_hire_button_background", "Background check"), UiButtonKind.Secondary, UiIcon.ShieldCheck));
                }
            }
            else if (view.PageTab == 1)
            {
                var career = new WorkFormBand { Title = w.T("reallife_work_person_career", "Career") };
                career.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_experience", "Experience"), Value = TechText.Years(s.P.YearsDays) });
                career.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_school", "Schooling"), Value = TechText.School(s.P.School) });
                career.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_certificates", "Certificates"), Value = TechText.Certs(s.P) });
                form.Left.Add(career);
            }
            else
            {
                int start = competing ? Math.Max(a.RivalBid, s.Ask) : s.Ask;
                int dollars = Math.Max(1, Math.Min(200, view.Get(0, start / 100)));
                t.Amounts.Add(new WorkAmount { Id = 0, Value = dollars, Min = 1, Max = 200, Figure = "$" + dollars.ToString(Inv) + " " + w.T("reallife_work_an_hour", "an hour") });
                var contract = new WorkFormBand { Title = w.T("reallife_work_person_contract", "Contract") };
                contract.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_your_offer", "Your offer"), Stepper = 0 });
                contract.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_col_asks", "Asks"), Value = TechText.Rate(s.Ask) });
                if (competing)
                {
                    contract.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_rival_bid", "Rival's offer"), Value = a.RivalBy + ", " + TechText.Rate(a.RivalBid) });
                }
                bool done = a.Stage >= (int)ApplyStage.Accepted;
                contract.Foot = Act(Offer, a.Id, w.F("reallife_work_hire_at", "Hire at {pay} an hour", "pay", "$" + dollars.ToString(Inv)), UiButtonKind.Primary, UiIcon.Check, false,
                    done ? w.T("reallife_work_already_hired", "They have already accepted.") : !HireOffers.Beats(a,
                        dollars * 100) ? w.F("reallife_work_must_beat", "Offer at least {pay} to beat the rival.", "pay", TechText.Rate(a.RivalBid)) : null);
                form.Left.Add(contract);
                var answer = new WorkFormBand { Title = w.T("reallife_work_their_answer", "Their answer") };
                if (a.Stage == (int)ApplyStage.Countered)
                {
                    answer.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_they_came_back", "They came back with"), Value = TechText.Rate(a.Counter) });
                    answer.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = Act(Counter, a.Id,
                        w.T("reallife_work_take_counter", "Take their counter"), UiButtonKind.Primary, UiIcon.Check) });
                }
                answer.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = w.T("reallife_work_shortlist_label", "Shortlist"),
                    Act = Act(Shortlist, a.Id, w.T("reallife_hire_button_shortlist", "Shortlist"),
                    UiButtonKind.Secondary, UiIcon.Star, false, a.Stage == (int)ApplyStage.Shortlisted ? w.T("reallife_work_already_shortlisted", "Already on your shortlist.") : null) });
                answer.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = w.T("reallife_work_reject_label", "Turn them down"),
                    Act = Act(Walk, a.Id, w.T("reallife_work_walk_away", "Let them go"),
                    UiButtonKind.Destructive, UiIcon.Close) });
                if (a.At == ApplyStage.Accepted)
                {
                    string buyout = d.BuyoutRefusal(a, s);
                    answer.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = w.T("reallife_work_buyout_label", "Buy out their notice"),
                        Act = Act(Buyout, a.Id, w.T("reallife_hire_button_buyout", "Buy out their notice"),
                        UiButtonKind.Secondary, UiIcon.Coin, false, buyout == null ? null : TechText.T(buyout, "name", s.P.Name)) });
                }
                form.Right.Add(answer);
            }
            return t;
        }

        // ------------------------------------------------------------------------------------------------- Market

        private WorkTab Market(TechDirector d, HiringFile h, int day)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Table };
            t.Columns = new[] { WorkBuilder.Col(w.T("reallife_work_col_line", "Line"), UiCellKind.Tile), WorkBuilder.Col(w.T("reallife_work_col_looking", "Looking"), UiCellKind.Figure, 80, true),
                WorkBuilder.Col(w.T("reallife_work_level_junior", "Junior"), UiCellKind.Figure, 72), WorkBuilder.Col(w.T("reallife_work_level_mid", "Mid"), UiCellKind.Figure, 72),
                WorkBuilder.Col(w.T("reallife_work_level_senior", "Senior"), UiCellKind.Figure, 80), WorkBuilder.Col(w.T("reallife_work_col_rival_ads", "Rival ads"), UiCellKind.Figure, 88),
                WorkBuilder.Col(w.T("reallife_work_col_going_rate", "Going rate"), UiCellKind.Money, 96) };
            t.EmptyTitle = w.T("reallife_work_market_empty_title", "Nobody is on the market");
            t.EmptyBody = w.T("reallife_work_market_lead", "Professionals looking for work show here by line. Post an ad and the ones who fit apply.");
            var levels = new int[TalentCatalogue.Levels];
            var lines = new List<int[]>((int)TFamily.Count);   // family, shortage, looking, going rate: what the foot picks its two lists from
            for (int fam = 0; fam < (int)TFamily.Count; fam++)
            {
                Array.Clear(levels, 0, levels.Length);
                int seekers = 0, openings = 0, yours = 0;
                for (int i = 0; i < h.Seekers.Count; i++)
                {
                    Seeker s = h.Seekers[i];
                    if (!s.Taken && !s.Passive && (int)s.P.Def.Family == fam)
                    {
                        seekers++;
                        levels[s.P.Level]++;
                    }
                }
                for (int i = 0; i < h.RivalAds.Count; i++)
                {
                    openings += h.RivalAds[i].Family == fam && h.RivalAds[i].To > day ? 1 : 0;
                }
                for (int i = 0; i < h.Ads.Count; i++)
                {
                    yours += !h.Ads[i].Search && h.Ads[i].Open(day) && (int)TalentCatalogue.Role(h.Ads[i].Role).Family == fam ? 1 : 0;
                }
                int first = HiringRules.RoleOf((TFamily)fam, 0);
                int going = first >= 0 ? d.GoingFor(first, TalentCatalogue.Mid) : 0;
                int shortage = d.ShortageOf((TFamily)fam);
                int seniors = levels[2] + levels[3] + levels[4];
                var r = new WorkRow { Id = fam, Icon = FamilyGlyph((TFamily)fam), Name = TechText.FamilyName((TFamily)fam),
                    Sub = w.F("reallife_work_your_ads_n", "{n} of your ads open", "n", yours.ToString(Inv)),
                    Cells = new[] { null, seekers.ToString(Inv), levels[0].ToString(Inv), levels[1].ToString(Inv), seniors.ToString(Inv),
                        openings.ToString(Inv), going > 0 ? TechText.Rate(going) : WorkBuilder.None(w) },
                    Values = new[] { 0, Math.Min(100, seekers * 6), 0, 0, 0, 0, 0 } };
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_looking", "Looking"), seekers.ToString(Inv)));
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_going_rate", "Going rate"), going > 0 ? TechText.Rate(going) : WorkBuilder.None(w)));
                var facts = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_the_line", "The line") };
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_shortage", "Shortage"), TechText.Mille(shortage)));
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_rival_ads", "Rival ads"), openings.ToString(Inv)));
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_your_ads", "Your open ads"), yours.ToString(Inv)));
                r.Sections.Add(facts);
                r.Sections.Add(new WorkSection { Part = WorkPart.Note, Lead = w.T("reallife_work_market_how_lead", "How to hire from here."), NoteIcon = UiIcon.Info,
                    Text = w.T("reallife_work_market_how", "Post an ad for a role in this line and the seekers who fit apply over a few days. A shortage makes them ask more.") });
                r.Acts.Add(Act(NewAd, 0, w.T("reallife_work_new_ad", "New ad"), UiButtonKind.Primary, UiIcon.Plus, true));
                t.Rows.Add(r);
                lines.Add(new[] { fam, shortage, seekers, going });
            }
            MarketFoot(t, lines, w);
            return t;
        }

        // The Market's foot: the lines that are hardest to hire in (the biggest shortage) and the lines with the most people looking.
        private static void MarketFoot(WorkTab t, List<int[]> lines, UtilitiesWords w)
        {
            var byShortage = new List<int[]>(lines);
            byShortage.Sort((a, b) => b[1].CompareTo(a[1]));
            var byLooking = new List<int[]>(lines);
            byLooking.Sort((a, b) => b[2].CompareTo(a[2]));
            MarketList(t, w.T("reallife_work_band_hardest", "Hardest to hire in"), byShortage, w, true);
            MarketList(t, w.T("reallife_work_band_most_looking", "Most people looking"), byLooking, w, false);
        }

        private static void MarketList(WorkTab t, string title, List<int[]> sorted, UtilitiesWords w, bool byShortage)
        {
            int n = Math.Min(3, sorted.Count);
            var icons = new UiIcon[n];
            var names = new string[n];
            var hints = new string[n];
            var values = new string[n];
            for (int i = 0; i < n; i++)
            {
                icons[i] = FamilyGlyph((TFamily)sorted[i][0]);
                names[i] = TechText.FamilyName((TFamily)sorted[i][0]);
                hints[i] = byShortage ? w.F("reallife_work_shortage_n", "Shortage {n}", "n", TechText.Mille(sorted[i][1])) : w.F("reallife_work_looking_n",
                    "{n} looking", "n", sorted[i][2].ToString(Inv));
                values[i] = sorted[i][3] > 0 ? TechText.Rate(sorted[i][3]) : WorkBuilder.None(w);
            }
            WorkBands.AddIfAny(t.Below, WorkBands.Tiles(title, icons, names, hints, values));
        }

        private static UiIcon FamilyGlyph(TFamily f)
        {
            switch (f)
            {
                case TFamily.Engineering: return UiIcon.Code;
                case TFamily.Data: return UiIcon.Database;
                case TFamily.Security: return UiIcon.ShieldCheck;
                case TFamily.Infrastructure: return UiIcon.HardDrives;
                case TFamily.Product: return UiIcon.Layout;
                case TFamily.Design: return UiIcon.PaintBrush;
                case TFamily.Commercial: return UiIcon.Handshake;
                case TFamily.Support: return UiIcon.Headset;
                case TFamily.Corporate: return UiIcon.Building;
                case TFamily.Built: return UiIcon.Hammer;
                default: return UiIcon.People;
            }
        }

        // ------------------------------------------------------------------------------------------------- Search

        private WorkTab Search(TechDirector d, HiringFile h, int day, WorkView view)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Form };
            var form = new WorkFormModel { Lead = w.T("reallife_work_search_lead",
                "A search firm finds senior people, and a crew search finds technicians for a data centre. Its fee is a share of a year's pay.") };
            t.Form = form;
            TalentGame.Businesses(_places);
            if (_places.Count == 0)
            {
                form.Lead = TechText.T("reallife_hire_new_no_business");
                return t;
            }
            int roleAt = Math.Max(0, Math.Min(TalentCatalogue.RoleCount - 1, view.Get(0, TalentCatalogue.SoftwareEngineer)));
            RoleDef r = TalentCatalogue.Role(roleAt);
            int least = Math.Max(r.LeastLevel, HiringCatalogue.SearchLeastLevel);
            if (r.MostLevel < least)
            {
                roleAt = TalentCatalogue.SoftwareEngineer;
                r = TalentCatalogue.Role(roleAt);
                least = HiringCatalogue.SearchLeastLevel;
            }
            int level = Math.Max(least, Math.Min(r.MostLevel, least + view.Get(1, 0)));
            int place = Math.Max(0, Math.Min(_places.Count - 1, view.Get(2, 0)));
            var names = new string[_places.Count];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = TalentGame.NameOf(_places[i]);
            }
            var roleNames = new string[TalentCatalogue.RoleCount];
            for (int i = 0; i < roleNames.Length; i++)
            {
                roleNames[i] = TechText.Role(i);
            }
            var levelNames = new string[r.MostLevel - least + 1];
            for (int i = 0; i < levelNames.Length; i++)
            {
                levelNames[i] = TechText.Level(least + i);
            }
            t.Amounts.Add(Choice(0, roleAt, roleNames));
            t.Amounts.Add(Choice(1, level - least, levelNames));
            t.Amounts.Add(Choice(2, place, names));
            d.SearchQuote(r.Id, level, out int fee, out int retainer);
            var brief = new WorkFormBand { Title = w.T("reallife_work_brief_title", "Brief a search firm") };
            brief.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_ad_place", "At"), Stepper = 2 });
            brief.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_col_role", "Role"), Stepper = 0 });
            brief.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_col_level", "Level"), Stepper = 1 });
            brief.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_col_fee", "Fee"),
                Value = TechText.Cents(fee) + ", " + w.F("reallife_work_up_front", "{n} up front", "n", TechText.Cents(retainer)) });
            bool seats = HiringRules.Seats(TalentGame.Shifts(_places[place]), r);
            brief.Foot = Act(Brief, 0, w.T("reallife_hire_button_brief", "Brief the firm"), UiButtonKind.Primary, UiIcon.Search, false,
                !seats ? TechText.T(TechDirector.SeatRefusal) : r.MostLevel < HiringCatalogue.SearchLeastLevel ? w.T("reallife_work_search_level", "A search finds senior people only.") : null);
            form.Left.Add(brief);
            // the data centre's crew
            string crewWhy = d.CrewRefusal(TalentGame.Place(_places[place]));
            int crew = Math.Max(1, Math.Min(HiringCatalogue.CrewMost, view.Get(3, 4)));
            t.Amounts.Add(new WorkAmount { Id = 3, Value = crew, Min = 1, Max = HiringCatalogue.CrewMost, Figure = crew.ToString(Inv) });
            d.CrewQuote(TalentCatalogue.Technician, TalentCatalogue.Mid, crew, out int crewFee, out int crewRetainer, out int each);
            var crewBand = new WorkFormBand { Title = w.T("reallife_work_crew_title", "Data centre crew") };
            crewBand.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_crew_technicians", "Technicians"), Stepper = t.Amounts.Count - 1 });
            crewBand.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_col_fee", "Fee"),
                Value = TechText.Cents(crewFee) + ", " + w.F("reallife_work_each_n", "{n} each", "n", TechText.Cents(each)) });
            crewBand.Foot = Act(BriefCrew, 0, w.T("reallife_hire_button_brief_crew", "Brief a crew search"), UiButtonKind.Secondary,
                UiIcon.HardDrives, false, crewWhy == null ? null : w.T(crewWhy, "A crew search needs a data centre and headquarters."));
            form.Left.Add(crewBand);
            var running = new WorkFormBand { Title = w.T("reallife_work_running_searches", "Searches running") };
            for (int i = 0; i < h.Ads.Count; i++)
            {
                JobAd ad = h.Ads[i];
                if (ad.Search)
                {
                    running.Rows.Add(new WorkFormRow { Field = WorkField.Tile,
                        Label = TechText.Role(ad.Role) + (ad.CrewSearch ? " x" + ad.Crew.ToString(Inv) : string.Empty) + ", " + TechText.Level(ad.Level),
                        Hint = w.F("reallife_work_fee_n", "Fee {n}", "n", TechText.Money(ad.Fee / 100)), Value = TechText.Day(ad.Due), Icon = UiIcon.Search });
                }
            }
            running.Count = running.Rows.Count.ToString(Inv);
            if (running.Rows.Count == 0)
            {
                running.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = w.T("reallife_work_no_searches", "No search is running. A brief brings three candidates in a few days.") });
            }
            form.Right.Add(running);
            var seekNames = new List<string>(3);
            var seekRoles = new List<string>(3);
            var seekAsks = new List<string>(3);
            for (int i = 0; i < h.Seekers.Count && seekNames.Count < 3; i++)
            {
                Seeker s = h.Seekers[i];
                if (!s.Taken && !s.Passive && s.P.Def.Family == r.Family && s.P.Level >= least)
                {
                    seekNames.Add(s.P.Name);
                    seekRoles.Add(TechText.Role(s.P.Role) + ", " + TechText.Level(s.P.Level));
                    seekAsks.Add(TechText.Rate(s.Ask));
                }
            }
            WorkBands.AddIfAny(form.Below, WorkBands.People(w.T("reallife_work_band_on_market", "On the market now"), seekNames.ToArray(), seekRoles.ToArray(), seekAsks.ToArray()));
            var office = new WorkFormBand { Title = w.T("reallife_work_office_campaigns", "Recruitment office") };
            for (int i = 0; i < h.Campaigns.Count; i++)
            {
                OfficeCampaign c = h.Campaigns[i];
                office.Rows.Add(new WorkFormRow { Field = WorkField.Tile, Label = c.Skill,
                    Hint = c.Agency + ", " + c.Delivered.ToString(Inv) + " / " + c.Planned.ToString(Inv), Value = TechText.Day(c.To), Icon = UiIcon.Megaphone });
            }
            if (office.Rows.Count > 0)
            {
                office.Count = office.Rows.Count.ToString(Inv);
                form.Right.Add(office);
            }
            return t;
        }

        // ------------------------------------------------------------------------------------------------- Team

        private WorkTab Team(TechDirector d, int day)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Table, Search = w.T("reallife_work_search_people", "Search people") };
            t.Columns = new[] { WorkBuilder.Col(w.T("reallife_work_col_name", "Name"), UiCellKind.Person), WorkBuilder.Col(w.T("reallife_work_col_role", "Role"), UiCellKind.Text, 232),
                WorkBuilder.Col(w.T("reallife_work_skill_craft", "Craft"), UiCellKind.Meter, 120), WorkBuilder.Col(w.T("reallife_work_col_pay", "Pay"), UiCellKind.Money, 80),
                WorkBuilder.Col(w.T("reallife_work_col_morale", "Morale"), UiCellKind.Meter, 112) };
            t.EmptyTitle = w.T("reallife_work_no_team_title", "Nobody is on your payroll");
            t.EmptyBody = w.T("reallife_work_no_team", "Hire from the applicants and they join here, each with a page for their file, career and contract.");
            t.EmptyIcon = UiIcon.People;
            int year = GameCalendar.DaysPerYear;
            var gapNames = new List<string>(3);
            var gapRoles = new List<string>(3);
            var gapRights = new List<string>(3);
            var wornNames = new List<string>(3);
            var wornRoles = new List<string>(3);
            var wornRights = new List<string>(3);
            long payroll = 0;
            int moraleSum = 0, leaving = 0;
            for (int i = 0; i < d.People.Count && i < 60; i++)
            {
                Person p = d.People[i];
                Profile f = p.File;
                int paid = p.Game != null ? TalentGame.PaidCents(p.Game) : f.GameWage;
                payroll += paid;
                moraleSum += f.Morale / 10;
                leaving += f.NoticeEnd > 0 ? 1 : 0;
                int goingPay = p.Game != null ? PaidWage.Cents(f.Going, Prices.Level, 0) : f.Going;
                if (goingPay - paid >= GapMost)
                {
                    gapNames.Add(p.Name);
                    gapRoles.Add(w.F("reallife_work_paid_going", "Paid {paid}, going {going}", "paid", TechText.Rate(paid), "going", TechText.Rate(goingPay)));
                    gapRights.Add("-" + TechText.Rate(goingPay - paid));
                }
                if (f.Burnout / 10 >= WornFrom)
                {
                    wornNames.Add(p.Name);
                    wornRoles.Add(TechText.Role(f.Role) + ", " + TechText.Level(f.Level));
                    wornRights.Add(w.T("reallife_work_burnout", "Burnout") + " " + (f.Burnout / 10).ToString(Inv));
                }
                string state = TalentRules.OfferOpen(f, day) ? TechText.T("reallife_roles_state_offer") : f.NoticeEnd > 0 ? TechText.T("reallife_roles_state_leaving", "day", f.NoticeEnd.ToString())
                    : f.RaiseAsk > 0 ? TechText.T("reallife_roles_state_asks", "percent", TechText.Bp(f.RaiseAsk)) : null;
                var r = new WorkRow { Id = i, Person = p.Name, Name = p.Name, Sub = p.Business, Ring = state != null ? UiRole.Warn : UiRole.Ok,
                    Cells = new[] { null, TechText.Role(f.Role) + ", " + TechText.Level(f.Level), Math.Min(100, p.MainPoints).ToString(Inv), TechText.Rate(paid), (f.Morale / 10).ToString(Inv) },
                    Values = new[] { 0, 0, Math.Min(100, p.MainPoints), 0, Math.Max(0, Math.Min(100, f.Morale / 10)) }, Open = Act(OpenPerson, i,
                        string.Empty, UiButtonKind.Secondary, UiIcon.Person, true) };
                r.Title = p.Name;
                r.Sentence = state ?? TechText.Role(f.Role) + ", " + p.Business;
                // what the row does not say: what the person is worth on the market, when they were last looked at, and how worn they are
                var now = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_now", "Now") };
                now.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_going_pay", "Going pay"), TechText.Rate(goingPay)));
                now.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_last_review", "Last review"),
                    f.Rating > 0 ? f.Rating.ToString(Inv) + " / 5, " + TechText.Day(f.ReviewDay) : WorkBuilder.NotYet(w)));
                now.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_hired_on", "Joined"), TechText.Day(f.Joined)));
                r.Sections.Add(now);
                var worn = new WorkSection { Part = WorkPart.Bars, Title = w.T("reallife_work_skills_word", "Skills and wear") };
                worn.Bars.Add(new WorkBar { Label = TechText.Skill(f.Def.SkillA), Value = Math.Min(100, f.A / 1000) });
                worn.Bars.Add(new WorkBar { Label = TechText.Skill(f.Def.SkillB), Value = Math.Min(100, f.B / 1000) });
                worn.Bars.Add(new WorkBar { Label = w.T("reallife_work_burnout", "Burnout"), Value = Math.Max(0, Math.Min(100, f.Burnout / 10)) });
                r.Sections.Add(worn);
                r.Acts.Add(Act(OpenPerson, i, w.T("reallife_work_open_page", "Open their page"), UiButtonKind.Primary, UiIcon.Person, true));
                t.Rows.Add(r);
            }
            if (d.People.Count > 0)
            {
                t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_payroll", "Payroll an hour"), TechText.Money((int)(payroll / 100))));
                t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_morale", "Average morale"), (moraleSum / d.People.Count).ToString(Inv)));
                t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_leaving", "Leaving"), leaving.ToString(Inv)));
            }
            WorkBands.AddIfAny(t.Below, WorkBands.People(w.T("reallife_work_band_under_going", "Paid under the going rate"), gapNames.ToArray(), gapRoles.ToArray(), gapRights.ToArray(), 2));
            WorkBands.AddIfAny(t.Below, WorkBands.People(w.T("reallife_work_band_wearing", "Wearing thin"), wornNames.ToArray(), wornRoles.ToArray(), wornRights.ToArray(), 2));
            return t;
        }

        private const int GapMost = 200, WornFrom = 20;   // cents an hour a person may be paid under the going pay before it is shown, and the burnout from which they are wearing thin

        // ------------------------------------------------------------------------------------------------- a team person's page

        private WorkTab PersonPage(TechDirector d, int day, WorkView view)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Form, Sub = true, PersonTabs = new[] { w.T("reallife_work_person_file", "File"), w.T("reallife_work_person_career", "Career"),
                w.T("reallife_work_person_contract", "Contract") } };
            t.Acts.Add(Act(WorkKinds.Back, 0, w.T("reallife_work_back_to_team", "Back to the team"), UiButtonKind.Secondary, UiIcon.Back, true));
            if (view.Pick < 0 || view.Pick >= d.People.Count)
            {
                t.Form = new WorkFormModel { Lead = w.T("reallife_work_person_gone", "This person is no longer on your payroll.") };
                return t;
            }
            Person person = d.People[view.Pick];
            Profile p = person.File;
            RoleDef r = p.Def;
            int year = GameCalendar.DaysPerYear;
            int paid = person.Game != null ? TalentGame.PaidCents(person.Game) : p.GameWage;
            bool offer = TalentRules.OfferOpen(p, day);
            var form = new WorkFormModel { Head = new UiProfileModel { Name = person.Name, Hue = UiColour.HueOf(person.Name),
                Ring = p.Morale < 400 ? UiRole.Warn : UiRole.Ok, Role = TechText.Role(p.Role) + ", " + TechText.Level(p.Level),
                RoleIcon = RoleGlyph(p.Role), Employer = person.Business, EmployerIcon = UiIcon.Store,
                Status = p.NoticeEnd > 0 ? TechText.T("reallife_roles_state_leaving", "day",
                    p.NoticeEnd.ToString()) : offer ? TechText.T("reallife_roles_state_offer") : p.RaiseAsk > 0 ? TechText.T("reallife_roles_state_asks",
                    "percent", TechText.Bp(p.RaiseAsk)) : TechText.T("reallife_roles_state_fine"),
                StatusTone = p.NoticeEnd > 0 || offer || p.RaiseAsk > 0 ? UiRole.Warn : UiRole.Ok, FactLabels = new[] { w.T("reallife_work_col_pay",
                    "Pay"), w.T("reallife_work_hired_on", "Joined"), w.T("reallife_work_col_morale", "Morale") },
                FactValues = new[] { TechText.Rate(paid), TechText.Day(p.Joined), TechText.Mille(p.Morale) } } };
            t.Form = form;
            string key = p.Id;
            var mates = new List<string>(3);
            var mateRoles = new List<string>(3);
            var matePays = new List<string>(3);
            for (int i = 0; i < d.People.Count && mates.Count < 3; i++)
            {
                Person m = d.People[i];
                if (i != view.Pick && m.Business == person.Business)
                {
                    mates.Add(m.Name);
                    mateRoles.Add(TechText.Role(m.File.Role) + ", " + TechText.Level(m.File.Level));
                    matePays.Add(TechText.Rate(m.Game != null ? TalentGame.PaidCents(m.Game) : m.File.GameWage));
                }
            }
            WorkBands.AddIfAny(form.Below, WorkBands.People(w.T("reallife_work_band_same_team", "On the same team"), mates.ToArray(), mateRoles.ToArray(), matePays.ToArray()));
            if (view.PageTab == 0)
            {
                var attrs = new WorkFormBand { Title = w.T("reallife_work_attributes", "Attributes") };
                attrs.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = TechText.Skill(r.SkillA), Value = Math.Min(100, p.A / 1000) } });
                attrs.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = TechText.Skill(r.SkillB), Value = Math.Min(100, p.B / 1000) } });
                attrs.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = w.T("reallife_work_skill_craft", "Craft"), Value = Math.Min(100, person.MainPoints) } });
                attrs.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = w.T("reallife_work_col_morale", "Morale"), Value = Math.Max(0, Math.Min(100, p.Morale / 10)) } });
                attrs.Rows.Add(new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = w.T("reallife_work_burnout", "Burnout"), Value = Math.Max(0, Math.Min(100, p.Burnout / 10)) } });
                form.Left.Add(attrs);
                var now = new WorkFormBand { Title = w.T("reallife_work_now", "Now") };
                if (offer)
                {
                    now.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = TechText.Fill("reallife_roles_file_offer", "rival", p.PoachBy,
                        "percent", TechText.Bp(p.PoachBp), "day", p.PoachDay.ToString()) });
                    var answer = new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = Act(Match, 0,
                        w.T("reallife_roles_button_match", "Match it"), UiButtonKind.Primary, UiIcon.Check) };
                    answer.More.Add(Act(LetThem, 0, w.T("reallife_roles_button_let_them", "Let them go"), UiButtonKind.Destructive, UiIcon.Close));
                    now.Rows.Add(answer);
                }
                else if (p.RaiseAsk > 0)
                {
                    now.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = TechText.Fill("reallife_roles_file_asks", "percent", TechText.Bp(p.RaiseAsk), "day", p.AskEnd.ToString()) });
                    var answer = new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = Act(GiveRise, p.RaiseAsk,
                        w.T("reallife_roles_button_give", "Give the rise"), UiButtonKind.Primary, UiIcon.Check) };
                    answer.More.Add(Act(RefuseRise, 0, w.T("reallife_roles_button_refuse", "Refuse"), UiButtonKind.Secondary, UiIcon.Close));
                    now.Rows.Add(answer);
                }
                now.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_going_pay", "Going pay"), Value = TechText.Rate(p.Going) });
                now.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_last_review", "Last review"),
                    Value = p.Rating > 0 ? p.Rating.ToString(Inv) + " / 5, " + TechText.Day(p.ReviewDay) : WorkBuilder.NotYet(w) });
                form.Right.Add(now);
            }
            else if (view.PageTab == 1)
            {
                bool promote = TalentRules.CanPromote(p, day, year, person.MainPoints);
                var career = new WorkFormBand { Title = w.T("reallife_work_person_career", "Career") };
                career.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_col_role", "Role"), Value = TechText.Role(p.Role) + ", " + TechText.Level(p.Level) });
                career.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_experience", "Experience"),
                    Value = TechText.Years(p.YearsDays + (day - p.Joined) * 365 / Math.Max(60, year)) });
                career.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_school", "Schooling"), Value = TechText.School(p.School) });
                career.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_certificates", "Certificates"), Value = TechText.Certs(p) });
                career.Rows.Add(new WorkFormRow { Field = WorkField.Check, Label = promote ? TechText.Fill("reallife_roles_career_promote", "level",
                    TechText.Level(p.Level + 1), "percent", TechText.Bp(TalentCatalogue.PromotePayBp))
                    : TechText.Fill("reallife_roles_career_no_promote", "rating", TalentCatalogue.PromoteRating.ToString(), "least",
                        TechText.Percent(p.Level + 1 < TalentCatalogue.Levels ? TalentCatalogue.LevelSkillLeast[p.Level + 1] - 5 : 100)), On = promote });
                bool canRise = TalentRules.CanRise(p, day);
                string resting = canRise ? null : TechText.T("reallife_wage_rested_line", "day", WageRules.NextDay(p.RecognisedDay).ToString());
                var rises = new WorkFormRow { Field = WorkField.Button, Label = w.T("reallife_work_rises_label", "Pay"), Act = Act(Promote, 0,
                    w.T("reallife_roles_button_promote", "Promote"), UiButtonKind.Primary, UiIcon.TrendUp, false,
                    !canRise ? resting : promote ? null : w.T("reallife_work_not_ready_promote", "They are not ready for a step up.")) };
                rises.More.Add(Act(GiveRise, 300, w.T("reallife_roles_button_rise_3", "Rise of 3%"), UiButtonKind.Secondary, UiIcon.Plus, false, resting));
                rises.More.Add(Act(GiveRise, 800, w.T("reallife_roles_button_rise_8", "Rise of 8%"), UiButtonKind.Secondary, UiIcon.Plus, false, resting));
                career.Rows.Add(rises);
                form.Left.Add(career);
                var learn = new WorkFormBand { Title = w.T("reallife_work_learning", "Learning") };
                if (p.OnCourse(day))
                {
                    learn.Rows.Add(new WorkFormRow { Field = WorkField.Text,
                        Value = TechText.T(p.CourseSkill == 2 ? "reallife_roles_career_on_degree" : p.Exam >= 0 ? "reallife_roles_career_on_exam" : "reallife_roles_career_on_course",
                        "day", p.CourseEnd.ToString()) });
                }
                else
                {
                    int share = TechBridges.CourseShare();
                    int feeA = (int)((long)Prices.At(TalentRules.CourseFee(p.A / 1000)) * share / 10000), feeB = (int)((long)Prices.At(TalentRules.CourseFee(p.B / 1000)) * share / 10000);
                    var courses = new WorkFormRow { Field = WorkField.Button, Label = w.T("reallife_work_courses_label", "A course"),
                        Act = Act(CourseA, 0, TechText.Skill(r.SkillA) + ", " + TechText.Money(feeA), UiButtonKind.Secondary, UiIcon.Books) };
                    courses.More.Add(Act(CourseB, 0, TechText.Skill(r.SkillB) + ", " + TechText.Money(feeB), UiButtonKind.Secondary, UiIcon.Books));
                    learn.Rows.Add(courses);
                    if (TechBridges.DegreeFee != null && TechBridges.DegreeFee() > 0)
                    {
                        learn.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = w.T("reallife_work_degree_label", "A degree"),
                            Act = Act(Degree, 0, w.F("reallife_work_degree_fee", "Sponsor a degree, {fee}", "fee", TechText.Money(TechBridges.DegreeFee())),
                            UiButtonKind.Secondary, UiIcon.Cap) });
                    }
                    var certs = new List<int>(4);
                    for (int c = 0; c < TalentCatalogue.CertCount; c++)
                    {
                        if (!p.Has(c) && p.ScoreOf(TalentCatalogue.Certs[c].Skill) >= 0)
                        {
                            certs.Add(c);
                        }
                    }
                    if (certs.Count > 0)
                    {
                        var names = new string[certs.Count];
                        for (int i = 0; i < names.Length; i++)
                        {
                            names[i] = TechText.Cert(certs[i]);
                        }
                        int at = Math.Max(0, Math.Min(certs.Count - 1, view.Get(0, 0)));
                        t.Amounts.Add(Choice(0, at, names));
                        int chance = TalentRules.ExamChance(p, certs[at]);
                        learn.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_exam_label", "A certificate"), Stepper = 0 });
                        learn.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = chance > 0 ? w.F("reallife_work_exam_chance", "{chance} chance, {fee}", "chance", TechText.Percent(chance),
                            "fee", TechText.Money(Prices.At(TalentCatalogue.Certs[certs[at]].Fee))) : w.F("reallife_work_exam_needs",
                                "Needs {n} in the skill", "n", TechText.Percent(TalentCatalogue.Certs[certs[at]].Needs)),
                            Act = Act(Exam, certs[at], w.T("reallife_roles_button_exam", "Sit the exam"), UiButtonKind.Secondary, UiIcon.Scales,
                                false, chance > 0 ? null : w.T("reallife_work_exam_short", "Not skilled enough yet.")) });
                    }
                }
                form.Right.Add(learn);
            }
            else
            {
                int works = p.Def.Remote && person.Game == null ? 3 : 2;
                var workNames = new string[works];
                for (int i = 0; i < works; i++)
                {
                    workNames[i] = TechText.Work(i);
                }
                int work = Math.Max(0, Math.Min(works - 1, view.Get(0, p.Work)));
                t.Amounts.Add(Choice(0, work, workNames));
                var contract = new WorkFormBand { Title = w.T("reallife_work_person_contract", "Contract") };
                contract.Rows.Add(new WorkFormRow { Field = WorkField.Stepper, Label = w.T("reallife_work_ad_arrangement", "Arrangement"), Stepper = 0 });
                contract.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_they_want", "They want"), Value = TechText.Work((int)TalentRules.Wants(p, d.TalentSeed)) });
                bool probation = TalentRules.OnProbation(p, day, year);
                long severance = TalentRules.Severance(p, day, year, p.GameWage * TalentCatalogue.WeekHours, LeaveKind.LaidOff);
                contract.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_notice", "Notice"),
                    Value = w.F("reallife_work_n_days", "{n} days", "n", TalentRules.NoticeDays(p, day,
                    year).ToString(Inv)) + (probation ? ", " + w.T("reallife_work_probation", "on probation") : string.Empty) });
                contract.Rows.Add(new WorkFormRow { Field = WorkField.Label, Label = w.T("reallife_work_severance", "Severance"), Value = TechText.Cents(severance) });
                contract.Foot = Act(SetWork, 0, w.T("reallife_work_set_arrangement", "Set this arrangement"), UiButtonKind.Primary, UiIcon.Check,
                    false, work == p.Work ? w.T("reallife_work_already_set", "It is set so.") : null);
                form.Left.Add(contract);
                var terms = new WorkFormBand { Title = w.T("reallife_work_terms_title", "Terms and leaving") };
                terms.Rows.Add(new WorkFormRow { Field = WorkField.Text,
                    Value = p.NonCompete ? TechText.T("reallife_roles_contract_clause_on") : TechText.T("reallife_roles_contract_clause_off", "percent", TechText.Bp(TalentCatalogue.NonCompeteBp)) });
                terms.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = string.Empty,
                    Act = Act(Clause, 0, w.T("reallife_roles_button_clause", "Add a restraint clause"), UiButtonKind.Secondary, UiIcon.Lock, false,
                        p.NonCompete ? w.T("reallife_work_clause_on", "The clause is in their contract.") : null) });
                terms.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = w.T("reallife_work_lay_off_label", "Lay them off"),
                    Act = Act(LayOff, 0, w.T("reallife_roles_button_lay_off", "Lay off"), UiButtonKind.Destructive, UiIcon.SignOut, false,
                    p.NoticeEnd == 0 ? null : w.T("reallife_work_already_leaving", "They are already leaving.")) });
                if (TalentRules.FairDismissal(p, day, year))
                {
                    terms.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = w.T("reallife_work_dismiss_label", "Dismiss them"),
                        Act = Act(Dismiss, 0, w.T("reallife_roles_button_dismiss", "Dismiss now"), UiButtonKind.Destructive, UiIcon.SignOut,
                        false, p.NoticeEnd == 0 ? null : w.T("reallife_work_already_leaving", "They are already leaving.")) });
                }
                form.Right.Add(terms);
            }
            return t;
        }

        // ------------------------------------------------------------------------------------------------- doing

        private bool Carry(WorkDo act, TechDirector d, HiringFile h, WorkView view)
        {
            Applicant a = h.ApplicantOf(act.Arg);
            Person person = view.Sub == 2 && view.Pick >= 0 && view.Pick < d.People.Count ? d.People[view.Pick] : null;
            string key = person != null ? person.File.Id : null;
            switch (act.Kind)
            {
                case Shortlist: d.Shortlist(act.Arg, true); return true;
                case Reject: d.Shortlist(act.Arg, false); return true;
                case Offer:
                {
                    Seeker s = a != null ? h.SeekerOf(a.Seeker) : null;
                    if (a == null || s == null)
                    {
                        return false;
                    }
                    d.MakeOffer(act.Arg, Math.Max(1, view.Get(0, (HireOffers.Competing(a, SaveGameManager.Current.Day) ? Math.Max(a.RivalBid, s.Ask) : s.Ask) / 100)) * 100);
                    return true;
                }
                case Counter: d.TakeCounter(act.Arg); return true;
                case Walk: d.WalkAway(act.Arg); view.Sub = 0; return true;
                case Close: d.CloseAd(act.Arg); return true;
                case Interview: return d.Interview(act.Arg);
                case Refs: return d.Check(act.Arg, false);
                case Background: return d.Check(act.Arg, true);
                case Buyout:
                {
                    Seeker s = a != null ? h.SeekerOf(a.Seeker) : null;
                    if (a == null || s == null)
                    {
                        return false;
                    }
                    int day = SaveGameManager.Current.Day, start = HiringRules.BuyoutStart(day);
                    string name = s.P.Name, amount = TechText.Cents(HiringRules.BuyoutQuote(a, s, day)), days = HiringRules.BuyoutDays(a.Start, start).ToString(), when = start.ToString();
                    int id = a.Id;
                    GameConfirm.Show("reallife_hire_buyout_title".Localize(new { name }),
                        "reallife_hire_buyout_body".Localize(new { amount, day = when, days, factor = TechGuide.Factor(HiringCatalogue.BuyoutBp) }),
                        () =>
                        {
                            d.BuyOutNotice(id);
                            _local++;
                        }, null, null, null, true, false);
                    return true;
                }
                case NewAd: view.Sub = 3; view.Pick = 0; return true;
                case OpenApplicant: view.Sub = 1; view.Pick = act.Arg; view.PageTab = 0; return true;
                case OpenPerson: view.Sub = 2; view.Pick = act.Arg; view.PageTab = 0; return true;
                case PostAd: return PostTheAd(d, view);
                case Brief: return BriefSearch(d, view);
                case BriefCrew: return BriefCrewSearch(d, view);
                case Match: d.AnswerRival(key, true); return true;
                case LetThem: d.AnswerRival(key, false); return true;
                case GiveRise: return key != null && d.GiveRise(key, act.Arg);
                case RefuseRise: d.RefuseRise(key); return true;
                case Promote: return key != null && d.Promote(key);
                case SetWork: d.SetWork(key, view.Get(0, person != null ? person.File.Work : 0)); return true;
                case CourseA: return key != null && d.Course(key, 0);
                case CourseB: return key != null && d.Course(key, 1);
                case Degree: return key != null && d.SponsorDegree(key);
                case Exam: return key != null && d.Exam(key, act.Arg);
                case Clause: d.AddClause(key); return true;
                case LayOff:
                case Dismiss:
                {
                    if (person == null)
                    {
                        return false;
                    }
                    int year = GameCalendar.DaysPerYear, day = SaveGameManager.Current.Day;
                    Profile p = person.File;
                    string name = person.Name;
                    bool dismiss = act.Kind == Dismiss;
                    long severance = TalentRules.Severance(p, day, year, p.GameWage * TalentCatalogue.WeekHours, LeaveKind.LaidOff);
                    GameConfirm.Show((dismiss ? "reallife_roles_confirm_dismiss_title" : "reallife_roles_confirm_lay_off_title").Localize(new { name }),
                        dismiss ? "reallife_roles_confirm_dismiss_body".Localize() : "reallife_roles_confirm_lay_off_body".Localize(new { notice = TalentRules.NoticeDays(p,
                            day, year).ToString(), severance = TechText.Cents(severance) }),
                        () =>
                        {
                            d.LetGo(key, dismiss);
                            _local++;
                        }, null, null, null, true, true);
                    return true;
                }
                default: return false;
            }
        }

        // The new ad as the player set it: the view's choices, paid by the director.
        private bool PostTheAd(TechDirector d, WorkView v)
        {
            TalentGame.Businesses(_places);
            if (_places.Count == 0)
            {
                return false;
            }
            int roleAt = Math.Max(0, Math.Min(TalentCatalogue.RoleCount - 1, v.Get(0, 0)));
            RoleDef r = TalentCatalogue.Role(roleAt);
            int level = Math.Max(r.LeastLevel, Math.Min(r.MostLevel, r.LeastLevel + v.Get(1, Math.Max(r.LeastLevel, TalentCatalogue.Mid) - r.LeastLevel)));
            BuildingRegistration place = _places[Math.Max(0, Math.Min(_places.Count - 1, v.Get(2, 0)))];
            int going = d.GoingFor(r.Id, level);
            int most = going * PayBands[Math.Max(0, Math.Min(PayBands.Length - 1, v.Get(3, 1)))] / 1000, least = most * 85 / 100;
            int skillAt = Math.Max(0, Math.Min(2, v.Get(5, 0)));
            int skill = skillAt == 0 ? 0 : skillAt == 1 ? TalentCatalogue.LevelSkillLeast[level] : (TalentCatalogue.LevelSkillLeast[level] + TalentCatalogue.LevelSkillMost[level]) / 2;
            int works = r.Remote ? 3 : 2;
            int work = Math.Max(0, Math.Min(works - 1, v.Get(6, 0)));
            int days = HiringCatalogue.AdDays[Math.Max(0, Math.Min(HiringCatalogue.AdDays.Length - 1, v.Get(7, 1)))];
            int posts = Math.Max(1, Math.Min(HireOffers.PostsMost, v.Get(8, 1)));
            bool posted = d.PostAd(TalentGame.Place(place), r.Id, level, least, most, skill, work, days, v.Get(4, 0) == 1, posts);
            if (posted)
            {
                v.Sub = 0;
            }
            return posted;
        }

        private bool BriefSearch(TechDirector d, WorkView v)
        {
            TalentGame.Businesses(_places);
            if (_places.Count == 0)
            {
                return false;
            }
            int roleAt = Math.Max(0, Math.Min(TalentCatalogue.RoleCount - 1, v.Get(0, TalentCatalogue.SoftwareEngineer)));
            RoleDef r = TalentCatalogue.Role(roleAt);
            int least = Math.Max(r.LeastLevel, HiringCatalogue.SearchLeastLevel);
            int level = Math.Max(least, Math.Min(r.MostLevel, least + v.Get(1, 0)));
            return d.StartSearch(TalentGame.Place(_places[Math.Max(0, Math.Min(_places.Count - 1, v.Get(2, 0)))]), r.Id, level);
        }

        private bool BriefCrewSearch(TechDirector d, WorkView v)
        {
            TalentGame.Businesses(_places);
            if (_places.Count == 0)
            {
                return false;
            }
            return d.StartCrewSearch(TalentGame.Place(_places[Math.Max(0, Math.Min(_places.Count - 1, v.Get(2, 0)))]), TalentCatalogue.Technician,
                TalentCatalogue.Mid, Math.Max(1, Math.Min(HiringCatalogue.CrewMost, v.Get(3, 4))));
        }
    }
}
