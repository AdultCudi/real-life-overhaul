using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

// Checks for a manager's task switches and their weekly rota (Scripts/Economy/Company/ManagerRules: Runs, Due and Rota):
// a task switched off never runs at any hour of any day, at any kind of place, and the company asks the player about it
// again; each task runs only at its own hour; a manager's rota is left alone when the week has no gap and written only
// when the new week keeps everybody on it and covers no fewer hours; the director goes through those rules and through
// nothing else; a save of 0.44.0 loads with every task on and writes back the same line, nothing added to it.
internal static partial class Program
{
    private static void ManagerTasks()
    {
        ManagerTaskSwitches();
        ManagerTaskHours();
        ManagerRotaWhole();
        ManagerTaskWiring();
        ManagerTaskOldSave();
    }

    private static readonly ManagedKind[] ManagedKinds = { ManagedKind.Shop, ManagedKind.Works, ManagedKind.Warehouse };

    // Over four weeks, every hour: how often a task would run at a kind of place with these switches.
    private static int ManagerRuns(int tasks, ManagedKind kind, ManagerTask task)
    {
        int runs = 0;
        for (int day = 1; day <= 28; day++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                runs += ManagerRules.Due(day, hour, task) && ManagerRules.Runs(tasks, kind, task) ? 1 : 0;
            }
        }
        return runs;
    }

    private static void ManagerTaskSwitches()
    {
        foreach (ManagedKind kind in ManagedKinds)
        {
            int all = ManagerRules.Kinds(kind);
            for (int t = 0; t < (int)ManagerTask.Count; t++)
            {
                var task = (ManagerTask)t;
                if (!ManagerRules.Has(all, task))
                {
                    Check(ManagerRuns(all, kind, task) == 0 && ManagerRuns(0xff, kind, task) == 0,
                        "manager switches: a " + kind + " never has " + task + ", whatever its switches say");
                    continue;
                }
                int off = all & ~ManagerRules.Bit(task);
                bool othersRun = true;
                for (int u = 0; u < (int)ManagerTask.Count; u++)
                {
                    if (u != t && ManagerRules.Has(all, (ManagerTask)u))
                    {
                        othersRun &= ManagerRuns(off, kind, (ManagerTask)u) == ManagerRuns(all, kind, (ManagerTask)u) && ManagerRuns(off, kind, (ManagerTask)u) > 0;
                    }
                }
                Check(ManagerRuns(all, kind, task) > 0 && ManagerRuns(off, kind, task) == 0 && ManagerRuns(0, kind, task) == 0 && othersRun,
                    "manager switches: at a " + kind + ", " + task + " switched off never runs in four weeks of hours, and the other tasks run as before");
                var m = new ManagerFile { Place = "P~1", Id = "boss", Tasks = off, Seen = true };
                Check(!ManagerRules.Answers(true, m, task, "e1"), "manager switches: at a " + kind + ", " + task + " switched off is asked of the player again");
            }
        }
        // Every switch off: nothing at all.
        int none = 0;
        foreach (ManagedKind kind in ManagedKinds)
        {
            for (int t = 0; t < (int)ManagerTask.Count; t++)
            {
                none += ManagerRuns(0, kind, (ManagerTask)t);
            }
        }
        Check(none == 0, "manager switches: with every switch off a manager does nothing at any hour");
    }

    private static void ManagerTaskHours()
    {
        // Each task at its own hour only: the daily ones every day, the Sunday ones once a week.
        int[] want = { 28, 28, 28, 28, 4, 28, 4, 28 };
        int[] at = { ManagerCatalogue.StaffHour, ManagerCatalogue.StaffHour, ManagerCatalogue.StaffHour, ManagerCatalogue.StaffHour, ManagerCatalogue.StockHour,
                     ManagerCatalogue.PriceHour, ManagerCatalogue.RotaHour, ManagerCatalogue.HireHour };
        bool right = true;
        var wrong = new List<string>();
        for (int t = 0; t < (int)ManagerTask.Count; t++)
        {
            int count = 0, elsewhere = 0;
            for (int day = 1; day <= 28; day++)
            {
                for (int hour = 0; hour < 24; hour++)
                {
                    bool due = ManagerRules.Due(day, hour, (ManagerTask)t);
                    count += due ? 1 : 0;
                    bool sundayOnly = t == (int)ManagerTask.Stock || t == (int)ManagerTask.Rota;
                    elsewhere += due && (hour != at[t] || (sundayOnly && ManagerRules.Weekday(day) != ManagerCatalogue.Sunday)) ? 1 : 0;
                }
            }
            if (count != want[t] || elsewhere != 0)
            {
                right = false;
                wrong.Add((ManagerTask)t + " " + count + " times, " + elsewhere + " at another hour");
            }
        }
        Check(right, "manager switches: each task is made only at its own hour, the weekly rota only on Sunday at " + ManagerCatalogue.RotaHour + ":00"
                     + (wrong.Count > 0 ? " (" + string.Join("; ", wrong) + ")" : string.Empty));
        Check(!ManagerRules.Due(7, ManagerCatalogue.RotaHour, ManagerTask.Count) && !ManagerRules.Due(6, ManagerCatalogue.RotaHour, ManagerTask.Rota)
              && ManagerRules.Due(7, ManagerCatalogue.RotaHour, ManagerTask.Rota) && !ManagerRules.Due(7, ManagerCatalogue.RotaHour - 1, ManagerTask.Rota),
            "manager switches: the rota is looked at on a Sunday at its hour and at no other time");
    }

    // A week on two stations: each shift of one worker at one station, day and hours.
    private static RotaPlan ManagerWeek(int covered, int wanted, params int[] shifts)
    {
        var plan = new RotaPlan();
        plan.Size(2);
        for (int i = 0; i + 4 < shifts.Length; i += 5)
        {
            int worker = shifts[i], station = shifts[i + 1], day = shifts[i + 2], from = shifts[i + 3], to = shifts[i + 4];
            for (int h = from; h < to; h++)
            {
                plan.At[(day * RotaCatalogue.Hours + h) * 2 + station] = (short)worker;
            }
        }
        plan.CoveredHours = covered;
        plan.WantedHours = wanted;
        return plan;
    }

    private static RotaProblem ManagerStaff(params int[] can)
    {
        var p = new RotaProblem();
        p.Size(2, can.Length / 2);
        for (int i = 0; i < can.Length; i++)
        {
            p.Can[i] = (byte)can[i];
        }
        return p;
    }

    private static void ManagerRotaWhole()
    {
        var hoursNow = new int[8];
        var hoursAfter = new int[8];
        bool[] none = new bool[3];
        int dropped;
        // Ann (0) and Ben (1) can work both stations; Cat (2) has no skill either takes.
        RotaProblem staff = ManagerStaff(80, 80, 70, 70, 0, 0);
        // The week as it is: every wanted hour manned (Monday 9 to 17 at both stations).
        RotaPlan whole = ManagerWeek(16, 16, 0, 0, 0, 9, 17, 1, 1, 0, 9, 17);
        RotaPlan other = ManagerWeek(16, 16, 1, 0, 0, 9, 17, 0, 1, 0, 9, 17);
        Check(ManagerRules.Rota(whole, other, staff, none, hoursNow, hoursAfter, out dropped) == RotaCall.Whole && hoursNow[0] == 8 && hoursNow[1] == 8 && hoursNow[2] == 0,
            "manager rota: a week with every wanted hour manned and everybody who can work on it is left alone, even when the planner would shuffle it (it used to be written over every Sunday)");
        // A gap: Ben has left the morning unmanned at station 1 (8 of 16 hours).
        RotaPlan gap = ManagerWeek(8, 16, 0, 0, 0, 9, 17);
        RotaPlan mends = ManagerWeek(16, 16, 0, 0, 0, 9, 17, 1, 1, 0, 9, 17);
        Check(ManagerRules.Rota(gap, mends, staff, none, hoursNow, hoursAfter, out dropped) == RotaCall.Write && dropped == 0 && hoursAfter[0] == 8 && hoursAfter[1] == 8,
            "manager rota: a gap is mended by a new week that keeps everybody on it, and that week is written");
        RotaPlan dropsAnn = ManagerWeek(16, 16, 1, 0, 0, 9, 17, 1, 1, 0, 9, 17);
        Check(ManagerRules.Rota(gap, dropsAnn, staff, none, hoursNow, hoursAfter, out dropped) == RotaCall.LeavesOut && dropped == 1 && hoursAfter[0] == 0,
            "manager rota: a new week that takes somebody on the rota off it is never written, however many hours it covers");
        RotaPlan less = ManagerWeek(4, 16, 0, 0, 0, 9, 13);
        Check(ManagerRules.Rota(gap, less, staff, none, hoursNow, hoursAfter, out dropped) == RotaCall.CoversLess && dropped == 0,
            "manager rota: a new week that leaves hours with nobody that have somebody now is never written");
        RotaPlan same = ManagerWeek(8, 16, 0, 1, 0, 9, 17);
        Check(ManagerRules.Rota(gap, same, staff, none, hoursNow, hoursAfter, out dropped) == RotaCall.NoBetter,
            "manager rota: a new week that mends nothing is not written (no change out of nowhere)");
        // Somebody who can work and has no shift (a new hire): the week is looked at, and written if it puts them on.
        RotaProblem withNew = ManagerStaff(80, 80, 70, 70, 60, 60);
        RotaPlan newcomer = ManagerWeek(16, 16, 0, 0, 0, 9, 17, 1, 1, 0, 9, 13, 2, 1, 0, 13, 17);
        Check(ManagerRules.Rota(whole, newcomer, withNew, none, hoursNow, hoursAfter, out dropped) == RotaCall.Write && hoursAfter[2] == 4,
            "manager rota: somebody who can work and has no shift is put on the rota when the new week keeps everybody else on it");
        Check(ManagerRules.Rota(whole, whole, withNew, none, hoursNow, hoursAfter, out dropped) == RotaCall.NoBetter,
            "manager rota: a new hire the planner finds no hours for leaves the week as it is");
        // Those the planner leaves as they are (a desk holder) count for neither side.
        bool[] annFixed = { true, false, false };
        Check(ManagerRules.Rota(gap, dropsAnn, staff, annFixed, hoursNow, hoursAfter, out dropped) == RotaCall.Write && dropped == 0,
            "manager rota: somebody the planner leaves as they are is not counted as taken off");
        // The same week always gives the same call.
        RotaCall first = ManagerRules.Rota(gap, mends, staff, none, hoursNow, hoursAfter, out int d1);
        int a0 = hoursAfter[0];
        RotaCall again = ManagerRules.Rota(gap, mends, staff, none, hoursNow, hoursAfter, out int d2);
        Check(first == again && d1 == d2 && a0 == hoursAfter[0], "manager rota: the same weeks give the same call on every machine");
    }

    private static void ManagerTaskWiring()
    {
        string managers = ModSource(@"Economy\Company\CompanyDirector.Managers.cs");
        string decisions = ModSource(@"Economy\Company\CompanyDirector.ManagerDecisions.cs");
        string jobs = ModSource(@"Economy\Company\CompanyDirector.RotaJobs.cs");
        string app = ModSource(@"Economy\Company\CompanyApp.Managers.cs");
        string hour = MemberText(managers, "private void Hour(int day, int hour)");
        Check(hour.Contains("ManagerRules.Due(day, hour, (ManagerTask)t)") && !hour.Contains("ManagerCatalogue.RotaHour") && !hour.Contains("ManagerCatalogue.PriceHour"),
            "manager switches: the hour's decisions are made by ManagerRules.Due alone");
        Check(MemberText(managers, "private void Round(int day, int hour, ManagerTask task)").Contains("!ManagerRules.Runs(mp.Own.Tasks, mp.Kind, task)"),
            "manager switches: every round of decisions, the test button's too, skips a place whose switch for it is off");
        Check(Occurs(decisions, "RotaAsk(") == 1 && MemberText(decisions, "private void DecideRota(").Contains("RotaAsk(mp.Place.Place, JobManager, false)")
              && Occurs(managers, "JobManager") == 0 && Occurs(jobs, "!ManagerRotaJudged(job.Place, sheet)") == 1
              && jobs.IndexOf("!ManagerRotaJudged(job.Place, sheet)", StringComparison.Ordinal) < jobs.IndexOf("return RotaWrite(job, place, sheet", StringComparison.Ordinal),
            "manager switches: a manager's week is asked for in one place only, and judged just before it would be written");
        Check(MemberText(decisions, "private bool ManagerRotaJudged(string place, RotaSheet sheet)").Contains("if (!ManagerRotaGiven(mp))")
              && MemberText(decisions, "private bool ManagerRotaGiven(ManagedPlace mp)").Contains("ManagerRules.Runs(mp.Own.Tasks, mp.Kind, ManagerTask.Rota)")
              && MemberText(decisions, "private bool ManagerRotaJudged(string place, RotaSheet sheet)").Contains("ManagerRules.Rota(now, fill, sheet.Problem, sheet.Fixed"),
            "manager switches: with the weekly rota switched off while a week is worked out, the schedule is not touched; with it on, only a whole week is written");
        Check(MemberText(managers, "public void ManagerTaskSet(string place, ManagerTask task, bool on)").Contains("ManagerRotaDropped(place)"),
            "manager switches: the weekly rota switched off drops a week of the manager's still waiting");
        Check(MemberText(app, "private void WritePlace(InfoBox box, ManagedPlace mp)").Contains("TaskSwitch(box, place, task, TaskKeys[t]")
              && MemberText(app, "private void TaskSwitch(").Contains("box.Toggle(labelKey, note, on, set)"),
            "manager switches: the manager's own panel has a switch for each task, the Mod settings app's own");
    }

    private static void ManagerTaskOldSave()
    {
        // A line as 0.44.0 wrote it: a business's manager appointed with every kind, a factory's with its three.
        string Record(int tasks, string place, string id, string name)
        {
            var n = new int[24];
            n[1] = 12;
            n[2] = tasks;
            n[3] = ManagerCatalogue.WageBandDefault;
            n[4] = ManagerCatalogue.StockDaysDefault;
            n[5] = ManagerCatalogue.PriceLowDefault;
            n[6] = ManagerCatalogue.PriceHighDefault;
            n[7] = ManagerCatalogue.BudgetDefault;
            n[8] = 5;
            return string.Join("|", n) + "|" + place + "|" + id + "||||" + name;
        }
        string line = "v=1^h:10|4|0^m:" + Record(ManagerRules.Kinds(ManagedKind.Shop), "Main%20St~3", "e1", "Ann Lee") + ";"
                      + Record(ManagerRules.Kinds(ManagedKind.Works), "Mill%20Rd~8", "e2", "Bob Day") + "^d:1|9|21|6|3|0|5|30|32|0|0|0|Main%20St~3||Ann Lee|";
        ManagerState s = ManagerStore.Read(line, out int version);
        bool every = s != null && s.Managers.Count == 2;
        for (int i = 0; every && i < 2; i++)
        {
            ManagedKind kind = i == 0 ? ManagedKind.Shop : ManagedKind.Works;
            for (int t = 0; t < (int)ManagerTask.Count; t++)
            {
                every &= ManagerRules.Runs(s.Managers[i].Tasks, kind, (ManagerTask)t) == ManagerRules.Has(ManagerRules.Kinds(kind), (ManagerTask)t);
            }
        }
        Check(every && version == 1, "manager switches: a save of 0.44.0 loads with every task its place has switched on, as it was");
        Check(s != null && ManagerStore.Write(s) == line, "manager switches: that save writes back the very same line, so an older version reads it as before");
        Check(s != null && s.Decisions.Count == 1 && s.Decisions[0].Task == (int)ManagerTask.Rota && s.Decisions[0].A == 5,
            "manager switches: a rota decision of 0.44.0 reads back as it was");
    }
}
