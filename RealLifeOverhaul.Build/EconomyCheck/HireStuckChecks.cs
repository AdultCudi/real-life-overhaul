using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;
using RealLifeOverhaul.Construction;

// A hired builder's site that stood still at 40% (the owner's report, 25 Oct 2026, on the city build): project 2, Warehouses at
// 2 Maple Street on lot uptown_1_04_0004 (36 by 35 m), plans of 1513 m2 over 4 storeys (the estimate 1338000 at the day's
// prices), so its site 28 was set out with 49 working hours (the civic line said 349: the programme's own hours for a plot,
// written before the builder's replaced them); builder 1 hired on day 26 at seven for 1610780, quoting 4 working days of works,
// its crews at 1208 in 1000 of the city's pace; the player asleep 24 hours at a time, the site at 40% from day 29 to day 39.
// Every stage's end held the works in that stage, whose end they could not pass (PlansRules.StageAt at the progress less a
// hair, the hour capped at the stage's end), so the plot stood at the end of its groundworks for good; a failed inspection
// held its site at the stage's end with the rework never done. Here: the rule that moves the works on, the owner's build from
// the hire with the player asleep, the owner's save as it stands picking up from 40% with nothing paid twice or skipped, a
// failure put right, the quote's days and due day from the site's own schedule, and the sources.
internal static partial class Program
{
    private const int OwnersProject = 2, OwnersBuilder = 1, OwnersArea = 1513, OwnersStoreys = 4, OwnersBidDays = 4, OwnersPace = 1208, OwnersHireDay = 26;
    private const long OwnersPrice = 1610780;

    private static void HireStuck()
    {
        HireStageEndChecks();
        HireOwnersBuildChecks();
        HireOwnersSaveChecks();
        HireFailedChecks();
        HireScheduleChecks();
        HireStuckSourceChecks();
    }

