using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Tech;

// Checks for education (Scripts/Careers/Study): the courses' table, the timetable and the window a class can be joined
// in, attendance and the bar, homework and revision, the exam's mark and grades, retakes, fees, grants and instalments,
// the student loan, skills learnt and fading, the week's study, and the save's line.
internal static partial class Program
{
    private static void Careers()
    {
        StudyTable();
        StudyTimetable();
        StudyAttendance();
        StudyExams();
        StudyFees();
        StudyLoan();
        StudySkills();
        StudySave();
    }

    private static void StudyTable()
    {
        var ids = new HashSet<int>();
        var keys = new HashSet<string>();
        bool unique = true, skills = true, gains = true, times = true, needs = true, certs = true, schools = true, places = true;
        foreach (CourseDef c in StudyCatalogue.All)
        {
            unique &= ids.Add(c.Id) && keys.Add(c.Key) && c.Id >= 0 && c.Id < StudyCatalogue.IdsMost && StudyCatalogue.Find(c.Id) == c;
            skills &= c.SkillA >= 0 && c.SkillA < Skills.Count && c.SkillB >= 0 && c.SkillB < Skills.Count && c.SkillA != c.SkillB;
            gains &= c.GainA > 0 && c.GainB > 0 && c.GainA >= c.GainB && c.GainA <= 50 && c.Fee > 0;
            if (c.Timetabled)
            {
                times &= c.Days != 0 && c.Hours > 0 && c.Hour >= 7 && c.Hour + c.Hours <= 22 && c.Sessions > 0;
            }
            else
            {
                times &= c.Kind == CourseKind.Certificate && c.Homework > 0;
            }
            foreach (int n in c.Needs)
            {
                needs &= StudyCatalogue.Find(n) != null && n != c.Id;
            }
            if (c.Kind == CourseKind.Certificate)
            {
                certs &= c.Cert >= 0 && c.Cert < TalentCatalogue.CertCount && StudyCatalogue.OfCert(c.Cert) == c;
            }
            else
            {
                certs &= c.Cert < 0;
            }
            if (c.Kind == CourseKind.Vocational || c.Higher)
            {
                schools &= c.School >= TalentCatalogue.Diploma;
            }
            places &= c.Kind == CourseKind.Short || c.Kind == CourseKind.Vocational ? c.Campus == Campus.School || c.Campus == Campus.Online : true;
        }
        Check(unique, "study: every course has its own id (under 64) and key, and Find returns it");
        Check(skills, "study: every course teaches two different skills of the player's");
        Check(gains, "study: every course adds to both skills, the first the most, and has a fee");
        Check(times, "study: every timetabled course has days, an hour between 07:00 and 22:00 and classes; the rest are certificates studied at home");
        Check(needs, "study: what a course needs is another course of the table");
        Check(certs, "study: each certificate is one of the professions' twelve, and only they are");
        Check(schools, "study: a diploma or a degree counts as schooling for an employer");
        Check(places, "study: short courses and diplomas are at the school or online");
        int certificates = 0;
        for (int i = 0; i < TalentCatalogue.CertCount; i++)
        {
            certificates += StudyCatalogue.OfCert(i) != null ? 1 : 0;
        }
        Check(certificates == TalentCatalogue.CertCount, "study: all twelve of the professions' certificates can be studied for (" + certificates + ")");
        Check(Skills.Count == TalentCatalogue.SkillCount + Skills.OwnKeys.Length, "study: the player's skills are the professions' and the fourteen of their own");
        Check(StudyCatalogue.SkillKey(TalentCatalogue.Coding) == "reallife_talent_skill_coding" && StudyCatalogue.SkillKey(Skills.Driving) == "reallife_study_skill_driving",
            "study: a skill's name is the professions' own word, or one of the study's");
        CourseDef law = StudyCatalogue.Find(StudyCatalogue.LegalPractice), mba = StudyCatalogue.Find(StudyCatalogue.Mba);
        var f = new StudyFile();
        Check(!StudyRules.Qualified(f, law) && !StudyRules.Qualified(f, mba), "study: the legal practice course and the MBA need a degree first");
        f.Results.Add(new Enrolment { Course = StudyCatalogue.Law, Stage = (int)EnrolStage.Passed, Grade = 2 });
        Check(StudyRules.Qualified(f, law) && StudyRules.Qualified(f, mba), "study: a law degree opens both");
        Check(StudyRules.School(f) == TalentCatalogue.Bachelor && StudyRules.Passed(f) == 1L << StudyCatalogue.Law, "study: a degree passed counts as a bachelor's and is in the mask of passes");
    }

