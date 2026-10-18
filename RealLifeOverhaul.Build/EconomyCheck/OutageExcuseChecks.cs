using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Utilities;

// What the home's internet down excuses (Scripts/Careers/Study/StudyRules: Excuses, NoNetWhy, ClassExcused, Excuse,
// Counted, Attendance, HomeworkSet; Work/ProRules: DayExcused, Excuse, the interview's window and its move;
// Utilities/InternetRules: OutageBetween, Down): a provider's outage excuses an online class (left out of the attendance
// and the mark), a working day from home (no missed day, no warning) and moves a video interview; no plan, one not yet
// connected and one cut for an unpaid bill excuse nothing. The texts, the save's new fields and What's new.
internal static partial class Program
{
    private static void OutageExcuses()
    {
        OutageRule();
        OutageClasses();
        OutageWorkDays();
        OutageInterviews();
        OutageConnections();
        OutageSave();
        OutageTexts();
        OutageSources();
    }

    private static readonly HomeNet[] OwnDoing = { HomeNet.NoHome, HomeNet.NoPlan, HomeNet.Installing, HomeNet.Cut };

    // ---------------------------------------------------------------- the one rule
    private static void OutageRule()
    {
        bool own = false;
        foreach (HomeNet n in OwnDoing)
        {
            own |= StudyRules.Excuses(n);
        }
        Check(StudyRules.Excuses(HomeNet.Outage) && !StudyRules.Excuses(HomeNet.Online) && !own,
            "outage excuse: only the provider's outage excuses; no home, no plan, one being put in or one cut for a bill excuse nothing");
        Check(StudyRules.NoNetWhy(HomeNet.Online) == null && StudyRules.NoNetWhy(HomeNet.Outage) == "reallife_study_why_outage"
              && StudyRules.NoNetWhy(HomeNet.NoPlan) == "reallife_study_why_no_internet" && StudyRules.NoNetWhy(HomeNet.Cut) == "reallife_study_why_no_internet"
              && StudyRules.NoNetWhy(HomeNet.Installing) == "reallife_study_why_no_internet" && StudyRules.NoNetWhy(HomeNet.NoHome) == "reallife_study_why_no_internet",
            "outage excuse: the refusal at the computer names an outage apart from the player's own reasons");
        CourseDef boot = StudyCatalogue.Find(StudyCatalogue.CodingBootcamp);
        Check(StudyRules.AtClass(boot, StudyMode.FullTime, new StudyHere { Home = true, Net = HomeNet.Outage }) == "reallife_study_why_outage"
              && StudyRules.AtClass(boot, StudyMode.FullTime, new StudyHere { Home = true, Net = HomeNet.Cut }) == "reallife_study_why_no_internet"
              && StudyRules.AtClass(boot, StudyMode.FullTime, new StudyHere { Home = true }) == "reallife_study_why_no_internet"
              && StudyRules.AtClass(boot, StudyMode.FullTime, new StudyHere { Home = true, Online = true }) == null,
            "outage excuse: an online class at home in an outage says so; unpaid or no plan says the home has no working internet");
    }

