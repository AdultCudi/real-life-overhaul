using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Tech;

// The management ladder at a city company (Scripts/Careers/Ladder, CareersDirector.Ladder): the ranks and what each may do,
// the company seeded from the save the same on every machine, who is responsible for what, a day of the stores, every task
// decided by the rank that may, the powers refused to the rank that may not, the score, the promotion with its tools, the
// player's own requests, pay from the professions' figures, the save, the sources that wire it in, and the texts.
internal static partial class Program
{
    private static void Ladder()
    {
        LadderRanksChecks();
        LadderPayChecks();
        LadderSeedChecks();
        LadderDutyChecks();
        LadderDayChecks();
        LadderTaskChecks();
        LadderPowerChecks();
        LadderPromotionChecks();
        LadderRequestChecks();
        LadderSaveChecks();
        LadderKeepChecks();
        LadderTiming();
        LadderSources();
        LadderTexts();
    }

    private const int LadderSeed = 4242;

    private static string LadderName(int n) => "Person " + (n & 0xFFFF);

    // A job on the floor of a store of a city chain, and that chain's stores: the job's own and two more in its district, one in another.
    private static Job LadderJob(int prof = ProCatalogue.BarStaff, int rank = 0)
    {
        var j = new Job { Hired = 10, RankDay = 10, ReviewDay = 10, Pay = 7000, SettledDay = 9 };
        j.Post.Prof = prof;
        j.Post.Rank = rank;
        j.Post.Kind = (int)EmployerKind.City;
        j.Post.Employer = "Corner Coffee";
        j.Post.Street = "Main Street";
        j.Post.Number = 1;
        return j;
    }

    private static List<StoreSeed> LadderStores(int count = 4, int staff = 4)
    {
        var list = new List<StoreSeed>();
        for (int s = 0; s < count; s++)
        {
            var st = new StoreSeed { Street = s == 0 ? "Main Street" : "Street " + s, Number = s == 0 ? 1 : 10 + s, Name = "Corner Coffee", District = s < 3 ? "midtown" : "harbour" };
            for (int k = 0; k < staff; k++)
            {
                st.Staff.Add(new StaffSeed { GameId = "g" + s + "_" + k, Name = "Staff " + s + "." + k, Skill = 30 + k * 10 + s });
            }
            list.Add(st);
        }
        return list;
    }

    private static LadderFile LadderMade(int rung = 0, int stores = 4, int staff = 4)
    {
        Job j = LadderJob();
        var f = new LadderFile();
        CareerLadderRules.Seed(f, j, "n:Corner Coffee|ba:businesstype_coffeeshop", "Corner Coffee", "", LadderStores(stores, staff), LadderSeed, LadderName, 20);
        for (int r = 0; r < rung; r++)
        {
            CareerLadderRules.Promote(f, 20, ExecSeat.Coo, LadderSeed, LadderName);
        }
        return f;
    }

    // ---------------------------------------------------------------- the ranks

    private static void LadderRanksChecks()
    {
        Check(LadderRanks.All.Length == 6 && LadderRanks.CareerTop == (int)Rung.Executive && LadderRanks.Of(LadderRanks.CareerTop).Level == Rung.Executive,
            "ladder: five rungs of a career, frontline to executive, and the owner apart (the suggestion's L0 to L4)");
        bool grows = true, asks = true;
        for (int r = 1; r <= LadderRanks.CareerTop; r++)
        {
            RungDef a = LadderRanks.Of(r - 1), b = LadderRanks.Of(r);
            grows &= (b.Powers & a.Powers) == a.Powers && b.Powers != a.Powers;
            asks &= b.Days >= a.Days && b.Score >= a.Score && b.Need >= a.Need && b.PayRank > a.PayRank && b.Days > 0 && b.Score > 0;
        }
        Check(grows, "ladder: every rung keeps every power of the rung below and adds its own, so a promotion brings its new tools and loses none");
        Check(asks, "ladder: each rung asks at least the time, score and skills of the one below and starts from a higher rank of the management profession's pay");
        Check(LadderRanks.Of((int)Rung.Executive).Recommend && !LadderRanks.Of((int)Rung.Supervisor).Recommend && LadderRanks.Of((int)Rung.Regional).Stores >= 2
              && LadderRanks.Of((int)Rung.StoreManager).Stores == 1, "ladder: an executive needs a recommendation, the rest take one as a help; a regional manager needs two stores");
        Check(LadderRanks.Has(0, RungPower.Requests) && !LadderRanks.Has(0, RungPower.Cover) && LadderRanks.Has(1, RungPower.Cover) && !LadderRanks.Has(1, RungPower.Hire)
              && LadderRanks.Has(2, RungPower.Hire) && LadderRanks.Has(2, RungPower.Raises) && !LadderRanks.Has(2, RungPower.Transfers) && LadderRanks.Has(3, RungPower.Transfers)
              && LadderRanks.Has(3, RungPower.Appoint) && !LadderRanks.Has(3, RungPower.Policy) && LadderRanks.Has(4, RungPower.Policy),
            "ladder: the suggestion's permissions by level: requests on the floor, cover from a supervisor, hiring and raises from a store manager, transfers and appointments regional, policy executive");
        int seen = 0;
        for (int i = 0; i < LadderRanks.Listed.Length; i++)
        {
            seen |= (int)LadderRanks.Listed[i];
        }
        Check(seen == (int)LadderRanks.Of((int)Rung.Owner).Powers, "ladder: the Career app lists every power there is");
        Check(LadderRanks.TitleKey(4, ExecSeat.Chro) == "reallife_ladder_rung_executive_chro" && LadderRanks.TitleKey(2, ExecSeat.None) == "reallife_ladder_rung_store_manager",
            "ladder: a rung's title key, an executive's by the seat");
        Func<int, int> money = s => s == TalentCatalogue.Accounting || s == TalentCatalogue.Analysis ? 60 : 10;
        Check(LadderRanks.BestSeat(money) == ExecSeat.Cfo && LadderRanks.BestSeat(s => 0) == ExecSeat.Coo, "ladder: the seat that fits the player's skills best, operations on a tie");
    }

