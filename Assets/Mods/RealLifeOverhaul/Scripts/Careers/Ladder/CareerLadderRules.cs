using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

namespace RealLifeOverhaul.Careers
{
    /// <summary>One member of the game's own staff at a store, as the save holds them (AiBusinessEmployeeData).</summary>
    public struct StaffSeed
    {
        public string GameId, Name;
        public int Skill;
    }

    /// <summary>One store of the company as the save holds it: the game's own business at an address and its staff.</summary>
    public sealed class StoreSeed
    {
        public string Street = string.Empty, Name = string.Empty, District = string.Empty;
        public int Number;
        public readonly List<StaffSeed> Staff = new List<StaffSeed>(CareerLadderRules.StaffMost);
    }

    /// <summary>
    /// The management ladder at one of the city's companies, game-free: the company seeded from the save, who is responsible for
    /// what, the player's performance score, promotion, pay and the player's own requests. The day's running of the stores is in
    /// CareerLadderRules.Day, the tasks and tools in CareerLadderRules.Tasks. Every roll is the save's seed hashed with what it is about
    /// (TechRandom), so the same save gives the same company on every machine; nothing here allocates per frame.
    /// </summary>
    public static partial class CareerLadderRules
    {
        public const int StoresMost = 12;        // stores of a company modelled: the player's and the eleven nearest by district and address
        public const int StaffMost = 12;         // staff at one store
        public const int TasksMost = 16;         // tasks waiting at once
        public const int ScoreStart = 60;        // a new job's performance score
        public const int TaskDays = 3;           // days a task waits for a decision before it lapses
        public const int MoraleBase = 60;        // what morale settles to with nothing either way
        public const int LowMorale = 40;         // a store's average under this is low
        public const int QuitBelow = 25;         // a person under this morale may leave...
        public const int QuitChance = 150;       // ...per mille a day
        public const int DemandChance = 35;      // per mille a day a person with nothing waiting asks for something
        public const int AiFill = 450;           // per mille a day a store manager of the company's fills a gap
        public const int AiAnswer = 400;         // per mille a day a manager of the company's answers a demand
        public const int AiResolve = 500;        // per mille a day a regional manager of the company's settles what is sent up
        public const int EquipmentCost = 150;    // whole money at the start's prices: a demand for equipment
        public const int InsuranceCost = 200;    // ...for insurance
        public const int OutingCost = 300;       // ...a staff outing for a store whose morale is low
        public const int CoverHours = 4;         // hours of overtime to cover a gap on a shift
        public const int RaiseGapDays = 14;      // days between two raises of one person
        public const int WageLeast = 1200;       // cents an hour at the start's prices a member of staff of no skill is paid (the game's own staff are paid 12 to 18)
        public const int WagePerSkill = 12;      // ...and so many cents more a point of skill
        public const int ReviewBonusMost = 8;    // points a review's score moves by the performance score, either way
        public const int AskLeast = 100, AskMost = 900;   // per mille: the boss grants a request at least, at most
        private const int Salt = 8800;           // the dice's own numbers (TechRandom), apart from the careers' 8100 to 8700

        /// <summary>True for a job the ladder runs at: one of the city's businesses or a rival's, worked at its building.</summary>
        public static bool Holds(Job j)
        {
            Profession p = j != null ? j.Def : null;
            return p != null && p.Place == Workplace.Business && (j.Post.Kind == (int)EmployerKind.City || j.Post.Kind == (int)EmployerKind.Rival)
                   && !string.IsNullOrEmpty(j.Post.Street);
        }

        /// <summary>The rung a job starts on: a manager's post at a store starts in charge of a shift, or of the store from its third rank.</summary>
        public static int StartRung(Job j)
        {
            if (j == null || j.Post.Prof != ProCatalogue.Manager)
            {
                return (int)Rung.Frontline;
            }
            return j.Post.Rank >= 2 ? (int)Rung.StoreManager : (int)Rung.Supervisor;
        }

        /// <summary>What a member of staff of a skill is paid, cents an hour at the start's prices.</summary>
        public static int WageFor(int skill) => WageLeast + TalentCatalogue.Clamp(skill, 0, 100) * WagePerSkill;

        // ---------------------------------------------------------------- seeding