    // ---------------------------------------------------------------- an online class
    private static void OutageClasses()
    {
        CourseDef boot = StudyCatalogue.Find(StudyCatalogue.CodingBootcamp);   // online, Mon Wed Fri at 18, 10 classes
        CourseDef cs = StudyCatalogue.Find(StudyCatalogue.ComputerScience);   // the university
        Check(StudyRules.ClassExcused(boot, StudyMode.FullTime, HomeNet.Outage) && StudyRules.ClassExcused(cs, StudyMode.Distance, HomeNet.Outage)
              && !StudyRules.ClassExcused(cs, StudyMode.FullTime, HomeNet.Outage),
            "outage excuse: a class held online from home is excused in an outage; one held at the university is not");
        bool own = false;
        foreach (HomeNet n in OwnDoing)
        {
            own |= StudyRules.ClassExcused(boot, StudyMode.FullTime, n);
        }
        Check(!own && !StudyRules.ClassExcused(boot, StudyMode.FullTime, HomeNet.Online), "outage excuse: an online class is missed with no plan, one not yet connected or one cut for an unpaid bill");

        // the window: Monday (day 1) at 18:00, from half an hour before to an hour after
        Check(StudyRules.JoinWindow(boot, StudyMode.FullTime, 1, out int from, out int to) && from == 1 * 1440 + 18 * 60 - StudyCatalogue.ArriveEarlyMinutes
              && to == 1 * 1440 + 18 * 60 + StudyCatalogue.LateMinutes && !StudyRules.JoinWindow(boot, StudyMode.FullTime, 2, out _, out _),
            "outage excuse: an online class's window is from its early minutes before the hour to its late minutes after, on its days only");

        // ten classes: eight attended, two gone by in an outage, then the course is over
        Enrolment excused = Bootcamp(), missed = Bootcamp();
        int day = 1;
        for (int i = 0; i < boot.Sessions; i++, day = StudyRules.Next(boot, StudyMode.FullTime, day + 1))
        {
            if (i < 2)
            {
                Check(StudyRules.Excuse(excused, day) && StudyRules.Miss(missed, day), "outage excuse: class " + (i + 1) + " counted as gone by");
            }
            else
            {
                StudyRules.Attend(excused, day);
                StudyRules.Attend(missed, day);
            }
        }
        Check(excused.Seen == boot.Sessions && excused.Done == boot.Sessions - 2 && excused.Excused == 2 && excused.ExcusedDay == 3
              && StudyRules.Counted(excused) == boot.Sessions - 2 && StudyRules.Attendance(excused) == 1000,
            "outage excuse: two excused classes are held, left out of those counted, and the rest attended is full attendance (" + StudyRules.Attendance(excused) + ")");
        Check(missed.Excused == 0 && StudyRules.Attendance(missed) == (boot.Sessions - 2) * 1000 / boot.Sessions,
            "outage excuse: the same two missed count against attendance as before (" + StudyRules.Attendance(missed) + ")");
        Check(StudyRules.ExamScore(excused, 0, false, 0) > StudyRules.ExamScore(missed, 0, false, 0)
              && StudyRules.ExamScore(excused, 0, false, 0) - StudyRules.ExamScore(missed, 0, false, 0) == 8,
            "outage excuse: the mark's attendance part counts excused classes neither way (" + StudyRules.ExamScore(excused, 0, false, 0) + " against " + StudyRules.ExamScore(missed, 0, false, 0) + " missed)");
        Check(StudyRules.HomeworkSet(excused) == boot.Homework && StudyRules.HomeworkSet(missed) == (boot.Sessions - 2) * boot.Homework / boot.Sessions,
            "outage excuse: every counted class attended sets all the homework; excused classes take none away");

        // the bar: six of ten lost to an outage, four attended, is no bar; six missed is
        Enrolment barredNot = Bootcamp(), barred = Bootcamp();
        day = 1;
        for (int i = 0; i < boot.Sessions; i++, day = StudyRules.Next(boot, StudyMode.FullTime, day + 1))
        {
            if (i < 6)
            {
                StudyRules.Excuse(barredNot, day);
                StudyRules.Miss(barred, day);
            }
            else
            {
                StudyRules.Attend(barredNot, day);
                StudyRules.Attend(barred, day);
            }
        }
        StudyRules.Advance(barredNot, day);
        StudyRules.Advance(barred, day);
        Check(barredNot.Stage == (int)EnrolStage.Exam && barred.Stage == (int)EnrolStage.Barred,
            "outage excuse: classes lost to an outage never bar the exam ({study_barred}); the same classes missed do (" + (EnrolStage)barredNot.Stage + ", " + (EnrolStage)barred.Stage + ")");

        // every class excused: no division by nothing, full attendance, all homework open
        Enrolment all = Bootcamp();
        day = 1;
        for (int i = 0; i < boot.Sessions; i++, day = StudyRules.Next(boot, StudyMode.FullTime, day + 1))
        {
            StudyRules.Excuse(all, day);
        }
        Check(StudyRules.Counted(all) == 0 && StudyRules.Attendance(all) == 1000 && StudyRules.HomeworkSet(all) == boot.Homework,
            "outage excuse: a course with every class excused counts as fully attended, with its homework set");

        // once a day: an excuse after the day is counted, or past the course's classes, does nothing
        Enrolment once = Bootcamp();
        Check(StudyRules.Excuse(once, 1) && !StudyRules.Excuse(once, 1) && !StudyRules.Miss(once, 1) && once.Seen == 1 && once.Excused == 1,
            "outage excuse: a day's class is counted once, excused or missed");
        Check(!StudyRules.Excuse(all, day + 7) && all.Excused == boot.Sessions, "outage excuse: nothing beyond the course's classes is excused");

        // the same inputs, the same answer
        Enrolment a1 = Bootcamp(), a2 = Bootcamp();
        StudyRules.Excuse(a1, 1);
        StudyRules.Excuse(a2, 1);
        Check(a1.Seen == a2.Seen && a1.Excused == a2.Excused && StudyRules.Attendance(a1) == StudyRules.Attendance(a2), "outage excuse: the same class excused twice over gives the same record");
    }

