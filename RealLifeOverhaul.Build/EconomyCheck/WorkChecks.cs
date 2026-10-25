using System;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Tech;

// Checks for the working life (Scripts/Careers/Work): the professions' ladders and pay, what a rank asks, the employer's
// score, the interview and the offer, haggling, the working day and what it pays, settling the days, notice and
// references, the city's vacancies, and the save's line.
internal static partial class Program
{
    private static void Work()
    {
        WorkLadders();
        WorkAsks();
        WorkHiring();
        WorkDays();
        WorkLeaving();
        WorkVacancies();
        WorkSave();
        WorkReapply();
        WorkPath();
        WorkAppliedAgain();
    }

    private static void WorkLadders()
    {
        bool ids = true, pay = true, need = true, roles = true, quals = true, certs = true, hours = true, exec = true, types = true;
        for (int i = 0; i < ProCatalogue.All.Length; i++)
        {
            Profession p = ProCatalogue.All[i];
            ids &= p.Id == i && ProCatalogue.Find(i) == p;
            hours &= p.Days != 0 && p.Hours >= 6 && p.Hours <= 10 && p.Hour + p.Hours <= 22 && p.Hour >= 6;
            types &= p.Public == (p.Place == Workplace.Hospital || p.Place == Workplace.School || p.Place == Workplace.CityHall);
            bool topExec = false;
            for (int r = 0; r < p.Ranks.Length; r++)
            {
                ProRank k = p.Ranks[r];
                pay &= k.Pay > 0 && (r == 0 || k.Pay > p.Ranks[r - 1].Pay);
                need &= k.Need >= 0 && k.Need <= 100;
                roles &= k.Role < TalentCatalogue.RoleCount && k.Level >= 0 && k.Level < TalentCatalogue.Levels;
                foreach (int q in k.Quals)
                {
                    quals &= StudyCatalogue.Find(q) != null;
                }
                certs &= k.Cert < TalentCatalogue.CertCount && (k.Cert < 0 || StudyCatalogue.OfCert(k.Cert) != null);
                exec &= !topExec || k.Exec;   // once a ladder reaches the top seats it stays there
                topExec |= k.Exec;
            }
        }
        Check(ProCatalogue.All.Length == ProCatalogue.Count, "work: the professions' count is the table's");
        Check(ids, "work: every profession's id is its place in the table");
        Check(pay, "work: every rung pays more than the one below");
        Check(need, "work: what a rung asks is a number of points");
        Check(roles, "work: every rung stands for a role and a level of the professions");
        Check(quals, "work: every qualification a rung needs is a course of the table");
        Check(certs, "work: every certificate a rung needs can be studied for");
        Check(hours, "work: every working day is six to ten hours between 06:00 and 22:00");
        Check(exec, "work: the top seats are the end of a ladder");
        Check(types, "work: a profession without business types is the public sector's");
        Profession dev = ProCatalogue.Find(ProCatalogue.Developer);
        Check(ProCatalogue.Employs(dev, "ba:businesstype_webdevelopmentagency") && ProCatalogue.Employs(dev, "reallife:businesstype_softwarestudio") && !ProCatalogue.Employs(dev, "ba:businesstype_supermarket"),
            "work: developers are taken on by web agencies and the tech sector's own firms, not supermarkets");
        Check(ProCatalogue.OfFamily((int)Family.Engineering, 0) == ProCatalogue.Developer && ProCatalogue.OfFamily((int)Family.Infrastructure, 0) == ProCatalogue.ItSupport
              && ProCatalogue.OfFamily((int)Family.Count, 0) == -1, "work: an ad of the jobs board finds its profession by family");
        Check(ProCatalogue.RankOfLevel(dev, TalentCatalogue.Senior) == 2 && ProCatalogue.RankOfLevel(dev, TalentCatalogue.Junior) == 0, "work: an ad's level finds its rung");
        int a, b;
        dev.SkillsOf(5, out a, out b);
        Check(a == TalentCatalogue.Leadership && b == TalentCatalogue.Delivery, "work: an engineering manager is judged on leadership and delivery");
        Profession nurse = ProCatalogue.Find(ProCatalogue.Nurse);
        nurse.SkillsOf(1, out a, out b);
        Check(a == Skills.Clinical && b == Skills.Care, "work: a nurse is judged on clinical care and care");
        long week = (long)ProCatalogue.Find(ProCatalogue.Developer).Ranks[2].Pay * 40 / 100;
        long top = (long)ProCatalogue.Find(ProCatalogue.Manager).Ranks[6].Pay * 40 / 100;
        Check(week >= 9000 && week <= 11000 && top >= 34000 && top <= 42000, "work: a senior developer's week is about 10,000, a chief executive's about 38,000 (" + week + ", " + top + ")");
        int bar = ProCatalogue.Find(ProCatalogue.BarStaff).Ranks[0].Pay;
        Check(bar >= 6500 && bar <= 8000, "work: the first rung of a bar, after the barista's short course, is about the drinks cart's best hour (" + bar + " cents)");
    }

