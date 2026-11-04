using RealLifeOverhaul.Ui;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Hustles;
using RealLifeOverhaul.Tech;

// The work interface's new rules (wave 4): standing at work, a colleague up for the same rung, other applicants, a campus's seats and
// classmates, its tutors, floors, credits and averages, a rival's offer to an applicant, an ad's posts, a CV's past jobs and the
// Marketplace's sellers. Each rule is checked for its numbers, its bounds, its save round trip and an older save.
internal static partial class Program
{
    private static void WorkUiRules()
    {
        WkStanding();
        WkColleague();
        WkApplicants();
        WkRoster();
        WkTutorAndFloor();
        WkGrades();
        WkOffers();
        WkPosts();
        WkSellers();
        WkFilters();
        WkPool();
        WkBestPaid();
        WkTeamworkAndChart();
        WkSaves();
        WkRuns();
    }

    private static Job WkJob(int due, int worked, int late, int favour)
    {
        var j = new Job { Due = due, Worked = worked, Late = late, Favour = favour, Hired = 10 };
        j.Post.Street = "Broadway";
        j.Post.Number = 7;
        j.Post.Employer = "Harbour Freight";
        return j;
    }

    private static void WkStanding()
    {
        Standing s = StandingRules.Of(WkJob(10, 10, 1, 0), 82);
        Check(s.Performance == 82 && s.Punctuality == 90 && s.Reliability == 100 && s.Teamwork == 50 && s.Measured,
            "work standing: ten shifts due, ten worked, one late is 90 on time, 100 reliable, teamwork in the middle with no standing among colleagues");
        s = StandingRules.Of(WkJob(10, 7, 0, 15), 60);
        Check(s.Reliability == 70 && s.Punctuality == 100 && s.Teamwork == 100,
            "work standing: three shifts missed is 70 reliable; the best standing with colleagues is 100 teamwork");
        s = StandingRules.Of(WkJob(0, 0, 0, -15), 50);
        Check(!s.Measured && s.Teamwork == 0 && s.Reliability == 100,
            "work standing: before any shift is due the figures say nothing yet, and the worst standing is 0 teamwork");
        s = StandingRules.Of(WkJob(5, 2, 9, 0), 140);
        Check(s.Punctuality >= 0 && s.Performance == 100, "work standing: more late than worked cannot go under 0, and a score over 100 is held at 100");
        var f = new StandingFile { Seed = 11 };
        int weeks = 0;
        for (int day = 0; day < 7 * 30; day += 7)
        {
            weeks += StandingRules.Week(f, 50 + day % 40, day);
        }
        Check(weeks == 30 && f.Count == Peer.RingSize && f.Ring[Peer.RingSize - 1] == 50 + (7 * 29) % 40,
            "work standing: the weekly ring keeps the last twelve weeks of the player's score, the newest at the end");
        Check(StandingRules.Week(f, 99, 7 * 29 + 3) == 0, "work standing: the ring is written once a week, not twice");
    }

    private static void WkColleague()
    {
        var a = StandingRules.Colleague("Marcus Lee", 2, 72, PeerRole.UpForPost, 100, 7, 6);
        var b = StandingRules.Colleague("Marcus Lee", 2, 72, PeerRole.UpForPost, 100, 7, 6);
        Check(a.Count == 6 && a.Score == b.Score && a.Mean() == b.Mean() && a.Teamwork == b.Teamwork,
            "work colleague: the same seed gives the same colleague with the same six weeks");
        bool inBounds = true;
        for (int w = 0; w < 200; w++)
        {
            StandingRules.Step(a, 7, w, 0);
            inBounds &= a.Score >= 0 && a.Score <= 100 && a.Punctuality >= 0 && a.Punctuality <= 100 && a.Reliability <= 100 && a.Teamwork <= 100
                && a.Count <= Peer.RingSize;
        }
        Check(inBounds, "work colleague: a colleague's score and standing stay between 0 and 100 over 200 weeks, the ring never over twelve");
        var f = new StandingFile { Seed = 3 };
        for (int w = 0; w < 8; w++)
        {
            f.Push(80);
        }
        var weak = StandingRules.Colleague("Pilar Duran", 2, 60, PeerRole.UpForPost, 1, 3, 8);
        var strong = StandingRules.Colleague("Oleg Kozlov", 2, 92, PeerRole.UpForPost, 1, 3, 8);
        Check(StandingRules.PlayerGetsPost(f, weak) && !StandingRules.PlayerGetsPost(f, strong),
            "work colleague: the better record gets the rung: 80 over eight weeks beats a colleague of about 60 and loses to one of about 92");
        var thin = new StandingFile();
        thin.Push(10);
        Check(StandingRules.PlayerGetsPost(thin, strong) && StandingRules.PlayerGetsPost(f, null),
            "work colleague: with fewer than four weeks held, or no rival, the player is not passed over");
        f.Peers.Add(weak);
        Check(f.UpFor() == weak, "work colleague: the colleague up for the same rung is found by role");
    }