    // The rule: at a stage's end the works go on into the next stage unless the inspection that ends it has not passed.
    private static void HireStageEndChecks()
    {
        Check(PlansRules.WorkingStage(ProjectKind.Plot, 0.4, 0) == WorkStage.Foundation && PlansRules.WorkingStage(ProjectKind.Plot, 0.4 - 1e-7, 0) == WorkStage.Foundation
              && PlansRules.WorkingStage(ProjectKind.Plot, 0.39, 0) == WorkStage.Ground,
            "hire stuck: at the end of a plot's groundworks (40%, which no inspection ends) its works are in its foundations (they were held in their groundworks for good)");
        Check(PlansRules.WorkingStage(ProjectKind.Plot, 0.6, 0) == WorkStage.Foundation && PlansRules.WorkingStage(ProjectKind.Plot, 0.6, 1 << (int)Inspection.Foundation) == WorkStage.Frame
              && PlansRules.WorkingStage(ProjectKind.Plot, 0.8, 1 << (int)Inspection.Foundation) == WorkStage.Frame
              && PlansRules.WorkingStage(ProjectKind.Plot, 0.8, (1 << (int)Inspection.Foundation) | (1 << (int)Inspection.Frame)) == WorkStage.Envelope,
            "hire stuck: a stage's end holds the works only until its inspection passes, then they go on into the next stage");
        Check(PlansRules.WorkingStage(ProjectKind.Plot, 0.7, 0) == WorkStage.Foundation && PlansRules.WorkingStage(ProjectKind.Plot, 0.95, 1 << (int)Inspection.Foundation) == WorkStage.Frame,
            "hire stuck: a site past an inspection it never had is held at that stage's end for it: no inspection is skipped");
        Check(PlansRules.WorkingStage(ProjectKind.Rebuild, 0.2, 0) == WorkStage.Foundation && PlansRules.WorkingStage(ProjectKind.Contract, 0.25, 0) == WorkStage.Foundation
              && PlansRules.WorkingStage(ProjectKind.Contract, 0.75, 0) == WorkStage.Envelope && PlansRules.WorkingStage(ProjectKind.Contract, 0.9999, 0) == WorkStage.Envelope,
            "hire stuck: a rebuild's groundworks (20%) and the city's works' first stages (25%, 50%, 75%) go on into the next stage the same way");
        Check(PlansRules.Inspections(ProjectKind.Plot) == 3 && PlansRules.Inspections(ProjectKind.Rebuild) == 3 && PlansRules.Inspections(ProjectKind.Contract) == 1
              && PlansRules.Passed(new[] { InspectionState.Passed, InspectionState.Booked, InspectionState.Passed }) == 5 && PlansRules.Passed(null) == 0,
            "hire stuck: a building has three inspections and the city's works one; the inspections passed are read as bits");
        // The owner's site at the end of its groundworks: an hour of its works goes on.
        float needed = PlansRules.Hours(BuildUse.Warehouses, OwnersArea, OwnersStoreys, ProjectKind.Plot), done = needed * 0.4f;
        WorkStage stage = PlansRules.WorkingStage(ProjectKind.Plot, done / (double)needed, 0);
        SiteHour hour = SiteRules.Hour(ProjectKind.Plot, stage, done, needed, 1.0, OwnersPace, 1.0, InspectionState.NotDue, 0, 0, 29, 14);
        Check(needed == 49f && stage == WorkStage.Foundation && hour.Call == SiteCall.Worked && Math.Abs(hour.Hours - 1.208) < 1e-6,
            "hire stuck: the owner's site of " + needed + " working hours at 40% works 1.208 hours in its next working hour, in its foundations (" + stage + ", " + hour.Call + ", "
            + hour.Hours.ToString("0.###") + ")");
        SiteHour held = SiteRules.Hour(ProjectKind.Plot, WorkStage.Foundation, needed * 0.6f, needed, 1.0, OwnersPace, 1.0, InspectionState.NotDue, 0, 0, 30, 9);
        SiteHour waits = SiteRules.Hour(ProjectKind.Plot, WorkStage.Foundation, needed * 0.6f, needed, 1.0, OwnersPace, 1.0, InspectionState.Booked, 31, 0, 31, 9);
        SiteHour visit = SiteRules.Hour(ProjectKind.Plot, WorkStage.Foundation, needed * 0.6f, needed, 1.0, OwnersPace, 1.0, InspectionState.Booked, 31, 0, 31, 10);
        SiteHour late = SiteRules.Hour(ProjectKind.Plot, WorkStage.Foundation, needed * 0.6f, needed, 1.0, OwnersPace, 1.0, InspectionState.Booked, 31, 0, 33, 7);
        Check(held.Call == SiteCall.Due && held.Inspection == (int)Inspection.Foundation && held.Hours == 0.0 && waits.Call == SiteCall.Held && visit.Call == SiteCall.Visit
              && late.Call == SiteCall.Visit, "hire stuck: at the foundations' end the inspection falls due, waits, and the inspector comes from ten on the day booked or any hour after");
        SiteHour done1 = SiteRules.Hour(ProjectKind.Plot, WorkStage.Envelope, needed * 0.9995f, needed, 1.0, OwnersPace, 1.0, InspectionState.Passed, 0, 0, 40, 11);
        Check(done1.Call == SiteCall.Done && done1.Hours > 0.0 && needed * 0.9995f + (float)done1.Hours >= needed, "hire stuck: the final inspection passed, the works are done in that hour");
        Check(SiteRules.Visits(5, 10, 5) && !SiteRules.Visits(5, 9, 5) && SiteRules.Visits(6, 7, 5) && !SiteRules.Visits(4, 12, 5), "hire stuck: the inspector's visit: from ten on its day");
        Check(SiteRules.WorkingHour(1, 7, 0) && !SiteRules.WorkingHour(1, 17, 0) && SiteRules.WorkingHour(1, 18, 1) && !SiteRules.WorkingHour(1, 7, -1) && !SiteRules.WorkingHour(6, 10, 0)
              && Math.Abs(SiteRules.DayHours(0) - 10.0) < 1e-9 && Math.Abs(SiteRules.DayHours(1) - 12 * 1.35) < 1e-4 && Math.Abs(SiteRules.DayHours(-1) - 8 * 0.75) < 1e-9,
            "hire stuck: the programme's working hours and a working day's hours at its speed, by the building code (CivicCatalogue's)");
    }