    private static void WorkAsks()
    {
        var f = new StudyFile();
        Profession dev = ProCatalogue.Find(ProCatalogue.Developer), law = ProCatalogue.Find(ProCatalogue.Lawyer);
        Check(!ProRules.Qualified(f, law, 0) && ProRules.Qualified(f, dev, 0), "work: law needs its degree; a developer may come self-taught");
        Check(ProRules.Need(f, dev, 0) == dev.Ranks[0].Need + ProRules.SelfTaughtExtra, "work: self-taught, the first rung asks more");
        f.Results.Add(new Enrolment { Course = StudyCatalogue.CodingBootcamp, Stage = (int)EnrolStage.Passed, Grade = 2 });
        Check(ProRules.Need(f, dev, 0) == dev.Ranks[0].Need, "work: a bootcamp is a developer's qualification");
        StudyRules.Learn(f, TalentCatalogue.Coding, 30000, 1);
        StudyRules.Learn(f, TalentCatalogue.SystemDesign, 20000, 1);
        Check(ProRules.Points(f, dev, 0) == (30 * 3 + 20 * 2) / 5, "work: a rung's points are its first skill three fifths and its second two");
        Check(ProRules.Eligible(f, dev, 0) && ProRules.BestRank(f, dev, 0) == 0, "work: with a bootcamp and some coding, a junior developer's post");
        Check(!ProRules.Qualified(f, ProCatalogue.Find(ProCatalogue.SecurityPro), 2), "work: a senior security post needs its certificate");
        f.Results.Add(new Enrolment { Course = StudyCatalogue.Law, Stage = (int)EnrolStage.Passed, Grade = 3 });
        Check(ProRules.Qualified(f, law, 0) && !ProRules.Qualified(f, law, 1) && !ProRules.Qualified(f, law, 2), "work: a law degree makes a paralegal; a trainee needs the practice course, an associate the bar");
        f.Results.Add(new Enrolment { Course = StudyCatalogue.LegalPractice, Stage = (int)EnrolStage.Passed, Grade = 2 });
        f.Results.Add(new Enrolment { Course = StudyCatalogue.BarExam, Stage = (int)EnrolStage.Passed, Grade = 2 });
        Check(ProRules.Qualified(f, law, 4), "work: with the bar, every rung of law is open");
        Check(ProRules.YearsDays(dev, 0) == 0 && ProRules.YearsDays(dev, 2) == (dev.Ranks[1].Days + dev.Ranks[2].Days) / 2, "work: coming in high needs half the years of rising inside");
        Check(ProRules.SelfTaught(dev) && !ProRules.SelfTaught(law) && !ProRules.SelfTaught(ProCatalogue.Find(ProCatalogue.Nurse)), "work: law and nursing take nobody self-taught");
    }

