using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Tech;

// What the education's marks, homework and final grades are made of (Scripts/Careers/Study/StudyRules.Marks.cs and .Learning.cs, RosterRules.ExamEdgeOf,
// CareersDirector.Coursework and .Marks, Sitting.StartEnergy): a sat result never moves, every piece of homework a course promises can be done before it is
// marked, a diligent student's mark is in the top band whatever the day's luck, the page's forecast is the mark the sitting gives, and the pages say so.
internal static partial class Program
{
    private static void SchoolMarks()
    {
        SchoolMarkSat();
        SchoolMarkLuck();
        SchoolMarkHomework();
        SchoolMarkGrades();
        SchoolMarkForecast();
        SchoolMarkEdge();
        SchoolMarkSources();
        SchoolMarkTexts();
    }

    // ---------------------------------------------------------------- a result once sat
    private static void SchoolMarkSat()
    {
        CourseDef plumber = StudyCatalogue.Find(StudyCatalogue.Plumber);
        var f = new StudyFile { Seed = 77 };
        var e = new Enrolment { Course = plumber.Id, Start = 1, Seen = plumber.Sessions, Done = plumber.Sessions, Homework = plumber.Homework, Stage = (int)EnrolStage.Exam, ExamFrom = 20, ExamTo = 27 };
        f.Courses.Add(e);
        StudyRules.Sit(e, 90, 21);
        f.Finish(e);
        bool moved = false;
        for (int day = 22; day < 400; day++)
        {
            StudyRules.ExamMissed(f.Results[0], day);                       // a result is not live: no window to lapse
            StudyRules.Fade(f, day);                                        // the day's pass on the skills
            StudyRules.Studied(f, day, 0);
            if (day % 40 == 0)
            {
                f = CareersStore.Read(CareersStore.Write(f), out int _);    // a save and a load
            }
            moved |= f.Results[0].Score != 90 || f.Results[0].Grade != StudyRules.Grade(plumber, 90) || f.Results[0].Stage != (int)EnrolStage.Passed;
        }
        Check(!moved && f.Results[0].Score == 90, "school marks: a mark sat (90, a distinction) is the same after 380 days, their passes and nine saves and loads");

        // A fail with a retake to come keeps its mark when the retake's window goes by unsat, as many times as it does.
        var r = new Enrolment { Course = plumber.Id, Start = 1, Seen = plumber.Sessions, Done = plumber.Sessions, Homework = plumber.Homework, Stage = (int)EnrolStage.Exam, ExamFrom = 20, ExamTo = 27 };
        StudyRules.Sit(r, 45, 21);
        Check(r.Stage == (int)EnrolStage.Retake && r.Score == 45, "school marks: a fail of 45 leaves a retake with the mark 45 shown");
        bool kept = true;
        for (int day = 22; day < 80 && r.Stage != (int)EnrolStage.Failed; day++)
        {
            StudyRules.ExamMissed(r, day);
            kept &= r.Score == 45 && r.Grade == 0;
        }
        Check(kept && r.Stage == (int)EnrolStage.Failed && r.Retakes == StudyCatalogue.RetakesMost && r.Score == 45,
            "school marks: a retake not sat in its days costs the retake but never turns the mark 45 into 0, however many days go by");
        var never = new Enrolment { Course = plumber.Id, Start = 1, Stage = (int)EnrolStage.Exam, ExamFrom = 20, ExamTo = 27 };
        StudyRules.ExamMissed(never, 28);
        Check(never.Stage == (int)EnrolStage.Retake && never.Score == 0, "school marks: an exam never sat has no mark, and a fail with the retake to come");
    }