    // ---------------------------------------------------------------- pay

    private static void LadderPayChecks()
    {
        Profession manager = ProCatalogue.Find(ProCatalogue.Manager);
        int pay = 7000, last = pay;
        bool up = true;
        for (int r = 1; r <= LadderRanks.CareerTop; r++)
        {
            int next = CareerLadderRules.PayFor(r, EmployerKind.City, last);
            int own = ProRules.PostPay(manager, LadderRanks.Of(r).PayRank, EmployerKind.City, 1000);
            up &= next >= own && next >= last * (10000 + LifeRules.PromoteBp) / 10000 && next > last;
            last = next;
        }
        Check(up, "ladder: each promotion pays at least the management profession's rate for the rung (ProCatalogue) and a promotion's rise on the pay before");
        Check(CareerLadderRules.PayFor(0, EmployerKind.City, 7000) == 7000, "ladder: the floor is paid the job's own rate");
        Check(CareerLadderRules.PayFor(1, EmployerKind.City, 90000) == 90000 * (10000 + LifeRules.PromoteBp) / 10000, "ladder: a high rate already earned still rises by a promotion's share, never falls to the catalogue's");
    }

    // ---------------------------------------------------------------- seeding

    private static void LadderSeedChecks()
    {
        LadderFile f = LadderMade();
        CityCompany c = f.Company;
        Check(c.Stores.Count == 4 && c.Stores[0].Street == "Main Street" && c.Districts.Count == 2 && c.Stores[3].Region == 1, "ladder: the job's store first, every store kept with its district");
        bool managed = true;
        for (int s = 0; s < c.Stores.Count; s++)
        {
            managed &= CareerLadderRules.Holder(c, s, (int)Rung.StoreManager) != null && CareerLadderRules.Holder(c, s, (int)Rung.Supervisor) != null && c.Stores[s].Needed == 4;
        }
        Check(managed, "ladder: each store gets a manager and a supervisor from its most skilled staff, and needs the staff the game gave it");
        Check(CareerLadderRules.RegionalOf(c, 0) != null && CareerLadderRules.RegionalOf(c, 1) != null && CareerLadderRules.ExecutiveOf(c, ExecSeat.Coo) != null && CareerLadderRules.ExecutiveOf(c, ExecSeat.Chro) != null
              && CareerLadderRules.ExecutiveOf(c, ExecSeat.Cfo) != null, "ladder: a regional manager for each district and the three executives, as people of the company");
        Check(f.Rung == 0 && CareerLadderRules.ScoreOf(f) == CareerLadderRules.ScoreStart && CareerLadderRules.StaffAt(c, 0) == 4 && c.People[0].GameId == "g0_0",
            "ladder: the player starts on the floor at the job's store, whose staff are the game's own");
        Check(LadderStore.Write(LadderMade()) == LadderStore.Write(LadderMade()), "ladder: the same save seeds the same company on every machine (determinism)");
        var empty = new List<StoreSeed> { new StoreSeed { Street = "Main Street", Number = 1, Name = "Corner Coffee" } };
        var g = new LadderFile();
        CareerLadderRules.Seed(g, LadderJob(), "k", "Corner Coffee", "", empty, LadderSeed, LadderName, 20);
        Check(CareerLadderRules.StaffAt(g.Company, 0) == 2 && g.Company.Stores[0].Needed == 2, "ladder: a store the save lists no staff for is given two of the company's own, so it can be run");
        Check(CareerLadderRules.StartRung(LadderJob(ProCatalogue.Manager, 0)) == (int)Rung.Supervisor && CareerLadderRules.StartRung(LadderJob(ProCatalogue.Manager, 3)) == (int)Rung.StoreManager
              && CareerLadderRules.StartRung(LadderJob()) == (int)Rung.Frontline, "ladder: a manager's post starts above the floor, every other profession on it");
        Check(CareerLadderRules.Holds(LadderJob()) && !CareerLadderRules.Holds(LadderJob(ProCatalogue.Developer)), "ladder: a job at a store runs the ladder, an office profession's does not");
        Job pub = LadderJob();
        pub.Post.Kind = (int)EmployerKind.Public;
        Check(!CareerLadderRules.Holds(pub), "ladder: never the public sector");
        Check(f.Of(LadderJob()) && !f.Of(new Job { Hired = 11 }), "ladder: the record belongs to the job it was seeded for, a new job starts a new one");
        var sm = new LadderFile();
        CareerLadderRules.Seed(sm, LadderJob(ProCatalogue.Manager, 3), "k", "C", "", LadderStores(), LadderSeed, LadderName, 20);
        Check(sm.Rung == (int)Rung.StoreManager && CareerLadderRules.Holder(sm.Company, 0, (int)Rung.StoreManager) == null, "ladder: a store manager's job leaves no other store manager at the store");
    }

    // ---------------------------------------------------------------- who does what

