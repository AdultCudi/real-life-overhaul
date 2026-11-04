using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>One line of the CV: a spell at an employer, or a qualification won (a title, where, when, how it ended).</summary>
    public sealed class CareerSpell
    {
        public string Title, Employer, Dates, Note;
        public UiIcon Icon = UiIcon.Briefcase;
        public UiRole Tone = UiRole.None;
        public bool Current;
    }

    /// <summary>
    /// What fills the feet of the Career pages, built from plain facts by the live source and the previews alike: what is due and who else applied, the best paid
    /// posts and the courses a door away, a practice's record, what an employer sees in a new player, and the CV as a page of its own. Game-free.
    /// </summary>
    public static partial class CareerPages
    {
        private static WorkFormRow Tile(UiIcon icon, string label, string hint, string value) => new WorkFormRow { Field = WorkField.Tile, Icon = icon, Label = label, Hint = hint, Value = value };

        private static WorkFormRow Pair(string label, string value) => new WorkFormRow { Field = WorkField.Label, Label = label, Value = value };

        private static WorkFormRow Meter(string label, int value) => new WorkFormRow { Field = WorkField.Bar, Bar = new WorkBar { Label = label, Value = Math.Max(0, Math.Min(100, value)) } };

        /// <summary>A new player's page foot: what an employer sees of them, and the courses that open a door (each with the post it opens).</summary>
        public static void NewPlayerBands(UtilitiesWords w, CareerFacts f, int skillPoints, int qualifications, int coursesUnderWay, int applications, string[] courses, string[] opens)
        {
            var see = new WorkFormBand { Title = w.T("reallife_work_band_see", "What employers see in you") };
            see.Rows.Add(Meter(w.T("reallife_work_see_skills", "Skills"), skillPoints));
            see.Rows.Add(Pair(w.T("reallife_work_see_quals", "Qualifications held"), qualifications.ToString()));
            see.Rows.Add(Pair(w.T("reallife_work_see_courses", "Courses under way"), coursesUnderWay.ToString()));
            see.Rows.Add(Pair(w.T("reallife_work_see_apps", "Applications waiting"), applications.ToString()));
            f.NewBelow.Add(see);
            var doors = new WorkFormBand { Title = w.T("reallife_work_band_doors", "Courses that open a door") };
            for (int i = 0; i < courses.Length && i < 3; i++)
            {
                doors.Rows.Add(Tile(UiIcon.Cap, courses[i], w.F("reallife_work_opens_post", "Opens: {post}", "post", opens[i]), null));
            }
            if (doors.Rows.Count > 0)
            {
                f.NewBelow.Add(doors);
            }
        }

        /// <summary>The foot of the Applications list: what is due and when, and the record the employers weigh (who else applied for a post is in its detail).</summary>
        public static void ApplicationsBands(UtilitiesWords w, WorkTab apps, int[] days, string[] whens, int[] kinds, string[] posts, int[] record)
        {
            var due = new WorkFormBand { Title = w.T("reallife_work_band_due", "What is due") };
            var order = new List<int>(whens.Length);
            for (int i = 0; i < whens.Length; i++)
            {
                order.Add(i);
            }
            order.Sort((a, b) => days[a] != days[b] ? days[a].CompareTo(days[b]) : a.CompareTo(b));   // what is due first, in date order
            for (int n = 0; n < order.Count && n < 4; n++)
            {
                int i = order[n];
                string what = kinds[i] == 1 ? w.T("reallife_work_due_interview", "Attend the interview") : kinds[i] == 2 ? w.T("reallife_work_due_offer", "Answer the offer")
                    : w.T("reallife_work_due_answer", "Wait for an answer");
                due.Rows.Add(Tile(kinds[i] == 1 ? UiIcon.Calendar : kinds[i] == 2 ? UiIcon.Handshake : UiIcon.Clock, what, posts[i], whens[i]));
            }
            if (due.Rows.Count > 0)
            {
                apps.Below.Add(due);
            }
            if (record != null && record.Length == 4 && record[0] >= 0)
            {
                var mine = new WorkFormBand { Title = w.T("reallife_work_band_record", "Your record, as they weigh it") };
                mine.Rows.Add(Meter(w.T("reallife_work_stand_performance", "Performance"), record[0]));
                mine.Rows.Add(Meter(w.T("reallife_work_stand_punctuality", "Punctuality"), record[1]));
                mine.Rows.Add(Meter(w.T("reallife_work_stand_colleagues", "With colleagues"), record[2]));
                mine.Rows.Add(Meter(w.T("reallife_work_stand_reliability", "Reliability"), record[3]));
                apps.Below.Add(mine);
            }
        }

        /// <summary>The foot of the Professions list: the best paid posts open to the player, and the courses a door away (what each opens).</summary>
        public static void ProfessionsBands(UtilitiesWords w, WorkTab pro, string[] posts, string[] pays, UiIcon[] icons, string[] courses, string[] opens)
        {
            var best = new WorkFormBand { Title = w.T("reallife_work_band_best", "Best paid open to you") };
            for (int i = 0; i < posts.Length && i < 3; i++)
            {
                best.Rows.Add(Tile(icons[i], posts[i], null, pays[i] + "/h"));
            }
            if (best.Rows.Count > 0)
            {
                pro.Below.Add(best);
            }
            var door = new WorkFormBand { Title = w.T("reallife_work_band_door_away", "A course away") };
            for (int i = 0; i < courses.Length && i < 3; i++)
            {
                door.Rows.Add(Tile(UiIcon.Cap, courses[i], w.F("reallife_work_opens_post", "Opens: {post}", "post", opens[i]), null));
            }
            if (door.Rows.Count > 0)
            {
                pro.Below.Add(door);
            }
        }

        /// <summary>Own work's record: the contracts finished and what they earned beside the contracts, and the ones finished lately under the practice.</summary>
        public static void OwnBands(UtilitiesWords w, WorkFormModel form, int finished, int earned, string[] clients, string[] fees, string[] days)
        {
            var record = new WorkFormBand { Title = w.T("reallife_work_band_own_record", "Your record") };
            record.Rows.Add(Pair(w.T("reallife_work_own_finished", "Contracts finished"), finished.ToString()));
            record.Rows.Add(Pair(w.T("reallife_work_own_earned", "Earned from contracts"), "$" + earned.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)));
            record.Rows.Add(Pair(w.T("reallife_work_own_average", "Average fee"), finished > 0 ? "$" + (earned / finished).ToString("N0", System.Globalization.CultureInfo.InvariantCulture) : "$0"));
            form.Right.Add(record);
            var lately = new WorkFormBand { Title = w.T("reallife_work_band_own_lately", "Finished lately") };
            for (int i = 0; i < clients.Length && i < 3; i++)
            {
                lately.Rows.Add(Tile(UiIcon.Check, clients[i], days[i], fees[i]));
            }
            if (lately.Rows.Count > 0)
            {
                form.Left.Add(lately);
            }
        }

        // The CV's foot: the courses under way (the qualifications still to come) and, in work, where the next rung leads and what it asks.
        private static void CvBelow(UtilitiesWords w, WorkFormModel form, CareerJob job, UiIcon[] icons, string[] names, string[] hints, string[] values)
        {
            if (names != null && names.Length > 0)
            {
                var studying = new WorkFormBand { Title = w.T("reallife_work_band_studying_now", "Studying now"), Count = names.Length.ToString() };
                for (int i = 0; i < names.Length && i < 3; i++)
                {
                    studying.Rows.Add(Tile(icons[i], names[i], hints[i], values[i]));
                }
                form.Below.Add(studying);
            }
            CareerRung next = null;
            for (int i = 0; job != null && next == null && i < job.Rungs.Count; i++)
            {
                next = job.Rungs[i].State == 2 ? job.Rungs[i] : null;
            }
            if (next != null)
            {
                int met = 0;
                for (int i = 0; i < job.Needs.Count; i++)
                {
                    met += job.Needs[i].Met ? 1 : 0;
                }
                var leads = new WorkFormBand { Title = w.T("reallife_work_band_leads", "Where this leads") };
                leads.Rows.Add(Tile(UiIcon.Trophy, next.Title, w.F("reallife_work_needs_met", "{met} of {all} met", "met", met.ToString(), "all", job.Needs.Count.ToString()), next.Pay));
                for (int i = 0; i < job.Needs.Count && i < 3; i++)
                {
                    leads.Rows.Add(new WorkFormRow { Field = WorkField.Check, Label = job.Needs[i].Words, On = job.Needs[i].Met });
                }
                form.Below.Add(leads);
            }
        }

        /// <summary>
        /// The CV as a page of its own: the person at the head with the figures an employer reads, the working life as a dated list of jobs with a glyph and a
        /// reference each, the skills as bars, the qualifications, and the pension with the way to retire.
        /// </summary>
        public static void CvPage(UtilitiesWords w, WorkTab cv, string name, string title, string employer, List<CareerSpell> spells, List<CareerSpell> quals, int jobsHeld, int monthsWorked,
            string earned, int goodRefs, string pot, int age, int retireAge, string[] skills, int[] skillValues, string whyNot, bool retired = false, CareerJob job = null,
            UiIcon[] studyIcons = null, string[] studyNames = null, string[] studyHints = null, string[] studyValues = null)
        {
            cv.Shape = WorkShape.Form;
            var form = new WorkFormModel();
            form.Head = new UiProfileModel
            {
                Name = name, Hue = UiColour.HueOf(name), Role = title ?? w.T("reallife_work_cv_no_job", "Not in work"), RoleIcon = UiIcon.Briefcase, Employer = employer, EmployerIcon = UiIcon.Store,
                FactLabels = new[] { w.T("reallife_work_cv_jobs_held", "Jobs held"), w.T("reallife_work_cv_months", "Months worked"), w.T("reallife_work_cv_earned", "Earned"),
                    w.T("reallife_work_cv_references", "References") },
                FactValues = new[] { jobsHeld.ToString(), monthsWorked.ToString(), earned, w.F("reallife_work_cv_refs_good", "{n} good", "n", goodRefs.ToString()) }
            };
            var life = new WorkFormBand { Title = w.T("reallife_work_cv_working_life", "Working life"), Count = spells.Count.ToString() };
            for (int i = 0; i < spells.Count && i < 6; i++)
            {
                CareerSpell s = spells[i];
                life.Rows.Add(Tile(s.Icon, s.Title + (string.IsNullOrEmpty(s.Employer) ? string.Empty : ", " + s.Employer), s.Dates, s.Current ? null : s.Note));
            }
            if (spells.Count == 0)
            {
                life.Rows.Add(new WorkFormRow { Field = WorkField.Text, Value = w.T("reallife_work_cv_none", "No job yet. Your jobs and references appear here as you earn them.") });
            }
            form.Left.Add(life);
            if (skills != null && skills.Length > 0)
            {
                var sk = new WorkFormBand { Title = w.T("reallife_work_cv_skills", "Skills an employer reads") };
                for (int i = 0; i < skills.Length && i < 4; i++)
                {
                    sk.Rows.Add(Meter(skills[i], skillValues[i]));
                }
                form.Left.Add(sk);
            }
            if (quals.Count > 0)
            {
                var q = new WorkFormBand { Title = w.T("reallife_work_cv_qualifications", "Qualifications"), Count = quals.Count.ToString() };
                for (int i = 0; i < quals.Count && i < 5; i++)
                {
                    q.Rows.Add(Tile(quals[i].Icon, quals[i].Title, quals[i].Employer, quals[i].Dates));
                }
                form.Right.Add(q);
            }
            var pension = new WorkFormBand { Title = w.T("reallife_work_cv_pension", "Pension") };
            pension.Rows.Add(Pair(w.T("reallife_life_sum_pension", "Pension pot"), pot));
            pension.Rows.Add(Pair(w.T("reallife_work_cv_age", "Your age"), age.ToString()));
            pension.Rows.Add(Pair(w.T("reallife_work_cv_retire_from", "You can retire from"), retireAge.ToString()));
            if (!retired)
            {
                pension.Foot = new WorkAct { Kind = (int)CareerAct.Retire, Label = w.T("reallife_life_button_retire", "Retire"), Style = UiButtonKind.Destructive, Icon = UiIcon.SignOut,
                    Why = whyNot };
            }
            form.Right.Add(pension);
            CvBelow(w, form, job, studyIcons, studyNames, studyHints, studyValues);
            cv.Form = form;
        }
    }
}