    // The owner's project from the hire, the player asleep 24 hours at a time: it goes on through every stage to its keys.
    private static void HireOwnersBuildChecks()
    {
        HiredSim sim = OwnersHire(477835107, false);
        sim.Awake(27L * 24 + 8);
        for (long wake = 28L * 24 + 8; wake <= 70L * 24 + 8 && sim.Handed < 0; wake += 24)
        {
            sim.Asleep(wake);
        }
        ProjectFile p = sim.P;
        int due = CommissionRules.Due(ProjectKind.Plot, 49, CommissionRules.Promised(CommissionRules.UsualDays(49, 0), OwnersBidDays), 0, OwnersHireDay, 8);
        Check(sim.Handed > 0, "hire stuck: the owner's project, the player asleep 24 hours at a time from day 28, goes past 40% and is handed over (on day " + sim.Handed + ", the site at "
                              + (sim.Done / sim.Needed * 100f).ToString("0") + "%)");
        Check(sim.Stages.Count == 4 && sim.Stages[0] == WorkStage.Ground && sim.Stages[1] == WorkStage.Foundation && sim.Stages[2] == WorkStage.Frame && sim.Stages[3] == WorkStage.Envelope,
            "hire stuck: each of its four stages begins in turn (" + string.Join(", ", sim.Stages) + ")");
        Check(sim.Booked == 3 + sim.Failed && sim.Visited == 3 + sim.Failed && p.Insp[0] == InspectionState.Passed && p.Insp[1] == InspectionState.Passed && p.Insp[2] == InspectionState.Passed,
            "hire stuck: its builder books every inspection itself the hour it falls due and each passes in the end (" + sim.Booked + " booked, " + sim.Visited + " visits, " + sim.Failed + " failed)");
        long stages = CommissionRules.StagePay(OwnersPrice, 0) + CommissionRules.StagePay(OwnersPrice, 1) + CommissionRules.StagePay(OwnersPrice, 2);
        Check(p.HireStages == 7 && sim.StagePaid[0] == CommissionRules.StagePay(OwnersPrice, 0) && sim.StagePaid[1] == CommissionRules.StagePay(OwnersPrice, 1)
              && sim.StagePaid[2] == CommissionRules.StagePay(OwnersPrice, 2) && p.HirePaid == OwnersPrice - sim.Penalty && CommissionRules.Deposit(OwnersPrice) + stages + sim.Settled - sim.Back == OwnersPrice - sim.Penalty,
            "hire stuck: each stage paid once as its inspection passes, and the contract settled to its price less any penalty (paid " + p.HirePaid + ")");
        Check(sim.Short == 0 && p.WaitHours == 0, "hire stuck: a hired builder's site never stands still with nothing to wait for (" + sim.Short + " hour(s))");
        Check(sim.Failed > 0 || sim.Handed <= due, "hire stuck: passing first time, asleep, it is handed over by its due day by its site's schedule (handed on day " + sim.Handed + ", due " + due + ")");
        // As the old director dated a booking from the hour the catch-up ran (the waking hour), the same builder asleep is late.
        HiredSim old = OwnersHire(477835107, true);
        old.Awake(27L * 24 + 8);
        for (long wake = 28L * 24 + 8; wake <= 70L * 24 + 8 && old.Handed < 0; wake += 24)
        {
            old.Asleep(wake);
        }
        Check(old.Handed > sim.Handed, "hire stuck: dated by the hour it is worked, a booking keeps the days asleep the site would keep awake (a booking dated from the waking hour hands over on day "
                                       + old.Handed + ", not " + sim.Handed + ")");
        // The same, awake throughout: asleep is never the later.
        HiredSim awake = OwnersHire(477835107, false);
        awake.Awake(70L * 24);
        Check(awake.Handed > 0 && sim.Handed <= awake.Handed, "hire stuck: asleep, it is handed over no later than awake throughout (day " + sim.Handed + " against " + awake.Handed + ")");
    }

    // The owner's save as it stands: project 2 in its groundworks, its site at 40%, every inspection to come, the deposit paid, 83
    // working hours stood still, due on day 36; loaded on day 39 at seven. It picks up from 40%, nothing paid twice or skipped.
    private static void HireOwnersSaveChecks()
    {
        var p = OwnersProjectFile();
        p.StageSeen = (int)WorkStage.Ground;
        p.DueDay = 36;
        p.HireDays = OwnersBidDays;
        p.HirePaid = CommissionRules.Deposit(OwnersPrice);
        p.WaitHours = 83;
        float needed = p.Hours;
        var sim = new HiredSim(p, needed * 0.4f, 477835107, BuildCatalogue.RivalReputation[OwnersBuilder], 0, 39L * 24 + 6, false);
        sim.Awake(39L * 24 + 22);
        for (long wake = 40L * 24 + 7; wake <= 80L * 24 + 7 && sim.Handed < 0; wake += 24)
        {
            sim.Asleep(wake);
        }
        int late = sim.Handed - 36;
        long penalty = CommissionRules.Penalty(OwnersPrice, late);
        Check(sim.Resumed == 1 && p.WaitHours == 0, "hire stuck: the owner's save: its site held at 40% is noticed once at its next working hour and told (" + sim.Resumed + ")");
        Check(sim.Stages.Count == 3 && sim.Stages[0] == WorkStage.Foundation && sim.Stages[1] == WorkStage.Frame && sim.Stages[2] == WorkStage.Envelope,
            "hire stuck: the owner's save: its works go on from 40% into the foundations, then the frame, then the shell (" + string.Join(", ", sim.Stages) + ")");
        Check(p.Insp[0] == InspectionState.Passed && p.Insp[1] == InspectionState.Passed && p.Insp[2] == InspectionState.Passed && sim.Booked == 3 + sim.Failed,
            "hire stuck: the owner's save: each of the three inspections booked by the builder and passed, none skipped (" + sim.Booked + " booked, " + sim.Failed + " failed)");
        Check(sim.Handed > 39 && p.HireStages == 7 && sim.Penalty == penalty && p.HirePaid == OwnersPrice - penalty
              && sim.StagePaid[0] == 402695 && sim.StagePaid[1] == 483234 && sim.StagePaid[2] == 483234 && CommissionRules.Deposit(OwnersPrice) == 161078,
            "hire stuck: the owner's save: the deposit not taken again, each stage paid once (402695, 483234, 483234), handed over on day " + sim.Handed + ", " + late
            + " day(s) late, the penalty " + penalty + " off the price: " + sim.Settled + " paid at the handover, " + sim.Back + " back to the player, " + p.HirePaid + " paid in all");
    }