    private static void StudyTimetable()
    {
        CourseDef electrician = StudyCatalogue.Find(StudyCatalogue.Electrician);
        // Day 1 is a Monday: the electrician's classes are Monday, Wednesday, Friday at 09:00.
        Check(StudyRules.On(electrician.Days, 1) && !StudyRules.On(electrician.Days, 2) && StudyRules.On(electrician.Days, 3) && StudyRules.On(electrician.Days, 8),
            "study: the timetable's days follow the game's week (day 1 a Monday)");
        Check(StudyRules.Days(electrician, StudyMode.PartTime) == StudyCatalogue.PartTimeDaysSchool && StudyRules.Hour(electrician, StudyMode.PartTime) == StudyCatalogue.PartTimeHourSchool,
            "study: part time at the school is Saturday mornings");
        Check(StudyRules.Length(electrician, StudyMode.PartTime) > StudyRules.Length(electrician, StudyMode.FullTime), "study: part time takes longer");
        int left;
        Check(!StudyRules.Joinable(electrician, StudyMode.FullTime, 1, 9 * 60 - StudyCatalogue.ArriveEarlyMinutes - 1, out left), "study: a class cannot be joined too early");
        Check(StudyRules.Joinable(electrician, StudyMode.FullTime, 1, 9 * 60 - StudyCatalogue.ArriveEarlyMinutes, out left) && left == electrician.Hours * 60,
            "study: early, the whole class is still to run");
        Check(StudyRules.Joinable(electrician, StudyMode.FullTime, 1, 9 * 60 + 30, out left) && left == electrician.Hours * 60 - 30, "study: late, only what is left of it");
        Check(!StudyRules.Joinable(electrician, StudyMode.FullTime, 1, 9 * 60 + StudyCatalogue.LateMinutes + 1, out left), "study: too late, it cannot be joined");
        Check(!StudyRules.Joinable(electrician, StudyMode.FullTime, 2, 9 * 60, out left), "study: no class on a day off the timetable");
        Check(StudyRules.Gone(electrician, StudyMode.FullTime, 1, 1, 9 * 60 + StudyCatalogue.LateMinutes + 1) && !StudyRules.Gone(electrician, StudyMode.FullTime, 1, 1, 9 * 60 + 30)
              && StudyRules.Gone(electrician, StudyMode.FullTime, 1, 2, 0), "study: a class is gone once it can no longer be joined");
        Check(StudyRules.Next(electrician, StudyMode.FullTime, 2) == 3 && StudyRules.Next(electrician, StudyMode.FullTime, 6) == 8, "study: the next class is the next day of the timetable");
        CourseDef cert = StudyCatalogue.Find(StudyCatalogue.CloudAssociate);
        Check(StudyRules.Days(cert, StudyMode.FullTime) == 0 && StudyRules.Next(cert, StudyMode.FullTime, 1) == -1 && !StudyRules.Joinable(cert, StudyMode.FullTime, 1, 600, out left),
            "study: a certificate has no classes");
    }

