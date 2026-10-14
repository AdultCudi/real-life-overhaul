using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The Team tab. The table lists every professional of the player's; one that is picked has three pages in the white
    /// box: their file (role, level, skills, experience, school, certificates, how they feel, pay against the going rate,
    /// the last review, and whatever waits for an answer), their career (the role, a promotion, a rise, a course or an
    /// exam) and their contract (where they work, a restraint clause, notice and severance, letting them go).
    /// </summary>
    public sealed partial class RolesApp
    {
        public const string TeamRow = "pro:";
        private const int PageFile = 0, PageCareer = 1, PageContract = 2;

        private readonly int[] _roles = new int[TalentCatalogue.RoleCount];
        private readonly List<int> _certs = new List<int>(TalentCatalogue.CertCount);
        private int _exam;

        private TableSpec WriteTeam(InfoBox box, int day)
        {
            IReadOnlyList<Person> people = _director.People;
            int year = GameCalendar.DaysPerYear;
            TableFilter businesses = RolesColumns.Team.Filters[0];
            businesses.Names.Clear();
            Person picked = null;
            string id = Picked(_picked, TeamRow);
            int waiting = 0, leaving = 0, morale = 0;
            for (int i = 0; i < people.Count; i++)
            {
                Person person = people[i];
                Profile p = person.File;
                TableRow row = Row(TeamRow + p.Id);
                row.Set(0, person.Name);
                row.Set(1, TechText.Role(p.Role));
                row.Set(2, TechText.Level(p.Level), p.Level);
                row.Set(3, TechText.Percent(person.MainPoints), person.MainPoints);
                row.Set(4, TechText.Fill("reallife_roles_row_specialisms", "a", TechText.Score(p.A), "b", TechText.Score(p.B)), (p.A + p.B) / 2000.0);
                row.Set(5, TechText.Mille(p.Morale), p.Morale, p.Morale < 400 ? TableRow.Red : p.Morale < 550 ? TableRow.Yellow : TableRow.Plain);
                row.Set(6, TechText.Mille(p.Burnout), p.Burnout, p.Burnout >= TalentCatalogue.BurnoutSick ? TableRow.Red : p.Burnout >= 500 ? TableRow.Yellow : TableRow.Plain);
                row.Set(7, person.Business.Length > 0 ? person.Business : TechText.T("reallife_roles_row_unassigned"));
                bool waits = p.RaiseAsk > 0 || TalentRules.OfferOpen(p, day) || TalentRules.CanPromote(p, day, year, person.MainPoints);
                row.Set(8, State(person, day, year), waits || p.NoticeEnd > 0 ? TableRow.Yellow : TableRow.Plain);
                row.Kind = KindOf(businesses, person.Business);
                row.Kind2 = waits ? RolesColumns.Waiting : p.NoticeEnd > 0 ? RolesColumns.Leaving : p.OnCourse(day) ? RolesColumns.Learning : RolesColumns.Settled;
                waiting += waits ? 1 : 0;
                leaving += p.NoticeEnd > 0 ? 1 : 0;
                morale += p.Morale;
                picked = p.Id == id ? person : picked;
            }
            if (_rows.Count == 0)
            {
                Say(TechText.T("reallife_roles_row_no_team"));
            }
            if (picked != null)
            {
                switch (_page)
                {
                    case PageCareer: WriteCareer(box, picked, day, year); break;
                    case PageContract: WriteContract(box, picked, day, year); break;
                    default: WriteFile(box, picked, day, year); break;
                }
                return RolesColumns.Team;
            }
            _picked = null;
            box.Title(TechText.T("reallife_roles_team_title"));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_roles_sum_waiting", waiting.ToString());
            box.Row("reallife_roles_sum_people", people.Count.ToString());
            box.Row("reallife_roles_sum_leaving", leaving.ToString());
            box.Row("reallife_roles_sum_morale", people.Count > 0 ? TechText.Mille(morale / people.Count) : "-");
            box.Note(TechText.T("reallife_roles_team_about"));
            Meta.Guide.Point(box, PeopleGuide);
            return RolesColumns.Team;
        }

        private static int KindOf(TableFilter filter, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return -1;
            }
            int kind = filter.Names.IndexOf(name);
            if (kind < 0)
            {
                kind = filter.Names.Count;
                filter.Names.Add(name);
            }
            return kind;
        }

        // What waits or is under way, in a word or two.
        private static string State(Person person, int day, int year)
        {
            Profile p = person.File;
            if (TalentRules.OfferOpen(p, day))
            {
                return TechText.T("reallife_roles_state_offer");
            }
            if (p.NoticeEnd > 0)
            {
                return TechText.T("reallife_roles_state_leaving", "day", p.NoticeEnd.ToString());
            }
            if (p.RaiseAsk > 0)
            {
                return TechText.T("reallife_roles_state_asks", "percent", TechText.Bp(p.RaiseAsk));
            }
            if (TalentRules.CanPromote(p, day, year, person.MainPoints))
            {
                return TechText.T("reallife_roles_state_promote");
            }
            if (p.OnCourse(day))
            {
                return TechText.T(p.Exam >= 0 ? "reallife_roles_state_exam" : "reallife_roles_state_course");
            }
            if (p.Sick)
            {
                return TechText.T("reallife_roles_state_sick");
            }
            return TechText.T("reallife_roles_state_fine");
        }

        // ---------------------------------------------------------------- a professional's file

        private void WriteFile(InfoBox box, Person person, int day, int year)
        {
            Profile p = person.File;
            RoleDef r = p.Def;
            box.Title(person.Name);
            bool offer = TalentRules.OfferOpen(p, day);
            if (offer)
            {
                box.Line(TechText.Fill("reallife_roles_file_offer", "rival", p.PoachBy, "percent", TechText.Bp(p.PoachBp), "day", p.PoachDay.ToString()));
            }
            else if (p.RaiseAsk > 0)
            {
                box.Line(TechText.Fill("reallife_roles_file_asks", "percent", TechText.Bp(p.RaiseAsk), "day", p.AskEnd.ToString()));
            }
            else if (p.NoticeEnd > 0)
            {
                box.Line(TechText.T(LeaveKey(p), "day", p.NoticeEnd.ToString()));
            }
            box.Line(TechText.Fill("reallife_roles_file_role", "role", TechText.Role(p.Role), "level", TechText.Level(p.Level), "main", TechText.Percent(person.MainPoints),
                "payroll", person.Game == null ? TechText.T("reallife_roles_file_payroll") : string.Empty));
            box.Line(TechText.Fill("reallife_roles_file_skills", "a", TechText.Skill(r.SkillA), "a_score", TechText.Score(p.A), "b", TechText.Skill(r.SkillB), "b_score", TechText.Score(p.B)));
            box.Line(TechText.Fill("reallife_roles_file_background", "years", TechText.Years(p.YearsDays + (day - p.Joined) * 365 / System.Math.Max(60, year)), "school", TechText.School(p.School),
                "certs", TechText.Certs(p)));
            box.Line(TechText.Fill("reallife_roles_file_feel", "morale", TechText.Mille(p.Morale), "burnout", TechText.Mille(p.Burnout), "work", TechText.Work(p.Work)));
            box.Line(TechText.Fill("reallife_roles_file_pay", "paid", TechText.Rate(p.GameWage), "going", TechText.Rate(p.Going)));
            box.Line(p.Rating > 0
                ? TechText.Fill("reallife_roles_file_review", "rating", p.Rating.ToString(), "day", p.ReviewDay.ToString())
                : TechText.T("reallife_roles_file_review_none", "day", p.ReviewDay.ToString()));
            string key = p.Id;
            if (offer || p.RaiseAsk > 0)
            {
                Transform answer = box.ButtonRow();
                if (offer)
                {
                    InfoBox.Button(answer, InfoBox.Positive, "reallife_roles_button_match", () => Do(() => _director.AnswerRival(key, true)));
                    InfoBox.Button(answer, InfoBox.Negative, "reallife_roles_button_let_them", () => Do(() => _director.AnswerRival(key, false)));
                }
                else
                {
                    int ask = p.RaiseAsk;
                    InfoBox.Button(answer, InfoBox.Positive, "reallife_roles_button_give", () => Do(() => _director.GiveRise(key, ask)));
                    InfoBox.Button(answer, InfoBox.Negative, "reallife_roles_button_refuse", () => Do(() => _director.RefuseRise(key)));
                }
            }
            Transform more = box.ButtonRow();
            InfoBox.Button(more, InfoBox.Positive, "reallife_roles_button_career", () => Page(PageCareer));
            InfoBox.Button(more, InfoBox.Additional, "reallife_roles_button_contract", () => Page(PageContract));
            InfoBox.Button(more, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, PeopleGuide);
        }

        private static string LeaveKey(Profile p)
        {
            switch ((LeaveKind)p.Leaving)
            {
                case LeaveKind.LaidOff: return "reallife_roles_file_leaving_laid_off";
                case LeaveKind.Poached: return "reallife_roles_file_leaving_poached";
                case LeaveKind.BurntOut: return "reallife_roles_file_leaving_burnt_out";
                default: return "reallife_roles_file_leaving_resigned";
            }
        }

        // ---------------------------------------------------------------- their career

        private void WriteCareer(InfoBox box, Person person, int day, int year)
        {
            Profile p = person.File;
            RoleDef r = p.Def;
            string key = p.Id;
            box.Title(TechText.T("reallife_roles_career_title", "name", person.Name));
            int count = Roles(person);
            int at = 0;
            for (int i = 0; i < count; i++)
            {
                at = _roles[i] == p.Role ? i : at;
            }
            if (count > 1 && box.HasSelector)
            {
                box.Selector(count, at, i => TechText.Role(_roles[i]), i => Do(() => _director.SetRole(key, _roles[i])));
            }
            else
            {
                box.Line(TechText.Role(p.Role));
            }
            bool promote = TalentRules.CanPromote(p, day, year, person.MainPoints);
            box.Line(promote
                ? TechText.Fill("reallife_roles_career_promote", "level", TechText.Level(p.Level + 1), "percent", TechText.Bp(TalentCatalogue.PromotePayBp))
                : TechText.Fill("reallife_roles_career_no_promote", "rating", TalentCatalogue.PromoteRating.ToString(), "least", TechText.Percent(p.Level + 1 < TalentCatalogue.Levels ? TalentCatalogue.LevelSkillLeast[p.Level + 1] - 5 : 100)));
            Transform rises = box.ButtonRow();
            if (promote)
            {
                InfoBox.Button(rises, InfoBox.Positive, "reallife_roles_button_promote", () => Do(() => _director.Promote(key)));
            }
            InfoBox.Button(rises, InfoBox.Additional, "reallife_roles_button_rise_3", () => Do(() => _director.GiveRise(key, 300)));
            InfoBox.Button(rises, InfoBox.Additional, "reallife_roles_button_rise_8", () => Do(() => _director.GiveRise(key, 800)));
            if (p.OnCourse(day))
            {
                box.Line(TechText.T(p.CourseSkill == 2 ? "reallife_roles_career_on_degree" : p.Exam >= 0 ? "reallife_roles_career_on_exam" : "reallife_roles_career_on_course", "day", p.CourseEnd.ToString()));
            }
            else
            {
                int share = TechBridges.CourseShare();   // (the player's own academy: Careers)
                int feeA = (int)((long)Prices.At(TalentRules.CourseFee(p.A / 1000)) * share / 10000), feeB = (int)((long)Prices.At(TalentRules.CourseFee(p.B / 1000)) * share / 10000);
                box.Line(TechText.Fill("reallife_roles_career_courses", "a", TechText.Skill(r.SkillA), "a_gain", TalentRules.CourseGain(p.A / 1000).ToString(), "a_fee", TechText.Money(feeA),
                    "b", TechText.Skill(r.SkillB), "b_gain", TalentRules.CourseGain(p.B / 1000).ToString(), "b_fee", TechText.Money(feeB), "days", TalentCatalogue.CourseDays.ToString()));
                bool degree = TechBridges.DegreeFee != null && TechBridges.DegreeFee() > 0;
                if (degree)
                {
                    box.Line(TechText.Fill("reallife_roles_career_degree", "fee", TechText.Money(TechBridges.DegreeFee()), "days", TalentCatalogue.Scaled(TalentCatalogue.DegreeDays, year).ToString(),
                        "gain", TalentCatalogue.DegreeGain.ToString()));
                }
                _certs.Clear();
                int examCert = -1;
                for (int c = 0; c < TalentCatalogue.CertCount; c++)
                {
                    TalentCatalogue.CertDef cert = TalentCatalogue.Certs[c];
                    if (!p.Has(c) && p.ScoreOf(cert.Skill) >= 0)
                    {
                        _certs.Add(c);
                    }
                }
                if (_certs.Count > 0)
                {
                    _exam = Mathf.Clamp(_exam, 0, _certs.Count - 1);
                    if (_certs.Count > 1 && box.HasSelector)
                    {
                        box.Selector(_certs.Count, _exam, i => TechText.Cert(_certs[i]), i =>
                        {
                            _exam = i;
                            Refresh();
                        });
                    }
                    int cert = _certs[_exam];
                    int chance = TalentRules.ExamChance(p, cert);
                    box.Line(chance > 0
                        ? TechText.Fill("reallife_roles_career_exam", "cert", TechText.Cert(cert), "fee", TechText.Money(Prices.At(TalentCatalogue.Certs[cert].Fee)), "chance", TechText.Percent(chance))
                        : TechText.Fill("reallife_roles_career_exam_short", "cert", TechText.Cert(cert), "needs", TechText.Percent(TalentCatalogue.Certs[cert].Needs)));
                    if (chance > 0)
                    {
                        examCert = cert;
                    }
                }
                Transform courses = box.ButtonRow();
                InfoBox.Button(courses, InfoBox.Additional, "reallife_roles_button_course_a", () => Do(() => _director.Course(key, 0)));
                InfoBox.Button(courses, InfoBox.Additional, "reallife_roles_button_course_b", () => Do(() => _director.Course(key, 1)));
                if (degree)
                {
                    InfoBox.Button(courses, InfoBox.Additional, "reallife_roles_button_degree", () => Do(() => _director.SponsorDegree(key)));
                }
                if (examCert >= 0)
                {
                    int sit = examCert;
                    InfoBox.Button(courses, InfoBox.Positive, "reallife_roles_button_exam", () => Do(() => _director.Exam(key, sit)));
                }
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_roles_button_back", () => Page(PageFile));
            Meta.Guide.Point(box, PeopleGuide);
        }

        // The roles this professional can be moved to: the roles of their game skill, or for the mod's own payroll the roles without one of the same family.
        private int Roles(Person person)
        {
            int n = 0;
            for (int i = 0; i < TalentCatalogue.Roles.Length; i++)
            {
                RoleDef r = TalentCatalogue.Roles[i];
                bool fits = person.Game != null ? r.InGame && r.GameSkill == person.Skill : !r.InGame && r.Family == person.File.Def.Family || r.Id == person.File.Role;
                if (fits)
                {
                    _roles[n++] = r.Id;
                }
            }
            return n;
        }

        // ---------------------------------------------------------------- their contract

        private void WriteContract(InfoBox box, Person person, int day, int year)
        {
            Profile p = person.File;
            string key = p.Id;
            box.Title(TechText.T("reallife_roles_contract_title", "name", person.Name));
            int works = p.Def.Remote && person.Game == null ? 3 : 2;
            if (box.HasSelector)
            {
                box.Selector(works, Mathf.Clamp(p.Work, 0, works - 1), TechText.Work, i => Do(() => _director.SetWork(key, i)));
            }
            else
            {
                box.Line(TechText.Work(p.Work));
            }
            box.Line(TechText.T(TalentRules.Content(p, _director.TalentSeed) ? "reallife_roles_contract_content" : "reallife_roles_contract_wants", "work", TechText.Work((int)TalentRules.Wants(p, _director.TalentSeed))));
            bool probation = TalentRules.OnProbation(p, day, year);
            long severance = TalentRules.Severance(p, day, year, p.GameWage * TalentCatalogue.WeekHours, LeaveKind.LaidOff);
            box.Line(TechText.Fill(probation ? "reallife_roles_contract_probation" : "reallife_roles_contract_terms", "notice", TalentRules.NoticeDays(p, day, year).ToString(),
                "severance", TechText.Cents(severance), "years", TalentRules.ServiceYears(p, day, year).ToString(),
                "until", (p.Joined + TalentCatalogue.Scaled(TalentCatalogue.ProbationDays, year)).ToString()));
            box.Line(p.NonCompete ? TechText.T("reallife_roles_contract_clause_on") : TechText.T("reallife_roles_contract_clause_off", "percent", TechText.Bp(TalentCatalogue.NonCompeteBp)));
            Transform buttons = box.ButtonRow();
            if (!p.NonCompete)
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_roles_button_clause", () => Do(() => _director.AddClause(key)));
            }
            if (p.NoticeEnd == 0)
            {
                string name = person.Name;
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_roles_button_lay_off", () => GameConfirm.Show(
                    "reallife_roles_confirm_lay_off_title".Localize(new { name }),
                    "reallife_roles_confirm_lay_off_body".Localize(new { notice = TalentRules.NoticeDays(p, day, year).ToString(), severance = TechText.Cents(severance) }),
                    () => UiClone.Safe(() => Do(() => _director.LetGo(key, false))), null, null, null, true, true));
                if (TalentRules.FairDismissal(p, day, year))
                {
                    InfoBox.Button(buttons, InfoBox.Negative, "reallife_roles_button_dismiss", () => GameConfirm.Show(
                        "reallife_roles_confirm_dismiss_title".Localize(new { name }), "reallife_roles_confirm_dismiss_body".Localize(),
                        () => UiClone.Safe(() =>
                        {
                            _picked = null;
                            Do(() => _director.LetGo(key, true));
                        }), null, null, null, true, true));
                }
            }
            InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "reallife_roles_button_back", () => Page(PageFile));
            Meta.Guide.Point(box, PeopleGuide);
        }

        private void Do(System.Action act)
        {
            UiClone.Safe(act);
            Refresh();
        }
    }
}