        /// <summary>
        /// The company as the save holds it now: its stores (the player's first), the game's own staff at each, a store manager
        /// and a supervisor from the most skilled of them, a regional manager for each district and the three executives, and the
        /// player at the rung their job starts on. The people the company is given beyond the game's staff are named by
        /// <paramref name="nameOf"/> from a number of the seed's, the same on every machine.
        /// </summary>
        public static void Seed(LadderFile f, Job j, string key, string name, string rival, List<StoreSeed> stores, int seed, Func<int, string> nameOf, int day)
        {
            f.Hired = j.Hired;
            f.Street = j.Post.Street;
            f.Number = j.Post.Number;
            f.Employer = j.Post.Employer;
            f.Rung = StartRung(j);
            f.Seat = (int)ExecSeat.None;
            f.RungDay = day;
            f.Score = ScoreStart * 10;
            f.Recommended = false;
            f.Ask = (int)StaffWant.None;
            f.Prefer = 0;
            f.Equipped = f.Insured = false;
            f.Handled = f.Lapsed = 0;
            f.LastDay = day;
            f.Week = day / 7;
            f.Tasks.Clear();
            CityCompany c = f.Company;
            c.Clear();
            c.Key = key ?? string.Empty;
            c.Name = name ?? string.Empty;
            c.Rival = rival ?? string.Empty;
            int salt = TechRandom.Of(c.Key);
            for (int s = 0; s < stores.Count && c.Stores.Count < StoresMost; s++)
            {
                StoreSeed from = stores[s];
                int region = c.Districts.IndexOf(from.District ?? string.Empty);
                if (region < 0)
                {
                    region = c.Districts.Count;
                    c.Districts.Add(from.District ?? string.Empty);
                }
                var st = new Outlet { Street = from.Street ?? string.Empty, Number = from.Number, Name = from.Name ?? string.Empty, Region = region };
                int at = c.Stores.Count;
                c.Stores.Add(st);
                for (int k = 0; k < from.Staff.Count && k < StaffMost; k++)
                {
                    StaffSeed m = from.Staff[k];
                    AddPerson(c, at, m.GameId, m.Name, m.Skill, WageFor(m.Skill), TechRandom.Range(seed, Salt, salt, c.NextPerson, 55, 75));
                }
                while (StaffAt(c, at) < 2)
                {
                    // (a store the save holds no staff for: two people of the company's own, so it can be run)
                    int n = c.NextPerson;
                    int skill = TechRandom.Range(seed, Salt + 1, salt, n, 30, 70);
                    AddPerson(c, at, string.Empty, nameOf != null ? nameOf((int)TechRandom.Hash(seed, Salt + 2, salt, n)) : "?", skill, WageFor(skill), 65);
                }
                st.Needed = Math.Max(1, StaffAt(c, at));
                st.Results = MoraleBase;
            }
            KeepHeads(f, seed, nameOf);
            Head(c, seed, salt, nameOf, (int)Rung.Executive, -1, ExecSeat.Coo);
            Head(c, seed, salt, nameOf, (int)Rung.Executive, -1, ExecSeat.Chro);
            Head(c, seed, salt, nameOf, (int)Rung.Executive, -1, ExecSeat.Cfo);
            for (int s = 0; s < c.Stores.Count; s++)
            {
                KeepRoles(f, s);
            }
            for (int r = (int)Rung.Supervisor; r <= f.Rung; r++)
            {
                TakeRole(f, r);
            }
            Figures(f, day);
        }

        // A regional manager for each district that has none and is not the player's.
        private static void KeepHeads(LadderFile f, int seed, Func<int, string> nameOf)
        {
            CityCompany c = f.Company;
            int salt = TechRandom.Of(c.Key);
            for (int r = 0; r < c.Districts.Count; r++)
            {
                bool mine = f.Rung == (int)Rung.Regional && (RegionScope(c) < 0 || c.Stores[0].Region == r);
                if (!mine && RegionalOf(c, r) == null)
                {
                    Head(c, seed, salt, nameOf, (int)Rung.Regional, r, ExecSeat.None);
                }
            }
        }