    private static void WorkHiring()
    {
        var c = new Candidate { Points = 40, Need = 30, Qualified = true, Cycle = 1 };
        int base0 = ProRules.HireScore(c);
        Check(base0 == 60, "work: ten points over the post scores sixty (" + base0 + ")");
        c.Qualified = false;
        Check(ProRules.HireScore(c) == 0, "work: unqualified scores nothing");
        c.Qualified = true;
        c.Points = 25;
        Check(ProRules.HireScore(c) == 35, "work: under the post costs three a point");
        c.Points = 40;
        c.Referral = true;
        c.Agency = true;
        Check(ProRules.HireScore(c) == 75, "work: a friend's word and the recruitment office count");
        c.Referral = c.Agency = false;
        c.Record = true;
        Check(ProRules.HireScore(c) == 45, "work: a record costs fifteen");
        c.Record = false;
        c.Cycle = 3;
        Check(ProRules.HireScore(c) == 48, "work: a recession makes employers choosier");
        c.Cycle = 1;
        c.Good = 5;
        c.Poor = 1;
        Check(ProRules.HireScore(c) == 58, "work: good references count to eight at most, a poor one ten off");
        c.Good = c.Poor = 0;
        c.Experience = 10000;
        Check(ProRules.HireScore(c) == 72, "work: experience counts to twelve at most");
        Check(ProRules.Screened(50, 0) && !ProRules.Screened(49, 0) && ProRules.Screened(45, 10) && !ProRules.Screened(30, 99), "work: screening is the score and the day's luck, bounded");
        Check(ProRules.Offered(60, 0) && !ProRules.Offered(59, 0) && !ProRules.Offered(0, 15), "work: an offer needs sixty with the interview's luck");
        Check(ProRules.OfferPay(10000, 60) == 9500 && ProRules.OfferPay(10000, 100) == 10700 && ProRules.OfferPay(10000, 200) == 11000 && ProRules.OfferPay(10000, 75) == 9950, "work: the offer is the post's rate, a little more for a strong candidate");
        Check(ProRules.AskChance(90, ProRules.AskSmallBp, 0) > ProRules.AskChance(60, ProRules.AskSmallBp, 0) && ProRules.AskChance(80, ProRules.AskLargeBp, 0) < ProRules.AskChance(80, ProRules.AskSmallBp, 0)
              && ProRules.AskChance(80, ProRules.AskSmallBp, 1) < ProRules.AskChance(80, ProRules.AskSmallBp, 0), "work: asking is likelier to work for a strong candidate, a small ask and the first time");
        Check(ProRules.AskChance(0, 5000, 5) == 5 && ProRules.AskChance(100, 0, 0) == 90, "work: an ask is never sure either way");
        Check(!ProRules.Withdraws(ProRules.AskSmallBp, 0, 0) && ProRules.Withdraws(ProRules.AskLargeBp, 0, 100) && !ProRules.Withdraws(ProRules.AskLargeBp, 0, 500), "work: only a large or a second ask can lose the offer");
        bool luck = true;
        for (int i = 0; i < 300; i++)
        {
            int l = ProRules.Luck(99, i, 1, ProRules.ScreenLuck);
            luck &= l >= -ProRules.ScreenLuck && l <= ProRules.ScreenLuck;
        }
        Check(luck, "work: an application's luck stays in its bounds");
        Profession dev = ProCatalogue.Find(ProCatalogue.Developer);
        Check(ProRules.PostPay(dev, 0, EmployerKind.TechFirm, 1000) == dev.Ranks[0].Pay * 1100 / 1000 && ProRules.PostPay(dev, 0, EmployerKind.Public, 1000) < dev.Ranks[0].Pay
              && ProRules.PostPay(dev, 0, EmployerKind.City, 1300) == dev.Ranks[0].Pay * 1300 / 1000, "work: what a post pays follows the employer and the market's shortage");
    }

    private static void WorkDays()
    {
        Profession dev = ProCatalogue.Find(ProCatalogue.Developer);   // weekdays 09:00 for 8 hours
        int minutes;
        bool late;
        Check(ProRules.WorkDay(dev, 1) && !ProRules.WorkDay(dev, 6) && !ProRules.WorkDay(dev, 7), "work: a developer works weekdays");
        Check(!ProRules.CanStart(dev, 1, 9 * 60 - ProRules.ClockEarlyMinutes - 1, out minutes, out late), "work: not too early");
        Check(ProRules.CanStart(dev, 1, 8 * 60 + 30, out minutes, out late) && minutes == 8 * 60 && !late, "work: early, the whole day");
        Check(ProRules.CanStart(dev, 1, 9 * 60 + 30, out minutes, out late) && minutes == 8 * 60 - 30 && late, "work: half an hour after, late, and the rest of the day");
        Check(!ProRules.CanStart(dev, 1, 9 * 60 + ProRules.ClockLateMinutes + 1, out minutes, out late), "work: too late to start");
        var j = new Job { Hired = 1, SettledDay = 0 };
        new Opening { Prof = ProCatalogue.Developer, Rank = 0 }.CopyTo(j.Post);
        ProRules.Worked(j, 1, 300);
        Check(ProRules.Remaining(dev, j, 1, 14 * 60) == 180, "work: what is left of a day already begun");
        ProRules.Worked(j, 1, 120);
        Check(ProRules.Settle(j, 1) && j.Worked == 1 && j.Due == 1, "work: seven hours of eight is a day worked");
        Check(!ProRules.Settle(j, 1) && j.Due == 1, "work: a day is settled once");
        ProRules.Worked(j, 2, 200);
        Check(!ProRules.Settle(j, 2) && j.Absent == 1, "work: too little of a day is a missed day");
        Check(!ProRules.Settle(j, 6) && j.Due == 2, "work: a day off is nothing");
        Check(ProRules.Attendance(j) == 500, "work: attendance is the share of days worked");
        Check(ProRules.DayOver(dev, 1, 1, 17 * 60) && !ProRules.DayOver(dev, 1, 1, 16 * 60 + 59) && ProRules.DayOver(dev, 1, 2, 0), "work: a day is over at its last hour");
        Check(ProRules.Wage(6000, 90) == 9000 && ProRules.Wage(6000, -5) == 0, "work: pay is by the minute");
        Check(ProRules.FromHome(WorkMode.Remote, 3) && ProRules.FromHome(WorkMode.Hybrid, 1) && ProRules.FromHome(WorkMode.Hybrid, 5) && !ProRules.FromHome(WorkMode.Hybrid, 3)
              && !ProRules.FromHome(WorkMode.OnSite, 1), "work: remote is every day at home, hybrid Mondays and Fridays");
        var w = new Job { Hired = 1 };
        ProRules.Worked(w, 6, 100);
        ProRules.Worked(w, 7, 100);
        ProRules.Worked(w, 8, 100);
        Check(w.WeekMinutes == 100 && w.WeekOf == 1, "work: the week starts again each seventh day");
    }