    // A failed inspection is put right at the stage's end and the inspector asked again (it was held there for good).
    private static void HireFailedChecks()
    {
        float needed = 49f, done = needed * 0.6f;
        int rework = SiteRules.Rework(PlansRules.StageHours(49, ProjectKind.Plot, WorkStage.Foundation));
        int hours = 0;
        SiteHour h = default(SiteHour);
        for (int left = rework; hours < 100; hours++)
        {
            h = SiteRules.Hour(ProjectKind.Plot, WorkStage.Foundation, done, needed, 1.0, OwnersPace, 1.0, InspectionState.Failed, 31, left, 31, 11 + hours % 6);
            left = h.Rework;
            if (h.Call != SiteCall.Rework)
            {
                break;
            }
        }
        Check(rework > 0 && h.Call == SiteCall.Redo && h.Rework == 0 && h.Hours == 0.0 && hours < 10,
            "hire stuck: a failed inspection is put right at the stage's end in " + (hours + 1) + " working hour(s) (" + rework + " tenths at 1208) and falls due again");
        SiteHour idle = SiteRules.Hour(ProjectKind.Plot, WorkStage.Foundation, done, needed, 1.0, 0, 1.0, InspectionState.Failed, 31, rework, 31, 11);
        Check(idle.Call == SiteCall.Held && idle.Rework == rework, "hire stuck: with nobody at work a failure waits, its rework as it was");
        // Every seed's owner's build ends handed over, its failures put right and booked again.
        int handed = 0, failures = 0;
        for (int seed = 1; seed <= 60; seed++)
        {
            HiredSim sim = OwnersHire(seed * 7919, false);
            sim.Awake(90L * 24);
            handed += sim.Handed > 0 && sim.P.HireStages == 7 && sim.Booked == 3 + sim.Failed ? 1 : 0;
            failures += sim.Failed;
        }
        Check(handed == 60 && failures > 0, "hire stuck: over 60 saves' seeds every owner's build is handed over, " + failures + " failed inspection(s) among them put right and passed");
    }

