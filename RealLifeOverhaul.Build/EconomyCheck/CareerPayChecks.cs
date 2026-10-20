using System;
using System.Collections.Generic;
using System.Linq;
using RealLifeOverhaul;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Tech;

// Checks for what the professions pay and how fast they are climbed, set against the gigs (Scripts/Careers/Work/
// ProCatalogue, LifeRules, Study/StudyCatalogue and StudyRules.Clash, with Jobs/JobCatalogue). A player found a barista
// paid about 18 an hour while the courier gig at its top level made 1,000 to 1,500 a day, a software degree taking eight
// weeks (most of a year of 60 days) and reviews a month apart; and a class from 10:00 to 14:00 looked as if it clashed
// with a shift at 14:00, though a class counts once three quarters of it is sat. The rules: a first rung pays by what it
// asks, against the gigs' own figures; the top of a degree's ladder is several times its first rung; courses take weeks,
// not months; reviews and time at a rank keep a pace that is never more than twice as slow in a long year; a working
// day teaches enough for the next rung; and a course's timetable is set against the job's working days.
internal static partial class Program
{
    private static void CareerPay()
    {
        CareerPayTiers();
        CareerPayLadders();
        CareerPayCourses();
        CareerPace();
        CareerClimb();
        CareerFit();
    }

    // A game hour of a gig at a level: a shift's piece pay (a courier's round of about 800 m with its distance) over the
    // shift's four game hours; ride-hail by the fare, a 600 m fare being about 80 seconds of driving. Tips not counted.
    private static double GigHour(JobDef def, int level)
    {
        double hour;
        if (def.ShiftUnits > 0)
        {
            hour = (def.UnitPay * def.ShiftUnits + (def.PayPerMetre > 0 ? def.PayPerMetre * 800.0 : 0.0)) / 4.0;
        }
        else
        {
            hour = (def.UnitPay + def.PayPerMetre * 600.0) / (80.0 / 60.0);
        }
        return hour * JobCatalogue.PayFactor(level);
    }

    private static readonly CourseKind[] NoKinds = new CourseKind[0];

    private static CourseKind[] KindsOf(ProRank r) => r.Quals.Select(q => StudyCatalogue.Find(q).Kind).ToArray();

    private static void CareerPayTiers()
    {
        JobDef[] foot = JobCatalogue.All.Where(g => g.Needs == JobNeed.Outdoors && g.Id != JobCatalogue.Busker).ToArray();
        JobDef courier = JobCatalogue.Find(JobCatalogue.Courier);
        double footLow = foot.Min(g => GigHour(g, 1)), footTop = foot.Max(g => GigHour(g, JobCatalogue.MaxLevel));
        double courierTop = GigHour(courier, JobCatalogue.MaxLevel);
        double courierDay = (courier.UnitPay * courier.DailyUnits + courier.PayPerMetre * 800.0 * courier.DailyUnits / courier.ShiftUnits) * JobCatalogue.PayFactor(1);
        Console.WriteLine("  gigs an hour: on foot " + footLow.ToString("0") + " at level 1 to " + footTop.ToString("0") + " at the top; the courier " + courierTop.ToString("0")
                          + " at the top; the courier's capped day at level 1 " + courierDay.ToString("0"));
        bool none = true, course = true, diploma = true, degree = true, day = true;
        var low = new List<string>();
        foreach (Profession p in ProCatalogue.All)
        {
            for (int r = 0; r < p.Ranks.Length; r++)
            {
                ProRank k = p.Ranks[r];
                double hour = k.Pay / 100.0;
                CourseKind[] kinds = r == 0 || k.Quals.Length > 0 ? KindsOf(k) : null;
                if (kinds == null)
                {
                    continue;   // a rung above the first that asks no course of its own
                }
                bool ok = true;
                if (r == 0 && kinds.Length == 0)
                {
                    ok = hour >= footLow;                         // no course at all: at least the gigs on foot
                    none &= ok;
                }
                else if (kinds.Length > 0 && kinds.All(c => c == CourseKind.Degree || c == CourseKind.Postgrad))
                {
                    ok = hour >= courierTop * 1.1 && hour * 8 >= courierDay * 0.9;   // a degree: more than the courier at its top, a day about its capped day
                    degree &= ok;
                    day &= hour * 8 >= courierDay * 0.9;
                }
                else if (kinds.Contains(CourseKind.Vocational) && !kinds.Contains(CourseKind.Short))
                {
                    ok = hour >= courierTop * 0.8;                // a diploma: about the courier's piece pay at its top
                    diploma &= ok;
                }
                else if (kinds.Length > 0)
                {
                    ok = hour > footTop;                          // a short course (or any course): more than every gig on foot at its top
                    course &= ok;
                }
                if (!ok)
                {
                    low.Add(p.Key + " " + r + " (" + hour + ")");
                }
            }
        }
        Check(none, "career pay: a first rung that asks no course pays at least the gigs on foot at level 1 (" + footLow.ToString("0") + ")");
        Check(course, "career pay: a rung after a short course pays more an hour than every gig on foot at its top level (" + footTop.ToString("0") + ")");
        Check(diploma, "career pay: a rung after a diploma pays about the courier's top level (" + (courierTop * 0.8).ToString("0") + " or more)");
        Check(degree, "career pay: a rung after a degree pays a tenth more than the courier at its top level (" + (courierTop * 1.1).ToString("0") + ")");
        Check(day, "career pay: an eight-hour day after a degree comes to about the courier's whole capped day (" + (courierDay * 0.9).ToString("0") + " or more)");
        Check(low.Count == 0, "career pay: no rung below its tier (" + string.Join(", ", low) + ")");
        int barista = ProCatalogue.Find(ProCatalogue.BarStaff).Ranks[0].Pay;
        Check(barista * 8 / 100 >= 500 && barista * 8 / 100 <= 700, "career pay: a barista's eight-hour day is 500 to 700, not the 144 a player found (" + barista * 8 / 100 + ")");
    }

