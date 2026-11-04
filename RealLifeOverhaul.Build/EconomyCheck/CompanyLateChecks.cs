using System;
using System.Diagnostics;
using RealLifeOverhaul.Economy;

// A manager who hires and lets go of crew: a working day worked short of the day planned is a late day, counted running; three running and the board
// having somebody for their place is a let go, so a place is never left short. The count is kept with the employee and the older save loads without it.
internal static partial class Program
{
    private static void CompanyLate()
    {
        int before = _run, failedBefore = _failed;
        LateCount();
        LetGoRule();
        LateCrew();
        LateSaves();
        Console.WriteLine("  late days and the manager who lets go: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    private static void LateCount()
    {
        var f = new StaffFile { Id = "e1" };
        StaffRules.LateDay(f, 8, 8, false);
        Check(f.LateDays == 0, "late: a full day is no late day");
        StaffRules.LateDay(f, 8, 7, false);
        Check(f.LateDays == 1, "late: a day an hour short is one (" + f.LateDays + ")");
        StaffRules.LateDay(f, 8, 8, false);
        Check(f.LateDays == 0, "late: a full day clears the count, so three late days are running days");
        StaffRules.LateDay(f, 8, 2, false);
        StaffRules.LateDay(f, 6, 0, false);
        StaffRules.LateDay(f, 8, 7, false);
        Check(f.LateDays == 3, "late: three short days in a row are three");
        StaffRules.LateDay(f, 0, 0, false);
        Check(f.LateDays == 3, "late: a day off changes nothing");
        StaffRules.LateDay(f, 8, 0, true);
        Check(f.LateDays == 3, "late: a day sick or in training changes nothing");
        StaffRules.LateDay(f, 8, 9, false);
        Check(f.LateDays == 0, "late: a day over the plan is a full day");
        for (int i = 0; i < 300; i++)
        {
            StaffRules.LateDay(f, 8, 0, false);
        }
        Check(f.LateDays == CompanyCatalogue.LateMost, "late: the count is bounded (" + f.LateDays + ")");
        var g = new StaffFile { Id = "e2" };
        StaffRules.LateDay(g, 8, -3, false);
        Check(g.LateDays == 1, "late: a worked count under nothing is still a late day, never an error");
        Check(CompanyCatalogue.LateShort >= 1, "late: a day is late by at least an hour, the game counting whole hours");
    }

    private static void LetGoRule()
    {
        Check(ManagerRules.LetGo(ManagerCatalogue.HireLate, true), "let go: three late days and somebody for their place is a let go");
        Check(!ManagerRules.LetGo(ManagerCatalogue.HireLate - 1, true), "let go: two late days is not");
        Check(!ManagerRules.LetGo(ManagerCatalogue.HireLate + 4, false), "let go: nobody on the board for their place, never, however late");
        Check(!ManagerRules.LetGo(0, true), "let go: on time is never");
        Check(ManagerCatalogue.HireLate == 3, "let go: after three late days, as the page says");
    }

    // A place of twelve over sixty days with seeded lateness: one let go a day at most, each followed by a hire, so the crew never falls below what is kept.
    private static void LateCrew()
    {
        int[] ran = new int[2];
        for (int run = 0; run < 2; run++)
        {
            const int keep = 12;
            var staff = new StaffFile[keep];
            for (int i = 0; i < keep; i++)
            {
                staff[i] = new StaffFile { Id = "e" + i };
            }
            int crew = keep, letGo = 0, worstDays = 0, lowest = keep, next = keep;
            for (int day = 1; day <= 60; day++)
            {
                for (int i = 0; i < keep; i++)
                {
                    // A person is late on some days by a seeded draw: some mostly, most never.
                    bool late = CityModel.Draw(77, 90 + i % 3, day * 31 + i, 1000) < (i % 4 == 0 ? 350 : 20);
                    StaffRules.LateDay(staff[i], 8, late ? 6 : 8, false);
                    worstDays = Math.Max(worstDays, staff[i].LateDays);
                }
                // The manager's pass: the latest of those at three or more goes, if the board has somebody (here always), and the crew is filled.
                int pick = -1;
                for (int i = 0; i < keep; i++)
                {
                    if (ManagerRules.LetGo(staff[i].LateDays, true) && (pick < 0 || staff[i].LateDays > staff[pick].LateDays))
                    {
                        pick = i;
                    }
                }
                if (pick >= 0)
                {
                    crew--;
                    lowest = Math.Min(lowest, crew);
                    letGo++;
                    staff[pick] = new StaffFile { Id = "e" + next++ };
                    crew++;
                }
            }
            ran[run] = letGo * 1000 + worstDays;
            Check(letGo >= 1 && letGo <= 60 && crew == keep && lowest >= keep - 1, "late crew: over sixty days " + letGo + " were let go, one a day at most, and the crew is back at " + keep + " after each");
        }
        Check(ran[0] == ran[1], "late crew: the same seed gives the same history twice");
        // The cost of counting a day for the largest company the save keeps.
        var many = new StaffFile[CompanyCatalogue.StaffMost];
        for (int i = 0; i < many.Length; i++)
        {
            many[i] = new StaffFile { Id = "x" + i };
        }
        var watch = Stopwatch.StartNew();
        for (int day = 0; day < 365; day++)
        {
            for (int i = 0; i < many.Length; i++)
            {
                StaffRules.LateDay(many[i], 8, (day + i) % 5 == 0 ? 6 : 8, false);
            }
        }
        watch.Stop();
        Check(watch.ElapsedMilliseconds < 250, "late cost: a year of lateness for " + CompanyCatalogue.StaffMost + " staff is counted in " + watch.ElapsedMilliseconds + " ms");
    }

    private static void LateSaves()
    {
        var s = new CompanyState { Seed = 7, FirstDay = 1, LastDay = 40 };
        s.Staff.Add(new StaffFile { Id = "e1", Place = "shop~1", LateDays = 2 });
        s.Staff.Add(new StaffFile { Id = "e2", Place = "shop~1", LateDays = 0, Rival = "Onyx" });
        string line = CompanyStore.Write(s);
        CompanyState back = CompanyStore.Read(line, out int version);
        Check(back != null && back.Staff.Count == 2 && back.Staff[0].LateDays == 2 && back.Staff[1].LateDays == 0 && back.Staff[1].Rival == "Onyx", "late save: the count round trips with the employee");
        Check(CompanyStore.Write(back) == line, "late save: writing what was read gives the same line");
        // The line before the count was kept: the record ends at the rival.
        string older = line.Replace("|2;", ";").Replace("|Onyx|0^c:", "|Onyx^c:");
        CompanyState old = CompanyStore.Read(older, out version);
        Check(old != null && old.Staff.Count == 2 && old.Staff[0].LateDays == 0 && old.Staff[1].Rival == "Onyx" && older != line, "late save: an older line loads with nobody late");
        string wild = line.Replace("|2;", "|7777;");
        CompanyState bounded = CompanyStore.Read(wild, out version);
        Check(bounded != null && bounded.Staff[0].LateDays == CompanyCatalogue.LateMost, "late save: a figure out of range is bounded on reading");
    }
}
