using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Careers;

// An employer that stops existing or stops trading where the player works lets the player go cleanly (Scripts/Careers: Work/ClosureRules,
// CareersDirector.Closing). A player's report: a month of applications to a business that closed over the weekend, then no way to go to work and
// only a quit or a sacking (with its penalty and its wait) to apply again. First which posts still have an employer, then where one that moved
// goes, the pay, the job's end on the CV, the priority, the save, the source that applies it on every path, then the texts.
internal static partial class Program
{
    private static void Layoff()
    {
        LayoffEmploys();
        LayoffSuccessor();
        LayoffPay();
        LayoffClose();
        LayoffApplications();
        LayoffSave();
        LayoffMend();
        LayoffSources();
        LayoffTexts();
    }

    private static Profession LayoffProfession => ProCatalogue.Find(ProCatalogue.Accountant);

    private static Opening LayoffPost(EmployerKind kind, string employer, string rival = "", RealLifeOverhaul.Careers.Via via = RealLifeOverhaul.Careers.Via.Board, string street = "Quay Road", int number = 12)
    {
        return new Opening
        {
            Id = 1, Prof = ProCatalogue.Accountant, Rank = 1, Kind = (int)kind, Employer = employer, Street = street, Number = number, Rival = rival, Pay = 2500, Via = (int)via
        };
    }

    private static Premises Trading(string name, string rival = "") => new Premises { Stands = true, Trades = true, Name = name, Rival = rival };

    private static EmployerRef LayoffEmployer(string name, string street, int number, EmployerKind kind = EmployerKind.City, string rival = "", string type = null)
    {
        type = type ?? LayoffProfession.Types[0];
        return new EmployerRef { Name = name, Street = street, Number = number, Kind = kind, Rival = rival, Type = type, Professions = ProCatalogue.EmployedBy(type) };
    }

    // ---------------------------------------------------------------- which posts still have an employer

    private static void LayoffEmploys()
    {
        Opening city = LayoffPost(EmployerKind.City, "Gerardo Toombs");
        Check(ClosureRules.Employs(city, Trading("Gerardo Toombs")), "layoff: a post at a business that trades there under its name has its employer");
        Check(!ClosureRules.Employs(city, new Premises { Stands = true, Trades = false }),
            "layoff: the business closed (the game empties its registration), bought by the player or let to another player: no employer (the report: closed over the weekend)");
        Check(!ClosureRules.Employs(city, new Premises { Stands = true, Trades = false, Name = "Gerardo Toombs" }),
            "layoff: a business that has stopped trading is gone even where the registration still carries its name (bought by the player, or let go)");
        Check(!ClosureRules.Employs(city, Trading("Quay Cafe")), "layoff: a different business in the same unit is not the employer");
        Check(!ClosureRules.Employs(city, new Premises()), "layoff: the building came down (a demolition lets its records go): no employer");
        Check(ClosureRules.Employs(LayoffPost(EmployerKind.Public, "@reallife_work_employer_health"), new Premises())
              && ClosureRules.Employs(LayoffPost(EmployerKind.City, "Gerardo Toombs", street: string.Empty), new Premises()),
            "layoff: the public sector never closes, and a post with no address has no premises to lose");
        Opening rival = LayoffPost(EmployerKind.Rival, "Quay Cafe", "rival:7");
        Check(ClosureRules.Employs(rival, Trading("Quay Cafe", "rival:7")) && !ClosureRules.Employs(rival, Trading("Quay Cafe", "rival:8"))
              && !ClosureRules.Employs(rival, Trading("Other Name", "rival:7")) && !ClosureRules.Employs(rival, new Premises { Stands = true }),
            "layoff: a rival's business is still that rival's, under its name, or it is not the employer");
        Opening ad = LayoffPost(EmployerKind.Rival, "Harbour Holdings", "rival:7", RealLifeOverhaul.Careers.Via.JobsBoard);
        Check(ClosureRules.Employs(ad, Trading("Harbour Cafe", "rival:7")) && !ClosureRules.Employs(ad, Trading("Harbour Cafe", "rival:9")),
            "layoff: a rival's jobs board ad names the company, so the business under it may be called anything while it is the rival's");
        Opening tech = LayoffPost(EmployerKind.TechFirm, "Northlight");
        Check(ClosureRules.Employs(tech, new Premises { Stands = true }) && !ClosureRules.Employs(tech, new Premises()),
            "layoff: a tech firm's office is its building: it stands, or it is gone");
        Opening techAtRival = LayoffPost(EmployerKind.TechFirm, "Northlight", "rival:7");
        Check(ClosureRules.Employs(techAtRival, Trading("Anything", "rival:7")) && !ClosureRules.Employs(techAtRival, new Premises { Stands = true, Trades = false }),
            "layoff: a tech firm that works from a rival's business goes with that business");
        Check(ClosureRules.Employs(null, new Premises()), "layoff: no post, no question");
    }

    // ---------------------------------------------------------------- the employer moved

