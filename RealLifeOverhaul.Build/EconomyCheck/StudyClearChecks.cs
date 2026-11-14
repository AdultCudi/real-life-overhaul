using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Tech;

// Study and careers said clearly (Scripts/Careers/Study/StudyNeedRules, CareersDirector.Why and .Needs, the two Campus apps, ExamDesks): where
// each course's exam is sat, that the pages and the notices say it, and that every refusal names what is short with its numbers.
internal static partial class Program
{
    private static void StudyClear()
    {
        StudyClearExamPlaces();
        StudyClearAsks();
        StudyClearTexts();
        StudyClearSources();
    }

    private static readonly string[] ExamWhyKeys = { "reallife_study_why_exam_school", "reallife_study_why_exam_university", "reallife_study_why_exam_hospital" };

    // Every course of the catalogue, in every way it can be followed: its exam has a place, the place is the one the rule sits it at, and anywhere else says where to go.
    private static void StudyClearExamPlaces()
    {
        var here = new[]
        {
            new StudyHere(), new StudyHere { School = true }, new StudyHere { University = true }, new StudyHere { Hospital = true },
            new StudyHere { Home = true, Online = true }
        };
        int desk = 0, room = 0, ward = 0, bad = 0, modes = 0;
        for (int i = 0; i < StudyCatalogue.All.Length; i++)
        {
            CourseDef c = StudyCatalogue.All[i];
            for (int m = 0; m < 3; m++)
            {
                var mode = (StudyMode)m;
                if ((mode == StudyMode.PartTime && (!c.Timetabled || c.Kind == CourseKind.Short)) || (mode == StudyMode.Distance && (!c.Distance || c.Campus == Campus.Online)))
                {
                    continue;   // not a way this course is followed
                }
                modes++;
                ExamPlace place = StudyNeedRules.ExamPlaceOf(c, mode);
                desk += place == ExamPlace.Desk ? 1 : 0;
                room += place == ExamPlace.Room ? 1 : 0;
                ward += place == ExamPlace.Ward ? 1 : 0;
                for (int h = 0; h < here.Length; h++)
                {
                    string why = StudyRules.AtExam(c, mode, here[h]);
                    bool right = place == ExamPlace.Desk ? here[h].School : place == ExamPlace.Room ? here[h].University : here[h].Hospital;
                    bool says = Array.IndexOf(ExamWhyKeys, why) == (int)place;   // a wrong place names the place it is sat at, not another's
                    if (right ? why != null : !says || !StudyRules.Elsewhere(why))
                    {
                        bad++;
                        Console.WriteLine("study clear: " + c.Key + " " + mode + " exam at place " + h + " gave " + (why ?? "none") + ", the exam is sat at " + place);
                    }
                }
            }
        }
        Check(bad == 0, "study clear: every course's exam is sat at one stated place (" + desk + " at a study desk, " + room + " in a classroom, " + ward + " on a ward, over " + modes
                        + " ways to follow a course), and anywhere else the reason names that place");
        bool certs = true, wards = true, rooms = true;
        for (int i = 0; i < StudyCatalogue.All.Length; i++)
        {
            CourseDef c = StudyCatalogue.All[i];
            certs &= c.Kind != CourseKind.Certificate || StudyNeedRules.ExamPlaceOf(c, StudyMode.FullTime) == ExamPlace.Desk;
            wards &= c.Campus != Campus.Hospital || StudyNeedRules.ExamPlaceOf(c, StudyMode.FullTime) == ExamPlace.Ward;
            rooms &= c.Campus != Campus.University || StudyNeedRules.ExamPlaceOf(c, StudyMode.FullTime) == ExamPlace.Room;
        }
        CourseDef bar = StudyCatalogue.Find(StudyCatalogue.BarExam);
        Check(certs && wards && rooms && StudyNeedRules.ExamPlaceOf(bar, StudyMode.FullTime) == ExamPlace.Desk && StudyNeedRules.ExamOpenDays(bar) == 14
              && StudyRules.AtExam(bar, StudyMode.FullTime, here[2]) == "reallife_study_why_exam_school" && StudyRules.AtExam(bar, StudyMode.FullTime, here[4]) == "reallife_study_why_exam_school",
            "study clear: a certificate such as the bar exam is studied at home and sat at a study desk inside the school within 14 days; the university and home both say where to go");
        Check(StudyNeedRules.MarkedInClass(StudyCatalogue.Find(StudyCatalogue.FirstAid)) && !StudyNeedRules.MarkedInClass(bar)
              && StudyNeedRules.ExamOpenDays(StudyCatalogue.Find(StudyCatalogue.Law)) == 7 && StudyNeedRules.ExamOpenDays(StudyCatalogue.Find(StudyCatalogue.FirstAid)) == 7,
            "study clear: a short course is marked in its last class, a degree's exam stays open a week");
        // The window: a weekend inside it gives the next weekday, none once it has gone.
        var e = new Enrolment { Course = StudyCatalogue.Law, Stage = (int)EnrolStage.Exam, ExamFrom = 6, ExamTo = 13 };   // day 1 is a Monday: 6 and 7 are the weekend
        Check(StudyNeedRules.ExamOpen(e, 6) && StudyNeedRules.ExamNextDay(e, 6) == 8 && StudyNeedRules.ExamNextDay(e, 14) == -1 && !StudyNeedRules.ExamOpen(e, 14),
            "study clear: an exam open over a weekend says the next weekday it can be sat, and none after its last day");
    }