    private static void StudyAttendance()
    {
        CourseDef aid = StudyCatalogue.Find(StudyCatalogue.FirstAid);
        var e = new Enrolment { Course = aid.Id, Start = 1 };
        Check(StudyRules.Attend(e, 3) && !StudyRules.Attend(e, 3) && !StudyRules.Miss(e, 3), "study: a day's class is counted once, attended or missed");
        Check(e.Done == 1 && e.Seen == 1, "study: an attended class is seen and done");
        Check(StudyRules.Attend(e, 6) && StudyRules.Advance(e, 6) && e.Stage == (int)EnrolStage.Exam, "study: after the last class the exam is due");
        Check(!StudyRules.Attend(e, 10), "study: no class is counted once they are over");
        CourseDef plumber = StudyCatalogue.Find(StudyCatalogue.Plumber);
        var p = new Enrolment { Course = plumber.Id, Start = 1 };
        for (int d = 1; p.Seen < plumber.Sessions; d++)
        {
            if (d % 3 == 0)
            {
                StudyRules.Attend(p, d);
            }
            else
            {
                StudyRules.Miss(p, d);
            }
        }
        Check(StudyRules.Attendance(p) < StudyCatalogue.Barred && StudyRules.Advance(p, 30) && p.Stage == (int)EnrolStage.Barred, "study: a student who missed too many is barred from the exam");
        var h = new Enrolment { Course = plumber.Id, Start = 1 };
        Check(StudyRules.HomeworkSet(h) == 0 && !StudyRules.HomeworkOpen(h), "study: no homework before the first class");
        for (int d = 1; d <= plumber.Sessions; d++)
        {
            StudyRules.Attend(h, d);
        }
        Check(StudyRules.HomeworkSet(h) == plumber.Homework && StudyRules.HomeworkOpen(h), "study: every class attended, all the homework is set");
        h.Homework = plumber.Homework;
        Check(!StudyRules.HomeworkOpen(h), "study: with the classes still going, nothing beyond the set homework");
        StudyRules.Advance(h, 20);
        Check(StudyRules.HomeworkOpen(h), "study: once the classes are over, revision can be done");
        h.Homework = plumber.Homework + StudyCatalogue.RevisionMost;
        Check(!StudyRules.HomeworkOpen(h), "study: revision counts only so far");
        CourseDef cert = StudyCatalogue.Find(StudyCatalogue.DataAnalyst);
        var c = new Enrolment { Course = cert.Id, Start = 1 };
        Check(StudyRules.HomeworkSet(c) == cert.Homework && !StudyRules.Advance(c, 2), "study: a certificate's study is all set at once and is not over until done");
        c.Homework = cert.Homework;
        Check(StudyRules.Advance(c, 4) && c.Stage == (int)EnrolStage.Exam && c.ExamTo == 4 + StudyCatalogue.ExamWindowDays * 2, "study: a certificate's exam opens when its study is done");
    }