    private static void LayoffSuccessor()
    {
        Opening city = LayoffPost(EmployerKind.City, "Gerardo Toombs");
        var all = new List<EmployerRef>
        {
            LayoffEmployer("Gerardo Toombs", "Quay Road", 12),                       // where the post is: not a move
            LayoffEmployer("Quay Cafe", "Abbey Road", 3),                            // another business
            LayoffEmployer("Gerardo Toombs", "Wharf Street", 20),
            LayoffEmployer("Gerardo Toombs", "Abbey Road", 9),
            LayoffEmployer("Gerardo Toombs", "Abbey Road", 4, type: "zzz_employs_nobody"),
            LayoffEmployer("Gerardo Toombs", "Zenith Street", 1, EmployerKind.Rival, "rival:7")
        };
        int at = ClosureRules.Successor(city, all);
        Check(at == 3, "layoff: an employer that stands elsewhere under its name takes the job: the first by street and number (" + at + ")");
        all.RemoveAt(3);
        Check(ClosureRules.Successor(city, all) == 2, "layoff: with that one gone the next takes it, and a business that employs nobody of the profession never does");
        all.RemoveAt(2);
        Check(ClosureRules.Successor(city, all) == -1, "layoff: no business of the name elsewhere: the employer closed, it did not move");
        Opening rival = LayoffPost(EmployerKind.Rival, "Quay Cafe", "rival:7");
        var theirs = new List<EmployerRef> { LayoffEmployer("Quay Cafe", "Quay Road", 12, EmployerKind.Rival, "rival:7"), LayoffEmployer("Quay Cafe", "Pier Lane", 2, EmployerKind.Rival, "rival:7"),
            LayoffEmployer("Quay Cafe", "Pier Lane", 1, EmployerKind.Rival, "rival:8") };
        Check(ClosureRules.Successor(rival, theirs) == 1, "layoff: a rival's business that moves is found by its owner, never another rival's of the same name");
        Check(ClosureRules.Successor(LayoffPost(EmployerKind.TechFirm, "Northlight"), all) == -1 && ClosureRules.Successor(LayoffPost(EmployerKind.Public, "Council"), all) == -1
              && ClosureRules.Successor(LayoffPost(EmployerKind.City, "Gerardo Toombs", street: string.Empty), all) == -1 && ClosureRules.Successor(null, all) == -1,
            "layoff: an office, the public sector and a post with no address do not move by themselves");
    }

    // ---------------------------------------------------------------- the pay

    private static Job LayoffJob(int hired, EmployerKind kind = EmployerKind.City)
    {
        var j = new Job { Hired = hired, RankDay = hired, ReviewDay = hired, Pay = 2500, SettledDay = hired - 1 };
        LayoffPost(kind, "Gerardo Toombs").CopyTo(j.Post);
        return j;
    }

    private static void LayoffPay()
    {
        Job j = LayoffJob(10);
        long day = LifeRules.DayPay(j), week = LifeRules.WeekPay(j);
        Check(day > 0 && week > day * 3, "layoff: a week's pay is more than three working days' (" + week + " against " + day * 3 + ")");
        Check(ClosureRules.Payment(j, 11, 60) == day * 3 && ClosureRules.Payment(j, 10, 365) == day * 3 && ClosureRules.PayLeastDays == 3,
            "layoff: someone who had worked a day is paid three working days, never nothing");
        Check(ClosureRules.Payment(j, 10 + 60, 60) == week && ClosureRules.Payment(j, 10 + 60 * 2 + 5, 60) == week * 2 && ClosureRules.Payment(j, 10 + 59, 60) == day * 3,
            "layoff: from a year in it is a week's pay for each year of service");
        Check(ClosureRules.Payment(j, 10 + 365, 365) == week && ClosureRules.Payment(j, 10 + 120, 28) == week * 2,
            "layoff: the year is the game's, never shorter than sixty days, as the careers' redundancy has it");
        long big = ClosureRules.Payment(j, 10 + 60 * 10, 60);
        Check(big == week * 10 && big >= ClosureRules.Payment(j, 10 + 60, 60), "layoff: the pay never falls as the service grows");
        Job short1 = LayoffJob(10);
        short1.Pay = 1;
        Check(ClosureRules.Payment(short1, 10, 60) > 0 || LifeRules.DayPay(short1) == 0, "layoff: a tiny wage still pays what the rule says");
    }

    // ---------------------------------------------------------------- the job's end