    private static void CareerPayLadders()
    {
        JobDef courier = JobCatalogue.Find(JobCatalogue.Courier);
        double courierTop = GigHour(courier, JobCatalogue.MaxLevel);
        bool steep = true, tops = true, steps = true;
        var flat = new List<string>();
        foreach (Profession p in ProCatalogue.All)
        {
            ProRank first = p.Ranks[0], top = p.Ranks[p.Ranks.Length - 1];
            CourseKind[] kinds = KindsOf(first);
            if (kinds.Length > 0 && kinds.All(c => c == CourseKind.Degree || c == CourseKind.Postgrad))
            {
                bool ok = top.Pay >= first.Pay * 3;
                steep &= ok;
                if (!ok)
                {
                    flat.Add(p.Key);
                }
            }
            tops &= top.Pay / 100.0 > courierTop;
            for (int r = 1; r < p.Ranks.Length; r++)
            {
                steps &= p.Ranks[r].Pay * 100 >= p.Ranks[r - 1].Pay * 105;   // every rung at least a twentieth more than the one below
            }
        }
        Check(steep, "career pay: the top of every ladder that starts with a degree pays three times its first rung or more (" + string.Join(", ", flat) + ")");
        Check(tops, "career pay: the top of every ladder pays more an hour than the courier at its top level");
        Check(steps, "career pay: every rung pays at least a twentieth more than the one below");
        Profession dev = ProCatalogue.Find(ProCatalogue.Developer), boss = ProCatalogue.Find(ProCatalogue.Manager);
        Check(dev.Ranks[0].Pay == 12000 && dev.Ranks[dev.Ranks.Length - 1].Pay == 70000 && boss.Ranks[boss.Ranks.Length - 1].Pay == 95000,
            "career pay: a junior developer 120 an hour, a chief technology officer 700, a chief executive 950");
    }

    private static void CareerPayCourses()
    {
        bool weeks = true, payback = true;
        var slow = new List<string>();
        foreach (CourseDef c in StudyCatalogue.All)
        {
            if (!c.Timetabled)
            {
                continue;
            }
            int days = StudyRules.Length(c, StudyMode.FullTime);
            int most = c.Kind == CourseKind.Short ? 14 : c.Kind == CourseKind.Degree ? 28 : 21;
            bool ok = days <= most && days >= 7 && StudyRules.Length(c, StudyMode.PartTime) >= days;
            weeks &= ok;
            if (!ok)
            {
                slow.Add(c.Key + " " + days);
            }
            if (c.Kind == CourseKind.Postgrad)
            {
                continue;
            }
            // The best first rung it opens: its fee earned back in twelve working days there.
            int best = 0;
            foreach (Profession p in ProCatalogue.All)
            {
                foreach (ProRank r in p.Ranks)
                {
                    if (r.Quals.Contains(c.Id))
                    {
                        best = Math.Max(best, r.Pay);
                    }
                }
            }
            if (best > 0)
            {
                payback &= (long)best * 8 * 12 / 100 >= c.Fee;
            }
        }
        Check(weeks, "courses: a short course two weeks at most, a diploma or a course after a degree three, a degree four (" + string.Join(", ", slow) + ")");
        Check(payback, "courses: every course's fee is earned back in twelve working days of the best rung it opens");
        CourseDef se = StudyCatalogue.Find(StudyCatalogue.SoftwareEngineering);
        Check(StudyRules.Length(se, StudyMode.FullTime) == 21 && se.Sessions == 9, "courses: the software engineering degree is three weeks of classes, not eight");
        var plumber = StudyCatalogue.Find(StudyCatalogue.Plumber);
        int total = 0;
        for (int i = 0; i < plumber.Sessions; i++)
        {
            total += StudyRules.ClassGain(plumber.GainA, plumber.Sessions, 0, 1000);
        }
        Check(Math.Abs(total - plumber.GainA * 1000) <= plumber.Sessions, "courses: fewer classes teach the same in all");
    }

