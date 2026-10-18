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
    /// The Campus app, in the game's frame (GameUi.AppShell, copied from MyEmployees): tabs over the table on the right
    /// (courses on offer, the courses under way, results, skills and the family's schooling), and on the left the box with
    /// the row picked. Nothing runs while it is closed; the rows are made when it opens, a tab is picked or something changes.
    /// </summary>
    public sealed partial class CampusApp
    {
        public const string CourseRow = "course:";
        private const string ResultRow = "result:", SkillRow = "skill:";

        private static readonly string[] TabKeys =
        {
            "reallife_campus_tab_courses", "reallife_campus_tab_studying", "reallife_campus_tab_results", "reallife_campus_tab_skills", "reallife_campus_tab_family"
        };

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
            using var piece = SessionWatch.Piece("campus: the app being built from the game's MyEmployees");
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
                    box.Note(CareersText.T("reallife_campus_off_body"));
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
                        case CareersDirector.TabFamily: table = WriteFamily(box); break;
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

        // The Guide's pages the tabs point to at their foot: courses and skills, and the family's schooling.
        private const string StudyGuide = "reallife_meta_title_careers_study", FamilyGuide = "reallife_meta_title_careers_family";

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
                WriteCourse(box, f, picked);
                return CampusColumns.Courses;
            }
            _picked = null;
            box.Title(CareersText.T("reallife_campus_courses_title"));
            box.Note(CareersText.T("reallife_campus_courses_about"));
            box.Section("reallife_ibox_sec_now");
            int council = CareersDirector.Council;
            if (council != 0)
            {
                box.Line(CareersText.T(council > 0 ? "reallife_campus_council_up" : "reallife_campus_council_down", "share", CareersText.Mille(StudyCatalogue.CouncilShare)),
                    council > 0 ? CellTone.Green : CellTone.Yellow);
            }
            box.Line(CareersText.T(CareersDirector.UniversityOpen ? "reallife_campus_courses_university" : "reallife_campus_courses_school_only"),
                CareersDirector.UniversityOpen ? CellTone.Plain : CellTone.Yellow);
            Meta.Guide.Point(box, StudyGuide);
            return CampusColumns.Courses;
        }

        private static readonly StudyMode[] Modes = { StudyMode.FullTime, StudyMode.PartTime, StudyMode.Distance };

        private void WriteCourse(InfoBox box, StudyFile f, CourseDef c)
        {
            Enrolment live = f.Live(c.Id);
            bool ok = StudyRules.Qualified(f, c) && (!f.Holds(c.Id) || c.Id == StudyCatalogue.DrivingRefresher);
            box.Title(CareersText.Course(c.Id));
            box.Subtitle(CareersText.Fill("reallife_ibox_course_sub", "kind", CareersText.Kind(c.Kind), "place", _director.PlaceOf(c, StudyMode.FullTime)));
            if (live != null)
            {
                box.Chip(CareersText.T("reallife_ibox_chip_enrolled"), CellTone.Green);
            }
            else if (!ok)
            {
                bool held = f.Holds(c.Id);
                box.Chip(CareersText.T(held ? "reallife_campus_sum_passed" : "reallife_career_state_unqualified"), held ? CellTone.Green : CellTone.Red);
                box.Line(CareersText.T(held ? "reallife_campus_course_held" : "reallife_campus_course_locked"), held ? CellTone.Plain : CellTone.Red);
            }
            else
            {
                box.Chip(CareersText.T("reallife_career_state_open"));
            }
            box.Note(CareersText.About(c));

            box.Section("reallife_ibox_sec_course");
            int grant = _director.GrantFor(c);
            int fee = Prices.At(StudyRules.Fee(c, grant));
            box.Row("reallife_campus_col_fee", CareersText.Money(fee), grant > 0 ? CellTone.Green : grant < 0 ? CellTone.Yellow : CellTone.Plain);
            if (grant != 0)
            {
                box.RowPair("reallife_ibox_grants", CareersText.Mille(System.Math.Abs(grant)), "reallife_ibox_full_fee", CareersText.Money(Prices.At(c.Fee)));
            }
            box.Row("reallife_ibox_paid_in", CareersText.T("reallife_ibox_parts", "parts", StudyRules.Instalments(c).ToString()));
            box.Row("reallife_ibox_counts_as", c.School >= 0 ? TechText.School(c.School) : CareersText.T("reallife_campus_course_no_school"));
            if (c.Needs.Length > 0 || c.NeedsDegree)
            {
                box.Check(CareersText.T("reallife_campus_course_needs", "needs", Needs(c)), StudyRules.Qualified(f, c));
            }

            box.Section("reallife_campus_col_teaches");
            int aNow = StudyRules.Points(f, c.SkillA), bNow = StudyRules.Points(f, c.SkillB);
            box.MeterWords(CareersText.Skill(c.SkillA), LookRules.Fill(aNow, c.GainA), CareersText.T("reallife_ibox_upto", "now", aNow.ToString(), "gain", c.GainA.ToString()),
                aNow >= c.GainA ? CellTone.Green : CellTone.Plain);
            box.MeterWords(CareersText.Skill(c.SkillB), LookRules.Fill(bNow, c.GainB), CareersText.T("reallife_ibox_upto", "now", bNow.ToString(), "gain", c.GainB.ToString()),
                bNow >= c.GainB ? CellTone.Green : CellTone.Plain);

            box.Section("reallife_ibox_sec_how");
            box.Note(c.Timetabled
                ? CareersText.Fill("reallife_campus_course_exam", "pass", c.Pass.ToString(), "homework", c.Homework.ToString(), "share", CareersText.Mille(StudyCatalogue.Barred))
                : CareersText.Fill("reallife_campus_course_home", "homework", c.Homework.ToString(), "hours", StudyCatalogue.HomeworkHours.ToString(), "pass", c.Pass.ToString()));
            if (live != null)
            {
                Transform on = box.ButtonRow();
                InfoBox.Button(on, InfoBox.Positive, "reallife_campus_button_studying", () => _director.OpenCampus(CareersDirector.TabStudying, CourseRow + c.Id));
                InfoBox.Button(on, InfoBox.Additional, "common_close", ClosePick);
                Meta.Guide.Point(box, StudyGuide);
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
            box.Section("reallife_campus_col_timetable");
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
                box.Row("reallife_campus_col_mode", CareersText.Mode(mode));
                box.Row("reallife_campus_col_timetable", CareersText.Timetable(StudyRules.Days(c, mode), StudyRules.Hour(c, mode)));
                box.Row("reallife_ibox_class_length", CareersText.T("reallife_ibox_hours", "hours", c.Hours.ToString()));
                box.Row("reallife_campus_col_length", CareersText.Weeks(StudyRules.Length(c, mode)));
                box.Row("reallife_campus_col_where", _director.PlaceOf(c, mode));
            }
            else
            {
                box.Row("reallife_campus_col_mode", CareersText.Mode(mode));
            }
            Transform buttons = box.ButtonRow();
            if (ok)
            {
                int id = c.Id;
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_campus_button_enrol", () => Do(() => _director.Enrol(id, mode, false)));
                if (c.Loanable && CareersDirector.UniversityOpen)
                {
                    InfoBox.Button(buttons, InfoBox.Additional, "reallife_campus_button_loan", () => Do(() => _director.Enrol(id, mode, true)));
                }
            }
            InfoBox.Button(buttons, InfoBox.Additional, "common_close", ClosePick);
            Meta.Guide.Point(box, StudyGuide);
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
                bool pass = picked.Stage == (int)EnrolStage.Passed;
                box.Title(CareersText.Course(c.Id));
                box.Subtitle(CareersText.Kind(c.Kind));
                box.Chip(CareersText.Stage(picked.At), pass ? CellTone.Green : CellTone.Yellow);
                box.Note(CareersText.About(c));
                box.Section("reallife_ibox_sec_result");
                if (pass)
                {
                    box.Row("reallife_campus_col_grade", CareersText.Grade(c, picked.Grade), StudyRules.Honours(c, picked.Grade) ? CellTone.Green : CellTone.Plain);
                    box.Row("reallife_campus_col_mark", CareersText.T("reallife_ibox_of", "value", picked.Score.ToString(), "most", "100"));
                }
                box.Row("reallife_campus_col_ended", CareersText.Day(picked.Ended));
                if (pass && c.Cert >= 0)
                {
                    box.Line(CareersText.T("reallife_campus_result_cert"), CellTone.Green);
                }
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                Meta.Guide.Point(box, StudyGuide);
                return CampusColumns.Results;
            }
            _picked = null;
            box.Title(CareersText.T("reallife_campus_results_title"));
            box.Note(CareersText.T("reallife_campus_results_about"));
            box.Section("reallife_ibox_sec_record");
            box.Row("reallife_campus_sum_passed", passed.ToString());
            box.Row("reallife_campus_sum_honours", honours.ToString(), honours > 0 ? CellTone.Green : CellTone.Plain);
            box.Row("reallife_campus_sum_school", TechText.School(StudyRules.School(f)));
            Meta.Guide.Point(box, StudyGuide);
            return CampusColumns.Results;
        }

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
                bool fading = f.Skill[index] > StudyCatalogue.FadeLeast * 1000 && day - f.Used[index] > StudyCatalogue.FadeAfterDays;
                box.Title(CareersText.Skill(index));
                box.Chip(CareersText.T(f.Skill[index] <= 0 ? "reallife_campus_skill_none" : fading ? "reallife_campus_skill_fading" : "reallife_campus_skill_kept"),
                    fading ? CellTone.Yellow : CellTone.Plain);
                box.Section("reallife_ibox_sec_now");
                box.Row("reallife_campus_col_points", CareersText.Points(f.Skill[index]));
                box.Row("reallife_campus_col_best", CareersText.Points(f.Best[index]));
                box.Row("reallife_campus_col_used", f.Skill[index] > 0 ? CareersText.Day(f.Used[index]) : "-", fading ? CellTone.Yellow : CellTone.Plain);
                box.Section("reallife_ibox_sec_where_taught");
                box.Note(CareersText.T("reallife_campus_skill_courses", "courses", Teaching(index)));
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, "common_close", ClosePick);
                Meta.Guide.Point(box, StudyGuide);
                return CampusColumns.Skills;
            }
            _picked = null;
            box.Title(CareersText.T("reallife_campus_skills_title"));
            box.Note(CareersText.T("reallife_campus_skills_about", "days", StudyCatalogue.FadeAfterDays.ToString(), "floor", CareersText.Mille(StudyCatalogue.FadeFloor)));
            box.Section("reallife_ibox_sec_now");
            box.Row("reallife_campus_sum_skills", any.ToString());
            TutorButton(box);
            Meta.Guide.Point(box, StudyGuide);
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