    private static void LayoffClose()
    {
        var w = new WorkFile();
        Job j = LayoffJob(100);
        j.Warnings = 2;
        j.Absent = 3;
        j.Due = 5;
        j.Worked = 0;
        j.Late = 2;
        j.Rating = 1;
        j.NoticeEnd = 140;
        j.Leaving = (int)LeaveReason.Resigned;
        j.NonCompete = true;
        j.Earned = 5000;
        w.Job = j;
        int restraint = w.RestraintUntil;
        Spell old = ProRules.Spell(j, 101, LeaveReason.Resigned);
        Check(old.Reference == 2 && ProRules.Spell(j, 101, LeaveReason.Dismissed).Reference == 1 && ProRules.Spell(j, 101, LeaveReason.WalkedOut).Reference == 1,
            "layoff: for contrast, a day's job left by quitting reads fair on the CV and one ended by a sacking or a walk out reads poor");
        Spell s = ClosureRules.Close(w, j, 101, 60, out long cents);
        Check(w.Job == null && w.History.Count == 1 && ReferenceEquals(w.History[0], s), "layoff: the job is over and on the CV");
        Check(s.Leaving == (int)LeaveReason.EmployerClosed && s.Leaving != (int)LeaveReason.Resigned && s.Leaving != (int)LeaveReason.Dismissed && s.Leaving != (int)LeaveReason.WalkedOut
              && s.Leaving != (int)LeaveReason.Misconduct && s.Leaving != (int)LeaveReason.Redundant, "layoff: it ends as the employer closed, not a quit, a sacking, a walk out or a cut");
        Check(s.Reference == 3, "layoff: the reference is good, though the service was a day, nothing was worked and warnings stood");
        Check(s.Top == j.Post.Rank && s.Prof == j.Post.Prof && s.Employer == "Gerardo Toombs" && s.From == 100 && s.To == 101 && s.Earned == 5000,
            "layoff: the CV keeps the title, the employer, the dates and what was earned");
        ProRules.References(w, out int good, out int poor);
        Check(good == 1 && poor == 0, "layoff: the reference counts as a good one with future employers");
        Check(ProRules.Experience(w, j.Post.Prof, 500, 60) == 1 * 365 / LifeRules.PaceYear(60), "layoff: the days worked still count as experience in the profession");
        Check(!ClosureRules.Restrains(LeaveReason.EmployerClosed) && !ClosureRules.Restrains(LeaveReason.Redundant) && !ClosureRules.Restrains(LeaveReason.Retired)
              && ClosureRules.Restrains(LeaveReason.Resigned) && ClosureRules.Restrains(LeaveReason.Dismissed), "layoff: a restraint clause does not bind someone whose employer closed");
        Check(w.RestraintUntil == restraint && w.Applications.Count == 0, "layoff: no restraint, no application and no mark follows the player");
        Check(cents == LifeRules.DayPay(j) * 3 && cents > 0, "layoff: the pay is the rule's (three days for a day's service)");
        Check(w.LaidOffDay == 101 && w.LaidOffFrom == "Gerardo Toombs" && w.BenefitWeek == LifeRules.BenefitWeek(LifeRules.WeekPay(j)) && w.BenefitUntil == 101 + 7 * LifeRules.BenefitWeeks,
            "layoff: the day and the employer are kept for the Career app, and the council's benefit runs as for a redundancy");
        var first = new Opening { Id = 5, Prof = j.Post.Prof, Rank = 1, Employer = "Quay Cafe", Street = "Quay Road", Number = 14 };
        Check(w.TurnedDown(first, 101) == null && w.TurnedDown(j.Post, 101) == null, "layoff: there is no wait before applying for any post, the old employer's included");
        Check(ProRules.Spell(j, 101, LeaveReason.EmployerClosed).Reference == 3 && ProRules.Spell(LayoffJob(1), 3, LeaveReason.EmployerClosed).Reference == 3,
            "layoff: the reference rule is good for a closure, whatever the service");
        // the priority
        Check(ClosureRules.Priority(w, 101) && ClosureRules.Priority(w, 101 + ClosureRules.PriorityDays) && !ClosureRules.Priority(w, 101 + ClosureRules.PriorityDays + 1) && !ClosureRules.Priority(w, 100),
            "layoff: for three weeks the next applications have priority");
        Check(ClosureRules.Shown(w, 101 + ClosureRules.ShownDays) && !ClosureRules.Shown(w, 101 + ClosureRules.ShownDays + 1) && !ClosureRules.Shown(new WorkFile(), 101),
            "layoff: the Career app says who closed for a month, and never to someone never laid off");
        Check(ClosureRules.AnswerDay(w, 101, 2) == 101 + ProRules.AnswerDaysLeast && ClosureRules.AnswerDay(new WorkFile(), 101, 2) == 103 && ClosureRules.AnswerDay(w, 200, 2) == 202,
            "layoff: an employer answers someone laid off the next morning, and everyone else after the usual wait");
        Check(ClosureRules.InterviewAfter(w, 101) == ClosureRules.InterviewAfterDays && ClosureRules.InterviewAfter(w, 130) == ProRules.InterviewAfterDays
              && ClosureRules.InterviewAfterDays < ProRules.InterviewAfterDays, "layoff: the interview is the day after the invitation for someone laid off, shorter than the usual wait");
        w.Job = LayoffJob(102);
        Check(!ClosureRules.Priority(w, 105) && !ClosureRules.Shown(w, 105), "layoff: with a new job the priority and the Career app's line are over");
    }