    private static void WorkLeaving()
    {
        Profession dev = ProCatalogue.Find(ProCatalogue.Developer);
        Check(ProRules.NoticeDays(dev.Ranks[0], 0) == 7 && ProRules.NoticeDays(dev.Ranks[2], 2) == 14 && ProRules.NoticeDays(dev.Ranks[6], 6) == 28, "work: notice grows with the rung");
        var j = new Job { Hired = 10, Due = 20, Worked = 19 };
        new Opening { Prof = ProCatalogue.Developer, Rank = 1, Employer = "Codeharbour Software" }.CopyTo(j.Post);
        Check(ProRules.Reference(j, 60, LeaveReason.Resigned) == 3, "work: a good reference for good service");
        Check(ProRules.Reference(j, 15, LeaveReason.Resigned) == 2, "work: a fair one for a short stay");
        Check(ProRules.Reference(j, 60, LeaveReason.WalkedOut) == 1 && ProRules.Reference(j, 60, LeaveReason.Dismissed) == 1, "work: a poor one for walking out or being dismissed");
        j.Worked = 10;
        Check(ProRules.Reference(j, 60, LeaveReason.Resigned) == 2, "work: a fair one for patchy attendance");
        var w = new WorkFile();
        w.Remember(ProRules.Spell(j, 70, LeaveReason.Resigned));
        w.Remember(new Spell { Prof = ProCatalogue.Lawyer, From = 0, To = 30, Reference = 1 });
        int good, poor;
        ProRules.References(w, out good, out poor);
        Check(good == 0 && poor == 1 && w.History[0].To == 70, "work: references are counted from the CV");
        Check(ProRules.Experience(w, ProCatalogue.Developer, 100, 365) == 60 * 365 / LifeRules.PaceYearMost && ProRules.Experience(w, ProCatalogue.Developer, 100, 60) == 60 * 365 / 60,
            "work: experience is in days of a 365-day year, at the careers' pace (a year of 60 to " + LifeRules.PaceYearMost + " days)");
        for (int i = 0; i < WorkFile.HistoryMost + 5; i++)
        {
            w.Remember(new Spell { Prof = 0 });
        }
        Check(w.History.Count == WorkFile.HistoryMost, "work: the CV keeps its most recent jobs");
    }

    private static void WorkVacancies()
    {
        Check(ProRules.OpeningsWanted(0, false) > ProRules.OpeningsWanted(3, false) && ProRules.OpeningsWanted(1, true) == ProRules.OpeningsWanted(1, false) + ProRules.OpeningsAgency,
            "work: more vacancies in a boom and with the recruitment office");
        Profession dev = ProCatalogue.Find(ProCatalogue.Developer), bar = ProCatalogue.Find(ProCatalogue.BarStaff);
        var seen = new int[8];
        bool neverTop = true;
        for (int roll = 0; roll < 1000; roll++)
        {
            int r = ProRules.RankRoll(dev, roll);
            seen[r]++;
            neverTop &= !dev.Ranks[r].Exec && ProRules.RankRoll(bar, roll) < bar.Ranks.Length;
        }
        Check(neverTop && seen[0] > seen[1] && seen[1] > seen[2] && seen[4] > 0, "work: vacancies are mostly first rungs, a few high ones, never a top seat");
        int remote = 0, onsite = 0;
        for (int roll = 0; roll < 1000; roll++)
        {
            remote += ProRules.ModeRoll(dev, roll) == WorkMode.Remote ? 1 : 0;
            onsite += ProRules.ModeRoll(ProCatalogue.Find(ProCatalogue.Nurse), roll) == WorkMode.OnSite ? 1 : 0;
        }
        Check(remote == 200 && onsite == 1000, "work: a fifth of developers' vacancies are remote; nursing never is");
        var o = new Opening { Prof = 0, Rank = 1, Employer = "A", Street = "Main", Number = 3 };
        var p = new Opening();
        o.CopyTo(p);
        Check(o.Same(p) && o.Open(o.From) && !o.Open(o.To + 1), "work: a vacancy copies whole and is the same post");
    }

