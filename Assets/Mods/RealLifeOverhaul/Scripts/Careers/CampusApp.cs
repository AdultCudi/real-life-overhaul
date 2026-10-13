using System.Collections.Generic;
using BAModAPI;
using Localizor;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Tech;
using UI;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The Campus app, in the game's frame (GameUi.AppShell, copied from MyEmployees) as the mod's other apps are. Its tabs
    /// over the table on the right, and on the left the white box with the one thing picked:
    ///
    ///   Courses    every course on offer: where, when, how long, the fee after grants and what it teaches; pick one to
    ///              read it, choose full time, part time or online, and enrol
    ///   Studying   the courses under way: the next class, attendance, homework, the exam; pick one to attend the class,
    ///              do homework, sit the exam, pay a fee that is owed or withdraw
    ///   Results    every course finished, with its grade: the qualifications an employer reads
    ///   Skills     the player's own skills, the best each has been and whether it is fading; tutoring from home
    ///   Family     the family's children, their schooling and a grown child's university (CampusApp.Family.cs)
    ///
    /// Everything on it is the game's. Nothing runs while the app is closed: the rows and figures are made when it opens,
    /// when a tab is picked and when the player changes something in it.
    /// </summary>
    public sealed partial class CampusApp
    {
        public const string CourseRow = "course:";
        private const string ResultRow = "result:", SkillRow = "skill:";

        private static readonly string[] TabKeys = { "reallife_campus_tab_courses", "reallife_campus_tab_studying", "reallife_campus_tab_results", "reallife_campus_tab_skills", "reallife_campus_tab_family" };

        public static IModLogger Logger;

        private readonly CareersDirector _director;
        private readonly List<TableRow> _rows = new List<TableRow>(64);
        private readonly List<TableRow> _pool = new List<TableRow>(64);

        private AppShell _shell;
        private DataTable _table;
        private int _tab, _wanted = -1;
        private string _wantPick, _picked;
        private bool _ready, _writing;
        private int _mode;

        public CampusApp(CareersDirector director)
        {
            _director = director;
        }

        public GameObject Build(FullMenu menu, Transform dormant)
        {
            SessionWatch.Mark("campus: the app being built from the game's MyEmployees");
            _shell = AppShell.Make(menu, dormant, CareersDirector.CampusAppId);
            _table = DataTable.On(_shell, "Campus");
            if (_table == null || !_shell.SetTabCount(TabKeys.Length) || !_shell.SetColumns(CampusColumns.Off))
            {
                return null;
            }
            _table.Money = amount => CareersText.Money((long)amount);
            _table.SelectionLost = ClosePick;
            _table.onRowSelected.AddListener(row => UiClone.Safe(() => RowClicked(row)));
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int tab = i;
                UiClone.Key(_shell.TabLabels[i], TabKeys[i]);
                UiClone.Rewire(_shell.Tabs[i], () => ShowTab(tab));
            }
            return _shell.Root;
        }

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        public void Want(int tab, string pick)
        {
            _wanted = Mathf.Clamp(tab, 0, TabKeys.Length - 1);
            _wantPick = pick;
            if (Showing)
            {
                Opened();
            }
        }

        public void Opened()
        {
            if (_shell == null || _table == null)
            {
                return;
            }
            if (_wanted >= 0)
            {
                _tab = _wanted;
                _picked = _wantPick;
                _wanted = -1;
                _wantPick = null;
            }
            _ready = _director.StudyRunning;
            Refresh();
        }

        public void Closed()
        {
            _ready = false;
        }

        public void Changed()
        {
            if (Showing && _ready && !_writing)
            {
                Refresh();
            }
        }

        private void ShowTab(int tab)
        {
            if (tab != _tab || _picked != null)
            {
                _tab = tab;
                _picked = null;
                Refresh();
            }
        }

        private void Refresh()
        {
            if (_shell == null || _table == null || _writing)
            {
                return;
            }
            _writing = true;
            try
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    _pool.Add(_rows[i]);
                }
                _rows.Clear();
                _shell.ShowTab(_tab);
                InfoBox box = _shell.Box;
                box.Clear();
                TableSpec table = CampusColumns.Off;
                StudyFile f = _director.Study;
                if (!_ready || SaveGameManager.Current == null || f == null)
                {
                    UiClone.Words(_shell.Header, CareersText.T("reallife_campus_app"));
                    box.Title(CareersText.T("reallife_campus_off_title"));
                    box.Line(CareersText.T("reallife_campus_off_body"));
                    _picked = null;
                }
                else
                {
                    int held = 0;
                    for (int i = 0; i < f.Results.Count; i++)
                    {
                        held += f.Results[i].Stage == (int)EnrolStage.Passed ? 1 : 0;
                    }
                    UiClone.Words(_shell.Header, CareersText.T("reallife_campus_head", "courses", f.Courses.Count.ToString(), "held", held.ToString()));
                    int day = SaveGameManager.Current.Day;
                    switch (_tab)
                    {
                        case CareersDirector.TabStudying: table = WriteStudying(box, f, day); break;
                        case CareersDirector.TabResults: table = WriteResults(box, f); break;
                        case CareersDirector.TabSkills: table = WriteSkills(box, f, day); break;
                        case CareersDirector.TabFamily: table = WriteFamily(box, f, day); break;
                        default: table = WriteCourses(box, f, day); break;
                    }
                }
                box.Done();
                _table.Show(table, _rows, _picked);
            }
            finally
            {
                _writing = false;
            }
        }

        private TableRow Row(string id)
        {
            TableRow row;
            if (_pool.Count > 0)
            {
                row = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                row = new TableRow();
            }
            row.Reset(id);
            row.Pick = true;
            row.Index = _rows.Count;
            _rows.Add(row);
            return row;
        }

        private static string Picked(string id, string kind)
        {
            return id != null && id.StartsWith(kind, System.StringComparison.Ordinal) ? id.Substring(kind.Length) : null;
        }

        private static int PickedId(string id, string kind)
        {
            string rest = Picked(id, kind);
            int n;
            return rest != null && int.TryParse(rest, out n) ? n : -1;
        }

        private void RowClicked(TableRow row)
        {
            if (row == null || !row.Pick)
            {
                _table.ClearSelection();
                return;
            }
            _picked = row.Id;
            _mode = 0;
            Refresh();
        }

        private void ClosePick()
        {
            _picked = null;
            _table.ClearSelection();
            Refresh();
        }

        private void Do(System.Func<string> act)
        {
            string why = null;
            UiClone.Safe(() => why = act());
            if (why != null)
            {
                Notifications.Show(NotificationType.Error, why, null, 8f);
            }
            Refresh();
        }

        // ---------------------------------------------------------------- Courses

        private TableSpec WriteCourses(InfoBox box, StudyFile f, int day)
        {
            CourseDef picked = null;
            int id = PickedId(_picked, CourseRow);
            for (int i = 0; i < StudyCatalogue.All.Length; i++)
            {
                CourseDef c = StudyCatalogue.All[i];
                if (!CareersDirector.Offered(c))
                {
                    continue;
                }
                TableRow row = Row(CourseRow + c.Id);
                int fee = Prices.At(StudyRules.Fee(c, _director.GrantFor(c)));
                row.Set(0, CareersText.Course(c.Id));
                row.Set(1, CareersText.Kind(c.Kind));
                row.Set(2, _director.PlaceOf(c, StudyMode.FullTime));
                row.Set(3, c.Timetabled ? c.Sessions.ToString() : "-", c.Sessions);
                row.Set(4, CareersText.Timetable(StudyRules.Days(c, StudyMode.FullTime), StudyRules.Hour(c, StudyMode.FullTime)));
                int length = StudyRules.Length(c, StudyMode.FullTime);
                row.Set(5, length > 0 ? CareersText.Weeks(length) : "-", length);
                row.Set(6, CareersText.Money(fee), fee);
                row.Set(7, CareersText.Fill("reallife_campus_row_teaches", "a", CareersText.Skill(c.SkillA), "b", CareersText.Skill(c.SkillB)));
                bool live = f.Live(c.Id) != null, held = f.Holds(c.Id), ok = StudyRules.Qualified(f, c);
                row.Set(8, CareersText.T(live ? "reallife_campus_state_on" : held ? "reallife_campus_state_held" : ok ? "reallife_campus_state_open" : "reallife_campus_state_locked"),
                    live || held ? TableRow.Green : ok ? TableRow.Plain : TableRow.Yellow);
                row.Kind = (int)c.Kind;
                row.Kind2 = live || held ? CampusColumns.Taken : ok ? CampusColumns.Open : CampusColumns.Locked;
                picked = c.Id == id ? c : picked;
            }
            if (picked != null)
            {
                WriteCourse(box, f, picked, day);
                return CampusColumns.Courses;
            }
            _picked = null;
            box.Title(CareersText.T("reallife_campus_courses_title"));
            box.Line(CareersText.T("reallife_campus_courses_about"));
            box.Line(CareersText.T(CareersDirector.UniversityOpen ? "reallife_campus_courses_university" : "reallife_campus_courses_school_only"));
            int council = CareersDirector.Council;
            if (council != 0)
            {
                box.Line(CareersText.T(council > 0 ? "reallife_campus_council_up" : "reallife_campus_council_down", "share", CareersText.Mille(StudyCatalogue.CouncilShare)));
            }
            return CampusColumns.Courses;
        }

        private static readonly StudyMode[] Modes = { StudyMode.FullTime, StudyMode.PartTime, StudyMode.Distance };

        private void WriteCourse(InfoBox box, StudyFile f, CourseDef c, int day)
        {
            box.Title(CareersText.Course(c.Id));
            box.Line(CareersText.About(c));
            box.Line(CareersText.Fill("reallife_campus_course_kind", "kind", CareersText.Kind(c.Kind), "place", _director.PlaceOf(c, StudyMode.FullTime),
                "school", c.School >= 0 ? TechText.School(c.School) : CareersText.T("reallife_campus_course_no_school")));
            box.Line(CareersText.Fill("reallife_campus_course_teaches", "a", CareersText.Skill(c.SkillA), "a_gain", c.GainA.ToString(), "a_now", StudyRules.Points(f, c.SkillA).ToString(),
                "b", CareersText.Skill(c.SkillB), "b_gain", c.GainB.ToString(), "b_now", StudyRules.Points(f, c.SkillB).ToString()));
            if (c.Needs.Length > 0 || c.NeedsDegree)
            {
                box.Line(CareersText.T("reallife_campus_course_needs", "needs", Needs(c)));
            }
            int grant = _director.GrantFor(c);
            int fee = Prices.At(StudyRules.Fee(c, grant));
            box.Line(grant != 0
                ? CareersText.Fill("reallife_campus_course_fee_grant", "fee", CareersText.Money(fee), "full", CareersText.Money(Prices.At(c.Fee)), "share", CareersText.Mille(System.Math.Abs(grant)),
                    "parts", StudyRules.Instalments(c).ToString())
                : CareersText.Fill("reallife_campus_course_fee", "fee", CareersText.Money(fee), "parts", StudyRules.Instalments(c).ToString()));
            if (c.Timetabled)
            {
                box.Line(CareersText.Fill("reallife_campus_course_exam", "pass", c.Pass.ToString(), "homework", c.Homework.ToString(), "share", CareersText.Mille(StudyCatalogue.Barred)));
            }
            else
            {
                box.Line(CareersText.Fill("reallife_campus_course_home", "homework", c.Homework.ToString(), "hours", StudyCatalogue.HomeworkHours.ToString(), "pass", c.Pass.ToString()));
            }
            Enrolment live = f.Live(c.Id);
            if (live != null)
            {
                box.Line(CareersText.T("reallife_campus_course_on"));
                Transform on = box.ButtonRow();
                InfoBox.Button(on, InfoBox.Positive, "reallife_campus_button_studying", () => _director.OpenCampus(CareersDirector.TabStudying, CourseRow + c.Id));
                InfoBox.Button(on, InfoBox.Additional, "common_close", ClosePick);
                return;
            }
            // The ways it can be followed: its own timetable, part time, and online for a course that allows it.
            int modes = 0;
            var offered = new StudyMode[3];
            for (int i = 0; i < Modes.Length; i++)
            {
                StudyMode m = Modes[i];
                if (m == StudyMode.PartTime && (!c.Timetabled || c.Kind == CourseKind.Short))
                {
                    continue;
                }
                if (m == StudyMode.Distance && (!c.Distance || c.Campus == Campus.Online))
                {
                    continue;
                }
                offered[modes++] = m;
            }
            _mode = Mathf.Clamp(_mode, 0, System.Math.Max(0, modes - 1));
            StudyMode mode = offered[_mode];
            if (modes > 1 && box.HasSelector)
            {
                box.Selector(modes, _mode, i => CareersText.Mode(offered[i]), i =>
                {
                    _mode = i;
                    Refresh();
                });
            }
            if (c.Timetabled)
            {
                box.Line(CareersText.Fill("reallife_campus_course_when", "mode", CareersText.Mode(mode), "timetable", CareersText.Timetable(StudyRules.Days(c, mode), StudyRules.Hour(c, mode)),
                    "hours", c.Hours.ToString(), "length", CareersText.Weeks(StudyRules.Length(c, mode)), "place", _director.PlaceOf(c, mode)));
            }
            Transform buttons = box.ButtonRow();
            bool ok = StudyRules.Qualified(f, c) && (!f.Holds(c.Id) || c.Id == StudyCatalogue.DrivingRefresher);
            if (ok)
            {
                int id = c.Id;
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_campus_button_enrol", () => Do(() => _director.Enrol(id, mode, false)));
                if (c.Loanable && CareersDirector.UniversityOpen)
                {
                    InfoBox.Button(buttons, InfoBox.Additional, "reallife_campus_button_loan", () => Do(() => _director.Enrol(id, mode, true)));
                }
            }
            else
            {
                box.Line(CareersText.T(f.Holds(c.Id) ? "reallife_campus_course_held" : "reallife_campus_course_locked"));
            }
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
        }

        private static string Needs(CourseDef c)
        {
            var b = new System.Text.StringBuilder(64);
            for (int i = 0; i < c.Needs.Length; i++)
            {
                if (b.Length > 0)
                {
                    b.Append(' ').Append(CareersText.T("reallife_campus_or")).Append(' ');
                }
                b.Append(CareersText.Course(c.Needs[i]));
            }
            if (c.NeedsDegree)
            {
                if (b.Length > 0)
                {
                    b.Append(' ').Append(CareersText.T("reallife_campus_or")).Append(' ');
                }
                b.Append(CareersText.T("reallife_campus_any_degree"));
            }
            return b.ToString();
        }

        // ---------------------------------------------------------------- Studying

        private TableSpec WriteStudying(InfoBox box, StudyFile f, int day)
        {
            Enrolment picked = null;
            int id = PickedId(_picked, CourseRow);
            int minute = CareersGame.MinuteOfDay;
            for (int i = 0; i < f.Courses.Count; i++)
            {
                Enrolment e = f.Courses[i];
                CourseDef c = e.Def;
                if (c == null)
                {
                    continue;
                }
                StudyMode mode = (StudyMode)e.Mode;
                TableRow row = Row(CourseRow + c.Id);
                row.Set(0, CareersText.Course(c.Id));
                row.Set(1, CareersText.Mode(mode));
                int next = e.Stage == (int)EnrolStage.Studying && c.Timetabled ? StudyRules.Next(c, mode, e.SettledDay >= day ? day + 1 : day) : -1;
                row.Set(2, next > 0 ? CareersText.T("reallife_campus_row_next", "day", CareersText.Day(next), "hour", CareersText.Hour(StudyRules.Hour(c, mode))) : "-", next);
                row.Set(3, c.Timetabled ? CareersText.T("reallife_campus_row_of", "done", e.Done.ToString(), "of", c.Sessions.ToString()) : "-", StudyRules.Attendance(e),
                    StudyRules.Attendance(e) < StudyCatalogue.Barred + 150 && e.Seen > 2 ? TableRow.Yellow : TableRow.Plain);
                row.Set(4, CareersText.T("reallife_campus_row_of", "done", e.Homework.ToString(), "of", c.Homework.ToString()), e.Homework);
                row.Set(5, CareersText.Money(e.Fee), e.Fee);
                row.Set(6, e.Stage == (int)EnrolStage.Exam || e.Stage == (int)EnrolStage.Retake
                    ? CareersText.T("reallife_campus_row_exam", "from", CareersText.Day(e.ExamFrom), "to", CareersText.Day(e.ExamTo)) : "-", e.ExamFrom);
                int minutes;
                bool now = e.Stage == (int)EnrolStage.Studying && e.SettledDay < day && StudyRules.Joinable(c, mode, day, minute, out minutes);
                row.Set(7, e.FeeLate ? CareersText.T("reallife_campus_state_fee") : now ? CareersText.T("reallife_campus_state_class_now") : CareersText.Stage(e.At),
                    e.FeeLate ? TableRow.Red : now || StudyRules.ExamDay(e, day) ? TableRow.Green : TableRow.Plain);
                picked = c.Id == id ? e : picked;
            }
            if (picked != null)
            {
                WriteEnrolment(box, f, picked, day);
                return CampusColumns.Studying;
            }
            _picked = null;
            box.Title(CareersText.T("reallife_campus_studying_title"));
            box.Line(CareersText.T("reallife_campus_studying_about", "early", StudyCatalogue.ArriveEarlyMinutes.ToString(), "late", StudyCatalogue.LateMinutes.ToString()));
            box.Line(CareersText.Pair("reallife_campus_sum_week", CareersText.Minutes(f.WeekOf == (day - 1) / 7 ? f.WeekMinutes : 0)));
            if (f.GraduationCourse >= 0 && f.GraduationDay >= day)
            {
                box.Line(CareersText.Fill("reallife_campus_ceremony", "course", CareersText.Course(f.GraduationCourse), "day", CareersText.Day(f.GraduationDay),
                    "hour", CareersText.Hour(CareersDirector.CeremonyHour), "place", _director.PlaceOf(StudyCatalogue.Find(f.GraduationCourse), StudyMode.FullTime)));
                if (f.GraduationDay == day)
                {
                    InfoBox.Button(box.ButtonRow(), InfoBox.Positive, "reallife_campus_button_graduate", () => Do(_director.Graduate));
                }
            }
            if (f.LoanCents > 0)
            {
                box.Line(CareersText.Pair("reallife_campus_sum_loan", CareersText.Cents(f.LoanCents)));
            }
            return CampusColumns.Studying;
        }

        private void WriteEnrolment(InfoBox box, StudyFile f, Enrolment e, int day)
        {
            CourseDef c = e.Def;
            StudyMode mode = (StudyMode)e.Mode;
            int id = c.Id;
            box.Title(CareersText.Course(c.Id));
            box.Line(CareersText.Fill("reallife_campus_on_mode", "mode", CareersText.Mode(mode), "place", _director.PlaceOf(c, mode), "start", CareersText.Day(e.Start)));
            if (c.Timetabled)
            {
                box.Line(CareersText.Fill("reallife_campus_on_classes", "done", e.Done.ToString(), "seen", e.Seen.ToString(), "of", c.Sessions.ToString(), "share", CareersText.Mille(StudyRules.Attendance(e)),
                    "timetable", CareersText.Timetable(StudyRules.Days(c, mode), StudyRules.Hour(c, mode))));
            }
            box.Line(CareersText.Fill("reallife_campus_on_homework", "done", e.Homework.ToString(), "set", StudyRules.HomeworkSet(e).ToString(), "of", c.Homework.ToString()));
            box.Line(CareersText.Fill("reallife_campus_on_paid", "paid", CareersText.Money(e.Fee), "loan", CareersText.Money(e.Loan), "parts", e.Instalments.ToString(), "of", StudyRules.Instalments(c).ToString()));
            if (e.Stage == (int)EnrolStage.Exam || e.Stage == (int)EnrolStage.Retake)
            {
                box.Line(CareersText.Fill(e.Stage == (int)EnrolStage.Retake ? "reallife_campus_on_retake" : "reallife_campus_on_exam", "from", CareersText.Day(e.ExamFrom), "to", CareersText.Day(e.ExamTo),
                    "opens", CareersText.Hour(StudyCatalogue.ExamOpens), "closes", CareersText.Hour(StudyCatalogue.ExamCloses), "fee", CareersText.Money(Prices.At(StudyRules.RetakeFee(c))),
                    "score", e.Score.ToString()));
            }
            Transform buttons = box.ButtonRow();
            int minutes;
            if (e.FeeLate)
            {
                box.Line(CareersText.T("reallife_campus_on_owed", "amount", CareersText.Money(_director.Owed(e))));
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_campus_button_pay", () => Do(() => _director.PayOwed(id)));
            }
            string attend = _director.CanAttend(e, out minutes);
            if (attend == null || attend != "reallife_study_why_no_class")
            {
                if (attend != null)
                {
                    box.Line(CareersText.T(attend));
                }
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_campus_button_attend", () => Do(() => _director.Attend(id)));
            }
            string homework = _director.CanHomework(e);
            if (homework == null || homework != "reallife_study_why_no_homework")
            {
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_campus_button_homework", () => Do(() => _director.Homework(id)));
            }
            string sit = _director.CanSit(e);
            if (sit == null || sit != "reallife_study_why_no_exam")
            {
                if (sit != null)
                {
                    box.Line(CareersText.T(sit));
                }
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_campus_button_sit", () => Do(() => _director.Sit(id)));
            }
            Transform more = box.ButtonRow();
            string name = CareersText.Course(id);
            InfoBox.Button(more, InfoBox.Negative, "reallife_campus_button_withdraw", () => HudConfirm.Show(
                "reallife_campus_confirm_withdraw_title".Localize(new { name }), "reallife_campus_confirm_withdraw_body".Localize(),
                () => UiClone.Safe(() =>
                {
                    _picked = null;
                    _director.Withdraw(id);
                }), null, null, null, true, true));
            InfoBox.Button(more, InfoBox.Additional, "common_close", ClosePick);
        }

        // ---------------------------------------------------------------- Results

        private TableSpec WriteResults(InfoBox box, StudyFile f)
        {
            Enrolment picked = null;
            int index = PickedId(_picked, ResultRow);
            int passed = 0, honours = 0;
            for (int i = f.Results.Count - 1; i >= 0; i--)
            {
                Enrolment e = f.Results[i];
                CourseDef c = e.Def;
                if (c == null)
                {
                    continue;
                }
                bool pass = e.Stage == (int)EnrolStage.Passed;
                TableRow row = Row(ResultRow + i);
                row.Set(0, CareersText.Course(c.Id));
                row.Set(1, CareersText.Kind(c.Kind));
                row.Set(2, pass ? CareersText.Grade(c, e.Grade) : "-", pass ? e.Grade : -1, pass && StudyRules.Honours(c, e.Grade) ? TableRow.Green : TableRow.Plain);
                row.Set(3, pass || e.Stage == (int)EnrolStage.Failed ? e.Score.ToString() : "-", e.Score);
                row.Set(4, c.Timetabled ? CareersText.Mille(StudyRules.Attendance(e)) : "-", StudyRules.Attendance(e));
                row.Set(5, e.Retakes.ToString(), e.Retakes);
                row.Set(6, CareersText.Day(e.Ended), e.Ended);
                row.Set(7, CareersText.Money(e.Fee), e.Fee);
                row.Set(8, CareersText.Stage(e.At), pass ? TableRow.Green : TableRow.Yellow);
                row.Kind = pass ? CampusColumns.ResultPassed : e.Stage == (int)EnrolStage.Withdrawn ? CampusColumns.ResultLeft : CampusColumns.ResultFailed;
                passed += pass ? 1 : 0;
                honours += pass && StudyRules.Honours(c, e.Grade) ? 1 : 0;
                picked = i == index ? e : picked;
            }
            if (picked != null)
            {
                CourseDef c = picked.Def;
                box.Title(CareersText.Course(c.Id));
                box.Line(CareersText.About(c));
                box.Line(picked.Stage == (int)EnrolStage.Passed
                    ? CareersText.Fill("reallife_campus_result_passed", "grade", CareersText.Grade(c, picked.Grade), "score", picked.Score.ToString(), "day", CareersText.Day(picked.Ended))
                    : CareersText.Fill("reallife_campus_result_other", "stage", CareersText.Stage(picked.At), "day", CareersText.Day(picked.Ended)));
                if (picked.Stage == (int)EnrolStage.Passed && c.Cert >= 0)
                {
                    box.Line(CareersText.T("reallife_campus_result_cert"));
                }
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                return CampusColumns.Results;
            }
            _picked = null;
            box.Title(CareersText.T("reallife_campus_results_title"));
            box.Line(CareersText.T("reallife_campus_results_about"));
            box.Line(CareersText.Pair("reallife_campus_sum_passed", passed.ToString()));
            box.Line(CareersText.Pair("reallife_campus_sum_honours", honours.ToString()));
            box.Line(CareersText.Pair("reallife_campus_sum_school", TechText.School(StudyRules.School(f))));
            return CampusColumns.Results;
        }

        // ---------------------------------------------------------------- Skills

        private TableSpec WriteSkills(InfoBox box, StudyFile f, int day)
        {
            int index = PickedId(_picked, SkillRow);
            int any = 0;
            for (int s = 0; s < Skills.Count; s++)
            {
                TableRow row = Row(SkillRow + s);
                row.Set(0, CareersText.Skill(s));
                row.Set(1, CareersText.Points(f.Skill[s]), f.Skill[s]);
                row.Set(2, CareersText.Points(f.Best[s]), f.Best[s]);
                row.Set(3, f.Skill[s] > 0 ? CareersText.Day(f.Used[s]) : "-", f.Used[s]);
                bool fading = f.Skill[s] > StudyCatalogue.FadeLeast * 1000 && day - f.Used[s] > StudyCatalogue.FadeAfterDays;
                row.Set(4, CareersText.T(f.Skill[s] <= 0 ? "reallife_campus_skill_none" : fading ? "reallife_campus_skill_fading" : "reallife_campus_skill_kept"),
                    fading ? TableRow.Yellow : TableRow.Plain);
                row.Kind = Skills.Professional(s) ? CampusColumns.SkillProfession : CampusColumns.SkillTrade;
                any += f.Skill[s] > 0 ? 1 : 0;
            }
            if (index >= 0 && index < Skills.Count)
            {
                box.Title(CareersText.Skill(index));
                box.Line(CareersText.Fill("reallife_campus_skill_line", "points", CareersText.Points(f.Skill[index]), "best", CareersText.Points(f.Best[index]),
                    "day", f.Skill[index] > 0 ? CareersText.Day(f.Used[index]) : "-"));
                box.Line(CareersText.T("reallife_campus_skill_courses", "courses", Teaching(index)));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                return CampusColumns.Skills;
            }
            _picked = null;
            box.Title(CareersText.T("reallife_campus_skills_title"));
            box.Line(CareersText.T("reallife_campus_skills_about", "days", StudyCatalogue.FadeAfterDays.ToString(), "floor", CareersText.Mille(StudyCatalogue.FadeFloor)));
            box.Line(CareersText.Pair("reallife_campus_sum_skills", any.ToString()));
            TutorButton(box);
            return CampusColumns.Skills;
        }

        /// <summary>The courses on offer that teach a skill, joined by commas.</summary>
        private static string Teaching(int skill)
        {
            var b = new System.Text.StringBuilder(96);
            for (int i = 0; i < StudyCatalogue.All.Length; i++)
            {
                CourseDef c = StudyCatalogue.All[i];
                if ((c.SkillA == skill || c.SkillB == skill) && CareersDirector.Offered(c))
                {
                    if (b.Length > 0)
                    {
                        b.Append(", ");
                    }
                    b.Append(CareersText.Course(c.Id));
                }
            }
            return b.Length > 0 ? b.ToString() : CareersText.T("reallife_campus_skill_no_course");
        }
    }
}