    private static Enrolment Bootcamp()
    {
        return new Enrolment { Course = StudyCatalogue.CodingBootcamp, Mode = (int)StudyMode.FullTime, Start = 1, Stage = (int)EnrolStage.Studying };
    }

    // ---------------------------------------------------------------- a working day from home
    private static void OutageWorkDays()
    {
        // day 1 is a Monday: a hybrid post works from home on Mondays and Fridays
        Check(ProRules.DayExcused(WorkMode.Remote, 2, HomeNet.Outage) && ProRules.DayExcused(WorkMode.Hybrid, 1, HomeNet.Outage)
              && !ProRules.DayExcused(WorkMode.Hybrid, 2, HomeNet.Outage) && !ProRules.DayExcused(WorkMode.OnSite, 1, HomeNet.Outage),
            "outage excuse: only a day worked from home is excused by the home's internet");
        bool own = false;
        foreach (HomeNet n in OwnDoing)
        {
            own |= ProRules.DayExcused(WorkMode.Remote, 2, n);
        }
        Check(!own && !ProRules.DayExcused(WorkMode.Remote, 2, HomeNet.Online), "outage excuse: a day from home is missed, a warning, with no plan, one not yet connected or one cut for an unpaid bill");

        Profession dev = ProCatalogue.Find(ProCatalogue.Developer);
        ProRules.StartWindow(dev, 3, out int from, out int to);
        Check(from == 3 * 1440 + dev.Hour * 60 - ProRules.ClockEarlyMinutes && to == 3 * 1440 + dev.Hour * 60 + ProRules.ClockLateMinutes,
            "outage excuse: a working day's start window is its early minutes before its hour to its late minutes after");

        var j = new Job { Hired = 1, Work = (int)WorkMode.Remote };
        j.Post.Prof = ProCatalogue.Developer;
        bool worked = ProRules.Settle(j, 2);
        Check(!worked && j.Due == 1 && j.Absent == 1, "outage excuse: a day from home not worked settles as missed before the excuse");
        ProRules.Excuse(j);
        Check(j.Due == 0 && j.Absent == 0 && j.Warnings == 0 && ProRules.Attendance(j) == 1000 && j.SettledDay == 2,
            "outage excuse: an excused day is neither due nor missed, like a sick day: attendance stays whole and the day is settled");
        ProRules.Excuse(j);
        Check(j.Due == 0 && j.Absent == 0, "outage excuse: excusing never takes the counts below nothing");
    }

    // ---------------------------------------------------------------- an interview by video from home
    private static void OutageInterviews()
    {
        var a = new Application { Stage = (int)ApplyStep.Interview, InterviewDay = 5, InterviewHour = 11, Lapse = 5 };   // day 5 a Friday
        a.Post.Work = (int)WorkMode.Remote;
        ProRules.InterviewWindow(a, out int from, out int to);
        Check(from == 5 * 1440 + 11 * 60 - ProRules.InterviewEarlyMinutes && to == 5 * 1440 + 11 * 60 + ProRules.InterviewLateMinutes,
            "outage excuse: an interview's window is from its early minutes before its hour to its late minutes after");
        Check(!ProRules.InterviewGone(a, 5, 11 * 60 + ProRules.InterviewLateMinutes) && ProRules.InterviewGone(a, 5, 11 * 60 + ProRules.InterviewLateMinutes + 1)
              && ProRules.InterviewGone(a, 6, 0) && !ProRules.InterviewGone(a, 4, 23 * 60),
            "outage excuse: an interview is gone once its window has shut, or its day is past");
        var onsite = new Application { Stage = (int)ApplyStep.Interview, InterviewDay = 5, InterviewHour = 11 };
        onsite.Post.Work = (int)WorkMode.Hybrid;
        bool own = false;
        foreach (HomeNet n in OwnDoing)
        {
            own |= ProRules.InterviewExcused(a, n);
        }
        Check(ProRules.InterviewExcused(a, HomeNet.Outage) && !ProRules.InterviewExcused(onsite, HomeNet.Outage) && !own,
            "outage excuse: a video interview from home in an outage is moved; one at the employer, or one lost to the player's own internet, is not");
        ProRules.Reschedule(a, 6);
        Check(a.InterviewDay == 8 && a.Lapse == 8 && a.InterviewHour == 11 && a.Stage == (int)ApplyStep.Interview,
            "outage excuse: a Friday's interview moved from the day after goes to Monday at the same hour, still an invitation (" + a.InterviewDay + ")");
        Check(ProRules.Weekday(6) == 8 && ProRules.Weekday(7) == 8 && ProRules.Weekday(8) == 8 && ProRules.Weekday(12) == 12, "outage excuse: interviews fall on weekdays");
    }