        // A manager above the stores: a person of the company's own at head office.
        private static void Head(CityCompany c, int seed, int salt, Func<int, string> nameOf, int role, int region, ExecSeat seat)
        {
            int n = c.NextPerson;
            int skill = TechRandom.Range(seed, Salt + 3, salt, n, 65, 92);
            Colleague p = AddPerson(c, -1, string.Empty, nameOf != null ? nameOf((int)TechRandom.Hash(seed, Salt + 4, salt, n)) : "?", skill, WageFor(skill) * 3, 70);
            p.Role = role;
            p.Region = region;
            p.Seat = (int)seat;
        }

        private static Colleague AddPerson(CityCompany c, int store, string gameId, string name, int skill, int wage, int morale)
        {
            var p = new Colleague
            {
                Id = c.NextPerson++, GameId = gameId ?? string.Empty, Name = name ?? string.Empty, Store = store, Skill = TalentCatalogue.Clamp(skill, 0, 100),
                    Wage = Math.Max(0, wage),
                Morale = TalentCatalogue.Clamp(morale, 0, 100)
            };
            c.People.Add(p);
            return p;
        }

        /// <summary>The people working at a store, its managers included.</summary>
        public static int StaffAt(CityCompany c, int store)
        {
            int n = 0;
            for (int i = 0; i < c.People.Count; i++)
            {
                n += c.People[i].Store == store && store >= 0 ? 1 : 0;
            }
            return n;
        }

        /// <summary>The person of the company holding a store's role (a supervisor, a store manager), or null.</summary>
        public static Colleague Holder(CityCompany c, int store, int role)
        {
            for (int i = 0; i < c.People.Count; i++)
            {
                Colleague p = c.People[i];
                if (p.Store == store && p.Role == role)
                {
                    return p;
                }
            }
            return null;
        }

        /// <summary>The company's regional manager of a district, or null.</summary>
        public static Colleague RegionalOf(CityCompany c, int region)
        {
            for (int i = 0; i < c.People.Count; i++)
            {
                Colleague p = c.People[i];
                if (p.Role == (int)Rung.Regional && (p.Region == region || p.Region < 0))
                {
                    return p;
                }
            }
            return null;
        }

        /// <summary>The company's executive in a seat, or null.</summary>
        public static Colleague ExecutiveOf(CityCompany c, ExecSeat seat)
        {
            for (int i = 0; i < c.People.Count; i++)
            {
                Colleague p = c.People[i];
                if (p.Role == (int)Rung.Executive && p.Seat == (int)seat)
                {
                    return p;
                }
            }
            return null;
        }

        // A store's manager and supervisor filled from its most skilled staff, where the role is nobody's and is not the player's to fill.
        private static void KeepRoles(LadderFile f, int s)
        {
            CityCompany c = f.Company;
            if (!PlayerHolds(f, (int)Rung.StoreManager, s) && Holder(c, s, (int)Rung.StoreManager) == null && !PlayerAppoints(f, s))
            {
                Colleague best = MostSkilled(c, s);
                if (best != null)
                {
                    best.Role = (int)Rung.StoreManager;
                }
            }
            if (!PlayerHolds(f, (int)Rung.Supervisor, s) && Holder(c, s, (int)Rung.Supervisor) == null && StaffAt(c, s) >= 3)
            {
                Colleague next = MostSkilled(c, s);
                if (next != null)
                {
                    next.Role = (int)Rung.Supervisor;
                }
            }
        }

        // The most skilled of a store's staff on the floor, the first taken on of equals: the one a store's role is filled from.
        private static Colleague MostSkilled(CityCompany c, int s)
        {
            Colleague best = null;
            for (int i = 0; i < c.People.Count; i++)
            {
                Colleague p = c.People[i];
                if (p.Store == s && p.Role == (int)Rung.Frontline && (best == null || p.Skill > best.Skill || (p.Skill == best.Skill && p.Id < best.Id)))
                {
                    best = p;
                }
            }
            return best;
        }