    private static void LayoffApplications()
    {
        WorkFile w = new WorkFile();
        var made = new List<Application>();
        foreach (ApplyStep step in new[] { ApplyStep.Sent, ApplyStep.Interview, ApplyStep.Offered, ApplyStep.Accepted })
        {
            var a = new Application { Day = 90, Stage = (int)step, StartDay = 140, Lapse = 120, InterviewDay = 110 };
            LayoffPost(EmployerKind.City, "Gerardo Toombs").CopyTo(a.Post);
            w.Applications.Add(a);
            made.Add(a);
        }
        var done = new Application { Day = 90, Stage = (int)ApplyStep.Rejected, Refused = 95 };
        LayoffPost(EmployerKind.City, "Gerardo Toombs").CopyTo(done.Post);
        w.Applications.Add(done);
        bool all = true;
        for (int i = 0; i < made.Count; i++)
        {
            all &= made[i].Live && ClosureRules.Shut(made[i]) && !made[i].Live && made[i].At == ApplyStep.Closed;
        }
        Check(all, "layoff: an application to a closed business, at whatever step, is over and no longer live (so no interview, offer or start follows)");
        Check(!ClosureRules.Shut(done) && done.Stage == (int)ApplyStep.Rejected && done.Refused == 95, "layoff: an application already over is left as it was");
        Check(!ClosureRules.Shut(made[0]), "layoff: an application is closed once");
        var late = new Application { Stage = (int)ApplyStep.Accepted, StartDay = 140 };
        Check(ClosureRules.StartSooner(late, 101) && late.StartDay == 101 + ProRules.StartDays && !ClosureRules.StartSooner(late, 101),
            "layoff: a job accepted to start when the old one's notice ran out starts soon after a lay off, the notice no longer running");
        var soon = new Application { Stage = (int)ApplyStep.Accepted, StartDay = 102 };
        Check(!ClosureRules.StartSooner(soon, 101) && soon.StartDay == 102 && !ClosureRules.StartSooner(new Application { Stage = (int)ApplyStep.Offered, StartDay = 140 }, 101),
            "layoff: a start already near, or an offer not yet taken, is not moved");
    }

    // ---------------------------------------------------------------- the save

    private static void LayoffSave()
    {
        var g = new StudyFile { Seed = 3 };
        WorkFile w = g.Work;
        Job j = LayoffJob(100);
        w.Job = j;
        ClosureRules.Close(w, j, 130, 60, out _);
        var app = new Application { Day = 120, Stage = (int)ApplyStep.Closed, Answer = 121 };
        LayoffPost(EmployerKind.City, "Gerardo Toombs").CopyTo(app.Post);
        w.Applications.Add(app);
        string line = CareersStore.Write(g);
        StudyFile back = CareersStore.Read(line, out int version);
        Check(CareersStore.Write(back) == line && back.Work.LaidOffDay == 130 && back.Work.LaidOffFrom == "Gerardo Toombs" && back.Work.Applications.Count == 1
              && back.Work.Applications[0].Stage == (int)ApplyStep.Closed && !back.Work.Applications[0].Live && back.Work.History.Count == 1
              && back.Work.History[0].Leaving == (int)LeaveReason.EmployerClosed && back.Work.History[0].Reference == 3,
            "layoff: the day, the employer, a closed application and the CV line come back from the save");
        Check(ClosureRules.Priority(back.Work, 140) && !ClosureRules.Priority(back.Work, 160), "layoff: the priority carries across a save");
        // a save from before: the careers line's header holds neither field
        int cut = line.IndexOf("~o:", StringComparison.Ordinal);
        string head = line.Substring(0, cut), rest = line.Substring(cut);
        int lastBar = head.LastIndexOf('|'), before = head.LastIndexOf('|', lastBar - 1);
        string old = head.Substring(0, before) + rest;
        StudyFile was = CareersStore.Read(old, out version);
        Check(was.Work.LaidOffDay == 0 && was.Work.LaidOffFrom.Length == 0 && was.Work.History.Count == 1 && !ClosureRules.Priority(was.Work, 140) && !ClosureRules.Shown(was.Work, 140),
            "layoff: a save from before this loads, as never laid off");
        var gone = new StudyFile { Seed = 4 };
        gone.Work.Job = LayoffJob(100);
        gone.Work.Job.NoticeEnd = 120;
        gone.Work.Job.Leaving = (int)LeaveReason.Resigned;
        StudyFile kept = CareersStore.Read(CareersStore.Write(gone), out version);
        Check(kept.Work.Job != null && kept.Work.Job.Post.Employer == "Gerardo Toombs" && kept.Work.LaidOffDay == 0,
            "layoff: a job whose employer has gone loads as the job it was; the look at the load (CareersDirector.StartWork) is what repairs it, with the city in hand");
    }

    // ---------------------------------------------------------------- a player who quit, or was sacked, because the employer had closed

    private static Spell LayoffSpell(LeaveReason why, int from, int to, int reference, string employer = "Gerardo Toombs", EmployerKind kind = EmployerKind.City)
    {
        return new Spell { Prof = ProCatalogue.Accountant, Top = 1, Kind = (int)kind, Employer = employer, From = from, To = to, Reference = reference, Leaving = (int)why, Earned = 900 };
    }