    private static void LadderDutyChecks()
    {
        LadderFile f = LadderMade();
        Check(!CareerLadderRules.PlayerHolds(f, 1, 0) && !CareerLadderRules.PlayerHolds(f, 2, 0) && CareerLadderRules.InUnit(f, 0) && !CareerLadderRules.InUnit(f, 1), "ladder: on the floor nothing is the player's to run but their own");
        LadderFile s1 = LadderMade(1);
        Check(CareerLadderRules.PlayerHolds(s1, 1, 0) && !CareerLadderRules.PlayerHolds(s1, 1, 1) && !CareerLadderRules.PlayerHolds(s1, 2, 0) && CareerLadderRules.Holder(s1.Company, 0, (int)Rung.Supervisor) == null,
            "ladder: a supervisor runs their own store's shift; the supervisor who was there goes back to the floor");
        LadderFile s3 = LadderMade(3);
        Check(CareerLadderRules.InUnit(s3, 1) && CareerLadderRules.InUnit(s3, 2) && !CareerLadderRules.InUnit(s3, 3) && CareerLadderRules.RegionalOf(s3.Company, 0) == null && CareerLadderRules.RegionalOf(s3.Company, 1) != null,
            "ladder: a regional manager runs the stores of their district, and the regional manager of the other stays");
        Check(CareerLadderRules.Holder(s3.Company, 0, (int)Rung.StoreManager) != null, "ladder: the store the player leaves for the region gets a manager of its own");
        LadderFile s4 = LadderMade(4);
        Check(CareerLadderRules.InUnit(s4, 3) && s4.Seat == (int)ExecSeat.Coo && CareerLadderRules.ExecutiveOf(s4.Company, ExecSeat.Coo) == null && CareerLadderRules.RegionalOf(s4.Company, 0) != null,
            "ladder: an executive runs every store from their seat, whose holder moves on; the region they left gets a manager again");
        LadderFile one = LadderMade(3, 3);
        Check(CareerLadderRules.RegionScope(one.Company) == 0, "ladder: with every store in one district the region is that district");
        Check(CareerLadderRules.Superior(f) == CareerLadderRules.Holder(f.Company, 0, (int)Rung.Supervisor) && CareerLadderRules.Superior(s3) == CareerLadderRules.ExecutiveOf(s3.Company, ExecSeat.Coo)
              && CareerLadderRules.Superior(s4) == null, "ladder: the player answers to the rung above them, an executive to the owner");
    }

    // ---------------------------------------------------------------- a day of the stores

    private static void LadderDayChecks()
    {
        LadderFile f = LadderMade();
        for (int d = 21; d < 60; d++)
        {
            CareerLadderRules.Day(f, LadderSeed, d, LadderName);
        }
        Check(f.Tasks.Count == 0 && f.LastDay == 59, "ladder: on the floor the company runs itself; no task is the player's");
        LadderFile s = LadderMade(1);
        s.Company.People.Remove(CareerLadderRules.Holder(s.Company, 0, (int)Rung.Frontline) ?? FirstFloor(s.Company, 0));
        CareerLadderRules.Day(s, LadderSeed, 21, LadderName);
        Check(HasTask(s, TaskKind.Cover, 0), "ladder: a supervisor's store a hand short brings a gap on the shift to cover");
        CareerLadderRules.Day(s, LadderSeed, 22, LadderName);
        Check(s.Lapsed >= 1, "ladder: a shift's gap left to the day's end lapses and counts against the player");
        int before = s.Lapsed;
        LadderFile m = LadderMade(2);
        m.Company.People.Remove(FirstFloor(m.Company, 0));
        CareerLadderRules.Day(m, LadderSeed, 21, LadderName);
        Check(HasTask(m, TaskKind.Hire, 0), "ladder: a store manager's gap is a post to fill");
        for (int d = 22; d < 22 + CareerLadderRules.TaskDays + 1; d++)
        {
            CareerLadderRules.Day(m, LadderSeed, d, LadderName);
        }
        Check(m.Lapsed >= 1 && before >= 1, "ladder: a task left past its days lapses");
        Check(m.Company.Stores[0].GapDays >= m.Company.Policy.Escalate && !HasTask(m, TaskKind.Escalation, 0),
            "ladder: the player's own store's gap is theirs: never sent up past them to settle for them");
        LadderFile w = LadderMade();
        bool asked = false;
        for (int d = 21; d < 200 && !asked; d++)
        {
            CareerLadderRules.Day(w, LadderSeed, d, LadderName);
            for (int i = 0; i < w.Company.People.Count; i++)
            {
                asked |= w.Company.People[i].Want != (int)StaffWant.None;
            }
        }
        Check(asked, "ladder: the staff ask for things now and then (a raise, a shift, equipment, insurance)");
        LadderFile low = LadderMade();
        for (int i = 0; i < low.Company.People.Count; i++)
        {
            low.Company.People[i].Morale = low.Company.People[i].Store == 1 ? 5 : low.Company.People[i].Morale;
        }
        int staff1 = CareerLadderRules.StaffAt(low.Company, 1);
        for (int d = 21; d < 40; d++)
        {
            CareerLadderRules.Day(low, LadderSeed, d, LadderName);
        }
        Check(CareerLadderRules.StaffAt(low.Company, 1) <= staff1 + 2, "ladder: staff of low morale leave, and the company's managers take people on again");
        LadderFile reg = LadderMade(3);
        reg.Company.Stores[1].GapDays = reg.Company.Policy.Escalate;
        reg.Company.People.Remove(FirstFloor(reg.Company, 1));
        reg.Company.People.Remove(CareerLadderRules.Holder(reg.Company, 1, (int)Rung.StoreManager));
        CareerLadderRules.Day(reg, LadderSeed, 21, LadderName);
        Check(HasTask(reg, TaskKind.Escalation, 1) && HasTask(reg, TaskKind.Appoint, 1), "ladder: a store of the player's region with a post open too long and no manager sends both up to them");
        Check(CareerLadderRules.Holder(reg.Company, 1, (int)Rung.StoreManager) == null, "ladder: a store of the player's region waits for the player to appoint its manager");
        int week = f.Company.Stores[0].Spent = 500;
        CareerLadderRules.Day(f, LadderSeed, 63, LadderName);
        Check(f.Company.Stores[0].Spent < week, "ladder: each week's budget starts again");
    }