    private static void WkApplicants()
    {
        var o = new Opening { Id = 41, Prof = 1, Rank = 2, Employer = "Hudson Depot" };
        var f = new StandingFile { Seed = 9 };
        int n = StandingRules.OthersFor(9, o);
        Check(n >= 0 && n <= StandingRules.OthersPerPostMost && n == StandingRules.OthersFor(9, o),
            "work applicants: no more than three others apply for one post, the same number each time it is asked");
        var list = new List<Peer>();
        for (int i = 0; i < 3; i++)
        {
            f.Others.Add(StandingRules.Applicant("Nia Brooks " + i, o, 60, 400, 9, i));
        }
        Check(StandingRules.OthersOf(f, 41, list) == 3 && list[0].Post == 41 && list[0].Role == (int)PeerRole.Applicant && list[0].Day <= 400,
            "work applicants: others an older save kept are found by the post they applied for, with their day");
        // F3: the others of a post are made from the post, so every post of the board shows the same people however often it is read
        var drawn = new List<Peer>();
        var again = new List<Peer>();
        bool stable = true, namesOk = true, daysOk = true;
        int withOthers = 0;
        for (int id = 1; id <= 40; id++)
        {
            var post = new Opening { Id = id, Prof = 1, Rank = 1, From = 380 + id % 7 };
            StandingRules.OthersDerive(9, post, 60, drawn);
            StandingRules.OthersDerive(9, new Opening { Id = id + 500, From = 390 }, 60, again);   // another post asked for in between
            StandingRules.OthersDerive(9, post, 60, again);
            stable &= drawn.Count == again.Count;
            var names = new HashSet<string>();
            for (int k = 0; k < drawn.Count && k < again.Count; k++)
            {
                stable &= drawn[k].Name == again[k].Name && drawn[k].Score == again[k].Score && drawn[k].Day == again[k].Day && drawn[k].Teamwork == again[k].Teamwork;
                namesOk &= !string.IsNullOrEmpty(drawn[k].Name) && names.Add(drawn[k].Name);
                daysOk &= drawn[k].Day >= post.From && drawn[k].Day <= post.From + 4 && drawn[k].Post == id;
            }
            withOthers += drawn.Count > 0 ? 1 : 0;
        }
        Check(stable && namesOk && daysOk && withOthers > 10,
            "work applicants: forty posts of a board each show the same others, with the same names, records and days, each time they are asked for (" + withOthers + " had others)");
        var best = new List<Peer> { new Peer { Score = 95 }, new Peer { Score = 40 } };
        Check(StandingRules.Beaten(70, 0, best, 9, 41) == 0, "work applicants: an applicant of 95 beats a player of 70 for the post");
        var weak = new List<Peer> { new Peer { Score = 40 }, new Peer { Score = 50 } };
        Check(StandingRules.Beaten(80, 0, weak, 9, 41) == -1 && StandingRules.Beaten(80, 0, new List<Peer>(), 9, 41) == -1,
            "work applicants: the player is first when no other applicant scores more, and when there are none");
        Check(StandingRules.Beaten(80, 0, weak, 9, 41) == StandingRules.Beaten(80, 0, weak, 9, 41), "work applicants: the weighing is the same on a reload");
    }

    private static void WkRoster()
    {
        var f = new RosterFile { Seed = 5 };
        int course = StudyCatalogue.Accounting;
        Intake i = RosterRules.Ensure(f, course, 100);
        Check(i.Capacity == RosterRules.SeatsDefault && i.Start == RosterRules.StartOf(100) && i.Start % RosterRules.IntakeDays == 0,
            "work roster: a cohort has 24 seats and starts on a cohort day");
        int early = RosterRules.Taken(5, course, i.Start, 24, i.Start - 60);
        int mid = RosterRules.Taken(5, course, i.Start, 24, i.Start - 20);
        int late = RosterRules.Taken(5, course, i.Start, 24, i.Start + 10);
        Check(early == 0 && mid <= late && late <= 24, "work roster: others take seats over time, none before the intake opens, never more than the capacity");
        Intake later = RosterRules.Ensure(f, course, i.Start + 20);
        Check(later == i && i.Students.Count == RosterRules.Taken(5, course, i.Start, 24, i.Start + 20),
            "work roster: asked again later, the same cohort has more students, those it had kept");
        int full = -1;
        for (int c = 0; c < 60 && full < 0; c++)
        {
            if (RosterRules.Taken(5, c, 392, 24, 417) == 24)
            {
                full = c;
            }
        }
        Check(full >= 0, "work roster: some cohorts fill every seat");
        if (full >= 0)
        {
            Intake closed = RosterRules.Ensure(f, full, 417);
            Check(RosterRules.Left(closed) == 0 && !RosterRules.CanJoin(closed) && !RosterRules.Join(closed), "work roster: a full course cannot be joined");
        }
        Intake mine = RosterRules.Ensure(f, StudyCatalogue.Business, 200);
        int before = RosterRules.Left(mine);
        bool joined = RosterRules.Join(mine);
        Check(before == 0 || (joined && RosterRules.Left(mine) == before - 1 && !RosterRules.Join(mine)),
            "work roster: the player takes one seat and cannot take a second");
        var staff = new List<string> { "Ava Lindqvist", "Tomas Reyes" };
        string who = null;
        for (int day = 0; day < 60 && who == null; day++)
        {
            var g = new RosterFile { Seed = 100 + day };
            Intake gi = RosterRules.Ensure(g, StudyCatalogue.Business, 200);
            RosterRules.Join(gi);
            who = RosterRules.EmployeeFollows(g, gi, staff, 200);
            Check(who == null || (gi.Students[gi.Students.Count - 1].Employee && RosterRules.EmployeeFollows(g, gi, staff, 200) == null),
                "work roster: an employee of the player's follows the same course at most once and is marked an employee");
        }
        Check(who != null, "work roster: an employee of the player's follows the same course in some saves");
        // F2: the same students every time, however often the class is read and whatever was dropped between two reads
        var again = new RosterFile { Seed = 5 };
        Intake twin = RosterRules.Ensure(again, course, 100);
        Intake wide = RosterRules.Ensure(again, course, i.Start + 20);
        bool same = true;
        for (int k = 0; k < twin.Students.Count && k < i.Students.Count; k++)
        {
            same &= i.Students[k].Name == twin.Students[k].Name && i.Students[k].Gpa == twin.Students[k].Gpa;
        }
        Check(same && twin == wide && wide.Students.Count == later.Students.Count && wide.Students[wide.Students.Count - 1].Name == later.Students[later.Students.Count - 1].Name,
            "work roster: two reads of one class give the same names and averages, a file made again too");
        var seen = new HashSet<string>();
        bool unique = true;
        for (int k = 0; k < RosterRules.SeatsDefault * 3; k++)
        {
            unique &= k >= 24 * 24 || seen.Add(RosterRules.NameOf(5, course, i.Start, k));
        }
        Check(unique && !RosterRules.NameOf(5, course, i.Start, 0).StartsWith("Student"), "work roster: no two students of a cohort share a name, and the names are the catalogue's");
        var churn = new RosterFile { Seed = 5 };
        var first = new List<string>();
        for (int round = 0; round < 3; round++)
        {
            var names = new List<string>();
            for (int c = 1; c <= 40; c++)
            {
                Intake ci = RosterRules.Ensure(churn, c, 3000 + 28 * (c % 3));
                names.Add(ci.Students.Count > 0 ? ci.Students[ci.Students.Count - 1].Name : "-");
            }
            if (round == 0)
            {
                first = names;
            }
            else
            {
                Check(string.Join("|", names) == string.Join("|", first), "work roster: reading every course again names the same students, with forty courses read " + (round + 1) + " times");
            }
        }
        Check(churn.Intakes.Count == 0 || churn.Intakes.Count >= 40 || churn.Intakes.Count <= RosterRules.IntakesMost, "work roster: the cohorts of forty courses are all kept in memory, none made again between reads");
        var strangers = new RosterFile { Seed = 5 };
        RosterRules.Ensure(strangers, course, 300);
        Intake seat = RosterRules.Ensure(strangers, StudyCatalogue.Business, 300);
        RosterRules.Join(seat);
        RosterFile loaded = RosterStore.Read(RosterStore.Write(strangers), out int _);
        Check(loaded.Intakes.Count == 1 && loaded.Intakes[0].PlayerIn && loaded.Intakes[0].Course == StudyCatalogue.Business,
            "work roster: only the cohort the player sits in is saved; a cohort of strangers is made again as it was");
        Intake remade = RosterRules.Ensure(loaded, course, 300);
        Intake was = strangers.Find(course, RosterRules.StartOf(300));
        Check(was != null && remade.Students.Count == was.Students.Count && remade.Students.Count > 0 && remade.Students[0].Name == was.Students[0].Name,
            "work roster: after a save and a load the same strangers are in the class");
        for (int c = 0; c < 40; c++)
        {
            RosterRules.Ensure(f, c % 20 + 1, 28 * c);
        }
        Check(f.Intakes.Count <= RosterRules.IntakesMost, "work roster: the cohorts kept in memory stay under their cap");
    }