    private static void LayoffMend()
    {
        // what barred them: a poor reference takes ten points off the hire score of every application, and a fair one gives nothing
        Profession p = LayoffProfession;
        RealLifeOverhaul.Careers.Candidate c = new RealLifeOverhaul.Careers.Candidate { Qualified = true, Points = 40, Need = 40, Rank = 1, Cycle = 1 };
        var w = new WorkFile();
        Spell sacked = LayoffSpell(LeaveReason.Dismissed, 100, 108, 1);
        w.History.Add(sacked);
        ProRules.References(w, out c.Good, out c.Poor);
        int before = ProRules.HireScore(c);
        Check(c.Poor == 1 && before == 40 && !ProPath.CanBeOffered(before),
            "layoff, quit: after a sacking for missed days a poor reference leaves an application at " + before + ", too low for even the best luck at both steps: the Jobs page offers nothing (Apply says there is no chance)");
        // the case: the business closed on day 103, the player went on missing days and was sacked on 108
        Check(ClosureRules.Mends(sacked, 103, false, 110), "layoff, quit: a sacking that came after the business closed is a redundancy");
        w.RestraintUntil = 140;
        w.RestraintProf = ProCatalogue.Accountant;
        w.RestraintEmployer = "Gerardo Toombs";
        w.RestraintWeek = 5000;
        Check(ClosureRules.Mend(w, sacked, 110) && sacked.Leaving == (int)LeaveReason.EmployerClosed && sacked.Reference == 3, "layoff, quit: it is put right on the CV, with a good reference");
        ProRules.References(w, out c.Good, out c.Poor);
        int after = ProRules.HireScore(c);
        Check(c.Poor == 0 && c.Good == 1 && after == 54 && ProPath.CanBeOffered(after) && after > before,
            "layoff, quit: the applications score " + after + " and can be offered again");
        Check(w.RestraintUntil == 0 && w.RestraintProf == -1 && w.RestraintEmployer.Length == 0 && w.RestraintWeek == 0, "layoff, quit: the restraint clause the quit set is lifted");
        Check(w.LaidOffDay == 108 && w.LaidOffFrom == "Gerardo Toombs" && ClosureRules.Priority(w, 110) && !ClosureRules.Priority(w, 108 + ClosureRules.PriorityDays + 1),
            "layoff, quit: someone who left within three weeks has the priority for the rest of them");
        Check(!ClosureRules.Mends(sacked, 103, false, 110) && !ClosureRules.Mend(w, sacked, 111), "layoff, quit: a job put right is not put right twice (the load can run again)");
        // a claim already filed over a restraint is a court's, not the CV's
        var filed = new WorkFile { RestraintUntil = 150, RestraintProf = ProCatalogue.Accountant, RestraintEmployer = "Gerardo Toombs", ClaimNumber = 7 };
        ClosureRules.Mend(filed, LayoffSpell(LeaveReason.Resigned, 100, 108, 2), 110);
        Check(filed.RestraintUntil == 150 && filed.ClaimNumber == 7, "layoff, quit: a restraint with a claim filed over it is left to the court");
        var other = new WorkFile { RestraintUntil = 150, RestraintProf = ProCatalogue.Accountant, RestraintEmployer = "Harbour Law" };
        ClosureRules.Mend(other, LayoffSpell(LeaveReason.Resigned, 100, 108, 2), 110);
        Check(other.RestraintUntil == 150, "layoff, quit: another employer's restraint is not touched");
        // which jobs are put right
        Check(ClosureRules.Mends(LayoffSpell(LeaveReason.Resigned, 100, 108, 2), 105, false, 200) && ClosureRules.Mends(LayoffSpell(LeaveReason.WalkedOut, 100, 108, 1), 100, false, 200)
              && ClosureRules.Mends(LayoffSpell(LeaveReason.WalkedOut, 100, 108, 1), 108, false, 200), "layoff, quit: a resignation, a sacking or a walk out with the closing inside the job, its first and last day included");
        Check(!ClosureRules.Mends(LayoffSpell(LeaveReason.Resigned, 100, 108, 2), 109, false, 200) && !ClosureRules.Mends(LayoffSpell(LeaveReason.Resigned, 100, 108, 2), 99, false, 200),
            "layoff, quit: a quit before the business closed, or a closing before the job began, is the player's own");
        Check(!ClosureRules.Mends(LayoffSpell(LeaveReason.Misconduct, 100, 108, 1), 105, true, 110) && !ClosureRules.Mends(LayoffSpell(LeaveReason.Poached, 100, 108, 3), 105, true, 110)
              && !ClosureRules.Mends(LayoffSpell(LeaveReason.Retired, 100, 108, 3), 105, true, 110) && !ClosureRules.Mends(LayoffSpell(LeaveReason.Redundant, 100, 108, 3), 105, true, 110)
              && !ClosureRules.Mends(LayoffSpell(LeaveReason.SpunOut, 100, 108, 3), 105, true, 110),
            "layoff, quit: misconduct, a better offer, retiring, a cut and a spin out are never made over");
        Check(!ClosureRules.Mends(LayoffSpell(LeaveReason.Resigned, 100, 108, 2, "Northlight", EmployerKind.TechFirm), 105, true, 110)
              && !ClosureRules.Mends(LayoffSpell(LeaveReason.Resigned, 100, 108, 2, "@reallife_work_employer_health", EmployerKind.Public), 105, true, 110),
            "layoff, quit: a tech firm and the public sector do not close");
        Spell nothing = LayoffSpell(LeaveReason.Dismissed, 100, 108, 1);
        Check(ClosureRules.Mends(nothing, -1, true, 108 + ClosureRules.RepairDays) && !ClosureRules.Mends(nothing, -1, true, 108 + ClosureRules.RepairDays + 1)
              && !ClosureRules.Mends(nothing, -1, false, 110) && !ClosureRules.Mends(nothing, -1, true, 107),
            "layoff, quit: with no closing recorded, a job left within two months by someone whose employer is gone now is made over, and one whose employer still trades is not");
    }