        /// <summary>
        /// The player takes a rung's role in their unit: whoever of the company held it steps aside. A supervisor goes back to the
        /// floor; a store manager moves to a store of the company without one, or leaves (a post for the player to fill); a regional
        /// manager and an executive move on.
        /// </summary>
        private static void TakeRole(LadderFile f, int rung)
        {
            CityCompany c = f.Company;
            switch ((Rung)rung)
            {
                case Rung.Supervisor:
                    {
                        Colleague p = Holder(c, 0, (int)Rung.Supervisor);
                        if (p != null)
                        {
                            p.Role = (int)Rung.Frontline;
                        }
                        break;
                    }
                case Rung.StoreManager:
                    {
                        Colleague p = Holder(c, 0, (int)Rung.StoreManager);
                        if (p == null)
                        {
                            break;
                        }
                        int to = -1;
                        for (int s = 1; s < c.Stores.Count && to < 0; s++)
                        {
                            to = Holder(c, s, (int)Rung.StoreManager) == null ? s : -1;
                        }
                        if (to > 0)
                        {
                            p.Store = to;
                        }
                        else
                        {
                            c.People.Remove(p);
                        }
                        break;
                    }
                case Rung.Regional:
                    {
                        for (int i = c.People.Count - 1; i >= 0; i--)
                        {
                            Colleague p = c.People[i];
                            if (p.Role == (int)Rung.Regional && (RegionScope(c) < 0 || p.Region == c.Stores[0].Region))
                            {
                                c.People.RemoveAt(i);
                            }
                        }
                        break;
                    }
                case Rung.Executive:
                    {
                        Colleague p = ExecutiveOf(c, (ExecSeat)f.Seat);
                        if (p != null)
                        {
                            c.People.Remove(p);
                        }
                        break;
                    }
            }
        }

        // ---------------------------------------------------------------- who is responsible for what

        /// <summary>The district the player's region is, or -1 when their district has only their store: then the region is the whole company.</summary>
        public static int RegionScope(CityCompany c)
        {
            if (c.Stores.Count == 0)
            {
                return -1;
            }
            int region = c.Stores[0].Region, n = 0;
            for (int s = 0; s < c.Stores.Count; s++)
            {
                n += c.Stores[s].Region == region ? 1 : 0;
            }
            return n >= 2 ? region : -1;
        }

        /// <summary>True if a store is in what the player runs at their rung: their own store, their region, or the whole company.</summary>
        public static bool InUnit(LadderFile f, int s)
        {
            CityCompany c = f.Company;
            if (s < 0 || s >= c.Stores.Count)
            {
                return false;
            }
            if (f.Rung <= (int)Rung.StoreManager)
            {
                return s == 0;
            }
            if (f.Rung == (int)Rung.Regional)
            {
                int scope = RegionScope(c);
                return scope < 0 || c.Stores[s].Region == scope;
            }
            return true;
        }

        /// <summary>
        /// True if a duty of a rung at a store is the player's: the shift's at their store as its supervisor, or as its manager
        /// with no supervisor under them; the store's as its manager; what is sent up from their region as its manager; what is
        /// sent up to their seat as an executive.
        /// </summary>
        public static bool PlayerHolds(LadderFile f, int duty, int s)
        {
            switch ((Rung)duty)
            {
                case Rung.Supervisor:
                    return s == 0 && (f.Rung == (int)Rung.Supervisor || (f.Rung == (int)Rung.StoreManager && Holder(f.Company, 0, (int)Rung.Supervisor) == null));
                case Rung.StoreManager:
                    return s == 0 && f.Rung == (int)Rung.StoreManager;
                case Rung.Regional:
                    return f.Rung == (int)Rung.Regional && InUnit(f, s);
                case Rung.Executive:
                    return f.Rung == (int)Rung.Executive;
                default:
                    return false;
            }
        }

        // True if a store's missing manager is the player's to appoint (as its regional manager).
        private static bool PlayerAppoints(LadderFile f, int s) => f.Rung == (int)Rung.Regional && InUnit(f, s) && s != 0;

        /// <summary>The executive seat a concern sent up goes to: a gap to operations, morale to people, money to finance.</summary>
        public static ExecSeat SeatFor(Concern about) => about == Concern.Morale ? ExecSeat.Chro : about == Concern.Budget ? ExecSeat.Cfo : ExecSeat.Coo;

