using System;

namespace RealLifeOverhaul.Careers
{
    /// <summary>
    /// The test buttons' rules, game-free so that the offline checks run the same: every task the player's rung handles brought
    /// at once, and a task decided by its first choice with what it did checked against what it should have done.
    /// </summary>
    public static partial class CareerLadderRules
    {
        /// <summary>
        /// One task of each kind the player's rung decides, now, at the player's unit: a supervisor's cover and two demands (a shift
        /// to answer, a raise to pass up); a store manager's post to fill and a demand that costs; a regional manager's three
        /// matters sent up and a store to appoint a manager to; an executive's matter of their seat. Returns how many were brought.
        /// </summary>
        public static int BringAll(LadderFile f, int day)
        {
            CityCompany c = f.Company;
            if (c.Stores.Count == 0)
            {
                return 0;
            }
            int before = f.Tasks.Count;
            switch ((Rung)f.Rung)
            {
                case Rung.Supervisor:
                    Queue(f, TaskKind.Cover, 0, -1, 0, day, 0, day);
                    Wanting(f, 0, StaffWant.Shift, day);
                    Wanting(f, 0, StaffWant.Raise, day);
                    break;
                case Rung.StoreManager:
                    Queue(f, TaskKind.Hire, 0, -1, 0, day, 0);
                    Wanting(f, 0, StaffWant.Equipment, day);
                    if (PlayerHolds(f, (int)Rung.Supervisor, 0))
                    {
                        Queue(f, TaskKind.Cover, 0, -1, 0, day, 0, day);
                    }
                    break;
                case Rung.Regional:
                    {
                        int s = OtherInUnit(f);
                        Queue(f, TaskKind.Escalation, s, -1, (int)Concern.Gap, day, 0);
                        Queue(f, TaskKind.Escalation, s, -1, (int)Concern.Morale, day, OutingCost);
                        Colleague p = FloorPerson(c, s);
                        if (p != null)
                        {
                            p.Want = (int)StaffWant.Insurance;
                            p.WantDay = day;
                            Queue(f, TaskKind.Escalation, s, p.Id, (int)Concern.Budget, day, Cost(StaffWant.Insurance));
                        }
                        Colleague manager = Holder(c, s, (int)Rung.StoreManager);
                        if (manager != null && s != 0)
                        {
                            manager.Role = (int)Rung.Frontline;   // the store's manager has gone back to the floor: one to appoint
                        }
                        if (Holder(c, s, (int)Rung.StoreManager) == null)
                        {
                            Queue(f, TaskKind.Appoint, s, -1, 0, day, 0);
                        }
                        break;
                    }
                case Rung.Executive:
                    {
                        Concern about = f.Seat == (int)ExecSeat.Chro ? Concern.Morale : f.Seat == (int)ExecSeat.Cfo ? Concern.Budget : Concern.Gap;
                        int s = OtherInUnit(f);
                        Colleague p = about == Concern.Budget ? FloorPerson(c, s) : null;
                        if (p != null)
                        {
                            p.Want = (int)StaffWant.Equipment;
                            p.WantDay = day;
                        }
                        Queue(f, TaskKind.Escalation, s, p != null ? p.Id : -1, (int)about, day, about == Concern.Morale ? OutingCost : about == Concern.Budget ? Cost(StaffWant.Equipment) : 0);
                        break;
                    }
            }
            return f.Tasks.Count - before;
        }

        // A demand of a member of staff on the floor, as the player's task.
        private static void Wanting(LadderFile f, int s, StaffWant w, int day)
        {
            Colleague p = FloorPerson(f.Company, s, w);
            if (p == null)
            {
                return;
            }
            p.Want = (int)w;
            p.WantDay = day;
            Queue(f, TaskKind.Demand, s, p.Id, (int)w, day, Cost(w));
        }

        // A person on the floor at a store, one with nothing asked first, and not one asked for something else already.
        private static Colleague FloorPerson(CityCompany c, int s, StaffWant w = StaffWant.None)
        {
            Colleague any = null;
            for (int i = 0; i < c.People.Count; i++)
            {
                Colleague p = c.People[i];
                if (p.Store != s || p.Role != (int)Rung.Frontline)
                {
                    continue;
                }
                if (p.Want == (int)StaffWant.None)
                {
                    return p;
                }
                any = any ?? (p.Want == (int)w ? p : null);
            }
            return any;
        }

        // A store of the player's unit other than their own where there is one, else their own.
        private static int OtherInUnit(LadderFile f)
        {
            for (int s = 1; s < f.Company.Stores.Count; s++)
            {
                if (InUnit(f, s))
                {
                    return s;
                }
            }
            return 0;
        }