    // The quote, the due day, the site's progress and its lateness from one rule: the site's own schedule.
    private static void HireScheduleChecks()
    {
        int usual = CommissionRules.UsualDays(49, 0), promised = CommissionRules.Promised(usual, OwnersBidDays);
        int days = CommissionRules.Days(ProjectKind.Plot, 49, promised, 0);
        int due = CommissionRules.Due(ProjectKind.Plot, 49, promised, 0, OwnersHireDay, 8);
        int oldDue = PlanningRules.AfterWorkingDays(OwnersHireDay, OwnersBidDays + 2);
        Check(usual == TenderRules.StandardDays(49) && usual == 5 && promised == 1250,
            "hire stuck: the owner's quote: 49 working hours are 5 usual working days in the city's day, so its 4 days of works need 1250 in 1000 of the city's pace (its crews kept 1208)");
        Check(days >= OwnersBidDays + 3 && due > oldDue,
            "hire stuck: the owner's quote reads " + days + " working days to the keys, its inspections' waits in them, due on day " + due + " (the old due day " + oldDue
            + " had a builder keeping its quote late)");
        // A builder keeping the pace its bid needs is never late, whatever the hours, the code, the day and the hour it starts.
        string lateOne = null;
        int runs = 0;
        foreach (int hours in new[] { 40, 49, 75, 120, 213, 330, 480 })
        {
            foreach (int permits in new[] { -1, 0, 1 })
            {
                int u = CommissionRules.UsualDays(hours, permits);
                foreach (int bid in new[] { Math.Max(1, u * 9 / 10), u, u * 115 / 100 + 1 })
                {
                    int pace = CommissionRules.Promised(u, bid);
                    for (int start = 26; start < 33; start += 2)
                    {
                        foreach (int at in new[] { 0, 7, 12, 15, 16, 20 })
                        {
                            int dueDay = CommissionRules.Due(ProjectKind.Plot, hours, pace, permits, start, at);
                            HiredSim sim = new HiredSim(ScheduleProject(hours, pace, dueDay), 0f, 1, 100, permits, (long)start * 24 + at - 1, false) { AlwaysPass = true };
                            sim.Awake((long)(dueDay + 60) * 24);
                            runs++;
                            if (sim.Handed < 0 || sim.Handed > dueDay)
                            {
                                lateOne = lateOne ?? hours + " h, permits " + permits + ", bid " + bid + " days, from day " + start + " at " + at + ": due " + dueDay + ", handed " + sim.Handed;
                            }
                        }
                    }
                }
            }
        }
        Check(lateOne == null, "hire stuck: over " + runs + " sites a builder keeping its quoted pace hands over by its due day, awake (" + (lateOne ?? "none late") + ")");
        // Strict permits: a tender's ten hours a day (the old quote) against the city's eight at three quarters speed.
        int strictUsual = CommissionRules.UsualDays(120, -1), oldUsual = TenderRules.StandardDays(120);
        int strictDue = CommissionRules.Due(ProjectKind.Plot, 120, CommissionRules.Promised(strictUsual, strictUsual), -1, 26, 8);
        HiredSim strict = new HiredSim(ScheduleProject(120, CommissionRules.Promised(oldUsual, oldUsual), PlanningRules.AfterWorkingDays(26, oldUsual + 2)), 0f, 1, 100, -1, 26L * 24 + 7, false) { AlwaysPass = true };
        strict.Awake(200L * 24);
        Check(strictUsual == 20 && oldUsual == 12 && strict.Handed > strict.P.DueDay && strictDue >= strict.Handed,
            "hire stuck: under strict permits 120 hours are 20 working days, not a tender's 12: a builder at the old quote's pace was late (handed " + strict.Handed + ", due "
            + strict.P.DueDay + "); the schedule's due day " + strictDue + " covers it");
        // The quote reads the same all week, and the days follow the pace.
        Check(CommissionRules.Days(ProjectKind.Plot, 300, 1000, 0) == CommissionRules.Days(ProjectKind.Plot, 300, 1000, 0) && CommissionRules.Days(ProjectKind.Plot, 300, 1400, 0) < CommissionRules.Days(ProjectKind.Plot, 300, 800, 0)
              && CommissionRules.Days(ProjectKind.Plot, 300, 1000, -1) > CommissionRules.Days(ProjectKind.Plot, 300, 1000, 0) && CommissionRules.Days(ProjectKind.Plot, 300, 1000, 1) < CommissionRules.Days(ProjectKind.Plot, 300, 1000, 0),
            "hire stuck: a quote's days are the same every look, fewer at a faster pace or under fast-track permits, more under strict ones");
        Check(CommissionRules.Promised(5, 4) == 1250 && CommissionRules.Promised(5, 1) == BuildCatalogue.HirePaceMost && CommissionRules.Promised(1, 50) == BuildCatalogue.HirePaceLeast
              && CommissionRules.UsualDays(0, 0) == 1 && CommissionRules.UsualDays(10, 0) == 1 && CommissionRules.UsualDays(11, 0) == 2,
            "hire stuck: the pace a bid needs is bounded as a builder's pace is; the usual days of no hours are one");
        Check(SiteRules.HandoverDay(ProjectKind.Plot, 480, BuildCatalogue.HirePaceLeast, -1, 1, 0) < 1 + BuildCatalogue.ScheduleMostDays && SiteRules.WorkingDays(29, 33) == 5 && SiteRules.WorkingDays(27, 28) == 0
              && SiteRules.WorkingDays(9, 8) == 0, "hire stuck: the slowest schedule ends inside its bound; working days are counted Monday to Friday");
        var timer = System.Diagnostics.Stopwatch.StartNew();
        long sum = 0;
        for (int i = 0; i < 800; i++)
        {
            sum += CommissionRules.Days(ProjectKind.Plot, 40 + i % 440, 400 + i % 1200, i % 3 - 1);
        }
        timer.Stop();
        Check(sum > 0 && timer.Elapsed.TotalMilliseconds < 400.0, "hire stuck: 800 quotes' days in " + timer.Elapsed.TotalMilliseconds.ToString("0") + " ms (a quotes page asks for eight)");
    }