    // ---------------------------------------------------------------- the day's luck, once
    private static void SchoolMarkLuck()
    {
        bool same = true, bounded = true, varied = false;
        int first = int.MinValue;
        for (int i = 0; i < 300; i++)
        {
            var e = new Enrolment { Course = i % 50, Start = 1 + i, Retakes = i % 3 };
            int a = StudyRules.Luck(4242, e), b = StudyRules.Luck(4242, e);
            var copy = new Enrolment { Course = e.Course, Start = e.Start, Retakes = e.Retakes, Seen = 9, Done = 4, Homework = 3 };   // what is done since changes nothing
            same &= a == b && a == StudyRules.Luck(4242, copy);
            bounded &= a >= -StudyCatalogue.ExamNoise && a <= StudyCatalogue.ExamNoise;
            first = first == int.MinValue ? a : first;
            varied |= a != first;
        }
        Check(same && bounded && varied && StudyCatalogue.ExamNoise <= 5, "school marks: the day's luck is drawn once for a sitting (the same on every read, day and load), within five either way, and still differs between sittings");
    }

    // ---------------------------------------------------------------- homework: every piece promised can be done
    private static void SchoolMarkHomework()
    {
        int courses = 0, bad = 0, late = 0, fewer = 0;
        for (int i = 0; i < StudyCatalogue.All.Length; i++)
        {
            CourseDef c = StudyCatalogue.All[i];
            if (!c.Timetabled || c.Homework <= 0)
            {
                continue;
            }
            courses++;
            int before = 0;
            for (int k = 1; k <= c.Homework; k++)
            {
                int after = StudyRules.HomeworkSetAfter(c, k);
                bad += after <= before || after > c.Sessions ? 1 : 0;
                before = after;
                var at = new Enrolment { Course = c.Id, Done = after };
                var one = new Enrolment { Course = c.Id, Done = after - 1 };
                bad += StudyRules.HomeworkSet(at) < k || StudyRules.HomeworkSet(one) >= k ? 1 : 0;   // piece k comes with class "after" and not a class before
            }
            late += StudyRules.HomeworkSetAfter(c, c.Homework) >= c.Sessions ? 1 : 0;   // the last piece is set at least one class before the last
            for (int excused = 0; excused <= 1; excused++)
            {
                for (int done = 0; done <= c.Sessions - excused; done++)
                {
                    var e = new Enrolment { Course = c.Id, Done = done, Excused = excused };
                    int old = Math.Min(c.Homework, done * c.Homework / Math.Max(1, c.Sessions - excused));
                    fewer += StudyRules.HomeworkSet(e) < old ? 1 : 0;   // a save in progress never reads fewer pieces than it did
                    bad += StudyRules.HomeworkSet(e) > c.Homework || (done == 0 && StudyRules.HomeworkSet(e) != 0) ? 1 : 0;
                    bad += done == c.Sessions - excused && StudyRules.HomeworkSet(e) != c.Homework ? 1 : 0;
                    int next = StudyRules.HomeworkNext(e);
                    if (next > 0)
                    {
                        var ahead = new Enrolment { Course = c.Id, Done = next, Excused = excused };
                        var short1 = new Enrolment { Course = c.Id, Done = next - 1, Excused = excused };
                        bad += StudyRules.HomeworkSet(ahead) <= StudyRules.HomeworkSet(e) ? 1 : 0;      // the next piece does come at the class the page says
                        bad += StudyRules.HomeworkSet(short1) > StudyRules.HomeworkSet(e) ? 1 : 0;      // and not a class before it
                    }
                    else
                    {
                        bad += StudyRules.HomeworkSet(e) != c.Homework ? 1 : 0;
                    }
                }
            }
        }
        Check(bad == 0 && courses > 25, "school marks: for " + courses + " courses with classes and homework every piece has its own class it is set after, in order, and the count of open pieces and the next to come are what the set count says (" + bad + " wrong)");
        Check(late == 0, "school marks: no course sets its last piece of homework at its last class (" + late + " do): a short course is marked there");
        Check(fewer == 0, "school marks: a course in a save made before reads as many pieces set as it did, never fewer, so nothing the player already had is taken away");

        // The whole way through, as a student who does every piece as it is set: nothing is left undone when the course is marked.
        int notDone = 0, played = 0, revisionBad = 0;
        for (int i = 0; i < StudyCatalogue.All.Length; i++)
        {
            CourseDef c = StudyCatalogue.All[i];
            if (c.Homework <= 0)
            {
                continue;
            }
            var e = new Enrolment { Course = c.Id, Start = 1 };
            int day = 1;
            for (int k = 1; k <= c.Sessions; k++, day++)
            {
                StudyRules.Attend(e, day);
                if (StudyRules.MarkedAtLastClass(c) && k == c.Sessions)
                {
                    notDone += e.Homework < c.Homework ? 1 : 0;   // the moment the short course is marked
                }
                while (e.Stage == (int)EnrolStage.Studying && StudyRules.HomeworkOpen(e))
                {
                    e.Homework++;
                }
            }
            if (!c.Timetabled)
            {
                while (StudyRules.HomeworkOpen(e) && e.Homework < c.Homework)
                {
                    e.Homework++;
                }
            }
            StudyRules.Advance(e, day);
            notDone += e.Homework < c.Homework ? 1 : 0;
            int guard = 0;
            while (StudyRules.HomeworkOpen(e) && guard++ < 50)
            {
                e.Homework++;   // an exam stage keeps the piece open as revision, up to the most
            }
            revisionBad += e.Homework != c.Homework + StudyCatalogue.RevisionMost ? 1 : 0;
            played++;
        }
        Check(notDone == 0 && played > 40, "school marks: a student who does each piece as it is set has done every piece of all " + played + " courses by the time the course is marked (a short course's last class included): " + notDone + " left undone");
        Check(revisionBad == 0, "school marks: after the classes the pieces done go on as revision up to the most, in every course");

        // A save of 1.0.26 with a short course under way: the piece that could never come is open now, nothing already done is lost.
        CourseDef dm = StudyCatalogue.Find(StudyCatalogue.DigitalMarketing), boot = StudyCatalogue.Find(StudyCatalogue.CodingBootcamp);
        var saved = new Enrolment { Course = dm.Id, Start = 1, Seen = 3, Done = 3, Homework = 1, SettledDay = 3 };
        Check(StudyRules.HomeworkSet(saved) == 2 && StudyRules.HomeworkOpen(saved) && StudyRules.HomeworkWaiting(saved) == 1 && saved.Homework == 1,
            "school marks: digital marketing in a 1.0.26 save after three classes with one piece done now has the second open, and the piece done stays done");
        Check(StudyRules.HomeworkSetAfter(dm, 1) == 1 && StudyRules.HomeworkSetAfter(dm, 2) == 3 && StudyRules.HomeworkSetAfter(boot, 1) == 1 && StudyRules.HomeworkSetAfter(boot, 4) == 5,
            "school marks: digital marketing sets its pieces after classes 1 and 3 of 4, the coding bootcamp's four after classes 1, 2, 4 and 5 of 6");
    }