    private static void WorkSave()
    {
        var f = new StudyFile { Seed = 5 };
        WorkFile w = f.Work;
        w.NextId = 40;
        w.AgencyUntil = 33;
        w.Earned = 987654;
        w.DaysWorked = 12;
        w.StartTold = true;
        w.Openings.Add(new Opening { Id = 3, Prof = ProCatalogue.Lawyer, Rank = 2, Kind = (int)EmployerKind.Rival, Employer = "Firm; with|odd~chars", Street = "Elm St", Number = 12, Rival = "r1", Pay = 6000,
            Work = 1, From = 10, To = 20, Via = (int)Via.JobsBoard, Family = 8, Level = 1 });
        var a = new Application { Day = 11, Stage = (int)ApplyStep.Offered, Answer = 12, InterviewDay = 14, InterviewHour = 11, Score = 71, Offer = 6100, Asks = 1, Lapse = 17 };
        w.Openings[0].CopyTo(a.Post);
        w.Applications.Add(a);
        w.Job = new Job { Hired = 5, RankDay = 5, Pay = 4000, Work = 2, Boss = 3, SettledDay = 9, Due = 4, Worked = 3, Absent = 1, Warnings = 1, Earned = 123400, NoticeEnd = 25, Leaving = 1 };
        new Opening { Id = 1, Prof = ProCatalogue.Developer, Rank = 1, Kind = (int)EmployerKind.TechFirm, Employer = "Skyvault Systems", Street = "Main", Number = 4, Pay = 5500 }.CopyTo(w.Job.Post);
        w.History.Add(new Spell { Prof = ProCatalogue.BarStaff, Top = 1, Employer = "Cafe", From = 1, To = 4, Reference = 3, Leaving = 1, Earned = 5000 });
        string line = CareersStore.Write(f);
        int version;
        StudyFile back = CareersStore.Read(line, out version);
        Check(CareersStore.Write(back) == line, "work: the save's line reads back the same");
        WorkFile b = back.Work;
        Check(b.NextId == 40 && b.AgencyUntil == 33 && b.Earned == 987654 && b.DaysWorked == 12, "work: the working life's figures come back");
        Check(b.Openings.Count == 1 && b.Openings[0].Employer == "Firm  with odd chars" && b.Openings[0].Family == 8 && b.Openings[0].Via == (int)Via.JobsBoard,
            "work: a vacancy comes back, its name's dividers made spaces");
        Check(b.Applications.Count == 1 && b.Applications[0].Offer == 6100 && b.Applications[0].At == ApplyStep.Offered && b.Applications[0].Post.Rank == 2, "work: an application comes back");
        Check(b.Job != null && b.Job.Post.Employer == "Skyvault Systems" && b.Job.Pay == 4000 && b.Job.Warnings == 1 && b.Job.NoticeEnd == 25 && b.Job.Earned == 123400, "work: the job comes back");
        Check(b.History.Count == 1 && b.History[0].Reference == 3, "work: the CV comes back");
        Check(b.StartTold, "work: the first step told comes back, so it is said once a save");
        var none = CareersStore.Read("v=1~h:1", out version);
        Check(none.Work.Job == null && none.Work.Openings.Count == 0 && !none.Work.StartTold, "work: a line of the study's first version has no working life, and the first step is still to tell");
    }