    // The sources: the director works a site's hour by the rules at the hour the programme works it, books at that hour, holds
    // a builder's site while faulted, is told the day a site was handed over, tells the player of each stage, booking, delay and
    // pick-up; the programme names the hours a builder's site is set out with.
    private static void HireStuckSourceChecks()
    {
        string sites = ModSource(@"Construction\ConstructionDirector.Sites.cs"), hire = ModSource(@"Construction\ConstructionDirector.Hire.cs");
        string day = ModSource(@"Construction\ConstructionDirector.Day.cs"), works = ModSource(@"Civic\CivicWorks.cs"), wiring = ModSource(@"Wiring\Wiring.ConstructionLinks.cs");
        string landmarks = ModSource(@"Wiring\Wiring.LandmarkLinks.cs"), builders = ModSource(@"Civic\SiteBuilders.cs");
        string work = MethodText(sites, "private float Work(");
        Check(work.Contains("PlansRules.WorkingStage(model, done / needed, PlansRules.Passed(p.Insp))") && work.Contains("SiteRules.Hour(model, stage, done, needed, hours, pace, share, held,")
              && !work.Contains("StageAt(") && work.Contains("Inspect(p, work.Inspection, day);") && work.Contains("InspectionDue(p, work.Inspection, day, hour);"),
            "hire stuck sources: a site's working hour is the rules' (the working stage, the hour's call), the inspector's visit and a booking at the hour worked");
        Check(MethodText(sites, "public float WorkOn(").Contains("int day, int hour") && MethodText(sites, "public float WorkOn(").Contains("_state.OnSite(siteId) != null || DevOnSite(siteId) != null) ? 0f : hours")
              && MethodText(hire, "private void HiredBooks(").Contains("SiteRules.InspectorDay(day, hour)") && !MethodText(hire, "private void HiredBooks(").Contains("ConstructionGame.Day"),
            "hire stuck sources: the programme says the hour it works; a builder books from that hour; a faulted construction game holds its builders' sites");
        Check(Regex.IsMatch(works, @"SiteBuilders\.WorkOn\(site, Speed\(site\), \(long\)day \* 24 \+ hour\)") && builders.Contains("public static Func<SiteState, float, long, float> Hours;")
              && wiring.Contains("d.WorkOn(site.Id, site.Done, site.Needed, hours, (int)(at / 24), (int)(at % 24))") && builders.Contains("public static Action<SiteState, int> Handed;")
              && wiring.Contains("SiteBuilders.Handed = (site, day) => Director?.SiteHanded(site.Id, day);") && works.Contains("SiteBuilders.HandedOver(site, day);"),
            "hire stuck sources: the programme passes the hour it works and the day a site was handed over, through the wiring");
        Check(works.Contains("Needed = HoursOf(kind, hours, ref random),") && works.Contains("Needed = hours > 0f ? hours : Mathf.Round(") && !wiring.Contains("site.Needed = hours;")
              && !landmarks.Contains("site.Needed = hours;") && wiring.Contains("out why, hours)") && landmarks.Contains("out why, hours)"),
            "hire stuck sources: a site is set out with its builder's hours and its line says them (it said the programme's 349 for a plot of 49)");
        Check(MethodText(hire, "private int Bids(").Contains("CommissionRules.UsualDays(SiteHours(p), Permits())") && MethodText(hire, "public int Quotes(").Contains("CommissionRules.Days(p.Model, hours, CommissionRules.Promised(usual, into[r].Days), permits)")
              && MethodText(hire, "public string HireBuilder(").Contains("p.DueDay = CommissionRules.Due(p.Model, p.Hours, promised, permits,") && !hire.Contains("StandardDays")
              && !MethodText(hire, "public string HireBuilder(").Contains("AfterWorkingDays"),
            "hire stuck sources: the quote, its days and the due day come from the site's own schedule in the city's working day");
        Check(MethodText(day, "private void DayWork(").Contains("HiresDay(day, before);") && MethodText(sites, "private float Work(").Contains("HireResumed(p, done / needed, day);")
              && MethodText(hire, "private void HiredStage(").Contains("\"reallife_bld_notice_hire_begun\"") && MethodText(hire, "private void HiredBooks(").Contains("\"reallife_bld_notice_hire_booked\"")
              && MethodText(hire, "private void HiresDay(").Contains("\"reallife_bld_notice_hire_overdue\"") && MethodText(hire, "private void HireResumed(").Contains("\"reallife_bld_notice_hire_resumed\""),
            "hire stuck sources: the player hears of each stage, each booking, the day the builder runs late and a held site picked up again");
        Check(MethodText(day, "private void HourWork(").Contains("SiteRules.Visits(day, hour, p.InspDay[k])") && !day.Contains("InspectorHour =") && MethodText(sites, "public void SiteHanded(").Contains("int day)"),
            "hire stuck sources: the director's clock has the inspector by the same rule; a handover is dated by the day that finished it");
        string lost = MethodText(sites, "private void SitesLost(");
        Check(MethodText(day, "private void DayWork(").Contains("SitesLost();") && lost.Contains("if (SiteCount() <= 0)") && lost.Contains("SiteOf(p.SiteId).Known")
              && lost.Contains("StartSite(p, Math.Max(0, p.Contractor), out string why)") && lost.Contains("SetSiteDone(site, (float)(from * p.Hours));")
              && wiring.Contains("ConstructionBridges.SiteDone = SiteDone;") && wiring.Contains("ConstructionBridges.SiteDone = null;"),
            "hire stuck sources: a building whose site the programme lost is set out again each day where its stage began (never while the programme holds no site at all)");
        Check(!Regex.IsMatch(ModSource(@"Construction\SiteRules.cs") + ModSource(@"Construction\PlansRules.cs") + ModSource(@"Construction\CommissionRules.cs"), @"\bUnityEngine\b|\bRandom\.|DateTime|TickCount|\bTime\.|GetHashCode|new [A-Z][A-Za-z]*\[")
              && !Regex.IsMatch(hire + sites, @"using System\.Linq|\.Where\(|\.Select\(|void Update\(|FindObjectsOfType|FindObjectsByType|\bHarmony\b"),
            "hire stuck sources: the rules are game-free and allocate nothing; no LINQ, no Update, no scene search, no patch");
    }