    private static Colleague FirstFloor(CityCompany c, int s)
    {
        for (int i = 0; i < c.People.Count; i++)
        {
            if (c.People[i].Store == s && c.People[i].Role == (int)Rung.Frontline)
            {
                return c.People[i];
            }
        }
        return null;
    }

    private static bool HasTask(LadderFile f, TaskKind kind, int store)
    {
        for (int i = 0; i < f.Tasks.Count; i++)
        {
            if (f.Tasks[i].Kind == (int)kind && f.Tasks[i].Store == store)
            {
                return true;
            }
        }
        return false;
    }

    // ---------------------------------------------------------------- every task of every rung decided

    private static void LadderTaskChecks()
    {
        for (int rung = 1; rung <= LadderRanks.CareerTop; rung++)
        {
            for (int seat = 1; seat <= (rung == LadderRanks.CareerTop ? 3 : 1); seat++)
            {
                LadderFile f = LadderMade(Math.Min(rung, 3));
                if (rung == LadderRanks.CareerTop)
                {
                    CareerLadderRules.Promote(f, 20, (ExecSeat)seat, LadderSeed, LadderName);
                }
                int brought = CareerLadderRules.BringAll(f, 30);
                int good = 0, count = f.Tasks.Count;
                var tasks = new List<LadderTask>(f.Tasks);
                var bad = new List<string>();
                foreach (LadderTask t in tasks)
                {
                    if (CareerLadderRules.DecideFirst(f, t, LadderSeed, 30, LadderName, out string what))
                    {
                        good++;
                    }
                    else
                    {
                        bad.Add((TaskKind)t.Kind + " " + what);
                    }
                }
                Check(brought > 0 && good == count && f.Tasks.Count == 0 && f.Handled == count,
                    "ladder: every task of a " + LadderRanks.Of(f.Rung).Key + (rung == LadderRanks.CareerTop ? " (" + (ExecSeat)seat + ")" : "") + " decided does what it should (" + good + " of " + count
                    + "; " + string.Join("; ", bad) + ")");
            }
        }
        LadderFile k = LadderMade(1);
        CareerLadderRules.BringAll(k, 30);
        int kinds = 0;
        foreach (LadderTask t in k.Tasks)
        {
            kinds |= 1 << t.Kind;
        }
        Check((kinds & (1 << (int)TaskKind.Cover)) != 0 && (kinds & (1 << (int)TaskKind.Demand)) != 0, "ladder: a supervisor's tasks are the shift's gap and the staff's requests");
        LadderFile r = LadderMade(3);
        CareerLadderRules.BringAll(r, 30);
        kinds = 0;
        foreach (LadderTask t in r.Tasks)
        {
            kinds |= 1 << t.Kind;
        }
        Check((kinds & (1 << (int)TaskKind.Escalation)) != 0 && (kinds & (1 << (int)TaskKind.Appoint)) != 0, "ladder: a regional manager's tasks are what is sent up and a manager to appoint");
        LadderFile h = LadderMade(2);
        CareerLadderRules.BringAll(h, 30);
        LadderTask hire = null;
        foreach (LadderTask t in h.Tasks)
        {
            hire = t.Kind == (int)TaskKind.Hire ? t : hire;
        }
        h.Company.Policy.Floor = 95;
        string why = hire != null ? CareerLadderRules.Answer(h, hire.Id, Verdict.Yes, 0, LadderSeed, 30, LadderName) : "none";
        Check(why == "reallife_ladder_why_floor" || why == "reallife_ladder_why_wage", "ladder: a candidate outside the hiring policy cannot be taken on (" + why + ")");
        CareerLadderRules.Candidate(LadderSeed, 7, 1, out int sk1, out int wg1);
        CareerLadderRules.Candidate(LadderSeed, 7, 1, out int sk2, out int wg2);
        Check(sk1 == sk2 && wg1 == wg2 && sk1 >= 20 && sk1 <= 90, "ladder: a post's candidates are the same each time they are looked at");
        LadderFile cover = LadderMade(1);
        CareerLadderRules.BringAll(cover, 30);
        LadderTask ct = null;
        foreach (LadderTask t in cover.Tasks)
        {
            ct = t.Kind == (int)TaskKind.Cover ? t : ct;
        }
        int moraleBefore = CareerLadderRules.MoraleAt(cover.Company, 0);
        Check(ct != null && CareerLadderRules.Answer(cover, ct.Id, Verdict.No, -1, LadderSeed, 30, LadderName) == null && CareerLadderRules.MoraleAt(cover.Company, 0) < moraleBefore,
            "ladder: a shift run short costs the shift's morale");
    }

    // ---------------------------------------------------------------- the powers a rung has not

