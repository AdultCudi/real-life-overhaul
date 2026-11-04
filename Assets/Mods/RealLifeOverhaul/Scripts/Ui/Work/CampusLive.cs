using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.DaySeries;
using RealLifeOverhaul.Tech;
using UI.Notification;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Campus on the kit: the catalogue with its seats, teacher and what each course asks and gives, the term's diary with the courses under way above
    /// it, the transcript with credits, the grade point average by quarter and the player's place in the class, the skills with where each is taught, the
    /// family and the schools. Read from the careers' own records; every button goes to the director that owns it. Enrol and the classes are not starts:
    /// the classes begin at the desk. Nothing a frame.
    /// </summary>
    public sealed class CampusLive : IWorkSource
    {
        public static readonly CampusLive Instance = new CampusLive();

        // the actions of this app, from 100 (the screen's own are 1 to 3)
        private const int Enrol = 100, Pay = 101, Withdraw = 102, Graduate = 103, PayChild = 104, DeclineChild = 105, Tutor = 106, ShowSchool = 107, ShowMain = 108, OpenTeacher = 109;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly StudyMode[] Modes = { StudyMode.FullTime, StudyMode.PartTime, StudyMode.Distance };
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
            CareersDirector d = CareersDirector.Instance;
            GameInstance save = SaveGameManager.Current;
            if (_app.Ready && d != null && save != null)
            {
                _app.Tabs[0] = Courses(d, d.Study, save.Day, view);
                _app.Tabs[1] = Studying(d, d.Study, save.Day, view);
            }
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
            _app = new WorkApp { Ready = d != null && f != null && save != null, OffTitle = W.T("reallife_work_campus_off_title", "Studying is switched off"),
                OffBody = W.T("reallife_work_campus_off", "Load a save with studying switched on to see your courses.") };
            UtilitiesWords w = W;
            _app.Nav = new[]
            {
                new UiNavEntry { Icon = UiIcon.Books, Label = w.T("reallife_campus_tab_courses", "Courses") },
                new UiNavEntry { Icon = UiIcon.Calendar, Label = w.T("reallife_campus_tab_studying", "Studying") },
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
            _app.Tabs = new[] { new WorkTab(), new WorkTab(), Results(d, f, day), SkillsTab(d, f), Family(d), Schools(d) };
            int live = 0;
            for (int i = 0; i < f.Courses.Count; i++)
            {
                live += f.Courses[i].Def != null ? 1 : 0;
            }
            _app.Nav[1].Count = live > 0 ? live.ToString(Inv) : null;
            _app.Nav[5].Count = _app.Tabs[5].Rows.Count > 0 ? _app.Tabs[5].Rows.Count.ToString(Inv) : null;
        }

        private static string SubjectName(UtilitiesWords w, int subject)
        {
            switch (subject)
            {
                case 0: return w.T("reallife_work_subject_business", "Business");
                case 1: return w.T("reallife_work_subject_tech", "Tech");
                case 2: return w.T("reallife_work_subject_trades", "Trades");
                case 3: return w.T("reallife_work_subject_food", "Food and drink");
                default: return w.T("reallife_work_subject_care", "Care and service");
            }
        }

        // ------------------------------------------------------------------------------------------------- Courses

        private WorkTab Courses(CareersDirector d, StudyFile f, int day, WorkView view)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Cards, Search = w.T("reallife_work_search_courses", "Search courses"), AllWord = w.T("reallife_work_filter_all_word", "All"),
                Chips = new[] { SubjectName(w, 0), SubjectName(w, 1), SubjectName(w, 2), SubjectName(w, 3), SubjectName(w, 4) },
                EmptyTitle = w.T("reallife_work_empty_courses_title", "No course matches"), EmptyBody = w.T("reallife_work_empty_courses", "Another subject, or a shorter search, may show one.") };
            long cash = HustleCashOf();
            RealLifeOverhaul.Careers.Job job = d.Work != null ? d.Work.Job : null;
            for (int i = 0; i < StudyCatalogue.All.Length; i++)
            {
                CourseDef c = StudyCatalogue.All[i];
                if (!CareersDirector.Offered(c))
                {
                    continue;
                }
                Intake intake = d.IntakeOf(c);
                int fee = Prices.At(StudyRules.Fee(c, d.GrantFor(c)));
                string feeWords = CareersText.Money(fee);
                bool live = f.Live(c.Id) != null;
                int skillNow = SkillPoints(d, c);
                string why = live ? w.T("reallife_study_why_already", "You are on that course already.") : f.Holds(c.Id) ? w.T("reallife_study_why_passed", "You have passed that course already.")
                    : !RosterRules.FloorMet(c, skillNow) ? w.T("reallife_study_why_floor", "A skill the course asks for is under its floor.") : intake != null
                        && !RosterRules.CanJoin(intake) ? w.T("reallife_study_why_seats", "Every seat of this course is taken.") : null;
                int seatsLeft = intake != null ? RosterRules.Left(intake) : -1;
                string tag = CareersText.Kind(c.Kind);
                var r = new WorkRow { Id = c.Id, Icon = WorkGlyphs.Course(c), Name = CareersText.Course(c.Id), Sub = d.PlaceOf(c, StudyMode.FullTime), Line = CareersText.About(c), Status = tag,
                    Tone = c.Kind == CourseKind.Short || c.Kind == CourseKind.Certificate ? UiRole.Muted : UiRole.Accent, Group = WorkGlyphs.Subject(c), FootRight = feeWords,
                    FootLeft = c.Timetabled ? w.F("reallife_work_n_weeks", "{n} weeks", "n", Math.Max(1, StudyRules.Length(c,
                        StudyMode.FullTime) / 7).ToString(Inv)) : CareersText.Mode(StudyMode.Distance) };
                if (c.Timetabled)
                {
                    r.NextLabel = w.T("reallife_work_next_class", "Next class");
                    r.NextValue = CareersText.Timetable(StudyRules.Days(c, StudyMode.FullTime), StudyRules.Hour(c, StudyMode.FullTime));
                }
                r.Title = r.Name;
                r.HeadSub = r.Sub;
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_campus_col_length", "Length"), r.FootLeft));
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_campus_col_fee", "Fee"), feeWords));
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_classes", "Classes"), c.Timetabled ? CareersText.Timetable(StudyRules.Days(c, StudyMode.FullTime),
                    StudyRules.Hour(c, StudyMode.FullTime)) : "-"));
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_seats", "Seats left"), intake != null ? (seatsLeft == 0 ? w.T("reallife_work_full", "Full")
                    : w.F("reallife_work_seats_of", "{n} of {all}", "n", seatsLeft.ToString(Inv), "all", intake.Capacity.ToString(Inv))) : "-"));
                var learn = new WorkSection { Part = WorkPart.Bars, Title = w.T("reallife_work_you_will_learn", "What you will learn"),
                    Note = w.T("reallife_work_skill_after", "skill after the course") };
                learn.Bars.Add(new WorkBar { Label = CareersText.Skill(c.SkillA), Value = Math.Min(100, StudyRules.Points(f, c.SkillA)), Note = "+" + c.GainA.ToString(Inv) });
                if (c.GainB > 0)
                {
                    learn.Bars.Add(new WorkBar { Label = CareersText.Skill(c.SkillB), Value = Math.Min(100, StudyRules.Points(f, c.SkillB)), Note = "+" + c.GainB.ToString(Inv) });
                }
                r.Sections.Add(learn);
                var before = new WorkSection { Part = WorkPart.Checks, Title = w.T("reallife_work_before_enrol", "Before you enrol") };
                before.Checks.Add(new WorkNeed { Words = w.F("reallife_work_you_have_fee", "You have the {fee} fee", "fee", feeWords), Met = cash >= fee,
                    Have = cash >= fee ? null : w.F("reallife_work_short_by_n", "short by {n}", "n", CareersText.Money(fee - cash)) });
                if (job != null && c.Timetabled)
                {
                    WorkFit fit = StudyRules.Fit(c, StudyMode.FullTime, job.Def, out int _, out int _);
                    before.Checks.Add(new WorkNeed { Words = fit == WorkFit.Clash ? w.T("reallife_work_clash",
                        "Your shifts clash with the classes") : w.T("reallife_work_evenings_free", "Your evenings are free"),
                        Met = fit != WorkFit.Clash });
                }
                if (RosterRules.Floor(c, out int fs, out int fn))
                {
                    before.Checks.Add(new WorkNeed { Words = w.F("reallife_work_skill_floor", "{skill} {have}, the course asks for {need}", "skill",
                        CareersText.Skill(fs), "have", skillNow.ToString(Inv), "need", fn.ToString(Inv)),
                        Met = skillNow >= fn });
                }
                r.Sections.Add(before);
                if (intake != null)
                {
                    for (int s = 0; s < intake.Students.Count; s++)
                    {
                        if (intake.Students[s].Employee)
                        {
                            r.Sections.Add(new WorkSection { Part = WorkPart.Text, Text = w.F("reallife_work_employee_in_class",
                                "{name} from your team is in this class.", "name", intake.Students[s].Name) });
                            break;
                        }
                    }
                }
                Tutor tutor = d.TutorOf(c);
                if (tutor != null)
                {
                    var teacher = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_your_teacher_title", "Your teacher") };
                    teacher.People.Add(new WorkPerson { Name = tutor.Name, Role = w.F("reallife_work_teacher_line",
                        "{years} years teaching, rated {rating} by students", "years", tutor.Years.ToString(Inv),
                        "rating", (tutor.Rating / 10f).ToString("0.0", Inv)) });
                    r.Sections.Add(teacher);
                    if (tutor.PersonId > 0)
                    {
                        r.Acts.Add(new WorkAct { Kind = OpenTeacher, Arg = tutor.PersonId, Label = w.T("reallife_work_open_teacher", "Teacher's page"), Style = UiButtonKind.Secondary,
                            Icon = UiIcon.Person, Opens = true });
                    }
                }
                r.HintLead = w.T("reallife_work_classes_desk_lead", "Classes start at your desk.");
                r.Hint = w.T("reallife_work_classes_desk_line", "Click it in the classroom when the bell rings, in the game's own work panel.");
                r.HintIcon = UiIcon.Person;
                r.Acts.Add(new WorkAct { Kind = Enrol, Arg = c.Id * 8, Label = seatsLeft == 0 && why != null ? w.T("reallife_work_full",
                    "Full") : w.F("reallife_work_enrol_for", "Enrol, {fee}", "fee", feeWords),
                    Style = UiButtonKind.Primary, Icon = UiIcon.Check, Why = why });
                if (c.Loanable && CareersDirector.UniversityOpen)
                {
                    r.Acts.Add(new WorkAct { Kind = Enrol, Arg = c.Id * 8 + 4, Label = w.T("reallife_campus_button_loan", "Enrol on a student loan"),
                        Style = UiButtonKind.Secondary, Icon = UiIcon.Bank, Why = why });
                }
                r.Acts.Add(new WorkAct { Kind = ShowSchool, Arg = (int)c.Campus, Label = w.T("reallife_work_show_school", "Show school"),
                    Style = UiButtonKind.Secondary, Icon = UiIcon.Locate, Opens = true,
                    Why = c.Campus == Campus.Online || c.Campus == Campus.Hospital ? w.T("reallife_work_no_school_place",
                        "This course is taught online, or on a ward, so there is no school to show.") : null });
                t.Rows.Add(r);
            }
            ChooseMode(d, f, t, view, w);
            return t;
        }

        // The timetable the player may follow the chosen course on (full time, part time, at a distance): a selector in its detail, and the mode in its Enrol buttons.
        private static void ChooseMode(CareersDirector d, StudyFile f, WorkTab t, WorkView view, UtilitiesWords w)
        {
            WorkRow chosen = WorkTabPages.ChosenRow(t, view);
            if (chosen == null)
            {
                return;
            }
            CourseDef c = StudyCatalogue.Find(chosen.Id);
            var offered = new List<StudyMode>(3);
            foreach (StudyMode m in Modes)
            {
                if (m == StudyMode.PartTime && (!c.Timetabled || c.Kind == CourseKind.Short))
                {
                    continue;
                }
                if (m == StudyMode.Distance && (!c.Distance || c.Campus == Campus.Online))
                {
                    continue;
                }
                offered.Add(m);
            }
            if (offered.Count < 2)
            {
                return;
            }
            var words = new string[offered.Count];
            for (int i = 0; i < words.Length; i++)
            {
                words[i] = CareersText.Mode(offered[i]);
            }
            int at = Math.Max(0, Math.Min(words.Length - 1, view.Get(0, 0)));
            t.Amounts.Add(new WorkAmount { Id = 0, Value = at, Min = 0, Max = words.Length - 1, Words = words, Figure = words[at] });
            chosen.Sections.Insert(0, new WorkSection { Part = WorkPart.Amount, Title = w.T("reallife_campus_col_mode", "Mode"), Stepper = 0 });
            for (int i = 0; i < chosen.Acts.Count; i++)
            {
                if (chosen.Acts[i].Kind == Enrol)
                {
                    chosen.Acts[i].Arg = c.Id * 8 + (int)offered[at] + ((chosen.Acts[i].Arg & 4) != 0 ? 4 : 0);   // the course, the way it is followed and whether the loan pays
                }
            }
        }

        private static long HustleCashOf() => RealLifeOverhaul.Hustles.HustleGame.Cash;

        private static int SkillPoints(CareersDirector d, CourseDef c) => RosterRules.Floor(c, out int skill, out int _) ? StudyRules.Points(d.Study, skill) : 0;

        // ------------------------------------------------------------------------------------------------- Studying

        private WorkTab Studying(CareersDirector d, StudyFile f, int day, WorkView view)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Diary };
            var live = new List<Enrolment>(4);
            for (int i = 0; i < f.Courses.Count; i++)
            {
                Enrolment e = f.Courses[i];
                if (e.Def == null)
                {
                    continue;
                }
                live.Add(e);
                CourseDef c = e.Def;
                int next = StudyRules.NextClass(e, day, CareersGame.MinuteOfDay, -1);
                string attended = e.Seen > 0 ? (e.Done * 100 / e.Seen).ToString(Inv) + "%" : "-";
                bool examOpen = e.At == EnrolStage.Exam || e.At == EnrolStage.Retake;
                var r = new WorkRow { Id = c.Id, Icon = WorkGlyphs.Course(c), Name = CareersText.Course(c.Id), Sub = d.PlaceOf(c, (StudyMode)e.Mode),
                    Status = examOpen ? w.T("reallife_work_exam_open", "Exam open") : w.T("reallife_work_under_way", "Under way"), Tone = examOpen ? UiRole.Warn : UiRole.Accent };
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_campus_col_next", "Next class"),
                    next > 0 ? CareersText.Day(next / (24 * 60)) + " " + CareersText.TimeOfDay(next % (24 * 60)) : "-"));
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_campus_col_attended", "Attended"), attended));
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_homework", "Homework"), w.F("reallife_work_n_of_n", "{n} of {all}",
                    "n", e.Homework.ToString(Inv), "all", StudyRules.HomeworkSet(e).ToString(Inv))));
                r.Acts.Add(new WorkAct { Kind = Pay, Arg = c.Id, Label = w.T("reallife_campus_button_pay", "Pay what is owed"), Style = UiButtonKind.Secondary, Icon = UiIcon.Coin,
                    Why = e.FeeLate ? null : w.T("reallife_work_nothing_owed", "Nothing is owed.") });
                r.Acts.Add(new WorkAct { Kind = Withdraw, Arg = c.Id, Label = w.T("reallife_campus_button_withdraw", "Withdraw"), Style = UiButtonKind.Destructive, Icon = UiIcon.Close });
                t.Rows.Add(r);
            }
            if (f.GraduationCourse >= 0 && f.GraduationDay > 0)
            {
                t.Acts.Add(new WorkAct { Kind = Graduate, Label = w.T("reallife_campus_button_graduate", "Attend graduation"), Style = UiButtonKind.Primary, Icon = UiIcon.Check,
                    Why = day >= f.GraduationDay ? null : w.T("reallife_work_not_yet_graduate", "The ceremony is not yet.") });
            }
            t.Diary = new WorkDiary { Month = DiaryOf(live, day, view) };
            return t;
        }

        private static readonly string[] MonthKeys =
        {
            "reallife_calendar_month_1", "reallife_calendar_month_2", "reallife_calendar_month_3", "reallife_calendar_month_4", "reallife_calendar_month_5", "reallife_calendar_month_6",
            "reallife_calendar_month_7", "reallife_calendar_month_8", "reallife_calendar_month_9", "reallife_calendar_month_10", "reallife_calendar_month_11", "reallife_calendar_month_12"
        };

        // The term as a month: each class on its day and hour, the exam window's ends, and the day chosen beside it.
        private static UiDiaryModel DiaryOf(List<Enrolment> live, int today, WorkView view)
        {
            int year = GameCalendar.DaysPerYear;
            int month = CalendarRules.MonthOf(today, year) + view.Month;
            int from = CalendarRules.MonthStart(month, year), length = CalendarRules.MonthLength(month, year);
            int inYear = CalendarRules.MonthInYear(month, year);
            string name = UiText.T(MonthKeys[Math.Max(0, Math.Min(MonthKeys.Length - 1, inYear - 1))]);
            bool thisMonth = today >= from && today < from + length;
            int chosen = view.Day > 0 ? Math.Min(view.Day, length) : thisMonth ? today - from + 1 : 1;
            var entries = new List<UiDiaryEntry>(64);
            for (int e = 0; e < live.Count; e++)
            {
                Enrolment en = live[e];
                CourseDef c = en.Def;
                var mode = (StudyMode)en.Mode;
                if (c.Timetabled)
                {
                    int days = StudyRules.Days(c, mode), hour = StudyRules.Hour(c, mode), last = en.Start + StudyRules.Length(c, mode) + 14;
                    for (int day = Math.Max(from, en.Start); day < from + length && day <= last; day++)
                    {
                        if (StudyRules.On(days, day))
                        {
                            entries.Add(new UiDiaryEntry { Day = day - from + 1, Icon = WorkGlyphs.Course(c), Title = W.F("reallife_work_diary_class",
                                "Class: {course}", "course", CareersText.Course(c.Id)), Value = CareersText.Hour(hour) });
                        }
                    }
                }
                if (en.ExamFrom >= from && en.ExamFrom < from + length)
                {
                    entries.Add(new UiDiaryEntry { Day = en.ExamFrom - from + 1, Icon = UiIcon.Scales, Title = W.F("reallife_work_diary_exam", "Exam: {course}", "course", CareersText.Course(c.Id)),
                        Value = CareersText.Hour(StudyCatalogue.ExamOpens) });
                }
                if (en.ExamTo >= from && en.ExamTo < from + length && en.ExamTo != en.ExamFrom)
                {
                    entries.Add(new UiDiaryEntry { Day = en.ExamTo - from + 1, Icon = UiIcon.Scales, Title = W.F("reallife_work_diary_exam_closes",
                        "Exam closes: {course}", "course", CareersText.Course(c.Id)) });
                }
            }
            entries.Sort((a, b) => a.Day.CompareTo(b.Day));
            string[] weekdays = { UiText.T("common_monday"), UiText.T("common_tuesday"), UiText.T("common_wednesday"), UiText.T("common_thursday"),
                UiText.T("common_friday"), UiText.T("common_saturday"),
                UiText.T("common_sunday") };
            for (int i = 0; i < weekdays.Length; i++)
            {
                weekdays[i] = weekdays[i].Length > 3 ? weekdays[i].Substring(0, 3) : weekdays[i];
            }
            return new UiDiaryModel
            {
                Month = name, Weekdays = weekdays, Lead = RealLifeOverhaul.Calendar.MonthViewRules.Lead(from), Length = length,
                    Today = thisMonth ? today - from + 1 : 0, Chosen = chosen, PastBefore = thisMonth ? today - from + 1 : 0,
                Entries = entries.ToArray(),
                    ChosenTitle = weekdays[(RealLifeOverhaul.Calendar.MonthViewRules.Lead(from) + chosen - 1) % 7] + " " + chosen.ToString(Inv) + " " + name,
                    ChosenEmpty = UiText.T("reallife_work_diary_empty"),
                AheadTitle = UiText.T("reallife_work_diary_ahead"), CanBack = true, CanOn = true
            };
        }

        // ------------------------------------------------------------------------------------------------- Results

        private WorkTab Results(CareersDirector d, StudyFile f, int day)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Statement };
            var s = new WorkStatement { EmptyTitle = w.T("reallife_work_results_empty_title", "No results yet"),
                EmptyBody = w.T("reallife_work_no_results", "A finished course adds its grade, credits and your place in the class here.") };
            t.Statement = s;
            var marks = new List<Mark>(16);
            d.Marks(marks);
            int gpa = RosterRules.Gpa(marks);
            string you = RealLifeOverhaul.Rivals.RivalGame.PlayerName();
            int quarter = Math.Max(1, GameCalendar.DaysPerYear / 4);
            var terms = new SortedDictionary<int, WorkTerm>();
            int credits = 0, results = 0;
            for (int i = 0; i < f.Results.Count; i++)
            {
                Enrolment e = f.Results[i];
                CourseDef c = e.Def;
                if (c == null)
                {
                    continue;
                }
                results++;
                bool passed = e.Stage == (int)EnrolStage.Passed, failed = e.Stage == (int)EnrolStage.Failed;
                credits += passed ? RosterRules.CreditsOf(c) : 0;
                int term = e.Start / quarter;
                AddMark(terms, term, quarter, w, new WorkMark { Id = c.Id, Icon = WorkGlyphs.Course(c), Course = CareersText.Course(c.Id), Place = d.PlaceOf(c, (StudyMode)e.Mode),
                    Credits = RosterRules.CreditsOf(c).ToString(Inv), Attendance = StudyRules.Attendance(e).ToString(Inv) + "%", AttendanceGrade = StudyRules.Attendance(e),
                    Exam = passed || failed ? e.Score.ToString(Inv) : "-", ExamGrade = passed || failed ? e.Score : -1, Letter = passed || failed ? RosterRules.LetterOf(e.Score) : null,
                    Percent = passed || failed ? e.Score.ToString(Inv) + "%" : "-", LetterGrade = passed || failed ? e.Score : -1,
                    Status = passed ? (c.Cert >= 0 || c.Kind == CourseKind.Certificate ? w.T("reallife_work_licence_won", "Licence won") : w.T("reallife_work_passed", "Passed"))
                        : failed ? w.T("reallife_work_repeat", "Repeat") : w.T("reallife_work_left", "Left"), Tone = passed ? UiRole.Ok : failed ? UiRole.Warn : UiRole.Muted });
            }
            for (int i = 0; i < f.Courses.Count; i++)
            {
                Enrolment e = f.Courses[i];
                CourseDef c = e.Def;
                if (c == null)
                {
                    continue;
                }
                results++;
                AddMark(terms, e.Start / quarter, quarter, w, new WorkMark { Id = c.Id, Icon = WorkGlyphs.Course(c), Course = CareersText.Course(c.Id), Place = d.PlaceOf(c, (StudyMode)e.Mode),
                    Credits = RosterRules.CreditsOf(c).ToString(Inv), Attendance = StudyRules.Attendance(e).ToString(Inv) + "%", AttendanceGrade = StudyRules.Attendance(e), Exam = "-", Percent = "-",
                    Status = w.T("reallife_work_under_way", "Under way"), Tone = UiRole.Accent });
            }
            if (results == 0)
            {
                return t;
            }
            Enrolment last = f.Results.Count > 0 ? f.Results[f.Results.Count - 1] : null;
            string campus = last != null && last.Def != null ? CareersText.Campus(last.Def.Campus) : string.Empty;
            s.Head = new UiProfileModel { Name = you, Hue = UiColour.HueOf(you), Role = w.T("reallife_work_student",
                "Student") + (campus.Length > 0 ? ", " + campus : string.Empty), Ring = UiRole.Ok };
            Intake intake = last != null ? d.IntakeOf(last) : null;
            var list = new List<ClassLine>(RosterRules.SeatsDefault + 1);
            int rank = RosterRules.Rank(intake, you, Math.Max(0, gpa), list);
            s.FigureLabels = new[] { w.T("reallife_work_gpa_short", "GPA"), w.T("reallife_work_credits_word", "Credits"), w.T("reallife_work_class_rank", "Class rank") };
            s.FigureValues = new[] { gpa >= 0 ? (gpa / 100f).ToString("0.0", Inv) : "-", credits.ToString(Inv), w.F("reallife_work_rank_of",
                "{rank} of {all}", "rank", rank.ToString(Inv), "all", list.Count.ToString(Inv)) };
            s.TranscriptTitle = w.T("reallife_work_transcript", "Transcript");
            s.Heads = new[] { w.T("reallife_campus_col_course", "Course"), w.T("reallife_work_col_credits", "Credits"), w.T("reallife_work_col_attendance", "Attendance"),
                w.T("reallife_campus_col_exam", "Exam"), w.T("reallife_work_col_grade", "Grade"), w.T("reallife_work_col_state", "State") };
            foreach (KeyValuePair<int, WorkTerm> term in terms)
            {
                s.Terms.Add(term.Value);
            }
            s.TotalLabel = w.T("reallife_work_total_label", "Credits earned, grade point average");
            s.TotalCredits = w.F("reallife_work_n_credits", "{n} credits", "n", credits.ToString(Inv));
            s.TotalGpa = gpa >= 0 ? (gpa / 100f).ToString("0.0", Inv) : "-";
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
                s.Chart = new UiChartModel { Kind = UiChartKind.Line, Title = w.T("reallife_work_gpa_by_term", "Grade point average by quarter"), Points = pts,
                    Series = new[] { new UiSeries { Label = w.T("reallife_work_gpa_short", "GPA"), Values = vals, Tint = UiRole.Accent } }, Format = v => v.ToString("0.0", Inv) };
            }
            s.ComingTitle = w.T("reallife_work_coming_up", "Coming up");
            for (int i = 0; i < f.Courses.Count && s.Coming.Count < 4; i++)
            {
                Enrolment e = f.Courses[i];
                if (e.Def != null && e.ExamTo >= day && (e.At == EnrolStage.Exam || e.At == EnrolStage.Retake || e.ExamFrom > 0))
                {
                    int from = Math.Max(e.ExamFrom, day);
                    s.Coming.Add(new KeyValuePair<string, string>(CareersText.Day(from) + ", " + w.F("reallife_work_exam_of", "Exam: {course}", "course", CareersText.Course(e.Course)),
                        w.F("reallife_work_in_n_days", "{n} days", "n", Math.Max(0, from - day).ToString(Inv))));
                }
            }
            for (int i = 0; i < f.Courses.Count && s.Coming.Count < 4; i++)
            {
                Enrolment e = f.Courses[i];
                if (e.Def != null && e.Start > day)
                {
                    s.Coming.Add(new KeyValuePair<string, string>(CareersText.Day(e.Start) + ", " + w.F("reallife_work_starts_of", "{course} starts", "course", CareersText.Course(e.Course)),
                        w.F("reallife_work_in_n_days", "{n} days", "n", (e.Start - day).ToString(Inv))));
                }
            }
            s.ClassTitle = w.T("reallife_work_your_class", "Your class");
            s.ClassNote = w.T("reallife_work_by_gpa", "by grade point average");
            for (int i = 0; i < list.Count && i < 8; i++)
            {
                s.Class.Add(new WorkPerson { Name = list[i].You ? list[i].Name + " " + w.T("reallife_work_you_mark", "(you)") : list[i].Name, Right = (list[i].Gpa / 100f).ToString("0.0", Inv),
                    Ring = list[i].You ? UiRole.Accent : UiRole.None });
            }
            s.GainTitle = w.T("reallife_work_courses_gave", "What the courses gave you");
            for (int sk = 0; sk < Skills.Count && s.Gains.Count < 8; sk++)
            {
                int points = StudyRules.Points(f, sk);
                if (points > 0)
                {
                    s.Gains.Add(new WorkBar { Label = CareersText.Skill(sk), Value = Math.Min(100, points) });
                }
            }
            return t;
        }

        private static void AddMark(SortedDictionary<int, WorkTerm> terms, int term, int quarter, UtilitiesWords w, WorkMark mark)
        {
            if (!terms.TryGetValue(term, out WorkTerm t))
            {
                t = new WorkTerm { Title = w.F("reallife_work_term_title", "Quarter {n}, day {from} to {to}", "n", (term + 1).ToString(Inv), "from",
                    (term * quarter).ToString(Inv), "to", ((term + 1) * quarter).ToString(Inv)) };
                terms[term] = t;
            }
            t.Marks.Add(mark);
            int credits = 0;
            for (int i = 0; i < t.Marks.Count; i++)
            {
                int.TryParse(t.Marks[i].Credits, out int c);
                credits += c;
            }
            t.Note = w.F("reallife_work_term_note", "{n} courses, {credits} credits", "n", t.Marks.Count.ToString(Inv), "credits", credits.ToString(Inv));
        }

        // ------------------------------------------------------------------------------------------------- Skills, Family, Schools

        private WorkTab SkillsTab(CareersDirector d, StudyFile f)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Skills, Title = w.T("reallife_campus_tab_skills", "Skills"), Lead = w.T("reallife_work_skills_lead", "What your courses and work have taught you."),
                EmptyTitle = w.T("reallife_work_skills_empty_title", "No skills yet"), EmptyBody = w.T("reallife_work_no_skills",
                    "Courses and work teach them. Enrol on a course and its skills grow here.") };
            for (int s = 0; s < Skills.Count; s++)
            {
                int points = StudyRules.Points(f, s);
                if (points <= 0)
                {
                    continue;
                }
                t.Skills.Add(new WorkBar { Label = CareersText.Skill(s), Value = Math.Min(100, points) });
                string place = null;
                for (int i = 0; i < StudyCatalogue.All.Length && place == null; i++)
                {
                    CourseDef c = StudyCatalogue.All[i];
                    place = CareersDirector.Offered(c) && (c.SkillA == s || (c.SkillB == s && c.GainB > 0)) ? CareersText.Course(c.Id) : null;
                }
                t.SkillIcons.Add(place != null ? UiIcon.Cap : UiIcon.Briefcase);
                t.SkillPlaces.Add(place != null ? w.F("reallife_work_taught_in", "Taught in {course}", "course", place) : w.T("reallife_work_taught_at_work", "Learned at work"));
            }
            return t;
        }

        private WorkTab Family(CareersDirector d)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Profiles, EmptyTitle = w.T("reallife_work_no_kids_title", "No child is at university yet"),
                EmptyBody = w.T("reallife_work_no_children", "A grown child who wants a degree comes to you with a course and its fee. You pay for it or decline, and what they study shows here.") };
            IReadOnlyList<ChildView> kids = d.Kids();
            for (int i = 0; kids != null && i < kids.Count; i++)
            {
                ChildStudy c = d.ChildOf(kids[i].Name);
                var k = new WorkKid { Name = kids[i].Name, Ring = UiRole.Ok, Role = c != null && c.Course >= 0 ? CareersText.Course(c.Course) : w.T("reallife_work_no_course_yet", "No course yet"),
                    FactLabels = new[] { w.T("reallife_work_col_state", "State") }, FactValues = new[] { c != null ? c.At.ToString() : "-" } };
                if (c != null && c.At == ChildStep.Offered)
                {
                    k.Acts.Add(new WorkAct { Kind = PayChild, Arg = i, Label = w.T("reallife_campus_button_child_pay", "Pay for the degree"), Style = UiButtonKind.Primary, Icon = UiIcon.Check });
                    k.Acts.Add(new WorkAct { Kind = DeclineChild, Arg = i, Label = w.T("reallife_campus_button_child_decline", "Not this time"),
                        Style = UiButtonKind.Destructive, Icon = UiIcon.Close });
                }
                t.Kids.Add(k);
            }
            var form = new WorkFormModel();
            var tutor = new WorkFormBand { Title = w.T("reallife_work_tutor_title", "Tutor a pupil"), Note = w.T("reallife_work_tutor_note",
                "Teach a pupil a skill you know well, for a fee. It takes an evening.") };
            string why = d.CanTutor(out int _);
            tutor.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = new WorkAct { Kind = Tutor, Label = w.T("reallife_campus_button_tutor", "Give a tutoring session"),
                Style = UiButtonKind.Secondary, Icon = UiIcon.Cap, Why = why == null ? null : w.T(why, "You cannot tutor now.") } });
            form.Left.Add(tutor);
            t.Form = form;
            return t;
        }

        // The city's schools as a market (Scripts/Schools): league order, pupils against places, results and the inspection's grade.
        private WorkTab Schools(CareersDirector d)
        {
            UtilitiesWords w = W;
            var t = new WorkTab { Shape = WorkShape.Table, EmptyTitle = w.T("reallife_work_schools_empty_title", "No school is open yet"),
                EmptyBody = w.T("reallife_work_no_schools", "The city's schools open as it grows.") };
            t.Columns = new[] { WorkBuilder.Col(w.T("reallife_campus_col_school", "School"), UiCellKind.Tile), WorkBuilder.Col(w.T("reallife_career_col_where", "Where"), UiCellKind.Text, 160),
                WorkBuilder.Col(w.T("reallife_work_col_pupils", "Pupils"), UiCellKind.Text, 120), WorkBuilder.Col(w.T("reallife_work_col_results", "Results"), UiCellKind.Meter, 160),
                WorkBuilder.Col(w.T("reallife_work_col_inspection", "Inspection"), UiCellKind.Chip, 128) };
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
                string name = RealLifeOverhaul.Schools.SchoolsGame.Name(f.Id), grade = RealLifeOverhaul.Schools.SchoolsText.Grade(f.Grade);
                UiRole tone = f.Grade <= (int)RealLifeOverhaul.Schools.SchoolGrade.Good ? UiRole.Ok : UiRole.Warn;
                var row = new WorkRow { Id = f.Id, Icon = UiIcon.Building, Name = name, Sub = RealLifeOverhaul.Schools.SchoolsText.Owner(f.Owner),
                    Status = grade, Tone = tone, Chip = grade, ChipTone = tone,
                    Cells = new[] { null, RealLifeOverhaul.Schools.SchoolsGame.District(def.District),
                        f.Pupils.ToString(Inv) + " / " + f.Places.ToString(Inv), f.Results >= 0 ? f.Results.ToString(Inv) + "%" : "-", grade },
                    Values = new[] { 0, 0, 0, Math.Max(0, f.Results), 0 }, HeadSub = RealLifeOverhaul.Schools.SchoolsGame.District(def.District) };
                row.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_pupils", "Pupils"), f.Pupils.ToString(Inv) + " / " + f.Places.ToString(Inv)));
                row.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_results", "Results"), f.Results >= 0 ? f.Results.ToString(Inv) + "%" : "-"));
                var facts = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_the_school", "The school") };
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_owner", "Owner"), RealLifeOverhaul.Schools.SchoolsText.Owner(f.Owner)));
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_teachers", "Teachers"), f.Teachers.ToString(Inv)));
                facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_waiting", "Families waiting"), f.Waiting.ToString(Inv)));
                row.Sections.Add(facts);
                var marks = new WorkSection { Part = WorkPart.Bars, Title = w.T("reallife_work_how_it_does", "How it does") };
                marks.Bars.Add(new WorkBar { Label = w.T("reallife_work_col_results", "Results"), Value = Math.Max(0, Math.Min(100, f.Results)) });
                marks.Bars.Add(new WorkBar { Label = w.T("reallife_work_teachers_skill", "Teachers' skill"), Value = Math.Max(0, Math.Min(100, f.Skill)) });
                marks.Bars.Add(new WorkBar { Label = w.T("reallife_work_places_filled", "Places filled"), Value = f.Places > 0 ? Math.Min(100, f.Pupils * 100 / f.Places) : 0 });
                row.Sections.Add(marks);
                if (f.Id == RealLifeOverhaul.Schools.SchoolRules.CampusId)
                {
                    row.Locate = new WorkAct { Kind = ShowMain, Arg = f.Id, Label = w.T("reallife_work_show_school", "Show school"), Opens = true };
                    row.Acts.Add(new WorkAct { Kind = ShowMain, Arg = f.Id, Label = w.T("reallife_work_show_school", "Show school"),
                        Style = UiButtonKind.Primary, Icon = UiIcon.Locate, Opens = true });
                }
                t.Rows.Add(row);
            }
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
                    case Enrol:
                    {
                        why = d.Enrol(act.Arg / 8, Modes[Math.Max(0, Math.Min(2, act.Arg & 3))], (act.Arg & 4) != 0);
                        break;
                    }
                    case Pay: why = d.PayOwed(act.Arg); break;
                    case Withdraw:
                        GameConfirm.Show("reallife_campus_confirm_withdraw_title".Localize(new { name = CareersText.Course(act.Arg) }), "reallife_campus_confirm_withdraw_body".Localize(),
                            () =>
                            {
                                d.Withdraw(act.Arg);
                                _local++;
                            }, null, null, null, true, true);
                        return true;
                    case Graduate: why = d.Graduate(); break;
                    case Tutor: why = d.Tutor(); break;
                    case PayChild: why = d.PayChild(ChildName(d, act.Arg)); break;
                    case DeclineChild: why = d.DeclineChild(ChildName(d, act.Arg)); break;
                    case ShowSchool: why = d.ShowCampus((Campus)act.Arg); break;
                    case OpenTeacher: why = RealLifeOverhaul.Careers.PeoplePoolGame.Open(act.Arg) ? null : "reallife_career_why_route"; break;
                    case ShowMain: why = RealLifeOverhaul.Schools.SchoolsGame.Guide() ? null : "reallife_career_why_route"; break;
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