    private static void CareerPace()
    {
        Check(LifeRules.PaceYear(30) == 60 && LifeRules.PaceYear(60) == 60 && LifeRules.PaceYear(90) == 90 && LifeRules.PaceYear(365) == LifeRules.PaceYearMost,
            "pace: the careers keep the game's year from 60 days to " + LifeRules.PaceYearMost);
        bool gates = true, longer = true;
        foreach (Profession p in ProCatalogue.All)
        {
            for (int r = 1; r < p.Ranks.Length; r++)
            {
                int at60 = LifeRules.Paced(p.Ranks[r].Days, 60), at365 = LifeRules.Paced(p.Ranks[r].Days, 365);
                gates &= at60 >= 5 && at60 <= 36;
                longer &= at365 >= at60 && at365 <= at60 * 2 + 1;
            }
        }
        Check(gates, "pace: a rung needs 5 to 35 game days at the one below in the game's own year");
        Check(longer, "pace: in a year of 365 days a rung needs at most twice that");
        // Coming in high needs half the time of rising inside, in game days, at every year's length.
        Profession dev = ProCatalogue.Find(ProCatalogue.Developer);
        bool high = true;
        foreach (int year in new[] { 60, 90, 120, 365 })
        {
            for (int r = 1; r < dev.Ranks.Length - 1; r++)
            {
                int inside = 0;
                for (int k = 1; k <= r; k++)
                {
                    inside += LifeRules.Paced(dev.Ranks[k].Days, year);
                }
                var w = new WorkFile();
                w.History.Add(new Spell { Prof = dev.Id, From = 1, To = 1 + (inside + 1) / 2 + 1 });
                int experience = ProRules.Experience(w, dev.Id, 100, year);
                var w2 = new WorkFile();
                w2.History.Add(new Spell { Prof = dev.Id, From = 1, To = 1 + inside / 2 - 2 });
                high &= experience >= ProRules.YearsDays(dev, r) && (inside / 2 - 2 <= 0 || ProRules.Experience(w2, dev.Id, 100, year) < ProRules.YearsDays(dev, r));
            }
        }
        Check(high, "pace: coming in at a rung needs half the game days of rising to it inside, in a year of 60, 90, 120 and 365 days");
    }

    // A junior developer straight from a computer science degree, working every working day for eight hours with nothing
    // else studied: the reviews every two weeks of the game's year, the skills learnt at work, the time at each rank.
    private static void CareerClimb()
    {
        var f = new StudyFile();
        f.Results.Add(new Enrolment { Course = StudyCatalogue.ComputerScience, Stage = (int)EnrolStage.Passed, Grade = 3 });
        CourseDef cs = StudyCatalogue.Find(StudyCatalogue.ComputerScience);
        StudyRules.Learn(f, cs.SkillA, cs.GainA * 1000, 1);
        StudyRules.Learn(f, cs.SkillB, cs.GainB * 1000, 1);
        Profession dev = ProCatalogue.Find(ProCatalogue.Developer);
        var j = new Job { Hired = 1, RankDay = 1, ReviewDay = 1 };
        new Opening { Prof = dev.Id, Rank = 0, Kind = (int)EmployerKind.City, Employer = "Codeharbour Software" }.CopyTo(j.Post);
        j.Pay = dev.Ranks[0].Pay;
        const int year = 60;
        var reached = new List<int>();
        for (int day = 1; day <= 200; day++)
        {
            if ((day - 1) % 7 < 5)
            {
                dev.SkillsOf(j.Post.Rank, out int a, out int b);
                foreach (int s in new[] { a, b })
                {
                    StudyRules.Learn(f, s, StudyCatalogue.WorkLearn * 8 * (100 - StudyRules.Points(f, s)) / 100, day);
                }
            }
            if (day >= j.ReviewDay + LifeRules.ReviewEvery(year))
            {
                j.ReviewDay = day;
                if (LifeRules.CanPromote(f, j, 4, day, year))
                {
                    j.Post.Rank++;
                    j.RankDay = day;
                    j.Pay = LifeRules.PromotedPay(j, j.Post.Rank);
                    reached.Add(day);
                }
            }
        }
        Console.WriteLine("  a junior developer with a 4 at every review, from a degree only: promoted on days " + string.Join(", ", reached)
                          + " (a review every " + LifeRules.ReviewEvery(year) + " days)");
        Check(reached.Count >= 3 && reached[0] <= 30 && reached[2] <= 100, "climb: from a degree alone, three promotions within 100 game days of steady work (" + string.Join(", ", reached) + ")");
        Check(reached.Count < dev.Ranks.Length - 1 || reached[reached.Count - 1] >= 100, "climb: the top of the ladder is not reached in a few weeks");
        // A working day teaches about a point at 20 points, half one at 60.
        int at20 = StudyCatalogue.WorkLearn * 8 * 80 / 100, at60 = StudyCatalogue.WorkLearn * 8 * 40 / 100;
        Check(at20 >= 800 && at20 <= 1200 && at60 * 2 == at20, "climb: a working day teaches about a point at 20 points and half one at 60 (" + at20 + ", " + at60 + " thousandths)");
    }