    // ---------------------------------------------------------------- the connection, as Utilities answers
    private static void OutageConnections()
    {
        int d = 4;
        var a = new RealLifeOverhaul.Utilities.Account { Service = Service.Internet, State = SupplyState.Active, OutageFrom = d * 24 + 10, OutageUntil = d * 24 + 12 };   // 10:00 to 12:00
        int ten = d * 1440 + 10 * 60, noon = d * 1440 + 12 * 60;
        Check(InternetRules.OutageBetween(a, ten - 90, ten) && InternetRules.OutageBetween(a, noon - 1, noon + 60) && InternetRules.OutageBetween(a, ten + 30, ten + 30)
              && !InternetRules.OutageBetween(a, ten - 90, ten - 1) && !InternetRules.OutageBetween(a, noon, noon + 60) && !InternetRules.OutageBetween(a, ten + 1440, noon + 1440),
            "outage excuse: an outage counts over a span that meets any minute of it, and not the day after");
        Check(InternetRules.OutageBetween(a, ten, ten) == InternetRules.InOutage(a, d * 24 + 10) && InternetRules.OutageBetween(a, noon, noon) == InternetRules.InOutage(a, d * 24 + 12),
            "outage excuse: a single minute reads as the hour's outage does");
        Check(InternetRules.Down(a, ten, ten + 60) == NetDown.Outage && InternetRules.Down(a, noon, noon + 60) == NetDown.Up,
            "outage excuse: a connected plan reads as an outage over the outage, and up after");
        var none = new RealLifeOverhaul.Utilities.Account { Service = Service.Internet, State = SupplyState.Active };
        var closed = new RealLifeOverhaul.Utilities.Account { Service = Service.Internet, State = SupplyState.Closed, OutageFrom = a.OutageFrom, OutageUntil = a.OutageUntil };
        var pending = new RealLifeOverhaul.Utilities.Account { Service = Service.Internet, State = SupplyState.Pending };
        var cut = new RealLifeOverhaul.Utilities.Account { Service = Service.Internet, State = SupplyState.Cut, CutForDebt = true, OutageFrom = a.OutageFrom, OutageUntil = a.OutageUntil };
        Check(InternetRules.Down(none, ten, noon) == NetDown.Up && InternetRules.Down(null, ten, noon) == NetDown.NoPlan && InternetRules.Down(closed, ten, noon) == NetDown.NoPlan
              && InternetRules.Down(pending, ten, noon) == NetDown.Installing && InternetRules.Down(cut, ten, noon) == NetDown.Cut,
            "outage excuse: no plan, a closed one, one being put in and one cut for an unpaid bill are said as such, never as an outage, even over an old outage's hours");
    }

    // ---------------------------------------------------------------- the save
    private static void OutageSave()
    {
        var f = new StudyFile { Seed = 9 };
        f.Courses.Add(new Enrolment { Course = StudyCatalogue.CodingBootcamp, Mode = (int)StudyMode.FullTime, Start = 1, Seen = 4, Done = 1, SettledDay = 8, Stage = (int)EnrolStage.Studying,
            StartTold = 777, Excused = 3, ExcusedDay = 8 });
        string line = CareersStore.Write(f);
        StudyFile back = CareersStore.Read(line, out _);
        Check(back.Courses.Count == 1 && back.Courses[0].Excused == 3 && back.Courses[0].ExcusedDay == 8 && CareersStore.Write(back) == line,
            "outage excuse: the excused classes and the last one's day read back from the save's line");
        string old = line.Replace("|777|3|8", "|777");
        StudyFile was = CareersStore.Read(old, out _);
        Check(old != line && was.Courses.Count == 1 && was.Courses[0].Excused == 0 && was.Courses[0].ExcusedDay == -1 && was.Courses[0].Seen == 4,
            "outage excuse: a save from before them loads with no class excused");
        string over = line.Replace("|777|3|8", "|777|9|8");
        Check(CareersStore.Read(over, out _).Courses[0].Excused == 4, "outage excuse: more excused than held reads as no more than held");
    }

