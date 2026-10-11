using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;

// The checks of what the staff rota planner does to a week that is already there (Scripts/Economy/Company/RotaLedger.cs:
// the decisions of RotaGame.Write and RotaGame.PutBack), of the stamp a kept sheet is told stale by, of the promotion
// figure the forecast of the days ahead is made with (Business/BusinessCatalogue.Written, the function that writes it), of
// the hours kept out of the advice for want of a late opening permit, and that a rota and an advice made a step at a time
// are the ones made in one go.
internal static partial class Program
{
    private static void RotaLedgerChecks()
    {
        RotaLimitsAndBehaviour();
        RotaWhoIsLeftAlone();
        RotaWhatIsWritten();
        RotaPutBackChecks();
        RotaHeadquartersHolders();
        RotaStampChecks();
        RotaPromotionChecks();
        RotaPermitChecks();
        RotaStepped();
    }

    private static RotaShift RotaAt(int day, int from, int to, int worker, int station)
    {
        return new RotaShift { Day = day, From = from, To = to, Worker = worker, Station = station };
    }

    private static string RotaWeekText(List<RotaShift> week)
    {
        var lines = new List<string>();
        foreach (RotaShift s in week)
        {
            lines.Add(s.Day + ":" + s.From + "-" + s.To + ":" + s.Worker + "@" + s.Station);
        }
        lines.Sort(string.CompareOrdinal);
        return string.Join(" ", lines);
    }