    // Applying again after a no (WorkFile.TurnedDown, ProRules.ReapplyDays): the same post at the same employer waits 14 days,
    // whichever vacancy it comes in; another employer or another post is open at once; the day is saved, and an old save's
    // noes count from the day they can be told from, or bar nothing.
    private static void WorkReapply()
    {
        Opening Post(int id, string employer, int prof, int rank) => new Opening
        {
            Id = id, Prof = prof, Rank = rank, Kind = (int)EmployerKind.Rival, Employer = employer, Street = "Elm St", Number = 12, Pay = 5000, From = 1, To = 400
        };
        var f = new StudyFile { Seed = 9 };
        WorkFile w = f.Work;
        Opening first = Post(5, "Harbour Law", ProCatalogue.Lawyer, 1);
        var a = new Application { Day = 18, Stage = (int)ApplyStep.Sent, Answer = 20, Score = 40 };
        first.CopyTo(a.Post);
        w.Applications.Add(a);
        Check(w.TurnedDown(first, 20) == null && a.Refused == -1, "work: an application waiting for its answer bars nothing");
        a.Reject(20);
        Check(a.Refused == 20 && a.At == ApplyStep.Rejected, "work: a no keeps its day");
        Check(ProRules.ReapplyDays == 14 && ProRules.ReapplyFrom(20) == 34, "work: the same post can be applied for again 14 days after a no");
        Check(w.TurnedDown(first, 20) == a && w.TurnedDown(first, 21) == a, "work: the same post at the same employer is refused on the day of the no and the next day");
        Check(w.TurnedDown(first, 33) == a, "work: ...and on the 13th day after it");
        Check(w.TurnedDown(first, 34) == null && w.TurnedDown(first, 60) == null, "work: ...and taken again from the 14th day");
        Check(w.TurnedDown(Post(6, "Quay Partners", ProCatalogue.Lawyer, 1), 21) == null, "work: the same profession and rank at another employer is open at once");
        Check(w.TurnedDown(Post(7, "Harbour Law", ProCatalogue.Lawyer, 2), 21) == null && w.TurnedDown(Post(8, "Harbour Law", ProCatalogue.Developer, 1), 21) == null,
            "work: another post at the same employer is open at once");
        Opening again = Post(41, "Harbour Law", ProCatalogue.Lawyer, 1);
        again.From = 25;
        Check(w.TurnedDown(again, 26) == a && w.TurnedDown(again, 34) == null, "work: a new vacancy the employer posts later for the same post is refused until the 14 days are over");
        var later = new Application { Day = 30, Stage = (int)ApplyStep.Sent };
        first.CopyTo(later.Post);
        later.Reject(36);
        w.Applications.Add(later);
        Check(w.TurnedDown(first, 40) == later && w.TurnedDown(first, 49) == later && w.TurnedDown(first, 50) == null, "work: the latest of two noes for the same post sets the day");
        var offered = new Application { Day = 10, Stage = (int)ApplyStep.Offered, Offer = 5000, Lapse = 15 };
        first.CopyTo(offered.Post);
        offered.Stage = (int)ApplyStep.Declined;
        Check(!offered.Bars(11), "work: an offer the player turned down bars nothing");

        // the save's round trip, and an old save without the day
        var g = new StudyFile { Seed = 9 };
        var screened = new Application { Day = 18, Stage = (int)ApplyStep.Rejected, Answer = 20, Refused = 98765 };
        first.CopyTo(screened.Post);
        var interviewed = new Application { Day = 18, Stage = (int)ApplyStep.Rejected, Answer = 19, InterviewDay = 23, InterviewHour = 11, Refused = 98765 };
        Post(9, "Quay Partners", ProCatalogue.Lawyer, 1).CopyTo(interviewed.Post);
        var withdrawn = new Application { Day = 18, Stage = (int)ApplyStep.Rejected, Answer = 19, InterviewDay = 23, Offer = 5200, Asks = 1, Lapse = 26, Refused = 98765 };
        Post(10, "Harbour Law", ProCatalogue.Developer, 1).CopyTo(withdrawn.Post);
        g.Work.Applications.Add(screened);
        g.Work.Applications.Add(interviewed);
        g.Work.Applications.Add(withdrawn);
        int version;
        screened.Refused = 20;
        string line = CareersStore.Write(g);
        StudyFile back = CareersStore.Read(line, out version);
        Check(CareersStore.Write(back) == line && back.Work.Applications.Count == 3 && back.Work.Applications[0].Refused == 20 && back.Work.Applications[1].Refused == 98765,
            "work: a no's day comes back from the save");
        Check(back.Work.TurnedDown(first, 33) != null && back.Work.TurnedDown(first, 34) == null, "work: a no read back from the save still bars its post for the rest of the 14 days");
        screened.Refused = interviewed.Refused = withdrawn.Refused = 98765;
        string old = CareersStore.Write(g).Replace("|98765|0", string.Empty);   // a save from before the day held neither it nor the post's week after it (ShiftOpenChecks)
        Check(old.IndexOf("98765", StringComparison.Ordinal) < 0, "work: the old save's line has no day of a no");
        StudyFile was = CareersStore.Read(old, out version);
        WorkFile ow = was.Work;
        Check(ow.Applications.Count == 3 && ow.Applications[0].Refused == 20 && ow.Applications[1].Refused == 23 && ow.Applications[2].Refused == -1,
            "work: an old save's noes count from the answer's day or the interview's, and a withdrawn offer's day is not known");
        Check(ow.TurnedDown(first, 33) != null && ow.TurnedDown(first, 34) == null && ow.TurnedDown(Post(10, "Harbour Law", ProCatalogue.Developer, 1), 27) == null,
            "work: an old save loads with its noes barring from the day they were made, and one with no day bars nothing");
        var fresh = CareersStore.Read("v=1~h:1", out version);
        Check(fresh.Work.TurnedDown(first, 1) == null, "work: a save with no applications bars nothing");
    }

