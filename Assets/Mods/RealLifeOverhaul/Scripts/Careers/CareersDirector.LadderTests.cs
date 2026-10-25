using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;
using UI.Notification;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The management ladder's test buttons (options page, Tests): a job at one of the city's chains, a promotion a rung at a
    /// time, every task of the rung brought, every task decided and checked, and the player's own request answered. Each writes
    /// a "Careers check (ladder ...)" line with PASS or why not, and one notice.
    /// </summary>
    public sealed partial class CareersDirector
    {
        private bool LadderReady()
        {
            if (!WorkReady())
            {
                return false;
            }
            if (!Settings.CareersLadder)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ladder_test_off", null, 8f);
                return false;
            }
            return true;
        }

        // The ladder of the job, seeded now if the job has none yet; a notice and null if the job is not at a city company.
        private LadderFile LadderNow()
        {
            LadderKeep(Save.Day);
            LadderFile f = Ladder;
            if (f == null)
            {
                Notifications.Show(NotificationType.Warning, "reallife_ladder_test_no_ladder", null, 8f);
                Logger?.Info("Careers check (ladder): no job at one of the city's companies to test on; press \"Give me a job at one of the city's chains\" first: FAIL, nothing to test");
            }
            return f;
        }

        /// <summary>
        /// A job at a store of one of the city's chains with at least two stores (a rival's, or the city's own of one name), on
        /// the floor of a profession the store employs; a job held elsewhere is left first. The company seeded at once and the
        /// Career app opened on its Management tab.
        /// </summary>
        public void TestLadderJob()
        {
            if (!LadderReady())
            {
                return;
            }
            try
            {
                int day = Save.Day;
                WorkFile w = _study.Work;
                if (Ladder != null)
                {
                    Logger?.Info("Careers check (ladder job): the job at " + w.Job.Post.Employer + " is on the ladder already: PASS");
                    OpenCareer(TabLadder, null);
                    return;
                }
                ReadEmployers(day);
                EmployerRef at = null;
                int prof = -1, most = 1;
                for (int i = 0; i < _employers.Count; i++)
                {
                    EmployerRef e = _employers[i];
                    int stores = Chain(e);
                    int p = FloorProfession(e);
                    if (p >= 0 && stores > most)
                    {
                        at = e;
                        prof = p;
                        most = stores;
                    }
                }
                if (at == null)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ladder_test_no_chain", null, 8f);
                    Logger?.Info("Careers check (ladder job): no business of the city's belongs to a company with two stores or more: FAIL, nothing to give");
                    return;
                }
                if (w.Job != null)
                {
                    EndJob(LeaveReason.Resigned, day, false);   // (a test: the job held is left as if its notice had run)
                }
                Profession own = ProCatalogue.Find(prof);
                var j = new Job { Hired = day, RankDay = day, ReviewDay = day, Work = (int)WorkMode.OnSite, SettledDay = day - 1 };
                j.Post.Id = w.Next();
                j.Post.Prof = prof;
                j.Post.Kind = (int)at.Kind;
                j.Post.Employer = at.Name;
                j.Post.Street = at.Street;
                j.Post.Number = at.Number;
                j.Post.Rival = at.Rival;
                j.Post.Pay = j.Pay = ProRules.PostPay(own, 0, at.Kind, 1000);
                j.Post.Work = (int)WorkMode.OnSite;
                j.Post.From = j.Post.To = day;
                j.Boss = TechRandom.Range(StudySeed, 8501, TechRandom.Of(at.Name), 0, 0, ProRules.BossKinds - 1);
                Hired(j, day);
                w.Job = j;
                FitShift(j.Post);
                LadderKeep(day);
                LadderFile f = Ladder;
                bool ok = f != null && f.Company.Stores.Count >= 2 && f.Company.Stores[0].Street == at.Street && f.Company.Stores[0].Number == at.Number;
                Logger?.Info("Careers check (ladder job): " + own.Key + " at " + at.Name + " (" + at.Street + " " + at.Number + "), " + most + " stores of the company in the city; seeded "
                             + (f != null ? f.Company.Stores.Count + " store(s), " + f.Company.People.Count + " people, the job's store first" : "nothing") + ": " + (ok ? "PASS" : "FAIL"));
                Notifications.Show(ok ? NotificationType.Success : NotificationType.Error, ok ? "reallife_ladder_test_job_pass" : "reallife_ladder_test_job_fail",
                    Data("employer", CareersText.Employer(at.Name), "title", CareersText.Title(prof, 0), "stores", (f != null ? f.Company.Stores.Count : 0).ToString()), 10f);
                Changed();
                OpenCareer(TabLadder, null);
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        // The stores in the city of the company an employer belongs to: the same rival's, or the city's own of the same name and type.
        private int Chain(EmployerRef e)
        {
            int n = 0;
            for (int i = 0; i < _employers.Count; i++)
            {
                EmployerRef o = _employers[i];
                bool same = e.Rival.Length > 0 ? o.Rival == e.Rival : o.Rival.Length == 0 && o.Name == e.Name && o.Type == e.Type;
                n += same ? 1 : 0;
            }
            return n;
        }

        // A profession worked on the floor of a store the employer's type employs (the management profession last: it starts above the floor).
        private static int FloorProfession(EmployerRef e)
        {
            int manager = -1;
            for (int p = 0; p < ProCatalogue.All.Length; p++)
            {
                Profession pro = ProCatalogue.All[p];
                if (pro.Place != Workplace.Business || !ProCatalogue.Holds(e.Professions, pro, e.Type))
                {
                    continue;
                }
                if (pro.Id != ProCatalogue.Manager)
                {
                    return pro.Id;
                }
                manager = pro.Id;
            }
            return manager;
        }

        /// <summary>The player promoted one rung on the ladder now, whatever it asks for: its pay, its role and its tools checked, the rung below's kept.</summary>
        public void TestLadderPromote()
        {
            if (!LadderReady())
            {
                return;
            }
            try
            {
                LadderFile f = LadderNow();
                if (f == null)
                {
                    return;
                }
                if (f.Rung >= LadderRanks.CareerTop)
                {
                    Notifications.Show(NotificationType.Info, "reallife_ladder_test_top", null, 8f);
                    Logger?.Info("Careers check (ladder promotion): already " + LadderRanks.Of(f.Rung).Key + ", the top of a career: PASS");
                    return;
                }
                if (f.Company.Stores.Count < LadderRanks.Of(f.Rung + 1).Stores)
                {
                    Notifications.Show(NotificationType.Warning, "reallife_ladder_why_stores", null, 8f);
                    Logger?.Info("Careers check (ladder promotion): " + f.Company.Name + " has " + f.Company.Stores.Count + " store(s), the next rung needs "
                                 + LadderRanks.Of(f.Rung + 1).Stores + ": PASS, refused as it should be");
                    return;
                }
                Job j = _study.Work.Job;
                int pay = j.Pay, rung = f.Rung;
                RungPower before = f.Def.Powers;
                LadderPromote(j, Save.Day);
                RungPower after = f.Def.Powers;
                Colleague superior = CareerLadderRules.Superior(f);
                bool ok = f.Rung == rung + 1 && (after & before) == before && after != before && j.Pay >= pay && (f.Rung >= (int)Rung.Executive || superior != null
                    || f.Rung >= (int)Rung.Regional);
                Logger?.Info("Careers check (ladder promotion): " + LadderRanks.Of(rung).Key + " to " + LadderRanks.Of(f.Rung).Key
                             + (f.Rung == (int)Rung.Executive ? " (" + (ExecSeat)f.Seat + ")" : string.Empty)
                             + ", pay " + pay + " to " + j.Pay + " cents an hour at the start's prices, " + Count(before) + " tools to " + Count(after) + ", reports to "
                             + (superior != null ? superior.Name : "the owner") + ": " + (ok ? "PASS" : "FAIL"));
                Notifications.Show(ok ? NotificationType.Success : NotificationType.Error, ok ? "reallife_ladder_test_promote_pass" : "reallife_ladder_test_promote_fail",
                    Data("title", LadderTitle(f), "count", Count(after).ToString()), 10f);
                Changed();
                OpenCareer(TabLadder, null);
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        private static int Count(RungPower p)
        {
            int n = 0;
            for (int i = 0; i < LadderRanks.Listed.Length; i++)
            {
                n += (p & LadderRanks.Listed[i]) != 0 ? 1 : 0;
            }
            return n;
        }

        /// <summary>Every task the player's rung decides brought now, to be decided in the Career app (or by the next button).</summary>
        public void TestLadderTasks()
        {
            if (!LadderReady())
            {
                return;
            }
            try
            {
                LadderFile f = LadderNow();
                if (f == null)
                {
                    return;
                }
                if (f.Rung == (int)Rung.Frontline)
                {
                    Notifications.Show(NotificationType.Info, "reallife_ladder_test_tasks_floor", null, 10f);
                    Logger?.Info("Careers check (ladder tasks): on the floor no task is the player's; their own requests are on the Management tab: PASS");
                    OpenCareer(TabLadder, null);
                    return;
                }
                int brought = CareerLadderRules.BringAll(f, Save.Day);
                bool ok = brought > 0 || f.Tasks.Count > 0;
                Logger?.Info("Careers check (ladder tasks): " + brought + " task(s) brought for a " + LadderRanks.Of(f.Rung).Key + ", " + f.Tasks.Count
                             + " waiting: " + (ok ? "PASS" : "FAIL, none could be brought"));
                Notifications.Show(ok ? NotificationType.Success : NotificationType.Error, ok ? "reallife_ladder_test_tasks_pass" : "reallife_ladder_test_tasks_fail",
                    Data("count", f.Tasks.Count.ToString()), 10f);
                Changed();
                OpenCareer(TabLadder, null);
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        /// <summary>Every task waiting decided by its first choice, each checked against what it should have done: a line each.</summary>
        public void TestLadderDecide()
        {
            if (!LadderReady())
            {
                return;
            }
            try
            {
                LadderFile f = LadderNow();
                if (f == null)
                {
                    return;
                }
                if (f.Tasks.Count == 0)
                {
                    Notifications.Show(NotificationType.Info, "reallife_ladder_test_decide_none", null, 8f);
                    Logger?.Info("Careers check (ladder decisions): no task waiting; press \"Bring every task of my rank now\" first: PASS, nothing to decide");
                    return;
                }
                var tasks = new List<LadderTask>(f.Tasks);
                int good = 0;
                for (int i = 0; i < tasks.Count; i++)
                {
                    LadderTask t = tasks[i];
                    bool ok = CareerLadderRules.DecideFirst(f, t, StudySeed, Save.Day, NameOf, out string what);
                    good += ok ? 1 : 0;
                    Logger?.Info("Careers check (ladder decision): " + (TaskKind)t.Kind + (t.Kind == (int)TaskKind.Escalation ? " (" + (Concern)t.About + ")" : string.Empty) + " at "
                                 + LadderStoreName(f, t.Store) + ": " + what + ": " + (ok ? "PASS" : "FAIL"));
                }
                Notifications.Show(good == tasks.Count ? NotificationType.Success : NotificationType.Error,
                    good == tasks.Count ? "reallife_ladder_test_decide_pass" : "reallife_ladder_test_decide_fail",
                    Data("count", tasks.Count.ToString(), "good", good.ToString()), 10f);
                Changed();
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }

        private static string LadderStoreName(LadderFile f, int s) => s >= 0
            && s < f.Company.Stores.Count ? f.Company.Stores[s].Name + " (" + f.Company.Stores[s].Street + " " + f.Company.Stores[s].Number + ")" : "?";

        /// <summary>The player's own request for equipment (or the one waiting) answered now, as the next morning would: granted or not, and its effect there.</summary>
        public void TestLadderAsk()
        {
            if (!LadderReady())
            {
                return;
            }
            try
            {
                LadderFile f = LadderNow();
                if (f == null)
                {
                    return;
                }
                int day = Save.Day;
                if (f.Ask == (int)StaffWant.None)
                {
                    string why = CareerLadderRules.Ask(f, f.Equipped ? (f.Insured ? StaffWant.Shift : StaffWant.Insurance) : StaffWant.Equipment, 1, day - 1);
                    if (why != null)
                    {
                        Notifications.Show(NotificationType.Warning, why, null, 8f);
                        return;
                    }
                }
                else
                {
                    f.AskDay = Math.Min(f.AskDay, day - 1);
                }
                var asked = (StaffWant)f.Ask;
                bool equipped = f.Equipped, insured = f.Insured;
                int prefer = f.Prefer;
                bool answered = CareerLadderRules.AnswerAsk(f, StudySeed, day, out bool granted);
                bool effect = !granted || (asked == StaffWant.Equipment ? f.Equipped && !equipped : asked == StaffWant.Insurance ? f.Insured && !insured : f.Prefer != prefer);
                bool ok = answered && effect && f.Ask == (int)StaffWant.None;
                if (granted && asked == StaffWant.Shift)
                {
                    KeepJobShift(_study.Work.Job, day, false);
                }
                Logger?.Info("Careers check (ladder request): " + asked + " asked of " + LadderBoss(f) + ", " + (granted ? "granted" : "turned down") + " at a chance of "
                             + CareerLadderRules.AskChance(f) + " per mille, the effect " + (granted ? (effect ? "in place" : "MISSING") : "none, as turned down") + ": " + (ok ? "PASS" : "FAIL"));
                Notifications.Show(ok ? NotificationType.Success : NotificationType.Error, granted ? "reallife_ladder_note_ask_yes" : "reallife_ladder_note_ask_no",
                    Data("boss", LadderBoss(f), "what", WantText((int)asked)), 10f);
                Changed();
            }
            catch (Exception e)
            {
                Fault("a test", e);
            }
        }
    }
}