        /// <summary>The person the player answers to: one rung up in their unit, or null for the company's owner.</summary>
        public static Colleague Superior(LadderFile f)
        {
            CityCompany c = f.Company;
            switch ((Rung)f.Rung)
            {
                case Rung.Frontline:
                    return Holder(c, 0, (int)Rung.Supervisor) ?? Holder(c, 0, (int)Rung.StoreManager) ?? (c.Stores.Count > 0 ? RegionalOf(c, c.Stores[0].Region) : null);
                case Rung.Supervisor:
                    return Holder(c, 0, (int)Rung.StoreManager) ?? (c.Stores.Count > 0 ? RegionalOf(c, c.Stores[0].Region) : null);
                case Rung.StoreManager:
                    return (c.Stores.Count > 0 ? RegionalOf(c, c.Stores[0].Region) : null) ?? ExecutiveOf(c, ExecSeat.Coo);
                case Rung.Regional:
                    return ExecutiveOf(c, ExecSeat.Coo);
                default:
                    return null;
            }
        }

        // ---------------------------------------------------------------- the stores' figures and the performance score

        /// <summary>The staff a store's schedule wants: what its work needs, one fewer on a lean schedule, one more on a full one.</summary>
        public static int Wanted(Outlet st) => Math.Max(1, st.Needed + st.Rota - 1);

        /// <summary>A store's staffing as a share of what its schedule wants, 0 to 100 (a gap covered on the day counts as filled).</summary>
        public static int Staffing(CityCompany c, int s, int day)
        {
            Outlet st = c.Stores[s];
            int wanted = Wanted(st), staff = StaffAt(c, s);
            if (st.Covered == day && staff < wanted)
            {
                staff++;
            }
            return Math.Min(100, staff * 100 / wanted);
        }

        /// <summary>A store's staff's average morale, 0 to 100 (the base for a store nobody works at).</summary>
        public static int MoraleAt(CityCompany c, int s)
        {
            int sum = 0, n = 0;
            for (int i = 0; i < c.People.Count; i++)
            {
                if (c.People[i].Store == s)
                {
                    sum += c.People[i].Morale;
                    n++;
                }
            }
            return n > 0 ? sum / n : MoraleBase;
        }

        /// <summary>The player's unit's staffing and morale, each the average of its stores'.</summary>
        public static void UnitFigures(LadderFile f, int day, out int staffing, out int morale, out int stores)
        {
            CityCompany c = f.Company;
            int st = 0, mo = 0;
            stores = 0;
            for (int s = 0; s < c.Stores.Count; s++)
            {
                if (!InUnit(f, s))
                {
                    continue;
                }
                st += Staffing(c, s, day);
                mo += MoraleAt(c, s);
                stores++;
            }
            staffing = stores > 0 ? st / stores : 100;
            morale = stores > 0 ? mo / stores : MoraleBase;
        }

        /// <summary>The share of the tasks since the last review that were decided, 0 to 100 (100 with none).</summary>
        public static int Handling(LadderFile f) => f.Handled + f.Lapsed > 0 ? f.Handled * 100 / (f.Handled + f.Lapsed) : 100;

        /// <summary>
        /// A day's performance, 0 to 100. On the floor: turning up (the job's attendance, thousandths) and the store's results. A
        /// manager: their unit's staffing and morale, and the tasks decided rather than left.
        /// </summary>
        public static int Today(LadderFile f, int attendance, int day)
        {
            UnitFigures(f, day, out int staffing, out int morale, out int _);
            if (f.Rung == (int)Rung.Frontline)
            {
                int results = f.Company.Stores.Count > 0 ? f.Company.Stores[0].Results : MoraleBase;
                return TalentCatalogue.Clamp((TalentCatalogue.Clamp(attendance, 0, 1000) / 10 * 60 + results * 40) / 100, 0, 100);
            }
            return TalentCatalogue.Clamp((staffing * 40 + morale * 35 + Handling(f) * 25) / 100, 0, 100);
        }

        /// <summary>The day's performance taken into the score: a week's running average, kept in tenths.</summary>
        public static void Mark(LadderFile f, int attendance, int day)
        {
            f.Score = TalentCatalogue.Clamp((f.Score * 6 + Today(f, attendance, day) * 10) / 7, 0, 1000);
        }

        /// <summary>The performance score, 0 to 100.</summary>
        public static int ScoreOf(LadderFile f) => f.Score / 10;