    private static void StudyExams()
    {
        CourseDef plumber = StudyCatalogue.Find(StudyCatalogue.Plumber);
        var good = new Enrolment { Course = plumber.Id, Done = plumber.Sessions, Seen = plumber.Sessions, Homework = plumber.Homework + 2 };
        var poor = new Enrolment { Course = plumber.Id, Done = plumber.Sessions / 2, Seen = plumber.Sessions, Homework = 0 };
        int high = StudyRules.ExamScore(good, 50, false, 0), low = StudyRules.ExamScore(poor, 5, false, 0);
        Check(high > low && high >= StudyCatalogue.Distinction && low < StudyCatalogue.Merit, "study: attendance, homework and skill raise the mark (" + high + " against " + low + ")");
        Check(StudyRules.ExamScore(poor, 5, true, 0) == low - 5, "study: sitting it tired costs five");
        Check(StudyRules.ExamScore(good, 50, false, 999) - high <= StudyCatalogue.ExamNoise, "study: the day's luck is bounded");
        bool luck = true;
        for (int i = 0; i < 200; i++)
        {
            int l = StudyRules.Luck(1234, new Enrolment { Course = i % 40, Start = i });
            luck &= l >= -StudyCatalogue.ExamNoise && l <= StudyCatalogue.ExamNoise;
        }
        Check(luck, "study: the luck stays within ten either way");
        Check(StudyRules.Grade(plumber, 49) == 0 && StudyRules.Grade(plumber, 50) == 1 && StudyRules.Grade(plumber, 65) == 2 && StudyRules.Grade(plumber, 80) == 3,
            "study: a course's grades: fail, pass, merit, distinction");
        CourseDef law = StudyCatalogue.Find(StudyCatalogue.Law);
        Check(StudyRules.Grade(law, 39) == 0 && StudyRules.Grade(law, 40) == 1 && StudyRules.Grade(law, 50) == 2 && StudyRules.Grade(law, 60) == 3 && StudyRules.Grade(law, 70) == 4,
            "study: a degree's classes: fail, third, lower second, upper second, first");
        Check(StudyRules.Honours(law, 4) && StudyRules.Honours(law, 3) && !StudyRules.Honours(law, 2) && StudyRules.TopGrade(law) == 4 && StudyRules.TopGrade(plumber) == 3,
            "study: honours are a first or an upper second (a distinction for a course)");
        var e = new Enrolment { Course = plumber.Id, Stage = (int)EnrolStage.Exam, ExamFrom = 10, ExamTo = 17 };
        Check(!StudyRules.ExamDay(e, 9) && StudyRules.ExamDay(e, 10) && !StudyRules.ExamDay(e, 13), "study: an exam is sat on a weekday within its window (day 13 is a Saturday)");
        Check(!StudyRules.Sit(e, 20, 11) && e.Stage == (int)EnrolStage.Retake && e.Retakes == 1 && e.ExamFrom == 11 + StudyCatalogue.RetakeDays, "study: a fail leaves a retake later");
        Check(!StudyRules.Sit(e, 20, 20) && e.Retakes == 2 && e.Stage == (int)EnrolStage.Retake, "study: a second fail, a second retake");
        Check(!StudyRules.Sit(e, 20, 30) && e.Stage == (int)EnrolStage.Failed, "study: a third fail is the end of it");
        var lapse = new Enrolment { Course = plumber.Id, Stage = (int)EnrolStage.Exam, ExamFrom = 10, ExamTo = 17 };
        Check(!StudyRules.ExamMissed(lapse, 17) && StudyRules.ExamMissed(lapse, 18) && lapse.Stage == (int)EnrolStage.Retake, "study: an exam not sat in its days counts as a fail");
        var pass = new Enrolment { Course = plumber.Id, Stage = (int)EnrolStage.Exam };
        Check(StudyRules.Sit(pass, 66, 12) && pass.Stage == (int)EnrolStage.Passed && pass.Grade == 2 && pass.Ended == 12, "study: a pass keeps its grade and day");
        Check(StudyRules.CeremonyDay(1) == 6 && StudyRules.CeremonyDay(5) == 13 && StudyRules.CeremonyDay(6) == 13 && (StudyRules.CeremonyDay(30) - 1) % 7 == 5 && StudyRules.CeremonyDay(30) >= 32,
            "study: graduation is the first Saturday at least two days after the result");
        Check(StudyRules.RetakeFee(plumber) == Math.Max(StudyCatalogue.RetakeFeeLeast, plumber.Fee * StudyCatalogue.RetakeFeeShare / 1000)
              && StudyRules.RetakeFee(StudyCatalogue.Find(StudyCatalogue.DrivingRefresher)) == StudyCatalogue.RetakeFeeLeast, "study: a retake costs a share of the fee, never under the least");
    }

