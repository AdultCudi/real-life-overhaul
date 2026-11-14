using System;
using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// Saying what is short: every refusal of enrolling, attending, homework, an exam and graduation names what is short, the level it
    /// asks and the level the player has, and where the exam is sat is said in the same words on both interfaces, in the notices and at
    /// the desks. A reason with figures is one string (<see cref="CareersWhy"/>). Nothing here runs per frame.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private const string SaidKey = "reallife_study_why_said";

        /// <summary>Shows a refusal as a notice with its figures (a reason with none is its key alone); the one way every page says one.</summary>
        internal static void Say(NotificationType type, string why, float seconds = 8f)
        {
            Dictionary<string, string> data = CareersWhy.DataOf(why);
            Dictionary<string, string> taken = Instance != null ? Instance.TakeWhyData() : null;
            Notifications.Show(type, CareersWhy.KeyOf(why), data ?? taken, seconds);
        }

        /// <summary>The reason for tiredness with the energy it needs and the player's.</summary>
        internal static string TiredWhy()
        {
            return CareersWhy.With("reallife_study_why_tired", "need", StudyCatalogue.TiredEnergy.ToString(), "have", Mathf.RoundToInt(CareersGame.Energy).ToString());
        }

        /// <summary>An instalment owed on a course, with its amount.</summary>
        internal string OwedWhy(Enrolment e)
        {
            return CareersWhy.With("reallife_study_why_fee_owed", "amount", CareersText.Money(Owed(e)));
        }

        /// <summary>Money short for a payment: what it is for (a locale key), what it costs at today's prices and what the player has.</summary>
        internal static string MoneyWhy(string whatKey, int needWhole)
        {
            int have = CareersGame.Cash;
            return CareersWhy.With("reallife_study_why_short_money", "what", CareersText.T(whatKey), "need", CareersText.Money(needWhole), "have", CareersText.Money(have),
                "short", CareersText.Money(Math.Max(0, needWhole - have)));
        }

        // ---------------------------------------------------------------- enrolling

        /// <summary>Every requirement of enrolling in a course with the player's own level beside it (<see cref="StudyNeedRules.Asks"/>), at today's prices.</summary>
        internal void EnrolAsks(CourseDef c, StudyMode mode, bool loan, List<Ask> into)
        {
            into.Clear();
            if (_study == null || c == null || Save == null)
            {
                return;
            }
            var e = new Enrolment { Course = c.Id, Mode = (int)mode, Start = Save.Day, Grant = GrantFor(c) };
            int due = Prices.At(StudyRules.InstalmentDue(e));
            Intake i = IntakeOf(c);
            int room = Math.Max(0, Prices.At(StudyCatalogue.LoanMost) - (int)(_study.LoanCents / 100));
            StudyNeedRules.Asks(_study, c, loan, CareersGame.Cash, due, room, i != null ? RosterRules.Left(i) : -1, into);
        }

        /// <summary>Why a course cannot be enrolled in, with every requirement the player is short of and what raises it, or null when they can enrol.</summary>
        internal string EnrolWhy(CourseDef c, StudyMode mode, bool loan, List<Ask> asks)
        {
            if (!_studyOn || c == null || !Offered(c))
            {
                return "reallife_study_why_closed";
            }
            EnrolRefusal refusal = StudyRules.CanEnrol(_study, c, mode);
            if (refusal == EnrolRefusal.Passed && c.Id == StudyCatalogue.DrivingRefresher)
            {
                refusal = EnrolRefusal.None;   // a refresher can be taken again
            }
            if (refusal == EnrolRefusal.Already || refusal == EnrolRefusal.Passed || refusal == EnrolRefusal.NoDistance || refusal == EnrolRefusal.Closed)
            {
                return "reallife" + "_study_why_" + Refusal(refusal);   // put together, so the text check reads only whole keys
            }
            EnrolAsks(c, mode, loan, asks);
            return StudyNeedRules.Short(asks) == 0 ? null : AsksWhy(c, asks);
        }

        // The sentences of what is short, one for each, then what raises each skill that is short; a notice has room for 240 characters.
        private string AsksWhy(CourseDef c, List<Ask> asks)
        {
            var said = new List<string>(8);
            var raises = new List<string>(2);
            for (int i = 0; i < asks.Count; i++)
            {
                Ask a = asks[i];
                if (a.Met)
                {
                    continue;
                }
                said.Add(AskShort(c, a));
                if (a.Kind == AskKind.Skill)
                {
                    raises.Add(RaiseLine(a.Id, c.Id));
                }
            }
            said.AddRange(raises);
            return CareersWhy.With(SaidKey, "text", CareersWhy.Fit(said));
        }

        /// <summary>What is short, in a sentence: the level it asks and the level the player has.</summary>
        internal string AskShort(CourseDef c, Ask a)
        {
            switch (a.Kind)
            {
                case AskKind.Skill:
                    return CareersText.Fill("reallife_study_short_skill", "skill", CareersText.Skill(a.Id), "need", a.Need.ToString(), "have", a.Have.ToString());
                case AskKind.Course:
                    return CareersText.T("reallife_study_short_course", "needs", NeedsOf(c));
                case AskKind.Seats:
                    return CareersText.T("reallife_study_short_seats", "day", CareersText.Day(RosterRules.StartOf(Save.Day) + RosterRules.IntakeDays));
                case AskKind.Slots:
                    return CareersText.T("reallife_study_short_slots", "n", a.Have.ToString());
                case AskKind.Loan:
                    return CareersText.T("reallife_study_short_loan", "need", CareersText.Money(a.Need), "have", CareersText.Money(a.Have));
                default:
                    return CareersText.T("reallife_study_short_cash", "need", CareersText.Money(a.Need), "have", CareersText.Money(a.Have));
            }
        }

        /// <summary>What a requirement of a course reads as on its page, left and right: "Accounting at 30" beside "you have 12".</summary>
        internal void AskWords(CourseDef c, Ask a, out string words, out string have)
        {
            switch (a.Kind)
            {
                case AskKind.Skill:
                    words = CareersText.T("reallife_study_ask_skill", "skill", CareersText.Skill(a.Id), "need", a.Need.ToString());
                    have = CareersText.T("reallife_study_ask_have", "have", a.Have.ToString());
                    break;
                case AskKind.Course:
                    words = CareersText.T("reallife_study_ask_course", "needs", NeedsOf(c));
                    have = a.Met ? CareersText.T("reallife_study_ask_passed") : CareersText.T("reallife_study_ask_none");
                    break;
                case AskKind.Seats:
                    words = CareersText.T("reallife_study_ask_seats");
                    have = CareersText.T("reallife_study_ask_left", "n", a.Have.ToString());
                    break;
                case AskKind.Slots:
                    words = CareersText.T("reallife_study_ask_slots", "most", StudyRules.LiveMost.ToString());
                    have = CareersText.T("reallife_study_ask_on", "n", a.Have.ToString());
                    break;
                case AskKind.Loan:
                    words = CareersText.T("reallife_study_ask_loan", "need", CareersText.Money(a.Need));
                    have = CareersText.T("reallife_study_ask_left_money", "have", CareersText.Money(a.Have));
                    break;
                default:
                    words = CareersText.T("reallife_study_ask_cash", "need", CareersText.Money(a.Need));
                    have = CareersText.T("reallife_study_ask_have", "have", CareersText.Money(a.Have));
                    break;
            }
        }

        /// <summary>The courses a course needs passed first, any one of them or any degree, by name.</summary>
        internal static string NeedsOf(CourseDef c)
        {
            var b = new System.Text.StringBuilder(64);
            string or = CareersText.T("reallife_campus_or");
            for (int i = 0; i < c.Needs.Length; i++)
            {
                b.Append(b.Length > 0 ? " " + or + " " : string.Empty).Append(CareersText.Course(c.Needs[i]));
            }
            if (c.NeedsDegree)
            {
                b.Append(b.Length > 0 ? " " + or + " " : string.Empty).Append(CareersText.T("reallife_campus_any_degree"));
            }
            return b.ToString();
        }

        // ---------------------------------------------------------------- what raises a skill

        /// <summary>
        /// What raises a skill, from the same catalogue the courses and professions are: up to two courses that teach it and up to two
        /// professions whose work does. <paramref name="except"/> is the course being asked about, or -1.
        /// </summary>
        internal static string RaiseLine(int skill, int except)
        {
            var courses = new List<int>(4);
            var jobs = new List<int>(4);
            StudyNeedRules.RaisingCourses(skill, except, Offered, courses);
            StudyNeedRules.RaisingProfessions(skill, jobs);
            string by = NameList(courses, 2, false), work = NameList(jobs, 2, true);
            string name = CareersText.Skill(skill);
            if (by.Length > 0 && work.Length > 0)
            {
                return CareersText.Fill("reallife_study_raise_both", "skill", name, "courses", by, "jobs", work);
            }
            if (by.Length > 0)
            {
                return CareersText.T("reallife_study_raise_courses", "skill", name, "courses", by);
            }
            return work.Length > 0 ? CareersText.T("reallife_study_raise_work", "skill", name, "jobs", work) : string.Empty;
        }

        private static string NameList(List<int> ids, int most, bool professions)
        {
            var b = new System.Text.StringBuilder(64);
            for (int i = 0; i < ids.Count && i < most; i++)
            {
                b.Append(i > 0 ? ", " : string.Empty).Append(professions ? CareersText.ProName(ids[i]) : CareersText.Course(ids[i]));
            }
            return b.ToString();
        }

        // ---------------------------------------------------------------- where the exam is sat

        /// <summary>The words for where a course's exam is sat: a study desk inside the school, its classroom at the university, a ward.</summary>
        internal string ExamWhere(CourseDef c, StudyMode mode)
        {
            switch (StudyNeedRules.ExamPlaceOf(c, mode))
            {
                case ExamPlace.Desk: return CareersText.T("reallife_study_exam_where_desk", "school", CareersText.Campus(Campus.School));
                case ExamPlace.Ward: return CareersText.T("reallife_study_exam_where_ward");
                default: return CareersText.T("reallife_study_exam_where_room", "place", PlaceOf(c, mode));
            }
        }

        /// <summary>
        /// Where and how a course's exam is sat and until which day, in a sentence: for an exam open, its last day; for a retake, its days; before
        /// then, how long after the last class or piece of homework it stays open.
        /// </summary>
        internal string ExamLine(CourseDef c, StudyMode mode, Enrolment e)
        {
            string where = ExamWhere(c, mode);
            string opens = CareersText.Hour(StudyCatalogue.ExamOpens), closes = CareersText.Hour(StudyCatalogue.ExamCloses);
            if (e != null && e.Stage == (int)EnrolStage.Exam)
            {
                return CareersText.Fill("reallife_study_exam_line_open", "where", where, "opens", opens, "closes", closes, "day", CareersText.Day(e.ExamTo));
            }
            if (e != null && e.Stage == (int)EnrolStage.Retake)
            {
                return CareersText.Fill("reallife_study_exam_line_retake", "where", where, "opens", opens, "closes", closes, "from", CareersText.Day(e.ExamFrom), "to", CareersText.Day(e.ExamTo));
            }
            string days = StudyNeedRules.ExamOpenDays(c).ToString();
            if (StudyNeedRules.MarkedInClass(c))
            {
                return CareersText.Fill("reallife_study_exam_line_short", "where", where, "days", days);
            }
            return CareersText.Fill(c.Timetabled ? "reallife_study_exam_line_later_classes" : "reallife_study_exam_line_later_homework",
                "where", where, "opens", opens, "closes", closes, "days", days);
        }

        /// <summary>Show on map for the place a course's exam is sat; null if done, else a reason key. A ward is any hospital, so it has no one place.</summary>
        internal string ShowExam(CourseDef c, StudyMode mode)
        {
            switch (StudyNeedRules.ExamPlaceOf(c, mode))
            {
                case ExamPlace.Desk: return ShowCampus(Campus.School);
                case ExamPlace.Ward: return "reallife_study_why_no_ward_place";
                default: return ShowCampus(Campus.University);
            }
        }

        /// <summary>The same for the campus that holds a course, as the page's button asks it (the exam's place, which is where the course's building is).</summary>
        internal string ShowCourse(int course, int place)
        {
            if (StudyCatalogue.Find(course) == null)
            {
                return "reallife_career_why_route";
            }
            return place == (int)ExamPlace.Ward ? "reallife_study_why_no_ward_place" : ShowCampus(place == (int)ExamPlace.Desk ? Campus.School : Campus.University);
        }

        /// <summary>The exam of an enrolment said as a notice: where, when and by which day; a click shows the place on the map (the course's card where it has none).</summary>
        private void TellExam(string key, Enrolment e)
        {
            CourseDef c = e.Def;
            var data = new Dictionary<string, string>(6)
            {
                { "course", CareersText.Course(c.Id) }, { "where", ExamWhere(c, (StudyMode)e.Mode) }, { "opens", CareersText.Hour(StudyCatalogue.ExamOpens) },
                { "closes", CareersText.Hour(StudyCatalogue.ExamCloses) }, { "day", CareersText.Day(e.ExamTo) }
            };
            int course = e.Course;
            Notifications.Show(NotificationType.Info, key, data, 10f, null, Guard.Wrap(() => ExamClick(course)), true, false);
        }

        private void ExamClick(int course)
        {
            Enrolment e = _studyOn ? _study.Live(course) : null;
            if (e == null || e.Def == null || ShowExam(e.Def, (StudyMode)e.Mode) != null)
            {
                OpenCampus(TabStudying, CampusApp.CourseRow + course);   // where the map cannot point, the card says it
            }
        }

        private int _examTodayDay = -1;
        private long _examTodayMask;

        // At the hour exams open, once a day for each: an exam of the player's that can be sat today, and where.
        private void ExamsToday(int day)
        {
            if (_examTodayDay != day)
            {
                _examTodayDay = day;
                _examTodayMask = 0;
            }
            for (int i = 0; i < _study.Courses.Count; i++)
            {
                Enrolment e = _study.Courses[i];
                if (e.Def == null || !StudyRules.ExamDay(e, day) || e.Course >= StudyCatalogue.IdsMost || (_examTodayMask & (1L << e.Course)) != 0)
                {
                    continue;
                }
                _examTodayMask |= 1L << e.Course;
                TellExam("reallife_study_note_exam_today", e);
            }
        }

        private ExamDesks _examDesks;

        // Once a second: the school's study desks marked while the player is inside it with an exam of theirs open there, taken off otherwise.
        private void ExamDesksTick()
        {
            bool wanted = false;
            if (_studyOn && Save != null && _study.Courses.Count > 0 && CareersGame.InSchool())
            {
                for (int i = 0; i < _study.Courses.Count && !wanted; i++)
                {
                    Enrolment e = _study.Courses[i];
                    wanted = e.Def != null && AtTheSchool(e, true) && StudyNeedRules.ExamOpen(e, Save.Day);
                }
            }
            if (!wanted && _examDesks == null)
            {
                return;
            }
            if (_examDesks == null)
            {
                _examDesks = new ExamDesks(Logger, this);
            }
            _examDesks.Tick(wanted);
        }

        private int _elsewhereDay = -1;
        private long _elsewhereMask;

        // Once a second, only at the university: an exam of the player's that is not sat here is told, with where it is sat; once a day for each.
        private void ExamElsewhere()
        {
            if (!_studyOn || Save == null || _study.Courses.Count == 0 || !AtUniversity())
            {
                return;
            }
            int day = Save.Day;
            for (int i = 0; i < _study.Courses.Count; i++)
            {
                Enrolment e = _study.Courses[i];
                if (e.Def == null || !StudyNeedRules.ExamOpen(e, day) || StudyNeedRules.ExamPlaceOf(e.Def, (StudyMode)e.Mode) == ExamPlace.Room || e.Course >= StudyCatalogue.IdsMost)
                {
                    continue;
                }
                if (_elsewhereDay != day)
                {
                    _elsewhereDay = day;
                    _elsewhereMask = 0;
                }
                if ((_elsewhereMask & (1L << e.Course)) != 0)
                {
                    continue;
                }
                _elsewhereMask |= 1L << e.Course;
                TellExam("reallife_study_note_exam_elsewhere", e);
            }
        }
    }
}