        /// <summary>What the performance score adds to a review's score, at most <see cref="ReviewBonusMost"/> either way; 60 adds nothing.</summary>
        public static int ReviewBonus(LadderFile f) => f == null ? 0 : TalentCatalogue.Clamp((ScoreOf(f) - ScoreStart) / 3, -ReviewBonusMost, ReviewBonusMost);

        // ---------------------------------------------------------------- promotion and pay

        /// <summary>The seat an executive's promotion is to: the one aimed for, else the best fit of the player's skills.</summary>
        public static ExecSeat SeatAimed(LadderFile f, Func<int, int> pointsOf) => f.Aim != (int)ExecSeat.None ? (ExecSeat)f.Aim : LadderRanks.BestSeat(pointsOf);

        /// <summary>Game days at the rung before the next: its days at the careers' pace, a third fewer with a recommendation where it is not required.</summary>
        public static int DaysNeeded(LadderFile f, int year)
        {
            if (f.Rung >= LadderRanks.CareerTop)
            {
                return 0;
            }
            RungDef next = LadderRanks.Of(f.Rung + 1);
            int days = LifeRules.Paced(next.Days, year);
            return f.Recommended && !next.Recommend ? days * 2 / 3 : days;
        }

        /// <summary>The points the next rung asks for, against the player's.</summary>
        public static void SkillsNeeded(LadderFile f, Func<int, int> pointsOf, out int have, out int need)
        {
            int next = Math.Min(f.Rung + 1, LadderRanks.CareerTop);
            have = LadderRanks.Points(next, SeatAimed(f, pointsOf), pointsOf);
            need = LadderRanks.Of(next).Need;
        }

        /// <summary>Why the player cannot be promoted to the next rung at a review now, as a locale key; null if they can.</summary>
        public static string WhyNot(LadderFile f, int day, int year, Func<int, int> pointsOf)
        {
            if (f.Rung >= LadderRanks.CareerTop)
            {
                return "reallife_ladder_why_top";
            }
            RungDef next = LadderRanks.Of(f.Rung + 1);
            if (f.Company.Stores.Count < next.Stores)
            {
                return "reallife_ladder_why_stores";
            }
            if (!f.Aiming)
            {
                return "reallife_ladder_why_not_aiming";
            }
            if (day - f.RungDay < DaysNeeded(f, year))
            {
                return "reallife_ladder_why_days";
            }
            if (ScoreOf(f) < next.Score)
            {
                return "reallife_ladder_why_score";
            }
            SkillsNeeded(f, pointsOf, out int have, out int need);
            if (have < need)
            {
                return "reallife_ladder_why_skills";
            }
            return next.Recommend && !f.Recommended ? "reallife_ladder_why_recommend" : null;
        }

        /// <summary>True if the superior recommends the player at a review: a score five over the next rung's, and nothing left to lapse.</summary>
        public static bool Recommends(LadderFile f)
        {
            return f.Rung < LadderRanks.CareerTop && ScoreOf(f) >= LadderRanks.Of(f.Rung + 1).Score + 5 && f.Lapsed == 0;
        }

        /// <summary>A review: the superior's recommendation, and the count of tasks decided starts again.</summary>
        public static void Reviewed(LadderFile f)
        {
            f.Recommended |= Recommends(f);
            f.Handled = f.Lapsed = 0;
        }

        /// <summary>
        /// The player is promoted a rung: its role is theirs and whoever held it steps aside, every tool of it is theirs at once, the
        /// tasks of the old rung go to whoever holds it now, and the recommendation is used.
        /// </summary>
        public static void Promote(LadderFile f, int day, ExecSeat seat, int seed, Func<int, string> nameOf)
        {
            if (f.Rung >= LadderRanks.CareerTop)
            {
                return;
            }
            f.Rung++;
            f.Seat = f.Rung == (int)Rung.Executive ? (int)(seat == ExecSeat.None ? ExecSeat.Coo : seat) : (int)ExecSeat.None;
            f.RungDay = day;
            f.Recommended = false;
            f.Handled = f.Lapsed = 0;
            f.Tasks.Clear();
            TakeRole(f, f.Rung);
            for (int s = 0; s < f.Company.Stores.Count; s++)
            {
                KeepRoles(f, s);
            }
            KeepHeads(f, seed, nameOf);   // the region the player leaves for a seat gets a manager of its own
        }

