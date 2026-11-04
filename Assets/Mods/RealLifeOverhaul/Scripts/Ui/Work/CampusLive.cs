using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Tech;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Campus on the kit: the catalogue with its seats, teacher and what each course asks, the term's classes, the transcript with credits, the
    /// grade point average by quarter and the player's place in the class, the skills, the family and the schools. Read from the careers' own
    /// records; every button goes to the director that owns it. Enrol and the classes are not starts: the classes begin at the desk, and
    /// Nothing a frame.
    /// </summary>
    public sealed class CampusLive : IWorkSource
    {
        public static readonly CampusLive Instance = new CampusLive();

        private const int Enrol = 100, Pay = 101, Withdraw = 102, Graduate = 103, PayChild = 104, DeclineChild = 105, Tutor = 106;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private int _local, _wanted = -1, _readGeneration = -1;
        private WorkApp _app = new WorkApp();

        private CampusLive() { }

        public string Id => "campus";
        public string Title => W.T("reallife_campus_app", "Campus");
        public UiIcon Icon => UiIcon.Cap;
        public bool Across => true;
        public string T(string key, string english) => UiText.Words(key, english);
        private static UtilitiesWords W => CareerLive.Words();

        public int Generation
        {
            get
            {
                GameInstance save = SaveGameManager.Current;
                return DaySeriesDirector.Generation + _local + (save != null ? save.Day * 24 + save.Hour : 0);
            }
        }

        public static void Want(int tab)
        {
            Instance._wanted = tab;
            Instance._local++;
        }

        public int TakeWanted()
        {
            int tab = _wanted;
            _wanted = -1;
            return tab;
        }

        public UiNavEntry[] Nav(WorkView view)
        {
            Read();
            return _app.Nav;
        }

        public WorkPage Page(WorkView view)
        {
            Read();
            return WorkTabPages.Build(_app, view, W);
        }

        // ------------------------------------------------------------------------------------------------- reading

        private void Read()
        {
            int generation = Generation;
            if (_readGeneration == generation)
            {
                return;
            }
            _readGeneration = generation;
            CareersDirector d = CareersDirector.Instance;
            GameInstance save = SaveGameManager.Current;
            StudyFile f = d != null ? d.Study : null;
            _app = new WorkApp { Ready = d != null && f != null && save != null, OffBody = W.T("reallife_career_off_body",
                "Load a save with studying switched on to see your courses.") };
            UtilitiesWords w = W;
            _app.Nav = new[]
            {
                new UiNavEntry { Icon = UiIcon.Books, Label = w.T("reallife_campus_tab_courses", "Courses") },
                new UiNavEntry { Icon = UiIcon.Calendar, Label = w.T("reallife_campus_tab_studying", "Studying"), Count = f != null
                    && f.Courses.Count > 0 ? f.Courses.Count.ToString(Inv) : null },
                new UiNavEntry { Icon = UiIcon.ClipboardText, Label = w.T("reallife_campus_tab_results", "Results") },
                new UiNavEntry { Icon = UiIcon.Star, Label = w.T("reallife_campus_tab_skills", "Skills") },
                new UiNavEntry { Icon = UiIcon.Heart, Label = w.T("reallife_campus_tab_family", "Family") },
                new UiNavEntry { Icon = UiIcon.Building, Label = w.T("reallife_campus_tab_schools", "Schools") }
            };
            if (!_app.Ready)
            {
                return;
            }
            int day = save.Day;
            _app.Tabs = new[] { Courses(d, f, day), Studying(d, f, day), Results(d, f, day), SkillsTab(f), Family(d), Schools(d) };
        }

        private WorkTab Courses(CareersDirector d, StudyFile f, int day)
        {
            UtilitiesWords w = W;
            var t = new WorkTab
            {
                TableTitle = w.T("reallife_campus_tab_courses", "Courses"), Lead = w.T("reallife_work_classes_desk",
                    "Classes start at your desk. Click it in the classroom when the bell rings."),
                Columns = new[] { Col(w.T("reallife_campus_col_course", "Course"), true), Col(w.T("reallife_campus_col_school", "School")),
                    Col(w.T("reallife_campus_col_length", "Classes")),
                    Col(w.T("reallife_work_col_seats", "Seats left")), Col(w.T("reallife_campus_col_fee", "Fee"), false, true) }
            };
            for (int i = 0; i < StudyCatalogue.All.Length; i++)
            {
                CourseDef c = StudyCatalogue.All[i];
                if (!CareersDirector.Offered(c))
                {
                    continue;
                }
                Intake intake = d.IntakeOf(c);
                int fee = Prices.At(StudyRules.Fee(c, d.GrantFor(c)));
                bool live = f.Live(c.Id) != null;
                string why = live ? w.T("reallife_study_why_already", "You are on this course.") : f.Holds(c.Id) ? w.T("reallife_study_why_passed",
                    "You have passed this course.")
                    : !RosterRules.FloorMet(c, SkillPoints(d, c)) ? w.T("reallife_study_why_floor",
                        "Your skill is under what the course asks.") : intake != null
                        && !RosterRules.CanJoin(intake) ? w.T("reallife_study_why_seats", "Every seat is taken.") : null;
                string seats = intake != null ? RosterRules.Left(intake).ToString(Inv) + " / " + intake.Capacity.ToString(Inv) : "-";
                var r = new WorkRow { Id = c.Id, Icon = UiIcon.Cap, Cells = new[] { CareersText.Course(c.Id), d.PlaceOf(c, StudyMode.FullTime),
                    c.Timetabled ? c.Sessions.ToString(Inv) : "-", seats, CareersText.Money(fee) },
                    Status = KindName(c), Tone = UiRole.Accent, Group = (int)c.Kind <= 3 ? Math.Min(2, (int)c.Kind) : 2 };
                Tutor tutor = d.TutorOf(c);
                r.Detail = CareersText.Course(c.Id) + ". " + (tutor != null ? w.F("reallife_work_teacher",
                    "Your teacher: {name}, {years} years teaching, rated {rating} by students.", "name", tutor.Name, "years", tutor.Years.ToString(Inv),
                    "rating", (tutor.Rating / 10f).ToString("0.0", Inv)) : string.Empty);
                if (RosterRules.Floor(c, out int skill, out int need))
                {
                    r.Detail += " " + w.F("reallife_work_floor_line", "The course asks for {need} points; you have {have}.", "need",
                        need.ToString(Inv), "have", SkillPoints(d, c).ToString(Inv));
                }
                r.Acts.Add(new WorkAct { Kind = Enrol, Label = w.F("reallife_work_enrol_for", "Enrol, {fee}", "fee", CareersText.Money(fee)),
                    Style = UiButtonKind.Primary, Icon = UiIcon.Check, Why = why });
                if (intake != null)
                {
                    for (int s = 0; s < intake.Students.Count && s < 6; s++)
                    {
                        r.People.Add(new WorkPerson { Name = intake.Students[s].Name,
                            Role = intake.Students[s].Employee ? w.T("reallife_work_your_team", "From your team") : w.T("reallife_work_classmate",
                            "Classmate"), Label = w.T("reallife_work_gpa", "Grade point average"),
                            Score = intake.Students[s].Gpa / 4 });
                    }
                    r.PeopleTitle = w.T("reallife_work_in_class", "In this class");
                }
                long cash = (long)HustleCashOf();
                int owe = fee;
                r.Facts.Add(new KeyValuePair<string, string>(w.T("reallife_campus_col_fee", "Fee"), CareersText.Money(fee)));
                r.Facts.Add(new KeyValuePair<string, string>(w.T("reallife_campus_col_length", "Classes"), c.Timetabled ? c.Sessions.ToString(Inv) : "-"));
                r.Facts.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_seats", "Seats left"), seats));
                r.Facts.Add(new KeyValuePair<string, string>(w.T("reallife_work_before_fee", "You have the fee"), cash >= owe ? w.T("reallife_work_yes", "Yes") : w.T("reallife_work_short_by", "No, short by") + " " + CareersText.Money(owe - cash)));
                RealLifeOverhaul.Careers.Job job = d.Work != null ? d.Work.Job : null;
                if (job != null && c.Timetabled)
                {
                    WorkFit fit = StudyRules.Fit(c, StudyMode.FullTime, job.Def, out int _, out int _);
                    r.Facts.Add(new KeyValuePair<string, string>(w.T("reallife_work_before_evenings", "Class times free"), fit == WorkFit.Fits ? w.T("reallife_work_yes", "Yes") : fit == WorkFit.Clash ? w.T("reallife_work_clash", "Clashes with your shifts") : w.T("reallife_work_leave_early", "Yes, leaving early")));
                }
                if (RosterRules.Floor(c, out int fs, out int fn))
                {
                    r.Facts.Add(new KeyValuePair<string, string>(CareersText.Skill(fs), SkillPoints(d, c).ToString(Inv) + " / " + fn.ToString(Inv)));
                }
                t.Rows.Add(r);
            }
            t.Grid = true;
            t.GridKicker = w.T("reallife_campus_tab_courses", "Courses");
            t.Chips = new[] { w.F("reallife_work_filter_all", "All {n}", "n", t.Rows.Count.ToString(Inv)), w.T("reallife_work_kind_short", "Short"),
                w.T("reallife_work_kind_trade", "Diplomas"), w.T("reallife_work_kind_higher", "Degrees") };
            return t;
        }

        private static int HustleCashOf() => RealLifeOverhaul.Hustles.HustleGame.Cash;

        private static int SkillPoints(CareersDirector d, CourseDef c) => RosterRules.Floor(c, out int skill, out int _) ? StudyRules.Points(d.Study,
            skill) : 0;

        private static string KindName(CourseDef c) => CareersText.Kind(c.Kind);

        private static UiColumn Col(string title, bool grow = false, bool right = false) => new UiColumn { Title = title, Grow = grow,
            Width = grow ? 0f : 184f, Right = right };

        private WorkTab Studying(CareersDirector d, StudyFile f, int day)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { TableTitle = w.T("reallife_campus_tab_studying", "Studying"), Lead = w.T("reallife_work_nothing_studying",
                "Nothing under way. Enrol on a course and its classes, homework and exam appear here."),
                Columns = new[] { Col(w.T("reallife_campus_col_course", "Course"), true), Col(w.T("reallife_campus_col_next", "Next class")),
                    Col(w.T("reallife_campus_col_attended", "Attended")), Col(w.T("reallife_campus_col_exam", "Exam")) } };
            for (int i = 0; i < f.Courses.Count; i++)
            {
                Enrolment e = f.Courses[i];
                CourseDef c = e.Def;
                if (c == null)
                {
                    continue;
                }
                int next = StudyRules.NextClass(e, day, CareersGame.MinuteOfDay, -1);
                string attended = e.Seen > 0 ? (e.Done * 100 / e.Seen).ToString(Inv) + "%" : "-";
                var r = new WorkRow { Id = c.Id, Icon = UiIcon.Cap, Cells = new[] { CareersText.Course(c.Id),
                    next > 0 ? CareersText.Day(next / (24 * 60)) : "-", attended, e.ExamTo > 0 ? CareersText.Day(e.ExamTo) : "-" },
                    Status = e.At == EnrolStage.Exam || e.At == EnrolStage.Retake ? w.T("reallife_work_exam_open",
                        "Exam open") : w.T("reallife_work_under_way", "Under way"), Tone = UiRole.Accent,
                    Detail = w.F("reallife_work_studying_line",
                        "{course}: {done} of {seen} classes attended. Classes are sat at your desk; the exam is sat at the school.", "course",
                        CareersText.Course(c.Id), "done", e.Done.ToString(Inv), "seen", e.Seen.ToString(Inv)) };
                r.Acts.Add(new WorkAct { Kind = Pay, Label = w.T("reallife_campus_button_pay", "Pay what is owed"), Style = UiButtonKind.Primary,
                    Icon = UiIcon.Check, Why = e.FeeLate ? null : w.T("reallife_work_nothing_owed", "Nothing is owed.") });
                r.Acts.Add(new WorkAct { Kind = Withdraw, Label = w.T("reallife_campus_button_withdraw", "Withdraw"),
                    Style = UiButtonKind.Destructive, Icon = UiIcon.Close });
                t.Rows.Add(r);
            }
            if (f.GraduationCourse >= 0 && f.GraduationDay > 0)
            {
                t.Acts.Add(new WorkAct { Kind = Graduate, Label = w.T("reallife_campus_button_graduate", "Graduate"), Style = UiButtonKind.Primary,
                    Icon = UiIcon.Check, Why = day >= f.GraduationDay ? null : w.T("reallife_work_not_yet_graduate", "The ceremony is not yet.") });
            }
            return t;
        }

        private WorkTab Results(CareersDirector d, StudyFile f, int day)
        {
            UtilitiesWords w = W;
            var marks = new List<Mark>(16);
            d.Marks(marks);
            int gpa = RosterRules.Gpa(marks);
            var t = new WorkTab { TableTitle = w.T("reallife_work_transcript", "Transcript"), Lead = w.T("reallife_work_no_results",
                "No results yet. A finished course adds its grade, credits and your place in the class here."),
                Columns = new[] { Col(w.T("reallife_campus_col_course", "Course"), true), Col(w.T("reallife_work_col_credits", "Credits")),
                    Col(w.T("reallife_campus_col_exam", "Exam")), Col(w.T("reallife_work_col_grade", "Grade"), false, true) } };
            int credits = 0;
            for (int i = f.Results.Count - 1; i >= 0; i--)
            {
                Enrolment e = f.Results[i];
                CourseDef c = e.Def;
                if (c == null)
                {
                    continue;
                }
                bool passed = e.Stage == (int)EnrolStage.Passed;
                credits += passed ? RosterRules.CreditsOf(c) : 0;
                t.Rows.Add(new WorkRow { Id = c.Id, Icon = UiIcon.Cap, Cells = new[] { CareersText.Course(c.Id),
                    RosterRules.CreditsOf(c).ToString(Inv), e.Score.ToString(Inv), RosterRules.LetterOf(e.Score) + " " + e.Score.ToString(Inv) + "%" },
                    Status = passed ? w.T("reallife_work_passed", "Passed") : w.T("reallife_work_not_passed", "Not passed"),
                        Tone = passed ? UiRole.Ok : UiRole.Warn, Grade = e.Score,
                    Detail = w.F("reallife_work_result_line", "{course}: {letter}, {score}%.", "course", CareersText.Course(c.Id), "letter",
                        RosterRules.LetterOf(e.Score), "score", e.Score.ToString(Inv)) });
            }
            var coming = new List<UiFigureModel>(3);
            for (int i = 0; i < f.Courses.Count && coming.Count < 3; i++)
            {
                Enrolment e = f.Courses[i];
                if (e.Def != null && e.ExamTo >= day && (e.At == EnrolStage.Exam || e.At == EnrolStage.Retake || e.ExamFrom > 0))
                {
                    int from = Math.Max(e.ExamFrom, day);
                    coming.Add(new UiFigureModel { Label = w.F("reallife_work_exam_of", "Exam: {course}", "course", CareersText.Course(e.Course)),
                        Value = w.F("reallife_work_in_days", "{day}, in {n} days", "day", CareersText.Day(from), "n", Math.Max(0, from - day).ToString(Inv)) });
                }
            }
            t.Figures = coming.ToArray();
            if (t.Rows.Count > 0)
            {
                string you = RealLifeOverhaul.Rivals.RivalGame.PlayerName();
                t.Profile = new UiProfileModel { Name = you, Hue = UiColour.HueOf(you), Role = w.T("reallife_work_student", "Student"), RoleIcon = UiIcon.Cap,
                    FactLabels = new[] { w.T("reallife_work_gpa", "Grade point average"), w.T("reallife_work_credits_earned", "Credits earned") },
                    FactValues = new[] { gpa >= 0 ? (gpa / 100f).ToString("0.0", Inv) : "-", credits.ToString(Inv) } };
                Enrolment last = f.Results[f.Results.Count - 1];
                Intake intake = d.IntakeOf(last);
                var list = new List<ClassLine>(RosterRules.SeatsDefault + 1);
                int rank = RosterRules.Rank(intake, you, Math.Max(0, gpa), list);
                t.Profile.FactLabels = new[] { t.Profile.FactLabels[0], t.Profile.FactLabels[1], w.T("reallife_work_class_rank", "Class rank") };
                t.Profile.FactValues = new[] { t.Profile.FactValues[0], t.Profile.FactValues[1], w.F("reallife_work_rank_of", "{rank} of {all}",
                    "rank", rank.ToString(Inv), "all", list.Count.ToString(Inv)) };
                var quarters = new List<int>(8);
                var averages = new List<int>(8);
                if (RosterRules.ByTerm(marks, GameCalendar.DaysPerYear, quarters, averages) >= 2)
                {
                    var pts = new string[averages.Count];
                    var vals = new float[averages.Count];
                    for (int k = 0; k < pts.Length; k++)
                    {
                        pts[k] = w.F("reallife_work_quarter_n", "Quarter {n}", "n", (quarters[k] + 1).ToString(Inv));
                        vals[k] = averages[k] / 100f;
                    }
                    t.Chart = new UiChartModel { Kind = UiChartKind.Line, Title = w.T("reallife_work_gpa_by_term", "Grade point average by quarter"), Points = pts,
                        Series = new[] { new UiSeries { Label = w.T("reallife_work_gpa", "Grade point average"), Values = vals, Tint = UiRole.Accent } }, Format = v => v.ToString("0.0", Inv) };
                }
                t.PeopleTitle = w.T("reallife_work_your_class", "Your class");
                t.PeopleCount = w.T("reallife_work_by_gpa", "by grade point average");
                for (int i = 0; i < list.Count && i < 8; i++)
                {
                    t.People.Add(new WorkPerson { Name = list[i].You ? list[i].Name + " " + w.T("reallife_work_you_mark", "(you)") : list[i].Name,
                        Role = (i + 1).ToString(Inv), Label = w.T("reallife_work_gpa", "Grade point average"), Score = list[i].Gpa / 4 });
                }
            }
            return t;
        }

        private WorkTab SkillsTab(StudyFile f)
        {
            UtilitiesWords w = W;
            var labels = new List<string>(Skills.Count);
            var values = new List<int>(Skills.Count);
            for (int s = 0; s < Skills.Count; s++)
            {
                int points = StudyRules.Points(f, s);
                if (points > 0)
                {
                    labels.Add(CareersText.Skill(s));
                    values.Add(Math.Min(100, points));
                }
            }
            return new WorkTab { Lead = labels.Count == 0 ? w.T("reallife_work_no_skills",
                "No skills yet. Courses and work teach them.") : w.T("reallife_work_skills_lead", "What your courses and work have taught you."),
                AttrTitle = w.T("reallife_campus_tab_skills", "Skills"), AttrLabels = labels.ToArray(), AttrValues = values.ToArray() };
        }

        private WorkTab Family(CareersDirector d)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { TableTitle = w.T("reallife_campus_tab_family", "Family"), Lead = w.T("reallife_work_no_children",
                "No children at university yet."),
                Columns = new[] { Col(w.T("reallife_work_col_child", "Child"), true), Col(w.T("reallife_campus_col_course", "Course")),
                    Col(w.T("reallife_work_col_state", "State")) } };
            IReadOnlyList<ChildView> kids = d.Kids();
            for (int i = 0; kids != null && i < kids.Count; i++)
            {
                ChildStudy c = d.ChildOf(kids[i].Name);
                var r = new WorkRow { Id = i, Icon = UiIcon.Heart, Cells = new[] { kids[i].Name, c != null
                    && c.Course >= 0 ? CareersText.Course(c.Course) : "-", c != null ? c.At.ToString() : "-" }, Detail = kids[i].Name };
                if (c != null && c.At == ChildStep.Offered)
                {
                    r.Acts.Add(new WorkAct { Kind = PayChild, Arg = i, Label = w.T("reallife_campus_button_child_pay", "Pay for the course"),
                        Style = UiButtonKind.Primary, Icon = UiIcon.Check });
                    r.Acts.Add(new WorkAct { Kind = DeclineChild, Arg = i, Label = w.T("reallife_campus_button_child_decline", "Decline"),
                        Style = UiButtonKind.Destructive, Icon = UiIcon.Close });
                }
                t.Rows.Add(r);
            }
            string whyTutor = d.CanTutor(out int tutorSkill);
            t.Acts.Add(new WorkAct { Kind = Tutor, Label = w.T("reallife_campus_button_tutor", "Tutor a pupil"), Style = UiButtonKind.Secondary, Icon = UiIcon.Cap,
                Why = whyTutor == null ? null : w.T(whyTutor, "You cannot tutor now.") });
            return t;
        }

        // The city's schools as a market (Scripts/Schools): league order, fee, pupils against places, results and the inspection's grade.
        private WorkTab Schools(CareersDirector d)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { TableTitle = w.T("reallife_campus_tab_schools", "Schools"), Lead = w.T("reallife_work_no_schools",
                "No schools are open yet."),
                Columns = new[] { Col(w.T("reallife_campus_col_school", "School"), true), Col(w.T("reallife_career_col_where", "Where")),
                    Col(w.T("reallife_work_col_pupils", "Pupils")), Col(w.T("reallife_work_col_results", "Results"), false, true) } };
            RealLifeOverhaul.Schools.SchoolState s = RealLifeOverhaul.Schools.SchoolsDirector.State;
            if (!RealLifeOverhaul.Schools.SchoolsDirector.Running || s == null)
            {
                return t;
            }
            int[] order = RealLifeOverhaul.Schools.SchoolRules.League(s);
            for (int r = 0; r < order.Length; r++)
            {
                RealLifeOverhaul.Schools.SchoolFile f = s.Files[order[r]];
                RealLifeOverhaul.Schools.SchoolDef def = RealLifeOverhaul.Schools.SchoolRules.Def(f.Id);
                t.Rows.Add(new WorkRow { Id = f.Id, Icon = UiIcon.Building, Cells = new[] { RealLifeOverhaul.Schools.SchoolsGame.Name(f.Id),
                    RealLifeOverhaul.Schools.SchoolsGame.District(def.District),
                    f.Pupils.ToString(Inv) + " / " + f.Places.ToString(Inv), f.Results >= 0 ? f.Results.ToString(Inv) + "%" : "-" },
                    Status = RealLifeOverhaul.Schools.SchoolsText.Grade(f.Grade),
                        Tone = f.Grade <= (int)RealLifeOverhaul.Schools.SchoolGrade.Good ? UiRole.Ok : UiRole.Warn, Grade = f.Results,
                    Detail = RealLifeOverhaul.Schools.SchoolsGame.Name(f.Id) + ". " + RealLifeOverhaul.Schools.SchoolsText.Owner(f.Owner) });
            }
            t.GradeColumn = -1;
            return t;
        }

        // ------------------------------------------------------------------------------------------------- doing

        public bool Do(WorkDo act, WorkView view)
        {
            CareersDirector d = CareersDirector.Instance;
            if (d == null)
            {
                return false;
            }
            try
            {
                string why = null;
                switch (act.Kind)
                {
                    case Enrol: why = d.Enrol(act.Arg, StudyMode.FullTime, false); break;
                    case Pay: why = d.PayOwed(act.Arg); break;
                    case Withdraw: d.Withdraw(act.Arg); break;
                    case Graduate: why = d.Graduate(); break;
                    case Tutor: why = d.Tutor(); break;
                    case PayChild: why = d.PayChild(ChildName(d, act.Arg)); break;
                    case DeclineChild: why = d.DeclineChild(ChildName(d, act.Arg)); break;
                    default: return false;
                }
                if (why != null)
                {
                    Notifications.Show(NotificationType.Error, why, d.TakeWhyData(), 8f);
                    return false;
                }
                _local++;
                return true;
            }
            catch (Exception e)
            {
                CareersDirector.Logger?.Warn("Campus (kit): " + act.Kind + " failed: " + e.Message);
                return false;
            }
        }

        private static string ChildName(CareersDirector d, int i)
        {
            IReadOnlyList<ChildView> kids = d.Kids();
            return kids != null && i >= 0 && i < kids.Count ? kids[i].Name : string.Empty;
        }
    }
}
