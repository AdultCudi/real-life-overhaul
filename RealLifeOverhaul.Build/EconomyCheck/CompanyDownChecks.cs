using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

// What the managers' page grades and what happens to a question the owner leaves (the Company app on the kit): how fast a manager answers, their
// judgement, thrift and crew's morale as grades from 0 to 100; and a question left with the owner a day passing down to the area's executive, once.
internal static partial class Program
{
    private static void CompanyDown()
    {
        int before = _run, failedBefore = _failed;
        SpeedGrade();
        OtherGrades();
        HandDownRule();
        DownSaves();
        RotaMoves();
        Console.WriteLine("  the managers' grades, the question handed down and the person a new rota moves: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }

    // The week a manager writes moves somebody from one station to another: the log says who, from where and to where (ManagerRules.Move).
    private static void RotaMoves()
    {
        var now = new RotaPlan();
        var fill = new RotaPlan();
        now.Size(2);
        fill.Size(2);
        // Station 0 the fryer, 1 the grill. Worker 0 stands at the fryer for 20 hours now and at the grill for 30 after; worker 1 stays at the grill; worker 2 moves too, but is a fixed one.
        for (int c = 0; c < 20; c++)
        {
            now.At[c * 2] = 0;
        }
        for (int c = 20; c < 50; c++)
        {
            fill.At[c * 2 + 1] = 0;
        }
        for (int c = 60; c < 100; c++)
        {
            now.At[c * 2 + 1] = 1;
            fill.At[c * 2 + 1] = 1;
        }
        for (int c = 100; c < 160; c++)
        {
            now.At[c * 2] = 2;
            fill.At[c * 2 + 1] = 2;
        }
        int worker, from, to;
        bool found = ManagerRules.Move(now, fill, 2, 3, new[] { false, false, true }, out worker, out from, out to);
        Check(found && worker == 0 && from == 0 && to == 1, "rota move: the person a new week moves is found with the station they came from and the one they go to (" + worker + " from " + from + " to " + to + ")");
        Check(!ManagerRules.Move(now, now, 2, 3, null, out worker, out from, out to) && worker == -1, "rota move: the same week twice moves nobody");
        found = ManagerRules.Move(now, fill, 2, 3, null, out worker, out from, out to);
        Check(found && worker == 2 && from == 0 && to == 1, "rota move: of two who move, the one with more hours at the new station (" + worker + ")");
        found = ManagerRules.Move(now, fill, 2, 3, new[] { true, false, true }, out worker, out from, out to);
        Check(!found, "rota move: the planner's fixed ones are never moved, and somebody on one week only is placed or taken off, not moved");
        var other = new RotaPlan();
        other.Size(3);
        Check(!ManagerRules.Move(now, other, 2, 3, null, out worker, out from, out to) && !ManagerRules.Move(null, fill, 2, 3, null, out worker, out from, out to) && !ManagerRules.Move(now, fill, 0, 3, null, out worker, out from, out to),
            "rota move: weeks of different stations, no week or no stations move nobody");
        // A place's weeks of different people over sixty weeks: a move is never of somebody who is not on both.
        bool never = true;
        for (int week = 0; week < 60; week++)
        {
            var a = new RotaPlan();
            var b = new RotaPlan();
            a.Size(3);
            b.Size(3);
            for (int c = 0; c < RotaCatalogue.Cells; c++)
            {
                for (int s = 0; s < 3; s++)
                {
                    a.At[c * 3 + s] = (short)(CityModel.Draw(31, week, c * 3 + s, 5) - 1);
                    b.At[c * 3 + s] = (short)(CityModel.Draw(32, week, c * 3 + s, 5) - 1);
                }
            }
            if (ManagerRules.Move(a, b, 3, 4, null, out worker, out from, out to))
            {
                never &= worker >= 0 && worker < 4 && from >= 0 && to >= 0 && from != to && from < 3 && to < 3;
            }
        }
        Check(never, "rota move: over sixty seeded weeks a move names a worker and two different stations of the place");
    }

    private static void SpeedGrade()
    {
        var ring = new List<int>();
        Check(ManagerRules.Speed(ring) == -1 && ManagerRules.Speed(null) == -1, "speed: before any request is settled there is no grade, never a zero");
        ManagerRules.AddSpeed(ring, 0);
        Check(ManagerRules.Speed(ring) == 100, "speed: a request answered the day it came is a hundred");
        ring.Clear();
        ManagerRules.AddSpeed(ring, 1);
        Check(ManagerRules.Speed(ring) == 75, "speed: a day's wait costs a quarter (" + ManagerRules.Speed(ring) + ")");
        ring.Clear();
        ManagerRules.AddSpeed(ring, ManagerCatalogue.SpeedDays);
        Check(ManagerRules.Speed(ring) == 0, "speed: the days a request has before it lapses, on average, are nothing");
        ring.Clear();
        ManagerRules.AddSpeed(ring, 0);
        ManagerRules.AddSpeed(ring, 2);
        Check(ManagerRules.Speed(ring) == 75, "speed: it is the mean of the days (0 and 2 are 75)");
        ring.Clear();
        ManagerRules.AddSpeed(ring, 400);
        ManagerRules.AddSpeed(ring, -3);
        Check(ring[0] == 99 && ring[1] == 0 && ManagerRules.Speed(ring) >= 0 && ManagerRules.Speed(ring) <= 100, "speed: a wait out of range is clamped before it is kept");
        ring.Clear();
        for (int i = 0; i < 50; i++)
        {
            ManagerRules.AddSpeed(ring, i % 5);
        }
        Check(ring.Count == ManagerCatalogue.SpeedKept && ring[ring.Count - 1] == 49 % 5 && ring[0] == (50 - ManagerCatalogue.SpeedKept) % 5,
            "speed: the ring keeps the last " + ManagerCatalogue.SpeedKept + ", the newest last (" + ring.Count + ")");
        // The grade falls as the waits lengthen and never leaves 0 to 100, whatever the order they came in.
        bool monotone = true, inRange = true;
        int last = 101;
        for (int d = 0; d <= 10; d++)
        {
            ring.Clear();
            for (int i = 0; i < 7; i++)
            {
                ManagerRules.AddSpeed(ring, d);
            }
            int grade = ManagerRules.Speed(ring);
            monotone &= grade <= last;
            inRange &= grade >= 0 && grade <= 100;
            last = grade;
        }
        Check(monotone && inRange, "speed: a longer wait never grades higher, and the grade stays within 0 to 100");
        var a = new List<int> { 0, 3, 1, 2 };
        var b = new List<int> { 2, 1, 3, 0 };
        Check(ManagerRules.Speed(a) == ManagerRules.Speed(b), "speed: the order the waits came in does not matter");
    }

    private static void OtherGrades()
    {
        // Judgement: the skill's own chance of a mistake until a month's decisions say otherwise.
        Check(ManagerRules.Judgement(100, 0, 0, 0) == 100 && ManagerRules.Judgement(0, 0, 0, 0) == 0, "judgement: with nothing decided it is the skill's own chance, none at 100 and the worst at 0");
        Check(ManagerRules.Judgement(60, 0, 0, 0) > ManagerRules.Judgement(40, 0, 0, 0), "judgement: more skill errs less");
        Check(ManagerRules.Judgement(60, 100, 0, 0) > ManagerRules.Judgement(60, 0, 0, 0), "judgement: a regional manager over them cuts the mistakes");
        Check(ManagerRules.Judgement(40, 0, 30, 0) > ManagerRules.Judgement(40, 0, 0, 0) && ManagerRules.Judgement(40, 0, 30, 10) < ManagerRules.Judgement(40, 0, 0, 0),
            "judgement: a month with no mistakes grades above the skill's chance, and one with many below it");
        Check(ManagerRules.Judgement(40, 0, 2, 1) > ManagerRules.Judgement(40, 0, 30, 15), "judgement: one mistake in two decisions weighs less than fifteen in thirty");
        bool inRange = true, monotone = true;
        for (int m = 0; m <= 40; m += 4)
        {
            int grade = ManagerRules.Judgement(55, 20, 40, m);
            inRange &= grade >= 0 && grade <= 100;
            monotone &= m == 0 || grade <= ManagerRules.Judgement(55, 20, 40, m - 4);
        }
        Check(inRange && monotone, "judgement: more mistakes never grade higher, and the grade stays within 0 to 100");
        Check(ManagerRules.Judgement(50, 0, -5, -5) >= 0 && ManagerRules.Judgement(150, 200, 3, 9) <= 100, "judgement: figures out of range are bounded");
        // Thrift and morale.
        Check(ManagerRules.Thrift(0, 1000) == 100 && ManagerRules.Thrift(1000, 1000) == 0 && ManagerRules.Thrift(2500, 1000) == 0 && ManagerRules.Thrift(250, 1000) == 75,
            "thrift: the share of the month's budget still there, nothing when it is spent or over");
        Check(ManagerRules.Thrift(0, 0) == -1 && ManagerRules.Thrift(50, -5) == -1, "thrift: with no budget there is no grade to give");
        Check(ManagerRules.Morale(58, 9) == 58 && ManagerRules.Morale(130, 4) == 100 && ManagerRules.Morale(-4, 4) == 0 && ManagerRules.Morale(60, 0) == -1,
            "morale: the staff's standing on average, bounded, and no grade for no staff");
    }

    // A question left with the owner a day goes to the area's executive: only where the manager gives it that way, the executive is in post, it was
    // never passed down, it is no proposal, and the policy lets that executive settle that kind.
    private static void HandDownRule()
    {
        int approver = Array.IndexOf(PolicyCatalogue.Def(PolicyField.Approver).Steps, PolicyCatalogue.ApprovePeople);
        int sources = Array.IndexOf(PolicyCatalogue.Def(PolicyField.Sources).Steps, PolicyCatalogue.SourceOwn);
        const int owner = EscalationRules.LevelOwner;
        Check(EscalationRules.HandsDown(EscalationKind.Exception, owner, 10, 11, false, true, true, approver, sources), "hand down: an exception left a day, the manager's switch on and the people chief in post, is passed down");
        Check(!EscalationRules.HandsDown(EscalationKind.Exception, owner, 10, 10, false, true, true, approver, sources), "hand down: not on the day it came to the owner");
        Check(!EscalationRules.HandsDown(EscalationKind.Exception, owner, 10, 11, false, false, true, approver, sources), "hand down: not while the manager gives their questions to the owner alone");
        Check(!EscalationRules.HandsDown(EscalationKind.Exception, owner, 10, 12, false, true, false, approver, sources), "hand down: not with nobody in the executive's seat");
        Check(!EscalationRules.HandsDown(EscalationKind.Exception, owner, 10, 12, true, true, true, approver, sources), "hand down: never twice, so a question the executive could not settle does not go back and forth");
        Check(!EscalationRules.HandsDown(EscalationKind.Proposal, owner, 10, 12, false, true, true, approver, sources), "hand down: a proposal is the owner's alone");
        Check(!EscalationRules.HandsDown(EscalationKind.Kpi, owner, 10, 12, false, true, true, approver, sources), "hand down: a place off its targets is reported to the owner, not settled by an executive");
        Check(!EscalationRules.HandsDown(EscalationKind.Exception, EscalationRules.LevelExecutive, 10, 12, false, true, true, approver, sources),
            "hand down: a question already with an executive is not handed down again");
        int nobody = Array.IndexOf(PolicyCatalogue.Def(PolicyField.Approver).Steps, PolicyCatalogue.ApproveNobody);
        Check(!EscalationRules.HandsDown(EscalationKind.Exception, owner, 10, 12, false, true, true, nobody, sources), "hand down: not an exception the policy lets nobody but the owner approve");
        bool kinds = true;
        foreach (EscalationKind k in new[] { EscalationKind.Budget, EscalationKind.Demand, EscalationKind.Mood, EscalationKind.Wave })
        {
            kinds &= EscalationRules.HandsDown(k, owner, 1, 2, false, true, true, approver, sources);
        }
        Check(kinds, "hand down: the executive settles what is a budget, a demand, a mood or a wave of leavers under any policy");
        // The same question over the days: handed down once, then never again, whatever the executive does with it.
        int level = owner, moved = 10, handed = 0;
        bool down = false;
        for (int day = 10; day < 40; day++)
        {
            if (EscalationRules.HandsDown(EscalationKind.Budget, level, moved, day, down, true, true, approver, sources))
            {
                level = EscalationRules.LevelExecutive;
                down = true;
                moved = day;
                handed++;
                level = owner;   // the executive could not settle it and it went back
            }
        }
        Check(handed == 1, "hand down: over thirty days a question is passed down once (" + handed + ")");
    }

    private static void DownSaves()
    {
        var s = new ManagerState { MonthFrom = 10 };
        var m = new ManagerFile { Place = "shop~1", Id = "m1", Name = "Ray Walsh", HandDown = true, MarketingStep = 4, LeaseStep = 1, MarketingSpent = 130 };
        for (int i = 0; i < 25; i++)
        {
            ManagerRules.AddSpeed(m.Speeds, i % 4);
        }
        s.Managers.Add(m);
        ManagerState back = ManagerStore.Read(ManagerStore.Write(s), out int version);
        ManagerFile r = back != null && back.Managers.Count == 1 ? back.Managers[0] : null;
        bool same = r != null && r.HandDown && r.MarketingStep == 4 && r.LeaseStep == 1 && r.MarketingSpent == 130 && r.Speeds.Count == ManagerCatalogue.SpeedKept && r.Name == "Ray Walsh";
        for (int i = 0; same && i < r.Speeds.Count; i++)
        {
            same &= r.Speeds[i] == m.Speeds[i];
        }
        Check(same, "manager save: the speed ring, the hand-down switch, the marketing and lease limits and the month's marketing spend round trip");
        Check(ManagerStore.Write(back) == ManagerStore.Write(s), "manager save: writing what was read gives the same line");
        // The line the version before wrote has none of them: it loads with the starting steps, no switch and no waits.
        var plain = new ManagerState { MonthFrom = 10 };
        plain.Managers.Add(new ManagerFile { Place = "shop~1", Id = "m1", Name = "Ray Walsh" });
        string line = ManagerStore.Write(plain);
        int cut = line.IndexOf("Ray Walsh", StringComparison.Ordinal) + "Ray Walsh".Length;
        ManagerState older = ManagerStore.Read(line.Substring(0, cut), out version);
        ManagerFile o = older != null && older.Managers.Count == 1 ? older.Managers[0] : null;
        Check(o != null && !o.HandDown && o.Speeds.Count == 0 && o.MarketingStep == ManagerCatalogue.MarketingDefault && o.LeaseStep == ManagerCatalogue.LeaseDefault && o.MarketingSpent == 0,
            "manager save: an older line loads with the starting steps, no hand-down and no waits");
        // A month's end clears the marketing spend with the rest of the month, and keeps the waits, which are the manager's whole record.
        m.EndMonth();
        Check(m.MarketingSpent == 0 && m.Speeds.Count == ManagerCatalogue.SpeedKept, "manager month: the marketing spend starts again, the waits are kept");
        // A question handed down is kept as handed down; an older line has it not.
        var h = new HierarchyState();
        Escalation e = h.Add(EscalationKind.Exception, "shop~1", EscalationRules.LevelExecutive, 12);
        e.Down = true;
        h.Add(EscalationKind.Budget, "shop~1", EscalationRules.LevelOwner, 13);
        HierarchyState hb = HierarchyStore.Read(HierarchyStore.Write(h), out int hv);
        Check(hb != null && hb.Escalations.Count == 2 && hb.Escalations[0].Down && !hb.Escalations[1].Down, "escalation save: the question handed down is kept as handed down");
        string hline = HierarchyStore.Write(h);
        int at = hline.IndexOf("^l:", StringComparison.Ordinal);
        string[] records = hline.Substring(hline.IndexOf("^e:", StringComparison.Ordinal) + 3, at - hline.IndexOf("^e:", StringComparison.Ordinal) - 3).Split(';');
        string strip = hline.Replace(records[0], records[0].Substring(0, records[0].LastIndexOf('|')));
        HierarchyState ho = HierarchyStore.Read(strip, out hv);
        Check(ho != null && ho.Escalations.Count == 2 && !ho.Escalations[0].Down, "escalation save: a line from before the flag still loads, and nothing was handed down");
    }
}