    // The way to an offer (Careers/Work/ProPath): the chance of one, the points short, each profession's next step and the
    // first step of all from a new game, and that a course's effect on a rung is foreseen as passing it then makes it.
    private static void WorkPath()
    {
        bool chance = true, monotone = true;
        int last = -1;
        for (int score = 0; score <= 100; score++)
        {
            bool any = false;
            for (int s = -ProRules.ScreenLuck; s <= ProRules.ScreenLuck; s++)
            {
                for (int o = -ProRules.InterviewLuck; o <= ProRules.InterviewLuck; o++)
                {
                    any |= ProRules.Screened(score, s) && ProRules.Offered(score, o);
                }
            }
            int c = ProPath.OfferChance(score);
            chance &= any == ProPath.CanBeOffered(score) && c >= 0 && c <= 100;
            monotone &= c >= last;
            last = c;
        }
        Check(chance && monotone, "path: an offer can come exactly where some luck of both steps passes, and the chance only grows with the score");
        Check(ProPath.OfferChance(0) == 0 && ProPath.OfferChance(44) == 0 && ProPath.OfferChance(45) == 1 && ProPath.OfferChance(75) == 100,
            "path: under 45 no luck brings an offer; 45 is a sliver; 75 is sure (" + ProPath.OfferChance(45) + ", " + ProPath.OfferChance(75) + ")");

        var probe = new Candidate { Points = 0, Need = 10, Qualified = true, Cycle = 1 };
        int shortBy = ProPath.PointsShort(probe);
        Candidate at = probe, below = probe;
        at.Points = shortBy;
        below.Points = shortBy - 1;
        Check(shortBy > 0 && shortBy <= 100 && ProPath.CanBeOffered(ProRules.HireScore(at)) && !ProPath.CanBeOffered(ProRules.HireScore(below)),
            "path: the points short are the fewest that make an offer possible (" + shortBy + ")");
        probe.Qualified = false;
        Check(ProPath.PointsShort(probe) > 100, "path: no points make up for a missing qualification");

        // A new player: some posts let them apply, none can bring an offer. That is what stopped every offer.
        var fresh = new StudyFile();
        var t = new Candidate { Cycle = 1 };
        int eligible = 0, ready = 0;
        bool same = true;
        foreach (Profession p in ProCatalogue.All)
        {
            eligible += ProRules.Eligible(fresh, p, 0) ? 1 : 0;
            ready += ProPath.Ready(fresh, p, 0, null, t) ? 1 : 0;
            for (int r = 0; r < p.Ranks.Length; r++)
            {
                Candidate x = ProPath.With(fresh, p, r, null, t);
                same &= x.Points == ProRules.Points(fresh, p, r) && x.Need == ProRules.Need(fresh, p, r) && x.Qualified == ProRules.Qualified(fresh, p, r) && x.Rank == r;
            }
        }
        Check(eligible > 0 && ready == 0, "path: a new player may apply for " + eligible + " first posts but no employer could offer one: a course comes first");
        Check(same, "path: with no course added, what an employer reads is what the rules read");
        int prof, course;
        bool first = ProPath.FirstStep(fresh, t, true, out prof, out course);
        CourseDef c1 = StudyCatalogue.Find(course);
        Check(first && prof >= 0 && c1 != null && c1.Kind == CourseKind.Short && c1.Campus == Campus.School,
            "path: a new player's first step is a short course at the school (" + (c1 != null ? c1.Key : "none") + " for " + (prof >= 0 ? ProCatalogue.All[prof].Key : "none") + ")");
        int prof2, course2;
        Check(ProPath.FirstStep(fresh, t, false, out prof2, out course2) && prof2 == prof && course2 == course, "path: the first step needs no university");

        // The course passed: the post is then within reach, as foreseen.
        var done = new StudyFile();
        if (c1 != null)
        {
            done.Results.Add(new Enrolment { Course = c1.Id, Stage = (int)EnrolStage.Passed, Grade = 2 });
            StudyRules.Learn(done, c1.SkillA, c1.GainA * 1000, 1);
            StudyRules.Learn(done, c1.SkillB, c1.GainB * 1000, 1);
        }
        Profession pp = ProCatalogue.Find(prof);
        Check(pp != null && ProPath.Ready(done, pp, 0, null, t) && ProPath.For(done, pp, 0, t, true).Step == PathStep.Ready && ProPath.For(done, pp, 0, t, true).Chance > 0,
            "path: with that course passed an offer can come, as foreseen");
        Console.WriteLine("  path: a new player's first step is " + (c1 != null ? c1.Key : "none") + " for " + (pp != null ? pp.Key : "none") + ", then about "
                          + (pp != null ? ProPath.For(done, pp, 0, t, true).Chance : 0) + "% an application");
        bool foreseen = true;
        foreach (CourseDef c in StudyCatalogue.All)
        {
            var g = new StudyFile();
            g.Results.Add(new Enrolment { Course = c.Id, Stage = (int)EnrolStage.Passed, Grade = 2 });
            StudyRules.Learn(g, c.SkillA, c.GainA * 1000, 1);
            StudyRules.Learn(g, c.SkillB, c.GainB * 1000, 1);
            foreach (Profession p in ProCatalogue.All)
            {
                for (int r = 0; r < p.Ranks.Length; r++)
                {
                    Candidate x = ProPath.With(fresh, p, r, c, t);
                    foreseen &= x.Points == ProRules.Points(g, p, r) && x.Need == ProRules.Need(g, p, r) && x.Qualified == ProRules.Qualified(g, p, r);
                }
            }
        }
        Check(foreseen, "path: a course's effect on every rung is foreseen as passing it makes it");

        // Each profession's next step.
        Profession law = ProCatalogue.Find(ProCatalogue.Lawyer), sales = ProCatalogue.Find(ProCatalogue.Sales), nurse = ProCatalogue.Find(ProCatalogue.Nurse);
        PathView lv = ProPath.For(fresh, law, 0, t, true);
        Check(lv.Step == PathStep.Course && lv.Course == StudyCatalogue.Law, "path: law needs its degree first");
        Check(ProPath.For(fresh, law, 0, t, false).Step == PathStep.Closed, "path: with the university off, law is closed");
        PathView sv = ProPath.For(fresh, sales, 0, t, true);
        Check(sv.Step == PathStep.Skills && sv.Short > 0 && sv.Course >= 0 && ProPath.Ready(fresh, sales, 0, StudyCatalogue.Find(sv.Course), t),
            "path: sales needs no course but more skill, and names a course that gives enough (" + sv.Short + " points, " + (sv.Course >= 0 ? StudyCatalogue.Find(sv.Course).Key : "none") + ")");
        PathView nv = ProPath.For(fresh, nurse, 0, t, true);
        Check(nv.Step == PathStep.Course && Array.IndexOf(nurse.Ranks[0].Quals, nv.Course) >= 0, "path: nursing's first post needs one of its courses");
        var studying = new StudyFile();
        studying.Courses.Add(new Enrolment { Course = StudyCatalogue.Law, Stage = (int)EnrolStage.Studying });
        PathView lv2 = ProPath.For(studying, law, 0, t, true);
        Check(lv2.Step == PathStep.Studying && lv2.Course == StudyCatalogue.Law, "path: a course under way is the step");
        var lawyer = new StudyFile();
        lawyer.Results.Add(new Enrolment { Course = StudyCatalogue.Law, Stage = (int)EnrolStage.Passed, Grade = 3 });
        StudyRules.Learn(lawyer, TalentCatalogue.Law, 40000, 1);
        StudyRules.Learn(lawyer, TalentCatalogue.Writing, 20000, 1);
        Check(ProPath.For(lawyer, law, 0, t, true).Step == PathStep.Ready, "path: with the degree passed a paralegal's post can be offered");
        PathView cert = ProPath.For(lawyer, law, 2, t, true);
        Check(cert.Step == PathStep.Course && cert.Course == StudyCatalogue.LegalPractice, "path: an associate's rung asks the practice course before the bar");
    }