        /// <summary>What a rung pays: a promotion's rise on the pay now, or the management profession's own rate for it at this kind of employer if more.</summary>
        public static int PayFor(int rung, EmployerKind kind, int pay)
        {
            RungDef r = LadderRanks.Of(rung);
            if (r.PayRank < 0)
            {
                return pay;
            }
            int up = (int)((long)pay * (10000 + LifeRules.PromoteBp) / 10000);
            int own = ProRules.PostPay(ProCatalogue.Find(ProCatalogue.Manager), r.PayRank, kind, 1000);
            return Math.Max(up, own);
        }

        // ---------------------------------------------------------------- the player's own requests

        /// <summary>The cost of a request granted, whole money at the start's prices.</summary>
        public static int Cost(StaffWant w) => w == StaffWant.Equipment ? EquipmentCost : w == StaffWant.Insurance ? InsuranceCost : 0;

        /// <summary>The store's budget left this week, whole money at the start's prices.</summary>
        public static int BudgetLeft(CityCompany c, int s) => s >= 0 && s < c.Stores.Count ? Math.Max(0, c.Policy.Budget - c.Stores[s].Spent) : 0;

        /// <summary>Why the player may not ask for this now, as a locale key; null if they may.</summary>
        public static string CanAsk(LadderFile f, StaffWant w)
        {
            if (f.Ask != (int)StaffWant.None)
            {
                return "reallife_ladder_why_asked";
            }
            if (w == StaffWant.Raise || w == StaffWant.None)
            {
                return "reallife_ladder_why_ask_kind";
            }
            if ((w == StaffWant.Equipment && f.Equipped) || (w == StaffWant.Insurance && f.Insured))
            {
                return "reallife_ladder_why_have";
            }
            return null;
        }

        /// <summary>The player asks their superior for a shift (a way: -1 earlier, 1 later), equipment or insurance; answered the next day.</summary>
        public static string Ask(LadderFile f, StaffWant w, int way, int day)
        {
            string why = CanAsk(f, w);
            if (why != null)
            {
                return why;
            }
            f.Ask = (int)w;
            f.AskDay = day;
            f.AskWay = way < 0 ? -1 : 1;
            return null;
        }

        /// <summary>The chance (per mille) the superior grants the player's request, by the performance score.</summary>
        public static int AskChance(LadderFile f) => TalentCatalogue.Clamp(300 + (ScoreOf(f) - 50) * 15, AskLeast, AskMost);

        /// <summary>
        /// The superior answers the player's request the day after it was made: granted by the chance its score gives, and only
        /// within the store's budget. True if answered (granted says how). A shift moves the job's start an hour (at most two either
        /// way, within the opening hours: CareersDirector.Hours), equipment makes the work teach more, insurance adds to the
        /// employer's pension payments.
        /// </summary>
        public static bool AnswerAsk(LadderFile f, int seed, int day, out bool granted)
        {
            granted = false;
            StaffWant w = (StaffWant)f.Ask;
            if (w == StaffWant.None || day <= f.AskDay)
            {
                return false;
            }
            int cost = Cost(w);
            bool affordable = cost <= BudgetLeft(f.Company, 0);
            granted = affordable && TechRandom.Mille(seed, Salt + 9, f.AskDay, f.Ask) < AskChance(f);
            if (granted)
            {
                if (cost > 0 && f.Company.Stores.Count > 0)
                {
                    f.Company.Stores[0].Spent += cost;
                }
                switch (w)
                {
                    case StaffWant.Shift: f.Prefer = TalentCatalogue.Clamp(f.Prefer + f.AskWay, -2, 2); break;
                    case StaffWant.Equipment: f.Equipped = true; break;
                    case StaffWant.Insurance: f.Insured = true; break;
                }
            }
            f.Ask = (int)StaffWant.None;
            return true;
        }

        public const int EquippedLearn = 1250;   // thousandths: what the work teaches with the equipment asked for
        public const int InsuredPensionBp = 200; // basis points the employer pays into the pension more with insurance
    }
}