    private static void StudyFees()
    {
        CourseDef law = StudyCatalogue.Find(StudyCatalogue.Law), aid = StudyCatalogue.Find(StudyCatalogue.FirstAid);
        var f = new StudyFile();
        Check(StudyRules.Grant(f, law, 100000, 1000, 0) == 0, "study: no grant for a student with money and no result");
        Check(StudyRules.Grant(f, law, 100, 1000, 0) == StudyCatalogue.NeedGrantShare, "study: a bursary for a student with little money");
        f.Results.Add(new Enrolment { Course = StudyCatalogue.Plumber, Stage = (int)EnrolStage.Passed, Grade = 3 });
        Check(StudyRules.Grant(f, law, 100000, 1000, 0) == StudyCatalogue.MeritGrantShare, "study: a merit grant after a distinction");
        Check(StudyRules.Grant(f, law, 100, 1000, 1) == Math.Min(StudyCatalogue.GrantMost, StudyCatalogue.MeritGrantShare + StudyCatalogue.NeedGrantShare + StudyCatalogue.CouncilShare),
            "study: grants add up to the most there can be");
        Check(StudyRules.Grant(new StudyFile(), law, 100000, 1000, -1) == -StudyCatalogue.CouncilShare && StudyRules.Fee(law, -StudyCatalogue.CouncilShare) > law.Fee,
            "study: the council's surcharge raises the fee");
        Check(StudyRules.Grant(f, aid, 100, 1000, 0) == 0, "study: a short course has no grants but the council's");
        Check(StudyRules.Instalments(law) == 3 && StudyRules.Instalments(aid) == 1, "study: a degree is paid in thirds, a short course at once");
        var e = new Enrolment { Course = law.Id };
        int paid = 0, first = StudyRules.InstalmentDue(e);
        Check(first > 0, "study: the first instalment is due at once");
        paid += first;
        e.Instalments = 1;
        Check(StudyRules.InstalmentDue(e) == 0, "study: the second waits for a third of the classes");
        e.Seen = law.Sessions / 3;
        paid += StudyRules.InstalmentDue(e);
        e.Instalments = 2;
        e.Seen = law.Sessions * 2 / 3;
        paid += StudyRules.InstalmentDue(e);
        e.Instalments = 3;
        Check(paid == StudyRules.Fee(law, 0) && StudyRules.InstalmentDue(e) == 0, "study: the three instalments come to the fee, and nothing after");
    }

    private static void StudyLoan()
    {
        Check(StudyRules.LoanInterest(1000000, 60) > 0 && StudyRules.LoanInterest(0, 60) == 0, "study: the loan's interest runs on what is owed");
        Check(StudyRules.LoanRepayment(500000, StudyCatalogue.LoanThreshold, 8, 1000) == 0, "study: nothing is taken from pay at the threshold");
        long take = StudyRules.LoanRepayment(500000, StudyCatalogue.LoanThreshold + 1000, 8, 1000);
        Check(take == 8000L * StudyCatalogue.LoanShareBp / 10000, "study: the loan takes its share of what is paid over the threshold");
        Check(StudyRules.LoanRepayment(100, StudyCatalogue.LoanThreshold + 100000, 8, 1000) == 100, "study: never more than is owed");
        var f = new StudyFile();
        Check(StudyRules.LoanRoom(f, StudyCatalogue.LoanMost, 1000) && !StudyRules.LoanRoom(f, StudyCatalogue.LoanMost + 1, 1000), "study: the loan lends up to its most");
    }

    private static void StudySkills()
    {
        var f = new StudyFile();
        StudyRules.Learn(f, TalentCatalogue.Coding, 150000, 5);
        Check(f.Skill[TalentCatalogue.Coding] == 100000 && f.Best[TalentCatalogue.Coding] == 100000 && f.Used[TalentCatalogue.Coding] == 5, "study: a skill stops at 100 points");
        int low = StudyRules.ClassGain(30, 10, 0, 1000), top = StudyRules.ClassGain(30, 10, 100, 1000);
        Check(low == 3000 && top == 3000 * StudyCatalogue.GainAtTop / 1000, "study: a class teaches less the nearer the top");
        Check(StudyRules.ClassGain(30, 10, 0, TalentCatalogue.SchoolLearning[TalentCatalogue.Master]) > low, "study: a better schooling learns faster");
        CourseDef plumber = StudyCatalogue.Find(StudyCatalogue.Plumber);
        int total = 0;
        for (int i = 0; i < plumber.Sessions; i++)
        {
            total += StudyRules.ClassGain(plumber.GainA, plumber.Sessions, 0, 1000);
        }
        Check(Math.Abs(total - plumber.GainA * 1000) <= plumber.Sessions, "study: every class of a course adds up to its gain at the start");
        f = new StudyFile();
        StudyRules.Learn(f, TalentCatalogue.Law, 60000, 1);
        StudyRules.Fade(f, 1 + StudyCatalogue.FadeAfterDays);
        Check(f.Skill[TalentCatalogue.Law] == 60000, "study: a skill does not fade before its days");
        for (int d = 2 + StudyCatalogue.FadeAfterDays; d < 3000; d++)
        {
            StudyRules.Fade(f, d);
        }
        Check(f.Skill[TalentCatalogue.Law] == 60000 * StudyCatalogue.FadeFloor / 1000, "study: a skill fades to its floor and no further (" + f.Skill[TalentCatalogue.Law] + ")");
        StudyRules.Learn(f, Skills.Hair, 15000, 1);
        StudyRules.Fade(f, 400);
        Check(f.Skill[Skills.Hair] == 15000, "study: nothing under twenty points fades");
        Check(!StudyRules.Studied(f, 1, 60 * 20) && StudyRules.Studied(f, 2, 60 * 11) && !StudyRules.Studied(f, 3, 60 * 5), "study: the stressed mark is said once a week");
        Check(!StudyRules.Studied(f, 8, 60 * 29) && StudyRules.Studied(f, 9, 60 * 2), "study: a new week starts the count again");
    }

