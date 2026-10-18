using System.Text;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The Career app's first tab, Professions: how offers come, what the player can apply for now and the first step; then
    /// every profession with what its first post still needs, and, picked, its whole ladder (Careers/Work/ProPath, ProRules).
    /// </summary>
    public sealed partial class CareerApp
    {
        private readonly int[] _vacancies = new int[ProCatalogue.Count];
        private readonly int[] _openTo = new int[ProCatalogue.Count];

        private TableSpec WriteStart(InfoBox box, WorkFile w, StudyFile f, int day)
        {
            Candidate template = _director.Template();
            int open = 0;
            for (int p = 0; p < ProCatalogue.Count; p++)
            {
                _vacancies[p] = _openTo[p] = 0;
            }
            for (int i = 0; i < w.Openings.Count; i++)
            {
                Opening o = w.Openings[i];
                if (o.Def == null || !o.Open(day) || o.Prof < 0 || o.Prof >= ProCatalogue.Count)
                {
                    continue;
                }
                _vacancies[o.Prof]++;
                if (w.Applied(o) == null && _director.OpenToMe(o, template))
                {
                    _openTo[o.Prof]++;
                    open++;
                }
            }
            int id = PickedId(_picked, ProfessionRow), ready = 0;
            Profession picked = null;
            PathView pickedView = default(PathView);
            bool university = CareersDirector.UniversityOpen;
            for (int i = 0; i < ProCatalogue.All.Length; i++)
            {
                Profession p = ProCatalogue.All[i];
                Candidate c = template;
                c.Experience = ProRules.Experience(w, p.Id, day, GameCalendar.DaysPerYear);
                c.Agency = w.AgencyUntil >= day;
                PathView v = ProPath.For(f, p, 0, c, university);
                TableRow row = Row(ProfessionRow + p.Id);
                row.Set(0, CareersText.ProName(p.Id));
                row.Set(1, CareersText.Title(p.Id, 0));
                int pay = Prices.At(p.Ranks[0].Pay);
                row.Set(2, CareersText.T("reallife_career_row_rate", "rate", CareersText.Rate(pay)), pay);
                int points = ProRules.Points(f, p, 0), need = ProRules.Need(f, p, 0);
                row.Set(3, CareersText.T("reallife_career_row_fit", "points", points.ToString(), "need", need.ToString()), points - need,
                    v.Step == PathStep.Ready ? TableRow.Green : v.Step == PathStep.Skills ? TableRow.Yellow : TableRow.Plain);
                row.Set(4, CareersText.T("reallife_career_row_vacancies", "open", _openTo[p.Id].ToString(), "all", _vacancies[p.Id].ToString()), _openTo[p.Id] * 100 + _vacancies[p.Id],
                    _openTo[p.Id] > 0 ? TableRow.Green : TableRow.Plain);
                bool apply = v.Step == PathStep.Ready && _openTo[p.Id] > 0;
                row.Set(5, NextShort(v, apply), apply ? TableRow.Green : v.Step == PathStep.Ready ? TableRow.Plain : v.Step == PathStep.Closed ? TableRow.Red : TableRow.Yellow);
                row.Kind = apply ? CareerColumns.PathApply : v.Step == PathStep.Ready ? CareerColumns.PathWait : v.Step == PathStep.Skills ? CareerColumns.PathSkills : CareerColumns.PathCourse;
                ready += v.Step == PathStep.Ready ? 1 : 0;
                if (p.Id == id)
                {
                    picked = p;
                    pickedView = v;
                }
            }
            if (picked != null)
            {
                WriteProfession(box, w, f, picked, pickedView);
                return CareerColumns.Professions;
            }
            _picked = null;
            box.Title(CareersText.T("reallife_career_start_title"));
            box.Note(CareersText.T("reallife_career_start_how"));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_career_sum_open", open.ToString(), open > 0 ? CellTone.Green : CellTone.Plain);
            box.Row("reallife_career_sum_ready", ready.ToString());
            box.Section("reallife_ibox_sec_next");
            int course = -1;
            if (open > 0)
            {
                box.Line(CareersText.T("reallife_career_start_apply", "count", open.ToString()), CellTone.Green);
            }
            else
            {
                int prof;
                box.Line(CareersText.T("reallife_career_start_first", "step",
                    _director.FirstStep(template, out prof, out course) ? _director.FirstStepLine(prof, course) : CareersText.T("reallife_career_step_none")));
            }
            if (!university)
            {
                box.Line(CareersText.T("reallife_career_start_no_university"), CellTone.Yellow);
            }
            box.Section("reallife_ibox_sec_how");
            box.Note(CareersText.Fill("reallife_career_start_steps", "least", ProRules.AnswerDaysLeast.ToString(), "most", ProRules.AnswerDaysMost.ToString(),
                "after", ProRules.InterviewAfterDays.ToString(), "days", ProRules.OfferDays.ToString()));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, open > 0 ? InfoBox.Positive : InfoBox.Additional, "reallife_career_button_vacancies", () => ShowTab(CareersDirector.TabJobs));
            if (course >= 0)
            {
                int first = course;
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_career_button_campus", () => UiClone.Safe(() => _director.OpenCampus(CareersDirector.TabCourses, CampusApp.CourseRow + first)));
            }
            Meta.Guide.Point(box, JobsGuide);
            return CareerColumns.Professions;
        }

        /// <summary>A profession's next step in a few words, for its row.</summary>
        private static string NextShort(PathView v, bool apply)
        {
            switch (v.Step)
            {
                case PathStep.Ready: return apply ? CareersText.T("reallife_career_next_apply", "chance", CareersText.Percent(v.Chance)) : CareersText.T("reallife_career_next_wait");
                case PathStep.Studying: return CareersText.T("reallife_career_next_studying", "course", CareersText.Course(v.Course));
                case PathStep.Course:
                case PathStep.Certificate: return CareersText.T("reallife_career_next_course", "course", CareersText.Course(v.Course));
                case PathStep.Closed: return CareersText.T("reallife_career_next_closed");
                default:
                    return v.Short <= 100 ? CareersText.T("reallife_career_next_skills", "points", v.Short.ToString()) : CareersText.T("reallife_career_next_far");
            }
        }

        private void WriteProfession(InfoBox box, WorkFile w, StudyFile f, Profession p, PathView v)
        {
            box.Title(CareersText.ProName(p.Id));
            bool apply = v.Step == PathStep.Ready && _openTo[p.Id] > 0;
            box.Chip(NextShort(v, apply), apply ? CellTone.Green : v.Step == PathStep.Ready ? CellTone.Plain : v.Step == PathStep.Closed ? CellTone.Red : CellTone.Yellow);
            CourseDef c = StudyCatalogue.Find(v.Course);
            string course = c != null ? CareersText.Course(c.Id) : string.Empty, place = c != null ? _director.PlaceOf(c, StudyMode.FullTime) : string.Empty;
            int a, b;
            p.SkillsOf(0, out a, out b);
            box.Section("reallife_ibox_sec_next");
            switch (v.Step)
            {
                case PathStep.Ready:
                    box.Line(_openTo[p.Id] > 0
                        ? CareersText.T("reallife_career_prof_apply", "count", _openTo[p.Id].ToString(), "chance", CareersText.Percent(v.Chance))
                        : CareersText.T("reallife_career_prof_wait"), _openTo[p.Id] > 0 ? CellTone.Green : CellTone.Plain);
                    break;
                case PathStep.Studying:
                    box.Line(CareersText.T("reallife_career_prof_studying", "course", course, "place", place));
                    break;
                case PathStep.Course:
                    box.Line(CareersText.T("reallife_career_prof_course", "course", course, "place", place));
                    break;
                case PathStep.Certificate:
                    box.Line(CareersText.T("reallife_career_prof_cert", "course", course));
                    break;
                case PathStep.Closed:
                    box.Line(CareersText.T("reallife_career_prof_closed", "course", course), CellTone.Red);
                    break;
                default:
                    box.Line(CareersText.Fill("reallife_career_prof_skills", "points", v.Short.ToString(), "a", CareersText.Skill(a), "b", CareersText.Skill(b)));
                    if (c != null)
                    {
                        box.Note(CareersText.T("reallife_career_prof_teaches", "course", course, "place", place));
                    }
                    break;
            }
            if (_vacancies[p.Id] == 0 && v.Step != PathStep.Ready)
            {
                box.Line(CareersText.T("reallife_career_prof_none_now"), CellTone.Yellow);
            }
            // The ladder: each rung's title and pay as a row (the rung the player is on in green), and under it what it asks.
            box.Section("reallife_career_prof_ladder");
            int now = w.Job != null && w.Job.Post.Prof == p.Id ? w.Job.Post.Rank : -1;
            var line = new StringBuilder(160);
            for (int r = 0; r < p.Ranks.Length; r++)
            {
                ProRank rank = p.Ranks[r];
                int need = ProRules.Need(f, p, r), points = ProRules.Points(f, p, r);
                box.RowWords(CareersText.Title(p.Id, r), CareersText.T("reallife_career_row_rate", "rate", CareersText.Rate(Prices.At(rank.Pay))), r == now ? CellTone.Green : CellTone.Plain);
                line.Length = 0;
                line.Append(CareersText.Fill("reallife_ibox_rung_asks", "need", need.ToString(), "points", points.ToString()));
                string needs = rank.Quals.Length > 0 || rank.Cert >= 0 ? RungNeeds(rank) : string.Empty;
                if (needs.Length > 0)
                {
                    line.Append(' ').Append(CareersText.T("reallife_career_post_needs", "needs", needs));
                }
                if (r > 0)
                {
                    line.Append(' ').Append(CareersText.T("reallife_career_prof_after", "days", TalentCatalogue.Scaled(rank.Days, GameCalendar.DaysPerYear).ToString()));
                }
                if (r == now)
                {
                    line.Append(' ').Append(CareersText.T("reallife_career_prof_here"));
                }
                box.Note(line.ToString());
            }
            Transform buttons = box.ButtonRow();
            if (c != null)
            {
                int id = c.Id;
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_career_button_campus", () => UiClone.Safe(() => _director.OpenCampus(CareersDirector.TabCourses, CampusApp.CourseRow + id)));
            }
            if (_vacancies[p.Id] > 0)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_career_button_vacancies", () => ShowTab(CareersDirector.TabJobs));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, JobsGuide);
        }

        /// <summary>What one rung needs of its own, in words: any one of its courses, and its certificate.</summary>
        private static string RungNeeds(ProRank rank)
        {
            var b = new StringBuilder(96);
            for (int i = 0; i < rank.Quals.Length; i++)
            {
                if (i > 0)
                {
                    b.Append(' ').Append(CareersText.T("reallife_campus_or")).Append(' ');
                }
                b.Append(CareersText.Course(rank.Quals[i]));
            }
            if (rank.Cert >= 0)
            {
                if (b.Length > 0)
                {
                    b.Append(", ");
                }
                b.Append(TechText.Cert(rank.Cert));
            }
            return b.ToString();
        }
    }
}