    // ---------------------------------------------------------------- the final grade
    private static void SchoolMarkGrades()
    {
        int diligent = 0, notTop = 0, ordinaryFail = 0;
        var byKind = new Dictionary<CourseKind, int[]>();
        foreach (int school in new[] { TalentCatalogue.SelfTaught, TalentCatalogue.Diploma, TalentCatalogue.Bachelor, TalentCatalogue.Master })
        {
            int learning = StudyRules.Learning(school);
            for (int i = 0; i < StudyCatalogue.All.Length; i++)
            {
                CourseDef c = StudyCatalogue.All[i];
                // The diligent: every class, every piece as it is set, the exam sat rested; the worst luck and the worst tutor there can be.
                var f = new StudyFile();
                var e = new Enrolment { Course = c.Id, Start = 1 };
                int day = 1;
                for (int k = 1; k <= c.Sessions; k++, day++)
                {
                    StudyRules.Attend(e, day);
                    StudyRules.Learn(f, c.SkillA, StudyRules.ClassGain(c.GainA, c.Sessions, StudyRules.Points(f, c.SkillA), learning), day);
                    if (StudyRules.MarkedAtLastClass(c) && k == c.Sessions)
                    {
                        break;   // marked as the class ends
                    }
                    while (e.Stage == (int)EnrolStage.Studying && StudyRules.HomeworkOpen(e))
                    {
                        e.Homework++;
                        StudyRules.Learn(f, c.SkillA, StudyRules.HomeworkGain(c, true, StudyRules.Points(f, c.SkillA), learning), day);
                    }
                }
                if (!c.Timetabled)
                {
                    while (StudyRules.HomeworkOpen(e) && e.Homework < c.Homework)
                    {
                        e.Homework++;
                        StudyRules.Learn(f, c.SkillA, StudyRules.HomeworkGain(c, true, StudyRules.Points(f, c.SkillA), learning), day);
                    }
                }
                int pts = StudyRules.Points(f, c.SkillA);
                ExamMark worst = StudyRules.Mark(e, pts, false, -StudyCatalogue.ExamNoise, -RosterRules.EdgeMost);
                diligent++;
                if (worst.Grade != StudyRules.TopGrade(c))
                {
                    notTop++;
                    Console.WriteLine("school marks: " + c.Key + " (school " + school + ") diligent, worst luck and tutor: " + worst.Total + " is grade " + worst.Grade + ", the top is " + StudyRules.TopGrade(c));
                }
                if (school == TalentCatalogue.SelfTaught)
                {
                    // An ordinary student: three classes in four, half the pieces that were set, rested, an ordinary day.
                    var o = new Enrolment { Course = c.Id, Start = 1 };
                    var g = new StudyFile();
                    for (int k = 1; k <= c.Sessions; k++)
                    {
                        if (k % 4 != 0)
                        {
                            StudyRules.Attend(o, k);
                            StudyRules.Learn(g, c.SkillA, StudyRules.ClassGain(c.GainA, c.Sessions, StudyRules.Points(g, c.SkillA), learning), k);
                        }
                        else
                        {
                            StudyRules.Miss(o, k);
                        }
                    }
                    o.Homework = StudyRules.HomeworkSet(o) / 2;
                    int mid = StudyRules.Mark(o, StudyRules.Points(g, c.SkillA), false, 0, 0).Total;
                    ordinaryFail += mid < c.Pass && StudyRules.Attendance(o) >= StudyCatalogue.Barred ? 1 : 0;
                    if (!byKind.TryGetValue(c.Kind, out int[] row))
                    {
                        row = new[] { 100, 0, 100, 0 };
                        byKind[c.Kind] = row;
                    }
                    row[0] = Math.Min(row[0], mid);
                    row[1] = Math.Max(row[1], mid);
                    row[2] = Math.Min(row[2], worst.Total);
                    row[3] = Math.Max(row[3], worst.Total);
                }
            }
        }
        foreach (KeyValuePair<CourseKind, int[]> kv in byKind)
        {
            Console.WriteLine("school marks: " + kv.Key + ": an ordinary student's mark " + kv.Value[0] + " to " + kv.Value[1] + "; a diligent one's worst day " + kv.Value[2] + " to " + kv.Value[3]);
        }
        Check(notTop == 0 && diligent > 150, "school marks: a student who attends every class, does every piece set and sits it rested is in the top band in all " + diligent + " cases (every course, four schoolings), on the worst luck and the worst tutor there can be (" + notTop + " are not)");
        Check(ordinaryFail == 0, "school marks: an ordinary student, three classes in four and half the homework, passes every course they may sit (" + ordinaryFail + " fail)");
    }

