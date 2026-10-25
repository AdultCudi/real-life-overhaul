using System;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>A field of the company's policy, each one executive's to set: the saved number nowhere, the Career app's buttons only.</summary>
    public enum PolicyField
    {
        Staffing = 0, Escalate = 1,           // operations
        Floor = 2, Band = 3, WageTop = 4,     // people
        Budget = 5                            // finance
    }

    /// <summary>
    /// The player's decisions, game-free: a task answered (yes, no or passed up, with the person or candidate picked), and the
    /// tools a rung brings that are not tasks (the store's schedule, a raise, a transfer, the company's policy). Each checks the
    /// rung's power first and says why not as a locale key, so a button the rung does not have never acts.
    /// </summary>
    public static partial class CareerLadderRules
    {
        public const int CandidatesEach = 3;   // candidates for a post

        /// <summary>A candidate for a post: a skill and the pay they ask, the same each time it is asked (the task's number and the seed).</summary>
        public static void Candidate(int seed, int task, int k, out int skill, out int wage)
        {
            skill = TechRandom.Range(seed, Salt + 20, task, k, 20, 90);
            wage = WageFor(skill) * TechRandom.Range(seed, Salt + 21, task, k, 90, 115) / 100;
        }

        /// <summary>A candidate's name's number (for the name the game would give them).</summary>
        public static int CandidateName(int seed, int task, int k) => (int)TechRandom.Hash(seed, Salt + 22, task, k);

        /// <summary>True if a candidate is within the company's hiring policy: the skill's floor and the most it pays.</summary>
        public static bool WithinPolicy(CompanyPolicy p, int skill, int wage) => skill >= p.Floor && wage <= p.WageTop;

        /// <summary>
        /// The player decides a task. <paramref name="pick"/> is the person (a cover, a transfer, an appointment) or the candidate's
        /// place (a hire). Null if done, else why not as a locale key; a decision taken counts toward the score.
        /// </summary>
        public static string Answer(LadderFile f, int id, Verdict choice, int pick, int seed, int day, Func<int, string> nameOf)
        {
            LadderTask t = f.TaskOf(id);
            CityCompany c = f.Company;
            if (t == null || t.Store < 0 || t.Store >= c.Stores.Count)
            {
                return "reallife_ladder_why_no_task";
            }
            if (choice == Verdict.Up && !LadderRanks.Has(f.Rung, RungPower.PassUp))
            {
                return "reallife_ladder_why_power";
            }
            string why;
            switch ((TaskKind)t.Kind)
            {
                case TaskKind.Cover: why = Cover(f, t, choice, pick, day); break;
                case TaskKind.Hire: why = Hire(f, t, choice, pick, seed, day, nameOf); break;
                case TaskKind.Demand: why = Demand(f, t, choice, day); break;
                case TaskKind.Escalation: why = Escalation(f, t, choice, pick, seed, day, nameOf); break;
                case TaskKind.Appoint: why = Appoint(f, t, choice, pick); break;
                default: why = "reallife_ladder_why_no_task"; break;
            }
            if (why != null)
            {
                return why;
            }
            f.Tasks.Remove(t);
            f.Handled++;
            Figures(f, day);
            return null;
        }

        // A gap on today's shift: someone of the staff covers it (overtime, and a little of their morale), it runs short, or the store manager is asked to hire sooner.
        private static string Cover(LadderFile f, LadderTask t, Verdict choice, int pick, int day)
        {
            CityCompany c = f.Company;
            Outlet st = c.Stores[t.Store];
            switch (choice)
            {
                case Verdict.Yes:
                    {
                        Colleague p = c.PersonOf(pick);
                        if (p == null || p.Store != t.Store)
                        {
                            return "reallife_ladder_why_pick";
                        }
                        st.Covered = day;
                        p.Morale = TalentCatalogue.Clamp(p.Morale - 6, 0, 100);
                        return null;
                    }
                case Verdict.No:
                    for (int i = 0; i < c.People.Count; i++)
                    {
                        if (c.People[i].Store == t.Store)
                        {
                            c.People[i].Morale = TalentCatalogue.Clamp(c.People[i].Morale - 2, 0, 100);
                        }
                    }
                    return null;
                default:
                    if (f.Rung >= (int)Rung.StoreManager)
                    {
                        return "reallife_ladder_why_yours";   // the store's manager is the player: nobody to pass it to
                    }
                    st.Pushed = day;
                    return null;
            }
        }

        // A post to fill: one of the candidates within the policy, keep looking, or ask the region for help.
        private static string Hire(LadderFile f, LadderTask t, Verdict choice, int pick, int seed, int day, Func<int, string> nameOf)
        {
            CityCompany c = f.Company;
            if (!LadderRanks.Has(f.Rung, RungPower.Hire))
            {
                return "reallife_ladder_why_power";
            }
            switch (choice)
            {
                case Verdict.Yes:
                    {
                        if (pick < 0 || pick >= CandidatesEach)
                        {
                            return "reallife_ladder_why_pick";
                        }
                        Candidate(seed, t.Id, pick, out int skill, out int wage);
                        if (!WithinPolicy(c.Policy, skill, wage))
                        {
                            return skill < c.Policy.Floor ? "reallife_ladder_why_floor" : "reallife_ladder_why_wage";
                        }
                        Colleague p = AddPerson(c, t.Store, string.Empty, nameOf != null ? nameOf(CandidateName(seed, t.Id, pick)) : "?", skill, wage, 72);
                        p.RaisedDay = day;
                        c.Stores[t.Store].GapDays = 0;
                        return null;
                    }
                case Verdict.No:
                    return null;   // looked again tomorrow, if the post is still open
                default:
                    c.Stores[t.Store].GapDays = Math.Max(c.Stores[t.Store].GapDays, c.Policy.Escalate);   // up to the region at once
                    return null;
            }
        }

        // A demand: met (a raise needs the raise power and stays in the band; a cost needs the demands power and the budget), turned down, or passed up.
        private static string Demand(LadderFile f, LadderTask t, Verdict choice, int day)
        {
            CityCompany c = f.Company;
            Colleague p = c.PersonOf(t.Person);
            if (p == null || p.Want == (int)StaffWant.None)
            {
                f.Tasks.Remove(t);
                return "reallife_ladder_why_no_task";
            }
            StaffWant w = (StaffWant)p.Want;
            switch (choice)
            {
                case Verdict.Yes:
                    {
                        RungPower needs = w == StaffWant.Shift ? RungPower.ShiftAsks : w == StaffWant.Raise ? RungPower.Raises : RungPower.Demands;
                        if (!LadderRanks.Has(f.Rung, needs))
                        {
                            return w == StaffWant.Raise ? "reallife_ladder_why_raise_power" : "reallife_ladder_why_power";
                        }
                        if (w == StaffWant.Raise && day - p.RaisedDay < RaiseGapDays)
                        {
                            return "reallife_ladder_why_raised";
                        }
                        if (w != StaffWant.Raise && !Allowed(c, t.Store, w))
                        {
                            return "reallife_ladder_why_budget";
                        }
                        Decide(c, t.Store, p, true, day);
                        return null;
                    }
                case Verdict.No:
                    Decide(c, t.Store, p, false, day);
                    return null;
                default:
                    {
                        // the manager above decides it as the company would: a raise within the band, money up to half over the week's budget
                        bool yes = w == StaffWant.Raise ? day - p.RaisedDay >= RaiseGapDays : c.Stores[t.Store].Spent + Cost(w) <= c.Policy.Budget * 3 / 2;
                        Decide(c, t.Store, p, yes, day);
                        return null;
                    }
            }
        }

        // Sent up from a store: a gap (a transfer of the person picked, or a hire over the policy), low morale (an outing), money (approved or not).
        private static string Escalation(LadderFile f, LadderTask t, Verdict choice, int pick, int seed, int day, Func<int, string> nameOf)
        {
            CityCompany c = f.Company;
            Outlet st = c.Stores[t.Store];
            if (!LadderRanks.Has(f.Rung, RungPower.Resolve) || f.Rung < (int)Rung.Regional)
            {
                return "reallife_ladder_why_power";
            }
            if (choice == Verdict.Up)
            {
                if (f.Rung >= (int)Rung.Executive)
                {
                    return "reallife_ladder_why_yours";
                }
                Settle(f, t.Store, (Concern)t.About, seed, day, nameOf);   // the executive settles it as the company would
                return null;
            }
            switch ((Concern)t.About)
            {
                case Concern.Gap:
                    if (choice == Verdict.Yes)
                    {
                        Colleague p = c.PersonOf(pick);
                        if (p == null || p.Store == t.Store || !InUnit(f, p.Store) || p.Role != (int)Rung.Frontline)
                        {
                            return "reallife_ladder_why_pick";
                        }
                        p.Store = t.Store;
                        p.Morale = TalentCatalogue.Clamp(p.Morale - 3, 0, 100);
                    }
                    else
                    {
                        HireOne(f, t.Store, seed, day, nameOf, c.Policy.Floor, c.Policy.WageTop + 300);   // a hire over the policy's pay, the region's to allow
                    }
                    st.GapDays = 0;
                    return null;
                case Concern.Morale:
                    if (choice == Verdict.Yes)
                    {
                        Outing(c, t.Store);
                    }
                    st.LowDays = 0;
                    return null;
                default:
                    {
                        Colleague p = c.PersonOf(t.Person);
                        if (p != null && p.Want != (int)StaffWant.None)
                        {
                            bool yes = choice == Verdict.Yes;
                            if (yes)
                            {
                                st.Spent += Cost((StaffWant)p.Want);   // over the budget, on the region's word
                                p.Morale = TalentCatalogue.Clamp(p.Morale + 10, 0, 100);
                                p.Want = (int)StaffWant.None;
                            }
                            else
                            {
                                Decide(c, t.Store, p, false, day);
                            }
                        }
                        return null;
                    }
            }
        }

        // A store without a manager: the regional manager appoints one of its staff, or leaves it to head office.
        private static string Appoint(LadderFile f, LadderTask t, Verdict choice, int pick)
        {
            CityCompany c = f.Company;
            if (!LadderRanks.Has(f.Rung, RungPower.Appoint))
            {
                return "reallife_ladder_why_power";
            }
            if (choice != Verdict.Yes)
            {
                Colleague best = MostSkilled(c, t.Store);
                if (best != null)
                {
                    best.Role = (int)Rung.StoreManager;
                }
                return null;
            }
            Colleague p = c.PersonOf(pick);
            if (p == null || p.Store != t.Store || p.Role == (int)Rung.StoreManager)
            {
                return "reallife_ladder_why_pick";
            }
            p.Role = (int)Rung.StoreManager;
            p.Wage = (int)((long)p.Wage * (10000 + c.Policy.Band) / 10000);
            p.Morale = TalentCatalogue.Clamp(p.Morale + 10, 0, 100);
            return null;
        }

        // ---------------------------------------------------------------- tools that are not tasks

        /// <summary>The store manager sets the store's schedule (0 lean, 1 standard, 2 full), no fuller than the company's staffing policy.</summary>
        public static string SetRota(LadderFile f, int rota)
        {
            if (!LadderRanks.Has(f.Rung, RungPower.Rota) || f.Rung != (int)Rung.StoreManager || f.Company.Stores.Count == 0)
            {
                return "reallife_ladder_why_power";
            }
            rota = TalentCatalogue.Clamp(rota, 0, 2);
            if (rota > f.Company.Policy.Staffing)
            {
                return "reallife_ladder_why_policy_rota";
            }
            f.Company.Stores[0].Rota = rota;
            return null;
        }

        /// <summary>A small raise for a person in the player's unit, within the policy's band, once in <see cref="RaiseGapDays"/>.</summary>
        public static string RaiseOne(LadderFile f, int person, int day)
        {
            CityCompany c = f.Company;
            Colleague p = c.PersonOf(person);
            if (!LadderRanks.Has(f.Rung, RungPower.Raises))
            {
                return "reallife_ladder_why_raise_power";
            }
            if (p == null || !InUnit(f, p.Store))
            {
                return "reallife_ladder_why_pick";
            }
            if (day - p.RaisedDay < RaiseGapDays)
            {
                return "reallife_ladder_why_raised";
            }
            GiveRaise(c, p, day);
            if (p.Want == (int)StaffWant.Raise)
            {
                p.Want = (int)StaffWant.None;
                RemoveTask(f, TaskKind.Demand, p.Id);
            }
            return null;
        }

        /// <summary>A member of staff moved to another store of the player's unit.</summary>
        public static string Move(LadderFile f, int person, int to)
        {
            CityCompany c = f.Company;
            Colleague p = c.PersonOf(person);
            if (!LadderRanks.Has(f.Rung, RungPower.Transfers))
            {
                return "reallife_ladder_why_power";
            }
            if (p == null || p.Role != (int)Rung.Frontline || !InUnit(f, p.Store) || !InUnit(f, to) || to == p.Store)
            {
                return "reallife_ladder_why_pick";
            }
            p.Store = to;
            p.Morale = TalentCatalogue.Clamp(p.Morale - 3, 0, 100);
            return null;
        }

        /// <summary>The seat a policy field belongs to.</summary>
        public static ExecSeat SeatOf(PolicyField field) => field <= PolicyField.Escalate ? ExecSeat.Coo : field <= PolicyField.WageTop ? ExecSeat.Chro : ExecSeat.Cfo;

        /// <summary>A policy field's value, its least and most, and the step one press moves it.</summary>
        public static void Range(CompanyPolicy p, PolicyField field, out int value, out int least, out int most, out int step)
        {
            switch (field)
            {
                case PolicyField.Staffing: value = p.Staffing; least = 0; most = 2; step = 1; return;
                case PolicyField.Escalate: value = p.Escalate; least = 2; most = 7; step = 1; return;
                case PolicyField.Floor: value = p.Floor; least = 20; most = 70; step = 5; return;
                case PolicyField.Band: value = p.Band; least = 200; most = 1000; step = 100; return;
                case PolicyField.WageTop: value = p.WageTop; least = 1500; most = 4000; step = 100; return;
                default: value = p.Budget; least = 200; most = 2000; step = 100; return;
            }
        }

        /// <summary>The executive moves a field of their seat's policy a step up or down; every store keeps to it from the next decision. A store's schedule fuller than the new staffing is brought down to it.</summary>
        public static string SetPolicy(LadderFile f, PolicyField field, int way)
        {
            if (!LadderRanks.Has(f.Rung, RungPower.Policy) || f.Rung != (int)Rung.Executive)
            {
                return "reallife_ladder_why_power";
            }
            if ((int)SeatOf(field) != f.Seat)
            {
                return "reallife_ladder_why_seat";
            }
            CompanyPolicy p = f.Company.Policy;
            Range(p, field, out int value, out int least, out int most, out int step);
            int to = TalentCatalogue.Clamp(value + (way < 0 ? -step : step), least, most);
            if (to == value)
            {
                return "reallife_ladder_why_policy_end";
            }
            switch (field)
            {
                case PolicyField.Staffing:
                    p.Staffing = to;
                    for (int s = 0; s < f.Company.Stores.Count; s++)
                    {
                        f.Company.Stores[s].Rota = Math.Min(f.Company.Stores[s].Rota, to);
                    }
                    break;
                case PolicyField.Escalate: p.Escalate = to; break;
                case PolicyField.Floor: p.Floor = to; break;
                case PolicyField.Band: p.Band = to; break;
                case PolicyField.WageTop: p.WageTop = to; break;
                default: p.Budget = to; break;
            }
            return null;
        }

        private static void RemoveTask(LadderFile f, TaskKind kind, int person)
        {
            for (int i = f.Tasks.Count - 1; i >= 0; i--)
            {
                if (f.Tasks[i].Kind == (int)kind && f.Tasks[i].Person == person)
                {
                    f.Tasks.RemoveAt(i);
                }
            }
        }
    }
}