    private static void LadderPowerChecks()
    {
        LadderFile s = LadderMade(1);
        CareerLadderRules.BringAll(s, 30);
        LadderTask raise = null;
        foreach (LadderTask t in s.Tasks)
        {
            raise = t.Kind == (int)TaskKind.Demand && t.About == (int)StaffWant.Raise ? t : raise;
        }
        Check(raise != null && CareerLadderRules.Answer(s, raise.Id, Verdict.Yes, -1, LadderSeed, 30, LadderName) == "reallife_ladder_why_raise_power",
            "ladder: a supervisor cannot give a raise: they pass it up");
        Check(raise != null && CareerLadderRules.Answer(s, raise.Id, Verdict.Up, -1, LadderSeed, 30, LadderName) == null, "ladder: ...and passing it up has the store manager decide it");
        Check(CareerLadderRules.SetRota(s, 2) == "reallife_ladder_why_power" && CareerLadderRules.RaiseOne(s, s.Company.People[0].Id, 30) == "reallife_ladder_why_raise_power",
            "ladder: a supervisor sets no schedule and gives no raise");
        LadderFile m = LadderMade(2);
        Check(CareerLadderRules.SetRota(m, 2) == "reallife_ladder_why_policy_rota" && CareerLadderRules.SetRota(m, 0) == null && m.Company.Stores[0].Rota == 0,
            "ladder: a store manager sets the schedule within the company's staffing policy and no fuller");
        Colleague p = FirstFloor(m.Company, 0);
        int wage = p.Wage;
        p.RaisedDay = -1000;
        Check(CareerLadderRules.RaiseOne(m, p.Id, 30) == null && p.Wage == wage * (10000 + m.Company.Policy.Band) / 10000 && CareerLadderRules.RaiseOne(m, p.Id, 31) == "reallife_ladder_why_raised",
            "ladder: a store manager's raise is the policy's band, once in a while");
        Check(CareerLadderRules.Move(m, p.Id, 1) == "reallife_ladder_why_power", "ladder: a store manager moves nobody between stores");
        LadderFile r = LadderMade(3);
        Colleague q = FirstFloor(r.Company, 1);
        Check(CareerLadderRules.Move(r, q.Id, 2) == null && q.Store == 2 && CareerLadderRules.Move(r, FirstFloor(r.Company, 3).Id, 1) == "reallife_ladder_why_pick",
            "ladder: a regional manager moves staff between the stores of their region, not from another's");
        Check(CareerLadderRules.SetPolicy(r, PolicyField.Budget, 1) == "reallife_ladder_why_power", "ladder: a regional manager sets no policy");
        LadderFile e = LadderMade(4);
        int budget = e.Company.Policy.Budget, staffing = e.Company.Policy.Staffing;
        Check(CareerLadderRules.SetPolicy(e, PolicyField.Budget, 1) == "reallife_ladder_why_seat" && CareerLadderRules.SetPolicy(e, PolicyField.Staffing, 1) == null
              && e.Company.Policy.Staffing == staffing + 1 && e.Company.Policy.Budget == budget, "ladder: an executive sets the policy of their seat only");
        Check(CareerLadderRules.SetPolicy(e, PolicyField.Staffing, 1) == "reallife_ladder_why_policy_end", "ladder: a policy goes no further than its most");
        CareerLadderRules.SetPolicy(e, PolicyField.Staffing, -1);
        CareerLadderRules.SetPolicy(e, PolicyField.Staffing, -1);
        bool lean = true;
        for (int i = 0; i < e.Company.Stores.Count; i++)
        {
            lean &= e.Company.Stores[i].Rota == 0;
        }
        Check(lean, "ladder: a leaner staffing policy brings every store's schedule down to it");
        CareerLadderRules.BringAll(e, 30);
        Check(e.Tasks.Count > 0 && CareerLadderRules.Answer(e, e.Tasks[0].Id, Verdict.Up, -1, LadderSeed, 30, LadderName) == "reallife_ladder_why_yours",
            "ladder: an executive has nobody to pass a matter up to");
        LadderFile floor = LadderMade();
        floor.Tasks.Add(new LadderTask { Id = 99, Kind = (int)TaskKind.Hire, Store = 0, Lapse = 40 });
        Check(CareerLadderRules.Answer(floor, 99, Verdict.Yes, 0, LadderSeed, 30, LadderName) == "reallife_ladder_why_power", "ladder: on the floor the player hires nobody");
    }

    // ---------------------------------------------------------------- promotion