    // ---------------------------------------------------------------- the forecast is the mark
    private static void SchoolMarkForecast()
    {
        CourseDef law = StudyCatalogue.Find(StudyCatalogue.Law);
        var e = new Enrolment { Course = law.Id, Start = 1, Seen = 6, Done = 5, Homework = 2 };
        int luck = StudyRules.Luck(9, e);
        ExamMark forecast = StudyRules.Mark(e, 33, false, luck, 1);
        ExamMark sat = StudyRules.Mark(e, 33, StudyRules.SatTired(80f), luck, 1);
        ExamMark tired = StudyRules.Mark(e, 33, StudyRules.SatTired(29.9f), luck, 1);
        int sum = forecast.Turning + forecast.Attendance + forecast.Homework + forecast.Skill + forecast.Revision + forecast.Tired + forecast.Tutor + forecast.Luck;
        Check(forecast.Total == sat.Total && sum == forecast.Total && forecast.Grade == StudyRules.Grade(law, forecast.Total),
            "school marks: the forecast's parts add up to its mark, and the exam sat rested gives that same mark and grade (" + forecast.Total + ")");
        Check(tired.Total == forecast.Total - StudyRules.MarkTired && tired.Tired == -StudyRules.MarkTired && forecast.Tired == 0 && !StudyRules.SatTired(30f) && StudyRules.SatTired(29.9f),
            "school marks: sitting tired takes exactly five off, tired being less than thirty energy as the exam begins");
        var moved = new Enrolment { Course = law.Id, Start = 1, Seen = 7, Done = 6, Homework = 3 };
        Check(StudyRules.Mark(moved, 33, false, luck, 1).Total > forecast.Total && StudyRules.Mark(e, 33, false, luck, 1).Total == forecast.Total,
            "school marks: the forecast moves with a class attended or a piece done, and with nothing else: asked again it gives the same mark");
        Check(StudyRules.Mark(e, 100, false, luck, 1).Skill == StudyRules.MarkSkill && StudyRules.ExamScore(e, 33, false, luck) + 1 == forecast.Total,
            "school marks: the skill part reaches the full fifteen at full preparation, and the older ExamScore is the same sum without the tutor");
    }