    // A course's timetable against the job's working days. The player's case: a degree on Tuesday, Thursday and Friday
    // from 10:00 to 14:00 and the bar from 14:00, Wednesday to Sunday.
    private static void CareerFit()
    {
        Profession bar = ProCatalogue.Find(ProCatalogue.BarStaff), dev = ProCatalogue.Find(ProCatalogue.Developer);
        CourseDef se = StudyCatalogue.Find(StudyCatalogue.SoftwareEngineering), barista = StudyCatalogue.Find(StudyCatalogue.Barista);
        WorkFit fit = StudyRules.Fit(se, StudyMode.FullTime, bar, out int days, out int leave);
        Check(fit == WorkFit.LeaveClass && days == (StudyCatalogue.Thu | StudyCatalogue.Fri) && leave == 13 * 60,
            "fit: a 10:00 class of four hours and a shift at 14:00 fit, leaving class at 13:00 on Thursday and Friday (" + fit + ", " + days + ", " + leave + ")");
        Check(StudyRules.Fit(barista, StudyMode.FullTime, bar, out days, out _) == WorkFit.Fits && days == 0, "fit: a three-hour class from 10:00 and a shift at 14:00 fit whole");
        Check(StudyRules.Fit(se, StudyMode.PartTime, bar, out days, out _) == WorkFit.Clash && days == StudyCatalogue.Thu, "fit: evening classes clash with the bar's evenings, on Thursday");
        Check(StudyRules.Fit(se, StudyMode.FullTime, dev, out days, out _) == WorkFit.Clash && days == (StudyCatalogue.Tue | StudyCatalogue.Thu | StudyCatalogue.Fri),
            "fit: a full-time degree clashes with an office's working days");
        Check(StudyRules.Fit(se, StudyMode.PartTime, dev, out days, out _) == WorkFit.Fits && days == 0, "fit: evening classes fit an office's day");
        var late = new CourseDef(90, "check_late", CourseKind.Vocational, Campus.School, 4, 0, 3, StudyCatalogue.Mon, 15, 100, Skills.Care, 10, Skills.Clinical, 5);
        Profession nurse = ProCatalogue.Find(ProCatalogue.Nurse);
        Check(StudyRules.Fit(late, StudyMode.FullTime, nurse, out days, out leave) == WorkFit.LeaveWork && days == StudyCatalogue.Mon && leave == 7 * 60 + 450,
            "fit: a ward's day from 07:00 left at 14:30, once it counts, reaches a class at 15:00 in time");
        CourseDef cert = StudyCatalogue.Find(StudyCatalogue.CloudAssociate);
        Check(StudyRules.Fit(cert, StudyMode.FullTime, dev, out _, out _) == WorkFit.Fits && StudyRules.Fit(se, StudyMode.FullTime, null, out _, out _) == WorkFit.Fits,
            "fit: a certificate, or no job, always fits");
        var f = new StudyFile();
        f.Courses.Add(new Enrolment { Course = barista.Id, Stage = (int)EnrolStage.Studying, Mode = (int)StudyMode.FullTime });
        f.Courses.Add(new Enrolment { Course = se.Id, Stage = (int)EnrolStage.Studying, Mode = (int)StudyMode.FullTime });
        f.Courses.Add(new Enrolment { Course = StudyCatalogue.Law, Stage = (int)EnrolStage.Exam, Mode = (int)StudyMode.FullTime });
        Check(StudyRules.Fit(f, bar, out days, out leave, out int course) == WorkFit.LeaveClass && course == se.Id && leave == 13 * 60,
            "fit: of the courses being followed the one that matters most is said, and one past its classes is not counted");
        Check(StudyRules.CountsFrom(600, 240) == 780 && StudyRules.CountsFrom(600, 181) == 600 + 136, "fit: a class counts once three quarters of it is sat, the minute rounded up");
    }
}