    private static void StudySave()
    {
        var f = new StudyFile { Seed = 77, FirstDay = 3, LastDay = 40, LoanCents = 123456, LoanTaken = 200000, LoanRepaid = 76544, WeekOf = 5, WeekMinutes = 600, GraduationDay = 44, GraduationCourse = 27 };
        StudyRules.Learn(f, TalentCatalogue.Law, 41234, 20);
        StudyRules.Learn(f, Skills.Care, 9000, 30);
        f.Courses.Add(new Enrolment { Course = StudyCatalogue.Plumber, Mode = 1, Start = 10, Seen = 5, Done = 4, SettledDay = 22, Homework = 2, Fee = 3000, Instalments = 1, Grant = 200, FeeLate = true });
        f.Results.Add(new Enrolment { Course = StudyCatalogue.Law, Stage = (int)EnrolStage.Passed, Score = 72, Grade = 4, Ended = 39, Graduated = true, Loan = 9000, OnLoan = true });
        f.Results.Add(new Enrolment { Course = StudyCatalogue.FirstAid, Stage = (int)EnrolStage.Withdrawn, Ended = 12 });
        string line = CareersStore.Write(f);
        int version;
        StudyFile back = CareersStore.Read(line, out version);
        Check(version == CareersStore.Version && CareersStore.Write(back) == line, "study: the save's line reads back the same");
        Check(back.Skill[TalentCatalogue.Law] == 41234 && back.Used[Skills.Care] == 30 && back.LoanCents == 123456 && back.GraduationCourse == 27, "study: skills, the loan and the graduation come back");
        Enrolment c = back.Courses.Count == 1 ? back.Courses[0] : null;
        Check(c != null && c.Mode == 1 && c.Seen == 5 && c.Done == 4 && c.SettledDay == 22 && c.Grant == 200 && c.FeeLate, "study: a course under way comes back as it was");
        Check(back.Results.Count == 2 && back.Holds(StudyCatalogue.Law) && back.GradeOf(StudyCatalogue.Law) == 4 && !back.Holds(StudyCatalogue.FirstAid), "study: the results come back, passes as qualifications");
        Check(CareersStore.Read("v=1~h:5", out version).Courses.Count == 0, "study: a short line reads as nothing under way");
        var data = new Dictionary<string, string> { { CareersStore.Key, "not a line" } };
        Check(CareersStore.Load(data, out version) == null, "study: a damaged line is not read");
        var many = new StudyFile();
        for (int i = 0; i < StudyFile.ResultsMost + 10; i++)
        {
            var e = new Enrolment { Course = StudyCatalogue.FirstAid, Stage = i == 0 ? (int)EnrolStage.Passed : (int)EnrolStage.Withdrawn };
            many.Courses.Add(e);
            many.Finish(e);
        }
        Check(many.Results.Count == StudyFile.ResultsMost && many.Holds(StudyCatalogue.FirstAid), "study: results are capped, and the oldest that are not passes go first");
    }
}