    private static void WkTutorAndFloor()
    {
        var f = new RosterFile { Seed = 8 };
        Tutor t = RosterRules.TutorFor(f, StudyCatalogue.Accounting);
        Check(t != null && RosterRules.TutorFor(f, StudyCatalogue.Accounting) == t && RosterRules.TutorFor(new RosterFile { Seed = 8 }, StudyCatalogue.Accounting).Name == t.Name && t.Years >= 2 && t.Rating >= 30 && t.Rating <= 50,
            "work tutor: one tutor a course, made once, with years teaching and a rating between 3.0 and 5.0");
        Check(RosterRules.ExamEdge(new Tutor { Rating = 50 }) == 2 && RosterRules.ExamEdge(new Tutor { Rating = 40 }) == 0
            && RosterRules.ExamEdge(new Tutor { Rating = 20 }) == -2
              && RosterRules.ExamEdge(null) == 0, "work tutor: a tutor moves an exam score by two points at most, either way");
        CourseDef acc = StudyCatalogue.Find(StudyCatalogue.Accounting);
        bool floor = RosterRules.Floor(acc, out int skill, out int need);
        Check(floor && skill == TalentCatalogue.Accounting && need == 30 && !RosterRules.FloorMet(acc, 29) && RosterRules.FloorMet(acc, 30),
            "work floor: a course with a skill floor refuses enrolling under it and allows it at it");
        Check(RosterRules.FloorMet(StudyCatalogue.Find(StudyCatalogue.FirstAid), 0) && !RosterRules.Floor(null, out int _, out int _),
            "work floor: a course with no floor asks nothing");
    }