        /// <summary>
        /// A task decided by its first choice (yes, with the first person or the first candidate within the policy; passed up where
        /// yes is not the rung's), then checked: the gap covered, a person taken on, the demand answered, the matter settled, a
        /// manager appointed. False with why in <paramref name="what"/> if it did not do what it should.
        /// </summary>
        public static bool DecideFirst(LadderFile f, LadderTask t, int seed, int day, Func<int, string> nameOf, out string what)
        {
            CityCompany c = f.Company;
            int s = t.Store, staff = s >= 0 && s < c.Stores.Count ? StaffAt(c, s) : 0;
            Colleague p = c.PersonOf(t.Person);
            int pick = -1;
            Verdict choice = Verdict.Yes;
            switch ((TaskKind)t.Kind)
            {
                case TaskKind.Cover:
                    pick = FirstAt(c, s, false);
                    break;
                case TaskKind.Appoint:
                    pick = FirstAt(c, s, true);
                    break;
                case TaskKind.Hire:
                    for (int k = 0; k < CandidatesEach && pick < 0; k++)
                    {
                        Candidate(seed, t.Id, k, out int skill, out int wage);
                        pick = WithinPolicy(c.Policy, skill, wage) ? k : -1;
                    }
                    choice = pick >= 0 ? Verdict.Yes : Verdict.Up;
                    break;
                case TaskKind.Demand:
                    choice = LadderRanks.Has(f.Rung, t.About == (int)StaffWant.Shift ? RungPower.ShiftAsks : t.About == (int)StaffWant.Raise ? RungPower.Raises : RungPower.Demands) ? Verdict.Yes : Verdict.Up;
                    if (choice == Verdict.Yes && t.About != (int)StaffWant.Raise && t.About != (int)StaffWant.Shift && !Allowed(c, s, (StaffWant)t.About))
                    {
                        choice = Verdict.Up;
                    }
                    break;
                case TaskKind.Escalation:
                    if (t.About == (int)Concern.Gap)
                    {
                        Colleague spare = Spare(f, s, true);
                        pick = spare != null ? spare.Id : -1;
                        choice = spare != null ? Verdict.Yes : Verdict.No;   // no one to move: the hire over the policy
                    }
                    break;
            }
            int lowBefore = s >= 0 && s < c.Stores.Count ? MoraleAt(c, s) : 0;
            string why = Answer(f, t.Id, choice, pick, seed, day, nameOf);
            if (why != null)
            {
                what = "refused (" + why + ")";
                return false;
            }
            bool ok;
            switch ((TaskKind)t.Kind)
            {
                case TaskKind.Cover:
                    ok = c.Stores[s].Covered == day;
                    what = "the shift's gap covered by person " + pick;
                    break;
                case TaskKind.Hire:
                    ok = choice == Verdict.Up ? c.Stores[s].GapDays >= c.Policy.Escalate : StaffAt(c, s) == staff + 1;
                    what = choice == Verdict.Up ? "no candidate within the policy: sent up to the region" : "candidate " + pick + " taken on, staff " + staff + " to " + StaffAt(c, s);
                    break;
                case TaskKind.Demand:
                    ok = p != null && p.Want == (int)StaffWant.None;
                    what = "the " + (StaffWant)t.About + " demand " + (choice == Verdict.Up ? "passed up and decided above" : "met");
                    break;
                case TaskKind.Escalation:
                    if (t.About == (int)Concern.Gap)
                    {
                        ok = StaffAt(c, s) == staff + 1 && c.Stores[s].GapDays == 0;
                        what = choice == Verdict.Yes ? "person " + pick + " moved to the store" : "a hire over the policy allowed";
                    }
                    else if (t.About == (int)Concern.Morale)
                    {
                        ok = c.Stores[s].LowDays == 0 && MoraleAt(c, s) > lowBefore;
                        what = "a staff outing, morale " + lowBefore + " to " + MoraleAt(c, s);
                    }
                    else
                    {
                        ok = p == null || p.Want == (int)StaffWant.None;
                        what = "the spending over the budget approved";
                    }
                    break;
                default:
                    ok = Holder(c, s, (int)Rung.StoreManager) != null;
                    what = "person " + pick + " appointed the store's manager";
                    break;
            }
            ok &= f.TaskOf(t.Id) == null;
            return ok;
        }

        // The first person at a store who can take a shift's cover (anyone) or a manager's post (not one already).
        private static int FirstAt(CityCompany c, int s, bool manager)
        {
            for (int i = 0; i < c.People.Count; i++)
            {
                Colleague p = c.People[i];
                if (p.Store == s && (!manager || p.Role != (int)Rung.StoreManager))
                {
                    return p.Id;
                }
            }
            return -1;
        }
    }
}