    private static void LadderPromotionChecks()
    {
        LadderFile f = LadderMade();
        Func<int, int> none = s => 0, all = s => 100;
        Check(CareerLadderRules.WhyNot(f, 20, 60, all) == "reallife_ladder_why_days", "ladder: no promotion before the time in the rank");
        int day = 20 + CareerLadderRules.DaysNeeded(f, 60);
        f.Score = 400;
        Check(CareerLadderRules.WhyNot(f, day, 60, all) == "reallife_ladder_why_score", "ladder: ...nor under the next rung's performance score");
        f.Score = 900;
        Check(CareerLadderRules.WhyNot(f, day, 60, none) == "reallife_ladder_why_skills", "ladder: ...nor without the next rung's skills");
        f.Aiming = false;
        Check(CareerLadderRules.WhyNot(f, day, 60, all) == "reallife_ladder_why_not_aiming", "ladder: ...nor with the player's name not put forward");
        f.Aiming = true;
        Check(CareerLadderRules.WhyNot(f, day, 60, all) == null, "ladder: with all of it the player can be promoted at the review");
        int days = CareerLadderRules.DaysNeeded(f, 60);
        f.Recommended = true;
        Check(CareerLadderRules.DaysNeeded(f, 60) == days * 2 / 3, "ladder: a recommendation takes a third off the days below the top");
        LadderFile x = LadderMade(3);
        x.Score = 1000;
        x.RungDay = 0;
        x.Recommended = false;
        Check(CareerLadderRules.WhyNot(x, 500, 60, all) == "reallife_ladder_why_recommend", "ladder: an executive's seat needs the recommendation");
        x.Lapsed = 0;
        CareerLadderRules.Reviewed(x);
        Check(x.Recommended && CareerLadderRules.WhyNot(x, 500, 60, all) == null, "ladder: a review with a score five over and nothing lapsed brings it");
        LadderFile y = LadderMade(3);
        y.Score = 1000;
        y.Lapsed = 1;
        CareerLadderRules.Reviewed(y);
        Check(!y.Recommended, "ladder: no recommendation with a task left to lapse");
        LadderFile one = LadderMade(2, 1);
        one.Score = 1000;
        one.RungDay = 0;
        Check(CareerLadderRules.WhyNot(one, 500, 60, all) == "reallife_ladder_why_stores", "ladder: a company of one store has no regional manager to be promoted to");
        LadderFile p = LadderMade(1);
        p.Tasks.Add(new LadderTask { Id = 77, Kind = (int)TaskKind.Cover, Store = 0, Lapse = 99 });
        Colleague old = CareerLadderRules.Holder(p.Company, 0, (int)Rung.StoreManager);
        CareerLadderRules.Promote(p, 30, ExecSeat.None, LadderSeed, LadderName);
        Check(p.Rung == (int)Rung.StoreManager && p.RungDay == 30 && p.Tasks.Count == 0 && CareerLadderRules.Holder(p.Company, 0, (int)Rung.StoreManager) == null && (old.Store != 0 || !p.Company.People.Contains(old)),
            "ladder: promoted to store manager, the old manager moves to a store without one or leaves, and the old rung's tasks go to whoever holds it now");
        LadderFile top = LadderMade(4);
        CareerLadderRules.Promote(top, 40, ExecSeat.Cfo, LadderSeed, LadderName);
        Check(top.Rung == (int)Rung.Executive && CareerLadderRules.WhyNot(top, 500, 60, all) == "reallife_ladder_why_top", "ladder: no rung past an executive");
        LadderFile b = LadderMade();
        b.Score = 1000;
        Check(CareerLadderRules.ReviewBonus(b) == CareerLadderRules.ReviewBonusMost && CareerLadderRules.ReviewBonus(null) == 0, "ladder: the score moves a review by its most at best, nothing away from the ladder");
        b.Score = 0;
        Check(CareerLadderRules.ReviewBonus(b) == -CareerLadderRules.ReviewBonusMost, "ladder: ...and as far the other way at worst");
        b.Score = CareerLadderRules.ScoreStart * 10;
        for (int d = 0; d < 40; d++)
        {
            CareerLadderRules.Mark(b, 1000, 21 + d);
        }
        Check(CareerLadderRules.ScoreOf(b) > CareerLadderRules.ScoreStart && CareerLadderRules.ScoreOf(b) <= 100, "ladder: turning up every day lifts the score on the floor, never past 100");
    }

    // ---------------------------------------------------------------- the player's own requests

    private static void LadderRequestChecks()
    {
        LadderFile f = LadderMade();
        Check(CareerLadderRules.Ask(f, StaffWant.Raise, 1, 30) == "reallife_ladder_why_ask_kind" && CareerLadderRules.Ask(f, StaffWant.Equipment, 0, 30) == null
              && CareerLadderRules.Ask(f, StaffWant.Shift, 1, 30) == "reallife_ladder_why_asked", "ladder: one request at a time, a shift, equipment or insurance (a raise is the review's)");
        Check(!CareerLadderRules.AnswerAsk(f, LadderSeed, 30, out bool _), "ladder: a request is answered the day after, not the same day");
        f.Score = 1000;
        int granted = 0, denied = 0;
        for (int seed = 1; seed < 200; seed++)
        {
            LadderFile g = LadderMade();
            g.Score = 1000;
            CareerLadderRules.Ask(g, StaffWant.Equipment, 0, 30);
            CareerLadderRules.AnswerAsk(g, seed, 31, out bool yes);
            granted += yes && g.Equipped && g.Company.Stores[0].Spent == CareerLadderRules.EquipmentCost ? 1 : 0;
            denied += !yes && !g.Equipped ? 1 : 0;
        }
        Check(granted + denied == 199 && granted > 150 && denied > 0, "ladder: a good score is mostly a yes, never always; a yes is paid from the store's budget (" + granted + " granted)");
        LadderFile broke = LadderMade();
        broke.Score = 1000;
        broke.Company.Stores[0].Spent = broke.Company.Policy.Budget;
        CareerLadderRules.Ask(broke, StaffWant.Insurance, 0, 30);
        Check(CareerLadderRules.AnswerAsk(broke, LadderSeed, 31, out bool paid) && !paid && !broke.Insured && broke.Ask == (int)StaffWant.None, "ladder: over the store's budget the answer is no");
        LadderFile s = LadderMade();
        s.Score = 1000;
        bool moved = false;
        for (int seed = 1; seed < 20 && !moved; seed++)
        {
            s.Ask = 0;
            CareerLadderRules.Ask(s, StaffWant.Shift, -1, 30);
            CareerLadderRules.AnswerAsk(s, seed, 31, out bool yes);
            moved = yes && s.Prefer == -1;
        }
        Check(moved, "ladder: a shift granted moves the job's start an hour (the opening hours fit it: CareersDirector.Hours)");
        s.Prefer = -2;
        s.Ask = 0;
        CareerLadderRules.Ask(s, StaffWant.Shift, -1, 30);
        CareerLadderRules.AnswerAsk(s, 3, 31, out bool _);
        Check(s.Prefer >= -2, "ladder: two hours either way at most");
        Check(CareerLadderRules.EquippedLearn > 1000 && CareerLadderRules.InsuredPensionBp > 0, "ladder: equipment teaches more, insurance pays more into the pension");
    }