    private static void WkGrades()
    {
        Check(RosterRules.LetterOf(88) == "A" && RosterRules.LetterOf(81) == "B+" && RosterRules.LetterOf(74) == "B"
            && RosterRules.LetterOf(52) == "C" && RosterRules.LetterOf(20) == "F",
            "work grades: 88 is an A, 81 a B+, 74 a B, 52 a C, 20 an F");
        Check(RosterRules.PointsOf(88) == 400 && RosterRules.PointsOf(52) == 200 && RosterRules.PointsOf(10) == 0,
            "work grades: an A is 4.0 points, a C 2.0, an F none");
        var marks = new List<Mark>
        {
            new Mark { Credits = 6, Points = 400, Start = 10 }, new Mark { Credits = 4, Points = 330, Start = 12 },
            new Mark { Credits = 4, Points = 200, Start = 90 }, new Mark { Credits = 6, Points = 300, Start = 100 }
        };
        Check(RosterRules.Gpa(marks) == (6 * 400 + 4 * 330 + 4 * 200 + 6 * 300 + 10) / 20 && RosterRules.Gpa(new List<Mark>()) == -1,
            "work grades: the average is weighted by credits, and says nothing with no credits");
        var q = new List<int>();
        var a = new List<int>();
        int terms = RosterRules.ByTerm(marks, 60, q, a);
        Check(terms == 2 && q[0] < q[1] && a[0] == (6 * 400 + 4 * 330 + 5) / 10,
            "work grades: marks are grouped by the quarter the course began, the earlier quarter first, each its own average");
        CourseDef degree = StudyCatalogue.Find(StudyCatalogue.ComputerScience);
        CourseDef shortCourse = StudyCatalogue.Find(StudyCatalogue.FirstAid);
        Check(RosterRules.CreditsOf(degree) == 120 && RosterRules.CreditsOf(shortCourse) == 3 && RosterRules.CreditsOf(null) == 0,
            "work grades: a degree is 120 credits, a short course 3");
        var i = new Intake();
        i.Students.Add(new Student { Name = "Priya Shah", Gpa = 400 });
        i.Students.Add(new Student { Name = "Leo Park", Gpa = 380 });
        i.Students.Add(new Student { Name = "Hana Okada", Gpa = 340 });
        var list = new List<ClassLine>();
        int rank = RosterRules.Rank(i, "You", 340, list);
        Check(rank == 3 && list.Count == 4 && list[2].You && list[0].Name == "Priya Shah",
            "work grades: the player is placed in the class by average, first on a tie, with the others named");
        Check(RosterRules.Rank(null, "You", 300, list) == 1 && list.Count == 1, "work grades: alone in a class the player is first");
    }

    private static void WkOffers()
    {
        Check(HireOffers.Posts(new JobAd { Posts = 0 }) == 1 && HireOffers.Posts(new JobAd { Posts = 9 }) == HireOffers.PostsMost
            && HireOffers.Posts(null) == 1,
            "work offers: an ad has one to five posts");
        var f = new HiringFile { Seed = 4 };
        var ad = new JobAd { Id = 1, Posts = 2, From = 0, To = 90 };
        f.Ads.Add(ad);
        f.Applicants.Add(new Applicant { Id = 1, Ad = 1, Stage = (int)ApplyStage.Accepted });
        Check(HireOffers.ToFill(f, ad) == 1 && !HireOffers.Full(f, ad), "work offers: two posts with one accepted leaves one to fill");
        f.Applicants.Add(new Applicant { Id = 2, Ad = 1, Stage = (int)ApplyStage.Started });
        Check(HireOffers.ToFill(f, ad) == 0 && HireOffers.Full(f, ad), "work offers: an ad with every post taken is full and brings no more applicants");

        var g = new HiringFile { Seed = 21 };
        var s = new Seeker { Id = 1, Ask = 4000 };
        s.P.Level = 2;
        g.Seekers.Add(s);
        Family family = s.P.Def.Family;
        g.RivalAds.Add(new RivalAd { Rival = "Guo Interactive", Family = (int)family, Level = 2, To = 999 });
        var app = new Applicant { Id = 5, Seeker = 1, Ad = 1, Stage = (int)ApplyStage.Shortlisted };
        g.Applicants.Add(app);
        int made = -1;
        for (int day = 100; day < 400 && made < 0; day++)
        {
            HireOffers.Day(g, day, new List<string> { "Guo Interactive" });
            made = app.RivalBid > 0 ? day : -1;
        }
        Check(made > 0 && app.RivalBy == "Guo Interactive" && app.RivalBid == (int)(4000L * (10000 + HireOffers.OpenOfferBp) / 10000)
            && app.RivalLapse == made + HireOffers.RivalDays,
            "work offers: a rival with an ad in the applicant's line makes a named offer over their ask, with a deadline three days on");
        Check(HireOffers.Competing(app, made) && !HireOffers.Beats(app, app.RivalBid - 1) && HireOffers.Beats(app, app.RivalBid),
            "work offers: the player beats the offer by matching it, not by offering less");
        var h = new HiringFile { Seed = 21 };
        var s2 = new Seeker { Id = 1, Ask = 4000 };
        h.Seekers.Add(s2);
        var late = new Applicant { Id = 6, Seeker = 1, Stage = (int)ApplyStage.Offered, RivalBid = 4400, RivalLapse = 110, RivalBy = "Guo Interactive" };
        h.Applicants.Add(late);
        HireOffers.Day(h, 110, null);
        Check(!s2.Taken, "work offers: on the deadline's last day the applicant is still the player's to win");
        HireOffers.Day(h, 111, null);
        Check(s2.Taken && s2.Employer == "Guo Interactive", "work offers: the day after the deadline the rival takes the applicant");
        var k = new HiringFile { Seed = 21 };
        var s3 = new Seeker { Id = 1, Ask = 4000 };
        k.Seekers.Add(s3);
        var won = new Applicant { Id = 7, Seeker = 1, Stage = (int)ApplyStage.Accepted, RivalBid = 4400, RivalLapse = 110 };
        k.Applicants.Add(won);
        HireOffers.Day(k, 112, null);
        Check(!s3.Taken && won.RivalLapse == 0, "work offers: when the player hired first the rival loses the applicant and its offer ends");
        var old = new Applicant { Id = 8, Seeker = 1, Stage = (int)ApplyStage.Countered, RivalBid = 4300 };
        var m = new HiringFile { Seed = 21 };
        m.Seekers.Add(new Seeker { Id = 1, Ask = 4000 });
        m.Applicants.Add(old);
        HireOffers.Day(m, 200, null);
        Check(old.RivalLapse == 200 + HireOffers.OldBidLapse,
            "work offers: a bid from an older save, with no deadline, is given one from the day it is first seen");
        var ans = new HiringFile { Seed = 21 };
        var seeker = new Seeker { Id = 1, Ask = 4000 };
        var ap = new Applicant { Id = 9, Seeker = 1, Offer = 4000, RivalBid = 4400, RivalLapse = 300, RivalBy = "Guo Interactive", Interest = 700 };
        Check(HiringRules.Answer(ans, ap, seeker, 299, 0, 1000) == ApplyStage.Countered && ap.Counter == 4400,
            "work offers: an offer under another firm's open bid is answered with a counter at the bid");
        ap.Offer = 4400;
        ap.Stage = (int)ApplyStage.Offered;
        Check(HiringRules.Answer(ans, ap, seeker, 299, 0, 1000) == ApplyStage.Accepted, "work offers: an offer that matches the bid is accepted");
    }