    // ---------------------------------------------------------------- the tutor's edge
    private static void SchoolMarkEdge()
    {
        bool same = true, bounded = true, varies = false;
        for (int course = 0; course < 60; course++)
        {
            var none = new RosterFile { Seed = 123 };
            var kept = new RosterFile { Seed = 123 };
            kept.Tutors.Add(new Tutor { Course = course, Name = "x", Rating = RosterRules.RatingOf(123, course) });
            int a = RosterRules.ExamEdgeOf(none, course), b = RosterRules.ExamEdgeOf(kept, course), c = RosterRules.ExamEdgeOf(none, course);
            same &= a == b && a == c && a == RosterRules.EdgeOfRating(RosterRules.RatingOf(123, course));
            bounded &= Math.Abs(a) <= RosterRules.EdgeMost;
            varies |= a != RosterRules.ExamEdgeOf(none, 0);
        }
        Check(same && bounded && varies, "school marks: a course's tutor's edge is the same with the tutor made or not (the people pool up or not), always within two, and it differs between courses");
    }

    // ---------------------------------------------------------------- the code that reads it
    private static void SchoolMarkSources()
    {
        string work = ModSource(@"Careers\CareersDirector.Coursework.cs"), marks = ModSource(@"Careers\CareersDirector.Marks.cs"), sitting = ModSource(@"Careers\Sitting.cs"),
            study = ModSource(@"Careers\CareersDirector.Study.cs"), old = ModSource(@"Careers\CampusApp.Studying.cs"), kit = ModSource(@"Ui\Work\CampusLive.cs");
        string examine = MemberText(work, "private void Examine(Enrolment e, int day, float startEnergy = -1f)"), ended = MemberText(work, "private void StudyEnded(");
        Check(examine.Contains("MarkOf(e, StudyRules.SatTired(startEnergy >= 0f ? startEnergy : CareersGame.Energy))") && !examine.Contains("Energy < 30") && !work.Contains("TutorOf(c)"),
            "school marks: the exam is marked by the one sum, tired by the energy it began with, and the tutor's edge from the seed, never from the people pool");
        Check(ended.Contains("Examine(e, day, s.StartEnergy)") && ended.Contains("AfterClass(e, day, true, s.StartEnergy)") && sitting.Contains("StartEnergy = CareersGame.Energy;")
              && sitting.IndexOf("StartEnergy = CareersGame.Energy;", StringComparison.Ordinal) < sitting.IndexOf("CareersGame.Tiring(true);", StringComparison.Ordinal),
            "school marks: a sitting reads the player's energy as it begins, before its own tiring starts, and hands it to the exam and to a short course's last class");
        Check(MemberText(marks, "internal void MarkLines(").Contains("MarkOf(e, false)") && old.Contains("_director.MarkLines(e, _marks)") && kit.Contains("d.MarkLines(e, marks)"),
            "school marks: both interfaces write the forecast from the one MarkOf that marks the exam, rested");
        Check(old.Contains("CareersDirector.HomeworkLines(e, out string hwOpen, out string hwNext)") && kit.Contains("CareersDirector.HomeworkLines(e, out string hwOpen, out string hwNext)")
              && ModSource(@"Careers\CampusApp.cs").Contains("CareersDirector.HomeworkPlan(c)") && kit.Contains("CareersDirector.HomeworkPlan(c)"),
            "school marks: both interfaces say how many pieces are open, when the next is set and after which classes each is set");
        Check(study.Contains("Results(e, day, false, true)") && work.Contains("lapsed ? \"reallife_study_note_lapsed_retake\" : \"reallife_study_note_failed_retake\"")
              && old.Contains("\"reallife_campus_on_retake_unsat\""), "school marks: an exam not sat in its days is told as not sat, and never as a fail with a mark");
        Check(!kit.Contains("StudyRules.HomeworkSet(e).ToString(Inv)"), "school marks: the kit's Homework figure is the pieces done of the course's, no longer of those set so far");
        string guide = ModSource(@"Careers\CareersGuide.cs");
        Check(guide.Contains("\"study_noise\"") && guide.Contains("\"study_exam_tired\"") && guide.Contains("\"study_mark_tutor\""), "school marks: the Guide's figures for the mark are read from the code");
    }

