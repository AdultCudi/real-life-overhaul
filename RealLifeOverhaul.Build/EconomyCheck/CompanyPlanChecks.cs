using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Economy;

// Named rota plans of a business (the Company app on the kit): which plan is in use for a week (dated, repeating every week or alternate weeks), the cap it
// puts on each person's hours (overtime past it), the floor of crew it puts under the planner's need, a copy of a plan, and the save line.
internal static partial class Program
{
    private static void CompanyPlans()
    {
        int before = _run, failedBefore = _failed;
        PlanInUse();
        PlanCaps();
        PlanFloor();
        PlanEditing();
        PlanSaves();
        Console.WriteLine("  rota plans: in use, the cap, the floor, copies and the save: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    private static RotaPlanSet PlanSet(params RotaPlanFile[] plans)
    {
        var set = new RotaPlanSet { Place = "Main~1" };
        foreach (RotaPlanFile p in plans)
        {
            set.Plans.Add(p);
            set.NextId = Math.Max(set.NextId, p.Id + 1);
        }
        set.Active = plans.Length > 0 ? plans[0].Id : 0;
        return set;
    }

    private static void PlanInUse()
    {
        var standard = new RotaPlanFile { Id = 1, Name = "Standard week" };
        var holiday = new RotaPlanFile { Id = 2, Name = "Holiday week", From = 220, To = 227 };
        var lean = new RotaPlanFile { Id = 3, Name = "Lean week" };
        RotaPlanSet set = PlanSet(standard, holiday, lean);
        Check(RotaPlanRules.InUse(set, 214) == standard, "plan in use: the chosen plan when no dated one holds (day 214)");
        Check(RotaPlanRules.InUse(set, 222) == holiday, "plan in use: a dated plan that holds for the week is in use (day 222)");
        Check(RotaPlanRules.InUse(set, 218) == holiday, "plan in use: a dated plan holds for the whole week it touches, the week of day 218 (Monday 218) includes 220");
        Check(RotaPlanRules.InUse(set, 211) == standard, "plan in use: the week before it does not hold (day 211, a Monday)");
        Check(RotaPlanRules.InUse(set, 229) == holiday && RotaPlanRules.InUse(set, 233) == standard, "plan in use: the week its last day falls in still holds (day 229); the next does not (day 233)");
        set.Active = 3;
        Check(RotaPlanRules.InUse(set, 100) == lean && RotaPlanRules.InUse(set, 222) == holiday, "plan in use: choosing another plan changes the fallback only, a dated plan still wins its days");
        // Two dated plans in one week: the later start, then the lower number.
        var late = new RotaPlanFile { Id = 4, Name = "Late", From = 223, To = 230 };
        set.Plans.Add(late);
        Check(RotaPlanRules.InUse(set, 224) == late, "plan in use: of two dated plans in a week, the one that begins later");
        // Alternate weeks: odd weeks and even weeks.
        var odd = new RotaPlanFile { Id = 1, Name = "Odd", Parity = RotaPlanRules.Odd };
        var even = new RotaPlanFile { Id = 2, Name = "Even", Parity = RotaPlanRules.Even };
        RotaPlanSet alt = PlanSet(odd, even);
        int week1 = 3, week2 = 10;   // days in the first and the second week
        Check(RotaPlanRules.WeekOf(1) == 0 && RotaPlanRules.WeekOf(7) == 0 && RotaPlanRules.WeekOf(8) == 1 && RotaPlanRules.WeekOf(214) == 30,
            "plan weeks: day 1 is the first week, a week is seven days, day 214 is in the week the page calls 31");
        Check(RotaPlanRules.InUse(alt, week1) == odd && RotaPlanRules.InUse(alt, week2) == even && RotaPlanRules.InUse(alt, 214) == odd,
            "plan in use: alternate weeks take turns, the first week odd");
        alt.Active = 2;
        Check(RotaPlanRules.InUse(alt, week1) == odd && RotaPlanRules.InUse(alt, week2) == even, "plan in use: the chosen plan only where it applies; another that does takes the other weeks");
        RotaPlanSet only = PlanSet(even);
        Check(RotaPlanRules.InUse(only, week1) == null && RotaPlanRules.InUse(only, week2) == even, "plan in use: a business whose only plan skips this week has none, and the planner's own rules stand");
        Check(RotaPlanRules.InUse(null, 5) == null && RotaPlanRules.InUse(new RotaPlanSet(), 5) == null, "plan in use: no plans, none in use");
        var bounded = new RotaPlanFile { Id = 9, From = 0, To = 20 };
        Check(RotaPlanRules.Applies(bounded, 1) && RotaPlanRules.Applies(bounded, 20) && !RotaPlanRules.Applies(bounded, 22), "plan days: a plan with an end and no start holds until that week is over");
        var begin = new RotaPlanFile { Id = 9, From = 30, To = 0 };
        Check(!RotaPlanRules.Applies(begin, 21) && RotaPlanRules.Applies(begin, 30) && RotaPlanRules.Applies(begin, 400), "plan days: a plan with a start and no end holds from that week on");
        // The same answer for the same figures, and every day of a year gives a plan or none, never a fault.
        bool steady = true;
        for (int day = 1; day <= 365; day++)
        {
            steady &= RotaPlanRules.InUse(set, day) == RotaPlanRules.InUse(set, day) && RotaPlanRules.InUse(alt, day) != null;
        }
        Check(steady, "plan in use: the same twice, and a business with an odd and an even plan has one every week of the year");
    }

    private static void PlanCaps()
    {
        var p = new RotaPlanFile { Hours = 40 };
        Check(RotaPlanRules.Cap(p) == 40 && RotaPlanRules.Cap(null) == 0 && RotaPlanRules.Cap(new RotaPlanFile()) == 0, "plan cap: the hours set, none for no plan, and their own hours (0) set no cap");
        p.Overtime = true;
        Check(RotaPlanRules.Cap(p) == 40 + RotaPlanRules.OvertimeExtra, "plan cap: overtime allows " + RotaPlanRules.OvertimeExtra + " hours more");
        p.Hours = 200;
        Check(RotaPlanRules.Cap(p) <= RotaPlanRules.HoursTop + RotaPlanRules.OvertimeExtra, "plan cap: never past what a week can hold");
        // Putting it on a person's demands.
        var none = new RotaPlanFile { Hours = 30 };
        RotaDemand[] open = { new RotaDemand { Kind = RotaDemandKind.Hours, A = 20, B = 0 } };
        RotaDemand[] capped = RotaPlanRules.Capped(open, none);
        Check(capped[0].B == 30 && capped[0].A == 20, "plan cap: an hours demand with no top gets the plan's as its top");
        RotaDemand[] wide = { new RotaDemand { Kind = RotaDemandKind.Hours, A = 35, B = 60 } };
        capped = RotaPlanRules.Capped(wide, none);
        Check(capped[0].B == 30 && capped[0].A == 30, "plan cap: a looser top comes down to the plan's, and the least with it");
        RotaDemand[] tight = { new RotaDemand { Kind = RotaDemandKind.Hours, A = 10, B = 24 } };
        capped = RotaPlanRules.Capped(tight, none);
        Check(capped[0].B == 24 && capped[0].A == 10, "plan cap: a person who asks for less than the cap keeps their own");
        RotaDemand[] days = { new RotaDemand { Kind = RotaDemandKind.Days, A = 4 } };
        capped = RotaPlanRules.Capped(days, none);
        Check(capped.Length == 2 && capped[1].Kind == RotaDemandKind.Hours && capped[1].B == 30 && capped[1].Priority == RotaCatalogue.PriorityHigh && capped[0].Kind == RotaDemandKind.Days,
            "plan cap: a person with no hours demand is given one, a limit the planner keeps, their others untouched");
        capped = RotaPlanRules.Capped(RotaWorker.Empty, none);
        Check(capped.Length == 1 && capped[0].B == 30, "plan cap: a person with no demands at all is given the one");
        var full = new RotaDemand[RotaCatalogue.DemandsMost];
        for (int i = 0; i < full.Length; i++)
        {
            full[i] = new RotaDemand { Kind = RotaDemandKind.FreeDays, Mask = 1UL };
        }
        Check(RotaPlanRules.Capped(full, none).Length == RotaCatalogue.DemandsMost, "plan cap: a person at the most demands is not given another");
        RotaDemand[] same = { new RotaDemand { Kind = RotaDemandKind.Hours, A = 20, B = 0 } };
        Check(ReferenceEquals(RotaPlanRules.Capped(same, new RotaPlanFile()), same) && ReferenceEquals(RotaPlanRules.Capped(same, null), same), "plan cap: with no cap the demands are left as they are");
    }

    private static void PlanFloor()
    {
        var p = new RotaPlanFile();
        p.Blocks.Add(new RotaBlock { From = 7, To = 10, Crew = 2 });
        p.Blocks.Add(new RotaBlock { From = 17, To = 21, Crew = 5 });
        var cells = new int[RotaCatalogue.Cells];
        RotaPlanRules.FloorCells(p, cells);
        bool right = true;
        for (int d = 0; d < RotaCatalogue.Days; d++)
        {
            for (int h = 0; h < RotaCatalogue.Hours; h++)
            {
                int want = h >= 7 && h < 10 ? 2 : h >= 17 && h < 21 ? 5 : 0;
                right &= cells[d * RotaCatalogue.Hours + h] == want;
            }
        }
        Check(right, "plan floor: the crew of each block in its hours of every day, nothing outside them");
        p.Blocks.Add(new RotaBlock { From = 8, To = 9, Crew = 4 });
        RotaPlanRules.FloorCells(p, cells);
        Check(cells[8] == 4 && cells[7] == 2 && cells[9] == 2, "plan floor: where blocks meet the larger crew stands");
        // Lifting a week's worth to the floor: three stations, one a cleaning one.
        const int stations = 4;
        var value = new int[RotaCatalogue.Cells * stations];
        var taken = new bool[RotaCatalogue.Cells * stations];
        var open = new bool[RotaCatalogue.Cells];
        var floor = new int[RotaCatalogue.Cells];
        var kind = new byte[] { RotaGroupKind.Service, RotaGroupKind.Service, RotaGroupKind.Cleaning };
        var groupOf = new[] { 0, 1, 1, 2 };   // station -> group (stations 0 service, 1 and 2 service, 3 cleaning)
        for (int h = 6; h < 22; h++)
        {
            open[h] = true;   // Monday 06 to 22
        }
        value[9 * stations + 0] = 7;   // one wanted at nine
        taken[9 * stations + 1] = true;   // and somebody the planner leaves alone at another station
        floor[9] = 3;
        floor[3] = 3;   // closed at three: no floor there
        floor[12] = 2;
        int added = RotaPlanRules.Lift(floor, open, stations, value, taken, kind, groupOf, 2);
        int wanted9 = 0;
        for (int s = 0; s < stations; s++)
        {
            wanted9 += value[9 * stations + s] > 0 || taken[9 * stations + s] ? 1 : 0;
        }
        Check(wanted9 == 3 && value[9 * stations + 2] == 2 && value[9 * stations + 3] == 0, "plan floor: an hour short of the crew has stations added at the least worth, never the cleaning one, and counts those left alone");
        Check(value[3 * stations + 0] == 0 && value[3 * stations + 1] == 0, "plan floor: a closed hour never asks for anybody");
        Check(added == 1 + 2, "plan floor: three stations were added in all (" + added + ")");
        floor[9] = 99;
        RotaPlanRules.Lift(floor, open, stations, value, taken, kind, groupOf, 2);
        wanted9 = 0;
        for (int s = 0; s < stations; s++)
        {
            wanted9 += value[9 * stations + s] > 0 || taken[9 * stations + s] ? 1 : 0;
        }
        Check(wanted9 <= 3, "plan floor: a floor past the stations there are asks for the stations there are, no more (" + wanted9 + ")");
        // The cost of lifting a week at the most stations.
        const int most = RotaCatalogue.StationsMost;
        var bigValue = new int[RotaCatalogue.Cells * most];
        var bigTaken = new bool[RotaCatalogue.Cells * most];
        var bigKind = new byte[most];
        var bigGroup = new int[most];
        var allOpen = new bool[RotaCatalogue.Cells];
        var allFloor = new int[RotaCatalogue.Cells];
        for (int i = 0; i < RotaCatalogue.Cells; i++)
        {
            allOpen[i] = true;
            allFloor[i] = 6;
        }
        var watch = Stopwatch.StartNew();
        for (int run = 0; run < 200; run++)
        {
            Array.Clear(bigValue, 0, bigValue.Length);
            RotaPlanRules.Lift(allFloor, allOpen, most, bigValue, bigTaken, bigKind, bigGroup, 2);
        }
        watch.Stop();
        CheckSay(watch.ElapsedMilliseconds < 500, "plan floor cost: lifting a week of " + most + " stations is " + (watch.ElapsedMilliseconds / 200.0).ToString("0.00") + " ms a time");
        Check(RotaPlanRules.FloorHours(p) == (3 * 2 + 4 * 5 + 1 * 4) * RotaCatalogue.Days && RotaPlanRules.FloorHours(null) == 0, "plan floor: the hours a week the floors ask for are summed (210), none for no plan");
    }

    private static void PlanEditing()
    {
        var set = new RotaPlanSet { Place = "Main~1" };
        RotaPlanFile first = RotaPlanRules.Add(set, "Standard week");
        Check(first != null && first.Id == 1 && first.Hours == 0 && first.Parity == RotaPlanRules.Every && !first.Overtime && first.Blocks.Count == 0 && !first.Dated,
            "plan new: the starting plan repeats every week, on the people's own hours, no overtime, no floor, no days");
        first.Hours = 40;
        first.Overtime = true;
        first.Blocks.Add(new RotaBlock { From = 7, To = 10, Crew = 2 });
        first.Shifts = 2;
        RotaPlanFile copy = RotaPlanRules.Copy(set, first.Id, "Standard week copy");
        Check(copy != null && copy.Id == 2 && copy.Name == "Standard week copy" && copy.Hours == 40 && copy.Overtime && copy.Shifts == 2 && copy.Blocks.Count == 1 && copy.Blocks[0].Crew == 2,
            "plan copy: a new number, the name given, the figures and the floor of the plan it was made from");
        first.Blocks.Add(new RotaBlock { From = 12, To = 14, Crew = 3 });
        Check(copy.Blocks.Count == 1, "plan copy: the copy is its own, a change to the first does not reach it");
        copy.From = 100;
        Check(!RotaPlanRules.Copy(set, copy.Id, "y").Dated, "plan copy: a copy of a dated plan holds for no days until it is given some");
        while (set.Plans.Count < RotaPlanRules.PlansMost)
        {
            RotaPlanRules.Add(set, "More");
        }
        Check(RotaPlanRules.Add(set, "Too many") == null && RotaPlanRules.Copy(set, 1, "Too many") == null && set.Plans.Count == RotaPlanRules.PlansMost, "plan new: at most " + RotaPlanRules.PlansMost + " plans a business");
        Check(RotaPlanRules.Copy(set, 99, "x") == null, "plan copy: of a plan there is not, nothing");
        // Putting a plan right.
        var wild = new RotaPlanFile { Shifts = 9, Hours = 3, Parity = 7, From = -4, To = -9 };
        wild.Blocks.Add(new RotaBlock { From = 20, To = 30, Crew = 99 });
        wild.Blocks.Add(new RotaBlock { From = 5, To = 5, Crew = 0 });
        wild.Blocks.Add(new RotaBlock { From = 6, To = 9, Crew = 3 });
        RotaPlanRules.Tidy(wild);
        bool fine = wild.Shifts == 2 && wild.Hours == RotaPlanRules.HoursLeast && wild.Parity == RotaPlanRules.Even && wild.From == 0 && wild.To == 0 && wild.Blocks.Count <= RotaPlanRules.BlocksMost;
        int end = 0;
        for (int i = 0; i < wild.Blocks.Count; i++)
        {
            fine &= wild.Blocks[i].From >= end && wild.Blocks[i].To > wild.Blocks[i].From && wild.Blocks[i].To <= RotaCatalogue.Hours && wild.Blocks[i].Crew >= 1 && wild.Blocks[i].Crew <= RotaPlanRules.CrewMost;
            end = wild.Blocks[i].To;
        }
        Check(fine, "plan tidy: shifts, hours and weeks inside what the planner can use, blocks in order, inside the day, none overlapping, none empty (" + wild.Blocks.Count + " kept)");
        var backwards = new RotaPlanFile { From = 50, To = 20 };
        RotaPlanRules.Tidy(backwards);
        Check(backwards.To >= backwards.From, "plan tidy: a plan never ends before it begins");
        int version = wild.Version;
        RotaPlanRules.Tidy(wild);
        Check(wild.Version == version + 1, "plan tidy: every change raises the plan's version, so what was worked out before is not reused");
        Check(RotaPlanRules.Name("  a,b|c;d^e  ").IndexOfAny(new[] { '|', ';', '^' }) < 0 && RotaPlanRules.Name("").Length > 0 && RotaPlanRules.Name(new string('x', 99)).Length == RotaPlanRules.NameMost,
            "plan name: no dividers, never empty, a short one");
    }

    private static void PlanSaves()
    {
        var sets = new Dictionary<string, RotaPlanSet>();
        RotaPlanSet a = PlanSet(new RotaPlanFile { Id = 1, Name = "Standard week", Hours = 40, Overtime = false },
            new RotaPlanFile { Id = 2, Name = "Holiday, week", From = 220, To = 227, Parity = RotaPlanRules.Odd, Hours = 32, Overtime = true, Shifts = 2 });
        a.Plans[0].Blocks.Add(new RotaBlock { From = 7, To = 10, Crew = 2 });
        a.Plans[0].Blocks.Add(new RotaBlock { From = 17, To = 21, Crew = 5 });
        a.Active = 1;
        sets["Main Street~12"] = a;
        sets["Harbour~4"] = PlanSet(new RotaPlanFile { Id = 3, Name = "Lean" });
        string line = RotaPlanStore.Write(sets);
        var back = new Dictionary<string, RotaPlanSet>();
        bool ok = RotaPlanStore.Read(line, back);
        RotaPlanSet r = ok && back.ContainsKey("Main Street~12") ? back["Main Street~12"] : null;
        bool same = ok && back.Count == 2 && r != null && r.Plans.Count == 2 && r.Active == 1 && r.NextId == 3 && r.Plans[0].Name == "Standard week" && r.Plans[0].Hours == 40
                    && r.Plans[0].Blocks.Count == 2 && r.Plans[0].Blocks[1].From == 17 && r.Plans[0].Blocks[1].Crew == 5 && r.Plans[1].From == 220 && r.Plans[1].To == 227
                    && r.Plans[1].Parity == RotaPlanRules.Odd && r.Plans[1].Overtime && r.Plans[1].Shifts == 2 && r.Plans[1].Name == "Holiday  week" && back["Harbour~4"].Plans[0].Id == 3;
        Check(same, "plan save: every plan, its weeks, days, hours, overtime, shifts and floor, and which is chosen, round trip");
        Check(RotaPlanStore.Write(back) == line, "plan save: writing what was read gives the same line");
        Check(RotaPlanStore.Read("v=2^p:x|1|2|", new Dictionary<string, RotaPlanSet>()) == false, "plan save: a line of another version is refused");
        Check(RotaPlanStore.Read("", back) && back.Count == 0, "plan save: no line, no plans");
        var junk = new Dictionary<string, RotaPlanSet>();
        Check(RotaPlanStore.Read("v=1^p:|1|2|1,1,0,0,0,0,0,,x^q:nothing^p:Main~1|1|2|1,1,0,0,0,40,1,,Standard", junk) && junk.Count == 1 && junk.ContainsKey("Main~1"),
            "plan save: a part that is not a business's is skipped, and the rest still loads");
        var many = new Dictionary<string, RotaPlanSet>();
        for (int i = 0; i < 200; i++)
        {
            many["P" + i] = PlanSet(new RotaPlanFile { Id = 1, Name = "S" });
        }
        var capped = new Dictionary<string, RotaPlanSet>();
        RotaPlanStore.Read(RotaPlanStore.Write(many), capped);
        Check(capped.Count == RotaPlanStore.PlacesMost, "plan save: at most " + RotaPlanStore.PlacesMost + " businesses keep plans");
        var crowd = new RotaPlanSet { Place = "X~1" };
        for (int i = 1; i <= 20; i++)
        {
            crowd.Plans.Add(new RotaPlanFile { Id = i, Name = "P" + i });
        }
        var cropped = new Dictionary<string, RotaPlanSet>();
        RotaPlanStore.Read(RotaPlanStore.Write(new Dictionary<string, RotaPlanSet> { { "X~1", crowd } }), cropped);
        Check(cropped["X~1"].Plans.Count == RotaPlanRules.PlansMost, "plan save: at most " + RotaPlanRules.PlansMost + " plans a business");
        string wild = RotaPlanStore.Write(sets).Replace(",40,", ",9999,").Replace("17-21-5", "17-99-99");
        var tidy = new Dictionary<string, RotaPlanSet>();
        RotaPlanStore.Read(wild, tidy);
        Check(tidy["Main Street~12"].Plans[0].Hours == RotaPlanRules.HoursTop && tidy["Main Street~12"].Plans[0].Blocks[1].To <= RotaCatalogue.Hours && tidy["Main Street~12"].Plans[0].Blocks[1].Crew <= RotaPlanRules.CrewMost,
            "plan save: figures out of range are put right on reading");
        Check(RotaPlanStore.Key == "reallife:co_rota_plans", "plan save: the key the brief names");
    }
}