    // ------------------------------------------------------------------ the owner's numbers

    private static ProjectFile OwnersProjectFile()
    {
        var p = new ProjectFile
        {
            Id = OwnersProject, Kind = ProjectKind.Plot, Stage = ProjectStage.Building, Lot = "uptown_1_04_0004", Use = BuildUse.Warehouses, Width = 36, Depth = 35, Area = OwnersArea,
            Storeys = OwnersStoreys, Quality = 88, Estimate = 1338000, Contractor = OwnersBuilder, ContractorPrice = OwnersPrice, HirePace = OwnersPace, SiteId = 28, StartDay = OwnersHireDay
        };
        p.Hours = PlansRules.Hours(p.Use, p.Area, p.Storeys, p.Kind);
        return p;
    }

    // The owner's hire as the fixed director makes it: due on the day its site's schedule hands it over at its quote's pace.
    private static HiredSim OwnersHire(int seed, bool bookAtWaking)
    {
        ProjectFile p = OwnersProjectFile();
        int usual = CommissionRules.UsualDays(p.Hours, 0);
        p.HireDays = CommissionRules.Days(p.Model, p.Hours, CommissionRules.Promised(usual, OwnersBidDays), 0);
        p.DueDay = CommissionRules.Due(p.Model, p.Hours, CommissionRules.Promised(usual, OwnersBidDays), 0, OwnersHireDay, 8);
        p.HirePaid = CommissionRules.Deposit(OwnersPrice);
        return new HiredSim(p, 0f, seed, BuildCatalogue.RivalReputation[OwnersBuilder], 0, OwnersHireDay * 24L + 7, bookAtWaking);
    }

    private static ProjectFile ScheduleProject(int hours, int pace, int due)
    {
        return new ProjectFile
        {
            Id = 9, Kind = ProjectKind.Plot, Stage = ProjectStage.Building, Use = BuildUse.Offices, Area = 1000, Storeys = 4, Hours = hours, Quality = 80, Contractor = 0,
            ContractorPrice = 1000000, HirePace = pace, DueDay = due, HirePaid = CommissionRules.Deposit(1000000)
        };
    }

    // A hired builder's site as the game runs it, without the game: the programme's working hours go through the director's working
    // hour (ConstructionDirector.Work and its HiredBooks, Inspect and HireStagePaid, here as they are there, on PlansRules.WorkingStage
    // and SiteRules.Hour), and the director's own clock has the inspector's visits too. Asleep, the programme stands still (the civic
    // director counts in the game's scaled time, which the game's time machine stops) and works the sleep's hours at once on waking,
    // while the director's clock goes on hour by hour (unscaled time). The site's hours are kept in floats, as the programme keeps them.
    private sealed class HiredSim
    {
        public readonly ProjectFile P;
        public float Done;
        public readonly float Needed;
        public readonly long[] StagePaid = new long[(int)Inspection.Count];
        public readonly List<WorkStage> Stages = new List<WorkStage>(6);
        public int Handed = -1, Resumed, Booked, Visited, Failed, Short;
        public long Settled, Back, Penalty;
        public bool AlwaysPass;
        private readonly int _seed, _reputation, _permits;
        private readonly bool _bookAtWaking;
        private long _clock, _programme;