    // What a course asks, the levels beside each, and what raises a skill.
    private static void StudyClearAsks()
    {
        var f = new StudyFile { Seed = 4 };
        CourseDef finance = StudyCatalogue.Find(StudyCatalogue.Finance);
        CourseDef bar = StudyCatalogue.Find(StudyCatalogue.BarExam);
        f.Skill[TalentCatalogue.Accounting] = 12000;
        var asks = new List<Ask>(6);
        StudyNeedRules.Asks(f, finance, false, 100, 2933, 0, 5, asks);
        Ask skill = asks.Find(a => a.Kind == AskKind.Skill);
        Ask cash = asks.Find(a => a.Kind == AskKind.Cash);
        Check(skill.Kind == AskKind.Skill && skill.Id == TalentCatalogue.Accounting && skill.Need == 30 && skill.Have == 12 && !skill.Met && cash.Need == 2933 && cash.Have == 100 && !cash.Met
              && StudyNeedRules.Short(asks) == 2,
            "study clear: finance asks Accounting 30 and the player has 12, and the fee to pay now is short: both are named with their numbers, none left out");
        Check(skill.Need == 30 && finance.GainA == 40 && finance.SkillA == TalentCatalogue.Analysis,
            "study clear: the 40 and 20 of a degree are what it teaches, never the floor it asks (finance asks Accounting 30 only)");
        f.Skill[TalentCatalogue.Accounting] = 31000;
        StudyNeedRules.Asks(f, finance, false, 5000, 2933, 0, 5, asks);
        Check(StudyNeedRules.Short(asks) == 0, "study clear: a player with the skill, the money and a seat is short of nothing");
        StudyNeedRules.Asks(f, bar, false, 5000, 2200, 0, -1, asks);
        Ask course = asks.Find(a => a.Kind == AskKind.Course);
        Check(course.Kind == AskKind.Course && !course.Met && course.Have == 0 && course.Need == 1, "study clear: the bar exam asks the Legal Practice Course passed first, and says none is");
        f.Results.Add(new Enrolment { Course = StudyCatalogue.LegalPractice, Stage = (int)EnrolStage.Passed });
        StudyNeedRules.Asks(f, bar, false, 5000, 2200, 0, -1, asks);
        Check(asks.Find(a => a.Kind == AskKind.Course).Met, "study clear: once it is passed the page ticks it");
        for (int i = 0; i < StudyRules.LiveMost; i++)
        {
            f.Courses.Add(new Enrolment { Course = StudyCatalogue.FirstAid + i });
        }
        StudyNeedRules.Asks(f, finance, true, 5000, 2933, 1000, 5, asks);
        Ask slots = asks.Find(a => a.Kind == AskKind.Slots), loan = asks.Find(a => a.Kind == AskKind.Loan);
        Check(slots.Kind == AskKind.Slots && !slots.Met && slots.Have == 4 && loan.Kind == AskKind.Loan && loan.Have == 1000 && loan.Need == 2933 && !loan.Met,
            "study clear: four courses at once and a student loan with too little room are named with their numbers");

        // every course that asks a skill: the floor is a number above 0, and something in the catalogue raises that skill
        int floors = 0, missing = 0;
        var courses = new List<int>(4);
        var jobs = new List<int>(4);
        for (int i = 0; i < StudyCatalogue.All.Length; i++)
        {
            CourseDef c = StudyCatalogue.All[i];
            if (!RosterRules.Floor(c, out int s, out int points))
            {
                continue;
            }
            floors++;
            StudyNeedRules.RaisingCourses(s, c.Id, null, courses);
            StudyNeedRules.RaisingProfessions(s, jobs);
            missing += points > 0 && (courses.Count > 0 || jobs.Count > 0) ? 0 : 1;
        }
        Check(floors >= 5 && missing == 0, "study clear: every skill a course asks for (" + floors + " courses) has a floor above 0 and a course or a job that raises it");
        StudyNeedRules.RaisingCourses(TalentCatalogue.Accounting, -1, null, courses);
        StudyNeedRules.RaisingProfessions(TalentCatalogue.Coding, jobs);
        Check(courses.Contains(StudyCatalogue.Accounting) && courses.Contains(StudyCatalogue.Bookkeeping) && courses[0] == StudyCatalogue.Accounting
              && jobs.Contains(ProCatalogue.Developer),
            "study clear: Accounting is raised by the accounting degree and bookkeeping, Coding by work as a developer, from the same catalogue");

        // a notice has room for 240 characters: the first sentence is always kept, a later one only whole
        string said = StudyNeedRules.Fit(new[] { new string('a', 100), new string('b', 100), new string('c', 60), new string('d', 30) }, 240);
        Check(said.Length <= 240 && said.StartsWith("aaa", StringComparison.Ordinal) && said.Contains("bbb") && !said.Contains("ccc") && said.EndsWith("ddd", StringComparison.Ordinal),
            "study clear: a refusal's sentences are kept whole up to 240 characters, a shorter one after a long one still fitting");

        // a post's missing qualification is named: a group of courses of which any will do, and a certificate's course
        var student = new StudyFile { Seed = 6 };
        Profession lawyer = ProCatalogue.Find(ProCatalogue.All.Length > 0 ? FirstWithQuals() : 0);
        var groups = new List<int[]>(2);
        var certs = new List<int>(2);
        int rank = lawyer.Ranks.Length - 1;
        StudyNeedRules.PostMissing(student, lawyer, rank, groups, certs);
        Check(ProRules.Qualified(student, lawyer, rank) == (groups.Count == 0 && certs.Count == 0),
            "study clear: a post the player is not qualified for names what is missing (any one of its courses, its certificate's course), and a qualified one names nothing");
    }