    // ---------------------------------------------------------------- the save

    private static void LadderSaveChecks()
    {
        LadderFile f = LadderMade(3);
        for (int d = 21; d < 35; d++)
        {
            CareerLadderRules.Day(f, LadderSeed, d, LadderName);
        }
        CareerLadderRules.BringAll(f, 35);
        f.Prefer = 1;
        f.Insured = true;
        f.Aim = (int)ExecSeat.Chro;
        string line = LadderStore.Write(f);
        LadderFile back = LadderStore.Read(line, out int version);
        Check(version == LadderStore.Version && LadderStore.Write(back) == line, "ladder: the save line read back writes the same line (round trip)");
        Check(back.Rung == 3 && back.Prefer == 1 && back.Insured && back.Aim == (int)ExecSeat.Chro && back.Company.Stores.Count == f.Company.Stores.Count && back.Tasks.Count == f.Tasks.Count
              && back.Company.People.Count == f.Company.People.Count && back.Of(LadderJob()), "ladder: the rung, the requests granted, the company and its tasks come back as saved");
        LadderFile old = LadderStore.Read("v=1~h:10|Main Street|1|Corner Coffee|2", out int _);
        Check(old.Rung == 2 && old.Aiming && old.Score == CareerLadderRules.ScoreStart * 10 && old.Company.Policy.Budget == 600 && old.Tasks.Count == 0,
            "ladder: a line with fewer fields (an older version) reads the rest as their defaults");
        var data = new Dictionary<string, string> { [LadderStore.Key] = "this is not a ladder line" };
        LadderFile none = LadderStore.Load(data, out int _);
        Check(none == null && data.ContainsKey(LadderStore.Key), "ladder: a line that cannot be read is kept as it was read");
        Check(LadderStore.Key == "reallife:career_ladder" && LadderStore.Key != CareersStore.Key, "ladder: its own line of the save, apart from the careers' so a save without it loads as before");
    }

    // ---------------------------------------------------------------- the company against the save

    private static void LadderKeepChecks()
    {
        LadderFile f = LadderMade(3);
        int people = f.Company.People.Count, at3 = CareerLadderRules.StaffAt(f.Company, 3);
        f.Tasks.Add(new LadderTask { Id = 50, Kind = (int)TaskKind.Escalation, Store = 3, Lapse = 99 });
        f.Tasks.Add(new LadderTask { Id = 51, Kind = (int)TaskKind.Escalation, Store = 2, Lapse = 99 });
        CareerLadderRules.DropStore(f, 2);
        Check(f.Company.Stores.Count == 3 && f.TaskOf(51) == null && f.TaskOf(50) != null && f.TaskOf(50).Store == 2 && CareerLadderRules.StaffAt(f.Company, 2) == at3,
            "ladder: a store no longer the company's goes with its staff and tasks; the stores after it move up");
        CareerLadderRules.DropStore(f, 0);
        Check(f.Company.Stores.Count == 3, "ladder: the player's own store is never let go");
        var ids = new HashSet<string> { "g0_0", "g0_1", "g0_3" };
        int before = CareerLadderRules.StaffAt(f.Company, 0);
        int left = CareerLadderRules.KeepStaff(f, 0, ids);
        Check(left == 1 && CareerLadderRules.StaffAt(f.Company, 0) == before - 1, "ladder: a member of the game's staff the game no longer has at the store (poached, say) has left the company too");
        Check(CareerLadderRules.KeepStaff(f, 0, new HashSet<string>()) == 0, "ladder: a store the game lists nobody at is left as it is");
        Check(people > 0, "ladder: the company had people to keep");
    }

    // ---------------------------------------------------------------- timing

    private static void LadderTiming()
    {
        LadderFile f = LadderMade(4, CareerLadderRules.StoresMost, CareerLadderRules.StaffMost);
        CareerLadderRules.Day(f, LadderSeed, 21, LadderName);
        var timer = Stopwatch.StartNew();
        const int days = 200;
        for (int d = 22; d < 22 + days; d++)
        {
            CareerLadderRules.Day(f, LadderSeed, d, LadderName);
            CareerLadderRules.Mark(f, 1000, d);
        }
        timer.Stop();
        double ms = timer.Elapsed.TotalMilliseconds / days;
        Check(ms < 2.0, "ladder: a day of the largest company modelled (" + CareerLadderRules.StoresMost + " stores of " + CareerLadderRules.StaffMost + ") takes under 2 ms (" + ms.ToString("0.000") + " ms)");
    }

    // ---------------------------------------------------------------- the sources