    // ---------------------------------------------------------------- the words
    private static void SchoolMarkTexts()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        if (en == null)
        {
            Check(false, "school marks: the English texts are at " + LocalesFolder());
            return;
        }
        var want = new (string Key, string[] Names)[]
        {
            ("reallife_study_mark_title", new string[0]), ("reallife_study_mark_about", new string[0]), ("reallife_study_mark_forecast", new string[0]),
            ("reallife_study_mark_value", new[] { "{mark}", "{grade}" }), ("reallife_study_mark_tired_value", new[] { "{mark}", "{need}", "{energy}" }),
            ("reallife_study_mark_short", new string[0]), ("reallife_campus_hw_next_at", new[] { "{n}", "{done}" }), ("reallife_campus_hw_all_set", new[] { "{of}" }),
            ("reallife_campus_hw_plan", new[] { "{n}", "{list}" }), ("reallife_campus_hw_plan_one", new[] { "{list}" }), ("reallife_campus_hw_open", new string[0]), ("reallife_campus_hw_next", new string[0]),
            ("reallife_campus_on_retake_unsat", new[] { "{fee}", "{from}", "{to}", "{opens}", "{closes}" }), ("reallife_study_note_lapsed_retake", new[] { "{course}", "{day}" }),
            ("reallife_study_note_lapsed", new[] { "{course}" })
        };
        var missing = new List<string>();
        foreach ((string key, string[] names) in want)
        {
            if (!en.TryGetValue(key, out string text) || string.IsNullOrWhiteSpace(text) || Array.Exists(names, n => text.IndexOf(n, StringComparison.Ordinal) < 0)
                || (key.StartsWith("reallife_study_note_", StringComparison.Ordinal) && text.Length > 240))
            {
                missing.Add(key);
            }
        }
        Check(missing.Count == 0, "school marks: the new texts are in English with their figures, and the notices are within 240 characters (" + string.Join(", ", missing) + ")");
        string guide = en.TryGetValue("reallife_guide_careers_study", out string g) ? g : string.Empty;
        Check(guide.Contains("{study_noise}") && guide.Contains("a mark once sat never changes") && !guide.Contains("Each class sets a piece of homework") && !guide.Contains("up to ten."),
            "school marks: the Guide says how the mark is made, that homework is set as the classes go and that a mark once sat never changes");
        Check(WhatsNewHas(en, "Fixed: the last piece of homework of a short course") && WhatsNewHas(en, "Fixed: an exam counted you tired") && WhatsNewHas(en, "Fixed: a retake you did not sit")
              && WhatsNewHas(en, "Fixed: the tutor's edge on an exam") && WhatsNewHas(en, "Fixed: exam marks swung by ten points"), "school marks: What's new has one line for each fix");
    }
}