        public HiredSim(ProjectFile p, float done, int seed, int reputation, int permits, long lastWorked, bool bookAtWaking)
        {
            P = p;
            Done = done;
            Needed = p.Hours;
            _seed = seed;
            _reputation = reputation;
            _permits = permits;
            _clock = lastWorked;
            _programme = lastWorked;
            _bookAtWaking = bookAtWaking;
        }

        // Awake: each hour as it comes, the programme's and then the director's.
        public void Awake(long until)
        {
            while (_clock < until && Handed < 0)
            {
                _clock++;
                _programme = _clock;
                Programme(_clock, _clock);
                Clock(_clock);
            }
        }

        // Asleep until the waking hour: the director's clock hour by hour, then the programme catches the hours up at once.
        public void Asleep(long wake)
        {
            for (long t = _clock + 1; t <= wake && Handed < 0; t++)
            {
                Clock(t);
            }
            _clock = wake;
            for (long t = _programme + 1; t <= wake && Handed < 0; t++)
            {
                Programme(t, wake);
            }
            _programme = wake;
        }

        private void Clock(long t)
        {
            int d = (int)(t / 24), h = (int)(t % 24);
            for (int k = 0; k < (int)Inspection.Count && Handed < 0; k++)
            {
                if (P.Insp[k] == InspectionState.Booked && SiteRules.Visits(d, h, P.InspDay[k]))
                {
                    Inspect(k);
                }
            }
        }

        private void Programme(long t, long now)
        {
            int d = (int)(t / 24), h = (int)(t % 24);
            if (Handed >= 0 || !SiteRules.WorkingHour(d, h, _permits))
            {
                return;
            }
            Done += Math.Max(0f, Work(d, h, now));
            if (Done >= Needed)
            {
                Handover(d);
            }
        }

        private float Work(int day, int hour, long now)
        {
            ProjectKind model = P.Model;
            if (P.WaitHours > 0)
            {
                Resumed++;
                P.WaitHours = 0;
            }
            WorkStage stage = PlansRules.WorkingStage(model, Done / (double)Needed, PlansRules.Passed(P.Insp));
            if ((int)stage != P.StageSeen)
            {
                P.StageSeen = (int)stage;
                Stages.Add(stage);
            }
            int inspection = PlansRules.InspectionAfter(model, stage);
            InspectionState held = inspection >= 0 ? P.Insp[inspection] : InspectionState.Passed;
            SiteHour work = SiteRules.Hour(model, stage, Done, Needed, CivicCatalogue.SiteSpeed(_permits), P.HirePace, 1.0, held, inspection >= 0 ? P.InspDay[inspection] : 0, P.Rework, day, hour);
            P.Rework = work.Rework;
            switch (work.Call)
            {
                case SiteCall.Due:
                case SiteCall.Redo:
                    P.Insp[work.Inspection] = InspectionState.Booked;
                    P.InspDay[work.Inspection] = _bookAtWaking ? SiteRules.InspectorDay((int)(now / 24), (int)(now % 24)) : SiteRules.InspectorDay(day, hour);
                    Booked++;
                    break;
                case SiteCall.Visit:
                    Inspect(work.Inspection);
                    break;
                case SiteCall.Short:
                    P.WaitHours++;
                    Short++;
                    break;
            }
            return (float)work.Hours;
        }

        private void Inspect(int k)
        {
            int tries = P.InspTries[k]++;
            Visited++;
            int builder = CommissionRules.Quality(_reputation);
            int odds = SiteRules.PassOdds(SiteRules.WorkQuality(builder, builder, P.Quality), false, P.Visited, builder >= 60);
            if (AlwaysPass || SiteRules.Passes(_seed, P.Id, k, tries, odds))
            {
                P.Insp[k] = InspectionState.Passed;
                Paid(k);
                return;
            }
            P.Insp[k] = InspectionState.Failed;
            P.Fails++;
            Failed++;
            P.Rework = SiteRules.Rework(PlansRules.StageHours(P.Hours, P.Model, (WorkStage)Math.Max(0, P.StageSeen)));
        }

        private void Paid(int k)
        {
            if ((P.HireStages & (1 << k)) != 0)
            {
                return;
            }
            P.HireStages |= 1 << k;
            long pay = CommissionRules.StagePay(P.ContractorPrice, k);
            StagePaid[k] += pay;
            P.HirePaid += pay;
        }

        private void Handover(int day)
        {
            Handed = day;
            for (int k = 0; k < (int)Inspection.Count; k++)
            {
                Paid(k);
            }
            Penalty = CommissionRules.Penalty(P.ContractorPrice, Math.Max(0, day - P.DueDay));
            Settled = CommissionRules.Settle(P.ContractorPrice, P.HirePaid, Penalty, out long back);
            Back = back;
            P.HirePaid += Settled - back;
        }
    }
}