    private static void WkPosts()
    {
        var f = new HiringFile { Seed = 6 };
        var s = new Seeker { Id = 3 };
        s.P.Level = 3;
        s.P.YearsDays = 12 * 365;
        string history = HireOffers.PastJobs(6, s);
        var jobs = new List<PastJob>();
        int n = HireOffers.Read(history, jobs);
        Check(n == HireOffers.HistoryMost && jobs[0].Level <= 3 && jobs[0].Level >= jobs[n - 1].Level && jobs[0].Years >= 1
            && history == HireOffers.PastJobs(6, s),
            "work history: a senior seeker lists three past jobs, each at their level or below, a few years each, the same every time");
        // no CV lists more years of work than the seeker has behind them, whatever their level, and a stored history that does is made again
        bool within = true;
        var listed = new List<PastJob>();
        for (int id = 1; id <= 300; id++)
        {
            var t = new Seeker { Id = id };
            t.P.Level = id % TalentCatalogue.Levels;
            t.P.YearsDays = TechRandom.Range(6, id, 1, 0, TalentCatalogue.LevelYearsLeast[t.P.Level], TalentCatalogue.LevelYearsMost[t.P.Level]) * 365;
            HireOffers.Read(HireOffers.HistoryOf(6, t), listed);
            int total = 0;
            for (int k = 0; k < listed.Count; k++)
            {
                total += listed[k].Years;
            }
            within &= total <= HireOffers.YearsBudget(t) && (listed.Count == 0 || t.P.Level > 0);
        }
        var old = new Seeker { Id = 9, History = "2:4:Atlas#1:4:Atlas#0:4:Atlas" };
        old.P.Level = 2;
        old.P.YearsDays = 5 * 365;
        HireOffers.Read(HireOffers.HistoryOf(6, old), listed);
        int oldTotal = 0;
        for (int k = 0; k < listed.Count; k++)
        {
            oldTotal += listed[k].Years;
        }
        Check(within && oldTotal <= 5, "work history: no CV lists more years than the seeker has (300 seekers of every level), and one an older save made with more is made again");
        s.P.Level = 0;
        Check(HireOffers.HistoryOf(6, s).Length == 0 && s.History == "-", "work history: the first level has no past jobs, and that is kept");
        Check(HireOffers.Read("x:y#3:2", jobs) == 0 && HireOffers.Read(null, jobs) == 0, "work history: a damaged history reads as none");
        var store = new Seeker { Id = 4 };
        store.P.Level = 2;
        f.Seekers.Add(store);
        HireOffers.HistoryOf(6, store);
        HiringFile back = HiringStore.Read(HiringStore.Write(f), out int _);
        Check(back.Seekers.Count == 1 && back.Seekers[0].History == store.History, "work history: a CV's past jobs are saved with the seeker");
    }