    private static void LadderSources()
    {
        string life = ModSource(@"Careers\CareersDirector.Life.cs"), work = ModSource(@"Careers\CareersDirector.Work.cs"), hours = ModSource(@"Careers\CareersDirector.Hours.cs");
        string game = ModSource(@"Careers\Ladder\LadderGame.cs"), study = ModSource(@"Careers\CareersDirector.Study.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        string settings = ModSource(@"Core\Settings.cs"), app = ModSource(@"Careers\CareerApp.cs"), desk = ModSource(@"Careers\CareersDirector.DeskDay.cs");
        Check(life.Contains("LadderReviewBonus()") && life.Contains("LadderReview(rating, day)") && life.Contains("!LadderManages()") && life.Contains("LadderPromote(j, day)")
              && life.Contains("LadderPension()"), "ladder: the job's review reads the score, promotes on the ladder, keeps a manager off the profession's ranks, and insurance adds to the pension");
        int ladderDay = work.IndexOf("LadderDay(day);", StringComparison.Ordinal), review = work.IndexOf("WorkDayMore(day);", StringComparison.Ordinal);
        Check(ladderDay > 0 && review > ladderDay && work.Contains("LadderWorked(j, minutes, day)") && work.Contains("LadderLearn()") && work.Contains("LadderKeep(Save.Day)"),
            "ladder: the company's day runs each morning before the review; a day worked and the equipment count");
        Check(hours.Contains("Fitted(own, j.Post, LadderPrefer(j))") && hours.Contains("int prefer = 0"), "ladder: a shift granted moves the job's start through the opening hours' own fit");
        Check(study.Contains("LadderWrite(data)"), "ladder: its line is written with the careers' at every save");
        Check(game.Contains("RentedByPlayer") && !System.Text.RegularExpressions.Regex.IsMatch(game, @"(aiEmployees|BusinessName|businessTypeName|businessOwnerRivalId|scheduleDays|satisfaction)\s*=[^=]")
              && !game.Contains(".Add(new AiBusinessEmployeeData") && !game.Contains("ReplaceAiBusinessEmployee"),
            "ladder: the game's businesses and staff are only read, never written, and no player's business (Going Public shows another's as rented by the player) is a store of the company");
        foreach (string test in new[] { "TestLadderJob", "TestLadderPromote", "TestLadderTasks", "TestLadderDecide", "TestLadderAsk" })
        {
            Check(mod.Contains("CareersDirector.Instance?." + test + "()"), "ladder: Options > Tests has " + test);
        }
        Check(settings.Contains("CareersLadderId = \"careers_ladder\"") && settings.Contains("public static bool CareersLadder = true;"), "ladder: its option, on by default");
        Check(app.Contains("\"reallife_career_tab_ladder\"") && app.Contains("WriteLadder(box, w, day)") && desk.Contains("reallife_ladder_desk_tasks"),
            "ladder: the Career app's Management tab, and the desk says what waits");
    }

    // ---------------------------------------------------------------- the texts

    private static void LadderTexts()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        var keys = new List<string> { "reallife_career_tab_ladder", "reallife_meta_title_careers_ladder", "reallife_guide_careers_ladder", "reallife_guide_careers_ladder_about", "reallife_ladder_enabled" };
        for (int r = 0; r < LadderRanks.All.Length; r++)
        {
            keys.Add(LadderRanks.TitleKey(r, ExecSeat.None));
        }
        for (int s = 1; s <= 3; s++)
        {
            keys.Add(LadderRanks.TitleKey(4, (ExecSeat)s));
        }
        foreach (RungPower p in LadderRanks.Listed)
        {
            keys.Add(LadderRanks.PowerKey(p));
        }
        foreach (string k in new[] { "cover", "hire", "demand", "appoint", "escalation_gap", "escalation_morale", "escalation_budget" })
        {
            keys.Add("reallife_ladder_do_" + k + "_about");
            keys.Add("reallife_ladder_do_" + k + "_yes");
            keys.Add("reallife_ladder_do_" + k + "_no");
        }
        foreach (string w in new[] { "none", "raise", "shift", "equipment", "insurance" })
        {
            keys.Add("reallife_ladder_want_" + w);
        }
        for (int f = 0; f <= (int)PolicyField.Budget; f++)
        {
            keys.Add("reallife_ladder_policy_" + ((PolicyField)f).ToString().ToLowerInvariant());
        }
        var lacking = new List<string>();
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> own = LoadWords(System.IO.Path.Combine(LocalesFolder(), code + ".json"));
            foreach (string key in keys)
            {
                if (own == null || en == null || !own.TryGetValue(key, out string words) || words.Length == 0 || !en.TryGetValue(key, out string english) || !SamePlaceholders(words, english))
                {
                    lacking.Add(code + " " + key);
                }
            }
            if (own != null)
            {
                foreach (KeyValuePair<string, string> kv in own)
                {
                    if (kv.Key.StartsWith("reallife_ladder_note_", StringComparison.Ordinal) && code == "en" && kv.Value.Length > 240)
                    {
                        lacking.Add(code + " " + kv.Key + " (over 240)");
                    }
                }
            }
        }
        Check(lacking.Count == 0, "ladder: every rung, seat, power, decision, demand and policy text is in en, pt and zh-cn with the same placeholders (lacking: " + string.Join(", ", lacking) + ")");
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("ladder", langs, codes))
        {
            return;
        }
        var missing = new List<string>();
        for (int l = 0; l < codes.Length; l++)
        {
            if (!LadderLines.TryGetValue(codes[l], out string wanted) || !WhatsNewHas(langs[l], wanted))
            {
                missing.Add(codes[l]);
            }
        }
        Check(missing.Count == 0 && codes.Length == LadderLines.Count, "ladder: What's new has the feature's line in some reallife_changes_ row of every language (lacking: " + string.Join(", ", missing) + ")");
    }
}