    // ---------------------------------------------------------------- the source

    private static void LayoffSources()
    {
        string closing = ModSource(@"Careers\CareersDirector.Closing.cs"), tests = ModSource(@"Careers\CareersDirector.ClosingTests.cs"), work = ModSource(@"Careers\CareersDirector.Work.cs");
        string apply = ModSource(@"Careers\CareersDirector.Applying.cs"), own = ModSource(@"Careers\CareersDirector.Own.cs"), game = ModSource(@"Careers\Work\WorkGame.cs");
        string app = ModSource(@"Careers\CareerApp.cs"), life = ModSource(@"Careers\CareerApp.Life.cs"), jobs = ModSource(@"Careers\CareerApp.Jobs.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string rules = ModSource(@"Careers\Work\ProRules.cs"), text = ModSource(@"Careers\CareersText.cs");
        Check(closing.Length > 0 && tests.Length > 0 && work.Length > 0 && apply.Length > 0 && own.Length > 0 && game.Length > 0 && app.Length > 0 && life.Length > 0 && jobs.Length > 0
              && mod.Length > 0 && rules.Length > 0 && text.Length > 0, "layoff: the sources are read");
        string layOff = MethodText(closing, "private void LayOff(Job j, int day, bool load)");
        Check(layOff.Contains("ClosureRules.Close(w, j, day, Year, out long cents);") && layOff.Contains("CareersGame.Pay(whole, ClosedKey, null, employer);")
              && layOff.Contains("Prices.At((int)(cents / 100))") && layOff.Contains("DeskRelease(\"the employer closed\");") && layOff.Contains("EndedOwn(j, LeaveReason.EmployerClosed, day);")
              && layOff.Contains("LadderKeep(day);") && layOff.Contains("ClosureRules.StartSooner("),
            "layoff: the job ends through the closure rules; the pay is scaled by the price level and paid under its own transaction line; the desk, the ladder's company and a job accepted elsewhere are dealt with");
        Check(!layOff.Contains("LeaveReason.Resigned") && !layOff.Contains("LeaveReason.Dismissed") && !layOff.Contains("LeaveReason.WalkedOut") && !layOff.Contains("Warnings++")
              && !layOff.Contains("EndJob(") && !layOff.Contains("Resign(") && !layOff.Contains("WalkOut("), "layoff: nothing on the way is a quit, a walk out, a sacking or a mark");
        Check(layOff.Contains("DeskGame.BossText(employer") && layOff.Contains("\"reallife_work_text_closed\"") && layOff.Contains("Tell2(NotificationType.Warning, \"reallife_work_note_closed\""),
            "layoff: the employer's text and one notice");
        string keep = MethodText(closing, "private bool EmployerKeep(int day, bool load)");
        Check(keep.Contains("LayOff(j, day, load);") && keep.Contains("MoveJob(j, to, day);") && keep.Contains("FollowOrClose(a, day)") && keep.Contains("w.Openings.RemoveAt(i);")
              && keep.IndexOf("MovedTo(j.Post, day)", StringComparison.Ordinal) < keep.IndexOf("LayOff(j, day, load);", StringComparison.Ordinal),
            "layoff: the look moves the job with an employer that moved before it lays anyone off, closes applications and takes closed vacancies off the board");
        Check(MethodText(closing, "private void MoveJob(Job j, EmployerRef to, int day)").Contains("DeskRelease(\"the employer moved\");")
              && MethodText(closing, "private void MoveJob(Job j, EmployerRef to, int day)").Contains("j.Post.Street = to.Street;")
              && MethodText(closing, "private void MoveJob(Job j, EmployerRef to, int day)").Contains("\"reallife_work_text_moved\""),
            "layoff: a job that moves goes to the new address, its desk with it, and the employer says so");
        string startWork = MethodText(work, "private void StartWork()");
        Check(startWork.Contains("EmployerKeep(Save.Day, true);"), "layoff: at every load a job whose employer is gone is repaired the same way");
        string mend = MethodText(closing, "private int MendSpells(int day)");
        Check(startWork.Contains("MendSpells(Save.Day);") && mend.Contains("WorkGame.ClosedOn(s.Employer, s.From, s.To)") && mend.Contains("ClosureRules.Mends(s, closed, gone, day)") && mend.Contains("ClosureRules.Mend(w, s, day);")
              && mend.Contains("Logger?.Info(\"Careers: \" + mended") && !mend.Contains("CareersGame.Pay("),
            "layoff, quit: at every load the CV's quits and sackings after a closing are made over, with one line in Player.log, and nothing is paid back");
        Check(MethodText(game, "public static int ClosedOn(string name, int from, int to)").Contains("Entities.MarketEventType.BusinessClosed"),
            "layoff, quit: the closing is the game's own record of it");
        string hour = MethodText(work, "private void WorkHour(int day, int hour)");
        Check(hour.Contains("EmployerKeep(day, false);") && hour.IndexOf("EmployerKeep(day, false);", StringComparison.Ordinal) < hour.IndexOf("SettleDay(j, day);", StringComparison.Ordinal),
            "layoff: the hour looks before it settles a day, so a closed employer's day is never a missed one");
        string dayRun = MethodText(work, "private void WorkDayRun(int day)");
        Check(dayRun.Contains("EmployerKeep(day, false);") && dayRun.IndexOf("EmployerKeep(day, false);", StringComparison.Ordinal) < dayRun.IndexOf("SettleDay(j, d);", StringComparison.Ordinal)
              && work.Contains("ClosureRules.InterviewAfter(w, day)"),
            "layoff: each morning looks before it settles the days gone by; an invitation to someone laid off names the next day");
        Check(MethodText(apply, "public string Apply(int opening)").Contains("if (Gone(o))") && apply.Contains("ClosureRules.AnswerDay(w, day, "),
            "layoff: a vacancy at a business that closed since the board was read is refused, and someone laid off is answered first");
        Check(MethodText(own, "private void EndedOwn(Job j, LeaveReason why, int day)").Contains("ClosureRules.Restrains(why)"), "layoff: no restraint clause binds someone whose employer closed");
        Check(MethodText(rules, "public static int Reference(Job j, int day, LeaveReason why)").Contains("LeaveReason.EmployerClosed") && text.Contains("why > 9 ? 9 : why"),
            "layoff: the reference rule and the CV's wording know the new reason");
        string premises = MethodText(game, "public static bool PremisesAt(string street, int number, out Premises at)");
        Check(premises.Length > 0 && !premises.Contains("GetBuildingRegistration(") && premises.Contains("GameRegistrations.Found(reg)") && premises.Contains("return false;"),
            "layoff: the city is read from the save's own list (the game's lookup makes a registration for a building that has none and throws for one that is gone), and nothing is judged where the city cannot be told");
        Check(life.Contains("ShowTab(CareersDirector.TabJobs)") && life.Contains("reallife_career_laid_off") && app.Contains("ClosureRules.Shown(w, day)") && app.Contains("LaidOffNote(box, w, day);")
              && jobs.Contains("ClosureRules.Priority(w, day)"),
            "layoff: the Career app says who the player was laid off by, with a button to the open roles, and the Jobs page says the priority");
        string closedTest = MethodText(tests, "public void TestClosed()"), movedTest = MethodText(tests, "public void TestMoved()");
        Check(mod.Contains(".AddButton(\"reallife_work_test_closed\", () => Careers.CareersDirector.Instance?.TestClosed())")
              && mod.Contains(".AddButton(\"reallife_work_test_moved\", () => Careers.CareersDirector.Instance?.TestMoved())")
              && closedTest.Contains("\"Careers check (employer closes): ") && closedTest.Contains("(ok ? \"PASS\" : \"FAIL\")") && closedTest.Contains("WorkGame.TestShut = t.At;")
              && closedTest.Contains("EmployerKeep(day, false)") && closedTest.Contains("s.Reference == 3") && closedTest.Contains("barredAfter == barredBefore")
              && movedTest.Contains("\"Careers check (employer moves): ") && movedTest.Contains("(ok ? \"PASS\" : \"FAIL\")") && movedTest.Contains("_testMoveTo = there;"),
            "layoff: Options > Tests hires the player into a test job, closes or moves the employer through the real look, checks the status, the mark, the pay, the CV, the wait and the priority, and writes PASS or FAIL to Player.log");
    }

    // ---------------------------------------------------------------- the texts

    private static void LayoffTexts()
    {
        var keys = new[]
        {
            "reallife_transaction_work_closed", "reallife_work_leaving_employerclosed", "reallife_work_step_closed", "reallife_work_note_closed", "reallife_work_text_closed",
            "reallife_work_text_moved", "reallife_work_note_app_closed", "reallife_career_laid_off", "reallife_career_laid_off_priority", "reallife_career_button_open_roles",
            "reallife_work_test_closed", "reallife_work_test_closed_pass", "reallife_work_test_closed_fail", "reallife_work_test_closed_busy", "reallife_work_test_moved",
            "reallife_work_test_moved_pass", "reallife_work_test_moved_fail"
        };
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        var lacking = new List<string>();
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> own = LoadWords(System.IO.Path.Combine(LocalesFolder(), code + ".json"));
            foreach (string key in keys)
            {
                if (own == null || en == null || !own.TryGetValue(key, out string words) || words.Length == 0 || !en.TryGetValue(key, out string english)
                    || !SamePlaceholders(words, english) || (code == "en" && words.Length > 240) || words.Contains("  ") || words.Contains("!") || words.Contains("—") || words.Contains("–"))
                {
                    lacking.Add(code + " " + key);
                }
            }
        }
        Check(lacking.Count == 0, "layoff: the notices, the texts and the tests' buttons are in en, pt and zh-cn with the same placeholders, no dash, no exclamation mark, no double space and each English notice 240 characters at most (lacking: "
                                  + string.Join(", ", lacking) + ")");
        Check(en != null && en.TryGetValue("reallife_guide_careers_life", out string guide) && guide.Contains("you are laid off within the hour: redundancy pay"),
            "layoff: the Guide's careers page says what happens when the employer closes");
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("layoff", langs, codes))
        {
            return;
        }
        var lines = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["en"] = "Fixed: when the business you work for closes, is bought or loses its building, you are now laid off at once with redundancy pay and a good reference, with no quit, sacking or missed day against you and no wait before you apply again; if it moves, your job moves with it. A quit or sacking that followed a closing is put right as a redundancy when you load.",
            ["pt"] = "Corrigido: quando a empresa onde você trabalha fecha, é comprada ou perde o prédio, seu emprego termina na hora com indenização e uma boa referência, sem demissão, pedido de saída ou falta contra você e sem espera para se candidatar de novo; se ela se mudar, seu emprego vai junto. Uma saída ou demissão que veio depois de um fechamento é corrigida como dispensa ao carregar o jogo.",
            ["zh-cn"] = "已修复：你工作的企业倒闭、被收购或失去楼宇时，你会当场被裁员并获得补偿金和良好推荐，不记辞职、解雇或缺勤，再次申请也无需等待；企业搬迁时，你的工作随之搬迁。关闭之后发生的辞职或解雇，在载入存档时会改记为裁员。",
            ["de"] = "Behoben: Wenn ein Betrieb schließt, verkauft wird oder sein Gebäude verliert, endet der Job dort sofort mit Abfindung und gutem Zeugnis, ohne Kündigung, Rauswurf oder Fehltag und ohne Wartezeit vor neuen Bewerbungen; zieht der Betrieb um, zieht der Job mit. Eine Kündigung oder ein Rauswurf nach einer Schließung wird beim Laden als Entlassung berichtigt.",
            ["fr"] = "Corrigé : quand l'entreprise où l'on travaille ferme, est rachetée ou perd son bâtiment, l'emploi prend fin aussitôt avec une indemnité et une bonne référence, sans démission, renvoi ni absence au dossier et sans attente avant de postuler de nouveau ; si elle déménage, l'emploi la suit. Une démission ou un renvoi survenu après une fermeture est corrigé en licenciement au chargement.",
            ["es"] = "Corregido: cuando el negocio donde se trabaja cierra, lo compran o pierde su edificio, el empleo termina al instante con indemnización y una buena referencia, sin renuncia, despido ni falta en contra y sin espera para volver a postularse; si se muda, el empleo se va con él. Una renuncia o un despido posterior a un cierre se corrige como despido por cierre al cargar la partida.",
            ["ru"] = "Исправлено: когда заведение, где вы работаете, закрывается, продаётся или теряет здание, работа сразу заканчивается с выходным пособием и хорошей рекомендацией, без увольнения, ухода или прогула в вашем деле и без ожидания перед новыми заявками; если заведение переезжает, работа переезжает вместе с ним. Увольнение или уход после закрытия при загрузке исправляются на сокращение.",
            ["nl"] = "Opgelost: sluit het bedrijf waar je werkt, wordt het gekocht of verliest het zijn pand, dan eindigt de baan meteen met ontslagvergoeding en een goede referentie, zonder ontslag, opzegging of gemiste dag op je naam en zonder wachttijd voor nieuwe sollicitaties; verhuist het bedrijf, dan verhuist de baan mee. Een ontslag of opzegging na een sluiting wordt bij het laden rechtgezet als ontslag door sluiting.",
            ["tr"] = "Düzeltildi: çalıştığınız iş yeri kapanırsa, satın alınırsa ya da binasını kaybederse iş hemen kıdem tazminatı ve iyi bir referansla biter; istifa, kovulma ya da devamsızlık kaydı düşmez ve yeniden başvurmak için beklenmez; iş yeri taşınırsa iş de onunla taşınır. Bir kapanıştan sonraki istifa ya da kovulma, yükleme sırasında işten çıkarma olarak düzeltilir."
        };
        var missing = new List<string>();
        for (int l = 0; l < codes.Length; l++)
        {
            if (!lines.TryGetValue(codes[l], out string wanted) || !WhatsNewHas(langs[l], wanted))
            {
                missing.Add(codes[l]);
            }
        }
        Check(missing.Count == 0 && codes.Length == lines.Count,
            "layoff: What's new has the fix's line, with its language's fix mark, in some reallife_changes_ row of every language (lacking: " + string.Join(", ", missing) + ")");
    }
}
