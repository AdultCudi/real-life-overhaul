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
            screen.View.Filter = 2;
        }
        return screen;
    }

    private static string KindWord(UtilitiesWords w, int level) => level == 0 ? w.T("reallife_work_level_beginner", "Beginner") : level == 1 ? w.T("reallife_work_level_intermediate", "Intermediate")
        : w.T("reallife_work_level_licence", "Licence");

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

    private static WorkTab CoursesTab(UtilitiesWords w, string state)
    {
        var t = new WorkTab { Shape = WorkShape.Cards, Search = w.T("reallife_work_search_courses", "Search courses"), AllWord = w.T("reallife_work_filter_all_word", "All"),
            Chips = new[] { w.T("reallife_work_subject_business", "Business"), w.T("reallife_work_subject_cooking", "Cooking"), w.T("reallife_work_subject_tech", "Tech"),
                w.T("reallife_work_subject_trades", "Trades"), w.T("reallife_work_subject_languages", "Languages") }, EmptyTitle = w.T("reallife_work_empty_courses_title", "No course matches"),
            EmptyBody = w.T("reallife_work_empty_courses", "Another subject, or a shorter search, may show one.") };
        // name, school, line, next class, weeks, fee, subject group, glyph, level, seats left
        object[][] list =
        {
            new object[] { "Introduction to Accounting", "Midtown Community College", "Books, tax and a balance sheet you can read.", "Mon 18:00", 12, "$1,850", 0, UiIcon.ChartLineUp, 0, 6 },
            new object[] { "Professional Cooking", "Garment District Trade School", "Knife work, stocks and running a line.", "Tue 09:00", 16, "$2,400", 1, UiIcon.ForkKnife, 0, 11 },
            new object[] { "Python for Business", "Hudson Tech Institute", "Scripts that clean up a stock sheet.", "Thu 18:30", 10, "$1,600", 2, UiIcon.Code, 0, 14 },
            new object[] { "Forklift and Warehouse Safety", "Garment District Trade School", "The licence for depot work.", "Mon 08:00", 3, "$650", 3, UiIcon.Wrench, 2, state == "courses_full" ? 0 : 9 },
            new object[] { "Spanish, Level 1", "Midtown Community College", "Enough to hire and manage a crew.", "Wed 18:00", 14, "$900", 4, UiIcon.Globe, 0, 10 },
            new object[] { "Retail Management", "Midtown Community College", "Staffing, stock and shop floors.", "Tue 18:00", 12, "$2,100", 0, UiIcon.Store, 1, 8 },
            new object[] { "Electrical Basics", "Garment District Trade School", "Wiring, fuses and safe repairs.", "Fri 09:00", 20, "$3,200", 3, UiIcon.Lightning, 0, 12 },
            new object[] { "Game Design Studio", "Hudson Tech Institute", "Prototype a game with a small team.", "Thu 10:00", 14, "$2,800", 2, UiIcon.GameController, 1, 5 },
            new object[] { "Customer Service Basics", "Midtown Community College", "Handling queues and complaints calmly.", "Sat 10:00", 4, "$480", 0, UiIcon.Headset, 0, 15 },
            new object[] { "Bread and Pastry", "Garment District Trade School", "Doughs, ovens and a morning's bake.", "Mon 06:00", 8, "$1,150", 1, UiIcon.Coffee, 0, 7 },
            new object[] { "Italian, Level 1", "Midtown Community College", "Menus, orders and small talk.", "Thu 18:00", 14, "$900", 4, UiIcon.Globe, 0, 13 },
            new object[] { "Plumbing Basics", "Garment District Trade School", "Pipes, valves and a leak at night.", "Wed 09:00", 12, "$2,000", 3, UiIcon.Drop, 0, 9 }
        };
        for (int i = 0; i < list.Length; i++)
        {
            object[] p = list[i];
            string name = (string)p[0], school = (string)p[1];
            int weeks = (int)p[4], seats = (int)p[9];
            string fee = (string)p[5];
            var r = new WorkRow { Id = i + 1, Icon = (UiIcon)p[7], Name = name, Sub = school, Line = (string)p[2], Status = KindWord(w, (int)p[8]), Tone = (int)p[8] == 2 ? UiRole.Warn : UiRole.Muted, Group = (int)p[6],
                NextLabel = w.T("reallife_work_next_class", "Next class"), NextValue = (string)p[3], FootLeft = w.F("reallife_work_n_weeks", "{n} weeks", "n", weeks.ToString()), FootRight = fee };
            r.Sub = school;
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_campus_col_length", "Length"), w.F("reallife_work_n_weeks", "{n} weeks", "n", weeks.ToString())));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_campus_col_fee", "Fee"), fee));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_classes", "Classes"), ((string)p[3]).Replace("Mon ", "Mon, Wed ").Replace("Tue ", "Tue, Thu ")));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_seats", "Seats left"), seats == 0 ? w.T("reallife_work_full", "Full") : w.F("reallife_work_seats_of", "{n} of {all}", "n", seats.ToString(), "all", "24")));
            var learn = new WorkSection { Part = WorkPart.Bars, Title = w.T("reallife_work_you_will_learn", "What you will learn"), Note = w.T("reallife_work_skill_after", "skill after the course") };
            learn.Bars.Add(new WorkBar { Label = i % 2 == 0 ? "Accounting" : "Craft", Value = 38, Note = "+12" });
            learn.Bars.Add(new WorkBar { Label = i % 2 == 0 ? "Business" : "Safety", Value = 54, Note = "+6" });
            learn.Bars.Add(new WorkBar { Label = "Numeracy", Value = 71, Note = "+4" });
            r.Sections.Add(learn);
            var before = new WorkSection { Part = WorkPart.Checks, Title = w.T("reallife_work_before_enrol", "Before you enrol") };
            before.Checks.Add(new WorkNeed { Words = w.F("reallife_work_you_have_fee", "You have the {fee} fee", "fee", fee), Met = true });
            before.Checks.Add(new WorkNeed { Words = w.T("reallife_work_evenings_free", "Your evenings are free"), Met = true });
            before.Checks.Add(new WorkNeed { Words = i == 0 ? w.F("reallife_work_skill_floor", "{skill} {have}, the course asks for {need}", "skill", "Numeracy", "have", "67", "need", "40") : "No skill is asked first", Met = true });
            r.Sections.Add(before);
            if (i == 0)
            {
                var team = new WorkSection { Part = WorkPart.Text, Text = "Ava Lindqvist from your Pioneer team is in this class." };
                r.Sections.Add(team);
            }
            var teacher = new WorkSection { Part = WorkPart.People, Title = w.T("reallife_work_your_teacher_title", "Your teacher") };
            teacher.People.Add(new WorkPerson { Name = "Dr. Elena Marsh", Role = w.F("reallife_work_teacher_line", "{years} years teaching, rated {rating} by students", "years", "12", "rating", "4.7"), Ring = UiRole.None });
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
            string[][] live =
            {
                new[] { "Introduction to Accounting", "Midtown Community College", "Mon 18:00", "83%", "day 421" }, new[] { "Spanish, Level 1", "Midtown Community College", "Tue 19:00", "91%", "day 424" },
                new[] { "Retail Management", "Midtown Community College", "Sat 10:00", "-", "day 450" }
            };
            UiIcon[] icons = { UiIcon.ChartLineUp, UiIcon.Globe, UiIcon.Store };
            for (int i = 0; i < live.Length; i++)
            {
                bool booked = i == 2;
                var r = new WorkRow { Id = i + 1, Icon = icons[i], Name = live[i][0], Sub = live[i][1], Status = booked ? w.F("reallife_work_starts_day", "Starts day {n}", "n", "440") : w.T("reallife_work_under_way", "Under way"), Tone = booked ? UiRole.Muted : UiRole.Accent };
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_campus_col_next", "Next class"), live[i][2]));
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_campus_col_attended", "Attended"), live[i][3]));
                r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_campus_col_exam", "Exam"), live[i][4]));
                if (!booked)
                {
                    r.Acts.Add(WkAct(101, w.T("reallife_campus_button_pay", "Pay what is owed"), UiButtonKind.Secondary, UiIcon.Coin, i == 0 ? null : w.T("reallife_work_nothing_owed", "Nothing is owed."), false, r.Id));
                }
                r.Acts.Add(WkAct(102, w.T("reallife_campus_button_withdraw", "Withdraw"), UiButtonKind.Destructive, UiIcon.Close, null, false, r.Id));
                t.Rows.Add(r);
            }
        }
        var m = new UiDiaryModel
        {
            Month = "March, year 3", Weekdays = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }, Lead = 2, Length = 30, Today = 14, Chosen = 16, PastBefore = 14,
            ChosenTitle = "Thursday 16 March", ChosenEmpty = w.T("reallife_work_diary_empty", "No class, homework or exam falls on this day."), AheadTitle = w.T("reallife_work_diary_ahead", "Coming up")
        };
        var entries = new List<UiDiaryEntry>();
        if (state != "studying_none")
        {
            for (int day = 1; day <= 30; day++)
            {
                int weekday = (2 + day - 1) % 7;
                if (weekday == 0 || weekday == 2)
                {
                    entries.Add(new UiDiaryEntry { Day = day, Icon = UiIcon.Cap, Title = w.F("reallife_work_diary_class", "Class: {course}", "course", "Introduction to Accounting"), Value = "18:00" });
                }
                if (weekday == 1 || weekday == 3)
                {
                    entries.Add(new UiDiaryEntry { Day = day, Icon = UiIcon.Globe, Title = w.F("reallife_work_diary_class", "Class: {course}", "course", "Spanish, Level 1"), Value = "19:00" });
                }
            }
            entries.Add(new UiDiaryEntry { Day = 9, Icon = UiIcon.ClipboardText, Title = w.F("reallife_work_diary_homework", "Homework due: {course}", "course", "Introduction to Accounting") });
            entries.Add(new UiDiaryEntry { Day = 16, Icon = UiIcon.ClipboardText, Title = w.F("reallife_work_diary_homework", "Homework due: {course}", "course", "Spanish, Level 1") });
            entries.Add(new UiDiaryEntry { Day = 20, Icon = UiIcon.Coin, Title = w.F("reallife_work_diary_fee", "Fee instalment, {course}", "course", "Retail Management"), Value = "$700" });
            entries.Add(new UiDiaryEntry { Day = 23, Icon = UiIcon.Scales, Title = w.F("reallife_work_diary_exam", "Exam: {course}", "course", "Introduction to Accounting"), Value = "10:00" });
            entries.Add(new UiDiaryEntry { Day = 26, Icon = UiIcon.Scales, Title = w.F("reallife_work_diary_oral", "Oral test: {course}", "course", "Spanish, Level 1"), Value = "19:00" });
            entries.Add(new UiDiaryEntry { Day = 27, Icon = UiIcon.ClipboardText, Title = w.F("reallife_work_diary_homework", "Homework due: {course}", "course", "Spanish, Level 1") });
        }
        m.Entries = entries.ToArray();
        t.Diary = new WorkDiary { Month = m };
        return t;
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
        s.Head = new UiProfileModel { Name = "Jordan Hayes", Hue = UiColour.HueOf("Jordan Hayes"), Role = "Midtown Community College, Diploma in Business, year 1", Ring = UiRole.Ok };
        s.FigureLabels = new[] { w.T("reallife_work_gpa_short", "GPA"), w.T("reallife_work_credits_word", "Credits"), w.T("reallife_work_class_rank", "Class rank") };
        s.FigureValues = new[] { "3.4", "48 of 120", "7 of 32" };
        s.CreditShare = 0.4f;
        s.TranscriptTitle = w.T("reallife_work_transcript", "Transcript");
        s.Heads = new[] { w.T("reallife_campus_col_course", "Course"), w.T("reallife_work_col_credits", "Credits"), w.T("reallife_work_col_attendance", "Attendance"),
            w.T("reallife_campus_col_exam", "Exam"), w.T("reallife_work_col_grade", "Grade"), w.T("reallife_work_col_state", "State") };
        s.TotalLabel = w.T("reallife_work_total_label", "Credits earned, grade point average");
        s.TotalCredits = "48 credits";
        s.TotalGpa = "3.4";
        var autumn = new WorkTerm { Title = "Autumn term, day 240 to 330", Note = "4 courses, 17 credits" };
        autumn.Marks.Add(new WorkMark { Icon = UiIcon.ChartLineUp, Course = "Business Maths", Place = "Midtown Community College", Credits = "6", Attendance = "96%", AttendanceGrade = 96, Exam = "88", ExamGrade = 88, Letter = "A", Percent = "88%", LetterGrade = 88, Status = w.T("reallife_work_passed", "Passed"), Tone = UiRole.Ok });
        autumn.Marks.Add(new WorkMark { Icon = UiIcon.Headset, Course = "Customer Service Basics", Place = "Midtown Community College", Credits = "4", Attendance = "100%", AttendanceGrade = 100, Exam = "81", ExamGrade = 81, Letter = "B+", Percent = "81%", LetterGrade = 81, Status = w.T("reallife_work_passed", "Passed"), Tone = UiRole.Ok });
        autumn.Marks.Add(new WorkMark { Icon = UiIcon.Wrench, Course = "Forklift and Warehouse Safety", Place = "Garment District Trade School", Credits = "3", Attendance = "100%", AttendanceGrade = 100, Exam = "74", ExamGrade = 74, Letter = "B", Percent = "74%", LetterGrade = 74, Status = w.T("reallife_work_licence_won", "Licence won"), Tone = UiRole.Ok });
        autumn.Marks.Add(new WorkMark { Icon = UiIcon.Globe, Course = "Spanish, Level 1", Place = "Midtown Community College", Credits = "4", Attendance = "72%", AttendanceGrade = 72, Exam = "52", ExamGrade = 52, Letter = "C", Percent = "52%", LetterGrade = 52, Status = w.T("reallife_work_repeat", "Repeat"), Tone = UiRole.Warn });
        s.Terms.Add(autumn);
        var spring = new WorkTerm { Title = "Spring term, day 340 to 430", Note = "now, 2 courses" };
        spring.Marks.Add(new WorkMark { Icon = UiIcon.ChartLineUp, Course = "Introduction to Accounting", Place = "Midtown Community College", Credits = "6", Attendance = "83%", AttendanceGrade = 83, Exam = "-", Letter = "B", Percent = "76% so far", LetterGrade = 76, Status = w.T("reallife_work_under_way", "Under way"), Tone = UiRole.Accent });
        spring.Marks.Add(new WorkMark { Icon = UiIcon.Globe, Course = "Spanish, Level 1", Place = "Midtown Community College", Credits = "4", Attendance = "91%", AttendanceGrade = 91, Exam = "-", Letter = "B", Percent = "70% so far", LetterGrade = 70, Status = w.T("reallife_work_under_way", "Under way"), Tone = UiRole.Accent });
        s.Terms.Add(spring);
        var summer = new WorkTerm { Title = "Summer term, starts day 440", Note = w.T("reallife_work_booked", "booked") };
        summer.Marks.Add(new WorkMark { Icon = UiIcon.Store, Course = "Retail Management", Place = "Midtown Community College", Credits = "6", Attendance = "-", Exam = "-", Letter = null, Percent = "-", Status = w.T("reallife_work_booked", "booked"), Tone = UiRole.Muted });
        s.Terms.Add(summer);
        s.Chart = new UiChartModel { Kind = UiChartKind.Line, Title = w.T("reallife_work_gpa_by_term", "Grade point average by quarter"), Points = new[] { "Autumn", "Spring", "Now" },
            Series = new[] { new UiSeries { Label = "GPA", Values = new[] { 3.1f, 3.3f, 3.4f }, Tint = UiRole.Accent } }, Format = v => v.ToString("0.0") };
        s.ComingTitle = w.T("reallife_work_coming_up", "Coming up");
        s.Coming.Add(new KeyValuePair<string, string>("Day 421, Accounting exam", "19 days"));
        s.Coming.Add(new KeyValuePair<string, string>("Day 424, Spanish oral test", "22 days"));
        s.Coming.Add(new KeyValuePair<string, string>("Day 440, Retail Management starts", "38 days"));
        s.ClassTitle = w.T("reallife_work_your_class", "Your class");
        s.ClassNote = w.T("reallife_work_by_gpa", "by grade point average");
        string[] names = { "Priya Shah", "Hana Okada", "Leo Park", "Amara Nwosu", "Sofia Marin", "Tomas Reyes", "Jordan Hayes (you)" };
        string[] gpa = { "4.0", "3.9", "3.8", "3.6", "3.5", "3.5", "3.4" };
        for (int i = 0; i < names.Length; i++)
        {
            s.Class.Add(new WorkPerson { Name = names[i], Right = gpa[i], Ring = i == 6 ? UiRole.Accent : UiRole.None });
        }
        s.GainTitle = w.T("reallife_work_courses_gave", "What the courses gave you");
        s.Gains.Add(new WorkBar { Label = "Numeracy", Value = 71, Note = "+9" });
        s.Gains.Add(new WorkBar { Label = "Customer care", Value = 88, Note = "+6" });
        s.Gains.Add(new WorkBar { Label = "Warehouse safety", Value = 74, Note = "+10" });
        s.Gains.Add(new WorkBar { Label = "Spanish", Value = 31, Note = "+4" });
        return t;
    }

    private static WorkTab SkillsTab(UtilitiesWords w)
    {
        var t = new WorkTab { Shape = WorkShape.Skills, Title = w.T("reallife_campus_tab_skills", "Skills"), Lead = w.T("reallife_work_skills_lead", "What your courses and work have taught you."),
            EmptyTitle = w.T("reallife_work_skills_empty_title", "No skills yet"), EmptyBody = w.T("reallife_work_no_skills", "Courses and work teach them. Enrol on a course and its skills grow here.") };
        string[][] skills =
        {
            new[] { "Numeracy", "71", "Midtown Community College" }, new[] { "Customer care", "88", "Work: Harbourside Freight HSF" }, new[] { "Warehouse safety", "74", "Garment District Trade School" },
            new[] { "Spanish", "31", "Midtown Community College" }, new[] { "Accounting", "38", "Midtown Community College" }, new[] { "Business", "54", "Midtown Community College" },
            new[] { "Teamwork", "71", "Work: Harbourside Freight HSF" }, new[] { "Forklift driving", "66", "Garment District Trade School" }
        };
        foreach (string[] s in skills)
        {
            t.Skills.Add(new WorkBar { Label = s[0], Value = int.Parse(s[1]) });
            t.SkillIcons.Add(s[2].StartsWith("Work") ? UiIcon.Briefcase : UiIcon.Cap);
            t.SkillPlaces.Add(s[2].StartsWith("Work") ? w.T("reallife_work_taught_at_work", "Learned at work") : w.F("reallife_work_taught_at", "Taught at {place}", "place", s[2]));
        }
        return t;
    }

    private static WorkTab FamilyTab(UtilitiesWords w, string state)
    {
        var t = new WorkTab { Shape = WorkShape.Profiles, EmptyTitle = w.T("reallife_work_no_kids_title", "No child is at university yet"),
            EmptyBody = w.T("reallife_work_no_children", "A grown child who wants a degree comes to you with a course and its fee. You pay for it or decline, and what they study shows here.") };
        if (state != "family_none")
        {
            var k1 = new WorkKid { Name = "Maya Hayes", Role = "Reads Business, Midtown Community College", Ring = UiRole.Ok, Fund = 62, FundLabel = w.T("reallife_work_college_fund", "College fund"), FundNote = "$9,300 of $15,000",
                FactLabels = new[] { "Year", "Grade", "Tutoring" }, FactValues = new[] { "1 of 3", "B+", "None" } };
            k1.Acts.Add(WkAct(104, w.T("reallife_campus_button_child_pay", "Pay for the degree"), UiButtonKind.Primary, UiIcon.Check, null, false, 0));
            k1.Acts.Add(WkAct(105, w.T("reallife_campus_button_child_decline", "Not this time"), UiButtonKind.Destructive, UiIcon.Close, null, false, 0));
            t.Kids.Add(k1);
            var k2 = new WorkKid { Name = "Theo Hayes", Role = "Primary school, Hudson Primary", Ring = UiRole.Ok, Fund = 34, FundLabel = w.T("reallife_work_college_fund", "College fund"), FundNote = "$5,100 of $15,000",
                FactLabels = new[] { "Year", "Grade", "Tutoring" }, FactValues = new[] { "Year 4", "B", "Maths" } };
            t.Kids.Add(k2);
        }
        var form = new WorkFormModel();
        var tutor = new WorkFormBand { Title = w.T("reallife_work_tutor_title", "Tutor a pupil"), Note = w.T("reallife_work_tutor_note", "Teach a pupil a skill you know well, for a fee. It takes an evening.") };
        tutor.Rows.Add(new WorkFormRow { Field = WorkField.Button, Label = string.Empty, Act = WkAct(106, w.T("reallife_campus_button_tutor", "Give a tutoring session"), UiButtonKind.Secondary, UiIcon.Cap) });
        form.Left.Add(tutor);
        t.Form = form;
        return t;
    }

    private static WorkTab SchoolsTab(UtilitiesWords w)
    {
        var t = new WorkTab { Shape = WorkShape.Table };
        t.Columns = new[] { WkCol(w.T("reallife_campus_col_school", "School"), UiCellKind.Tile), WkCol(w.T("reallife_career_col_where", "Where"), UiCellKind.Text, 160),
            WkCol(w.T("reallife_work_col_pupils", "Pupils"), UiCellKind.Text, 120), WkCol(w.T("reallife_work_col_results", "Results"), UiCellKind.Meter, 160), WkCol(w.T("reallife_work_col_inspection", "Inspection"), UiCellKind.Chip, 128) };
        t.EmptyTitle = w.T("reallife_work_schools_empty_title", "No school is open yet");
        t.EmptyBody = w.T("reallife_work_no_schools", "The city's schools open as it grows.");
        string[][] schools =
        {
            new[] { "Midtown Community College", "Midtown", "412 / 480", "88", "Outstanding", "ok" }, new[] { "Garment District Trade School", "Garment District", "256 / 320", "79", "Good", "ok" },
            new[] { "Hudson Tech Institute", "Hudson Yards", "198 / 240", "84", "Good", "ok" }, new[] { "Murray Hill Academy", "Murray Hill", "310 / 300", "61", "Needs work", "warn" },
            new[] { "Uptown Prep", "Uptown", "280 / 400", "91", "Outstanding", "ok" }
        };
        for (int i = 0; i < schools.Length; i++)
        {
            string[] s = schools[i];
            var r = new WorkRow { Id = i + 1, Icon = UiIcon.Building, Name = s[0], Sub = s[1], Cells = new[] { null, s[1], s[2], s[3], s[4] }, Values = new[] { 0, 0, 0, int.Parse(s[3]), 0 }, Status = s[4],
                Tone = s[5] == "ok" ? UiRole.Ok : UiRole.Warn, Chip = s[4], ChipTone = s[5] == "ok" ? UiRole.Ok : UiRole.Warn };
            r.Locate = WkAct(107, w.T("reallife_work_show_school", "Show school"), UiButtonKind.Secondary, UiIcon.Locate, null, true, r.Id);
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_pupils", "Pupils"), s[2]));
            r.Figures.Add(new KeyValuePair<string, string>(w.T("reallife_work_col_results", "Results"), s[3] + "%"));
            var facts = new WorkSection { Part = WorkPart.Pairs, Title = w.T("reallife_work_the_school", "The school") };
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_owner", "Owner"), i == 3 ? w.T("reallife_work_owner_council", "The council") : w.T("reallife_work_owner_private", "A private trust")));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_courses_taught", "Courses taught"), (9 - i).ToString()));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_waiting", "Families waiting"), (i * 7 % 23).ToString()));
            facts.Pairs.Add(new KeyValuePair<string, string>(w.T("reallife_work_teachers", "Teachers"), (14 + i * 3).ToString()));
            r.Sections.Add(facts);
            var marks = new WorkSection { Part = WorkPart.Bars, Title = w.T("reallife_work_how_it_does", "How it does") };
            marks.Bars.Add(new WorkBar { Label = w.T("reallife_work_col_results", "Results"), Value = int.Parse(s[3]) });
            marks.Bars.Add(new WorkBar { Label = w.T("reallife_work_teachers_skill", "Teachers' skill"), Value = System.Math.Min(100, int.Parse(s[3]) - 4 + i * 2) });
            marks.Bars.Add(new WorkBar { Label = w.T("reallife_work_places_filled", "Places filled"), Value = System.Math.Min(100, int.Parse(s[2].Split(' ')[0]) * 100 / int.Parse(s[2].Split(' ')[2])) });
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
        return t;
    }
}
