using System.Collections.Generic;
using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    public static WorkScreen CampusScreenOf(UiWords t, string state)
    {
        UtilitiesWords w = UtilitiesWordsOf(t);
        WorkApp app = CampusAppOf(w, state);
        var screen = new WorkScreen(new SampleWork("campus", w.T("reallife_campus_app", "Campus"), UiIcon.Cap, true, app.Nav, t, v => WorkTabPages.Build(app, v, w)));
        screen.View.Tab = WkTabOf("campus", state);
        if (state == "courses_full")
        {
            screen.View.Row = 3;   // the forklift course, whose seats are all taken
        }
        else if (state == "courses_cooking")
        {
            screen.View.Filter = 4;
        }
        return screen;
    }

    private static WorkApp CampusAppOf(UtilitiesWords w, string state)
    {
        var app = new WorkApp { Ready = true };
        bool studying = state != "studying_none";
        WorkTab courses = CoursesTab(w, state);
        WorkTab study = StudyingTab(w, state);
        WorkTab results = ResultsTab(w, state);
        WorkTab skills = SkillsTab(w);
        WorkTab family = FamilyTab(w, state);
        WorkTab schools = SchoolsTab(w);
        app.Tabs = new[] { courses, study, results, skills, family, schools };
        app.Nav = new[] { WkNav(UiIcon.Books, w.T("reallife_campus_tab_courses", "Courses")),
            WkNav(UiIcon.Calendar, w.T("reallife_campus_tab_studying", "Studying"), studying && study.Rows.Count > 0 ? study.Rows.Count.ToString() : null),
            WkNav(UiIcon.ClipboardText, w.T("reallife_campus_tab_results", "Results")), WkNav(UiIcon.Star, w.T("reallife_campus_tab_skills", "Skills")),
            WkNav(UiIcon.Heart, w.T("reallife_campus_tab_family", "Family")), WkNav(UiIcon.Building, w.T("reallife_campus_tab_schools", "Schools"), schools.Rows.Count.ToString()) };
        return app;
    }

    private static string SDays(UtilitiesWords w, int a, int b) => a == b ? SWd(w, a) : SWd(w, a) + ", " + SWd(w, b);

    // The real courses of the catalogue, with their own figures: key, glyph, subject chip, kind (0 short, 1 diploma), weeks, fee, the two class days and the hour, seats left of 24,
    // the two skills (key, gain) and the teacher (name, years, rating in tenths).
    private sealed class WkCourse
    {
        public string Key;
        public UiIcon Icon;
        public int Group, Kind, Weeks, Fee, D1, D2, Hour, Seats;
        public string SkillA, SkillB, Teacher;
        public int GainA, GainB, Years, Rating;
    }

    private static readonly WkCourse[] WkCourses =
    {
        new WkCourse { Key = "first_aid", Icon = UiIcon.Heartbeat, Group = 4, Kind = 0, Weeks = 2, Fee = 120, D1 = 2, D2 = 5, Hour = 10, Seats = 11, SkillA = "first_aid", GainA = 25, SkillB = "care", GainB = 5, Teacher = "Ines Calloway", Years = 12, Rating = 47 },
        new WkCourse { Key = "barista", Icon = UiIcon.Coffee, Group = 3, Kind = 0, Weeks = 2, Fee = 150, D1 = 0, D2 = 3, Hour = 10, Seats = 8, SkillA = "hospitality", GainA = 20, SkillB = "cooking", GainB = 5, Teacher = "Marco Ferrante", Years = 9, Rating = 43 },
        new WkCourse { Key = "bartending", Icon = UiIcon.ForkKnife, Group = 3, Kind = 0, Weeks = 3, Fee = 180, D1 = 1, D2 = 4, Hour = 14, Seats = 14, SkillA = "hospitality", GainA = 25, SkillB = "cooking", GainB = 5, Teacher = "Ruth Okonkwo", Years = 6, Rating = 41 },
        new WkCourse { Key = "forklift", Icon = UiIcon.Wrench, Group = 2, Kind = 0, Weeks = 2, Fee = 250, D1 = 0, D2 = 2, Hour = 9, Seats = 9, SkillA = "handling", GainA = 30, SkillB = "driving", GainB = 5, Teacher = "Walter Haas", Years = 15, Rating = 45 },
        new WkCourse { Key = "security_licence", Icon = UiIcon.ShieldCheck, Group = 4, Kind = 0, Weeks = 2, Fee = 200, D1 = 0, D2 = 3, Hour = 18, Seats = 12, SkillA = "guarding", GainA = 25, SkillB = "first_aid", GainB = 5, Teacher = "Dara Quinn", Years = 8, Rating = 39 },
        new WkCourse { Key = "driving_refresher", Icon = UiIcon.Car, Group = 2, Kind = 0, Weeks = 1, Fee = 90, D1 = 5, D2 = 5, Hour = 9, Seats = 15, SkillA = "driving", GainA = 15, SkillB = null, Teacher = "Pat Lindgren", Years = 11, Rating = 42 },
        new WkCourse { Key = "electrician", Icon = UiIcon.Lightning, Group = 2, Kind = 1, Weeks = 20, Fee = 3200, D1 = 4, D2 = 4, Hour = 9, Seats = 12, SkillA = "electrical", GainA = 40, SkillB = "handling", GainB = 5, Teacher = "Gus Varga", Years = 18, Rating = 44 },
        new WkCourse { Key = "plumber", Icon = UiIcon.Drop, Group = 2, Kind = 1, Weeks = 18, Fee = 2800, D1 = 2, D2 = 4, Hour = 9, Seats = 9, SkillA = "plumbing", GainA = 40, SkillB = "handling", GainB = 5, Teacher = "Nell Hartigan", Years = 14, Rating = 46 },
        new WkCourse { Key = "carpenter", Icon = UiIcon.Hammer, Group = 2, Kind = 1, Weeks = 18, Fee = 2600, D1 = 1, D2 = 3, Hour = 9, Seats = 7, SkillA = "joinery", GainA = 40, SkillB = "handling", GainB = 5, Teacher = "Tobias Reyes", Years = 21, Rating = 48 },
        new WkCourse { Key = "chef", Icon = UiIcon.ForkKnife, Group = 3, Kind = 1, Weeks = 16, Fee = 2400, D1 = 1, D2 = 4, Hour = 9, Seats = 10, SkillA = "cooking", GainA = 35, SkillB = "hospitality", GainB = 5, Teacher = "Ilse Brandt", Years = 17, Rating = 47 },
        new WkCourse { Key = "hairdresser", Icon = UiIcon.PaintBrush, Group = 4, Kind = 1, Weeks = 14, Fee = 1900, D1 = 0, D2 = 2, Hour = 10, Seats = 13, SkillA = "hair", GainA = 35, SkillB = "beauty", GainB = 5, Teacher = "Soraya Malik", Years = 10, Rating = 44 },
        new WkCourse { Key = "care_worker", Icon = UiIcon.Heart, Group = 4, Kind = 1, Weeks = 16, Fee = 2100, D1 = 1, D2 = 3, Hour = 9, Seats = 16, SkillA = "care", GainA = 35, SkillB = "clinical", GainB = 5, Teacher = "Joan Pellew", Years = 13, Rating = 45 }
    };

    private static WorkTab CoursesTab(UtilitiesWords w, string state)
    {
        var t = new WorkTab { Shape = WorkShape.Cards, Search = w.T("reallife_work_search_courses", "Search courses"), AllWord = w.T("reallife_work_filter_all_word", "All"),
            Chips = new[] { w.T("reallife_work_subject_business", "Business"), w.T("reallife_work_subject_tech", "Tech"), w.T("reallife_work_subject_trades", "Trades"),
                w.T("reallife_work_subject_food", "Food and drink"), w.T("reallife_work_subject_care", "Care and service") }, EmptyTitle = w.T("reallife_work_empty_courses_title", "No course matches"),
            EmptyBody = w.T("reallife_work_empty_courses", "Another subject, or a shorter search, may show one.") };
        for (int i = 0; i < WkCourses.Length; i++)
        {
            WkCourse c = WkCourses[i];
            int seats = c.Key == "forklift" && state == "courses_full" ? 0 : c.Seats;
            string fee = "$" + c.Fee.ToString("N0", System.Globalization.CultureInfo.InvariantCulture), weeks = w.F("reallife_work_n_weeks", "{n} weeks", "n", c.Weeks.ToString());
            string timetable = Sf(w, "reallife_study_timetable", "days", SDays(w, c.D1, c.D2), "hour", c.Hour.ToString("00") + ":00");
            string tag = Sx(w, c.Kind == 0 ? "reallife_study_kind_short" : "reallife_study_kind_vocational");
            var r = new WorkRow { Id = i + 1, Icon = c.Icon, Name = SCourse(w, c.Key), Sub = Sx(w, "reallife_study_campus_school"), Line = Sx(w, "reallife_study_about_" + c.Key), Status = tag,
                Tone = c.Kind == 0 ? UiRole.Muted : UiRole.Accent, Group = c.Group, NextLabel = w.T("reallife_work_next_class", "Next class"), NextValue = timetable, FootLeft = weeks, FootRight = fee };
            r.Title = r.Name;
            r.HeadSub = r.Sub;
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_campus_col_length", "Length"), weeks));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_campus_col_fee", "Fee"), fee));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_classes", "Classes"), timetable));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_seats", "Seats left"), seats == 0 ? w.T("reallife_work_full", "Full")
                : w.F("reallife_work_seats_of", "{n} of {all}", "n", seats.ToString(), "all", "24")));
            var learn = new WorkSection { Part = WorkPart.Bars, Title = w.T("reallife_work_you_will_learn", "What you will learn"), Note = w.T("reallife_work_skill_after", "skill after the course") };
            learn.Bars.Add(new WorkBar { Label = SSkill(w, c.SkillA), Value = c.SkillA == "handling" ? 74 : c.SkillA == "first_aid" ? 55 : c.SkillA == "hospitality" ? 40 : c.SkillA == "cooking" ? 38 : 20 + i * 3, Note = "+" + c.GainA });
            if (c.SkillB != null)
            {
                learn.Bars.Add(new WorkBar { Label = SSkill(w, c.SkillB), Value = c.SkillB == "driving" ? 66 : c.SkillB == "care" ? 31 : 12 + i * 2, Note = "+" + c.GainB });
            }
            r.Sections.Add(learn);
            var before = new WorkSection { Part = WorkPart.Checks, Title = w.T("reallife_work_before_enrol", "Before you enrol") };
            before.Checks.Add(new WorkNeed { Words = w.F("reallife_work_you_have_fee", "You have the {fee} fee", "fee", fee), Met = true });
            before.Checks.Add(new WorkNeed { Words = w.T("reallife_work_evenings_free", "Your evenings are free"), Met = true });
            if (c.Kind == 1)
            {
                before.Checks.Add(new WorkNeed { Words = w.F("reallife_work_skill_floor", "{skill} {have}, the course asks for {need}", "skill", SSkill(w, "handling"), "have", "74", "need", "40"), Met = true });
            }
            r.Sections.Add(before);
            if (c.Key == "first_aid")
            {
                r.Sections.Add(new WorkSection { Part = WorkPart.Text, Text = w.F("reallife_work_employee_in_class", "{name} from your team is in this class.", "name", "Ava Lindqvist") });
            }
            var teacher = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_your_teacher_title", "Your teacher") };
            teacher.People.Add(new WorkPerson { Name = c.Teacher, Role = w.F("reallife_work_teacher_line", "{years} years teaching, rated {rating} by students", "years", c.Years.ToString(), "rating", (c.Rating / 10f).ToString("0.0")),
                Halves = SHalves(c.Rating), Ring = UiRole.None });
            r.Sections.Add(teacher);
            r.HintLead = w.T("reallife_work_classes_desk_lead", "Classes start at your desk.");
            r.Hint = w.T("reallife_work_classes_desk_line", "Click it in the classroom when the bell rings, in the game's own work panel.");
            r.HintIcon = UiIcon.Person;
            r.Acts.Add(WkAct(100, seats == 0 ? w.T("reallife_work_full", "Full") : w.F("reallife_work_enrol_for", "Enrol, {fee}", "fee", fee), UiButtonKind.Primary, UiIcon.Check,
                seats == 0 ? w.T("reallife_study_why_seats", "Every seat of this course is taken.") : null, false, r.Id));
            r.Acts.Add(WkAct(107, w.T("reallife_work_show_school", "Show school"), UiButtonKind.Secondary, UiIcon.Locate, null, true, r.Id));
            t.Rows.Add(r);
        }
        return t;
    }

    private static WorkTab StudyingTab(UtilitiesWords w, string state)
    {
        var t = new WorkTab { Shape = WorkShape.Diary };
        if (state != "studying_none")
        {
            // against today (412, a Tuesday): barista from day 405, care work from day 380, chef booked for day 440
            object[][] live =
            {
                new object[] { "barista", UiIcon.Coffee, 0, 3, 10, "100%", 421, false }, new object[] { "care_worker", UiIcon.Heart, 1, 3, 9, "91%", 468, false },
                new object[] { "chef", UiIcon.ForkKnife, 1, 4, 9, null, 0, true }
            };
            for (int i = 0; i < live.Length; i++)
            {
                bool booked = (bool)live[i][7];
                string key = (string)live[i][0];
                var r = new WorkRow { Id = i + 1, Icon = (UiIcon)live[i][1], Name = SCourse(w, key), Sub = Sx(w, "reallife_study_campus_school"),
                    Status = booked ? w.F("reallife_work_starts_day", "Starts day {n}", "n", "440") : w.T("reallife_work_under_way", "Under way"), Tone = booked ? UiRole.Muted : UiRole.Accent };
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_campus_col_next", "Next class"), booked ? SDay(w, 440) + " " + ((int)live[i][4]).ToString("00") + ":00" : SDay(w, 414) + " " + ((int)live[i][4]).ToString("00") + ":00"));
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_campus_col_attended", "Attended"), (string)live[i][5] ?? w.T("reallife_work_not_started", "Not started")));
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_campus_col_exam", "Exam"), booked ? w.T("reallife_work_not_set", "Not set") : SDay(w, (int)live[i][6])));
                r.Acts.Add(WkAct(102, w.T("reallife_campus_button_withdraw", "Withdraw"), UiButtonKind.Destructive, UiIcon.Close, null, false, r.Id));
                t.Rows.Add(r);
            }
        }
        // March, the 14th is today (day 412, a Tuesday): the 1st falls on a Wednesday
        var m = new UiDiaryModel
        {
            Month = Sx(w, "reallife_calendar_month_3"), Weekdays = new[] { SWd(w, 0), SWd(w, 1), SWd(w, 2), SWd(w, 3), SWd(w, 4), SWd(w, 5), SWd(w, 6) }, Lead = 2, Length = 31, Today = 14, Chosen = 16, PastBefore = 14,
            ChosenTitle = SWd(w, 3) + " 16 " + Sx(w, "reallife_calendar_month_3"), ChosenEmpty = w.T("reallife_work_diary_empty", "No class, homework or exam falls on this day."),
                AheadTitle = w.T("reallife_work_diary_ahead", "Coming up")
        };
        var entries = new List<UiDiaryEntry>();
        if (state != "studying_none")
        {
            string barista = SCourse(w, "barista"), care = SCourse(w, "care_worker");
            for (int day = 1; day <= 31; day++)
            {
                int weekday = (2 + day - 1) % 7;
                if ((weekday == 0 || weekday == 3) && day >= 7 && day <= 22)
                {
                    entries.Add(new UiDiaryEntry { Day = day, Icon = UiIcon.Coffee, Title = w.F("reallife_work_diary_class", "Class: {course}", "course", barista), Short = barista, Value = "10:00" });
                }
                if (weekday == 1 || weekday == 3)
                {
                    entries.Add(new UiDiaryEntry { Day = day, Icon = UiIcon.Heart, Title = w.F("reallife_work_diary_class", "Class: {course}", "course", care), Short = care, Value = "09:00" });
                }
            }
            entries.Add(new UiDiaryEntry { Day = 9, Icon = UiIcon.ClipboardText, Title = w.F("reallife_work_diary_homework", "Homework due: {course}", "course", care), Short = care });
            entries.Add(new UiDiaryEntry { Day = 17, Icon = UiIcon.ClipboardText, Title = w.F("reallife_work_diary_homework", "Homework due: {course}", "course", care), Short = care });
            entries.Add(new UiDiaryEntry { Day = 23, Icon = UiIcon.Scales, Title = w.F("reallife_work_diary_exam", "Exam: {course}", "course", barista), Short = barista, Value = "10:00" });
            entries.Sort((a, b) => a.Day.CompareTo(b.Day));   // in date order, as the live diary sorts it
        }
        m.Entries = entries.ToArray();
        t.Diary = new WorkDiary { Month = m };
        return t;
    }

    private static WorkMark WkMark(UtilitiesWords w, string course, UiIcon icon, int credits, int attendance, int exam, string status, UiRole tone)
    {
        string letter = exam >= 80 ? "A" : exam >= 70 ? "B" : exam >= 60 ? "C" : "D";
        return new WorkMark { Icon = icon, Course = SCourse(w, course), Place = Sx(w, "reallife_study_campus_school"), Credits = credits.ToString(), Attendance = attendance + "%", AttendanceGrade = attendance,
            Exam = exam.ToString(), ExamGrade = exam, Letter = letter, Percent = exam + "%", LetterGrade = exam, Status = status, Tone = tone };
    }

    private static WorkTab ResultsTab(UtilitiesWords w, string state)
    {
        var t = new WorkTab { Shape = WorkShape.Statement };
        var s = new WorkStatement { EmptyTitle = w.T("reallife_work_results_empty_title", "No results yet"),
            EmptyBody = w.T("reallife_work_no_results", "A finished course adds its grade, credits and your place in the class here.") };
        t.Statement = s;
        if (state == "results_new")
        {
            return t;
        }
        s.Head = new UiProfileModel { Name = "Jordan Hayes", Hue = UiColour.HueOf("Jordan Hayes"), Role = w.T("reallife_work_student", "Student") + ", " + Sx(w, "reallife_study_campus_school"), Ring = UiRole.Ok };
        s.FigureLabels = new[] { w.T("reallife_work_gpa_short", "GPA"), w.T("reallife_work_credits_word", "Credits"), w.T("reallife_work_class_rank", "Class rank") };
        s.FigureValues = new[] { "3.4", "11", w.F("reallife_work_rank_of", "{rank} of {all}", "rank", "7", "all", "24") };
        s.TranscriptTitle = w.T("reallife_work_transcript", "Transcript");
        s.Heads = new[] { w.T("reallife_campus_col_course", "Course"), w.T("reallife_work_col_credits", "Credits"), w.T("reallife_work_col_attendance", "Attendance"),
            w.T("reallife_campus_col_exam", "Exam"), w.T("reallife_work_col_grade", "Grade"), w.T("reallife_work_col_state", "State") };
        s.TotalLabel = w.T("reallife_work_total_label", "Credits earned, grade point average");
        s.TotalCredits = w.F("reallife_work_n_credits", "{n} credits", "n", "11");
        s.TotalGpa = "3.4";
        var q4 = new WorkTerm { Title = w.F("reallife_work_term_title", "Quarter {n}, day {from} to {to}", "n", "4", "from", "270", "to", "359"), Note = w.F("reallife_work_term_note", "{n} courses, {credits} credits", "n", "3", "credits", "9") };
        q4.Marks.Add(WkMark(w, "forklift", UiIcon.Wrench, 3, 100, 74, w.T("reallife_work_licence_won", "Licence won"), UiRole.Ok));
        q4.Marks.Add(WkMark(w, "first_aid", UiIcon.Heartbeat, 3, 96, 88, w.T("reallife_work_passed", "Passed"), UiRole.Ok));
        q4.Marks.Add(WkMark(w, "security_licence", UiIcon.ShieldCheck, 3, 72, 52, w.T("reallife_work_repeat", "Repeat"), UiRole.Warn));
        s.Terms.Add(q4);
        var q5 = new WorkTerm { Title = w.F("reallife_work_term_title", "Quarter {n}, day {from} to {to}", "n", "5", "from", "360", "to", "449"), Note = w.F("reallife_work_term_note", "{n} courses, {credits} credits", "n", "3", "credits", "8") };
        q5.Marks.Add(new WorkMark { Icon = UiIcon.Coffee, Course = SCourse(w, "barista"), Place = Sx(w, "reallife_study_campus_school"), Credits = "2", Attendance = "100%", AttendanceGrade = 100,
            Exam = w.T("reallife_work_not_yet", "Not yet"), Percent = w.T("reallife_work_not_yet", "Not yet"), Status = w.T("reallife_work_under_way", "Under way"), Tone = UiRole.Accent });
        q5.Marks.Add(new WorkMark { Icon = UiIcon.Heart, Course = SCourse(w, "care_worker"), Place = Sx(w, "reallife_study_campus_school"), Credits = "6", Attendance = "91%", AttendanceGrade = 91,
            Exam = w.T("reallife_work_not_yet", "Not yet"), Percent = w.T("reallife_work_not_yet", "Not yet"), Status = w.T("reallife_work_under_way", "Under way"), Tone = UiRole.Accent });
        q5.Marks.Add(new WorkMark { Icon = UiIcon.ForkKnife, Course = SCourse(w, "chef"), Place = Sx(w, "reallife_study_campus_school"), Credits = "6", Attendance = w.T("reallife_work_not_started", "Not started"),
            Exam = w.T("reallife_work_not_yet", "Not yet"), Percent = w.T("reallife_work_not_yet", "Not yet"), Status = w.T("reallife_work_booked", "booked"), Tone = UiRole.Muted });
        s.Terms.Add(q5);
        s.Chart = new UiChartModel { Kind = UiChartKind.Line, Title = w.T("reallife_work_gpa_by_term", "Grade point average by quarter"),
            Points = new[] { w.F("reallife_work_quarter_n", "Quarter {n}", "n", "3"), w.F("reallife_work_quarter_n", "Quarter {n}", "n", "4"), w.F("reallife_work_quarter_n", "Quarter {n}", "n", "5") },
            Series = new[] { new UiSeries { Label = w.T("reallife_work_gpa_short", "GPA"), Values = new[] { 3.1f, 3.3f, 3.4f }, Tint = UiRole.Accent } }, Format = v => v.ToString("0.0") };
        s.ComingTitle = w.T("reallife_work_coming_up", "Coming up");
        s.Coming.Add(new KeyValuePair<string, string>(SDay(w, 421) + ", " + w.F("reallife_work_exam_of", "Exam: {course}", "course", SCourse(w, "barista")), w.F("reallife_work_in_n_days", "{n} days", "n", "9")));
        s.Coming.Add(new KeyValuePair<string, string>(SDay(w, 440) + ", " + w.F("reallife_work_starts_of", "{course} starts", "course", SCourse(w, "chef")), w.F("reallife_work_in_n_days", "{n} days", "n", "28")));
        s.ClassTitle = w.T("reallife_work_your_class", "Your class");
        s.ClassNote = w.T("reallife_work_by_gpa", "by grade point average");
        string[] names = { "Priya Shah", "Hana Okada", "Leo Park", "Amara Nwosu", "Sofia Marin", "Tomas Reyes", "Jordan Hayes" };
        string[] gpa = { "4.0", "3.9", "3.8", "3.6", "3.5", "3.5", "3.4" };
        for (int i = 0; i < names.Length; i++)
        {
            s.Class.Add(new WorkPerson { Name = names[i], Right = gpa[i], Ring = i == 6 ? UiRole.Accent : UiRole.None });
        }
        s.GainTitle = w.T("reallife_work_courses_gave", "What the courses gave you");
        s.Gains.Add(new WorkBar { Label = SSkill(w, "handling"), Value = 74, Note = "+30" });
        s.Gains.Add(new WorkBar { Label = SSkill(w, "first_aid"), Value = 55, Note = "+25" });
        s.Gains.Add(new WorkBar { Label = SSkill(w, "driving"), Value = 66, Note = "+5" });
        s.Gains.Add(new WorkBar { Label = SSkill(w, "guarding"), Value = 25, Note = "+5" });
        return t;
    }

    private static WorkTab SkillsTab(UtilitiesWords w)
    {
        var t = new WorkTab { Shape = WorkShape.Skills, Title = w.T("reallife_campus_tab_skills", "Skills"), Lead = w.T("reallife_work_skills_lead", "What your courses and work have taught you."),
            EmptyTitle = w.T("reallife_work_skills_empty_title", "No skills yet"), EmptyBody = w.T("reallife_work_no_skills",
                "Courses and work teach them. Enrol on a course and its skills grow here.") };
        // skill key, points, the course that taught it (null: learned at work)
        object[][] skills =
        {
            new object[] { "handling", 74, "forklift" }, new object[] { "driving", 66, "forklift" }, new object[] { "first_aid", 55, "first_aid" }, new object[] { "hospitality", 40, "barista" },
            new object[] { "cooking", 38, "barista" }, new object[] { "care", 31, "first_aid" }, new object[] { "guarding", 25, "security_licence" }, new object[] { "electrical", 12, null }
        };
        foreach (object[] s in skills)
        {
            int points = (int)s[1];
            t.Skills.Add(new WorkBar { Label = SSkill(w, (string)s[0]), Value = points });
            t.SkillIcons.Add(s[2] == null ? UiIcon.Briefcase : UiIcon.Cap);
            t.SkillPlaces.Add(CampusBands.SkillLine(w, points, s[2] == null ? w.T("reallife_work_taught_at_work", "Learned at work") : w.F("reallife_work_taught_in", "Taught in {course}", "course", SCourse(w, (string)s[2]))));
        }
        CampusBands.SkillsBands(w, t, SSkill(w, "handling") + " 74", SSkill(w, "electrical") + " 12", 43, new[] { SCourse(w, "electrician"), SCourse(w, "care_worker"), SCourse(w, "security_licence") },
            new[] { "+40 " + SSkill(w, "electrical"), "+35 " + SSkill(w, "care"), "+25 " + SSkill(w, "guarding") }, new[] { "$3,200", "$2,100", "$200" });
        return t;
    }

    private static WorkTab FamilyTab(UtilitiesWords w, string state)
    {
        var t = new WorkTab { Shape = WorkShape.Profiles, EmptyTitle = w.T("reallife_work_no_kids_title", "No child is at university yet"),
            EmptyBody = w.T("reallife_work_no_children", "A grown child who wants a degree comes to you with a course and its fee. You pay for it or decline, and what they study shows here.") };
        var form = new WorkFormModel();
        if (state != "family_none")
        {
            var k1 = new WorkKid { Name = "Maya Hayes", Role = SCourse(w, "cybersecurity"), Ring = UiRole.Ok, Fund = 78,
                FundLabel = w.T("reallife_work_college_fund", "College fund"), FundNote = w.F("reallife_work_fund_of", "{fund} of {cost}", "fund", "$9,300", "cost", "$12,000"),
                FactLabels = new[] { w.T("reallife_work_kid_age", "Age"), w.T("reallife_work_kid_school", "School"), w.T("reallife_work_col_state", "State") },
                FactValues = new[] { "19", Sx(w, "reallife_work_school_state"), Sx(w, "reallife_work_step_offered") } };
            k1.Acts.Add(WkAct(104, w.T("reallife_campus_button_child_pay", "Pay for the degree"), UiButtonKind.Primary, UiIcon.Check, null, false, 0));
            k1.Acts.Add(WkAct(105, w.T("reallife_campus_button_child_decline", "Not this time"), UiButtonKind.Destructive, UiIcon.Close, null, false, 0));
            t.Kids.Add(k1);
            var k2 = new WorkKid { Name = "Theo Hayes", Role = w.T("reallife_work_no_course_yet", "No course yet"), Ring = UiRole.Ok, Fund = 34,
                FundLabel = w.T("reallife_work_college_fund", "College fund"), FundNote = w.F("reallife_work_fund_of", "{fund} of {cost}", "fund", "$5,100", "cost", "$15,000"),
                FactLabels = new[] { w.T("reallife_work_kid_age", "Age"), w.T("reallife_work_kid_school", "School"), w.T("reallife_work_col_state", "State") },
                FactValues = new[] { "9", Sx(w, "reallife_work_school_private"), w.T("reallife_work_kid_too_young", "Too young") } };
            t.Kids.Add(k2);
            CampusBands.FamilyBand(w, form, "$14,400", 1, "$12,000", "$0");
        }
        else
        {
            CampusBands.FamilyBand(w, form, "$0", 0, "$0", "$0");
        }
        var tutor = new WorkFormBand { Title = w.T("reallife_work_tutor_title", "Tutor a pupil") };
        tutor.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = w.T("reallife_work_tutor_note",
            "Teach a pupil a skill you know well, for a fee. It takes an evening."), Act = WkAct(106, w.T("reallife_campus_button_tutor",
            "Give a tutoring session"), UiButtonKind.Secondary, UiIcon.Cap) });
        form.Left.Insert(0, tutor);
        t.Form = form;
        return t;
    }

    private static WorkTab SchoolsTab(UtilitiesWords w)
    {
        var t = new WorkTab { Shape = WorkShape.Table };
        t.Columns = new[] { WkCol(w.T("reallife_campus_col_school", "School"), UiCellKind.Tile), WkCol(w.T("reallife_career_col_where", "Where"), UiCellKind.Text, 160),
            WkCol(w.T("reallife_work_col_pupils", "Pupils"), UiCellKind.Text, 120), WkCol(w.T("reallife_work_col_results", "Results"),
                UiCellKind.Meter, 160), WkCol(w.T("reallife_work_col_inspection", "Inspection"), UiCellKind.Chip, 128) };
        t.EmptyTitle = w.T("reallife_work_schools_empty_title", "No school is open yet");
        t.EmptyBody = w.T("reallife_work_no_schools", "The city's schools open as it grows.");
        string[][] schools =
        {
            new[] { "Midtown Community College", "Midtown", "412 / 480", "88", "Outstanding", "ok" }, new[] { "Garment District Trade School", "Garment District", "256 / 320", "79", "Good", "ok" },
            new[] { "Hudson Tech Institute", "Hudson Yards", "198 / 240", "84", "Good", "ok" }, new[] { "Murray Hill Academy", "Murray Hill", "310 / 300", "61", "Needs work", "warn" },
            new[] { "Uptown Prep", "Uptown", "280 / 400", "91", "Outstanding", "ok" }
        };
        string[] grades = { "reallife_work_inspect_outstanding", "reallife_work_inspect_good", "reallife_work_inspect_good", "reallife_work_inspect_needs_work", "reallife_work_inspect_outstanding" };
        for (int i = 0; i < schools.Length; i++)
        {
            string[] s = schools[i];
            string grade = Sx(w, grades[i]);
            var r = new WorkRow { Id = i + 1, Icon = UiIcon.Building, Name = s[0], Sub = s[1], Cells = new[] { null, s[1], s[2], s[3], grade },
                Values = new[] { 0, 0, 0, int.Parse(s[3]), 0 }, Status = grade,
                Tone = s[5] == "ok" ? UiRole.Ok : UiRole.Warn, Chip = grade, ChipTone = s[5] == "ok" ? UiRole.Ok : UiRole.Warn };
            r.Locate = WkAct(107, w.T("reallife_work_show_school", "Show school"), UiButtonKind.Secondary, UiIcon.Locate, null, true, r.Id);
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_pupils", "Pupils"), s[2]));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_results", "Results"), s[3] + "%"));
            var facts = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_the_school", "The school") };
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_owner", "Owner"), i == 3 ? w.T("reallife_work_owner_council",
                "The council") : w.T("reallife_work_owner_private", "A private trust")));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_courses_taught", "Courses taught"), (9 - i).ToString()));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_waiting", "Families waiting"), (i * 7 % 23).ToString()));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_teachers", "Teachers"), (14 + i * 3).ToString()));
            r.Sections.Add(facts);
            var marks = new WorkSection { Part = WorkPart.Bars, Title = w.T("reallife_work_how_it_does", "How it does") };
            marks.Bars.Add(new WorkBar { Label = w.T("reallife_work_col_results", "Results"), Value = int.Parse(s[3]) });
            marks.Bars.Add(new WorkBar { Label = w.T("reallife_work_teachers_skill", "Teachers' skill"), Value = System.Math.Min(100, int.Parse(s[3]) - 4 + i * 2) });
            marks.Bars.Add(new WorkBar { Label = w.T("reallife_work_places_filled", "Places filled"), Value = System.Math.Min(100,
                int.Parse(s[2].Split(' ')[0]) * 100 / int.Parse(s[2].Split(' ')[2])) });
            r.Sections.Add(marks);
            var head = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_the_head", "The head") };
            string[] heads = { "Amelia Frost", "Dmitri Volkov", "Chidi Okeke", "Marta Lindqvist", "Hugh Pemberton" };
            head.People.Add(new WorkPerson { Name = heads[i], Role = w.F("reallife_work_head_line", "Head teacher, {n} years", "n", (6 + i * 3).ToString()) });
            r.Sections.Add(head);
            r.Acts.Add(WkAct(107, w.T("reallife_work_show_school", "Show school"), UiButtonKind.Primary, UiIcon.Locate, null, true, r.Id));
            t.Rows.Add(r);
        }
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_pupils", "Pupils in all"), "1,456"));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_places", "Places"), "1,740"));
        t.Stats.Add(new KeyValuePair<string, string>(w.T("reallife_work_stat_best_results", "Best results"), "91%"));
        CampusBands.SchoolsBands(w, t);
        return t;
    }
}