    // A post applied to again after a withdrawal holds two applications: every action goes to the live one, and the
    // withdrawn one is never taken for it (a player's report of 25 Oct: the interview could not be reached).
    private static void WorkAppliedAgain()
    {
        var w = new WorkFile();
        var post = new Opening { Id = 77, Prof = ProCatalogue.Lawyer, Rank = 1, Kind = (int)EmployerKind.Rival, Employer = "Harbour Law", Street = "Elm St", Number = 12, Pay = 5000, From = 1, To = 400 };
        var old = new Application { Day = 10, Stage = (int)ApplyStep.Withdrawn };
        post.CopyTo(old.Post);
        var again = new Application { Day = 20, Stage = (int)ApplyStep.Interview, InterviewDay = 22, InterviewHour = 10 };
        post.CopyTo(again.Post);
        w.Applications.Add(old);
        w.Applications.Add(again);
        Check(w.ApplicationOf(77) == again, "work: a post applied to again finds the live application, not the withdrawn one");
        var third = new Application { Day = 30, Stage = (int)ApplyStep.Declined };
        post.CopyTo(third.Post);
        again.Stage = (int)ApplyStep.Withdrawn;
        w.Applications.Add(third);
        Check(w.ApplicationOf(77) == third, "work: with none live, the newest application to a post is found");
        string app = ModSource(@"Careers\CareerApp.cs");
        Check(app.Contains("OlderApplicationRow + i") && app.Contains("_seenPosts.Add(a.Post.Id)"),
            "work sources: an older application to the same post has a row of its own, so a click lands on the one meant");
    }
}