    private static void WkSellers()
    {
        var f = new SellerFile();
        SellerRules.Ensure(f, 12, k => "Seller " + k);
        Check(f.Sellers.Count == SellerRules.PoolSize && f.Sellers[0].Id == 1 && f.Sellers[0].Rating >= 20 && f.Sellers[0].Rating <= 50,
            "work sellers: a pool of 24 named sellers, each with a rating");
        SellerRules.Ensure(f, 12, k => "Again " + k);
        Check(f.Sellers.Count == SellerRules.PoolSize && f.Sellers[0].Name == "Seller 0", "work sellers: asked again the pool is the same people");
        bool tags = false, plain = false;
        Seller discount = null, premium = null;
        for (int i = 0; i < f.Sellers.Count; i++)
        {
            tags |= f.Sellers[i].Tag != 0;
            plain |= f.Sellers[i].Tag == 0;
            discount = discount ?? (f.Sellers[i].Tag == (int)SellerTag.Discount ? f.Sellers[i] : null);
            premium = premium ?? (f.Sellers[i].Tag == (int)SellerTag.Premium ? f.Sellers[i] : null);
        }
        Check(tags && plain, "work sellers: some sellers carry a tag and most do not");
        discount = discount ?? new Seller { Tag = (int)SellerTag.Discount };
        premium = premium ?? new Seller { Tag = (int)SellerTag.Premium };
        Check(SellerRules.Asking(discount, 1000, false, false) == 890 && SellerRules.Asking(premium, 1000, false, false) == 1060
            && SellerRules.Asking(null, 1000, false, false) == 1000,
            "work sellers: a discount seller asks 11% under the going price, a premium seller 6% over, a plain one the price");
        Seller rival = SellerRules.ForRival(f, "Guo Logistics", 12);
        Check(rival != null && rival.Rival == "Guo Logistics" && SellerRules.ForRival(f, "Guo Logistics", 12) == rival && SellerRules.Asking(rival,
            10000, true, false) == 9700
              && SellerRules.Asking(rival, 10000, false, true) == 10400,
                  "work sellers: a rival sells as a seller of its own, a little under on vehicles and over on machines");
        for (int i = 0; i < 10; i++)
        {
            SellerRules.ForRival(f, "Rival " + i, 12);
        }
        Check(f.Sellers.Count <= SellerRules.PoolSize + SellerRules.RivalsMost, "work sellers: at most six rivals sell");
        bool pickedPerson = true;
        for (int roll = 0; roll < 100; roll++)
        {
            pickedPerson &= string.IsNullOrEmpty(SellerRules.Pick(f, roll).Rival);
        }
        Check(pickedPerson, "work sellers: a listing's seller is a person of the pool; a rival lists by its own rule");
        var list = new List<Listing> { new Listing { Id = 1, SellerId = 4 }, new Listing { Id = 2, SellerId = 4 }, new Listing { Id = 3,
            SellerId = 5 }, new Listing { Id = 4, SellerId = 4, Gone = true } };
        var more = new List<Listing>();
        Check(SellerRules.MoreFrom(list, 4, 1, more) == 1 && more[0].Id == 2,
            "work sellers: more from this seller leaves out the listing shown and the sold ones");
        Check(SellerRules.VersusWorth(880, 1000) == -12 && SellerRules.VersusWorth(1030, 1000) == 3 && SellerRules.VersusWorth(5, 0) == 0,
            "work sellers: asking against worth is a whole per cent, under or over");
        ThingDef machine = HustleCatalogue.Things[HustleCatalogue.Things.Length - 6];
        ThingDef stock = HustleCatalogue.Things[HustleCatalogue.Things.Length - 1];
        Check(machine.Kind == ThingKind.Machine && stock.Kind == ThingKind.Stock && SellerRules.CategoryOf(new Listing(),
            machine) == SellerRules.CategoryMachines
              && SellerRules.CategoryOf(new Listing { Car = true }, null) == SellerRules.CategoryVehicles && SellerRules.CategoryOf(new Listing(),
                  stock) == SellerRules.CategoryStock,
            "work sellers: machines and stock lots are categories of their own, a car a vehicle");
        var state = new HustleState { Seed = 3 };
        var homes = new List<HustlePlace> { new HustlePlace("Broadway", 1), new HustlePlace("5th Avenue", 2) };
        var book = new SellerFile();
        SellerRules.Ensure(book, 3, k => "Seller " + k);
        var dealers = new List<RivalDealer> { new RivalDealer { Name = "Guo Logistics", Machines = true } };
        bool rivalListed = false, stockUnder = true, allNamed = true;
        for (int day = 1; day < 40; day++)
        {
            HustleRules.Morning(state, day, homes, null, null, 100, 1000, book, dealers);
            for (int i = 0; i < state.Listings.Count; i++)
            {
                Listing l = state.Listings[i];
                allNamed &= l.SellerId > 0 && book.Of(l.SellerId) != null && book.Of(l.SellerId).Name == l.Seller;
                rivalListed |= l.Seller == "Guo Logistics";
            }
        }
        Check(allNamed && rivalListed, "work sellers: every day's listing names a seller of the book, and a rival with a machines business lists machines");
        var same = new HustleState { Seed = 3 };
        var book2 = new SellerFile();
        SellerRules.Ensure(book2, 3, k => "Seller " + k);
        HustleRules.Morning(same, 5, homes, null, null, 100, 1000, book2, dealers);
        HustleRules.Morning(state, 5, homes, null, null, 100, 1000, book, dealers);
        bool equal = same.Listings.Count == state.Listings.Count;
        for (int i = 0; equal && i < same.Listings.Count; i++)
        {
            equal &= same.Listings[i].Asking == state.Listings[i].Asking && same.Listings[i].Seller == state.Listings[i].Seller;
        }
        Check(equal && stockUnder, "work sellers: a day's listings are the same for the same seed, book and day");
        var plainDay = new HustleState { Seed = 3 };
        HustleRules.Morning(plainDay, 5, homes, null, null, 100, 1000);
        Check(plainDay.Listings.Count > 0 && plainDay.Listings[0].SellerId == 0,
            "work sellers: without a book the day's listings are as they were, a name alone");
    }

    private static void WkFilters()
    {
        Check(PostFilterRules.Near(PostFilterRules.NearMetres, false) && !PostFilterRules.Near(PostFilterRules.NearMetres + 1, false) && !PostFilterRules.Near(-1, false) && PostFilterRules.Near(99999, true),
            "work filters: Near you is within the distance from home, or work from home, and a place unknown is not near");
        Check(PostFilterRules.Open(false, false) && !PostFilterRules.Open(false, true) && PostFilterRules.Open(true, true),
            "work filters: Open now is a workplace the game's schedule has open, or work from home");
    }

    // A player with no job is shown the best-paid posts they qualify for first: by the post's pay, not by the text of a cell ("$9.50" is under "$12.00").
    private static void WkBestPaid()
    {
        var rows = new List<RealLifeOverhaul.Ui.WorkRow>();
        int[] pays = { 950, 1200, 1075, 20000, 800 };
        for (int i = 0; i < pays.Length; i++)
        {
            rows.Add(new RealLifeOverhaul.Ui.WorkRow { Id = i + 1, Order = pays[i], Mask = i == 3 ? 4 : 2, Cells = new[] { null, "Pine", "09:00", "$" + pays[i] / 100.0, "-", "You qualify" } });
        }
        RealLifeOverhaul.Ui.WorkBuilder.BestPaid(rows, 2);
        Check(rows.Count == 4 && rows[0].Order == 1200 && rows[1].Order == 1075 && rows[2].Order == 950 && rows[3].Order == 800,
            "work best paid: the posts a player with no job qualifies for come best pay first by the pay itself ($9.50 is under $12.00), those they do not qualify for left out");
    }