    private static int FirstWithQuals()
    {
        for (int i = 0; i < ProCatalogue.All.Length; i++)
        {
            for (int r = 0; r < ProCatalogue.All[i].Ranks.Length; r++)
            {
                if (ProCatalogue.All[i].Ranks[r].Quals.Length > 0 || ProCatalogue.All[i].Ranks[r].Cert >= 0)
                {
                    return i;
                }
            }
        }
        return 0;
    }

    private static readonly string[][] FigureTexts =
    {
        // the figure keys: a text and the names its producer fills in (each a refusal that names its numbers)
        new[] { "reallife_study_why_tired", "need", "have" },
        new[] { "reallife_study_why_fee_owed", "amount" },
        new[] { "reallife_study_why_exam_hours", "opens", "closes", "now" },
        new[] { "reallife_study_why_exam_day", "from", "to" },
        new[] { "reallife_study_why_ceremony_hour", "day", "hour", "today", "now" },
        new[] { "reallife_study_why_short_money", "what", "need", "have", "short" },
        new[] { "reallife_study_short_skill", "skill", "need", "have" },
        new[] { "reallife_study_short_course", "needs" },
        new[] { "reallife_study_short_cash", "need", "have" },
        new[] { "reallife_study_short_loan", "need", "have" },
        new[] { "reallife_study_short_seats", "day" },
        new[] { "reallife_study_short_slots", "n" },
        new[] { "reallife_study_ask_skill", "skill", "need" },
        new[] { "reallife_study_ask_have", "have" },
        new[] { "reallife_study_ask_course", "needs" },
        new[] { "reallife_study_ask_cash", "need" },
        new[] { "reallife_study_ask_loan", "need" },
        new[] { "reallife_study_raise_both", "skill", "courses", "jobs" },
        new[] { "reallife_study_raise_courses", "skill", "courses" },
        new[] { "reallife_study_raise_work", "skill", "jobs" },
        new[] { "reallife_study_exam_where_desk", "school" },
        new[] { "reallife_study_exam_where_room", "place" },
        new[] { "reallife_study_exam_line_open", "where", "opens", "closes", "day" },
        new[] { "reallife_study_exam_line_retake", "where", "opens", "closes", "from", "to" },
        new[] { "reallife_study_exam_line_later_classes", "where", "opens", "closes", "days" },
        new[] { "reallife_study_exam_line_later_homework", "where", "opens", "closes", "days" },
        new[] { "reallife_study_exam_line_short", "where", "days" },
        new[] { "reallife_study_skip_exam", "course", "pass" },
        new[] { "reallife_study_skip_retake", "course", "pass" },
        new[] { "reallife_study_note_exam_open", "course", "where", "opens", "closes", "day" },
        new[] { "reallife_study_note_exam_open_home", "course", "where", "opens", "closes", "day" },
        new[] { "reallife_study_note_exam_today", "course", "where", "opens", "closes", "day" },
        new[] { "reallife_study_note_exam_elsewhere", "course", "where", "day" },
        new[] { "reallife_study_note_enrolled", "course", "day", "hour", "place" },
        new[] { "reallife_study_note_enrolled_home", "course", "where" },
        new[] { "reallife_work_why_unqualified", "missing" },
        new[] { "reallife_work_why_skills", "a", "b", "need", "have", "least", "raise" },
        new[] { "reallife_work_why_no_chance", "points" },
        new[] { "reallife_ladder_why_days", "have", "need" },
        new[] { "reallife_ladder_why_score", "have", "need" },
        new[] { "reallife_ladder_why_skills", "a", "b", "have", "need", "raise" },
        new[] { "reallife_ladder_why_stores", "have", "need" },
        new[] { "reallife_ladder_why_recommend", "boss" },
        new[] { "reallife_school_no_taxi_held", "days", "held" },
        new[] { "reallife_school_no_taxi_points", "points", "most" },
        new[] { "reallife_school_short_money", "fee", "have" }
    };

