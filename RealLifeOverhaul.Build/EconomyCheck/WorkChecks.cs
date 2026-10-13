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
        Check(week >= 2500 && week <= 4000 && top >= 8000 && top <= 12000, "work: a senior developer's week is about 3,200, a chief executive's about 10,000 (" + week + ", " + top + ")");
        Check(ProCatalogue.Find(ProCatalogue.BarStaff).Ranks[0].Pay < 2500 * 1 + 100, "work: the first rung of a bar is about the game's cashier");
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
        Check(ProRules.Experience(w, ProCatalogue.Developer, 100, 365) == 60 && ProRules.Experience(w, ProCatalogue.Developer, 100, 60) == 60 * 365 / 60, "work: experience is in years of 365 days");
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
        var none = CareersStore.Read("v=1~h:1", out version);
        Check(none.Work.Job == null && none.Work.Openings.Count == 0, "work: a line of the study's first version has no working life");
    }
}