    // The work apps' people are people of the city's one people pool: picked by hash, the same every time, their id saved beside the name, an older save loads.
    private static void WkPool()
    {
        PeoplePool.Pick = null;
        PeoplePool.Find = null;
        PoolPerson none = RosterRules.PersonOf(5, StudyCatalogue.Accounting, 100, 0);
        Check(none.Id == 0 && none.Name.Length > 0 && none.Name == RosterRules.PersonOf(5, StudyCatalogue.Accounting, 100, 0).Name,
            "work pool: with the population off a student is a name of the catalogue, the same every time");
        PeoplePool.Pick = h => new PoolPerson { Id = (int)(h % 500) + 1, Name = "Pool " + ((int)(h % 500) + 1) };
        PeoplePool.Find = id => id >= 1 && id <= 500 ? new PoolPerson { Id = id, Name = "Pool " + id } : default(PoolPerson);
        try
        {
            var f = new RosterFile { Seed = 5 };
            Intake i = RosterRules.Ensure(f, StudyCatalogue.Accounting, 300);
            Intake j = RosterRules.Ensure(f, StudyCatalogue.Accounting, 300);
            bool ids = i.Students.Count > 0 && i == j;
            for (int k = 0; k < i.Students.Count; k++)
            {
                ids &= i.Students[k].PersonId > 0 && i.Students[k].Name == "Pool " + i.Students[k].PersonId && i.Students[k].Name == RosterRules.NameOf(5, i.Course, i.Start, k);
            }
            Check(ids, "work pool: students are people of the pool, each with their id, the same cohort every time it is read");
            Tutor t = RosterRules.TutorFor(f, StudyCatalogue.Accounting);
            Check(t.PersonId > 0 && t.Name == "Pool " + t.PersonId, "work pool: a course's teacher is a person of the pool");
            i.PlayerIn = true;
            string line = RosterStore.Write(f);
            RosterFile back = RosterStore.Read(line, out int _);
            Intake bi = back.Find(i.Course, i.Start);
            bool kept = bi != null && bi.Students.Count == i.Students.Count && back.TutorOf(StudyCatalogue.Accounting).PersonId == t.PersonId;
            for (int k = 0; kept && k < i.Students.Count; k++)
            {
                kept &= bi.Students[k].PersonId == i.Students[k].PersonId;
            }
            Check(kept, "work pool: the ids of students and the teacher are saved and read back");
            // a save from before the ids: the same line without them loads, with no person
            int cut = line.LastIndexOf('|');
            RosterFile oldSave = RosterStore.Read(line.Replace("|" + i.Students[0].PersonId, string.Empty), out int _);
            Check(oldSave.Intakes.Count >= 1, "work pool: a save from before the ids still loads");
            var others = new List<Peer>();
            StandingRules.OthersDerive(5, new Opening { Id = 14, From = 40, Rank = 1 }, 60, others);
            bool fine = others.Count >= 0;
            for (int k = 0; k < others.Count; k++)
            {
                fine &= others[k].PersonId > 0 && others[k].Name == "Pool " + others[k].PersonId;
            }
            Check(fine, "work pool: the other applicants for a post are people of the pool");
            var book = new SellerFile();
            SellerRules.Ensure(book, 9, k => new PoolPerson { Id = 100 + k, Name = "Seller " + k });
            SellerFile sb = SellerStore.Read(SellerStore.Write(book), out int _);
            Check(sb.Sellers.Count == book.Sellers.Count && sb.Sellers[3].PersonId == 103, "work pool: sellers are people of the pool and their ids are saved");
        }
        finally
        {
            PeoplePool.Pick = null;
            PeoplePool.Find = null;
        }
    }

    private static readonly UtilitiesWords EnglishWords = new UtilitiesWords((k, e) => e, v => v.ToString(), () => System.Globalization.CultureInfo.InvariantCulture);

    // A promotion goes by teamwork as the page says it does, and the chart's two lines end together however many weeks each has.
    private static void WkTeamworkAndChart()
    {
        var job = new CareerJob { Mine = new[] { 70, 72, 75, 74, 78 }, Theirs = new[] { 60, 62, 64, 66, 68, 70, 72, 74 }, RivalName = "Marcus Lee" };
        UiChartModel chart = CareerMyJob.Chart(job, EnglishWords);
        Check(chart.Series.Length == 2 && chart.Series[1].Values.Length == 5 && chart.Series[1].Values[4] == 74f && chart.Series[1].Values[0] == 66f,
            "work chart: the colleague's line is drawn over the player's first weeks and ends in the same week (the last five of eight)");
        var shorter = new CareerJob { Mine = new[] { 70, 72, 75, 74, 78 }, Theirs = new[] { 60, 62 }, RivalName = "Marcus Lee" };
        UiChartModel two = CareerMyJob.Chart(shorter, EnglishWords);
        Check(two.Series.Length == 2 && two.Series[1].Values.Length == 5 && two.Series[1].Values[4] == 62f,
            "work chart: a colleague with fewer weeks than the player still has a line, ending in the same week");
        Check(PostFilterRules.PromotionTeamwork == 50 && StandingRules.TeamworkOf(0) == PostFilterRules.PromotionTeamwork,
            "work teamwork: the bar a promotion asks is the middle standing, which a player nobody has helped or crossed meets");
    }

