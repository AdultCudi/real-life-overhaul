using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>What a day at the company brought, for the director's one notice.</summary>
    public struct LadderNews
    {
        public int NewTasks, Lapsed, Left, Hired;
    }

    /// <summary>
    /// A day at the company, game-free: in the order the company's own managers work (the suggestion's order of automation):
    /// gaps first, then demands and morale, then what is sent up. Where a duty is the player's, it waits for them as a task;
    /// elsewhere the company's own manager does it, within the policy, by the dice. A loop over the stores and their people
    /// once a game day; nothing per frame.
    /// </summary>
    public static partial class CareerLadderRules
    {
        /// <summary>One game day at the company. <paramref name="nameOf"/> names the people it takes on.</summary>
        public static LadderNews Day(LadderFile f, int seed, int day, Func<int, string> nameOf)
        {
            var news = new LadderNews();
            CityCompany c = f.Company;
            if (c.Stores.Count == 0)
            {
                f.LastDay = day;
                return news;
            }
            if (f.Week != day / 7)
            {
                f.Week = day / 7;
                for (int s = 0; s < c.Stores.Count; s++)
                {
                    c.Stores[s].Spent = 0;   // a new week's budget
                }
            }
            Lapse(f, seed, day, nameOf, ref news);
            int salt = TechRandom.Of(c.Key);
            for (int s = 0; s < c.Stores.Count; s++)
            {
                Outlet st = c.Stores[s];
                int gap = Math.Max(0, Wanted(st) - StaffAt(c, s));
                bool covered = st.Covered == day - 1;   // yesterday's shift was covered
                st.GapDays = gap > 0 ? st.GapDays + 1 : 0;
                Moods(f, s, gap > 0 && !covered, day);
                news.Left += Leaving(f, seed, salt, s, day);
                Wants(f, seed, salt, s, day, ref news);
                Gaps(f, seed, salt, s, day, nameOf, ref news);
                int morale = MoraleAt(c, s);
                st.LowDays = morale < LowMorale ? st.LowDays + 1 : 0;
                SendUp(f, seed, salt, s, day, nameOf, ref news);
                KeepRoles(f, s);
                Appointing(f, s, day, ref news);
            }
            Figures(f, day);
            f.LastDay = day;
            return news;
        }

        /// <summary>Every store's results now: half its staffing, half its staff's morale.</summary>
        public static void Figures(LadderFile f, int day)
        {
            CityCompany c = f.Company;
            for (int s = 0; s < c.Stores.Count; s++)
            {
                c.Stores[s].Results = (Staffing(c, s, day) + MoraleAt(c, s)) / 2;
            }
        }

        // Morale drifts to the base; a gap left on the shift, a lean schedule, a demand left waiting pull it down, a full schedule and a manager on the floor up.
        private static void Moods(LadderFile f, int s, bool shortHanded, int day)
        {
            CityCompany c = f.Company;
            Outlet st = c.Stores[s];
            int by = (shortHanded ? -2 : 0) + (st.Rota == 0 ? -1 : st.Rota == 2 ? 1 : 0) + (st.Present >= day - 1 && st.Present >= 0 ? 1 : 0);
            for (int i = 0; i < c.People.Count; i++)
            {
                Colleague p = c.People[i];
                if (p.Store != s)
                {
                    continue;
                }
                int m = p.Morale + by + Math.Sign(MoraleBase - p.Morale);
                m -= p.Want != (int)StaffWant.None && day - p.WantDay > TaskDays ? 1 : 0;
                p.Morale = TalentCatalogue.Clamp(m, 0, 100);
            }
        }

        // Somebody whose morale has fallen too far may leave; a store manager leaving leaves the store without one.
        private static int Leaving(LadderFile f, int seed, int salt, int s, int day)
        {
            CityCompany c = f.Company;
            int left = 0;
            for (int i = c.People.Count - 1; i >= 0; i--)
            {
                Colleague p = c.People[i];
                if (p.Store == s && p.Morale < QuitBelow && TechRandom.Mille(seed, Salt + 10, day, p.Id ^ salt) < QuitChance)
                {
                    DropTasksOf(f, p.Id);
                    c.People.RemoveAt(i);
                    left++;
                }
            }
            return left;
        }

        private static void DropTasksOf(LadderFile f, int person)
        {
            for (int i = f.Tasks.Count - 1; i >= 0; i--)
            {
                if (f.Tasks[i].Person == person)
                {
                    f.Tasks.RemoveAt(i);
                }
            }
        }

        // Demands: new ones come; the player's to answer wait as tasks, the company's managers answer theirs by the dice.
        private static void Wants(LadderFile f, int seed, int salt, int s, int day, ref LadderNews news)
        {
            CityCompany c = f.Company;
            for (int i = 0; i < c.People.Count; i++)
            {
                Colleague p = c.People[i];
                if (p.Store != s || p.Role == (int)Rung.Regional || p.Role == (int)Rung.Executive)
                {
                    continue;
                }
                if (p.Want == (int)StaffWant.None)
                {
                    if (TechRandom.Mille(seed, Salt + 11, day, p.Id ^ salt) >= DemandChance)
                    {
                        continue;
                    }
                    p.Want = p.Wage < WageFor(p.Skill) ? (int)StaffWant.Raise : 1 + TechRandom.Range(seed, Salt + 12, day, p.Id ^ salt, 0, 3);
                    p.WantDay = day;
                }
                StaffWant w = (StaffWant)p.Want;
                int duty = w == StaffWant.Shift ? (int)Rung.Supervisor : (int)Rung.StoreManager;
                if (PlayerHolds(f, duty, s) || PlayerHolds(f, (int)Rung.Supervisor, s))
                {
                    news.NewTasks += Queue(f, TaskKind.Demand, s, p.Id, (int)w, day, Cost(w)) ? 1 : 0;
                }
                else if (!Waiting(f, p.Id) && (Holder(c, s, duty) ?? Holder(c, s, (int)Rung.StoreManager)) != null && TechRandom.Mille(seed, Salt + 13, day, p.Id ^ salt) < AiAnswer)
                {
                    bool yes = w == StaffWant.Raise ? day - p.RaisedDay >= RaiseGapDays : Allowed(c, s, w);
                    if (!yes && w != StaffWant.Raise && (PlayerHolds(f, (int)Rung.Regional, s) || (f.Rung == (int)Rung.Executive && f.Seat == (int)ExecSeat.Cfo)))
                    {
                        // over the store's budget: its manager sends it up to the player's region or finance seat
                        news.NewTasks += Queue(f, TaskKind.Escalation, s, p.Id, (int)Concern.Budget, day, Cost(w)) ? 1 : 0;
                    }
                    else
                    {
                        Decide(c, s, p, yes, day);
                    }
                }
            }
        }

        // True if a task of the player's is about this person: the company's managers leave it to them.
        private static bool Waiting(LadderFile f, int person)
        {
            for (int i = 0; i < f.Tasks.Count; i++)
            {
                if (f.Tasks[i].Person == person)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True if a store can meet a demand now: within its week's budget.</summary>
        public static bool Allowed(CityCompany c, int s, StaffWant w) => Cost(w) <= BudgetLeft(c, s);

        // A demand met (within the band and the budget) or turned down.
        private static void Decide(CityCompany c, int s, Colleague p, bool yes, int day)
        {
            StaffWant w = (StaffWant)p.Want;
            if (yes)
            {
                if (w == StaffWant.Raise)
                {
                    GiveRaise(c, p, day);
                }
                else
                {
                    c.Stores[s].Spent += Cost(w);
                    p.Morale = TalentCatalogue.Clamp(p.Morale + (w == StaffWant.Shift ? 8 : 10), 0, 100);
                }
            }
            else
            {
                p.Morale = TalentCatalogue.Clamp(p.Morale - 8, 0, 100);
            }
            p.Want = (int)StaffWant.None;
        }

        private static void GiveRaise(CityCompany c, Colleague p, int day)
        {
            p.Wage = (int)((long)p.Wage * (10000 + c.Policy.Band) / 10000);
            p.Morale = TalentCatalogue.Clamp(p.Morale + 12, 0, 100);
            p.RaisedDay = day;
        }

        // Gaps: the player's to fill or cover wait as tasks; the company's store manager fills theirs by the dice, sooner when a supervisor passed it up.
        private static void Gaps(LadderFile f, int seed, int salt, int s, int day, Func<int, string> nameOf, ref LadderNews news)
        {
            CityCompany c = f.Company;
            Outlet st = c.Stores[s];
            if (StaffAt(c, s) >= Wanted(st))
            {
                return;
            }
            if (PlayerHolds(f, (int)Rung.Supervisor, s) && st.Covered != day)
            {
                news.NewTasks += Queue(f, TaskKind.Cover, s, -1, 0, day, 0, day) ? 1 : 0;   // today's shift: decided today
            }
            if (PlayerHolds(f, (int)Rung.StoreManager, s))
            {
                news.NewTasks += Queue(f, TaskKind.Hire, s, -1, 0, day, 0) ? 1 : 0;
                return;
            }
            if (Holder(c, s, (int)Rung.StoreManager) == null)
            {
                return;   // nobody to hire: it goes up once it has waited the policy's days
            }
            int chance = st.Pushed >= day - TaskDays && st.Pushed >= 0 ? AiFill * 2 : AiFill;
            if (TechRandom.Mille(seed, Salt + 14, day, s ^ salt) < chance)
            {
                HireOne(f, s, seed, day, nameOf, c.Policy.Floor, c.Policy.WageTop);
                news.Hired++;
            }
        }

        // One person taken on at a store: a skill from the policy's floor up, paid what the skill is paid, no more than the policy's top.
        private static Colleague HireOne(LadderFile f, int s, int seed, int day, Func<int, string> nameOf, int floor, int wageTop)
        {
            CityCompany c = f.Company;
            int n = c.NextPerson;
            int skill = TechRandom.Range(seed, Salt + 15, day, n, TalentCatalogue.Clamp(floor, 0, 100), TalentCatalogue.Clamp(floor + 30, 0, 100));
            int wage = Math.Min(WageFor(skill), Math.Max(WageLeast, wageTop));
            Colleague p = AddPerson(c, s, string.Empty, nameOf != null ? nameOf((int)TechRandom.Hash(seed, Salt + 16, day, n)) : "?", skill, wage, 70);
            c.Stores[s].GapDays = StaffAt(c, s) >= Wanted(c.Stores[s]) ? 0 : c.Stores[s].GapDays;
            return p;
        }

        // What a store's managers cannot settle goes up: a gap left past the policy's days, morale low as long. To the player's region
        // or seat as a task; elsewhere the company's regional manager settles it by the dice, and after twice the days an executive does.
        private static void SendUp(LadderFile f, int seed, int salt, int s, int day, Func<int, string> nameOf, ref LadderNews news)
        {
            CityCompany c = f.Company;
            Outlet st = c.Stores[s];
            int days = Math.Max(1, c.Policy.Escalate);
            for (int about = 0; about < 2; about++)
            {
                int waited = about == (int)Concern.Gap ? st.GapDays : st.LowDays;
                if (waited < days || PlayerHolds(f, (int)Rung.StoreManager, s))
                {
                    continue;   // (the player's own store: its gaps and morale are theirs, not sent past them)
                }
                if (PlayerHolds(f, (int)Rung.Regional, s) || (PlayerHolds(f, (int)Rung.Executive, s) && waited >= days * 2 && f.Seat == (int)SeatFor((Concern)about)))
                {
                    news.NewTasks += Queue(f, TaskKind.Escalation, s, -1, about, day, about == (int)Concern.Morale ? OutingCost : 0) ? 1 : 0;
                    continue;
                }
                bool regional = RegionalOf(c, st.Region) != null && TechRandom.Mille(seed, Salt + 17 + about, day, s ^ salt) < AiResolve;
                bool top = waited >= days * 2 && !(f.Rung == (int)Rung.Executive && f.Seat == (int)SeatFor((Concern)about));
                if (regional || top)
                {
                    Settle(f, s, (Concern)about, seed, day, nameOf);
                }
            }
        }

        // The company's own manager settles what was sent up: a gap with a transfer from the fullest store near it, else a hire; low morale with an outing.
        private static void Settle(LadderFile f, int s, Concern about, int seed, int day, Func<int, string> nameOf)
        {
            CityCompany c = f.Company;
            Outlet st = c.Stores[s];
            if (about == Concern.Morale)
            {
                Outing(c, s);
                st.LowDays = 0;
                return;
            }
            Colleague spare = Spare(f, s, false);
            if (spare != null)
            {
                spare.Store = s;
                spare.Morale = TalentCatalogue.Clamp(spare.Morale - 3, 0, 100);
                st.GapDays = 0;
                return;
            }
            HireOne(f, s, seed, day, nameOf, c.Policy.Floor, c.Policy.WageTop);
        }

        private static void Outing(CityCompany c, int s)
        {
            c.Stores[s].Spent += OutingCost;
            for (int i = 0; i < c.People.Count; i++)
            {
                if (c.People[i].Store == s)
                {
                    c.People[i].Morale = TalentCatalogue.Clamp(c.People[i].Morale + 10, 0, 100);
                }
            }
        }

        /// <summary>
        /// A member of staff on the floor who can be spared for a store: at a store of the same district (or, for the player's unit,
        /// one of it) with more staff than its schedule wants, the least needed first. Null for none.
        /// </summary>
        public static Colleague Spare(LadderFile f, int to, bool playerUnit)
        {
            CityCompany c = f.Company;
            Colleague best = null;
            int bestOver = 0;
            for (int s = 0; s < c.Stores.Count; s++)
            {
                if (s == to || (playerUnit ? !InUnit(f, s) : c.Stores[s].Region != c.Stores[to].Region))
                {
                    continue;
                }
                int over = StaffAt(c, s) - Wanted(c.Stores[s]);
                if (over <= bestOver)
                {
                    continue;
                }
                for (int i = 0; i < c.People.Count; i++)
                {
                    Colleague p = c.People[i];
                    if (p.Store == s && p.Role == (int)Rung.Frontline)
                    {
                        best = p;
                        bestOver = over;
                        break;
                    }
                }
            }
            return best;
        }

        // A store whose manager left: the player's to appoint as its regional manager (a task); KeepRoles fills it elsewhere.
        private static void Appointing(LadderFile f, int s, int day, ref LadderNews news)
        {
            if (PlayerAppoints(f, s) && Holder(f.Company, s, (int)Rung.StoreManager) == null && StaffAt(f.Company, s) > 0)
            {
                news.NewTasks += Queue(f, TaskKind.Appoint, s, -1, 0, day, 0) ? 1 : 0;
            }
        }

        /// <summary>A task raised unless the same is waiting already or the list is full; true if raised. A task lapses after <see cref="TaskDays"/>, a shift's at the day's end.</summary>
        private static bool Queue(LadderFile f, TaskKind kind, int s, int person, int about, int day, int amount, int lapse = -1)
        {
            for (int i = 0; i < f.Tasks.Count; i++)
            {
                LadderTask t = f.Tasks[i];
                if (t.Kind == (int)kind && t.Store == s && t.Person == person && t.About == about)
                {
                    return false;
                }
            }
            if (f.Tasks.Count >= TasksMost)
            {
                return false;
            }
            f.Tasks.Add(new LadderTask { Id = f.NextTask++, Kind = (int)kind, Store = s, Person = person, About = about, Day = day, Lapse = lapse >= 0 ? lapse : day + TaskDays, Amount = amount });
            return true;
        }

        // Tasks past their day: left to whoever is above or below, and counted against the player.
        private static void Lapse(LadderFile f, int seed, int day, Func<int, string> nameOf, ref LadderNews news)
        {
            CityCompany c = f.Company;
            for (int i = f.Tasks.Count - 1; i >= 0; i--)
            {
                LadderTask t = f.Tasks[i];
                if (t.Lapse >= day)
                {
                    continue;
                }
                f.Tasks.RemoveAt(i);
                f.Lapsed++;
                news.Lapsed++;
                Colleague p = c.PersonOf(t.Person);
                switch ((TaskKind)t.Kind)
                {
                    case TaskKind.Demand:
                        if (p != null && t.Store >= 0 && t.Store < c.Stores.Count)
                        {
                            Decide(c, t.Store, p, false, day);   // nobody answered: as good as a no
                        }
                        break;
                    case TaskKind.Escalation:
                        if (t.Store >= 0 && t.Store < c.Stores.Count)
                        {
                            Settle(f, t.Store, (Concern)t.About, seed, day, nameOf);   // the executive above settles it
                        }
                        break;
                    case TaskKind.Appoint:
                        if (t.Store >= 0 && t.Store < c.Stores.Count)
                        {
                            Colleague best = MostSkilled(c, t.Store);
                            if (best != null && Holder(c, t.Store, (int)Rung.StoreManager) == null)
                            {
                                best.Role = (int)Rung.StoreManager;   // head office appoints
                            }
                        }
                        break;
                }
            }
        }

        /// <summary>A store that is no longer the company's let go, with its staff and its tasks; the places of the stores after it move up.</summary>
        public static void DropStore(LadderFile f, int s)
        {
            CityCompany c = f.Company;
            if (s <= 0 || s >= c.Stores.Count)
            {
                return;   // (the player's own store is kept, as the job is)
            }
            c.Stores.RemoveAt(s);
            for (int i = c.People.Count - 1; i >= 0; i--)
            {
                Colleague p = c.People[i];
                if (p.Store == s)
                {
                    c.People.RemoveAt(i);
                }
                else if (p.Store > s)
                {
                    p.Store--;
                }
            }
            for (int i = f.Tasks.Count - 1; i >= 0; i--)
            {
                LadderTask t = f.Tasks[i];
                if (t.Store == s)
                {
                    f.Tasks.RemoveAt(i);
                }
                else if (t.Store > s)
                {
                    t.Store--;
                }
            }
        }

        /// <summary>
        /// The game's own staff at a store against the ids the game has there now: one it no longer has has left (a gap); the
        /// people the company took on since are the model's own and stay. Returns how many left.
        /// </summary>
        public static int KeepStaff(LadderFile f, int s, HashSet<string> ids)
        {
            CityCompany c = f.Company;
            int left = 0;
            if (ids == null || ids.Count == 0)
            {
                return 0;   // (the game lists nobody there: nothing to tell from it)
            }
            for (int i = c.People.Count - 1; i >= 0; i--)
            {
                Colleague p = c.People[i];
                if (p.Store == s && p.GameId.Length > 0 && !ids.Contains(p.GameId))
                {
                    DropTasksOf(f, p.Id);
                    c.People.RemoveAt(i);
                    left++;
                }
            }
            return left;
        }
    }
}