    // A refusal's text holds every name its producer fills in, so the numbers reach the player; the notices fit 240 characters with the longest values; no dashes, no exclamations.
    private static void StudyClearTexts()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Check(en != null, "study clear texts: en.json is read");
        if (en == null)
        {
            return;
        }
        var missing = new List<string>();
        var absent = new List<string>();
        for (int i = 0; i < FigureTexts.Length; i++)
        {
            string key = FigureTexts[i][0];
            if (!en.TryGetValue(key, out string text))
            {
                absent.Add(key);
                continue;
            }
            for (int n = 1; n < FigureTexts[i].Length; n++)
            {
                if (!text.Contains("{" + FigureTexts[i][n] + "}"))
                {
                    missing.Add(key + " lacks {" + FigureTexts[i][n] + "}");
                }
            }
        }
        Check(absent.Count == 0 && missing.Count == 0, "study clear texts: every refusal and exam line is in en.json and names its figures (" + FigureTexts.Length + " texts)"
              + (absent.Count > 0 ? "; absent " + string.Join(", ", absent) : string.Empty) + (missing.Count > 0 ? "; " + string.Join(", ", missing) : string.Empty));
        // the figure keys of a refusal name what is needed and what the player has
        string[] numbered = { "reallife_study_why_tired", "reallife_study_short_skill", "reallife_study_short_cash", "reallife_study_short_loan", "reallife_study_why_short_money",
            "reallife_work_why_skills", "reallife_ladder_why_days", "reallife_ladder_why_score", "reallife_ladder_why_skills", "reallife_ladder_why_stores", "reallife_school_short_money" };
        bool both = true;
        for (int i = 0; i < numbered.Length; i++)
        {
            string text = en.TryGetValue(numbered[i], out string t) ? t : string.Empty;
            both &= text.Contains("{need}") && text.Contains("{have}") || (numbered[i] == "reallife_school_short_money" && text.Contains("{fee}") && text.Contains("{have}")) || (numbered[i] == "reallife_study_why_short_money");
        }
        Check(both, "study clear texts: a skill, a fee, a loan, energy, days and a score are said as the level needed and the level the player has");
        // every text of the set: no dash, no exclamation mark
        var style = new List<string>();
        for (int i = 0; i < FigureTexts.Length; i++)
        {
            if (en.TryGetValue(FigureTexts[i][0], out string text) && (text.Contains("!") || text.Contains("—") || text.Contains("–") || text.Contains(" - ")))
            {
                style.Add(FigureTexts[i][0]);
            }
        }
        Check(style.Count == 0, "study clear texts: no dash and no exclamation mark" + (style.Count > 0 ? " (" + string.Join(", ", style) + ")" : string.Empty));
        // a notice fits 240 characters with the longest values its producer gives
        string[] notices = { "reallife_study_note_exam_open", "reallife_study_note_exam_open_home", "reallife_study_note_exam_today", "reallife_study_note_exam_elsewhere",
            "reallife_study_note_enrolled", "reallife_study_note_enrolled_home", "reallife_study_why_tired", "reallife_study_why_exam_hours", "reallife_study_why_exam_day",
            "reallife_study_why_ceremony_hour", "reallife_study_why_exam_school", "reallife_study_why_exam_university", "reallife_study_why_exam_hospital",
            "reallife_study_why_short_money", "reallife_work_why_unqualified", "reallife_ladder_why_skills" };
        string where = "its classroom at Hudson City University, 1234 Long Street Name, floor 4, room 5";
        var worst = new Dictionary<string, string>
        {
            { "course", new string('c', 34) }, { "where", where }, { "place", where }, { "opens", "09:00" }, { "closes", "17:00" }, { "day", "Day 365" }, { "from", "Day 365" },
            { "to", "Day 365" }, { "hour", "14:00" }, { "need", "$18,000" }, { "have", "$18,000" }, { "now", "17:59" }, { "today", "Day 365" }, { "what", "fee to pay now" },
            { "short", "$18,000" }, { "missing", "one of Law or Legal Practice Course; Chartered Accountant" }, { "amount", "$6,000" }, { "a", "System design" }, { "b", "Troubleshooting" },
            { "raise", "Coding goes up with the Coding Bootcamp or Computer Science, or with work as a developer." }
        };
        var tooLong = new List<string>();
        for (int i = 0; i < notices.Length; i++)
        {
            string text = en.TryGetValue(notices[i], out string t) ? t : string.Empty;
            foreach (KeyValuePair<string, string> pair in worst)
            {
                text = text.Replace("{" + pair.Key + "}", pair.Value);
            }
            if (text.Length > 240)
            {
                tooLong.Add(notices[i] + " " + text.Length);
            }
        }
        Check(tooLong.Count == 0, "study clear texts: every notice and refusal fits 240 characters with the longest figures" + (tooLong.Count > 0 ? " (" + string.Join(", ", tooLong) + ")" : string.Empty));
    }

    // The sources: the exam is said on both interfaces, in the notice and at the desks; every refusal that names figures is made through one place; the wrong place tells where to go.
    private static void StudyClearSources()
    {
        string why = ModSource(@"Careers\CareersDirector.Why.cs");
        string needs = ModSource(@"Careers\CareersDirector.Needs.cs");
        string study = ModSource(@"Careers\CareersDirector.Study.cs");
        string coursework = ModSource(@"Careers\CareersDirector.Coursework.cs");
        string old = ModSource(@"Careers\CampusApp.cs") + ModSource(@"Careers\CampusApp.Studying.cs");
        string kit = ModSource(@"Ui\Work\CampusLive.cs");
        Check(old.Contains("ExamLine(") && old.Contains("EnrolWhy(") && old.Contains("AskWords(") && old.Contains("ShowCourse(") && old.Contains("ShowExam(") && old.Contains("interactable = false"),
            "study clear: the old Campus app says where the exam is sat, lists what the course asks with the player's levels, dims Enrol with the reason and has Show on map");
        Check(kit.Contains("ExamLine(") && kit.Contains("EnrolWhy(") && kit.Contains("AskWords(") && kit.Contains("ShowOnMap(") && kit.Contains("d.ShowCourse(") && kit.Contains("loanWhy"),
            "study clear: the kit's Campus says the same on every course and every course under way, dims Enrol with the reason and has Show on map");
        string open = MemberText(why, "private void TellExam(");
        Check(open.Contains("ExamWhere(") && open.Contains("Guard.Wrap(() => ExamClick(course))") && why.Contains("ShowExam(e.Def"),
            "study clear: the notice that an exam is open says where, by which day, and its click shows the place on the map");
        string after = MemberText(coursework, "private bool AfterClass(") + coursework;
        Check(after.Contains("TellExam(\"reallife_study_note_exam_open\", e)") && after.Contains("TellExam(\"reallife_study_note_exam_open_home\", e)")
              && study.Contains("ExamsToday(day)") && study.Contains("said[\"where\"] = ExamWhere("),
            "study clear: the notice at the end of the classes, at the end of a certificate's homework, each morning an exam is open and at enrolling all say where it is sat");
        string guard = ModSource(@"Careers\CareersDirector.Prompt.cs");
        Check(guard.Contains("ExamDesksTick();") && guard.Contains("ExamElsewhere();") && why.Contains("private void ExamDesksTick()") && why.Contains("AtTheSchool(e, true) && StudyNeedRules.ExamOpen(e, Save.Day)")
              && why.Contains("!AtUniversity()"),
            "study clear: the school's desks are marked while an exam of the player's is open there, and the university says where an exam not sat there is sat");
        string desks = ModSource(@"Careers\ExamDesks.cs");
        Check(desks.Contains("customOverlayHeaderKey = HeaderKey") && desks.Contains("customOverlayHeaderKey = _headers[i]") && desks.Contains("_marks.Set(") && desks.Contains("_mark.Show(")
              && desks.Contains("_mark.Hide()") && desks.Contains("_marks.Clear()") && guard.Contains("_examDesks?.Clear()") && study.Contains("_examDesks?.Clear()"),
            "study clear: an exam desk carries the game's mark, its name and the destination marker, and all are given back when the exam is over, the player leaves or study stops");
        // the figures reach the player: every refusal that names them is returned through its one producer, never as a bare key
        string all = string.Empty;
        foreach (string file in new[] { "CareersDirector.cs", "CareersDirector.ClassPlay.cs", "CareersDirector.DeskDay.cs", "CareersDirector.Prompt.cs", "CareersDirector.Coursework.cs",
                     "CareersDirector.Study.cs", "CareersDirector.Family.cs", "CareersDirector.Degrees.cs", "CareersDirector.Applying.cs" })
        {
            all += ModSource(@"Careers\" + file);
        }
        Check(!all.Contains("return \"reallife_study_why_tired\"") && !all.Contains("return \"reallife_study_why_fee_owed\"") && !all.Contains("return \"reallife_study_why_money\"")
              && !all.Contains("return \"reallife_work_why_unqualified\"") && !all.Contains("return \"reallife_work_why_skills\"") && !all.Contains("return \"reallife_study_why_floor\"")
              && !all.Contains("return \"reallife_study_why_ceremony_hour\"") && !all.Contains("return \"reallife_study_why_loan_full\""),
            "study clear: no refusal that has figures is returned as a bare key (tired, a fee owed, money, a skill short, a qualification missing, graduation's hour)");
        Check(MemberText(ModSource(@"Careers\CareersDirector.ClassPlay.cs"), "private DeskStart DeskStartOf(").Contains("OwedWhy(e)")
              && coursework.Contains("OwedWhy(e)") && coursework.Contains("reallife_study_why_exam_day") && coursework.Contains("reallife_study_why_exam_hours\", \"opens\""),
            "study clear: an exam refused for the hour or the day says the hours, today and the next day it can be sat; a fee owed says its amount");
        Check(needs.Contains("\"reallife_work_why_unqualified\", \"missing\"") && needs.Contains("\"reallife_work_why_skills\", \"a\"") && needs.Contains("\"reallife_work_why_no_chance\", \"points\"")
              && needs.Contains("LadderWhy(") && needs.Contains("RaiseLine(a, -1)") && ModSource(@"Careers\CareersDirector.Ladder.cs").Contains("LadderWhyNot(LadderFile f) => LadderWhy(f)")
              && ModSource(@"Careers\CareersDirector.Applying.cs").Contains("ApplyWhy(o)") && ModSource(@"Ui\Work\CareerLive.cs").Contains("d.ApplyWhy(o, template)")
              && ModSource(@"Careers\CareerApp.Jobs.cs").Contains("_director.ApplyWhy(o, template)"),
            "study clear: applying for a post and the next rank on the ladder name what is short in figures and what raises it, on both interfaces");
        string school = ModSource(@"Licences\Driving\DrivingSchool.cs");
        Check(school.Contains("{held}") && school.Contains("{points}") && school.Contains("{fee}") && school.Contains("ReasonText(reason, service)") && ModSource(@"Licences\SchoolDesk.cs").Contains("ReasonText(reason, service)"),
            "study clear: the driving school's refusals say the days held, the points held and the fee against what the player has");
        Check(ModSource(@"Careers\CareersText.cs").Contains("key.IndexOf(CareersWhy.Sep) >= 0 ? CareersWhy.Say(key)")
              && ModSource(@"Careers\CareersWhy.cs").Contains("public static string Say(string why)"),
            "study clear: a reason with figures is said with them wherever it is shown");
    }
}