    private static void WkSaves()
    {
        var s = new StandingFile { Hired = 12, Street = "Pine Street", Number = 3, Employer = "Hudson Depot", Seed = 77 };
        StandingRules.Week(s, 71, 70);
        StandingRules.Week(s, 74, 77);
        s.Peers.Add(StandingRules.Colleague("Marcus Lee", 2, 70, PeerRole.UpForPost, 12, 77, 5));
        s.Others.Add(StandingRules.Applicant("Nia Brooks", new Opening { Id = 9, Rank = 1 }, 60, 120, 77, 0));
        StandingFile back = StandingStore.Read(StandingStore.Write(s), out int v);
        Check(v == StandingStore.Version && back.Hired == 12 && back.Employer == "Hudson Depot" && back.Count == 2 && back.Ring[1] == 74
            && back.WeekOf == s.WeekOf && back.Seed == 77
              && back.Peers.Count == 1 && back.Peers[0].Name == "Marcus Lee" && back.Peers[0].Count == 5 && back.Peers[0].Ring[4] == s.Peers[0].Ring[4]
              && back.Others.Count == 1 && back.Others[0].Post == 9 && back.Others[0].Name == "Nia Brooks",
            "work saves: the standing, its ring, a colleague and another applicant survive a save and a load");
        Check(StandingStore.Write(back) == StandingStore.Write(s), "work saves: the standing line writes the same after a round trip");
        for (int d = 1; d <= 10; d++)
        {
            StandingRules.Shift(s, d, 480, 70 + d, 17200);
        }
        StandingRules.Shift(s, 10, 240, 90, 8600);
        StandingFile sh = StandingStore.Read(StandingStore.Write(s), out int _);
        Check(s.Shifts.Count == StandingRules.ShiftsMost && s.Shifts[s.Shifts.Count - 1].Day == 10 && s.Shifts[s.Shifts.Count - 1].Minutes == 240 && sh.Shifts.Count == 7 && sh.Shifts[0].Day == 4
              && sh.Shifts[6].Pay == 8600, "work saves: the last seven worked shifts are kept, a day written twice is one record, and they survive a save and a load");
        string oldShiftLine = StandingStore.Write(s);
        oldShiftLine = oldShiftLine.Substring(0, oldShiftLine.IndexOf("~r:", System.StringComparison.Ordinal) >= 0 ? oldShiftLine.IndexOf("~r:", System.StringComparison.Ordinal) : oldShiftLine.Length);
        Check(StandingStore.Read(oldShiftLine, out int _).Shifts.Count == 0, "work saves: a standing line from before the recent shifts loads with none");
        var r = new RosterFile { Seed = 5 };
        Intake i = RosterRules.Ensure(r, StudyCatalogue.Business, 100);
        RosterRules.Join(i);
        Tutor made = RosterRules.TutorFor(r, StudyCatalogue.Business);
        RosterFile rb = RosterStore.Read(RosterStore.Write(r), out int _);
        Check(rb.Intakes.Count == 1 && rb.Intakes[0].PlayerIn && rb.Intakes[0].Students.Count == i.Students.Count && rb.Tutors.Count == 1
            && rb.Tutors[0].Name == made.Name
              && (i.Students.Count == 0 || rb.Intakes[0].Students[0].Gpa == i.Students[0].Gpa),
                  "work saves: the cohorts, their students and the tutors survive a save and a load");
        var sellers = new SellerFile();
        SellerRules.Ensure(sellers, 3, k => "Seller " + k);
        SellerRules.ForRival(sellers, "Guo Logistics", 3);
        SellerFile sb = SellerStore.Read(SellerStore.Write(sellers), out int _);
        Check(sb.Sellers.Count == sellers.Sellers.Count && sb.NextId == sellers.NextId && sb.OfRival("Guo Logistics") != null
            && sb.Sellers[3].Rating == sellers.Sellers[3].Rating,
            "work saves: the sellers, a rival's among them, survive a save and a load");
        var state = new HustleState { Seed = 2 };
        state.Listings.Add(new Listing { Id = 4, Thing = "phone", Condition = 70, Shown = 70, Asking = 200, Floor = 150, Seller = "Priya Shah",
            SellerId = 7, Street = "Broadway", Number = 2 });
        HustleState sback = HustleStore.Read(HustleStore.Write(state), out int _);
        Check(sback.Listings.Count == 1 && sback.Listings[0].SellerId == 7 && sback.Listings[0].Seller == "Priya Shah"
            && sback.Listings[0].Street == "Broadway",
            "work saves: a listing keeps its seller's record in the Marketplace line");
        string oldLine = HustleStore.Write(state).Replace("|Broadway|7", "|Broadway|");
        HustleState sold = HustleStore.Read(oldLine, out int _);
        Check(sold.Listings.Count == 1 && sold.Listings[0].SellerId == 0 && sold.Listings[0].Seller == "Priya Shah",
            "work saves: a Marketplace line from before the sellers loads, the listing with its name alone");
        var hiring = new HiringFile { Seed = 3 };
        hiring.Ads.Add(new JobAd { Id = 1, Posts = 3 });
        hiring.Applicants.Add(new Applicant { Id = 1, Seeker = 1, Ad = 1, RivalBid = 4000, RivalBy = "Guo", RivalLapse = 55 });
        HiringFile hb = HiringStore.Read(HiringStore.Write(hiring), out int _);
        Check(hb.Ads[0].Posts == 3 && hb.Applicants[0].RivalLapse == 55 && hb.Applicants[0].RivalBy == "Guo",
            "work saves: an ad's posts and a rival's deadline survive a save and a load");
    }
}