    // ---------------------------------------------------------------- the texts
    private static void OutageTexts()
    {
        string folder = LocalesFolder();
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(folder, "en.json")), pt = LoadWords(System.IO.Path.Combine(folder, "pt.json")),
            zh = LoadWords(System.IO.Path.Combine(folder, "zh-cn.json"));
        if (en == null || pt == null || zh == null)
        {
            Check(false, "outage excuse: the texts are at " + folder);
            return;
        }
        var words = new (string Key, string[] Names, bool Notice)[]
        {
            ("reallife_study_why_outage", new string[0], false), ("reallife_study_note_class_outage", new[] { "{course}" }, true),
            ("reallife_study_note_class_excused", new[] { "{course}", "{day}" }, true), ("reallife_campus_excused_one", new[] { "{day}" }, false),
            ("reallife_campus_excused", new[] { "{count}", "{day}" }, false), ("reallife_work_text_outage", new string[0], false),
            ("reallife_work_note_outage", new[] { "{employer}" }, true), ("reallife_work_note_interview_moved", new[] { "{employer}", "{day}", "{hour}" }, true),
            ("reallife_study_test_outage", new string[0], false), ("reallife_study_test_outage_none", new string[0], true), ("reallife_study_test_outage_no_day", new string[0], true)
        };
        var missing = new List<string>();
        foreach ((string key, string[] names, bool notice) in words)
        {
            foreach (Dictionary<string, string> lang in new[] { en, pt, zh })
            {
                if (!lang.TryGetValue(key, out string text) || string.IsNullOrWhiteSpace(text) || Array.Exists(names, n => text.IndexOf(n, StringComparison.Ordinal) < 0))
                {
                    missing.Add(key);
                }
            }
            if (notice && en.TryGetValue(key, out string said))
            {
                string told = said.Replace("{course}", "Computer Science (BSc)").Replace("{employer}", "Northwind Analytics Consulting").Replace("{day}", "Wednesday 28").Replace("{hour}", "11:00");
                Check(told.Length <= 240, "outage excuse: " + key + " fits a notice (" + told.Length + " characters)");
            }
        }
        Check(missing.Count == 0, "outage excuse: every new text is in en, pt and zh-cn with its {names}" + (missing.Count > 0 ? ": " + string.Join(", ", missing) : string.Empty));
        Check(en.TryGetValue("reallife_guide_careers_study", out string study) && study.Contains("internet provider had an outage is excused")
              && en.TryGetValue("reallife_guide_careers_jobs", out string jobs) && jobs.Contains("A day off sick is excused. So is a day from home") && jobs.Contains("moves it to the next weekday")
              && en.TryGetValue("reallife_guide_careers_life", out string life) && life.Contains("lost to your internet provider's outage, is excused"),
            "outage excuse: the Guide's courses, jobs and working life pages say what an outage excuses");
        Check(OutageSaid(en, "Fixed: when your internet provider has an outage, an online class") && OutageSaid(pt, "Corrigido: quando o seu provedor de internet fica fora do ar")
              && OutageSaid(zh, "已修复：当你的网络服务商断网时"),
            "outage excuse: What's new has the fix's line in en, pt and zh-cn, in some reallife_changes_ row");
    }

    // Every reallife_changes_ row, anywhere in the row, never only the next row: the release turns the next row into the
    // version's own.
    private static bool OutageSaid(Dictionary<string, string> texts, string words)
    {
        foreach (KeyValuePair<string, string> pair in texts)
        {
            if (pair.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && !pair.Key.EndsWith("_title", StringComparison.Ordinal)
                && pair.Value != null && pair.Value.IndexOf(words, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- the game's side, read as text
    private static void OutageSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        int said = 0;
        var where = new List<string>();
        foreach (string file in System.IO.Directory.GetFiles(scripts, "*.cs", System.IO.SearchOption.AllDirectories))
        {
            string text = System.IO.File.ReadAllText(file);
            if (text.Contains("\"reallife_study_why_no_internet\""))
            {
                said++;
                where.Add(System.IO.Path.GetFileName(file));
            }
        }
        Check(said == 1 && where[0] == "StudyRules.cs", "outage excuse: every activity refused for the home's internet gives its reason through the one rule (StudyRules.NoNetWhy): " + string.Join(", ", where));
        string study = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "Careers", "CareersDirector.Study.cs"));
        string work = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "Careers", "CareersDirector.Work.cs"));
        Check(study.Contains("StudyRules.ClassExcused(") && study.Contains("StudyRules.Excuse(e, day)") && !study.Contains("changed |= StudyRules.Miss(e, day);")
              && !study.Contains("&& StudyRules.Miss(e, d))"),
            "outage excuse: both ways a class goes by (its hour, and the days caught up) decide the excuse");
        Check(work.Contains("ProRules.DayExcused(") && work.Contains("OutageExcused(j, day, from, to)") && work.Contains("ProRules.InterviewExcused(") && work.Contains("!InterviewMoved(a, day)"),
            "outage excuse: a working day settled and an interview lapsing both ask what an outage excuses");
    }
}