    // The limits as behaviour: what a rota does at each of them, and that a business beyond what is kept is refused whole.
    private static void RotaLimitsAndBehaviour()
    {
        RotaProblem p = RotaMake(1, 1);
        RotaWant(p, 0, 0x01, 6, 18);
        RotaPlan plan = RotaSolve(p, "twelve hours");
        Check(plan.Full && plan.Shifts == 1 && RotaLongestShift(plan) == 12, "rota limits: one person works twelve hours at a station as one shift");
        p = RotaMake(1, 1);
        RotaWant(p, 0, 0x01, 6, 19);
        plan = RotaSolve(p, "thirteen hours");
        Check(plan.CoveredHours == 12 && RotaLongestShift(plan) <= 12, "rota limits: of thirteen hours on end at one station, one person works twelve and no thirteenth (" + plan.CoveredHours + ")");
        p = RotaMake(2, 1);
        RotaWant(p, 0, 0x01, 0, 24);
        RotaWant(p, 1, 0x01, 0, 24);
        plan = RotaSolve(p, "a whole day wanted twice");
        Check(RotaHoursOf(plan, 0, 0) == 14, "rota limits: with work for every hour of the day at two stations, one person works fourteen hours and not a fifteenth (" + RotaHoursOf(plan, 0, 0) + ")");
        int[] weights = new int[3];
        for (int priority = 0; priority < 3; priority++)
        {
            p = RotaMake(1, 1);
            RotaWant(p, 0, 0x01, 9, 13);
            p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.Hours, priority, 30, 40) };
            weights[priority] = RotaSolve(p, "a least of hours that is not there, priority " + priority).UnmetWeight;
        }
        Check(weights[0] == 2 && weights[1] == 3 && weights[2] == 5, "rota limits: a demand unmet weighs two, three or five by its priority, as the game takes it off satisfaction (" + weights[0] + ", " + weights[1] + ", " + weights[2] + ")");
        p = RotaMake(2, 2);
        p.Can[0 * 2 + 1] = 0;
        p.Can[1 * 2 + 0] = 0;
        RotaWant(p, 0, 0x01, 9, 17, RotaCatalogue.ValueFloor);
        RotaWant(p, 1, 0x01, 9, 17, RotaCatalogue.ValueSupport);
        plan = RotaSolve(p, "the least values");
        Check(plan.Full && plan.WantedHours == 16, "rota limits: an hour of the smallest crew and an hour at a support station are both wanted and both covered");

        Check(RotaLedger.Fits(RotaCatalogue.StationsMost, RotaCatalogue.WorkersMost) && RotaLedger.Fits(0, 0), "rota limits: a business of 64 stations and 120 employees is within what is kept, and so is an empty one");
        Check(!RotaLedger.Fits(RotaCatalogue.StationsMost + 1, 5) && !RotaLedger.Fits(5, RotaCatalogue.WorkersMost + 1), "rota limits: one station or one employee more is refused whole, not read in part");
        p = RotaMake(RotaCatalogue.StationsMost, RotaCatalogue.WorkersMost);
        for (int s = 0; s < RotaCatalogue.StationsMost; s++)
        {
            RotaWant(p, s, 0x7F, 8, 20, 10 + s);
        }
        plan = RotaSolve(p, "the most that is kept");
        Check(plan.WantedHours == 64 * 84 && plan.CoveredHours > 0, "rota limits: the largest business that is kept gets a rota that keeps the rules (" + plan.CoveredHours + " of " + plan.WantedHours + " hours)");
    }

    private static void RotaWhoIsLeftAlone()
    {
        // Stations 0 and 1 are headquarters desks, 2 is the cleaner's. Workers 0 to 3.
        bool[] whole = { true, true, false };
        var present = new List<RotaShift>
        {
            RotaAt(0, 8, 16, 0, 0), RotaAt(1, 8, 16, 0, 0),         // 0 holds a desk
            RotaAt(0, 14, 16, 1, 2),                                // 1 cleans
            RotaAt(2, 9, 12, 2, RotaLedger.Unread),                 // 2 stands at a station that was not read
            RotaAt(3, 9, 12, 3, RotaLedger.Gone),                   // 3 has a shift at a station that is gone
            RotaAt(0, 6, 10, RotaLedger.Unread, 2),                 // a driver, of all people, at the cleaner's station
            RotaAt(4, 6, 10, RotaLedger.Gone, 1)                    // somebody who has left
        };
        var fixedWorker = new bool[4];
        RotaLedger.Fixed(present, whole, 4, fixedWorker);
        Check(fixedWorker[0], "rota ledger: whoever holds a headquarters desk is left exactly as they are");
        Check(!fixedWorker[1], "rota ledger: the cleaner of a headquarters is not: a cleaning shift may be moved");
        Check(fixedWorker[2], "rota ledger: whoever has a shift at a station the planner did not read is left exactly as they are");
        Check(!fixedWorker[3], "rota ledger: a shift at a station that is gone fixes nobody");
        Check(!RotaLedger.Removes(present[0], fixedWorker) && !RotaLedger.Removes(present[1], fixedWorker), "rota ledger: a fill takes out no shift of a desk holder");
        Check(RotaLedger.Removes(present[2], fixedWorker), "rota ledger: a fill takes out the shifts of somebody it may place");
        Check(!RotaLedger.Removes(present[3], fixedWorker), "rota ledger: a fill takes out no shift at a station that was not read");
        Check(RotaLedger.Removes(present[4], fixedWorker), "rota ledger: a shift at a station that is gone is taken out with the rest of that person's");
        Check(!RotaLedger.Removes(present[5], fixedWorker) && !RotaLedger.Removes(present[6], fixedWorker), "rota ledger: a fill never touches a shift of somebody the planner did not read");

        var at = new short[RotaCells * 3];
        for (int i = 0; i < at.Length; i++)
        {
            at[i] = -1;
        }
        var taken = new bool[RotaCells * 3];
        RotaLedger.Lay(present, fixedWorker, 3, at, taken);
        Check(taken[(0 * 24 + 8) * 3 + 0] && taken[(1 * 24 + 15) * 3 + 0] && at[(0 * 24 + 8) * 3 + 0] == -1, "rota ledger: the desk holder's hours are taken, not the planner's to give");
        Check(at[(0 * 24 + 14) * 3 + 2] == 1 && !taken[(0 * 24 + 14) * 3 + 2], "rota ledger: the cleaner's hours are the rota as it stands, to be rated and moved");
        Check(taken[(0 * 24 + 6) * 3 + 2] && taken[(4 * 24 + 6) * 3 + 1], "rota ledger: an hour somebody the planner did not read stands at a station is taken");
        int laid = 0;
        for (int i = 0; i < at.Length; i++)
        {
            laid += (at[i] >= 0 ? 1 : 0) + (taken[i] ? 1 : 0);
        }
        Check(laid == 16 + 2 + 4 + 4, "rota ledger: shifts at stations that were not read or are gone are laid nowhere (" + laid + ")");

        // Shifts the game should not hold, and may: hours outside the day, a day outside the week.
        var odd = new List<RotaShift> { RotaAt(2, -3, 2, 1, 2), RotaAt(2, 22, 30, 1, 2), RotaAt(9, 8, 12, 1, 2), RotaAt(-1, 8, 12, 1, 2) };
        var oddAt = new short[RotaCells * 3];
        var oddTaken = new bool[RotaCells * 3];
        for (int i = 0; i < oddAt.Length; i++)
        {
            oddAt[i] = -1;
        }
        RotaLedger.Lay(odd, new bool[4], 3, oddAt, oddTaken);
        int oddHours = 0;
        for (int i = 0; i < oddAt.Length; i++)
        {
            oddHours += oddAt[i] >= 0 ? 1 : 0;
        }
        Check(oddHours == 4 && oddAt[(2 * 24 + 0) * 3 + 2] == 1 && oddAt[(2 * 24 + 23) * 3 + 2] == 1, "rota ledger: a shift that runs outside its day is laid for the hours inside it, and one on no day of the week not at all (" + oddHours + ")");
        var nobody = new bool[4];
        RotaLedger.Fixed(new List<RotaShift>(), whole, 4, nobody);
        Check(!nobody[0] && !nobody[1] && !nobody[2] && !nobody[3], "rota ledger: in an empty week nobody is fixed");

        RotaProblem p = RotaMake(3, 4);
        RotaLedger.Bench(p, fixedWorker);
        Check(p.Can[0 * 3] == 0 && p.Can[0 * 3 + 2] == 0 && p.Can[2 * 3 + 1] == 0 && p.Can[1 * 3 + 2] == 50 && p.Can[3 * 3] == 50, "rota ledger: somebody left as they are can be placed nowhere by the planner");
        Check(RotaLedger.Idles(true, false, false), "rota ledger: somebody who had a shift and has none is unassigned from work and gets the game's idle task");
        Check(!RotaLedger.Idles(false, false, false), "rota ledger: somebody who had no shift before is not touched for having none now");
        Check(!RotaLedger.Idles(true, true, false), "rota ledger: somebody who still has a shift is not unassigned");
        Check(!RotaLedger.Idles(true, false, true) && !RotaLedger.Idles(false, false, true), "rota ledger: somebody who holds a headquarters plan is never unassigned: the game could not give the plan back");
    }

    private static void RotaWhatIsWritten()
    {
        var plan = new RotaPlan();
        plan.Size(3);
        for (int h = 9; h < 17; h++)
        {
            plan.At[(0 * 24 + h) * 3 + 0] = (short)(h < 13 ? 0 : 1);    // station 0 on Monday: worker 0, then worker 1
            plan.At[(0 * 24 + h) * 3 + 1] = 2;                           // station 1: worker 2
            plan.At[(1 * 24 + h) * 3 + 2] = 3;                           // station 2 on Tuesday: worker 3
            plan.At[(5 * 24 + h) * 3 + 0] = 0;                           // station 0 on Saturday, which is closed
        }
        plan.At[(0 * 24 + 20) * 3 + 0] = 0;                              // and one more hour for worker 0 late on Monday
        bool[] open = { true, true, true, true, true, false, false }, all3 = { true, true, true }, all4 = { true, true, true, true }, none4 = new bool[4];
        var shifts = new List<RotaShift>();
        RotaLedger.Shifts(plan, open, all3, all4, none4, shifts);
        Check(shifts.Count == 5, "rota ledger: a plan comes to one shift for each run of one person at one station (" + shifts.Count + ")");
        Check(RotaWeekText(shifts) == "0:13-17:1@0 0:20-21:0@0 0:9-13:0@0 0:9-17:2@1 1:9-17:3@2", "rota ledger: with its day, its hours, its person and its station (" + RotaWeekText(shifts) + ")");
        RotaLedger.Shifts(plan, open, new[] { true, false, true }, all4, none4, shifts);
        bool dead = false;
        foreach (RotaShift s in shifts)
        {
            dead |= s.Station == 1;
        }
        Check(shifts.Count == 4 && !dead, "rota ledger: no shift is ever written for a station that is not in the building at that moment");
        RotaLedger.Shifts(plan, open, all3, new[] { true, true, true, false }, none4, shifts);
        dead = false;
        foreach (RotaShift s in shifts)
        {
            dead |= s.Worker == 3;
        }
        Check(shifts.Count == 4 && !dead, "rota ledger: no shift is ever written for somebody who does not work there at that moment");
        RotaLedger.Shifts(plan, open, all3, new[] { false, true, true, true }, none4, shifts);
        Check(RotaWeekText(shifts) == "0:13-17:1@0 0:9-17:2@1 1:9-17:3@2", "rota ledger: and the colleague's shift after theirs still starts where it did");
        RotaLedger.Shifts(plan, open, all3, all4, new[] { false, false, true, false }, shifts);
        Check(shifts.Count == 4, "rota ledger: nor one for somebody who is left as they are");
        RotaLedger.Shifts(plan, new bool[7], all3, all4, none4, shifts);
        Check(shifts.Count == 0, "rota ledger: nothing is written into a week that is closed");
        var empty = new RotaPlan();
        empty.Size(3);
        RotaLedger.Shifts(empty, open, all3, all4, none4, shifts);
        Check(shifts.Count == 0, "rota ledger: an empty plan comes to no shifts");

        // A fill on a week: what is taken out and what stays.
        var week = new List<RotaShift>
        {
            RotaAt(0, 8, 12, 0, 0), RotaAt(2, 8, 12, 1, 1), RotaAt(3, 10, 14, 2, RotaLedger.Unread), RotaAt(0, 6, 9, RotaLedger.Unread, RotaLedger.Unread), RotaAt(6, 8, 12, 3, 2)
        };
        var fixedWorker = new bool[4];
        RotaLedger.Fixed(week, new bool[3], 4, fixedWorker);
        RotaLedger.Write(week, plan, open, all3, all4, fixedWorker, shifts);
        string after = RotaWeekText(week);
        Check(after.Contains("3:10-14:2@-1") && after.Contains("0:6-9:-1@-1"), "rota ledger: after a fill the shift at the station that was not read and the driver's shift are exactly where they were");
        Check(!after.Contains("0:8-12:0@0") && !after.Contains("2:8-12:1@1") && !after.Contains("6:8-12:3@2"), "rota ledger: the old shifts of those it places are gone, on closed days as well");
        Check(!after.Contains("2@1") && after.Contains("0:9-13:0@0") && after.Contains("1:9-17:3@2"), "rota ledger: the plan is in, without the shift of the one who is left as they are (" + after + ")");
    }

    private static void RotaPutBackChecks()
    {
        // The week before; a fill; the week put back.
        var before = new List<RotaShift>
        {
            RotaAt(0, 8, 16, 0, 0), RotaAt(1, 8, 16, 0, 0), RotaAt(0, 12, 20, 1, 1), RotaAt(4, 9, 13, 2, 1), RotaAt(2, 7, 11, RotaLedger.Unread, RotaLedger.Unread)
        };
        var kept = new List<RotaShift>(before);
        var week = new List<RotaShift>(before);
        var plan = new RotaPlan();
        plan.Size(2);
        for (int d = 0; d < 5; d++)
        {
            for (int h = 9; h < 17; h++)
            {
                plan.At[(d * 24 + h) * 2] = (short)(d % 3);
                plan.At[(d * 24 + h) * 2 + 1] = (short)((d + 1) % 3);
            }
        }
        bool[] open = { true, true, true, true, true, false, false }, both = { true, true }, three = { true, true, true }, fixedWorker = new bool[3];
        var scratch = new List<RotaShift>();
        RotaLedger.Write(week, plan, open, both, three, fixedWorker, scratch);
        Check(RotaWeekText(week) != RotaWeekText(before) && week.Count == 11, "rota put back: a fill changes the week (" + week.Count + " shifts)");
        RotaLedger.PutBack(kept, week);
        Check(RotaWeekText(week) == RotaWeekText(before), "rota put back: putting the old week back gives exactly the week there was before the fill");
        RotaLedger.Write(week, plan, open, both, three, fixedWorker, scratch);
        RotaLedger.Write(week, plan, open, both, three, fixedWorker, scratch);
        RotaLedger.PutBack(kept, week);
        Check(RotaWeekText(week) == RotaWeekText(before), "rota put back: filled twice and put back once, it is still the first week");

        // What changed meanwhile.
        week = new List<RotaShift>(before);
        RotaLedger.Write(week, plan, open, both, three, fixedWorker, scratch);
        week.RemoveAll(s => s.Worker == RotaLedger.Unread);
        week.Add(RotaAt(5, 1, 5, RotaLedger.Unread, RotaLedger.Unread));     // the driver's shift was moved by the player since
        RotaLedger.PutBack(kept, week);
        Check(RotaWeekText(week).Contains("5:1-5:-1@-1") && !RotaWeekText(week).Contains("2:7-11:-1@-1"), "rota put back: the shift of somebody left alone stays as it is today, not as it was");
        var gone = new List<RotaShift>(before);
        gone[3] = RotaAt(4, 9, 13, RotaLedger.Gone, 1);                      // worker 2 has left since
        gone[2] = RotaAt(0, 12, 20, 1, RotaLedger.Gone);                     // and the station of worker 1's shift was sold
        week = new List<RotaShift>(before);
        RotaLedger.Write(week, plan, open, both, three, fixedWorker, scratch);
        RotaLedger.PutBack(gone, week);
        Check(RotaWeekText(week) == "0:8-16:0@0 1:8-16:0@0 2:7-11:-1@-1", "rota put back: a shift whose person has left or whose station is gone is not put back (" + RotaWeekText(week) + ")");
        Check(RotaLedger.Restores(RotaAt(0, 8, 9, 0, 0)) && !RotaLedger.Restores(RotaAt(0, 8, 9, RotaLedger.Unread, 0)) && !RotaLedger.Restores(RotaAt(0, 8, 9, RotaLedger.Gone, 0))
              && !RotaLedger.Restores(RotaAt(0, 8, 9, 0, RotaLedger.Gone)), "rota put back: what goes back is a shift of somebody the planner may place, still there, at a station still there");
        Check(RotaLedger.Stays(RotaAt(0, 8, 9, RotaLedger.Unread, 0)) && !RotaLedger.Stays(RotaAt(0, 8, 9, 0, 0)) && !RotaLedger.Stays(RotaAt(0, 8, 9, RotaLedger.Gone, 0)), "rota put back: what stays of today's week is only what the planner leaves alone");
        week = new List<RotaShift>();
        RotaLedger.PutBack(new List<RotaShift>(), week);
        Check(week.Count == 0, "rota put back: an empty week put back into an empty week is empty");
    }

    // A headquarters: whoever has a desk keeps it, whatever they ask for, and only the empty desks are filled.
    private static void RotaHeadquartersHolders()
    {
        var shop = new RotaShop();
        shop.Size(3, 1);
        shop.GroupKind[0] = RotaGroupKind.Service;
        shop.FixedHours = true;
        for (int cell = 0; cell < RotaCells; cell++)
        {
            shop.Open[cell] = cell / 24 < 5 && cell % 24 >= 8 && cell % 24 < 16;
        }
        RotaProblem p = RotaMake(3, 3);
        p.Whole[0] = p.Whole[1] = p.Whole[2] = true;
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.FreeDays, 2, 0, 0, 0x10), RotaAsk(RotaDemandKind.Hours, 2, 10, 20) };   // the holder wants Fridays free and twenty hours at most: no desk could be given to them
        var present = new List<RotaShift>();
        for (int d = 0; d < 5; d++)
        {
            present.Add(RotaAt(d, 8, 16, 0, 0));
        }
        var fixedWorker = new bool[3];
        RotaLedger.Fixed(present, p.Whole, 3, fixedWorker);
        var now = new RotaPlan();
        now.Size(3);
        RotaLedger.Lay(present, fixedWorker, 3, now.At, shop.Taken);
        RotaLedger.Bench(p, fixedWorker);
        var advisor = new RotaAdvisor();
        advisor.Grid(shop, shop.Open, p);
        RotaPlan plan = RotaSolve(p, "a headquarters with a desk held");
        Check(fixedWorker[0] && RotaHoursOf(plan, 0) == 0, "rota headquarters: the planner gives the holder of a desk nothing and takes nothing from them");
        int heldWanted = 0;
        for (int cell = 0; cell < RotaCells; cell++)
        {
            heldWanted += p.Value[cell * 3] > 0 ? 1 : 0;
        }
        Check(heldWanted == 0, "rota headquarters: the desk that is held is not wanted from anybody else, in any hour");
        Check(plan.Full && plan.WantedHours == 80 && RotaHoursOf(plan, 1) == 40 && RotaHoursOf(plan, 2) == 40, "rota headquarters: the two empty desks are filled for the whole week by the two who had none");
        var week = new List<RotaShift>(present);
        var scratch = new List<RotaShift>();
        RotaLedger.Write(week, plan, new[] { true, true, true, true, true, false, false }, new[] { true, true, true }, new[] { true, true, true }, fixedWorker, scratch);
        int holders = 0;
        foreach (RotaShift s in week)
        {
            holders += s.Worker == 0 && s.Station == 0 && s.From == 8 && s.To == 16 ? 1 : 0;
        }
        Check(holders == 5 && week.Count == 15, "rota headquarters: after the fill the holder has the five shifts they had, although no rota would have given them the desk");
        RotaLedger.PutBack(present, week);
        Check(RotaWeekText(week) == RotaWeekText(present), "rota headquarters: and put back, the week is the one there was");

        // Without anybody at a desk, the same person could not be given one: the rule that makes the holder worth keeping.
        p = RotaMake(1, 1);
        p.Whole[0] = true;
        RotaWant(p, 0, 0x1F, 8, 16, RotaCatalogue.ValueWork);
        p.Worker[0].Demands = new[] { RotaAsk(RotaDemandKind.Hours, 2, 10, 20) };
        Check(RotaSolve(p, "a desk for somebody of twenty hours").CoveredHours == 0, "rota headquarters: somebody of twenty hours a week is given no desk of forty: a holder like that keeps theirs only because holders are left alone");
    }

    private static long RotaStampOf(string[] stations, string[] staff, int hours)
    {
        long h = RotaLedger.StampStart;
        foreach (string id in stations)
        {
            h = RotaLedger.Mix(h, id);
        }
        foreach (string id in staff)
        {
            h = RotaLedger.Mix(h, id);
        }
        return RotaLedger.Mix(h, hours);
    }

    private static void RotaStampChecks()
    {
        long stamp = RotaStampOf(new[] { "till-a", "till-b", "mop" }, new[] { "e1", "e2" }, 9 * 32 + 17);
        Check(stamp == RotaStampOf(new[] { "till-a", "till-b", "mop" }, new[] { "e1", "e2" }, 9 * 32 + 17), "rota stamp: the same business gives the same stamp");
        Check(stamp != RotaStampOf(new[] { "till-a", "till-c", "mop" }, new[] { "e1", "e2" }, 9 * 32 + 17), "rota stamp: one station sold and another placed, the same number of them, changes the stamp");
        Check(stamp != RotaStampOf(new[] { "till-a", "till-b" }, new[] { "e1", "e2" }, 9 * 32 + 17) && stamp != RotaStampOf(new[] { "till-a", "till-b", "mop", "mop-2" }, new[] { "e1", "e2" }, 9 * 32 + 17), "rota stamp: a station fewer or more changes it");
        Check(stamp != RotaStampOf(new[] { "till-a", "till-b", "mop" }, new[] { "e1", "e3" }, 9 * 32 + 17), "rota stamp: somebody leaving and another taken on changes it");
        Check(stamp != RotaStampOf(new[] { "till-a", "till-b", "mop" }, new[] { "e1", "e2" }, 9 * 32 + 18), "rota stamp: an hour more of opening changes it");
        Check(RotaLedger.Mix(RotaLedger.Mix(RotaLedger.StampStart, 1), 2) != RotaLedger.Mix(RotaLedger.Mix(RotaLedger.StampStart, 2), 1), "rota stamp: the same figures in another order are another stamp");
        Check(RotaLedger.Hash(null) != RotaLedger.Hash(string.Empty) && RotaLedger.Hash("ab") != RotaLedger.Hash("ba") && RotaLedger.Hash("till") == RotaLedger.Hash("till"), "rota stamp: a missing id, an empty one and two ids of the same letters are told apart");
        var seen = new HashSet<long>();
        bool unique = true;
        for (int i = 0; i < 2000; i++)
        {
            unique &= seen.Add(RotaStampOf(new[] { "item-" + i, "item-" + (i + 1) }, new[] { "e" + (i % 7) }, i % 24));
        }
        Check(unique, "rota stamp: two thousand businesses that differ by one id have two thousand stamps");
    }

    // The promotion figure the mod writes, and so the one the forecast of another day is asked with: one function for both.
    private static void RotaPromotionChecks()
    {
        const float Base = 0.5f;
        Check(BusinessCatalogue.Written(40, 0, 0, Base) == 40 && BusinessCatalogue.Written(0, 0, 0, Base) == 0 && BusinessCatalogue.Written(100, 0, 0, Base) == 100, "rota forecast: with nothing of the mod's on a day, the figure is the business's own");
        Check(BusinessCatalogue.Written(40, 10, 0, Base) == BusinessCatalogue.PromotionFor(40, 10, 0, Base) && BusinessCatalogue.Written(40, 0, -8, Base) == BusinessCatalogue.PromotionFor(40, 0, -8, Base),
            "rota forecast: with something on, it is the figure the mod's own arithmetic gives");
        int own = 40, with = BusinessCatalogue.Written(own, 0, 10, Base);
        float gain = (Base + 0.75f * with / 100f) / (Base + 0.75f * own / 100f);
        Check(with > own && Math.Abs(gain - 1.10f) < 0.01f, "rota forecast: a campaign of ten per cent gives a figure that brings ten per cent more customers by the game's formula (" + with + ", " + gain.ToString("0.000") + ")");
        Check(BusinessCatalogue.Written(100, 0, 10, Base) == 100 && BusinessCatalogue.Written(97, 0, 15, Base) == 100, "rota forecast: a business whose figure is at the top gains nothing from a campaign: the figure stops at 100");
        Check(BusinessCatalogue.Written(0, -10, -20, 0f) == 0, "rota forecast: and it never goes under nothing");
        Check(BusinessCatalogue.Written(40, 0, 40, Base) == BusinessCatalogue.Written(40, 0, BusinessCatalogue.AmbientMostBoost, Base), "rota forecast: what the economy and the company add together is kept to its bound, as when it is written");
        Check(BusinessCatalogue.Written(40, -50, 0, Base) == BusinessCatalogue.Written(40, BusinessCatalogue.MostDip, 0, Base), "rota forecast: and one of business pressure's own changes to its own");
        Check(BusinessCatalogue.Written(40, -12, 10, Base) < BusinessCatalogue.Written(40, 0, 10, Base) && BusinessCatalogue.Written(40, -12, 10, Base) == BusinessCatalogue.PromotionFor(40, -12, 10, Base),
            "rota forecast: an issue that ends within the week and a campaign that runs on are counted together on the days both are there, and apart afterwards");
    }

    private static void RotaPermitChecks()
    {
        var barred = new bool[RotaCells];
        RotaAdvisor.LateHours(barred);
        Check(barred[0] && barred[4] && !barred[5] && !barred[21] && barred[22] && barred[23] && barred[6 * 24 + 23] && !barred[6 * 24 + 12], "rota permit: without the late opening permit, the hours from 22:00 and before 05:00 are not to be opened");
        var advisor = new RotaAdvisor();
        var net = new int[RotaCells];
        var open = new bool[RotaCells];
        var random = new Random(11);
        bool within = true, shape = true;
        for (int round = 0; round < 100; round++)
        {
            for (int cell = 0; cell < RotaCells; cell++)
            {
                net[cell] = random.Next(-2000, 4000);
            }
            advisor.Blocks(net, barred, new int[7], open);
            for (int d = 0; d < 7; d++)
            {
                for (int h = 0; h < 24;)
                {
                    if (!open[d * 24 + h])
                    {
                        h++;
                        continue;
                    }
                    int from = h;
                    while (h < 24 && open[d * 24 + h])
                    {
                        h++;
                    }
                    within &= !LicenceRules.IsLate(from, h);
                    shape &= h - from >= RotaCatalogue.BlockLeast;
                }
            }
        }
        Check(within && shape, "rota permit: on 100 weeks of chance no block advised without the permit is late by the licence's own rule");

        // A shop worth opening from 16:00 to midnight.
        RotaShop shop = RotaTills(1);
        for (int cell = 0; cell < RotaCells; cell++)
        {
            int h = cell % 24;
            shop.Open[cell] = h >= 16 && h < 24;
            shop.Customers[cell] = h >= 16 ? 30 : 0;
        }
        RotaProblem p = RotaMake(1, 4);
        var outlook = new RotaOutlook();
        advisor.Advise(shop, p, RotaSolverUnderTest, null, barred, outlook);
        bool none = true;
        for (int cell = 0; cell < RotaCells; cell++)
        {
            none &= !(outlook.Best[cell] && barred[cell]) && !(outlook.Staffed[cell] && barred[cell]);
        }
        Check(none && outlook.HoursBest == 7 * 6 && outlook.HoursStaffed == 42, "rota permit: without the permit the advice is 16:00 to 22:00, and the hours the staff can keep stay inside it too (" + outlook.HoursBest + ")");
        Check(outlook.Barred && outlook.HoursFree == 7 * 8 && outlook.ProfitFree - outlook.ProfitBest == 14 * (20 * 400L - 1500), "rota permit: and it says what the permit would add: fourteen hours, each worth its customers less its wages (" + (outlook.ProfitFree - outlook.ProfitBest) + ")");
        advisor.Advise(shop, p, RotaSolverUnderTest, null, null, outlook);
        Check(!outlook.Barred && outlook.HoursBest == 56 && outlook.Best[23], "rota permit: with the permit the advice runs to midnight and nothing is held back");
        for (int cell = 0; cell < RotaCells; cell++)
        {
            shop.Customers[cell] = cell % 24 >= 9 && cell % 24 < 18 ? 30 : 0;
        }
        advisor.Advise(shop, p, RotaSolverUnderTest, null, barred, outlook);
        Check(!outlook.Barred && outlook.HoursBest == 63 && outlook.ProfitFree == 0, "rota permit: a business that is worth opening by day only has nothing held back for want of the permit");
    }

    // A rota and an advice made a step at a time are the ones made in one go, and no step is long.
    private static void RotaStepped()
    {
        var whole = new RotaSolver();
        var stepped = new RotaSolver();
        var advisorWhole = new RotaAdvisor();
        var advisorStepped = new RotaAdvisor();
        var barred = new bool[RotaCells];
        RotaAdvisor.LateHours(barred);
        int differ = 0, advicesDiffer = 0, steps = 0, most = 0;
        for (int seed = 1; seed <= 100; seed++)
        {
            int stations = 2 + seed % 9, workers = 2 + (seed * 5) % 17;
            RotaProblem p = RotaGenerate(seed, stations, workers, seed % 4 != 0);
            RotaPlan a = whole.Solve(p, new RotaPlan());
            RotaPlan b = stepped.Start(p, new RotaPlan());
            int n = 0;
            while (!stepped.Step(1 + seed % 3))
            {
                n++;
            }
            steps += n;
            most = Math.Max(most, n);
            bool same = a.CoveredValue == b.CoveredValue && a.Wage == b.Wage && a.UnmetWeight == b.UnmetWeight && a.Shifts == b.Shifts && a.Level == b.Level && a.Gaps.Count == b.Gaps.Count && a.Unmet.Count == b.Unmet.Count;
            for (int i = 0; same && i < RotaCells * stations; i++)
            {
                same = a.At[i] == b.At[i];
            }
            differ += same ? 0 : 1;

            // The same business as a shop of tills, for the advice.
            var shop = new RotaShop();
            shop.Size(stations, 1);
            shop.GroupKind[0] = RotaGroupKind.Service;
            shop.HasCustomers = true;
            shop.Margin = 300 + seed * 7;
            for (int s = 0; s < stations; s++)
            {
                shop.Cap[s] = 10 + (s * 7 + seed) % 20;
                shop.StationWage[s] = 1400;
            }
            for (int cell = 0; cell < RotaCells; cell++)
            {
                shop.Open[cell] = cell % 24 >= 9 && cell % 24 < 17;
                shop.Customers[cell] = cell % 24 >= 7 ? (cell * 13 + seed * 5) % 90 : 0;
            }
            for (int s = 0; s < stations; s++)
            {
                p.Kind[s] = RotaKind.Work;
            }
            var first = new RotaOutlook();
            var second = new RotaOutlook();
            RotaProblem q = RotaGenerate(seed, stations, workers, seed % 4 != 0);
            for (int s = 0; s < stations; s++)
            {
                q.Kind[s] = RotaKind.Work;
            }
            bool[] bar = seed % 2 == 0 ? barred : null;
            advisorWhole.Advise(shop, p, whole, null, bar, first);
            advisorStepped.Begin(shop, q, stepped, null, bar, second);
            while (!advisorStepped.Step(1))
            {
            }
            same = first.ProfitNow == second.ProfitNow && first.ProfitStaffed == second.ProfitStaffed && first.ProfitBest == second.ProfitBest && first.ProfitFree == second.ProfitFree
                   && first.HoursStaffed == second.HoursStaffed && first.HoursBest == second.HoursBest && first.HoursFree == second.HoursFree && first.Barred == second.Barred
                   && first.Plan.Wage == second.Plan.Wage && first.Roles.Count == second.Roles.Count;
            for (int cell = 0; same && cell < RotaCells; cell++)
            {
                same = first.Best[cell] == second.Best[cell] && first.Staffed[cell] == second.Staffed[cell] && first.Net[cell] == second.Net[cell];
            }
            for (int i = 0; same && i < RotaCells * stations; i++)
            {
                same = first.Plan.At[i] == second.Plan.At[i] && p.Value[i] == q.Value[i];
            }
            advicesDiffer += same ? 0 : 1;
        }
        Check(differ == 0, "rota in steps: on 100 generated businesses the rota made a step at a time is the rota made in one go (" + differ + " differ)");
        Check(advicesDiffer == 0, "rota in steps: and so is the advice, with its hours, its sums and its rota (" + advicesDiffer + " differ)");
        Check(steps > 100 * 20, "rota in steps: a rota is many steps, not one (" + steps / 100 + " on average, " + most + " at most)");

        // The largest business: no step is long, so a few of them fit the share of a frame the director gives them.
        RotaProblem large = RotaGenerate(99, 40, 60, true);
        for (int cell = 0; cell < RotaCells; cell++)
        {
            for (int s = 0; s < 40; s++)
            {
                large.Value[cell * 40 + s] = cell % 24 >= 6 && cell % 24 < 22 ? 50 + (s * 37 + cell * 11) % 400 : 0;
            }
        }
        var plan = new RotaPlan();
        for (int i = 0; i < 20; i++)
        {
            stepped.Solve(large, plan);
        }
        // Each step is timed ten times and its quickest time counts: a pause of the runtime that happens to fall into one run is not the step's.
        var watch = new Stopwatch();
        var quickest = new List<double>();
        for (int run = 0; run < 10; run++)
        {
            stepped.Start(large, plan);
            bool done = false;
            for (int step = 0; !done; step++)
            {
                watch.Restart();
                done = stepped.Step(1);
                watch.Stop();
                double ms = watch.Elapsed.TotalMilliseconds;
                if (step == quickest.Count)
                {
                    quickest.Add(ms);
                }
                quickest[step] = Math.Min(quickest[step], ms);
            }
        }
        double longest = 0, total = 0;
        int count = quickest.Count * 10;
        foreach (double ms in quickest)
        {
            longest = Math.Max(longest, ms);
            total += ms * 10;
        }
        Check(longest < 0.5, "rota in steps, the largest business: the longest single step takes under half a millisecond here (" + longest.ToString("0.000") + " ms), so one step stays within a frame's share on a runtime several times slower");
        Console.WriteLine("  rota in steps, 40 stations and 60 employees: " + count / 10 + " steps a rota, " + (total / 10).ToString("0.00") + " ms in all, the longest step " + longest.ToString("0.000")
                          + " ms; at 2 ms a frame that is about " + Math.Ceiling(total / 10 / 2.0) + " frame(s) here and " + Math.Ceiling(total / 10 * 8 / 2.0) + " on a runtime eight times slower");
    }
}
